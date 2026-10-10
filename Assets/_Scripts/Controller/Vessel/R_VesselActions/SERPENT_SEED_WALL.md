# Serpent Seed Wall: the MASS ability (and its level-5 Lockdown)

Designed by Garrett on 2026-10-09; it fills the Serpent's last open design slot. Nothing here
has run in the Unity Editor yet (see "Editor checks" at the end).

## The deception it serves

The Serpent's stopped stance and its cloak are built to look like each other.

- **Stopping** (B / R, `ToggleStationaryModeAction`) makes the Serpent the seed: it stops laying
  trail, super-shields its latest prism and grows a wall from it. While
  stopped it is immune to elemental debuffs (`VesselElementalImmunity`,
  `WhileTranslationRestricted`), so it can bait and absorb the long-cooldown specialist abilities
  that remove super-shields.
- **Cloaking** (RT unscoped, `CloakSeedWallAction`) leaves the same super-shielded seed growing the
  same wall, plus an illusion of the hull frozen where it was, which reads as a Serpent that just
  stopped. The real Serpent flies on, cloaked. Everyone else sees neither the hull nor its trail;
  the pilot flying it sees a translucent hull (`pilotGhostAlpha` 0.3, a runtime clone of the ghost
  material, which is authored fully clear) and their trail dimmed (`pilotTrailShade` 0.35 through
  `Prism.SetColorShade`), so they know they are hidden without losing their own ship. When the
  cloak ends the illusion collapses into the seed over `illusionMorphSeconds` (0.6 s: travels to
  it, turns to its pose, shrinks to its size), and the seed's stellation blooms again as the
  hull and trail reappear. (Until 2026-10-10 the executor's `IsLocalUser` was hard-wired true, so
  the pilot got the invisible view, and the illusion vanished in place.)

So a seed with a Serpent next to it is either a stopped, invulnerable Serpent or a cloak decoy,
and the wall gives nothing away about which. A seed shot immediately (the Sniper Shot at Charge 5,
or an energized Rhino blade) grows nothing.

## The wall

The Serpent's trail is laid UNSHIELDED (Garrett, 2026-10-10; `Serpent.prefab` `VesselPrismController.shielded` 1 -> 0). Shielding is what an omni crystal gives the wall, so a trail that arrived shielded showed every brick as an oversized octahedron before any crystal was collected.

| | |
|---|---|
| Assembler | `SerpentWallAssembler` on the seed (`SeedWallActionSO.assemblerType: SerpentLattice`). The legacy `WallAssembler` is untouched and still serves flora. |
| Geometry | `SerpentWallLattice` (pure, tested by `SerpentWallLatticeTests`). |
| Bricks | Aspect 2: short side x long side 2x short x depth. At rest 3 x 6 x 0.5, the Serpent trail's own `BaseScale`. |
| Pattern | Checkerboard of orientations: site (i, j) is long-axis-up when i + j is even (the seed is (0, 0) and keeps its own up), long-axis-right when odd. A herringbone that is symmetric, with a square hole at every lattice cell, rather than a strict tiling. |
| Spacing | Pitch = 5 x short (15 at rest). A shielded prism draws as the octahedron that circumscribes its box at 3x the box's half-extents (`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE`), whose cross-section in the wall plane is a rhombus. Each brick's LONG-axis vertex points at its neighbour's SHORT-axis vertex, where they would meet at 4.5 x short, and `SerpentWallLattice.ShieldGap` adds half a short side of air (1.5 at rest). The gap also clears the super-shielded seed, whose stellation covers the whole 3x box face-on, at every twist up to `MaxTwistDegrees` (25). The hole is a square of side 4 x short. Unshielded, the bricks stand well apart: that gap is the room the shield needs. (The first cut spaced them by the BOX, 1.5 x short, and shielded bricks interpenetrated into one clump; the second made the vertices meet exactly, and in play shielded bricks read as touching.) |
| Growth | Every `siteClaimInterval` (0.15 s), for the seed's whole life, the first open site outward from the seed (a disc, by distance then angle) that has a loose prism within `recruitRadius` (40 x Mass multiplier) PLUS the site's distance from the seed pulls it into place and reshapes it; up to 6 open sites past the frontier are tried per claim. The loose mass is the trail running back from the seed, so a radius about the site alone would starve every site past the first ring. There is no brick limit (Garrett, 2026-10-10: "prism walls should continue growing indefinitely"): a site with nothing in reach is tried again later, and a brick that is shot out reopens its site. `SerpentWallLattice.GrowthOrder` has a stable prefix so the order extends forever without moving a standing brick. |
| Stealing | The Serpent's own prisms fly in at `ownPullSpeed` (20 u/s), an opponent's at `opponentPullSpeed` (6 u/s), and an opponent's prism is stolen when it lands. Super-shielded prisms, other seeds and creature bodies (`HealthPrism`) are never recruited. |
| Ends | Never on its own: leaving the stance or the cloak ending does not stop growth. The wall stops growing and answering crystals, and lets its bricks go as ordinary prisms, when the seed is destroyed or loses its super-shield. |

### MASS sets size and spacing, at placement

`SeedWallActionSO.massSizeMultiplierAtFull` (x2 at Mass 10, x1 at rest, floored at x0.5) scales
the brick, and the pitch is derived from the brick, so one number scales both. It is read on the
placing vessel's own executor (`SeedAssemblerActionExecutor.SnapshotWallShape`, because the SO is
shared by every Serpent) and **snapshotted into the wall**: the elemental state when the seed is
placed decides how that wall assembles, and a later change in the pilot's Mass does not touch it.

## Crystals re-shield every live wall

`SerpentWallShieldByCrystalEffectSO` sits in the Serpent's omni crystal effects AND all four
elemental crystal lists (since 2026-10-10: the first playtest collected a crystal and nothing
happened, and in most modes the crystals on offer are elemental). When the Serpent collects any
crystal:

1. The crystal morphs into beams: one `SniperBeam` tracer (the rifle's pooled, fading line, in the
   Serpent's domain colour) from the crystal to **every** live super-shielded seed that Serpent
   owns, flaring on the seed.
2. A shield ripples through each wall from the seed outward, one ring per `shieldRippleStep`
   (0.08 s). Bricks the Serpent no longer owns are skipped.

## Level 5: Lockdown (gated on `IsUpgradeActive(Mass)` when the crystal is collected)

When the Serpent collects a crystal while its Mass upgrade is active, every live wall it owns
locks up, however old the wall is. (It was decided at placement until 2026-10-10, so a wall laid
before the upgrade never locked; the replicated unlock bit is read at crystal time instead, so
every peer still agrees.)

1. Every brick turns `lockTwistDegrees` (15) clockwise as seen from the Serpent's seat, over
   `lockTwistSeconds` (0.4 s). Every lattice cell keeps four-fold symmetry, so it stays square,
   but the two mirror-image kinds of cell go opposite ways: one parity closes and the other opens.
   That is the checkerboard. A clockwise twist opens the ODD cells (i + j odd, counting the seed's
   lower-left cell as even). At 15 degrees the open hole grows from 12.0 to 13.1 (short = 3) and
   the closed one shrinks to 9.6; the opening peaks around 20 degrees. Neighbouring shields keep
   clear air at every twist up to 25 degrees (`SerpentWallLatticeTests`). Bricks and panels are
   turned only through `SerpentWallLattice.BrickRotation` / `PanelRotation`: the first build signed
   the twist separately in the assembler, turned the bricks counter-clockwise against the maths,
   and laid the panels in the cells that had closed.
2. Each open cell whose four corner bricks are standing is sealed with a flat DANGER panel
   (`lockPanelThickness` 0.15), the largest square that fits between the twisted bricks
   (`SerpentWallLattice.LargestClearSquare`), laid through `EventOnSpawnPrismAndReturn` in the
   Serpent's domain.
3. Later crystals re-shield the wall and replace any panel that was shot out. The twist happens
   once: "lock up" is a final state, not a ratchet. A locked wall keeps growing; new bricks land
   already twisted, and each open cell is sealed as soon as its four bricks stand.

## Prism model

The Serpent's trail prefab (`Prisms With Pools/Serpent Prism.prefab`) and the danger panels'
`Prism Interactive.prefab` were the last two pooled prisms on Unity's built-in cube (12
triangles). Both now use the fleet's 24-face prism mesh (`_Models/Testing/Prism.asset`, guid
8f4a329b…), so a wall's bricks and panels match every other vessel's prisms.

## Multiplayer

The wall is rebuilt on every peer from replicated inputs, the way the legacy wall and the Scarab
switch are: the cloak and stance presses round-trip through `R_VesselActionHandler`, and crystal
effects are broadcast. The Lockdown decision reads the replicated unlock bit at crystal time. The
cloak's pilot view is decided per machine (`IsLocalUser`). Recruitment reads
each peer's own prism set, so two peers can pull different loose prisms into the same site, as
the legacy wall could.

## Editor checks (none have run)

1. Stop (B / R) next to your own trail: a disc of 2:1 bricks forms around the seed with square
   holes, alternating up/right, and it KEEPS growing after you fly off, for as long as there is
   trail in reach. Shoot a brick out: the site refills.
2. Cloak near an opponent's trail: their prisms fly in slower and change colour as they land.
   While cloaked you see your hull translucent and your trail dimmed; a second player sees
   neither. When the cloak ends the illusion shrinks into the seed and the seed's spikes bloom.
3. Shoot the seed right after placing it: nothing grows.
4. Collect an omni crystal with two live walls: a beam to each seed, then a shield ripple; the
   shielded diamonds point tip to tip with a visible gap, and the seed's spikes touch nothing.
5. Raise Mass to 10, place a wall: bricks and spacing are twice the size.
6. Place a wall, raise Mass to 5, then collect any crystal: the bricks twist, half the holes close and
   the rest get red danger panels that fit without poking through. The panels go in the holes
   that OPENED (the bigger ones), never the ones that closed.
7. The HUD row shows the Seed Wall placeholder icon in the Mass slot.
8. The wall's bricks and panels show the 24-face prism, not a plain cube.
