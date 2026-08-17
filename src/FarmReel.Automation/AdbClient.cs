using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FarmReel.Automation
{
    /// <summary>Thin wrapper over adb.exe for a specific device serial.</summary>
    public class AdbClient
    {
        public string AdbPath { get; }
        public string Serial { get; }

        public AdbClient(string adbPath, string serial)
        {
            AdbPath = string.IsNullOrEmpty(adbPath) ? "adb.exe" : adbPath;
            Serial = serial;
        }

        private string WithSerial(string args) => $"-s {Serial} {args}";

        public (int code, string output, string err) Shell(string command, int timeoutMs = 30000)
            => ProcessRunner.Run(AdbPath, WithSerial("shell " + command), timeoutMs);

        public (int code, string output, string err) Raw(string args, int timeoutMs = 30000)
            => ProcessRunner.Run(AdbPath, args, timeoutMs);

        public bool IsOnline()
        {
            var (code, output, _) = ProcessRunner.Run(AdbPath, "devices", 10000);
            return code == 0 && output.Contains(Serial) && output.Contains("device") &&
                   !output.Contains("offline");
        }

        public Task TapAsync(int x, int y) => Task.Run(() => Shell($"input tap {x} {y}"));

        public Task SwipeAsync(int x1, int y1, int x2, int y2, int durationMs)
            => Task.Run(() => Shell($"input swipe {x1} {y1} {x2} {y2} {durationMs}"));

        public Task InputTextAsync(string text) => Task.Run(() =>
        {
            // `input text` chokes on spaces/special chars; push via clipboard instead.
            var safe = text.Replace(" ", "%s").Replace("&", "\\&");
            Shell($"input text '{safe}'");
        });

        public Task KeyEventAsync(int keyCode) => Task.Run(() => Shell($"input keyevent {keyCode}"));

        public Task ForceStopAsync(string package) => Task.Run(() => Shell($"am force-stop {package}"));

        public Task ClearAppDataAsync(string package) => Task.Run(() => Shell($"pm clear {package}"));

        public Task StartActivityAsync(string activity) => Task.Run(() => Shell($"am start {activity}"));

        public Task StartAppAsync(string package)
            => Task.Run(() => Shell($"monkey -p {package} -c android.intent.category.LAUNCHER 1"));

        public Task InstallApkAsync(string apkPath) => Task.Run(() =>
        {
            var result = Raw($"install -r {ProcessRunner.Quote(apkPath)}", 300000);
            if (result.code != 0 || !result.output.Contains("Success"))
                Raw($"install {ProcessRunner.Quote(apkPath)}", 300000);
        });

        public bool IsPackageInstalled(string package)
        {
            var (_, output, _) = Shell($"pm path {package}");
            return output.Contains(package);
        }

        public string GetProp(string key)
        {
            var (_, output, _) = Shell($"getprop {key}");
            return output.Trim();
        }

        public Task SetPropAsync(string key, string value) => Task.Run(() => Shell($"setprop {key} {value}"));

        public Task SetProxyAsync(string host, int port) =>
            Task.Run(() => Shell($"settings put global http_proxy {host}:{port}"));

        public Task ClearProxyAsync() =>
            Task.Run(() => Shell("settings put global http_proxy :0"));

        public string DumpUi()
        {
            Shell("uiautomator dump /sdcard/window_dump.xml");
            var (_, output, _) = Shell("cat /sdcard/window_dump.xml");
            return output;
        }

        public bool SaveScreenshot(string savePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // capture to device storage, then pull (shell redirection does not
                // work with UseShellExecute=false, so we avoid `exec-out > file`)
                Shell("screencap -p /sdcard/screen.png", 30000);
                var result = Raw($"pull /sdcard/screen.png {ProcessRunner.Quote(savePath)}", 60000);
                if (result.code != 0 && !File.Exists(savePath))
                {
                    // last resort: exec-out binary to a file via a helper
                    Shell("screencap -p /sdcard/screen.png", 30000);
                    Raw($"pull /sdcard/screen.png {ProcessRunner.Quote(savePath)}", 60000);
                }
                return File.Exists(savePath) && new FileInfo(savePath).Length > 1000;
            }
            catch { return false; }
        }

        public string GetIp()
        {
            var (_, output, _) = Shell("curl -s ifconfig.me 2>/dev/null || busybox wget -qO- ifconfig.me");
            var ip = output.Trim();
            if (ip.Length > 0 && ip.Length < 64) return ip;
            return "";
        }

        public Task SetTimeZoneAsync(string tz) => Task.Run(() =>
        {
            Shell("settings put global auto_time_zone 0");
            Shell($"setprop persist.sys.timezone {tz}");
        });

        public Task SetGpsAsync(string lat, string lng) => Task.Run(() =>
        {
            // LDPlayer/MuMu virtual GPS via settings; real devices need a GPS mock app.
            Shell($"settings put secure mock_location 1");
            Shell($"appops set com.android.settings android:mock_location allow");
            Shell($"am broadcast -a com.ldmnq.gps -e lat {lat} -e lng {lng}");
            Shell($"am broadcast -a com.mumu.gps -e lat {lat} -e lng {lng}");
        });

        public List<string> ListPackages()
        {
            var (_, output, _) = Shell("pm list packages");
            var list = new List<string>();
            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("package:", StringComparison.Ordinal))
                    list.Add(t.Substring(8).Trim());
            }
            return list;
        }
    }
}
