using System.Windows;
using DISTestKit.ViewModel;

namespace DISTestKit
{
public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}