using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Flow;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>High-level account operations: CRUD, import, live checks, 2FA, appeal, unlock.</summary>
    public class AccountService
    {
        private readonly AccountRepository _accounts;
        private readonly PageRepository _pages;
        private readonly DeviceRepository _devices;
        private readonly SettingsService _settings;
        private readonly IFlowRunner _flows;
        private readonly IpGeoService _geo;
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public AccountService(AccountRepository accounts, PageRepository pages, DeviceRepository devices,
            SettingsService settings, IFlowRunner flows, IpGeoService geo)
        {
            _accounts = accounts;
            _pages = pages;
            _devices = devices;
            _settings = settings;
            _flows = flows;
            _geo = geo;
        }

        public List<Account> GetAll() => _accounts.GetAll();
        public Account Get(long id) => _accounts.Get(id);
        public long Save(Account a) => _accounts.Save(a);
        public void Delete(long id) { _pages.DeleteByAccount(id); _accounts.Delete(id); }

        /// <summary>Import accounts from text: one per line, fields separated by '|':
        /// email|password|2fa(secret or code)|phone|dob</summary>
        public (int added, int skipped) ImportFromText(string text)
        {
            int added = 0, skipped = 0;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split('|');
                if (parts.Length < 2) { skipped++; continue; }
                var acc = new Account
                {
                    Email = parts[0].Trim(),
                    PasswordEnc = CredentialVault.Protect(parts[1].Trim()),
                    TotpSecretEnc = parts.Length > 2 ? CredentialVault.Protect(parts[2].Trim()) : "",
                    Phone = parts.Length > 3 ? parts[3].Trim() : "",
                    DateOfBirth = parts.Length > 4 ? parts[4].Trim() : ""
                };
                if (string.IsNullOrEmpty(acc.Email)) { skipped++; continue; }
                _accounts.Save(acc);
                added++;
            }
            return (added, skipped);
        }

        /// <summary>Check whether the account is live/dead/checkpointed using its UID via public HTTP.</summary>
        public async Task<AccountStatus> CheckLiveAsync(Account account)
        {
            try
            {
                if (string.IsNullOrEmpty(account.Uid)) return AccountStatus.Unknown;
                var req = new HttpRequestMessage(HttpMethod.Get, "https://www.facebook.com/" + account.Uid);
                req.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
                var resp = await _http.SendAsync(req).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var lower = body.ToLowerInvariant();

                AccountStatus status;
                if (lower.Contains("temporarily locked") || lower.Contains("account locked"))
                    status = AccountStatus.Checkpoint;
                else if (lower.Contains("your account has been disabled") || lower.Contains("account disabled") ||
                         lower.Contains("unusual activity") && resp.StatusCode == System.Net.HttpStatusCode.OK && body.Length < 20000)
                    status = AccountStatus.Die;
                else if (resp.IsSuccessStatusCode && body.Contains("\"name\""))
                    status = AccountStatus.Live;
                else
                    status = AccountStatus.Unknown;

                account.Status = status;
                account.LastCheckAt = DateTime.Now;
                _accounts.Save(account);
                return status;
            }
            catch (Exception ex)
            {
                Log.Warn("Account", $"Live check failed for {account.Email}: {ex.Message}");
                return AccountStatus.Unknown;
            }
        }

        public string Get2FaCode(Account account)
        {
            var secret = CredentialVault.Unprotect(account.TotpSecretEnc);
            if (string.IsNullOrEmpty(secret)) return "";
            return Totp.Generate(secret);
        }

        public void Set2FaSecret(Account account, string secret)
        {
            account.TotpSecretEnc = CredentialVault.Protect(secret);
            _accounts.Save(account);
        }

        public void SetPassword(Account account, string password)
        {
            account.PasswordEnc = CredentialVault.Protect(password);
            _accounts.Save(account);
        }

        public string GetPassword(Account account) => CredentialVault.Unprotect(account.PasswordEnc);

        public async Task<string> RecordIpAsync(DeviceInstance device)
        {
            var geo = await _geo.LookupAsync().ConfigureAwait(false);
            device.LastIpJson = System.Text.Json.JsonSerializer.Serialize(geo);
            if (_settings.AutoGpsTimezone && !string.IsNullOrEmpty(geo.Lat))
            {
                var info = System.Text.Json.JsonSerializer.Deserialize<DeviceInfo>(
                    string.IsNullOrEmpty(device.DeviceInfoJson) ? "{}" : device.DeviceInfoJson)
                    ?? new DeviceInfo();
                info.GpsLat = geo.Lat;
                info.GpsLng = geo.Lng;
                info.Timezone = geo.Timezone;
                device.DeviceInfoJson = System.Text.Json.JsonSerializer.Serialize(info);
            }
            _devices.Save(device);
            Log.Info("Device", $"Recorded IP {geo.Ip} ({geo.Country}) for {device.Name}", device.Name);
            return geo.Ip;
        }

        public async Task<FlowResult> AppealAsync(Account account)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "appeal",
                new Dictionary<string, string> { ["email"] = account.Email, ["dob"] = account.DateOfBirth })
                .ConfigureAwait(false);
        }

        public async Task<FlowResult> Unlock282Async(Account account)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "unlock282",
                new Dictionary<string, string>()).ConfigureAwait(false);
        }

        public async Task<FlowResult> CreateInstagramAsync(Account account)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "create_instagram",
                new Dictionary<string, string>()).ConfigureAwait(false);
        }

        public async Task<FlowResult> ToggleProfessionalModeAsync(Account account, bool enable)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "professional_mode",
                new Dictionary<string, string> { ["enable"] = enable.ToString() }).ConfigureAwait(false);
        }

        public async Task<FlowResult> SetAccountInfoAsync(Account account, string field, string value)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "set_account_info",
                new Dictionary<string, string> { ["field"] = field, ["value"] = value }).ConfigureAwait(false);
        }

        public async Task<FlowResult> AddFriendsAsync(Account account, string uidOrLink)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "add_friend",
                new Dictionary<string, string> { ["uid"] = uidOrLink }).ConfigureAwait(false);
        }

        public async Task<FlowResult> JoinGroupAsync(Account account, string groupIdOrLink)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "join_group",
                new Dictionary<string, string> { ["group"] = groupIdOrLink }).ConfigureAwait(false);
        }

        public async Task<FlowResult> SharePostAsync(Account account, string postLink, bool toProfile, bool toGroups, int groupCount)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "share_post",
                new Dictionary<string, string>
                {
                    ["link"] = postLink,
                    ["profile"] = toProfile.ToString(),
                    ["groups"] = toGroups.ToString(),
                    ["count"] = groupCount.ToString()
                }).ConfigureAwait(false);
        }

        public async Task<FlowResult> ReplyInboxAsync(Account account, string replyText)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "reply_inbox",
                new Dictionary<string, string> { ["text"] = replyText }).ConfigureAwait(false);
        }

        public async Task<FlowResult> PostCheckInAsync(Account account, string photoPath, string location)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "checkin_post",
                new Dictionary<string, string> { ["photo"] = photoPath, ["location"] = location })
                .ConfigureAwait(false);
        }

        public async Task<FlowResult> CreateStoryAsync(Account account, string photoPath, string link)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "create_story",
                new Dictionary<string, string> { ["photo"] = photoPath, ["link"] = link }).ConfigureAwait(false);
        }

        public async Task<FlowResult> PullNamesAsync(DeviceInstance device)
        {
            return await _flows.PullAccountNamesAsync(device).ConfigureAwait(false);
        }

        public async Task<FlowResult> LoginAsync(Account account, DeviceInstance device)
        {
            var password = GetPassword(account);
            if (string.IsNullOrEmpty(password)) return new FlowResult { Success = false, Message = "No password stored" };
            var otp = Get2FaCode(account);
            return await _flows.LoginAsync(device, account, otp).ConfigureAwait(false);
        }

        public async Task<FlowResult> CreatePageAsync(Account account, string pageName, string category)
        {
            return await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "create_page",
                new Dictionary<string, string> { ["name"] = pageName, ["category"] = category }).ConfigureAwait(false);
        }

        public async Task<FlowResult> CheckNotificationsAsync(Account account)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "check_notifications",
                new Dictionary<string, string>()).ConfigureAwait(false);

        public async Task<FlowResult> ReviewPageAsync(Account account, string pageLink, string reviewText)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "reviews",
                new Dictionary<string, string> { ["link"] = pageLink, ["text"] = reviewText }).ConfigureAwait(false);

        public async Task<FlowResult> CheckPrimaryLocationAsync(Account account)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "check_primary_location",
                new Dictionary<string, string>()).ConfigureAwait(false);

        public async Task<FlowResult> ManualLoginAsync(Account account, DeviceInstance device)
            => await _flows.RunActionAsync(device ?? _devices.Get(account.DeviceId), account, "manual_login",
                new Dictionary<string, string>()).ConfigureAwait(false);

        public async Task<FlowResult> ManualBackupAsync(Account account)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "backup_profile",
                new Dictionary<string, string> { ["dest"] = _settings.ProfilesDir + "\\" + account.Uid }).ConfigureAwait(false);

        public async Task<FlowResult> PostToGroupAsync(Account account, string groupIdOrLink, string text)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "post_to_group",
                new Dictionary<string, string> { ["group"] = groupIdOrLink, ["text"] = text }).ConfigureAwait(false);

        public async Task<FlowResult> LeaveGroupAsync(Account account, string groupIdOrLink)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "leave_group",
                new Dictionary<string, string> { ["group"] = groupIdOrLink }).ConfigureAwait(false);

        public async Task<FlowResult> GroupSuggestionsAsync(Account account, string keyword)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "groups_suggestions",
                new Dictionary<string, string> { ["keyword"] = keyword }).ConfigureAwait(false);

        public async Task<FlowResult> WatchLiveAsync(Account account, string liveLink)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "watch_live",
                new Dictionary<string, string> { ["link"] = liveLink }).ConfigureAwait(false);

        public async Task<FlowResult> ViewStoryAsync(Account account, string storyLink)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "view_story",
                new Dictionary<string, string> { ["link"] = storyLink }).ConfigureAwait(false);

        public async Task<FlowResult> ConfirmFriendRequestsAsync(Account account)
            => await _flows.RunActionAsync(_devices.Get(account.DeviceId), account, "confirm_friend",
                new Dictionary<string, string>()).ConfigureAwait(false);
    }
}
