using System;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DISTestKit.ViewModel
{
    public class VolumeChartViewModel
    {
        private readonly ObservableCollection<DateTimePoint> _values = new();

        public void Clear() => _values.Clear();

        public void ConfigureForAggregatedData(DateTime startDate)
        {
            // Configure for 24-hour view with hourly intervals
            XAxes[0].Labeler = value => new DateTime((long)value).ToString("HH:mm");
            XAxes[0].MinLimit = startDate.Ticks;
            XAxes[0].MaxLimit = startDate.AddDays(1).Ticks;
            XAxes[0].UnitWidth = TimeSpan.FromHours(1).Ticks;
        }

        public void ConfigureForRealTime()
        {
            // Configure for 1-minute real-time view
            var now = DateTime.Now;
            XAxes[0].Labeler = value => new DateTime((long)value).ToString("HH:mm:ss");
            XAxes[0].MinLimit = now.AddMinutes(-1).Ticks;
            XAxes[0].MaxLimit = now.Ticks;
            XAxes[0].UnitWidth = TimeSpan.FromMinutes(1).Ticks;

            // Reset Y-axis to default real-time configuration
            YAxes[0].MinLimit = 0;
            YAxes[0].MaxLimit = 3000;
            YAxes[0].UnitWidth = double.NaN; // Let LiveCharts auto-calculate step size
        }

        public ObservableCollection<ISeries> Series { get; }
        public Axis[] XAxes { get; }
        public Axis[] YAxes { get; }

        public VolumeChartViewModel()
        {
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Volume",
                    Values = _values,
                    Stroke = new SolidColorPaint(new SKColor(80, 132, 221), 2),
                    Fill = new SolidColorPaint(new SKColor(80, 132, 221, 80)),
                },
            };

            var now = DateTime.Now;
            XAxes =
            [
                new Axis
                {
                    Labeler = value => new DateTime((long)value).ToString("HH:mm:ss"),
                    MinLimit = now.AddMinutes(-1).Ticks,
                    MaxLimit = now.Ticks,
                    TicksPaint = new SolidColorPaint(new SKColor(80, 132, 221)),
                    UnitWidth = TimeSpan.FromMinutes(1).Ticks,
                },
            ];
            YAxes =
            [
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 3000,
                    MinStep = 1,
                    Labeler = value => ((int)value).ToString(),
                },
            ];
        }

        public void Update(DateTimePoint point)
        {
            _values.Add(point);
            if (_values.Count > 3000)
                _values.RemoveAt(0);
            XAxes[0].MinLimit = point.DateTime.AddMinutes(-1).Ticks;
            XAxes[0].MaxLimit = point.DateTime.Ticks;

            var maxY = _values.Select(v => v.Value).DefaultIfEmpty(0).Max();
            var padding = maxY * 0.2;
            if (padding == 0)
                padding = 10;

            var maxLimit = Math.Ceiling((maxY + padding).GetValueOrDefault());
            YAxes[0].MinLimit = 0;
            YAxes[0].MaxLimit = maxLimit;

            // Set step size to get approximately 5-6 labels
            var step = maxLimit / 5;
            if (step > 0)
            {
                YAxes[0].UnitWidth = step;
            }
        }

        public void AddAggregatedDataPoint(DateTimePoint point)
        {
            _values.Add(point);
        }

        public void FinalizeAggregatedData(string timeUnit)
        {
            if (timeUnit == "day")
            {
                // For daily view, ensure we show exactly 7 days ending with the latest date
                var endDate =
                    _values.Count > 0 ? _values.Max(v => v.DateTime.Date) : DateTime.Today;
                var startDate = endDate.AddDays(-6); // 7 days total including end date

                // Create a dictionary of existing data points for quick lookup
                var dataByDate = _values.ToDictionary(v => v.DateTime.Date, v => v.Value);

                // Clear existing values and rebuild with 7 consecutive days
                _values.Clear();

                for (int i = 0; i < 7; i++)
                {
                    var date = startDate.AddDays(i);
                    var value = dataByDate.GetValueOrDefault(date, 0); // Use 0 if no data for this date
                    _values.Add(new DateTimePoint(date, value));
                }

                // Configure X-axis for exactly 7 days - show all date labels
                XAxes[0].Labeler = value => new DateTime((long)value).ToString("MM-dd");
                XAxes[0].MinLimit = startDate.AddHours(-12).Ticks;
                XAxes[0].MaxLimit = endDate.AddHours(12).Ticks;
                XAxes[0].UnitWidth = TimeSpan.FromDays(1).Ticks;
            }
            else if (timeUnit == "hour")
            {
                // For hourly data (today view)
                if (_values.Count == 0)
                    return;

                var minDate = _values.Min(v => v.DateTime);
                var maxDate = _values.Max(v => v.DateTime);

                XAxes[0].Labeler = value => new DateTime((long)value).ToString("HH:mm");
                XAxes[0].MinLimit = minDate.AddMinutes(-30).Ticks;
                XAxes[0].MaxLimit = maxDate.AddMinutes(30).Ticks;
                XAxes[0].UnitWidth = TimeSpan.FromHours(1).Ticks;
            }
            else if (timeUnit == "week")
            {
                // For weekly data (monthly view)
                if (_values.Count == 0)
                    return;

                // Simple approach - just show Week 1, Week 2, etc. for all ticks
                XAxes[0].Labeler = value =>
                {
                    // Find the closest data point to this tick
                    var tickTime = new DateTime((long)value);
                    var closestPoint = _values
                        .OrderBy(v => Math.Abs((v.DateTime - tickTime).TotalDays))
                        .FirstOrDefault();

                    if (
                        closestPoint != null
                        && Math.Abs((closestPoint.DateTime - tickTime).TotalDays) < 3.5
                    )
                    {
                        var weekIndex = _values.ToList().IndexOf(closestPoint) + 1;
                        return $"Week {weekIndex}";
                    }

                    return string.Empty;
                };

                var minDate = _values.Min(v => v.DateTime);
                var maxDate = _values.Max(v => v.DateTime);
                XAxes[0].MinLimit = minDate.AddDays(-7).Ticks;
                XAxes[0].MaxLimit = maxDate.AddDays(7).Ticks;
                XAxes[0].UnitWidth = TimeSpan.FromDays(3.5).Ticks;
            }

            // Configure Y-axis based on actual data values
            var maxY = _values.Select(v => v.Value).DefaultIfEmpty(0).Max();
            var padding = maxY * 0.2;
            if (padding == 0)
                padding = 10;

            var maxLimit = Math.Ceiling((maxY + padding).GetValueOrDefault());
            YAxes[0].MinLimit = 0;
            YAxes[0].MaxLimit = maxLimit;

            // Set step size to get approximately 5-6 labels
            var step = maxLimit / 5;
            if (step > 0)
            {
                YAxes[0].UnitWidth = step;
            }
        }
    }
}
