using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class PagesView : UserControl
    {
        private PagesViewModel _vm;
        public PagesViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public PagesView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private PageRow Selected => Grid.SelectedItem as PageRow;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var accountId = long.TryParse(InputBox.Ask("Account ID", "0"), out var id) ? id : 0;
            var name = InputBox.Ask("Page name", "");
            var pageId = InputBox.Ask("Page ID (optional)", "");
            var mode = InputBox.Ask("Identity mode (page|profile|professional)", "page");
            if (!string.IsNullOrEmpty(name)) _vm.AddPage(accountId, name, pageId, mode);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeletePage(Selected);

        private async void Dashboard_Click(object sender, RoutedEventArgs e) => await _vm.PullDashboardAsync(Selected);
        private async void DeletePosts_Click(object sender, RoutedEventArgs e) => await _vm.DeleteAllPostsAsync(Selected);

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var link = InputBox.Ask("Post link", "");
            var count = int.TryParse(InputBox.Ask("Groups to share to (0 = profile only)", "0"), out var c) ? c : 0;
            if (!string.IsNullOrEmpty(link)) await _vm.SharePostAsync(Selected, link, count == 0, count > 0, count);
        }

        private async void JoinGroup_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var link = InputBox.Ask("Group ID or link (empty = search keyword)", "");
            var keyword = InputBox.Ask("Keyword (if searching)", "");
            await _vm.JoinGroupAsync(Selected, link, !string.IsNullOrEmpty(keyword), keyword);
        }

        private async void SetInfo_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var field = InputBox.Ask("Field (profile|cover|bio|email|address)", "bio");
            var value = InputBox.Ask("Value or file path", "");
            if (value != null) await _vm.SetInfoAsync(Selected, field, value);
        }

        private async void Copyright_Click(object sender, RoutedEventArgs e) => await _vm.AutoDeleteCopyrightAsync(Selected);

        private void BulkEdit_Click(object sender, RoutedEventArgs e)
        {
            var ids = Grid.SelectedItems.Cast<PageRow>().Select(p => p.Id).ToList();
            if (ids.Count == 0) return;
            var field = InputBox.Ask("Field (status|identitymode|monetization|notes)", "status");
            var value = InputBox.Ask("Value", "");
            if (value != null) _vm.BulkEdit(ids, field, value);
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (_vm != null) _vm.PageFilter = FilterBox.Text;
        }

        private async void Monetization_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var result = await _vm.CheckMonetizationAsync(Selected);
            Ui.Run(() => Log("Monetization check: " + result.Message));
        }

        private static void Log(string message) => FarmReel.Core.Utils.Log.Info("Pages", message);
    }
}
