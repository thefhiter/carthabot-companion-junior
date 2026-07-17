using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarthaBotVPL.Services;   // WifiTransport.FindBridgeOnLanAsync

namespace CompanionApp.Wireless
{
    /// <summary>
    /// Shared, UI-free Wi-Fi setup helpers so BOTH the main connection chooser and the Draw studio
    /// can offer the same first-time flow: scan nearby Wi-Fi, then put the robot on it (over the USB
    /// cable) using the network's name + password — after which the app reaches it over Wi-Fi.
    /// </summary>
    public static class WifiProvisioner
    {
        /// <summary>
        /// Networks to offer the user: the ones in range right now (live scan) merged with the ones
        /// this PC already knows (saved profiles), the robot's own access points removed.
        /// </summary>
        public static List<WifiNet> ScanNetworks()
        {
            var byName = new Dictionary<string, WifiNet>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in GetNearbyNetworks().Concat(GetKnownWifiNetworks()))
            {
                if (string.IsNullOrWhiteSpace(n.Ssid)) continue;
                if (n.Ssid.StartsWith("CarthaBot", StringComparison.OrdinalIgnoreCase) ||
                    n.Ssid.StartsWith("ESP32", StringComparison.OrdinalIgnoreCase)) continue;
                if (!byName.ContainsKey(n.Ssid)) byName[n.Ssid] = n;
            }
            return byName.Values.OrderByDescending(w => w.Rssi).ToList();
        }

        /// <summary>Networks in range right now, via "netsh wlan show networks" (best-effort).</summary>
        public static List<WifiNet> GetNearbyNetworks()
        {
            var list = new List<WifiNet>();
            try
            {
                string outp = RunCmd("netsh", "wlan show networks mode=bssid") ?? "";
                WifiNet cur = null;
                foreach (var raw in outp.Split('\n'))
                {
                    var line = raw.Trim();
                    int c = line.IndexOf(':');
                    if (line.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase) && c >= 0)
                    {
                        string ssid = line.Substring(c + 1).Trim();
                        if (ssid.Length == 0) { cur = null; continue; }
                        cur = new WifiNet { Ssid = ssid, Rssi = -70 };
                        list.Add(cur);
                    }
                    else if (cur != null && line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase) && c >= 0)
                    {
                        // "Signal : 84%" → approximate an RSSI for sorting (-100..-30)
                        var pct = new string(line.Substring(c + 1).Where(char.IsDigit).ToArray());
                        if (int.TryParse(pct, out int p)) cur.Rssi = -100 + (p * 70 / 100);
                    }
                    else if (cur != null && line.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase))
                    {
                        cur.Lock = !line.ToLowerInvariant().Contains("open");
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>Wi-Fi networks this PC already has saved profiles for (always available, no admin).</summary>
        public static List<WifiNet> GetKnownWifiNetworks()
        {
            var list = new List<WifiNet>();
            try
            {
                string outp = RunCmd("netsh", "wlan show profiles") ?? "";
                foreach (var raw in outp.Split('\n'))
                {
                    int c = raw.LastIndexOf(':');
                    if (c < 0) continue;
                    string ssid = raw.Substring(c + 1).Trim();
                    if (ssid.Length == 0 || ssid == "<None>") continue;
                    if (!list.Exists(w => string.Equals(w.Ssid, ssid, StringComparison.OrdinalIgnoreCase)))
                        list.Add(new WifiNet { Ssid = ssid, Rssi = -75 });
                }
            }
            catch { }
            return list;
        }

        /// <summary>(ssid, password) of the Wi-Fi this PC is on — to pre-fill the form. Best-effort.</summary>
        public static (string ssid, string pass) GetCurrentPcWifi()
        {
            try
            {
                string connName = RunCmd("powershell",
                    "-NoProfile -Command \"Get-NetConnectionProfile | Where-Object { $_.InterfaceAlias -like '*Wi-Fi*' } | Select-Object -First 1 -ExpandProperty Name\"")?.Trim();
                if (string.IsNullOrWhiteSpace(connName)) return (null, "");
                var saved = GetKnownWifiNetworks().Select(w => w.Ssid).ToList();
                string ssid = saved.Where(p => connName == p || connName.StartsWith(p + " "))
                                   .OrderByDescending(p => p.Length).FirstOrDefault() ?? connName;
                return (ssid, GetSavedPassword(ssid));
            }
            catch { return (null, ""); }
        }

        /// <summary>The saved password for an SSID, from the exported profile XML (locale-independent).</summary>
        public static string GetSavedPassword(string ssid)
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

        public class ProvisionResult
        {
            public bool Ok;           // credentials were handed to the robot without error
            public bool Found;        // the robot was actually located on the LAN afterwards
            public string Endpoint;   // "host:port" to connect to afterwards
            public string Message;    // user-facing status
        }

        /// <summary>
        /// Put the robot on the named Wi-Fi over the USB cable (most reliable, no AP switching):
        /// open its REPL, relay a framed CBCFG line to the ESP32-C3, then wait and find it on the LAN.
        /// </summary>
        public static async Task<ProvisionResult> SaveOverUsbAsync(string ssid, string pass,
            Action<string> progress = null)
        {
            void Say(string s) => progress?.Invoke(s);
            if (string.IsNullOrWhiteSpace(ssid))
                return new ProvisionResult { Ok = false, Message = "Type your Wi-Fi name first." };

            Say("Looking for the robot on USB…");
            var sp = await Task.Run(() => OpenRobotPort());
            if (sp == null)
                return new ProvisionResult { Ok = false, Message = "Couldn't find the robot on USB. Plug it in with the cable, turn it on, then try again." };

            try
            {
                Say($"Sending '{ssid}' to the robot over USB…");
                string ssidHex = ToHex(ssid.Trim());
                string passHex = ToHex(pass ?? "");
                string code = "import machine,ubinascii\r\n" +
                              "_u=machine.UART(0,115200,tx=machine.Pin(0),rx=machine.Pin(1))\r\n" +
                              $"_u.write(b'\\x10CBCFG '+ubinascii.unhexlify('{ssidHex}')+b'\\t'+ubinascii.unhexlify('{passHex}')+b'\\n')\r\n";

                sp.Write("\x03"); await Task.Delay(120);   // Ctrl-C
                sp.Write("\x05"); await Task.Delay(120);   // Ctrl-E (paste mode)
                sp.Write(code);
                sp.Write("\x04"); await Task.Delay(700);   // Ctrl-D (run)

                string echo = "";
                try { echo = sp.ReadExisting(); } catch { }
                if (echo.Contains("Traceback") || echo.Contains("Error"))
                    return new ProvisionResult { Ok = false, Message = "The robot reported an error while saving the Wi-Fi. Open a coding activity over USB once (so MicroPython is set up), then try again." };
            }
            finally { try { sp.Close(); } catch { } }

            Say($"Saved! Waiting for CarthaBot to join '{ssid}'…");
            await Task.Delay(9000);
            string ip = null;
            try { ip = await WifiTransport.FindBridgeOnLanAsync(3333, 3); } catch { }   // retry across all subnets
            if (ip != null)
                return new ProvisionResult { Ok = true, Found = true, Endpoint = ip + ":3333",
                    Message = $"✅ CarthaBot joined '{ssid}'! It's at {ip}. You can unplug the USB cable now." };

            // Credentials were saved fine. We just couldn't spot it on the LAN yet (it may still be
            // joining, or mDNS/scan needs another moment). Don't strand the user — let them Connect,
            // which re-scans every subnet AND tries carthabot.local.
            return new ProvisionResult
            {
                Ok = true,
                Found = false,
                Endpoint = "carthabot.local:3333",
                Message = $"Saved to CarthaBot. It should be joining '{ssid}' — press Connect and I'll find it on " +
                          "your network. (If it never connects, re-check the password and that it's a 2.4 GHz network.)"
            };
        }

        // Find the robot's USB COM port by probing each for a live MicroPython REPL (higher COMs first).
        public static SerialPort OpenRobotPort()
        {
            var names = SerialPort.GetPortNames()
                .OrderByDescending(n => { int.TryParse(new string(n.Where(char.IsDigit).ToArray()), out var num); return num; })
                .ToList();
            foreach (var name in names)
            {
                SerialPort sp = null;
                try
                {
                    sp = new SerialPort(name, 115200) { ReadTimeout = 400, WriteTimeout = 600, DtrEnable = true, RtsEnable = true };
                    var openTask = Task.Run(() => sp.Open());
                    if (!openTask.Wait(900) || openTask.IsFaulted) continue;
                    sp.Write("\x03");
                    System.Threading.Thread.Sleep(150);
                    try { sp.DiscardInBuffer(); } catch { }
                    sp.Write("print('CBPROBE',6*7)\r\n");
                    System.Threading.Thread.Sleep(450);
                    string resp = "";
                    try { resp = sp.ReadExisting(); } catch { }
                    if (resp.Contains("CBPROBE 42")) return sp;
                    try { sp.Close(); } catch { }
                }
                catch { try { sp?.Close(); } catch { } }
            }
            return null;
        }

        private static string ToHex(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
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
    }
}
