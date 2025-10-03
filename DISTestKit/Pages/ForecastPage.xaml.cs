using System.Windows.Controls;
using System.Windows.Input;
using DISTestKit.ViewModel;

namespace DISTestKit.Pages
{
    /// <summary>
    /// Interaction logic for ForecastPage.xaml
    /// </summary>
    public partial class ForecastPage : UserControl
    {
        public ForecastPage()
        {
            InitializeComponent();
            var realTimeService = new Services.RealTimeMetricsService(
                "http://34.142.158.178/api/"
            );
            DataContext = new ForecastViewModel(realTimeService);
        }

        private void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                if (DataContext is ForecastViewModel viewModel && viewModel.CanSendMessage)
                {
                    viewModel.SendMessageCommand.Execute(null);
                }
            }
        }

        private void ChatMenuButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
