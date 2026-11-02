# Prism effects: shader architecture and tuning map

Prisms are drawn by **Entities Graphics** (one shared material family, per-instance data). Every visual "thing that
happens to a prism" falls into one of two families. Keep new effects inside one of them.

## 1. Clock family (LOCKED, in-place, on the prism's own material)

State lives on the prism instance as stamped clocks; the shader graph animates from the stamp with no per-frame CPU work.
Spec: [`Docs/PRISM_ANIMATION.md`](../PRISM_ANIMATION.md). Do not refactor without reading it; it is guarded by
`PrismClockWiringTests`, `PrismClockWiringValidator` and `PrismClockSmokeTest`.

| Effect | CPU entry | Shader side |
|---|---|---|
| **Suction** (pulled toward a point) | `Prism.StampSuctionToward` → `PrismRenderService` stamp | `PrismGraphs/SuctionGraph.shadergraph` + `Graphs/PrismClockAnimation.hlsl` (wired by `Tools/Shaders/wire_prism_suction_clock.py`, `PrismClockGraphWirer`) |
| **Grow** (scale-in / growth) | `Prism` → `scaleAnimator.BeginGrowthAnimation`, `SetGrowthRate` | Transform scale (CPU animator), clock material shading |
| **Steal / domain change** | `Prism.ChangeTeam(domain)` (steal effect SOs: `*StealPrismEffectSO`) | Colour from `PrismLit.ColorSet.TryGetPrismKindColors(domain, kind, …)`; the cell re-files the prism under the new domain |
| **Deflection / danger / unstable** | `Prism.StampDeflection`, `UnstablePrismGraph.shadergraph` | Clock graphs |

These were audited, not changed, on `fulmen/prism-burn`: their visuals are byte-identical.

## 2. Burn family (overlay proxies, pooled, `Assets/_Graphics/Materials/PrismBurn/`)

A burning prism is hidden (`SetOwnerHidden`) and a **proxy cube** with the prism's matrix is drawn instead through
`Graphics.RenderMeshInstanced` from pooled arrays (`PrismBurnView`). Per-burn data is a `MaterialPropertyBlock`
array (`_Burn` = progress/seed/flags, `_Ash` = ash colour). No per-prism materials and no allocations per burn
(`PrismBurnManager.BurnState` is pooled; arrays grow only on a new high-water mark).

```
FulmenSystem / Grizzly claw / FulmenNetSync
        │ Ignite(..., seconds, PrismBurnSpread, PrismBurnStyle, generation)
        ▼
PrismBurnManager  (server-authoritative, pooled BurnState, decaying forest-fire spread, BurnOut on finish)
        │ Begin / Update / End
        ▼
PrismBurnView     (distance tiers, instanced draw, MPB arrays, ash colour cache)
        ├── PrismPaperBurn.shader  (style Paper: near curl / mid dissolve, used by the claw)
        └── PrismLaserBurn.shader  (style Laser: Fulmen, skin peels to red-hot edge → domain-blue ash flakes)
                 both #include PrismEffectCommon.hlsl  (Hash33/Hash31/ValueNoise/PrismIgnitionPoint/Bayer4/PrismDitherClip)
```

### Laser burn (Fulmen) look
Ignition point on the skin → a noisy front sweeps the prism. Ahead of it: untouched skin. On the front: a thin
**red-hot dissolve line** (`_EmberWidth`) with a heat tint band (`_HeatWidth`). Behind it: **ash in the domain colour**
(`_AshDarken`, `_AshSpeckle`) with sparse dying embers (`_AshEmbers`); the ash front follows (`_AshLag`) and breaks into
flakes (hash speckle clip) until nothing is left. No flame, no light, no bloom, no screen-space work, no `_Time`.
Shader model 3.0, single pass + DepthOnly, ~535 DXIL instructions (checked by `Tools/Shaders/verify_prism_burn.py`).

### Spread ("forest fire")
`PrismBurnSpread` (struct, serialised on `FulmenConfigSO.burnSpread`): every `Interval` s a burn rolls once on
neighbours within `Radius`, with chance `Chance · Decay^generation`, up to `MaxPerBurn` children, stopping at
`MaxGenerations`; children burn `SecondsScale^gen` as long. Fulmen default: 2 gens, 7 u, 0.45, ×0.5, 0.8 s, 3/burn,
×0.8 ⇒ at most 12 extra prisms per struck prism. `PrismBurnSpread.Unbounded(...)` reproduces the legacy rule the claw uses.

## Where to tweak

| What | Where |
|---|---|
| Fulmen burn duration (5 s), strike burn radius (10 u), spread | `Assets/_SO_Assets/Environment/FulmenConfig.asset` → `burnSeconds`, `burnRadius`, `burnSpread` |
| Spread code default | `PrismBurnSpread.FulmenDefault` (`Assets/_Scripts/Controller/Managers/PrismBurnSpread.cs`) |
| Laser look (ember colour/width, heat band, ash darkness/speckle/embers/lag, noise) | `Assets/_Graphics/Materials/PrismBurn/PrismLaserBurn.mat` |
| Laser draw distance, ash colour source (prism vs igniter domain), fallback ash colour | `Assets/Resources/PrismBurnFxConfig.asset` → `laserDistance`, `laserFarBand`, `ashColorSource`, `fallbackAshColor` |
| Paper burn (claw) look and tiers | `PrismPaperBurn.mat`, `PrismBurnFxConfig.asset` near/mid fields |
| Legacy spread (claw and other callers) | `PrismBurnManager` serialised `spreadRadius`, `spreadChance`, `spreadInterval` |
| Shared noise / dither helpers | `PrismEffectCommon.hlsl` (pure functions; change only with the verifier) |

## Adding a new prism effect
1. In-place, state-driven, lives as long as the prism → clock family; follow `PRISM_ANIMATION.md`.
2. Transient, replaces the prism look → burn family: add a `PrismBurnStyle`, a shader that includes
   `PrismEffectCommon.hlsl` and reads `_Burn`/`_Ash` from `UNITY_INSTANCING_BUFFER`, a material slot in
   `PrismBurnFxConfigSO`, and a tier in `PrismBurnView`. Extend `verify_prism_burn.py`.
3. Never create a material per prism; use the MPB arrays.

## Checks
`python3 Tools/Shaders/verify_prism_burn.py` (C++ parity of the HLSL fields, DXC compile on D3D11/Vulkan/Metal/GLES3,
instruction budgets) and EditMode `PrismBurnSpreadTests`.
