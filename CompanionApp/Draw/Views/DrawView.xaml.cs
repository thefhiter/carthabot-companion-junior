using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CarthaBotVPL.Services;          // ConnectionMode
using CompanionApp.Draw.Services;
using CompanionApp.Draw.ViewModels;
using Prism.Events;

namespace CompanionApp.Draw.Views
{
    /// <summary>
    /// "Draw with a pen" studio. The child sketches on the InkCanvas (or stamps a shape, which is
    /// added as a stroke); pressing ▶ hands every stroke to the view-model, which compiles them into
    /// a turtle-plotter program and streams it to the CarthaBot.
    /// </summary>
    public partial class DrawView : UserControl
    {
        private readonly DrawViewModel _vm;

        public DrawView(IEventAggregator eventAggregator, List<string> oldComs)
            : this(eventAggregator, oldComs, ConnectionMode.Usb, null) { }

        // The pen is FIXED DOWN — the robot can't lift it to jump between strokes. So by default the
        // drawing is ONE continuous line: when the child lifts the mouse the line is finished, and a
        // new scribble replaces it (no "jump" lines). Advanced users can turn this off.
        private bool _oneLineMode = true;
        private bool _previewOn = true;   // kids see exactly what CarthaBot will draw, from the start

        public DrawView(IEventAggregator eventAggregator, List<string> oldComs, ConnectionMode mode, string param)
        {
            InitializeComponent();
            _vm = new DrawViewModel(eventAggregator, oldComs, mode, param);
            DataContext = _vm;

            // a chunky, kid-friendly black pen
            InkPad.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = Colors.Black,
                Width = 4,
                Height = 4,
                FitToCurve = true,
                IgnorePressure = true
            };

            // one-line rule: as soon as a new continuous scribble is finished, drop the older ones
            InkPad.StrokeCollected += OnStrokeCollected;

            // keep the live plan + preview in sync as the child draws / edits / tunes
            InkPad.Strokes.StrokesChanged += (_, __) => Refresh();
            Loaded += (_, __) => { if (PreviewBtn != null) PreviewBtn.Content = L("drawPreviewOn", "👁 Preview ✓"); DrawPreview(); };
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DrawViewModel.SizeMm) ||
                    e.PropertyName == nameof(DrawViewModel.MmPerSec) ||
                    e.PropertyName == nameof(DrawViewModel.TrackMm) ||
                    e.PropertyName == nameof(DrawViewModel.StartHeading))
                    Refresh();
            };
            SizeChanged += (_, __) => { if (_previewOn) DrawPreview(); };
            PreviewKeyDown += OnKey;
            Unloaded += (_, __) => StopAirDraw();   // release the webcam when the child leaves the studio
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (!ctrl) return;
            if (e.Key == Key.Z) { OnUndo(null, null); e.Handled = true; }
            else if (e.Key == Key.S) { OnSaveDrawing(null, null); e.Handled = true; }
            else if (e.Key == Key.Enter) { OnSend(null, null); e.Handled = true; }
        }

        private List<IReadOnlyList<Point>> CurrentStrokes() => InkPad.Strokes
            .Select(st => (IReadOnlyList<Point>)st.StylusPoints.Select(p => new Point(p.X, p.Y)).ToList())
            .ToList();

        private void Refresh()
        {
            _vm.UpdatePlan(CurrentStrokes());
            if (_previewOn) DrawPreview();
        }

        // ----- planned-path overlay: the exact polyline the robot will trace -----
        private void DrawPreview()
        {
            PreviewLayer.Children.Clear();
            if (!_previewOn) return;

            var path = DrawCompiler.SimplifiedCanvasPath(CurrentStrokes(), _vm.BuildSettings());
            if (path.Count < 2) return;

            var poly = new System.Windows.Shapes.Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xE8, 0x43, 0x93)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Points = new PointCollection(path)
            };
            PreviewLayer.Children.Add(poly);

            // green start dot (where the robot's pen begins)
            AddDot(path[0], Color.FromRgb(0x2E, 0xCC, 0x71), 9);
            // small dots at each "stop" vertex so kids see where it pauses/turns
            for (int i = 1; i < path.Count - 1; i++)
                AddDot(path[i], Color.FromRgb(0xEE, 0x7B, 0x2F), 4);
        }

        private void AddDot(Point p, Color c, double size)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = size, Height = size, Fill = new SolidColorBrush(c) };
            Canvas.SetLeft(dot, p.X - size / 2);
            Canvas.SetTop(dot, p.Y - size / 2);
            PreviewLayer.Children.Add(dot);
        }

        private void OnTogglePreview(object sender, RoutedEventArgs e)
        {
            _previewOn = !_previewOn;
            PreviewBtn.Content = _previewOn ? L("drawPreviewOn", "👁 Preview ✓") : L("drawPreview", "👁 Preview");
            DrawPreview();
        }

        private void OnToggleCode(object sender, RoutedEventArgs e) => _vm.ToggleCode(CurrentStrokes());

        private void OnCopyCode(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_vm.GeneratedCode ?? ""); _vm.Status = "Copied the MicroPython to the clipboard."; }
            catch (Exception ex) { _vm.Status = "Couldn't copy: " + ex.Message; }
        }

        private void OnSaveCode(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_vm.GeneratedCode)) { _vm.Status = "No code yet — draw something first."; return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MicroPython (*.py)|*.py",
                FileName = "carthabot-drawing.py",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) return;
            try { System.IO.File.WriteAllText(dlg.FileName, _vm.GeneratedCode); _vm.Status = "Saved " + dlg.FileName; }
            catch (Exception ex) { _vm.Status = "Couldn't save: " + ex.Message; }
        }

        // ----- shapes are stamped onto the canvas as a stroke, then sent like any other ink -----
        private void OnStampShape(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.Tag is string tag) ||
                !Enum.TryParse(tag, out DrawCompiler.Shape shape))
                return;

            double w = InkPad.ActualWidth, h = InkPad.ActualHeight;
            if (w < 10 || h < 10) return;

            // build the shape in "pixel" units (reuse the mm path math), then place it on the canvas
            double sizePx = Math.Min(w, h) * 0.7;
            var paper = DrawCompiler.ShapePath(shape, new DrawSettings { SizeMm = sizePx });
            double cx = w / 2, cy = h / 2;

            var sp = new StylusPointCollection(
                paper.Select(p => new StylusPoint(cx + p.X, cy - p.Y)));   // flip Y back to canvas space
            if (sp.Count < 2) return;

            if (_oneLineMode) InkPad.Strokes.Clear();   // one shape = the whole one-line drawing
            InkPad.Strokes.Add(new Stroke(sp)
            {
                DrawingAttributes = new DrawingAttributes { Color = Colors.Black, Width = 4, Height = 4 }
            });
            _vm.Status = _oneLineMode
                ? $"Stamped a {shape.ToString().ToLower()}. Press ▶ to draw it!"
                : $"Stamped a {shape.ToString().ToLower()}. Add more, or press ▶ to draw it.";
        }

        private void OnUndo(object sender, RoutedEventArgs e)
        {
            if (InkPad.Strokes.Count > 0)
                InkPad.Strokes.RemoveAt(InkPad.Strokes.Count - 1);
        }

        private void OnClear(object sender, RoutedEventArgs e) => InkPad.Strokes.Clear();

        // The child finished a freehand scribble (lifted the mouse). In one-line mode that scribble IS
        // the whole drawing, so we drop everything else — the fixed pen can't jump to a second line.
        private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            if (!_oneLineMode) return;
            for (int i = InkPad.Strokes.Count - 1; i >= 0; i--)
                if (!ReferenceEquals(InkPad.Strokes[i], e.Stroke))
                    InkPad.Strokes.RemoveAt(i);
        }

        private void OnToggleOneLine(object sender, RoutedEventArgs e)
        {
            _oneLineMode = !_oneLineMode;
            OneLineBtn.Content = _oneLineMode ? "✏️ One line: ON" : "✏️ One line: off";
            // turning it back on collapses any existing multi-stroke drawing to just the last line
            if (_oneLineMode && InkPad.Strokes.Count > 1)
            {
                var last = InkPad.Strokes[InkPad.Strokes.Count - 1];
                InkPad.Strokes.Clear();
                InkPad.Strokes.Add(last);
            }
            _vm.Status = _oneLineMode
                ? "One-line mode: draw your picture without lifting — the pen can't jump. ✏️"
                : "Free mode: multiple strokes are joined by thin 'travel' lines (the pen stays down).";
        }

        private static string L(string key, string fallback) =>
            Application.Current?.TryFindResource(key) as string ?? fallback;

        private void OnToggleErase(object sender, RoutedEventArgs e)
        {
            bool erasing = InkPad.EditingMode != InkCanvasEditingMode.EraseByStroke;
            InkPad.EditingMode = erasing ? InkCanvasEditingMode.EraseByStroke : InkCanvasEditingMode.Ink;
            EraseBtn.Content = erasing ? L("drawEraseOn", "🧽  Erase: ON") : L("drawEraseOff", "🧽  Erase: off");
        }

        // ----- type a word; the robot writes it with a single-stroke vector font -----
        private void OnStampText(object sender, RoutedEventArgs e)
        {
            string word = (TextInput.Text ?? "").Trim();
            if (word.Length == 0) { _vm.Status = "Type a word (A–Z, 0–9) first."; return; }

            double w = InkPad.ActualWidth, h = InkPad.ActualHeight;
            if (w < 10 || h < 10) return;

            var glyphs = DrawText.Layout(word, out double totalW);
            if (glyphs.Count == 0) { _vm.Status = "I can only write letters A–Z and digits 0–9."; return; }

            // fit the word to ~85% width / ~30% height of the canvas, centred
            double scale = Math.Min(w * 0.85 / totalW, h * 0.30 / DrawText.Height);
            double drawnW = totalW * scale, drawnH = DrawText.Height * scale;
            double offX = (w - drawnW) / 2;
            double baseY = (h + drawnH) / 2;   // baseline; glyph y grows up, canvas y grows down

            if (_oneLineMode) InkPad.Strokes.Clear();   // the word becomes the drawing
            foreach (var g in glyphs)
            {
                var sp = new StylusPointCollection(g.Select(p => new StylusPoint(offX + p.X * scale, baseY - p.Y * scale)));
                if (sp.Count >= 2)
                    InkPad.Strokes.Add(new Stroke(sp) { DrawingAttributes = new DrawingAttributes { Color = Colors.Black, Width = 4, Height = 4 } });
            }
            _vm.Status = $"Wrote \"{word.ToUpper()}\". Press ▶ to draw it (letters may join with a thin line — the pen can't lift).";
        }

        // ----- save / open drawings as InkCanvas ISF -----
        private void OnSaveDrawing(object sender, RoutedEventArgs e)
        {
            if (InkPad.Strokes.Count == 0) { _vm.Status = "Nothing to save yet."; return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CarthaBot drawing (*.isf)|*.isf",
                FileName = "my-drawing.isf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                using (var fs = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Create))
                    InkPad.Strokes.Save(fs);
                _vm.Status = "Saved " + dlg.FileName;
            }
            catch (Exception ex) { _vm.Status = "Couldn't save: " + ex.Message; }
        }

        private void OnOpenDrawing(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CarthaBot drawing (*.isf)|*.isf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                StrokeCollection loaded;
                using (var fs = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Open))
                    loaded = new StrokeCollection(fs);
                InkPad.Strokes.Clear();           // keep the same collection so StrokesChanged stays wired
                InkPad.Strokes.Add(loaded);
                _vm.Status = "Opened " + dlg.FileName;
            }
            catch (Exception ex) { _vm.Status = "Couldn't open: " + ex.Message; }
        }

        // ============================================================================================
        //  AIR DRAW — trace a shape in the air with your hand in front of the webcam
        // ============================================================================================
        // The child locks the camera onto their hand, then PINCHES two fingers together (thumb + finger,
        // making a little ring) to put the pen DOWN and opens them to lift it — just like grabbing a pen.
        // Each pinch draws one stroke; every stroke is recorded in 640x480 camera space and, on "Use it",
        // fitted onto the ink canvas so the existing preview → compile → send pipeline draws it.
        // A "Hold to draw" button is a fallback for when the pinch is hard to see.

        private HandTracker _tracker;
        private DispatcherTimer _camTimer;
        private WriteableBitmap _camBitmap;
        private Ellipse _pointerDot;

        private readonly List<List<Point>> _airSegments = new List<List<Point>>();  // one list per pinch-stroke
        private readonly List<Polyline> _trailLines = new List<Polyline>();          // matching overlay visuals
        private List<Point> _currentSeg;
        private Polyline _currentLine;

        private bool _airCalibrated;
        private bool _holdPressed;      // the fallback "Hold to draw" button is held down
        private bool _penWasDown;       // edge detection for pinch/hold transitions
        private bool _hasFirstFrame;

        private void OnOpenAirDraw(object sender, RoutedEventArgs e)
        {
            AirOverlay.Visibility = Visibility.Visible;
            EnsureAirVisuals();
            ResetAirTrail();
            _airCalibrated = false;
            _hasFirstFrame = false;
            CamError.Visibility = Visibility.Collapsed;

            _tracker = new HandTracker();
            _tracker.Start();   // opens the camera on a background thread; OnCamTick watches for failure

            SetAirButtons(canLock: true, canHold: false, canAgain: false, canUse: false);
            AirLockBtn.Content = L("drawAirLock", "🎯 Lock on my hand");
            AirHint.Text = L("drawAirHintStart", "Starting the camera…");

            _camTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };  // ~30 fps
            _camTimer.Tick += OnCamTick;
            _camTimer.Start();
        }

        private void EnsureAirVisuals()
        {
            if (_pointerDot == null)
            {
                _pointerDot = new Ellipse
                {
                    Width = 26,
                    Height = 26,
                    Stroke = Brushes.White,
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(0x88, 0xE8, 0x43, 0x93)),
                    Visibility = Visibility.Collapsed
                };
                CamOverlay.Children.Add(_pointerDot);   // stays on top: added last, after any trail lines
            }
        }

        private void OnCamTick(object sender, EventArgs e)
        {
            if (_tracker == null) return;
            if (_tracker.OpenFailed)
            {
                CamError.Text = _tracker.LastError ?? L("drawAirNoCam", "No camera found. Plug in a webcam and try again.");
                CamError.Visibility = Visibility.Visible;
                try { _camTimer?.Stop(); } catch { }
                SetAirButtons(canLock: false, canHold: false, canAgain: false, canUse: false);
                return;
            }
            if (!_tracker.TryGetLatest(out byte[] bgr, out bool found, out double px, out double py,
                                       out bool calibrated, out bool pinched))
                return;

            // blit the frame into the reused WriteableBitmap
            if (_camBitmap == null)
            {
                _camBitmap = new WriteableBitmap(HandTracker.FrameWidth, HandTracker.FrameHeight, 96, 96,
                                                 PixelFormats.Bgr24, null);
                CamImage.Source = _camBitmap;
            }
            _camBitmap.WritePixels(new Int32Rect(0, 0, HandTracker.FrameWidth, HandTracker.FrameHeight),
                                   bgr, HandTracker.FrameWidth * 3, 0);

            if (!_hasFirstFrame)
            {
                _hasFirstFrame = true;
                if (!_airCalibrated)
                    AirHint.Text = L("drawAirHintLock", "Put your open hand in the green box, then press 🎯 Lock.");
            }

            bool penDown = false;
            if (calibrated && found)
            {
                penDown = pinched || _holdPressed;

                _pointerDot.Visibility = Visibility.Visible;
                SetDotPenDown(penDown);
                Canvas.SetLeft(_pointerDot, px - _pointerDot.Width / 2);
                Canvas.SetTop(_pointerDot, py - _pointerDot.Height / 2);

                if (penDown && !_penWasDown) StartSegment();
                if (penDown) AppendPoint(px, py);
            }
            else
            {
                _pointerDot.Visibility = Visibility.Collapsed;
            }

            if (!penDown && _penWasDown) EndSegment();          // released, or hand lost mid-stroke
            if (_airCalibrated && penDown != _penWasDown)
                AirHint.Text = penDown
                    ? L("drawAirHintDrawing", "✍️ Drawing! Keep your fingers pinched. Open them to lift the pen.")
                    : L("drawAirHintReady", "✌️ Pinch your fingers together to draw, open them to stop.");
            _penWasDown = penDown;
        }

        private void StartSegment()
        {
            _currentSeg = new List<Point>();
            _airSegments.Add(_currentSeg);
            _currentLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xE8, 0x43, 0x93)),
                StrokeThickness = 4,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _trailLines.Add(_currentLine);
            CamOverlay.Children.Insert(0, _currentLine);   // under the pointer dot
        }

        private void AppendPoint(double px, double py)
        {
            if (_currentSeg == null) return;
            var p = new Point(px, py);
            if (_currentSeg.Count == 0 || (p - _currentSeg[_currentSeg.Count - 1]).Length >= 3.0)
            {
                _currentSeg.Add(p);
                _currentLine.Points.Add(p);
                if (TotalTrailPoints() > 6000) EndSegment();   // safety cap
            }
        }

        private void EndSegment()
        {
            // drop a stray one-point tap so it doesn't count as a stroke
            if (_currentSeg != null && _currentSeg.Count < 2 && _airSegments.Count > 0)
            {
                _airSegments.RemoveAt(_airSegments.Count - 1);
                if (_currentLine != null) { CamOverlay.Children.Remove(_currentLine); _trailLines.Remove(_currentLine); }
            }
            _currentSeg = null;
            _currentLine = null;
            UpdateAirButtonsForTrail();
        }

        private int TotalTrailPoints()
        {
            int n = 0;
            foreach (var s in _airSegments) n += s.Count;
            return n;
        }

        private void SetDotPenDown(bool down)
        {
            if (down)
            {
                _pointerDot.Width = _pointerDot.Height = 30;
                _pointerDot.Fill = new SolidColorBrush(Color.FromArgb(0xCC, 0x2E, 0xCC, 0x71));  // solid green = pen down
            }
            else
            {
                _pointerDot.Width = _pointerDot.Height = 26;
                _pointerDot.Fill = new SolidColorBrush(Color.FromArgb(0x66, 0xE8, 0x43, 0x93));  // faint = aiming
            }
        }

        private void OnAirLock(object sender, RoutedEventArgs e)
        {
            if (_tracker == null) return;
            _tracker.RequestCalibration();
            _airCalibrated = true;
            ResetAirTrail();
            AirLockBtn.Content = L("drawAirRelock", "🔄 Re-lock");
            SetAirButtons(canLock: true, canHold: true, canAgain: false, canUse: false);
            AirHint.Text = L("drawAirHintReady", "✌️ Pinch your fingers together to draw, open them to stop.");
        }

        // fallback for when the pinch is hard to detect: hold this button to force the pen down
        private void OnAirHoldDown(object sender, MouseButtonEventArgs e)
        {
            if (!_airCalibrated) return;
            _holdPressed = true;
        }

        private void OnAirHoldUp(object sender, MouseEventArgs e) => _holdPressed = false;

        private void OnAirAgain(object sender, RoutedEventArgs e)
        {
            ResetAirTrail();
            SetAirButtons(canLock: true, canHold: true, canAgain: false, canUse: false);
            AirHint.Text = L("drawAirHintReady", "✌️ Pinch your fingers together to draw, open them to stop.");
        }

        private void OnAirUse(object sender, RoutedEventArgs e)
        {
            var stroke = BuildStrokeFromSegments();
            if (stroke == null)
            {
                _vm.Status = L("drawAirTooSmall", "That drawing was too small — try again with a bigger motion.");
                return;
            }
            InkPad.Strokes.Clear();               // the air drawing IS the one-line picture
            InkPad.Strokes.Add(stroke);           // StrokesChanged → live preview + plan refresh
            _vm.Status = L("drawAirAdded", "✋ Added your air drawing! Press ▶ to send it to CarthaBot.");
            StopAirDraw();
        }

        // Fit every recorded pinch-stroke into the ink canvas (shared bbox → aspect + relative position
        // preserved) and concatenate them into ONE ink stroke; the fixed pen joins the strokes with thin
        // travel lines, exactly as it does for stamped shapes.
        private Stroke BuildStrokeFromSegments()
        {
            double w = InkPad.ActualWidth, h = InkPad.ActualHeight;
            if (w < 10 || h < 10) return null;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            int total = 0;
            foreach (var seg in _airSegments)
                foreach (var p in seg)
                {
                    total++;
                    if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
                }
            if (total < 8) return null;

            double bw = maxX - minX, bh = maxY - minY;
            if (bw < 12 && bh < 12) return null;   // barely moved

            double margin = 0.82;                  // leave a border so the drawing isn't edge-to-edge
            double scale = Math.Min(w * margin / Math.Max(bw, 1), h * margin / Math.Max(bh, 1));
            double offX = (w - bw * scale) / 2, offY = (h - bh * scale) / 2;

            var sp = new StylusPointCollection();
            foreach (var seg in _airSegments)
                foreach (var p in seg)
                    sp.Add(new StylusPoint(offX + (p.X - minX) * scale, offY + (p.Y - minY) * scale));
            if (sp.Count < 2) return null;

            return new Stroke(sp)
            {
                DrawingAttributes = new DrawingAttributes { Color = Colors.Black, Width = 4, Height = 4, FitToCurve = true }
            };
        }

        private void ResetAirTrail()
        {
            foreach (var line in _trailLines) CamOverlay.Children.Remove(line);
            _trailLines.Clear();
            _airSegments.Clear();
            _currentSeg = null;
            _currentLine = null;
            _penWasDown = false;
            _holdPressed = false;
        }

        private void UpdateAirButtonsForTrail()
        {
            int total = TotalTrailPoints();
            AirAgainBtn.IsEnabled = total > 0;
            AirUseBtn.IsEnabled = total >= 8;
        }

        private void SetAirButtons(bool canLock, bool canHold, bool canAgain, bool canUse)
        {
            AirLockBtn.IsEnabled = canLock;
            AirHoldBtn.IsEnabled = canHold;
            AirAgainBtn.IsEnabled = canAgain;
            AirUseBtn.IsEnabled = canUse;
        }

        private void OnCloseAirDraw(object sender, RoutedEventArgs e) => StopAirDraw();

        private void StopAirDraw()
        {
            try { _camTimer?.Stop(); } catch { }
            if (_camTimer != null) { _camTimer.Tick -= OnCamTick; _camTimer = null; }
            try { _tracker?.Stop(); } catch { }
            _tracker = null;
            _holdPressed = false;
            _penWasDown = false;
            if (AirOverlay != null) AirOverlay.Visibility = Visibility.Collapsed;
        }

        private async void OnSend(object sender, RoutedEventArgs e)
        {
            // each stroke -> an ordered list of canvas points (Y down); the VM joins them into one path
            var strokes = InkPad.Strokes
                .Select(st => (IReadOnlyList<Point>)st.StylusPoints
                    .Select(p => new Point(p.X, p.Y)).ToList())
                .ToList();

            await _vm.SendInkAsync(strokes);
        }
    }
}
