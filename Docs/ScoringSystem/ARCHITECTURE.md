# Scoring System — Architecture

Canonical reference for the **Scoring System** in Cosmic Shore — the shared
scoring data plus the two UI surfaces that present it:

1. **In-game score UI** — the live HUD shown during gameplay (per-domain team
   scores + the local player's centerline score).
2. **Final scoreboard** — the end-game results screen (ranked player cards,
   winner banner, mode stats, lobby buttons).

("Scoring System" is the umbrella term; "scoreboard" below refers specifically
to the end-game surface.)

Both are *views* over two **fundamentals** — **Domain** (team identity/color)
and **scoring** (per-player `Score` + per-domain sums). They are not new
systems; they render the same `GameDataSO` / `IRoundStats` data two ways.

> **Marking convention.** Sections tag the end-state we're refactoring toward:
> **[current]** = as-built today, **[legacy]** = to be removed,
> **[target]** = the agreed end-state (see `REFACTOR.md`). The doc doubles as the
> refactor's north star, so read the [target] notes as binding intent, not just
> description.

---

## Diagrams: current → target

These two diagrams are the **map for the refactor**: we are moving from CURRENT
to TARGET. TARGET keeps the same class hierarchy + SOAP lifecycle — it removes a
fork, deletes a dead layout, and de-duplicates (one animator, one color source,
one reward, one sentinel helper). It is consolidation, not a rewrite. See
`REFACTOR.md` for the sequenced steps.

### Current (as-built)

```
[DATA — shared]
RoundStats : NetworkBehaviour (IRoundStats)
  └ NetworkVariables ──replicate──► observer events
       (OnScoreChanged, OnCrystalsCollectedChanged, OnJoustCollisionChanged…)
GameDataSO : data container + SOAP channels
  • RoundStatsList · DomainStatsList · LocalRoundStats
  • WinnerName · WinnerDomain
  • flags: IsMultiplayerMode (!) · IsGolfRules · RequestedDomainCount
  • SOAP: OnMiniGameTurnStarted/End · OnWinnerCalculated ·
          OnShowGameEndScreen · OnResetForReplay
        ▲ writes winner + CalculateDomainStats   │ raises SOAP
[server] MiniGameControllerBase ▸ per-mode (domain-aggregated winner)

[SURFACE A — in-game HUD (live)]
MiniGameHUD ─► MultiplayerHUD (abstract)
  ├ GetInitialCardValue()             ◄ per-mode metric
  ├ layout = HasDomainPanelWiring ? domain : legacy        (!)
  │    ├ domain ─► DomainScorePanel   (team sum + avatars)
  │    └ legacy ─► PlayerScoreEntry   (one card per player)
  └ centerline score ◄ local OnScoreChanged
  per-mode: HexRaceHUD · MultiplayerJoustHUD · MultiplayerCrystalCaptureHUD

[SURFACE B — end-game (results)]
OnWinnerCalculated
  └ EndGameCinematicController (+Hex/Joust/Crystal)    reads IsMultiplayerMode (!)
      └ …cinematic… ─► InvokeShowGameEndScreen
          └ Scoreboard (+Hex/Joust/Crystal/Duel/CoOp)  reads IsMultiplayerMode (!)
              ├ SortPlayers / FormatPlayerScore   ◄ per-mode
              ├ PlayerScoreCard
              └ ScoreboardStatsProvider ─► StatRowUI

[shared] HUDAnimationSettingsSO (config asset)

(!) SMELLS
 1  score-anim CODE duplicated ×3 → PlayerScoreEntry, PlayerScoreCard, DomainScorePanel
 2  IsMultiplayerMode fork        → Scoreboard, PauseMenu, EndGameCinematic, MultiplayerSetup…
 3  two HUD layouts coexist       → domain vs legacy per-player
 4  domain→color resolved 3 ways  → ThemeManagerData / DomainColorPaletteSO / view list
 5  2 crystal-reward amounts + magic loser sentinels (10000 / 99999)
 6  [FLOW-*] Debug.Log spam in MiniGameHUD
```

### Target (goal — approved)

```
(consolidation, NOT a rewrite — same hierarchy + same SOAP lifecycle)

[DATA — shared]   (same shape; one removal)
RoundStats : NetworkBehaviour (IRoundStats)
  └ NetworkVariables ──replicate──► observer events        (unchanged)
GameDataSO : data container + SOAP channels
  • RoundStatsList · DomainStatsList · LocalRoundStats · Winner{Name,Domain}
  • IsGolfRules · RequestedDomainCount       ◄ IsMultiplayerMode REMOVED
  • SOAP lifecycle events                     (unchanged)

[server] per-mode controller — domain-aggregated, RPC-synced   (unchanged)

══ ONE unified, always-networked path ══   (solo = host + AI  ≡  online)

[SURFACE A — in-game HUD]
MiniGameHUD ─► MultiplayerHUD
  ├ GetInitialCardValue()        ◄ per-mode metric   (unchanged)
  └ DomainScorePanel  ── the ONLY layout   (legacy per-player REMOVED)

[SURFACE B — end-game]
OnWinnerCalculated ─► EndGameCinematicController (+per-mode)
  └ InvokeShowGameEndScreen ─► Scoreboard (+per-mode)
      ├ SortPlayers / FormatPlayerScore   ◄ per-mode
      ├ PlayerScoreCard
      └ ScoreboardStatsProvider ─► StatRowUI
  lobby buttons keyed off concrete signals (host? connected-client count?)
     ── NOT IsMultiplayerMode

[shared — NEW, extracted once]
ScoreNumberAnimator   (roll · punch · color-flash; driven by HUDAnimationSettingsSO)
  reused by ► DomainScorePanel · PlayerScoreEntry · PlayerScoreCard   (DRY)

domain→color : ONE source (theme palette)
crystal reward: ONE source of truth
loser score  : ONE encode/decode helper (no magic literals)
logging      : no [FLOW-*] spam
```

---

## 1. The two surfaces at a glance

| | In-game HUD | Final scoreboard |
|---|---|---|
| Entry class | `MiniGameHUD` → `MultiplayerHUD` | `Scoreboard` |
| Shown when | gameplay turn active | after `OnWinnerCalculated` → cinematic |
| Trigger event | `OnMiniGameTurnStarted` | `OnShowGameEndScreen` |
| Per-player widget | `PlayerScoreEntry` (live) | `PlayerScoreCard` (final) |
| Per-team widget | `DomainScorePanel` | banner + domain-tinted cards |
| Data read | `IRoundStats.OnXxxChanged` (live) | `RoundStatsList` / `DomainStatsList` (snapshot) |
| Mode customization | abstract `GetInitialCardValue` + stat event | virtual `SortPlayers`/`FormatPlayerScore` |

---

## 2. Data layer (shared)

### `IRoundStats` (`_Scripts/Data/Enums/IRoundStats.cs`)
Per-player stat snapshot. Implemented by `RoundStats` (a `NetworkBehaviour`),
so every property is backed by a server-authoritative `NetworkVariable` and
each change raises an observer event:

- Identity: `Name`, `Domain`.
- Primary: `float Score` + `OnScoreChanged`.
- Mode metrics: `CrystalsCollected`, `OmniCrystalsCollected`, `JoustCollisions`,
  … each with an `OnXxxChanged` event (e.g. `OnOmniCrystalsCollectedChanged`).
- `Cleanup()` zeroes everything for replay.

`DomainStats` (same file) is the per-team aggregate: `{ Domains Domain; float Score; }`.

### `GameDataSO` scoring API (`_Scripts/Utility/DataContainers/GameDataSO.cs`)
The shared hub. **SOAP events** (all `ScriptableEventNoParam`):
`OnMiniGameTurnStarted`, `OnMiniGameTurnEnd`, `OnWinnerCalculated`,
`OnShowGameEndScreen`, `OnResetForReplay` — raised via `InvokeTurnStarted()`,
`InvokeWinnerCalculated()`, `InvokeShowGameEndScreen()`, etc.

**State:**
- `RoundStatsList : List<IRoundStats>` — every player (local + remote + AI).
- `DomainStatsList : List<DomainStats>` — per-domain aggregates (winner first
  once sorted).
- `LocalRoundStats` — the local player's stats.
- `WinnerName`, `WinnerDomain` — server-authoritative result.
- `IsGolfRules` (get/private set), `RequestedDomainCount` (=3),
  `IsMultiplayerMode` **[legacy — see §8]**.
- **Server-synced per-domain metric sums** (BUGS.md B9 / "Approach B"):
  `GetDomainMetricSum(d)` / `SetDomainMetricSum(d, v)` + `OnDomainMetricSumsChanged`.
  The server computes each domain's metric sum and replicates it; clients display it
  verbatim instead of re-summing per-player stats on the client (see §3).

**Methods:** `SortRoundStats(golf)`, `SortDomainStats(golf)`,
`CalculateDomainStats(golf)`, `IsLocalDomainWinner(out DomainStats)`,
`SumCrystalsCollectedByDomain(d)`, `SumJoustCollisionsByDomain(d)`,
`TryGetDomainReachingCrystalTarget(target, out winner)`,
`TryGetDomainReachingJoustTarget(...)`.

> **[target]** Scoring is already domain-aggregated and the stat layer is
> already RPC-synced (`RoundStats` NetworkVariables). The refactor's job is to
> make the **views** consume this uniformly for solo-host and online alike — not
> to add new data.

---

## 3. Surface A — In-game HUD

### Class map
| Class | File | Role |
|---|---|---|
| `MiniGameHUD` | `_Scripts/UI/MiniGameHUD.cs` | Base: centerline score, local + AI cards, lifecycle |
| `MiniGameHUDView` | `_Scripts/UI/View/MinigameHUDView.cs` | `scoreDisplay`, `playerScoreContainer`, ready button, etc. |
| `MultiplayerHUD` | `_Scripts/UI/MultiplayerHUD.cs` | Abstract: domain-panel vs legacy per-player layout |
| `MultiplayerHUDView` | `_Scripts/UI/View/MultiplayerHUDView.cs` | `allyDomainContainer`, `opposingDomainsContainer`, `domainPanelPrefab` |
| `DomainScorePanel` | `_Scripts/UI/Elements/DomainScorePanel.cs` | Per-team sum + avatar row |
| `PlayerScoreEntry` | `_Scripts/UI/Elements/PlayerScoreEntry.cs` | Live per-player card (also reused as avatar chip) |
| `ObjectiveIndicator` | `_Scripts/UI/…` | Off-screen objective pointer (auto-created HexRace/Joust) |
| `CurrentScore` | `_Scripts/UI/CurrentScore.cs` | Special case: volume-difference (Jade − Ruby) display |

### Layout switch
`MultiplayerHUD` picks its layout at `OnMiniGameTurnStarted`:
`_useDomainView = multiplayerView.HasDomainPanelWiring` (true when ally +
opposing containers + panel prefab are all wired).

- **[current/target] domain layout** — local domain panel in the LEFT (ally)
  container, 1-2 opposing panels in the RIGHT container (only domains that have
  players; bounded by `RequestedDomainCount`).
- **[legacy] per-player layout** — one `PlayerScoreEntry` per player in
  `PlayerScoreContainer`, kept only as a fallback for scenes not yet wired for
  domain panels. **Slated for removal** (`REFACTOR.md` R6).

### Lifecycle
```
OnClientReady ──► HandleClientReady (UniTask): Show → cleanup → pre-game
                  cinematic → unlock & show Ready button
OnMiniGameTurnStarted ──► MiniGameHUD: subscribe localRoundStats.OnScoreChanged;
                          SetupLocalPlayerCard; (isAIAvailable) SetupAICards
                       └► MultiplayerHUD (override): choose layout;
                          InitializeDomainPanels | InitializePlayerCards;
                          SubscribeToPlayerStats(each)
OnMiniGameTurnEnd ──► unsubscribe; clear cards/panels
OnResetForReplay ──► reset score UI to "0"
```

### Score → screen dispatch (observer)
```
RoundStats NetworkVariable (server write, replicated)
   └► IRoundStats.OnXxxChanged                    (e.g. OnOmniCrystalsCollectedChanged)
       └► <Mode>HUD.HandleXxxStatChanged          (HexRaceHUD / JoustHUD / CrystalCaptureHUD)
           └► MultiplayerHUD.HandlePlayerStatChanged(stats)
               ├─[domain]► DomainScorePanel.UpdateSum( SumStatByDomain(domain) )
               └─[legacy]► PlayerScoreEntry.UpdateScore( GetInitialCardValue(stats) )
```
- `GetInitialCardValue(stats)` = `gameData.ScoringRule.LiveMetric(stats)` — the
  per-mode metric (HexRace/CrystalCapture → Crystals, Joust → Jousts), used by the
  **legacy per-player** card. `SumStatByDomain(domain)` no longer re-sums on the
  client — it returns the **server-synced** `GameDataSO.GetDomainMetricSum(domain)`
  (BUGS.md B9; see the subsection below).
- The **centerline score** (`view.scoreDisplay`) is the *local* player's
  `Score`, driven separately by `MiniGameHUD.UpdateScoreUI` on
  `localRoundStats.OnScoreChanged`.

### [B9 / Approach B] Server-authoritative domain sums + reactive panels
Clients do **not** re-sum per-player stats for the domain boxes — a client's OWN
`RoundStats` metric can fail to replicate to the owner (BUGS.md B9). Two pieces:

- **Values (server-authoritative).** `MultiplayerDomainGamesController` (the base of
  HexRace / Joust / CrystalCapture) runs a throttled (0.1s) **server** coroutine that
  writes `ScoringMetrics.SumByDomain(gameData, rule.Metric, ActiveDomains[i])` into
  three `NetworkVariable<int>`. Every peer's `OnValueChanged` mirrors the value into
  `GameDataSO.SetDomainMetricSum` (→ `OnDomainMetricSumsChanged`), and
  `MultiplayerHUD.SumStatByDomain` returns `gameData.GetDomainMetricSum`. So every
  client shows exactly the host's number, and the single code path serves all three
  modes via the per-mode `rule.Metric` (Crystals / Jousts).
- **Structure (reactive).** `RoundStats.n_Domain` / `n_Name` now raise
  `OnAnyStatChanged`; `MultiplayerHUD` rebuilds the panel set whenever the
  ally/opposing domain set or roster changes (`RebuildDomainPanels` + allocation-free
  `DomainLayoutChanged`) and subscribes `OnPlayerAdded`, instead of snapshotting once
  at turn start (BUGS.md B8).

> **Legacy gap:** the above covers only the **domain** layout. The legacy per-player
> layout still reads per-player `RoundStats` directly and can show a frozen own-card
> on a client — `TODOS.md` TD1 (recheck).

---

## 4. Surface B — Final scoreboard

### Pipeline (event-driven, server-authoritative winner)
```
Game controller (server) — OnTurnEndedCustom / domain-aggregated end check
   ├─ set gameData.WinnerName / WinnerDomain
   ├─ gameData.CalculateDomainStats(golf)            ← sorts DomainStatsList
   └─ gameData.InvokeWinnerCalculated()
        └► EndGameCinematicController.OnWinnerCalculated   (_Scripts/Utility/DataContainers/)
             RunCompleteEndGameSequence:
               victory lap → camera moves → PlayScoreRevealSequence(virtual)
               → AwardCrystalReward(delegated) → intensity/quest toasts
               → Continue button → connecting panel → ResetGameForNewRound
             └─ gameData.InvokeShowGameEndScreen()
                  └► Scoreboard.ShowScoreboard()             (_Scripts/UI/Scoreboard.cs)
                       ConfigureLobbyButtons()
                       ShowMultiplayerView():
                         SortPlayers(RoundStatsList)          (virtual)
                         DetermineWinnerDomain()              (virtual; DomainStatsList[0])
                         SetBannerForDomain()  → "{DOMAIN} VICTORY"
                         PopulatePlayerCards() → one PlayerScoreCard per player
                       PopulateDynamicStats() → ScoreboardStatsProvider.GetStats()
```

### `Scoreboard` (base)
- Subscribes `OnShowGameEndScreen → ShowScoreboard`, `OnResetForReplay →
  HideScoreboard`.
- `PopulatePlayerCards`: instantiates `PlayerScoreCard` per ordered player,
  tints to domain color, sets avatar (`SO_ProfileIconList` for humans /
  `SO_AIProfileList` for AI), optional secondary stat, `+N` crystal reward on
  the winner, and awards crystals to the local player if they won
  (`AwardCrystalsIfLocalWinner`).
- `ConfigureLobbyButtons`: host/single-player see **Main Menu + Play Again**;
  non-host clients see **Leave Lobby**. **[legacy dependency]** gated on
  `IsMultiplayerMode` (§8).
- Virtual extension points: `SortPlayers`, `DetermineWinnerDomain`,
  `FormatPlayerScore`, `FormatSecondaryStat`, `SetBannerForDomain`.

### `PlayerScoreCard` (`_Scripts/UI/PlayerScoreCard.cs`)
End-game row: avatar, name, formatted score, domain-tinted background,
optional secondary stat line, optional `+N` crystal reward. Entrance/punch/roll
animations from `HUDAnimationSettingsSO`. (Distinct from the in-game
`PlayerScoreEntry`.)

### `EndGameCinematicController` (`_Scripts/Utility/DataContainers/`)
Runs the cinematic between winner-calculation and the scoreboard. Crystal reward
is **delegated to the scoreboard** by default
(`delegateCrystalRewardToScoreboard = true`) to avoid double-award. Per-mode
subclasses override `DetermineLocalPlayerWon` (compares `gameData.WinnerDomain`
to the local domain) and `PlayScoreRevealSequence` (the big VICTORY/DEFEAT
number).

### Dynamic stats providers
`ScoreboardStatsProvider` (abstract) → `GetStats() : List<StatData>`, rendered as
`StatRowUI` rows under the cards. Implementations: `HexRaceStatsProvider`,
`MultiplayerJoustStatsProvider`, `MultiplayerCrystalCaptureStatsProvider`,
`WildlifeBlitzStatsProvider`, and the generic `UniversalStatsProvider`
(`StatModuleSO`-bound). All in `_Scripts/Controller/Arcade/` or `_Scripts/UI/`.

---

## 5. Per-mode reference

| Mode (id) | HUD subclass | In-game metric (event) | Scoreboard subclass | Sort | Winner score | Loser score | End-game reveal |
|---|---|---|---|---|---|---|---|
| **HexRace** (33) | `HexRaceHUD` | `OmniCrystalsCollected` (`OnOmniCrystalsCollectedChanged`) | `HexRaceScoreboard` | golf ↑, tiebreak `CrystalsCollected`↓ | `MM:SS:CS` (score < 10000) | `"{N} Crystals Left"` (10000 + N) | VICTORY/RACE TIME • DEFEAT/CRYSTALS LEFT |
| **Joust** (34) | `MultiplayerJoustHUD` | `JoustCollisions` (`OnJoustCollisionChanged`) | `MultiplayerJoustScoreboard` | golf ↑, tiebreak `JoustCollisions`↓ | `MM:SS:CS` (score < 99999) | `"{N} Joust(s) Left"` (domain deficit) | VICTORY/WON BY N JOUSTS • DEFEAT/LOST BY N JOUSTS |
| **Crystal Capture** (35) | `MultiplayerCrystalCaptureHUD` | `CrystalsCollected` (`OnCrystalsCollectedChanged`) | `MultiplayerCrystalCaptureScoreboard` | points ↓ | `"{N} Crystals"` | `"{N} Crystals"` | VICTORY/WON BY N CRYSTALS • DEFEAT/LOST BY N CRYSTALS |
| **Cellular Duel** (29) | `MiniGameHUD`/`MultiplayerHUD` (per scene) | `Score` | `DuelForCellScoreboard` | points ↓ | `"{N}"` | `"{N}"` | base `EndGameCinematicController` |
| **Wildlife Blitz** co-op (32) | `MiniGameHUD` (`isAIAvailable`) | `Score` | `CoOpScoreBoard` (base + extra opponent-score field) | base | base | base | base |

> **Magic loser sentinels.** HexRace encodes losers as `10000 + crystalsLeft`,
> Joust as `99999`. The threshold (`< 10000f` / `< 99999f`) is duplicated as a
> literal in both the controller (write) and the scoreboard (decode) — fragile;
> see `REFACTOR.md` R4.

---

## 6. Shared look/feel

- **`HUDAnimationSettingsSO`** (`_Scripts/UI/HUDAnimationSettingsSO.cs`, asset
  referenced by every card/panel) is the single source for entrance, score
  punch, counter roll, color flash, countdown, HUD fade, and scoreboard
  entrance/banner timings + `useUnscaledTime`. Per Config Separation, tuning
  lives here, not on per-widget SerializeFields.
- **Domain → color** resolution order: `ThemeManagerData.ColorSet`
  (`TryGetColorSetByDomain`) → `DomainColorPaletteSO` → `MiniGameHUDView.domainColors`
  (white fallback). The end-game `Scoreboard` additionally has hardcoded
  `*TeamBannerColor` fallbacks. Three paths today → unify (`REFACTOR.md` R5).

---

## 7. Patterns to follow

- **SOAP + observer, no polling.** React to `IRoundStats.OnXxxChanged` and
  `GameDataSO` SOAP events; never poll score state per-frame. Fail loud on
  missing SOAP refs (no if-null guards on event fields).
- **Server-authoritative, domain-aggregated.** The controller decides the winner
  on the server via domain sums (`TryGetDomainReaching*`), writes
  `WinnerName`/`WinnerDomain`, and broadcasts. Views never compute the winner.
- **Subclass, don't fork.** Add a mode by overriding the virtual
  `Sort/Format*` (scoreboard) and implementing the abstract metric selector
  (HUD) — not by branching inside the base classes.
- **Golf vs points** is a per-mode sort decision (`IsGolfRules`), not a base
  special case.

---

## 8. [target] One unified, always-networked scoring path

The game always runs as a **network host**, so "solo" is just host + AI. The
scoring data is already RPC-synced; the goal is to delete the
single-player/multiplayer **fork** so solo-host and online render identically
(domain-aggregated, RPC-synced).

**`IsMultiplayerMode` fork map (current call sites):**

| File:line | Use |
|---|---|
| `Controller/Managers/Arcade.cs:66,107,162` | **writes** the flag; `:107` sets `isMultiplayer && SelectedPlayerCount > 1` (so a 1-player game is marked non-MP) |
| `Controller/Arcade/MultiplayerMiniGameControllerBase.cs:44,450` | reads/writes during config sync |
| `UI/Scoreboard.cs:154,456` | lobby-button visibility + Play-Again host guard |
| `UI/PauseMenu.cs:131` | pause-menu behavior |
| `Utility/DataContainers/EndGameCinematicController.cs:245` | `fadeLoserTrail` only when MP |
| `Controller/Multiplayer/MultiplayerSetup.cs:86` | session creation path |
| `Controller/Party/HostConnectionService.cs:1714` | presence activity string |
| `System/SceneLoader.cs:139` | log line only |

**Target:** route all the above through the always-networked host model (solo =
host + AI), driving lobby/cinematic/scoreboard behavior off concrete signals
(connected client count, `WinnerDomain`, domain membership) rather than the
`IsMultiplayerMode` boolean, then remove the flag. **This is a discuss-first
item** (`REFACTOR.md` R1) — agree the per-site replacement before any code.

---

## 9. How to add a new game mode's scoreboard + HUD

1. **In-game HUD:** subclass `MultiplayerHUD`; implement `GetInitialCardValue`
   (the metric) and `SubscribeToPlayerStats`/`UnsubscribeFromPlayerStats`
   (the matching `IRoundStats.OnXxxChanged`), forwarding to
   `HandlePlayerStatChanged`. (See `HexRaceHUD` — ~25 lines.)
2. **End-game scoreboard:** subclass `Scoreboard`; override `SortPlayers`
   (golf vs points + tiebreak) and `FormatPlayerScore` (+ optional
   `FormatSecondaryStat`).
3. **Score reveal (optional):** subclass `EndGameCinematicController`; override
   `DetermineLocalPlayerWon` + `PlayScoreRevealSequence`.
4. **Stats rows (optional):** add a `ScoreboardStatsProvider` (or wire
   `UniversalStatsProvider` + `StatModuleSO`s).
5. **Wiring:** in the scene, assign `gameData`, the view containers + prefabs,
   `gameController` on the `Scoreboard`, the domain-panel containers on the
   `MultiplayerHUDView`, and the SOAP event refs. Follow [target] (§8) — no new
   `IsMultiplayerMode` reads.

---

## 10. File index

| Role | File |
|---|---|
| Data interface / aggregate | `_Scripts/Data/Enums/IRoundStats.cs` |
| Data hub + SOAP events | `_Scripts/Utility/DataContainers/GameDataSO.cs` |
| In-game HUD base / view | `_Scripts/UI/MiniGameHUD.cs`, `_Scripts/UI/View/MinigameHUDView.cs` |
| In-game MP HUD / view | `_Scripts/UI/MultiplayerHUD.cs`, `_Scripts/UI/View/MultiplayerHUDView.cs` |
| In-game MP domain-sum sync (server→clients) | `_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs` (NetworkVariable sums → `GameDataSO.SetDomainMetricSum`) |
| In-game widgets | `_Scripts/UI/Elements/DomainScorePanel.cs`, `_Scripts/UI/Elements/PlayerScoreEntry.cs` |
| Mode HUDs | `_Scripts/UI/HexRaceHUD.cs`, `MultiplayerJoustHUD.cs`, `MultiplayerCrystalCaptureHUD.cs` |
| End-game scoreboard base | `_Scripts/UI/Scoreboard.cs`, `_Scripts/UI/PlayerScoreCard.cs` |
| Mode scoreboards | `_Scripts/UI/HexRaceScoreboard.cs`, `MultiplayerJoustScoreboard.cs`, `MultiplayerCrystalCaptureScoreboard.cs`, `DuelForCellScoreboard.cs`, `CoOpScoreBoard.cs` |
| End-game cinematic | `_Scripts/Utility/DataContainers/EndGameCinematicController.cs` (+ `HexRace`/`MultiplayerJoust`/`MultiplayerCrystalCapture` subclasses) |
| Stats providers | `_Scripts/UI/ScoreboardStatsProvider.cs`, `UniversalStatsProvider.cs`, `StatModuleSO.cs`, `StatRowUI.cs`; `_Scripts/Controller/Arcade/*StatsProvider.cs` |
| Shared anim config | `_Scripts/UI/HUDAnimationSettingsSO.cs` |

---

## 11. Cross-references

- Per-mode scoring/end-game detail: `_Scripts/Controller/Arcade/HEXRACE.md`,
  `JOUST.md`, `CRYSTAL_CAPTURE.md`.
- Scene/mode inventory + scoring summary: `Docs/SCENES.md`.
- Domain semantics + domain-aggregated scoring: `CLAUDE.md` § "Team Domains" and
  the HexRace section.
- RPC continuation threading (if score sync ever touches UGS/Netcode `Task`s):
  `Docs/THREADING.md`.

See `REFACTOR.md` for the sequenced backlog, `BUGS.md` for open correctness
issues, `TODOS.md` for loose/open items (e.g. the legacy-layout client sync), and
`TESTS.md` for manual verification procedures.
