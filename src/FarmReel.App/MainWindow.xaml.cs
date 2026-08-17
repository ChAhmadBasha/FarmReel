using System.Windows;
using FarmReel.App.ViewModels;

namespace FarmReel.App
{
    public partial class MainWindow : Window
    {
        private readonly ServiceLocator _svc;
        public MainViewModel Main { get; }

        public MainWindow(ServiceLocator svc)
        {
            InitializeComponent();
            _svc = svc;
            Main = new MainViewModel(svc);
            DashboardTab.ViewModel = Main;
            DevicesTab.ViewModel = new DevicesViewModel(svc);
            AccountsTab.ViewModel = new AccountsViewModel(svc);
            PagesTab.ViewModel = new PagesViewModel(svc);
            PostsTab.ViewModel = new PostsViewModel(svc);
            ActiveTab.ViewModel = new ActiveViewModel(svc);
            GroupsTab.ViewModel = new GroupsViewModel(svc);
            EmailsTab.ViewModel = new EmailsViewModel(svc);
            TemplatesTab.ViewModel = PostsTab.ViewModel; // shared template store
            FlowsTab.ViewModel = new FlowsViewModel(svc);
            RecorderTab.ViewModel = new RecorderViewModel(svc);
            LogsTab.ViewModel = new LogsViewModel(svc);
            SettingsTab.ViewModel = new SettingsViewModel(svc);
            DataContext = Main;
            RefreshAll();
            Loaded += (s, e) => Main.MaybeAutoStart();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            Main.ToggleRun();
            StatusText.Text = Main.Status;
            RunButton.Content = Main.IsRunning ? "Stop" : "Run";
            LogLine(Main.Status == "Running" ? "Automation started" : "Automation stopped");
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        public void RefreshAll()
        {
            Main.Refresh();
            SummaryText.Text = Main.Summary;
            DevicesTab.Refresh();
            AccountsTab.Refresh();
            PagesTab.Refresh();
            PostsTab.Refresh();
            ActiveTab.Refresh();
            GroupsTab.Refresh();
            EmailsTab.Refresh();
            LogsTab.RefreshFromDb();
        }

        private void LogLine(string message)
        {
            FarmReel.Core.Utils.Log.Info("App", message);
        }
    }
}
