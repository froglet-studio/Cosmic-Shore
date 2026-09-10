# Bloomrush — the Manta party game (GameModes.Bloomrush = 52)

**Manta-only, 2–4 players, 2–3 domains, 4 intensities.** Tag everything you fly past, then reach
a crystal before the fuses burn down and set it all off at once. The mode is the Manta's
accessibility thesis played straight: nobody has to learn a button — the whole scoring loop is
skim (arm bombs) → graze (plant them, silently, one per target) → crystal (Kabloom the board).
Vessel mechanics: `_Scripts/Controller/Vessel/R_VesselActions/MANTA_STING_KABLOOM.md`.

## Rules

- **120-second timed round** — the platform's first timed-highest-score mode in the domain-games
  family. The scene's `NetworkTimeBasedTurnMonitor` (duration 120) is the ONLY end condition;
  `BloomrushScoringRuleSO.IsObjectiveReached` is a permanent no.
- **Score = hostile prism VOLUME destroyed** (`ScoringMetric.VolumeDestroyed = 11`, reading
  `IRoundStats.HostileVolumeDestroyed`, domain-summed) — the Manta's kit is about volume, and
  volume is what a bigger bloom buys.
- **"Beat the fuse" is a blast-size fact, not a scoring case**: a crystal-cashed bloom detonates
  at `kabloomBlastScale`, a fuse expiry at the smaller `fuseBlastScale`, so bombs that time out
  score only a fraction of a cashed bloom by construction.
- **Tiebreaker = fuses beaten** (`IRoundStats.FusesBeaten`, domain-summed) — credited per bomb
  cashed by a Kabloom, over the owner-detects → server-records RPC. Enum order (Jade → Ruby →
  Gold) stays the deterministic last resort.
- **The FUSE is the mode's own intensity dial**: 30 / 25 / 20 / 20 seconds by intensity
  (`fuseSecondsByIntensity` on the controller), pushed through the static
  `MantaBombRules.FuseSecondsOverride` on EVERY peer at countdown end — after the config-sync
  gate, so a client can never plant on intensity-1 fuses in an intensity-4 match — and cleared
  in `OnNetworkDespawn` so freestyle bombs go back to the authored 25 s.
- Team scoring folds through the standard `ScoringRuleSO` surfaces (`ResolveWinner`,
  `ResolvePlacementOrder`, team-major `BuildResults`), so the mode is **Maelstrom-admissible**
  on the scoring axis (not added to the Tournament pool here — that is a design call).

## The arena

**Rampage's cactus forest, referenced verbatim** — the scene is a donor clone of
`MinigameBends.unity`, which already reuses Rampage's four per-intensity cell configs (the
Bends precedent: the same vessel economy wants the same place; here the same DENSE, breakable
reef the Manta's blooms want). The reef is identical at all four intensities; what moves is the
fuse (above), the wildlife (the profile's climb, inherited), and the crystals:

- **Omni crystal ladder re-cut toward abundance** (crystals are the detonator, so scarcity is
  how contested cashing out is): `3×players+2 / 2×players+1 / players / players−1 (min 1)` by
  intensity — Rampage's inversion, Salvo's shape.
- **Elemental crystal scatter**: 16 crystals, radius 850, deterministic seed 45, scattered at
  countdown end (the Salvo recipe) — the element economy for Charge/Mass/Space/Time growth.

## Controller

`BloomrushController : MultiplayerDomainGamesController` — 1 round / 1 turn,
`UseGolfRules = false` (points, higher wins), `HasEndGame = false` (the rule + ClientRpc own the
end), `UseSceneReloadForReplay = true`. `OnCountdownTimerEnded` applies the fuse override and
scatters the elemental crystals on the server; `OnTurnEndedCustom` resolves the winner through
the rule, assigns scores, and broadcasts `SyncFinalScores_ClientRpc` (names / scores / domains /
volumes / fuses-beaten arrays). Objective arrow: `RampageObjectiveProvider` (nearest managed
omni crystal — here the detonator, there the blast trigger). AI is the platform default: crystal
seeking IS the AI's cash-out line, and no external target provider is installed (the Rampage
rule — a mode whose objective is a crystal must never override crystal seeking).

Comeback: `ElementalComebackSystem` maps Bloomrush to `VolumeDestroyed` — the deficit is read
in the quantity the mode SCORES (a count deficit against a volume score is uncalibratable; the
platform's `ScoreDifferenceSource.VolumeDestroyed` exists for exactly this pairing). Card rate
0.00036 in volume units: a quarter of the expected winning volume (300 cactus prisms × 75 =
22,500 → 5,625) buys ~2 element levels, the Dog Fight curve. The generator asserts that a
quarter-of-expected deficit buys at least one whole level — re-derive from the first playtest's
real volumes (`Tools/Build/author_bloomrush_assets.py`).

## Feel

The loop is buttonless, so every beat has to announce itself or the mode reads as nothing
happening. The full account is in `MANTA_STING_KABLOOM.md` §6a; what matters here:

- **The cascade is the mode's payoff** — a cashed board detonates nearest-first with a small
  stagger, so it reads as a chain rolling outward from the pilot instead of one bang.
- **The planter sees their own fuses** — a halo on each bombed target, drawn through the reef,
  quickening and going hot as its fuse burns down. Planter-local; the target sees nothing.
- **The cash-out toasts** (`BloomrushKabloom`, `GameToastConfig_Bloomrush.asset`) in the
  dedicated feed — the only place this mode puts a message.
- **An idle hint** (`BloomrushStingHint`, 25 s, reset by the first Kabloom) tells a new pilot
  what flying into things does, because there is no button to discover.
- **Audio ships silent by design** — six `EventReference` slots on `MantaStingConfig.asset`
  for the audio owner. No new haptic (the two-feel policy; see the doc).

## Registration (all authored by the generator)

| Surface | Entry |
|---|---|
| Enum | `GameModes.Bloomrush = 52` |
| Metric | `ScoringMetric.VolumeDestroyed = 11` → `ScoringMetrics.Read` |
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameBloomrush.unity` (+ Build Settings) |
| Card | `Assets/_SO_Assets/Games/ArcadeGameBloomrush.asset` (Manta-locked, 2–4 players) |
| Live roster | `GameLists/`OrganicRematchGames` (the master) and `ArcadeGames` (the Arcade grid's roster — a master-only card is launchable but invisible in the arcade, `Docs/HomeHub/ARCHITECTURE.md` §3.1) (the modern-mode list — Salvo's shape) |
| Progression | `ProgressionConfig.asset` (`- 52`) |
| Rule | `Scoring Rules/BloomrushScoringRule.asset` (`BloomrushScoringRuleSO`) |
| HUD objective | `MiniGameHUD` case → `RampageObjectiveProvider` ("ObjectiveProvider_Bloomrush") |
| Toasts | `Game Toasts/GameToastConfig_Bloomrush.asset` + a row in `GameToastLibrary.asset` |

Generator: `Tools/Build/author_bloomrush_assets.py` (donor-clone, deterministic guids,
idempotent, `--check`). It asserts on the donor's exact field blocks — the day the Bends scene
is reworked it becomes permanently un-runnable, which is the correct end state for a one-shot
migration; the shipped assets are the record.

## Verification status

**Authored out-of-editor; not yet compiled or played in Unity.** The C# passed a full Roslyn
stub-harness bind check (all method bodies resolved against transcribed real signatures;
negative control fired). In-editor pass still owed: scene opens clean, the four intensities'
fuse/crystal reads, a two-client round end with the scoreboard's team-major rows, and the card
appearing in the Arcade UI. Card art is shared placeholder (the Bends card's sprites) until the
art pass.
