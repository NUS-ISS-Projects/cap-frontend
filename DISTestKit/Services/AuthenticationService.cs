using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DISTestKit.Model;

namespace DISTestKit.Services
{
    public class AuthenticationService
    {
        private readonly HttpClient _http;

        public AuthenticationService(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public async Task<AuthenticationResult> LoginAsync(string email, string password)
        {
            try
            {
                var request = new LoginRequest(email, password);
                var response = await _http.PostAsJsonAsync("auth/login", request);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (loginResponse?.Token != null)
                    {
                        return new AuthenticationResult(true, loginResponse.Token);
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new AuthenticationResult(
                    false,
                    ErrorMessage: $"Login failed: {errorContent}"
                );
            }
            catch (Exception ex)
            {
                return new AuthenticationResult(
                    false,
                    ErrorMessage: $"Network error: {ex.Message}"
                );
            }
        }

        public async Task<AuthenticationResult> RegisterAsync(string email, string password)
        {
            try
            {
                var request = new RegisterRequest(email, password);
                var response = await _http.PostAsJsonAsync("auth/register", request);

                if (response.IsSuccessStatusCode)
                {
                    var registerResponse =
                        await response.Content.ReadFromJsonAsync<RegisterResponse>();
                    return new AuthenticationResult(
                        true,
                        ErrorMessage: registerResponse?.Message ?? "Registration successful"
                    );
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new AuthenticationResult(
                    false,
                    ErrorMessage: $"Registration failed: {errorContent}"
                );
            }
            catch (Exception ex)
            {
                return new AuthenticationResult(
                    false,
                    ErrorMessage: $"Network error: {ex.Message}"
                );
            }
        }
    }
}
