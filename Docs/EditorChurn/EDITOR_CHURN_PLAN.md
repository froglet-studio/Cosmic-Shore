# Editor Churn: the plan (Project file)

The workstreams and the starting evidence for the Editor Churn project. Their status lives in `Docs/EditorChurn/TRACKER.md`, not here.

## Workstreams, ranked

**W1: Reproduce and measure, with no person needed.**
- `bash Tools/Build/verify_unity.sh --project <full checkout> --no-tests` opens the real editor in batchmode on a clean clone. Then capture `git status --porcelain` and `git diff`, after one open and after two (a file that changes on the second open shows a writer that never settles).
- `Tools/Build/editor_churn.py` is a reader with `--self-test`. It sorts each dirty file into one category: missing meta, generator YAML not in canonical form, a package or settings file that rewrites itself, version skew, line endings, per-machine state, an editor script writing on load, or unknown. It shows the smallest part of the diff that matters and names the likely writer.
- If a thread has no Unity, say so, and use the people's patches.

**W2: Missing and forked `.meta` files (the most dangerous one).**
- Claude sessions write `Assets/` files with no Unity running, so nothing creates the `.meta`. Each machine's Unity then makes a different random GUID for the same script, which can break references differently on each machine.
- Commit the missing metas with **the GUID that existing references already use**: grep prefabs, scenes and SOs for `m_Script` / `guid:`. Never commit a second GUID for the same asset.
- `Tools/Build/check_meta_files.py` (`--self-test`, `--fix` writes deterministic GUIDs) fails on:
  - a file or folder with no `.meta`;
  - a `.meta` with no file;
  - a duplicate GUID.

  It allow-lists `~` folders and `.bundle` contents by rule rather than by name.

**W3: Generators that write non-canonical YAML.**
- The `Tools/Build/author_*.py`, `fix_*.py` and other scripts that write prefabs, materials, scenes or `EditorBuildSettings.asset` must produce exactly what Unity re-serializes, so a generator run followed by a Unity open makes no diff.
- Put the shared writer in one place (`arcade_mode_lib.py` or a new `unity_yaml.py`). Every generator that writes an asset or `.cs` also writes its `.meta`.
- Each generator's `--check` fails on non-canonical output. Prove it with a negative control: drop a default field and show the check fails.
- Respect the locked rules while you do this:
  - shared-prefab overrides: never re-author nulled overrides into a scene (`Docs/GAMECANVAS.md`);
  - spent one-shot steps stand down rather than abort;
  - `colour_trace.py --check`.

**W4: Package auto-writers.**
- EDM4U, the Android dependency resolver: `GvhProjectSettings.xml` and the `Assets/Plugins/Android/*.gradle` templates.
- Vivox: `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`.
- URP's material upgrader: `URPProjectSettings.asset`.
- Anything else the harness finds.

For each one, prefer committing its settled output once, then switch it to resolve on demand or at build time rather than on every open.

**W5: Version skew and line endings.**
- Is everyone on the pinned editor version and `Packages/packages-lock.json`?
- `.gitattributes` has `* text=auto`. Check CRLF vs LF behaviour on Windows, and decide per file type.

**W6: Editor code that writes on load.**
- Look for `[InitializeOnLoad]`, `[InitializeOnLoadMethod]`, `EditorApplication.delayCall`, `AssetDatabase.SaveAssets`, `EditorUtility.SetDirty`, `AssetPostprocessor`, `OnValidate` code that changes serialized state, and `EditorBuildSettings.scenes =` (check Build Profiles in `Assets/Settings/Build Profiles/` too).
- An editor tool may write only when a person runs it, never on open.

**W7: Gates.**
- Wire `check_meta_files.py` and the canonical-YAML checks into `.claude/skills/ship/gates.sh`, the static tier of `.github/workflows/unity-ci.yml`, and `bleeding-edge-guard.yml`.
- Add an opt-in local pre-commit hook for GitHub Desktop users, never a required one.
- At most one quiet, fast session hook that writes a `.meta` when a session creates an `Assets/` file. Shombith turned the noisy hooks off on 2026-10-23, so his `/approve` decides it.
- The open-and-diff harness becomes a CI step once a Unity runner is attached again. Until then, say it's manual.

**W8: The ruleset.**
- `Docs/claude/EDITOR_CHURN.md`, with a one-line index entry and its section headings in `CLAUDE.md` § "Where everything else lives".
- It covers:
  - Rules for sessions: no `Assets/` file without its `.meta`; canonical YAML only.
  - Rules for people:
    - what's safe to discard;
    - what to commit;
    - a new `.meta` on someone else's script = commit nothing and report it;
    - how to update `bleeding-edge` without losing work.
  - Rules for package and settings changes: an EDM4U resolve, a URP upgrade or a Unity version bump is one person, one commit, announced.
  - A two-minute triage for "I see a phantom change".
- The tracker's file table is linked from it as the living list.

**W9: The hand test.**
- Windows and macOS each clone fresh, open Unity, and see a clean GitHub Desktop.
- Suggest it as **one** ticket through `/team-tasks`, with a checklist line per OS, for the person to approve. Never file it yourself, and never assign it.

## Starting evidence (as of 2026-11-12, `bleeding-edge` 4d580c31; the tracker overrides this once it exists)

Caleb's list, 16 files:
- the four Configure prefabs (`ArcadeGameConfigureModal`, `ConfigureArenaView`, `ConfigureMaelstromView`, `ConfigureView`);
- five new `.cs.meta` (`RegattaCourseFeatures`, `IJacksRaceCoach`, `JacksRacePlan`, `RegattaCourseFeaturesTests`, `SkeinRailSeekTests`);
- `mainTemplate.gradle`, `settingsTemplate.gradle`;
- `Assets/Resources/SwitchbackPathRay.mat`;
- `EditorBuildSettings.asset`, `GvhProjectSettings.xml`, the Vivox `Settings.json`, `URPProjectSettings.asset`.

What has been checked:
- **Confirmed:** those five `.cs` files are tracked with no `.meta`, and 23 tracked `Assets/` files lack one (some are legitimate `~` folders and `.bundle` contents).
- **Suspected, from the history:**
  - the Configure prefabs are written by `author_menu_configure.py`, `author_arena_launch_panel_layout.py`, `author_ai_difficulty_row.py` and `fix_domain_picker_wiring.py`;
  - `EditorBuildSettings.asset` is written by at least seven `author_*_assets.py` (redline, breakwater, switchback, bloomrush, waystation, skein, salvo);
  - the EDM4U files were last committed in #1656 (Shombith, 2026-10-31);
  - the Vivox settings are committed as an empty dictionary;
  - URP holds `m_LastMaterialVersion: 10`.

None of the suspected causes have been seen in a real Unity open yet.
