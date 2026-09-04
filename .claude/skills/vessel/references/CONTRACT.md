# The Vessel-Class Contract — clause by clause

Ground truth for what every Cosmic Shore vessel must satisfy, how each clause is enforced, and
the failure modes already paid for once. Read the section for the subsystem you are touching;
read all of it when creating or completing a vessel. Where this file and the codebase disagree,
**the codebase wins** — fix this file in the same branch.

**Contents**
1. [Identity & registration](#1-identity--registration) — enum, prefab, containers, camera, SO_Vessel, telemetry
2. [Actions & executors (the R_ system)](#2-actions--executors) — stateless SOs, registry, input maps, AI parity
3. [The elemental ability map](#3-the-elemental-ability-map) — the per-vessel design record
4. [Element levels & level-5 upgrades](#4-element-levels--level-5-upgrades) — ResourceSystem, scaling, replication, laws
5. [The four-icon ability row](#5-the-four-icon-ability-row) — order, upgrade signal, hints, geometry
6. [Elemental petal bars](#6-elemental-petal-bars) — the required flower display
7. [Hull morphs & vessel animation](#7-hull-morphs--vessel-animation) — blend shapes, rest poses, rig swaps
8. [The HUD controller/view pair](#8-the-hud-controllerview-pair) — lifecycle, gating, binding discipline
9. [Impact effects & skimmers](#9-impact-effects--skimmers) — containers, self-guards, joust
10. [Docs & paper trail](#10-docs--paper-trail) — trackers, design docs, verification ledger
11. [Fleet status snapshot](#11-fleet-status-snapshot) — dated; verify before trusting

Naming trap that applies everywhere: file names renamed Ship→Vessel but class names did not.
`VesselActionSO.cs` → `ShipActionSO` · `VesselActionExecutorBase.cs` → `ShipActionExecutorBase` ·
`VesselHelper.cs` → `ShipHelper` · `R_VesselElementStatsHandler.cs` → `R_ShipElementStatsHandler` ·
`VesselActions.cs` → `enum ShipActions` · `VesselHUD.cs` → `ShipHUD` (legacy) ·
`VesselCardView.cs` → `ShipCardView`. **Grep by class name.**

---

## 1. Identity & registration

A vessel class exists when ALL of these do. Five of eleven classes (Urchin, Grizzly, Termite,
Falcon, Shrike) fail parts of this today — that is the backlog, not a pattern to copy.

| # | Requirement | Enforced by |
|---|---|---|
| 1.1 | `VesselClassType` enum entry with an explicit numeric ID, never reused/renumbered (`Assets/_Scripts/Data/Enums/VesselClassType.cs`; `Any=-1`, `Random=0`) | convention only — serialization drift if broken |
| 1.2 | Prefab at `Assets/_Prefabs/Spacevessels/{Vessel}.prefab`, **named exactly after the enum member** — the auditors and `ElementalAbilityMapSO.LoadFor` key off the name | `VesselAbilityRowAuditor` flags unparseable names |
| 1.3 | Prefab root: `NetworkObject` (+ `ClientNetworkTransform`, `NetcodeHooks`), `VesselController`, `VesselStatus` with serialized `vesselType` = the enum value (this IS the registration key — lookup scans `IVesselStatus.VesselType`, no dictionary) | `ServerPlayerVesselInitializer.SpawnVesselForPlayer` hard-errors without NetworkObject; `VesselPrefabContainer` warns+skips without VesselStatus |
| 1.4 | The `[RequireComponent]` set on VesselStatus satisfied: `VesselPrismController`, `ResourceSystem`, `VesselTransformer`, `AIPilot`, `ElementalBarsController`, `VesselCameraCustomizer`, a **concrete** `VesselAnimation` subclass, `R_VesselActionHandler`, `VesselCustomization`, `R_ShipElementStatsHandler` | RequireComponent + lazy GetOrAdd accessors (self-heal, but unconfigured) |
| 1.5 | `VesselStatus._shipInstance` → the VesselController; `vesselHUDController` → an `IVesselHUDController`; `_nearFieldSkimmer`/`_farFieldSkimmer` wired; `VesselController.gameData` → `Runtime GameData.asset` (`Assets/_SO_Assets/Game Data/`) | runtime LogError/LogWarning ("ShipInstance is not referenced", "HUD will not function", "Ship properties will not be set") |
| 1.6 | Registered in `Assets/_SO_Assets/Vessel Prefab Container.asset` (`_shipPrefabs`) — BOTH spawn paths resolve exclusively through it | runtime LogError "No Vessel Prefab found" — note `VesselSpawner` resolves Random/Any over ALL enum values, so a Random roll can land on an unregistered class and fail loudly (three LogErrors, no vessel, the orphaned player is destroyed) |
| 1.7 | Listed in `Assets/DefaultNetworkPrefabs.asset` — clients cannot replicate an unregistered NetworkObject | nothing audits container↔network-list sync; manual |
| 1.8 | `Assets/_SO_Assets/Camera/{Vessel}CameraSettingsSO.asset` assigned to `VesselCameraCustomizer.settings` | unguarded deref → NRE on local-player camera apply |
| 1.9 | `SO_Vessel` meta asset (`SO_Class_{Name}.asset` in `Assets/_SO_Assets/Classes/`, menu `CosmicShore/Vessel/Vessel`) with Class, Name, `InitialResourceLevels`, icons; added to the relevant `SO_Classlist_*`. NOTE: Arcade writes `InitialResourceLevels` into `GameDataSO.ResourceCollection`, but the downstream hop to `ResourceSystem.InitializeElementLevels` is currently **dead** — both `SetResourceLevels` call sites are commented out; the live element seed is `ResourceSystem.Start()` | absence = invisible in hangar/arcade selection |
| 1.10 | `VesselCustomization._shipGeometries` populated; every hull MeshRenderer needs ≥2 material slots (`ShipHelper.ApplyShipMaterial` writes `materials[1]`; SkinnedMeshRenderer uses `materials[0]`) | LogError "Vessel geometries are not set"; IndexOutOfRange at theming |
| 1.11 | Telemetry: a per-vessel `VesselTelemetry` subclass **on the prefab** with its `VesselStatEventSO` refs wired (Sparrow/Squirrel pattern). `VesselTelemetryBootstrapper` is the degraded stopgap (runtime AddComponent, null stat SOs, warns every spawn); a new subclass must also extend its VesselType switch | warning every spawn in degraded mode |

**Spawning**: only two sanctioned paths, both DI-inject via `GameObjectInjector.InjectRecursive`
and converge on `VesselController.Initialize(IPlayer)` (single-shot — "Double initialization not
allowed"): `PlayerSpawner`→`VesselSpawner` (single-player) and
`ServerPlayerVesselInitializer`→`ClientPlayerVesselInitializer.InitializePair` (networked).
Swaps go through `ReInitializePair`/`MenuServerPlayerVesselInitializer.RequestSwap` (re-syncs
domain from `NetDomain` before repaint, inherits pose + speed). A bespoke `Instantiate` leaves
`[Inject]` fields null. Domain theming is automatic (`ShipHelper.SetShipProperties` at Initialize
and on every NetDomain replication) — never hand-wire team materials, and never run theming
before `VesselStatus.Player` is set (Domain LogErrors "No Player found to get domain!" and
falls back to Jade — the wrong paint plus a console error).

---

## 2. Actions & executors

- **`ShipActionSO` assets are shared and stateless** (declared in
  `R_VesselActions/Data Containers/VesselActionSO.cs`): `StartAction/StopAction(registry,
  status)` per call; no unlock state, no bound ElementalFloats, no subscriptions on SOs
  (last-initializer-wins in multiplayer is the shipped cautionary tale; the
  `ElementalFloatBinder` call is deliberately commented dead).
- **State lives in executors**: `ShipActionExecutorBase` subclasses in
  `R_VesselActions/Executors/`, listed in the prefab's `ActionExecutorRegistry._executors`,
  resolved by `execs.Get<T>()`.
- **Input binding is prefab data**: `R_VesselActionHandler._inputEventShipActions`
  (InputEvents → List\<ShipActionSO>; multiple SOs on one event start/stop together) plus
  `_touchActionOverrides`/`_gamepadActionOverrides` (per active device; DualMouse shares
  gamepad). Dispatch replicates by re-execution: ServerRpc → ClientRpc →
  `PerformShipControllerActions` on every peer.
- **AI parity is free if you don't fork**: AI drives the same executors. Stick-triggered
  abilities need explicit AI trigger synthesis in the executor (autopilot produces no stick
  input). Do not build a parallel ability path.
- **Asset instances** live in `Assets/_SO_Assets/VesselActions/{Vessel}/` (+ `Common Vessel
  Action/` for shared ones).
- **Legacy trap**: `Assets/_Scripts/Controller/Vessel/VesselActions/` (MonoBehaviour
  `ShipAction`) compiles but is dead — the R_ handler's dictionaries are typed `ShipActionSO`
  and the legacy `ShipHelper.Perform*` overloads have zero callers. `ResourceEvents` mapping is
  likewise built but never dispatched.
- **Resource meters** are `ResourceSystem.Resources` slots addressed **by serialized index**
  from action SOs and HUD controllers — document every index; a gauge bound to a meter whose
  writer sets `CurrentAmount` directly (never raising `OnResourceChanged`) can never move.
- Init-order rule: `R_VesselActionHandler.Initialize` runs `InitializeAll` on executors
  **before** populating its binding maps — any consumer querying bindings must retry until
  success (resolve lazily via `CollectBoundActions`), never latch on first attempt.

## 3. The elemental ability map

`Assets/Resources/ElementalAbilityMaps/{VesselClassType}.asset` — **this exact folder and
name**: `ElementalAbilityMapSO.LoadFor` does `Resources.Load("ElementalAbilityMaps/{class}")`,
so an asset anywhere else (including `_SO_Assets/`, where old docs proposed it) silently loads
null → all multipliers 1×, no unlocks, no error. Exactly 4 entries (`SlotCount=4`,
OnValidate-trimmed). Per `ElementalAbilityEntry`:

| Field | Meaning |
|---|---|
| `Element` | Charge=1, Mass=2, Space=3, Time=4 (`enum Element`; unlock bit = `1 << (element-1)`) |
| `AbilityLabel` / `AbilityDescription` | the ability + **the real authoring home of the scaling** (a description citing the wrong SO caused doc-vs-asset drift within one branch) |
| `Input` | the `InputEvents` the ability rides. `0` (`FullSpeedStraightAction`) doubles as "unset" — legitimate **only** for passive/impact-driven abilities; otherwise it blocks hint→ability derivation |
| `MultiplierAtFullLevel` / `MinMultiplier` | generic quantitative scaling (1× at resting level, atFull at level 10, floored). **Pin to 1 when a dedicated authored field on the action SO carries the scaling** — otherwise one element drives two parameters (no-double-dip; nothing audits this, it is a convention you must check by grepping the vessel's `Multiplier(element)` consumers incl. `VesselTransformer`) |
| `UnlockLevel` (5) / `RelockBelowLevel` (4) / `LatchPolicy` | qualitative tier + hysteresis |
| `UpgradeLabel` / `UpgradeDescription` | what the player is told at L5 — the HUD reads the map |

The map has **no icon fields** — icons live only in `VesselHUDView.abilityIcons`
(ARCHITECTURE.md §3.2's entry table is stale on this point). The asset **is the design
record**: it must agree with `FLEET_MAPS.md`, and open slots (`(open design slot)`, `Input: 0`,
empty UpgradeLabel) mean the design does not exist — **stop and get approval; never author a
mapping to satisfy the auditor** (BACKLOG.md, locked).

## 4. Element levels & level-5 upgrades

- **`ResourceSystem`** (a non-networked MonoBehaviour, via `ElementalShipComponent` — levels
  never replicate) is the single elemental state home on every vessel:
  `GetLevel(element) = floor(effective × 10)` ∈ **[-5, 15]**; level 5 = the all-petals-white
  flower. The one signal is the per-vessel C# event **`OnElementLevelChange`** (deduped integer
  transitions; NOT SOAP — vessel-internal). Subscribe idempotently, detach in OnDestroy.
- **Scaling**: `ElementalScaling.Multiplier` (1× anchor at resting level, `LerpUnclamped` into
  deficit/overcharge bands, floored) via `R_VesselElementalAbilityHandler.Multiplier(element)`
  — lazily self-created through `VesselStatus.ElementalAbilityHandler` (GetOrAdd, zero prefab
  wiring). Authored per-field curves use `ElementalFloat.EvaluateLive(status)`.
- **Upgrades**: unlock at effective level ≥ `UnlockLevel`, re-lock below `RelockBelowLevel`
  (Relock policy). **Outcome-affecting checks go through `IsUpgradeActive(element)`** — on
  non-owner peers it reads the replicated owner-write `NetworkVariable<byte> NetElementUnlocks`
  on **`R_VesselActionHandler`** (VesselStatus is deliberately a plain MonoBehaviour; old docs
  putting the bits on VesselStatus are stale). Element levels themselves never replicate;
  `ElementalScaling.MeetsQualitativeThreshold` is display-only. HUD/VFX observe
  `OnUpgradeStateChanged(Element, bool)`. Per-use snapshot at fire time (piercing/shield/sparing
  flags ride the shot, which also makes replication timing benign). No mid-action interruption.
- **Laws (LOCKED)**: all buffs/debuffs route through Elementals
  (`ApplyElementalEffect`; single-writer modifier layers `SetComebackModifier` /
  `SetFaunaBuffModifier`). The **maintained-mechanism law**: nothing sustained may HOLD a level
  above 10 — `SustainedCeiling` + `RecoverBaseLevels` enforce it structurally; convert
  over-ceiling sustained gains into decaying transients, and never write base levels per tick
  (the comeback system's original clobber bug). Upgrade design ground rules (FLEET_MAPS §2–§3):
  reuse existing primitives; regular shield only, never SuperShield; no timers/decay; gate
  strictly in the acting system's layer — domain-sparing in the explosion/collection layer only,
  never `Prism.Damage`, never `*ByDangerPrismEffectSO`. Danger stays domain-blind (CLAUDE.md's
  locked danger-prism design).

## 5. The four-icon ability row

Canon: `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.1–7.4. The distilled contract:

- Exactly **four icons, lower right, charge → mass → space → time left-to-right** — the same
  order as the flowers, so position answers "which flower upgrades this?".
  `VesselHUDView.AbilityDisplayOrder` is the single source; `OnValidate` auto-sorts
  `abilityIcons`; `ValidateAbilityIconRow` (editor-only, called from
  `VesselHUDController.Initialize`) warns on count/order/layout violations — silence is how the
  Squirrel once shipped a reversed row.
- **Upgrade signal = three independent layers** in `SetAbilityUpgraded`: authored
  `upgradedSprite` swap (authored art only, restored on re-lock) · the element-petal badge in
  level-5 white from `ElementalBarsConfigSO` (blooms/withers, a CHILD of the icon so per-frame
  repaints can't stomp it) · optional tint + persistent rest-scale bump with one-shot punch.
- **Live-gauge icons** (cooldown fill, heat tint, drift lean, jaw gape): `tintIconOnUpgrade =
  false` AND override `SetAbilityUpgraded` → call base, then re-anchor every captured rest scale
  to `AbilityIconRestScale(element)` — a rest-scale field written but never read left the
  Dolphin branch's Time slot as the one icon with no L5 bump. `SquirrelVesselHUDView` (sealed)
  is the reference.
- **Geometry**: author all four buttons identically in the HUD **variant** (same anchor span,
  sizeDelta/anchoredPosition 0, one shared y, even anchor-fraction pitch — aspect-ratio-safe).
  Resolve effective rects through the **vessel prefab's `m_Modifications` first** (instance
  overrides once split the Squirrel's row: four icons on two positions). A HUD-instance root
  that isn't stretched (0,0)-(1,1)/sizeDelta 0 collapses the whole HUD to screen centre (the
  Serpent bug). Some icons may live in the vessel prefab, not the variant (Rhino: 3 of 4).
- **Control hints bind to abilities, never positions**: `hint.binding` → `InputHintBindingMap`
  → `InputEvents` → map `Input` (fallback: `CollectBoundActions` shared-asset intersection) →
  `TryGetAbilityIcon`. `InputDeviceIconSetSwitcher.BindHintsToAbilities` runs once from
  `VesselHUDController.Initialize`; without a switcher on the HUD nothing places hints (they
  strand on reorder) and Xbox/PS sets render simultaneously. Never reparent a hint; **never
  write a hint's size** (zero-sizeDelta stretch rects — collapsing the anchor span destroys
  their size); use `InverseLerpUnclamped` (anchor fractions far outside 0..1 are correct);
  `WarnIfPlacedOffScreen` closes the loop.
- **Auditor**: `FrogletTools > Vessels > Audit Vessel Ability Rows` — asset-only; checks map
  completeness, 4 icons one-per-element, order, uniform size/pitch (1.5 px on a deterministic
  1920×1080 canvas), switcher + hint coverage. Blind spots: <2 resolved icons skips geometry;
  slot geometry reads the icon's PARENT rect.

## 6. Elemental petal bars

The flower display is **fleet-required** (CLAUDE.md's "opt-in rollout" phrasing is stale):
`[RequireComponent(typeof(ElementalBarsController))]` on VesselStatus,
`VesselController.Initialize` drives it, and every missing piece is runtime-created **with a
loud warning naming the authoring tool** — warnings mean the prefab authoring was skipped, they
are not the contract. Author via `FrogletTools > Vessels > Wire Elemental Petal Bars` (selected
HUD prefab in Prefab Mode) or `Bake Elemental Petal Bars Into All Vessel HUDs` (fleet pass),
then assign the view to `ElementalBarsController.elementBars`. All look/feel lives in
`Assets/Resources/ElementalBarsConfig.asset` (`ElementalBarsConfigSO`) — never per-prefab
fields. Petals are pure-white silhouettes multiply-tinted per `DistributePetalValues`
(level [-5,15] round-robin over 5 petals → {fire, grey, white, blue, lime}) — **never
hue-shift**, never author pre-coloured petal art. Event-driven only (`OnElementLevelChange`,
re-seed on OnEnable); tweens `SetLink`ed and rest-snapped in OnDisable. The view's juice API
(`JuiceCrystalCollected`/`JuiceJoust`/`JuiceDriftStart`) and its designed access point
(`ElementalBarsController.ElementBars`) currently have **zero callers** — an open hook, not
shipped routing (the Squirrel HUD controller juices its own view's icons instead). Wire flower
juice through `ElementBars` when a vessel wants it.

## 7. Hull morphs & vessel animation

- **Opt-in by ART, zero wiring**: a vessel morphs iff its skinned meshes carry blend shapes
  labeled `charge`/`mass`/`space`/`time` (case-insensitive; whole tokens between `_.-`/space —
  `mass_hull` binds, `massive_jaw` doesn't; two-element names are ambiguous → ignored;
  FBX deformer prefixes fine; the shape's last-frame weight is its extreme). Discovery is
  `VesselAnimation.CollectElementShapes` at Initialize. No per-prefab flags exist.
- **A GENERATED hull morphs procedurally, and it must say so.** The Scarab has no morphable FBX —
  its morphs are the four element extremes of its own pure hull function, baked to deltas and
  blended at the shared config's feel (SCARAB.md §3.0.2). Such a vessel implements
  **`IProceduralElementMorphSource`** (`ProceduralMorphElements` + `HiddenLegacyModelRoot`), which
  is what keeps the audit honest twice over: procedural coverage counts as real, and element
  blend shapes under the declared hidden legacy root are marked INERT instead of counted — a
  shape on a renderers-off placeholder greens the audit while the hull morphs by nothing. If you
  build a second procedural hull, keep the split: the builder owns geometry (topology-asserted
  extreme bakes, bounds pinned to the weight-lattice union, `DontRecalculateBounds` writes), the
  animation owns time (config SO feel, instant seed, kill-and-retween, LateUpdate push after the
  base's shape-key write) — and the morph writes localPosition/mesh while puppetry writes
  localRotation, so no channel gains a second writer.
- Morphs express only the **[0,10] band** (deficit holds level-0, overcharge holds level-10 —
  hull and flowers always agree); DOTween glides from
  `Assets/Resources/VesselElementalMorphConfig.asset`, never snaps.
- **`LateUpdate` is the single writer of blend-shape weights — load-bearing**: the Animator
  writes bound curves after Update, so an FBX carrying even constant-zero blend-shape curves
  would stomp tween-written weights. Tweens drive a cached array; only LateUpdate touches the
  renderer. Do not "simplify" this. Corollary: **any FBX re-export must carry BOTH the
  animation takes and the four shape keys** — a re-export once silently dropped the Squirrel's
  shapes (the shipped FBX is a byte-level splice; see CLAUDE.md ▸ Elemental Hull Morphs).
- **Parts resolve BY NAME** (`ResolvePart`: authored ref wins, then candidate names, rig bone
  first, legacy name after; `ReportUnresolvedParts` warns loudly, limbs degrade frozen rather
  than NRE). Rigged art must use `CaptureRestRotations`/`RotatePartFromRest`/rest-aware
  `ResetAnimation` — driving bones toward absolute rotations assumes identity rest and flattens
  a rig (two shipped Dolphin bugs came from this root).
- **A LABELLED shape is not a SHAPE — measure its MAGNITUDE before trusting a rig.** Of the
  three unwired `*_shapekey_with_animations` rigs, only the **Dolphin's carries a real morph**
  (mass moves 10,909 verts, time 9,272). The **Rhino's and Urchin's four element shapes each
  move ONE vertex by ZERO** — name-only placeholders. Swapping either rig in turns the morph
  audit GREEN while the hull morphs by nothing, which is worse than the current honest zero.
  Read the FBX `Geometry` sub-records of subtype `Shape` and sum the deltas. Evidence + the
  per-file table: `Docs/VESSEL_CONSTRUCTION.md` §4.
- **Rig swaps** (Dolphin/Urchin/Rhino placeholders → their `*_shapekey_with_animations` rigs)
  are a hands-on editor pass: run `FrogletTools > Vessels > Plan Vessel Rig Swap`
  (report-only) and follow its printed procedure — migrate gameplay objects to mapped bones,
  retire legacy MeshRenderers, re-fit colliders by eye, re-point ship geometry, **clear the
  animation's part fields** so they re-resolve to bones, re-run the morph audit. Order of work
  + the salvage-before-delete gate: `Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md`.
- **A rig swap moves every measured mount on the vessel — UNLESS you fit the instance
  transform, which is strictly better.** Each of the three rigs is the shipped hull under some
  transform (Dolphin identity, Rhino `z −1.5545` — every lathe ring matches at identical radius
  and vertex count — Urchin a uniform `2.105×`). Solve for that transform by nearest-neighbour
  residual over the two files' world point clouds and place the rig instance AT it, and every
  collider and FX mount keeps its world position: nothing downstream is re-measured, which is
  most of the cost of a swap. Re-measure only what the rig genuinely re-poses (the Rhino's
  wings sit 1.38× wider in its bind pose, and its wings stop being separate GameObjects, so
  anything parented to them re-parents to bones).
- Audit: `FrogletTools > Vessels > Audit Vessel Elemental Morphs` (asset-only, exact runtime
  discovery). Mislabeled shapes fail **silently** in game — the audit is the only detector.
  **Since 2026-08-26 it measures MAGNITUDE**, so an empty labelled shape no longer passes:
  `Travel()` reports each shape's farthest vertex travel as a fraction of its mesh's own
  bounding-box diagonal, and anything under `MinShapeTravelFraction` reads INERT. **The
  threshold has to be RELATIVE** — a historical `Sparrow Missile.fbx` carried shapes indexing
  243 and 309 vertices and moving them 4e-6 units, which an absolute epsilon calls live.
  Measured over every shipped vessel model, real shapes travel 2.46–17.94% of the diagonal and
  fake ones 0.0000%, so the constant is picked from inside a measured gap rather than guessed.
  Also run `Audit Vessel Construction` (guid ownership, nested-instance reachability, duplicate
  coincident hull renderers).
  Edit-mode tests: `VesselElementalMorphTests`, `VesselRigPartResolutionTests`.

## 8. The HUD controller/view pair

- **Shape**: a controller extending `VesselHUDController` on the **vessel prefab root**, wired
  into `VesselStatus.vesselHUDController`; a view extending the abstract `VesselHUDView` inside
  the per-vessel HUD prefab (`Assets/_Prefabs/UI Elements/VesselHUD/{Vessel}HUDVariant.prefab` —
  Squirrel/Manta/Serpent are true variants of `VesselHUDPrefab.prefab` (root "ShipHUDPrefab");
  Sparrow/Rhino/Dolphin are standalone prefabs with the same structure; none carries a Canvas of
  its own), nested in the vessel prefab under a `ShipHUDContainer` child that carries the
  ScreenSpaceOverlay Canvas. The instance is authored **active** — it starts hidden at runtime
  because `VesselController.Initialize` calls `HideHUD()` (CanvasGroup fade to 0, then
  SetActive(false)). New controllers go in `Assets/_Scripts/UI/Controller/` and
  views in `Assets/_Scripts/UI/View/` (Squirrel/Dolphin's controllers under
  `R_VesselActions/Data Containers/` are historical drift, not the pattern).
- **`IVesselHUDView` is a trap**: an empty marker interface implemented by nothing —
  `VesselHUDView` (abstract class) is the real contract. The legacy `ShipHUD` reparent path is
  dead for the shipping fleet (only Termite still nests `HUDContainer.prefab`).
- **Lifecycle**: `VesselController.Initialize` initializes the HUD **hidden** and calls
  `SubscribeToEvents` only for the local user. Visibility is owned by scene hosts:
  `MiniGameHUD` (on `OnMiniGameTurnStarted`) and `MenuMiniGameHUD` (freestyle enter/exit; after
  a swap, re-shown via `ClientPlayerVesselInitializer.ReInitializePair` →
  `OnPlayerPairInitialized`). Never add a show-HUD call in a vessel controller; never remove
  either half of the swap re-show. `Show`/`Hide` are DOTween fades — nothing pops.
- **Binding discipline** (the Dolphin branch shipped this wrong three times): one symmetric
  `Rebind()`/`Unbind()` pair, called from Initialize (detach-first — swaps re-run Initialize on
  live components; Rhino's Unsubscribe-then-Subscribe exists because a re-init double-counted),
  OnEnable (gated) and OnDisable/OnDestroy. Gate everything on
  `IsInitializedAsAI || !IsLocalUser` — **the base class does not gate for you** — and
  sender-filter shared SOAP channels (every vessel that wires `boostChanged` raises it — today
  every Squirrel instance, **including remote ones**, per-frame via `DecayBoost` — so an
  unfiltered handler lets a remote vessel pin your energy bar).
- **Data discipline**: bind resources **by name** with serialized index as fallback; only bind
  meters whose writers raise the per-resource event; adopt displayed constants from the gameplay
  component (one authored number); per-shot objects get a static presentation event with
  listeners filtering by their own vessel ("presentation only — listeners must not change
  outcomes"), never polling or scene searches.

## 9. Impact effects & skimmers

**No editor auditor covers this subsystem** — violations surface only at runtime (NRE on null
container by fail-loud design, silent no-op on empty arrays/uninitialized skimmers, one joust
warning). Be exhaustive here; this is the contract's least-guarded clause.

- **Vessel side**: `VesselImpactor` on the root (RequireComponent pulls `NetworkVesselImpactor`)
  with `vesselImpactorDataContainerSO` → `Assets/_SO_Assets/Effects/Effect Containers/
  VesselContainers/{Vessel}ImpactorDataContainer.asset`. Baseline prism trio every shipping
  vessel authors: `VesselDamagePrismEffect` + `VesselHapticsByPrismEffect` +
  `VesselElementalDebuffByDangerPrismEffect`; plus ≥1 crystal effect
  (`{Vessel}VesselExplosionByCrystalEffect` / `...ChangeResourceByCrystalEffect`). Empty arrays
  are a legal opt-out; a **null container is a bug that NREs on first contact by design**.
- **Impactability**: every hittable collider carries an `ImpactCollider` whose `impactorObject`
  points at the owning impactor — colliders without one are invisible to the whole impact
  system. Multi-collider hulls are fine (0.5 s crystal latch dedupes).
- **Skimmer**: nest `Assets/_Prefabs/Spacevessels/Components/Skimmer.prefab` (or
  `ForceFieldSkimmer Variant.prefab` for sword capsules) and **override the nested
  SkimmerImpactor's container** with `.../SkimmerContainers/{Vessel}*SkimmerImpactorDataContainer.asset`
  (Manta's is `MantaOvercharge…`, Rhino's `RhinoForceField…`) — the base
  prefab ships a NULL container (stale field names only), so an un-overridden nested skimmer
  NREs on first prism contact. Wire the skimmer into `VesselStatus._nearFieldSkimmer` /
  `_farFieldSkimmer` — `VesselController.Initialize` only initializes those two references; an
  unwired skimmer stack is permanently inert dead weight (current Dolphin/Sparrow state).
- **Locked rules**: the mirrored vessel↔own-skimmer self-guards in both `AcceptImpactee`
  switches stay (the Rhino sword used to mute its own pilot); skimmer-vs-own-DOMAIN-prism is the
  separate serialized `Skimmer.affectSelf` flag — **the C# default AND the base Skimmer.prefab
  are both `true`**, and it ships true on Squirrel (danger-trail loop), Dolphin's wired skimmer,
  and Rhino's sword; explicitly override it to 0 unless self-skim is the vessel's design (Manta,
  Serpent do); danger-prism effect SOs never gate on domain; shell-tier
  cooperation (`ShellOwnsContact` suppression + probe registration) must survive any collider
  rearrangement.
- **There are THREE self-guard shapes, and picking the wrong one is silent.** (a) *Reference
  compare* on `VesselStatus` — "me exactly" (vessel↔own-skimmer). (b) *Domain compare* — "my
  team", which is what `Skimmer.affectSelf` and `VesselChangeSpeedByPrismEffectSO` use, and it is
  a **trap** whenever the intent is per-pilot: switching it off also blinds the vessel to its
  teammates' mass. (c) *Owner + age* — `prism.ownerID` against `VesselStatus.PlayerName` within a
  grace of `prismProperties.TimeCreated`, i.e. "mass I am making right now"; this is the only
  shape that can spare a pilot their own fresh trail while leaving every other pilot's trail (and
  their own older trail) fully live. `SelfTrailContactConfigSO` owns it fleet-wide — route a new
  self-rule through it rather than authoring a fourth shape.
  Two facts that make (b) worse than it looks: **`affectSelf` is evaluated AFTER the skimmer
  effect loop**, so it gates only `_skimStartTimes` bookkeeping and changes nothing for effects
  (a vessel with `affectSelf = 0` still runs every skimmer prism effect on its own mass); and
  `Prism` has **no vessel handle at all** — no `Prism.Vessel`, and `Prism.Trail` is null on
  vessel-laid prisms — so `ownerID`/`PlayerName` strings are the only per-pilot identity
  available. Prefer `ownerID`: it records who LAID it and a steal does not reassign it.
- **Joust**: vessels participating in Joust wire a `VesselExplosionBySkimmerEffectSO` in their
  SKIMMER container's `vesselSkimmerEffectsSO` — `ExecuteJoustImpact` warns on every confirmed
  joust otherwise. The vessel-side `vesselSkimmerEffects` arrays are empty fleet-wide; authoring
  the same effect on both sides double-fires.
- **Crystal pickup on skim is crystal-side** (`ElementalCrystalImpactor.elementalCrystalShipEffects`)
  — leave per-vessel `skimmerCrystalEffectsSO` empty; do not duplicate pickup logic.
- **Crackle opt-in = two halves**: the `ForcefieldCrackleController` on the skimmer GO +
  `SkimmerForcefieldCracklePrismEffect` in the skimmer container.
- **A fast projectile is a TELEPORT, not a sweep — it only tests the points it LANDS on.**
  `Projectile.MoveProjectileAsync` writes `position += Velocity·Δt` and PhysX samples the
  discrete trigger once per physics step, so the path BETWEEN samples is never tested. Measured
  on the Sparrow: at its base 375 u/s a round covers 6.25 u per 60 fps frame behind a 1.65
  hit sphere — **26% of its own path**, ~3% at high SPACE, and it halves again at 30 fps. The
  symptom is a gun that cannot clear a dense patch no matter how much you shoot, with no misses
  to see; the tell is *"making the projectile bigger fixes it"*, because a big enough ball closes
  the per-frame gap. Fix with `PrismSpatialIndex.QuerySegment` +
  `Projectile.sweptPrismDetection` (dispatch nearest-first, and have the sweep OWN the contact
  class so the trigger cannot double-fire) — never by inflating the collider, and never with
  `Physics.SphereCast` (CLAUDE.md forbids physics queries against prisms; a transform teleport
  also bypasses CCD entirely). Rate and spread cannot compensate: they multiply a path the
  weapon is structurally blind to.
- **A `SphereCollider`'s world radius is `m_Radius × the LARGEST lossy-scale component`** — this
  trap has now bitten the same vessel twice. Once as the 12-diameter hit sphere nobody authored
  (a `0.3` radius on a tracer stretched ×20 in z), and again when growing a round's
  cross-section only: the untouched z-stretch stays the max, so a radius re-derived from
  `lossyScale` never moves. Author it as `desiredWorldRadius / maxScaleComponent`, and when a
  size must track a non-uniform scale, carry the factor EXPLICITLY rather than re-deriving.
- **Hygiene**: renaming container fields without `[FormerlySerializedAs]` silently strips
  authored effects (Sparrow lost all elemental-crystal feedback this way); an effect asset that
  exists but sits in no container executes never (several orphans exist); fork shared effect SOs
  before changing per-vessel behavior.

## 10. Docs & paper trail

- **Canon map**: CLAUDE.md holds the locked fleet contracts;
  `Docs/ElementalAbilitySystem/{ARCHITECTURE,FLEET_MAPS,AUDIT,BACKLOG}.md` hold the elemental
  contract, per-vessel status, dated evidence (CONFIRMED = adversarially verified with
  file:line, REPORTED = re-verify at fix time), and the sequenced plan; per-ability deep docs
  live beside the code in `Assets/_Scripts/Controller/Vessel/R_VesselActions/*.md`
  (`RHINO_SHIELD_SWIPE.md`, `RHINO_RAMP_BOOST.md`, `SQUIRREL_TUBE.md` are the exemplars).
  `Docs/README.md`'s tree does NOT index the vessel docs; CLAUDE.md's Documentation Index
  carries the `ElementalAbilitySystem/` row (added alongside this skill) and CLAUDE.md cites the
  per-ability docs in prose (Impact Effects §, Key Systems table).
- **On shipping vessel work**, update in-branch: FLEET_MAPS §1 live table + §2 status (→
  "APPROVED + SHIPPED" with the shipped table, Squirrel-style) · ARCHITECTURE §7.2 fleet status
  · BACKLOG item → SHIPPED with deltas · **CLAUDE.md's fleet table** · the map asset's
  UpgradeLabel/UpgradeDescription · a co-located `{FEATURE}.md` design doc (overview/control
  tables · Files table (role → path) · Tuning-knobs table (knob → asset → shipped value) ·
  numbered `## In-editor verification` · `## Follow-ups`).
- **Verification ledger**: anything committed without an editor pass gets a 🔴 section in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md` (newest first: what landed / verify-in-editor steps /
  first-pass tuning table) — never only a PR body or chat message.
- Keep inline `see Docs/... §X` comments in sync; commit `.meta` files; conventional commits per
  `GIT_RULES.md`.

## 11. Fleet status snapshot

Dated **2026-08-02** — this table goes stale; re-establish ground truth per SKILL.md §2 before
trusting it. "Registered" = in `Vessel Prefab Container.asset` (`DefaultNetworkPrefabs.asset`
already lists all 11 prefabs).

| Vessel | Registered | Map | Row icons | Morph shapes | Impact/skimmer | Notes |
|---|---|---|---|---|---|---|
| Squirrel | ✅ | 4/4 + 4 upgrades SHIPPED | ✅ compliant | ✅ (spliced FBX — re-export carefully) | ✅ reference | the reference vessel |
| Sparrow | ✅ | 4/4 + 4 upgrades SHIPPED | ✅ row; no switcher (hints static, both glyph sets render); glyph art wrong | ✅ | skimmer containers all-empty; 2nd inline skimmer unwired | |
| Manta | ✅ | 3/4 quantitative, 0 upgrades — **open slots** | 0 found (re-survey at vessel-prefab level) | ✅ (Manta meshes, also Termite/Falcon/Shrike) | ✅ | blocked on design |
| Dolphin | ✅ | 4/4 + 4 upgrades SHIPPED (`claude/dolphin-energy-crystal-cooldown-zpvc07`) | 4/4 bound; no switcher (hints unbound) | ❌ placeholder art — rig swap pending | ✅ **since the branch** — the reference pointed at a DISABLED twin | energy economy + drift boost + cone; see `R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` |
| Rhino | ✅ | 1/4, 0 upgrades — **open slots** | 4 icons exist (3 in vessel prefab), unbound | ❌ placeholder — rig swap pending | ✅ (sword variant) | bind once map approved |
| Serpent | ✅ | 1/4, 0 upgrades — **open slots** | 0 bound; HUD root was centre-collapsed (fixed) | ✅ | ❌ "VacuumSkimmer" has no SkimmerImpactor/container AND its GameObject is INACTIVE — cannot vacuum | fails Audit Vessel Skimmers |
| Urchin / Grizzly / Termite / Falcon / Shrike | ❌ | none | none | Urchin ❌ (rig pending) · Grizzly ❌ (no art) · Termite/Falcon/Shrike ✅ | none (nested base skimmer would NRE) | prefabs exist and are already in DefaultNetworkPrefabs (all 11 are — don't re-add); Vessel Prefab Container registration unstarted; NONE of the five has a `{Vessel}CameraSettingsSO.asset`; Falcon/Shrike additionally lack SO_Class assets |

**This table was WRONG about the Dolphin's impact/skimmer column until 2026-08** — it read
"✅ containers; 2nd skimmer stack unwired" when the truth was the exact inverse: the *second*
stack (`EnergySkimmer`) was the live one doing the physics, and the stack the vessel actually
REFERENCED was a disabled leftover. Reading a prefab and concluding "looks wired" is not
evidence; resolve the reference to its GameObject and check `m_IsActive` up the ancestor chain,
or just run **FrogletTools > Vessels > Audit Vessel Skimmers**, which does exactly that.
