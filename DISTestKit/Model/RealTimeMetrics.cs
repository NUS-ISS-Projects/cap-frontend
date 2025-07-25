namespace DISTestKit.Model
{
    public class RealTimeMetrics
    {
        public long LastPduReceivedTimestampMs { get; set; }
        public long PdusInLastSixtySeconds { get; set; }
        public double AveragePduRatePerSecondLastSixtySeconds { get; set; }
    }
}
