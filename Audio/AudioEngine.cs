using System;
using System.Threading;
using System.Threading.Tasks;

namespace WhiteNoise.Audio
{
    /// <summary>
    /// Noise pattern shapes for LFO modulation.
    /// </summary>
    public enum NoisePattern
    {
        Constant,   // flat, no modulation
        Wave,       // smooth sine LFO
        Rain,       // faster, irregular amplitude flutter
        Pulse,      // rhythmic on/off pulsing
        Breathing   // slow deep inhale/exhale shape
    }

    /// <summary>
    /// Platform-agnostic audio engine.
    /// Produces a PCM float buffer that the platform audio layer streams.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        // ── public state ────────────────────────────────────────────────────
        public float Volume        { get; set; } = 0.5f;   // 0..1
        public NoisePattern Pattern { get; set; } = NoisePattern.Wave;
        public bool CrackleEnabled { get; set; } = false;
        public float CrackleIntensity { get; set; } = 0.3f; // 0..1

        // Duration / fade
        public TimeSpan Duration   { get; set; } = TimeSpan.Zero; // Zero = infinite
        public bool FadeOut        { get; set; } = true;
        public TimeSpan FadeDuration { get; set; } = TimeSpan.FromSeconds(30);

        // ── audio constants ──────────────────────────────────────────────────
        public const int SampleRate   = 44100;
        public const int Channels     = 2;
        public const int BufferFrames = 1024; // ~23ms at 44100 Hz

        // ── private state ────────────────────────────────────────────────────
        private readonly Random   _rng     = new Random();
        private double            _lfoPhase = 0.0;       // 0..2π
        private double            _time     = 0.0;       // seconds since start
        private DateTime          _startTime;
        private bool              _running  = false;
        private CancellationTokenSource? _cts;
        private IPlatformAudioOutput? _output;

        // crackle state
        private double _crackleTimer = 0.0;
        private double _nextCrackle  = 0.0;
        private float  _crackleDecay = 0.0f;
        private float  _crackleAmp   = 0.0f;

        // ── lifecycle ────────────────────────────────────────────────────────

        public void Start(IPlatformAudioOutput output)
        {
            if (_running) return;
            _output    = output;
            _startTime = DateTime.UtcNow;
            _time      = 0.0;
            _lfoPhase  = 0.0;
            _running   = true;
            _cts       = new CancellationTokenSource();
            output.Initialize(SampleRate, Channels, BufferFrames);
            output.Start(FillBuffer);
        }

        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
            _output?.Stop();
            _output?.Dispose();
            _output = null;
        }

        public void Dispose() => Stop();

        // ── DSP core ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the platform audio layer to fill one buffer.
        /// Returns false when playback should end (duration elapsed).
        /// </summary>
        public bool FillBuffer(float[] buffer, int frameCount)
        {
            if (!_running) return false;

            double elapsed   = (DateTime.UtcNow - _startTime).TotalSeconds;
            double totalSecs = Duration.TotalSeconds;
            bool   hasEnd    = totalSecs > 0;

            // should we stop?
            if (hasEnd && elapsed >= totalSecs)
            {
                Array.Clear(buffer, 0, buffer.Length);
                return false;
            }

            double dt = 1.0 / SampleRate;

            for (int i = 0; i < frameCount; i++)
            {
                _time += dt;

                // ── white noise base ─────────────────────────────────────────
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);

                // ── LFO modulation ───────────────────────────────────────────
                float lfoGain = ComputeLfo(_time);

                // ── crackle layer ────────────────────────────────────────────
                float crackle = CrackleEnabled ? ComputeCrackle(dt) : 0f;

                // ── sample assembly ──────────────────────────────────────────
                float sample = noise * lfoGain + crackle;

                // ── fade envelope ────────────────────────────────────────────
                float fade = ComputeFade(elapsed + i * dt, totalSecs);

                sample *= fade * Volume;
                sample  = Math.Clamp(sample, -1f, 1f);

                buffer[i * Channels]     = sample; // L
                buffer[i * Channels + 1] = sample; // R
            }

            return true;
        }

        // ── LFO shapes ───────────────────────────────────────────────────────

        private float ComputeLfo(double t)
        {
            return Pattern switch
            {
                NoisePattern.Constant  => 1.0f,
                NoisePattern.Wave      => LfoWave(t, freq: 0.15),
                NoisePattern.Rain      => LfoRain(t),
                NoisePattern.Pulse     => LfoPulse(t, freq: 0.5),
                NoisePattern.Breathing => LfoBreathing(t),
                _                      => 1.0f
            };
        }

        // Smooth sine wave: amplitude gently rises and falls
        private float LfoWave(double t, double freq)
        {
            double phase = t * freq * 2.0 * Math.PI;
            return 0.6f + 0.4f * (float)Math.Sin(phase);
        }

        // Rain: fast irregular flicker simulating rainfall variation
        private float LfoRain(double t)
        {
            double fast  = Math.Sin(t * 3.7 * Math.PI * 2.0);
            double slow  = Math.Sin(t * 0.4 * Math.PI * 2.0);
            double noise = _rng.NextDouble() * 0.15;
            return (float)Math.Max(0.2, 0.55 + 0.3 * fast * slow + noise);
        }

        // Rhythmic pulse: hard gated square-ish wave, softened with tanh
        private float LfoPulse(double t, double freq)
        {
            double phase = (t * freq) % 1.0;
            double saw   = Math.Sin(phase * Math.PI * 2.0);
            return (float)(0.5 + 0.5 * Math.Tanh(saw * 4.0));
        }

        // Breathing: asymmetric slow inhale (fast rise) / exhale (slow fall)
        private float LfoBreathing(double t)
        {
            double cycle = t % 5.5;           // ~5.5s breath cycle
            double norm  = cycle / 5.5;       // 0..1
            double shape;
            if (norm < 0.35)
                shape = norm / 0.35;          // fast inhale
            else
                shape = 1.0 - (norm - 0.35) / 0.65; // slow exhale
            return (float)(0.25 + 0.75 * shape);
        }

        // ── crackle / firewood ───────────────────────────────────────────────

        private float ComputeCrackle(double dt)
        {
            _crackleTimer += dt;

            // trigger a new crackle event?
            if (_crackleTimer >= _nextCrackle)
            {
                _crackleTimer = 0.0;
                // schedule next: Poisson-ish inter-arrival based on intensity
                double rate   = 1.0 + CrackleIntensity * 15.0; // 1..16 events/sec
                _nextCrackle  = -Math.Log(_rng.NextDouble() + 1e-9) / rate;
                _crackleAmp   = (float)(_rng.NextDouble() * 0.6 + 0.2) * CrackleIntensity;
                _crackleDecay = (float)(_rng.NextDouble() * 0.004 + 0.001); // fast decay
            }

            if (_crackleAmp < 0.0001f) return 0f;

            float sample  = (float)(_rng.NextDouble() * 2.0 - 1.0) * _crackleAmp;
            _crackleAmp  *= (1.0f - _crackleDecay * SampleRate * (float)dt);
            if (_crackleAmp < 0.0001f) _crackleAmp = 0f;
            return sample;
        }

        // ── fade envelope ────────────────────────────────────────────────────

        private float ComputeFade(double elapsed, double totalSecs)
        {
            if (!FadeOut || totalSecs <= 0) return 1.0f;

            double fadeSecs  = Math.Min(FadeDuration.TotalSeconds, totalSecs);
            double fadeStart = totalSecs - fadeSecs;

            if (elapsed < fadeStart) return 1.0f;

            double t = (elapsed - fadeStart) / fadeSecs; // 0..1
            // smooth cosine fade
            return (float)(0.5 + 0.5 * Math.Cos(t * Math.PI));
        }
    }
}
