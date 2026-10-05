# Elemental Bars, Hull Morphs & Ability Row

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### Elemental Bars (per-vessel buff/debuff display)

`ElementalBarsView` (`_Scripts/UI/View/ElementalBarsView.cs`) is the shared HUD widget every vessel uses to convey its dynamic and meta-earned elemental buffs/debuffs. Each of the four elements (Charge, Mass, Space, Time) renders as a **5-fold-symmetric "flower"**: five copies of one crisp white petal sprite, pivot-centred and rotated 72°·n. The petal shape differs per element (charge = irregular pentagon, mass = triangle, space = kite, time = rhombus), all sharing an inward-pointing 72° apex so adjacent inner edges stay parallel and form the negative-space gaps.

**Level → colour mapping.** `ResourceSystem.GetLevel(element)` returns `floor(normalizedLevel × 10)` with `normalizedLevel ∈ [-0.5, 1.5]` → an integer in **[-5, 15]**. `ElementalBarsConfigSO.DistributePetalValues` spreads that total round-robin across the five petals; each petal value lands in `{-1,0,1,2,3}` → `{fire, grey, white, blue, lime}`:

| Level | -5 | 0 | +5 | +10 | +15 |
|---|---|---|---|---|---|
| Petals | all fire | all grey | all white | all blue | all lime |

At any total at most two adjacent colours show (e.g. +8 → 3 blue + 2 white). Petals are pure white, so a single multiply-tint reproduces every colour exactly — **never hue-shift** (a low-saturation source can't reach grey/white or vivid colours). Each petal recolours and scale-pops about the flower centre (outward bloom) on upgrade, flash+shakes on downgrade.

**The maintained-mechanism law (LOCKED).** No sustained/held mechanism may HOLD an element above integer level **10** — the 10..15 overcharge band belongs to **transients only**, and everything in it drains back to (at most) 10: temporary effects decay to zero, crystal-earned base overcharge bleeds down (`RecoverBaseLevels`), and the comeback bonus fills toward 10 and never past it. (A FOURTH such layer, the **domain fauna buff** — living fauna hearts empowering their whole domain's vessels — was **REMOVED** in Sep 2026, because it granted standing elemental power for nothing but having fauna alive. The law governs whatever sustained mechanisms exist; do not rebuild that one. `Docs/ECOSYSTEM.md §15`.) The player always gets to *feel* a reward above 10, and the drain always restores the headroom to feel the next one. Enforced in `ResourceSystem` (`SustainedCeiling`, `CompositeEffectiveLevel`); mechanics log: `Docs/ECOSYSTEM.md §15`.

**Single source of truth — `ElementalBarsConfigSO`** (`_Scripts/ScriptableObjects/`, asset at `Resources/ElementalBarsConfig.asset`). Per CLAUDE.md Config Separation, all shared look/feel lives here: the 5 tick colours, per-element petal sprites, and every juice timing/haptic. All vessels reference the one asset, so the spec can't drift between prefabs. Holds the petal math (`DistributePetalValues`, `ColorForTick`) and constants (`PetalCount=5`, `MinLevel=-5`, `MaxLevel=15`, `PetalSpacing=72`).

**Per-vessel integration.** `ElementalBarsController` (on all 11 vessel prefabs — formerly named `SilhouetteController` before the vessel silhouette/trail-display HUD element it also drove was removed; the leftover `Silhouette` GameObjects were finally excised from all 13 vessel + HUD-variant prefabs in 2026-08, along with the dead `silhouette`/`silhouetteContainer`/`trailContainer` keys — do not re-add a vessel silhouette to a HUD) is the driver: `InitializeElementBars()` calls `elementBars.Build()`, seeds levels, and subscribes to `ResourceSystem.OnElementLevelChange`. The `elementBars` reference is null-safe — vessels without the view wired simply show no bars (opt-in rollout). `SquirrelVesselHUDView` routes drift/joust/crystal juice into the view.

**Zero-wire by default.** With no config or petalRoot assigned, the view loads `Resources/ElementalBarsConfig`, auto-creates a centred flower container per element, and loads petal sprites from `Resources/ElementPetals/{element}_petal`. To author explicitly (recommended for real positioning), run **FrogletTools > Vessels > Wire Elemental Petal Bars** (assigns config + creates `*_Flower` containers), then position the containers. A petal authored in-prefab as `Petal{0..4}` under a container is reused (not duplicated) and normalised via `ElementalBarsView.ConfigurePetal`.

**Patterns to follow:**
- **Spec changes go in the config asset**, never per-vessel SerializeFields — that's the whole point of the shared system.
- **Petal sprites are pure-white silhouettes** tinted at runtime. Add a new element by adding its sprite to the config's `petals` list and `Resources/ElementPetals/`.
- **Rolling out to another vessel**: add an `ElementalBarsView` to that vessel's HUD (or run the wirer), then assign it to the vessel's `ElementalBarsController.elementBars`. No code changes.
- **Performance**: petals render at ~88px — keep `maxTextureSize` small (128). One `Image` per petal (20 total), `raycastTarget` off, event-driven (no `Update`), `SetLevel`/`RefreshBar` early-out when nothing changed, tweens `SetLink`ed and killed + snapped to rest on `OnDisable` for pooled/toggled HUDs.

### Elemental Hull Morphs (the vessel model is an element display)

The vessel's own hull conveys its element levels: vessel models carry **blend shapes on their
skinned meshes labeled by element name** (`charge` / `mass` / `space` / `time`, case-insensitive —
authored into the FBX), and `VesselAnimation` (base class, runs on every vessel) discovers them **by
name** at `Initialize` and glides each between its extremes as the effective element level moves
through the **[0, 10] progression band** — the deficit band [-5, 0) holds the level-0 silhouette,
the overcharge band (10, 15] holds the level-10 authored extreme (the same effective level the HUD
flowers read, so hull and flowers always agree). Transitions are DOTween glides, never snaps —
continuity of existence applies to the vessel's own body.

- **Single source of feel — `VesselElementalMorphConfigSO`** (`_Scripts/ScriptableObjects/`, asset
  at `Resources/VesselElementalMorphConfig.asset`): morph duration + ease, plus the pure helpers
  (`NormalizedMorphWeight`, `TryResolveElement` — both edit-mode tested in
  `VesselElementalMorphTests`). Spec changes go in the asset, never per-vessel fields.
- **Opt-in by art, zero wiring.** A vessel morphs the moment its model ships element-labeled shape
  keys — no per-prefab flags (the old `UseShapeKeys` bool + hardcoded shape indices are retired).
  Non-element art shapes (jaws, tendrils) are untouched; a name mentioning two elements is ambiguous
  and ignored. The shape's authored extreme is read from its last frame weight, so 0-100 and custom
  frame weights both work.
- **Fleet status**: audit with **FrogletTools > Vessels > Audit Vessel Elemental Morphs** (asset-only,
  no play mode, uses the exact runtime discovery). Manta/Termite/Falcon/Shrike (Manta meshes),
  Sparrow, Serpent, Squirrel **and now the Dolphin** ship labeled shapes that MOVE the hull (9/12
  by shape keys, measured 2026-08-26); **the Dolphin was rig-swapped on 2026-08-26** and is the
  only vessel that has ever changed families. Urchin/Rhino still wire shape-less test meshes; their
  rigs were swapped for the PUPPETRY with the morph honestly absent, because those rigs' element
  shapes are LABELLED BUT INERT (below). Grizzly has no labeled shapes yet.
  **The Scarab morphs PROCEDURALLY** — its hull is generated (`ScarabHullForm`), so its morphs are
  the four element extremes of that same pure function, baked to per-vertex deltas and blended at
  the fleet's shared feel (`ScarabHullBuilder` owns geometry, `ScarabAnimation` owns time via the
  same config SO; record: `R_VesselActions/SCARAB.md §3.0.2`). A vessel like it declares
  **`IProceduralElementMorphSource`**, which the auditor reads two ways: procedural coverage
  COUNTS, and element blend shapes under the source's hidden legacy model root report as
  **INERT** — the Scarab wraps the Sparrow FBX renderers-off, and without that marking the audit
  reported it morph-complete via a model nobody can see. *A blend shape on a hidden renderer is
  the labelled-but-empty-shape trap (§ below) in a second costume: green audit, nothing on
  screen.* The two inert reasons are **disjoint and both apply**: since the auditor now filters on
  travel magnitude AND on hidden-root membership, its fleet line counts shape-key and procedural
  morphs together and supersedes the 9/12 above — re-run it to read the combined number.
- **The Squirrel's FBX is a spliced hybrid of two historical exports — do not re-export over it
  blindly.** The 2024-10-29 export (`aa5046d41`, "add squirrel with shapekeys") carried
  `Time/Mass/Space/Charge` but its takes were broken; the 2024-11-15 re-export (`dc2c8ea54`,
  "fixed squirrel animations") repaired 2,622 of 3,483 bone curves across all 9 takes **and
  silently dropped all four shape keys** — which also silently killed the elemental morph surface.
  The shipped file is the fixed export with the four shape-key subtrees grafted back at the FBX
  binary level (valid because both exports share byte-identical topology and vertex drift ≤2e-6;
  verified by byte-level structural diff: base objects untouched, takes byte-identical to the fixed
  export, shapes byte-identical to the shape-key export, and **zero blend-shape animation curves**
  — the donor's constant-zero residue curves were deliberately left out). Same path + GUID; the
  mesh fileID is a name-hash shared by both exports, and the `.meta` pins each clip's take name to
  an explicit internalID matching `SquirrelAnimatorController 1`'s motion references — so the
  nested prefab instance, the animator clips, and the blend-space puppetry
  (`MantaAnimationContoller` → Animator floats `Pitch/Yaw/Roll/Throttle`) all keep binding. Any
  future Squirrel re-export must carry BOTH the fixed takes and the four element shape keys.
- **Morph weights are written in `LateUpdate`, which is load-bearing.** Unity's Animator writes
  bound curves every frame during the animation update — after `Update`, where tweens run — so an
  export that carries even constant-zero blend-shape curves would stomp script-set weights every
  frame. Tweens therefore drive a cached weight and `VesselAnimation.LateUpdate` is the single
  writer to the renderers, making the element level authoritative over any stray animation curve on
  any vessel (the current Squirrel takes are clean, but the defense is deliberate). Do not
  "simplify" the tween to write the renderer directly.
- **Animated parts resolve BY NAME too** (`VesselAnimation.ResolvePart`, `ResolveParts()` hook):
  an authored inspector reference always wins, and an empty one is looked up among the model's
  descendants by candidate name — current rig bone first, legacy part name as fallback. This is
  what makes an art swap cheap: the stale references come back null and the rig's bones bind
  themselves. Unbound parts are reported (`ReportUnresolvedParts`) and degrade to "that limb
  doesn't move", never a per-frame `NullReferenceException`.

#### The rigged-model swap (Dolphin / Urchin / Rhino)

These three were the fleet's only vessels whose art could not morph, and it was **not** a wiring
oversight — their prefabs wired fundamentally different models. `Dolphin_Test.fbx` is 17 separate
static part meshes, `Urchan_Test.fbx` 14, and Rhino wires `Rhino_Test.fbx` (7 meshes); none carries
a single blend shape. **The Dolphin left this set on 2026-08-26**: its prefab now wires
`dolphin_shapekey_with_animations.fbx` and it morphs on all four elements (Mass 12.056%, Charge
4.314%, Space 3.140%, Time 13.538% of the hull diagonal). Rhino and Urchin remain, by decision
rather than by omission — see the swap section below. **That last one used to read "Rhino wires `Vessel_Placeholder_1.fbx`", which
was this very document repeating the guid trap `VESSEL_CONSTRUCTION.md` §2 exists to warn about** —
`Rhino_Test.fbx.meta` is the file whose own `guid:` line carries `4a58…`; the placeholder merely
remapped its materials into it. Corrected 2026-08-26, and the placeholders have since been
retired. Their `*_shapekey_with_animations.fbx` rigs are one skinned mesh on an armature plus four
element shapes — **but only the DOLPHIN's four actually move the hull.** The Rhino's and the
Urchin's each index ONE vertex and move it 0.0000 units, verified across all 93 FBX blobs in the
project's history, so those two have never had a morph anywhere and a swap must leave the morph
honestly absent (`VESSEL_CONSTRUCTION.md` §4, §7). Each rig was authored FOR that vessel's
script: the dolphin rig's `jetT/jetm/jetB × .l/.r` + `jaw.u`/`jaw.b` are exactly
`RiptideAnimation`'s six thrusters and two jaws; the rhino rig's `wing1.*`/`jet.*` are
`RhinoAnimation`'s wings and engines (its `wing2.*` back wings host colliders, nothing drives them);
the urchin rig's `gunM.*`/`jetT.*`/`jetB.*` are `UrchinAnimation`'s guns and jets. The three scripts
name those bones as their primary resolution candidates, so the **code half of the port is done**.

**Rest poses are the reason a rig needs more than a name swap.** Puppetry drives a part *toward* an
absolute local rotation, which silently assumes it rests at identity — true of part-per-mesh art
placed by translation alone, false of a rig, where the bone's rest angle is what fans the engines
out (`wing1.l` rests at ~42°, `jet.l` at ~115°, `gunM.l` at ~90°). So `VesselAnimation` gained
`CaptureRestRotations` / `RotatePartFromRest` / rest-aware `ResetAnimation`: parts are driven
**relative to the pose they were authored in**. Identity-rest art is unaffected; rigged art holds
its shape. Two Dolphin bugs surfaced from the same root and are fixed: `RiptideAnimation` re-homed
its drift parts onto `Chassis` every non-drifting frame (a no-op on the old art, where they were
already its children — on the rig it would have permanently flattened the armature onto `fuse` and
collapsed the six jets onto one point; it now restores each part's **own** captured parent), and its
`InitialRotations` list was indexed two slots out of step with `animationTransforms`, so each engine
animated around a neighbour's rest pose. **That second fix changes the Dolphin's current look** — its
six engine cases rest at 26–169° and were being dragged toward identity.

The prefab half is **FrogletTools > Vessels > Swap Vessel Rig** (`VesselRigSwapper`, 2026-08-26),
which performs it. A `SkinnedMeshRenderer`'s bone list, bindposes, bounds and imported mesh IDs are
owned by Unity's FBX importer — that is the one part that genuinely needs the editor, and the reason
the swap is a tool rather than hand-authored YAML. Everything else is measured: each rig instance is
placed at a **fitted** transform that lands its hull on the shipped hull (Dolphin identity, Rhino
`z −1.5545`, Urchin `localScale 0.474905`), so **no collider is re-fitted** — the volumes are
re-homed onto bones with their world pose preserved. The standing warning still applies inside the
tool: every legacy part carries its `MeshRenderer` alongside its collider, so it strips the art when
it re-homes one, or the old hull welds to the new skeleton. Run **FrogletTools > Vessels > Plan
Vessel Rig Swap** first (report only, never writes): it prints, per vessel, which gameplay object
belongs on
which bone, which objects have **no mapped bone** and would go dark when the old model is disabled
(Rhino's `ForceFieldSkimmer` parents to the legacy root), the rig's element shapes, and the ship-
geometry re-point. The printed procedure ends by clearing the animation's part fields — leave them
**empty** so they resolve to bones — and re-running the morph audit.
- **Seeding**: `VesselAnimation` snaps to live levels at `Initialize` (the live initial emit is
  `ResourceSystem.Start`), and `ResourceSystem.InitializeElementLevels` now emits
  `OnElementLevelChange` (deduped) so a mid-session re-seed repaints every consumer — hull morphs,
  HUD flowers, and ability unlock state alike. Note `SetResourceLevels` currently has **no live
  caller** (its historical MiniGame turn-reset and Hangar call sites are commented out); the emit
  future-proofs any revived re-seed path.

### The Four-Icon Ability Row (LOCKED structure — every vessel HUD)

Every vessel HUD shows **exactly four ELEMENTAL ability icons in the lower right — one per element** —
and the order is not a layout preference, it is the element contract made visible:

> **The icons run charge → mass → space → time, left to right — the same order as the element
> flowers above them.** Each icon sits under the element that upgrades that ability (per the vessel's
> `ElementalAbilityMapSO`), so "which flower do I fill to upgrade this?" is answered by position alone.

`VesselHUDView.AbilityDisplayOrder` is the single source of that order — `VesselHUDController`'s
upgrade-seeding loop and `ElementalBarsView`'s flower layout read the same array. `OnValidate` keeps
the `abilityIcons` list sorted into it; `VesselHUDView.ValidateAbilityIconRow()` (editor-only, called
once from `VesselHUDController.Initialize`) warns on the wrong icon count, an out-of-order binding, or
a layout whose left-to-right order contradicts the bindings.

**The upgrade signal** (element hits its unlock level, default 5 — the all-petals-white flower):
`R_VesselElementalAbilityHandler.OnUpgradeStateChanged` → `VesselHUDController` →
`VesselHUDView.SetAbilityUpgraded`. Three independent layers, so the signal survives any per-vessel
presentation: (1) **authored sprite swap** (`AbilityIconBinding.upgradedSprite`, restored on re-lock —
authored art only, never runtime-generated); (2) the **element badge** — that element's petal in the
level-5 white from `ElementalBarsConfigSO`, blooming in / withering out per the continuity law, and a
*child* of the icon so per-frame icon repaints can never stomp it; (3) an optional **tint + persistent
scale bump** with a one-shot unlock punch.

- **Icons that are live gameplay gauges** (cooldown fill, heat tint, drift lean, impact flash) set
  `tintIconOnUpgrade = false` — never overload a gauge colour with upgrade meaning — and their view
  **must** override `SetAbilityUpgraded` to re-anchor its captured rest scales to
  `AbilityIconRestScale(element)`, or its own tweens settle back to the pre-upgrade scale and wipe the
  bump. `SquirrelVesselHUDView` is the reference implementation.
- **Fleet status** (audit it yourself: **FrogletTools > Vessels > Audit Vessel Ability Rows**, which
  reports every vessel's compliance against this contract from assets alone, no play mode):

  | vessel | map | icons | order | uniform | control chip |
  |---|---|---|---|---|---|
  | Squirrel | complete (4/4 named, 4/4 upgrades; **RE-CUT 2026-09-24** — every row moved: Charge=Crystal Joust / **Shepherd**, Mass=Boost Ring / Twin Rings, Space=Steal / **Iron Grip** (a shielded prism is stolen outright and KEEPS its armour, via the `superSteal` parameter that had been in `PrismTeamManager.Steal` all along with nobody passing it), Time=Skimming / Live Wire. **Charge scales the joust's STEAL** (filled 2026-09-28 by design request, having shipped as a deliberate hole): `VesselOvertakeBySkimmerEffectSO.stealScale` multiplies the petals an overtake takes off an opposing pilot, ×1 at rest (the priced Strike, 0.8 petal per element) → ×2.5 at level 10 (2 whole petals per element), read off the THIEF's REPLICATED level because the steal also runs on the victim's machine and element levels never replicate. Opponent branch only; the Rhino's sword shares the type and authors it disabled. Mass's old row retired to BASE: the trail is fixed at 1.35 (`trailVolume` `Enabled: 0`, which `EvaluateLive` returns as `Value` — no code) and **drifting lays shielded prisms at every level** (`massUpgradeShieldsTrail` → `driftShieldsTrail`; the Manta's `turnUpgradeShieldsTrail` keeps ITS Mass-5 gate). Two HUD bindings were retired rather than re-homed — the drift sprite (core flight, no element, and it sat on the card the ring now occupies) and `overheatIcon`, whose drivers had had no callers since the Sparrow's overheat mechanic was deleted. (That stated cost — *no drift readout on the HUD* — is CLOSED: the drift icon's response came back on its own core card, see the TENTH pass below.) **A second pass (2026-09-24) moved the ARTWORK, which the first had not** — re-binding the gauges and the cooldown moved where the meters DRAW and left every `m_Sprite` where it was, so each card showed the previous ability's icon; the row now binds the Image that already carries the right art (boost ring → Mass, steal → Space) and only ONE sprite changed, the retired drift placeholder becoming the project's own `objective_joust.png`. *Re-binding a card moves its METERS; the art is a different field and moves with neither.* That pass also pulled skimming out of the elemental row onto the fleet's first NON-ELEMENTAL card, which **a third pass (2026-09-25) walked back and replaced with the right occupant**: skimming is what TIME scales and what Live Wire upgrades, so a Time flower over a locked plate was a flower doing real work above an ability that did not exist — Time takes the skim icon AND `boostFill`, and the card with no flower above it becomes the **DRIFT** (`CoreAbility.Drift`, chip **LT** from the binding's own `input`), which is the thing on this hull that genuinely has no element. Charge takes the SKULL (`HuntIcon-PLACEHOLDER.png`, already in the tree), and **SPACE is GENERATED** — a `ScopeRingGraphic` whose radius IS the skimmer's live reach (Space scales it 15 → 30, and a steal reaches exactly as far as the skimmer does) with the running steal count inside it, tinted in the pilot's own domain because *a stolen prism changes hands to that domain*. Three decisions in it: the ring EASES (an element level moves in steps and a ring that stepped would read as a glitch), both values are POLLED (the reach is continuous with no event, and the count is a server-write `NetworkVariable` the owner reads back), and the bound icon is deliberately INVISIBLE so the card is not LOCKED and the lockup still has something to kern. **A fourth pass (2026-09-26) made the two generated cards say what they are MADE of, and a FIFTH the same day cut half of it back.** The Boost Ring lays DANGER prisms, so the icon wears the palette's danger rim (`SO_ColorSet.GetDangerSignalColor`) — that stands. A generated `PerspectiveTunnelGraphic` wore the TEAM inside it, a one-point-perspective tunnel with its vanishing point at the icon's centre, and it was **removed on a look call** and deleted rather than left unreferenced. The measurement that allowed it was sound (the sprite is a circle of eight prism BLOCKS whose middle is measured empty at r 0.520, so the two colours never touched a pixel — `Docs/PALETTE.md §4.3`'s *separated, never blended*), and the conclusion is the one to carry: **separated is what makes two hues legible; it is not what makes a second hue worth having** — on a card drawn at 60 units they still compete for the same glance, and the domain is already said by the trail, the flower above the card and the Space card's count. The steal count moved out of the reach ring to hang BELOW it (inside, it competed with the ring exactly when the ring was smallest, which is its resting state) at a size measured for FOUR digits off the shipped font — Aldrich's widest digit advances 49.641 at 68pt, so 20pt puts `0000` at 73% of the box, FIXED rather than auto-sized because a number that shrinks as it ticks over reads as a glitch. **The fifth pass's own finding is about SIZE: a card's rect and what is DRAWN inside it are two different questions, and the lockup only answers the first.** The Space card was reported oversized *like the time ones were* and the cause was NOT the kerning bug — `NormaliseIcon` reaches it correctly — but that `NormaliseIcon` kerns an icon's RECT and cannot see what a generated CHILD draws in it: the ring sat on the icon's centre at a radius that nearly filled the box with the count hung off the plate below, spanning 10% more than the plate itself. Fixed by LIFTING the ring off centre (`reachRingCenterY`) so the ring and the number split the box vertically instead of stacking out of it — point-anchored with an offset rather than stretched, because `ScopeRingGraphic` draws about its own `rect.center` and is SHARED with the Serpent's scope reticle, so the lift belongs to the card's layout and not to the component. *When a generated card reads oversized, check its CONTENT's extent before checking its kerning.* Two more findings. **A colour authored FOR A SHADER is not a colour a UI slot may read, and this is now three for three** — `DullCrystalColor` is black, `DarkCTA` a dark olive, and `EnvironmentColors.Danger` **HDR at 1.498 with alpha 0**; the shader composes, tolerates HDR and ignores alpha, a UI slot does none of those, so `SO_ColorSet.GetDangerSignalColor()` joins its two siblings (`Docs/PALETTE.md §2.6`) and the alpha is the nastier half, because a transparent tint is indistinguishable from a tint that never ran. And **a gate was lying**: `check_using_directives.py` reported a `using` that was on line 1, because `\ufeff` is Unicode category `Cf` rather than whitespace, so `^\s*using` could not match the FIRST using directive in any of the project's **74 BOM'd `.cs` files** — fixed with `utf-8-sig` plus two self-test cases, and *a false POSITIVE is the worse direction for a gate, because it is what teaches people to stop reading it.* The Space card's whole readout is measured against the icon's own box, the shared style asset and the font's advance table by `Tools/Build/check_squirrel_card_fit.py` (`--check`, `--self-test`, 6 negative controls, the first of which is the reported layout and fires). **A SIXTH pass the same day found that the four OTHER cards were the wrong ones.** The fifth pass fixed a real defect and the same report came back naming the PLATES, which content cannot reach — so the frame was measured instead of the source: at one scanline the four authored cards span 115 px against Space's 166, a ratio of **0.693**, and every Squirrel ability button is authored at `localScale 0.7`. `AbilityLockupView.PlaceHost` normalises a host to 1, but all four authored hosts carry `AbilityButtonPressJuice`, whose `Awake` cached that 0.7 long before the lockup ran and whose `OnDisable` wrote it back on every hide of the HUD; the GENERATED Space host carries no juice and kept the 1. So **Space was the only correct card** — *four wrong cards agree with each other, so the one that is right is what looks wrong.* The rule is **a rest scale cached before the thing that OWNS the layout has run is a stale rest scale**, and it fails by quietly restoring the old value rather than by doing nothing: it is the icon-level rule this row already carries (a view that runs its own scale tweens must re-anchor to `AbilityIconRestScale`) met one level up at the HOST, and it hid longer because the icon version breaks ONE card while this breaks every card except one. The juice now captures LAZILY (so it can only ever restore a scale it took itself) and `PlaceHost` hands it the new rest outright; `Tools/Build/check_rest_scale_capture.py` (`--check`, `--self-test`, 4 negative controls) fails any UI component that captures a scale in `Awake`/`OnEnable`/`Start`, and no prefab changed — absorbing an authored scale is what `PlaceHost` is for. Its reporting-loop companion: **when a fix lands and the same sentence comes back, the second report is evidence about a DIFFERENT system, not a weaker version of the first** — measure the picture before re-reading the code that draws it. **A SEVENTH pass the same day doubled the row's SPACING, fleet-wide, and it is one field**: with the four cards finally drawing at their real 104-unit plate width the air between them closed to the 12 the pitch lays out and the row read as one strip, so `AbilityLockupStyle.cardPitch` went **116 → 128** (gap 12 → 24, `plateWidth + 2 × cellGap` → `+ 4 ×`). *A vessel cannot author its own pitch*, so "do that for all vessels, consistently" needed no per-vessel work and the element flowers ride the same columns. The fleet audit behind the sixth pass is stated rather than assumed: the Squirrel is the ONLY HUD authoring a non-1 host scale and the only one carrying `AbilityButtonPressJuice`; Dolphin/Scarab/Sparrow host at 1 with nothing that touches `localScale`, Manta/Rhino/Serpent bind no icons, and no vessel prefab overrides a host's `m_LocalScale` — so the correction is complete today, while `PlaceHost` + the gate are what make it complete tomorrow. **The pitch now has a CEILING it never had**: both the tests and the auditor assert `(cards − 1) × cardPitch + plateWidth + rowMarginRight` stays inside the right half of the 1920 reference canvas (656 of 960 today, max pitch 204), with the card count read off `VesselHUDView`'s own two display orders so adding a core ability TIGHTENS the bound instead of silently invalidating it — which the **EIGHTH pass** the same day promptly did, adding the fleet-wide OMNI CRYSTAL card (a SIXTH column here, 784 of 960, still inside). That card is the row's first **emblem**: a core card now has an upper cell if the style names a mark for it, so the lockup draws three shapes and the upper cell is what differs — a flower is a level readout, an emblem is a NAME, and the drift has neither. This hull is the only one that authors a lower icon for it, a ring of eight 45°-turned squares baked from `AOEShieldedRingSpawner`'s own numbers because **a shielded prism reads as a DIAMOND where a bare one reads as a SQUARE** (an octahedron's cross-section is exactly the box's square turned 45° and grown to the shield's reach), tinted in the pilot's domain's shielded base face. Its honest cost: the boost ring and the shielded ring are nearly the same figure — 8 prisms at radius ~8 either way — so the two icons are each other's rotation and **colour is what separates them**, danger red against the domain's shielded hue. **A NINTH pass the same day found that hue was the NO-TEAM SENTINEL's** — `Domains.Blue` has a full row in `SO_ColorSet`, so an unresolved domain answered `(0, 0, 1)`: hue exactly 240° at saturation 1.000 with zero green, against Jade's 217°/0.82/0.489. *A sentinel that has a row in a lookup table gets a plausible answer, so a lookup that failed to resolve renders as a different team rather than as a failure* — worse than a black or transparent slot, which reads as unimplemented and gets reported. It was identified only because the report said it was **not Jade's** (*more green and less saturated*). The refusal is in the CALLER (`Blue` already MEANS unresolved in this codebase) and the accessor stays a pure palette read; the other half was the ordinary snapshot bug — the controller read `vesselStatus.Domain` once at `Initialize`, twice over, since `SetPlayerDomainColor` had the identical defect on the steal count and the boost fill, and both are now POLLED through one `RepaintForDomain` in the `Update` that was already running. `Docs/PALETTE.md §2.8`. **A TENTH pass (2026-09-26) found the joust card lighting at FULL SPEED, and the cause was fleet-wide**: `FullSpeedStraightAction` (the input enum's zero) is both a REAL event (every strategy raises it while the throttle is buried and the stick centred) and the "no button" sentinel a passive map entry is authored with, and `VesselHUDController`'s press resolver matched it to the FIRST `Input: 0` entry — the joust on Squirrel/Butterfly/Manta/Rhino/Scarab, the Mass card on Dolphin/Serpent/Urchin. The chip side already read the zero as "no control"; the press side now agrees (`IsPassiveSentinel`, `HudPassiveInputSentinelTests`), and *a value that is both an event and a sentinel will eventually be matched as the event by some lookup*. The same pass lit CORE cards from their binding's own input (they drew an LT chip and never lit on LT), restored the drift icon's response on the core card — sprite swap, tint, swell, and a lean fed per frame from the SHIP's frame so it tracks the side the nose actually swings to — and found the old sharp-drift wiring listened on `EventOnSharpDrifting`, which **nothing raises**, so that look had never fired (now `EventOnDoubleDriftStarted`, the channel the drift actions raise). An omni crystal pickup now lights the OMNI card (`VesselHUDView.PlayOmniCrystalCollected`: the plate's one-shot flash plus an icon punch toward white) instead of the joust icon it shared before the omni card existed. See `_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_ELEMENT_RECUT.md`) | 4/4 + 2 core | ✅ | ✅ | ✅ chip drawn. **Trap (2026-08-26):** the Time icon's sprite was authored in `SquirrelHUDVariant` **and** overridden on `Squirrel.prefab`'s nested HUD instance (`propertyPath: m_Sprite` on the icon's fileID), so the variant's value had been dead for as long as the override existed and editing it changed nothing on screen. The override is deleted; the variant is the single source of truth for all four icons. *An instance override beats the prefab asset — so when a HUD edit does not show up in play, dump the VESSEL prefab's `m_Modifications` for that component's fileID before re-checking the HUD.* Dump it as raw lines: the entry wraps `- target:` across two lines, so a one-line regex reports zero overrides and a 166-override instance reads as clean. |
  | Sparrow | 4/4 named, **4/4 upgrades** (Time re-scoped 2026-08: indefinite boost, base roll, Elemental Ward. **Mass L5 = Shielded Prisms again** — it briefly moved to Space 5 in 2026-08 round 4 and was returned by design sign-off on 2026-08-13, settling the split: **MASS owns the SUBSTANCE of what you fire** (turret prism stretch, in-flight round growth, armour) and **SPACE owns its REACH** (pierce on both fire modes — range was its other half until 2026-09-28, when the guns went FIXED-range at 1350 u/s / 257.8 u and the Space row became a known hole awaiting a re-cut — SPACE still scales the skimmer (20 → 40), so `element_ability_table.py` reports the row wired, but the ability it is NAMED for no longer reads it; the guns now run on HEAT, 0 to 30 in six even 5 s phases ending in a 37.5° collapse, which does not reset on release and cools at 5x (a full gun is cold in 6 s), with RANGE falling linearly with the cone to a quarter at full spread (`GunSpreadMath.RangeFactor`, applied to muzzle speed so rounds-in-flight is unchanged), drawn on the Space card as a gauge with phase marks — `SPARROW_SPRAY_ACCURACY.md` Rounds 7-8). In-flight growth is ONE curve — `ElementalScaling.RoundGrowthFactorForLevel` — reaching all THREE things this vessel fires, each with its own authored pair and its own SHAPE (`RoundGrowthRamp`): bullets and turret prisms grow across the whole flight at 3×/6×, the skyburst missile at 20× in the first FIFTH of its flight, then held. **The skyburst satisfies "growth is a hit volume, not a size" (the Projectile charge shell row) from the OTHER end**: it has a readable BODY worth growing and no growing hit volume for a shell to draw, so the MODEL grows and the sphere collider is FITTED to it every frame — radius = the model at its widest across the flight axis (never the box DIAGONAL, √2 too big on a round body), front surface exactly on the model's tip. **A model may stick out the BACK of its collider — a tail that has already passed you cannot cause a false read — but never out the FRONT.** `Projectile.flightGrowthTarget` selects that path (`ModelHitRadius`/`ModelHitCentre` do the fit, measured per flight from the target's own renderer bounds, hardcoding nothing) and is empty on every other round: a 20-long tracer STREAK is a smear, not a body, and growing it draws a cannonball. The reach change is real and deliberate — the old fixed 8.5 u sphere was `0.85 × ProjectileScale 10` arithmetic that dwarfed a 1.7 u model, and the fitted one is 3.81 u at resting Mass and now varies with Mass (`SPARROW_SKYBURST_BAY.md`)) | 4/4 | ✅ | ✅ | ✅ chip drawn |
  | Dolphin | complete | 4/4 | ✅ | ✅ | ✅ chip drawn |
  | Scarab | complete | 4/4 | ✅ | ✅ | ✅ chip drawn. **Correction (2026-08-25):** `Scarab.prefab` does reference **`ScarabHUDVariant`** (guid `4f3ce7d760a1e0c76f3bc8c6a6842a92`) — the earlier note here read a stale prefab-instance **name override** (`m_Name: SparrowHUDVariant`) as the reference. The override is deleted; `ScarabHUDVariant` is the live asset, not an orphan. *A prefab-instance name override is not a prefab reference.* **Sprites (2026-10-05):** the four icons carried the SPARROW's art (missiles / swap weapon / bullet / boost from `HUD UI/New_Sparrow`), matching none of its abilities; they now carry Scarab placeholders (Cavitation Blast / Switch / Ball Forge / Throttle, `*-PLACEHOLDER.png`, white so the view's gauge tints still read) from `author_hull_icon_placeholders.py`, swapped by `author_hull_ability_rows.py`. |
  | Urchin | complete (4/4 named, 4/4 upgrades; re-cut 2026-08-18 — Charge owns the merged spike weapon, Space the new track projector) | 0/4 | — | — | n/a — **no `UrchinHUDVariant.prefab` exists**, so `UrchinVesselHUDController`/`View` are unreferenced code |
  | Manta | complete (4/4 named, 3/4 upgrades — the 2026-08-26 spec remake: Charge=Sting bomb bay / Contagion, Mass=Yastri turn trails / Shielded Turn Trails, Space=Kabloom bloom scale / No Friendly Fire, Time=Soar max speed / **L5 open** (Wake Highway was built and cut 2026-09 after its first playtest — a boost ring read as an unexplained launcher); Sting and Kabloom are deliberately Input 0 — planting is grazing, detonation is a crystal pickup. See `_Scripts/Controller/Vessel/R_VesselActions/MANTA_STING_KABLOOM.md`) | 4/4 | ✅ | ✅ | ✅ chip drawn (placeholder silhouettes pending art pass) |
  | Rhino | 2/4 named, 0/4 upgrades (Time filled 2026-09 — **Ramp Spool**, the ramp's wind-up rate, which `RampBoostActionExecutor` had been reading all along against a flat 1.0/1.0 asset; Input 0 deliberately — the ramp engages on a full-throttle straight, not a button) | **2/4 + 2 LOCKED** (bound 2026-10-05: Mass = Trail Slabs, Time = Ramp Spool; Charge and Space are open design slots, bound with no icon so they draw LOCKED — the contract for a slot with no ability) | ✅ | ✅ | n/a (both bound abilities are passive). Placeholder silhouettes `*-PLACEHOLDER.png` from `Tools/Build/author_hull_icon_placeholders.py`; hosts and bindings in `RhinoHUDVariant` by `author_hull_ability_rows.py` |
  | Serpent | 3/4 named, **2/4 upgrades** (the 2026-09-16 scope + rifle re-cut: Charge=Sniper Shot on RT / **Pierce** (armour, not a count), Space=Scope on LT / **Deep Focus**, Time=**Solid Fuel Pellets** on A — restored 2026-09-25: one press burns one pellet, overlapping burns stack additively (4 at once = 4x the effect), a fuel tank that refills at a fixed rate and holds four, and a release no longer cancels a burn, which had made stacking impossible and zeroed every AI burn; see `_Scripts/Controller/Vessel/R_VesselActions/SERPENT_FUEL_PELLETS.md`, Mass still an open design slot). Its RIGHT TRIGGER is **contextual** — scoped it fires the rifle, unscoped it still cloaks, and each action asks `SniperScopeActionExecutor.IsScoped` rather than learning the other's wiring. See `_Scripts/Controller/Vessel/R_VesselActions/SERPENT_SNIPER_SCOPE.md` | **3/4 + 1 LOCKED** (Time — the four-pellet fuel icon, whose pips ARE the tank; Charge = Sniper Shot and Space = Scope bound 2026-10-05 to `*-PLACEHOLDER.png` silhouettes from `author_hull_icon_placeholders.py`, hosts in `SerpentHUDVariant` and bindings on the view `Serpent.prefab` adds, both by `author_hull_ability_rows.py`) | ✅ | ✅ | ✅ chip drawn on Charge/Space/Time; Mass (open design slot) renders LOCKED; since 2026-09-16 the Charge card's cooldown veil DOES draw on one (the veil sizes itself on the ability plate, which a locked card has), and the scope additionally draws its own generated reticle + recharge ring inside its eyepiece |
  | Butterfly | complete (4/4 named, 4/4 upgrades) | **4/4** | ✅ | ✅ | ✅ chip drawn. Bound 2026-09-25 — the view and controller were complete from day one and the HUD variant bound NOTHING (`abilityIcons: []`, `wingEnergyGauge: {fileID: 0}`), so all four cards drew LOCKED and the Fold's recharge veil swept a bare plate. Placeholder silhouettes pending art pass, authored by `Tools/Build/author_butterfly_icon_placeholders.py`; the row and the bindings by `author_butterfly_ability_row.py`, into the **HUD variant** rather than the vessel prefab (the Squirrel's override trap). **The Mass meter is bound TWICE to one Image and both are required** — the view WRITES through `wingEnergyGauge`, the lockup ADOPTS through the binding's own `gauge`, and binding only the field leaves `RetireLegacyChrome` switching off a correctly-driven meter. Since the 2026-09-26 re-cut that Image is a MODE fill (full = Mass mode, empty = Dust mode), not an energy meter |

  **A vessel may also carry NON-ELEMENTAL cards, and they go to the LEFT of those four.** An ability
  no element upgrades — a hull's engine, always available, that the four elemental ones spend — is
  keyed on `CoreAbility` and drawn by the lockup as an ability plate with an **emblem** above it or
  nothing at all — never a flower — in `VesselHUDView.CoreAbilityDisplayOrder`, one card pitch apart. The separation is the whole
  point: the elemental row must go on reading charge → mass → space → time with nothing interleaved,
  or *"which flower do I fill to upgrade this?"* stops being answered by position. `PlaceHost`'s slot
  index is signed (`0..3` elemental, negative for core), so adding one pushes the set further left
  and **no elemental column moves**. There is deliberately no `SetCoreAbilityUpgraded` — an upgrade is
  an element reaching level 5 and a core ability has no element. There are **two** today. The
  **Squirrel's drift** (`CoreAbility.Drift`) is the bare case — no upper cell at all, and no other
  vessel binds one. The **OMNI CRYSTAL card** (`CoreAbility.OmniCrystal`, 2026-09-26) is the emblem
  case and is on **EVERY vessel**, structurally: `VesselHUDView.EnsureOmniCrystalCard` is not
  virtual and not opt-in, because a card a vessel can forget is a card most vessels will be
  missing and every hull can fly through a crystal. Its upper plate carries the crystal's own mark
  from ONE authored row on the fleet-wide style (`AbilityLockupStyleSO.coreAbilityEmblems`), drawn
  **untinted** — an omni crystal belongs to nobody until somebody takes it — while its lower plate
  is per-vessel (`omniAbilitySprite`) because what a crystal DOES is a property of the hull. Two
  hulls fill it today: the Dolphin GENERATES its lower plate (the blast's prism tally, re-homed by
  `DolphinVesselHUDView.EnsureGeneratedAbilityIcons`), and the Squirrel authors one (a ring of shielded prisms, baked by
  `author_squirrel_shielded_ring_icon.py` as the octahedron's own cross-section — *a shielded prism
  reads as a DIAMOND where a bare one reads as a SQUARE*, so the icon says "shielded" by drawing
  the armour — and tinted the pilot's domain's shielded base face through the new
  `SO_ColorSet.GetShieldedSignalColor`, `Docs/PALETTE.md §2.7`); the other six render LOCKED,
  which is the honest state and what a locked card is for. **It shipped painted the NO-TEAM
  SENTINEL, and that is the finding worth more than the card**: `SO_ColorSet` authors a full
  `DomainColorSet` for `Domains.Blue` and `TryGetColorSetByDomain` returns it, so a domain that had
  not resolved yet answered with `(0, 0, 1)` — hue exactly **240°** at saturation **1.000** with zero
  green, against Jade's 217°/0.82/0.489. **A sentinel that has a row in a lookup table gets a
  plausible answer, so a lookup that failed to resolve does not render as a failure — it renders as
  a different team**, which is strictly worse than §§2.4-2.7's traps: a black or transparent slot
  reads as *not implemented* and gets reported, while a saturated wrong hue reads as *implemented
  and mis-tinted* and gets rationalised (it survived a whole round that way, and was identified only
  when the report said it was **not Jade's** — *more green and less saturated*). The refusal belongs
  to the CALLER, because "no pilot can fly Blue" is a fact about PILOTS while a neutral mine
  legitimately wants no-team's colour, so the accessor stays a pure palette read; and `Blue` already
  MEANS unresolved in this codebase (`EchoSightActionExecutor` and `SniperShotActionExecutor` both
  write `status?.Player != null ? status.Domain : Domains.Blue`), which is the whole reason it must
  never be asked for one. The second half was the ordinary snapshot bug the domain rule above states
  outright: the controller read `vesselStatus.Domain` once at `Initialize` — **twice**, since
  `SetPlayerDomainColor` had the identical defect on the steal count and the boost fill — so it held
  whatever was true one frame after spawn, before the pick replicates. Both now go through one
  `PushPalette`, POLLED in the `Update` that was already running (free, and no subscription to
  tear down against a `Player` a vessel swap replaces), gated on `Player` being present because
  `IVesselStatus.Domain` LogErrors when it is not. **That retry then singled out JADE, which is the
  finding's second half**: the latch recorded which domain it had ATTEMPTED to paint rather than
  whether the paint LANDED, and a palette resolve legitimately fails while `ThemeManagerData` is
  unavailable during the spawn chain — so the retry was gated on the domain CHANGING, and **Jade is
  `NetDomain`'s own initialiser**, the only value that can already be the recorded one (Ruby and Gold
  always arrive as a change and always repaint). *A latch that records its input rather than its
  outcome fails on exactly one input — whichever one is the default — so it presents as a bug about
  that input rather than about the latch*, the second outing of `/vessel` rule 6 from a new
  subsystem. `PaletteLanded(domain, resolved)` is a one-line pure static with its OWN suite
  (`SquirrelHudPaletteLatchTests`) because replacing it with `true` is a **logic** regression: the
  Roslyn harness compiles it clean and all eight textual gates pass it (measured — 7/7 assertions
  pass the fix, 4 fail the regression). And the reason none of it was verifiable by eye is a palette
  fact: **Jade's shielded tier is blue on both halves** (base 217.4°, rim 222.7°) and sits 22.6° from
  the sentinel's 240°, while Jade's *identity* teal is 41° away in the other direction — so
  white-when-unresolved is the ONLY thing separating "this is Jade" from "this never resolved".
  **And the colour was STILL wrong, for one reason underneath both remaining rounds: a palette float
  is a LINEAR intensity (`m_ActiveColorSpace: 1`) and a UI `Image.color` is GAMMA.** Measured off a
  screenshot, a UI colour maps 1:1 to display bytes (`blueColor` (0.220, 0.510, 1.000) renders as
  exactly (56,130,255)), so handing `ShieldedOutsideBlockColor` to an Image skipped the conversion
  entirely. `Color.gamma` is the fix and **the proof is the HUE**: converted, Jade's base face is
  (83, 134, 185) at hue **210.3°** against the prisms' measured **209.4°** — under one degree, where
  the previous answer sat at 217.3°. *That 8° had been written down as an ACES hue shift; it was the
  missing conversion, and a wrong hypothesis does not land within a degree.* Two corrections layered
  on top are deleted because both were compensating rather than doing a job: a peak NORMALISATION
  justified as *"the authored colour is too dark for a UI slot"* — **it is not dark, it is linear** —
  and a 0.25 lerp toward white modelling bloom + ACES on top of that. The normalisation is also what
  manufactured the second symptom: it pushed the hue to 217.3°, which is
  `ElementalBarsConfigSO.blueColor` to within **0.3°**, i.e. the colour that means *two upgrades in*
  on the very row the icon sits on. Converted honestly it is legible (brightness 0.725) and 0.230 of
  saturation clear of that rung. Shipped Jade (83,134,185) / Ruby (156,113,183) / Gold (152,126,81),
  gated by `ShieldedSignalColorTests` against the shipped assets. Three rules: **a colour that looks
  too dark for UI may just be in the wrong space** — reach for the conversion before a brightness
  correction, since one tuned on an unconverted value is tuned on a different colour and moves hue
  too; **the five `ElementalBarsConfigSO` ladder colours are the HUD's VOCABULARY** (fire = deficit,
  grey = 0, white = +1, blue = +2, lime = +3) and any new HUD tint is checked against them; and
  **when a report is about a COLOUR, sample the frame before reading the code that sets it** — two
  colours 0.3° apart are identical in a diff and different on a screen
  (`Docs/DIAGNOSTICS.md`'s *Report On-Screen UI* rule, one step out). ⚠ The three sibling
  `*SignalColor` accessors still normalise a linear value and are deliberately unchanged: their job
  is an unmistakable SIGNAL rather than a world match, and the error's size grows with how far apart
  a colour's channels are (Jade's shielded base shifts 7°, its trail highlight 0.2°).
  `Docs/PALETTE.md §2.8`, `§2.9`. Two more findings travel
  with it. **An
  extension point that has only ever had one user has only ever been tested for that user's
  shape**: the lockup's core pass `continue`d on a binding with no icon where the elemental pass
  falls back to a locked host, so the card would have VANISHED on seven hulls rather than locking —
  both now share one `ResolveLockedHost`. And **a survey that mis-parses reads exactly like a
  survey**: the first "which hulls do something with an omni crystal" pass reported the Dolphin and
  Urchin as empty, both do something, and the wrong answer reached two doc comments before it was
  re-measured — plausible because the Scarab really IS empty (by design; its skimmer forges the
  crystal into a ball before the hull reaches it), so one right row carried two wrong ones. **Its control chip comes from the BINDING** (`CoreAbilityBinding.input`),
  not from the ability map, and that follows from where the fact lives: an elemental card's control
  is authored on its `ElementalAbilityMapSO` entry, and a core ability has no entry at all — both
  are still ONE authored fact with the glyph derived from it. Two rules came out of building it.
  **A value applied only by an EVENT is missing on everything that event does not reach**: the
  lockup's icon kerning was written solely by `SetAbilityUpgraded`, which the controller seeds per
  ELEMENT, so the first non-elemental card drew at its authored 80 in a cell sized for 60 — a third
  larger than its four neighbours, on the one card in the row nothing else touches, with every
  authored rect identical and therefore nothing in the prefab to find. It is now written by
  `AbilityLockupView.NormaliseIcon` as the row is laid out. And **a vessel may GENERATE an ability
  icon rather than author one** (`VesselHUDView.EnsureGeneratedAbilityIcons` +
  `BindGeneratedAbilityIcon`, called from `Build`), because some readouts are a live MEASUREMENT
  rather than a picture and a sprite ladder quantizes one and silently stops matching it — the
  Squirrel's Space card is a `ScopeRingGraphic` whose **radius IS the skimmer's live reach** with
  the running steal count inside it. An authored icon always wins, so a generated one can never
  overwrite a prefab's art; the stated cost is that the ASSET no longer shows the whole row, so the
  lockup auditor names the unbound slots and says it cannot tell *undesigned* from *generated*
  apart. `Docs/ABILITY_LOCKUP.md` § "Non-elemental cards".

  **EVERY vessel HUD now wears the ABILITY LOCKUP** (`Docs/ABILITY_LOCKUP.md`) — the totem
  card that fuses each icon with the element flower that upgrades it. It is **structural, not
  opt-in**: `VesselHUDController.Initialize` calls `VesselHUDView.EnsureAbilityLockup`, the one
  method every vessel HUD routes through, so a vessel cannot be authored without it and a new
  vessel inherits it BEFORE it has a single icon — a HUD that binds nothing still gets four LOCKED
  cards, so **Manta / Rhino / Serpent are on the fleet's UI today** with their open design slots
  drawn as slots rather than being left on the old UI. Audit with **FrogletTools > Vessels > Audit
  Ability Lockups**, which checks the shared style and — the things one shared style cannot absorb —
  whether each vessel's own icon size still fits the card after kerning, and which card each bound
  gauge is actually authored under.

  The Dolphin deliberately runs with **both** `tintIconOnUpgrade` and `showUpgradeBadge` off —
  all four of its icons are live gauges, so the persistent scale bump is its only upgrade
  signal, which is why nothing in `DolphinVesselHUDView` writes an icon transform per event.
  Its Space slot **does** tint — the jaw pair blends to `ElementalBarsConfigSO.limeColor` over
  the top 15% of banked skim energy — but that is a GAUGE colour carrying gauge meaning, and it
  lands on the jaw halves, not on the row's (fully transparent) Space icon, so it never collides
  with the upgrade path. Reading it as an upgrade tint is the mistake to avoid.
  Mechanics: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.
  **Since 2026-08-17 the whole map is cut around ONE weapon**, because the Dolphin has
  essentially one offensive act — bank energy by skimming, fly into a crystal, release a cone —
  and each element now owns one ORTHOGONAL DIMENSION of it, so the four-icon row reads left to
  right as the whole weapon: **energy → gape · Charge → thickness · Space → reach · Mass → when
  the next crystal arrives · Time → the boost that gets you there**. Charge owns the **Echo
  Sight** (RT) *and* the blast's capsule DIAMETER (`0.75x` the authored core at rest rising to
  `1.5x` at level 10) — the profile you are widening is the profile the sight draws — and since
  `halfLength + radius` is always `maxScale / 2`, Charge does not enlarge the blast, it
  REDISTRIBUTES its extent, trading a long thin fan for a short fat capsule. That pair is the
  fleet's first use of **`ElementalScaling.MultiplierFromRest`**, the opt-in un-anchored twin of
  `Multiplier`: the default anchors at exactly 1 at the resting level so an element can only ADD
  to a vessel's baseline, and handing an element a parameter's whole RANGE means the authored
  value becomes what a MID-level vessel gets — a real, deliberate baseline change, not a bug.
  Charge 5 ("Pilot Echo") extends the sight from mass to PILOTS, and **a highlight competes with
  everything else the same trigger lights up** — the first version only raised `_ColorMultiplier`
  and was invisible in Rampage, because the sight lights all ~9,800 cactus prisms at once so
  brightness was the one channel already saturated, and a hull tint says nothing at all about a
  pilot standing BEHIND mass. It now marks a vessel two ways, each covering a case the other
  cannot: the hull is driven to its own **saturated domain colour** (`_Color1`/`_Color2` as well as
  `_ColorMultiplier`, lerped from each material's own authored values, so it is a shift and a Ruby
  pilot can never read as Jade — HUE is what separates a ship from lit mass), and an additive
  **halo** (`EchoSightHalo.shader` — a soft disc with a hard RING at the hull's silhouette) drawn
  `ZTest Always` so it reads through prisms and in empty space. Three render states there are
  load-bearing: `ZTest Always` (the only way "behind mass" can read), `Blend One One` (can only ADD
  light, so it never darkens what it marks and never needs a sort order), `ZWrite Off` (can never
  occlude the world). It is hand-written ShaderLab because Shader Graph cannot express "ignore the
  depth buffer" on a URP Unlit target; it billboards in the VERTEX shader from the object origin so
  the halo costs no per-frame CPU transform write and one shared unit quad serves every size (the
  radius is a shader property, never a transform scale); and it is sized by
  `PrismOcclusionCorridor.MeasureCircumscribedRadius`, the corridor's own hull measurement, so a new
  vessel of any size is correct with nothing authored. **A locator must not obey perspective** — a
  world-sized disc vanishes exactly when it is most needed, so the radius is
  `max(what it subtends at this depth, a screen-space FLOOR)`: hull-sized and silhouette-tracing up
  close, constant angular size past the crossover (measured 59 px at 1080p out to the 2400u max
  reach, vs ~20 px before). That is why the offset is applied in CLIP space and pre-multiplied by
  `w` — surviving the perspective divide is what turns a world size into a screen size — and why
  the x offset carries the inverse aspect. The cost is that the ring stops tracing the silhouette at
  range and becomes a reticle, which is the correct trade: the trace separates a ship from mass it is
  tangled in (a close-range problem) while at range the job is only "there is a pilot over there".
  **The sight's RANGE gate needs nothing added** — `BlastVolume.Height` is already the Space-scaled
  cone reach and both consumers reject past it, and fauna/flora are already covered because a
  creature's body prisms are `HealthPrism : Prism` and draw with the two graphs the sight is spliced
  into; crystals are the one thing it does not reach (`DOLPHIN_CRYSTAL_SEEDING.md` §11). **Per-vessel CPU is correct there and would
  be a violation on prisms** — the prism half of the same sight is a global uniform only because
  there are tens of thousands of them; a dozen vessels already individually simulated, lit only
  while a trigger is held, is the ordinary tool. Both halves share ONE predicate
  (`BlastVolume.Contains`, the CPU transcription of `AOEConicSweepQueryJob`), so a highlighted
  vessel and the prisms around it light up together.
  **Mass took crystal seeding** off Charge (recharge multiplier renamed
  `cooldownMultiplierAtFullMass`), **Twin Seed is retired** — one crystal per cycle at every
  level — and Mass 5 ("Claimed Seed") changes the seed's TIER instead of its count: below it the
  seed is a free-for-all OMNI crystal wearing the lime CTA, so your own ammunition stands in open
  space for whoever reaches it first; at Mass 5 it lands TEAM-locked. Both halves of that gate
  move together — the prefab swap (`OmniCrystalImpactor` → `TeamCrystalImpactor`) AND the
  `ownDomain` stamp, which is simultaneously `Crystal.CanBeCollected`'s gate and what
  `ResolveActivationMaterial` paints from, so a crystal always LOOKS exactly as collectable as it
  is (`Docs/PALETTE.md` §2.2). **Mass gave up the trail entirely** (`trailVolume` disabled,
  `massUpgradeShieldsTrail` off — the machinery stays, it is the Squirrel's Heavy Trail, it is
  just no longer wired here). Its HUD row was re-cut to match: Charge draws a **procedural**
  blast-profile capsule (`BlastProfileGraphic` — a sprite ladder would quantize a continuous
  function of two live meters and silently stop matching the blast on the first retune), Mass the
  seeding recharge, Space the jaws, Time the boost ring — and the prism tally lives on the
  non-elemental **OMNI CRYSTAL card**, centred, held until the NEXT blast replaces it (the card is
  "what this hull does with a crystal", which for the Dolphin is the blast; the authored text is
  re-homed there at runtime by `DolphinVesselHUDView.EnsureGeneratedAbilityIcons`). **The omni card
  reports what a blast did to MASS and Charge what it did to the LIVING** — pilots debuffed and creatures
  killed, two stacked bare numbers in the prism tally's own grammar, told apart by palette colour
  (pilots in `whiteColor`, the colour the engaged sight wears; creatures in `blueColor`, the
  neutral-lifeform range a living heart already wears). The two counts arrive differently and the
  asymmetry is the lesson: the blast can report PILOTS itself (`ExplosionImpactor` keeps a per-blast
  vessel ledger, so a target loitering in a growing cone counts once, and `OnBlastResolved` now
  carries a `BlastTally` struct so the next quantity is an added field rather than two silently
  reordered ints), but it cannot report CREATURES — a creature dies when its last body prism is
  destroyed and the ECOLOGY announces that several steps downstream
  (`CellRuntimeDataSO.OnFaunaKilled`, carrying the killer's NAME), so fauna are counted over the
  blast's own lifetime between the new `OnBlastBegan` and `OnBlastResolved`. That window is exact
  only because the blast is the Dolphin's ONLY prism-destroying force, and two blasts overlapping
  inside the 0.15 s cooldown would share a count — fine for a tally, **never** for scoring, which is
  `StatsManager`'s job off the same channel. **Colour is a
  LANGUAGE across that row, not per-icon decoration** (second pass, same day): the Charge profile
  crosses the shared `ElementalBarsConfigSO` ladder's **grey → white** — already the HUD's words for
  "not in use" / "in use", since a petal steps through exactly those two between levels 0 and 1 — and
  the Mass slot crosses **lime → the pilot's own DOMAIN colour**, because the upgrade's whole point
  is that the seed becomes a TEAM crystal, so the slot says which team. It uses **`SO_ColorSet.GetDomainSignalColor`** — the domain UI colour with its
  brightest channel driven to 1 — resolved LIVE off `GameDataSO.ThemeManagerData.ColorSet`, the path
  every other domain-tinted UI reads, so the domain-changer toy re-colours it and nothing is
  snapshotted at component-creation time. **A crystal colour is NOT a domain's UI colour**: the slot
  first sampled `DullCrystalColor` on the sound reasoning that the icon should wear what the crystal
  wears, and rendered BLACK — that field is authored `(0,0,0)` on Jade, Ruby AND Gold, which is right
  on a faceted crystal (a near-black body with a bright fresnel rim) and unusable in UI, while
  `BrightCrystalColor` tops out at value 0.75. The new accessor returns white for an unauthored
  domain, because a colour accessor that can return black can make a UI element vanish, and a
  vanished element reads as "not implemented" rather than as "mis-tinted" (`Docs/PALETTE.md` §2.4). A **Space reach bar was tried and dropped**: reach only moves when the
  element moves, so a near-static line competed with two live gauges, and the slot says more by
  saying only ANGLE and AMOUNT. One general lesson from the same pass: **a centre-fan triangulation
  of a generated `MaskableGraphic` is only as good as its outline ORDERING** — the profile's caps
  were swept from the wrong basis vector, so the outline jumped across the shape and the fan drew a
  bowtie with hollow wedges; a mis-ordered outline does not fail, it renders a plausible wrong shape,
  so check for a simple convex loop (area, and that no step between consecutive vertices crosses the
  interior) rather than for "vertices roughly in the right places". Record:
  `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` §8.
  Before that, **from 2026-08-14 its Charge ability was PASSIVE and its Space ability owned the
  right trigger** — crystal seeding runs on a cooldown loop that plants crystals in the cell's
  CYTOPLASM (volume-uniform across the band, never inside the nucleus, and at the live cap the
  clock PAUSES rather than culling — not creating mass is allowed, aging it out is not), which
  freed RT for the **Echo Sight**: hold it and every prism inside the crystal blast's live
  destruction volume lights up. It touches nothing but photons — no camera write, no speed
  change, and it cannot destroy, move or protect a prism. (A zoomed first-person view was built
  alongside it and **cut**:
  it would have needed the speed tunnel to grow a public FOV-home surface for one vessel's view
  effect, and the highlight carries the ability on its own. If it is ever revisited, the one
  safe shape — move the tunnel's HOME, never `Camera.fieldOfView`, which a live tunnel
  overwrites every frame and then bakes in permanently as the home it restores to — is recorded
  in `DOLPHIN_CRYSTAL_SEEDING.md` §2.) One general lesson: **a passive ability is bound to no
  input event, so `CollectBoundActions` can never resolve its SO** — wire the config directly on
  the executor; the binding sweep is a fallback, not the path.
  **A SUPER-SHIELD inside your own cone glows in the DANGER colour** (2026-09-28), because a
  crystal blast that reaches one ENDS there (`PrismSpatialIndex.ResolveExplosionHit`,
  `shouldContinue = false`, before the domain test — your own super-shields stop it too). A
  super-shielded prism wears the plain team material, so the fact rides a per-prism STATE bit
  (`_PrismSuperShielded`, `Prism.SetSuperShieldMark`, set by the stellated shield in both
  directions — never per frame) and the colour is the palette's own danger colour, published by
  `PrismLit`. Own sight only; a rival's cone flags nothing. `DOLPHIN_CRYSTAL_SEEDING.md` §16.
  The prism highlight is the second citizen of the §4.7 global-uniform shape
  (`Docs/PRISM_ANIMATION.md` §4.7.1) — five globals per frame, zero per-prism CPU, and the
  previewed volume is built by the same helper the detonation uses so the two cannot drift. **It
  lights WHOLE prisms, and that is a correctness fix rather than a look preference**: the volume test
  samples the prism's own ORIGIN (from the object matrix, the idiom `PrismClockAnimation.hlsl`
  already uses) because `AOEConicSweepQueryJob` tests exactly one point per prism and destroys the
  whole prism — a per-fragment test paints the geometric intersection, which is a shape the blast
  does not operate on. It is also cheaper: the branch can no longer diverge across a prism. **A
  highlight's colour has to stay out of the palette's language** — the cast was a warm amber
  precisely because no tier owns warm, and moving it to a pale cool blue (2026-08-17, at gain 1.15 →
  0.70) enters the shielded tier's neighbourhood, so it is held clear by being DESATURATED (S 0.55 vs
  a tier's 0.9+) and by a gain low enough that the prism's own tier shows through the cast; if a lit
  shielded prism ever reads as a tier change, lower the gain before touching the hue.
  **Since 2026-08-19 EVERY player sees it, and a rival's cone wears that pilot's DOMAIN colour** —
  the sight was local-only on the reasoning that it is a thing the pilot looks through, which was
  right about the camera and wrong about the arena: mass is the shared object both Dolphin-only
  modes are fought over, so "which prisms is that rival about to take" is the most useful fact on
  the field, and the jaws already telegraph the aim. **Your own cone is untouched and wins outright
  on every prism it covers** — a rival sweeping across your mass cannot recolour, brighten or dim
  it, because an instrument that changes appearance when somebody else moves is one you cannot
  read; peers only ever mark mass your own sight is not marking, and blend among THEMSELVES by
  weight-averaged hue at the brightness of the strongest, never summed (four overlapping cones
  would otherwise blow the arena white exactly where the fight is). Hue is the right channel for a
  peer even though the sight otherwise stays out of the palette's language, because "whose is
  this" is the one question the platform always answers with domain colour — held clear by
  desaturating toward white, so it reads as coloured LIGHT rather than the prism changing team.
  Three things generalise. (1) **The trigger needed no new networking**: `R_VesselActionHandler`
  already round-trips every press/release through the server, so the executor was ALREADY running
  on every peer's replica and only an `IsLocalPilot` guard discarded it — check that channel before
  building one. (An ability bound under a device override rather than the shared map would resolve
  against the OBSERVER's input device, so it would not replicate consistently.) (2) **The cone's
  SIZE did need replicating** (`NetEchoSightShape`, owner-write, 3 floats, ~0.5%-change gated),
  because element levels never replicate and a crystal's effects are replayed to the OWNER alone,
  and because banked skim energy is simulated locally and never SPENT remotely — so a third
  client's replica would draw a cone of the wrong reach, thickness and gape. Only the scalars
  travel; the apex and axes come off the already-replicated transform, so a peer's mark turns at
  full frame rate and only resizes at the tick, and **a peer with no shape yet draws nothing**
  rather than guessing. (3) **A bounded bank of N globals is still O(1) in prisms** — four array
  slots packed once per frame in `LateUpdate`, frame-stamped so a despawned ship cannot leave a
  cone burned in; the arrays must be declared at FILE SCOPE in the HLSL (Shader Graph has no array
  property type — which is why this needed no graph edit) and OUTSIDE every CBUFFER. The Charge-5
  PILOT highlight stays local on purpose: prisms are shared because mass is the shared object, a
  mark on a person is not. Composition is proven by compiling and RUNNING the shipped HLSL
  (`Tools/Shaders/verify_prism_sight_composition.py`) — which is what caught that routing your own
  sight through the same weighted average was algebraically identical and **not bit-identical**
  (`x/x*x` rounds; 3,381 of 89,301 lit samples drifted).
  **The AI holds it too** (same day): `AIPilot` lights a vessel's aim telegraph while it is
  DRIFTING and its course is locked on its objective — the commit window, where the cone's
  direction is already decided — so an AI's blast is as readable as a human's rather than the
  only unannounced one on the field. It asks for a CAPABILITY, never for a vessel:
  **`IAimTelegraphAction`** marks an action whose whole effect is showing others what you are
  lining up, and `R_VesselActionHandler.TryGetInputForAction<T>` answers which control this hull
  puts it on — so the AI that flies all eleven vessels names neither the Dolphin nor a trigger,
  most of the fleet is a silent no-op, and the next telegraph opts in with one interface. The
  interface carries no members but does carry a CONTRACT, because the AI holds it blind: no
  cooldown, no resource, no ammunition, no effect on motion. Three general findings came out of
  it. (1) **Replicate an AI's press when the ability's output does not already ride some other
  replicated channel** — an AI pilot runs SERVER-ONLY, so its local `PerformShipControllerActions`
  is right for the drift (motion, and the transform already replicates) and useless for photons;
  hence `PerformShipControllerActionsReplicated`. (2) **Owner and local pilot coincide for every
  human and diverge for every AI**, so a gate that conflates them works perfectly until something
  autonomous uses it — `NetEchoSightShape` was published on `IsLocalPilot` and therefore never for
  an AI, whose sight could then draw on no machine at all including the host's. (3) **A behaviour
  loop needs its own re-goal event**: the AI's `UpdateCellContent` was driven only by the cell's
  `OnCellItemsUpdated`, a CRYSTAL event rather than a "this pilot needs a new target" event, so an
  AI that overshot kept circling a crystal it could no longer reach; it now re-seeks once per
  commit cycle (latched, because `IsDrifting` does not fall on the frame the control is released).
  Both modes with AI Dolphins get all of this from `AIPilot` with no per-mode code.
  Detail: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` (§14, §15).

  Manta / Rhino / Serpent are blocked on **design, not wiring**: their
  `ElementalAbilityMapSO` entries are still `(open design slot)` with `Input = 0` and no
  `UpgradeLabel`, and those slots are bound with no icon so they draw LOCKED cards (the designed abilities beside them are bound). Author the map
  (`Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 holds the un-approved proposals) and the icons
  before wiring — do not invent an element→ability mapping to satisfy the audit. Once the map
  exists, the mechanical half is one click: **FrogletTools > Vessels > Wire Vessel Ability Row**
  (`VesselAbilityRowWirer`) places the four buttons at the fleet-standard bands, creates a
  `{Element}Icon` in each, and binds `abilityIcons` in `AbilityDisplayOrder` — on ANY vessel, from
  nothing. It is idempotent (find-by-name, re-bind only) and never touches sprites, so it is a
  repair path as well as a bring-up path. A slot whose gauge is authored art is ADOPTED by name
  rather than re-created, and a vessel with its own live gauges adds a per-vessel step there (the
  Dolphin's is the only one today).
- Full reference: `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.1. The `/vessel` skill
  encodes this contract (plus the rest of the per-vessel checklist) — use it for any vessel work.

**The control chip is DRAWN by the card, from one fleet-wide glyph set — a vessel authors no
glyphs.** Each card derives its own artwork: the ability → its `ElementalAbilityMapSO` entry's
`InputEvents` → `InputHintBindingMap.BindingFor` → the physical control → its sprite (pad) or label
(keyboard) in `ControlGlyphSetSO` (`Resources/ControlGlyphSet`). Reassign an ability to a different
input, or move an icon in the row, and the chip follows on its own; a wrong label is structurally
impossible. Blank is the honest state for a passive ability and for a pad button with no keyboard
equivalent — a pad glyph shown to a keyboard player is the misinformation this replaced. The chip
wears the control's HELD art off the card's existing press path, so the card lights the ability and
the chip lights the button, one press.

**`InputDeviceIconSetSwitcher` is a pure DETECTOR and draws nothing** (627 lines → 138). It used to
own authored per-device glyph roots, a per-set hint list it lit and tinted, and a pass that placed
each hint onto its ability icon; all of that is retired, because a second set of glyphs competes
with the card's chip no matter who toggles it — and on the three `VesselHUDPrefab` variants the
switcher's own reference was the ONLY thing sparing those roots from the lockup's retire sweep,
while `ApplySet` re-activated them on every device change. *A reference from something the lockup
superseded is not evidence anything still uses it — including a reference held by the superseded
component itself.* Deleting the display half surfaced three defects it had masked: **`OnSetChanged`
was declared, subscribed and never raised** (so a chip could never follow a device change — *an
event nobody raises looks identical to an event nobody needs*); **the keyboard set was unreachable
on every vessel**, because `KeyboardSet()` fell back to Xbox whenever `keyboardTextRoot` was null
and no vessel wires one (*a fallback that protected an authored display keeps firing after the
display stops being authored*); and **`padGlyphHeld`/`heldColor` were authored and read by nothing**.
Do NOT hand-position control glyphs against a HUD layout, and do not re-author per-vessel glyph
sets — that is the brittleness this replaced. See `Docs/ABILITY_LOCKUP.md` § "Retiring the old UI"
and `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.2.
