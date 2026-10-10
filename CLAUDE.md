# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## Where everything else lives (read the topic file before touching its area)

This file holds only the rules every session needs. Everything else moved verbatim into topic files under `Docs/claude/`. Each entry lists the section headings the file contains, so a reference like "CLAUDE.md § Anti-Patterns" can be found by grepping this index. To find a passage quoted from the old single-file CLAUDE.md, search both places: `grep -rn '<phrase>' CLAUDE.md Docs/claude/`.

- [`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`](Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): LOCKED. Read before any ecology, fauna, flora, or HyperSea change.
  Sections: Ecosystem Design Principles (LOCKED — read before any ecology change)
- [`Docs/claude/ABOUT_PROJECT.md`](Docs/claude/ABOUT_PROJECT.md): Vessel classes, team domains, tech stack.
  Sections: About This Project; Vessel Classes; Team Domains; Tech Stack
- [`Docs/claude/PROJECT_STRUCTURE.md`](Docs/claude/PROJECT_STRUCTURE.md): Folder layout, assembly definitions, where tests live, scene inventory.
  Sections: Project Structure; Assembly Definitions; **Tests live under an `Editor/` folder, not in an asmdef — until their dependencies are extracted.**; Scene Inventory
- [`Docs/claude/GAME_MODES_AND_CONTROLLERS.md`](Docs/claude/GAME_MODES_AND_CONTROLLERS.md): The GameModes enum (every mode), controller hierarchy, game launch pipeline.
  Sections: Game Modes & Controllers
- [`Docs/claude/DOCUMENTATION_INDEX.md`](Docs/claude/DOCUMENTATION_INDEX.md): Catalogue of every doc under Docs/ and what it covers.
  Sections: Documentation Index
- [`Docs/claude/ARCHITECTURE_CORE.md`](Docs/claude/ARCHITECTURE_CORE.md): SOAP, threading, bootstrap, auth/session flow, Reflex DI, input strategy.
  Sections: Architecture Patterns; ScriptableObject Config Separation; SOAP — Scriptable Object Architecture Pattern (Primary Architecture); Threading & Main-Thread Affinity; Bootstrap & Scene Flow; Authentication & Session Flow; Dependency Injection (Reflex); Input Strategy Pattern
- [`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`](Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md): Impact effects architecture; the LOCKED PvP-is-petals-only rule; the LOCKED FMOD exposed-field convention.
  Sections: Impact Effects Architecture; PvP is petals only (LOCKED, Garrett 2026-10-10); Audio (FMOD) — every sound is an exposed, editable field (LOCKED convention)
- [`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`](Docs/claude/MULTIPLAYER_AND_SOCIAL.md): Netcode, player spawning, party/invite lobby, friends, AI backfill, SkimRace.
  Sections: Multiplayer / Netcode; Party / Invite Lobby System; Friend System; Player Count & AI Backfill Pipeline; SkimRace Game Mode
- [`Docs/claude/FTUE_DIALOGUE_AI.md`](Docs/claude/FTUE_DIALOGUE_AI.md): The FTUE quest graph, dialogue system, AI opponent system. Holds the LOCKED guided-path rule: to send a player somewhere, spotlight the one control to press next, dim and disable everything else except Settings, and let them press it - never navigate for them.
  Sections: FTUE (First-Time User Experience) — the QUEST GRAPH; Dialogue System; AI Opponent System
- [`Docs/claude/MENU_AND_LAVA_LAMP.md`](Docs/claude/MENU_AND_LAVA_LAMP.md): Menu_Main screens, ScreenSwitcher, menu freestyle (lava-lamp) HUD.
  Sections: Menu Screen Navigation (Menu_Main Scene); Lava-Lamp Mode (Menu Freestyle Merge)
- [`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`](Docs/claude/VESSEL_HUD_AND_ELEMENTS.md): Elemental bars, hull morphs, rigged-model swap, the LOCKED four-icon ability row.
  Sections: Elemental Bars (per-vessel buff/debuff display); Elemental Hull Morphs (the vessel model is an element display); The Four-Icon Ability Row (LOCKED structure — every vessel HUD)
- [`Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md`](Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md): Namespace convention, key systems & classes reference, async pattern.
  Sections: Namespace Convention; Key Systems & Classes; Async Pattern
- [`Docs/claude/ANTI_PATTERNS.md`](Docs/claude/ANTI_PATTERNS.md): Read before writing or reviewing C#.
  Sections: Anti-Patterns to Avoid
- [`Docs/claude/SHADERS_AND_PERFORMANCE.md`](Docs/claude/SHADERS_AND_PERFORMANCE.md): HLSL / Shader Graph, performance standards, prism performance.
  Sections: Shader & Visual Development; HLSL / Shader Graph; Performance Standards; Prism System Performance
- [`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`](Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md): Read before designing any gameplay feature: fundamentals, order of preference, universality.
  Sections: Design Philosophy: Favor Emergent Systems Over Bespoke Solutions; The fundamentals (working list); Process for curating fundamentals; Order of preference; Don't "cheat" emergence without asking; Universality — one HyperSea, one rule set; When in doubt

## Testing

### Test Infrastructure

- **Framework**: Unity Test Framework 1.6.0 (NUnit-based)
- **Edit-mode tests**: `Assets/_Scripts/Tests/Editor/` — 17 test files covering enums, data SOs, geometry utils, party data, resource collection, disposable groups, camera settings, etc.
- **Bootstrap tests**: `Assets/_Scripts/System/Bootstrap/Tests/Editor/` — `AppManagerBootstrapTests` (file: `BootstrapControllerTests.cs`), `BootstrapConfigSOTests`, `SceneTransitionManagerTests`, `ApplicationLifecycleManagerTests`, `ApplicationStateMachineTests`, `SceneFlowIntegrationTests`
- **Multiplayer tests**: `Assets/_Scripts/Controller/Multiplayer/Tests/Editor/`
- **SOAP framework tests**: `Assets/Plugins/Obvious/Soap/Core/Editor/Tests/`
- **Test scenes**: `Assets/_Scenes/TestInput/`, `Assets/_Scenes/Game_TestDesign/`

### Build & CI

No automated CI/CD pipeline is currently configured. Builds are manual. Build profiles live in `Assets/Settings/Build Profiles/`.

### Unity CLI verification — `/verify-unity` gates every C# commit (LOCKED rule)

The repo carries **`com.unity.pipeline`** (`0.5.0-exp.1`, experimental) so a session can drive the
**open** Unity Editor from the terminal via the standalone `unity` binary — real compiles, Play
mode, in-editor verification, no human alt-tabbing and no copy-pasted console errors. Setup +
troubleshooting: `Docs/unity-cli-setup.md`. The CLI is experimental and changes often; **`unity
--help` for the installed version is authoritative** — never assume a command or flag from memory
or from another machine.

- **The rule: any C# change must pass `/verify-unity` before it is committed.** A green edit-mode
  suite or a clean mental compile does not substitute for the Editor actually compiling and loading
  the change. If the CLI genuinely isn't available in the session (no editor open, no `unity`
  binary), say so explicitly in the commit/PR and file the change in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md` as before — never claim a verification that didn't run.
- **`[CliCommand]` wrappers** are the editor methods exposed to the CLI's `unity command` surface —
  the sanctioned way to give the CLI a repo-specific operation. They are editor tooling: they live
  under an `Editor/` folder (Assembly-CSharp-Editor), never in runtime code.
  <!-- TODO: no [CliCommand] wrappers exist in-repo yet; when the first one lands, record its
       location and the naming convention here. -->
- **The runtime component never ships.** `UnityPipelineReleaseGuard`
  (`Assets/_Scripts/Editor/Build/`) is an `IPreprocessBuildWithReport` that throws
  `BuildFailedException` on any **non-development** build whose shipped content (build scenes,
  prefabs they instantiate, Resources, preloaded assets) would include the `com.unity.pipeline`
  runtime component. Development builds are exempt — that is the sanctioned way to debug the CLI
  integration in a player. Do not weaken or bypass this guard: release builds go to paying Steam
  customers.

## Editor Tooling (LOCKED convention — read `Docs/TOOLING.md` before adding any `[MenuItem]`)

**Every first-party editor tool lives under ONE menu root, `FrogletTools/`, and appears
automatically in `FrogletTools > Froglet Master Tool`.** The `Tools/Cosmic Shore/…` and
`Cosmic Shore/…` roots were retired — do not reintroduce them, and do not add a tool under
`Tools/`, `Window/`, or a new root of your own.

- **Discovery is automatic, never registered.** `FrogletToolRegistry` reflects over `[MenuItem]`
  attributes; a tool shows up on the board the moment its path starts with `FrogletTools/` and it
  compiles. There is no manifest to update. That prefix is also the only filter, so third-party
  package menus (FMOD, Soap, Quick Scene Pro) are never picked up and are left where
  their vendors put them.
- **The board is a card grid**: one collapsible colour-coded section per category, one card per
  tool (title, description, five-dot importance), most important first, flowing into as many
  columns as the window is wide enough for.
- **`[FrogletTool(category, Importance, Description)]`** on the same static method as the
  `[MenuItem]` controls the section, the ranking (1–5, which is also the dot rating on the card)
  and the blurb. It is optional — omit it and the registry infers a category from the path/type
  name and uses importance 3. The attribute compiles into the **editor** assembly, so only files
  under an `Editor/` folder can use it; a runtime-assembly tool behind `#if UNITY_EDITOR` still
  appears, just with inferred metadata.
- **Draw through `FrogletEditorPalette`** (banner, `ColorButton`, `StatusPill`, `DrawCard`,
  accent stripes, semantic Ok/Warn/Error/Info colours, light-skin adaptation) so every Froglet
  window reads as one product. Do not hand-roll `GUI.color` juggling in a new window — extend the
  palette instead.
- **Prefab drift is a first-class check.** `PrefabInstanceSceneScanner` reads prefab-instance
  overrides straight out of scene YAML (fast, read-only, no scenes opened) and
  `PrefabDriftFixer` performs every write through `PrefabUtility` on a properly loaded scene.
  Use these rather than opening scenes to interrogate `PrefabUtility`, and never hand-edit scene
  or prefab YAML to "apply" an override. **FrogletTools > Ecology > Audit Cell-Owned Visuals**
  rides the same scanner for the Cell's half of this: it reports scene-placed membrane/nucleus/
  cytoplasm instances that duplicate what the scene's Cell already spawns, and Cell overrides whose
  `propertyPath` names a field the script no longer has (Unity never prunes an unresolvable
  modification, so retired fields linger for years pointing at guids no asset carries).
- **Editor-tool config belongs in a ScriptableObject**, not a hard-coded list in the window
  (`GameModePrefabKitSO` is the reference) — same config-separation rule as gameplay.
- **A tool's OUTPUT is the deliverable; the tool is scaffolding.** A wirer/setup/migration tool
  writes a scene, prefab or SO into the human's **working tree**, while the branch carries only
  the tool — so the tool merges and its data does not, and the feature is broken on every other
  machine with nothing in the diff to explain it. Any tool that writes assets therefore
  `FrogletToolChangeLedger.Record(ToolName, path)`s in the same block that writes each one and
  draws `FrogletToolShipPanel.Draw(Ship, this)`: **Validate & Push** (saves, validates, stages
  ONLY that tool's recorded paths — never `-A` — commits, pushes; protected branches refused) and
  **Retire Tool** (deletes the one-off + scratch assets, refusing while its output is still
  unpushed, so retirement can't strand it). The catch-all is **FrogletTools > Build > Pending Tool
  Changes**, which also lists dirty files no tool claimed. Contract:
  `Docs/TOOLING.md` § "Tool output is a deliverable". Agent-side gate: the `/ship-tools` skill,
  and `/ship` §2.5 — which `/ship-quick` and `/ship-deep` inherit and **no mode may skip**. A
  READER tool (audit/report only) needs none of this; say so in its doc comment.

## Shared prefabs are single sources of truth (see `Docs/GAMECANVAS.md`)

`GameCanvas.prefab` is the in-game UI surface for every mode; the same rules apply to any prefab
shared across scenes.

- **A scene override always beats the prefab.** Overrides parked in a scene are why editing the
  prefab stopped changing anything — six game-mode scenes each carried ~1,770 unapplied overrides,
  1,734 of them byte-identical. If a change should apply to every mode, **Apply to Prefab**.
  **An override that NULLS a reference is the dangerous shape**, because the prefab looks correctly
  wired and nothing on it says otherwise: `ArcadeGameConfigureModal.prefab` wires `domainInfoItems`
  to its three real tiles and Menu_Main overrode all three to `{fileID: 0}`, so the modal attached
  no click listener to any domain tile (`if (!item || !item.Button) continue;`) and **a domain pick
  never reached the server in any mode** — every player flew the Jade default, which reads on screen
  as blue. Fixed by DELETING the overrides (`Tools/Build/fix_domain_picker_wiring.py`, `--check`) so
  the prefab's own wiring applies — never by re-authoring the same references into the scene, which
  is how the override got there. General rule: **a nulled reference fails as "the feature quietly
  does nothing", not as an error**, so audit for `objectReference: {fileID: 0}` overrides on any
  serialized list a feature depends on.
- **A variant, never a copy.** If a mode needs a different canvas, use **Create ▸ Prefab Variant**.
  `GameCanvas-SkimRace.prefab` is a hard copy, which severed propagation and left 8 references
  dangling into the other prefab asset.
- **Genuinely per-mode values go in config or code**, not a scene override: an SO keyed by
  `GameModes`, or a runtime resolve. There is exactly one `MiniGameControllerBase` per gameplay
  scene, so the canvas finds it itself (`MiniGameHUD.EnsureReadyButtonWiring`,
  `Scoreboard.ResolveGameController`) — an explicit inspector assignment still wins.
- **Never bind a UnityEvent to a concrete controller subclass.** `OnReadyClicked` is public on
  `MiniGameControllerBase`; naming `SkimRaceController` in the inspector creates a per-scene
  override for no gain.
- **Run `FrogletTools > Game Modes > Game Mode Prefab Kit` ▸ Validate before committing a scene**
  that contains a shared prefab.
- **A scene's structural edits on an instance are part of its state, and an override count does not
  show them.** Every one of the 15 GameCanvas fork scenes REMOVED the prefab's own HUD and Scoreboard
  components and re-added them as scene components, deleted the end-game subtree and added a newer
  one — so "consolidate the uniform overrides into the prefab" would have produced a prefab nobody
  runs. Read `m_RemovedGameObjects` / `m_RemovedComponents` / `m_AddedComponents` (the scanner
  counts them) before deciding what a prefab should become, and take the shipped state from a
  DONOR SCENE, not the asset. Retire a fork with **FrogletTools ▸ Game Modes ▸ GameCanvas Unifier**
  (`Docs/GAMECANVAS.md §9`) — a guid swap by hand dangles every override and every scene reference.

## Code Style

- Clean, maintainable C# — favor readability over cleverness
- Use `[Header("Section Name")]` and `[Tooltip("...")]` attributes generously on serialized fields
- Use `[SerializeField]` with private fields, not public fields
- Pattern match where it improves clarity: `effects is { Length: > 0 }`
- Use `TryGetComponent` over `GetComponent` + null check
- Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
- Anti-spam / cooldown patterns belong in the SO config, not hardcoded
- Always assign static numeric values to enum members to prevent Unity serialization drift
- **All player-facing text has a human-facing control (LOCKED rule, every branch).** Every word the
  player can read is a serialized field on an SO, prefab or component, or in an authored table. A
  human can add, edit, blank or delete it in the inspector (or an authoring window) without touching
  code. This is the text twin of the FMOD rule ("every sound is an exposed `EventReference`"). The
  rule in practice:
  - **Never** put a C# string literal that reaches a `TMP_Text`, toast, dialogue or button label.
  - Text whose facts are computed is an **authored template with tokens** (`Hold {glyph:Time} to
    boost`). Code fills the tokens; a human owns the sentence.
  - An empty field shows nothing. That is how a line is deleted, so never fall back to a hardcoded
    default.
  - **Excluded:** logs, dev/diagnostic overlays and editor-tool UI.
  - Existing literal-built strings are the **legacy shape**. Move them to authored fields when you
    touch them. Worked example: `Docs/ModePreview/TRAINING_PLAN.md` §4.2.1.
- Commit messages follow conventional commits: `type(scope): summary` (see `GIT_RULES.md`)

## Debugging Methodology

When investigating issues, follow this systematic approach:

1. Reproduce the issue consistently
2. Add `ProfilerMarker`s to isolate the hot path
3. Check the call stack in Timeline view for self-time
4. Narrow to the specific derived class (base class profiling often hides the real culprit)
5. Fix, profile again, confirm improvement with data

Do not guess at performance problems. Profile first.

## Communication Preferences

- Be direct and technical. Skip preamble and motivational framing.
- When presenting solutions, lead with the code, then explain if needed.
- If you need to make a judgment call between two valid approaches, pick the one that's simpler to maintain and mention the tradeoff briefly.
- When refactoring, preserve the existing naming conventions and folder structure unless explicitly asked to reorganize.
- For shader work: always specify which render pipeline stage and what Shader Graph node types are involved.
- Don't repeat back what I just told you. Acknowledge briefly and move to the solution.

## What Claude Code Should Never Do

- Stop to ask "would you like me to continue?" after completing one of several related files
- Introduce new packages or dependencies without flagging it first
- Restructure folder organization or namespaces without explicit instruction
- Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
- **Write a lookup that cannot be asked as a QUESTION — one whose only form reports a miss as an error.** A `TryGet` that logs is a DEMAND ("give me this; a miss is a fault") and is right where the caller is about to act on the result. It is wrong for a PROBE ("which of these exist on this build?"), and the two readings end up sharing one method because the demand is written first. `VesselPrefabContainer.TryGetShipPrefab` did: **nine** of its twelve call sites were probes, including `ToyVesselRoster.ResolveOffered`, which runs every time a toy matrix is built — i.e. **every domain change** — so a hull that is on the fleet roster but whose prefab is not authored yet (the Butterfly) produced a red `LogError` per rebuild, and buried the one keyed warning that names the setup tool to run. **A question that cannot be asked without raising an error makes every asker either lie or shout**: the ones that lie skip the check and reintroduce the silent-omission defect, and the ones that shout drown the message that would have fixed it. Give the lookup a `reportMissing: false` form (or a quiet `Has…` twin) and keep the loud one for the sites that really are about to spend the result. `.claude/skills/vessel/references/CONTRACT.md` §1.
- **Leave a finished system's bring-up telemetry on `CSDebug.Log` (or, worse, raw `Debug.Log`).** The dense per-step trace you need while building a system is console spam the day it works, and every such trace outlives the cycle that wrote it — the `[FLOW-n]` spawn trace and the `[GyroidColony]` census each shipped ~60 and 1-per-5s log lines forever. Put it on a **`CSLogChannel`** (`CSDebug.LogVerbose(channel, …)`, `CSDebug.IsVerbose(channel)`), which defaults to OFF and is toggled per-channel in **FrogletTools > Toolbox > Logging** — the trace stays in the tree as knowledge without shouting. Guard with `IsVerbose` first anywhere the interpolated message itself is expensive (a `[Conditional]` method's arguments are still evaluated in the Editor). Warnings and errors never move to a channel; a real fault must always be loud. And **nothing per-frame or per-contact gets a log at all** — not even a channelled one: the offenders that surfaced here were a per-skim resource log and a per-frame camera-zoom readout, both simply deleted. **The 2026-09 console sweep made this the RULE for every subsystem, not just the two that
  prompted it**: the console sat at 999+ before a match started because ~780 info sites were
  unconditional — the party layer re-logging its 3 s presence refresh, the boot chain narrating
  ~40 steps, the lattice colonies logging per PLANT, every vessel spawn printing its audio bring-up,
  and every menu screen logging one line per CARD it populated. Every subsystem now has a channel
  (`Boot`, `Party`, `CloudData`, `Audio`, `Ecology`, `ArcadeMatch`, `MenuUI`, `Input`,
  `VesselTelemetry`, `PrismRuntime`, `FTUE`, plus the per-feature ones), the
  Logging toolbox REFLECTS the enum (its hand-kept row table had already drifted nine channels
  behind), and each member carries a `[CSLogChannelLabel]` that `CSDebugTests` asserts. The
  decision per site is mechanical — a method-entry trace, a per-item population line, a data dump
  or a placeholder is DELETED; a state transition or a session/target/course fact goes to
  `LogVerbose`; a real fault stays a warning or error; and `<color=…>` / emoji never appear in a
  log. `python3 Tools/Build/check_console_logging.py` (`--self-test`) is the gate: it fails on a
  raw `Debug.Log` in runtime code and on rich text inside any `CSDebug` call, with the tool,
  benchmark and stress-test scripts whose output IS their deliverable allow-listed by path.
- Write a tooling, diagnostics, benchmark, or debug-overlay script that uses `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD` without reading `Docs/CONDITIONAL_COMPILATION.md` and running `python3 Tools/Build/check_conditional_compilation.py` first. "It compiles in the Editor" proves nothing here — the Editor always defines `UNITY_EDITOR`, so this whole bug class is invisible until the Release build fails
- Leave TODO comments as a substitute for completing the work
- Generate code that compiles but ignores the established architecture patterns above
- Add if-null guards on SOAP ScriptableEvent serialized fields — fail loud
- Plug a placeholder/temp FMOD event into a new sound, or hardcode an event path in code. Add the `[SerializeField] EventReference` (per ability, per trigger, per emitter), ship it **empty**, and say so — an unwired slot is a visible TODO; a temp event is one nobody ever finds
- **Leave a spent one-shot `assert` sitting ABOVE a generator's validation section.** A `Tools/Build/author_*.py` script typically performs a one-time migration (clone a donor scene, patch its wiring) and *then* validates everything it built. When the donor moves on, the migration's `assert` fires — and takes every check below it with it, while `--check` is still wired into the workflow and still looks like a gate. `author_dogfight_assets.py` validated **nothing** for as long as that was true, including four checks added specifically to guard the missile-tier scoring; `author_ribcage_assets.py` and `author_wildlife_liberation_assets.py` sat in that state on the identical `controller field block not found in donor scene` assertion. **Measured across the family on 2026-09-09, SIX of the eight mode generators then checked were red and nobody was reading them**, in two distinct classes: those two plus `author_drumfire_assets.py` (`donor crystal block not found`) aborted on a spent one-shot, while `author_salvo_assets.py`, `author_dogfight_assets.py` and `author_hijack_assets.py` failed their asset-key validation because they still emitted `CallToActionTargetType`, a field the call-to-action retirement deleted from `SO_ArcadeGame` — that retirement swept the 42 shipped assets and left the generators that author them untouched, so re-running any of them re-introduced the retired key. (`author_switchback_assets.py` was a third shape: its asserts passed and it reported drift from its own output.) A fourth class surfaced when the whole family was measured again: twelve generators re-emitted the legacy `CardBackground` placeholder over the `/cardart` render. **Fixed, measured 2026-10-06:** #969 moved the shared pieces into `arcade_mode_lib.py` — `card_background()` (the guid comes from the mode's render, so a card that drifts back to the placeholder fails), `card_errors`/`check_cards` (retired keys), `drift()` (Dog Fight, Hijack and Salvo used to print "no files written" and exit 0 without diffing) and `committed_scene()` (the clone stands down once the scene is committed), all held by `arcade_mode_lib.py --self-test`; #967 guarded Wildlife Liberation's spent clone and fixed Tollway. All 25 `author_*_assets.py` scripts (21 mode generators plus four flora/vessel kits) now pass `--check`, and the Ribcage and Drumfire modes have been retired with their generators. `author_urchin_assets.py --check` still validates without diffing against disk. The general lesson restated: **a generator that owns an asset's content is a second place every schema change has to land, and it does not fail at the time of the change — it fails the next time somebody runs the generator, which may be months later and on somebody else's branch.** **A spent one-shot must STAND DOWN, not abort** — guard it (`if not DONOR_STILL_MATCHES: skip`), register the already-committed output so the downstream checks describe the shipped artifact, and prove the checks fire with a negative control. Corollary worth stating separately: **a gate that aborts looks exactly like a gate that passes if nobody reads its output**, so a green `--check` in a commit message is only evidence if you can name a failure it produced.
- Use `renderer.material` when `renderer.sharedMaterial` + MaterialPropertyBlock works
