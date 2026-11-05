using CommunityToolkit.Mvvm.ComponentModel;
using Flylet.Controls;
using Flylet.Core.Helpers;
using Flylet.Core.Media;
using Flylet.Core.Threading;
using Flylet.Core.UI;
using Flylet.Helpers;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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

        private bool useLessMemory = DefaultValuesStore.UseLessMemory;

        /// <summary>
        /// Flyouts drawn with the CPU instead of the graphics card: about 40 MB less, a bit more CPU while shown.
        /// </summary>
        public bool UseLessMemory
        {
            get => useLessMemory;
            set
            {
                if (SetProperty(ref useLessMemory, value))
                {
                    AppDataHelper.UseLessMemory = value;
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

        /// <summary>
        /// The blur behind the cards needs DWMWA_USE_HOSTBACKDROPBRUSH, which only Windows 11 has;
        /// on Windows 10 the backdrop brush paints solid black.
        /// </summary>
        public static bool IsGlassSupported { get; } = Environment.OSVersion.Version.Build >= 22000;

        private bool useGlass = DefaultValuesStore.UseGlass;

        /// <summary>
        /// Blurs whatever is behind each card (see FlyoutGlass); the background opacity becomes the glass tint.
        /// </summary>
        public bool UseGlass
        {
            get => useGlass;
            set
            {
                if (SetProperty(ref useGlass, value))
                {
                    AppDataHelper.UseGlass = value;
                    OnPropertyChanged(nameof(IsGlassActive));
                    OnPropertyChanged(nameof(FlyoutBackgroundOpacityHeader));
                    // A full-strength tint covers the glass completely, so start somewhere it shows
                    if (value && flyoutBackgroundOpacity >= 100)
                    {
                        FlyoutBackgroundOpacity = 50;
                    }
                }
            }
        }

        public bool IsGlassActive => useGlass && IsGlassSupported;

        public string FlyoutBackgroundOpacityHeader =>
            IsGlassActive ? Properties.Strings.Settings_GlassTint : Properties.Strings.Settings_FlyoutBackgroundOpacity;

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
                    if (value && animatedBackground == null && currentBackgroundItem?.IsAnimated == true)
                    {
                        // A saved GIF isn't decoded while the photo is off
                        ShowBackgroundItem(currentBackgroundItem);
                    }

                    UpdateBackgroundAnimation();
                    AppDataHelper.UseFlyoutBackgroundImage = value;
                }
            }
        }

        private ImageSource flyoutBackgroundImage;

        /// <summary>
        /// The photo or GIF frame showing now, or null before one is picked.
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
                    if (currentBackgroundItem != null)
                    {
                        currentBackgroundItem.Zoom = value;
                        SaveBackgroundListSoon();
                    }
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
                    if (currentBackgroundItem != null)
                    {
                        currentBackgroundItem.PositionX = value;
                        SaveBackgroundListSoon();
                    }
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
                    if (currentBackgroundItem != null)
                    {
                        currentBackgroundItem.PositionY = value;
                        SaveBackgroundListSoon();
                    }
                }
            }
        }

        /// <summary>
        /// Turns the photo a quarter turn; the framing starts over because the picture's shape changed.
        /// </summary>
        public bool RotateFlyoutBackgroundImage(bool clockwise)
        {
            if (currentBackgroundItem == null)
                return false;

            if (animatedBackground != null)
            {
                currentBackgroundItem.Turns = (currentBackgroundItem.Turns + (clockwise ? 1 : 3)) % 4;
                ResetFlyoutBackgroundImageFraming();
                ShowAnimatedBackground(AnimatedBackgroundHelper.Rotate(animatedBackground, clockwise), animationFrame);
                SaveBackgroundListSoon();
                return true;
            }

            // A GIF with no animation loaded failed to decode; TryRotate would write JPEG data into it
            if (currentBackgroundItem.IsAnimated)
                return false;

            string path = GetFlyoutBackgroundItemPath(currentBackgroundItem);
            if (!BackgroundImageHelper.TryRotate(path, clockwise))
                return false;

            ResetFlyoutBackgroundImageFraming();
            FlyoutBackgroundImage = BackgroundImageHelper.TryLoad(path);
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
        /// The background opacity setting also fades the photo, which would otherwise cover the
        /// see-through color behind it. The darkening stays, so the light text stays readable.
        /// </summary>
        public double FlyoutBackgroundImageOpacity => flyoutBackgroundOpacity / 100;

        #region Background list

        private static string BackgroundsFolder =>
            Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "Backgrounds");

        private static string BackgroundListPath => Path.Combine(BackgroundsFolder, "backgrounds.json");

        // The minutes behind each entry of the "Change background" box; 0 is every time the flyout opens
        private static readonly int[] flyoutBackgroundIntervalMinutes = { 0, 5, 30, 60, 1440 };

        private readonly BackgroundRotation backgroundRotation = new();
        private BackgroundItem currentBackgroundItem;
        private DispatcherTimer backgroundListSaveTimer;
        private int backgroundLoadId;
        private int backgroundDecodes;

        /// <summary>
        /// The user's photos and GIFs, in the order they play. Only the current one is in memory.
        /// </summary>
        public ObservableCollection<BackgroundItem> FlyoutBackgroundItems { get; } = new();

        public BackgroundItem CurrentFlyoutBackgroundItem => currentBackgroundItem;

        public bool HasFlyoutBackgrounds => FlyoutBackgroundItems.Count > 0;

        public bool HasSeveralFlyoutBackgrounds => FlyoutBackgroundItems.Count > 1;

        public bool CanReorderFlyoutBackgrounds => HasSeveralFlyoutBackgrounds && !IsFlyoutBackgroundShuffled;

        /// <summary>
        /// Set while the user is framing a photo, so it doesn't change under them.
        /// </summary>
        public bool IsBackgroundRotationPaused { get; set; }

        private int flyoutBackgroundIntervalIndex;

        public int FlyoutBackgroundIntervalIndex
        {
            get => flyoutBackgroundIntervalIndex;
            set
            {
                value = Math.Clamp(value, 0, flyoutBackgroundIntervalMinutes.Length - 1);
                if (SetProperty(ref flyoutBackgroundIntervalIndex, value))
                {
                    int minutes = flyoutBackgroundIntervalMinutes[value];
                    backgroundRotation.Interval = TimeSpan.FromMinutes(minutes);
                    AppDataHelper.FlyoutBackgroundIntervalMinutes = minutes;
                }
            }
        }

        public bool IsFlyoutBackgroundShuffled => backgroundRotation.Order == BackgroundRotationOrder.Shuffle;

        /// <summary>
        /// 0 plays in the user's order, 1 shuffles.
        /// </summary>
        public int FlyoutBackgroundOrderIndex
        {
            get => IsFlyoutBackgroundShuffled ? 1 : 0;
            set
            {
                var order = value == 1 ? BackgroundRotationOrder.Shuffle : BackgroundRotationOrder.InOrder;
                if (backgroundRotation.Order != order)
                {
                    backgroundRotation.Order = order;
                    AppDataHelper.FlyoutBackgroundShuffle = value == 1;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFlyoutBackgroundShuffled));
                    OnPropertyChanged(nameof(CanReorderFlyoutBackgrounds));
                }
            }
        }

        /// <summary>
        /// How much disk space the imported copies take, as text for Settings.
        /// </summary>
        public string FlyoutBackgroundDiskUsage
        {
            get
            {
                long bytes = 0;
                foreach (var item in FlyoutBackgroundItems)
                {
                    try { bytes += new FileInfo(GetFlyoutBackgroundItemPath(item)).Length; } catch { }
                }

                string size = bytes >= 1024 * 1024
                    ? $"{bytes / (1024.0 * 1024.0):0.#} MB"
                    : $"{Math.Max(1, bytes / 1024)} KB";
                return string.Format(Properties.Strings.Settings_FlyoutBackgroundImages_DiskUse, size);
            }
        }

        public string GetFlyoutBackgroundItemPath(BackgroundItem item) => Path.Combine(BackgroundsFolder, item.FileName);

        private BackgroundItem FindBackgroundItem(string fileName) =>
            FlyoutBackgroundItems.FirstOrDefault(item => item.FileName == fileName);

        /// <summary>
        /// Imports pictures and adds them to the list, one at a time and off the UI thread: a big GIF takes a
        /// few seconds. The first new one shows right away, so the user sees the result.
        /// </summary>
        /// <returns>False if any file couldn't be read as a picture; the others are still added.</returns>
        public async Task<bool> AddFlyoutBackgroundImagesAsync(IEnumerable<string> paths)
        {
            bool allImported = true;
            BackgroundItem firstNew = null;
            foreach (string path in paths)
            {
                var item = await Task.Run(() => ImportBackgroundFile(path));
                if (item == null)
                {
                    allImported = false;
                    continue;
                }

                FlyoutBackgroundItems.Add(item);
                firstNew ??= item;
            }

            if (firstNew != null)
            {
                OnBackgroundListChanged();
                backgroundRotation.Select(firstNew.FileName);
                UseFlyoutBackgroundImage = true;
                ShowBackgroundItem(firstNew);
            }

            return allImported;
        }

        private static BackgroundItem ImportBackgroundFile(string path)
        {
            string name = Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(BackgroundsFolder);

                if (string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase)
                    && AnimatedBackgroundHelper.IsAnimatedGif(path))
                {
                    if (new FileInfo(path).Length > AnimatedBackgroundHelper.MaxFileSize)
                        return null;

                    string destination = Path.Combine(BackgroundsFolder, name + ".gif");
                    File.Copy(path, destination, true);
                    if (LoadAndCleanUp(() => AnimatedBackgroundHelper.TryLoad(destination)) != null)
                        return new BackgroundItem { FileName = name + ".gif" };

                    File.Delete(destination);
                    return null;
                }

                return BackgroundImageHelper.TryImport(path, Path.Combine(BackgroundsFolder, name + ".jpg"))
                    ? new BackgroundItem { FileName = name + ".jpg" }
                    : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Takes a picture off the list and deletes Flylet's copy, which gives the disk space back.
        /// </summary>
        public void RemoveFlyoutBackgroundItem(BackgroundItem item)
        {
            if (!FlyoutBackgroundItems.Remove(item))
                return;

            try { File.Delete(GetFlyoutBackgroundItemPath(item)); } catch { }

            OnBackgroundListChanged();
            if (item == currentBackgroundItem)
            {
                ShowBackgroundItem(FindBackgroundItem(backgroundRotation.Current));
            }
        }

        public void MoveFlyoutBackgroundItem(BackgroundItem item, int newIndex)
        {
            int oldIndex = FlyoutBackgroundItems.IndexOf(item);
            if (oldIndex < 0 || oldIndex == newIndex || newIndex < 0 || newIndex >= FlyoutBackgroundItems.Count)
                return;

            FlyoutBackgroundItems.Move(oldIndex, newIndex);
            OnBackgroundListChanged();
        }

        public void SelectFlyoutBackgroundItem(BackgroundItem item)
        {
            if (item == currentBackgroundItem)
                return;

            backgroundRotation.Select(item.FileName);
            SaveBackgroundChangeTime();
            ShowBackgroundItem(item);
        }

        /// <summary>
        /// The next picture is picked and prepared as the flyout goes away, not when it comes back: it never
        /// changes while being looked at, a big GIF has time to decode, and only one picture is in memory.
        /// </summary>
        public void OnFlyoutHidden()
        {
            // A GIF still decoding gets to finish first, so quick flyouts don't pile up decodes
            if (useFlyoutBackgroundImage && !IsBackgroundRotationPaused && backgroundDecodes == 0 && backgroundRotation.AdvanceIfDue())
            {
                SaveBackgroundChangeTime();
                ShowBackgroundItem(FindBackgroundItem(backgroundRotation.Current));
            }

            if (ShowWhatsNewCard)
            {
                // It's been on screen now, so restarting the app doesn't bring it back
                AppDataHelper.LastSeenVersion = Program.AppVersion;
                if (--whatsNewShowsLeft <= 0)
                {
                    ShowWhatsNewCard = false;
                }
            }
        }

        #region What's new

        // Goes away by itself after this many flyouts: once is easy to miss, and having to close it would be a chore
        private const int WhatsNewMaxShows = 5;

        private int whatsNewShowsLeft;

        private bool showWhatsNewCard;

        public bool ShowWhatsNewCard
        {
            get => showWhatsNewCard;
            private set => SetProperty(ref showWhatsNewCard, value);
        }

        public bool WhatsNewShownThisRun { get; private set; }

        public string WhatsNewTitle => string.Format(Properties.Strings.WhatsNew_Title,
            Version.TryParse(Program.AppVersion, out var version) ? version.ToString(3) : Program.AppVersion);

        public void DismissWhatsNew()
        {
            AppDataHelper.LastSeenVersion = Program.AppVersion;
            whatsNewShowsLeft = 0;
            ShowWhatsNewCard = false;
        }

        // Must run before the first run gets stamped (RatePromptHelper.Initialize): that's how a fresh install is told from an update
        private void InitializeWhatsNew()
        {
            string current = Program.AppVersion;
            string lastSeen = AppDataHelper.LastSeenVersion;

            if (WhatsNewPolicy.ShouldShow(lastSeen, current, !string.IsNullOrEmpty(AppDataHelper.FirstRunUtc)))
            {
                whatsNewShowsLeft = WhatsNewMaxShows;
                ShowWhatsNewCard = true;
                WhatsNewShownThisRun = true;
            }
            else if (lastSeen != current)
            {
                AppDataHelper.LastSeenVersion = current;
            }
        }

        #endregion

        private void OnBackgroundListChanged()
        {
            backgroundRotation.SetItems(FlyoutBackgroundItems.Select(item => item.FileName));
            SaveBackgroundChangeTime();
            SaveBackgroundList();
            OnPropertyChanged(nameof(HasFlyoutBackgrounds));
            OnPropertyChanged(nameof(HasSeveralFlyoutBackgrounds));
            OnPropertyChanged(nameof(CanReorderFlyoutBackgrounds));
            OnPropertyChanged(nameof(FlyoutBackgroundDiskUsage));
        }

        /// <summary>
        /// Puts a picture on screen once it's ready; the old one stays up until then. When several are asked
        /// for in a row, only the last one counts.
        /// </summary>
        private async void ShowBackgroundItem(BackgroundItem item)
        {
            int loadId = ++backgroundLoadId;

            if (item == null)
            {
                ApplyBackground(null, null, null);
                return;
            }

            string path = GetFlyoutBackgroundItemPath(item);
            if (!item.IsAnimated)
            {
                ApplyBackground(item, null, BackgroundImageHelper.TryLoad(path));
                return;
            }

            AnimatedBackground animation = null;
            // A GIF isn't decoded while the photo is off
            if (useFlyoutBackgroundImage)
            {
                int turns = item.Turns;
                backgroundDecodes++;
                try
                {
                    animation = await Task.Run(() => LoadAndCleanUp(() => AnimatedBackgroundHelper.TryLoad(path, turns)));
                }
                finally
                {
                    backgroundDecodes--;
                }

                if (loadId != backgroundLoadId)
                    return;

                // Removed from the list while it was decoding: its file is gone
                if (!FlyoutBackgroundItems.Contains(item))
                {
                    ShowBackgroundItem(FindBackgroundItem(backgroundRotation.Current));
                    return;
                }
            }

            ApplyBackground(item, animation, null);
        }

        private void ApplyBackground(BackgroundItem item, AnimatedBackground animation, ImageSource still)
        {
            currentBackgroundItem = item;
            AppDataHelper.FlyoutBackgroundCurrent = item?.FileName ?? string.Empty;
            OnPropertyChanged(nameof(CurrentFlyoutBackgroundItem));

            // Straight into the fields: this is the picture's saved framing, not the user changing it
            flyoutBackgroundImageZoom = item?.Zoom ?? DefaultValuesStore.FlyoutBackgroundImageZoom;
            flyoutBackgroundImagePositionX = item?.PositionX ?? DefaultValuesStore.FlyoutBackgroundImagePositionX;
            flyoutBackgroundImagePositionY = item?.PositionY ?? DefaultValuesStore.FlyoutBackgroundImagePositionY;
            OnPropertyChanged(nameof(FlyoutBackgroundImageZoom));
            OnPropertyChanged(nameof(FlyoutBackgroundImagePositionX));
            OnPropertyChanged(nameof(FlyoutBackgroundImagePositionY));

            if (animation != null)
            {
                ShowAnimatedBackground(animation);
            }
            else
            {
                ClearAnimatedBackground();
                FlyoutBackgroundImage = still;
            }
        }

        private void SaveBackgroundList()
        {
            backgroundListSaveTimer?.Stop();
            try
            {
                Directory.CreateDirectory(BackgroundsFolder);
                string temp = BackgroundListPath + ".tmp";
                File.WriteAllText(temp, BackgroundList.Serialize(FlyoutBackgroundItems));
                File.Move(temp, BackgroundListPath, true);
            }
            catch
            {
                // Tried again on the next change; the pictures themselves are safe
            }
        }

        // Dragging a framing slider changes the values many times a second
        private void SaveBackgroundListSoon()
        {
            if (backgroundListSaveTimer == null)
            {
                backgroundListSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                backgroundListSaveTimer.Tick += (_, _) => SaveBackgroundList();
            }

            backgroundListSaveTimer.Stop();
            backgroundListSaveTimer.Start();
        }

        /// <summary>
        /// Reads the list at startup without holding it up: only the current picture is loaded, and a saved
        /// GIF only when the photo is on (so call this after that switch is read).
        /// </summary>
        private void LoadFlyoutBackgrounds()
        {
            List<BackgroundItem> items;
            try
            {
                Directory.CreateDirectory(BackgroundsFolder);
                items = File.Exists(BackgroundListPath)
                    ? BackgroundList.Parse(File.ReadAllText(BackgroundListPath))
                    : MoveOldPhotoIntoList();

                // A damaged list mustn't cost the user their pictures: start over from what's in the folder
                if (items.Count == 0)
                {
                    items = Directory.EnumerateFiles(BackgroundsFolder)
                        .Where(file => Path.GetExtension(file) is ".jpg" or ".gif")
                        .Select(file => new BackgroundItem { FileName = Path.GetFileName(file) })
                        .ToList();
                }

                items.RemoveAll(item => !File.Exists(GetFlyoutBackgroundItemPath(item)));

                // Left behind when a file was in use as it was removed, or an import was cut short
                foreach (string file in Directory.EnumerateFiles(BackgroundsFolder))
                {
                    string name = Path.GetFileName(file);
                    if (file != BackgroundListPath && items.All(item => item.FileName != name))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch
            {
                items = new List<BackgroundItem>();
            }

            foreach (var item in items)
            {
                FlyoutBackgroundItems.Add(item);
            }

            flyoutBackgroundIntervalIndex = Math.Max(0, Array.IndexOf(flyoutBackgroundIntervalMinutes, AppDataHelper.FlyoutBackgroundIntervalMinutes));
            backgroundRotation.Interval = TimeSpan.FromMinutes(flyoutBackgroundIntervalMinutes[flyoutBackgroundIntervalIndex]);
            backgroundRotation.Order = AppDataHelper.FlyoutBackgroundShuffle ? BackgroundRotationOrder.Shuffle : BackgroundRotationOrder.InOrder;
            backgroundRotation.SetItems(items.Select(item => item.FileName));
            backgroundRotation.Select(AppDataHelper.FlyoutBackgroundCurrent);
            RestoreBackgroundChangeTime();

            var current = FindBackgroundItem(backgroundRotation.Current);
            if (current == null)
                return;

            currentBackgroundItem = current;
            flyoutBackgroundImageZoom = current.Zoom;
            flyoutBackgroundImagePositionX = current.PositionX;
            flyoutBackgroundImagePositionY = current.PositionY;
            if (!current.IsAnimated)
            {
                flyoutBackgroundImage = BackgroundImageHelper.TryLoad(GetFlyoutBackgroundItemPath(current));
            }
            else if (useFlyoutBackgroundImage)
            {
                ShowBackgroundItem(current);
            }
        }

        private void RestoreBackgroundChangeTime()
        {
            if (DateTimeOffset.TryParse(AppDataHelper.FlyoutBackgroundLastChangeUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var saved) && saved <= DateTimeOffset.UtcNow)
            {
                backgroundRotation.LastChangeTime = saved;
            }
            else
            {
                SaveBackgroundChangeTime();
            }
        }

        private void SaveBackgroundChangeTime()
        {
            AppDataHelper.FlyoutBackgroundLastChangeUtc = backgroundRotation.LastChangeTime.ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Up to 0.11 there was one picture, directly in the app's folder, with its framing in the settings.
        /// </summary>
        private List<BackgroundItem> MoveOldPhotoIntoList()
        {
            var items = new List<BackgroundItem>();
            string localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            string oldGif = Path.Combine(localFolder, "FlyoutBackground.gif");
            string oldStill = Path.Combine(localFolder, "FlyoutBackground.jpg");
            // When both existed, 0.11 showed the photo after a restart
            string old = File.Exists(oldStill) ? oldStill : File.Exists(oldGif) ? oldGif : null;

            if (old != null)
            {
                // Opacity had no effect on a photo up to 0.11, so don't let an old value fade it now
                if (useFlyoutBackgroundImage && flyoutBackgroundOpacity < 100)
                {
                    FlyoutBackgroundOpacity = 100;
                    AppDataHelper.FlyoutBackgroundOpacity = 100;
                }

                string name = Guid.NewGuid().ToString("N") + Path.GetExtension(old);
                File.Move(old, Path.Combine(BackgroundsFolder, name));
                items.Add(new BackgroundItem
                {
                    FileName = name,
                    Zoom = Math.Clamp(AppDataHelper.FlyoutBackgroundImageZoom, 100, 300),
                    PositionX = Math.Clamp(AppDataHelper.FlyoutBackgroundImagePositionX, 0, 100),
                    PositionY = Math.Clamp(AppDataHelper.FlyoutBackgroundImagePositionY, 0, 100),
                    Turns = old == oldGif ? AppDataHelper.FlyoutBackgroundAnimationTurns : 0
                });
            }

            File.WriteAllText(BackgroundListPath, BackgroundList.Serialize(items));

            if (old != null)
            {
                try { File.Delete(oldStill); } catch { }
                try { File.Delete(oldGif); } catch { }
            }

            return items;
        }

        #endregion

        #region Animated background

        private AnimatedBackground animatedBackground;
        private DispatcherTimer animationTimer;
        private int animationFrame;
        private int animationViewers;

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

            FlyoutBackgroundImage = animation.CreateFrame(animationFrame);
            UpdateBackgroundAnimation();
        }

        private void ClearAnimatedBackground()
        {
            animationTimer?.Stop();
            if (animatedBackground == null)
                return;

            animatedBackground = null;
            OnPropertyChanged(nameof(IsFlyoutBackgroundAnimated));
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
            flyoutBackgroundImage = animation.CreateFrame(animationFrame);
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
            InitializeWhatsNew();

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
            UseLessMemory = AppDataHelper.UseLessMemory;

            // Straight into the fields: the setters would re-save what was just read and rebuild the accent
            // more than once, and the background is applied by the theme update below, once the system theme is known
            customAccentColor = ParseColorOrDefault(AppDataHelper.CustomAccentColor, DefaultValuesStore.CustomAccentColor);
            useCustomAccentColor = AppDataHelper.UseCustomAccentColor;
            customFlyoutBackgroundColor = ParseColorOrDefault(AppDataHelper.CustomFlyoutBackgroundColor, DefaultValuesStore.CustomFlyoutBackgroundColor);
            useCustomFlyoutBackgroundColor = AppDataHelper.UseCustomFlyoutBackgroundColor;
            useGlass = AppDataHelper.UseGlass;
            useFlyoutBackgroundImage = AppDataHelper.UseFlyoutBackgroundImage;
            flyoutBackgroundImageDim = Math.Clamp(AppDataHelper.FlyoutBackgroundImageDim, MinFlyoutBackgroundImageDim, 100);
            flyoutBackgroundBlur = Math.Clamp(AppDataHelper.FlyoutBackgroundBlur, 0, 100);
            LoadFlyoutBackgrounds();
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
            OnPropertyChanged(nameof(FlyoutBackgroundImageOpacity));
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
            // Called on a WMI worker thread when Windows switches light/dark; the theme update touches WPF objects
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => OnSystemThemeChanged(sender, args));
                return;
            }

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
