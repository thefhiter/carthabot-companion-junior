using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CarthaBotVPL.Models;
using CarthaBotVPL.Services;
using CompanionApp.Events;
using Microsoft.Win32;
using Prism.Events;

namespace CarthaBotVPL.ViewModels
{
    /// <summary>
    /// View-model for the embedded "Coding (under 6)" VPL studio inside the Companion app.
    /// Mirrors the standalone CarthaBot VPL but talks to the robot the same way the other
    /// Companion modules do: the host flashes MicroPython first, then this view connects to the
    /// freshly-created COM port (current ports minus the ports seen before flashing) and streams
    /// the compiled program in paste mode.
    /// </summary>
    public class VplViewModel : ObservableObject
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<string> _oldComs;
        private SerialPort _port;

        // live telemetry reader (digital twin)
        private Thread _reader;
        private volatile bool _reading;
        /// <summary>Raised on a background thread with one parsed telemetry packet
        /// (l, r, front, ground, R, G, B, anim) from the running robot.</summary>
        public event Action<int[]> TelemetryReceived;

        public ObservableCollection<VplRule> Rules { get; } = new ObservableCollection<VplRule>();

        private VplRule _selectedRule;
        public VplRule SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (_selectedRule != null) _selectedRule.IsSelected = false;
                if (Set(ref _selectedRule, value) && _selectedRule != null)
                    _selectedRule.IsSelected = true;
            }
        }

        private string _status = "Click an Event on the left to add your first rule";
        public string Status { get => _status; set => Set(ref _status, value); }

        private bool _compiledOk;
        public bool CompiledOk { get => _compiledOk; set => Set(ref _compiledOk, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set { if (Set(ref _isConnected, value)) Raise(nameof(ConnectionText)); } }
        public string ConnectionText => IsConnected ? "CarthaBot connected" : "CarthaBot not connected";

        private bool _showCode;
        public bool ShowCode { get => _showCode; set => Set(ref _showCode, value); }

        /// <summary>Thymio-style advanced mode: reveals the Timer and State blocks for older kids.</summary>
        private bool _advancedMode;
        public bool AdvancedMode { get => _advancedMode; set => Set(ref _advancedMode, value); }

        private string _generatedCode = "";
        public string GeneratedCode { get => _generatedCode; set => Set(ref _generatedCode, value); }

        public bool HasRules => Rules.Count > 0;

        public ICommand NewCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand PlayLiveCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand InfoCommand { get; }
        public ICommand ToggleCodeCommand { get; }
        public ICommand ToggleAdvancedCommand { get; }
        public ICommand CloseCommand { get; }

        /// <summary>Look up a localized string from the app-level resource dictionaries (falls back to English).</summary>
        private static string L(string key, string fallback) =>
            Application.Current?.TryFindResource(key) as string ?? fallback;

        public ICommand AddEventCommand { get; }
        public ICommand AddActionCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand SelectRuleCommand { get; }
        public ICommand RemoveActionCommand { get; }

        public VplViewModel(IEventAggregator eventAggregator, List<string> oldComs)
        {
            _eventAggregator = eventAggregator;
            _oldComs = oldComs ?? new List<string>();

            Rules.CollectionChanged += (s, e) => { Reindex(); Raise(nameof(HasRules)); };

            NewCommand = new RelayCommand(NewProgram);
            OpenCommand = new RelayCommand(OpenProgram);
            SaveCommand = new RelayCommand(SaveProgram);
            UndoCommand = new RelayCommand(Undo);
            PlayCommand = new RelayCommand(Play);
            PlayLiveCommand = new RelayCommand(PlayLive);
            StopCommand = new RelayCommand(Stop);
            ConnectCommand = new RelayCommand(Connect);
            InfoCommand = new RelayCommand(ShowInfo);
            ToggleCodeCommand = new RelayCommand(() => { Compile(); ShowCode = !ShowCode; });
            ToggleAdvancedCommand = new RelayCommand(() =>
            {
                AdvancedMode = !AdvancedMode;
                Status = AdvancedMode ? L("vplStAdvOn", "Advanced mode — Timer and Memory unlocked ✨")
                                      : L("vplStAdvOff", "Simple mode");
            });
            CloseCommand = new RelayCommand(Close);

            Status = L("vplStEmpty", _status);

            AddEventCommand = new RelayCommand(p => AddEvent((string)p));
            AddActionCommand = new RelayCommand(p => AddAction((string)p));
            RemoveRuleCommand = new RelayCommand(p => { Snapshot(); Rules.Remove((VplRule)p); });
            SelectRuleCommand = new RelayCommand(p => SelectedRule = (VplRule)p);
            RemoveActionCommand = new RelayCommand(RemoveAction);

            Connect();
        }

        private void Reindex() { for (int i = 0; i < Rules.Count; i++) Rules[i].Index = i + 1; }

        // ---- undo: structural snapshots (add / remove / new / open) ----
        private readonly Stack<string> _undoStack = new Stack<string>();

        private void Snapshot() => _undoStack.Push(SerializeRules());

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            RestoreRules(_undoStack.Pop());
            SelectedRule = null;
            CompiledOk = false;
            Status = L("vplStUndone", "Undone ↶");
        }

        private void AddEvent(string kindName)
        {
            Snapshot();
            var kind = (EventKind)Enum.Parse(typeof(EventKind), kindName);
            var rule = new VplRule { Event = new VplEvent { Kind = kind } };
            if (kind == EventKind.Button) rule.Event.Button = ButtonDir.Up;
            Rules.Add(rule);
            SelectedRule = rule;
            Status = L("vplStPickAction", "Now pick an action on the right ➜");
            CompiledOk = false;
            UiSounds.Blip();
        }

        private void AddAction(string kindName)
        {
            var target = SelectedRule ?? Rules.LastOrDefault();
            if (target == null) { Status = L("vplStEventFirst", "Add an event first (left), then an action"); return; }
            Snapshot();
            var kind = (ActionKind)Enum.Parse(typeof(ActionKind), kindName);
            target.Actions.Add(new VplAction { Kind = kind });
            SelectedRule = target;
            CompiledOk = false;
            UiSounds.Blip();
        }

        private void RemoveAction(object p)
        {
            Snapshot();
            foreach (var r in Rules)
                if (r.Actions.Contains((VplAction)p)) { r.Actions.Remove((VplAction)p); break; }
            CompiledOk = false;
        }

        private string Compile()
        {
            GeneratedCode = VplCompiler.Generate(Rules);
            CompiledOk = true;
            Status = L("vplCompiledOk", "Compilation completed successfully");
            return GeneratedCode;
        }

        private async void Play()
        {
            if (Rules.Count == 0) { Status = L("vplStNothing", "Nothing to run — add a rule first"); return; }
            var code = Compile();

            if (_port == null || !_port.IsOpen) Connect();
            if (_port == null || !_port.IsOpen) { Status = L("vplStPlug", "Plug in CarthaBot and turn it on"); return; }

            try
            {
                _port.Write(new byte[] { 0x03 }, 0, 1); // Ctrl-C
                await Task.Delay(80);
                _port.Write(new byte[] { 0x05 }, 0, 1); // Ctrl-E -> paste mode
                await Task.Delay(80);
                byte[] payload = Encoding.ASCII.GetBytes(code.Replace("\r\n", "\n") + "\n");
                _port.Write(payload, 0, payload.Length);
                _port.Write(new byte[] { 0x04 }, 0, 1); // Ctrl-D -> run
                Status = L("vplStRunning", "CarthaBot is running your program ▶");
            }
            catch (Exception ex) { Status = "Oops, try again"; System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        /// <summary>Run on the real robot AND mirror it live in the 3D twin: compile with
        /// telemetry, stream the program, then read the state the robot streams back.</summary>
        private async void PlayLive()
        {
            if (Rules.Count == 0) { Status = L("vplStNothing", "Nothing to run — add a rule first"); return; }
            GeneratedCode = VplCompiler.Generate(Rules, telemetry: true);
            CompiledOk = true;

            if (_port == null || !_port.IsOpen) Connect();
            if (_port == null || !_port.IsOpen) { Status = L("vplStPlug", "Plug in CarthaBot and turn it on"); return; }

            try
            {
                StopReader();
                _port.Write(new byte[] { 0x03 }, 0, 1); // Ctrl-C
                await Task.Delay(80);
                _port.Write(new byte[] { 0x05 }, 0, 1); // Ctrl-E -> paste mode
                await Task.Delay(80);
                byte[] payload = Encoding.ASCII.GetBytes(GeneratedCode.Replace("\r\n", "\n") + "\n");
                _port.Write(payload, 0, payload.Length);
                _port.Write(new byte[] { 0x04 }, 0, 1); // Ctrl-D -> run
                StartReader();
                Status = L("vplStLive", "Live! CarthaBot and its 3D twin are moving together 📡");
            }
            catch (Exception ex) { Status = "Oops, try again"; System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void StartReader()
        {
            StopReader();
            _reading = true;
            _reader = new Thread(ReaderLoop) { IsBackground = true, Name = "CarthaBot telemetry" };
            _reader.Start();
        }

        private void StopReader()
        {
            _reading = false;
            _reader = null;
        }

        // Reads telemetry lines "T,l,r,front,ground,r,g,b,anim" the robot prints ~10×/s.
        private void ReaderLoop()
        {
            var buffer = new StringBuilder();
            while (_reading && _port != null && _port.IsOpen)
            {
                string chunk = null;
                try { chunk = _port.ReadExisting(); }
                catch { break; }

                if (string.IsNullOrEmpty(chunk)) { Thread.Sleep(15); continue; }
                buffer.Append(chunk);

                int nl;
                while ((nl = IndexOfNewline(buffer)) >= 0)
                {
                    string line = buffer.ToString(0, nl).Trim();
                    buffer.Remove(0, nl + 1);
                    ParseTelemetry(line);
                }
                if (buffer.Length > 4096) buffer.Clear();   // guard against runaway noise
            }
        }

        private static int IndexOfNewline(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++) if (sb[i] == '\n') return i;
            return -1;
        }

        private void ParseTelemetry(string line)
        {
            if (string.IsNullOrEmpty(line) || line[0] != 'T') return;
            var parts = line.Split(',');
            if (parts.Length < 9) return;
            var v = new int[8];
            for (int i = 0; i < 8; i++)
                if (!int.TryParse(parts[i + 1], out v[i])) return;
            TelemetryReceived?.Invoke(v);
        }

        private void Stop()
        {
            StopReader();
            try { _port?.Write("\x03"); } catch { }
            Status = L("vplStStopped", "Stopped");
        }

        #region serial

        private void Connect()
        {
            try
            {
                var current = SerialPort.GetPortNames().ToList();
                var candidate = current.Except(_oldComs).FirstOrDefault() ?? current.LastOrDefault();
                if (candidate == null) { IsConnected = false; Status = L("vplStPlug", "Plug in CarthaBot and turn it on"); return; }

                _port = new SerialPort
                {
                    PortName = candidate,
                    BaudRate = 115200,
                    Encoding = Encoding.ASCII,
                    NewLine = "\r\n",
                    ReadTimeout = 600,
                    WriteTimeout = 600
                };
                _port.Open();
                _port.Write("\x03");
                _port.WriteLine("");
                IsConnected = _port.IsOpen;
                if (IsConnected) Status = L("vplStReady", "CarthaBot is ready");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Status = L("vplStPlug", "Plug in CarthaBot and turn it on");
                System.Diagnostics.Debug.WriteLine("VPL connect: " + ex.Message);
            }
        }

        private void CloseSerial()
        {
            StopReader();
            try { _port?.Write("\x03"); } catch { }
            try { if (_port != null && _port.IsOpen) _port.Close(); } catch { }
            _port = null;
            IsConnected = false;
        }

        #endregion

        private void NewProgram()
        {
            if (Rules.Count > 0) Snapshot();
            Rules.Clear(); SelectedRule = null; GeneratedCode = ""; CompiledOk = false;
            Status = L("vplStNew", "New program");
        }

        private void Close()
        {
            try { Stop(); } catch { }
            CloseSerial();
            _eventAggregator.GetEvent<VplCloseEvent>().Publish();
        }

        // ---- Save / Open (.cbvpl JSON) — the same serializer backs the undo stack ----

        private string SerializeRules()
        {
            var dto = Rules.Select(r => new RuleDto
            {
                Kind = r.Event.Kind.ToString(),
                Button = r.Event.Button.ToString(),
                Detected = r.Event.Detected,
                StateFilter = r.Event.StateFilter,
                Actions = r.Actions.Select(a => new ActionDto
                {
                    Kind = a.Kind.ToString(), Move = a.Move.ToString(), Speed = a.Speed,
                    R = a.R, G = a.G, B = a.B, Sound = a.Sound.ToString(),
                    Seconds = a.Seconds, Anim = a.Anim.ToString(), StateValue = a.StateValue,
                    Notes = a.Notes.Select(n => n.Pitch).ToArray()
                }).ToList()
            }).ToList();
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        }

        private void RestoreRules(string json)
        {
            var dto = JsonSerializer.Deserialize<List<RuleDto>>(json);
            Rules.Clear();
            foreach (var rd in dto ?? new List<RuleDto>())
            {
                var rule = new VplRule
                {
                    Event = new VplEvent
                    {
                        Kind = (EventKind)Enum.Parse(typeof(EventKind), rd.Kind),
                        Button = (ButtonDir)Enum.Parse(typeof(ButtonDir), rd.Button ?? "Up"),
                        Detected = rd.Detected,
                        StateFilter = rd.StateFilter ?? -1
                    }
                };
                foreach (var ad in rd.Actions ?? new List<ActionDto>())
                {
                    var act = new VplAction
                    {
                        Kind = (ActionKind)Enum.Parse(typeof(ActionKind), ad.Kind),
                        Move = (MoveDir)Enum.Parse(typeof(MoveDir), ad.Move ?? "Forward"),
                        Speed = ad.Speed, R = ad.R, G = ad.G, B = ad.B,
                        Sound = (SoundKind)Enum.Parse(typeof(SoundKind), ad.Sound ?? "Beep"),
                        Seconds = ad.Seconds ?? 1.0,
                        Anim = (LedAnim)Enum.Parse(typeof(LedAnim), ad.Anim ?? "Rainbow"),
                        StateValue = ad.StateValue ?? 0
                    };
                    if (ad.Notes != null)
                        for (int i = 0; i < ad.Notes.Length && i < act.Notes.Count; i++)
                            act.Notes[i].Pitch = ad.Notes[i];
                    rule.Actions.Add(act);
                }
                Rules.Add(rule);
            }
            // A program that uses Timer / Memory blocks unlocks advanced mode.
            if (Rules.Any(r => r.Event.Kind == EventKind.Timer || r.Event.StateFilter >= 0 ||
                               r.Actions.Any(a => a.Kind == ActionKind.Timer || a.Kind == ActionKind.State)))
                AdvancedMode = true;
        }

        private void SaveProgram()
        {
            var dlg = new SaveFileDialog { Filter = "CarthaBot VPL (*.cbvpl)|*.cbvpl|All files (*.*)|*.*", FileName = "program.cbvpl" };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, SerializeRules());
            Status = "Saved " + Path.GetFileName(dlg.FileName);
        }

        private void OpenProgram()
        {
            var dlg = new OpenFileDialog { Filter = "CarthaBot VPL (*.cbvpl)|*.cbvpl|All files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                string json = File.ReadAllText(dlg.FileName);
                if (Rules.Count > 0) Snapshot();
                RestoreRules(json);
                Status = "Opened " + Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex) { Status = "Could not open file: " + ex.Message; }
        }

        private void ShowInfo()
        {
            MessageBox.Show(
                "CarthaBot VPL — Coding for under 6\n\n" +
                "Pair an EVENT (left) with one or more ACTIONS (right) to make a rule,\n" +
                "then press ▶ to run it on your CarthaBot.\n\n© FAB619",
                "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private class RuleDto
        {
            public string Kind { get; set; }
            public string Button { get; set; }
            public bool Detected { get; set; }
            public int? StateFilter { get; set; }
            public List<ActionDto> Actions { get; set; }
        }
        private class ActionDto
        {
            public string Kind { get; set; }
            public string Move { get; set; }
            public int Speed { get; set; }
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
            public string Sound { get; set; }
            public double? Seconds { get; set; }
            public string Anim { get; set; }
            public int? StateValue { get; set; }
            public int[] Notes { get; set; }
        }
    }
}
