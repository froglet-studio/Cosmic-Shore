# The Scarab's crystal → ball morph

**The crystal does not shatter. It closes into the ball.**

A Scarab that flies its skimmer through an omni crystal turns that crystal into a ball, in place
and at rest (`SCARAB.md` §4.1). Until now the crystal *also* burst into the shared spent-crystal
husk spray, so a forge read as two unrelated events: something exploded, and separately a ball
appeared. It now reads as one — the cage folds inward, lands on the ball's faceted hull, and the
ball takes the surface out from under it.

This is the second instance of a **per-vessel omni-crystal retirement**: the shared husk spray is
the platform default, and a vessel may replace it with its own animation as part of its vessel
package. The Squirrel's ring morph (`cece/funny-edison-v3z7hq`) was the first and established the
shader half; this branch generalises the geometry half and adds the Scarab.

---

## 1. Why the mapping is a hull projection, not a face census

The Squirrel's morph rests on an exact coincidence: the omni crystal's cage has **40 triangular +
24 pentagonal = 64** non-quad panels, and eight shielded prisms show **8 × 8 = 64** octahedron
faces, so every panel becomes exactly one face with nothing invented and nothing spare.

**There is no such arithmetic here, and forcing one would be inventing geometry.** The ball is a
subdivided icosphere — `IcosphereMeshGenerator.Generate(subdivisions: 2, radius: sphereCol.radius,
flatShaded: true)` — which is `20 × 4² = ` **320 facets** against the cage's 64 panels. A 1:1
reading would leave 256 facets unclaimed.

A **convex hull** needs no census. Every source vertex slides along its own ray from the ball's
centre until it meets the hull, and takes that facet's normal:

| | Squirrel (octahedra) | Scarab (hull) |
|---|---|---|
| unit of assignment | one panel → one face | one vertex → the surface |
| requires a census | yes, exact | no |
| last frame | on the octahedra | on the ball's real facets |
| reading | the cage flies apart into plates | the cage folds closed |

What the hull mapping gives up is "every panel becomes a face". What it buys is that the last frame
lies **exactly on the target's real surface, wearing its real per-facet normals, for any convex
target** — which is the property the hand-off actually needs, and it generalises to the next vessel
without a new coincidence.

A hull's surface is a continuous function of direction, so two adjacent source vertices landing
either side of a crease land *on* the crease. The cage wraps the shape rather than tearing.

**Measured** (the shipped builder against the shipped icosphere, `CrystalMorphMeshBuilderTests`):
every vertex lands within `1.1e-7` of a facet plane, and wears that facet's normal to `1.2e-7`.
Landed radii sit in `[0.4874, 0.4997]` against a mesh radius of `0.5` and an inradius of `0.4910` —
i.e. on the polyhedron, not on the circumscribed sphere.

---

## 2. What makes it seamless

1. **It draws the crystal's own renderers.** Mesh, shared materials and MaterialPropertyBlock are
   copied off the live crystal, so the morph's first frame *is* the crystal — including the Shepard
   shells' band animation and `Crystal.ApplyColorSetTint`'s collectability colour. A rebuilt
   look-alike would pop on frame 0, which is the one frame that has to be free.

2. **It ends ON the real ball.** The target is read from the ball's own shipped hull mesh at its own
   radius, in the frame the ball draws it in. There is no second authority to drift from: retune
   `AstroLeagueSettingsSO.ballMeshSubdivisions` or the collider radius and the animation follows.

3. **The ball is live and only its PHOTONS wait.** `AstroLeagueBall.SetMorphStandIn` holds nothing
   but rendering — collider, rigidbody, strike path and the whole `ScarabBallForge.OnForged`
   adoption are final the instant the ball is minted, so a pilot arriving one frame later strikes a
   finished ball while the crystal is still landing on it. That is the clock-material law's own
   division (`Docs/PRISM_ANIMATION.md` §4) applied to a hand-off.

4. **It is parented to the ball.** A forged ball is at rest but strikeable from frame 0; if it is
   struck mid-morph the crystal travels with it instead of being left behind in space.

5. **It starts in the pose the crystal HAD, never the one it has.** See §5.

6. **THREE things are carried, not just position:**

   | carried | how | why it is not optional |
   |---|---|---|
   | geometry | TEXCOORD2, `CrystalMorph` | the shape |
   | **normals** | TEXCOORD3, `CrystalMorphNormal` | both shaders derive base colour from `(1 − N·V)⁴` through the **same** `FresnelColors` subgraph, so without it the morph arrives with the CAGE's normals sitting on the ball's facets — the right shape wearing the wrong surface |
   | colour | `_DullCrystalColor`/`_BrightCrystalColor` → the ball's `_DarkColor`/`_BrightColor`, **re-read every frame** off its live property block | the pair IS the shading, and the ball animates it continuously through its domain phase — a snapshot converges on the colour the ball wore a third of a second ago |

**The tail is a dissolve, not a cross-fade of two different-looking things.** It exists because the
two are drawn by different shaders in different queues, and no amount of matched colour changes
that: the ball is **opaque and z-writing** (`BlockGraph`, alpha-clip dither), the crystal is **four
alpha-blended, non-z-writing shells in the transparent queue** (`ShepardGraph`). So the object that
*wins* is the real one, and the crystal simply stops contributing to it — over the last 15% of the
window, from a state where the two already agree on surface, normals and colour.

The shader's window is the **geometry half alone**, so the last staggered solid has landed before
the tail begins. *A stagger is only free if it finishes first.*

---

## 3. How it runs

```
Scarab's skimmer touches an omni crystal            [server only]
  └─ ScarabBallForgeBySkimmerCrystalEffectSO.Execute
       ├─ ScarabBallForge.Request(...)              mints the ball, at rest, at the crystal
       ├─ ball.MarkForgedFromCrystal(crystal)       stamps n_ForgedFrom  ──► every peer
       └─ ConsumeCrystal(..., SuppressHusk = true)  the pickup sound and the impact latch stay;
                                                    only the spray is somebody else's job now

EVERY PEER (server, client replica, local mint alike)
  └─ AstroLeagueBall.OnNetworkSpawn / n_ForgedFrom.OnValueChanged
       └─ ScarabCrystalMorph.Begin(ball, origin)
            ├─ resolve the crystal by id through the cell nearest the collect pose
            ├─ adopt its four shells (mesh + shared materials + property block)
            ├─ read the ball's hull into this object's frame
            ├─ CrystalMorphMeshBuilder.TryBuild   (targets in TEXCOORD2, normals in TEXCOORD3)
            ├─ ball.SetMorphStandIn(true)         (photons only)
            └─ ONE stamp: _CrystalMorph = (PrismClock.Now, morphSeconds, stagger)

  t = 0.85 · duration : geometry, normals and colour have landed → the BALL takes the surface
  t = 1.00 · duration : the crystal's shells have dissolved off it; the morph destroys itself
```

**No new MESSAGE — one more variable on an object that already replicates.** This is not "no new
networking": `n_ForgedFrom` is a NetworkVariable this branch adds. What it avoids is a new RPC, a
new channel, or any per-peer coordination — the ball is already replicated, so it carries *where it
came from* and each peer starts its own morph off that. It uses the read-now-**and**-subscribe shape
`n_SizeScale` uses two blocks up, for the same reason: the stamp happens after
`NetworkObject.Spawn`, so the spawn payload cannot carry it and a late joiner sees only the
variable. Cost is 33 bytes of state per live ball, written once.

**The husk suppression rides the payload the manager already broadcasts** (`ExplodeParams
.SuppressHusk`), because the husk is spawned on every peer, so the suppression has to reach every
peer. It suppresses the spray and nothing else — the pickup sound still plays and the impact latch
still closes, because those belong to the pickup rather than to the spray.

---

## 4. Files

| file | role |
|---|---|
| `_Graphics/Materials/Graphs/CrystalMorph.hlsl` | the GPU half — `CrystalMorph` (position) + `CrystalMorphNormal`, one stamp, zero per-frame CPU |
| `_Graphics/Materials/Graphs/ShepardGraph.shadergraph` | both splices, at the END of the vertex position and normal chains |
| `_Scripts/Utility/CrystalMorphMeshBuilder.cs` | the geometry — unshared emit, hull landing, per-solid phase |
| `_Scripts/ScriptableObjects/CrystalMorphConfigSO.cs` + `Resources/CrystalMorphConfig.asset` | the FLEET's morph feel, one asset |
| `_Scripts/Controller/Vessel/R_VesselActions/ScarabCrystalMorph.cs` | the runner — adopt, stamp, converge, hand off, dissolve |
| `_Scripts/Controller/Vessel/R_VesselActions/CrystalForgeOrigin.cs` | which crystal, and the pose it was spent in |
| `_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs` | `n_ForgedFrom`, `MarkForgedFromCrystal`, `SetMorphStandIn`, the hull/colour surface |
| `…/Skimmer Crystal Effects/ScarabBallForgeBySkimmerCrystalEffectSO.cs` | marks the ball, suppresses the husk |
| `_Scripts/Controller/Environment/FlowField/Crystal.cs` | `CollectPose`/`CollectScale`, `ExplodeParams.SuppressHusk` |
| `_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs` | the nine geometry gates |
| `Tools/Shaders/wire_crystal_morph.py` / `verify_crystal_morph.py` | the splice, and the shipped HLSL compiled and run |

---

## 5. Tuning knobs

All of them live on **`Resources/CrystalMorphConfig`** (`CrystalMorphConfigSO`) and apply to every
vessel's crystal morph. **Never author a per-prefab duration** — that is exactly how the crystal
capture beat drifted to 1 s on two fauna and 3 s on eleven flora (`Docs/ECOSYSTEM.md` §31).

| field | ships at | what it does |
|---|---|---|
| `duration` | **0.44** | the whole animation. Matches the platform's crystal-capture beat, so a pickup reads the same length whichever vessel took it and whatever it became |
| `morphFraction` | 0.85 | how much of the window is GEOMETRY. The rest is the dissolve |
| `stagger` | 0.35 | how much of the window is spent staggering solids. 0 = the cage closes as one |
| `phaseNear` / `phaseFar` | 1 / 0 | phase of the nearest/furthest solid. As shipped the OUTERMOST struts leave first, so the collapse reads as a shell folding in. Swap them to invert it — no code change |
| `colourBlendFraction` | 0.8 | fraction of the geometry half spent carrying the crystal's colour pair onto the ball's. Must finish before the hand-off |

---

## 6. Two failures the platform already paid for, and what catches them here

**`Crystal.CollectPose` — a pickup is serviced by TWO unordered trigger callbacks in one physics
step**, and one of them ends in a respawn that re-poses the crystal *synchronously* on a host. So
anything reading `transform.position` for "where the crystal was collected" reads its NEXT home in
one of two arbitrary orders. Across the wire it is worse and not even order-dependent: collection
and respawn are independent chains, so a remote peer nearly always sees the moved crystal.

The answer is not to order the callbacks — that cannot be done from inside either of them, and any
arrangement that appeared to work would hold only until the next collider was added. It is to
**report the pose that was**, which is exact by construction: the only way the pose can be "new" at
read time is that `MoveToNewPos` ran this very frame, which is the move being serviced.

**`NetworkExplodeParams` drops any field you add to the payload — the husk shipped anyway.**
The suppression was added to `Crystal.ExplodeParams` and the crystal was still shattering on
screen. `NetworkCrystalManager` does not send `ExplodeParams`: it converts to a separate DTO
(`NetworkExplodeParams`) that carried only `Course`/`Speed`/`PlayerName`, and `ToExplodeParams()`
rebuilds the struct with every field it does not know about back at its **default**. So the flag
was `false` again by the time `Explode` ran — on every peer *including the host*, which runs the
ClientRpc too. It worked only on the no-network local path (`LocalCrystalManager` passes the
struct through untouched), which is the one path a solo editor test does not take.

*A DTO between the two ends of a message is a second place every field has to be added, and the
failure is silent in both directions.* Adding one to the payload type compiles, reads correctly,
and does nothing. `NetworkExplodeParamsTests` now round-trips **by reflection over
`ExplodeParams`' own fields**, so the next field is covered without anyone remembering to extend
the test — and a field of a type the test cannot populate fails by name rather than being skipped,
because a silent skip would restore exactly the blind spot it exists to close. It was
negative-controlled by reintroducing the bug (2 passed → 0 passed, 2 failed).

**Every rejection is named.** This animation's dependencies are invisible to it, and every way they
can fail produces the SAME symptom on screen — the ball appears and the crystal is gone. So each
exit says which one it was, and the whole path traces under `CSLogChannel.CrystalMorph`
(FrogletTools ▸ Toolbox ▸ Logging):

| refusal | means |
|---|---|
| "the ball has no hull mesh yet" | `Begin` ran before the ball's `Awake` |
| "no crystal with id N on this peer" | the cell had not finished initialising, or the crystal was destroyed |
| "exposed no drawable shell" | the crystal's models carry no MeshFilter + MeshRenderer pair |
| "shell N draws a different mesh" | the crystal is not four coincident copies of one cage |
| "not Read/Write enabled" | the importer setting, named with the fix |
| "no `_DarkColor`/`_BrightColor` pair" | the ball is not drawing with the prism fresnel material, so the hand-off will show a colour change |

Every one of them falls back to the ball's ordinary birth bloom. *A silent fallback is worse than
no fallback: it converts every distinct cause into the same symptom.*

---

## 7. Cost

One `Mesh` build per forge: the cage's ~2,900 distinct points are cast once each (deduped by weld)
against 320 facets, by best-fit facet with a barycentric verify — the exhaustive ray scan is the
fallback, not the path. Four draw calls while the morph is alive (one per crystal shell, all on one
mesh), for `duration` seconds. The animation itself is a single stamp; the only per-frame writes are
uniforms — the colour convergence and the tail opacity, a handful of property-block values per
shell.

**No collider impact and no gameplay change.** The ball is minted, sized, adopted and made
strikeable exactly as before; the morph adds no collider and touches no networked gameplay state.

---

## 8. Verification status

**🟡 SEEN ONCE, PARTIALLY.** A playtest on 2026-09-02 ran the forge and reported *"the previous
explosion was not removed so I was seeing the shattered debris still"* — which confirms the morph
itself plays, and is exactly how the husk-suppression defect in §6 was found. So:

- **Confirmed running:** the crystal→ball morph plays on a forge.
- **NOT yet seen:** the husk suppression actually working (the fix landed after that playtest), the
  hand-off reading as seamless, the strike-mid-morph case, and anything on a second peer.

What has been proven offline, and how:

| gate | proves | result |
|---|---|---|
| `wire_crystal_morph.py --check` | both splices present, acyclic, one feeder per input, slot widths match the HLSL, UV2/UV3 on the right channels, each morph last in its chain, one clock + one stamp | OK |
| `verify_crystal_morph.py` | compiles and RUNS the shipped HLSL: unstamped is identity, t=0 is the source exactly, t=1 is the target exactly at every phase, monotone, and the normal runs the position's schedule | OK to 1.2e-7 |
| Roslyn + faithful stubs | the four new C# files type-check against transcribed signatures | 0 errors; negative-controlled |
| Roslyn, no stubs | the four EDITED files: syntax, duplicate members, `System`/`UnityEngine` ambiguity | clean |
| `CrystalMorphMeshBuilderTests` | the nine geometry gates, COMPILED AND RUN against the shipped builder + shipped icosphere generator | 9/9; two injected defects each confirmed to fail it |
| `NetworkExplodeParamsTests` | every `ExplodeParams` field survives the RPC's DTO, by reflection | 2/2; negative-controlled against the shipped bug |
| `check_conditional_compilation.py` | no guard/namespace hazard | OK, 1855 files |

**What only a playtest can answer:** whether the collapse reads as a cage closing rather than as a
shrink; whether 0.44 s is the right length at forge distances; whether the outermost-first cascade
is the right direction (swap `phaseNear`/`phaseFar` to try the other); and whether the hand-off is
invisible or shows a step.

### In-editor verification

1. **Scarab Scramble, solo.** Fly the skimmer through a bright crystal. Expect: **no husk spray at
   all** — no shattered debris anywhere, on any peer;
   the crystal's cage folds inward and lands on the ball; the ball appears at full size (no separate
   bloom) as the cage dissolves off it. Total ≈ 0.44 s.
2. **Strike it mid-morph.** Forge a ball and hit it immediately. Expect: the ball launches normally
   and the half-finished crystal travels *with* it — never a crystal left behind at the forge point.
3. **The ball is live from frame 0.** Forge and immediately hit — the strike must register while the
   crystal is still visible.
4. **An ELEMENTAL crystal is untouched.** Fly the skimmer through one: no ball, no morph, the hull
   collects it normally (`Execute`'s `is not OmniCrystalImpactor` guard).
5. **The blast forge still sprays.** Juke-dash a crystal: it should still burst into husks and the
   ball launch away — deliberately unchanged (§9).
6. **MPPM, two clients.** Forge on the client. Expect the morph on BOTH peers, starting at the
   crystal's position on both — not at wherever that peer's crystal has respawned to.
7. **Trace it** with FrogletTools ▸ Toolbox ▸ Logging ▸ `CrystalMorph` if any of the above is wrong;
   every refusal names itself.

---

## 9. Follow-ups / known limitations

- **The BLAST forge keeps the husk, deliberately.** `ScarabBallForgeByExplosionEffectSO` spawns the
  ball at `_forwardClearance` (20 u) along the blast normal *and* launches it at speed, so "this
  crystal became this ball, in place" is not what happens there — the crystal is engulfed by a
  wavefront and a ball departs. Morphing it would have the cage chase a receding ball across 20 u.
  If that path ever wants a retirement it should be a different animation (a suction into the
  departing ball), not this one.
- **The hull path is not wired on this branch.** The Squirrel's branch carries
  `VesselOmniCrystalRetirementSO` + the `VesselImpactorDataContainerSO` slot +
  `OmniCrystalImpactor`'s skip, which is how a HULL-collected crystal gets a retirement. The Scarab
  needs none of it (its forge owns the crystal directly), so it was left there rather than landed
  here as an unused abstraction. `CrystalImpactData`'s pose/id carry belongs with it.
- **The morph re-reads the ball's colour every frame but snapshots the crystal's start colour.**
  That is deliberate — the start is a fixed point of the lerp — but it means a crystal whose tint
  changes mid-morph (it should not; it has been spent) would not follow.
- **A peer that cannot resolve the crystal shows the plain bloom.** Most likely on a late joiner
  whose cell is still initialising. Named, not silent, and self-correcting on the next forge.
- **`_bloomTimer` is cancelled by the stand-in, not by the forge.** A morph that refuses *after*
  `SetMorphStandIn(true)` cannot happen today (the stand-in is the last thing `Stamp` does), but if
  a future refusal is added below it, the ball would lose its bloom as well as its morph.
