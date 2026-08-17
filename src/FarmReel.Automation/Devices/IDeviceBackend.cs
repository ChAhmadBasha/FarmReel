using System.Threading.Tasks;
using FarmReel.Core.Models;

namespace FarmReel.Automation.Devices
{
    /// <summary>Abstracts emulator/real-device control so the rest of the app is backend-agnostic.</summary>
    public interface IDeviceBackend
    {
        DeviceInstance Device { get; }
        AdbClient Adb { get; }

        Task<bool> EnsureStartedAsync();
        Task<bool> EnsureStoppedAsync();
        bool IsOnline { get; }

        /// <summary>Apply the stored fingerprint (MAC/IMEI/AndroidID/model/GPS/timezone) to the device.</summary>
        Task<bool> ApplyFingerprintAsync(DeviceInfo info);
        Task<string> GetEgressIpAsync();
        Task<bool> InstallApkAsync(string apkPath, string package);
    }
}
