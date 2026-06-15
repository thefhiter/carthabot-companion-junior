using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using CarthaBotVPL.Models;
using HelixToolkit.Wpf;

namespace CompanionApp.Controls
{
    public enum MiniBotMode { Idle, Move, Glow }

    /// <summary>
    /// A tiny, non-interactive live 3D CarthaBot shown inside a VPL action card.
    /// In Move mode it drives/turns to match the chosen direction; in Glow mode it
    /// lights up the chosen LED colour. One frozen mesh is shared across all cards;
    /// each card animates only its own transform.
    /// </summary>
    public partial class MiniBot3D : UserControl
    {
        private static Model3DGroup _sharedModel;

        private TranslateTransform3D _trans;
        private AxisAngleRotation3D _yaw;
        private SolidColorBrush _haloBrush;
        private AxisAngleRotation3D _wheelRot;   // rolling wheels on this card's own model copy

        public MiniBot3D()
        {
            InitializeComponent();
            BuildHalo();
            LoadRobot();
            Loaded += (_, __) => UpdateVisual();
        }

        // ---------- dependency properties ----------

        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(nameof(Mode), typeof(MiniBotMode), typeof(MiniBot3D),
                new PropertyMetadata(MiniBotMode.Idle, OnAnyChanged));

        public MiniBotMode Mode { get => (MiniBotMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(nameof(Direction), typeof(MoveDir), typeof(MiniBot3D),
                new PropertyMetadata(MoveDir.Forward, OnAnyChanged));

        public MoveDir Direction { get => (MoveDir)GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.Register(nameof(GlowColor), typeof(Color), typeof(MiniBot3D),
                new PropertyMetadata(Colors.Black, OnAnyChanged));

        public Color GlowColor { get => (Color)GetValue(GlowColorProperty); set => SetValue(GlowColorProperty, value); }

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((MiniBot3D)d).UpdateVisual();

        // ---------- model + halo ----------

        private static Model3DGroup SharedModel()
        {
            if (_sharedModel == null)
            {
                try
                {
                    string p = Path.Combine(AppContext.BaseDirectory, "Vpl", "Assets", "carthabot_robot.obj");
                    if (File.Exists(p))
                    {
                        var m = new ModelImporter().Load(p);
                        if (m.CanFreeze) m.Freeze();   // frozen → safe to share across viewports
                        _sharedModel = m;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("MiniBot3D load: " + ex.Message); }
            }
            return _sharedModel;
        }

        private void LoadRobot()
        {
            var model = SharedModel();
            if (model == null) return;
            _yaw = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
            _trans = new TranslateTransform3D(0, 0, 0);
            var grp = new Transform3DGroup();
            // The Blender OBJ exporter writes the scene rotated 180° about Z; undo it
            // first so the robot's face looks at the camera and +Y is "forward".
            grp.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 180)));
            grp.Children.Add(new RotateTransform3D(_yaw));
            grp.Children.Add(_trans);
            // each card gets its own copy so its wheels can roll independently
            var copy = (Model3DGroup)model.Clone();
            RigWheels(copy);
            RobotHost.Content = copy;
            RobotHost.Transform = grp;
            Halo.Transform = grp;
        }

        /// <summary>Same wheel-finding heuristic as the simulator: wheel parts span both
        /// sides in X but are one wheel-diameter small in Y.</summary>
        private void RigWheels(Model3DGroup grp)
        {
            var wheels = new System.Collections.Generic.List<GeometryModel3D>();
            var b = Rect3D.Empty;
            foreach (var child in grp.Children)
            {
                if (!(child is GeometryModel3D g)) continue;
                var cb = g.Bounds;
                if (cb.SizeX > 2.0 && cb.SizeY < 1.5 && Math.Abs(cb.Y + cb.SizeY / 2) < 1.0)
                {
                    wheels.Add(g);
                    b.Union(cb);
                }
            }
            if (wheels.Count == 0) return;
            _wheelRot = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            var spin = new RotateTransform3D(_wheelRot, new Point3D(0, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2));
            foreach (var w in wheels) w.Transform = spin;
        }

        private void BuildHalo()
        {
            var mesh = new MeshGeometry3D();
            const int seg = 28;
            const double r = 2.05, z = 0.05;   // wider than the shell so the glow ring shows
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
                mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1);
            }
            _haloBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            var mat = new MaterialGroup();
            mat.Children.Add(new DiffuseMaterial(_haloBrush));
            mat.Children.Add(new EmissiveMaterial(_haloBrush));
            Halo.Content = new GeometryModel3D(mesh, mat);
        }

        // ---------- visual state ----------

        private void ClearAnims()
        {
            _trans?.BeginAnimation(TranslateTransform3D.OffsetXProperty, null);
            _trans?.BeginAnimation(TranslateTransform3D.OffsetYProperty, null);
            _trans?.BeginAnimation(TranslateTransform3D.OffsetZProperty, null);
            _yaw?.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
            _wheelRot?.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
            _haloBrush?.BeginAnimation(SolidColorBrush.ColorProperty, null);
        }

        private void SpinWheels(double degreesPerLoop, double seconds)
        {
            _wheelRot?.BeginAnimation(AxisAngleRotation3D.AngleProperty,
                new DoubleAnimation(0, degreesPerLoop, new Duration(TimeSpan.FromSeconds(seconds)))
                { RepeatBehavior = RepeatBehavior.Forever });
        }

        // gentle left-right sway, like a robot wiggling along a line
        private void Sway()
        {
            _yaw?.BeginAnimation(AxisAngleRotation3D.AngleProperty,
                new DoubleAnimation(-9, 9, new Duration(TimeSpan.FromSeconds(0.7)))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                  EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        private void UpdateVisual()
        {
            if (_trans == null || _yaw == null) return;
            ClearAnims();
            _trans.OffsetX = 0; _trans.OffsetY = 0; _trans.OffsetZ = 0; _yaw.Angle = 0;

            if (Mode == MiniBotMode.Glow)
            {
                if (KeyLight != null) KeyLight.Color = Lighten(GlowColor);
                _haloBrush.Color = Color.FromArgb(255, GlowColor.R, GlowColor.G, GlowColor.B);
                Bob(0.10, 1.6);   // gentle idle
                return;
            }

            // Move / Idle modes use white light and no glow
            if (KeyLight != null) KeyLight.Color = Colors.White;
            _haloBrush.Color = Color.FromArgb(0, 0, 0, 0);

            if (Mode == MiniBotMode.Move)
            {
                switch (Direction)
                {
                    case MoveDir.Forward:  Drive(0.7); SpinWheels(-360, 1.4); break;
                    case MoveDir.Backward: Drive(-0.7); SpinWheels(360, 1.4); break;
                    case MoveDir.Left:     Spin(360); break;   // continuous left turn
                    case MoveDir.Right:    Spin(-360); break;  // continuous right turn
                    case MoveDir.FollowLine: Drive(0.55); SpinWheels(-360, 1.4); Sway(); break;
                    default:               Bob(0.08, 1.8); break; // Stop = idle
                }
            }
            else
            {
                Bob(0.08, 1.8);
            }
        }

        private static Color Lighten(Color c)
        {
            // blend 35% toward white so the body reads as the colour, not too dark
            byte M(byte v) => (byte)(v + (255 - v) * 0.35);
            return Color.FromRgb(M(c.R), M(c.G), M(c.B));
        }

        private void Drive(double dist)
        {
            var a = new DoubleAnimation(0, dist, new Duration(TimeSpan.FromSeconds(0.85)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            _trans.BeginAnimation(TranslateTransform3D.OffsetYProperty, a);
        }

        private void Spin(double degrees)
        {
            var a = new DoubleAnimation(0, degrees, new Duration(TimeSpan.FromSeconds(3.4)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _yaw.BeginAnimation(AxisAngleRotation3D.AngleProperty, a);
        }

        private void Bob(double height, double seconds)
        {
            var a = new DoubleAnimation(0, height, new Duration(TimeSpan.FromSeconds(seconds)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            _trans.BeginAnimation(TranslateTransform3D.OffsetZProperty, a);
        }
    }
}
