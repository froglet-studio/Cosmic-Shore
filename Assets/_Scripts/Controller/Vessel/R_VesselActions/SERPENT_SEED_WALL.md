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
  stopped. The real Serpent flies on, cloaked. Everyone else sees neither the hull nor its trail.
  The pilot flying it sees their hull as a glowing outline in their domain colour
  (`CloakGhost.shader`: additive Fresnel rim, a faint fill and slow bands running nose to tail)
  and their trail as a ghost of itself, drawn through the prism dither at `pilotTrailAlpha` (0.4),
  so they know they are hidden without losing their own ship.
- **No cuts.** The cloak fades in over `fadeInSeconds` (0.4 s): the trail dissolves (to 0.4 for
  the pilot, to nothing for everyone else) and the pilot's hull settles from a flash
  (`transitionFlare`) into its outline. When the cloak ends, the illusion collapses into the seed
  over `illusionMorphSeconds` (0.6 s: travels to it, turns to its pose, shrinks to its size), then
  the reveal runs over `fadeOutSeconds` (0.7 s): the pilot's ghost brightens into a flash, the hull
  takes its own materials back under it (and reappears for everyone else) wrapped in a ghost glow
  that fades off it, the seed's stellation blooms, and the trail fills back in until it is solid.
  (Until 2026-10-10 `IsLocalUser` was hard-wired true, so the pilot got the invisible view. Until
  2026-10-13 the pilot's hull was a 30%-alpha dark-blue Lit material and the trail was darkened,
  which read as broken rather than cloaked, and everything snapped back in one frame.)

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
| Spacing | Pitch = 5 x short (15 at rest). A shielded prism draws as the octahedron that circumscribes its box at 3x the box's half-extents (`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE`), whose cross-section in the wall plane is a rhombus. Each brick's LONG-axis vertex points at its neighbour's SHORT-axis vertex, where they would meet at 4.5 x short, and `SerpentWallLattice.ShieldGap` adds half a short side of air (1.5 at rest). The gap also clears the super-shielded seed, whose stellation covers the whole 3x box face-on. The hole is a square of side 4.02 x short, tilted 26.6 degrees (see Lockdown). Unshielded, the bricks stand well apart: that gap is the room the shield needs. (The first cut spaced them by the BOX, 1.5 x short, and shielded bricks interpenetrated into one clump; the second made the vertices meet exactly, and in play shielded bricks read as touching.) |
| Growth | The wall STEALS FROM ITS EDGE (Garrett, 2026-10-13, restoring the legacy wall's behaviour): only an open site next to a brick (the seed included) can take a prism, and only one within the steal radius of that site. So the wall grows toward the mass beside it and creeps along a trail it is eating, not as a uniform disc. Every `stealSweepInterval` (0.3 s), for the seed's whole life, one sweep (`SerpentWallStealPlanner`) does a single spatial query, then up to `stealWavesPerSweep` (4) waves: each pairs every edge site with its nearest free prism in reach, grants nearest-first (one prism per site, one site per prism), and the new bricks open the next wave's edge, up to `maxStealsPerSweep` (48) per sweep. Per frame, only prisms still in flight are moved; standing bricks are checked once a sweep. There is no brick limit (Garrett, 2026-10-10): a brick that is shot out reopens its site as edge. With the Serpent stopped on its own trail, which runs back from the seed along the wall's normal, the edge only reaches the first prisms of it (about two rings at rest); after that the wall grows where loose mass passes its edge. |
| Steal radius | `stealRadius` (40) x the Mass multiplier (the whole wall scales with Mass) x the owner's live Space multiplier (`spaceStealRadiusMultiplierAtFull`, x1 at rest to x2 at Space 10, floored at x0.5). Space is the wall's reach. |
| Stealing | The Serpent's own prisms fly in at `ownPullSpeed` (20 u/s), an opponent's at `opponentPullSpeed` (6 u/s), and an opponent's prism is stolen when it lands. An opponent's SHIELDED prism is taken only while the Serpent's Space upgrade (Deep Focus) is active, read live each sweep; without it they are left alone. Super-shielded prisms, other seeds, another wall's bricks and creature bodies (`HealthPrism`) are never taken. |
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

1. Every square hole whose four corner bricks are standing is sealed with a flat DANGER panel
   (`lockPanelThickness` 0.15), laid through `EventOnSpawnPrismAndReturn` in the Serpent's domain.
2. Every panel is the same size (`SerpentWallLattice.CellPanel`). The hole's four sides are the
   four shield edges facing it, so the panel is tilted to match them: 26.6 degrees
   (`atan(1 / Aspect)`) in an even cell, the mirror angle in an odd one. It is shrunk by
   `lockPanelClearance` (0.25 x short, 0.75 at rest: a 10.6 panel in a 12.1 hole) so an even crack
   runs between the panel and every shielded brick around it; the wall reads as tiles with
   cracks. `SerpentWallLatticeTests` checks the crack on all four sides and that a brute-force
   search finds no bigger square.
3. Later crystals re-shield the wall and replace any panel that was shot out. A locked wall keeps
   growing, and each hole is sealed as soon as its fourth brick lands.

Until 2026-10-13 Lockdown first TWISTED every brick 15 degrees, which closed half the holes and
opened the other half, and panelled only the open half. Garrett dropped the twist for one panel
size in every hole.

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

1. Stop (B / R) next to your own trail: 2:1 bricks form around the seed with square holes,
   alternating up/right. Fly a trail past the wall's edge: the wall reaches out and eats along it,
   lopsided toward the mass rather than a circle, and keeps doing so after you fly off. Shoot a
   brick out: the site refills. Raise Space: the edge reaches further. Pass an opponent's SHIELDED
   trail: it is left alone without the Space upgrade and stolen with it.
2. Cloak near an opponent's trail: their prisms fly in slower and change colour as they land.
   The cloak fades in: your hull flashes and settles into a glowing outline in your domain
   colour, and your trail dissolves to a dithered ghost; a second player sees the trail dissolve
   to nothing and the hull gone. When the cloak ends the illusion shrinks into the seed, your
   outline brightens, the hull comes back under a glow that fades off, the seed's spikes bloom
   and the trail fills back in. Nothing snaps. Check the console for CloakGhost shader errors.
3. Shoot the seed right after placing it: nothing grows.
4. Collect an omni crystal with two live walls: a beam to each seed, then a shield ripple; the
   shielded diamonds point tip to tip with a visible gap, and the seed's spikes touch nothing.
5. Raise Mass to 10, place a wall: bricks and spacing are twice the size.
6. Place a wall, raise Mass to 5, then collect any crystal: nothing twists, and EVERY square hole
   gets a red danger panel of the same size, tilted to match the diamonds around it, with an even
   crack between the panel and each shielded brick.
7. The HUD row shows the Seed Wall placeholder icon in the Mass slot.
8. The wall's bricks and panels show the 24-face prism, not a plain cube.
