using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FarmReel.Core.Flow;
using FarmReel.Automation.Devices;
using FarmReel.Automation.Elements;
using FarmReel.Automation.Vision;
using FarmReel.Core.Utils;

namespace FarmReel.Automation
{
    /// <summary>Implements the flow primitives on a real device via ADB + vision.</summary>
    public class FlowHost : IFlowHost
    {
        private readonly IDeviceBackend _backend;
        private readonly ElementFinder _finder;
        private readonly string _tempDir;

        public FlowHost(IDeviceBackend backend, IOcrEngine ocr, string tempDir)
        {
            _backend = backend;
            _tempDir = tempDir ?? Path.GetTempPath();
            Directory.CreateDirectory(_tempDir);
            _finder = new ElementFinder(backend, path => ocr.Recognize(path));
        }

        private string Shot()
        {
            var path = Path.Combine(_tempDir, $"shot_{Guid.NewGuid():N}.png");
            return _backend.Adb.SaveScreenshot(path) ? path : "";
        }

        public Task<bool> StartAppAsync(string package, bool fresh)
        {
            return Task.Run(async () =>
            {
                if (fresh)
                {
                    await _backend.Adb.ForceStopAsync(package).ConfigureAwait(false);
                    await _backend.Adb.ClearAppDataAsync(package).ConfigureAwait(false);
                }
                else
                {
                    await _backend.Adb.ForceStopAsync(package).ConfigureAwait(false);
                }
                await Task.Delay(800).ConfigureAwait(false);
                await _backend.Adb.StartAppAsync(package).ConfigureAwait(false);
                await Task.Delay(2500).ConfigureAwait(false);
                return true;
            });
        }

        public Task ForceStopAsync(string package) => _backend.Adb.ForceStopAsync(package);
        public Task ClearAppDataAsync(string package) => _backend.Adb.ClearAppDataAsync(package);

        public async Task<string> GetScreenTextAsync()
        {
            var sb = new System.Text.StringBuilder();
            var xml = _backend.Adb.DumpUi();
            foreach (var m in _finder.FindInXml(xml, new ElementMatcher { Any = true }))
                sb.AppendLine(m.Text);
            return sb.ToString();
        }

        public async Task<ElementMatch> FindElementAsync(ElementMatcher matcher, int timeoutSec)
        {
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, timeoutSec));
            while (DateTime.UtcNow < deadline)
            {
                var shot = Shot();
                var found = _finder.Find(matcher, shot);
                if (found.Found) return found;
                await Task.Delay(1500).ConfigureAwait(false);
            }
            return new ElementMatch { Found = false };
        }

        public async Task<bool> TapAsync(ElementMatcher matcher, int x, int y)
        {
            if (matcher != null)
            {
                var found = await FindElementAsync(matcher, 15).ConfigureAwait(false);
                if (!found.Found) return false;
                await _backend.Adb.TapAsync(found.X, found.Y).ConfigureAwait(false);
                return true;
            }
            await _backend.Adb.TapAsync(x, y).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> TapIfAsync(ElementMatcher matcher, int timeoutSec)
        {
            var found = await FindElementAsync(matcher, Math.Min(timeoutSec, 8)).ConfigureAwait(false);
            if (!found.Found) return false;
            await _backend.Adb.TapAsync(found.X, found.Y).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> LongPressAsync(ElementMatcher matcher)
        {
            var found = await FindElementAsync(matcher, 15).ConfigureAwait(false);
            if (!found.Found) return false;
            await _backend.Adb.SwipeAsync(found.X, found.Y, found.X, found.Y, 800).ConfigureAwait(false);
            return true;
        }

        public Task SwipeAsync(int x1, int y1, int x2, int y2, int durationMs)
            => _backend.Adb.SwipeAsync(x1, y1, x2, y2, durationMs);

        public async Task<bool> ScrollToAsync(ElementMatcher matcher, string direction, int maxScrolls)
        {
            for (int i = 0; i < maxScrolls; i++)
            {
                var shot = Shot();
                var found = _finder.Find(matcher, shot);
                if (found.Found) return true;
                int w = 1080, h = 1920;
                using (var bmp = ScreenService.Load(shot)) { if (bmp != null) { w = bmp.Width; h = bmp.Height; } }
                switch (direction?.ToLowerInvariant())
                {
                    case "up": await _backend.Adb.SwipeAsync(w / 2, h / 3, w / 2, h * 2 / 3, 400).ConfigureAwait(false); break;
                    case "left": await _backend.Adb.SwipeAsync(w * 2 / 3, h / 2, w / 3, h / 2, 400).ConfigureAwait(false); break;
                    default: await _backend.Adb.SwipeAsync(w / 2, h * 2 / 3, w / 2, h / 3, 400).ConfigureAwait(false); break;
                }
                await Task.Delay(800).ConfigureAwait(false);
            }
            return false;
        }

        public async Task<bool> TypeAsync(ElementMatcher matcher, string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            if (matcher != null)
            {
                var found = await FindElementAsync(matcher, 15).ConfigureAwait(false);
                if (!found.Found) return false;
                await _backend.Adb.TapAsync(found.X, found.Y).ConfigureAwait(false);
            }
            await Task.Delay(600).ConfigureAwait(false);
            await _backend.Adb.InputTextAsync(text).ConfigureAwait(false);
            return true;
        }

        public Task<bool> VerifyAsync(ElementMatcher matcher, int timeoutSec)
        {
            var m = matcher ?? new ElementMatcher();
            return Task.Run(async () =>
            {
                var found = await FindElementAsync(m, timeoutSec).ConfigureAwait(false);
                return found.Found;
            });
        }

        public Task BackAsync() => _backend.Adb.KeyEventAsync(4);

        public async Task<bool> PullAppDataAsync(string package, string destDir)
        {
            if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(destDir)) return false;
            try
            {
                Directory.CreateDirectory(destDir);
                // Android 11+ scoped storage: app-private files under /sdcard/Android/data/<pkg>
                var result = _backend.Adb.Raw(
                    $"pull /sdcard/Android/data/{package}/files {ProcessRunner.Quote(destDir)}", 180000);
                if (result.code != 0)
                {
                    // fallback: /data/data requires root
                    result = _backend.Adb.Raw(
                        $"pull /data/data/{package} {ProcessRunner.Quote(destDir)}", 180000);
                }
                return result.code == 0;
            }
            catch (Exception ex)
            {
                Log.Warn("Flow", "Profile backup failed: " + ex.Message);
                return false;
            }
        }

        public async Task OpenLinkAsync(string uri)
        {
            _backend.Adb.Shell($"am start -a android.intent.action.VIEW -d \"{uri}\"");
            await Task.Delay(2500).ConfigureAwait(false);
        }

        public async Task<bool> SelectFilesAsync(IReadOnlyList<string> fileNames, string mediaType)
        {
            // Gallery selection: FB gallery is a grid. Best-effort: tap cells from top-left.
            // Selector positions must be validated per FB version on real devices.
            var count = Math.Max(1, fileNames.Count);
            int cols = 3;
            int cellW = 1080 / cols;
            int cellH = cellW;
            int startY = 600; // below the header
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                await _backend.Adb.TapAsync(col * cellW + cellW / 2, startY + row * cellH + cellH / 2)
                    .ConfigureAwait(false);
                await Task.Delay(500).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> CommentOnPostAsync(string text, string photoPath)
        {
            // Tap the comment field (top of the comment sheet), type, send.
            await _backend.Adb.TapAsync(540, 500).ConfigureAwait(false);
            await Task.Delay(800).ConfigureAwait(false);
            await _backend.Adb.InputTextAsync(text ?? "").ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);
            await _backend.Adb.KeyEventAsync(66).ConfigureAwait(false); // enter
            return true;
        }

        public Task<string> CaptureScreenshotAsync(string savePath)
        {
            var path = string.IsNullOrEmpty(savePath)
                ? Path.Combine(_tempDir, $"shot_{Guid.NewGuid():N}.png")
                : savePath;
            return Task.FromResult(_backend.Adb.SaveScreenshot(path) ? path : "");
        }

        public async Task<bool> IsScreenTextPresentAsync(string regex, int timeoutSec)
        {
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, timeoutSec));
            while (DateTime.UtcNow < deadline)
            {
                var shot = Shot();
                if (_finder.IsTextPresent(regex, shot, isRegex: true)) return true;
                await Task.Delay(1200).ConfigureAwait(false);
            }
            return false;
        }
    }
}
