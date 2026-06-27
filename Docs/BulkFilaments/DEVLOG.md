# The Bulk Filaments Dev Log

Newest entries first. Keep this as the local implementation trail; preserve broader
handoff context in Second Brain Prime.

## 2026-06-27 - Startup recovery and moving filament roots

Todd reported that the countdown reached `GO`, but gameplay never began: no
Squirrel, no controls, and only the wormhole/filaments animating. The filament
root flares also stayed pinned to their original wall positions while filaments
rotated.

Root cause:

- Bulk still required `gameData.LocalPlayer.IsLocalUser` before staging the
  vessel.
- Offline/prototype mini-game players can legitimately report `IsLocalUser ==
  false`, and the scene mini-game spawner can miss `OnInitializeGame` if its
  `Start()` subscribes after the arcade controller fires initialization.
- That left the run active after countdown while `StageVesselForStart()` waited
  forever for a strict local vessel.

Changes:

- Added an idempotent `MiniGamePlayerSpawnerAdapter.EnsureLocalPlayerSpawned()`
  helper.
- Bulk now resolves the playable human vessel from `LocalPlayer`,
  `gameData.Players`, or scene `Player` objects, and force-spawns through the
  mini-game spawner if the initialize event was missed.
- Bulk starts the recovered player after placing the vessel on the first filament
  and falls back to the recovered player's `RoundStats` when `LocalRoundStats`
  is absent.
- Moved filament wall roots into `BulkFilamentsController.Roots.cs`; root forks
  and root sprite overlays now recompute from each filament endpoint every frame.

Verification:

- `git diff --check` passes for the touched files.
- Unity 6000.3.17f1 detected earlier script changes and showed no `error CS`
  entries in the checked editor-log tail.
- After a final namespace cleanup, Unity batchmode stalled before reaching the
  C# compile phase, matching prior package/import stalls in this project.
- Needs live editor import/playtest confirmation that the Squirrel stages after
  `GO` and the roots visually track rotating filaments.

## 2026-06-26 - Audio, palette, and nanite chase playtest pass

Todd completed a playable run and reported that Dopamine was too quiet/not
obviously slider-controlled, the whole mode had become a teal glow wash, and
the nanites were warping between filaments instead of visibly chasing.

Changes:

- Let Bulk's Dopamine source use the full music slider range instead of the
  normal `AudioSystem` `/5` legacy cap, while still responding to music
  enable/level changes.
- Re-applied Bulk music volume after muting default jukebox/background layers
  so the mode-specific music cannot be left quiet by the normal mix cleanup.
- Retuned Bulk energy and mirror shaders away from pure additive teal glow:
  darker wall values, lower alpha/emission pulse, violet/gold/orange accents,
  red nanites, magenta crystals, and reduced sprite-overlay glow.
- Moved nanite visuals into `BulkFilamentsController.Nanites.cs` and replaced
  per-filament snapping with velocity/acceleration steering, offsets, and
  separation so the swarm flies through gaps while the route-distance chase
  still controls fail pressure.

Verification:

- `git diff --check` passed for the touched Bulk C#, shaders, and the new
  nanite partial.
- Unity 6000.3.17f1 editor log showed successful domain reload/import after
  the code changes, with only Unity AI/account/licensing warnings at the tail.

## 2026-06-25 - Startup deadlock and mirror shader repair

Todd reopened Bulk Filaments after the sprite import and saw the wormhole render
with no ship, no controls, and no countdown while Unity showed console errors.

Changes:

- Removed the start-flow deadlock where Bulk waited for a local vessel before
  starting countdown, even though the base single-player controller activates
  the vessel after countdown completion.
- Moved first-filament vessel staging into the post-countdown run path and kept
  retrying during update until the local Squirrel is available.
- Preserved ongoing vessel validation after staging so replay/reset-created
  vessel references can still be reacquired.
- Fixed `BulkVoronoiMirror` on Metal by removing duplicate declarations of
  Unity's built-in reflection probe symbols.

Verification:

- Force-quit the hung Unity 6000.3.17f1 editor and its import workers.
- Confirmed source no longer contains the old
  `Waiting for local vessel before countdown` gate or the duplicate
  `unity_SpecCube0` declarations.
- Unity batchmode import reached package registration and then idled with no
  import/compiler workers or log progress, so live editor reimport remains the
  next authoritative verification step.

## 2026-06-25 - High-detail sprite handoff import

Todd provided the completed sprite-art handoff from
`output/imagegen/bulk-filaments-sprite-art-2026-06-25`.

Changes:

- Replaced the first-pass `1152x96` runtime strips with the new 12-frame 4x3
  atlases for filament interior flow, capture ring flares, tether crackle,
  gameplay nanites, speed diamonds, root wall lightning, and fauna/nanite events.
- Kept the existing `Resources/Textures/BulkFilaments` runtime names so the
  controller loads versioned assets from the Unity repo instead of the staging
  output folder.
- Updated `BulkSpriteSheet` to support 4-column x 3-row atlases.
- Added black-to-alpha shader handling for glow sheets, letting black-background
  additive-style art disappear without showing rectangular boxes.
- Used transparent atlases for gameplay nanites, speed diamonds, and fauna/event
  cards so enemies and pickups remain readable.

Verification:

- `git diff --check` passed.
- Unity 6000.3.17f1 batch import completed with no C# compile errors, shader
  errors, YAML parser errors, or PackageCache duplicate-path errors.

## 2026-06-25 - Sprite-overlay detail and ecosystem pass

Todd asked to push the visual/gameplay detail toward the 2026-06-24 upgrade
report: closer race-style follow cam, sprite-sheet energy detail over existing
shader geometry, more intricate filament roots, non-formation nanites, and fauna
inside the wormhole that can distract the chase.

Changes:

- Added 12-frame runtime sprite sheets for filament flow, latch-ring flares,
  tether crackle, nanite insects, power diamonds, filament roots, and squid-like
  Bulk fauna.
- Added `BulkSpriteSheet` transparent shader and a runtime sprite-overlay partial
  so those sheets layer over filaments, latch rings, tethers, crystals, roots,
  nanites, and fauna without replacing the existing shader/line geometry.
- Tightened camera zoom: closest follow distance is much closer behind the
  Squirrel, and max zoom-out is roughly half the previous range.
- Added close-camera impact showers so contact bursts can throw particles and
  short lightning strokes through the camera volume at close follow distances.
- Replaced the nanite ring/halo placement with a deterministic swarm cloud.
- Added ambient squid-like fauna; nanites can get distracted, eat fauna, fall
  behind slightly, and play a low procedural giant-fauna cry for large kills.
- Expanded filament root geometry from simple five-line flares into wider
  branching root forks, then layered animated root sprites over the endpoints.

Verification:

- `git diff --check` passed.
- Conflict-marker scan over touched Bulk C# files, the new shader, and generated
  texture metadata found no markers.
- Sprite-sheet sanity check confirmed all seven generated PNG sheets are valid
  12-frame strips at `1152x96` (`96x96` per frame).
- Unity 6000.3.17f1 batch import was attempted, but it did not reach the C# or
  shader compile phase before being stopped after the log sat in package scan
  warnings for duplicated `Packages/com.unity.timeline/* 2` immutable-package
  assets. No Bulk compile or shader errors appeared in the log before stop.

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
