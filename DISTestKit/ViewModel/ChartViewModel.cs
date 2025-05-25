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

namespace DISTestKit.ViewModel
{
    public class ChartViewModel: INotifyPropertyChanged
    {

        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> XFormatter { get; set; }
        //For simulating data without DIS messages. For testing only
        //public Func<double, string> YFormatter { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }

        private ChartValues<DateTimePoint> _chartValues;
        private int _messageCountThisSecond = 0;
        private System.Timers.Timer _timer;

        public ChartViewModel()
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

            //For testing only
            //YFormatter = val => val.ToString("N");

            var now = DateTime.Now;
            MinX = now.AddMinutes(-1).Ticks;
            MaxX = now.Ticks;

            _timer = new System.Timers.Timer(1000); // 1 second
            _timer.Elapsed += UpdateChart;
            _timer.Start();
        }

        private void UpdateChart(object sender, ElapsedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var now = DateTime.Now;

                //To test the chart without DIS messages. For testing only
                //_chartValues.Add(new DateTimePoint(now, new Random().Next(1, 10)));
                _chartValues.Add(new DateTimePoint(now, _messageCountThisSecond));

                foreach (var value in _chartValues)
                {
                    if(value != null)
                    {
                        if (value.DateTime < DateTime.Now.AddMinutes(-1))
                            _chartValues.Remove(value);
                    }
                }


                if (_chartValues.Count > 1)
                {
                    MinX = _chartValues[0].DateTime.Ticks;
                    MaxX = _chartValues[^1].DateTime.Ticks;
                    OnPropertyChanged(nameof(MinX));
                    OnPropertyChanged(nameof(MaxX));
                }

                _messageCountThisSecond = 0; // reset counter after plotting

            });
        }

        // Call this from DIS receiver each time a message is received
        public void IncrementMessageCount()
        {
            _messageCountThisSecond++;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    }
}
