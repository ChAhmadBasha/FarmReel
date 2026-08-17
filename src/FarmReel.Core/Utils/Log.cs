using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FarmReel.Core.Data;
using FarmReel.Core.Models;

namespace FarmReel.Core.Utils
{
    /// <summary>Thread-safe logger with file, database and in-app event sinks.</summary>
    public static class Log
    {
        private static readonly ConcurrentQueue<LogEntry> _pending = new ConcurrentQueue<LogEntry>();
        private static readonly object _fileLock = new object();
        public static event Action<LogEntry> EntryAdded;
        private static string _filePath = "farmreel.log";
        private static Timer _flushTimer;

        public static void Initialize(string logFile)
        {
            _filePath = logFile;
            var dir = Path.GetDirectoryName(Path.GetFullPath(logFile));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _flushTimer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        public static void Debug(string action, string message, string device = "", string account = "")
            => Write("DEBUG", action, message, device, account);

        public static void Info(string action, string message, string device = "", string account = "")
            => Write("INFO", action, message, device, account);

        public static void Warn(string action, string message, string device = "", string account = "")
            => Write("WARN", action, message, device, account);

        public static void Error(string action, string message, string device = "", string account = "")
            => Write("ERROR", action, message, device, account);

        public static void Error(string action, Exception ex, string device = "", string account = "")
            => Write("ERROR", action, ex?.Message ?? "exception", device, account);

        private static void Write(string level, string action, string message, string device, string account)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Device = device ?? "",
                Account = account ?? "",
                Action = action ?? "",
                Message = message ?? ""
            };
            _pending.Enqueue(entry);
            try
            {
                lock (_fileLock)
                {
                    File.AppendAllText(_filePath,
                        $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{level}] {action} {message}\n");
                }
            }
            catch { /* logging must never throw */ }
            EntryAdded?.Invoke(entry);
        }

        public static void Flush()
        {
            var batch = new List<LogEntry>();
            while (_pending.TryDequeue(out var e)) batch.Add(e);
            if (batch.Count == 0) return;
            try
            {
                foreach (var e in batch) new LogRepository().Insert(e);
            }
            catch { /* DB may not be ready */ }
        }
    }
}
