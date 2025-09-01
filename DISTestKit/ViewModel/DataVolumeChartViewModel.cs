using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media;
using DISTestKit.Model;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

namespace DISTestKit.ViewModel
{
    public class DataVolumeChartViewModel : INotifyPropertyChanged
    {
        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }
        private ObservableCollection<DateTimePoint> _chartValues;

        public DataVolumeChartViewModel()
        {
            _chartValues = new ObservableCollection<DateTimePoint>();

            Series = new ISeries[]
            {
                new LineSeries<DateTimePoint>
                {
                    Values = _chartValues,
                    GeometrySize = 5,
                    Mapping = (point, index) =>
                        new LiveChartsCore.Kernel.Coordinate(point.DateTime.Ticks, point.Value),
                },
            };

            XAxes = new Axis[]
            {
                new Axis { Labeler = value => new DateTime((long)value).ToString("HH:mm:ss") },
            };

            YAxes = new Axis[] { new Axis { Labeler = value => ((int)value).ToString() } };
        }

        public void AddDataPoint(DateTime time, int messageCount)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _chartValues.Add(new DateTimePoint(time, messageCount));

                var cutOffTime = DateTime.Now.AddMinutes(-1);
                var toRemove = _chartValues.Where(v => v.DateTime < cutOffTime).ToList();
                foreach (var item in toRemove)
                    _chartValues.Remove(item);

                if (_chartValues.Count > 1)
                {
                    XAxes[0].MinLimit = _chartValues[0].DateTime.Ticks;
                    XAxes[0].MaxLimit = _chartValues[^1].DateTime.Ticks;
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class DateTimePoint
    {
        public DateTime DateTime { get; set; }
        public double Value { get; set; }

        public DateTimePoint(DateTime dateTime, double value)
        {
            DateTime = dateTime;
            Value = value;
        }
    }
}
