using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AsiErp.Converters
{
    public class InrCurrencyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return "₹0";
            if (!double.TryParse(value.ToString(), out double val)) return "₹0";

            bool withDecimals = parameter?.ToString() == "2";
            var inCulture = new CultureInfo("en-IN");
            string formatted = Math.Abs(val).ToString(withDecimals ? "N2" : "N0", inCulture);
            return (val < 0 ? "-₹" : "₹") + formatted;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InrShortConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return "₹0";
            if (!double.TryParse(value.ToString(), out double val)) return "₹0";

            double absVal = Math.Abs(val);
            string prefix = val < 0 ? "-₹" : "₹";
            if (absVal >= 10000000) return prefix + (absVal / 10000000.0).ToString("F2") + " Cr";
            if (absVal >= 100000) return prefix + (absVal / 100000.0).ToString("F2") + " L";
            return prefix + Math.Round(absVal).ToString("N0", new CultureInfo("en-IN"));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DateFormatConverter : IValueConverter
    {
        private static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString())) return "—";
            string str = value.ToString()!;
            var parts = str.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[1], out int m) && m >= 1 && m <= 12)
            {
                string day = parts[2].TrimStart('0');
                if (string.IsNullOrEmpty(day)) day = "0";
                string yr = parts[0].Length >= 2 ? parts[0].Substring(parts[0].Length - 2) : parts[0];
                return $"{day} {Months[m - 1]} {yr}";
            }
            return str;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
            return s switch
            {
                "Paid" or "Completed" or "In stock" or "ok" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B8556")),
                "Partial" or "Low" or "low" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5A62E")),
                "Unpaid" or "Out" or "out" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C23A32")),
                "Auto" or "domestic" or "Domestic" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D62B8")),
                "export" or "Export" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23968A")),
                "retail" or "Retail" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5A62E")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78839A"))
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
            return s switch
            {
                "Paid" or "Completed" or "In stock" or "ok" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1F2EA")),
                "Partial" or "Low" or "low" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBF0D6")),
                "Unpaid" or "Out" or "out" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBE5E2")),
                "Auto" or "domestic" or "Domestic" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6ECF9")),
                "export" or "Export" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F5F2")),
                "retail" or "Retail" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBF0D6")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6F7FA"))
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RouteToActiveBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")); // Vibrant Royal Blue
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RouteToActiveForegroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Brushes.White;
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")); // Crisp Slate-400
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RouteToActiveFontWeightConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return FontWeights.Bold;
            }
            return FontWeights.SemiBold;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RouteToActiveBorderBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RouteToActiveIndicatorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TabActiveBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#172443")); // Active Navy
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")); // Inactive Light
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TabActiveForegroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Brushes.White;
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")); // Slate-600
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TabActiveBorderConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#172443"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")); // Crisp Border
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
