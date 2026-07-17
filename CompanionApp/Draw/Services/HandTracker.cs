using System;
using System.Threading;
using OpenCvSharp;

namespace CompanionApp.Draw.Services
{
    /// <summary>
    /// Webcam-based "air drawing" tracker for the Draw studio. A child holds up their hand (or any
    /// brightly-coloured marker/toy) in front of the camera and moves it in the air; the tracker
    /// follows that pointer and reports its position so the View can record a trail and hand it to
    /// the robot like any other drawing.
    ///
    /// It is deliberately CLASSICAL computer vision (no ML model, no Python) so it ships inside the
    /// self-contained WPF app: the child parks their pointer in a target box for a moment, we sample
    /// its colour (works for ANY skin tone or object), then track the largest blob of that colour by
    /// hue. Hue tracking is robust to lighting changes, which matters in a classroom / living room.
    ///
    /// Capture + all image processing run on a background thread. The View pulls the latest processed
    /// frame (as raw BGR bytes) on the UI thread via <see cref="TryGetLatest"/> — no WPF types leak
    /// into this service, and the heavy work never touches the UI thread.
    /// </summary>
    public sealed class HandTracker : IDisposable
    {
        public const int FrameWidth = 640;
        public const int FrameHeight = 480;

        private VideoCapture _cap;
        private Thread _thread;
        private volatile bool _running;

        private readonly object _lock = new object();
        private byte[] _latest;                 // BGR24, FrameWidth*FrameHeight*3
        private bool _pointFound;
        private double _px, _py;                // smoothed pointer, in 640x480 frame space
        private bool _calibrated;
        private bool _pinched;                  // fingers pinched together (pen down)

        // calibration request + the locked-on colour (HSV)
        private volatile bool _calibrateRequested;
        private int _hueTarget, _satFloor, _valFloor;

        // exponential-moving-average smoothing of the pointer (kills hand jitter)
        private double _sx, _sy;
        private bool _hasSmoothed;

        private bool _pinchLatched;

        // Last valid color-blob bounding box — lets the ML model keep running for a few frames
        // even when the color blob drops out (e.g., hand overlaps background momentarily).
        private Rect _lastHandBbox;

        // ML hand-landmark model (21 joints). When it's confident we use its landmarks + gesture;
        // otherwise we fall back to the classical colour-blob fingertip detection.
        private HandLandmarker _landmarker;

        public string LastError { get; private set; }
        public bool IsRunning => _running;

        // The camera is opened ON the background thread (it can take a second or two), so the UI never
        // blocks. The View polls OpenFailed to show a "no camera" message if the open ultimately fails.
        private volatile bool _openFailed;
        public bool OpenFailed => _openFailed;

        /// <summary>Start the capture/tracking thread. Always returns true — the camera opens in the
        /// background; watch <see cref="OpenFailed"/> (and TryGetLatest) to know if it actually worked.</summary>
        public bool Start()
        {
            if (_running) return true;
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "HandTracker" };
            _thread.Start();
            return true;
        }

        // Find a webcam that actually delivers frames. We scan a few device indices per backend and
        // require a real frame to read before accepting one — a machine can have a phantom/disconnected
        // camera at index 0 that "opens" but never produces an image, so opening alone isn't enough.
        // MSMF is tried first (most reliable on modern Windows laptops), then DirectShow, then any.
        private static VideoCapture OpenCamera()
        {
            foreach (var api in new[] { VideoCaptureAPIs.MSMF, VideoCaptureAPIs.DSHOW, VideoCaptureAPIs.ANY })
            {
                for (int idx = 0; idx < 4; idx++)
                {
                    VideoCapture cap = null;
                    try
                    {
                        cap = new VideoCapture(idx, api);
                        if (cap.IsOpened() && DeliversFrame(cap)) return cap;
                        cap.Release();
                    }
                    catch { try { cap?.Release(); } catch { } }
                }
            }
            return null;
        }

        // Grab a few frames to confirm the device is live (auto-exposure may need a couple of reads).
        private static bool DeliversFrame(VideoCapture cap)
        {
            using (var m = new Mat())
                for (int t = 0; t < 10; t++)
                {
                    if (cap.Read(m) && !m.Empty()) return true;
                    Thread.Sleep(40);
                }
            return false;
        }

        /// <summary>Ask the tracker to sample whatever is inside the centre box on the next frame.</summary>
        public void RequestCalibration() => _calibrateRequested = true;

        /// <summary>Forget the locked colour (the pointer stops being tracked until re-calibrated).</summary>
        public void ResetCalibration()
        {
            lock (_lock) { _calibrated = false; _pointFound = false; _hasSmoothed = false; _pinchLatched = false; }
        }

        /// <summary>
        /// Copy the most recent processed frame + pointer for the UI to render. Returns false until the
        /// first frame arrives. <paramref name="bgr"/> is BGR24 of FrameWidth×FrameHeight.
        /// </summary>
        public bool TryGetLatest(out byte[] bgr, out bool pointFound, out double px, out double py,
                                 out bool calibrated, out bool pinched)
        {
            lock (_lock)
            {
                if (_latest == null)
                {
                    bgr = null; pointFound = false; px = py = 0; calibrated = false; pinched = false;
                    return false;
                }
                bgr = _latest;                 // the loop always allocates a fresh buffer, so sharing is safe
                pointFound = _pointFound;
                px = _px; py = _py;
                calibrated = _calibrated;
                pinched = _pinched;
                return true;
            }
        }

        private void Loop()
        {
            // open the webcam here (off the UI thread) so the studio opens instantly
            _cap = OpenCamera();
            if (_cap == null)
            {
                LastError = "No camera found. Plug in a webcam and try again.";
                _openFailed = true;
                _running = false;
                return;
            }
            try
            {
                _cap.Set(VideoCaptureProperties.FrameWidth, FrameWidth);
                _cap.Set(VideoCaptureProperties.FrameHeight, FrameHeight);
                _cap.Set(VideoCaptureProperties.BufferSize, 1);   // lowest latency (ignored by some drivers)
            }
            catch { /* property support varies by driver — we resize every frame anyway */ }

            try { _landmarker = new HandLandmarker(); } catch { _landmarker = null; }   // ML model (off the UI thread)

            using (var raw = new Mat())
            using (var frame = new Mat())
            using (var hsv = new Mat())
            using (var mask = new Mat())
            {
                var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(7, 7));
                var boxSize = new OpenCvSharp.Size(150, 150);
                var boxRect = new Rect((FrameWidth - boxSize.Width) / 2, (FrameHeight - boxSize.Height) / 2,
                                        boxSize.Width, boxSize.Height);

                while (_running)
                {
                    try
                    {
                        if (!_cap.Read(raw) || raw.Empty()) { Thread.Sleep(15); continue; }

                        // mirror (selfie view) + normalise size so all coordinates are 640x480
                        Cv2.Resize(raw, frame, new OpenCvSharp.Size(FrameWidth, FrameHeight));
                        Cv2.Flip(frame, frame, FlipMode.Y);
                        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

                        if (_calibrateRequested)
                        {
                            _calibrateRequested = false;
                            SampleColour(hsv, boxRect);
                        }

                        bool found = false, pinched = false;
                        double fx = 0, fy = 0;
                        OpenCvSharp.Point[] tips = null;
                        OpenCvSharp.Point handCentre = default;
                        OpenCvSharp.Point2f[] landmarks = null;
                        if (_calibrated)
                            found = TrackPointer(frame, hsv, mask, kernel, out fx, out fy, out pinched,
                                                 out tips, out handCentre, out landmarks);

                        // Color blob lost the hand — try ML on the last known position.
                        // Shape-based landmark model can often recover when colour briefly fails.
                        if (_calibrated && !found && _lastHandBbox.Width > 0
                            && _landmarker != null && _landmarker.Ready)
                        {
                            OpenCvSharp.Point2f[] lmFb; float cfFb;
                            if (_landmarker.Detect(frame, _lastHandBbox, out lmFb, out cfFb))
                            {
                                landmarks = lmFb;
                                bool prevL = _pinchLatched;
                                double pfx, pfy; bool pwl;
                                LandmarkGesture(lmFb, prevL, out pfx, out pfy, out pwl);
                                _pinchLatched = pwl;
                                pinched = _pinchLatched;
                                const double aFb = 0.62;
                                if (!_hasSmoothed || pinched != prevL) { _sx = pfx; _sy = pfy; _hasSmoothed = true; }
                                else { _sx = aFb * pfx + (1 - aFb) * _sx; _sy = aFb * pfy + (1 - aFb) * _sy; }
                                fx = _sx; fy = _sy;
                                found = true;
                            }
                        }

                        // draw the "park your hand here" guide until a colour is locked
                        if (!_calibrated)
                        {
                            Cv2.Rectangle(frame, boxRect, new Scalar(120, 220, 90), 3);
                            var c = new OpenCvSharp.Point(FrameWidth / 2, FrameHeight / 2);
                            Cv2.DrawMarker(frame, c, new Scalar(120, 220, 90), MarkerTypes.Cross, 26, 2);
                        }
                        else if (found && landmarks != null)
                        {
                            DrawSkeleton(frame, landmarks, pinched);   // 21-joint ML skeleton (only visualisation)
                        }

                        PublishFrame(frame, found, fx, fy, pinched);
                    }
                    catch
                    {
                        // never let a single bad frame kill the loop
                        Thread.Sleep(20);
                    }
                }
            }
        }

        // Sample the dominant hue/sat/val inside the target box using histogram peaks.
        // Histogram-based peak is far more robust than arithmetic mean — mean is dragged off by
        // shadows, highlights and any background pixel that sneaks into the calibration box.
        private void SampleColour(Mat hsv, Rect box)
        {
            using (var roi = new Mat(hsv, box))
            {
                // --- hue: find the histogram peak (dominant hue in the patch) ---
                int peakH = 0;
                {
                    var hBins = new int[180];
                    for (int r = 0; r < roi.Rows; r++)
                        for (int c = 0; c < roi.Cols; c++)
                        {
                            var px = roi.At<Vec3b>(r, c);
                            if (px.Item1 > 30 && px.Item2 > 30)   // ignore dark / grey pixels
                                hBins[px.Item0]++;
                        }
                    for (int i = 1; i < 180; i++) if (hBins[i] > hBins[peakH]) peakH = i;
                }

                // --- sat / val: use mean over the full box for the floor baseline ---
                Scalar mean = Cv2.Mean(roi);
                int s = (int)Math.Round(mean.Val1);
                int v = (int)Math.Round(mean.Val2);

                lock (_lock)
                {
                    _hueTarget = peakH;
                    // Wider, more permissive floors so slight lighting changes don't break tracking
                    _satFloor = Clamp(s - 100, 20, 220);
                    _valFloor = Clamp(v - 110, 20, 220);
                    _calibrated = true;
                    _hasSmoothed = false;
                    _pinchLatched = false; _pinched = false;
                }
            }
        }

        // The color blob ONLY localises the hand (finds the bounding box). All gesture decisions —
        // pen position, pinch detection — come exclusively from the ML 21-joint landmark model.
        // There is NO classical fingertip fallback: when the ML model misses a frame, the tracker
        // holds its last state silently rather than switching to a different detection method that
        // would give different (confusing) behaviour.
        private bool TrackPointer(Mat frame, Mat hsv, Mat mask, Mat kernel, out double px, out double py,
                                  out bool pinched, out OpenCvSharp.Point[] fingertips, out OpenCvSharp.Point handCentre,
                                  out OpenCvSharp.Point2f[] landmarks)
        {
            px = py = 0; pinched = false; fingertips = null;
            handCentre = default; landmarks = null;
            int hueTol = 20, hue, satFloor, valFloor;
            lock (_lock) { hue = _hueTarget; satFloor = _satFloor; valFloor = _valFloor; }

            // ---- 1. Color threshold → hand blob → bounding box (localisation only) ----
            int lo = hue - hueTol, hi = hue + hueTol;
            var sv0 = new Scalar(0, satFloor, valFloor);
            if (lo < 0 || hi > 179)
            {
                int loA = (lo + 180) % 180, hiA = 179, loB = 0, hiB = hi % 180;
                using (var m1 = new Mat()) using (var m2 = new Mat())
                {
                    Cv2.InRange(hsv, new Scalar(loA, sv0.Val1, sv0.Val2), new Scalar(hiA, 255, 255), m1);
                    Cv2.InRange(hsv, new Scalar(loB, sv0.Val1, sv0.Val2), new Scalar(hiB, 255, 255), m2);
                    Cv2.BitwiseOr(m1, m2, mask);
                }
            }
            else
            {
                Cv2.InRange(hsv, new Scalar(lo, sv0.Val1, sv0.Val2), new Scalar(hi, 255, 255), mask);
            }
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);

            Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out _,
                             RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours == null || contours.Length == 0) return false;

            int hand = -1; double handArea = 0;
            for (int i = 0; i < contours.Length; i++)
            {
                double a = Cv2.ContourArea(contours[i]);
                if (a > handArea) { handArea = a; hand = i; }
            }
            if (hand < 0 || handArea < 450) return false;

            var contour = contours[hand];
            var mHand = Cv2.Moments(contour);
            if (Math.Abs(mHand.M00) < 1e-3) return false;
            double cx = mHand.M10 / mHand.M00, cy = mHand.M01 / mHand.M00;
            handCentre = new OpenCvSharp.Point((int)cx, (int)cy);
            Rect bb = Cv2.BoundingRect(contour);
            _lastHandBbox = bb;   // persist for the inter-frame ML fallback in the main loop

            // ---- 2. ML landmark model — ONLY source of gesture and pen position ----
            bool prevLatched = _pinchLatched;
            bool wantLock, freshPen; double penx, peny;

            OpenCvSharp.Point2f[] lm; float conf;
            if (_landmarker != null && _landmarker.Ready && _landmarker.Detect(frame, bb, out lm, out conf))
            {
                landmarks = lm;
                LandmarkGesture(lm, prevLatched, out penx, out peny, out wantLock);
                freshPen = true;
            }
            else
            {
                // ML missed this frame — hold current pinch state, keep pen at last smoothed position.
                // The debounce below will release after 3 consecutive missed frames.
                penx = _hasSmoothed ? _sx : cx;
                peny = _hasSmoothed ? _sy : cy;
                wantLock = prevLatched;
                freshPen = false;
            }

            // ---- 3. Pinch state: instant on AND instant off ----
            // LandmarkGesture hysteresis (on=40%/off=58% of hand span) already prevents flicker;
            // holding extra frames only delays the pen-up and causes a visible drawing tail.
            _pinchLatched = wantLock;
            pinched = _pinchLatched;

            // ---- 4. Pointer smoothing: snap on state change, EMA otherwise ----
            double tx = freshPen ? penx : _sx;
            double ty = freshPen ? peny : _sy;
            const double ema = 0.62;
            if (!_hasSmoothed || pinched != prevLatched) { _sx = tx; _sy = ty; _hasSmoothed = true; }
            else { _sx = ema * tx + (1 - ema) * _sx; _sy = ema * ty + (1 - ema) * _sy; }
            px = _sx; py = _sy;
            return true;
        }

        // Fingertips = convex-hull peaks separated by deep, acute convexity defects (finger valleys), plus
        // the topmost point so a single raised finger is still found. Nearby candidates are merged.
        private static OpenCvSharp.Point[] FindFingertips(OpenCvSharp.Point[] contour, double cx, double cy, double hs)
        {
            var cand = new System.Collections.Generic.List<OpenCvSharp.Point>();
            Vec4i[] defects = TryConvexityDefects(contour);
            if (defects != null)
            {
                double minDepth = 0.08 * hs;
                foreach (var d in defects)
                {
                    if (d.Item3 / 256.0 < minDepth) continue;      // shallow = not a finger valley
                    var s = contour[d.Item0];
                    var e = contour[d.Item1];
                    var f = contour[d.Item2];
                    if (Angle(s, f, e) > 1.75) continue;            // > ~100° = too wide to be fingers
                    cand.Add(s); cand.Add(e);                       // both flanks are fingertips
                }
            }

            // always include the topmost contour point (a lone finger has no defect)
            var topPt = contour[0];
            foreach (var p in contour) if (p.Y < topPt.Y) topPt = p;
            cand.Add(topPt);

            // keep only points that stick out from the palm, then merge near-duplicates
            double minReach = 0.32 * hs, mergeDist = 0.12 * hs;
            var tips = new System.Collections.Generic.List<OpenCvSharp.Point>();
            foreach (var p in cand)
            {
                if (Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) < minReach) continue;
                bool dup = false;
                for (int i = 0; i < tips.Count; i++)
                    if (Dist(tips[i], p) < mergeDist)
                    {
                        if (p.Y < tips[i].Y) tips[i] = p;                   // keep the higher (tip) of the pair
                        dup = true; break;
                    }
                if (!dup) tips.Add(p);
            }
            if (tips.Count > 6) tips = tips.GetRange(0, 6);                 // a hand has at most 5
            return tips.ToArray();
        }

        // Convexity defects require the hull winding to match the contour; OpenCV throws otherwise, so try
        // both orientations and give up gracefully (fingertips then fall back to the topmost point only).
        private static Vec4i[] TryConvexityDefects(OpenCvSharp.Point[] contour)
        {
            foreach (bool cw in new[] { false, true })
            {
                try
                {
                    int[] hull = Cv2.ConvexHullIndices(contour, cw);
                    if (hull != null && hull.Length > 3)
                        return Cv2.ConvexityDefects(contour, hull);
                }
                catch { /* wrong winding — try the other one */ }
            }
            return null;
        }

        private static double Dist(OpenCvSharp.Point a, OpenCvSharp.Point b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        // angle at vertex v between v→a and v→b, in radians
        private static double Angle(OpenCvSharp.Point a, OpenCvSharp.Point v, OpenCvSharp.Point b)
        {
            double ax = a.X - v.X, ay = a.Y - v.Y, bx = b.X - v.X, by = b.Y - v.Y;
            double dot = ax * bx + ay * by;
            double m = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by);
            if (m < 1e-6) return Math.PI;
            return Math.Acos(Math.Max(-1, Math.Min(1, dot / m)));
        }

        // Draw the detected fingertips (cyan, with a light skeleton to the palm) + the finger count, and
        // highlight the two closest tips in green when the child has locked them.
        private static void DrawFingers(Mat frame, OpenCvSharp.Point[] tips, OpenCvSharp.Point centre, bool locked)
        {
            if (tips == null) return;
            var cyan = new Scalar(230, 230, 60);
            var green = new Scalar(90, 220, 100);
            int bi = -1, bj = -1; double best = double.MaxValue;
            for (int i = 0; i < tips.Length; i++)
                for (int j = i + 1; j < tips.Length; j++)
                {
                    double d = Dist(tips[i], tips[j]);
                    if (d < best) { best = d; bi = i; bj = j; }
                }
            for (int i = 0; i < tips.Length; i++)
            {
                bool isLockPair = locked && (i == bi || i == bj);
                var col = isLockPair ? green : cyan;
                Cv2.Line(frame, centre, tips[i], col, 1, LineTypes.AntiAlias);
                Cv2.Circle(frame, tips[i], isLockPair ? 12 : 9, col, -1, LineTypes.AntiAlias);
                Cv2.Circle(frame, tips[i], isLockPair ? 12 : 9, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
            }
            if (locked && bi >= 0)
                Cv2.Line(frame, tips[bi], tips[bj], green, 3, LineTypes.AntiAlias);
            Cv2.PutText(frame, tips.Length.ToString() + (tips.Length == 1 ? " finger" : " fingers"),
                        new OpenCvSharp.Point(12, 28), HersheyFonts.HersheySimplex, 0.8,
                        new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
        }

        // ---- ML-landmark helpers ----

        private static float LDist(OpenCvSharp.Point2f a, OpenCvSharp.Point2f b)
            => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        // From the 21 landmarks, decide pen position and whether the index+thumb pinch is active.
        // Pen always aims at the index fingertip (landmark 8) — the natural pointing position.
        // Pen-DOWN fires ONLY when index tip (8) + thumb tip (4) come close together — the same
        // natural "pinch" gesture used for camera zooming.  Using a specific pair (not any two fingers)
        // avoids false triggers from other gestures like a peace sign or a fist.
        private static void LandmarkGesture(OpenCvSharp.Point2f[] lm, bool prevLatched,
                                            out double penx, out double peny, out bool wantLock)
        {
            var wrist = lm[0];
            float span = Math.Max(1f, LDist(wrist, lm[9]));   // wrist → middle-finger base = hand scale

            // Pen aims at the index fingertip — the natural pointing position
            penx = lm[8].X; peny = lm[8].Y;

            // Pen-down: specifically index tip (8) + thumb tip (4) pinch together
            float dist = LDist(lm[4], lm[8]);
            float on  = 0.40f * span;   // 40% of hand span = pinching
            float off = 0.58f * span;   // must open wider to lift (hysteresis prevents flicker)
            wantLock = dist < (prevLatched ? off : on);
            if (wantLock)
            {
                // Pen tip = midpoint of the index-thumb pinch — the exact point they meet
                penx = (lm[4].X + lm[8].X) / 2.0;
                peny = (lm[4].Y + lm[8].Y) / 2.0;
            }
        }

        // Draw the real 21-joint hand skeleton (bones + joints) + a bounding box, like MediaPipe. Bones go
        // green while two fingers are locked (drawing), cyan while aiming.
        private static void DrawSkeleton(Mat frame, OpenCvSharp.Point2f[] lm, bool locked)
        {
            if (lm == null || lm.Length < 21) return;
            var cyan = new Scalar(230, 230, 60);
            var green = new Scalar(90, 220, 100);
            var magenta = new Scalar(255, 0, 255);
            var white = new Scalar(255, 255, 255);

            float minx = 1e9f, miny = 1e9f, maxx = -1e9f, maxy = -1e9f;
            foreach (var p in lm)
            {
                if (p.X < minx) minx = p.X; if (p.X > maxx) maxx = p.X;
                if (p.Y < miny) miny = p.Y; if (p.Y > maxy) maxy = p.Y;
            }
            Cv2.Rectangle(frame, new Rect((int)minx - 10, (int)miny - 10,
                          (int)(maxx - minx) + 20, (int)(maxy - miny) + 20), green, 2);

            var boneCol = locked ? green : cyan;
            foreach (var (a, b) in HandLandmarker.Connections)
                Cv2.Line(frame, P(lm[a]), P(lm[b]), boneCol, 2, LineTypes.AntiAlias);

            var tipSet = new System.Collections.Generic.HashSet<int>(HandLandmarker.TipIds);
            for (int i = 0; i < 21; i++)
            {
                // Index tip (8) and thumb tip (4) glow green and grow when pinched (pen down)
                bool isPinchPair = locked && (i == 4 || i == 8);
                int r = isPinchPair ? 10 : (tipSet.Contains(i) ? 7 : 5);
                var jointCol = isPinchPair ? green : magenta;
                Cv2.Circle(frame, P(lm[i]), r, jointCol, -1, LineTypes.AntiAlias);
                Cv2.Circle(frame, P(lm[i]), r, white, 1, LineTypes.AntiAlias);
            }
            // Draw a bright connecting line between index tip and thumb tip when pinched
            if (locked)
                Cv2.Line(frame, P(lm[4]), P(lm[8]), green, 3, LineTypes.AntiAlias);
        }

        private static OpenCvSharp.Point P(OpenCvSharp.Point2f p) => new OpenCvSharp.Point((int)p.X, (int)p.Y);

        // Convert the display Mat to a fresh BGR24 byte[] and publish it + the pointer under the lock.
        private void PublishFrame(Mat frame, bool found, double fx, double fy, bool pinched)
        {
            Mat cont = frame.IsContinuous() ? frame : frame.Clone();
            int len = (int)(cont.Total() * cont.ElemSize());   // 640*480*3
            byte[] buf = new byte[len];
            System.Runtime.InteropServices.Marshal.Copy(cont.Data, buf, 0, len);
            if (!ReferenceEquals(cont, frame)) cont.Dispose();

            lock (_lock)
            {
                _latest = buf;
                _pointFound = found;
                _pinched = found && pinched;
                if (found) { _px = fx; _py = fy; }
            }
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        public void Stop()
        {
            _running = false;
            try { _thread?.Join(800); } catch { }
            _thread = null;
            try { _cap?.Release(); } catch { }
            try { _cap?.Dispose(); } catch { }
            _cap = null;
            try { _landmarker?.Dispose(); } catch { }   // safe: the loop has exited by now
            _landmarker = null;
        }

        public void Dispose() => Stop();
    }
}
