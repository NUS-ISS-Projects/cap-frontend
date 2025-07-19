using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using DISTestKit.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DISTestKit.ViewModel
{
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

        private readonly RealTimeMetricsService _svc;

        public ForecastViewModel(RealTimeMetricsService svc)
        {
            _svc = svc;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
