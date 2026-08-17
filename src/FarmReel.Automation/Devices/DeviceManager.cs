using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Services;
using FarmReel.Core.Utils;

namespace FarmReel.Automation.Devices
{
    /// <summary>Factory + coordinator for all device backends. Implements IDeviceCoordinator.</summary>
    public class DeviceManager : IDeviceCoordinator
    {
        private static readonly object _startLock = new object();
        private static DateTime _lastStartUtc = DateTime.MinValue;

        private readonly SettingsService _settings;
        private readonly VpnManager _vpn;
        private readonly IpGeoService _geo;
        private readonly Dictionary<long, IDeviceBackend> _backends = new Dictionary<long, IDeviceBackend>();

        public DeviceManager(SettingsService settings, VpnManager vpn, IpGeoService geo)
        {
            _settings = settings;
            _vpn = vpn;
            _geo = geo;
        }

        public IDeviceBackend GetBackend(DeviceInstance device)
        {
            lock (_backends)
            {
                if (_backends.TryGetValue(device.Id, out var b)) return b;
                b = device.DeviceType switch
                {
                    DeviceType.LDPlayer => (IDeviceBackend)new LDPlayerBackend(device, _settings.LdConsolePath, _settings.AdbPath),
                    DeviceType.MuMu => new MuMuBackend(device, _settings.MuMuManagerPath, _settings.AdbPath),
                    DeviceType.RealPhone => new RealDeviceBackend(device, _settings.AdbPath),
                    _ => new LDPlayerBackend(device, _settings.LdConsolePath, _settings.AdbPath)
                };
                _backends[device.Id] = b;
                return b;
            }
        }

        public async Task<bool> EnsureStartedAsync(DeviceInstance device)
        {
            // Throttle instance starts so the CPU can cool down (settings-driven)
            lock (_startLock)
            {
                var wait = _settings.WaitBetweenLdStartSec;
                if (wait > 0 && _lastStartUtc != DateTime.MinValue)
                {
                    var elapsed = (DateTime.UtcNow - _lastStartUtc).TotalSeconds;
                    if (elapsed < wait)
                        Thread.Sleep((int)((wait - elapsed) * 1000));
                }
                _lastStartUtc = DateTime.UtcNow;
            }

            var backend = GetBackend(device);
            var ok = await backend.EnsureStartedAsync().ConfigureAwait(false);
            if (!ok) return false;

            // Re-apply stored fingerprint on every run (stability matters for account safety)
            DeviceInfo info = null;
            if (!string.IsNullOrEmpty(device.DeviceInfoJson))
            {
                try { info = JsonSerializer.Deserialize<DeviceInfo>(device.DeviceInfoJson); }
                catch { }
            }
            if (info != null)
                await backend.ApplyFingerprintAsync(info).ConfigureAwait(false);

            // VPN / proxy per account
            if (!string.IsNullOrEmpty(device.VpnProfile))
                await _vpn.EnsureConnectedAsync(device).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(device.Proxy))
                await ApplyProxyAsync(device, backend).ConfigureAwait(false);

            // IP country allow/block filter
            if (_settings.CheckIp)
            {
                var ip = await backend.GetEgressIpAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(ip))
                {
                    var geo = await _geo.LookupIpAsync(ip).ConfigureAwait(false);
                    device.LastIpJson = JsonSerializer.Serialize(geo);
                    Log.Info("Device", $"Egress IP: {ip} ({geo.Country})", device.Name);

                    var filter = _settings.IpCountryFilter; // "allow" | "block" | ""
                    var list = _settings.IpCountryList.Split(',')
                        .Select(s => s.Trim().ToUpperInvariant())
                        .Where(s => s.Length > 0)
                        .ToList();
                    if (list.Count > 0)
                    {
                        var cc = geo.CountryCode.ToUpperInvariant();
                        bool inList = list.Contains(cc);
                        bool denied = (filter == "block" && inList) || (filter == "allow" && !inList);
                        if (denied)
                        {
                            Log.Warn("Device", $"IP country {geo.Country} ({cc}) denied by {filter} list - stopping {device.Name}", device.Name);
                            await backend.EnsureStoppedAsync().ConfigureAwait(false);
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public async Task<bool> EnsureStoppedAsync(DeviceInstance device)
        {
            var backend = GetBackend(device);
            return await backend.EnsureStoppedAsync().ConfigureAwait(false);
        }

        public async Task<List<string>> ListAvailableAsync()
        {
            var list = new List<string>();
            var (code, output, _) = ProcessRunner.Run(_settings.AdbPath, "devices", 10000);
            if (code == 0)
            {
                foreach (var line in output.Split('\n'))
                {
                    var t = line.Trim();
                    if (t.EndsWith("\tdevice", StringComparison.Ordinal) || t.EndsWith(" device", StringComparison.Ordinal))
                        list.Add(t.Split('\t')[0].Trim());
                }
            }
            return list;
        }

        private static async Task ApplyProxyAsync(DeviceInstance device, IDeviceBackend backend)
        {
            // format: host:port or host:port:user:pass
            var parts = device.Proxy.Split(':');
            if (parts.Length < 2) return;
            if (int.TryParse(parts[1], out var port))
                await backend.Adb.SetProxyAsync(parts[0], port).ConfigureAwait(false);
        }

        /// <summary>Compute the standard adb serial for an emulator index (best effort).</summary>
        public static string SerialForIndex(int index)
        {
            // LDPlayer: 5555 + index*2 ; MuMu: 16384 + index*32 (documented best effort)
            if (index < 0) index = 0;
            return "127.0.0.1:" + (5555 + index * 2);
        }

        // ---- LDPlayer instance management (ldconsole) ----

        /// <summary>Create a new LDPlayer instance (optionally copying from an existing one).</summary>
        public (bool ok, string msg) CreateInstance(string name, int copyFromIndex)
        {
            var console = _settings.LdConsolePath;
            if (string.IsNullOrEmpty(console)) return (false, "ldconsole path not configured");
            var (code, output, _) = ProcessRunner.Run(console,
                copyFromIndex >= 0
                    ? $"copy --from {copyFromIndex} --name {ProcessRunner.Quote(name)}"
                    : $"add --name {ProcessRunner.Quote(name)}", 180000);
            return (code == 0, code == 0 ? output : "ldconsole failed: " + output);
        }

        public (bool ok, string msg) RemoveInstance(int index)
        {
            var console = _settings.LdConsolePath;
            if (string.IsNullOrEmpty(console)) return (false, "ldconsole path not configured");
            var (code, output, _) = ProcessRunner.Run(console, $"remove --index {index}", 180000);
            return (code == 0, code == 0 ? output : "ldconsole remove failed: " + output);
        }

        public (bool ok, string msg) BackupInstance(int index, string filePath)
        {
            var console = _settings.LdConsolePath;
            if (string.IsNullOrEmpty(console)) return (false, "ldconsole path not configured");
            var (code, output, _) = ProcessRunner.Run(console,
                $"backup --index {index} --file {ProcessRunner.Quote(filePath)}", 600000);
            return (code == 0, code == 0 ? output : "ldconsole backup failed: " + output);
        }

        public (bool ok, string msg) RestoreInstance(int index, string filePath)
        {
            var console = _settings.LdConsolePath;
            if (string.IsNullOrEmpty(console)) return (false, "ldconsole path not configured");
            var (code, output, _) = ProcessRunner.Run(console,
                $"restore --index {index} --file {ProcessRunner.Quote(filePath)}", 600000);
            return (code == 0, code == 0 ? output : "ldconsole restore failed: " + output);
        }

        /// <summary>Toggle network bridging (per-instance IP). Best-effort; LDPlayer exposes
        /// this in newer console versions as `modify --network <bridge>` or via its config.</summary>
        public (bool ok, string msg) SetNetworkBridge(int index, bool enabled)
        {
            var console = _settings.LdConsolePath;
            if (string.IsNullOrEmpty(console)) return (false, "ldconsole path not configured");
            // Try the documented switch; if unsupported, log and continue (setting stored in DB).
            var (code, output, _) = ProcessRunner.Run(console,
                $"modify --index {index} --network {(enabled ? "bridge" : "nat")}", 60000);
            return (code == 0, code == 0 ? output : "network bridge modify failed (may be unsupported on this LDPlayer version): " + output);
        }
    }
}
