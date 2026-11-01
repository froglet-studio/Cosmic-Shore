# Froglet Compute: where it stands (report for the team)

**From:** Shombith's sessions, 2026-10-28 to 2026-11-01 (started by a session for Garrett, 2026-10-26). **Branch:**
`claude/eager-franklin-e6g77h`, PR #1659 into `bleeding-edge` (open; not merged). **Status:** the live console runs on
the team's Oracle VM (https://130-210-34-146.sslip.io) with dragonsweapon on it; everything is checked offline
(`compute.py self-test` green, the trainer compiled against the Unity references, the console driven in Chromium) and
**no Unity trainer run has been watched end to end in the editor since the seed, the diagnostics and Update AI were
added** (section 3). Tooling, so changes go through Shombith (CLAUDE.md).

**2026-11-01, Shombith's question answered in code** ("users play a new mode a few times, that data is recorded and
reused by the AI in a compute run, and each vessel has an Update AI button"): see "The two learning paths" below.

## 1. In one minute

- **What it is:** a queue of learning searches that every collaborator's computer works through whenever its person
  is away. A search is a parametric space (the dials) and a loss (how good a setting is). Machines share the best
  answers through git, and one board merges them. Design: `Docs/Compute/README.md`.
- **Set and forget:** `python3 Tools/Compute/compute.py install` (Windows Task Scheduler on idle, macOS LaunchAgent,
  Linux systemd). It waits for 10 minutes idle on mains power, runs at the lowest priority, stops when you touch the
  machine, never touches your checkout, and publishes only to `compute/<you>/<machine>`.
- **What it searches today (12 jobs):**
  - **The Regatta balance plan** (Garrett set it, Yash follows it; `Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md`):
    stage 1 AI drives (Rhino in Headlong, Manta in Redline, and the Regatta variants: ready), stage 2 the Regatta course
    that levels the trained AIs (four jobs, blocked on stage 1), stage 3 the smallest elemental baseline (blocked on
    stage 2). Today the models put the slowest AI at 2.05-2.18x the fastest's time (the Manta is twice as fast).
  - **Bioforms:** a MAP-Elites forge of new swarm bodies (ready), and NCA trainings of the best of them (proposed,
    needs torch and the first forged targets).
- **It finds its own work:** the scout's lenses (coverage of every vessel's Time games x I1-I4, the plans, bioform
  diversity, hand-rolled sweeps, labs, numbers set by hand, board health) listed 90 opportunities on its first run.
  A daily scout sweep (a scheduled task, README section 9) keeps the queue and the board current.

- **The two learning paths, joined (2026-11-01, `Docs/Compute/README.md` section 20).** People's editor races were
  already recorded and learned hourly by Yash's sweep; compute runs bred the trainer's AIs from defaults; nothing a
  run found reached the game. Now every Unity trainer run starts from a **people's seed** (the throttle and boost
  habits measured from the people's own races of that pair, `people_seed.py`, shown on the run), and the console's
  **Vessel AIs** page has **Update AI** per vessel: the best trained genome per mode and intensity goes into the
  game's archive on the `ai` branch (the asset every normal match deploys), with one of the core four's yes recorded
  in the commit. The shipped game changes when `ai`'s pull request merges.
- **What a run looks like now (2026-10-31 to 11-01):** a sent run shows as Starting within a minute with a live bar;
  New run takes every intensity (one run per level) and one match; Show the game opens Unity's window on any PC and
  the run records the game's diagnostics and sends them with its console errors through the bug ledger; Serpentine,
  Scarab Rally and Brittle Star Time train; the console wears the Silver Screen look.

## 2. Try it

1. `python3 Tools/Compute/compute.py doctor` - what your machine can run.
2. `python3 Tools/Compute/compute.py top --why` - the queue and what your machine would run next.
3. `python3 Tools/Compute/compute.py run --once` - one session by hand (it waits until you are idle; `--always` to start now).
4. `python3 Tools/Compute/compute.py board` - what every machine found.
5. Happy with it: `python3 Tools/Compute/compute.py install`.

3. **Update AI, from a browser:** the live console > Vessel AIs. Three vessels (Butterfly, Dolphin, Sparrow) already
   have trained genomes from dragonsweapon's first runs (fitness negative: they predate the next-gate fix, so they
   scored no gates). Press Update AI, pick your name, and `origin/ai` gets the commit; the page says when it is in.
4. **The people's seed:** race Squirrel in Skim Race twice in the editor (the recorder sends them by itself), then send
   a Unity run of that pair from the console: the run's row on Vessel AIs says "seeded: N gene(s) from the people's
   races".

## 3. What is untested

- **The people's seed inside Unity** (the trainer reads `-trainSeed`, compiled, never run) and **Update AI's commit
  deployed** (a real push to `ai` and a match on that archive): the checklist has both.

- The installers on Windows and macOS, and a real push to `compute/*` on origin (the self-test pushes to a throwaway
  remote).
- Any torch job (the NCA training command job); the relay of a unit between two real machines.
- Every evaluator is an offline model; nothing found is true until an editor race says so.

## 4. Open items, and what is asked of whom

**Handed to Shombith entirely (Garrett, 2026-10-26).** His starting point: `Docs/Compute/HANDOFF.md`.

- **Shombith (tooling):** review `Tools/Compute` and the five new skills before the merge; decide whether the daily
  scout sweep may keep pushing `Docs/Compute/**` to its own branch, `compute-queue`.
- **Yash (Regatta AI):** the stage-1 results are proposals for you; the plan's decision 1 (what spread counts as level).
- **Garrett (swarms, NCA):** which forged bodies are worth wiring or training.
- **Anyone with a Windows or macOS machine:** be the first `install` and say what happened.

## 5. Latest activity (generated)

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-01 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area compute --days 14`._

### Recent changes (25+)
- 2026-11-01 `0ab950ad2` style(compute): the console in Silver Screen - film-still black and white, movie-poster headings, typewriter labels — on another branch
- 2026-10-31 `13d29aef5` feat(compute): New run at every intensity, one match, the game shown on any PC with its diagnostics sent; the three Time races join the trainer — on another branch
- 2026-10-31 `4123efa81` feat(compute): a sent run shows at once as Starting; the hub syncs the ai branch hourly (Rev 41, section 109) — on another branch
- 2026-10-31 `4dac04568` compute: request r20261031-224826-16e9 (unity-train) — on another branch
- 2026-10-31 `d66da69bf` compute: request r20261031-224836-9a6a (unity-train) — on another branch
- 2026-10-31 `5efa7f34b` fix(compute): the VM script's bleeding-edge probe is silent (its 404 read as a failure) — on another branch
- 2026-10-31 `ce02469bc` fix(compute): ship review - the VM picks bleeding-edge once merged, BRANCH names this branch, two counts re-derived — on another branch
- 2026-10-31 `f4b23db2a` docs(compute): ship pass - the README's sections in CLAUDE.md, /compute traps, follow-up rows, the area report — on another branch
- 2026-10-29 `03b31eb69` compute: request r20261030-024007-46ba (unity-train) — on another branch
- 2026-10-29 `dc81fb56a` compute: request r20261030-022415-eba4 (unity-train) — on another branch
- 2026-10-29 `88a0ca210` fix(compute): the app opens at once and waits for its data instead of failing (HTTP 503) — on another branch
- 2026-10-29 `d94833762` feat(compute): the app - Froglet Compute on your own computer, a run starts here at once — on another branch
- 2026-10-29 `746e91da7` fix(compute): the console finds its jobs in a fresh session (the hourly sync showed none) — on another branch
- 2026-10-29 `fc4ef88ce` fix(ai): review fixes - a trainer match's reload tail is the trainer's; stills taken in the match; reports skip trainer flights — on another branch
- 2026-10-29 `b079ef27d` fix(ai): the trainer's seat is AI everywhere; learners skip flights no person flew; Third Eye reads the trainer, saves its view — on another branch
- 2026-10-29 `c415c5e9a` feat(compute): Unity runs keep the game's replays, keep the trainer's flights out of people's races, open the Third Eye — on another branch
- 2026-10-29 `dc2ce4013` fix(compute): the VM setup clones one branch, proves the token can push, says where it stopped — on another branch
- 2026-10-29 `e2ccfa129` fix(compute): the ai sync replays Ys file by file and settles only copies a side has moved past — on another branch
- 2026-10-29 `82182ac8d` fix(compute): the ai sync takes Ys work that reached bleeding-edge without calling it a conflict — on another branch
- 2026-10-29 `7b3b715a9` chore(compute): Compute skips Astro League (Docs/Compute/skip_modes.json) — on another branch
- 2026-10-29 `a494df9a8` feat(compute): the console is an application - Linear's shell, Tailscale's machine list, Optuna's study view — on another branch
- 2026-10-28 `a79ce61d5` fix(compute): Stop reaches every run, the page sends without a cloud session, runs say what kind they are — on another branch
- 2026-10-28 `9fc40d51a` style(compute): the console takes froglet.games' look - quieter, one accent at a time — on another branch
- 2026-10-28 `c6318e885` feat(compute): the mothership - every computer live, runs sent to the one you pick, stop from the console — on another branch
- 2026-10-28 `0900bc354` fix(ai-training): a new pilot seeks its scenario's target; Compute can show the game — on another branch

### Also in flight on other branches
- `claude/eager-franklin-e6g77h` (unattributed, 2026-11-01): docs(ai): vessel-mode chart regenerated on the merged tree (Serpent Gulag's card, no trainer scenario yet)
- `compute-queue` (froglet-compute (vm), froglet-compute (shombithofficial), froglet-compute (noreply), 2026-10-31): compute: request r20261031-224826-16e9 (unity-train)

### Open tickets (Froglet Tasks)
- T-20261025-dc8b [taken by Yash] test: Playtest every Regatta AI in one sitting, on the newest AI (Ys-bleeding-edge until its next merge into bleeding-edge, then bleeding-edge): (1) run the edit-mode tests RegattaNoCrystalTargetTests, RhinoRampDriveTests, DolphinChargeDriveTests and RegattaDriveTuningTests; (2) race Regatta with every AI hull on AI seats - Squirrel, Manta, Rhino, Dolphin, Urchin, Stoat - at Hard, I1 then I3 (I4 for the Squirrel and Manta), Third Eye on with trails, and watch each against its expected drive: Squirrel skims the rail on the ring side; Manta holds Soar (~700 u/s) and pivots only in hard turns; Rhino ramps on straights and brakes before rings (corner speed 333, line 0.676); Dolphin drifts again at each discharge's peak (line 0.785); no AI turns toward a crystal; every AI threads rings through their centre without circling. Where an older merged ticket's numbers differ, the newest tuning is right. (who: anyone with the Unity Editor)
<!-- team_report:end -->
