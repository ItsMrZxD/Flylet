using System;

namespace Flylet.Core.Media
{
    public enum MediaCardArt
    {
        Left,
        Right,
        Background,
        Hidden
    }

    public enum MediaCardTimeline
    {
        Full,
        Thin,
        Hidden
    }

    public enum MediaCardControls
    {
        Full,
        PlayOnly,
        Hidden
    }

    public enum MediaCardShape
    {
        Rounded,
        Square,
        Pill
    }

    public enum MediaCardPreset
    {
        Classic,
        Minimal,
        CompactPill,
        AlbumBackground,
        Custom
    }

    /// <summary>
    /// What the media card shows and where. The sizes live here so the card and the sessions
    /// panel (which pages between cards) always agree on how tall a card is.
    /// </summary>
    public readonly record struct MediaCardLayout(
        MediaCardArt Art,
        bool ShowSource,
        bool ShowArtist,
        MediaCardTimeline Timeline,
        MediaCardControls Controls,
        MediaCardShape Shape)
    {
        public const double Padding = 16;
        public const double ArtSize = 96;
        public const double FullTimelineHeight = 66;
        public const double SourceRowHeight = 20;
        public const double TitleRowHeight = 20;
        public const double ArtistRowHeight = 16;
        public const double ControlsRowHeight = 40;
        public const double ButtonSize = 36;
        public const double PillHeight = 64;
        public const double PillPadding = 8;
        public const double PillArtSize = 48;
        public const double RoundedCornerRadius = 8;

        public static MediaCardLayout Classic { get; } =
            new(MediaCardArt.Left, true, true, MediaCardTimeline.Full, MediaCardControls.Full, MediaCardShape.Rounded);

        public static MediaCardLayout Minimal { get; } =
            new(MediaCardArt.Hidden, false, true, MediaCardTimeline.Thin, MediaCardControls.Hidden, MediaCardShape.Rounded);

        public static MediaCardLayout CompactPill { get; } =
            new(MediaCardArt.Left, false, false, MediaCardTimeline.Hidden, MediaCardControls.PlayOnly, MediaCardShape.Pill);

        public static MediaCardLayout AlbumBackground { get; } =
            new(MediaCardArt.Background, true, true, MediaCardTimeline.Full, MediaCardControls.Full, MediaCardShape.Rounded);

        public static MediaCardLayout FromPreset(MediaCardPreset preset) => preset switch
        {
            MediaCardPreset.Minimal => Minimal,
            MediaCardPreset.CompactPill => CompactPill,
            MediaCardPreset.AlbumBackground => AlbumBackground,
            _ => Classic,
        };

        /// <summary>
        /// The preset this layout matches exactly, or <see cref="MediaCardPreset.Custom"/>.
        /// </summary>
        public MediaCardPreset Preset =>
            this == Classic ? MediaCardPreset.Classic
            : this == Minimal ? MediaCardPreset.Minimal
            : this == CompactPill ? MediaCardPreset.CompactPill
            : this == AlbumBackground ? MediaCardPreset.AlbumBackground
            : MediaCardPreset.Custom;

        public bool IsPill => Shape == MediaCardShape.Pill;

        public bool ShowsArtTile => Art is MediaCardArt.Left or MediaCardArt.Right;

        /// <summary>
        /// The pill has no room for the source app's name.
        /// </summary>
        public bool SourceVisible => ShowSource && !IsPill;

        /// <summary>
        /// Full controls sit under the title on a regular card; everything else sits to the right of the text.
        /// </summary>
        public bool ControlsBelowText => Controls == MediaCardControls.Full && !IsPill;

        /// <summary>
        /// The pill is too short for the slider, so a full timeline shrinks to the thin bar there.
        /// </summary>
        public MediaCardTimeline EffectiveTimeline =>
            IsPill && Timeline == MediaCardTimeline.Full ? MediaCardTimeline.Thin : Timeline;

        /// <summary>
        /// Height of the art and text block, without the padding around it.
        /// </summary>
        public double TopSectionHeight
        {
            get
            {
                if (IsPill)
                    return PillArtSize;

                double text = (SourceVisible ? SourceRowHeight : 0)
                    + TitleRowHeight
                    + (ShowArtist ? ArtistRowHeight : 0)
                    + (ControlsBelowText ? ControlsRowHeight : 0);

                if (Controls != MediaCardControls.Hidden && !ControlsBelowText)
                    text = Math.Max(text, ButtonSize);

                return ShowsArtTile ? Math.Max(ArtSize, text) : text;
            }
        }

        public double Height => IsPill
            ? PillHeight
            : Padding + TopSectionHeight + (EffectiveTimeline == MediaCardTimeline.Full ? FullTimelineHeight : Padding);

        public double CornerRadius => Shape switch
        {
            MediaCardShape.Square => 0,
            MediaCardShape.Pill => PillHeight / 2,
            _ => RoundedCornerRadius,
        };
    }
}
