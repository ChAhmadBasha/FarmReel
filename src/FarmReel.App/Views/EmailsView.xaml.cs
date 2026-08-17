using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class EmailsView : UserControl
    {
        private EmailsViewModel _vm;
        public EmailsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public EmailsView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private EmailRow Selected => Grid.SelectedItem as EmailRow;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var email = InputBox.Ask("Email address", "");
            if (string.IsNullOrEmpty(email)) return;
            var password = InputBox.Ask("App password", "");
            var provider = InputBox.Ask("Provider (zoho|gmail|outlook|yandex)", "zoho");
            var trusted = MessageBox.Show("Mark as trusted mail?", "Emails", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            _vm.AddEmail(email, password, provider, trusted);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeleteEmail(Selected);

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var receiver = InputBox.Ask("Receiver email (where Facebook sends the code)", "");
            if (!string.IsNullOrEmpty(receiver))
            {
                var code = await _vm.TestGetCodeAsync(Selected, receiver);
                MessageBox.Show(string.IsNullOrEmpty(code) ? "No code received (timeout)" : "Code: " + code,
                    "Email OTP", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Stock_Click(object sender, RoutedEventArgs e)
        {
            var stock = await _vm.FetchStockAsync();
            MessageBox.Show(stock.Count == 0 ? "No stock info (check Logs tab / server config)" : string.Join("\n", stock),
                "Mail stock", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Order_Click(object sender, RoutedEventArgs e)
        {
            var count = int.TryParse(InputBox.Ask("How many mail accounts?", "5"), out var c) ? c : 5;
            var provider = InputBox.Ask("Provider (outlook|gmail|zoho, empty = any)", "outlook");
            var added = await _vm.OrderStockAsync(count, provider ?? "");
            MessageBox.Show($"Delivered {added} mail account(s) into the Emails list.",
                "Order", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
