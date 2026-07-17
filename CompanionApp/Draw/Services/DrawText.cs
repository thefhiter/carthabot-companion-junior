using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CompanionApp.Draw.Services
{
    /// <summary>
    /// A compact single-stroke ("engraving") vector font for A–Z and 0–9, so the child can type a
    /// word and have CarthaBot write it. Glyphs live on a 0..4 (x) × 0..6 (y, UP) grid; a few letters
    /// need more than one stroke (the pen is fixed down, so those get a short joining line — that's
    /// expected). <see cref="Layout"/> lays a word out left-to-right and returns the polylines.
    /// </summary>
    public static class DrawText
    {
        // strokes separated by ';', points by space, x/y by ',' — coordinates on a 0..4 × 0..6 grid (y up).
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = "0,0 2,6 4,0;0.8,2.4 3.2,2.4",
            ['B'] = "0,0 0,6 3,6 3.8,5 3,3.4 0,3.4;3,3.4 3.9,1.6 3,0 0,0",
            ['C'] = "4,5 2.5,6 1,6 0,4.5 0,1.5 1,0 2.5,0 4,1",
            ['D'] = "0,0 0,6 2,6 3.6,4.5 3.6,1.5 2,0 0,0",
            ['E'] = "4,6 0,6 0,0 4,0;0,3 3,3",
            ['F'] = "4,6 0,6 0,0;0,3 3,3",
            ['G'] = "4,5 2.5,6 1,6 0,4.5 0,1.5 1,0 2.5,0 4,1 4,2.6 2.4,2.6",
            ['H'] = "0,0 0,6;4,0 4,6;0,3 4,3",
            ['I'] = "0,6 4,6;2,6 2,0;0,0 4,0",
            ['J'] = "4,6 4,1 3,0 1.5,0 0.4,1",
            ['K'] = "0,0 0,6;0,3 4,6;1.4,3.7 4,0",
            ['L'] = "0,6 0,0 4,0",
            ['M'] = "0,0 0,6 2,3 4,6 4,0",
            ['N'] = "0,0 0,6 4,0 4,6",
            ['O'] = "1,0 0,1.5 0,4.5 1,6 3,6 4,4.5 4,1.5 3,0 1,0",
            ['P'] = "0,0 0,6 3,6 3.8,5 3,3.4 0,3.4",
            ['Q'] = "1,0 0,1.5 0,4.5 1,6 3,6 4,4.5 4,1.5 3,0 1,0;2.4,1.6 4.4,-0.4",
            ['R'] = "0,0 0,6 3,6 3.8,5 3,3.4 0,3.4;1.6,3.4 4,0",
            ['S'] = "4,5 2.5,6 1,6 0,5 0.7,3.4 3.4,2.6 4,1 3,0 1.5,0 0,1",
            ['T'] = "0,6 4,6;2,6 2,0",
            ['U'] = "0,6 0,1.5 1,0 3,0 4,1.5 4,6",
            ['V'] = "0,6 2,0 4,6",
            ['W'] = "0,6 1,0 2,3 3,0 4,6",
            ['X'] = "0,0 4,6;0,6 4,0",
            ['Y'] = "0,6 2,3 4,6;2,3 2,0",
            ['Z'] = "0,6 4,6 0,0 4,0",
            ['0'] = "1,0 0,1.5 0,4.5 1,6 3,6 4,4.5 4,1.5 3,0 1,0;1,1 3,5",
            ['1'] = "1,5 2,6 2,0;0.6,0 3.4,0",
            ['2'] = "0,5 1,6 3,6 4,5 4,4 0,0 4,0",
            ['3'] = "0,6 4,6 2,3.4;2,3.4 4,2 3,0 1,0 0,1",
            ['4'] = "3,0 3,6 0,2.4 4,2.4",
            ['5'] = "4,6 0,6 0,3.4 3,3.4 4,2.2 3,0 1,0 0,1",
            ['6'] = "4,5 3,6 1,6 0,4 0,1.5 1,0 3,0 4,1.5 4,2.5 3,3.6 1,3.6 0,2.6",
            ['7'] = "0,6 4,6 1.6,0",
            ['8'] = "2,3.4 0.6,4.6 1.4,6 2.6,6 3.4,4.6 2,3.4 0.4,2 1,0 3,0 3.6,2 2,3.4",
            ['9'] = "0,1 1,0 3,0 4,2 4,4.5 3,6 1,6 0,4.5 0,3.5 1,2.4 3,2.4 4,3.4",
            ['!'] = "2,6 2,2;2,0.6 2,0",
            ['?'] = "0,5 1,6 3,6 4,5 4,4 2,2.6 2,2;2,0.6 2,0",
            ['.'] = "1.6,0 2.4,0 2.4,0.8 1.6,0.8 1.6,0",
            [','] = "2.4,0.8 1.6,-0.8",
            ['-'] = "0.6,3 3.4,3",
            ['+'] = "0.6,3 3.4,3;2,1.5 2,4.5",
            ['='] = "0.6,3.8 3.4,3.8;0.6,2.2 3.4,2.2",
            [':'] = "2,4.2 2,5;2,1 2,1.8",
        };

        private const double GlyphHeight = 6.0;
        private const double Gap = 1.4;     // space between letters
        private const double SpaceW = 3.0;  // width of a blank space

        /// <summary>Width (in grid units) a glyph advances — its rightmost x, or a fixed space width.</summary>
        private static double Advance(char c)
        {
            if (c == ' ') return SpaceW + Gap;
            if (!Glyphs.TryGetValue(c, out var enc)) return 0;
            double max = 0;
            foreach (var stroke in enc.Split(';'))
                foreach (var pt in stroke.Split(' '))
                {
                    var xy = pt.Split(',');
                    if (xy.Length == 2 && double.TryParse(xy[0], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double x) && x > max)
                        max = x;
                }
            return max + Gap;
        }

        /// <summary>
        /// Lay <paramref name="text"/> out left-to-right. Returns polylines on a grid whose origin is
        /// the baseline-left, x growing right and y growing UP (0..6). <paramref name="totalWidth"/> is
        /// the full advance width so the caller can scale/centre it onto the canvas.
        /// </summary>
        public static List<List<Point>> Layout(string text, out double totalWidth)
        {
            var result = new List<List<Point>>();
            double penX = 0;
            foreach (char raw in (text ?? "").ToUpperInvariant())
            {
                char c = raw;
                if (c == ' ') { penX += SpaceW + Gap; continue; }
                if (!Glyphs.TryGetValue(c, out var enc)) continue;

                foreach (var strokeStr in enc.Split(';'))
                {
                    var stroke = new List<Point>();
                    foreach (var pt in strokeStr.Split(' '))
                    {
                        var xy = pt.Split(',');
                        if (xy.Length != 2) continue;
                        if (double.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                            double.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
                            stroke.Add(new Point(penX + x, y));
                    }
                    if (stroke.Count >= 2) result.Add(stroke);
                }
                penX += Advance(c);
            }
            totalWidth = Math.Max(1e-6, penX);
            return result;
        }

        public static double Height => GlyphHeight;
    }
}
