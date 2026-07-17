using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace CompanionApp.Draw.Services
{
    /// <summary>
    /// Runs the MediaPipe hand-landmark model (OpenCV Zoo, ONNX) to turn a cropped hand image into the
    /// 21 3-D hand-knuckle landmarks — the real skeleton (wrist, 4 joints per finger) you see in
    /// MediaPipe demos. Classical CV (the colour blob) only localises the hand; this gives the joints.
    ///
    /// The model expects a roughly-centred, upright hand cropped to 224×224, RGB, scaled to [0,1]
    /// (NHWC). We feed it the bounding box the colour-lock already found. Outputs: 21×3 screen
    /// landmarks (in 0..224 crop space) + a presence/confidence score.
    /// </summary>
    public sealed class HandLandmarker : IDisposable
    {
        public const int Size = 224;

        private readonly InferenceSession _session;
        public bool Ready { get; }
        public string LoadError { get; private set; }

        // MediaPipe hand skeleton: pairs of landmark indices that are bones (for drawing lines).
        public static readonly (int, int)[] Connections =
        {
            (0,1),(1,2),(2,3),(3,4),            // thumb
            (0,5),(5,6),(6,7),(7,8),            // index
            (5,9),(9,10),(10,11),(11,12),       // middle
            (9,13),(13,14),(14,15),(15,16),     // ring
            (13,17),(17,18),(18,19),(19,20),    // pinky
            (0,17)                              // palm base
        };
        // fingertip landmark indices (thumb, index, middle, ring, pinky)
        public static readonly int[] TipIds = { 4, 8, 12, 16, 20 };

        public HandLandmarker()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Draw", "Assets", "handpose.onnx");
                if (!File.Exists(path)) { LoadError = "handpose.onnx not found next to the app."; Ready = false; return; }
                var so = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                try { so.IntraOpNumThreads = 2; } catch { }
                _session = new InferenceSession(path, so);
                Ready = true;
            }
            catch (Exception ex)
            {
                LoadError = "Couldn't load the hand model: " + ex.Message;
                Ready = false;
            }
        }

        /// <summary>
        /// Detect the 21 hand landmarks (in FRAME pixel coordinates) inside <paramref name="bbox"/> of the
        /// BGR frame. Returns false (and empty landmarks) if the model isn't confident a hand is there.
        /// </summary>
        public bool Detect(Mat frameBgr, Rect bbox, out Point2f[] landmarks, out float confidence)
        {
            landmarks = Array.Empty<Point2f>();
            confidence = 0f;
            if (!Ready) return false;

            // Expand the colour-lock box to a padded SQUARE centred on the hand.
            // The square must stay fully inside the frame so the inverse mapping from
            // 0..224 crop space back to frame pixels stays correct on all edges.
            int cx = bbox.X + bbox.Width / 2, cy = bbox.Y + bbox.Height / 2;
            int half = (int)(Math.Max(bbox.Width, bbox.Height) * 0.85) + 1;  // 1.7× colour-blob
            // Side is at most the smallest frame dimension so the square always fits
            int side = Math.Min(half * 2, Math.Min(frameBgr.Width, frameBgr.Height));
            if (side < 16) return false;
            // Centre on (cx, cy) then clamp so the square never falls outside the frame
            int x0 = Math.Max(0, Math.Min(cx - side / 2, frameBgr.Width  - side));
            int y0 = Math.Max(0, Math.Min(cy - side / 2, frameBgr.Height - side));
            var crop = new Rect(x0, y0, side, side);

            float[] input;
            using (var roi = new Mat(frameBgr, crop))
            using (var resized = new Mat())
            {
                Cv2.Resize(roi, resized, new OpenCvSharp.Size(Size, Size));
                input = ToRgbTensorData(resized);   // [1,224,224,3] R,G,B /255, NHWC row-major
            }

            var tensor = new DenseTensor<float>(input, new[] { 1, Size, Size, 3 });
            float[] lm, cf;
            using (var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor("input_1", tensor) }))
            {
                lm = results.First(r => r.Name == "Identity").AsTensor<float>().ToArray();      // 63 = 21×3
                cf = results.First(r => r.Name == "Identity_1").AsTensor<float>().ToArray();     // presence
            }
            confidence = cf.Length > 0 ? cf[0] : 0f;
            if (confidence < 0.40f) return false;   // 0.40 catches more valid hands than 0.50 without adding many false positives

            // landmark x,y are in 0..224 crop space → map back to frame pixels.
            // The crop is always a square of size `side`, so both axes use the same scale.
            float scale = (float)side / Size;
            var pts = new Point2f[21];
            for (int i = 0; i < 21; i++)
            {
                pts[i] = new Point2f(crop.X + lm[i * 3]     * scale,
                                     crop.Y + lm[i * 3 + 1] * scale);
            }
            landmarks = pts;
            return true;
        }

        // BGR Mat (224×224) → float[224*224*3] laid out as R,G,B per pixel, scaled to [0,1]
        private static float[] ToRgbTensorData(Mat bgr)
        {
            int n = Size * Size * 3;
            byte[] buf = new byte[n];
            Mat cont = bgr.IsContinuous() ? bgr : bgr.Clone();
            Marshal.Copy(cont.Data, buf, 0, n);
            if (!ReferenceEquals(cont, bgr)) cont.Dispose();

            float[] t = new float[n];
            for (int i = 0; i < Size * Size; i++)
            {
                t[i * 3 + 0] = buf[i * 3 + 2] / 255f;   // R (from BGR)
                t[i * 3 + 1] = buf[i * 3 + 1] / 255f;   // G
                t[i * 3 + 2] = buf[i * 3 + 0] / 255f;   // B
            }
            return t;
        }

        public void Dispose() => _session?.Dispose();
    }
}
