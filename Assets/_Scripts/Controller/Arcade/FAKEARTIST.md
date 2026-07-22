# Fake Artist — Technical Documentation

## Overview

Fake Artist (`GameModes.FakeArtist = 39`) is the free-for-all social-deduction painting
party game — Cosmic Shore's take on the tabletop game *A Fake Artist Goes to New York*,
inverted: here the imposter **knows the subject but not how to draw it**, while the honest
artists **know how to draw their strokes but not what they're drawing**. It is built
directly on the Connect-the-Dots painting toy's stroke toolkit (BACKLOG.md's sanctioned
"separate, score-bearing mode" — the toy itself stays scoreless).

Each round, up to 12 players simultaneously fly 3 assigned strokes of one secret artwork.
Honest artists get full "connect the dots" ring guides; the fake artist gets only each
stroke's start and end. Then everyone but the fake artist votes on two questions — *what
are we drawing?* and *who is the fake artist?* — points land, the full blueprint blooms
over the painted prisms as the reveal, and the next round starts on a fresh canvas. First
player to the win target (default 8) takes the gallery.

- **The drawing IS the deduction surface.** Nothing is announced; players read the
  emerging prism artwork itself. The imposter's improvised middles betray them; the honest
  artists' ring-accurate strokes betray the subject. Working entirely through Prisms/Mass.
- **Everyone is their own team.** No team selection. 12 trail identities = 6 paint colors
  (Jade, Ruby, Gold, Blue + synthetic Fire and Lime) x 2 states (normal / shielded
  octahedron prisms) — identity is carried by the mass itself, per-player scoring rides
  `RoundStats.GoalsScored`.
- **The gallery is conserved mass.** Rounds place their canvases on a golden-angle ring;
  finished artworks persist for the whole session (no TTLs, no cullers — Universality).
  The scene-reload replay is the only sink.
- **Parametric variation, never the same painting twice.** Every round re-generates a
  preset through a seeded transform (yaw, mirror, scale jitter, curl-field warp) and
  repartitions it to exactly `players x 3` medium strokes.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity` — single unified
  scene, no separate singleplayer variant; solo-ish play is a party of one + AI backfill
  (minimum 3 players total). Authored by **Tools ▸ Cosmic Shore ▸ Setup Fake Artist
  Minigame** (clones the NucleusRush scene and swaps the mode stack).
- **GameMode enum**: `GameModes.FakeArtist = 39`, display name "Fake Artist".
- **Controller**: `FakeArtistController : MultiplayerDomainGamesController` (ready-sync +
  feed posts + roster cleanup; the domain-sum NetworkVariables are harmless noise in FFA).
- **Scoring**: `FakeArtistScoringRuleSO`, `metric = ScoringMetric.Goals`, points (not
  golf). PER-PLAYER rule: `UsesPerPlayerWinner => true` switches EndGameSequencer's win
  check and the Scoreboard banner to `WinnerName`. Round tally (`FakeArtistScorer`, values
  on `FakeArtistConfig.asset`): +1 correct subject, +1 correct accusation, flat −1 to any
  player accused by ≥1 voter (imposter or not), +4 to the fake artist every round (a
  caught imposter nets +3). Totals can go negative.
- **Turn monitor**: `FakeArtistTurnMonitor` resolves the win target from
  `EndConditionOverridesSO.GetFakeArtistWinTarget()` at StartMonitor (never a per-scene
  field), syncs via NetworkVariable → `GameDataSO.GoalTargetCount`, and ends the turn when
  the controller's round resolves (draw → vote → reveal). The first-to-N check runs in
  `OnTurnEndedCustom` via `rule.IsObjectiveReached` (per-player scan, never SumByDomain).
- **Domains**: `MinDomainsAllowed = MaxDomainsAllowed = 3` (pinned; cosmetic — domains are
  paint, not teams). `MinPlayersAllowed = 3`, `MaxPlayersAllowed = 12` (the modal's hard
  cap). AI backfill fills `PlayerCount − humans`.
- **Vessels**: all six playable vessels (list copied from the Brood Rush card).
- **AI opponents**: fly their dealt dots via `AIPilot.SetExternalTargetProvider` closures
  (Rampage pattern) with server-side pen control; an AI imposter gets start+end dots only,
  same as a human. AI ballots are injected server-side with config-driven accuracy
  (`AICorrectSubjectChance` / `AICorrectImposterChance`).
- **Comeback**: `ComebackRatePerScoreDeficit = 0` — elemental comeback is a flight-power
  system; deduction rounds shouldn't buff trailing players' vessels. (Source case `Goals`
  registered in `ElementalComebackSystem.EnsureExists` should the rate ever be raised.)
- **Config**: `_SO_Assets/Games/ArcadeGameFakeArtist.asset`, registered in
  `GameLists/OrganicRematchGames.asset` (both the DI list and `GameCard.AllGames` point
  at it) and unlocked via `ProgressionConfig.asset` `alwaysUnlockedModes` (39); mode
  tuning on `_SO_Assets/Games/FakeArtistConfig.asset`. **These assets + the scene are
  committed directly** (not only tool-generated) so the card appears and launches from a
  fresh checkout — Menu_Main's arcade grid has 12 physical card slots, so the 8th game
  fills a spare slot with no scene-hierarchy edit. The `Setup Fake Artist Minigame` tool
  remains the way to *refine* the scene (see limitations).

## Class Inventory (`_Scripts/Controller/Arcade/FakeArtist/`)

| Class | Role |
|---|---|
| `FakeArtistController` | Round phase machine (Idle → Drawing → Voting → Revealing → Resolved), brush table, targeted deal RPCs, vote collection, tally + reveal, first-to-N final sync |
| `FakeArtistBrushes` | The 12 trail identities: slot→(paint domain, shielded) table, synthetic Fire `(Domains)5` / Lime `(Domains)6` material-set minting + registration, per-spawn brush integrity (`PrismKinds.Clear` + `ActivateShield`), UI/ribbon colors |
| `FakeArtistArtworkBuilder` | Pure/deterministic: preset → seeded parametric variation → repartition to exactly `players x strokesPerPlayer` strokes → flight-order deal; ride-dot extraction; golden-angle round anchors; subject-choice building |
| `FakeArtistStrokeGuide` | Local player's private guide: ring markers per dot (imposter: start+end only), distance-latch completion (zero colliders), pen up/down, objective-arrow relay |
| `FakeArtistRevealGhost` | Post-vote whole-artwork LineRenderer blueprint, regenerated locally from (preset, size, seed) — no geometry crosses the wire pre-vote |
| `FakeArtistVotePanel` | Runtime-built overlay UI: role card, two-question timed vote, imposter waiting card, reveal card; CanvasGroup fades |
| `FakeArtistScorer` | Pure vote tally (unit-tested) |
| `FakeArtistConfigSO` | All mode tuning: artwork size, stroke count, phase timings, point values, AI vote accuracy |
| `../Scoring/FakeArtistScoringRuleSO` | Per-player scoring strategy (win check, results, reveal) |
| `../TurnMonitors/FakeArtistTurnMonitor` | Win-target resolve/sync + round-resolved turn end + phase display RPC |

## The draw → vote → score pipeline (server-authoritative)

```
SetupNewRound (server)                    ready gate (all humans) → countdown
  └─ OnCountdownTimerEnded (server)
       ├─ SetupRoundAndDeal():
       │    roster snapshot → brushes asserted (NetDomain.Value = paint domain)
       │    preset + seed → BuildStrokes (players×3) → Deal (contiguous flight order)
       │    imposter = least-often-imposter (server RNG tiebreak)
       │    per-player world dots (imposter degraded to start+end)
       │    → DealRound_ClientRpc  [TARGETED per client - strokes+role stay secret]
       ├─ base: SetPlayersActive + StartTurn (all peers)
       └─ ArmAIPainters()  [SetExternalTargetProvider closures + server pen control]
  Drawing … clients latch dots locally → ReportStrokeComplete_ServerRpc
  [SERVER] all painters done OR DrawSeconds elapsed
  └─ BeginVoting(): ClearAIProviders + HoldAIPens + InjectAIVotes
       → BeginVote_ClientRpc (subject options + roster names)
            non-imposters: two-question panel → SubmitVote_ServerRpc
            imposter: waiting card (identity never leaves the server)
  [SERVER] all ballots in OR VoteSeconds elapsed
  └─ ResolveRound(): FakeArtistScorer.ScoreRound → GoalsScored += delta (NV → live HUD)
       → RevealRound_ClientRpc (subject, imposter, preset/size/seed, deltas)
            every peer: reveal card + FakeArtistRevealGhost blooms over the prisms
  [SERVER] RevealSeconds elapsed → phase = Resolved
  └─ FakeArtistTurnMonitor.CheckForEndOfTurn → InvokeGameTurnConditionsMet
       → SyncTurnEnd_ClientRpc → OnTurnEndedCustom (server gate):
            rule.IsObjectiveReached (any player ≥ GoalTargetCount)?
              no  → base flow → SetupNewRound (next canvas on the gallery ring)
              yes → AssignScores → SyncFinalScores_ClientRpc
                    → WinnerName/WinnerDomain → SetResults
                    → InvokeWinnerCalculated + InvokeMiniGameEnd → Scoreboard
```

- `OnTurnEndedCustom` runs on every peer (from `SyncTurnEnd_ClientRpc`) — gated
  `if (!IsServer || _finalResultsSent) return;`. `SetupNewRound` is suppressed by
  `_finalResultsSent` (the HasEndGame=false pairing).
- Trails are per-peer simulations; identity is per-spawn deterministic from replicated
  state, so every peer sees the same artwork (modulo the platform's accepted per-peer
  spawn-timing drift). Pen-up now replicates via `VesselController.n_TrailPenUp`
  (owner-write NV added for this mode — also fixes freestyle party painting).

## Networking Model

| Concern | Owner | Mechanism |
|---|---|---|
| Brush table (name → slot) | Server assigns | `SyncBrushTable_ClientRpc`; paint color rides `Player.NetDomain` (server-write; synthetic values replicate as raw enum) |
| Shielded brush state | Every peer, deterministically | slot table → `VesselPrismController.SetTrailShielded` + `OnBlockSpawned` integrity hook per peer |
| Stroke assignments + role | Server secret | TARGETED `DealRound_ClientRpc` (`ClientRpcSendParams.TargetClientIds`) — no owner-read NVs exist; broadcast-and-filter is forbidden for secrets |
| Subject | Server secret | Sent only to the imposter in their deal; revealed to all in `RevealRound_ClientRpc` |
| Stroke completion | Client reports, server counts | `ReportStrokeComplete_ServerRpc` (sender resolved via `SenderClientId`, AI excluded — AI shares the host's clientId) |
| Ballots | Client submits, server tallies | `SubmitVote_ServerRpc` (validated: no imposter vote, no self-accusation, no dupes); AI ballots injected server-side |
| Points | Server writes | `RoundStats.GoalsScored` (server-write NV → live per-player HUD cards) |
| Pen state | Owner writes | `VesselController.n_TrailPenUp` (owner-write NV; server owns AI vessels) |
| Win target | Server resolves | `FakeArtistTurnMonitor` NV → `GameDataSO.GoalTargetCount` |
| Final results | Server snapshot | `SyncFinalScores_ClientRpc` (FixedString64Bytes[] names + scores + domains + points) |
| Reveal artwork | Regenerated locally | (preset, size, seed) → `FakeArtistArtworkBuilder` is deterministic on every peer |

Trust note: the host is a player and technically holds the server secrets — acceptable for
a host-authoritative party game (same class as every other mode's host authority).

## The 12 brush identities

Slot order: Jade, Ruby, Gold, Blue, **Fire** `(Domains)5`, **Lime** `(Domains)6`, then the
same six shielded. `FakeArtistBrushes.EnsureSyntheticSetsRegistered` mints the two
synthetic `SO_MaterialSet`s per peer (base-set clone tinted from
`EnvironmentColors.Danger` and `BrightCTA/DarkCTA`) and registers them in
`ThemeManagerDataContainerSO.TeamMaterialSets` — every repaint site is a plain dictionary
lookup, so prisms, shields, steals, transparency AND hulls all just work. Fire is the
danger *look* without `IsDangerous` (no friendly-fire gameplay); shielded brushes use the
real shield state (poppable — but the mode is non-destructive, so shields hold).

Known synthetic-domain blast radius (accepted, mode-local): trail-ribbon colors applied
manually (`TryGetTrailColors`), explosion VFX tint falls back, shared
`GetDomainUIColor` grays out (mode UI uses `FakeArtistBrushes.UIColor`), density
grids/fauna diets don't bucket them — irrelevant here (no Cell). **Never use synthetic
domains in modes with Cell control or domain-aggregated scoring.**

## Ecology configuration

**Fake Artist v1 runs with no active ecology.** The committed scene keeps the cloned
NucleusRush **Cell** as an inert membrane (visual boundary) but **removes the
`NetworkCrystalManager`** — with no bootstrap crystal the Cell's spawner never starts, so
no crystals, flora, or fauna appear. Two hard reasons this matters:
`Prism.OnTriggerEnter(CellItem)` auto-shields touched prisms (which would corrupt
normal-brush identities — the deduction surface), and fauna would graze the gallery
mid-round (the artwork must survive to the vote). The environment IS the accumulating
painted gallery. (The `Setup Fake Artist Minigame` tool goes further and removes the Cell
GameObject entirely; leaving it as inert ambience is an equally valid, lower-risk state.)
If a future pass wants live cytoplasm ambience, wire a standard Cell with an empty
SpawnProfile and no bootstrap crystal via the `/ecology` skill — never a mode-local
substitute.

**Collider-budget impact: ~zero net.** Guide rings/gates/jacks use NO colliders (pure
distance latching). Painted trail prisms carry the standard per-prism BoxCollider budget —
36 medium strokes/round ≈ 700–1100 prisms/round, ~3–6k by game end, comparable to a long
freestyle painting session; shielded-brush prisms keep the authored box trigger (no
always-on MeshColliders). The vote panel is UI-only. No Cell means no fauna collider load
at all.

## End condition

Authored ONLY via **Tools ▸ Cosmic Shore ▸ End Game Conditions**
(`Resources/EndConditionOverrides.asset` → `fakeArtistWinTarget`, 0 = default 8) — see the
`/EndGameConditions` skill. Per-PLAYER race to N points across unbounded rounds
(`numberOfRounds` stays `int.MaxValue`); typical game length ≈ 3–6 rounds.

## Strategy surface (why it's a deduction game, not a drawing test)

- The imposter sees the subject name but must improvise stroke middles — too-straight
  lines between distant dots read as fraud; overconfident flourishes read as knowledge.
- Honest artists trade accuracy against information: drawing your rings cleanly helps the
  group read the subject (your +1) but sharpens everyone's imposter-detection — including
  reads against YOU if your strokes wobble.
- The flat −1 for being accused (imposter or not) makes wild accusations expensive for the
  target and makes blending in valuable for everyone, not just the imposter.
- +4/round means an uncaught imposter wins in two clean rounds — the table must catch them
  fast, but a wrong accusation feeds the −1 economy.
- Brush identity is public (trail look); WHAT you drew is public; only the guides were
  private — deduction runs on flight behavior, not hidden information asymmetries.

## Shared-Code Touchpoints (added for this mode)

| Change | File |
|---|---|
| `FakeArtist = 39` enum entry | `_Scripts/Data/Enums/GameModes.cs` |
| Trail pen-up replication (`n_TrailPenUp`, owner-write) + catch-up apply | `_Scripts/Controller/Vessel/VesselController.cs` |
| `IsSpawnerPaused` getter + `SetTrailShielded` | `_Scripts/Controller/Vessel/VesselPrismController.cs` |
| `UsesPerPlayerWinner` virtual (FFA winner path) | `_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs` |
| Per-player win check branch | `_Scripts/Utility/DataContainers/EndGameSequencer.cs` |
| `SetBannerForPlayer` ("{NAME} WINS") branch | `_Scripts/UI/Scoreboard.cs` |
| `case GameModes.FakeArtist` objective relay | `_Scripts/UI/MiniGameHUD.cs` |
| `case GameModes.FakeArtist` → Goals comeback source | `_Scripts/Controller/Arcade/ElementalComebackSystem.cs` |
| `fakeArtistWinTarget` live+build fields + getter | `_Scripts/ScriptableObjects/EndConditionOverridesSO.cs` |
| Window row + effective/baseline display | `_Scripts/Editor/EndConditionOverridesWindow.cs` |

## Assets

| Asset | Path |
|---|---|
| Arcade card | `_SO_Assets/Games/ArcadeGameFakeArtist.asset` (committed; registered in `GameLists/OrganicRematchGames.asset`) |
| Scoring rule | `_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset` (committed; `metric=Goals`, `golfRules=0`) |
| Mode config | `_SO_Assets/Games/FakeArtistConfig.asset` (committed) |
| Scene | `_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity` (committed clone of MinigameNucleusRush + component swap + crystal-manager removal; in Build Settings) |
| Unlock | `ProgressionConfig.asset` `alwaysUnlockedModes` includes 39 |
| Win target | `Resources/EndConditionOverrides.asset` → `fakeArtistWinTarget` (default 8) |

## Known limitations / follow-ups

- **In-editor verification pending (authored headless).** The card + scene are committed
  and should appear/launch as-is, but no Unity pass has run. Verify: (1) card visible +
  clickable in the Arcade grid; (2) MPPM 2-player run — brush colors/shields on both
  peers, pen-up gaps identical on both peers, deal secrecy (client log has no other
  player's strokes), vote flow, reveal ghost, first-to-8 scoreboard.
- **The committed scene is the minimal clone** (controller/monitor swapped, rule/config
  wired, crystal manager removed). It still carries NucleusRush's **4 spawn points**
  (`GetRandomSpawnPose` recycles them, so a 12-player game overlaps spawns) and its
  **domain-panel in-game HUD** (`MultiplayerHUDView` domain wiring intact → the HUD shows
  3 domain sums rather than per-player cards; the end-game Scoreboard is already
  per-player and correct). Run **Tools ▸ Cosmic Shore ▸ Setup Fake Artist Minigame** to
  refine: it grows the spawn ring to 12, clears the domain-panel wiring for the per-player
  layout, and removes the Cell entirely. The tool is idempotent — it finds the committed
  assets and only refines the scene.
- **Card art is Brood Rush placeholders** (icons + background copied so the card renders);
  replace with dedicated Fake Artist art.
- **Scoreboard/feed tint for Fire/Lime players is gray** (shared `GetDomainUIColor`
  fallback). Mode UI uses `FakeArtistBrushes.UIColor`; extending the shared lookup is a
  candidate follow-up.
- **12 `PlayerScoreEntry` cards** in the legacy per-player HUD layout may need layout
  love at max party size (container is horizontal, not scrolling).
- **AI stroke fidelity** is throttle-limited (`AIPilot` steering); AI drawings look
  sloppy at low intensity — acceptable (humans judge each other; AI are filler), tune via
  intensity→skill if needed.
- **The imposter's vote-phase idle**: the fake artist just waits during the vote; a
  future pass could give them a bluff action (e.g., a decoy "thinking" animation).
- **No haptics** (two-feel policy untouched); no Wwise-specific SFX beyond the shared
  shield-engage sounds.
