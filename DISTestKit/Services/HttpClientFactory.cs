using System;
using System.Net.Http;

namespace DISTestKit.Services
{
    public static class HttpClientFactory
    {
        public static HttpClient CreateAuthenticatedClient(string baseUrl)
        {
            var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

            var token = TokenManager.GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }
    }
}
