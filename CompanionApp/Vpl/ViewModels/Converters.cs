using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CarthaBotVPL.ViewModels
{
    /// <summary>Converts a "#AARRGGBB" / "#RRGGBB" string into a SolidColorBrush (for colour previews).</summary>
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)value)); }
            catch { return Brushes.Transparent; }
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>Converts a "#AARRGGBB" / "#RRGGBB" string into a Color (for the 3D LED glow).</summary>
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try { return (Color)ColorConverter.ConvertFromString((string)value); }
            catch { return Colors.Black; }
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>True → Collapsed, False → Visible (used to show the empty-canvas hint).</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>True → Visible, False → Collapsed.</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>
    /// Two-way enum ↔ bool for radio-style tiles. IsChecked is true when the bound enum equals
    /// the ConverterParameter; checking a tile writes that enum value back to the source.
    /// Because every tile shares the same bound property, they behave as one radio group.
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null && parameter != null &&
               string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                if (targetType.IsEnum) return Enum.Parse(targetType, parameter.ToString());
                // Also used for int-typed "radio" groups (state filter / state value).
                return System.Convert.ChangeType(parameter.ToString(), targetType, CultureInfo.InvariantCulture);
            }
            return Binding.DoNothing;
        }
    }
}
