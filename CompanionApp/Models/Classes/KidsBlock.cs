using Prism.Mvvm;

namespace CompanionApp.Models.Classes
{
    /// <summary>
    /// The kind of action a Kids-Coding block represents.
    /// These map directly onto the CarthaBot hardware (motors / NeoPixel strip / speaker).
    /// </summary>
    public enum KidsAction
    {
        Forward,
        Backward,
        Left,
        Right,
        Lights,
        Beep
    }

    /// <summary>
    /// A single colourful, icon-only programming block used by the under-7 "Kids Coding" method.
    /// Pre-readers tap blocks from the palette to build a sequence; no text or typing is required.
    /// </summary>
    public class KidsBlock : BindableBase
    {
        public KidsAction Action { get; }

        /// <summary>Large unicode glyph shown on the block (renders with the default Segoe UI font).</summary>
        public string Glyph { get; }

        /// <summary>Short caption (localised) – kept tiny so it never gets in the way of the icon.</summary>
        public string Caption { get; }

        /// <summary>Background colour of the block (hex string consumed by XAML).</summary>
        public string Color { get; }

        public KidsBlock(KidsAction action, string glyph, string caption, string color)
        {
            Action = action;
            Glyph = glyph;
            Caption = caption;
            Color = color;
        }

        /// <summary>Returns an independent copy so the same palette block can be added many times.</summary>
        public KidsBlock Clone() => new KidsBlock(Action, Glyph, Caption, Color);
    }
}
