using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageCanvas.src.Blocks
{
    internal class BlockLargeCanvas : BlockCanvas
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {        
            canvasSize = 64;            
        
            base.OnBlockPlaced(world, blockPos, byItemStack);
        }
    }
}
