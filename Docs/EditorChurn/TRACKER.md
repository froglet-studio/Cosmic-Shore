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
| 1 | `Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourseFeatures.cs.meta` | missing-meta | Unity asset import; the `.cs` was committed by a session with no Unity running | first import on each machine | **damage**: each machine invents its own random GUID | `.meta` committed upstream by #2169 (2026-11-17) with its own GUID; W2's deterministic copy was dropped at the merge so the upstream GUID stands. The upstream file is the two-line form (`fileFormatVersion` + `guid`, no `MonoImporter` block): suspected that Unity expands it on first import (row 20) | `check_meta_files.py` (built; not yet in `gates.sh`, W7) | root-caused | #2169 |
| 2 | `Assets/_Scripts/Controller/Vessel/IJacksRaceCoach.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | as row 1 | as row 1 | root-caused | #2169 |
| 3 | `Assets/_Scripts/Controller/Vessel/JacksRacePlan.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | as row 1 | as row 1 | root-caused | #2169 |
| 4 | `Assets/_Scripts/Tests/Editor/RegattaCourseFeaturesTests.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | as row 1 | as row 1 | root-caused | #2169 |
| 5 | `Assets/_Scripts/Tests/Editor/SkeinRailSeekTests.cs.meta` | missing-meta | as row 1 | as row 1 | **damage**: as row 1 | as row 1 | as row 1 | root-caused | #2169 |
| 6 | `Assets/_Prefabs/ArcadeGameConfigureModal.prefab` | generator-yaml | `author_ai_difficulty_row.py`, `author_menu_configure.py`, `fix_domain_picker_wiring.py` | Unity re-serializes on open or save (unproven) | unknown until a diff is seen; this prefab carried the nulled-reference domain-picker bug (`CLAUDE.md` § Shared prefabs) | W3 | W3 canonical `--check` (not built) | seen | |
| 7 | `Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureArenaView.prefab` | generator-yaml | `author_menu_configure.py` (the plan also names `author_arena_launch_panel_layout.py`; the classifier does not find it naming this file) | as row 6 | unknown until a diff is seen | W3 | W3 | seen | |
| 8 | `Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureMaelstromView.prefab` | generator-yaml | `author_menu_configure.py` | as row 6 | unknown until a diff is seen | W3 | W3 | seen | |
| 9 | `Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureView.prefab` | generator-yaml | `author_ai_difficulty_row.py`, `author_menu_configure.py`, `author_menu_toyconfigure.py`, `author_ui_lattice.py` | as row 6 | unknown until a diff is seen | W3 | W3 | seen | |
| 10 | `ProjectSettings/EditorBuildSettings.asset` | generator-yaml | 19 `Tools/Build` scripts name it, `arcade_mode_lib.py` and the `author_*_assets.py` family among them (the plan counted 7) | a generator run, then Unity re-serializes; Build Profiles may also write it | noise, unless a scene list is lost | W3 (one writer in `arcade_mode_lib.py`); on Windows also row 21's line endings | W3 | seen | |
| 11 | `Assets/Resources/SwitchbackPathRay.mat` | editor-on-load | **proven** (harness, caleb-windows 2026-11-19): URP's material upgrader. The committed file had no `AssetVersion` sub-asset, so the first import added one (`version: 10`) and re-sorted `m_Floats`. Open 2 left it alone. aradia1's unmerged `Charles-Land` (2026-11-06) carries the same upgrade from another machine, with a different random sub-asset fileID, so this is not one PC's quirk. It is the only one of the 244 tracked materials on a URP shader without that stamp (187 have it). The Play-mode `WorldColorTrace.Paint` suspicion was not needed to explain it. | first import (no Play mode) | noise | commit the upgraded form Unity wrote (byte-identical to the measured file, sha256 `fef4b185`) | none yet: a check that every URP-shader `.mat` carries the stamp (W6) | root-caused | `claude/editor-churn-measure-1` |
| 12 | `Assets/Plugins/Android/mainTemplate.gradle` | package-settings | EDM4U Android resolver | auto-resolve on open or on a platform switch (unproven) | noise unless a dependency changes | W4: commit the settled output once, resolve on demand | W4 | seen | |
| 13 | `Assets/Plugins/Android/settingsTemplate.gradle` | package-settings | EDM4U Android resolver | as row 12 | as row 12 | W4 | W4 | seen | |
| 14 | `ProjectSettings/GvhProjectSettings.xml` | package-settings | EDM4U settings (last committed in #1656, Shombith, 2026-10-31, per the plan) | as row 12 | noise | W4 | W4 | seen | |
| 15 | `ProjectSettings/Packages/com.unity.services.vivox/Settings.json` | package-settings | Vivox package settings; committed as `{"m_Dictionary": {"m_DictionaryValues": []}}` (checked at `26c275a19`) | Vivox settings provider on open (unproven) | noise | W4 | W4 | seen | |
| 16 | `ProjectSettings/URPProjectSettings.asset` | package-settings | URP material upgrader; committed with `m_LastMaterialVersion: 10` (checked at `26c275a19`) | URP upgrade pass on open (unproven) | noise | W4 | W4 | seen | |
| 17 | `Assets/.DS_Store`, `Assets/_Scripts/Controller/.DS_Store` | per-machine | macOS Finder; both are **tracked** at `26c275a19` | Finder opens the folder on a Mac | noise | stop tracking (`git rm --cached` + `.gitignore`) after the build check | W7 | suspected | |
| 18 | `Assets/StreamingAssets.meta` | missing-meta (orphan) | Unity's asset database: the folder holds no tracked file, so git never creates it and a fresh clone has the `.meta` with no folder | first open of a fresh clone: Unity deletes an orphaned `.meta`, or recreates it with a new GUID if something makes the folder (inferred, not seen) | **damage** if anything references the folder's GUID; noise otherwise | commit a `.gitkeep` in the folder (Unity ignores dotfiles) once a real open confirms the churn | `check_meta_files.py` (orphan) | suspected | |
| 19 | `Assets/Plugins/FMOD/Cache/Editor.meta` | missing-meta (orphan) | as row 18; FMOD writes `FMODStudioCache.asset` (gitignored) into this folder on open, so the folder reappears | as row 18 | as row 18 | as row 18 (FMOD's folder; the cache asset stays ignored) | `check_meta_files.py` (orphan) | suspected | |
| 20 | the five `.cs.meta` of rows 1-5, as committed by #2169 | missing-meta | Unity asset import rewriting a two-line `.meta` into the full `MonoImporter` form | first import on each machine | probably none: 753 tracked `.cs.meta` already have the short form and none is in Caleb's 16-file list, so Unity 6 seems to leave it alone | nothing until a real open shows it change | `check_meta_files.py` does not yet flag the short form | suspected | |
| 21 | `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset` (and row 11's file) | line-endings | **proven** (harness, caleb-windows): Git for Windows' system `core.autocrlf=true` plus `* text=auto` checks these out as CRLF, and Unity re-saves them as LF with the same content | first open on Windows | noise: shows modified with no diff | candidate: `eol=lf` for Unity YAML in `.gitattributes`, so the checkout already is what Unity writes (decision, W4) | none yet | seen | |

**Tracked `Assets/` files with no `.meta`, measured at `26c275a19`: 22** (the plan said 23 at `4d580c31`):
the five scripts in rows 1-5; the two `.DS_Store` in row 17; 12 files inside FMOD's macOS `.bundle` folders and 2 in
`DeviceLogAndroid~/`, which Unity never imports (legitimate, to be allow-listed by rule in W2); and
`Assets/Samples/Shader Graph/12.1.6/Procedural Patterns/.sample.json`, a dotfile Unity ignores (legitimate).

## Workstreams

| W | what | status | branch | PR | next |
|---|---|---|---|---|---|
| W1 | Reproduce and measure: `editor_churn.py` (classify + measure harness) | merged 2026-11-16; self-test and the harness's own controls pass; **not yet run against a real Unity** | `claude/editor-churn-w1-harness` | #2027 | run `measure` on a fresh clone with Unity 6000.3.17f1 (Windows and macOS) and record the first real measurement |
| W2 | Missing and forked `.meta` files | `check_meta_files.py` built (`--self-test`, `--fix`); rows 1-5 landed upstream in #2169 first (W2's copies dropped); rows 18-19 and 20 found, left for a real Unity open | `claude/editor-churn-w2-meta` | | wire `check_meta_files.py --tracked` into `gates.sh` (W7, Shombith's queue); confirm rows 18-19 in a real open, then `.gitkeep` them |
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
| 2026-11-19 | caleb-windows | Windows 11 | 6000.3.17f1 | `f160af0f7` | fresh (no Library) | 3 | 3 | 0 | harness |

First harness run against a real Unity: 2026-11-19 on caleb-windows (rows 11 and 21). Both opens compiled; open 2 changed nothing, so nothing on this machine fails to settle. Of the hand list's 16 files, only rows 10 and 11 showed up; the rest need a trigger a plain open does not pull (a generator run, Play mode, a package resolve) or a Mac. Before that, the cloud container that built W1 had no Unity. The harness was checked with
stand-in editors (one writing churn shaped like row 1, 12 and 14, one writing nothing): it found 4 dirty after open
1 and 1 file changed again by open 2 (the never-settling writer), exit 1; and 0 and 0, exit 0, for the quiet one.

## Intake log

| date | whose | OS / Unity | what was brought | rows |
|---|---|---|---|---|
| 2026-11-12 | Caleb | ? / ? | a list of 16 file names (no `churn.patch`) | 1-16 (17 found while measuring missing metas) |
| 2026-11-19 | Caleb (harness on his PC) | Windows 11 / 6000.3.17f1 | `editor_churn.py measure`, 2 opens, fresh clone at `f160af0f7`; logs kept on his PC | 10, 11, 21 |

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
- **2026-11-16 (W2 thread, Claude for Caleb):** `--fix` writes a `.meta` only where the form is exact (folders,
  `.cs`, `.asmdef`, `.asmref`), with GUID = md5("cosmic-shore:" + path), so two sessions fixing the same file write
  the same GUID. The five scripts of rows 1-5 are an enum/struct/plain-class file, an interface, a struct and two
  NUnit fixtures: nothing serialized can reference them, so no existing GUID had to be kept (checked: no tracked
  file names any of the five new GUIDs, and no branch fetched 2026-11-13 carries a `.meta` for them).
- **2026-11-16 (W2):** the hidden-asset rules (`.`-prefixed, `~`-suffixed, `cvs`, `.tmp`, bundle contents) cover
  every other tracked `Assets/` file without a `.meta`; the two tracked `.DS_Store` (row 17) are hidden by rule and
  stay a separate per-machine row.
- **Open:** `check_meta_files.py --tracked` is red on rows 18-19 until they are fixed, so wiring it into `gates.sh`
  waits for them (or for an allow-list Shombith agrees to).
- **Open:** the plan counted 23 tracked `Assets/` files without a `.meta` at `4d580c31`; 22 at `26c275a19`.
