using HarmonyLib;
using ProtoBuf;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Items;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageCanvas
{
    public class VintageCanvasModSystem : ModSystem
    {
        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;
        public static VintageCanvasConfig config;

        public static CanvasNetworkHandler NetworkHandler {  get; private set; }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockPaintJar", typeof(BlockPaintJar));
            api.RegisterBlockClass("BlockCanvas", typeof(BlockCanvas));
            api.RegisterBlockClass("BlockLargeCanvas", typeof(BlockLargeCanvas));
            api.RegisterBlockClass("BlockEasel", typeof(BlockEasel));
            api.RegisterBlockClass("BlockEaselH", typeof(BlockEaselH));
            api.RegisterBlockClass("BlockPalette", typeof(BlockPalette));
            api.RegisterBlockClass("BlockFrescoPlaster", typeof(BlockFrescoPlaster));
            api.RegisterItemClass("ItemBrush", typeof(ItemBrush));
            api.RegisterItemClass("ItemPigment", typeof(ItemPigment));
            api.RegisterItemClass("ItemPastel", typeof(ItemPastel));
            api.RegisterBlockEntityClass("EaselEntity", typeof(BlockEntityEasel));
            api.RegisterBlockEntityClass("BlockEntityEaselH", typeof(BlockEntityEaselH));
            api.RegisterBlockEntityClass("BlockEntityCanvas", typeof(BlockEntityCanvas));
            api.RegisterBlockEntityClass("BlockEntityLargeCanvas", typeof(BlockEntityLargeCanvas));
            api.RegisterBlockEntityClass("BlockEntityPalette", typeof(BlockEntityPalette));
            api.RegisterCollectibleBehaviorClass("CollectibleBehaviorPaintTool", typeof(CollectibleBehaviorPaintTool));

            new Harmony("vintagecanvas").PatchAll();

            NetworkHandler = new CanvasNetworkHandler(api);
            NetworkHandler.RegisterChannel();

            TryToLoadConfig(api);
        }

        private void TryToLoadConfig(ICoreAPI api)
        {
            try
            {
                config = api.LoadModConfig<VintageCanvasConfig>("VintageCanvasConfig.json");
                if (config == null)
                {
                    config = new VintageCanvasConfig();
                }

                api.StoreModConfig<VintageCanvasConfig>(config, "VintageCanvasConfig.json");
            }

            catch
            {
                Mod.Logger.Error("Could not load config. Using default settings instead.");
                config = new VintageCanvasConfig();
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.GameWorldSave += OnWorldSave;
            api.Event.SaveGameLoaded += OnWorldLoad;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
        }

        private void OnWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("canvasidcounter", SerializerUtil.Serialize(IdRegistry.currentId));
        }

        private void OnWorldLoad()
        {
            byte[] iddata = sapi.WorldManager.SaveGame.GetData("canvasidcounter");
            if (iddata != null)
            {
                IdRegistry.currentId = SerializerUtil.Deserialize<int>(iddata);
            }
        }
    }

    [ProtoContract]
    public class PaintSavePacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
        [ProtoMember(4)] public byte[] PixelData { get; set; }
        [ProtoMember(5)] public int Face { get; set; }
    }

    [ProtoContract]
    public class PaletteSavePacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
        [ProtoMember(4)] public BlockEntityPalette.Slot[] slots { get; set; }
    }

    [ProtoContract]
    public class FrameSavePacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
        [ProtoMember(4)] public string FrameType { get; set; }
    }

    [ProtoContract]
    public class FrescoPushPacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
        [ProtoMember(4)] public int face { get; set; }
        [ProtoMember(5)] public int[] pixels { get; set; }
    }

    [ProtoContract]
    public class FrescoRequestPacket
    {
        [ProtoMember(1)] public int PosX { get; set; }
        [ProtoMember(2)] public int PosY { get; set; }
        [ProtoMember(3)] public int PosZ { get; set; }
        [ProtoMember(4)] public int Face { get; set; }
    }
}
