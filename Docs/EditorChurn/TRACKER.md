# Editor Churn: the tracker

The source of truth for where the Editor Churn project stands. The plan (`EDITOR_CHURN_PLAN.md`) says *what* to do;
this file says *where it stands*. When the two disagree, this file wins, and the thread that finds the difference
records it under Decisions.

**Done when** a fresh clone of `bleeding-edge`, opened in Unity once and then twice, leaves `git status` clean on
Windows and macOS, and gates stop a new case at commit time.

**Statuses** keep to what was really checked: `suspected` (inferred from code or history), `seen` (a person's list,
a diff or a harness run showed it change), `root-caused` (the writer is proven), `fixed` (cause removed AND a gate
holds it), `verified-in-Unity` (a real Unity open after the fix left it clean). Never mark a row further than the
evidence.

**Tools.** `python3 Tools/Build/editor_churn.py classify` sorts a checkout's churn (or a person's
`churn-status.txt` + `churn.patch`) into categories with the likely writer; `... measure --project <clean clone>` is
the harness (open in Unity once, then twice, capture each). `--self-test` holds both. `--md` prints rows in this
table's shape.

## File table

Categories are `editor_churn.py`'s: missing-meta, generator-yaml, package-settings, version-skew, line-endings,
per-machine, editor-on-load, unknown. "Writer" is the classifier's candidate list unless it says proven.

| # | path | category | writer | trigger | harm | fix | gate | status | commit |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourseFeatures.cs.meta` | missing-meta | Unity asset import; the `.cs` was committed by a session with no Unity running | first import on each machine | **damage**: each machine invents its own random GUID | W2: commit the `.meta` with the GUID existing references use | W2 `check_meta_files.py` (not built) | seen | |
| 2 | `Assets/_Scripts/Controller/Vessel/IJacksRaceCoach.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | W2 | W2 | seen | |
| 3 | `Assets/_Scripts/Controller/Vessel/JacksRacePlan.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | W2 | W2 | seen | |
| 4 | `Assets/_Scripts/Tests/Editor/RegattaCourseFeaturesTests.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | W2 | W2 | seen | |
| 5 | `Assets/_Scripts/Tests/Editor/SkeinRailSeekTests.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | W2 | W2 | seen | |
| 6 | `Assets/_Prefabs/ArcadeGameConfigureModal.prefab` | generator-yaml | `author_ai_difficulty_row.py`, `author_menu_configure.py`, `fix_domain_picker_wiring.py` | Unity re-serializes on open or save (unproven) | unknown until a diff is seen; this prefab carried the nulled-reference domain-picker bug (`CLAUDE.md` § Shared prefabs) | W3 | W3 canonical `--check` (not built) | seen | |
| 7 | `Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureArenaView.prefab` | generator-yaml | `author_menu_configure.py` (the plan also names `author_arena_launch_panel_layout.py`; the classifier does not find it naming this file) | as row 6 | unknown until a diff is seen | W3 | W3 | seen | |
| 8 | `Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureMaelstromView.prefab` | generator-yaml | `author_menu_configure.py` | as row 6 | unknown until a diff is seen | W3 | W3 | seen | |
| 9 | `Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureView.prefab` | generator-yaml | `author_ai_difficulty_row.py`, `author_menu_configure.py`, `author_menu_toyconfigure.py`, `author_ui_lattice.py` | as row 6 | unknown until a diff is seen | W3 | W3 | seen | |
| 10 | `ProjectSettings/EditorBuildSettings.asset` | generator-yaml | 19 `Tools/Build` scripts name it, `arcade_mode_lib.py` and the `author_*_assets.py` family among them (the plan counted 7) | a generator run, then Unity re-serializes; Build Profiles may also write it | noise, unless a scene list is lost | W3 (one writer in `arcade_mode_lib.py`) | W3 | seen | |
| 11 | `Assets/Resources/SwitchbackPathRay.mat` | unknown | **suspected**: runtime `SwitchbackPathRays.SharedMaterial()` loads the shared asset with `Resources.Load` and calls `WorldColorTrace.Paint` on it; in Play mode in the editor that changes the asset, which the editor then saves. No generator names it. | Play mode (inferred from code, not seen) | noise (colour values) | paint an instance, not the asset (owner: Switchback's) | W6-style check (not built) | seen | |
| 12 | `Assets/Plugins/Android/mainTemplate.gradle` | package-settings | EDM4U Android resolver | auto-resolve on open or on a platform switch (unproven) | noise unless a dependency changes | W4: commit the settled output once, resolve on demand | W4 | seen | |
| 13 | `Assets/Plugins/Android/settingsTemplate.gradle` | package-settings | EDM4U Android resolver | as row 12 | as row 12 | W4 | W4 | seen | |
| 14 | `ProjectSettings/GvhProjectSettings.xml` | package-settings | EDM4U settings (last committed in #1656, Shombith, 2026-10-31, per the plan) | as row 12 | noise | W4 | W4 | seen | |
| 15 | `ProjectSettings/Packages/com.unity.services.vivox/Settings.json` | package-settings | Vivox package settings; committed as `{"m_Dictionary": {"m_DictionaryValues": []}}` (checked at `26c275a19`) | Vivox settings provider on open (unproven) | noise | W4 | W4 | seen | |
| 16 | `ProjectSettings/URPProjectSettings.asset` | package-settings | URP material upgrader; committed with `m_LastMaterialVersion: 10` (checked at `26c275a19`) | URP upgrade pass on open (unproven) | noise | W4 | W4 | seen | |
| 17 | `Assets/.DS_Store`, `Assets/_Scripts/Controller/.DS_Store` | per-machine | macOS Finder; both are **tracked** at `26c275a19` | Finder opens the folder on a Mac | noise | stop tracking (`git rm --cached` + `.gitignore`) after the build check | W7 | suspected | |

**Tracked `Assets/` files with no `.meta`, measured at `26c275a19`: 22** (the plan said 23 at `4d580c31`):
the five scripts in rows 1-5; the two `.DS_Store` in row 17; 12 files inside FMOD's macOS `.bundle` folders and 2 in
`DeviceLogAndroid~/`, which Unity never imports (legitimate, to be allow-listed by rule in W2); and
`Assets/Samples/Shader Graph/12.1.6/Procedural Patterns/.sample.json`, a dotfile Unity ignores (legitimate).

## Workstreams

| W | what | status | branch | PR | next |
|---|---|---|---|---|---|
| W1 | Reproduce and measure: `editor_churn.py` (classify + measure harness) | built; self-test and the harness's own controls pass; **not yet run against a real Unity** | `claude/editor-churn-w1-harness` | | run `measure` on a fresh clone with Unity 6000.3.17f1 (Windows and macOS) and record the first real measurement |
| W2 | Missing and forked `.meta` files | not started | | | ships first: rows 1-5, `check_meta_files.py` |
| W3 | Generators write canonical YAML | not started | | | rows 6-10, once a real diff shows what Unity changes |
| W4 | Package auto-writers (EDM4U, Vivox, URP) | not started | | | rows 12-16 |
| W5 | Version skew and line endings | not started | | | |
| W6 | Editor code that writes on load | not started | | | row 11 (a Play-mode write, not on load) belongs nearest here |
| W7 | Gates | not started | | | Shombith's `/approve` |
| W8 | The ruleset `Docs/claude/EDITOR_CHURN.md` | not started | | | |
| W9 | The hand test (Windows + macOS) | not started | | | one `/team-tasks` ticket, suggested only |

## Measurements

| date | machine | OS | Unity | commit | Library | dirty after open 1 | dirty after last open | changed by open 2 | source |
|---|---|---|---|---|---|---|---|---|---|
| 2026-11-12 | Caleb's | ? | 6000.3.17f1 (pinned; not confirmed) | `4d580c31` | ? | 16 | n/a | n/a | hand list (no patch) |

No harness run against a real Unity yet: the cloud container that built W1 has no Unity. The harness was checked with
stand-in editors (one writing churn shaped like row 1, 12 and 14, one writing nothing): it found 4 dirty after open
1 and 1 file changed again by open 2 (the never-settling writer), exit 1; and 0 and 0, exit 0, for the quiet one.

## Intake log

| date | whose | OS / Unity | what was brought | rows |
|---|---|---|---|---|
| 2026-11-12 | Caleb | ? / ? | a list of 16 file names (no `churn.patch`) | 1-16 (17 found while measuring missing metas) |

## Decisions and open questions

- **2026-11-13 (W1 thread, Claude for Caleb):** the classifier keeps the plan's eight categories. When nothing else
  names a file, it adds a hint, not a category: runtime C# that names the asset ("runtime code?"), for the
  Play-mode write shape row 11 suggests.
- **2026-11-13 (W1):** `measure` snapshots the working tree into git tree objects through a private index
  (`GIT_INDEX_FILE`), so it reports what each open changed on top of the last without touching the checkout's index
  or files. It refuses a dirty checkout unless `--allow-dirty`.
- **Open:** the "editor churn / repo hygiene" lane is not in `.claude/skills/swimlanes/lanes.json` yet. Editing it
  collides with Shombith's occupied "Claude skills, hooks and editor tools" lane (PR #1916, still open on
  2026-11-13). Add it once #1916 lands.
- **Open:** `verify_unity.sh` (which `measure` runs) finds the editor at `/opt/unity/<ver>/Editor/Unity` (Linux) and
  uses `timeout` and `pgrep`. On macOS set `UNITY=/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/MacOS/Unity`
  and install coreutils' `timeout`; on Windows run it from Git Bash with `UNITY=".../Editor/Unity.exe"`. Not tried on
  either yet.
- **Open:** which OS and Unity version Caleb's 2026-11-12 list came from.
- **Open:** the plan counted 23 tracked `Assets/` files without a `.meta` at `4d580c31`; 22 at `26c275a19`.
