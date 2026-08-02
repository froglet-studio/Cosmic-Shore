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

## Status legend

| | meaning |
|---|---|
| ♾️ STANDING | Re-runnable utility. Never "done". No discharge owed. |
| ⏳ PENDING | One-shot, **not yet run** (or run but output not committed). Owes a run + push. |
| ✅ RUN | One-shot, output committed. Now owes **retirement** (delete + doc rewrite). |
| 🗑️ RETIRED | Deleted from the tree. Row kept as the recovery pointer. |
| ❓ UNCLEAR | Needs investigation before it can be classified. |

## Ledger

Seeded 2026-08-02 by an audit of every `[MenuItem]` in the tree against the artifacts it
claims to produce. "Evidence" is what was checked, not what was assumed.

### ⏳ Owed a run (one-shot, undischarged)

| Tool | Menu | Evidence it never ran | Discharge |
|---|---|---|---|
| `StripCrystalAudioSourceTool.cs` | `Tools > Cosmic Shore > Strip Crystal AudioSources` | 9 prefabs under `_Prefabs/Environment` still carry a real `AudioSource` (`!u!82`) **and** the `Crystal` script: ActiveCrystalMass, ActiveCrystalSpace, Crystal, CrystalCharge, CrystalMass, CrystalSpace, CrystalTime, MazeCrystal, OldCrystalTime. The tool's own header says *"One-shot… Run this tool once after pulling this change."* | [D1](#d1--strip-crystal-audiosources) |
| `CanvasUpgraderWindow.cs` | `Tools > Cosmic Shore > Canvas Upgrader` | Migration is **partial**: 12 canvases at 1920×1080, **3 still at 800×450** — `_Scenes/Singleplayer Scenes/SplashScreen.unity`, `_Prefabs/CORE/GameCanvas.prefab`, `_Prefabs/GameCanvas-HexRace.prefab`. PR #598 landed the tool + 15 prefabs + 5 scenes, so the run happened but stopped short. | [D2](#d2--canvas-upgrader-stragglers) |
| `ElementalPetalBarWirer.cs` | `Tools > Cosmic Shore > Bake Elemental Petal Bars Into All Vessel HUDs` | `ElementalBarsController` is on **all 11** vessel prefabs, but `ElementalBarsView` (guid `5f380cf9…`) appears in only **3** assets: `Squirrel.prefab`, `Sparrow.prefab`, `SquirrelHUDVariant.prefab`. The other 9 vessels have the driver with nothing to drive. Config + sprites exist (`Resources/ElementalBarsConfig.asset`, `Resources/ElementPetals/{charge,mass,space,time}_petal.png`). | [D3](#d3--bake-elemental-petal-bars) |

### ♾️ Standing (no discharge owed)

| Tool | Menu | Note |
|---|---|---|
| `PrismClockWiringValidator.cs` | `… > Prism Animation > Validate Clock Wiring` | Run after any graph edit. Wiring itself **is** landed — all three graphs carry the clock properties + Custom Function nodes. |
| `PrismClockGraphWirer.cs` | `… > Prism Animation > Auto-Wire Clock Properties (All Graphs)` | Idempotent **repair** tool (writes graph JSON via `File.WriteAllText`). Normally reports "already present". Keep as the recovery path named in the troubleshooting table. |
| `PrismClockSmokeTest.cs` | `… > Prism Animation > Smoke Test - Re-Bloom Nearby Prisms` | Play-mode diagnostic. |
| `CellEnvironmentBaselineMeasurer.cs` | `… > Measure Cell Environment Baselines` | Re-run whenever an environment generator changes. **Output was transcribed** — the six freestyle cells carry distinct measured thresholds (Caldera 441891 · Daedala 648504 · Geode 574650 · Orrery 206986 · Yggdra 552356 · Zephyr 437285). |
| `EndConditionOverridesWindow.cs` | `… > End Game Conditions` | The only authoring path for end-game counts. `Resources/EndConditionOverrides.asset` exists and carries authored values. |
| `VesselAbilityRowAuditor.cs` | `… > Audit Vessel Ability Rows` | Read-only fleet compliance report. |
| `VesselElementalMorphAuditor.cs` | `… > Audit Vessel Elemental Morphs` | Read-only, asset-only. |
| `VesselRigSwapPlanner.cs` | `… > Plan Vessel Rig Swap` | Report-only by design ("never writes"). The Dolphin/Urchin/Rhino rig swap it plans is a **human editor pass**, tracked in CLAUDE.md — not a tool discharge. |
| `LifeFormCrystalValidator.cs` | `… > Validate Lifeform Crystals` | Enforces the every-lifeform-drops-a-crystal invariant. |
| `PrismGridTestSceneSetupTool.cs` | `… > Setup Prism Grid Explosion Scene` | Builds a throwaway benchmark scene on demand; `_Scenes/Game_TestDesign/PrismInstancingStressTest.unity` exists. |
| `PrismExplosionBenchmarkReport.cs` | `… > Prism Grid Benchmark > …` | Report generator over benchmark runs. |
| `PerformanceBenchmarkWindow.cs` | `FrogletTools > Performance Benchmark` | Standing instrumentation (`BENCHMARK_TOOL.md`). |
| `ToyboxSetupTool.cs` | `… > Setup Freestyle Toybox` | Re-runnable authoring/repair. **Output landed**: `Resources/Toybox.asset`, `_SO_Assets/Toys/Toy_{CellSelector,Conveyor,DomainChanger,LifeformMatrix,Painting,VesselChanger}.asset`, and `ToyboxController` is present in `Menu_Main.unity`. |
| `CosmicShoreBuildPipeline.cs` | `… > Build > …` | Build entry points (`Docs/BUILD_AND_DELIVERY.md`). |
| `RaycastTargetAuditTool.cs` | `… > UI > Raycast Target Audit` | Audit + opt-in fix, meant to be re-run as UI grows. |
| `ForceReserializeScriptableObjects.cs` | `FrogletTools > Legacy > Force Re-Serialize …` | Maintenance utility for serialization drift. |

<!-- ROWS -->

## Discharge blocks

Full copy-pasteable instructions for every `⏳ PENDING` row above. Run them in any order —
they touch disjoint assets. After each, confirm the expected paths appear in `git status`
before committing; **an empty diff means the tool did not do what this block claims**, which
is a bug to report rather than a run to skip.

### D1 — Strip Crystal AudioSources

```
Unity ▸ Tools > Cosmic Shore > Strip Crystal AudioSources

expect console: "[StripCrystalAudioSourceTool] Stripped AudioSource from 9 prefab(s):"
                (a "nothing to remove" line means it already ran — tell the session)

writes: Assets/_Prefabs/Environment/{ActiveCrystalMass,ActiveCrystalSpace,Crystal,
        CrystalCharge,CrystalMass,CrystalSpace,CrystalTime,MazeCrystal,OldCrystalTime}.prefab
```

```bash
git add "Assets/_Prefabs/Environment/*.prefab"
git commit -m "chore(assets): strip dead AudioSources from crystal prefabs"
git push -u origin <branch>
```

Then retire: delete `Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs` (+ `.meta`) and
move its ledger row to 🗑️ RETIRED with the SHA.

### D2 — Canvas Upgrader (stragglers)

```
Unity ▸ Tools > Cosmic Shore > Canvas Upgrader
        → target the 3 remaining 800x450 canvases:
          Assets/_Scenes/Singleplayer Scenes/SplashScreen.unity
          Assets/_Prefabs/CORE/GameCanvas.prefab
          Assets/_Prefabs/GameCanvas-HexRace.prefab

expect: each ends at m_ReferenceResolution {x: 1920, y: 1080}
verify: grep -rl "m_ReferenceResolution: {x: 800, y: 450}" Assets   → no output
```

```bash
git add "Assets/_Scenes/Singleplayer Scenes/SplashScreen.unity" \
        "Assets/_Prefabs/CORE/GameCanvas.prefab" \
        "Assets/_Prefabs/GameCanvas-HexRace.prefab"
git commit -m "chore(assets): finish 800x450 -> 1920x1080 canvas migration"
git push -u origin <branch>
```

**Check before running**: `GameCanvas.prefab` is the full game-UI root. If it is
deliberately held at 800×450, say so here and mark the row ✅ with that note instead of
migrating it. Once all three are done the tool becomes retirable.

### D3 — Bake Elemental Petal Bars

```
Unity ▸ Tools > Cosmic Shore > Bake Elemental Petal Bars Into All Vessel HUDs

expect: an ElementalBarsView + *_Flower containers on each vessel HUD, and each vessel's
        ElementalBarsController.elementBars no longer None
writes: Assets/_Prefabs/Spacevessels/*.prefab and/or _Prefabs/UI Elements/VesselHUD/*.prefab
verify: the view guid appears in more than the current 3 assets —
        grep -rl 5f380cf9710e6ec478121956993a387e Assets --include=*.prefab | wc -l
```

```bash
git add "Assets/_Prefabs/Spacevessels" "Assets/_Prefabs/UI Elements/VesselHUD"
git commit -m "chore(assets): bake elemental petal bars into the remaining vessel HUDs"
git push -u origin <branch>
```

**This one is a judgement call, not a defect.** CLAUDE.md describes the view as an
intentional *opt-in rollout* and the runtime is null-safe (no view = no bars, no error), so
the 9 unwired vessels are not broken. But a menu item named "…Into **All** Vessel HUDs"
exists precisely to finish this, and the flowers need hand-positioning per HUD afterwards.
Decide: finish the rollout, or narrow the tool's name/scope to match the real intent. Either
way the row stops being ⏳.

<!-- BLOCKS -->
