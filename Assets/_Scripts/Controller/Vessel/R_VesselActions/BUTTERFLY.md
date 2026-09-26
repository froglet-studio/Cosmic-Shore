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
- **It fights by SOARING, not by shooting.** Its one weapon is DUST it trails beneath it, and the
  only button involved is the one that chooses between painting and dusting. Nothing is aimed.
- **It is legible from a long way off.** The camera sits at **207 units** (`|followOffset|`, the
  fleet's longest after the Serpent's 250), so the wingspan and the beat carry the read, and every
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

### 2.0 The camera — and the one field it shipped inheriting (2026-09-26)

`ButterflyCameraSettingsSO` is `followOffset (0, 37.4, -204)` — **70% further than the shipped
`(0, 22, -120)`**, scaled uniformly so the look-down angle is unchanged (2026-09-26). It is the
fleet's longest camera after the Serpent's 250, and the brief's "flies slow from far away". That number is load-bearing for more
than the look — the prism occlusion corridor and the vessel-tail width are both derived from
`|followOffset.z|`, so it sizes the hull's whole relationship with the camera.

**It also shipped with `farClipPlane 1000` against the fleet's 12000**, which was reported as *the
draw distance goes way down with the Butterfly*. That is what it was, and the cause was not the
camera: `ButterflyVesselSetup.BuildCameraSettings` named `followOffset` and nothing else, so every
other field came out at the C# field initializer — and `CameraSettingsSO.farClipPlane`'s initializer
was 1000 while all nine shipped assets say 12000. **A twelfth of the fleet's draw distance, chosen
by nobody.**

1000 does not cross a standard cell: a 1200-radius membrane is 2400 units across, so the far wall
of the arena was clipped away outright, and in Waystation — whose course spans the whole shell —
the next cluster was routinely not drawn. `CustomCameraController` writes the value straight onto
the live camera in two places, so it reached the screen in full.

Fixed three ways, because the asset alone would only fix this hull:

- the asset is 12000;
- the **class initializer is now 12000**, so the next vessel is correct by construction (a no-op
  today — all nine assets carry the key);
- `ButterflyVesselSetup` now **states** it, and `Tools/Build/check_vessel_camera_farclip.py`
  (`--self-test`) fails any vessel camera below the fleet's own measured mode, or carrying no
  `farClipPlane` at all. Its negative control is this exact value.

This is the `/vessel` skill's rule 4-i met from the other side. That rule warns that a field ABSENT
from a prefab takes its initializer, so a silent prefab is not an unset one; here the field was
*present* in the asset and still carried the initializer, because the tool that wrote the asset
never named it. **General rule: a generated asset's un-named fields are the C# initializer's
opinion, and an initializer that disagrees with every shipped asset is a trap rather than a
default** — so the fleet value belongs in the initializer, and a gate belongs on the agreement.

The rest of its camera block is deliberate and unflagged: `dynamicMinDistance 10` /
`dynamicMaxDistance 40`, `followSmoothTime 0.2`, `rotationSmoothTime 5`, adaptive zoom off.

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
| **Charge** | **Scale Dust** — in Dust mode, the capsule debuffs opposing pilots (the BITE is Charge), kills opposing lifeform hearts, refreshes ally ones | passive (lives in Dust mode) | **Monarch** — the dust bites twice as deep |
| **Mass** | **Mass / Dust Mode** — RT switches between a WIDE wake (5x at Mass 0 → 20x at Mass 15) and the dust | RT (`RightStickAction`) | **Gilded Wake** — Mass mode lays SHIELDED prisms |
| **Space** | **Dust Reach** — the capsule's LENGTH; own mass it touches grows / turns dangerous / shields, opposing mass is destroyed / shrunk / stolen | passive (lives in Dust mode) | **Diamond Dust** — own PLAIN mass occasionally super-shields |
| **Time** | **Fold** — hold to stop, reach out along your heading, release to be there; leaves a standing pair of domain gates behind | LT (`LeftStickAction`) | **Far Fold** — the fold reaches twice as far |

**The 2026-09-26 re-cut, in one paragraph.** The hull shipped with TWO wing skimmers (near and far
field), both always on and both invisible, and an RT that spent a wing-energy meter to widen the
wake. It now has **ONE skimmer** — a capsule hanging below the hull — and the right trigger is a
**mode switch**: Mass mode paints (wide wake, dust off), Dust mode dusts (narrow wake, capsule on
and drawn as falling motes). Nothing costs energy any more; the choice itself is the cost, because
you cannot paint wide and dust at once.

### 3.1 Charge — Scale Dust

The dust exists only in **Dust mode**. It is `ButterflyDustSkimmer.prefab`: a `CapsuleCollider`
(centre `(0, −0.5, 0)`, height 1, radius 0.5, along local Y) so it hangs **below** the hull, a
`Skimmer` whose Space-scaled `Scale` is elongated on **Y only** (`elongateYOnly`) so Space makes it
LONGER rather than fatter, and `ButterflyDustField`, which switches the collider AND the impactor
off together outside Dust mode (a disabled collider sends no `OnTriggerExit`, and Unity delivers
trigger messages to disabled MonoBehaviours, so both have to go) and draws the capsule as a runtime
particle fall of motes sized to its live world length. Motes stop EMITTING on the way out rather
than vanishing (continuity of existence). The motes wear the **shielded tier of the vessel's own
domain** — the rim colour (`ShieldedInsideBlockColor`), read through
`SO_ColorSet.TryGetPrismKindColors(domain, PrismKind.Shielded)`, the one source every prism tier is
painted from, so the dust always matches the shielded mass it lays and shields. It is read LIVE
each frame against the last domain painted, so a domain change reaches the NEXT motes while the
ones in the air finish in their old colour; `Domains.Blue` and a missing palette fall back to the
authored `dustColor`. Motes are 2.5-6 world units: at the Butterfly's ~204-unit camera a world unit
is ~2.6 px at 1080p, and the first cut's 0.5-1.6 were 1-4 px, which made Dust mode read as nothing.

What it does to the **living**:

- an **opposing pilot** takes an all-element decaying debuff, classed `VesselContact` so an arena
  ward cannot cancel it. **Charge is the BITE**: `VesselElementalDebuffBySkimmerEffectSO` gained a
  `biteScale` `ElementalFloat` (0.5x at rest → 2x at level 10, floor 0.25) that multiplies the
  priced magnitude (−0.333333 per element over four). Every other vessel authors none, so it is 1.
- an **opposing lifeform** (flora or fauna) whose heart the capsule reaches **dies** — the wither,
  through the normal sealed death path, crystal dropped exactly as starvation would. No speed gate:
  this is the slowest hull in the fleet, and an overtake requirement would mean it could never kill
  anything that was not rooted.
- an **ally lifeform** is **refreshed** instead (`SkimmerNourishLifeformByCrystalEffectSO` →
  `ILifeFormEntity.Nourish()`): its starvation clock resets and its breeding counter advances. It is
  a FOOD-WEB event, never a size (`Docs/ECOSYSTEM.md §40`), and it is latched at 5 s per lifeform so
  a Butterfly parked over a plant cannot fund a population boom by hovering.

**Element levels do not replicate, and this is the first vessel whose SCALING has to agree across
peers**: a skimmer overlap is observed on every machine, and the debuff's bite and the Mass-mode
width would otherwise be computed from each machine's own (wrong) idea of the pilot's level. So
`R_VesselActionHandler.NetElementLevels` (owner-write `ushort`, one 4-bit nibble per element,
clamped 0..15) publishes the integer levels and `ElementalFloat.EvaluateReplicated(status)` reads
them. Integer levels are exactly what an `ElementalFloat` is authored against, so nothing is lost;
fractional overcharge above 15 clamps.

### 3.2 Mass — Mass / Dust Mode

The right trigger **toggles** (`SpreadWingsActionSO.inputStyle`, `Toggle` by default; `HoldForDust`
is the other option and is one enum change away). A Butterfly spawns in **Mass mode**.

- **Mass mode**: the wings are spread, the dust is off, and the wake is WIDE —
  `massModeWidth` is `ElementalFloat.Multiplier(5, 15, Mass, floor 1)`, so **5x at Mass 0, 10x at
  Mass 5, 15x at Mass 10, 20x at Mass 15** (an `ElementalFloat` is linear in the level and extends
  past 10, which is exactly what "20x at 15" asks for). It is applied as
  `VesselPrismController.WidthMultiplier`, eased over `widthBlendSeconds` (1.5 s) so a mode change
  reads as a stroke rather than a snap.
- **Dust mode**: the wings fold, the wake narrows to the unmultiplied key, and the capsule goes live.

`WidthMultiplier` is a new, general knob on the prism controller (default 1, so every other vessel
is byte-identical). A widened prism **states** its size through `Prism.AdmitTargetScale` after
`Initialize` — the pooled prism clamps each axis to `maxScale` 10–40 inside the setter with no log,
so a 20x key would otherwise be silently cut to 40 across (CLAUDE.md, "An AUTHORED prism size widens
its clamp").

**Mass's old second dial is off.** `trailVolume` (per-prism volume, 1 → 2.5) is disabled on this
hull, because Mass is now the WIDTH and two Mass dials on one prism would be one number counted
twice.

**Gilded Wake (L5)** replaces Mural: while Mass is upgraded, Mass mode lays **shielded** prisms
(`VesselPrismController.ForceShielded`, gated on `IsUpgradeActive(Mass)` — the replicated bit, since
whether a prism is shielded is an outcome every peer must agree on). Dust mode's narrow keys are
unshielded either way. Shielded mass is not food and leaves the targeting grids, so a Mass-5
Butterfly's murals are paintings the herbivores leave alone.

### 3.3 Space — Dust Reach

Space is the capsule's **LENGTH** (the skimmer's `Scale`, 60 → 150 across L0..L10 on Y only), and
the capsule is what the dust does to **mass** (`SkimmerScaleDustPrismEffectSO`):

| Prism | Outcome (one per prism) |
|---|---|
| **own domain**, plain | grows along a random axis (+35%) · turns **DANGEROUS** · turns **SHIELDED** (0.4 / 0.3 / 0.3) |
| own domain, shielded or dangerous | grows only (it already carries a state) |
| own domain, super-shielded | untouched |
| **opposing** | **destroyed** (debris at the capsule's contact velocity) · **shrunk** 35% · **stolen** |
| opposing, super-shielded | the destroy path, which the super-shield turns into a deflection |

**Each prism's outcome is a deterministic roll of THAT PRISM** — a hash of its rounded position, its
target scale and its domain — never `UnityEngine.Random`. A skimmer contact is observed on every
peer, so a random roll would give each machine a different arena. The same hash drives the random
growth axis.

**Diamond Dust (L5)** replaces Broadwing: dust on your own **plain** mass has a **6%** chance to
raise a **super-shield** instead. It is deliberately rare — super-shielded mass is invulnerable to
everything but an energised Rhino blade.

### 3.4 Time — Fold

Hold to stop, watch a ghost reach out along the heading you arrived on, release to be there — and
**every fold leaves a PAIR OF GATES standing**, one where you left and one where you arrived. They
are domain switches: any vessel of the Butterfly's domain threads either and is at the other, as
often as it likes, until this Butterfly folds again and the new pair replaces the old one. That is
what turns the fleet's slowest hull into a team's shortcut rather than a ship that can only ever
move itself.

See **`BUTTERFLY_FOLD.md`** (§ "Every fold leaves a PAIR OF GATES standing" for the gates).

## 4. The wake — the piano keys

| Number | Value | Why |
|---|---|---|
| `BaseScale` | `26 × 1.2 × 3.4` | Wide across, thin, SHORT along the flight path — a key, not a ribbon |
| `initialWavelength` | 9 | Leaves air between consecutive keys at cruise, so they read as separate |
| `minBlockScale` | 0.35 | The key's resting width (`26 × 0.35 ≈ 9.1`) — Dust mode's wake |
| `WidthMultiplier` (Mass mode) | Mass, 5x → 20x at L15 | Mass mode's wake: ~45 across at Mass 0, ~182 at Mass 15 |
| `Gap` | 0 | ONE wide key, not two rails |
| `trailVolume` | **disabled** | Mass is the WIDTH now; a second Mass dial on the same prism would double-count |

## 5. HUD

Deliberately sparse: two of four abilities are passive and have no state a pilot can wait on, so
neither gets a gauge (drawing one would be an instrument reporting a decision nobody makes). The two
that do have state get one readout each — a **mode fill** on the Mass plate (FULL in Mass mode,
EMPTY in Dust mode, travelling between the two as the wake eases; it was a wing-energy meter until
the energy cost was retired, and a gauge whose meter is gone would be a lie), and the fleet's
**clockwise depleting recharge veil** on the Time plate. The mode fill is a BINARY state drawn with
a transition — never a partial fill a pilot could read as a quantity.

The Fold's veil is the ONLY feedback a refused press gets, which is why the controller pushes it
every frame rather than off an edge.

### 5.1 The row was written and never bound (2026-09-25)

The view and the controller above were complete from the day the hull shipped, and
`ButterflyHUDVariant.prefab` bound **none** of it: `abilityIcons: []` and
`wingEnergyGauge: {fileID: 0}`. So all four cards rendered LOCKED and the recharge veil swept over
a bare plate — which is the Serpent's report one vessel over, and the rule it left behind is that
an **indicator whose only rendering is its VALUE has no rendering at its extremes**: a veil with
nothing under it looks identical at "just fired" and at "ready", and identical again to a veil
nobody is driving.

Two tools close it, and the split is deliberate — art and wiring go stale for different reasons:

| tool | writes |
|---|---|
| `Tools/Build/author_butterfly_icon_placeholders.py` | four 128 px PLACEHOLDER silhouettes under `_Graphics/Icons/AbilityIcons/Butterfly/`, the Manta's scheme (`author_manta_icon_placeholders.py`) |
| `Tools/Build/author_butterfly_ability_row.py` | the four hosts + icons + the wing-meter Image into the HUD variant, and the bindings |

Each icon names the **ACT**, not the vessel — three of the four could otherwise be "a wing" and be
unreadable at the ~40 px a card draws: a wing shedding motes (Scale Dust), the whole butterfly
(Spread Wings), a wing with reach arcs (Wingreach), and **two rings with an arrow between them**
(Fold, drawn as the gates it now leaves). The icon tool asserts each silhouette's coverage into a
readable band, because a placeholder at 2% is a hairline and one at 60% is a blob and both read as
no icon at all.

Three things about the wiring are worth keeping:

- **It is authored into the HUD VARIANT, not `Butterfly.prefab`.** `ButterflyHUDView` is an added
  component on the variant, so the bindings and the objects they point at live together and the
  vessel prefab needs no instance override — an override there is the Squirrel's Time-icon trap,
  where a value on the variant had been dead for as long as the override existed.
- **The meter is bound TWICE, to the same Image, and both are required.** The view WRITES
  `fillAmount` through `wingEnergyGauge`; the lockup ADOPTS the meter through the binding's own
  `gauge` field. Bind only the field and the lockup never claims it, so `RetireLegacyChrome`
  switches it off as an unrecognised child of the host — a correctly-driven meter drawing nothing.
- **Nothing authored here is a layout decision.** The lockup owns position, pitch, cell size and
  host scale, and derives each icon's scale as `iconBoxSize / its authored size`; the icons are
  authored at exactly `iconBoxSize` so that derivation is 1 today and still correct if the style
  moves.

Binding icons also flips `RetireLegacyHudContent` from "clear the whole root" to "spare what is
still referenced". Measured: `ButterflyHUDView` references only the new row and the ensured
`InputDeviceIconSetSwitcher` carries a single float, so nothing from the base HUD is spared and the
screen is unchanged apart from the row itself.

## 6. Files

| Role | Path |
|---|---|
| Hull geometry (pure) | `Assets/_Scripts/Controller/Vessel/ButterflyHullForm.cs` |
| Hull emitter | `Assets/_Scripts/Controller/Vessel/ButterflyHullBuilder.cs` |
| Wingbeat + morph | `Assets/_Scripts/Controller/Animation/ButterflyAnimation.cs` |
| Fold tuning / executor | `R_VesselActions/Data Containers/FoldActionSO.cs`, `R_VesselActions/Executors/FoldActionExecutor.cs` |
| Mode switch tuning / executor | `R_VesselActions/Data Containers/SpreadWingsActionSO.cs`, `R_VesselActions/Executors/SpreadWingsActionExecutor.cs` |
| Dust capsule (collider gate + motes) | `R_VesselActions/ButterflyDustField.cs`, `_Prefabs/Spacevessels/Components/ButterflyDustSkimmer.prefab` |
| Dust (mass) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleDustPrismEffectSO.cs` |
| Dust (pilot) | `ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselElementalDebuffBySkimmerEffectSO.cs` (`biteScale`) |
| Dust (lifeform) | `ImpactEffects/EffectsSO/Skimmer Crystal Effects/SkimmerWitherLifeformByCrystalEffectSO.cs` (opposing), `SkimmerNourishLifeformByCrystalEffectSO.cs` (ally) |
| Omni-crystal bloom | `_Prefabs/Projectile/AOEButterflyBloom.prefab` + `ButterflyVesselExplosionByCrystalEffect.asset` + `ButterflyBloomExplosionImpactorDataContainer.asset` |
| Replicated element levels | `R_VesselActionHandler.NetElementLevels`, `R_VesselElementalAbilityHandler.ReplicatedLevel`, `ElementalFloat.EvaluateReplicated` |
| Dust assets (generated) | `Tools/Build/author_butterfly_dust.py` (`--check`) |
| The new skimmer arm | `ImpactEffects/EffectsSO/Abstract Effect Types/SkimmerLifeformCrystalEffectSO.cs` |
| Fold gate | `R_VesselActions/FoldGate.cs`, `R_VesselActions/FoldGateGeometry.cs` |
| Fold gate offline proof | `Tools/Build/foldgate_harness/` (compiles and RUNS the shipped geometry) |
| HUD | `UI/Controller/ButterflyHUDController.cs`, `UI/View/ButterflyHUDView.cs` |
| HUD row + icons (authored) | `Tools/Build/author_butterfly_ability_row.py`, `Tools/Build/author_butterfly_icon_placeholders.py` |
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
   **Audit Vessel Skimmers** (ONE skimmer, the near field, assigned; the far field EMPTY by design
   — the audit may flag its collider as disabled at rest, which is Mass mode working), **Audit Vessel Elemental Morphs**
   (four PROCEDURAL morphs, none INERT), **Audit Ability Lockups**, **Audit Vessel Construction**.
4. **Fly it** in Menu_Main freestyle via the vessel-changer toy. Check: the hull is a butterfly and
   the wings BEAT visibly from the chase camera; the wake is a row of separate wide keys.
5. **Mode switch** (RT): spawns in Mass mode with a WIDE wake (~5x the key). RT once → wings fold,
   wake narrows over ~1.5 s, the Mass fill empties, and a fall of motes in your domain's shielded
   colour appears **below** the hull
   (and only there — confirm there is exactly ONE dust volume and none at the wingtips). RT again →
   back. Feed Mass and watch Mass mode's wake widen toward 20x; at Mass 5 its keys come out shielded.
5a. **Dust on mass.** In Dust mode fly low over your own trail: keys grow, some go dangerous, some
   shielded — and **the same keys do the same thing on a second client**. Over an opponent's trail:
   keys vanish, shrink, or change to your colour. At Space 5, an occasional own key goes
   super-shielded (stellated).
5b. **Dust on the living.** Dust an opposing pilot (debuff lands; bigger at high Charge), an opposing
   creature or plant (it dies and drops its crystal), and one of your own (nothing visible changes —
   its starvation clock resets).
5c. **Omni crystal.** Collect one: a large bloom; any opposing lifeform heart inside it dies; your
   own lifeforms and all prisms survive.
6. **Fold** (LT): the vessel stops, a ghost appears on the hull and travels; thumbs IN pull it to the
   cell core, thumbs OUT to the membrane, hands off leaves it at half radius; `YDiff` rolls the
   whole frame; release teleports you. Confirm the Time card's veil sweeps and a second press inside
   the recharge does nothing.
7. **No trail pile.** Hold the Fold for 5 s and confirm **no** prisms accumulate at the origin.
8. **MPPM two clients.** Confirm the fold's stop, closed wings and final pose all replicate, and
   that the ghost appears on the OWNER's screen only.
9. **Elements.** Feed Mass and Space crystals and watch the hull morph, Mass mode's wake widen and
   the dust capsule lengthen.

## 9. Follow-ups

- **The omni-crystal bloom kills opposing LIFEFORM hearts only.** "Destroys opposing domain
  crystals" was read as the crystals that belong to an opposing domain in play — the hearts of its
  flora and fauna. Opposing **team crystals** (`TeamCrystalImpactor`) are not in the blast's sweep
  (`ExplosionImpactor.SweepCrystals` picks up OMNI crystals only) and are untouched; that needs a new
  sweep arm if wanted. The bloom is **non-destructive to prisms** (`destructive: 0` on
  `AOEButterflyBloom.prefab`) — one field if it should also clear mass.
- **`R_VesselActionHandler.NetElementLevels` replicates element levels fleet-wide** (one ushort per
  vessel, owner-written only on change). Only the Butterfly reads it today
  (`ElementalFloat.EvaluateReplicated`). Every other skimmer effect that scales on a level still reads
  the observer's local number — the same bug class, now with a fix available.
- **The wing-energy resource is unused.** The meter stays in the resource list (removing an index
  shifts every authored index after it) but nothing drains or reads it.
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
- **The four ability icons are PLACEHOLDERS** (§5.1), white silhouettes for the art pass to replace
  1:1. `upgradedSprite` is empty on all four, so an upgrade is signalled by the card alone.
- **No card icons.** `SO_Class_Butterfly` authors no `IconActive`/`IconInactive`, so
  `check_vessel_class_icons.py` is red on this hull. The Scarab's fix
  (`Tools/Build/render_scarab_card_icons.py`) is the pattern.
- **A standing fold gate has no HUD marker and no sound**, and an AI never threads one
  (`BUTTERFLY_FOLD.md` § Follow-ups).
