using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.App.ViewModels
{
    public class AccountsViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<AccountRow> Accounts { get; } = new ObservableCollection<AccountRow>();
        public ObservableCollection<PageRow> Pages { get; } = new ObservableCollection<PageRow>();

        public AccountsViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        public void Refresh()
        {
            ApplyFilter();
            Pages.Clear();
            foreach (var p in _svc.Pages.GetAll()) Pages.Add(new PageRow { Model = p });
        }

        public string ImportText { get; set; } = "";

        public void AddAccount(string email, string password, string totp, string phone, string dob)
        {
            var acc = new Account
            {
                Email = email ?? "",
                PasswordEnc = CredentialVault.Protect(password ?? ""),
                TotpSecretEnc = string.IsNullOrEmpty(totp) ? "" : CredentialVault.Protect(totp),
                Phone = phone ?? "",
                DateOfBirth = dob ?? ""
            };
            _svc.Accounts.Save(acc);
            Refresh();
        }

        public void ImportFromText()
        {
            if (string.IsNullOrWhiteSpace(ImportText)) return;
            var (added, skipped) = _svc.AccountService.ImportFromText(ImportText);
            Refresh();
            Log.Info("Accounts", $"Imported {added} accounts ({skipped} skipped)");
        }

        public void DeleteAccount(AccountRow row)
        {
            if (row == null) return;
            _svc.AccountService.Delete(row.Id);
            Refresh();
        }

        public async Task CheckLiveAsync(AccountRow row)
        {
            if (row == null) return;
            var status = await _svc.AccountService.CheckLiveAsync(row.Model).ConfigureAwait(false);
            Ui.Run(() => { row.Model.Status = status; row.OnPropertyChanged(nameof(AccountRow.Status)); Refresh(); });
        }

        public async Task CheckLiveAllAsync()
        {
            foreach (var row in Accounts.ToList())
                await CheckLiveAsync(row).ConfigureAwait(false);
        }

        public async Task LoginAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.LoginAsync(row.Model, _svc.Devices.Get(row.Model.DeviceId))
                .ConfigureAwait(false);
            Log.Info("Accounts", $"Login {row.Email}: {result.Message}", row.Email);
        }

        public async Task PullNamesAsync(DeviceRow device)
        {
            if (device == null) return;
            var result = await _svc.AccountService.PullNamesAsync(device.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Pull names on {device.Name}: {result.Message}");
            Refresh();
        }

        public string Copy2Fa(AccountRow row)
        {
            if (row == null) return "";
            var code = _svc.AccountService.Get2FaCode(row.Model);
            System.Windows.Clipboard.SetText(code);
            Log.Info("Accounts", $"2FA code {code} copied to clipboard");
            return code;
        }

        public void Set2Fa(AccountRow row, string secret)
        {
            if (row == null) return;
            _svc.AccountService.Set2FaSecret(row.Model, secret);
            row.OnPropertyChanged(nameof(AccountRow.Has2Fa));
        }

        public void SetPassword(AccountRow row, string password)
        {
            if (row == null) return;
            _svc.AccountService.SetPassword(row.Model, password);
        }

        public async Task AppealAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.AppealAsync(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Appeal: {result.Message}");
        }

        public async Task Unlock282Async(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.Unlock282Async(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Unlock 282: {result.Message}");
        }

        public async Task ToggleProModeAsync(AccountRow row, bool enable)
        {
            if (row == null) return;
            var result = await _svc.AccountService.ToggleProfessionalModeAsync(row.Model, enable).ConfigureAwait(false);
            Log.Info("Accounts", $"Professional mode {enable}: {result.Message}");
        }

        public async Task SetInfoAsync(AccountRow row, string field, string value)
        {
            if (row == null) return;
            var result = await _svc.AccountService.SetAccountInfoAsync(row.Model, field, value).ConfigureAwait(false);
            Log.Info("Accounts", $"Set {field}: {result.Message}");
        }

        public async Task CreateInstagramAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.CreateInstagramAsync(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Create Instagram: {result.Message}");
        }

        public async Task AddFriendAsync(AccountRow row, string uidOrLink)
        {
            if (row == null || string.IsNullOrEmpty(uidOrLink)) return;
            var result = await _svc.AccountService.AddFriendsAsync(row.Model, uidOrLink).ConfigureAwait(false);
            Log.Info("Accounts", $"Add friend: {result.Message}");
        }

        public async Task JoinGroupAsync(AccountRow row, string groupIdOrLink)
        {
            if (row == null || string.IsNullOrEmpty(groupIdOrLink)) return;
            var result = await _svc.AccountService.JoinGroupAsync(row.Model, groupIdOrLink).ConfigureAwait(false);
            Log.Info("Accounts", $"Join group: {result.Message}");
        }

        public async Task SharePostAsync(AccountRow row, string link, bool profile, bool groups, int count)
        {
            if (row == null || string.IsNullOrEmpty(link)) return;
            var result = await _svc.AccountService.SharePostAsync(row.Model, link, profile, groups, count)
                .ConfigureAwait(false);
            Log.Info("Accounts", $"Share post: {result.Message}");
        }

        public async Task ReplyInboxAsync(AccountRow row, string text)
        {
            if (row == null) return;
            var result = await _svc.AccountService.ReplyInboxAsync(row.Model, text).ConfigureAwait(false);
            Log.Info("Accounts", $"Reply inbox: {result.Message}");
        }

        public async Task CreatePageAsync(AccountRow row, string name, string category)
        {
            if (row == null || string.IsNullOrEmpty(name)) return;
            var result = await _svc.AccountService.CreatePageAsync(row.Model, name, category).ConfigureAwait(false);
            Log.Info("Accounts", $"Create page '{name}': {result.Message}");
        }

        public void AssignDevice(AccountRow row, long deviceId)
        {
            if (row == null) return;
            row.Model.DeviceId = deviceId;
            _svc.Accounts.Save(row.Model);
            row.OnPropertyChanged(nameof(AccountRow.Device));
        }

        // ---- search / filter ----

        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set { _filter = value ?? ""; OnPropertyChanged(); ApplyFilter(); }
        }

        private void ApplyFilter()
        {
            Accounts.Clear();
            var f = _filter.Trim().ToLowerInvariant();
            foreach (var a in _svc.Accounts.GetAll())
            {
                if (f.Length == 0 ||
                    a.Email.ToLowerInvariant().Contains(f) ||
                    a.Name.ToLowerInvariant().Contains(f) ||
                    a.Uid.ToLowerInvariant().Contains(f) ||
                    a.Status.ToString().ToLowerInvariant().Contains(f))
                    Accounts.Add(new AccountRow { Model = a });
            }
        }

        // ---- extra account features ----

        public async Task CheckNotificationsAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.CheckNotificationsAsync(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Notifications: {result.Message}");
        }

        public async Task ReviewPageAsync(AccountRow row, string link, string text)
        {
            if (row == null) return;
            var result = await _svc.AccountService.ReviewPageAsync(row.Model, link, text).ConfigureAwait(false);
            Log.Info("Accounts", $"Review: {result.Message}");
        }

        public async Task CheckPrimaryLocationAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.CheckPrimaryLocationAsync(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Primary location: {result.Message}");
        }

        public async Task ManualLoginAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.ManualLoginAsync(row.Model, _svc.Devices.Get(row.Model.DeviceId))
                .ConfigureAwait(false);
            Log.Info("Accounts", $"Manual login: {result.Message}");
        }

        public async Task ManualBackupAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.ManualBackupAsync(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Manual backup: {result.Message}");
        }

        public async Task PostToGroupAsync(AccountRow row, string group, string text)
        {
            if (row == null || string.IsNullOrEmpty(group)) return;
            var result = await _svc.AccountService.PostToGroupAsync(row.Model, group, text).ConfigureAwait(false);
            Log.Info("Accounts", $"Post to group: {result.Message}");
        }

        public async Task LeaveGroupAsync(AccountRow row, string group)
        {
            if (row == null || string.IsNullOrEmpty(group)) return;
            var result = await _svc.AccountService.LeaveGroupAsync(row.Model, group).ConfigureAwait(false);
            Log.Info("Accounts", $"Leave group: {result.Message}");
        }

        public async Task GroupSuggestionsAsync(AccountRow row, string keyword)
        {
            if (row == null || string.IsNullOrEmpty(keyword)) return;
            var result = await _svc.AccountService.GroupSuggestionsAsync(row.Model, keyword).ConfigureAwait(false);
            Log.Info("Accounts", $"Group suggestions: {result.Message}");
        }

        public async Task WatchLiveAsync(AccountRow row, string link)
        {
            if (row == null || string.IsNullOrEmpty(link)) return;
            var result = await _svc.AccountService.WatchLiveAsync(row.Model, link).ConfigureAwait(false);
            Log.Info("Accounts", $"Watch live: {result.Message}");
        }

        public async Task ViewStoryAsync(AccountRow row, string link)
        {
            if (row == null || string.IsNullOrEmpty(link)) return;
            var result = await _svc.AccountService.ViewStoryAsync(row.Model, link).ConfigureAwait(false);
            Log.Info("Accounts", $"View story: {result.Message}");
        }

        public async Task ConfirmFriendsAsync(AccountRow row)
        {
            if (row == null) return;
            var result = await _svc.AccountService.ConfirmFriendRequestsAsync(row.Model).ConfigureAwait(false);
            Log.Info("Accounts", $"Confirm friends: {result.Message}");
        }

        public async Task CheckInPostAsync(AccountRow row, string photo, string location)
        {
            if (row == null) return;
            var result = await _svc.AccountService.PostCheckInAsync(row.Model, photo, location).ConfigureAwait(false);
            Log.Info("Accounts", $"Check-in post: {result.Message}");
        }

        public async Task CreateStoryAsync(AccountRow row, string photo, string link)
        {
            if (row == null) return;
            var result = await _svc.AccountService.CreateStoryAsync(row.Model, photo, link).ConfigureAwait(false);
            Log.Info("Accounts", $"Create story: {result.Message}");
        }
    }

    public class PagesViewModel : ObservableObject
    {
        private readonly ServiceLocator _svc;
        public ObservableCollection<PageRow> Pages { get; } = new ObservableCollection<PageRow>();

        public PagesViewModel(ServiceLocator svc) { _svc = svc; Refresh(); }

        public void Refresh()
        {
            Pages.Clear();
            var f = _pageFilter.Trim().ToLowerInvariant();
            foreach (var p in _svc.Pages.GetAll())
            {
                if (f.Length == 0 ||
                    p.Name.ToLowerInvariant().Contains(f) ||
                    p.PageId.ToLowerInvariant().Contains(f) ||
                    p.Status.ToLowerInvariant().Contains(f) ||
                    p.IdentityMode.ToLowerInvariant().Contains(f))
                    Pages.Add(new PageRow { Model = p });
            }
        }

        private string _pageFilter = "";
        public string PageFilter
        {
            get => _pageFilter;
            set { _pageFilter = value ?? ""; OnPropertyChanged(); Refresh(); }
        }

        public void AddPage(long accountId, string name, string pageId, string identityMode)
        {
            var page = new Page { AccountId = accountId, Name = name ?? "", PageId = pageId ?? "", IdentityMode = identityMode ?? "page" };
            _svc.Pages.Save(page);
            Refresh();
        }

        public void DeletePage(PageRow row)
        {
            if (row == null) return;
            _svc.PageService.Delete(row.Id);
            Refresh();
        }

        public async Task PullDashboardAsync(PageRow row)
        {
            if (row == null) return;
            var result = await _svc.PageService.PullDashboardAsync(row.Model).ConfigureAwait(false);
            Ui.Run(() => { row.OnPropertyChanged(nameof(PageRow.Followers)); row.OnPropertyChanged(nameof(PageRow.Reach)); Refresh(); });
            Log.Info("Pages", $"Dashboard pull: {result.Message}");
        }

        public async Task DeleteAllPostsAsync(PageRow row)
        {
            if (row == null) return;
            var result = await _svc.PageService.DeleteAllPostsAsync(row.Model).ConfigureAwait(false);
            Log.Info("Pages", $"Delete all posts: {result.Message}");
        }

        public async Task SharePostAsync(PageRow row, string link, bool profile, bool groups, int count)
        {
            if (row == null || string.IsNullOrEmpty(link)) return;
            var result = await _svc.PageService.SharePostAsync(row.Model, link, profile, groups, count).ConfigureAwait(false);
            Log.Info("Pages", $"Share post: {result.Message}");
        }

        public async Task JoinGroupAsync(PageRow row, string idOrLink, bool byKeyword, string keyword)
        {
            if (row == null) return;
            var result = await _svc.PageService.JoinGroupAsync(row.Model, idOrLink, byKeyword, keyword).ConfigureAwait(false);
            Log.Info("Pages", $"Join group: {result.Message}");
        }

        public async Task SetInfoAsync(PageRow row, string field, string value)
        {
            if (row == null) return;
            var result = await _svc.PageService.SetPageInfoAsync(row.Model, field, value).ConfigureAwait(false);
            Log.Info("Pages", $"Set page {field}: {result.Message}");
        }

        public int BulkEdit(IEnumerable<long> ids, string field, string value)
        {
            var count = _svc.PageService.BulkEdit(ids, field, value);
            Refresh();
            return count;
        }

        public async Task AutoDeleteCopyrightAsync(PageRow row)
        {
            if (row == null) return;
            var result = await _svc.PageService.AutoDeleteCopyrightAsync(row.Model).ConfigureAwait(false);
            Log.Info("Pages", $"Auto-delete copyright: {result.Message}");
        }

        public async Task<Core.Services.FlowResult> CheckMonetizationAsync(PageRow row)
        {
            if (row == null) return new Core.Services.FlowResult { Success = false, Message = "no page" };
            return await _svc.PageService.CheckMonetizationAsync(row.Model).ConfigureAwait(false);
        }
    }
}
