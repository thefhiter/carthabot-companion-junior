using CompanionApp.Events;
using CompanionApp.Models.Classes;
using CompanionApp.Service;
using DMSkin.WPF;
using Prism.Events;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;

namespace CompanionApp.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow 
    {

        IEventAggregator _eventAggregator;

        private Notifier _notifier;

        public MainWindow(IEventAggregator eventAggregator)
        {
            InitializeComponent();

            _eventAggregator = eventAggregator;
            SetWindowSize();

        }

        private void SetWindowSize()
        {
            // Get screen width and height

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Set minimum window width and height to 80% of screen size
            this.MinWidth = screenWidth * 0.8;
            this.MinHeight = screenHeight * 0.8;

            // Optionally, center the window on the screen
            //this.Left = (screenWidth - this.MinWidth) / 2;
            //this.Top = (screenHeight - this.MinHeight) / 2;

        }


        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string version = await CheckVersion.IsUpToDate(IniSupport.GetVersion());



            if (version == "uptodate"){

                _notifier = new Notifier(cfg =>
                {

                    cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(TimeSpan.FromSeconds(3), MaximumNotificationCount.FromCount(15));
                    cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: Application.Current.MainWindow,
                    corner: Corner.BottomRight,
                    offsetX: 10,
                    offsetY: 10);

                });


                _notifier.ShowCustomMessageErrSucc("Version up to date");
                return;
            }
            else if (string.IsNullOrEmpty(version))
            {
                _notifier = new Notifier(cfg =>
                {

                    cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(TimeSpan.FromSeconds(3), MaximumNotificationCount.FromCount(15));
                    cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: Application.Current.MainWindow,
                    corner: Corner.BottomRight,
                    offsetX: 10,
                    offsetY: 10);

                });

                _notifier.ShowCustomMessageErrSucc("Network Error\nUnable to check for update");
                return;
            }
            _eventAggregator.GetEvent<NewVersionAvaliableEvent>().Publish(version);
            await Task.Delay(2000);

            _notifier = new Notifier(cfg =>
            {

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(TimeSpan.FromSeconds(5), MaximumNotificationCount.FromCount(15));
                cfg.PositionProvider = new WindowPositionProvider(
                parentWindow: Application.Current.MainWindow,
                corner: Corner.BottomRight,
                offsetX: 10,
                offsetY: 10);

            });

            _notifier.ShowCustomMessage(new string[] { "New Version is Available", version });
        }


        private void Image_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {


            var image = sender as Image;
            if (image != null)
            {
                var toolTip = image.ToolTip as ToolTip;
                if (toolTip != null)
                {
                    toolTip.IsOpen = true;
                }
            }

        }
    }
}
