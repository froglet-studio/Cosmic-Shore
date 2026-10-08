# The Squirrel's omni-crystal morph

**The crystal does not shatter. It becomes the ring.**

A Squirrel that flies through an omni crystal lays a ring of eight shielded prisms just ahead of
its nose. Until now the crystal *also* burst into the shared spent-crystal husk spray, so the
pickup read as two unrelated events: something exploded, and separately some prisms appeared. It
now reads as one — the crystal's cage opens, its plates fly outward, and they land as the eight
octahedra of the ring it just made.

This is a **per-vessel omni-crystal retirement**: the shared husk spray is the platform default,
and a hull may replace it with its own animation as part of its vessel package
(`VesselImpactorDataContainerSO.OmniCrystalRetirement`). The Squirrel is the first hull to fill
that slot; every other hull keeps the spray until it is given one. The Scarab's forge
(`SCARAB_CRYSTAL_MORPH.md`) is the other instance of the same animation, ending on a ball instead
of a ring, and the two share everything but the target (`CrystalMorphRunner`).

History: first explored on `cece/funny-edison-v3z7hq` against the old crystal (four coincident
`ShepardGraph` shells of the whole cage). Finalised here on the **one-layer omni** — a single body
on `OmniCrystalFresnelShader` with Mass's Shepard-tone triangles as overlays (`Docs/PALETTE.md`
§2.10) — which is what made the body morphable on a shader that already carried the morph path.

---

## 1. Why the mapping is exactly 1:1

The fact the whole animation is built on, and a measurement, not a design choice.

The omni body (`Assets/_Models/OmniCrystalExport1_8-21-25.fbx`, slot 0 of `Crystal.prefab`) is a
cage of **122 disjoint solids**:

| solid | count | faces each | total |
|---|---|---|---|
| box strut | 90 | 6 quads | 540 quads |
| triangular prism | 20 | 2 **triangles** + 3 quads | **40 triangles**, 60 quads |
| pentagonal prism | 12 | 2 **pentagons** + 5 quads | **24 pentagons**, 60 quads |
| | | | **64 non-quad faces**, 660 quads |

A shielded prism draws as a circumscribing octahedron (`OctahedronMeshGenerator`): **8 triangular
faces**. The Squirrel's ring is one ring of eight (`AOEShieldedRingSpawner` → `SpawnableRings`
`ringCount 1`, `prismsPerRing 8`).

> **40 + 24 = 64 = 8 × 8.** Every panel of the crystal becomes exactly one face of a shield, with
> nothing invented and nothing spare.

The 660 quads — struts and plate rims — are the leftovers. Each collapses to the centre of the
octahedron its own solid was assigned to and is absorbed; they are stamped phase 0, so they are
gone before the panels land.

Re-proven on the one-layer body by `python3 Tools/Build/measure_omni_crystal_morph.py`:
`960 control points, 724 polygons, 1432 triangles; solids 122; panels {3: 40, 5: 24}; PANEL CENSUS
OK; per octahedron [8, 8, 8, 8, 8, 8, 8, 8]`.

**Eight octahedra are not one convex hull**, which is why this is a census and not the Scarab's
hull cast. Both live in `CrystalMorphMeshBuilder`; the class doc names the two traps the census
is written around (a face is found STRUCTURALLY by shared indices — 60 of the cage's quads are
non-planar; a panel is ANCHORED to its face's three corners — a perimeter map left 83 of 336
corners off a target corner).

---

## 2. What is carried, and what makes the hand-off seamless

| carried | how | why it is not optional |
|---|---|---|
| geometry | TEXCOORD2, `CrystalMorph` | the shape — every panel lands corner-for-corner on its face |
| normals | TEXCOORD3, `CrystalMorphNormal` | the shading is `f(N·V)`; without it the cage's normals sit on the shield's faces |
| colour PAIR | `_DarkColor`/`_BrightColor` lerped onto the pair the laid prisms bound | the omni body and BlockGraph use the **same two names** for the same two roles |
| colour FORMULA | `CrystalMorphEase` weight, per face, in `OmniCrystalFresnelShader` | the body draws `lerp(Bright, Dark, (1+N·V)/2)`, BlockGraph draws `lerp(Dark, Bright, (1−N·V)⁴)` (FresnelColors → FresnelPower4, back-face branch included). The same pair through two formulas is still two surfaces |

1. **It draws the crystal's own renderers.** Body and overlays are copied off the live crystal —
   mesh, shared materials, property block — so frame 0 IS the crystal, tint and all.
2. **The overlays leave, they do not pop.** The falling Shepard-tone triangles (slots 1–3) and
   their rim (slot 4) are a different mesh and cannot fold, so they fade over
   `overlayFadeFraction` of the geometry window while the cage opens.
3. **It ends ON the real prisms.** Targets come from the ring `BoostRingBuilder` actually laid
   (`BoostRingBuilder.RingLaid`): each prism's own shield semi-axes
   (`PrismOctahedronShield.ShellSemiAxesLocal`) at its own final pose and `TargetScale`. Retune
   `SpawnableRings` and the animation follows; there is no second authority.
4. **The ring is live from frame 0; only its PHOTONS wait.** Colliders, shield state and the
   spatial index go final at the lay, so the ring is skimmable while the crystal is still landing
   on it. `Prism.SetOwnerHidden` holds drawing and nothing else — the existing photon-only hold,
   reused rather than a second one added.
5. **The ring's own arrival is REPLACED, not overlapped.** A boost prism blooms in on the clock at
   `k = 2.5/s` — ~61% grown at the 0.37 s hand-off. Revealing it then would show the crystal land
   on full-size shields and the ring jump to 61% and grow back. So at the hand-off, while the
   prisms are still hidden under the crystal, the morph settles their growth
   (`Prism.CompleteGrowthImmediately`, the API for exactly a covered window) and any shield bloom
   (`PrismOctahedronShield.Engage(instant: true)`), and only then reveals them. The morph IS the
   ring's arrival animation.
6. **It starts in the pose the crystal HAD.** `CrystalImpactData.Origin` (a `CrystalForgeOrigin`,
   the struct the Scarab's ball already replicates) carries the id and the `Crystal.CollectPose`
   captured at the collect; the crystal's transform is never read for pose, because by then it has
   usually respawned elsewhere with its rotation reset.

**The tail is a dissolve.** The body (`OmniCrystalFresnelShader`, screen-door `_Opacity`) and the
prism (`BlockGraph`) are still two shaders, and the object that wins has to be the real one — so
after the geometry half (`morphFraction`, the shader's window) the ring draws itself and the body
dissolves off it over the last 15%.

---

## 3. How it runs

```
Squirrel hull touches an omni crystal
  ├─ OmniCrystalImpactor.ExecuteEffect                        [server]
  │     vessel carries an OmniCrystalRetirement → ExplodeParams.SuppressHusk = true
  │     (sound + impact latch stay; only the spray is withheld, on every peer)
  │
  └─ VesselImpactor.ExecuteOmniCrystalImpact                  [owner → server → EVERY peer]
        ├─ OmniCrystalRetirement (runs FIRST)
        │     SquirrelCrystalMorphByCrystalEffectSO
        │       └─ SquirrelCrystalMorph.Begin(data.Origin, domain)
        │             resolve crystal by id · adopt body + overlays · subscribe RingLaid
        └─ VesselCrystalEffects
              SquirrelVesselExplosionByCrystalEffect → AOEShieldedRingSpawner
                └─ SpawnableRings → BoostRingBuilder.LayRing → RingLaid
                      └─ SquirrelCrystalMorph.OnRingLaid
                            filters: same domain · Shielded · within ringCaptureRadius
                            CrystalMorphMeshBuilder.TryBuild(cage, 8 octahedra)
                            prism.SetOwnerHidden(true) × 8
                            ONE stamp: _CrystalMorph = (PrismClock.Now, geometry s, stagger)

  t = morphFraction · duration : settle growth + shield bloom, SetOwnerHidden(false) — ring draws
  t = duration                 : body dissolved; morph destroyed (mesh destroyed with it)
```

**No new message.** The vessel's crystal effects are already broadcast, and the suppression rides
the explode payload the crystal manager already broadcasts (`NetworkExplodeParams` round-trips
every `ExplodeParams` field, by reflection test). `CrystalImpactData` grew by one
`CrystalForgeOrigin` (45 bytes on a crystal-collect RPC).

**The morph does not lay the ring, and must not.** One authority for the ring; if it never comes
(the explosion effect's 0.15 s anti-spam cooldown swallows a second pickup), the morph holds the
crystal still for `targetGraceSeconds`, then fades it out with a warning that names the sibling.

---

## 4. Files

| file | role |
|---|---|
| `_Graphics/Materials/Graphs/CrystalMorph.hlsl` | `CrystalMorphEase` (the one schedule) + `CrystalMorph` + `CrystalMorphNormal` |
| `_Graphics/Materials/Shaders/OmniCrystalFresnelShader.shader` | the body: morph path, colour-formula blend onto BlockGraph's, dissolve |
| `_Scripts/Utility/CrystalMorphMeshBuilder.cs` | the panel census (`OctahedronTarget`) beside the Scarab's hull cast; one shared `Emit` |
| `_Scripts/ScriptableObjects/CrystalMorphConfigSO.cs` + `Resources/CrystalMorphConfig.asset` | the FLEET's feel, one asset |
| `R_VesselActions/CrystalMorphRunner.cs` | adopt · stamp · colour · overlays · hand-off · dissolve — shared with the Scarab |
| `R_VesselActions/SquirrelCrystalMorph.cs` | the ring target: listen, measure the shields, hold, settle, reveal |
| `…/Abstract Effect Types/VesselOmniCrystalRetirementSO.cs` | the per-hull slot's type |
| `…/Vessel Crystal Effects/SquirrelCrystalMorphByCrystalEffectSO.cs` + `_SO_Assets/…/SquirrelCrystalMorphByCrystal.asset` | the Squirrel's retirement, wired in `SquirrelImpactorDataContainer` |
| `Controller/Environment/Spawning/BoostRingBuilder.cs` | `RingLaid` + `BoostRingLay`, listener isolated |
| `ImpactEffects/Impactors/{OmniCrystalImpactor,VesselImpactor,CrystalImpactData}.cs` | husk suppression, retirement-first dispatch, `Origin` carry |
| `Tests/Editor/CrystalMorphPanelCensusTests.cs` | twelve census gates (incl. the full 64-face ring) |
| `Tools/Build/measure_omni_crystal_morph.py` | the census against the shipped FBX |
| `Tools/Build/crystal_morph_harness/` | runs both shipped geometry suites headlessly (`run.sh`) |

---

## 5. Tuning

All on **`Resources/CrystalMorphConfig`** — shared with the Scarab. Never author a per-prefab
duration (`Docs/ECOSYSTEM.md` §31).

| field | ships at | what it does here |
|---|---|---|
| `duration` | 0.44 | whole animation, the platform's crystal-capture beat |
| `morphFraction` | 0.85 | geometry share; the rest is the dissolve |
| `stagger` | 0.35 | how much of the geometry window the phases spread over |
| `colourBlendFraction` | 0.8 | share of the geometry spent carrying the pair |
| `overlayFadeFraction` | 0.3 | share of the geometry over which the tone triangles leave |
| `fillerPhase` | 0 | struts and rims absorbed first |
| `panelPhaseStart` / `panelPhaseEnd` | 0.55 / 1 | each shield assembles face by face across this band |
| `targetGraceSeconds` | 1.5 | how long to wait for the ring |
| `ringCaptureRadius` | 60 | how far a ring may be from the collect and still be this pickup's |

Inspection: set `duration` to 9 (20×) on the asset; every other timing is a fraction of it.

---

## 6. Verification

**Not opened in Unity** (no editor in the session that built it). Proven offline:

| gate | proves | result |
|---|---|---|
| `measure_omni_crystal_morph.py` | the census on the shipped one-layer body | 64 = 8 × 8, 8 per shield |
| `crystal_morph_harness/run.sh` | both shipped suites COMPILED AND RUN against the shipped builder | 21/21; four injected defects (inward normal, no balance cap, shrunken panel, unwelded solids) each fail it |
| `verify_crystal_morph.py` | the shipped HLSL compiled and run: identity unstamped, exact ends, phase order, normal on schedule, **ease is the position's schedule and 0 unstamped** | OK; negative-controlled |
| glslang, SPIR-V | `OmniCrystalFresnelShader` vert + frag compile | OK; a planted error fails |
| `unity_refcompile` player + editor | every changed C# file against real Unity references | 0 project errors; a planted error is caught |
| `wire_crystal_morph.py --check` | ShepardGraph's splices still well-formed | OK |
| logging / conditional-compilation / using / abstract-member gates | repo hygiene | OK |

### In-editor verification

1. **Any mode with a Squirrel and omni crystals** (Skim Race, freestyle). Fly through a crystal.
   Expect: **no husk spray**; the cage opens and its plates fly to the ring ahead of the nose; the
   falling tone triangles fade as it opens; the ring appears exactly where the plates land, at full
   size, with no grow-in after it; total ≈ 0.44 s. Set `duration` to 9 to watch it slowly.
2. **Skim the ring mid-morph.** Fly straight through the ring the instant it forms: boost must
   register while the crystal is still landing (the ring is live mass from frame 0).
3. **Two pickups inside 0.15 s.** The second has no ring (explosion cooldown): expect that crystal
   to hold still, then fade, with the named warning — not a hang, not a pop.
4. **Every other hull is unchanged.** Collect with a Sparrow/Manta: husk spray as before.
5. **Scarab forge** (`SCARAB_CRYSTAL_MORPH.md` §8) — it now shares the runner, so re-run it: its
   colour now actually converges (it was writing `_DullCrystalColor` to a body that no longer has it).
6. **MPPM, two clients.** Collect on the client: the morph plays on both peers, starting where the
   crystal was on both, ending on each peer's own ring.
7. Trace with **FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph**.

---

## 7. Known limitations / follow-ups

- **A ring prism destroyed mid-morph** (an explosion, a joust) still has panels landing where it
  was; they dissolve with the rest. Visual only.
- **A recycled prism under a dead morph.** If a morph is destroyed mid-hold (scene teardown) its
  `OnDestroy` unhides the eight prisms. The boost pool serves only rings and nothing that draws from
  it hides a prism, so this is harmless; pool reuse clears the hold anyway.
- **DistanceSpreadAndColors' far tint and spread** are not carried by the formula blend: 0.3–2.5%
  of their range at pickup distance. The dissolve tail absorbs it.
- **How much of it the pilot sees is a playtest question.** The ring is laid 8 u ahead of the hull,
  and a Squirrel at speed crosses that in a fraction of the 0.44 s window, so the back half of the
  morph plays behind a chase camera. The panels-first-from-the-front reading may want
  `panelPhaseStart` lowered (faces land earlier) or the ring's `initialOffset` lengthened — tune it
  watching, not here.
- **Other hulls** can take the slot one at a time: a new `VesselOmniCrystalRetirementSO` subclass
  plus a `CrystalMorphRunner` subclass that supplies the target.
