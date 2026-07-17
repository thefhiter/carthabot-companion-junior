using CompanionApp.Events;
using CompanionApp.Models.Classes;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CompanionApp.ViewModels
{
    /// <summary>
    /// View-model for the "Kids Coding" method – a tangible, icon-only block programming
    /// experience designed for children under 7 (inspired by Thymio VPL / ScratchJr / Bee-Bot).
    ///
    /// The child builds a sequence of big colourful blocks (forward, back, left, right, lights, beep).
    /// Pressing PLAY turns that sequence into MicroPython and streams it to the CarthaBot over the
    /// USB serial REPL (same paste-mode mechanism the Advanced Programming module uses), so the
    /// real robot performs the program immediately – no typing and no reading required.
    /// </summary>
    public class KidsCodingViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<string> _oldComs;
        private SerialPort _serialPort;

        // Movement duration (ms) for one block – kept short so the robot stays on a desk/mat.
        private const int StepMs = 800;

        public ObservableCollection<KidsBlock> Palette { get; } = new ObservableCollection<KidsBlock>();
        public ObservableCollection<KidsBlock> Program { get; } = new ObservableCollection<KidsBlock>();

        public DelegateCommand<KidsBlock> AddBlockCommand { get; }
        public DelegateCommand<KidsBlock> RemoveBlockCommand { get; }
        public DelegateCommand ClearCommand { get; }
        public DelegateCommand PlayCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand CloseCommand { get; }
        public DelegateCommand RepeatPlusCommand { get; }
        public DelegateCommand RepeatMinusCommand { get; }

        private int _repeat = 1;
        public int Repeat
        {
            get => _repeat;
            set => SetProperty(ref _repeat, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { SetProperty(ref _isConnected, value); RaisePropertyChanged(nameof(StatusColor)); }
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        private string _status = "";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string StatusColor => IsConnected ? "#37D67A" : "#F2994A";

        /// <summary>True while the program strip has at least one block (drives the empty-hint).</summary>
        public bool HasBlocks => Program.Count > 0;

        public KidsCodingViewModel(IEventAggregator eventAggregator, List<string> oldComs)
        {
            _eventAggregator = eventAggregator;
            _oldComs = oldComs ?? new List<string>();

            BuildPalette();

            AddBlockCommand = new DelegateCommand<KidsBlock>(AddBlock);
            RemoveBlockCommand = new DelegateCommand<KidsBlock>(RemoveBlock);
            ClearCommand = new DelegateCommand(ClearProgram);
            PlayCommand = new DelegateCommand(Play);
            StopCommand = new DelegateCommand(Stop);
            CloseCommand = new DelegateCommand(Close);
            RepeatPlusCommand = new DelegateCommand(() => { if (Repeat < 5) Repeat++; });
            RepeatMinusCommand = new DelegateCommand(() => { if (Repeat > 1) Repeat--; });

            Program.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(HasBlocks));

            // Try to grab the freshly-flashed MicroPython COM port right away.
            Connect();
        }

        private void BuildPalette()
        {
            // Glyphs are plain unicode so they render with the standard UI font (no extra assets).
            Palette.Add(new KidsBlock(KidsAction.Forward, "⬆", "Go", "#37D67A"));   // ⬆
            Palette.Add(new KidsBlock(KidsAction.Backward, "⬇", "Back", "#F2994A")); // ⬇
            Palette.Add(new KidsBlock(KidsAction.Left, "⬅", "Left", "#2D9CDB"));     // ⬅
            Palette.Add(new KidsBlock(KidsAction.Right, "➡", "Right", "#9B51E0"));   // ➡
            Palette.Add(new KidsBlock(KidsAction.Lights, "☀", "Lights", "#EB5757")); // ☀
            Palette.Add(new KidsBlock(KidsAction.Beep, "♪", "Beep", "#F2C94C"));     // ♪
        }

        private void AddBlock(KidsBlock source)
        {
            if (source == null || Program.Count >= 16) return;
            Program.Add(source.Clone());
        }

        private void RemoveBlock(KidsBlock block)
        {
            if (block != null) Program.Remove(block);
        }

        private void ClearProgram()
        {
            Program.Clear();
        }

        private void Close()
        {
            try { Stop(); } catch { }
            CloseSerial();
            _eventAggregator.GetEvent<KidsCodingCloseEvent>().Publish();
        }

        #region Serial (MicroPython REPL)

        /// <summary>
        /// Detect and open the CarthaBot serial port (the new port that appeared after the
        /// MicroPython firmware was flashed = current ports minus the ports seen before flashing).
        /// </summary>
        public void Connect()
        {
            try
            {
                var current = SerialPort.GetPortNames().ToList();
                var candidate = current.Except(_oldComs).FirstOrDefault() ?? current.LastOrDefault();

                if (candidate == null)
                {
                    IsConnected = false;
                    Status = "Plug in CarthaBot and turn it on";
                    return;
                }

                _serialPort = new SerialPort
                {
                    PortName = candidate,
                    BaudRate = 115200,
                    Encoding = Encoding.ASCII,
                    NewLine = "\r\n",
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                _serialPort.Open();
                IsConnected = _serialPort.IsOpen;

                // Wake the REPL and make sure nothing is running.
                _serialPort.Write("\x03"); // Ctrl+C
                _serialPort.WriteLine("");

                Status = IsConnected ? "CarthaBot is ready" : "Plug in CarthaBot and turn it on";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Status = "Plug in CarthaBot and turn it on";
                System.Diagnostics.Debug.WriteLine("Kids connect error: " + ex.Message);
            }
        }

        private void CloseSerial()
        {
            if (_serialPort == null) return;
            try { _serialPort.Write("\x03"); } catch { }
            try { if (_serialPort.IsOpen) _serialPort.Close(); } catch { }
            _serialPort = null;
            IsConnected = false;
        }

        #endregion

        #region Play / Stop

        private async void Play()
        {
            if (IsRunning) return;

            if (Program.Count == 0)
            {
                Status = "Add some blocks first 🙂";
                return;
            }

            // Re-detect the port if the robot was plugged in after the view opened.
            if (_serialPort == null || !_serialPort.IsOpen)
                Connect();

            if (_serialPort == null || !_serialPort.IsOpen)
            {
                Status = "Plug in CarthaBot and turn it on";
                return;
            }

            try
            {
                IsRunning = true;
                Status = "CarthaBot is playing your code ▶";

                string code = GenerateMicroPython();

                // Drain any leftover output, then stop anything running and stream in paste mode.
                try { _serialPort.ReadExisting(); } catch { }
                _serialPort.Write(new byte[] { 0x03 }, 0, 1); // Ctrl+C
                await Task.Delay(80);
                _serialPort.Write(new byte[] { 0x05 }, 0, 1); // Ctrl+E -> paste mode
                await Task.Delay(80);

                byte[] payload = Encoding.ASCII.GetBytes(code.Replace("\r\n", "\n") + "\n");
                _serialPort.Write(payload, 0, payload.Length);

                _serialPort.Write(new byte[] { 0x04 }, 0, 1); // Ctrl+D -> execute

                // Let the program run; keep draining so the USB buffer never back-pressures.
                int total = Math.Min(400 + Repeat * Program.Count * (StepMs + 250), 15000);
                for (int waited = 0; waited < total; waited += 100)
                {
                    await Task.Delay(100);
                    try { _serialPort.ReadExisting(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Status = "Oops! Try again";
                System.Diagnostics.Debug.WriteLine("Kids play error: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
                if (IsConnected) Status = "CarthaBot is ready";
            }
        }

        private void Stop()
        {
            try { _serialPort?.Write("\x03"); } catch { }
            IsRunning = false;
            if (IsConnected) Status = "Stopped";
        }

        #endregion

        /// <summary>
        /// Build a complete MicroPython program from the block sequence. The motor / LED / speaker
        /// pin map is taken straight from the CarthaBot firmware (communs.h / MotorControl.cpp):
        ///   M1 dir = GP23, M1 PWM = GP29 ; M2 dir = GP24, M2 PWM = GP28
        ///   NeoPixel strip = GP21 (11 LEDs) ; Speaker = GP20 ; default speed = 150/255.
        /// </summary>
        public string GenerateMicroPython()
        {
            var sb = new StringBuilder();
            sb.AppendLine("import machine, neopixel, time");
            sb.AppendLine("SPEED = 150");
            sb.AppendLine("# 2-input (L9110-style) H-bridge per wheel: PWM the active coil, hold");
            sb.AppendLine("# the other at 0, so both wheels run at the same speed (M1=left 23/29,");
            sb.AppendLine("# M2=right 24/28; forward = M1 via GP29, M2 via GP24).");
            sb.AppendLine("# forward coils matched to this robot's wiring: left fwd=GP23, right fwd=GP24");
            sb.AppendLine("m1_fwd = machine.PWM(machine.Pin(23)); m1_fwd.freq(1000)");
            sb.AppendLine("m1_rev = machine.PWM(machine.Pin(29)); m1_rev.freq(1000)");
            sb.AppendLine("m2_fwd = machine.PWM(machine.Pin(24)); m2_fwd.freq(1000)");
            sb.AppendLine("m2_rev = machine.PWM(machine.Pin(28)); m2_rev.freq(1000)");
            sb.AppendLine("np = neopixel.NeoPixel(machine.Pin(21), 11)");
            sb.AppendLine("spk = machine.PWM(machine.Pin(20)); spk.duty_u16(0)");
            sb.AppendLine("def _duty(v):");
            sb.AppendLine("    if v < 0: v = 0");
            sb.AppendLine("    if v > 255: v = 255");
            sb.AppendLine("    return int(v * 65535 // 255)");
            sb.AppendLine("def _motors(l, r):");
            sb.AppendLine("    if l >= 0:");
            sb.AppendLine("        m1_fwd.duty_u16(_duty(l)); m1_rev.duty_u16(0)");
            sb.AppendLine("    else:");
            sb.AppendLine("        m1_fwd.duty_u16(0); m1_rev.duty_u16(_duty(-l))");
            sb.AppendLine("    if r >= 0:");
            sb.AppendLine("        m2_fwd.duty_u16(_duty(r)); m2_rev.duty_u16(0)");
            sb.AppendLine("    else:");
            sb.AppendLine("        m2_fwd.duty_u16(0); m2_rev.duty_u16(_duty(-r))");
            sb.AppendLine("def stop(): _motors(0, 0)");
            sb.AppendLine("def _drive(l, r, ms):");
            sb.AppendLine("    _motors(l, r); time.sleep_ms(ms); stop(); time.sleep_ms(200)");
            sb.AppendLine($"def forward(): _drive(SPEED, SPEED, {StepMs})");
            sb.AppendLine($"def backward(): _drive(-SPEED, -SPEED, {StepMs})");
            sb.AppendLine($"def left(): _drive(-SPEED, SPEED, {StepMs})");
            sb.AppendLine($"def right(): _drive(SPEED, -SPEED, {StepMs})");
            sb.AppendLine("BRIGHT = 60  # LED brightness cap (0-255): lower = far less current, prevents brown-out/power-off");
            sb.AppendLine("def lights(r, g, b):");
            sb.AppendLine("    for i in range(11): np[i] = (r*BRIGHT//255, g*BRIGHT//255, b*BRIGHT//255)");
            sb.AppendLine("    np.write()");
            sb.AppendLine("def rainbow():");
            sb.AppendLine("    cols = [(255,0,0),(255,120,0),(255,255,0),(0,200,0),(0,80,255),(150,0,255)]");
            sb.AppendLine("    for c in cols:");
            sb.AppendLine("        lights(*c); time.sleep_ms(120)");
            sb.AppendLine("    lights(0, 0, 0)");
            sb.AppendLine("def beep():");
            sb.AppendLine("    spk.freq(880); spk.duty_u16(30000); time.sleep_ms(200); spk.duty_u16(0); time.sleep_ms(120)");
            sb.AppendLine("def run():");

            var body = new StringBuilder();
            foreach (var block in Program)
            {
                switch (block.Action)
                {
                    case KidsAction.Forward: body.AppendLine("    forward()"); break;
                    case KidsAction.Backward: body.AppendLine("    backward()"); break;
                    case KidsAction.Left: body.AppendLine("    left()"); break;
                    case KidsAction.Right: body.AppendLine("    right()"); break;
                    case KidsAction.Lights: body.AppendLine("    rainbow()"); break;
                    case KidsAction.Beep: body.AppendLine("    beep()"); break;
                }
            }
            if (body.Length == 0) body.AppendLine("    pass");
            sb.Append(body.ToString());

            sb.AppendLine($"for _ in range({Math.Max(1, Repeat)}):");
            sb.AppendLine("    run()");
            sb.AppendLine("stop()");
            sb.AppendLine("lights(0, 0, 0)");
            return sb.ToString();
        }
    }
}
