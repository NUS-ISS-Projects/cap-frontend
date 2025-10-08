using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DISTestKit.Model;
using DISTestKit.Services;

namespace DISTestKit.ViewModel
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly UserService _userService;
        private string _userId = "";
        private string _name = "";
        private string _email = "";

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged(nameof(Email));
            }
        }

        public ICommand SaveCommand { get; }

        public SettingsViewModel()
        {
            _userService = new UserService("http://34.142.158.178/api/");
            // Load existing settings from cache or backend
            _ = LoadExistingAsync();

            SaveCommand = new RelayCommand(async () => await SaveAsync());
        }

        private async Task LoadExistingAsync()
        {
            try
            {
                // First fetch user profile to get userId
                var profile = await _userService.GetUserProfileAsync();
                if (profile != null)
                {
                    _userId = profile.UserId;
                    Email = profile.Email;

                    // Then fetch user session using the userId
                    var session = await _userService.GetUserSessionAsync();
                    if (session != null && !string.IsNullOrEmpty(session.Name))
                    {
                        Name = session.Name;
                    }
                    else
                    {
                        Name = "User";
                    }
                }
            }
            catch
            {
                Name = "User";
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Save user session with current name
                var lastSession = new LastSession(
                    Date: DateTime.UtcNow.ToString("o"),
                    View: "dashboard"
                );

                var request = new SaveUserSessionRequest(
                    UserId: _userId,
                    UserName: Email,
                    Name: Name,
                    LastSession: lastSession
                );

                var success = await _userService.SaveUserSessionAsync(request);

                if (success)
                {
                    // Update the MainWindow's UserName display
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow =
                            System.Windows.Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            mainWindow.UserName = Name;
                        }
                    });

                    System.Windows.MessageBox.Show(
                        "Settings saved!",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information
                    );
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Failed to save settings. Please try again.",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error saving settings: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
