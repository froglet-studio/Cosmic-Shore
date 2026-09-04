# FMOD tasks for Charles (audio owner)

Everything here is authored in **FMOD Studio** (`Cosmic Shore/Cosmic Shore.fspro`) or in the FMOD
inspector fields in Unity — no C# involved. Engineering deliberately did not touch the FMOD project.
Context for each item: `FMOD_AUDIT.md` in this folder. Ordered by impact.

## C1 — Route the buses through the VCAs (unblocks the one-place volume model)

Both VCAs exist (`vca:/Music`, `vca:/SFX`) but are assigned to **no bus**. Assign:

- `Music` group bus → `Music` VCA
- `SFX` group bus → `SFX` VCA

Every event already routes to one of those two buses (checked: 66 events → SFX, `Music` → Music).
Rebuild banks. Then tell engineering: flipping `AudioSystem.driveFmodVcas` makes the in-game
sliders control **everything** — including the `StudioEventEmitter` loops on fauna and crystals that
ignore the slider today — with the slider applied exactly once.

## C2 — `Boost Activate` has a loop region but is fired as a one-shot

`event:/SFX/Oneshots/Gameplay sfx/Boost Activate` loops. The code refuses to fire a looping event
fire-and-forget (it leaked one immortal instance per boost — see `PERFORMANCE_OPTIMIZATION.md §0.4`),
so **boost is silent** and logs one error per session. Either remove the loop region (a sting), or
tell engineering it is meant to be a sustained bed and it gets an owned start/stop instance like the
drift.

## C3 — Wire the three empty `AudioSystem` slots (Unity inspector, Bootstrap scene)

`AudioSystem` (Bootstrap scene, AudioSystem prefab instance) has three unwired categories that each
log a warning the first time they fire: `driftStartEvent`, `driftEndEvent` (probably intentionally
empty — the Squirrel's drift is `DriftAudioController`; wire them only if the other vessels should
get a generic drift sting) and **`creatureBlockHitEvent`** (a creature eating a non-flora prism —
currently silent; this one wants a sound).

## C4 — Squirrel proximity boost: a one-shot used as a loop

`Squirrel.prefab ▸ ProximityBoostAudioController`: `boostTickEvent` AND `boostLoopEvent` both point
at `…/Gameplay sfx/Skim` (a one-shot). The loop slot expects a looping event with a 0..1
`Boost Amount` parameter (continuous "speed surge" layer). Author one, or clear `boostLoopEvent` so
each skim plays once.

## C5 — Max-instance / voice-stealing limits on the creature loops

Fauna loops (`Mass shark`, `Mass Tadpole`, `Mass brittle star`, the Charge/Space/Time tadpoles) are
one instance per creature; Wildlife Liberation runs ~500–1,200 creatures. Engineering enabled
`StopEventsOutsideMaxDistance` so only creatures inside each event's **max distance** hold a voice —
so please **check every loop's 3D max distance is deliberate** (a huge max distance defeats it),
and set a per-event **Max Instances** with stealing = Oldest/Quietest on each loop.

## C6 — Real channel count for desktop builds (FMOD ▸ Edit Settings ▸ Platform)

Play-in-Editor is set to 1024 virtual / 256 real channels; the **default platform (i.e. builds)
inherits FMOD's default 32 real channels**, so the editor mix and the build mix differ. Set the
desktop platform's Real Channel Count explicitly (128 is a reasonable start) so what you mix in the
editor is what ships.

## C7 — `CrystalTime.prefab` emitters

Two `StudioEventEmitter`s both playing `Creature colide`: one on **Trigger Enter** (fires for every
collider that enters the crystal — skimmers, prisms, fauna) and one on **Object Destroy**. Confirm
that is the intent; the trigger-enter one is a likely source of untuned bursts.

## C8 — Optional: enable the FMOD error callback while diagnosing

`FMOD ▸ Edit Settings ▸ Enable Error Callback` is off and Logging Level is None, so FMOD's own
diagnostics never reach the console — the "random FMOD errors" you see are all Unity-side messages.
Turn Error Callback on (and Logging to Warning) during a bug hunt; it names the event/function that
failed. Turn back off for release.

## Reference — how volume works after this branch

- Sliders → `GameSetting` (persisted, roams, last-writer-wins) → `AudioSystem`.
- Today: `AudioSystem` applies the slider **per instance** on everything the code creates
  (engine, drift, boost, flora bed, all one-shots, music). Emitters authored on prefabs are not
  covered.
- After C1 + `driveFmodVcas` on: `AudioSystem` writes the two VCAs and the per-instance path
  collapses to each emitter's trim — one knob, everything covered.
