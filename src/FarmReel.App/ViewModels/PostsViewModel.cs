using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    public class PostsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<PostJobRow> Jobs { get; } = new ObservableCollection<PostJobRow>();
        public ObservableCollection<TaskRow> Tasks { get; } = new ObservableCollection<TaskRow>();
        public ObservableCollection<TemplateRow> Templates { get; } = new ObservableCollection<TemplateRow>();

        public PostsViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set { _filter = value ?? ""; OnPropertyChanged(); Refresh(); }
        }

        public void Refresh()
        {
            Jobs.Clear();
            var f = _filter.Trim().ToLowerInvariant();
            foreach (var j in _svc.PostJobs.GetAll())
            {
                if (f.Length == 0 ||
                    j.PageName.ToLowerInvariant().Contains(f) ||
                    j.PostType.ToString().ToLowerInvariant().Contains(f) ||
                    j.ContentFolder.ToLowerInvariant().Contains(f) ||
                    j.State.ToLowerInvariant().Contains(f))
                    Jobs.Add(new PostJobRow { Model = j });
            }
            Tasks.Clear();
            foreach (var t in _svc.ScheduledTasks.GetAll()) Tasks.Add(new TaskRow { Model = t });
            Templates.Clear();
            foreach (var t in _svc.Templates.GetAll()) Templates.Add(new TemplateRow { Model = t });
        }

        public void AddJob(long pageId, long deviceId, string pageName, string postType, string folder,
            int number, string scheduleMode, string cron, int dailyLimit)
        {
            var job = new PostJob
            {
                PageId = pageId,
                DeviceId = deviceId,
                PageName = pageName ?? "",
                PostType = Enum.TryParse<PostType>(postType, out var pt) ? pt : PostType.Photo,
                ContentFolder = folder ?? "",
                NumberOfPosts = Math.Max(1, number),
                ScheduleMode = Enum.TryParse<ScheduleMode>(scheduleMode, out var sm) ? sm : ScheduleMode.WhenRun,
                ScheduleCron = cron ?? "",
                DailyLimit = dailyLimit,
                State = scheduleMode == "WhenRun" ? "idle" : "idle"
            };
            _svc.PostJobs.Save(job);
            Refresh();
        }

        public void UpdateJob(PostJobRow row, Action<PostJob> mutate)
        {
            if (row == null) return;
            mutate(row.Model);
            _svc.PostJobs.Save(row.Model);
            Refresh();
        }

        public void ToggleJob(PostJobRow row)
        {
            if (row == null) return;
            row.Model.Enabled = !row.Model.Enabled;
            _svc.PostJobs.Save(row.Model);
            Refresh();
        }

        public void ResetState(PostJobRow row)
        {
            if (row == null) return;
            row.Model.State = "idle";
            row.Model.LastError = "";
            _svc.PostJobs.Save(row.Model);
            Refresh();
        }

        public void ResetState(InteractionRow row)
        {
            if (row == null) return;
            row.Model.State = "idle";
            row.Model.LastError = "";
            _svc.InteractionJobs.Save(row.Model);
            Refresh();
        }

        public void DeleteJob(PostJobRow row)
        {
            if (row == null) return;
            _svc.PostJobs.Delete(row.Id);
            Refresh();
        }

        public void RunNow(PostJobRow row)
        {
            if (row == null) return;
            _svc.Orchestrator.RunNow(row.Model);
        }

        public void RunAllNow()
        {
            foreach (var j in _svc.PostJobs.GetAll().Where(j => j.Enabled))
                _svc.Orchestrator.RunNow(j);
        }

        public List<string> PreviewFolder(string folder, string filterFirst, string filterLast)
        {
            return FileNameParser.ScanMedia(folder, false, filterFirst, filterLast);
        }

        public void ApplyTemplate(PostJobRow row, Template template)
        {
            if (row == null || template == null) return;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(template.Json);
                var root = doc.RootElement;
                if (root.TryGetProperty("captionText", out var ct)) row.Model.CaptionText = ct.GetString() ?? "";
                if (root.TryGetProperty("commentText", out var cm)) { row.Model.CommentEnabled = true; row.Model.CommentText = cm.GetString() ?? ""; }
                if (root.TryGetProperty("hashtags", out var ht)) { row.Model.HashtagsFromFile = false; row.Model.HashtagsText = ht.GetString() ?? ""; }
                if (root.TryGetProperty("commentRandom", out var cr)) row.Model.CommentRandom = cr.GetBoolean();
                if (root.TryGetProperty("aiCaption", out var ai)) row.Model.AiCaption = ai.GetBoolean();
                if (root.TryGetProperty("dailyLimit", out var dl)) row.Model.DailyLimit = dl.GetInt32();
                if (root.TryGetProperty("audience", out var au)) row.Model.Audience = au.GetString() ?? "public";
                _svc.PostJobs.Save(row.Model);
                Refresh();
            }
            catch (Exception ex) { Log.Error("Posts", "Template apply failed: " + ex.Message); }
        }

        public void SaveTemplate(string name, string category, PostJobRow row, string notes)
        {
            if (string.IsNullOrEmpty(name) || row == null) return;
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                row.Model.CaptionText, row.Model.CommentText, row.Model.HashtagsText,
                row.Model.CommentRandom, row.Model.AiCaption, row.Model.DailyLimit, row.Model.Audience
            });
            _svc.Templates.Save(new Template { Name = name, Category = category ?? "post", Json = json, Notes = notes ?? "" });
            Refresh();
        }

        public void DeleteTemplate(TemplateRow row)
        {
            if (row == null) return;
            _svc.Templates.Delete(row.Id);
            Refresh();
        }

        public void AddTask(string name, string cron, string targetType)
        {
            _svc.ScheduledTasks.Save(new ScheduledTask { Name = name ?? "", Cron = cron ?? "", TargetType = targetType ?? "post" });
            Refresh();
        }

        public void DeleteTask(TaskRow row)
        {
            if (row == null) return;
            _svc.ScheduledTasks.Delete(row.Id);
            Refresh();
        }

        public async Task CheckVideoAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            var info = _svc.VideoCheck.Probe(filePath);
            if (info.Parsed)
                Log.Info("Posts", $"{Path.GetFileName(filePath)}: {info.Width}x{info.Height}, {info.DurationSec:0.0}s, {info.VideoCodec}/{info.AudioCodec} - {(info.IsSupported ? "supported" : info.Reason)}");
            else
                Log.Warn("Posts", $"Could not probe {filePath} - is ffprobe configured?");
        }
    }

    public class ActiveViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<InteractionRow> Jobs { get; } = new ObservableCollection<InteractionRow>();

        public ActiveViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        public void Refresh()
        {
            Jobs.Clear();
            foreach (var j in _svc.InteractionJobs.GetAll()) Jobs.Add(new InteractionRow { Model = j });
        }

        public void AddJob(long deviceId, long pageId, string kind, int maxActions, int maxLikes, int maxComments,
            int maxFollows, int minDelay, int maxDelay)
        {
            _svc.InteractionJobs.Save(new InteractionJob
            {
                DeviceId = deviceId,
                PageId = pageId,
                Kind = kind ?? "feed",
                MaxActions = maxActions,
                MaxLikes = maxLikes,
                MaxComments = maxComments,
                MaxFollows = maxFollows,
                MinDelaySec = minDelay,
                MaxDelaySec = maxDelay
            });
            Refresh();
        }

        public void UpdateJob(InteractionRow row, Action<InteractionJob> mutate)
        {
            if (row == null) return;
            mutate(row.Model);
            _svc.InteractionJobs.Save(row.Model);
            Refresh();
        }

        public void DeleteJob(InteractionRow row)
        {
            if (row == null) return;
            _svc.InteractionJobs.Delete(row.Id);
            Refresh();
        }

        public void RunNow(InteractionRow row)
        {
            if (row == null) return;
            _svc.Orchestrator.RunNow(row.Model);
        }
    }

    public class GroupsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<GroupRow> Groups { get; } = new ObservableCollection<GroupRow>();
        public ObservableCollection<DeviceRow> Devices { get; } = new ObservableCollection<DeviceRow>();

        public GroupsViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        public void Refresh()
        {
            Groups.Clear();
            foreach (var g in _svc.Groups.GetAll())
            {
                var count = _svc.Devices.GetAll().Count(d => d.GroupId == g.Id);
                Groups.Add(new GroupRow { Model = g, DeviceCount = count });
            }
            Devices.Clear();
            foreach (var d in _svc.Devices.GetAll()) Devices.Add(new DeviceRow { Model = d });
        }

        public void AddGroup(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _svc.Groups.Save(new LdGroup { Name = name });
            Refresh();
        }

        public void DeleteGroup(GroupRow row)
        {
            if (row == null) return;
            _svc.Groups.Delete(row.Id);
            Refresh();
        }

        public void AssignGroup(DeviceRow device, long groupId)
        {
            if (device == null) return;
            device.Model.GroupId = groupId;
            _svc.Devices.Save(device.Model);
            Refresh();
        }

        public void BackupGroups(string dir)
        {
            _svc.Backup.BackupGroups(_svc.Groups.GetAll());
            Log.Info("Groups", "Groups backed up");
        }
    }

    public class EmailsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<EmailRow> Emails { get; } = new ObservableCollection<EmailRow>();

        public EmailsViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        public void Refresh()
        {
            Emails.Clear();
            foreach (var e in _svc.Emails.GetAll()) Emails.Add(new EmailRow { Model = e });
        }

        public void AddEmail(string email, string password, string provider, bool trusted)
        {
            if (string.IsNullOrEmpty(email)) return;
            _svc.Emails.Save(new EmailAccount
            {
                Email = email,
                PasswordEnc = CredentialVault.Protect(password ?? ""),
                Provider = provider ?? "zoho",
                IsTrusted = trusted
            });
            Refresh();
        }

        public void DeleteEmail(EmailRow row)
        {
            if (row == null) return;
            _svc.Emails.Delete(row.Id);
            Refresh();
        }

        public async Task<string> TestGetCodeAsync(EmailRow row, string receiver)
        {
            if (row == null || string.IsNullOrEmpty(receiver)) return "";
            var code = await _svc.EmailOtp.GetCodeFromAccountAsync(row.Model, receiver, 30).ConfigureAwait(false);
            Ui.Run(() => Log.Info("Emails", $"Code for {receiver}: {(string.IsNullOrEmpty(code) ? "none (timeout)" : code)}"));
            return code;
        }
    }

    public class LogsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();
        private readonly System.Collections.Generic.List<LogEntry> _all = new System.Collections.Generic.List<LogEntry>();

        public LogsViewModel(ServiceLocator svc)
        {
            _svc = svc;
            foreach (var e in new LogRepository().GetRecent(500)) { _all.Add(e); }
            ApplyFilter("");
            Log.EntryAdded += e => Ui.Run(() =>
            {
                _all.Add(e);
                while (_all.Count > 2000) _all.RemoveAt(0); // cap memory
                ApplyFilter(_filter);
            });
        }

        private string _filter = "";
        public string Filter { get => _filter; set { _filter = value ?? ""; ApplyFilter(_filter); } }

        private void ApplyFilter(string filter)
        {
            Entries.Clear();
            var f = filter?.Trim().ToLowerInvariant() ?? "";
            foreach (var e in _all)
            {
                if (f.Length == 0 || e.Level.ToLowerInvariant().Contains(f) ||
                    e.Action.ToLowerInvariant().Contains(f) || e.Message.ToLowerInvariant().Contains(f))
                    Entries.Add(e);
            }
        }

        public void RefreshFromDb()
        {
            _all.Clear();
            foreach (var e in new LogRepository().GetRecent(500)) _all.Add(e);
            ApplyFilter(_filter);
        }
    }
}
