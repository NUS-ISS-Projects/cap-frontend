using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DISTestKit.Model;
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

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }

    public enum Period
    {
        None,
        Today,
        Week,
        Month,
        Year,
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
                OnPropertyChanged(nameof(IsDatePickerEnabled));
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
                    VolumeVm.Clear(); // Clear aggregated data before switching to real-time
                    VolumeVm.ConfigureForRealTime();
                }
            }
        }
        public bool IsPaused => !_isPlaying;

        // ––– Date/Time Selection ––––––––––––––––––––––––––––––––––––––––

        private DateTime _selectedDate = DateTime.Now.Date;
        private bool _isLoadingFromCache = false;

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
                {
                    _ = LoadOnceAsync();
                    // Save session when date is selected by user (not when loading from cache)
                    if (SelectedPeriod != Period.None && !_isLoadingFromCache)
                    {
                        string view = SelectedPeriod switch
                        {
                            Period.Today => "day",
                            Period.Week => "week",
                            Period.Month => "month",
                            _ => "dashboard",
                        };
                        _ = SaveUserSessionAsync(view);
                    }
                }
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
                OnPropertyChanged(nameof(IsDatePickerEnabled));
                if (value != Period.None)
                {
                    _isPlaying = false; // Set directly to avoid triggering LoadOnceAsync twice
                    OnPropertyChanged(nameof(IsPlaying));
                    OnPropertyChanged(nameof(IsPaused));
                    _timer.Enabled = false;
                }
            }
        }

        public bool IsDatePickerEnabled => SelectedPeriod != Period.None;
        public ICommand TodayCommand { get; }
        public ICommand WeekCommand { get; }
        public ICommand MonthCommand { get; }

        // ––– Infrastructure –––––––––––––––––––––––––––––––––––––––––––––
        private readonly string baseURL;
        private readonly RealTimeLogsService _realTimeLogsSvc;
        private readonly RealTimeMetricsService _realTimeMetricsSvc;
        private readonly AggregationService _aggregationSvc;
        private readonly UserService _userService;
        private readonly Timer _timer;
        private long _lastTimestamp;
        private int? _previousTotalPackets;
        private string _userId = "";
        private string _userName = "";
        private string _name = "";

        public MainViewModel()
        {
            baseURL = "http://34.142.158.178/api/";
            _realTimeMetricsSvc = new RealTimeMetricsService(baseURL);
            _realTimeLogsSvc = new RealTimeLogsService(baseURL);
            _aggregationSvc = new AggregationService(baseURL);
            _userService = new UserService(baseURL);
            VolumeVm = new VolumeChartViewModel();
            ThroughputVm = new ThroughputChartViewModel();
            ComparisonVm = new PduTypeComparisonViewModel();
            LogsVm = new LogViewModel();
            DataVolumeVm = new DataVolumeChartViewModel();
            TodayCommand = new RelayCommand(() =>
            {
                SelectedPeriod = Period.Today;
                IsPlaying = false; // Ensure we're in paused mode for aggregated data
                VolumeVm.Clear(); // Clear data first
                LoadDateFromCache();
                _ = LoadOnceAsync();
            });
            WeekCommand = new RelayCommand(() =>
            {
                SelectedPeriod = Period.Week;
                LoadDateFromCache();
                _ = LoadOnceAsync();
            });
            MonthCommand = new RelayCommand(() =>
            {
                SelectedPeriod = Period.Month;
                LoadDateFromCache();
                _ = LoadOnceAsync();
            });

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

            // Load user session data
            _ = LoadUserSessionAsync();
        }

        private async Task LoadUserSessionAsync()
        {
            try
            {
                // Always call API when dashboard first loads
                var profile = await _userService.GetUserProfileAsync();
                if (profile != null)
                {
                    _userId = profile.UserId;
                    _userName = profile.Email;
                    // Cache the profile for other pages
                    UserSessionCache.SetUserProfile(profile);
                }

                var session = await _userService.GetUserSessionAsync();
                if (session != null)
                {
                    _name = session.Name;
                    // Cache the session for other pages
                    UserSessionCache.SetUserSession(session);

                    // Update the MainWindow's UserName display
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = App.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            mainWindow.UserName = _name;
                        }
                    });
                }
            }
            catch
            {
                // Failed to load user session, continue with default behavior
            }
        }

        private void LoadDateFromCache()
        {
            try
            {
                // Load the last selected date from cache (no API call)
                var session = UserSessionCache.GetUserSession();
                if (session != null && DateTime.TryParse(session.LastSession.Date, out var lastDate))
                {
                    _isLoadingFromCache = true;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedDate = lastDate;
                    });
                    _isLoadingFromCache = false;
                }
            }
            catch
            {
                // If cache loading fails, keep current date
            }
        }

        private async Task SaveUserSessionAsync(string view)
        {
            try
            {
                if (string.IsNullOrEmpty(_userId))
                    return;

                var lastSession = new LastSession(
                    Date: SelectedDate.ToString("o"),
                    View: view
                );

                var request = new SaveUserSessionRequest(
                    UserId: _userId,
                    UserName: _userName,
                    Name: _name,
                    LastSession: lastSession
                );

                var success = await _userService.SaveUserSessionAsync(request);

                // Update cache after successful save
                if (success)
                {
                    var updatedSession = new UserSession(
                        UserId: _userId,
                        UserName: _userName,
                        Name: _name,
                        LastSession: lastSession
                    );
                    UserSessionCache.SetUserSession(updatedSession);
                }
            }
            catch
            {
                // Failed to save user session, continue silently
            }
        }

        public async Task LoadOnceAsync()
        {
            // Only clear charts when loading aggregated data (Today/Week/Month), not for real-time play
            if (SelectedPeriod != Period.None)
            {
                VolumeVm.Clear();
                LogsVm.Reset();
            }

            if (IsPaused)
            {
                var agg = await _aggregationSvc.GetAggregateAsync(
                    today: SelectedPeriod == Period.Today,
                    week: SelectedPeriod == Period.Week,
                    month: SelectedPeriod == Period.Month,
                    date: SelectedPeriod == Period.None && !SelectedTime.HasValue
                        ? SelectedDate
                        : (DateTime?)null,
                    startDate: SelectedTime.HasValue ? SelectedDate
                        : (SelectedPeriod == Period.Week || SelectedPeriod == Period.Month)
                            ? SelectedDate
                        : null,
                    endDate: SelectedTime.HasValue ? SelectedDate : null
                );

                // Add all data points first
                var weekIndex = 0;
                foreach (var b in agg.Buckets)
                {
                    DateTime when = agg.TimeUnit switch
                    {
                        "hour" => SelectedDate.AddHours(b.Hour ?? 0),
                        "day" => DateTime.Parse(b.Date!),
                        "week" => DateTime.Parse(agg.Start).AddDays(weekIndex * 7),
                        _ => SelectedDate,
                    };

                    if (agg.TimeUnit == "week")
                        weekIndex++;

                    VolumeVm.AddAggregatedDataPoint(new DateTimePoint(when, b.TotalPackets));
                }

                // Then configure chart dynamically based on actual data
                VolumeVm.FinalizeAggregatedData(agg.TimeUnit);
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
