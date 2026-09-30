# Skim Race — Garrett playtest

How to start one normal Skim Race against the trained archive and what to
write down. This is a match, not a training run. Do not press Learn.

Branch: `feat/ai-genetic-training`.
Editor: Unity `6000.3.17f1`.
Mode: Skim Race (`GameModes.SkimRace`, scene `MinigameSkimRace`).
Hull: Squirrel. The arcade card lists only Squirrel.
Intensity: **4**.

## What you will fly against

The archive has one entry, filed by the Prompt 6 Learn pass (2026-09-29):

- Key: `Squirrel_SkimRace_I4` (Squirrel, SkimRace, intensity 4).
- Archive entry fitness: 974.2953. Generation 2. Notes: `Deployed after 72 episodes`.
- Sidecar: `Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json` (matches the entry).

This genome is **not** a training finisher. The Prompt 9 Learn pass ran 72
more evaluations (generations 3 to 5). None reached 54 crystals or ended
before the 240 s cap, and the best rollout collected 28 crystals. That
pass's generation-5 auto-deploy was lost when the Editor restarted on a full
disk, so the archive is still the generation-2 entry above. Intensity 4's
crystal target is 54. The training fitness is not a race score. See
`SKIM_RACE_AI_STATUS.md` §0c.

## Benchmark

Garrett's bar: a Skim Race score of **110 or better** (lower is better; the
winner's score is the finish time), or a win against Garrett.

**Status: unmet.** Nobody has flown against this archive yet. In the
automated races below the host seat had no input, and the best AI score was
207.00.

## Automated races (host seat not flown)

Setup matched the steps below: arcade Skim Race, Squirrel, intensity 4,
player count 4, **Use archive in normal play** on, **Store a genome per
intensity** off. Learn was not pressed, and neither was **Play against
trained AI**. All three AI seats logged `[Deploy] … flies the archive`. An
editor script pressed Ready once. The host seat then sat with no input, so
these are not Garrett results.

| Race | Winner (domain) | Score | Crystals (AI seats) | Host crystals | AI stalled at 0? |
|---|---|---|---|---|---|
| 1 | Ruby | 223.73 | Ruby 36 + 18, Gold 2 | 1 | No |
| 2 | Ruby | 207.00 | Ruby 41 + 13, Gold 4 | 0 | No |
| 3 | Ruby | 208.26 | Ruby 37 + 17, Gold 3 | 0 | No |

The benchmark is unmet: no score reached 110, and no race was finished
within 70 s. Before the steering fix, the generation-1 archive did not
finish in 240 s. One race ended with every AI at 0 crystals, and the other
with every AI at 1.

A normal HexRace does not set `IsTraining`. With **Use archive in normal
play** on, AI seats install `TrainingPilot` from this entry and stop
`AIPilot` first. Intensity 4 flies the stored genome with no dither. The
host seat stays on player input.

## Steps

1. Pull branch `feat/ai-genetic-training`.
2. Open the project in Unity `6000.3.17f1`.
3. Open **FrogletTools → AI Training**.
4. Open the **Deploy** tab.
   - Leave **Use archive in normal play** on.
   - Leave **Store a genome per intensity** off.
   - Do not press **Learn**.
   - Do not press **Play against trained AI**. That button starts a
     training-shaped play session. Launch from the arcade instead.
5. From the main menu, start **Skim Race** the normal arcade way:
   - Vessel: Squirrel.
   - Intensity: **4**.
   - Player count: **4** if you are alone in the party, or any total above
     the number of humans, so AI backfill seats exist. A count of 1 spawns
     no AI.
   - You fly the host seat.
6. Play several races through to the normal finish (a domain reaches the
   crystal target, or you stop the match yourself and say so). Do not
   reload into Learn between them.

## What to record

For each race:

- Your finish: domain, place, crystals, and whether your domain reached
  the crystal target.
- Each AI seat's finish: domain, crystals, and whether that domain reached
  the crystal target.
- Whether each AI ship steers and changes speed through the race, or snaps
  (teleports, instant course jumps, score changes with no flight).
- The console line `[Deploy] … flies the archive`. That line means
  `TrainingPilot` was installed from `Squirrel_SkimRace_I4`. If it is
  absent, the seat is still on `AIPilot` and the match is not against this
  archive.

Write the numbers down as they happened. A training fitness such as 974.30
is crystals × 100 minus the time and golf penalties inside a capped episode.
It is not a race result.
