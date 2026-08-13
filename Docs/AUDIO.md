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
| a trail collapsing, mode entry, cell drain | `BlockDestroy` per prism — rewritten to `CreatureBlockHit` when a creature caused the kill (`Prism.PlayDestructionSFX`) and to `FloraCollision` for flora prisms (`HealthPrism`), so the burst arrives under *three* category names |
| a swarm starving | `CreatureDeath` per creature |
| an AOE blast shielding a neighbourhood | `ShieldActivate` per prism, **2D** |
| those shields expiring | `ShieldDeactivate` per prism, 2D — and **synchronized by construction**: `PrismTimerManager` drains every expired shield timer in one `Update`, so prisms shielded together expire together |

Worst case measured: **Wildlife Liberation**, ~1,409 concurrently live creatures, each carrying
an embedded elemental heart. Death is silent, but `Fauna.ReleaseHeart` → `Crystal.ActivateCrystal`
re-enables each heart's collider and leaves it as a free collectible; mass is conserved and
nothing culls them, so a cleared cage becomes a dense field of loose crystals for a vessel to
sweep. The Wanderway belt is second at ~120 live crystals.

**Cleared as non-sources** (checked, so nobody re-checks them): AOE/explosion paths do not touch
crystals at all (`ExplosionImpactor` / `AOEExplosion` / `PrismSpatialIndex` contain no crystal
handling, and neither `CrystalImpactor` subclass accepts a projectile or explosion impactor);
lifeform death itself (`ActivateCrystal` is silent); cell drain and `RequestCellSwap` (destroy
crystals with the root, silently); turn-end and shape-drawing teardown (`DestroyCrystal`, silent);
the spent-crystal husk pool (no audio).

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
| **magnitude** | `maxPendingVoices` + `burstMagnitudeGain` | Blocked events are *folded into pending aggregates* and replayed later — spaced out, at the centroid of the events they stand for, and **scaled up by how many that is** (`1 + gain·log2(represents)`, clamped). `maxPendingVoices = 0` = drop outright (the older pure-throttle behaviour). |

Plus `volumeScale` (category baseline) and `burstVolumeFalloff` / `minBurstVolume`
(successive voices in a window decay toward a floor, so a sustained burst does not pile
up but also never fades to inaudible).

**The magnitude lever is not garnish — it replaces something the fix removes.** Before the
limiter, a big burst was loud *precisely because its voices stacked*, which is the defect. That is
why `BlockDestroy` was attenuated to `0.35` in the first place ("dozens of prisms can break in a
single frame"). Kill the stacking and leave the attenuation alone and you have simply made the
Dolphin's blast quiet. So loudness is put back deliberately and logarithmically, on the one voice
that speaks for the crowd, instead of accidentally and coherently across N voices.

### What a burst actually resolves to

The reference case is a **Dolphin AOE blast**. `PrismSpatialIndex.MAX_NEW_HITS_PER_FRAME = 48`, so
a blast destroys up to 48 prisms *per frame* and backlogs the rest to later frames — every one
calling `Prism.PlayDestructionSFX` → `BlockDestroy`. Simulated against the shipped policy:

| prisms | voices | worst same-frame | loudest voice | total energy |
|---|---|---|---|---|
| **ungoverned** 48 | 48 | **48 (+33.6 dB)** | 1.00 | 48.00 |
| **old throttle** 48 | 4 | **4 (+12.0 dB)** | 0.35 | 1.40 |
| **old throttle** 300 | 4 | 4 (+12.0 dB) | 0.35 | 1.40 |
| shipped 48 | 3 | **1 (+0.0 dB)** | 0.67 | 1.54 |
| shipped 300 | 5 | 1 (+0.0 dB) | 0.84 | 2.83 |
| shipped 2000 | 21 | 1 (+0.0 dB) | 0.84 | 13.51 |

Three things to read off it:

1. **The old throttle capped the count and did nothing about simultaneity.** Its 4 admitted voices
   could all start on the same frame, so it still stacked at +12 dB and still combed. A voice
   budget alone is not a fix — this is why the Dolphin still sounded wrong.
2. **Worst same-frame is now 1, always.** Coherent summation is not reduced, it is *eliminated*:
   there is no frame on which two voices of one category can start.
3. **The old throttle made a 48-prism blast and a 300-prism blast sound identical** (1.40 energy
   for both). Now they scale — that is the magnitude lever doing its job.

Suppressed events are never charged an FMOD instance, so frame cost falls with the voice count:
**93.8 % / 98.3 % / 99.0 %** of `CreateInstance`/`start`/`release` calls avoided at 48 / 300 / 2000
prisms.

### Two ordering rules inside the limiter that look like details and are not

Both were found by simulating a sustained blast, and both are silent when wrong:

- **A pending backlog outranks a fresh arrival.** A sustained burst delivers new events before
  every `Drain`, so letting the newest one take the voice slot starves the queue for the whole
  blast: every voice then speaks for exactly one prism, none ever carries the crowd, and the
  aggregates grow until the blast ends and release as a late thump. An isolated event still plays
  instantly, because nothing is pending in that case.
- **Overflow folds into the *lightest* aggregate, not the last.** `Drain` pops FIFO, so appending
  to the last aggregate parks the whole crowd behind a single-event aggregate and the first replay
  carries no magnitude at all.

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

## 3a. One event, one emitter — the limiter is not a licence to double-fire

**Unity raises `OnTriggerEnter` on *both* colliders of a contact pair.** If both sides own an
impactor with its own accept path and its own latch, neither latch can see the other and a single
physical event fires the same one-shot **twice**, in the same frame, at the same position.

That is exactly what an omni-crystal pickup did: `VesselImpactor.AcceptImpactee` (the
`case OmniCrystalImpactor` branch) fired `CrystalCollect`, and `OmniCrystalImpactor.AcceptImpactee`
→ `ExecuteEffect` → `Crystal.Explode` → `PlayExplosionAudio` fired it again. `Crystal`'s copy was
removed: `VesselImpactor` has no owner/network gate and no vessel-type exclusion, so it fires on
every peer for every vessel, whereas the crystal-side path early-returns on network clients and
skips `Explode` entirely for the Manta — it could only ever add a duplicate, never be the only
voice.

**Why the limiter does not excuse this.** A duplicate is not "one extra voice it will throttle
away" — the limiter *holds it back and replays it ~45 ms later as a soft echo*, on every single
pickup, turning a constant redundancy into a permanent audible artifact in the common one-at-a-time
case. Before adding a `PlayGameplaySFX` call to an impactor, check whether the other side of the
contact already plays it.

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
