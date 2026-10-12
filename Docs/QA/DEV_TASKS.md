# Dev tasks from QA failures

Every entry here was created by a `FAIL` in a submitted `Docs/QA/RESULTS/` file.
Written by the `/qa-backlog` skill — add detail freely, but do not delete an entry
by hand: it closes when its QA item passes on a later run.

**Definition of done for every task below:** the named QA item passes on a build
that contains the fix. Nothing here is done because the code "looks right" — that
is exactly how these items got onto the QA list in the first place.

Status: 🔵 open · 🟠 in progress (branch named) · 🟢 fixed, awaiting retest.

<!-- qa-dev-tasks -->



## DT-001 — The menu-return/loading veil does not hide the build: prisms are visibly **popping in duri 🔵
- **QA item:** QA-MENU-VEIL-PAUSE
- **Failed on:** bleeding-edge @ 5663cc4b3, 6000.3.17f1, Editor, 2026-10-08 by akouroshm
- **Observed:** The menu-return/loading veil does not hide the build: prisms are visibly **popping in during loading**, and on entering a game the player sees a **"metal seal" opening up** — an animation that appears to be a remnant of an old loading-screen concept, not the current veil. The teardown/build is not being covered. (Observed on 71d67ba9b.)
- **Source of the change:** PRs #672, #693, #698 (the menu-return veil hold + the prewarmed pause menu). Design reference: `Docs/CONNECTING_PANEL.md`.
- **Likely files:** two distinct sub-symptoms —
  1. *Prisms popping in during loading* (the veil lifts, or never covers, while the arena is still building): `Assets/_Scripts/Controller/Environment/Spawning/EnvironmentLoadVeil.cs` and the load-gate tempo/hold it rides (`Docs/CONNECTING_PANEL.md`). Check that the veil holds until the build reports ready rather than lifting on a timer.
  2. *The "metal seal" opening animation on game entry* (suspected vestige of an old loading screen): `Assets/_Prefabs/UI Elements/In Game/ConnectingPanel.prefab`, `Assets/_Scripts/UI/ConnectingPanelController.cs`, `Assets/_Scripts/UI/Elements/ConnectingPanel.cs`. Confirm whether that open-animation is intended or leftover art/behaviour to remove.
- **Done when:** QA-MENU-VEIL-PAUSE passes.

## DT-002 — Auditors ran without throwing, but several report beyond the known exceptions 🔵
- **QA item:** QA-AUDIT-TOOLS
- **Failed on:** bleeding-edge @ 88478a6d, 6000.3.17f1, Editor, 2026-10-09 by akouroshm
- **Observed:** Auditors ran without throwing, but several report beyond the known exceptions. Audit Vessel Skimmers: **6 faults** (known exception is Serpent only). Validate Lifeform Crystals: **34 warnings**. Measure Cell Environment Baselines: **1 warning**. Game Mode Prefab Kit Validate: **75 warnings**. Pending Tool Changes: **~11 uncommitted files**. Ability Rows / Elemental Morphs / Speed Tunnel / Corridor Radii / Cell-Owned Visuals / Occlusion Corridor: ran clean. NOTE: the item's step 10 is STALE — End Game Conditions correctly shows Wildlife Liberation **30** (the mode was re-targeted 250 to 30) and Dog Fight 120; 30 is right, so that line is a doc bug in this item, not a tool fault (fixed in the item body).
- **Triage, per auditor** (each is a separate investigation — this task is a cluster, not one bug):
  - *Audit Vessel Skimmers — 6 faults:* the six hulls named by the audit are **Sparrow, Urchin, Serpent, Scarab, Rhino, Grizzly** (QA 2026-10-09). Classified from prefab wiring (`Assets/_Prefabs/Spacevessels/<hull>.prefab`):
    - **Serpent** — already tracked, do NOT re-triage here: wires an inactive `VacuumSkimmer` with no impactor/container; fix lives in `QA-P2-SERPENT-SKIMMER` / `Docs/ElementalAbilitySystem/BACKLOG.md §10–14`.
    - **Sparrow, Scarab, Grizzly** — each wires a **`DummySkimmer`** placeholder (no real impactor/effect container), so they fault by incompleteness, not regression. These are combat / ball / AI hulls; the design question is *should this hull skim at all?* If no, add it to the audit's known-exception set; if yes, author a real skimmer + impactor + prism-effect container.
    - **Urchin** — wires the base `Skimmer`; check its active state and whether its container holds any prism effects.
    - **Rhino** — wires `ForceFieldSkimmer` (the energy sword) plus `GrowSkimmer`/`ScaleSkimmer`. This is the **suspicious one**: the Rhino's skimmer is load-bearing gameplay (the sword skims), so a fault here is most likely a real wiring regression rather than an unauthored placeholder. Verify `NearFieldSkimmer`/`FarFieldSkimmer` point at the active ForceFieldSkimmer and that `Skimmer.IsInitialized` holds at runtime (the Dolphin's dead-skimmer bug, `DOLPHIN_ENERGY_ECONOMY.md §5`, is the reference failure mode).
    Owner surface: `FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers` (it reports the exact fault per hull — assignment / active-up-the-ancestor-chain / missing components / empty effect container).
    - **Classified offline, 2026-10-10** by `python3 Tools/Build/audit_vessel_skimmers.py`, the editor audit's twin (same checks, read from the prefab YAML through the nested prefab instances and their overrides; it reproduces QA's six hulls exactly — Sparrow, Urchin, Serpent, Scarab, Rhino, Grizzly — and passes Butterfly, Dolphin, Manta, Squirrel). Per hull, the reported fault and what it actually is:
      - **Sparrow, Grizzly** — `container 'SparrowSkimmerImpactorDataContainer' has no prism effects` (the Grizzly's `DummySkimmer` borrows the Sparrow's container). **Scarab** — `container 'ScarabSkimmerImpactorDataContainer' has no prism effects`. All three are complete placeholders (impactor, ImpactCollider, Rigidbody, trigger sphere all present); the only thing missing is a prism effect in the container, so each is the design question above (should this hull skim prisms at all?), not a wiring bug.
      - **Urchin** — `SkimmerImpactor has no effect container`. Its near field is a plain instance of `Components/Skimmer.prefab`, whose impactor serializes no `skimmerImpactorDataContainer` at all (the base asset predates the container field), and the Urchin's instance never overrides it — every other hull on that base does (Squirrel, Manta). There is no Urchin skimmer container asset anywhere under `_SO_Assets/Effects`, so this is "the Urchin was never given a skim", not drift. Fix if it should skim: author `UrchinSkimmerImpactorDataContainer.asset` and set it on the instance's impactor (the same override shape `Rhino.prefab` carries for its sword).
      - **Rhino** — `no trigger collider`. The Rhino's instance of `ForceFieldSkimmer Variant` REMOVES the base skimmer's trigger `SphereCollider` (`m_RemovedComponents` on the instance) and adds a non-trigger `CapsuleCollider` (the sword, `{1.5, 30, 4.8}`) on the same object. Everything else is wired: impactor → this skimmer, `RhinoForceFieldSkimmerImpactorDataContainer` with two prism effects, ImpactCollider, Rigidbody, every ancestor active. The capsule reaches shielded prisms through the shell tier (`PrismShellContactManager.AppendProbe` builds a capsule probe from it by name) and unshielded prisms through PhysX anyway, because every prism collider in the project is a trigger (one trigger side is enough for `OnTriggerEnter`; the skimmer carries the Rigidbody). So the audit's "no trigger collider" is a heuristic that does not see the other side, and the sword skims. **The open question is only whether removing the sphere was deliberate:** `SkimmerImpactor`'s own comment says the Rhino probes "the sphere; on the Rhino also the sword capsule", which the shipped prefab contradicts. A human who knows the sword decides; the audits stay as they are until then (a Rhino exception in the audit would hide a later real break).
      - **Serpent** — `'VacuumSkimmer' is INACTIVE; no SkimmerImpactor; no ImpactCollider; no Rigidbody`: tracked, as above.
      - **Manta** passes only because its instance REMOVES the base impactor + ImpactCollider and adds its own pair with `MantaStingSkimmerImpactorDataContainer`; a reader that ignores `m_RemovedComponents` sees two impactors, the first without a container. The offline tool honors the removal list; a future tool must too.
  - *Validate Lifeform Crystals — 34 warnings:* dump the list; the new flora families (Borromean, Mandelbulb) and recently-added fauna are the likely source. `FrogletTools ▸ Validation ▸ Validate Lifeform Crystals`.
    - **Named offline, 2026-10-10** by `python3 Tools/Build/audit_lifeform_crystals.py` (the validator's twin: same two checks over the prefab YAML, nested instances and removals honoured, and over the 281 lifeform config assets). It reports **32** findings; the validator also prints its two per-half summary lines as `LogWarning`, which makes QA's **34**. They are: **1 prefab**, `_Prefabs/FloraAndFauna/Populations/Qfish.prefab`, which carries a `LightFauna` and no `Crystal` at all (the runtime guard provisions one at play time and logs it, so the prefab needs its crystal authored); and **31 fauna configs with no `Variant` block enabled**, none of them a flora family: the six Swarm Cell builder/swarm configs, `Lifeforms/WormColonyFaunaConfig`, the NCA lizard, the ten Substrate species (`Substrate Fauna/*`), and the thirteen `Swarm Fauna/Bench/*` fixtures. Every other config authors its heart (`author_lifeform_heart_sizes.py --check` is green). The substrate and swarm species keep their hearts in the core's heart list rather than the per-config Variant block (`Docs/SUBSTRATE_FAUNA.md` §6), and the Bench configs are benchmark fixtures, so the question for each family is whether it should author a `Variant.HeartWorldScale` or be a documented exception in the validator; until that is decided the count stays at 34 on every run. The twin's `--self-test` gates both static CI jobs, which print the live report beside it. Its fixtures are synthetic, so no gameplay edit can turn it red: a lifeform prefab whose crystal comes through a nested instance with an element override passes, and each half is broken once (no crystal, a crystal the instance removes, a non-elemental crystal, two crystals; no Variant block, a disabled one, one with no HeartWorldScale). The skimmer twin's `--self-test` is synthetic the same way, and since 2026-10-12 it also reads hulls in subfolders and hulls authored as Prefab Variants (VesselStatus inherited from the base), resolves a component added on an intermediate prefab from the hull, and lists every prefab with no VesselStatus as not audited instead of dropping it.
  - *Game Mode Prefab Kit — 75 warnings:* most likely the GameCanvas / shared-prefab drift this repo tracks (`Docs/GAMECANVAS.md`); confirm whether they are the known fork-scene overrides or new.
    - **Read offline, 2026-10-10:** `KitValidator.CheckCrossSceneDrift` emits one Warning per drifted scene instance (`<scene>: N unapplied override(s)`) plus one per kit entry whose identical overrides could be consolidated, and `CheckActiveScene` one per Required entry missing from the OPEN scene, so 75 is the size of the drift inventory, not a count of regressions. The GameCanvas half is gated offline and is green on this tree (`python3 Tools/Build/gamecanvas_unification_report.py --check`: 15 scenes on the one prefab, fork gone, no override walls). What is left to triage is the per-entry drift of the OTHER kit prefabs; the editor report names each scene and count, so paste it the next time the tool runs rather than re-deriving it here.
  - *Pending Tool Changes — ~11 uncommitted files:* an editor tool wrote assets that were never committed (the "tool output is a deliverable" trap). Identify which tool and land its output (`/ship-tools`).
  - *Measure Cell Environment Baselines — 1 warning:* a single cell's PhaseThresholds drifted from its measured baseline; name the cell.
- **Note:** the known-exception lists in the QA item itself may also be stale (it predates the Butterfly vessel and the arena hulls); refresh them as hulls are triaged so this stops reading as a blanket FAIL.
- **Done when:** QA-AUDIT-TOOLS passes (every auditor clean or only genuinely-known exceptions, with the exception list brought current).

## DT-003 — EditMode Run All: **2685 pass, 35 fail** (total ~2720 — far above the item's stale "≈762"  🔵
- **QA item:** QA-EDITMODE-TESTS
- **Failed on:** bleeding-edge @ 5ba3e941, 6000.3.17f1, Editor, 2026-10-12 by akouroshm
- **Observed:** EditMode Run All: **2685 pass, 35 fail** (total ~2720 — far above the item's stale "≈762" expectation, which is now corrected in the item body). All **11** named step-4 suites are GREEN. The 35 failures span 14 suites, several entirely red (FollowHeightScale 6/6, RaceRankToastDriver 7/7, RearViewLaw 5/5, SparrowCombatTier 3/3, PartyInviteSystem 3/3) — a whole-suite-red pattern that usually means a shared regression or stale assertions, not 35 independent bugs. Screenshot: `evidence/2026-10-12-akouroshm/QA-EDITMODE-TESTS-35-failures.webp`
- **The 35 failing tests, by suite** (source files under `Assets/_Scripts/**/Editor/` / `Assets/_Scripts/Tests/Editor/`):
  - **FollowHeightScaleTests (6/6 — whole suite red):** ANewFollowTargetResetsTheHeight · ComposesWithTheRearView · DefaultIsTheAuthoredOffset · ScaleIsAppliedAtThePointOfUseAndSurvivesReappliedSettings · ScaleIsClampedToUnitRange · ZeroHeightSitsDirectlyBehindAtTheSameDistance
  - **RaceRankToastDriverTests (7/7 — whole suite red):** ANewTurn_ForgetsTheRankingAndReAnnouncesTheLeader · BreakingATieAtTheTop_AnnouncesTheNewLeader · BreakingThatTie_IsPassingThem · PassingSomeone_IsAnnounced · PassingTwoPlayersAtOnce_PostsTwoToasts_NearestFirst · TakingTheLead_PostsBothSituations · TheFirstCrystal_NamesALeader
  - **RearViewLawTests (5/5 — whole suite red):** ForwardPoseIsUnchangedByTheFeatureExisting · RearViewFollowsALiveZoomInsteadOfFightingIt · RearViewLooksBackAtTheShip · RearViewMirrorsZOnlySoHeightAndOffsetAreKept · RearViewSitsTheSameDistanceAheadThatTheCameraSatBehind
  - **SparrowCombatTierTests (3/3 — whole suite red):** EveryTierIsWiredIntoTheThingThatProducesIt · FiftyHostilePrismsBuyOneRocket · TheChargeCardCarriesTheMissileGauge
  - **PartyInviteSystemTests (3/3 — whole suite red):** HasOpenSlots_AfterRemovingMember_ReturnsTrue · HasOpenSlots_FourMembers_ReturnsFalse · PartySlot_FullParty_ThenKick_OpensSlot
  - **ElementalScalingUnificationTests (2):** ADisabledElementalFloatIsExactlyOneEverywhere · SparrowSpaceWouldInvertWithoutItsFloor
  - **WeeklyChallengeTests (2):** DayKey_IsTheUtcDate_AndTestDaysAreASeventhOfTheTestPeriod · NextRolloverUtc_IsTheFollowingMonday
  - **EnumIntegrityTests (1):** GameModes_MultiplayerModes_AllContainMultiplayerInName
  - **LifeformHeartSizeTests (1):** CanonicalLifeformSet_IsTheWholeSpeciesTimesElementMatrix
  - **PrismLitTests (1):** ShaderReadsTheDomainGateBeforeAnyGeometry
  - **RampageIntensityLadderTests (1):** RampageSpawnProfiles_ClimbTheFaunaLadder_AndShareOneForest
  - **ScreenshotFramingTests (1):** Solve_WorldAlignedIgnoresTheSubjectsHeading
  - **ShieldedSignalColorTests (1):** TheSentinelIsRefused
  - **ThresherChainSolverTests (1):** PivotSteer_InPlaneStickDoesNothing
- **Triage hint:** group by suite, not by test. A suite that is **entirely red** (5 of the 14) almost always means one shared cause — a renamed/changed API the test binds to, a changed default/constant, or an assertion that was written against behaviour that has since been re-authored (several of these subjects moved this cycle: the rear-view/follow-height camera work, the Sparrow combat-tier re-cut, the race-rank toast driver). Start with the all-red suites: for each, read the suite's `[SetUp]` + the first failing assertion — it will usually reveal whether the TEST is stale (update the test) or the SUBJECT regressed (fix the code). The single-test failures are likely independent and cheaper.
- **Note:** these are assertion failures, not a compile break — the suite compiled and ran 2720 tests, so this is not the "suite silently stopped compiling" failure the item also guards against. Capture each suite's first assertion message when triaging (QA's screenshot gives names only).
- **Done when:** QA-EDITMODE-TESTS passes (EditMode Run All fully green).

## DT-004 — Full-auto shots have launch sound; **the skyburst missiles have NO launch sound** 🔵
- **QA item:** QA-SPARROW-PROJECTILE-POOL
- **Failed on:** bleeding-edge @ 5ba3e941, 6000.3.17f1, Editor, 2026-10-12 by akouroshm
- **Observed:** Full-auto shots have launch sound; **the skyburst missiles have NO launch sound**. Both shot types still interact with prisms (live colliders), and no NullReferenceException was reported. So this is specifically a missing missile launch SFX, not a dud projectile or a pool/injection crash. Likely an empty/unwired `EventReference` on the skyburst launch path (per the FMOD exposed-field convention, an empty slot ships silent).
- **Scope note:** this is **narrower than the original QA item**, which was about async-refilled pooled projectiles going silent/dud seconds after spawn (an injection NRE from `LaunchProjectile`). That failure mode did NOT occur — colliders are live and there is no NRE. What's missing is only the skyburst's own launch sound, on every missile (not just post-refill ones), while the full-auto gun is audible. So treat this as an **authoring/wiring gap on the skyburst launch SFX**, not a pool-injection regression.
- **Likely files:** the skyburst bay / gun action and its launch-sound field — `Assets/_Scripts/Controller/Vessel/R_VesselActions/` (the SkyBurst gun action SO + executor; the full-auto path is `FullAutoActionSO` / `FireGunActionExecutor`, which IS audible — compare the two). Reference: `R_VesselActions/SPARROW_SKYBURST_BAY.md`. Per the FMOD convention (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`), the fix is to author the skyburst launch `EventReference` on the component that fires it (ship it as a real event, not a borrowed/temp one) — confirm the field exists and is simply empty before adding one.
- **Done when:** QA-SPARROW-PROJECTILE-POOL passes (skyburst launches are audible; full-auto still audible; colliders live; no `LaunchProjectile` NRE).

<!-- /qa-dev-tasks -->

---

### Entry format (for reference)

```
## DT-NNN — <one-line symptom> 🔵
- **QA item:** QA-PRISM-OCCLUSION (step 1)
- **Failed on:** bleeding-edge @ 2e2d3aaf, Unity 6000.0.x, Editor/Windows, 2026-08-06 by <tester>
- **Observed:** <verbatim console text / description>
- **Source of the change:** PR #661 (`claude/transparent-prism-occlusion-3fwjky`)
- **Likely files:** `_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl`, `PrismOcclusionCorridor.cs`
- **Done when:** QA-PRISM-OCCLUSION passes.
```
