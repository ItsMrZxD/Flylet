using Flylet.Core.Audio;
using Flylet.Core.Media;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Flylet.Controls
{
    /// <summary>
    /// The music visualizer timeline: the track, waves in shades of the accent that follow the music's bass, mid
    /// and high, the extras from <see cref="VisualizerStyle"/>, and the thumb. The real slider sits on top,
    /// see-through, so seeking works as usual. In the flyout it listens to the PC (loopback); in the Settings
    /// preview it plays <see cref="FakeMusic"/> and never listens. Runs only while on screen.
    /// </summary>
    public sealed class VisualizerWaves : FrameworkElement
    {
        #region Properties

        public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
            nameof(Accent), typeof(Brush), typeof(VisualizerWaves),
            new FrameworkPropertyMetadata(Brushes.DeepPink, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush Accent
        {
            get => (Brush)GetValue(AccentProperty);
            set => SetValue(AccentProperty, value);
        }

        public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
            nameof(TrackBrush), typeof(Brush), typeof(VisualizerWaves),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public static readonly DependencyProperty IsHoveredProperty = DependencyProperty.Register(
            nameof(IsHovered), typeof(bool), typeof(VisualizerWaves),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// The pointer is over the slider: the thumb's dot grows, like the normal slider's.
        /// </summary>
        public bool IsHovered
        {
            get => (bool)GetValue(IsHoveredProperty);
            set => SetValue(IsHoveredProperty, value);
        }

        /// <summary>
        /// How far the song has played, in pixels of this element's width: the waves end at the thumb.
        /// </summary>
        public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
            nameof(Fraction), typeof(double), typeof(VisualizerWaves),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Fraction
        {
            get => (double)GetValue(FractionProperty);
            set => SetValue(FractionProperty, value);
        }

        #endregion

        private const double ThumbRadius = 10;
        private const double TrackThickness = 4;

        private bool running;
        private bool usesCapture;
        private TimeSpan lastFrame;
        private double time;

        public VisualizerWaves()
        {
            IsHitTestVisible = false;
            IsVisibleChanged += (_, _) => UpdateRunning();
            Unloaded += (_, _) => SetRunning(false);
            Loaded += (_, _) => UpdateRunning();
        }

        private bool IsPreview => Window.GetWindow(this) is SettingsWindow;

        private void UpdateRunning() => SetRunning(IsLoaded && IsVisible);

        private void SetRunning(bool run)
        {
            if (run == running)
                return;
            running = run;
            if (run)
            {
                // The Settings preview stays visible while Settings is open (even minimized), so it never listens
                usesCapture = !IsPreview;
                if (usesCapture)
                    LoopbackAudioCapture.Shared.Acquire();
                lastFrame = TimeSpan.Zero;
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
                if (usesCapture)
                    LoopbackAudioCapture.Shared.Release();
                usesCapture = false;
                sparks.Clear();
            }
        }

        // Every frame WPF draws, in step with the screen (owner's call: 60 fps; ~14% of one core while pinned,
        // to be optimized later, see TODO.md)
        private void OnRendering(object sender, EventArgs e)
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (now == lastFrame)
                return;
            double dt = lastFrame == TimeSpan.Zero ? 1 / 60.0 : Math.Min(0.1, (now - lastFrame).TotalSeconds);
            lastFrame = now;
            time += dt;
            UpdateSparks(dt);
            InvalidateVisual();
        }

        #region Levels and sparkles

        private float bass, mid, high;
        private float lastBass;
        private double sparkCooldown;
        private readonly List<Spark> sparks = new();
        private readonly Random random = new();

        private struct Spark
        {
            public double X, Y, VelocityY, Life;
        }

        private static VisualizerStyle CurrentStyle => FlyoutHandler.Instance?.UIManager.VisualizerStyle ?? VisualizerStyle.Lively;

        private void ReadLevels()
        {
            if (usesCapture)
            {
                var capture = LoopbackAudioCapture.Shared;
                (bass, mid, high) = (capture.Bass, capture.Mid, capture.High);
            }
            else
            {
                (bass, mid, high) = FakeMusic.Levels(time);
            }
        }

        private void UpdateSparks(double dt)
        {
            ReadLevels();

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.Life -= dt * 1.6;
                s.Y += s.VelocityY * dt;
                if (s.Life <= 0)
                    sparks.RemoveAt(i);
                else
                    sparks[i] = s;
            }

            sparkCooldown -= dt;
            if (CurrentStyle.Has(VisualizerExtras.Sparkles) && bass > 0.6f && lastBass <= 0.6f && sparkCooldown <= 0)
            {
                sparkCooldown = 0.15;
                int count = 3 + (int)(bass * 4);
                for (int i = 0; i < count && sparks.Count < 40; i++)
                {
                    sparks.Add(new Spark
                    {
                        X = random.NextDouble(),
                        Y = random.NextDouble() - 0.5,
                        VelocityY = (random.NextDouble() - 0.5) * 0.8,
                        Life = 1,
                    });
                }
            }
            lastBass = bass;
        }

        #endregion

        #region Drawing

        private readonly record struct Layer(double Shade, double Alpha, double Speed, double Wave, int Band, double Base, double Gain);

        private static readonly Layer[] BaseLayers =
        {
            new(0.55, 0.40, 1.4, 0.05, 0, 0.15, 0.85),
            new(-0.25, 0.60, -1.0, 0.08, 1, 0.12, 0.70),
            new(0.25, 0.80, 2.4, 0.13, 2, 0.08, 0.50),
        };

        private static readonly Layer[] MoreLayers =
        {
            new(0.75, 0.28, -1.8, 0.035, 0, 0.10, 0.60),
            new(-0.45, 0.65, 3.1, 0.17, 2, 0.06, 0.40),
        };

        private static readonly Layer[] AllLayers = Concat(BaseLayers, MoreLayers);

        // Brushes and pens only change with the accent or the style, not every frame
        private Color cachedAccent;
        private VisualizerExtras cachedExtras = (VisualizerExtras)(-1);
        private Brush cachedTrack;
        private Pen[] wavePens, glowPens;
        private Pen pastPen, playedPen, trackPen;
        private Brush fillBrush, sparkBrush, haloBrush, accentBrush, hoverBrush;
        private readonly List<Point> points = new();
        private readonly List<double> top = new(), bottom = new();

        private static Color Shade(Color c, double f)
        {
            byte S(byte v) => (byte)Math.Round(f > 0 ? v + (255 - v) * f : v * (1 + f));
            return Color.FromRgb(S(c.R), S(c.G), S(c.B));
        }

        private static Color WithAlpha(Color c, double alpha) => Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B);

        private static T Frozen<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }

        private static Pen RoundPen(Brush brush, double thickness) => Frozen(new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        });

        private static Brush WaveBrush(Color accent, double shade, double alpha, bool colorShift)
        {
            if (!colorShift)
                return Frozen(new SolidColorBrush(WithAlpha(Shade(accent, shade), alpha)));

            // Dark at the start of the song, light near the thumb (across each wave's own width)
            return Frozen(new LinearGradientBrush(
                WithAlpha(Shade(accent, -0.45), alpha), WithAlpha(Shade(accent, 0.5), alpha), 0));
        }

        private void EnsureResources(Color accent, VisualizerExtras extras)
        {
            if (accent == cachedAccent && extras == cachedExtras && ReferenceEquals(cachedTrack, TrackBrush) && wavePens != null)
                return;

            cachedAccent = accent;
            cachedExtras = extras;
            cachedTrack = TrackBrush;
            bool shift = (extras & VisualizerExtras.ColorShift) != 0;

            wavePens = Array.ConvertAll(AllLayers, l => RoundPen(WaveBrush(accent, l.Shade, l.Alpha, shift), 2));
            // A cheap glow: the same wave drawn wide and faint underneath (a blur effect would cost far more)
            glowPens = Array.ConvertAll(AllLayers, l => RoundPen(WaveBrush(accent, l.Shade, l.Alpha * 0.22, shift), 7));
            pastPen = RoundPen(Frozen(new SolidColorBrush(WithAlpha(accent, 0.22))), 1.5);

            accentBrush = Frozen(new SolidColorBrush(accent));
            hoverBrush = Frozen(new SolidColorBrush(Shade(accent, 0.2)));
            playedPen = RoundPen(shift ? WaveBrush(accent, 0, 1, true) : accentBrush, TrackThickness);
            trackPen = RoundPen(TrackBrush ?? Brushes.Gray, TrackThickness);

            var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            fill.GradientStops.Add(new GradientStop(WithAlpha(accent, 0.04), 0));
            fill.GradientStops.Add(new GradientStop(WithAlpha(accent, 0.26), 0.5));
            fill.GradientStops.Add(new GradientStop(WithAlpha(accent, 0.04), 1));
            fillBrush = Frozen(fill);

            sparkBrush = Frozen(new SolidColorBrush(Shade(accent, 0.6)));
            haloBrush = Frozen(new SolidColorBrush(WithAlpha(accent, 0.3)));
        }

        private float Level(int band) => band switch { 0 => bass, 1 => mid, _ => high };

        private double Amp(in Layer layer, double maxAmp) => (layer.Base + layer.Gain * Level(layer.Band)) * maxAmp;

        private double WaveY(in Layer layer, double x, double end, double mid0, double amp)
        {
            double fade = Math.Min(1, Math.Min((end - x) / 18, (x + 1) / 12));
            return mid0 + Math.Sin(x * layer.Wave + time * layer.Speed) * amp * fade;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 2 * ThumbRadius || h <= 0)
                return;

            var style = CurrentStyle;
            var accent = Accent is SolidColorBrush b ? b.Color : Colors.DeepPink;
            EnsureResources(accent, style.Extras);

            double fraction = Math.Clamp(Fraction / w, 0, 1);
            double end = ThumbRadius + fraction * (w - 2 * ThumbRadius);
            double mid0 = h / 2;
            double maxAmp = (h / 2 - 2) * style.Amount / 100.0;
            int layerCount = style.Has(VisualizerExtras.MoreWaves) ? AllLayers.Length : BaseLayers.Length;

            // Track: the part still to play
            dc.DrawLine(trackPen, new Point(TrackThickness / 2, mid0), new Point(w - TrackThickness / 2, mid0));

            // Faint echo of the waves on the part still to play
            if (style.Has(VisualizerExtras.PastThumb) && w - end > 24)
            {
                for (int i = 0; i < 2; i++)
                {
                    var layer = BaseLayers[i];
                    double amp = Amp(layer, maxAmp) * 0.35;
                    points.Clear();
                    for (double x = end + 6; x <= w - 4; x += 3)
                    {
                        double fade = Math.Min(1, Math.Min((x - end) / 30, (w - x) / 30));
                        points.Add(new Point(x, mid0 + Math.Sin(x * layer.Wave + time * layer.Speed) * amp * fade));
                    }
                    dc.DrawGeometry(null, pastPen, Polyline(new Point(end + 3, mid0)));
                }
            }

            if (end > 6)
            {
                // Soft fill between the highest and lowest wave
                if (style.Has(VisualizerExtras.Fill))
                {
                    top.Clear();
                    bottom.Clear();
                    for (double x = 3; x <= end; x += 3)
                    {
                        double hi = mid0, lo = mid0;
                        for (int i = 0; i < layerCount; i++)
                        {
                            double y = WaveY(AllLayers[i], x, end, mid0, Amp(AllLayers[i], maxAmp));
                            hi = Math.Min(hi, y);
                            lo = Math.Max(lo, y);
                        }
                        top.Add(hi);
                        bottom.Add(lo);
                    }
                    var area = new StreamGeometry();
                    using (var ctx = area.Open())
                    {
                        ctx.BeginFigure(new Point(0, mid0), true, true);
                        for (int i = 0; i < top.Count; i++)
                            ctx.LineTo(new Point(3 + i * 3, top[i]), false, false);
                        for (int i = bottom.Count - 1; i >= 0; i--)
                            ctx.LineTo(new Point(3 + i * 3, bottom[i]), false, false);
                    }
                    area.Freeze();
                    dc.DrawGeometry(fillBrush, null, area);
                }

                bool glow = style.Has(VisualizerExtras.Glow);
                for (int i = 0; i < layerCount; i++)
                {
                    var layer = AllLayers[i];
                    double amp = Amp(layer, maxAmp);
                    points.Clear();
                    for (double x = 3; x <= end; x += 3)
                        points.Add(new Point(x, WaveY(layer, x, end, mid0, amp)));

                    var geometry = Polyline(new Point(0, mid0));
                    if (glow)
                        dc.DrawGeometry(null, glowPens[i], geometry);
                    dc.DrawGeometry(null, wavePens[i], geometry);
                }

                // The played part of the track, on top of the waves
                dc.DrawLine(playedPen, new Point(TrackThickness / 2, mid0), new Point(end, mid0));
            }

            // Sparkles over the played part
            foreach (var s in sparks)
            {
                double x = s.X * end;
                if (x < 2)
                    continue;
                double r = 1 + s.Life * 1.5;
                dc.PushOpacity(Math.Clamp(s.Life, 0, 1));
                dc.DrawEllipse(sparkBrush, null, new Point(x, mid0 + s.Y * h * 0.8), r, r);
                dc.Pop();
            }

            // Thumb: a solid accent circle like the other sliders' (lighter while hovered); it can pulse with the bass
            var center = new Point(end, mid0);
            bool pulse = style.Has(VisualizerExtras.ThumbPulse);
            if (pulse && bass > 0.05f)
                dc.DrawEllipse(haloBrush, null, center, ThumbRadius + 6 * bass, ThumbRadius + 6 * bass);
            double radius = ThumbRadius + (pulse ? 1.5 * bass : 0);
            dc.DrawEllipse(IsHovered ? hoverBrush : accentBrush, null, center, radius, radius);
        }

        private static Layer[] Concat(Layer[] a, Layer[] b)
        {
            var all = new Layer[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        private StreamGeometry Polyline(Point start)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, false, false);
                if (points.Count > 0)
                    ctx.PolyLineTo(points, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        #endregion
    }
}
