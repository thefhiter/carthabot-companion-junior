using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using CarthaBotVPL.Models;
using CarthaBotVPL.Services;
using CarthaBotVPL.ViewModels;
using CompanionApp.Controls;
using Prism.Events;

namespace CarthaBotVPL.Views
{
    /// <summary>
    /// Embedded "Coding (under 6)" VPL studio. The host (Companion MainViewModel) flashes the
    /// MicroPython firmware first, then opens this view with the list of COM ports that existed
    /// before flashing so the view-model can find the CarthaBot's new serial port.
    /// </summary>
    public partial class VplView : UserControl
    {
        public VplView(IEventAggregator eventAggregator, List<string> oldComs)
            : this(eventAggregator, oldComs, ConnectionMode.Usb, null) { }

        public VplView(IEventAggregator eventAggregator, List<string> oldComs, ConnectionMode mode, string param)
        {
            InitializeComponent();
            var vm = new VplViewModel(eventAggregator, oldComs, mode, param);
            DataContext = vm;
            Loaded += (_, __) => { LoadEmptyStateRobot(); UpdateStarBadge(); };
            Sim.MissionCompleted += _ => UpdateStarBadge();
            // telemetry arrives on a background thread → marshal to the UI then feed the twin
            vm.TelemetryReceived += v =>
                Dispatcher.BeginInvoke(new Action(() => Sim.FeedTelemetry(v)));
            // mic-test packets too: (span, floor, clap) from the robot's own microphone
            vm.MicSampleReceived += (s, f, c) =>
                Dispatcher.BeginInvoke(new Action(() => OnMicSample(s, f, c)));
        }

        /// <summary>Look up a localized string from the app-level resource dictionaries.</summary>
        private static string L(string key, string fallback) =>
            Application.Current?.TryFindResource(key) as string ?? fallback;

        // ===== 🎤 microphone test: the robot samples its own mic and the app shows it =====
        private int _micClaps;

        private async void OnMicTest(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is VplViewModel vm)) return;
            _micClaps = 0;
            MicClapCount.Text = "0";
            MicLevelFill.Width = 0;
            MicStatusText.Text = L("vplMicWaiting", "Waking up CarthaBot's microphone…");
            MicOverlay.Visibility = Visibility.Visible;

            bool ok = await vm.StartMicTestAsync();
            MicStatusText.Text = ok
                ? L("vplMicHint", "Clap your hands or talk near CarthaBot — the bar shows what it hears!")
                : L("vplMicFail", "Couldn't reach CarthaBot — plug it in (or connect) and try again.");
        }

        private void OnCloseMicTest(object sender, RoutedEventArgs e)
        {
            if (DataContext is VplViewModel vm) vm.StopMicTest();
            MicOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnMicSample(int span, int floor, bool clap)
        {
            if (MicOverlay.Visibility != Visibility.Visible) return;
            // 440-wide bar; a solid clap (~12000 peak-to-peak ADC counts) fills it
            MicLevelFill.Width = Math.Min(440.0, span * 440.0 / 12000.0);
            MicGlyphScale.ScaleX = MicGlyphScale.ScaleY = 1 + Math.Min(0.35, span / 30000.0);
            if (!clap) return;

            _micClaps++;
            MicClapCount.Text = _micClaps.ToString();
            UiSounds.Blip();
            var pop = new DoubleAnimationUsingKeyFrames();
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.55, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.09)), new SineEase { EasingMode = EasingMode.EaseOut }));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3)), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }));
            MicClapScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            MicClapScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        // ===== Live: run on the real robot and mirror it in the 3D twin =====
        private void OnPlayLive(object sender, RoutedEventArgs e)
        {
            Sim.RunLive();
            SimOverlay.Visibility = Visibility.Visible;
        }

        // little yellow counter on the 🏆 button showing how many stars are earned
        private void UpdateStarBadge()
        {
            if (StarBadge == null) return;
            int stars = Mission.All.Count(m => MissionProgress.IsDone(m.Id));
            StarBadge.Visibility = stars > 0 ? Visibility.Visible : Visibility.Collapsed;
            StarBadgeText.Text = stars.ToString();
        }

        // Point the empty-state 3D viewer at the CarthaBot model that the build copies
        // next to the exe (Content -> Vpl\Assets\carthabot_robot.obj).
        private void LoadEmptyStateRobot()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Vpl", "Assets", "carthabot_robot.obj");
                if (EmptyRobot != null && File.Exists(path))
                    EmptyRobot.Source = path;
            }
            catch { /* empty-state mascot is decorative; ignore load issues */ }
        }

        // ===== 3D simulation: pressing ▶ runs the rules on the virtual CarthaBot =====
        private void OnPlaySim(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is VplViewModel vm)) return;
            Sim.Run(vm.Rules);
            SimOverlay.Visibility = Visibility.Visible;
        }

        private void OnCloseSim(object sender, RoutedEventArgs e)
        {
            Sim.StopSim();
            SimOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnStopSim(object sender, RoutedEventArgs e)
        {
            Sim.StopSim();
            SimOverlay.Visibility = Visibility.Collapsed;
        }

        // ===== missions: guided challenges played in the simulator, one star each =====
        private void OnToggleMissions(object sender, RoutedEventArgs e)
        {
            if (MissionsOverlay.Visibility == Visibility.Visible)
            {
                MissionsOverlay.Visibility = Visibility.Collapsed;
                return;
            }
            MissionList.ItemsSource = Mission.All.Select(m => new MissionCard(m)).ToList();
            MissionsOverlay.Visibility = Visibility.Visible;
        }

        private void OnCloseMissions(object sender, RoutedEventArgs e) =>
            MissionsOverlay.Visibility = Visibility.Collapsed;

        private void OnMissionCard(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.Tag is MissionCard card)) return;
            if (!(DataContext is VplViewModel vm)) return;
            MissionsOverlay.Visibility = Visibility.Collapsed;
            // missions are tuned for the Classic playground — pin it
            if (Sim.CurrentMap.Id != "classic")
                Sim.SetMap(SimMap.All[0]);
            Sim.Run(vm.Rules, card.Mission);
            SimOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>Row model for one mission card (localized at open time).</summary>
        public class MissionCard
        {
            public Mission Mission { get; }
            public string Glyph => Mission.Glyph;
            public string Title { get; }
            public string Hint { get; }
            public Visibility StarVisibility { get; }

            public MissionCard(Mission m)
            {
                Mission = m;
                Title = Application.Current?.TryFindResource(m.TitleKey) as string ?? m.Id;
                Hint = Application.Current?.TryFindResource(m.HintKey) as string ?? "";
                StarVisibility = MissionProgress.IsDone(m.Id) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ===== sound previews: the child hears a choice immediately, robot connected or not =====
        private void OnPreviewSound(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string name &&
                Enum.TryParse(name, out SoundKind kind))
                UiSounds.Preset(kind);
        }

        private void OnPreviewTune(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is VplAction action)
                UiSounds.Tune(action.Notes);
        }

        // A tap on a tune cell plays that pitch (only when the note was set, not cleared).
        private void OnNoteCell(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is NoteSlot slot &&
                fe.Tag is string t && int.TryParse(t, out int pitch) && slot.Pitch == pitch)
                UiSounds.Note(pitch);
        }

        // Clicking anywhere on a rule selects it, so the next Action tile is added to that rule.
        private void Rule_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is VplRule rule &&
                DataContext is VplViewModel vm)
            {
                vm.SelectedRule = rule;
            }
        }

        // ===== mute toggle for all app sounds (handy in classrooms) =====
        private void OnToggleMute(object sender, RoutedEventArgs e)
        {
            UiSounds.Enabled = !UiSounds.Enabled;
            if (MuteGlyph != null) MuteGlyph.Text = UiSounds.Enabled ? "🔊" : "🔇";
        }

        // ===== Save a PNG snapshot of the program =====
        private void OnScreenshot(object sender, RoutedEventArgs e)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
                int h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
                if (w <= 0 || h <= 0) return;

                var rtb = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                rtb.Render(this);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string file = Path.Combine(dir, $"CarthaBot-VPL-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                using (var fs = new FileStream(file, FileMode.Create))
                    encoder.Save(fs);

                if (DataContext is VplViewModel vm) vm.Status = "Saved " + file;
            }
            catch (Exception ex)
            {
                if (DataContext is VplViewModel vm) vm.Status = "Screenshot failed: " + ex.Message;
            }
        }
    }
}
