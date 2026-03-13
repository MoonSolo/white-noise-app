using System;
using System.Threading;

namespace WhiteNoise.Audio
{
    public enum NoisePattern
    {
        Constant,
        Wave,
        Rain,
        Pulse,
        Breathing,
        Custom      // user-controlled noise colour + wave speed
    }

    public class AudioEngine : IDisposable
    {
        // ── public state ─────────────────────────────────────────────────────
        public float Volume           { get; set; } = 0.5f;
        public NoisePattern Pattern   { get; set; } = NoisePattern.Wave;
        public bool CrackleEnabled    { get; set; } = false;
        public float CrackleIntensity { get; set; } = 0.3f;

        // Custom mode parameters
        // NoiseFrequency: low-pass cutoff on the raw noise.
        //   100 Hz  → deep brown/red rumble
        //   1000 Hz → pink-ish
        //   20000 Hz → flat white (no filtering)
        public float NoiseFrequency { get; set; } = 20000f;

        // WaveFrequency: LFO speed in cycles per minute (1 = very slow, 60 = 1 Hz)
        public float WaveFrequency  { get; set; } = 9f;

        // Duration / fade
        public TimeSpan Duration     { get; set; } = TimeSpan.Zero;
        public bool FadeOut          { get; set; } = true;
        public TimeSpan FadeDuration { get; set; } = TimeSpan.FromSeconds(30);

        // ── audio constants ──────────────────────────────────────────────────
        public const int SampleRate   = 44100;
        public const int Channels     = 2;
        public const int BufferFrames = 1024;

        // ── private state ────────────────────────────────────────────────────
        private readonly Random _rng     = new Random();
        private double          _time    = 0.0;
        private DateTime        _startTime;
        private bool            _running = false;
        private CancellationTokenSource? _cts;
        private IPlatformAudioOutput?    _output;

        // one-pole low-pass filter accumulator
        private float _lpState = 0f;

        // crackle state
        private double _crackleTimer = 0.0;
        private double _nextCrackle  = 0.0;
        private float  _crackleDecay = 0.0f;
        private float  _crackleAmp   = 0.0f;

        // ── lifecycle ─────────────────────────────────────────────────────────

        public void Start(IPlatformAudioOutput output)
        {
            if (_running) return;
            _output    = output;
            _startTime = DateTime.UtcNow;
            _time      = 0.0;
            _lpState   = 0f;
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

        public bool FillBuffer(float[] buffer, int frameCount)
        {
            if (!_running) return false;

            double elapsed   = (DateTime.UtcNow - _startTime).TotalSeconds;
            double totalSecs = Duration.TotalSeconds;

            if (totalSecs > 0 && elapsed >= totalSecs)
            {
                Array.Clear(buffer, 0, buffer.Length);
                return false;
            }

            double dt = 1.0 / SampleRate;

            // Low-pass coefficient: a = 1 - exp(-2π * fc / fs)
            // At fc=20000 Hz, a≈1 → no filtering (white). At fc=100 Hz → deep rumble.
            float fc  = Math.Clamp(NoiseFrequency, 80f, 20000f);
            float lpA = 1f - (float)Math.Exp(-2.0 * Math.PI * fc / SampleRate);

            for (int i = 0; i < frameCount; i++)
            {
                _time += dt;

                // ── raw white noise ───────────────────────────────────────────
                float raw = (float)(_rng.NextDouble() * 2.0 - 1.0);

                // ── colour filter (always run, neutral at 20 kHz) ─────────────
                _lpState += lpA * (raw - _lpState);

                // In Custom mode use filtered signal; others use flat white noise
                float noise = Pattern == NoisePattern.Custom ? _lpState : raw;

                // ── LFO modulation ────────────────────────────────────────────
                float lfoGain = ComputeLfo(_time);

                // ── crackle layer ─────────────────────────────────────────────
                float crackle = CrackleEnabled ? ComputeCrackle(dt) : 0f;

                // ── mix + volume + fade ───────────────────────────────────────
                float fade   = ComputeFade(elapsed + i * dt, totalSecs);
                float sample = (noise * lfoGain + crackle) * fade * Volume;
                sample = Math.Clamp(sample, -1f, 1f);

                buffer[i * Channels]     = sample;
                buffer[i * Channels + 1] = sample;
            }

            return true;
        }

        // ── LFO shapes ───────────────────────────────────────────────────────

        private float ComputeLfo(double t)
        {
            return Pattern switch
            {
                NoisePattern.Constant  => 1.0f,
                NoisePattern.Wave      => LfoSine(t, freqHz: 0.15),
                NoisePattern.Rain      => LfoRain(t),
                NoisePattern.Pulse     => LfoPulse(t, freqHz: 0.5),
                NoisePattern.Breathing => LfoBreathing(t),
                // Custom: WaveFrequency is in cycles/min → divide by 60 for Hz
                NoisePattern.Custom    => LfoSine(t, freqHz: WaveFrequency / 60.0),
                _                      => 1.0f
            };
        }

        private float LfoSine(double t, double freqHz)
        {
            return 0.6f + 0.4f * (float)Math.Sin(t * freqHz * 2.0 * Math.PI);
        }

        private float LfoRain(double t)
        {
            double fast  = Math.Sin(t * 3.7 * Math.PI * 2.0);
            double slow  = Math.Sin(t * 0.4 * Math.PI * 2.0);
            double noise = _rng.NextDouble() * 0.15;
            return (float)Math.Max(0.2, 0.55 + 0.3 * fast * slow + noise);
        }

        private float LfoPulse(double t, double freqHz)
        {
            double saw = Math.Sin((t * freqHz % 1.0) * Math.PI * 2.0);
            return (float)(0.5 + 0.5 * Math.Tanh(saw * 4.0));
        }

        private float LfoBreathing(double t)
        {
            double norm  = (t % 5.5) / 5.5;
            double shape = norm < 0.35 ? norm / 0.35 : 1.0 - (norm - 0.35) / 0.65;
            return (float)(0.25 + 0.75 * shape);
        }

        // ── crackle ──────────────────────────────────────────────────────────

        private float ComputeCrackle(double dt)
        {
            _crackleTimer += dt;
            if (_crackleTimer >= _nextCrackle)
            {
                _crackleTimer = 0.0;
                double rate   = 1.0 + CrackleIntensity * 15.0;
                _nextCrackle  = -Math.Log(_rng.NextDouble() + 1e-9) / rate;
                _crackleAmp   = (float)(_rng.NextDouble() * 0.6 + 0.2) * CrackleIntensity;
                _crackleDecay = (float)(_rng.NextDouble() * 0.004 + 0.001);
            }
            if (_crackleAmp < 0.0001f) return 0f;
            float s      = (float)(_rng.NextDouble() * 2.0 - 1.0) * _crackleAmp;
            _crackleAmp *= 1.0f - _crackleDecay * SampleRate * (float)dt;
            if (_crackleAmp < 0.0001f) _crackleAmp = 0f;
            return s;
        }

        // ── fade ─────────────────────────────────────────────────────────────

        private float ComputeFade(double elapsed, double totalSecs)
        {
            if (!FadeOut || totalSecs <= 0) return 1.0f;
            double fadeSecs  = Math.Min(FadeDuration.TotalSeconds, totalSecs);
            double fadeStart = totalSecs - fadeSecs;
            if (elapsed < fadeStart) return 1.0f;
            double t = (elapsed - fadeStart) / fadeSecs;
            return (float)(0.5 + 0.5 * Math.Cos(t * Math.PI));
        }
    }
}
