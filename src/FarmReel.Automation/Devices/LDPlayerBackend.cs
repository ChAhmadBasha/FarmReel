using System;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Automation.Devices
{
    /// <summary>Controls an LDPlayer instance via ldconsole.exe + ADB.</summary>
    public class LDPlayerBackend : IDeviceBackend
    {
        private readonly string _consolePath;

        public DeviceInstance Device { get; }
        public AdbClient Adb { get; }

        public LDPlayerBackend(DeviceInstance device, string consolePath, string adbPath)
        {
            Device = device;
            _consolePath = string.IsNullOrEmpty(consolePath) ? "ldconsole.exe" : consolePath;
            Adb = new AdbClient(adbPath, device.AdbSerial);
        }

        private string Console(string args, int timeoutMs = 120000)
        {
            var (_, output, _) = ProcessRunner.Run(_consolePath, args, timeoutMs);
            return output;
        }

        public async Task<bool> EnsureStartedAsync()
        {
            if (Adb.IsOnline()) return true;
            var index = Math.Max(0, Device.Index);
            Console($"launch --index {index}");
            // wait for boot: adb online + package visible
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(2000).ConfigureAwait(false);
                if (Adb.IsOnline() && Adb.IsPackageInstalled(Device.PackageName))
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                    return true;
                }
            }
            Log.Warn("Device", $"LDPlayer {Device.Name} did not become ready in time", Device.Name);
            return false;
        }

        public Task<bool> EnsureStoppedAsync()
        {
            Console($"quit --index {Math.Max(0, Device.Index)}");
            return Task.FromResult(true);
        }

        public bool IsOnline => Adb.IsOnline();

        public async Task<bool> ApplyFingerprintAsync(DeviceInfo info)
        {
            try
            {
                var index = Math.Max(0, Device.Index);
                var args =
                    $"modify --index {index}" +
                    (string.IsNullOrEmpty(info.Mac) ? "" : $" --mac {info.Mac}") +
                    (string.IsNullOrEmpty(info.Imei) ? "" : $" --imei {info.Imei}") +
                    (string.IsNullOrEmpty(info.Imsi) ? "" : $" --imsi {info.Imsi}") +
                    (string.IsNullOrEmpty(info.SimSerial) ? "" : $" --simserial {info.SimSerial}") +
                    (string.IsNullOrEmpty(info.AndroidId) ? "" : $" --androidid {info.AndroidId}") +
                    (string.IsNullOrEmpty(info.Model) ? "" : $" --model {info.Model}") +
                    (string.IsNullOrEmpty(info.Manufacturer) ? "" : $" --manufacturer {info.Manufacturer}");
                if (args.Length > "modify --index ".Length + 2)
                    Console(args);

                if (!string.IsNullOrEmpty(info.Timezone))
                    await Adb.SetTimeZoneAsync(info.Timezone).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.GpsLat))
                    await Adb.SetGpsAsync(info.GpsLat, info.GpsLng).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Device", $"Fingerprint apply failed: {ex.Message}", Device.Name);
                return false;
            }
        }

        public Task<string> GetEgressIpAsync() => Task.Run(() => Adb.GetIp());

        public async Task<bool> InstallApkAsync(string apkPath, string package)
        {
            try
            {
                if (!string.IsNullOrEmpty(apkPath) && System.IO.File.Exists(apkPath))
                {
                    await Adb.InstallApkAsync(apkPath).ConfigureAwait(false);
                    return Adb.IsPackageInstalled(package);
                }
                // fallback to ldconsole installapp
                Console($"installapp --index {Math.Max(0, Device.Index)} --filename {ProcessRunner.Quote(apkPath)}");
                return Adb.IsPackageInstalled(package);
            }
            catch (Exception ex)
            {
                Log.Warn("Device", $"APK install failed: {ex.Message}", Device.Name);
                return false;
            }
        }
    }
}
