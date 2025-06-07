namespace DISTestKit.Model
{
    public class AggregatedMetricsOverview
    {
        public string? TimeWindowDescription { get; set; }
        public DateTime DataFromUtc { get; set; }
        public DateTime DataUntilUtc { get; set; }
        public long TotalPackets { get; set; }
        public double AveragePacketsPerSecond { get; set; }
        public PeakLoadInfo? PeakLoad { get; set; }
        public class PeakLoadInfo {
            public double PeakPacketsPerSecond       { get; set; }
            public DateTime PeakIntervalStartUtc     { get; set; }
            public DateTime PeakIntervalEndUtc       { get; set; }
            public long PacketsInPeakInterval        { get; set; }
        }
    }
}