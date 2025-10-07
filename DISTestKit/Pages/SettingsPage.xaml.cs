using System.Windows.Controls;
using DISTestKit.ViewModel;

namespace DISTestKit.Pages
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }
    }
}
