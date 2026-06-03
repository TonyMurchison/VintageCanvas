
using ProtoBuf;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
                    .SetMessageHandler<FrameSavePacket>(OnServerReceiveFrameSave); ;

            }
                else
                {
                ((ICoreClientAPI)api).Network
                    .RegisterChannel(ChannelId)
                    .RegisterMessageType<PaintSavePacket>()
                    .RegisterMessageType<PaletteSavePacket>() 
                    .RegisterMessageType<FrameSavePacket>();
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
                
                if (ee != null)
                {
                    ee.UpdatePixelData(SerializerUtil.Deserialize<int[]>(packet.PixelData));
                }
                else if (ce != null)
                {
                    ce.UpdatePixelData(SerializerUtil.Deserialize<int[]>(packet.PixelData));
                }
                else if (pe != null)
                {
                    pe.UpdatePixelData(SerializerUtil.Deserialize<int[]>(packet.PixelData));
                }
                else
                    {
                        api.World.Logger.Warning("No pixel-receiving entity found.");
                    }
                }

        public void SendPixelData(BlockPos pos, byte[] pixelData)
        {
            if (api.Side != EnumAppSide.Client) return;
            ((ICoreClientAPI)api).Network.GetChannel(ChannelId).SendPacket(new PaintSavePacket
            {
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z,
                PixelData = pixelData
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


