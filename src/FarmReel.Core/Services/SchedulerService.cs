using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>Evaluates schedules every 30s and pushes due jobs to the orchestrator queue.</summary>
    public class SchedulerService : IDisposable
    {
        private readonly PostJobRepository _postJobs;
        private readonly ScheduledTaskRepository _tasks;
        private readonly SettingsService _settings;
        private readonly BackupService _backup;
        private Timer _timer;
        private string _lastMinute = "";
        private string _lastDay = "";

        public event Action<PostJob> PostJobDue;
        public event Action<ScheduledTask> TaskDue;
        public event Action AutoStopReached;

        public SchedulerService(PostJobRepository postJobs, ScheduledTaskRepository tasks, SettingsService settings,
            BackupService backup)
        {
            _postJobs = postJobs;
            _tasks = tasks;
            _settings = settings;
            _backup = backup;
        }

        public void Start()
        {
            if (_timer != null) return; // guard against double-start
            _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Tick()
        {
            var now = DateTime.Now;
            var minuteKey = now.ToString("HH:mm");
            _lastMinute = minuteKey;

            // Daily maintenance: reset counters + optional DB backup when the day changes
            var dayKey = now.ToString("yyyy-MM-dd");
            if (dayKey != _lastDay)
            {
                _lastDay = dayKey;
                ResetDailyCounters();
                if (_settings.AutoBackupDb)
                {
                    Log.Info("Scheduler", "Daily DB backup");
                    _backup.BackupDatabase();
                }
            }

            // Auto-stop at configured time
            var autoStop = _settings.AutoStopAt;
            if (!string.IsNullOrEmpty(autoStop) && minuteKey == autoStop)
            {
                Log.Info("Scheduler", "Auto-stop time reached: " + autoStop);
                AutoStopReached?.Invoke();
            }

            // Post jobs with schedule mode Clock/Weekly (state=queued -> orchestrator workers pick up)
            foreach (var job in _postJobs.GetAll().Where(j => j.Enabled && j.ScheduleMode != ScheduleMode.WhenRun))
            {
                if (job.DailyLimit > 0 && job.PostsToday >= job.DailyLimit) continue;
                if (job.State == "running" || job.State == "queued") continue;
                if (!CronParser.Matches(job.ScheduleCron, now)) continue;
                job.State = "queued";
                job.NextRunAt = now.AddMinutes(1);
                _postJobs.Save(job);
                PostJobDue?.Invoke(job);
            }

            // Generic scheduled tasks
            foreach (var task in _tasks.GetAll().Where(t => t.Enabled))
            {
                if (!CronParser.Matches(task.Cron, now)) continue;
                TaskDue?.Invoke(task);
            }
        }

        private void ResetDailyCounters()
        {
            foreach (var job in _postJobs.GetAll())
            {
                job.PostsToday = 0;
                _postJobs.Save(job);
            }
        }

        public void Dispose() => _timer?.Dispose();
    }
}
