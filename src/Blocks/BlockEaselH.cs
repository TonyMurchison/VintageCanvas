using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Blocks
{

    //Responsibilities:
    //Track player interactions, perform raycasting, calculate paint intersections
    public class BlockEaselH : BlockEasel
    {        
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            easelName = "vintagecanvas:heasel";
            allowedCanvas = "largecanvas";
            canvasSize = 64;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            easelName = "vintagecanvas:heasel";
            allowedCanvas = "largecanvas";
            canvasSize = 64;
        }
    }
}

