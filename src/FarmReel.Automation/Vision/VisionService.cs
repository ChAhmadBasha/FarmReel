using System;
using System.Drawing;
using FarmReel.Core.Utils;

namespace FarmReel.Automation.Vision
{
    /// <summary>OCR abstraction: pluggable engine (external PaddleOCR/Tesseract exe) or none.</summary>
    public interface IOcrEngine
    {
        string Recognize(string imagePath);
        bool IsAvailable { get; }
    }

    public class NoOcrEngine : IOcrEngine
    {
        public string Recognize(string imagePath) => "";
        public bool IsAvailable => false;
    }

    public class ExternalOcrEngine : IOcrEngine
    {
        private readonly string _exePath;
        private readonly string _argsTemplate;

        public ExternalOcrEngine(string exePath, string argsTemplate)
        {
            _exePath = exePath;
            _argsTemplate = string.IsNullOrEmpty(argsTemplate) ? "{image}" : argsTemplate;
        }

        public bool IsAvailable => !string.IsNullOrEmpty(_exePath) && System.IO.File.Exists(_exePath);

        public string Recognize(string imagePath)
        {
            if (!IsAvailable) return "";
            try
            {
                var args = _argsTemplate.Replace("{image}", ProcessRunner.Quote(imagePath));
                var (code, output, _) = ProcessRunner.Run(_exePath, args, 60000);
                if (code != 0) return "";
                return output.Trim();
            }
            catch (Exception ex)
            {
                Log.Warn("OCR", "External OCR failed: " + ex.Message);
                return "";
            }
        }
    }

    /// <summary>Pure .NET template matching (normalized cross-correlation on grayscale).</summary>
    public static class TemplateMatcher
    {
        public static bool Find(Bitmap screen, Bitmap template, out int centerX, out int centerY, double threshold = 0.82)
        {
            centerX = 0; centerY = 0;
            if (screen == null || template == null) return false;
            if (template.Width > screen.Width || template.Height > screen.Height) return false;

            var sGray = ToGray(screen);
            var tGray = ToGray(template);
            double tMean = Mean(tGray);
            double tVar = Variance(tGray, tMean);
            if (tVar < 1e-6) return false;

            double best = double.MinValue;
            int step = Math.Max(1, Math.Min(screen.Width, screen.Height) / 500); // speed up large screens
            for (int y = 0; y <= sGray.GetLength(0) - tGray.GetLength(0); y += step)
            {
                for (int x = 0; x <= sGray.GetLength(1) - tGray.GetLength(1); x += step)
                {
                    double sum = 0, sSum = 0, sSum2 = 0;
                    int count = 0;
                    for (int ty = 0; ty < tGray.GetLength(0); ty++)
                    {
                        for (int tx = 0; tx < tGray.GetLength(1); tx++)
                        {
                            double sv = sGray[y + ty, x + tx];
                            double tv = tGray[ty, tx];
                            sum += sv * tv;
                            sSum += sv;
                            sSum2 += sv * sv;
                            count++;
                        }
                    }
                    double sMean = sSum / count;
                    double sVar = (sSum2 - sSum * sMean) / count;
                    if (sVar < 1e-6) continue;
                    double denom = Math.Sqrt(sVar * tVar * count * count);
                    if (denom < 1e-9) continue;
                    double ncc = (sum - sSum * tMean) / denom;
                    if (ncc > best) { best = ncc; centerX = x + template.Width / 2; centerY = y + template.Height / 2; }
                }
            }
            return best >= threshold;
        }

        private static double[,] ToGray(Bitmap bmp)
        {
            var g = new double[bmp.Height, bmp.Width];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    g[y, x] = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                }
            return g;
        }

        private static double Mean(double[,] a)
        {
            double s = 0;
            foreach (var v in a) s += v;
            return s / a.Length;
        }

        private static double Variance(double[,] a, double mean)
        {
            double s = 0;
            foreach (var v in a) { var d = v - mean; s += d * d; }
            return s / a.Length;
        }
    }
}
