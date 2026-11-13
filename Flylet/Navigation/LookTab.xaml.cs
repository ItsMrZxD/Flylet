using Flylet.Controls;
using Flylet.DesignTime;
using Flylet.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flylet.Core.Helpers;
using Flylet.UI;

namespace Flylet.Navigation
{
    /// <summary>
    /// Settings > Look: theme, colors, photo, media card and top bar, under a live preview.
    /// </summary>
    public partial class LookTab : UserControl
    {
        private MockMediaSession sampleMediaSession;

        public LookTab()
        {
            InitializeComponent();

            // The "already added" note is about the last Add; it's stale once the user leaves the page
            Unloaded += (_, _) => FlyoutBackgroundImageDuplicateText.Visibility = Visibility.Collapsed;

            // A still volume card for the preview; the real one is driven by the audio module
            PreviewVolumeControl.VolumeSlider.Value = 40;
            PreviewVolumeControl.textVal.Text = "40";

            // The preview's own top bar: the setting decides if it's always there, hidden until pointed at, or gone
            var previewTopBar = new FlyoutTopBar();
            PreviewTopBarHost.Content = previewTopBar;
            PreviewCards.MouseEnter += (_, _) => previewTopBar.SetPointerOverFlyout(true);
            PreviewCards.MouseLeave += (_, _) => previewTopBar.SetPointerOverFlyout(false);

            PreviewVolumeControl.SizeChanged += (_, _) => UpdateFlyoutShape();
            PreviewSessionControl.SizeChanged += (_, _) => UpdateFlyoutShape();
            PreviewTopBarHost.SizeChanged += (_, _) => UpdateFlyoutShape();

            BackgroundTilesControl.ItemsSource = backgroundTiles;

            Loaded += LookTab_Loaded;
            Unloaded += LookTab_Unloaded;
        }

        private void LookTab_Loaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated += AudioFlyoutHelper_MediaSessionsUpdated;
            FlyoutHandler.Instance.UIManager.PropertyChanged += UIManager_PropertyChanged;
            FlyoutHandler.Instance.UIManager.FlyoutBackgroundItems.CollectionChanged += FlyoutBackgroundItems_CollectionChanged;
            UpdateFlyoutShape();
            RebuildBackgroundTiles();
            UpdatePreviewSession();
        }

        private void LookTab_Unloaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated -= AudioFlyoutHelper_MediaSessionsUpdated;
            FlyoutHandler.Instance.UIManager.PropertyChanged -= UIManager_PropertyChanged;
            FlyoutHandler.Instance.UIManager.FlyoutBackgroundItems.CollectionChanged -= FlyoutBackgroundItems_CollectionChanged;
            // The thumbnails only take memory while this page is open
            backgroundTiles.Clear();
            backgroundThumbnails.Clear();
        }

        private void AudioFlyoutHelper_MediaSessionsUpdated(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(UpdatePreviewSession);
        }

        private void UIManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(UIManager.CurrentFlyoutBackgroundItem)
                or nameof(UIManager.IsFlyoutBackgroundShuffled))
            {
                UpdateBackgroundTileStates();
            }
        }

        #region Background thumbnails

        private readonly ObservableCollection<object> backgroundTiles = new();
        private readonly Dictionary<string, ImageSource> backgroundThumbnails = new();
        private BackgroundTile pressedTile;
        private Point pressedPosition;

        public const double BackgroundTileWidth = 112;

        // Until the preview has been measured: about what a volume card and a media card come to
        private const double DefaultFlyoutAspect = 2.4;

        private double flyoutAspect = DefaultFlyoutAspect;

        public static readonly DependencyProperty BackgroundTileHeightProperty =
            DependencyProperty.Register(
                nameof(BackgroundTileHeight),
                typeof(double),
                typeof(LookTab),
                new PropertyMetadata(BackgroundTileWidth / DefaultFlyoutAspect));

        public double BackgroundTileHeight
        {
            get => (double)GetValue(BackgroundTileHeightProperty);
            set => SetValue(BackgroundTileHeightProperty, value);
        }

        /// <summary>
        /// The thumbnails and the Adjust frame take the flyout's shape with both cards showing, read off the
        /// preview so it follows the media card's layout. The top bar only counts if it's always there.
        /// </summary>
        private void UpdateFlyoutShape()
        {
            if (PreviewVolumeControl.ActualHeight <= 0 || PreviewSessionControl.ActualHeight <= 0)
                return;

            double height = PreviewVolumeControl.ActualHeight + PreviewSessionControl.ActualHeight + UIManager.DefaultVerticalSpacing;
            if (FlyoutHandler.Instance.UIManager.TopBarVisibility == TopBarVisibility.Visible)
            {
                height += PreviewTopBarHost.ActualHeight;
            }

            double aspect = UIManager.FlyoutWidth / height;
            if (Math.Abs(aspect - flyoutAspect) < 0.001)
                return;

            flyoutAspect = aspect;
            BackgroundTileHeight = BackgroundTileWidth / aspect;
            foreach (var entry in backgroundTiles)
            {
                if (entry is BackgroundTile tile)
                {
                    tile.UpdateViewbox(aspect);
                }
            }
        }

        private void FlyoutBackgroundItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => RebuildBackgroundTiles();

        private void RebuildBackgroundTiles()
        {
            var uiManager = FlyoutHandler.Instance.UIManager;
            backgroundTiles.Clear();
            foreach (var item in uiManager.FlyoutBackgroundItems)
            {
                var tile = new BackgroundTile(item);
                backgroundTiles.Add(tile);
                LoadBackgroundThumbnail(tile);
            }

            backgroundTiles.Add(new BackgroundAddTile());
            UpdateBackgroundTileStates();
        }

        private void UpdateBackgroundTileStates()
        {
            var uiManager = FlyoutHandler.Instance.UIManager;
            int number = 1;
            foreach (var entry in backgroundTiles)
            {
                if (entry is BackgroundTile tile)
                {
                    tile.Number = number++;
                    tile.ShowNumber = !uiManager.IsFlyoutBackgroundShuffled;
                    tile.IsCurrent = tile.Item == uiManager.CurrentFlyoutBackgroundItem;
                }
            }
        }

        // Small and off the UI thread, so a long list doesn't slow this page or hold full-size pictures in memory
        private async void LoadBackgroundThumbnail(BackgroundTile tile)
        {
            string fileName = tile.Item.FileName;
            if (!backgroundThumbnails.TryGetValue(fileName, out var thumbnail))
            {
                string path = FlyoutHandler.Instance.UIManager.GetFlyoutBackgroundItemPath(tile.Item);
                int turns = tile.Item.Turns;
                thumbnail = await Task.Run(() => TryLoadThumbnail(path, turns));
                if (thumbnail == null)
                    return;

                backgroundThumbnails[fileName] = thumbnail;
            }

            tile.ShowThumbnail(thumbnail, flyoutAspect);
        }

        // Turns: a GIF's quarter turns are only saved in the list; a photo's are in the file itself
        private static ImageSource TryLoadThumbnail(string path, int turns)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.DecodePixelWidth = 240;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();

                if (turns == 0)
                    return image;

                var turned = new TransformedBitmap(image, new RotateTransform(turns * 90));
                turned.Freeze();
                return turned;
            }
            catch
            {
                return null;
            }
        }

        private void BackgroundTile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            pressedTile = (BackgroundTile)((FrameworkElement)sender).DataContext;
            pressedPosition = e.GetPosition(this);
        }

        private void BackgroundTile_MouseMove(object sender, MouseEventArgs e)
        {
            if (pressedTile == null || e.LeftButton != MouseButtonState.Pressed
                || !FlyoutHandler.Instance.UIManager.CanReorderFlyoutBackgrounds)
            {
                return;
            }

            var moved = e.GetPosition(this) - pressedPosition;
            if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var tile = pressedTile;
            pressedTile = null;
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(BackgroundTile), tile), DragDropEffects.Move);
        }

        // A press that never became a drag is a click
        private void BackgroundTile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (pressedTile != null && pressedTile == ((FrameworkElement)sender).DataContext)
            {
                FlyoutHandler.Instance.UIManager.SelectFlyoutBackgroundItem(pressedTile.Item);
            }

            pressedTile = null;
        }

        private void BackgroundTile_DragOver(object sender, DragEventArgs e)
        {
            bool isTile = e.Data.GetDataPresent(typeof(BackgroundTile));
            e.Effects = isTile ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            if (isTile)
            {
                ((BackgroundTile)((FrameworkElement)sender).DataContext).IsDropTarget = true;
            }
        }

        private void BackgroundTile_DragLeave(object sender, DragEventArgs e)
        {
            ((BackgroundTile)((FrameworkElement)sender).DataContext).IsDropTarget = false;
        }

        private void BackgroundTile_Drop(object sender, DragEventArgs e)
        {
            var target = (BackgroundTile)((FrameworkElement)sender).DataContext;
            target.IsDropTarget = false;
            if (e.Data.GetData(typeof(BackgroundTile)) is BackgroundTile dragged)
            {
                var uiManager = FlyoutHandler.Instance.UIManager;
                uiManager.MoveFlyoutBackgroundItem(dragged.Item, uiManager.FlyoutBackgroundItems.IndexOf(target.Item));
            }
        }

        private const int BackgroundRemoveGuardMilliseconds = 500;

        private long lastBackgroundRemoveTicks = -BackgroundRemoveGuardMilliseconds;
        private bool isImportingBackgrounds;

        private void RemoveBackgroundTile_Click(object sender, RoutedEventArgs e)
        {
            // The tiles shift left right away, so a double-click's second click would land on the next picture's x
            long now = Environment.TickCount64;
            if (now - lastBackgroundRemoveTicks < BackgroundRemoveGuardMilliseconds)
                return;
            lastBackgroundRemoveTicks = now;

            var tile = (BackgroundTile)((FrameworkElement)sender).DataContext;
            FlyoutBackgroundImageDuplicateText.Visibility = Visibility.Collapsed;
            backgroundThumbnails.Remove(tile.Item.FileName);
            FlyoutHandler.Instance.UIManager.RemoveFlyoutBackgroundItem(tile.Item);
        }

        private async void AddFlyoutBackgroundImages_Click(object sender, RoutedEventArgs e)
        {
            if (isImportingBackgrounds)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Settings_FlyoutBackgroundImageFilter}|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.jxr",
                Title = Strings.Settings_ChooseFlyoutBackgroundImage,
                Multiselect = true
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            // A big GIF takes a few seconds; one import at a time. The Add tile is rebuilt as pictures arrive,
            // so this can't be done by disabling the button
            isImportingBackgrounds = true;
            try
            {
                var (imported, skippedDuplicates) = await FlyoutHandler.Instance.UIManager.AddFlyoutBackgroundImagesAsync(dialog.FileNames);
                FlyoutBackgroundImageErrorText.Visibility = imported ? Visibility.Collapsed : Visibility.Visible;
                FlyoutBackgroundImageDuplicateText.Visibility = skippedDuplicates ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                isImportingBackgrounds = false;
            }
        }

        private void AdjustFlyoutBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            var uiManager = FlyoutHandler.Instance.UIManager;
            uiManager.IsBackgroundRotationPaused = true;
            try
            {
                new Views.AdjustPhotoWindow(flyoutAspect) { Owner = Window.GetWindow(this) }.ShowDialog();
            }
            finally
            {
                uiManager.IsBackgroundRotationPaused = false;
            }

            // It may have been turned or framed differently
            if (uiManager.CurrentFlyoutBackgroundItem is { } current)
            {
                backgroundThumbnails.Remove(current.FileName);
                RebuildBackgroundTiles();
            }
        }

        #endregion

        /// <summary>
        /// The preview follows the song the media keys control, so the options are judged on real art and
        /// text; it falls back to a sample song when nothing is playing.
        /// </summary>
        private void UpdatePreviewSession()
        {
            PreviewSessionControl.DataContext = (object)FlyoutHandler.Instance.AudioFlyoutHelper.PreviewMediaSession
                ?? (sampleMediaSession ??= new MockMediaSession(Strings.MediaCard_SampleTitle, Strings.MediaCard_SampleArtist, Program.AppName));
        }
    }
}
