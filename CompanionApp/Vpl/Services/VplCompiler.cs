using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CarthaBotVPL.Models;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// Compiles a list of VPL rules into a MicroPython program for the CarthaBot (RP2040).
    ///
    /// Pin map is taken from the official CarthaBot firmware (communs.h / MotorControl.cpp):
    ///   Motor 1 (left)  : direction = GP23, PWM = GP29
    ///   Motor 2 (right) : direction = GP24, PWM = GP28   (note: opposite polarity to motor 1)
    ///   NeoPixel ring   : GP21 (11 LEDs)
    ///   Speaker         : GP20
    ///   Buttons         : Forward=GP7, Down=GP17, Left=GP18, Right=GP19, Center=GP22 (active-high)
    ///   IR sensors      : not defined in the public firmware → exposed as editable constants.
    ///
    /// Semantics mirror Thymio VPL: actuator outputs are *latched* (a Move/Color keeps its value
    /// until another rule changes it), and rules are evaluated every loop ("when event → set outputs").
    /// ONE exception, for small fingers: movement started by a BUTTON rule is "while held" —
    /// releasing the button stops the motors (sensor / timer / start rules stay fully latched,
    /// because those are autonomous behaviours). Advanced-mode features follow Thymio too: a Timer
    /// action starts a one-shot timer whose elapse raises the Timer event for one loop turn, and a
    /// 4-value state (★ ♥ ● ■) can gate any event. The timer / state / animation / tune subsystems
    /// are only emitted when the program uses them, so the code stays small and readable.
    /// </summary>
    public static class VplCompiler
    {
        /// <summary>Pitches of the 6-slot tune editor, bottom (0) to top (4): C5 D5 E5 G5 A5 (pentatonic).</summary>
        public static readonly int[] PentatonicHz = { 523, 587, 659, 784, 880 };

        /// <summary>Duration of one tune slot in milliseconds.</summary>
        public const int NoteMs = 190;

        public static string Generate(IEnumerable<VplRule> rules) => Generate(rules, false);

        /// <summary>
        /// Compile the rules. When <paramref name="telemetry"/> is true the program also prints a
        /// compact state line ~10×/s ("T,l,r,front,ground,r,g,b,anim") so the app's digital twin
        /// can mirror the real robot live. The kid-facing code overlay calls without telemetry.
        /// </summary>
        public static string Generate(IEnumerable<VplRule> rules, bool telemetry)
        {
            var list = rules.ToList();
            bool usesTimer = list.Any(r => r.Event.Kind == EventKind.Timer ||
                                           r.Actions.Any(a => a.Kind == ActionKind.Timer));
            bool usesState = list.Any(r => r.Event.StateFilter >= 0 ||
                                           r.Actions.Any(a => a.Kind == ActionKind.State));
            bool usesAnim = list.Any(r => r.Actions.Any(a => a.Kind == ActionKind.Anim));
            bool usesTune = list.Any(r => r.Actions.Any(a => a.Kind == ActionKind.Sound && a.Sound == SoundKind.Custom));
            bool usesPreset = list.Any(r => r.Actions.Any(a => a.Kind == ActionKind.Sound && a.Sound != SoundKind.Custom));
            // button-driven movement is "while held": stop the motors when the button is released
            bool usesHoldDrive = list.Any(r => r.Event.Kind == EventKind.Button &&
                                               r.Actions.Any(a => a.Kind == ActionKind.Move));
            // "follow the line" drive mode (latched, steered every pass by the ground sensor)
            bool usesFollow = list.Any(r => r.Actions.Any(
                a => a.Kind == ActionKind.Move && a.Move == MoveDir.FollowLine));

            var sb = new StringBuilder();

            sb.AppendLine("# === CarthaBot VPL — generated program ===");
            sb.AppendLine("import machine, neopixel, time");
            sb.AppendLine();
            sb.AppendLine("SPEED_FREQ = 1000");
            sb.AppendLine("# Motors (from CarthaBot firmware: M1=left dir23/pwm29, M2=right dir24/pwm28)");
            sb.AppendLine("m1_dir = machine.Pin(23, machine.Pin.OUT)");
            sb.AppendLine("m2_dir = machine.Pin(24, machine.Pin.OUT)");
            sb.AppendLine("m1_pwm = machine.PWM(machine.Pin(29)); m1_pwm.freq(SPEED_FREQ)");
            sb.AppendLine("m2_pwm = machine.PWM(machine.Pin(28)); m2_pwm.freq(SPEED_FREQ)");
            sb.AppendLine("np = neopixel.NeoPixel(machine.Pin(21), 11)");
            sb.AppendLine("spk = machine.PWM(machine.Pin(20)); spk.duty_u16(0)");
            sb.AppendLine();
            sb.AppendLine("# Buttons (active-high)");
            sb.AppendLine("btn_up = machine.Pin(7, machine.Pin.IN, machine.Pin.PULL_DOWN)");
            sb.AppendLine("btn_down = machine.Pin(17, machine.Pin.IN, machine.Pin.PULL_DOWN)");
            sb.AppendLine("btn_left = machine.Pin(18, machine.Pin.IN, machine.Pin.PULL_DOWN)");
            sb.AppendLine("btn_right = machine.Pin(19, machine.Pin.IN, machine.Pin.PULL_DOWN)");
            sb.AppendLine("btn_center = machine.Pin(22, machine.Pin.IN, machine.Pin.PULL_DOWN)");
            sb.AppendLine();
            sb.AppendLine("# IR sensors — set these to match your CarthaBot wiring");
            sb.AppendLine("IR_FRONT_PIN = 26");
            sb.AppendLine("IR_GROUND_PIN = 27");
            sb.AppendLine("ir_front = machine.Pin(IR_FRONT_PIN, machine.Pin.IN)");
            sb.AppendLine("ir_ground = machine.Pin(IR_GROUND_PIN, machine.Pin.IN)");
            sb.AppendLine();
            sb.AppendLine("def _duty(v):");
            sb.AppendLine("    if v < 0: v = 0");
            sb.AppendLine("    if v > 255: v = 255");
            sb.AppendLine("    return int(v * 65535 // 255)");
            sb.AppendLine();
            sb.AppendLine("def motors(left, right):");
            sb.AppendLine("    # +ve = forward; firmware uses opposite direction polarity per motor");
            if (telemetry)
            {
                sb.AppendLine("    global _ml, _mr");
                sb.AppendLine("    _ml = left; _mr = right");
            }
            sb.AppendLine("    m1_dir.value(0 if left >= 0 else 1); m1_pwm.duty_u16(_duty(abs(left)))");
            sb.AppendLine("    m2_dir.value(1 if right >= 0 else 0); m2_pwm.duty_u16(_duty(abs(right)))");
            sb.AppendLine();

            if (usesAnim)
            {
                // A static colour cancels any running ring animation, like on Thymio
                // where the latest actuator command wins.
                sb.AppendLine("def set_color(r, g, b):");
                sb.AppendLine(telemetry ? "    global anim, _cr, _cg, _cb, _aid" : "    global anim");
                sb.AppendLine("    anim = None");
                if (telemetry) sb.AppendLine("    _cr = r; _cg = g; _cb = b; _aid = -1");
                sb.AppendLine("    for i in range(11): np[i] = (r, g, b)");
                sb.AppendLine("    np.write()");
            }
            else
            {
                sb.AppendLine("def set_color(r, g, b):");
                if (telemetry)
                {
                    sb.AppendLine("    global _cr, _cg, _cb, _aid");
                    sb.AppendLine("    _cr = r; _cg = g; _cb = b; _aid = -1");
                }
                sb.AppendLine("    for i in range(11): np[i] = (r, g, b)");
                sb.AppendLine("    np.write()");
            }
            sb.AppendLine();
            sb.AppendLine("def tone(freq, ms):");
            sb.AppendLine("    if freq > 0:");
            sb.AppendLine("        spk.freq(freq); spk.duty_u16(30000)");
            sb.AppendLine("    time.sleep_ms(ms); spk.duty_u16(0)");
            sb.AppendLine();

            if (usesPreset)
            {
                sb.AppendLine("def play(name):");
                sb.AppendLine("    if name == 'happy':");
                sb.AppendLine("        for f in (523, 659, 784): tone(f, 120)");
                sb.AppendLine("    elif name == 'sad':");
                sb.AppendLine("        for f in (392, 330, 262): tone(f, 160)");
                sb.AppendLine("    elif name == 'siren':");
                sb.AppendLine("        for f in (440, 880, 440, 880): tone(f, 120)");
                sb.AppendLine("    else:");
                sb.AppendLine("        tone(880, 150)");
                sb.AppendLine();
            }

            if (usesTune)
            {
                sb.AppendLine("def play_tune(notes):");
                sb.AppendLine("    for f, ms in notes:");
                sb.AppendLine("        if f > 0:");
                sb.AppendLine("            spk.freq(f); spk.duty_u16(30000)");
                sb.AppendLine("        else:");
                sb.AppendLine("            spk.duty_u16(0)");
                sb.AppendLine("        time.sleep_ms(ms)");
                sb.AppendLine("    spk.duty_u16(0)");
                sb.AppendLine();
            }

            if (usesState)
            {
                sb.AppendLine("state = 0   # 0..3 = the four memory shapes: star, heart, circle, square");
                sb.AppendLine();
            }

            if (usesTimer)
            {
                sb.AppendLine("timer_deadline = None");
                sb.AppendLine("timer_fired = False");
                sb.AppendLine();
            }

            if (usesHoldDrive)
            {
                sb.AppendLine("btn_drive_prev = False   # was a button driving the motors last pass?");
                sb.AppendLine();
            }

            if (usesFollow)
            {
                sb.AppendLine("follow = False           # \"follow the line\" drive mode");
                sb.AppendLine("follow_speed = 150");
                sb.AppendLine();
            }

            if (telemetry)
            {
                sb.AppendLine("# --- live telemetry for the app's digital twin ---");
                sb.AppendLine("_ml = 0; _mr = 0          # last motor commands");
                sb.AppendLine("_cr = 0; _cg = 0; _cb = 0 # last LED colour");
                sb.AppendLine("_aid = -1                 # running animation id (-1 = none)");
                sb.AppendLine("_tlast = 0");
                sb.AppendLine();
            }

            if (usesAnim)
            {
                sb.AppendLine("# --- LED ring animations (non-blocking, ticked every loop) ---");
                sb.AppendLine("anim = None");
                sb.AppendLine("anim_t0 = 0");
                sb.AppendLine();
                sb.AppendLine("def set_anim(kind, r=0, g=0, b=0):");
                sb.AppendLine(telemetry ? "    global anim, anim_t0, _cr, _cg, _cb, _aid" : "    global anim, anim_t0");
                sb.AppendLine("    anim = (kind, r, g, b); anim_t0 = time.ticks_ms()");
                if (telemetry)
                    sb.AppendLine("    _cr = r; _cg = g; _cb = b; _aid = ('rainbow', 'blink', 'chase', 'breathe').index(kind)");
                sb.AppendLine();
                sb.AppendLine("def _wheel(p):");
                sb.AppendLine("    p = int(p) % 255");
                sb.AppendLine("    if p < 85: return (255 - p * 3, p * 3, 0)");
                sb.AppendLine("    if p < 170:");
                sb.AppendLine("        p -= 85; return (0, 255 - p * 3, p * 3)");
                sb.AppendLine("    p -= 170; return (p * 3, 0, 255 - p * 3)");
                sb.AppendLine();
                sb.AppendLine("def anim_tick():");
                sb.AppendLine("    if anim is None: return");
                sb.AppendLine("    t = time.ticks_diff(time.ticks_ms(), anim_t0)");
                sb.AppendLine("    k, r, g, b = anim");
                sb.AppendLine("    if k == 'rainbow':");
                sb.AppendLine("        for i in range(11): np[i] = _wheel(t // 12 + i * 23)");
                sb.AppendLine("    elif k == 'blink':");
                sb.AppendLine("        on = (t // 350) % 2 == 0");
                sb.AppendLine("        for i in range(11): np[i] = (r, g, b) if on else (0, 0, 0)");
                sb.AppendLine("    elif k == 'chase':");
                sb.AppendLine("        h = (t // 80) % 11");
                sb.AppendLine("        for i in range(11):");
                sb.AppendLine("            np[i] = (r, g, b) if i == h else (r // 8, g // 8, b // 8)");
                sb.AppendLine("    else:   # breathe");
                sb.AppendLine("        ph = t % 2400");
                sb.AppendLine("        lv = ph / 1200 if ph < 1200 else (2400 - ph) / 1200");
                sb.AppendLine("        for i in range(11): np[i] = (int(r * lv), int(g * lv), int(b * lv))");
                sb.AppendLine("    np.write()");
                sb.AppendLine();
            }

            // Run-once (Start) actions before the main loop.
            var startRules = list.Where(r => r.Event.Kind == EventKind.Start).ToList();
            sb.AppendLine("# --- startup ---");
            sb.AppendLine("motors(0, 0); set_color(0, 0, 0)");
            foreach (var r in startRules)
                foreach (var a in r.Actions)
                    sb.AppendLine(ActionLine(a, "", usesFollow));   // module level → no indent
            sb.AppendLine();

            // Reactive rules in the loop.
            var loopRules = list.Where(r => r.Event.Kind != EventKind.Start).ToList();
            sb.AppendLine("# --- main loop ---");
            sb.AppendLine("while True:");
            bool emptyLoop = loopRules.Count == 0 && !usesAnim && !usesTimer && !telemetry;
            if (emptyLoop)
            {
                sb.AppendLine("    time.sleep_ms(20)");
            }
            else
            {
                if (usesTimer)
                {
                    sb.AppendLine("    timer_fired = False");
                    sb.AppendLine("    if timer_deadline is not None and time.ticks_diff(time.ticks_ms(), timer_deadline) >= 0:");
                    sb.AppendLine("        timer_fired = True; timer_deadline = None");
                }
                if (usesHoldDrive)
                    sb.AppendLine("    btn_drive = False");
                foreach (var r in loopRules)
                {
                    sb.AppendLine($"    if {Condition(r.Event, usesState)}:");
                    if (r.Actions.Count == 0)
                    {
                        sb.AppendLine("        pass");
                    }
                    else
                    {
                        foreach (var a in r.Actions)
                            sb.AppendLine(ActionLine(a, "        ", usesFollow));
                        if (usesHoldDrive && r.Event.Kind == EventKind.Button &&
                            r.Actions.Any(a => a.Kind == ActionKind.Move))
                            sb.AppendLine("        btn_drive = True");
                    }
                }
                if (usesHoldDrive)
                {
                    sb.AppendLine("    # button-driven movement is \"while held\": stop on release");
                    sb.AppendLine("    if btn_drive_prev and not btn_drive:");
                    sb.AppendLine(usesFollow ? "        motors(0, 0); follow = False" : "        motors(0, 0)");
                    sb.AppendLine("    btn_drive_prev = btn_drive");
                }
                if (usesFollow)
                {
                    sb.AppendLine("    # follow-the-line mode: steer with the ground sensor every pass");
                    sb.AppendLine("    if follow:");
                    sb.AppendLine("        if ir_ground.value():");
                    sb.AppendLine("            motors(follow_speed, follow_speed)");
                    sb.AppendLine("        else:");
                    sb.AppendLine("            motors(follow_speed, -follow_speed)");
                }
                if (usesAnim) sb.AppendLine("    anim_tick()");
                if (telemetry)
                {
                    sb.AppendLine("    # stream live state to the app's digital twin (~10 Hz)");
                    sb.AppendLine("    if time.ticks_diff(time.ticks_ms(), _tlast) >= 100:");
                    sb.AppendLine("        _tlast = time.ticks_ms()");
                    sb.AppendLine("        print('T,%d,%d,%d,%d,%d,%d,%d,%d' % (_ml, _mr, " +
                                  "1 if ir_front.value() else 0, 1 if ir_ground.value() else 0, _cr, _cg, _cb, _aid))");
                }
                sb.AppendLine("    time.sleep_ms(20)");
            }

            return sb.ToString();
        }

        private static string Condition(VplEvent e, bool usesState)
        {
            string cond;
            switch (e.Kind)
            {
                case EventKind.Button:
                    string pin = e.Button switch
                    {
                        ButtonDir.Up => "btn_up",
                        ButtonDir.Down => "btn_down",
                        ButtonDir.Left => "btn_left",
                        ButtonDir.Right => "btn_right",
                        _ => "btn_center"
                    };
                    cond = $"{pin}.value()";
                    break;
                case EventKind.Obstacle:
                    cond = e.Detected ? "ir_front.value()" : "not ir_front.value()";
                    break;
                case EventKind.Line:
                    cond = e.Detected ? "ir_ground.value()" : "not ir_ground.value()";
                    break;
                case EventKind.Timer:
                    cond = "timer_fired";
                    break;
                default:
                    cond = "True";
                    break;
            }

            if (usesState && e.StateFilter >= 0)
                cond += $" and state == {e.StateFilter}";
            return cond;
        }

        private static string ActionLine(VplAction a, string indent, bool usesFollow = false)
        {
            switch (a.Kind)
            {
                case ActionKind.Move:
                    int s = a.Speed;
                    if (a.Move == MoveDir.FollowLine)
                        return indent + $"follow = True; follow_speed = {s}";
                    string call = a.Move switch
                    {
                        MoveDir.Forward => $"motors({s}, {s})",
                        MoveDir.Backward => $"motors({-s}, {-s})",
                        // Left = left wheel back + right wheel forward, matching the CarthaBot
                        // firmware's TurnFullLeft (M1 dir=HIGH, M2 dir=HIGH); Right is the mirror.
                        MoveDir.Left => $"motors({-s}, {s})",
                        MoveDir.Right => $"motors({s}, {-s})",
                        _ => "motors(0, 0)"
                    };
                    // a direct drive command takes over from follow-the-line mode
                    return indent + (usesFollow ? $"follow = False; {call}" : call);
                case ActionKind.Color:
                    return indent + $"set_color({a.R}, {a.G}, {a.B})";
                case ActionKind.Sound:
                    if (a.Sound == SoundKind.Custom)
                        return indent + $"play_tune([{TuneLiteral(a)}])";
                    string name = a.Sound switch
                    {
                        SoundKind.Happy => "happy",
                        SoundKind.Sad => "sad",
                        SoundKind.Siren => "siren",
                        _ => "beep"
                    };
                    return indent + $"play('{name}')";
                case ActionKind.Wait:
                    return indent + $"time.sleep({Sec(a.Seconds)})";
                case ActionKind.Anim:
                    string kind = a.Anim switch
                    {
                        LedAnim.Blink => "blink",
                        LedAnim.Chase => "chase",
                        LedAnim.Breathe => "breathe",
                        _ => "rainbow"
                    };
                    return indent + $"set_anim('{kind}', {a.R}, {a.G}, {a.B})";
                case ActionKind.Timer:
                    int ms = (int)(a.Seconds * 1000);
                    return indent + $"timer_deadline = time.ticks_add(time.ticks_ms(), {ms})";
                case ActionKind.State:
                    return indent + $"state = {a.StateValue}";
                default:
                    return indent + "pass";
            }
        }

        /// <summary>The child's 6-slot tune as "(freq, ms)" pairs; trailing rests are trimmed.</summary>
        private static string TuneLiteral(VplAction a)
        {
            var slots = a.Notes.Select(n => n.Pitch).ToList();
            while (slots.Count > 0 && slots[slots.Count - 1] < 0) slots.RemoveAt(slots.Count - 1);
            if (slots.Count == 0) return $"(523, {NoteMs})";
            return string.Join(", ", slots.Select(p =>
                p < 0 ? $"(0, {NoteMs})" : $"({PentatonicHz[p]}, {NoteMs})"));
        }

        private static string Sec(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
