using CommunityToolkit.Mvvm.ComponentModel;
using Flylet.Controls;
using Flylet.Core.Helpers;
using Flylet.Core.Media;
using Flylet.Core.Threading;
using Flylet.Core.UI;
using Flylet.Helpers;
using ModernWpf;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flylet.UI
{
    public class UIManager : ObservableObject
    {
        public const double FlyoutWidth = 360;

        public const double DefaultVerticalSpacing = 8;

        public const double FlyoutShadowDepth = 32;

        public static Thickness FlyoutShadowMargin = GetFlyoutShadowMargin(FlyoutShadowDepth);

        private ElementTheme currentSystemTheme = ElementTheme.Dark;
        private ThemeResources themeResources;
        private ResourceDictionary lightResources;
        private ResourceDictionary darkResources;
        private Color defaultLightFlyoutBackgroundColor;
        private Color defaultDarkFlyoutBackgroundColor;
        private readonly DebounceDispatcher accentRefreshDebouncer = new();

        private bool _isThemeUpdated;

        #region Properties

        private bool restartRequired;

        public bool RestartRequired
        {
            get => restartRequired;
            set => SetProperty(ref restartRequired, value);
        }

        #region General

        private TopBarVisibility topBarVisibility = TopBarVisibility.Visible;

        public TopBarVisibility TopBarVisibility
        {
            get => topBarVisibility;
            set
            {
                if (SetProperty(ref topBarVisibility, value))
                {
                    AppDataHelper.TopBarVisibility = value;
                }
            }
        }

        private ElementTheme appTheme = DefaultValuesStore.AppTheme;

        public ElementTheme AppTheme
        {
            get => appTheme;
            set
            {
                if (SetProperty(ref appTheme, value))
                {
                    UpdateAppTheme();
                    AppDataHelper.AppTheme = value;
                }
            }
        }

        private ElementTheme flyoutTheme = DefaultValuesStore.FlyoutTheme;

        public ElementTheme FlyoutTheme
        {
            get => flyoutTheme;
            set
            {
                if (SetProperty(ref flyoutTheme, value))
                {
                    UpdateTheme();
                    AppDataHelper.FlyoutTheme = value;
                }
            }
        }

        private ElementTheme actualFlyoutTheme = ElementTheme.Dark;

        public ElementTheme ActualFlyoutTheme
        {
            get => actualFlyoutTheme;
            private set => SetProperty(ref actualFlyoutTheme, value);
        }

        private int flyoutTimeout = DefaultValuesStore.FlyoutTimeout;

        public int FlyoutTimeout
        {
            get => flyoutTimeout;
            set
            {
                if (SetProperty(ref flyoutTimeout, value))
                {
                    AppDataHelper.FlyoutTimeout = flyoutTimeout;
                }
            }
        }

        private double flyoutBackgroundOpacity = DefaultValuesStore.FlyoutBackgroundOpacity;

        public double FlyoutBackgroundOpacity
        {
            get => flyoutBackgroundOpacity;
            set
            {
                if (SetProperty(ref flyoutBackgroundOpacity, value))
                {
                    OnFlyoutBackgroundOpacityChanged();
                }
            }
        }

        private bool trayIconEnabled = DefaultValuesStore.TrayIconEnabled;

        public bool TrayIconEnabled
        {
            get => trayIconEnabled;
            set
            {
                if (SetProperty(ref trayIconEnabled, value))
                {
                    OnTrayIconEnabledChanged();
                }
            }
        }

        private bool useColoredTrayIcon = DefaultValuesStore.UseColoredTrayIcon;

        public bool UseColoredTrayIcon
        {
            get => useColoredTrayIcon;
            set
            {
                if (SetProperty(ref useColoredTrayIcon, value))
                {
                    OnUseColoredTrayIconChanged();
                }
            }
        }

        private bool flyoutAnimationEnabled = DefaultValuesStore.FlyoutAnimationEnabled;

        public bool FlyoutAnimationEnabled
        {
            get => flyoutAnimationEnabled;
            set
            {
                if (SetProperty(ref flyoutAnimationEnabled, value))
                {
                    OnFadeAnimationEnabledChanged();
                }
            }
        }

        private bool useCustomAccentColor = DefaultValuesStore.UseCustomAccentColor;

        public bool UseCustomAccentColor
        {
            get => useCustomAccentColor;
            set
            {
                if (SetProperty(ref useCustomAccentColor, value))
                {
                    ApplyAccentColor();
                    AppDataHelper.UseCustomAccentColor = value;
                }
            }
        }

        private Color customAccentColor = (Color)ColorConverter.ConvertFromString(DefaultValuesStore.CustomAccentColor);

        public Color CustomAccentColor
        {
            get => customAccentColor;
            set
            {
                if (SetProperty(ref customAccentColor, value))
                {
                    if (useCustomAccentColor)
                    {
                        ApplyAccentColor();
                    }
                    AppDataHelper.CustomAccentColor = value.ToString();
                }
            }
        }

        private bool useCustomFlyoutBackgroundColor = DefaultValuesStore.UseCustomFlyoutBackgroundColor;

        public bool UseCustomFlyoutBackgroundColor
        {
            get => useCustomFlyoutBackgroundColor;
            set
            {
                if (SetProperty(ref useCustomFlyoutBackgroundColor, value))
                {
                    UpdateTheme();
                    AppDataHelper.UseCustomFlyoutBackgroundColor = value;
                }
            }
        }

        private Color customFlyoutBackgroundColor = (Color)ColorConverter.ConvertFromString(DefaultValuesStore.CustomFlyoutBackgroundColor);

        public Color CustomFlyoutBackgroundColor
        {
            get => customFlyoutBackgroundColor;
            set
            {
                if (SetProperty(ref customFlyoutBackgroundColor, value))
                {
                    if (useCustomFlyoutBackgroundColor)
                    {
                        UpdateTheme();
                    }
                    AppDataHelper.CustomFlyoutBackgroundColor = value.ToString();
                }
            }
        }

        private static string FlyoutBackgroundImagePath =>
            Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "FlyoutBackground.jpg");

        private bool useFlyoutBackgroundImage = DefaultValuesStore.UseFlyoutBackgroundImage;

        public bool UseFlyoutBackgroundImage
        {
            get => useFlyoutBackgroundImage;
            set
            {
                if (SetProperty(ref useFlyoutBackgroundImage, value))
                {
                    OnPropertyChanged(nameof(IsFlyoutBackgroundImageShown));
                    UpdateTheme();
                    AppDataHelper.UseFlyoutBackgroundImage = value;
                }
            }
        }

        private ImageSource flyoutBackgroundImage;

        /// <summary>
        /// The imported copy of the user's photo, or null before one is picked.
        /// </summary>
        public ImageSource FlyoutBackgroundImage
        {
            get => flyoutBackgroundImage;
            private set
            {
                if (SetProperty(ref flyoutBackgroundImage, value))
                {
                    OnPropertyChanged(nameof(IsFlyoutBackgroundImageShown));
                    UpdateTheme();
                }
            }
        }

        public bool IsFlyoutBackgroundImageShown => useFlyoutBackgroundImage && flyoutBackgroundImage != null;

        private double flyoutBackgroundImageDim = DefaultValuesStore.FlyoutBackgroundImageDim;

        /// <summary>
        /// How much the photo is darkened, in percent. Never fully clear, so light text stays readable.
        /// </summary>
        public double FlyoutBackgroundImageDim
        {
            get => flyoutBackgroundImageDim;
            set
            {
                value = Math.Clamp(value, MinFlyoutBackgroundImageDim, 100);
                if (SetProperty(ref flyoutBackgroundImageDim, value))
                {
                    OnPropertyChanged(nameof(FlyoutBackgroundImageDimOpacity));
                    AppDataHelper.FlyoutBackgroundImageDim = value;
                }
            }
        }

        public const double MinFlyoutBackgroundImageDim = 20;

        public double FlyoutBackgroundImageDimOpacity => flyoutBackgroundImageDim / 100;

        /// <summary>
        /// Imports the picture at <paramref name="path"/> as the flyout background and turns it on.
        /// </summary>
        /// <returns>False if the file couldn't be read as a picture; the current photo stays.</returns>
        public bool SetFlyoutBackgroundImage(string path)
        {
            if (!BackgroundImageHelper.TryImport(path, FlyoutBackgroundImagePath))
                return false;

            FlyoutBackgroundImage = BackgroundImageHelper.TryLoad(FlyoutBackgroundImagePath);
            UseFlyoutBackgroundImage = true;
            return FlyoutBackgroundImage != null;
        }

        #endregion

        #region Layout

        private FlyoutWindowPlacementMode onScreenFlyoutWindowPlacementMode;

        public FlyoutWindowPlacementMode OnScreenFlyoutWindowPlacementMode
        {
            get => onScreenFlyoutWindowPlacementMode;
            set
            {
                if (SetProperty(ref onScreenFlyoutWindowPlacementMode, value))
                {
                    AppDataHelper.OnScreenFlyoutWindowPlacementMode = value;
                }
            }
        }

        private FlyoutWindowAlignments onScreenFlyoutWindowAlignment;

        public FlyoutWindowAlignments OnScreenFlyoutWindowAlignment
        {
            get => onScreenFlyoutWindowAlignment;
            set
            {
                if (SetProperty(ref onScreenFlyoutWindowAlignment, value))
                {
                    AppDataHelper.OnScreenFlyoutWindowAlignment = value;
                }
            }
        }

        private Thickness onScreenFlyoutWindowMargin;

        public Thickness OnScreenFlyoutWindowMargin
        {
            get => onScreenFlyoutWindowMargin;
            set
            {
                if (SetProperty(ref onScreenFlyoutWindowMargin, value))
                {
                    AppDataHelper.OnScreenFlyoutWindowMargin = value;
                }
            }
        }

        private FlyoutWindowExpandDirection onScreenFlyoutWindowExpandDirection;

        public FlyoutWindowExpandDirection OnScreenFlyoutWindowExpandDirection
        {
            get => onScreenFlyoutWindowExpandDirection;
            set
            {
                if (SetProperty(ref onScreenFlyoutWindowExpandDirection, value))
                {
                    AppDataHelper.OnScreenFlyoutWindowExpandDirection = value;
                }
            }
        }

        private StackingDirection onScreenFlyoutContentStackingDirection;

        public StackingDirection OnScreenFlyoutContentStackingDirection
        {
            get => onScreenFlyoutContentStackingDirection;
            set
            {
                if (SetProperty(ref onScreenFlyoutContentStackingDirection, value))
                {
                    AppDataHelper.OnScreenFlyoutContentStackingDirection = value;
                }
            }
        }

        #endregion

        #region Media Controls

        private MediaCardLayout mediaCardLayout = DefaultValuesStore.MediaCardLayout;

        /// <summary>
        /// How the media card looks. The single options below and the preset all read and write this.
        /// </summary>
        public MediaCardLayout MediaCardLayout
        {
            get => mediaCardLayout;
            private set
            {
                if (SetProperty(ref mediaCardLayout, value))
                {
                    OnMediaCardLayoutChanged();
                }
            }
        }

        public MediaCardPreset MediaCardPreset
        {
            get => mediaCardLayout.Preset;
            set
            {
                // Custom isn't a layout of its own, picking it keeps whatever is set now
                if (value != MediaCardPreset.Custom)
                {
                    MediaCardLayout = MediaCardLayout.FromPreset(value);
                }
            }
        }

        public MediaCardArt MediaCardArt
        {
            get => mediaCardLayout.Art;
            set => MediaCardLayout = mediaCardLayout with { Art = value };
        }

        public bool MediaCardShowSource
        {
            get => mediaCardLayout.ShowSource;
            set => MediaCardLayout = mediaCardLayout with { ShowSource = value };
        }

        public bool MediaCardShowArtist
        {
            get => mediaCardLayout.ShowArtist;
            set => MediaCardLayout = mediaCardLayout with { ShowArtist = value };
        }

        public MediaCardTimeline MediaCardTimeline
        {
            get => mediaCardLayout.Timeline;
            set => MediaCardLayout = mediaCardLayout with { Timeline = value };
        }

        public MediaCardControls MediaCardControls
        {
            get => mediaCardLayout.Controls;
            set => MediaCardLayout = mediaCardLayout with { Controls = value };
        }

        public MediaCardShape MediaCardShape
        {
            get => mediaCardLayout.Shape;
            set => MediaCardLayout = mediaCardLayout with { Shape = value };
        }

        private double sessionControlHeight = MediaCardLayout.Classic.Height;

        public double SessionControlHeight
        {
            get => sessionControlHeight;
            private set => SetProperty(ref sessionControlHeight, value);
        }

        private Orientation sessionsPanelOrientation = DefaultValuesStore.SessionsPanelOrientation;

        public Orientation SessionsPanelOrientation
        {
            get => sessionsPanelOrientation;
            set
            {
                if (SetProperty(ref sessionsPanelOrientation, value))
                {
                    OnSessionsPanelOrientation();
                }
            }
        }

        private int maxVerticalSessionControlsCount = DefaultValuesStore.MaxVerticalSessionControlsCount;

        public int MaxVerticalSessionControlsCount
        {
            get => maxVerticalSessionControlsCount;
            set
            {
                if (SetProperty(ref maxVerticalSessionControlsCount, value))
                {
                    OnMaxVerticalSessionControlsCount();
                }
            }
        }

        private double calculatedSessionsPanelMaxHeight = MediaCardLayout.Classic.Height;

        public double CalculatedSessionsPanelMaxHeight
        {
            get => calculatedSessionsPanelMaxHeight;
            private set => SetProperty(ref calculatedSessionsPanelMaxHeight, value);
        }

        private double calculatedSessionsPanelSpacing;

        public double CalculatedSessionsPanelSpacing
        {
            get => calculatedSessionsPanelSpacing;
            private set => SetProperty(ref calculatedSessionsPanelSpacing, value);
        }

        #endregion

        #endregion

        public void Initialize()
        {
            OnScreenFlyoutWindowPlacementMode = AppDataHelper.OnScreenFlyoutWindowPlacementMode;
            OnScreenFlyoutWindowAlignment = AppDataHelper.OnScreenFlyoutWindowAlignment;
            OnScreenFlyoutWindowMargin = AppDataHelper.OnScreenFlyoutWindowMargin;
            OnScreenFlyoutWindowExpandDirection = AppDataHelper.OnScreenFlyoutWindowExpandDirection;
            OnScreenFlyoutContentStackingDirection = AppDataHelper.OnScreenFlyoutContentStackingDirection;

            TopBarVisibility = AppDataHelper.TopBarVisibility;
            FlyoutTimeout = AppDataHelper.FlyoutTimeout;
            MediaCardLayout = new MediaCardLayout(
                AppDataHelper.MediaCardArt,
                AppDataHelper.MediaCardShowSource,
                AppDataHelper.MediaCardShowArtist,
                AppDataHelper.MediaCardTimeline,
                AppDataHelper.MediaCardControls,
                AppDataHelper.MediaCardShape);
            ApplyMediaCardSize();
            MaxVerticalSessionControlsCount = AppDataHelper.MaxVerticalSessionControlsCount;
            SessionsPanelOrientation = AppDataHelper.SessionsPanelOrientation;

            themeResources = (ThemeResources)Application.Current.Resources
                .MergedDictionaries.FirstOrDefault(x => x is ThemeResources);
            lightResources = themeResources.ThemeDictionaries["Light"];
            darkResources = themeResources.ThemeDictionaries["Dark"];
            defaultLightFlyoutBackgroundColor = ((SolidColorBrush)lightResources["FlyoutBackground"]).Color;
            defaultDarkFlyoutBackgroundColor = ((SolidColorBrush)darkResources["FlyoutBackground"]).Color;

            FlyoutBackgroundOpacity = AppDataHelper.FlyoutBackgroundOpacity;

            TrayIconManager.SetupTrayIcon();

            TrayIconEnabled = AppDataHelper.TrayIconEnabled;
            UseColoredTrayIcon = AppDataHelper.UseColoredTrayIcon;
            FlyoutAnimationEnabled = AppDataHelper.FlyoutAnimationEnabled;

            // Straight into the fields: the setters would re-save what was just read and rebuild the accent
            // more than once, and the background is applied by the theme update below, once the system theme is known
            customAccentColor = ParseColorOrDefault(AppDataHelper.CustomAccentColor, DefaultValuesStore.CustomAccentColor);
            useCustomAccentColor = AppDataHelper.UseCustomAccentColor;
            customFlyoutBackgroundColor = ParseColorOrDefault(AppDataHelper.CustomFlyoutBackgroundColor, DefaultValuesStore.CustomFlyoutBackgroundColor);
            useCustomFlyoutBackgroundColor = AppDataHelper.UseCustomFlyoutBackgroundColor;
            useFlyoutBackgroundImage = AppDataHelper.UseFlyoutBackgroundImage;
            flyoutBackgroundImageDim = Math.Clamp(AppDataHelper.FlyoutBackgroundImageDim, MinFlyoutBackgroundImageDim, 100);
            flyoutBackgroundImage = BackgroundImageHelper.TryLoad(FlyoutBackgroundImagePath);
            if (useCustomAccentColor)
            {
                ApplyAccentColor();
            }

            FlyoutTheme = AppDataHelper.FlyoutTheme;
            AppTheme = AppDataHelper.AppTheme;

            SystemTheme.SystemThemeChanged += OnSystemThemeChanged;
            SystemTheme.Initialize();
        }

        private void OnFlyoutBackgroundOpacityChanged()
        {
            UpdateFlyoutBackground();
            AppDataHelper.FlyoutBackgroundOpacity = flyoutBackgroundOpacity;
        }

        private void OnTrayIconEnabledChanged()
        {
            TrayIconManager.UpdateTrayIconVisibility(trayIconEnabled);
            AppDataHelper.TrayIconEnabled = TrayIconEnabled;
        }

        private void OnUseColoredTrayIconChanged()
        {
            UpdateTrayIcon();
            AppDataHelper.UseColoredTrayIcon = useColoredTrayIcon;
        }

        private void OnFadeAnimationEnabledChanged()
        {
            AppDataHelper.FlyoutAnimationEnabled = flyoutAnimationEnabled;
        }

        private void OnSystemThemeChanged(object sender, SystemThemeChangedEventArgs args)
        {
            currentSystemTheme = args.IsSystemLightTheme ? ElementTheme.Light : ElementTheme.Dark;
            UpdateTheme();
        }

        /// <summary>
        /// Picks up a changed system accent once a burst of colorization change messages settles.
        /// </summary>
        /// <remarks>
        /// DWM sends WM_DWMCOLORIZATIONCOLORCHANGED several times per change, and a stream of them while the
        /// color animates, sometimes before the new accent can be read. Each refresh rebuilds every accent resource.
        /// </remarks>
        public void QueueSystemAccentColorRefresh()
        {
            accentRefreshDebouncer.Debounce(TimeSpan.FromMilliseconds(250), () =>
            {
                if (!useCustomAccentColor)
                {
                    ApplyAccentColor();
                }
            });
        }

        private void ApplyAccentColor()
        {
            if (useCustomAccentColor)
            {
                themeResources.AccentColor = customAccentColor;
                return;
            }

            // ModernWpf only detects the system accent when AccentColor becomes null, and assigning null
            // again is a no-op, so a throwaway value forces the re-detection
            themeResources.AccentColor = Colors.Transparent;
            themeResources.AccentColor = null;
        }

        private static Color ParseColorOrDefault(string value, string defaultValue)
        {
            return AccentColorHelper.TryParseColor(value, out var color)
                ? color
                : (Color)ColorConverter.ConvertFromString(defaultValue);
        }

        private void UpdateAppTheme()
        {
            ThemeManager.Current.ApplicationTheme = appTheme switch
            {
                ElementTheme.Default => null,
                ElementTheme.Light => ApplicationTheme.Light,
                ElementTheme.Dark => ApplicationTheme.Dark,
                _ => null,
            };
        }

        private void UpdateTheme()
        {
            // The flyout's text and controls come from its theme, so a custom background picks the theme
            // that stays readable on it instead of the flyout theme setting. The photo is always darkened.
            ActualFlyoutTheme = IsFlyoutBackgroundImageShown ? ElementTheme.Dark
                : useCustomFlyoutBackgroundColor
                ? (AccentColorHelper.IsLight(customFlyoutBackgroundColor) ? ElementTheme.Light : ElementTheme.Dark)
                : (flyoutTheme == ElementTheme.Default ? currentSystemTheme : flyoutTheme);

            if (!_isThemeUpdated)
            {
                _isThemeUpdated = true;
            }

            UpdateFlyoutBackground();
            UpdateTrayIcon();
        }

        private void UpdateFlyoutBackground()
        {
            if (!_isThemeUpdated) return;

            var themeResource = actualFlyoutTheme == ElementTheme.Light ? lightResources : darkResources;
            var defaultColor = actualFlyoutTheme == ElementTheme.Light ? defaultLightFlyoutBackgroundColor : defaultDarkFlyoutBackgroundColor;
            var color = useCustomFlyoutBackgroundColor ? customFlyoutBackgroundColor : defaultColor;
            themeResource["FlyoutBackground"] = new SolidColorBrush(color) { Opacity = flyoutBackgroundOpacity * 0.01 };
        }

        private void UpdateTrayIcon()
        {
            if (!_isThemeUpdated) return;

            TrayIconManager.UpdateTrayIconInternal(currentSystemTheme, useColoredTrayIcon);
        }

        private void OnMaxVerticalSessionControlsCount()
        {
            UpdateCalculatedSessionsPanelMaxHeight();
            AppDataHelper.MaxVerticalSessionControlsCount = maxVerticalSessionControlsCount;
        }

        private void OnSessionsPanelOrientation()
        {
            UpdateCalculatedSessionsPanelMaxHeight();
            AppDataHelper.SessionsPanelOrientation = sessionsPanelOrientation;
        }

        private void UpdateCalculatedSessionsPanelMaxHeight()
        {
            if (sessionsPanelOrientation == Orientation.Vertical)
            {
                var n = maxVerticalSessionControlsCount;
                CalculatedSessionsPanelMaxHeight = (sessionControlHeight * n) + (DefaultVerticalSpacing * (n - 1));
                CalculatedSessionsPanelSpacing = DefaultVerticalSpacing;
            }
            else
            {
                CalculatedSessionsPanelMaxHeight = sessionControlHeight;
                CalculatedSessionsPanelSpacing = 0;
            }
        }

        private void OnMediaCardLayoutChanged()
        {
            OnPropertyChanged(nameof(MediaCardPreset));
            OnPropertyChanged(nameof(MediaCardArt));
            OnPropertyChanged(nameof(MediaCardShowSource));
            OnPropertyChanged(nameof(MediaCardShowArtist));
            OnPropertyChanged(nameof(MediaCardTimeline));
            OnPropertyChanged(nameof(MediaCardControls));
            OnPropertyChanged(nameof(MediaCardShape));

            ApplyMediaCardSize();

            AppDataHelper.MediaCardArt = mediaCardLayout.Art;
            AppDataHelper.MediaCardShowSource = mediaCardLayout.ShowSource;
            AppDataHelper.MediaCardShowArtist = mediaCardLayout.ShowArtist;
            AppDataHelper.MediaCardTimeline = mediaCardLayout.Timeline;
            AppDataHelper.MediaCardControls = mediaCardLayout.Controls;
            AppDataHelper.MediaCardShape = mediaCardLayout.Shape;
        }

        /// <summary>
        /// The media card's height and corners depend on its layout; the flyout's media card chrome
        /// and the sessions panel's paging both follow these.
        /// </summary>
        private void ApplyMediaCardSize()
        {
            SessionControlHeight = mediaCardLayout.Height;
            Application.Current.Resources["MediaCardCornerRadius"] = new CornerRadius(mediaCardLayout.CornerRadius);
            UpdateCalculatedSessionsPanelMaxHeight();
        }

        internal static Thickness GetFlyoutShadowMargin(double depth)
        {
            double radius = 0.9 * depth;
            double offset = 0.4 * depth;

            return new Thickness(
                radius,
                radius,
                radius,
                radius + offset);
        }
    }

    public enum TopBarVisibility
    {
        Visible = 0,
        AutoHide = 1,
        Collapsed = 2
    }
}
