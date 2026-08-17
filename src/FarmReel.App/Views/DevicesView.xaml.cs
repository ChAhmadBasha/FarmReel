using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class DevicesView : UserControl
    {
        private DevicesViewModel _vm;
        public DevicesViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public DevicesView()
        {
            InitializeComponent();
        }

        public void Refresh() => _vm?.Refresh();

        private DeviceRow Selected => Grid.SelectedItem as DeviceRow;

        private void AddLd_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Ask("Name (e.g. LD_1)", "LD_1");
            var index = int.TryParse(InputBox.Ask("Instance index (0-based)", "0"), out var i) ? i : 0;
            var cpu = int.TryParse(InputBox.Ask("CPU cores", "2"), out var c) ? c : 2;
            var ram = int.TryParse(InputBox.Ask("RAM (MB)", "2048"), out var r) ? r : 2048;
            _vm.AddDevice(name, index, "LDPlayer", "", cpu, ram);
        }

        private void AddMuMu_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Ask("Name (e.g. MuMu_1)", "MuMu_1");
            var index = int.TryParse(InputBox.Ask("Instance index (0-based)", "0"), out var i) ? i : 0;
            _vm.AddDevice(name, index, "MuMu", "", 4, 4096);
        }

        private void AddPhone_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Ask("Name", "Phone_1");
            var serial = InputBox.Ask("ADB serial (ip:port)", "192.168.1.50:5555");
            _vm.AddDevice(name, 0, "RealPhone", serial, 0, 0);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeleteDevice(Selected);
        private async void Scan_Click(object sender, RoutedEventArgs e) => await _vm.ScanOnlineAsync();
        private async void Start_Click(object sender, RoutedEventArgs e) => await _vm.StartDeviceAsync(Selected);
        private async void Stop_Click(object sender, RoutedEventArgs e) => await _vm.StopDeviceAsync(Selected);
        private void Fingerprint_Click(object sender, RoutedEventArgs e) => _vm.GenerateFingerprint(Selected);
        private async void SyncGps_Click(object sender, RoutedEventArgs e) => await _vm.SyncGpsTimezoneAsync(Selected);

        private void Arrange_Click(object sender, RoutedEventArgs e)
        {
            var cols = int.TryParse(ColumnsBox.Text, out var c) ? c : 3;
            _vm.ArrangeWindows(cols);
        }

        private async void InstallApk_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "APK files (*.apk)|*.apk" };
            if (dlg.ShowDialog() == true)
            {
                var pkg = InputBox.Ask("Package name", "com.facebook.katana");
                await _vm.InstallApkAsync(Selected, dlg.FileName, pkg);
            }
        }

        private void ImportVpn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "OpenVPN profiles (*.ovpn)|*.ovpn" };
            if (dlg.ShowDialog() == true)
            {
                var name = InputBox.Ask("Profile name", System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));
                _vm.ImportVpnProfile(dlg.FileName, name);
            }
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose backup folder" };
            if (dlg.ShowDialog() == true) _vm.BackupDevices(dlg.FolderName);
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (_vm != null) _vm.Filter = FilterBox.Text;
        }

        private void BridgeOn_Click(object sender, RoutedEventArgs e) => _vm.ToggleBridge(Selected, true);
        private void BridgeOff_Click(object sender, RoutedEventArgs e) => _vm.ToggleBridge(Selected, false);

        private async void TimeChange_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var hours = int.TryParse(InputBox.Ask("Hours to shift (e.g. -6 or +12)", "0"), out var h) ? h : 0;
            await _vm.ChangeDeviceTimeAsync(Selected, hours);
        }

        private async void ClearFb_Click(object sender, RoutedEventArgs e) => await _vm.ClearFbDataAsync(Selected);

        private void CopyLd_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Ask("New instance name", "LD_copy");
            var from = int.TryParse(InputBox.Ask("Copy from index", "0"), out var i) ? i : 0;
            if (!string.IsNullOrEmpty(name)) _vm.CopyInstance(name, from);
        }

        private void RemoveLd_Click(object sender, RoutedEventArgs e)
        {
            var index = int.TryParse(InputBox.Ask("Instance index to remove", "0"), out var i) ? i : 0;
            if (MessageBox.Show($"Remove LDPlayer index {index}? This deletes the emulator VM.",
                    "Remove instance", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                _vm.RemoveInstance(index);
        }

        private void BackupLd_Click(object sender, RoutedEventArgs e)
        {
            var index = int.TryParse(InputBox.Ask("Instance index to back up", "0"), out var i) ? i : 0;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Backup destination",
                FileName = $"LD_{index}.ldbk",
                Filter = "LDPlayer backups (*.ldbk)|*.ldbk"
            };
            if (dlg.ShowDialog() == true) _vm.BackupInstance(index, dlg.FileName);
        }

        private void RestoreLd_Click(object sender, RoutedEventArgs e)
        {
            var index = int.TryParse(InputBox.Ask("Instance index to restore into", "0"), out var i) ? i : 0;
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "LDPlayer backups (*.ldbk)|*.ldbk" };
            if (dlg.ShowDialog() == true) _vm.RestoreInstance(index, dlg.FileName);
        }
    }
}
