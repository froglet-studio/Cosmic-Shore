# Maelstrom Rework — Spec & Wiring Contract

Living spec for the Maelstrom (Tournament meta-mode) AAA polish pass. Canonical mechanics still live
in `ARCHITECTURE.md`; this doc covers the **rework** (the hub-between-rounds flow, the data-driven
scene view, the networked ready-up, and the connecting reveal) and the **UI wiring checklist**.

> **Naming:** *Maelstrom* is the player-facing name (and now the **scene** name). Internal identifiers
> stay **Tournament** (`TournamentController`, `TournamentSceneView`, `TournamentDataSO`,
> `GameModes.Tournament = 36`). One word changed surfaces; the code identity did not.

> **Length model (locked for this pass):** race to `WinTarget = 6` per-domain points (`{2,1,0}` per
> game), hard cap `MaxGames = 7`. Intensity does **not** affect length — it only sets the per-game
> difficulty draw pool (`3 modes × intensity` "experiences").

---

## 1. Round-count math (why the scroll view sizes for ≤6/7)

Round count is driven by the number of **active domains** (teams), not intensity:

| Players | Active domains | Rounds (min–max) | Notes |
|---|---|---|---|
| 2 | 2 (1v1) | **3–4** | both teams gain every game (2 or 1); someone hits 6 by game 4 |
| 3 | 3 | **3–6** | last place earns 0; worst case 5/5/5 after 5, game 6 decides |
| 4 | 3 (2-1-1) | **3–6** | same as 3 domains |
| 4 as 2v2 | 2 | **3–4** | behaves like the 2-domain row |

The `MaxGames = 7` cap **never actually triggers** under `{2,1,0}` / target 6 — it's a dormant safety
net. Practical max is **6** (3 teams) or **4** (2 teams). All intensities 1–4 give identical ranges.
**Scroll views should hold ≤6 cards (size for 7 to be safe).** The round counter shows `ROUND N`
(variable length — can't show "of 6").

---

## 2. The reworked flow

```
Menu/Arcade → Maelstrom (LOBBY, round 0) ── ready-up ──▶ random game
                                                              │ game ends → Scoreboard (Continue, host)
   ┌──────────────────────────────────────────────────────────┘
   ▼
Maelstrom (HUB) — last round's data + standings, ready-up ── ready-up ──▶ next random game
   (repeats until a domain hits 6 / cap 7)
   ▼
Maelstrom (SUMMARY) — winner banner + all rounds ── Next ──▶ Rank panel ── Play Again | Main Menu
```

- **Continue** on a game's Scoreboard always returns to the **Maelstrom scene** (hub mid-run, summary
  when decided) — it no longer jumps straight to the next game.
- The **next mode is drawn at ready-up** (`BeginNextRound`), so the hub never reveals what's next.
- The reveal happens at the **connecting panel** (mode + intensity) as the round loads.

---

## 3. Data model — `TournamentDataSO` (Phase 1)

Per-round history survives the per-scene reset (the UI-only scene has no live `gameData.Players`):

- `TournamentPlayerSnapshot` — `Name, Domain, AvatarId, IsAI, Rank, ScoreText, Secondary`.
- `TournamentRoundRecord` — `RoundNumber, ModeDisplayName, Intensity, List<Players>, List<DomainOrder>`;
  `WinningDomain => DomainOrder[0]`.
- `List<TournamentRoundRecord> History` (`[NonSerialized]`, cleared by `ResetRuntime`).
- Captured in `RecordResults(results, snapshots, modeName, intensity)` at game-end;
  `TournamentController.BuildPlayerSnapshots` resolves avatar/AI from the still-live `gameData.Players`.

---

## 4. Flow / state machine (Phase 2)

`TournamentController`:
- `AdvanceToNextGame()` → `LoadTournamentScene()` (hub or summary by phase).
- `BeginNextRound()` → draw + load the next random game (called by the hub ready-up).
- `RestartTournament()` (Play Again) → loads a **fresh lobby** (reset keeps the intensity ceiling via
  `RestartFromSummary`).
- `HandleSceneLoaded` (Maelstrom scene): `Complete`→Summary · `Summary`→restart · active+games→**hub
  (no reset)** · else→fresh start.
- State machine adds `InGame → Lobby`.

---

## 5. Networked ready-up — `TournamentLobbyNetwork` (Phase 3)

Scene-placed `NetworkBehaviour` (host-authoritative). 30s auto-start; snaps to 5s once **every
connected client** is ready. Deadline + ready tally are `NetworkVariable`s so every peer renders the
countdown (in the Ready button). Arms/advances **only in phase Lobby**; fires `BeginNextRound()` at the
deadline. Public: `SecondsRemaining`, `LocalReady`, `ReadyCount`, `TotalPlayers`, `ToggleLocalReady()`.

---

## 6. Scene view + components (Phase 4) — the wiring contract

`TournamentSceneView` drives three layouts by phase: **active** (lobby + hub), **summary**, **rank**.
The active/summary scrolls hold **Tournament Data Cards**, each nesting its round's **Player Data Cards**.

### Two prefabs (+ one for rank rows)
- **Tournament Data Card** = `TournamentRoundCard` — round header + its own player-card container.
  Fields: `roundNameText` (mode), `roundNumberText` (opt), `winningDomainText`, `winnerColorTargets[]`,
  `winningDomainRoot` (hidden in preview), `currentRoundHighlight`, `playerCardPrefab`
  (`TournamentPlayerCard`), `playerCardContainer`. `Setup(record,…)` for a completed round;
  `SetupPreview(roundNumber, roster,…)` for the round-0 lobby (roster, no round score/winner).
- **Player Data Card** = `TournamentPlayerCard` — fields: `avatarImage`, `nameText`, `roundScoreText`,
  `roundScoreRoot` (opt, hidden in preview), `totalScoreText`, `totalScoreRoot` (opt), `colorTargets[]`
  (domain tint). **Round Score** = the player's result that round (`snapshot.ScoreText`); **Total Score**
  = the player's DOMAIN cumulative tournament points, as-of that round (climbs across cards).
- `TournamentDomainScoreView` — `placeText, domainNameText, pointsText, colorTargets[], youBadge`.
  Used for the **summary rank rows**.

### `TournamentSceneView` serialized fields
| Group | Fields |
|---|---|
| Data | `gameData`, `tournamentData`, `lobbyNetwork` |
| Shared | `titleText` |
| Active — top bar | `activeRoot`, `gameModesText` (pool), `roundCounterText` ("ROUND N"), `raceRuleText` ("First domain to {WinTarget} points wins"), `leadingDomainText`+`leadingDomainColorTargets[]`, `standingsText` (opt — cumulative now lives in the player cards) |
| Active — scroll | `roundCardPrefab` (Tournament Data Card) + `historyContent`, `historyScrollRect` |
| Active — START | `readyButton`+`readyButtonLabel`, `readyTallyText` (opt) |
| Summary | `summaryRoot`, `winnerBannerText`, `winnerBannerColorTargets[]`, `summaryHistoryContent`, `nextButton` |
| Rank | `rankRoot`, `rankRowPrefab` (`TournamentDomainScoreView`), `rankContainer`, `playAgainButton`, `mainMenuButton`, `onClickToMainMenu` |
| Avatars | `profileIconList`, `aiProfileList` |

> Button `onClick`s are **auto-wired in code** — assign the `Button` refs only; do **not** add inspector
> `onClick` entries for ready/next/playAgain/mainMenu (double-fire).

- Round-0 lobby shows a single **preview card** (the roster, no scores) so the intro isn't empty.
- Player roster + round cards reorder by the overall leader (`BuildSortedStandings`).
- `standingsText` / `leadingDomainText` surface the cumulative race tally (the "first to 6" state).

---

## 7. Connecting reveal + splash trim (Phase 5) + polish (Phase 6)

- `TournamentStandingsFormatter.FormatConnecting()` — lightweight splash text (leading domain +
  up-next mode/intensity). `BootStatusBroadcaster` shows it only when a **game** is loading (clean
  splash when the **hub** loads).
- Between-game dwell retired (`BetweenGameSummaryDwellSeconds = 0`).
- `TournamentConnectingInfo` — in-scene connecting-panel reveal (`modeNameText, intensityText,
  leadDomainText, leadDomainColorTargets[]`); resolves on every peer from synced config. Drop on each
  domain-game HUD's connecting panel; call `Refresh()` (runs on enable) when shown.
- Polish: smooth auto-scroll to the latest round card; ready-button countdown pulse.

---

## 8. Phase status

| Phase | Status |
|---|---|
| 0 Scene rename | ✅ |
| 1 Data model | ✅ |
| 2 Flow / state machine | ✅ |
| 3 Networked ready-up | ✅ |
| 4 Scene view + card components | ✅ |
| 5 Connecting reveal + splash trim | ✅ |
| 6 Polish (auto-scroll, pulse) | ✅ |
| **7 UI authoring + wiring** | ⬜ (user) |

---

## 9. Phase 7 — UI wiring checklist (Maelstrom.unity)

1. **Prefabs:** build the **Player Data Card** (`TournamentPlayerCard` — round + total scores), the
   **Tournament Data Card** (`TournamentRoundCard`, with its `playerCardPrefab` = the Player Data Card +
   `playerCardContainer`), and a `TournamentDomainScoreView` prefab for the summary rank rows.
2. **Lobby network:** add a GameObject with **`NetworkObject` + `TournamentLobbyNetwork`** (autoStart 30,
   allReady 5).
3. **Scene view:** build `activeRoot` / `summaryRoot` / `rankRoot` and wire every field in §6 on the
   `TournamentSceneView`. (`gameData`, `tournamentData`, `titleText`, `onClickToMainMenu` survive from the
   old component; the rest are new.)
4. **Connecting reveal:** add `TournamentConnectingInfo` to each domain-game HUD's connecting panel and
   wire its fields (and drive the panel's show/hide if it isn't already).
5. **Verify:** solo+bots full run (Continue → hub → ready → next; 30s auto / 5s all-ready; auto-scroll;
   summary → Next → rank → Play Again/Main Menu); 2-player MPPM (countdown + standings identical across
   peers; clients see no host-only buttons); a normal game after a Maelstrom uses the standard buttons.

### Open question (Phase 5 surface)
The in-scene `ConnectingPanel` has **no current driver** in code (no `ToggleConnectingPanel` caller). If
you want the richer in-scene reveal (vs. the splash-only `FormatConnecting`), we need to wire its
show/hide into the pre-game flow. Flag which surface you want and I'll wire it.

---

## v2 — UI integration (Shombith) ✅ (2026-06-19)

Driven by the final Maelstrom intro-panel UI. All ✅ done; everything above is unchanged — this section
only adds the v2 deltas + the exact scene-wiring map.

### What changed (code)
- **Player Data Card → `TournamentPlayerCard`** (new component): shows **Round Score** (that round's
  result) + **Total Score** (the player's DOMAIN cumulative tournament points, *as-of that round*, so it
  climbs across cards). Replaces the `PlayerScoreCard` reuse inside the round card.
- **Tournament Data Card (`TournamentRoundCard`)** nests `TournamentPlayerCard`s and takes a domain→total
  resolver. The winning-domain block always renders — `WINNING DOMAIN : —` in the preview / undecided round.
- **Domain-coloured names via TMP rich text** — only the NAME is tinted (`LEADING DOMAIN : <jade>JADE</jade>`,
  `WINNING DOMAIN : <jade>JADE</jade>`); the label stays white. The **card BG** tints to the winner via
  `winnerColorTargets` (assign the card's background image), and the **player row** tints via the player
  card's `colorTargets`.
- **Separate animated countdown text** (`countdownText`) — "Game will start in {N}s", DOTween punch each
  tick. The button face is now optional (`readyButtonLabel` shows START / READY ✓ only when wired).
- **Local countdown fallback** (`localCountdownSeconds`, default 30) — ticks + animates for panel testing
  even before `TournamentLobbyNetwork` is in the scene (display-only, no auto-advance).
- **Top-bar labels** carry their full prefix in one TMP: `GAMEMODES : …`, `LEADING DOMAIN : …`, `ROUND N`,
  and `raceRuleText` = "First domain to {WinTarget} points wins". Round counter is `ROUND N` (no "/6").
- Round 0 shows a single **preview card** (roster, no scores, winner `—`); AI appear from round 1.

### Scene-wiring map (Maelstrom.unity → final hierarchy)

**`TournamentSceneView`** (on `GameCanvas - TournamentSceneView`):

| Field | Hierarchy object |
|---|---|
| `gameData` / `tournamentData` | GameData SO / `TournamentData.asset` |
| `lobbyNetwork` | *(empty for the first test — add the NetworkObject later)* |
| `titleText` | Title Text (TMP) |
| `activeRoot` | Intro Panel |
| `gameModesText` | PoolText |
| `roundCounterText` | RoundStatusText |
| `raceRuleText` | InfoText |
| `leadingDomainText` | LeadingDomainText |
| `roundCardPrefab` | **MaelstromRoundScoreCardBG prefab asset** |
| `historyContent` | Content |
| `historyScrollRect` | MaelstromSummaryScrollView |
| `readyButton` | Start Button |
| `readyButtonLabel` | Start Button ▸ Text (TMP) *(optional)* |
| `countdownText` | Text (TMP) (1) — "Game will start in X…" |
| `profileIconList` / `aiProfileList` | `SO_ProfileIconList` / `MainAIProfileList.asset` |
| `leadingDomainColorTargets` / `standingsText` | *(leave empty — name is rich-text coloured; cumulative lives in the cards)* |
| Summary / Rank fields | *(later — Summary Panel)* |

**`TournamentRoundCard`** (on the MaelstromRoundScoreCardBG prefab):

| Field | Hierarchy object |
|---|---|
| `roundNameText` | RoundNameText |
| `winningDomainText` | WinningDomainText |
| `winnerColorTargets` | the card's background **Image** (MaelstromRoundScoreCardBG) |
| `playerCardPrefab` | **MaelStromPlayerScoreCardContainer prefab asset** |
| `playerCardContainer` | PlayerScoreCardContainer |
| `roundNumberText` / `winningDomainRoot` / `currentRoundHighlight` | *(optional)* |

**`TournamentPlayerCard`** (on the MaelStromPlayerScoreCardContainer prefab):

| Field | Hierarchy object |
|---|---|
| `avatarImage` | Avatar |
| `nameText` | PlayerNameText |
| `roundScoreText` | RoundScoreText |
| `totalScoreText` | TotalScoreText |
| `colorTargets` | the player row's border/background **Image** |
| `roundScoreRoot` / `totalScoreRoot` | *(optional — auto-hide when empty)* |

### Notes
- `roundCardPrefab` / `playerCardPrefab` must point to the **prefab assets**, not the scene template
  instances — the templates under `Content` (and the sample player row) are cleared at runtime and
  rebuilt from data, so an unbound prefab = empty scroll.
- `lobbyNetwork` is optional for the first panel test (local countdown covers the animation); add a
  `NetworkObject + TournamentLobbyNetwork` GameObject for the real 30s/5s ready-up.
- With no live session (Play straight from the scene), the roster is empty (no Player objects) — the
  chrome + countdown render, but cards have no rows. Run via the game flow (or ask for a debug sample-data
  toggle) to see populated cards.
