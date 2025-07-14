
using System;

namespace DISTestKit.Model
{
    public class DisPacket
    {
        public int No { get; set; }
        public DateTime Time { get; set; }
        public string PDUType   { get; set; } = string.Empty;
        public string Type      => PDUType;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public int Length { get; set; }
        public Dictionary<string, object> Details { get; set; } 
            = new Dictionary<string, object>();
    }
}