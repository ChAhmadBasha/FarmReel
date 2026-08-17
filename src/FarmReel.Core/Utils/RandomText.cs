using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FarmReel.Core.Utils
{
    /// <summary>Random text selection with placeholder templates.</summary>
    public static class RandomText
    {
        private static readonly Random _rng = new Random();

        public static string PickLine(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "";
            var lines = File.ReadAllLines(filePath, Encoding.UTF8)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            return lines.Count == 0 ? "" : lines[_rng.Next(lines.Count)];
        }

        public static string Pick(IReadOnlyList<string> items)
        {
            return items == null || items.Count == 0 ? "" : items[_rng.Next(items.Count)];
        }

        public static string Expand(string template, string accountName = "", string pageName = "")
        {
            if (string.IsNullOrEmpty(template)) return template;
            var now = DateTime.Now;
            var sb = new StringBuilder(template);
            sb.Replace("%date%", now.ToString("MMM d, yyyy"));
            sb.Replace("%time%", now.ToString("HH:mm"));
            sb.Replace("%year%", now.Year.ToString());
            sb.Replace("%name%", accountName);
            sb.Replace("%page%", pageName);
            sb.Replace("%random_number%", _rng.Next(1, 10000).ToString());
            sb.Replace("%random%", Guid.NewGuid().ToString("N").Substring(0, 8));
            return sb.ToString();
        }

        public static string RandomComment(string fileOrText, bool random, string fallback)
        {
            if (random && !string.IsNullOrEmpty(fileOrText))
            {
                var line = PickLine(fileOrText);
                if (!string.IsNullOrEmpty(line)) return line;
            }
            return Expand(fallback);
        }

        public static int RandomDelay(int minSec, int maxSec)
        {
            if (maxSec <= minSec) return minSec;
            return _rng.Next(minSec, maxSec + 1);
        }
    }
}
