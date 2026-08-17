using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class AccountsView : UserControl
    {
        private AccountsViewModel _vm;
        public AccountsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public AccountsView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private AccountRow Selected => Grid.SelectedItem as AccountRow;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var email = InputBox.Ask("Email or UID", "");
            if (string.IsNullOrEmpty(email)) return;
            var password = InputBox.Ask("Password", "");
            var totp = InputBox.Ask("2FA secret (optional)", "");
            var phone = InputBox.Ask("Phone (optional)", "");
            var dob = InputBox.Ask("Date of birth (optional, used for appeal)", "");
            _vm.AddAccount(email, password, totp, phone, dob);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeleteAccount(Selected);

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Text files (*.txt;*.csv)|*.txt;*.csv|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _vm.ImportText = System.IO.File.ReadAllText(dlg.FileName);
                _vm.ImportFromText();
            }
        }

        private async void Live_Click(object sender, RoutedEventArgs e) => await _vm.CheckLiveAsync(Selected);
        private async void LiveAll_Click(object sender, RoutedEventArgs e) => await _vm.CheckLiveAllAsync();
        private async void Login_Click(object sender, RoutedEventArgs e) => await _vm.LoginAsync(Selected);

        private void Copy2Fa_Click(object sender, RoutedEventArgs e) => _vm.Copy2Fa(Selected);

        private void Set2Fa_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var secret = InputBox.Ask("TOTP secret", "");
            if (secret != null) _vm.Set2Fa(Selected, secret);
        }

        private async void Appeal_Click(object sender, RoutedEventArgs e) => await _vm.AppealAsync(Selected);
        private async void Unlock_Click(object sender, RoutedEventArgs e) => await _vm.Unlock282Async(Selected);

        private async void CreatePage_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var name = InputBox.Ask("Page name", "");
            var cat = InputBox.Ask("Category", "Business");
            if (!string.IsNullOrEmpty(name)) await _vm.CreatePageAsync(Selected, name, cat);
        }

        private async void ProMode_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var answer = MessageBox.Show("Enable Professional mode?", "Pro Mode", MessageBoxButton.YesNo);
            await _vm.ToggleProModeAsync(Selected, answer == MessageBoxResult.Yes);
        }

        private async void Friend_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var uid = InputBox.Ask("Friend UID or profile link", "");
            if (!string.IsNullOrEmpty(uid)) await _vm.AddFriendAsync(Selected, uid);
        }

        private async void JoinGroup_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var g = InputBox.Ask("Group ID or link", "");
            if (!string.IsNullOrEmpty(g)) await _vm.JoinGroupAsync(Selected, g);
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var link = InputBox.Ask("Post link", "");
            var count = int.TryParse(InputBox.Ask("Share to how many groups (0 = profile only)", "0"), out var c) ? c : 0;
            if (!string.IsNullOrEmpty(link)) await _vm.SharePostAsync(Selected, link, count == 0, count > 0, count);
        }

        private async void Ig_Click(object sender, RoutedEventArgs e) => await _vm.CreateInstagramAsync(Selected);

        private async void PullNames_Click(object sender, RoutedEventArgs e)
        {
            var serial = InputBox.Ask("ADB serial of the device to pull names from", "");
            if (string.IsNullOrEmpty(serial)) return;
            var dev = new DeviceRow
            {
                Model = new FarmReel.Core.Models.DeviceInstance { Name = serial, AdbSerial = serial, PackageName = "com.facebook.katana" }
            };
            await _vm.PullNamesAsync(dev);
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (_vm != null) _vm.Filter = FilterBox.Text;
        }

        private async void ManualLogin_Click(object sender, RoutedEventArgs e) => await _vm.ManualLoginAsync(Selected);
        private async void ManualBackup_Click(object sender, RoutedEventArgs e) => await _vm.ManualBackupAsync(Selected);
        private async void ConfirmFriends_Click(object sender, RoutedEventArgs e) => await _vm.ConfirmFriendsAsync(Selected);

        private async void LeaveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var g = InputBox.Ask("Group ID or link", "");
            if (!string.IsNullOrEmpty(g)) await _vm.LeaveGroupAsync(Selected, g);
        }

        private async void GroupSuggest_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var kw = InputBox.Ask("Search keyword", "");
            if (!string.IsNullOrEmpty(kw)) await _vm.GroupSuggestionsAsync(Selected, kw);
        }

        private async void PostToGroup_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var g = InputBox.Ask("Group ID or link", "");
            var text = InputBox.Ask("Post text", "");
            if (!string.IsNullOrEmpty(g)) await _vm.PostToGroupAsync(Selected, g, text ?? "");
        }

        private async void CheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var photo = InputBox.Ask("Photo path (optional)", "");
            var loc = InputBox.Ask("Location (empty = auto)", "");
            await _vm.CheckInPostAsync(Selected, photo ?? "", loc ?? "");
        }

        private async void Story_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var photo = InputBox.Ask("Photo path", "");
            var link = InputBox.Ask("Link (optional)", "");
            if (!string.IsNullOrEmpty(photo)) await _vm.CreateStoryAsync(Selected, photo, link ?? "");
        }

        private async void Live_Click2(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var link = InputBox.Ask("Live video link", "");
            if (!string.IsNullOrEmpty(link)) await _vm.WatchLiveAsync(Selected, link);
        }

        private async void ViewStory_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var link = InputBox.Ask("Story link", "");
            if (!string.IsNullOrEmpty(link)) await _vm.ViewStoryAsync(Selected, link);
        }

        private async void Notif_Click(object sender, RoutedEventArgs e) => await _vm.CheckNotificationsAsync(Selected);

        private async void Review_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var link = InputBox.Ask("Page link to review", "");
            var text = InputBox.Ask("Review text", "");
            if (!string.IsNullOrEmpty(link)) await _vm.ReviewPageAsync(Selected, link, text ?? "");
        }

        private async void Location_Click(object sender, RoutedEventArgs e) => await _vm.CheckPrimaryLocationAsync(Selected);
    }
}
