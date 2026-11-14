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

        // ==========================================
        // DRAG-TO-SCROLL (HORIZONTAL & TABLE TOUCH/MOUSE GLIDE)
        // ==========================================
        public static readonly DependencyProperty IsDragScrollEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsDragScrollEnabled",
                typeof(bool),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(false, OnIsDragScrollEnabledChanged));

        public static bool GetIsDragScrollEnabled(DependencyObject obj) => (bool)obj.GetValue(IsDragScrollEnabledProperty);
        public static void SetIsDragScrollEnabled(DependencyObject obj, bool value) => obj.SetValue(IsDragScrollEnabledProperty, value);

        private static readonly DependencyProperty DragStartPointProperty =
            DependencyProperty.RegisterAttached("DragStartPoint", typeof(Point), typeof(SmoothScrollHelper), new PropertyMetadata(new Point()));

        private static readonly DependencyProperty DragStartHOffsetProperty =
            DependencyProperty.RegisterAttached("DragStartHOffset", typeof(double), typeof(SmoothScrollHelper), new PropertyMetadata(0.0));

        private static readonly DependencyProperty IsDraggingProperty =
            DependencyProperty.RegisterAttached("IsDragging", typeof(bool), typeof(SmoothScrollHelper), new PropertyMetadata(false));

        private static void OnIsDragScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                if ((bool)e.NewValue)
                {
                    sv.PreviewMouseDown += ScrollViewer_DragMouseDown;
                    sv.PreviewMouseMove += ScrollViewer_DragMouseMove;
                    sv.PreviewMouseUp += ScrollViewer_DragMouseUp;
                    sv.PreviewMouseWheel += ScrollViewer_HorizontalMouseWheel;
                }
                else
                {
                    sv.PreviewMouseDown -= ScrollViewer_DragMouseDown;
                    sv.PreviewMouseMove -= ScrollViewer_DragMouseMove;
                    sv.PreviewMouseUp -= ScrollViewer_DragMouseUp;
                    sv.PreviewMouseWheel -= ScrollViewer_HorizontalMouseWheel;
                }
            }
        }

        private static void ScrollViewer_DragMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;

            // Don't initiate drag if user clicked directly on interactive child control
            if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
            {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
            {
                sv.SetValue(DragStartPointProperty, e.GetPosition(sv));
                sv.SetValue(DragStartHOffsetProperty, sv.HorizontalOffset);
                sv.SetValue(IsDraggingProperty, true);
                sv.CaptureMouse();
            }
        }

        private static void ScrollViewer_DragMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;

            bool isDragging = (bool)sv.GetValue(IsDraggingProperty);
            if (!isDragging) return;

            if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed)
            {
                sv.SetValue(IsDraggingProperty, false);
                sv.ReleaseMouseCapture();
                return;
            }

            Point currentPoint = e.GetPosition(sv);
            Point startPoint = (Point)sv.GetValue(DragStartPointProperty);
            double startHOffset = (double)sv.GetValue(DragStartHOffsetProperty);

            double deltaX = currentPoint.X - startPoint.X;

            if (Math.Abs(deltaX) > 1)
            {
                double newOffset = startHOffset - deltaX;
                sv.ScrollToHorizontalOffset(Math.Clamp(newOffset, 0, sv.ScrollableWidth));
            }
        }

        private static void ScrollViewer_DragMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;

            sv.SetValue(IsDraggingProperty, false);
            if (sv.IsMouseCaptured)
            {
                sv.ReleaseMouseCapture();
            }
        }

        private static void ScrollViewer_HorizontalMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (sv.ScrollableWidth <= 0) return;

            // If Shift is pressed or this scrollviewer cannot scroll vertically, scroll horizontally
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || sv.ScrollableHeight <= 0)
            {
                double step = -(e.Delta / 120.0) * 120.0;
                double newOffset = Math.Clamp(sv.HorizontalOffset + step, 0, sv.ScrollableWidth);
                sv.ScrollToHorizontalOffset(newOffset);
                e.Handled = true;
            }
        }

        private static bool IsInteractiveControl(DependencyObject dep)
        {
            while (dep != null)
            {
                if (dep is TextBox || dep is ComboBox || dep is Button || dep is CheckBox || dep is RadioButton)
                    return true;
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            }
            return false;
        }
    }
}
