using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using CarthaBotVPL.Models;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// Plays the CarthaBot's sounds on the PC so the child hears a choice immediately,
    /// without the robot connected. The tones are synthesized in-memory as WAV (square wave
    /// with a soft fade, the closest desktop cousin of the robot's PWM speaker) and match
    /// the exact frequencies the VplCompiler sends to the firmware.
    /// </summary>
    public static class UiSounds
    {
        private const int SampleRate = 22050;
        private static SoundPlayer _current;
        private static readonly object Gate = new object();

        /// <summary>Master switch (a parent can mute the app from the toolbar).</summary>
        public static bool Enabled { get; set; } = true;

        // ---- public surface ----

        public static void Preset(SoundKind kind)
        {
            switch (kind)
            {
                case SoundKind.Happy: Play((523, 120), (659, 120), (784, 120)); break;
                case SoundKind.Sad: Play((392, 160), (330, 160), (262, 160)); break;
                case SoundKind.Siren: Play((440, 120), (880, 120), (440, 120), (880, 120)); break;
                case SoundKind.Custom: break;   // previewed via Tune()
                default: Play((880, 150)); break;
            }
        }

        /// <summary>One pitch of the tune editor (0..4 → pentatonic), so each tap is audible.</summary>
        public static void Note(int pitchIndex)
        {
            if (pitchIndex < 0 || pitchIndex >= VplCompiler.PentatonicHz.Length) return;
            Play((VplCompiler.PentatonicHz[pitchIndex], 160));
        }

        /// <summary>The whole 6-slot tune, exactly as the robot will play it.</summary>
        public static void Tune(IEnumerable<NoteSlot> notes)
        {
            var slots = notes.Select(n => n.Pitch).ToList();
            while (slots.Count > 0 && slots[slots.Count - 1] < 0) slots.RemoveAt(slots.Count - 1);
            if (slots.Count == 0) return;
            Play(slots.Select(p => p < 0 ? (0, VplCompiler.NoteMs) : (VplCompiler.PentatonicHz[p], VplCompiler.NoteMs)).ToArray());
        }

        /// <summary>Soft tick when a block lands on the canvas.</summary>
        public static void Blip() => Play(0.10, (1320, 45));

        /// <summary>Low "bonk" when the virtual robot bumps the obstacle brick.</summary>
        public static void Bonk() => Play(0.20, (140, 110), (95, 170));

        /// <summary>Coin pick-up chime; the pitch rises with each coin collected.</summary>
        public static void Coin(int number)
        {
            int[] basis = { 784, 988, 1175, 1319 };
            int f = basis[Math.Max(0, Math.Min(basis.Length - 1, number))];
            Play(0.15, (f, 90), (f * 3 / 2, 140));
        }

        /// <summary>Little fanfare for a successful run / completed mission.</summary>
        public static void Success() => Play(0.16, (659, 90), (784, 90), (1047, 200));

        /// <summary>Big mission-complete fanfare.</summary>
        public static void Fanfare() => Play(0.18, (523, 110), (659, 110), (784, 110), (1047, 160), (784, 90), (1047, 320));

        // ---- synthesis ----

        private static void Play(params (int hz, int ms)[] notes) => Play(0.17, notes);

        private static void Play(double amplitude, params (int hz, int ms)[] notes)
        {
            if (!Enabled || notes == null || notes.Length == 0) return;
            Task.Run(() =>
            {
                try
                {
                    var wav = BuildWav(notes, amplitude);
                    lock (Gate)
                    {
                        _current?.Stop();
                        _current = new SoundPlayer(new MemoryStream(wav));
                        _current.Play();   // async; stream stays alive inside the player
                    }
                }
                catch { /* sound is decorative — never crash the app for it */ }
            });
        }

        private static byte[] BuildWav((int hz, int ms)[] notes, double amplitude)
        {
            int totalSamples = notes.Sum(n => n.ms * SampleRate / 1000);
            var pcm = new short[totalSamples];
            int pos = 0;
            foreach (var (hz, ms) in notes)
            {
                int count = ms * SampleRate / 1000;
                int fade = Math.Min(count / 6, SampleRate / 100);   // ~10 ms anti-click ramp
                for (int i = 0; i < count; i++, pos++)
                {
                    if (hz <= 0) continue;   // rest = silence
                    double t = (double)i / SampleRate;
                    // Square wave like the robot's PWM, softened with a touch of sine.
                    double square = Math.Sign(Math.Sin(2 * Math.PI * hz * t));
                    double sine = Math.Sin(2 * Math.PI * hz * t);
                    double v = 0.6 * square + 0.4 * sine;
                    double env = 1.0;
                    if (i < fade) env = i / (double)fade;
                    else if (i > count - fade) env = (count - i) / (double)fade;
                    pcm[pos] = (short)(v * env * amplitude * short.MaxValue);
                }
            }

            using var ms2 = new MemoryStream();
            using var w = new BinaryWriter(ms2);
            int dataLen = pcm.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataLen);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16);
            w.Write((short)1); w.Write((short)1);            // PCM, mono
            w.Write(SampleRate); w.Write(SampleRate * 2);    // byte rate
            w.Write((short)2); w.Write((short)16);           // block align, bits
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataLen);
            foreach (var s in pcm) w.Write(s);
            w.Flush();
            return ms2.ToArray();
        }
    }
}
