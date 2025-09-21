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

        public ForecastViewModel(RealTimeMetricsService svc)
        {
            _svc = svc;
            _aggregationSvc = new AggregationService("http://localhost:32080/api/");

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

            // Add prediction data (still using sample data for now)
            AddPredictionData();
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

        private void AddPredictionData()
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

            var random = new Random();
            var now = DateTime.Now;

            // Move any future points from historical to prediction series
            var futurePoints = historicalValues.Where(p => p.DateTime > now).ToList();
            foreach (var futurePoint in futurePoints)
            {
                historicalValues.Remove(futurePoint);
                predictedValues.Add(new DateTimePoint(futurePoint.DateTime, futurePoint.Value));
            }

            switch (_selectedPeriod)
            {
                case Period.Today:
                    var lastHistoricalValue = historicalValues.LastOrDefault()?.Value ?? 500;
                    var lastHistoricalTime = historicalValues.LastOrDefault()?.DateTime ?? now;

                    if (now.Hour < 23)
                    {
                        predictedValues.Add(
                            new DateTimePoint(lastHistoricalTime, lastHistoricalValue)
                        );
                    }

                    for (int hour = now.Hour + 1; hour < 24; hour++)
                    {
                        var dateTime = _selectedDate.AddHours(hour);
                        var variation = random.Next(-100, 100);
                        var value = Math.Max(50, lastHistoricalValue + variation);
                        predictedValues.Add(new DateTimePoint(dateTime, value));
                        lastHistoricalValue = value;
                    }
                    break;

                case Period.Week:
                    // Find the last non-zero value or use the last value if all are zero
                    var lastNonZeroPoint = historicalValues.LastOrDefault(h => h.Value > 0);
                    var lastWeekValue =
                        lastNonZeroPoint?.Value ?? historicalValues.LastOrDefault()?.Value ?? 500;
                    var lastWeekTime = historicalValues.LastOrDefault()?.DateTime ?? _selectedDate;

                    // Add connecting point to ensure smooth transition, but only if we have actual data
                    if (historicalValues.Count > 0 && lastWeekValue > 0)
                    {
                        predictedValues.Add(new DateTimePoint(lastWeekTime, lastWeekValue));
                    }

                    // Generate predictions for future days only if we have meaningful historical data
                    if (lastWeekValue > 0)
                    {
                        var startPredictionDate =
                            DateTime.Today > lastWeekTime.Date
                                ? DateTime.Today.AddDays(1)
                                : lastWeekTime.Date.AddDays(1);
                        var endPredictionDate = _selectedDate.AddDays(6);

                        // Create smoother variations for daily predictions
                        var dailyVariation = Math.Max(100, (double)lastWeekValue * 0.1); // 10% variation max

                        for (
                            var date = startPredictionDate;
                            date <= endPredictionDate;
                            date = date.AddDays(1)
                        )
                        {
                            var variation = (random.NextDouble() - 0.5) * dailyVariation * 2;
                            var value = Math.Max(lastWeekValue * 0.1, lastWeekValue + variation);
                            predictedValues.Add(new DateTimePoint(date, value));
                            lastWeekValue = value;
                        }
                    }
                    break;

                case Period.Month:
                    var lastMonthValue = historicalValues.LastOrDefault()?.Value ?? 25000;
                    var lastMonthTime = historicalValues.LastOrDefault()?.DateTime ?? _selectedDate;

                    var startOfMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                    // Only add predictions if we have actual data
                    if (historicalValues.Count > 0 && lastMonthValue > 0)
                    {
                        // Determine the first week we want to predict.
                        // Start after the CURRENT week-of-month to avoid drawing an orange point on Week 4.
                        var nextWeekStart = GetWeekStart(lastMonthTime).AddDays(7); // week after last data
                        var currentWeekStart = GetWeekStart(_selectedDate);
                        var firstPredictionWeek =
                            nextWeekStart <= currentWeekStart
                                ? currentWeekStart.AddDays(7) // skip current week entirely
                                : nextWeekStart;

                        var predictionEndDate = endOfMonth.AddMonths(1);

                        // Create smoother variations for weekly predictions
                        var weeklyVariation = Math.Max(500, (double)lastMonthValue * 0.15); // 15% variation max

                        // add a connecting point at the last actual week (overlaps blue)
                        predictedValues.Add(new DateTimePoint(lastMonthTime, lastMonthValue));
                        // seed the first predicted week at the same value as last actual, but at Week+1
                        predictedValues.Add(new DateTimePoint(firstPredictionWeek, lastMonthValue));

                        for (
                            var weekStart = firstPredictionWeek.AddDays(7);
                            weekStart <= predictionEndDate;
                            weekStart = weekStart.AddDays(7)
                        )
                        {
                            var variation = (random.NextDouble() - 0.5) * weeklyVariation * 2;
                            var value = Math.Max(lastMonthValue * 0.1, lastMonthValue + variation);
                            predictedValues.Add(new DateTimePoint(weekStart, value));
                            lastMonthValue = value;
                        }
                    }
                    break;
            }

            UpdateYAxisBasedOnData();
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

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(ChatInputText))
                return;

            // Add user message
            var userMessage = new ChatMessage { Message = ChatInputText, IsFromUser = true };

            ChatMessages.Add(userMessage);

            // Clear input
            var sentMessage = ChatInputText;
            ChatInputText = "";

            // Simulate AI response (you can replace this with actual AI integration)
            var aiResponse = GenerateAIResponse(sentMessage);
            var aiMessage = new ChatMessage { Message = aiResponse, IsFromUser = false };

            ChatMessages.Add(aiMessage);

            // Reset input to preview text
            UpdateChatInputPreview();
        }

        private void ClearChat()
        {
            ChatMessages.Clear();
        }

        private string GenerateAIResponse(string userMessage)
        {
            // This is a placeholder for actual AI integration
            // You can replace this with calls to your AI service

            if (userMessage.ToLower().Contains("24 hours"))
            {
                return "Based on the data from the past 24 hours, I can see patterns in your DIS network traffic. The volume shows peak activity during business hours with an average of X messages per second. Would you like me to analyze specific aspects like PDU types or network performance?";
            }
            else if (userMessage.ToLower().Contains("week"))
            {
                return "Analyzing the weekly data shows interesting trends. There's typically higher activity on weekdays compared to weekends. The data suggests optimal network performance with occasional spikes during training exercises. Would you like a detailed breakdown of the patterns?";
            }
            else if (userMessage.ToLower().Contains("month"))
            {
                return "The monthly analysis reveals long-term trends in your DIS system usage. I notice cyclical patterns that align with training schedules and system maintenance windows. Overall network health appears stable with predictable load patterns.";
            }
            else
            {
                return "I understand you'd like analysis of your DIS data. I can help analyze network performance, identify patterns, predict traffic spikes, and provide insights on system optimization. What specific aspect would you like me to focus on?";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
