using System;
using System.Collections.Generic;

namespace FarmReel.Core.Models
{
    public enum AccountStatus
    {
        Unknown = 0,
        Live = 1,
        Die = 2,
        Checkpoint = 3,
        Locked282 = 4,
        Suspended = 5,
        AppealPending = 6
    }

    public enum DeviceType
    {
        LDPlayer = 0,
        MuMu = 1,
        RealPhone = 2
    }

    public class Account
    {
        public long Id { get; set; }
        public string Uid { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordEnc { get; set; } = "";
        public string TotpSecretEnc { get; set; } = "";
        public string Phone { get; set; } = "";
        public string DateOfBirth { get; set; } = "";
        public string Name { get; set; } = "";
        public AccountStatus Status { get; set; } = AccountStatus.Unknown;
        public long DeviceId { get; set; }
        public string MailProvider { get; set; } = "";       // zoho | gmail | outlook | yandex | microsoft
        public string Notes { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastCheckAt { get; set; }
        public DateTime? DateCreated { get; set; }           // account creation date (farmed accounts)
        public bool ProfessionalMode { get; set; }
        public string ExtraJson { get; set; } = "{}";

        // Runtime (not persisted)
        public string Display => string.IsNullOrEmpty(Name) ? (string.IsNullOrEmpty(Email) ? Uid : Email) : Name;
    }

    public class Page
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public string PageId { get; set; } = "";
        public string Name { get; set; } = "";
        public string IdentityMode { get; set; } = "page";   // page | profile | professional
        public string Status { get; set; } = "unknown";      // ok | not_recommended | flagged
        public long Followers { get; set; }
        public long Reach { get; set; }
        public bool MonetizationEligible { get; set; }
        public string MonetizationNote { get; set; } = "";
        public string SupportInboxJson { get; set; } = "[]";
        public string DashboardJson { get; set; } = "{}";
        public string Notes { get; set; } = "";
        public DateTime? LastCheckAt { get; set; }
        public string Display => string.IsNullOrEmpty(Name) ? PageId : Name;
    }

    public class DeviceInstance
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public long GroupId { get; set; }
        public DeviceType DeviceType { get; set; } = DeviceType.LDPlayer;
        public long AccountId { get; set; }
        public string PackageName { get; set; } = "com.facebook.katana";
        public string AdbSerial { get; set; } = "";          // 127.0.0.1:5555 style
        public int Cpu { get; set; } = 2;
        public int RamMb { get; set; } = 2048;
        public string Resolution { get; set; } = "1080x1920";
        public int Dpi { get; set; } = 480;
        public bool NetworkBridge { get; set; }
        public string DeviceInfoJson { get; set; } = "{}";   // mac/imei/androidid/model/manufacturer/gps/tz
        public string LastIpJson { get; set; } = "{}";
        public string VpnProfile { get; set; } = "";         // name of .ovpn profile
        public string Proxy { get; set; } = "";              // host:port[:user:pass]
        public bool Rooted { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime? LastRunAt { get; set; }

        public string Display => Name;
    }

    public class DeviceInfo
    {
        public string Mac { get; set; } = "";
        public string Imei { get; set; } = "";
        public string Imsi { get; set; } = "";
        public string SimSerial { get; set; } = "";
        public string AndroidId { get; set; } = "";
        public string Model { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Serial { get; set; } = "";
        public string GpsLat { get; set; } = "";
        public string GpsLng { get; set; } = "";
        public string Timezone { get; set; } = "";
    }

    public class LdGroup
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public class EmailAccount
    {
        public long Id { get; set; }
        public string Email { get; set; } = "";
        public string PasswordEnc { get; set; } = "";
        public string Provider { get; set; } = "";   // zoho | gmail | outlook | yandex
        public bool IsTrusted { get; set; }
        public string Notes { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Display => Email;
    }

    public class LogEntry
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO";   // DEBUG | INFO | WARN | ERROR
        public string Device { get; set; } = "";
        public string Account { get; set; } = "";
        public string Action { get; set; } = "";
        public string Message { get; set; } = "";
        public string ScreenshotPath { get; set; } = "";

        public string DisplayLine =>
            $"{Timestamp:HH:mm:ss} [{Level}] {Action} {Message}";
    }

    public class Template
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "post";   // post | active | login | reg
        public string Json { get; set; } = "{}";
        public string Notes { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class Setting
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public class LicenseInfo
    {
        public string Key { get; set; } = "";
        public string Plan { get; set; } = "";            // trial | 1m | 3m | 12m
        public DateTime? ExpiresAt { get; set; }
        public int TimeChangeKeysTotal { get; set; }
        public int TimeChangeKeysUsed { get; set; }
        public bool RegFullEnabled { get; set; }
        public bool VerifyNoveryEnabled { get; set; }
        public string Note { get; set; } = "";
    }
}
