
using Cairo.Freetype;
using ProtoBuf;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Utility
    {
        public class CanvasNetworkHandler
        {
            private const string ChannelId = "vintagecanvas";

            private readonly ICoreAPI api;

            public CanvasNetworkHandler(ICoreAPI api)
            {
                this.api = api;
            }

            public void RegisterChannel()
            {
                if (api.Side == EnumAppSide.Server)
                {
                ((ICoreServerAPI)api).Network
                    .RegisterChannel(ChannelId)
                    .RegisterMessageType<PaintSavePacket>()
                    .SetMessageHandler<PaintSavePacket>(OnServerReceiveSave)
                    .RegisterMessageType<PaletteSavePacket>()
                    .SetMessageHandler<PaletteSavePacket>(OnServerReceivePaletteSave)
                    .RegisterMessageType<FrameSavePacket>()
                    .SetMessageHandler<FrameSavePacket>(OnServerReceiveFrameSave)
                    .RegisterMessageType<FrescoPushPacket>()
                    .RegisterMessageType<FrescoRequestPacket>()
                    .SetMessageHandler<FrescoRequestPacket>(OnServerReceiveFrescoRequest);             
            }
                else
                {
                ((ICoreClientAPI)api).Network
                    .RegisterChannel(ChannelId)
                    .RegisterMessageType<PaintSavePacket>()
                    .RegisterMessageType<PaletteSavePacket>()
                    .RegisterMessageType<FrameSavePacket>()
                    .RegisterMessageType<FrescoPushPacket>()
                    .SetMessageHandler<FrescoPushPacket>(OnClientReceiveFrescoPush)
                    .RegisterMessageType<FrescoRequestPacket>();

                }
            }

        private void OnClientReceiveFrescoPush(FrescoPushPacket packet)
        {
            var capi = (ICoreClientAPI)api;
            BlockPos pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);
            string id = FrescoStore.compileFrescoID(pos, packet.face);

            FrescoStore.Data[id] = packet.pixels;

            var bem = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (bem != null)
            {
                bem.MarkDirty();
                bem.MarkMeshDirty();
            }
        }

        private void OnServerReceiveFrescoRequest(IServerPlayer fromPlayer, FrescoRequestPacket packet)
        {
            var sapi = (ICoreServerAPI)api;
            var pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);

            string id = FrescoStore.compileFrescoID(pos, packet.Face);

            if (FrescoStore.Data.ContainsKey(id))
            {
                sapi.Network.GetChannel(ChannelId).SendPacket(new FrescoPushPacket
                {
                    PosX = pos.X,
                    PosY = pos.Y,
                    PosZ = pos.Z,
                    pixels = FrescoStore.Data[id],
                    face = packet.Face
                }, fromPlayer);
            }
        }

        private void OnServerReceiveFrameSave(IServerPlayer fromPlayer, FrameSavePacket packet)
        {
            var sapi = (ICoreServerAPI)api;
            var pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);

            var ce = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCanvas;
            if (ce != null)
            {
                ce.UpdateFrame(fromPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, packet.FrameType);
            }
        }

        private void OnServerReceivePaletteSave(IServerPlayer fromPlayer, PaletteSavePacket packet)
        {
            var sapi = (ICoreServerAPI)api;
            var pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);

            var pe = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPalette;


            if (pe != null)
            {
                pe.UpdateSlots(packet.slots);
            }

            else
            {
                api.World.Logger.Warning("Palette Entity not found");
            }
        }

        private void OnServerReceiveSave(IServerPlayer fromPlayer, PaintSavePacket packet)
        {
            var sapi = (ICoreServerAPI)api;
            var pos = new BlockPos(packet.PosX, packet.PosY, packet.PosZ);

            
            var ee = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityEasel;
            var ce = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCanvas;
            var pe = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPalette;

            if (sapi.World.BlockAccessor.GetBlock(pos) is BlockMicroBlock)
            {
                var bemb = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                string id = FrescoStore.compileFrescoID(pos, packet.Face);

                FrescoStore.Data[id] = TextureUtil.ReadCompressedPixelData(packet.PixelData);
                bemb.MarkDirty(true, fromPlayer);
                bemb.MarkMeshDirty();

                return;
            }
            else if (ee != null)
            {
                ee.UpdatePixelData(TextureUtil.ReadCompressedPixelData(packet.PixelData));
            }
            else if (ce != null)
            {
                ce.UpdatePixelData(TextureUtil.ReadCompressedPixelData(packet.PixelData));
            }
            else if (pe != null)
            {
                pe.UpdatePixelData(TextureUtil.ReadCompressedPixelData(packet.PixelData));
            }
            else
                {
                    //api.World.Logger.Warning("No pixel-receiving entity found.");
                }
            }

        public void SendFrescoRequest(BlockPos pos, int face)
        {
            if (api.Side != EnumAppSide.Client) return;
            try
            {
                ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new FrescoRequestPacket
                {
                    PosX = pos.X,
                    PosY = pos.Y,
                    PosZ = pos.Z,
                    Face = face
                });
                //api.Logger.Debug("Packet sent");
            }
            catch
            {
                api.Logger.Debug("Packet failed to send");
            }
        }

        public void SendPixelData(BlockPos pos, byte[] pixelData, int face)
        {
            if (api.Side != EnumAppSide.Client) return;
            ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new PaintSavePacket
            {
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z,
                PixelData = pixelData,
                Face = face
            });
        }

        public void SendSlotData(BlockPos pos, BlockEntityPalette.Slot[] Slots)
        {
            if (api.Side != EnumAppSide.Client) return;
            ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new PaletteSavePacket
            {
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z,
                slots = Slots
            });
        }

        public void SendFrameData(BlockPos pos, string frametype)
        {
            if (api.Side != EnumAppSide.Client) return;
            ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new FrameSavePacket
            {
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z,
                FrameType = frametype
            });
        }
    }    
}


