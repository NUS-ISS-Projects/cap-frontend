using System.Windows;
using System.Windows.Controls;
using DISTestKit.Services;

namespace DISTestKit.Pages
{
    public partial class RegisterPage : UserControl
    {
        private readonly AuthenticationService _authService;

        public RegisterPage()
        {
            InitializeComponent();
            _authService = new AuthenticationService("http://34.142.158.178/api/");
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailBox.Text?.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both email and password.", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RegisterButton.IsEnabled = false;
                RegisterButton.Content = "Registering...";

                var result = await _authService.RegisterAsync(email, password);

                if (result.IsSuccess)
                {
                    MessageBox.Show(result.ErrorMessage ?? "Registration successful! Please log in.", "Registration Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    (Application.Current.MainWindow as MainWindow)!.ShowLogin();
                }
                else
                {
                    MessageBox.Show(result.ErrorMessage ?? "Registration failed", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RegisterButton.IsEnabled = true;
                RegisterButton.Content = "Register";
            }
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)!.ShowLogin();
        }
    }
}
