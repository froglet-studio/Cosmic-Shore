# Skim Race — Garrett playtest

How to start one normal Skim Race against the current AI and what to write down.
This is a match, not a training run. Do not press Learn.

Branch: `feat/ai-genetic-training`.
Editor: Unity `6000.3.17f1`.
Mode: Skim Race (`GameModes.HexRace`, scene `MinigameHexRace`).
Hull: Squirrel. The arcade card lists only Squirrel.

## What you will actually fly against

There is no trained opponent to install.

- `Assets/_SO_Assets/AI Training/Archive.asset` has `Entries: []`. The key a normal
  intensity-4 match looks up is `Squirrel_HexRace_I4`. That entry does not exist.
- `SessionState.asset` hall of fame is empty: `HallOfFameBest` has no genes, and
  `HallOfFameBestFitness` is −Infinity. No genome was exported. The file
  `Assets/_SO_Assets/AI Training/Exports/SkimRace_Squirrel_I4.json` was not created.
- The session population holds 24 unevaluated genomes (generation 0, fitness 0,
  zero evaluations). Those stay in `SessionState.asset`. They are not a deployed
  opponent and they were not copied out as one.

Until an archive entry exists, every AI seat stays on the stock `AIPilot`.
The host seat stays on player input.

When an entry does exist later, a normal HexRace (Learn off) installs `TrainingPilot`
on AI seats only, stops `AIPilot` first, and flies the stored genome at intensity 4
with no dither. That path is `TrainingControlSO.DeployArchiveInNormalPlay` →
`TrainingDeploymentService` → `ArchiveDeployment`. It is already switched on
(`DeployArchiveInNormalPlay: 1`). It does nothing while the archive key is missing.

## Steps

1. Pull branch `feat/ai-genetic-training`.
2. Open the project in Unity `6000.3.17f1`.
3. Open **FrogletTools → AI Training**.
4. Open the **Deploy** tab. Leave **Use archive in normal play** on.
   Leave **Store a genome per intensity** off.
   Do not press **Push best genome into archive**. That button stays disabled:
   Learn has not finished a match, so there is no hall-of-fame genome to push.
   Do not press **Play against trained AI**. That button also starts play mode.
   Launch from the arcade instead, so this is a normal match.
5. Do not press **Learn**. Learn is the trainer. It fills the match with AI and
   sets `IsTraining`, which turns deployment off for that run.
6. From the main menu, start **Skim Race** the normal arcade way:
   - Vessel: Squirrel (the card allows only that hull).
   - Intensity: **4**.
   - Player count: **4** if you are alone in the party, or any total above the
     number of humans, so AI backfill seats exist. A count of 1 spawns no AI.
   - You fly the host seat.
7. Play the race to the normal finish. Do not reload into Learn.

## What to record

- Your domain’s finish (place and whether that domain reached the crystal target).
- Each AI seat’s crystals collected and finish.
- Whether each AI ship steers and changes speed through the race, or snaps
  (teleports, instant course jumps, score changes with no flight).
- Which pilot was on the AI seats. With the archive empty, that is `AIPilot`.
  A console line `[Deploy] … flies the archive` means a `TrainingPilot` was
  installed. You should not see that line on this build.

## After an archive entry exists

Repeat the same steps. Intensity 4 then flies `Squirrel_HexRace_I4` with no dither.
Intensities 1–3 dither that same genome and do not write it back. The human seat
stays on player input either way. A missing entry still leaves the AI seats on
`AIPilot`.
