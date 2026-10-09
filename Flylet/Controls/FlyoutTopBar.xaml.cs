using Flylet.Helpers;
using Flylet.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace Flylet.Controls
{
    public partial class FlyoutTopBar : UserControl
    {
        private static TimeSpan duration = TimeSpan.FromMilliseconds(167);
        private static IEasingFunction easingFunction = new CircleEase() { EasingMode = EasingMode.EaseOut };
        private static PropertyPath heightPath = new(HeightProperty);
        private static PropertyPath marginPath = new(MarginProperty);
        private static PropertyPath opacityPath = new("Background.Opacity");

        private Storyboard expandStoryboard;
        private Storyboard collapseStoryboard;

        private bool _topBarOverlay;
        private bool _topBarVisible = true;
        private TopBarVisibility _topBarVisibility = TopBarVisibility.Visible;

        private bool isAnimating;

        // Set by the flyout view: pointing anywhere at the flyout, not only at this strip, shows the buttons
        private bool isPointerOverFlyout;

        #region Properties

        public static readonly DependencyProperty TopBarVisibilityProperty =
            DependencyProperty.Register(
                nameof(TopBarVisibility),
                typeof(TopBarVisibility),
                typeof(FlyoutTopBar),
                new PropertyMetadata(DefaultValuesStore.DefaultTopBarVisibility, OnTopBarVisibilityPropertyChanged));

        public TopBarVisibility TopBarVisibility
        {
            get => (TopBarVisibility)GetValue(TopBarVisibilityProperty);
            set => SetValue(TopBarVisibilityProperty, value);
        }

        private static void OnTopBarVisibilityPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FlyoutTopBar topBar)
            {
                topBar.OnTopBarVisibilityChanged((TopBarVisibility)e.NewValue);
            }
        }

        #endregion

        public FlyoutTopBar()
        {
            InitializeComponent();

            PrepareAnimations();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) { return; }

            BindingOperations.SetBinding(this, TopBarVisibilityProperty,
                new Binding(nameof(UIManager.TopBarVisibility)) { Source = FlyoutHandler.Instance.UIManager, Mode = BindingMode.OneWay });
        }

        /// <summary>
        /// Tells the bar whether the pointer is over the flyout it sits on.
        /// </summary>
        public void SetPointerOverFlyout(bool isOver)
        {
            isPointerOverFlyout = isOver;

            if (isOver)
                OnMouseEnter();
            else
                OnMouseLeave();
        }

        private void OnMouseEnter()
        {
            if (isAnimating)
                return;

            if (_topBarVisibility == TopBarVisibility.AutoHide && !_topBarOverlay)
            {
                _topBarOverlay = true;
                UpdateTopBar(true);
            }
        }

        private void OnMouseLeave()
        {
            if (isAnimating)
                return;

            if (_topBarVisibility == TopBarVisibility.AutoHide && _topBarOverlay)
            {
                _topBarOverlay = false;
                UpdateTopBar(false);
            }
        }

        private void OnTopBarVisibilityChanged(TopBarVisibility value)
        {
            _topBarVisibility = value;
            _topBarVisible = value == TopBarVisibility.Visible;

            _topBarOverlay = false;
            if (value != TopBarVisibility.Collapsed && isPointerOverFlyout)
            {
                _topBarOverlay = true;
                _topBarVisible = true;
            }

            UpdateTopBar(_topBarVisible);
        }

        private void UpdateTopBar(bool showTopBar)
        {
            PrepareAnimations();

            DragBorder.Visibility = _topBarVisibility != TopBarVisibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

            if (showTopBar)
            {
                expandStoryboard?.Begin();
                isAnimating = true;
            }
            else
            {
                collapseStoryboard?.Begin();
                isAnimating = true;
            }
        }

        private void PrepareAnimations()
        {
            if (expandStoryboard == null)
            {
                DoubleAnimation heightAnim = new()
                {
                    To = 32.0,
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(heightAnim, TopBarGrid);
                Storyboard.SetTargetProperty(heightAnim, heightPath);

                ThicknessAnimation tbMarginAnim = new()
                {
                    To = new(0),
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(tbMarginAnim, TopBarGrid);
                Storyboard.SetTargetProperty(tbMarginAnim, marginPath);

                ThicknessAnimation g1MarginAnim = new()
                {
                    To = new(0),
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(g1MarginAnim, G1);
                Storyboard.SetTargetProperty(g1MarginAnim, marginPath);

                ThicknessAnimation g2MarginAnim = new()
                {
                    To = new(0),
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(g2MarginAnim, G2);
                Storyboard.SetTargetProperty(g2MarginAnim, marginPath);

                DoubleAnimation opacityAnim = new()
                {
                    To = 1.0,
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(opacityAnim, DragBorder);
                Storyboard.SetTargetProperty(opacityAnim, opacityPath);

                expandStoryboard = new()
                {
                    Children = { heightAnim, tbMarginAnim, g1MarginAnim, g2MarginAnim, opacityAnim }
                };
                expandStoryboard.Completed += (_, __) =>
                {
                    isAnimating = false;
                    if (!isPointerOverFlyout)
                    {
                        OnMouseLeave();
                    }
                };
            }
            if (collapseStoryboard == null)
            {
                DoubleAnimation heightAnim = new()
                {
                    To = 10.0,
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(heightAnim, TopBarGrid);
                Storyboard.SetTargetProperty(heightAnim, heightPath);

                ThicknessAnimation tbMarginAnim = new()
                {
                    To = new(0, 0, 0, -10),
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(tbMarginAnim, TopBarGrid);
                Storyboard.SetTargetProperty(tbMarginAnim, marginPath);

                ThicknessAnimation g1MarginAnim = new()
                {
                    To = new(-34, 0, 0, 0),
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(g1MarginAnim, G1);
                Storyboard.SetTargetProperty(g1MarginAnim, marginPath);

                ThicknessAnimation g2MarginAnim = new()
                {
                    To = new(0, 0, -34, 0),
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(g2MarginAnim, G2);
                Storyboard.SetTargetProperty(g2MarginAnim, marginPath);

                DoubleAnimation opacityAnim = new()
                {
                    To = 0.0,
                    Duration = duration,
                    EasingFunction = easingFunction
                };
                Storyboard.SetTarget(opacityAnim, DragBorder);
                Storyboard.SetTargetProperty(opacityAnim, opacityPath);

                collapseStoryboard = new()
                {
                    Children = { heightAnim, tbMarginAnim, g1MarginAnim, g2MarginAnim, opacityAnim }
                };
                collapseStoryboard.Completed += (_, __) =>
                {
                    isAnimating = false;
                    if (isPointerOverFlyout)
                    {
                        OnMouseEnter();
                    }
                };
            }
        }
    }
}
