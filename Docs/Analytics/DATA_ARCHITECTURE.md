# Data Architecture — Cloud Save schemas, classification, and the analytics contract

> **Status:** schemas approved; commits 1-4 shipped. Companion to `DATA_INVENTORY.md` (what exists today)
> and `INSTRUMENTATION_DATA.html` (the instrumentation team's event reference).
>
> **Decisions taken (2026-07-27):** PostHog is fed **directly from the client** through a second
> analytics sink · cloud saves are **broken, not migrated** (pre-launch) · PostHog receives **all
> events plus person properties** · **display name is allowed** to leave the client.
>
> The PostHog project is **already set up** — the Unity → PostHog link is the only missing piece
> (§7.3, commit 4). A person is identified by UGS player ID and **searchable by display name**
> (§7.3.1). §8 inventories the legal exposure this creates.
>
> Sources read: `UGSKeys.cs`, `UGSDataService.cs`, `CloudDataRepository.cs`, `UGSCloudSaveProvider.cs`,
> `AnalyticsServiceFacade.cs`, `UGSStatsManager.cs`, `PlayerDataService.cs`, `VesselUnlockSystem.cs`,
> `HangarCloudData.cs`, `VesselStatsCloudData.cs`, `PlayerStatsProfile.cs` (+4 per-mode profiles),
> `GameModeProgressionData.cs`, `GameDataSO.cs`, `Player.cs`, `PauseSystem.cs`,
> `HostConnectionDataSO.cs`, `PartySessionService.cs`, `MiniGameControllerBase.cs`.

---

## 0.1 Companion documents

| Doc | What it is |
|---|---|
| `DATA_INVENTORY.md` | What we persisted before this rework |
| `EVENT_SCHEMA.json` | Machine-readable event contract — the source for UGS Event Manager + PostHog config |
| `POSTHOG_SETUP.md` | The out-of-repo checklist: create the project, paste the key, verify, insights, deletion runbook, free-tier guardrails |
| `viability-report.md` | Why PostHog over the alternatives; the three attribution-bridging options |
| `event-taxonomy.md` | Target event schema from the analysis pass |
| `utm-conventions.md` | Campaign/UTM naming conventions |
| `implementation-plan.md` | The original phased plan behind the sink layer |
| `../../Tools/Analytics/README.md` | Bulk Cloud Save export + PostHog backfill scripts |

The bottom five were written on the attribution/viability branch (PR #592) and salvaged here; each
carries a provenance banner. **This document is the authority on implemented shape.**

---

## 0. Why this document exists

Two asks landed together and they turn out to be the same problem:

1. *"Make the data sets consistent, following the same architecture and format, classified the way
   AAA studios classify data."*
2. The instrumentation email: per-game **flight time**, **completion timestamp**, and a
   **`lobby_id` / `player_ids` / `invite_triggered`** triple on `game_started` so we can measure
   organic rematch rate.

They are the same problem because **every field the email asks for is a field we cannot currently
produce**, and the reason we cannot produce it is the same reason the schemas drifted: nobody ever
wrote down what a record is, who owns it, or what a timestamp means. §1 fixes the discipline, §2–4
fix the schemas, §5–7 deliver the email.

---

## 1. The classification model

Every persisted or emitted field is classified on four axes. The axes are not decoration — each one
decides something concrete (where it lives, who may write it, whether it can be deleted on request,
whether it may leave the device).

### 1.1 Tier — *what kind of fact is this?*

| Tier | Meaning | Loss impact | Example |
|---|---|---|---|
| `IDENTITY` | Who the account is | Account is unrecoverable | `UserId`, `DisplayName`, `AvatarId` |
| `ECONOMY` | Anything with a balance or a purchase | Player-visible loss, support tickets | `CrystalBalance`, `UnlockedRewardIds` |
| `PROGRESSION` | Earned, monotonic, gated | Player-visible loss | `UnlockedModes`, `Unlocked` per vessel |
| `PREFERENCE` | Player-chosen, re-choosable | Mild annoyance | `SelectedVessel`, audio/invert settings |
| `TELEMETRY` | Derived from play, informational | No player-visible loss | `BestDriftTimeSeconds`, `TotalPrismsDamaged` |
| `SOCIAL` | Relationships and party state | Rebuildable from UGS | friends, party roster |
| `SESSION` | Valid for one run of the process | Nothing | `match_id`, flight clock |

The tier decides **debounce and durability**. `ECONOMY` and `PROGRESSION` writes must not be lost;
`TELEMETRY` may be coalesced aggressively. This is already half-true in the codebase by accident
(`VESSEL_STATS` uses a 2s debounce, `player_profile` 1.5s) — the tier makes it a rule.

### 1.2 Authority — *who is allowed to write it?*

| Authority | Rule |
|---|---|
| `CLIENT` | Local system is the sole writer; cloud is a mirror |
| `SERVER` | Netcode server / host stamps it; clients echo, never compute |
| `SERVICE` | A UGS service owns it; we cache a read-only projection |

This axis exists because of a concrete bug class the email's asks would otherwise walk straight into:
if each client computes `player_ids` locally, they disagree (join order, mid-load drops) and the
`GROUP BY` fragments. Match-envelope fields are `SERVER`. See §6.2.

### 1.3 Durability

`PERSISTENT` (UGS Cloud Save) · `DEVICE` (PlayerPrefs, deliberately not roaming — consent flags,
first-launch guard) · `EPHEMERAL` (process lifetime only).

### 1.4 Privacy class

| Class | Meaning | May leave device? |
|---|---|---|
| `P0` | Non-personal (scores, durations, counts) | Yes |
| `P1` | Pseudonymous (UGS player ID, session IDs) | Yes |
| `P2` | Personal (display name, avatar choice) | Yes — **only under granted consent + age gate** |

`P2` fields are grouped structurally (see §2) so a GDPR export or erasure request operates on a
**group**, not a field hunt. This is the single strongest argument for the nesting in §2.1.

> **Legal follow-up (blocking for store release, not for this branch):** approving display name for
> PostHog means `Docs/Legal/PRIVACY_POLICY_TEMPLATE.md` and the consent dialog copy must name
> PostHog as a data processor and list the categories sent. The existing COPPA age gate and opt-in
> consent in `AnalyticsServiceFacade` already gate collection correctly — only the disclosure text
> is missing.

---

## 2. Format standard

Applies to every Cloud Save model, no exceptions. Today we violate every one of these.

| Rule | Standard | What we do today |
|---|---|---|
| Cloud Save key | `SCREAMING_SNAKE_CASE` | `player_profile` is snake, the other four are SCREAMING |
| JSON field | `PascalCase` | `player_profile` is camelCase, the other four are Pascal |
| Analytics event + param | `snake_case` | already correct |
| Timestamp | `long`, **Unix epoch milliseconds UTC**, suffix `UtcMs` | three formats: `firstSeenUtc` (epoch ms), `LastLoginTick` (.NET ticks), `LastUsedTicks` (.NET ticks) |
| Duration | `float`, **seconds**, suffix `Seconds` | `BestDriftTime`, `BestBoostTime` — unit only in a comment |
| Composite dict key | `"{Mode}:{Intensity}"` | `"Mode:1"` in progression, `"Mode_1"` in stats — two separators for one concept |
| Root model | carries `int SchemaVersion` | absent everywhere |
| Collections | never null after load (`OnAfterLoad`) | correct today, keep it |

`SchemaVersion` is added **now**, while breaking is free, precisely so the next change is a
migration instead of another break. It is the cheapest field in this document and the only one that
pays for itself twice.

### 2.1 On nesting

`PLAYER_PROFILE` and the mode records below use nested groups rather than a flat bag. Three concrete
reasons, not aesthetics:

1. A group maps 1:1 to a privacy class, so erasure/export is `delete profile.Identity`, not a field audit.
2. A group maps 1:1 to a PostHog person-property prefix (`economy_crystal_balance`), so the mirror in
   §7.3 is mechanical rather than a hand-maintained list.
3. A group maps 1:1 to a writer, which makes the single-writer rule checkable by inspection.

Cost: nested `[Serializable]` classes. Newtonsoft (which the UGS SDK uses under the hood, and which
`UGSCloudSaveProvider.LoadAsync` already falls back to) round-trips them fine. `JsonUtility` is not
used on this path — worth restating, because it silently drops `Dictionary<,>` and has bitten this
codebase before.

---

## 3. Cloud Save schemas — the proposal

### 3.0 Key map (before → after)

| Today | Proposed | Change |
|---|---|---|
| `player_profile` | `PLAYER_PROFILE` | rename + restructure + absorb `LastLoginTick` |
| `VESSEL_STATS` | *(deleted)* | **merged into `HANGAR_DATA`** |
| `HANGAR_DATA` | `HANGAR_DATA` | restructure; absorbs vessel stats; write-dead fields fixed |
| `PLAYER_STATS_PROFILE` | `MODE_STATS` | 4 bespoke sub-models collapse to 1 uniform record |
| `GAME_MODE_PROGRESSION` | `GAME_MODE_PROGRESSION` | untouched (quest-system pass) except `SchemaVersion` |

Net: 5 keys → 4. One fewer round-trip at sign-in, and the vessel join stops being a client-side
correlation across two payloads.

---

### 3.1 `PLAYER_PROFILE`

**Tier** IDENTITY + ECONOMY + PROGRESSION · **Authority** CLIENT · **Privacy** P2 (`Identity` group), P0 (rest)
**Writer** `PlayerDataService` (sole) · **Debounce** 1.5s

```json
{
  "SchemaVersion": 1,
  "Identity": {
    "UserId": "gwfoLsxqsqbh3nDbJcOLU1ISqxpo",
    "DisplayName": "Shomback",
    "AvatarId": 8
  },
  "Economy": {
    "CrystalBalance": 37,
    "LifetimeCrystalsEarned": 412,
    "LifetimeCrystalsSpent": 375,
    "UnlockedRewardIds": []
  },
  "Progression": {
  },
  "Lifecycle": {
    "FirstSeenUtcMs": 1784063404202,
    "LastSeenUtcMs": 1784930112000,
    "SessionCount": 41,
    "GamesCompleted": 44,
    "TotalFlightTimeSeconds": 7302.1,
    "LastAppVersion": "0.9.3",
    "LastPlatform": "Android"
  }
}
```

**What changed and why**

- Key renamed to `SCREAMING_SNAKE`; fields to `PascalCase`. Consistency ask, item one.
- `firstSeenUtc` → `Lifecycle.FirstSeenUtcMs`. Same value, name now states the unit.
- **`LastLoginTick` moves here** from `PLAYER_STATS_PROFILE` as `LastSeenUtcMs`, converted from .NET
  ticks to epoch ms. It was never a per-game-mode stat — it is an account fact sitting in the wrong
  bucket. This is the "classify more data here rather than using unused places" ask.
- **New — `LifetimeCrystalsEarned` / `LifetimeCrystalsSpent`.** `PlayerDataService.AddCrystals` /
  `TrySpendCrystals` already emit `crystals_earned` / `crystals_spent`; accumulating them costs two
  `+=` and answers hoarding-vs-spending without an event-store roll-up.
- **New — `SessionCount`, `GamesCompleted`, `TotalFlightTimeSeconds`.** Retention denominators. The
  flight total is the lifetime sum of the per-game figure the email asks for (§5), so the two can be
  reconciled and a drift between them is a real instrumentation alarm.
- **New — `LastAppVersion`, `LastPlatform`.** We currently emit no build or platform anywhere. Every
  "is this regression real" question needs them and none can be answered today.

---

### 3.2 `HANGAR_DATA` — hangar ⊕ vessel stats, merged

**Tier** PROGRESSION (`Unlocked`) + PREFERENCE (`SelectedVessel`) + TELEMETRY (rest) · **Authority** CLIENT · **Privacy** P0
**Writers** `VesselUnlockSystem` (unlock), `MenuVesselSelectionPanelController` (selection), `UGSStatsManager.ReportVesselTelemetry` (stats) · **Debounce** 2.0s

```json
{
  "SchemaVersion": 1,
  "SelectedVessel": "Squirrel",
  "PreferredVessel": "Squirrel",
  "Vessels": {
    "Squirrel": {
      "Unlocked": true,
      "UnlockedUtcMs": 1784063404202,
      "LastUsedUtcMs": 1784930112000,
      "GamesPlayed": 37,
      "FlightTimeSeconds": 4820.6,
      "BestDriftTimeSeconds": 19.834,
      "BestBoostTimeSeconds": 32.292,
      "TotalPrismsDamaged": 726,
      "Counters": { "BestCleanStreak": 40, "JoustsWon": 37, "PrismsStolen": 12193 }
    },
    "Dolphin": {
      "Unlocked": true,
      "UnlockedUtcMs": 1784063404202,
      "LastUsedUtcMs": 1784720000000,
      "GamesPlayed": 2,
      "FlightTimeSeconds": 211.3,
      "BestDriftTimeSeconds": 2.279,
      "BestBoostTimeSeconds": 1.167,
      "TotalPrismsDamaged": 19,
      "Counters": {}
    }
  }
}
```

**What changed and why**

- **`VESSEL_STATS` is gone**, folded in per the "couple both of these together" ask. One vessel is now
  one record. Previously answering *"how much has this player flown the vessel they've unlocked?"*
  required correlating two Cloud Save payloads client-side on a bare string key, with no guarantee the
  two agreed on the vessel-name spelling.
- **`SelectedVessel` gets a writer.** It had **none** — the only reference outside the model was a
  read in `LogControlWindow.cs:1130`, which is why the dump showed `""`. Now written on vessel-swap
  confirm (`UGSStatsManager.ReportVesselSelected`), so it is genuinely "last selected by the user"
  — **plus a default**, because a deliberate pick is the only writer and a player who never opens
  the vessel panel would still read `null`. `UGSDataService.SyncHangarToVessels` falls it back to the
  starter vessel (below) on every load, and also repairs it if it names a vessel the player does not
  own.
- **The starter vessel is seeded into the record.** Ownership was only ever written by
  `VesselUnlockSystem.UnlockVessel`, which early-returns on a vessel that is already unlocked — so
  the one vessel the player owns from first launch (the Squirrel, authored `isLocked: 0`) was never
  persisted and `HANGAR_DATA` reported an empty hangar for a player who could fly. The authored
  truth now lives in a new **`SO_Vessel.OwnedFromStart`** flag rather than `isLocked`, because
  `Unlock()` rewrites `isLocked` at runtime and the editor persists that mutation back into the
  asset — so `isLocked` cannot answer "what did we author?" after the first play session.
  `SyncHangarToVessels` seeds every `OwnedFromStart` vessel on load, and `ResetAllUnlocks` re-grants
  them: a reset returns the player to a *fresh account*, not a locked-out one.
  (Falcon and Shrike had no serialized `isLocked` at all, so they defaulted to **unlocked** —
  both are `Planned` vessels and are now explicitly locked.)
- **`VesselPreferences` (plural) → `PreferredVessel` (singular)**, exactly as asked, and it is now
  *derived, not chosen*: `argmax(FlightTimeSeconds)`, recomputed whenever a vessel's flight time is
  written. "Most hours played" needs hours played, which is why `FlightTimeSeconds` is new here — the
  old `VesselPreference.LastUsedTicks`/`Favorited` pair could not express it, and was never written
  either (only `.Clear()`ed, at `VesselUnlockSystem.cs:84`).
- **`UnlockedVessels: [""]` is a real bug, not a display artifact.** `VesselUnlockSystem.UnlockVessel`
  persists `vessel.Name`, and one `SO_Vessel` asset has a blank `Name`. The flat list becomes a keyed
  map with an explicit `Unlocked` flag, and the writer rejects blank keys — the shape stops permitting
  the bug.
- `BestDriftTime` → `BestDriftTimeSeconds` (and boost likewise): unit in the name.
- `Counters` (free-form `Dictionary<string,int>`) is **kept as-is**. It is the one part of the old
  schema that was already right: per-vessel stats extend without a schema change, which is why
  Sparrow and Squirrel can carry different counters. Do not formalise it.

---

### 3.3 `MODE_STATS` (was `PLAYER_STATS_PROFILE`)

**Tier** TELEMETRY + PROGRESSION · **Authority** CLIENT · **Privacy** P0
**Writer** `UGSStatsManager.Report*Stats` (sole) · **Debounce** 2.0s

```json
{
  "SchemaVersion": 1,
  "Modes": {
    "Scurry:1": {
      "GamesPlayed": 6,
      "GamesWon": 2,
      "BestScore": 39.0,
      "LastPlayedUtcMs": 1784930112000,
      "FlightTimeSeconds": 812.4
    },
    "Joust:1": {
      "GamesPlayed": 2,
      "GamesWon": 1,
      "BestScore": 151.968109,
      "LastPlayedUtcMs": 1784900000000,
      "FlightTimeSeconds": 344.9
    }
  }
}
```

**What changed and why**

- **Four bespoke sub-models collapse to one uniform record.** Today `WildlifeBlitzPlayerStatsProfile`,
  `SkimRacePlayerStatsProfile`, `JoustPlayerStatsProfile` and `ScurryPlayerStatsProfile` are
  four separate classes that hold *the same idea* under four different field names — `HighScores`,
  `BestMultiplayerRaceTimes`, `BestRaceTimes`, `HighScores` — two of them `int` and two `float`.
  Adding Astro League or Brood Rush today means a fifth class, a fifth field on the root, a fifth
  `??=` in `PlayerStatsRepository.OnAfterLoad`, and a fifth branch in
  `UGSStatsManager.GetEvaluatedHighScore`. After this change, **adding a mode is data, not code.**
- Key separator normalised to `:`, matching `GAME_MODE_PROGRESSION.IntensityPlayCounts`. One
  convention across the project.
- `BestScore` is `float` for every mode. Golf-vs-high-score direction is **not stored in the
  record** — storing it per row would let it drift from the mode's real rules.
  **Correction to the original draft:** the retired `LeaderboardConfigSO` never owned the direction
  (it had no such field), and the controller's `MiniGameControllerBase.UseGolfRules` is not reachable at
  report time — reporters run outside the controller's lifetime. The direction therefore lives in
  one table in `UGSStatsManager`, which is exactly where the old `GetEvaluatedHighScore` already
  hardcoded it. This is consolidation, not new duplication, but the real fix is to publish
  `SO_Game.GolfScoring` onto `GameDataSO` so both read one value (§10).
- **New — `GamesPlayed` / `GamesWon` per mode:intensity.** This is the denominator the email's
  "rematch rate **by game mode**" analysis needs, and it gives per-mode win rate for free.
- **New — `FlightTimeSeconds`** per mode:intensity, from the same clock as §5.
- `LastLoginTick` left for `PLAYER_PROFILE` (§3.1).

> **Known redundancy, deliberately not resolved here:** `MODE_STATS[mode:i].GamesPlayed` and
> `GAME_MODE_PROGRESSION.IntensityPlayCounts["mode:i"]` are now the same number in two keys. Both
> writers are correct today, and unifying them means touching the quest/intensity-unlock logic —
> which is explicitly deferred to the quest-system pass. Flagged so it is not rediscovered as a bug.
> When that pass lands, `GameModeProgressionService` should read the count from `MODE_STATS` and drop
> its own dictionary.

---

### 3.4 `GAME_MODE_PROGRESSION` — deferred, as instructed

Untouched except `SchemaVersion: 1`, so it does not become the one key without a version. It is
rebuilt in the quest-system pass, absorbing the redundancy noted above.

---

## 4. What this costs

| Area | Change |
|---|---|
| Deleted | `VesselStatsCloudData.cs`, `VesselStatsRepository.cs`, 4 per-mode `*PlayerStatsProfile.cs` |
| Rewritten | `PlayerProfileData.cs`, `HangarCloudData.cs`, `PlayerStatsProfile.cs` → `ModeStatsCloudData.cs` |
| Touched | `UGSKeys.cs`, `UGSDataService.cs`, `PlayerDataService.cs`, `UGSStatsManager.cs`, `VesselUnlockSystem.cs`, `LogControlWindow.cs`, `AnalyticsServiceFacade.cs` |
| New writers | `SelectedVessel`, `PreferredVessel`, per-vessel + per-mode `FlightTimeSeconds`, lifetime counters |
| Data loss | **All existing cloud saves reset** (approved: pre-launch) |

No migration shims are written. The `SchemaVersion` field is what makes the *next* change a migration.

---

## 5. `flight_time_seconds` — the email's first ask

> *"elapsed time from when the player is given control until the end of the game (excluding pause)"*

### 5.1 Where "given control" is

`GameDataSO.StartTurn()` (`GameDataSO.cs:332`) — it sets `IsTurnRunning = true` and raises
`OnMiniGameTurnStarted`. It is called from `MultiplayerMiniGameControllerBase`'s
`OnCountdownTimerEnded_ClientRpc` (`:164`) and `MultiplayerDomainGamesController` (`:97`) — i.e.
the instant after the countdown when players are activated. That is exactly the moment asked for,
and it already exists on every controller path. Turn end is `InvokeGameTurnConditionsMet()`, which
sets `IsTurnRunning = false`.

### 5.2 Why the obvious implementations are both wrong

| Candidate | Fails because |
|---|---|
| `Time.realtimeSinceStartup` delta — **what `duration_seconds` uses today** | counts pause and backgrounded time |
| `Time.time` delta (scaled) | excludes pause correctly, but **Astro League distorts `Time.timeScale`** for hitstop and goal slow-mo (`AstroLeagueBall.cs:1068`, `AstroLeagueController.cs:894`), so seconds-in-slow-mo under-count |

### 5.3 The implementation — `FlightClock`

A small `SESSION`-tier accumulator:

```
accumulate Time.unscaledDeltaTime  while  IsTurnRunning
                                    &&  !PauseSystem.Paused
                                    &&  !appBackgrounded
```

- Unscaled → immune to hitstop/slow-mo.
- `PauseSystem.Paused` (`PauseSystem.cs:8`, with `OnGamePaused` / `OnGameResumed` events already
  present) → excludes the pause menu. This is the "not sure how hard that is" part of the email:
  **it is easy**, the flag and both events already exist.
- `ApplicationLifecycleManager.OnAppPaused` → also excludes backgrounding, which the email did not
  ask for but which is the same class of dead time and would otherwise dominate mobile numbers.
- **Accumulates across turns and rounds** within one game, so multi-round modes report total time at
  the stick. The Ready-button gap between rounds is excluded for free, because `IsTurnRunning` is
  false there.

### 5.4 Keep `duration_seconds` too

`game_completed` keeps the existing wall-clock `duration_seconds` alongside the new
`flight_time_seconds`. **The delta between them is the pause + AFK + between-round time**, which is a
free churn signal we would otherwise have to instrument separately.

The same clock feeds `PLAYER_PROFILE.Lifecycle.TotalFlightTimeSeconds`,
`HANGAR_DATA.Vessels[v].FlightTimeSeconds` (which is what makes `PreferredVessel` computable) and
`MODE_STATS[m:i].FlightTimeSeconds`. One clock, four consumers.

### 5.5 Menu freestyle counts too

The lava lamp **is** freestyle (one system, two names — see CLAUDE.md § "Lava-Lamp Mode"), so the
vessel drifting behind the menu is the gameplay vessel and time spent flying it is time at the
stick. But freestyle has no countdown, no turn and no end: `MenuCrystalClickHandler` never raises
`GameDataSO.StartTurn`, so §5.1's gate can never open there and every minute of it was invisible.

`FlightClock` therefore runs a **second segment** with the same integrator minus the turn gate:

```
accumulate Time.unscaledTime  while  freestyleActive
                                &&  !PauseSystem.Paused
                                &&  !appBackgrounded
```

- Driven by `MenuCrystalClickHandler` at exactly the two lines that grant and revoke control
  (`InputController.SetPause(false/true)`), so the camera blend is not counted as flight.
- Accumulated **separately** from the game segment, so a freestyle segment can never contaminate
  `LastGameSeconds` — which `game_completed` reads.
- Published per closed segment via `FlightClock.OnFreestyleSegmentCompleted`, rather than held to a
  "last visit" total: freestyle has no end event to read one at. A segment closes on leaving
  freestyle, on pause, **and on backgrounding** — the last one deliberately, so a mobile app killed
  while suspended has already banked what it earned.
- `MenuCrystalClickHandler.OnDisable` closes the segment, so launching a game from freestyle banks
  the time instead of dropping it.

Landed by `UGSStatsManager.ReportFreestyleFlight` into `PLAYER_PROFILE.Lifecycle.TotalFlightTimeSeconds`
and `HANGAR_DATA.Vessels[v].FlightTimeSeconds` — the second one matters because "most hours played on
a vessel" (`PreferredVessel`) is otherwise blind to the vessel the player spends the most time in.
It deliberately does **not** touch `GamesCompleted` or `GamesPlayed`: no game was played.

**No new event and no new parameter.** Menu flight reaches PostHog through the person properties
`total_flight_time_seconds` / `preferred_vessel`, which `ReportFreestyleFlight` re-publishes via
`AnalyticsServiceFacade.IdentifyPlayer()`. Nothing has to be added to the UGS Event Manager. If a
discrete `freestyle_session_ended` event is wanted later for segmentation, it costs one event name
and zero parameters (`flight_time_seconds` and `vessel_class` already exist and are reusable).

---

## 6. Grouping players — the email's second ask

> *"a `lobby_id` or `party_id` on `game_started` — a shared identifier that all players in the same
> game instance carry"*

### 6.1 One identifier is not enough — send two

This is the one place I want to push back on the email as written.

`PartySessionService.ActiveSession.Id` is the natural candidate and it is genuinely shared: under the
locked **eager per-user Relay** design every player hosts a session on entering Menu_Main, and an
accepted invite makes the joiner join *the inviter's* session — so both carry the same id. But a party
that stays together and plays three matches back-to-back keeps **one** session id, so grouping on it
alone collapses three games into one and undercounts everything per-match.

Conversely a per-match id alone loses "the same four people stayed together across the evening".

So `game_started` carries both:

| Field | Value | Grain |
|---|---|---|
| `match_id` | GUID minted per game launch | one game instance — **this is the "same game instance" key the email wants** |
| `party_id` | `PartySessionService.ActiveSession.Id` | one sitting; stable across consecutive matches |

The organic-rematch query then reads: *same `player_ids` set + same `game_mode` +
`invite_triggered = false` + a **different** `party_id` + within X days of a prior session together.*
Requiring a different `party_id` is what stops three matches in one sitting from registering as two
rematches — which the email's single-identifier version would have done.

### 6.2 Envelope authority

`match_id`, `party_id` and `invite_triggered` are **`SERVER` authority**: the host stamps them once
in `MultiplayerMiniGameControllerBase.OnNetworkSpawn` and broadcasts them through the existing
`SyncGameConfigToClients_ClientRpc` (which already carries intensity, player count and AI backfill).
Clients echo them verbatim and never recompute — otherwise the `GROUP BY` the whole analysis depends
on fragments into near-duplicate rows.

**`player_ids` is the exception, and the draft was wrong about it.** It cannot be stamped at
`OnNetworkSpawn`, because the roster has not settled there. Stamping it later would mean either a
new RPC hooked into a turn-start path that **nine** controllers override, or a base-owned hook that
does not exist. Instead it is derived on every peer at `game_started` from **replicated
`Player` NetworkObjects** and then **sorted**.

That is deterministic for the same reason host-stamping would be: it reads replicated state, which
is identical on every peer once settled, and sorting removes join-order divergence. The original
objection — that clients would disagree — applies to deriving from the *local party roster*
(`HostConnectionDataSO.PartyMembers`), which is the party roster rather than the match roster and
still lists a peer that dropped during scene load. Deriving from the spawned NetworkObjects does not
have that problem.

### 6.3 `player_ids` — we cannot produce this today

`Player` replicates **no UGS player ID**. `IPlayer.PlayerUUID` is `=> Name` (`Player.cs:81`) — a
display name masquerading as a UUID.

Two sources were considered:

| Source | Verdict |
|---|---|
| `HostConnectionDataSO.PartyMembers` | **Rejected.** It is the *party roster*, not the *match roster*. A member who dropped during scene load is still listed, AI is not represented, and it says nothing about who actually spawned in. |
| New `Player.NetUgsPlayerId` | **Chosen.** Owner-write `NetworkVariable<FixedString64Bytes>`, set in `OnNetworkSpawn` from `AuthenticationService.Instance.PlayerId`, next to the existing `NetName` / `NetAvatarId` owner writes. |

The NetworkVariable is authoritative for who is genuinely in the match. `player_ids` is filtered to
humans (`!NetIsAI`); AI is reported separately as `player_count_ai`. UGS Analytics parameters accept
only scalars, so it travels as a comma-joined string and the PostHog sink expands it back to an array.

`PlayerUUID` is **left as-is** rather than repointed at the new id: it is load-bearing for AOE block
ownership strings (`AOERadialBlocks`, `AOEBlockCreation`, `AOEDangerHemisphereBlocks`) and for
`MiniGame`'s local-player comparison, so changing its meaning would be a gameplay change smuggled
into an analytics commit. `IPlayer.UgsPlayerId` is added alongside it, and retiring `PlayerUUID` is
tracked in §10.

### 6.4 `invite_triggered` — where the truth lives

Neither peer can answer this alone: the joiner knows they accepted an invite, the host knows they sent
one, and a third party who arrived through presence knows neither. So it is party-level state.

`HostConnectionDataSO` gains `[HideInInspector] public bool PartyFormedByInvite`:

- **set `true`** on the joiner in `PartyInviteController.AcceptInviteAsync`, and on the host when
  `OnPartyMemberJoined` fires for a player with an outstanding sent invite;
- **reset `false`** in `ResetRuntimeData()`, on party leave, and whenever `PartyMembers.Count <= 1`;
- **read only by the host at launch**, and broadcast per §6.2 — so all clients report one value.

Reset-on-empty is the load-bearing part. Without it a party that formed by invite, dissolved, and
re-formed organically the next day would still report `invite_triggered = true` and be excluded from
exactly the organic-rematch cohort we are trying to measure.

---

## 7. Event schema

### 7.1 Common envelope — on every event

`player_id` (UGS, P1) · `session_id` · `app_version` · `platform` · `device_model` ·
`schema_version` · `timestamp_utc_ms` (long) · `timestamp_utc_iso` (string).

Both timestamp forms are sent deliberately: `_ms` for arithmetic, `_iso` for the day/week/month
bucketing the email asks for without a per-query conversion. UGS and PostHog both stamp their own
ingest time — the explicit field is the *client* time and is what the email means by "at completion".

### 7.2 `game_started` / `game_completed`

```
game_started
  match_id          NEW  server-stamped GUID, one per game instance
  party_id          NEW  server-stamped Relay session id
  player_ids        NEW  server-stamped array of human UGS ids
  invite_triggered  NEW  server-stamped bool
  player_count_human NEW
  player_count_ai   NEW  (was ai_count)
  game_mode, intensity, vessel_class, player_count, is_multiplayer    (existing)

game_completed
  flight_time_seconds  NEW  §5 — control granted → end, pause/background excluded
  timestamp_utc_ms     NEW  }  in the envelope, called out because the email asks for them
  timestamp_utc_iso    NEW  }
  match_id, party_id   NEW  echoed, so completion joins to start
  final_score          NEW
  final_rank           NEW
  duration_seconds, player_won, game_mode, intensity, vessel_class,
  player_count, ai_count, is_multiplayer                              (existing)
```

`vessel`, `game_mode` and `player id` from the email are already present or arrive via the envelope.

### 7.3 PostHog

> **Status:** the PostHog project itself is **already set up**. The only missing piece is the
> Unity → PostHog link, which is what this section builds.

**Transport.** `IAnalyticsSink` is extracted behind the existing `AnalyticsServiceFacade.RecordEvent`
choke point (`AnalyticsServiceFacade.cs:285`) — that method is already the single funnel every event
passes through, so this is an interface extraction, not a rewrite. Two sinks register:
`UgsAnalyticsSink` (today's behaviour, unchanged) and `PostHogAnalyticsSink`.

`PostHogAnalyticsSink` batches to `{host}/batch/`. It flushes on pause/quit and on a size/interval
threshold, persists its queue to `Application.persistentDataPath` so offline play is not lost, and
sits **behind the same consent + age gate** as UGS — an `IAnalyticsSink` cannot opt out of the gate,
because the gate is upstream of the interface.

Config lives in `PostHogConfigSO` (project API key, host, batch size, flush interval, enabled). The
PostHog **project** API key is write-only by design and is safe to ship in a client build. A PostHog
**personal** API key is read/admin-capable and must never be placed in this SO or any client build.

**Coverage.** All ~30 events already declared in `UGSKeys.cs` plus the new fields above — the two sinks
receive identical payloads, so PostHog is at full parity from the first commit rather than the current
partial feed.

**Erasure is only partial from the client, and this is surfaced rather than hidden.** Deleting a
PostHog *person* requires a personal (admin) API key, which must never ship in a client build. So
`PostHogAnalyticsSink.RequestDataDeletion` does what it actually can: stops collecting, drops the
pending queue, and sets `gdpr_deletion_requested` + a timestamp on the person so an operator or
server-side automation can complete the deletion. **Until that automation exists, pressing the
in-game erasure button does not fully honor the request** — see §8.6 and §10.

#### 7.3.1 Identity — two searchable keys, one canonical

Sending the UGS ID *alone* would make PostHog unusable for the thing it is for: recognising a player.
Sending the display name *as the identity* would break every join and silently split one player into
several the first time they rename. So:

| Role | Value | Why |
|---|---|---|
| `distinct_id` (canonical identity) | UGS player ID | Immutable. Same key as Cloud Save, Leaderboards and UGS Analytics, so PostHog joins to all three with no mapping table. Survives renames. |
| `display_name` (person property, searchable) | `PLAYER_PROFILE.Identity.DisplayName` | What a human actually recognises. PostHog person search matches on any property, so a name lookup finds the person and their whole history. |

Both are always present, so a person is findable by **either**. The distinction is only which one the
event graph is keyed on — using a mutable, non-unique string as the key is how analytics datasets
fragment, and two players can legitimately choose the same display name.

`display_name` is set as a **person property, not an event property**. One mutable copy per person
instead of one immutable copy per event. That is data minimisation done structurally (§8.5), it makes
a rename correct retroactively rather than leaving a trail of stale names, and it makes rectification
and erasure a single-object operation instead of a scan.

#### 7.3.2 Person properties (`$set` on identify)

From `PLAYER_PROFILE`: `display_name` (P2), `avatar_id`, `crystal_balance`, `xp`, `first_seen_utc_ms`,
`session_count`, `games_completed`, `total_flight_time_seconds`.
From `HANGAR_DATA`: `preferred_vessel`, `selected_vessel`, `unlocked_vessel_count`.
From `GAME_MODE_PROGRESSION`: `unlocked_mode_count`.

Refreshed on profile change and at session end. The §2.1 grouping is what makes this mapping
mechanical rather than a hand-maintained list that drifts.

### 7.4 Backend configuration

Two non-code steps, both easy to forget and both silent when skipped:

1. **UGS dashboard Event Manager** — every event *and every parameter* must be declared or the backend
   **silently discards** the event. This is already noted in `UGSKeys.cs:26` and is the most likely
   cause of "very few data is transferred".
2. **PostHog project** — create the project, take the write-only key, define the rematch insight.

Both are driven from a generated `Docs/Analytics/EVENT_SCHEMA.json` (every event, parameter, type,
and which sinks receive it) so dashboard configuration is a copy-paste rather than a memory exercise,
and so schema drift between code and dashboard is diffable.

---

## 8. Legal exposure of sending this data externally

> **Not legal advice.** This is an engineering inventory of the obligations that attach once player
> data leaves the device for a third party, written so counsel can be pointed at specific items
> rather than at "we added analytics". Items marked **BLOCKING** must be closed before a public store
> release; none of them block this branch.

The trigger is specific: today all player data terminates at Unity Gaming Services, which is already
named in our terms and already the identity provider. Adding PostHog makes Froglet a **data
controller** exporting personal data to a **new processor**. Approving `display_name` (§1.4) makes
the payload personal data rather than pseudonymous, which raises almost every item below from
"document it" to "do something about it".

### 8.1 Lawful basis and consent — BLOCKING

- Display name + device ID + behavioural history is **personal data** under GDPR/UK GDPR. Analytics
  of this kind is realistically consent-based, and ePrivacy/PECR separately governs reading or
  writing identifiers on the device.
- Consent must be **freely given, specific, informed, unambiguous, and as easy to withdraw as to
  give**. The existing opt-in gate in `AnalyticsServiceFacade` (`ConsentGranted` + `AgeEligible`,
  tri-state, default deny) is structurally correct and already satisfies the mechanics.
- What is missing is **disclosure**: consent copy and `Docs/Legal/PRIVACY_POLICY_TEMPLATE.md` must
  name PostHog as a recipient and state the categories sent. Consent obtained without naming the
  recipient is not informed consent, so the gate we already have does not cover us on its own.
- Withdrawal must stop *future* collection **and** trigger deletion of past data (§8.6).

### 8.2 Processor contract — BLOCKING

- A signed **Data Processing Agreement** with PostHog is required (GDPR Art. 28). PostHog publishes
  one; it has to actually be executed, not assumed.
- PostHog's **sub-processor list** must be reviewed and the categories disclosed in our policy. Any
  future sub-processor change is our problem to track, not theirs.

### 8.3 International transfer — RESOLVED: EU Cloud

**Decision: the PostHog project is EU Cloud (Frankfurt), and it stays there.** The client host is
`https://eu.i.posthog.com` (`PostHogConfig.asset`). PostHog's region cannot be changed after the
project is created, so this is effectively permanent.

The question that prompted a re-think — *"should it be US, since Froglet is a Delaware C-corp?"* —
rests on a premise worth correcting explicitly, because it will come up again:

- **GDPR applies based on where the *players* are, not where the company is incorporated.** Art. 3(2)
  gives it extraterritorial scope: a US entity offering a service to people in the EU is in scope.
  A game on Steam and mobile stores will have EEA/UK players from day one, so incorporation in
  Delaware changes nothing about the obligation.
- **No US law requires US residency for consumer game analytics.** Data-residency mandates of that
  kind attach to government/defense work, not to a game studio's telemetry. So US Cloud buys nothing
  legally.
- **US Cloud would cost real work.** Every EEA/UK player's events become a restricted transfer out
  of the EEA, requiring Standard Contractual Clauses plus a transfer impact assessment, or reliance
  on the EU–US Data Privacy Framework — whose two predecessors (Safe Harbour, Privacy Shield) were
  both struck down, so it is a foundation that has failed twice before.
- **EU Cloud costs nothing.** Same price, same features, and for batched analytics the latency
  difference is irrelevant — events are queued and uploaded on an interval, not per-frame.

So EU Cloud is strictly the cheaper option: it removes the transfer question for the strictest
regime we are subject to, and gives up nothing for US players. Keeping it.

### 8.4 Children — BLOCKING if the game is rated for under-13s

- COPPA prohibits collecting personal information from under-13s without verifiable parental
  consent. Our age gate already blocks collection entirely for under-13, and the PostHog sink sits
  **downstream** of that gate by construction (§7.3), so the technical posture is correct.
- The residual risks are classification, not code:
  - If the game is listed as child-directed or mixed-audience on either store, **Google Play Families
    policy restricts which SDKs may be present at all** — a self-built HTTP sink is easier to defend
    here than a vendor SDK, which is a genuine point in favour of the §7.3 approach.
  - Apple's Kids Category prohibits third-party analytics outright. If Kids Category is ever a
    target, PostHog is incompatible with it.
  - Age-gate answers are self-declared. That is the standard approach and generally accepted, but it
    should be a documented decision rather than an implicit one.

### 8.5 Display names specifically — the biggest new risk

Approving display name is the single change that most increases exposure, for a reason that is easy
to miss: **it is free text a player typed.**

- Players routinely put real names, emails, phone numbers, social handles, and occasionally
  special-category data (health, religion, sexuality, political affiliation) in display names. Every
  one of those lands in PostHog and inherits the strictest applicable rules — special-category data
  under GDPR Art. 9 needs explicit consent, which a generic analytics consent does not provide.
- Display names are visible to other players in-game, but "semi-public" does **not** make it
  non-personal data. It is still identifying and still in scope.
- Mitigations worth taking, in order of value:
  1. **Person property, not event property** (already in §7.3.1) — one copy per person, so
     rectification and erasure are one operation, and a rename corrects history instead of leaving
     a trail.
  2. Whatever profanity/PII filter guards the display name at entry should be treated as a privacy
     control, not just a moderation one — it is the only thing standing between free text and the
     analytics store.
  3. Keep a documented kill switch: `display_name` is one property in one mapping, so if counsel
     objects the design degrades to ID-only without touching anything else.

### 8.6 Data subject rights — a concrete gap this branch creates

Access, rectification, erasure, portability, and objection all now have to reach **two** systems.

- `AnalyticsServiceFacade.RequestDataDeletion()` currently calls **only**
  `AnalyticsService.Instance.RequestDataDeletion()` (UGS). The moment a PostHog sink exists, that
  method is **incomplete** — it would report a deletion that did not fully happen, which is worse
  than not offering the button.
- **Shipped:** `RequestDataDeletion()` now fans out to every sink, so UGS deletion still runs and
  PostHog is told too. **But PostHog deletion is only partial from the client** (§7.3): the write-only
  project key cannot delete a person, so the sink flags `gdpr_deletion_requested` and an operator or
  server-side automation must finish it. That automation is a **blocking release item** (§10) — a
  button that reports a deletion which did not happen is worse than no button.
- The right-to-erasure button must also be reachable in Settings/Privacy, not only via API.

### 8.7 Retention

- GDPR requires a defined, documented, justified retention period — "forever, it's analytics" is not
  one. PostHog retention is plan-dependent and must be configured, not defaulted.
- Cloud Save data has no retention policy today either. Worth setting both at once while we are in
  the area.

### 8.8 App store disclosure — BLOCKING, and the most likely practical failure

This is where mismatches actually get caught, because both stores compare declared behaviour against
observed behaviour.

- **Apple Privacy Nutrition Labels** must declare identifiers, usage data, and — because of display
  name — **user content**, all as *linked to identity*. Under-declaring is a common rejection cause.
- **App Tracking Transparency**: first-party analytics not shared with data brokers and not used for
  cross-app tracking generally does not require the ATT prompt, but that determination must be
  **written down and justified**, not assumed. PostHog must not be configured for any cross-app or
  advertising identity resolution.
- **Google Play Data Safety** form must declare collection, third-party sharing, encryption in
  transit, and a deletion mechanism (§8.6 is what makes that declaration true).
- Both forms must be updated **in the same release** as the sink ships. A truthful form describing
  the previous build is still a false declaration.

### 8.9 Security and configuration hygiene

- Ship the **project** (write-only) key only. A personal API key in a client build is a full data
  breach — it is readable in any decompiled build.
- HTTPS only. Never log payloads containing display names through `CSDebug` in release builds.
- **IP address**: PostHog captures client IP for geolocation by default. IP is personal data in the
  EU. Decide explicitly whether to disable geolocation or disclose it; the default is a silent
  collection we did not consciously choose.
- Confirm **session replay and autocapture are off** on the project. Neither is meaningful for a Unity
  client, but a project default left on is an unnecessary category of collection to have to defend.

### 8.10 Contractual and regional odds and ends

- **Unity UGS terms** and PostHog terms both apply to data that originates in UGS and is exported.
  Worth a read to confirm nothing restricts onward export of UGS-derived data.
- **CCPA/CPRA**: analytics of this kind is typically not a "sale", but "sharing" is defined broadly.
  Categories collected and any opt-out must be disclosed if there is a California audience.
- Other regimes with their own consent or localisation rules (Brazil LGPD, Canada PIPEDA, South Korea
  PIPA, India DPDP) attach as the audience widens. Not urgent; do not discover them at launch.

### 8.11 Summary — what actually has to happen

| # | Item | Owner | Blocking release? |
|---|---|---|---|
| 1 | Name PostHog in privacy policy + consent copy, list categories | product/legal | **Yes** |
| 2 | Execute PostHog DPA; review sub-processors | legal | **Yes** |
| 3 | Confirm PostHog project region; SCCs if US Cloud with EEA/UK players | legal + instrumentation | **Yes** |
| 4 | Extend `RequestDataDeletion()` to delete the PostHog person | **engineering, commit 4** | **Yes** |
| 5 | Update Apple Nutrition Labels + Play Data Safety in the shipping release | product | **Yes** |
| 6 | Confirm store age rating / Families-policy classification | product | Yes, if child-directed |
| 7 | Define + configure retention (PostHog and Cloud Save) | instrumentation | No |
| 8 | Decide on IP/geolocation capture; confirm replay + autocapture off | instrumentation | No |
| 9 | Document the ATT determination | product | No |
| 10 | Treat the display-name entry filter as a privacy control | engineering | No |

---

## 9. Sequencing

Four commits, each independently reviewable and shippable.

| # | Scope | Depends on |
|---|---|---|
| 1 | **SHIPPED** — Format standard + schema rewrite (§2–4): models, repositories, `UGSKeys`, writers, `LogControlWindow` dump | — |
| 2 | **SHIPPED** — `FlightClock` + `flight_time_seconds` + timestamps on `game_completed` (§5); feeds the new per-vessel / per-mode / lifetime totals | 1 |
| 3 | **SHIPPED** — Match envelope (§6): `Player.NetUgsPlayerId`, `HostConnectionDataSO.PartyFormedByInvite`, host stamping through `SyncGameConfigToClients_ClientRpc`, new `game_started` fields | 1 |
| 4 | **SHIPPED** — `IAnalyticsSink` extraction + `PostHogAnalyticsSink` + `PostHogConfigSO` + dual identity (§7.3.1) + person properties + deletion fan-out (§8.6, partial) + `EVENT_SCHEMA.json` | 2, 3 |

Verification is in-editor per commit: MPPM two-client session for commit 3 (both clients must emit
byte-identical `match_id` / `party_id` / `player_ids` / `invite_triggered`), and the
`LogControlWindow` cloud-data dump for commit 1. Commit 4 verifies against the live PostHog project:
one event round-trips, the person is findable by **both** UGS ID and display name, and the deletion
path removes it.

---

## 10. Open items

| Item | Owner | Blocking release? |
|---|---|---|
| ~~PostHog project region~~ — **RESOLVED: EU Cloud**, client host set to `eu.i.posthog.com` (§8.3) | — | Done |
| Privacy policy + consent copy must name PostHog and list categories sent (§8.1) | legal/product | **Yes** |
| Execute the PostHog DPA, review sub-processors (§8.2) | legal | **Yes** |
| Apple Nutrition Labels + Play Data Safety updated in the shipping release (§8.8) | product | **Yes** |
| Declare all events/parameters in the UGS Event Manager, or the backend silently discards them (§7.4) | instrumentation | No |
| Confirm PostHog replay + autocapture off; decide on IP/geolocation capture (§8.9) | instrumentation | No |
| Define retention for PostHog and Cloud Save (§8.7) | instrumentation | No |
| **Fill in the PostHog project API key** in `Assets/Resources/PostHogConfig.asset` (write-only project key, never a personal key) — the sink stays inert until then | instrumentation | **Yes** |
| **Server-side PostHog person deletion** to complete right-to-erasure; the client can only flag it (§7.3, §8.6) | instrumentation | **Yes** |
| Run `Tools/Analytics/export_cloud_save.py` + `import_snapshot_to_posthog.py` once to backfill existing players into PostHog People (needs a read-only UGS service account) | instrumentation | No |
| One `SO_Vessel` asset has a blank `Name` — fix the asset (§3.2 stops it persisting, it does not fix the asset) | gameplay | No |
| Publish `SO_Game.GolfScoring` onto `GameDataSO` so scoring direction has one source (§3.3) | gameplay | No |
| Retire `IPlayer.PlayerUUID` (display name) in favour of `UgsPlayerId`, once AOE ownership strings are decoupled (§6.3) | gameplay | No |
| `IntensityPlayCounts` ↔ `MODE_STATS.GamesPlayed` redundancy | quest-system pass | No |

~~PostHog project + write-only API key~~ — **done** (project set up; only the Unity link is outstanding,
which is commit 4).
