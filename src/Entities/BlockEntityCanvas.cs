using System;
using System.Collections.Generic;
using System.Text;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Entities
{
    internal class BlockEntityCanvas : BlockEntity
    {
        ICoreClientAPI capi;
        IBlockTextureAtlasAPI atlas;
        public int? canvasId;
        public int[] pixeldata;
        private MeshData clientMesh;
        public int canvasSize = 32;
        public string textureName = "canvas.png";

        Dictionary<string, string> FrameSequence = new Dictionary<string, string>{
            { "none", "simple" },
            { "simple", "fancy" },
            { "fancy", "none" }
        };

        public override void Initialize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                atlas = capi.BlockTextureAtlas;                
            }
            base.Initialize(api);

            if (pixeldata != null)
            {
                RegisterDelayedCallback(dt =>
                {
                    UpdateTexture();
                }, 100);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {         
            if (byItemStack == null)            {
                base.OnBlockPlaced(byItemStack);
                
                RegisterDelayedCallback(dt =>
                {
                    UpdateTexture();
                }, 10);
                return;
            }

            canvasId = byItemStack.Attributes.TryGetInt("canvasid");
            if (Api.Side == EnumAppSide.Client)
            {
                //If canvas has pixeldata: load and set local
                if (byItemStack.Attributes.HasAttribute("vc_pixeldata"))
                {
                    byte[] serialisedpixeldata = byItemStack.Attributes.GetBytes("vc_pixeldata");
                    pixeldata = TextureUtil.ReadCompressedPixelData(serialisedpixeldata);
                }
                //Else, initialise pixeldata as canvas default
                else
                {
                    BitmapRef bmp = capi.Assets.Get(new AssetLocation("vintagecanvas:textures/block/" + textureName)).ToBitmap(capi);
                    pixeldata = bmp.Pixels;
                }
                RegisterDelayedCallback(dt =>
                {
                    SynchroniseTexture();
                }, 100);
            }

            MarkDirty(true);
            if (byItemStack.Attributes.HasAttribute("vc_pixeldata"))
            {
                RegisterDelayedCallback(dt =>
                {
                    UpdateTexture();
                }, 10);
            }

            base.OnBlockPlaced(byItemStack);
        }

        public void AddFrame(ItemStack held, IPlayer byPlayer)
        {
            if (held == null)
            {
                return;
            }

            string frame = Block.Variant["frameshape"];
            UpdateFrame(held, FrameSequence[frame]);
            VintageCanvasModSystem.NetworkHandler.SendFrameData(Pos, FrameSequence[frame]);

            if (frame != "fancy")
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                if (held.StackSize < 1)
                {
                    held = null;
                }

                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }    
            
            UpdateTexture();            
            MarkDirty(true);
        }


        private void SynchroniseTexture()
        {
            if (capi != null)
            {
                BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(Pos);
                VintageCanvasModSystem.NetworkHandler.SendPixelData(Pos, TextureUtil.WriteCompressedPixelData(pixeldata));
                MarkDirty(true);
            }
        }
        private void UpdateTexture()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                //send pixeldata to server              

                ClientMain main = capi.World as ClientMain;

                ClientPlatformAbstract platform = main.Platform;
                if (pixeldata == null)
                {
                    return;
                }
                BitmapRef bmpref = platform.CreateBitmapFromPixels(pixeldata, canvasSize, canvasSize);

                //Insert custom texture at vintagecanvas:canvasId
                if (canvasId == null)
                {
                    Api.World.Logger.Error("Wall canvas tried to update without canvasId");
                    return;
                }
                AssetLocation texLoc = new AssetLocation("vintagecanvas", canvasId.ToString());

                capi.BlockTextureAtlas.GetOrInsertTexture(
                        texLoc,
                        out int _,
                        out TextureAtlasPosition texPos,
                        () => platform.CreateBitmapFromPixels(pixeldata, canvasSize, canvasSize),
                        0.005f
                    );
                //Api.World.Logger.Event("Texture inserted at texPos: " + texPos);

                //Reroute "painting" slot of the renderer
                ITexPositionSource defaultSrc = capi.Tesselator.GetTextureSource(Block);
                var paintingSrc = new TextureUtil.PaintingTexSource(defaultSrc, texPos, capi.BlockTextureAtlas.Size);


                String shapestring = "vintagecanvas-canvas-" + Block.Variant["ratio"];
                capi.Tesselator.TesselateShape(
                    shapestring,
                    Block.Code,
                    Block.Shape,
                    out MeshData mesh,
                    paintingSrc
                );

                clientMesh = mesh;                
            }
        }

        //only triggers serverside, after receiving pixeldata packet
        public void UpdatePixelData(int[] pixeldata)
        {
            this.pixeldata = (pixeldata);
            MarkDirty(true);            
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
                if (canvasId != null)
                {
                    tree["canvasid"] = new IntAttribute((int)canvasId);
                }
                if (pixeldata != null)
                {
                    tree.SetBytes("vc_pixeldata", TextureUtil.WriteCompressedPixelData(pixeldata));
                }
            }
            catch
            {
                Api.World.Logger.Debug("Canvas failed to write to tree");
            }
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {            
            base.FromTreeAttributes(tree, worldForResolving);
            canvasId = tree.GetInt("canvasid");
            if (tree.HasAttribute("vc_pixeldata"))
            {
                pixeldata = TextureUtil.ReadCompressedPixelData(tree.GetBytes("vc_pixeldata"));                    
                if(capi != null)
                {
                    UpdateTexture();
                }
            }                   

        }

        public void UpdateFrame(ItemStack held, string frametype)
        {
            if (held.Collectible.Code.BeginsWith("game", "plank"))
            {
                string wood = held.Collectible.Variant["wood"];
                string ratio = Block.Variant["ratio"];
                string side = Block.Variant["side"];

                Dictionary<string, string> canvasVariant = new Dictionary<string, string>
                {
                    { "ratio", ratio },
                    { "frameshape", frametype },
                    { "framewood", wood },
                    { "side", side }
                };

                Block newBlock = Api.World.GetBlock(Block.CodeWithVariants(canvasVariant));
                Api.World.BlockAccessor.ExchangeBlock(newBlock.BlockId, Pos);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            clientMesh?.Dispose();
        }
    }
}
