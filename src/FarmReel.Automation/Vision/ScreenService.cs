using System;
using System.Drawing;
using System.IO;
using FarmReel.Automation.Devices;

namespace FarmReel.Automation.Vision
{
    public static class ScreenService
    {
        public static string Capture(IDeviceBackend backend, string savePath)
        {
            var ok = backend.Adb.SaveScreenshot(savePath);
            return ok ? savePath : "";
        }

        public static Bitmap Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var fs = File.OpenRead(path);
                return new Bitmap(fs);
            }
            catch { return null; }
        }

        /// <summary>Try to locate a template image (e.g. reaction strip, AI-label toggle) on screen.</summary>
        public static bool FindTemplate(string screenshotPath, string templatePath, out int x, out int y)
        {
            x = 0; y = 0;
            using var screen = Load(screenshotPath);
            using var tpl = Load(templatePath);
            if (screen == null || tpl == null) return false;
            return TemplateMatcher.Find(screen, tpl, out x, out y);
        }
    }
}
