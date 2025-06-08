using System.Windows;
using DISTestKit.ViewModel;

namespace DISTestKit
{
public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
             // Auto‐start streaming on launch:
            _vm.PlayCommand.Execute(null);
        }
    }
}