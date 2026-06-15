using System;
using System.Windows.Media;
using CarthaBotVPL.Models;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// The LED ring animation formulas, shared by the virtual robot (VplRuntime) and the
    /// live digital twin (LiveTwin) — both must colour exactly like the firmware's anim_tick().
    /// </summary>
    public static class LedAnimMath
    {
        public static Color Evaluate(LedAnim anim, Color baseColor, double tMs)
        {
            switch (anim)
            {
                case LedAnim.Rainbow:
                    return Wheel(tMs / 12.0);
                case LedAnim.Blink:
                    return ((long)(tMs / 350) % 2 == 0) ? baseColor : Colors.Black;
                case LedAnim.Chase:
                    double k = (tMs / 80.0) % 11 / 11.0;
                    double lvl = 0.45 + 0.55 * Math.Abs(Math.Sin(k * Math.PI));
                    return Color.FromRgb((byte)(baseColor.R * lvl), (byte)(baseColor.G * lvl), (byte)(baseColor.B * lvl));
                default:   // breathe
                    double ph = tMs % 2400;
                    double lv = ph < 1200 ? ph / 1200 : (2400 - ph) / 1200;
                    return Color.FromRgb((byte)(baseColor.R * lv), (byte)(baseColor.G * lv), (byte)(baseColor.B * lv));
            }
        }

        public static Color Wheel(double p)
        {
            int v = (int)p % 255; if (v < 0) v += 255;
            if (v < 85) return Color.FromRgb((byte)(255 - v * 3), (byte)(v * 3), 0);
            if (v < 170) { v -= 85; return Color.FromRgb(0, (byte)(255 - v * 3), (byte)(v * 3)); }
            v -= 170; return Color.FromRgb((byte)(v * 3), 0, (byte)(255 - v * 3));
        }
    }
}
