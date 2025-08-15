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

        // Chat functionality
        public ObservableCollection<ChatMessage> ChatMessages { get; }
        public ObservableCollection<string> TimePeriods { get; }
        public ICommand SendMessageCommand { get; }

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

            // Initialize commands
            TodayCommand = new RelayCommand(() => SelectedPeriod = Period.Today);
            WeekCommand = new RelayCommand(() => SelectedPeriod = Period.Week);
            MonthCommand = new RelayCommand(() => SelectedPeriod = Period.Month);

            // Set initial chat input preview
            UpdateChatInputPreview();

            // Ensure SelectedDate is set to today's date
            _selectedDate = DateTime.Today;

            // build chart series
            var vals = new ObservableCollection<DateTimePoint>();
            VolumeSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "Volume",
                    Values = vals,
                    Stroke = new SolidColorPaint(SKColors.MediumPurple, 2),
                    Fill = new SolidColorPaint(new SKColor(128, 0, 128, 80)),
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

            // kick off real-time updating
            // _ = StartRealtimeAsync(vals);
        }

        // private async Task StartRealtimeAsync(ObservableCollection<DateTimePoint> vals)
        // {
        //     while (true)
        //     {
        //         var dto = await _svc.GetMetricsAsync();
        //         var ts = DateTimeOffset
        //             .FromUnixTimeMilliseconds(dto.LastPduReceivedTimestampMs)
        //             .LocalDateTime;
        //         App.Current.Dispatcher.Invoke(() =>
        //         {
        //             vals.Add(new DateTimePoint(ts, dto.PdusInLastSixtySeconds));
        //             if (vals.Count > 60)
        //                 vals.RemoveAt(0);
        //             VolumeXAxes[0].MinLimit = vals[0].DateTime.Ticks;
        //             VolumeXAxes[0].MaxLimit = ts.Ticks;
        //             TotalVolumeLastMinute = dto.PdusInLastSixtySeconds;
        //             AverageVolumePerSecond = dto.AveragePduRatePerSecondLastSixtySeconds;
        //             OnPropertyChanged(nameof(TotalVolumeLastMinute));
        //             OnPropertyChanged(nameof(AverageVolumePerSecond));
        //         });
        //         await Task.Delay(1000);
        //     }
        // }

        private async void RefreshHistorical()
        {
            // TODO: fetch historical data for SelectedDate+Time → SelectedDate+Time+1h
            // then repopulate VolumeSeries[0].Values and metrics
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
