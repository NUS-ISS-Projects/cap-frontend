using System;
using System.Collections.ObjectModel;
using DISTestKit.Model;
using DISTestKit.Services;

namespace DISTestKit.ViewModel
{
    public class LogViewModel
    {
        public ObservableCollection<DisPacket> Packets { get; } = new ObservableCollection<DisPacket>();
        private int _nextNo = 1;

        public void Reset() => Packets.Clear();

        public void AddEntityState(long ts, int site, int app, int entity, 
                           double x, double y, double z)
        {
            var proto = "EntityStatePdu";
            var src   = $"{site}-{app}-{entity}";
            var dest  = "NA";
            var info  = $"X={x:F2}, Y={y:F2}, Z={z:F2}";
            AddCommon(ts, PacketType.EntityState, src, dest, proto, 0, info);
        }

        public void AddFireEvent(long ts, int fSite,int fApp,int fEntity,
                                        int tSite,int tApp,int tEntity,
                                        int mSite,int mApp,int mEntity)
        {
            var proto = "FireEventPdu";
            var src   = $"{fSite}-{fApp}-{fEntity}";
            var dest  = $"{tSite}-{tApp}-{tEntity}";
            var info  = $"Munition={mSite}-{mApp}-{mEntity}";
            AddCommon(ts, PacketType.FireEvent, src, dest, proto, 0, info);
        }

        private void AddCommon(long ts, PacketType type, string src, string dest, 
                       string proto, int len, string info)
        {
            long epoch = RealTimeMetricsService.FromDisAbsoluteTimestamp(ts);
            var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime;
            Packets.Insert(0, new DisPacket {
                No          = _nextNo++,
                Time        = dt,
                Type        = type,
                Source      = src,
                Destination = dest,
                Protocol    = proto,
                Info        = info
            });
        }
    }
}