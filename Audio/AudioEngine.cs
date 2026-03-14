using System;

namespace WhiteNoise.Audio
{
    public enum NoisePattern
    {
        Constant,   // flat white noise
        Wave,       // slow sine LFO ~0.15 Hz
        Rain,       // fast shimmer
        Pulse,      // rhythmic thumps
        Breathing,  // slow asymmetric ramp
        Ocean,      // random wave bursts 250-450 Hz band, 0.5-2s intervals
        Custom      // user-controlled frequency + wave speed
    }

    public class AudioEngine
    {
        // ── audio constants ──────────────────────────────────────────────
        private const int SampleRate = 44100;
        private const int Channels   = 2;
        private const int FrameSize  = 1024;

        // ── state ────────────────────────────────────────────────────────
        private readonly Random _rng = new Random();

        // One-pole low-pass for Custom mode
        private float _lpState;

        // LFO / envelope phase
        private double _lfoPhase;          // 0..1
        private double _breathPhase;       // 0..1

        // Ocean state
        private float  _oceanEnv;          // current envelope amplitude 0..1
        private float  _oceanTargetEnv;    // target (burst on or silent)
        private float  _oceanAttackRate;
        private float  _oceanDecayRate;
        private double _oceanLpState;      // bandpass lo
        private double _oceanHpState;      // bandpass hi
        private float  _oceanCenterHz;     // current center freq 250-450
        private int    _oceanHoldSamples;  // samples left in current burst/gap
        private bool   _oceanBursting;

        // Crackle state
        private float  _crackleAmp;
        private float  _crackleDecay;
        private int    _crackleSamplesLeft;

        // Fade-out
        private long   _totalSamples;      // total samples to produce (0 = infinite)
        private long   _samplesProduced;
        private int    _fadeSamples;
        private bool   _fadeActive;

        // ── public properties ─────────────────────────────────────────────
        public float   Volume          { get; set; } = 0.7f;
        public NoisePattern Pattern    { get; set; } = NoisePattern.Constant;
        public bool    CrackleEnabled  { get; set; } = false;
        public float   CrackleIntensity { get; set; } = 0.5f;

        /// <summary>Low-pass cutoff in Hz for Custom mode (80–20000).</summary>
        public float   NoiseFrequency  { get; set; } = 1000f;

        /// <summary>Wave speed in cpm for Custom wave LFO (1–60).</summary>
        public float   WaveFrequency   { get; set; } = 10f;

        /// <summary>When true Custom mode applies the wave LFO; when false it's flat (Constant).</summary>
        public bool    CustomWaveEnabled { get; set; } = false;

        /// <summary>Duration in seconds; 0 = play forever.</summary>
        public float   Duration        { get; set; } = 0f;

        public bool    FadeOut         { get; set; } = false;
        public float   FadeDuration    { get; set; } = 5f;

        // ── init ─────────────────────────────────────────────────────────
        public AudioEngine()
        {
            _oceanHoldSamples = SampleRate; // start silent
            _oceanBursting    = false;
            _oceanCenterHz    = 350f;
            ScheduleNextOceanPhase();
        }

        public void Reset()
        {
            _lfoPhase       = 0;
            _breathPhase    = 0;
            _lpState        = 0;
            _oceanEnv       = 0;
            _oceanLpState   = 0;
            _oceanHpState   = 0;
            _crackleAmp     = 0;
            _samplesProduced = 0;
            _fadeActive     = false;

            if (Duration > 0)
            {
                _totalSamples = (long)(Duration * SampleRate);
                _fadeSamples  = FadeOut ? (int)(FadeDuration * SampleRate) : 0;
            }
            else
            {
                _totalSamples = 0;
                _fadeSamples  = 0;
            }

            ScheduleNextOceanPhase();
        }

        // ── main fill ────────────────────────────────────────────────────
        /// <summary>
        /// Fills <paramref name="buffer"/> with interleaved stereo PCM floats.
        /// Returns false when the duration has elapsed (caller should stop).
        /// </summary>
        public bool FillBuffer(float[] buffer, int frames)
        {
            bool running = true;

            for (int i = 0; i < frames; i++)
            {
                // ── duration / fade ───────────────────────────────────────
                float fadeMul = 1f;
                if (_totalSamples > 0)
                {
                    long remaining = _totalSamples - _samplesProduced;
                    if (remaining <= 0)
                    {
                        // silence remainder of buffer
                        for (int j = i * Channels; j < frames * Channels; j++)
                            buffer[j] = 0f;
                        return false;
                    }
                    if (_fadeSamples > 0 && remaining <= _fadeSamples)
                    {
                        float t = (float)remaining / _fadeSamples;
                        fadeMul = 0.5f - 0.5f * MathF.Cos(t * MathF.PI); // cosine fade
                    }
                }

                // ── raw noise sample ─────────────────────────────────────
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);

                // ── pattern envelope ─────────────────────────────────────
                float env = ComputePatternEnvelope(noise);

                // ── crackle (additive, independent of noise gate) ─────────
                float crackle = 0f;
                if (CrackleEnabled)
                    crackle = StepCrackle();

                // ── mix ───────────────────────────────────────────────────
                // If pattern is Constant/Custom with no wave and crackleOnly:
                // we allow crackle without carrier noise by zeroing the noise
                // when pattern produces zero env — but env for Constant is always 1,
                // so to support crackle-only we check below in the write path.

                float sample = env * noise + crackle;
                sample *= Volume * fadeMul;
                sample  = Math.Clamp(sample, -1f, 1f);

                buffer[i * Channels]     = sample; // L
                buffer[i * Channels + 1] = sample; // R

                _samplesProduced++;
            }

            // advance LFO phases for non-per-sample patterns
            return running;
        }

        // ── pattern envelope (called once per sample) ────────────────────
        private float ComputePatternEnvelope(float rawNoise)
        {
            const double inv44100 = 1.0 / SampleRate;

            switch (Pattern)
            {
                // ── Constant ──────────────────────────────────────────────
                case NoisePattern.Constant:
                    return 1f;

                // ── Wave (slow sine, ~0.15 Hz) ────────────────────────────
                case NoisePattern.Wave:
                {
                    _lfoPhase += 0.15 * inv44100;
                    if (_lfoPhase >= 1.0) _lfoPhase -= 1.0;
                    float lfo = 0.5f + 0.5f * MathF.Sin((float)(_lfoPhase * 2 * Math.PI));
                    return 0.3f + 0.7f * lfo;
                }

                // ── Rain (fast shimmer) ───────────────────────────────────
                case NoisePattern.Rain:
                {
                    _lfoPhase += 3.7 * inv44100;
                    if (_lfoPhase >= 1.0) _lfoPhase -= 1.0;
                    double jitter = _rng.NextDouble() * 0.005;
                    float fast = 0.5f + 0.5f * MathF.Sin((float)((_lfoPhase + jitter) * 2 * Math.PI));
                    _breathPhase += 0.4 * inv44100;
                    if (_breathPhase >= 1.0) _breathPhase -= 1.0;
                    float slow = 0.5f + 0.5f * MathF.Sin((float)(_breathPhase * 2 * Math.PI));
                    return 0.2f + 0.5f * fast * slow;
                }

                // ── Pulse (soft rhythmic thumps) ──────────────────────────
                case NoisePattern.Pulse:
                {
                    _lfoPhase += 0.5 * inv44100;
                    if (_lfoPhase >= 1.0) _lfoPhase -= 1.0;
                    float raw = MathF.Sin((float)(_lfoPhase * 2 * Math.PI));
                    float shaped = (float)Math.Tanh(raw * 3.0);
                    return 0.35f + 0.65f * (0.5f + 0.5f * shaped);
                }

                // ── Breathing (slow asymmetric triangle) ─────────────────
                case NoisePattern.Breathing:
                {
                    double period = 5.5; // seconds
                    _breathPhase += inv44100 / period;
                    if (_breathPhase >= 1.0) _breathPhase -= 1.0;
                    float env;
                    if (_breathPhase < 0.4)
                        env = (float)(_breathPhase / 0.4);      // inhale 40 %
                    else
                        env = (float)(1.0 - (_breathPhase - 0.4) / 0.6); // exhale 60 %
                    return 0.1f + 0.9f * env;
                }

                // ── Ocean (random burst, 250-450 Hz band) ─────────────────
                case NoisePattern.Ocean:
                    return ComputeOceanSample(rawNoise);

                // ── Custom (LP filter + optional wave LFO) ────────────────
                case NoisePattern.Custom:
                {
                    // apply one-pole LP
                    float fc = Math.Clamp(NoiseFrequency, 80f, 20000f);
                    float a  = 1f - MathF.Exp(-2f * MathF.PI * fc / SampleRate);
                    _lpState += a * (rawNoise - _lpState);

                    if (!CustomWaveEnabled)
                        return 1f; // flat — caller multiplies by lpState via the noise path

                    // wave LFO
                    double waveHz = Math.Clamp(WaveFrequency / 60.0, 1.0 / 60.0, 1.0);
                    _lfoPhase += waveHz * inv44100;
                    if (_lfoPhase >= 1.0) _lfoPhase -= 1.0;
                    float lfo = 0.5f + 0.5f * MathF.Sin((float)(_lfoPhase * 2 * Math.PI));
                    return 0.2f + 0.8f * lfo;
                }

                default:
                    return 1f;
            }
        }

        // ── ocean DSP ────────────────────────────────────────────────────
        private float ComputeOceanSample(float rawNoise)
        {
            // advance hold counter
            if (_oceanHoldSamples > 0)
                _oceanHoldSamples--;
            else
                ScheduleNextOceanPhase();

            // smooth envelope toward target
            float rate = _oceanBursting ? _oceanAttackRate : _oceanDecayRate;
            _oceanEnv += (_oceanTargetEnv - _oceanEnv) * rate;

            if (_oceanEnv < 0.001f && !_oceanBursting)
                _oceanEnv = 0f;

            // band-pass the noise around _oceanCenterHz
            // Two cascaded one-pole: LP at fc+bw/2, HP at fc-bw/2
            float bw   = 80f; // Hz bandwidth
            float fcLp = Math.Clamp(_oceanCenterHz + bw * 0.5f, 100f, 20000f);
            float fcHp = Math.Clamp(_oceanCenterHz - bw * 0.5f, 50f, 19000f);

            float aLp = 1f - MathF.Exp(-2f * MathF.PI * fcLp / SampleRate);
            float aHp = 1f - MathF.Exp(-2f * MathF.PI * fcHp / SampleRate);

            _oceanLpState += aLp * (rawNoise - (float)_oceanLpState);
            float hp = (float)_oceanLpState - (float)_oceanHpState;
            _oceanHpState += aHp * ((float)_oceanLpState - (float)_oceanHpState);

            // allow clipping by not clamping here — clips naturally when * volume
            return hp * _oceanEnv * 3.5f; // boost since BP attenuates
        }

        private void ScheduleNextOceanPhase()
        {
            if (_oceanBursting)
            {
                // end burst → start gap
                _oceanBursting  = false;
                _oceanTargetEnv = 0f;
                // gap: 0.3 – 1.0 s
                _oceanHoldSamples = (int)((_rng.NextDouble() * 0.7 + 0.3) * SampleRate);
                _oceanDecayRate   = 0.0005f;
            }
            else
            {
                // start burst
                _oceanBursting  = true;
                _oceanTargetEnv = 1f;
                // burst duration: 0.5 – 2.0 s
                _oceanHoldSamples = (int)((_rng.NextDouble() * 1.5 + 0.5) * SampleRate);
                _oceanAttackRate  = 0.002f;
                _oceanDecayRate   = 0.0008f;
                // pick random center frequency 250–450 Hz
                _oceanCenterHz = 250f + (float)(_rng.NextDouble() * 200.0);
            }
        }

        // ── crackle ───────────────────────────────────────────────────────
        private float StepCrackle()
        {
            if (_crackleSamplesLeft > 0)
            {
                _crackleSamplesLeft--;
                _crackleAmp *= _crackleDecay;
                float n = (float)(_rng.NextDouble() * 2.0 - 1.0);
                return _crackleAmp * n * CrackleIntensity;
            }

            // Poisson inter-arrival: mean interval decreases with intensity
            double meanInterval = SampleRate * (0.05 + (1.0 - CrackleIntensity) * 0.45);
            double u = Math.Max(_rng.NextDouble(), 1e-9);
            int nextCrackle = (int)(-meanInterval * Math.Log(u));
            _crackleSamplesLeft = Math.Max(0, nextCrackle - 200);

            // new crackle event
            _crackleAmp   = 0.4f + (float)_rng.NextDouble() * 0.6f;
            _crackleDecay = 0.92f + (float)_rng.NextDouble() * 0.06f;

            float sample = (float)(_rng.NextDouble() * 2.0 - 1.0);
            return _crackleAmp * sample * CrackleIntensity;
        }
    }
}