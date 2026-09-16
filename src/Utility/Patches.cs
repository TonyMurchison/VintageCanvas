using Cairo;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using VintageCanvas.src.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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
            Block transparentFrescoBlock = capi.World.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-transparent"));

            for (int i = 0; i < __result.Count; i++)
            {
                if (decorIds[i] != frescoBlock.BlockId 
                    && decorIds[i] != transparentFrescoBlock.BlockId)
                {
                    continue;
                }

                //TEMP
                string frescoId = "vintagecanvasfresco" + pos.ToString() + "-" + i;

                if (!FrescoStore.Data.ContainsKey(frescoId))
                {
                    int[] pixeldata = new int[1024];
                    if (decorIds[i] == frescoBlock.BlockId)
                    {
                        pixeldata = capi.Assets.Get(new AssetLocation("vintagecanvas:textures/block/canvas.png")).ToBitmap(capi).Pixels;
                    }
                    if (decorIds[i] == transparentFrescoBlock.BlockId)
                    {
                        pixeldata = capi.Assets.Get(new AssetLocation("vintagecanvas:textures/empty.png")).ToBitmap(capi).Pixels;
                    }
                    
                    FrescoStore.Data.Add(frescoId, pixeldata);
                }
                
                AssetLocation texLoc = new AssetLocation(frescoId);                

                Block block = capi.World.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-white"));
                VoxelMaterial vm = VoxelMaterial.FromBlock(capi, block, pos, false);

                if (FrescoStore.Data.ContainsKey(frescoId))
                {
                    TextureAtlasPosition t = TextureUtil.SwapPaintingTexture(FrescoStore.Data[frescoId], 32, texLoc, capi);
                    for (int f = 0; f < vm.Texture.Length; f++)
                    {
                        vm.Texture[f] = t;
                        vm.TextureInside[f] = t;
                    }
                }

                __result[i] = vm;                
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityChisel), "Interact")]
    public static class ScrapeFrescoPatch
    {
        static void Postfix(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer != null && byPlayer.InventoryManager.ActiveTool == EnumTool.Knife)
            {
                if (!byPlayer.Entity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    return;
                }

                var face = blockSel.Face.Index;
                BlockEntityMicroBlock bec = byPlayer.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMicroBlock;
                if (bec == null) return;
                if (bec.DecorIds != null)
                {
                    string frescoId = "vintagecanvasfresco" + blockSel.Position.ToString() + "-" + face;
                    if (FrescoStore.Data.ContainsKey(frescoId))
                    {
                        FrescoStore.Data.Remove(frescoId);
                    }
                }
            }
            return;
        }
    }
    
    //Constantly offsets any off-hand hunger value of artists back to default
    [HarmonyPatch(typeof(EntityBehaviorHunger), "OnGameTick")]
    public static class OffsetArtistHunger
    {
        static void Prefix(EntityBehaviorHunger __instance, float deltaTime)
        {
            Entity entity = __instance.entity;
            EntityPlayer player = entity as EntityPlayer;
                
            if (player == null || entity.WatchedAttributes.GetInt("ambidextrous", 0) == 0 && false)
            {
                return;
            }
            EntityFloatStats stats = entity.Stats["hungerrate"];

            var c = player.WatchedAttributes["characterClass"];

            if(stats != null && c.ToString() == "canvaspainter")
            {
                if (stats.ValuesByKey.ContainsKey("offhanditem"))
                {                    
                    player.Stats.Set("hungerrate", "vintagecanvas:ambidextrous", -0.2f, false);
                }
                else
                {
                    ((Entity)player).Stats.Remove("hungerrate", "vintagecanvas:ambidextrous");
                }
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityMicroBlock), "GetBlockInfo")]
    public static class ChiselInfoPatch
    {
        static void Postfix(IPlayer forPlayer, StringBuilder dsc)
        {
            if (forPlayer?.CurrentBlockSelection?.Face != null)
            {
                BlockPos pos = forPlayer.CurrentBlockSelection.Position;
                BlockEntityMicroBlock? bemb = forPlayer.Entity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                if (bemb == null) return;
                int[]? decors = bemb.DecorIds;
                if (decors == null) return;
                int? decor = decors[forPlayer.CurrentBlockSelection.Face.Index];
                if (decor == forPlayer.Entity.World.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-white")).BlockId
                    || decor == forPlayer.Entity.World.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-transparent")).BlockId)
                {
                    var str = dsc.ToString();
                    System.String ps = Lang.Get("Paintable surface");
                    if (!str.EndsWith(ps + "\r\n")){
                        dsc.AppendLine(ps);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BlockMicroBlock), "OnBlockBroken")]
    public static class FrescoRemovedPatch
    {

        static void Postfix(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
        for(int i = 0; i < 6; i++)
            {
                string ID = "vintagecanvasfresco" + pos.ToString() + "-" + i;
                if (FrescoStore.Data.ContainsKey(ID))
                {
                    FrescoStore.Data.Remove(ID);                    
                }
            }   
        }
    }

    
    [HarmonyPatch(typeof(BlockEntityMicroBlock), "FromTreeAttributes")]
    public static class FrescoFromTreePatch
    {
        static void Postfix(BlockEntityMicroBlock __instance, ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (worldAccessForResolve.Api.Side == EnumAppSide.Client) {
                int[] dids = __instance.DecorIds;
                int frescoid = worldAccessForResolve.GetBlock(new AssetLocation("vintagecanvas:frescoplaster-white")).Id;
                var facelist = BlockFacing.ALLFACES;

                foreach(BlockFacing face in facelist)
                {
                    int did = __instance.GetDecor(face);
                    if (did != null && did == frescoid)
                    {
                        BlockPos Pos = __instance.Pos;
                        string id = FrescoStore.compileFrescoID(Pos, face.Index);

                        ((ICoreClientAPI)worldAccessForResolve.Api).Event.EnqueueMainThreadTask(() =>
                        {
                            VintageCanvasModSystem.NetworkHandler.SendFrescoRequest(Pos, face.Index);
                        }, "frescoRequestSend");
                    }                        
                }
            }
        }
    }    
}
