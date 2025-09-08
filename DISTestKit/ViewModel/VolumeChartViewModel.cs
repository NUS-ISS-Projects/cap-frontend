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
            // Switch to line series for real-time view
            SwitchToLineSeries();

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
                    Fill = null,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 1,
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

        private void SwitchToLineSeries()
        {
            if (Series.Count > 0 && Series[0] is LineSeries<DateTimePoint>)
                return;

            Series.Clear();
            Series.Add(
                new LineSeries<DateTimePoint>
                {
                    Name = "Volume",
                    Values = _values,
                    Stroke = new SolidColorPaint(new SKColor(80, 132, 221), 2),
                    Fill = null,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 1,
                }
            );
        }

        private void SwitchToRowSeries()
        {
            if (Series.Count > 0 && Series[0] is RowSeries<DateTimePoint>)
                return; // Already row series

            Series.Clear();
            Series.Add(
                new RowSeries<DateTimePoint>
                {
                    Name = "Volume",
                    Values = _values,
                    Stroke = new SolidColorPaint(new SKColor(80, 132, 221), 2),
                    Fill = new SolidColorPaint(new SKColor(80, 132, 221, 80)),
                    // For horizontal bars, map X as value (count) and Y as time (ticks)
                    Mapping = static (point, index) =>
                        new LiveChartsCore.Kernel.Coordinate(
                            point.Value ?? 0,
                            point.DateTime.Ticks
                        ),
                }
            );
        }

        public void Update(DateTimePoint point)
        {
            _values.Add(point);
            if (_values.Count > 3000)
                _values.RemoveAt(0);
            XAxes[0].MinLimit = point.DateTime.AddMinutes(-1).Ticks;
            XAxes[0].MaxLimit = point.DateTime.Ticks;

            var maxY = _values.Select(v => v.Value ?? 0).DefaultIfEmpty(0).Max();
            var padding = maxY * 0.2;
            if (padding == 0)
                padding = 10;

            var maxLimit = Math.Ceiling(maxY + padding);
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

        private int GetWeekOfMonth(DateTime date, DateTime startOfMonth)
        {
            var dayOfMonth = date.Day;
            var startDayOfWeek = (int)startOfMonth.DayOfWeek;
            return ((dayOfMonth + startDayOfWeek - 2) / 7) + 1;
        }

        private int GetWeeksInMonth(DateTime startOfMonth)
        {
            var daysInMonth = DateTime.DaysInMonth(startOfMonth.Year, startOfMonth.Month);
            var startDayOfWeek = (int)startOfMonth.DayOfWeek;
            var totalDays = daysInMonth + startDayOfWeek - 1;
            return (totalDays - 1) / 7 + 1;
        }

        public void FinalizeAggregatedData(string timeUnit)
        {
            // Switch to appropriate series type based on time unit
            if (timeUnit == "hour")
            {
                // Today view - keep as line series
                SwitchToLineSeries();
            }
            else
            {
                // Week and month views - use row series (horizontal bar chart)
                SwitchToRowSeries();
            }

            if (timeUnit == "day")
            {
                // For weekly view, ensure we show exactly 7 days ending with the latest date
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

                // Configure Y-axis to show dates (for horizontal bars, Y-axis shows categories)
                YAxes[0].Labeler = value => new DateTime((long)value).ToString("MM-dd");
                YAxes[0].MinLimit = startDate.AddHours(-12).Ticks;
                YAxes[0].MaxLimit = endDate.AddHours(12).Ticks;
                YAxes[0].UnitWidth = TimeSpan.FromDays(1).Ticks;

                // Configure X-axis to show values (for horizontal bars, X-axis shows values)
                XAxes[0].Labeler = value => ((int)value).ToString();
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
                // For monthly view, always show exactly 5 weeks (Week 1 to Week 5)
                var endDate =
                    _values.Count > 0 ? _values.Max(v => v.DateTime.Date) : DateTime.Today;

                // Calculate start of the month containing endDate
                var startOfMonth = new DateTime(endDate.Year, endDate.Month, 1);

                // Create week periods based on existing data
                var dataByWeek = new Dictionary<int, double>();

                // Group existing data by week within the month
                foreach (var point in _values)
                {
                    var weekOfMonth = GetWeekOfMonth(point.DateTime, startOfMonth);
                    if (weekOfMonth >= 1 && weekOfMonth <= 5) // Only consider weeks 1-5
                    {
                        if (!dataByWeek.ContainsKey(weekOfMonth))
                            dataByWeek[weekOfMonth] = 0;
                        dataByWeek[weekOfMonth] += point.Value ?? 0;
                    }
                }

                // Clear existing values and rebuild with exactly 5 weeks, evenly spaced
                _values.Clear();

                // Create evenly spaced week positions (similar to how week view handles 7 days)
                var baseDate = startOfMonth;
                for (int week = 1; week <= 5; week++)
                {
                    // Use a fixed spacing approach rather than actual calendar weeks
                    var weekPosition = baseDate.AddDays((week - 1) * 6); // 6-day spacing for better distribution
                    var value = dataByWeek.GetValueOrDefault(week, 0); // Use 0 if no data for this week
                    _values.Add(new DateTimePoint(weekPosition, value));
                }

                // Configure Y-axis for exactly 5 evenly distributed weeks
                YAxes[0].Labeler = value =>
                {
                    var tickTime = new DateTime((long)value);
                    
                    // Find which week this tick represents based on position
                    for (int i = 0; i < _values.Count; i++)
                    {
                        var dataPoint = _values[i];
                        if (Math.Abs((dataPoint.DateTime - tickTime).TotalDays) < 3)
                        {
                            return $"Week {i + 1}";
                        }
                    }
                    
                    return string.Empty; // Hide extra labels
                };
                
                // Set limits to evenly distribute the 5 weeks across the Y-axis
                var startLimit = baseDate.AddDays(-3); // Week 1 start with padding
                var endLimit = baseDate.AddDays(4 * 6 + 3); // Week 5 end with padding
                
                YAxes[0].MinLimit = startLimit.Ticks;
                YAxes[0].MaxLimit = endLimit.Ticks;
                YAxes[0].UnitWidth = TimeSpan.FromDays(6).Ticks; // Use 6-day intervals for even spacing

                // Configure X-axis to show values (for horizontal bars, X-axis shows values)
                XAxes[0].Labeler = value => ((int)value).ToString();
            }

            // Configure value axis based on actual data and series type
            var maxValue = _values.Select(v => v.Value ?? 0).DefaultIfEmpty(0).Max();
            var padding = maxValue * 0.2;
            if (padding == 0)
                padding = 10;

            var maxLimit = Math.Ceiling(maxValue + padding);

            if (timeUnit == "hour")
            {
                // Line series: values on Y-axis
                YAxes[0].MinLimit = 0;
                YAxes[0].MaxLimit = maxLimit;
                var step = maxLimit / 5;
                if (step > 0)
                {
                    YAxes[0].UnitWidth = step;
                }
            }
            else
            {
                // Row series: values on X-axis (horizontal bars)
                XAxes[0].MinLimit = 0;
                XAxes[0].MaxLimit = maxLimit;
                var step = maxLimit / 5;
                if (step > 0)
                {
                    XAxes[0].UnitWidth = step;
                }
            }
        }
    }
}
