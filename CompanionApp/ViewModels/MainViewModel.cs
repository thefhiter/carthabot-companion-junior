using AdvancedProgramming.Events;
using AdvancedProgramming.Views;
using BehaveProject.Events;
using BehaveProject.Views;
using CompanionApp.Events;
using CompanionApp.Models;
using CompanionApp.Service;
using CompanionApp.Views;
using LearningProject.Models.Events;
using LearningProject.Views;
using MazeProject.Events;
using MazeProject.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Syncfusion.UI.Xaml.ProgressBar;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CompanionApp.ViewModels
{
    public class StepItem
    {
        public string Title { get; set; }
        public StepStatus Status { get; set; } = StepStatus.Inactive;

    }
    public class MainViewModel : BindableBase
    {
        private DispatcherTimer _timer;

        private ObservableCollection<StepItem> _steps;
        public ObservableCollection<StepItem> Steps
        {
            get { return _steps; }
            set { SetProperty(ref _steps, value); }
        }
        private int _selectedIndex;
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetProperty(ref _selectedIndex, value); }
        }
        private MarkerShapeType _selectedMarkerShape;
        public MarkerShapeType SelectedMarkerShape
        {
            get { return _selectedMarkerShape; }
            set { SetProperty(ref _selectedMarkerShape, value); }
        }

        private StepStatus _selectedItemStatus;
        public StepStatus SelectedItemStatus
        {
            get { return _selectedItemStatus; }
            set { SetProperty(ref _selectedItemStatus, value); }
        }

        private DispatcherTimer dispatcherTimer;


        IEventAggregator _eventAggregator;


        private string sourceFile = string.Empty;

        public DelegateCommand CancelCommand { get; set; }
        public DelegateCommand CloseViewCommand { get; set; }
        public DelegateCommand OpenWebSiteCommand { get; set; }
        public DelegateCommand OpenWebGithubCommand { get; set; }
        public DelegateCommand OpenWebCarthaSoftCommand { get; set; }
        public DelegateCommand OpenAssamblyCommand { get; set; }


        


        /// <summary>/// Prism Property/// </summary>
        private UserControl _view;

        public UserControl View
        {
            get { return _view; }
            set { SetProperty(ref _view, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private Visibility _isViewVisiblity;


        public Visibility IsViewVisiblity
        {
            get { return _isViewVisiblity; }
            set { SetProperty(ref _isViewVisiblity, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private bool _showPlugInAnimation;

        public bool ShowPlugInAnimation
        {
            get { return _showPlugInAnimation; }
            set { SetProperty(ref _showPlugInAnimation, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private Module _selectedModule;

        public Module SelectedModule
        {
            get { return _selectedModule; }
            set { SetProperty(ref _selectedModule, value); }
        }

        public int MyEventHandlerMethod { get; private set; }

        public MainViewModel(IEventAggregator eventAggregator)
        {

            Steps = new ObservableCollection<StepItem>
            {
                new StepItem { Title = "Detecting" },
                new StepItem { Title = "Flashing" },
                new StepItem { Title = "Done" }
            };

            SelectedMarkerShape = MarkerShapeType.Circle;
            SelectedItemStatus = StepStatus.Inactive;
            SelectedIndex = 0;


            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<LoadModuleEvent>().Subscribe(LoadModuleMethod);
            IsViewVisiblity = Visibility.Collapsed;

            ShowPlugInAnimation = false;


            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
            dispatcherTimer.Tick += Check;

            CancelCommand = new DelegateCommand(Cancel);
            CloseViewCommand = new DelegateCommand(CloseViewMethod);

            ///
            _eventAggregator.GetEvent<LearnCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<BehaveCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<MazeCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<AdvancedProgrammingCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<KidsCodingCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<VplCloseEvent>().Subscribe(CloseViewMethod);


            OpenWebSiteCommand = new DelegateCommand(OpenWebSiteMethod);
            OpenWebGithubCommand = new DelegateCommand(OpenWebGithubMethod);
            OpenWebCarthaSoftCommand = new DelegateCommand(OpenWebCarthaSoftMethod);
            OpenAssamblyCommand = new DelegateCommand(OpenAssamblyMethod);




        }

        private async void OpenAssamblyMethod()
        {
            ShowPlugInAnimation = false;
            IsViewVisiblity = Visibility.Visible;

            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);

            await Task.Delay(1500);
            View = new AssamblyView();
        }
        
        private async void OpenWebCarthaSoftMethod()
        {
            ShowPlugInAnimation = false;
            IsViewVisiblity = Visibility.Visible;

            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);

            await Task.Delay(1500);
            View = new LearningMainView(_eventAggregator);
        }

        private void OpenWebGithubMethod()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = IniSupport.GetGitHubUrl(),
                UseShellExecute = true
            });
        }

        private void OpenWebSiteMethod()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"{IniSupport.GetSiteUrl()}/{Settings.Default.Language}",
                UseShellExecute = true
            });
        }

        #region Plug-In Animation Method
        public void Cancel()
        {
            dispatcherTimer.Stop();
            ShowPlugInAnimation = false;

        }

        private async void Check(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();

            try
            {
                SelectedItemStatus = StepStatus.Indeterminate;
                SelectedIndex = 0;

                // Detect RPI-RP2 drive safely
                var drive = DriveInfo
                    .GetDrives()
                    .FirstOrDefault(d => d.DriveType == DriveType.Removable &&
                                         d.IsReady &&
                                         string.Equals(d.VolumeLabel, "RPI-RP2", StringComparison.OrdinalIgnoreCase));


                if (drive == null)
                {
                    dispatcherTimer.Start();

                    return; // No board found, just exit silently

                }
                else
                {

                }

                // Step 1: Flashing start
                SelectedItemStatus = StepStatus.Active;
                var oldComs = SerialPort.GetPortNames().ToList();

                await Task.Delay(1500); // Wait before writing file
                SelectedIndex = 1;
                SelectedItemStatus = StepStatus.Indeterminate;

                // Step 2: Copy file to RPI drive
                string destinationPath = Path.Combine(drive.RootDirectory.FullName, "code.uf2");
                await Task.Run(() => File.Copy(sourceFile, destinationPath, overwrite: true));

                await Task.Delay(1000);
                SelectedItemStatus = StepStatus.Active;


                SelectedIndex = 2;
                await Task.Delay(1000);

                ShowPlugInAnimation = false;
                SelectedItemStatus = StepStatus.Active;




                // Launch correct module view
                switch (SelectedModule)
                {
                    case Module.Learn:
                        {
                            IsViewVisiblity = Visibility.Visible;

                            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);

                            await Task.Delay(1500);
                            View = new LearningMainView(_eventAggregator);
                            break;
                        }

                    case Module.Python:
                        View = new AdvancedProgrammingView(_eventAggregator, oldComs);
                        IsViewVisiblity = Visibility.Visible;
                        _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);
                        break;

                    case Module.KidsCoding:
                        View = new KidsCodingView(_eventAggregator, oldComs);
                        IsViewVisiblity = Visibility.Visible;
                        _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);
                        break;

                    case Module.VplJunior:
                        View = new CarthaBotVPL.Views.VplView(_eventAggregator, oldComs);
                        IsViewVisiblity = Visibility.Visible;
                        _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);
                        break;

                    case Module.Explore:
                        View = new MazeMainView(_eventAggregator, oldComs);
                        IsViewVisiblity = Visibility.Visible;
                        _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);
                        break;

                    case Module.Behaviour:
                        View = new BehaviorMainView(_eventAggregator, Settings.Default.Language);
                        IsViewVisiblity = Visibility.Visible;
                        _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);
                        _eventAggregator.GetEvent<LoadPDFEvent>().Publish("Commande_Boutons.pdf");
                        break;
                }

                SelectedItemStatus = StepStatus.Active;

            }
            catch (Exception ex)
            {
                // Log or show user message if something fails
                Debug.WriteLine($"Error in Check(): {ex.Message}");
            }
        }


        #endregion

        private void LoadModuleMethod(Module obj)
        {
            SelectedItemStatus = StepStatus.Inactive;
            SelectedIndex = 0;

            SelectedModule = obj;
            dispatcherTimer.Start();
            ShowPlugInAnimation = true;

            string sourceFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources/u2f");
            switch (obj)
            {
                case Module.Learn:
                    sourceFile = Path.Combine(sourceFolder, "BootLoader_microPython.uf2");
                    break;
                case Module.Python:
                    sourceFile = Path.Combine(sourceFolder, "BootLoader_microPython.uf2");
                    break;
                case Module.KidsCoding:
                    sourceFile = Path.Combine(sourceFolder, "BootLoader_microPython.uf2");
                    break;
                case Module.VplJunior:
                    sourceFile = Path.Combine(sourceFolder, "BootLoader_microPython.uf2");
                    break;
                case Module.Explore:

                    sourceFile = Path.Combine(sourceFolder, "MazeCode.uf2");

                    break;
                case Module.Behaviour:
                    sourceFile = Path.Combine(sourceFolder, "Modes.uf2");

                    break;
                default:
                    break;
            }

            /*_dialogService.ShowDialog("PlugAndPowerOnView", new DialogParameters
            {
                {"module",obj}
            }, result =>
            {
                if (result.Parameters.Count > 0)
                {
                    switch(obj)
                    {
                        case Module.Learn:
                            string sourceFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CarthaSoft");
                            string sourceFile = Path.Combine(sourceFolder, "CarthaSoft.html");
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = sourceFile,
                                UseShellExecute = true // Required for opening files in the default application
                            });

                            View = new LearningMainView(_eventAggregator);
                            IsViewVisiblity = Visibility.Visible;
                            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);

                            break;
                        case Module.Explore:

                            View = new MazeMainView(_eventAggregator);
                            IsViewVisiblity = Visibility.Visible;
                            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true); 

                            break;
                        case Module.Behaviour:
                            break;
                        default:
                            break;
                    }


                }



            }, "PlugAndPowerOnShell");

           */
        }

        private void CloseViewMethod()
        {
            View = null;
            IsViewVisiblity = Visibility.Collapsed;
            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(false);

        }


    }
}
