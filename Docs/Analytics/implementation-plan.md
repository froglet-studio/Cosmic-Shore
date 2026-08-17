# Analytics & Attribution — Implementation Plan

> **Provenance.** Written on the `claude/analytics-attribution-viability-vwkunw` branch
> (PR #592) as an analysis/design pass, and salvaged onto bleeding-edge after the sink
> layer shipped separately. The *analysis* here stands. Where it describes implementation
> shape (interface signatures, class names, field lists), **`Docs/Analytics/DATA_ARCHITECTURE.md`
> is the authority** - the shipped `IAnalyticsSink` differs in detail (`RecordEvent` +
> `Identify` + `StartCollection`/`StopCollection`, person properties, a disk-persisted queue).

> Companion to `viability-report.md` (2026-07-14). Effort in developer-days assumes one
> developer familiar with the codebase. Every phase is independently shippable and useful
> even if later phases never happen. Builds are manual (no CI), so each phase's estimate
> includes its own device-build verification pass.

## Phase 0 — Make the data we already collect true (≈ 4–5 days)

Ship this regardless of everything else. It fixes silent data loss and closes the funnel
gaps in the *existing* UGS pipeline.

| # | Task | Effort | Notes |
|---|---|---|---|
| 0.1 | **UGS Event Manager audit.** Declare all 28 events + parameters from `viability-report.md` §1.2 in the dashboard Event Manager; check the Event Browser for invalid/rejected events; confirm events are arriving at all from shipped builds. | 0.5–1d (dashboard) | UGS silently discards undeclared events. Until this is done we do not actually know our analytics works. Also confirm current MAU/event volume while in the dashboard (feeds Phase 2 budget check). |
| 0.2 | **Taxonomy conformance fixes** (`event-taxonomy.md` §3): add `match_id` + `score` + `crystals_collected` to game events; fold `crystal_balance_snapshot` into `session_ended`; drop `play_again_pressed`; move `ad_impression` to ad-shown (wire `AdShowStart/Complete/Failure`); strip counterpart player IDs from social events; switch `quest_completed` to stable IDs; refresh `UserActionType`. | 1.5–2d | All changes inside the facade + `UGSKeys` + 3 call sites. Do renames **now**, before any second sink exists, so PostHog starts with a clean vocabulary. |
| 0.3 | **P0 missing events** (`event-taxonomy.md` §4): `session_started`, `ftue_step_completed`, `game_quit_midway`, `network_disconnected`. | 1.5–2d | FTUE is the top of the activation funnel and currently emits nothing. |
| 0.4 | **Adopt `utm-conventions.md`.** Retag the links we control today (site, Discord, itch cross-links, newsletter template); create the shared campaign-calendar sheet (campaign name, start/stop, channel, spend) that Option A correlation depends on. | 0.5d (no code) | Wishlist attribution starts working the day the Steam page exists — the vocabulary must exist first. |

**Exit criteria:** Event Browser shows zero invalid events; a full play session produces the
expected event sequence in the UGS dashboard; links on controlled surfaces carry tags.

## Phase 1 — Sink seam (≈ 0.5–1 day)

> **Status: SHIPPED (2026-07-15).** `IAnalyticsSink` + `UgsAnalyticsSink` extracted;
> facade fans out to a sink list; envelope stamped in the sink layer.

Pure refactor, no behavior change, shippable alone.

- Add `IAnalyticsSink { void Record(string name, IDictionary<string,object> parameters); void Flush(); void SetCollecting(bool); }`
  in `_Scripts/System/Instrumentation/`.
- Move the `CustomEvent` construction from `AnalyticsServiceFacade.RecordEvent` into
  `UgsAnalyticsSink`; the facade iterates a sink list. Consent/age/network gating stays in
  the facade (upstream of all sinks).
- Envelope stamping (`event-taxonomy.md` §2: `session_id`, `install_id`, `build_version`,
  `platform`, `ts_utc`, `player_id`) implemented in the sink layer; `UgsAnalyticsSink`
  skips the fields UGS auto-collects, other sinks take all of them.

**Exit criteria:** behavior identical to before (same events land in UGS); one new
interface + one moved class; unit test for envelope stamping.

## Phase 2 — PostHog dual-emit (≈ 2.5–4 days)

> **Status: CODE SHIPPED (2026-07-15)** — via the thin HTTP `/batch/` sink (the §3.1.5
> fallback shape) rather than the young official SDK: zero new package dependencies, all
> platforms, fully owned. `PostHogAnalyticsSink` + `PostHogConfigSO` +
> `Assets/Resources/PostHogConfig.asset`. Remaining human steps (project creation, API
> key paste, device verification, privacy-policy entry): `POSTHOG_SETUP.md`.

- Create the PostHog project on the **EU (Frankfurt)** instance; set **billing limit $0**
  (hard-caps us at the free 1M events/month — overage drops, never bills).
- Add the official SDK: UPM git URL `https://github.com/PostHog/posthog-unity.git?path=com.posthog.unity`
  (requires Unity 2021.3+ / .NET Standard 2.1 — we're on Unity 6000.3, fine).
- Implement `PostHogAnalyticsSink`: `distinct_id` = UGS `PlayerId` (rows join across systems);
  lazy-init on first consented record (keeps it out of the boot path and away from the UGS
  init sequence); wire `AnalyticsServiceFacade.SetConsent` → `PostHog.OptIn()/OptOut()` so
  the SDK's persisted state agrees with ours.
- Per-sink filter hook: predicate in `PostHogAnalyticsSink` to exclude chatty events (`ui_action`,
  `setting_changed`) if the dashboard volume check from 0.1 says the 1M/month budget is
  tight. Default: send everything.
- Privacy: add PostHog (EU) as a processor in the privacy policy
  (`Docs/Legal/PRIVACY_POLICY_TEMPLATE.md` has the placeholder section); write the deletion
  runbook — on a delete-my-data request, in addition to the existing UGS call, delete the
  person (with events) in the PostHog UI. Client code must never hold the private API key.
- **Acceptance test (the SDK is 7 months old):** IL2CPP Android device build + iOS build;
  verify capture, opt-out persistence, offline queue/flush on pause, no boot-time
  regression. **Fallback if it fails:** replace the SDK with a ~200-line
  `UnityWebRequest`-based sink posting to PostHog `/batch/` (documented, unauthenticated
  POST with `api_key` in body, no rate limits) — +1–2 days, same vendor, nothing else
  changes.

**Exit criteria:** the same event stream visible in both UGS and PostHog with joinable IDs;
opt-out verified end-to-end on device; privacy policy updated.

## Phase 3 — Standing dashboards (≈ 1–2 days, no code)

Build once in PostHog, then it's self-serve:

1. Activation funnel: `game_first_launched → menu_ready → freestyle_entered → game_started → game_completed` (+ `ftue_step_completed` once 0.3 ships).
2. Retention curves (D1/D7/D30) cohorted by first-seen week — and by `acquisition_source` once any bridge exists.
3. Mode/intensity popularity and completion rates (`game_started` vs `game_completed` vs `game_quit_midway` by `game_mode`).
4. Churn forensics: `session_ended.last_ui_action` + `loss_streak` distribution before churn.
5. Economy: `crystals_earned/spent/spend_blocked` flows, `vessel_unlocked` timing.
6. Social funnel: invite sent → received → `party_joined`; `party_join_failed` rate.

**Exit criteria:** the two motivating questions are answerable in the UI: cohort retention
comparison (by date window today, by source when available) and cross-player funnels.

## Phase 3a — Android Play Install Referrer (conditional, ≈ 1–2 days)

**Trigger:** first real campaign that drives Google Play installs (not before the game is
even on Play).
Read the referrer once on first launch (Play Install Referrer library via a small plugin or
existing UPM wrapper), parse `utm_*`, cache in PlayerPrefs, emit `acquisition_source`
alongside `game_first_launched`, and stamp it as a PostHog person property so every cohort
chart can segment by it. This is the per-install attribution rehearsal that Steam cannot
give us (see report §2 Option B) with zero policy risk.

## Phase 4 — Steam launch analytics package (conditional, ≈ 5–8 days, when the Steam build is real)

Belongs to the Steam-launch project, listed here so it isn't reinvented:

- Steamworks.NET integration + app boot init (2–3d).
- **Steam sign-in linked to UGS Authentication** (`LinkWithSteamAsync` stub →
  real session-ticket flow, 2–3d). This is the identity fix: persistent, reinstall-proof
  player identity on PC, which makes cohorts person-level instead of install-level.
- Store page ships with `utm-conventions.md` links from day one (wishlist attribution
  accrues pre-launch).
- Date-window correlation (Option A) as the attribution method: Steam UTM CSV + campaign
  calendar + PostHog cohort curves. Read the report §2 Option A for the statistical
  guardrails (when cohort-quality reads are valid vs. volume-only reads).
- Explicitly **not** in this package: launch-query-param attribution (Option B — rejected,
  report §2), unless Valve has removed the args warning dialog and documented
  install-survival by then.

## What we are NOT doing, and why

| Not doing | Why |
|---|---|
| **Option B (Steam launch query params)** | Valve's client shows a non-dismissible warning dialog on `steam://` launches with args (since ~Mar 2024); param survival through install→first-launch is undocumented; covers only channels Steam UTM already reports aggregate-side; requires Steamworks work that has no other analytics payoff. Re-evaluate only if Valve changes the UX. |
| **Option C (coupon/key redemption backend)** | No backend exists anywhere in the product; 12–20 build days plus permanent ops (issuance, support, fraud); zero current paid spend to justify it. Revisit at sustained ≥~$5k/mo paid UA across multiple channels. If revisited: UGS Cloud Code + Cloud Save is the least-new-infra shape. |
| **Path 2 (UGS export → DuckDB)** | The flat-file export it presumes was discontinued Aug 2023; today's only raw export is a Snowflake share requiring our own Snowflake account (violates the no-warehouse constraint) plus a pipeline to babysit — the data-engineer-shaped burden we're explicitly avoiding. |
| **Bespoke aggregation over the UGS API** | No query API exists (ingestion-only REST; dashboard SQL capped at 2,000 rows, human-only). Structurally impossible, independent of being a bad idea. |
| **Amplitude / Mixpanel instead of PostHog** | Both gate behavioral cohorts off the free tier — the exact capability gap we're fixing. Amplitude's Unity plugin is dormant, mobile-only, wrapping deprecated native SDKs. Mixpanel's SDK is healthy, but paying from day one to get cohorts loses to PostHog's free tier. Swappable later behind `IAnalyticsSink` in ~a day if this call proves wrong. |
| **A third sink / CDP / event bus** | Two sinks behind one facade is the complexity ceiling for a team this size. |
| **High-frequency telemetry events** (per-pickup, positional, per-frame perf) | Event-count budget and no attached decision. Per-vessel lifetime counters already flow through `VesselTelemetry` → Cloud Save. |
| **Waiting for perfect identity before shipping cohorts** | Install-level cohorts (status quo) are good enough for every Phase 3 dashboard; person-level identity arrives with Steam sign-in (Phase 4) — don't block on it. |

## Sequence & total

```
Phase 0 (4–5d) ─→ Phase 1 (0.5–1d) ─→ Phase 2 (2.5–4d) ─→ Phase 3 (1–2d)
                                                     └─ conditional: 3a (1–2d), 4 (5–8d)
```

Core commitment (Phases 0–3): **≈ 8–12 developer-days**, after which the ongoing burden is
"look at dashboards" — no pipelines, no servers, no data engineer. Hard cost ceiling: $0
(UGS free ≤50k MAU; PostHog free ≤1M events/month with a $0 billing cap).
