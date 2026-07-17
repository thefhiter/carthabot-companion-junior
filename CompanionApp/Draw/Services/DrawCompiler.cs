using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;

namespace CompanionApp.Draw.Services
{
    /// <summary>
    /// Tunable knobs for turning a 2-D drawing into CarthaBot motion. The defaults are a sane
    /// starting point; the two calibration values (<see cref="MmPerSec"/> / <see cref="DegPerSec"/>)
    /// should be dialled in once on the real robot — the Draw screen exposes them as sliders.
    /// </summary>
    public class DrawSettings
    {
        /// <summary>Motor duty (0..255) while the bot rolls forward and draws a line.</summary>
        public int DrawSpeed = 150;
        /// <summary>Motor duty (0..255) while the bot spins in place to change heading.
        /// 150 = the turn power the VPL studio has always used successfully on this robot.</summary>
        public int TurnSpeed = 150;
        /// <summary>How far the bot actually rolls per second at <see cref="DrawSpeed"/> (mm/s). Calibrate on hardware.</summary>
        public double MmPerSec = 90;
        /// <summary>Wheel diameter in mm (user-measured on the real robot). Documents the drive
        /// geometry: one full wheel revolution lays down π·64 ≈ 201 mm of line.</summary>
        public double WheelDiamMm = 64;
        /// <summary>PHYSICAL distance between the two wheels (mm) — measurable with a ruler.
        /// Used directly for smooth rolling arcs (no pivot, minimal wheel scrub).</summary>
        public double TrackMm = 95;
        /// <summary>How far the pen hole sits IN FRONT of the wheel axle (mm, user-measured).
        /// A diff-drive can only rotate about its axle line, so a pivot sweeps the pen along a
        /// 15 mm arc — the planner tracks the true pen position and aims every move so the pen
        /// still lands exactly on each corner (the sweep becomes a small corner rounding).</summary>
        public double PenOffsetMm = 15;
        /// <summary>Pivot friction factor: spinning in place scrubs the wheels sideways, so pivots
        /// are slower than pure geometry predicts. Tuned by the "turned too far / not enough" taps.</summary>
        public double PivotScrub = 1.35;
        /// <summary>Degrees spun per second in a pivot — DERIVED: turns and draws use the same motor
        /// duty, so the wheel surface speed is MmPerSec; a 360° spin walks each wheel π·Track·Scrub.</summary>
        public double DegPerSec => 360.0 * MmPerSec / (Math.PI * TrackMm * PivotScrub);
        /// <summary>Longest side of the finished drawing on paper, in millimetres.</summary>
        public double SizeMm = 200;
        /// <summary>Douglas–Peucker tolerance (mm): bigger = fewer, straighter segments (smoother freehand).</summary>
        public double SimplifyMm = 4;
        /// <summary>How many times to re-trace the line for a darker result (only applied to CLOSED shapes).</summary>
        public int Passes = 1;
        /// <summary>Direction the robot is placed facing, as a math heading (90 = up/＋Y, 0 = right, 180 = left, -90 = down).</summary>
        public double StartHeadingDeg = 90;
    }

    /// <summary>One robot instruction: spin <see cref="TurnDeg"/>° (＋ = left/CCW) then roll <see cref="ForwardMm"/> mm.</summary>
    public readonly struct DrawMove
    {
        public readonly double TurnDeg;
        public readonly double ForwardMm;
        public DrawMove(double turnDeg, double forwardMm) { TurnDeg = turnDeg; ForwardMm = forwardMm; }
    }

    /// <summary>The result of compiling a drawing: the MicroPython program plus a few stats for the UI.</summary>
    public class DrawProgram
    {
        public string Code = "";
        public int MoveCount;
        public double TotalDrawMm;
        public double EstimatedSeconds;
    }

    /// <summary>
    /// Compiles a drawing (freehand ink strokes, or a chosen shape) into a MicroPython program that
    /// drives a <b>fixed-pen</b> CarthaBot like a turtle plotter: the pen sits in the robot's hole and
    /// always touches the paper, so the whole drawing is traced as ONE continuous line — the bot turns
    /// in place, then rolls forward, segment after segment.
    ///
    /// Motor wiring / <c>motors()</c> are copied verbatim from <c>VplCompiler</c> (verified on this
    /// robot): both wheels roll forward together, a left turn = <c>motors(-s, s)</c>.
    /// </summary>
    public static class DrawCompiler
    {
        // The robot is placed facing the TOP of the paper, so heading starts at 90° (＋Y, "up").
        private const double StartHeadingDeg = 90.0;
        // Pause after each move so momentum fully dies before the next move starts. Long enough
        // that every move begins from a dead stop — that consistency is what makes turns repeatable.
        private const int SettleMs = 120;
        // The robot keeps moving a little after the motors cut (momentum). Because draw/turn powers
        // are fixed, that coast is a near-constant and can be subtracted from the timed move.
        // NEVER "brake" by driving both coils of a motor high — on this board it stalls/browns out
        // the robot entirely (verified 2026-07-02: turns stopped happening at all).
        // Pivot timing fitted to THREE hardware tests 2026-07-02 (corner angles measured on the
        // paper, robot turn = 180° − drawn angle):
        //   timed 85° → spun ~120° | timed 115° → spun ~150° | timed 55° → spun ~60°
        // Least-squares line through all three:  spun = 1.5 × timed − 17.5°.
        // So the robot really pivots ~1.5× FASTER than the geometric DPS predicts (rate gain),
        // and loses a fixed ~17.5° spinning up from standstill (start-up loss). Inverting the
        // line gives the timing that lands exactly on the wanted angle:
        //   timed = (wanted + SPINUP) / (DPS × RATE_GAIN)
        private const double TurnSpinupDeg = 17.5;
        private const double TurnRateGain = 1.5;
        private const double RollCoastMm = 2.0;
        // Full-power (255) blip at the start of every move: breaks static friction so even the
        // shortest segments (circle arcs) actually move instead of humming in place.
        private const int KickMs = 30;
        // Bends up to this angle (with enough forward travel) are driven as smooth rolling arcs
        // instead of stop-and-pivot — pivots hook the pen sideways at every vertex.
        private const double ArcMaxDeg = 40.0;

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Compile ink <paramref name="strokes"/> (each a polyline in canvas pixels, Y pointing DOWN).
        /// All strokes are joined in drawing order into a single pen-down path.
        /// </summary>
        public static DrawProgram Generate(IReadOnlyList<IReadOnlyList<Point>> strokes, DrawSettings s)
        {
            var paper = ToPaperPath(strokes, s);
            return Generate(paper, s);
        }

        /// <summary>Compile a path already expressed in paper millimetres (X right, Y up).</summary>
        public static DrawProgram Generate(IReadOnlyList<Point> paperPath, DrawSettings s)
        {
            // multi-pass re-tracing only makes sense for a CLOSED path (the bot ends where it began);
            // for an open freehand line the pen can't return, so we force a single pass.
            bool closed = paperPath != null && paperPath.Count > 2 &&
                          (paperPath[0] - paperPath[paperPath.Count - 1]).Length < Math.Max(2.0, s.SizeMm * 0.05);
            var moves = PathToMoves(paperPath, s.StartHeadingDeg, s.PenOffsetMm);
            return Emit(moves, s, closed);
        }

        /// <summary>Compile a ready-made list of moves (used by the calibration test buttons).</summary>
        public static DrawProgram GenerateMoves(IEnumerable<DrawMove> moves, DrawSettings s)
            => Emit(moves.ToList(), s, false);

        /// <summary>
        /// The exact polyline the robot will trace, but kept in <b>canvas pixels</b> so the UI can
        /// overlay it on the original ink. Same flatten + Douglas–Peucker as the real compile —
        /// simplifying in canvas space with a scaled epsilon yields the identical shape.
        /// </summary>
        public static List<Point> SimplifiedCanvasPath(IReadOnlyList<IReadOnlyList<Point>> strokes, DrawSettings s)
        {
            var all = FlattenOrdered(strokes);
            if (all.Count < 2) return new List<Point>();

            double minX = all.Min(p => p.X), maxX = all.Max(p => p.X);
            double minY = all.Min(p => p.Y), maxY = all.Max(p => p.Y);
            double maxSide = Math.Max(1e-6, Math.Max(maxX - minX, maxY - minY));
            // epsilon_paper / scale, where scale = SizeMm / maxSide
            double epsPx = Math.Max(0.1, s.SimplifyMm) * maxSide / Math.Max(1e-6, s.SizeMm);
            return DouglasPeucker(all, epsPx);
        }

        /// <summary>Turn ink strokes into a single, centred, scaled, simplified paper path (mm, Y up).</summary>
        public static List<Point> ToPaperPath(IReadOnlyList<IReadOnlyList<Point>> strokes, DrawSettings s)
        {
            var all = FlattenOrdered(strokes);
            if (all.Count < 2) return new List<Point>();

            double minX = all.Min(p => p.X), maxX = all.Max(p => p.X);
            double minY = all.Min(p => p.Y), maxY = all.Max(p => p.Y);
            double w = Math.Max(1e-6, maxX - minX), h = Math.Max(1e-6, maxY - minY);
            double scale = s.SizeMm / Math.Max(w, h);
            double cx = (minX + maxX) / 2.0, cy = (minY + maxY) / 2.0;

            // centre on the bounding box and flip Y (canvas Y grows downward, paper Y grows up)
            var paper = all.Select(p => new Point((p.X - cx) * scale, -(p.Y - cy) * scale)).ToList();
            var simplified = DouglasPeucker(paper, Math.Max(0.1, s.SimplifyMm));
            return simplified;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>The shapes offered on the Draw screen. Each is a single closed pen-down path.</summary>
        public enum Shape { Square, Triangle, Circle, Star, Heart, Zigzag, Spiral, House, Arrow }

        /// <summary>
        /// A shape as a paper path (mm, Y up), already sized to <see cref="DrawSettings.SizeMm"/>.
        /// Returned straight to <see cref="Generate(IReadOnlyList{Point}, DrawSettings)"/>.
        /// </summary>
        public static List<Point> ShapePath(Shape shape, DrawSettings s)
        {
            double r = s.SizeMm / 2.0;
            var pts = new List<Point>();
            switch (shape)
            {
                case Shape.Square:
                    pts.Add(new Point(-r, -r)); pts.Add(new Point(r, -r));
                    pts.Add(new Point(r, r));   pts.Add(new Point(-r, r));
                    pts.Add(new Point(-r, -r));
                    break;

                case Shape.Triangle:
                    for (int i = 0; i <= 3; i++)
                    {
                        double a = Math.PI / 2 + i * 2 * Math.PI / 3;   // point up
                        pts.Add(new Point(r * Math.Cos(a), r * Math.Sin(a)));
                    }
                    break;

                case Shape.Circle:
                    for (int i = 0; i <= 36; i++)
                    {
                        double a = i * 2 * Math.PI / 36;
                        pts.Add(new Point(r * Math.Cos(a), r * Math.Sin(a)));
                    }
                    break;

                case Shape.Star:
                    for (int i = 0; i <= 10; i++)
                    {
                        double rad = (i % 2 == 0) ? r : r * 0.42;
                        double a = Math.PI / 2 + i * Math.PI / 5;       // first tip up
                        pts.Add(new Point(rad * Math.Cos(a), rad * Math.Sin(a)));
                    }
                    break;

                case Shape.Heart:
                    for (int i = 0; i <= 48; i++)
                    {
                        double t = i * 2 * Math.PI / 48;
                        double x = 16 * Math.Pow(Math.Sin(t), 3);
                        double y = 13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t);
                        pts.Add(new Point(x / 16.0 * r, y / 16.0 * r));
                    }
                    break;

                case Shape.Zigzag:
                    {
                        int teeth = 4;
                        double step = s.SizeMm / teeth;
                        for (int i = 0; i <= teeth; i++)
                            pts.Add(new Point(-r + i * step, (i % 2 == 0) ? -r / 2 : r / 2));
                        break;
                    }

                case Shape.Spiral:
                    for (int i = 0; i <= 180; i++)
                    {
                        double t = i / 180.0;
                        double a = t * 5 * 2 * Math.PI;                 // 5 turns
                        double rad = r * t;
                        pts.Add(new Point(rad * Math.Cos(a), rad * Math.Sin(a)));
                    }
                    break;

                case Shape.House:   // single closed outline: walls + roof
                    pts.Add(new Point(-r, -r));
                    pts.Add(new Point(r, -r));
                    pts.Add(new Point(r, 0.2 * r));
                    pts.Add(new Point(0, r));
                    pts.Add(new Point(-r, 0.2 * r));
                    pts.Add(new Point(-r, -r));
                    break;

                case Shape.Arrow:   // single closed outline pointing right
                    pts.Add(new Point(-r, -0.3 * r));
                    pts.Add(new Point(0.2 * r, -0.3 * r));
                    pts.Add(new Point(0.2 * r, -0.6 * r));
                    pts.Add(new Point(r, 0));
                    pts.Add(new Point(0.2 * r, 0.6 * r));
                    pts.Add(new Point(0.2 * r, 0.3 * r));
                    pts.Add(new Point(-r, 0.3 * r));
                    pts.Add(new Point(-r, -0.3 * r));
                    break;
            }

            // Keep every shape inside a SizeMm × SizeMm box so it always fits the paper. Only shapes
            // whose own parametric form overshoots (e.g. the heart) get scaled down; the rest are
            // already within the envelope and pass through unchanged.
            double half = s.SizeMm / 2.0;
            double ext = pts.Count == 0 ? 0 : pts.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
            if (ext > half + 1e-6)
            {
                double k = half / ext;
                for (int i = 0; i < pts.Count; i++) pts[i] = new Point(pts[i].X * k, pts[i].Y * k);
            }
            return pts;
        }

        // ------------------------------------------------------------------ path -> moves

        /// <summary>Walk the path and emit (turn-to-heading, roll-forward) for each segment.</summary>
        public static List<DrawMove> PathToMoves(IReadOnlyList<Point> path) => PathToMoves(path, StartHeadingDeg);

        /// <summary>As above, but starting from a given robot heading (degrees, 0 = ＋X, CCW＋).</summary>
        public static List<DrawMove> PathToMoves(IReadOnlyList<Point> path, double startHeadingDeg)
            => PathToMoves(path, startHeadingDeg, 0);

        /// <summary>
        /// As above, compensating for a pen mounted <paramref name="penOffsetMm"/> mm IN FRONT of
        /// the wheel axle. The robot can only pivot about its axle, so each pivot sweeps the pen
        /// along an arc of that radius — uncompensated, every corner displaces the next line
        /// sideways by up to penOffset·√2. This planner tracks the TRUE pen position instead:
        ///   pivot:  pen' = pen + d·(h_new − h_old)      (d = offset, h = heading unit vector)
        ///   drive:  pen'' = pen' + L·h_new
        /// Landing the pen exactly on vertex V means solving pen'' = V, which is closed-form:
        ///   w = V − pen + d·h_old   →   h_new = w/|w| ,  L = |w| − d.
        /// With d = 0 this reduces exactly to the plain per-segment walk.
        /// </summary>
        public static List<DrawMove> PathToMoves(IReadOnlyList<Point> path, double startHeadingDeg, double penOffsetMm)
        {
            var moves = new List<DrawMove>();
            if (path == null || path.Count < 2) return moves;

            double d = Math.Max(0, penOffsetMm);
            double heading = startHeadingDeg;                            // degrees, math convention
            double px = path[0].X, py = path[0].Y;                       // the PEN starts on the first point

            for (int i = 1; i < path.Count; i++)
            {
                double vx = path[i].X - px, vy = path[i].Y - py;
                double segLen = Math.Sqrt(vx * vx + vy * vy);
                if (segLen < 0.5) continue;                              // skip sub-millimetre jitter

                double segDeg = Math.Atan2(vy, vx) * 180.0 / Math.PI;    // math heading: 0 = ＋X, CCW＋
                double turnPlain = NormalizeDeg(segDeg - heading);

                // Gentle bends run as rolling arcs (see Emit) which already follow the ideal
                // line — only stop-and-pivot corners sweep the pen and need the compensation.
                bool pivots = d > 0 && (Math.Abs(turnPlain) > ArcMaxDeg || segLen < 10);
                if (!pivots)
                {
                    moves.Add(new DrawMove(turnPlain, segLen));
                    heading = segDeg;
                }
                else
                {
                    double wx = vx + d * Math.Cos(heading * Math.PI / 180.0);
                    double wy = vy + d * Math.Sin(heading * Math.PI / 180.0);
                    double target = Math.Atan2(wy, wx) * 180.0 / Math.PI;
                    double len = Math.Sqrt(wx * wx + wy * wy) - d;       // the pivot already advanced the pen by d·(h_new−h_old)
                    moves.Add(new DrawMove(NormalizeDeg(target - heading), Math.Max(0, len)));
                    heading = target;
                }

                px = path[i].X; py = path[i].Y;                          // pen lands on the vertex
            }
            return moves;
        }

        // ------------------------------------------------------------------ moves -> MicroPython

        private static DrawProgram Emit(List<DrawMove> moves, DrawSettings s, bool closed)
        {
            int passes = (closed && s.Passes > 1) ? Math.Min(5, s.Passes) : 1;
            var sb = new StringBuilder();
            double totalMm = 0, totalSec = 0.5;   // 0.5 s settle before the first move

            sb.AppendLine("# === CarthaBot Draw — generated plotter program ===");
            sb.AppendLine("# Fixed pen in the robot's hole: it always touches the paper, so the whole");
            sb.AppendLine("# drawing is ONE continuous line. Place the bot facing the top of the page.");
            sb.AppendLine("import machine, time");
            sb.AppendLine();
            sb.AppendLine("SPEED_FREQ = 1000");
            sb.AppendLine("# Motors — same wiring as the VPL studio (verified on this robot):");
            sb.AppendLine("# left fwd=GP23 / rev=GP29 ; right fwd=GP24 / rev=GP28.");
            sb.AppendLine("m1_fwd = machine.PWM(machine.Pin(23)); m1_fwd.freq(SPEED_FREQ)");
            sb.AppendLine("m1_rev = machine.PWM(machine.Pin(29)); m1_rev.freq(SPEED_FREQ)");
            sb.AppendLine("m2_fwd = machine.PWM(machine.Pin(24)); m2_fwd.freq(SPEED_FREQ)");
            sb.AppendLine("m2_rev = machine.PWM(machine.Pin(28)); m2_rev.freq(SPEED_FREQ)");
            sb.AppendLine("try:");
            sb.AppendLine("    import neopixel");
            sb.AppendLine("    np = neopixel.NeoPixel(machine.Pin(21), 11)");
            sb.AppendLine("except Exception:");
            sb.AppendLine("    np = None");
            sb.AppendLine();
            sb.AppendLine("def ring(r, g, b):");
            sb.AppendLine("    if np is None: return");
            sb.AppendLine("    for i in range(11): np[i] = (r, g, b)");
            sb.AppendLine("    np.write()");
            sb.AppendLine();
            sb.AppendLine("def _duty(v):");
            sb.AppendLine("    if v < 0: v = 0");
            sb.AppendLine("    if v > 255: v = 255");
            sb.AppendLine("    return int(v * 65535 // 255)");
            sb.AppendLine();
            sb.AppendLine("def motors(left, right):");
            sb.AppendLine("    if left >= 0:");
            sb.AppendLine("        m1_fwd.duty_u16(_duty(left)); m1_rev.duty_u16(0)");
            sb.AppendLine("    else:");
            sb.AppendLine("        m1_fwd.duty_u16(0); m1_rev.duty_u16(_duty(-left))");
            sb.AppendLine("    if right >= 0:");
            sb.AppendLine("        m2_fwd.duty_u16(_duty(right)); m2_rev.duty_u16(0)");
            sb.AppendLine("    else:");
            sb.AppendLine("        m2_fwd.duty_u16(0); m2_rev.duty_u16(_duty(-right))");
            sb.AppendLine();
            sb.AppendLine("# --- calibration (tune these on real paper) ---");
            sb.AppendLine($"DRAW = {s.DrawSpeed}      # forward motor power while drawing");
            sb.AppendLine($"TURN = {s.TurnSpeed}      # motor power while spinning in place");
            sb.AppendLine($"MMPS = {F(s.MmPerSec)}    # millimetres rolled per second at DRAW");
            sb.AppendLine($"WHEEL_MM = {F(s.WheelDiamMm)}   # wheel diameter: one wheel turn = {F(Math.Round(Math.PI * s.WheelDiamMm, 1))} mm of line");
            sb.AppendLine($"TRACK_MM = {F(s.TrackMm)}   # physical distance between the two wheels");
            sb.AppendLine($"DEAD = 60          # PWM duty below which a loaded wheel barely moves");
            sb.AppendLine("# Pivoting in place, each wheel walks a circle of diameter TRACK_MM (times a");
            sb.AppendLine("# scrub-friction factor), and turns use the same power as draws, so:");
            sb.AppendLine($"# DPS = 360 * MMPS / (pi * TRACK_MM * {F(s.PivotScrub)})");
            sb.AppendLine($"DPS  = {F(Math.Round(s.DegPerSec, 2))}   # degrees spun per second in a pivot (derived)");
            sb.AppendLine($"SETTLE = {SettleMs}      # pause (ms) after each move: momentum dies, next move starts from standstill");
            sb.AppendLine($"SPINUP_DEG = {F(TurnSpinupDeg)}   # rotation lost spinning up from standstill (hardware-fitted)");
            sb.AppendLine($"RATE_GAIN = {F(TurnRateGain)}    # real pivot rate / geometric DPS (hardware-fitted: spun = 1.5*timed - 17.5)");
            sb.AppendLine($"COAST_MM  = {F(RollCoastMm)}    # millimetres it keeps rolling after the motors cut");
            sb.AppendLine($"KICK_MS = {KickMs}        # full-power blip that breaks static friction at the start of every move");
            sb.AppendLine();
            sb.AppendLine("def _kick(v):");
            sb.AppendLine("    # full power in the direction we're about to go (0 stays 0)");
            sb.AppendLine("    if v > 0: return 255");
            sb.AppendLine("    if v < 0: return -255");
            sb.AppendLine("    return 0");
            sb.AppendLine();
            sb.AppendLine("def _go(l, r, t):");
            sb.AppendLine("    # every move starts with a short full-power kick so the wheels actually");
            sb.AppendLine("    # start turning (short moves used to just hum: static friction + the pen");
            sb.AppendLine("    # dragging beat the cruise power from a standstill), then cruise.");
            sb.AppendLine("    motors(_kick(l), _kick(r)); time.sleep_ms(KICK_MS)");
            sb.AppendLine("    if t > KICK_MS:");
            sb.AppendLine("        motors(l, r); time.sleep_ms(t - KICK_MS)");
            sb.AppendLine("    motors(0, 0); time.sleep_ms(SETTLE)");
            sb.AppendLine();
            sb.AppendLine("def fwd(mm):");
            sb.AppendLine("    if mm <= 0: return");
            sb.AppendLine("    d = mm - COAST_MM              # the coast finishes the distance for us");
            sb.AppendLine("    if d < 1: d = 1");
            sb.AppendLine("    _go(DRAW, DRAW, int(d / MMPS * 1000))");
            sb.AppendLine();
            sb.AppendLine("def turn(deg):");
            sb.AppendLine("    if deg > 180: deg -= 360");
            sb.AppendLine("    if deg < -180: deg += 360");
            sb.AppendLine("    if abs(deg) < 0.5: return");
            sb.AppendLine("    a = abs(deg) + SPINUP_DEG      # pay the start-up loss up front");
            sb.AppendLine("    t = int(a / (DPS * RATE_GAIN) * 1000)");
            sb.AppendLine("    if t < KICK_MS: t = KICK_MS");
            sb.AppendLine("    if deg >= 0:");
            sb.AppendLine("        _go(-TURN, TURN, t)   # left / counter-clockwise");
            sb.AppendLine("    else:");
            sb.AppendLine("        _go(TURN, -TURN, t)   # right / clockwise");
            sb.AppendLine();
            sb.AppendLine("def arc(deg, mm):");
            sb.AppendLine("    # gentle bend: roll and turn AT THE SAME TIME (inner wheel slower) ->");
            sb.AppendLine("    # one smooth continuous line, no stop-and-pivot, no pen hooks at corners");
            sb.AppendLine("    rad = abs(deg) * 0.0174533");
            sb.AppendLine("    R = mm / rad                   # curve radius at the pen");
            sb.AppendLine("    ro = R + TRACK_MM / 2.0        # outer wheel radius");
            sb.AppendLine("    ri = R - TRACK_MM / 2.0        # inner wheel radius");
            sb.AppendLine("    ratio = ri / ro");
            sb.AppendLine("    if ratio < 0: ratio = 0");
            sb.AppendLine("    inner = int(DEAD + (DRAW - DEAD) * ratio)");
            sb.AppendLine("    t = int(ro * rad / MMPS * 1000)   # outer wheel runs at DRAW = MMPS");
            sb.AppendLine("    if deg >= 0:");
            sb.AppendLine("        _go(inner, DRAW, t)   # bending left: left wheel is inner");
            sb.AppendLine("    else:");
            sb.AppendLine("        _go(DRAW, inner, t)");
            sb.AppendLine();
            // build the move lines once; per-move time/length is summed for the estimate.
            // Gentle bends become smooth arcs (roll + turn together, no pivot); only sharp
            // corners — and bends too tight for the inner wheel to keep rolling — pivot.
            var moveLines = new List<string>();
            foreach (var m in moves)
            {
                double turnAbs = Math.Abs(m.TurnDeg);
                bool gentle = turnAbs >= 0.5 && turnAbs <= ArcMaxDeg && m.ForwardMm >= 10;
                if (gentle)
                {
                    double penR = m.ForwardMm / (turnAbs * Math.PI / 180.0);
                    gentle = penR - s.TrackMm / 2.0 >= 15;   // inner wheel keeps rolling, no stall
                }

                if (gentle)
                {
                    moveLines.Add($"arc({F(m.TurnDeg)}, {F(m.ForwardMm)})");
                    totalMm += m.ForwardMm;
                    totalSec += m.ForwardMm / Math.Max(1, s.MmPerSec) + SettleMs / 1000.0;
                    continue;
                }

                if (turnAbs >= 0.5)
                {
                    moveLines.Add($"turn({F(m.TurnDeg)})");
                    totalSec += (turnAbs + TurnSpinupDeg) / Math.Max(1, s.DegPerSec * TurnRateGain) + SettleMs / 1000.0;
                }
                if (m.ForwardMm > 0)
                {
                    moveLines.Add($"fwd({F(m.ForwardMm)})");
                    totalMm += m.ForwardMm;
                    totalSec += m.ForwardMm / Math.Max(1, s.MmPerSec) + SettleMs / 1000.0;
                }
            }

            sb.AppendLine("# --- the drawing ---");
            sb.AppendLine("motors(0, 0); ring(0, 40, 0)   # green = drawing");
            sb.AppendLine("time.sleep_ms(500)");
            sb.AppendLine();

            if (passes > 1 && moveLines.Count > 0)
            {
                sb.AppendLine($"for _pass in range({passes}):   # re-trace for a darker line");
                foreach (var line in moveLines) sb.AppendLine("    " + line);
                // extra passes add their own draw time/length to the estimate
                totalSec += (passes - 1) * (totalSec - 0.5);
                totalMm *= passes;
            }
            else
            {
                foreach (var line in moveLines) sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine("motors(0, 0); ring(0, 0, 0)    # done");
            sb.AppendLine("print('DRAW_DONE')");

            return new DrawProgram
            {
                Code = sb.ToString(),
                MoveCount = moves.Count,
                TotalDrawMm = totalMm,
                EstimatedSeconds = totalSec
            };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Join all strokes into one point list. The first stroke keeps its drawing order; after that
        /// we greedily chain the nearest remaining endpoint (reversing a stroke when its tail is closer),
        /// so the unavoidable pen-down "travel" between strokes is as short as possible.
        /// </summary>
        public static List<Point> FlattenOrdered(IReadOnlyList<IReadOnlyList<Point>> strokes)
        {
            var pending = new List<List<Point>>();
            if (strokes != null)
                foreach (var st in strokes)
                    if (st != null && st.Count >= 2)
                        pending.Add(st.ToList());

            var result = new List<Point>();
            if (pending.Count == 0) return result;

            var first = pending[0]; pending.RemoveAt(0);
            result.AddRange(first);
            Point cur = result[result.Count - 1];

            while (pending.Count > 0)
            {
                int best = 0; bool reverse = false; double bestD = double.MaxValue;
                for (int i = 0; i < pending.Count; i++)
                {
                    double dHead = Dist2(cur, pending[i][0]);
                    double dTail = Dist2(cur, pending[i][pending[i].Count - 1]);
                    if (dHead < bestD) { bestD = dHead; best = i; reverse = false; }
                    if (dTail < bestD) { bestD = dTail; best = i; reverse = true; }
                }
                var next = pending[best]; pending.RemoveAt(best);
                if (reverse) next.Reverse();
                result.AddRange(next);
                cur = result[result.Count - 1];
            }
            return result;
        }

        private static double Dist2(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static double NormalizeDeg(double d)
        {
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return d;
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Ramer–Douglas–Peucker line simplification (keeps the shape, drops jitter).</summary>
        public static List<Point> DouglasPeucker(IReadOnlyList<Point> pts, double epsilon)
        {
            if (pts == null || pts.Count < 3) return pts?.ToList() ?? new List<Point>();

            double dmax = 0; int index = 0;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                double d = PerpendicularDistance(pts[i], pts[0], pts[pts.Count - 1]);
                if (d > dmax) { index = i; dmax = d; }
            }

            if (dmax > epsilon)
            {
                var left = DouglasPeucker(pts.Take(index + 1).ToList(), epsilon);
                var right = DouglasPeucker(pts.Skip(index).ToList(), epsilon);
                left.RemoveAt(left.Count - 1);   // drop the shared joint
                left.AddRange(right);
                return left;
            }
            return new List<Point> { pts[0], pts[pts.Count - 1] };
        }

        private static double PerpendicularDistance(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double mag = Math.Sqrt(dx * dx + dy * dy);
            if (mag < 1e-9) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double u = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (mag * mag);
            double projX = a.X + u * dx, projY = a.Y + u * dy;
            return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
        }
    }
}
