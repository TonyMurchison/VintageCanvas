using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Utility
{
    public class FrescoStore : ModSystem
    {
        public static Dictionary<string, int[]> Data = new();

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.GameWorldSave += () => api.WorldManager.SaveGame.StoreData("frescoData", SerializerUtil.Serialize(Data));
            byte[] data = api.WorldManager.SaveGame.GetData("frescoData");
            if (data != null)
            {
                api.Event.SaveGameLoaded += () => Data = SerializerUtil.Deserialize<Dictionary<string, int[]>>(data);
            }
        }
        
        public static string compileFrescoID(BlockPos pos, int face)
        {
            return "vintagecanvasfresco" + pos.ToString() + "-" + face;
        }
    }
}