using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CarthaBotVPL.Models;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// The virtual CarthaBot: runs the child's VPL rules with the SAME semantics as the
    /// MicroPython that VplCompiler streams to the real robot —
    ///   * rules are level-triggered and evaluated every loop pass,
    ///   * actuator outputs (motors, LEDs, animations) are latched until changed,
    ///   * Wait and Sound block the rule loop, but the motors keep running (time.sleep!),
    ///   * a Timer action arms a one-shot whose elapse raises the Timer event for one pass,
    ///   * the 4-shape state (★ ♥ ● ■) gates events through their state filter.
    /// Driving is a differential-drive integration over the SimWorld manifest geometry,
    /// so the virtual IR sensors "see" exactly what the 3D scene shows.
    /// </summary>
    public class VplRuntime
    {
        private readonly SimWorld _world;
        private readonly List<VplRule> _rules;
        private readonly Queue<VplAction> _queue = new Queue<VplAction>();

        // pose
        public double X { get; private set; }
        public double Y { get; private set; }
        /// <summary>Yaw in degrees; 0 = model front (+Y), counter-clockwise positive.</summary>
        public double YawDeg { get; private set; }

        // latched outputs
        private double _l, _r;                       // -255..255
        public Color LedBase { get; private set; } = Colors.Black;
        private LedAnim? _anim;
        private Color _animColor = Colors.Cyan;
        private double _animT;                       // seconds since the animation started
        public int StateValue { get; private set; }

        // timer
        private double _timerLeft = -1;              // <0 = not armed
        private bool _timerFired;

        // clap (PC microphone or the on-screen 👏): pending until the rule loop
        // actually runs, then true for exactly one pass — a clap during a Wait
        // isn't lost, it fires right after (kind to small hands and long sounds)
        private bool _clapPending;
        private bool _clapFired;

        // rule-loop blocking (Wait / Sound)
        private double _blockedFor;

        // button-driven movement is "while held": stop the motors when the button is released
        private bool _btnDrivePrev;
        private bool _pendingBtnDrive;

        // "follow the line" drive mode (latched, steered every pass by the ground sensor)
        private bool _followMode;
        private double _followSpeed = 150;

        // inputs from the on-screen D-pad
        private readonly HashSet<ButtonDir> _pressed = new HashSet<ButtonDir>();

        // sensors / world flags (read by the view for the HUD)
        public bool FrontDetected { get; private set; }
        public bool OnLine { get; private set; }
        public bool Collided { get; private set; }
        public bool AtGoal { get; private set; }
        public bool AnyButtonDown => _pressed.Count > 0;
        public bool IsDriving => Math.Abs(_l) > 1 || Math.Abs(_r) > 1;
        public LedAnim? ActiveAnim => _anim;

        // mission scenario knobs (the simulator sets these before Start)
        public bool ObstacleVisible = true;
        public Point ObstacleAt;
        public bool CoinsActive;
        public bool GoalActive;
        public bool[] CoinTaken;

        /// <summary>Raised when a rule plays a sound (the view plays it on the PC + hops the bot).</summary>
        public event Action<VplAction> SoundPlayed;
        /// <summary>Raised when the robot drives over an active coin (index).</summary>
        public event Action<int> CoinCollected;
        /// <summary>Raised each time the robot newly bumps into the obstacle brick.</summary>
        public event Action Bumped;

        private bool _inContact;

        public VplRuntime(SimWorld world, IEnumerable<VplRule> rules)
        {
            _world = world;
            _rules = rules.Select(CloneRule).ToList();
            ObstacleAt = world.ObstaclePos;
            CoinTaken = new bool[world.Coins.Count];

            X = world.StartPos.X; Y = world.StartPos.Y;
            // manifest heading: 0° = +X; model yaw: 0° = +Y  →  yaw = heading - 90
            YawDeg = world.StartDeg - 90;

            // Start rules run once, before the loop (their Waits/Sounds block like the firmware's).
            foreach (var r in _rules.Where(r => r.Event.Kind == EventKind.Start))
                foreach (var a in r.Actions)
                    _queue.Enqueue(a);
        }

        private static VplRule CloneRule(VplRule r)
        {
            var c = new VplRule { Event = r.Event.Clone() };
            foreach (var a in r.Actions) c.Actions.Add(a.Clone());
            return c;
        }

        public void ButtonDown(ButtonDir d) => _pressed.Add(d);
        public void ButtonUp(ButtonDir d) => _pressed.Remove(d);

        /// <summary>Inject one clap (from the PC microphone or the on-screen 👏 button).</summary>
        public void Clap() => _clapPending = true;

        // ------------------------------------------------------------------ tick

        public void Tick(double dt)
        {
            if (dt <= 0) return;
            _animT += dt;

            // 1. sensors against the manifest geometry
            double yawRad = YawDeg * Math.PI / 180.0;
            var fwd = new Vector(-Math.Sin(yawRad), Math.Cos(yawRad));
            var pos = new Point(X, Y);

            var nose = pos + fwd * _world.RobotFrontY;
            FrontDetected = ObstacleVisible &&
                            _world.RayToObstacle(nose, fwd, ObstacleAt) <= _world.IrRange;

            OnLine = _world.OnTrack(pos + fwd * _world.GroundSensorY);

            // 2. one-shot timer
            _timerFired = false;
            if (_timerLeft >= 0)
            {
                _timerLeft -= dt;
                if (_timerLeft < 0) { _timerFired = true; _timerLeft = -1; }
            }

            // 3. the rule loop (skipped while a Wait/Sound is blocking — like time.sleep)
            if (_blockedFor > 0)
            {
                _blockedFor -= dt;
            }
            else
            {
                if (_queue.Count == 0)
                {
                    // consume the pending clap: true for exactly this rule pass
                    _clapFired = _clapPending;
                    _clapPending = false;

                    bool btnDrive = false;
                    foreach (var r in _rules.Where(r => r.Event.Kind != EventKind.Start))
                        if (EventTrue(r.Event))
                        {
                            foreach (var a in r.Actions)
                                _queue.Enqueue(a);
                            if (r.Event.Kind == EventKind.Button &&
                                r.Actions.Any(a => a.Kind == ActionKind.Move))
                                btnDrive = true;
                        }
                    _pendingBtnDrive = btnDrive;
                }
                while (_blockedFor <= 0 && _queue.Count > 0)
                    Apply(_queue.Dequeue());

                // end of a rule pass: if the driving button was released, stop (mirrors firmware)
                if (_queue.Count == 0 && _blockedFor <= 0)
                {
                    if (_btnDrivePrev && !_pendingBtnDrive) { _l = 0; _r = 0; _followMode = false; }
                    _btnDrivePrev = _pendingBtnDrive;
                }
            }

            // follow-the-line mode: steer with the ground sensor every pass
            // (off the line → physical left turn = (-f, f), a left-hand search)
            if (_followMode)
            {
                _l = OnLine ? _followSpeed : -_followSpeed;
                _r = _followSpeed;
            }

            // 4. physics: differential drive (always integrates — sleep doesn't stop motors)
            double v = (_l + _r) / 2.0 / 75.0;            // 150 avg → 2 units/s
            // standard diff-drive: +omega = CCW = left; matches the firmware (left = motors(-s, s))
            double omega = (_r - _l) / 3.0;
            YawDeg += omega * dt;
            double nx = X + fwd.X * v * dt;
            double ny = Y + fwd.Y * v * dt;

            // stay on the mat (margin = robot half-length so the nose never crosses the rim)
            nx = Math.Max(-_world.MatHalf.X + 1.8, Math.Min(_world.MatHalf.X - 1.8, nx));
            ny = Math.Max(-_world.MatHalf.Y + 1.8, Math.Min(_world.MatHalf.Y - 1.8, ny));

            // soft collision with the obstacle brick: stop at the face, remember the bump
            if (ObstacleVisible &&
                _world.CircleHitsObstacle(new Point(nx, ny), _world.RobotHalfWidth, ObstacleAt))
            {
                Collided = true;
                if (!_inContact) { _inContact = true; Bumped?.Invoke(); }
            }
            else
            {
                _inContact = false;
                X = nx; Y = ny;
            }

            // 5. coins + goal
            if (CoinsActive)
                for (int i = 0; i < _world.Coins.Count; i++)
                    if (!CoinTaken[i] && (new Point(X, Y) - _world.Coins[i]).Length <= _world.CoinRadius + 0.6)
                    {
                        CoinTaken[i] = true;
                        CoinCollected?.Invoke(i);
                    }

            AtGoal = GoalActive && (new Point(X, Y) - _world.GoalPos).Length <= _world.GoalRadius;
        }

        private bool EventTrue(VplEvent e)
        {
            if (e.StateFilter >= 0 && StateValue != e.StateFilter) return false;
            return e.Kind switch
            {
                EventKind.Button => _pressed.Contains(e.Button),
                EventKind.Obstacle => e.Detected ? FrontDetected : !FrontDetected,
                EventKind.Line => e.Detected ? OnLine : !OnLine,
                EventKind.Timer => _timerFired,
                EventKind.Clap => _clapFired,
                _ => false
            };
        }

        private void Apply(VplAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Move:
                    int s = a.Speed;
                    if (a.Move == MoveDir.FollowLine)
                    {
                        _followMode = true;
                        _followSpeed = s;
                        break;
                    }
                    _followMode = false;   // a direct drive command takes over
                    (_l, _r) = a.Move switch
                    {
                        MoveDir.Forward => ((double)s, (double)s),
                        MoveDir.Backward => ((double)-s, (double)-s),
                        // same wheel signs as the firmware: left turn = left wheel back, right fwd
                        MoveDir.Left => ((double)-s, (double)s),
                        MoveDir.Right => ((double)s, (double)-s),
                        _ => (0.0, 0.0)
                    };
                    break;
                case ActionKind.Color:
                    LedBase = Color.FromRgb(a.R, a.G, a.B);
                    _anim = null;                       // a static colour cancels the animation
                    break;
                case ActionKind.Anim:
                    _anim = a.Anim;
                    _animColor = Color.FromRgb(a.R, a.G, a.B);
                    _animT = 0;
                    break;
                case ActionKind.Sound:
                    _blockedFor = a.Sound switch
                    {
                        SoundKind.Happy => 0.36,
                        SoundKind.Sad => 0.48,
                        SoundKind.Siren => 0.48,
                        SoundKind.Custom => Math.Max(1, a.Notes.Count(n => n.Pitch >= 0)) * (VplCompiler.NoteMs / 1000.0),
                        _ => 0.15
                    };
                    SoundPlayed?.Invoke(a);
                    break;
                case ActionKind.Wait:
                    _blockedFor = a.Seconds;
                    break;
                case ActionKind.Timer:
                    _timerLeft = a.Seconds;
                    break;
                case ActionKind.State:
                    StateValue = a.StateValue;
                    break;
            }
        }

        /// <summary>The LED ring colour right now, animations included (shared with the live twin).</summary>
        public Color CurrentLedColor()
        {
            if (_anim == null) return LedBase;
            return LedAnimMath.Evaluate(_anim.Value, _animColor, _animT * 1000.0);
        }
    }
}
