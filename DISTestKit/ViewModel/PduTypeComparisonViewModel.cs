using System.Collections.ObjectModel;
using System.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DISTestKit.ViewModel
{
    public class PduTypeComparisonViewModel : INotifyPropertyChanged
    {
        // The two series: EntityStatePdu and FireEventPdu
        public ObservableCollection<ISeries> Series { get; }

        // A single X‐axis with two labels
        public Axis[] XAxes { get; }
        public Axis[] YAxes { get; }

        public PduTypeComparisonViewModel()
        {
            var entityCount = 0;
            var fireCount   = 0;

            Series = new ObservableCollection<ISeries>
            {
                new ColumnSeries<long>
                {
                    Name   = "EntityStatePdu",
                    Values = new ObservableCollection<long> { entityCount },
                    Fill   = new SolidColorPaint(SKColors.CornflowerBlue)
                },
                new ColumnSeries<long>
                {
                    Name   = "FireEventPdu",
                    Values = new ObservableCollection<long> { fireCount },
                    Fill   = new SolidColorPaint(SKColors.OrangeRed)
                }
            };

            XAxes = new[]
            {
                new Axis
                {
                    Labels = new[] { "Counts" },
                    Name   = ""    
                }
            };

            YAxes = new[]
            {
                new Axis
                {
                    Name     = "",
                    MinLimit = 0
                }
            };
        }

        public void UpdateCounts(long entityStateCount, long fireEventCount)
        {
            // assumes Series[0] is EntityState, Series[1] is FireEvent
            var entitySeries = (ColumnSeries<long>)Series[0];
            var fireSeries = (ColumnSeries<long>)Series[1];

            if (entitySeries.Values is ObservableCollection<long> entityValues && entityValues.Count > 0)
                entityValues[0] = entityStateCount;

            if (fireSeries.Values is ObservableCollection<long> fireValues && fireValues.Count > 0)
                fireValues[0] = fireEventCount;

            OnPropertyChanged(nameof(Series));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}