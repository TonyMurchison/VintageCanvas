using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static VintageCanvas.src.Entities.BlockEntityEasel;

namespace VintageCanvas.src.Blocks
{
    internal class BlockCanvas : Block
    {
        private Dictionary<int, MultiTextureMeshRef> MeshRefDict = new();
        public int canvasSize = 32;
        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            ItemStack blockdrops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)[0];
            BlockEntityCanvas ce = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCanvas;
            blockdrops.Attributes.SetInt("canvasid", (int)ce.canvasId);
            if (ce.pixeldata != null)
            {
                var cdata = TextureUtil.Compress(SerializerUtil.Serialize(ce.pixeldata));
                blockdrops.Attributes.SetBytes("vc_pixeldata", cdata);
            }
            return [blockdrops];
        }
        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
        {
            //Only reliable way I could find of guaranteeing an ID check trigger whenever you obtain a canvas.
            if (extractedStack != null)
            {
                if (extractedStack.Attributes.GetString("canvasid") == null && api.Side == EnumAppSide.Server)
                {
                    int Id = IdRegistry.getCanvasId();
                    extractedStack.Attributes.SetInt("canvasid", Id);
                    slot.MarkDirty();
                }
            }
            base.OnModifiedInInventorySlot(world, slot, extractedStack);                  
        }
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack canvasStack = base.OnPickBlock(world, pos);
            BlockEntityCanvas ce = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCanvas;
            if (ce != null && ce.pixeldata != null)
            {               
                canvasStack.Attributes.SetBytes("vc_pixeldata",
                    TextureUtil.Compress(SerializerUtil.Serialize(ce.pixeldata)));
            }
            if (ce != null)
            {
                canvasStack.Attributes.SetInt("canvasid", (int)ce.canvasId);
            }
            return canvasStack;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel.Block.Code.PathStartsWith("easel"))
            {
                return;
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack held = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            BlockEntityCanvas ce = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCanvas;
            if (ce != null)
            {
                ce.AddFrame(held);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int canvasSize = 32;
            if(this is BlockLargeCanvas)
            {
                canvasSize = 64;
            }
            int? canvasId = itemstack.Attributes.TryGetInt("canvasid");
            if(canvasId == null) { return; }
            if (!MeshRefDict.ContainsKey((int)canvasId) && itemstack.Attributes.HasAttribute("vc_pixeldata")) {                
                //Create renderinfo

                int[] pixeldata = 
                    SerializerUtil.Deserialize<int[]>(TextureUtil.Decompress(
                    itemstack.Attributes.GetBytes("vc_pixeldata")));
                AssetLocation texLoc = new AssetLocation("vintagecanvas", canvasId.ToString());

                Block block = api.World.GetBlock(BlockId);

                MeshData m = TextureUtil.SwapPaintingTexture(
                    pixeldata,
                    canvasSize,
                    texLoc,
                    capi.Tesselator.GetTextureSource(block),
                    block.Code,
                    block.Shape,
                    capi
                    );

                renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(m);
                MeshRefDict[(int)canvasId] = renderinfo.ModelRef;
                m.Dispose();
                
            }
            else if (MeshRefDict.ContainsKey((int)canvasId))
            {
                renderinfo.ModelRef = MeshRefDict[(int)canvasId];
                return;
            }            
        }        
    }
}
