# Serpent Seed Wall: the MASS ability (and its level-5 Lockdown)

Designed by Garrett on 2026-10-09; it fills the Serpent's last open design slot. Nothing here
has run in the Unity Editor yet (see "Editor checks" at the end).

## The deception it serves

The Serpent's stopped stance and its cloak are built to look like each other.

- **Stopping** (B / R, `ToggleStationaryModeAction`) makes the Serpent the seed: it stops laying
  trail, super-shields its latest prism and grows a wall from it until it flies again. While
  stopped it is immune to elemental debuffs (`VesselElementalImmunity`,
  `WhileTranslationRestricted`), so it can bait and absorb the long-cooldown specialist abilities
  that remove super-shields.
- **Cloaking** (RT unscoped, `CloakSeedWallAction`) leaves the same super-shielded seed growing the
  same wall, plus a ghost of the hull frozen where it was, which reads as a Serpent that just
  stopped. The real Serpent flies on, cloaked.

So a seed with a Serpent next to it is either a stopped, invulnerable Serpent or a cloak decoy,
and the wall gives nothing away about which. A seed shot immediately (the Sniper Shot at Charge 5,
or an energized Rhino blade) grows nothing.

## The wall

| | |
|---|---|
| Assembler | `SerpentWallAssembler` on the seed (`SeedWallActionSO.assemblerType: SerpentLattice`). The legacy `WallAssembler` is untouched and still serves flora. |
| Geometry | `SerpentWallLattice` (pure, tested by `SerpentWallLatticeTests`). |
| Bricks | Aspect 2: short side x long side 2x short x depth. At rest 3 x 6 x 0.5, the Serpent trail's own `BaseScale`. |
| Pattern | Checkerboard of orientations: site (i, j) is long-axis-up when i + j is even (the seed is (0, 0) and keeps its own up), long-axis-right when odd. A herringbone that is symmetric, with a square hole at every lattice cell, rather than a strict tiling. |
| Spacing | Pitch = 1.5 x short. A shielded prism draws as an octahedron, whose cross-section in the wall plane is a rhombus; at this pitch each brick's LONG-axis vertex touches its neighbour's SHORT-axis vertex. The hole is a square of side 1.118 x short. |
| Growth | Every `siteClaimInterval` (0.15 s) while bonding, the next site outward from the seed (a disc, by distance then angle) pulls the nearest loose prism within `recruitRadius` (40 x Mass multiplier) into place and reshapes it. Up to `bondingDepth` (50) bricks. A site with nothing near it is skipped, never waited on. |
| Stealing | The Serpent's own prisms fly in at `ownPullSpeed` (20 u/s), an opponent's at `opponentPullSpeed` (6 u/s), and an opponent's prism is stolen when it lands. Super-shielded prisms, other seeds and creature bodies (`HealthPrism`) are never recruited. |
| Ends | Growth stops when the cloak ends or the Serpent flies again. The wall stops answering crystals, and lets its bricks go as ordinary prisms, when the seed is destroyed or loses its super-shield. |

### MASS sets size and spacing, at placement

`SeedWallActionSO.massSizeMultiplierAtFull` (x2 at Mass 10, x1 at rest, floored at x0.5) scales
the brick, and the pitch is derived from the brick, so one number scales both. It is read on the
placing vessel's own executor (`SeedAssemblerActionExecutor.SnapshotWallShape`, because the SO is
shared by every Serpent) and **snapshotted into the wall**: the elemental state when the seed is
placed decides how that wall assembles, and a later change in the pilot's Mass does not touch it.

## Omni crystals re-shield every live wall

`SerpentWallShieldByCrystalEffectSO` sits in the Serpent's omni crystal effects. When the Serpent
collects an omni crystal:

1. The crystal morphs into beams: one `SniperBeam` tracer (the rifle's pooled, fading line, in the
   Serpent's domain colour) from the crystal to **every** live super-shielded seed that Serpent
   owns, flaring on the seed.
2. A shield ripples through each wall from the seed outward, one ring per `shieldRippleStep`
   (0.08 s). Bricks the Serpent no longer owns are skipped.

## Level 5: Lockdown (gated on `IsUpgradeActive(Mass)` at placement)

A wall seeded at Mass 5 or above locks up on its first omni crystal:

1. Every brick turns `lockTwistDegrees` (15) clockwise as seen from the Serpent's seat, over
   `lockTwistSeconds` (0.4 s). Every lattice cell keeps four-fold symmetry, so it stays square,
   but the two mirror-image kinds of cell go opposite ways: one parity closes and the other opens.
   That is the checkerboard. At 15 degrees the open hole grows from 3.35 to 3.65 (short = 3) and
   the closed one shrinks to 2.6; the opening peaks around 20 degrees.
2. Each open cell whose four corner bricks are standing is sealed with a flat DANGER panel
   (`lockPanelThickness` 0.15), the largest square that fits between the twisted bricks
   (`SerpentWallLattice.LargestClearSquare`), laid through `EventOnSpawnPrismAndReturn` in the
   Serpent's domain.
3. Later crystals re-shield the wall and replace any panel that was shot out. The twist happens
   once: "lock up" is a final state, not a ratchet.

## Multiplayer

The wall is rebuilt on every peer from replicated inputs, the way the legacy wall and the Scarab
switch are: the cloak and stance presses round-trip through `R_VesselActionHandler`, and crystal
effects are broadcast. The Lockdown decision reads the replicated unlock bit. Recruitment reads
each peer's own prism set, so two peers can pull different loose prisms into the same site, as
the legacy wall could.

## Editor checks (none have run)

1. Stop (B / R) next to your own trail: a disc of 2:1 bricks forms around the seed with square
   holes, alternating up/right, and stops growing when you fly off.
2. Cloak near an opponent's trail: their prisms fly in slower and change colour as they land.
3. Shoot the seed right after placing it: nothing grows.
4. Collect an omni crystal with two live walls: a beam to each seed, then a shield ripple; the
   shielded diamonds should touch tip to tip with no overlap.
5. Raise Mass to 10, place a wall: bricks and spacing are twice the size.
6. At Mass 5+, place a wall, collect an omni crystal: the bricks twist, half the holes close and
   the rest get red danger panels that fit without poking through.
7. The HUD row shows the Seed Wall placeholder icon in the Mass slot.
