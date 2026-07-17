/*
 * CarthaBot ESP32-C3 Wireless Bridge  v3  — IN-APP Wi-Fi provisioning
 * ------------------------------------------------------------------
 * Flash this ONCE. After that, the CarthaBot Companion app can:
 *   - SCAN nearby Wi-Fi networks   (the ESP detects your Wi-Fi)
 *   - JOIN your Wi-Fi with a password you type in the app   (saved in flash/NVS)
 *   - then you CODE over your own network (carthabot.local:3333) — internet kept,
 *     no more switching to "CarthaBot-WiFi".
 *
 * Two channels:
 *   :3333  TCP  = transparent UART bridge (the REPL link the app already uses)
 *   :80    HTTP = control API for the app's "Connect to my Wi-Fi" phase:
 *       GET  /status                      -> {"ap":..,"sta":bool,"ssid":..,"ip":..}
 *       GET  /scan                        -> {"nets":[{"ssid":..,"rssi":..,"lock":bool}, ...]}
 *       POST /setwifi   ssid=..&pass=..   -> saves + joins, returns {"ok":bool,"ip":..}
 *       GET  /forget                      -> clears saved Wi-Fi (back to AP-only)
 *
 * On boot: always raises AP "CarthaBot-WiFi" (for first-time setup / fallback) AND,
 * if Wi-Fi was saved, joins it (AP+STA). mDNS = carthabot.local.
 *
 * WIRING (unchanged):  Pico GP0->ESP GPIO4 ,  ESP GPIO5->Pico GP1 ,  3V3/GND common.
 * ROBOT: needs boot.py os.dupterm(UART0) — the app installs it automatically over USB.
 *
 * Libraries: NimBLE-Arduino (BLE). WebServer/Preferences/ESPmDNS ship with the ESP32 core.
 */
#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>
#include <Preferences.h>
#include <ESPmDNS.h>

#define ENABLE_BLE     1
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

const char* AP_SSID = "CarthaBot-WiFi";
const char* AP_PASS = "carthabot";

HardwareSerial RobotSerial(1);
WiFiServer  tcpServer(TCP_PORT);
WiFiClient  tcpClient;
WebServer   http(80);
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

// ---------------- BLE ----------------
#if ENABLE_BLE
#include <NimBLEDevice.h>
#define NUS_SERVICE "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_RX_CHAR "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_TX_CHAR "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"
NimBLECharacteristic* txChar=nullptr; volatile bool bleConnected=false; uint8_t bleTxBuf[20]; size_t bleTxLen=0;
void flushBleTx(){ if(bleConnected&&txChar&&bleTxLen){ txChar->setValue(bleTxBuf,bleTxLen); txChar->notify(); } bleTxLen=0; }
class ServerCB: public NimBLEServerCallbacks{ void onConnect(NimBLEServer*){bleConnected=true;} void onDisconnect(NimBLEServer*){bleConnected=false; NimBLEDevice::startAdvertising();} };
class RxCB: public NimBLECharacteristicCallbacks{ void onWrite(NimBLECharacteristic* c){ std::string v=c->getValue(); for(unsigned char ch:v) toRobot((uint8_t)ch); } };
#endif

// ---------------- Wi-Fi join from saved creds ----------------
bool joinSaved() {
  prefs.begin("cbwifi", true);
  String ssid = prefs.getString("ssid", "");
  String pass = prefs.getString("pass", "");
  prefs.end();
  if (ssid.length() == 0) return false;
  DBGF("[wifi] joining saved '%s'\n", ssid.c_str());
  WiFi.begin(ssid.c_str(), pass.c_str());
  for (int i=0;i<40 && WiFi.status()!=WL_CONNECTED;i++) delay(250);
  return WiFi.status()==WL_CONNECTED;
}

String jsonEscape(const String& s){ String o; for (char c: s){ if(c=='"'||c=='\\') o+='\\'; o+=c; } return o; }

void handleStatus(){
  bool sta = WiFi.status()==WL_CONNECTED;
  String ip = sta ? WiFi.localIP().toString() : WiFi.softAPIP().toString();
  String j = "{\"ap\":\"" + String(AP_SSID) + "\",\"sta\":" + (sta?"true":"false") +
             ",\"ssid\":\"" + jsonEscape(WiFi.SSID()) + "\",\"ip\":\"" + ip + "\",\"mdns\":\"" + MDNS_NAME + ".local\"}";
  http.send(200,"application/json",j);
}
void handleScan(){
  int n = WiFi.scanNetworks();
  String j = "{\"nets\":[";
  for (int i=0;i<n;i++){
    if(i) j+=",";
    j += "{\"ssid\":\"" + jsonEscape(WiFi.SSID(i)) + "\",\"rssi\":" + String(WiFi.RSSI(i)) +
         ",\"lock\":" + (WiFi.encryptionType(i)==WIFI_AUTH_OPEN?"false":"true") + "}";
  }
  j += "]}";
  WiFi.scanDelete();
  http.send(200,"application/json",j);
}
void handleSetWifi(){
  String ssid = http.arg("ssid");
  String pass = http.arg("pass");
  if (ssid.length()==0){ http.send(400,"application/json","{\"ok\":false,\"err\":\"no ssid\"}"); return; }
  prefs.begin("cbwifi", false); prefs.putString("ssid", ssid); prefs.putString("pass", pass); prefs.end();
  WiFi.begin(ssid.c_str(), pass.c_str());
  for (int i=0;i<40 && WiFi.status()!=WL_CONNECTED;i++) delay(250);
  bool ok = WiFi.status()==WL_CONNECTED;
  String ip = ok ? WiFi.localIP().toString() : "";
  http.send(200,"application/json", String("{\"ok\":")+(ok?"true":"false")+",\"ip\":\""+ip+"\",\"mdns\":\""+MDNS_NAME+".local\"}");
  DBGF("[wifi] setwifi '%s' -> %s %s\n", ssid.c_str(), ok?"OK":"FAIL", ip.c_str());
}
void handleForget(){ prefs.begin("cbwifi", false); prefs.clear(); prefs.end(); http.send(200,"application/json","{\"ok\":true}"); }

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
  WiFi.softAP(AP_SSID, AP_PASS);        // always on: first-time setup + fallback
  bool sta = joinSaved();
  DBGF("[wifi] AP=192.168.4.1  STA=%s %s\n", sta?"on":"off", sta?WiFi.localIP().toString().c_str():"");

  tcpServer.begin(); tcpServer.setNoDelay(true);
  if (MDNS.begin(MDNS_NAME)) MDNS.addService("carthabot","tcp",TCP_PORT);

  http.on("/status", handleStatus);
  http.on("/scan",   handleScan);
  http.on("/setwifi",handleSetWifi);     // accepts GET or POST args
  http.on("/forget", handleForget);
  http.begin();

#if ENABLE_BLE
  NimBLEDevice::init("CarthaBot");
  NimBLEServer* server=NimBLEDevice::createServer(); server->setCallbacks(new ServerCB());
  NimBLEService* svc=server->createService(NUS_SERVICE);
  NimBLECharacteristic* rx=svc->createCharacteristic(NUS_RX_CHAR, NIMBLE_PROPERTY::WRITE|NIMBLE_PROPERTY::WRITE_NR);
  rx->setCallbacks(new RxCB());
  txChar=svc->createCharacteristic(NUS_TX_CHAR, NIMBLE_PROPERTY::NOTIFY);
  svc->start();
  NimBLEAdvertising* adv=NimBLEDevice::getAdvertising(); adv->addServiceUUID(NUS_SERVICE); adv->setName("CarthaBot"); adv->start();
#endif
}

void loop(){
  http.handleClient();

  // a NEW TCP client always wins (drops a stale one) -> reconnects never get stuck
  if (tcpServer.hasClient()){
    WiFiClient inc = tcpServer.available();
    if (tcpClient && tcpClient.connected()) tcpClient.stop();
    tcpClient = inc; tcpClient.setNoDelay(true);
  }
  if (tcpClient && tcpClient.connected())
    while (tcpClient.available()) toRobot((uint8_t)tcpClient.read());

  while (RobotSerial.available()){
    uint8_t b=(uint8_t)RobotSerial.read();
    if (tcpClient && tcpClient.connected()) tcpClient.write(b);
#if ENABLE_BLE
    bleTxBuf[bleTxLen++]=b; if (bleTxLen>=sizeof(bleTxBuf)) flushBleTx();
#endif
  }
#if ENABLE_BLE
  if (bleTxLen) flushBleTx();
#endif

  bool linked=(tcpClient && tcpClient.connected())
#if ENABLE_BLE
    || bleConnected
#endif
    ;
  digitalWrite(STATUS_LED, linked?HIGH:((millis()/500)%2));
}
