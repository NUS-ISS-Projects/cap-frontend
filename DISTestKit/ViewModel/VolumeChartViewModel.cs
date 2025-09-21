using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DISTestKit.ViewModel
{
    public class VolumeChartViewModel : System.ComponentModel.INotifyPropertyChanged
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

            // Reset Y-axis to default real-time configuration (for LineSeries, Y-axis shows values)
            YAxes[0].Labeler = value => ((int)value).ToString();
            YAxes[0].MinLimit = 0;
            YAxes[0].MaxLimit = 3000;
            YAxes[0].UnitWidth = double.NaN; // Let LiveCharts auto-calculate step size
        }

        public ObservableCollection<ISeries> Series { get; private set; }
        public Axis[] XAxes { get; }
        public Axis[] YAxes { get; }

        public VolumeChartViewModel()
        {
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Traffic Volume (packets)",
                    Values = _values,
                    Stroke = new SolidColorPaint(new SKColor(80, 132, 221), 2),
                    Fill = null,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 1,
                    Mapping = static (point, index) =>
                        new LiveChartsCore.Kernel.Coordinate(
                            point.DateTime.Ticks,
                            point.Value ?? 0
                        ),
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
            // Force a hard swap so the chart re-templates from bar -> line reliably
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Traffic Volume (packets)",
                    Values = _values,
                    Stroke = new SolidColorPaint(new SKColor(80, 132, 221), 2),
                    Fill = null,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 1,
                    Mapping = static (point, index) =>
                        new LiveChartsCore.Kernel.Coordinate(
                            point.DateTime.Ticks,
                            point.Value ?? 0
                        ),
                },
            };
            OnPropertyChanged(nameof(Series));
        }

        private void SwitchToRowSeries()
        {
            // Force a hard swap so the chart re-templates from line -> bar reliably
            var row = new RowSeries<DateTimePoint>
            {
                Name = "Traffic Volume (packets)",
                Values = _values,
                Stroke = new SolidColorPaint(new SKColor(80, 132, 221), 2),
                Fill = new SolidColorPaint(new SKColor(80, 132, 221, 80)),
                // For horizontal bars, map X as value (count) and Y as time (ticks)
                Mapping = static (point, index) =>
                    new LiveChartsCore.Kernel.Coordinate(
                        point.Value ?? 0,
                        point.DateTime.Ticks
                    ),
            };
            // Default tooltip: header shows Y (date) and line text shows X (count)
            TrySetToolTipFormatter(row, p => $"{p.Coordinate.PrimaryValue:N0}");
            TrySetXYToolTipFormatters(
                row,
                x => $"{x.Coordinate.PrimaryValue:N0}",
                y =>
                {
                    try { return new DateTime((long)y.Coordinate.SecondaryValue).ToString("MM-dd"); }
                    catch { return string.Empty; }
                }
            );

            Series = new ObservableCollection<ISeries> { row };
            OnPropertyChanged(nameof(Series));
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

                // Tooltip: show date (MM-dd) + total volume, and header as date
                if (Series.Count > 0 && Series[0] is RowSeries<DateTimePoint> rsDay)
                {
                    TrySetToolTipFormatter(
                        rsDay,
                        p =>
                        {
                            var dt = new DateTime((long)p.Coordinate.SecondaryValue);
                            return $"{dt:MM-dd}: {p.Coordinate.PrimaryValue:N0}";
                        }
                    );
                    TrySetXYToolTipFormatters(
                        rsDay,
                        x => $"{x.Coordinate.PrimaryValue:N0}",
                        y => new DateTime((long)y.Coordinate.SecondaryValue).ToString("MM-dd")
                    );
                }
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

                // Ensure Y axis shows numeric values after coming from bar views
                YAxes[0].Labeler = value => ((int)value).ToString();
                YAxes[0].MinLimit = 0;
                YAxes[0].UnitWidth = double.NaN; // let LiveCharts decide until we set it below
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

                // Tooltip: show Week N + total volume, and header as Week N
                if (Series.Count > 0 && Series[0] is RowSeries<DateTimePoint> rsWeek)
                {
                    TrySetToolTipFormatter(
                        rsWeek,
                        p =>
                        {
                            // Find the closest week index to the hovered point
                            var tickTime = new DateTime((long)p.Coordinate.SecondaryValue);
                            var idx = -1;
                            var minDiff = double.MaxValue;
                            for (int i = 0; i < _values.Count; i++)
                            {
                                var diff = Math.Abs((_values[i].DateTime - tickTime).TotalDays);
                                if (diff < minDiff)
                                {
                                    minDiff = diff;
                                    idx = i;
                                }
                            }
                            var label = idx >= 0 ? $"Week {idx + 1}" : "Week";
                            return $"{label}: {p.Coordinate.PrimaryValue:N0}";
                        }
                    );
                    TrySetXYToolTipFormatters(
                        rsWeek,
                        x => $"{x.Coordinate.PrimaryValue:N0}",
                        y =>
                        {
                            var tickTime = new DateTime((long)y.Coordinate.SecondaryValue);
                            var idx = -1;
                            var minDiff = double.MaxValue;
                            for (int i = 0; i < _values.Count; i++)
                            {
                                var diff = Math.Abs((_values[i].DateTime - tickTime).TotalDays);
                                if (diff < minDiff) { minDiff = diff; idx = i; }
                            }
                            return idx >= 0 ? $"Week {idx + 1}" : "Week";
                        }
                    );
                }
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

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private static void TrySetToolTipFormatter(
            ISeries series,
            Func<LiveChartsCore.Kernel.ChartPoint, string> formatter
        )
        {
            try
            {
                var t = series.GetType();
                var prop = t.GetProperty("ToolTipLabelFormatter")
                           ?? t.GetProperty("TooltipLabelFormatter");
                if (prop?.SetMethod != null)
                {
                    prop.SetValue(series, formatter);
                    return;
                }

                // try via interface
                var iface = typeof(ISeries);
                var ip = iface.GetProperty("ToolTipLabelFormatter")
                          ?? iface.GetProperty("TooltipLabelFormatter");
                var set = ip?.SetMethod;
                if (set != null)
                {
                    set.Invoke(series, new object[] { formatter });
                }
            }
            catch
            {
                // ignore — tooltip stays default
            }
        }

        private static void TrySetXYToolTipFormatters(
            ISeries series,
            Func<LiveChartsCore.Kernel.ChartPoint, string> xFormatter,
            Func<LiveChartsCore.Kernel.ChartPoint, string> yFormatter
        )
        {
            try
            {
                var t = series.GetType();
                var xp = t.GetProperty("XToolTipLabelFormatter");
                var yp = t.GetProperty("YToolTipLabelFormatter");
                xp?.SetValue(series, xFormatter);
                yp?.SetValue(series, yFormatter);

                if (xp == null || yp == null)
                {
                    // try on interface
                    var iface = typeof(ISeries);
                    xp = iface.GetProperty("XToolTipLabelFormatter");
                    yp = iface.GetProperty("YToolTipLabelFormatter");
                    xp?.SetValue(series, xFormatter);
                    yp?.SetValue(series, yFormatter);
                }
            }
            catch
            {
                // ignore — leave defaults
            }
        }
    }
}
