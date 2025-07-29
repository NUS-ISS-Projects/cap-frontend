using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DISTestKit.ViewModel
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private string _name = "";
        private string _email = "";
        private string _password = "";

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

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        public ICommand SaveCommand { get; }

        public SettingsViewModel()
        {
            // load existing settings from somewhere…
            LoadExisting();

            SaveCommand = new RelayCommand(async () => await SaveAsync());
        }

        private void LoadExisting()
        {
            // TODO: fetch from local storage or HTTP backend
            Name = "Cindy";
            Email = "cindy@example.com";
        }

        private async Task SaveAsync()
        {
            // TODO: push Name/Email/Password to your API
            // e.g. await _settingsService.UpdateAsync(Name, Email, Password);

            // simulate:
            await Task.Delay(200);
            System.Windows.MessageBox.Show(
                "Settings saved!",
                "Success",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information
            );
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
