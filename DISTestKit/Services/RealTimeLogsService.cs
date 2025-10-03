using System.Net.Http;
using System.Net.Http.Json;

namespace DISTestKit.Services
{
    public class RealTimeLogsService
    {
        private readonly HttpClient _http;

        public RealTimeLogsService(string baseUrl)
        {
            _http = HttpClientFactory.CreateAuthenticatedClient(baseUrl);
        }

        public record PduMessageDto(
            int Id,
            string PDUType,
            int Length,
            Dictionary<string, object> RecordDetails
        );

        public class RealtimeLogsResponse
        {
            public List<PduMessageDto> Pdu_messages { get; set; } = new();
        }

        /// <summary>
        /// GET /api/acquisition/realtime/logs?startTime=…&endTime=…[&limit=…]
        /// </summary>
        public async Task<List<PduMessageDto>> GetRealTimeLogsAsync(
            long startTime,
            long endTime,
            int? limit = null
        )
        {
            var url = $"acquisition/realtime/logs?startTime={startTime}&endTime={endTime}";
            if (limit.HasValue)
                url += $"&limit={limit.Value}";

            var resp = await _http.GetFromJsonAsync<RealtimeLogsResponse>(url);
            return resp?.Pdu_messages ?? new List<PduMessageDto>();
        }

        public static long FromDisAbsoluteTimestamp(long disTimestamp)
        {
            const long msbMask = 0x80000000L;
            const long valueMask = 0x7FFFFFFFL;
            return (disTimestamp & msbMask) != 0 ? disTimestamp & valueMask : disTimestamp;
        }

        public static long ToDisAbsoluteTimestamp(long unixSeconds)
        {
            const long msbMask = 0x80000000L;
            return (unixSeconds & 0x7FFFFFFFL) | msbMask;
        }
    }
}
