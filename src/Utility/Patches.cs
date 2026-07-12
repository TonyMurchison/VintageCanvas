using Cairo;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vintagestory.GameContent.BlockEntityMicroBlock;

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

                if (coreBlock.Code.Domain == "vintagecanvas")
                {
                    if (coreBlock.Code.Path.StartsWith("easel") || coreBlock.Code.Path.StartsWith("heasel"))
                    {
                        __result = new Vec4f(0f, 0f, 0f, 0.05f);
                        return false;
                    }
                }
            }
            return true;
        }
    }
    
    [HarmonyPatch(typeof(BlockEntityMicroBlock), "loadDecor")]
    public static class LoadDecorPatch
    {
        
        static void Postfix(ICoreClientAPI capi, List<uint> voxelCuboids, int[] decorIds,
            BlockPos pos, MeshData mesh, int decorRotations, RefList<VoxelMaterial> __result)
        {            
            if (__result == null || decorIds == null) return;

            Block frescoBlock = capi.World.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-white"));
            //TODO check if data in FrescoStore 
            for (int i = 0; i < __result.Count; i++)
            {
                if (decorIds[i] != frescoBlock.BlockId)
                {
                    continue;
                }

                //TEMP
                string frescoId = "vintagecanvasfresco" + pos.ToString() + "-" + i;

                if (!FrescoStore.Data.ContainsKey(frescoId))
                {
                    int[] pixeldata = capi.Assets.Get(new AssetLocation("vintagecanvas:textures/block/fresco.png")).ToBitmap(capi).Pixels;
                    FrescoStore.Data.Add(frescoId, pixeldata);
                }
                
                AssetLocation texLoc = new AssetLocation(frescoId);

                TextureAtlasPosition t = TextureUtil.SwapPaintingTexture(FrescoStore.Data[frescoId], 32, texLoc, capi);

                Block block = capi.World.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-white"));
                VoxelMaterial vm = VoxelMaterial.FromBlock(capi, block, pos, false);
                
                for (int f = 0; f < vm.Texture.Length; f++)
                {
                    vm.Texture[f] = t;
                    vm.TextureInside[f] = t;               
                }

                 __result[i] = vm;
            }
        }

        /*
        private static float getOutermostVoxelDistanceToCenter(List<uint> voxelCuboids, int faceindex)
        {
            int num = 0;
            int num2 = 0;
            switch (faceindex)
            {
                case 0:
                    num2 = (num = 16);
                    foreach (uint voxelCuboid in voxelCuboids)
                    {
                        num = Math.Min(num, (int)((voxelCuboid >> 8) & 0xF));
                    }

                    break;
                case 1:
                    num2 = (num = 0);
                    foreach (uint voxelCuboid2 in voxelCuboids)
                    {
                        num = Math.Max(num, (int)(((voxelCuboid2 >> 12) & 0xF) + 1));
                    }

                    break;
                case 2:
                    num2 = (num = 0);
                    foreach (uint voxelCuboid3 in voxelCuboids)
                    {
                        num = Math.Max(num, (int)(((voxelCuboid3 >> 20) & 0xF) + 1));
                    }

                    break;
                case 3:
                    num2 = (num = 16);
                    foreach (uint voxelCuboid4 in voxelCuboids)
                    {
                        num = Math.Min(num, (int)(voxelCuboid4 & 0xF));
                    }

                    break;
                case 4:
                    num2 = (num = 0);
                    foreach (uint voxelCuboid5 in voxelCuboids)
                    {
                        num = Math.Max(num, (int)(((voxelCuboid5 >> 16) & 0xF) + 1));
                    }

                    break;
                case 5:
                    num2 = (num = 16);
                    foreach (uint voxelCuboid6 in voxelCuboids)
                    {
                        num = Math.Min(num, (int)((voxelCuboid6 >> 4) & 0xF));
                    }

                    break;
            }

            return (float)Math.Abs(num2 - num) / 16f;
        }
        */
    }
}
