# The Dolphin's omni-crystal morph

**The crystal does not shatter. It becomes the blast.**

A Dolphin that strikes an omni crystal fires Echo Obliteration: a conic blast down its nose that
sweeps the prisms in front of it (`DOLPHIN_CRYSTAL_SEEDING.md` §2, `AOEConicExplosion`). Until now
the crystal *also* burst into the shared spent-crystal husk spray, so the pickup read as two
unrelated events: something shattered, and separately a cone went off. It now reads as one — the
crystal's cage bursts open down-range, its plates land on the surface of the cone, and the cone
takes the surface over while the crystal dissolves off it.

This is the third **per-vessel omni-crystal retirement** (`VesselImpactorDataContainerSO.OmniCrystalRetirement`),
after the Squirrel's ring (`SQUIRREL_CRYSTAL_MORPH.md`) and the Scarab's ball (`SCARAB_CRYSTAL_MORPH.md`).
It shares everything but the target with them (`CrystalMorphRunner`), and the fleet's feel
(`Resources/CrystalMorphConfig`) is unchanged.

---

## 1. What the Dolphin's omni-crystal ability is (the analysis)

| what | where | what it means for the morph |
|---|---|---|
| the effect | `DolphinVesselExplosionByCrystalEffect` (`VesselExplosionByCrystalEffectSO`) in the Dolphin's `VesselCrystalEffects` | the thing the crystal should become |
| the blast | `AOEConicExplosion.prefab`: `PrimitiveCone` on `LaserGraph`, `ExplosionDuration` 2.7 s, `ExplosionDelay` 0 | a convex, closed 20-sided cone (38 triangles, read/write on) |
| its pose | container at the hull's position and rotation (`_spawnOffset` 0), opening along ship forward; static in the world for its whole life | the target's pose is exact from the frame it fires |
| its size | reach = 2400 × Space multiplier, base diameter = 400–2080 by skim energy × Space, capsule core from Charge | changes every pickup — so the target is read off the blast, never authored |
| its growth | `sin(t·π/2)` of full size over 2.7 s; opacity `1 − that` | there is no single "final" cone; the morph lands on the cone *as it will be at the hand-off* |
| its colours | `_Color` (body) and `_EdgeColor` (Fresnel rim), domain-tinted by `ThemeManager` | the colour pair the crystal converges on |
| the second effect | `DolphinVesselChangeResourceByCrystalEffect` (spends the energy) | untouched |

At the hand-off (0.85 × 0.44 s = 0.374 s) the cone is 21.6% grown: about 520 u long and 85–450 u
across at rest Space. So the crystal (a few units across) bursts out to something a hundred times
its size. That is the reading: the crystal's energy *is* the blast.

## 2. Why the mapping is the hull cast, read around the crystal's middle

The cone is convex, so this is the Scarab's mapping, not the Squirrel's census: every vertex of the
cage lands on the cone's surface and takes that facet's normal (`CrystalMorphMeshBuilder.ConvexHullTarget`).

**But the cone does not surround the crystal.** The Scarab's ball is forged at the crystal, so a
ray from the ball's centre through each crystal vertex fans the cage over the whole ball. The
Dolphin's cone opens from the hull's nose and its middle is ~260 u down-range: seen from there,
every point of a small crystal lies in nearly the same direction, and the plain cast piles the whole
cage onto one patch near the apex. Measured by `DolphinCrystalMorphGeometryTests`: the plain cast's
landing points span **under 10%** of the cone's length; cast around the crystal they span **over 75%**.

So the builder grew one overload, `TryBuild(source, target, sourceCentre, …)`: each vertex keeps
its **direction from the crystal's own middle** and is cast along it from the hull's centre. The
cage opens the way it faces — front plates onto the front of the blast, back plates back up toward
the nose. When `sourceCentre` is the hull's centre (the Scarab) it is the original cast, bit for bit
(tested).

The cast starts from the middle of the cone's **axis** (the mesh origin), not the solid's centroid:
the backward half of the cage then reaches further up the narrow nose before it runs out of
directions. The very tip still gets few plates; it is a few percent of the cone's surface.

## 3. What makes the hand-off seamless

1. **It draws the crystal's own renderers** (`CrystalMorphRunner.Adopt`), so frame 0 is the crystal.
2. **It ends where the cone is drawn when it reappears.** The cone's container never moves and its
   growth is deterministic, so `AOEConicExplosion.TryGetConeLocalToWorldAt(seconds)` reads the
   blast's own curve (delay, `sin` ease, `MaxScaleVector`) at `MorphSeconds` after the blast's
   Initialize — the same frame the morph is stamped. Retune the blast's scales, duration or Space
   reach and the morph follows; there is no second authority.
3. **Only the cone's photons wait.** `AOEConicExplosion.SetMorphStandIn` sets
   `Renderer.forceRenderingOff` and nothing else: the sweep, the trigger and the prism damage run
   from the frame of the pickup exactly as before. **The ability is unchanged** — only who draws the
   first 0.37 s of it.
4. **The colour pair converges** on the cone's `_Color`/`_EdgeColor` (read as dark/bright off the
   blast's own material clone).
5. **It starts in the pose the crystal HAD** (`CrystalForgeOrigin`), never its live transform.

**What is NOT exact**, said plainly:

- The cone is `LaserGraph` (transparent, scrolling texture, Fresnel rim, ~78% opaque at the hand-off);
  the body's formula blend converges on BlockGraph's `lerp(Dark, Bright, (1−N·V)⁴)`, which is close
  to a Fresnel rim but not the same, and the cone's texture has no counterpart. The dissolve tail
  (the last 15%) is what absorbs the difference. Expect a soft cross-over, not an invisible one.
- The hand-off fires on the first frame at or past `MorphSeconds`, and the cone's growth is
  integrated from `deltaTime`, so the cone can be up to one frame larger than the landed plates:
  about 0.9% of full size per 16 ms frame at that point of the curve.

## 4. How it runs

```
Dolphin hull touches an omni crystal
  ├─ OmniCrystalImpactor.ExecuteEffect                        [server]
  │     vessel carries an OmniCrystalRetirement → ExplodeParams.SuppressHusk = true
  │
  └─ VesselImpactor.ExecuteOmniCrystalImpact                  [owner → server → EVERY peer]
        ├─ OmniCrystalRetirement (runs FIRST)
        │     DolphinCrystalMorphByCrystalEffectSO
        │       └─ DolphinCrystalMorph.Begin(data.Origin, vessel)
        │             resolve crystal by id · adopt body + overlays · subscribe AOEConicExplosion.Initialized
        └─ VesselCrystalEffects
              DolphinVesselExplosionByCrystalEffect → ExplosionHelper.CreateExplosion
                └─ AOEConicExplosion.Initialize → Initialized (isolated)
                      └─ DolphinCrystalMorph.OnConeInitialized
                            filters: same vessel · apex within ringCaptureRadius of the collect
                            cone at t = MorphSeconds → ConvexHullTarget (body frame)
                            CrystalMorphMeshBuilder.TryBuild(cage, cone, crystal centre)
                            cone.SetMorphStandIn(true)
                            ONE stamp: _CrystalMorph = (PrismClock.Now, geometry s, stagger)
                └─ Detonate → the sweep starts (live, invisible)

  t = morphFraction · duration : cone.SetMorphStandIn(false) — the blast draws itself
  t = duration                 : body dissolved; morph destroyed (mesh destroyed with it)
```

No new message: the vessel's crystal effects are already broadcast, so every peer fires its own
cone and lands its own morph on it. One run per machine, as for the Squirrel.

**The morph does not fire the blast, and must not.** If the blast never comes (the effect's
0.15 s anti-spam swallows a second pickup), the morph holds the crystal for `targetGraceSeconds`,
then fades it out with a warning that names the sibling.

## 5. Files

| file | role |
|---|---|
| `R_VesselActions/DolphinCrystalMorph.cs` | the cone target: listen, read the cone at the hand-off, hold, reveal |
| `…/Vessel Crystal Effects/DolphinCrystalMorphByCrystalEffectSO.cs` + `_SO_Assets/…/DolphinCrystalMorphByCrystal.asset` | the Dolphin's retirement, wired in `DolphinImpactorDataContainer` |
| `Controller/Projectiles/AOEConicExplosion.cs` | `Initialized` (isolated), `ConeMesh`, `ApexPosition`, `TryGetConeLocalToWorldAt`, `TryGetConeColours`, `SetMorphStandIn` |
| `Utility/CrystalMorphMeshBuilder.cs` | the `sourceCentre` overload of the hull cast |
| `ScriptableObjects/CrystalMorphConfigSO.cs` | tooltips only: the grace and capture radius now cover the cone |
| `Tests/Editor/DolphinCrystalMorphGeometryTests.cs` | seven gates on the cone cast |
| `Tools/Build/crystal_morph_harness/` | runs it headlessly with the other morph suites |

## 6. Tuning

Nothing new. The window is the fleet's (`Resources/CrystalMorphConfig`); the shape is the blast's.
`ringCaptureRadius` (60 u) doubles as the cone's capture radius; the cone fires at the hull, which
is within a few units of the crystal it touched. Inspection: set `duration` to 9 on the asset.

## 7. Verification

**Not opened in Unity** (no editor in the session that built it). Proven offline:

| gate | proves | result |
|---|---|---|
| `crystal_morph_harness/run.sh` | the three morph suites + hull fusion, compiled and run against the shipped builder | 54/54. Negative control: casting around the cone's centre instead of the crystal's fails the direction and coverage gates |
| `unity_refcompile` player + editor | every changed C# file against real Unity references | 0 errors in changed files (21 pre-existing elsewhere: `UgsRequestPolicy`, Cinemachine reference gaps); a planted error in `DolphinCrystalMorph` is caught |
| `ship/gates.sh` | repo hygiene | every gate on a touched file OK; three fail on untouched assets (Meerkat, Regatta, Undertow), as on `bleeding-edge` |
| the cone mesh | `PrimitiveCone.asset` decoded: 41 vertices, 38 triangles, closed (0 open edges), read/write on | the test cone is built to the same layout |

### In-editor verification

1. **Any mode with a Dolphin and omni crystals** (freestyle, a Dolphin minigame). Fly through a crystal.
   Expect: **no husk spray**; the cage bursts forward onto a cone down the nose; the cone appears where
   the plates land and keeps growing; total ≈ 0.44 s before the crystal is gone. Set `duration` to 9 to
   watch it slowly (the cone will then appear late, at its 9 × 0.85 s size — that is expected).
2. **Prisms in front of the nose break from the first frame**, while the plates are still flying —
   the blast is unchanged.
3. **Echo Sight + a pickup**: the cone the morph lands on is the volume the sight highlighted.
4. **Two pickups inside 0.15 s**: the second has no blast; expect that crystal to hold, then fade,
   with the named warning.
5. **Every other hull is unchanged.** Squirrel ring and Scarab forge still morph; Sparrow/Manta spray.
6. **MPPM, two clients**: the morph plays on both peers, each on its own cone.
7. Trace with **FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph**.

## 8. Known limitations / follow-ups

- **Shader seam** (§3): a cone-specific formula blend in `OmniCrystalFresnelShader` would close it;
  not done here because the body shader is shared by every retirement.
- **Blocked blast**: a super-shielded enemy prism stops the cone mid-morph and destroys it. The
  plates land where the cone would have been and dissolve. Visual only.
- **The tip of the cone** gets few plates (§2).
- **How much the pilot sees** is a playtest question: the cone opens away from a chase camera, so
  the plates fly *away* from the viewer.
