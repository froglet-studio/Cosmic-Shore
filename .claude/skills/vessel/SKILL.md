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
   **FrogletTools > Vessels > Audit Vessel Ability Rows** and **Audit Vessel Elemental Morphs** —
   both asset-only, both reuse the exact runtime discovery code, so report and game cannot
   disagree.
4. Grep by **class name**, not file name — the vessel layer renamed Ship→Vessel in file names
   only: `VesselActionSO.cs` declares `ShipActionSO`, `VesselHelper.cs` declares `ShipHelper`,
   `R_VesselElementStatsHandler.cs` declares `R_ShipElementStatsHandler`, `VesselActions.cs`
   declares `enum ShipActions`.
5. **Re-fetch any branch you cite immediately before asserting its state** — branches and
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

## 4. Implement — the ten rules that keep getting relearned

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
   in Initialize (vessel swaps re-run Initialize on live components), gated on
   `IsInitializedAsAI || !IsLocalUser` for HUD/pilot-only surfaces, and sender-filtered on
   shared SOAP channels. This exact bug shipped three times on one branch.
6. **Executor→SO resolution retries until success** — `R_VesselActionHandler.Initialize` runs
   executors *before* populating its binding maps, so a first-frame query that latches on
   attempt (not success) pins null forever. Resolve lazily via `CollectBoundActions`.
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

## 5. Audit, then hand back verification (you cannot run Unity; the human is the gate)

- State which auditors to run and the expected result: **Audit Vessel Ability Rows**,
  **Audit Vessel Elemental Morphs**, plus **Wire Elemental Petal Bars** (or **Bake Elemental
  Petal Bars Into All Vessel HUDs**) and **Plan Vessel Rig Swap** where relevant. Impact/skimmer wiring has **no auditor** — hand back
  explicit play-mode checks for it (prism hit, crystal collect ×1, skim, no NREs).
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

Known gap, first candidate for step 4: **impact-effect/skimmer container wiring has no
auditor** — misconfigurations there (null containers, unwired skimmers, orphaned effect assets)
have only runtime symptoms today.

## 8. Commit

Conventional commits per `GIT_RULES.md` (`type(scope): summary`, imperative, ≤72 chars, .meta
files included); one logical change per branch; develop on the feature branch; open a PR only
when asked.
