using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Blocks
{
    internal class BlockPalette : Block
    {
        private Dictionary<int, MultiTextureMeshRef> MeshRefDict = new();
        private Dictionary<int, int> ColorHashDict = new();
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var pe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPalette;
           
            var playerStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            if(playerStack == null) 
            { 
                return base.OnBlockInteractStart(world, byPlayer, blockSel); 
            }
            if (playerStack.Collectible.Code.Path.StartsWith("paintjar"))
            {
                pe.PaintJarInteract(playerStack, blockSel, byPlayer);
            }
            if (playerStack.Collectible.Code.Path.StartsWith("brush"))
            {
                pe.BrushInteract(playerStack, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            ItemStack[] paletteStack = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            BlockEntityPalette pe = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPalette;
            if (pe.paletteId != null)
            {
                paletteStack[0].Attributes.SetInt("paletteid", (int)pe.paletteId);
            }
            if (pe.slots != null)
            {
                paletteStack[0].Attributes.SetBytes("slots", 
                    SerializerUtil.Serialize(pe.slots));
                paletteStack[0].Attributes.SetInt("colorhash", HashSlotColors(pe.slots));
            }
            return paletteStack;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int? paletteId = itemstack.Attributes.TryGetInt("paletteid");
            if (paletteId == null) { return; }

            //Only update the mesh either if a) the mesh has never been built before, or b) the checksum of the colours is different from the record.
            bool updateMesh = false;
            if (!MeshRefDict.ContainsKey((int)paletteId) && itemstack.Attributes.HasAttribute("slots")){ updateMesh = true; }
            if (MeshRefDict.ContainsKey((int)paletteId) && ColorHashDict[(int)paletteId] != itemstack.Attributes.TryGetInt("colorhash")){ updateMesh = true; }

            if(updateMesh)
            {
                //Create renderinfo
                int[] pixeldata = new int[256];

                if (!itemstack.Attributes.HasAttribute("slots")) { return; }
                BlockEntityPalette.Slot[] slots = SerializerUtil.Deserialize<BlockEntityPalette.Slot[]>(
                    itemstack.Attributes.GetBytes("slots"));
                Random rnd = new Random(1);
                //set pixeldata to match up
                for (int j = 0; j < pixeldata.Length; j++)
                {
                    bool left = (j % 16 < 8);
                    bool top = (j < pixeldata.Length / 2);

                    int x = (j % 16 < 5, j % 16 > 10) switch
                    {
                        (true, false) => 0,
                        (false, false) => 1,
                        (false, true) => 2
                    };

                    int z = (j / 16 < 5, j / 16 > 10) switch
                    {
                        (true, false) => 0,
                        (false, false) => 1,
                        (false, true) => 2
                    };

                    int slotindex = x + (3 * z);

                    int targetcolor = (int)slots[slotindex].paintColor;
                    int variation = rnd.Next(10);
                    if (targetcolor != 0)
                    {
                        targetcolor = TextureUtil.BlendColor(-16119286, targetcolor, variation / 60f);
                    }
                    pixeldata[j] = targetcolor;
                }

                AssetLocation texLoc = new AssetLocation("vintagecanvas", "palette-" + paletteId.ToString());

                Block block = api.World.GetBlock(BlockId);

                MeshData m = TextureUtil.SwapPaintingTexture(
                    pixeldata,
                    16,
                    texLoc,
                    capi.Tesselator.GetTextureSource(block),
                    block.Code,
                    block.Shape,
                    capi
                    );

                renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(m);
                MeshRefDict[(int)paletteId] = renderinfo.ModelRef;
                ColorHashDict[(int)paletteId] = HashSlotColors(slots);
                m.Dispose();

            }
            else if (MeshRefDict.ContainsKey((int)paletteId))
            {
                renderinfo.ModelRef = MeshRefDict[(int)paletteId];
                return;
            }
        }

        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
        {
            //Only reliable way I could find of guaranteeing an ID check trigger whenever you obtain a canvas.
            if (extractedStack != null)
            {
                if (!extractedStack.Attributes.HasAttribute("paletteid") && api.Side == EnumAppSide.Server)
                {
                    int Id = IdRegistry.getCanvasId();
                    extractedStack.Attributes.SetInt("paletteid", Id);
                    slot.MarkDirty();
                }
            }
            base.OnModifiedInInventorySlot(world, slot, extractedStack);
        }

        static public int HashSlotColors(BlockEntityPalette.Slot[] slots)
        {
            int hash = 0;
            foreach(BlockEntityPalette.Slot slot in slots)
            {
                unchecked
                {
                    hash += slot.paintColor;
                }
            }
            return hash;
        }
    }    
}
