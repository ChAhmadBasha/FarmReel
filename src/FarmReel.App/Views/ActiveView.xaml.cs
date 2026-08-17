using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class ActiveView : UserControl
    {
        private ActiveViewModel _vm;
        public ActiveViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public ActiveView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private InteractionRow Selected => Grid.SelectedItem as InteractionRow;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var deviceId = long.TryParse(InputBox.Ask("Device ID", "0"), out var d) ? d : 0;
            var pageId = long.TryParse(InputBox.Ask("Page ID (0 = account)", "0"), out var p) ? p : 0;
            var kind = InputBox.Ask("Kind (feed|watch|reels|live|story|friends|groups)", "feed");
            var maxActions = int.TryParse(InputBox.Ask("Max actions", "30"), out var a) ? a : 30;
            var maxLikes = int.TryParse(InputBox.Ask("Max likes/reacts", "20"), out var l) ? l : 20;
            var maxComments = int.TryParse(InputBox.Ask("Max comments", "5"), out var c) ? c : 5;
            var maxFollows = int.TryParse(InputBox.Ask("Max follows", "5"), out var f) ? f : 5;
            var minDelay = int.TryParse(InputBox.Ask("Min delay (s)", "3"), out var mn) ? mn : 3;
            var maxDelay = int.TryParse(InputBox.Ask("Max delay (s)", "12"), out var mx) ? mx : 12;
            _vm.AddJob(deviceId, pageId, kind, maxActions, maxLikes, maxComments, maxFollows, minDelay, maxDelay);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeleteJob(Selected);
        private void Run_Click(object sender, RoutedEventArgs e) => _vm.RunNow(Selected);
        private void Reset_Click(object sender, RoutedEventArgs e) => _vm.ResetState(Selected);

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var likeFollow = MessageBox.Show("Like & Follow?", "Active", MessageBoxButton.YesNo);
            var deleteAfter = MessageBox.Show("Delete comment after use?", "Active", MessageBoxButton.YesNo);
            _vm.UpdateJob(Selected, j =>
            {
                j.LikeAndFollow = likeFollow == MessageBoxResult.Yes;
                j.CommentDeleteAfterUse = deleteAfter == MessageBoxResult.Yes;
            });
        }
    }
}
