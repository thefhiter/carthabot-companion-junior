using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// USB-serial transport for the embedded VPL. The Companion host already flashed
    /// MicroPython before opening this screen, so we just open the fresh COM port
    /// (the one that appeared after flashing = current ports minus the ports seen before).
    /// </summary>
    public class SerialTransport : ITransport
    {
        private readonly List<string> _oldComs;
        private SerialPort _port;

        public SerialTransport(IEnumerable<string> oldComs)
        {
            _oldComs = (oldComs ?? Enumerable.Empty<string>()).ToList();
        }

        public ConnectionMode Mode => ConnectionMode.Usb;
        public bool IsConnected => _port != null && _port.IsOpen;

        public event Action<string> Status;
        private void Report(string s) => Status?.Invoke(s);

        public async Task<bool> ConnectAsync()
        {
            try
            {
                var current = SerialPort.GetPortNames().ToList();
                var candidate = current.Except(_oldComs).FirstOrDefault() ?? current.LastOrDefault();
                if (candidate == null)
                {
                    Report("Plug in CarthaBot and turn it on");
                    return false;
                }

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
                _port.Write("\x03"); // Ctrl-C
                _port.WriteLine("");

                await EnsureWirelessBootAsync();

                Report("CarthaBot is ready (USB)");
                return _port.IsOpen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USB connect: " + ex.Message);
                _port = null;
                Report("Plug in CarthaBot and turn it on");
                return false;
            }
        }

        /// <summary>
        /// Make sure the robot has a boot.py that puts the MicroPython REPL on UART0 (GP0/GP1) —
        /// the pins the ESP32-C3 bridge uses — so the SAME robot can be programmed wirelessly
        /// (WiFi) with no extra steps. Idempotent; runs over the existing USB REPL. A .uf2
        /// reflash wipes the filesystem, so re-installing on every USB connect keeps wireless alive.
        /// Best-effort: never blocks normal USB use if it fails.
        /// </summary>
        private async Task EnsureWirelessBootAsync()
        {
            try
            {
                _port.Write("\x03"); await Task.Delay(150);
                try { _port.ReadExisting(); } catch { }
                _port.WriteLine("f=open('boot.py','w')"); await Task.Delay(70);
                _port.WriteLine("f.write('import os, machine\\n')"); await Task.Delay(70);
                _port.WriteLine("f.write('os.dupterm(machine.UART(0, 115200, tx=machine.Pin(0), rx=machine.Pin(1)))\\n')"); await Task.Delay(70);
                _port.WriteLine("f.close()"); await Task.Delay(100);
                try { _port.ReadExisting(); } catch { }
            }
            catch { /* best-effort */ }
        }

        public Task WriteAsync(byte[] data)
        {
            if (_port != null && _port.IsOpen) _port.Write(data, 0, data.Length);
            return Task.CompletedTask;
        }

        public string ReadExisting()
        {
            try { return (_port != null && _port.IsOpen) ? _port.ReadExisting() : ""; }
            catch { return ""; }
        }

        public void Close()
        {
            try { _port?.Write("\x03"); } catch { }
            try { if (_port != null && _port.IsOpen) _port.Close(); } catch { }
            _port = null;
        }

        public void Dispose() => Close();
    }
}
