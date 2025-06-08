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
        public RetransmitChartViewModel RetransmitVm { get; }
        public LogViewModel LogsVm { get; }

        public ICommand PlayCommand { get; }
        public ICommand RefreshCommand { get; }

        private readonly RealTimeMetricsService _metricsSvc;
        private readonly Timer _timer;
        private bool _isPlaying;
        private long _lastTimestamp = 0; 

        public MainViewModel()
        {
            _metricsSvc = new RealTimeMetricsService("http://34.98.89.167/api/acquisition/");
            VolumeVm = new VolumeChartViewModel();
            ThroughputVm = new ThroughputChartViewModel();
            RetransmitVm = new RetransmitChartViewModel();
            LogsVm = new LogViewModel();

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

            var nowEpochSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nowDisTs    = RealTimeMetricsService.ToDisAbsoluteTimestamp(nowEpochSec);

            // Fetch historical entity-states
            var states = await _metricsSvc.GetHistoricalEntityStatesAsync(0, nowDisTs);
            foreach (var s in states)
            {
                LogsVm.AddEntityState(
                    s.Timestamp,
                    s.Site, s.Application, s.Entity,
                    s.LocationX, s.LocationY, s.LocationZ
                );
            }

            // Fetch historical fire-events
            var fires = await _metricsSvc.GetHistoricalFireEventsAsync(0, nowDisTs);
            foreach (var f in fires)
            LogsVm.AddFireEvent(
                f.Timestamp,
                f.FiringSite, f.FiringApplication, f.FiringEntity,
                f.TargetSite, f.TargetApplication, f.TargetEntity,
                f.MunitionSite, f.MunitionApplication, f.MunitionEntity);

            _lastTimestamp = states.Select(s => s.Timestamp)
                               .Concat(fires.Select(f => f.Timestamp))
                               .DefaultIfEmpty(0)
                               .Max();

            IsPlaying = was;
        }

        private async Task OnTickAsync()
    {
        try
        {
            // First update charts from /realtime
            var dto = await _metricsSvc.GetAsync();
            var ts  = DateTimeOffset
                        .FromUnixTimeMilliseconds(dto.LastPduReceivedTimestampMs)
                        .LocalDateTime;

            App.Current.Dispatcher.Invoke(() =>
            {
                VolumeVm.Update(new DateTimePoint(ts, dto.PdusInLastSixtySeconds));
                ThroughputVm.Update(new DateTimePoint(ts, dto.AveragePduRatePerSecondLastSixtySeconds));
            });

            var nowEpochSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nowDisTs    = RealTimeMetricsService.ToDisAbsoluteTimestamp(nowEpochSec);

            var newStates = await _metricsSvc.GetHistoricalEntityStatesAsync(_lastTimestamp, nowDisTs);
            var newFires  = await _metricsSvc.GetHistoricalFireEventsAsync    (_lastTimestamp, nowDisTs);

            App.Current.Dispatcher.Invoke(() =>
            {
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
            var maxTs = new[]
            {
                newStates.Select(s => s.Timestamp).DefaultIfEmpty(_lastTimestamp).Max(),
                newFires .Select(f => f.Timestamp).DefaultIfEmpty(_lastTimestamp).Max()
            }.Max();
            _lastTimestamp = Math.Max(_lastTimestamp, maxTs);
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