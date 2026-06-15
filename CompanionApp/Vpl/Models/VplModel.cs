using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CarthaBotVPL.ViewModels;

namespace CarthaBotVPL.Models
{
    // ---- Event side (left column, orange in the Thymio VPL) ----

    public enum EventKind
    {
        Start,      // runs once when the program starts
        Button,     // one of the 5 physical buttons (▲ ▼ ◀ ▶ ●)
        Obstacle,   // front IR sensor – something is in front of the robot
        Line,       // ground IR sensor – the robot is over a line / edge
        Timer       // the timer started by a Timer action has elapsed (advanced mode)
    }

    public enum ButtonDir { Center, Up, Down, Left, Right }

    // ---- Action side (right column, blue in the Thymio VPL) ----

    public enum ActionKind
    {
        Move,    // set the two motors (forward / back / spin / stop) at a speed
        Color,   // set the top LED ring colour
        Sound,   // play a short sound or a 6-note tune composed by the child
        Wait,    // pause for a number of seconds before the next action
        Anim,    // animate the 11-LED ring (rainbow / blink / chase / breathe)
        Timer,   // start the timer (pairs with the Timer event, advanced mode)
        State    // remember a state: ★ ♥ ● ■  (pairs with the event state filter)
    }

    public enum MoveDir
    {
        Stop, Forward, Backward, Left, Right,
        FollowLine   // drive along the black line using the ground sensor (bang-bang)
    }

    public enum SoundKind { Beep, Happy, Sad, Siren, Custom }

    public enum LedAnim { Rainbow, Blink, Chase, Breathe }

    /// <summary>One of the 6 slots of the kid-composed tune (pentatonic, so anything sounds nice).</summary>
    public class NoteSlot : ObservableObject
    {
        /// <summary>-1 = rest, 0..4 = C5 D5 E5 G5 A5 (bottom to top).</summary>
        private int _pitch = -1;
        public int Pitch { get => _pitch; set => Set(ref _pitch, value); }

        public ICommand Toggle { get; }

        public NoteSlot()
        {
            // Clicking the active cell again clears the note back to a rest.
            Toggle = new RelayCommand(p =>
            {
                int v = int.Parse((string)p);
                Pitch = Pitch == v ? -1 : v;
            });
        }

        public NoteSlot Clone() => new NoteSlot { Pitch = Pitch };
    }

    /// <summary>The condition (trigger) of a rule.</summary>
    public class VplEvent : ObservableObject
    {
        private EventKind _kind;
        public EventKind Kind { get => _kind; set => Set(ref _kind, value); }

        private ButtonDir _button = ButtonDir.Up;
        public ButtonDir Button { get => _button; set => Set(ref _button, value); }

        /// <summary>For sensor events: true = "detected", false = "clear".</summary>
        private bool _detected = true;
        public bool Detected { get => _detected; set => Set(ref _detected, value); }

        /// <summary>Advanced mode: -1 = fire in any state, 0..3 = only fire in that state (★ ♥ ● ■).</summary>
        private int _stateFilter = -1;
        public int StateFilter { get => _stateFilter; set => Set(ref _stateFilter, value); }

        public ICommand PickButton { get; }
        public ICommand ToggleDetected { get; }
        public ICommand PickStateFilter { get; }

        public VplEvent()
        {
            PickButton = new RelayCommand(p => Button = (ButtonDir)Enum.Parse(typeof(ButtonDir), (string)p));
            ToggleDetected = new RelayCommand(p => Detected = bool.Parse((string)p));
            PickStateFilter = new RelayCommand(p => StateFilter = int.Parse((string)p));
        }

        public VplEvent Clone() => new VplEvent { Kind = Kind, Button = Button, Detected = Detected, StateFilter = StateFilter };
    }

    /// <summary>One actuator setting performed when a rule fires.</summary>
    public class VplAction : ObservableObject
    {
        private ActionKind _kind;
        public ActionKind Kind { get => _kind; set => Set(ref _kind, value); }

        // Move
        private MoveDir _move = MoveDir.Forward;
        public MoveDir Move { get => _move; set => Set(ref _move, value); }

        private int _speed = 150;     // 0..255, matches firmware default
        public int Speed { get => _speed; set => Set(ref _speed, value); }

        // Color (top LED ring)
        private byte _r = 0, _g = 180, _b = 255;
        public byte R { get => _r; set { if (Set(ref _r, value)) Raise(nameof(ColorHex)); } }
        public byte G { get => _g; set { if (Set(ref _g, value)) Raise(nameof(ColorHex)); } }
        public byte B { get => _b; set { if (Set(ref _b, value)) Raise(nameof(ColorHex)); } }
        public string ColorHex => $"#FF{_r:X2}{_g:X2}{_b:X2}";

        // Sound
        private SoundKind _sound = SoundKind.Beep;
        public SoundKind Sound { get => _sound; set => Set(ref _sound, value); }

        /// <summary>The 6 slots of the child's own tune (used when Sound == Custom).</summary>
        public ObservableCollection<NoteSlot> Notes { get; } = new ObservableCollection<NoteSlot>();

        // Wait (seconds) — also the duration for the Timer action
        private double _seconds = 1.0;
        public double Seconds { get => _seconds; set => Set(ref _seconds, value); }

        // Anim (LED ring animation)
        private LedAnim _anim = LedAnim.Rainbow;
        public LedAnim Anim { get => _anim; set => Set(ref _anim, value); }

        // State to remember (0..3 = ★ ♥ ● ■)
        private int _stateValue;
        public int StateValue { get => _stateValue; set => Set(ref _stateValue, value); }

        public ICommand PickMove { get; }
        public ICommand PickColor { get; }
        public ICommand PickSound { get; }
        public ICommand PickAnim { get; }
        public ICommand PickState { get; }

        public VplAction()
        {
            for (int i = 0; i < 6; i++) Notes.Add(new NoteSlot());
            // A friendly default so the tune is audible before the child edits it.
            Notes[0].Pitch = 0; Notes[1].Pitch = 2; Notes[2].Pitch = 4;

            PickMove = new RelayCommand(p => Move = (MoveDir)Enum.Parse(typeof(MoveDir), (string)p));
            PickSound = new RelayCommand(p => Sound = (SoundKind)Enum.Parse(typeof(SoundKind), (string)p));
            PickAnim = new RelayCommand(p => Anim = (LedAnim)Enum.Parse(typeof(LedAnim), (string)p));
            PickState = new RelayCommand(p => StateValue = int.Parse((string)p));
            PickColor = new RelayCommand(p =>
            {
                var parts = ((string)p).Split(',');
                R = byte.Parse(parts[0]); G = byte.Parse(parts[1]); B = byte.Parse(parts[2]);
            });
        }

        public VplAction Clone()
        {
            var a = new VplAction
            {
                Kind = Kind, Move = Move, Speed = Speed, R = R, G = G, B = B,
                Sound = Sound, Seconds = Seconds, Anim = Anim, StateValue = StateValue
            };
            for (int i = 0; i < Notes.Count && i < a.Notes.Count; i++) a.Notes[i].Pitch = Notes[i].Pitch;
            return a;
        }
    }

    /// <summary>A single VPL rule: one event paired with one or more actions.</summary>
    public class VplRule : ObservableObject
    {
        public int Index { get; set; }

        private VplEvent _event = new VplEvent();
        public VplEvent Event { get => _event; set => Set(ref _event, value); }

        public ObservableCollection<VplAction> Actions { get; } = new ObservableCollection<VplAction>();

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    }
}
