using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Blocks
{
    internal class BlockFrescoPlaster : Block
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel.Block is BlockMicroBlock)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
            else
            {
                string hint = Lang.Get("vintagecanvas:fresco-target");
                (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "frescotarget", hint);
                handling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine("\nRight-click onto a chiselled block to make a paintable surface.");
        }
    }
}
