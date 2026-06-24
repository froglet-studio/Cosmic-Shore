# The Bulk Filaments Dev Log

Newest entries first. Keep this as the local implementation trail; preserve broader
handoff context in Second Brain Prime.

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
