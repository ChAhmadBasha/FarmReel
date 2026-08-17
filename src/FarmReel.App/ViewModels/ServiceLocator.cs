using System;
using System.IO;
using FarmReel.Automation;
using FarmReel.Automation.Devices;
using FarmReel.Core.Data;
using FarmReel.Core.Services;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    /// <summary>Manual composition root. Wires the entire dependency graph.</summary>
    public class ServiceLocator : IDisposable
    {
        public SettingsService Settings { get; }
        public AccountRepository Accounts { get; }
        public PageRepository Pages { get; }
        public DeviceRepository Devices { get; }
        public PostJobRepository PostJobs { get; }
        public InteractionJobRepository InteractionJobs { get; }
        public EmailRepository Emails { get; }
        public GroupRepository Groups { get; }
        public TemplateRepository Templates { get; }
        public ScheduledTaskRepository ScheduledTasks { get; }

        public EmailOtpService EmailOtp { get; }
        public CaptchaService Captcha { get; }
        public SmsService Sms { get; }
        public AiService Ai { get; }
        public IpGeoService Geo { get; }
        public VideoCheckService VideoCheck { get; }
        public BackupService Backup { get; }
        public LicenseService License { get; }
        public VpnManager Vpn { get; }
        public DeviceManager DeviceManager { get; }
        public FlowRunner Flows { get; }
        public OrchestratorService Orchestrator { get; }
        public SchedulerService Scheduler { get; }
        public AccountService AccountService { get; }
        public PageService PageService { get; }
        public PostingService Posting { get; }
        public InteractionService Interaction { get; }
        public RegistrationService Registration { get; }

        public ServiceLocator()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FarmReel");
            Directory.CreateDirectory(appData);

            Db.Initialize(Path.Combine(appData, "farmreel.db"));
            Log.Initialize(Path.Combine(appData, "farmreel.log"));
            Ui.Dispatcher = System.Windows.Application.Current?.Dispatcher;

            Settings = new SettingsService(new SettingsRepository());
            Settings.AppDataDir = appData;

            Accounts = new AccountRepository();
            Pages = new PageRepository();
            Devices = new DeviceRepository();
            PostJobs = new PostJobRepository();
            InteractionJobs = new InteractionJobRepository();
            Emails = new EmailRepository();
            Groups = new GroupRepository();
            Templates = new TemplateRepository();
            ScheduledTasks = new ScheduledTaskRepository();

            EmailOtp = new EmailOtpService();
            Captcha = new CaptchaService(Settings);
            Sms = new SmsService(Settings);
            Ai = new AiService(Settings);
            Geo = new IpGeoService();
            VideoCheck = new VideoCheckService(Settings);
            Backup = new BackupService(Settings);
            License = new LicenseService(Settings);
            Vpn = new VpnManager(Settings, Geo);
            DeviceManager = new DeviceManager(Settings, Vpn, Geo);
            Flows = new FlowRunner(DeviceManager, Settings);
            Posting = new PostingService(Ai);
            Interaction = new InteractionService(Posting);
            Orchestrator = new OrchestratorService(PostJobs, InteractionJobs, Accounts, Pages, Devices,
                Settings, Flows, DeviceManager, Posting, Interaction, License);
            Scheduler = new SchedulerService(PostJobs, ScheduledTasks, Settings, Backup);
            AccountService = new AccountService(Accounts, Pages, Devices, Settings, Flows, Geo);
            PageService = new PageService(Pages, Accounts, Devices, PostJobs, Flows);
            Registration = new RegistrationService(Accounts, Devices, Settings, Flows, EmailOtp, Sms, Captcha, License);

            // Wire scheduler events
            Scheduler.AutoStopReached += () =>
            {
                Log.Info("Scheduler", "Auto-stop reached");
                Orchestrator.Stop();
                if (Settings.ShutdownAfterFinish) SystemShutdown.Shutdown(30);
            };
            Scheduler.TaskDue += task =>
            {
                switch (task.TargetType)
                {
                    case "shutdown":
                        Orchestrator.Stop();
                        SystemShutdown.Shutdown(30);
                        break;
                    case "backup":
                        Backup.BackupDatabase();
                        Backup.BackupGroups(Groups.GetAll());
                        break;
                    case "post":
                        // post jobs are picked from the DB by the orchestrator workers
                        break;
                }
            };
        }

        public void Dispose()
        {
            try { Orchestrator.Stop(); } catch { }
            try { Scheduler.Dispose(); } catch { }
            try { Log.Flush(); } catch { }
        }
    }
}
