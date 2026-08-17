using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;

        public SettingsViewModel(ServiceLocator svc)
        {
            _svc = svc;
            Load();
        }

        // Emulator paths
        public string LdConsolePath { get => _svc.Settings.LdConsolePath; set { _svc.Settings.LdConsolePath = value; OnPropertyChanged(); } }
        public string MuMuManagerPath { get => _svc.Settings.MuMuManagerPath; set { _svc.Settings.MuMuManagerPath = value; OnPropertyChanged(); } }
        public string AdbPath { get => _svc.Settings.AdbPath; set { _svc.Settings.AdbPath = value; OnPropertyChanged(); } }
        public string OpenVpnPath { get => _svc.Settings.OpenVpnPath; set { _svc.Settings.OpenVpnPath = value; OnPropertyChanged(); } }
        public string FfprobePath { get => _svc.Settings.FfprobePath; set { _svc.Settings.FfprobePath = value; OnPropertyChanged(); } }

        // Runtime
        public string MaxActiveLd { get => _svc.Settings.MaxActiveLd.ToString(); set { _svc.Settings.SetInt("max_active_ld", int.TryParse(value, out var v) ? v : 5); OnPropertyChanged(); } }
        public string WaitAfterLdBootSec { get => _svc.Settings.WaitAfterLdBootSec.ToString(); set { _svc.Settings.SetInt("wait_after_ld_boot_sec", int.TryParse(value, out var v) ? v : 35); OnPropertyChanged(); } }
        public string WaitBetweenLdStartSec { get => _svc.Settings.WaitBetweenLdStartSec.ToString(); set { _svc.Settings.SetInt("wait_between_ld_start_sec", int.TryParse(value, out var v) ? v : 15); OnPropertyChanged(); } }
        public string WaitAfterUploadSec { get => _svc.Settings.WaitAfterUploadSec.ToString(); set { _svc.Settings.SetInt("wait_after_upload_sec", int.TryParse(value, out var v) ? v : 30); OnPropertyChanged(); } }
        public string AutoStopAt { get => _svc.Settings.AutoStopAt; set { _svc.Settings.AutoStopAt = value; OnPropertyChanged(); } }
        public string ClearCacheEveryRuns { get => _svc.Settings.ClearCacheEveryRuns.ToString(); set { _svc.Settings.SetInt("clear_cache_every_runs", int.TryParse(value, out var v) ? v : 150); OnPropertyChanged(); } }
        public string InstagramPackage { get => _svc.Settings.InstagramPackage; set { _svc.Settings.InstagramPackage = value; OnPropertyChanged(); } }
        public bool SkipOfflineDevices { get => _svc.Settings.SkipOfflineDevices; set { _svc.Settings.SkipOfflineDevices = value; OnPropertyChanged(); } }
        public bool MoveAfterPost { get => _svc.Settings.MoveAfterPost; set { _svc.Settings.MoveAfterPost = value; OnPropertyChanged(); } }
        public bool ShutdownAfterFinish { get => _svc.Settings.ShutdownAfterFinish; set { _svc.Settings.ShutdownAfterFinish = value; OnPropertyChanged(); } }
        public bool AutoBackupDb { get => _svc.Settings.AutoBackupDb; set { _svc.Settings.AutoBackupDb = value; OnPropertyChanged(); } }

        // Services
        public string CaptchaApiKey { get => _svc.Settings.CaptchaApiKey; set { _svc.Settings.CaptchaApiKey = value; OnPropertyChanged(); } }
        public string SmsApiKey { get => _svc.Settings.SmsApiKey; set { _svc.Settings.SmsApiKey = value; OnPropertyChanged(); } }
        public string AiApiKey { get => _svc.Settings.AiApiKey; set { _svc.Settings.AiApiKey = value; OnPropertyChanged(); } }
        public string AiBaseUrl { get => _svc.Settings.AiBaseUrl; set { _svc.Settings.AiBaseUrl = value; OnPropertyChanged(); } }
        public string AiModel { get => _svc.Settings.AiModel; set { _svc.Settings.AiModel = value; OnPropertyChanged(); } }
        public string LicenseKey { get => _svc.Settings.LicenseKey; set { _svc.Settings.LicenseKey = value; OnPropertyChanged(); } }
        public string LicenseServer { get => _svc.Settings.LicenseServer; set { _svc.Settings.LicenseServer = value; OnPropertyChanged(); } }
        public string LicensePlan => _svc.License.Info.Plan;
        public string LicenseKeysLeft => $"{_svc.License.Info.TimeChangeKeysTotal - _svc.License.Info.TimeChangeKeysUsed}/{_svc.License.Info.TimeChangeKeysTotal}";
        public string RegFull => _svc.License.CanUseRegFull ? "Enabled" : "Disabled";
        public string Novery => _svc.License.CanUseNovery ? "Enabled" : "Disabled";

        public void Load() { /* properties read live from service */ }

        public void SaveAll()
        {
            _svc.Settings.Set("ldconsole_path", LdConsolePath);
            _svc.Settings.Set("mumu_manager_path", MuMuManagerPath);
            _svc.Settings.Set("adb_path", AdbPath);
            _svc.Settings.Set("openvpn_path", OpenVpnPath);
            _svc.Settings.Set("ffprobe_path", FfprobePath);
            _svc.Settings.Set("captcha_api_key", CaptchaApiKey);
            _svc.Settings.Set("sms_api_key", SmsApiKey);
            _svc.Settings.Set("ai_api_key", AiApiKey);
            _svc.Settings.Set("ai_base_url", AiBaseUrl);
            _svc.Settings.Set("ai_model", AiModel);
            _svc.Settings.Set("license_key", LicenseKey);
            _svc.Settings.Set("license_server", LicenseServer);
            _svc.Settings.Set("auto_stop_at", AutoStopAt);
            _svc.Settings.Set("instagram_package", InstagramPackage);
            _svc.Settings.SetBool("skip_offline_devices", SkipOfflineDevices);
            _svc.Settings.SetBool("move_after_post", MoveAfterPost);
            _svc.Settings.SetBool("shutdown_after_finish", ShutdownAfterFinish);
            _svc.Settings.SetBool("auto_backup_db", AutoBackupDb);
            Log.Info("Settings", "Settings saved");
        }

        public async Task ActivateLicenseAsync()
        {
            var info = await _svc.License.ActivateAsync(LicenseKey).ConfigureAwait(false);
            Ui.Run(() =>
            {
                OnPropertyChanged(nameof(LicensePlan));
                OnPropertyChanged(nameof(LicenseKeysLeft));
                OnPropertyChanged(nameof(RegFull));
                OnPropertyChanged(nameof(Novery));
                Log.Info("License", $"Activated: plan={info.Plan} keys={info.TimeChangeKeysTotal - info.TimeChangeKeysUsed} note={info.Note}");
            });
        }

        public async Task CheckAiAsync()
        {
            var ok = await _svc.Ai.CheckHealthAsync().ConfigureAwait(false);
            Ui.Run(() => Log.Info("AI", ok ? "AI connection OK" : "AI connection FAILED"));
        }

        public void BackupNow()
        {
            _svc.Backup.BackupDatabase();
            _svc.Backup.BackupProfiles(_svc.Accounts.GetAll());
            Log.Info("Backup", "Manual backup completed");
        }

        public async Task CheckEnvironmentAsync()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Environment check:");
            sb.AppendLine($"  LDPlayer console: {Exists(LdConsolePath)}");
            sb.AppendLine($"  MuMuManager: {Exists(MuMuManagerPath)}");
            sb.AppendLine($"  adb: {Exists(AdbPath)}");
            sb.AppendLine($"  openvpn: {Exists(OpenVpnPath)}");
            sb.AppendLine($"  ffprobe: {Exists(FfprobePath)}");
            sb.AppendLine($"  Captcha API key: {OnOff(_svc.Settings.CaptchaApiKey)}");
            sb.AppendLine($"  SMS API key: {OnOff(_svc.Settings.SmsApiKey)}");
            sb.AppendLine($"  AI API key: {OnOff(_svc.Settings.AiApiKey)}");
            sb.AppendLine($"  License: {_svc.License.Info.Plan} ({_svc.License.Info.Key})");
            sb.AppendLine($"  DB: {Db.DatabasePath}");
            try
            {
                var online = await _svc.DeviceManager.ListAvailableAsync().ConfigureAwait(false);
                sb.AppendLine($"  ADB devices online: {online.Count}");
            }
            catch (Exception ex) { sb.AppendLine("  ADB devices: error - " + ex.Message); }
            Ui.Run(() => Log.Info("Environment", sb.ToString()));
        }

        private static string Exists(string p) =>
            string.IsNullOrEmpty(p) ? "not set" : (File.Exists(p) ? "OK" : "MISSING: " + p);

        private static string OnOff(string v) => string.IsNullOrEmpty(v) ? "missing" : "set";
    }
}
