# Editor Tool Ledger

**Purpose.** Claude sessions cannot run Unity. When a session needs an asset authored,
migrated, or wired, it often writes a `[MenuItem]` editor tool and hands the human a
"click this" step. That step gets skipped: the branch merges with the **tool** but without
the tool's **output**, so `bleeding-edge` ends up carrying code that expects wired assets
nobody wired — plus a menu item that lingers forever after its job is done.

This ledger is where that obligation is **recorded once** instead of living in a PR body
or a chat message that scrolls away. It is the companion to
`Docs/UNITY_VERIFICATION_CHECKLIST.md`: that doc tracks *"a human should look at this"*,
this one tracks *"a human must run this and commit the diff"*.

Enforced by `/ship` §2.5 (the editor-tool discharge gate), whose evidence-gatherer is
`.claude/skills/ship/tool-discharge-check.sh`.

> **Why the script matters more than this file.** `UNITY_VERIFICATION_CHECKLIST.md` was
> created 2026-07-22 with one entry and never updated again — a doc-only convention that
> was abandoned after a single use. The gate is what keeps this file honest; if you find
> yourself updating the ledger by hand without the gate having asked you to, the
> convention is rotting again.

## Rules

1. **Every tool gets a row.** Adding, changing, or retiring a `[MenuItem]` tool means
   editing this file in the same commit.
2. **Two kinds, and the kind decides the fate.**
   - **Standing** — validators, auditors, reports, generators re-run on demand. Keep
     indefinitely. Docs cite the menu path.
   - **One-shot** — authors/migrates/wires assets once, then is dead weight. Must be
     *discharged* (run + output committed), then *retired* (deleted).
3. **Prefer never incurring the obligation.** Before writing a one-shot tool, check whether
   `/asset-surgery` can author the asset programmatically. A tool is justified only when
   the edit genuinely needs the running editor (importer, mesh/lightmap bake, scene
   instantiation from runtime state).
4. **A one-shot tool is discharged only when its output is committed** — the asset diff is
   the proof, not the intention. Record the commit SHA in the row.
5. **Retiring a one-shot tool rewrites its docs.** Every doc that said "run Tools > …"
   becomes past tense with a recovery pointer:
   `git show <sha> -- Assets/_Scripts/Editor/FooTool.cs`. Docs must never point at a menu
   item that no longer exists, and a retired tool must always be recoverable by SHA.
6. **`⏳ PENDING` is the only legal way to merge an undischarged one-shot tool** — and only
   with a complete discharge block (menu path, expected result, expected asset paths, the
   commit+push line) and a named owner.
7. **Scope is all of `Assets/`, not just `_Scripts/Editor/`.** First-party tools also live
   in `Assets/Editor/` (`CreateNewClass`, `CreateNewMiniGame`, `ToastNotificationSetup`,
   `MeshGeneration/PrismMesh`). Third-party menus (`PlayFabEditorExtensions/`,
   `PrimitivePlus/`, `YethGameDev/QuickScenePro/`, `Plugins/`) are out of scope.

## Status legend

| | meaning |
|---|---|
| ♾️ STANDING | Re-runnable utility. Never "done". No discharge owed. |
| ⏳ PENDING | One-shot, **not yet run** (or run but output not committed). Owes a run + push. |
| 🔧 FIX FIRST | Tool is broken/mis-scoped — running it now would do the wrong thing. |
| ✅ RUN | One-shot, output committed. Now owes **retirement** (delete + doc rewrite). |
| 🗑️ RETIRED | Deleted from the tree. Row kept as the recovery pointer. |

---

# ⏳ Owed a run

Discharge blocks are below in [§ Discharge blocks](#discharge-blocks).

**2026-08-03 update: D1–D4 were discharged programmatically on this branch** via
`/asset-surgery` — no editor run needed, the asset diffs are in the branch. What remains
for a human is D5, D6, and the verification pass (see § Remaining human steps).

| # | Tool | Status |
|---|---|---|
| [D1](#d1--strip-crystal-audiosources--discharged-by-surgery) | `StripCrystalAudioSourceTool.cs` | ✅ **DISCHARGED by surgery + tool RETIRED.** All 1,017 `!u!82` AudioSource docs removed across the 10 Crystal-carrying prefabs (9 crystals + `SpawnedSegments.prefab`'s 1,008), pure-deletion diffs, reference closure machine-verified. `BigCrystalVariant` inherits the stripped base — no dangling override (a hazard the editor-tool path had). One stale dormant `m_Enabled` override remains in `TadPoleFauna.prefab` targeting the removed component — Unity ignores it; an editor run would have left it identically. |
| [D2](#d2--restore-toy_conveyors-omnicrystalprefab--discharged) | `ToyboxSetupTool.cs` (one field) | ✅ **DISCHARGED.** `Toy_Conveyor.asset` `omniCrystalPrefab` restored to the exact reference `a0b32006` wrote (verified live against `Crystal.prefab`'s root component). |
| [D3](#d3--bake-elemental-petal-bars-into-sparrow--discharged) | `ElementalPetalBarWirer.cs` | ✅ **DISCHARGED by surgery.** 88 documents authored into `Sparrow.prefab` (4 `*_Flower` containers at the wirer's default row + 20 petals), donor-cloned from `SquirrelHUDVariant.prefab`'s wirer-authored output; sprites/rotations/grey verified against the donors and the ElementPetals metas; full parent/child reference closure verified. Flowers sit at the wirer's default positions — reposition to taste (optional aesthetics). |
| [D4](#d4--commit-the-five-missing-meta-files--discharged) | *(not a menu item)* | ✅ **DISCHARGED.** Five `.meta` files minted (donor-cloned `MonoImporter` format, repo-wide GUID collision sweep) and committed. If your local Unity already generated untracked `.meta` files for these five, delete the local copies and take the committed ones — nothing in-repo can reference a GUID that was never committed. |
| [D5](#d5--raycast-target-audit--prefab-pass) | `RaycastTargetAuditTool.cs` | ⏳ **HUMAN.** The **scene** half is done (`9c5dd537`); the **prefab** half is not: `GameCanvas.prefab` 71 × `m_RaycastTarget: 1`, `GameCanvas-HexRace.prefab` 114, `ArcadeGameConfigureModal.prefab` 107, `R_GameOverPanel.prefab` 57. Not surgically dischargeable: the tool's candidate classifier runs over the **composed** prefab (nested instances included) and the flips must be play-validated — both need the editor. |
| [D6](#d6--prism-grid-explosion-scene--decide-first) | `PrismGridTestSceneSetupTool.cs` | ⏳ **HUMAN (decision).** Both declared outputs absent: `Assets/Resources/PrismGridTestConfig.asset`, `PrismGridExplosionTest.unity`. Whether that is a debt depends on whether `BenchmarkResults/PrismExplosion/*.json` exists on the prompter's machine — unknowable from the repo. |

# ✅ Fixed on this branch (were "fix before running")

All three defects below are **fixed in code** — they need no editor time, only the review
that comes with the branch. Machine-verified as far as is possible without Unity (brace
balance, symbol resolution, call-site signatures, no dangling references).

| Tool | Defect | Fix |
|---|---|---|
| `CanvasUpgradeProcessor.cs` | `UpgradeRectHierarchy` had **no nested-prefab guard**, so upgrading a canvas re-scaled any already-×2.4 nested fragment to **×5.76**. The fragment path guarded its own re-runs via `ProjectSettings/CanvasUpgraderUpgradedPrefabs.txt`; descendants had no such guard. | New `CollectAlreadyUpgradedNested` computes the skip-set once per walk and all **7** scaling loops consult it. Conservative by construction: a transform is skipped only when its nested instance root resolves to a real source asset whose GUID is positively in the log — anything unresolvable is scaled exactly as before. This unblocks the `GameCanvas.prefab` run. |
| `LifeFormCrystalValidator.cs` | Filtered on `LifeForm \|\| LightFauna`. But `LightFauna` and `Boid` are **siblings** under `Fauna`, so every `Boid` prefab was silently skipped — `TadPoleFauna.prefab` and `TermiteDrone.prefab` were never checked, and the tool reported a clean bill it had not verified. | Now filters on `ILifeFormEntity`, the interface both branches implement (`LifeForm → Flora`, `Fauna → LightFauna / Boid`), so future lifeform types are covered automatically. |
| `Assets/Editor/ToastNotificationSetup.cs` | `CreateSettingsAsset` wrote to `Assets/_SO_Assets/` while `AddManagerToScene` read from `Assets/Resources/`. Since `ToastNotificationAPI` uses `Resources.Load`, the write path was **invisible to the shipping game** — a run authored a second settings asset nothing would ever read. | Single `SettingsPath` constant under `Assets/Resources/`, used by both methods; the now-unused `SOFolder` constant is gone. |

**Still needs the editor:** the canvas fix only *unblocks* `GameCanvas.prefab` — that run is
still owed, and it is the one genuine gap in the canvas migration (see the note below).
The lifeform validator should be re-run now that it actually scans `Boid` fauna; expect it
to report on `TadPoleFauna` and `TermiteDrone` for the first time.

Neither is blocking: the canvas fix is fail-safe (it can only skip, never over-scale), and
the validator is read-only.

## The Canvas migration is mostly a doc bug, not a pending run

An earlier revision of this ledger claimed "migration is partial — 3 canvases still at
800×450". Adversarial review refuted two of the three:

- `_Scenes/Singleplayer Scenes/SplashScreen.unity` — **not in build settings** (dead scene).
- `_Prefabs/GameCanvas-HexRace.prefab` — overridden to 1920×1080/240 by **all six** of its
  consumers, so its serialized base value never reaches a player.
- `_Prefabs/CORE/GameCanvas.prefab` — the one genuine gap, and it is blocked on the
  nested-prefab guard above.

The migration is otherwise substantially discharged: 48 asset-level fragment upgrades
across 5 commits (latest 2026-07-28) plus 7 scenes carrying the ×2.4 signature. A raw
`grep` for `m_ReferenceResolution: {x: 800, y: 450}` is **structurally blind** to where
that output actually landed — do not re-derive the old claim from it.

# 🗑️ RETIRED on this branch

Deleted, each verified to have **zero** references across `.cs`/`.prefab`/`.unity`/`.asset`
before removal. Recover any of them with
`git show 3193f058 -- <path>` (the commit immediately before deletion).

| Tool | Why it was dead |
|---|---|
| `Assets/_Scripts/Editor/ProfileAvatarBinder.cs` | Binds a `ProfileImage` component with **0 instances** in any scene or prefab and **0 code references**; superseded by `ProfileDisplayWidget`. (`ProfileImage.cs` is now fully orphaned — deleting it too is a design call, not a tool discharge, so it was left in place.) |
| `Assets/_Scripts/Editor/PlayfabProductGenerator.cs` | Authored PlayFab catalog products against `AuthenticationManager.PlayFabAccount` — the deprecated PlayFab auth. CLAUDE.md documents PlayFab as legacy/inert and the store is UGS Purchasing; the only remaining `PlayFabEconomyAPI` caller is `Utility/ChoppingBlock/AndroidIAPExample.cs`. A revival would be a rewrite against UGS Economy, not a recovery of this file. |
| `Assets/_Scripts/Editor/TriangleWindowMeshGenerator.cs` | Despite the name, generated a procedural **cube** into the open scene and wrote **nothing to disk** (no `AssetDatabase.CreateAsset` anywhere). No mesh asset by that name exists; no consumer past or present. Deleting it cannot break a reference because it never produced one. |

# 📝 Doc bugs (no editor needed)

| Location | Problem |
|---|---|
| `CLAUDE.md:1083` | Tells the reader to run `Tools > Cosmic Shore > Create Party Prefabs`. **No such tool exists anywhere in the repo** — the doc points at a menu item that was never committed. The prefabs it describes are already on disk. |
| `CLAUDE.md:1794` | "The `elementBars` reference is null-safe — vessels without the view wired simply show no bars (opt-in rollout)." **False.** `ElementalBarsController.InitializeElementBars` force-creates the view at runtime (its comment: "a REQUIRED system on every vessel") for any vessel with a `Canvas`, logging a warning. The five Canvas-less vessels (Urchin, Grizzly, Termite, Falcon, Shrike) silently get nothing. There is no pending fleet-rollout decision. |
| `ElementalBarsController.cs:83-84` | The runtime warning tells you to run "Bake Elemental Petal Bars Into All Vessel HUDs" — but that tool only re-authors prefabs that **already** carry a view, so it no-ops for exactly the vessels that trigger the warning. |
| `Docs/PERFORMANCE_OPTIMIZATION.md:228, :596` | Both assert the raycast audit "has still not been run in-editor". True only of the **prefab** pass — the scene pass landed at `9c5dd537`. Sourced from a 2026-07-09 capture that predates the 2026-07-17 output. |

# ♾️ Standing (no discharge owed)

| Tool | Menu | Note |
|---|---|---|
| `PrismClockWiringValidator.cs` | `… > Prism Animation > Validate Clock Wiring` | Wiring itself **is** landed — all three graphs carry the clock properties + Custom Function nodes. |
| `PrismClockGraphWirer.cs` | `… > Prism Animation > Auto-Wire Clock Properties (All Graphs)` | Idempotent **repair** tool (writes graph JSON via `File.WriteAllText`). Normally reports "already present". Named in the troubleshooting table as the recovery path. |
| `PrismClockSmokeTest.cs` | `… > Prism Animation > Smoke Test - Re-Bloom Nearby Prisms` | Play-mode diagnostic. |
| `CellEnvironmentBaselineMeasurer.cs` | `… > Measure Cell Environment Baselines` | **Output was transcribed** — the six freestyle cells carry distinct measured thresholds (Caldera 441891 · Daedala 648504 · Geode 574650 · Orrery 206986 · Yggdra 552356 · Zephyr 437285). |
| `EndConditionOverridesWindow.cs` | `… > End Game Conditions` | Only authoring path for end-game counts. `Resources/EndConditionOverrides.asset` exists with authored values. |
| `VesselAbilityRowAuditor.cs` | `… > Audit Vessel Ability Rows` | Read-only fleet compliance report. |
| `VesselElementalMorphAuditor.cs` | `… > Audit Vessel Elemental Morphs` | Read-only, asset-only. |
| `VesselRigSwapPlanner.cs` | `… > Plan Vessel Rig Swap` | Report-only by design. The Dolphin/Urchin/Rhino rig swap it plans is a **human editor pass** tracked in CLAUDE.md — not a tool discharge. |
| `ToyboxSetupTool.cs` | `… > Setup Freestyle Toybox` | Re-runnable authoring/repair. Output otherwise landed: `Resources/Toybox.asset`, all 6 `Toy_*.asset`, all 16 `Painting_*.asset`, and `ToyboxController` wired in `Menu_Main.unity`. Only the D2 field is missing. **Re-run caveat**: it re-saves `Menu_Main.unity` unconditionally, so expect incidental scene churn. |
| `PrismExplosionBenchmarkReport.cs` | `… > Prism Grid Benchmark > …` | Report generator over benchmark runs. |
| `PerformanceBenchmarkWindow.cs` | `FrogletTools > Performance Benchmark` | Standing instrumentation (`BENCHMARK_TOOL.md`). |
| `CosmicShoreBuildPipeline.cs` | `… > Build > …` | Build entry points (`Docs/BUILD_AND_DELIVERY.md`). |
| `ForceReserializeScriptableObjects.cs` | `FrogletTools > Legacy > Force Re-Serialize …` | Maintenance utility for serialization drift. |
| `CanvasUpgraderWindow.cs` | `… > Canvas Upgrader` | Standing — new UI keeps arriving at the old reference resolution. Nested-prefab guard fixed on this branch; the `GameCanvas.prefab` run is still owed. |
| `LifeFormCrystalValidator.cs` | `… > Validate Lifeform Crystals` | Enforces the every-lifeform-drops-a-crystal invariant. Now scans `Boid` fauna too (fixed on this branch). A composition-aware static run (2026-08-03) found 8 violations — see § Remaining human steps #5. |
| `Assets/Editor/ToastNotificationSetup.cs` | `Cosmic Shore > Toast Notification > …` | Authors the toast settings/channel/prefab. Settings path fixed on this branch (`Assets/Resources/`, where `Resources.Load` can see it). |
| `RaycastTargetAuditTool.cs` | `… > UI > Raycast Target Audit` | Standing — re-run as UI grows. Has an un-discharged prefab pass (D5). |
| `DialogueEditorWindow.cs`, `ElementalFloatEditor.cs`, `ComponentCopierWindow.cs`, `FindAssetByGUID.cs`, `SceneObjectCounter.cs`, `TextureMemoryUseWindow.cs`, `RuntimeTextureMemoryUsageWindow.cs`, `LogControlWindow.cs`, `FrogletTools.cs`, `AnimationRecorderWindow.cs`, `SceneBootstrapper.cs`, `ProfilerCsvLoggerMenu.cs` | various | Authoring/inspection utilities. No output obligation. |
| `Assets/Editor/CreateNewClass.cs`, `CreateNewMiniGame.cs`, `MeshGeneration/PrismMesh.cs` | various | First-party scaffolding generators outside `_Scripts/`. |

---

# Discharge blocks

### D1 — Strip Crystal AudioSources — DISCHARGED BY SURGERY

Done programmatically on branch `claude/ship-safeguards-tool-cleanup-fn6lk6` (2026-08-03):
all 1,017 `!u!82` AudioSource documents and their `m_Component` entries removed across the
10 Crystal-carrying prefabs under `_Prefabs/Environment` (the 9 crystals + 1,008 inlined in
`SpawnedSegments.prefab`). Machine-verified per file: removed ids appear nowhere, document
count dropped exactly, no non-stripped GameObject left componentless, every remaining
component reference resolves. `Mine.prefab` untouched (no Crystal — the tool's own filter).
`BigCrystalVariant.prefab` needed nothing: it inherits the stripped base, avoiding the
dangling `m_RemovedComponents` override the editor-tool path risked. Known leftover: one
dormant `m_Enabled` override in `TadPoleFauna.prefab` targeting the removed component —
Unity ignores it; an editor run would have left it identically.

The tool is **RETIRED** (deleted in the same branch). Recover:
`git show 5ac234f8:Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs` *(any commit
before the deletion)*. Verify in editor: collect a crystal, explosion SFX still plays (it
comes from `AudioSystem.PlayGameplaySFX`, not the deleted AudioSources).

### D2 — Restore `Toy_Conveyor`'s `omniCrystalPrefab` — DISCHARGED

Done on the same branch: `Assets/_SO_Assets/Toys/Toy_Conveyor.asset` line 25 restored to
`{fileID: 5535990081244205891, guid: 54802b89e00a0ed4281025fa5e770811, type: 3}` — the
exact reference `a0b32006` wrote before `c8e0dae6` byte-reverted it, re-verified live
against `Crystal.prefab` (that fileID is the `Crystal` MonoBehaviour on its root). Verify
in editor: fly the Wanderway toy; ~16% of scenes should carry the big body-collected omni
crystal (`MicroscenePalette.OmniCrystalChance` = 0.16).

### D3 — Bake elemental petal bars into Sparrow — DISCHARGED BY SURGERY

Done on the same branch: 88 documents authored into `Sparrow.prefab` — 4 `*_Flower`
containers under the `ElementalBars` RectTransform at the wirer's default row
(x = −156/−52/52/156, y = 0) and 20 petals (RectTransform + CanvasRenderer + Image),
donor-cloned from `SquirrelHUDVariant.prefab`'s wirer-authored output. Verified: sprite
GUIDs match `Resources/ElementPetals/*.png.meta` per element; petal quaternions match
`ConfigurePetal`'s `Euler(0,0,−72p)`; rest color is `ColorForTick(0)` grey; the view's four
`bars[i].petalRoot` references wired; full bidirectional parent/child closure; the 4
pre-existing stripped-doc structures byte-identical to HEAD. The flowers sit at default
positions — **repositioning them per Sparrow's HUD layout is optional aesthetics**, in
Prefab Mode, whenever convenient.

### D4 — Commit the five missing `.meta` files — DISCHARGED

Done on the same branch: five `.meta` files minted (donor-cloned `MonoImporter` block,
fresh GUIDs, repo-wide collision sweep) for `PrismClockGraphWirer.cs`,
`PrismClockSmokeTest.cs`, `PrismClockWiringValidator.cs`, `PrismClock.cs`,
`PrismClockDiagnostics.cs`. If your working copy has local untracked `.meta` files for
these (your Unity imported them before this landed), **delete the local copies and take
the committed ones** — no committed asset can reference the locally-minted GUIDs.
Re-check the whole tree any time:

```bash
comm -23 <(git ls-files 'Assets/*.cs' | grep -viE 'Plugins/|PlayFabSDK/|NiceVibrations/|Wwise/|PrimitivePlus/|YethGameDev/|PlayFabEditorExtensions/' | sort) \
         <(git ls-files 'Assets/*.cs.meta' | sed 's/\.meta$//' | sort)
```

### D5 — Raycast Target Audit — prefab pass

The scene pass is done; do **not** re-run against `Menu_Main` expecting a large delta.

```
Unity ▸ select in the Project window:
          Assets/_Prefabs/CORE/GameCanvas.prefab
          Assets/_Prefabs/GameCanvas-HexRace.prefab
          Assets/_Prefabs/ArcadeGameConfigureModal.prefab
          Assets/_Prefabs/R_GameOverPanel.prefab
          Assets/_Prefabs/UI Elements          (folder)
          Assets/_Prefabs/Spacevessels         (folder)
     ▸ Tools > Cosmic Shore > UI > Raycast Target Audit
     ▸ "Disable candidates in N selected prefab(s)"

verify: grep -c 'm_RaycastTarget: 1' Assets/_Prefabs/CORE/GameCanvas.prefab   # 71 -> well below
```

**Prefab edits are not undo-able** — rely on git, and click through one race plus the
modals before committing to confirm nothing lost its input.

### D6 — Prism Grid Explosion Scene — decide first

Both outputs are absent, but whether that is an un-discharged obligation depends on
something only you can see: **does `BenchmarkResults/PrismExplosion/*.json` exist on your
machine with both a `legacy-cpu` and a `gpu-clock` run?**

- **If yes** — the benchmark was run, the scene was throwaway, and the obligation is only
  to record the numbers. Mark this row ✅ and move `PrismGridTestSceneSetupTool.cs` to
  standing (a scene builder you re-invoke when needed).
- **If no** — the A/B comparison behind PR #642's perf claims was never actually measured.
  Run `Tools > Cosmic Shore > Setup Prism Grid Explosion Scene`, follow the protocol in
  `Docs/PRISM_EXPLOSION_BENCHMARK.md`, and commit
  `Assets/Resources/PrismGridTestConfig.asset` +
  `Assets/_Scenes/Game_TestDesign/PrismGridExplosionTest.unity` + the report.

---

# Remaining human steps (2026-08-03)

Everything mechanically dischargeable has been discharged in-branch. What is left needs
the running editor, play-validation, or knowledge only the prompter has:

1. **Open Unity once, confirm a clean import** — zero console errors, no "Missing (Mono
   Script)" rows. This blesses all of the branch's surgery (10 stripped prefabs, Sparrow's
   88 new documents, `Toy_Conveyor`, the five `.meta` files). If local untracked `.meta`
   files conflict, keep the committed ones (D4 note).
2. **D5 — raycast prefab pass**: select `GameCanvas.prefab`, `GameCanvas-HexRace.prefab`,
   `ArcadeGameConfigureModal.prefab`, `R_GameOverPanel.prefab`, `_Prefabs/UI Elements`,
   `_Prefabs/Spacevessels` in the Project window ▸ `Tools > Cosmic Shore > UI > Raycast
   Target Audit` ▸ disable candidates ▸ click through one race + the modals ▸ commit.
3. **GameCanvas.prefab canvas upgrade** (unblocked by the nested-instance guard, fixed +
   corrected on this branch): open it in Prefab Stage ▸ `Tools > Cosmic Shore > Canvas
   Upgrader` ▸ Scan ▸ Dry Run — the report must say it SKIPS the internals of nested
   already-upgraded fragments (e.g. `EndGameStatsPanel`) while still scaling their root
   RectTransforms ▸ Upgrade ▸ save ▸ commit.
4. **D6 — decide**: does `BenchmarkResults/PrismExplosion/*.json` exist on your machine
   with both `legacy-cpu` and `gpu-clock` runs? Yes → tell the next session to mark D6
   done. No → PR #642's A/B numbers were never measured; run
   `Tools > Cosmic Shore > Setup Prism Grid Explosion Scene` + the protocol in
   `Docs/PRISM_EXPLOSION_BENCHMARK.md`.
5. **Lifeform crystal violations — design decision**: a composition-aware static run of
   the (now Boid-aware) validator finds 8 prefabs with no elemental crystal, direct or
   nested: `TermiteDrone.prefab`, `worm.prefab` (live spawn path), plus 6 under
   `Populations/` (the dead spawn path per `Docs/ECOSYSTEM.md` §7). Which element each
   creature drops is a design choice — pick, then any session can author the crystals
   (or run the editor validator to confirm after authoring). Until then the runtime
   `LifeFormCrystal` guard provisions at play time with a warning.
6. **Play-verify the restored/authored content**: Wanderway omni crystal appears
   (~16% of scenes); crystal collect still plays its SFX; Sparrow's HUD shows four grey
   flowers (reposition to taste); toast assets still resolve.

---

## Audit method + caveats (2026-08-02)

Seeded by an audit of every `[MenuItem]` in `Assets/` against the artifacts it claims to
produce, then adversarially verified. Method: read the tool, enumerate its declared writes,
check those paths on disk, grep the target assets for the tool's fingerprint (script GUIDs,
serialized field names), and compare docs' claims to reality.

**The clone is shallow** (674 commits, back to 2026-06-12), so `git log` archaeology is
unavailable for 23 of 34 tools. **Artifact existence on disk is the primary evidence**, not
commit history — and the health metric is *"one-shot asset-authoring tools whose declared
output paths do not exist"*, never the tool-only-commit rate (which is dominated by
legitimate reporters).
