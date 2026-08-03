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
| `Category` | Swimlane + accent colour. Ten lanes, see `FrogletToolCategory`. |
| `Importance` | **1** (niche) … **5** (used every day). Drives sort order *and* bar length on the board. Default 3. |
| `Description` | One line rendered beside the bar. |
| `DisplayName` | Overrides the menu leaf as the label. |

**Where the attribute can go:** `FrogletToolAttribute` compiles into the *editor* assembly, so it
can only be used from a file under an `Editor/` folder. A tool that lives in the runtime assembly
behind `#if UNITY_EDITOR` (e.g. `LogControlWindow`) still appears on the board — it just falls back
to the inferred category and importance 3.

---

## The board

**FrogletTools ▸ Froglet Master Tool** renders the registry as a Gantt-style roadmap:

- One **colour-coded swimlane per category**, collapsible, with a count pill.
- One **bar per tool**, its length proportional to `Importance` against the ruler at the top.
  Click a bar to launch the tool.
- **Right-click a row** for Launch / Copy menu path / Ping script / Open script.
- Search filters across name, menu path, description and category.
- A **"Needs migration"** strip lists any tool still declaring a `[MenuItem]` outside
  `FrogletTools/`, so the convention enforces itself instead of relying on this document.

Launching routes through `EditorApplication.ExecuteMenuItem`, so validate functions and editor
context behave exactly as a manual menu click would, with a direct reflection call as fallback.

### Lanes

| Category | Lane | Colour |
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

Third-party menus (PlayFab, FMOD, Obvious Soap, Primitive Plus, Quick Scene Pro) were left alone —
they are not ours to move, and the registry filters them out.

**Doc references to the old paths are stale.** When you touch a doc that says
"Tools > Cosmic Shore > X", update it to the `FrogletTools/` path.

---

## Tool index

| Lane | Tool | What it is for |
|---|---|---|
| Game Modes | **Game Mode Prefab Kit** | The prefabs a new game-mode scene needs; Add to Scene / Open Prefab / Validate, plus cross-scene drift detection and consolidation. See `Docs/GAMECANVAS.md`. |
| Game Modes | End Game Conditions | The one place win conditions are authored for the domain modes. |
| Build | Windows x64 (Release / Development), Reveal Build Folder | Player builds. |
| Ecology | Prism Animation ▸ Validate Clock Wiring / Auto-Wire Clock Properties | The clock-material law gate. |
| Ecology | Measure Cell Environment Baselines | Per-cell prism baselines the phase thresholds ride on. |
| Validation | Validate Lifeform Crystals | Every lifeform drops exactly one elemental crystal. |
| Vessels | Audit Vessel Ability Rows / Elemental Morphs, Wire & Bake Petal Bars, Plan Rig Swap | Vessel HUD + model wiring. |
| Performance | Performance Benchmark, Prism Grid Benchmark, Texture Memory, Scene Object Counter | Frame cost and memory. |
| Scene Setup | Setup Freestyle Toybox, Setup Prism Grid Explosion Scene | Scene scaffolding. |
| Interface | Canvas Upgrader, Raycast Target Audit, Toast Notification setup | UI authoring. |
| Misc | Toolbox | Logging levels, scene shortcuts, runtime switches. |

---

## File index

| Role | Path |
|---|---|
| Master board | `Assets/_Scripts/Editor/FrogletTools/FrogletMasterToolWindow.cs` |
| Auto-discovery | `Assets/_Scripts/Editor/FrogletTools/FrogletToolRegistry.cs` |
| Metadata attribute | `Assets/_Scripts/Editor/FrogletTools/FrogletToolAttribute.cs` |
| Shared palette / widgets | `Assets/_Scripts/Editor/FrogletTools/FrogletEditorPalette.cs` |
| Prefab kit window | `Assets/_Scripts/Editor/FrogletTools/GameModePrefabKitWindow.cs` |
| Prefab kit validation | `Assets/_Scripts/Editor/FrogletTools/KitValidator.cs` |
| Scene drift scanner (read-only) | `Assets/_Scripts/Editor/FrogletTools/PrefabInstanceSceneScanner.cs` |
| Drift fixer (writes via PrefabUtility) | `Assets/_Scripts/Editor/FrogletTools/PrefabDriftFixer.cs` |
| Kit config SO | `Assets/_Scripts/ScriptableObjects/GameModePrefabKitSO.cs` |
