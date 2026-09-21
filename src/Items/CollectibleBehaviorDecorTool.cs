using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Items
{

    public class CollectibleBehaviorDecorTool : CollectibleBehavior
    {
        public CollectibleBehaviorDecorTool(CollectibleObject collObj) : base(collObj) { }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (slot.Itemstack.Collectible.Variant["paint"] != "none")
            {
                Block target = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (target == null)
                {
                    byEntity.World.Api.Logger.Debug("blockSel empty: sid");
                    return;
                }

                //Translucent decor on soil, glass and grass for some reason makes an Xray tool, so don't do that
                if (!(target is BlockSoil) && !target.Code.PathStartsWith("glass"))
                {
                    bool painted = false;

                    string paint = "vintagecanvas:wallpaint-" + slot.Itemstack.Collectible.Variant["paint"];
                    Block paintblock = byEntity.World.GetBlock(paint);
                    ItemStack stack = new ItemStack(paintblock, 1);

                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

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
        }
    }
}
