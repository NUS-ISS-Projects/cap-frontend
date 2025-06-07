using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
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
                    Stroke = new SolidColorPaint(SKColors.MediumPurple, 2),
                    Fill = new SolidColorPaint(new SKColor(128, 0, 128, 80))
                }
            };

            var now = DateTime.Now;
            XAxes = new[]
            {
                new Axis
                {
                    Labeler = value => new DateTime((long)value).ToString("HH:mm:ss"),
                    MinLimit = now.AddMinutes(-1).Ticks,
                    MaxLimit = now.Ticks
                }
            };
            YAxes = new[]
            {
                new Axis { MinLimit = 0, MaxLimit = 3000 }
            };
        }

        public void Update(DateTimePoint point)
        {
            _values.Add(point);
            if (_values.Count > 60) _values.RemoveAt(0);
            XAxes[0].MinLimit = _values[0].DateTime.Ticks;
            XAxes[0].MaxLimit = point.DateTime.Ticks;
        }
    }
}
