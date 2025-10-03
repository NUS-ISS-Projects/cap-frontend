using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace DISTestKit.Services
{
    public class PredictionService
    {
        private readonly HttpClient _http;

        public PredictionService(string baseUrl)
        {
            _http = HttpClientFactory.CreateAuthenticatedClient(baseUrl);
        }

        public record PredictionRequest(string timeUnit, string startDate);

        public record TrainingDataRange(string startDate, string endDate);

        public record PredictionMetadata(
            string dataSource,
            string forecastStartDate,
            int predictionPeriods,
            string timeUnit,
            int trainingDataPoints,
            TrainingDataRange trainingDataRange
        );

        public record PredictionResponse(
            PredictionMetadata metadata,
            List<string> predicted_labels,
            List<double> predicted_values
        );

        public record ChatRequest(string question, string sessionId);

        public record ChatResponse(
            string answer,
            string question,
            string sessionId,
            string timestamp
        );

        /// <summary>
        /// POST /api/prediction
        /// </summary>
        public async Task<PredictionResponse> GetPredictionAsync(
            string timeUnit,
            string startDate
        )
        {
            var request = new PredictionRequest(timeUnit, startDate);
            var response = await _http.PostAsJsonAsync("prediction", request);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Prediction API failed with status {response.StatusCode}"
                );
            }

            var result = await response.Content.ReadFromJsonAsync<PredictionResponse>();
            if (result == null)
                throw new InvalidOperationException("Prediction API returned no data.");

            return result;
        }

        /// <summary>
        /// POST /api/prediction/chat
        /// </summary>
        public async Task<ChatResponse> GetChatResponseAsync(string question, string sessionId)
        {
            var request = new ChatRequest(question, sessionId);
            var response = await _http.PostAsJsonAsync("prediction/chat", request);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Chat API failed with status {response.StatusCode}"
                );
            }

            var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
            if (result == null)
                throw new InvalidOperationException("Chat API returned no data.");

            return result;
        }
    }
}
