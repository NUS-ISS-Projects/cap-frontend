
using System;

namespace DISTestKit.Model
{
    public class DisPacket
    {
        public int No { get; set; }
        public DateTime Time { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public int Length { get; set; }
        public string Info { get; set; } = string.Empty;
    }
}