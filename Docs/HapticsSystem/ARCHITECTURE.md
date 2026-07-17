# Audio-Matched Haptics — Architecture

**Goal:** haptics that track the *audio experience* itself — every haptic in the game is either
measured from the actual sound's waveform or driven by real-time metering of the actual mix —
instead of five generic presets fired near sounds.

Status: **shipped** on `claude/audio-matched-haptics-0r6ib8`. Supersedes the rudimentary
approach on `claude/add-haptics-for-fx-sznU5` (32 hand-wired `HapticClip` inspector fields on
AudioSystem + preset fallbacks), which predated the FMOD migration and never covered the
continuous emitters.

## The two layers + the arbiter

```
                     ┌──────────────────────────────────────────────────────────┐
   AudioSystem       │              AudioHapticsOrchestrator                    │
   PlayGameplaySFX ──┤  TRANSIENTS: envelopes measured from each SFX's source   │
   PlayMenuAudio  ───┤  wav, fired at the same call that starts the FMOD one-   │
   PlaySFXClip ──────┤  shot (same category gain, listener-distance falloff)    │──► Lofelt
                     │                                                          │    NiceVibrations
   FMOD SFX bus ─────┤  BED: envelope follower on live bus metering (RMS +      │    (one channel)
   (bus:/, metered)  │  spectral centroid) — engine hum, drift, boost swells,   │──► iOS / Android
                     │  one-shot tails, all with zero per-emitter wiring        │    vibrator or
                     │                                                          │    gamepad rumble
                     │  ARBITER: one haptic channel — priorities, cooldowns,    │
                     │  preemption; the bed yields to transients and resumes    │
                     └──────────────────────────────────────────────────────────┘
```

1. **Transient layer (event-synchronized, waveform-measured).** Each `GameplaySFXCategory` /
   `MenuAudioCategory` has a `HapticTransientSpec`: an envelope of `HapticBreakpoint`s
   (time / amplitude / frequency / optional *emphasis* transient) plus mixing metadata
   (gain, priority, cooldown). The shipped defaults (`AudioHapticsBakedDefaults`, generated)
   were **measured from the FMOD Studio project's source wavs** (`Cosmic Shore/Assets/*.wav`)
   — a mine explosion's haptic *is* that explosion's envelope; the drift-release haptic *is*
   the "drift let go" waveform. Fired from inside `AudioSystem` at the exact call that starts
   the FMOD one-shot; spatialized events attenuate with listener distance (quadratic, config
   distances) just like the sound.

2. **Continuous bed (signal-measured).** `FmodSfxBusMeter` taps the SFX bus (`bus:/`) via
   **input (pre-fader) metering** on the channel-group head DSP (the same DSP FMOD's own
   debug overlay meters) plus one FFT DSP for `SPECTRAL_CENTROID`. Pre-fader is deliberate:
   the head DSP is the fader `AudioSystem.ApplySfxBus` drives with the SFX slider, so the
   haptic bed does not scale with the audio volume knob (haptics have their own setting) —
   yet a *muted* SFX setting still silences the bed naturally, because muted one-shots are
   never created and continuous emitters zero their instance volume. `HapticEnvelopeFollower`
   (fast attack, slow release, hysteresis gate, perceptual gamma, ceiling) turns bus RMS into
   the actuator level; the centroid steers haptic frequency (log-mapped 65 Hz–3.5 kHz → 0..1,
   slew-limited). Whatever the SFX mix does — speed-driven engine hum, drift layers, boost
   proximity swells, one-shot tails — the actuator follows. Music never contaminates the
   signal: it runs on the legacy Unity AudioSource path, outside the FMOD bus.

3. **Arbiter (`HapticTransientArbiter`).** The Lofelt runtime holds exactly ONE clip — every
   `Load()` evicts the playing one. The arbiter is therefore structural, not optional:
   min-strength floor, per-category cooldowns, a global inter-start interval (Load-thrash
   guard), priority preemption (higher always wins; equal priority needs comparable strength;
   anything may take a spent tail). The bed suspends while a transient plays and resumes
   afterwards — the transient delivers the crisp attack, the bed carries the measured tail.

## Platform strategies

| Platform | Bed drive | Transients |
|---|---|---|
| iOS (Core Haptics) | Looping constant clip + real-time `clipLevel` / `clipFrequencyShift` (both modulate live) | Full envelopes + emphasis transients |
| Android (amplitude control) | No real-time modulation → re-`Play()` a short constant clip every `androidChunkIntervalSeconds` with `clipLevel` applied per play (chunks overlap into continuity) | Full envelopes (no frequency, no native emphasis) |
| Gamepad (editor/desktop/console) | Same as iOS via `GamepadRumbler` motor-speed multiplication; clip can't loop → periodic re-play from due time | Envelopes resampled to 50 ms rumble entries; haptic frequency steers the low/high motor balance |
| Old devices (no advanced haptics) | Skipped | Degrades to a strength-matched preset via `fallbackPreset` |

## Key decisions (do not silently regress)

- **Haptics are independent of the SFX volume slider.** Sound-off players still get haptics
  (mobile convention); gating comes only from `GameSetting.HapticsEnabled` / `HapticsLevel`
  (level maps to Lofelt `outputLevel`). The bed naturally silences when SFX is muted (the
  metered signal is gone) — the transient layer is the guaranteed floor.
- **Local player only.** `HapticSpec.PlayIfManual`, `SkimmerScaleHapticWithDistanceByPrismSO`,
  `VesselOvertakeBySkimmerEffectSO`, and `ElementalBarsView` (via
  `SilhouetteController` → `HapticsAllowed`) all gate on `IVesselStatus.IsLocalUser`
  (which excludes AI). This re-lands the never-merged fix from
  `claude/fix-ui-haptics-mixing-2laRC` onto the refactored tree. World-broadcast events
  (`AstroLeagueBall` ClientRpcs) pass their world position instead and attenuate with distance.
- **`HapticController.PlayConstant` is now a bed pulse.** The old implementation loaded a fresh
  clip per call — per-frame callers (skimmer scaling) were evicting every one-shot haptic on
  the channel. `PulseContinuous` lifts the bed level instead (4 pulse slots, strongest wins,
  so a per-frame skim drive can't starve a concurrent debuff buzz): continuous by
  construction, and it mixes with (never fights) the audio-measured signal. Pulses actuate
  even with `bedEnabled` off, and on devices with no clip playback they degrade to the
  legacy constant pattern, rate-limited.
- **Battery/duty-cycle:** the bed hard-stops after `bedIdleStopSeconds` under the gate and
  restarts on signal; muted/disabled haptics do zero per-frame work
  (`CosmicShore.AudioHaptics.Update` profiler marker guards the whole pass).

## Config & tooling

- `AudioHapticsConfigSO` — single source of truth (bed tuning, arbitration, per-category
  envelopes, spatial distances, Android pacing). Optional asset at
  `Assets/Resources/AudioHapticsConfig.asset`; without it, `AudioHapticsBakedDefaults`
  covers **every** category (enforced by tests) — the system is zero-wire.
- `Tools > Cosmic Shore > Audio Haptics >`
  - **Create Config Asset** — authors the asset pre-filled with the measured defaults.
  - **Bake Envelopes From FMOD Source Audio** — re-runs `HapticWaveformAnalyzer` over the
    FMOD Studio project wavs (chunk-walking WAV reader; PCM 16/24/32 + float) and refreshes
    each spec's envelope. Which wav feeds which category = the spec's `bakedFrom` field.
  - **Log Status** — health dump.
- Legacy `AudioSystem.PlaySFXClip(AudioClip)` callers (CountdownTimer, ProfileModal, Crystal)
  get matched haptics automatically: the clip's waveform is analyzed at first play and cached.

## File map

| Role | File |
|---|---|
| Orchestrator (channel owner, bed, transients) | `_Scripts/System/Audio/AudioHapticsOrchestrator.cs` |
| FMOD bus metering + FFT centroid | `_Scripts/System/Audio/FmodSfxBusMeter.cs` |
| Measured per-category defaults (generated) | `_Scripts/System/Audio/AudioHapticsBakedDefaults.cs` |
| Config SO (all tunables) | `_Scripts/ScriptableObjects/AudioHapticsConfigSO.cs` |
| .haptic JSON + gamepad rumble rendering | `_Scripts/Utility/Audio/HapticPatternBuilder.cs` |
| Waveform → envelope analysis | `_Scripts/Utility/Audio/HapticWaveformAnalyzer.cs` |
| Bus-level follower (attack/release/gate) | `_Scripts/Utility/Audio/HapticEnvelopeFollower.cs` |
| Channel admission control | `_Scripts/Utility/Audio/HapticTransientArbiter.cs` |
| Gameplay facade (`PlayHaptic`, `PlayConstant`) | `_Scripts/Controller/IO/HapticController.cs` |
| Editor baker + menu | `_Scripts/Editor/AudioHapticsBaker.cs` |
| Edit-mode tests (5 fixtures) | `_Scripts/Tests/EditMode/Haptic*Tests.cs`, `AudioHapticsBakedDefaultsTests.cs` |

## Verification

- Edit-mode: run the `CosmicShore.Tests` haptic fixtures (pattern format, follower DSP,
  arbitration policy, analyzer on synthetic signals, full enum coverage of baked defaults).
- In-editor feel pass: connect a rumble gamepad (the only editor output — mobile vibration
  requires a device build), enter play mode, fly: engine bed should swell with speed, prism
  breaks tick, crystal pickup snaps, mine explosions preempt everything.
- On device: `Tools > Cosmic Shore > Audio Haptics > Log Status` for the chain health;
  `CosmicShore.AudioHaptics.Update` in the profiler (expected ≪ 0.1 ms — two DSP float reads
  + a few comparisons).
