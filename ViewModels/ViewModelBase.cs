using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace AsiErp.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsActive { get; set; }
        public bool IsStale { get; set; } = true;

        public virtual void OnActivated()
        {
            IsActive = true;
            if (IsStale)
            {
                IsStale = false;
                RefreshData();
            }
        }

        public virtual void OnDeactivated()
        {
            IsActive = false;
        }

        public virtual void RefreshData() { }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void DebounceAction(ref DispatcherTimer? timer, Action action, int milliseconds = 200)
        {
            var app = System.Windows.Application.Current;
            if (app == null || app.Dispatcher == null)
            {
                action();
                return;
            }

            if (!app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(action);
                return;
            }

            if (timer == null)
            {
                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(milliseconds)
                };
                timer.Tick += (s, e) =>
                {
                    if (s is DispatcherTimer dt)
                    {
                        dt.Stop();
                    }
                    action();
                };
            }
            timer.Stop();
            timer.Start();
        }
    }
}
