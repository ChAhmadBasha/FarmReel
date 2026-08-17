using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class DashboardView : UserControl
    {
        private MainViewModel _vm;
        public MainViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public DashboardView()
        {
            InitializeComponent();
        }
    }
}
