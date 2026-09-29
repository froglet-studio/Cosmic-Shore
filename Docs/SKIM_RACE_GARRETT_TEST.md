# Skim Race — Garrett playtest

How to start one normal Skim Race against the trained archive and what to
write down. This is a match, not a training run. Do not press Learn.

Branch: `feat/ai-genetic-training`.
Editor: Unity `6000.3.17f1`.
Mode: Skim Race (`GameModes.HexRace`, scene `MinigameHexRace`).
Hull: Squirrel. The arcade card lists only Squirrel.
Intensity: **4**.

## What you will fly against

The archive has one entry, filed by the 2026-09-29 Learn run:

- Key: `Squirrel_HexRace_I4` (Squirrel, HexRace, intensity 4).
- Archive entry fitness: 277.09552. Generation 0. Notes: `Auto-deploy after 24 episodes`.
- Sidecar: `Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json`.

That genome was the best of 24 evaluations. It collected 4 crystals in
120.1 seconds of game time. Every evaluation in that generation ended on
the trainer's 120-second cap. Intensity 4's crystal target is 54, so that
run did not finish a race. Your matches are the first time this archive is
asked to race under normal rules, with a human in the host seat and no
episode cap from the trainer.

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
  `TrainingPilot` was installed from `Squirrel_HexRace_I4`. If it is
  absent, the seat is still on `AIPilot` and the match is not against this
  archive.

Write the numbers down as they happened. A training fitness of 277.10 is
four crystals minus the time penalty inside a capped episode. It is not a
race result.
