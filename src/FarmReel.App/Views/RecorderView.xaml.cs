using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class RecorderView : UserControl
    {
        private RecorderViewModel _vm;
        public RecorderViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public RecorderView() { InitializeComponent(); }
        public void RefreshDevices() => _vm?.RefreshDevices();

        private void Start_Click(object sender, RoutedEventArgs e) => _vm?.Start();
        private void Stop_Click(object sender, RoutedEventArgs e) => _vm?.Stop();
        private void Save_Click(object sender, RoutedEventArgs e) => _vm?.Save();
        private void Clear_Click(object sender, RoutedEventArgs e) => _vm?.Clear();

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null || !_vm.IsRecording) return;
            var img = (Image)sender;
            var pos = e.GetPosition(img);
            if (img.ActualWidth <= 1 || img.ActualHeight <= 1) return;
            var scaleX = (double)_vm.PreviewWidth / img.ActualWidth;
            var scaleY = (double)_vm.PreviewHeight / img.ActualHeight;
            _vm.RecordTap((int)(pos.X * scaleX), (int)(pos.Y * scaleY));
        }
    }
}
