using System.Net.Http;
using System.Net.Http.Json;

namespace DISTestKit.Services
{
    public class RealTimeMetricsService
    {
        private readonly HttpClient _http;

        public RealTimeMetricsService(string baseUrl)
        {
            _http = HttpClientFactory.CreateAuthenticatedClient(baseUrl);
        }

        public record PeakLoadDto(
            double PeakPacketsPerSecond,
            DateTime PeakIntervalStartUtc,
            DateTime PeakIntervalEndUtc,
            int PacketsInPeakInterval
        );

        public record RealTimeMetricsDto(
            string TimeWindowDescription,
            DateTime DataFromUtc,
            DateTime DataUntilUtc,
            int TotalPackets,
            int EntityStatePackets,
            int FireEventPackets,
            int CollisionPackets,
            int DetonationPackets,
            int DataPduPackets,
            int ActionRequestPduPackets,
            int StartResumePduPackets,
            int SetDataPduPackets,
            int DesignatorPduPackets,
            int ElectromagneticEmissionsPduPackets,
            double AveragePacketsPerSecond,
            PeakLoadDto PeakLoad
        );

        /// <summary>
        /// GET /api/acquisition/metrics
        /// </summary>
        public async Task<RealTimeMetricsDto> GetMetricsAsync()
        {
            var resp = await _http.GetFromJsonAsync<RealTimeMetricsDto>("acquisition/metrics");
            if (resp == null)
                throw new InvalidOperationException("Failed to fetch acquisition metrics.");
            return resp;
        }
    }
}
