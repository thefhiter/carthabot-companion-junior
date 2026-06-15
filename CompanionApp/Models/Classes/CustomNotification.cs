using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ToastNotifications.Core;
using System.ComponentModel;
using ToastNotifications.Core;
using ToastNotifications;
using DMSkin.Core.MVVM;
using CompanionApp.Service;
using ToastNotifications.Messages.Error;

namespace CompanionApp.Models.Classes
{
    public class CustomNotification : NotificationBase, INotifyPropertyChanged
    {

        public DelegateCommand UpdateCommand { get; set; }
        private CustomDisplayPart _displayPart;

        public override NotificationDisplayPart DisplayPart => _displayPart ?? (_displayPart = new CustomDisplayPart(this));

        private string _message;
        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        private string _version;
        public string Version
        {
            get
            {
                return _version;
            }
            set
            {
                _version = value;
                OnPropertyChanged();
            }
        }
        public CustomNotification(string[] message) : base (message[0],new MessageOptions())
        {
            Message = message[0];
            Version = $"{message[1]}";

            DisplayPart.Height = 100;
            UpdateCommand = new DelegateCommand(UpdateMethod);
        }

        private void UpdateMethod(object obj)
        {
            this.Close();
            CheckVersion.OpenNewVersion(Version);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class CustomNotificationErrSucc : NotificationBase, INotifyPropertyChanged
    {

        public DelegateCommand CloseCommand { get; set; }
        private CustomDisplayPartErrSucc _displayPart;

        public override NotificationDisplayPart DisplayPart => _displayPart ?? (_displayPart = new CustomDisplayPartErrSucc(this));

        private string _message;
        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }
        private bool _isError;
        public bool IsError
        {
            get
            {
                return _isError;
            }
            set
            {
                _isError = value;
                OnPropertyChanged();
            }
        }

        public CustomNotificationErrSucc(string message) : base(message, new MessageOptions())
        {
            Message = message;
            IsError = message.ToLower().Contains("error");

            DisplayPart.Height = 100;
            CloseCommand = new DelegateCommand(CloseMethod);
        }

        private void CloseMethod(object obj)
        {
            this.Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public static class CustomMessageExtensions
    {
        public static void ShowCustomMessage(this Notifier notifier, string[] message)
        {
            notifier.Notify<CustomNotification>(() => new CustomNotification(message));
        }

        public static void ShowCustomMessageErrSucc(this Notifier notifier, string message)
        {
            notifier.Notify<CustomNotificationErrSucc>(() => new CustomNotificationErrSucc(message));
        }


    }
}
