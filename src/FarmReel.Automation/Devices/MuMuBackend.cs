using System;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Automation.Devices
{
    /// <summary>
    /// Controls a MuMu (12 / global) instance via MuMuManager.exe + ADB.
    /// MuMu's console API: `MuMuManager.exe launch -i <index>`, `shutdown -i <index>`,
    /// `install -i <index> -f <apk>`, `adb -i <index>` (prints adb info).
    /// </summary>
    public class MuMuBackend : IDeviceBackend
    {
        private readonly string _managerPath;

        public DeviceInstance Device { get; }
        public AdbClient Adb { get; }

        public MuMuBackend(DeviceInstance device, string managerPath, string adbPath)
        {
            Device = device;
            _managerPath = string.IsNullOrEmpty(managerPath) ? "MuMuManager.exe" : managerPath;
            Adb = new AdbClient(adbPath, device.AdbSerial);
        }

        private string Manager(string args, int timeoutMs = 120000)
        {
            var (_, output, _) = ProcessRunner.Run(_managerPath, args, timeoutMs);
            return output;
        }

        public async Task<bool> EnsureStartedAsync()
        {
            if (Adb.IsOnline()) return true;
            var index = Math.Max(0, Device.Index);
            Manager($"launch -i {index}");
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(2000).ConfigureAwait(false);
                if (Adb.IsOnline() && Adb.IsPackageInstalled(Device.PackageName))
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                    return true;
                }
            }
            Log.Warn("Device", $"MuMu {Device.Name} did not become ready in time", Device.Name);
            return false;
        }

        public Task<bool> EnsureStoppedAsync()
        {
            Manager($"shutdown -i {Math.Max(0, Device.Index)}");
            return Task.FromResult(true);
        }

        public bool IsOnline => Adb.IsOnline();

        public async Task<bool> ApplyFingerprintAsync(DeviceInfo info)
        {
            try
            {
                if (!string.IsNullOrEmpty(info.Timezone))
                    await Adb.SetTimeZoneAsync(info.Timezone).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.GpsLat))
                    await Adb.SetGpsAsync(info.GpsLat, info.GpsLng).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.Model))
                    await Adb.SetPropAsync("ro.product.model", info.Model).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.Manufacturer))
                    await Adb.SetPropAsync("ro.product.manufacturer", info.Manufacturer).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.AndroidId))
                    await Adb.SetPropAsync("ro.boot.id", info.AndroidId).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Device", $"MuMu fingerprint apply failed: {ex.Message}", Device.Name);
                return false;
            }
        }

        public Task<string> GetEgressIpAsync() => Task.Run(() => Adb.GetIp());

        public async Task<bool> InstallApkAsync(string apkPath, string package)
        {
            try
            {
                if (System.IO.File.Exists(apkPath))
                {
                    Manager($"install -i {Math.Max(0, Device.Index)} -f {ProcessRunner.Quote(apkPath)}");
                    return Adb.IsPackageInstalled(package);
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn("Device", $"MuMu APK install failed: {ex.Message}", Device.Name);
                return false;
            }
        }
    }
}
