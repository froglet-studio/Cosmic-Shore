# The Stoat's Slingshot AI: trained on the trampoline (2026-11-06)

`Plan step: time-arcade`. Brief: `/mnt/project-files/regatta-training/BRIEFS.md` section 2 (Garrett's regatta balancing).
Owner of the Stoat, its kit and its AI: **Yash**. Every number below is a MODEL (`Tools/Build/slingshot_stoat_sim.py`)
unless it says "real". Nothing here is written into an asset: the trained rows are a proposal for Yash.

## 1. What the real game shows

35 recorded Slingshot races (`bugledger/yskha` 34, `gradi` 1, `owner` 1) carry 64 AI Stoat rivals with their ring
counts. The kit changed four times in those days (orbit, easier orbit, aimed exit, trampoline), so only the races
flown after the trampoline commit (8abe1fe8, 2026-11-04 02:13 +05:30) measure today's AI:

| Race (real) | Kit | AI Stoats, real | Model on the same course and seats |
|---|---|---|---|
| Yash, I3, 2026-11-07 02:08 | 60 u shell, presses for 35 deg+, aim x100 | **neither finished in 283 s** (11 and 13 rings of 16); one flew 2,400 u out from the centre at up to 400 u/s, then crawled back at 60 u/s | 233 s and 262 s, 12-21 rings missed |

The model reproduces the failure (2x cruise pace, many missed rings, flung far off the course) but is kinder than the
editor. That is the one race on the trampoline kit; nobody has finished a trampoline race yet, Yash included (he left
that race at 1 ring). Yash halved the shell to 30 u with a hard core the same night (`c459f442` on `Ys-bleeding-edge`,
"it moves the vessel too much"), and that kit is what this search trained on.

## 2. Why a new model file

`stoat_orbit_sim.py` (Yash's) explored the trampoline before the C# existed, and three things differ from what shipped:
the bounce's kick moves about a third of the C#'s distance (the C# kick is `(cos/2 + 1) x kick / 1.5` over 1.5 s on the
flight clock); an untouched shell is passed only once the hull is outside it; and the C# always lays the shell on the
hull's turning line (`--trampoline` chose the shipped rows on the straight line). `slingshot_stoat_sim.py` ports the
shipped code line for line, reads every number from the assets (so it follows Yash's 30 u shell and hard core when
they merge), and leaves his file untouched.

## 3. What the AI's numbers can and cannot do

On the 30 u shell, **no press-to-turn rule beats never pressing**: a 45-cell grid over the turn angle, aim scale and
distance ran 139-255 s a race, against 139-150 s for an AI that never presses. The trampoline AI only presses to turn,
and a bounce costs it its line as often as it saves a corner.

The orbit's AI also pressed for the warp alone on a long leg (`aiWarpMinDistance`); the trampoline AI never read that
field. A laid shell warps the flight (x1.86 while it is up, x3.1 in the bounce) and the bounce keeps the warp as
momentum for 6 s, so a Stoat that lays a shell on every long straight and grazes it shallow flies about twice as fast.
This branch lets `AutopilotTrampoline` read `aiWarpMinDistance` (`StoatSlingMath.TrampolinePress`). **Both shipped
configs carry 0, so nothing changes in the game until Yash writes a number.**

## 4. The search

Job `slingshot-stoat-time` (`status: proposed`, run by hand: Compute is paused), evaluator `slingshot_fit` v1: six rows
(`aiSlingMinTurnDegrees`, `aiSlingIntervalSeconds`, `aiSlingMinDistance`, `trampolineAiTurnScale`, `autopilotHold01`,
`aiWarpMinDistance`), I1-I4 x 6 courses, the AI plan's composite (mean + 0.5 x worst of per-race time / cruise time),
first generation grown from every Stoat genome (`StoatSlingConfig_Slingshot`, the hull's, `_Warpline`). Kit: Yash's
`Ys-bleeding-edge` (30 u shell, core 0.2) with this branch's warp press. sep-CMA-ES, 1,512 evaluations a restart.

| Restart | Loss | Turn | Interval | Turn min distance | Aim scale | Hold | Warp distance |
|---|---:|---:|---:|---:|---:|---:|---:|
| B (grown from the genomes) | 1.0874 | 148 deg | 2.90 s | 13 u | 96 | 0.01 | 310 u |
| C (grown from the genomes) | 1.0885 | 26 deg | 2.87 s | 295 u | 68 | 0.02 | 338 u |
| D (from the shipped init alone, no genomes) | 1.0941 | 68 deg | 2.86 s | 549 u | 65 | 0.04 | 335 u |

B and C land 0.1% apart, B never improved in its last 20% of evaluations, C once by 0.06%. They agree on what matters
(warp press at 310-340 u, a press at most every ~2.9 s, the smallest kick); they differ on the turn press, which
hardly fires in either (B never turns; C turns only with its ring 295 u away), so that row is flat near the optimum. D, started from the shipped rows with no genomes, found the same basin 0.6% higher, but it was still improving in its last 20 evaluations, so it had not settled.
The smallest kick wins because a big one throws the hull off its line.

## 5. Fresh courses (seedbase 1001, 12 courses an intensity, model)

| Rows | I1 | I2 | I3 | I4 | Finished | Rings missed a race | Composite |
|---|---:|---:|---:|---:|---:|---:|---:|
| Shipped, 30 u kit (Ys-bleeding-edge) | 141.8 | 141.2 | 152.8 | 150.6 | 48/48 | 0.8-4.4 | 2.60 |
| Never presses (cruise) | 141.0 | 142.4 | 147.6 | 149.5 | 48/48 | 0-0.1 | 2.41 |
| Slingshot copy on bleeding-edge (35 deg, x100) on the 30 u kit | 181 | 192 | 263 | 233 | 32/48 | 12-27 | 5.18 |
| **Trained B** | **64.5** | **64.0** | **66.6** | **70.7** | **48/48** | 0.1-1.3 | **1.21** |
| **Trained C** | **63.6** | **61.6** | **66.0** | **70.8** | **48/48** | 0.1-1.0 | **1.18** |

Median seconds. Human marks (Yash's only finished races, both on the ORBIT kit, before the trampoline): I2 75.2 s, I3
89.6 s. The trained model AI is under both; no human has finished a trampoline race to compare against.

**In Regatta** (gate steering only at Time 10, three laps of the shipped course, no rails, one race an intensity): the
trained rows run 49-68 s against 180-258 s for the shipped ones. That is only what the rows would do if Regatta's own
copy took them (`StoatSlingConfig_Regatta`, on the unmerged overnight branch); the field's marks are Dolphin 54-72 s and
Squirrel 48.5-59.5 s.

## 6. Well trained? (AI_PLAN section 5's four tests)

1. Restarts agree: **yes** within 0.1% on the search courses (3% on fresh courses); C's last-20% gain was 0.06%.
2. Beats every existing genome on the composite: **yes**, 1.18-1.21 against 2.41 (cruise) and 2.60 (shipped).
3. Within 15% of the best human: **cannot be judged**, there is no finished human race on this kit.
4. Real game: **not run** (no editor in the cloud). The hand test: AI Stoats race Slingshot I1-I4 with C's rows; they
   finish, miss few rings and never fly far off the course; the model within 10% of the editor.

## 7. To promote (Yash's call)

Write C's rows into `StoatSlingConfig_Slingshot` with the learner's writer, move `StoatSlingTests`' pinned
`aiWarpMinDistance: 0` with them, and race them in the editor. The model's known gaps are `slingshot_stoat_sim.LIMITS`
(rivals, the Orrery's prisms, roll, the cell's outer shell).
