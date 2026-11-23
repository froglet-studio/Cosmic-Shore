# Serpent Seed Wall: the MASS ability (and its level-5 Lockdown)

Designed by Garrett on 2026-10-09; it fills the Serpent's last open design slot. Nothing here
has run in the Unity Editor yet (see "Editor checks" at the end).

## The deception it serves

The Serpent's stopped stance and its cloak both grow a wall from a seed with a Serpent beside it.

- **Stopping** (B / R, `ToggleStationaryModeAction`) makes the Serpent the seed: it stops laying
  trail, plain-shields its latest prism (`stanceShieldOnSeed`, never a super-shield) and grows a
  wall from it **for as long as it stays stopped**. When it flies again the wall stops stealing and
  answering crystals and stands as it is, exactly like a cloak wall whose super-shield was broken
  (see "The stance's wall"). While stopped it is immune to elemental debuffs
  (`VesselElementalImmunity`, `WhileTranslationRestricted`).
- **Cloaking** (RT unscoped, `CloakSeedWallAction`) leaves a SUPER-shielded seed (`shieldOnSeed`)
  growing the same wall, plus an illusion of the hull frozen where it was, which reads as a Serpent
  that just stopped. The real Serpent flies on, cloaked. Everyone else sees neither the hull nor its trail.
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

So a seed with a Serpent next to it is either a stopped, debuff-immune Serpent or a cloak decoy.
While the cloak runs, its seed is hidden with the rest of the trail under the illusion hull; once
the cloak ends, the seed itself tells the two apart to an opponent who looks closely, because
since 2026-10-13 only the cloak's seed carries the super-shield's stellation (Garrett: "that is only when it uses its cloak
ability"). A seed shot immediately (the Sniper Shot at Charge 5, or an energized Rhino blade)
grows nothing.

### The stance's wall

The stance seeds with `StartSeed(..., heldByStance: true)`. Its wall is HELD: `IsActive` is true
while the seed lives and the hold stands, with no super-shield needed. Leaving the stance (or the
turn ending, or the vessel going away) calls `ReleaseHeldWall`, which clears the hold. From then
on the wall is inactive by the same rule that ends a cloak wall whose super-shield is broken: no
sweeps, no crystal beam or ripple, no new Lockdown panels at the seed; the bricks stand, and those
already in flight still land. The cloak finishing while the stance is held does not end the
stance's wall, because the cloak's `StopBonding` is still deliberately a no-op. If the stance
stops on a cloak wall's seed (nothing laid since), the seed keeps its super-shield (a plain shield
never downgrades one) and the wall outlives the stance as a cloak wall.

## The wall

The Serpent's trail is laid UNSHIELDED (Garrett, 2026-10-10; `Serpent.prefab` `VesselPrismController.shielded` 1 -> 0). Shielding is what a crystal gives the wall, so a trail that arrived shielded showed every brick as an oversized octahedron before any crystal was collected.

| | |
|---|---|
| Assembler | `SerpentWallAssembler` on the seed (`SeedWallActionSO.assemblerType: SerpentLattice`). The legacy `WallAssembler` is untouched and still serves flora. |
| Geometry | `SerpentWallLattice` (pure, tested by `SerpentWallLatticeTests`). |
| Bricks | Aspect 2: short side x long side 2x short x depth. At rest 3 x 6 x 0.5, the Serpent trail's own `BaseScale`. |
| Plane | **Keel** (`wallPlane`, Garrett 2026-11-18, see "The wall's plane"): the wall stands ALONG the heading the seed was laid on, on the hull's up. Wall x is the heading, wall y the hull's up, the normal the hull's side, and the seed turns a quarter into it. `Across` (the wall square to the heading, the original) and `Wings` (flat along the heading, on the hull's right) remain as settings. |
| Pattern | Checkerboard of orientations: site (i, j) is long-axis-up when i + j is even (the seed is (0, 0) and keeps its own up), long-axis-right when odd. A herringbone that is symmetric, with a square hole at every lattice cell, rather than a strict tiling. |
| Spacing | Pitch = 5 x short (15 at rest). A shielded prism draws as the octahedron that circumscribes its box at 3x the box's half-extents (`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE`), whose cross-section in the wall plane is a rhombus. Each brick's LONG-axis vertex points at its neighbour's SHORT-axis vertex, where they would meet at 4.5 x short, and `SerpentWallLattice.ShieldGap` adds half a short side of air (1.5 at rest). The gap also clears the super-shielded seed, whose stellation covers the whole 3x box face-on. The hole is a square of side 4.02 x short, tilted 26.6 degrees (see Lockdown). Unshielded, the bricks stand well apart: that gap is the room the shield needs. (The first cut spaced them by the BOX, 1.5 x short, and shielded bricks interpenetrated into one clump; the second made the vertices meet exactly, and in play shielded bricks read as touching.) |
| Growth | The wall STEALS FROM ITS EDGE (Garrett, 2026-10-13, restoring the legacy wall's behaviour): only an open site next to a brick (the seed included) can take a prism, and only one within the steal radius of that site. So the wall grows toward the mass beside it and creeps along a trail it is eating, not as a uniform disc. Every `stealSweepInterval` (0.3 s), for as long as the wall is active (see Ends), one sweep (`SerpentWallStealPlanner`) does a single spatial query, then up to `stealWavesPerSweep` (4) waves: each pairs every edge site with its nearest free prism in reach, grants nearest-first (one prism per site, one site per prism), and the new bricks open the next wave's edge, up to `maxStealsPerSweep` (48) per sweep. Per frame, only prisms still in flight are moved; standing bricks are checked once a sweep. There is no brick limit (Garrett, 2026-10-10): a brick that is shot out reopens its site as edge. With the Keel plane a straight trail lies IN the wall, so the wall eats along it from the seed both ways (behind the seed at once; ahead of it once the cloak, if any, has ended). With the old Across plane the trail ran down the normal and the edge reached only its first prisms (about two rings at rest). |
| Steal radius | `stealRadius` (40) x the Mass multiplier (the whole wall scales with Mass) x the owner's live Space multiplier (`spaceStealRadiusMultiplierAtFull`, x1 at rest to x2 at Space 10, floored at x0.5). Space is the wall's reach. |
| Stealing | The Serpent's own prisms fly in at `ownPullSpeed` (20 u/s), an opponent's at `opponentPullSpeed` (6 u/s), and an opponent's prism is stolen when it lands. An opponent's SHIELDED prism is taken only while the Serpent's Space upgrade (Deep Focus) is active, read live each sweep; without it they are left alone. Super-shielded prisms, other seeds, another wall's bricks and creature bodies (`HealthPrism`) are never taken. |
| Refuel | The wall eating the Serpent's OWN trail is the only refill of its fuel pellets (Garrett, 2026-10-31): each of the owner's own trail prisms that lands is reported to its `ConsumeBoostActionExecutor`, one pellet per 1.5 s of building (`SERPENT_FUEL_PELLETS.md`, retuned from 2 s for Regatta pace on 2026-11-18). Flying out at cruise in the wall's plane pulls the trail in; with the Keel plane that is flying straight on, or pulling up or down. The tank fills only while the Serpent FLIES (`ConsumeBoostActionSO.refillWhileStopped` off): a stopped Serpent's stance wall eats the trail behind it for nothing. |
| Ends | A cloak wall: never on its own, the cloak ending does not stop growth; it stops growing and answering crystals when its seed loses its super-shield. A stance wall: when the Serpent flies again (see "The stance's wall"). Either kind also ends when the vessel that laid it is gone (despawn, disconnect): its Space level can no longer be read. An ended wall still reopens the sites of bricks that are shot out, so they do not stay claimed. Either kind lets its bricks go as ordinary prisms when the seed is destroyed. |

### The wall's plane (Garrett, 2026-11-18)

Garrett: "Give the Serpent a way to turn its wall 90 degrees, so it can drop a wall and keep flying without having to make
the 90 degree turn itself (compare how the TigerMoth works). Being constrained to a plane in order to grow its boost
charges is already challenge enough." Why: in Regatta the per-vessel starting Time boosts are going (they are handicaps),
and without its Time 10 the Serpent raced about 2x slower than the balanced five. The wall was laid in the seed prism's
own face, square to the heading, so the trail ran down its normal and fed it (and the pellets) only after a 90 degree turn.

| Choice | Picked | Options |
|---|---|---|
| The plane | **Keel**: along the heading, on the hull's up. Flying straight on feeds it, and so does a pure pitch: the Serpent's single stick pitches about the hull's right and banks whenever it yaws, so pitch is the one turn that stays exactly in a plane. | **Wings**: flat along the heading, on the hull's right; a flat turn feeds it, but the stick's bank into a yaw tips the Serpent out of it. **Across**: the original. All three stay a setting (`SeedWallActionSO.wallPlane`), one for the stance and the cloak alike. |
| The decoy | The seed turns a quarter with the wall and is still the centre brick (site 0,0, long axis on wall y); the illusion already "turns to its pose" as it collapses, so it now ends edge-on to the old heading. **While the cloak runs, no wall takes a trail prism laid under that cloak** (`CloakSeedWallActionExecutor.LaidUnderCloak`, `SerpentWallAssembler.HiddenUnderCloak`): a cloaked Serpent flying straight on lays its hidden trail in the wall's plane, and eating it would grow the decoy's wall FORWARD, at the real Serpent. Held back, a cloak wall grows only backward along the old trail, exactly as a stopped Serpent's does; once the cloak ends and the hull is in sight it eats the rest. | Let it eat everything at once: earlier refuel, but the decoy points at the real Serpent. |
| Stopping | **The tank fills only while the Serpent flies** (`refillWhileStopped` off): the turned stance wall eats the whole straight trail behind a stopped Serpent, which would otherwise make stopping a fuel stop. | On: stopping refuels. |

Elements are unchanged: Mass still sizes the wall, Space its reach and the shielded steal, Time the pellets, Charge the
scope, and the four L5s and the omni effect read as before.

### MASS sets size and spacing, at placement

`SeedWallActionSO.massSizeMultiplierAtFull` (x2 at Mass 10, x1 at rest, floored at x0.5) scales
the brick, and the pitch is derived from the brick, so one number scales both. It is read on the
placing vessel's own executor (`SeedAssemblerActionExecutor.SnapshotWallShape`, because the SO is
shared by every Serpent) and **snapshotted into the wall**: the elemental state when the seed is
placed decides how that wall assembles, and a later change in the pilot's Mass does not touch it.

## Crystals re-shield every active wall

`SerpentWallShieldByCrystalEffectSO` sits in the Serpent's omni crystal effects AND all four
elemental crystal lists (since 2026-10-10: the first playtest collected a crystal and nothing
happened, and in most modes the crystals on offer are elemental). When the Serpent collects any
crystal:

1. The crystal morphs into beams: one `SniperBeam` tracer (the rifle's pooled, fading line, in the
   Serpent's domain colour) from the crystal to the seed of **every** active wall that Serpent
   owns (each super-shielded cloak wall, and the stance's wall while it is stopped), flaring on
   the seed.
2. A shield ripples through each wall from the seed outward, one ring per `shieldRippleStep`
   (0.08 s). Bricks the Serpent no longer owns are skipped.

## Level 5: Lockdown (gated on `IsUpgradeActive(Mass)`, sealed as a hole's four bricks close)

**Recut 2026-10-29 (Garrett):** "lets have mass L5 put the exact same danger prisms in the exact same
spot, but instead of triggering on omnicrystal collection (lets leave the shielding to that) lets
have it trigger on all four surrounding wall bricks closing." While the Mass upgrade is active, a
hole is sealed the moment its fourth brick lands; holes already closed when the upgrade comes on are
sealed at the next steal sweep (`stealSweepInterval`). Crystals only shield. The replicated unlock
bit is read at each sweep, so every peer agrees and a wall laid before the upgrade still locks.
(Until 2026-10-29 a crystal collected under the upgrade locked the wall; until 2026-10-10 it was
decided at placement.)

1. Every square hole whose four corner bricks are standing is sealed with a flat DANGER panel
   (`lockPanelThickness` 0.15), laid through `EventOnSpawnPrismAndReturn` in the Serpent's domain.
2. Every panel is the same size (`SerpentWallLattice.CellPanel`). The hole's four sides are the
   four shield edges facing it, so the panel is tilted to match them: 26.6 degrees
   (`atan(1 / Aspect)`) in an even cell, the mirror angle in an odd one. It is shrunk by
   `lockPanelClearance` (0.25 x short, 0.75 at rest: a 10.6 panel in a 12.1 hole) so an even crack
   runs between the panel and every shielded brick around it; the wall reads as tiles with
   cracks. `SerpentWallLatticeTests` checks the crack on all four sides and that a brute-force
   search finds no bigger square.
3. A locked wall keeps growing, and each hole is sealed as soon as its fourth brick lands. A panel
   that was shot out comes back the next time a brick lands. Crystals re-shield the wall only.

Until 2026-10-13 Lockdown first TWISTED every brick 15 degrees, which closed half the holes and
opened the other half, and panelled only the open half. Garrett dropped the twist for one panel
size in every hole.

## Prism model

The Serpent's trail prefab (`Prisms With Pools/Serpent Prism.prefab`) and the danger panels'
`Prism Interactive.prefab` were the last two pooled prisms on Unity's built-in cube (12
triangles). Both now use the fleet's 24-face prism mesh (`_Models/Testing/Prism.asset`, guid
8f4a329b…), so a wall's bricks and panels match every other vessel's prisms.

## Multiplayer

The wall is rebuilt on each peer from replicated inputs, the way the legacy wall and the Scarab
switch are: the cloak and stance presses round-trip through `R_VesselActionHandler`, and crystal
effects are broadcast. **Gap (predates this branch):** `ToggleTranslationModeActionExecutor.Toggle`
returns early on any peer without authority over the vessel (it keeps the replicated
`n_IsTranslationRestricted` single-writer), so the STANCE's seed and wall are built only on the
owner and the server, and a third peer does not see that wall form (inferred from the gate; not
yet observed in a three-player session). The cloak's wall has no such gate. Follow-up: drive the stance's seed from
`stationaryModeChanged` on every peer rather than from the press. The Lockdown decision reads the replicated unlock bit at each steal sweep. The
cloak's pilot view is decided per machine (`IsLocalUser`). Recruitment reads
each peer's own prism set, so two peers can pull different loose prisms into the same site, as
the legacy wall could.

## Editor checks (none have run)

1. Lay a few passes of trail, then stop (B / R) beside them: 2:1 bricks form around the seed with
   square holes, alternating up/right, and the wall reaches out and eats along the trails it can
   reach, lopsided toward the mass rather than a circle. The seed is plain-shielded, with no
   stellation. Have a second player fly a trail past the edge: the wall eats along it too. Shoot a brick out: the site refills. Raise Space: the edge reaches further. Pass an
   opponent's SHIELDED trail: it is left alone without the Space upgrade and stolen with it. Fly
   off: the wall stops growing at once and stands; a crystal collected afterwards sends it no
   beam.
2. Cloak near an opponent's trail: the seed is super-shielded (stellated), and their prisms fly
   in slower and change colour as they land. The wall keeps growing after the cloak ends.
   The cloak fades in: your hull flashes and settles into a glowing outline in your domain
   colour, and your trail dissolves to a dithered ghost; a second player sees the trail dissolve
   to nothing and the hull gone. When the cloak ends the illusion shrinks into the seed, your
   outline brightens, the hull comes back under a glow that fades off, the seed's spikes bloom
   and the trail fills back in. Nothing snaps. Check the console for CloakGhost shader errors.
3. Shoot the seed right after placing it: nothing grows.
4. Collect an omni crystal with two cloak walls: a beam to each seed, then a shield ripple; the
   shielded diamonds point tip to tip with a visible gap, and the seed's spikes touch nothing.
5. Raise Mass to 10, place a wall: bricks and spacing are twice the size.
6. Cloak to place a wall, raise Mass to 5, then collect any crystal: nothing twists, and EVERY square hole
   gets a red danger panel of the same size, tilted to match the diamonds around it, with an even
   crack between the panel and each shielded brick.
7. The HUD row shows the Seed Wall placeholder icon in the Mass slot.
8. The wall's bricks and panels show the 24-face prism, not a plain cube.
