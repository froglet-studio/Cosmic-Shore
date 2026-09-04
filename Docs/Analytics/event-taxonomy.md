# Event Taxonomy — Canonical Schema & Conformance Review

> **Provenance.** Written on the `claude/analytics-attribution-viability-vwkunw` branch
> (PR #592) as an analysis/design pass, and salvaged onto bleeding-edge after the sink
> layer shipped separately. The *analysis* here stands. Where it describes implementation
> shape (interface signatures, class names, field lists), **`Docs/Analytics/DATA_ARCHITECTURE.md`
> is the authority** - the shipped `IAnalyticsSink` differs in detail (`RecordEvent` +
> `Identify` + `StartCollection`/`StopCollection`, person properties, a disk-persisted queue).

> Companion to `viability-report.md` (2026-07). Defines the naming convention, the required
> envelope on every event, a conformance review of the 28 events that exist today, and the
> events we should be firing but aren't. The current-state inventory lives in
> `DATA_INVENTORY.md`; this doc is the *target* spec.

---

## 1. Naming convention

**Pattern: `noun_verb-in-past-tense`, snake_case, all lowercase.**

- `game_started`, `crystals_earned`, `vessel_unlocked` — correct today, keep.
- The noun comes first so events sort/group by subsystem in any tool
  (`game_*`, `crystal*`, `party_*`, `friend_*`).
- No abbreviations, no camelCase, no spaces. Parameters follow the same rule.
- Enum-valued string parameters carry the C# enum's `ToString()` — that is already the
  de-facto convention (`game_mode`, `vessel_class`) and it is fine, but the enum value set
  must be treated as part of the schema (renaming a `GameModes` member is a breaking
  analytics change; note this in PR review).
- One event = one fact. Do not fire a second event to carry an extra property of the same
  fact (see `crystal_balance_snapshot` below).

**Where the constant lives:** every event name is a `const` in
`Assets/_Scripts/System/UGSKeys.cs` and is recorded only through
`AnalyticsServiceFacade` (`Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`).
No event name string literal may appear anywhere else. This is already true today — keep it
true; it is what makes a second sink a one-file change.

**UGS constraint:** every event *and every parameter* must also be declared in the UGS
dashboard Event Manager or the backend silently discards it. A new event is not "shipped"
until (a) the constant exists in `UGSKeys`, (b) the facade records it, (c) it is declared in
the dashboard, and (d) it has a row in this doc. Add all four to the PR checklist.

---

## 2. Required envelope (every event)

UGS Analytics automatically attaches its own envelope to every event — user ID, session ID,
platform, client version, timestamp, country and more are collected by the SDK and do not
need to be sent as custom parameters. **Do not duplicate them into custom parameters for the
UGS sink.**

The envelope matters the moment a second sink exists (see `implementation-plan.md` Phase 2):
a non-UGS sink gets nothing for free, so the facade must stamp the envelope itself. Putting
it in the sink layer — not in each call site — keeps call sites unchanged.

| Property | Type | Source | Notes |
|---|---|---|---|
| `player_id` | string | `AuthenticationService.Instance.PlayerId` (already cached in `AuthenticationDataVariable`) | UGS anonymous player ID. **Known limitation: does not survive reinstall** (anonymous auth, session token only). Per-person identity requires platform sign-in linking — the facade stubs exist (`AuthenticationServiceFacade.SignInWithSteamAsync` etc.) but are not implemented. |
| `session_id` | string | GUID generated once per app run by the facade | UGS has its own session ID; the second sink needs ours. Generate in the facade constructor. |
| `install_id` | string | Device GUID persisted in PlayerPrefs on first run | Survives sign-out but not reinstall/device wipe. Bridges pre-auth events to the player. |
| `build_version` | string | `Application.version` | |
| `platform` | string | `Application.platform.ToString()` | |
| `ts_utc` | long | Unix epoch ms at record time | Stamped by the sink, not the caller. |

Optional envelope, stamp when known: `game_mode`, `intensity` (only while a game is active —
lets any mid-game event segment by mode without each call site passing it).

---

## 3. Current events — conformance review

All 28 events route through `AnalyticsServiceFacade`. Verdicts: **OK** = keep as-is,
**RENAME** = misnamed against §1, **RESHAPE** = keep name, change parameters,
**MERGE** = fold into another event.

| Event | Verdict | Issue / change |
|---|---|---|
| `game_started` | RESHAPE | Add `match_id` (GUID per game, generated at `game_started`, reused on `game_completed`). Today a session with several games can only pair start/complete events by timestamp ordering — fragile in any downstream tool. |
| `game_completed` | RESHAPE | Add `match_id`, `score` (long), `crystals_collected` (int). The single most-queried event has **no score on it** — score lives only in Cloud Save bests and leaderboards, which lose every non-best game. |
| `session_ended` | RESHAPE | Absorb `crystal_balance_snapshot` as a `crystal_balance` parameter (see MERGE below). `reason`/`last_ui_action`/`app_state` are good. |
| `crystal_balance_snapshot` | MERGE | Fired only ever alongside `session_ended`, as a second event carrying one int. It is a property of session end, not a separate fact. Fold into `session_ended.crystal_balance`, delete the event. |
| `play_again_pressed` | RENAME | Named after the button, not the fact, and `ui_action` already covers UI clicks. Either drop it (a `game_started` within N seconds of `game_completed` with the same mode is a replay — derivable) or rename `replay_requested`. Prefer drop. |
| `repeated_game_fail` | RENAME | Verb-noun order inverted and "fail" not past tense. The fact is a loss-streak threshold crossing: rename `loss_streak_reached` with `streak` (int) — or drop the event and derive it downstream from `game_completed.player_won`, which any real query layer can do. Prefer derive-downstream once a second sink exists; keep until then. |
| `ui_action` | RESHAPE | Fine as a catch-all, but `UserActionType` (`Assets/_Scripts/Data/Enums/UserActionType.cs`) is stale — `ViewArcadeGameDolphinDarts` / `ViewArcadeGameRampage` reference games that no longer exist, and nothing covers the live screens (PORT, PROFILE, STORE tabs). Refresh the enum to the current IA. |
| `ad_impression` | RESHAPE | Fires on `AdLoaded` — that is a *load*, not an impression. `AdsSystem` already exposes `AdShowStart` / `AdShowComplete` / `AdShowFailure`; wire those as `ad_shown` / `ad_completed(completion_state)` / `ad_failed(error)` and fire `ad_impression` on show, not load. The current event over-counts by every preload that is never shown. |
| `game_first_launched` | OK | PlayerPrefs-guarded once-ever. Note: cannot fire before consent is granted (collection gate), so "first launch" is really "first consented launch" — acceptable, document it. This is also where the future `acquisition_source` parameter lands (see §4). |
| `menu_ready` | OK | |
| `freestyle_entered` | OK | |
| `mode_unlocked` | OK | |
| `intensity_unlocked` | OK | |
| `setting_changed` | OK | `value` stringified — acceptable tradeoff for one event covering nine settings. |
| `crystals_earned` / `crystals_spent` | OK | `source` is free-text from call sites — fix the vocabulary (allowed values: `game_reward`, `daily_challenge`, `quest`, `vessel_purchase`, …) and enforce at the two call sites in `PlayerDataService`. |
| `crystal_spend_blocked` | OK | Good starvation signal. |
| `vessel_unlocked` | OK | |
| `quest_completed` | OK | `quest` is the display title — switch to a stable ID (asset name) so renaming UI copy doesn't fork the funnel. |
| `share_triggered` | OK | |
| `friend_request_sent` / `friend_request_received` / `friend_added` | RESHAPE | Carry raw counterpart player IDs (`target_id`, `from_id`, `friend_id`). Analytically we only ever need *that it happened*; the counterpart ID is data-minimization liability under GDPR and useless in aggregate. Drop the ID parameters. |
| `party_invite_sent` / `party_invite_received` | RESHAPE | Same — drop `target_id` / `host_id`. |
| `party_joined` | OK | Add `party_size` (int) — the one aggregate-useful fact, available from `HostConnectionDataSO.PartyMembers`. |
| `minigame_favorited` | OK | |
| `cloud_save_failed` | OK | |

---

## 4. Events we should be firing and aren't

Priority: **P0** = blocks a funnel/cohort question we already want to ask; **P1** = high value,
system exists, hook is cheap; **P2** = valuable after a prerequisite ships.

### P0 — activation & churn funnel

| Event | Parameters | Hook point |
|---|---|---|
| `session_started` | `entry_point` (cold_launch \| resume) | `AnalyticsServiceFacade` on collection start / `OnAppPaused(false)`. Today only *ends* are explicit; the second sink should not have to reconstruct session starts from UGS built-ins it doesn't receive. |
| `ftue_step_completed` | `step_id` (string), `seconds_since_launch` (int) | `FTUEEventManager` / `TutorialFlowController` (Assets/FTUE/) — a full tutorial state machine that emits **zero** analytics. This is the top of the activation funnel and it is dark. |
| `game_quit_midway` | `game_mode`, `intensity`, `seconds_elapsed` (int) | Facade: game in progress (`_gameInProgress`) + scene exit / `OnClickToMainMenuButton` without `OnMiniGameEnd`. Rage-quit is currently indistinguishable from finishing. |
| `network_disconnected` | `app_state` (string), `in_game` (bool) | `NetworkMonitorData.OnNetworkLost` — the facade already subscribes to this event for gating but records nothing. Mid-match disconnects are a churn driver for a multiplayer game and are invisible. |

### P1 — funnels on systems that already exist

| Event | Parameters | Hook point |
|---|---|---|
| `daily_challenge_started` / `daily_challenge_completed` | `game_mode`, `intensity`, `score` (long), `tier_reached` (int) | `DailyChallengeSystem` — the retired Firebase collectors already defined this shape (`DATA_INVENTORY.md` §2.2). |
| `training_started` / `training_completed` | `game_mode`, `intensity`, `tier_reached` (int) | `TrainingGameProgressSystem`. |
| `checkout_opened` / `checkout_returned` | `product_id` (string), `price_usd` (float) | `IAPManager.OnCheckoutOpened` / `OnReturnedFromCheckout` — events exist, nothing subscribes. The entire (nascent) revenue funnel is unmeasured. |
| `ad_shown` / `ad_completed` / `ad_failed` | see §3 `ad_impression` row | `AdsSystem` static events, already exposed. |
| `vessel_selected` | `vessel_class` (string), `context` (menu \| pregame) | Loadout/vessel-selection panels — balance/preference data currently only inferable from `game_started.vessel_class`. |
| `party_join_failed` | `reason` (string, classified — `NetworkDiagnostics` already classifies these) | `PartyInviteController` catch paths. Social funnel has success events only; failures are invisible. |
| `leaderboard_viewed` | `board_id` (string) | `LeaderboardsMenu.OnScreenEnter`. |

### P2 — after a prerequisite ships

| Event | Parameters | Prerequisite |
|---|---|---|
| `acquisition_source` | `source`, `medium`, `campaign` (strings) — fired once, alongside `game_first_launched` | Android: Play Install Referrer. Steam: Steamworks SDK + launch query params (see `viability-report.md` Option B). |
| `purchase_completed` | `product_id`, `price_usd`, `order_id` | Backend order verification (`IAPManager.ConfirmPendingPurchase` seam — see `Docs/MENU_PROGRESSION_AND_IAP.md` §5). Do **not** fire on `checkout_returned`; unverified. |
| `pilot_recruited` / `vessel_upgraded` | per retired collector shapes | Captain progression re-enabled on UGS (`CAPTAIN_PROGRESS` repo is disabled). |
| `episode_unlocked` / `episode_completed` | `episode_id`, `missions_completed` | Episode progress wiring (`EpisodeProgressCloudData.ReportMissionCompleted` has no callers). |

### Deliberately not proposed

- Per-round / per-turn events, per-crystal pickup events, positional telemetry — event-volume
  cost with no cohort/funnel question attached. Aggregate per-game on `game_completed`
  (`crystals_collected`) instead. `VesselTelemetry` → Cloud Save already covers per-vessel
  lifetime counters without polluting the event stream.
- Frame-rate/perf events — use Unity's own performance reporting rather than the analytics
  stream; revisit only if a specific perf-cohort question arises.

---

## 5. Schema change discipline

1. Never change a parameter's type or meaning — add a new parameter and stop sending the old
   one (UGS Event Manager treats type changes as new parameters anyway; downstream tools do not).
2. Never reuse an event name for a different fact (mirrors the `GameModes` enum-ID rule).
3. Renames from §3 (e.g. `play_again_pressed` → drop): do them **before** the second sink
   ships, so the second system starts with a clean vocabulary and no legacy aliases.
4. This file is the schema registry of record; the UGS Event Manager dashboard must mirror
   it, not the other way around.
