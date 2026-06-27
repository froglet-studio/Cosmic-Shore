# The Bulk Filaments PRD

Status: playable prototype, active iteration
Owner lane: Froglet / Cosmic Shore
Primary scene: `MinigameBulkFilaments`
Primary controller: `BulkFilamentsController`

## Product Fantasy

Arks send Squirrel-class vessels upward out of a cell and into the Bulk, a higher-
dimensional embedding around the known universe. The pilot rides living energy
filaments through a reflective wormhole to chart a courier shortcut across the
galaxy. Survival proves the route so the Ark's navigation computer can follow.

## Player Promise

The player should feel like they are riding a neon tram line at impossible speed,
then launching through graceful bungee-like transfer arcs from filament to
filament while the tunnel collapses behind them.

## Current Scope

- Single-player first.
- Squirrel vessel only until more vessel assets are ready.
- Arcade card name: `The Bulk Filaments`.
- Description: `Ride living energy filaments through the Bulk, timing each transfer-latch before the wormhole collapses behind you.`
- Runtime-generated wormhole, filament chain, live music waveform filament
  overlays, sprite-sheet energy overlays, crystals, latch rings, nanite chase,
  ambient fauna, HUD telemetry, music, and latch sound effects.

## Core Loop

1. Ride the active green filament upward through the wormhole.
2. Orbit around the filament to collect crystals and avoid hazards.
3. Watch the next filament shift toward green as the transfer window approaches.
4. Pull or hold right trigger to fire and lock the front latch ring.
5. Pull left trigger within 2 seconds after front lock to fire the rear latch ring
   and commit transfer.
6. Misses cost time and speed; running off a filament respawns at the previous
   filament while the clock and nanites continue.
7. Finish after the full 20-30 transfer route.

## Controls

- Left stick up/down: camera look along the wormhole.
- Left stick left/right: camera zoom.
- Right stick left/right: orbit around the current filament.
- Right stick up/down: bias speed along the current filament.
- Right trigger: front latch ring.
- Left trigger: rear latch ring after the front ring locks.
- Keyboard fallback: W/S throttle, A/D orbit, arrows for camera, Space front latch,
  Enter rear latch.

## Scoring

V1 uses golf-style scoring:

`elapsed time + respawn penalties - crystal time credits`

Lower score wins. Crystals are also tracked for feedback and later scoring polish.

## Intensity

Intensity should scale:

- transfer count
- average speed and acceleration pressure
- latch timing strictness
- crystal density and hazard density
- nanite chase pressure
- wormhole visual chaos

## Audio

- Background music: `Assets/Resources/Audio/Music/Dopamine.mp3`.
- Latch fire: `Assets/Resources/Audio/BulkFilaments/BulkGrappleFire.mp3`.
- Latch surge: `Assets/Resources/Audio/BulkFilaments/BulkLatchSurge.mp3`.
- Power crystal pickup: `Assets/Resources/Audio/BulkFilaments/BulkPowerCrystal.mp3`.
- Miss feedback may remain procedural until the latch feel is stable.

## Feel Systems

- Bulk pauses and mutes default looping Cosmic Shore music / ambience while
  Dopamine plays, then restores those sources when Bulk resets.
- Pink power crystals play pickup audio, flash local lightning, and increase the
  vessel's maximum speed for the rest of the run.
- Wormhole-wall lightning crawls upward as a dense branching visual beat layer.
- Filament-to-filament lightning bolts can hit the vessel and reset speed back to
  the initial baseline.
- Each filament carries a live waveform from `Dopamine.mp3`: the ribbon uses the
  filament color, sits over the beam, renders at roughly 2x beam width, and
  scrolls opposite the vessel at 8x vessel speed.
- Transfer tethers should retract with a controlled underdamped feel: stretch,
  the vessel briefly gets pulled too close to the new filament, then settles back
  to the correct support length.
- Animated sprite overlays add high-frequency visual detail inside/around the
  filaments, latch rings, energy tethers, power crystals, filament roots, nanites,
  and ambient fauna while keeping the underlying runtime geometry lightweight.
- Nanites should chase as a chaotic cloud/swarm, not as an evenly spaced ring.
- Ambient fauna should make the wormhole feel inhabited; nanites can briefly
  attack/eat fauna before resuming the chase.
- Close follow-camera impacts should be allowed to fly through the particle
  shower so the player feels the vessel's speed and danger at race-camera zoom.

## Acceptance Checks

- The Arcade card appears in Explore Games.
- Launching from Arcade loads `MinigameBulkFilaments`.
- Countdown starts, Squirrel spawns on the first filament, music plays, and an
  audio listener exists.
- Default looping game music/ambience does not layer over Dopamine.
- Filament waveform overlays appear once Dopamine starts and scroll against the
  vessel direction.
- A full run lasts roughly the song length and requires 20-30 transfers.
- Power crystals increase speed and create pickup audio/lightning.
- Lightning hazards occasionally reset speed if they intersect the vessel.
- Animated sprite overlays are visible on filaments, latch rings, tethers,
  crystals, nanites, roots, and fauna.
- Nanites read as a swarm/cloud rather than a circular formation.
- Transfer logs distinguish front lock, rear lock, miss, blocked rear, and expiry.
- Result screen appears only after the route completes or a real Bulk finish
  condition fires.

## Open Design Questions

- Final name for the energy latch rings.
- How punitive misses should feel before the nanite chase becomes unfair.
- Whether multiplayer lanes should use distinct filament chains in one shared
  wormhole or separate tunnel volumes.
- How much authored animation should replace runtime-generated placeholder rings
  and beams.
