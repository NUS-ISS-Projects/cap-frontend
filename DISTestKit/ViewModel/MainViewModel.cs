using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Timers;
using LiveChartsCore.Defaults;
using DISTestKit.Services;
using DISTestKit.Model;
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
        public VolumeChartViewModel VolumeVm { get; }
        public ThroughputChartViewModel ThroughputVm { get; }
        public PduTypeComparisonViewModel ComparisonVm { get; }

        public DataVolumeChartViewModel DataVolumeVm { get; }
        public LogViewModel LogsVm { get; }

        public ICommand PlayCommand { get; }
        public ICommand RefreshCommand { get; }

        private readonly RealTimeMetricsService _metricsSvc;
        private readonly Timer _timer;
        private bool _isPlaying;
        private long _lastTimestamp = 0; 

        public int    TotalPdusLastMinute     { get; private set; }
        public double AveragePdusPerSecond    { get; private set; }
        public double PeakPdusPerSecond       { get; private set; }
        public string EntityVsFireSummary     { get; private set; }
        private readonly Queue<int> _window = new(); 

        public MainViewModel()
        {
            _metricsSvc = new RealTimeMetricsService("http://34.98.89.167/api/acquisition/");
            VolumeVm = new VolumeChartViewModel();
            ThroughputVm = new ThroughputChartViewModel();
            ComparisonVm = new PduTypeComparisonViewModel();
            LogsVm = new LogViewModel();
            DataVolumeVm = new DataVolumeChartViewModel();

            EntityVsFireSummary = string.Empty;

            PlayCommand = new RelayCommand(TogglePlay);
            RefreshCommand = new RelayCommand(() => _ = LoadOnceAsync());

            _timer = new Timer(1000) { AutoReset = true };
            _timer.Elapsed += async (_, __) => await OnTickAsync();
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
                _timer.Enabled = value;
            }
        }

        private void TogglePlay() => IsPlaying = !IsPlaying;

        private async Task LoadOnceAsync()
        {
            var was = IsPlaying;
            IsPlaying = false;
            LogsVm.Reset();

            // compute the end‐timestamp from pickers:
            var dt       = SelectedDate + SelectedTime;
            var unixSec  = new DateTimeOffset(dt).ToUnixTimeSeconds();
            var endDisTs = RealTimeMetricsService.ToDisAbsoluteTimestamp(unixSec);

            // compute start = one minute before (or whatever window):
            var startUnixSec = new DateTimeOffset(dt.AddMinutes(-1)).ToUnixTimeSeconds();
            var startDisTs   = RealTimeMetricsService.ToDisAbsoluteTimestamp(startUnixSec);

            // Fetch historical entity-states
            var states = await _metricsSvc.GetHistoricalEntityStatesAsync(startDisTs, endDisTs);
            foreach (var s in states)
            {
                LogsVm.AddEntityState(
                    s.Timestamp,
                    s.Site, s.Application, s.Entity,
                    s.LocationX, s.LocationY, s.LocationZ
                );
            }

            // Fetch historical fire-events
            var fires = await _metricsSvc.GetHistoricalFireEventsAsync(startDisTs, endDisTs);
            foreach (var f in fires)
            LogsVm.AddFireEvent(
                f.Timestamp,
                f.FiringSite, f.FiringApplication, f.FiringEntity,
                f.TargetSite, f.TargetApplication, f.TargetEntity,
                f.MunitionSite, f.MunitionApplication, f.MunitionEntity);

            _lastTimestamp = Math.Max(startDisTs, endDisTs);

            IsPlaying = was;
        }

        private async Task OnTickAsync()
        {
            try
            {
                // First update charts from /realtime
                var dto = await _metricsSvc.GetAsync();
                var countThisSecond = dto.PdusInLastSixtySeconds;
                _window.Enqueue((int)countThisSecond);
                if (_window.Count > 60) _window.Dequeue();
                TotalPdusLastMinute = _window.Sum();
                AveragePdusPerSecond = _window.Average();
                PeakPdusPerSecond = _window.Max();
            
                var ts  = DateTimeOffset
                            .FromUnixTimeMilliseconds(dto.LastPduReceivedTimestampMs)
                            .LocalDateTime;

                App.Current.Dispatcher.Invoke(() =>
                {
                    VolumeVm.Update(new DateTimePoint(ts, dto.PdusInLastSixtySeconds));
                    ThroughputVm.Update(new DateTimePoint(ts, dto.AveragePduRatePerSecondLastSixtySeconds));
                    DataVolumeVm.AddDataPoint(DateTime.Now, Convert.ToInt32(dto.PdusInLastSixtySeconds /60));
                });

                var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var nowDisTs = RealTimeMetricsService.ToDisAbsoluteTimestamp(nowSecs);

                var newStates = await _metricsSvc.GetHistoricalEntityStatesAsync(_lastTimestamp, nowDisTs);
                var newFires  = await _metricsSvc.GetHistoricalFireEventsAsync(_lastTimestamp, nowDisTs);

                var entityCount = newStates.LongCount();
                var fireCount   = newFires.LongCount();
                EntityVsFireSummary = $"{entityCount} / {fireCount}";
                OnPropertyChanged(nameof(TotalPdusLastMinute));
                OnPropertyChanged(nameof(AveragePdusPerSecond));
                OnPropertyChanged(nameof(PeakPdusPerSecond));
                OnPropertyChanged(nameof(EntityVsFireSummary));

                App.Current.Dispatcher.Invoke(() =>
                {
                    ComparisonVm.UpdateCounts(entityCount, fireCount);
                    foreach (var s in newStates)
                        LogsVm.AddEntityState(
                            s.Timestamp,
                            s.Site, s.Application, s.Entity,
                            s.LocationX, s.LocationY, s.LocationZ);

                    foreach (var f in newFires)
                        LogsVm.AddFireEvent(
                            f.Timestamp,
                            f.FiringSite, f.FiringApplication, f.FiringEntity,
                            f.TargetSite, f.TargetApplication, f.TargetEntity,
                            f.MunitionSite, f.MunitionApplication, f.MunitionEntity);
                });

                // Advance bookmark
                var maxStateTs = newStates.Select(s => s.Timestamp).DefaultIfEmpty(_lastTimestamp).Max();
                var maxFireTs  = newFires .Select(f => f.Timestamp).DefaultIfEmpty(_lastTimestamp).Max();
                _lastTimestamp = Math.Max(_lastTimestamp, Math.Max(maxStateTs, maxFireTs));
            }
            catch
                {
                    // ignore
                }
        }
        private DateTime _selectedDate = DateTime.Now.Date;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate == value) return;
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                _ = LoadOnceAsync();
            }
        }

        private TimeSpan _selectedTime = DateTime.Now.TimeOfDay;
        public TimeSpan SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (_selectedTime == value) return;
                _selectedTime = value;
                OnPropertyChanged(nameof(SelectedTime));
                _ = LoadOnceAsync();
            }
        }
        private DateTime SelectedDateTime => SelectedDate + SelectedTime;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

}