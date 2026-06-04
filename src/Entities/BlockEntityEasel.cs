using Cairo.Freetype;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
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
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.Server;

namespace VintageCanvas.src.Entities
{
    internal class BlockEntityEasel : BlockEntity
    {
        InventoryGeneric canvasinventory;
        ITreeAttribute cachedTree;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        ITextureAtlasAPI atlas;
        public int[] pixeldata;
        public HashSet<int> changedpixels = new HashSet<int>();
        public int? canvasId;
        BitmapRef bmp;
        private MeshData clientMesh;
        int? tsubid;
        TextureAtlasPosition TexPos;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            //Load trees explicitly
            if (canvasinventory == null)
            {
                canvasinventory = new InventoryGeneric(1, "easel-canvas", api);
            }
            if (cachedTree != null)
            {                
                canvasinventory.FromTreeAttributes(cachedTree);
                canvasinventory?.ResolveBlocksOrItems();
                byte[] pixelbytes = cachedTree.GetBytes("vc_pixeldata");
                if (pixelbytes != null)
                {
                    pixeldata = SerializerUtil.Deserialize<int[]>(pixelbytes);
                }
            }


            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                atlas = capi.BlockTextureAtlas;
            }

            if (pixeldata != null)
            {
                RegisterDelayedCallback(dt =>
                {
                    UpdateTexture();
                }, 200);
            }
        }

        public ItemSlot CanvasSlot => canvasinventory[0];

        public void PlaceCanvas(ItemStack canvas)
        {
            canvasinventory[0].Itemstack = canvas;
            canvasId = canvas.Attributes.GetAsInt("canvasid");

            if (Api.Side == EnumAppSide.Client)
            {
                //If canvas has pixeldata: load and set local
                if (canvas.Attributes.HasAttribute("vc_pixeldata"))
                {
                    byte[] serialisedpixeldata = canvas.Attributes.GetBytes("vc_pixeldata");
                    pixeldata = SerializerUtil.Deserialize<int[]>(serialisedpixeldata);
                }
                //Else, initialise pixeldata as canvas default
                else
                {
                    bmp = capi.Assets.Get(new AssetLocation("vintagecanvas:textures/block/canvas.png")).ToBitmap(capi);
                    pixeldata = bmp.Pixels;
                }
            }
            if (Api.Side == EnumAppSide.Client)
            {
                Api.World.Logger.Debug("Canvas placed: " + canvasId);
            }

            UpdateShape();
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
                var pdata = SerializerUtil.Serialize(pixeldata);
                canvasStack.Attributes.SetBytes("vc_pixeldata", pdata);
            }

            canvasStack.Attributes.SetBool("vc_rendered", false);

            byPlayer.InventoryManager.TryGiveItemstack(canvasStack);
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            canvasinventory[0].Itemstack = null;
            canvasId = null;
            clientMesh = null;
            UpdateShape();
            pixeldata = null;
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
            Block newBlock = Api.World.GetBlock(new AssetLocation("vintagecanvas:easel-" + woodtype + "-" + ratio + "-" + side));

            if (newBlock != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
            }
        }

        public void PaintPixels(int[] pixelindices, int color, float alpha)
        {
            if (pixeldata == null) { Api.World.Logger.Error("Canvas pixel data not initiated"); return; }
            for (int i = 0; i < pixelindices.Length; i++)
            {
                if (0 <= pixelindices[i] && 1023 >= pixelindices[i])
                {
                    if (!changedpixels.Contains(pixelindices[i]))
                    {
                        int newColor = TextureUtil.BlendColor(color, pixeldata[pixelindices[i]], alpha);
                        pixeldata[pixelindices[i]] = newColor;
                        changedpixels.Add(pixelindices[i]);
                    }
                }
            }
            UpdateTexture();
        }

        //Convert pixeldata to bitmap, push into texture atlas location, tesselate custom mesh with rerouted "painting" texture target
        private void UpdateTexture()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                if(CanvasSlot.Itemstack == null)
                {
                    return;
                }
                //send pixeldata to server
                VintageCanvasModSystem.NetworkHandler.SendPixelData(Pos, SerializerUtil.Serialize(pixeldata));
                if(pixeldata == null)
                {
                    return;
                }        
                
                if (canvasId == null)
                {
                    Api.World.Logger.Error("Easel canvas tried to update without canvasId");
                    return;
                }

                AssetLocation texLoc = new AssetLocation("vintagecanvas", canvasId.ToString());

                clientMesh = TextureUtil.SwapPaintingTexture(
                    pixeldata,
                    32,
                    texLoc,
                    capi.Tesselator.GetTextureSource(Block),
                    Block.Code,
                    Block.Shape,
                    capi
                    );                
                
                MarkDirty(true);
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
            base.ToTreeAttributes(tree);
            canvasinventory.ToTreeAttributes(tree);
            if(pixeldata != null)
            {
                tree.SetBytes("vc_pixeldata", SerializerUtil.Serialize(pixeldata));
            }
            if(canvasId != null)
            {
                tree.SetInt("canvasid", (int)canvasId);
            }
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (tree.HasAttribute("canvasid"))
            {
                canvasId = tree.GetAsInt("canvasid");
            }
            cachedTree = tree.Clone();
            if (tree.HasAttribute("vc_pixeldata")){
                pixeldata = SerializerUtil.Deserialize<int[]>(tree.GetBytes("vc_pixeldata")); 
            }
            
        }

        public void UpdatePixelData(int[] pixeldata)
        {
            this.pixeldata = pixeldata;
        }               
    }
}
