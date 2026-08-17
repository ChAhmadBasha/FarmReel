using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>
    /// The run engine: pulls due post/interaction jobs, boots devices (respecting
    /// batch limits and wait settings), runs the flow, updates lifecycle folders and logs.
    /// </summary>
    public class OrchestratorService
    {
        private readonly PostJobRepository _postJobs;
        private readonly InteractionJobRepository _interactionJobs;
        private readonly AccountRepository _accounts;
        private readonly PageRepository _pages;
        private readonly DeviceRepository _devices;
        private readonly SettingsService _settings;
        private readonly IFlowRunner _flowRunner;
        private readonly IDeviceCoordinator _deviceCoordinator;
        private readonly PostingService _posting;
        private readonly InteractionService _interaction;
        private readonly LicenseService _license;

        private readonly object _lock = new object();
        private readonly List<Task> _workers = new List<Task>();
        private CancellationTokenSource _cts;
        private volatile bool _running;
        private int _activeBatches;

        public bool IsRunning => _running;
        public int ActiveBatches => _activeBatches;

        public OrchestratorService(
            PostJobRepository postJobs,
            InteractionJobRepository interactionJobs,
            AccountRepository accounts,
            PageRepository pages,
            DeviceRepository devices,
            SettingsService settings,
            IFlowRunner flowRunner,
            IDeviceCoordinator deviceCoordinator,
            PostingService posting,
            InteractionService interaction,
            LicenseService license)
        {
            _postJobs = postJobs;
            _interactionJobs = interactionJobs;
            _accounts = accounts;
            _pages = pages;
            _devices = devices;
            _settings = settings;
            _flowRunner = flowRunner;
            _deviceCoordinator = deviceCoordinator;
            _posting = posting;
            _interaction = interaction;
            _license = license;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_running) return;
                _running = true;
                _cts = new CancellationTokenSource();
                var batchCount = Math.Max(1, _settings.MaxActiveLd);
                for (int i = 0; i < batchCount; i++)
                {
                    var id = i;
                    _workers.Add(Task.Run(() => WorkerLoopAsync(id, _cts.Token)));
                }
                Log.Info("Orchestrator", $"Started with {batchCount} batch workers");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_running) return;
                _running = false;
                _cts?.Cancel();
                try { Task.WaitAll(_workers.ToArray(), TimeSpan.FromSeconds(30)); } catch { }
                _workers.Clear();
                Log.Info("Orchestrator", "Stopped");
                if (_settings.ShutdownAfterFinish)
                {
                    Log.Info("Orchestrator", "ShutdownAfterFinish enabled - shutting down PC in 60s");
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(60000).ConfigureAwait(false);
                        SystemShutdown.Shutdown(30);
                    });
                }
            }
        }

        public void RunNow(PostJob job) => Task.Run(async () => await ExecutePostJobAsync(job).ConfigureAwait(false));

        public void RunNow(InteractionJob job) => Task.Run(async () => await ExecuteInteractionJobAsync(job).ConfigureAwait(false));

        private async Task WorkerLoopAsync(int workerId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var job = FindNextPostJob();
                    if (job != null)
                    {
                        await ExecutePostJobAsync(job).ConfigureAwait(false);
                        continue;
                    }
                    var interact = FindNextInteractionJob();
                    if (interact != null)
                    {
                        await ExecuteInteractionJobAsync(interact).ConfigureAwait(false);
                        continue;
                    }
                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log.Error("Orchestrator", $"Worker {workerId} error: {ex.Message}");
                    await Task.Delay(5000).ConfigureAwait(false);
                }
            }
        }

        private PostJob FindNextPostJob()
        {
            lock (_lock)
            {
                return _postJobs.GetAll().FirstOrDefault(j =>
                    j.Enabled && j.State == "queued" && (j.DailyLimit <= 0 || j.PostsToday < j.DailyLimit));
            }
        }

        private InteractionJob FindNextInteractionJob()
        {
            lock (_lock)
            {
                return _interactionJobs.GetAll().FirstOrDefault(j => j.Enabled && j.State == "queued");
            }
        }

        private async Task ExecutePostJobAsync(PostJob job)
        {
            Interlocked.Increment(ref _activeBatches);
            try
            {
                job.State = "running";
                job.LastError = "";
                _postJobs.Save(job);

                var device = _devices.Get(job.DeviceId);
                var page = _pages.Get(job.PageId);
                var account = _accounts.Get(page?.AccountId ?? 0);
                if (device == null || page == null)
                {
                    job.State = "failed";
                    job.LastError = "Device or page not found";
                    _postJobs.Save(job);
                    return;
                }

                Log.Info("Post", $"Starting {job.PostType} for {page.Name} on {device.Name}",
                    device.Name, account?.Email ?? "");

                // Status posts have no media - build text and run directly
                if (job.PostType == PostType.Status)
                {
                    await ExecuteStatusPostAsync(job, device, page, account).ConfigureAwait(false);
                    return;
                }

                // Resolve content
                var folder = job.RandomFolder && !string.IsNullOrEmpty(job.RandomFolderRoot)
                    ? PickRandomFolder(job.RandomFolderRoot)
                    : job.ContentFolder;
                var files = FileNameParser.ScanMedia(folder, includePosted: false,
                    job.FilenameFilterFirst, job.FilenameFilterLast);
                if (files.Count == 0)
                {
                    job.State = "failed";
                    job.LastError = "No media files in folder: " + folder;
                    _postJobs.Save(job);
                    return;
                }

                var posted = 0;
                foreach (var file in files.Take(Math.Max(1, job.NumberOfPosts)))
                {
                    // daily limit guard
                    if (job.DailyLimit > 0 && job.PostsToday >= job.DailyLimit) break;

                    var postFiles = FileNameParser.ResolvePostFiles(folder, file);
                    var caption = _posting.ResolveCaption(job, folder, file, account, page);
                    var comment = _posting.ResolveComment(job, folder, file, account, page);
                    var commentPhoto = job.CommentWithPhoto ? FileNameParser.CommentPhotoFor(file, folder) : "";

                    if (!await _deviceCoordinator.EnsureStartedAsync(device).ConfigureAwait(false))
                    {
                        job.State = _settings.SkipOfflineDevices ? "skipped" : "failed";
                        job.LastError = "Device failed to start: " + device.Name;
                        _postJobs.Save(job);
                        return;
                    }

                    var result = await _flowRunner.PostAsync(device, account, page, job,
                        postFiles.Count > 1 ? postFiles[0] : file, caption, comment, commentPhoto)
                        .ConfigureAwait(false);

                    if (result.Success)
                    {
                        posted++;
                        job.PostsToday++;
                        job.LastRunAt = DateTime.Now;
                        job.LastFile = file;
                        foreach (var pf in postFiles)
                        {
                            if (_settings.MoveAfterPost)
                                FileNameParser.MoveToLifecycleFolder(pf, folder, posted: true);
                        }
                        Log.Info("Post", $"Posted {Path.GetFileName(file)} -> {result.Message}", device.Name);
                    }
                    else
                    {
                        job.LastError = result.Message;
                        FileNameParser.MoveToLifecycleFolder(file, folder, posted: false);
                        Log.Warn("Post", $"Post failed: {result.Message}", device.Name);
                        break;
                    }

                    await Task.Delay(Math.Max(5, _settings.WaitAfterUploadSec) * 1000).ConfigureAwait(false);
                }

                job.State = posted > 0 ? "done" : "failed";
                _postJobs.Save(job);
            }
            catch (Exception ex)
            {
                job.State = "failed";
                job.LastError = ex.Message;
                _postJobs.Save(job);
                Log.Error("Post", ex);
            }
            finally
            {
                MaybeClearCache();
                Interlocked.Decrement(ref _activeBatches);
            }
        }

        /// <summary>Clear our own temp/screenshot cache every N runs to keep control snappy.</summary>
        private void MaybeClearCache()
        {
            try
            {
                var counter = _settings.GetInt("run_counter", 0) + 1;
                _settings.SetInt("run_counter", counter);
                var every = _settings.ClearCacheEveryRuns;
                if (every <= 0 || counter < every) return;
                _settings.SetInt("run_counter", 0);

                var tmp = Path.Combine(_settings.AppDataDir, "tmp");
                if (Directory.Exists(tmp))
                {
                    foreach (var f in Directory.GetFiles(tmp)) { try { File.Delete(f); } catch { } }
                    Log.Info("Orchestrator", $"Cleared tmp cache ({every} runs)");
                }
                var shots = Path.Combine(_settings.AppDataDir, "screenshots");
                if (Directory.Exists(shots))
                {
                    foreach (var f in Directory.GetFiles(shots))
                    {
                        try
                        {
                            if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-2)) File.Delete(f);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Orchestrator", "Cache cleanup failed: " + ex.Message);
            }
        }

        private async Task ExecuteInteractionJobAsync(InteractionJob job)
        {
            Interlocked.Increment(ref _activeBatches);
            try
            {
                job.State = "running";
                _interactionJobs.Save(job);

                var device = _devices.Get(job.DeviceId);
                var account = _accounts.Get(device?.AccountId ?? 0);
                var page = job.PageId > 0 ? _pages.Get(job.PageId) : null;
                if (device == null)
                {
                    job.State = "failed";
                    job.LastError = "Device not found";
                    _interactionJobs.Save(job);
                    return;
                }

                if (!await _deviceCoordinator.EnsureStartedAsync(device).ConfigureAwait(false))
                {
                    job.State = _settings.SkipOfflineDevices ? "skipped" : "failed";
                    job.LastError = "Device failed to start";
                    _interactionJobs.Save(job);
                    return;
                }

                var plan = _interaction.BuildPlan(job);
                foreach (var action in plan)
                {
                    if (_cts.IsCancellationRequested) break;
                    var result = await _flowRunner.InteractAsync(device, account, page, job,
                        action.Action, action.Text).ConfigureAwait(false);
                    if (result.Success && job.CommentDeleteAfterUse && action.Action == "comment")
                    {
                        // delete the just-posted comment to leave no trace
                        await _flowRunner.RunActionAsync(device, account, "delete_last_comment",
                            new Dictionary<string, string>()).ConfigureAwait(false);
                    }
                    if (job.MaxActions > 0 && plan.IndexOf(action) >= job.MaxActions) break;
                    await Task.Delay(RandomText.RandomDelay(job.MinDelaySec, job.MaxDelaySec) * 1000)
                        .ConfigureAwait(false);
                }

                job.State = "done";
                job.LastRunAt = DateTime.Now;
                _interactionJobs.Save(job);
            }
            catch (Exception ex)
            {
                job.State = "failed";
                job.LastError = ex.Message;
                _interactionJobs.Save(job);
                Log.Error("Active", ex);
            }
            finally
            {
                Interlocked.Decrement(ref _activeBatches);
            }
        }

        private static string PickRandomFolder(string root)
        {
            var dirs = Directory.GetDirectories(root);
            return dirs.Length == 0 ? root : dirs[new Random().Next(dirs.Length)];
        }

        private async Task ExecuteStatusPostAsync(PostJob job, DeviceInstance device, Page page, Account account)
        {
            try
            {
                if (!await _deviceCoordinator.EnsureStartedAsync(device).ConfigureAwait(false))
                {
                    job.State = _settings.SkipOfflineDevices ? "skipped" : "failed";
                    job.LastError = "Device failed to start: " + device.Name;
                    _postJobs.Save(job);
                    return;
                }
                var caption = _posting.BuildStatusText(job, account, page);
                var comment = _posting.ResolveComment(job, job.ContentFolder, "", account, page);
                var result = await _flowRunner.PostAsync(device, account, page, job, "", caption, comment, "")
                    .ConfigureAwait(false);
                if (result.Success)
                {
                    job.State = "done";
                    job.PostsToday++;
                    job.LastRunAt = DateTime.Now;
                }
                else
                {
                    job.State = "failed";
                    job.LastError = result.Message;
                }
                _postJobs.Save(job);
            }
            catch (Exception ex)
            {
                job.State = "failed";
                job.LastError = ex.Message;
                _postJobs.Save(job);
                Log.Error("Post", ex);
            }
        }
    }

    /// <summary>Windows shutdown helper.</summary>
    public static class SystemShutdown
    {
        public static void Shutdown(int delaySeconds = 30)
        {
            try
            {
                var psi = new ProcessStartInfo("shutdown.exe")
                {
                    Arguments = $"/s /t {Math.Max(5, delaySeconds)} /c \"FarmReel finished its work\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Warn("Shutdown", "Could not initiate shutdown: " + ex.Message);
            }
        }
    }
}
