using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CompanionApp.Wireless
{
    /// <summary>One nearby Wi-Fi network the ESP32-C3 reported from a scan.</summary>
    public class WifiNet
    {
        public string Ssid { get; set; }
        public int Rssi { get; set; }
        public bool Lock { get; set; }
        public string Bars => Rssi >= -55 ? "▮▮▮" : Rssi >= -68 ? "▮▮▯" : "▮▯▯";
        public string LockIcon => Lock ? "🔒" : "";
        public string Display => $"{Bars}  {Ssid}  {LockIcon}";
    }

    /// <summary>
    /// Talks to the ESP32-C3 bridge v3 control API (HTTP, port 80) so the robot can be put on the
    /// user's OWN Wi-Fi from inside the app — scan networks, join one, then code over it
    /// (carthabot.local:3333) with no more switching to the CarthaBot-WiFi access point.
    ///
    /// First-time setup: the PC must be on "CarthaBot-WiFi" so it can reach the ESP at 192.168.4.1.
    /// After the robot joins the home Wi-Fi it remembers it and rejoins on every boot.
    /// </summary>
    public class WifiProvisioningService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public WifiProvisioningService(string host = "192.168.4.1")
        {
            _baseUrl = "http://" + host;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        }

        /// <summary>GET /scan → the Wi-Fi networks the robot can see.</summary>
        public async Task<List<WifiNet>> ScanAsync()
        {
            var list = new List<WifiNet>();
            var json = await _http.GetStringAsync(_baseUrl + "/scan");
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("nets", out var nets))
            {
                foreach (var n in nets.EnumerateArray())
                {
                    var ssid = n.TryGetProperty("ssid", out var s) ? s.GetString() : null;
                    if (string.IsNullOrEmpty(ssid)) continue;
                    list.Add(new WifiNet
                    {
                        Ssid = ssid,
                        Rssi = n.TryGetProperty("rssi", out var r) ? r.GetInt32() : -100,
                        Lock = n.TryGetProperty("lock", out var l) && l.GetBoolean()
                    });
                }
            }
            return list;
        }

        /// <summary>GET /setwifi?ssid=&amp;pass= → robot saves + joins; returns its new address.</summary>
        public async Task<(bool ok, string ip, string mdns)> SetWifiAsync(string ssid, string pass)
        {
            var url = _baseUrl + "/setwifi?ssid=" + Uri.EscapeDataString(ssid) +
                      "&pass=" + Uri.EscapeDataString(pass ?? "");
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            bool ok = doc.RootElement.TryGetProperty("ok", out var o) && o.GetBoolean();
            string ip = doc.RootElement.TryGetProperty("ip", out var i) ? i.GetString() : "";
            string mdns = doc.RootElement.TryGetProperty("mdns", out var m) ? m.GetString() : "carthabot.local";
            return (ok, ip, mdns);
        }
    }
}
