using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class SettingsView : UserControl
    {
        private SettingsViewModel _vm;
        public SettingsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public SettingsView() { InitializeComponent(); }

        private void Save_Click(object sender, RoutedEventArgs e) => _vm?.SaveAll();
        private async void Activate_Click(object sender, RoutedEventArgs e) { if (_vm != null) await _vm.ActivateLicenseAsync(); }
        private async void CheckAi_Click(object sender, RoutedEventArgs e) { if (_vm != null) await _vm.CheckAiAsync(); }
        private void Backup_Click(object sender, RoutedEventArgs e) => _vm?.BackupNow();
        private async void Env_Click(object sender, RoutedEventArgs e) { if (_vm != null) await _vm.CheckEnvironmentAsync(); }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var (available, version, url, sha, notes) = await _vm.CheckForUpdatesAsync();
            if (!available)
            {
                MessageBox.Show($"You are on the latest version ({_vm.CurrentVersion}).",
                    "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var ask = MessageBox.Show(
                $"Update available: {_vm.CurrentVersion} -> {version}\n\n{notes}\n\nDownload and install now?",
                "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ask != MessageBoxResult.Yes) return;
            var path = await _vm.DownloadUpdateAsync(url, sha);
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Download failed (see Logs tab).", "Update", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show("Update downloaded. The app will close and restart to finish installing.",
                "Update", MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.ApplyUpdate(path);
        }
    }
}
