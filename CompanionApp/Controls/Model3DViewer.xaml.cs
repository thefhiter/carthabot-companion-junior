using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace CompanionApp.Controls
{
    /// <summary>
    /// Lightweight 3D model viewer (orbit / zoom) for showing models authored in Blender
    /// and exported as .obj / .stl / .3ds / .ply. Set <see cref="Source"/> to a file path.
    /// Set <see cref="AutoRotate"/> to spin the model slowly around its up (Z) axis.
    /// </summary>
    public partial class Model3DViewer : UserControl
    {
        public Model3DViewer()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(string), typeof(Model3DViewer),
                new PropertyMetadata(null, OnSourceChanged));

        /// <summary>Path to a 3D model file (.obj/.stl/.3ds/.ply).</summary>
        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty AutoRotateProperty =
            DependencyProperty.Register(nameof(AutoRotate), typeof(bool), typeof(Model3DViewer),
                new PropertyMetadata(false, OnAutoRotateChanged));

        /// <summary>When true, the loaded model spins slowly around its up (Z) axis.</summary>
        public bool AutoRotate
        {
            get => (bool)GetValue(AutoRotateProperty);
            set => SetValue(AutoRotateProperty, value);
        }

        /// <summary>Seconds for one full turntable revolution (default 12).</summary>
        public double RotateSeconds { get; set; } = 12.0;

        private AxisAngleRotation3D _spin;

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((Model3DViewer)d).Load(e.NewValue as string);

        private static void OnAutoRotateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (Model3DViewer)d;
            if ((bool)e.NewValue) v.StartSpin(); else v.StopSpin();
        }

        public void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                ModelHost.Content = null;
                return;
            }

            try
            {
                Model3DGroup model = new ModelImporter().Load(path);
                ModelHost.Content = model;
                if (AutoRotate) StartSpin();
                View.ZoomExtents();
            }
            catch (Exception ex)
            {
                ModelHost.Content = null;
                System.Diagnostics.Debug.WriteLine("Model3DViewer load failed: " + ex.Message);
            }
        }

        private void StartSpin()
        {
            if (ModelHost.Content == null) return;

            // Spin around the up axis (HelixViewport3D defaults to Z-up).
            _spin = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
            ModelHost.Transform = new RotateTransform3D(_spin);

            var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(RotateSeconds)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _spin.BeginAnimation(AxisAngleRotation3D.AngleProperty, anim);
        }

        private void StopSpin()
        {
            if (_spin != null)
            {
                _spin.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
                _spin = null;
            }
            ModelHost.Transform = Transform3D.Identity;
        }
    }
}
