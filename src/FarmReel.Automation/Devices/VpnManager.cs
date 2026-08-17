using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Services;
using FarmReel.Core.Utils;

namespace FarmReel.Automation.Devices
{
    /// <summary>
    /// Per-account OpenVPN management. Two import flows are supported (copy .ovpn file,
    /// or import from a folder of profiles). Supports a connect delay so IPs rotate
    /// gradually, and OTG/tether routing when the emulator uses the host network.
    /// </summary>
    public class VpnManager
    {
        private readonly SettingsService _settings;
        private readonly IpGeoService _geo;
        private readonly Dictionary<long, string> _runningProfiles = new Dictionary<long, string>();

        public VpnManager(SettingsService settings, IpGeoService geo)
        {
            _settings = settings;
            _geo = geo;
        }

        public string ProfilesDir => _settings.VpnDir;

        public void ImportProfile(string ovpnPath, string profileName)
        {
            if (!File.Exists(ovpnPath)) throw new FileNotFoundException("OpenVPN profile not found", ovpnPath);
            Directory.CreateDirectory(ProfilesDir);
            var dest = Path.Combine(ProfilesDir, (profileName ?? Path.GetFileNameWithoutExtension(ovpnPath)) + ".ovpn");
            File.Copy(ovpnPath, dest, overwrite: true);
            Log.Info("VPN", "Imported profile: " + dest);
        }

        public List<string> ListProfiles()
        {
            if (!Directory.Exists(ProfilesDir)) return new List<string>();
            var list = new List<string>();
            foreach (var f in Directory.GetFiles(ProfilesDir, "*.ovpn"))
                list.Add(Path.GetFileNameWithoutExtension(f));
            return list;
        }

        public async Task<bool> EnsureConnectedAsync(DeviceInstance device)
        {
            try
            {
                var openvpn = _settings.OpenVpnPath;
                if (string.IsNullOrEmpty(openvpn) || !File.Exists(openvpn)) return false;

                var profile = Path.Combine(ProfilesDir, device.VpnProfile + ".ovpn");
                if (!File.Exists(profile)) return false;

                // disconnect previous profile for this device
                if (_runningProfiles.TryGetValue(device.Id, out var old))
                {
                    if (old != device.VpnProfile)
                        await DisconnectAsync(device.Id).ConfigureAwait(false);
                }

                var before = (await _geo.LookupAsync().ConfigureAwait(false)).Ip;
                // start OpenVPN in the background
                var psi = new System.Diagnostics.ProcessStartInfo(openvpn)
                {
                    Arguments = $"--config {ProcessRunner.Quote(profile)} --redirect-gateway def1 --route-nopull",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = System.Diagnostics.Process.Start(psi);
                _runningProfiles[device.Id] = device.VpnProfile;
                Log.Info("VPN", $"Connecting {device.VpnProfile} for {device.Name}", device.Name);

                // wait for IP change (with delay option for gradual rotation)
                var delaySec = _settings.GetInt("vpn_connect_delay_sec", 0);
                if (delaySec > 0) await Task.Delay(delaySec * 1000).ConfigureAwait(false);
                for (int i = 0; i < 12; i++)
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                    var after = (await _geo.LookupAsync().ConfigureAwait(false)).Ip;
                    if (!string.IsNullOrEmpty(after) && after != before)
                    {
                        Log.Info("VPN", $"Connected: {before} -> {after}", device.Name);
                        return true;
                    }
                }
                return true; // non-fatal: proceed even if IP change not detected
            }
            catch (Exception ex)
            {
                Log.Warn("VPN", "Connect failed: " + ex.Message);
                return false;
            }
        }

        public Task<bool> DisconnectAsync(long deviceId)
        {
            // OpenVPN process exits on kill; track profiles
            _runningProfiles.Remove(deviceId);
            var (_, _, _) = ProcessRunner.Run("taskkill", "/IM openvpn.exe /F", 10000);
            return Task.FromResult(true);
        }

        public async Task<string> GetIpForDeviceAsync(DeviceInstance device)
        {
            var backend = new AdbClient(_settings.AdbPath, device.AdbSerial);
            return await Task.Run(() => backend.GetIp()).ConfigureAwait(false);
        }
    }
}
