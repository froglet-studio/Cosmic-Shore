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
| Plays back as | Declawed real vessels — hull, **jets and tail** — inside the **recording area** (§3.1) |
| Shots | **Free** (detached), plus **Pilot** / **Orbit** / **Chase**, all three steerable — §3.2 |
| Transport | Play/pause, ×0.1 to ×8, ±5 s snap, cycle shot, cycle pilot — on screen and on the pad |
| Lands in | `<repo>/Recordings/<scene>_<date>_<time>.cstheater` — already git-ignored, never pushed |

**Measured size: 36 bytes per pose.** Four pilots at 30 Hz for five minutes is **1.24 MB**, and
that is uncompressed on purpose — a recording you can read in a hex editor is worth more right
now than one that is four times smaller. Delta-coding positions and packing rotations
smallest-three takes a pose to roughly 10 B; that belongs in the phase that needs it.

### The ghost decision

A ghost is **a real vessel with everything that ACTS removed**, so what is left can only draw:
hull, jets and tail, flying a recorded path and touching nothing.

It began as harvested meshes read off the prefab asset, which was safe and silent and **could never
have a jet** — a jet is a particle system and a tail is a `TrailRenderer`, and neither survives
being copied as geometry. Getting them means instantiating the real prefab, which carries two
hazards worth naming separately, because only one of them is the one people expect.

**Hazard 1 — the stray `NetworkObject`, and being offline does not remove it.** Netcode scans
loaded scenes for un-spawned `NetworkObject`s and adopts each as an in-scene *placed* object, keyed
on a hash every instance of one prefab shares — so the **second** stray throws inside
`PopulateScenePlacedObjects` and breaks synchronisation for every later joiner, on this machine,
silently (`Docs/PartySystem/BUGS.md` B16). The theater sends and receives nothing, but there is
always a NetworkManager running here: the project hosts even in Menu_Main, and offline mode is
itself a `127.0.0.1` local host. *Local does not mean there is no NetworkManager to confuse.*

**Hazard 2 — the one that would actually corrupt a match.** A vessel prefab carries
`VesselPrismController`. An un-declawed ghost retracing a flight would lay **real trail prisms into
the live cell**: conserved mass injected into a running simulation by something that is supposed to
be a picture.

Both are answered the same way, and the shape is the point:

- **Nothing ever wakes.** The prefab is instantiated under a **deactivated holder**, so `Awake` is
  deferred; the strip runs while the instance is inert; only the survivors are ever activated. That
  is also why the strip uses `DestroyImmediate` — a deferred `Destroy` lands at end of frame, which
  is *after* the activation that would have run every stripped component's `Awake`.
- **The strip is a whitelist of what may DRAW**, not a blacklist of what to remove. A blacklist
  forgets the next component somebody adds to a vessel, and that failure is silent and cumulative.
  The list is renderers, particle systems, trails, lights, the `Animator`, and the three tail/jet
  markers — whose own `Awake`s do nothing but size the FX, which is what a ghost wants.
- **The strip repeats until it stops making progress**, because `RequireComponent` refuses a
  destruction whose dependent is still present and the dependency order among a vessel's own
  scripts is not knowable from here. Anything still standing is **named**: a component that cannot
  be removed is one whose `Awake` is about to run on a ghost, which is the whole thing this
  prevents, so it must not fail quietly.

**Jets swell with the recorded speed, normalised against that pilot's own fastest moment.** Self
-calibrating on purpose: a plume sized against an authored cruise speed needs a number per hull,
and the fleet's straight-line speeds span 34×. Against its own top speed a ghost reads right with
nothing authored.

**Trails are cleared on every seek, every loop, and every time a ghost reappears.** A
`TrailRenderer`'s points are in world space, so a jump otherwise draws one straight ribbon from
where the ghost was to where it now is — the same trap, and the same fix, as a pooled missile's
tail.

**The hull radius is measured from MESHES only.** Every shot's framing is in multiples of it, and a
trail is hundreds of units long the moment it starts drawing — measuring it would hand the camera a
"hull radius" that grows as the ship flies and pull every shot steadily away from it.

Stated cost: a ghost has no **hull morph** (that needs `VesselAnimation`, which the strip removes)
and its `Animator` plays its default state with nothing driving its parameters. `fullGhosts: false`
falls back to the harvested mesh, which instantiates nothing.

`ToyModelBuilder.NormalizeToRadius` still carries the one line the harvest needed — a target radius
of zero or less means **native scale and native pivot**, because a recorded pose is relative to the
ship's *pivot* and re-centring on its bounds would slide the whole replay off by that offset.

### 3.1 The recording area

**The world stays. Only the interface goes.** The first cut masked the live world off the camera
onto a private layer, on the reasoning that a theater wants a clean void — and a void is exactly
what it produced: no prisms, no environment, no crystals, nothing but ghosts in the dark. A Halo
theater shows you the *map*. So the mask is opt-in now (`TheaterConfigSO.hideWorld`, default off,
for "show me this flight and nothing else"), and the stage's real job is the three things that
genuinely have to stop while somebody watches a replay.

1. **The gameplay UI is hidden, and that is a BUG FIX before it is a look.** IMGUI and uGUI process
   the same mouse event independently and neither can consume it for the other, so every theater
   button sitting over a live uGUI control pressed **both** — which is why the two rightmost
   buttons, parked over the HUD's own top-right Volume/Pause button, kicked the player back to the
   menu. Root canvases are switched off and the EventSystem is stood down; world-space canvases are
   left alone, because they are part of the scene rather than part of the interface.
2. **The local pilot's input is paused** (`IsLocalPilot`, never `IsLocalUser` — the legacy
   single-player spawn path never network-spawns its Player), so the theater and the vessel are not
   both reading the same sticks.
3. **The live vessels stop drawing**, through `forceRenderingOff` rather than by disabling
   anything: a frozen real ship parked beside the ghost replaying its own flight is the one thing
   in shot that can only ever confuse. Every component keeps running, and restoring is one bool.

Two consequences, stated rather than buried:

- **The match keeps simulating underneath.** Prisms are still laid, fauna still feed, the clock
  still runs — which is what makes entering and leaving free: you come back to the game you left,
  mid-flight. It also means the world you fly through is the world as it is **now**, not as it was
  during the recording. Prisms are P1; until then the trails in shot are live ones.
- **Re-enabling a canvas re-dirties its graphics.** A `Graphic`'s rebuilds are inert while its
  `Canvas` is disabled (CLAUDE.md's `Canvas.enabled` trap), so anything the HUD tried to redraw
  while the theater was up was dropped. `SetAllDirty` on the way out, rather than leaving a stale
  readout behind.

**What a ghost still does not have: jets, tail or trail.** The puppet is harvested meshes, and a
jet is a particle system — it cannot come along without instantiating the prefab, which is the one
thing the harvest exists to avoid (§ the puppet decision). The prism trail is P1. Both are absent
by construction rather than by oversight.

### 3.2 The four shots

| Shot | What it is |
|---|---|
| **Free** | Halo Forge's monitor. Detached, flown by hand; keeps flying while the replay is paused |
| **Pilot** | That hull's own authored camera offset, so the replay is framed the way its pilot framed it |
| **Orbit** | Circling one pilot in WORLD space, so their tumbling does not tumble the shot |
| **Chase** | Riding one pilot's own frame, so the shot turns and rolls as they do |

**A following camera you cannot steer is a camera you are stuck behind.** The first cut posed each
of these from the subject alone, which left a director with exactly the vantage the shot's author
chose. So all three watching shots carry a **yaw, pitch, dolly and lift the player owns**, applied
on top of the shot's framing: the shot decides where the camera lives, the player decides where it
looks from. Selecting the shot you are already on re-centres it, so one key both chooses a vantage
and undoes however far you steered — there is no separate reset to learn.

**The only difference between the three is the BASIS the offsets are measured in**, which is why
they are one class (`TheaterSubjectCamera`) rather than three. Chase and Pilot ride the subject's
frame, so a barrel roll rolls the shot. Orbit is measured in world space, so the subject can tumble
without taking the camera with it — which is the whole reason to want an orbit rather than a chase.

The dolly is **multiplicative** (e-folds per second), so one press moves the same *fraction* of the
current distance whether the camera is on the hull or a kilometre out. An additive dolly is
unusable at both ends of a fleet whose sizes span two orders of magnitude.

**Pilot is their vantage, not their picture.** P0 records the vessel, not the camera, so the shot
reconstructs the offset from `CameraSettingsSO.followOffset` on the hull's own prefab — read off
the *asset*, which keeps the harvest's whole point intact. The gameplay rig's smoothing and the
speed tunnel's FOV narrowing are not in the recording and are not reproduced. Recording the local
camera's own pose would make it exact for one pilot and costs 36 B/sample; it is not done yet.

**A loop restarts the shot, not just the data.** The orbit's phase used to keep accumulating across
loops, so the same three seconds arrived from a different angle every time and read as a different
recording. Anything the camera accumulates is part of what the viewer is comparing against.

The free camera's **horizon is locked** — yaw accumulates about world up, pitch is clamped short of
vertical, no roll. Forge's monitor cannot roll either, and unlike a vessel a camera has no horizon
of its own to tell a director they are upside down.

Its **speed is in units of the framed action's own radius per second**, so one authored number
crosses a 200-unit skirmish and a 3,000-unit arena in the same few seconds.

Both cameras **read the devices directly**, the way `ScreenshotGesture` and `OverviewGesture` do,
rather than going through `IInputStrategy`. The strategies exist to turn sticks into a *vessel's*
flight parameters — a dual-stick mix, an eased virtual stick, a signed throttle — none of which
means anything to a camera, and routing through one would make the theater's feel a function of
which hull the player happens to be flying.

### 3.3 Controls

| | |
|---|---|
| **9** | Start / stop recording |
| **8** | Enter / leave the recording area |
| **1 2 3 4** | Free / Pilot / Orbit / Chase — press again to re-centre (theater only) |
| **5** | Watch the next pilot (in the theater only) |

The shot keys exist because the first playtest **could not change shot at all** — the buttons were
being pressed through to the HUD underneath. Hiding the UI fixes that, and a key fixes it twice: a
shot you can only reach by clicking is one you cannot reach while flying the free camera with a pad
in both hands. A key cannot be covered.

Inside it, on a pad — **following** a pilot: right stick orbits, left stick dollies in and out,
triggers lift. **Free**: left stick flies, right stick looks, triggers climb and dive. Either way
**RB** boosts and **LB** crawls, **D-pad** cycles shot and pilot, **A** is play/pause. On keyboard
and mouse: **WASD**, **Q/E**, hold **right mouse** to look (arrow keys without it), **Shift**
boost, **Ctrl** crawl.

The pad transport is polled **only** while the recording area is up. Outside it the D-pad and the
south button belong to whatever is on screen, and a director's shortcut that fires during a match
is a control the player did not press.

The overlay is **IMGUI on purpose**: it needs no canvas, no prefab and no scene wiring, so it
exists in every scene the director does — which is the only reason a zero-wire tool can have
controls at all. It is drawn at 2× through `GUI.matrix` rather than by doubling every rect, so the
*font* scales with the buttons; a 2× button wearing 1× text reads as a bug. It scales back down on
a screen too narrow to hold the panel.

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
- **Every theater file now type-checks with zero errors** against a faithful Roslyn stub of each
  Unity and project API it touches (`Vector3`/`Quaternion`/`Camera`/`GUI`/`Gamepad`, plus
  `CameraManager`, `IVesselStatus`, `IPlayer`, `VesselModelBuilder`, `VesselPrefabContainer`,
  `PrismLit`). That is a real compile, not a parse — it catches a missing member, a drifted
  override and an argument mismatch, which a syntax check structurally cannot.
- The six standing out-of-editor gates pass.
- **The first playtest (2026-09-25) found five defects and all five are fixed here**, listed with
  what each one actually was because four of them were one cause wearing four costumes:

  | Reported | Cause |
  |---|---|
  | Static / Pilot> buttons exit to the menu | IMGUI and uGUI both get the click; those two sat over the HUD's Volume/Pause button |
  | Ships are highlight-coloured, not their normal look | The flat domain fill was the default; it was chosen for a void that no longer exists |
  | No prisms, environment, crystals or trails | The culling mask hid the whole world — now opt-in |
  | Shots and free camera "made no difference" | The shot buttons were the only way to change shot, and they were being eaten (row 1) |
  | A loop starts from a different place | The orbit phase accumulated across loops |

- **Still not opened in Unity after those fixes.** A human needs to: press 9, press 9, confirm a
  file in `Recordings/`, press 8, confirm the world and its prisms are in shot and the HUD is gone,
  press **1** and fly the free camera, press **2** and confirm a loop returns to the same vantage,
  press 8 and confirm the HUD and the live ships come back.
- **The failures worth watching for**, both on the way out: the stage restores the camera mask
  first and unconditionally (a mask left on an empty layer is a black screen for the rest of the
  session), and it re-enables every canvas it hid plus the EventSystem. If a session ever comes
  back from the theater with no UI or no input, `TheaterStage.Exit` is the whole of where to look.
