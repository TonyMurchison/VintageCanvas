using System;
using System.Text;
using Vintagestory.API.Common;

namespace VintageCanvas.src.Items
{
    internal class ItemPastel : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine("\nA simple tool for making monochromatic sketches.");
        }
    }
}