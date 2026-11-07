## None
Every table row has the right column count and the three files are the only thing under `.claude/skills/world-look/` (untracked; no git commit, push, checkout or stash was run). Nothing more is needed; the work is complete.

**Summary (10 lines)**

1. Written: `/home/user/Cosmic-Shore/.claude/skills/world-look/references/LOOK_LANGUAGE.md` (87 lines), `VOCABULARY.md` (64), `LESSONS.md` (186). Nothing else touched; `who_works_where.py` shows no unmerged work on the folder (in-flight branches touch `ability-icon` only; Docs/Shaders is Yash's and Garrett's).
2. LOOK_LANGUAGE: 14 rules in the icon language's table shape, each with the owner's dated words cited file:line and a one-line sheet/gate test; the two LOCKED rules quoted verbatim (`SHADERS_AND_PERFORMANCE.md:9`, `:31`); the three distances named from the game (chase `|followOffset|` 6.7-250 u by vessel, the vision band's 1,200 u crossing / spindle tiers 184-1,000 u, the card); the phone tier from `PLATFORM_UNIFICATION.md:324-331, :559-567`; comfort from `WORMHOLE_SHADERS.md` §14.4.
3. The one decision to confirm: "lit" means the fleet's facing two-tone (dark body, bright square-on face, the domain as one accent), so the flatness gate measures depth and facet contrast, not Unity's Lit shader; consequences: the line/ribbon drawings are owed a redraw, and lenses need a clause.
4. Key finding behind that decision, measured from the assets: every world shader graph is a URP **Unlit** sub-target (`BlockGraph`, `CrystalGraph`, `VesselGraph`, `SpindleGraph`, ...); facets are told by normal/facing nodes (BlockGraph 3, CrystalGraph 4, VesselGraph 3) and by none in `WispGraph`, `LaserGraph`, `ConicGraph`, `ExplosionGraph`, `RippleGraph`.
5. "What this draft does not know" lists 9 open points, including that no owner's "yes" is written after any yardstick (the plan says "accepted"; the record says "shipped and not sent back"), the far-distance number (1,000 / 1,200 / 2,000), and who decides when Yash's wormhole and Garrett's rule disagree.
6. VOCABULARY: 23 primitives with shipped path, shader resolved through the material's `m_Shader` guid, and lit/faceted verdict; plus 8 "to draw" items (lance as a volume, the ward, a ring as a volume, the fleet trail for ribbon users, a shard spark, a blast front, the jet plume, a world petal only if wanted). `Tools/Build/look_kit/` exists with only `__init__.py` (a sibling is writing it), so rows say "to measure by `look_kit/audit.py`" where I did not read the look.
7. Flat offenders found: the sniper/lance beam (`SniperBeam.cs:201-202`: a `LineRenderer` on `Sprites/Default`, flare on URP Unlit; shared by the Serpent and the Shrike), the `TrailRenderer` ribbons on Manta/Sparrow/Thresher/GrizzlyBomb/SkyBurstProjectile/TadpoleSpindle, `ThresherChainFresnelShader` (a ribbon faking a tube), the conic explosions on `LaserGraph`, and the 19 particle prefabs (additive sheets). The ward has no world drawing at all (a state only); the PvP petals are HUD-only.
8. LESSONS: 29 numbered, dated entries in the owner's words first: the crystal capture (3), the Urchin cradle incl. the prism-morph "do not reach for the tuning dials first" rule (3), the ring explosion + "cheap remakes" (2), the switch-ring facets (1), the Orca missiles/bomb/silence (4), the prism death/steal rounds 14-15 (3), the vortex ring (1, a flat effect six days after the rule was locked), the wormhole's rounds (10), the Brittle Star smear (1), the Manta trail (1).
9. Citations were pinned by `grep -n` and corrected in a second pass (nine ranges fixed); the `|followOffset|` pipe is escaped so the table renders.
10. Not done / for the morning: nothing in these files is confirmed by Garrett, Yash or Shombith; the `OrcaCrewLit.mat` shader guid `933532a4fcc9baf4fa0491de14d08ed7` could not be resolved in this container (no URP package cache) and is marked "to confirm"; the SkyBurst/Manta ribbon materials were not resolved (awk on the prefab's TrailRenderer block returned nothing) and are marked "to measure".

**Shipped references found, with their shader names (material → shader, resolved by guid)**

| Reference | Shader |
|---|---|
| `Assets/_Graphics/Materials/BlockMaterials/PrismMaterial.mat`, `DangerBlockMaterial.mat`, `ShieldedBlockMaterial.mat`, `SuperShieldedBlockMaterial.mat`, `CloakedPrismMaterial.mat` | `BlockGraph.shadergraph` (Unlit sub-target, normal-shaded) |
| `BlockMaterials/ExplodingBlockMaterial.mat` | `ExplodingBlockGraph.shadergraph` (Unlit) |
| `CrystalMaterials/CrystalMaterial.mat` | `CrystalGraph.shadergraph` (Unlit, 4 normal nodes) |
| `CrystalMaterials/OmniCrystalBody.mat` | `Custom/OmniCrystalFresnelShader` (opaque, no light reads) |
| `CrystalMaterials/MassDepthPrisms.mat` (`author_mass_crystal_look.py`) | `Custom/OmniDepthPrismShader` |
| `CrystalMaterials/ExplodingCrystalMaterial.mat` (the capture husk) | `ExplodingCrystalGraph.shadergraph` (Unlit) |
| `CrystalMaterials/ChargeCrystalArcs.mat` | `Custom/CrystalChargeArcsShader` (Blend One One) |
| `Assets/_Graphics/Materials/EngineVesselMaterial.mat` (the ember; the Ark's jets) and the domain ShipMaterials | `VesselGraph.shadergraph` (Unlit, rim/face by facing) |
| `Assets/_Graphics/Materials/PrismCollisionBurstMaterial.mat` (the death bloom, gnomon cut) | `CosmicShore/PrismCollisionBurst` (opaque, light includes) |
| `Assets/_Graphics/Materials/ExplosionGraph.mat` (far fallback) | `ExplosionGraph.shadergraph` (Unlit, no normals) |
| `Assets/Resources/SolitonTunnel.mat` (the calm jelly / plain wormhole) | `CosmicShore/SolitonTunnel` (full-screen lens pass) |
| `Assets/Resources/SolitonTunnelAdditive.mat` (the reticle) | `CosmicShore/SolitonTunnelAdditive` (Blend One One) |
| `Assets/_Graphics/Materials/Wormhole.mat`, `WormholeSeamless.mat` | `CosmicShore/Wormhole` |
| `Assets/Resources/VortexRingLens.mat`, `BlackHoleLens.mat` | `CosmicShore/VortexRingLens`, `CosmicShore/BlackHoleLens` |
| The Squirrel-style trail (Manta, 11-07): prisms 20 × 0.75 × 5.5 on `PrismMaterial.mat` | `BlockGraph.shadergraph` |
| `Shader Graphs_TrailsGraph.mat` (ribbon) | `WispGraph.shadergraph` (Unlit, no normals) |
| `Shaders/TrailViewerMaterial.mat`; `Resources/BallTrail.mat` | `TrailViewerShader`; `CosmicShore/BallTrail` (Blend One One) |
| Orca missile plume: emissive copy of `Assets/Resources/OrcaCrewLit.mat` (`OrcaMissileVisual.cs:92, :110`) | package shader guid `933532a4fcc9baf4fa0491de14d08ed7` (URP Lit by guid, to confirm) |
| The lance/beam: `SniperBeam.cs` (Serpent + Shrike `ShrikeExecutor.cs:957`) | `Sprites/Default` line, `Universal Render Pipeline/Unlit` flare (flat offender) |
| `Assets/_Graphics/Materials/LaserMaterial.mat`; `BlueConicExplosionMaterial.mat` (+Red/Green/Yellow) | `LaserGraph.shadergraph` (Unlit, no normals) |
| `FulmenBolt` (bolt) | `CosmicShore/FulmenBolt` (Blend One One) |
| The Brittle Star ring blast band (`BrittleStarExecutor.cs:1362`) | the domain material → `VesselGraph.shadergraph`; smear `CosmicShore/BrittleStarRingSmear` |
| Silence perimeter (`Controller/FX/SilencePerimeter.cs`, `SilencePerimeterPass.cs`) | `CosmicShore/SilencePerimeter` (Blend One OneMinusSrcAlpha, faceted shards) |
| `Assets/Resources/StoatRipple.mat`; `RippleMaterial.mat` (+3 colours) | `CosmicShore/StoatRipple` (opaque); `RippleGraph.shadergraph` (Unlit) |
| `ShieldedBlueSpreadFresnelMaterial.mat` and 13 siblings; `DangerProjectileMaterial.mat` | `Custom/SpreadFresnelShader` (legacy hand HLSL) |
| `Assets/_Models/ShieldShader_Mat.mat` (no prefab/asset user found) | `ShieldShader.shadergraph` (Unlit) |
| `PrismSliceMaterial.mat`; `PrismBurn/PrismLaserBurn.mat`; `PrismBurn/PrismPaperBurnNear.mat` | `CosmicShore/PrismSlice`, `PrismLaserBurn`, `PrismPaperBurn` (opaque, light includes) |
| `Graphs/CloakGhost.mat` | `CosmicShore/CloakGhost` (Blend One One) |
| `ThresherChainMaterial.mat` (chain, Brittle Star cords) | `CosmicShore/ThresherChainFresnel` (opaque, ZWrite on, view-aligned ribbon) |
| `Game/Vessel/Animation/JetMaterial.mat`; `JetFX.prefab`, `JetVFXGraph.vfx` | `Custom/JetShader` (opaque, no light reads); particle / VFX |
| `Resources/SoftSpark.mat`; `fx_spark_*.mat`; `ring_ADD.mat`; the FX/Projectile particle prefabs (19) | `CosmicShore/SoftSpark` (Blend One One); third-party sprite shaders |
| The ward (`ResourceSystem.IsImmuneTo`, Dolphin Drift Ward) | no world drawing (to draw) |
| The PvP petals (`ElementalBarsView`) | HUD only (UI) |

## None
Built, self-tested and run on the real tree. Everything below is from the final run (`--self-test` OK; the audit; `--baseline`; `--check` passes with 0 new right after the baseline; `--scenes` adds nothing: no scene-placed effect exists).

## Files (all absolute, all under what I own)
- `/home/user/Cosmic-Shore/Tools/Build/look_kit/__init__.py` (new)
- `/home/user/Cosmic-Shore/Tools/Build/look_kit/audit.py` (new, 1.4k lines, stdlib only, READER tool; `--json`, `--check`, `--baseline`, `--scenes`, `--self-test`, `--root`; exit 0/1/2/3)
- `/home/user/Cosmic-Shore/Tools/Build/look_kit/audit_baseline.json` (written by `--baseline`: 72 known offenders)
- `/home/user/Cosmic-Shore/Docs/Shaders/WORLD_LOOK_AUDIT.md` (generated, 284 lines)
- Note: the parent's wip checkpoints (`471a4ff9df`, `7696ea3a3c`) already carry these files; `git status` on them is clean. I ran no git write.

## Counts (Assets/_Prefabs, 560 prefabs; runtime 0.8 s)
- Effects found: **67** (25 with at least one flat signal, 30 clean, 12 unreadable: every material slot empty, assigned at runtime)
- World renderers on them: 189 (MeshRenderer 150, ParticleSystem 28, TrailRenderer 8, SkinnedMeshRenderer 1, ModelInstance 1, VisualEffect 1); particle modes: Billboard 11, None 8, Mesh 5, Stretch 4; 105 empty material slots
- Signals: **72**: unlit-shader 24, unlit-by-content 13, additive-sheet 21, billboard-particles 9, thin-mesh 5, screen-ring 0, no-normals 0
- Renderers excluded as UI (under a screen-space canvas): 0

## Ten most-used shaders on world effects
URP/Particles/Unlit 27; Shader Graphs/RippleGraph 12; Custom/SpreadFresnelShader 8; Shader Graphs/VesselGraph 7; GAPH Custom Shader/Shader_IntegradedEffect 5; Custom/ViewAngleBasedColorBlendHDR 5; Shader Graphs/ChargeCrystal 3; Shader Graphs/SpikeGraph 3; Custom/CrystalChargeArcsShader 2; Shader Graphs/LaserGraph 2.

## The "does not read" set (most signals first)
1. `Spacevessels/Rhino.prefab` 10: five sword-blade TrailRenderers on `Custom/ViewAngleBasedColorBlendHDR` (unlit-by-content + additive-sheet)
2. `Environment/Materials/FX/FX_skimmer_ribbons.prefab` 8: three particle systems on URP/Particles/Unlit (billboard + additive)
3. `Projectile/GrizzlyBomb.prefab` 8: "Halo" and "Ring" **quads** on URP/Particles/Unlit (unlit, additive-sheet, thin-mesh: ratio 0.0) plus the BallTrail additive ribbon — exactly the "ring that says burst" shape the plan cites
4. `FX_skimmer_greenblock.prefab` 6, `FX_skimmer_lightning.prefab` 5, `Projectile/FalconProjectile.prefab` 4, `Projectile/Projectile.prefab` 4 (ProjectileGraph is a flat fill + an unlit billboard burst), `FxBubbles` 3, `Projectile/AxeBubble` 3 (ForceFieldGraph, SnowGraph, built-in Unlit/Transparent), `ProjectileFX` 3, `FxBoost` 2, `JetFX` 2, `AOEScarabCavitation` 2 (two quads, thin-mesh), `Environment/Crystal.prefab` 1 and `CrystalCharge.prefab` 1 (CrystalChargeArcsShader: additive, no lighting code)
- Shaders that are unlit by content: `CosmicShore/BallTrail`, `Custom/CrystalChargeArcsShader`, `Custom/ViewAngleBasedColorBlendHDR`, `Shader Graphs/ExplosionGraph`, `ForceFieldGraph`, `ProjectileGraph`, `SnowGraph`.
- Yardsticks pass: the prism blocks (`Trails/*`, BlockGraph reads Normal Vector), CrystalGraph, ChargeCrystal, OmniDepthPrism, SpreadFresnel, RippleGraph, SpindleGraph (its normal read lives in SpreadSubGraph, found by sub-graph recursion).

## What the heuristics could not decide (43 lines in the doc, folded per prefab)
- 105 empty material slots (material assigned at runtime, e.g. 90 DerbyBlock clones in `Projectile/Ellipsoid1.prefab`, all `AOE*Spawner` projectiles, the four `fx_*Crackle` particle slots): 12 effects are unreadable offline.
- 17 renderers draw a mesh that lives in an FBX (Trails/*, Urchin spikes, crystals): thin-mesh / no-normals skipped.
- `Rhino.prefab` LeftJetParticle/RightJetParticle: material guid `e823cd5b5d27c0f4b8256e7c12ee3e6d` is owned by no .meta under Assets (a dangling reference; never in git history either) — a real defect to review, not a look question.
- `GrizzlySniperProjectile.prefab` (SparrowProjectile) and `fxriptidecone.prefab`: mesh guids `2341dea8…` / `a7272371…` resolve to nothing — dangling mesh references.
- `SkyBurstProjectile.prefab` MissileVisual: its material is embedded in `Assets/_Models/Sparrow Missile.fbx` (the importer's), shader unreadable.
- `VfxArcLightning.prefab`: a VFX Graph; its outputs are inside the .vfx.
- 4 particle renderers in Stretch mode (FX_skimmer_lightning, FX_skimmer_ribbons, FxBoost, JetFX): the task names 0/2/3 for billboard-particles, so Stretch raises nothing; question for the parent whether a stretched billboard should count.
- `VesselTail.prefab` TrailRenderer uses built-in material Default-Line (fileID 10306): shader name not certain (marked `?`).

## Decisions I made beyond the spec (flag for review)
- Added a seventh signal, `unlit-by-content`: a Shader Graph on the Unlit sub-target with no Normal/Fresnel/Light node (custom-function HLSL and sub graphs are read too), or a .shader with no lighting code. Needed because every project graph in this tree sits on the Unlit sub-target and shades itself; the name test alone would call the prism graphs flat and the fills lit.
- The finder's word list carries the task's words plus kin (projectile, missile, bomb, burst, spark, crackle, laser, boost, jet, lightning, ripple, shockwave, halo, glow, flare), so the Orca/Grizzly munitions are found even without a particle. Both lists are constants at the top of `audit.py`.
- Screen-space canvases with m_RenderMode 1 (Camera) are excluded alongside 0. `screen-ring` is detectable only as a world renderer on the UI layer or a ring/radius/reticle-named world renderer inside a screen-space canvas; the tree has 0 LineRenderers, so it fired nowhere (the self-test proves both forms).
- A prefab found only by a particle/trail/unlit material lists only those renderers (a vessel's hull is not an effect); one found by name lists every world renderer.
- Self-test negative controls: unlit-shader, unlit-by-content (graph, .shader, sub graph lit vs flat), additive-sheet, billboard-particles (lit billboard passes; Particles/Lit found but not unlit), screen-ring (both forms), thin-mesh (lit quad is thin only), no-normals, UI canvas quad excluded, "Starling" not a ring, hull mesh left out, dangling and FBX-embedded materials, baseline pass/fail/missing (exit 3), report renders every rule.
