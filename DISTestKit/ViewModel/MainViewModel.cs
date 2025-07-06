using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Timers;
using LiveChartsCore.Defaults;
using DISTestKit.Services;
using DISTestKit.ViewModel;
using System.Linq;
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
                if (_isPlaying == value) return;
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
                if (_selectedDate == value) return;
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                if (IsPaused) _ = LoadOnceAsync();
            }
        }

        private DateTime? _selectedTime = null;
        public DateTime? SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (_selectedTime == value) return;
                _selectedTime = value;
                OnPropertyChanged(nameof(SelectedTime));
                if (IsPaused) _ = LoadOnceAsync();
            }
        }

        // For a selected time, define a 1-hour window
        private DateTime StartDateTime => SelectedDate.Date;
        private DateTime EndDateTime => SelectedTime.HasValue
            ? SelectedDate.Date + SelectedTime.Value.TimeOfDay + TimeSpan.FromHours(1)
            : SelectedDate.Date.AddDays(1).AddTicks(-1);

        // ––– Dashboard Metrics ––––––––––––––––––––––––––––––––––––––––––
        public int    TotalPdusLastMinute   { get; private set; }
        public double AveragePdusPerSecond  { get; private set; }
        public double PeakPdusPerSecond     { get; private set; }
        public string EntityVsFireSummary   { get; private set; }
        private readonly Queue<int> _window = new();

        // ––– Infrastructure –––––––––––––––––––––––––––––––––––––––––––––
        private readonly RealTimeMetricsService _metricsSvc;
        private readonly Timer _timer;
        private long _lastTimestamp;


        public MainViewModel()
        {
            _metricsSvc = new RealTimeMetricsService("http://34.102.132.29/api/acquisition/");
            VolumeVm = new VolumeChartViewModel();
            ThroughputVm = new ThroughputChartViewModel();
            ComparisonVm = new PduTypeComparisonViewModel();
            LogsVm = new LogViewModel();
            DataVolumeVm = new DataVolumeChartViewModel();

            EntityVsFireSummary = string.Empty;

            PlayCommand    = new RelayCommand(() => IsPlaying = !IsPlaying);
            RefreshCommand = new RelayCommand(() =>
            {
                SelectedDate = DateTime.Now.Date;
                SelectedTime = null;
                _ = LoadOnceAsync();
            });

            _timer = new Timer(1000) { AutoReset = true };
            _timer.Elapsed += async (_, __) => await OnTickAsync();
            _lastTimestamp = 0;
            IsPlaying = true;
        }

        private async Task LoadOnceAsync()
        {
            LogsVm.Reset();
            var startDt = StartDateTime;
            var endDt = EndDateTime;

            // Convert to DIS timestamps
            var startTs = RealTimeMetricsService.ToDisAbsoluteTimestamp(
                new DateTimeOffset(startDt).ToUnixTimeSeconds());
            var endTs = RealTimeMetricsService.ToDisAbsoluteTimestamp(
                new DateTimeOffset(endDt).ToUnixTimeSeconds());

            // Fetch and push historical rows
            var states = await _metricsSvc.GetHistoricalEntityStatesAsync(startTs, endTs);
            var fires = await _metricsSvc.GetHistoricalFireEventsAsync(startTs, endTs);

            App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var s in states)
                        LogsVm.AddEntityState(
                            s.Timestamp, s.Site, s.Application, s.Entity,
                            s.LocationX, s.LocationY, s.LocationZ);

                    foreach (var f in fires)
                        LogsVm.AddFireEvent(
                            f.Timestamp,
                            f.FiringSite, f.FiringApplication, f.FiringEntity,
                            f.TargetSite, f.TargetApplication, f.TargetEntity,
                            f.MunitionSite, f.MunitionApplication, f.MunitionEntity);
                });
            _lastTimestamp = endTs;
        }

        private async Task OnTickAsync()
        {
            try
            {
                // First update charts from /realtime
                var dto = await _metricsSvc.GetAsync();
                var nowLocal = DateTimeOffset
                        .FromUnixTimeMilliseconds(dto.LastPduReceivedTimestampMs)
                        .LocalDateTime;
                _window.Enqueue((int)dto.PdusInLastSixtySeconds);
                if (_window.Count > 60) _window.Dequeue();

                TotalPdusLastMinute    = _window.Sum();
                AveragePdusPerSecond   = _window.Average();
                PeakPdusPerSecond      = _window.Max();
                OnPropertyChanged(nameof(TotalPdusLastMinute));
                OnPropertyChanged(nameof(AveragePdusPerSecond));
                OnPropertyChanged(nameof(PeakPdusPerSecond));

                var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var nowTs   = RealTimeMetricsService.ToDisAbsoluteTimestamp(nowSecs);
                var newStates = await _metricsSvc.GetHistoricalEntityStatesAsync(_lastTimestamp, nowTs);
                var newFires  = await _metricsSvc.GetHistoricalFireEventsAsync   (_lastTimestamp, nowTs);
                var entityCount = newStates.LongCount();
                var fireCount   = newFires .LongCount();
                EntityVsFireSummary = $"{entityCount} / {fireCount}";
                OnPropertyChanged(nameof(EntityVsFireSummary));

                App.Current.Dispatcher.Invoke(() =>
                {
                    VolumeVm.Update(new DateTimePoint(nowLocal, dto.PdusInLastSixtySeconds));
                    ThroughputVm.Update(new DateTimePoint(nowLocal, dto.AveragePduRatePerSecondLastSixtySeconds));
                    DataVolumeVm.AddDataPoint(nowLocal, (int)(dto.PdusInLastSixtySeconds/60));

                    ComparisonVm.UpdateCounts(entityCount, fireCount);

                    foreach (var s in newStates)
                    LogsVm.AddEntityState(
                        s.Timestamp, s.Site, s.Application, s.Entity,
                        s.LocationX, s.LocationY, s.LocationZ);

                    foreach (var f in newFires)
                    LogsVm.AddFireEvent(
                        f.Timestamp,
                        f.FiringSite, f.FiringApplication, f.FiringEntity,
                        f.TargetSite,  f.TargetApplication,  f.TargetEntity,
                        f.MunitionSite,f.MunitionApplication,f.MunitionEntity);
                });

                // Advance bookmark
                var maxState = newStates.Select(s => s.Timestamp).DefaultIfEmpty(_lastTimestamp).Max();
                var maxFire  = newFires .Select(f => f.Timestamp).DefaultIfEmpty(_lastTimestamp).Max();
                _lastTimestamp = Math.Max(_lastTimestamp, Math.Max(maxState, maxFire));
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