# The Bulk Filaments

First playable prototype for `GameModes.TheBulkFilaments`.

Living docs:

- `Docs/BulkFilaments/PRD.md`
- `Docs/BulkFilaments/DEVLOG.md`

## Fantasy

Arks sometimes send vessels out of a cell and into the Bulk, the higher-dimensional
embedding around the known universe. A pilot rides energy filaments through a
wormhole-like shortcut; if the vessel survives, the Ark's navigation system can
follow the computed route and deliver information faster than rival couriers.

## Current V1

- Single-player only.
- Squirrel-only via `ArcadeGameTheBulkFilaments.asset`.
- Scene: `MinigameBulkFilaments`.
- Controller: `BulkFilamentsController`.
- Music: `Resources/Audio/Music/Dopamine.mp3`.
- Latch SFX: `Resources/Audio/BulkFilaments/BulkGrappleFire.mp3` and
  `Resources/Audio/BulkFilaments/BulkLatchSurge.mp3`.
- Power crystal SFX: `Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3`.
- Arcade Explore card: `OrganicRematchGames.asset` is the runtime-injected menu list.
- Runtime-generated wormhole, filaments, live music waveform overlays, crystals,
  latch rings, animated sprite overlays, nanites, ambient fauna, and simple HUD
  telemetry.

## Controls

- Left stick up/down: look ahead/down the wormhole with the follow camera.
- Left stick left/right: zoom the follow camera out/in.
- Right stick left/right: orbit around the current filament.
- Right stick up/down: bias speed along the current filament.
- Right trigger: fire the front latch ring at the next filament.
- Left trigger: fire the rear latch ring after the front ring locks.
- Keyboard fallback: W/S throttle, A/D or left/right arrows orbit,
  up/down arrows camera look, Space front latch, Enter rear latch.

## Loop

1. The active filament is green.
2. The next filament shifts from red/orange/yellow toward green near closest approach.
3. Pull or hold right trigger inside the timing window to lock the front ring.
4. After the front ring energizes, pull left trigger within 2 seconds to complete
   the transfer.
5. Trigger too early/late to miss; if the vessel runs out of filament, it respawns at
   the previous filament while the clock and nanite chase continue.
6. Filament nanites rise from below and catch the player if they fail to keep pace.
7. Power crystals add speed, lightning pickup bursts, and time credit.
8. Wormhole lightning crawls along the walls; filament-to-filament bolts can reset
   speed if they hit the vessel.
9. Live Dopamine waveform ribbons pulse over the filaments opposite the vessel's
   travel direction at 8x vessel speed.
10. Sprite overlays add animated flow, flares, crackle, sparkle, root energy,
   nanite motion, and squid-like fauna detail over the runtime geometry.
11. Nanites chase as a swarm and can get distracted eating ambient fauna before
   resuming pursuit.
12. Finish after the Bulk controller's 20-30 transfer chain is completed.

## Intensity

Transfer count scales from roughly `24` to `30` transfers for intensities 1-4,
with the `Dopamine.mp3` length used as a lower bound when available.

## Scoring

V1 uses golf-style scoring:

`elapsed time + respawn penalties - crystal time credits`

Lower score is better. Crystals collected are also written to `RoundStats`.

## Follow-Up Polish

- Replace cloned Wildlife Blitz HUD/endgame references with Bulk-specific HUD/endgame
  presentation.
- Replace remaining runtime primitive placeholders with authored shader/sprite assets.
- Extend live Dopamine analysis from filament waveforms into authored lightning
  and fauna surge beats.
- Add multiplayer lanes where each player gets a distinct filament chain in the same
  wormhole volume.
