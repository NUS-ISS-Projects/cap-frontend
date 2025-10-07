using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DISTestKit.Model;

namespace DISTestKit.Services
{
    public class UserService
    {
        private readonly HttpClient _http;

        public UserService(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public async Task<UserProfile?> GetUserProfileAsync()
        {
            try
            {
                var token = TokenManager.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    token
                );
                _http.DefaultRequestHeaders.Accept.Clear();
                _http.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );

                var response = await _http.GetAsync("users/profile");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserProfile>();
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<UserSession?> GetUserSessionAsync()
        {
            try
            {
                var token = TokenManager.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    token
                );
                _http.DefaultRequestHeaders.Accept.Clear();
                _http.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );

                var response = await _http.GetAsync("user-sessions");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserSession>();
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> SaveUserSessionAsync(SaveUserSessionRequest request)
        {
            try
            {
                var token = TokenManager.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    token
                );
                _http.DefaultRequestHeaders.Accept.Clear();
                _http.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );

                var response = await _http.PostAsJsonAsync("user-sessions", request);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
