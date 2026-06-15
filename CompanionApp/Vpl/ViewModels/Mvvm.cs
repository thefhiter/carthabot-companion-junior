using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CarthaBotVPL.ViewModels
{
    /// <summary>Minimal INotifyPropertyChanged base so the app stays dependency-free.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }

        protected void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Lightweight ICommand for MVVM wiring.</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _can;

        public RelayCommand(Action<object> execute, Func<object, bool> can = null)
        {
            _execute = execute;
            _can = can;
        }

        public RelayCommand(Action execute, Func<bool> can = null)
        {
            _execute = _ => execute();
            if (can != null) _can = _ => can();
        }

        public bool CanExecute(object parameter) => _can == null || _can(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
