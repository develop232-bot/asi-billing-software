using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AsiErp.Helpers
{
    public static class SmoothScrollHelper
    {
        public static readonly DependencyProperty IsSmoothScrollEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsSmoothScrollEnabled",
                typeof(bool),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(false, OnIsSmoothScrollEnabledChanged));

        public static bool GetIsSmoothScrollEnabled(DependencyObject obj) => (bool)obj.GetValue(IsSmoothScrollEnabledProperty);
        public static void SetIsSmoothScrollEnabled(DependencyObject obj, bool value) => obj.SetValue(IsSmoothScrollEnabledProperty, value);

        private static readonly DependencyProperty TargetOffsetProperty =
            DependencyProperty.RegisterAttached(
                "TargetOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(0.0));

        private static readonly DependencyProperty LastWheelTimeProperty =
            DependencyProperty.RegisterAttached(
                "LastWheelTime",
                typeof(long),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(0L));

        private static readonly DependencyProperty VelocityMultiplierProperty =
            DependencyProperty.RegisterAttached(
                "VelocityMultiplier",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(1.0));

        private static readonly DependencyProperty CurrentVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "CurrentVerticalOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(0.0, OnCurrentVerticalOffsetChanged));

        private static void OnCurrentVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        private static void OnIsSmoothScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                {
                    scrollViewer.CanContentScroll = false;
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                }
                else
                {
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                }
            }
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            // If user scrolled via scrollbar thumb drag or keyboard, keep target in sync
            if (Math.Abs(e.VerticalChange) > 0 && Math.Abs(e.VerticalOffset - (double)scrollViewer.GetValue(TargetOffsetProperty)) > 250)
            {
                scrollViewer.SetValue(TargetOffsetProperty, e.VerticalOffset);
            }
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;
            if (scrollViewer.ScrollableHeight <= 0) return;

            double delta = e.Delta;
            if (Math.Abs(delta) < 0.001) return;

            long now = Stopwatch.GetTimestamp();
            long lastTime = (long)scrollViewer.GetValue(LastWheelTimeProperty);
            scrollViewer.SetValue(LastWheelTimeProperty, now);

            // Calculate elapsed time in ms between consecutive wheel clicks
            double elapsedMs = (lastTime > 0) ? (now - lastTime) * 1000.0 / Stopwatch.Frequency : 1000.0;

            // Velocity acceleration: rapid scrolling speeds up naturally (up to 2.5x)
            double multiplier = (double)scrollViewer.GetValue(VelocityMultiplierProperty);
            if (elapsedMs < 160)
            {
                multiplier = Math.Min(2.5, multiplier + 0.45);
            }
            else
            {
                multiplier = 1.0;
            }
            scrollViewer.SetValue(VelocityMultiplierProperty, multiplier);

            // Fast, satisfying base step: 180px per wheel click * multiplier
            double step = -(delta / 120.0) * 180.0 * multiplier;

            double currentTarget = (double)scrollViewer.GetValue(TargetOffsetProperty);

            // If direction changed (scrolling other way), snap target to current offset immediately
            if ((step > 0 && currentTarget < scrollViewer.VerticalOffset) ||
                (step < 0 && currentTarget > scrollViewer.VerticalOffset))
            {
                currentTarget = scrollViewer.VerticalOffset;
            }

            double newTarget = Math.Clamp(currentTarget + step, 0, scrollViewer.ScrollableHeight);
            scrollViewer.SetValue(TargetOffsetProperty, newTarget);

            // Snappy, silky-smooth animation: 120ms with QuadraticEase
            var animation = new DoubleAnimation
            {
                From = scrollViewer.VerticalOffset,
                To = newTarget,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            scrollViewer.BeginAnimation(CurrentVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
            e.Handled = true;
        }
    }
}
