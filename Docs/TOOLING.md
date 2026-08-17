# Editor tooling — one menu, one board

Every first-party editor tool in Cosmic Shore lives under a **single menu root: `FrogletTools/`**,
and every one of them shows up automatically in **FrogletTools ▸ Froglet Master Tool**.

There is no registration list, no manifest asset, and no step you can forget.

---

## The convention

> **A tool appears in the Froglet Master Tool the moment its `[MenuItem]` path starts with
> `FrogletTools/` and it compiles. That is the entire contract.**

```csharp
using CosmicShore.Editor.Froglet;
using UnityEditor;

public static class MyTool
{
    [MenuItem("FrogletTools/Validation/Check Widget Integrity")]
    static void Run() { /* ... */ }
}
```

That is enough. `FrogletToolRegistry` reflects over `[MenuItem]` attributes in the project's
assemblies at window-open time, infers a category from the path and type name, and draws it.

### Opt into better placement

Add `[FrogletTool]` on the same method when you want to control the lane, the ranking or the
blurb:

```csharp
[MenuItem("FrogletTools/Game Modes/Game Mode Prefab Kit", false, 10)]
[FrogletTool(FrogletToolCategory.GameModes, Importance = 5,
    Description = "Add and validate every prefab a game-mode scene needs.")]
public static void Open() { ... }
```

| Field | Meaning |
|---|---|
| `Category` | Section + accent colour. Ten sections, see `FrogletToolCategory`. |
| `Importance` | **1** (niche) … **5** (used every day). Drives sort order within a section and the five-dot rating on the card. Default 3. |
| `Description` | One line rendered on the card body. |
| `DisplayName` | Overrides the menu leaf as the card title. |

**Where the attribute can go:** `FrogletToolAttribute` compiles into the *editor* assembly, so it
can only be used from a file under an `Editor/` folder. A tool that lives in the runtime assembly
behind `#if UNITY_EDITOR` (e.g. `LogControlWindow`) still appears on the board — it just falls back
to the inferred category and importance 3.

---

## The board

**FrogletTools ▸ Froglet Master Tool** renders the registry as a card grid:

- One **collapsible, colour-coded section per category**, with a count pill. Expand all /
  Collapse all live in the toolbar.
- One **card per tool** — title, what it does, and a five-dot importance rating in the section's
  accent colour. Click a card to launch it. Cards flow into as many columns as the window is wide
  enough for, most important first.
- **Right-click a card** for Launch / Copy menu path / Ping script / Open script.
- Search filters across name, menu path, description and category.

Launching routes through `EditorApplication.ExecuteMenuItem`, so validate functions and editor
context behave exactly as a manual menu click would, with a direct reflection call as fallback.

The `FrogletTools/` prefix is also the registry's only filter, so third-party menus (PlayFab,
FMOD, Soap, Quick Scene Pro, …) are never picked up — the board shows only tools we own.

### Sections

| Category | Section | Colour |
|---|---|---|
| `Build` | Build & Release | coral |
| `SceneSetup` | Scene Setup | azure |
| `GameModes` | Game Modes | jade |
| `Vessels` | Vessels & HUD | violet |
| `Ecology` | Prisms & Ecology | lime |
| `Performance` | Performance | gold |
| `Validation` | Validation | ruby |
| `Interface` | Interface | cyan |
| `Services` | Services & Data | magenta |
| `Misc` | Misc | slate |

---

## Shared look

All Froglet windows draw through **`FrogletEditorPalette`** so the toolchain reads as one product:
accents, semantic colours (`Ok` / `Warn` / `Error` / `Info` / `Muted`), `Banner`, `ColorButton`,
`StatusPill`, `DrawCard`, `DrawAccentStripe`. Colours adapt to the light Editor skin automatically.

**Do not hand-roll `GUI.color` juggling in a new window** — add what you need to the palette so
every window gets it.

---

## Tool output is a deliverable

> **A tool that writes assets must record what it wrote and draw
> `FrogletToolShipPanel`. Its OUTPUT is the deliverable; the tool is scaffolding.**

A wirer, a setup tool, a migration — its real product is the rewritten scene, the re-authored
prefab, the generated SO. That product lands in the **working tree**, and the branch only carries
what someone chose to commit. Committing the tool and forgetting its output ships code that
expects data nobody pushed: broken on every other machine, with nothing in the diff to explain it.

The failure is likely rather than rare for two structural reasons. Whoever wrote the tool
**cannot see its output** — it runs in someone else's editor, minutes or days later. And the
output looks like noise: a regenerated `.unity` is thousands of YAML lines nobody reads, so
`git status` showing it dirty is easy to scroll past. So the panel makes committing it a button
at the point of the mistake, instead of a thing to remember later.

### The contract

```csharp
using CosmicShore.Editor.Froglet;

const string ToolName = "Wire Foo Widgets";     // ledger key + commit subject; keep it stable

static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
{
    ToolScriptPaths = new[] { "Assets/_Scripts/Editor/FrogletTools/MyWirer.cs" },
    Validate = () => ValidateWiring(),          // the tool's own correctness check
};

void Wire()
{
    // ... write assets ...
    FrogletToolChangeLedger.Record(ToolName, assetPath);      // record AS YOU WRITE
    FrogletToolChangeLedger.RecordOpenScenes(ToolName);       // if you edited scene contents
}

void OnGUI()
{
    // ... the tool's own UI ...
    FrogletToolShipPanel.Draw(Ship, this);                    // Validate & Push + Retire Tool
}
```

| Button | What it does |
|---|---|
| **Validate & Push** | Saves assets + open scenes, runs the built-in checks and the tool's own `Validate`, stages **only** this tool's recorded paths, commits, pushes to the current branch. Everything else dirty is listed and left alone. |
| **Retire Tool** | Deletes the tool's own scripts and scratch assets and commits the removal. Refuses while the tool still has unpushed output. |

Built-in checks (they apply to any tool, whatever it does): scripts compile; no scene has unsaved
changes; every staged asset has its `.meta`; no orphan `.meta` (one whose asset is gone). A missing
`.meta` is the classic half-committed output — the asset lands with a fresh GUID on the next
machine and every reference to it breaks.

### Rules

- **Record as you write, not at the end.** A path recorded in the same block that wrote it cannot
  be missed by an early return or an exception.
- **Never stage by wildcard, and scope the commit too.** `FrogletGit` has no `-A` path by design:
  a tool commits its own output or nothing. Someone else's half-finished edit sitting next to it
  in the tree is not yours to sweep up. The commit carries the same pathspec — a bare
  `git commit` records the WHOLE index, so anything the human had already staged of their own
  would ride along and undo, at the last step, the care `add` took all the way up to it.
- **Output first, retirement second.** Deleting a tool while its output is uncommitted strands the
  output with nothing left that could reproduce it. The panel enforces the order.
- **Retire the one-offs; keep the re-runnables.** A tool written to perform ONE migration is
  scaffolding — delete it once its output is verified (`chore(tools): retire <name> after
  verification`). Keep it only if it is idempotent and someone will need it again (auditors,
  validators, re-wirers) — and then add it to the tool index below.
- **No push to a protected branch.** `main`, `master`, `bleeding-edge`, `develop`, `release/*` are
  refused by both buttons.
- **A READER tool needs none of this.** An auditor that only logs writes nothing, records nothing,
  and draws no panel. Say so in its doc comment so nobody looks for missing output.

### The fallback: Pending Tool Changes

**FrogletTools ▸ Build ▸ Pending Tool Changes** is the last gate before a branch ships. It lists
what each tool recorded *and* every other dirty file under `Assets/`, `Packages/` and
`ProjectSettings/` — because a tool can only record what it was written to record, and the gap is
exactly where things hide. Each group validates, commits and pushes on its own; a one-off can be
retired from here too.

The ledger lives at `Library/FrogletToolChangeLedger.json` — machine-local and gitignored, which is
correct: it describes what THIS editor wrote and has not committed. It survives editor restarts,
which is the window in which the forgetting happens.

The agent-side counterpart is the `/ship-tools` skill (and `/ship` §2.5, which no ship mode may
skip).

---

## The migration that happened

Menu items previously lived under three roots. All first-party items were moved under
`FrogletTools/`; the `Tools/Cosmic Shore/…` and `Cosmic Shore/…` roots no longer exist.

| Was | Now |
|---|---|
| `Tools/Cosmic Shore/Build/…` | `FrogletTools/Build/…` |
| `Tools/Cosmic Shore/End Game Conditions` | `FrogletTools/Game Modes/End Game Conditions` |
| `Tools/Cosmic Shore/Prism Animation/…` | `FrogletTools/Ecology/Prism Animation/…` |
| `Tools/Cosmic Shore/Measure Cell Environment Baselines` | `FrogletTools/Ecology/…` |
| `Tools/Cosmic Shore/Strip Crystal AudioSources` | `FrogletTools/Ecology/…` |
| `Tools/Cosmic Shore/Validate Lifeform Crystals` | `FrogletTools/Validation/…` |
| `Tools/Cosmic Shore/Audit Vessel *`, `Wire/Bake Elemental Petal Bars`, `Plan Vessel Rig Swap` | `FrogletTools/Vessels/…` |
| `Tools/Cosmic Shore/Setup Freestyle Toybox`, `Setup Prism Grid Explosion Scene` | `FrogletTools/Scene Setup/…` |
| `Tools/Cosmic Shore/Prism Grid Benchmark/…` | `FrogletTools/Performance/Prism Grid Benchmark/…` |
| `Tools/Cosmic Shore/Canvas Upgrader`, `UI/Raycast Target Audit` | `FrogletTools/Interface/…` |
| `Tools/{Texture Memory Usage, Runtime Texture Memory Usage, Scene Object Counter}` | `FrogletTools/Performance/…` |
| `Tools/Triangle Window Mesh Generator` | `FrogletTools/Misc/…` |
| `Cosmic Shore/Toast Notification/…` | `FrogletTools/Interface/Toast Notification/…` |
| `Window/Animation Recorder` | `FrogletTools/Misc/Animation Recorder` |

Third-party menus (PlayFab, FMOD, Obvious Soap, Primitive Plus, **Quick Scene Pro**) were left
exactly where they were — they are not ours to move, and the registry never picks them up because
their paths do not start with `FrogletTools/`.

**Doc references to the old paths are stale.** When you touch a doc that says
"Tools > Cosmic Shore > X", update it to the `FrogletTools/` path.

---

## Tool index

| Lane | Tool | What it is for |
|---|---|---|
| Build | **Pending Tool Changes** | Uncommitted asset output from editor tools. Validate, push, retire. The last gate before a branch ships. |
| Game Modes | **Game Mode Prefab Kit** | The prefabs a new game-mode scene needs; Add to Scene / Open Prefab / Validate, plus cross-scene drift detection and consolidation. See `Docs/GAMECANVAS.md`. |
| Game Modes | End Game Conditions | The one place win conditions are authored for the domain modes. |
| Build | Windows x64 (Release / Development), Reveal Build Folder | Player builds. |
| Ecology | Prism Animation ▸ Validate Clock Wiring / Auto-Wire Clock Properties | The clock-material law gate. |
| Ecology | Prism Animation ▸ **Occlusion Dither Lab** | The occlusion corridor's unit shape, live — kernel + scale dials driven as shader globals **while the game runs**, a preview that IS the shipped GPU code, a Measure button that runs the corridor's own |coverage − alpha| admission rule against the shipped baseline, and Bake to write the result back into `PrismOcclusionCorridor.hlsl`. Keeper (re-runnable), but it writes source, so it draws the ship panel. See `Docs/PRISM_ANIMATION.md` §4.7. |
| Ecology | Measure Cell Environment Baselines | Per-cell prism baselines the phase thresholds ride on. |
| Validation | Validate Lifeform Crystals | Every lifeform drops exactly one elemental crystal. |
| Vessels | Audit Vessel Ability Rows / Elemental Morphs, Wire & Bake Petal Bars, Plan Rig Swap | Vessel HUD + model wiring. |
| Performance | Performance Benchmark, Prism Grid Benchmark, Texture Memory, Scene Object Counter | Frame cost and memory. |
| Scene Setup | Setup Freestyle Toybox, Setup Prism Grid Explosion Scene | Scene scaffolding. |
| Interface | Canvas Upgrader, Raycast Target Audit, Toast Notification setup | UI authoring. |
| Misc | Toolbox ▸ Logging | Log levels, **diagnostic channels**, and console stack-trace depth. Channels (`CSLogChannel`) carry a finished system's BRING-UP telemetry — `[FLOW-n]` spawn/session flow, `[GyroidColony]` lattice — and default to OFF, so the trace stays in the tree as knowledge without spamming the console; turn one on before investigating that system. Warnings and errors never sit on a channel. Reader only — writes `EditorPrefs`, never assets, so no ship panel. |
| Misc | Toolbox | Scene shortcuts, runtime switches, quest/crystal/UGS debug tabs. |

---

## File index

| Role | Path |
|---|---|
| Master board | `Assets/_Scripts/Editor/FrogletTools/FrogletMasterToolWindow.cs` |
| Auto-discovery | `Assets/_Scripts/Editor/FrogletTools/FrogletToolRegistry.cs` |
| Metadata attribute | `Assets/_Scripts/Editor/FrogletTools/FrogletToolAttribute.cs` |
| Shared palette / widgets | `Assets/_Scripts/Editor/FrogletTools/FrogletEditorPalette.cs` |
| Ship panel (Validate & Push / Retire Tool) | `Assets/_Scripts/Editor/FrogletTools/FrogletToolShipPanel.cs` |
| Tool-output ledger | `Assets/_Scripts/Editor/FrogletTools/FrogletToolChangeLedger.cs` |
| Pending Tool Changes window | `Assets/_Scripts/Editor/FrogletTools/FrogletToolShipWindow.cs` |
| git CLI wrapper (quoting-safe, no wildcards) | `Assets/_Scripts/Editor/FrogletTools/FrogletGit.cs` |
| Prefab kit window | `Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs` |
| Prefab kit validation | `Assets/_Scripts/Editor/FrogletTools/KitValidator.cs` |
| Scene drift scanner (read-only) | `Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs` |
| Drift fixer (writes via PrefabUtility) | `Assets/_Scripts/Editor/FrogletTools/PrefabDriftFixer.cs` |
| Kit config SO | `Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs` |
