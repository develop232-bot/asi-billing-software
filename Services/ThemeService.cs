using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AsiErp.Services
{
    public static class ThemeService
    {
        public static bool IsDark { get; private set; } = false;

        // Dark Theme Colors
        private static readonly SolidColorBrush DarkPageBg = new(Color.FromRgb(11, 15, 25));       // #0B0F19
        private static readonly SolidColorBrush DarkCardBg = new(Color.FromRgb(17, 24, 39));       // #111827
        private static readonly SolidColorBrush DarkSubtleBg = new(Color.FromRgb(31, 41, 55));     // #1F2937
        private static readonly SolidColorBrush DarkBorder = new(Color.FromRgb(31, 41, 55));       // #1F2937
        private static readonly SolidColorBrush DarkTextPrimary = new(Color.FromRgb(248, 250, 252)); // #F8FAFC
        private static readonly SolidColorBrush DarkTextSecondary = new(Color.FromRgb(156, 163, 175)); // #9CA3AF
        private static readonly SolidColorBrush DarkInputBg = new(Color.FromRgb(31, 41, 55));      // #1F2937
        private static readonly SolidColorBrush DarkInputBorder = new(Color.FromRgb(55, 65, 81));   // #374151

        // Light Theme Colors (Default)
        private static readonly SolidColorBrush LightPageBg = new(Color.FromRgb(248, 250, 252));    // #F8FAFC
        private static readonly SolidColorBrush LightCardBg = new(Color.FromRgb(255, 255, 255));    // #FFFFFF
        private static readonly SolidColorBrush LightSubtleBg = new(Color.FromRgb(241, 245, 249));  // #F1F5F9
        private static readonly SolidColorBrush LightBorder = new(Color.FromRgb(226, 232, 240));    // #E2E8F0
        private static readonly SolidColorBrush LightTextPrimary = new(Color.FromRgb(15, 23, 42));  // #0F172A
        private static readonly SolidColorBrush LightTextSecondary = new(Color.FromRgb(100, 116, 139)); // #64748B
        private static readonly SolidColorBrush LightInputBg = new(Color.FromRgb(255, 255, 255));   // #FFFFFF
        private static readonly SolidColorBrush LightInputBorder = new(Color.FromRgb(203, 213, 225)); // #CBD5E1

        static ThemeService()
        {
            DarkPageBg.Freeze();
            DarkCardBg.Freeze();
            DarkSubtleBg.Freeze();
            DarkBorder.Freeze();
            DarkTextPrimary.Freeze();
            DarkTextSecondary.Freeze();
            DarkInputBg.Freeze();
            DarkInputBorder.Freeze();

            LightPageBg.Freeze();
            LightCardBg.Freeze();
            LightSubtleBg.Freeze();
            LightBorder.Freeze();
            LightTextPrimary.Freeze();
            LightTextSecondary.Freeze();
            LightInputBg.Freeze();
            LightInputBorder.Freeze();

            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is Window win && win.Name != "LoginWindow")
                {
                    ApplyToElement(win, IsDark);
                }
            }));
        }

        public static void ApplyTheme(bool isDark, Window? window = null)
        {
            IsDark = isDark;

            var app = Application.Current;
            if (app != null)
            {
                app.Resources["AppPageBackground"] = isDark ? DarkPageBg : LightPageBg;
                app.Resources["AppCardBackground"] = isDark ? DarkCardBg : LightCardBg;
                app.Resources["AppBorderBrush"] = isDark ? DarkBorder : LightBorder;
                app.Resources["AppTextPrimary"] = isDark ? DarkTextPrimary : LightTextPrimary;
                app.Resources["AppTextSecondary"] = isDark ? DarkTextSecondary : LightTextSecondary;

                foreach (Window win in app.Windows)
                {
                    try
                    {
                        ApplyToElement(win, isDark);
                    }
                    catch { }
                }
            }
            else if (window != null)
            {
                ApplyToElement(window, isDark);
            }
        }

        public static void ApplyToElement(DependencyObject root, bool isDark)
        {
            if (root == null) return;
            ApplyRecursive(root, isDark, 0);

            if (root is FrameworkElement fe)
            {
                fe.Loaded -= OnElementLoaded;
                fe.Loaded += OnElementLoaded;
            }
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                ApplyRecursive(d, IsDark, 0);
            }
        }

        private static void ApplyRecursive(DependencyObject d, bool isDark, int depth)
        {
            if (depth > 60) return;

            // Preserve Sidebar and any element explicitly marked PreserveTheme
            if (d is FrameworkElement fe && (fe.Name == "SidebarBorder" || Equals(fe.Tag, "PreserveTheme")))
            {
                return;
            }

            // Do NOT modify Buttons, RadioButtons, or ToggleButtons - they have intentional styling/colors!
            if (d is Button || d is RadioButton || (d is System.Windows.Controls.Primitives.ToggleButton && d is not CheckBox))
            {
                return;
            }

            if (d is Window win)
            {
                win.Background = isDark ? DarkPageBg : LightPageBg;
            }
            else if (d is ScrollViewer sv)
            {
                if (IsPageBackground(sv.Background))
                {
                    sv.Background = isDark ? DarkPageBg : LightPageBg;
                }
            }
            else if (d is Border b)
            {
                if (b.Name == "TopBarBorder")
                {
                    b.Background = isDark ? DarkCardBg : LightCardBg;
                    b.BorderBrush = isDark ? DarkBorder : LightBorder;
                }
                else if (b.Background is SolidColorBrush scb && scb.Color.A < 220)
                {
                    // Translucent highlight (e.g. #26FFFFFF on stat cards), preserve completely!
                }
                else if (IsCardBackground(b.Background))
                {
                    b.Background = isDark ? DarkCardBg : LightCardBg;
                    if (IsBorderBrush(b.BorderBrush))
                    {
                        b.BorderBrush = isDark ? DarkBorder : LightBorder;
                    }
                }
                else if (IsSubtleBackground(b.Background))
                {
                    b.Background = isDark ? DarkSubtleBg : LightSubtleBg;
                    if (IsBorderBrush(b.BorderBrush))
                    {
                        b.BorderBrush = isDark ? DarkBorder : LightBorder;
                    }
                }
                else if (IsPageBackground(b.Background))
                {
                    b.Background = isDark ? DarkPageBg : LightPageBg;
                }
            }
            else if (d is TextBlock tb)
            {
                if (IsDarkText(tb.Foreground))
                {
                    tb.Foreground = isDark ? DarkTextPrimary : LightTextPrimary;
                }
                else if (IsSecondaryText(tb.Foreground))
                {
                    tb.Foreground = isDark ? DarkTextSecondary : LightTextSecondary;
                }
            }
            else if (d is DataGrid dg)
            {
                dg.Background = isDark ? DarkCardBg : LightCardBg;
                dg.RowBackground = isDark ? DarkCardBg : LightCardBg;
                dg.AlternatingRowBackground = isDark ? DarkSubtleBg : LightCardBg;
                dg.HorizontalGridLinesBrush = isDark ? DarkBorder : LightBorder;
                dg.VerticalGridLinesBrush = isDark ? DarkBorder : LightBorder;
            }
            else if (d is System.Windows.Controls.Primitives.DataGridColumnHeader dgch)
            {
                dgch.Background = isDark ? DarkSubtleBg : LightSubtleBg;
                dgch.Foreground = isDark ? DarkTextSecondary : LightTextSecondary;
                dgch.BorderBrush = isDark ? DarkBorder : LightBorder;
            }
            else if (d is DataGridRow dgr)
            {
                dgr.Background = isDark ? DarkCardBg : LightCardBg;
                dgr.Foreground = isDark ? DarkTextPrimary : LightTextPrimary;
            }
            else if (d is TextBox txt)
            {
                if (IsInputBackground(txt.Background))
                {
                    txt.Background = isDark ? DarkInputBg : LightInputBg;
                    txt.Foreground = isDark ? DarkTextPrimary : LightTextPrimary;
                    txt.BorderBrush = isDark ? DarkInputBorder : LightInputBorder;
                    txt.CaretBrush = isDark ? DarkTextPrimary : LightTextPrimary;
                }
            }
            else if (d is ComboBox cb)
            {
                if (IsInputBackground(cb.Background))
                {
                    cb.Background = isDark ? DarkInputBg : LightInputBg;
                    cb.Foreground = isDark ? DarkTextPrimary : LightTextPrimary;
                    cb.BorderBrush = isDark ? DarkInputBorder : LightInputBorder;
                }
            }
            else if (d is DatePicker dp)
            {
                dp.Background = isDark ? DarkInputBg : LightInputBg;
                dp.Foreground = isDark ? DarkTextPrimary : LightTextPrimary;
                dp.BorderBrush = isDark ? DarkInputBorder : LightInputBorder;
            }
            else if (d is ListBox lb)
            {
                if (IsCardBackground(lb.Background) || IsPageBackground(lb.Background))
                {
                    lb.Background = isDark ? DarkCardBg : LightCardBg;
                    lb.Foreground = isDark ? DarkTextPrimary : LightTextPrimary;
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < count; i++)
            {
                ApplyRecursive(VisualTreeHelper.GetChild(d, i), isDark, depth + 1);
            }
        }

        private static bool IsCardBackground(Brush? b)
        {
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.A < 200) return false;
                bool isLight = c.R >= 250 && c.G >= 250 && c.B >= 250;
                bool isDark = (c.R == 17 && c.G == 24 && c.B == 39);
                return isLight || isDark;
            }
            return false;
        }

        private static bool IsSubtleBackground(Brush? b)
        {
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.A < 200) return false;
                return (c.R >= 235 && c.R <= 248 && c.G >= 240 && c.G <= 250 && c.B >= 245 && c.B <= 252) ||
                       (c.R == 31 && c.G == 41 && c.B == 55);
            }
            return false;
        }

        private static bool IsPageBackground(Brush? b)
        {
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.A < 200) return false;
                return (c.R >= 246 && c.R <= 250 && c.G >= 248 && c.G <= 252 && c.B >= 250 && c.B <= 254) ||
                       (c.R == 11 && c.G == 15 && c.B == 25);
            }
            return false;
        }

        private static bool IsBorderBrush(Brush? b)
        {
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.A < 200) return false;
                return (c.R >= 200 && c.R <= 235 && c.G >= 210 && c.G <= 240 && c.B >= 220 && c.B <= 245) ||
                       (c.R == 31 && c.G == 41 && c.B == 55) ||
                       (c.R == 55 && c.G == 65 && c.B == 81);
            }
            return false;
        }

        private static bool IsDarkText(Brush? b)
        {
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.R == 248 && c.G == 250 && c.B == 252) return true;

                if (c.R <= 60 && c.G <= 75 && c.B <= 95 && c.A > 200)
                {
                    int maxDiff = Math.Max(Math.Abs(c.R - c.G), Math.Max(Math.Abs(c.G - c.B), Math.Abs(c.R - c.B)));
                    return maxDiff <= 35;
                }
            }
            return false;
        }

        private static bool IsSecondaryText(Brush? b)
        {
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.R == 156 && c.G == 163 && c.B == 175) return true;

                if (c.R >= 65 && c.R <= 155 && c.G >= 80 && c.G <= 170 && c.B >= 100 && c.B <= 190 && c.A > 200)
                {
                    int maxDiff = Math.Max(Math.Abs(c.R - c.G), Math.Max(Math.Abs(c.G - c.B), Math.Abs(c.R - c.B)));
                    return maxDiff <= 40;
                }
            }
            return false;
        }

        private static bool IsInputBackground(Brush? b)
        {
            if (b == null) return true;
            if (b is SolidColorBrush scb)
            {
                var c = scb.Color;
                if (c.A < 200) return false;
                return (c.R >= 240 && c.G >= 240 && c.B >= 240) ||
                       (c.R == 31 && c.G == 41 && c.B == 55);
            }
            return false;
        }
    }
}
