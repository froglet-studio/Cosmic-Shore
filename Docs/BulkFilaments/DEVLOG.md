# The Bulk Filaments Dev Log

Newest entries first. Keep this as the local implementation trail; preserve broader
handoff context in Second Brain Prime.

## 2026-06-24 - Music slider and wall-art restore

Todd playtested the richer build and reported that Bulk's custom music was too
loud and ignored the music volume slider, and that the wormhole wall artwork had
become less visible.

Changes:

- Bulk's custom `Dopamine` music source now listens to `GameSetting` music
  enable/level events.
- Bulk music uses the same `/5` legacy `AudioSystem` music scaling as the normal
  music sources, so the settings music slider controls it live.
- Removed the forced `AudioListener.volume = 1` behavior from Bulk audio.
- Added periodic Bulk audio mix enforcement so normal jukebox/background layers
  stay muted if they start after Bulk has already loaded.
- Retuned the Voronoi mirror wall toward darker stained-glass/circuit artwork:
  stronger opacity, stronger blue cell boundaries, darker facets, and less
  reflection-probe washout.

Verification:

- `git diff --check` passed.
- Unity batch import and Play Mode QA were blocked because the project was open
  in another Unity instance.

## 2026-06-24 - Depth sprites, visible nanites, finale, and scoring pass

Todd asked for the brighter level to regain depth and texture, for stronger
contact explosions, for the nanite chase to become readable, for a mission
accomplished launch finale, for realtime-ish mirror reflection, and for scoring
to consider route progress, elapsed time, and speed crystals.

Changes:

- Added a lightweight animated dark-glyph sprite shader for surface detail.
- Added animated glyph quads along filaments, on speed crystal faces, and on the
  front/rear latch riding rings to add darker moving highlights over the bright
  energy palette.
- Made speed diamonds use per-crystal material instances with non-white,
  constantly shifting hues.
- Increased speed diamond, pulse gate, nanite, hazard, latch, respawn, and
  transfer contact particle/lightning bursts.
- Made the nanite swarm much larger and added a visible chase wake line so the
  tail pressure reads clearly during play.
- Added a low-resolution realtime reflection probe for the outer mirror wall;
  the shader now blends probe reflection with its procedural Voronoi facets.
- Added a Bulk Break finale: on final transfer the squirrel launches out of the
  wormhole into a starfield while green mission-complete HUD text appears.
- Changed score calculation to combine elapsed time, filaments traversed, speed
  crystals collected, respawns, and incomplete-route penalties while preserving
  low-score/golf-style sorting.
- Hardened the editor QA driver so if bootstrap/auth/menu scenes steal focus
  after Bulk loads, it reloads Bulk and resumes the smoke test.

Verification:

- Unity 6000.3.17f1 batch import completed with no C# compile errors and no
  shader errors.
- Bulk Filaments direct Play Mode smoke test passed after bootstrap reload:
  3 transfers, 1 crystal.

## 2026-06-24 - Visual/gameplay upgrade implementation burst

Todd asked to bring the playable prototype closer to the visual upgrade report:
richer tunnel texture, beat-reactive motion, living filaments, bigger speed
diamonds, pulse gates, nanite pressure, and a stronger finale.

Changes:

- Changed orbit input into an angular thruster model: controller input adds
  angular velocity, the vessel keeps arcing after thrust, and transfers damp but
  do not fully erase rotational motion.
- Added per-filament rotation around the wormhole axis so latch alignment is a
  moving target while the widened transfer window keeps the prototype forgiving.
- Replaced straight beam samples with low-amplitude multi-period sine offsets so
  filaments read more like living vines/branches.
- Enlarged speed diamonds by 4x, swapped them to runtime octahedron meshes, and
  added pickup shard bursts, local particles, and extra lightning branches.
- Added pulse gates at roughly 15% route intervals; passing one fires a blue
  ring surge, speed impulse, stacked max-speed bonus, procedural SFX, particles,
  and lightning.
- Added direction-change nanite pops: reversing orbit thrust can destroy a few
  trailing nanites in glowing particle/lightning bursts.
- Added a finale ramp after roughly 72% route progress: wall pulse, lightning
  cadence, nanite pressure, material pulse, and camera FOV all intensify.
- Added a lightweight fake mirror/Voronoi wall shader and runtime cylinder mesh
  outside the wormhole rings for the trippy hall-of-mirrors look.
- Added a lightweight emissive transparent shader for Bulk energy lines,
  diamonds, gates, shards, nanites, and lightning.
- Lowered minimum camera follow distance from 62 to 31 so the player can zoom
  about 2x closer to the Squirrel.

Verification:

- Unity batchmode compile could not run because this project was already open in
  another Unity instance.
- A temporary Unity reference-assembly C# harness compiled the Bulk partials and
  `OctahedronMeshGenerator` successfully with `UNITY_EDITOR` and
  `ENABLE_INPUT_SYSTEM` defined.

## 2026-06-23 - Live filament waveform overlay

Todd asked for the actual `Dopamine.mp3` waveform to be drawn over each filament
as a same-color energy ribbon.

Changes:

- Added `BulkFilamentsController.Waveform.cs` as a small partial dedicated to
  live music visualization.
- Each filament now gets a waveform `LineRenderer` above the beam at roughly 2x
  beam width.
- The waveform samples `_musicSource.GetOutputData(...)`, normalizes the live
  output window, and scrolls the sample phase opposite vessel travel at
  `_speed * 8`.
- Reset paths clear waveform scroll and sample state with the rest of the runtime
  Bulk visuals.

## 2026-06-23 - Latch, audio, and documentation pass

Todd completed a real playthrough and reported:

- The result screen could end too early while the Squirrel kept climbing.
- `Dopamine.mp3` was not audible during gameplay.
- Trigger pulls were hard to correlate with transfers.
- Transfers needed distinct fire and lock-on sound feedback.
- Camera zoom would help sell speed.

Changes:

- Disabled the copied Wildlife Blitz `TurnMonitorController` so Bulk owns its
  finish condition.
- Reworked latch input into staged RT front latch and LT rear latch.
- Added detailed `[BulkFilamentsInput]` logs for trigger timing analysis.
- Added left-stick horizontal camera zoom.
- Added runtime music source hardening and a single-listener guard.
- Follow-up tuning widened the effective latch window, made held RT lock when it
  reaches the transfer zone, and changed LT to a clean 2-second rear-latch grace
  after front lock instead of a second distance-window check.
- Forced Bulk prototype audio sources and listener volume to audible settings
  after Unity logs showed `Dopamine.mp3` loaded and `Play()` was called even
  though Todd could not hear music or latch SFX.
- Generated and installed KIE latch sound effects:
  - `Assets/Resources/Audio/BulkFilaments/BulkGrappleFire.mp3`
  - `Assets/Resources/Audio/BulkFilaments/BulkLatchSurge.mp3`
- Added Bulk-only audio mix isolation: default AudioSystem music and other
  looping/background layers are muted/paused while Dopamine plays, then restored
  when Bulk resets.
- Generated and installed `Assets/Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3`.
- Power crystals now play pickup SFX, spawn a small local lightning burst, increase
  current speed, and stack a max-speed bonus for the run.
- Added dense branching procedural wormhole wall lightning plus filament-to-
  filament lightning hazards that reset speed on hit.
- Added underdamped transfer tether retraction: latch cables render as segmented
  energy curves, and the vessel itself briefly undershoots too close to the new
  filament before settling back to the correct support distance.
- Removed the close-camera speed-particle experiment after playtest feedback; it
  read as camera-dependent spinning sticks instead of convincing velocity.
- Kept procedural audio fallback for missing assets and latch misses.

Next test:

- Launch the Arcade card, confirm `Dopamine.mp3` plays, and play one route.
- After the run, inspect Unity logs for `[BulkFilamentsInput]` to separate actual
  trigger locks from automatic or end-of-filament behavior.
- Tune latch windows after seeing the log timings from Todd's controller pulls.

## Earlier prototype notes

See `Assets/_Scripts/Controller/Arcade/BULK_FILAMENTS.md` for the current control
summary and Second Brain Prime Froglet journal notes for the full chronology.
