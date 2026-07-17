/*
 * CarthaBot ESP32-C3 Wireless Bridge  v4  — set Wi-Fi from the app, over USB (no CarthaBot-WiFi)
 * ----------------------------------------------------------------------------------------------
 * Flash this ONCE. Then, in the Companion app's "Connect to my Wi-Fi" screen, you TYPE your
 * Wi-Fi name + password and press "Save to robot (USB)". The app sends those to the robot over
 * the USB cable; the robot relays them to this ESP, which SAVES them and JOINS your Wi-Fi.
 * From then on the robot rejoins your Wi-Fi automatically on every power-up, and you code over
 * carthabot.local:3333 on your OWN network. You never touch the CarthaBot-WiFi access point.
 *
 * How credentials arrive (no AP, no HTTP needed): the app makes the robot emit a tiny framed
 * line on UART0 -> this ESP's UART1 RX. The frame is:
 *     <0x10> "CBCFG " <ssid> <0x09 tab> <password> <0x0A newline>
 * We intercept that frame (never forward it to the robot/clients), save ssid/pass to flash
 * (NVS), and WiFi.begin() immediately. 0x10 (DLE) never appears in normal REPL output.
 *
 * Also: keeps a fallback AP "CarthaBot-WiFi", mDNS carthabot.local, and the reconnect fix
 * (a new TCP client always wins). BLE + the transparent :3333 bridge are unchanged.
 *
 * WIRING (unchanged): Pico GP0->ESP GPIO4 , ESP GPIO5->Pico GP1 (CN1 SCL) , 3V3 & GND shared.
 * Libraries: NimBLE-Arduino. (Preferences/WiFi/ESPmDNS ship with the ESP32 core.)
 */
#include <Arduino.h>
#include <WiFi.h>
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
#define CFG_MAGIC      0x10          // DLE: starts a "set Wi-Fi" frame from the app

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

// ---- join saved Wi-Fi from flash ----
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

// ---- a "set Wi-Fi" frame arrived from the app (via the robot's UART) ----
void applyConfigLine(const String& line) {
  // line = "CBCFG <ssid>\t<pass>"
  if (!line.startsWith("CBCFG ")) return;
  String rest = line.substring(6);
  int tab = rest.indexOf('\t');
  if (tab < 0) return;
  String ssid = rest.substring(0, tab);
  String pass = rest.substring(tab + 1);
  if (ssid.length() == 0) return;
  prefs.begin("cbwifi", false); prefs.putString("ssid", ssid); prefs.putString("pass", pass); prefs.end();
  DBGF("[cfg] saved Wi-Fi '%s' — joining…\n", ssid.c_str());
  WiFi.disconnect();
  WiFi.begin(ssid.c_str(), pass.c_str());
  for (int i=0;i<40 && WiFi.status()!=WL_CONNECTED;i++) delay(250);
  if (WiFi.status()==WL_CONNECTED) { DBGF("[cfg] joined, IP=%s\n", WiFi.localIP().toString().c_str()); MDNS.begin(MDNS_NAME); }
  else DBG("[cfg] join failed (check password); AP still available");
}

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
  WiFi.softAP(AP_SSID, AP_PASS);     // fallback / first-run
  bool sta = joinSaved();
  DBGF("[wifi] AP=192.168.4.1  STA=%s %s\n", sta?"on":"off", sta?WiFi.localIP().toString().c_str():"");

  tcpServer.begin(); tcpServer.setNoDelay(true);
  if (MDNS.begin(MDNS_NAME)) MDNS.addService("carthabot","tcp",TCP_PORT);

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
  // a NEW TCP client always wins -> reconnects never get stuck
  if (tcpServer.hasClient()){
    WiFiClient inc = tcpServer.available();
    if (tcpClient && tcpClient.connected()) tcpClient.stop();
    tcpClient = inc; tcpClient.setNoDelay(true);
  }
  if (tcpClient && tcpClient.connected())
    while (tcpClient.available()) toRobot((uint8_t)tcpClient.read());

  // robot -> clients, while watching for a "set Wi-Fi" frame from the app
  static bool capturing = false;
  static String cfgLine;
  while (RobotSerial.available()){
    uint8_t b=(uint8_t)RobotSerial.read();
    if (!capturing && b == CFG_MAGIC) { capturing = true; cfgLine = ""; continue; }   // start frame (eaten)
    if (capturing) {
      if (b == '\n') { capturing = false; applyConfigLine(cfgLine); }
      else { cfgLine += (char)b; if (cfgLine.length() > 250) capturing = false; }
      continue;                                                                        // frame bytes eaten
    }
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
