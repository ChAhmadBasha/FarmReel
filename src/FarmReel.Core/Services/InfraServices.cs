using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>IP geolocation lookup + timezone mapping.</summary>
    public class IpGeoService
    {
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public class GeoInfo
        {
            public string Ip { get; set; } = "";
            public string Country { get; set; } = "";
            public string CountryCode { get; set; } = "";
            public string City { get; set; } = "";
            public string Lat { get; set; } = "";
            public string Lng { get; set; } = "";
            public string Timezone { get; set; } = "";
        }

        public async Task<GeoInfo> LookupAsync()
        {
            try
            {
                var body = await _http.GetStringAsync("https://ipapi.co/json/").ConfigureAwait(false);
                return Parse(body);
            }
            catch (Exception ex)
            {
                Log.Warn("IpGeo", "Lookup failed: " + ex.Message);
                return new GeoInfo();
            }
        }

        /// <summary>Geolocate a specific IP (used for per-instance egress IPs).</summary>
        public async Task<GeoInfo> LookupIpAsync(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return new GeoInfo();
            try
            {
                var body = await _http.GetStringAsync("https://ipapi.co/" + Uri.EscapeDataString(ip.Trim()) + "/json/")
                    .ConfigureAwait(false);
                return Parse(body);
            }
            catch (Exception ex)
            {
                Log.Warn("IpGeo", $"Lookup for {ip} failed: {ex.Message}");
                return new GeoInfo();
            }
        }

        private static GeoInfo Parse(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            return new GeoInfo
            {
                Ip = r.TryGetProperty("ip", out var ip) ? ip.GetString() ?? "" : "",
                Country = r.TryGetProperty("country_name", out var cn) ? cn.GetString() ?? "" : "",
                CountryCode = r.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "" : "",
                City = r.TryGetProperty("city", out var ci) ? ci.GetString() ?? "" : "",
                Lat = r.TryGetProperty("latitude", out var la) ? la.GetRawText() : "",
                Lng = r.TryGetProperty("longitude", out var lo) ? lo.GetRawText() : "",
                Timezone = r.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? "" : ""
            };
        }
    }

    /// <summary>ffprobe-based video codec validation + optional ffmpeg transcode.</summary>
    public class VideoCheckService
    {
        public class VideoInfo
        {
            public bool Parsed { get; set; }
            public double DurationSec { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string VideoCodec { get; set; } = "";
            public string AudioCodec { get; set; } = "";
            public bool IsSupported { get; set; }
            public string Reason { get; set; } = "";
        }

        private readonly SettingsService _settings;

        public VideoCheckService(SettingsService settings) { _settings = settings; }

        public VideoInfo Probe(string filePath)
        {
            var info = new VideoInfo();
            var ffprobe = _settings.FfprobePath;
            if (string.IsNullOrEmpty(ffprobe) || !File.Exists(ffprobe)) return info;
            try
            {
                var psi = new ProcessStartInfo(ffprobe)
                {
                    Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit(15000);
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                info.Parsed = true;
                if (root.TryGetProperty("format", out var fmt))
                {
                    if (fmt.TryGetProperty("duration", out var dur))
                        double.TryParse(dur.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out info.DurationSec);
                }
                if (root.TryGetProperty("streams", out var streams))
                {
                    foreach (var s in streams.EnumerateArray())
                    {
                        var type = s.TryGetProperty("codec_type", out var ct) ? ct.GetString() : "";
                        var codec = s.TryGetProperty("codec_name", out var cn) ? cn.GetString() : "";
                        if (type == "video")
                        {
                            info.VideoCodec = codec;
                            if (s.TryGetProperty("width", out var w)) info.Width = w.GetInt32();
                            if (s.TryGetProperty("height", out var h)) info.Height = h.GetInt32();
                        }
                        else if (type == "audio") info.AudioCodec = codec;
                    }
                }
                var okVideo = info.VideoCodec is "h264" or "hevc" or "h265" or "vp9" or "av1" or "mpeg4";
                var okAudio = string.IsNullOrEmpty(info.AudioCodec) || info.AudioCodec is "aac" or "mp3" or "opus";
                info.IsSupported = okVideo && okAudio;
                info.Reason = info.IsSupported ? "" : $"Unsupported codec: {info.VideoCodec}/{info.AudioCodec}";
            }
            catch (Exception ex)
            {
                info.Reason = "ffprobe error: " + ex.Message;
            }
            return info;
        }

        /// <summary>Transcode to H.264 + AAC (LDPlayer-safe). Returns output path.</summary>
        public async Task<string> TranscodeAsync(string filePath, string outputDir)
        {
            var ffmpeg = _settings.FfprobePath.Replace("ffprobe", "ffmpeg");
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException("ffmpeg not found next to ffprobe");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var output = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(filePath) + "_h264.mp4");
            var psi = new ProcessStartInfo(ffmpeg)
            {
                Arguments = $"-y -i \"{filePath}\" -c:v libx264 -preset veryfast -c:a aac -movflags +faststart \"{output}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            await Task.WhenAll(
                p?.StandardOutput.ReadToEndAsync() ?? Task.FromResult(""),
                p?.StandardError.ReadToEndAsync() ?? Task.FromResult(""));
            p?.WaitForExit();
            return File.Exists(output) ? output : "";
        }
    }

    /// <summary>Daily DB backup + profiles/cookies/groups backup.</summary>
    public class BackupService
    {
        private readonly SettingsService _settings;

        public BackupService(SettingsService settings) { _settings = settings; }

        public void BackupDatabase()
        {
            try
            {
                var dir = Path.Combine(_settings.BackupDir, "db");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, $"farmreel_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(Db.DatabasePath, dest, overwrite: true);
                // retention: keep 30 days
                foreach (var f in Directory.GetFiles(dir, "farmreel_*.db"))
                {
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-30)) File.Delete(f);
                }
                Log.Info("Backup", "Database backed up: " + dest);
            }
            catch (Exception ex) { Log.Warn("Backup", "DB backup failed: " + ex.Message); }
        }

        public void BackupProfiles(IEnumerable<Models.Account> accounts)
        {
            try
            {
                var dir = Path.Combine(_settings.BackupDir, "profiles");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                foreach (var a in accounts)
                {
                    var src = Path.Combine(_settings.ProfilesDir, a.Uid);
                    if (Directory.Exists(src))
                        CopyDirectory(src, Path.Combine(dir, a.Uid));
                }
            }
            catch (Exception ex) { Log.Warn("Backup", "Profiles backup failed: " + ex.Message); }
        }

        public void BackupGroups(IEnumerable<Models.LdGroup> groups)
        {
            try
            {
                var file = Path.Combine(_settings.BackupDir, $"groups_{DateTime.Now:yyyyMMdd}.json");
                var json = JsonSerializer.Serialize(groups);
                File.WriteAllText(file, json);
            }
            catch (Exception ex) { Log.Warn("Backup", "Groups backup failed: " + ex.Message); }
        }

        public static void CopyDirectory(string source, string dest)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(source))
                File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(source))
                CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
        }
    }

    /// <summary>Keyword-based classification of Facebook threat screens (282, checkpoint, ban, temp-lock).</summary>
    public static class ThreatClassifier
    {
        public static string Classify(string screenText)
        {
            if (string.IsNullOrEmpty(screenText)) return "unknown";
            var t = screenText.ToLowerInvariant();
            if (t.Contains("you can't use this feature right now") ||
                t.Contains("you cannot use this feature") ||
                t.Contains("temporarily blocked") && t.Contains("282")) return "locked282";
            if (t.Contains("unusual activity") || t.Contains("confirm your identity") ||
                t.Contains("we noticed unusual") || t.Contains("review your account")) return "checkpoint";
            if (t.Contains("your account has been disabled") || t.Contains("account disabled") ||
                t.Contains("permanently disabled")) return "suspended";
            if (t.Contains("temporarily locked") || t.Contains("security check")) return "templock";
            if (t.Contains("we'll get back to you") || t.Contains("under review")) return "inreview";
            return "unknown";
        }
    }
}
