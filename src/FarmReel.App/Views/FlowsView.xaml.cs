using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class FlowsView : UserControl
    {
        private FlowsViewModel _vm;
        public FlowsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public FlowsView()
        {
            InitializeComponent();
        }

        private void FlowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && FlowList.SelectedItem is string name)
                _vm.Load(name);
        }

        private void New_Click(object sender, System.Windows.RoutedEventArgs e) => _vm?.NewFlow();
        private void Load_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (FlowList.SelectedItem is string name) _vm?.Load(name);
        }
        private void Validate_Click(object sender, System.Windows.RoutedEventArgs e) => _vm?.Validate();
        private void Save_Click(object sender, System.Windows.RoutedEventArgs e) => _vm?.Save();
        private void Delete_Click(object sender, System.Windows.RoutedEventArgs e) => _vm?.Delete();
    }
}
