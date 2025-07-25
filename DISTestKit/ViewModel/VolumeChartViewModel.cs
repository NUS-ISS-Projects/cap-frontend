using System;
using System.Collections.ObjectModel;
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
                },
            ];
            YAxes = [new Axis { MinLimit = 0, MaxLimit = 3000 }];
        }

        public void Update(DateTimePoint point)
        {
            _values.Add(point);
            if (_values.Count > 60)
                _values.RemoveAt(0);
            XAxes[0].MinLimit = _values[0].DateTime.Ticks;
            XAxes[0].MaxLimit = point.DateTime.Ticks;

            var maxY = _values.Select(v => v.Value).DefaultIfEmpty(0).Max();
            var padding = maxY * 0.2;
            if (padding == 0)
                padding = 10;

            YAxes[0].MinLimit = 0;
            YAxes[0].MaxLimit = Math.Ceiling((maxY + padding).GetValueOrDefault());
        }
    }
}
