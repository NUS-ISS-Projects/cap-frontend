using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.Timers;
using System.Collections.ObjectModel;
using DISTestKit.Model;

namespace DISTestKit.ViewModel
{
    public class DataVolumeChartViewModel : INotifyPropertyChanged
    {
        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> XFormatter { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        private ChartValues<DateTimePoint> _chartValues;
        public Func<double, string> IntFormatter { get; set; } = val => ((int)val).ToString();
        public Separator IntSeparator { get; set; } = new Separator { Step = 1 };
        public DataVolumeChartViewModel()
        {
            _chartValues = new ChartValues<DateTimePoint>();

            SeriesCollection = new SeriesCollection
        {
            new LineSeries
            {
                Title = "DIS Messages/sec",
                Values = _chartValues,
                PointGeometrySize = 5
            }
        };

            XFormatter = val => new DateTime((long)val).ToString("HH:mm:ss");
            var now = DateTime.Now;
            MinX = now.AddMinutes(-1).Ticks;
            MaxX = now.Ticks;
        }

        public void AddDataPoint(DateTime time, int messageCount)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _chartValues.Add(new DateTimePoint(time, messageCount));
                _chartValues.RemoveAll(v => v.DateTime < DateTime.Now.AddMinutes(-1));

                if (_chartValues.Count > 1)
                {
                    MinX = _chartValues[0].DateTime.Ticks;
                    MaxX = _chartValues[^1].DateTime.Ticks;
                    OnPropertyChanged(nameof(MinX));
                    OnPropertyChanged(nameof(MaxX));
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class ChartValuesExtensions
    {
        public static void RemoveAll<T>(this ChartValues<T> values, Func<T, bool> predicate)
        {
            var toRemove = values.Where(predicate).ToList();
            foreach (var item in toRemove)
                values.Remove(item);
        }
    }

}


