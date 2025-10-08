using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DISTestKit.Model;
using DISTestKit.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;

namespace DISTestKit.ViewModel
{
    public class ChatMessage
    {
        public string Message { get; set; } = "";
        public bool IsFromUser { get; set; }
        public Brush Background =>
            IsFromUser
                ? new SolidColorBrush(Color.FromRgb(80, 132, 221))
                : new SolidColorBrush(Color.FromRgb(240, 240, 240));
        public Brush Foreground => IsFromUser ? Brushes.White : Brushes.Black;
        public HorizontalAlignment Alignment =>
            IsFromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public class ForecastViewModel : PageViewModel, INotifyPropertyChanged
    {
        // volume chart
        public ObservableCollection<ISeries> VolumeSeries { get; }
        public Axis[] VolumeXAxes { get; }
        public Axis[] VolumeYAxes { get; }

        // top metrics
        public double TotalVolumeLastMinute { get; private set; }
        public double AverageVolumePerSecond { get; private set; }

        // Error/warning display
        private bool _showInsufficientDataWarning = false;
        public bool ShowInsufficientDataWarning
        {
            get => _showInsufficientDataWarning;
            set
            {
                _showInsufficientDataWarning = value;
                OnPropertyChanged(nameof(ShowInsufficientDataWarning));
            }
        }

        private string _insufficientDataMessage = "";
        public string InsufficientDataMessage
        {
            get => _insufficientDataMessage;
            set
            {
                _insufficientDataMessage = value;
                OnPropertyChanged(nameof(InsufficientDataMessage));
            }
        }

        // date/time selection
        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                _ = RefreshHistoricalAsync();
                _ = SaveUserSessionAsync();
            }
        }

        private TimeSpan? _selectedTime;
        public TimeSpan? SelectedTime
        {
            get => _selectedTime;
            set
            {
                _selectedTime = value;
                OnPropertyChanged(nameof(SelectedTime));
                _ = RefreshHistoricalAsync();
            }
        }

        // Period selection
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
                _ = RefreshHistoricalAsync();
            }
        }

        public ICommand TodayCommand { get; }
        public ICommand WeekCommand { get; }
        public ICommand MonthCommand { get; }
        public ICommand YearCommand { get; }

        // Chat functionality
        public ObservableCollection<ChatMessage> ChatMessages { get; }
        public ObservableCollection<string> TimePeriods { get; }
        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }

        private string _chatInputText = "";
        public string ChatInputText
        {
            get => _chatInputText;
            set
            {
                _chatInputText = value;
                OnPropertyChanged(nameof(ChatInputText));
                OnPropertyChanged(nameof(CanSendMessage));
            }
        }

        private string _selectedTimePeriod = "24 hours";
        public string SelectedTimePeriod
        {
            get => _selectedTimePeriod;
            set
            {
                _selectedTimePeriod = value;
                OnPropertyChanged(nameof(SelectedTimePeriod));
                UpdateChatInputPreview();
            }
        }

        public bool CanSendMessage => !string.IsNullOrWhiteSpace(ChatInputText);

        private readonly RealTimeMetricsService _svc;
        private readonly AggregationService _aggregationSvc;
        private readonly PredictionService _predictionSvc;
        private readonly UserService _userService;
        private string _userId = "";
        private string _userName = "";
        private string _name = "";

        public ForecastViewModel(RealTimeMetricsService svc)
        {
            _svc = svc;
            _aggregationSvc = new AggregationService("http://34.142.158.178/api/");
            _predictionSvc = new PredictionService("http://34.142.158.178/api/");
            _userService = new UserService("http://34.142.158.178/api/");

            // Load user profile data
            _ = LoadUserProfileAsync();

            // Initialize chat functionality
            ChatMessages = new ObservableCollection<ChatMessage>();
            TimePeriods = new ObservableCollection<string> { "24 hours", "week", "month" };
            SendMessageCommand = new RelayCommand(SendMessage);
            ClearChatCommand = new RelayCommand(ClearChat);

            // Initialize commands
            TodayCommand = new RelayCommand(() => SelectedPeriod = Period.Today);
            WeekCommand = new RelayCommand(() => SelectedPeriod = Period.Week);
            MonthCommand = new RelayCommand(() => SelectedPeriod = Period.Month);
            YearCommand = new RelayCommand(() => SelectedPeriod = Period.Year);

            // Set initial chat input preview
            UpdateChatInputPreview();

            // Ensure SelectedDate is set to today's date
            _selectedDate = DateTime.Today;

            // build chart series - only show predictions
            var predictedVals = new ObservableCollection<DateTimePoint>();
            VolumeSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "AI Prediction",
                    Values = predictedVals,
                    Stroke = new SolidColorPaint(SKColor.Parse("#5084DD"), 3)
                    {
                        PathEffect = new DashEffect(new float[] { 10f, 5f }),
                    },
                    Fill = null,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#5084DD")),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#5084DD"), 1),
                    GeometrySize = 6,
                    LineSmoothness = 0.8,
                    Mapping = static (point, index) =>
                        new LiveChartsCore.Kernel.Coordinate(
                            point.DateTime.Ticks,
                            point.Value ?? 0
                        ),
                },
            };
            var now = DateTime.Now;
            VolumeXAxes = new[]
            {
                new Axis
                {
                    Labeler = v => new DateTime((long)v).ToString("HH:mm:ss"),
                    MinLimit = now.AddMinutes(-1).Ticks,
                    MaxLimit = now.Ticks,
                },
            };
            VolumeYAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 1000,
                    Labeler = value => ((int)value).ToString(),
                    UnitWidth = 200,
                },
            };

            // Set initial period to Today and load data after everything is initialized
            _selectedPeriod = Period.Today;
            _ = RefreshHistoricalAsync();
        }

        private async Task RefreshHistoricalAsync()
        {
            if (_selectedPeriod == Period.None)
                return;

            // Clear existing prediction data
            if (VolumeSeries.Count > 0)
            {
                if (
                    VolumeSeries[0] is LineSeries<DateTimePoint> predictedSeries
                    && predictedSeries.Values is ObservableCollection<DateTimePoint> predictedValues
                )
                {
                    predictedValues.Clear();
                }
            }

            // Configure chart based on selected period
            ConfigureChartForPeriod();

            // Load only prediction data from API
            await LoadPredictionDataFromAPI();
        }

        private void ConfigureChartForPeriod()
        {
            if (VolumeXAxes == null || VolumeXAxes.Length == 0)
                return;

            var now = DateTime.Now;

            switch (_selectedPeriod)
            {
                case Period.Today:
                    // Configure for hourly data over 24 hours + forecast
                    VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("HH:mm");
                    VolumeXAxes[0].MinLimit = _selectedDate.Ticks;
                    VolumeXAxes[0].MaxLimit = _selectedDate.AddDays(2).Ticks; // Extended to show next day forecast
                    VolumeXAxes[0].UnitWidth = TimeSpan.FromHours(1).Ticks;
                    break;

                case Period.Week:
                    // Configure for daily data: 7 days historical + 7 days forecast
                    var weekStartDate = _selectedDate.AddDays(-6);
                    var weekEndDate = _selectedDate.AddDays(6); // Extend to show forecast
                    VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("MM/dd");
                    VolumeXAxes[0].MinLimit = weekStartDate.Ticks;
                    VolumeXAxes[0].MaxLimit = weekEndDate.AddDays(1).Ticks;
                    VolumeXAxes[0].UnitWidth = TimeSpan.FromDays(1).Ticks;
                    break;

                case Period.Month:
                    // Configure for weekly data over a month
                    var startOfMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
                    // Extend chart to show forecast data beyond current month
                    var chartEndDate = endOfMonth.AddMonths(1);

                    // For month view, we'll use week numbers as labels
                    VolumeXAxes[0].Labeler = v =>
                    {
                        var date = new DateTime((long)v);
                        var weekStart = GetWeekStart(startOfMonth);
                        var weekNumber = ((date - weekStart).Days / 7) + 1;
                        return $"Week {weekNumber}";
                    };
                    VolumeXAxes[0].MinLimit = startOfMonth.AddDays(-3).Ticks;
                    VolumeXAxes[0].MaxLimit = chartEndDate.Ticks;
                    VolumeXAxes[0].UnitWidth = TimeSpan.FromDays(7).Ticks;
                    break;

                case Period.Year:
                    // Configure for monthly data over a year
                    var startOfYear = new DateTime(_selectedDate.Year, 1, 1);
                    var endOfYear = new DateTime(_selectedDate.Year, 12, 31);
                    VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("MMM");
                    VolumeXAxes[0].MinLimit = startOfYear.AddDays(-15).Ticks;
                    VolumeXAxes[0].MaxLimit = endOfYear.AddDays(15).Ticks;
                    VolumeXAxes[0].UnitWidth = TimeSpan.FromDays(30).Ticks;
                    break;
            }
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            // Get the start of the week (Monday)
            var dayOfWeek = (int)date.DayOfWeek;
            var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1; // Handle Sunday as 7th day
            return date.Date.AddDays(-daysToSubtract);
        }

        private async Task LoadPredictionDataFromAPI()
        {
            if (VolumeSeries.Count < 1)
                return;

            var predictedSeries = VolumeSeries[0] as LineSeries<DateTimePoint>;

            if (predictedSeries?.Values is not ObservableCollection<DateTimePoint> predictedValues)
                return;

            // Reset warning state
            ShowInsufficientDataWarning = false;

            try
            {
                // Determine the timeUnit and startDate based on selected period
                string timeUnit;
                string startDate;

                switch (_selectedPeriod)
                {
                    case Period.Today:
                        timeUnit = "day";
                        startDate = _selectedDate.ToString("yyyy-MM-dd");
                        break;
                    case Period.Week:
                        timeUnit = "week";
                        startDate = _selectedDate.ToString("yyyy-MM-dd");
                        break;
                    case Period.Month:
                        timeUnit = "month";
                        startDate = new DateTime(
                            _selectedDate.Year,
                            _selectedDate.Month,
                            1
                        ).ToString("yyyy-MM-dd");
                        break;
                    default:
                        return;
                }

                // Call the prediction API
                var prediction = await _predictionSvc.GetPredictionAsync(timeUnit, startDate);

                // Check for errors or insufficient data
                if (prediction.HasError)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ShowInsufficientDataWarning = true;
                        InsufficientDataMessage =
                            prediction.details ?? "Unable to generate predictions";
                    });
                    return;
                }

                if (prediction.IsInsufficientData)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ShowInsufficientDataWarning = true;
                        InsufficientDataMessage =
                            "Not enough data available to generate forecast for this period";
                    });
                    return;
                }

                // Ensure we have valid data
                if (prediction.predicted_labels == null || prediction.predicted_values == null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ShowInsufficientDataWarning = true;
                        InsufficientDataMessage = "Invalid prediction data received";
                    });
                    return;
                }

                // Parse and add prediction data
                DateTime baseDate = _selectedDate.Date;
                TimeSpan? previousTime = null;

                for (int i = 0; i < prediction.predicted_labels.Count; i++)
                {
                    DateTime predictionTime;
                    double predictionValue = prediction.predicted_values[i];

                    // Parse the label based on time unit
                    if (timeUnit == "day")
                    {
                        // For day timeUnit, labels are "HH:mm" format
                        if (TimeSpan.TryParse(prediction.predicted_labels[i], out var timeOfDay))
                        {
                            // Start predictions from the next day
                            if (i == 0)
                            {
                                baseDate = _selectedDate.Date.AddDays(1);
                            }

                            // Check if time wrapped around (e.g., from 23:00 to 00:00)
                            if (previousTime.HasValue && timeOfDay < previousTime.Value)
                            {
                                baseDate = baseDate.AddDays(1);
                            }
                            previousTime = timeOfDay;

                            predictionTime = baseDate + timeOfDay;
                        }
                        else
                        {
                            continue; // Skip invalid labels
                        }
                    }
                    else if (timeUnit == "week" || timeUnit == "month")
                    {
                        // Label format: "yyyy-MM-dd" or full date
                        if (DateTime.TryParse(prediction.predicted_labels[i], out var parsedDate))
                        {
                            predictionTime = parsedDate;
                        }
                        else
                        {
                            continue; // Skip invalid labels
                        }
                    }
                    else
                    {
                        continue;
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        predictedValues.Add(new DateTimePoint(predictionTime, predictionValue));
                    });
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateYAxisBasedOnData();
                });
            }
            catch (Exception ex)
            {
                // Log error or handle gracefully - for now, silently fail
                System.Diagnostics.Debug.WriteLine($"Prediction API error: {ex.Message}");
            }
        }

        private void UpdateYAxisBasedOnData()
        {
            if (VolumeSeries.Count < 1 || VolumeYAxes == null || VolumeYAxes.Length == 0)
                return;

            var predictedSeries = VolumeSeries[0] as LineSeries<DateTimePoint>;

            var predictedValues =
                predictedSeries?.Values as ObservableCollection<DateTimePoint>
                ?? new ObservableCollection<DateTimePoint>();

            if (predictedValues.Count > 0)
            {
                var maxY = predictedValues.Max(v => v.Value ?? 0);
                var padding = maxY * 0.2;

                // Ensure minimum scale even when all values are zero or very small
                if (maxY == 0)
                {
                    maxY = 100; // Default max for empty data
                    padding = 20;
                }
                else if (padding < 10)
                {
                    padding = 10;
                }

                VolumeYAxes[0].MinLimit = 0;
                VolumeYAxes[0].MaxLimit = Math.Ceiling(maxY + padding);

                var stepSize = Math.Ceiling((maxY + padding) / 5);
                if (stepSize > 0)
                {
                    VolumeYAxes[0].UnitWidth = stepSize;
                }
            }
        }

        private void UpdateChatInputPreview()
        {
            ChatInputText = $"Analyze for me the data for the past {SelectedTimePeriod}";
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(ChatInputText))
                return;

            // Add user message
            var userMessage = new ChatMessage { Message = ChatInputText, IsFromUser = true };

            ChatMessages.Add(userMessage);

            // Clear input and save the message
            var sentMessage = ChatInputText;
            ChatInputText = "";

            // Get AI response from the API
            var aiResponse = await GetAIResponseFromAPI(sentMessage);
            var aiMessage = new ChatMessage { Message = aiResponse, IsFromUser = false };

            ChatMessages.Add(aiMessage);

            // Reset input to preview text
            UpdateChatInputPreview();
        }

        private void ClearChat()
        {
            ChatMessages.Clear();
        }

        private string _sessionId = Guid.NewGuid().ToString();

        private async Task<string> GetAIResponseFromAPI(string question)
        {
            try
            {
                var response = await _predictionSvc.GetChatResponseAsync(question, _sessionId);
                return response.answer;
            }
            catch (Exception ex)
            {
                // Fallback message if API call fails
                return $"I apologize, but I'm having trouble connecting to the analysis service right now. Please try again later. (Error: {ex.Message})";
            }
        }

        private async Task LoadUserProfileAsync()
        {
            try
            {
                var profile = await _userService.GetUserProfileAsync();
                if (profile != null)
                {
                    _userId = profile.UserId;
                    _userName = profile.Email;
                }

                var session = await _userService.GetUserSessionAsync();
                if (session != null)
                {
                    _name = string.IsNullOrEmpty(session.Name) ? "User" : session.Name;
                }
            }
            catch
            {
                // Failed to load user session, continue with default behavior
            }
        }

        private async Task SaveUserSessionAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_userId))
                    return;

                var lastSession = new LastSession(
                    Date: SelectedDate.ToString("o"),
                    View: "forecast"
                );

                var request = new SaveUserSessionRequest(
                    UserId: _userId,
                    UserName: _userName,
                    Name: _name,
                    LastSession: lastSession
                );

                await _userService.SaveUserSessionAsync(request);
            }
            catch
            {
                // Failed to save user session, continue silently
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
