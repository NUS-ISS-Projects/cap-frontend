using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DISTestKit.ViewModel
{
    public class ThroughputChartViewModel
    {
        private readonly ObservableCollection<DateTimePoint> _values = new();
        public ObservableCollection<ISeries> Series { get; }
        public Axis[] XAxes { get; }
        public Axis[] YAxes { get; }

        public ThroughputChartViewModel()
        {
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Throughput",
                    Values = _values,
                    Stroke = new SolidColorPaint(SKColors.CornflowerBlue, 2),
                    Fill = null,
                },
            };

            var now = DateTime.Now;
            XAxes = new[]
            {
                new Axis
                {
                    Labeler = value => new DateTime((long)value).ToString("HH:mm"),
                    MinLimit = now.AddMinutes(-10).Ticks,
                    MaxLimit = now.Ticks,
                },
            };
            YAxes = new[]
            {
                new Axis { MinLimit = 0, MaxLimit = 800 },
            };
        }

        public void Update(DateTimePoint point)
        {
            _values.Add(point);
            if (_values.Count > 60)
                _values.RemoveAt(0);
            XAxes[0].MinLimit = _values[0].DateTime.Ticks;
            XAxes[0].MaxLimit = point.DateTime.Ticks;
        }
    }
}
