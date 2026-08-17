using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class GroupsView : UserControl
    {
        private GroupsViewModel _vm;
        public GroupsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public GroupsView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Ask("Group name", "");
            if (!string.IsNullOrEmpty(name)) _vm.AddGroup(name);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
            => _vm.DeleteGroup(GroupGrid.SelectedItem as GroupRow);

        private void Assign_Click(object sender, RoutedEventArgs e)
        {
            var device = DeviceGrid.SelectedItem as DeviceRow;
            var group = GroupGrid.SelectedItem as GroupRow;
            if (device != null && group != null) _vm.AssignGroup(device, group.Id);
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Backup folder" };
            if (dlg.ShowDialog() == true) _vm.BackupGroups(dlg.FolderName);
        }
    }
}
