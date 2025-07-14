using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DISTestKit.Model;

namespace DISTestKit.Services
{
    public class RealTimeMetricsService
    {
        private readonly HttpClient _http;
        public RealTimeMetricsService(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public async Task<RealTimeMetrics> GetAsync()
        {
            var result = await _http.GetFromJsonAsync<RealTimeMetrics>("realtime");
            if (result == null)
                throw new InvalidOperationException("Failed to retrieve real-time metrics.");
            return result;
        }

        public async Task<List<EntityStateRecord>> GetHistoricalEntityStatesAsync(long startTime, long endTime)
            => await _http.GetFromJsonAsync<List<EntityStateRecord>>(
                   $"entity-states?startTime={startTime}&endTime={endTime}")
               ?? new List<EntityStateRecord>();

        public async Task<List<FireEventRecord>> GetHistoricalFireEventsAsync(long startTime, long endTime)
            => await _http.GetFromJsonAsync<List<FireEventRecord>>(
                   $"fire-events?startTime={startTime}&endTime={endTime}")
               ?? new List<FireEventRecord>();

        public record PduMessageDto(
            int Id,
            string PDUType,
            int Length,
            Dictionary<string, object> recordDetails
        );

        /// <summary>
        /// Temporary mock implementation—returns 5 fake messages with incrementing IDs.
        /// Swap this out with your real HTTP GET when you're ready.
        /// </summary>
        public static async Task<List<PduMessageDto>> GetHistoricalLogsAsync(long startTime, long endTime)
        {
            // simulate a tiny delay
            await Task.Delay(50);

            var list = new List<PduMessageDto>();
            for (int i = 0; i < 5; i++)
            {
                var ts = startTime + i;
                var human = DateTimeOffset
                    .FromUnixTimeSeconds(ts)
                    .UtcDateTime
                    .ToString("yyyy-MM-ddTHH:mm:ssZ");

                var details = new Dictionary<string, object>
                {
                    ["timestampEpoch"]  = ts,
                    ["timestampHuman"]  = human,
                    ["site"]            = 18,
                    ["application"]     = 23,
                    ["entity"]          = 1000 + i,
                    ["locationX"]       = -2699992.1562432176 + i,
                    ["locationY"]       = -4299991.894479483  + i,
                    ["locationZ"]       =  3783925.059998549  + i
                };

                list.Add(new PduMessageDto(
                    Id: i + 1,
                    PDUType: (i % 2 == 0) ? "EntityState" : "Designator",
                    Length: 150 + i * 2,
                    recordDetails: details
                ));
            }

            return list;
        }
        // ----------------------------------------------------------------

        public static long FromDisAbsoluteTimestamp(long disTimestamp)
        {
            const long msbMask   = 0x80000000L;
            const long valueMask = 0x7FFFFFFFL;
            return (disTimestamp & msbMask) != 0
                ? disTimestamp & valueMask
                : disTimestamp;
        }
        public static long ToDisAbsoluteTimestamp(long unixSeconds)
        {
            const long msbMask = 0x80000000L;
            return (unixSeconds & 0x7FFFFFFFL) | msbMask;
        }
    }
}
