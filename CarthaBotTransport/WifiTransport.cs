using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// WiFi transport — a TCP socket to the ESP32-C3 bridge, which relays bytes to/from the
    /// RP2040 UART. Default endpoint is the bridge SoftAP (192.168.4.1:3333): join the
    /// "CarthaBot-WiFi" network on the PC first.
    /// </summary>
    public class WifiTransport : ITransport
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;

        public WifiTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public ConnectionMode Mode => ConnectionMode.Wifi;
        public bool IsConnected => _client != null && _client.Connected;

        public event Action<string> Status;
        private void Report(string s) => Status?.Invoke(s);

        public async Task<bool> ConnectAsync()
        {
            // Fast, clear message only when the target is the AP's literal IP and we're not on it.
            if (!OnBridgeSubnet())
            {
                Report("Not on the robot's WiFi yet. Open Windows WiFi settings, join " +
                       "'CarthaBot-WiFi' (password: carthabot), then press Connect.");
                return false;
            }

            // 1) Try the configured address first (an IP, or an mDNS name like carthabot.local).
            if (await TryConnectAsync(_host, _port, announce: true)) return true;

            // 2) Same-network fallback: the robot joined YOUR Wi-Fi but mDNS (carthabot.local) may
            //    not resolve on Windows — scan this PC's local network for the bridge and use it.
            Report("Searching your network for CarthaBot…");
            string found = null;
            try { found = await FindBridgeOnLanAsync(_port); } catch { }
            if (found != null && await TryConnectAsync(found, _port, announce: false))
            {
                Report($"CarthaBot is ready (WiFi {found}:{_port})");
                return true;
            }

            Report("Couldn't find CarthaBot on your network. Make sure the robot is ON and joined " +
                   "your Wi-Fi (set it up once over USB), then press Connect.");
            return false;
        }

        /// <summary>Connect to one host:port, with a few quick retries (the bridge can briefly
        /// refuse a reconnect while freeing a previous client).</summary>
        private async Task<bool> TryConnectAsync(string host, int port, bool announce)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    if (announce) Report(attempt == 1 ? $"Connecting over WiFi to {host}:{port}…" : $"Retrying… ({attempt}/3)");
                    _client = new TcpClient { NoDelay = true };
                    var connect = _client.ConnectAsync(host, port);
                    if (await Task.WhenAny(connect, Task.Delay(6000)) != connect)
                    {
                        _ = connect.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default); // observe the abandoned connect
                        Close();
                        if (attempt == 3) return false;
                        await Task.Delay(500);
                        continue;
                    }
                    await connect;
                    _stream = _client.GetStream();
                    await WriteAsync(new byte[] { 0x03 }); // Ctrl-C
                    if (announce) Report($"CarthaBot is ready (WiFi {host}:{port})");
                    return true;
                }
                catch
                {
                    Close();
                    if (attempt == 3) return false;
                    await Task.Delay(500);
                }
            }
            return false;
        }

        /// <summary>True if this PC has an IPv4 address on the same /24 as the bridge host — i.e.
        /// it has actually joined the CarthaBot-WiFi network. For a hostname or any detection
        /// problem we don't second-guess the user and return true so the real connect still runs.</summary>
        private bool OnBridgeSubnet()
        {
            if (!IPAddress.TryParse(_host, out var hostIp)) return true; // hostname / custom target
            var hb = hostIp.GetAddressBytes();
            if (hb.Length != 4) return true;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var lb = ua.Address.GetAddressBytes();
                        if (lb[0] == hb[0] && lb[1] == hb[1] && lb[2] == hb[2]) return true; // same /24
                    }
                }
            }
            catch { return true; } // never block on a detection error
            return false;
        }

        private string FriendlyError(Exception ex)
        {
            if (!OnBridgeSubnet())
                return "Not on the robot's WiFi yet. Open Windows WiFi settings, join " +
                       "'CarthaBot-WiFi' (password: carthabot), then press Connect.";
            return $"Couldn't reach the bridge at {_host}:{_port}. Make sure the robot is ON, then " +
                   "press Connect. (" + ex.Message + ")";
        }

        /// <summary>Scan this PC's local /24(s) for the CarthaBot bridge (port open + REPL answers).
        /// Returns the robot's IP, or null. Scans EVERY active IPv4 subnet (Wi-Fi first), so a machine
        /// with VirtualBox / VPN / Ethernet adapters doesn't waste the scan on the wrong network.</summary>
        public static Task<string> FindBridgeOnLanAsync(int port) => FindBridgeOnLanAsync(port, 1);

        /// <summary>As above, retrying the whole sweep up to <paramref name="attempts"/> times (the
        /// bridge can take a few seconds after joining before its TCP server answers).</summary>
        public static async Task<string> FindBridgeOnLanAsync(int port, int attempts)
        {
            var prefixes = LocalIPv4Prefixes();
            if (prefixes.Count == 0) return null;

            for (int attempt = 0; attempt < Math.Max(1, attempts); attempt++)
            {
                foreach (var prefix in prefixes)   // Wi-Fi subnet(s) first → usually a fast hit
                {
                    var tasks = new List<Task<string>>();
                    for (int i = 1; i <= 254; i++) tasks.Add(ProbeBridgeAsync(prefix + i, port));
                    var results = await Task.WhenAll(tasks);
                    var hit = results.FirstOrDefault(r => r != null);
                    if (hit != null) return hit;
                }
                if (attempt < attempts - 1) await Task.Delay(2500);
            }
            return null;
        }

        /// <summary>All active IPv4 "/24" prefixes ("a.b.c."), wireless adapters first, virtual ones last.</summary>
        private static List<string> LocalIPv4Prefixes()
        {
            var wifi = new List<string>();
            var wired = new List<string>();
            var virt = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var s = ua.Address.ToString();
                        if (s.StartsWith("127.") || s.StartsWith("169.254")) continue;
                        var p = s.Split('.');
                        if (p.Length != 4) continue;
                        string prefix = p[0] + "." + p[1] + "." + p[2] + ".";

                        string name = (ni.Name + " " + ni.Description).ToLowerInvariant();
                        bool isVirtual = name.Contains("virtualbox") || name.Contains("vmware") ||
                                         name.Contains("hyper-v") || name.Contains("vethernet") ||
                                         name.Contains("vpn") || name.Contains("loopback") ||
                                         name.Contains("tap") || name.Contains("tunnel");
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && !isVirtual)
                            wifi.Add(prefix);
                        else if (isVirtual)
                            virt.Add(prefix);
                        else
                            wired.Add(prefix);
                    }
                }
            }
            catch { }
            return wifi.Concat(wired).Concat(virt)
                       .Distinct()
                       .ToList();
        }

        /// <summary>Probe one IP: if the port is open AND the CarthaBot REPL answers, return the IP.</summary>
        private static async Task<string> ProbeBridgeAsync(string ip, int port)
        {
            try
            {
                using (var c = new TcpClient { NoDelay = true })
                {
                    var connect = c.ConnectAsync(ip, port);
                    if (await Task.WhenAny(connect, Task.Delay(500)) != connect)
                    {
                        _ = connect.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default); // observe so it's not an unobserved task exception
                        return null;
                    }
                    await connect;
                    var s = c.GetStream();
                    await s.WriteAsync(new byte[] { 0x03 }, 0, 1);                 // Ctrl-C
                    await Task.Delay(60);
                    var probe = Encoding.ASCII.GetBytes("print('CBNET',6*7)\r\n");
                    await s.WriteAsync(probe, 0, probe.Length);
                    // Poll for up to ~1.2 s — the reply can arrive in chunks and the bridge adds latency.
                    var sb = new StringBuilder(); var buf = new byte[512];
                    for (int waited = 0; waited < 1200; waited += 120)
                    {
                        while (s.DataAvailable)
                        {
                            int n = await s.ReadAsync(buf, 0, buf.Length);
                            if (n <= 0) break;
                            sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                        }
                        if (sb.ToString().Contains("CBNET 42")) return ip;
                        await Task.Delay(120);
                    }
                    return sb.ToString().Contains("CBNET 42") ? ip : null;
                }
            }
            catch { return null; }
        }

        public async Task WriteAsync(byte[] data)
        {
            if (_stream != null)
            {
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
        }

        public string ReadExisting()
        {
            if (_stream == null) return "";
            try
            {
                if (!_stream.DataAvailable) return "";
                var sb = new StringBuilder();
                var buf = new byte[1024];
                while (_stream.DataAvailable)
                {
                    int n = _stream.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        public void Close()
        {
            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        public void Dispose() => Close();
    }
}
