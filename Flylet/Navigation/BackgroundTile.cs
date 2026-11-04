using CommunityToolkit.Mvvm.ComponentModel;
using Flylet.Core.Helpers;
using System.Windows;
using System.Windows.Media;

namespace Flylet.Navigation
{
    /// <summary>
    /// One picture in the Settings thumbnail row.
    /// </summary>
    public sealed class BackgroundTile : ObservableObject
    {
        public BackgroundTile(BackgroundItem item) => Item = item;

        public BackgroundItem Item { get; }

        public bool IsAnimated => Item.IsAnimated;

        private ImageSource thumbnail;

        public ImageSource Thumbnail => thumbnail;

        private Rect viewbox = new(0, 0, 1, 1);

        /// <summary>
        /// The part of the picture the tile shows: the same part the flyout does.
        /// </summary>
        public Rect Viewbox
        {
            get => viewbox;
            private set => SetProperty(ref viewbox, value);
        }

        public void ShowThumbnail(ImageSource image, double tileAspect)
        {
            thumbnail = image;
            // The crop first, so the picture never shows squeezed for a moment
            UpdateViewbox(tileAspect);
            OnPropertyChanged(nameof(Thumbnail));
        }

        public void UpdateViewbox(double tileAspect)
        {
            if (thumbnail == null || thumbnail.Height <= 0)
                return;

            Viewbox = BackgroundImageHelper.GetViewbox(
                thumbnail.Width / thumbnail.Height, tileAspect, Item.Zoom / 100, Item.PositionX / 100, Item.PositionY / 100);
        }

        private int number;

        public int Number
        {
            get => number;
            set => SetProperty(ref number, value);
        }

        private bool showNumber;

        /// <summary>
        /// Shuffle has no order to show.
        /// </summary>
        public bool ShowNumber
        {
            get => showNumber;
            set => SetProperty(ref showNumber, value);
        }

        private bool isCurrent;

        public bool IsCurrent
        {
            get => isCurrent;
            set => SetProperty(ref isCurrent, value);
        }

        private bool isDropTarget;

        public bool IsDropTarget
        {
            get => isDropTarget;
            set => SetProperty(ref isDropTarget, value);
        }
    }

    /// <summary>
    /// The "Add" button at the end of the thumbnail row.
    /// </summary>
    public sealed class BackgroundAddTile
    {
    }
}
