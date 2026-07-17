using AdvancedProgramming.Events;
using AdvancedProgramming.Views;
using CarthaBotVPL.Services;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using ScintillaNet.Abstractions.Enumerations;
using Syncfusion.Windows.Edit;
using Syncfusion.Windows.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static System.Windows.Forms.DataFormats;

namespace AdvancedProgramming.ViewModels
{
    public class AdvancedProgrammingViewModel : BindableBase
    {
        public IEventAggregator eventAggregator { get; set; }
        public List<string> OldCom { get; set; }
        // Commands
        public Prism.Commands.DelegateCommand ConnectCommand { get; }
        public Prism.Commands.DelegateCommand SendCommand { get; }
        public Prism.Commands.DelegateCommand RunScriptCommand { get; }
        public Prism.Commands.DelegateCommand StopScriptCommand { get; }
        public Prism.Commands.DelegateCommand ClearCommand { get; }
        public Prism.Commands.DelegateCommand ComboDropDownOpenedCommand { get; }
        public Prism.Commands.DelegateCommand CloseViewCommand { get; }



        
        public ICommand editLoadedCommand { get; }

        // NEW: Load/Save
        public Prism.Commands.DelegateCommand LoadFileScriptCommand { get; }
        public Prism.Commands.DelegateCommand SaveScriptCommand { get; }

        // CLI Output (readonly terminal text)
        private string _cliOutput;
        public string CliOutput
        {
            get => _cliOutput;
            set => SetProperty(ref _cliOutput, value);
        }

        // CLI Command input (single line)
        private string _commandLine;
        public string CommandLine
        {
            get => _commandLine;
            set => SetProperty(ref _commandLine, value);
        }

        // Python script editor text
        private string _pythonScript;
        public string PythonScript
        {
            get => _pythonScript;
            set => SetProperty(ref _pythonScript, value);
        }


        // Track if a script is running
        private bool _isScriptRunning;
        public bool IsScriptRunning
        {
            get => _isScriptRunning;
            set
            {
                SetProperty(ref _isScriptRunning, value);
                RaisePropertyChanged(nameof(CanRunScript));
                RaisePropertyChanged(nameof(CanStopScript));
            }
        }

        // Transport (USB serial / WiFi TCP) — the MicroPython paste-mode REPL
        // protocol is identical on every carrier; only the pipe changes.
        private ITransport _transport;

        // Connection selection. Defaults to USB so the existing behaviour is byte-for-byte
        // unchanged; the host chooser calls Configure(...) to switch to WiFi.
        private ConnectionMode _mode = ConnectionMode.Usb;
        private string _wifiEndpoint = "carthabot.local:3333";

        // Background polling reader loop (replaces SerialPort.DataReceived).
        private Thread _reader;
        private volatile bool _reading;

        private ObservableCollection<string> _comPorts;
        public ObservableCollection<string> ComPorts
        {
            get { return _comPorts; }
            set { SetProperty(ref _comPorts, value); }
        }
        private int _selectedPort;
        public int SelectedPort
        {
            get { return _selectedPort; }
            set { SetProperty(ref _selectedPort, value); }
        }
        private bool _isConnected;
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                SetProperty(ref _isConnected, value);
                IsEnabled = !IsConnected;
                RaisePropertyChanged(nameof(CanRunScript));
                RaisePropertyChanged(nameof(CanStopScript));
                // Re-evaluate the toolbar commands now that the link state changed.
                SendCommand?.RaiseCanExecuteChanged();
                RunScriptCommand?.RaiseCanExecuteChanged();
                StopScriptCommand?.RaiseCanExecuteChanged();
            }
        }
        private bool _isEnabled;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetProperty(ref _isEnabled, value); }
        }
        private string _com;
        public string COM
        {
            get { return _com; }
            set { SetProperty(ref _com, value); }
        }

        private Languages _language;
        public Languages Language
        {
            get { return _language; }
            set { SetProperty(ref _language, value); }
        }

        private string _documentSource;
        public string DocumentSource
        {
            get { return _documentSource; }
            set { SetProperty(ref _documentSource, value); }
        }
        public AdvancedProgrammingViewModel()
        {
            DocumentSource = @"";
            Language = Languages.Custom;
            ConnectCommand = new Prism.Commands.DelegateCommand(ConnectMethod);
            SendCommand = new Prism.Commands.DelegateCommand(SendMethod, CanSend)
                          .ObservesProperty(() => CommandLine);
            RunScriptCommand = new Prism.Commands.DelegateCommand(RunScriptViaRawREPL, () => CanRunScript).ObservesProperty(() => IsScriptRunning).ObservesProperty(()=> PythonScript);
            StopScriptCommand = new Prism.Commands.DelegateCommand(StopScript);
            ClearCommand = new Prism.Commands.DelegateCommand(ClearMethod);

            // NEW
            LoadFileScriptCommand = new Prism.Commands.DelegateCommand(LoadFileScript);
            SaveScriptCommand = new Prism.Commands.DelegateCommand(SaveScript);

            ComboDropDownOpenedCommand = new Prism.Commands.DelegateCommand(() => ComPorts = new ObservableCollection<string>(SerialPort.GetPortNames()));

            editLoadedCommand = new Syncfusion.Windows.Shared.DelegateCommand<object>(ExecuteEditLoaded);

            CloseViewCommand = new Prism.Commands.DelegateCommand(()=> eventAggregator.GetEvent<AdvancedProgrammingCloseEvent>().Publish());
        }
        public void ExecuteEditLoaded(object obj)
        {
            var editControl = obj as EditControl;

            AdvancedProgrammingView custom = new AdvancedProgrammingView(eventAggregator,OldCom);
            PythonLanguage customLanguage = new PythonLanguage(obj as EditControl);
            customLanguage.Lexem = custom.Resources["pythonLanguageLexems"] as LexemCollection;
            customLanguage.Formats = custom.Resources["pythonLanguageFormats"] as FormatsCollection;
            (obj as EditControl).CustomLanguage = customLanguage;
        }



        private void ClearMethod()
        {
            CliOutput = "";
        }

        public void Subscribe(IEventAggregator _eventAggregator)
        {
            this.eventAggregator = _eventAggregator;
            eventAggregator.GetEvent<ScriptChangedEvent>().Subscribe((obj) =>
            {
                PythonScript = obj.Text;
                DocumentSource = obj.DocumentSource;
            }
            );
        }

        /// <summary>
        /// Pick the connection carrier before ConnectMethod() runs. The view's USB ctor never
        /// calls this (USB stays the default), while the WiFi ctor calls it with the chosen
        /// mode and the endpoint ("host:port").
        /// </summary>
        public void Configure(ConnectionMode mode, string param)
        {
            _mode = mode;
            if (mode == ConnectionMode.Wifi && !string.IsNullOrWhiteSpace(param)) _wifiEndpoint = param;
        }

        /// <summary>Build the transport for the selected Mode (mirrors VplViewModel / KidsCodingViewModel).</summary>
        private ITransport BuildTransport()
        {
            switch (_mode)
            {
                case ConnectionMode.Wifi:
                    string host = "carthabot.local";
                    int port = 3333;
                    if (!string.IsNullOrWhiteSpace(_wifiEndpoint))
                    {
                        var parts = _wifiEndpoint.Split(':');
                        host = parts[0].Trim();
                        if (parts.Length > 1) int.TryParse(parts[1].Trim(), out port);
                    }
                    return new WifiTransport(host, port);
                default:
                    // USB: SerialTransport picks the freshly-flashed COM (current ports minus OldCom).
                    return new SerialTransport(OldCom ?? new List<string>());
            }
        }

        /// <summary>
        /// Connect to MicroPython device
        /// </summary>
        /// 
        private void Disconnect()
        {
            StopReader();

            if (_transport == null) { IsConnected = false; return; }

            try
            {
                // Try to stop a running script (Ctrl+C) then drop the link.
                try { _ = _transport.WriteAsync(new byte[] { 0x03 }); } catch { }
                try { _transport.Close(); } catch { }
            }
            catch (Exception ex)
            {
                AppendCliOutput("Disconnect warning: " + ex.Message);
            }
            finally
            {
                _transport = null;
                IsConnected = false;
            }
        }

        /// <summary>
        /// Open the robot link over the selected carrier (USB / WiFi / BLE). The USB path keeps
        /// the exact same effective behaviour: SerialTransport opens the freshly-flashed COM
        /// (current ports minus OldCom) and wakes the REPL with Ctrl+C.
        /// </summary>
        public async void ConnectMethod()
        {
            if (IsConnected) return;

            try
            {
                // Drop any previous link first.
                StopReader();
                try { _transport?.Close(); } catch { }
                _transport = null;
                IsConnected = false;

                var t = BuildTransport();
                t.Status += s => AppendCliOutput(s);

                bool ok = await t.ConnectAsync();
                _transport = ok ? t : null;
                IsConnected = ok;

                if (ok)
                {
                    if (_transport.Mode == ConnectionMode.Usb) COM = "USB";
                    StartReader();
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                AppendCliOutput("Error: " + ex.Message);
            }
        }

        #region Reader loop (polling, replaces SerialPort.DataReceived)

        private void StartReader()
        {
            StopReader();
            _reading = true;
            _reader = new Thread(ReaderLoop) { IsBackground = true, Name = "CarthaBot REPL reader" };
            _reader.Start();
        }

        private void StopReader()
        {
            _reading = false;
            _reader = null;
        }

        /// <summary>
        /// Poll the transport for incoming bytes, split into lines, and stream MicroPython
        /// output to the CLI — keeping the original filtering (drop pure ">>>" / "..." prompts).
        /// Works identically over USB, WiFi and BLE because it only uses ITransport.ReadExisting().
        /// </summary>
        private void ReaderLoop()
        {
            var buffer = new StringBuilder();
            while (_reading && _transport != null && _transport.IsConnected)
            {
                string chunk = null;
                try { chunk = _transport.ReadExisting(); }
                catch { break; }

                if (string.IsNullOrEmpty(chunk)) { Thread.Sleep(15); continue; }
                buffer.Append(chunk);

                int nl;
                while ((nl = IndexOf(buffer, '\n')) >= 0)
                {
                    string line = buffer.ToString(0, nl);
                    buffer.Remove(0, nl + 1);
                    line = line.TrimEnd('\r');

                    string trimmed = line.Trim();
                    // Skip lines that are just bare REPL prompts.
                    if (trimmed == ">>>" || trimmed == "...") continue;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith(">>>") || line.StartsWith("...")) continue;

                    AppendCliOutput(line);
                }

                if (buffer.Length > 8192) buffer.Clear(); // guard against runaway noise
            }
        }

        private static int IndexOf(StringBuilder sb, char c)
        {
            for (int i = 0; i < sb.Length; i++) if (sb[i] == c) return i;
            return -1;
        }

        #endregion

        /// <summary>
        /// Append text into CLI Output (thread-safe)
        /// </summary>
        private void AppendCliOutput(string text)
        {
            var app = Application.Current;
            if (app == null) return; // app shutting down — don't crash the reader thread
            try { app.Dispatcher.Invoke(() => { CliOutput += text + Environment.NewLine; }); }
            catch { /* dispatcher gone / detached — ignore */ }
        }

        /// <summary>
        /// Send single-line CLI command
        /// </summary>
        private async void SendMethod()
        {
            if (string.IsNullOrWhiteSpace(CommandLine))
                return;

            try
            {
                if (_transport != null && _transport.IsConnected)
                    await _transport.WriteAsync(Encoding.ASCII.GetBytes(CommandLine + "\r\n"));
                // Do not show sent command in CLI
                CommandLine = string.Empty;
            }
            catch (Exception ex)
            {
                AppendCliOutput("Send error: " + ex.Message);
            }
        }
        private bool _carRun;
        public bool CarRun
        {
            get { return _carRun; }
            set { SetProperty(ref _carRun, value); }
        }

        private bool IsOpen => _transport != null && _transport.IsConnected;

        private bool CanSend()
        {
            CarRun = !string.IsNullOrWhiteSpace(CommandLine) && IsOpen;
            return !string.IsNullOrWhiteSpace(CommandLine) && IsOpen;
        }

        private bool CanRunScript => !IsScriptRunning && !string.IsNullOrWhiteSpace(PythonScript) && IsOpen;
        private bool CanStopScript => IsScriptRunning && IsOpen;

        /// <summary>
        /// Run script using raw REPL, CLI shows only MicroPython output
        /// </summary>
        private async void RunScriptViaRawREPL()
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(PythonScript))
                return;

            try
            {
                IsScriptRunning = true;

                // Enter paste mode
                await _transport.WriteAsync(new byte[] { 0x05 }); // Ctrl+E
                await Task.Delay(100);

                // Send the script itself (no Ctrl+E inside!)
                await _transport.WriteAsync(Encoding.ASCII.GetBytes(PythonScript.Replace("\r\n", "\n") + "\n"));

                // End paste mode and run
                await _transport.WriteAsync(new byte[] { 0x04 }); // Ctrl+D

            }
            catch (Exception ex)
            {
                AppendCliOutput("Script error: " + ex.Message);
                IsScriptRunning = false;
            }
        }

        /// <summary>
        /// Stop the running script (Ctrl+C)
        /// </summary>
        private async void StopScript()
        {
            try
            {
                if (_transport != null && _transport.IsConnected)
                    await _transport.WriteAsync(new byte[] { 0x03 }); // Ctrl+C
            }
            catch (Exception ex)
            {
                AppendCliOutput("Stop error: " + ex.Message);
            }
            finally
            {
                IsScriptRunning = false;
            }
        }
        // ================================
        // NEW: Load/Save Implementation
        // ================================

        private void LoadFileScript()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Python Files (*.py)|*.py|Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
                };

                if (dlg.ShowDialog() == true)
                {
                    DocumentSource = dlg.FileName;
                    //PythonScript = File.ReadAllText(dlg.FileName);
                    //eventAggregator.GetEvent<ScriptLoadedEvent>().Publish(PythonScript);
                    //AppendCliOutput("Loaded script: " + dlg.FileName);
                }
            }
            catch (Exception ex)
            {
                AppendCliOutput("Load error: " + ex.Message);
            }
        }

        private void SaveScript()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Python Files (*.py)|*.py|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = "script.py"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, PythonScript ?? "");
                    //AppendCliOutput("Saved script: " + dlg.FileName);
                }
            }
            catch (Exception ex)
            {
                AppendCliOutput("Save error: " + ex.Message);
            }
        }
    }
}
