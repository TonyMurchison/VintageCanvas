using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
        private static Dictionary<int, MultiTextureMeshRef> MeshRefDict = new();
        private static Dictionary<int, int> ColorHashDict = new();
        private SkillItem[] ToolModes;
        private int paletteHash = 0;
        public int activeSlot = 0;

        
        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {            
            if (!(slot == forPlayer.InventoryManager.ActiveHotbarSlot))
            {
                return ToolModes;
            }

            if(slot.Itemstack.Attributes.GetInt("colorhash", 0) == paletteHash)
            {
                return ToolModes;
            }

            if (!slot.Itemstack.Attributes.HasAttribute("slots")) { return null; }

            byte[] slotdata = slot.Itemstack.Attributes.GetBytes("slots");
            BlockEntityPalette.Slot[] slots = SerializerUtil.Deserialize<BlockEntityPalette.Slot[]>(slotdata);

            ICoreClientAPI capi = forPlayer.Entity.World.Api as ICoreClientAPI;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].paintColor != null)
                {
                    if (capi != null)
                    {
                        ToolModes[i].WithIcon(capi, (cr, x, y, w, h, rgba) =>
                        {
                            cr.SetSourceRGBA(TextureUtil.ARGBtoRGBA(slots[i].paintColor));
                            cr.Rectangle(x, y, w, h);
                            cr.Fill();
                        });
                    }
                }
            }

            paletteHash = HashSlotColors(slots);
            return ToolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return slot.Itemstack.Attributes.GetInt("toolmode", 0);
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            //activeslot gets read out by BlockPaintJar during interaction
            slot.Itemstack.Attributes.SetInt("activeslot", toolMode);            

            return;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string baseDescription = base.GetPlacedBlockInfo(world, pos, forPlayer);
            StringBuilder sb = new StringBuilder();

            if (VintageCanvasModSystem.config.PaintDepletion)
            {
                BlockEntityPalette bep = GetBlockEntity<BlockEntityPalette>(pos);
                if (bep == null) return sb.ToString().TrimEnd();
                int paletteindex = bep.PaletteIndex(forPlayer.CurrentBlockSelection);
                int paintamount = bep.slots[paletteindex].fullness;

                sb.Append(baseDescription);
                sb.AppendLine("Paint in slot: " + Math.Round(paintamount * 0.01f, 2) + "L");
            }

            return sb.ToString().TrimEnd();
        }
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var pe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPalette;
           
            var playerStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            if(playerStack == null) 
            {
                PickUpPalette(world, byPlayer, blockSel);
                return true;
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
            if(dropQuantityMultiplier == 0) { return null; }
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

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool pb = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            if(byItemStack != null && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }

            return pb;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int? paletteId = itemstack.Attributes.TryGetInt("paletteid");
            if (paletteId == null) return; 

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

                MeshData m = TextureUtil.SwapPaintingTextureMesh(
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
                itemstack.Attributes.SetInt("colorhash", HashSlotColors(slots));
            }
            else if (MeshRefDict.ContainsKey((int)paletteId))
            {
                renderinfo.ModelRef = MeshRefDict[(int)paletteId];
                return;
            }
        }

        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
        {
            if (extractedStack != null && extractedStack.StackSize != 0)
            {
                if (!extractedStack.Attributes.HasAttribute("paletteid") && api.Side == EnumAppSide.Server)
                {
                    int Id = IdRegistry.getCanvasId();
                    extractedStack.Attributes.SetInt("paletteid", Id);
                    slot.MarkDirty();
                }

                if (!extractedStack.Attributes.HasAttribute("slots") && api.Side == EnumAppSide.Server)
                {
                    BlockEntityPalette.Slot[] slotdata = new BlockEntityPalette.Slot[9];

                    for(int i = 0; i < slotdata.Length; i++)
                    {
                        slotdata[i] = new BlockEntityPalette.Slot();
                    }

                    extractedStack.Attributes.SetBytes("slots", SerializerUtil.Serialize(slotdata));
                    slot.MarkDirty();
                }
            }
            if (extractedStack != null && extractedStack.StackSize == 0) slot.MarkDirty();


            base.OnModifiedInInventorySlot(world, slot, extractedStack);
        }

        private void PickUpPalette(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {            
            ItemStack paletteStack = new ItemStack(this);
            //TODO picking up logic
            BlockEntityPalette pe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPalette;
            if (pe.paletteId != null)
            {
                paletteStack.Attributes.SetInt("paletteid", (int)pe.paletteId);
            }
            if (pe.slots != null)
            {
                paletteStack.Attributes.SetBytes("slots",
                    SerializerUtil.Serialize(pe.slots));
                paletteStack.Attributes.SetInt("colorhash", HashSlotColors(pe.slots));
            }
            byPlayer.InventoryManager.TryGiveItemstack(paletteStack);

            world.BlockAccessor.BreakBlock(blockSel.Position, byPlayer, 0);
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

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ToolModes = new SkillItem[]
            {
                new SkillItem() { Code = new AssetLocation("Slot1"), Name = Lang.Get("Slot 1") },
                new SkillItem() { Code = new AssetLocation("Slot2"), Name = Lang.Get("Slot 2") },
                new SkillItem() { Code = new AssetLocation("Slot3"), Name = Lang.Get("Slot 3") },
                new SkillItem() { Code = new AssetLocation("Slot4"), Name = Lang.Get("Slot 4") },
                new SkillItem() { Code = new AssetLocation("Slot5"), Name = Lang.Get("Slot 5") },
                new SkillItem() { Code = new AssetLocation("Slot6"), Name = Lang.Get("Slot 6") },
                new SkillItem() { Code = new AssetLocation("Slot7"), Name = Lang.Get("Slot 7") },
                new SkillItem() { Code = new AssetLocation("Slot8"), Name = Lang.Get("Slot 8") },
                new SkillItem() { Code = new AssetLocation("Slot9"), Name = Lang.Get("Slot 9") }
            };

            if(api.Side == EnumAppSide.Client)
            {
                MeshRefDict.Clear();
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (ToolModes != null)
            {
                foreach (SkillItem skillItem in ToolModes)
                {
                    skillItem.Texture?.Dispose();
                }
            }
            MeshRefDict.Clear();
            base.OnUnloaded(api);
        }
    }    
}
