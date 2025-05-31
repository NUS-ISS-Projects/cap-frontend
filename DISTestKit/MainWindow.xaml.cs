using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DISTestKit.ViewModel;

namespace DISTestKit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ChartViewModel? _chartViewModel;
        private readonly DISListenerViewModel? _disReceiver;

        public MainWindow()
        {
            try
                {
                    InitializeComponent();

                    _chartViewModel = new ChartViewModel();
                    DataContext = _chartViewModel;

                    _disReceiver = new DISListenerViewModel(_chartViewModel);
                    _disReceiver.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Startup Exception");
                    Application.Current.Shutdown();
                }

        }
    }
}