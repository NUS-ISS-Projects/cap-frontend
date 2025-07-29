using System.Windows;
using DISTestKit.Pages;

namespace DISTestKit
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        public void ShowLogin()
        {
            SideNavPanel.Visibility = Visibility.Collapsed;
            MainContent.Content = new LoginPage();
        }

        public void ShowRegister()
        {
            SideNavPanel.Visibility = Visibility.Collapsed;
            MainContent.Content = new RegisterPage();
        }

        public void ShowDashboard()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            MainContent.Content = new DashboardPage();
        }

        public void ShowForecast()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            MainContent.Content = new ForecastPage();
        }

        public void ShowSettings()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            MainContent.Content = new SettingsPage();
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e) => ShowDashboard();

        private void ForecastButton_Click(object sender, RoutedEventArgs e) => ShowForecast();

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettings();
    }
}
