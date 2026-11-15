using Flylet.Core.Audio;
using System;
using System.Windows;
using System.Windows.Media;

namespace Flylet.Controls
{
    /// <summary>
    /// SPIKE (visualizer-spike branch): three waves in shades of the accent that follow the music's bass, mid and
    /// high, drawn over the played part of the timeline. Captures audio and redraws only while on screen.
    /// </summary>
    public sealed class VisualizerWaves : FrameworkElement
    {
        public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
            nameof(Accent), typeof(Brush), typeof(VisualizerWaves),
            new FrameworkPropertyMetadata(Brushes.DeepPink, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush Accent
        {
            get => (Brush)GetValue(AccentProperty);
            set => SetValue(AccentProperty, value);
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

        private bool running;
        private double time;
        private int frames, renders;

        public VisualizerWaves()
        {
            IsHitTestVisible = false;
            IsVisibleChanged += (_, _) => UpdateRunning();
            Unloaded += (_, _) => SetRunning(false);
            Loaded += (_, _) => UpdateRunning();
        }

        // Only in the real flyout: the Settings preview stays visible while Settings is open (even minimized)
        private void UpdateRunning() => SetRunning(IsLoaded && IsVisible && Window.GetWindow(this) is not SettingsWindow);

        private void SetRunning(bool run)
        {
            if (run == running)
                return;
            running = run;
            if (run)
            {
                LoopbackAudioCapture.Shared.Acquire();
                lastFrame = TimeSpan.Zero;
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
                LoopbackAudioCapture.Shared.Release();
            }
        }

        // Every frame WPF draws (60 a second), in step with the screen, so the waves move smoothly. Cheap now that
        // nothing is created per frame except the three wave shapes
        private TimeSpan lastFrame;

        private void OnRendering(object sender, EventArgs e)
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (now == lastFrame)
                return;
            time += lastFrame == TimeSpan.Zero ? 1 / 60.0 : Math.Min(0.1, (now - lastFrame).TotalSeconds);
            lastFrame = now;
            frames++;
            InvalidateVisual();
        }

        private static Color Shade(Color c, double f)
        {
            byte S(byte v) => (byte)Math.Round(f > 0 ? v + (255 - v) * f : v * (1 + f));
            return Color.FromRgb(S(c.R), S(c.G), S(c.B));
        }

        private static readonly (double Shade, double Alpha, double Speed, double Wave)[] Layers =
        {
            (0.55, 0.40, 1.4, 0.05),
            (-0.25, 0.60, -1.0, 0.08),
            (0.25, 0.80, 2.4, 0.13),
        };

        // Pens only change with the accent, not every frame
        private Color penColor;
        private Pen[] pens;
        private readonly System.Collections.Generic.List<Point> points = new();

        private Pen[] PensFor(Color accent)
        {
            if (pens != null && accent == penColor)
                return pens;
            penColor = accent;
            pens = new Pen[Layers.Length];
            for (int i = 0; i < Layers.Length; i++)
            {
                var color = Shade(accent, Layers[i].Shade);
                color.A = (byte)(Layers[i].Alpha * 255);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                pens[i] = new Pen(brush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                pens[i].Freeze();
            }
            return pens;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            renders++;

            // The slider's thumb center runs from its radius to width minus its radius
            const double thumbRadius = 10;
            double fraction = w > 0 ? Math.Clamp(Fraction / w, 0, 1) : 0;
            double end = thumbRadius + fraction * (w - 2 * thumbRadius);
            if (end < 4 || h <= 0)
                return;

            var capture = LoopbackAudioCapture.Shared;
            var accent = Accent is SolidColorBrush b ? b.Color : Colors.DeepPink;
            var layerPens = PensFor(accent);
            double mid = h / 2, maxAmp = h / 2 - 2;
            double[] amps =
            {
                (0.15 + 0.85 * capture.Bass) * maxAmp,
                (0.12 + 0.70 * capture.Mid) * maxAmp,
                (0.08 + 0.50 * capture.High) * maxAmp,
            };

            for (int i = 0; i < Layers.Length; i++)
            {
                var layer = Layers[i];
                points.Clear();
                for (double x = 3; x <= end; x += 3)
                {
                    double fade = Math.Min(1, Math.Min((end - x) / 18, (x + 1) / 12));
                    points.Add(new Point(x, mid + Math.Sin(x * layer.Wave + time * layer.Speed) * amps[i] * fade));
                }

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(0, mid), false, false);
                    ctx.PolyLineTo(points, true, true);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, layerPens[i], geometry);
            }
        }
    }
}
