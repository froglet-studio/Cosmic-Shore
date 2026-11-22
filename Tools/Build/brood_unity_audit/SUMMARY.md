# Brood Rush in real Unity: AI-only, Medium, 3 teams of 3 (2026-11-22)

42 AI-only matches of Brood Rush played in the real Unity game (6000.3.17f1, batchmode, one fresh Play session from
Bootstrap per match), 378 seats, 22-23 seats per hull. The tree is PR #2322 (claude/project-thread-nqfbgz) with
#2329 (Rhino), #2331 (Manta), #2332 (Mass-Space AI rows) merged in and #2330's Swordfish afterimage config
(1.7x at rest), on branch `claude/brood-unity-audit`.

Setup: Medium AI difficulty on every seat, intensity 2, three teams (Jade, Ruby, Gold), nine seats drawn from the 17
card hulls (`rosters.py --matches 42 --seed 7`), the host flying team A's first hull and handed to the AI at turn
start (all 42 host seats report Medium), 300 s cap (no match reached it: every match ended on broods, 90-181 s).

**Index** = a seat's VC + VD over the mean VC + VD of all nine seats in its match, averaged per hull (the Amoebius
judge's definition, `Tools/Compute/evaluators/brood_judge.py`). VC, VD and Lost are the match's own VolumeLedger
books. Shares are per-seat value over the match mean of VC + VD.

## Per hull

| Hull | n | Index | sd | VC share | VD share | Lost share | VC mean | VD mean | Team broods | Win | In nucleus | Speed | Trail vol | Ability starts | Host seats |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Dolphin | 23 | 1.71 | 0.37 | 1.70 | 0.01 | 0.28 | 51073 | 264 | 2.52 | 74% | 62% | 111 | 1.87 | 59 | 2 |
| Sparrow | 22 | 1.36 | 0.52 | 1.17 | 0.19 | 0.24 | 35548 | 5904 | 0.82 | 9% | 60% | 58 | 0.47 | 65 | 2 |
| TigerMoth | 22 | 1.31 | 0.55 | 1.12 | 0.19 | 0.27 | 37020 | 7691 | 1.55 | 41% | 68% | 58 | 0.40 | 0 | 3 |
| Thresher | 22 | 1.02 | 0.30 | 0.93 | 0.09 | 0.16 | 27793 | 2777 | 1.32 | 36% | 46% | 227 | 1.15 | 26 | 6 |
| Manta | 22 | 1.01 | 0.16 | 0.96 | 0.05 | 0.19 | 28785 | 1585 | 1.36 | 36% | 50% | 171 | 0.13 | 0 | 2 |
| Wrasse | 23 | 0.99 | 0.41 | 0.52 | 0.47 | 0.12 | 15322 | 15169 | 2.00 | 57% | 67% | 57 | 0.49 | 0 | 1 |
| Grizzly | 22 | 0.98 | 0.18 | 0.88 | 0.10 | 0.07 | 29529 | 3529 | 0.95 | 27% | 52% | 50 | 0.40 | 108 | 2 |
| Meerkat | 22 | 0.95 | 0.23 | 0.83 | 0.12 | 0.13 | 23717 | 3324 | 1.05 | 32% | 68% | 66 | 1.92 | 43 | 3 |
| Scarab | 22 | 0.92 | 0.17 | 0.89 | 0.03 | 0.19 | 24344 | 973 | 1.59 | 41% | 56% | 205 | 1.00 | 0 | 3 |
| Squirrel | 22 | 0.92 | 0.19 | 0.86 | 0.06 | 0.21 | 23296 | 1752 | 1.09 | 32% | 61% | 115 | 2.16 | 2 | 2 |
| Honeyguide | 23 | 0.92 | 0.17 | 0.90 | 0.02 | 0.29 | 28234 | 543 | 1.61 | 43% | 61% | 38 | 1.54 | 5 | 1 |
| Shrike | 22 | 0.88 | 0.35 | 0.84 | 0.04 | 0.06 | 23219 | 1213 | 0.77 | 18% | 71% | 18 | 0.07 | 82 | 5 |
| Swordfish | 22 | 0.85 | 0.22 | 0.77 | 0.08 | 0.52 | 22443 | 2276 | 1.14 | 18% | 62% | 47 | 0.73 | 4 | 3 |
| Rhino | 22 | 0.81 | 0.19 | 0.77 | 0.04 | 0.36 | 24233 | 1307 | 0.77 | 18% | 69% | 57 | 1.00 | 4 | 2 |
| Urchin | 22 | 0.79 | 0.59 | 0.78 | 0.02 | 0.36 | 22548 | 451 | 1.14 | 18% | 89% | 138 | 2.50 | 0 | 4 |
| Stoat | 23 | 0.79 | 0.21 | 0.63 | 0.16 | 0.21 | 19959 | 5224 | 1.43 | 35% | 74% | 106 | 1.94 | 94 | 0 |
| Orca | 22 | 0.77 | 0.23 | 0.60 | 0.17 | 0.26 | 18158 | 5410 | 1.18 | 27% | 77% | 35 | 0.80 | 0 | 1 |

Within 0.90-1.10 of the field: 8 of 17 (Grizzly, Honeyguide, Manta, Meerkat, Scarab, Squirrel, Thresher, Wrasse).

## What stands out

- **Dolphin is far ahead (1.71)**, almost all of it VC: 51k a seat, about twice the field, with almost no VD. Its team
  wins 74% of the time against a 33% base rate.
- **Sparrow (1.36) and TigerMoth (1.31) are the other two above the bar.** Both have high spread (sd 0.5), so they
  have big matches and quiet ones. Sparrow's team wins only 9%, so its volume does not turn into broods.
- **Below the bar: Orca 0.77, Stoat 0.79, Urchin 0.79, Rhino 0.81, Swordfish 0.85, Shrike 0.88.** Even with #2329 and
  #2330, Rhino and Swordfish are still low. Swordfish loses the most of its own volume of any hull (Lost share 0.52).
- **Wrasse is the only hull whose output is half VD** (VC 15k, VD 15k). Its index is at the field (0.99), and its
  team wins 57%.
- **Team A (Jade, the host's team) wins 19 of 42 matches**, against an even share of 14. Rosters are random, so this is
  a domain, spawn or host-seat effect. The host seat, flown by the AI at Medium, indexes 1.06; the 336 backfill seats
  index 0.99.
- **Six hulls recorded zero ability starts** (TigerMoth, Manta, Wrasse, Scarab, Urchin, Orca). The counter sees
  autopilot direct starts and input presses through `VesselActionTape`. Either those AIs never press, or they start
  abilities some other way. Check this before trusting any of the "ability starts" column.
- Matches are short: mean 119 s, and 19 of 42 ended at 90 s.

## Files

- `results.csv`: one row per seat (run, match, teams, hull, domain, host, VC, VD, Lost, held, team broods, trail
  volume, element levels, speed, time in the nucleus, ability starts and names, pilots on the hull).
- `matches.jsonl`: the full match records, including each AI pilot's fields.
- `summarize.py` builds the table; `rosters.py` builds the rosters; `jsonl_to_csv.py` rebuilds a CSV from the jsonl.
- Harness: `Assets/_Scripts/Editor/Launch/BroodAudit/` (Editor-only; it writes only its own result files).

## Running it again

```
Unity -batchmode -projectPath <checkout> -buildTarget Win64 -logFile Logs/brood.log   -executeMethod CosmicShore.Editor.BroodAuditLauncher.Run -broodOut Logs/BroodAudit/run   -broodRosters "$(py -3 Tools/Build/brood_unity_audit/rosters.py --matches 14 | head -1)"   -broodSeeds 1 -broodDifficulty Medium -broodLimit 300 -broodQuit
```

**Run one editor at a time on a machine.** The offline host always binds UDP 7777, so a second editor boots to a
menu with no pilot and every one of its matches faults with "menu never gave a local pilot". The opt-in
`-broodRehost` restarts that host on another port, but its menu still gets no vessel, so it is not a working fix yet.
A match takes about 2.5-4 min of wall time.
