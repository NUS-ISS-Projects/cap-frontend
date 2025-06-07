namespace DISTestKit.Model
{
    public class CustomRangeAggregation: MonthlyAggregation
    {
        public string? StartDate { get; set; }
        public string? EndDate   { get; set; }
    }
}