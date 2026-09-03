# Analytics & Attribution Viability Report

> **Provenance.** Written on the `claude/analytics-attribution-viability-vwkunw` branch
> (PR #592) as an analysis/design pass, and salvaged onto bleeding-edge after the sink
> layer shipped separately. The *analysis* here stands. Where it describes implementation
> shape (interface signatures, class names, field lists), **`Docs/Analytics/DATA_ARCHITECTURE.md`
> is the authority** - the shipped `IAnalyticsSink` differs in detail (`RecordEvent` +
> `Identify` + `StartCollection`/`StopCollection`, person properties, a disk-persisted queue).

> Investigation date: 2026-07-14. Companions: `event-taxonomy.md` (target schema),
> `utm-conventions.md` (link-tagging vocabulary), `implementation-plan.md` (phased plan).
> Current-state data inventory: `DATA_INVENTORY.md`. All pricing/policy claims below were
> verified against live sources on 2026-07-14; citations inline. Where something could not
> be verified it is flagged, not guessed (§4).

## Recommendation (TL;DR)

**Aggregation gap → dual-emit to PostHog behind the existing facade.** Extract an
`IAnalyticsSink` interface inside `AnalyticsServiceFacade` (one file — the abstraction layer
already exists and is enforced), keep UGS as sink #1, add PostHog as sink #2 via its
official Unity SDK. PostHog's free tier (1M events/month, funnels + cohorts + SQL included,
$0 billing cap = hard-capped free, EU residency, GDPR deletion API) directly answers the
cohort/funnel questions the UGS dashboard can't. Estimated 3–5 dev-days including build
verification, near-zero ongoing maintenance. The alternative — UGS raw export → DuckDB — is
**dead on arrival**: Unity discontinued file-based raw export in Aug 2023; the only raw
export today is a Snowflake Secure Data Share requiring our own Snowflake account, and
there is no REST query API to build on even if we wanted to (we don't, and you were right
not to want to).

**Attribution gap → mostly a policy problem today, not a code problem.** The game is not on
Steam yet (no Steamworks SDK, no app ID — see §0), so the Steam↔UGS bridge is launch prep.
Adopt the UTM vocabulary now (`utm-conventions.md` — wishlist attribution starts the day the
store page exists), plan on **Option A (date-window correlation against Steam's aggregate
UTM report)** as the launch-time answer, and **skip Options B and C**: B because Valve's
2024 client hardening puts a scary, non-dismissible warning dialog on `steam://` launches
with arguments and param survival across install→first-launch is undocumented; C because
there is no backend, no paid spend to justify one, and per-user Steam attribution is
structurally unavailable from Valve anyway. The one cheap per-install bridge actually
available to this project is the **Google Play Install Referrer** on Android — 1–2 days,
zero policy risk — worth doing whenever a real campaign drives Play installs.

What would change this: PostHog's young Unity SDK failing an IL2CPP device build (fallback:
a ~200-line HTTP sink against PostHog's no-rate-limit capture endpoint — same vendor, same
plan); sustained event volume blowing past 1M/month (fix: per-sink filtering of chatty
events, not a plan change); Valve shipping a real attribution API (no sign of it); or
monthly paid UA spend reaching a level (~$5k+/mo sustained) where channel-level ROI
decisions justify Option C's build+run cost.

---

## 0. Premise corrections (read first)

Three assumptions in the investigation brief do not match the repository. They materially
change the option assessments, so they lead the report:

1. **The game is not shipping on Steam today.** There is no Steamworks SDK (no
   Steamworks.NET, no Facepunch.Steamworks, no Unity Steam package — the only "Steam" hits in
   `Assets/` are inside the inert PlayFab SDK). There is no Steam build target configuration;
   the only build profile on disk is a Linux one, and `ProjectSettings.asset` carries mobile
   bundle IDs (`com.FrogletGames.TailGlider` for Android/iPhone/Standalone). Current public
   distribution per `README.md` is **itch.io + TestFlight**. Steam/PC is aspirational — it
   appears in `Docs/Legal/PRIVACY_POLICY_TEMPLATE.md` and `Docs/MENU_PROGRESSION_AND_IAP.md`
   as a planned platform. Consequence: there is no Steam acquisition data to bridge *yet*;
   the Steam-side options below are launch preparation, not fixes for a live gap.
2. **Networking is Unity Netcode for GameObjects only** (`com.unity.netcode.gameobjects`
   2.5.0). No Photon/Fusion anywhere in the project. (No impact on the analysis; corrected
   for the record.)
3. **There is no build pipeline.** No `.github/` directory, no CI config of any kind in the
   repo, no Unity Cloud Build configuration. Builds are manual (confirmed by `CLAUDE.md`).
   Consequence: "where an SDK addition needs CI changes" is moot — an SDK addition is a
   `Packages/manifest.json` change plus manual build/test time, nothing else.

One correction in our favor: the brief assumed analytics calls might be scattered through
gameplay code. They are not — see §1.3. The abstraction layer the brief asks us to propose
**already exists and is enforced**, which makes the second-sink option far cheaper than the
brief feared.

---

## 1. Phase 1 — Audit of what exists

### 1.1 UGS initialization & services in use

**Initialization path** (all in the Bootstrap scene):

```
AppManager.Start()                                  _Scripts/System/AppManager.cs:152
  └─ AuthenticationServiceFacade.StartAuthentication()        AppManager.cs:541
      ├─ UnityServices.InitializeAsync()            AuthenticationServiceFacade.cs:85
      ├─ AuthenticationService.SignInAnonymouslyAsync()       :119
      └─ OnSignedIn (SOAP) ──► AnalyticsServiceFacade.StartCollectionIfReady()
                               (consent + age gate + network + init gated,
                                AnalyticsServiceFacade.cs:233)
```

`AppManager` is the Reflex DI root; `AnalyticsServiceFacade` is constructed as a lazy DI
singleton (`AppManager.cs:384`) and wires ~25 event subscriptions in its constructor.

**Installed UGS packages** (`Packages/manifest.json`):

| Package | Version | Actually used? | Evidence |
|---|---|---|---|
| `com.unity.services.analytics` | 6.2.1 | **Yes** | `AnalyticsServiceFacade` (single writer) |
| `com.unity.services.core` | 1.16.0 | **Yes** | `UnityServices.InitializeAsync()`; Authentication (anonymous) |
| `com.unity.services.cloudsave` | 3.4.0 | **Yes** | `UGSCloudSaveProvider`, 12 keyed repositories (`DATA_INVENTORY.md` §1) |
| `com.unity.services.leaderboards` | 2.3.3 | **Yes** | `WeeklyChallengeLeaderboardService` (`AddPlayerScoreAsync` / `GetScoresAsync` against ONE board, id on `WeeklyChallengeCatalogSO.leaderboardId`). The per-mode path in `UGSStatsManager` was retired. |
| `com.unity.services.friends` | 1.1.1 | **Yes** | `FriendsServiceFacade` (relationships + presence) |
| `com.unity.services.multiplayer` | 1.1.8 | **Yes** | Sessions + Relay: `PartySessionService.cs:184/239`, `PresenceLobbyService` (lobby-only presence session), `MultiplayerSetup` |
| `com.unity.purchasing` | 4.12.2 | **Installed, unused** | `IAPManager` is a web-checkout flow via `Application.OpenURL` — no store SDK calls (`Docs/MENU_PROGRESSION_AND_IAP.md` §5) |
| `com.unity.ads` | 4.12.0 | **Yes (mobile only)** | `AdsSystem.cs` — init compiled only for `UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID` |

**Not installed:** Remote Config, Matchmaker, Vivox, Cloud Code. Legacy PlayFab SDK is
present under `Assets/PlayFabSDK/` but inert (auth deprecated, economy stubbed). Firebase
was fully removed (2026-06-11 decision, `DATA_INVENTORY.md` §2).

Engine: Unity **6000.3.17f1**, mobile-first (Adaptive Performance + Samsung provider, Unity
Ads mobile gate, touch input strategies).

### 1.2 Every analytics event currently fired

All 28 events flow through `AnalyticsServiceFacade.RecordEvent` (F = facade,
`_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`). Event-name constants live in
`_Scripts/System/UGSKeys.cs`. Parameter types are the C# types handed to `CustomEvent.Add`.

| Event | Trigger (file:line) | Parameters (type) |
|---|---|---|
| `game_started` | First `OnMiniGameTurnStarted` per game — F:338 | `game_mode` (string), `intensity` (int), `vessel_class` (string), `player_count` (int), `ai_count` (int), `is_multiplayer` (bool) |
| `game_completed` | `OnMiniGameEnd` — F:348 | all of the above + `duration_seconds` (int), `player_won` (bool, omitted when unresolvable) |
| `repeated_game_fail` | 3rd+ consecutive loss, inside game-end handler — F:368 | `game_mode` (string), `fail_count` (int) |
| `session_ended` | App pause or quit (once per foreground stretch) — F:448 | `reason` (string: pause\|quit), `last_ui_action` (string), `app_state` (string) |
| `crystal_balance_snapshot` | Fired alongside `session_ended` — F:465 | `balance` (int) |
| `ui_action` | `UserActionSystem.OnUserActionCompleted` — F:487 | `action_type` (string), `action_label` (string), `action_value` (int) |
| `ad_impression` | `AdsSystem.AdLoaded` (ad *loaded*, not shown) — F:498 | — |
| `play_again_pressed` | Scoreboard Play Again → `UGSStatsManager.cs:217` | — |
| `game_first_launched` | Once ever (PlayerPrefs guard), on first collection start — F:640 | — |
| `menu_ready` | Menu interactive — `MainMenuController.cs:192` | `is_first` (bool), `seconds_since_launch` (int) |
| `freestyle_entered` | Lava-lamp → freestyle transition — F:576 | `is_first` (bool), `seconds_since_launch` (int) |
| `mode_unlocked` | Quest claim unlocks next mode — `GameModeProgressionService.cs:236` | `game_mode` (string) |
| `intensity_unlocked` | Tier 3/4 unlock — `GameModeProgressionService.cs:549,568` | `game_mode` (string), `intensity` (int) |
| `setting_changed` | Any of 9 `GameSetting.OnChange*` statics — F:134-142 | `setting` (string), `value` (string), `seconds_since_launch` (int) |
| `crystals_earned` | `PlayerDataService.cs:321` (`AddCrystals`) | `amount` (int), `source` (string), `balance` (int) |
| `crystals_spent` | `PlayerDataService.cs:339` (`TrySpendCrystals` success) | `amount` (int), `source` (string), `balance` (int) |
| `crystal_spend_blocked` | `PlayerDataService.cs:331` (spend refused) | `amount` (int), `item` (string), `balance` (int) |
| `vessel_unlocked` | Hangar purchase — `HangarVesselDetailView.cs:243` | `vessel` (string), `cost` (int), `balance` (int) |
| `quest_completed` | `QuestSystem.cs:41` | `quest` (string, display title), `shard_value` (int) |
| `share_triggered` | `SnsShare.cs:26` | `game_mode` (string) |
| `friend_request_sent` | `FriendsDataSO.OnFriendRequestSent` — F:595 | `target_id` (string) |
| `friend_request_received` | `FriendsDataSO.OnFriendRequestReceived` — F:601 | `from_id` (string) |
| `friend_added` | `FriendsDataSO.OnFriendAdded` — F:607 | `friend_id` (string) |
| `party_invite_sent` | `HostConnectionDataSO.OnInviteSent` — F:613 | `target_id` (string) |
| `party_invite_received` | `HostConnectionDataSO.OnInviteReceived` — F:619 | `host_id` (string) |
| `party_joined` | `HostConnectionDataSO.OnPartyJoinCompleted` — F:625 | — |
| `minigame_favorited` | `FavoriteSystem.OnFavoriteChanged` — F:627 | `game_mode` (string), `favorited` (bool) |
| `cloud_save_failed` | `UGSCloudSaveProvider.OnSaveFailed` — F:634 | `key` (string) |

Notes and defects found:

- **No orphaned constants**: every name in `UGSKeys` has a live firing path.
- **Dashboard declaration is unverifiable from the repo and is a silent failure mode.**
  UGS Analytics is schema-first: custom events must be declared in the dashboard Event
  Manager, and incoming events are validated against that definition — mismatches are
  rejected as invalid ([custom-event docs](https://docs.unity.com/en-us/analytics/events/custom-event)).
  `DATA_INVENTORY.md` lists "declare all events in Event Manager" as *remaining work* —
  until someone confirms in the dashboard (Event Browser shows invalid events over the last
  48h), we cannot know which of these 28 events are actually landing.
  **Action: audit the Event Manager against this table before anything else.**
- Collection is **opt-in** (consent + 13+ age gate); events before consent are dropped, so
  `game_first_launched` really means "first *consented* launch."
- Misnamed/misshaped events (full review in `event-taxonomy.md` §3): `ad_impression` fires
  on ad *load*, over-counting impressions; `crystal_balance_snapshot` is a parameter
  masquerading as an event; `play_again_pressed` duplicates `ui_action`; the social events
  carry counterpart player IDs that aggregate analysis never needs (GDPR data-minimization
  liability); `game_started`/`game_completed` lack a `match_id` correlator; and
  **`game_completed` carries no score**.
- Dark systems (zero events): FTUE/tutorial, daily challenge, training games, IAP checkout
  funnel, mid-game quits, network disconnects. List with hook points: `event-taxonomy.md` §4.

### 1.3 Abstraction layer — exists, enforced, one seam

`AnalyticsServiceFacade` is the **only** first-party file that touches
`Unity.Services.Analytics` (verified by search — every other reference is a facade
injection). Gameplay/UI code either raises SOAP events the facade subscribes to, or calls
typed `Record*` methods on the injected facade. All 28 events funnel through one method,
`RecordEvent(string, IDictionary<string, object>)` (F:285).

**Consequence for Phase 3:** adding a second destination is a change to *one file* — extract
an `IAnalyticsSink` behind `RecordEvent` and register a second sink. Zero gameplay-code
edits, zero call-site churn. The expensive scenario the brief worried about does not exist.

### 1.4 Player identity

- **Anonymous UGS Authentication only.** `AuthenticationServiceFacade.StartAuthentication()`
  → `SignInAnonymouslyAsync()`. The UGS `PlayerId` is the shared key across Analytics, Cloud
  Save, Leaderboards, Friends, and Sessions (the facade deliberately sets no
  `ExternalUserId` override — F:246).
- **Does not survive reinstall.** Anonymous identity persists via a locally cached session
  token; uninstalling deletes it and the next install mints a new `PlayerId`. Cross-device
  play is likewise a fresh identity.
- **No platform auth is wired.** `AuthenticationServiceFacade.cs:196-204` contains
  `SignInWithSteamAsync` / `SignInWithGoogleAsync` / `SignInWithAppleAsync` /
  `LinkWithSteamAsync` etc. — all stubs returning `Task.CompletedTask`. Steam auth is not
  connected to UGS in any form.
- Install-time cohorting exists: `player_profile.firstSeenUtc` (Unix ms, stamped once at
  profile creation) in Cloud Save.
- Implication for attribution: until platform sign-in ships, every cohort is an *install*
  cohort, not a *person* cohort. Reinstalls contaminate retention measures regardless of
  which bridging option is chosen.

### 1.5 Steamworks SDK

Absent entirely (§0.1). Nothing is wired: no achievements, no stats, no rich presence, no
Steam Input, no overlay handling. Any Option-B work starts with "integrate Steamworks.NET
and create the Steam app" — a Steam-launch work package, not an analytics tweak.

### 1.6 Build pipeline

None (§0.3). Manual builds from the editor; one Linux build profile asset
(`Assets/Settings/Build Profiles/`), Android/iOS Burst AOT configs in `ProjectSettings/`.
An added SDK affects only `Packages/manifest.json` (UPM) or `Assets/Plugins/` and the manual
build/test loop.

### 1.7 UTM / campaign / referral handling

None anywhere in first-party code (searched `utm`, `campaign`, `referral`, `acquisition`
case-insensitively across `Assets/_Scripts`). The instrumentation reference explicitly parks
`store_page_view` / UTM attribution as "storefront-side tooling, not a client event"
(`Docs/Analytics/INSTRUMENTATION_DATA.html`). There is also no deep-link / app-link handling
on mobile, and no Play Install Referrer integration.

### 1.8 Consent & privacy (baseline for the GDPR questions in Phase 3)

Already built and live-wired, opt-in by design:

- `PrivacyConsentController` (`_Scripts/UI/Privacy/`): first-run COPPA age gate (neutral
  birth-year picker) → consent dialog. Decline/under-13 keeps playing with analytics off.
- `AnalyticsPrivacySettingsController`: Settings toggle (revoke/grant later) + "delete my
  data" → `AnalyticsServiceFacade.RequestDataDeletion()` → UGS erasure API.
- The facade gates collection on: consent granted AND age-eligible AND signed in AND network
  up (`StartCollectionIfReady`, F:233). Under-13 is a hard never-collect.

Any second sink inherits these gates for free if it lives behind the facade — it additionally
needs its own erasure path and a privacy-policy processor entry (§3.1).

---

## 2. Phase 2 — The three attribution bridging options

Framing fact verified with Valve's own docs: **Steam UTM analytics is aggregate-only by
design** — "The report never includes Steam ID's or any other info about individual users"
([UTM Analytics doc](https://partner.steamgames.com/doc/marketing/utm_analytics)), and no
Steamworks Web API exposes acquisition data at all. There is no Valve-side path to a
per-user `UTM → SteamID` join, ever. The only per-user options are things *we* build
(Options B and C). Judge all three options against that ceiling.

What the Steam UTM report does give (all verified 2026-07-14, same source):

- Visits (total / trusted / tracked), wishlist adds, purchases, and activations per UTM
  combination, attributed within a **72-hour window** from the click, CSV-downloadable.
- Conversions only attribute for users **logged into Steam in that browser**; practitioner
  measurements put tracked traffic at **≲10% of clicks** ([Gamesight](https://docs.gamesight.io/docs/steam-utm-analytics),
  [HowToMarketAGame](https://howtomarketagame.com/2021/04/14/how-to-use-steams-utm-feature-to-track-the-number-of-wishlists-and-sales-your-marketing-is-generating/)).
  Treat absolute numbers as a fixed-bias sample; compare channels against each other, not
  against totals.
- Low-volume UTM combinations are suppressed below an **undisclosed** threshold — another
  reason the vocabulary in `utm-conventions.md` is deliberately small.
- No pixels / no GA: Valve ended Google Analytics support July 2023 and supports no
  third-party trackers on store pages ([google_analytics doc](https://partner.steamgames.com/doc/marketing/google_analytics)).

### Option A — date-window correlation. **Recommended at launch.**

No code. Tag every controlled link (vocabulary in `utm-conventions.md`), read Steam's UTM
CSV and UGS/PostHog cohort curves side by side, attribute by campaign window.

- **Contamination model.** A window's install cohort mixes paid and organic. If paid is a
  fraction *p* of window installs, an underlying retention difference Δ between paid and
  organic shows up attenuated to *p*·Δ, and the sample size needed to detect it grows as
  1/*p*². Concretely: detecting a 5-point D7 retention difference (20% vs 25%) at
  conventional power needs ≈1,100 installs per cohort at *p*=1; at *p*=0.5 that becomes
  ≈4,400 window installs; at *p*=0.25 it is ≈17,600 — hopeless. **Rule of thumb: date
  correlation answers retention-quality questions only when the campaign dominates its
  window (paid ≥ ~50% of installs), and the window contains ≥ ~1,000 installs.**
- **Volume questions are much cheaper.** "Did the campaign move installs at all" is
  detectable when the daily lift exceeds ~2×√(baseline dailies) — at a 50-install/day
  organic baseline, a ~15-install/day lift is visible. This works at the scale the game is
  at now.
- **Minimum-DAU answer:** below roughly **100 installs/day during a campaign window**, stop
  trying to read cohort *quality* from date correlation; you can still read cohort *size*.
  Steam-side wishlist/visit deltas from the UTM report remain readable at any scale (subject
  to the row-suppression threshold).
- **Hygiene that makes A work:** never overlap two campaigns; log campaign start/stop dates
  in a shared sheet (folding festival/streamer spikes into the log matters more than any
  tooling); compare like-for-like weekdays.

### Option B — Steam launch query params. **Skip. Re-evaluate only if Valve softens the UX.**

Verified current state ([ISteamApps docs](https://partner.steamgames.com/doc/api/ISteamApps)):

- `GetLaunchQueryParam` still exists and needs **no allowlist** — only `@`-prefixed
  (internal) and `_`-prefixed (Steam-reserved) parameter names are restricted. The
  `steam://run/<appid>/?source=x` query form and `GetLaunchCommandLine` (with the
  "Use launch command line" Installation setting) both remain documented.
- **The blocker is the client UX**: since ~March 2024 the Steam client shows a blocking
  warning dialog on `steam://` launches carrying arguments — listing the args with "if you
  did not request this launch … select cancel" wording — and it cannot be disabled
  (community-verified; Valve never published the patch note — flagged in §4). A browser
  external-protocol prompt stacks on top. That is a hostile funnel for exactly the casual
  clicks we'd be measuring.
- **Unverified whether params survive install→first-launch** (`steam://run` installs if
  necessary, but nothing documents arg persistence through the install flow). For an
  *acquisition* funnel — where the user by definition doesn't have the game installed —
  this is the load-bearing behavior, and it is undocumented. Assume unreliable.
- Coverage ceiling is small anyway: only links we control (Discord, site, newsletter —
  surfaces Steam UTM already covers aggregate-side), never store-page discovery, paid ads,
  or search.
- Cost side: requires Steamworks.NET integration (not present), a Steam app ID (doesn't
  exist), and the boot hook. For completeness: the hook would be a read at
  `AppManager.Awake()`/`Start()` (Steamworks init must precede it), cached and emitted as
  `acquisition_source` alongside `game_first_launched` in
  `AnalyticsServiceFacade.MaybeRecordFirstLaunch()` (F:640).
- **Verdict:** the per-user data B yields is a small, biased sliver of controlled-channel
  traffic, bought with a scary dialog in the acquisition funnel and a dependency on
  undocumented install behavior. Not worth building at launch. The Android
  **Play Install Referrer** delivers the same shape of data (`utm_*` into the client, per
  install) with none of these problems — if we want a per-install bridge to rehearse before
  Steam, build it there (`implementation-plan.md` Phase 3a).

### Option C — coupon/key redemption. **Skip until real paid spend exists.**

- **Backend reality: there is none.** No server component exists anywhere. The web-checkout
  IAP flow explicitly defers entitlement verification "once a backend exists"
  (`Docs/MENU_PROGRESSION_AND_IAP.md` §5). PlayFab (which has native coupon primitives) is
  legacy and being retired — reviving it for this would cut against the UGS-only decision.
  This is greenfield: the least-infrastructure implementation is UGS Cloud Code (serverless,
  not currently installed) + Cloud Save for the code table, plus issuance tooling on
  froglet.games.
- **Honest work estimate** (code table + issuance endpoint + redemption endpoint + in-game
  redemption UI + rate limiting + abuse controls + ops): **12–20 dev-days to build**, plus
  ongoing cost that is the real problem — code batches per campaign, support tickets for
  failed redemptions, fraud review. This is the "needs a fraction of a data engineer"
  category the constraints exclude.
- **Is it justified at our spend level? No.** There is zero paid acquisition today (no ad
  platform config, no campaign tooling, no marketing site instrumentation, and the game
  pre-dates its own Steam page). Option C's payoff — a true per-user UTM→player join — only
  prices in when channel-level budget reallocation decisions ride on it, i.e. sustained
  meaningful paid UA (ballpark ≥$5k/month across ≥2 channels). Revisit then; the design
  sketch above goes in the backlog, not the roadmap.

---

## 3. Phase 3 — The aggregation fix

### 3.1 Path 1 — second analytics SDK (**recommended: PostHog**)

**Free-tier comparison, verified against live pricing pages 2026-07-14:**

| | PostHog | Mixpanel | Amplitude |
|---|---|---|---|
| Free events/mo | **1M** ([pricing](https://posthog.com/pricing)) | 1M (was 20M — tier collapsed) ([pricing](https://mixpanel.com/pricing)) | 2M ([pricing](https://amplitude.com/pricing)) |
| Funnels on free | Yes | Yes | Yes ("basic analytics") |
| **Cohorts on free** | **Yes** ("almost all product features" free; group analytics is the only relevant paid gate) | **No/limited** — behavioral cohorts are a Growth-plan feature ([docs](https://docs.mixpanel.com/docs/pricing)) | **No** — behavioral cohorts are a Plus-plan bullet |
| SQL on free | Yes (HogQL, free in public beta) | No | No (Query add-on, Growth+ only, effectively discontinued) |
| Retention (free) | 1 year | 2 years (platform default) | ~1 year (medium confidence) |
| Overage safety | **$0 billing limit = hard-capped free; overage events dropped, never billed** | capped, upgrade prompt | capped |
| EU residency | Frankfurt, same price, self-serve | project-creation-time choice, free-plan gating unconfirmed | signup-time choice, free-plan gating unconfirmed |
| GDPR deletion | Self-serve API + UI ([docs](https://posthog.com/docs/privacy/data-deletion)) | OAuth deletion API | Deletion API |
| **Unity SDK** | **Official**, new (Dec 2025), v1.1.0 Jul 2026, UPM, Unity 2021.3+, **Win/Mac/Linux/iOS/Android/WebGL**, built-in `OptOut/OptIn` ([repo](https://github.com/PostHog/posthog-unity), [docs](https://posthog.com/docs/libraries/unity)) | Official, pure C#, active (v3.6.0 Jun 2026, "reduce IL2CPP bloat"), UPM; caveats: no Simplified ID Merge, PlayerPrefs persistence, open WebGL storage bug ([repo](https://github.com/mixpanel/mixpanel-unity)) | Official but **dormant** — v2.8 May 2024, wrapper over *legacy* native iOS/Android SDKs, **no desktop/standalone support**, open EU-endpoint bug on iOS ([repo](https://github.com/amplitude/unity-plugin), [docs](https://amplitude.com/docs/sdks/analytics/unity)) |
| HTTP fallback | `/batch/` endpoint, api_key in body, **no rate limits on capture**, <20MB/batch | `/track` (2,000 events/batch) | HTTP V2 / Batch APIs |

**Why PostHog:** the aggregation gap *is* cohorts + funnels, and Mixpanel/Amplitude both
gate cohorts off their free tiers — using them means paying immediately or not fixing the
gap. PostHog has everything needed on free, the only current-generation official Unity SDK
that covers desktop (which the Steam plan eventually needs), a hard $0 spend cap, EU
residency, and a self-serve deletion API. The SDK's youth (first release Dec 2025) is the
one real risk; mitigation below.

**Least-friction integration for this codebase** (details in `implementation-plan.md`):

1. Extract `IAnalyticsSink { Record(name, params); Flush(); SetEnabled(bool); }` inside the
   facade; wrap the existing UGS calls as `UgsAnalyticsSink`. All 28 events + future ones
   flow through both sinks with **zero call-site changes** (§1.3).
2. Add `PostHogAnalyticsSink` using the official UPM package. The facade's existing gates (consent,
   age, sign-in) apply before any sink sees an event; additionally call
   `PostHog.OptOut()`/`OptIn()` from `SetConsent` so the SDK's own persistence agrees.
3. Stamp the envelope (`event-taxonomy.md` §2) in the sink layer; use the UGS `PlayerId` as
   the PostHog `distinct_id` so UGS and PostHog rows join trivially.
4. Init ordering: PostHog init is independent of `UnityServices.InitializeAsync()` — no
   interaction with the UGS init sequence. Initialize it lazily on first consented record to
   keep it out of the boot path entirely.
5. **Risk mitigation for the young SDK:** acceptance test = one IL2CPP Android device build
   + one iOS build, verify capture + opt-out + no startup cost regressions. PostHog docs
   don't state IL2CPP compatibility explicitly (flagged §4) — but iOS support implies it,
   and the fallback is cheap: a ~200-line `UnityWebRequest` sink against PostHog's `/batch/`
   capture endpoint (POST-only, api_key in body, no rate limits), which is vendor-supported
   for exactly this. Same vendor, same dashboards, no plan change.
6. **Event budget:** 28 event types at current DAU lands well under 1M/month. The chatty
   ones (`ui_action`, `setting_changed`) can be filtered *per sink* if volume ever
   threatens the cap — a one-line predicate in `PostHogAnalyticsSink`, invisible to UGS. Set the
   PostHog billing limit to $0 so the worst case is dropped overage events, never a bill.

**GDPR/consent:** the flow that exists (opt-in consent + COPPA age gate + settings opt-out +
delete-my-data) already satisfies the collection side for a second processor — the second
sink simply lives behind the same gates. Required additions: (a) list PostHog as a
processor in the privacy policy (template already stubbed at
`Docs/Legal/PRIVACY_POLICY_TEMPLATE.md`); (b) choose the EU (Frankfurt) instance at project
creation; (c) extend the delete-my-data path — the client cannot hold PostHog's private API
key, so deletion is an ops runbook step (PostHog UI: person → delete with events) or later a
Cloud Code relay; volumes will be tiny. (d) UGS SDK note: Unity 6.2+ deprecates
`StartDataCollection` in favor of `EndUserConsent.SetConsentState` — the facade will need
that migration on the next analytics-package bump regardless of this project.

### 3.2 Path 2 — UGS Data Export → DuckDB (**not viable as specced**)

The brief's premise ("Data Export on a paid tier") is out of date. Verified 2026-07-14:

- The file-based Raw Data Export API belonged to *legacy* Unity Analytics and was
  **discontinued 2023-08-31** ([Unity announcement](https://discussions.unity.com/t/discontinuation-of-legacy-analytics-raw-data-export-api-remote-settings/925226)).
- Today's only raw export is **"Data Access": a Snowflake Secure Data Share** — Unity
  replicates org event data into a Snowflake share; you connect **your own Snowflake
  account** and pay Snowflake compute ([Data Access docs](https://docs.unity.com/en-us/analytics/data-access/data-access)).
  No S3/GCS/Azure delivery, no CSV/JSON/Parquet drops, no BigQuery. Dashboard exports are
  PNG/CSV widgets only. (Tier gating for Data Access is undocumented — flagged §4 — but
  moot: the Snowflake requirement alone violates our "no cloud warehouse" constraint.)
- There is **no REST query API** — the Analytics REST API is ingestion-only, and Unity staff
  point external-access questions at Snowflake ([staff answer](https://discussions.unity.com/t/how-to-get-ugs-analytics-data-externally/933903)).
  The dashboard SQL Data Explorer is human-only and capped at 2,000 result rows.
- Pipeline sketch, for the record, since the brief asked: it would be *Snowflake share →
  scheduled `COPY INTO` Parquet in Snowflake stage → download → DuckDB*, with model layers
  `raw_events → sessions (sessionize on 30-min gaps) → player_day (one row per player per
  day: games, wins, crystals, minutes) → cohorts (keyed on first-seen date / acquisition
  tag) → aggregates`. Roughly 5–8 dev-days one-time **plus a Snowflake account, plus a
  recurring pipeline to babysit, plus every query surface hand-built** — funnels and cohort
  curves that PostHog renders out of the box. One-time vs ongoing is the wrong framing for
  a pipeline: the ongoing part (schema drift when events change, export failures, "is the
  data current?" doubt) is precisely the data-engineer-shaped burden the constraints
  exclude.
- UGS free tier itself is healthy and stays: 50k MAU free, no event cap (fair use 500
  events/MAU), 13-month raw retention, no deprecation signals
  ([pricing](https://docs.unity.com/en-us/analytics/pricing-and-billing/pricing),
  [fair usage](https://docs.unity.com/en-us/analytics/pricing-and-billing/fair-usage-limits)).
  Keep it as system of record; it's just not queryable enough alone.

**On the "bespoke aggregation layer over the UGS API" alternative the brief warned against:**
agreed, and it is not even possible — there is no query API to build it on. The instinct
behind the prior was right.

### 3.3 The tradeoff being made

Choosing Path 1 (PostHog dual-emit) trades a **second data copy and a second vendor
relationship** for **zero pipeline ownership and out-of-the-box cohort/funnel/SQL UI**. The
double-emission drift risk (events diverging between sinks) is structurally eliminated by
the single-choke-point facade — both sinks see the identical dictionary. The vendor risk is
bounded by the facade too: `PostHogAnalyticsSink` is swappable for any HTTP-capable vendor in a day
without touching a call site. What would reopen Path 2: Unity shipping file-based export to
customer-owned storage (watch the release notes), or the team acquiring a standing Snowflake
account for other reasons.

---

## 4. What could not be determined (and why)

| Unknown | Why | Impact |
|---|---|---|
| Whether the 28 events are declared in the UGS Event Manager dashboard | Dashboard-side state, not in repo; no API to check | Any undeclared event is being silently discarded today. First action of `implementation-plan.md` Phase 0. |
| Actual current DAU / event volume | No dashboard access from this environment | Free-tier headroom estimates in §3.1 use conservative assumptions; confirm against the UGS MAU chart before Phase 2 of the plan. |
| Steam UTM row-suppression threshold value | Valve deliberately doesn't publish it | Favors a small UTM vocabulary (done). |
| Steam UTM case/variant normalization | Undocumented by Valve either way | `utm-conventions.md` §1 assumes case-sensitivity (industry norm) and enforces lowercase — safe under either behavior. |
| Whether `steam://run` args survive install→first-launch | Undocumented; community reports only cover the already-installed case | Load-bearing for Option B; treated as unreliable → B skipped. |
| Official patch note for the March-2024 steam:// warning dialog | Community threads only; Valve staff locked threads confirming intent, no patch note found | Confidence in the dialog's existence is high (multiple reports); exact wording/scope (query-param form vs `//args` form) unverified. |
| PostHog Unity SDK IL2CPP statement | Not documented by PostHog | Mitigated by device-build acceptance test + HTTP-sink fallback (§3.1.5). |
| UGS "Data Access" tier gating and 365-day SQL query window | Docs silent on gating; SQL window figure appeared only in unfetchable support content | Moot for the recommendation (Path 2 rejected on the Snowflake requirement). |
| Whether Mixpanel/Amplitude free tiers include EU residency | Official pages don't state plan gating | Only mattered for the runners-up. |
| Whether any UGS events currently reach the dashboard at all | Depends on the two unknowns above (declaration + consent-flow deployment in shipped builds) | The Phase 0 dashboard audit resolves this in an hour of dashboard work. |

## 5. Source index (key citations)

- UGS: [MAU pricing](https://docs.unity.com/en-us/analytics/pricing-and-billing/mau-based-pricing) ·
  [fair usage](https://docs.unity.com/en-us/analytics/pricing-and-billing/fair-usage-limits) ·
  [Data Access (Snowflake)](https://docs.unity.com/en-us/analytics/data-access/data-access) ·
  [legacy export discontinuation](https://discussions.unity.com/t/discontinuation-of-legacy-analytics-raw-data-export-api-remote-settings/925226) ·
  [custom events / Event Manager](https://docs.unity.com/en-us/analytics/events/custom-event) ·
  [SQL Data Explorer](https://docs.unity.com/ugs/en-us/manual/analytics/manual/sql-data-explorer)
- Steam: [UTM Analytics](https://partner.steamgames.com/doc/marketing/utm_analytics) ·
  [ISteamApps (GetLaunchQueryParam / GetLaunchCommandLine)](https://partner.steamgames.com/doc/api/ISteamApps) ·
  [GA support ended](https://partner.steamgames.com/doc/marketing/google_analytics) ·
  [Gamesight on tracked-share](https://docs.gamesight.io/docs/steam-utm-analytics)
- PostHog: [pricing](https://posthog.com/pricing) · [Unity SDK](https://posthog.com/docs/libraries/unity) ·
  [posthog-unity repo](https://github.com/PostHog/posthog-unity) · [capture API](https://posthog.com/docs/api/capture) ·
  [data deletion](https://posthog.com/docs/privacy/data-deletion) · [GDPR](https://posthog.com/docs/privacy/gdpr-compliance)
- Mixpanel: [pricing](https://mixpanel.com/pricing) · [plan gates](https://docs.mixpanel.com/docs/pricing) ·
  [unity SDK](https://github.com/mixpanel/mixpanel-unity) · [EU residency](https://docs.mixpanel.com/docs/privacy/eu-residency)
- Amplitude: [pricing](https://amplitude.com/pricing) · [unity plugin](https://github.com/amplitude/unity-plugin) ·
  [unity docs (no desktop)](https://amplitude.com/docs/sdks/analytics/unity)
