# The Bulk Filaments

First playable prototype for `GameModes.TheBulkFilaments`.

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
- Arcade Explore card: `OrganicRematchGames.asset` is the runtime-injected menu list.
- Runtime-generated wormhole, filaments, crystals, latch rings, nanites, and simple
  HUD telemetry.

## Controls

- Left stick up/down: bias speed along the current filament.
- Right stick left/right: orbit around the current filament.
- Left or right trigger: fire the auto-aimed latch rings at the next filament.
- Keyboard fallback: W/S throttle, A/D or arrows orbit, Space/Enter latch.

## Loop

1. The active filament is green.
2. The next filament shifts from red/orange/yellow toward green near closest approach.
3. Trigger inside the timing window to transfer.
4. Trigger too early/late to miss; if the vessel runs out of filament, it respawns at
   the previous filament while the clock and nanite chase continue.
5. Filament nanites rise from below and catch the player if they fail to keep pace.
6. Finish after an intensity-scaled number of transfers.

## Intensity

Transfer count scales as `12 / 18 / 24 / 30` for intensities 1-4.

## Scoring

V1 uses golf-style scoring:

`elapsed time + respawn penalties - crystal time credits`

Lower score is better. Crystals collected are also written to `RoundStats`.

## Follow-Up Polish

- Replace cloned Wildlife Blitz HUD/endgame references with Bulk-specific HUD/endgame
  presentation.
- Replace runtime primitive placeholders with authored shader/sprite assets.
- Add actual audio analysis or authored beat map for Dopamine lightning/surges.
- Add multiplayer lanes where each player gets a distinct filament chain in the same
  wormhole volume.
