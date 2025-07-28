using System.Windows;
using System.Windows.Controls;

namespace DISTestKit.Pages
{
    public partial class LoginPage : UserControl
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // swap to DashboardPage
            (Application.Current.MainWindow as MainWindow)!.ShowDashboard();
        }

        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            // swap to RegisterPage
            (Application.Current.MainWindow as MainWindow)!.ShowRegister();
        }
    }
}
