# Branch archive: `claude/analytics-attribution-viability-vwkunw`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-07-15 by Claude
- **Unmerged commits:** 5
- **Forked from:** `d132cf51f` (2026-07-14, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `bba04f12c`
- **Files touched (21):**
  - `Assets/Resources/PostHogConfig.asset`
  - `Assets/Resources/PostHogConfig.asset.meta`
  - `Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs.meta`
  - `Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`
  - `Assets/_Scripts/System/Instrumentation/IAnalyticsSink.cs`
  - `Assets/_Scripts/System/Instrumentation/IAnalyticsSink.cs.meta`
  - `Assets/_Scripts/System/Instrumentation/PostHogSink.cs`
  - `Assets/_Scripts/System/Instrumentation/PostHogSink.cs.meta`
  - `Assets/_Scripts/System/Instrumentation/UgsAnalyticsSink.cs`
  - `Assets/_Scripts/System/Instrumentation/UgsAnalyticsSink.cs.meta`
  - `Docs/Analytics/POSTHOG_SETUP.md`
  - `Docs/Analytics/event-taxonomy.md`
  - `Docs/Analytics/implementation-plan.md`
  - `Docs/Analytics/utm-conventions.md`
  - `Docs/Analytics/viability-report.md`
  - `Tools/Analytics/.gitignore`
  - `Tools/Analytics/README.md`
  - `Tools/Analytics/__pycache__/export_cloud_save.cpython-311.pyc`
  - `Tools/Analytics/export_cloud_save.py`
  - `Tools/Analytics/import_snapshot_to_posthog.py`

### `a09830eb3` — docs(analytics): attribution & aggregation viability report + event taxonomy, UTM conventions, implementation plan

_Claude, 2026-07-14 22:40:40 +0000_

```text
Investigation deliverables (no feature code):
- viability-report.md: audit of the UGS analytics footprint (28 events, single-writer
  facade, anonymous-only identity, no Steamworks/CI/UTM handling), assessment of the
  three Steam attribution bridges, and the aggregation-fix decision. Recommends
  dual-emit to PostHog behind the existing facade; rejects UGS raw export (Snowflake-
  only since Aug 2023, no query API) and Steam launch-param attribution (2024 client
  warning dialog, undocumented install survival).
- event-taxonomy.md: canonical naming convention, required envelope, conformance
  review of all 28 live events, prioritized missing-event list with hook points.
- utm-conventions.md: fixed lowercase UTM vocabulary, per-surface tagged URLs,
  verified Steam UTM report behavior, Play/App Store counterparts.
- implementation-plan.md: phased plan (~8-12 dev-days core), independently shippable
  phases, explicit not-doing list.

External facts (UGS tiers/export, Steam UTM + steam:// behavior, PostHog/Amplitude/
Mixpanel free tiers and Unity SDK status) verified against live sources 2026-07-14;
citations inline.
```

```text
 Docs/Analytics/event-taxonomy.md      | 152 ++++++++++++++++++++
 Docs/Analytics/implementation-plan.md | 129 +++++++++++++++++
 Docs/Analytics/utm-conventions.md     | 137 ++++++++++++++++++
 Docs/Analytics/viability-report.md    | 485 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 4 files changed, 903 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 927 lines)</summary>

```diff
diff --git a/Docs/Analytics/event-taxonomy.md b/Docs/Analytics/event-taxonomy.md
new file mode 100644
index 000000000..db366667c
--- /dev/null
+++ b/Docs/Analytics/event-taxonomy.md
@@ -0,0 +1,152 @@
+# Event Taxonomy — Canonical Schema & Conformance Review
+
+> Companion to `viability-report.md` (2026-07). Defines the naming convention, the required
+> envelope on every event, a conformance review of the 28 events that exist today, and the
+> events we should be firing but aren't. The current-state inventory lives in
+> `DATA_INVENTORY.md`; this doc is the *target* spec.
+
+---
+
+## 1. Naming convention
+
+**Pattern: `noun_verb-in-past-tense`, snake_case, all lowercase.**
+
+- `game_started`, `crystals_earned`, `vessel_unlocked` — correct today, keep.
+- The noun comes first so events sort/group by subsystem in any tool
+  (`game_*`, `crystal*`, `party_*`, `friend_*`).
+- No abbreviations, no camelCase, no spaces. Parameters follow the same rule.
+- Enum-valued string parameters carry the C# enum's `ToString()` — that is already the
+  de-facto convention (`game_mode`, `vessel_class`) and it is fine, but the enum value set
+  must be treated as part of the schema (renaming a `GameModes` member is a breaking
+  analytics change; note this in PR review).
+- One event = one fact. Do not fire a second event to carry an extra property of the same
+  fact (see `crystal_balance_snapshot` below).
+
+**Where the constant lives:** every event name is a `const` in
+`Assets/_Scripts/System/UGSKeys.cs` and is recorded only through
+`AnalyticsServiceFacade` (`Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`).
+No event name string literal may appear anywhere else. This is already true today — keep it
+true; it is what makes a second sink a one-file change.
+
+**UGS constraint:** every event *and every parameter* must also be declared in the UGS
+dashboard Event Manager or the backend silently discards it. A new event is not "shipped"
+until (a) the constant exists in `UGSKeys`, (b) the facade records it, (c) it is declared in
+the dashboard, and (d) it has a row in this doc. Add all four to the PR checklist.
+
+---
+
+## 2. Required envelope (every event)
+
+UGS Analytics automatically attaches its own envelope to every event — user ID, session ID,
+platform, client version, timestamp, country and more are collected by the SDK and do not
+need to be sent as custom parameters. **Do not duplicate them into custom parameters for the
+UGS sink.**
+
+The envelope matters the moment a second sink exists (see `implementation-plan.md` Phase 2):
+a non-UGS sink gets nothing for free, so the facade must stamp the envelope itself. Putting
+it in the sink layer — not in each call site — keeps call sites unchanged.
+
+| Property | Type | Source | Notes |
+|---|---|---|---|
+| `player_id` | string | `AuthenticationService.Instance.PlayerId` (already cached in `AuthenticationDataVariable`) | UGS anonymous player ID. **Known limitation: does not survive reinstall** (anonymous auth, session token only). Per-person identity requires platform sign-in linking — the facade stubs exist (`AuthenticationServiceFacade.SignInWithSteamAsync` etc.) but are not implemented. |
+| `session_id` | string | GUID generated once per app run by the facade | UGS has its own session ID; the second sink needs ours. Generate in the facade constructor. |
+| `install_id` | string | Device GUID persisted in PlayerPrefs on first run | Survives sign-out but not reinstall/device wipe. Bridges pre-auth events to the player. |
+| `build_version` | string | `Application.version` | |
+| `platform` | string | `Application.platform.ToString()` | |
+| `ts_utc` | long | Unix epoch ms at record time | Stamped by the sink, not the caller. |
+
+Optional envelope, stamp when known: `game_mode`, `intensity` (only while a game is active —
+lets any mid-game event segment by mode without each call site passing it).
+
+---
+
+## 3. Current events — conformance review
+
+All 28 events route through `AnalyticsServiceFacade`. Verdicts: **OK** = keep as-is,
+**RENAME** = misnamed against §1, **RESHAPE** = keep name, change parameters,
+**MERGE** = fold into another event.
+
+| Event | Verdict | Issue / change |
+|---|---|---|
+| `game_started` | RESHAPE | Add `match_id` (GUID per game, generated at `game_started`, reused on `game_completed`). Today a session with several games can only pair start/complete events by timestamp ordering — fragile in any downstream tool. |
+| `game_completed` | RESHAPE | Add `match_id`, `score` (long), `crystals_collected` (int). The single most-queried event has **no score on it** — score lives only in Cloud Save bests and leaderboards, which lose every non-best game. |
+| `session_ended` | RESHAPE | Absorb `crystal_balance_snapshot` as a `crystal_balance` parameter (see MERGE below). `reason`/`last_ui_action`/`app_state` are good. |
+| `crystal_balance_snapshot` | MERGE | Fired only ever alongside `session_ended`, as a second event carrying one int. It is a property of session end, not a separate fact. Fold into `session_ended.crystal_balance`, delete the event. |
+| `play_again_pressed` | RENAME | Named after the button, not the fact, and `ui_action` already covers UI clicks. Either drop it (a `game_started` within N seconds of `game_completed` with the same mode is a replay — derivable) or rename `replay_requested`. Prefer drop. |
+| `repeated_game_fail` | RENAME | Verb-noun order inverted and "fail" not past tense. The fact is a loss-streak threshold crossing: rename `loss_streak_reached` with `streak` (int) — or drop the event and derive it downstream from `game_completed.player_won`, which any real query layer can do. Prefer derive-downstream once a second sink exists; keep until then. |
+| `ui_action` | RESHAPE | Fine as a catch-all, but `UserActionType` (`Assets/_Scripts/Data/Enums/UserActionType.cs`) is stale — `ViewArcadeGameDarts` / `ViewArcadeGameRampage` reference games that no longer exist, and nothing covers the live screens (PORT, PROFILE, STORE tabs). Refresh the enum to the current IA. |
+| `ad_impression` | RESHAPE | Fires on `AdLoaded` — that is a *load*, not an impression. `AdsSystem` already exposes `AdShowStart` / `AdShowComplete` / `AdShowFailure`; wire those as `ad_shown` / `ad_completed(completion_state)` / `ad_failed(error)` and fire `ad_impression` on show, not load. The current event over-counts by every preload that is never shown. |
+| `game_first_launched` | OK | PlayerPrefs-guarded once-ever. Note: cannot fire before consent is granted (collection gate), so "first launch" is really "first consented launch" — acceptable, document it. This is also where the future `acquisition_source` parameter lands (see §4). |
+| `menu_ready` | OK | |
+| `freestyle_entered` | OK | |
+| `mode_unlocked` | OK | |
+| `intensity_unlocked` | OK | |
+| `setting_changed` | OK | `value` stringified — acceptable tradeoff for one event covering nine settings. |
+| `crystals_earned` / `crystals_spent` | OK | `source` is free-text from call sites — fix the vocabulary (allowed values: `game_reward`, `daily_challenge`, `quest`, `vessel_purchase`, …) and enforce at the two call sites in `PlayerDataService`. |
+| `crystal_spend_blocked` | OK | Good starvation signal. |
+| `vessel_unlocked` | OK | |
+| `quest_completed` | OK | `quest` is the display title — switch to a stable ID (asset name) so renaming UI copy doesn't fork the funnel. |
+| `share_triggered` | OK | |
+| `friend_request_sent` / `friend_request_received` / `friend_added` | RESHAPE | Carry raw counterpart player IDs (`target_id`, `from_id`, `friend_id`). Analytically we only ever need *that it happened*; the counterpart ID is data-minimization liability under GDPR and useless in aggregate. Drop the ID parameters. |
+| `party_invite_sent` / `party_invite_received` | RESHAPE | Same — drop `target_id` / `host_id`. |
+| `party_joined` | OK | Add `party_size` (int) — the one aggregate-useful fact, available from `HostConnectionDataSO.PartyMembers`. |
+| `minigame_favorited` | OK | |
+| `cloud_save_failed` | OK | |
+
+---
+
+## 4. Events we should be firing and aren't
+
+Priority: **P0** = blocks a funnel/cohort question we already want to ask; **P1** = high value,
+system exists, hook is cheap; **P2** = valuable after a prerequisite ships.
+
+### P0 — activation & churn funnel
+
+| Event | Parameters | Hook point |
+|---|---|---|
+| `session_started` | `entry_point` (cold_launch \| resume) | `AnalyticsServiceFacade` on collection start / `OnAppPaused(false)`. Today only *ends* are explicit; the second sink should not have to reconstruct session starts from UGS built-ins it doesn't receive. |
+| `ftue_step_completed` | `step_id` (string), `seconds_since_launch` (int) | `FTUEEventManager` / `TutorialFlowController` (Assets/FTUE/) — a full tutorial state machine that emits **zero** analytics. This is the top of the activation funnel and it is dark. |
+| `game_quit_midway` | `game_mode`, `intensity`, `seconds_elapsed` (int) | Facade: game in progress (`_gameInProgress`) + scene exit / `OnClickToMainMenuButton` without `OnMiniGameEnd`. Rage-quit is currently indistinguishable from finishing. |
+| `network_disconnected` | `app_state` (string), `in_game` (bool) | `NetworkMonitorData.OnNetworkLost` — the facade already subscribes to this event for gating but records nothing. Mid-match disconnects are a churn driver for a multiplayer game and are invisible. |
+
+### P1 — funnels on systems that already exist
+
+| Event | Parameters | Hook point |
+|---|---|---|
+| `daily_challenge_started` / `daily_challenge_completed` | `game_mode`, `intensity`, `score` (long), `tier_reached` (int) | `DailyChallengeSystem` — the retired Firebase collectors already defined this shape (`DATA_INVENTORY.md` §2.2). |
+| `training_started` / `training_completed` | `game_mode`, `intensity`, `tier_reached` (int) | `TrainingGameProgressSystem`. |
+| `checkout_opened` / `checkout_returned` | `product_id` (string), `price_usd` (float) | `IAPManager.OnCheckoutOpened` / `OnReturnedFromCheckout` — events exist, nothing subscribes. The entire (nascent) revenue funnel is unmeasured. |
+| `ad_shown` / `ad_completed` / `ad_failed` | see §3 `ad_impression` row | `AdsSystem` static events, already exposed. |
+| `vessel_selected` | `vessel_class` (string), `context` (menu \| pregame) | Loadout/vessel-selection panels — balance/preference data currently only inferable from `game_started.vessel_class`. |
+| `party_join_failed` | `reason` (string, classified — `NetworkDiagnostics` already classifies these) | `PartyInviteController` catch paths. Social funnel has success events only; failures are invisible. |
+| `leaderboard_viewed` | `board_id` (string) | `LeaderboardsMenu.OnScreenEnter`. |
+
+### P2 — after a prerequisite ships
+
+| Event | Parameters | Prerequisite |
+|---|---|---|
+| `acquisition_source` | `source`, `medium`, `campaign` (strings) — fired once, alongside `game_first_launched` | Android: Play Install Referrer. Steam: Steamworks SDK + launch query params (see `viability-report.md` Option B). |
+| `purchase_completed` | `product_id`, `price_usd`, `order_id` | Backend order verification (`IAPManager.ConfirmPendingPurchase` seam — see `Docs/MENU_PROGRESSION_AND_IAP.md` §5). Do **not** fire on `checkout_returned`; unverified. |
+| `pilot_recruited` / `vessel_upgraded` | per retired collector shapes | Captain progression re-enabled on UGS (`CAPTAIN_PROGRESS` repo is disabled). |
+| `episode_unlocked` / `episode_completed` | `episode_id`, `missions_completed` | Episode progress wiring (`EpisodeProgressCloudData.ReportMissionCompleted` has no callers). |
+
+### Deliberately not proposed
+
+- Per-round / per-turn events, per-crystal pickup events, positional telemetry — event-volume
+  cost with no cohort/funnel question attached. Aggregate per-game on `game_completed`
+  (`crystals_collected`) instead. `VesselTelemetry` → Cloud Save already covers per-vessel
+  lifetime counters without polluting the event stream.
+- Frame-rate/perf events — use Unity's own performance reporting rather than the analytics
+  stream; revisit only if a specific perf-cohort question arises.
+
+---
+
+## 5. Schema change discipline
```

</details>

### `a1ae7b365` — feat(analytics): pluggable sink layer + PostHog dual-emit + Cloud Save export tool

_Claude, 2026-07-15 16:49:23 +0000_

```text
Implements the viability report's recommended path (aggregation fix, Phases 1-2):

- IAnalyticsSink + AnalyticsEnvelope: AnalyticsServiceFacade now fans every event
  out to a sink list; consent/age/network gating unchanged and upstream of all
  sinks. UgsAnalyticsSink wraps the existing UGS SDK calls (system of record,
  behavior identical).
- PostHogSink: thin main-thread client over PostHog's documented /batch/ capture
  endpoint (batching, pause/quit flush, 429-free public endpoint, 4xx drop vs
  transient retry, consent-revoke queue purge). distinct_id = UGS PlayerId so
  PostHog rows join UGS Analytics and Cloud Save directly. Zero new package
  dependencies (UnityWebRequest + Newtonsoft + UniTask, all already in project).
- PostHogConfigSO + Resources/PostHogConfig.asset: zero-wire config (Resources
  load, no scene changes). Empty API key = sink disabled; per-event exclusion
  list as the free-tier budget lever.
- Tools/Analytics/export_cloud_save.py + README: bulk Cloud Save export per
  Unity's official guidance (Admin API Get Players enumeration + item paging,
  Basic auth service account, 429/Retry-After handling for the 1,000-req/30-min
  cap), JSONL output + DuckDB query recipes over all 12 save keys.
- Docs/Analytics/POSTHOG_SETUP.md: step-by-step for the human-side setup (EU
  project, $0 billing cap, key paste, verification, team invites, HogQL starter
  queries, deletion runbook).
```

```text
 Assets/Resources/PostHogConfig.asset                             |  20 +++
 Assets/Resources/PostHogConfig.asset.meta                        |   8 ++
 Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs             |  54 ++++++++
 Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs.meta        |  11 ++
 Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs | 175 +++++++++++++++++---------
 Assets/_Scripts/System/Instrumentation/IAnalyticsSink.cs         |  71 +++++++++++
 Assets/_Scripts/System/Instrumentation/IAnalyticsSink.cs.meta    |  11 ++
 Assets/_Scripts/System/Instrumentation/PostHogSink.cs            | 196 +++++++++++++++++++++++++++++
 Assets/_Scripts/System/Instrumentation/PostHogSink.cs.meta       |  11 ++
 Assets/_Scripts/System/Instrumentation/UgsAnalyticsSink.cs       |  57 +++++++++
 Assets/_Scripts/System/Instrumentation/UgsAnalyticsSink.cs.meta  |  11 ++
 Docs/Analytics/POSTHOG_SETUP.md                                  | 193 +++++++++++++++++++++++++++++
 Docs/Analytics/implementation-plan.md                            |   9 ++
 Tools/Analytics/README.md                                        | 106 ++++++++++++++++
 Tools/Analytics/__pycache__/export_cloud_save.cpython-311.pyc    | Bin 0 -> 17111 bytes
 Tools/Analytics/export_cloud_save.py                             | 245 +++++++++++++++++++++++++++++++++++++
 16 files changed, 1119 insertions(+), 59 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1268 lines)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs b/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs
new file mode 100644
index 000000000..2ddb3371b
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs
@@ -0,0 +1,54 @@
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    /// <summary>
+    /// Connection + batching config for the PostHog analytics sink
+    /// (<see cref="CosmicShore.Core.PostHogSink"/>). One asset lives at
+    /// <c>Assets/Resources/PostHogConfig.asset</c> and is loaded by name from
+    /// <see cref="CosmicShore.Core.AnalyticsServiceFacade"/> — no scene wiring needed.
+    ///
+    /// Leaving <see cref="projectApiKey"/> empty disables the sink entirely (UGS is
+    /// unaffected), so the asset is safe to ship unconfigured. Setup guide:
+    /// Docs/Analytics/POSTHOG_SETUP.md.
+    /// </summary>
+    [CreateAssetMenu(
+        fileName = "PostHogConfig",
+        menuName = "ScriptableObjects/Analytics/PostHog Config")]
+    public class PostHogConfigSO : ScriptableObject
+    {
+        [Header("Connection")]
+        [Tooltip("PostHog → Settings → Project → 'Project API key' (starts with phc_). This is the public write-only key — safe to ship in the client. Leave EMPTY to disable the PostHog sink.")]
+        [SerializeField] string projectApiKey = "";
+
+        [Tooltip("Ingestion host. EU cloud: https://eu.i.posthog.com — US cloud: https://us.i.posthog.com. Must match the region the PostHog project was created in.")]
+        [SerializeField] string host = "https://eu.i.posthog.com";
+
+        [Header("Batching")]
+        [Tooltip("Send queued events as soon as this many have accumulated.")]
+        [SerializeField] int maxBatchSize = 30;
+
+        [Tooltip("Also send on the first event recorded after this many seconds since the last successful send.")]
+        [SerializeField] float flushIntervalSeconds = 30f;
+
+        [Tooltip("Hard cap on locally queued events while offline or failing. Oldest events are dropped beyond this.")]
+        [SerializeField] int maxQueueSize = 500;
+
+        [Header("Filtering")]
+        [Tooltip("Event names never forwarded to PostHog — the free-tier budget lever for chatty events (e.g. ui_action). UGS still receives them.")]
+        [SerializeField] List<string> excludedEvents = new();
+
+        public string ProjectApiKey => projectApiKey == null ? string.Empty : projectApiKey.Trim();
+        public string Host => string.IsNullOrWhiteSpace(host) ? "https://eu.i.posthog.com" : host.Trim().TrimEnd('/');
+        public int MaxBatchSize => Mathf.Max(1, maxBatchSize);
+        public float FlushIntervalSeconds => Mathf.Max(1f, flushIntervalSeconds);
+        public int MaxQueueSize => Mathf.Max(MaxBatchSize, maxQueueSize);
+
+        /// <summary>True when the sink should be created at all.</summary>
+        public bool IsUsable => !string.IsNullOrEmpty(ProjectApiKey);
+
+        public bool IsExcluded(string eventName) =>
+            excludedEvents != null && excludedEvents.Contains(eventName);
+    }
+}
diff --git a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
index 2e37aadd4..fff4229c7 100644
--- a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
+++ b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
@@ -5,23 +5,23 @@ using CosmicShore.Gameplay;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.UI;
 using CosmicShore.Utility;
-using Unity.Services.Analytics;
 using Unity.Services.Core;
 using UnityEngine;
 
 namespace CosmicShore.Core
 {
     /// <summary>
-    /// Single writer for all UGS Analytics custom events (UGS-only pipeline —
-    /// the Firebase analytics path is retired).
+    /// Single writer for all analytics custom events. Owns the collection lifecycle
+    /// (starts after UGS sign-in, gated by consent, age gate, and network availability),
+    /// records events through one choke point, and flushes only on app pause/quit.
     ///
-    /// Owns the collection lifecycle: starts data collection after UGS sign-in
-    /// (gated by consent and network availability), records events through one
-    /// choke point, and flushes only on app pause/quit — the SDK batches
-    /// everything else automatically.
-    ///
-    /// Custom events and their parameters must also be declared in the UGS
-    /// dashboard Event Manager or the backend silently discards them.
+    /// Destinations are <see cref="IAnalyticsSink"/>s: <see cref="UgsAnalyticsSink"/>
+    /// (system of record — events/parameters must be declared in the UGS dashboard Event
+    /// Manager or the backend silently discards them) and, when
+    /// <c>Resources/PostHogConfig.asset</c> carries a project API key,
+    /// <see cref="PostHogSink"/> (cohort/funnel/SQL exploration — see
+    /// Docs/Analytics/POSTHOG_SETUP.md). Every event goes to every sink; consent gating
+    /// lives here, upstream of all sinks.
     /// </summary>
     public class AnalyticsServiceFacade
     {
@@ -45,6 +45,9 @@ namespace CosmicShore.Core
         /// <summary>PlayerPrefs key (device-local) guarding the once-ever first-launch event.</summary>
         const string FirstLaunchPrefKey = "AnalyticsFirstLaunchRecorded";
 
+        /// <summary>PlayerPrefs key for the per-install GUID stamped on every event's envelope.</summary>
+        const string InstallIdPrefKey = "AnalyticsInstallId";
+
         /// <summary>Consecutive losses before <c>repeated_game_fail</c> fires.</summary>
         const int RepeatedFailThreshold = 3;
 
@@ -58,6 +61,12 @@ namespace CosmicShore.Core
         readonly HostConnectionDataSO _hostConnectionData;
         readonly bool _allowLog;
 
+        // Destinations. Index 0 is always UGS (system of record); PostHog is appended
+        // when Resources/PostHogConfig.asset is configured with a project API key.
+        readonly List<IAnalyticsSink> _sinks = new();
+        readonly string _sessionId;
+        readonly string _installId;
+
         bool _collecting;
         bool _isConnected = true;
         bool _signedIn;
@@ -111,6 +120,16 @@ namespace CosmicShore.Core
             _hostConnectionData = hostConnectionData;
             _allowLog = allowLog;
 
+            _sessionId = Guid.NewGuid().ToString("N");
+            _installId = GetOrCreateInstallId();
+
+            _sinks.Add(new UgsAnalyticsSink(Log));
+            var postHogConfig = Resources.Load<PostHogConfigSO>("PostHogConfig");
+            if (postHogConfig != null && postHogConfig.IsUsable)
+                _sinks.Add(new PostHogSink(postHogConfig, Log));
+            else
+                Log("PostHog sink disabled (Resources/PostHogConfig has no project API key).");
+
             AuthData.OnSignedIn.OnRaised += HandleSignedIn;
             NetworkData.OnNetworkFound.OnRaised += HandleNetworkFound;
             NetworkData.OnNetworkLost.OnRaised += HandleNetworkLost;
@@ -201,15 +220,18 @@ namespace CosmicShore.Core
         public void RequestDataDeletion()
         {
             SetConsent(false);
-            try
+            foreach (var sink in _sinks)
             {
-                AnalyticsService.Instance.RequestDataDeletion();
-                Log("Data deletion requested.");
-            }
-            catch (Exception e)
-            {
```

</details>

### `a6742d774` — chore(analytics): drop committed __pycache__, ignore pyc artifacts

_Claude, 2026-07-15 16:50:03 +0000_

```text
 Tools/Analytics/.gitignore                                    |   2 ++
 Tools/Analytics/__pycache__/export_cloud_save.cpython-311.pyc | Bin 17111 -> 0 bytes
 2 files changed, 2 insertions(+)
```

### `9cefb27dc` — feat(analytics): PostHog snapshot importer for existing Cloud Save data

_Claude, 2026-07-15 16:54:43 +0000_

```text
Reads items.jsonl from export_cloud_save.py and sends one cloud_save_snapshot
event per player to PostHog /batch/ (distinct_id = UGS PlayerId), flattening
profile/stats/vessel/progression/hangar fields as event properties and person
properties ($set) so the pre-existing player base is immediately visible in
People, cohorts, and SQL. Dry-run mode prints payloads without sending.
Server-side only — no game session or consent dialog involved.
```

```text
 Tools/Analytics/README.md                     |  47 ++++++++++++-
 Tools/Analytics/import_snapshot_to_posthog.py | 211 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 257 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 279 lines)</summary>

```diff
diff --git a/Tools/Analytics/README.md b/Tools/Analytics/README.md
index 1a2ca6800..fa910a486 100644
--- a/Tools/Analytics/README.md
+++ b/Tools/Analytics/README.md
@@ -1,4 +1,14 @@
-# Cloud Save Export Tool
+# Cloud Save Export & Snapshot Import Tools
+
+Two standalone scripts (Python 3.8+, standard library only, no game session or consent
+flow involved — these read server-side data with a service-account key):
+
+| Script | Purpose |
+|---|---|
+| `export_cloud_save.py` | Dump every player's Cloud Save data to JSONL (analyze locally with DuckDB) |
+| `import_snapshot_to_posthog.py` | Push the export into PostHog as one `cloud_save_snapshot` event + person properties per player, so the historical player base is visible in PostHog's UI/SQL immediately |
+
+## Export
 
 `export_cloud_save.py` dumps **every player's Cloud Save data** (all 12 keys —
 `player_profile`, `PLAYER_STATS_PROFILE`, `VESSEL_STATS`, `GAME_MODE_PROGRESSION`, …) to
@@ -97,6 +107,41 @@ WHERE p.key = 'player_profile';
 The key inventory and each key's JSON shape are documented in
 `Docs/Analytics/DATA_INVENTORY.md` §1.
 
+## Importing the snapshot into PostHog
+
+PostHog only sees events from the moment the in-game sink (or this importer) sends them —
+it cannot read UGS retroactively. Cloud Save also isn't an event stream: it's each
+player's *current state*. So the honest way to get "the data that's already there" into
+PostHog is a **snapshot import**: one `cloud_save_snapshot` event per player, timestamped
+at import time, with the useful fields flattened as event properties and mirrored to
+person properties (`$set`). Because `distinct_id` is the UGS PlayerId — the same ID the
+in-game sink uses — future live events attach to these same person records.
+
+```bash
+export POSTHOG_API_KEY=phc_...   # PostHog → Settings → Project → Project API key
+
+# Inspect what would be sent (prints the first 3 payloads, sends nothing)
+python3 import_snapshot_to_posthog.py --items export_2026_07_15/items.jsonl --dry-run
+
+# Send it (default host is EU; pass --host https://us.i.posthog.com for a US project)
+python3 import_snapshot_to_posthog.py --items export_2026_07_15/items.jsonl
+```
+
+Fields imported per player (when present in their saves): `name`, `avatar_id`, `xp`,
+`crystal_balance`, `first_seen`, `last_login`, `rewards_unlocked`, `total_games`,
+`favorite_vessel`, `unlocked_modes` (+count), `max_intensity_unlocked`,
+`recorded_play_count`, `vessels_unlocked`, `selected_vessel`, `keys_present`.
+
+After import: **People** shows every player with those properties; cohorts can filter on
+them (e.g. `total_games > 20 AND last_login < -30d` = lapsed veterans); SQL sees them as
+`properties.*` on the `cloud_save_snapshot` event. Re-running after a fresh export writes
+a newer snapshot per player — use the latest event per `distinct_id` in queries. A few
+thousand snapshot events is negligible against the 1M/month free tier.
+
+Only what the snapshot *flattens* goes to PostHog; the complete raw saves stay in the
+JSONL/DuckDB export. Deep per-key analysis (high scores per intensity, per-vessel
+counters, etc.) belongs in DuckDB; PostHog gets the cohort-able summary.
+
 ## Notes
 
 - Only the **default** access class is exported by default — all Cosmic Shore keys live
diff --git a/Tools/Analytics/import_snapshot_to_posthog.py b/Tools/Analytics/import_snapshot_to_posthog.py
new file mode 100644
index 000000000..fdbddbf1b
--- /dev/null
+++ b/Tools/Analytics/import_snapshot_to_posthog.py
@@ -0,0 +1,211 @@
+#!/usr/bin/env python3
+"""
+One-time import of exported Cloud Save data into PostHog as per-player snapshots.
+
+Reads the items.jsonl produced by export_cloud_save.py, merges each player's saved
+keys, and sends ONE `cloud_save_snapshot` event per player to PostHog's /batch/
+capture endpoint — with the useful fields flattened as event properties AND set as
+person properties ($set), so People, cohorts, filters, and SQL all work over the
+historical player base immediately. distinct_id is the UGS PlayerId, identical to
+what the in-game PostHog sink sends, so future live events land on the same persons.
+
+This is a snapshot (timestamped now), not fabricated history: Cloud Save holds
+current state, not an event log, so there is nothing meaningful to backdate.
+Re-running after a fresh export just writes a newer snapshot event per player.
+
+Requirements: Python 3.8+, standard library only.
+
+Usage:
+  export POSTHOG_API_KEY=phc_...
+  python3 import_snapshot_to_posthog.py --items export_2026_07_15/items.jsonl
+  python3 import_snapshot_to_posthog.py --items .../items.jsonl --dry-run   # inspect first
+"""
+
+import argparse
+import json
+import os
+import sys
+import time
+import urllib.error
+import urllib.request
+
+DOTNET_EPOCH_TICKS = 621_355_968_000_000_000  # .NET ticks at 1970-01-01 (100ns units)
+
+
+def parse_value(raw):
+    """items.jsonl stores each saved object as a JSON string; tolerate raw objects too."""
+    if isinstance(raw, str):
+        try:
+            return json.loads(raw)
+        except (json.JSONDecodeError, ValueError):
+            return None
+    return raw if isinstance(raw, dict) else None
+
+
+def ticks_to_iso(ticks):
+    try:
+        ticks = int(ticks)
+        if ticks <= DOTNET_EPOCH_TICKS:
+            return None
+        return iso_from_unix((ticks - DOTNET_EPOCH_TICKS) / 10_000_000)
+    except (TypeError, ValueError, OverflowError):
+        return None
+
+
+def epoch_ms_to_iso(ms):
+    try:
+        ms = int(ms)
+        if ms <= 0:
+            return None
+        return iso_from_unix(ms / 1000)
+    except (TypeError, ValueError, OverflowError):
+        return None
+
+
+def iso_from_unix(seconds):
+    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(seconds))
+
+
+def build_snapshot(saves: dict) -> dict:
+    """Flatten one player's merged Cloud Save keys into snapshot properties.
+    Every field is optional — players may hold any subset of keys."""
+    props = {"snapshot_source": "cloud_save_export", "keys_present": sorted(saves.keys())}
+
+    profile = saves.get("player_profile") or {}
+    if profile:
+        props["name"] = profile.get("displayName")
+        props["avatar_id"] = profile.get("avatarId")
+        props["xp"] = profile.get("xp")
+        props["crystal_balance"] = profile.get("crystalBalance")
+        props["first_seen"] = epoch_ms_to_iso(profile.get("firstSeenUtc"))
+        rewards = profile.get("unlockedRewardIds")
+        props["rewards_unlocked"] = len(rewards) if isinstance(rewards, list) else 0
```

</details>

### `bba04f12c` — feat(analytics): Windows-first export/import UX

_Claude, 2026-07-15 17:18:08 +0000_

```text
- README rewritten for a Windows cmd user: exact copy-paste blocks (set env vars,
  py launcher), expected output for every step, key->property mapping table from
  UGSKeys.cs, troubleshooting matrix (auth 401/403, empty export, PostHog 401,
  region mismatch, missing Python).
- export_cloud_save.py: accepts UGS_SECRET as well as UGS_SECRET_KEY; utf-8 file IO.
- import_snapshot_to_posthog.py: --items defaults to cloud_save_export/items.jsonl
  (zero-argument happy path), friendly missing-file error, dry-run shows 5 payloads
  + batch count, per-batch failure tally with safe-rerun exit instead of aborting
  on transient 5xx.
```

```text
 Tools/Analytics/README.md                     | 283 +++++++++++++++++++++++++++++++++++---------------------
 Tools/Analytics/export_cloud_save.py          |  12 ++-
 Tools/Analytics/import_snapshot_to_posthog.py |  59 +++++++++---
 3 files changed, 229 insertions(+), 125 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 486 lines)</summary>

```diff
diff --git a/Tools/Analytics/README.md b/Tools/Analytics/README.md
index fa910a486..8a0d55686 100644
--- a/Tools/Analytics/README.md
+++ b/Tools/Analytics/README.md
@@ -1,92 +1,195 @@
-# Cloud Save Export & Snapshot Import Tools
+# Cloud Save Export & PostHog Snapshot Import
 
-Two standalone scripts (Python 3.8+, standard library only, no game session or consent
-flow involved — these read server-side data with a service-account key):
+Two scripts that get **everything already stored in UGS Cloud Save** out of Unity's
+backend and into places you can actually analyze it:
 
-| Script | Purpose |
+| Script | What it does |
 |---|---|
-| `export_cloud_save.py` | Dump every player's Cloud Save data to JSONL (analyze locally with DuckDB) |
-| `import_snapshot_to_posthog.py` | Push the export into PostHog as one `cloud_save_snapshot` event + person properties per player, so the historical player base is visible in PostHog's UI/SQL immediately |
+| `export_cloud_save.py` | Downloads every player's Cloud Save data to files on your PC |
+| `import_snapshot_to_posthog.py` | Pushes one snapshot per player into PostHog so the whole player base shows up in People / cohorts / SQL |
 
-## Export
+Plain Python, **standard library only — nothing to pip install**. These talk to Unity's
+servers directly with an admin key: **no Unity editor, no game session, no consent
+dialog involved.**
 
-`export_cloud_save.py` dumps **every player's Cloud Save data** (all 12 keys —
-`player_profile`, `PLAYER_STATS_PROFILE`, `VESSEL_STATS`, `GAME_MODE_PROGRESSION`, …) to
-JSONL files you can query locally with DuckDB, open in a spreadsheet, or join against
-PostHog exports. Python 3.8+, standard library only — no pip installs.
+---
 
-It follows Unity's official bulk-export guidance (support article
-[47770905934740](https://support.unity.com/hc/en-us/articles/47770905934740), verified
-2026-07): enumerate players with data via the Cloud Save Admin API's *Get Players*
-endpoint, then page each player's items. The APIs are free to call; the binding
-constraint is the admin rate limit (**1,000 requests per 30 minutes** — the tool waits
-and resumes automatically on 429).
+## 0. One-time setup (~5 minutes of clicking)
 
-## One-time setup (~5 minutes)
+**A. UGS service account** (the read-only admin credential):
 
-1. **Create a service account**: [Unity Cloud](https://cloud.unity.com) →
-   **Administration → Service Accounts → New**. Name it e.g. `cloudsave-export`.
-2. **Give it read access**: on the service account → *Project roles → Add project role* →
-   select the Cosmic Shore project → role **Cloud Save Viewer** (read-only is all it needs).
-3. **Create a key**: on the service account → *Keys → Create key*. Copy the **Key ID** and
-   **Secret Key** (secret is shown once). Treat these like passwords — never commit them,
-   never put them in the game client.
-4. **Find the project ID**: Unity Cloud → project → Settings (a UUID, same one in
-   `ProjectSettings/UnityConnectSettings.asset`).
+1. Open [cloud.unity.com](https://cloud.unity.com) → **Administration → Service Accounts**
+   → **New**. Name it `cloudsave-export`.
+2. On the account → **Project roles → Add project role** → pick the Cosmic Shore project →
+   role **Cloud Save Viewer**.
+3. On the account → **Keys → Create key** → copy the **Key ID** and the **Secret Key**
+   (the secret is shown only once). Treat both like passwords — never commit them, never
+   put them in the game.
+4. Also note the **Project ID** (a UUID): Unity Cloud → the project → Settings.
 
-## Usage
+**B. PostHog key**: PostHog → **Settings → Project → Project API key** (starts with
+`phc_`). Same key the game config uses.
 
-```bash
-export UGS_KEY_ID=xxxxxxxx
-export UGS_SECRET_KEY=xxxxxxxx
+**C. Python on Windows** (skip if `py --version` already works): install from
+[python.org/downloads](https://www.python.org/downloads/) — on the first installer
+screen **tick "Add python.exe to PATH"**. Any Python 3.8+ is fine.
 
-# See the project's environments (usually just 'production')
-python3 export_cloud_save.py --project-id <PROJECT_ID> --list-environments
+---
 
-# Full export of every player's default-class data
-python3 export_cloud_save.py --project-id <PROJECT_ID> --environment production --out export_$(date +%Y_%m_%d)
+## 1. Windows: the whole flow, copy-paste
 
-# Just specific players (comma list, or @file with one ID per line)
-python3 export_cloud_save.py --project-id <PROJECT_ID> --players 7bKp...,9dQz...
+Open **Command Prompt** (Start menu → type `cmd` → Enter), then paste these blocks one at
+a time. Replace only the three `YOUR_...` placeholders (no quotes needed unless the value
+contains spaces — these never do).
+
+**Go to the tools folder** (adjust the path to where the repo is on your disk):
+
+```bat
+cd /d C:\Projects\Cosmic-Shore\Tools\Analytics
 ```
 
-Output:
+**Set the credentials for this window** (`set` lasts until you close the window; nothing
+is saved to disk):
 
-- `players.jsonl` — one line per player: `player_id` + per-access-class `{numKeys, totalSize}` metadata.
-- `items.jsonl` — one line per stored item: `player_id`, `access_class`, `key`,
-  `value` (the saved object as a JSON string), `write_lock`, `modified`, `created`.
+```bat
+set UGS_KEY_ID=YOUR_KEY_ID
+set UGS_SECRET=YOUR_SECRET_KEY
+set UGS_PROJECT_ID=YOUR_PROJECT_ID
+```
 
-**How long it takes:** requests ≈ `players/100` (enumeration) + ~1 per player per 20 keys
-(item pages). With the 1,000-per-30-min cap that's roughly **900 players per half-hour**;
-small player counts finish in seconds. Player IDs come back alphabetically, so players who
-join mid-export can be missed in one pass — re-run for a clean cut (the official caveat).
+**Sanity check — list the project's environments** (also proves the credentials work):
 
-## Querying with DuckDB
+```bat
+py export_cloud_save.py --list-environments
+```
 
-[DuckDB](https://duckdb.org) is a single binary; `duckdb` in the export folder gives you a
-SQL shell over the files:
+Expected output — one line per environment:
 
-```sql
--- Everything, flattened
-SELECT * FROM read_json_auto('items.jsonl') LIMIT 10;
+```
+9f6e...-...-...   production
+```
+
+**Export everything** (uses the `production` environment by default; add
+`--environment <name>` if your data lives elsewhere):
+
+```bat
+py export_cloud_save.py
+```
+
+Expected output:
+
+```
+  50 players, 214 items so far...
+  100 players, 431 items so far...
+Done. 137 players, 583 items.
+  cloud_save_export\players.jsonl
+  cloud_save_export\items.jsonl
+```
+
+If you see `rate limited (429): waiting 60s...` lines, that's normal — Unity's admin API
+allows 1,000 requests per 30 minutes and the script waits and resumes by itself. A few
+thousand players can take an hour or more; just leave the window open.
+
+**Dry-run the PostHog import** (prints the first 5 payloads it *would* send — makes zero
+network calls, safe to run as many times as you like):
```

</details>
