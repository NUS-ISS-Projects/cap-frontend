using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Timers;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

using DISTestKit.Model;
using Timer = System.Timers.Timer;

namespace DISTestKit.ViewModel
{
    public class ChartViewModel: INotifyPropertyChanged
    {

        public ObservableCollection<ISeries> Series { get; set; } = [];
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        private readonly ObservableCollection<DateTimePoint> _chartValues = [];
        private int _messageCountThisSecond;
        private readonly Timer _timer;
        
        public ObservableCollection<DisPacket> Packets { get; set; } = new();

        private int _nextPacketNo = 1;

         public ChartViewModel()
        {
            _chartValues = new ObservableCollection<DateTimePoint>();

            Series = new ObservableCollection<ISeries>()
            {
                new LineSeries<DateTimePoint>
                {
                    Name        = "DIS Messages/sec",
                    Values      = _chartValues,
                    GeometrySize= 8,
                    Stroke      = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    Fill        = null
                }
            };

            // set up the axes
            var now = DateTime.Now;

            XAxes = new[]
            {
                new Axis
                {
                    Name     = "Time",
                    Labeler  = value => new DateTime((long)value).ToString("HH:mm:ss"),
                    MinLimit = now.AddMinutes(-1).Ticks,
                    MaxLimit = now.Ticks
                }
            };
            YAxes = new[]
            {
                new Axis { Name = "Messages/sec", MinLimit = 0 }
            };

            // start a System.Timers.Timer (aliased above)
            _timer = new Timer(1000);
            _timer.Elapsed += UpdateChart;
            _timer.Start();
        }


       private void UpdateChart(object? sender, ElapsedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var now = DateTime.Now;
                _chartValues.Add(new DateTimePoint(now, _messageCountThisSecond));

                // Remove points older than 1 minute
                while (_chartValues.Count > 0 && _chartValues[0].DateTime < DateTime.Now.AddMinutes(-1))
                {
                    _chartValues.RemoveAt(0);
                }

                // slide the X-axis window
                XAxes[0].MinLimit = _chartValues.Count > 0
                    ? _chartValues[0].DateTime.Ticks
                    : now.AddMinutes(-1).Ticks;
                XAxes[0].MaxLimit = now.Ticks;

                // notify the view
                OnPropertyChanged(nameof(XAxes));
                OnPropertyChanged(nameof(Series));

                _messageCountThisSecond = 0;
            });
        }

        // Call this from DIS receiver each time a message is received
        public void IncrementMessageCount()
        {
            _messageCountThisSecond++;
        }

        /// <summary>
        /// Call this whenever a DIS message arrives—this method creates a DisPacket
        /// instance and adds it to the “Packets” collection so the DataGrid will update.
        /// </summary>
        public void AddPacket(string source, string destination, string protocol, int length, string info)
        {
            var packet = new DisPacket
            {
                No = _nextPacketNo++,
                Time = DateTime.Now,
                Source = source,
                Destination = destination,
                Protocol = protocol,
                Length = length,
                Info = info
            };

            // Must dispatch to UI‐thread because Packets is bound to DataGrid
            App.Current.Dispatcher.Invoke(() =>
            {
                Packets.Add(packet);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    }
}
