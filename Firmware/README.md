# CarthaBot wireless bridge (ESP32-C3)

Lets the **CarthaBot VPL** app reach the robot over **WiFi** or **BLE** instead of the USB cable.
The ESP32-C3 is a transparent UART bridge: it passes the app's MicroPython paste-mode bytes
straight to the RP2040, so nothing about the program protocol changes.

## Hardware

DFRobot **Beetle ESP32-C3**, wired to the CarthaBot **CN1** header (`3V3 / SCL / SDA / GND`):

| CarthaBot CN1 | RP2040 | → | Beetle pad |
|---|---|:--:|---|
| 3V3 | 3.3 V | → | **3V3** (add a 470–1000 µF cap across 3V3/GND) |
| GND | GND | → | **GND** |
| SDA | GP0 (UART0 TX) | → | **4** (ESP RX) |
| SCL | GP1 (UART0 RX) | ← | **5** (ESP TX) |

The robot must already be running MicroPython (flash it once over USB from the app).

## Build & flash

1. Arduino IDE → install the **ESP32** board package (Espressif), select **DFRobot Beetle ESP32-C3**.
2. Library Manager → install **NimBLE-Arduino** (1.4.x).
3. Open `esp32c3_bridge/esp32c3_bridge.ino`, upload over the Beetle's USB-C.

## How the app connects (defaults already match this firmware)

- **WiFi** — the bridge makes a SoftAP **`CarthaBot-WiFi`** (password `carthabot`).
  Join that network on the PC, then in the app pick *WiFi* and connect to **`192.168.4.1:3333`**.
- **BLE** — the bridge advertises **`CarthaBot`** (Nordic UART Service). In the app pick *BLE*,
  device name **`CarthaBot`**, connect.

The on-board LED (GPIO10) lights while a WiFi or BLE client is connected.

> Change `ENABLE_WIFI` / `ENABLE_BLE` at the top of the sketch to run only one radio if you want.
