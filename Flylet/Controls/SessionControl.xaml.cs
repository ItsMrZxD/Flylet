using Flylet.Core.Helpers;
using Flylet.Core.Media;
using Flylet.Core.Media.Control;
using ModernWpf;
using ModernWpf.Media.Animation;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Flylet.Controls
{
    public partial class SessionControl : UserControl
    {
        private static readonly Brush AccentForegroundBrush = CreateBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

        private MediaSession _mediaSession;

        #region Properties

        public static readonly DependencyProperty LayoutProperty =
            DependencyProperty.Register(
                nameof(Layout),
                typeof(MediaCardLayout),
                typeof(SessionControl),
                new PropertyMetadata(MediaCardLayout.Classic, OnLayoutChanged));

        public MediaCardLayout Layout
        {
            get => (MediaCardLayout)GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        #endregion

        public SessionControl()
        {
            InitializeComponent();

            Loaded += SessionControl_Loaded;
            Unloaded += SessionControl_Unloaded;
            DataContextChanged += SessionControl_DataContextChanged;
            RootGrid.SizeChanged += (_, _) => UpdateClip();

            ApplyLayout();
            BindingOperations.SetBinding(this, LayoutProperty,
                new Binding(nameof(UI.UIManager.MediaCardLayout)) { Source = FlyoutHandler.Instance.UIManager });
        }

        private void SessionControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MediaSession oldMediaSession)
            {
                oldMediaSession.MediaPropertiesChanging -= MediaSession_MediaPropertiesChanging;
                oldMediaSession.MediaPropertiesChanged -= MediaSession_MediaPropertiesChanged;
            }
            if (e.NewValue is MediaSession mediaSession)
            {
                _mediaSession = mediaSession;
                mediaSession.MediaPropertiesChanging += MediaSession_MediaPropertiesChanging;
                mediaSession.MediaPropertiesChanged += MediaSession_MediaPropertiesChanged;
            }

            UpdateAccentColor();
            UpdateBackgroundArt();
        }

        private void SessionControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_mediaSession != null)
            {
                _mediaSession.MediaPropertiesChanging += MediaSession_MediaPropertiesChanging;
                _mediaSession.MediaPropertiesChanged += MediaSession_MediaPropertiesChanged;
            }
        }

        private void SessionControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_mediaSession != null)
            {
                _mediaSession.MediaPropertiesChanging -= MediaSession_MediaPropertiesChanging;
                _mediaSession.MediaPropertiesChanged -= MediaSession_MediaPropertiesChanged;
            }
        }

        private void MediaSession_MediaPropertiesChanging(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                BeginTrackTransition();
            });
        }

        private void MediaSession_MediaPropertiesChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                EndTrackTransition();
                UpdateAccentColor();
                UpdateBackgroundArt();

                // The More button that opens this pane can collapse (e.g. the source dropped
                // Shuffle/Repeat/Stop entirely), leaving nothing left to close it if it's still open
                if (_mediaSession.CalculatedMoreControlsButtonVisibility != Visibility.Visible)
                {
                    ControlsSplitView.IsPaneOpen = false;
                }
            });
        }

        private void BeginTrackTransition()
        {
            ThumbnailImageBrush.BeginAnimation(Brush.OpacityProperty, null);
            TextBlockGrid.BeginAnimation(OpacityProperty, null);
            mediaArtistBlockTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            mediaTitleBlockTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);

            ThumbnailImageBrush.Opacity = 0.0;
            TextBlockGrid.Opacity = 0.0;
            mediaArtistBlockTranslateTransform.Y = 0.0;
            mediaTitleBlockTranslateTransform.Y = 0.0;
        }

        private void EndTrackTransition()
        {
            var direction = _mediaSession.TrackChangeDirection;

            var fadeAnim = new FadeInThemeAnimation() { Duration = TimeSpan.FromMilliseconds(367) };

            ThumbnailImageBrush.BeginAnimation(Brush.OpacityProperty, fadeAnim);
            TextBlockGrid.BeginAnimation(OpacityProperty, fadeAnim);

            double offset = direction switch
            {
                Core.Media.MediaPlaybackTrackChangeDirection.Forward => 300.0,
                Core.Media.MediaPlaybackTrackChangeDirection.Backward => -300.0,
                _ => 40.0,
            };

            DependencyProperty property = direction switch
            {
                Core.Media.MediaPlaybackTrackChangeDirection.Unknown => TranslateTransform.YProperty,
                _ => TranslateTransform.XProperty,
            };

            double delay = direction switch
            {
                Core.Media.MediaPlaybackTrackChangeDirection.Unknown => 100.0,
                _ => 0.0,
            };

            var anim1 = new DoubleAnimationUsingKeyFrames()
            {
                KeyFrames =
                {
                    new DiscreteDoubleKeyFrame(offset, TimeSpan.Zero),
                    new SplineDoubleKeyFrame(0, TimeSpan.FromMilliseconds(367), new KeySpline(0.1, 0.9, 0.2, 1))
                }
            };
            mediaTitleBlockTranslateTransform.BeginAnimation(property, anim1);

            var anim2 = new DoubleAnimationUsingKeyFrames()
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                KeyFrames = anim1.KeyFrames
            };
            mediaArtistBlockTranslateTransform.BeginAnimation(property, anim2);
        }

        /// <summary>
        /// Colors the play button and timeline with an accent picked from the thumbnail,
        /// or restores the default look when the thumbnail has no usable color.
        /// </summary>
        private void UpdateAccentColor()
        {
            var buttonResources = PlayPauseButton.Resources;
            var timelineResources = TimelineGrid.Resources;

            if (_mediaSession?.Thumbnail is not ImageSource thumbnail || !AccentColorHelper.TryGetAccentColor(thumbnail, out var accent))
            {
                buttonResources.Clear();
                timelineResources.Clear();
                TimelineProgressBar.ClearValue(ForegroundProperty);
                ThinTimelineFill.SetResourceReference(Border.BackgroundProperty, "SystemControlHighlightAccentBrush");
                return;
            }

            var normal = CreateBrush(accent);
            var pointerOver = CreateBrush(AccentColorHelper.Shade(accent, 0.08));
            var pressed = CreateBrush(AccentColorHelper.Shade(accent, -0.08));

            buttonResources["ButtonBackground"] = normal;
            buttonResources["ButtonBackgroundPointerOver"] = pointerOver;
            buttonResources["ButtonBackgroundPressed"] = pressed;
            buttonResources["ButtonForeground"] = AccentForegroundBrush;
            buttonResources["ButtonForegroundPointerOver"] = AccentForegroundBrush;
            buttonResources["ButtonForegroundPressed"] = AccentForegroundBrush;
            buttonResources["ButtonBorderBrush"] = Brushes.Transparent;
            buttonResources["ButtonBorderBrushPointerOver"] = Brushes.Transparent;
            buttonResources["ButtonBorderBrushPressed"] = Brushes.Transparent;

            timelineResources["SliderTrackValueFill"] = normal;
            timelineResources["SliderTrackValueFillPointerOver"] = pointerOver;
            timelineResources["SliderTrackValueFillPressed"] = pressed;
            timelineResources["SliderThumbBackground"] = normal;
            timelineResources["SliderThumbBackgroundPointerOver"] = pointerOver;
            timelineResources["SliderThumbBackgroundPressed"] = pressed;

            TimelineProgressBar.Foreground = normal;
            ThinTimelineFill.Background = normal;
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SessionControl)d).ApplyLayout();
        }

        private void ApplyLayout()
        {
            var layout = Layout;
            bool pill = layout.IsPill;

            Height = layout.Height;

            TopGrid.Height = layout.TopSectionHeight;
            TopGrid.Margin = pill
                ? new Thickness(MediaCardLayout.PillPadding, MediaCardLayout.PillPadding, 12, MediaCardLayout.PillPadding)
                : new Thickness(MediaCardLayout.Padding, MediaCardLayout.Padding, MediaCardLayout.Padding, 0);
            TimelineRow.Height = new GridLength(
                layout.EffectiveTimeline == MediaCardTimeline.Full ? MediaCardLayout.FullTimelineHeight
                : pill ? 0 : MediaCardLayout.Padding);

            // Album art tile
            double artSize = pill ? MediaCardLayout.PillArtSize : MediaCardLayout.ArtSize;
            var artCorners = new CornerRadius(pill ? artSize / 2 : layout.Shape == MediaCardShape.Square ? 0 : MediaCardLayout.RoundedCornerRadius);
            double artGap = pill ? 10 : 14;
            bool artRight = layout.Art == MediaCardArt.Right;
            ThumbnailGrid.Visibility = layout.ShowsArtTile ? Visibility.Visible : Visibility.Collapsed;
            ThumbnailGrid.Width = ThumbnailGrid.Height = artSize;
            ThumbnailPlaceholder.CornerRadius = ThumbnailBorder.CornerRadius = artCorners;
            Grid.SetColumn(ThumbnailGrid, artRight ? 3 : 0);
            ThumbnailGrid.Margin = artRight ? new Thickness(artGap, 0, 0, 0) : new Thickness(0, 0, artGap, 0);

            // Text
            AppInfoPanel.Visibility = layout.SourceVisible ? Visibility.Visible : Visibility.Collapsed;
            ArtistTextBlock.Visibility = layout.ShowArtist ? Visibility.Visible : Visibility.Collapsed;
            TextStack.VerticalAlignment = layout.ControlsBelowText ? VerticalAlignment.Top : VerticalAlignment.Center;

            // Controls
            bool playOnly = layout.Controls == MediaCardControls.PlayOnly;
            ControlsGrid.Visibility = layout.Controls == MediaCardControls.Hidden ? Visibility.Collapsed : Visibility.Visible;
            if (layout.ControlsBelowText)
            {
                Grid.SetColumn(ControlsGrid, 1);
                Grid.SetRow(ControlsGrid, 1);
                Grid.SetRowSpan(ControlsGrid, 1);
                ControlsGrid.Margin = new Thickness(0, 4, 0, 0);
                ControlsGrid.VerticalAlignment = VerticalAlignment.Bottom;
                ControlsGrid.ClearValue(WidthProperty);
                MoreControlsHost.Visibility = Visibility.Visible;
            }
            else
            {
                Grid.SetColumn(ControlsGrid, 2);
                Grid.SetRow(ControlsGrid, 0);
                Grid.SetRowSpan(ControlsGrid, 2);
                ControlsGrid.Margin = new Thickness(8, 0, 0, 0);
                ControlsGrid.VerticalAlignment = VerticalAlignment.Center;
                // The split view inside asks for far more width than its buttons need, which would
                // squeeze the title, so the column gets exactly the buttons' width
                ControlsGrid.Width = playOnly
                    ? MediaCardLayout.ButtonSize
                    : (MediaCardLayout.ButtonSize * 3) + 16;
                MoreControlsHost.Visibility = Visibility.Collapsed;
                ControlsSplitView.IsPaneOpen = false;
            }
            PreviousButton.Visibility = NextButton.Visibility = playOnly ? Visibility.Collapsed : Visibility.Visible;
            PlayPauseButton.Margin = playOnly ? new Thickness(0) : new Thickness(8, 0, 8, 0);

            // Timeline
            TimelineGrid.Visibility = layout.EffectiveTimeline == MediaCardTimeline.Full ? Visibility.Visible : Visibility.Collapsed;
            ThinTimelineGrid.Visibility = layout.EffectiveTimeline == MediaCardTimeline.Thin ? Visibility.Visible : Visibility.Collapsed;

            UpdateBackgroundArt();
            UpdateClip();
        }

        /// <summary>
        /// Shows the blurred album art behind the card when the layout asks for it and there is art.
        /// The darkened art needs light text whatever the flyout theme is, so the card goes dark on it.
        /// </summary>
        private void UpdateBackgroundArt()
        {
            bool show = Layout.Art == MediaCardArt.Background && _mediaSession?.Thumbnail != null;

            BackgroundArtGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                ThemeManager.SetRequestedTheme(this, ElementTheme.Dark);
            }
            else
            {
                ClearValue(ThemeManager.RequestedThemeProperty);
            }
        }

        /// <summary>
        /// Rounds the background art and the thin timeline with the card's corners.
        /// </summary>
        private void UpdateClip()
        {
            double radius = Layout.CornerRadius;
            RootGrid.Clip = new RectangleGeometry(new Rect(RootGrid.RenderSize), radius, radius);
        }
    }
}
