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
- **3D CarthaBot model** used throughout — in the action cards, the simulator, and the
  Assembly Manual's interactive exploded view.

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
