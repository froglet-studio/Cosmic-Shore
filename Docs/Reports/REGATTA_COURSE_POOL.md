# Regatta's shuffled course pool (2026-11-07)

Plan step: `time-arena-course`. Brief 13 of the Regatta balancing workstreams. Asked by Garrett (2026-11-06): one
Regatta level, no intensities, several balanced course variants the game shuffles between.

## What changed

- **One level, a pool of circuits.** `Assets/_SO_Assets/Cell Configs/Regatta Cell/Regatta Course Pool.asset`
  (`RegattaCoursePoolSO`) lists the circuits a Regatta match can lay. Today it holds the four circuits that used to
  be the intensity ladder (Wide, Mixed, Hairpins, Tight). An entry is a Regatta cell config; the config's rails
  prefab carries the circuit, so the course data did not move out of `RegattaCourse` and the prefab.
- **The host draws, every machine lays the same course.** `RegattaController.DrawCourseVariant` deals from a shuffle
  bag (`CourseShuffleBag`: every enabled circuit once before any repeats, never the same one twice running). The
  draw is stored in `GameDataSO.CourseVariant` and sent in `SyncGameConfigToClients_ClientRpc`. The scene's Cell uses
  the new `CellTypeChoiceOptions.CourseVariantWise` and lays `CellConfigs[CourseVariant]`. It waits until the
  variant is known on that machine (on the host too), so no machine can latch a different arena.
- **Intensity no longer picks the course.** Nothing on the Regatta course path reads `SelectedIntensity` now.
- **Pins.** `CourseVariantOverride.Forced` pins the next match's circuit (the replay sets it from the summary's new
  `CoursePoolEntry`, one-based; a benchmark can set it). Human flight records gain `courseVariant`.
- **Authoring.** `Tools/Build/author_regatta_assets.py` writes the pool, the new scripts' metas, the scene's Cell
  block (`cellTypeChoiceOptions: 3`) and the controller's `coursePool` field, and checks the pool's entries are the
  Cell's configs in order and are different circuits. Negative controls: setting the scene back to IntensityWise,
  and making two entries the same circuit, both fail `--check`.

## What ran

| check | result |
|---|---|
| `author_regatta_assets.py --check` | OK, 49 files; both negative controls fail it as intended |
| `unity_refcompile/run.sh` (player) | 0 errors in changed files; the 21 errors it reports are on bleeding-edge already (Vivox and Moderation packages the cloud fetch cannot get). Negative control: a typo in `RegattaCoursePoolSO.cs` is caught and tagged |
| `unity_refcompile/run.sh --config editor` | 0 errors in changed files, including the new test file |
| `regatta_course_harness/export_course.sh --check`, `course_port.py --self-test` | unchanged and passing (no pure course file was touched) |
| `check_console_logging.py`, `check_conditional_compilation.py` | pass |
| `.claude/skills/ship/gates.sh` | see the PR |
| `RegattaCoursePoolTests` (EditMode) | written, **not run**: there is no Unity editor in the cloud |

## Not verified (needs the Unity editor)

1. The EditMode tests `RegattaCoursePoolTests` and `RegattaCourseTests`.
2. A Regatta match alone: the log line `[Regatta] Course variant k (...)` (ArcadeMatch channel) and the circuit laid
   match; three matches in a row lay three different circuits.
3. A two-machine match: host and client lay the same circuit (same ring positions, same rails).
4. Watching a Regatta replay re-lays the recorded circuit.

## Left to other work

- The card's `StartingElements` rows are still keyed by intensity, though intensity no longer picks the circuit they
  were measured on. Removing intensity from the arena launch is the "Replace arena intensities with vessel selection
  modes" thread's.
- New balanced circuits come from the pool search (brief 12) and the wider course space (brief 10); each joins as a
  rails prefab, a cell config and a `POOL_ENTRIES` row in the generator.
- `RegattaHullBenchmark` (on the unmerged `claude/regatta-overnight-balance`) picks a circuit by intensity; once this
  lands it should pin `CourseVariantOverride.Forced` instead.
