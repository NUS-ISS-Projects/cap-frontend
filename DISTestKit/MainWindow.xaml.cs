using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DISTestKit.Pages;
using DISTestKit.Services;

namespace DISTestKit
{
    public partial class MainWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private DispatcherTimer? _timeTimer;
        private string _userName = "User";

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(
                this,
                new System.ComponentModel.PropertyChangedEventArgs(propertyName)
            );
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            SetRobotIcon();
            InitializeTimer();

            // Check if user has a valid token
            if (TokenManager.HasValidToken())
            {
                ShowDashboard();
            }
            else
            {
                ShowLogin();
            }
        }

        private void SetRobotIcon()
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                var robotBrush = new SolidColorBrush(Color.FromRgb(80, 132, 221));
                var backgroundBrush = new SolidColorBrush(Colors.White);

                drawingContext.DrawEllipse(
                    backgroundBrush,
                    new Pen(robotBrush, 1),
                    new System.Windows.Point(16, 16),
                    15,
                    15
                );

                drawingContext.DrawRectangle(
                    robotBrush,
                    null,
                    new System.Windows.Rect(10, 18, 12, 10)
                );

                drawingContext.DrawRectangle(
                    robotBrush,
                    null,
                    new System.Windows.Rect(12, 8, 8, 8)
                );

                var eyeBrush = new SolidColorBrush(Colors.White);
                drawingContext.DrawEllipse(eyeBrush, null, new System.Windows.Point(14, 11), 1, 1);
                drawingContext.DrawEllipse(eyeBrush, null, new System.Windows.Point(18, 11), 1, 1);

                var antennaPen = new Pen(robotBrush, 1);
                drawingContext.DrawLine(
                    antennaPen,
                    new System.Windows.Point(16, 8),
                    new System.Windows.Point(16, 5)
                );
                drawingContext.DrawEllipse(robotBrush, null, new System.Windows.Point(16, 4), 1, 1);
            }

            var renderTargetBitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            this.Icon = renderTargetBitmap;
        }

        private void InitializeTimer()
        {
            _timeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timeTimer.Tick += UpdateTime;
            _timeTimer.Start();

            UpdateTime(null, null);
        }

        private void UpdateTime(object? sender, EventArgs? e)
        {
            var now = DateTime.Now;

            var timeTextBlock =
                this.FindName("CurrentTimeText") as System.Windows.Controls.TextBlock;
            if (timeTextBlock != null)
            {
                timeTextBlock.Text = now.ToString("HH:mm:ss");
            }

            var dateTextBlock =
                this.FindName("CurrentDateText") as System.Windows.Controls.TextBlock;
            if (dateTextBlock != null)
            {
                dateTextBlock.Text = now.ToString("dd/MM/yyyy");
            }
        }

        public void ShowLogin()
        {
            SideNavPanel.Visibility = Visibility.Collapsed;
            MainContent.Content = new LoginPage();
        }

        public void ShowRegister()
        {
            SideNavPanel.Visibility = Visibility.Collapsed;
            MainContent.Content = new RegisterPage();
        }

        public void ShowDashboard()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            MainContent.Content = new DashboardPage();
            UpdateUserName();
        }

        private void UpdateUserName()
        {
            UserName = "User";
        }

        public void UpdateLastSelectedDate(string dateText)
        {
            var lastSelectedDateTextBlock = this.FindName("LastSelectedDateText") as TextBlock;
            if (lastSelectedDateTextBlock != null)
            {
                lastSelectedDateTextBlock.Text = dateText;
            }
        }

        public void ShowForecast()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            MainContent.Content = new ForecastPage();
        }

        public void ShowSettings()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            MainContent.Content = new SettingsPage();
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e) => ShowDashboard();

        private void ForecastButton_Click(object sender, RoutedEventArgs e) => ShowForecast();

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettings();

        private void LogoutButton_Click(object sender, RoutedEventArgs e) => Logout();

        public void Logout()
        {
            TokenManager.ClearToken();
            UserSessionCache.Clear();
            ShowLogin();
        }
    }
}
