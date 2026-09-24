# Theater mode

A Halo 3-style match recorder for development: it saves a full game and lets you fly a camera
around it afterwards. Not video — a re-creation of what happened, from any angle, as many times
as you like.

**P0 is shipped and is vessel-only.** Press **9** to start and stop a recording, **8** to watch
the most recent one back. Everything else is a later phase (§6).

---

## 1. Outcomes, never inputs

Halo 3's theater stored controller input and re-ran the simulation. That needs a simulation that
produces the same output twice, and this one does not — deliberately, in most cases:

| Blocker | Where |
|---|---|
| Flight integrates in `Update`, so it is frame-rate dependent | `VesselTransformer.Update` → `MoveShip` |
| Flora and fauna roll on each peer's own `UnityEngine.Random` | `CellNetworkSync`, both life spawners |
| `Mathf.Sin` / `Acos` are not bit-identical across Mono and IL2CPP | Switchback already refuses to ship a course seed for this reason |
| Remote vessels arrive as replicated transforms, never simulated here | Netcode |
| PhysX is not guaranteed reproducible | contacts, depenetration, sleeping |

Recording where things **were** needs none of that to be true. So none of the five has to be
fixed, and none of them can break a recording later. This is the decision the whole design rests
on; do not revisit it by trying to make the simulation deterministic.

## 2. Why it is cheap here

**Mass is conserved.** No decay, no lifespan, no timed culler — a prism exists from an explicit
lay until an explicit active force removes it. The object class that outnumbers everything else by
two orders of magnitude therefore has *no continuous state to sample*: it is already a birth-and-
death event log, and four hook sites cover it (`Prism.Initialize`, `ReturnToPool`, `Damage`,
`Consume`).

**The clock-material law.** Every prism visual is `f(clock, one stamp)` with zero per-frame CPU.
Rebase the clock on playback and grow-ins, shield morphs, erosion wipes and death animations
reproduce *exactly*. Nothing about prism animation has to be recorded.

**The environment is a reference.** Cell config + intensity + seed is about a hundred bytes,
because the generators are closed-form and deterministic on one machine.

## 3. What P0 does

| | |
|---|---|
| Records | Every live vessel's position, rotation and speed at `vesselSampleHz` (default 30) |
| Roster from | `VesselVisionShading.CollectStampedVessels` — a list a PLATFORM LAW keeps correct, so no DI, no scene wiring, no `GameDataSO` |
| Timebase | `Time.time`, which is exactly what `PrismClock.Now` reads, so P1's prism events already share one clock |
| Plays back as | Domain-coloured ghost hulls on the shared manually-driven replay camera |
| Shots | Orbit (frames everything, auto-rotating) and Follow (behind one pilot, distance in units of that hull's own radius) |
| Transport | Play/pause, ×0.1 to ×8, ±5 s snap, cycle pilot — on screen, no extra key bindings |
| Lands in | `<repo>/Recordings/<scene>_<date>_<time>.cstheater` — already git-ignored, never pushed |

**Measured size: 36 bytes per pose.** Four pilots at 30 Hz for five minutes is **1.24 MB**, and
that is uncompressed on purpose — a recording you can read in a hex editor is worth more right
now than one that is four times smaller. Delta-coding positions and packing rotations
smallest-three takes a pose to roughly 10 B; that belongs in the phase that needs it.

### The puppet decision

A puppet is **harvested from the prefab asset, never instantiated** (`VesselModelBuilder`, which
reads meshes without ever waking the prefab). Three things fall out at once: no gameplay
controller can fly a puppet off its recorded pose, no `Awake` side effect fires, and — the one
that matters — **there is no `NetworkObject` to neutralise**. Instantiating a vessel prefab
without spawning it is the B16 trap, where Netcode adopts the stray as an in-scene placed object
and the *second* one breaks synchronisation for the rest of the session. Harvesting sidesteps it
by construction rather than by remembering a guard.

Stated cost: a P0 puppet has no jets, no tail, no hull morph and no animation. It is a ghost.

`ToyModelBuilder.NormalizeToRadius` gained one line for this — a target radius of zero or less now
means **native scale and native pivot**. A toy wants a model normalised into a station of a known
size; a puppet retracing a recorded flight needs the opposite, because the recorded pose is
relative to the ship's *pivot* and re-centring on the hull's bounds would slide the whole replay
off by that offset. A non-positive radius previously yielded `scale = 0`, an invisible model,
which was never what any caller wanted.

### Platform laws it inherits, with nothing to add

`CameraManager.BeginManualReplayCamera` already holds the prism-occlusion-corridor and speed-tunnel
suppressions — it is one of only two sanctioned holders. The vessel vision band deliberately has no
suppression, so a theater camera marks every hull, which is what a broadcast view wants. **Do not
add a second live camera**: `Camera.main`, the speed tunnel and the graphics settings all key off
the one rig.

## 4. Seeking, and the part that will get harder

**P0 snaps to any timestamp perfectly, forward or backward, and that is proven rather than
claimed.** A vessel track is *sampled state*, so any time resolves by binary search with nothing
to rebuild; the offline harness asserts that a backward walk is bit-identical to a forward one.

That will not stay free. **Prisms are events, and events have history.** From P1 a backward seek
becomes: load the nearest liveness snapshot, rebuild through the existing `LayBudgetedAsync`
behind `EnvironmentLoadVeil`, replay forward. A snapshot is one bit per event id plus a cursor —
about **9 KB at 70k prisms**, against ~1.1 MB for a full state dump, which is what makes it
affordable to write them often.

Smooth backward *scrubbing* is explicitly out of scope, by decision. Snapping is not a lesser
version of it; it is a different and much cheaper mechanism, and it removes the hardest constraint
on the clock and on every subsystem that plays an animation forward.

## 5. Audio

**Yes — and snapping backward is the easy case for audio, not the hard one.** Nobody expects sound
to be continuous across a time jump: you stop every loop, restore the ones the snapshot says were
playing, and carry on. A one-shot before the new position simply never fires.

The hook surface is far smaller than the call-site count suggests, because CLAUDE.md already
requires every sound to be an `EventReference` played through one of two places:

| Kind | Where it is played | Call sites | How it records |
|---|---|---|---|
| **One-shots** | `AudioSystem.PlayGameplaySFX` / `PlaySFXEvent` (+ overloads) | 50, through **5 methods on one class** | An event: GUID + world position + time, ~24 B. Re-fire on playback. |
| **One-shots, direct** | `FMODOneShotVolumeHelper` | 2 methods (one real caller, the Urchin's spike) | Same. |
| **Loops** | `StudioEventEmitter` (engine, drift, proximity boost, flora ambient) | 8 | Start/stop events + a parameter sample at 5–10 Hz. |
| **Music** | `AudioSystem` music instance | 1 | One event + start time; FMOD's `setTimelinePosition` makes a seek exact. |

So a complete audio recorder is roughly **seven methods and one component**, not a sweep of the
codebase — and the standing rule that forbids `RuntimeManager.PlayOneShot` in first-party code is
what keeps it that way. Cost is small: a busy match is maybe 20–40 one-shots a second, so about
1 KB/s, ~0.3 MB over five minutes raw. Loop parameters at ~12 live instances add ~0.5 KB/s.

Four things are genuinely awkward, and none is a blocker:

1. **Spatialisation changes.** FMOD's listener in a theater is the free camera, not a vessel, so
   the mix is what the *camera* hears rather than what the recording player heard. For a director
   tool that is arguably the right answer, but it means "reproduce what I heard" is not on offer.
2. **Parameter capture is per instance.** Most loops carry one to three parameters, so it is cheap,
   but every parameter has to be enumerated — there is no "record all" on an FMOD instance.
3. **Event GUIDs must stay stable.** An FMOD project that renames an event breaks old recordings.
   Same versioning problem as everything else here; a loud failure is acceptable.
4. **Volume must route through `AudioSystem.ResolveSfxInstanceVolume`**, or playback ignores the
   SFX slider — the defect `Docs/AudioSystem/FMOD_AUDIT.md` already records.

It is not implemented. Decision: audio was descoped for now; this section exists so the answer is
"yes, here is the shape and the cost" rather than a fresh investigation later.

## 6. Phases

| | | Status |
|---|---|---|
| **P0** | Vessel ghost recorder. Free camera over ghost ships. | **shipped** |
| **P1** | The world comes back: environment by reference + the four `Prism` hook sites. | next |
| **P2** | Everything else that moves: fauna, crystals, projectiles, blasts, balls. Per-subsystem toggles matter here. | |
| **P3** | Transport: snap to a timestamp via liveness snapshots; chapter markers off the annotation stream. | |
| **P4** | Director: point the screenshot director's fifteen capture concepts at recorded time instead of live time. | the payoff |

P4 is the phase with the most value for this team and is an acceptable place to stop. The
screenshot director already solves framing, occlusion, marking and clearance; today it can only
shoot the present. Pointed at a recording, the same concepts shoot a moment you already know is
worth shooting — the difference between hoping for a good capture and taking one.

### Toggles

`TheaterConfigSO` (`Resources/TheaterConfig`, optional — the defaults work with nothing authored)
carries one flag per subsystem. The ones for later phases are listed now rather than hidden,
because which subsystem costs what is the decision the config exists to expose:

| Toggle | Share of a fauna-heavy 5-minute recording |
|---|---|
| `recordFauna` | **80–90%** — the one toggle that changes a recording's size *category* |
| `recordPrisms` | ~4% (events, not samples) |
| `recordProjectiles` | ~3% (launches only; sampling rounds is 5× dearer and no more faithful) |
| `recordVessels` | ~2% |
| `recordAnnotations` | ~1% |

## 7. Verification status

- The **format** is compiled and **run** offline against the shipped `TheaterRecording.cs`
  (Roslyn + a Unity stub): 15 checks pass, covering round-trip fidelity, refusal of foreign and
  truncated bytes, forward compatibility (an unknown stream is skipped, not fatal), interpolation,
  absence outside a track's span, the backward-seek claim, and the 36 B/pose constant measured
  against the writer rather than believed.
- `TheaterRecordingTests` covers the same ground in the edit-mode suite.
- The five standing out-of-editor gates pass. The three Unity-dependent files parse with **zero
  syntax-class errors**.
- **Not opened in Unity.** The recorder, the playback, the puppet build and the camera handover
  have not been run. A human needs to: press 9 in a match, press 9 again, confirm a file appears in
  `Recordings/`, press 8, and confirm ghosts move and the camera orbits.
