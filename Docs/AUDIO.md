# AUDIO.md — gameplay SFX and the burst policy

**Read this before adding a `PlayGameplaySFX` call site, before adding a
`GameplaySFXCategory`, or before touching `AudioSystem`'s dispatch.**

## 0. The law: no gameplay SFX category may stack on itself

> **Every gameplay one-shot passes through one per-category burst gate. A frame that
> produces N of the same event must never start N copies of the same FMOD event.**

This is not a tuning preference; it is a correctness rule about how identical sounds
combine. It is enforced structurally — see §3 — so a new call site inherits it with
nothing to wire.

## 1. Why — the two problems, and why only one of them is obvious

Gameplay SFX are fired **per event**, and several event classes are inherently bursty:

| burst source | what fires |
|---|---|
| a vessel sweeping a crystal shower | one `CrystalCollect` + one `Element*Received` **per crystal** |
| an AOE blast over a crystal field | `CrystalCollect` per crystal |
| a fauna die-off / mass-kill mode (Wildlife Liberation, Rampage) | every lifeform drops a heart, and every heart collected is another pair |
| a trail collapsing, mode entry, cell drain | `BlockDestroy` per prism |
| a swarm starving | `CreatureDeath` per creature |

Each one-shot is its own FMOD instance — `FMODOneShotVolumeHelper.PlaySFXOneShot` runs
`CreateInstance` → `setVolume` → `set3DAttributes` → `start` → `release`. So:

**The obvious problem — voices.** N events in a frame is N instances created, N voices
started, N virtualisation decisions. This is the frame-cost half.

**The non-obvious problem, and the one that actually sounds bad — coherent summation.**
The copies are *the same asset started within the same frame*, so they are correlated,
not independent. N identical correlated signals sum to **+20·log₁₀(N) dB**: ten crystals
is **+20 dB**, thirty is **+29.5 dB**. And the sub-millisecond offsets between them make
each pair a comb filter, so the stack is not merely loud, it is *metallic and phasey* —
the "it tried to combine far too many sounds" symptom.

**The consequence that matters for fixing it:** *attenuating each voice does not help.*
Turn every copy down and they still sum coherently and still comb-filter; you get a
quieter version of the same ugly sound. The phasing is a function of **simultaneity**,
not level. That is why the fix has to space voices apart in time, and why a
volume-scale-only mitigation (which is what `BlockDestroy` had before this policy) was
never going to be sufficient on its own.

## 2. The three levers

All three live per category in **`GameplaySFXPolicySO`**
(`Assets/_Scripts/ScriptableObjects/`, shared asset at
`Assets/Resources/GameplaySFXPolicy.asset`). This is the **only** tuning surface — per
CLAUDE.md config separation, none of it belongs on `AudioSystem` as serialized fields.

| lever | field | job |
|---|---|---|
| **decoherence** | `minRetriggerSeconds` | Minimum gap between two voices of a category. **This is the one that fixes the sound.** Identical one-shots only comb-filter when they start within a few ms; spacing them turns a burst into a legible rattle of distinct hits. |
| **budget** | `maxVoicesPerWindow` + `windowSeconds` | Hard ceiling on voices started per window. The CPU / FMOD-voice half. |
| **magnitude** | `maxPendingVoices` | Blocked events are *folded into pending aggregates* and replayed later — spaced out, quieter, at the centroid of the events they stand for — so a big burst still **sounds** big. `0` = drop outright (the older pure-throttle behaviour). |

Plus `volumeScale` (category baseline) and `burstVolumeFalloff` / `minBurstVolume`
(successive voices in a window decay toward a floor, so a sustained burst does not pile
up but also never fades to inaudible).

### What a burst actually resolves to

30 crystals destroyed on one frame, under the shipped `CrystalCollect` policy
(`minRetrigger 0.045`, `max 3 per 0.12s`, `pending 2`):

```
t=0.000   voice 1  (immediate — the leading edge is NEVER delayed)
t=0.046   voice 2  at the centroid of the suppressed crystals, ×0.6
t=0.092   voice 3  at the centroid of the rest,                ×0.36
          ————————————————————————————————————————
          3 voices, none within 45 ms of another  (was: 30 voices, all at t=0)
```

## 3. The four layers that make it un-authorable to skip

1. **The gate is in the dispatch, not at the call sites.** Every overload of
   `AudioSystem.PlayGameplaySFX` funnels through `PlayGameplaySFXInternal`, which calls
   `GameplaySFXBurstLimiter.TryAdmit` before it ever reaches FMOD. There is no
   "unthrottled" entry point for a gameplay category to opt into.
2. **The limiter is pure and fully tested.** `GameplaySFXBurstLimiter`
   (`_Scripts/System/Audio/`) takes `now` as a parameter and touches no Unity object, so
   the whole decision surface — spacing, budget, window roll, falloff floor, coalescing,
   centroid, per-category isolation — is covered by
   `_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs`. That matters because the bug
   only reproduces under a burst that is awkward to stage by hand in play mode.
3. **Unlisted categories stay permissive by construction.** `defaultPolicy` is
   `Unlimited()`, so a category nobody has identified as bursty behaves exactly as it did
   before the policy existed. Adding a category is safe; *forgetting* to add one costs
   nothing but the old behaviour.
4. **Duplicates fail loud.** A category listed twice in the asset logs a warning on first
   lookup (the later entry would silently never apply), and a test asserts the shipped
   table has none.

## 4. Call-site rules

- **Pass a world position whenever you have one.** `PlayGameplaySFX(category, worldPos)`
  gives FMOD 3D panning and attenuation, which decorrelates copies *before* the limiter
  ever sees them — a distant AI's crystal is quiet, a nearby one is loud. The 2D overload
  `PlayGameplaySFX(category)` has **zero** spatial decorrelation: every copy lands dead
  centre on the listener and sums perfectly. That is why the four `Element*Received`
  stingers — which are 2D by design, being reward stingers rather than physical events —
  carry the tightest limits in the table.
- **Do not add a bespoke throttle, cooldown, or "last played time" field at a call site.**
  That is the mistake this policy replaced (`AudioSystem` carried a hand-rolled
  `BlockDestroy`-only sliding window). Add a `GameplaySFXCategoryPolicy` entry instead.
- **Do not gate a category on a distance check to "reduce spam".** Distance is FMOD's job
  via the 3D attributes; a code-side distance gate re-implements attenuation badly and
  goes wrong the moment the listener moves.
- **Continuous sounds are not one-shots.** An engine hum, a drift loop, a proximity
  whine is a persistent `EventInstance` owned by a controller (`DriftAudioController`,
  `ProximityBoostAudioController`), not a repeated one-shot. If you find yourself firing
  a one-shot every frame, you want an instance with a parameter.

## 5. Adding a category

1. Add the enum member to `GameplaySFXCategory` (`AudioSystem.cs`) **with an explicit
   numeric value** — the project-wide rule against Unity serialization drift.
2. Add the `EventReference` field + its entry in `InitializeGameplaySFXEvents()`.
3. Ask: *can more than one of these happen in a single frame?* If yes, add a
   `GameplaySFXCategoryPolicy` entry to `GameplaySFXPolicySO`'s defaults **and** to
   `Assets/Resources/GameplaySFXPolicy.asset`. If no, do nothing — the permissive default
   applies.
4. Wire the FMOD event on the `AudioSystem` prefab (`_Prefabs/CORE/AudioSystem.prefab`).
   An unwired category logs once and is otherwise silent.

## 6. Volume routing (unchanged by this doc, recorded so it is not re-derived)

Every FMOD SFX event routes through the bus at `AudioSystem.sfxBusPath` (default
`bus:/`), whose volume and mute are driven from `GameSetting.SFXLevel` / `SFXEnabled`.
That is what makes the **whole** SFX bank obey the slider — continuous emitters included,
not just one-shots. `ResolveFMODSFXVolume()` therefore returns `1` when the bus resolved
(folding the slider in per-instance too would attenuate by slider²) and falls back to the
raw slider only when the bus did not resolve. The limiter's category volume multiplies on
top of that, not instead of it.

The legacy Unity `AudioSource` path (`PlaySFXClip`, the music sources, `masterMixer`) is
separate and is **not** governed by this policy — it has two remaining callers
(`CountdownTimer`, `ProfileModal`), neither of which can burst.

## 7. Files

| Role | File |
|---|---|
| Burst gate (pure, testable) | `_Scripts/System/Audio/GameplaySFXBurstLimiter.cs` |
| Per-category policy (the only tuning surface) | `_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs` |
| Shared policy asset | `Assets/Resources/GameplaySFXPolicy.asset` |
| Dispatch + per-frame drain pump | `_Scripts/System/Audio/AudioSystem.cs` |
| FMOD one-shot helper (per-instance volume) | `_Scripts/Controller/FX/FMODOneShotVolumeHelper.cs` |
| Tests | `_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs` |
| AudioSystem prefab (event wiring) | `_Prefabs/CORE/AudioSystem.prefab` |

## 8. In-editor verification

The limiter's logic is covered by edit-mode tests, so what needs the running editor is
the *feel*:

1. Play a mode with dense crystals (Wildlife Blitz, Crystal Capture) or a mass-kill mode
   (Wildlife Liberation, Rampage) and fly through a crystal shower.
2. Confirm the collect sound reads as a **rapid rattle of distinct hits** rather than one
   loud phased blast, and that the first hit is *immediate* (no perceptible latency on the
   leading edge — the limiter never delays it).
3. Watch the FMOD profiler's voice count during the burst; it should stay flat where it
   previously spiked.
4. If a burst still sounds stacked, the dial is `minRetriggerSeconds` (raise it), not
   `volumeScale` — see §1 for why attenuation alone cannot fix it.
