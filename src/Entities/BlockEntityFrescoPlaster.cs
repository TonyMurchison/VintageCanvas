using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageCanvas.src.Entities
{
    internal class BlockEntityFrescoPlaster : BlockEntity
    {
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}
