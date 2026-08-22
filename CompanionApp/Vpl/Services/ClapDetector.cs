using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// Listens to the PC microphone and raises <see cref="Clapped"/> when it hears a hand clap —
    /// the desktop stand-in used by the 3D playground, so the 👏 event tile feels magical there:
    /// clap your hands, the virtual robot reacts. (On the real robot the compiled program listens
    /// on the CarthaBot's own MIC400 microphone, GP27/ADC1 — see VplCompiler.)
    ///
    ///  * raw winmm waveIn via P/Invoke — no NuGet dependency, works wherever WPF does;
    ///  * 16 kHz / 16-bit mono in 20 ms buffers;
    ///  * a clap = a sharp transient: buffer loudness ≥ CLAP_RATIO × the rolling noise floor,
    ///    ≥ ATTACK_RATIO × the recent average, and above an absolute gate, with a refractory
    ///    window so one clap fires exactly once;
    ///  * <see cref="SuppressFor"/> mutes detection while the app itself plays sounds — with
    ///    open speakers the sim's own beeps would otherwise "clap" back at the robot.
    ///
    /// The winmm callback runs on a driver thread: subscribers must marshal to the UI thread.
    /// </summary>
    public sealed class ClapDetector : IDisposable
    {
        private const int SampleRate = 16000;
        private const int BufferMs = 20;
        private const int SamplesPerBuffer = SampleRate * BufferMs / 1000;   // 320
        private const int BufferBytes = SamplesPerBuffer * 2;
        private const int BufferCount = 8;

        // tuning (RMS levels are 0..1 of full scale)
        private const double AbsoluteGate = 0.050;   // quieter than this is never a clap
        private const double FloorRatio = 5.0;       // vs the slow ambient noise floor
        private const double AttackRatio = 2.5;      // vs the average of the last ~8 buffers
        private const int RefractoryMs = 350;        // one clap = one event

        /// <summary>Raised once per detected clap, ON THE AUDIO DRIVER THREAD.</summary>
        public event Action Clapped;

        /// <summary>Loudness of the latest buffer, 0..1 (for a "listening" pulse in the UI).
        /// Written on the driver thread, read on the UI thread; x64 double reads are atomic.</summary>
        public double Level { get; private set; }

        public bool IsRunning { get; private set; }

        /// <summary>True when Windows reports at least one recording device.</summary>
        public static bool HasMicrophone
        {
            get { try { return waveInGetNumDevs() > 0; } catch { return false; } }
        }

        private IntPtr _handle;
        private WaveInProc _callback;                 // field: keeps the delegate alive for winmm
        private readonly IntPtr[] _headers = new IntPtr[BufferCount];
        private readonly IntPtr[] _buffers = new IntPtr[BufferCount];
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastClapMs = -100000;
        private long _suppressUntilMs;
        private double _floor = 0.004;                // rolling ambient loudness
        private readonly double[] _recent = new double[8];
        private int _recentPos;
        private volatile bool _running;               // gate for the driver-thread callback

        /// <summary>Open the default microphone and start listening. False if there is no
        /// usable device (none plugged in, or Windows microphone privacy blocks the app).</summary>
        public bool Start()
        {
            if (IsRunning) return true;
            if (!HasMicrophone) return false;

            var fmt = new WAVEFORMATEX
            {
                wFormatTag = 1,                       // PCM
                nChannels = 1,
                nSamplesPerSec = SampleRate,
                wBitsPerSample = 16,
                nBlockAlign = 2,
                nAvgBytesPerSec = SampleRate * 2,
                cbSize = 0
            };
            _callback = OnWaveData;
            if (waveInOpen(out _handle, WAVE_MAPPER, ref fmt, _callback, IntPtr.Zero, CALLBACK_FUNCTION) != 0)
            {
                _handle = IntPtr.Zero;
                return false;
            }

            for (int i = 0; i < BufferCount; i++)
            {
                _buffers[i] = Marshal.AllocHGlobal(BufferBytes);
                var hdr = new WAVEHDR { lpData = _buffers[i], dwBufferLength = BufferBytes };
                _headers[i] = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
                Marshal.StructureToPtr(hdr, _headers[i], false);
                waveInPrepareHeader(_handle, _headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
                waveInAddBuffer(_handle, _headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
            }

            _running = true;
            if (waveInStart(_handle) != 0) { Stop(); return false; }
            IsRunning = true;
            return true;
        }

        public void Stop()
        {
            _running = false;                          // callbacks stop re-queuing first
            if (_handle != IntPtr.Zero)
            {
                try
                {
                    waveInStop(_handle);
                    waveInReset(_handle);              // hands every queued buffer back
                    for (int i = 0; i < BufferCount; i++)
                        if (_headers[i] != IntPtr.Zero)
                            waveInUnprepareHeader(_handle, _headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
                    waveInClose(_handle);
                }
                catch { /* the mic is decorative — never crash the studio for it */ }
                _handle = IntPtr.Zero;
            }
            for (int i = 0; i < BufferCount; i++)
            {
                if (_headers[i] != IntPtr.Zero) { Marshal.FreeHGlobal(_headers[i]); _headers[i] = IntPtr.Zero; }
                if (_buffers[i] != IntPtr.Zero) { Marshal.FreeHGlobal(_buffers[i]); _buffers[i] = IntPtr.Zero; }
            }
            Level = 0;
            IsRunning = false;
        }

        /// <summary>Ignore the microphone for a moment (call when the app plays its own sound).</summary>
        public void SuppressFor(double seconds) =>
            _suppressUntilMs = Math.Max(_suppressUntilMs, _clock.ElapsedMilliseconds + (long)(seconds * 1000));

        public void Dispose() => Stop();

        // ------------------------------------------------------------- driver-thread callback

        private void OnWaveData(IntPtr hwi, uint msg, IntPtr instance, IntPtr param1, IntPtr param2)
        {
            if (msg != MM_WIM_DATA || !_running) return;
            try
            {
                var hdr = Marshal.PtrToStructure<WAVEHDR>(param1);
                int samples = (int)hdr.dwBytesRecorded / 2;
                if (samples > 0) Analyze(hdr.lpData, samples);
                if (_running) waveInAddBuffer(hwi, param1, (uint)Marshal.SizeOf<WAVEHDR>());
            }
            catch { /* one bad buffer must not kill the audio thread */ }
        }

        private void Analyze(IntPtr data, int samples)
        {
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                double s = Marshal.ReadInt16(data, i * 2) / 32768.0;
                sum += s * s;
            }
            double rms = Math.Sqrt(sum / samples);
            Level = rms;

            // average of the previous ~8 buffers (before adding this one)
            double avg = 0;
            for (int i = 0; i < _recent.Length; i++) avg += _recent[i];
            avg = Math.Max(0.002, avg / _recent.Length);
            _recent[_recentPos] = rms;
            _recentPos = (_recentPos + 1) % _recent.Length;

            // ambient floor: climbs slowly, falls faster (recovers after our own sounds)
            _floor = rms > _floor ? _floor * 0.995 + rms * 0.005
                                  : _floor * 0.95 + rms * 0.05;
            if (_floor < 0.0025) _floor = 0.0025;

            long now = _clock.ElapsedMilliseconds;
            if (now < _suppressUntilMs) return;              // the app is talking, not the child
            if (now - _lastClapMs < RefractoryMs) return;    // still inside the last clap

            if (rms >= AbsoluteGate && rms >= _floor * FloorRatio && rms >= avg * AttackRatio)
            {
                _lastClapMs = now;
                Clapped?.Invoke();
            }
        }

        // ------------------------------------------------------------- winmm interop

        private const uint WAVE_MAPPER = unchecked((uint)-1);
        private const uint CALLBACK_FUNCTION = 0x30000;
        private const uint MM_WIM_DATA = 0x3C0;

        private delegate void WaveInProc(IntPtr hwi, uint uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")] private static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint waveInOpen(out IntPtr hwi, uint deviceId, ref WAVEFORMATEX format, WaveInProc callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveInPrepareHeader(IntPtr hwi, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInUnprepareHeader(IntPtr hwi, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInAddBuffer(IntPtr hwi, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInStart(IntPtr hwi);
        [DllImport("winmm.dll")] private static extern uint waveInStop(IntPtr hwi);
        [DllImport("winmm.dll")] private static extern uint waveInReset(IntPtr hwi);
        [DllImport("winmm.dll")] private static extern uint waveInClose(IntPtr hwi);
    }
}
