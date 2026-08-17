using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Automation.Devices
{
    /// <summary>Controls a real Android phone over ADB (Wi-Fi `adb connect` or USB/4G tether).</summary>
    public class RealDeviceBackend : IDeviceBackend
    {
        public DeviceInstance Device { get; }
        public AdbClient Adb { get; }

        public RealDeviceBackend(DeviceInstance device, string adbPath)
        {
            Device = device;
            Adb = new AdbClient(adbPath, device.AdbSerial);
        }

        public async Task<bool> EnsureStartedAsync()
        {
            if (Adb.IsOnline()) return true;
            // try to (re)connect over Wi-Fi
            if (Device.AdbSerial.Contains(":"))
            {
                Adb.Raw($"connect {Device.AdbSerial}", 15000);
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(1500).ConfigureAwait(false);
                    if (Adb.IsOnline()) return true;
                    Adb.Raw($"connect {Device.AdbSerial}", 15000);
                }
            }
            Log.Warn("Device", $"Real device {Device.Name} not reachable (is it plugged in / Wi-Fi debugging on?)", Device.Name);
            return false;
        }

        public Task<bool> EnsureStoppedAsync() => Task.FromResult(true); // phones stay on

        public bool IsOnline => Adb.IsOnline();

        public async Task<bool> ApplyFingerprintAsync(DeviceInfo info)
        {
            try
            {
                if (!string.IsNullOrEmpty(info.Timezone))
                    await Adb.SetTimeZoneAsync(info.Timezone).ConfigureAwait(false);
                // Real devices can't change IMEI/MAC without root; log what we apply
                if (!string.IsNullOrEmpty(info.GpsLat))
                    await Adb.SetGpsAsync(info.GpsLat, info.GpsLng).ConfigureAwait(false);
                return true;
            }
            catch { return false; }
        }

        public Task<string> GetEgressIpAsync() => Task.Run(() => Adb.GetIp());

        public async Task<bool> InstallApkAsync(string apkPath, string package)
        {
            if (!System.IO.File.Exists(apkPath)) return false;
            await Adb.InstallApkAsync(apkPath).ConfigureAwait(false);
            return Adb.IsPackageInstalled(package);
        }
    }
}
