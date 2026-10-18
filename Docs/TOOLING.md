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
    [MenuItem("FrogletTools/Utilities/Check Widget Integrity")]
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
| `Category` | Section + accent colour. Fourteen menu groups, see `FrogletToolCategory` and the section table below. |
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
- A documented tool's card carries a **DOCS chip** (bottom-right) that opens its documentation on
  GitHub — see `DocPath` in the authoring contract below.
- **Right-click a card** for Launch / Open documentation / Copy menu path / Ping script / Open script.
- Search filters across name, menu path, description and category.

Launching routes through `EditorApplication.ExecuteMenuItem`, so validate functions and editor
context behave exactly as a manual menu click would, with a direct reflection call as fallback.

The `FrogletTools/` prefix is also the registry's only filter, so third-party menus (FMOD,
Soap, Quick Scene Pro, …) are never picked up — the board shows only tools we own.

### Sections

The menu and the board use the same sections. The menu root carries only the two hubs
(**Froglet Master Tool**, **Toolbox**), then one group per section in alphabetical order; a group
is at most two levels deep (`Vessels/Wiring/…`, `Performance/Benchmarks/…`); the `Legacy` group
and every loose leaf at the root are gone (reorganised 2026-10-14).

| Category | Menu group | Board section | Colour | Priority band |
|---|---|---|---|---|
| `Ai` | `AI/` | AI | ruby | 100 |
| `Build` | `Amoebius/`, `Build/` | Build & Release | coral | 110, 120 |
| `Diagnostics` | `Diagnostics/` | Diagnostics & Health | indigo | 130 |
| `Ecology` | `Ecology/` (+ `Prism Animation/`) | Prisms & Ecology | lime | 140 |
| `Ftue` | `FTUE/` | FTUE | violet | 150 |
| `GameModes` | `Game Modes/` | Game Modes | jade | 160 |
| `Performance` | `Performance/` (+ `Benchmarks/`) | Performance | gold | 170 |
| `Qa` | `QA/` | QA | cyan | 180 |
| `SceneSetup` | `Scenes/` | Scenes | azure | 190 |
| `Services` | `Services/` | Services & Data | magenta | 200 |
| `Interface` | `UI/` | UI | cyan | 210 |
| `Utilities` | `Utilities/` | Utilities | slate | 220 |
| `Vessels` | `Vessels/` (+ `Wiring/`, `Audits/`) | Vessels & HUD | violet | 230 |

**Priorities are the ordering contract.** Each group owns a base (the band above); items within a
group count up from it, and a jump of 20 (`base + 20`, `base + 40`) draws a separator inside that
submenu (Unity separates neighbours more than 10 apart). The hubs sit at `-100` / `-99` so the
root reads hubs, separator, groups. Keep a new tool inside its group's band; never leave the
priority at the default.

---

## Shared look

All Froglet windows draw through **`FrogletEditorPalette`** so the toolchain reads as one product:
accents, semantic colours (`Ok` / `Warn` / `Error` / `Info` / `Muted`), `Banner`, `ColorButton`,
`StatusPill`, `DrawCard`, `DrawAccentStripe`. Colours adapt to the light Editor skin automatically.

**Do not hand-roll `GUI.color` juggling in a new window** — add what you need to the palette so
every window gets it.

---

## The authoring contract (STRICT — every new tool, human- or AI-written)

The conventions above describe what exists. This section is the **normative checklist a new tool
must satisfy before it ships** — write against it, and review against it. An AI-authored tool has
no excuse for missing any line of it, because the whole list is mechanical.

### 1. Placement & naming

- **File location**: `Assets/_Scripts/Editor/` for standalone tools, or a subfolder per tool
  family (`Editor/FrogletTools/` is the board's own infrastructure, `Editor/Build/` the build
  pipeline, `Editor/Diagnostics/` the diagnostics family — crash detector + bug ledger). Never a runtime folder + `#if UNITY_EDITOR`
  unless the tool must share a predicate with runtime code — and then read
  `Docs/CONDITIONAL_COMPILATION.md` first and run `python3 Tools/Build/check_conditional_compilation.py`.
- **Namespace**: `CosmicShore.Editor` (the board's own infrastructure uses
  `CosmicShore.Editor.Froglet`; tools consume it via `using CosmicShore.Editor.Froglet;`).
- **Class naming**: `<Thing>Window` for an `EditorWindow`, `<Thing>Auditor` / `<Thing>Validator`
  for readers, `<Thing>Wirer` / `<Thing>SetupTool` for writers. One concern per file.

### 2. Menu & metadata — the attribute is mandatory for new tools

For legacy tools the `[FrogletTool]` attribute is optional (the registry infers). **A new tool
always carries it**, on the same static method as the `[MenuItem]`:

```csharp
[MenuItem("FrogletTools/<Section>/<Tool Name>", false, <priority>)]
[FrogletTool(FrogletToolCategory.<Category>, Importance = <1..5>,
    Description = "<one honest line — what it does, not what it is called>",
    DocPath = "Docs/<ItsDoc>.md#<anchor>")]
public static void Open() { ... }
```

- The menu path's middle segment IS the section: the registry files an un-annotated tool by
  that segment first (`SegmentCategories`), keywords second.
- **Importance is calibrated, not aspirational**: 5 = used every day / a ship gate; 4 = reached
  for weekly or the standing auditor for a law; 3 = the neutral default; 2 = occasional; 1 = niche.
- **Pick from the existing categories.** Extending `FrogletToolCategory` is a curation act —
  propose it, get sign-off, and update the palette map, `LabelFor`, and this doc's section table
  in the same change. Never invent a section by menu path alone.
- **`DocPath` links the tool to its documentation** — a repo-relative path (+ optional `#anchor`),
  never a URL. The card on the board grows a **DOCS chip** and an *Open documentation* context
  entry that open the page on GitHub (`FrogletDocLinks` builds the link from the checkout's own
  origin remote on the `bleeding-edge` branch, falling back to the local file). A documented tool
  declares it; a tool with no doc yet omits it — the chip only appears when it is real.

### 3. Colour & format rules (the "one product" look)

- **Every colour comes from `FrogletEditorPalette`.** No literal `Color(...)`, no hex, no
  `GUI.color` juggling in tool code. A widget the palette lacks gets **added to the palette**, not
  hand-rolled in the window.
- **The banner accent is the tool's category colour** — `FrogletEditorPalette.ColorFor(<category>)`
  — so a window and its card on the board agree. Semantic colours (`Ok` / `Warn` / `Error` /
  `Info` / `Muted`) are reserved for **state**, never for decoration: a green button means the
  action is safe/ready, a ruby pill means something is wrong.
- **Standard widgets for standard jobs**: `Banner` for the header (title + one-sentence subtitle),
  `StatusPill` for state, `ColorButton` for actions, `DrawCard` + `DrawAccentStripe` for rows,
  `HorizontalRule` between sections. Text styles come from the palette (`Title`, `Subtitle`,
  `SectionHeader`, `CardTitle`, `CardBody`, `Pill`), never ad-hoc `GUIStyle`s with hardcoded colours.

### 4. Window anatomy

Top to bottom, every Froglet window reads the same way:

1. `FrogletEditorPalette.Banner(title, one-sentence purpose, ColorFor(category))`
2. A toolbar / status row (refresh, filters, the headline pills)
3. The scrolled body — cards or accent-striped rows
4. Writer tools only: `FrogletToolShipPanel.Draw(Ship, this)` **last**

Behavioural rules that go with it:

- **Heavy work happens on demand, never per repaint.** One `git status` / asset scan / file stat
  per explicit Refresh, cached into fields; `OnGUI` only draws. (See `FrogletToolShipWindow.Refresh`.)
- **Mutating state from a button runs deferred**, after the GUI pass — mid-layout mutation throws
  (`FrogletToolShipWindow._deferred` is the reference shape).
- A destructive button (delete, overwrite, retire) confirms via `EditorUtility.DisplayDialog`.

### 5. Config lives in a ScriptableObject, never in the window

- **Project-shared tool config** (lists of prefabs, per-mode values): a `ScriptableObject` asset,
  wired like gameplay config (`GameModePrefabKitSO` is the reference).
- **Machine-local tool state** (toggles, intervals, last-used paths): a
  `ScriptableSingleton<T>` under `UserSettings/` (gitignored, survives restarts, still a real SO
  with `[SerializeField]` + tooltips), or `EditorPrefs` for single scalars (the Logging window's
  precedent). Never a hard-coded list or a magic constant in the window class.

### 6. Reader or writer — declare which, and honour it

- A **READER** (audits, reports, log viewers) writes no assets, records nothing in the ledger, and
  draws no ship panel — **and says so in its class doc comment**, so nobody hunts for missing
  output. Writing to gitignored locations (`Logs/`, `UserSettings/`, `Library/`) keeps a tool a
  reader.
- A **WRITER** (wirers, setup, migrations, generators) follows the ship contract below in full:
  ledger-record as it writes, ship panel in `OnGUI`, retire-when-one-off.
- Read-only asset loading uses `AssetDatabase.LoadAssetAtPath`, never `PrefabUtility.LoadPrefabContents`
  (which opens a preview scene per prefab and dies on the malformed data an auditor exists to find).

### 7. Console discipline

- A tool's console output is **headline-only**: one summary line per run, warnings/errors only for
  real faults. Detailed output goes to a report the tool opens or pings (a file under `Logs/`, a
  window section) — never 60 lines of `Debug.Log`.
- Bring-up traces belong on a `CSLogChannel` (off by default), per CLAUDE.md's logging rules.
  Nothing per-frame or per-contact logs, ever.

### 8. Ship the paperwork with the tool

A new keeper tool is not done until the **Tool index** and **File index** tables in this doc carry
its row — and, when it has real documentation, until its `DocPath` points there, so the board's
DOCS chip works. A one-off migration tool instead states its one-off nature in its doc comment, so
its retirement (via the ship panel) surprises nobody.

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
| `Tools/Cosmic Shore/Strip Crystal AudioSources` | retired 2026-10-14; its job (never run until then) was done by hand on 2026-10-15: the 9 crystal prefabs' dead `AudioSource`s and `TadPoleFauna`'s override on one were removed |
| `Tools/Cosmic Shore/Validate Lifeform Crystals` | `FrogletTools/Ecology/…` |
| `Tools/Cosmic Shore/Audit Vessel *`, `Wire/Bake Elemental Petal Bars`, `Plan Vessel Rig Swap` | `FrogletTools/Vessels/Audits/…`, `FrogletTools/Vessels/Wiring/…` |
| `Tools/Cosmic Shore/Setup Freestyle Toybox`, `Setup Prism Grid Explosion Scene` | `FrogletTools/Scenes/…` |
| `Tools/Cosmic Shore/Prism Grid Benchmark/…` | `FrogletTools/Performance/Benchmarks/Prism Grid/…` |
| `Tools/Cosmic Shore/Canvas Upgrader`, `UI/Raycast Target Audit` | `FrogletTools/UI/…` |
| `Tools/{Texture Memory Usage, Runtime Texture Memory Usage, Scene Object Counter}` | `FrogletTools/Performance/…` |
| `Tools/Triangle Window Mesh Generator` | retired 2026-10-14 |
| `Cosmic Shore/Toast Notification/…` | retired 2026-10-14 (the toast assets shipped) |
| `Window/Animation Recorder` | retired 2026-10-14 |

### The 2026-10-14 reorganisation

The `Legacy` group and the loose root leaves were removed and every remaining item was filed by
section (table above). Retired outright, with the owner's sign-off, because their output had
shipped or nothing had used them since they were written: the six Legacy duplicates of Toolbox
buttons (Logging ×3, scene shortcuts ×3), `Create/Class`, `Create/MiniGame`, `Component Copier`,
`ElementalFloat Editor`, `Triangle Window Mesh Generator`, `Animation Recorder`, the AOE benchmark
pair, the three Quest Graph one-shots (Create Main Quest, Setup Runner, Wire Phase 0 UI), the five
Toast Notification scaffold items, `Create Butterfly Vessel`, and `Strip Crystal AudioSources`.

| Was | Now |
|---|---|
| `FrogletTools/AI Training`, `…/AI Training/Quick Setup` | `FrogletTools/AI/AI Training`, `…/AI/AI Training Quick Setup` |
| `FrogletTools/Performance Benchmark` | `FrogletTools/Performance/Performance Benchmark` |
| `FrogletTools/Quest Graph Editor`, `Quest Graph/Layout All Phases (Rows)`, `Legacy/Dialogue Editor` | `FrogletTools/FTUE/…` |
| `FrogletTools/Legacy/Find Asset by GUID`, `Legacy/Force Re-Serialize All ScriptableObjects`, `Validation/Audit Stray Network Objects` | `FrogletTools/Utilities/…` |
| `FrogletTools/Validation/Validate Lifeform Crystals` | `FrogletTools/Ecology/…` |
| `FrogletTools/Interface/…`, `Diagnostics/Report On-Screen UI` | `FrogletTools/UI/…` |
| `FrogletTools/Diagnostics/Prism Contact Probe` | `FrogletTools/Performance/…` |
| `FrogletTools/Performance/Crystal Depth Prism Benchmark`, `Crystal Flip Wave Benchmark`, `Prism Grid Benchmark/…` | `FrogletTools/Performance/Benchmarks/…` |
| `FrogletTools/Scene Setup/…` | `FrogletTools/Scenes/…` |
| `FrogletTools/Vessels/Wire *`, `Bake *`, `Plan/Swap Vessel Rig` | `FrogletTools/Vessels/Wiring/…` |
| `FrogletTools/Vessels/Audit *`, `Validate *` | `FrogletTools/Vessels/Audits/…` |

Third-party menus (FMOD, Obvious Soap, Primitive Plus, **Quick Scene Pro**) were left
exactly where they were — they are not ours to move, and the registry never picks them up because
their paths do not start with `FrogletTools/`.

**Doc references to the old paths are stale.** When you touch a doc that says
"Tools > Cosmic Shore > X", update it to the `FrogletTools/` path.

---

## Standing debt — the `Tools/Build/author_*.py` family's `--check`

A generator's whole claim is *the script is the source, the assets are the build*, and that claim
rests entirely on `--check` failing when an asset drifts. **Measured 2026-09-25 over the whole
family: 53 generators — 35 green, 11 RED, 7 un-runnable here** (`ModuleNotFoundError: No module
named 'numpy'`, an environment gap rather than a defect; keep those two classes apart before
quoting a number).

**Re-measured 2026-10-17 (`Ys-bleeding-edge` after bleeding-edge `a67fee38`): all 101
`author_*.py --check` pass, and each of the 11 listed as RED below now prints its own OK line**
(#969/#967 and the follow-ups fixed both classes). The list is kept as the record of what the
two failure classes looked like; re-run the loop before quoting a number, because the family
keeps growing.

```sh
for f in Tools/Build/author_*.py; do printf '%-46s ' "$(basename $f)";   timeout 180 python3 "$f" --check >/dev/null 2>&1 && echo OK || echo RED; done
```

RED as of that measurement: `author_bends_assets`, `author_dogfight_assets`,
`author_flora_populations`, `author_goal_stack`, `author_hijack_assets`,
`author_mode_controls_library`, `author_salvo_assets`, `author_switchback_assets`,
`author_tollway_assets`, `author_toybox_layout`, `author_wildlife_liberation_assets`.

They fall in two classes, both recorded in `CLAUDE.md`'s own anti-pattern list and neither
noticed at the time it was created:

- **A spent one-shot `assert` above the validation.** The generator clones a donor scene first and
  validates afterwards; the donor moves on, the migration's `assert` fires, and it takes every
  check below it with it — *a gate that ABORTS looks exactly like a gate that passes if nobody
  reads its output.* The fix is to make the one-shot STAND DOWN (guard the step, register the
  already-committed output so the downstream checks describe the shipped artifact), never to
  delete the checks.
- **An asset key a platform change deleted while the generator that authors it was left
  untouched.** A generator is a second place every schema change has to land, and it does not fail
  at the time of the change — it fails months later, on somebody else's branch, the next time
  anyone runs it. Re-running one of these currently RE-INTRODUCES the retired key.

**Glob `author_*.py`, not `author_*_assets.py`** — the narrow form sees 15 of the 53 and misses
every generator whose output is not a mode's asset set. **Do not attribute a red to your own
branch without an A/B** against a clean base worktree; on the measurement above the reds are
upstream and the measuring branch touched no `Tools/Build/` file at all.

---

## Tool index

| Lane | Tool | What it is for |
|---|---|---|
| FTUE | Quest Graph Editor, Dialogue Editor, Layout All Phases (Rows) | The FTUE quest graph and its dialogue, authored in place. |
| Utilities | Find Asset by GUID, Force Re-Serialize All ScriptableObjects, Audit Stray Network Objects | Project-wide lookups and integrity checks. |
| Build | **Pending Tool Changes** | Uncommitted asset output from editor tools. Validate, push, retire. The last gate before a branch ships. |
| Game Modes | **Game Mode Prefab Kit** | The prefabs a new game-mode scene needs; Add to Scene / Open Prefab / Validate, plus cross-scene drift detection and consolidation. See `Docs/GAMECANVAS.md`. |
| AI | **AI Training** | Operator surface for overnight genome search: Configure, Learn / Stop, Inspect, Archive, Deploy, and play a normal match against the archive. Keeper. Writes the `Assets/_SO_Assets/AI Training/` set (Quick Setup is idempotent) and records those writes, so it draws the ship panel. See `Assets/_Scripts/Utility/AITraining/README.md` § Operator. |
| Game Modes | **GameCanvas Unifier** | Absorb a forked in-game canvas into `CORE/GameCanvas.prefab`, re-point every scene onto it, and delete the fork. **Keeper, half spent.** The migration half (Absorb / Re-point / Delete fork) has run: the fork is gone and all 15 domain scenes are on CORE, so those buttons are dormant until another canvas forks. What stays live is **Fix prefab** — it enforces the canvas contract (1920x1080, Scale-With-Screen-Size, `AdaptiveCanvasScaler`, smart re-anchor through the Canvas Upgrader's own passes), strips missing scripts before saving, and **reverts any nested-instance override that NULLS a script-declared reference**, which is the class of bug that silently broke the domain picker and, later, every toast in the game — and **Fix scene**, which reverts redundant overrides. Note Unity cannot revert an override whose target no longer exists in the prefab; those have to come out of the scene YAML, which `gamecanvas_unification_report.py` reports. WRITER: records to the ledger, draws the ship panel. See `Docs/GAMECANVAS.md` §9. |
| Game Modes | End Game Conditions | The one place win conditions are authored for the domain modes. |
| Build | Windows x64 (Release / Development), WebGL Toybox (+ Development), Reveal Build Folder | Player builds. WebGL Toybox (`Editor/Build/WebToyboxBuild.cs`) is the browser Toybox: three scenes and `FROGLET_WEB_TOYBOX` for that build only, output `Builds/WebGLToybox/`; Unity Build Automation calls its `PreExport` instead (`Docs/WEBGL_TOYBOX.md`). Build tools write no assets, so no ship panel. |
| Build | **Amoebius ▸ Launch Amoebius** (+ *Rebuild Amoebius*, *Show Amoebius Folder*) | Opens Amoebius, Froglet's own engine for Cosmic Shore (`Port/`), built from the checkout the editor has open: `dotnet publish` of `Port/src/CosmicShore.Launcher` into `Library/Amoebius`, redone only when the launcher's source changed, then started. No .NET 10 SDK yet: it opens the copy in `Port/dist/Amoebius-Windows.zip`. Reader only - writes nothing but the gitignored `Library/Amoebius`, so no ship panel. See `Port/docs/LAUNCHER.md`. |
| Ecology | Prism Animation ▸ Validate Clock Wiring / Auto-Wire Clock Properties | The clock-material law gate. |
| Ecology | Prism Animation ▸ **Occlusion Dither Lab** | The occlusion corridor's unit shape, live — kernel + scale dials driven as shader globals **while the game runs**, a preview that IS the shipped GPU code, a Measure button that runs the corridor's own |coverage − alpha| admission rule against the shipped baseline, and Bake to write the result back into `PrismOcclusionCorridor.hlsl`. Keeper (re-runnable), but it writes source, so it draws the ship panel. See `Docs/PRISM_ANIMATION.md` §4.7. |
| Ecology | Measure Cell Environment Baselines | Per-cell prism baselines the phase thresholds ride on. |
| Ecology | Validate Lifeform Crystals | Every lifeform drops exactly one elemental crystal. |
| Vessels | Audits ▸ Audit Vessel Ability Rows / Elemental Morphs / …; Wiring ▸ Wire & Bake Petal Bars, Wire Vessel Ability Row, Plan / Swap Vessel Rig | Vessel HUD + model wiring. |
| Vessels | Wiring ▸ **Bake Crystal Hull Fusions** | Solves, at edit time, where each elemental crystal's faces land on each vessel hull (`Resources/CrystalHullFusionConfig` entries) and bakes the answer into a `CrystalHullFusionBakeSO` per entry (plus one shared `<Element>_FusionTemplate.asset` mesh per element), so a pickup in game does no geometry. Skinned and static multi-part hulls alike; 12 hulls × 4 elements. Lists every entry CURRENT / MISSING / STALE / UNRESOLVABLE with the reason; Validate fails on anything not current. **Keeper** — re-run whenever a hull or crystal model, an entry's `tileFill`/`surfaceLift` or the solver changes (the game warns once and solves at runtime until it is). Doc: `Assets/_Scripts/Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md` §4. |
| Performance | Performance Benchmark, Benchmarks ▸ Prism Grid / Crystal Depth Prism / Crystal Flip Wave, Prism Contact Probe, Texture Memory, Scene Object Counter | Frame cost and memory. |
| Scenes | Setup Freestyle Toybox, Setup Prism Grid Explosion Scene, Testing Multiplayer toggles | Scene scaffolding. |
| UI | Canvas Upgrader, Raycast Target Audit, Report On-Screen UI | UI authoring. |
| UI | **Codex** | The in-game encyclopedia, authored in one place across all three kingdoms — **Ethirions** (every crystal), **Ecology** (every lifeform) and **Tools** (every freestyle toy, grouped by the fundamental it changes: Pilot / World / Creation). *Scan & Merge* re-reads the project, *Bake* renders each entry's hero image, and the right-hand panel edits any entry by hand. Keeper (idempotent — the codex needs re-scanning every time a crystal, species or toy is added; **do not retire it**). Its load-bearing property is that **Scan & Merge is always safe to run**: a field-ownership contract makes harvested facts the tool's and all prose the writer's, and an entry whose source asset vanished is reported as an orphan and never auto-deleted. WRITER: records the codex asset and every PNG on the ledger; ships via Pending Tool Changes. See `Docs/CODEX.md`. |
| UI | **Wire Offline Menu Surfaces** (+ *Regenerate Icons*) | Wires Menu_Main's offline surfaces: the online/offline lamp, its `ConfirmQuestionBar`, and an `OfflineUIGate` per online-only panel. Keeper (idempotent, re-runnable whenever a panel is added). Works on the OPEN scene, never the YAML, so unsaved authoring is adopted rather than clobbered; finds panels by COMPONENT TYPE (they sit in prefab instances whose object names differ from their script names) and creates only what is missing. Screens dim in place, sub-panels hide — a screen the nav bar can reach must never be hidden. Generates the accept/cancel icons and never overwrites an existing file; the *Regenerate Icons* entry forces a re-render. WRITER: records to the ledger, ships via Pending Tool Changes. See `Docs/OFFLINE_MODE.md` §7–§11. |
| Vessels | **Vessel Studio** | Opens on the studio HOME: the web hub's heading and one card per `Docs/Studios/VesselStudio/studios.json` studio with the hub's own looping preview (`StudioPreviews.cs`); a card opens that studio built from the checkout and served by Amoebius (the artifact's own build, Sync/Ask/Decisions working; /vessel-studio D34). Its last card is **+ New studio**, which opens the Vessel Studio artifact in claude.ai, where the + panel sends a new-studio request straight to a Claude Code session (D36). READER: no tuning UI of its own. The artifact is the one source of truth; its numbers reach the vessel's assets through `/artifact-to-unity` (`Tools/Build/studio_to_unity.py`, `--check` for drift). `author_stoat_assets.py` seeds those assets once and no longer asserts their numbers. See `Docs/Studios/VESSEL_STUDIO_PLAN.md` §2. |
| AI | **Third Eye** | Watch Play mode from a second camera in its own window while you play in the Game view: pick any pilot (you, a rival, an AI), then Chase, Follow or Free (fly or orbit), the Vessel Studio's three cameras. *AI thinking* draws each AI's aim (a line to the point it steers for, coloured by state) and a ring on the crystal it is after; the bottom strip reads the watched AI's state, recoveries and difficulty mistakes. Its own hidden camera (never the gameplay rig), look adopted through `OffscreenCameraSetup`, posed in `beginCameraRendering` so it never lags; keeps rendering while paused. Reader only - writes `UserSettings/ThirdEyeSettings.asset`, never assets, so no ship panel. See `.claude/skills/vessel-ai/SKILL.md` §4. |
| Utilities | Toolbox ▸ Logging | Log levels, **diagnostic channels**, and console stack-trace depth. Channels (`CSLogChannel`) carry a finished system's BRING-UP telemetry — `[FLOW-n]` spawn/session flow, `[GyroidColony]` lattice — and default to OFF, so the trace stays in the tree as knowledge without spamming the console; turn one on before investigating that system. Warnings and errors never sit on a channel. Reader only — writes `EditorPrefs`, never assets, so no ship panel. |
| Utilities | Toolbox | Scene shortcuts, runtime switches, quest/crystal/UGS debug tabs. |
| Diagnostics | **Crash Detector** | Always-on editor crash watchdog. A background thread journals every error/exception to `Logs/CrashDetector/` and heartbeats a session sentinel; when the editor dies abnormally (Unity crash, PC fault, force-kill — even with the main thread hung), the next launch writes a `Crash-*.log` report from the stale sentinel + captured errors + the tail of Unity's own `Editor-prev.log`. Reader only — writes machine-local logs and `UserSettings/`, never assets, so no ship panel. Shares the Diagnostics window with the Bug Ledger; a crash report can be filed into the ledger with one button. |
| Diagnostics | **Bug Ledger** | The team's live bug list. It has NO window (removed 2026-10-15 — "no complex UI toolboxes in Unity"): every distinct error/exception/assert signature auto-files one issue into the GITIGNORED live store (`BugLedger/local/`), and the only UI is ONE question after a play test that logged a new error or made a new perf capture — sent with no question since 2026-10-16 (`BugHuntAutoSend`). It pushes the ledger plus new redlog/crash/perf files to the sender's own `bugledger/<user>` branch, built in a throwaway index so the working tree and branch are never touched (`BugLedgerSender` over `FrogletGit`). Claude fixes from there (`/bug-ledger`, `bug_ledger.py mark-fixed` writes VALIDATING + the fix commit onto that branch); the editor adopts the fix once that commit is in its HEAD, two clean play runs close it, a recurrence reopens it and asks again. Editor TOOLS still file findings through `BugLedger.ReportFromTool`/`ReportToolFindings` (`VesselSkimmerAudit` is the reference integration); they ride along on the next send. Full doc: `Docs/DIAGNOSTICS.md`; store contract: `BugLedger/README.md`. Writes no Assets/ — no ship panel. |
| QA | **QA Session** | The untested-development backlog, run from inside the editor. Creates the tester's session file, lists the backlog, and for each item renders its numbered steps, its PASS/FAIL definitions and its known do-not-fail-on exceptions verbatim — so a first-time tester never opens `QA_BACKLOG.md`. Reads as five numbered steps (session → right build → set up Unity → run tests → submit); each step's reasoning hides behind a `?`. Evidence attaches per item, including a one-click copy of the Editor log. Every rule lives in `Tools/QA/*.py` (self-tested, 59 checks) and the window only draws — it never parses or writes a results file, so a layout bug can never become a lost verdict. Writes no Assets/ — no ship panel; its output is the tester's own `Docs/QA/RESULTS/` file, ordinary committable project data. Needs python3 on PATH. Full doc: `Docs/QA/README.md`. |

---

## File index

| Role | Path |
|---|---|
| Master board | `Assets/_Scripts/Editor/FrogletTools/FrogletMasterToolWindow.cs` |
| Launch Amoebius (builds and opens the engine) | `Assets/_Scripts/Editor/LaunchAmoebius.cs` |
| Auto-discovery | `Assets/_Scripts/Editor/FrogletTools/FrogletToolRegistry.cs` |
| Metadata attribute | `Assets/_Scripts/Editor/FrogletTools/FrogletToolAttribute.cs` |
| Shared palette / widgets | `Assets/_Scripts/Editor/FrogletTools/FrogletEditorPalette.cs` |
| Ship panel (Validate & Push / Retire Tool) | `Assets/_Scripts/Editor/FrogletTools/FrogletToolShipPanel.cs` |
| Tool-output ledger | `Assets/_Scripts/Editor/FrogletTools/FrogletToolChangeLedger.cs` |
| Pending Tool Changes window | `Assets/_Scripts/Editor/FrogletTools/FrogletToolShipWindow.cs` |
| git CLI wrapper (quoting-safe, no wildcards) | `Assets/_Scripts/Editor/FrogletTools/FrogletGit.cs` |
| Prefab kit window | `Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs` |
| Crystal hull fusion baker | `Assets/_Scripts/Editor/CrystalHullFusionBaker.cs` |
| AI Training window | `Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs` |
| GameCanvas Unifier (window / engine) | `Assets/_Scripts/Editor/FrogletTools/GameCanvasUnifierWindow.cs`, `GameCanvasUnifier.cs` |
| Prefab kit validation | `Assets/_Scripts/Editor/FrogletTools/KitValidator.cs` |
| Third Eye (window / camera math / pilot read-out / settings) | `Assets/_Scripts/Editor/AI/ThirdEyeWindow.cs`, `ThirdEyeRig.cs`, `ThirdEyePilots.cs`, `ThirdEyeSettings.cs`; tests `Assets/_Scripts/Tests/Editor/ThirdEyeRigTests.cs` |
| Scene drift scanner (read-only) | `Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs` |
| Drift fixer (writes via PrefabUtility) | `Assets/_Scripts/Editor/FrogletTools/PrefabDriftFixer.cs` |
| Kit config SO | `Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs` |
| Codex window (list + actions) | `Assets/_Scripts/Editor/Codex/CodexWindow.cs` |
| Codex window (detail panel) | `Assets/_Scripts/Editor/Codex/CodexWindow.Detail.cs` |
| Codex harvester — ethirions + ecology, and the shared merge | `Assets/_Scripts/Editor/Codex/CodexHarvester.cs` |
| Codex harvester — tools (freestyle toys) | `Assets/_Scripts/Editor/Codex/ToolCodexHarvester.cs` |
| Codex image baker (photographs a prefab) | `Assets/_Scripts/Editor/Codex/CodexImageBaker.cs` |
| Codex tool portrait (draws a toy, which has no prefab) | `Assets/_Scripts/Editor/Codex/ToolPortraitBuilder.cs` |
| Codex data asset + schema | `Assets/_Scripts/ScriptableObjects/Codex/CodexSO.cs`, `Assets/Resources/Codex.asset` |
| Codex documentation | `Docs/CODEX.md` |
| Crash detector monitor (always-on watchdog) | `Assets/_Scripts/Editor/Diagnostics/CrashDetectorMonitor.cs` |
| Crash detector settings (`ScriptableSingleton`, `UserSettings/`) | `Assets/_Scripts/Editor/Diagnostics/CrashDetectorSettings.cs` |
| Diagnostics window (Crash Detector + Bug Ledger tabs) | `Assets/_Scripts/Editor/Diagnostics/DiagnosticsWindow.cs` |
| Bug ledger core (capture, store, validation) | `Assets/_Scripts/Editor/Diagnostics/BugLedger.cs` |
| Bug ledger issue model (hand-rolled JSON) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerIssue.cs` |
| Bug ledger settings (`ScriptableSingleton`, `UserSettings/`) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerSettings.cs` |
| Bug ledger tab view | `Assets/_Scripts/Editor/Diagnostics/BugLedgerView.cs` |
| Bug ledger: the automatic after-play send | `Assets/_Scripts/Editor/Diagnostics/BugHuntAutoSend.cs` |
| Bug ledger sender (bugledger/<user> branch, throwaway index) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerSender.cs` |
| Bug ledger git publisher (scoped, off-thread) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerPublisher.cs` |
| Bug ledger store contract (committed data) | `BugLedger/README.md` (project root) |
| Shared signature core (runtime-safe, for the future in-game reporter) | `Assets/_Scripts/Utility/BugSignature.cs` |
| Signature determinism tests | `Assets/_Scripts/Tests/Editor/BugSignatureTests.cs` |
| Doc-link resolver (DOCS chips → GitHub) | `Assets/_Scripts/Editor/FrogletTools/FrogletDocLinks.cs` |
| Diagnostics documentation | `Docs/DIAGNOSTICS.md` |
| QA session window (draws only; all rules live in python) | `Assets/_Scripts/Editor/QA/QASessionWindow.cs` |
| QA session CLI the window drives (`state`/`new`/`set`/`attach`/`submit`) | `Tools/QA/session.py` |
| QA submit validator + publisher | `Tools/QA/submit.py` |
| QA results applier (backlog/archive/dev-tasks, content-hash ledger) | `Tools/QA/apply_results.py` |
| QA loop documentation | `Docs/QA/README.md` |
