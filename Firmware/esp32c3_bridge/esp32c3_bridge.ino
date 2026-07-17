/*
 * CarthaBot wireless bridge  —  DFRobot Beetle ESP32-C3
 * ------------------------------------------------------
 * Transparently relays bytes between the CarthaBot VPL app and the robot's
 * RP2040 over UART, so the app's MicroPython paste-mode (Ctrl-C/E/D) works
 * unchanged over WiFi or BLE.
 *
 * WIRING to the CarthaBot CN1 header (3V3 / SCL / SDA / GND):
 *      CN1 3V3  -> Beetle 3V3      (power; add 470-1000uF across 3V3/GND)
 *      CN1 GND  -> Beetle GND
 *      CN1 SDA  -> Beetle pad 4    (GP0/UART0-TX on RP2040  ->  ESP RX = GPIO4)
 *      CN1 SCL  <- Beetle pad 5    (GP1/UART0-RX on RP2040  <-  ESP TX = GPIO5)
 *
 * The CarthaBot must already run MicroPython (flash it once over USB with the app).
 *
 * APP PROTOCOL (must match CarthaBotVPL):
 *   WiFi : SoftAP "CarthaBot-WiFi" (pass "carthabot"), raw TCP server on 192.168.4.1:3333
 *   BLE  : Nordic UART Service, device name "CarthaBot"
 *            service 6E400001-...  RX(write) 6E400002-...  TX(notify) 6E400003-...
 *
 * Build (Arduino IDE):
 *   - Board:   "DFRobot Beetle ESP32-C3"  (ESP32 Arduino core 2.0.x / 3.0.x)
 *   - Library: "NimBLE-Arduino" 1.4.x  (Library Manager)
 *   - Upload over the Beetle's USB-C.
 */

#include <WiFi.h>
#include <NimBLEDevice.h>

// ---- pins / link ----
#define UART_RX_PIN 4        // ESP RX  <- CarthaBot SDA (RP2040 GP0/TX)
#define UART_TX_PIN 5        // ESP TX  -> CarthaBot SCL (RP2040 GP1/RX)
#define UART_BAUD   115200
#define STATUS_LED  10       // Beetle on-board LED

// ---- features (disable one if RAM is tight) ----
#define ENABLE_WIFI 1
#define ENABLE_BLE  1

// ---- WiFi ----
const char* AP_SSID = "CarthaBot-WiFi";
const char* AP_PASS = "carthabot";      // must be >= 8 chars
const uint16_t TCP_PORT = 3333;

// ---- BLE / Nordic UART Service ----
#define NUS_SERVICE "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_RX      "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"  // app writes here -> robot
#define NUS_TX      "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"  // robot -> app (notify)
#define BLE_NAME    "CarthaBot"

HardwareSerial Robot(1);                 // UART1 to the RP2040

#if ENABLE_WIFI
WiFiServer tcpServer(TCP_PORT);
WiFiClient tcpClient;
#endif

#if ENABLE_BLE
NimBLECharacteristic* txChar = nullptr;
volatile bool bleConnected = false;

class ServerCB : public NimBLEServerCallbacks {
  void onConnect(NimBLEServer*)    { bleConnected = true; }
  void onDisconnect(NimBLEServer*) { bleConnected = false; NimBLEDevice::startAdvertising(); }
};

// App -> robot: forward every written chunk straight to the UART.
class RxCB : public NimBLECharacteristicCallbacks {
  void onWrite(NimBLECharacteristic* c) {
    std::string v = c->getValue();
    if (!v.empty()) Robot.write((const uint8_t*)v.data(), v.size());
  }
};
#endif

void setup() {
  pinMode(STATUS_LED, OUTPUT);
  digitalWrite(STATUS_LED, LOW);

  Robot.begin(UART_BAUD, SERIAL_8N1, UART_RX_PIN, UART_TX_PIN);

#if ENABLE_WIFI
  WiFi.mode(WIFI_AP);
  WiFi.softAP(AP_SSID, AP_PASS);
  tcpServer.begin();
  tcpServer.setNoDelay(true);
#endif

#if ENABLE_BLE
  NimBLEDevice::init(BLE_NAME);
  NimBLEServer* srv = NimBLEDevice::createServer();
  srv->setCallbacks(new ServerCB());

  NimBLEService* svc = srv->createService(NUS_SERVICE);
  NimBLECharacteristic* rxChar =
      svc->createCharacteristic(NUS_RX, NIMBLE_PROPERTY::WRITE | NIMBLE_PROPERTY::WRITE_NR);
  rxChar->setCallbacks(new RxCB());
  txChar = svc->createCharacteristic(NUS_TX, NIMBLE_PROPERTY::NOTIFY);
  svc->start();

  NimBLEAdvertising* adv = NimBLEDevice::getAdvertising();
  adv->addServiceUUID(NUS_SERVICE);
  adv->setScanResponse(true);
  adv->start();
#endif
}

void loop() {
  bool linked = false;

#if ENABLE_WIFI
  // Accept / refresh a single TCP client.
  if (!tcpClient || !tcpClient.connected()) {
    WiFiClient incoming = tcpServer.available();
    if (incoming) { tcpClient = incoming; tcpClient.setNoDelay(true); }
  }
  // App(WiFi) -> robot
  if (tcpClient && tcpClient.connected()) {
    linked = true;
    while (tcpClient.available()) Robot.write(tcpClient.read());
  }
#endif

#if ENABLE_BLE
  if (bleConnected) linked = true;
#endif

  // robot -> app (WiFi stream + BLE notify), coalesced into small packets.
  static uint8_t buf[180];
  size_t n = 0;
  while (Robot.available() && n < sizeof(buf)) {
    uint8_t b = (uint8_t)Robot.read();
    buf[n++] = b;
#if ENABLE_WIFI
    if (tcpClient && tcpClient.connected()) tcpClient.write(b);
#endif
  }
#if ENABLE_BLE
  if (n > 0 && bleConnected && txChar) {
    txChar->setValue(buf, n);
    txChar->notify();
  }
#endif

  digitalWrite(STATUS_LED, linked ? HIGH : LOW);
}
