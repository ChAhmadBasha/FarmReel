using System.Collections.Generic;
using FarmReel.Core.Data;
using FarmReel.Core.Models;

namespace FarmReel.Core.Services
{
    public class SettingsService
    {
        private readonly SettingsRepository _repo;

        public SettingsService(SettingsRepository repo) { _repo = repo; }

        public string Get(string key, string def = "") => _repo.Get(key, def);
        public void Set(string key, string value) => _repo.Set(key, value);
        public int GetInt(string key, int def = 0) => _repo.GetInt(key, def);
        public void SetInt(string key, int value) => _repo.Set(key, value.ToString());
        public bool GetBool(string key, bool def = false) => _repo.GetBool(key, def);
        public void SetBool(string key, bool value) => _repo.Set(key, value ? "true" : "false");
        public Dictionary<string, string> GetAll() => _repo.GetAll();

        // ---- App data folders ----
        public string AppDataDir
        {
            get
            {
                var d = Get("app_data_dir", "");
                if (string.IsNullOrEmpty(d))
                {
                    d = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                        "FarmReel");
                }
                return d;
            }
            set { Set("app_data_dir", value); }
        }

        public string ContentRoot => Get("content_root", "");
        public string BackupDir => Get("backup_dir", System.IO.Path.Combine(AppDataDir, "backup"));
        public string ProfilesDir => System.IO.Path.Combine(AppDataDir, "profiles");
        public string CookiesDir => System.IO.Path.Combine(AppDataDir, "cookies");
        public string DeviceInfoDir => System.IO.Path.Combine(AppDataDir, "device_info");
        public string FlowsDir => System.IO.Path.Combine(AppDataDir, "flows");
        public string ApkVaultDir => System.IO.Path.Combine(AppDataDir, "apk");
        public string VpnDir => System.IO.Path.Combine(AppDataDir, "vpn");

        // ---- Emulator paths ----
        public string LdConsolePath { get => Get("ldconsole_path", "ldconsole.exe"); set => Set("ldconsole_path", value); }
        public string MuMuManagerPath { get => Get("mumu_manager_path", "MuMuManager.exe"); set => Set("mumu_manager_path", value); }
        public string AdbPath { get => Get("adb_path", "adb.exe"); set => Set("adb_path", value); }
        public string OpenVpnPath { get => Get("openvpn_path", "openvpn.exe"); set => Set("openvpn_path", value); }
        public string FfprobePath { get => Get("ffprobe_path", "ffprobe.exe"); set => Set("ffprobe_path", value); }

        // ---- Runtime behavior ----
        public int MaxActiveLd { get => GetInt("max_active_ld", 5); set => SetInt("max_active_ld", value); }
        public int WaitAfterLdBootSec { get => GetInt("wait_after_ld_boot_sec", 35); set => SetInt("wait_after_ld_boot_sec", value); }
        public int WaitBetweenLdStartSec { get => GetInt("wait_between_ld_start_sec", 15); set => SetInt("wait_between_ld_start_sec", value); }
        public int WaitAfterUploadSec { get => GetInt("wait_after_upload_sec", 30); set => SetInt("wait_after_upload_sec", value); }
        public bool HardwareAcceleration { get => GetBool("hardware_acceleration", true); set => SetBool("hardware_acceleration", value); }
        public bool CheckIp { get => GetBool("check_ip", true); set => SetBool("check_ip", value); }
        public bool AutoGpsTimezone { get => GetBool("auto_gps_timezone", true); set => SetBool("auto_gps_timezone", value); }
        public string IpCountryFilter { get => Get("ip_country_filter", ""); set => Set("ip_country_filter", value); } // allow|block|""
        public string IpCountryList { get => Get("ip_country_list", ""); set => Set("ip_country_list", value); }
        public bool AutoBackupDb { get => GetBool("auto_backup_db", true); set => SetBool("auto_backup_db", value); }
        public int ClearCacheEveryRuns { get => GetInt("clear_cache_every_runs", 150); set => SetInt("clear_cache_every_runs", value); }
        public bool ShutdownAfterFinish { get => GetBool("shutdown_after_finish", false); set => SetBool("shutdown_after_finish", value); }
        public string AutoStopAt { get => Get("auto_stop_at", ""); set => Set("auto_stop_at", value); } // "HH:mm"
        public bool SkipOfflineDevices { get => GetBool("skip_offline_devices", true); set => SetBool("skip_offline_devices", value); }
        public bool MoveAfterPost { get => GetBool("move_after_post", true); set => SetBool("move_after_post", value); }
        public bool RunAtStartup { get => GetBool("run_at_startup", false); set => SetBool("run_at_startup", value); }
        public string FacebookApkPath { get => Get("facebook_apk_path", ""); set => Set("facebook_apk_path", value); }
        public string InstagramApkPath { get => Get("instagram_apk_path", ""); set => Set("instagram_apk_path", value); }
        public string InstagramPackage { get => Get("instagram_package", "com.instagram.android"); set => Set("instagram_package", value); }

        // ---- Services ----
        public string CaptchaApiKey { get => Get("captcha_api_key", ""); set => Set("captcha_api_key", value); }
        public string SmsApiKey { get => Get("sms_api_key", ""); set => Set("sms_api_key", value); }
        public string AiApiKey { get => Get("ai_api_key", ""); set => Set("ai_api_key", value); }
        public string AiBaseUrl { get => Get("ai_base_url", "https://api.openai.com/v1"); set => Set("ai_base_url", value); }
        public string AiModel { get => Get("ai_model", "gpt-4o-mini"); set => Set("ai_model", value); }
        public string LicenseKey { get => Get("license_key", ""); set => Set("license_key", value); }
        public string LicenseServer { get => Get("license_server", ""); set => Set("license_server", value); }
    }
}
