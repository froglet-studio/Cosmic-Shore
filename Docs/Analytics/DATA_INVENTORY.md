# UGS Data Inventory — Cloud Save, Analytics, Leaderboards

> Companion to `INSTRUMENTATION_DATA.html` (the instrumentation event reference, rebuilt against this codebase).
> Scope: **VSlice and VDemo**. VLater items are tracked but not planned for implementation yet.
> Sources: `UGSKeys.cs`, `UGSDataService.cs`, `CloudDataRepository.cs`, `UGSCloudSaveProvider.cs`,
> `UGSStatsManager.cs`, `_Scripts/System/Instrumentation/`, `VesselTelemetry.cs` + subclasses.

---

## 1. Cloud Save inventory (what we persist today, as JSON)

All keys live in `Assets/_Scripts/System/UGSKeys.cs` and are owned by one repository each under
`Assets/_Scripts/System/CloudData/` (`UGSDataService` facade, debounced `CloudDataRepository<T>` base,
`UGSCloudSaveProvider` → `CloudSaveService.Instance.Data.Player`). Serialization: the raw object is
handed to the UGS SDK (Newtonsoft under the hood); `JsonUtility.FromJson` exists only as a legacy
string fallback on load.

| # | Key | Model | Status | Debounce | Writes |
|---|---|---|---|---|---|
| 1 | `player_profile` | `PlayerProfileData` | **ACTIVE** | 1.5s | Per mutation (crystals, rewards, avatar/name) |
| 2 | `PLAYER_STATS_PROFILE` | `PlayerStatsProfile` | **ACTIVE** | 2.0s | Game end (only when a best is beaten) |
| 3 | `VESSEL_STATS` | `VesselStatsCloudData` | **ACTIVE** | 2.0s | Every game end |
| 4 | `GAME_MODE_PROGRESSION` | `GameModeProgressionData` | **ACTIVE** | 1.5s + immediate | Quest claims, intensity unlocks, stat reports |
| 5 | `HANGAR_DATA` | `HangarCloudData` | **ACTIVE** | 1.5s | Vessel lock/unlock |
| 6 | `PLAYER_SETTINGS` | `PlayerSettingsCloudData` | **ACTIVE** | 1.5s | Per settings change |
| 7 | `DAILY_CHALLENGE` | `DailyChallengeCloudData` | **WIRED, DEFERRED** — repo loads/flushes; system still on PlayerPrefs+PlayFab. Migration deferred (PlayFab economy coupling). | 1.5s | none yet |
| 8 | `TRAINING_PROGRESS` | `TrainingProgressCloudData` | **WIRED** — repo loads/flushes; system still writes local file. Migration held for Unity review. | 1.5s | none yet |
| 9 | `CAPTAIN_PROGRESS` | `CaptainProgressCloudData` | **DISABLED** — `CaptainManager` stubbed since PlayFab retirement (repo not wired) | 1.5s | none |
| 10 | `EPISODE_PROGRESS` | `EpisodeProgressCloudData` | **SCAFFOLD** — `ReportMissionCompleted` has no callers | 1.5s | none |
| 11 | `SQUAD_DATA` | `SquadCloudData` | **WIRED** (new) — repo loads/flushes; system still writes `squad.data`. Migration held for Unity review. | 1.5s | none yet |
| 12 | `LOADOUT_DATA` | `LoadoutCloudData` | **WIRED** (new) — repo loads/flushes; system still writes local files. Migration held for Unity review. | 1.5s | none yet |

### 1.1 `player_profile` — `PlayerProfileData` (`_Scripts/UI/Views/PlayerProfileData.cs`)

```json
{
  "userId": "UGS-auth PlayerId",
  "displayName": "Pilot####",
  "avatarId": 3,
  "crystalBalance": 250,
  "xp": 1275,
  "unlockedRewardIds": ["reward_id", "..."],
  "firstSeenUtc": 1718900000000
}
```

`firstSeenUtc` (Unix epoch ms, UTC) is stamped once when the account's profile is first created
(Phase 2) — used for install-relative cohorting / retention analysis.

Writer: `PlayerDataService` (sole writer; `AddCrystals`, `TrySpendCrystals`, `UnlockReward`,
profile edits → `MarkDirty`).
Read once per session, merged with cloud (`MergeCloudProfile` unions reward IDs).
**Note for the data team: "omnicrystals" in the instrumentation doc == `crystalBalance` here.**

### 1.2 `PLAYER_STATS_PROFILE` — `PlayerStatsProfile` (`_Scripts/UI/PlayerStatsProfile.cs`)

```json
{
  "LastLoginTick": 638537251200000000,
  "BlitzStats":          { "HighScores": { "WildlifeBlitz_2": 1840 }, "LifetimeCrystalsCollected": 0 },
  "MultiHexStats":       { "BestMultiplayerRaceTimes": { "HexRace_2": 93.41 } },
  "JoustStats":          { "BestRaceTimes": { "MultiplayerJoust_1": 75.2 } },
  "CrystalCaptureStats": { "HighScores": { "MultiplayerCrystalCapture_3": 42 } }
}
```

Dictionary keys are `"{GameMode}_{Intensity}"`. Writer: `UGSStatsManager.Report*Stats()` at game end,
called by the per-mode score trackers/reporters. `LifetimeCrystalsCollected` is deprecated
(kept for backwards compatibility).

### 1.3 `VESSEL_STATS` — `VesselStatsCloudData` (`_Scripts/Controller/Vessel/VesselStatsCloudData.cs`)

```json
{
  "Vessels": {
    "Sparrow": {
      "BestDriftTime": 12.7,
      "BestBoostTime": 8.3,
      "TotalPrismsDamaged": 4210,
      "GamesPlayed": 57,
      "Counters": { "PrismBlocksShot": 1894, "SkyburstMissilesShot": 230, "DangerBlocksSpawned": 88 }
    },
    "Squirrel": {
      "BestDriftTime": 21.4,
      "BestBoostTime": 6.1,
      "TotalPrismsDamaged": 1022,
      "GamesPlayed": 31,
      "Counters": { "JoustsWon": 12, "PrismsStolen": 340, "BestCleanStreak": 17 }
    }
  }
}
```

Fed by `VesselTelemetry` (base: drift/boost/prisms-damaged) + per-vessel subclasses
(`SparrowVesselTelemetry`, `SquirrelVesselTelemetry`); flushed by
`UGSStatsManager.ReportVesselTelemetry()` at every game end. The `Counters` dictionary is the
extension point for new per-vessel stats — no schema change needed.

### 1.4 `GAME_MODE_PROGRESSION` — `GameModeProgressionData` (`_Scripts/System/Progression/`)

```json
{
  "UnlockedModes": ["WildlifeBlitz", "HexRace"],
  "CompletedQuests": ["HexRace"],
  "BestStats": { "WildlifeBlitz": 1840.0 },
  "MaxUnlockedIntensity": { "WildlifeBlitz": 3 },
  "IntensityPlayCounts": { "WildlifeBlitz_2": 11 }
}
```

Writer: `GameModeProgressionService` — quest claims and intensity unlocks save **immediately**
(`SaveImmediateAsync`), stat reports save debounced.

### 1.5 `HANGAR_DATA` — `HangarCloudData` (`_Scripts/System/CloudData/Models/`)

```json
{
  "UnlockedVessels": ["Squirrel", "Sparrow"],
  "VesselPreferences": { "Squirrel": { "LastUsedTicks": 638537251200000000, "Favorited": true } },
  "SelectedVessel": "Squirrel"
}
```

Writer: `VesselUnlockSystem` (unlock/lock/reset). Synced back onto `SO_Vessel` assets on load.

### 1.6 `PLAYER_SETTINGS` — `PlayerSettingsCloudData`

```json
{
  "MusicEnabled": true, "SFXEnabled": true, "HapticsEnabled": true,
  "InvertYEnabled": false, "InvertThrottleEnabled": false, "JoystickVisualsEnabled": true,
  "MusicLevel": 0.8, "SFXLevel": 1.0, "HapticsLevel": 0.6
}
```

Writer: `GameSetting.SyncToCloud()` on every individual setting change (full object). Cloud wins
over PlayerPrefs on load; PlayerPrefs kept as offline mirror.

### 1.7 `DAILY_CHALLENGE` — `DailyChallengeCloudData` (**migration pending**)

```json
{
  "ChallengeDate": "2026-06-11",
  "LastTicketIssuedDate": "2026-06-11",
  "TicketBalance": 3,
  "GameMode": "WildlifeBlitz",
  "Intensity": 2,
  "HighScore": 1430,
  "RewardTiers": [ { "Satisfied": true, "Claimed": false }, { "Satisfied": false, "Claimed": false }, { "Satisfied": false, "Claimed": false } ]
}
```

`DailyChallengeSystem` still reads/writes 10 PlayerPrefs keys (`DailyChallengeTicketBalance`,
`RewardTier*Claimed/Satisfied`, …). The repository exists and is loaded each session but never written.

### 1.8 `TRAINING_PROGRESS` — `TrainingProgressCloudData` (**migration pending**)

```json
{
  "Games": {
    "WildlifeBlitz": {
      "CurrentIntensity": 2,
      "Tiers": [ { "Satisfied": true, "Claimed": true }, { "Satisfied": true, "Claimed": false }, { "Satisfied": false, "Claimed": false }, { "Satisfied": false, "Claimed": false } ]
    }
  }
}
```

`TrainingGameProgressSystem` still writes `Application.persistentDataPath/training_progress.data`
(Newtonsoft, immediate full-dictionary rewrite, no debounce).

### 1.9 `CAPTAIN_PROGRESS` — `CaptainProgressCloudData` (**disabled**)

```json
{
  "Captains": {
    "CaptainName": { "XP": 150, "Level": 2, "Unlocked": true, "Encountered": true, "UpgradeCount": 1 }
  }
}
```

`CaptainManager` is stubbed (`[PLAYFAB DISABLED]`). Re-enabling captain/pilot progression on UGS is
a prerequisite for the VDemo pilot events (`pilot_recruited`, `vessel_upgraded`).

### 1.10 `EPISODE_PROGRESS` — `EpisodeProgressCloudData` (**scaffold**)

```json
{
  "UnlockedEpisodes": ["episode_01"],
  "CompletedEpisodes": [],
  "EpisodeProgress": { "episode_01": { "MissionsCompleted": 2, "TotalMissions": 5, "BestScore": 900, "StarsEarned": 4 } }
}
```

Model + repository exist; `ReportMissionCompleted()` has no callers yet. This is where the episode
revenue funnel (VSlice in the instrumentation doc) will persist.

### 1.11 Local-only persistence (not yet in cloud)

| Store | Data | Owner |
|---|---|---|
| `squad.data` (file) | `Squad` — leader + 2 rogues (class + element each) | `SquadSystem` |
| `game_favorites.data` (file) | `List<GameModes>` favorited minigames | `FavoriteSystem` |
| loadout file | `Loadout` — intensity, player count, vessel, mode, isMultiplayer | `LoadoutSystem` |
| PlayerPrefs | daily challenge state (→ §1.7), settings mirror, `Client_guid`, volumes | various |

Squad, favorites, and loadout are all small, analytics-relevant (squad config is an activation gate;
favorites is a referral signal), and should get cloud keys + events when touched for instrumentation.

---

## 2. Current analytics footprint (what actually fires today)

> **Decision (2026-06-11): UGS-only.** The Firebase analytics path is being retired — port
> `user_ui_action` and `ad_impression` to UGS custom events, delete `FirebaseAnalyticsController`
> wiring and the `cs_*` Firebase collector implementations (keep their taxonomy as the UGS event
> spec). Removing the Firebase SDK from the project entirely is a separate ticket (check non-analytics
> dependents first).
>
> **Implemented (2026-06-11, P0+P1):** `AnalyticsServiceFacade` (`_Scripts/System/Instrumentation/`)
> is now the single writer for all UGS Analytics events — it owns StartDataCollection (sign-in +
> consent + network gated), records `game_started`, `game_completed`, `session_ended`, `ui_action`,
> `ad_impression`, `play_again_pressed`, and flushes only on pause/quit. The whole Firebase analytics
> path was deleted. **Audit correction:** the previous "live" Firebase events never actually fired —
> `UnityAnalytics.cs`, `CSAnalyticsManager.prefab`, and `AdManager.prefab` were all orphaned (placed
> in no scene), so UGS data collection had never started. Remaining P1 work: the consent dialog +
> settings opt-out (both call `AnalyticsServiceFacade.SetConsent`), and declaring the six events in
> the UGS dashboard Event Manager.

### 2.1 Live

| Destination | Event / data | Trigger |
|---|---|---|
| UGS Analytics | `play_again_pressed` (no params) | Scoreboard "Play Again" → `UGSStatsManager.TrackPlayAgain()` (also calls `Flush()` per event) |
| UGS Analytics | built-in auto events (session, device, newPlayer) | `UnityAnalytics.cs` starts collection on sign-in; network-aware pause/resume |
| UGS Leaderboards | HexRace time, WildlifeBlitz score, Joust time, CrystalCapture crystals — per intensity, IDs from `LeaderboardConfigSO` | per-mode trackers at game end → `UGSStatsManager.SubmitScoreInternal()` |
| UGS Cloud Save | everything in §1 (active keys) | see §1 |
| Firebase | `app_open` | SDK init (`FirebaseAnalyticsController`) |
| Firebase | `ad_impression` | `AdsSystem.AdLoaded` |
| Firebase | `user_ui_action` (`content`=label, `content_type`=action type, `value`) | `UserActionSystem.OnUserActionCompleted` (PlayGame, ViewHangarMenu, ViewArcadeMenu, …) |

### 2.2 Dormant (built, not firing)

- **All 16 `cs_*` Firebase collector events** (`CSAnalyticsManager` + 7 collectors): arcade/training/
  mission/daily-challenge start+complete, captain purchase/upgrade, store purchases, watch-ad,
  daily reward, app open/close. Dead because `CSUtilitiesFirebase.InitSDK()` is commented out
  ("infinite hang on Android"). **These define our event taxonomy already — port them to UGS Analytics
  instead of resurrecting the Firebase path.**
- `level_start` / `level_end` in `FirebaseAnalyticsController` — subscriptions commented out.
- `screen_view` — method exists, never called.
- `UserJourneySystem` / `QuestSystem` — full funnel state machine, emits zero analytics.
- PlayFab PlayStream `AnalyticsController` — disabled, delete when convenient.

### 2.3 Consent

`IS_CONSENTED = true` is hardcoded in `UnityAnalytics.cs`; Firebase collection enabled
unconditionally. **No consent dialog exists.** Required before any store release (GDPR/COPPA);
treat as a VSlice blocker for shipping analytics.

---

## 3. Features to add (prioritized, VSlice → VDemo)

Status legend used here and in the HTML: **LIVE** (firing to UGS today) · **DORMANT** (code exists,
disabled or Firebase-only) · **BUILD** (system exists, add the event hook) · **NO SYSTEM** (feature
itself doesn't exist).

### 3.1 VSlice — instrument now (hook points already exist)

> **Shipped 2026-06-15 (Phase 2).** All hooks below are now LIVE through `AnalyticsServiceFacade`
> typed methods + SO-asset SOAP subscriptions: economy (`crystals_earned/spent/spend_blocked`,
> `vessel_unlocked`, `crystal_balance_snapshot`), activation (`game_first_launched`, `menu_ready`,
> `freestyle_entered`, `mode_unlocked`, `intensity_unlocked`), `setting_changed`, social
> (`friend_request_sent/received`, `friend_added`, `party_invite_sent/received`, `party_joined`,
> `minigame_favorited`, `share_triggered`), retention (`quest_completed`, `repeated_game_fail`),
> plus `player_won` on `game_completed` and `firstSeenUtc` on the profile. Remaining VSlice work:
> the consent dialog/opt-out, and declaring all events in the UGS dashboard Event Manager.

0. **Retire the Firebase analytics path** (UGS-only decision) — port `ui_action` + `ad_impression`
   to UGS, delete dead collectors and `FirebaseAnalyticsController` wiring.
1. **UGS Analytics event pipeline** — a single `CSAnalyticsManager`-style facade over
   `AnalyticsService.Instance.RecordEvent`, SOAP-event-driven, replacing the dead Firebase
   collectors. All events below route through it.
2. **Game lifecycle**: `game_started` / `game_completed` (mode, intensity, vessel, player count,
   score, duration, win/loss) — hook `GameDataSO.OnMiniGameTurnStarted` / `OnMiniGameEnd`
   (the exact subscriptions that are commented out in `FirebaseAnalyticsController`).
3. **Activation funnel**: `game_first_launched` (PlayerPrefs `IsInitialPlay` already exists),
   `freestyle_entered` (lava-lamp first tap — `MenuCrystalClickHandler.ToggleTransition`),
   `menu_ready_first` (`MainMenuController` → Ready), `vessel_inspected`, `mode_unlocked` /
   `intensity_unlocked` (`GameModeProgressionService` — unlock data already cloud-saved),
   `time_to_first_flight` (derived client timer).
4. **Economy**: `crystals_earned` / `crystals_spent` / `crystal_spend_blocked` — one-line hooks in
   `PlayerDataService.AddCrystals` / `TrySpendCrystals` (false branch = starvation signal),
   `vessel_unlocked` (`VesselUnlockSystem`), `crystal_balance_snapshot` at session end (balance is
   already in `player_profile`).
5. **Session quality**: `session_ended` with `last_screen` (`ScreenSwitcher` current screen +
   `ApplicationStateMachine.PreviousState`) and `last_event_type` — the churn-forensics ask.
6. **Settings**: `setting_changed` (which, new value, seconds-since-install) — hook the nine
   existing `GameSetting.OnChange*` events; covers invert-Y/throttle/joystick retention segmentation.
7. **Social**: `friend_request_sent/accepted/declined` (`FriendsServiceFacade` methods),
   `party_invite_sent/accepted` (`HostConnectionService.SendInviteAsync`, `OnPartyJoinCompleted`
   SOAP events), `minigame_favorited` (`FavoriteSystem.ToggleFavorite`), `share_triggered`
   (`SnsShare`).
8. **Daily challenge**: finish the Cloud Save migration (§1.7), then `daily_challenge_started/
   completed/skipped` — the dormant collectors already define params (game type, intensity, vessel,
   score, reward).
9. **Consent dialog** + analytics opt-out setting (blocker, see §2.3).

### 3.2 VDemo — next

10. **Training progress cloud migration** (§1.8) + `training_started/completed` (dormant collectors
    define the shape).
11. **Captains/pilots on UGS**: re-enable `CaptainProgress` (§1.9) → unlocks `pilot_recruited`,
    `vessel_upgraded`, `pilot_backstory_viewed` events.
12. **Episode funnel**: wire `EpisodeProgressCloudData.ReportMissionCompleted`, add
    `episode_browsed/unlocked/completed`. Token-store purchase events are blocked on real IAP
    (`IAPManager` is a stub) — flag to production.
13. **Flight-style telemetry**: extend `VesselTelemetry` with input intensity sampling,
    near-miss/proximity (via `BlockDensityGrid`), ability-usage counters (via
    `ActionExecutorRegistry`); add a derived per-session `style_tag` written to `player_profile`
    (or a new `PLAYER_STYLE` key) and as a UGS Analytics user property. Note: "altitude bands /
    map coverage" from the data-team doc translate to **cell occupancy / cell coverage** in our IA.
14. **Squad cloud migration** + `squad_configured` (activation gate in the data team's funnel).
15. **Quest/UserJourney analytics**: `quest_completed` / `journey_stage_reached` from
    `UserJourneySystem` (currently silent).

### 3.3 Explicitly out of scope until VLater

Factions (stub only — `PortFactionView` throws), friend-code referral chains (no code system; UGS
player names with `#suffix` are the nearest primitive), episode-token progressive pricing,
bunk space, prism→omni conversion, store-page/UTM attribution (storefront-side tooling, not client).

---

## 4. Push/pull optimization review

> **Phase 3 status (2026-06-15):** infrastructure fixes #2, #4, #6, #9 shipped; deprecated field
> (#7's cousin) dropped; all 12 repos wired (incl. new Squad/Loadout). Issues #5 (per-event Flush)
> shipped in Phase 1. The local→cloud system migrations (#1) are **held for Unity review**; Daily
> Challenge is **deferred** (PlayFab economy coupling).

### What's already good

- Single `UGSDataService` facade; one repo per key; dependency-inverted provider (`ICloudSaveProvider`).
- Debounced writes (1.5–2s) coalesce bursts of `MarkDirty()` into one `SaveAsync`.
- Parallel load of all repos once per session at sign-in; offline-safe defaults when cloud is down.
- Leaderboard submits are per-game-end only; stats writes only when a best is beaten.

### Issues found (ordered by impact)

1. **Two systems bypass the cloud entirely** — Daily Challenge (PlayerPrefs) and Training Progress
   (local file, unbounded immediate writes). Models + repos already exist; finish the migration.
   A device wipe currently loses both.
2. **[SHIPPED]** ~~No retry/backoff anywhere.~~ `UGSCloudSaveProvider.SaveAsync` now retries with
   2s/4s/8s backoff and returns `bool`; the repo keeps `_dirty` on failure and the debounce loop
   retries. Offline returns false silently; genuine online failures toast + emit `cloud_save_failed`
   once per episode (HashSet guard), on the main thread.
3. **Full-object rewrites per save.** Every `MarkDirty` re-uploads the whole model. Mostly fine
   (objects are small), but `VESSEL_STATS` and `GAME_MODE_PROGRESSION` grow with content. UGS Cloud
   Save has no partial update, so the fix is **key splitting** (e.g. per-vessel keys) only if/when
   payloads grow past ~10KB — not worth it today, worth watching.
4. **[SHIPPED]** ~~`FlushAllAsync` saves clean repos.~~ Now skips repos where `!IsDirty`.
5. **[SHIPPED in P1]** ~~`AnalyticsService.Flush()` per event.~~ Routed through `AnalyticsServiceFacade`;
   flush only on app pause/quit.
6. **[SHIPPED]** ~~Legacy load fallback silently loses dictionaries.~~ `UGSCloudSaveProvider.LoadAsync`
   fallback now uses `JsonConvert.DeserializeObject<T>` (Newtonsoft), not `JsonUtility`.
7. **Fire-and-forget `MarkDirty` with null-conditional chains** (`UGSStatsManager.SaveProfile/
   SaveVesselStats`: `_ugsDataService?.StatsRepo?.MarkDirty()`) — a null silently no-ops a save.
   Fail loud per project policy.
8. **Settings full-sync on every toggle** (`GameSetting.SyncToCloud` copies all 9 fields per change).
   Harmless at this size; the debounce absorbs it. No action.
9. **[SHIPPED]** ~~No save-latency/failure observability.~~ `cloud_save_failed` (key) now emits once
   per online failure episode via `UGSCloudSaveProvider.OnSaveFailed` → `AnalyticsServiceFacade`.

### Pull-side

Loads happen exactly once per session (parallel, post-sign-in) — correct. No polling found. The only
gap: services that miss `OnInitialized` because of scene timing re-read repo caches lazily, which is
fine.

---

## 5. Later: getting data out of UGS (parked, per discussion)

When we get to the external dashboard: UGS Analytics has a **Data Explorer + REST export** and
BigQuery-style raw event export on paid tiers; Cloud Save is queryable per-player via the
Admin/Access APIs (service account + project key) — a small server (or Cloud Code endpoint) can
aggregate `PLAYER_STATS_PROFILE` / `VESSEL_STATS` for a web leaderboard/ops dashboard. Decide then:
UGS Dashboards (zero work, limited), scheduled export → our own DB (flexible), or Cloud Code
read-API (per-player, real-time). Not started — tracked here so it isn't lost.
