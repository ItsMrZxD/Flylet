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

            // A still volume card for the preview; the real one is driven by the audio module
            PreviewVolumeControl.VolumeSlider.Value = 40;
            PreviewVolumeControl.textVal.Text = "40";

            // The preview's own top bar: the setting decides if it's always there, hidden until pointed at, or gone
            var previewTopBar = new FlyoutTopBar();
            PreviewTopBarHost.Content = previewTopBar;
            PreviewCards.MouseEnter += (_, _) => previewTopBar.SetPointerOverFlyout(true);
            PreviewCards.MouseLeave += (_, _) => previewTopBar.SetPointerOverFlyout(false);

            BackgroundTilesControl.ItemsSource = backgroundTiles;

            Loaded += LookTab_Loaded;
            Unloaded += LookTab_Unloaded;
        }

        private void LookTab_Loaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated += AudioFlyoutHelper_MediaSessionsUpdated;
            FlyoutHandler.Instance.UIManager.PropertyChanged += UIManager_PropertyChanged;
            FlyoutHandler.Instance.UIManager.FlyoutBackgroundItems.CollectionChanged += FlyoutBackgroundItems_CollectionChanged;
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
                thumbnail = await Task.Run(() => TryLoadThumbnail(path));
                if (thumbnail == null)
                    return;

                backgroundThumbnails[fileName] = thumbnail;
            }

            tile.Thumbnail = thumbnail;
        }

        private static ImageSource TryLoadThumbnail(string path)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.DecodePixelWidth = 168;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                return image;
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

        private void RemoveBackgroundTile_Click(object sender, RoutedEventArgs e)
        {
            var tile = (BackgroundTile)((FrameworkElement)sender).DataContext;
            backgroundThumbnails.Remove(tile.Item.FileName);
            FlyoutHandler.Instance.UIManager.RemoveFlyoutBackgroundItem(tile.Item);
        }

        private async void AddFlyoutBackgroundImages_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Settings_FlyoutBackgroundImageFilter}|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.jxr",
                Title = Strings.Settings_ChooseFlyoutBackgroundImage,
                Multiselect = true
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            // A big GIF takes a few seconds; one import at a time
            var button = (Button)sender;
            button.IsEnabled = false;
            try
            {
                bool imported = await FlyoutHandler.Instance.UIManager.AddFlyoutBackgroundImagesAsync(dialog.FileNames);
                FlyoutBackgroundImageErrorText.Visibility = imported ? Visibility.Collapsed : Visibility.Visible;
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void AdjustFlyoutBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            var uiManager = FlyoutHandler.Instance.UIManager;
            uiManager.IsBackgroundRotationPaused = true;
            try
            {
                new Views.AdjustPhotoWindow { Owner = Window.GetWindow(this) }.ShowDialog();
            }
            finally
            {
                uiManager.IsBackgroundRotationPaused = false;
            }

            // It may have been turned
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
