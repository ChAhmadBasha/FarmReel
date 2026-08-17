using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class LogsView : UserControl
    {
        private LogsViewModel _vm;
        public LogsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public LogsView() { InitializeComponent(); }
        public void RefreshFromDb() => _vm?.RefreshFromDb();

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (_vm != null) _vm.Filter = FilterBox.Text;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => _vm?.RefreshFromDb();
    }
}
