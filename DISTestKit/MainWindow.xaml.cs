using System.Windows;
using DISTestKit.ViewModel;
using Wpf.Ui.Appearance; 
using Wpf.Ui.Controls;

namespace DISTestKit
{
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
                System.Windows.MessageBox.Show(ex.ToString(), "Startup Exception");
                Application.Current.Shutdown();
            }

        }
    }
}