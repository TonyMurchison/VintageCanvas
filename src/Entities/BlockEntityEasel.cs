using Cairo;
using Cairo.Freetype;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.Server;
using static System.Net.Mime.MediaTypeNames;

namespace VintageCanvas.src.Entities
{
    internal class BlockEntityEasel : BlockEntity
    {
        InventoryGeneric canvasinventory;
        ITreeAttribute cachedTree;
        ICoreClientAPI capi;
        public int[] pixeldata;
        public HashSet<int> changedpixels = new HashSet<int>();
        public int? canvasId;
        public int canvasSize = 32;
        public string easelName = "vintagecanvas:easel";
        public string textureName = "canvas.png";
        public string allowedCanvas = "canvas";
        private MeshData clientMesh;

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if(this is BlockEntityEaselH)
            {
                canvasSize = 64;
            }

            //Load trees explicitly
            if (canvasinventory == null)
            {
                canvasinventory = new InventoryGeneric(1, "easel-canvas", api);
            }
            if (cachedTree != null)
            {                
                canvasinventory.FromTreeAttributes(cachedTree);
                canvasinventory?.ResolveBlocksOrItems();
                if (cachedTree.HasAttribute("vc_pixeldata"))
                {
                    byte[] pixelbytes = cachedTree.GetBytes("vc_pixeldata");
                    if (pixelbytes != null)
                    {
                        pixeldata = TextureUtil.ReadCompressedPixelData(pixelbytes);
                    }
                }                
            }


            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }

            if (pixeldata != null)
            {
                RegisterDelayedCallback(dt =>
                {
                    SynchroniseTexture();
                    UpdateTexture();
                }, 200);
            }
        }

        public ItemSlot CanvasSlot => canvasinventory[0];

        public void PlaceCanvas(ItemStack canvas)
        {
            if (this is BlockEntityEaselH)
            {
                canvasSize = 64;
                textureName = "canvas-large.png";
            }

            if (!canvas.Collectible.Code.PathStartsWith(allowedCanvas))
            {
                return;
            }

            canvasinventory[0].Itemstack = canvas;
            canvasId = canvas.Attributes.GetAsInt("canvasid");

            if (Api.Side == EnumAppSide.Client)
            {
                //If canvas has pixeldata: load and set local
                if (canvas.Attributes.HasAttribute("vc_pixeldata"))
                {
                    byte[] serialisedpixeldata = canvas.Attributes.GetBytes("vc_pixeldata");
                    pixeldata = TextureUtil.ReadCompressedPixelData(serialisedpixeldata);
                    SynchroniseTexture();
                }
                //Else, initialise pixeldata as canvas default
                else
                {                    
                    pixeldata = capi.Assets.Get(new AssetLocation("vintagecanvas:textures/block/") + textureName).ToBitmap(capi).Pixels;
                    SynchroniseTexture();
                }
            }

            UpdateShape();
            canvasinventory.MarkSlotDirty(0);
            MarkDirty(true);
            
            if (canvas.Attributes.HasAttribute("vc_pixeldata"))
            {
                UpdateTexture();
            }
        }

        public void GetCanvas(IPlayer byPlayer)
        {
            var canvasStack = CanvasSlot.Itemstack.Clone();

            if (pixeldata != null)
            {
                var cdata = TextureUtil.WriteCompressedPixelData(pixeldata);
                canvasStack.Attributes.SetBytes("vc_pixeldata", cdata);
            }

            canvasStack.Attributes.SetBool("vc_rendered", false);

            byPlayer.InventoryManager.TryGiveItemstack(canvasStack);
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            canvasinventory[0].Itemstack = null;
            canvasinventory[0].MarkDirty();
            canvasId = null;
            clientMesh = null;
            pixeldata = null;
            UpdateShape();
            MarkDirty(true);            
        }


        //Changes the source block shape when adding/removing canvases
        private void UpdateShape()
        {           
            string side = Block.Variant["side"];
            string woodtype = Block.Variant["woodtype"];
            string ratio = "none";
            if (!canvasinventory[0].Empty)
            {
                ratio = canvasinventory[0].Itemstack.Collectible.Variant["ratio"];
            }
            Block newBlock = Api.World.GetBlock(new AssetLocation(easelName + "-" + woodtype + "-" + ratio + "-" + side));

            if (newBlock != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
            }
        }
        
        //Pushes textures from client side to server side, then forces texture update through FromTree
        public void SynchroniseTexture()
        {
            if (capi != null)
            {
                BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(Pos);
                VintageCanvasModSystem.NetworkHandler.SendPixelData(Pos, TextureUtil.WriteCompressedPixelData(pixeldata), 0);
                MarkDirty(true);
            }
        }

        //Convert pixeldata to bitmap, push into texture atlas location, tesselate custom mesh with rerouted "painting" texture target
        public void UpdateTexture()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                if(CanvasSlot.Itemstack == null || pixeldata == null || canvasId == null)
                {
                    return;
                }

                AssetLocation texLoc = new AssetLocation("vintagecanvas", canvasId.ToString());

                clientMesh = TextureUtil.SwapPaintingTextureMesh(
                    pixeldata,
                    canvasSize,
                    texLoc,
                    capi.Tesselator.GetTextureSource(Block),
                    Block.Code,
                    Block.Shape,
                    capi
                    );
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (clientMesh != null)
            {
                mesher.AddMeshData(clientMesh.Clone());
                return true;   // skip default block mesh
            }
            
            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            try
            {
                base.ToTreeAttributes(tree);
                canvasinventory.ToTreeAttributes(tree);
                if (pixeldata != null)
                {
                    try
                    {
                        tree.SetBytes("vc_pixeldata",TextureUtil.WriteCompressedPixelData(pixeldata));
                    }
                    catch
                    {
                        Api.World.Logger.Error("Failed to write pixeldata to tree");
                    }
                }
                if (canvasId != null)
                {
                    tree.SetInt("canvasid", (int)canvasId);
                }
            }
            catch
            {
                Api.World.Logger.Error("Error in ToTreeAttributes");
            }
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            try
            {
                base.FromTreeAttributes(tree, worldForResolving);
                if (tree.HasAttribute("canvasid"))
                {
                    canvasId = tree.GetAsInt("canvasid");
                }
                cachedTree = tree.Clone();

                canvasinventory?.FromTreeAttributes(tree);
                canvasinventory?.ResolveBlocksOrItems();

                if (capi != null)
                {
                    UpdateShape();
                }

                if (tree.HasAttribute("vc_pixeldata"))
                {
                    pixeldata = TextureUtil.ReadCompressedPixelData(tree.GetBytes("vc_pixeldata"));                    
                    if(capi != null) { 
                        UpdateTexture();
                    }
                }
                
            }
            catch
            {
                Api.World.Logger.Error("Error in FromTreeAttributes");
            }
        }

        public void UpdatePixelData(int[] pixeldata)
        {
            this.pixeldata = pixeldata;
            MarkDirty(true);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            clientMesh?.Dispose();
        }
    }
}
