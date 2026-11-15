using System;

namespace Flylet.Core.Media
{
    public enum VisualizerPreset
    {
        Subtle,
        Lively,
        Party,
        Custom
    }

    /// <summary>
    /// Optional touches on top of the music visualizer's waves.
    /// </summary>
    [Flags]
    public enum VisualizerExtras
    {
        None = 0,
        Fill = 1,
        Glow = 2,
        ThumbPulse = 4,
        Sparkles = 8,
        ColorShift = 16,
        MoreWaves = 32,
        PastThumb = 64,
    }

    /// <summary>
    /// How the music visualizer timeline looks: which extras are on and how much the waves move (percent).
    /// </summary>
    public readonly record struct VisualizerStyle(VisualizerExtras Extras, int Amount)
    {
        public const int MinAmount = 40;
        public const int MaxAmount = 160;

        public static VisualizerStyle Subtle { get; } = new(VisualizerExtras.None, 70);

        public static VisualizerStyle Lively { get; } = new(VisualizerExtras.Fill | VisualizerExtras.ThumbPulse, 100);

        public static VisualizerStyle Party { get; } = new(
            VisualizerExtras.Fill | VisualizerExtras.Glow | VisualizerExtras.ThumbPulse | VisualizerExtras.Sparkles
            | VisualizerExtras.ColorShift | VisualizerExtras.MoreWaves, 130);

        /// <summary>
        /// The style a preset stands for; Custom is the user's own mix.
        /// </summary>
        public static VisualizerStyle FromPreset(VisualizerPreset preset, VisualizerStyle custom) => preset switch
        {
            VisualizerPreset.Subtle => Subtle,
            VisualizerPreset.Lively => Lively,
            VisualizerPreset.Party => Party,
            _ => custom.Normalized(),
        };

        public bool Has(VisualizerExtras extra) => (Extras & extra) == extra && extra != VisualizerExtras.None;

        public VisualizerStyle With(VisualizerExtras extra, bool on)
            => this with { Extras = on ? Extras | extra : Extras & ~extra };

        public static int ClampAmount(int amount) => Math.Clamp(amount, MinAmount, MaxAmount);

        /// <summary>
        /// Drops unknown extras and keeps the amount in range (a stored value may come from anywhere).
        /// </summary>
        public VisualizerStyle Normalized()
        {
            const VisualizerExtras all = VisualizerExtras.Fill | VisualizerExtras.Glow | VisualizerExtras.ThumbPulse
                | VisualizerExtras.Sparkles | VisualizerExtras.ColorShift | VisualizerExtras.MoreWaves
                | VisualizerExtras.PastThumb;
            return new VisualizerStyle(Extras & all, ClampAmount(Amount));
        }
    }

    /// <summary>
    /// Made-up music for the Settings preview: a steady beat with some movement, so people see the visualizer
    /// without anything playing and without Flylet listening to the PC.
    /// </summary>
    public static class FakeMusic
    {
        /// <summary>
        /// Bass, mid and high levels (0-1) at a moment in seconds. Same time, same levels.
        /// </summary>
        public static (float Bass, float Mid, float High) Levels(double seconds)
        {
            const double bpm = 118;
            double beat = seconds * bpm / 60 % 1;
            double kick = Math.Exp(-beat * 6);
            double mid = 0.35 + 0.2 * Math.Sin(seconds * 1.7) + 0.1 * Math.Sin(seconds * 5.3);
            double high = 0.25 + 0.15 * Math.Sin(seconds * 9.1) * Math.Sin(seconds * 2.3) + 0.1 * Math.Exp(-((beat + 0.5) % 1) * 8);
            return ((float)Math.Clamp(0.15 + 0.85 * kick, 0, 1), (float)Math.Clamp(mid, 0, 1), (float)Math.Clamp(high, 0, 1));
        }
    }
}
