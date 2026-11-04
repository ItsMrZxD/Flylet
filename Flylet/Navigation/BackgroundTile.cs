using CommunityToolkit.Mvvm.ComponentModel;
using Flylet.Core.Helpers;
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

        public ImageSource Thumbnail
        {
            get => thumbnail;
            set => SetProperty(ref thumbnail, value);
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
