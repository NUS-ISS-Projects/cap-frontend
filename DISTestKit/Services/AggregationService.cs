using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace DISTestKit.Services
{
    public class AggregationService
    {
        private readonly HttpClient _http;

        public AggregationService(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public record AggregateResponse(
            string TimeUnit,
            string Start,
            string End,
            List<Bucket> Buckets
        );

        public record Bucket(
            int? Hour,
            string? Date,
            string? Week,
            long TotalPackets,
            long EntityStatePduCount,
            long FireEventPduCount,
            long CollisionPduCount,
            long DetonationPduCount,
            long DataPduCount,
            long ActionRequestPduCount,
            long StartResumePduCount,
            long SetDataPduCount,
            long DesignatorPduCount,
            long ElectromagneticEmissionsPduCount
        );

        /// <summary>
        /// GET /api/acquisition/aggregate?today=true
        /// or ?week=true, ?month=true,
        /// or ?date=YYYY-MM-DD
        /// or ?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
        /// </summary>
        public async Task<AggregateResponse> GetAggregateAsync(
            bool today = false,
            bool week = false,
            bool month = false,
            DateTime? date = null,
            DateTime? startDate = null,
            DateTime? endDate = null
        )
        {
            var q = new List<string>();
            if (today)
                q.Add("today=true");
            else if (week)
                q.Add("week=true");
            else if (month)
                q.Add("month=true");
            else if (date.HasValue)
                q.Add($"date={date.Value:yyyy-MM-dd}");
            else if (startDate.HasValue && endDate.HasValue)
            {
                q.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                q.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            }

            var url = "acquisition/aggregate" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
            var resp = await _http.GetFromJsonAsync<AggregateResponse>(url);
            if (resp is null)
                throw new InvalidOperationException("Aggregate API returned no data.");
            return resp;
        }
    }
}
