# Test plan before pushing `cece/nice-brahmagupta-ojclij` (2026-10-12)

This branch holds thirteen independent contributions ("parts" A to M), made between 2026-10-09 and
2026-10-12, including the evolution work merged in from `cece/friendly-goodall-woddjw`. Each part was
reviewed on its own by an independent reviewer with two adversarial verifiers; the 58 confirmed
findings were then fixed on this branch, each fix set reviewed again adversarially (section 4 lists
the fixes per part). Nothing here has been run in the Unity Editor: no `unity` CLI was available,
so the `/verify-unity` gate did not run for any C# change on this branch. This document is what to test there, part by part, and how to push only the parts
that pass.

- **Section 1** sets up the editor once.
- **Section 2** lists what was already proven without Unity.
- **Section 3** is the parts at a glance: risk, benefit, what a push needs.
- **Section 4** is one block per part: what it does, benefits, risks, and the editor test steps.
- **Section 5** is the selective push: `Tools/Build/pick_branch_parts.py`.
- **Section 6** lists what needs a person rather than a test.

Every test step has a **PASS** line and a **FAIL** line. Record a FAIL with the step number, what
you saw, and the Console's first red line.

## 1. Set up once

1. `git fetch origin && git checkout cece/nice-brahmagupta-ojclij`. The branch already contains
   `bleeding-edge` as of 2026-10-12 (`cadc7012a`).
2. Open the project in Unity 6000.3.17f1 and wait for the import and compile to finish. Open the
   Console, click **Clear**, and wait for the next domain reload. **PASS:** 0 red errors.
   **FAIL:** any compile error; note the file and line.
3. **Window > General > Test Runner > EditMode > Run All.** `bleeding-edge` itself already fails 35
   EditMode tests in 14 suites (Docs/QA/DEV_TASKS.md DT-003, measured 2026-10-12 by QA): FollowHeightScale,
   RaceRankToastDriver, RearViewLaw, SparrowCombatTier, PartyInviteSystem, ElementalScalingUnification,
   WeeklyChallenge, EnumIntegrity, LifeformHeartSize, PrismLit, RampageIntensityLadder,
   ScreenshotFraming, ShieldedSignalColor and ThresherChainSolver. Those are not this branch's.
   **PASS:** no red test outside those 14 suites, and every suite this branch adds or changes
   appears and is green: `BounceNoticeDeliveryTests`, `FriendNameResolverTests`,
   `IncomingInviteInboxTests`, `LifeformGenomeTests`, `LobbySlotRowReadyTests`,
   `RecentPlayersStoreTests`, `ReplayFileTests`, `SkimRaceAITests`, `VesselEffectCooldownsTests`,
   `LoadInsightsAttributionTests` and `LoadSweepTableTests`. Each part's own steps give the expected
   counts. **FAIL:** a red test in any other suite, or one of those suites missing (it did not
   compile into the editor assembly).
4. **Before any Play-mode test:** Play-mode edits to a ScriptableObject persist on disk, and several
   steps below touch shipped assets (AI training's Archive and SessionState, Skim Race AI configs,
   cell configs). When you finish, run `git status` and discard every asset change you did not mean
   to make with `git checkout -- <path>`. Never commit them from a test session.
5. Part J's multiplayer steps need **two machines or two Unity instances signed into two different
   UGS accounts** (ParrelSync or a second clone). Mark them "needs two accounts" if you cannot.

## 2. Already proven without Unity

Measured on the final branch head, 2026-10-12. "Reference compile" is
`Tools/Build/unity_refcompile/run.sh`: the project's C# compiled against Unity 6's real reference
assemblies and package sources, in the player (IL2CPP release) and editor configurations. "Prisma"
is the repository's own engine under `Port/`, which compiles `Assets/_Scripts` live and runs the game
headless. None of this replaces the editor: it proves the code compiles and the logic runs, not that
scenes, prefabs and the Inspector behave.

_Pending: the final gate run on the integrated head._

## 3. The parts at a glance

| Part | What | Ships to players | Risk | Needs | Push |
|---|---|---|---|---|---|
| A | CI gates and offline audit tools | No. CI, Python tools and one Prisma test. | Low | - | First, alone or with anything. |
| B | Repo hygiene: untrack FMOD's cache, PR template, hygiene report | No. | Low for programmers; needs the sound designer first | - | After the sound designer opens `Cosmic Shore.fspro`, saves and builds, and commits (section 6). |
| C | Steam dual-app uploader and Playtest runbook | No. Release tooling and docs. | Low | - | Any time. |
| D | Plans and docs only | No. | Low | - | Last, with or after the parts it describes. |
| E | AI training fixes and the deployed tournament genome | Yes, one behaviour: Skim Race and Regatta AI after a Hangar practice game. | Medium | - | After its editor steps pass. |
| F | Skim Race AI intensity-4 policy v2-i4 | Yes: every AI Squirrel in Skim Race (and Regatta) at intensity 4. | Low | - | After its editor steps pass. |
| G | Load-time target and unattended load sweep (R7) | Instrumentation only: span markers around existing boot and connecting-panel awaits; the sweep is dev-build only. | Medium | E, M | With E. |
| H | Parity harness, Unity side | Hooks that do nothing unless the capture tool or an environment variable turns them on. | Medium | M | After M has landed, then I after H. |
| I | Prisma engine (Port/ only) | No. Unity never compiles Port/. | Low | H, M | After H. |
| J | Multiplayer QoL: friends, recent players, ready lights, dropping out, invite inbox | Yes: Menu_Main's friends panels, the lobby, the party layer. | Medium | - | After the two-account steps pass. |
| K | Leak and lifetime fixes | Yes: effect cooldown tables, score trackers, four delayed tasks. | Low | - | Any time after its steps pass. |
| L | Heritable genome (evolution), off by default | One always-on change: 4 extra bytes in the fauna network spawn payload. Everything else is off unless a biome enables it. | Low (off), with one network caveat | - | After its steps pass and the LOCKED rule is signed off. |
| M | Prisma engine APIs the parity probe compiles against | No. Port/ only. | Very low | - | Any time; first of M, H, I. |

## 4. The parts

### Part A: CI gates and offline audit tools

| | |
|---|---|
| Commits | 12: 8c6952942 a70e2cd49 d0405a4fd ea7b82e1b 1c5753e22 e81cf407e e8f7a5557 cc81cf2d5 c4e928ca9 1cb1e81ce 68d502b42 a8b1dc65e |
| Ships to players | No. CI, Python tools and one Prisma test. |
| Risk | Low |
| Needs | None. Every other part's CI is red without it (see section 5). |
| Push | First, alone or with anything. |

**What it does.** Un-reds `bleeding-edge`'s guard: `Tools/CI/validate_project.py` stops flagging `UnityEditor` inside Port/ and inside string literals (now with a real one-line C# lexer, so a reference inside an interpolation hole is still caught). Both static CI jobs gain three gates: every asset generator's `--check` (30 generators, more than 660 generated files), the AI/replay input-only scan, and the self-tests of two offline audit twins. The twins, `Tools/Build/audit_vessel_skimmers.py` and `audit_lifeform_crystals.py`, reproduce the editor's Audit Vessel Skimmers and Validate Lifeform Crystals without Unity and print the reason for every fault (DT-002's six skimmer hulls, 32 lifeform findings).

**Benefit.** The guard has failed at "Validate project" on every `bleeding-edge` push since 2026-10-08 (runs #391 to #406), and every later static step was skipped, so nothing else was checked for four days. With A, the whole static job passes again (measured by running its steps on `bleeding-edge` + A). The generator gate closes the failure class CLAUDE.md records ("six of eight mode generators were red and nobody was reading them"): a planted one-number drift in a generated asset now fails CI.

**Risks.**

- The generator gate compares generated assets byte for byte. From now on an Inspector edit to any of the 660+ generator-owned assets (an arcade card, a cell config, a Skim Race AI config, a lifeform heart) turns `bleeding-edge` red; so does Unity reserializing them after a new serialized field or an editor upgrade. *High likelihood, and intended. It changes the team's workflow: designers change the generator, not the asset. Announce it with the push.*
- The audit self-tests read live game assets as fixtures. A gameplay change that moves one (Squirrel's skimmer, the Physarum config) fails the guard. *Medium. After the fix a moved fixture prints a one-line 'fixture moved' failure instead of a traceback.*

**Fixed after review.**

- `validate_project.py` lexes C# literals, so `UnityEditor` inside an interpolation hole, after a verbatim string ending in a backslash, or after a `'"'` char literal is caught again (three negative controls added).
- The audit twins' self-tests use synthetic fixtures, so a gameplay asset change cannot turn CI red; the skimmer twin reads hulls in subfolders and Prefab Variants and lists every prefab it cannot audit instead of dropping it.
- The bleeding-edge guard's autofix prompt lists every new gate.
- The Prisma AssetTool test computes the majority it expects from the donor metas.

**Test steps.**

<!-- steps:A -->
1. **Terminal battery (no Unity).** Use a FULL checkout (a sparse one without PNG/FBX makes author_hijack, author_switchback and author_lifeform_heart_sizes fail for missing files). Run `git status` and confirm it is clean, then from the repo root paste:
   `python3 Tools/CI/test_validate_project.py && python3 Tools/CI/validate_project.py && python3 Tools/Build/audit_vessel_skimmers.py --self-test && python3 Tools/Build/audit_vessel_skimmers.py && python3 Tools/Build/audit_lifeform_crystals.py --self-test && python3 Tools/Build/audit_lifeform_crystals.py && python3 Tools/Build/check_ai_no_state_writes.py --self-test && python3 Tools/Build/check_ai_no_state_writes.py --check && python3 Tools/Build/arcade_mode_lib.py --self-test && for f in Tools/Build/author_*_assets.py Tools/Build/author_skimrace_ai_config.py Tools/Build/author_lifeform_heart_sizes.py; do python3 "$f" --check || { echo "GATE RED: $f"; break; }; done`
   - **PASS:** it finishes in about 2 minutes, and the output contains all of these:
     - `34/34 passed` and `PASSED: nothing that blocks a build.`
     - From the skimmer self-test: a `live tree: 6 fault(s) ... (informational, not asserted)` line, 15 lines starting `ok`, then `self-test: OK`.
     - From the skimmer report: 14 hull blocks, then `Not audited (no VesselStatus on the prefab root; the editor skips these too):` with exactly nine `-- Components/...: no VesselStatus found` lines, then `6 fault(s); standing exceptions: Serpent`.
     - From the lifeform self-test: 9 lines starting `ok`, then `self-test: OK`. The lifeform report ends with `32 warning(s)`.
     - `self-test OK`, `ai-no-state-writes: OK`, and `self-test: OK` from arcade_mode_lib.
     - Every generator's last line says OK, clean or match, and no line says `GATE RED`.
   - **FAIL:** any line starting `FAIL`, `FAILED`, `DRIFT`, `Traceback` or `GATE RED`, or a fault count other than 6 or a warning count other than 32 (if so, the tree changed since this review: report it).

2. **The editor-in-runtime gate catches a reference in an interpolation hole (negative control).** In a terminal, create `Assets/_Scripts/ProbeInterp.cs` containing exactly `public static class ProbeInterp { public static string S() => $"playing={UnityEditor.EditorApplication.isPlaying}"; }`. Run `python3 Tools/CI/validate_project.py; echo exit=$?`. Delete the file (and `Assets/_Scripts/ProbeInterp.cs.meta` if Unity was open and made one), then run the same command again.
   - **PASS:** the first run prints `editor-in-runtime: 1 error(s)` naming `Assets/_Scripts/ProbeInterp.cs:1`, then `FAILED: ...` and `exit=1`. The second run prints `PASSED: nothing that blocks a build.` and `exit=0`.
   - **FAIL:** the first run says `editor-in-runtime: clean` (the hole is still hidden), or the second run still fails (check `git status`; never commit the probe file).

3. **CI dry run with no side effects.** Push the selective-push branch. On GitHub open Actions ▸ Unity CI ▸ Run workflow, set 'Use workflow from' to the branch and Tier to `static`, and run it. Open the `conditional-compilation` job. Do NOT use Run workflow on the Bleeding-edge guard for a feature branch: on failure it opens a '<branch> is broken' issue.
   - **PASS:** the job is green, and these steps ran green: 'Audit vessel skimmers (offline)' (its log shows `self-test: OK` twice and the `Not audited` list), 'Check the AI and replay trees never write vessel state', and 'Check every asset generator against disk'. The job finishes in under about 5 minutes.
   - **FAIL:** the job is red, one of those steps is missing or skipped, or the job times out.

4. **Unity: Audit Vessel Skimmers against the twin.**
   1. Open the project and click the Console's Clear button.
   2. Run **FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers**. When the dialog 'N fault(s) found ... File in Bug Ledger?' appears, click **Skip** (File writes issue files into the repo).
   3. Open the Console message that starts 'Vessel skimmer audit'.
   4. In a terminal run `python3 Tools/Build/audit_vessel_skimmers.py` and compare hull by hull.
   - Differences that do not count: the editor names the slots NearFieldSkimmer/FarFieldSkimmer and the script names them `_nearFieldSkimmer`/`_farFieldSkimmer`. The script's trailing `Not audited` list of nine Components/ prefabs has no editor counterpart, because the editor skips those silently.
   - **PASS:** both say 6 faults, with these `***` lines:
     - Grizzly and Sparrow: `container 'SparrowSkimmerImpactorDataContainer' has no prism effects`.
     - Scarab: `container 'ScarabSkimmerImpactorDataContainer' has no prism effects`.
     - Rhino: `no trigger collider`.
     - Urchin: `SkimmerImpactor has no effect container`.
     - Serpent: `'VacuumSkimmer' is INACTIVE; no SkimmerImpactor; no ImpactCollider ...; no Rigidbody ...`.
     - Both say OK for Butterfly (its far field 'crystal pickup only'), Dolphin, Manta, Squirrel and Thresher, and '(none)' for Falcon, Shrike and Termite.
   - **FAIL:** a hull is in one list and not the other, a reason differs, or the totals differ. Paste both lists into DT-002 (Docs/QA/DEV_TASKS.md).

5. **Unity: Validate Lifeform Crystals against the twin.** Click the Console's Clear button. Run **FrogletTools ▸ Validation ▸ Validate Lifeform Crystals** (the menu is Validation, not Ecology). Type `[LifeFormCrystal]` in the Console search box and count the warnings. In a terminal run `python3 Tools/Build/audit_lifeform_crystals.py` and compare.
   - **PASS:** the editor shows exactly 34 `[LifeFormCrystal]` warnings:
     - One for `Assets/_Prefabs/FloraAndFauna/Populations/Qfish.prefab` ('lifeform has NO crystal').
     - The summary '1 issue(s) across 31 lifeform prefab(s)'.
     - 31 'has no Variant tuning enabled' warnings for the same 31 config paths the script prints (6 Swarm Cell, WormColonyFaunaConfig, the NCA lizard, 10 Substrate, 13 Swarm Fauna/Bench).
     - The summary '31 config(s) of 210 will render the platform default heart'.
     - The script prints the same 32 finding lines plus '31 lifeform prefab(s)' and '210 lifeform config(s) checked'.
   - **FAIL:** different paths, or different prefab or config counts. Console warnings that do not start with `[LifeFormCrystal]` do not count.

6. **The generator gate catches an Inspector edit.** In the Project window select `Assets/Resources/SkimRaceAIConfig_I4.asset`, change any number in the Inspector, and use File ▸ Save Project. In a terminal run `python3 Tools/Build/author_skimrace_ai_config.py --check`. Undo with `git checkout -- Assets/Resources/SkimRaceAIConfig_I4.asset` and run the same `--check` again.
   - **PASS:** the first run prints `DRIFT: Assets/Resources/SkimRaceAIConfig_I4.asset` and `--check FAILED: 1 asset(s) drifted`. After the undo it prints `--check OK`.
   - **FAIL:** the first run says OK (the gate cannot see an Inspector edit), or the second still reports drift (check `git status`).

7. **Prisma AssetTool tests (optional, no Unity).** From the repo root run `dotnet test Port/tests/CosmicShore.AssetTool.Tests -c Release`. This writes ignored bin/obj folders under Port/. It takes about 50 s on a cold build and about 12 s warm.
   - **PASS:** `Passed! - Failed: 0, Passed: 25`.
   - **FAIL:** any failed test, especially `A_new_model_gets_the_projects_import_settings_and_loads_inside_a_prefab`. If its message says 'two donor metas at the newest importer version tie on length', a donor meta changed; pick donors per the test's comment.

8. **The autofix prompt lists every gate.** Open `.github/workflows/bleeding-edge-guard.yml` and search for `Reproduce first`.
   - **PASS:** the list includes:
     - `test_validate_project.py` and the four other validator `--self-test` lines.
     - `check_console_logging.py --self-test` and plain `check_console_logging.py`.
     - `audit_vessel_skimmers.py --self-test` and `audit_lifeform_crystals.py --self-test`.
     - `check_ai_no_state_writes.py --self-test` and `--check`.
     - `arcade_mode_lib.py --self-test` and the `author_*` loop.
     - The paragraph that starts 'A generator that reports DRIFT, or a self-test that reports FAILED, is not one of the mechanical classes'.
   - **FAIL:** any of those is missing.

9. **After the push.** Open Actions ▸ Bleeding-edge guard ▸ the first run on bleeding-edge after the push.
   - **PASS:** 'Static validation' is green with every step run and none skipped:
     - Self-test the validators, Validate project, Check conditional compilation, Check console logging.
     - The roster scenes and HUD checks.
     - Audit vessel skimmers (offline), the AI/replay gate, the generator loop.
     - The call-to-action, junie and prism shader wiring checks.
     - The open 'bleeding-edge is broken' issue is closed by the Report status job. 'Unity compile' showing skipped is normal (no runner is configured).
   - **FAIL:** red at any step. Red at 'Self-test the validators' or 'Validate project' points at this part's validate_project change; red later is a newly visible problem, so read the step log.
<!-- /steps:A -->

### Part B: Repo hygiene: untrack FMOD's cache, PR template, hygiene report

| | |
|---|---|
| Commits | 2: 001f75452 a2e1025a1 |
| Ships to players | No. |
| Risk | Low for programmers; needs the sound designer first |
| Needs | None. |
| Push | After the sound designer opens `Cosmic Shore.fspro`, saves and builds, and commits (section 6). |

**What it does.** Takes FMOD Studio's per-machine bank cache (`Cosmic Shore/.cache/`, 279 files) and unsaved-state scratch (`Cosmic Shore/.unsaved/`, 2 files) out of git and ignores both. Adds `.github/pull_request_template.md` and `Docs/GITHUB_HYGIENE_REPORT.md` (four decisions left to a person: Git LFS, history rewrite, duplicate source audio, CODEOWNERS).

**Benefit.** The repository stops growing by about 77 MB of binary cache churn per FMOD bank build (the sound designer committed cache changes three times in the two weeks before). Pull requests opened on GitHub get a consistent template whose headings the `/qa-backlog` skill reads. The FMOD project itself (Metadata, source audio, the built banks Unity loads) stays tracked, so runtime audio is unaffected.

**Risks.**

- Pulling B deletes `.cache` and `.unsaved` from every other clone. `.unsaved` currently holds an unsaved 'Squirel boost' mix edit that is newer than the saved FMOD metadata; the hygiene report now records the values. *High on the sound designer's machine: one slow bank rebuild, and the unsaved edit is lost unless saved first.*
- The sound designer's `git pull` can abort over locally modified cache files, and branches carrying FMOD commits get modify/delete conflicts on up to 281 files that GitHub's web editor cannot resolve. *Medium. Unblock with `git rm -r --cached "Cosmic Shore/.cache" "Cosmic Shore/.unsaved"` and commit.*

**Fixed after review.**

- The hygiene report records the unsaved 'Squirel boost' values (Audio 1 +9 dB, Audio 3 0 dB, as in the shipped bank) against the saved +1 dB and -9 dB.
- The PR template's not-run line uses the phrase the `/qa-backlog` scan searches pull request bodies for.

**Test steps.**

<!-- steps:B -->
1. Cheapest check, no Unity needed. In a terminal at the repo root on the branch, run `git ls-files "Cosmic Shore/.cache" "Cosmic Shore/.unsaved" | wc -l`, then `git status`.
   - **PASS:** the first command prints 0, and git status (or GitHub Desktop's Changes tab) lists no path under `Cosmic Shore/.cache` or `Cosmic Shore/.unsaved`.
   - **FAIL:** any number above 0, or any .cache or .unsaved path shown as changed or untracked.
2. In File Explorer or Finder, open `Cosmic Shore/Build/Desktop` at the repo root (outside Assets/).
   - **PASS:** Master.bank, Master.strings.bank and SFX.bank are present, SFX.bank is about 37 MB, and `Cosmic Shore/Build/GUIDs.txt` exists.
   - **FAIL:** any of the four files is missing or 0 KB.
3. Open the project in Unity and wait for the import to finish. Open **FMOD > Edit Settings** and look at the bank source section, then clear the Console.
   - **PASS:** the source is the Studio project at `Cosmic Shore/Cosmic Shore.fspro`, the Master and SFX banks are listed, and no red FMOD error appears ('bank not found', 'could not locate Studio project').
   - **FAIL:** a red FMOD error, an empty bank list, or a Studio project path warning.
4. Open `Assets/_Scenes/Bootstrap.unity` and press Play. Wait for Menu_Main and click a few menu buttons (Arcade, back). Then start any mode with the Squirrel and press boost.
   - **PASS:** menu music plays, button clicks are audible, the Squirrel boost sound plays, and the Console shows no FMOD EventNotFoundException or bank error. Everything sounds the same as on bleeding-edge.
   - **FAIL:** silence where bleeding-edge has sound, or FMOD errors in the Console.
5. Sound designer (aradia1, FMOD Studio 2.02), BEFORE pulling this part. Follow `Docs/GITHUB_HYGIENE_REPORT.md` §1.1. Open `Cosmic Shore/Cosmic Shore.fspro`, open **Events > SFX > Oneshots > Gameplay sfx > Squirel boost**, and read the volume of the tracks **Audio 1** and **Audio 3**. The shipped SFX.bank and the old unsaved state have Audio 1 = +9 dB and Audio 3 = 0 dB; the saved project has +1 dB and -9 dB. The designer chooses the intended values, sets them, does **File > Save** and **File > Build**, and commits only `Metadata/` and `Build/` changes.
   - **PASS:** the designer confirms the intended values, the title bar shows no `*` after saving, and the commit contains no `.cache` or `.unsaved` path.
   - **FAIL:** the designer does not know which values are intended, or the values they want exist only in the unsaved state. Hold the push of this part until they are saved.
6. On the sound designer's machine, pull or merge the pushed branch. Reopen FMOD Studio, run **File > Build** once, and look at git status or GitHub Desktop.
   - **PASS:** the pull completes. The build finishes (slower the first time, because the cache regenerates). Afterwards the only changes are under `Cosmic Shore/Build` or `Metadata`, and no .cache or .unsaved file appears.
   - **FAIL:** the pull stops with 'Your local changes would be overwritten' or with modify/delete conflicts on `Cosmic Shore/.cache/...`. Recover with the report's §1.1 command `git rm -r --cached "Cosmic Shore/.cache" "Cosmic Shore/.unsaved"`, commit, and pull again; never 'keep mine'. The step also fails if .cache files show up as changes after the build.
7. Open `.github/pull_request_template.md` (or, after the push, click **Compare & pull request** on GitHub).
   - **PASS:** the description is pre-filled with five headings (What and why, Systems touched, Verification status, Tool output, Follow-ups), and the Verification status comment's last line reads `- NOT run, untested; verify in editor: <the exact scene, mode and thing to look at>.`
   - **FAIL:** an empty description, different headings, or a NOT-run line without 'untested' and 'verify in editor'. PRs created with gh or the API do not get the template; that is expected.
<!-- /steps:B -->

### Part C: Steam dual-app uploader and Playtest runbook

| | |
|---|---|
| Commits | 2: bc472fc70 5ac73ea4a |
| Ships to players | No. Release tooling and docs. |
| Risk | Low |
| Needs | None. |
| Push | Any time. |

**What it does.** `Tools/Steam/upload.sh` uploads to either Steamworks app by a named target (`--target base` or `--target playtest`), refuses until that target's ids exist and names the checklist item that creates them, refuses when both pairs share an id, and stamps the target into the build description. `Docs/STEAM_PLAYTEST_RUNBOOK.md` covers the Playtest child app, its store page and signups, the wave policy, and the branch model across two apps.

**Benefit.** Closes Steam board row R6, which blocks A4, A5 and E7. Release engineering gets one script for both apps; Valve's Playtest rules quoted in the runbook were re-checked against Valve's pages on 2026-10-12.

**Risks.**

- An upload with `--branch internal` and no `--set-live` lands unassigned, so a smoke test can run against an older build. The docs now say `--branch internal --set-live`. *Medium before the doc fix; low after.*
- The same-id guard only works when both id pairs are exported in the shell. *Low. An unassigned upload to the wrong app reaches nobody until a person publishes it.*

**Fixed after review.**

- Every internal upload in the runbook, `Tools/Steam/README.md` and `Docs/BUILD_AND_DELIVERY.md` is `--branch internal --set-live`; A4 creates the password-protected `internal` branch on the Playtest app first; B5 and B7 run on the Playtest app.

**Test steps.**

<!-- steps:C -->
1. Run `bash -n Tools/Steam/upload.sh && echo SYNTAX_OK`.
   - **PASS:** prints SYNTAX_OK.
   - **FAIL:** a syntax error or no output.
2. Run `./Tools/Steam/upload.sh --help`.
   - **PASS:** prints the comment block, including `--target base|playtest` and the Targets and Branch convention sections.
   - **FAIL:** an error, no output, or shell code below `set -euo pipefail`.
3. Refusal with no ids. Run `env -u STEAM_APPID -u STEAM_DEPOTID -u STEAM_PLAYTEST_APPID -u STEAM_PLAYTEST_DEPOTID ./Tools/Steam/upload.sh --target playtest --build-dir Builds/Windows64; echo exit=$?`, then repeat with `--target base` and with `--target Playtest`.
   - **PASS:** playtest prints 'ERROR: STEAM_PLAYTEST_APPID is not set. Complete checklist A4 ...' and exit=1. Base prints 'ERROR: STEAM_APPID is not set. Complete checklist A2 ...' and exit=1. Capital P prints "ERROR: Unknown --target 'Playtest'" and exit=1.
   - **FAIL:** any run reaches steamcmd, exits 0, or names the wrong item.
4. Safe dry run. Paste: `mkdir -p /tmp/cs-steam/build /tmp/cs-steam/bin && touch /tmp/cs-steam/build/CosmicShore.exe && printf 'version=0.9.1\ncommit=abcdef0123456789\nconfiguration=release\n' > /tmp/cs-steam/build/build_manifest.txt && printf '#!/usr/bin/env bash\necho "STUB steamcmd $*"\n' > /tmp/cs-steam/bin/steamcmd && chmod +x /tmp/cs-steam/bin/steamcmd && unset STEAM_PASSWORD && export STEAMCMD=/tmp/cs-steam/bin/steamcmd STEAM_USER=test STEAM_APPID=100 STEAM_DEPOTID=101 STEAM_PLAYTEST_APPID=200 STEAM_PLAYTEST_DEPOTID=201`. Run `./Tools/Steam/upload.sh --target playtest --build-dir /tmp/cs-steam/build --branch internal --set-live`, then `grep -E '"appid"|"desc"|"setlive"' Tools/Steam/work/app_build.vdf; grep DepotID Tools/Steam/work/depot_build.vdf`. Never use real ids or a real steamcmd.
   - **PASS:** no confirmation prompt appears. The summary shows 'target : playtest', 'app id : 200 (STEAM_PLAYTEST_APPID)', 'depot id : 201', 'set live : true' and 'description : Cosmic Shore 0.9.1 release abcdef01 [playtest] -> internal', then a 'STUB steamcmd +login test +run_app_build' line. The greps show appid "200", the [playtest] desc, setlive "internal" and depot 201.
   - **FAIL:** a prompt appears, any 100 or 101 appears, setlive is not "internal", or anything other than the STUB line runs.
5. Same terminal. Run the step 4 upload again WITHOUT `--set-live`, then repeat the greps.
   - **PASS:** 'set live : false' and setlive "". This is the 'lands unassigned' case the docs now warn about.
   - **FAIL:** setlive is non-empty without --set-live.
6. Run `./Tools/Steam/upload.sh --target base --build-dir /tmp/cs-steam/build --branch internal --set-live` and repeat the greps.
   - **PASS:** app id 100 (STEAM_APPID), depot 101, a description ending '[base] -> internal', setlive "internal", and the VDFs contain only 100 and 101.
   - **FAIL:** any 200 or 201 appears, or the description lacks [base].
7. Same-id refusal. Run `STEAM_PLAYTEST_APPID=100 ./Tools/Steam/upload.sh --target playtest --build-dir /tmp/cs-steam/build; echo exit=$?`, then `STEAM_PLAYTEST_DEPOTID=101 ./Tools/Steam/upload.sh --target playtest --build-dir /tmp/cs-steam/build; echo exit=$?`.
   - **PASS:** the first prints 'ERROR: STEAM_APPID and STEAM_PLAYTEST_APPID are the same id ...' and the second 'ERROR: STEAM_DEPOTID and STEAM_PLAYTEST_DEPOTID are the same id ...'. Both exit 1 with no STUB line.
   - **FAIL:** either run reaches the STUB line or exits 0.
8. Default confirmation. Run `./Tools/Steam/upload.sh --target playtest --build-dir /tmp/cs-steam/build --branch default --set-live` three times: type 100 and Enter; then press Ctrl-D (or append `</dev/null`); then type 200 and Enter, followed by `grep setlive Tools/Steam/work/app_build.vdf`.
   - **PASS:** every run shows '*** This will make the build LIVE on the default branch for every tester who has been granted Playtest access (the PLAYTEST app). ***' and 'Type the playtest app id (STEAM_PLAYTEST_APPID) to confirm:'. Runs 1 and 2 end 'Confirmation did not match STEAM_PLAYTEST_APPID. Aborted.' with no STUB line. Run 3 shows the STUB line and setlive "default".
   - **FAIL:** a wrong id or Ctrl-D reaches the STUB line, the prompt names the base app, or run 3 aborts.
9. Run `git status --short Tools/Steam`, then `rm -rf /tmp/cs-steam Tools/Steam/work` and close the terminal.
   - **PASS:** git status prints nothing (work/ is git-ignored).
   - **FAIL:** work/ or rendered .vdf files show as untracked or modified.
10. Docs read-through by the owners, on GitHub. Shombith reads `Docs/STEAM_PLAYTEST_RUNBOOK.md` A4 step 6, the 'Branch model under two apps' table and its four consequences, and the 'Uploading to either app' block. Garrett reads 'The one irreversible step: the name' and the A6 template. Then read `Tools/Steam/README.md` 'Turning it on' and `Docs/BUILD_AND_DELIVERY.md` §0 (B7 row), §4 and §8 steps 9-11. Click every link.
    - **PASS:** tables render. A4 step 6 creates a password-protected `internal` branch on the child app before uploading. Every internal upload command reads `--target ... --build-dir ... --branch internal --set-live`. B7 and §8 steps 9-11 run on the Playtest child app's `internal`, then set `default` live from App Admin. No line still says 'promote to beta' or B7 'during the closed playtest' without the Playtest-app routing. All in-repo links open, and the owners agree they can execute each A4 step.
    - **FAIL:** a broken table or link, an internal upload without --set-live, a B5/B7 step routed through the base app's beta, or a step an owner cannot execute.
11. DEFERRED until A2 and A4 are done in Steamworks. Create the password-protected `internal` branch on the Playtest child app. Build a Windows player with `Tools/Build/build_windows.sh`, export the real Playtest ids on the build machine (never commit them), and run `./Tools/Steam/upload.sh --target playtest --build-dir Builds/Windows64 --branch internal --set-live`. Open Steamworks > the Playtest child app > App Admin > Builds. Then, on a clean PC, install the child app, switch it to `internal` (Properties > Betas, enter the password) and launch it.
    - **PASS:** a build whose description reads 'Cosmic Shore <version> release <8-char commit> [playtest] -> internal' is LIVE on `internal` on the CHILD app, not the base app. The installed game launches with a working Steam overlay (B7).
    - **FAIL:** the build lands on the base app or sits unassigned, the description lacks [playtest], or steamcmd prints an error while the script still says 'Upload complete'.
<!-- /steps:C -->

### Part D: Plans and docs only

| | |
|---|---|
| Commits | 9: e335ed1c0 2a8f1119b 80aabc866 889e950c4 48d84486f b762f66a2 94fc408b4 e0454b9c8 26ff520b8 |
| Ships to players | No. |
| Risk | Low |
| Needs | Its QA items describe A, E, F, G, H and J. |
| Push | Last, with or after the parts it describes. |

**What it does.** The Rhino elemental-ability decision packet (`Docs/ElementalAbilitySystem/RHINO_PROPOSAL.md`, Steam board R10) and its index rows, the ecosim lab marked broken in the labmaker catalogue, a note on the Prefab Kit's 75 warnings, a full QA backlog refresh (52 new items, 209 total), and the Skim Race intensity-1 three-seat tune recorded as a negative result.

**Benefit.** Design gets a Rhino packet grounded in the current tree (its corrections were re-checked file by file). QA gets 52 new items, and the negative result stops the same tune from being re-run.

**Risks.**

- Some QA items describe features in parts A, E, F, G, H and J. Pushed without those parts, QA tests code that is not there and files bogus dev tasks. *High if D goes first. The pick tool warns.*
- QA_BACKLOG.md is generated by `/qa-backlog`; hand edits here are dated and limited to this branch's items. *Low.*

**Fixed after review.**

- The new QA items' wrong menu and asset paths, the Skim Race practice step that cannot exist, and the parity test counts are corrected.
- The Rhino packet says the sword's trigger bindings live in `_gamepadActionOverrides` (desktop only), and its two broken table rows are escaped.

**Test steps.**

<!-- steps:D -->
The 2026-10-10 `/qa-backlog` refresh absorbed the checklist's open sections as of `48d84486f`; sections and steps added after it are NOT yet QA items, so run each part's steps from this plan until the next `/qa-backlog` run.


1. OFFLINE, no Unity, terminal at the repo root. Run `python3 Tools/Build/element_ability_table.py Rhino`, `python3 Tools/Build/audit_vessel_skimmers.py`, `python3 Tools/Build/audit_lifeform_crystals.py`, `python3 Tools/Build/gamecanvas_unification_report.py --check`, `python3 Tools/QA/session.py selftest`, `python3 Tools/QA/apply_results.py --selftest` and `python3 Tools/QA/apply_results.py --dry-run`. Then `python3 -c "import re,collections;s=open('Docs/QA/QA_BACKLOG.md',encoding='utf-8').read();i=re.findall(r'^### (QA-[A-Z0-9-]+)',s,re.M);print(len(i),len(set(i)),[k for k,v in collections.Counter(i).items() if v>1])"`.
   - **PASS:** 'RHINO abilities 2/4 scaling wired 3/4 L5 gates wired 0/4'; '6 fault(s); standing exceptions: Serpent'; '32 warning(s)'; 'GameCanvas unification gate: OK (15 scenes ...)'; 33/33; 17/17; 'No new verdicts to apply'; '208 208 []' (the count on this tree).
   - **FAIL:** any count differs.
2. UNITY, 2 min: the menu and asset paths D's QA items name. Open the FrogletTools menu and confirm each of these exists: Vessels > Audit Vessel Skimmers; Validation > Validate Lifeform Crystals; Performance > Crystal Flip Wave Benchmark; Parity > Capture Goldens; Performance Benchmark (it has a Load Time Insights tab); AI Training; AI > Skim Race AI Benchmark; Toolbox (Logging tab with AITraining and Parity rows); Game Modes > Game Mode Prefab Kit; QA > QA Session. In the Project window, search 'SceneNameList' and 'Runtime GameData'.
   - **PASS:** every menu is at the listed path. SceneNameList is at `Assets/_SO_Assets/SceneNameList.asset` and Runtime GameData at `Assets/_SO_Assets/Game Data/Runtime GameData.asset`. The QA backlog items name these same paths. The one known leftover is QA-ECOLOGY-BORROMEAN-FLORA step 3, which still says 'Ecology > Validate Lifeform Crystals' (it predates this branch). That is a doc fault, not a game fault.
   - **FAIL:** a menu or asset is missing from the listed path (a real defect), or a branch-added QA item still names 'Ecology > Validate Lifeform Crystals', 'Benchmarks > Crystal Flip Wave' or 'Assets/Resources/SceneNameList'.
3. UNITY, 2 min: open FrogletTools > QA > QA Session and press its refresh button. Open the instructions panel of QA-OCT-IMPORT-SMOKE, QA-AUDIT-OFFLINE-TWINS, QA-AI-PRACTICE-PILOTS, QA-PARITY-CAPTURE and QA-SKIMRACE-RELATIVITY.
   - **PASS:** the window shows 208 backlog items, and each opened item shows numbered steps plus PASS and FAIL text.
   - **FAIL:** a different count, or an '(added 2026-10-10)' item showing 'This item's instructions could not be read out of QA_BACKLOG.md'.
4. UNITY, 3 min: Rhino packet facts. (a) Open `Assets/_Prefabs/Spacevessels/Components/ForceFieldSkimmer Variant.prefab`, select the Skimmer component and expand Scale. (b) Open `Assets/_Prefabs/Spacevessels/Rhino.prefab` and select the child GrowSkimmerActionRegistry. (c) On the Rhino root's R_VesselActionHandler, expand Input Event Ship Actions, Touch Action Overrides and Gamepad Action Overrides.
   - **PASS:** (a) Scale is Enabled with Element Space, Min 30, Max 50. (b) The GameObject's active checkbox is unticked. (c) Input Event Ship Actions has one entry (event 0, two actions), Touch Action Overrides is empty, and Gamepad Action Overrides holds events 1 and 2 with one swipe action each. This matches packet §0.3 as corrected (desktop-only triggers).
   - **FAIL:** Scale is not 30 to 50 on Space, GrowSkimmerActionRegistry is active, or the bindings differ from §0.3. Tell design before they read the packet.
5. UNITY, 3 min: packet §0.4. Press Play in Menu_Main, enter freestyle as the Rhino, fly a long straight line at full throttle, then look back at the trail.
   - **PASS:** the trail slabs stay at their resting size (the dead grow loop).
   - **FAIL:** the slabs visibly grow. Then §0.4, the §2b Mass row and §5 item 4 must be revised before design ticks rows.
6. GitHub render check: open `Docs/ElementalAbilitySystem/RHINO_PROPOSAL.md` §6.1 and `Docs/claude/DOCUMENTATION_INDEX.md` (STEAM_PLAYTEST_RUNBOOK row) on GitHub.
   - **PASS:** step 3's cell reads in full, ending '... keep the FX/pop semantics on the real IsEnergized; map UpgradeLabel: Bright Edge + UpgradeDescription'. The index row ends '... STEAM_BUSINESS_SETUP.md is the paid-path (Revision 1) runbook and points here.'
   - **FAIL:** either cell is cut off at a '|'.
<!-- /steps:D -->

### Part E: AI training fixes and the deployed tournament genome

| | |
|---|---|
| Commits | 7: 0e67df5bb 369d4f9ad 67f25a9fb 20dffeb08 8a9133186 8d6894da3 2bcbe4e57 |
| Ships to players | Yes, one behaviour: Skim Race and Regatta AI after a Hangar practice game. |
| Risk | Medium |
| Needs | None to compile. G needs E. |
| Push | After its editor steps pass. |

**What it does.** The genetic trainer gets its own session flag (`GameDataSO.IsGeneticTrainingSession`), so the Hangar's practice flag no longer makes Skim Race and Regatta drop their AI Squirrels from the racing pilot to the generic one for the rest of the app session. The training pilot's prism sensor queries the prism spatial index instead of physics (no 64-collider truncation, no per-hit `GetComponentInParent`, same data in Prisma and Unity). Prisma gains `--train export`, and the Prisma tournament winner (fitness 221.4 against 47.2 for the previous genome over the same races) is committed as the deployed Squirrel Skim Race intensity-4 genome.

**Benefit.** Players: after any Hangar practice game, later Skim Race and Regatta matches in the same session keep the racing AI instead of the weaker generic pilot. Editor: training sessions cannot be confused with practice, the sensor reads the real nearest prisms, and the deployed genome is the tournament pick rather than one lucky flight.

**Risks.**

- Editor Learn can still replace the deployed pick: after its first match the runner deploys its own hall of fame (most likely generation 7's top elite, gene 160.96394, tournament mean 104.8). The stale 680 genome is gone, but pinning a tournament pick needs a runner change (an archive entry Learn does not overwrite). *High whenever someone presses Learn; editor only. Discard Archive.asset after any Learn (step 7).*
- The prism sensor change does not close the 0.6 s blind window right after a prism is laid (the earlier claim was wrong and is corrected). *Editor-only; the row stays open in the AI training docs.*
- The sensor's cost is no longer capped at 64 hits; at the default radius it can scan every registered prism and sort the result per pilot per frame. *Editor training and 'Play against trained AI' only. Profile before changing it.*

**Fixed after review.**

- Prisma's export normalises the session counters and makes the hall of fame the deployed pick; the committed `SessionState.asset` is exactly what the fixed export writes, so Learn evolves to generation 8 and never re-deploys the stale 680 genome.
- A malformed `--fitness` fails the export (exit 2) instead of recording fitness 0.
- The prism sensor's docs no longer claim the 0.6 s spawn window is closed, and its performance note says to profile first.

**Test steps.**

<!-- steps:E -->
1. **Compile and import.** Open the project on the branch and wait for the import and compile to finish. Open the Console and click Clear. In the Project window, select `Assets/_SO_Assets/AI Training/Archive.asset`, then `Assets/_SO_Assets/AI Training/SessionState.asset`.
   - **PASS:** The Console shows 0 red errors. Both assets draw in the Inspector (Archive shows an Entries list; SessionState shows a Population section) with no 'script cannot be loaded' warning.
   - **FAIL:** Any compile error, or either asset shows a missing-script or deserialization error.

2. **Edit-mode tests.** Open Window > General > Test Runner > EditMode tab. Run `SkimRaceAITests` (in particular `Deployment_ClaimsOnlyNormalSkimRaceAndRegatta`), `AITrainingCoreTests` and `PrismSpatialIndexTests`.
   - **PASS:** All green.
   - **FAIL:** Any red test. A failing `Deployment_ClaimsOnlyNormalSkimRaceAndRegatta` means the practice/GA flag split is broken.

3. **Read the deployed data before anything enters Play from the AI Training window.**
   - Open FrogletTools > AI Training > Archive tab and click the row in 'All entries'.
   - Select `Archive.asset` and open Entries > Element 0 > Notes, and Entries > Element 0 > Genome > Gene Values > Element 0.
   - Select `SessionState.asset` and expand Population. Read Generation, Evaluations This Gen, Next Checkout Index and the size of the Population list. Then read Hall Of Fame Best Fitness, and Hall Of Fame Best > Gene Values > Element 0 and Generation Born.
   - Open the window's Inspect tab and read 'Best fitness'.
   - **PASS:**
     - The Archive row reads `Squirrel_SkimRace_I4    fitness 221.4    gen 7`.
     - Notes starts 'Port tournament 2026-10-10: pop13', and Archive Gene Values Element 0 = 164.41396.
     - SessionState: Generation 7, Evaluations This Gen 192, Next Checkout Index 192, Population list size 24, Hall Of Fame Best Fitness 221.4, Hall Of Fame Best Gene Values Element 0 = 164.41396 (the same genome as the archive), Generation Born 7.
     - Inspect 'Best fitness' 221.40.
   - **FAIL:**
     - Evaluations This Gen is negative (-999808): the counter fix did not land, so Learn never leaves generation 7.
     - Hall Of Fame Best Fitness is 679.9873, or its Gene Values Element 0 is 179.45839: the stale 680 hall of fame is back.
     - Any archive value differs from the above.

4. **The practice flag no longer mutes the Skim Race pilot (the change players get).**
   - In FrogletTools > Toolbox > Logging, enable the `AITraining` channel.
   - Open `Assets/_Scenes/Menu_Main.unity` and press Play.
   - Select `Assets/_SO_Assets/Game Data/Runtime GameData.asset` and tick `Is Training`. This stands in for a Hangar practice game earlier in the session. To do it for real instead: Hangar > Squirrel > Training > Slip and Stride, play it, then return to the menu.
   - Then go to Arcade > Skim Race > Squirrel, set intensity 4 and 2 or more players so AI seats are backfilled, and click Launch.
   - **PASS:** The Console shows `[SkimRaceAI] <AI name> flies the Skim Race pilot (..., I4, CrystalTrackObjective)` once per AI Squirrel, and the AI Squirrels collect crystals along the track.
   - **FAIL:** No `[SkimRaceAI]` line, and the AI vessel in the Hierarchy has only an active `AI Pilot` (no `Skim Race Pilot` component). That is the pre-branch bug.

5. **Negative control and the play-mode hook.** Repeat step 4, but untick `Is Training` and tick `Is Genetic Training Session` on Runtime GameData.asset before launching Skim Race. Finish or quit the match, exit Play, and look at Runtime GameData.asset again.
   - **PASS:** No `[SkimRaceAI]` line and no `[Deploy]` line. The AI Squirrels fly the legacy AI Pilot. After exiting Play, `Is Genetic Training Session` is unticked again (TrainingPlayModeHook cleared it).
   - **FAIL:** The Skim Race pilot installs while the GA flag is set, or the flag is still ticked after leaving Play.

6. **The archive genome flies (corrected checklist step 17; run it BEFORE any Learn).**
   - Select `Assets/Resources/SkimRaceAIConfig.asset` (the base asset, NOT `SkimRaceAIConfig_I4`) and untick `Deploy In Normal Play`.
   - In FrogletTools > AI Training > Deploy tab, make sure `Use archive in normal play` is ticked.
   - Click `Play against trained AI`. The dialog should say that AI seats keyed to Squirrel use the intensity-4 genome.
   - Click Play and fly the host seat through a whole race. Select an AI vessel in the Hierarchy.
   - **PASS:**
     - The Console shows `[Deploy] <AI> flies the archive (Squirrel, SkimRace, play intensity 4)` for each AI seat.
     - The AI vessel has exactly one `Training Pilot` component, and its `AI Pilot` is disabled.
     - Each AI follows the track and collects at least 1 crystal over the race.
     - The Console shows no exceptions from PrismSensor or TrainingPilot.
   - Optional A/B: import the old genome (`git show 67f25a9fb^:"Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json"`) with Archive tab > Import JSON and fly 3 races with each genome. The new genome should collect more crystals on average (Prisma measured 3.46 against 1.67 per flight).
   - **FAIL:** A `[SkimRaceAI]` line appears instead (the wrong config was toggled), no `[Deploy]` line, two pilots on one seat, red errors, or the AI circles without collecting anything.
   - Afterwards: re-tick Deploy In Normal Play and discard changes to SkimRaceAIConfig.asset, TrainingControl.asset and Archive.asset (if you imported).

7. **GA session (destructive: run LAST, then discard).**
   - Keep the `AITraining` log channel on (FrogletTools > Toolbox > Logging).
   - In FrogletTools > AI Training > Configure, choose scenario `Scenario_HexRace` and click Learn.
   - While it runs: select Runtime GameData.asset, and select each AI vessel. Open Window > Analysis > Profiler (CPU, Hierarchy) and search for `TrainingPilot.Update` at a dense moment.
   - Note the first `[Training] Rollout start. generation=...` line.
   - Wait for the first `[Training] Rollout recorded. generation=...` lines. Then open the Archive tab and the Inspect tab, and select Archive.asset (Entries > Element 0 > Fitness and Genome > Gene Values > Element 0).
   - Press Stop and exit Play.
   - **PASS:**
     - While running: `Is Genetic Training Session` is ticked and `Is Training` is unticked; each seat has exactly one `Training Pilot`; there are no `[SkimRaceAI]` or `[Deploy]` lines.
     - The first Rollout start and every Rollout recorded line say generation=8.
     - After the first match, the Archive entry's Fitness equals the Inspect tab's 'Best fitness', and neither is 680.0/679.99.
     - Archive Gene Values Element 0 is either 164.41396 (pop13, fitness 221.4) or, more likely, 160.96394. 160.96394 is generation 7's top elite (in-training mean about 250), which the Learn loop deploys under its own hall-of-fame rule. Either value is a PASS.
     - TrainingPilot.Update stays well under 1 ms per pilot.
     - After Stop, `Is Genetic Training Session` is unticked.
   - **FAIL:**
     - generation=7 in the Rollout lines: the SessionState counter fix is missing.
     - The Archive entry reads fitness 680.0 with Gene Values Element 0 = 179.45839: the stale hall of fame was re-deployed.
     - A second pilot on a seat, or the flag left ticked after Stop.
     - Multi-millisecond TrainingPilot.Update self time. Record the number, because the PrismSensor cost has not been profiled (README > Performance notes).
   - Afterwards, in every case: discard every change under `Assets/_SO_Assets/AI Training/` and to `Assets/_SO_Assets/Game Data/Runtime GameData.asset` (GitHub Desktop: right-click > Discard changes).

8. **Clean tree before pushing.** Look at git status (the GitHub Desktop Changes tab).
   - **PASS:** No changes left under `Assets/_SO_Assets/AI Training/`, `Assets/Resources/SkimRaceAIConfig*.asset` or `Runtime GameData.asset`, and no file anywhere contains `IsGeneticTrainingSession: 1`.
   - **FAIL:** Any of those files is modified. Discard before pushing; they are test state, not deliverables.

9. **(Optional, no editor; Prisma CLI, needs the .NET 10 SDK) Export guards.** From the repo root, build with `dotnet build Port/src/CosmicShore.Player`. Run `COSMIC_SHORE_PROJECT=$PWD dotnet Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore.dll --train export --genome "Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json" --fitness 221,4 --train-out /tmp/exp_bad`, then run it again with `--fitness 221.4 --train-out /tmp/exp_ok`.
   - **PASS:**
     - The first run exits 2, prints `--fitness expects a finite number ... got '221,4'`, and creates no /tmp/exp_bad.
     - The second run exits 0 and writes `Squirrel_SkimRace_I4.Archive.asset` and `Squirrel_SkimRace_I4.SessionState.asset`. The SessionState drop-in matches the committed SessionState.asset except LastWriteUtc.
   - **FAIL:** The bad value exits 0, or writes an archive at fitness 0.

Withdrawn from the old checklist: the step-4 check 'steers around a rail laid in front of it within a second ... which the collider-based scan could not see', and the 'Landed' bullet that says PrismSensor sees a prism during the 0.6 s its collider is off. The code does not deliver that; the spawn window is still open (README > Performance notes; Docs/AI_TRAINING_CONSOLIDATION.md item 6 note and risk row 13). There is nothing to test there until a PrismSpatialIndex/Prism lifecycle change lands.
<!-- /steps:E -->

### Part F: Skim Race AI intensity-4 policy v2-i4

| | |
|---|---|
| Commits | 3: acf012253 9339b9f21 b70608c27 |
| Ships to players | Yes: every AI Squirrel in Skim Race (and Regatta) at intensity 4. |
| Risk | Low |
| Needs | None. |
| Push | After its editor steps pass. |

**What it does.** Replaces `Assets/Resources/SkimRaceAIConfig_I4.asset` with policy `skimrace-v2-i4`, re-tuned in the offline simulator on the Relativity course and written by `Tools/Build/author_skimrace_ai_config.py`. Data only, no code.

**Benefit.** Simulated on fresh seeds and re-run by the reviewer to the hundredth: one AI seat median 149.3 to 141.3 s; two seats where every seat finished 5/40 to 39/40; three seats 2/20 to 14/20. In practice the second AI seat stops falling out of the race.

**Risks.**

- Never flown in the editor; the simulator is calibrated at 26 ms frames and the editor's 3-AI cell has run at 46-52 ms, where the AI is slower. *Medium likelihood, low impact: times differ from the prediction.*
- Regatta at intensity 4 loads the same asset and was not simulated. *Medium likelihood, low-medium impact: Regatta's AI may thread fewer rings.*
- The new policy bumps the ribbon more often and still finishes. *Cosmetic.*

**Fixed after review.**

- `Docs/SKIM_RACE_AI.md`'s status section quotes the shipped v2-i4 time.

**Test steps.**

<!-- steps:F -->
1. Pull the branch and let Unity import. Clear the Console and reimport Assets/Resources (right-click > Reimport).
   - **PASS:** no error mentions SkimRaceAIConfig_I4, and no orphan-.meta warning for `Assets/_Scripts/Editor/Parity` or `Assets/_Scripts/Utility/Replay` (the pick tool drops those folder metas when part H is not in the push).
   - **FAIL:** an error on SkimRaceAIConfig_I4, a missing-script warning, or an orphan-.meta warning for those two folders.
2. Select `Assets/Resources/SkimRaceAIConfig_I4` and read the Inspector without editing. Then select `Assets/Resources/SkimRaceAIConfig` (the base config, which holds the deployment flag the game reads).
   - **PASS:** the I4 config shows Policy Version skimrace-v2-i4, Deploy In Normal Play ticked, Skim Height 5.894, Lane Height Step 2.437, Min Throttle 0.126, Crossing Lead Seconds 0.04 and Crossing Lookahead Scale 0.932. The base config has Deploy In Normal Play ticked.
   - **FAIL:** skimrace-v1-i4, a different value, or a missing script.
3. Window > General > Test Runner > EditMode: run the SkimRaceAITests fixture.
   - **PASS:** all green, including TrackerAndCaptureSearch_DefaultToOff_AndAreUnsetInEveryShippedPolicy and LevelApproach_DefaultsToOff_AndIsUnsetInI1AndI4.
   - **FAIL:** any red test.
4. Live smoke test. Tick AITraining in FrogletTools > Toolbox > Logging, press Play in Bootstrap, and in Menu_Main go to Arcade > Skim Race at intensity 4 with the Squirrel and Players 3. Watch for 60 s.
   - **PASS:** one '[SkimRaceAI] <name> flies the Skim Race pilot (skimrace-v2-i4, I4, CrystalTrackObjective)' line per AI seat, and both AIs fly the five-lobed ribbon with rising crystal counts.
   - **FAIL:** the line names skimrace-v1-i4 or skimrace-v1, or does not appear; or an AI circles one crystal for more than 10 s or stops.
5. **Two-AI and three-AI benchmark.** Open FrogletTools > AI > Skim Race AI Benchmark. Set Races 10, Intensity 4, Total players (host + AI) 3, Benchmark limit 0, and Per-race timeout 240 (the default 150 cuts off about a quarter of v2-i4's races and counts them as timeouts). Click Run benchmark (enters Play mode) and keep Unity in front for about 30 min. Then run `python3 Tools/Build/skimrace_benchmark_report.py BenchmarkResults/SkimRaceAI/skimrace_I4_<stamp>.jsonl --markdown`. Repeat with Total players 4. A Skim Race ends at the first finisher, so only the winner has a finish time; the other seats are judged by crystals.
   - **PASS (each run):** full-sequence completion rate 10/10, timeouts (no finish) 0/10, and a finish-time median of 150 s or less (the simulator predicts about 141 s). At Total players 3, the 'other AI at end' column is 44/52 or more in at least 8 races. 'completed within limit: 0/10' and every row's 'FAIL: finish ... > limit' are EXPECTED, because 70 s is unreachable on Relativity. At Total players 4, judge '(team win)' rows on completion and timeouts only.
   - **FAIL:** any timeout at 240 s; a median over 160 s; 'other AI at end' at 40/52 or less in 3 or more races (the second seat falling away, v1's failure); or an exception. If the report's mean frame time is above 40 ms, write it down and do not fail on time alone (the simulator assumed 26 ms frames).
6. Regatta at intensity 4 (this part changes it without measuring it). With AITraining on, go to Arcade > Regatta at intensity 4, Squirrel, Players 3, and play to the end. If an AI fails, isolate the cause: `git show acf012253^:Assets/Resources/SkimRaceAIConfig_I4.asset > Assets/Resources/SkimRaceAIConfig_I4.asset`, repeat the race, then `git checkout -- Assets/Resources/SkimRaceAIConfig_I4.asset`.
   - **PASS:** '(skimrace-v2-i4, I4, RegattaRingObjective)' appears for each AI, ring counts keep rising, no AI orbits a ring for more than 10 s, and the match ends normally.
   - **FAIL:** an AI threads 0 rings in 60 s, orbits, or stalls, AND the old v1-i4 asset does not show the same problem.
7. Doc check (F3): open `Docs/SKIM_RACE_AI.md` §9, I4 bullet.
   - **PASS:** it says the shipped skimrace-v2-i4 completes in about 141 s median (§6.13: 141.3 s with one seat, 141.4 s first finisher with two, simulator, not flown in the editor). Those numbers match the §6.13 table.
   - **FAIL:** it still quotes ~146 s or 'the shipped policy' without naming v2-i4.
8. Before committing anything, run `python3 Tools/Build/author_skimrace_ai_config.py --check` and `git status`.
   - **PASS:** '--check OK', and no change under Assets/Resources/SkimRaceAIConfig*.asset.
   - **FAIL:** DRIFT, or a saved inspector edit. Revert it with git checkout.
<!-- /steps:F -->

### Part G: Load-time target and unattended load sweep (R7)

| | |
|---|---|
| Commits | 5: 766b14800 04dc39982 ea164e254 bf7c09077 6eec85f81 |
| Ships to players | Instrumentation only: span markers around existing boot and connecting-panel awaits; the sweep is dev-build only. |
| Risk | Medium |
| Needs | parts E, M; Needs E (`GameDataSO.IsGeneticTrainingSession`); the pick tool adds it. |
| Push | With E. |

**What it does.** Publishes a load-time target (cold boot to menu 10 s, menu to first playable frame 20 s) in `Docs/PERFORMANCE_OPTIMIZATION.md` section 0.6, mirrored in code and held by a test. Adds a cold-boot recording to Load Time Insights and an unattended dev-build sweep (`-csmloadsweep`) that launches every roster card at every intensity and writes a worst-first table with PASS/FAIL per row.

**Benefit.** Steam checklist D2 / DoD #5 finally has a target and a way to measure it: about 120 manual launch-and-read cycles become one command.

**Risks.**

- The sweep finds its rosters by asset name: renaming ArcadeGames or ArenaGames makes it log an error and sweep what remains. *Low.*
- Reports from before and after the attribution fix are not like-for-like: envelope spans now claim only unclaimed time. *Low: compare only reports made on the same build.*
- A force-killed sweep leaves the build armed until two PlayerPrefs keys are deleted (documented in step 14). *Low: dev builds only.*
- Boot instrumentation wraps existing awaits in shipped auth and boot code. *Low: each wrap was traced; cancellation and exceptions still propagate.*

**Fixed after review.**

- The sweep reads the real ArcadeGames and ArenaGames rosters and skips scenes not in the build (31 cards, 30 swept by default, 120 cells).
- Envelope spans (connecting-panel dwell, arena gate, no-panel hold) claim only time no other span claims, so prism lays keep their attribution.
- The sweep stops with a 'Stopped early' table when the menu never becomes ready.
- A cold-boot recording completes only in Menu_Main.
- The unused BenchmarkConfig target fields are gone, and the doc's anchors match the shipped assets (dwell 4 s, floor 6.4 s, profile cap 15 s).

**Test steps.**

<!-- steps:G -->
Part G ships with or after parts E (`GameDataSO.IsGeneticTrainingSession`) and M (Prisma's `Application.CanStreamedLevelBeLoaded`); the pick tool adds them.

1. **Compile check on the exact set being pushed.** Open the project in Unity on the branch or cherry-pick being pushed. Wait for the domain reload, then open the Console with the Errors filter on.
   - **PASS:** zero compile errors.
   - **FAIL:** `CS1061 'GameDataSO' does not contain a definition for 'IsGeneticTrainingSession'` in LoadSweepRunner.cs means part E is missing from the push set. Any other CS error in a LoadInsights*, LoadSweep*, ConnectingPanelController, MiniGameHUD or BenchmarkConfigSO file is also a FAIL.

2. **Edit-mode tests.** Open Window > General > Test Runner > EditMode. Type `LoadSweepTableTests` in the filter and click Run Selected. Repeat with `LoadInsightsAttributionTests`, then with `BenchmarkConfigSOTests`.
   - **PASS:** all 10 LoadSweepTableTests are green, including PublishedTargets_MatchTheCodeConstants, CardsFromRosters_ReadsBothRostersOnce_AndSkipsScenesNotInTheBuild and BuildMarkdown_AnEarlyStopAndAnAbortedBootSayWhy. All 4 LoadInsightsAttributionTests are green. BenchmarkConfigSOTests are green.
   - **FAIL:** any red test.

3. **BenchmarkConfig asset after the target fields were dropped.** Select `Assets/_SO_Assets/Benchmark/BenchmarkConfig.asset` and look at the Inspector.
   - **PASS:** there is no "Load-Time Targets" header, and selecting the asset puts no warning or error in the Console.
   - **FAIL:** the header is still shown, or a serialization warning appears.

4. **Shipped behaviour with recording OFF.** Open FrogletTools > Performance Benchmark > Load Time Insights tab. The big button must read "Arm Record Insight Mode - record the next game load" (not ARMED), and "Also record the cold boot ..." must be unticked; fix them if not. Open `Assets/_Scenes/Bootstrap.unity`, press Play and let it reach Menu_Main. Then go to Arcade > Skim Race > Intensity 1 > Start and play until the countdown ends.
   - **PASS:** the boot looks as it does on bleeding-edge (splash, sign-in, then the menu with the vessel flying). The connecting screen shows for about 4 s, then the arena, then the countdown. The Console has no line starting `[LoadInsights]` and no exception that mentions LoadInsights or LoadSpanScope.
   - **FAIL:** a hang on the splash or the auth screen, a new exception, or any `[LoadInsights] Recording` line while disarmed.

5. **Auth branches that now carry spans.** (a) Turn the machine's network off and Play from Bootstrap. (b) Turn the network on, choose Edit > Clear All PlayerPrefs (this signs you out and creates a new anonymous account), Play from Bootstrap, answer the privacy/age screen if it appears, enter a new username and confirm. (c) If the auth panel appears, press the guest button.
   - **PASS:** (a) shows "No connection. Starting offline..." and reaches the menu. (b) shows the username panel, and the menu loads after you confirm. (c) signs in and reaches the menu. All three behave as on bleeding-edge.
   - **FAIL:** any of them stalls on the auth screen or the splash, or throws an exception.

6. **Game-launch recording in the Editor.** In the Load Time Insights tab, click "Arm Record Insight Mode". Play from Bootstrap, then go to Arcade > Skim Race > Intensity 1 > Start. When the countdown starts, open the newest report in the tab, or the .txt in `%USERPROFILE%/AppData/LocalLow/Froglet Games/Cosmic Shore/Benchmarks/LoadInsights/`.
   - **PASS:** the span list contains `Connecting panel dwell (4.0s)` (Scripted Delays, wait) and `Connecting panel hold: arena-ready gate (remainder no builder span claimed)` (Game Flow). In the timeline both are tagged `(envelope)`. Completion reads "Loaded - arena complete (laid + grown), connecting screen done". The tab's caption under the chart reads "Exact attribution: every ms goes to the latest-started open span (an envelope only gets what nothing else claims) ...".
   - **FAIL:** there is no report, the report ends at the countdown instead, either span is missing, or either span is not tagged `(envelope)`.

7. **Attribution on a heavy cell (finding G2's fix).** Stay armed and launch Arcade > Dog Fight > Intensity 4 > Start. Optionally repeat with Scurry at Intensity 4. Open that report's .txt.
   - **PASS:** the top cost is a `Streamed prism lay (...)` span (Cell & Environment), not the hold. `Connecting panel hold: arena-ready gate ...` has a small exclusive time, about 1-2 s (the gate's stable window plus gaps). The dwell's exclusive time is below 4.0 s whenever a lay was running during it.
   - **FAIL:** the hold or the dwell is the top cost with many seconds while a `Streamed prism lay` span was still open, or the lay's exclusive time is cut off at the moment the hold opened.

8. **Cold-boot recording in the Editor.** With Record Insight Mode armed, tick "Also record the cold boot (engine start -> main menu ready) on the next Play / app start". Open Bootstrap.unity, press Play and wait for the menu vessel.
   - **PASS:** the Console shows `[LoadInsights] ● Recording cold boot` early, and a "Load report ready" line whose reason is "Main menu ready - OnClientReady in Menu_Main" when the menu appears. A file `load_boot_*.json/.txt` exists, and its header reads "Interval Cold boot - engine start -> main menu ready". The spans include 'Shader variant warm-up', 'Minimum splash hold', 'Bootstrap -> Authentication scene load', an 'Auth: ...' sign-in span, 'Auth: waiting for PlayerDataService (cloud profile load, 15s timeout)', 'Host start: waiting for Relay session (attempt 1/3, 15s timeout)', 'Netcode scene load -> activation (host, Menu_Main)' and 'Menu_Main active -> menu vessel spawned (OnClientReady)'. The last one is tagged `(envelope)`. There is no 'Engine start -> first script hook' span, which is expected in the Editor. Launching a game afterwards produces a SEPARATE `load_<scene>_*` report.
   - **FAIL:** there is no boot report, the boot report never finishes (the tab still says Recording at the menu), or the game launch is merged into the boot report or replaces it.

9. **A boot that did not start from Bootstrap is not saved as a cold boot (finding G6's fix).** Keep the recorder armed and the cold-boot toggle ticked. Choose FrogletTools > Scene Setup > Testing Multiplayer > Do not load Bootstrap Scene on Play. Open `Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity`, press Play, wait about 20 s, then Stop. Afterwards choose FrogletTools > Scene Setup > Testing Multiplayer > Load Bootstrap Scene on Play to restore the redirect.
   - **PASS:** if the scene raises OnClientReady, the Console shows the warning "[LoadInsights] The cold-boot recording reached its first OnClientReady in 'Maelstrom', not Menu_Main: this session did not boot from Bootstrap ..." followed by "Load recording ended ... Aborted: not a cold boot: first OnClientReady was in 'Maelstrom' ...". If the scene never raises OnClientReady, the recording ends at Stop with "Aborted: play session ended". Either way, the tab's newest report never reads "Main menu ready".
   - **FAIL:** a `load_boot_*` report whose completion reads "Main menu ready - OnClientReady in Menu_Main" while its sceneTo is Maelstrom.

10. **Clean up the Editor arm flags.** Untick the cold-boot toggle and click the ARMED button to disarm. Press Play, then Stop.
    - **PASS:** the tab shows not armed, and there are no `[LoadInsights]` lines on that Play.
    - **FAIL:** recording still starts.

11. **Sweep smoke test.** Make a Development Build with Burst ON (Build Profiles > a Windows or the CS Linux profile, Development Build ticked). Run it from a terminal on a machine that has signed in once: `"Cosmic Shore.exe" -csmloadsweep -csmloadsweep-modes SkimRace,Joust -csmloadsweep-intensities 1 -csmloadsweep-cooldown 2`
    - **PASS:** the build boots to the menu, launches Skim Race I1, returns to the menu, launches Joust I1, returns again and quits by itself. In `.../LocalLow/Froglet Games/Cosmic Shore/Benchmarks/LoadInsights/` there is `sweep_<stamp>.md` with a cold-boot row (PASS/FAIL against 10 s, with a Note column) and 2 cell rows (PASS/FAIL against 20 s), plus `load_boot_*` and two `load_Minigame*` reports. The file has no "Stopped early" line.
    - **FAIL:** the build hangs, does not quit, writes no `sweep_*.md`, or a row says NO DATA.

12. **Roster check (finding G1's fix).** Run the dev build with `-csmloadsweep -csmloadsweep-intensities 1`. After about a minute, open `Player.log` in the same LocalLow folder, find the two `[LoadSweep]` lines below, then close the app.
    - **PASS:** `[LoadSweep] Roster: 31 card(s) from 2 of 2 roster(s) (ArcadeGames, ArenaGames); 0 skipped, scene not in the build.` and `[LoadSweep] 30 cell(s): intensities [1] ...` (31 roster cards minus Maelstrom). No `Cell ...` line names BlockBandit, DolphinDarts, Elimination, MazeRun, SlipNStride, WildlifeBlitz or ShootingGallery, and there is no "Skipped '...'" warning.
    - **FAIL:** N is 37, a legacy mode appears, either roster is reported "is not loaded", or any card is skipped.

13. **The sweep stops when the menu never comes (finding G3's fix).** Sign the dev build out: on Windows, run regedit and delete all values under `HKEY_CURRENT_USER\Software\Froglet Games\Cosmic Shore`. This creates a new anonymous account on the next start. Run `"Cosmic Shore.exe" -csmloadsweep -csmloadsweep-modes SkimRace -csmloadsweep-intensities 1`. Do NOT answer the privacy or username screen. Wait about 4.5 minutes.
    - **PASS:** the build quits by itself about 240 s after boot. `Player.log` has `[LoadSweep] Stopped early: The menu was not ready within 240s of the cold boot (active scene 'Authentication') ...`. The new `sweep_<stamp>.md` opens with a `**Stopped early:** ...` line, its boot row reads FAIL with the note "Aborted: sweep: the menu was not ready within 240s", and it lists 0 cells.
    - **FAIL:** the build launches Skim Race from the auth screen, it never quits, or the table has no "Stopped early" line.

14. **Arm-flag restore and leak (corrected statement).** (a) Let the step-11 smoke test finish by itself, then start the dev build normally (no flags) and play one game. (b) Start a sweep, kill it from Task Manager mid-cell, then start the build normally and play one game. (c) Delete the values `CSM.LoadInsights.Armed` and `CSM.LoadInsights.BootArmed` under `HKEY_CURRENT_USER\Software\Froglet Games\Cosmic Shore`, then start the build normally and play one game.
    - **PASS:** (a) no new `load_*.json` appears, because a sweep that finishes restores the unarmed state. (b) a new `load_boot_*` and a `load_<scene>_*` DO appear: this is the documented leak after a force-kill. (c) no new `load_*.json` appears.
    - **FAIL:** (a) or (c) writes a new report, or (b) writes none (the doc would then be wrong in the other direction).

15. **Peer-wait span (optional).** Use two signed-in accounts in a party. With Record Insight Mode armed on the FASTER machine, the host launches Skim Race I1.
    - **PASS:** that machine's report contains "Waiting for peers' arenas (n of 2 humans still building)" as a wait span.
    - **FAIL:** the span is absent although the connecting screen visibly waited on the other player.

16. **Full matrix (hand-off to H8, not a push gate).** On the GTX 1060 floor machine with Burst ON and a cached sign-in, run the dev build with `-csmloadsweep`.
    - **PASS:** `sweep_<stamp>.md` lists 120 cell rows plus the boot row, has no "Stopped early" line, and the log says `Roster: 31 card(s)`.
    - **FAIL:** the row count is not 120, or many rows are aborted timeouts.
<!-- /steps:G -->

### Part H: Parity harness, Unity side

| | |
|---|---|
| Commits | 9: 1a397c3e3 646d599e0 9339b9f21 b3490953f aaf649451 9bf9961f5 751ec5a11 635d0787b a348f9750 |
| Ships to players | Hooks that do nothing unless the capture tool or an environment variable turns them on. |
| Risk | Medium |
| Needs | parts M; Needs M in Prisma (the probe uses engine types M adds); the pick tool adds it. I needs H. |
| Push | After M has landed, then I after H. |

**What it does.** Adds a replay recorder and player for flight input, a deterministic session (seeded random, fixed capture frame rate, seeds for seven previously unseeded `System.Random` sites), a probe that writes Prisma's parity format, and **FrogletTools > Parity > Capture Goldens**, which plays each `Port/parity/manifest.json` case and writes the Unity goldens Prisma diffs against.

**Benefit.** Without Unity goldens every Unity channel of Prisma's parity scoreboard is MISSING and the parity check passes vacuously. This part plus one capture run makes the scoreboard mean something. The recorder and player also reproduce flight bugs on demand.

**Risks.**

- Editor-side ordering nobody has witnessed: SessionState queue handling, quitting versus EnteredEditMode, playModeStartScene, and the Input System device injection. *Medium: steps 6 to 12 exercise each one.*
- Goldens captured under a tester's own profile or invert settings differ on every checkpoint. *Medium. The capture must run as the returning user `parity` with invert off.*
- Prisma's player build compiles against these Unity types directly, so a rename here breaks Prisma CI. *Low.*

**Fixed after review.**

- Replay and session state reset at subsystem registration and when Play ends, so the next Play flies on the devices.
- A capture can be cancelled (FrogletTools > Parity > Cancel Capture, or stopping Play), and a failing case advances the queue.
- Replay frames carry the stick vectors seven single-stick hulls read, and the recorder hears the first frame's button edges.
- Three clock-seeded `System.Random` sites take the session's seed.
- ReplayFileTests assert state instead of Soap delivery, which does nothing in edit mode.
- New: Record Input, Replay Recording and Stop Replay under FrogletTools > Parity, so a tester can record and replay a flight.

**Test steps.**

<!-- steps:H -->
Before you start: FrogletTools > Toolbox > Logging, set the log level to All so the [ParityCapture] info lines show. /verify-unity has not run for any part H commit, so every step below is a first in-editor run.

1. **Compile and menus.** Pull the branch, open the project in Unity and wait for the import to finish. Open the Console and FrogletTools > Froglet Master Tool. In Edit mode, open the FrogletTools > Parity menu.
   - **PASS:** The Console has no red CS#### errors. FrogletTools > Parity lists Capture Goldens, Cancel Capture, Record Input, Replay Recording and Stop Replay. In Edit mode, Record Input, Replay Recording and Stop Replay are greyed out. The Master Tool's Validation section shows a card for each of the five.
   - **FAIL:** A compile error, a missing menu item or card, or Record Input / Replay Recording / Stop Replay enabled in Edit mode.

2. **Logging channel.** Open FrogletTools > Toolbox > Logging and find the verbose channel list.
   - **PASS:** A row reads '[Parity] replay recorder/player, deterministic session, parity probe'. Toggling it works and the other rows behave as before.
   - **FAIL:** The row is missing or mislabelled, or toggling Parity changes other rows.

3. **Edit-mode tests.** Open Window > General > Test Runner > EditMode. Run CSDebugTests, then ReplayFileTests.
   - **PASS:** All CSDebugTests are green, including CSLogChannel_EveryMemberIsAUniqueSingleBit and CSLogChannel_EveryMemberCarriesALabel. ReplayFileTests shows 23 leaf tests (19 methods; Random_SequencePerSeed_IsReproducible runs once for each of its 5 seeds) and all 23 pass. They include ReplayPlayer_WritesRecordedFrames_AppliesEdges_AndHoldsTheLast, ReplayPlayer_Stop_ReleasesAButtonStillHeld, DeterministicSession_Reset_StopsTheReplayAndTheRecording_AndEndsTheSession, DeterministicSession_ResetRunsAtSubsystemRegistration, ReplayRecorder_ListensBeforeTheFirstFrame_AndCapturesFloatsAndSticks and ReplayRecorder_IsInertUntilStarted.
   - **FAIL:** Any test fails, or the old name ReplayPlayer_WritesRecordedFrames_RaisesEvents_AndHoldsTheLast still appears (a stale compile).

4. **Normal play is unchanged.** Restart Unity. Press Play from the usual start scene and reach Menu_Main. Click the menu crystal to take freestyle control and fly with WASD, the mouse and the ability keys. Then go to Arcade > Skim Race > Start > Ready and fly for about 30 s. While in Play, check the Hierarchy under DontDestroyOnLoad. Afterwards run `git status`.
   - **PASS:** Flight and abilities respond as on bleeding-edge. There is no '[ParityProbe]' object under DontDestroyOnLoad, and git status shows no new files under Port/parity/goldens.
   - **FAIL:** The vessel ignores input, a '[ParityProbe]' object appears, or files are written under Port/parity.

5. **FMOD loops stop.** In the same Skim Race, finish the match or quit to the menu, and listen.
   - **PASS:** The engine, drift and boost loops stop when the vessel is torn down. There are no new FMOD errors or exceptions.
   - **FAIL:** A loop keeps playing, or an error mentions ParityProbe or FmodSafe.

6. **Capture run.**
   - Setup:
     1. Sign in with a dedicated test account whose in-game display name is exactly `parity` and which is a returning user (no birth-year, consent or username prompts).
     2. In Settings: Invert Y OFF, Invert Throttle OFF, Music ON, SFX ON.
     3. Keep the Game view docked and visible.
   - Run FrogletTools > Parity > Capture Goldens. Do not touch the keyboard or mouse, and do not click any other editor window, until it finishes. That takes at least 6 minutes, longer below 60 fps.
   - **PASS:**
     1. '[ParityCapture] random goldens for 5 seed(s)'.
     2. 'case 1/5 'boot-menu' ... from Bootstrap, seed 42'. Play starts on Bootstrap and reaches Menu_Main with no prompts.
     3. Case 2 starts by itself: the Skim Race card opens, the match starts, Ready is pressed, and the Squirrel flies forward and then turns left.
     4. Cases 3 (astroleague-strike) and 4 (bloomrush-i4) play.
     5. Case 5 (skimrace-status): the Squirrel sweeps left and right and pitches once.
     6. The run ends with '[ParityCapture] done: 5/5 case(s) written'.
   - **FAIL:**
     - 'done' reports fewer than 5/5.
     - 'ParityProbe never began' or 'is not in the build settings' appears.
     - Play starts outside Bootstrap.
     - A prompt blocks the boot.
     - The vessel never moves in skimrace-fly, astroleague-strike or bloomrush-i4.

7. **Capture output.** Inspect what the capture wrote, using a file browser or text editor.
   - **PASS:** Port/parity/goldens/random/ holds random_0, random_1, random_42, random_1337 and random_20261006 (.json). Each of boot-menu, skimrace-fly, astroleague-strike, bloomrush-i4 and skimrace-status has state.jsonl, events.jsonl, transforms.jsonl and run.json. run.json reads {"engine":"unity","audio":"native"}. The first line of each events.jsonl contains "scene:Bootstrap". skimrace-fly/events.jsonl contains "OnLaunchGame" and a "scene:Minigame..." line. Match-time lines in state.jsonl contain "stats":{"parity":. transforms.jsonl is not empty.
   - **FAIL:** A missing file, a missing run.json, stats keyed by another name, or an empty transforms.jsonl for a race case.

8. **Statics reset (H0).** Straight after step 6, without restarting Unity or editing a script, press Play. Fly in freestyle with the keyboard, then the mouse, then a gamepad if you have one. Leave Play and run FrogletTools > Parity > Capture Goldens a second time in the same editor session.
   - **PASS:** The vessel answers keyboard, mouse and pad at once. In the second capture, case 2 'skimrace-fly' moves the Squirrel forward and left as in step 6, and the run again ends 'done: 5/5'.
   - **FAIL:** The vessel cruises straight and ignores the devices (a replay still owns input), or the second run's skimrace-fly vessel stays parked, or the second run reports fewer than 5/5.

9. **Determinism.**
   1. Copy Port/parity/goldens somewhere outside the repo.
   2. Run Capture Goldens again with the same account and settings. A restart is no longer required.
   3. Compare the two copies.
   - **PASS:** For every case, state.jsonl and events.jsonl are identical apart from the "t" values, and transforms.jsonl positions match to millimetres.
   - **FAIL:** A different event order or count, different stats, or positions that differ by metres. Note the case and the first differing line.

10. **Stop mid-case cancels the run (H4).** Start Capture Goldens. When case 2 'skimrace-fly' has started the match and the Squirrel is flying, press the editor's Play button to stop. Then run `git status`, and press Play once more.
    - **PASS:** The Console shows:
      - "[ParityCapture] case 'skimrace-fly' was interrupted; <repo>/Port/parity/goldens/skimrace-fly holds a partial run (run.json removed)..."
      - "[ParityCapture] Play was stopped before the case finished; capture cancelled"
      - "[ParityCapture] done: 1/5 case(s) written ..."

      The editor stays in Edit mode and no later case starts. Port/parity/goldens/skimrace-fly has no run.json. The extra Play does not force Bootstrap, creates no '[ParityProbe]' object, and flies on the devices. Afterwards, restore the partial directory with `git checkout -- Port/parity/goldens/skimrace-fly`, or delete it if it was never committed.
    - **FAIL:** Case 3 starts on its own, run.json is still present, there is no 'capture cancelled' line, or the next Play is still driven by the replay or the probe.

11. **Cancel Capture (H4).** Start Capture Goldens. During case 3 'astroleague-strike', choose FrogletTools > Parity > Cancel Capture. Afterwards, in Edit mode with nothing queued, choose Cancel Capture again. Then press Play.
    - **PASS:** The editor leaves Play. The Console shows "[ParityCapture] case 'astroleague-strike' was interrupted; ... (run.json removed)" and "[ParityCapture] cancelled at case 3/5; cases after it were not captured", and no later case starts. Port/parity/goldens/astroleague-strike has no run.json (restore it as in step 10). The second Cancel logs "[ParityCapture] no capture was queued; environment and replay state cleared". The final Play starts from the open scene, not a forced Bootstrap, has no '[ParityProbe]' object, and flies on the devices.
    - **FAIL:** Play keeps running, a later case starts, run.json remains, or the following Play is still driven by the replay or the probe.

12. **A replay steers a single-stick hull (H2, H3).** Press Play and go to Arcade > Dog Fight > Start > Ready; Dog Fight seats a Sparrow.
    1. Choose FrogletTools > Parity > Record Input, then click the Game view's tab to give it focus.
    2. Hold a hard left turn for about 2 s, fly straight for about 1 s, and tap one ability key once (the ability row shows its key).
    3. Choose FrogletTools > Parity > Replay Recording and take your hands off the keyboard and mouse. Watch until the motion settles.
    4. Choose FrogletTools > Parity > Stop Replay and fly again.
    5. Open the saved JSON.
    - **PASS:**
      - Record Input logs '[ParityCapture] recording input; ...'.
      - Replay Recording logs '[ParityCapture] replaying N recorded frame(s) from <repo>/Logs/Parity/recording-<yyyyMMdd-HHmmss>.json; ...'.
      - With no input, the Sparrow repeats the left turn, the ability fires once, and the vessel then holds the last recorded frame.
      - Stop Replay logs '[ParityCapture] replay stopped; input is back on the devices', and the keyboard and mouse fly the Sparrow again.
      - In the JSON, the turn's frames carry non-zero "EasedLeftJoystickPosition" values, and the ability key appears once in a "pressed" array and once in a later "released" array.
      - While a capture is running, Record Input, Replay Recording and Stop Replay are greyed out.
    - **FAIL:**
      - The Sparrow flies straight during the replay (the stick vectors are not replayed).
      - EasedLeftJoystickPosition is zero on every frame of the turn.
      - The ability press or release is missing from the JSON.
      - The devices do not return after Stop Replay.
      - The menu entries are enabled during a capture.

13. **Commit the goldens.** The tool records nothing in the tool ledger, so check FrogletTools > Build > Pending Tool Changes, or run `git status`.
    - **PASS:** Port/parity/goldens/** shows up as new files and is committed in its own commit with this part. No partial directory from steps 10 or 11 is committed, and every case directory has a run.json.
    - **FAIL:** The goldens are left uncommitted, or a case directory without run.json is committed.

14. **Optional build check** (needs a build). Build a Windows development player with the usual build profile. Run it once normally. Then run it with COSMIC_SHORE_REPLAY=<repo>/Port/parity/replays/boot-menu.json and COSMIC_SHORE_PARITY_OUT=<an empty temp folder> set in the environment.
    - **PASS:** The build succeeds (IL2CPP accepts the FMOD MonoPInvokeCallback). The normal run writes nothing. The run with the variables writes state, events, transforms and run.json into the temp folder.
    - **FAIL:** An IL2CPP or AOT error mentions ParityProbe or OnFmodEvent, the normal run writes files, or the run with the variables writes nothing.
<!-- /steps:H -->

### Part I: Prisma engine (Port/ only)

| | |
|---|---|
| Commits | 10: 8bd90dbe4 e03b31341 1fcb52135 1ea3d23b5 441fafc47 6bf9e5a0c 2c915676c 7ad2ef887 ca79890d2 a8a904c5c |
| Ships to players | No. Unity never compiles Port/. |
| Risk | Low |
| Needs | parts H, M; Needs H and M (its parity runner uses the game's replay types). |
| Push | After H. |

**What it does.** The engine side of the parity harness (replay hand-off to the game's own player, a one-writer-per-directory guard, the fixed `parity` returning-user profile, an FMOD gate for events the banks do not carry), git 2.55 bisect parsing, training checkpoints with `--resume-run`, two performance changes (a compiled field copier for `Instantiate`, a maintained trigger pass), and headless training that starts from the committed bot profile so a fresh machine does not stop at the first-run prompts.

**Benefit.** Prisma's full suite passes (1,721 tests). A long training run survives a killed worker. The weekly training routine stops hanging on a fresh container (its 2026-10-11 run sat at the Authentication prompts for 30 minutes).

**Risks.**

- The FMOD gate turns a stale event reference into a red error in Prisma, and a test asserts the strings bank carries at least 90% of `GUIDs.txt`. *Low: an audio-only change could turn Prisma CI red.*
- `Resources` now registers every ScriptableObject the content bridge loads, which changes what `FindObjectsOfTypeAll` returns in Prisma. *Low: Prisma only.*

**Fixed after review.**

- `--resume-run` resumes a run killed between two workers' checkpoint writes.
- The game's parity probe hears every event the strings bank carries.
- Headless training starts from the committed bot profile, so a fresh machine does not stop at the first-run prompts.

**Test steps.**

<!-- steps:I -->
1. **Port only, after H.** Build this part's branch with the pick tool once parts M and H have landed, then run `python3 Port/tools/check_unity_isolation.py` on it.
   - **PASS:** it names no file outside `Port/`, and `git log origin/bleeding-edge..<branch>` lists only part I's commits.
   - **FAIL:** a Unity file is listed (H or another part was not yet on `bleeding-edge`, so the tool added it). Land H first, or push I with H as one exception to the Port rule and say so in the pull request.
2. **Unity compile.** Open the project on the push branch, let it finish compiling, click the Console's Clear button and wait for the compile spinner to stop.
   - **PASS:** no new compile errors, and no Console line mentions Port/ (Unity never imports Port/).
   - **FAIL:** any compile error. It cannot come from this part, so it points at another part on the branch.

3. **Prisma builds and boots.** In Unity: **FrogletTools ▸ Prisma ▸ Launch Prisma**. The first launch builds Prisma into Library/Prisma and opens it. Play the branch the editor has open and wait for the build and the boot.
   - **PASS:** the build finishes without errors and the game reaches Menu_Main, after the birth-year, consent and username prompts on a new profile.
   - **FAIL:** a build error naming ParityRun.cs, ReplayFile, ReplayPlayer or DeterministicSession means part H is missing. One naming EVENT_CALLBACK, setCallback, MonoPInvokeCallback or captureFramerate means part M is missing; one naming StringsBankGuids or TrainingRunResume means a part I commit is missing. A crash during boot is also a fail.

4. **Skim Race match.** In Prisma: Arcade ▸ Skim Race, Squirrel, intensity 4 with AI seats. Fly one whole match: collect at least 5 crystals, skim along a trail, and fly into a prism wall once. Let the match end, then read Prisma's LAST SESSION report.
   - **PASS:** every pickup counts on the HUD and the crystal disappears. Skimming charges as on bleeding-edge, the wall hit registers, AI vessels race and the scoreboard appears. The report shows no exception types that a bleeding-edge Prisma run does not also show; the Squad..ctor NullReferenceException at the menu is pre-existing.
   - **FAIL:** crystals flown through without a pickup, a vessel passing through prisms it should hit, a match that never ends, or exceptions mentioning TriggerPass, Collider, ClonePlan, Instantiate, EventDescription or setCallback.

5. **Bloomrush.** In Prisma: Arcade ▸ Bloomrush at intensity 4. Play at least 60 s so the reef grows, and watch the omni crystals' flip animation.
   - **PASS:** the omni crystals flip, the log has no repeating exception, and the frame rate is equal to or better than bleeding-edge Prisma on the same machine.
   - **FAIL:** an exception repeating every frame, crystals frozen half-built, prisms that should be inactive still colliding, or a clearly lower frame rate.

6. **Engine tests in a terminal (needs a full checkout including the `Cosmic Shore/Build` folder).** From `Port/` run `dotnet test tests/CosmicShore.Tests --filter "FullyQualifiedName~TrainingResume|FullyQualifiedName~Fmod"`, then `dotnet test tests/CosmicShore.Tests`.
   - **PASS:** the filtered run prints `Passed! - Failed: 0, Passed: 24`. It includes `TrainingResumeTests.Checkpoints_a_generation_apart_resume_from_the_newest` and `FmodBankGateTests.TheBankListsWhatTheStringsBankCarriesAndTheProbesCallbackHearsEveryStart`. The full run prints `Failed: 0` (1725 tests on this branch).
   - **FAIL:** any failure. TheProjectsStringsBankIndexes... failing with 'Assert.NotNull' means the checkout lacks `Cosmic Shore/Build`, not a code fault.

7. **A killed run with a missing checkpoint resumes (optional, terminal, about 10 min).** Build the training player per Port/docs/AI_TRAINING.md 'Setup'.
   1. Run `dotnet ../binBE/CosmicShore.dll --train train --workers 2 --evals 1 --generations 4 --seed 300 --quiet --train-out out`.
   2. After the console has printed two `[train] generation N:` lines, press Ctrl+C and wait for the prompt.
   3. Delete `out/run/ckpt_w1.json` to simulate a kill before worker 1's write.
   4. Run the same command plus `--resume-run`.
   - **PASS:** the console prints `[train] worker(s) 1 had not checkpointed generation N when the run stopped; every worker resumes from ckpt_w0.json`, then `[train] resuming the run in .../out/run from generation N (2 checkpoint(s))`. Two `started worker ... from checkpoint` lines follow, and the run goes on to further `[train] generation` lines.
   - **FAIL:** `[train] cannot resume: ...`, or a worker that starts without 'from checkpoint'.

8. **The probe hears what the engine hears (optional, terminal).** With Prisma built, run the player as in Port/parity/README.md's table, with the probe pointed at a second directory: `COSMIC_SHORE_PARITY_OUT=/tmp/probe CosmicShore --headless --replay Port/parity/replays/skimrace-status.json --parity-out /tmp/engine`. Then run `grep -c '"kind":"fmod"' /tmp/engine/events.jsonl /tmp/probe/events.jsonl` and `grep -c 'Music/Music\|mass birttle' /tmp/engine/events.jsonl /tmp/probe/events.jsonl`.
   - **PASS:** both files have the same `fmod` count, and both contain `event:/Music/Music` and `mass birttle` start lines. The `fmod-stop` lines may still differ by the four `mass birttle` stops; that is known and not addressed here.
   - **FAIL:** the probe's `fmod` count is lower than the engine's, or it has no `event:/Music/Music` line.

9. **GitHub: Prisma parity CI on the push branch** (Actions ▸ 'Prisma parity CI' ▸ Run workflow, before merging to bleeding-edge).
   - **PASS:** the build, Engine unit tests, every smoke scene and the parity job are all green. The unit tests include BisectTests on the runner's newer git, TrainingResumeTests and FmodBankGateTests. Parity channels report MISSING, not FAIL, until goldens exist. The scoreboard step gives no warning if 6bf9e5a0c is included.
   - **FAIL:** BisectTests.Reads_a_real_bisect_limited_to_Port red (the git 2.55 claim is wrong), FmodBankGateTests red, or the build job red (H and I split).

10. **Only if you also capture parity goldens.** Run part H's **FrogletTools ▸ Parity ▸ Capture Goldens** signed in as a returning user whose display name is exactly `parity`. Use a throwaway account, never your own.
    - **PASS:** `Port/parity/goldens/<case>/state.jsonl` lines read `"stats":{"parity":{...}}`.
    - **FAIL:** stats keyed by any other name. Every state channel would then FAIL against the engine, which always plays as `parity`.
11. **Headless training on a fresh machine (terminal, about 5 min).** From the repository root, with the .NET 10 SDK: `dotnet build Port/src/CosmicShore.Player`, then run `XDG_DATA_HOME=$(mktemp -d) COSMIC_SHORE_NET=off COSMIC_SHORE_AUDIO=off COSMIC_SHORE_PROJECT=$PWD dotnet Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore.dll --train eval --genome "Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json" --flights 1 --seed 1 --train-out /tmp/train-probe` (on Windows, set `LOCALAPPDATA` to an empty folder instead of `XDG_DATA_HOME`).
    - **PASS:** the log shows `[train] profile: fresh copy of .../Port/parity/profile`, the scenes go Bootstrap, Authentication, Menu_Main, MinigameSkimRace within a few hundred frames, and `/tmp/train-probe/eval.csv` has a fitness row (measured 2026-10-12: Menu_Main at frame 92, the race at frame 140, exit 0).
    - **FAIL:** the run stays in 'Authentication' (the first-run prompts, the hang the 2026-10-11 weekly routine hit), or no `eval.csv`.
<!-- /steps:I -->

### Part J: Multiplayer QoL: friends, recent players, ready lights, dropping out, invite inbox

| | |
|---|---|
| Commits | 15: 75b764375 d70061b42 278055856 107c04c8a 6be10752d 71ed6a5ce caf4e2dd8 f2b1b343b 3703aee94 daec4871f 4817e6861 4e8d7789c e52d06987 0729e0d94 0f715f1d2 |
| Ships to players | Yes: Menu_Main's friends panels, the lobby, the party layer. |
| Risk | Medium |
| Needs | None. |
| Push | After the two-account steps pass. |

**What it does.** Players can send friend requests (by name, or from a new RECENT list of the last 20 pilots they played with), lobby seats light for the player who pressed Ready, a dropped client sees one neutral "Connection to the party was lost" notice after the screen is covered, and a party invite you miss stays in an inbox with a badge instead of vanishing after its popup.

**Benefit.** Before this, nothing in the game could SEND a friend request, a 2-4 player lobby lit seats in roster order rather than by who was ready, a dropped client watched its own hull vanish with the HUD up, and an invite missed while in a menu was gone.

**Risks.**

- A bare name only reaches pilots this machine already knows (online, party, recent, friends). A stranger needs their full Name#1234, which they read off their own panel and share out of band. *Medium: a UX gap, not a crash. Needs the two-account step against real UGS.*
- The bounce notice waits for the transition veil to lift (at most 15 s). If recovery cannot recreate a solo session (no internet), the notice can show behind black. *Low.*
- Typing in the Add Friend field can trigger raw keyboard gestures (screenshot, rear view, pilot swap); the callsign field shares this. *Medium likelihood, low impact. Step 6 records it; it does not block.*
- The friends panel layout was rewritten without an editor (cs-asset); the Online list shrank to about two rows. *Medium: check clipping at 16:9, 16:10 and 21:9.*

**Fixed after review.**

- The join-failure and connection-lost notice is shown through the self-creating toast manager once the transition veil lifts; the never-wired hold channel is removed.
- Add Friend resolves a bare name against players the game knows and sends by player id; a full Name#1234 goes by name; the panel shows your own tag.
- Recently played with is per account and never lists you; a failed save never skips the match end.
- A RECENT add survives the panel unloading, and the toast manager forgets toasts a scene change destroyed.
- The invite timeout cannot be set to 0 (which ended every invite at send).

**Test steps.**

<!-- steps:J -->
1. **Import and open.** Open the project on this branch and let it import. Open `Assets/_Scenes/Menu_Main.unity`, then `Assets/_Scenes/Bootstrap.unity`, and read the Console after each.
   - **PASS:** 0 compile errors, and no 'missing script' or 'missing reference' line naming FriendListPanel, AddFriendInput, AddFriendSendButton, AddFriendStatus, RecentHeader, Recent Scroll View, RecentEmptyText, InviteBadge (PartyInviteBadge) or PartyInviteNotificationPanel Variant.
   - **FAIL:** any red compile error, or a missing-script / missing-reference line naming one of those.

2. **Edit-mode tests.** Window > General > Test Runner > EditMode. Run `BounceNoticeDeliveryTests`, `FriendNameResolverTests`, `RecentPlayersStoreTests`, `LobbySlotRowReadyTests` and `IncomingInviteInboxTests`. Also run the existing `PartyInviteControllerTests`, `PartyInviteSystemTests` and `PartyInviteDataTests`.
   - **PASS:** all green, with result counts BounceNoticeDeliveryTests 3, FriendNameResolverTests 22, RecentPlayersStoreTests 28 (including the 9 FormatLastPlayed cases and the 3 'no usable id' cases), LobbySlotRowReadyTests 5, IncomingInviteInboxTests 26, plus the existing party tests. `ToastChannelHoldTests` no longer exists; it was deleted on purpose with the hold it tested.
   - **FAIL:** any red test, a different count, or ToastChannelHoldTests still present.

3. **Bounce-notice route, by inspection.**
   - In the Project window select `Assets/_Prefabs/CORE/PartyServices.prefab` and look at the PartyInviteController component.
   - In `Menu_Main`'s Hierarchy, find `UI_Refactored > Safe Area > ToastNotificationContainer`.
   - In the Project window, find `Assets/Resources/ToastNotificationSettings.asset` and `Assets/Resources/Channels/ToastNotificationChannel.asset`.
   - **PASS:** PartyInviteController has a 'Bounce Notice' header with 'Bounce Notice Veil Wait Seconds' = 15, and NO 'Bounce Toast Channel' field. ToastNotificationContainer exists and its checkbox is on. Both Resources assets exist.
   - **FAIL:** a 'Bounce Toast Channel' field is still shown, the container is missing or inactive, or either Resources asset is missing.

4. **Friends panel layout.**
   - Enter Play from Bootstrap, signed in, and reach Menu_Main. Go to the Arcade screen and click an empty party slot to open the friends panel.
   - Set the Game view to 1920x1080 (16:9), then 1024x768 (4:3).
   - Repeat from the Arena, Mission and Toybox screens.
   - **PASS:** on all four panels, top to bottom: ONLINE header and list (about 2 rows), ADD FRIEND header, then the name field with placeholder 'Pilot name or Name#1234' and the green add-friend glyph, then a status line reading 'Your tag: <YourName>#<4 digits>' (it may be blank for a moment right after sign-in, then fill in), then RECENT header with its list or empty-state line, then REQUESTS header and list. Nothing overlaps or clips at either aspect, and every text is readable.
   - **FAIL:** any section missing, overlapping, cut off or unreadable; the placeholder still reads "Pilot's exact display name"; or the status line stays blank for more than a few seconds while signed in.

5. **Add Friend with one account (Arcade friends panel).**
   - Do each action, in order:
     - (a) Press Send with the field empty.
     - (b) Type `zzqqxx` and press Send.
     - (c) Type `zzqqxx` again and press Enter in the field.
     - (d) Type your own display name without the #, then your own full tag exactly as the status line showed it.
     - (e) Type `zzqqxx#0000` and press Send.
     - (f) Click Send twice within half a second.
     - (g) Close the panel and reopen it.
   - **PASS:**
     - (a) The line reads "Type a pilot's name or their Name#1234."
     - (b) and (c) The line reads "Don't know zzqqxx. Type their full Name#1234." No 'Sending request...' line, no request, and Send stays clickable.
     - (d) Both read "That's you. Friends add you by your tag."
     - (e) The line reads 'Sending request to zzqqxx#0000...', then 'Couldn't send to zzqqxx#0000: <reason>', with one yellow [FriendsListPanel] warning, and Send is clickable again.
     - (f) One click sound and one outcome.
     - (g) The status line is back to 'Your tag: ...' and the typed text is still there.
   - **FAIL:** no status text; a request sent for a bare unknown name; Send stuck disabled; two outcomes or two sounds from a double click; or a red console error.

6. **Keyboard leakage while typing.** Click into the Add Friend field, type the characters c, 1, 0 and 2, then press Escape.
   - **PASS:** only the field text changes. No screenshot, no rear-view camera flip, no pilot swap, no screen navigation.
   - **FAIL:** any of those happen. This is a known risk the new field exposes; the profile callsign field shares it. Record it, do not block on it.

7. **Add Friend between two real accounts (machines A and B).** Turn on the Party log channel first: FrogletTools > Toolbox > Logging.
   - (a) On A, type B's name exactly as A's ONLINE row shows it, then press Send. Repeat later with the same name lower-cased and without spaces. On B, Decline the request.
   - (b) On B, read its own tag from its status line ('Your tag: BName#1234'). On A, type that tag exactly and press Send. On B, press Accept.
   - (c) On A, type B's bare name again and press Send.
   - **PASS:**
     - (a) A shows 'Sending request to <name>...', then 'Friend request sent to <name>.', and the field clears. The Party log line ends '(by id <B's player id>)'. Within one refresh, B's REQUESTS shows a FRIEND REQUEST row from A.
     - (b) A shows 'Friend request sent to BName#1234.' (logged 'by name'), and B's REQUESTS shows the row again. After Accept, the two accounts are friends.
     - (c) A shows '<name> is already your friend.' and nothing is sent.
   - **FAIL:** "Don't know <name>" for a pilot visible on A's ONLINE list; 'Couldn't send to BName#1234' for the exact tag; a send in (c); or a red error.

8. **Recently played with (two machines, NOT MPPM, for the main part).**
   - First find each account's player id: with the Party channel on, enter Play and read `localId:` in the '[HostConnectionService] Presence lobby joined' line.
   - Play one online arcade match with two humans and one AI seat through to the scoreboard. Back in Menu_Main, open Arcade > empty party slot on both machines.
   - On A, press the green glyph on B's RECENT row. Then play a second match with the same partner.
   - Then open `Application.persistentDataPath`. On Windows that is `%USERPROFILE%\AppData\LocalLow\Froglet Games\Cosmic Shore`; on macOS it is `~/Library/Application Support/Froglet Games/Cosmic Shore`.
   - Separately, on one machine, open MPPM (Window > Multiplayer > Multiplayer Play Mode) and sign Player 2 in as a different account, which shares persistentDataPath. Open its friends panel.
   - **PASS:**
     - The scoreboard appears normally on both machines.
     - Each machine's RECENT shows exactly the other human ('PLAYED JUST NOW', their name and avatar), with no AI row and no self row.
     - Pressing the glyph shows 'Friend request sent to NAME.', the row flips to REQUEST SENT, and B's REQUESTS shows the request. After Accept, both rows read FRIENDS.
     - After the second match there is still one row for the partner, at the top.
     - The folder holds `recent_players_<that account's player id>.data`, and NO `recent_players.data` is written.
     - The MPPM second account's RECENT does not show the first account's partner, and never shows a row for itself. It gets its own `recent_players_<its id>.data` once it finishes a match.
   - **FAIL:** scoreboard missing, an AI or self row, duplicate rows, the row not flipping, a shared `recent_players.data` being written, one account seeing another account's list, or a red error.

9. **Failed RECENT save never blocks the match end.**
   - On machine A, before a match, create a FOLDER named `recent_players_<A's player id>.data` in persistentDataPath.
   - Play an online match with another human to the end. Then delete the folder.
   - **PASS:** A's end screen and scoreboard appear normally. The Console has one yellow warning: '[GameDataSO] Recording the recently-played-with list failed (UnauthorizedAccessException: ...); the match end carries on.'
   - **FAIL:** no end screen or scoreboard on A, or a red exception from InvokeMiniGameEnd.

10. **RECENT press while the menu unloads.**
    - Open the friends panel with at least one RECENT row. Disable the network adapter so the request hangs.
    - Press the row's green glyph, then immediately close the panel and launch a solo arcade game.
    - Wait for the request to fail (up to about 30 s). Then re-enable the network.
    - **PASS:** no red MissingReferenceException from FriendsListPanel. A yellow [FriendsListPanel] warning is expected, and the outcome may appear as a toast or as a yellow 'No GameObject named ToastNotificationContainer' warning in the game scene.
    - **FAIL:** a red MissingReferenceException naming FriendsListPanel.OnAddRecentPlayerClicked.

11. **Ready identity.**
    - Use MPPM (Window > Multiplayer > Multiplayer Play Mode, Player 2) or two machines. The host invites the guest, then opens an arcade card (for example Skim Race) with the party in it.
    - The guest presses Ready first, then the host presses Ready.
    - Afterwards, a guest presses Ready and then leaves the party. Then close the card and open another.
    - **PASS:**
      - When the guest presses, the guest's seat lights on both screens and the host's seat stays dark.
      - The host's press lights the host's seat and the launch begins.
      - A guest who leaves after pressing goes dark, and the launch is re-evaluated.
      - A newly opened card starts with every seat dark.
      - Maelstrom's lobby ready count still counts correctly.
    - **FAIL:** the host's seat lights when the guest presses; lights carry over to a new card; both players are ready but there is no launch; or RPC errors appear. Both peers must run this branch, because the RPC was renamed.

12. **Join-failure bounce, with the notice.**
    - Host A sends B an invite, then quits the game. Within 60 s, B presses Accept.
    - Then, in the SAME Play session on B, do it a second time.
    - **PASS:**
      - B returns to its own working Menu_Main with its vessel flying, within about 15 s.
      - After the black veil lifts (never while the screen is still black), exactly one toast reads "Couldn't join - returned to your menu." in the menu's toast stack. It hides after about 5 s.
      - The Console has one yellow '[PartyInviteController] Bouncing to solo menu' warning.
      - The second bounce shows its toast too, with no MissingReferenceException from ToastNotificationManager.
    - **FAIL:** B stuck on black or a splash for more than 30 s; no vessel; no toast on either bounce; a toast that appears only while the screen is still black; two toasts for one bounce; or a red error.

13. **Requests list size check.** With one friend request already pending in B's REQUESTS list, A sends B a party invite while B's friends panel is open.
    - **PASS:** the party-invite row's Accept is visible or reachable by scrolling the Requests list, and accepting within 60 s joins the party.
    - **FAIL:** the invite row is hidden or unreachable, or it expires before it can be reached.

14. **Dropping out, client side (two machines).** Host and client play any scored arcade match with an AI seat or two.
    - (a) Disable the client's network mid-turn, with the pause menu OPEN.
    - (b) Repeat, but kill the host's process instead.
    - (c) With both in Menu_Main and the client on the Arcade screen (which pauses timeScale), quit the host.
    - **PASS:**
      - (a) and (b): about 30 s later in (a), and sooner in (b), the client's screen goes black BEFORE its own hull or HUD disappear. Then the client's own Menu_Main loads with its vessel moving.
      - Once the veil lifts, exactly one toast reads 'Connection to the party was lost - returned to your menu.' The text 'Host disconnected' appears nowhere.
      - (c): the menu reloads, the client's vessel moves (timeScale restored), and the same single toast appears.
      - In the client's friends panel, the host's row offers Spectate while the host is in a match, and Join once the host is back in the menu.
    - **FAIL:**
      - A frame of the arena with the client's own hull gone and the HUD up.
      - The menu stays black for more than 20 s with the network available.
      - No toast, two toasts, or the old 'Host disconnected' wording.
      - A frozen menu vessel in (c).
      - Spectate or Join missing on the host's row.

15. **Dropping out, host side (two machines, same setup).**
    - (a) Disable the client's network mid-turn.
    - (b) Repeat, but the client uses pause > LEAVE PARTY instead.
    - **PASS:**
      - (a): on the host, the client's hull freezes for about 30 s. Then one toast reads '<name> left - AI has the ship', and the hull flies under AI.
      - The turn and the scoreboard carry on, and the departed client's row keeps its score.
      - When the host returns to the menu there is no ghost party member and no stray AI vessel.
      - (b): the same handover with no 30 s wait.
    - **FAIL:** two toasts for one departure, the departed pilot's score row missing, a ghost member, or the match stalls.

16. **Invite inbox.**
    - Setup: THREE signed-in accounts (two inviters A and B, one invitee; machines or MPPM). Party log channel on. Invitee on the Home screen.
    - A sends an invite, then B sends one. Watch the console for 30 s.
    - On the invitee, open Arcade without opening the friends panel. Check the Arena, Mission and Toybox party panels too.
    - Press an empty '+' slot, then close the panel.
    - Decline B's row. Then Accept A's.
    - Separately:
      - with both invites on the card, press Decline on the CARD.
      - let an invite run 60 s unanswered, then have the inviter invite again.
      - in `PartyServices.prefab`, try to type 0 into HostConnectionService 'Outgoing Invite Timeout Seconds'.
    - **PASS:**
      - A's invite: the bottom-left card shows A with 'Also in your friends list for 60 s', one invite sound, and hides after 3 s.
      - B's invite: the card shows B with '2 invites waiting. Also in your friends list for N s'.
      - In the 30 s window there is exactly ONE '[INVITE-RECV] New invite' line per sender, and the sound does not replay.
      - The yellow badge right of PARTY reads 2, on all four party panels.
      - The '+' slot shows REQUESTS with BOTH invites, each with its seconds left. The badge is gone and stays gone after closing the panel.
      - Declining B's row keeps A's row, and the log reads "Declined '<B's id>'; 1 still open." Accepting A joins as before ('[HostConnectionService] Joined party').
      - A card Decline moves the card to the other invite instead of hiding it.
      - The unanswered invite's invitee row and the inviter's PENDING row clear within a few seconds of each other, and the re-invite arrives as new.
      - The timeout field refuses values below 1 (it snaps to 1).
      - The card's detail line fits its frame at 16:9 and 4:3, and the badge sits clear of the PARTY word and the 'Players Online' line on all four panels.
    - **FAIL:**
      - repeated invite lines or sounds, or the card bouncing;
      - a badge count that is wrong or stays after the panel was opened;
      - Decline of one sender removing the other;
      - a card Decline hiding the card while another invite is open;
      - the two sides' rows clearing far apart;
      - the timeout field accepting 0;
      - text overflowing the card, or the badge overlapping the header.
<!-- /steps:J -->

### Part K: Leak and lifetime fixes

| | |
|---|---|
| Commits | 5: c88d7083f 1c6fc87ad f375809ea e12a8165a 66aa4bed6 |
| Ships to players | Yes: effect cooldown tables, score trackers, four delayed tasks. |
| Risk | Low |
| Needs | None. |
| Push | Any time after its steps pass. |

**What it does.** The static per-vessel cooldown tables in four debuff and overtake effects now drop destroyed vessels; score trackers unsubscribe every scorer when destroyed, so the time-played loop dies with its tracker; four fire-and-forget delays take their owner's destroy token.

**Benefit.** A long session or a headless training run no longer keeps every dead vessel (and its trail) from earlier matches in four static tables, and a Skim Race client that leaves mid-race no longer keeps adding time score into the next match's stats. Prisma measured destroyed prisms reachable after 12 matches falling from 27,587 to 570.

**Risks.**

- A wrong cooldown answer would make danger-prism and explosion debuffs never land or land every frame. *Low: the comparison is unchanged and covered by the rewritten test.*
- A bound delay could be cancelled in a legitimate flow, skipping a track spawn or a round reset. *Low: each owner dies only with its scene.*

**Fixed after review.**

- VesselEffectCooldownsTests use keys Unity can create (they could not run at all before).
- The decoy-crystal debounce change, which was dead code, is reverted.

**Test steps.**

<!-- steps:K -->
`VesselDecoyByCrystalEffectSO.cs` is back to its content before this branch (the change was dead code), so it has no step.

1. **Compile gate.** Open the project at this branch and let Unity finish compiling. Open Window > General > Console and switch on the Error filter.
   - **PASS:** no red compiler error (CS####) names VesselEffectCooldowns.cs, VesselDecoyByCrystalEffectSO.cs, BaseScoreTracker.cs, NetworkScoreTracker.cs, SkimRaceController.cs, MultiplayerMiniGameControllerBase.cs or VesselEffectCooldownsTests.cs.
   - **FAIL:** any compiler error in one of those files.
2. **Edit-mode tests.** Open Window > General > Test Runner > EditMode tab. Type `VesselEffectCooldowns` in the search box and click Run Selected. Then click Run All and compare the red count with a bleeding-edge checkout.
   - **PASS:** VesselEffectCooldownsTests shows 5 green ticks: VesselTableIsTheGenericKeyedByResourceSystem, SecondApplicationInsideTheWindowIsRefused_AfterItIsAdmitted, KeysAreIndependent, DestroyedKeysLeaveTheTable_WhenANewKeyIsSeen, ClearForgetsEverything. The Console has no error mentioning ResourceSystem, IVesselStatus or "Can't add script behaviour". Run All shows no newly red test.
   - **FAIL:** any red VesselEffectCooldownsTests (the cooldown window or the destroyed-key prune in ObjectCooldowns is broken), or a test that is green on bleeding-edge and red here.
3. **Skim Race solo, then Play Again** (covers SpawnTrackEarly and ExecuteSceneReloadReplay). Open the Bootstrap scene and press Play. In Menu_Main go to Arcade > Skim Race, choose intensity 1 with one AI seat, and launch. Do not press Ready for 3 seconds. Then race to the scoreboard and click Play Again. (The old "quit during the fade" half is dropped. The 0.5 s black overlay blocks the pause menu, so it cannot be tested by hand.)
   - **PASS:** the track is laid within about 2 s of the arena appearing, before you press Ready. The race ends on the scoreboard. After Play Again the screen goes black, the scene reloads, a new track appears, the countdown runs and the picture fades in. No MissingReferenceException or OperationCanceledException from SkimRaceController or MultiplayerMiniGameControllerBase appears in the Console.
   - **FAIL:** no track before Ready, a screen that stays black after Play Again, or one of those exceptions.
4. **Joust to the end** (covers NetworkScoreTracker.DelayAndSendResults). Go to Arcade > Joust with one AI seat and play until the game ends.
   - **PASS:** the scoreboard appears about half a second after the game ends and names a winner, and Play Again reloads cleanly.
   - **FAIL:** no scoreboard, a scoreboard with no winner, or an exception from NetworkScoreTracker.
5. **Score tracker, single machine** (the check that tells old code from new for e12a8165a; it replaces the old Freestyle/RoundStatsList step, which could not fail). Open Window > UniTask Tracker and tick Enable AutoReload, Enable Tracking and Enable StackTrace. Launch Skim Race solo as in step 3 and press Ready. Mid-race, confirm the tracker lists Delay rows whose stack mentions TimePlayedScoring. In the Hierarchy select the 'Game' object of MinigameSkimRace. In the Inspector, on the SkimRaceScoreTracker component, open the ⋮ menu and choose Remove Component. Wait 3 s and click Reload in the tracker. A Netcode warning about a NetworkBehaviour destroyed while spawned is expected after this artificial step: ignore it, and stop Play afterwards.
   - **PASS:** no row mentioning TimePlayedScoring remains 3 s after the component is removed.
   - **FAIL:** rows mentioning TimePlayedScoring keep reappearing every quarter second. That is the old leak: the loop outlives its tracker.
6. **Cooldown smoke** (c88d7083f and 1c6fc87ad). The refused-inside-the-window rule is unchanged, so this only catches gross breakage. Do not judge this on an opponent joust: since PvP-is-petals-only, an opponent joust is throttled by the combat-hit latch, not by this table. Launch Skim Race at intensity 4 with two AI seats. Fly your hull into a red danger prism and keep scraping it for about 3 s while watching your elemental bars. Finish the race, click Play Again, and repeat once in the new race.
   - **PASS:** the bars drop in steps about once per second while you are in contact (every authored cooldown is 1 s); they neither drain every frame nor never drop. The second race behaves the same. No ArgumentNullException or KeyNotFoundException naming ObjectCooldowns or VesselEffectCooldowns appears.
   - **FAIL:** no drop at all, a continuous every-frame drain, or one of those exceptions.
7. **Score tracker, the real path** (needs a second account, plus a second machine or an MPPM virtual player). Use the main Editor as the CLIENT so it has the UniTask Tracker window. The host launches Skim Race with the client in the party. Mid-race, on the client, open the pause menu and choose Main Menu (for a client this leaves the party). With UniTask Tracker set up as in step 5, wait 3 s after Menu_Main appears.
   - **PASS:** the client lands in Menu_Main and no TimePlayedScoring row remains in its UniTask Tracker.
   - **FAIL:** TimePlayedScoring rows keep appearing in the menu. That is the leak the commit fixes.
<!-- /steps:K -->

### Part L: Heritable genome (evolution), off by default

| | |
|---|---|
| Commits | 9: 3bdd94362 e91c90363 b6e3a44ec 323a18eda f3b60adcb 511501e84 52d96167f 3ed212030 8791314e2 |
| Ships to players | One always-on change: 4 extra bytes in the fauna network spawn payload. Everything else is off unless a biome enables it. |
| Risk | Low (off), with one network caveat |
| Needs | None. |
| Push | After its steps pass and the LOCKED rule is signed off. |

**What it does.** Every lifeform carries four genes (Tempo, Reach, Fecundity, Cohesion). A child inherits its parent's genes with a small mutation through the same pick that carries its element; the genes scale pace, graze radius, feeds per birth and cohesion against an upkeep cost. A per-cell ledger records lineages, deaths by cause and a census. Adds **FrogletTools > Ecology > Evolution Monitor**, a headless harness with its evidence runs, and the Darwin Lab (a browser model held to the C# by a parity gate). Switched off by `CellConfigDataSO.Evolution.Enabled`, which no shipped asset sets.

**Benefit.** Masterplan Phase 3's missing piece (heredity with mutation) and Phase 4's lineage telemetry, with no gameplay change until a designer turns it on. The evidence reproduces offline: tempo falls under famine and rises on rich food, the selection-off control wanders.

**Risks.**

- The fauna spawn payload is 4 bytes longer even with evolution off, so peers on mismatched builds misread replicated fauna. *Low likelihood (same-build sessions are fine), high impact for a playtest build against a default build.*
- Testing it edits shipped cell configs in Play mode, and saving writes a new `evolution:` block; a tester can commit `Enabled: 1` by accident. *Medium: discard asset changes after testing (section 1 step 4).*
- It added a LOCKED rule ('the genome is a lean, not a level') to the ecosystem principles with no recorded sign-off, and the review fix routes a worm's FISSION through `Cell.OffspringPick` (a fission mutates; a player's CUT keeps the genome and is filed as a birth). *Needs the owner's sign-off on both (section 6).*

**Fixed after review.**

- A worm split expresses its genome once (no double upkeep), is filed as a birth under its parent, and a natural fission goes through `Cell.OffspringPick`.
- Ledger ids never repeat after `Clear()`, so a creature from the previous world cannot close a new one's row.
- The masterplan rows say built, off by default, not editor-verified.

**Test steps.**

<!-- steps:L -->
Facts every step relies on: there is no `Blob Cell Config`. Menu_Main boots **Garland** (`Assets/_SO_Assets/Cell Configs/Garland Cell/Garland Cell Config.asset` is the only config with BootDefault; Lattice is only first in the list). Garland's fauna (QuadFish, Brittlestar, Shark) are all LightFauna. Boid tadpoles live in Lattice. The only NetworkSynced fauna are the Wildlife Liberation sharks. **Change any knob BEFORE pressing Play**: a genome is expressed once, at spawn. **Play-mode edits to a Cell Config persist on disk**, so do the revert step at the end.

1. **Compile gate.** Open the project at this branch, wait for the import, open the Console, click Clear and wait for the recompile.
   - **PASS:** zero red errors. Nothing mentions LifeformGenome, EvolutionLedger, EvolutionSettings, GenomeExpression, EvolutionMonitorWindow, Fauna, WormFauna, Boid, LightFauna, Cell or FaunaNetworkSync.
   - **FAIL:** any compile error. Do not push this part.
2. **Edit-mode tests.** Open Window > General > Test Runner > EditMode. Search `LifeformGenomeTests` and click Run Selected. Then click Run All and compare with bleeding-edge.
   - **PASS:** LifeformGenomeTests shows 27 green, including Ledger_ClearNeverReissuesAnId_SoTheOldWorldsDeathsCloseNothing. Run All has no new failure, especially in the ecology, fauna and cell tests.
   - **FAIL:** any LifeformGenomeTests failure, or a test that passes on bleeding-edge but fails here.
3. **Monitor window.** NOT in Play mode, open FrogletTools > Froglet Master Tool and expand the Ecology section. Then open FrogletTools > Ecology > Evolution Monitor.
   - **PASS:** an 'Evolution Monitor' card is in the Ecology section. The window opens with a banner and a blue info box starting 'Enter Play mode in a scene with a Cell whose CellConfigDataSO has Evolution > Enabled on'.
   - **FAIL:** the card is missing, the window throws, or the Console shows a GUI error.
4. **Read-only default check.** In the Project window, select each of these under `Assets/_SO_Assets/Cell Configs/` and expand the 'Evolution' header in the Inspector without changing anything: `Garland Cell/Garland Cell Config.asset`, `Lattice Cell/Lattice Cell Config.asset`, `Yggdra Cell/Yggdra Cell Config.asset`, `Wildlife Liberation Cell/Wildlife Liberation Cell Config 1.asset`. Then run `git status`.
   - **PASS:** Enabled is unticked on all four. Mutation Rate 1, Mutation Sigma 0.08, Founder Spread 0.1, Tempo Pace Range 1.5, Tempo Upkeep Range 1.84, Reach Radius Range 1.5, Reach Upkeep Range 1.36, Fecundity Range 2, Fecundity Provision Range 2, Cohesion Range 1.6, Snapshot Interval Seconds 30, Max Snapshots Per Species 2048. git shows no modified .asset.
   - **FAIL:** Enabled is ticked anywhere, or viewing an asset made it modified.
5. **Switch-off regression, single player.** Open `Assets/_Scenes/Menu_Main.unity`, press Play, and keep FrogletTools > Ecology > Evolution Monitor open. Fly the freestyle Garland world for 5 minutes and watch QuadFish, Brittlestars and the shark swim, graze, breed and hunt. With the Cell Selector toy swap to Lattice (tadpoles) and to Yggdra (sharks hunt tadpoles), then back. Release a Worm Colony from the Spawn Matrix (Fauna) and destroy one of its middle body segments. Shoot or joust a few creatures.
   - **PASS:** fauna behave as on bleeding-edge. They spawn, eat, breed, wither inward when starved and drop their hearts when killed, and nothing pops out. The cut worm splits into two colonies that swim apart. No new Console error or warning, and nothing containing 'evolution', 'genome' or 'ledger'. The monitor shows the yellow box "Cell N's biome (Garland) has Evolution > Enabled OFF", and after a swap it names Lattice or Yggdra.
   - **FAIL:** an exception from Fauna, WormFauna, Boid, LightFauna, Cell or CellLifeSpawnerBase. Visibly different swim speeds, breeding or worm splitting than on bleeding-edge. A census line while the switch is off.
6. **Switch-off regression, multiplayer** (tests the payload change that is always on; needs a second player via an MPPM virtual player or a second machine/build signed into a second account). Both peers run THIS branch. Form a party (host plus one client) and launch Wildlife Liberation (`Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity`, intensity 1). Play 3 minutes, kill at least one shark from each peer, and have a client join late once sharks exist.
   - **PASS:** the client sees the same sharks as the host (count, heart/element colours), and deaths wither on both screens. Neither Console shows 'deserialize', 'NetworkVariable', 'FaunaIdentity', 'ArgumentOutOfRange' or 'EndOfStream' errors. The late joiner sees the sharks correctly.
   - **FAIL:** sharks missing or wrongly coloured on the client, deserialization errors, or a client disconnect at spawn.
7. **Feature on: ledger and census.** Exit Play. Tick Evolution > Enabled, keeping the defaults, on `Garland Cell/Garland Cell Config.asset` (the boot world) and on `Yggdra Cell/Yggdra Cell Config.asset`. Open FrogletTools > Toolbox and in its Logging section tick 'Ecology'. Press Play in Menu_Main, open the Evolution Monitor, and play 5 to 10 minutes in Garland. Then swap to Yggdra with the Cell Selector.
   - **PASS:** a card per Garland species appears with the first wave: a green 'N LIVING' pill, founders equal to the seeded count, and four histograms around the centre line. Within 30 s a census trace starts, and filtering the Console for 'evolution census' shows one line per species about every 30 s. After creatures breed (3 feeds per birth in Garland), 'births' and 'max generation' rise above 0. After a starvation, a shark meal or a vessel kill, the matching count in the deaths row (starved / eaten / vessel / jousted) rises. After the swap the cards restart for Yggdra's species.
   - **FAIL:** no cards, or 'No lifeform recorded yet' after the first wave. Census lines more often than every 30 s, or never. Births never counted although creatures breed. Deaths always land in 'other'. Exceptions.
8. **Inheritance.** Exit Play. On Garland Cell Config set Founder Spread to 0 (Enabled stays on). Press Play and watch the monitor through the first births.
   - **PASS:** before any birth, every locus shows a single bar just right of the centre line and '± 0.00'. After births, new bars appear only in the bins next to the centre, no more than about two bins away in the first generations, and '±' becomes small but not zero (about 0.02 to 0.08).
   - **FAIL:** children spread across the whole histogram (a fresh founder roll), or '±' stays 0.00 after several births (no mutation).
9. **Expression is visible.** Exit Play. On Garland Cell Config set Founder Spread 0.5 and Tempo Upkeep Range 3. Press Play. In the Hierarchy click three 'QuadFish(Clone)' objects with the SAME heart colour and read the LightFauna component's 'Starvation Seconds' (under 'Population control (prey-linked)'). For speed: exit, and on `Lattice Cell Config` tick Enabled and set Tempo Pace Range 3 and Founder Spread 0.5. Play, swap to Lattice with the Cell Selector, click three 'TadPoleFauna(Clone)' and read the Boid 'Min Speed' / 'Max Speed'. Then untick Enabled on both configs, press Play again and repeat both reads.
   - **PASS:** with Enabled on, the three QuadFish show clearly different Starvation Seconds (tens of percent apart), and the tadpoles show different Max Speed (spread roughly from half to double the authored value). With Enabled off, same-colour QuadFish show identical Starvation Seconds and tadpoles identical speeds.
   - **FAIL:** identical values with Enabled on, a 0 or negative value, or differing values with Enabled off.
10. **A worm split expresses once and is a birth** (the fix in 9e505d38e). Exit Play. On Garland Cell Config keep Enabled on and set Founder Spread 0.5 and Tempo Upkeep Range 3. Press Play. In the freestyle world use the Spawn Matrix > Fauna > Worm Colony to release a colony. In the Hierarchy select its root 'WormColony(Clone)' (the object with the WormFauna component, not a segment) and note its 'Starvation Seconds'. With the vessel, destroy one BODY segment in the middle of the chain (not the head or the tail end) so the worm splits. Select the new root, which appears as 'WormColony(Clone)(Clone)', and read its 'Starvation Seconds'. In the Evolution Monitor, note the Worm Colony card's founders and births before and after the cut.
    - **PASS:** the new root's Starvation Seconds equals the original root's to the displayed precision (a cut keeps the genome). On the cut, the worm card's founders count does not change, births rises by one, and max generation is at least 1.
    - **FAIL:** the new root reads lower than the original, by the same upkeep factor again (the old double division, e.g. 51.4 then 44.0). Or the cut raises founders by one with births unchanged (the half filed as a new founder).
    - Optional, if a fed full-length colony fissions on its own: births rises by one, and the half's Starvation Seconds is close to its parent's but usually not equal, because its genome mutated. It is never the parent's value divided again.
11. **Export.** In Play mode with Enabled on and a few census rows recorded, click 'Export JSON' in the monitor. Save OUTSIDE the project folder (for example the Desktop) and open the file.
    - **PASS:** valid JSON containing "loci":["tempo","reach","fecundity","cohesion"], a "species" list naming the Garland configs, and a non-empty "rows" list.
    - **FAIL:** nothing written, malformed JSON (for example decimal commas), or an exception.
12. **Network with evolution on** (second player needed as in step 6). Exit Play. Tick Evolution > Enabled on `Wildlife Liberation Cell/Wildlife Liberation Cell Config 1.asset` and set Founder Spread 0.5 and Tempo Upkeep Range 3. BOTH peers must run with this edit: an MPPM virtual player shares the project's assets, but a second machine must make the same edit. Host plus client launch Wildlife Liberation intensity 1. On each peer, find the same shark by its NetworkObject 'NetworkObjectId' on a 'MassSharkFauna(Clone)' and compare the LightFauna 'Starvation Seconds'. Open the Evolution Monitor on the client.
    - **PASS:** the same shark shows the same Starvation Seconds on host and client, within about 1% (the genome rides the payload at 1/254 per gene). Different sharks differ from each other. On the client the shark card shows 0 founders and 0 births, or does not appear. Local-only species (for example worms) may still appear on the client: that is expected, because they are simulated and recorded on each peer.
    - **FAIL:** host and client differ by more than about 1% for the same shark, every shark is identical on the host, or the client records shark founders.
13. **Revert everything.** Exit Play. Discard every change under `Assets/_SO_Assets/Cell Configs/` (`git checkout -- "Assets/_SO_Assets/Cell Configs"` or Discard in GitHub Desktop). Untick the Ecology log channel if you like.
    - **PASS:** `git status` shows no modified .asset under Assets/_SO_Assets/Cell Configs, and `git grep -n "evolution:" -- "Assets/*.asset"` returns nothing.
    - **FAIL:** any Cell Config or Fauna Config still modified. Do not commit it: it would turn evolution on, or add an evolution block, in a shipped biome.
<!-- /steps:L -->

### Part M: Prisma engine APIs the parity probe compiles against

| | |
|---|---|
| Commits | 2: 93c5d74e7 e74aa701b |
| Ships to players | No. Port/ only. |
| Risk | Very low |
| Needs | None. |
| Push | Any time; first of M, H, I. |

**What it does.** One commit: the engine counterparts of three Unity APIs the parity probe (part H) uses: FMOD event description callbacks and a bank list, `Time.captureFramerate`, and the `AOT.MonoPInvokeCallback` attribute.

**Benefit.** Lets H land as a Unity-only push and I as a Port-only push, as Port/CLAUDE.md asks. Measured: bleeding-edge + M builds in Prisma, and bleeding-edge + M + H builds too.

**Risks.**

- None found: new engine API surface that nothing on bleeding-edge calls yet. *Very low.*

**Test steps.**

<!-- steps:M -->
No Unity: Port/ only, and nothing on `bleeding-edge` calls these APIs until part H lands.

1. On the branch the pick tool builds for M, run `cd Port && dotnet build src/CosmicShore.Player -nologo -v q`.
   **PASS:** `0 Error(s)`.
   **FAIL:** any error.
2. Run `COSMIC_SHORE_NET=off COSMIC_SHORE_AUDIO=off dotnet test tests/CosmicShore.Tests -nologo -v q` from `Port/`.
   **PASS:** every test passes.
   **FAIL:** any failure; record its name.
3. Run `python3 Port/tools/check_unity_isolation.py` on that branch.
   **PASS:** no file outside `Port/` is named.
   **FAIL:** a Unity file is listed (the branch picked more than M).
<!-- /steps:M -->

## 5. Pushing only what passed

`Tools/Build/pick_branch_parts.py` builds a branch from chosen parts on top of `bleeding-edge`,
without touching your checkout, and never pushes.

```sh
git fetch origin
python3 Tools/Build/pick_branch_parts.py check          # every branch commit is in a part
python3 Tools/Build/pick_branch_parts.py list           # parts, commits, dependencies
python3 Tools/Build/pick_branch_parts.py build A C F K --branch push/ci-steam-skimrace-leaks
git log --oneline origin/bleeding-edge..push/ci-steam-skimrace-leaks
git push -u origin push/ci-steam-skimrace-leaks         # then open the PR with the printed body
```

What `build` does for you:

- It adds the parts a chosen part cannot compile without: **G needs E and M**, **H needs M**, and
  **I needs H and M**. It says when it adds one, and skips a commit that has already landed.
- It warns when part **A** is left out. `bleeding-edge`'s guard has been red at "Validate project"
  on every push since 2026-10-08, and A is the fix, so a branch without A goes red for that reason
  alone.
- It warns when part **D** is picked without the parts its QA items describe.
- It cherry-picks in history order. Documentation conflicts keep `bleeding-edge`'s text, because
  every part appended to the same running docs.
- It resets `Docs/UNITY_VERIFICATION_CHECKLIST.md` to `bleeding-edge`'s text and files ONE section
  with exactly the chosen parts' test steps from this document. A selective push files what it
  carries and nothing else.
- It drops a folder `.meta` whose folder the branch does not carry (F's commit carries two of H's).
- When D is picked, it 3-way merges this branch's QA backlog changes onto `bleeding-edge`'s current
  backlog, because a plain pick would keep the base's copy and drop the whole refresh.
- It stops on any code or asset conflict. None existed on 2026-10-12, so one means `bleeding-edge`
  moved; resolve it in the worktree it names.

### Suggested order

1. **Now, no editor needed:** A (the guard goes green again), C, M. Each is a one-command build.
2. **After the sound designer saves:** B.
3. **After their editor steps pass:** K, F, E then G, J, L. Each can go alone; G needs E and M.
4. **Parity:** H after M has landed, then I after H. Pushing H and I together is allowed but breaks the Port rule; say so in the pull request if you do.
5. **Last:** D, with or after the parts its QA items describe, then run `/qa-backlog` once on `bleeding-edge`.

```sh
python3 Tools/Build/pick_branch_parts.py build A C M --branch push/ci-steam-engine-apis
python3 Tools/Build/pick_branch_parts.py build K --branch push/leak-fixes
python3 Tools/Build/pick_branch_parts.py build E G --branch push/ai-training-and-load-time
```

When every part has landed, delete this document and `Tools/Build/pick_branch_parts.py`.

## 6. What needs a person, not a test

- **Sound designer, before part B.** Open `Cosmic Shore/Cosmic Shore.fspro`, decide the 'Squirel boost' track volumes (`Docs/GITHUB_HYGIENE_REPORT.md` §1.1 records both states), save, build and commit `Metadata/` and `Build/`. Pulling B deletes the unsaved state.
- **Owner sign-off for part L.** Two design calls: the LOCKED rule L added to `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md` ("the genome is a lean, not a level"), and the review fix that makes a worm's natural fission a mutating birth through `Cell.OffspringPick` while a player's cut keeps the genome.
- **Announce part A's generator gate.** After A lands, an Inspector edit to any generator-owned asset turns `bleeding-edge` red. Designers change the `Tools/Build/author_*.py` generator instead.
- **Part E's Learn still overwrites the deployed genome.** Pinning a tournament pick while Learn runs needs a runner change (an archive entry Learn never overwrites). Until then, discard `Archive.asset` after any Learn.
- **Part J's Add Friend reaches strangers only by full tag.** Players must share their Name#1234 out of band. A share or copy button is the obvious follow-up.
- **Accounts and machines.** Part J needs two UGS accounts on two machines (or MPPM); part H's capture needs a throwaway returning-user account named exactly `parity`.
- **Steamworks.** Part C's last step waits for checklist items A2 and A4 in Steamworks.
- **Weekly routines.** The Sunday AI-training routine now cuts its branch from `cece/nice-brahmagupta-ojclij` until parts E and I land, because `bleeding-edge` lacks `--train export`, `--resume-run` and the bot profile; its 2026-10-11 run hung on the first-run prompts for that reason. The Wednesday Skim Race tune reads sections 6.13 and 6.14 from this branch until F and D land.
- **Optional CI.** `dotnet test Port/tests/CosmicShore.AssetTool.Tests` (25 tests, about 12 s warm, 50 s cold) runs in no workflow; adding it to `prisma-parity-ci.yml` is cheap.
- **Not this branch's.** `bleeding-edge` fails 35 EditMode tests today (Docs/QA/DEV_TASKS.md DT-003); `Docs/UNITY_VERIFICATION_CHECKLIST.md` names `FrogletTools > Benchmarks > Crystal Flip Wave Benchmark`, which is now under Performance.
