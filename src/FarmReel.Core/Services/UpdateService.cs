using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>
    /// Update channel: fetches a version manifest from the license/update server,
    /// downloads the new single-file build, verifies its SHA-256, and applies it
    /// via a small helper script (the app must replace its own exe).
    /// </summary>
    public class UpdateService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _http;

        public UpdateService(SettingsService settings)
        {
            _settings = settings;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public string CurrentVersion => VersionInfo.Version;

        public string UpdateUrl => _settings.Get("update_url", "");

        public async Task<(bool available, string version, string url, string sha256, string notes)> CheckAsync()
        {
            var baseUrl = UpdateUrl;
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = _settings.LicenseServer; // fall back to license server
            if (string.IsNullOrEmpty(baseUrl))
                return (false, "", "", "", "");

            try
            {
                var manifestUrl = baseUrl.TrimEnd('/') + "/api/update/manifest";
                var json = await _http.GetStringAsync(manifestUrl).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                var url = root.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var sha = root.TryGetProperty("sha256", out var s) ? s.GetString() ?? "" : "";
                var notes = root.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(url)) url = manifestUrl.Replace("/manifest", "/download");
                var available = !string.IsNullOrEmpty(version) &&
                                !string.Equals(version, CurrentVersion, StringComparison.OrdinalIgnoreCase);
                return (available, version, url, sha, notes);
            }
            catch (Exception ex)
            {
                Log.Warn("Update", "Check failed: " + ex.Message);
                return (false, "", "", "", "");
            }
        }

        /// <summary>Download the update and verify its SHA-256. Returns the local file path.</summary>
        public async Task<string> DownloadAsync(string url, string sha256)
        {
            var dir = Path.Combine(_settings.AppDataDir, "updates");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "FarmReel.exe");

            var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(sha256))
            {
                using var sha = SHA256.Create();
                var hash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
                if (!string.Equals(hash, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error("Update", $"Hash mismatch (got {hash}, expected {sha256}) - refusing to install");
                    return "";
                }
            }
            File.WriteAllBytes(path, bytes);
            Log.Info("Update", $"Downloaded update ({bytes.Length} bytes) to {path}");
            return path;
        }

        /// <summary>Apply the update: kill the app, replace the exe, relaunch.</summary>
        public void Apply(string newExePath)
        {
            if (string.IsNullOrEmpty(newExePath) || !File.Exists(newExePath)) return;
            var appDir = Path.GetDirectoryName(Environment.ProcessPath)
                         ?? Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location)
                         ?? "";
            var targetExe = Path.Combine(appDir, "FarmReel.exe");
            var cmd = Path.Combine(Path.GetTempPath(), "farmreel_update.cmd");
            var content =
                "@echo off\r\n" +
                "timeout /t 3 /nobreak >nul\r\n" +
                "taskkill /im FarmReel.exe /f >nul 2>&1\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                $"copy /Y \"{newExePath}\" \"{targetExe}\" >nul\r\n" +
                $"start \"\" \"{targetExe}\"\r\n" +
                "del \"%~f0\"";
            File.WriteAllText(cmd, content);
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe")
                {
                    Arguments = $"/c \"{cmd}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                });
            }
            catch (Exception ex)
            {
                Log.Error("Update", "Apply failed: " + ex.Message);
            }
        }
    }
}
