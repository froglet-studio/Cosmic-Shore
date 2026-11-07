# Plan: a `/world-look` skill (the 3D twin of `/ability-icon`)

Garrett, 2026-11-07, asked what competencies to train next after the vessel models and the ability icons.
`Docs/SKILL_TRAINING_PLAN.md` 3.2 ranked the look of things in the world second: one wormhole look took 26 rounds and
had eight looks removed (`WORMHOLE_SHADERS.md`), the Urchin's cradle and the crystal capture were "terrible", three
flat effects were rejected before the rule was LOCKED, the Orca's missiles "feel cheap". This is the plan, in the
icon plan's shape (`Docs/Icons/ABILITY_ICON_SKILL_PLAN.md`). Nothing in `Assets/` changes with it.

## 1. What exists today (measured 2026-11-07, `Tools/Build/look_kit/audit.py`)

560 prefabs under `Assets/_Prefabs`; **67 world effects** found (particle systems, trails, lines, meshes on unlit or
particle shaders, the munitions by name): 25 carry at least one flat signal, 30 are clean, 12 are unreadable offline
(every material slot empty, assigned at runtime). 189 world renderers on them (MeshRenderer 150, ParticleSystem 28,
TrailRenderer 8, SkinnedMeshRenderer 1, VisualEffect 1); particle modes Billboard 11, None 8, Mesh 5, Stretch 4.

The ten most-used shaders on world effects: URP/Particles/Unlit 27, Shader Graphs/RippleGraph 12,
Custom/SpreadFresnelShader 8, Shader Graphs/VesselGraph 7, GAPH Custom Shader/Shader_IntegradedEffect 5,
Custom/ViewAngleBasedColorBlendHDR 5, Shader Graphs/ChargeCrystal 3, SpikeGraph 3, Custom/CrystalChargeArcsShader 2,
LaserGraph 2. **Every project shader graph that draws a world thing sits on URP's Unlit sub-target and shades
itself**: "lit" in this fleet is the facing two-tone (a dark body, a bright square-on face), not Unity's Lit shader.
That is the language's one decision to confirm (section 4).

## 2. The audit: what "flat" and "cheap" are, specifically

Signals (72 on 25 effects): unlit-shader 24, unlit-by-content 13, additive-sheet 21, billboard-particles 9,
thin-mesh 5, screen-ring 0, no-normals 0. The "does not read" set, most signals first:

| Effect | Signals | What it is |
|---|---|---|
| `Spacevessels/Rhino.prefab` | 10 | five sword-blade TrailRenderers on `Custom/ViewAngleBasedColorBlendHDR` (unlit by content, additive) |
| `FX/FX_skimmer_ribbons.prefab` | 8 | three particle systems on URP/Particles/Unlit (billboard, additive) |
| `Projectile/GrizzlyBomb.prefab` | 8 | "Halo" and "Ring" quads on URP/Particles/Unlit, thin-mesh ratio 0.0, plus an additive BallTrail ribbon: the "ring that says burst" Garrett named on 2026-10-21 |
| `FX_skimmer_greenblock` 6, `FX_skimmer_lightning` 5, `Projectile/FalconProjectile` 4, `Projectile/Projectile` 4 | | unlit billboards and flat fills |
| `FxBubbles` 3, `Projectile/AxeBubble` 3, `ProjectileFX` 3, `FxBoost` 2, `JetFX` 2, `AOEScarabCavitation` 2 (two quads), `Environment/Crystal` 1, `CrystalCharge` 1 | | the same shapes |

The failure shapes, each of which the kit has to make structurally hard: **flat sheet** (an unlit quad or ribbon),
**billboard** (a sprite that always faces the camera), **screen ring** (a world fact drawn on the HUD), **unlit
additive** (a glow with no body), **out of scale** (an effect sized in units, not to its owner), **hairline at
distance** (what vanishes at the chase camera's follow distance), **generic** (a burst any game could own).

The yardsticks pass: the prism blocks (BlockGraph reads the normal), CrystalGraph, ChargeCrystal, OmniDepthPrism,
SpreadFresnel, RippleGraph, SpindleGraph (its normal read lives in a sub-graph).

Also found, to review, not look questions: `Rhino.prefab`'s jet particles point at a material guid no `.meta` owns;
`GrizzlySniperProjectile.prefab` and `fxriptidecone.prefab` point at meshes that resolve to nothing.

## 3. The skill

```
.claude/skills/world-look/
  SKILL.md                      the bar, read-before-touch, the pipeline, the round, rules relearned, hand-off
  references/
    LOOK_LANGUAGE.md            14 rules in the owners' words, each with a sheet or gate test (DRAFT)
    VOCABULARY.md               the primitives the game owns, each with its shipped reference and its state
    LESSONS.md                  17 entries, dated, the owner's words first
Tools/Build/look_kit/
  audit.py                      the audit, the baseline gate, --self-test with negative controls (built)
  audit_baseline.json           the 72 known offenders (owed work)
  effect.py, builders, verify.py, sheet   (next: section 7)
Docs/Shaders/WORLD_LOOK_AUDIT.md          the generated audit
```

## 4. The language (`references/LOOK_LANGUAGE.md`): the one decision to confirm

"Lit" means the fleet's facing two-tone: a dark body, a bright square-on face, the domain as one accent. The
flatness gate therefore measures depth and facet contrast, not whether Unity's Lit shader is bound. Consequences:
the line and ribbon drawings (the Rhino's blades, the skimmer ribbons, the ball trail) are owed a redraw as volumes,
and lenses (the wormhole, the Butterfly fold) need a clause of their own. Garrett says yes or no before the builders
are written.

## 5. The vocabulary (`references/VOCABULARY.md`)

The prism (the world's material), the danger prism, the shield octahedron, the crystal, the capture beat, the
prism's bloom and gnomon cut, the domain band, the ember, the jelly lens, the trail chain, the ribbon, the plume,
the lance, the ward, the petals, the silence perimeter, the blast, the ripple, the spark, the cloak, the cut, LIT,
the spindle, the veil: each with its shipped reference and whether it is lit or flat today; and the primitives the
language needs that nothing draws yet.

## 6. The gates

Today (`audit.py`): the seven signals against a baseline, with a negative control for each in `--self-test`
(an unlit world quad, an unlit-by-content graph, an additive sheet, a billboard, a screen ring in both forms, a thin
mesh, a UI canvas quad excluded, a hull mesh left out, a baseline pass/fail/missing).

Planned (`verify.py`, the icon kit's shape): lit (the two-tone measured on the sheet: facet contrast), depth (the
thinnest extent against the longest, 0.08 or more), scale (size to the owner, measured from the prefab), the phone
budget (triangles, overdraw estimate, variants, no per-spawn material), comfort (the wormhole's 14.4 numbers), each
with a negative control.

## 7. The pipeline (one command per effect, when the builders exist)

```
python3 Tools/Build/look_kit/author.py <effect>            # spec -> build -> verify -> sheet -> write
python3 Tools/Build/look_kit/author.py <effect> --check    # drift gate
python3 Tools/Build/look_kit/audit.py --check              # no new flat effect anywhere
```

The round is SKILL.md section 3: brief, three thumbnails at the game's distance, build, the sheet looked at, the
owner flies it, LESSONS before the next.

## 8. Round status

| Round | What | Status |
|---|---|---|
| R0 | the audit, the baseline gate, the language, the vocabulary, the lessons, the skill | **built 2026-11-07** (the language is a draft) |
| R1 | the Orca's missile and bomb ("feel cheap"; the GrizzlyBomb's halo and ring quads are the audit's third worst) | proposed |
| R2 | the crystal capture ("looks terrible") | proposed |
| R3 | the black hole's vortex ring (the flat ring of 2026-10-24) | proposed |
| R4 | a trail: the Rhino's sword-blade ribbons (the audit's worst, 10 signals) | proposed |

## 9. Decisions for Garrett

1. The language's reading of "lit" (section 4), and the lens clause.
2. The first three effects for the rounds (section 8's order, or another).
3. Whether the flatness gate reads prefabs (today's audit) or only the sheet; and whether Stretch-mode particles
   count as billboards (four effects use Stretch).
