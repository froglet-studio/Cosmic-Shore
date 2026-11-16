# The Regatta race judge (the real game, headless)

**2026-11-16, Garrett's Regatta flywheel, round 1, worker W1.** Plan step: `time-arena-course`.

A course is scored by the game's own code: Amoebius boots the real game headless, launches Regatta through the normal
arcade flow (the editor's `RegattaHullBenchmark`, step for step), seats each hull's SHIPPED AI at Legendary with the
host seat idle in a hull nobody races, presses Ready, and timestamps every gate each AI threads. Nothing is modelled.

```sh
apt-get install -y dotnet-sdk-10.0                                     # once (dot.net is blocked in cloud sessions)
python3 Tools/Compute/evaluators/regatta_judge.py --self-test           # ~1 min: Urchin ~51 s, a 10 s limit is a DNF
python3 Tools/Compute/evaluators/regatta_judge.py --calibrate --seeds 1,2,3
python3 Tools/Compute/evaluators/regatta_judge.py --genome g.json --hulls Urchin,Manta --seeds 1,2 [--field]
```

From Python: `regatta_judge.judge(genome, seats, seeds, limit_s=400, backend="engine"|"model", field=False)`, the RESULT
shape of the flywheel's shared contract (`/mnt/project-files/regatta-flywheel/BRIEFS.md`). The engine side is
`Port/src/CosmicShore.Player/RaceJudge.cs` (`CosmicShore.dll --headless --race-judge SPEC --race-out RESULT`, optional
`--control-port N` to inspect a judged race live, `"trace": true` for a 4 Hz position and speed trace per seat).

## Speed

One process boots in ~20 s wall, then races its seeds back to back through Play Again: 4-20 s wall per race (5-12x real
time on one core). Alone mode runs one process per hull in parallel: **27 races (9 hulls x 3 seeds) in 3.5 min on 4
cores**, about 2 races per core-minute. A 6-hull field race: ~10 s wall per race after boot.

## Calibration against the editor benchmark (I1, the shipped course, no features, each AI alone, 3 seeds)

| Hull | Port (mean, sd) | Editor 2026-11-03 | Ratio | Why |
|---|---|---|---|---|
| Urchin | 50.9 s (0.4) | 51.0 | 1.00 | matches |
| Squirrel | 80.5 s (3.3) | 75.0 | 1.07 | matches within the noise band |
| Serpent | 108.7 s (6.4) | 100.0 | 1.09 | matches within the noise band |
| Manta | 82.5 s (0.8) | 67.0 | 1.23 | **game change**: Soar is the Manta's Time level-5 upgrade since the 2026-11 overhaul (`MantaAnalogTurnBoostExecutor.soarUnlockedBy = Time`); at Regatta's starting Time the upgrade is off, so the AI never boosts and holds 180 u/s (inspected live: `SoarUnlocked = False`) |
| Sparrow | 91.1 s (2.3) | 133.0 | 0.68 | not traced yet |
| Rhino | 35.2 s (3.2) | 95.0 | 0.37 | **game change**: since 2026-11-07 the Rhino's hull is a plough that shoves rail prisms aside (`RhinoPloughPrismEffectSO`); the 11-03 Rhino rammed and stalled (78% of an I4 race at 50 u/s). Now it cruises at ~440 u/s and shoves 200-500 prisms a race |
| Stoat | 238.7 s (1.0) | 70.0 | 3.4 | **game change**: since 2026-11-07 the Stoat's sling is a trampoline whose autopilot presses only when its steering target is 35+ degrees off the nose (`AutopilotTrampoline`); on Regatta's race line the target sits one look-ahead dead ahead, so it never presses and flies at its 60 u/s base (inspected live: `_aiLastSlingTime = -Infinity`) |
| Dolphin | DNF (3-21 of 24 gates in 400 s) | 97.5 | - | circles near rings at 50-100 u/s; Yash's editor flight records showed an AI Dolphin stuck at 0 u/s too (`AI_BOOST.md`). Not traced to a cause yet |
| Scarab | 43.9 s (2 of 3; one timed out at 14 gates) | - | - | no editor reference |

**Reading it.** The three hulls whose kits did not change since 2026-11-03 match the editor within 10%. Every large gap
traced so far is the game changing since that benchmark, not the port differing from Unity: the 11-03 numbers are
stale for the Manta, Rhino and Stoat. The calibration that would settle the rest is a FRESH editor run of
`RegattaHullBenchmark` (branch `claude/regatta-overnight-balance`, `-executeMethod ...RegattaHullBenchmark.RunBatch`) on
today's bleeding-edge.

**What the judge already says about Regatta today** (worth Garrett's eyes before any course search): alone on the I1
course the Rhino (35 s) and Scarab (44 s) beat the Urchin (51 s); the Stoat (239 s) cannot race because its AI never
uses its sling; the Dolphin AI does not finish. The course search balances whatever the AIs do, so a broken AI (Stoat,
Dolphin) is a link-3 problem to fix before the search can mean anything for those hulls.

## Field races

`field: true` races every seat at once (two domains, the host idle). The race ends when the first pilot finishes (the
mode's own end), so the others report gates, not times. Two 6-hull test fields (Urchin, Manta, Rhino, Squirrel, Serpent,
Sparrow): the Rhino won both in ~34 s; the Urchin had 15-16 of 24 gates.

## Limits

- The genome: until the game has `RegattaCourseGenome` (W2), the judge reads the genome's `intensity`, `seed` and
  `features` and says in `notes` what it ignored; once that type exists it hands it the whole genome.
- `prism_hits` is prisms destroyed (friendly + hostile) from `RoundStats`, not contacts.
- Headless has no GPU: prism clock animation is off (visual only).
- The boot fix (`UnattendedBoot`) closes the first-run privacy overlay unanswered and skips the username step, exactly
  as the game's soak runners do, and only in `--train` and `--race-judge` runs.
