using System.Windows;
using System.Windows.Controls;

namespace DISTestKit.Pages
{
    public partial class RegisterPage : UserControl
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)!.ShowLogin();
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)!.ShowLogin();
        }
    }
}
