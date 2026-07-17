/*
 * CarthaBot ESP32-C3 Wireless Bridge  v2  (station-mode friendly)
 * ---------------------------------------------------------------
 * Transparent UART1 <-> Wi-Fi (TCP) + BLE (Nordic UART Service) passthrough.
 *
 * WHAT'S NEW vs v1 (esp32c3_bridge.ino):
 *  1) STATION MODE: the bridge can JOIN your home/school Wi-Fi, so your PC stays on
 *     its normal network (internet kept!) and reaches the robot at carthabot.local:3333
 *     (or the printed IP). No more switching to "CarthaBot-WiFi". Fill HOME_SSID/HOME_PASS.
 *     It ALSO keeps its own AP as a fallback (AP+STA), so it works either way.
 *  2) mDNS: connect to  carthabot.local:3333  instead of hunting for the DHCP IP.
 *  3) RECONNECT FIX: a new TCP client now always wins (the old/stale one is dropped),
 *     so the app can reconnect repeatedly without "connection aborted" errors.
 *
 * WIRING (unchanged):
 *   Pico GP0 (UART0 TX, CN1 SDA) --> ESP GPIO4 (RX)
 *   Pico GP1 (UART0 RX, CN1 SCL) <-- ESP GPIO5 (TX)
 *   CN1 3V3 -> ESP 3V3, GND -> GND.  (optional) ESP GPIO6 -> RP2040 RUN.
 *
 * ROBOT SIDE: the robot needs boot.py with os.dupterm(UART0) so its REPL is on GP0/GP1.
 * The Companion app now installs that automatically on every USB connect.
 */
#include <Arduino.h>
#include <WiFi.h>
#include <ESPmDNS.h>

// ---------------- Config ----------------
#define ENABLE_BLE     1
#define ENABLE_RESET   1
#define DEBUG          1

// ---- Fill these in to use STATION mode (join your own Wi-Fi). Leave SSID "" for AP-only. ----
#define HOME_SSID      ""            // e.g. "TT_2560"   <-- put your Wi-Fi name here
#define HOME_PASS      ""            // e.g. "yourpassword"

#define MDNS_NAME      "carthabot"   // -> carthabot.local
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
WiFiServer tcpServer(TCP_PORT);
WiFiClient tcpClient;

#if DEBUG
  #define DBG(x)   Serial.println(x)
  #define DBGF(...) Serial.printf(__VA_ARGS__)
#else
  #define DBG(x)
  #define DBGF(...)
#endif

void pulseReset() {
#if ENABLE_RESET
  pinMode(RESET_PIN, OUTPUT); digitalWrite(RESET_PIN, LOW); delay(10);
  pinMode(RESET_PIN, INPUT);  DBG("[reset] pulsed RP2040 RUN");
#endif
}
inline void toRobot(uint8_t b) { if (b == RESET_TRIGGER) { pulseReset(); return; } RobotSerial.write(b); }

// ---------------- BLE (Nordic UART Service) ----------------
#if ENABLE_BLE
#include <NimBLEDevice.h>
#define NUS_SERVICE  "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_RX_CHAR  "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_TX_CHAR  "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"
NimBLECharacteristic* txChar = nullptr;
volatile bool bleConnected = false;
uint8_t bleTxBuf[20]; size_t bleTxLen = 0;
void flushBleTx(){ if (bleConnected && txChar && bleTxLen){ txChar->setValue(bleTxBuf, bleTxLen); txChar->notify(); } bleTxLen = 0; }
class ServerCB : public NimBLEServerCallbacks {
  void onConnect(NimBLEServer*){ bleConnected=true; DBG("[ble] connected"); }
  void onDisconnect(NimBLEServer*){ bleConnected=false; DBG("[ble] disconnected"); NimBLEDevice::startAdvertising(); }
};
class RxCB : public NimBLECharacteristicCallbacks {
  void onWrite(NimBLECharacteristic* c){ std::string v=c->getValue(); for(unsigned char ch: v) toRobot((uint8_t)ch); }
};
#endif

void setup() {
#if DEBUG
  Serial.begin(115200);
#endif
  pinMode(STATUS_LED, OUTPUT);
#if ENABLE_RESET
  pinMode(RESET_PIN, INPUT);
#endif
  RobotSerial.begin(UART_BAUD, SERIAL_8N1, UART_RX_PIN, UART_TX_PIN);

  bool useSta = (strlen(HOME_SSID) > 0);
  if (useSta) {
    WiFi.mode(WIFI_AP_STA);                  // join home Wi-Fi AND keep our own AP as fallback
    WiFi.softAP(AP_SSID, AP_PASS);
    WiFi.begin(HOME_SSID, HOME_PASS);
    DBGF("[wifi] joining '%s' ...\n", HOME_SSID);
    for (int i = 0; i < 40 && WiFi.status() != WL_CONNECTED; i++) { delay(250); }
    if (WiFi.status() == WL_CONNECTED)
      DBGF("[wifi] STA connected, IP=%s  -> use %s.local:%d or that IP\n",
           WiFi.localIP().toString().c_str(), MDNS_NAME, TCP_PORT);
    else
      DBG("[wifi] STA failed; AP 'CarthaBot-WiFi' still available");
  } else {
    WiFi.mode(WIFI_AP);
    WiFi.softAP(AP_SSID, AP_PASS);
    DBGF("[wifi] AP-only '%s'  ip=192.168.4.1:%d\n", AP_SSID, TCP_PORT);
  }

  tcpServer.begin();
  tcpServer.setNoDelay(true);

  if (MDNS.begin(MDNS_NAME)) { MDNS.addService("carthabot", "tcp", TCP_PORT); DBGF("[mdns] %s.local\n", MDNS_NAME); }

#if ENABLE_BLE
  NimBLEDevice::init("CarthaBot");
  NimBLEServer* server = NimBLEDevice::createServer();
  server->setCallbacks(new ServerCB());
  NimBLEService* svc = server->createService(NUS_SERVICE);
  NimBLECharacteristic* rxChar = svc->createCharacteristic(NUS_RX_CHAR, NIMBLE_PROPERTY::WRITE | NIMBLE_PROPERTY::WRITE_NR);
  rxChar->setCallbacks(new RxCB());
  txChar = svc->createCharacteristic(NUS_TX_CHAR, NIMBLE_PROPERTY::NOTIFY);
  svc->start();
  NimBLEAdvertising* adv = NimBLEDevice::getAdvertising();
  adv->addServiceUUID(NUS_SERVICE); adv->setName("CarthaBot"); adv->start();
  DBG("[ble] advertising as 'CarthaBot'");
#endif
}

void loop() {
  // ---- accept a client; a NEW client always wins so reconnects never get stuck ----
  if (tcpServer.hasClient()) {
    WiFiClient incoming = tcpServer.available();
    if (tcpClient && tcpClient.connected()) { tcpClient.stop(); DBG("[wifi] dropped stale client"); }
    tcpClient = incoming;
    tcpClient.setNoDelay(true);
    DBG("[wifi] client connected");
  }

  if (tcpClient && tcpClient.connected())
    while (tcpClient.available()) toRobot((uint8_t)tcpClient.read());

  while (RobotSerial.available()) {
    uint8_t b = (uint8_t)RobotSerial.read();
    if (tcpClient && tcpClient.connected()) tcpClient.write(b);
#if ENABLE_BLE
    bleTxBuf[bleTxLen++] = b; if (bleTxLen >= sizeof(bleTxBuf)) flushBleTx();
#endif
  }
#if ENABLE_BLE
  if (bleTxLen) flushBleTx();
#endif

  bool linked = (tcpClient && tcpClient.connected())
#if ENABLE_BLE
                || bleConnected
#endif
                ;
  digitalWrite(STATUS_LED, linked ? HIGH : ((millis() / 500) % 2));
}
