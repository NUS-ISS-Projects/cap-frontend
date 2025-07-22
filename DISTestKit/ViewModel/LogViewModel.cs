using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DISTestKit.Model;

namespace DISTestKit.ViewModel
{
    public class LogViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DisPacket> Packets { get; } =
            new ObservableCollection<DisPacket>();

        private DisPacket? _selectedPacket;
        public DisPacket? SelectedPacket
        {
            get => _selectedPacket;
            set
            {
                if (_selectedPacket != value)
                {
                    _selectedPacket = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public void Reset() => Packets.Clear();

        /// <summary>
        /// Called for every message, regardless of PDUType.
        /// </summary>
        public void AddPacket(
            int id,
            string pduType,
            int length,
            Dictionary<string, object> recordDetails
        )
        {
            if (
                !recordDetails.TryGetValue("timestampEpoch", out var rawTs)
                || !long.TryParse(rawTs?.ToString(), out var epoch)
            )
            {
                epoch = 0;
            }

            var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime;
            var details = new Dictionary<string, object>(recordDetails);
            var packet = new DisPacket
            {
                No = id,
                Time = dt,
                PDUType = pduType,
                Source = "192.168.56.1",
                Destination = "192.168.56.255",
                Protocol = "DIS",
                Length = length,
                Details = details,
            };

            Packets.Insert(0, packet);
            const int maxEntries = 100;
            while (Packets.Count > maxEntries)
                Packets.RemoveAt(Packets.Count - 1);
        }
    }
}
