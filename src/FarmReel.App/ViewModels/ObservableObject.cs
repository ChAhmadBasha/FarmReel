using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace FarmReel.App.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    /// <summary>Runs an action on the UI thread (used from background workers).</summary>
    public static class Ui
    {
        public static Dispatcher Dispatcher;

        public static void Run(Action action)
        {
            if (Dispatcher == null || Dispatcher.CheckAccess()) action();
            else Dispatcher.BeginInvoke(action);
        }
    }
}
