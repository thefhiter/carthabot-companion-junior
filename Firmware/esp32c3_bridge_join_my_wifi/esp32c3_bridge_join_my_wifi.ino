/*
 * CarthaBot ESP32-C3 Wireless Bridge — Wi-Fi ONLY ("JOIN MY WI-FI", credentials baked in)
 * ----------------------------------------------------------------------------------------
 * Bluetooth/BLE has been REMOVED. The CarthaBot is now programmed over USB (cable, directly to
 * the Pico) or Wi-Fi (this bridge). This firmware joins your home Wi-Fi on boot and relays the
 * PC <-> robot UART so the Companion apps can program the robot wirelessly.
 *
 * >>> IF YOUR WI-FI PASSWORD IS NOT EXACTLY what's in DEFAULT_PASS below, fix that line. <<<
 *
 * After flashing, open Serial Monitor @115200 and press RST (or replug). You should see:
 *     [wifi] AP=192.168.4.1  STA=on 192.168.1.xx     <-- joined! that's the robot's address
 * Then wire the Beetle back into the robot and, in the app, just press Connect (it auto-finds it).
 *
 * The in-app "Save to robot" still works too — saved credentials (NVS) override the baked-in ones.
 *
 * REPEAT-UPLOAD FIX: the robot->PC relay is NON-BLOCKING — it never waits on a slow or closed PC
 * socket. Before, when the PC stopped reading, tcpClient.write() blocked and froze the whole loop,
 * so the PC's Ctrl-C / next program never reached the robot and you had to press the robot's RESET
 * to upload again. Now PC->robot bytes always get through, so re-uploading over Wi-Fi just works.
 *
 * WIRING: Pico GP0->ESP GPIO4 , ESP GPIO5->Pico GP1 (CN1 SCL) , 3V3 & GND shared.
 * Libraries: none beyond the ESP32 Arduino core (WiFi / Preferences / ESPmDNS). No NimBLE needed.
 */
#include <Arduino.h>
#include <WiFi.h>
#include <Preferences.h>
#include <ESPmDNS.h>

// ====== YOUR WI-FI (home network) ======
// Works once the router's 2.4 GHz is set to WPA2-PSK (AES) only (the ESP32-C3 can't do WPA3/GCMP).
#define DEFAULT_SSID   "TT_2560"
#define DEFAULT_PASS   "619FABODF@B125"
// =======================================

#define ENABLE_RESET   1
#define DEBUG          1
#define MDNS_NAME      "carthabot"
#define UART_BAUD      115200
#define UART_RX_PIN    4
#define UART_TX_PIN    5
#define RESET_PIN      6
#define STATUS_LED     10
#define TCP_PORT       3333
#define RESET_TRIGGER  0x00
#define CFG_MAGIC      0x10

const char* AP_SSID = "CarthaBot-WiFi";
const char* AP_PASS = "carthabot";

HardwareSerial RobotSerial(1);
WiFiServer  tcpServer(TCP_PORT);
WiFiClient  tcpClient;
Preferences prefs;

#if DEBUG
  #define DBG(x)   Serial.println(x)
  #define DBGF(...) Serial.printf(__VA_ARGS__)
#else
  #define DBG(x)
  #define DBGF(...)
#endif

void pulseReset() {
#if ENABLE_RESET
  pinMode(RESET_PIN, OUTPUT); digitalWrite(RESET_PIN, LOW); delay(10); pinMode(RESET_PIN, INPUT);
#endif
}
inline void toRobot(uint8_t b){ if (b==RESET_TRIGGER){ pulseReset(); return; } RobotSerial.write(b); }

// Join Wi-Fi: prefer the network SAVED from the app (NVS) so you can CHANGE it anytime from the
// app without re-flashing; fall back to the baked-in DEFAULT_SSID/DEFAULT_PASS only if nothing
// has been saved yet. (The app's "Save to robot" writes the new creds via applyConfigLine below.)
String g_ssid = "";
bool joinWifi() {
  prefs.begin("cbwifi", true);
  String ssid = prefs.getString("ssid", "");
  String pass = prefs.getString("pass", "");
  prefs.end();
  if (ssid.length() == 0) { ssid = DEFAULT_SSID; pass = DEFAULT_PASS; }   // baked-in fallback
  g_ssid = ssid;
  if (ssid.length() == 0) return false;
  DBGF("[wifi] joining '%s' ...\n", ssid.c_str());
  WiFi.begin(ssid.c_str(), pass.c_str());
  for (int i=0;i<60 && WiFi.status()!=WL_CONNECTED;i++) delay(250);       // up to ~15 s
  return WiFi.status()==WL_CONNECTED;
}

void applyConfigLine(const String& line) {
  if (!line.startsWith("CBCFG ")) return;
  String rest = line.substring(6);
  int tab = rest.indexOf('\t');
  if (tab < 0) return;
  String ssid = rest.substring(0, tab);
  String pass = rest.substring(tab + 1);
  if (ssid.length() == 0) return;
  prefs.begin("cbwifi", false); prefs.putString("ssid", ssid); prefs.putString("pass", pass); prefs.end();
  WiFi.disconnect(); WiFi.begin(ssid.c_str(), pass.c_str());
  for (int i=0;i<60 && WiFi.status()!=WL_CONNECTED;i++) delay(250);
  if (WiFi.status()==WL_CONNECTED) { DBGF("[cfg] joined, IP=%s\n", WiFi.localIP().toString().c_str()); MDNS.begin(MDNS_NAME); }
}

void setup(){
#if DEBUG
  Serial.begin(115200);
#endif
  pinMode(STATUS_LED, OUTPUT);
#if ENABLE_RESET
  pinMode(RESET_PIN, INPUT);
#endif
  RobotSerial.begin(UART_BAUD, SERIAL_8N1, UART_RX_PIN, UART_TX_PIN);

  WiFi.mode(WIFI_AP_STA);
  WiFi.softAP(AP_SSID, AP_PASS);
  bool sta = joinWifi();
  if (sta) {
    DBGF("[wifi] AP=192.168.4.1  STA=on  IP=%s  (JOINED '%s')\n", WiFi.localIP().toString().c_str(), g_ssid.c_str());
  } else {
    int st = WiFi.status();   // 1=SSID not found, 4=auth/wrong-password, 6=disconnected
    const char* why = st==1 ? "SSID NOT FOUND (check name / must be 2.4 GHz / in range)"
                    : st==4 ? "WRONG PASSWORD"
                    : "not connected";
    DBGF("[wifi] AP=192.168.4.1  STA=off  status=%d  -> %s  (tried '%s')\n", st, why, g_ssid.c_str());
  }

  tcpServer.begin(); tcpServer.setNoDelay(true);
  if (MDNS.begin(MDNS_NAME)) MDNS.addService("carthabot","tcp",TCP_PORT);
}

void loop(){
  // Accept a new TCP client (a fresh connection replaces the old one so reconnects never hang).
  if (tcpServer.hasClient()){
    WiFiClient inc = tcpServer.available();
    if (tcpClient && tcpClient.connected()) tcpClient.stop();
    tcpClient = inc; tcpClient.setNoDelay(true);
  }

  // PC -> robot: forward every received byte. Command bytes MUST NOT be dropped.
  if (tcpClient && tcpClient.connected())
    while (tcpClient.available()) toRobot((uint8_t)tcpClient.read());

  // robot -> PC: NON-BLOCKING. Forward only while the TCP send buffer has room; if the PC is not
  // reading, DROP the surplus instead of blocking. Blocking here would freeze the whole loop and
  // stall the PC->robot direction above — which is exactly why a second upload used to be ignored
  // until the robot was reset. Robot->PC traffic is only echoes/telemetry, so dropping is safe.
  static bool capturing = false;
  static String cfgLine;
  while (RobotSerial.available()){
    uint8_t b=(uint8_t)RobotSerial.read();
    if (!capturing && b == CFG_MAGIC) { capturing = true; cfgLine = ""; continue; }
    if (capturing) {
      if (b == '\n') { capturing = false; applyConfigLine(cfgLine); }
      else { cfgLine += (char)b; if (cfgLine.length() > 250) capturing = false; }
      continue;
    }
    if (tcpClient && tcpClient.connected() && tcpClient.availableForWrite() > 0)
      tcpClient.write(b);
    // else: PC not draining -> drop this byte rather than stall the bridge.
  }

  bool linked = (tcpClient && tcpClient.connected());
  digitalWrite(STATUS_LED, linked ? HIGH : ((millis()/500)%2));
}
