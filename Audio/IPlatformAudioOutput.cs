using System;

namespace WhiteNoise.Audio
{
    /// <summary>
    /// Platform-specific audio output abstraction.
    /// Implement once per target (Windows/iOS/Android).
    /// </summary>
    public interface IPlatformAudioOutput : IDisposable
    {
        /// <summary>
        /// Initialise the audio device.
        /// </summary>
        void Initialize(int sampleRate, int channels, int bufferFrames);

        /// <summary>
        /// Begin streaming. The callback is called repeatedly to fill buffers.
        /// The callback returns false to signal end of playback.
        /// </summary>
        void Start(Func<float[], int, bool> fillBufferCallback);

        /// <summary>
        /// Stop streaming and release the device.
        /// </summary>
        void Stop();
    }
}
