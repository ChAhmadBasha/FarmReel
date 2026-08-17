using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FarmReel.Core.Utils
{
    /// <summary>Minimal cron-like matcher supporting "HH:mm" and "dow,dow HH:mm".</summary>
    public static class CronParser
    {
        private static readonly string[] Days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

        public static bool Matches(string cron, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(cron)) return false;
            cron = cron.Trim().ToLowerInvariant();

            string timePart = cron;
            var days = new HashSet<int>();
            if (cron.Contains(' '))
            {
                var parts = cron.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                foreach (var d in parts[0].Split(','))
                {
                    if (d == "*") { for (int i = 0; i < 7; i++) days.Add(i); }
                    else
                    {
                        var idx = Array.IndexOf(Days, d);
                        if (idx >= 0) days.Add(idx);
                        else if (int.TryParse(d, out var n) && n >= 0 && n <= 6) days.Add(n);
                    }
                }
                timePart = parts[1];
            }
            else
            {
                for (int i = 0; i < 7; i++) days.Add(i);
            }

            if (days.Count > 0 && !days.Contains((int)now.DayOfWeek)) return false;

            var hm = timePart.Split(':');
            if (hm.Length != 2) return false;
            if (!int.TryParse(hm[0], out var hh)) return false;
            if (!int.TryParse(hm[1], out var mm)) return false;
            return now.Hour == hh && now.Minute == mm;
        }
    }

    public static class RetryHelper
    {
        public static async Task<T> RetryAsync<T>(Func<Task<T>> action, int attempts = 3, int delayMs = 1000)
        {
            Exception last = null;
            for (int i = 0; i < attempts; i++)
            {
                try { return await action().ConfigureAwait(false); }
                catch (Exception ex) { last = ex; }
                if (i < attempts - 1) await Task.Delay(delayMs).ConfigureAwait(false);
            }
            throw new InvalidOperationException("Retry failed", last);
        }

        public static T Retry<T>(Func<T> action, int attempts = 3, int delayMs = 1000)
        {
            Exception last = null;
            for (int i = 0; i < attempts; i++)
            {
                try { return action(); }
                catch (Exception ex) { last = ex; }
                if (i < attempts - 1) Thread.Sleep(delayMs);
            }
            throw new InvalidOperationException("Retry failed", last);
        }
    }
}
