# CarthaBot Companion — Junior Edition

**CarthaBot Companion (Junior Edition)** is a Windows application by **FAB619** for the
**CarthaBot** educational robot. On top of the standard Companion modules it adds a full
**visual block‑programming studio for young children ("Coding, under 6")** — a Thymio‑style
event → action editor with a live 3D simulator, guided missions, and a real‑time digital
twin of the physical robot.

🌐 Official website: [carthabot.vercel.app](https://carthabot.vercel.app/en)

## ⬇️ Download

Grab the ready‑to‑run build from the **[Releases](https://github.com/thefhiter/carthabot-companion-junior/releases)** page.

1. Download `CarthaBot-Companion-Junior.zip` from the latest release.
2. **Extract the whole folder** (keep all files together).
3. Run **`CompanionApp.exe`** from inside the extracted folder.
4. Connect your CarthaBot over USB and start exploring.

> The build is **self‑contained** — no .NET install is required. Always launch
> `CompanionApp.exe` from within its own folder (it reads `Resources\Settings.ini`
> by a relative path).

## 🚀 Features

- **Mode Modules** — flash preprogrammed behaviours to CarthaBot: Obstacle Avoider,
  Line Follower, Friendly, Obedient.
- **CarthaSoft** — a Scratch‑like drag‑and‑drop visual programming interface, with
  example PDFs to guide students.
- **Advanced Python Programming** — a Python notepad for advanced coding on the robot.
- **Coding (under 6) — VPL studio** *(Junior edition highlight)*:
  - Thymio‑style rules: orange **Events** (Buttons · Obstacle · Line · Start · Timer)
    paired with blue **Actions** (Move · LED colour · Sound · Wait · Light show · Memory).
  - **3D simulator** with a playground and guided **missions** (glow on button, reach the
    flag, stop before the wall, coin hunt, rainbow party, follow the line) and earnable stars.
  - **📡 Live digital twin** — drives an on‑screen 3D CarthaBot in real time from the real
    robot's telemetry while your program runs on the hardware.
  - **"My tune"** pentatonic music editor, **follow‑the‑line** drive mode, light/dark theme,
    and full **English / French** localisation.
- **Draw with a pen** *(turtle plotter)* — drop a pen into CarthaBot's hole, then **draw a
  picture, stamp a shape (square · triangle · circle · star · heart · zig-zag · spiral · house ·
  arrow), or type a word**, and the robot traces it on paper. Live path **preview**, a generated-code
  viewer, save/open drawings, multi-pass "trace over" for darker lines, and one-tap **calibration**
  tests (100 mm line / 90° turn) that persist between sessions. Works over USB or WiFi.
- **3D CarthaBot model** used throughout — in the action cards, the simulator, and the
  Assembly Manual's interactive exploded view.

## 🆕 What's new in v3.6

- **Update checker fixed** — the in‑app update button now checks *this* repository
  (derived from `Resources\Settings.ini` → `GithubUrl`, one source of truth) and only
  offers a release that is **strictly newer** than the installed version, so it can
  never propose a downgrade again.
- **UI text fixes** — "Rabot" → "CarthaBot" in the Behaviours card (English and French).
- First public source release: includes the **Draw with a pen** module (+ unit tests in
  `CompanionApp.DrawTests`), the **Wireless (WiFi) transport**, the **CarthaSoft** Blockly
  assets, the six VPL simulator **maps** (city, garden, ocean, snow, space, adventure),
  the **Clap detector** for the 👏 event, the **Fredoka** embedded font, and the RP2040
  **Firmware** sources.

## 🛠️ Building from source

Requirements: **.NET SDK 8.x** (the app targets `net6.0-windows10.0.19041.0`; the 8.x SDK
builds it fine — the EOL warnings are expected) on Windows 10/11 x64.

```powershell
dotnet publish CompanionApp\CompanionApp.csproj -c Release -r win-x64 --self-contained true
```

The ready‑to‑run output lands in
`CompanionApp\bin\Release\net6.0-windows10.0.19041.0\win-x64\publish\`.
Launch `CompanionApp.exe` from inside that folder (it reads `Resources\Settings.ini` by a
relative path). The solution also contains the module projects (`AdvancedProgramming`,
`BehaveProject`, `LearningProject`, `MazeProject`), the `CarthaBotTransport` USB/WiFi
transport library, and `CompanionApp.DrawTests` (xUnit tests for the Draw path planner).

## 🧠 Requirements

- Windows 10 or 11 (64‑bit)
- A CarthaBot robot connected over USB

## 🤖 Hardware / firmware notes

CarthaBot is an RP2040‑based robot. Generated programs are flashed by copying a `.uf2` to the
robot, then streamed as MicroPython over USB. Motor pin map (from the firmware
`MazeProject/MazeCode/lib`): M1 (left) dir = GP23 / PWM = GP29, M2 (right) dir = GP24 /
PWM = GP28; NeoPixel ring = GP21, speaker = GP20.

## 📄 License

This project is licensed under the **CarthaBot Companion License** — see the [LICENSE](LICENSE)
file. Use, modification, or distribution without written permission from **FAB619** is strictly
prohibited.

## 💬 Contact

Developed by **FAB619**. For questions or support, contact info@fab619.com or open an issue.
