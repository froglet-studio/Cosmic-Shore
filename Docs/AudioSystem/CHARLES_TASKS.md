# FMOD tasks for Charles (audio owner)

Everything here is authored in **FMOD Studio** (`Cosmic Shore/Cosmic Shore.fspro`) or in the FMOD
inspector fields in Unity — no C# involved. Engineering deliberately did not touch the FMOD project.
Context for each item: `FMOD_AUDIT.md` in this folder. Ordered by impact.

## C0 — Replace the placeholders, in list order (new, 2026-11-16)

Every empty vessel slot (and the race-mode, menu and payload slots beside them) now plays a recycled,
already-banked event, so nothing the player does is silent (Garrett, 2026-11-16). Each one is listed
with the event that replaces it in **[`PLACEHOLDERS.md`](PLACEHOLDERS.md)**, ranked P1 (engines,
primary fire, the race gates) to P4. To replace one, author the event at its target path in FMOD Studio,
build the banks, commit; then `python3 Tools/Build/sound_placeholders.py --promote` moves the slots onto
it. No Unity inspector work. C4 and the four C9 sounds below are part of that list now.

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
fire-and-forget (it leaked one immortal instance per boost — see `Docs/archive/PERFORMANCE_LOG_2026.md §0.4`),
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

**2026-11-16:** cleared on the Squirrel, Goby and Thresher (the tick keeps playing). The loop is P1 in
`PLACEHOLDERS.md` as `event:/SFX/Vessels/Shared/Proximity boost`; no banked loop takes `Boost Amount`,
so it has no placeholder.

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

## C9 — Four UI sounds are SILENT until you author their events (new, 12 Sep)

Four menu/UI sounds were playing from `.wav` files inside the **NiceVibrations plugin's demo
folder** (`HapticSamples/`). Those clips sit under a notice that opens by declaring the pack
**CC-BY-NC 3.0 — non-commercial** — and whose source list names freesound titles rather than the
shipped filenames, so no shipped clip can be mapped to a term. They have been unwired and the four
call sites migrated to FMOD `EventReference` fields, per CLAUDE.md's audio convention.

**2026-11-16: no longer empty.** The countdown beat, typing tick and both currency-burst slots in
Menu_Main now hold listed placeholders (`Option Click`, `RedeemTicket`, `SmallReward`); their targets are
in `PLACEHOLDERS.md`. What follows is the original note.

**Every one of those four slots is deliberately EMPTY, so all four are silent right now.** That is
the convention, not an oversight: an empty slot is a visible TODO in the inspector, where pointing
it at a borrowed "temp" event would be an invisible one that survives to release. Nothing was
substituted, so what replaces each is entirely your call.

| Sound | Where the slot is | What it was doing |
|---|---|---|
| Countdown beat | `CountdownTimer.prefab` ▸ `CountdownTimer.countdownBeepEvent` | One tick per countdown number (3 · 2 · 1 · GO), in every game scene |
| Typing tick | `Profile.prefab`, `ModalWindows.prefab`, `Menu_Main` ▸ `ProfileModal.typingAudioEvent` | One tick per character while a random display name types itself in, ~75 ms apart |
| Currency burst — fired | `Menu_Main` ▸ `IconEmitter.onTriggerEvent` (3 emitters) | The moment a currency burst launches |
| Currency burst — landed | `Menu_Main` ▸ `IconEmitter.targetReachedEvent` (3 emitters) | The last icon arriving at the balance |

Two notes for authoring. The typing tick fires **~13 times a second** for the length of a name, so
it wants to be very short and cheap, with Max Instances set. And `IconEmitter` carries a per-emitter
`AudioVolume` trim (0.1 on the shipped emitters) which is multiplied into the player's SFX level, so
author these at full level and let the trim do the ducking.

All four now route through `AudioSystem` / `FMODOneShotVolumeHelper`, so they respect the SFX slider
the moment you wire them — including the `IconEmitter` pair, which previously called
`AudioSource.PlayOneShot` and ignored it.

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

<!-- sound-slots:begin -->
## Generated: the slot audit (2026-11-16, `python3 Tools/Build/sound_slots.py --charles`)

Written by the tool from the assets, the code and the FMOD project; edit nothing here by hand, rerun it. Every FMOD slot in the game, what the code expects of it (read from the call sites), what is in it, and what the Studio project says about that event. The hand-written sections above are the tasks; this is the queue.

**Counts.** 498 slots: 93 empty, 405 filled (434 serialised blocks, 63 fields the code declares that no asset has saved yet, 48 scene overrides folded into the prefab slots they set). FMOD: 72 events authored (24 loop), 71 in the built banks (`Build/GUIDs.txt`), 59 distinct events in use. Bank folders built: Desktop.

### Mis-wired slots (what `--check` fails on)

| Slot | Owner | Code expects | In the slot | Class | Why |
|---|---|---|---|---|---|
| `Thresher.prefab` ▸ `ThresherAudioController.rightTriggerEvent` | Thresher | loop | `SFX/Loops/Morning Star Right trigger (?)` | unknown-event | 'event:/SFX/Loops/Morning Star Right trigger' is not an event in the FMOD project (renamed or deleted in Studio; a stale reference) |

### Bank gaps

- no banks for Android (listed in ProjectSettings applicationIdentifier.Android; the FMOD project builds for: Desktop; bank folders built: Desktop. Add a Mobile platform in FMOD Studio and build; until then the Unity integration loads Build/Desktop on Android, because no BuildDirectory is set per platform)
- no banks for iOS (listed in ProjectSettings applicationIdentifier.iPhone; the FMOD project builds for: Desktop; bank folders built: Desktop. Add a Mobile platform in FMOD Studio and build; until then the Unity integration loads Build/Desktop on iOS, because no BuildDirectory is set per platform)

### Empty slots by owner (the brief queue)

| Owner | Empty | Slots (asset ▸ field; ◦ = declared in code, not saved in the asset yet) |
|---|---|---|
| Fauna | 44 | `BeeFauna.prefab` ▸ EventReference (emitter); `BeeFauna.prefab` ▸ pollinateSfx◦ (one-shot); `BeeFauna.prefab` ▸ stingSfx◦ (one-shot); `Arboretum Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Astro League Piranha Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Astro League Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Blob Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Blob Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Chum I1 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Chum I2 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Chum I3 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Chum I4 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Glasshouse I1 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Glasshouse I2 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Glasshouse I3 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Glasshouse I4 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Harvest I4 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Kelpwood I1 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Kelpwood I2 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Kelpwood I3 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Kelpwood I4 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Lattice Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Nucleus Rush Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Serpentine I1 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Serpentine I2 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Serpentine I3 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Serpentine I4 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Skim Race Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Thresher Time I1 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Thresher Time I2 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Thresher Time I3 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Thresher Time I4 Tadpole Fauna Config Data.asset` ▸ Variant.AudioLoopEvent (emitter); `Bee Fauna Charge.asset` ▸ Variant.AudioLoopEvent (emitter); `Bee Fauna Mass.asset` ▸ Variant.AudioLoopEvent (emitter); `Bee Fauna Space.asset` ▸ Variant.AudioLoopEvent (emitter); `Bee Fauna Time.asset` ▸ Variant.AudioLoopEvent (emitter); `Tadpole Fauna Charge.asset` ▸ Variant.AudioLoopEvent (emitter); `Tadpole Fauna Space.asset` ▸ Variant.AudioLoopEvent (emitter); `SwarmEvoFateFaunaConfig.asset` ▸ SwarmLoopEvent (emitter); `SwarmFaunaConfig.asset` ▸ SwarmLoopEvent (emitter); `SwarmGridFaunaConfig.asset` ▸ SwarmLoopEvent (emitter); `SwarmSortFaunaConfig.asset` ▸ SwarmLoopEvent (emitter); `TandavaSwarmFaunaConfig.asset` ▸ SwarmLoopEvent (emitter); `TandavaSeveringSwarmFaunaConfig.asset` ▸ SwarmLoopEvent (emitter) |
| Pilots | 18 | `CallsignSet.asset` ▸ callsigns[0].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[10].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[11].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[12].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[13].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[14].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[15].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[16].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[17].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[1].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[2].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[3].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[4].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[5].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[6].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[7].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[8].openSound (one-shot); `CallsignSet.asset` ▸ callsigns[9].openSound (one-shot) |
| UI | 12 | `ModalWindows.prefab` ▸ onTriggerEvent◦ (one-shot); `ModalWindows.prefab` ▸ onTriggerEvent◦ (one-shot); `ModalWindows.prefab` ▸ onTriggerEvent◦ (one-shot); `ModalWindows.prefab` ▸ onTriggerEvent◦ (one-shot); `ModalWindows.prefab` ▸ onTriggerEvent◦ (one-shot); `ModalWindows.prefab` ▸ targetReachedEvent◦ (one-shot); `ModalWindows.prefab` ▸ targetReachedEvent◦ (one-shot); `ModalWindows.prefab` ▸ targetReachedEvent◦ (one-shot); `ModalWindows.prefab` ▸ targetReachedEvent◦ (one-shot); `ModalWindows.prefab` ▸ targetReachedEvent◦ (one-shot); `Screens.prefab` ▸ onTriggerEvent◦ (one-shot); `Screens.prefab` ▸ targetReachedEvent◦ (one-shot) |
| Music | 9 | `Soundtrack.asset` ▸ tracks[0].trackEvent (loop); `Soundtrack.asset` ▸ tracks[1].trackEvent (loop); `Soundtrack.asset` ▸ tracks[2].trackEvent (loop); `Soundtrack.asset` ▸ tracks[3].trackEvent (loop); `Soundtrack.asset` ▸ tracks[4].trackEvent (loop); `Soundtrack.asset` ▸ tracks[5].trackEvent (loop); `Soundtrack.asset` ▸ tracks[6].trackEvent (loop); `Soundtrack.asset` ▸ tracks[7].trackEvent (loop); `Soundtrack.asset` ▸ tracks[8].trackEvent (loop) |
| Thresher | 5 | `MorningStar.prefab` ▸ EventReference (emitter); `Thresher.prefab` ▸ boostLoopEvent (loop); `Thresher.prefab` ▸ leftTriggerEvent (loop); `Thresher.prefab` ▸ leftTriggerReleaseEvent (one-shot); `Thresher.prefab` ▸ rightTriggerReleaseEvent (one-shot) |
| AudioSystem | 2 | `AudioSystem.prefab` ▸ driftEndEvent◦ (one-shot); `AudioSystem.prefab` ▸ driftStartEvent◦ (one-shot) |
| Goby | 1 | `Goby.prefab` ▸ boostLoopEvent (loop) |
| Serpent | 1 | `Serpent.prefab` ▸ pelletIgniteEvent◦ (one-shot) |
| Squirrel | 1 | `Squirrel.prefab` ▸ boostLoopEvent (loop) |
<!-- sound-slots:end -->
