using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CarthaBotVPL.Services;          // ITransport / SerialTransport / WifiTransport (shared transport)
using CompanionApp.Draw.Services;
using CompanionApp.Events;
using CompanionApp.Wireless;          // WifiState / WifiProvisioner / WifiNet (shared first-time Wi-Fi setup)
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace CompanionApp.Draw.ViewModels
{
    /// <summary>
    /// View-model for the "Draw with a pen" studio. The child draws on the canvas (freehand or by
    /// stamping a shape); pressing ▶ compiles the strokes into a turtle-plotter MicroPython program
    /// and streams it to the CarthaBot over the SAME paste-mode pipe the VPL studio uses (USB or WiFi).
    /// The robot's pen is fixed down, so everything is drawn as one continuous line.
    /// </summary>
    public class DrawViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<string> _oldComs;
        private ITransport _transport;

        public DrawViewModel(IEventAggregator eventAggregator, List<string> oldComs)
            : this(eventAggregator, oldComs, ConnectionMode.Usb, null) { }

        public DrawViewModel(IEventAggregator eventAggregator, List<string> oldComs, ConnectionMode mode, string param)
        {
            _eventAggregator = eventAggregator;
            _oldComs = oldComs ?? new List<string>();
            _mode = mode;
            if (mode == ConnectionMode.Wifi && !string.IsNullOrWhiteSpace(param)) _wifiEndpoint = param;
            // returning Wi-Fi user → reuse the address the robot was last reached at
            else if (WifiState.IsProvisioned && !string.IsNullOrWhiteSpace(WifiState.Endpoint)) _wifiEndpoint = WifiState.Endpoint;

            LoadCalibration();   // restore the user's tuned mm/s, °/s, size, powers from last time
            _status = L("drawStatusStart", _status);

            ConnectCommand = new DelegateCommand(async () => await Connect());
            StopCommand = new DelegateCommand(async () => await StopRobot());
            CloseCommand = new DelegateCommand(Close);
            HelpCommand = new DelegateCommand(() => ShowHelp = !ShowHelp);
            TestLineCommand = new DelegateCommand(async () => await SendTestLineAsync());
            TestTurnCommand = new DelegateCommand(async () => await SendTestTurnAsync());
            // one-tap self-calibration: after a test, the grown-up just taps what they saw and the
            // app nudges the rate 8% in the right direction — a few repeats converge on spot-on.
            NudgeCommand = new DelegateCommand<string>(k =>
            {
                switch (k)
                {
                    case "lineLong": MmPerSec = Math.Min(250, MmPerSec * 1.08); break;   // rolled farther than assumed
                    case "lineShort": MmPerSec = Math.Max(20, MmPerSec / 1.08); break;
                    // turned too far ⇒ pivots faster than modeled ⇒ less scrub friction than assumed
                    case "turnFar": PivotScrub /= 1.08; break;
                    case "turnShort": PivotScrub *= 1.08; break;
                }
                SaveCalibration();
                Status = L("drawCalSaved", "Saved! Run the same test again — repeat until it's spot on.");
            });
            SetFacingCommand = new DelegateCommand<string>(dir =>
            {
                StartHeading = dir switch { "right" => 0, "down" => -90, "left" => 180, _ => 90 };
            });
            SetSizeCommand = new DelegateCommand<string>(s =>
            {
                SizeMm = s switch { "small" => 100, "big" => 300, _ => 200 };
            });
            SetModeCommand = new DelegateCommand<string>(m =>
            {
                if (Enum.TryParse(m, true, out ConnectionMode cm)) Mode = cm;
            });
            ScanWifiCommand = new DelegateCommand(async () => await ScanWifi());
            SaveWifiCommand = new DelegateCommand(async () => await SaveWifiAndConnect());
            SkipWifiCommand = new DelegateCommand(async () => await SkipWifiSetup());
            CloseWifiSetupCommand = new DelegateCommand(() => ShowWifiSetup = false);
            WifiSetupCommand = new DelegateCommand(OpenWifiSetup);   // manual re-setup (changed network etc.)
        }

        // ------------------------------------------------------------------ first-time Wi-Fi setup

        private bool _showWifiSetup;
        public bool ShowWifiSetup { get => _showWifiSetup; set => SetProperty(ref _showWifiSetup, value); }

        public ObservableCollection<WifiNet> ScannedNetworks { get; } = new ObservableCollection<WifiNet>();

        private WifiNet _selectedNetwork;
        public WifiNet SelectedNetwork
        {
            get => _selectedNetwork;
            set { if (SetProperty(ref _selectedNetwork, value) && value != null) WifiSsidInput = value.Ssid; }
        }

        private string _wifiSsidInput = "";
        public string WifiSsidInput { get => _wifiSsidInput; set => SetProperty(ref _wifiSsidInput, value); }

        private string _wifiPassword = "";
        public string WifiPassword { get => _wifiPassword; set => SetProperty(ref _wifiPassword, value); }

        private string _provisionStatus = "";
        public string ProvisionStatus { get => _provisionStatus; set => SetProperty(ref _provisionStatus, value); }

        private bool _isProvisioning;
        public bool IsProvisioning
        {
            get => _isProvisioning;
            set { if (SetProperty(ref _isProvisioning, value)) RaisePropertyChanged(nameof(CanProvision)); }
        }
        public bool CanProvision => !IsProvisioning;

        public DelegateCommand ScanWifiCommand { get; }
        public DelegateCommand SaveWifiCommand { get; }
        public DelegateCommand SkipWifiCommand { get; }
        public DelegateCommand CloseWifiSetupCommand { get; }
        public DelegateCommand WifiSetupCommand { get; }

        private void OpenWifiSetup()
        {
            ShowWifiSetup = true;
            ProvisionStatus = "Pick your Wi-Fi, type the password, plug the robot in with the USB cable, then Save.";
            _ = ScanWifi();
            _ = AutoFillPcWifi();
        }

        private async Task ScanWifi()
        {
            try
            {
                var nets = await Task.Run(() => WifiProvisioner.ScanNetworks());
                ScannedNetworks.Clear();
                foreach (var n in nets) ScannedNetworks.Add(n);
            }
            catch { /* the typed SSID box is always available */ }
        }

        private async Task AutoFillPcWifi()
        {
            try
            {
                var (ssid, pass) = await Task.Run(() => WifiProvisioner.GetCurrentPcWifi());
                if (!string.IsNullOrWhiteSpace(ssid)) WifiSsidInput = ssid;
                if (!string.IsNullOrEmpty(pass)) WifiPassword = pass;
            }
            catch { }
        }

        private async Task SaveWifiAndConnect()
        {
            if (IsProvisioning) return;
            if (string.IsNullOrWhiteSpace(WifiSsidInput)) { ProvisionStatus = "Type your Wi-Fi name first."; return; }
            IsProvisioning = true;
            try
            {
                var r = await WifiProvisioner.SaveOverUsbAsync(WifiSsidInput, WifiPassword, s => RunOnUi(() => ProvisionStatus = s));
                ProvisionStatus = r.Message;
                if (r.Ok)
                {
                    WifiEndpoint = r.Endpoint;
                    WifiState.MarkProvisioned(r.Endpoint, WifiSsidInput?.Trim());
                    Mode = ConnectionMode.Wifi;
                    ShowWifiSetup = false;
                    if (r.Found)
                        await Connect();                 // located on the LAN → connect right away
                    else
                        Status = r.Message;              // still joining → let the user press Connect (it re-scans)
                }
            }
            finally { IsProvisioning = false; }
        }

        private async Task SkipWifiSetup()
        {
            WifiState.MarkProvisioned(WifiEndpoint, WifiSsidInput?.Trim());
            ShowWifiSetup = false;
            Mode = ConnectionMode.Wifi;
            await Connect();
        }

        // ------------------------------------------------------------------ bindable state

        private string _status = "Draw something, or pick a shape — then press ▶ to send it to CarthaBot.";
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { if (SetProperty(ref _isConnected, value)) RaisePropertyChanged(nameof(ConnectionText)); }
        }
        public string ConnectionText => IsConnected
            ? $"{L("drawConnYes", "CarthaBot connected")} ({Mode})"
            : L("drawConnNot", "CarthaBot not connected");

        /// <summary>Localized string lookup with an English fallback (resources may be FR or EN).</summary>
        private static string L(string key, string fallback) =>
            Application.Current?.TryFindResource(key) as string ?? fallback;

        private bool _busy;
        public bool Busy { get => _busy; set => SetProperty(ref _busy, value); }

        // True while a program is being streamed — the Send button binds IsEnabled to CanSend.
        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set { if (SetProperty(ref _isSending, value)) RaisePropertyChanged(nameof(CanSend)); }
        }
        public bool CanSend => !IsSending;

        // ---- connection method ----
        private ConnectionMode _mode = ConnectionMode.Usb;
        public ConnectionMode Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                {
                    RaisePropertyChanged(nameof(IsUsb));
                    RaisePropertyChanged(nameof(IsWifi));
                    RaisePropertyChanged(nameof(ConnectionText));
                }
            }
        }
        public bool IsUsb => Mode == ConnectionMode.Usb;
        public bool IsWifi => Mode == ConnectionMode.Wifi;

        private string _wifiEndpoint = "carthabot.local:3333";
        public string WifiEndpoint { get => _wifiEndpoint; set => SetProperty(ref _wifiEndpoint, value); }

        // ---- size + fixed drive settings ----
        // The studio is for young children, so there are no power/smoothing/passes knobs any more:
        // the compiler always uses the motor powers proven on this robot, a single pass, and an
        // automatic smoothing tolerance scaled to the paper size (faithful line, no jitter).
        private const int FixedDrawSpeed = 150;
        private const int FixedTurnSpeed = 150;   // same turn power the VPL studio uses on this robot

        private double _sizeMm = 200;
        public double SizeMm
        {
            get => _sizeMm;
            set
            {
                if (SetProperty(ref _sizeMm, Math.Round(value)))
                {
                    RaisePropertyChanged(nameof(IsSizeSmall));
                    RaisePropertyChanged(nameof(IsSizeMedium));
                    RaisePropertyChanged(nameof(IsSizeBig));
                }
            }
        }
        public bool IsSizeSmall => Math.Abs(_sizeMm - 100) < 1;
        public bool IsSizeMedium => Math.Abs(_sizeMm - 200) < 1;
        public bool IsSizeBig => Math.Abs(_sizeMm - 300) < 1;

        /// <summary>Smoothing picked automatically: ~1.5% of the paper size, clamped to 1.5–5 mm.</summary>
        private double AutoSmoothMm => Math.Max(1.5, Math.Min(5.0, _sizeMm * 0.015));

        private double _mmPerSec = 90;
        public double MmPerSec { get => _mmPerSec; set => SetProperty(ref _mmPerSec, Math.Round(value)); }

        // PHYSICAL distance between the two wheels (mm) — a grown-up can measure it with a ruler.
        // Turns are DERIVED from MmPerSec + this geometry (see DrawSettings.DegPerSec), so
        // calibrating the line automatically calibrates the turns.
        private double _trackMm = 95;
        public double TrackMm { get => _trackMm; set => SetProperty(ref _trackMm, Math.Round(value)); }

        // Pivot scrub-friction factor (spinning in place is slower than pure geometry predicts).
        // Not shown in the UI — the ↻ "turned too far / not enough" taps tune it.
        private double _pivotScrub = 1.35;
        public double PivotScrub
        {
            get => _pivotScrub;
            set => SetProperty(ref _pivotScrub, Math.Max(0.7, Math.Min(3.0, value)));
        }

        // Direction the robot is placed facing (math heading: 90=up, 0=right, 180=left, -90=down).
        private double _startHeading = 90;
        public double StartHeading
        {
            get => _startHeading;
            set { if (SetProperty(ref _startHeading, value)) RaisePropertyChanged(nameof(FacingArrow)); }
        }
        public string FacingArrow =>
            Math.Abs(_startHeading - 0) < 1 ? "→" :
            Math.Abs(_startHeading - 180) < 1 ? "←" :
            Math.Abs(_startHeading + 90) < 1 ? "↓" : "↑";

        private string _generatedCode = "";
        public string GeneratedCode { get => _generatedCode; set => SetProperty(ref _generatedCode, value); }

        private bool _showCode;
        public bool ShowCode { get => _showCode; set => SetProperty(ref _showCode, value); }

        private bool _showHelp;
        public bool ShowHelp { get => _showHelp; set => SetProperty(ref _showHelp, value); }

        /// <summary>Live one-line summary of what will be drawn (moves · line length · time).</summary>
        private string _plan = "";
        public string Plan { get => _plan; set => SetProperty(ref _plan, value); }

        public DrawSettings BuildSettings() => new DrawSettings
        {
            SizeMm = SizeMm,
            DrawSpeed = FixedDrawSpeed,
            TurnSpeed = FixedTurnSpeed,
            MmPerSec = MmPerSec,
            TrackMm = TrackMm,
            PivotScrub = PivotScrub,
            SimplifyMm = AutoSmoothMm,
            Passes = 1,
            StartHeadingDeg = StartHeading
        };

        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand CloseCommand { get; }
        public DelegateCommand HelpCommand { get; }
        public DelegateCommand TestLineCommand { get; }
        public DelegateCommand TestTurnCommand { get; }
        public DelegateCommand<string> SetModeCommand { get; }
        public DelegateCommand<string> SetFacingCommand { get; }
        public DelegateCommand<string> SetSizeCommand { get; }
        public DelegateCommand<string> NudgeCommand { get; }

        // ------------------------------------------------------------------ live plan / code preview

        /// <summary>Recompute the "N moves · X cm · ~Y s" summary for the current drawing.</summary>
        public void UpdatePlan(IReadOnlyList<IReadOnlyList<Point>> strokes)
        {
            if (strokes == null || strokes.Count == 0 || strokes.All(s => s == null || s.Count < 2))
            {
                Plan = "";
                if (ShowCode) GeneratedCode = "";
                return;
            }
            var prog = DrawCompiler.Generate(strokes, BuildSettings());
            Plan = prog.MoveCount == 0
                ? "Too small to trace"
                : $"✏️ {prog.MoveCount} moves · {Math.Round(prog.TotalDrawMm / 10.0)} cm · ~{Math.Round(prog.EstimatedSeconds)} s";
            if (ShowCode) GeneratedCode = prog.Code;
        }

        /// <summary>Show/hide the generated MicroPython (compiled from the current strokes).</summary>
        public void ToggleCode(IReadOnlyList<IReadOnlyList<Point>> strokes)
        {
            ShowCode = !ShowCode;
            if (ShowCode)
                GeneratedCode = (strokes != null && strokes.Any(s => s != null && s.Count >= 2))
                    ? DrawCompiler.Generate(strokes, BuildSettings()).Code
                    : "# draw something first";
        }

        // ------------------------------------------------------------------ send to robot

        /// <summary>Compile freehand ink strokes (canvas pixels, Y down) and stream them to the robot.</summary>
        public Task SendInkAsync(IReadOnlyList<IReadOnlyList<Point>> strokes)
        {
            if (strokes == null || strokes.Count == 0 || strokes.All(s => s == null || s.Count < 2))
            {
                Status = "Nothing to draw yet — sketch something or stamp a shape first.";
                return Task.CompletedTask;
            }
            var prog = DrawCompiler.Generate(strokes, BuildSettings());
            return SendProgramAsync(prog);
        }

        /// <summary>Compile a ready-made paper path (mm, Y up) and stream it to the robot.</summary>
        public Task SendPaperPathAsync(IReadOnlyList<Point> paperPath)
        {
            var prog = DrawCompiler.Generate(paperPath, BuildSettings());
            return SendProgramAsync(prog);
        }

        /// <summary>Send a single straight 100 mm roll so the user can measure it and tune mm/s.</summary>
        public Task SendTestLineAsync()
        {
            var prog = DrawCompiler.GenerateMoves(new[] { new DrawMove(0, 100) }, BuildSettings());
            return SendProgramAsync(prog, "📏 Drew a 100 mm test line. Measure it — if it isn't 10 cm, tap 'Line too long' or 'Line too short'.");
        }

        /// <summary>Send a single 90° spin so the user can check it and tune the wheel distance.</summary>
        public Task SendTestTurnAsync()
        {
            var prog = DrawCompiler.GenerateMoves(new[] { new DrawMove(90, 0) }, BuildSettings());
            return SendProgramAsync(prog, "↻ Turned 90° (left). Not a perfect quarter? Tap '↻ Turned too far' or '↻ Not far enough'.");
        }

        private bool _sending;

        private async Task SendProgramAsync(DrawProgram prog, string successMessage = null)
        {
            GeneratedCode = prog.Code;
            if (prog.MoveCount == 0)
            {
                Status = L("drawStTooSmall", "That drawing is too small to trace — make it bigger.");
                return;
            }
            // first time on Wi-Fi → set up the network before trying to send
            if (Mode == ConnectionMode.Wifi && !IsConnected && !WifiState.IsProvisioned)
            {
                OpenWifiSetup();
                Status = "Let's put CarthaBot on your Wi-Fi first.";
                return;
            }
            if (_sending) { Status = L("drawStBusy", "Hang on — still sending the last one…"); return; }
            _sending = true;
            IsSending = true;

            try
            {
            if (!await EnsureConnectedAsync())
            {
                Status = L("drawStPlug", "Plug in CarthaBot (or join its WiFi) and press Connect.");
                return;
            }

            SaveCalibration();   // remember the powers / mm/s / °/s that produced this run

            try
            {
                byte[] payload = Encoding.ASCII.GetBytes(prog.Code.Replace("\r\n", "\n") + "\n");
                // Recover the link the same way the VPL studio does: a back-pressured bridge only
                // unblocks once we READ, so drain + Ctrl-C a few times before pasting.
                for (int k = 0; k < 3; k++)
                {
                    try { _transport.ReadExisting(); } catch { }
                    await _transport.WriteAsync(new byte[] { 0x03 }); // Ctrl-C
                    await Task.Delay(60);
                }
                try { _transport.ReadExisting(); } catch { }
                await _transport.WriteAsync(new byte[] { 0x05 }); // Ctrl-E -> paste mode
                await Task.Delay(80);
                await _transport.WriteAsync(payload);
                await _transport.WriteAsync(new byte[] { 0x04 }); // Ctrl-D -> run

                Status = successMessage ?? string.Format(
                         L("drawStDrawing", "✏️ CarthaBot is drawing! ({0} moves, ~{1} cm of line, about {2} s)"),
                         prog.MoveCount, Math.Round(prog.TotalDrawMm / 10.0), Math.Round(prog.EstimatedSeconds));
            }
            catch (Exception ex)
            {
                Status = "Oops, couldn't send — try again.";
                System.Diagnostics.Debug.WriteLine("Draw send: " + ex.Message);
            }
            }
            finally { _sending = false; IsSending = false; }
        }

        private async Task StopRobot()
        {
            try { if (_transport != null) await _transport.WriteAsync(new byte[] { 0x03 }); } catch { }
            Status = L("drawStStopped", "Stopped. The pen stays where it is.");
        }

        // ------------------------------------------------------------------ connection (USB / WiFi)

        private async Task Connect()
        {
            // first time on Wi-Fi from this PC → put the robot on the network first (scan + name + password)
            if (Mode == ConnectionMode.Wifi && !WifiState.IsProvisioned) { OpenWifiSetup(); return; }
            bool ok = await EnsureConnectedAsync();
            Status = ok ? L("drawStReady", "CarthaBot connected. Ready to draw!")
                        : L("drawStConnFail", "Couldn't connect — check the cable / WiFi and try again.");
        }

        private Task<bool> EnsureConnectedAsync()
        {
            if (IsConnected) return Task.FromResult(true);
            return ConnectAsync();
        }

        private async Task<bool> ConnectAsync()
        {
            if (Busy) return IsConnected;
            Busy = true;
            try
            {
                try { _transport?.Close(); } catch { }
                _transport = null;
                IsConnected = false;

                ITransport t = Mode == ConnectionMode.Wifi ? MakeWifi(WifiEndpoint) : new SerialTransport(_oldComs);
                t.Status += s => RunOnUi(() => Status = s);

                bool ok = await t.ConnectAsync();
                _transport = ok ? t : null;
                IsConnected = ok;
                return ok;
            }
            catch (Exception ex)
            {
                _transport = null;
                IsConnected = false;
                RunOnUi(() => Status = "Couldn't connect to CarthaBot — " + ex.Message);
                return false;
            }
            finally { Busy = false; }
        }

        private static ITransport MakeWifi(string endpoint)
        {
            string host = "carthabot.local";
            int port = 3333;
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                var parts = endpoint.Split(':');
                host = parts[0].Trim();
                if (parts.Length > 1) int.TryParse(parts[1].Trim(), out port);
            }
            return new WifiTransport(host, port);
        }

        private void CloseLink()
        {
            try { _transport?.Close(); } catch { }
            _transport = null;
            IsConnected = false;
        }

        private static void RunOnUi(Action a)
        {
            var app = Application.Current;
            if (app != null) app.Dispatcher.Invoke(a); else a();
        }

        private void Close()
        {
            SaveCalibration();
            CloseLink();
            _eventAggregator.GetEvent<DrawCloseEvent>().Publish();
        }

        // ------------------------------------------------------------------ calibration persistence

        // A tiny JSON next to the user profile so the tuned values survive between sessions.
        private static string CalibrationPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "CarthaBot", "draw_calibration.json");

        private class CalibrationDto
        {
            public double SizeMm { get; set; }
            public double MmPerSec { get; set; }
            public double TrackMm { get; set; }
            public double PivotScrub { get; set; }   // 0 in pre-v2.6 files
            public double StartHeading { get; set; } = 90;
        }

        private void LoadCalibration()
        {
            try
            {
                if (!File.Exists(CalibrationPath)) return;
                var d = JsonSerializer.Deserialize<CalibrationDto>(File.ReadAllText(CalibrationPath));
                if (d == null) return;
                // snap old free-slider sizes to the nearest of the three kid choices
                if (d.SizeMm >= 20) _sizeMm = d.SizeMm <= 150 ? 100 : d.SizeMm >= 250 ? 300 : 200;
                if (d.MmPerSec > 0) _mmPerSec = d.MmPerSec;
                if (d.PivotScrub > 0)
                {
                    // v2.6+ file: physical track + scrub stored separately
                    if (d.TrackMm >= 40) _trackMm = d.TrackMm;
                    _pivotScrub = Math.Max(0.7, Math.Min(3.0, d.PivotScrub));
                }
                else if (d.TrackMm >= 40)
                {
                    // v2.5 file stored one EFFECTIVE track (physical × scrub): split it so the
                    // already-tuned pivot rate is preserved exactly.
                    _pivotScrub = Math.Max(0.7, Math.Min(3.0, d.TrackMm / _trackMm));
                }
                _startHeading = d.StartHeading;
            }
            catch { /* fall back to defaults — calibration is a convenience, never fatal */ }
        }

        private void SaveCalibration()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath));
                var dto = new CalibrationDto
                {
                    SizeMm = SizeMm, MmPerSec = MmPerSec, TrackMm = TrackMm, PivotScrub = PivotScrub,
                    StartHeading = StartHeading
                };
                File.WriteAllText(CalibrationPath, JsonSerializer.Serialize(dto));
            }
            catch { /* best-effort */ }
        }
    }
}
