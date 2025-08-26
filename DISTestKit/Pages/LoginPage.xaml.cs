using System.Windows;
using System.Windows.Controls;
using DISTestKit.Services;

namespace DISTestKit.Pages
{
    public partial class LoginPage : UserControl
    {
        private readonly AuthenticationService _authService;

        public LoginPage()
        {
            InitializeComponent();
            _authService = new AuthenticationService("http://localhost:32080/api/");
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailBox.Text?.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both email and password.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                LoginButton.IsEnabled = false;
                LoginButton.Content = "Logging in...";

                var result = await _authService.LoginAsync(email, password);

                if (result.IsSuccess && result.Token != null)
                {
                    TokenManager.SetToken(result.Token);
                    (Application.Current.MainWindow as MainWindow)!.ShowDashboard();
                }
                else
                {
                    MessageBox.Show(result.ErrorMessage ?? "Login failed", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Login";
            }
        }

        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)!.ShowRegister();
        }
    }
}
