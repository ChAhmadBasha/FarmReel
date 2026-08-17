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
    }
}
