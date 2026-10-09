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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;

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

        private TopBarVisibility topBarVisibility = DefaultValuesStore.DefaultTopBarVisibility;

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

        // An animated photo keeps its own file; when it exists it wins over the still one
        private static string FlyoutBackgroundGifPath =>
            Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "FlyoutBackground.gif");

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
                    if (value && animatedBackground == null)
                    {
                        // A saved GIF isn't decoded at startup while the photo is off
                        LoadAnimatedBackground();
                    }

                    UpdateBackgroundAnimation();
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

        private double flyoutBackgroundBlur = DefaultValuesStore.FlyoutBackgroundBlur;

        /// <summary>
        /// How soft the photo and album-art backgrounds are, 0 (sharp) to 100.
        /// </summary>
        public double FlyoutBackgroundBlur
        {
            get => flyoutBackgroundBlur;
            set
            {
                value = Math.Clamp(value, 0, 100);
                if (SetProperty(ref flyoutBackgroundBlur, value))
                {
                    OnPropertyChanged(nameof(FlyoutBackgroundBlurRadius));
                    OnPropertyChanged(nameof(AlbumBackgroundBlurRadius));
                    AppDataHelper.FlyoutBackgroundBlur = value;
                }
            }
        }

        /// <summary>
        /// Blur radius in pixels for the photo; 50% is the radius 0.11 shipped with.
        /// </summary>
        public double FlyoutBackgroundBlurRadius => flyoutBackgroundBlur * 0.6;

        /// <summary>
        /// The album art is blurred a bit more than a photo at the same setting (it's small and busy), which
        /// keeps the default album look exactly as shipped.
        /// </summary>
        public double AlbumBackgroundBlurRadius => FlyoutBackgroundBlurRadius * 1.6;

        private double flyoutBackgroundImageZoom = DefaultValuesStore.FlyoutBackgroundImageZoom;

        /// <summary>
        /// How far the photo is zoomed in, in percent: 100 shows as much of it as fits.
        /// </summary>
        public double FlyoutBackgroundImageZoom
        {
            get => flyoutBackgroundImageZoom;
            set
            {
                value = Math.Clamp(value, 100, 300);
                if (SetProperty(ref flyoutBackgroundImageZoom, value))
                {
                    AppDataHelper.FlyoutBackgroundImageZoom = value;
                }
            }
        }

        private double flyoutBackgroundImagePositionX = DefaultValuesStore.FlyoutBackgroundImagePositionX;

        /// <summary>
        /// Which part of the photo shows sideways, 0 (left edge) to 100 (right edge).
        /// </summary>
        public double FlyoutBackgroundImagePositionX
        {
            get => flyoutBackgroundImagePositionX;
            set
            {
                value = Math.Clamp(value, 0, 100);
                if (SetProperty(ref flyoutBackgroundImagePositionX, value))
                {
                    AppDataHelper.FlyoutBackgroundImagePositionX = value;
                }
            }
        }

        private double flyoutBackgroundImagePositionY = DefaultValuesStore.FlyoutBackgroundImagePositionY;

        /// <summary>
        /// Which part of the photo shows up and down, 0 (top) to 100 (bottom).
        /// </summary>
        public double FlyoutBackgroundImagePositionY
        {
            get => flyoutBackgroundImagePositionY;
            set
            {
                value = Math.Clamp(value, 0, 100);
                if (SetProperty(ref flyoutBackgroundImagePositionY, value))
                {
                    AppDataHelper.FlyoutBackgroundImagePositionY = value;
                }
            }
        }

        /// <summary>
        /// Turns the photo a quarter turn; the framing starts over because the picture's shape changed.
        /// </summary>
        public bool RotateFlyoutBackgroundImage(bool clockwise)
        {
            if (animatedBackground != null)
            {
                int turns = AppDataHelper.FlyoutBackgroundAnimationTurns;
                AppDataHelper.FlyoutBackgroundAnimationTurns = (turns + (clockwise ? 1 : 3)) % 4;
                ResetFlyoutBackgroundImageFraming();
                ShowAnimatedBackground(AnimatedBackgroundHelper.Rotate(animatedBackground, clockwise), animationFrame);
                return true;
            }

            if (!BackgroundImageHelper.TryRotate(FlyoutBackgroundImagePath, clockwise))
                return false;

            ResetFlyoutBackgroundImageFraming();
            FlyoutBackgroundImage = BackgroundImageHelper.TryLoad(FlyoutBackgroundImagePath);
            return FlyoutBackgroundImage != null;
        }

        /// <summary>
        /// Shows the whole photo again, centered.
        /// </summary>
        public void ResetFlyoutBackgroundImageFraming()
        {
            FlyoutBackgroundImageZoom = DefaultValuesStore.FlyoutBackgroundImageZoom;
            FlyoutBackgroundImagePositionX = DefaultValuesStore.FlyoutBackgroundImagePositionX;
            FlyoutBackgroundImagePositionY = DefaultValuesStore.FlyoutBackgroundImagePositionY;
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

            ClearAnimatedBackground(deleteFile: true);
            FlyoutBackgroundImage = BackgroundImageHelper.TryLoad(FlyoutBackgroundImagePath);
            ResetFlyoutBackgroundImageFraming();
            UseFlyoutBackgroundImage = true;
            return FlyoutBackgroundImage != null;
        }

        /// <summary>
        /// Like <see cref="SetFlyoutBackgroundImage"/>, and an animated GIF is accepted too: it's decoded off
        /// the UI thread, which can take a few seconds for a big one.
        /// </summary>
        public async Task<bool> SetFlyoutBackgroundImageAsync(string path)
        {
            // The saved GIF may still be open for reading (startup, or the photo just turned on); replacing or
            // deleting it then fails, and a GIF left behind would win over a newly chosen photo after a restart
            if (pendingAnimatedLoad is Task pending)
            {
                try { await pending; } catch { }
            }

            string tempPath = FlyoutBackgroundGifPath + ".tmp";

            // Off the UI thread: counting a big GIF's frames means reading the whole file
            var (isGif, loaded) = await Task.Run(() =>
            {
                try
                {
                    if (!string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase)
                        || !AnimatedBackgroundHelper.IsAnimatedGif(path))
                    {
                        return (false, (AnimatedBackground)null);
                    }

                    if (new FileInfo(path).Length > AnimatedBackgroundHelper.MaxFileSize)
                        return (true, null);

                    File.Copy(path, tempPath, true);
                    return (true, LoadAndCleanUp(() => AnimatedBackgroundHelper.TryLoad(tempPath)));
                }
                catch
                {
                    return (true, null);
                }
            });

            if (!isGif)
            {
                return SetFlyoutBackgroundImage(path);
            }

            try
            {
                if (loaded == null)
                    return false;

                File.Move(tempPath, FlyoutBackgroundGifPath, true);
            }
            catch
            {
                return false;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }

            try { File.Delete(FlyoutBackgroundImagePath); } catch { }

            ClearAnimatedBackground(deleteFile: false);
            AppDataHelper.FlyoutBackgroundAnimationTurns = 0;
            ShowAnimatedBackground(loaded);
            ResetFlyoutBackgroundImageFraming();
            UseFlyoutBackgroundImage = true;
            return true;
        }

        #region Animated background

        private AnimatedBackground animatedBackground;
        private DispatcherTimer animationTimer;
        private int animationFrame;
        private int animationViewers;
        private bool isLoadingAnimatedBackground;
        private Task pendingAnimatedLoad;

        public bool IsFlyoutBackgroundAnimated => animatedBackground != null;

        /// <summary>
        /// Called by every visible background layer; the animation only runs while at least one is on screen.
        /// </summary>
        public void AcquireBackgroundAnimation()
        {
            animationViewers++;
            UpdateBackgroundAnimation();
        }

        public void ReleaseBackgroundAnimation()
        {
            animationViewers = Math.Max(0, animationViewers - 1);
            UpdateBackgroundAnimation();
        }

        private void ShowAnimatedBackground(AnimatedBackground animation, int frame = 0)
        {
            animationTimer?.Stop();
            bool wasAnimated = animatedBackground != null;
            animatedBackground = animation;
            animationFrame = Math.Clamp(frame, 0, animation.Frames.Count - 1);
            if (!wasAnimated)
            {
                OnPropertyChanged(nameof(IsFlyoutBackgroundAnimated));
            }

            FlyoutBackgroundImage = animation.Frames[animationFrame];
            UpdateBackgroundAnimation();
        }

        private void ClearAnimatedBackground(bool deleteFile)
        {
            animationTimer?.Stop();
            if (animatedBackground == null && !deleteFile)
                return;

            animatedBackground = null;
            OnPropertyChanged(nameof(IsFlyoutBackgroundAnimated));
            if (deleteFile)
            {
                AppDataHelper.FlyoutBackgroundAnimationTurns = 0;
                try { File.Delete(FlyoutBackgroundGifPath); } catch { }
            }
        }

        /// <summary>
        /// Reads the saved animated photo, if there is one, without holding up startup.
        /// </summary>
        private async void LoadAnimatedBackground()
        {
            string path = FlyoutBackgroundGifPath;
            if (!useFlyoutBackgroundImage || isLoadingAnimatedBackground || !File.Exists(path))
                return;

            isLoadingAnimatedBackground = true;

            int turns = AppDataHelper.FlyoutBackgroundAnimationTurns;
            var loading = Task.Run(() => LoadAndCleanUp(() => AnimatedBackgroundHelper.TryLoad(path, turns)));
            pendingAnimatedLoad = loading;
            var loaded = await loading;
            pendingAnimatedLoad = null;
            isLoadingAnimatedBackground = false;

            // A new photo or GIF may have been picked meanwhile; it wins over this older one
            if (loaded != null && animatedBackground == null && File.Exists(path) && !File.Exists(FlyoutBackgroundImagePath))
            {
                ShowAnimatedBackground(loaded);
            }
        }

        /// <summary>
        /// Decoding leaves tens of MB of native picture buffers behind that .NET can't see, so they'd sit there
        /// until some later collection. One full collection right after this one-off job gives them back.
        /// </summary>
        private static AnimatedBackground LoadAndCleanUp(Func<AnimatedBackground> load)
        {
            var result = load();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return result;
        }

        private void UpdateBackgroundAnimation()
        {
            bool shouldRun = animatedBackground != null && useFlyoutBackgroundImage && animationViewers > 0;
            if (!shouldRun)
            {
                animationTimer?.Stop();
                return;
            }

            if (animationTimer == null)
            {
                // Normal, not Background: at Background it waited behind other UI work and the frames came late
                animationTimer = new DispatcherTimer(DispatcherPriority.Normal);
                animationTimer.Tick += (_, _) => AdvanceAnimation();
            }

            if (!animationTimer.IsEnabled)
            {
                animationTimer.Interval = animatedBackground.Delays[animationFrame];
                animationTimer.Start();
            }
        }

        private void AdvanceAnimation()
        {
            var animation = animatedBackground;
            if (animation == null)
            {
                animationTimer.Stop();
                return;
            }

            animationFrame = (animationFrame + 1) % animation.Frames.Count;
            flyoutBackgroundImage = animation.Frames[animationFrame];
            // Only the picture changes: not the theme, which a new photo would rebuild every frame
            OnPropertyChanged(nameof(FlyoutBackgroundImage));
            animationTimer.Interval = animation.Delays[animationFrame];
        }

        #endregion

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

        private CornerRadius mediaCardCornerRadius = new(MediaCardLayout.Classic.CornerRadius);

        /// <summary>
        /// The media card's corners. A binding, not an app resource: the flyout is created in a window
        /// band outside the Application's windows, so it never sees app resource changes.
        /// </summary>
        public CornerRadius MediaCardCornerRadius
        {
            get => mediaCardCornerRadius;
            private set => SetProperty(ref mediaCardCornerRadius, value);
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
            flyoutBackgroundBlur = Math.Clamp(AppDataHelper.FlyoutBackgroundBlur, 0, 100);
            flyoutBackgroundImageZoom = Math.Clamp(AppDataHelper.FlyoutBackgroundImageZoom, 100, 300);
            flyoutBackgroundImagePositionX = Math.Clamp(AppDataHelper.FlyoutBackgroundImagePositionX, 0, 100);
            flyoutBackgroundImagePositionY = Math.Clamp(AppDataHelper.FlyoutBackgroundImagePositionY, 0, 100);
            flyoutBackgroundImage = BackgroundImageHelper.TryLoad(FlyoutBackgroundImagePath);
            // After the photo switch is read: a saved GIF is only decoded when the photo is on
            LoadAnimatedBackground();
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
            MediaCardCornerRadius = new CornerRadius(mediaCardLayout.CornerRadius);
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
