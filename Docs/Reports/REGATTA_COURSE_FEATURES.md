# Regatta course features: the wider course space (2026-11-06)

Brief 10 of the 2026-11-06 Regatta balance plan (`Plan step: time-arena-course`), asked by Garrett: "the super shielded
tracks could have different sections, gaps, not all supershielded, there could be omnicrystals ... perturb regatta in
much more interesting ways ... search for multiple local minimums". Before this, the course search could move the ring
geometry and the seed, and (on the unmerged `claude/regatta-overnight-balance` branch) one neutral share for every leg.

**Every number below is a MODEL.** No Unity editor ran (the cloud sessions have none); the real-game checks are in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Regatta course features").

## 1. What the course can now be

`RegattaCourseFeatures` (`Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourseFeatures.cs`), a serialized field on each
`SpawnableRegattaRails` variant (so a pool entry, brief 13, carries it with its cell config):

| Dimension | What it is in the game | Default |
|---|---|---|
| Rail kind per stretch | super-shielded (today), shielded, or plain: a plain rail can be cut by abilities, so a rider's stretch is contested | super-shielded everywhere |
| Neutral colour per stretch | Blue, no team's lane: an Urchin leaves its rail and flies free, a Squirrel still skims it | team colour everywhere |
| Gap of a chosen length | nothing laid; the lane is split into OPEN trails there so a rider runs off the end (a hole inside one trail is bridged at full pace) | none |
| Detour | the whole braid bows off the spine (sine-squared, zero at the section's ends, so every lane still threads every ring at 22 u) | none |
| Alternate ring per gate | a second ring in the course ring's plane, either one counts; the rails thread only the course ring | none |
| Crystals on the line | at most 4 a lap, on the spine, laid as the crystal manager's course keys (they replace the nucleus crystals while on); no AI detours for one | none |

The empty set is today's course, prism for prism: the course harness output (`run.sh`), `course.js` (`export_course.sh
--check`) and the measured course are byte-identical, `course_port.py --self-test` and `author_regatta_assets.py --check`
pass. The only change to `regatta_course_measurements.json` is its `sourceHash` (the new file joins its `sources`).

The AI side: an AI takes an alternate ring only when here -> ring -> next gate is shorter by `aiAlternateMargin` (5%),
decided once per gate (`RegattaAlternateChoice`, the same rule in the model); an attached Urchin and the Squirrel's pilot
keep the course ring. Every reader of the rails (the controller's own-lane aim and race lines, the Skim Race pilot's
Regatta course, the human flight recorder) now reads them by LANE (`SpawnableRegattaRails.LanePrisms`, `LaneDomain`), so a
gap or a neutral first prism cannot hand a pilot the wrong lane.

## 2. What reaches which hull (the model)

`python3 Tools/Compute/evaluators/regatta_features.py --reach`: seconds a probe feature adds to a 3-lap race, mean of the
four shipped circuits. Roster: bleeding-edge's `regatta-course` job roster (Urchin `rail`, Rhino / Manta / Dolphin their
drive sims, Squirrel the competent-pilot lap model) and the competent-pilot model for the Serpent, Sparrow and Stoat, which
have no Regatta AI model on this branch. The measured roster is on the overnight branch; brief 12 reruns this on it.

| feature | Urchin | Squirrel | Rhino | Manta | Dolphin | Serpent | Sparrow | Stoat |
|---|---|---|---|---|---|---|---|---|
| (race today, s) | 55.3 | 55.4 | 62.5 | 31.5 | 65.8 | 275.4 | 122.5 | 111.8 |
| neutral 50% of every leg | **+112.0** | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| plain rail 50% of every leg (grid) | +13.8 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| a 120 u gap mid every leg | **+48.9** | +0.2 | 0 | 0 | 0 | 0 | 0 | 0 |
| a 100 u detour on every leg | +4.2 | +4.2 | +4.7 | +2.3 | +4.9 | 0 | 0 | 0 |
| an alternate inside every corner (250 u) | 0 | 0 | -15.7 | -8.0 | -16.6 | **-71.2** | -31.7 | -28.9 |
| 4 crystals on the line | 0 | 0 | 0 | -1.6 | 0 | 0 | -10.3 | **-18.5** |

What it says:
- **Neutral stretches and gaps are the levers on the Urchin alone.** The rider's whole advantage is its 300 u/s grind
  against a ~65 u/s free flight, so every metre of rail it cannot ride costs it about 4.6x. A gap also costs a re-attach.
- **Gaps do not reach the Squirrel.** Its skim boost decays at its prefab's `BoostDecayRate` (0.3 a second, 18 u/s^2 at
  its 60 throttle scaler), so even a 240 u gap costs it a fraction of a second. If the Squirrel needs slowing, a gap is not
  the tool; nothing in this space reaches it except a detour.
- **Plain rail is a weak, grid-only lever**, and its size is the model's guess at how much of a contested stretch rivals
  have cut (50% plain, 10% shielded). Alone, it costs nothing.
- **Detours reach everyone who follows the braid about equally** (the riders and the race-line AIs, +2 to +5 s), so they
  lengthen a race without balancing it. They reach only a human (or a competent pilot) as a route choice: fly the spine.
- **Alternate rings are the lever on the slow free-flyers**: the hulls that do not ride gain most where a corner is cut
  (Serpent -71 s, Sparrow -32 s, Stoat -29 s), the riders gain nothing. With neutral stretches this gives the search two
  independent knobs pulling the fastest (Urchin) and the slowest (Serpent, Sparrow) together.
- **Line crystals are not noise for the hulls Time reaches** in this model (Stoat -18.5 s, Sparrow -10.3 s at 4 a lap),
  and nothing for the Urchin, Squirrel and Rhino, which no element reaches. Their size rests on two guesses (a crystal is
  1 Time level, a racer collects half the crystals it passes); Garrett's "sparingly" is the cap of 4.

The model's assumptions are `KNOBS` in `regatta_features.py`, each named with what would measure it.

## 3. The search space

`regatta_features.space()`: per leg one section (start, length, kind, neutral, detour, bearing) and a gap of a chosen
LENGTH in u centred on it (kept off the rings); per ring an alternate (offset, bearing, mouth scale); crystals per lap and
phase. 82 numbers, and every init value is today's course (`features_from_params` leaves out anything that changes
nothing). `regatta_course.py` (VERSION 3) searches it with `features: true`: each hull is priced by
`regatta_features.race` (exactly `race_models.race` on an empty set), and the guard becomes `guard_fails` (every rail
through every mouth over the LAID lanes, nothing inside the nucleus or out at the membrane). `descriptors()` gives brief
12's quality-diversity axes (ridable share, contested share, gaps, detour, alternates, crystals).

Job `Docs/Compute/jobs/regatta-course-features.json` (status `proposed`, geometry + features, 98 numbers; Froglet Compute is
paused, so it runs by hand with `/mnt/project-files/regatta-training/local_run.py`).

## 4. Pipeline run (a dry run, not a result)

Three independent restarts (A, B, C) of `regatta-course-features` at I1, 600 evaluations each, on today's untrained AI
models. They prove the job runs end to end on the wider space; they are far from converged (98 numbers want tens of
thousands of evaluations), and by the plan's order no course is picked until the AIs are well trained. Results:
`/mnt/project-files/regatta-training/results/course-features/`.

| restart | best loss (shipped 0.778) | spread | Urchin | Squirrel | Rhino | Manta | Dolphin | sections | gaps | alternates | crystals |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A | 0.836 | 2.31x | 71.8 | 54.6 | 49.9 | 31.1 | 68.1 | 13 | 5 | 8 | 0 |
| B | 1.049 | 2.86x | 78.8 | 52.5 | 54.3 | 27.6 | 59.7 | 12 | 4 | 8 | 0 |
| C | 1.020 | 2.77x | 73.5 | 49.3 | 58.2 | 26.5 | 56.8 | 15 | 7 | 7 | 0 |

(seconds for 3 laps at I1, the job's five-hull roster; no DNF and no rail failing a mouth in any of them.)

What it shows: none beats today's course yet, and the three disagree on the geometry, so this is not a result. They do
agree on the direction the features pull: all three slow the Urchin (+16 to +23 s against today's 55 s) with neutral
stretches and gaps, keep about 90-95% of the lane ridable, open an alternate at nearly every ring, and lay no line
crystals (nothing on this roster is a Time hull). The spread stays above 2x because of the Manta drive sim, which
`MODEL_LIMITS` says runs about 3x faster than the game's AI Mantas; brief 12 reruns this on the measured roster.

## 5. How it is held

- `bash Tools/Build/regatta_course_harness/features.sh [--check]`: compiles the pure course files with the harness's
  UnityEngine stub and a minimal NUnit stub, RUNS every test in `RegattaCourseFeaturesTests` (9/9), and writes
  `features.json`, the C# fixture (4 feature sets x 4 intensities: lane points, leg params, every prism's plan, runs,
  alternate rings, line crystals).
- `python3 Tools/Compute/evaluators/regatta_features.py --self-test`: the port reproduces `features.json`; three negative
  controls (a moved detour, gap and alternate) must fail it; the empty set changes no hull's time; each feature moves the
  hulls the table says in the direction it says; the guard catches a gap at a ring.
- `regatta_course.self_test()`: the featured space at its init values scores today's course; a neutral rail on every leg
  must slow the Urchin (the features reach the loss).

## 6. Known limits

- No Unity editor ran. The game-side features (trails split at gaps, plain prisms cut in play, the alternate ring's
  crossing and lighting, line crystals as course keys, two-machine agreement) are on the verification checklist.
- The Squirrel AI's contact shell for a plain or shielded stretch is still sized as a super-shield's
  (`RegattaRingObjective`, `SkimRaceShell.SuperShieldHalfScale`), so it may skim such a stretch a little wide.
- The Serpent, Sparrow and Stoat are competent pilots in this table; in the game they fly race lines, which follow the
  braid, so a detour costs them about what it costs the Rhino and the Dolphin.
- The Manta drive sim is about 3x faster than the editor's AI Mantas (race_models' `MODEL_LIMITS`); its row is relative.
