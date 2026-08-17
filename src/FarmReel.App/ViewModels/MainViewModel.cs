using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    public class AccountRow : ObservableObject
    {
        public Account Model { get; set; }
        public long Id => Model.Id;
        public string Name => Model.Name;
        public string Email => Model.Email;
        public string Uid => Model.Uid;
        public string Status => Model.Status.ToString();
        public string Device => Model.DeviceId > 0 ? Model.DeviceId.ToString() : "-";
        public string Phone => Model.Phone;
        public string Dob => Model.DateOfBirth;
        public bool Has2Fa => !string.IsNullOrEmpty(Model.TotpSecretEnc);
        public string Notes => Model.Notes;
    }

    public class DeviceRow : ObservableObject
    {
        public DeviceInstance Model { get; set; }
        public long Id => Model.Id;
        public string Name => Model.Name;
        public int Index => Model.Index;
        public string Type => Model.DeviceType.ToString();
        public string Serial => Model.AdbSerial;
        public string Package => Model.PackageName;
        public string Group => Model.GroupId > 0 ? Model.GroupId.ToString() : "-";
        public string CpuRam => $"{Model.Cpu}C/{Model.RamMb}MB";
        public string Vpn => Model.VpnProfile;
        public string Proxy => Model.Proxy;
        public bool Enabled => Model.Enabled;
        public string Online { get; set; } = "?";
        public string LastIp { get; set; } = "";
    }

    public class PageRow : ObservableObject
    {
        public Page Model { get; set; }
        public long Id => Model.Id;
        public string Name => Model.Name;
        public string PageId => Model.PageId;
        public long AccountId => Model.AccountId;
        public string IdentityMode => Model.IdentityMode;
        public string Status => Model.Status;
        public long Followers => Model.Followers;
        public long Reach => Model.Reach;
        public bool Monetization => Model.MonetizationEligible;
        public string MonetizationNote => Model.MonetizationNote;
        public string Notes => Model.Notes;
    }

    public class PostJobRow : ObservableObject
    {
        public PostJob Model { get; set; }
        public long Id => Model.Id;
        public string Type => Model.PostType.ToString();
        public string Page => Model.PageName;
        public bool Enabled => Model.Enabled;
        public string Folder => Model.ContentFolder;
        public int Number => Model.NumberOfPosts;
        public string Schedule => Model.ScheduleMode == ScheduleMode.WhenRun ? "When Run"
            : Model.ScheduleCron;
        public string Comment => Model.CommentEnabled ? (Model.CommentRandom ? "Random" : "Custom") : "-";
        public string State => Model.State;
        public string LastError => Model.LastError;
        public long Today => Model.PostsToday;
        public string LastFile => Model.LastFile;
    }

    public class InteractionRow : ObservableObject
    {
        public InteractionJob Model { get; set; }
        public long Id => Model.Id;
        public string Kind => Model.Kind;
        public long DeviceId => Model.DeviceId;
        public bool Enabled => Model.Enabled;
        public int MaxActions => Model.MaxActions;
        public string Delay => $"{Model.MinDelaySec}-{Model.MaxDelaySec}s";
        public string State => Model.State;
        public string LastError => Model.LastError;
    }

    public class EmailRow : ObservableObject
    {
        public EmailAccount Model { get; set; }
        public long Id => Model.Id;
        public string Email => Model.Email;
        public string Provider => Model.Provider;
        public bool Trusted => Model.IsTrusted;
        public string Notes => Model.Notes;
    }

    public class GroupRow : ObservableObject
    {
        public LdGroup Model { get; set; }
        public long Id => Model.Id;
        public string Name => Model.Name;
        public string Notes => Model.Notes;
        public long DeviceCount { get; set; }
    }

    public class TemplateRow : ObservableObject
    {
        public Template Model { get; set; }
        public long Id => Model.Id;
        public string Name => Model.Name;
        public string Category => Model.Category;
        public string Notes => Model.Notes;
    }

    public class TaskRow : ObservableObject
    {
        public ScheduledTask Model { get; set; }
        public long Id => Model.Id;
        public string Name => Model.Name;
        public string Cron => Model.Cron;
        public string Target => Model.TargetType;
        public bool Enabled => Model.Enabled;
    }

    public class MainViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        private bool _running;
        private string _status = "Stopped";
        public ObservableCollection<LogEntry> RecentLogs { get; } = new ObservableCollection<LogEntry>();
        public ObservableCollection<AccountRow> Accounts { get; } = new ObservableCollection<AccountRow>();
        public ObservableCollection<PageRow> Pages { get; } = new ObservableCollection<PageRow>();
        public ObservableCollection<DeviceRow> Devices { get; } = new ObservableCollection<DeviceRow>();
        public ObservableCollection<PostJobRow> Posts { get; } = new ObservableCollection<PostJobRow>();

        public bool IsRunning { get => _running; set => SetProperty(ref _running, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }
        public string Summary { get; set; } = "";

        public MainViewModel(ServiceLocator svc)
        {
            _svc = svc;
            Log.EntryAdded += entry => Ui.Run(() =>
            {
                RecentLogs.Add(entry);
                while (RecentLogs.Count > 300) RecentLogs.RemoveAt(0);
            });
            Refresh();
        }

        public void Refresh()
        {
            Accounts.Clear();
            foreach (var a in _svc.Accounts.GetAll()) Accounts.Add(new AccountRow { Model = a });
            Pages.Clear();
            foreach (var p in _svc.Pages.GetAll()) Pages.Add(new PageRow { Model = p });
            Devices.Clear();
            foreach (var d in _svc.Devices.GetAll()) Devices.Add(new DeviceRow { Model = d });
            Posts.Clear();
            foreach (var p in _svc.PostJobs.GetAll()) Posts.Add(new PostJobRow { Model = p });
            var live = Accounts.Count(a => a.Status == AccountStatus.Live);
            var die = Accounts.Count(a => a.Status == AccountStatus.Die);
            Summary = $"Accounts: {Accounts.Count} (Live: {live}, Die: {die}) | Pages: {Pages.Count} | Devices: {Devices.Count} | Post jobs: {Posts.Count}";
            OnPropertyChanged(nameof(Summary));
        }

        public void ToggleRun()
        {
            if (IsRunning)
            {
                _svc.Orchestrator.Stop();
                _svc.Scheduler.Stop();
                IsRunning = false;
                Status = "Stopped";
                Log.Info("App", "Run stopped");
            }
            else
            {
                _svc.Orchestrator.Start();
                _svc.Scheduler.Start();
                IsRunning = true;
                Status = "Running";
                Log.Info("App", "Run started");
            }
        }

        /// <summary>Optional auto-start (Run at startup setting): begin automation shortly after launch.</summary>
        public async void MaybeAutoStart()
        {
            if (!_svc.Settings.RunAtStartup || IsRunning) return;
            await Task.Delay(30000);
            if (IsRunning) return;
            _svc.Orchestrator.Start();
            _svc.Scheduler.Start();
            IsRunning = true;
            Status = "Running (auto-start)";
            Log.Info("App", "Auto-start: automation began");
        }
    }
}
