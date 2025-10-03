using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

        public ForecastViewModel(RealTimeMetricsService svc)
        {
            _svc = svc;
            _aggregationSvc = new AggregationService("http://34.142.158.178/api/");
            _predictionSvc = new PredictionService("http://34.142.158.178/api/");

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

            // build chart series
            var historicalVals = new ObservableCollection<DateTimePoint>();
            var predictedVals = new ObservableCollection<DateTimePoint>();
            VolumeSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Historical Data",
                    Values = historicalVals,
                    Stroke = new SolidColorPaint(SKColor.Parse("#5084DD"), 2),
                    Fill = null,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#5084DD")),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#5084DD"), 1),
                    GeometrySize = 4,
                    LineSmoothness = 0.8,
                    Mapping = static (point, index) =>
                        new LiveChartsCore.Kernel.Coordinate(
                            point.DateTime.Ticks,
                            point.Value ?? 0
                        ),
                },
                new LineSeries<DateTimePoint>
                {
                    Name = "AI Prediction",
                    Values = predictedVals,
                    Stroke = new SolidColorPaint(SKColor.Parse("#FF6B35"), 2)
                    {
                        PathEffect = new DashEffect(new float[] { 10f, 5f }),
                    },
                    Fill = null,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#FF6B35")),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#FF6B35"), 1),
                    GeometrySize = 4,
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

            // Clear existing data from both series
            if (VolumeSeries.Count > 0)
            {
                // Clear historical data
                if (
                    VolumeSeries[0] is LineSeries<DateTimePoint> historicalSeries
                    && historicalSeries.Values
                        is ObservableCollection<DateTimePoint> historicalValues
                )
                {
                    historicalValues.Clear();
                }

                // Clear prediction data
                if (
                    VolumeSeries.Count > 1
                    && VolumeSeries[1] is LineSeries<DateTimePoint> predictedSeries
                    && predictedSeries.Values is ObservableCollection<DateTimePoint> predictedValues
                )
                {
                    predictedValues.Clear();
                }
            }

            // Configure chart based on selected period
            ConfigureChartForPeriod();

            // Fetch actual data from API
            await LoadHistoricalDataFromAPI();

            // Add prediction data from API
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
                    // Configure for hourly data over 24 hours
                    VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("HH:mm");
                    VolumeXAxes[0].MinLimit = _selectedDate.Ticks;
                    VolumeXAxes[0].MaxLimit = _selectedDate.AddDays(1).Ticks;
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

        private async Task LoadHistoricalDataFromAPI()
        {
            if (VolumeSeries.Count < 1)
                return;

            var historicalSeries = VolumeSeries[0] as LineSeries<DateTimePoint>;
            if (
                historicalSeries?.Values is not ObservableCollection<DateTimePoint> historicalValues
            )
                return;

            try
            {
                DateTime? apiStartDate = null;

                if (_selectedPeriod == Period.Week)
                {
                    // For week view, backend expects the end day of the 7‑day window as startDate
                    // e.g. startDate=2025-09-21 returns buckets from 2025-09-15..2025-09-21
                    apiStartDate = _selectedDate.Date;
                }
                else if (_selectedPeriod == Period.Month)
                {
                    // For month view, use the first day of the selected month
                    apiStartDate = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                }

                var agg = await _aggregationSvc.GetAggregateAsync(
                    today: _selectedPeriod == Period.Today,
                    week: _selectedPeriod == Period.Week,
                    month: _selectedPeriod == Period.Month,
                    startDate: apiStartDate
                );

                var weekIndex = 0;
                foreach (var bucket in agg.Buckets)
                {
                    DateTime when = agg.TimeUnit switch
                    {
                        "hour" => _selectedDate.AddHours(bucket.Hour ?? 0),
                        "day" => DateTime.Parse(bucket.Date!),
                        "week" => GetWeekStartDate(bucket.Week, agg.Start, weekIndex),
                        _ => _selectedDate,
                    };

                    if (agg.TimeUnit == "week")
                        weekIndex++;

                    // Only add points with actual data (TotalPackets > 0) or if it's not a week view
                    // For week view, we want to show all data points to see the timeline properly
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        historicalValues.Add(new DateTimePoint(when, bucket.TotalPackets));
                    });
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    // Align X axis to API range and include forecast horizon when on week view
                    if (_selectedPeriod == Period.Week && VolumeXAxes?.Length > 0)
                    {
                        if (
                            DateTime.TryParse(agg.Start, out var aggStart)
                            && DateTime.TryParse(agg.End, out var aggEnd)
                        )
                        {
                            var forecastEnd = _selectedDate.AddDays(6);
                            VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("MM/dd");
                            VolumeXAxes[0].MinLimit = aggStart.AddHours(-12).Ticks;
                            VolumeXAxes[0].MaxLimit = forecastEnd.AddHours(12).Ticks;
                            VolumeXAxes[0].UnitWidth = TimeSpan.FromDays(1).Ticks;
                        }
                    }

                    UpdateYAxisBasedOnData();
                });
            }
            catch { }
        }

        private static DateTime GetWeekStartDate(string? weekString, string apiStart, int weekIndex)
        {
            // Parse week string format: "Week 1 (2025-09-21 to 2025-09-27)"
            if (!string.IsNullOrEmpty(weekString))
            {
                var openParen = weekString.IndexOf('(');
                var toIndex = weekString.IndexOf(" to ");

                if (openParen > 0 && toIndex > openParen)
                {
                    var startDateStr = weekString.Substring(openParen + 1, toIndex - openParen - 1);
                    if (DateTime.TryParse(startDateStr, out DateTime weekStart))
                        return weekStart;
                }
            }

            // Fallback to calculating from API start date
            if (DateTime.TryParse(apiStart, out DateTime apiStartDate))
                return apiStartDate.AddDays(weekIndex * 7);

            return DateTime.Today.AddDays(weekIndex * 7);
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
            if (VolumeSeries.Count < 2)
                return;

            var historicalSeries = VolumeSeries[0] as LineSeries<DateTimePoint>;
            var predictedSeries = VolumeSeries[1] as LineSeries<DateTimePoint>;

            if (
                historicalSeries?.Values is not ObservableCollection<DateTimePoint> historicalValues
                || predictedSeries?.Values
                    is not ObservableCollection<DateTimePoint> predictedValues
            )
                return;

            try
            {
                // Determine the timeUnit and startDate based on selected period
                string timeUnit;
                string startDate;

                switch (_selectedPeriod)
                {
                    case Period.Today:
                        timeUnit = "hour";
                        startDate = _selectedDate.ToString("yyyy-MM-dd");
                        break;
                    case Period.Week:
                        timeUnit = "day";
                        startDate = _selectedDate.ToString("yyyy-MM-dd");
                        break;
                    case Period.Month:
                        timeUnit = "week";
                        startDate = new DateTime(_selectedDate.Year, _selectedDate.Month, 1)
                            .ToString("yyyy-MM-dd");
                        break;
                    case Period.Year:
                        timeUnit = "month";
                        startDate = new DateTime(_selectedDate.Year, 1, 1).ToString("yyyy-MM-dd");
                        break;
                    default:
                        return;
                }

                // Call the prediction API
                var prediction = await _predictionSvc.GetPredictionAsync(timeUnit, startDate);

                // Add connecting point from last historical data
                var lastHistoricalValue = historicalValues.LastOrDefault()?.Value ?? 0;
                var lastHistoricalTime = historicalValues.LastOrDefault()?.DateTime ?? _selectedDate;

                if (historicalValues.Count > 0 && lastHistoricalValue > 0)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        predictedValues.Add(
                            new DateTimePoint(lastHistoricalTime, lastHistoricalValue)
                        );
                    });
                }

                // Parse and add prediction data
                for (int i = 0; i < prediction.predicted_labels.Count; i++)
                {
                    DateTime predictionTime;
                    double predictionValue = prediction.predicted_values[i];

                    // Parse the label based on time unit
                    if (timeUnit == "hour")
                    {
                        // Label format: "HH:mm"
                        if (TimeSpan.TryParse(prediction.predicted_labels[i], out var timeOfDay))
                        {
                            predictionTime = _selectedDate.Date + timeOfDay;
                        }
                        else
                        {
                            continue; // Skip invalid labels
                        }
                    }
                    else if (timeUnit == "day" || timeUnit == "week")
                    {
                        // Label format: "yyyy-MM-dd"
                        if (
                            DateTime.TryParse(
                                prediction.predicted_labels[i],
                                out var parsedDate
                            )
                        )
                        {
                            predictionTime = parsedDate;
                        }
                        else
                        {
                            continue; // Skip invalid labels
                        }
                    }
                    else if (timeUnit == "month")
                    {
                        // Label format: could be "yyyy-MM" or full date
                        if (
                            DateTime.TryParse(
                                prediction.predicted_labels[i],
                                out var parsedDate
                            )
                        )
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
            if (VolumeSeries.Count < 2 || VolumeYAxes == null || VolumeYAxes.Length == 0)
                return;

            var historicalSeries = VolumeSeries[0] as LineSeries<DateTimePoint>;
            var predictedSeries = VolumeSeries[1] as LineSeries<DateTimePoint>;

            var historicalValues =
                historicalSeries?.Values as ObservableCollection<DateTimePoint>
                ?? new ObservableCollection<DateTimePoint>();
            var predictedValues =
                predictedSeries?.Values as ObservableCollection<DateTimePoint>
                ?? new ObservableCollection<DateTimePoint>();

            var allValues = historicalValues.Concat(predictedValues).ToList();
            if (allValues.Count > 0)
            {
                var maxY = allValues.Max(v => v.Value ?? 0);
                var padding = maxY * 0.2;

                // Ensure minimum scale even when all values are zero or very small
                if (maxY == 0)
                {
                    maxY = 1000; // Default max for empty data
                    padding = 200;
                }
                else if (padding < 100)
                {
                    padding = 100;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
