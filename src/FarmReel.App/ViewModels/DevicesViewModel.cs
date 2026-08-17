using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;
using FarmReel.Automation.Devices;

namespace FarmReel.App.ViewModels
{
    public class DevicesViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<DeviceRow> Devices { get; } = new ObservableCollection<DeviceRow>();
        public ObservableCollection<GroupRow> Groups { get; } = new ObservableCollection<GroupRow>();

        public DevicesViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        public void Refresh()
        {
            ApplyFilter();
            Groups.Clear();
            foreach (var g in _svc.Groups.GetAll()) Groups.Add(new GroupRow { Model = g });
        }

        public async Task ScanOnlineAsync()
        {
            var online = await _svc.DeviceManager.ListAvailableAsync().ConfigureAwait(false);
            Ui.Run(() =>
            {
                foreach (var d in Devices)
                    d.Online = online.Contains(d.Model.AdbSerial) ? "online" : "offline";
                foreach (var d in Devices) d.OnPropertyChanged(nameof(DeviceRow.Online));
                Log.Info("Devices", $"ADB devices online: {online.Count}");
            });
        }

        public void AddDevice(string name, int index, string type, string serial, int cpu, int ram)
        {
            var dev = new DeviceInstance
            {
                Name = string.IsNullOrEmpty(name) ? "LD_" + index : name,
                Index = index,
                DeviceType = Enum.TryParse<DeviceType>(type, out var t) ? t : DeviceType.LDPlayer,
                AdbSerial = string.IsNullOrEmpty(serial) ? DeviceManager.SerialForIndex(index) : serial,
                Cpu = cpu > 0 ? cpu : 2,
                RamMb = ram > 0 ? ram : 2048
            };
            _svc.Devices.Save(dev);
            Refresh();
            Log.Info("Devices", $"Added device {dev.Name}");
        }

        public void DeleteDevice(DeviceRow row)
        {
            if (row == null) return;
            _svc.Devices.Delete(row.Id);
            Refresh();
        }

        public void GenerateFingerprint(DeviceRow row)
        {
            if (row == null) return;
            var rng = new Random();
            var info = new DeviceInfo
            {
                Mac = string.Join(":", Enumerable.Range(0, 6).Select(_ => rng.Next(0, 256).ToString("X2"))),
                Imei = string.Join("", Enumerable.Range(0, 15).Select(_ => rng.Next(0, 10))),
                Imsi = string.Join("", Enumerable.Range(0, 15).Select(_ => rng.Next(0, 10))),
                SimSerial = string.Join("", Enumerable.Range(0, 19).Select(_ => rng.Next(0, 10))),
                AndroidId = Guid.NewGuid().ToString("N").Substring(0, 16),
                Model = new[] { "SM-G991B", "Pixel 7", "Redmi Note 11", "OPPO A54", "Vivo Y21" }[rng.Next(5)],
                Manufacturer = new[] { "samsung", "google", "Xiaomi", "OPPO", "vivo" }[rng.Next(5)]
            };
            row.Model.DeviceInfoJson = JsonSerializer.Serialize(info);
            _svc.Devices.Save(row.Model);
            Log.Info("Devices", $"Generated fingerprint for {row.Name}");
        }

        public async Task SyncGpsTimezoneAsync(DeviceRow row)
        {
            if (row == null) return;
            var ip = await _svc.AccountService.RecordIpAsync(row.Model).ConfigureAwait(false);
            Ui.Run(() => { row.LastIp = ip; row.OnPropertyChanged(nameof(DeviceRow.LastIp)); });
        }

        public void SetGps(DeviceRow row, string lat, string lng, string tz)
        {
            if (row == null) return;
            DeviceInfo info;
            try { info = JsonSerializer.Deserialize<DeviceInfo>(row.Model.DeviceInfoJson) ?? new DeviceInfo(); }
            catch { info = new DeviceInfo(); }
            if (!string.IsNullOrEmpty(lat)) info.GpsLat = lat;
            if (!string.IsNullOrEmpty(lng)) info.GpsLng = lng;
            if (!string.IsNullOrEmpty(tz)) info.Timezone = tz;
            row.Model.DeviceInfoJson = JsonSerializer.Serialize(info);
            _svc.Devices.Save(row.Model);
        }

        public async Task InstallApkAsync(DeviceRow row, string apkPath, string package)
        {
            if (row == null || string.IsNullOrEmpty(apkPath)) return;
            var backend = _svc.DeviceManager.GetBackend(row.Model);
            var ok = await backend.InstallApkAsync(apkPath, package).ConfigureAwait(false);
            Log.Info("Devices", $"Install {Path.GetFileName(apkPath)} on {row.Name}: {(ok ? "OK" : "FAILED")}");
        }

        public async Task StartDeviceAsync(DeviceRow row)
        {
            if (row == null) return;
            var ok = await _svc.DeviceManager.EnsureStartedAsync(row.Model).ConfigureAwait(false);
            Log.Info("Devices", $"Start {row.Name}: {(ok ? "OK" : "FAILED")}");
        }

        public async Task StopDeviceAsync(DeviceRow row)
        {
            if (row == null) return;
            await _svc.DeviceManager.EnsureStoppedAsync(row.Model).ConfigureAwait(false);
        }

        public void ArrangeWindows(int columns)
        {
            var windows = WindowArranger.FindEmulatorWindows("LDPlayer", "MuMu");
            WindowArranger.Arrange(windows, 0, columns);
            Log.Info("Devices", $"Arranged {windows.Count} emulator windows");
        }

        public void ImportVpnProfile(string ovpnPath, string profileName)
        {
            try
            {
                _svc.Vpn.ImportProfile(ovpnPath, profileName);
                Log.Info("Devices", "VPN profile imported: " + profileName);
            }
            catch (Exception ex) { Log.Error("Devices", ex); }
        }

        public void AssignVpn(DeviceRow row, string profileName)
        {
            if (row == null) return;
            row.Model.VpnProfile = profileName ?? "";
            _svc.Devices.Save(row.Model);
        }

        public void SetProxy(DeviceRow row, string proxy)
        {
            if (row == null) return;
            row.Model.Proxy = proxy ?? "";
            _svc.Devices.Save(row.Model);
        }

        public void BackupDevices(string backupDir)
        {
            if (string.IsNullOrEmpty(backupDir)) return;
            // Device config backup (JSON manifest); emulator VMs use ldconsole backup
            var manifest = JsonSerializer.Serialize(Devices.Select(d => d.Model).ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(backupDir, $"devices_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            Directory.CreateDirectory(backupDir);
            File.WriteAllText(path, manifest);
            Log.Info("Devices", "Device manifest backed up to " + path);
        }

        // ---- Time Change Key (metered device-time spoofing) ----

        public async Task<bool> ChangeDeviceTimeAsync(DeviceRow row, int hoursOffset)
        {
            if (row == null) return false;
            var (ok, remaining) = await _svc.License.ConsumeTimeChangeKeyAsync().ConfigureAwait(false);
            if (!ok)
            {
                Ui.Run(() => Log.Warn("Devices", $"No Time Change Keys left (plan: {_svc.License.Info.Plan})"));
                return false;
            }
            try
            {
                var target = DateTime.Now.AddHours(hoursOffset);
                var fmt = target.ToString("MMddHHmmyyyy");
                var backend = _svc.DeviceManager.GetBackend(row.Model);
                var result = backend.Adb.Shell($"su -c \"date {fmt}\"");
                if (result.code != 0)
                    backend.Adb.Shell($"date {fmt}"); // non-root fallback
                backend.Adb.Shell("settings put global auto_time 0");
                Log.Info("Devices", $"Time changed on {row.Name} to {target:yyyy-MM-dd HH:mm} ({hoursOffset:+0}h); keys left: {remaining}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Devices", "Time change failed: " + ex.Message);
                return false;
            }
        }

        public async Task ClearFbDataAsync(DeviceRow row)
        {
            if (row == null) return;
            var backend = _svc.DeviceManager.GetBackend(row.Model);
            await backend.Adb.ClearAppDataAsync(row.Model.PackageName).ConfigureAwait(false);
            Log.Info("Devices", $"Cleared app data for {row.Model.PackageName} on {row.Name}");
        }

        // ---- LDPlayer instance management ----

        public (bool ok, string msg) CopyInstance(string name, int copyFromIndex)
        {
            var (ok, msg) = _svc.DeviceManager.CreateInstance(name, copyFromIndex);
            Log.Info("Devices", $"Copy LD '{name}' from {copyFromIndex}: {(ok ? "OK" : msg)}");
            return (ok, msg);
        }

        public (bool ok, string msg) RemoveInstance(int index)
        {
            var (ok, msg) = _svc.DeviceManager.RemoveInstance(index);
            Log.Info("Devices", $"Remove LD index {index}: {(ok ? "OK" : msg)}");
            return (ok, msg);
        }

        public (bool ok, string msg) BackupInstance(int index, string filePath)
        {
            var (ok, msg) = _svc.DeviceManager.BackupInstance(index, filePath);
            Log.Info("Devices", $"Backup LD {index}: {(ok ? "OK" : msg)}");
            return (ok, msg);
        }

        public (bool ok, string msg) RestoreInstance(int index, string filePath)
        {
            var (ok, msg) = _svc.DeviceManager.RestoreInstance(index, filePath);
            Log.Info("Devices", $"Restore LD {index}: {(ok ? "OK" : msg)}");
            return (ok, msg);
        }

        public (bool ok, string msg) ToggleBridge(DeviceRow row, bool enabled)
        {
            if (row == null) return (false, "no device selected");
            row.Model.NetworkBridge = enabled;
            _svc.Devices.Save(row.Model);
            var (ok, msg) = _svc.DeviceManager.SetNetworkBridge(row.Model.Index, enabled);
            Log.Info("Devices", $"Network bridge {row.Name} = {enabled}: {(ok ? "OK" : msg)}");
            return (ok, msg);
        }

        // ---- Search / filter ----

        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set
            {
                _filter = value ?? "";
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            Devices.Clear();
            var f = _filter.Trim().ToLowerInvariant();
            foreach (var d in _svc.Devices.GetAll())
            {
                if (f.Length == 0 ||
                    d.Name.ToLowerInvariant().Contains(f) ||
                    d.AdbSerial.ToLowerInvariant().Contains(f) ||
                    d.PackageName.ToLowerInvariant().Contains(f) ||
                    d.DeviceType.ToString().ToLowerInvariant().Contains(f))
                    Devices.Add(new DeviceRow { Model = d });
            }
        }
    }
}
