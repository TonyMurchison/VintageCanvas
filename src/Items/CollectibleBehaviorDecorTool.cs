using System;
using System.Collections.Generic;
using System.Text;
using VintageCanvas.src.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Items
{

    public class CollectibleBehaviorDecorTool : CollectibleBehavior
    {
        public CollectibleBehaviorDecorTool(CollectibleObject collObj) : base(collObj) { }

        private void TryRefill(IPlayer byPlayer)
        {
            var offhandstack = byPlayer.InventoryManager.OffhandHotbarSlot.Itemstack;

            //Refill from offhand if available
            if (offhandstack != null && offhandstack.Collectible is BlockPaintJar)
            {
                BlockPaintJar jar = offhandstack.Collectible as BlockPaintJar;
                ItemStack jarcontents = jar.GetContent(offhandstack);
                if (jarcontents != null && jarcontents.Collectible.Code.PathStartsWith("paint"))
                {
                    string painttype = jarcontents.Collectible.Variant["color"];
                    Item newroller = byPlayer.Entity.World.GetItem("vintagecanvas:roller-" + painttype);
                    ItemStack newrollerstack = new ItemStack(newroller, 1);
                    byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = newrollerstack;
                    jar.TryTakeLiquid(offhandstack, 0.1f);
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (blockSel != null && slot.Itemstack.Collectible.Variant["paint"] != "none")
            {
                Block? target = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (target == null) return;                

                //Translucent decor on soil, glass and grass for some reason makes an Xray tool, so don't do that
                if (!(target is BlockSoil) && !target.Code.PathStartsWith("glass"))
                {
                    bool painted = false;

                    string paint = "vintagecanvas:wallpaint-" + slot.Itemstack.Collectible.Variant["paint"];
                    Block paintblock = byEntity.World.GetBlock(paint);
                    ItemStack stack = new ItemStack(paintblock, 1);

                    

                    if (target is BlockMicroBlock)
                    {
                        BlockEntityMicroBlock bemb = byEntity.Api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMicroBlock;
                        bemb.SetDecor(paintblock, blockSel.Face);
                        bemb.MarkDirty(true);
                        bemb.MarkMeshDirty();
                        painted = true;
                    }
                    else
                    {
                        painted = byEntity.World.BlockAccessor.SetDecor(paintblock, blockSel.Position, blockSel.Face);
                    }

                    if (painted)
                    {
                        //Consume paint charge
                        Item blankroller = byEntity.World.GetItem("vintagecanvas:roller-none");
                        ItemStack rollerstack = new ItemStack(blankroller, 1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = rollerstack;

                        //Try to refill
                        TryRefill(byPlayer);
                        
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

                    }

                    handHandling = EnumHandHandling.Handled;
                    handling = EnumHandling.Handled;
                }
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            }

            if (slot.Itemstack.Collectible.Variant["paint"] == "none")
            {
                TryRefill(byPlayer);
            }
        }
    }
}
