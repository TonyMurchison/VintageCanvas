using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;

namespace VintageCanvas.src.Entities
{
    internal class BlockEntityLargeCanvas : BlockEntityCanvas
    {
        public override void Initialize(ICoreAPI api)
        {
            textureName = "canvas-large.png";
            canvasSize = 64;
            base.Initialize(api);
        }
    }
}
