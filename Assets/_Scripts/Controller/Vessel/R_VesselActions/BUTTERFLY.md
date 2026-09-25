# The Butterfly

> *"It captures what we lost when we moved the Manta from a graceful soaring vessel to a speeding
> bomber. Someone who flies slow from far away with a wide piano-key trail. They get to enjoy
> making beautiful curvy surfaces through the hypersea. While everyone else is fighting they are
> meditating. Hand this to your friend who wants to relax, or play it when you need a break."*

`VesselClassType.Butterfly = 13`. A **two-thumb** hull — the dual-stick complement to the Serpent —
that cruises at **55 u/s** and turns at **45°/s**, lays a **wide slab of piano keys** behind it, and
carries no gun of any kind. It is the slowest vessel in the fleet and that is the design, not a
handicap to be tuned away.

## 1. What it is for

Every other vessel's loop is *do a thing to someone*. The Butterfly's loop is *go somewhere and
leave a surface behind*. Three consequences shape the whole design:

- **Its output is 2D prismscape.** A trail is the 1-dimensional case; the Butterfly is the hull
  that makes the 2-dimensional one — which the Urchin's `BlockscapeFollower` already knows how to
  roll across and the food web already knows how to graze. It builds the thing other vessels ride.
- **It fights by SOARING, not by shooting.** Two of its four abilities are passive and happen
  because you flew somewhere. There is no button that hurts anybody.
- **It is legible from a long way off.** The camera sits at **120 units** (the fleet's second
  longest after the Serpent's 250), so the wingspan and the beat carry the read, and every
  animation amplitude is authored against that distance rather than against a mirror.

### Its mode

**`Waystation(58)`** — the Butterfly-only migration race, and the only arcade card this hull flies.
It is cut against the Fold's single degree of freedom (the heading you leave on, after
`BUTTERFLY_FOLD.md` "One reach, everywhere"): clusters of rings you weave, laid a fold apart, each
ending in an exit gate that faces the next cluster. Nothing about the vessel was changed for it —
the mode needed one platform counter (`VesselTransformer.TeleportCount`, so a teleport threads no
ring) and one query (`R_VesselActionHandler.TryGetBoundAction<T>`, so an autopilot can time a hold
off the ability's own asset). See `_Scripts/Controller/Arcade/WAYSTATION.md`.

## 2. The hull

Procedural, like the Scarab's — there is no Butterfly model in the project, and the three unwired
`*_shapekey_with_animations` rigs that might have stood in carry element blend shapes that move
**one vertex by zero** on two of the three (`Docs/VESSEL_CONSTRUCTION.md` §4), so borrowing one
would turn the morph audit green while the hull morphed by nothing.

| Piece | Where |
|---|---|
| Pure geometry (a function of 19 authored floats + 5 integers) | `ButterflyHullForm.cs` |
| Scene side — meshes, children, materials, morph push | `ButterflyHullBuilder.cs` |
| Wingbeat, spread and fold poses, morph timing | `ButterflyAnimation.cs` |
| Compile-and-RUN gate over the shipped form | `Tools/Build/butterfly_hull_harness/` |

**Five parts, four of them wings.** A butterfly has four wings and the forewing/hindwing pair is
most of what separates it from a bird or a manta at this range. Each wing is its own part with its
own root hinge, because the beat rotates about that hinge and a wing welded into the body cannot
beat. Measured at the shipped settings: **809 verts, 2,464 tris, 21.35 u span × 11.55 u long**
(aspect 1.85).

### 2.1 What the elements do to the shape

The four morphs are `Generate` run at perturbed settings, so "level 7 Mass and level 3 Space" is the
base build plus a weighted sum of per-vertex deltas. Topology is a function of the INTEGER settings
only; `BakeMorphSet` asserts that rather than trusting it.

| Element | The shape it makes | Max vertex travel |
|---|---|---|
| Charge | Deeper lobes, sharper tip flare, more camber — a wing that looks like it sheds | 1.34 u |
| Mass | Bigger everywhere — span, chord, hindwing, body | 3.71 u |
| Space | LONG and THIN — a soaring aspect ratio, deliberately a different change from Mass's uniform growth | 6.25 u |
| Time | Body drawn out, wings raked back — a swift's silhouette, a shape about to leave | 3.33 u |

### 2.2 The beat

`beatAmplitude` **52°** at a standstill falling to **16°** in a glide, `beatHz` 1.15 → 1.7, with the
hindwings lagging **0.18 beats** (≈65°). All three are authored at fleet scale: the Rhino's 80°
wings are the calibration and 14–26° is invisible at a chase camera (the `/vessel` skill's rule 24,
which this hull is the most exposed to in the fleet).

> **The beat writes `localRotation` directly rather than through
> `VesselAnimation.RotatePartFromRest`, and that is load-bearing.** That helper lerps at the base's
> `lerpAmount` (authored **2**), a first-order lag with a ~0.5 s time constant. A 1.15 Hz beat
> through it emerges at `1/sqrt(1 + (2πfτ)²)` ≈ **27%** of its amplitude and ~75° late — so an
> authored 52° would have rendered as ~14°, straight into the band the fleet already knows reads as
> *"the ship feels dead"*. The lag exists to smooth a STICK. The beat is already a smooth continuous
> function of time and has nothing to gain from being filtered; every other input to the wing pose
> (eased stick, `MoveTowards` blends) is continuous too.

## 3. The four elements

The map asset (`Assets/Resources/ElementalAbilityMaps/Butterfly.asset`) is the design record.

| Element | Ability | Input | Level 5 |
|---|---|---|---|
| **Charge** | **Scale Dust** — the wings debuff pilots and wither creature hearts they pass through | passive | **Monarch** — the dust bites twice as deep |
| **Mass** | **Spread Wings** — hold to widen the wake; costs energy | RT (`RightStickAction`) | **Mural** — the spread is free |
| **Space** | **Wingreach** — opposing mass passing through the wings dissolves | passive | **Broadwing** — the far-field wings join in |
| **Time** | **Fold** — hold to stop, place a ghost anywhere in the cell, release to be there | LT (`LeftStickAction`) | **Far Fold** — the open-space fold reaches twice as far |

### 3.1 Charge — Scale Dust

Passive. The wings shed dust onto whatever **living** thing passes through them: an opposing pilot
takes an all-element decaying debuff, and a creature's heart withers through its normal death path
(mass conserved, continuity honoured, crystal dropped exactly as starvation would).

**There is deliberately no speed gate**, and that is the whole reason this is not the Squirrel's
joust. `VesselWitherLifeformByCrystalEffectSO` requires the vessel to be moving FASTER than its
target, because a joust is an overtake and the Squirrel's kit is speed. This hull is the slowest in
the fleet, so an overtake requirement would mean it could never kill anything that was not rooted.
A butterfly does not ram; it drifts over something and the dust does the work.

**It needed a new platform arm.** `SkimmerImpactor`'s crystal case returned outright on
`crystal.IsEmbedded` — correctly, because a heart is not skim-COLLECTABLE and without that gate
every skimmer crystal effect in the fleet (the Rhino sword's burst) would fire on it repeatedly. So
embedded hearts now go to their own list (`SkimmerLifeformCrystalEffectSO`), latched at 0.5 s.
**Every other vessel leaves that list empty, so the arm is a no-op fleet-wide.**

The pilot half closes a gap the fleet already knew about:
`author_combat_debuff_magnitudes.py --check` reports *"strike 8 pts → would be −1.333 levels total …
skimmer family's only drain SO is the Squirrel's overtake, which gates on being FASTER."* This is
that drain path, priced by the same rule — **−0.333333 per element over four**.

### 3.2 Mass — Spread Wings

Hold RT: the wings open and the wake widens from a narrow line of keys into a broad ribbon. **It
costs energy the whole time** — that is the point. A brush that is always at its widest is not a
brush, it is a setting, so what the pilot is actually composing is *where the broad strokes go*.

Running dry is a **close, not a refusal**: the wings shut, the wake narrows, the pilot keeps flying
and the wings re-open by themselves once the meter recovers past 15% (a hysteresis band, so a meter
hovering at zero cannot flutter them). A meter that blocked the press would make a brush feel like
a cooldown.

Mass's *continuous* dial is a different quantity — the trail prism's **volume**, the Squirrel's
mapping reused rather than reinvented. So Mass makes the wake bigger two ways that do not overlap:
the pilot spends energy to make it **wider**, and the element makes each key **heavier**.

### 3.3 Space — Wingreach

Passive. Opposing-domain mass that passes through the wings **dissolves**. The Butterfly reduces
enemy volume by soaring over it. Space is the **reach** — the skimmer's `Scale` ElementalFloat,
evaluated live, so a Butterfly that has fed on Space erases a wider swath on every pass.

`opposingDomainOnly` is a new flag on `SkimmerDamagePrismEffectSO`, defaulting **off** so the Rhino
is byte-identical. It has to live on the effect rather than on `Skimmer.affectSelf`, because that
flag is a domain compare evaluated **after** the effect loop and gates only the skim bookkeeping — a
vessel with `affectSelf` off still runs every skimmer prism effect on its own mass.

**Broadwing** arms the *far-field* wings with the same effect, gated on the replicated unlock bit
via the new `requiresUpgradeElement` field — so the upgrade genuinely widens the swath rather than
changing what a pass does.

### 3.4 Time — Fold

See **`BUTTERFLY_FOLD.md`**.

## 4. The wake — the piano keys

| Number | Value | Why |
|---|---|---|
| `BaseScale` | `26 × 1.2 × 3.4` | Wide across, thin, SHORT along the flight path — a key, not a ribbon |
| `initialWavelength` | 9 | Leaves air between consecutive keys at cruise, so they read as separate |
| `minBlockScale` / `maxBlockScale` | 0.35 / 1.0 | Wings shut is a narrow line; wings spread is the full slab |
| `Gap` | 0 | ONE wide key, not two rails |
| `trailVolume` | Mass, 1 → 2.5 | The Squirrel's mapping |

## 5. HUD

Deliberately sparse: two of four abilities are passive and have no state a pilot can wait on, so
neither gets a gauge (drawing one would be an instrument reporting a decision nobody makes). The two
that do have state get one readout each — a **linear wing-energy fill** on the Mass plate, pinned
full once Mural makes the spread free, and the fleet's **clockwise depleting recharge veil** on the
Time plate.

The Fold's veil is the ONLY feedback a refused press gets, which is why the controller pushes it
every frame rather than off an edge.

## 6. Files

| Role | Path |
|---|---|
| Hull geometry (pure) | `Assets/_Scripts/Controller/Vessel/ButterflyHullForm.cs` |
| Hull emitter | `Assets/_Scripts/Controller/Vessel/ButterflyHullBuilder.cs` |
| Wingbeat + morph | `Assets/_Scripts/Controller/Animation/ButterflyAnimation.cs` |
| Fold tuning / executor | `R_VesselActions/Data Containers/FoldActionSO.cs`, `R_VesselActions/Executors/FoldActionExecutor.cs` |
| Spread tuning / executor | `R_VesselActions/Data Containers/SpreadWingsActionSO.cs`, `R_VesselActions/Executors/SpreadWingsActionExecutor.cs` |
| Dust (pilot) | `ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselElementalDebuffBySkimmerEffectSO.cs` |
| Dust (lifeform) | `ImpactEffects/EffectsSO/Skimmer Crystal Effects/SkimmerWitherLifeformByCrystalEffectSO.cs` |
| The new skimmer arm | `ImpactEffects/EffectsSO/Abstract Effect Types/SkimmerLifeformCrystalEffectSO.cs` |
| HUD | `UI/Controller/ButterflyHUDController.cs`, `UI/View/ButterflyHUDView.cs` |
| Design record | `Assets/Resources/ElementalAbilityMaps/Butterfly.asset` |
| Asset builder (editor) | `Assets/_Scripts/Editor/FrogletTools/ButterflyVesselSetup.cs` |
| Offline gate | `Tools/Build/butterfly_hull_harness/run.sh` |

## 7. Building it — the editor step

**The prefab and its assets do not exist on the branch yet.** Everything that could be authored
headlessly was (the code, the ability map, the harness); the prefab could not be, because its root
carries a `NetworkObject` whose `GlobalObjectIdHash` Unity computes — a hand-authored hash that
collides breaks scene synchronization for every later joiner with no error
(`Docs/PartySystem/BUGS.md` B16), and the same is true of the nested prefab instances.

Run **FrogletTools ▸ Vessels ▸ Create Butterfly Vessel**, read its report, then use its
**Validate & Push** panel. Anything the report lists as `UNWIRED` is a real gap, not a warning.

## 8. In-editor verification

1. **Build.** Run the tool. Expect zero `UNWIRED` lines. It is idempotent — safe to re-run.
2. **Offline gate.** `DOTNET_ROOT=… Tools/Build/butterfly_hull_harness/run.sh` → `ALL CHECKS PASSED`.
3. **Audits.** `FrogletTools ▸ Vessels ▸` **Audit Vessel Ability Rows** (4/4, in order),
   **Audit Vessel Skimmers** (both wings initialized and active), **Audit Vessel Elemental Morphs**
   (four PROCEDURAL morphs, none INERT), **Audit Ability Lockups**, **Audit Vessel Construction**.
4. **Fly it** in Menu_Main freestyle via the vessel-changer toy. Check: the hull is a butterfly and
   the wings BEAT visibly from the chase camera; the wake is a row of separate wide keys.
5. **Spread** (RT): the wake widens over ~1.5 s, the wings flatten, the Mass gauge drains, the wings
   sag shut at empty and re-open by themselves.
6. **Fold** (LT): the vessel stops, a ghost appears on the hull and travels; thumbs IN pull it to the
   cell core, thumbs OUT to the membrane, hands off leaves it at half radius; `YDiff` rolls the
   whole frame; release teleports you. Confirm the Time card's veil sweeps and a second press inside
   the recharge does nothing.
7. **No trail pile.** Hold the Fold for 5 s and confirm **no** prisms accumulate at the origin.
8. **MPPM two clients.** Confirm the fold's stop, closed wings and final pose all replicate, and
   that the ghost appears on the OWNER's screen only.
9. **Elements.** Feed Mass and Space crystals and watch the hull morph and the wing reach grow.

## 9. Follow-ups

- **Nothing here has been run in the editor.** Every number is authored from analysis or from the
  offline harness; none is play-tested. The beat rate, the energy economy and the fold recharge are
  the three most likely to want a pass.
- **The ghost has no material of its own.** `FoldActionExecutor.ghostMaterial` is empty, so it
  borrows the hull's and reads as an opaque second ship. A translucent domain-tinted material is the
  intended look.
- **No jets and no tail mounts are authored.** `VesselTailAndJets` is on the prefab; the mounts are
  not (`Docs/VESSEL_TAIL_AND_JETS.md` §4.z: the plume's dial is `m_LocalScale` on the jet instance,
  target `(0.6, 0.6, 0.13) × |followOffset.z| / 20` = `(3.6, 3.6, 0.78)` at this camera).
- **No FMOD events.** Every sound slot ships empty and therefore silent, per the audio convention —
  a wingbeat, the fold's departure and arrival, and the dust are the four that want events.
- **The hull's morph bake duplicates the Scarab's.** `BakeMorphSet`/`BlendPart`/`AssertSameTopology`
  are the same machinery with different geometry. Extracting a shared `ProceduralHullMorph` is a
  genuine refactor, deliberately **logged and not acted on** inside a new-vessel branch.
- **No codex entry and no toybox portrait** — `ToyVesselRoster` will pick the hull up through
  `IProceduralHullSource`, but the codex bake has not been re-run.
