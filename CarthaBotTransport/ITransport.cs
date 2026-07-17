using System;
using System.Threading.Tasks;

namespace CarthaBotVPL.Services
{
    /// <summary>How the Companion reaches the CarthaBot from the VPL screen.</summary>
    public enum ConnectionMode
    {
        /// <summary>Direct USB-serial REPL (the host already flashed MicroPython).</summary>
        Usb,
        /// <summary>TCP socket to the ESP32-C3 bridge on the CN1 header (relays to the RP2040 UART).</summary>
        Wifi
    }

    /// <summary>
    /// A raw byte pipe to the CarthaBot. The MicroPython paste-mode protocol
    /// (Ctrl-C / Ctrl-E / code / Ctrl-D) is identical on every transport; only the
    /// carrier changes. Reading is supported too so the live 3D twin keeps receiving
    /// the robot's telemetry over WiFi just like it does over USB.
    /// </summary>
    public interface ITransport : IDisposable
    {
        ConnectionMode Mode { get; }
        bool IsConnected { get; }

        event Action<string> Status;

        Task<bool> ConnectAsync();
        Task WriteAsync(byte[] data);

        /// <summary>Drain any bytes received so far as text ("" if none). Non-blocking.</summary>
        string ReadExisting();

        void Close();
    }
}
