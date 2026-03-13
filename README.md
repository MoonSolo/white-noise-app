# White Noise — .NET MAUI App

A fully customisable white noise generator for Android, iOS, and Windows.  
All audio is **synthesised in real-time** — no audio assets required.

---

## Features

| Feature | Details |
|---|---|
| **White noise** | PCM float synthesis at 44 100 Hz stereo |
| **Patterns / LFO** | Constant · Ocean Wave · Rain · Pulse · Breathing |
| **Firewood crackle** | Procedural Poisson-impulse synthesis |
| **Duration** | 0 (infinite) → 120 minutes |
| **Fade out** | Cosine envelope, 5 s → 120 s |
| **Volume** | 0 – 100% |
| **Cross-platform** | Android · iOS / macCatalyst · Windows |

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Visual Studio 2022 | 17.8+ | Community edition is free |
| .NET SDK | 8.0 | Included with VS 2022 |
| MAUI workload | — | `dotnet workload install maui` |
| Android SDK | API 21+ | Via VS Android SDK manager |
| Xcode | 15+ | **Mac only**, required for iOS |
| Windows SDK | 10.0.19041 | Included with VS |

---

## Getting Started

### 1. Clone / open

```bash
git clone https://github.com/you/whitenoise.git
cd whitenoise
```

Open `WhiteNoise.sln` in Visual Studio 2022.

### 2. Restore packages

```bash
dotnet restore
```

NAudio is automatically included only for the Windows TFM.

### 3. Run

Select your target in the VS toolbar:

- **Windows Machine** — runs immediately, no device needed
- **Android Emulator** — create one via Tools → Android → Device Manager
- **iOS Simulator** — requires macOS + Xcode
- **Physical device** — plug in, trust the computer, select from dropdown

```bash
# Or from CLI:
dotnet build -t:Run -f net8.0-windows10.0.19041.0
dotnet build -t:Run -f net8.0-android
```

---

## Project Structure

```
WhiteNoise/
├── Audio/
│   ├── AudioEngine.cs          ← DSP core: noise, LFO, crackle, fade
│   └── IPlatformAudioOutput.cs ← platform abstraction interface
├── Platforms/
│   ├── Windows/
│   │   └── WindowsAudioOutput.cs   ← NAudio WaveOutEvent
│   ├── Android/
│   │   └── AndroidAudioOutput.cs   ← AudioTrack (PCM float)
│   └── iOS/
│       └── IosAudioOutput.cs       ← AVAudioEngine + PlayerNode
├── Services/
│   └── AudioService.cs         ← DI-friendly wrapper
├── ViewModels/
│   └── AudioViewModel.cs       ← MVVM, INotifyPropertyChanged
├── Views/
│   ├── MainPage.xaml           ← UI layout
│   └── MainPage.xaml.cs
├── App.xaml / App.xaml.cs
├── MauiProgram.cs              ← DI container setup
└── WhiteNoise.csproj
```

---

## Audio Architecture

```
MAUI UI (sliders, toggles)
        │  binds via ViewModel
        ▼
AudioViewModel  ──────────────────────────────
        │  calls                              │
        ▼                                     │
AudioService (singleton)                      │
        │  owns                               │
        ▼                                     │
AudioEngine (DSP)                             │
  ├── NoiseGenerator  (white noise buffer)    │
  ├── LfoModulator    (5 waveform shapes)     │
  ├── CrackleGenerator (Poisson impulse DSP)  │
  └── FadeEnvelope    (cosine ramp)           │
        │  fills float[] buffer               │
        ▼                                     │
IPlatformAudioOutput  ◄────────────────────────
  ├── WindowsAudioOutput  (NAudio)
  ├── AndroidAudioOutput  (AudioTrack)
  └── IosAudioOutput      (AVAudioEngine)
```

---

## DSP Details

### White noise
`Random.NextDouble()` mapped to `[-1, 1]` — spectrally flat (Gaussian would be
slightly more natural; swap `NextDouble()` for a Box-Muller transform if preferred).

### LFO patterns
All patterns output a gain multiplier in roughly `[0.2, 1.0]`:

| Pattern | Formula |
|---|---|
| Constant | `1.0` |
| Wave | `0.6 + 0.4 · sin(2π · 0.15 · t)` |
| Rain | Product of two sines at 3.7 Hz and 0.4 Hz + noise jitter |
| Pulse | `0.5 + 0.5 · tanh(4 · sin(2π · 0.5 · t))` |
| Breathing | Asymmetric triangle: 35% rise / 65% fall over 5.5 s |

### Crackle synthesis
Uses a **Poisson process** to trigger impulse events:
- Inter-arrival time: `–ln(U) / rate` where `rate = 1 + intensity × 15`
- Each event decays exponentially with a random amplitude and speed
- No audio files involved — fully procedural

### Fade envelope
Cosine fade applied over the last N seconds of playback:
`gain = 0.5 + 0.5 · cos(π · t)` where `t ∈ [0, 1]`

---

## Extending the App

### Adding a new pattern
1. Add a value to `NoisePattern` enum in `AudioEngine.cs`
2. Add a `Lfo*` method and wire it in `ComputeLfo()`
3. Add a `PatternItem` in `AudioViewModel.cs`

### Adding a new sound layer (e.g. rain drops, ocean)
1. Add a `ComputeRainLayer(double dt)` method to `AudioEngine.cs`
2. Add a public `bool RainEnabled` property
3. Mix into the sample: `sample += RainEnabled ? ComputeRainLayer(dt) : 0f;`
4. Add a toggle in `MainPage.xaml`

### Background audio (Android)
Create a `ForegroundService` that holds the `AudioService` reference.  
See: https://learn.microsoft.com/en-us/dotnet/maui/android/services/foreground-services

### Background audio (iOS)
The `AVAudioSession` category is already set to `.Playback`.  
Enable "Audio, AirPlay, and Picture in Picture" background mode in `Info.plist`.

---

## Troubleshooting

| Issue | Fix |
|---|---|
| NAudio missing on Windows | `dotnet add package NAudio` |
| No sound on Android emulator | Enable audio in emulator settings |
| iOS build fails | Ensure Xcode is installed and provisioning profile is set |
| `AllowUnsafeBlocks` error | Already set in `.csproj` — clean & rebuild |

---

## Licence

MIT — do whatever you want.
