# CarthaBot — Complete Wireless Upload Setup Guide (ESP32-C3, Wi-Fi + BLE)

> Upload kids' programs to CarthaBot **with no USB cable and no button presses**, using a
> DFRobot **Beetle ESP32-C3** as a transparent Wi-Fi/BLE bridge into the robot's MicroPython REPL.
>
> Target app: **CarthaBot Companion Junior v2.0**. Also applies to the standalone CarthaBot VPL
> app and the main Companion once their transports are wired.
>
> This is the full, step-by-step version: every stage has a **DO** section and a **VERIFY** check
> so you never move forward on a broken step.

---

## 0. The one concept that makes everything else make sense

CarthaBot has **two different operations people both call "upload," and only ONE needs the
BOOT/RESET buttons.** Confusing them is the single biggest source of wasted time here.

| | **A — Flash the firmware** | **B — Upload a kid's program** |
|---|---|---|
| What moves | MicroPython `.uf2` firmware | Generated MicroPython source code |
| Mechanism | RP2040 pretends to be a USB drive (`RPI-RP2`); you copy the `.uf2` onto it | A byte stream into the already-running MicroPython **REPL** (paste mode) |
| Buttons? | **YES** — hold **BOOT**, tap **RESET** to expose the `RPI-RP2` drive | **NO buttons** |
| Wireless? | **Impossible** over Wi-Fi/BLE (UF2 flashing *is* a USB-mass-storage trick) | **Yes** — USB, Wi-Fi, BLE all carry the identical bytes |
| How often | **Once, ever** (or when you change firmware) | Every time a kid runs a new program |

**The wire protocol for B (transport-agnostic):**

```
0x03   Ctrl-C   interrupt whatever is running   <- this is the "soft reset" between programs
0x05   Ctrl-E   enter paste mode
<generated MicroPython source>\n
0x04   Ctrl-D   execute now
```

**So:** the boot/reset buttons belong to **firmware flashing (A)** — a one-time wired bench step.
After MicroPython is on the robot, the ESP32-C3 carries every program (B) wirelessly with **zero
button presses**. The `0x03` the app sends before each run is what replaces the reset button
between programs.

**The classroom model you actually want:**

1. Flash MicroPython **once** over USB, with the buttons. *(Stage 1)*
2. Leave the ESP32-C3 wired inside the robot. *(Stages 2–3)*
3. Kids upload programs over Wi-Fi/BLE all day — no cables, no buttons. *(Stage 4)*

> Need a *hard* reset without touching the robot (e.g. a program hangs the REPL)? The optional
> **ESP→RUN reset line** (Stage 2 sketch + Stage 3 wiring) gives the app a wireless RESET. The only
> thing that stays wired-only forever is **UF2 firmware flashing**.

---

## 1. What you need

### 1.1 Hardware

| Item | Notes |
|---|---|
| CarthaBot robot (RP2040/Pico) | With its **BOOT** and **RESET** buttons accessible |
| DFRobot **Beetle ESP32-C3** | 3.3 V logic, Wi-Fi + BLE, has its own USB-C |
| 2× USB cables | One for the Pico (Stage 1), one for the Beetle (Stage 2) — can be the same cable used twice |
| 4 jumper wires | 3V3, GND, and the two UART signals |
| 1× **470–1000 µF** electrolytic cap | Across the Beetle 3V3/GND for Wi-Fi current spikes |
| (Optional) 1 extra wire | For the wireless hard-reset line (ESP GPIO → RP2040 RUN) |

### 1.2 Software

- **Windows PC** with the CarthaBot **Companion Junior v2.0** app (built or published).
- **Arduino IDE** (1.8.x or 2.x).
- **esp32 by Espressif** board package (Boards Manager) — core 2.x or 3.x.
- **NimBLE-Arduino** library (lighter and more reliable on the C3 than the stock BLE stack).

> Your network often makes Arduino Library Manager / GitHub downloads flaky. The reliable
> workaround you've used before applies here: download the **NimBLE-Arduino ZIP** once and install
> via *Sketch → Include Library → Add .ZIP Library*.

### 1.3 The firmware file (confirmed location)

The MicroPython firmware is **`BootLoader_microPython.uf2`**, kept in a folder named `u2f`
(a typo for `uf2` — that's why it's easy to miss). **Master / canonical source copy:**

```
C:\Users\LENOVO\Downloads\CarthaBotCompanion-Junior\CompanionApp\Resources\u2f\BootLoader_microPython.uf2
```

Other live copies (auto-generated from the master, so don't hand-edit the `bin\…` ones):

- Published Junior app (runs in production):
  `C:\Users\LENOVO\Desktop\CarthaBot Companion Junior\Resources\u2f\BootLoader_microPython.uf2`
- Standalone VPL app (note: under `Assets\`, not `Resources\u2f\`):
  `C:\Users\LENOVO\Downloads\CarthaBotVPL\CarthaBotVPL\Assets\BootLoader_microPython.uf2`
- Main Companion source:
  `…\CarthabotCompanion-main\CarthabotCompanion-main\CompanionApp\Resources\u2f\BootLoader_microPython.uf2`

> The same `u2f\` folder also holds **`Modes.uf2`** (pre-programmed behaviours) and
> **`MazeCode.uf2`** (maze module). For the wireless paste-mode REPL path you specifically need
> **`BootLoader_microPython.uf2`** — that's the MicroPython runtime the VPL/Kids modules talk to.

---

## 2. Stage 1 — Flash MicroPython onto the robot (one-time, USB + buttons)

This is the **only** step that needs the buttons and a cable. You do it once per robot.

### 2.1 DO

1. Plug the **robot's RP2040** into the PC by USB.
2. **Hold down the BOOT (BOOTSEL) button.** Keep holding it.
3. **Tap the RESET button** once (or, if powering from off: apply power while holding BOOT).
4. **Release BOOT.**
5. The robot now appears in Windows as a removable drive named **`RPI-RP2`**.
6. Copy **`BootLoader_microPython.uf2`** (from the path in §1.3) onto that `RPI-RP2` drive —
   drag-and-drop in Explorer is fine.
7. The drive ejects itself and the chip reboots **straight into MicroPython**. Done.

> What each button does: **RESET** restarts the chip; **BOOT held during reset** tells the ROM to
> come up in USB-mass-storage bootloader mode (`RPI-RP2`) instead of running your code. That combo
> is *only* needed to put firmware on — never for running programs later.

### 2.2 VERIFY (don't skip — everything wireless assumes this worked)

Pick either check:

- **Via the app (easiest):** open Companion Junior, open a module over **USB**, and confirm the
  robot reports **"CarthaBot is ready."** If a tiny program runs over USB, MicroPython is good.
- **Via a serial terminal:** open the new COM port at **115200 8N1** (PuTTY / Arduino Serial
  Monitor), press **Enter**, and you should get the MicroPython **`>>>`** REPL prompt.

If neither works, re-do §2.1 — the wireless stages cannot fix a missing/bad firmware flash.

> ⚠️ There is **no wireless version of this stage.** The ESP32-C3 can't present the `RPI-RP2`
> drive. (Even `machine.bootloader()` only re-enters UF2 mode — you'd still need USB to copy the
> file.) Treat firmware flashing as a wired bench task.

---

## 3. Stage 2 — Flash the bridge firmware onto the ESP32-C3 (one-time)

The Beetle has its **own USB-C**. You flash it like any ESP32 sketch — this is completely separate
from the robot and never touches the robot's buttons.

### 3.1 DO — Arduino IDE setup

1. **Tools → Board → Boards Manager** → install **esp32 by Espressif**.
2. **Tools → Board** → select your Beetle / *ESP32C3 Dev Module*.
3. **Tools → USB CDC On Boot → Enabled** (so the USB serial console works for debugging).
4. Install **NimBLE-Arduino** (Library Manager, or Add .ZIP per §1.2).
5. Paste the sketch from §3.2, save as `esp32c3_bridge.ino`.
6. **Build Wi-Fi-only first:** leave `ENABLE_BLE 0`. Get one transport working before adding the
   second. Then set `ENABLE_BLE 1` and re-flash.

### 3.2 DO — the bridge sketch (Wi-Fi + BLE + optional wireless reset)

This does one job: **byte-for-byte transparent passthrough** between the wireless link and UART1,
both directions. It must never line-buffer or alter bytes — the paste-mode stream contains raw
control bytes (`0x03 0x05 0x04`) that must arrive intact.

```cpp
/*
 * CarthaBot ESP32-C3 Wireless Bridge  (full version)
 * Transparent UART1 <-> Wi-Fi (TCP) and BLE (Nordic UART Service) passthrough,
 * plus an optional wireless hard-reset line to the RP2040 RUN net.
 *
 *   Pico GP0 (UART0 TX) --> ESP GPIO4 (RX)
 *   Pico GP1 (UART0 RX) <-- ESP GPIO5 (TX)
 *   (optional) ESP GPIO6 --> RP2040 RUN net (open-drain pulse = hard reset)
 *
 * USB Serial (the C3's USB-CDC) is used only for debug prints, so it stays free
 * while the robot link runs on UART1.
 */
#include <Arduino.h>
#include <WiFi.h>

// ---------------- Config ----------------
#define ENABLE_BLE     1          // 0 = compile a simpler Wi-Fi-only build first
#define ENABLE_RESET   1          // 0 = no wireless hard-reset line
#define DEBUG          1          // 0 = silence USB-console prints

#define UART_BAUD      115200
#define UART_RX_PIN    4          // ESP RX  <- Pico GP0 (UART0 TX)
#define UART_TX_PIN    5          // ESP TX  -> Pico GP1 (UART0 RX)
#define RESET_PIN      6          // -> RP2040 RUN net (only if ENABLE_RESET)
#define STATUS_LED     10         // match your board's LED pin
#define TCP_PORT       3333
#define RESET_TRIGGER  0x00       // app sends this byte to request a hard reset

const char* AP_SSID = "CarthaBot-WiFi";
const char* AP_PASS = "carthabot";   // must be >= 8 chars (WPA2)

HardwareSerial RobotSerial(1);       // UART1
WiFiServer tcpServer(TCP_PORT);
WiFiClient tcpClient;

#if DEBUG
  #define DBG(x)   Serial.println(x)
  #define DBGF(...) Serial.printf(__VA_ARGS__)
#else
  #define DBG(x)
  #define DBGF(...)
#endif

// ---- optional wireless hard reset: pulse the RP2040 RUN net low ----
void pulseReset() {
#if ENABLE_RESET
  pinMode(RESET_PIN, OUTPUT);
  digitalWrite(RESET_PIN, LOW);     // RUN low = reset
  delay(10);
  pinMode(RESET_PIN, INPUT);        // release (high-Z) = reboot into MicroPython
  DBG("[reset] pulsed RP2040 RUN");
#endif
}

// forward one inbound byte toward the robot, intercepting the reset trigger
inline void toRobot(uint8_t b) {
  if (b == RESET_TRIGGER) { pulseReset(); return; }
  RobotSerial.write(b);
}

// ---------------- BLE (Nordic UART Service) ----------------
#if ENABLE_BLE
#include <NimBLEDevice.h>
#define NUS_SERVICE  "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_RX_CHAR  "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"  // app writes -> robot
#define NUS_TX_CHAR  "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"  // robot -> app (notify)

NimBLECharacteristic* txChar = nullptr;
volatile bool bleConnected = false;
uint8_t bleTxBuf[20];               // NUS notifies in <= 20-byte chunks
size_t  bleTxLen = 0;

void flushBleTx() {
  if (bleConnected && txChar && bleTxLen) {
    txChar->setValue(bleTxBuf, bleTxLen);
    txChar->notify();
  }
  bleTxLen = 0;
}

class ServerCB : public NimBLEServerCallbacks {
  void onConnect(NimBLEServer*)    { bleConnected = true;  DBG("[ble] connected"); }
  void onDisconnect(NimBLEServer*) { bleConnected = false; DBG("[ble] disconnected");
                                     NimBLEDevice::startAdvertising(); }
};
class RxCB : public NimBLECharacteristicCallbacks {
  // NimBLE 2.x: onWrite(NimBLECharacteristic* c, NimBLEConnInfo& info)
  void onWrite(NimBLECharacteristic* c) {
    std::string v = c->getValue();
    for (unsigned char ch : v) toRobot((uint8_t)ch);
  }
};
#endif

void setup() {
#if DEBUG
  Serial.begin(115200);             // USB-CDC console (debug only)
#endif
  pinMode(STATUS_LED, OUTPUT);
#if ENABLE_RESET
  pinMode(RESET_PIN, INPUT);        // start high-Z so we never hold the robot in reset
#endif
  RobotSerial.begin(UART_BAUD, SERIAL_8N1, UART_RX_PIN, UART_TX_PIN);

  // ---- Wi-Fi SoftAP: PC joins "CarthaBot-WiFi"; robot at 192.168.4.1:3333 ----
  WiFi.mode(WIFI_AP);
  WiFi.softAP(AP_SSID, AP_PASS);
  tcpServer.begin();
  tcpServer.setNoDelay(true);
  DBGF("[wifi] AP '%s'  ip=%s  port=%d\n", AP_SSID, WiFi.softAPIP().toString().c_str(), TCP_PORT);

#if ENABLE_BLE
  NimBLEDevice::init("CarthaBot");
  NimBLEServer*  server = NimBLEDevice::createServer();
  server->setCallbacks(new ServerCB());
  NimBLEService* svc = server->createService(NUS_SERVICE);

  NimBLECharacteristic* rxChar = svc->createCharacteristic(
      NUS_RX_CHAR, NIMBLE_PROPERTY::WRITE | NIMBLE_PROPERTY::WRITE_NR);
  rxChar->setCallbacks(new RxCB());

  txChar = svc->createCharacteristic(NUS_TX_CHAR, NIMBLE_PROPERTY::NOTIFY);
  svc->start();

  NimBLEAdvertising* adv = NimBLEDevice::getAdvertising();
  adv->addServiceUUID(NUS_SERVICE);
  adv->setName("CarthaBot");
  adv->start();
  DBG("[ble] advertising as 'CarthaBot'");
#endif
}

void loop() {
  // ---- accept/refresh a single TCP client ----
  if (tcpServer.hasClient()) {
    if (!tcpClient || !tcpClient.connected()) {
      if (tcpClient) tcpClient.stop();
      tcpClient = tcpServer.available();
      tcpClient.setNoDelay(true);
      DBG("[wifi] client connected");
    } else {
      tcpServer.available().stop();          // one client at a time
    }
  }

  // ---- Wi-Fi -> robot ----
  if (tcpClient && tcpClient.connected()) {
    while (tcpClient.available()) toRobot((uint8_t)tcpClient.read());
  }
  // (BLE -> robot happens inside RxCB::onWrite)

  // ---- robot -> Wi-Fi and/or BLE ----
  while (RobotSerial.available()) {
    uint8_t b = (uint8_t)RobotSerial.read();
    if (tcpClient && tcpClient.connected()) tcpClient.write(b);
#if ENABLE_BLE
    bleTxBuf[bleTxLen++] = b;
    if (bleTxLen >= sizeof(bleTxBuf)) flushBleTx();
#endif
  }
#if ENABLE_BLE
  if (bleTxLen) flushBleTx();                 // push partial chunk so telemetry stays live
#endif

  // ---- status LED: solid when linked, slow blink when idle ----
  bool linked = (tcpClient && tcpClient.connected())
#if ENABLE_BLE
                || bleConnected
#endif
                ;
  digitalWrite(STATUS_LED, linked ? HIGH : ((millis() / 500) % 2));
}
```

### 3.3 VERIFY

- Open **Tools → Serial Monitor** at **115200**. On boot you should see the AP line
  (`ip=192.168.4.1 port=3333`) and, if BLE is on, `advertising as 'CarthaBot'`.
- On the PC's Wi-Fi list, **`CarthaBot-WiFi`** should appear.
- On a phone BLE scanner (e.g. nRF Connect), a device named **`CarthaBot`** should advertise.

> **NimBLE version note:** the sketch targets the 1.4.x API. On **NimBLE 2.x**, the `onWrite`
> callback gains a `NimBLEConnInfo&` parameter and a few enums moved. If it won't compile, either
> pin NimBLE-Arduino **1.4.x** or adjust the signature — same "deprecated methods in newer cores"
> class of issue you've hit before.

---

## 4. Stage 3 — Wire the ESP32-C3 into the robot (one-time)

CN1 is repurposed from I²C0 to **UART0**. Both sides are 3.3 V — **no level shifter needed**.

### 4.1 DO — signal + power wiring

| CarthaBot **CN1** | direction | Beetle **ESP32-C3** |
|---|:---:|---|
| **3V3** | → | **3V3** |
| **GND** | → | **GND** |
| **GP0** = UART0 **TX** | → | **GPIO4 (RX)** |
| **GP1** = UART0 **RX** | ← | **GPIO5 (TX)** |

CN1 pin order, top→bottom: **3V3 / SCL(GP1) / SDA(GP0) / GND**.

```
   CarthaBot CN1                         Beetle ESP32-C3
   ┌────────────┐                        ┌──────────────┐
   │ 3V3  ──────┼────────────────────────┤ 3V3          │
   │ SCL  (GP1) ┼──────────────┐    ┌─────┤ GPIO5  (TX)  │
   │ SDA  (GP0) ┼────────────┐ │    │     │ GPIO4  (RX)  │ ◄─┐
   │ GND  ──────┼──────────┐ │ │    │     │ GND          │   │
   └────────────┘          │ │ │    │     │ GPIO6 (opt)  │   │
                           │ │ └────│─────┼──────────────┘   │
   GP0(TX) ────────────────┼─┼──────┼───────────────► GPIO4(RX)
   GP1(RX) ◄───────────────┼─┼──────┘  GPIO5(TX) ────────────┘
   GND ────────────────────┘ └ common ground (mandatory)
```

**Rules that bite if ignored:**

- **Cross TX↔RX.** TX talks to RX. Straight-through = silence.
- **Power:** Wi-Fi current spikes to ~350 mA. Either fit a **470–1000 µF** cap across the Beetle
  3V3/GND, **or** power the Beetle from its **own USB-C / LiPo** and run only **3 wires** to the
  robot (**GND + the two signals**). A **common ground is mandatory** either way.
- ⚠️ **Never feed VBAT (4.2 V) into the Beetle 3V3 pin** — ESP32-C3 absolute max is **3.6 V**. Use
  the robot's regulated 3V3 or the Beetle's own supply.
- ⚠️ **The accelerometer (LIS2DH12) is disabled** while GP0/GP1 are UART — same pins. Fine for VPL/
  Kids programs that don't read tilt; just know it's the tradeoff of going wireless on CN1.

### 4.2 DO — optional wireless hard-reset wire

If you enabled `ENABLE_RESET 1`:

- Connect **ESP GPIO6** to the **RP2040 RUN net** — i.e. the same node the physical **RESET**
  button pulls toward GND. (Identify it at the RESET button: one pad is RUN, the other GND;
  tie GPIO6 to the **RUN** pad.)
- The sketch keeps GPIO6 high-Z and only pulses it **LOW for ~10 ms** when the app sends the
  reset trigger byte, then releases. This mimics a RESET tap and reboots straight into MicroPython
  (RUN is reset, **not** BOOTSEL — it never enters UF2 mode).

### 4.3 VERIFY

- Power the robot. The Beetle's status LED should **blink slowly** (idle, no client yet).
- From the PC, join **`CarthaBot-WiFi`**, then in a terminal `telnet 192.168.4.1 3333` (or a TCP
  tool) and type — you should see the robot's REPL echo come back. The LED should go **solid**
  while connected.

---

## 5. Stage 4 — Everyday wireless upload (no cables, no buttons)

MicroPython is on the robot (Stage 1), the bridge is flashed (Stage 2) and wired (Stage 3). Power
on → the Beetle boots, raises the AP, and advertises BLE. **No buttons.**

In **Companion Junior v2.0**, the **connection chooser appears before the flash wizard**. Choosing
Wi-Fi or BLE calls `OpenModuleWireless(mode)`, which **skips flashing** and opens the module
straight onto the chosen transport.

### 5.1 Wi-Fi path

1. On the PC, join Wi-Fi **`CarthaBot-WiFi`** (password **`carthabot`**).
2. Open the app → open a module (e.g. *Coding under 6 / VPL*).
3. Connection chooser → **Wi-Fi**, address **`192.168.4.1:3333`** (default).
4. The app's `WifiTransport` opens a `TcpClient` to that address. Build a program → **▶ Play** →
   the paste-mode stream goes PC → TCP → ESP → UART0 → REPL → the robot runs it.
5. **📡 Live** streams telemetry back the same way to drive the on-screen 3D digital twin.

### 5.2 BLE path

1. App → open a module → connection chooser → **BLE**.
2. The app's `BleTransport` (WinRT central) scans for an advertised name containing **`CarthaBot`**,
   connects the **Nordic UART Service** (`6E400001…`, write `6E400002…`, notify `6E400003…`), and
   writes in 20-byte chunks.
3. **▶ Play** streams the program over BLE; **📡 Live** receives telemetry notifications.

### 5.3 The "reset" between programs

You never touch RESET. Before each run the app sends **`0x03` (Ctrl-C)** to stop the previous
program, then `0x05 … 0x04` to paste and execute the new one. That Ctrl-C *is* the soft reset.
A *hard* reset (optional §4.2 line) is only for recovering a wedged REPL.

---

## 6. Wi-Fi vs BLE — which to use

| | **Wi-Fi (TCP :3333)** | **BLE (NUS)** |
|---|---|---|
| Speed | Fast | Slower (20-byte chunks) — fine for small programs |
| Range | Larger | ~10 m |
| PC internet | **Lost** while joined to the SoftAP | **Kept** |
| Per-session setup | Join `CarthaBot-WiFi` | None — just pick BLE |
| Best for | Bench work, big telemetry, a dedicated robot laptop | Classrooms; teacher PC that needs internet |
| Status in your build | Unit-tested vs a TCP mock (PASS) | Compiles; needs a live on-hardware run |

Practical default: **BLE for classroom, Wi-Fi for the workbench.** The bridge runs both at once, so
you choose per session, not at firmware time.

### 6.1 Optional: Wi-Fi station mode (keep the PC's internet)

If you'd rather the bridge join the room's router instead of being a SoftAP:

```cpp
WiFi.mode(WIFI_STA);
WiFi.begin("YourRoomSSID", "YourRoomPass");
// then read the DHCP-assigned IP from the USB console: Serial.println(WiFi.localIP());
```

Point the app's Wi-Fi address at that IP instead of `192.168.4.1`. Reserve the IP on the router so
it doesn't change.

---

## 7. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `CarthaBot-WiFi` not visible | Bridge not running / under-powered | Check Beetle power; watch USB console; confirm `softAP()` ran |
| Wi-Fi connects, app can't reach `:3333` | Wrong IP/port or PC not on the AP | PC must be on `CarthaBot-WiFi`; address `192.168.4.1:3333` |
| Connects but robot does **nothing** | MicroPython not actually flashed | Re-do Stage 1 over USB; verify `>>>` or "CarthaBot is ready" first |
| Garbage / dropped bytes | Baud mismatch or TX/RX not crossed | Both ends **115200 8N1**; GP0→GPIO4, GPIO5→GP1 |
| Program uploads but is corrupted | Bridge altering bytes | Raw `write()` passthrough only — no `println`, no `\r\n` stripping |
| Resets / brownouts under Wi-Fi | 350 mA spike | Add 470–1000 µF cap, or power Beetle from its own USB/LiPo |
| BLE device not found | Wrong name / advertising off | Advertised name must contain **`CarthaBot`**; check `adv->start()` |
| BLE connects, no telemetry back | Notify not enabled / chunking | NUS TX `6E400003…` must be **NOTIFY**; flush partial chunks (sketch does) |
| BLE sketch won't compile | NimBLE 1.4.x vs 2.x API drift | Pin NimBLE 1.4.x, or update the `onWrite` signature for 2.x |
| Accelerometer dead | Expected | GP0/GP1 are UART now; the LIS2DH12 shares that bus |
| Hard-reset wire holds robot off | GPIO6 left LOW / wired to GND pad | Must idle high-Z; pulse LOW only; tie to the **RUN** pad, not GND |
| Want a wireless **firmware** flash | Not possible | UF2 is USB mass-storage; Stage 1 is wired. §4.2 gives wireless *reset* only |

---

## 8. Reference card

**Authoritative robot pins (from project docs — use forever):**
Motors left fwd=GP23/rev=GP29, right fwd=GP24/rev=GP28 (sign-magnitude, forward=GP23+GP24).
IR obstacle GP9/10/11, line GP15/16, **all active-HIGH**. NeoPixel GP21, speaker GP20.

**ESP32-C3 bridge:**
UART1 RX=GPIO4 / TX=GPIO5 @ 115200 8N1 · status LED GPIO10 · optional reset GPIO6.

**Wi-Fi:** AP `CarthaBot-WiFi` / `carthabot` · robot `192.168.4.1:3333`.

**BLE:** advertised name `CarthaBot` · Nordic UART Service
`6E400001…` · write `6E400002…` · notify `6E400003…` · 20-byte chunks.

**Paste-mode REPL:** `0x03` (Ctrl-C) `0x05` (Ctrl-E) `<code>\n` `0x04` (Ctrl-D).

**Firmware file:**
`…\CarthaBotCompanion-Junior\CompanionApp\Resources\u2f\BootLoader_microPython.uf2`
(folder is `u2f`; siblings `Modes.uf2`, `MazeCode.uf2` are different firmwares).

---

## 9. One-page cheat sheet

```
ONE-TIME (wired bench)
  [1] Pico: hold BOOT + tap RESET -> RPI-RP2 drive -> copy BootLoader_microPython.uf2
        verify: >>> REPL at 115200, or app says "CarthaBot is ready"
  [2] ESP32-C3: flash esp32c3_bridge.ino over its own USB-C  (build ENABLE_BLE 0 first)
        verify: USB console shows ip=192.168.4.1; "CarthaBot-WiFi" + BLE "CarthaBot" appear
  [3] Wire CN1->Beetle:  3V3-3V3  GND-GND  GP0->GPIO4(RX)  GPIO5(TX)->GP1   (cross TX/RX)
        + cap 470-1000uF (or power Beetle separately, share GND)
        optional: ESP GPIO6 -> RP2040 RUN net (wireless hard reset)

EVERY DAY (no cables, no buttons)
  power on -> app -> connection chooser
     Wi-Fi : join "CarthaBot-WiFi"/"carthabot", target 192.168.4.1:3333
     BLE   : pick BLE, app finds "CarthaBot" (Nordic UART Service)
  build program -> Play   (app sends 0x03 0x05 <code> 0x04 -> REPL runs it)
  Live -> 3D digital twin

CAN'T do wireless: UF2 firmware flashing (always USB + buttons).
CAN do wireless:   every kid program, soft reset (Ctrl-C), and -- with the
                   optional GPIO6->RUN wire -- a hard reset too.
```
