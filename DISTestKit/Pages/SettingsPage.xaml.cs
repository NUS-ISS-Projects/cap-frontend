using System.Windows;
using System.Windows.Controls;
using DISTestKit.ViewModel;

namespace DISTestKit.Pages
{
    public partial class SettingsPage : UserControl
    {
        private readonly SettingsViewModel _vm;

        public SettingsPage()
        {
            InitializeComponent();
            _vm = new SettingsViewModel();
            DataContext = _vm;

            PasswordBox.Password = _vm.Password;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _vm.Password = PasswordBox.Password;
        }
    }
}
