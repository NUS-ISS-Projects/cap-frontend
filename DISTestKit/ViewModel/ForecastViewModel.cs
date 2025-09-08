using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
                RefreshHistorical();
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
                RefreshHistorical();
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
                RefreshHistorical();
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

        public ForecastViewModel(RealTimeMetricsService svc)
        {
            _svc = svc;

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
                },
                new LineSeries<DateTimePoint>
                {
                    Name = "AI Prediction",
                    Values = predictedVals,
                    Stroke = new SolidColorPaint(SKColor.Parse("#FF6B35"), 2)
                    {
                        PathEffect = new DashEffect(new float[] { 10f, 5f })
                    },
                    Fill = null,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#FF6B35")),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#FF6B35"), 1),
                    GeometrySize = 4,
                    LineSmoothness = 0.8,
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
            VolumeYAxes = new[] { new Axis { MinLimit = 0 } };

            // Set initial period to Today and load data after everything is initialized
            _selectedPeriod = Period.Today;
            RefreshHistorical();
        }

        private void RefreshHistorical()
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

            // TODO: Fetch and populate data from aggregation service
            // For now, add sample data points to demonstrate the chart
            AddSampleDataForPeriod();
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
                    var weekEndDate = DateTime.Today.AddDays(6); // Extend to show forecast
                    VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("MM/dd");
                    VolumeXAxes[0].MinLimit = weekStartDate.AddHours(-12).Ticks;
                    VolumeXAxes[0].MaxLimit = weekEndDate.AddHours(12).Ticks;
                    VolumeXAxes[0].UnitWidth = TimeSpan.FromDays(1).Ticks;
                    break;

                case Period.Month:
                    // Configure for weekly data over a month
                    var startOfMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
                    VolumeXAxes[0].Labeler = v => new DateTime((long)v).ToString("MM/dd");
                    VolumeXAxes[0].MinLimit = startOfMonth.AddDays(-3).Ticks;
                    VolumeXAxes[0].MaxLimit = endOfMonth.AddDays(3).Ticks;
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

        private void AddSampleDataForPeriod()
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

            switch (_selectedPeriod)
            {
                case Period.Today:
                    // Add historical hourly data (past hours until now)
                    var currentHour = now.Hour;
                    for (int hour = 0; hour <= currentHour; hour++)
                    {
                        var dateTime = _selectedDate.AddHours(hour);
                        var value = random.Next(100, 1000);
                        historicalValues.Add(new DateTimePoint(dateTime, value));
                    }

                    // Add predicted data (remaining hours of the day)
                    var lastHistoricalValue = historicalValues.LastOrDefault()?.Value ?? 500;
                    var lastHistoricalTime = historicalValues.LastOrDefault()?.DateTime ?? _selectedDate.AddHours(currentHour);
                    
                    // Add the connecting point (last historical point as first predicted point)
                    if (currentHour < 23)
                    {
                        predictedValues.Add(new DateTimePoint(lastHistoricalTime, lastHistoricalValue));
                    }
                    
                    for (int hour = currentHour + 1; hour < 24; hour++)
                    {
                        var dateTime = _selectedDate.AddHours(hour);
                        var variation = random.Next(-100, 100);
                        var value = Math.Max(50, lastHistoricalValue + variation);
                        predictedValues.Add(new DateTimePoint(dateTime, value));
                        lastHistoricalValue = value;
                    }
                    break;

                case Period.Week:
                    // Add historical daily data (past days up to today)
                    var startDate = _selectedDate.AddDays(-6);
                    
                    // Historical data: from 6 days ago up to and including today
                    for (int day = 0; day <= 6; day++)
                    {
                        var dateTime = startDate.AddDays(day);
                        // Only add historical data for dates up to today
                        if (dateTime.Date <= DateTime.Today)
                        {
                            var value = random.Next(2000, 8000);
                            historicalValues.Add(new DateTimePoint(dateTime, value));
                        }
                    }

                    // Add predicted data for future days (next 7 days after today)
                    var lastWeekValue = historicalValues.LastOrDefault()?.Value ?? 5000;
                    var lastWeekTime = historicalValues.LastOrDefault()?.DateTime ?? DateTime.Today;
                    
                    // Add the connecting point (last historical point as first predicted point)
                    predictedValues.Add(new DateTimePoint(lastWeekTime, lastWeekValue));
                    
                    // Add forecast for next 6 days
                    for (int day = 1; day <= 6; day++)
                    {
                        var dateTime = DateTime.Today.AddDays(day);
                        var variation = random.Next(-1000, 1000);
                        var value = Math.Max(1000, lastWeekValue + variation);
                        predictedValues.Add(new DateTimePoint(dateTime, value));
                        lastWeekValue = value;
                    }
                    break;

                case Period.Month:
                    // Add historical weekly data and predicted data
                    var startOfMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                    var weeksInMonth = Math.Ceiling(
                        (
                            DateTime.DaysInMonth(_selectedDate.Year, _selectedDate.Month)
                            + (int)startOfMonth.DayOfWeek
                        ) / 7.0
                    );

                    var currentWeek = Math.Min(
                        weeksInMonth - 1,
                        Math.Floor((_selectedDate.Date - startOfMonth.Date).Days / 7.0)
                    );

                    // Historical data
                    for (int week = 0; week <= currentWeek; week++)
                    {
                        var dateTime = startOfMonth.AddDays(week * 7);
                        var value = random.Next(10000, 40000);
                        historicalValues.Add(new DateTimePoint(dateTime, value));
                    }

                    // Predicted data
                    var lastMonthValue = historicalValues.LastOrDefault()?.Value ?? 25000;
                    var lastMonthTime = historicalValues.LastOrDefault()?.DateTime ?? startOfMonth.AddDays(currentWeek * 7);
                    
                    // Add the connecting point (last historical point as first predicted point)
                    if (currentWeek < weeksInMonth - 1)
                    {
                        predictedValues.Add(new DateTimePoint(lastMonthTime, lastMonthValue));
                    }
                    
                    for (int week = (int)currentWeek + 1; week < weeksInMonth; week++)
                    {
                        var dateTime = startOfMonth.AddDays(week * 7);
                        var variation = random.Next(-5000, 5000);
                        var value = Math.Max(5000, lastMonthValue + variation);
                        predictedValues.Add(new DateTimePoint(dateTime, value));
                        lastMonthValue = value;
                    }
                    break;
            }

            // Update Y-axis based on all data
            var allValues = historicalValues.Concat(predictedValues).ToList();
            if (allValues.Count > 0 && VolumeYAxes != null && VolumeYAxes.Length > 0)
            {
                var maxY = allValues.Max(v => v.Value ?? 0);
                var padding = maxY * 0.2;
                VolumeYAxes[0].MinLimit = 0;
                VolumeYAxes[0].MaxLimit = Math.Ceiling(maxY + padding);
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

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
