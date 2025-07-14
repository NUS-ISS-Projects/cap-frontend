using System.Windows.Controls;

namespace DISTestKit.Pages
{
    /// <summary>
    /// Interaction logic for DashboardPage.xaml
    /// </summary>
    public partial class DashboardPage : UserControl
    {
        public DashboardPage()
        {
            InitializeComponent();
            DataContext = new ViewModel.MainViewModel();
        }
    }
}
