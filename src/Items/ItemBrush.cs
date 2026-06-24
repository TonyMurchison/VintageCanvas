using System;
using System.Collections.Generic;
using System.Text;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static VintageCanvas.src.Entities.BlockEntityEasel;

namespace VintageCanvas.src.Items
{
    internal class ItemBrush : Item
    {
        //Keeping track of the brushes that have been renderred, and what colour they've been rendered in
        private Dictionary<int, MultiTextureMeshRef> MeshRefDict = new();
        private Dictionary<int, int> ColorDict = new();

        SkillItem[] toolModes;
        ICoreClientAPI capi;
        int brushHash = 0;
        int neutralColor = -8819893;

        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
        {
            //Only reliable way I could find of guaranteeing an ID check trigger whenever you obtain a canvas.
            if (extractedStack != null)
            {
                if (extractedStack.Attributes.TryGetInt("brushid") == null && api.Side == EnumAppSide.Server)
                {
                    int Id = IdRegistry.getCanvasId();
                    extractedStack.Attributes.SetInt("brushid", Id);
                    slot.MarkDirty();
                }
            }
            base.OnModifiedInInventorySlot(world, slot, extractedStack);
        }
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int? brushId = itemstack.Attributes.TryGetInt("brushid");
            if (brushId == null) { return; }
            bool updateMesh = false;

            //update brush mesh either when it has never been tesselated, or if the color doesn't match to the registered one
            if (!MeshRefDict.ContainsKey((int)brushId) && itemstack.Attributes.HasAttribute("paintcolor")) { updateMesh = true; }
            if (MeshRefDict.ContainsKey((int)brushId) 
                && itemstack.Attributes.TryGetInt("paintcolor") != ColorDict[(int)brushId]
                ) { updateMesh = true; }
            
            if (updateMesh)                 
            {
                //renderinfo.ModelRef.Dispose();

                //Create renderinfo
                int paintcolor = itemstack.Attributes.GetInt("paintcolor");
                if(paintcolor == 0)
                {
                    paintcolor = neutralColor;
                    if (MeshRefDict.ContainsKey((int)brushId) && ColorDict[(int)brushId] == neutralColor)
                    {
                        renderinfo.ModelRef = MeshRefDict[(int)brushId];
                        return;
                    }
                }
                int varietycolor = TextureUtil.BlendColor(-16119286, paintcolor, 0.15f);
                int[] pixeldata = [paintcolor, varietycolor, varietycolor, paintcolor];
                AssetLocation texLoc = new AssetLocation("vintagecanvas", brushId.ToString());

                MeshData mesh = TextureUtil.SwapPaintingTexture(
                    pixeldata,
                    2,
                    texLoc,
                    capi.Tesselator.GetTextureSource(itemstack.Item),
                    itemstack.Item.Code,
                    itemstack.Item.Shape,
                    capi
                    );
                
                var meshref = capi.Render.UploadMultiTextureMesh(mesh);

                MeshRefDict[(int)brushId] = meshref;
                ColorDict[(int)brushId] = paintcolor;
                renderinfo.ModelRef = meshref;
                mesh.Dispose();
                return;
                
            }
            else if(MeshRefDict.ContainsKey((int)brushId))
            {
                renderinfo.ModelRef = MeshRefDict[(int)brushId];
                return;
            }
        }

        /*
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if(blockSel == null)
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            if (blockSel.Block.Code.PathStartsWith("multiblock"))
            {
                handling = EnumHandHandling.PreventDefault;
                BlockEasel be = blockSel.Block as BlockEasel;
                var byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                be.OnBlockInteractStart(api.World, byPlayer, blockSel);
                return;
            }           
        }
        */

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            ItemStack offhandStack = forPlayer.InventoryManager.OffhandHotbarSlot.Itemstack;
            if (offhandStack == null) { return null; }

            //only update toolmodes once until the off-hands palette changes
            int paletteHash = offhandStack.Attributes.GetAsInt("colorhash");
            if(paletteHash == brushHash)
            {
                return toolModes;
            }
            
            if ( !offhandStack.Collectible.Code.PathStartsWith("palette") ){ return null; }
            if ( !offhandStack.Attributes.HasAttribute("slots") ) { return null; }

            byte[] slotdata = offhandStack.Attributes.GetBytes("slots");
            BlockEntityPalette.Slot[] slots = SerializerUtil.Deserialize<BlockEntityPalette.Slot[]>(slotdata);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].paintColor != null)
                {
                    if (capi != null) {
                        toolModes[i].WithIcon(capi, (cr, x, y, w, h, rgba) =>
                        {
                            cr.SetSourceRGBA(TextureUtil.ARGBtoRGBA(slots[i].paintColor));
                            cr.Rectangle(x, y, w, h);
                            cr.Fill();
                        });
                    }
                }
            }

            brushHash = BlockPalette.HashSlotColors(slots);
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return slot.Itemstack.Attributes.GetInt("toolmode", 0);
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            if (!byPlayer.InventoryManager.OffhandHotbarSlot.Itemstack.Collectible.Code.PathStartsWith("palette"))
            {
                return;
            }

            byte[] slotdata = byPlayer.InventoryManager.OffhandHotbarSlot.Itemstack.Attributes.GetBytes("slots");
            BlockEntityPalette.Slot[] slots = SerializerUtil.Deserialize<BlockEntityPalette.Slot[]>(slotdata);
            int paintcolor = slots[toolMode].paintColor;            

            slot.Itemstack.Attributes.SetInt("paintcolor", paintcolor);
            slot.Itemstack.Attributes.SetFloat("opacity", 1f);
            return;
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;

            toolModes = new SkillItem[]
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
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (toolModes != null)
            {
                foreach (SkillItem skillItem in toolModes)
                {
                    skillItem.Texture?.Dispose();
                }
            }
            base.OnUnloaded(api);
        }
    }
}
