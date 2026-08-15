# Vessel Jet FX — the two-layer law

**Status: shipped 2026-08-15.** Fleet-wide. Read this before touching any vessel's engine FX,
`VesselTrailCustomization`, or the trail-tint half of `ShipHelper.SetShipProperties`.

---

## 0. The law

> **Every vessel draws TWO cooperating jet FX layers, and BOTH wear the vessel's domain.**
>
> 1. **BEACON RIBBON** — one long, wide `TrailRenderer` streaming behind the hull. Tuned so
>    **other players can find the vessel**: it is the vessel's signature at ranges where the hull
>    itself is a few pixels.
> 2. **ENGINE PLUMES** — one short, bright FX per engine mount. Tuned as **feedback for the
>    pilot**, who sees their own engines from the chase camera.
>
> Neither substitutes for the other, because they are tuned for two different viewers. Both are
> `TrailRenderer`-bearing, which is what makes one tint pass repaint both.

The Squirrel authored both layers by hand and is the reference the fleet was brought up to. This
document is the record of what it actually had, what everyone else was missing, and the mechanism
that closed the gap.

---

## 1. What the Squirrel actually does (the reference, measured)

| Layer | Asset | `m_Time` | `widthMultiplier` | Instances | Role |
|---|---|---|---|---|---|
| Beacon ribbon | `_Prefabs/Spacevessels/Components/TrailEmpty.prefab` | **2.0 s** | **1.0** | 2 | findable at distance |
| Engine plume | `_Prefabs/Spacevessels/Components/Jet/jet.prefab` → `vfx_Projectile_02` | **0.5 s** | **0.3** | 4 | pilot feedback |

`jet.prefab` is a thin wrapper around `Effects Library/Froglet Stuff/Prefabs/vfx_Projectile_02.prefab`,
which is **3 ParticleSystems** (root, `Head`, `Particles`) **plus one `TrailRenderer`** (`Trail`).
The four instances sit on the Squirrel's engine bones — `bbone_BackEngine*` and
`bbone_FrontEngine*` — in two size pairs (outboard `1.2 × 0.6 × 0.13`, inboard `0.6 × 0.6 × 0.13`),
each on a host carrying `DriftJet`, which re-aims the plume along `VesselStatus.Course` while
drifting.

**The domain tint is the load-bearing part, and it was on the Squirrel alone.**
`VesselTrailCustomization` discovers every `TrailRenderer` under the vessel and rebuilds its
`colorGradient` from the domain's `TrailHighlightColor` / `TrailCoreColor`, preserving the authored
alpha curve. It is driven by the existing path:

```
Player.NetDomain changes
  → ShipHelper.SetShipProperties(themeManagerData, vessel)
      → IVessel.SetTrailColors(colorSet.TrailHighlightColor, colorSet.TrailCoreColor)
          → VesselController.SetTrailColors
              → VesselTrailCustomization.SetTrailColors   ← repaints BOTH layers at once
```

Because both layers are trails, the ribbon and the plumes are repainted by the same pass and can
never disagree about whose vessel they belong to.

> **Known and deliberate:** only the `TrailRenderer` half of a plume follows the domain. The three
> ParticleSystems inside `vfx_Projectile_02` carry an authored blue that does not change. That is
> exactly what the Squirrel has always shipped and what "excellent" was describing, so it was
> preserved rather than "fixed". Tinting the particles too is a separate, deliberate change.

---

## 2. The audit that motivated this (2026-08-15, from assets)

`VesselTrailCustomization` existed on **exactly one prefab in the project**.

| Vessel | Beacon ribbon | Engine plumes | Domain tint | `VesselController` |
|---|---|---|---|---|
| **Squirrel** | 2 | 4 | **YES** | yes |
| Dolphin | 1 | 0 | no | yes |
| Sparrow | 1 | 0 | no | yes |
| Rhino | 0 | 0 *(2 × `JetFX`, both `m_IsActive: 0`)* | no | yes |
| Manta | 0 | 0 | no | yes |
| Serpent | 0 | 0 | no | yes |
| Urchin | 0 | 0 | no | **no** |
| Grizzly | 0 | 0 | no | **no** |
| Falcon / Shrike / Termite | 0 | 0 | no | **no** |

So: the Dolphin and Sparrow had a ribbon that **never took their domain colour**; the Rhino's only
jet FX were three separate disabled experiments (`JetFX` ×2, `JetTest` carrying the orphaned
`ProceduralJetMesh`, and `LeftJetParticle`/`RightJetParticle`); and seven vessels had nothing at all.

`ParametricJetEffect.cs` and `ProceduralJetMesh.cs` are referenced by **no** prefab or scene — dead
code, left alone by this pass.

### Where each model's jets actually are

Taken from the FBX node tables, not guessed:

| Vessel | Model wired in the prefab | Engine mounts found |
|---|---|---|
| Dolphin | `Dolphin_Test.fbx` | `Engine Left.1/.2/.3`, `Engine Right.1/.2/.3` — **6** |
| Urchin | `Urchan_Test.fbx` | `JetTopLeft/Right`, `JetBottomLeft/Right` — **4** |
| Grizzly | `Vessel_Wedge_Scene (4).fbx` | `Ship_Wedge_Jet_UL/UR/BL/BR` — **4** |
| Rhino | `Rhino_Test.fbx` | `engine left`, `engine right` — **2** |
| Serpent | `SerpentExport.fbx` | `EngineBone` — **1** |
| Squirrel | `SquirrelVessel_CosmicShoresTest1.fbx` | 4 engine bones (authored by hand) |
| Sparrow | `SparrowModel1.fbx` | **none** — guns, shells, tails, wings only |
| Manta / Falcon / Shrike / Termite | `Manta_shapekey_rigged.fbx` | **none** — chassis and wing bones only |

---

## 3. The mechanism

`VesselJetFX` (on the vessel root, `[RequireComponent]` from `VesselStatus`) spawns whatever the
vessel is missing, driven by `VesselJetFXConfigSO` at `Resources/VesselJetFXConfig`.

**Bound in `VesselController.Initialize`**, alongside the occlusion-corridor and speed-tunnel laws
and for the same reason: `Initialize` is the one method every vessel calls on every spawn path, so
no vessel and no game mode can be authored without jets. Two properties of that binding are
deliberate:

- **It is NOT gated on `IsLocalPilot`.** The beacon exists to be seen by *other* players; a
  local-only binding would invert the feature.
- **It runs BEFORE `ShipHelper.SetShipProperties`.** That call is the vessel's first domain paint —
  spawn the trails after it and they keep their prefab colour until the player happens to change
  domain.

### Mounts are resolved by name

Exactly as `VesselAnimation.ResolvePart` already resolves animated parts. This is the only mechanism
that works uniformly across the fleet, because some vessels expose engines as real GameObjects
(Dolphin, Grizzly, Urchin, Rhino) and some only as FBX **bones** (Serpent). Hand-authoring onto a
bone is not reliably possible here: **the vessel FBX metas ship an empty `internalIDToNameTable`**,
so a bone has no stable name→fileID mapping to author against — while at runtime every bone is just
a named `Transform`.

A candidate must pass **both** filters:

1. **Name** — contains a `mountNameTokens` entry (`jet`, `engine`, `thruster`, `exhaust`) and no
   `mountExcludeTokens` entry (`case`, `shroud`, `hold`, `trim`, `frame`, `gun`, `fx`, `particle`,
   `test`). The exclusions cover two families: housings that *wrap* a nozzle (`Engine case Left.1`,
   `ShroudTopLeft`, `bbone_FrontEngineTrim.L`) and existing FX objects with engine-ish names
   (`JetFX`, `JetTest`, `LeftJetParticle`).
2. **Structure** — the transform must be a **rig bone** or draw an **enabled renderer on an active
   GameObject**. Name matching alone is not enough: the Sparrow's `ExhaustBarrage` is a
   `ToggleTranslationModeActionExecutor`, and would otherwise have collected a plume at the vessel
   origin, firing an engine out of the cockpit.

> ⚠️ **Exclusion tokens are SUBSTRING tests.** `"rig"` was tried during development for the
> Serpent's `EngineRig` and silently deleted **every right-side engine in the fleet** — `"right"`
> contains `"rig"` — leaving every vessel firing only its port engines, with nothing in the game
> saying so. `EngineRig` turned out to be an inactive Transform-only object the *structural* filter
> rejects for free. Check a new token against the audit's resolved mount list before adding it;
> `VesselJetFXMountResolutionTests` pins the whole real-name corpus for this reason.

### Authored FX always wins

Before spawning anything, `VesselJetFX` looks at the trails the vessel already has:

- **Beacon** — a vessel's beacon *is* its long trail; if it already draws one, it keeps it. (Squirrel,
  Dolphin, Sparrow.)
- **Plumes** — if any existing trail hangs under an engine-named ancestor, the vessel authors its
  own. (Squirrel.) This test uses the **loose** name match, deliberately ignoring the exclusion
  list: the Squirrel's authored jets hang off `bbone_BackEngineFrame.L` and
  `bbone_FrontEngineTrim.L`, which the strict test correctly rejects as cowlings. A false positive
  merely declines to add a layer the vessel probably has; a false negative would give the Squirrel a
  second full set of jets on top of its hand-tuned ones.

This is what makes the component safe to add anywhere, including the runtime `GetOrAdd` from
`VesselStatus.JetFX`.

### Sizing

Plumes are sized in **world** units and the mount's own `lossyScale` is divided out. That division is
load-bearing: the Dolphin's `Engine Left.1` sits at scale **0.01** and the Urchin's `JetTopLeft` at
**1.75**, so a plume given a raw local scale would be 100× too small on one vessel and nearly 2× too
big on another. Where the mount draws a nozzle mesh, the plume is sized from that renderer's bounds
(a jet should be about as wide as the engine it comes out of); where the mount is a bare bone, it
falls back to a fraction of the vessel's circumscribed hull radius, measured with the same
`PrismOcclusionCorridor.MeasureCircumscribedRadius` the occlusion law uses.

### Orientation, and why there is no per-frame cost

Each plume is parented to its mount and its **world rotation is set once** to the vessel's. Engine
bones rest at angles authored for the art (the Dolphin's cases at 26–169°), so inheriting the mount's
orientation would fire plumes sideways through the hull. Setting world rotation once means a bone
that later swings carries its plume with it — the gimbal reads as intentional and costs nothing.
`VesselJetFX` has **no `Update` and no `LateUpdate`**; its entire cost is instantiation at
`Initialize`.

### Derived mounts (models with no jet geometry)

The Manta family and the Sparrow expose no engine geometry at all. Rather than skip them, a
symmetric pair of mounts is derived at the rear of the hull from the measured hull radius, so the
vessel still reads as powered. **This is flagged for an art pass** — it is a reasonable default, not
an art direction. When those models gain named jet bones, the resolver picks them up with no code
change.

---

## 4. Files

| Role | File |
|---|---|
| Fleet tuning (single source) | `_Scripts/ScriptableObjects/VesselJetFXConfigSO.cs` → `Resources/VesselJetFXConfig.asset` |
| Spawner | `_Scripts/Controller/Vessel/VesselJetFX.cs` |
| Domain tint | `_Scripts/Controller/Vessel/VesselTrailCustomization.cs` |
| Binding | `_Scripts/Controller/Vessel/VesselController.cs` (`Initialize`, `ChangePlayer`, `SetTrailColors`) |
| Component contract | `_Scripts/Controller/Vessel/VesselStatus.cs` (`[RequireComponent]`, `JetFX`, `TrailCustomization`) |
| Fleet audit | `_Scripts/Editor/VesselJetFXAudit.cs` — **FrogletTools ▸ Vessels ▸ Audit Vessel Jet FX** |
| Tests | `_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs` |
| Beacon prefab | `_Prefabs/Spacevessels/Components/TrailEmpty.prefab` |
| Plume prefab | `_Prefabs/Spacevessels/Components/Jet/jet.prefab` |

### Change to `VesselTrailCustomization`

Discovery used to be cached at `Awake`. It is now **live** — resolved on each domain change — because
jets arrive *after* `Awake` (spawned during `VesselController.Initialize`), and a set captured at
`Awake` would silently omit every runtime jet and leave it wearing its prefab colour forever.
Re-discovery is cheap because it runs only on a domain change, never per frame. Authored alpha curves
are cached **per trail** in a dictionary rather than an index-parallel array, because the discovered
set grows at runtime and an index-parallel array mis-pairs curves the moment it does.

---

## 5. Tuning knobs

All in `Resources/VesselJetFXConfig.asset`. **First-pass values — tune against the Squirrel.**

| Knob | Default | Effect |
|---|---|---|
| `plumeScalePerMountSize` | `1.0` | Plume width as a multiple of the mount's nozzle bounds. Raise for fatter jets on vessels with visible engines. |
| `plumeScalePerHullRadius` | `0.11` | Fallback width for bone/derived mounts, as a fraction of hull radius. |
| `plumeLengthAspect` | `0.22` | Plume length ÷ width. The reference jet is a wide shallow flare, not a long cone. |
| `beaconOffsetPerHullRadius` | `-0.55` | How far behind the hull the ribbon sits. Squirrel authors −4.72 world units. |
| `maxEnginePlumes` | `6` | Per-vessel FX budget. The Dolphin's 6 mounts is the fleet's widest. |
| `derivedMountCount` | `2` | Plumes derived for a model with no jet geometry. |
| `derivedMountSpreadPerHullRadius` | `0.28` | Lateral separation of derived mounts. |

Worst case per vessel is the Dolphin: 6 plumes × 3 ParticleSystems = 18, against the Squirrel's
shipped 4 × 3 = 12. `maxEnginePlumes` is the valve if that proves too expensive on mobile.

---

## 6. In-editor verification

1. **FrogletTools ▸ Vessels ▸ Audit Vessel Jet FX** (asset-only). Expect every vessel to report a
   beacon and a plume set, and the mount lists in §2 to match. Urchin / Grizzly / Falcon / Shrike /
   Termite will report **`!! NO VesselController`** — see §7; that is a pre-existing condition this
   pass surfaced, not a regression.
2. Run `VesselJetFXMountResolutionTests` (edit mode). All green.
3. Enter **Menu_Main** freestyle. The Squirrel must look **exactly as before** — 4 engine jets, 2
   ribbons, no doubling. This is the single most important check: it proves the authored-FX
   detection fires.
4. Fly the **Dolphin** (`MinigameRampage`). Expect 6 engine plumes and its existing ribbon, both in
   the domain colour. Confirm it did **not** gain a second ribbon.
5. Fly the **Sparrow** (`MinigameDogFight`). Expect its existing ribbon tinted, plus 2 derived rear
   plumes. Judge whether the derived placement is acceptable or wants an art pass.
6. Fly the **Rhino** (`MinigameRibcage`) and the **Manta**. Rhino: 2 plumes on its engines. Manta: 2
   derived. Both ribbons tinted.
7. **Domain change, live** — use the Domain Changer toy in freestyle. Ribbon *and* plumes must
   repaint together on the same frame. (Plume particles staying blue is expected — §1.)
8. **MPPM, two clients.** Confirm each peer sees the other's vessel wearing the correct domain on
   both layers, and that a Cellular Duel ownership swap (`ChangePlayer`) repaints the inherited
   vessel.
9. Console must be clean. A `[VesselJetFX]` warning names its own fix.

---

## 7. Follow-ups

- 🔴 **Five vessels have no `VesselController`** — Urchin, Grizzly, Falcon, Shrike, Termite. They
  therefore never run `Initialize` and get no jet FX. This is not a jet-FX bug: without that
  component they cannot be spawned by the player/vessel pipeline at all. They gain jets for free the
  moment they gain a `VesselController`, with no change here.
- 🟡 **Derived mounts want an art pass** on the Manta family and the Sparrow (§3). The real fix is
  named jet bones in the models; the resolver already waits for them.
- 🟡 **Plume particles do not follow the domain** — only the trail half does (§1). Preserved from the
  Squirrel deliberately. Changing it means overriding `startColor` on three ParticleSystems per
  plume, which is a per-frame-free but per-instance write, and should be its own change.
- 🟢 **Dead code**: `ParametricJetEffect.cs` and `ProceduralJetMesh.cs` are referenced by nothing.
  The Rhino's `JetTest` object still holds a `ProceduralJetMesh` but is inactive. Safe to delete in a
  cleanup pass.
- 🟢 **Rhino's three disabled FX experiments** (`JetFX` ×2, `JetTest`, `LeftJetParticle` /
  `RightJetParticle`) are all `m_IsActive: 0` and were left in place — they cost nothing and removing
  them is an art call, not a code one.
