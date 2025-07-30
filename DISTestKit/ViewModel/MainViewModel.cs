using System.ComponentModel;
using System.Windows.Input;
using DISTestKit.Services;
using LiveChartsCore.Defaults;
using Timer = System.Timers.Timer;

namespace DISTestKit.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

        public bool CanExecute(object? _) => true;

        public void Execute(object? _) => _execute();

        public event EventHandler? CanExecuteChanged;
    }

    public enum Period
    {
        None,
        Today,
        Week,
        Month,
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        // ––– Sub-ViewModels ––––––––––––––––––––––––––––––––––––––––––––––
        public VolumeChartViewModel VolumeVm { get; }
        public ThroughputChartViewModel ThroughputVm { get; }
        public PduTypeComparisonViewModel ComparisonVm { get; }

        public DataVolumeChartViewModel DataVolumeVm { get; }
        public LogViewModel LogsVm { get; }

        // ––– Commands & Playback –––––––––––––––––––––––––––––––––––––––––
        public ICommand PlayCommand { get; }
        public ICommand RefreshCommand { get; }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value)
                    return;
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(IsPaused));
                _timer.Enabled = value;

                // When pausing, immediately load the selected range
                if (!value)
                    _ = LoadOnceAsync();
                else
                {
                    // On resume, clear any manual selection to show real-time
                    SelectedTime = null;
                    SelectedDate = DateTime.Now.Date;
                    _lastTimestamp = 0;
                    SelectedPeriod = Period.None;
                }
            }
        }
        public bool IsPaused => !_isPlaying;

        // ––– Date/Time Selection ––––––––––––––––––––––––––––––––––––––––

        private DateTime _selectedDate = DateTime.Now.Date;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate == value)
                    return;
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                if (IsPaused)
                    _ = LoadOnceAsync();
            }
        }

        private DateTime? _selectedTime = null;
        public DateTime? SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (_selectedTime == value)
                    return;
                _selectedTime = value;
                OnPropertyChanged(nameof(SelectedTime));
                if (IsPaused)
                    _ = LoadOnceAsync();
            }
        }
        private DateTime StartDateTime => SelectedDate.Date;
        private DateTime EndDateTime =>
            SelectedTime.HasValue
                ? SelectedDate.Date + SelectedTime.Value.TimeOfDay + TimeSpan.FromHours(1)
                : SelectedDate.Date.AddDays(1).AddTicks(-1);

        private Period _selectedPeriod = Period.None;
        public Period SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (_selectedPeriod == value)
                    return;
                _selectedPeriod = value;
                OnPropertyChanged(nameof(SelectedPeriod));
                if (value != Period.None)
                    IsPlaying = false;
            }
        }
        public ICommand TodayCommand { get; }
        public ICommand WeekCommand { get; }
        public ICommand MonthCommand { get; }

        // ––– Infrastructure –––––––––––––––––––––––––––––––––––––––––––––
        private readonly string baseURL;
        private readonly RealTimeLogsService _realTimeLogsSvc;
        private readonly RealTimeMetricsService _realTimeMetricsSvc;
        private readonly AggregationService _aggregationSvc;
        private readonly Timer _timer;
        private long _lastTimestamp;
        private int? _previousTotalPackets;

        public MainViewModel()
        {
            baseURL = "http://localhost:32080/api/";
            _realTimeMetricsSvc = new RealTimeMetricsService(baseURL);
            _realTimeLogsSvc = new RealTimeLogsService(baseURL);
            _aggregationSvc = new AggregationService(baseURL);
            VolumeVm = new VolumeChartViewModel();
            ThroughputVm = new ThroughputChartViewModel();
            ComparisonVm = new PduTypeComparisonViewModel();
            LogsVm = new LogViewModel();
            DataVolumeVm = new DataVolumeChartViewModel();
            TodayCommand = new RelayCommand(() => SelectedPeriod = Period.Today);
            WeekCommand = new RelayCommand(() => SelectedPeriod = Period.Week);
            MonthCommand = new RelayCommand(() => SelectedPeriod = Period.Month);

            PlayCommand = new RelayCommand(() => IsPlaying = !IsPlaying);
            RefreshCommand = new RelayCommand(() =>
            {
                SelectedDate = DateTime.Now.Date;
                SelectedTime = null;
                _ = LoadOnceAsync();
            });

            _timer = new Timer(2000) { AutoReset = true };
            _timer.Elapsed += async (_, __) => await OnTickAsync();
            _lastTimestamp = 0;
            IsPlaying = true;
        }

        public async Task LoadOnceAsync()
        {
            VolumeVm.Clear();
            LogsVm.Reset();

            if (IsPaused)
            {
                var agg = await _aggregationSvc.GetAggregateAsync(
                    today: SelectedPeriod == Period.Today,
                    week: SelectedPeriod == Period.Week,
                    month: SelectedPeriod == Period.Month,
                    date: SelectedPeriod == Period.None && !SelectedTime.HasValue
                        ? SelectedDate
                        : (DateTime?)null,
                    startDate: SelectedTime.HasValue ? SelectedDate : null,
                    endDate: SelectedTime.HasValue ? SelectedDate : null
                );

                foreach (var b in agg.Buckets)
                {
                    DateTime when = agg.TimeUnit switch
                    {
                        "hour" => SelectedDate.AddHours(b.Hour ?? 0),
                        "day" => DateTime.Parse(b.Date!),
                        "week" => SelectedDate,
                        _ => SelectedDate,
                    };

                    VolumeVm.Update(new DateTimePoint(when, b.TotalPackets));
                }
            }
            else
            {
                var startDt = StartDateTime;
                var endDt = EndDateTime;

                // Convert to DIS timestamps
                var startUnixTs = new DateTimeOffset(startDt).ToUnixTimeSeconds();
                var endUnixTs = new DateTimeOffset(endDt).ToUnixTimeSeconds();

                // Fetch and push historical rows
                var raw = await _realTimeLogsSvc.GetRealTimeLogsAsync(startUnixTs, endUnixTs);

                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var m in raw)
                        LogsVm.AddPacket(m.Id, m.PDUType, m.Length, m.RecordDetails);
                });
                _lastTimestamp = RealTimeLogsService.ToDisAbsoluteTimestamp(endUnixTs);
            }
        }

        private async Task OnTickAsync()
        {
            try
            {
                // Update chart from  metrics endpoint  ──────────────────────────────
                var metrics = await _realTimeMetricsSvc.GetMetricsAsync();
                var time = metrics.DataUntilUtc.ToLocalTime();

                int delta;
                if (_previousTotalPackets.HasValue)
                {
                    delta = metrics.TotalPackets - _previousTotalPackets.Value;
                    if (delta < 0)
                        delta = 0;
                }
                else
                    delta = 0;
                _previousTotalPackets = metrics.TotalPackets;

                App.Current.Dispatcher.Invoke(() =>
                    VolumeVm.Update(new DateTimePoint(time, delta))
                );

                // update logs from  logs endpoint ─────────────────────────────────

                var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var lastUnixTimestamp = RealTimeLogsService.FromDisAbsoluteTimestamp(
                    _lastTimestamp
                );
                var newLogs = await _realTimeLogsSvc.GetRealTimeLogsAsync(
                    lastUnixTimestamp,
                    nowSecs
                );

                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var m in newLogs)
                        LogsVm.AddPacket(m.Id, m.PDUType, m.Length, m.RecordDetails);
                });

                _lastTimestamp = RealTimeLogsService.ToDisAbsoluteTimestamp(nowSecs);
            }
            catch
            {
                // ignore
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
