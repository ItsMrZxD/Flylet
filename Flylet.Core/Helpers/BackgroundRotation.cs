using System;
using System.Collections.Generic;
using System.Linq;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// Whether the background photos or GIFs rotate in their listed order or at random.
    /// </summary>
    public enum BackgroundRotationOrder { InOrder, Shuffle }

    /// <summary>
    /// Decides which background photo or GIF is current when several are set to rotate.
    /// It only picks the current item; it never touches the files or the UI.
    /// </summary>
    public sealed class BackgroundRotation
    {
        private readonly TimeProvider timeProvider;
        private readonly Random random;

        private string[] items = Array.Empty<string>();
        private int currentIndex = -1;
        private DateTimeOffset lastChangeTime;

        public BackgroundRotation(TimeProvider timeProvider = null, Random random = null)
        {
            // null means "use the default", matching the TimelineController constructor.
            this.timeProvider = timeProvider ?? TimeProvider.System;
            this.random = random ?? new Random();
        }

        /// <summary>
        /// The photos or GIFs that can rotate, in order. Starts empty.
        /// </summary>
        public IReadOnlyList<string> Items => items;

        /// <summary>
        /// The item currently shown, or null when there are no items.
        /// </summary>
        public string Current => items.Length > 0 ? items[currentIndex] : null;

        /// <summary>
        /// Whether the rotation follows the list order or picks at random. Defaults to <see cref="BackgroundRotationOrder.InOrder"/>.
        /// </summary>
        public BackgroundRotationOrder Order { get; set; }

        /// <summary>
        /// Shortest gap between automatic changes. When zero, the item changes on every <see cref="OnFlyoutOpened"/>.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Replaces the list of items, keeping the current one if it is still there.
        /// </summary>
        /// <param name="items">The new file names. Blank entries and duplicates are ignored.</param>
        public void SetItems(IEnumerable<string> items)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(item) || !seen.Add(item))
                    continue;
                list.Add(item);
            }

            string oldCurrent = Current;
            this.items = list.ToArray();

            // Keep the current item if it survived; otherwise start at the first one (or none).
            int existingIndex = Array.IndexOf(this.items, oldCurrent);
            if (existingIndex >= 0)
            {
                currentIndex = existingIndex;
            }
            else
            {
                currentIndex = this.items.Length == 0 ? -1 : 0;
            }

            lastChangeTime = timeProvider.GetUtcNow();
        }

        /// <summary>
        /// Moves to the next item if enough time has passed since the last change, e.g. when the flyout opens.
        /// </summary>
        /// <returns>True only if the current item changed.</returns>
        public bool OnFlyoutOpened()
        {
            var now = timeProvider.GetUtcNow();
            bool enoughTime = Interval == TimeSpan.Zero || now - lastChangeTime >= Interval;

            if (!enoughTime || items.Length <= 1)
                return false;

            SelectNext();
            return true;
        }

        /// <summary>
        /// Moves to the next item right now, ignoring <see cref="Interval"/>.
        /// </summary>
        public void Next()
        {
            if (items.Length <= 1)
                return;

            SelectNext();
        }

        // Picks the next index and stamps the change time in one place, so Next and OnFlyoutOpened agree.
        private void SelectNext()
        {
            currentIndex = Order switch
            {
                BackgroundRotationOrder.Shuffle => ShuffleIndex(),
                _ => (currentIndex + 1) % items.Length,
            };

            lastChangeTime = timeProvider.GetUtcNow();
        }

        // A random index that isn't the current one, so the background never looks unchanged.
        private int ShuffleIndex()
        {
            if (items.Length == 1)
                return 0;

            int next = random.Next(items.Length - 1);
            return next >= currentIndex ? next + 1 : next;
        }
    }
}
