namespace DISTestKit.Model
{
    public class MonthlyAggregation
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public long EntityStatePduCount { get; set; }
        public long FireEventPduCount { get; set; }
    }
}
