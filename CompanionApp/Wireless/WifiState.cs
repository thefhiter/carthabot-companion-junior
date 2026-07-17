using System;
using System.IO;
using System.Text.Json;

namespace CompanionApp.Wireless
{
    /// <summary>
    /// Remembers whether the CarthaBot has ever been put on Wi-Fi from this PC, plus the address it
    /// ended up at. Used to tell a FIRST-time Wi-Fi connection (→ scan + enter name/password + provision)
    /// from a returning one (→ connect straight to the saved address). Stored next to the user profile
    /// so it survives between sessions, alongside the Draw calibration.
    /// </summary>
    public static class WifiState
    {
        private static string Path =>
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                   "CarthaBot", "wifi.json");

        private class Dto
        {
            public bool Provisioned { get; set; }
            public string Endpoint { get; set; }
            public string Ssid { get; set; }
        }

        private static Dto _cache;
        private static Dto Current => _cache ??= Load();

        private static Dto Load()
        {
            try
            {
                if (File.Exists(Path))
                    return JsonSerializer.Deserialize<Dto>(File.ReadAllText(Path)) ?? new Dto();
            }
            catch { /* fall back to a clean state */ }
            return new Dto();
        }

        /// <summary>True once the robot has successfully been put on / reached over Wi-Fi from this PC.</summary>
        public static bool IsProvisioned => Current.Provisioned;

        /// <summary>The saved "host:port" to connect straight to (empty if never set).</summary>
        public static string Endpoint => Current.Endpoint ?? "";

        /// <summary>The Wi-Fi name the robot was put on (for friendly messages).</summary>
        public static string Ssid => Current.Ssid ?? "";

        /// <summary>Record a successful Wi-Fi setup / connection so next time connects directly.</summary>
        public static void MarkProvisioned(string endpoint, string ssid = null)
        {
            var d = Current;
            d.Provisioned = true;
            if (!string.IsNullOrWhiteSpace(endpoint)) d.Endpoint = endpoint.Trim();
            if (!string.IsNullOrWhiteSpace(ssid)) d.Ssid = ssid.Trim();
            Save(d);
        }

        /// <summary>Forget the saved Wi-Fi setup (so the next connection is treated as first-time again).</summary>
        public static void Reset()
        {
            Save(new Dto());
        }

        private static void Save(Dto d)
        {
            _cache = d;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path, JsonSerializer.Serialize(d));
            }
            catch { /* best-effort */ }
        }
    }
}
