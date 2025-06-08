using System;
using System.Net.Http;
using System.Net.Http.Json;
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

        public static long FromDisAbsoluteTimestamp(long disTimestamp)
        {
            const long msbMask = 0x80000000L;
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