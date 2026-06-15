using AdvancedProgramming.Events;
using AdvancedProgramming.Views;
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

        // Serial Port
        private SerialPort _serialPort;

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
            set { SetProperty(ref _isConnected, value); IsEnabled = !IsConnected; }
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
        /// Connect to MicroPython device
        /// </summary>
        /// 
        private void Disconnect()
        {
            if (_serialPort == null) return;

            try
            {
                // Detach event first so no background reads fire
                _serialPort.DataReceived -= _serialPort_DataReceived;

                // Try to stop running script (but no Ctrl+D!)
                try { _serialPort.Write("\x03"); } catch { }

                // ⚠️ Do NOT call Close/Dispose synchronously – this is what crashes with Pico
                // Instead just drop the reference
            }
            catch (Exception ex)
            {
                AppendCliOutput("Disconnect warning: " + ex.Message);
            }
            finally
            {
                _serialPort = null;
                IsConnected = false;
            }
        }



        public void ConnectMethod()
        {
            try
            {
               COM = (new List<string>(SerialPort.GetPortNames())).Except(OldCom).First();

                if (IsConnected)
                {
                    //Disconnect();   // safe disconnect (no crash)
                }
                else
                {
                    if (SelectedPort == -1) return;

                    // Always recreate the SerialPort object fresh
                    _serialPort = new SerialPort
                    {
                        PortName = COM,
                        BaudRate = 115200,
                        Encoding = Encoding.ASCII,
                        NewLine = "\r\n"
                    };

                    _serialPort.DataReceived += _serialPort_DataReceived;
                    _serialPort.Open();
                    IsConnected = _serialPort.IsOpen;

                    // Wake up REPL
                    _serialPort.WriteLine("");
                }

            }
            catch (Exception ex)
            {
                IsConnected = false;
                AppendCliOutput("Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles serial port data received
        /// Only appends output from MicroPython, not sent script
        /// </summary>
        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null) return;
                while (_serialPort.BytesToRead > 0)
                {
                    string line = _serialPort.ReadLine();
                    // Only append actual output, ignore REPL prompts or sent code
                    if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(">>>") && !line.StartsWith("..."))
                    {
                        AppendCliOutput(line);
                    }

                    // REPL prompt indicates script finished
                    if (line.Trim().EndsWith(">>>"))
                    {
                        //IsScriptRunning = false;
                        AppendCliOutput(line);

                    }
                }
            }
            catch (Exception ex)
            {
                AppendCliOutput("Read error: " + ex.Message);
            }
        }

        /// <summary>
        /// Append text into CLI Output (thread-safe)
        /// </summary>
        private void AppendCliOutput(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CliOutput += text + Environment.NewLine;
            });
        }

        /// <summary>
        /// Send single-line CLI command
        /// </summary>
        private void SendMethod()
        {
            if (string.IsNullOrWhiteSpace(CommandLine))
                return;

            try
            {
                _serialPort?.WriteLine(CommandLine);
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
        private bool CanSend()
        {
            CarRun = !string.IsNullOrWhiteSpace(CommandLine) && _serialPort?.IsOpen == true;
            return !string.IsNullOrWhiteSpace(CommandLine) && _serialPort?.IsOpen == true;
        }

        private bool CanRunScript => !IsScriptRunning && !string.IsNullOrWhiteSpace(PythonScript) && _serialPort?.IsOpen == true;
        private bool CanStopScript => IsScriptRunning && _serialPort?.IsOpen == true;

        /// <summary>
        /// Run script using raw REPL, CLI shows only MicroPython output
        /// </summary>
        private async void RunScriptViaRawREPL()
        {
            if (_serialPort == null || !_serialPort.IsOpen || string.IsNullOrWhiteSpace(PythonScript))
                return;

            try
            {
                IsScriptRunning = true;

                // Enter paste mode
                _serialPort.Write(new byte[] { 0x05 }, 0, 1); // Ctrl+E
                await Task.Delay(100);

                // Send the script itself (no Ctrl+E inside!)
                _serialPort.Write(Encoding.ASCII.GetBytes(PythonScript.Replace("\r\n", "\n") + "\n"), 0,
                                  Encoding.ASCII.GetByteCount(PythonScript.Replace("\r\n", "\n") + "\n"));

                // End paste mode and run
                _serialPort.Write(new byte[] { 0x04 }, 0, 1); // Ctrl+D

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
        private void StopScript()
        {
            try
            {
                _serialPort?.Write("\x03"); // Ctrl+C
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
