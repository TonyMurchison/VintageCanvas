using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Common;

namespace VintageCanvas.src.Entities
{
    internal class BlockEntityEaselH : BlockEntityEasel
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            easelName = "vintagecanvas:heasel";
            textureName = "canvas-large.png";
            allowedCanvas = "largecanvas";
            canvasSize = 64;
        }
    }
}
