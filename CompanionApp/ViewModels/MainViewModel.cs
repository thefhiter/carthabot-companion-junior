using AdvancedProgramming.Events;
using AdvancedProgramming.Views;
using CarthaBotVPL.Services;
using BehaveProject.Events;
using BehaveProject.Views;
using CompanionApp.Events;
using CompanionApp.Models;
using CompanionApp.Service;
using CompanionApp.Views;
using CompanionApp.Wireless;
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

        // ---- Connection chooser (shown BEFORE the USB flash wizard) ----
        private bool _showConnectionChooser;
        public bool ShowConnectionChooser
        {
            get { return _showConnectionChooser; }
            set { SetProperty(ref _showConnectionChooser, value); }
        }

        private string _wifiEndpoint = "carthabot.local:3333";
        public string WifiEndpoint
        {
            get { return _wifiEndpoint; }
            set { SetProperty(ref _wifiEndpoint, value); }
        }

        public DelegateCommand<string> ChooseConnectionCommand { get; set; }

        // Kids screen: let a grown-up connect the already-set-up robot over WiFi (no cable).
        public DelegateCommand KidsWifiCommand { get; set; }

        // ---- In-app Wi-Fi provisioning: "Connect CarthaBot to your Wi-Fi" ----
        private bool _showWifiSetup;
        public bool ShowWifiSetup { get => _showWifiSetup; set => SetProperty(ref _showWifiSetup, value); }

        // True when the Wi-Fi setup was auto-opened from the connection chooser (first-time flow), so a
        // successful save should flow straight on to opening the chosen module over Wi-Fi.
        private bool _wifiFromChooser;

        private bool _isScanning;
        public bool IsScanning { get => _isScanning; set => SetProperty(ref _isScanning, value); }

        public ObservableCollection<WifiNet> ScannedNetworks { get; } = new ObservableCollection<WifiNet>();

        private WifiNet _selectedNetwork;
        public WifiNet SelectedNetwork
        {
            get => _selectedNetwork;
            set { if (SetProperty(ref _selectedNetwork, value) && value != null) WifiSsidInput = value.Ssid; }
        }

        // Typed Wi-Fi name (so the user can just type it — no scan / no CarthaBot-WiFi needed).
        private string _wifiSsidInput = "";
        public string WifiSsidInput { get => _wifiSsidInput; set => SetProperty(ref _wifiSsidInput, value); }

        private string _wifiPassword = "";
        public string WifiPassword { get => _wifiPassword; set => SetProperty(ref _wifiPassword, value); }

        private string _provisionStatus = "";
        public string ProvisionStatus { get => _provisionStatus; set => SetProperty(ref _provisionStatus, value); }

        public DelegateCommand OpenWifiSetupCommand { get; set; }
        public DelegateCommand ScanWifiCommand { get; set; }
        public DelegateCommand JoinWifiCommand { get; set; }
        public DelegateCommand SaveWifiOverUsbCommand { get; set; }
        public DelegateCommand CloseWifiSetupCommand { get; set; }
        public DelegateCommand SkipWifiCommand { get; set; }

        // ---- Kid-friendly connect screen (under-6 VPL only) ----
        private bool _showKidsConnect;
        public bool ShowKidsConnect
        {
            get { return _showKidsConnect; }
            set { SetProperty(ref _showKidsConnect, value); }
        }

        private string _kidsConnectStatus = "Looking for your CarthaBot…";
        public string KidsConnectStatus
        {
            get { return _kidsConnectStatus; }
            set { SetProperty(ref _kidsConnectStatus, value); }
        }

        // resource lookup with a safe fallback (keeps the kid screen localized)
        private static string L(string key, string fallback)
            => Application.Current?.TryFindResource(key) as string ?? fallback;

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
            ChooseConnectionCommand = new DelegateCommand<string>(ChooseConnection);
            KidsWifiCommand = new DelegateCommand(() => KidsChoose("Wifi"));

            OpenWifiSetupCommand = new DelegateCommand(() => { _wifiFromChooser = false; OpenWifiSetup(); });
            ScanWifiCommand = new DelegateCommand(async () => await ScanWifi());
            JoinWifiCommand = new DelegateCommand(async () => await JoinWifi());
            SaveWifiOverUsbCommand = new DelegateCommand(async () => await SaveWifiOverUsb());
            CloseWifiSetupCommand = new DelegateCommand(() => ShowWifiSetup = false);
            SkipWifiCommand = new DelegateCommand(SkipWifiSetup);

            ///
            _eventAggregator.GetEvent<LearnCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<BehaveCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<MazeCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<AdvancedProgrammingCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<KidsCodingCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<VplCloseEvent>().Subscribe(CloseViewMethod);
            _eventAggregator.GetEvent<DrawCloseEvent>().Subscribe(CloseViewMethod);
            // A module's in-view picker hit first-time Wi-Fi → show the registration screen on top.
            _eventAggregator.GetEvent<OpenWifiSetupEvent>().Subscribe(
                () => { _wifiFromChooser = false; OpenWifiSetup(); }, ThreadOption.UIThread);


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
            ShowConnectionChooser = false;
            ShowKidsConnect = false;
        }

        private async void Check(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();

            // Only keep polling for the USB board while a connect screen is actually open.
            // If the user already left it (picked WiFi on the kids screen, or cancelled),
            // stop here — don't flash a later-plugged robot or restart the poll.
            if (!ShowPlugInAnimation && !ShowKidsConnect)
                return;

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
                KidsConnectStatus = L("kidsConnFound", "Found it! Getting ready… ✨");
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
                ShowKidsConnect = false;
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

                    case Module.Draw:
                        View = new CompanionApp.Draw.Views.DrawView(_eventAggregator, oldComs);
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
                case Module.Draw:
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

            // The Behaviours module flashes its own Modes.uf2 firmware, which can ONLY happen
            // over USB — showing a WiFi option there was misleading (it was silently forced to
            // USB anyway), so skip the chooser and go straight to the plug-in / flash wizard.
            if (obj == Module.Behaviour)
            {
                ShowPlugInAnimation = true;
                dispatcherTimer.Start();
                return;
            }

            // Every other module — including the under-6 VPL — uses the USB / WiFi chooser.
            // (The old kid-friendly "Let's wake up CarthaBot!" connect screen is disabled.)
            ShowConnectionChooser = true;

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

        // User picked USB / WiFi in the chooser.
        private void ChooseConnection(string modeStr)
        {
            ShowConnectionChooser = false;

            if (!Enum.TryParse(modeStr, true, out ConnectionMode mode)) mode = ConnectionMode.Usb;

            // Modules that ship a custom .uf2 firmware (Explore/Maze, Behaviour) can ONLY be
            // delivered by flashing over USB — there is no wireless path for a UF2 flash. Force
            // those onto the USB wizard no matter what the user picked in the chooser.
            bool requiresUf2Flash = SelectedModule == Module.Explore || SelectedModule == Module.Behaviour;

            if (mode == ConnectionMode.Usb || requiresUf2Flash)
            {
                // Existing path: show the plug/flash wizard, which flashes then opens the module.
                SelectedItemStatus = StepStatus.Inactive;
                SelectedIndex = 0;
                ShowPlugInAnimation = true;
                dispatcherTimer.Start();
            }
            else
            {
                // WiFi: no flashing. FIRST time on Wi-Fi from this PC? Put the robot on the network
                // first (scan → pick name → password → save). After that, connect straight to it.
                if (!WifiState.IsProvisioned)
                {
                    _wifiFromChooser = true;
                    OpenWifiSetup();
                    return;
                }
                if (!string.IsNullOrWhiteSpace(WifiState.Endpoint)) WifiEndpoint = WifiState.Endpoint;
                OpenModuleWireless(mode);
            }
        }

        // "Skip — it's already on Wi-Fi": trust the saved address, remember it's set up, and continue.
        private void SkipWifiSetup()
        {
            WifiState.MarkProvisioned(WifiEndpoint, WifiSsidInput?.Trim());
            ShowWifiSetup = false;
            if (_wifiFromChooser) OpenModuleWireless(ConnectionMode.Wifi);
        }

        // The under-6 kids screen can skip the USB flash and connect the already-set-up robot
        // wirelessly. USB keeps the friendly auto-detect that is already polling; WiFi stops
        // that poll, hide the kid screen, and open the VPL straight over the ESP32-C3 bridge.
        private void KidsChoose(string modeStr)
        {
            if (!Enum.TryParse(modeStr, true, out ConnectionMode mode) || mode == ConnectionMode.Usb)
                return;

            dispatcherTimer.Stop();
            ShowKidsConnect = false;
            OpenModuleWireless(mode);
        }

        // ---- In-app Wi-Fi provisioning: scan -> pick -> join -> code on your own network ----
        private void OpenWifiSetup()
        {
            ShowConnectionChooser = false;
            ScannedNetworks.Clear();
            SelectedNetwork = null;
            WifiSsidInput = "";
            WifiPassword = "";
            ProvisionStatus = "Reading your PC's Wi-Fi…";
            ShowWifiSetup = true;
            _ = ScanWifi();         // fill the manual picker with saved networks
            _ = AutoFillPcWifi();   // auto-fill the PC's current network + password (manual override available)
        }

        // Auto-fill the panel with the Wi-Fi THIS PC is currently on (+ its saved password) so the robot
        // can join the same network in one click. Both fields stay editable (manual override).
        private async Task AutoFillPcWifi()
        {
            try
            {
                var (ssid, pass) = await Task.Run(() => GetCurrentPcWifi());
                if (!string.IsNullOrWhiteSpace(ssid))
                {
                    WifiSsidInput = ssid;
                    if (!string.IsNullOrEmpty(pass)) WifiPassword = pass;
                    ProvisionStatus = $"Auto-filled your PC's Wi-Fi: '{ssid}'. Plug the robot in with the USB cable and press Save — or change it below.";
                }
                else ProvisionStatus = "Plug the robot in, pick (or type) your Wi-Fi + password, then 'Save to robot'.";
            }
            catch { ProvisionStatus = "Plug the robot in, pick (or type) your Wi-Fi + password, then 'Save to robot'."; }
        }

        // (ssid, password) of the Wi-Fi this PC is connected to — best effort; user can override.
        private static (string ssid, string pass) GetCurrentPcWifi()
        {
            try
            {
                string connName = RunCmd("powershell",
                    "-NoProfile -Command \"Get-NetConnectionProfile | Where-Object { $_.InterfaceAlias -like '*Wi-Fi*' } | Select-Object -First 1 -ExpandProperty Name\"")?.Trim();
                if (string.IsNullOrWhiteSpace(connName)) return (null, "");
                // Windows may append a ' 2' disambiguation suffix — resolve to the real SSID via saved profiles.
                var saved = GetKnownWifiNetworks().Select(w => w.Ssid).ToList();
                string ssid = saved.Where(p => connName == p || connName.StartsWith(p + " "))
                                   .OrderByDescending(p => p.Length).FirstOrDefault() ?? connName;
                return (ssid, GetSavedPassword(ssid));
            }
            catch { return (null, ""); }
        }

        // The saved password for an SSID, from the exported profile XML (<keyMaterial>, locale-independent).
        private static string GetSavedPassword(string ssid)
        {
            try
            {
                string folder = Path.Combine(Path.GetTempPath(), "cbwifi_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(folder);
                RunCmd("netsh", $"wlan export profile name=\"{ssid}\" key=clear folder=\"{folder}\"");
                string pass = "";
                var xml = Directory.GetFiles(folder, "*.xml").FirstOrDefault();
                if (xml != null)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(xml), "<keyMaterial>(.*?)</keyMaterial>");
                    if (m.Success) pass = m.Groups[1].Value;
                }
                try { Directory.Delete(folder, true); } catch { }
                return pass;
            }
            catch { return ""; }
        }

        private static string RunCmd(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(6000); return o; }
            }
            catch { return null; }
        }

        // Populate the picker with the Wi-Fi networks THIS PC already knows (saved profiles) — reliable,
        // needs no admin/location (a live scan does), and the user's home network is always here, so they
        // pick it from a list instead of mistyping the SSID. The typed box stays as a fallback.
        private async Task ScanWifi()
        {
            try
            {
                // live nearby networks (netsh) merged with saved profiles — robot APs removed
                var nets = await Task.Run(() => WifiProvisioner.ScanNetworks());
                ScannedNetworks.Clear();
                foreach (var n in nets) ScannedNetworks.Add(n);
            }
            catch { /* the typed SSID box is always available as a fallback */ }
        }

        private static System.Collections.Generic.List<WifiNet> GetKnownWifiNetworks()
        {
            var list = new System.Collections.Generic.List<WifiNet>();
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show profiles")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(4000);
                    foreach (var raw in outp.Split('\n'))
                    {
                        int c = raw.LastIndexOf(':');
                        if (c < 0) continue;
                        string ssid = raw.Substring(c + 1).Trim();
                        if (ssid.Length == 0 || ssid == "<None>") continue;
                        // Hide the robot's OWN access points — you put the robot on YOUR network, not its AP.
                        if (ssid.StartsWith("CarthaBot", StringComparison.OrdinalIgnoreCase) ||
                            ssid.StartsWith("ESP32", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!list.Exists(w => w.Ssid == ssid)) list.Add(new WifiNet { Ssid = ssid });
                    }
                }
            }
            catch { }
            return list;
        }

        private async Task JoinWifi()
        {
            if (SelectedNetwork == null) { ProvisionStatus = "Pick a Wi-Fi network from the list first."; return; }
            if (IsScanning) return;
            IsScanning = true;
            ProvisionStatus = $"Telling CarthaBot to join '{SelectedNetwork.Ssid}'…";
            try
            {
                var (ok, ip, mdns) = await new WifiProvisioningService().SetWifiAsync(SelectedNetwork.Ssid, WifiPassword);
                if (ok)
                {
                    WifiEndpoint = (string.IsNullOrWhiteSpace(mdns) ? "carthabot.local" : mdns) + ":3333";
                    WifiState.MarkProvisioned(WifiEndpoint, SelectedNetwork.Ssid);
                    ProvisionStatus = $"✅ Done! CarthaBot joined '{SelectedNetwork.Ssid}'. Switch this PC back to your normal " +
                                      $"Wi-Fi, then close this and press Connect. Robot address: {WifiEndpoint}" +
                                      (string.IsNullOrEmpty(ip) ? "" : $" (or {ip}:3333)") + ".";
                }
                else
                {
                    ProvisionStatus = $"CarthaBot couldn't join '{SelectedNetwork.Ssid}'. Check the password and try again.";
                }
            }
            catch
            {
                ProvisionStatus = "Setup failed — make sure this PC is still on 'CarthaBot-WiFi', then Scan and try again.";
            }
            finally { IsScanning = false; }
        }

        // Send the typed Wi-Fi credentials to the robot over the USB cable (no CarthaBot-WiFi).
        // The robot relays a framed line to the ESP32-C3 (v4 firmware), which saves it + joins.
        private async Task SaveWifiOverUsb()
        {
            if (string.IsNullOrWhiteSpace(WifiSsidInput)) { ProvisionStatus = "Type your Wi-Fi name first."; return; }
            if (IsScanning) return;
            IsScanning = true;
            ProvisionStatus = "Looking for the robot on USB…";
            try
            {
                var sp = await Task.Run(() => OpenRobotPort());
                if (sp == null)
                {
                    ProvisionStatus = "Couldn't find the robot on USB. Plug it in with the cable, turn it on, then try again.";
                    return;
                }
                try
                {
                    ProvisionStatus = $"Sending '{WifiSsidInput}' to the robot over USB…";
                    string ssidHex = ToHex(WifiSsidInput.Trim());
                    string passHex = ToHex(WifiPassword ?? "");
                    string code = "import machine,ubinascii\r\n" +
                                  "_u=machine.UART(0,115200,tx=machine.Pin(0),rx=machine.Pin(1))\r\n" +
                                  $"_u.write(b'\\x10CBCFG '+ubinascii.unhexlify('{ssidHex}')+b'\\t'+ubinascii.unhexlify('{passHex}')+b'\\n')\r\n";

                    sp.Write("\x03"); await Task.Delay(120);   // Ctrl-C
                    sp.Write("\x05"); await Task.Delay(120);   // Ctrl-E (paste mode)
                    sp.Write(code);
                    sp.Write("\x04"); await Task.Delay(700);   // Ctrl-D (run)

                    // Honest feedback: if the robot printed a Python error, don't claim success.
                    string echo = "";
                    try { echo = sp.ReadExisting(); } catch { }
                    if (echo.Contains("Traceback") || echo.Contains("Error"))
                    {
                        ProvisionStatus = "The robot reported an error while saving the Wi-Fi. Open a coding activity over USB once (so MicroPython is set up), then try again.";
                        return;
                    }

                }
                finally { try { sp.Close(); } catch { } }

                // Wait for the ESP to attempt the join, then scan EVERY local subnet for the robot
                // (retried — it can take a few seconds after joining before its TCP server answers).
                ProvisionStatus = $"Saved! Waiting for CarthaBot to join '{WifiSsidInput}'…";
                await Task.Delay(9000);
                string ip = null;
                try { ip = await WifiTransport.FindBridgeOnLanAsync(3333, 3); } catch { }

                // The credentials were saved either way, so remember it and move on. If we found the
                // exact IP, use it; otherwise fall back to carthabot.local and let Connect re-find it
                // (Connect now scans all subnets + tries mDNS), instead of wrongly claiming failure.
                WifiEndpoint = (ip != null ? ip : "carthabot.local") + ":3333";
                WifiState.MarkProvisioned(WifiEndpoint, WifiSsidInput?.Trim());
                ProvisionStatus = ip != null
                    ? $"✅ CarthaBot joined your Wi-Fi! It's at {ip}."
                    : $"Saved! If CarthaBot is joining '{WifiSsidInput}', press Connect — I'll find it on your network.";
                if (_wifiFromChooser)
                {
                    ShowWifiSetup = false;
                    OpenModuleWireless(ConnectionMode.Wifi);
                }
                else if (ip != null)
                {
                    ProvisionStatus += " Unplug USB, then press Connect to start coding.";
                }
            }
            catch (Exception ex)
            {
                ProvisionStatus = "Couldn't send over USB: " + ex.Message;
            }
            finally { IsScanning = false; }
        }

        // Find the robot's USB COM port by probing each one for a live MicroPython REPL.
        // Tries higher COM numbers first (the robot is usually a recently-added higher port),
        // which skips Bluetooth COM ports — the old "first COM port" pick grabbed those by mistake.
        private static System.IO.Ports.SerialPort OpenRobotPort()
        {
            var names = System.IO.Ports.SerialPort.GetPortNames()
                .OrderByDescending(n => { int.TryParse(new string(n.Where(char.IsDigit).ToArray()), out var num); return num; })
                .ToList();
            foreach (var name in names)
            {
                System.IO.Ports.SerialPort sp = null;
                try
                {
                    sp = new System.IO.Ports.SerialPort(name, 115200)
                    { ReadTimeout = 400, WriteTimeout = 600, DtrEnable = true, RtsEnable = true };
                    var openTask = System.Threading.Tasks.Task.Run(() => sp.Open());
                    if (!openTask.Wait(900) || openTask.IsFaulted) continue;   // hung/failed open (Bluetooth etc.) -> skip
                    sp.Write("\x03");
                    System.Threading.Thread.Sleep(150);
                    try { sp.DiscardInBuffer(); } catch { }
                    sp.Write("print('CBPROBE',6*7)\r\n");
                    System.Threading.Thread.Sleep(450);
                    string resp = "";
                    try { resp = sp.ReadExisting(); } catch { }
                    if (resp.Contains("CBPROBE 42")) return sp;               // the robot's REPL answered
                    try { sp.Close(); } catch { }
                }
                catch { try { sp?.Close(); } catch { } }
            }
            return null;
        }

        private static string ToHex(string s)
        {
            var b = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            var sb = new System.Text.StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        // Open the requested module directly (no flashing) and let it connect over WiFi.
        private async void OpenModuleWireless(ConnectionMode mode)
        {
            string param = WifiEndpoint;
            var oldComs = SerialPort.GetPortNames().ToList();

            ShowPlugInAnimation = false;
            IsViewVisiblity = Visibility.Visible;
            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(true);
            await Task.Delay(300);

            switch (SelectedModule)
            {
                case Module.VplJunior:
                    View = new CarthaBotVPL.Views.VplView(_eventAggregator, oldComs, mode, param);
                    break;
                case Module.Draw:
                    View = new CompanionApp.Draw.Views.DrawView(_eventAggregator, oldComs, mode, param);
                    break;
                case Module.KidsCoding:
                    View = new KidsCodingView(_eventAggregator, oldComs);
                    break;
                case Module.Python:
                    OpenAdvancedWireless(mode, oldComs, param);
                    break;
                case Module.Explore:
                    View = new MazeMainView(_eventAggregator, oldComs);
                    break;
                case Module.Behaviour:
                    View = new BehaviorMainView(_eventAggregator, Settings.Default.Language);
                    break;
                case Module.Learn:
                    View = new LearningMainView(_eventAggregator);
                    break;
            }
        }

        /// <summary>
        /// Open the Advanced (Python) editor wirelessly: skip the USB flash wizard and let the
        /// editor connect to the ESP32-C3 bridge over WiFi via the new 4-arg view ctor.
        /// <paramref name="param"/> is the WiFi endpoint ("host:port").
        /// </summary>
        private void OpenAdvancedWireless(ConnectionMode mode, System.Collections.Generic.List<string> oldComs, string param)
        {
            View = new AdvancedProgrammingView(_eventAggregator, oldComs, mode, param);
        }

        private void CloseViewMethod()
        {
            View = null;
            IsViewVisiblity = Visibility.Collapsed;
            ShowConnectionChooser = false;
            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Publish(false);

        }


    }
}
