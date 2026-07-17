using System;
using System.Windows.Media;
using CarthaBotVPL.Models;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// The digital twin of the REAL CarthaBot. While a program runs on the robot, the
    /// generated MicroPython prints a telemetry line ~10×/s over the open serial port:
    ///   T,l,r,front,ground,red,green,blue,anim
    /// The twin latches those actuator/sensor values and dead-reckons its pose with the
    /// same differential-drive constants as the simulator, so the 3D robot moves the way
    /// the real one is actually moving (no position sensor exists, so drift is expected).
    /// </summary>
    public class LiveTwin
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double YawDeg { get; private set; }

        private double _l, _r;                  // latched real motor values (-255..255)
        public bool FrontDetected { get; private set; }
        public bool OnLine { get; private set; }
        public bool IsDriving => Math.Abs(_l) > 1 || Math.Abs(_r) > 1;

        // --- differential-drive constants ---
        // Wheel command 0..255 → linear wheel speed (sim units/s). 150 → 2.0 u/s.
        private const double UnitsPerSecPerCmd = 1.0 / 75.0;
        private readonly double _track;         // wheel separation (sim units)

        // --- detailed kinematics, exposed for the live HUD ---
        public int LeftCmd => (int)Math.Round(_l);
        public int RightCmd => (int)Math.Round(_r);
        public double LeftSpeed => _l * UnitsPerSecPerCmd;    // units/s
        public double RightSpeed => _r * UnitsPerSecPerCmd;   // units/s
        public double LinearSpeed { get; private set; }       // units/s (robot centre)
        public double TurnRateDeg { get; private set; }       // deg/s (+ = left, − = right)
        public double TurnRadius { get; private set; }        // units (NaN = straight, 0 = spin)
        public double DistanceTraveled { get; private set; }  // total path length (units)
        public double TotalTurnedDeg { get; private set; }    // cumulative |rotation| (deg)

        // raw left/right line-sensor levels straight off the robot (-1 = not reported)
        public int RawLineLeft { get; private set; } = -1;
        public int RawLineRight { get; private set; } = -1;

        private Color _ledBase = Colors.Black;
        private int _anim = -1;
        private double _animT;

        public LiveTwin(SimWorld world)
        {
            X = world.StartPos.X;
            Y = world.StartPos.Y;
            YawDeg = world.StartDeg - 90;       // same convention as the simulator
            // wheels sit at the sides of the body → separation ≈ full body width
            _track = Math.Max(0.5, world.RobotHalfWidth * 2.0);
        }

        /// <summary>Apply one telemetry packet: l, r, front, ground, R, G, B, anim.</summary>
        public void Update(int[] v)
        {
            if (v == null || v.Length < 8) return;
            _l = v[0]; _r = v[1];
            FrontDetected = v[2] != 0;
            OnLine = v[3] != 0;
            var c = Color.FromRgb((byte)Clamp(v[4]), (byte)Clamp(v[5]), (byte)Clamp(v[6]));
            if (v[7] != _anim || (v[7] < 0 && c != _ledBase)) _animT = 0;
            _ledBase = c;
            _anim = v[7];
            if (v.Length >= 10) { RawLineLeft = v[8]; RawLineRight = v[9]; }
        }

        private static int Clamp(int v) => Math.Max(0, Math.Min(255, v));

        /// <summary>Integrate the pose between packets (same constants as VplRuntime).</summary>
        public void Tick(double dt)
        {
            if (dt <= 0) return;
            _animT += dt;

            // 1. each wheel's linear speed (units/s) from its real motor command
            double vL = _l * UnitsPerSecPerCmd;
            double vR = _r * UnitsPerSecPerCmd;

            // 2. differential-drive body motion.
            //    v     = mean wheel speed  → forward speed of the robot centre
            //    omega = (right − left) / wheel separation → rotation rate.
            //    Standard sign so it matches the (hardware-verified) firmware where a
            //    left turn = motors(-s, s): vL<vR → omega>0 → CCW → left.
            double v = (vL + vR) / 2.0;
            double omega = (vR - vL) / _track;          // rad/s, +ve = CCW = left
            double omegaDeg = omega * 180.0 / Math.PI;

            LinearSpeed = v;
            TurnRateDeg = omegaDeg;
            TurnRadius = Math.Abs(omega) < 1e-9
                ? double.NaN                            // going straight
                : v / omega;                            // signed turn radius

            // 3. exact pose integration over dt (constant v, omega → circular arc),
            //    instead of a coarse Euler step, so curves don't drift.
            double th0 = YawDeg * Math.PI / 180.0;
            double th1 = th0 + omega * dt;
            if (Math.Abs(omega) < 1e-9)
            {
                X += -Math.Sin(th0) * v * dt;
                Y += Math.Cos(th0) * v * dt;
            }
            else
            {
                double rc = v / omega;                  // arc radius
                X += rc * (Math.Cos(th1) - Math.Cos(th0));
                Y += rc * (Math.Sin(th1) - Math.Sin(th0));
            }
            YawDeg = th1 * 180.0 / Math.PI;

            // 4. running totals for the live read-out
            DistanceTraveled += Math.Abs(v) * dt;
            TotalTurnedDeg += Math.Abs(omegaDeg) * dt;
        }

        public Color CurrentLedColor()
        {
            if (_anim < 0) return _ledBase;
            return LedAnimMath.Evaluate((LedAnim)_anim, _ledBase, _animT * 1000.0);
        }
    }
}
