using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace VintageCanvas.src.Utility
{
    internal class MessageForms
    {
        [ProtoContract]
        public class PaintSavePacket
        {
            [ProtoMember(1)] public int PosX { get; set; }
            [ProtoMember(2)] public int PosY { get; set; }
            [ProtoMember(3)] public int PosZ { get; set; }
            [ProtoMember(4)] public byte[] PixelData { get; set; }
            [ProtoMember(5)] public int face { get; set; }
        }
    }
}
