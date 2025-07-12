
using System;

namespace DISTestKit.Model
{
    public enum PacketType { EntityState, FireEvent }
    public class DisPacket
    {
        public int No { get; set; }
        public DateTime Time { get; set; }
        public PacketType Type { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string Info { get; set; } = string.Empty;
        public Dictionary<string,object> Details { get; set; } = [];
    }
}