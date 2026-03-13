// iOS platform audio output — uses AVAudioEngine with a PCM tap.
// Compiled only on iOS/MacCatalyst (condition in .csproj).

#if IOS || MACCATALYST
using System;
using AVFoundation;
using AudioUnit;
using Foundation;
using WhiteNoise.Audio;

namespace WhiteNoise.Platforms.iOS
{
    public class IosAudioOutput : IPlatformAudioOutput
    {
        private AVAudioEngine?                _engine;
        private AVAudioPlayerNode?            _playerNode;
        private AVAudioPcmBuffer?             _ringBuffer;
        private Func<float[], int, bool>?     _callback;
        private System.Threading.Thread?      _fillThread;
        private volatile bool                 _running;

        private int _sampleRate;
        private int _channels;
        private int _bufferFrames;

        public void Initialize(int sampleRate, int channels, int bufferFrames)
        {
            _sampleRate   = sampleRate;
            _channels     = channels;
            _bufferFrames = bufferFrames;

            // Configure audio session for background audio playback
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback,
                AVAudioSessionCategoryOptions.MixWithOthers, out _);
            session.SetActive(true, out _);

            _engine     = new AVAudioEngine();
            _playerNode = new AVAudioPlayerNode();
            _engine.AttachNode(_playerNode);

            var format = new AVAudioFormat(
                AVAudioCommonFormat.PCMFloat32,
                (double)sampleRate,
                (uint)channels,
                interleaved: false)!;

            _engine.Connect(_playerNode, _engine.MainMixerNode, format);
            _engine.Prepare();
            _engine.StartAndReturnError(out _);
        }

        public void Start(Func<float[], int, bool> fillBufferCallback)
        {
            _callback = fillBufferCallback;
            _running  = true;
            _playerNode!.Play();

            var format = new AVAudioFormat(
                AVAudioCommonFormat.PCMFloat32,
                (double)_sampleRate,
                (uint)_channels,
                interleaved: false)!;

            _fillThread = new System.Threading.Thread(() =>
            {
                var floatBuf = new float[_bufferFrames * _channels];

                while (_running)
                {
                    bool cont = _callback(floatBuf, _bufferFrames);

                    var pcmBuffer = new AVAudioPcmBuffer(format, (uint)_bufferFrames)!;
                    pcmBuffer.FrameLength = (uint)_bufferFrames;

                    // Deinterleave into AVAudioPcmBuffer channel data
                    unsafe
                    {
                        for (int ch = 0; ch < _channels; ch++)
                        {
                            float* channelData = (float*)pcmBuffer.FloatChannelData![ch];
                            for (int f = 0; f < _bufferFrames; f++)
                                channelData[f] = floatBuf[f * _channels + ch];
                        }
                    }

                    _playerNode.ScheduleBuffer(pcmBuffer, null);

                    if (!cont) { _running = false; break; }

                    // Throttle: sleep ~80% of buffer duration
                    int sleepMs = (int)(_bufferFrames * 800.0 / _sampleRate);
                    System.Threading.Thread.Sleep(Math.Max(1, sleepMs));
                }
            })
            { IsBackground = true, Name = "AudioFillThread" };

            _fillThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _fillThread?.Join(500);
            _playerNode?.Stop();
            _engine?.Stop();
        }

        public void Dispose()
        {
            Stop();
            _playerNode?.Dispose();
            _engine?.Dispose();
        }
    }
}
#endif
