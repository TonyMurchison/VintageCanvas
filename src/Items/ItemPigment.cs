using System;
using System.Text;
using Vintagestory.API.Common;

namespace VintageCanvas.src.Items
{
    internal class ItemPigment : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {            
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine("\nRight-click onto a jar of oil or tempera in order to make your paint.");
        }
    }
}