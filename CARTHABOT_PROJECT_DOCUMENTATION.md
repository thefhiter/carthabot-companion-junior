# CarthaBot — Full Project Documentation & Schema

> Master reference for everything built around **CarthaBot**, the FAB619 educational robot.
> Covers the hardware schema, the robot communication protocol, all three desktop apps,
> the VPL compiler / simulator / live digital twin, the 3D-model pipeline, and the full
> file/asset reference.
>
> **Author of this build work:** Claude (assistant), for the user (FAB619 / aiautomation).
> **Last consolidated:** 2026-06-19.
> **Canonical shipped app:** *CarthaBot Companion Junior* (`CompanionApp`), version **v2.0**.

---

## 0. Table of contents

1. [What CarthaBot is](#1-what-carthabot-is)
2. [The codebases (where everything lives)](#2-the-codebases-where-everything-lives)
3. [Hardware schema (THE schematic / pin map)](#3-hardware-schema-the-schematic--pin-map)
4. [Robot communication protocol (flash + REPL + transports)](#4-robot-communication-protocol)
5. [Software architecture — Companion Junior](#5-software-architecture--companion-junior)
6. [The VPL compiler — generated MicroPython](#6-the-vpl-compiler--generated-micropython)
7. [Simulator, Live Digital Twin, Missions & Maps](#7-simulator-live-digital-twin-missions--maps)
8. [Kids Coding method](#8-kids-coding-method)
9. [3D-model pipeline (Blender → HelixToolkit)](#9-3d-model-pipeline-blender--helixtoolkit)
10. [The standalone CarthaBot VPL app](#10-the-standalone-carthabot-vpl-app)
11. [ESP32-C3 Wi-Fi/BLE bridge firmware](#11-esp32-c3-wifible-bridge-firmware)
12. [Build / publish / run](#12-build--publish--run)
13. [Version history (changelog)](#13-version-history-changelog)
14. [Known TODOs & caveats](#14-known-todos--caveats)
15. [File & asset reference tables](#15-file--asset-reference-tables)

---

## 1. What CarthaBot is

**CarthaBot** is FAB619's proprietary educational robot for children — a Thymio-II–inspired
clone built on a **Raspberry Pi RP2040 / Pico** microcontroller. It teaches programming through
several age-graded methods, driven from a Windows desktop "Companion" application.

Physically (latest 3D-printed version, 29 Oct 2025): a **yellow/amber chamfered-rectangular
chassis** (~107 × 103 mm footprint) with a **smiley face engraved on the front**, oval IR vents
on the sides, two side drive wheels (black tyres, yellow rims), a **translucent white top cover**
with a D-pad of button holes that glows from RGB LEDs, a red top button, and a front ball-caster.

The Companion app offers four programming methods, age-graded:

| Method | Audience | Description |
|---|---|---|
| **Behaviours** | youngest | Pick pre-made robot behaviours. |
| **Coding (under 6) — VPL** | under 6 | Thymio-style **event → action** visual rule pairing (the flagship module built here). |
| **Kids Coding (under 7)** | under 7 | Tangible icon-block programming (built here; later hidden from the menu, see §8). |
| **Learn (CarthaSoft)** | school age | Scratch-like block editor in a WebView2. |
| **Advanced Programming** | oldest | Raw Python / MicroPython. |

Every method ultimately **generates MicroPython** and streams it to the robot over a serial
(or wireless) link.

---

## 2. The codebases (where everything lives)

There are **three desktop apps** plus the upstream FAB619 dev repo and a 3D-asset toolchain.

| # | Name | Path | Stack | Role |
|---|---|---|---|---|
| 1 | **Companion (main)** | `C:\Users\LENOVO\Downloads\CarthabotCompanion-main\CarthabotCompanion-main` | WPF / .NET 6, Prism + Syncfusion + DMSkin + MahApps | Original upstream app (GitHub `FAB619/CarthabotCompanion`). Kids Coding was first added here. |
| 2 | **Companion Junior** ⭐ | `C:\Users\LENOVO\Downloads\CarthaBotCompanion-Junior` | same stack **+ HelixToolkit.Wpf 3.1.2** | **Flagship / most-developed.** Embeds the VPL studio (`Module.VplJunior`), the 3D robot, simulator, live twin, missions, maps, and the USB/Wi-Fi/BLE connection chooser. **This is the canonical app — most of this document describes it.** |
| 3 | **CarthaBot VPL (standalone)** | `C:\Users\LENOVO\Downloads\CarthaBotVPL` | plain WPF / .NET 6 (NO Prism/Syncfusion) + `System.IO.Ports` | Independent clone of the Thymio VPL interface in FAB619 branding. Built on user instruction "make it another app, don't change the others." |
| 4 | **FAB619 dev repo** (reference) | `C:\Users\LENOVO\Downloads\Carthabot-dev` | Altium HW + firmware + CAD + CarthaSoft web app | **Ground truth** for hardware schematics, firmware pin maps, CAD/STL, and the CarthaSoft web payload. Read-only reference. |
| 5 | **3D model toolchain** | `C:\Users\LENOVO\tools\carthabot_models` + `C:\Users\LENOVO\tools\` | Blender 5.1 headless Python scripts | Builds every `.obj/.mtl/.glb` asset + sim-world manifests. See §9. |

> ⚠️ **License caveat:** the repo `LICENSE` forbids redistribution without FAB619's written
> permission. Do **not** auto-publish a public GitHub repo for these apps without confirming
> with the user first (this overrides the standing "GitHub repo per project" rule).

### Project relationship diagram

```
FAB619/CarthabotCompanion (main)  ──fork/copy──►  CarthaBotCompanion-Junior  (flagship)
        │                                               │
        │ (Kids Coding added here first)                ├── embeds VPL studio  (CompanionApp/Vpl/…)
        │                                               ├── 3D robot + simulator + live twin
        │                                               └── USB / Wi-Fi / BLE chooser

CarthaBotVPL (standalone)  ◄── same VPL concept, no Prism, fully independent

Carthabot-dev  ──► hardware schematics, firmware pin maps, CAD/STL, CarthaSoft web app  (reference)

tools/carthabot_models  ──► builds all 3D .obj/.mtl + sim manifests ──► copies into CompanionApp/Vpl/Assets/
```

---

## 3. Hardware schema (THE schematic / pin map)

CarthaBot is a **Thymio-II RP2040 clone** designed in Altium by FAB619. This section is the
authoritative electrical schema, reconstructed from the FAB619 dev repo firmware and schematics.

### 3.1 Where the real schematics live

All under `C:\Users\LENOVO\Downloads\Carthabot-dev\`:

| Artifact | Path |
|---|---|
| Top-level schematic PDF | `Schematic Carthabot V0 PDF.pdf` |
| Altium project (V2.0.1) | `Hardware Design\CARTHBOT V2.0.1\CarthaBOT.PrjPCB` |
| RP2040 MCU sheet | `Hardware Design\CARTHBOT V2.0.1\…microcontroleur.SchDoc` (net labels confirm GP0/GP1) |
| IR-sensor sheet | `Hardware Design\CARTHBOT V2.0.1\IR-sensors.SchDoc` |
| LED sheet | `Hardware Design\CARTHBOT V2.0.1\LEDs.SchDoc` |
| Power / Control-buttons sheets | `Power supply.SchDoc`, `Control buttons.SchDoc` |
| Exported PDF (V2.0.2) | `Hardware Design\CARTHBOT V2.0.2\CARTHBOT\CarthaBOT.pdf` |
| Gerbers / BOM / Pick&Place | `Hardware Design\CARTHBOT V2.0.1\FABRICATION FILES\…` |
| 3D PCB STEP | `…\CarthaBOTPCB.step` |
| Datasheets | `…\DOC\pico-datasheet.pdf`, `esp32_datasheet_en.pdf`, `esp32-wrover…pdf` |

> No Altium/KiCad is installed on this machine — view schematics via the exported **PDF**.

### 3.2 Authoritative GPIO pin map ⭐

This is the **ground-truth map** (found in `Carthabot-dev\Software\FW\FW V0\lib\` and the
MicroPython exercises `Exercice CarthaBot\…\MicroPython\{Suiveur_de_ligne.py, Eviteur_d'obstacle.py}`).
**Use these forever.**

#### Motors (2-input L9110-style H-bridge per wheel, sign-magnitude PWM)

| Wheel | Forward coil | Reverse coil |
|---|---|---|
| **Left (M1)** | **GP23** | GP29 |
| **Right (M2)** | **GP24** | GP28 |

- Drive scheme: **sign-magnitude PWM** — the *active* coil is driven at `duty_u16(_duty(speed))`,
  the *other* coil is held at **0**. Clean coast at 0. Same effective speed forward and back on
  both wheels.
- `forward` = drive GP23 + GP24 (both forward coils).
- Default speed 150/255 (≈59% duty).
- **Direction convention (firmware-verified & hardware-confirmed):**
  - `forward = motors(+s, +s)`
  - `back    = motors(−s, −s)`
  - `left    = motors(−s, +s)` (left wheel back, right wheel forward → CCW)
  - `right   = motors(+s, −s)`

> ⚠️ **Hard-won lesson:** the firmware's nominal M1/M2 forward polarity did **not** match the
> physical robot. After ~10 round-trips (versions v1.0.6 → v1.1.x) the **hardware-confirmed**
> map above won. See §13 for the full saga. The single most useful debugging trick was putting
> the build version in the title bar so the user's reports became trustworthy.

#### Infrared sensors (`lib/IRSensors/IRSensors.h`, all `digitalRead`, **ACTIVE-HIGH** → `1` = detected)

| Sensor | Pin | Sensor | Pin |
|---|---|---|---|
| Left | GP8 | Right | GP12 |
| Front-Left | GP9 | Back-Right | GP13 |
| **Front (centre)** | **GP10** | Back-Left | GP14 |
| Front-Right | GP11 | **Bottom-Left (line)** | **GP15** |
| | | **Bottom-Right (line)** | **GP16** |

- Helper logic used by the apps:
  - `front_obstacle()` = `ir(9) or ir(10) or ir(11)`
  - `on_line()` = `ir(15) or ir(16)`
- **Line-follow logic** (from `Suiveur_de_ligne.py`, sensors GP15=`ir_GL`/GP16=`ir_GR`):
  both `==1` → forward; left only → steer right; right only → steer left; none → creep/stop.
  (`1` = on the black line.)

> ⚠️ Earlier app builds **guessed** `IR_FRONT_PIN=26 / IR_GROUND_PIN=27` — those pins are **not**
> sensors, which is why line-following "didn't work." The authoritative pins above replaced them
> in v1.1.4. (The standalone VPL app may still carry the old 26/27 guess — fix when porting.)

#### Other peripherals

| Peripheral | Pin(s) |
|---|---|
| **NeoPixel** strip (11 LEDs) | GP21 |
| **Speaker** / buzzer | GP20 |
| Button — Forward | GP7 |
| Button — Down | GP17 |
| Button — Left | GP18 |
| Button — Right | GP19 |
| Button — Centre | GP22 |
| **I²C0 SDA** (CN1 + accelerometer) | **GP0** |
| **I²C0 SCL** (CN1 + accelerometer) | **GP1** |
| On-board accelerometer | **LIS2DH12** (U500) on I²C0 (GP0/GP1) |

### 3.3 Expansion connectors

| Connector | Type | Pinout |
|---|---|---|
| **CN1** | 4-pin JST (I²C0 / power) | top→bottom **3V3 / SCL(GP1) / SDA(GP0) / GND** |
| CN3 | 8-pin header | VBAT / OK / T1 / T2 / MS (test + program) |
| CN2 / CN700 | 2-pin | — |
| B600 / B601 | motor connectors | — |

- **CN1** is the same concept as the real Thymio's "wireless extension" connector (I²C + power).
  The genuine Thymio wireless module is an ATmega256RFR2 802.15.4/Zigbee/Aseba pair — **not Wi-Fi**
  and incompatible with the RP2040.
- GP0/GP1 are also the RP2040's **native UART0** pins → CN1 is repurposed as **UART0** to attach an
  ESP32-C3 Wi-Fi/BLE bridge (see §4.3 / §11). Repurposing the pins **disables the accelerometer**
  (it shares the bus / its pull-ups remain).

### 3.4 CAD / mechanical reference (for 3D modelling)

The shipped 3D robot is built from FAB619's **real STL parts** (mm units, 107 × 103 mm footprint):

```
Carthabot-dev\CAD Design\Carthabot 3D printed version 29_10_2025\00_STL\
  00_Carthabot Bottom Chassis.STL
  01_Carthabot 3rd Weel 3D Printed.STL      (front ball-caster)
  05_Carthabot Button Cup.STL
  06_Carthabot Top Cover With Logo.STL      (smiley + logo)
```

Reference imagery: photo `…\CarthaSoft\WebApplication\ui\devinfo\media\CathaBot.jpg`;
line-art `…\Graphics\Graphic Content\{ViewCapture20240730_183447.jpg, Plan de travail 1AA.jpg}`.
Altium 3D models (`.stp`) under `Hardware Design\CARTHBOT V2.0.1\Models\` (incl. `a_thymio_2_asm.stp`).

---

## 4. Robot communication protocol

The same protocol is used by every app and every transport; **only the transport changes**.

### 4.1 Flashing firmware

1. Hold BOOTSEL / power-on so the RP2040 enumerates as a mass-storage drive **`RPI-RP2`**.
2. Copy the appropriate **`.uf2`** to that drive (e.g. `BootLoader_microPython.uf2` for the
   VPL / Kids modules). The chip reboots into MicroPython.

The Companion's flash wizard (`ShowPlugInAnimation` overlay in `Views/MainView.xaml`, driven by
`MainViewModel`) animates: *Turn-on → Plug-USB → boot + reset → Detecting → Flashing → Done.*

### 4.2 MicroPython paste-mode REPL (the wire protocol)

Once flashed, the robot talks over **USB serial** as a MicroPython REPL. Programs are streamed
in **paste mode**:

```
0x03   Ctrl-C   interrupt any running program
0x05   Ctrl-E   enter paste mode
<the generated MicroPython source>\n
0x04   Ctrl-D   execute
```

This exact byte stream (`03 05 <code>\n 04`) is transport-agnostic.

### 4.3 Transports (USB / Wi-Fi / BLE)

A small transport abstraction lets the *same* REPL stream go over three links. Defined in
`CompanionApp/Vpl/Services/` (namespace `CarthaBotVPL.Services`):

| File | Transport | Details |
|---|---|---|
| `ITransport.cs` | interface + `ConnectionMode{Usb,Wifi,Ble}` | adds `ReadExisting()` so the live 3D twin keeps receiving telemetry over any link |
| `SerialTransport.cs` | **USB** | opens the COM port (host already flashes; no flashing here) |
| `WifiTransport.cs` | **Wi-Fi** | `System.Net.Sockets.TcpClient`, default **`192.168.4.1:3333`** |
| `BleTransport.cs` | **BLE** | WinRT `Windows.Devices.Bluetooth` central, **Nordic UART Service** (NUS) — svc `6E400001…`, RX-write `6E400002…`, TX-notify `6E400003…`, 20-byte chunked writes; scans for advertised name containing the target (`CarthaBot`) |

- To use WinRT BLE the project's **TFM was bumped** `net6.0-windows` → **`net6.0-windows10.0.19041.0`**
  (`+SupportedOSPlatformVersion 10.0.17763.0`). The whole Prism/Syncfusion/DMSkin/MahApps/Helix
  solution still builds with 0 errors.
- The **connection chooser** appears *before* the flash wizard (v2.0): `MainViewModel.LoadModuleMethod`
  sets `ShowConnectionChooser=true`; the overlay in `MainView.xaml` offers USB/Wi-Fi/BLE cards +
  Wi-Fi `IP:port` & BLE-name textboxes. USB → flash wizard then open over USB; Wi-Fi/BLE →
  `OpenModuleWireless(mode)` skips flashing and opens the module passing the mode/param down to
  `VplView(ea, oldComs, ConnectionMode, string)` → `VplViewModel(...)`.

> **Scope note:** wireless is fully wired only for `Module.VplJunior`. Other modules show the
> chooser but still use USB internally.

---

## 5. Software architecture — Companion Junior

The VPL studio is embedded as **`Module.VplJunior`** ("Coding (under 6)") in the Prism shell.
Everything lives under `CompanionApp/Vpl/`.

### 5.1 Folder map

```
CompanionApp/
├── Controls/
│   ├── Model3DViewer.xaml(.cs)     HelixViewport3D + ModelImporter; Source/AutoRotate/RotateSeconds DPs
│   ├── MiniBot3D.xaml(.cs)         tiny shared-mesh Viewport3D inside each action card (Idle/Move/Glow)
│   └── RobotSimulator.xaml(.cs)    full HelixViewport3D playground sim + live-twin HUD + maps + missions
├── Models/Classes/KidsBlock.cs     Kids Coding block model (see §8)
├── ViewModels/KidsCodingViewModel.cs
├── Views/KidsCodingView.xaml(.cs)
├── Views/MainView.xaml             home grid + flash wizard + connection chooser overlays
├── Views/AtelierView.xaml          the Modules/Ateliers card grid (hardcoded Borders!)
├── Views/AssamblyView.xaml(.cs)    Assembly Manual; 2D SVG ↔ 3D exploded-view toggle
├── Resources/
│   ├── Settings.ini                Version = v2.0  (drives the title bar)
│   ├── StringResources.xaml        EN strings (vpl*, kids*, learn* keys)
│   └── StringResources-FR.xaml     FR strings
├── CarthaSoft/                     the 428-file Scratch-like web app (Learn module payload)
└── Vpl/
    ├── README.md
    ├── Assets/                     all 3D .obj/.mtl + sim manifests + textures (see §15)
    ├── Models/
    │   ├── VplModel.cs             VplEvent / VplAction / enums (the program data model)
    │   ├── Missions.cs             mission definitions
    │   └── MapCatalog.cs           SimMap.All = classic / adventure / city / garden
    ├── Services/
    │   ├── ITransport.cs / SerialTransport.cs / WifiTransport.cs / BleTransport.cs   (§4.3)
    │   ├── VplCompiler.cs          rules → MicroPython (the heart; §6)
    │   ├── VplRuntime.cs           autonomous in-app interpreter (sim brain)
    │   ├── LiveTwin.cs             dead-reckoning digital twin fed by robot telemetry
    │   ├── LedAnimMath.cs          shared LED-animation maths (twin + runtime identical)
    │   ├── SimWorld.cs             loads a *_simworld/*_map_*.json manifest; ray/point queries
    │   └── UiSounds.cs             in-memory WAV synth for PC click/preview sounds
    ├── ViewModels/
    │   ├── VplViewModel.cs         the whole studio VM (rules, play, live, connect, missions)
    │   ├── Converters.cs           EnumToBool, StringToColor, …
    │   └── Mvvm.cs                 base VM / RelayCommand
    └── Views/
        └── VplView.xaml(.cs)       the Thymio-style studio UI
```

### 5.2 The VPL studio UI (`VplView.xaml`)

Faithful to the Thymio VPL reference:

- **Toolbar:** files (New/Open/Save) · green "compilation successful" banner · undo ·
  round **▶ Play / ⏹ Stop** · **📡 Live** (dark-red) · 🔌 **Connect ▾** popup (USB/Wi-Fi/BLE) ·
  🏆 missions (yellow star-count badge) · 🚀 advanced-mode toggle · 🗺 map picker ·
  ☀/🌙 light-dark theme toggle · info / screenshot / home.
- **Layout:** orange **Events** column (Buttons / Obstacle / Line / Start / Timer) │ rule canvas
  (rows with dotted `•••` connectors, dashed `+` add-slots, faint row numbers, ✕ delete) │
  blue **Actions** column (Move / LED Colour / Sound / Wait / Light-show / Memory-state).
- Action cards embed a **live 3D `MiniBot3D`** thumbnail that drives/spins/glows to preview the action.
- All text is localized EN/FR via `DynamicResource` (`vpl*` keys) so it follows the global language
  toggle; runtime theme swap via `ApplyTheme` (code-behind swaps `Vpl*` brushes).

### 5.3 The program data model (`Vpl/Models/VplModel.cs`)

A program is a list of **rules**, each = one `VplEvent` (trigger) + one `VplAction` (effect).

```
VplEvent
  EventKind   { Buttons, Obstacle, Line, Start, Timer }
  Button      (which D-pad button)
  StateFilter (advanced: -1 any, else 0..3 — the 4 memory shapes ★♥●■)

VplAction
  ActionKind  { Move, Color, Sound, Wait, Anim, State }
  Move        MoveDir { Forward, Backward, Left, Right, Stop, FollowLine }
  ColorHex    (LED colour)
  Sound       (preset 0..4; 4 = "My tune")
  Seconds     (Wait duration 0.2–5 s)
  Anim        { Rainbow, Blink, Chase, Breathe }   (light-show)
  Notes       (6-slot × 5-pitch pentatonic "My tune")
  StateValue  (advanced: which memory shape to set)
```

Programs persist as **`.cbvpl`** JSON (a DTO with nullable fields so old files still load).

---

## 6. The VPL compiler — generated MicroPython

`Vpl/Services/VplCompiler.cs` turns the rule list into a complete MicroPython program. The output
is the **definitive contract** with the hardware, so it embeds the authoritative pin map from §3.2.

### 6.1 Structure of generated code

```python
# pins (authoritative)
m1_fwd, m1_rev = Pin(23), Pin(29)      # LEFT  wheel coils
m2_fwd, m2_rev = Pin(24), Pin(28)      # RIGHT wheel coils
ir_fl, ir_fc, ir_fr = Pin(9), Pin(10), Pin(11)   # front obstacle (active-HIGH)
ir_gl, ir_gr        = Pin(15), Pin(16)           # bottom line   (active-HIGH)
np  = NeoPixel(Pin(21), 11)
spk = Pin(20)

def _duty(speed): ...                  # 0..255 → 0..65535
def motors(l, r):                      # SIGN-MAGNITUDE: drive active coil at duty, other = 0
def front_obstacle():  return ir_fl.value() or ir_fc.value() or ir_fr.value()
def on_line():         return ir_gl.value() or ir_gr.value()
def set_color(r,g,b):  ...             # NeoPixel
# optional subsystems emitted ONLY when used: state / timer / anim / tune / presets / telemetry

while True:                            # the rule loop — level-triggered every pass
    # each rule: if <event-condition>: <latched action>
    # Wait / Sound block the loop but motors keep running (latched)
    # follow-line steers every pass using the live ground sensors
```

### 6.2 Final, hardware-confirmed semantics

| Concept | Generated code |
|---|---|
| Forward | `motors(s, s)` |
| Backward | `motors(-s, -s)` |
| **Left** | `motors(-s, s)` |
| **Right** | `motors(s, -s)` |
| Stop / coast | `motors(0, 0)` |
| Follow line (latched mode) | both ground sensors → straight; left only → `motors(s,-s)`; right only → `motors(-s,s)`; none → creep |
| Obstacle event "detected" | `front_obstacle()` (active-HIGH) |
| Line event "on line" | `on_line()` (active-HIGH) |
| Wait | `time.sleep(x)` (blocks loop, motors keep latched value) |
| Light-show | non-blocking `anim_tick()` in the loop; `set_color` cancels it |
| Button "hold-to-drive" | movement runs *while held*; release edge emits `motors(0,0)` |

- The compiler emits **each subsystem only when used** (state/timer/anim/tune/presets/telemetry).
- The **kid-facing code overlay** calls `Generate(rules)` (no telemetry → clean code); the live
  twin uses `Generate(rules, telemetry:true)` which adds a ~10 Hz print of
  `T,l,r,front,ground,r,g,b,anim,RAWir_gl,RAWir_gr`.

### 6.3 Mascot equivalents

- **Kids Coding** (`KidsCodingViewModel.cs`) uses the same sign-magnitude `_motors(l,r)` helper
  and the same forward/left/right mapping.
- The **standalone CarthaBotVPL** has its own `Services/VplCompiler.cs` with the same intent but
  may still carry the **old motor scheme + the 26/27 IR-pin guess** — port the §3.2 map when next
  touched. (See §14.)

---

## 6.5 The Draw module — "Draw with a pen" (turtle plotter)

A separate Companion module (`CompanionApp/Draw/`) that turns a 2-D drawing into robot motion for a
**fixed pen** mounted in the robot's hole (the pen always touches the paper, so the whole drawing is
ONE continuous line). Reached from the modules screen as the pink **"Draw with a pen"** card
(`Module.Draw`); reuses the shared `CarthaBotTransport` (USB/WiFi paste-mode) exactly like the VPL.

### 6.5.1 Pipeline

```
ink strokes / shape / typed word          (canvas pixels, Y down)
   └─ FlattenOrdered()      join strokes, nearest-neighbour chaining to minimise pen-down travel
   └─ centre + scale        to DrawSettings.SizeMm (longest side), flip to paper coords (Y up)
   └─ DouglasPeucker()      drop freehand jitter (SimplifyMm tolerance)
   └─ PathToMoves()         per segment: turn-to-heading (＋=left/CCW) + roll-forward (mm)
   └─ Emit()                MicroPython: motors()/fwd(mm)/turn(deg) timed by MMPS / DPS
   └─ stream                Ctrl-C×3 (drain) → Ctrl-E → code → Ctrl-D    (same as VPL ▶)
```

Heading starts at **90° (facing the top of the page)** — the robot must be placed facing "up".
Motor wiring / `motors()` are copied verbatim from §6 (`VplCompiler`), so the Draw module stays in
lock-step with the verified pin map.

### 6.5.2 Calibration (the two values that matter)

`fwd(mm)` and `turn(deg)` convert distance/angle to time via two constants emitted in the program:

- **`MMPS`** — millimetres rolled per second at `DRAW` power → tune so a 📏 *100 mm test line* measures 10 cm.
- **`DPS`** — degrees spun per second at `TURN` power → tune so a ↻ *90° test* lands square.

Both, plus drawing size / powers / smoothing, are sliders in the UI and persist to
`%APPDATA%\CarthaBot\draw_calibration.json` between sessions. Best results when the pen sits near the
centre of the two wheels (a fixed pen offset from the turn axis blobs at each corner).

### 6.5.3 Files

| File | Role |
|------|------|
| `Draw/Services/DrawCompiler.cs` | shapes, flatten/order, Douglas–Peucker, path→moves, MicroPython emit |
| `Draw/Services/DrawText.cs` | single-stroke A–Z / 0–9 vector font for "write a word" |
| `Draw/ViewModels/DrawViewModel.cs` | connection, streaming, live plan, code preview, calibration persist |
| `Draw/Views/DrawView.xaml(.cs)` | InkCanvas, shape palette, preview overlay, code overlay, sliders |
| `Draw/Views/Converters.cs` | string→visibility for the plan chip |
| `CompanionApp.DrawTests/` | xUnit tests for the geometry engine (compiles the sources directly) |

Tests: `dotnet test CompanionApp.DrawTests/CompanionApp.DrawTests.csproj` (17 tests; isolated, not in
the app solution so it never affects the published build).

### 6.5.4 Features

Freehand ink + erase mode · stamp shapes (square, triangle, circle, star, heart, zig-zag, spiral) ·
write a word · 👁 path preview overlay (start dot + turn dots) · `</>` generated-code viewer ·
live "N moves · X cm · ~Y s" plan · save/open drawings (`.isf`) · USB/WiFi · keyboard
(Ctrl+Z / Ctrl+S / Ctrl+Enter).

Fully **localized EN/FR** — all Draw-screen chrome + main-flow status lines use `draw*` keys present
in both `StringResources.xaml` and `StringResources-FR.xaml` (50 keys, verified at parity).

**First-time Wi-Fi (shared with the main chooser):** `Wireless/WifiState.cs` remembers, in
`%APPDATA%\CarthaBot\wifi.json`, whether the robot has ever been put on Wi-Fi from this PC (+ the
address it ended up at). The **first** time Wi-Fi is chosen — in the connection chooser *or* the Draw
studio — the app opens a setup that **scans nearby networks** (live `netsh` + saved profiles, via
`Wireless/WifiProvisioner.cs`), pre-fills the PC's current SSID/password, and **provisions the robot
over the USB cable** (`SaveOverUsbAsync` → CBCFG relay → ESP32-C3 joins) then connects over Wi-Fi.
Afterwards it's marked provisioned, so later Wi-Fi connects go **straight to the saved address** with no
cable. A ⚙ button (Draw) and the "First time?" button (chooser) re-open setup to change networks.

**Limitation (by hardware):** fixed pen ⇒ one continuous line; multiple shapes/letters are joined by a
thin travel line (minimised by nearest-neighbour ordering, but unavoidable without a pen-lift servo).

---

## 7. Simulator, Live Digital Twin, Missions & Maps

The in-app 3D world lets kids watch their program run without hardware, and (📡 Live) mirror the
**real** robot in real time.

### 7.1 Autonomous simulator (`VplRuntime` + `RobotSimulator`)

- **Event-driven** interpreter (`VplRuntime.cs`) executes the rules with *exact firmware semantics*:
  level-triggered every pass, latched actuators, Wait/Sound block but motors keep running,
  one-shot timer, state gating, differential drive (~2 u/s at speed 150).
- `SimWorld.cs` loads a manifest JSON (exact Blender coords) and answers ray-vs-AABB and
  point-to-polyline sensor queries so sensing matches the rendered geometry.
- `RobotSimulator.xaml(.cs)` renders a HelixViewport3D playground: textured/decorated floor,
  rolling wheels, IR-beam visualisation, ground-sensor dot, LED **halo disc** under the bot,
  pen-trail in the current LED colour, sky gradient + drifting clouds, 🌙 disco mode,
  🎥 chase-camera, confetti + triple-hop on mission complete, and an on-screen **orange D-pad**
  (press-and-hold fires Button events) + HUD chips (🚧 IR, line, state shape, coins).

### 7.2 Live digital twin (`LiveTwin.cs`)

- 📡 **Live** compiles with `telemetry:true`, streams it, then a background `ReaderLoop`
  parses the `T,…` lines into a `TelemetryReceived` event → `Sim.FeedTelemetry(int[])`.
- `LiveTwin` latches actuator/sensor values and **dead-reckons** the pose using proper
  differential-drive kinematics:
  - per-wheel `vL = l/75`, `vR = r/75` u/s (150 cmd → 2 u/s);
  - body `v = (vL+vR)/2`, `omega = (vL−vR)/track` (track ≈ 2.8 u);
  - **exact circular-arc integration** (not Euler) so curves don't drift; straight-line branch
    when `|omega| < 1e-9`.
- A live **HUD** (`LiveDetailsPanel`, monospace) shows L/R wheel cmd + u/s, linear speed,
  turn °/s + left/right word, arc radius / straight / spin, heading, distance moved, total turned,
  and the **raw IR sensor values** (`front OBSTACLE/clear`, `line L=x R=y ON LINE/off`).
- The robot has no position sensor, so drift is expected and disclosed. Coins are hidden in live mode.

### 7.3 Missions (`Vpl/Models/Missions.cs`)

🏆 toolbar → overlay of mission cards. Stars persist in
`%LOCALAPPDATA%\CarthaBot\vpl_stars.json`. Missions are **pinned to the classic map** (their
coordinates are classic-specific).

| Mission | Goal |
|---|---|
| 💡 Glow on button | turn the LED green when a button is pressed |
| 🏁 Reach the flag | drive to the goal |
| 🛑 Stop before the wall | obstacle on the spawn straight; stop & hold 0.8 s; collision disqualifies |
| 💰 Coin hunt | collect 3 coins |
| 🌈 Rainbow party | reach goal + run a rainbow light-show |
| ➿ Follow the line | lap the track (visit 3 checkpoints + return near start, > 6 s) |

### 7.4 Maps (`Vpl/Models/MapCatalog.cs` + `SimWorld.Load`)

🗺 map-picker chip strip. Each map = floor + rim + follow-loop line + start pad + JSON manifest;
obstacle/coin/goal props are shared OBJs.

| Map | Floor | Notes |
|---|---|---|
| **classic** | teal mat + orange rim | rounded-rect loop; missions pinned here |
| **adventure** | fully **3D village** (trees, houses, barn, pond, mountains, 2 cars) | winding dirt road loop; coins on the road |
| **city** | grey asphalt | rounded-rect street loop, painted blocks |
| **garden** | grass | oval loop, flower-bloom ring |

> The adventure map was first an image-textured floor (`map_adventure.png`), then rebuilt as full
> 3D geometry. HelixToolkit.Wpf 3.1.2 **does** render textured OBJ via `map_Kd` — verified in-app.

### 7.5 Sim-world manifest schema

Manifests live in `Vpl/Assets/carthabot_simworld.json` (classic) and
`carthabot_map_{adventure,city,garden}.json`. Schema:

```jsonc
{
  "matHalf":        [13.0, 10.0],   // floor half-extents (X, Y)
  "trackHalfWidth": 0.45,           // line thickness (half)
  "track":          [[x,y], ...],   // closed polyline (~100 pts) of the follow line
  "start":   { "pos": [-3.5, -4.2], "deg": 0.0 },
  "obstacle":{ "pos": [2.5, 0.0], "half": [1.1, 0.5], "height": 1.2 },
  "goal":    { "pos": [5.0, 2.5], "radius": 1.3 },
  "coins":   [[2.0,-4.2], [6.5,0.0], [0.0,4.2]],
  "coinZ":   0.95,
  "coinRadius": 0.8,
  "robot": {                        // sensor geometry — MUST match the rendered model
    "halfWidth":  1.6,
    "halfLength": 1.6,
    "frontY":     1.6,
    "irRange":    2.6,
    "groundY":    1.45
  }
}
```

> **CRITICAL 3D gotcha:** the OBJ exporter setting `forward_axis='NEGATIVE_Y', up_axis='Z'` writes
> the scene **rotated 180° about Z**. `RobotSimulator` compensates with an `ExportFix()` 180° Z
> pre-rotation as the first child of every imported visual's transform. After the fix the robot
> front is +Y at yaw 0, matching the sim's `fwd=(-sin, cos)` convention.

---

## 8. Kids Coding method

The **"Kids Coding (under 7)"** tangible-block method (inspired by Thymio VPL) was the first
feature added (2026-06-10).

- Files: `Models/Classes/KidsBlock.cs`, `ViewModels/KidsCodingViewModel.cs`,
  `Views/KidsCodingView.xaml(.cs)`.
- Wired via `Module.KidsCoding` enum, `KidsCodingCloseEvent`, an Atelier card, `MainViewModel`
  routing (flashes `BootLoader_microPython.uf2` then opens the view), and EN/FR `kids*` strings.
- It generates MicroPython from icon blocks and streams it over the paste-mode REPL
  (verified live: "CarthaBot is ready"). It uses the same sign-magnitude motor helper as the VPL
  compiler (§6.3).

> **Menu state:** on 2026-06-11 the Kids Coding **card was removed** from the home grid and
> replaced by the Companion VPL ("Coding (under 6)"). The `Module.KidsCoding` view & routing still
> exist but are unreachable from the menu. ⚠️ The home grid (`Views/AtelierView.xaml`) is a
> `WrapPanel` of **hardcoded `Border` cards** that index into `AtelierViewModel.Atelier`
> (`Atelier[3]`=KidsCoding, `Atelier[4]`=VplJunior). Removing a `CarthaModule` from the VM shifts
> indices and mis-wires later cards — so the fix kept the `Atelier[3]` slot but deleted only the
> `<Border>`.

---

## 9. 3D-model pipeline (Blender → HelixToolkit)

### 9.1 Toolchain (installed 2026-06-11)

| Tool | Path / detail |
|---|---|
| **Blender 5.1.2** | `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe` (winget `BlenderFoundation.Blender`) |
| **uv** (Python runner) | winget `astral-sh.uv`; the uv-managed CPython at `%APPDATA%\uv\python\cpython-3.14-…\python.exe` (the Store `python.exe` is a stub) |
| **blender-mcp server** | `uv tool install blender-mcp` → `C:\Users\LENOVO\.local\bin\blender-mcp.exe`; registered as Claude MCP server **"blender"** (user scope) in `.claude.json` |
| **Blender add-on** | `blender_mcp_addon.py` in `%APPDATA%\Blender Foundation\Blender\5.1\scripts\addons\` (needs `requests` installed into Blender's modules dir) |
| **HelixToolkit.Wpf 3.1.2** | added to `CompanionApp.csproj` for WPF display |

**Run the MCP path:** double-click Desktop **"Start CarthaBot 3D (Blender MCP).bat"** → runs
`blender --python C:\Users\LENOVO\tools\blender_mcp_serve.py` → socket on **port 9876**.
Two hard requirements: **(1) restart Claude Code** (MCP servers load only at startup);
**(2) Blender must be running** so the server can bind 9876.

> ⚠️ **Interactive blender-mcp is unreliable on this machine** (socket flap between instances;
> **saving a `.blend` with a camera GUI-side hard-crashes** Blender via the AMD driver
> `atio6axx.dll` / EEVEE GPU). So author **headless**:
> `blender --background --factory-startup --python <script>` and render previews with
> **Cycles `device='CPU'`** (EEVEE crashes). Headless save skips the camera thumbnail → no crash.

### 9.2 Build scripts (`C:\Users\LENOVO\tools\carthabot_models\`)

| Script | Builds | Output |
|---|---|---|
| **`build_carthabot_real.py`** ⭐ | the **shipped robot** from FAB619's 4 real STLs (imports `wm.stl_import`, decimates, stacks cover/caster/button, adds modelled wheels, scales to 2.8 u, exports `forward=-Y/up=Z`) | `carthabot_robot.obj`(+`.mtl`) → tools dir **and** `CompanionApp\Vpl\Assets\` |
| `build_carthabot.py` | the old **stylized Thymio-style** robot v2 (superellipse body, IR windows, LED circle, etc.) | ⚠️ **do NOT run** — overwrites the shipped robot |
| `import_thymio_real.py` | imports `thymio-robot.zip` → `Thymio.fbx` | ⚠️ also overwrites the robot OBJ |
| `build_carthabot_exploded.py` | exploded/assembly view (separates part groups along axes) | `carthabot_exploded.obj` → Assembly Manual 3D toggle |
| `build_carthabot_simworld.py` | classic sim world (teal mat, rounded-rect loop, obstacle/coin/goal OBJs + manifest) | `carthabot_simworld.*` + `.json` |
| `build_carthabot_maps.py` | adventure / city / garden maps (3D village props, textured floors) | `carthabot_map_*.{obj,mtl,json}` + `map_adventure.png` |
| `build_carthabot_props.py` | play mat, track, obstacle, start pad, coins, hero scene | `carthabot_props.*`, `carthabot_scene_preview.png` |
| `inspect_carthabot_stl.py` / `inspect_thymio.py` | one-off geometry inspectors | — |

Helper launch scripts at `C:\Users\LENOVO\tools\`: `blender_mcp_serve.py`, `blender_enable_mcp.py`.

### 9.3 WPF display side

- `Controls/Model3DViewer.xaml(.cs)` — `HelixViewport3D` + `ModelImporter`, with `Source`
  (file-path), `AutoRotate` (bool), `RotateSeconds` DPs. Loads `.obj/.stl/.3ds/.ply` from **disk**,
  so assets are `<Content CopyToOutputDirectory=…>` in the csproj (NOT embedded resources).
- The VPL empty-state shows `<Model3DViewer AutoRotate RotateSeconds="14">` of `carthabot_robot.obj`
  in a gently-bobbing grid.
- `MiniBot3D` shares **one frozen** robot mesh across all action cards (lightweight `Viewport3D`,
  not HelixViewport3D).
- Assembly Manual (`Views/AssamblyView.xaml`) has a **2D ⟷ 3D** toggle swapping the WebView2 SVG
  for an interactive `Model3DViewer` of `carthabot_exploded.obj`.

> The **shipped robot** (`carthabot_robot.obj`) is built from the real CAD STLs by
> `build_carthabot_real.py`. The **exploded/assembly** view intentionally still uses the older
> stylized CarthaBot model.

---

## 10. The standalone CarthaBot VPL app

`C:\Users\LENOVO\Downloads\CarthaBotVPL` — fully independent (own `.sln`, plain WPF + `System.IO.Ports`,
no Prism/Syncfusion/DMSkin/Helix).

```
CarthaBotVPL/
├── CarthaBotVPL.sln / CarthaBotVPL.csproj      (TFM net6.0-windows10.0.19041.0 for BLE)
├── App.xaml(.cs)
├── Models/VplModel.cs
├── Services/
│   ├── ITransport.cs / SerialTransport.cs / WifiTransport.cs / BleTransport.cs
│   ├── RobotService.cs        flash + paste-mode REPL; transport-agnostic ConnectAsync(mode,param)
│   └── VplCompiler.cs         rules → MicroPython  (⚠ still old motor scheme + IR 26/27 guess)
├── ViewModels/{MainViewModel, Converters, Mvvm}.cs
├── Views/MainWindow.xaml(.cs)  Thymio event/action UI + "🔌 Connect ▾" popup
├── Themes/Styles.xaml
└── Firmware/esp32c3_bridge/esp32c3_bridge.ino + README.md
```

- Clones the Thymio event→action interface: Events column (orange) │ rule canvas │ Actions column
  (blue) + toolbar (New/Open/Save/Undo/Play/Stop/Code/Info).
- Programs save/load as `.cbvpl` JSON. A demo seed in `MainViewModel` is gated by env var
  `CBVPL_DEMO=1` (never shows for users).
- Published self-contained to `C:\Users\LENOVO\Desktop\CarthaBot VPL` (+ shortcut).
- USB/Wi-Fi/BLE transports + the ESP32-C3 bridge firmware were built here first, then propagated
  to the Junior Companion.

---

## 11. ESP32-C3 Wi-Fi/BLE bridge firmware

To give CarthaBot wireless, attach a **DFRobot Beetle ESP32-C3** (3.3 V, Wi-Fi + BLE) to **CN1**
used as **UART0**.

### 11.1 Wiring (both 3.3 V logic — no level shifter)

| CarthaBot CN1 | → | Beetle ESP32-C3 |
|---|---|---|
| 3V3 | → | 3V3 |
| GND | → | GND |
| SDA = **GP0 (UART0 TX)** | → | **GPIO4 (RX)** |
| SCL = **GP1 (UART0 RX)** | ← | **GPIO5 (TX)** |

- Cross-over TX↔RX. GPIO4/5 chosen = free non-strapping pins, keeps the ESP USB console.
- Add **470–1000 µF** across Beetle 3V3/GND for Wi-Fi current spikes (~350 mA), or power the Beetle
  from its own USB-C/LiPo (3-wire: GND + 2 signals).
- ⚠️ **Never** feed VBAT 4.2 V into 3V3 — ESP32-C3 max is 3.6 V.

### 11.2 Firmware (`Firmware/esp32c3_bridge/esp32c3_bridge.ino`)

- **NimBLE-Arduino** + **WiFi SoftAP** `"CarthaBot-WiFi"` / pass `"carthabot"`, TCP **:3333**.
- **BLE NUS** (Nordic UART Service), advertised name **`"CarthaBot"`**.
- **UART1** RX=GPIO4 / TX=GPIO5 @ 115200, transparent passthrough both directions. Status LED GPIO10.
- Verified: the paste-mode stream `03 05 <code>\n 04` arrives intact and in order over a local TCP
  mock (PASS). BLE compiles but needs the live peripheral to fully exercise.

Copies live in both `CarthaBotVPL\Firmware\` and `CarthaBotCompanion-Junior\Firmware\`.

---

## 12. Build / publish / run

All commands from the project root.

### Companion Junior (flagship)

```bash
# Build
dotnet build CompanionApp/CompanionApp.csproj -c Debug

# Publish (self-contained) to its own Desktop folder
dotnet publish CompanionApp/CompanionApp.csproj -c Release -r win-x64 \
  --self-contained true -o "C:\Users\LENOVO\Desktop\CarthaBot Companion Junior"
```

### Standalone CarthaBot VPL

```bash
dotnet publish CarthaBotVPL.csproj -c Release -r win-x64 \
  --self-contained true -o "C:\Users\LENOVO\Desktop\CarthaBot VPL"
```

### Gotchas

- **MSB3027 file-lock:** publishing fails if the published app is still running. Check
  `Get-Process CompanionApp` and gracefully close (`CloseMainWindow()`) first. A rename-swap of
  loaded DLLs does **not** work here.
- **Working directory:** the app reads `Resources\Settings.ini` via a **relative** path, so it must
  launch with its own folder as CWD (the Desktop shortcut sets `WorkingDirectory`) — otherwise the
  title shows "(0)" and a bogus update prompt.
- **Title bar = build marker:** the version from `Settings.ini` shows as
  `Companion Application (vX.Y.Z)` — use it to confirm the user is running the latest publish.
- **py_compile check:** validate generated MicroPython with the uv-managed CPython
  (`%APPDATA%\uv\python\cpython-3.14-…\python.exe`), not the Windows Store stub.

---

## 13. Version history (changelog)

> The bulk of the version churn was the **motor / sensor direction saga** — a long chain of
> hardware round-trips. Captured here so it never has to be re-derived.

| Version | Date | Change |
|---|---|---|
| — | 2026-06-10 | **Kids Coding** method added (main Companion); standalone **CarthaBot VPL** app created. |
| — | 2026-06-11 | Kids Coding card replaced by **Companion VPL** in the menu. 3D pipeline set up; robot/exploded/props/sim-world models built & shipped (Junior). VPL studio reworked to match Thymio; light/dark theme; full EN/FR localization. |
| — | 2026-06-12 | VPL major upgrade: advanced mode (timer + 4-shape memory), light-show, "My tune" pentatonic grid, UI sounds, event-driven simulator rewrite, **6 missions**, pen-trail / disco / chase-cam / confetti, hold-to-drive semantics, MiniBot3D rolling wheels. Robot v2 "Thymio-detail" rebuild, then **real Thymio FBX** shipped. |
| — | 2026-06-13 | **Live digital twin** (📡). Fixed Learn/CarthaSoft `ERR_FILE_NOT_FOUND` (bundled the 428-file web app). |
| v1.0.6→v1.0.7 | 2026-06-15 | Reverted a wrong Left/Right inversion; right wheel coil swap; **version put in title bar** as a build marker. |
| v1.0.8 | 2026-06-15 | **Global reverse** — final coil map: left fwd=GP23/rev=GP29, right fwd=GP24/rev=GP28. |
| v1.0.9 | 2026-06-15 | Motor directions **confirmed correct on hardware**; live-twin omega sign fix; coins hidden in live mode. |
| v1.1.0 | 2026-06-15 | Live twin: detailed differential-drive kinematics + on-screen read-out HUD. |
| v1.1.1 | 2026-06-15 | Front IR sensor treated active-low (interim). |
| v1.1.2 | 2026-06-15 | Ground/line IR treated active-low (interim). |
| v1.1.3 | 2026-06-15 | Added raw sensor diagnostic to telemetry/HUD. |
| **v1.1.4** | 2026-06-15 | **Found the authoritative pin map** — sensors were on the wrong pins (GP26/27 guess). Rewrote sensor section: IR fl/fc/fr = GP9/10/11, line gl/gr = GP15/16, **active-HIGH**. Motors already correct (forward = GP23+GP24). |
| v1.1.5 | 2026-06-15 | Shipped robot rebuilt from FAB619's **real CAD STLs**. |
| **v2.0** | 2026-06-18 | **USB/Wi-Fi/BLE connection chooser** before the flash wizard; transports + ESP32-C3 bridge firmware; TFM bumped to `net6.0-windows10.0.19041.0` for WinRT BLE. |

**Net result — the confirmed truth (use forever):**
- Motors: left fwd=GP23/rev=GP29, right fwd=GP24/rev=GP28, sign-magnitude PWM, forward=GP23+GP24.
- IR: obstacle = GP9/10/11, line = GP15/16, **all active-HIGH** (`1` = detected).

---

## 14. Known TODOs & caveats

- [ ] **Propagate the confirmed motor map + authoritative IR pins to the standalone CarthaBot VPL**
      compiler (`CarthaBotVPL\Services\VplCompiler.cs` still has the old `m1_dir/m2_dir` 2-pin scheme
      and the IR `26/27` guess).
- [ ] **Wire the other Junior modules' wireless transports** — only `Module.VplJunior` is fully
      wireless; KidsCoding / Maze / AdvancedProgramming still open USB internally even when the user
      picks Wi-Fi/BLE.
- [ ] **Copy the v2.0 chooser + transports into the main `CarthabotCompanion-main`** copy.
- [ ] **Live hardware test** of the Wi-Fi and BLE links with the wired Beetle ESP32-C3
      (Wi-Fi unit-tested vs a TCP mock; BLE compiles but unexercised).
- [ ] **Public GitHub download repo** (`thefhiter/carthabot-companion-junior`) — local commit
      exists, **not pushed**. ⚠️ Confirm with the user first (LICENSE forbids redistribution).
- ⚠️ **Do NOT run** `build_carthabot.py` or `import_thymio_real.py` — they overwrite the shipped
      `carthabot_robot.obj`. `build_carthabot_real.py` is the source of the shipped robot.
- ⚠️ WPF has no `PlaneProjection` / `Projection` (Silverlight/UWP only) — use `ScaleTransform` /
      `TranslateTransform`. HelixToolkit.Wpf 3.1.2 has no `MeshBuilder` in the `HelixToolkit.Wpf`
      namespace — build meshes as plain `MeshGeometry3D`.

---

## 15. File & asset reference tables

### 15.1 Key source files (Companion Junior)

| File | Purpose |
|---|---|
| `CompanionApp/Vpl/Services/VplCompiler.cs` | rules → MicroPython (the hardware contract) |
| `CompanionApp/Vpl/Services/VplRuntime.cs` | autonomous in-app interpreter |
| `CompanionApp/Vpl/Services/LiveTwin.cs` | dead-reckoning live digital twin |
| `CompanionApp/Vpl/Services/SimWorld.cs` | manifest loader + sensor queries |
| `CompanionApp/Vpl/Services/{ITransport,SerialTransport,WifiTransport,BleTransport}.cs` | USB/Wi-Fi/BLE transports |
| `CompanionApp/Vpl/Services/{UiSounds,LedAnimMath}.cs` | PC sounds + LED maths |
| `CompanionApp/Vpl/Models/{VplModel,Missions,MapCatalog}.cs` | data model + missions + maps |
| `CompanionApp/Vpl/ViewModels/VplViewModel.cs` | studio view-model |
| `CompanionApp/Vpl/Views/VplView.xaml(.cs)` | studio UI |
| `CompanionApp/Controls/{Model3DViewer,MiniBot3D,RobotSimulator}.xaml(.cs)` | 3D display controls |
| `CompanionApp/ViewModels/KidsCodingViewModel.cs` + `Views/KidsCodingView.xaml` | Kids Coding |
| `CompanionApp/Views/MainView.xaml` | home grid + flash wizard + connection chooser |
| `CompanionApp/Views/AssamblyView.xaml(.cs)` | Assembly Manual (2D/3D toggle) |
| `CompanionApp/Resources/Settings.ini` | `Version = v2.0` |
| `CompanionApp/Resources/StringResources*.xaml` | EN/FR strings |

### 15.2 3D assets (`CompanionApp/Vpl/Assets/`)

| Asset | What | Built by |
|---|---|---|
| `carthabot_robot.obj/.mtl` | **shipped robot** (from real CAD STLs) | `build_carthabot_real.py` |
| `carthabot_exploded.obj/.mtl` | assembly exploded view | `build_carthabot_exploded.py` |
| `carthabot_simworld.obj/.mtl/.json` | classic sim world + manifest | `build_carthabot_simworld.py` |
| `carthabot_map_{adventure,city,garden}.obj/.mtl/.json` | extra maps | `build_carthabot_maps.py` |
| `carthabot_{obstacle,coin,goalflag}.obj/.mtl` | shared sim props | `build_carthabot_simworld.py` |
| `carthabot_props.obj/.mtl` | hero scene props | `build_carthabot_props.py` |
| `map_adventure.png` | adventure floor texture (legacy; unused now) | `build_carthabot_maps.py` |
| `robot.png`, `logo.png`, `CarthaLogo.png` | 2D images | — |

### 15.3 Source / reference (`Carthabot-dev`)

| Item | Path |
|---|---|
| Top schematic PDF | `Schematic Carthabot V0 PDF.pdf` |
| Altium HW project | `Hardware Design\CARTHBOT V2.0.1\CarthaBOT.PrjPCB` (+ `IR-sensors`, `LEDs`, `microcontroleur` `.SchDoc`) |
| Firmware pin defs | `Software\FW\FW V0\lib\IRSensors\IRSensors.h`, `…\MotorControl.*` |
| MicroPython exercises | `Exercice CarthaBot\…\MicroPython\{Suiveur_de_ligne.py, Eviteur_d'obstacle.py}` |
| Real CAD STLs | `CAD Design\Carthabot 3D printed version 29_10_2025\00_STL\*.STL` |
| Robot photo | `Software\CarthaSoft\WebApplication\ui\devinfo\media\CathaBot.jpg` |
| CarthaSoft web app | `Software\CarthaSoft\WebApplication\ui\` (bundled into `CompanionApp\CarthaSoft\`) |

### 15.4 3D toolchain (`C:\Users\LENOVO\tools\`)

| Item | Path |
|---|---|
| Build scripts | `carthabot_models\build_carthabot*.py`, `import_thymio_real.py`, `inspect_*.py` |
| Preview renders | `carthabot_models\*.png` (e.g. `real_iso.png`, `carthabot_simworld_preview.png`) |
| MCP launch helpers | `blender_mcp_serve.py`, `blender_enable_mcp.py`, add-on clone `blender-mcp\` |
| Blender | `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe` |
| blender-mcp server | `C:\Users\LENOVO\.local\bin\blender-mcp.exe` (MCP server "blender", port 9876) |

---

*End of CarthaBot project documentation.*
