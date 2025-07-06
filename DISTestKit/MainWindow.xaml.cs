using System.Windows;
using DISTestKit.ViewModel;

namespace DISTestKit
{
public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            DashboardView.Visibility = Visibility.Visible;
            ForecastView.Visibility = Visibility.Collapsed;
        }
        private void ForecastButton_Click(object sender, RoutedEventArgs e)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            ForecastView.Visibility = Visibility.Visible;
        }
    }
}