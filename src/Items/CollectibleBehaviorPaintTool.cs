using System;
using System.Collections.Generic;
using System.Text;
using VintageCanvas.src.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Items
{
    internal class CollectibleBehaviorPaintTool : CollectibleBehavior
    {
        public CollectibleBehaviorPaintTool(CollectibleObject collObj) : base(collObj) { }


        //When something is tagged with this behaviour, redirect any attack on an easel to Easel.OnBlockInteractX();
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel != null) {
                BlockEasel be;
                if (blockSel.Block is BlockMultiblock)
                {
                    BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                    BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                    be = (BlockEasel)byEntity.World.BlockAccessor.GetBlock(bp);

                    blockSel = blockSel.Clone();
                    blockSel.Position = bp;
                }
                else if (blockSel.Block is BlockEasel)
                {
                    be = (BlockEasel)blockSel.Block;
                }
                else
                {
                    be = null;
                }

                if (be != null)
                {
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    be.OnBlockInteractStart(byEntity.World, byPlayer, blockSel);
                    handling = EnumHandling.PreventDefault;
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }

                //Copy OnContainedInteract() for paint jars
                if (blockSel.Block is BlockGroundStorage)
                {
                    BlockEntityGroundStorage bgse = (BlockEntityGroundStorage)byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    bgse.OnPlayerInteractStart(byPlayer, blockSel);
                    handling = EnumHandling.PreventDefault;
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel != null)
            {
                BlockEasel be = new();
                if (blockSel.Block is BlockMultiblock)
                {
                    BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                    BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                    be = (BlockEasel)byEntity.World.BlockAccessor.GetBlock(bp);

                    blockSel = blockSel.Clone();
                    blockSel.Position = bp;
                }
                else if (blockSel.Block is BlockEasel)
                {
                    be = (BlockEasel)blockSel.Block;
                }
                else
                {
                    be = null;
                }

                if (be != null)
                {
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    handling = EnumHandling.PreventDefault;
                    be.OnBlockInteractStep(secondsPassed, byEntity.World, byPlayer, blockSel);
                    return true;
                }
            }

            return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
        {
            if (blockSel != null)
            {
                BlockEasel be = new();
                if (blockSel.Block is BlockMultiblock)
                {
                    BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                    BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                    be = (BlockEasel)byEntity.World.BlockAccessor.GetBlock(bp);

                    blockSel = blockSel.Clone();
                    blockSel.Position = bp;
                }
                else if (blockSel.Block is BlockEasel)
                {
                    be = (BlockEasel)blockSel.Block;
                }
                else
                {
                    be = null;
                }

                if (be != null)
                {
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    handling = EnumHandling.PreventDefault;
                    return be.OnBlockInteractCancel(secondsPassed, byEntity.World, byPlayer, blockSel, cancelReason);
                }
            }

            return base.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSel, entitySel, cancelReason, ref handling);
        }

        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity, ref EnumHandling bhHandling)
        {
            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (byPlayer.CurrentBlockSelection != null)
            {
                if (byPlayer.CurrentBlockSelection.Block is BlockEasel || byPlayer.CurrentBlockSelection.Block is BlockMultiblock)
                {
                    bhHandling = EnumHandling.PreventDefault;
                }
            }
            
            return base.GetHeldTpHitAnimation(slot, byEntity, ref bhHandling);
        }
    }
}
