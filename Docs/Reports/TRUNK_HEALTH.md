# Trunk health: every time bleeding-edge went red

The team's history of red trunks. A red trunk is a red **Bleeding-edge guard** run on `bleeding-edge`
(`.github/workflows/bleeding-edge-guard.yml`). Kept by `/trunk-doctor` (`.claude/skills/trunk-doctor/SKILL.md` §7):
each incident gets a row when it is repaired.

**How to read it.** "Went red" is the first red run of a streak, and "Back green" the first green run after it.
Cancelled runs (a newer push superseded them) carry no verdict and are skipped, unlike the guard's report job before
2026-11-12. "First red push" is the PR (or the commit, for a direct push) of the first red run. "Pushed by" is whoever
pushed or merged it, which is not always the author: `trunk_doctor.py diagnose --run <id>` names both. "Pushes while
red" counts every push after the first red one until green. "Cause" is the failure class and finding of the first
red run, plus the last red run's when the cause changed inside the streak.

**How to refresh it.** `python3 .claude/skills/trunk-doctor/trunk_doctor.py history --pages 9 --diagnose` prints the
measured table below from the GitHub API. Copy new rows, then add the fix by hand in the Incidents section. Logs older
than 90 days are gone, so an old row keeps its cause only if it was recorded here in time.

## Summary (measured 2026-11-12, 779 guard runs, 2026-08-07 to 2026-11-12)

- **17 red streaks**, **245 pushes landed while the trunk was red**: 14 per streak on average, 71 in the worst.
- **Most of the red time came from five streaks**: 10-19 (6d 10h, 71 pushes), 10-08 (5d 6h, 39), 10-28 (3d 23h, 48),
  11-09 (still red at 2d 17h, 31) and 09-29 (2d 7h, 6). In each, more work merged on top instead of the trunk being
  fixed first.
- **By class**, counting the first red run: compile 7 (all before the Unity runner was removed on 2026-10-13),
  infra 4 (licence, runner lost), generator 3, static 2, and 1 whose log has expired. Since 10-13 the guard can only see static and generator
  failures (`Docs/BUILD_AND_DELIVERY.md` 10.1).
- **The tracking issue cannot be trusted as a signal.** Until 2026-11-12 the report job closed it on a cancelled run
  ("Recovered ... verified clean") while the trunk was red: #1867, #1872, #1889 and #1897. Fixed in the guard by the
  `/trunk-doctor` PR.

## Measured history

Output of `trunk_doctor.py history --pages 9 --diagnose`, 2026-11-12 16:50 UTC:

| Went red (UTC) | Back green | Red for | First red push | Pushed by | Pushes while red | Cause |
|---|---|---|---|---|---|---|
| 2026-08-08 02:26 | 2026-08-08 17:04 | 14h 38m | 77abf38d0b | Shombith (Shombith03) | 3 | log expired (GitHub keeps 90 days) |
| 2026-08-17 22:43 | 2026-08-18 14:13 | 15h 30m | #740 | Garrett (gradies) | 8 | compile: CS0104 VesselAbilityRowWirer.cs:176; CS0104 VesselAbilityRowWirer.cs:211 |
| 2026-08-25 03:41 | 2026-08-25 09:36 | 5h 55m | #786 | Garrett (gradies) | 6 | infra: runner lost |
| 2026-09-02 00:35 | 2026-09-02 01:08 | 0h 32m | e44a21a22f | Shombith (Shombith03) | 3 | compile: CS0246 DailyChallengeSystem.cs:30, :161, :29 (#826); at the end: compile: CS0103 EndConditionOverridesSO.cs:166; CS0117 ArcadeGameConfigureModal.cs:2416 (#829) |
| 2026-09-07 16:15 | 2026-09-07 17:19 | 1h 3m | #837 | Garrett (gradies) | 1 | compile: CS0535 SparrowCombatTierTests.cs:504 |
| 2026-09-11 04:01 | 2026-09-11 16:38 | 12h 37m | 5b5c7e5015 | Shombith (Shombith03) | 1 | infra: runner lost; at the end: compile: CS7036 MultiplayerMiniGameControllerBase.cs:423, :502 |
| 2026-09-11 20:59 | 2026-09-11 21:05 | 0h 6m | 97698d7956 | Shombith (Shombith03) | 1 | compile: CS0176 ArcadeLobbyList.cs:591 (#870) |
| 2026-09-12 22:11 | 2026-09-12 22:41 | 0h 30m | c3f99d26ed | Shombith (Shombith03) | 0 | compile: CS0246 ProfileModal.cs:359 (#878) |
| 2026-09-29 15:48 | 2026-10-01 23:09 | 2d 7h 21m | #931 | Garrett (gradies) | 6 | static: check_console_logging.py: Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs:710 |
| 2026-10-04 20:59 | 2026-10-05 16:07 | 19h 7m | #951 | Yash (YsKhan61) | 5 | compile: compile errors (not printed in the job log) |
| 2026-10-06 12:58 | 2026-10-06 15:31 | 2h 33m | #965 | Garrett (gradies) | 0 | compile: compile errors (not printed in the job log) |
| 2026-10-06 17:31 | 2026-10-08 03:58 | 1d 10h 27m | 5ec67afe0d | aradia1 | 22 | infra: licence |
| 2026-10-08 07:41 | 2026-10-13 13:58 | 5d 6h 17m | #1011 | Garrett (gradies) | 39 | infra: licence (#1025, reported as "did not compile"); at the end: static: validate_project.py: Port/tests/CosmicShore.Tests/ShaderGraphCompilerTests.cs:178; Port/src/CosmicShore.Content/Shaders/ShaderGraphAsset.cs:68 (#1037) |
| 2026-10-19 13:47 | 2026-10-25 23:47 | 6d 9h 59m | #1153 | Shombith (Shombith03) | 71 | static: the validators' self-tests: "FAIL the shipped Joust drift is rejected" (check_hud_onscreen.py) |
| 2026-10-28 23:28 | 2026-11-01 22:15 | 3d 22h 46m | #1630 | Yash (YsKhan61) | 48 | generator: author_meerkat_assets.py (drift); at the end: author_regatta_assets.py (drift) (#1631, #1681; Docs/Reports/CI.md) |
| 2026-11-06 19:28 | 2026-11-06 19:55 | 0h 27m | #1766 | Shombith (Shombith03) | 0 | generator: author_payload_assets.py (drift); author_thresher_assets.py (drift) (#1767) |
| 2026-11-09 23:38 | still red | 2d 17h 1m | #1863 | Shombith (Shombith03) | 31 | generator: author_flatline_assets.py (crash: numpy missing); at the end: + author_corona_assets.py, author_shatterplane_assets.py (crash: numpy missing) (#1867, #1872, #1889, #1897) |

## Incidents

One entry per incident `/trunk-doctor` handled, newest first. Shape: went red, cause, introduced by, red for, fix,
pushes while red, notes.

### 2026-11-09 23:38 UTC: three generators crash on the runner (numpy missing)

- **Went red:** run 764, `7d3ad2790d`, #1863 (`claude/great-rubin-rur3xs`, the Flatline mode), merged by Shombith.
  Last green: run 763, `1243255b1a`, 2026-11-09 23:19.
- **Cause (class: generator, crash):** `author_flatline_assets.py --check` imports `flatline_budget.py`, which imports
  numpy (`Tools/Build/flatline_budget.py:29`) to mirror the C# in float32. The GitHub-hosted image ships PyYAML but no
  numpy, so the check died with `ModuleNotFoundError: No module named 'numpy'`. Nothing had drifted. Two more
  generators broke the same way behind it, invisible because the trunk was already red:
  - `author_corona_assets.py` (`corona_budget.py:23`), first red in run 769, #1874 (`claude/amazing-goldberg-7zn80e`,
    Corona);
  - `author_shatterplane_assets.py` (`shatterplane_budget.py:31`), first red in run 774, #1883
    (`claude/fervent-edison-ol600q`, Shatterplane).
  All three named by `trunk_doctor.py diagnose --run 43257273901`.
- **Why nobody noticed:** every developer's Python has numpy, so `gates.sh` passed locally. The tracking issue was
  closed four times by cancelled runs (#1867 at 00:24 on 11-10, #1872, #1889, #1897 at 16:29 on 11-12), so it read
  as "fixed" while the trunk was still red.
- **Pushes while red:** 31 (to 2026-11-12 16:28): Shombith 19, Garrett 11, Caleb 1.
- **Fix (forward, small and certain):** `Tools/CI/requirements-checks.txt` (`numpy>=2,<3`; numpy 2's float32
  promotion is what the committed layouts verify on). The guard's validate job, its autofix prompt and
  `unity-ci.yml`'s static job install it before the checks. Reproduced with a Python that has PyYAML and no numpy (all
  three RED); passes with a fresh wheel-only numpy installed from that file (`--check: OK`, 46, 48 and 50 files). No
  generator was skipped or loosened. Second fix in the same PR: the report job no longer treats a cancelled run as a
  recovery. Both land with the `/trunk-doctor` PR.
- **A fourth finding, hidden behind the first (found 2026-11-13, 56 pushes in):** `author_kelpwood_assets.py --check`
  went red in run 813 (2026-11-13 00:40), introduced by froglet-studio/Cosmic-Shore#1934 (`claude/angler-kit`,
  merged by Garrett). The Angler's bite cooldown became element-scaled (`config.BiteCooldownFor(_status)` in place of
  `config.LockBiteCooldown`), and Kelpwood's load-bearing-assumption check matched the old call literally. Kelpwood
  reads no cooldown, only that a bite is still admitted as a Strike through `TryAdmit`, which still holds. Fix
  forward on the same PR: the needle names the new call. Reproduced red (`VALIDATION FAILED`), then `--check: OK, 60
  file(s) match`; a wrong needle still fails (exit 1). Nobody saw it because the trunk had been red for three days.
- **Red for:** still red at the time of writing. Update this line with the green run once the repair merges
  (`trunk_doctor.py` §5).
