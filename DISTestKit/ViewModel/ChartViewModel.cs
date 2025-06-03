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

        // ──────────────── VOLUME CHART ────────────────
        public ObservableCollection<ISeries> VolumeSeries { get; set; }
        public Axis[] VolumeXAxes { get; set; }
        public Axis[] VolumeYAxes { get; set; }

        // ───────────── THROUGHPUT CHART ─────────────
        public ObservableCollection<ISeries> ThroughputSeries { get; set; }
        public Axis[] ThroughputXAxes { get; set; }
        public Axis[] ThroughputYAxes { get; set; }

        // ───────────── RETRANSMITS CHART ─────────────
        public ObservableCollection<ISeries> RetransmitSeries { get; set; }
        public Axis[] RetransmitXAxes { get; set; }
        public Axis[] RetransmitYAxes { get; set; }

        // ───────────── DATA GRID (Packets) ────────────
        public ObservableCollection<DisPacket> Packets { get; set; }
        private int _nextPacketNo = 1;

        // ───────────── PRIVATE IN‐MEMORY STORAGE ────────
        private readonly ObservableCollection<DateTimePoint> _volumeValues;
        private readonly ObservableCollection<DateTimePoint> _throughputValues;
        private readonly ObservableCollection<DateTimePoint> _retransmitValues;
        private int _tickingCountVolume = 0;
        private int _tickingCountThroughput = 0;
        private int _tickingCountRetransmit = 0;
        private int _messageCountThisSecond;
        private readonly Timer _timer;

        public ChartViewModel()
        {
            _volumeValues = new ObservableCollection<DateTimePoint>();
            VolumeSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Volume",
                    Values = _volumeValues,
                    Stroke = new SolidColorPaint(SKColors.MediumPurple, 2),
                    Fill = new SolidColorPaint(new SKColor(128, 0, 128, 80))
                }
            };

            _throughputValues = new ObservableCollection<DateTimePoint>();
            ThroughputSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Throughput",
                    Values = _throughputValues,
                    Stroke = new SolidColorPaint(SKColors.CornflowerBlue, 2),
                    Fill = null
                }
            };

            _retransmitValues = new ObservableCollection<DateTimePoint>();
            RetransmitSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<DateTimePoint>
                {
                    Name = "Retransmits",
                    Values = _retransmitValues,
                    Stroke = null,
                    Fill = new SolidColorPaint(SKColors.SteelBlue.WithAlpha(200))
                }
            };

            // set up the axes
            var now = DateTime.Now;

            VolumeXAxes = new[]
            {
                new Axis
                {
                    Name     = "",
                    Labeler  = value => new DateTime((long)value).ToString("HH:mm:ss"),
                    MinLimit = now.AddMinutes(-1).Ticks,
                    MaxLimit = now.Ticks
                }
            };
            VolumeYAxes = new[]
            {
                new Axis { Name = "", MinLimit = 0, MaxLimit = 300 }
            };

            ThroughputXAxes = new[]
            {
                new Axis
                {
                    Name     = "",
                    Labeler  = value => new DateTime((long)value).ToString("HH:mm"),
                    MinLimit = now.AddMinutes(-10).Ticks,
                    MaxLimit = now.Ticks
                }
            };
            ThroughputYAxes = new[]
            {
                new Axis
                {
                    Name = "",
                    MinLimit = 0,
                    MaxLimit = 800 // for mock
                }
            };

            RetransmitXAxes = new[]
            {
                new Axis
                {
                    Name     = "",
                    Labeler  = value => new DateTime((long)value).ToString("HH:mm"),
                    MinLimit = now.AddMinutes(-10).Ticks,
                    MaxLimit = now.Ticks
                }
            };
            RetransmitYAxes = new[]
            {
                new Axis
                {
                    Name = "",
                    MinLimit = 0,
                    MaxLimit = 10 
                }
            };


            // ──────────────── Start a Timer ────────────────
            _timer = new Timer(1000);
            _timer.Elapsed += Timer_Tick;
            _timer.Start();

            Packets = new ObservableCollection<DisPacket>();
            MockPackets();
        }

        private void MockPackets()
        {
            var now = DateTime.Now;
            Packets.Add(new DisPacket
            {
                No = _nextPacketNo++,
                Time = now.AddSeconds(-50),
                Source = "192.168.0.10",
                Destination = "192.168.0.20",
                Protocol = "EntityStatePdu",
                Length = 200,
                Info = "Volume=120, Thrpt=500, Retrans=2"
            });
            Packets.Add(new DisPacket
            {
                No = _nextPacketNo++,
                Time = now.AddSeconds(-40),
                Source = "192.168.0.11",
                Destination = "192.168.0.21",
                Protocol = "FirePdu",
                Length = 220,
                Info = "Volume=110, Thrpt=450, Retrans=3"
            });
            Packets.Add(new DisPacket
            {
                No = _nextPacketNo++,
                Time = now.AddSeconds(-30),
                Source = "192.168.0.12",
                Destination = "192.168.0.22",
                Protocol = "DetonationPdu",
                Length = 230,
                Info = "Volume=130, Thrpt=600, Retrans=1"
            });
            Packets.Add(new DisPacket
            {
                No = _nextPacketNo++,
                Time = now.AddSeconds(-20),
                Source = "192.168.0.13",
                Destination = "192.168.0.23",
                Protocol = "EntityStatePdu",
                Length = 210,
                Info = "Volume=125, Thrpt=550, Retrans=2"
            });
            Packets.Add(new DisPacket
            {
                No = _nextPacketNo++,
                Time = now.AddSeconds(-10),
                Source = "192.168.0.14",
                Destination = "192.168.0.24",
                Protocol = "EntityStatePdu",
                Length = 215,
                Info = "Volume=140, Thrpt=620, Retrans=0"
            });
            Packets.Add(new DisPacket
            {
                No = _nextPacketNo++,
                Time = now,
                Source = "192.168.0.15",
                Destination = "192.168.0.25",
                Protocol = "FirePdu",
                Length = 225,
                Info = "Volume=135, Thrpt=580, Retrans=4"
            });
        }


       private void Timer_Tick(object? sender, ElapsedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var now = DateTime.Now;
               
                _volumeValues.Add(new DateTimePoint(now, _messageCountThisSecond));
                if (_volumeValues.Count > 10) _volumeValues.RemoveAt(0);

                _throughputValues.Add(new DateTimePoint(now, 300 + 150 * Math.Cos(_tickingCountThroughput * 0.07)));
                if (_throughputValues.Count > 10) _throughputValues.RemoveAt(0);
                _tickingCountThroughput++;

                int r = (_tickingCountRetransmit % 5 == 0) ? 3 : 1;
                _retransmitValues.Add(new DateTimePoint(now, r));
                if (_retransmitValues.Count > 10) _retransmitValues.RemoveAt(0);
                _tickingCountRetransmit++;

                _messageCountThisSecond = 0;

                OnPropertyChanged(nameof(VolumeXAxes));
                OnPropertyChanged(nameof(ThroughputXAxes));
                OnPropertyChanged(nameof(RetransmitXAxes));
                OnPropertyChanged(nameof(VolumeSeries));
                OnPropertyChanged(nameof(ThroughputSeries));
                OnPropertyChanged(nameof(RetransmitSeries));
            });
        }
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
