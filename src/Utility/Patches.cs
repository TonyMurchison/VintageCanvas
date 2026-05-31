using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Utility
{
    [HarmonyPatch(typeof(Block), "GetSelectionColor")]
    public static class MultiblockSelectionOutlinePatch
    {
        static bool Prefix(Block __instance, ref Vec4f __result, ICoreClientAPI capi, BlockPos pos)
        {
            if (__instance is BlockMultiblock mb)
            {
                
                Block coreBlock = capi.World.BlockAccessor.GetBlock(mb.GetControlBlockPos(pos));

                if (coreBlock.Code.Domain == "vintagecanvas" && coreBlock.Code.Path.StartsWith("easel"))
                {
                    __result = new Vec4f(0f, 0f, 0f, 0.05f);
                    return false;
                }
            }
            return true;
        }
    }
}