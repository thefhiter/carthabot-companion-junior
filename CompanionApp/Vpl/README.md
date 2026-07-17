# CarthaBot VPL — "Coding (under 6)" module

A Thymio-VPL-inspired visual programming studio embedded in the Companion app
(`Module.VplJunior`). Children pair **events** (orange, left palette) with
**actions** (blue, right palette) to build rules, then run them on the real
robot over USB serial — or inside the built-in 3D playground simulator.

## Layout

```
Vpl/
  Models/
    VplModel.cs      events, actions, rules, the 6-slot tune (NoteSlot)
    Missions.cs      mission catalog + star persistence (%LOCALAPPDATA%\CarthaBot\vpl_stars.json)
  Services/
    VplCompiler.cs   rules -> MicroPython for the RP2040 (see "Semantics")
    VplRuntime.cs    the same semantics interpreted in C# for the simulator
    SimWorld.cs      playground geometry loaded from carthabot_simworld.json
    UiSounds.cs      in-memory WAV synth (preset sounds, tune preview, UI feedback)
    ClapDetector.cs  PC-microphone clap detection (winmm waveIn) for the 👏 event
  ViewModels/
    VplViewModel.cs  rules list, compile/run/stop, save/open (.cbvpl), undo stack,
                     advanced-mode toggle (Timer + Memory blocks)
  Views/
    VplView.xaml     palettes, rule rows, configurators, overlays (sim / missions / code)
  Assets/            Blender-authored 3D models + the sim-world manifest
Controls/ (shared)
  RobotSimulator     the 3D playground (event-driven virtual robot, D-pad, HUD,
                     pen trail, chase cam, missions, confetti)
  MiniBot3D          live 3D robot thumbnail inside action cards
  Model3DViewer      generic OBJ viewer (empty state, assembly manual)
```

## Block set

Events: **Buttons** (5), **Obstacle** (front IR, detected/clear), **Line**
(ground IR, detected/clear), **Clap** 👏 (the PC microphone hears a hand clap —
see below), **Start**, **Timer rings** (advanced).
Every event can carry a **state filter** in advanced mode (★ ♥ ● ■ or "always").

### The clap event 👏

The robot has **no microphone**, so the PC's mic stands in for it
(`ClapDetector.cs`, raw winmm waveIn — no NuGet dependency): while the 3D
simulator runs a program with a clap rule, a bottom-centre tip appears
("🎤 Clap your hands — CarthaBot is listening!") with an on-screen 👏 button as
the no-mic fallback. Detection is a sharp-transient test (absolute gate +
5× the ambient floor + 2.5× the recent average, 350 ms refractory), and the
detector is suppressed while the app plays its own sounds so open speakers
can't clap back at the robot. In the runtime a clap is a **one-shot** like the
timer: it stays pending through a blocking Wait and fires for exactly one rule
pass. The compiler keeps clap rules visible in the generated MicroPython but
emits them under `if False:` with a comment — honest about the missing sensor.

Actions: **Move** (5 directions + speed), **LED Colour**, **Sound** (4 presets
+ "My tune": 6 pentatonic slots C5 D5 E5 G5 A5), **Wait**, **Light show**
(rainbow / blink / chase / breathe on the 11-LED ring), **Start timer**
(advanced), **Memory** = set state (advanced).

## Semantics (firmware AND simulator — they must stay identical)

* Rules are **level-triggered**, evaluated every loop pass (~20 ms on the robot).
* Actuators are **latched**: a Move/Colour/Animation persists until another rule
  changes it. ONE exception for small fingers: **movement started by a Button
  rule is "while held"** — releasing the button stops the motors (sensor, timer
  and start rules stay fully latched, since those are autonomous behaviours).
* `Wait` and sounds **block the rule loop but not the motors** (firmware uses
  `time.sleep`; the runtime models it with `_blockedFor`).
* The **timer** is a one-shot: the Timer action arms it, the Timer event is true
  for exactly one pass when it elapses.
* **state** is a single value 0..3 (★ ♥ ● ■), initially 0.
* A static colour **cancels** a running LED animation (last actuator wins).
* The compiler only emits the timer / state / animation / tune subsystems when
  the program uses them, so generated code stays small and readable.

Robot pin map (from the official firmware): motors dir GP23/GP24 + PWM
GP29/GP28, NeoPixel GP21 (11), speaker GP20, buttons GP7/17/18/19/22,
IR front GP26 / ground GP27 (editable constants in the generated code).

## The simulator contract

`carthabot_simworld.json` is written by the **same Blender script**
(`tools/carthabot_models/build_carthabot_simworld.py`) that exports the world
mesh, so the virtual sensors "see" exactly what is rendered: the line loop is a
sampled polyline (point-to-segment distance ≤ trackHalfWidth), the obstacle is
a ray-vs-AABB test, coins/goal are radius checks. The robot spawns on the start
pad; physics is differential drive (~2 units/s at speed 150).

**Axis gotcha:** Blender's OBJ export (`forward=-Y, up=Z`) writes the scene
rotated 180° about Z. Every imported visual gets a fixed 180° pre-rotation
(`ExportFix()` in RobotSimulator, same idea in MiniBot3D) — after it, the
robot's front is +Y at yaw 0.

## Missions

Six guided challenges with persisted stars: glow-on-button 💡, reach-the-flag 🏁,
stop-before-the-wall 🛑 (0.8 s stillness, bumping disqualifies), coin hunt 💰,
rainbow party 🌈, follow-the-line ➿ (visit the loop's three quarter-checkpoints
in either direction, then return to the start pad). The reference solution for
the line mission is the classic two-rule follower: *on the line → forward,
off the line → turn*.

## Localization

All UI strings live in `Resources/StringResources.xaml` (+ `-FR.xaml`) under
`vpl*` keys, referenced via `DynamicResource`; view-model strings go through
`L(key, fallback)` so the studio follows the app's language toggle live.

## Theme

One **sunny kid theme** only (warm cream canvas, milky panels, white cards,
Comic Sans labels) — the light/dark toggle was retired in v3.5 to keep the
studio bright and friendly, in the spirit of kids' apps like *Ferid Around the
World*. The brushes are the `Vpl*` DynamicResources declared at the top of
`VplView.xaml`; the code overlay keeps its dark Consolas console look on
purpose, and the simulator's 🌙 disco mode is gameplay, not a theme.

## 3D asset pipeline

Models are authored headlessly in Blender (`tools/carthabot_models/build_*.py`,
run with `blender --background --factory-startup --python <script>`; renders
must use Cycles CPU on this hardware). The robot (`build_carthabot.py`) follows
the Thymio II reference: superellipse shell, 5+2 IR windows with indicator
LEDs, 2 ground sensors, the 8-LED circle around the buttons, speaker grille,
mic, battery gauge, temperature pair, IR receiver, micro-USB, microSD, trailer
hook, pen hole, recessed wheels — plus CarthaBot's stripe, eyes, antenna,
screen and NeoPixel ring. Re-running a script regenerates the OBJ straight
into `Vpl/Assets/` (all registered as `<Content>` in the csproj).
