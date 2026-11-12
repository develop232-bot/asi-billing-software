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

        public static readonly DependencyProperty BubbleMouseWheelProperty =
            DependencyProperty.RegisterAttached(
                "BubbleMouseWheel",
                typeof(bool),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(false, OnBubbleMouseWheelChanged));

        public static bool GetBubbleMouseWheel(DependencyObject obj) => (bool)obj.GetValue(BubbleMouseWheelProperty);
        public static void SetBubbleMouseWheel(DependencyObject obj, bool value) => obj.SetValue(BubbleMouseWheelProperty, value);

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

        private static readonly DependencyProperty IsAnimatingProperty =
            DependencyProperty.RegisterAttached(
                "IsAnimating",
                typeof(bool),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CurrentVerticalOffsetProperty =
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

        private static void OnBubbleMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseWheel += Element_PreviewMouseWheel;
                }
                else
                {
                    element.PreviewMouseWheel -= Element_PreviewMouseWheel;
                }
            }
        }

        private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not UIElement element || e.Handled) return;

            // Find parent ScrollViewer
            DependencyObject current = element;
            ScrollViewer? parentScroll = null;
            while (current != null)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                if (current is ScrollViewer sv)
                {
                    parentScroll = sv;
                    break;
                }
            }

            if (parentScroll != null && parentScroll.ScrollableHeight > 0)
            {
                // Forward the event to parent ScrollViewer smoothly
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                parentScroll.RaiseEvent(eventArg);
                e.Handled = true;
            }
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            // Only sync target if the user manually dragged the scrollbar thumb or used page keys (not during animation)
            bool isAnimating = (bool)scrollViewer.GetValue(IsAnimatingProperty);
            if (!isAnimating && Math.Abs(e.VerticalChange) > 0)
            {
                scrollViewer.SetValue(TargetOffsetProperty, scrollViewer.VerticalOffset);
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

            // Elapsed milliseconds between wheel notches
            double elapsedMs = (lastTime > 0) ? (now - lastTime) * 1000.0 / Stopwatch.Frequency : 1000.0;

            // Momentum acceleration: continuous flicks accelerate smoothly up to 2.8x
            double multiplier = (double)scrollViewer.GetValue(VelocityMultiplierProperty);
            if (elapsedMs < 180)
            {
                multiplier = Math.Min(2.8, multiplier + 0.35);
            }
            else
            {
                multiplier = 1.0;
            }
            scrollViewer.SetValue(VelocityMultiplierProperty, multiplier);

            // Web-like continuous scroll step: ~135px per notch * multiplier
            double step = -(delta / 120.0) * 135.0 * multiplier;

            double currentTarget = (double)scrollViewer.GetValue(TargetOffsetProperty);

            // If animation wasn't running or direction reversed, start from actual current position
            bool isAnimating = (bool)scrollViewer.GetValue(IsAnimatingProperty);
            if (!isAnimating ||
                (step > 0 && currentTarget < scrollViewer.VerticalOffset) ||
                (step < 0 && currentTarget > scrollViewer.VerticalOffset))
            {
                currentTarget = scrollViewer.VerticalOffset;
            }

            double newTarget = Math.Clamp(currentTarget + step, 0, scrollViewer.ScrollableHeight);
            scrollViewer.SetValue(TargetOffsetProperty, newTarget);
            scrollViewer.SetValue(IsAnimatingProperty, true);

            // Silky-smooth website-like glide: 260ms with CubicEase Out
            var animation = new DoubleAnimation
            {
                From = scrollViewer.VerticalOffset,
                To = newTarget,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, args) =>
            {
                scrollViewer.SetValue(IsAnimatingProperty, false);
                scrollViewer.SetValue(TargetOffsetProperty, scrollViewer.VerticalOffset);
            };

            scrollViewer.BeginAnimation(CurrentVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
            e.Handled = true;
        }
    }
}
