using System.Windows;
using FarmReel.App.ViewModels;

namespace FarmReel.App
{
    public partial class App : Application
    {
        private ServiceLocator _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _services = new ServiceLocator();
            var window = new MainWindow(_services);
            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            base.OnExit(e);
        }
    }
}
