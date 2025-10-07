using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DISTestKit.Services
{
    public class TokenExpirationHandler : DelegatingHandler
    {
        private static bool _isHandlingExpiration = false;
        private static readonly object _lock = new object();

        public static void ResetExpirationFlag()
        {
            lock (_lock)
            {
                _isHandlingExpiration = false;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                bool shouldHandle = false;
                lock (_lock)
                {
                    if (!_isHandlingExpiration)
                    {
                        _isHandlingExpiration = true;
                        shouldHandle = true;
                    }
                }

                if (shouldHandle)
                {
                    // Token has expired, clear it and redirect to login
                    TokenManager.ClearToken();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            // Check if we're not already on the login page
                            var currentContent = mainWindow.MainContent?.Content;
                            if (currentContent is not DISTestKit.Pages.LoginPage)
                            {
                                MessageBox.Show(
                                    "Your session has expired. Please log in again.",
                                    "Session Expired",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information
                                );
                                mainWindow.ShowLogin();
                            }
                        }
                    });
                }
            }

            return response;
        }
    }

    public static class HttpClientFactory
    {
        public static HttpClient CreateAuthenticatedClient(string baseUrl)
        {
            var handler = new TokenExpirationHandler { InnerHandler = new HttpClientHandler() };
            var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };

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
