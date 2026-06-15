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

        private Color _ledBase = Colors.Black;
        private int _anim = -1;
        private double _animT;

        public LiveTwin(SimWorld world)
        {
            X = world.StartPos.X;
            Y = world.StartPos.Y;
            YawDeg = world.StartDeg - 90;       // same convention as the simulator
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
        }

        private static int Clamp(int v) => Math.Max(0, Math.Min(255, v));

        /// <summary>Integrate the pose between packets (same constants as VplRuntime).</summary>
        public void Tick(double dt)
        {
            if (dt <= 0) return;
            _animT += dt;
            double v = (_l + _r) / 2.0 / 75.0;
            // +ve omega = CCW / left turn. The real robot's left turn is motors(-s, +s),
            // so omega must be (right - left) to mirror the actual hardware on screen.
            double omega = (_r - _l) / 3.0;
            YawDeg += omega * dt;
            double yawRad = YawDeg * Math.PI / 180.0;
            X += -Math.Sin(yawRad) * v * dt;
            Y += Math.Cos(yawRad) * v * dt;
        }

        public Color CurrentLedColor()
        {
            if (_anim < 0) return _ledBase;
            return LedAnimMath.Evaluate((LedAnim)_anim, _ledBase, _animT * 1000.0);
        }
    }
}
