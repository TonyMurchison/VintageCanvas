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
        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            ItemStack blockdrops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)[0];
            BLockEntityCanvas ce = world.BlockAccessor.GetBlockEntity(pos) as BLockEntityCanvas;
            blockdrops.Attributes.SetInt("canvasid", (int)ce.canvasId);
            if (ce.pixeldata != null)
            {
                blockdrops.Attributes.SetBytes("vc_pixeldata", SerializerUtil.Serialize(ce.pixeldata));
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
            BLockEntityCanvas ce = world.BlockAccessor.GetBlockEntity(pos) as BLockEntityCanvas;
            if (ce != null && ce.pixeldata != null)
            {
                canvasStack.Attributes.SetBytes("vc_pixeldata", SerializerUtil.Serialize(ce.pixeldata));
            }
            if (ce != null)
            {
                canvasStack.Attributes.SetInt("canvasid", (int)ce.canvasId);
            }
            return canvasStack;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack held = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            BLockEntityCanvas ce = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BLockEntityCanvas;
            if (ce != null)
            {
                ce.AddFrame(held);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int? canvasId = itemstack.Attributes.TryGetInt("canvasid");
            if(canvasId == null) { return; }
            if (!MeshRefDict.ContainsKey((int)canvasId) &&itemstack.Attributes.HasAttribute("vc_pixeldata")) {                
                //Create renderinfo

                int[] pixeldata = SerializerUtil.Deserialize<int[]>(
                    itemstack.Attributes.GetBytes("vc_pixeldata"));
                AssetLocation texLoc = new AssetLocation("vintagecanvas", canvasId.ToString());

                Block block = api.World.GetBlock(BlockId);

                MeshData m = TextureUtil.SwapPaintingTexture(
                    pixeldata,
                    32,
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
