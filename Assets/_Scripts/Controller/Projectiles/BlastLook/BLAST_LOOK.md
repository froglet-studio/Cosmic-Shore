# The blast look

Garrett's footage critique, 2026-11-13, weak link 2: "Explosions and hits are flat stickers. In Grizzly Time a hit is
a white four-point star, then a pink comic-book starburst. The bomb's blast is a flat orange spiked disc that is gone
in about two frames. After a Brood Rush blast the frame is empty." The vision there: **arm, release, aftermath**, with
a real light, a shock front, and lit debris that hangs for a second or two "so a slow-motion shot has something to
savour". Approved by Garrett the same day.

## What was wrong, measured from the code

| What the footage showed | What it was |
|---|---|
| The white four-point star on the Grizzly | The trigger bomb's **halo**: an additive glow billboard (`glow1_ADD`) turned to the camera every frame |
| The pink starburst | The LT bomb's spike crown (crimson-magenta) on `Custom/SpreadFresnelShader`: a fresnel fill shades every spike the same, so the crown read as a flat star; behind it the **ring** billboard (`ring_ADD`) |
| The orange spiked disc | The RT bomb (red-orange), the same two problems, close to the camera |
| The blast gone in two frames | The blast is Unity's sphere on the ripple shader growing to 100 u in 1.2 s. A Grizzly blows its bomb about 10 u behind itself, so the chase camera is **inside** the sphere within a couple of frames, and from inside the sphere draws nothing. Opacity `1 - growth` then fades it for anyone outside. |
| Nothing after | Nothing was ever drawn after the sweep: no debris of the bomb, no embers, no light |

## The look, in three beats

All of it is in one asset (`BlastLookSO`; the Grizzly's is `Assets/_SO_Assets/Effects/BlastLook/GrizzlyBlastLook.asset`),
drawn by one system (`BlastLookSystem`) for every blast whose `AOEExplosion.blastLook` names a look. Every distance is a
share of the blast's radius, so a tap blast and a full squeeze are one picture at two sizes. Every time is game time, so
slow motion slows it, and the debris is seeded from the blast's position, so a replay throws the same shards.

1. **Arm** (the bomb, `GrizzlyBombVisual`). The bomb is a faceted solid now: its core is the look's low-poly icosphere
   and its spikes keep their cones, both on `CosmicShore/BlastFacet` (dark body edge-on, the bomb's colour square-on).
   The breath that used to be a glow sprite is **heat on the facets**; the ping that used to be a ring sprite is a
   **faceted shell** (`CosmicShore/BlastShell`) racing out; armed, the facets flash white-hot, strobe hot, and the
   shell collapses onto the bomb, heating as it closes: the space pulled in. No billboard is left on the bomb.
2. **Release** (the frame the sweep starts, `AOEExplosion.ReleaseLook`).
   - A **white-hot core**: a faceted icosphere 7.5 % of the radius (1.5 to 8 u) flashes out in 0.05 s, holds 0.1 s,
     then cools and collapses over 0.65 s. White-hot is the shooter's colour leant 78 % toward white, so the flash
     still says whose it is.
   - A **faceted shock shell** on the sweep's own `sin(t pi/2)` curve, so the front is exactly where the damage is.
     Edge-on facets carry the light, so from outside it is a lit sphere with a faceted rim and from inside (the
     rider's chase camera) it all but vanishes instead of fogging the view. It draws **in place of** the ripple
     sphere: the ripple's photons are held (`HoldPhotonsForMorph`), its sweep, trigger and damage run unchanged.
   - A real **point light** at the core, range 1.1 x the radius, falling to a third every 0.22 s. Most of the fleet's
     shaders are unlit graphs and do not take it yet; it is there for whatever does (and for the hull lighting work).
3. **Aftermath**: 34 **casing shards** (chunky triangular bipyramids, sizes on a cube curve so most are small and a
   few big) and 80 **embers** (small, stretched along their flight while fast, a speck once slowed). Both leave under
   exponential **drag**, so they decelerate and HANG at up to 60 % (shards) and 95 % (embers) of the radius; shards
   tumble on; both cool from white-hot through the shooter's colour to the faceted two-tone, and shrink away (opaque:
   they never fade by alpha) over the last 35 % of a 2.2 to 3.6 s (shards) or 1.1 to 2.4 s (embers) life.

## Files

| File | What |
|---|---|
| `BlastLookSO.cs` | The look: every number above, and the three materials |
| `BlastLookSystem.cs` | Simulates and draws every live look; one GameObject made on demand in the active scene; one dynamic mesh per blast; `Graphics.RenderMesh` for every camera |
| `BlastLookMath.cs` | The curves, pure (seeding, drag flight, heat, shrink, the core's and shell's beats), held by `BlastLookTests` |
| `BlastLookMeshes.cs` | The icospheres (80 and 320 facets) and the shard shape |
| `_Graphics/Materials/Graphs/BlastFacet.shader` | The solid half: derivative facet normals, the two-tone, heat |
| `_Graphics/Materials/Graphs/BlastShell.shader` | The shock front: a faceted rim light, premultiplied |
| `_Graphics/Materials/BlastLook/*.mat` | Core, shards, shell; the bomb's facet (`_Spread` 1) and shell |
| `Tools/Build/author_blast_look_assets.py` | Writes the metas, the materials and the look asset; wires `AOEGrizzlyExplosion.prefab` (`--check`) |
| `Tools/Build/author_grizzly_bomb_assets.py` | Dresses the bomb (run after the one above) |

## Cost

Per blast, CPU: 114 pieces stepped and written into one mesh (570 vertices) a frame, for at most 3.6 s; at most 12
looks live (the oldest retires early). GPU: the core (80 triangles), the shell (320, transparent) and the debris
(684, opaque, no sorting), three draws, no shadow casters; one short point light. No GameObject per piece, no
collider, no allocation per frame.

## Reusing it for the blast family

Make a `BlastLookSO` (or reuse the Grizzly's) and set it on any plain-sphere `AOEExplosion` prefab's **Blast Look**.
A subclass with its own `ExplodeAsync` (cones, prism layers, flowers) calls `ReleaseLook(position)` at its release.

## Status

Built 2026-11-13 in a cloud session; compiled with `unity_refcompile`; the look itself is judged on film (before and
after clips from the same replay, filmed by the trailer director on Gbox) and by Garrett.
