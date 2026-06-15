using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private bool _dark = true;   // matches the dark defaults declared in XAML

        public VplView(IEventAggregator eventAggregator, List<string> oldComs)
        {
            InitializeComponent();
            var vm = new VplViewModel(eventAggregator, oldComs);
            DataContext = vm;
            Loaded += (_, __) => { LoadEmptyStateRobot(); UpdateStarBadge(); };
            Sim.MissionCompleted += _ => UpdateStarBadge();
            // telemetry arrives on a background thread → marshal to the UI then feed the twin
            vm.TelemetryReceived += v =>
                Dispatcher.BeginInvoke(new Action(() => Sim.FeedTelemetry(v)));
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

        // ===== Light / dark theme toggle (swaps the DynamicResource brushes) =====
        private void OnToggleTheme(object sender, RoutedEventArgs e)
        {
            _dark = !_dark;
            ApplyTheme(_dark);
            if (ThemeGlyph != null) ThemeGlyph.Text = _dark ? "☀" : "🌙";
        }

        private void ApplyTheme(bool dark)
        {
            void Set(string key, string hex) => Resources[key] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hex));

            if (dark)
            {
                Set("VplBg", "#192428");
                Set("VplToolbar", "#2D383C");
                Set("VplPanel", "#22313A");
                Set("VplCard", "#22313A");
                Set("VplLine", "#33454F");
                Set("VplText", "#FFFFFF");
                Set("VplTextDim", "#9FB0B7");
                Set("VplChip", "#3A4A52");
                Set("VplDashed", "#51636C");
                Set("VplConnector", "#6B7980");
                Set("VplHover", "#22FFFFFF");
                Set("VplEventCardBg", "#2A2018");
                Set("VplActionCardBg", "#1C2A30");
            }
            else
            {
                Set("VplBg", "#FFFFFF");
                Set("VplToolbar", "#EEF1F3");
                Set("VplPanel", "#F3F5F7");
                Set("VplCard", "#FFFFFF");
                Set("VplLine", "#D7DEE2");
                Set("VplText", "#1C2B31");
                Set("VplTextDim", "#66767D");
                Set("VplChip", "#E5EBEE");
                Set("VplDashed", "#B9C4CA");
                Set("VplConnector", "#9DAAB0");
                Set("VplHover", "#14000000");
                Set("VplEventCardBg", "#FFF1E6");
                Set("VplActionCardBg", "#EAF5FC");
            }
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
