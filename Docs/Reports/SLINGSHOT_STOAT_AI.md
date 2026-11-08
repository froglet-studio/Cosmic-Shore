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

## 8. With the nose lead on (2026-11-07)

Brief 19 of the lead-on round (`BRIEFS.md` §14, §19). Every number is a MODEL (`slingshot_stoat_sim.py`, which flies
the fleet nose lag); the lead is `ai_nose_lead.NoseLead` at gain 2.5, the model of `AIPilot.NoseLeadGain` (PR #1786),
which the game ships at 0. Kit: the same as §4 (Yash's `c459f442`, 30 u shell, core 0.2, laid over this branch's tree
for the run only; this branch itself still carries the 60 u shell). Check: the parent's B and C rows score 1.0873 and
1.0885 on the search courses here, exactly as in §4.

**The twin search**, `slingshot-stoat-time-lead` (the parent's job, seeds and genomes, `nose_lead: 2.5`), run by hand,
900 evaluations a restart (`min_evals` + `window_evals`):

| Restart | Loss | Turn | Interval | Turn min distance | Aim scale | Hold | Warp distance |
|---|---:|---:|---:|---:|---:|---:|---:|
| B (grown from the genomes) | 1.0722 | 41 deg | 2.45 s | 300 u | 121 | 0.02 | 340 u |
| C (grown from the genomes) | 1.0736 | 89 deg | 2.92 s | 195 u | 60 | 0.00 | 298 u |

B and C land 0.13% apart; B's last gain was at evaluation 171, C's last-20% gain was 0.02%. Converged. Same basin
as the parent: a warp press at 300-340 u, a press every 2.5-2.9 s, no hold; the turn rows are flat again.

**The 2x2 on fresh courses** (seedbase 1001, 12 courses an intensity, median seconds, rings missed a race; one script,
`results/stoat-lead/two_by_two.py`, so all cells are on one tree):

| Rows | Lead | I1 | I2 | I3 | I4 | Misses a race I1-I4 | Finished | Composite |
|---|---|---:|---:|---:|---:|---|---:|---:|
| Shipped (Slingshot copy) | off | 144.2 | 140.3 | 153.8 | 164.3 | 1.58 / 0.58 / 3.33 / 4.92 | 48/48 | 2.614 |
| Shipped (Slingshot copy) | on | 143.2 | 142.3 | 145.5 | 159.6 | 0.50 / 0.92 / 2.25 / 5.08 | 48/48 | 2.749 |
| Parent B | off | 63.5 | 64.5 | 66.4 | 70.2 | 0.08 / 0.33 / 0.08 / 0.50 | 48/48 | 1.141 |
| **Parent B** | **on** | **60.5** | **60.8** | **65.3** | **67.5** | 0.00 / 0.42 / 0.00 / 0.17 | 48/48 | **1.108** |
| Parent C | off | 63.1 | 64.0 | 66.1 | 69.6 | 0.00 / 0.42 / 0.08 / 0.67 | 48/48 | 1.167 |
| Parent C | on | 60.0 | 61.5 | 65.2 | 69.2 | 0.00 / 0.50 / 0.00 / 0.17 | 48/48 | 1.120 |
| Lead-on B | off | 63.5 | 65.5 | 68.1 | 74.3 | 0.00 / 0.33 / 0.17 / 0.75 | 48/48 | 1.153 |
| Lead-on B | on | 59.6 | 62.3 | 64.6 | 74.0 | 0.00 / 0.25 / 0.25 / 0.75 | 48/48 | 1.116 |
| Lead-on C | off | 62.6 | 63.2 | 66.5 | 72.2 | 0.17 / 0.92 / 0.08 / 0.67 | 48/48 | 1.166 |
| Lead-on C | on | 61.2 | 63.5 | 65.8 | 70.9 | 0.08 / 0.75 / 0.00 / 0.25 | 48/48 | 1.130 |

(The parent rows' fresh composites here sit 1-6% under §5's, which used other base rows; compare within this table.)

What it says:
- **The lead alone does it.** The parent's rows with the lead on are 3-4% better (1.141 → 1.108, 1.167 → 1.120),
  about 3 s faster at I1-I2, and miss a third as many rings at I4.
- **The drive does not need to move.** The lead-on search beats the parent's rows on its own six search courses
  (1.072 against 1.104-1.122 for the parent's rows with the lead on) but not on fresh ones (1.116 and 1.130 against 1.108
  and 1.120): the gain was fitted to the search courses. The Stoat's misses were already few (the warp press flies long
  legs straight), so the nose lag cost it less than the Scarab or the Manta.
- The shipped rows get no help: the lead trims their misses at I1-I3 but they still cruise at 140-160 s.

Well trained (AI_PLAN §5), lead on: (1) restarts agree, yes (0.13%); (2) beats every genome, yes (1.11-1.13 against
2.61-2.75); (3) human marks (Yash, orbit kit, I2 75.2 s / I3 89.6 s): the model AI is under both; no human has finished
a trampoline race; (4) real game: not run.

**Proposal for Yash** (nothing written to an asset): the day `NoseLeadGain` is turned on for the Stoat, keep the
parent's rows (§7; B was best here with the lead on, C within 1%); the lead-on twin's rows are not better. Editor check:
AI Stoats race Slingshot I1-I4 at gain 0 and at 2.5 with those rows; at 2.5 they miss no more rings and are no slower.
