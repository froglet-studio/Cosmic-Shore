---
name: vessel
description: Use for ANY work on a Cosmic Shore vessel class — creating or registering a new vessel, vessel abilities/actions/executors (R_VesselActions), elemental ability maps and level-5 upgrades, vessel HUDs (the four-icon ability row, control hints, gauges, controller/view pairs), elemental petal bars, hull morphs / blend shapes / rig swaps, skimmers and impact-effect containers, or vessel prefab wiring (camera, telemetry, customization) on Manta, Dolphin, Rhino, Serpent, Sparrow, Squirrel, Urchin, Grizzly, Termite, Falcon, or Shrike. Loads the fleet-wide vessel contract (4 abilities × 4 elements × 4 icons), the audit tools, and the per-subsystem checklists so vessel work stops re-deriving the requirements each time. Trigger when editing Assets/_Scripts/Controller/Vessel/**, vessel HUD files under Assets/_Scripts/UI/**, Assets/_Prefabs/Spacevessels/**, Assets/Resources/ElementalAbilityMaps/**, or Docs/ElementalAbilitySystem/**.
---

# Vessel Class Protocol

You are working on a **vessel** — one of the 11 classes that are the player-facing fundamental of
Cosmic Shore. Every vessel satisfies one fleet-wide contract, and that contract has historically
been **re-derived from scratch on every vessel branch**, at real cost: on the Dolphin elemental
pass, three of four commits were corrections, and the branch re-discovered rules the fleet had
already paid for once — asymmetric event bindings (three separate instances), a
permanently-latched init race, a gauge bound to a meter that never raises events, doc/asset
drift within the branch itself. This skill exists so that never happens again. Follow it
exactly.

## 1. The contract (what every vessel is)

> **Four abilities, each mapped to one of the four elements, each with a level-5 upgrade, each
> shown as one of four HUD icons in charge → mass → space → time order** — plus the element
> flowers, the hull morphs, the impact/skimmer effect containers, and the registration set that
> makes the vessel spawnable. Element conventions: **Space = reach/presence · Time =
> rate/mobility · Charge = threat/energy · Mass = size/volume.** One parameter per element.

The full clause-by-clause checklist — requirements, enforcing mechanism, key files, wiring
steps, and the recorded failure modes per subsystem — is in
**`references/CONTRACT.md`** (this directory). Read the section(s) for the subsystem you are
touching before editing; read all of it when creating or completing a vessel.

| Your task touches… | Read CONTRACT.md § | Plus canon |
|---|---|---|
| New vessel / spawning / prefab wiring | §1 Registration | CLAUDE.md ▸ Player Spawning |
| Abilities, actions, executors, input | §2 Actions | `R_VesselActions/*.md` for that ability |
| Element scaling, map assets, L5 upgrades | §3 Map, §4 Elementals | `Docs/ElementalAbilitySystem/ARCHITECTURE.md` + `FLEET_MAPS.md` |
| HUD icons, hints, gauges, HUD lifecycle | §5 Ability row, §8 HUD pair | ARCHITECTURE.md §7.1–7.4 |
| Petal flowers / hull morphs / rig or FBX | §6 Bars, §7 Morphs | CLAUDE.md ▸ Elemental Bars / Hull Morphs |
| Collisions, crystals, skimmers, jousting | §9 Impact effects | CLAUDE.md ▸ Impact Effects; `RHINO_SHIELD_SWIPE.md` |
| Docs, shipping, verification | §10 Paper trail | `GIT_RULES.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |

## 2. Establish ground truth before editing (docs drift; assets + code do not)

Fleet-status tables go stale — CLAUDE.md's fleet table, `ARCHITECTURE.md` §3.2's field list, and
`FLEET_MAPS.md` proposals have each contradicted the shipped assets at some point. **The map
asset, the prefab, and the code are the record.** Before changing a vessel:

1. Read `Assets/Resources/ElementalAbilityMaps/{Vessel}.asset` — what is actually authored?
   `(open design slot)` + `Input: 0` + empty `UpgradeLabel` = the design does not exist yet.
2. Read the vessel prefab (`Assets/_Prefabs/Spacevessels/{Vessel}.prefab`) for the real wiring —
   including HUD icons that live in the **vessel** prefab, not the HUD variant (the Rhino's row
   was missed for exactly this reason), and `m_Modifications` overrides on nested prefabs.
3. Run (or, since you cannot run Unity, reason from the source of) the fleet auditors:
   **FrogletTools > Vessels > Audit Vessel Ability Rows**, **Audit Vessel Skimmers** and **Audit
   Vessel Elemental Morphs** — all asset-only, all reuse the exact runtime discovery code, so
   report and game cannot disagree.
4. **Per-vessel NUMBERS come from the prefab, never the class default.** Reasoning about a
   vessel's speed, boost, or scaling from the `VesselTransformer` field initializers you can
   see in the `.cs` will be wrong for whichever vessel overrides them — the Manta serializes
   `DefaultThrottleScaler: 180` against a class default of 50, so its cruise is 180 and its
   boosted top 720, not 60/210. Read the prefab YAML. (And note `ThrottleScaler`/`MinimumSpeed`
   are `[HideInInspector] public` runtime mirrors that serialize STALE garbage — `0` on most
   prefabs — and are only correct after `ResetTransformer()`; the authored truth is the
   `Default*` pair.)
4a. **…and the AUTHORED number is not the EFFECTIVE one — trace the consumer before you tune
   against it.** Reading the field is only half the job; a tuning request is about the value
   that reaches the screen. `VesselTransformer.CurrentBoostAmount()` multiplies
   `BoostMultiplier` **by** `ChargedBoostCharge`, and both are `BoostMultiplierFrom(...)` of the
   same meter — so a charged boost applies `maxBoostMultiplier` **squared**, and the Dolphin's
   real ceiling was `50 × 2² + 10 = 210` while its own design doc described a single ×2 (110).
   Tuning off the authored field would have missed by a factor of two. Read the formula that
   consumes the number — `ComputeThrottleTarget`, `EvaluateLive`, `ElementalScaling.Multiplier`
   — and write the derived value into the doc so the next pass starts from the effective number.
   Corollary: when the code and a design doc disagree, **the code is the record and the doc is
   the bug** — but do NOT correct the code inside a tuning branch. Halving a vessel's boost is
   its own change with its own retune; document the discrepancy, log it as a follow-up, and tune
   against shipped behaviour.
4b. **…and the EFFECTIVE number may never have been AUTHORED by anyone — find the line that
   CHOSE it before you match a second system to it.** The Sparrow's bullets flew a hit sphere of
   world diameter 12. Three assets were tuned to that number "for parity", a config default and
   a design doc both recorded it as deliberate — and nothing had chosen it: a `SphereCollider`
   takes the **largest** lossy-scale component, so the tracer's `(1.5, 1.5, 20)` stretch turned
   `m_Radius 0.3` into a 6.0-world-radius ball, 8× the projectile's visible 0.75 cross-section.
   The accident then propagated for two playtest rounds and produced its own downstream bugs (a
   spray in which every shot destroyed the previous prism). Before adopting a measured constant
   as a target, grep for the line that assigns it; if the number only ever emerges from
   arithmetic — a scale product, a clamp ceiling, a default — treat it as a bug candidate, not a
   spec. Collider sizes specifically: `worldRadius = m_Radius × max(|sx|,|sy|,|sz|)`, and sweep
   sibling prefabs for the same authored value.
5. Grep by **class name**, not file name — the vessel layer renamed Ship→Vessel in file names
   only: `VesselActionSO.cs` declares `ShipActionSO`, `VesselHelper.cs` declares `ShipHelper`,
   `R_VesselElementStatsHandler.cs` declares `R_ShipElementStatsHandler`, `VesselActions.cs`
   declares `enum ShipActions`.
6. **Re-fetch any branch you cite immediately before asserting its state** — branches and
   bleeding-edge move mid-session. This skill's own fleet snapshot went stale twice while being
   written: the Dolphin branch grew its row-wiring commit between research and verification, and
   a tooling refactor renamed every editor menu before ship.

## 3. The design-approval gate (do not break this)

**Never invent an element→ability→input mapping or a level-5 upgrade to fill an open slot or to
green the auditor.** Open slots on Manta/Dolphin/Rhino/Serpent-class maps are blocked on
**design, not wiring**: proposals live in `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 and are
un-implemented until Garrett marks them up. If your task requires a mapping that isn't approved,
STOP and ask (AskUserQuestion), presenting the FLEET_MAPS proposal for that row. The same gate
applies to new abilities, new resources on the meter list, and anything that adds a fundamental.

## 4. Implement — the twenty-four rules that keep getting relearned

1. **Ability SOs are shared and stateless.** Per-vessel state lives in executors / vessel-root
   MonoBehaviours; SOs receive `(registry, status)` per call. Never bind state to an SO asset.
2. **Read element scaling at use time** (`ElementalAbilityHandler.Multiplier(element)` /
   `ElementalFloat.EvaluateLive`), never cache at init. **No double-dipping**: if a dedicated
   authored field on the action SO carries the scaling, pin the map's generic
   `MultiplierAtFullLevel` to 1.
3. **Outcome-affecting upgrades gate on `IsUpgradeActive(element)`** (replicated
   `NetElementUnlocks` bits on `R_VesselActionHandler`) — never a raw local level read, which
   desyncs the prismscape across peers. Per-use snapshot at fire/use time.
4. **All buffs/debuffs route through Elementals** (`ResourceSystem.ApplyElementalEffect`), and
   no sustained mechanism may HOLD a level above 10 (the maintained-mechanism law, LOCKED).
5. **Event bindings are one symmetric Rebind/Unbind pair** on OnEnable/OnDisable, detach-first
   in Initialize (vessel swaps re-run Initialize on live components) — and the detach must sit
   **ABOVE** the pilot gate, not below it: `Subscribe() { if (IsAI || !IsLocalUser) return; …}`
   strands the previous pilot's handlers the moment a re-init hands that vessel to an AI or a
   remote owner. Teardown (`OnDisable`) is unconditional and idempotent for the same reason. Gated
   on
   `IsInitializedAsAI || !IsLocalUser` for HUD/pilot-only surfaces, and sender-filtered on
   shared SOAP channels. This exact bug shipped three times on one branch.
6. **Executor→SO resolution retries until success** — `R_VesselActionHandler.Initialize` runs
   executors *before* populating its binding maps, so a first-frame query that latches on
   attempt (not success) pins null forever. Resolve lazily via `CollectBoundActions` — **but
   only for an ability that HAS an input.** See rule 20: a passive ability is in no binding
   map, so that sweep can never find its SO.
7. **One authored number per displayed quantity.** A HUD readout adopts the gameplay component's
   value (`RiptideAnimation.MaxJawAngleDegrees` pattern); never author a "keep in step" copy.
   Bind HUD gauges **by name** with index fallback, and only to resources whose writers raise
   the per-resource event.
8. **Fork shared effect SOs before changing behavior** (the skim effect is shared with other
   vessels), and remember an effect executes **only if wired into the vessel's
   Impactor Data Container** — existing-but-unwired assets do nothing.
9. **Gauge-style ability icons**: `tintIconOnUpgrade = false` + override `SetAbilityUpgraded`
   re-anchoring every captured rest scale to `AbilityIconRestScale(element)` — or the view's own
   tweens erase the upgrade bump (`SquirrelVesselHUDView` is the reference).
10. **Author HUD content into prefabs** — runtime-created petals/rows are the loud fallback, not
    the contract. Clean console in play mode IS part of compliance. Respect platform laws on
    every vessel surface: continuity of existence (even previews bloom/wither),
    MaterialPropertyBlock over `renderer.material`, fail-loud SOAP, implicit-bool over `??` for
    UnityEngine.Object.
11. **A skimmer only skims if `VesselStatus` points AT it.** `VesselController.Initialize`
    initializes **only** `NearFieldSkimmer`/`FarFieldSkimmer`, and `SkimmerImpactor` drops every
    contact while `skimmer.IsInitialized` is false — so a vessel can carry a flawless skimmer
    (trigger sphere, kinematic rigidbody, `ImpactCollider`, container, layer 7) and skim nothing,
    silently, because the reference points at a disabled twin. Run **Audit Vessel Skimmers**
    first; never conclude from the prefab looking right.
12. **Before removing a "redundant" writer, enumerate ALL writers of that meter.** A resource can
    be fed by both `ResourceSystem`'s per-second `resourceGainRate` and an action executor, and
    an executor's own cooldown can block its path entirely — so deleting the passive trickle
    "because gain should come from the ability" left the Dolphin's boost with no working fill
    path at all. The trickle and `rechargeCooldownSeconds` had to move together. Grep every
    writer, then change the set. **The same enumeration is required to FREEZE a quantity**, and
    there the answer is per-writer rather than all-or-nothing: the Dolphin's drift speed hold
    pins the throttle-derived cruise `speed` but deliberately leaves `throttleMultiplier`
    (impact slows) and `velocityShift` (knockback/AOE) live — freezing those too would have
    quietly made a drifting vessel immune to danger prisms, which is a LOCKED-design violation
    hiding inside a feel change. List every writer, then say per writer whether the freeze
    covers it, and record that list in the doc.
13. **A cancelled UniTask never runs its tail.** `catch (OperationCanceledException) { }` means
    any status the routine set *before* its loop stays set forever. Interrupting a discharge left
    `BoostMultiplier`/`IsBoosting` frozen — a permanent free speed bonus. Restore that state in
    the routine's *starter*, not only in its completion path.
14. **A HUD controller must never reach for another vessel's executor by TYPE.**
    `GetComponentInChildren<SomeOtherVesselsExecutor>(true)` compiles fine, returns null on every
    vessel that isn't the one carrying it, and the gauge it feeds then simply never moves — no
    error, no warning, nothing to notice. `SquirrelVesselHUDController` polled the Sparrow-only
    `OverheatingActionExecutor` for its heat gauge for the component's entire life; the gauge was
    dead the whole time and the bug only surfaced when the Sparrow branch DELETED the type and the
    Squirrel stopped compiling. If a HUD needs a signal, bind it on the vessel's OWN component (a
    serialized reference on that vessel's prefab, so a missing wire is visible in the inspector) or
    route it through SOAP. Auditing tip: any `GetComponentInChildren<T>` in a per-vessel HUD
    controller is worth one grep — if `T`'s script GUID appears in exactly one vessel prefab and
    that is not this vessel, the call is dead.
15. **A gauge whose METER is deleted becomes a lie, not a spare part.** Removing the mechanic
    behind a HUD readout leaves an icon that still looks live. Either give it a new signal from the
    same ability (the Sparrow's heat ring became a binary strafing-roll charge pip) or remove it —
    never leave it stuck at a constant. If the new signal is BINARY, keep it visibly binary (0 or 1
    plus a transition); a partial fill on a pip reads as a meter and reopens the question you just
    closed. Drive it from a sibling image, never the ability icon itself, or you collide with the
    four-icon upgrade tint/badge (rule 9).
16. **Intervene in the flight model at `VesselTransformer.AdvanceSpeed`, not at
    `ComputeThrottleTarget`.** Four transformers exist (`VesselTransformer`,
    `SingleStickVesselTransformer` — what the Sparrow and Serpent actually run —
    `GunVesselTransformer`, `CommandVesselTransformer`) and the first two carry their own
    `MoveShip` AND their own `ComputeThrottleTarget`, so a change written into the target reaches
    only the vessels running the class you edited (the single-stick override ignores `XDiff` and
    the throttle-scaler multiplier entirely). `AdvanceSpeed` is the one line both `MoveShip`s call
    — the choke point where anything that must hold for EVERY vessel belongs, and where the
    Dolphin's drift speed hold sits. Two companions of `speed` need the same treatment when you
    touch it: the `toggleManualThrottle` lerp is a SECOND throttle channel living in each
    `MoveShip` (no shipped prefab enables it — check before assuming your change covered it), and
    `_speedTrackingRate` is a latched ramp state (the Rhino's ramp boost) that a naive early-return
    can silently consume.

17. **A `UniTask.Delay(1/rate)` fire loop quantizes to WHOLE FRAMES**, so an authored rate is
    silently `min(rate, framerate)` — a 60 fps client fires twice as fast as a 30 fps one, and
    the rate simply cannot exceed the frame rate. It looks correct at any rate whose interval
    happens to straddle two frames (30/s at 60 fps was right by luck for a year). Owe fire in
    SECONDS and pay it off in whole volleys (`owed += Time.deltaTime`; fire `floor(owed/interval)`),
    capping the per-tick catch-up and DROPPING the excess so a hitch never discharges as a burst.
18. **Never draw from `UnityEngine.Random` in a per-shot hot path.** It is global state that
    deterministic systems seed (`Random.InitState` for the HexRace track), so a gun rolling it
    120×/s makes their output depend on how long someone held a trigger. Use a pure integer hash
    of a per-shot serial: no global state, and peers that agree on the shot count agree on the
    result — which matters wherever the spawned object is local and unreplicated.
19. **Weapon "feel" complaints are usually a CEILING, not a tuning value.** Before re-tuning,
    find what caps output per unit of input: prisms have no HP (one hit = one kill) and a
    sub-upgrade round dies on its first impact, so a Sparrow's ceiling is exactly *rounds/s*.
    Rate, spread and accuracy all multiply a 1:1 relationship and cannot break it — only pierce
    depth, chain effects, or **size** can, and size wins because destruction footprint goes as the
    SQUARE of the radius. Say which ceiling you found before proposing numbers.
20. **A PASSIVE ability is bound to no input event, so `CollectBoundActions` can never resolve
    its SO.** The binding maps are keyed by `InputEvents`; an ability with no input is in none
    of them, so the lazy sweep of rule 6 returns null forever and the executor silently runs on
    its field initializers — an ability that looks wired, logs nothing, and is tuned by an asset
    nobody is reading. Wire the config **directly on the executor** as a `[SerializeField]`, so a
    missing wire is visible in the inspector, and keep the sweep only as a fallback for a vessel
    that still lists the action against an input. (Dolphin crystal seeding, 2026-08-14.)
21. **An ability that wants the camera's FOV must move the speed tunnel's HOME, never
    `Camera.fieldOfView`.** `VesselSpeedTunnel` owns FOV fleet-wide and is the only writer. A
    direct write fails two ways, both silent: while the tunnel is engaged it is overwritten every
    frame, and when the tunnel ENGAGES it captures whatever FOV it finds as the home to restore
    later — so a live zoom is baked in permanently and the player never gets their FOV back.
    Camera POSE is free (the law is explicitly a no-camera-distance-change effect); FOV is not.
    And before adding a public FOV surface to that law for one vessel, check the ability still
    earns it without the zoom — the Dolphin's Echo Sight did, and the surface was reverted.
22. **A shared impact effect is PER-VESSEL WIRING. Audit which containers list it — never infer
    it from the class existing, from an asset existing, or from a doc saying it happens.** An
    effect only runs for a vessel whose `VesselImpactorDataContainerSO` array actually contains
    it, and a missing entry is *totally silent*: no null, no warning, just a consequence that
    never occurs. `VesselChangeSpeedByPrismEffectSO` shipped absent from the Dolphin (whose
    `DolphinVesselChangeSpeedByPrism` asset existed and was referenced by **no** container) and
    from the Sparrow (no asset at all, in the one vessel Dog Fight flies) — so neither slowed on
    any prism, danger included, for the fleet's whole life. **An orphaned effect asset is the
    tell**, and it is one sweep: map every `*.asset.meta` GUID to its name, then check which
    GUIDs appear inside the six `VesselContainers/*.asset` arrays. Anything of that script type
    that appears in none is authored-but-dead. Do the same sweep for TUNING once wired —
    per-vessel instances drift apart silently, and a prism should read the same whichever hull
    hits it. (Dolphin/Sparrow/Manta prism slow, 2026-08-15.)
23. **An impact effect must not scale a SERIALIZED authored field on `VesselStatus` in place.**
    Check whether the property is runtime bookkeeping or a serialized value with an authored
    default before writing it. `BoostMultiplier` is `[SerializeField] boostMultiplier = 4` and is
    what boost sources that don't write it fall back to (`BoostActionSO` only flips `IsBoosting`;
    `VesselResetBoostPrismEffectSO` restores it to an authored base) — so "halve the boost on a
    ram" applied to it ratchets the vessel's authored number toward 1 a little further on every
    collision, permanently, with nothing in the game to restore it. Scale the RESOURCE METER
    instead and let the executor re-derive; a creeping, unrecoverable nerf is indistinguishable
    from a tuning problem for as long as anyone will look. (Dolphin boost ram, 2026-08-14.)

24. **Puppetry amplitudes are FLEET-SCALE — 14-26 degrees is invisible.** A new vessel whose
    animation swings its parts through "a believable" 15-25 deg reads at chase-camera distance
    as *no puppeteering at all*, and the report you get back is "the ship feels dead", not "the
    numbers are small". `RhinoAnimation` is the calibration: wings and engines swing through
    `yawAnimationScaler = 80` deg, the fuselage through 25. Match that order of magnitude, and
    drive the parts that should answer to FLIGHT rather than to the stick off
    `VesselStatus.Speed`, so they keep moving under a boost or a danger-prism slow the stick
    knows nothing about.
    **Corollary — a part's arc must be SIGNED through its rest pose** when its two ends mean two
    states. Rotating "toward rest" as speed rises can only reach the pose the mesh was authored
    in, which is usually neither state you wanted: legs meant to read gear-down-when-slow /
    tucked-at-speed need `Lerp(+hang, -tuck, speed01)`, not `splay * (1 - speed01)`.
    (Scarab hull, 2026-08-15.)

25. **A named accessor that LOOKS like a geometry is often one factor of it.** Rules 4a/4b cover
    an authored number that isn't the effective one; this is its sibling — a property whose name
    promises the real dimension while its body carries only the base term, with the multipliers
    applied at the *use* site. `VesselPrismController.TrailZScale` is `BaseScale.z` alone, but a
    laid trail prism is `BaseScale.z × ZScaler × boostScale × ∛(MASS volume multiplier)`. The
    `waitTillOutsideSkimmer` clearance delay divided by the accessor, so an upgraded vessel's
    prism collider switched on while the prism was still inside the ship — and the symptom
    ("I clip my own trail after upgrading") points at collision code, not at a scale accessor
    three files away. **Read the accessor's BODY and compare it to the expression at the spawn
    site**; if the spawn site multiplies and the accessor does not, the accessor is a base term
    and every consumer sizing real geometry off it is wrong by the same factor. When you fix one,
    document the accessor as a base term so the next reader does not re-adopt it.
    (Self-trail contact, 2026-08-17.)

## 5. Audit, then hand back verification (you cannot run Unity; the human is the gate)

- State which auditors to run and the expected result: **Audit Vessel Ability Rows**,
  **Audit Vessel Skimmers**, **Audit Vessel Elemental Morphs**, plus **Wire Elemental Petal
  Bars** (or **Bake Elemental Petal Bars Into All Vessel HUDs**) and **Plan Vessel Rig Swap**
  where relevant. Vessel-impactor container wiring still has no in-editor auditor, but do NOT
  hand that half back as play-mode-only: run the rule-22 sweep yourself first (GUID → name over
  `*.asset.meta`, then cross-reference the six `VesselContainers/*.asset` arrays) and print the
  per-vessel table — which vessels carry the effect, which are missing it, and whether the wired
  ones share tuning. That is a static, seconds-long check that catches the entire "authored but
  never wired" class before a human ever opens Unity; play-mode checks (prism hit, crystal
  collect ×1, no NREs) then confirm the wiring you already proved exists.
- **Check that the feedback you are asking a human to judge is OBSERVABLE before you ask.** A
  skim's three signals are each individually invisible on a desktop editor: the haptic is a
  NO-OP (NiceVibrations does nothing there), the beam VFX only draws if the skimmed prism
  authored a `ParticleEffect` (several prefabs, incl. the menu trail prism, leave it empty —
  and `Instantiate(null)` throws inside a `.Forget()`ed UniTaskVoid, so it fails invisibly),
  and a gauge that moves a tenth of its range per event reads as nothing. "I feel no X" then
  carries **zero** information about whether X is wired, and three round-trips can be spent
  debugging a chain that was working. Enumerate the signals, ask which of them can actually
  reach the human on their platform, and add a discrete unmistakable beat if the answer is
  none.
- Give numbered in-editor verification steps: scene, action, concrete observable, the SO knobs
  to tune, and an MPPM two-client step wherever replicated state (unlock bits, swaps) changed.
- Anything you could not editor-verify gets a 🔴 entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`
  (what landed / verify steps / first-pass tuning table) — never only a PR body or chat.
- Never claim something works that you have not seen work.

## 6. Update the paper trail (the drift you don't fix becomes the next branch's bug)

When vessel work ships, update in the same branch: `FLEET_MAPS.md` (§1 live table + §2 proposal
→ APPROVED + SHIPPED, Squirrel-style), `ARCHITECTURE.md` §7.2 fleet status, `BACKLOG.md` item →
SHIPPED with deltas, **CLAUDE.md's fleet-status table** (the Dolphin branch updated FLEET_MAPS
but not CLAUDE.md — don't repeat that), the map asset's `UpgradeLabel`/`UpgradeDescription`, and
a co-located design doc `Assets/_Scripts/Controller/Vessel/R_VesselActions/{FEATURE}.md`
(overview + Files table + tuning-knobs table + "## In-editor verification" + "## Follow-ups";
`RHINO_SHIELD_SWIPE.md` is the exemplar). Delete stale serialized blocks left by script-field
renames, and use `[FormerlySerializedAs]` when renaming container fields.

## 7. Growing the contract (how a vessel requirement becomes enforced)

When a new fleet-wide vessel requirement emerges, don't leave it as tribal knowledge — walk it
up the enforcement ladder the shipped systems use:

1. **Single source**: one code constant or Resources-loaded config SO
   (`VesselHUDView.AbilityDisplayOrder`, `ElementalBarsConfigSO`) — never per-prefab fields.
2. **Author-time**: `OnValidate` normalization + an editor-conditional validator called from the
   runtime init path (`ValidateAbilityIconRow` pattern).
3. **Runtime**: warn-and-degrade with the fix named in the warning
   (`CreateDefaultElementBars` pattern) — visible degradation, never silent, never a crash.
4. **Fleet audit**: an asset-only `FrogletTools > Vessels` auditor (`[MenuItem]` + `[FrogletTool(FrogletToolCategory.Vessels, ...)]` so it shows in the master window) that reuses the exact runtime
   discovery code (`VesselElementalMorphAuditor` pattern).
5. **Record it**: CLAUDE.md + this skill's CONTRACT.md + the ship checklist.

**Worked example (2026-08):** the skimmer half of that gap is now closed —
`VesselSkimmerAudit` (`FrogletTools > Vessels > Audit Vessel Skimmers`) walks every vessel
prefab's `NearFieldSkimmer`/`FarFieldSkimmer` to its GameObject and checks active state up the
whole ancestor chain, the impactor/`ImpactCollider`/trigger-collider/`Rigidbody` the trigger path
needs, and whether the container holds prism effects; when a container asks for the forcefield
crackle it also checks the `ForcefieldCrackleController` + its `overlayRenderer`, because that
effect needs **three** pieces across three files and returns silently without any of them.

Remaining gap, next candidate: **vessel-impactor container wiring** (null containers, orphaned
effect assets, an effect authored but never added to the vessel's container) still has only
runtime symptoms.

## 8. Commit

Conventional commits per `GIT_RULES.md` (`type(scope): summary`, imperative, ≤72 chars, .meta
files included); one logical change per branch; develop on the feature branch; open a PR only
when asked.
