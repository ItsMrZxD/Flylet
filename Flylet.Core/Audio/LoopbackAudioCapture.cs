using NAudio.Wave;
using System;
using System.Diagnostics;

namespace Flylet.Core.Audio
{
    /// <summary>
    /// Listens to what the default playback device is playing (WASAPI loopback through NAudio, which Flylet already
    /// ships; not the microphone) and turns it into three smoothed levels, bass, mid and high, each 0-1, for the
    /// music visualizer. Runs only while at least one viewer holds it. On Windows 10 it hears all system sound.
    /// </summary>
    public sealed class LoopbackAudioCapture
    {
        public static LoopbackAudioCapture Shared { get; } = new();

        private const int FftSize = 1024;

        private readonly object gate = new();
        private int viewers;
        private WasapiLoopbackCapture capture;

        private readonly float[] ring = new float[FftSize];
        private int ringPos;
        private readonly double[] re = new double[FftSize];
        private readonly double[] im = new double[FftSize];
        private readonly double[] window = new double[FftSize];
        private double peakBass = 1e-3, peakMid = 1e-3, peakHigh = 1e-3;
        private float sBass, sMid, sHigh;
        private long lastDataTicks;
        private long lastFftTicks;

        // Written by NAudio's capture thread, read by the UI thread every frame
        private volatile float bass, mid, high;

        public LoopbackAudioCapture()
        {
            for (int i = 0; i < FftSize; i++)
                window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftSize - 1));
        }

        // Windows sends no packets while nothing plays, so levels fade out once the data stops
        private float Fade(float level)
        {
            double age = (Stopwatch.GetTimestamp() - lastDataTicks) / (double)Stopwatch.Frequency;
            return age < 0.1 ? level : (float)(level * Math.Max(0, 1 - (age - 0.1) / 0.3));
        }

        public float Bass => Fade(bass);
        public float Mid => Fade(mid);
        public float High => Fade(high);

        public void Acquire()
        {
            lock (gate)
            {
                viewers++;
                if (viewers != 1)
                    return;

                try
                {
                    capture = new WasapiLoopbackCapture();
                    capture.DataAvailable += OnDataAvailable;
                    capture.RecordingStopped += (_, e) =>
                    {
                        if (e.Exception != null)
                            Debug.WriteLine($"Loopback capture stopped: {e.Exception.Message}");
                    };
                    capture.StartRecording();
                }
                catch (Exception ex)
                {
                    // No playback device, or audio blocked: the waves just stay calm
                    Debug.WriteLine($"Loopback capture failed: {ex.Message}");
                    capture?.Dispose();
                    capture = null;
                }
            }
        }

        public void Release()
        {
            lock (gate)
            {
                if (viewers == 0)
                    return;
                viewers--;
                if (viewers != 0)
                    return;

                if (capture != null)
                {
                    capture.DataAvailable -= OnDataAvailable;
                    try { capture.StopRecording(); } catch { }
                    capture.Dispose();
                    capture = null;
                }
                bass = mid = high = 0;
                sBass = sMid = sHigh = 0;
            }
        }

        /// <summary>
        /// Listens to the default playback device as it is now. A capture stays on the device it was opened
        /// on, so after the default device changes it hears nothing. Does nothing while no viewer holds it.
        /// </summary>
        public void Restart()
        {
            lock (gate)
            {
                if (viewers == 0)
                    return;

                // Stop as the last viewer and start as the first, then count the other viewers again
                int held = viewers;
                viewers = 1;
                Release();
                Acquire();
                viewers = held;
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            var format = ((WasapiLoopbackCapture)sender).WaveFormat;
            if (format.Encoding != WaveFormatEncoding.IeeeFloat && format.Encoding != WaveFormatEncoding.Extensible)
                return;
            if (format.BitsPerSample != 32 || e.BytesRecorded == 0)
                return;

            int channels = format.Channels;
            int frames = e.BytesRecorded / (4 * channels);
            for (int f = 0; f < frames; f++)
            {
                float mono = 0;
                for (int c = 0; c < channels; c++)
                    mono += BitConverter.ToSingle(e.Buffer, (f * channels + c) * 4);
                ring[ringPos] = mono / channels;
                ringPos = (ringPos + 1) % FftSize;
            }

            long now = Stopwatch.GetTimestamp();
            lastDataTicks = now;

            // One analysis per ~15 ms is plenty for 60 fps waves
            if ((now - lastFftTicks) / (double)Stopwatch.Frequency < 0.015)
                return;
            lastFftTicks = now;

            for (int i = 0; i < FftSize; i++)
            {
                re[i] = ring[(ringPos + i) % FftSize] * window[i];
                im[i] = 0;
            }
            Fft(re, im);

            double binHz = (double)format.SampleRate / FftSize;
            double b = Band(30, 150, binHz);
            double m = Band(150, 2000, binHz);
            double h = Band(2000, 8000, binHz);

            // Slow automatic gain per band, so quiet and loud songs both move the waves
            peakBass = Math.Max(b, peakBass * 0.995);
            peakMid = Math.Max(m, peakMid * 0.995);
            peakHigh = Math.Max(h, peakHigh * 0.995);

            sBass = Smooth(sBass, (float)(b / peakBass));
            sMid = Smooth(sMid, (float)(m / peakMid));
            sHigh = Smooth(sHigh, (float)(h / peakHigh));
            bass = sBass; mid = sMid; high = sHigh;
        }

        // Fast attack, slower release
        private static float Smooth(float current, float target)
            => target > current ? current + (target - current) * 0.6f : current + (target - current) * 0.15f;

        private double Band(double fromHz, double toHz, double binHz)
        {
            int from = Math.Max(1, (int)(fromHz / binHz));
            int to = Math.Min(FftSize / 2 - 1, (int)(toHz / binHz));
            double sum = 0;
            for (int i = from; i <= to; i++)
                sum += Math.Sqrt(re[i] * re[i] + im[i] * im[i]);
            return sum / Math.Max(1, to - from + 1);
        }

        private static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, bb = i + k + len / 2;
                        double tr = re[bb] * cr - im[bb] * ci;
                        double ti = re[bb] * ci + im[bb] * cr;
                        re[bb] = re[a] - tr; im[bb] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }
    }
}
