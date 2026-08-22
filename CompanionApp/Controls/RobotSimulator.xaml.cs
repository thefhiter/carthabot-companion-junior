using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using CarthaBotVPL.Models;
using CarthaBotVPL.Services;
using HelixToolkit.Wpf;

namespace CompanionApp.Controls
{
    /// <summary>
    /// The VPL playground: a 3D play mat (Blender-authored, with a JSON manifest of its exact
    /// coordinates) where a virtual CarthaBot RUNS THE CHILD'S RULES — the on-screen D-pad fires
    /// Button events, the front IR "sees" the obstacle brick, the ground sensor "sees" the black
    /// loop, and missions award stars when their goal is met.
    /// </summary>
    public partial class RobotSimulator : UserControl
    {
        private SimWorld World;                 // the active map's geometry
        private SimMap _map = SimMap.All[0];     // current map (default: classic)
        private static readonly Dictionary<string, Model3D> _worldModels = new Dictionary<string, Model3D>();
        private static readonly SolidColorBrush DimDot = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));

        /// <summary>The map currently shown (so callers can re-run on the same map).</summary>
        public SimMap CurrentMap => _map;

        // shared, frozen models (loaded once per process)
        private static Model3D _robotModel, _obstacleModel, _coinModel, _goalModel;

        private AxisAngleRotation3D _yawRot;
        private TranslateTransform3D _trans;
        private SolidColorBrush _haloBrush;
        private TranslateTransform3D _obstacleTrans;
        private readonly List<ModelVisual3D> _coinVisuals = new List<ModelVisual3D>();

        private VplRuntime _rt;
        private LiveTwin _twin;       // live digital twin of the real robot (telemetry-driven)
        private bool _liveMode;
        private Mission _mission;
        private bool _missionDone;
        private double _stillTime;

        // active actor accessors — read from the live twin or the virtual runtime
        private bool HasActor => _liveMode ? _twin != null : _rt != null;
        private double SrcX => _liveMode ? _twin.X : _rt.X;
        private double SrcY => _liveMode ? _twin.Y : _rt.Y;
        private double SrcYaw => _liveMode ? _twin.YawDeg : _rt.YawDeg;
        private Color SrcLed => _liveMode ? _twin.CurrentLedColor() : _rt.CurrentLedColor();
        private bool SrcFront => _liveMode ? _twin.FrontDetected : _rt.FrontDetected;
        private bool SrcLine => _liveMode ? _twin.OnLine : _rt.OnLine;

        // follow-the-line mission progress (3 quarter checkpoints + return to start)
        private readonly bool[] _lineVisited = new bool[3];
        private double _lineTime;

        // the pen trail: the robot paints its path in its current LED colour
        private readonly List<LinesVisual3D> _trail = new List<LinesVisual3D>();
        private LinesVisual3D _trailSeg;
        private Point3D? _trailLast;
        private Color _trailColor;

        private bool _followCam;
        private bool _disco;
        private int _coinsTaken;

        // rolling wheels (the wheel parts of the cloned model share one axle rotation)
        private AxisAngleRotation3D _wheelRot;
        private double _wheelRadius = 0.55;
        private double _prevX, _prevY;

        // sensor visualisation: IR beam triangle + ground-sensor dot (robot-local space)
        private ModelVisual3D _beamVisual;
        private GeometryModel3D _beamModel;
        private SolidColorBrush _groundDotBrush;

        private DispatcherTimer _timer;
        private readonly Stopwatch _clock = new Stopwatch();
        private TimeSpan _lastTick;

        // 👏 clap event: the PC microphone stands in for the robot's (which the real
        // robot uses itself when a compiled program runs — MIC400 on GP27/ADC1)
        private ClapDetector _clapDetector;
        private bool _hasClapRule;

        /// <summary>Raised once when the active mission's goal is reached.</summary>
        public event Action<Mission> MissionCompleted;

        private static string L(string key, string fallback) =>
            Application.Current?.TryFindResource(key) as string ?? fallback;

        public RobotSimulator()
        {
            InitializeComponent();
            LoadModels();
            World = SimWorld.Load(_map.Manifest);
            BuildRobotAndDecor();    // robot, halo, clouds, sensors (map-independent)
            ApplyMap();              // floor + obstacle/goal/coins for the current map
            PopulateMapPicker();
            // if the studio is torn down with the sim running, release the microphone
            Unloaded += (_, __) => StopClapListening();
        }

        private IEnumerable<VplRule> _lastRules;

        /// <summary>Row model for one map-picker chip.</summary>
        private class MapChip
        {
            public SimMap Map { get; init; }
            public string Glyph => Map.Glyph;
            public string Name { get; init; }
            public bool Selected { get; init; }
        }

        private void PopulateMapPicker()
        {
            MapPicker.ItemsSource = SimMap.All.Select(m => new MapChip
            {
                Map = m,
                Name = L(m.NameKey, m.Id),
                Selected = m.Id == _map.Id
            }).ToList();
        }

        private void OnPickMap(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.Tag is SimMap map) || map.Id == _map.Id) return;
            bool wasLive = _liveMode;
            var rules = _lastRules;
            SetMap(map);
            PopulateMapPicker();
            UiSounds.Blip();
            // resume the current activity on the new map (free play; missions are map-specific)
            if (wasLive) RunLive();
            else if (rules != null) Run(rules);
        }

        /// <summary>Switch the playground to another map (rebuilds the floor and props).</summary>
        public void SetMap(SimMap map)
        {
            StopSim();
            _map = map;
            World = SimWorld.Load(map.Manifest);
            ApplyMap();
            ResetRobotToStart();
        }

        private void ResetRobotToStart()
        {
            if (_trans == null) return;
            _trans.OffsetX = World.StartPos.X;
            _trans.OffsetY = World.StartPos.Y;
            _yawRot.Angle = World.StartDeg - 90;
            _prevX = World.StartPos.X; _prevY = World.StartPos.Y;
            ClearTrail();
            SetHalo(Colors.Black);
        }

        // ------------------------------------------------------------------ scene

        private static Model3D Import(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Vpl", "Assets", name);
            if (!File.Exists(path)) return null;
            var m = new ModelImporter().Load(path);
            m.Freeze();
            return m;
        }

        private static void LoadModels()
        {
            try
            {
                _robotModel ??= Import("carthabot_robot.obj");
                _obstacleModel ??= Import("carthabot_obstacle.obj");
                _coinModel ??= Import("carthabot_coin.obj");
                _goalModel ??= Import("carthabot_goalflag.obj");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RobotSimulator models: " + ex.Message);
            }
        }

        // The Blender OBJ exporter (forward=-Y, up=Z) writes the scene rotated 180° around Z,
        // so every imported model gets this fixed pre-rotation to land on manifest coordinates.
        private static RotateTransform3D ExportFix() =>
            new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 180));

        private static Model3D WorldModel(string objFile)
        {
            if (!_worldModels.TryGetValue(objFile, out var m))
            {
                m = Import(objFile);
                _worldModels[objFile] = m;
            }
            return m;
        }

        /// <summary>The map floor + its obstacle / goal / coins (rebuilt on every map switch).</summary>
        private void ApplyMap()
        {
            var wm = WorldModel(_map.WorldObj);
            if (wm != null)
            {
                WorldHost.Content = wm;
                WorldHost.Transform = ExportFix();
            }

            if (_obstacleModel != null)
            {
                ObstacleHost.Content = _obstacleModel;
                _obstacleTrans = new TranslateTransform3D(World.ObstaclePos.X, World.ObstaclePos.Y, 0);
                var og = new Transform3DGroup();
                og.Children.Add(ExportFix());
                og.Children.Add(_obstacleTrans);
                ObstacleHost.Transform = og;
            }

            if (_goalModel != null)
            {
                GoalHost.Content = _goalModel;
                var g = new Transform3DGroup();
                g.Children.Add(ExportFix());
                g.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 150)));
                g.Children.Add(new TranslateTransform3D(World.GoalPos.X, World.GoalPos.Y, 0));
                GoalHost.Transform = g;
            }

            // spinning coins at this map's manifest positions
            CoinsHost.Children.Clear();
            _coinVisuals.Clear();
            if (_coinModel != null)
            {
                foreach (var c in World.Coins)
                {
                    var spin = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
                    var grp = new Transform3DGroup();
                    grp.Children.Add(new RotateTransform3D(spin));
                    grp.Children.Add(new TranslateTransform3D(c.X, c.Y, World.CoinZ));
                    var vis = new ModelVisual3D { Content = _coinModel, Transform = grp };
                    spin.BeginAnimation(AxisAngleRotation3D.AngleProperty,
                        new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(3.2)))
                        { RepeatBehavior = RepeatBehavior.Forever });
                    _coinVisuals.Add(vis);
                    CoinsHost.Children.Add(vis);
                }
            }
        }

        /// <summary>Robot, LED halo, sensor beam and clouds — identical across all maps.</summary>
        private void BuildRobotAndDecor()
        {
            if (_robotModel != null)
            {
                _yawRot = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
                _trans = new TranslateTransform3D(World.StartPos.X, World.StartPos.Y, 0);
                var grp = new Transform3DGroup();
                grp.Children.Add(ExportFix());
                grp.Children.Add(new RotateTransform3D(_yawRot));
                grp.Children.Add(_trans);
                var robot = (Model3D)_robotModel.Clone();   // own copy so the wheels can roll
                RigWheels(robot);
                RobotHost.Content = robot;
                RobotHost.Transform = grp;
                Halo.Transform = grp;
                BuildSensorVisuals(grp);
            }
            BuildHalo();
            BuildClouds();
        }

        /// <summary>Find the wheel parts of the imported model (they span both sides in X but
        /// are small in Y — one wheel diameter) and hook them to a shared axle rotation.</summary>
        private void RigWheels(Model3D model)
        {
            if (!(model is Model3DGroup grp)) return;
            var wheels = new List<GeometryModel3D>();
            var b = Rect3D.Empty;
            foreach (var child in grp.Children.OfType<GeometryModel3D>())
            {
                var cb = child.Bounds;
                if (cb.SizeX > 2.0 && cb.SizeY < 1.5 && Math.Abs(cb.Y + cb.SizeY / 2) < 1.0)
                {
                    wheels.Add(child);
                    b.Union(cb);
                }
            }
            if (wheels.Count == 0) return;
            double cy = b.Y + b.SizeY / 2, cz = b.Z + b.SizeZ / 2;
            _wheelRadius = Math.Max(0.2, b.SizeZ / 2);
            _wheelRot = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            var spin = new RotateTransform3D(_wheelRot, new Point3D(0, cy, cz));
            foreach (var w in wheels) w.Transform = spin;
        }

        /// <summary>IR beam + ground-sensor dot, riding the robot's transform. Geometry is in
        /// RAW model space, where the front is -Y (the ExportFix flips it to +Y on screen).</summary>
        private void BuildSensorVisuals(Transform3D robotTransform)
        {
            // translucent red beam triangle, nose to IR range
            var beam = new MeshGeometry3D();
            double z = 0.5;
            beam.Positions.Add(new Point3D(0, -World.RobotFrontY, z));
            beam.Positions.Add(new Point3D(-0.95, -(World.RobotFrontY + World.IrRange), z));
            beam.Positions.Add(new Point3D(0.95, -(World.RobotFrontY + World.IrRange), z));
            beam.TriangleIndices.Add(0); beam.TriangleIndices.Add(1); beam.TriangleIndices.Add(2);
            beam.Normals.Add(new Vector3D(0, 0, 1)); beam.Normals.Add(new Vector3D(0, 0, 1)); beam.Normals.Add(new Vector3D(0, 0, 1));
            var beamBrush = new SolidColorBrush(Color.FromArgb(60, 255, 70, 30));
            var beamMat = new MaterialGroup();
            beamMat.Children.Add(new DiffuseMaterial(beamBrush));
            beamMat.Children.Add(new EmissiveMaterial(beamBrush));
            _beamModel = new GeometryModel3D(beam, beamMat) { BackMaterial = beamMat };
            _beamVisual = new ModelVisual3D { Transform = robotTransform };
            View.Children.Add(_beamVisual);

            // ground-sensor dot under the nose: bright when it sees the line
            var dot = new MeshGeometry3D();
            const int seg = 16; const double r = 0.22, dz = 0.065;
            var gc = new Point3D(0, -World.GroundSensorY, dz);
            dot.Positions.Add(gc); dot.Normals.Add(new Vector3D(0, 0, 1));
            for (int i = 0; i <= seg; i++)
            {
                double t = i / (double)seg * 2 * Math.PI;
                dot.Positions.Add(new Point3D(gc.X + Math.Cos(t) * r, gc.Y + Math.Sin(t) * r, dz));
                dot.Normals.Add(new Vector3D(0, 0, 1));
            }
            for (int i = 1; i <= seg; i++)
            {
                dot.TriangleIndices.Add(0); dot.TriangleIndices.Add(i); dot.TriangleIndices.Add(i + 1);
            }
            _groundDotBrush = new SolidColorBrush(Color.FromArgb(90, 120, 130, 140));
            var dotMat = new MaterialGroup();
            dotMat.Children.Add(new DiffuseMaterial(_groundDotBrush));
            dotMat.Children.Add(new EmissiveMaterial(_groundDotBrush));
            var dotVisual = new ModelVisual3D
            {
                Content = new GeometryModel3D(dot, dotMat),
                Transform = robotTransform
            };
            View.Children.Add(dotVisual);
        }

        /// <summary>Three soft clouds drifting across the sky behind the playground.</summary>
        private void BuildClouds()
        {
            var rnd = new Random(7);
            for (int c = 0; c < 3; c++)
            {
                var cluster = new ModelVisual3D();
                double s = 1.0 + c * 0.25;
                foreach (var (ox, oy, rr) in new[] { (0.0, 0.0, 1.7), (1.6, 0.3, 1.25), (-1.5, 0.2, 1.1) })
                {
                    cluster.Children.Add(new SphereVisual3D
                    {
                        Center = new Point3D(ox * s, oy * s, 0),
                        Radius = rr * s,
                        Fill = new SolidColorBrush(Color.FromRgb(0xE9, 0xEE, 0xF4)),
                        ThetaDiv = 14, PhiDiv = 10
                    });
                }
                var drift = new TranslateTransform3D(0, 15 + c * 2.5, 10 + c * 1.8);
                cluster.Transform = drift;
                double dur = 70 + rnd.Next(40);
                drift.BeginAnimation(TranslateTransform3D.OffsetXProperty,
                    new DoubleAnimation(-32, 32, new Duration(TimeSpan.FromSeconds(dur)))
                    { RepeatBehavior = RepeatBehavior.Forever });
                CloudHost.Children.Add(cluster);
            }
        }

        private void BuildHalo()
        {
            var mesh = new MeshGeometry3D();
            const int seg = 36;
            // wider than the robot footprint so the glow shows AROUND the shell
            // (the real Thymio's bottom RGB LEDs spill onto the ground the same way)
            const double r = 2.05, z = 0.055;
            mesh.Positions.Add(new Point3D(0, 0, z));
            mesh.Normals.Add(new Vector3D(0, 0, 1));
            for (int i = 0; i <= seg; i++)
            {
                double t = i / (double)seg * 2 * Math.PI;
                mesh.Positions.Add(new Point3D(Math.Cos(t) * r, Math.Sin(t) * r, z));
                mesh.Normals.Add(new Vector3D(0, 0, 1));
            }
            for (int i = 1; i <= seg; i++)
            {
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(i);
                mesh.TriangleIndices.Add(i + 1);
            }
            _haloBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            var mat = new MaterialGroup();
            mat.Children.Add(new DiffuseMaterial(_haloBrush));
            mat.Children.Add(new EmissiveMaterial(_haloBrush));
            Halo.Content = new GeometryModel3D(mesh, mat);
        }

        // ------------------------------------------------------------------ control surface

        /// <summary>Start simulating the given rules; optionally inside a guided mission.</summary>
        public void Run(IEnumerable<VplRule> rules, Mission mission = null)
        {
            StopSim();
            _mission = mission;
            _missionDone = false;
            _stillTime = 0;
            _lastRules = rules;

            _rt = new VplRuntime(World, rules ?? Enumerable.Empty<VplRule>());

            // scenario: free play shows the whole playground; missions tailor it
            bool free = mission == null;
            _rt.ObstacleVisible = free || mission.ShowObstacle || mission.ObstacleOverride.HasValue;
            _rt.ObstacleAt = mission?.ObstacleOverride ?? World.ObstaclePos;
            _rt.CoinsActive = free || mission.ShowCoins;
            _rt.GoalActive = free || mission.ShowGoal;

            _rt.SoundPlayed += OnRuntimeSound;
            _rt.CoinCollected += OnRuntimeCoin;
            _rt.Bumped += OnRuntimeBump;

            // visuals follow the scenario
            ObstacleHost.Content = _rt.ObstacleVisible ? _obstacleModel : null;
            _obstacleTrans?.SetValue(TranslateTransform3D.OffsetXProperty, _rt.ObstacleAt.X);
            _obstacleTrans?.SetValue(TranslateTransform3D.OffsetYProperty, _rt.ObstacleAt.Y);
            GoalHost.Content = _rt.GoalActive ? _goalModel : null;
            for (int i = 0; i < _coinVisuals.Count; i++)
                _coinVisuals[i].Content = _rt.CoinsActive ? _coinModel : null;

            // HUD + banner
            CoinChip.Visibility = _rt.CoinsActive ? Visibility.Visible : Visibility.Collapsed;
            CoinText.Text = $"💰 0/{World.Coins.Count}";
            StateChip.Visibility = Visibility.Collapsed;
            LiveDetailsPanel.Visibility = Visibility.Collapsed;
            PadHint.Text = L("vplSimPad", "CarthaBot's buttons");
            if (mission != null)
            {
                MissionBanner.Visibility = Visibility.Visible;
                MissionBanner.Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x15, 0x22, 0x2A));
                MissionGlyph.Text = mission.Glyph;
                MissionTitle.Text = L(mission.TitleKey, mission.Id);
                MissionHint.Text = L(mission.HintKey, "");
                MissionStar.Visibility = Visibility.Collapsed;

                // slide the banner in from the top
                var slide = new System.Windows.Media.TranslateTransform(0, -46);
                MissionBanner.RenderTransform = slide;
                slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new DoubleAnimation(-46, 0, TimeSpan.FromSeconds(0.45))
                    { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } });
                MissionBanner.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)));
            }
            else
            {
                MissionBanner.Visibility = Visibility.Collapsed;
            }

            // 👏 rules: listen on the PC microphone (and show the clap tip) while running
            _hasClapRule = rules != null && rules.Any(r => r.Event.Kind == EventKind.Clap);
            StartClapListening();

            // reset mission progress, pen trail and pose
            for (int i = 0; i < _lineVisited.Length; i++) _lineVisited[i] = false;
            _lineTime = 0;
            _coinsTaken = 0;
            ClearTrail();
            ConfettiLayer.Children.Clear();
            SetHalo(Colors.Black);
            UpdateRobotVisual();

            _clock.Restart();
            _lastTick = TimeSpan.Zero;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _timer.Tick += (_, __) => Tick();
            _timer.Start();
        }

        /// <summary>Mirror the REAL robot live: the twin is driven by serial telemetry, not rules.
        /// The whole playground is shown (free-play scenario) and missions/D-pad are inactive.</summary>
        public void RunLive()
        {
            StopSim();
            _liveMode = true;
            _twin = new LiveTwin(World);
            _mission = null;
            _missionDone = false;

            // show the full playground
            ObstacleHost.Content = _obstacleModel;
            _obstacleTrans?.SetValue(TranslateTransform3D.OffsetXProperty, World.ObstaclePos.X);
            _obstacleTrans?.SetValue(TranslateTransform3D.OffsetYProperty, World.ObstaclePos.Y);
            GoalHost.Content = _goalModel;
            // no coins in live mode — it mirrors the real robot, not the coin game
            for (int i = 0; i < _coinVisuals.Count; i++) _coinVisuals[i].Content = null;

            // HUD + a "LIVE" banner
            CoinChip.Visibility = Visibility.Collapsed;
            StateChip.Visibility = Visibility.Collapsed;
            LiveDetailsPanel.Visibility = Visibility.Visible;
            UpdateLiveDetails();
            PadHint.Text = L("vplSimRealBtns", "Press the buttons on the real CarthaBot");
            MissionBanner.Visibility = Visibility.Visible;
            MissionBanner.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x8A, 0x12, 0x12));
            MissionGlyph.Text = "📡";
            MissionTitle.Text = L("vplSimLiveTitle", "LIVE — mirroring the real CarthaBot");
            MissionHint.Text = L("vplSimLiveHint", "The 3D robot moves with your real robot in real time");
            MissionStar.Visibility = Visibility.Collapsed;

            // live mode: the REAL robot listens with its own microphone (GP27/ADC1) and
            // runs its clap rules itself — the PC mic must not inject a second clap
            _hasClapRule = false;
            StopClapListening();

            ClearTrail();
            ConfettiLayer.Children.Clear();
            SetHalo(Colors.Black);
            _prevX = _twin.X; _prevY = _twin.Y;
            UpdateRobotVisual();

            _clock.Restart();
            _lastTick = TimeSpan.Zero;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _timer.Tick += (_, __) => Tick();
            _timer.Start();
        }

        /// <summary>Feed one parsed telemetry packet from the real robot to the twin.</summary>
        public void FeedTelemetry(int[] values) => _twin?.Update(values);

        public void StopSim()
        {
            _timer?.Stop();
            _timer = null;
            if (_rt != null)
            {
                _rt.SoundPlayed -= OnRuntimeSound;
                _rt.CoinCollected -= OnRuntimeCoin;
                _rt.Bumped -= OnRuntimeBump;
                _rt = null;
            }
            _twin = null;
            _liveMode = false;
            LiveDetailsPanel.Visibility = Visibility.Collapsed;
            StopClapListening();
            _trans?.BeginAnimation(TranslateTransform3D.OffsetZProperty, null);
        }

        // ---------------------------------------------------------------- 👏 clap event

        /// <summary>Show the microphone tip and start listening for real hand claps.
        /// No usable mic (unplugged, or blocked by Windows privacy) → the tip switches
        /// to "tap 👏" and the on-screen button carries the event alone.</summary>
        private void StartClapListening()
        {
            StopClapListening();
            if (!_hasClapRule) return;

            ClapTip.Visibility = Visibility.Visible;
            var det = new ClapDetector();
            det.Clapped += OnMicClap;                      // driver thread → marshalled below
            bool listening = det.Start();
            if (listening)
            {
                _clapDetector = det;
                ClapTipText.Text = L("vplClapTip", "Clap your hands — CarthaBot is listening!");
                ClapMicGlyph.Opacity = 1.0;
            }
            else
            {
                det.Clapped -= OnMicClap;
                det.Dispose();
                ClapTipText.Text = L("vplClapTipNoMic", "No microphone — tap 👏 to clap!");
                ClapMicGlyph.Opacity = 0.35;
            }
        }

        private void StopClapListening()
        {
            if (_clapDetector != null)
            {
                _clapDetector.Clapped -= OnMicClap;
                _clapDetector.Dispose();
                _clapDetector = null;
            }
            if (ClapTip != null) ClapTip.Visibility = Visibility.Collapsed;
        }

        private void OnMicClap() =>
            Dispatcher.BeginInvoke(new Action(InjectClap));

        private void OnClapButton(object sender, RoutedEventArgs e) => InjectClap();

        private void InjectClap()
        {
            if (_rt == null) return;
            _rt.Clap();
            // little "heard you!" pop on the mic glyph
            var pop = new DoubleAnimationUsingKeyFrames();
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.45, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.09)), new SineEase { EasingMode = EasingMode.EaseOut }));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.28)), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }));
            ClapMicScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            ClapMicScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        // ------------------------------------------------------------------ loop

        private void Tick()
        {
            if (!HasActor) return;
            var now = _clock.Elapsed;
            double dt = Math.Min(0.1, (now - _lastTick).TotalSeconds);
            _lastTick = now;

            if (_liveMode) _twin.Tick(dt); else _rt.Tick(dt);
            UpdateRobotVisual();
            SetHalo(SrcLed);
            UpdateTrail();
            UpdateChaseCamera();

            // HUD
            FrontDot.Fill = SrcFront ? Brushes.OrangeRed : DimDot;
            LineDot.Fill = SrcLine ? Brushes.White : DimDot;

            // the mic glyph breathes with what the microphone hears ("it's listening!")
            if (_clapDetector != null)
                ClapMicGlyph.Opacity = 0.55 + 0.45 * Math.Min(1.0, _clapDetector.Level * 12);

            if (!_liveMode)
            {
                UpdateStateChip();
                if (_rt.CoinsActive)
                    CoinText.Text = $"💰 {_rt.CoinTaken.Count(t => t)}/{World.Coins.Count}";
                CheckMission(dt);
            }
            else
            {
                UpdateLiveDetails();
            }
        }

        /// <summary>Live read-out of the twin's computed motion (live mode only).</summary>
        private void UpdateLiveDetails()
        {
            if (_twin == null) return;
            double heading = ((_twin.YawDeg % 360) + 360) % 360;
            string turnWord = _twin.TurnRateDeg > 3 ? "left"
                            : _twin.TurnRateDeg < -3 ? "right" : "—";
            string arc = double.IsNaN(_twin.TurnRadius) ? "straight"
                       : Math.Abs(_twin.TurnRadius) < 0.05 ? "spin in place"
                       : $"{Math.Abs(_twin.TurnRadius):0.0} u";
            string lL = _twin.RawLineLeft < 0 ? "?" : _twin.RawLineLeft.ToString();
            string lR = _twin.RawLineRight < 0 ? "?" : _twin.RawLineRight.ToString();
            LiveDetailsText.Text = string.Join("\n", new[]
            {
                $"L wheel {_twin.LeftCmd,4}  {_twin.LeftSpeed,5:0.0} u/s",
                $"R wheel {_twin.RightCmd,4}  {_twin.RightSpeed,5:0.0} u/s",
                $"speed   {_twin.LinearSpeed,6:0.00} u/s",
                $"turn    {_twin.TurnRateDeg,6:0.0} °/s {turnWord}",
                $"arc     {arc}",
                $"heading {heading,6:0} °",
                $"moved   {_twin.DistanceTraveled,6:0.0} u",
                $"turned  {_twin.TotalTurnedDeg,6:0} °",
                "—— sensors ——",
                $"front    {(_twin.FrontDetected ? "OBSTACLE" : "clear")}",
                $"line  L={lL} R={lR}  {(_twin.OnLine ? "ON LINE" : "off")}",
            });
        }

        private void UpdateRobotVisual()
        {
            if (!HasActor || _yawRot == null) return;
            _yawRot.Angle = SrcYaw;
            _trans.OffsetX = SrcX;
            _trans.OffsetY = SrcY;

            // roll the wheels by the distance actually travelled
            if (_wheelRot != null)
            {
                double yaw = SrcYaw * Math.PI / 180.0;
                double dx = SrcX - _prevX, dy = SrcY - _prevY;
                double dist = dx * -Math.Sin(yaw) + dy * Math.Cos(yaw);   // signed along heading
                _wheelRot.Angle -= dist / _wheelRadius * 180.0 / Math.PI;
            }
            _prevX = SrcX; _prevY = SrcY;

            // sensor visualisation
            if (_beamVisual != null)
            {
                var want = SrcFront ? _beamModel : null;
                if (!ReferenceEquals(_beamVisual.Content, want)) _beamVisual.Content = want;
            }
            if (_groundDotBrush != null)
                _groundDotBrush.Color = SrcLine
                    ? Color.FromArgb(220, 160, 240, 255)
                    : Color.FromArgb(90, 120, 130, 140);
        }

        private void SetHalo(Color c)
        {
            if (_haloBrush == null) return;
            bool off = c.R == 0 && c.G == 0 && c.B == 0;
            _haloBrush.Color = off ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(190, c.R, c.G, c.B);
        }

        private void UpdateStateChip()
        {
            if (_rt == null) return;
            // the chip appears as soon as the program plays with memory
            string[] glyphs = { "★", "♥", "●", "■" };
            Color[] cols =
            {
                Color.FromRgb(0xF2, 0xC9, 0x4C), Color.FromRgb(0xEB, 0x57, 0x57),
                Color.FromRgb(0x2D, 0x9C, 0xDB), Color.FromRgb(0x37, 0xD6, 0x7A)
            };
            if (StateChip.Visibility != Visibility.Visible && _rt.StateValue == 0) return;
            StateChip.Visibility = Visibility.Visible;
            StateGlyph.Text = glyphs[_rt.StateValue];
            StateGlyph.Foreground = new SolidColorBrush(cols[_rt.StateValue]);
        }

        private void OnRuntimeSound(VplAction a)
        {
            // with open speakers our own sound would "clap" back into the microphone
            if (UiSounds.Enabled)
                _clapDetector?.SuppressFor(0.35 + a.Sound switch
                {
                    SoundKind.Happy => 0.36,
                    SoundKind.Sad => 0.48,
                    SoundKind.Siren => 0.48,
                    SoundKind.Custom => Math.Max(1, a.Notes.Count(n => n.Pitch >= 0)) * (VplCompiler.NoteMs / 1000.0),
                    _ => 0.15
                });

            if (a.Sound == SoundKind.Custom) UiSounds.Tune(a.Notes);
            else UiSounds.Preset(a.Sound);

            // the classic happy hop
            var hop = new DoubleAnimationUsingKeyFrames();
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(0.55, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.22)), new SineEase { EasingMode = EasingMode.EaseOut }));
            hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5)), new SineEase { EasingMode = EasingMode.EaseIn }));
            hop.Completed += (_, __) => { _trans?.BeginAnimation(TranslateTransform3D.OffsetZProperty, null); if (_trans != null) _trans.OffsetZ = 0; };
            _trans?.BeginAnimation(TranslateTransform3D.OffsetZProperty, hop);
        }

        private void OnRuntimeCoin(int index)
        {
            if (index >= 0 && index < _coinVisuals.Count)
                _coinVisuals[index].Content = null;
            if (UiSounds.Enabled) _clapDetector?.SuppressFor(0.5);
            UiSounds.Coin(_coinsTaken++);          // pitch rises with every coin
        }

        private void OnRuntimeBump()
        {
            if (UiSounds.Enabled) _clapDetector?.SuppressFor(0.5);
            UiSounds.Bonk();
        }

        // ---------------------------------------------------------------- pen trail

        /// <summary>The robot "draws" its path on the mat in its current LED colour
        /// (a nod to Thymio's pen hole). Off LEDs draw a soft pencil grey.</summary>
        private void UpdateTrail()
        {
            if (!HasActor) return;
            var p = new Point3D(SrcX, SrcY, 0.07);
            if (_trailLast == null) { _trailLast = p; return; }
            if ((p - _trailLast.Value).Length < 0.09) return;

            var led = SrcLed;
            var c = (led.R == 0 && led.G == 0 && led.B == 0)
                ? Color.FromRgb(0x6B, 0x77, 0x7D)
                : led;

            if (_trailSeg == null || !SimilarColor(c, _trailColor) || _trailSeg.Points.Count > 500)
            {
                _trailSeg = new LinesVisual3D { Color = c, Thickness = 2.6 };
                _trailColor = c;
                _trail.Add(_trailSeg);
                View.Children.Add(_trailSeg);
                if (_trail.Count > 24)
                {
                    View.Children.Remove(_trail[0]);
                    _trail.RemoveAt(0);
                }
            }
            _trailSeg.Points.Add(_trailLast.Value);
            _trailSeg.Points.Add(p);
            _trailLast = p;
        }

        private static bool SimilarColor(Color a, Color b) =>
            Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 36;

        private void ClearTrail()
        {
            foreach (var t in _trail) View.Children.Remove(t);
            _trail.Clear();
            _trailSeg = null;
            _trailLast = null;
        }

        // ---------------------------------------------------------------- disco mode

        /// <summary>🌙 dims the whole playground so the LED glow, light shows and the
        /// pen trail pop — the kids' "disco mode".</summary>
        private void OnToggleDisco(object sender, RoutedEventArgs e)
        {
            _disco = !_disco;
            DiscoGlyph.Opacity = _disco ? 1.0 : 0.55;
            if (_disco)
            {
                SceneAmbient.Color = Color.FromRgb(0x16, 0x18, 0x20);
                SceneKey.Color = Color.FromRgb(0x2A, 0x2C, 0x36);
                SceneFill.Color = Color.FromRgb(0x12, 0x14, 0x1B);
                SkyTop.Color = Color.FromRgb(0x03, 0x04, 0x0A);
                SkyMid.Color = Color.FromRgb(0x06, 0x09, 0x13);
                SkyLow.Color = Color.FromRgb(0x08, 0x0E, 0x1A);
            }
            else
            {
                SceneAmbient.Color = Color.FromRgb(0x5A, 0x5F, 0x66);
                SceneKey.Color = Color.FromRgb(0xE8, 0xE8, 0xE8);
                SceneFill.Color = Color.FromRgb(0x6A, 0x70, 0x77);
                SkyTop.Color = Color.FromRgb(0x0B, 0x1B, 0x33);
                SkyMid.Color = Color.FromRgb(0x14, 0x33, 0x4A);
                SkyLow.Color = Color.FromRgb(0x1B, 0x4A, 0x50);
            }
        }

        // ---------------------------------------------------------------- chase camera

        private void OnToggleFollow(object sender, RoutedEventArgs e)
        {
            _followCam = !_followCam;
            FollowGlyph.Opacity = _followCam ? 1.0 : 0.55;
            if (!_followCam && View.Camera is System.Windows.Media.Media3D.PerspectiveCamera cam)
            {
                cam.Position = new Point3D(0, -27, 21);
                cam.LookDirection = new Vector3D(0, 27, -19);
                cam.UpDirection = new Vector3D(0, 0, 1);
            }
        }

        private void UpdateChaseCamera()
        {
            if (!_followCam || !HasActor) return;
            if (!(View.Camera is System.Windows.Media.Media3D.PerspectiveCamera cam)) return;
            double yaw = SrcYaw * Math.PI / 180.0;
            var fwd = new Vector3D(-Math.Sin(yaw), Math.Cos(yaw), 0);
            var target = new Point3D(SrcX, SrcY, 0.7);
            var pos = target - fwd * 9.5 + new Vector3D(0, 0, 6.0);
            cam.Position = pos;
            cam.LookDirection = target - pos;
            cam.UpDirection = new Vector3D(0, 0, 1);
        }

        // ---------------------------------------------------------------- confetti

        private static readonly Color[] ConfettiColors =
        {
            Color.FromRgb(0xF2, 0xC9, 0x4C), Color.FromRgb(0xEB, 0x57, 0x57),
            Color.FromRgb(0x2D, 0x9C, 0xDB), Color.FromRgb(0x37, 0xD6, 0x7A),
            Color.FromRgb(0xFF, 0x8A, 0xE2), Color.FromRgb(0x2B, 0xC0, 0xE8)
        };

        private void ThrowConfetti()
        {
            var rnd = new Random();
            double w = Math.Max(200, ConfettiLayer.ActualWidth);
            double h = Math.Max(200, ConfettiLayer.ActualHeight);
            for (int i = 0; i < 42; i++)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = 7 + rnd.Next(6),
                    Height = 11 + rnd.Next(7),
                    Fill = new SolidColorBrush(ConfettiColors[rnd.Next(ConfettiColors.Length)]),
                    RadiusX = 2, RadiusY = 2,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                var spin = new System.Windows.Media.RotateTransform(rnd.Next(360));
                rect.RenderTransform = spin;
                Canvas.SetLeft(rect, rnd.NextDouble() * w);
                Canvas.SetTop(rect, -24 - rnd.NextDouble() * 60);
                ConfettiLayer.Children.Add(rect);

                double dur = 1.7 + rnd.NextDouble() * 1.2;
                var fall = new DoubleAnimation(Canvas.GetTop(rect), h + 30, TimeSpan.FromSeconds(dur))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var rot = new DoubleAnimation(spin.Angle, spin.Angle + (rnd.Next(2) == 0 ? 1 : -1) * (240 + rnd.Next(360)),
                                              TimeSpan.FromSeconds(dur));
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(dur)) { BeginTime = TimeSpan.FromSeconds(dur * 0.55) };
                var captured = rect;
                fall.Completed += (_, __) => ConfettiLayer.Children.Remove(captured);
                rect.BeginAnimation(Canvas.TopProperty, fall);
                spin.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rot);
                rect.BeginAnimation(OpacityProperty, fade);
            }
        }

        // ------------------------------------------------------------------ missions

        private void CheckMission(double dt)
        {
            if (_mission == null || _missionDone || _rt == null) return;

            bool done = false;
            switch (_mission.Goal)
            {
                case MissionGoal.GlowOnButton:
                    var c = _rt.CurrentLedColor();
                    done = _rt.AnyButtonDown && c.G > 140 && c.R < 110 && c.B < 110;
                    break;
                case MissionGoal.ReachFlag:
                    done = _rt.AtGoal;
                    break;
                case MissionGoal.StopAtWall:
                    if (_rt.Collided) { _stillTime = 0; break; }
                    if (_rt.FrontDetected && !_rt.IsDriving) _stillTime += dt;
                    else _stillTime = 0;
                    done = _stillTime >= 0.8;
                    break;
                case MissionGoal.CollectCoins:
                    done = _rt.CoinTaken.All(t => t);
                    break;
                case MissionGoal.RainbowParty:
                    done = _rt.AtGoal && _rt.ActiveAnim == LedAnim.Rainbow;
                    break;
                case MissionGoal.FollowLine:
                    // one lap = touch all three quarter checkpoints (either direction),
                    // then come back near the start pad
                    _lineTime += dt;
                    var cps = World.LineCheckpoints;
                    var rp = new Point(_rt.X, _rt.Y);
                    for (int i = 0; i < cps.Count && i < _lineVisited.Length; i++)
                        if (!_lineVisited[i] && (rp - cps[i]).Length < 2.0)
                        {
                            _lineVisited[i] = true;
                            if (UiSounds.Enabled) _clapDetector?.SuppressFor(0.3);
                            UiSounds.Blip();
                            MissionHint.Text = L(_mission.HintKey, "") + $"   ✓ {_lineVisited.Count(v => v)}/3";
                        }
                    done = _lineVisited.All(v => v) && _lineTime > 6 &&
                           (rp - World.StartPos).Length < 2.0;
                    break;
            }
            if (!done) return;

            _missionDone = true;
            MissionProgress.MarkDone(_mission.Id);
            if (UiSounds.Enabled) _clapDetector?.SuppressFor(1.6);
            UiSounds.Fanfare();
            ThrowConfetti();
            CelebrationHops();

            MissionBanner.Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x9A, 0x6A, 0x00));
            MissionTitle.Text = L("vplMisDone", "Mission complete!");
            MissionHint.Text = L("vplMisDoneHint", "CarthaBot is so proud of you!");
            MissionStar.Visibility = Visibility.Visible;
            var pop = new DoubleAnimationUsingKeyFrames();
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.35)), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.8 }));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
            StarScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            StarScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

            MissionCompleted?.Invoke(_mission);
        }

        /// <summary>Three happy hops when a mission completes.</summary>
        private void CelebrationHops()
        {
            if (_trans == null) return;
            var hops = new DoubleAnimationUsingKeyFrames();
            double t = 0;
            hops.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            foreach (double h in new[] { 0.8, 0.55, 0.3 })
            {
                hops.KeyFrames.Add(new EasingDoubleKeyFrame(h, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t + 0.18)),
                    new SineEase { EasingMode = EasingMode.EaseOut }));
                hops.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t + 0.4)),
                    new BounceEase { EasingMode = EasingMode.EaseOut, Bounces = 1, Bounciness = 4 }));
                t += 0.42;
            }
            hops.Completed += (_, __) => { _trans?.BeginAnimation(TranslateTransform3D.OffsetZProperty, null); if (_trans != null) _trans.OffsetZ = 0; };
            _trans.BeginAnimation(TranslateTransform3D.OffsetZProperty, hops);
        }

        // ------------------------------------------------------------------ D-pad

        private static ButtonDir DirOf(object sender) =>
            (ButtonDir)Enum.Parse(typeof(ButtonDir), (string)((FrameworkElement)sender).Tag);

        private void PadDown(object sender, MouseButtonEventArgs e) => _rt?.ButtonDown(DirOf(sender));
        private void PadUp(object sender, MouseButtonEventArgs e) => _rt?.ButtonUp(DirOf(sender));
        private void PadLeave(object sender, MouseEventArgs e) => _rt?.ButtonUp(DirOf(sender));
    }
}
