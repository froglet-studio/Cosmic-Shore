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

## 1. The two surfaces at a glance

| | In-game HUD | Final scoreboard |
|---|---|---|
| Entry class | `MiniGameHUD` → `MultiplayerHUD` | `Scoreboard` |
| Shown when | gameplay turn active | after `OnWinnerCalculated` → `EndGameSequencer` |
| Trigger event | `OnMiniGameTurnStarted` | `OnShowGameEndScreen` |
| Per-player widget | `PlayerScoreEntry` (live) | `PlayerScoreCard` (final) |
| Per-team widget | `DomainScorePanel` | banner + domain-tinted cards |
| Data read | `IRoundStats.OnXxxChanged` (live) | `gameData.Results` (rule-produced, ranked) |
| Mode customization | the mode's `ScoringRuleSO` (`LiveMetric`) | the mode's `ScoringRuleSO` (`BuildResults`) |

---

## 2. Data layer (shared)

### `IRoundStats` (`_Scripts/Data/Enums/IRoundStats.cs`)
Per-player stat snapshot. Implemented by `RoundStats` (a `NetworkBehaviour`),
so every metric property is backed by a server-authoritative `NetworkVariable` and
each change raises an observer event:

- Identity: `Name` (NetworkVariable) and `Domain` — **`Domain` is NOT networked**; it is
  a local mirror of the owning `Player.NetDomain`, kept in sync on every peer by `Player`
  (retired `n_Domain`, BUGS.md B10).
  `Name` is seeded from `Player.Name` at every scene's pair-init
  (`InitializeForMultiplayerMode`, server-side) AND kept live mid-scene:
  `Player.OnNetNameValueChanged` mirrors a replicated rename into
  `RoundStats.Name` on every peer (server write replicates `n_Name`), so a
  menu profile rename reaches scoreboard identity without waiting for the
  next scene load. Full rename pipeline:
  `Docs/PresenceSystem/ARCHITECTURE.md` § "Identity propagation".
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
`SumCrystalsCollectedByDomain(d)` (used by `ElementalComebackSystem`).
End condition, winner, and per-domain sums now live in the mode's `ScoringRuleSO`
(`IsObjectiveReached` / `ResolveWinner`) over `ScoringMetrics.SumByDomain` — the old
`TryGetDomainReaching*` / `SumJoustCollisionsByDomain` helpers were retired.

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
| `ObjectiveIndicator` | `_Scripts/UI/…` | Off-screen objective pointer (auto-created SkimRace/Joust) |
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
   └► IRoundStats.OnAnyStatChanged                (one generic event; no per-mode HUD subclass)
       └► MultiplayerHUD.HandlePlayerStatChanged(stats)
           ├─[domain]► reconcile layout off Player.Domain (rebuild if it changed);
           │           DomainScorePanel sums come from gameData.GetDomainMetricSum
           │           (server-synced) via OnDomainMetricSumsChanged
           └─[legacy]► PlayerScoreEntry.UpdateScore( GetInitialCardValue(stats) )
```
- `GetInitialCardValue(stats)` = `gameData.ScoringRule.LiveMetric(stats)` — the
  per-mode metric (SkimRace/Scurry → Crystals, Joust → Jousts), used by the
  **legacy per-player** card. `SumStatByDomain(domain)` no longer re-sums on the
  client — it returns the **server-synced** `GameDataSO.GetDomainMetricSum(domain)`
  (BUGS.md B9; see the subsection below).
- The **centerline score** (`view.scoreDisplay`) is the *local* player's
  `Score`, driven separately by `MiniGameHUD.UpdateScoreUI` on
  `localRoundStats.OnScoreChanged`.

### Domain attribution + in-game box values
**Domain has ONE networked source: `Player.NetDomain`.** `Player.Domain` mirrors it on
every peer immediately; `RoundStats.Domain` is a **local mirror** `Player` keeps in sync
on every peer (`InitializeForMultiplayerMode` + `OnNetDomainChanged`) — there is no
`RoundStats.n_Domain` (retired, BUGS.md B10). This removed the lagging second
representation that misplaced a client's own icon.

- **Structure / grouping (authoritative).** `MultiplayerHUD` builds the boxes and groups
  player icons entirely off `Player.Domain` via `gameData.Players` (`HasPlayersInDomain`,
  `CreateDomainPanel`). The reconcile is **membership-aware**: `DomainLayoutChanged`
  compares an order-stable layout signature (each player's name → `Player.Domain`, plus
  ally domain + domain count), so it rebuilds when a player MOVES domains, not only when
  the SET of domains changes.
- **Values (server-synced, "Approach B").** `MultiplayerDomainGamesController` (base of
  SkimRace / Joust / Scurry) runs a throttled (0.1s) **server** coroutine that
  writes `ScoringMetrics.SumByDomain(gameData, rule.Metric, ActiveDomains[i])` into three
  `NetworkVariable<int>`; every peer mirrors them into `GameDataSO.SetDomainMetricSum`
  (→ `OnDomainMetricSumsChanged`), and `MultiplayerHUD.SumStatByDomain` returns
  `gameData.GetDomainMetricSum`. **Now redundant** with the domain fixed at the source (the
  HUD could re-sum locally by `Player.Domain`); kept as harmless robustness — `TODOS.md` TD3.

> **Legacy gap:** the above covers only the **domain** layout. The legacy per-player layout
> still reads per-player `RoundStats` directly — `TODOS.md` TD1.

---

## 4. Surface B — Final scoreboard

### Pipeline (event-driven, server-authoritative winner)
```
Game controller (server) — OnTurnEndedCustom / rule.IsObjectiveReached end check
   ├─ rule.AssignScores(gameData, winner, finishTime)   → each RoundStats.Score
   ├─ gameData.SetResults(rule.BuildResults(gameData))   ← ONE ranked List<ScoreResult>;
   │     also sets gameData.WinnerName / WinnerDomain (= Results[0])
   ├─ gameData.CalculateDomainStats(golf)                ← DomainStatsList (legacy fallback)
   ├─ SyncFinalScores_ClientRpc(...)  → each client rebuilds gameData.Results
   └─ gameData.InvokeWinnerCalculated()
        └► EndGameSequencer.HandleWinnerCalculated         (_Scripts/Utility/DataContainers/)
             halt all vessels (IsStationary + input pause) → GameEnd SFX
             → (optional preScoreboardDelay) → gameData.InvokeShowGameEndScreen()
                  └► Scoreboard.ShowScoreboard()             (_Scripts/UI/Scoreboard.cs)
                       ConfigureLobbyButtons()
                       banner domain = gameData.WinnerDomain → "{DOMAIN} VICTORY"
                       PopulateFromResults(gameData.Results) → one PlayerScoreCard per result,
                         in order, ScoreText (primary) + Secondary
                       PopulateDynamicStats() → ScoreboardStatsProvider.GetStats()
```
The mode's `ScoringRuleSO` is the single producer (`BuildResults`); the scoreboard is the
mode-agnostic consumer of `gameData.Results` (R10). The crystal reward is the scoreboard's
`AwardCrystalsIfLocalWinner` only. (`BuildReveal` fed the removed cinematic and is now unconsumed.)

### `Scoreboard` (base)
- Subscribes `OnShowGameEndScreen → ShowScoreboard`, `OnResetForReplay →
  HideScoreboard`.
- `PopulateFromResults(gameData.Results)`: one `PlayerScoreCard` per `ScoreResult`, in
  rule order, each with its `ScoreText` (primary) + `Secondary`, tinted to domain color,
  avatar (`SO_ProfileIconList` humans / `SO_AIProfileList` AI), `+N` crystal reward on the
  winner, and `AwardCrystalsIfLocalWinner` if the local player won. Order/primary/secondary
  all come from the rule — no per-mode formatting here.
- Banner domain = `gameData.WinnerDomain` (falls back to `Results[0].Domain`).
- `ConfigureLobbyButtons`: host/single-player see **Main Menu + Play Again**;
  non-host clients see **Leave Lobby**. **[legacy dependency]** gated on
  `IsMultiplayerMode` (§8).
- The old per-mode `SortPlayers` / `DetermineWinnerDomain` / `FormatPlayerScore` /
  `FormatSecondaryStat` / `SetBannerForDomain` virtuals were removed (R10) — the rule
  produces all of it.

### `PlayerScoreCard` (`_Scripts/UI/PlayerScoreCard.cs`)
End-game row: avatar, name, formatted score, domain-tinted background,
optional secondary stat line, optional `+N` crystal reward. Entrance/punch/roll
animations from `HUDAnimationSettingsSO`. (Distinct from the in-game
`PlayerScoreEntry`.)

### `EndGameSequencer` (`_Scripts/Utility/DataContainers/`)
Slim bridge between winner-calculation and the scoreboard — there is **no end-game
cinematic** (removed). On `OnWinnerCalculated` it halts every vessel
(`VesselStatus.IsStationary = true` + input pause, so nothing flies behind the
scoreboard), plays the GameEnd SFX, then raises `OnShowGameEndScreen` — the signal the
`Scoreboard` and `LifeForm` (ecology cleanup) already listen for. It also re-homes the
two end-of-game progression toasts (quest complete / intensity unlocked) off
`GameModeProgressionService` events. The per-mode reveal (`ScoringRuleSO.BuildReveal`)
is no longer consumed; the scoreboard cards read `gameData.Results` (`BuildResults`).

### Dynamic stats providers
`ScoreboardStatsProvider` (abstract) → `GetStats() : List<StatData>`, rendered as
`StatRowUI` rows under the cards. Implementations: `SkimRaceStatsProvider`,
`JoustStatsProvider`, `ScurryStatsProvider`,
`WildlifeBlitzStatsProvider`, and the generic `UniversalStatsProvider`
(`StatModuleSO`-bound). All in `_Scripts/Controller/Arcade/` or `_Scripts/UI/`.

---

## 5. Per-mode reference

The rule modes each have **one `ScoringRuleSO` asset**; the shared HUD and scoreboard
read its output. The last two rows are non-rule modes that keep their own scoreboard.
(The *Reveal* column records `BuildReveal`'s output, which is **no longer shown** — the
end-game cinematic was removed; `EndGameSequencer` now just raises the scoreboard.)

| Mode (id) | `ScoringRuleSO` | Metric | Sort | Winner / loser score | Reveal (`BuildReveal`) |
|---|---|---|---|---|---|
| **SkimRace** (33) | `SkimRaceScoringRuleSO` | Crystals | golf ↑, tiebreak `CrystalsCollected`↓ | finish time `MM:SS:CS` / `EncodeSkimRaceLoserScore` (10000 + crystals-left) → "{N} Crystals Left" | VICTORY/RACE TIME • DEFEAT/CRYSTALS LEFT |
| **Joust** (34) | `JoustScoringRuleSO` | Jousts | golf ↑, tiebreak `JoustCollisions`↓ | finish time `MM:SS:CS` / `JoustLoserScore` (99999) → "{N} Jousts Left" (domain deficit) | VICTORY/WON BY N JOUSTS • DEFEAT/LOST BY N JOUSTS |
| **Crystal Capture** (35) | `ScurryScoringRuleSO` | Crystals | golf ↑, tiebreak `CrystalsCollected`↓ | finish time `MM:SS:CS` / `EncodeSkimRaceLoserScore` (10000 + crystals-left) → "{N} Crystals Left"; secondary "{N} Crystals" | VICTORY/CAPTURE TIME • DEFEAT/CRYSTALS LEFT |
| **Rampage** (2) | `RampageScoringRuleSO` | PrismsDestroyed | golf ↑, tiebreak `HostilePrismsDestroyed`↓ | finish time `MM:SS:CS` / `EncodeSkimRaceLoserScore` (10000 + prisms-left) → "{N} Prisms Left"; secondary "{N} Prisms" | VICTORY/RAMPAGE TIME • DEFEAT/PRISMS LEFT |
| **Cellular Duel** (29) | — (no rule) | `Score` | points ↓ | `"{N}"` | `EndGameSequencer`; `DuelForCellScoreboard` |
| **Wildlife Blitz** co-op (32) | — (no rule) | `Score` | base | base | `CoOpScoreBoard` + `EndGameSequencer` |

> **Loser sentinels (centralized).** Golf modes encode a DNF loser score — SkimRace /
> Crystal Capture / Rampage `10000 + team-metric-remaining` (all three share
> `EncodeSkimRaceLoserScore`; the "SkimRace" naming is legacy), Joust `99999` — via the one
> `GolfScoreSentinels` helper (`Encode…` / `IsFinishTime`), the single documented source
> after `REFACTOR.md` R4. The rule's `AssignScores` writes it; `BuildResults` decodes it
> into `ScoreText`.

---

## 6. Shared look/feel

- **`HUDAnimationSettingsSO`** (`_Scripts/UI/HUDAnimationSettingsSO.cs`, asset
  referenced by every card/panel) is the single source for entrance, score
  punch, counter roll, color flash, countdown, HUD fade, and scoreboard
  entrance/banner timings + `useUnscaledTime`. Per Config Separation, tuning
  lives here, not on per-widget SerializeFields.
- **Domain → color** resolves from the ONE source: `ThemeManagerData.ColorSet`
  (`SO_ColorSet`). Flat scoring UI uses `GetDomainUIColor` (= `TrailHighlightColor`);
  the Maelstrom cards / Connecting-panel rank use the named accent role
  `GetDomainUIAccentColor` (= `DomainColorSet.UIAccentColor`, a deliberately
  brighter translucent tint that falls back to `GetDomainUIColor` when
  unauthored). The former parallel `DomainColorPaletteSO` is deleted
  (`REFACTOR.md` R5).

---

## 7. Patterns to follow

- **SOAP + observer, no polling.** React to `IRoundStats.OnXxxChanged` and
  `GameDataSO` SOAP events; never poll score state per-frame. Fail loud on
  missing SOAP refs (no if-null guards on event fields).
- **Server-authoritative, domain-aggregated.** The controller decides the winner
  on the server via the mode's `ScoringRuleSO` (`IsObjectiveReached` / `ResolveWinner`,
  over `ScoringMetrics.SumByDomain`), writes `WinnerName`/`WinnerDomain`, and broadcasts.
  Views never compute the winner.
- **One `ScoringRuleSO` per mode, not per-mode UI subclasses.** A mode's end
  condition, metric, winner, per-player score, ranked results, and reveal all live in
  its `ScoringRuleSO`. The HUD and scoreboard are mode-agnostic and consume
  the rule's output (`gameData.Results`, `BuildReveal`, `LiveMetric`) — do **not** subclass
  them per mode (the old `SkimRaceHUD` / `*Scoreboard` / `*EndGameController` subclasses were
  removed, R10).
- **Golf vs points** is a per-mode flag on the rule (`ScoringRuleSO.golfRules`), not a
  base special case.

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
| `Controller/Multiplayer/MultiplayerSetup.cs:86` | session creation path |
| `Controller/Party/HostConnectionService.cs:1714` | presence activity string |
| `System/SceneLoader.cs:139` | log line only |

**Target:** route all the above through the always-networked host model (solo =
host + AI), driving lobby/scoreboard behavior off concrete signals
(connected client count, `WinnerDomain`, domain membership) rather than the
`IsMultiplayerMode` boolean, then remove the flag. **This is a discuss-first
item** (`REFACTOR.md` R1) — agree the per-site replacement before any code.

---

## 9. How to add a new game mode's scoreboard + HUD

The per-mode work is **one `ScoringRuleSO`** — the shared HUD / scoreboard are
mode-agnostic and read its output. There are **no** per-mode HUD / scoreboard
subclasses any more, and **no end-game cinematic** (`EndGameSequencer` raises the scoreboard).

1. **Write the rule:** subclass `ScoringRuleSO` (`_Scripts/Controller/Arcade/Scoring/`).
   Set `metric` + `golfRules`; implement `IsObjectiveReached` (end condition + winning
   domain), `AssignScores` (per-player `Score`), `BuildResults` (the ranked
   `List<ScoreResult>` — order + `ScoreText` + optional `Secondary`), and `BuildReveal`
   (abstract, so still required to compile, but **currently unconsumed** — the end-game
   cinematic that displayed it was removed). Add the `[CreateAssetMenu]` and create the asset.
2. **Wire it:** the mode's controller (a `MultiplayerDomainGamesController`) publishes the
   asset to `gameData.ScoringRule` and calls `gameData.SetResults(rule.BuildResults(gameData))`
   on turn end; the turn monitor calls `rule.IsObjectiveReached`.
3. **No UI subclasses:** the shared `MultiplayerHUD` (domain boxes via `Player.Domain` +
   server-synced sums), `Scoreboard` (`PopulateFromResults`), and the shared
   `EndGameSequencer` (raises `OnShowGameEndScreen`) handle the rest.
4. **Stats rows (optional):** add a `ScoreboardStatsProvider` (or wire
   `UniversalStatsProvider` + `StatModuleSO`s). *(R12: these per-mode providers are slated
   to fold behind the rule; until then this is how stat rows are added.)*
5. **Scene wiring:** assign `gameData`, the view containers + prefabs, `gameController` on
   the `Scoreboard`, and the domain-panel containers (`allyDomainContainer` /
   `opposingDomainsContainer` / `domainPanelPrefab`) on the `MultiplayerHUDView`. Follow
   [target] (§8) — no new `IsMultiplayerMode` reads.

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
| **Per-mode scoring rule** (the only per-mode code) | `_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs` (+ `SkimRace`/`Joust`/`Scurry` `ScoringRuleSO`), `ScoringMetrics.cs` |
| Ranked-results types | `_Scripts/Data/Structs/ScoreResult.cs`, `_Scripts/Controller/Arcade/ScoreResultBuilder.cs`, `_Scripts/Controller/Arcade/Scoring/ScoreReveal.cs` |
| End-game scoreboard | `_Scripts/UI/Scoreboard.cs` (reads `gameData.Results`), `_Scripts/UI/PlayerScoreCard.cs` |
| Non-rule scoreboards | `_Scripts/UI/DuelForCellScoreboard.cs`, `CoOpScoreBoard.cs` (rule modes use the base `Scoreboard`) |
| End-game sequencer | `_Scripts/Utility/DataContainers/EndGameSequencer.cs` (halts vessels, GameEnd SFX, raises `OnShowGameEndScreen`; shared by all modes) |
| Stats providers | `_Scripts/UI/ScoreboardStatsProvider.cs`, `UniversalStatsProvider.cs`, `StatModuleSO.cs`, `StatRowUI.cs`; `_Scripts/Controller/Arcade/*StatsProvider.cs` |
| Shared anim config | `_Scripts/UI/HUDAnimationSettingsSO.cs` |

---

## 11. Cross-references

- Per-mode scoring/end-game detail: `_Scripts/Controller/Arcade/SKIMRACE.md`,
  `JOUST.md`, `SCURRY.md`.
- Scene/mode inventory + scoring summary: `Docs/SCENES.md`.
- Domain semantics + domain-aggregated scoring: `CLAUDE.md` § "Team Domains" and
  the SkimRace section.
- RPC continuation threading (if score sync ever touches UGS/Netcode `Task`s):
  `Docs/THREADING.md`.

See `REFACTOR.md` for the sequenced backlog, `BUGS.md` for open correctness
issues, `TODOS.md` for loose/open items (e.g. the legacy-layout client sync), and
`TESTS.md` for manual verification procedures.
