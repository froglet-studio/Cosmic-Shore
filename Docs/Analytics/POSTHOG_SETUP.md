# PostHog Setup — Step-by-Step

> What shipped in code: `AnalyticsServiceFacade` now fans every event out to pluggable
> sinks (`IAnalyticsSink`). Sink #1 is UGS (unchanged, still the system of record). Sink #2
> is `PostHogAnalyticsSink` — a thin client over PostHog's `/batch/` capture endpoint, enabled the
> moment `Assets/Resources/PostHogConfig.asset` has a project API key. No scene or prefab
> wiring is required. Consent/age gating is unchanged and sits upstream of both sinks.
>
> This doc is the checklist of the parts that happen **outside the repo**: creating the
> PostHog project, pasting the key, verifying data, and how anyone on the team gets
> insights + SQL out of it.

---

## 1. Create the PostHog project (~10 minutes, one person, once)

1. Go to **[https://eu.posthog.com/signup](https://eu.posthog.com/signup)** — this creates
   the org on the **EU cloud (Frankfurt)**. Use the EU region: same price as US, keeps
   GDPR simple, and the region cannot be changed later. No credit card required.
2. Create an organization (e.g. *Froglet*) and a project (e.g. *Cosmic Shore*). When asked
   about the install platform, pick anything / skip — we use the HTTP API, not a snippet.
3. **Set the billing limit to $0** (makes overage a hard drop instead of a bill —
   the free tier is 1M events/month):
   *Organization settings → Billing → Product analytics → Edit billing limit → $0.*
4. Copy the **Project API key** (starts with `phc_`):
   *Settings → Project → Project API key.*
   This key is public/write-only by design — it can only ingest events, never read data —
   so it is safe to ship inside the client build.

## 2. Wire the key into the game (~1 minute)

1. In the Unity editor, select **`Assets/Resources/PostHogConfig.asset`**.
2. Paste the key into **Project Api Key**. Leave **Host** as `https://eu.i.posthog.com`
   (it must match the region from step 1 — `https://us.i.posthog.com` if you ignored the
   EU advice).
3. That's it. On next play, the facade detects the key and adds the PostHog sink.
   An empty key = sink disabled, UGS unaffected — so the asset is safe on every branch.

Optional knobs on the same asset: `Excluded Events` (event names never sent to PostHog —
the free-tier budget lever; start empty, add `ui_action` first if volume ever grows),
batch size / flush interval / offline queue cap.

## 3. Verify events are flowing (~5 minutes)

1. Enter Play Mode (or run a build).
2. **Answer the privacy flow with Accept.** Collection is opt-in: no age gate + consent =
   no events to *either* sink. If your test device answered "decline" in the past, flip it
   in Settings → the analytics/privacy toggle
   (`AnalyticsPrivacySettingsController`), or clear PlayerPrefs.
3. Play one game to the end screen, then background/quit the app (that triggers the flush).
4. In PostHog: **Activity** (left nav) — events appear within seconds of a flush:
   `game_started`, `game_completed`, `session_ended`, etc., each carrying
   `player_id`, `install_id`, `app_version`, `platform`, `schema_version`, and the gameplay
   parameters. (`session_id` / `build_version` never shipped - the envelope was reworked.)
5. Cross-check identity: the event's *distinct ID* equals the UGS PlayerId (same ID you
   see in UGS dashboards and Cloud Save) — that's the join key across all our systems.

**If nothing shows up**, in order of likelihood: consent not accepted (step 3.2); empty
API key or wrong host region in the asset; the event is in `Excluded Events`; no network
(events queue in memory and send on the next trigger; a killed process loses the queue —
backgrounding first flushes it).

## 4. Give your teammate access (the "another user does SQL" requirement)

*Organization settings → Members → Invite.* Free tier includes **unlimited team members**,
so invite whoever does analysis — they get the full UI: Product analytics, saved insights,
dashboards, and the SQL editor. No per-seat cost, no read-only tier gymnastics.

## 5. Getting insights out (no SQL needed for most questions)

PostHog's built-in insight types cover the standard questions — prefer them over SQL
because they handle cohorting/sessionization for you:

| Question | Where |
|---|---|
| Daily/weekly active players, trends per event | **Product analytics → New insight → Trends** |
| "Of players who launched, how many reached the menu, entered freestyle, finished a game?" | **Funnels** — steps: `game_first_launched` → `menu_ready` → `freestyle_entered` → `game_started` → `game_completed` |
| D1/D7/D30 retention | **Retention** — first event `game_first_launched` (or `game_started`), returning event `game_started` |
| Cohort of players who did X, analyzed for Y | **Cohorts** (People → Cohorts), then filter any insight by cohort |
| Player-level drill-down | **People** — every person is a UGS PlayerId with full event history |

## 6. SQL (HogQL) — for everything else

**Product analytics → New insight → SQL**, or the **SQL** item in the left nav. The
`events` table has: `event`, `timestamp`, `distinct_id`, and `properties.*` (our custom
parameters + envelope). Casts: `properties.*` values read as JSON — wrap numbers in
`toInt()`/`toFloat()` when aggregating. Ready-to-paste starters against our live schema:

```sql
-- Daily active players, last 30 days
SELECT toDate(timestamp) AS day, count(DISTINCT distinct_id) AS dau
FROM events
WHERE timestamp > now() - INTERVAL 30 DAY
GROUP BY day ORDER BY day
```

```sql
-- Games played per mode per day
SELECT toDate(timestamp) AS day, properties.game_mode AS mode, count() AS games
FROM events
WHERE event = 'game_completed'
GROUP BY day, mode ORDER BY day, games DESC
```

```sql
-- Win rate by mode and intensity (multiplayer balance check)
SELECT properties.game_mode AS mode,
       toInt(properties.intensity) AS intensity,
       round(countIf(toString(properties.player_won) = 'true') / count(), 3) AS win_rate,
       count() AS games
FROM events
WHERE event = 'game_completed' AND properties.player_won IS NOT NULL
GROUP BY mode, intensity
ORDER BY games DESC
```

```sql
-- Average game duration by mode (seconds)
SELECT properties.game_mode AS mode,
       round(avg(toInt(properties.duration_seconds))) AS avg_seconds,
       count() AS games
FROM events
WHERE event = 'game_completed'
GROUP BY mode ORDER BY games DESC
```

```sql
-- What players were doing right before the session ended (churn forensics)
SELECT properties.last_ui_action AS last_action, count() AS sessions
FROM events
WHERE event = 'session_ended'
GROUP BY last_action ORDER BY sessions DESC
```

```sql
-- Crystal economy: earned vs spent per day
SELECT toDate(timestamp) AS day,
       sumIf(toInt(properties.amount), event = 'crystals_earned') AS earned,
       sumIf(toInt(properties.amount), event = 'crystals_spent')  AS spent
FROM events
WHERE event IN ('crystals_earned', 'crystals_spent')
GROUP BY day ORDER BY day
```

```sql
-- New (consented) players per day + platform split
SELECT toDate(timestamp) AS day, properties.platform AS platform, count() AS new_players
FROM events
WHERE event = 'game_first_launched'
GROUP BY day, platform ORDER BY day
```

```sql
-- Loss-streak distribution (how deep do losing streaks go before players stop?)
SELECT toInt(properties.fail_count) AS streak, count() AS occurrences
FROM events
WHERE event = 'repeated_game_fail'
GROUP BY streak ORDER BY streak
```

Save any of these as an insight and pin it to a dashboard — that's the shared team view.
The full event vocabulary is `event-taxonomy.md` / `viability-report.md` §1.2.

## 7. Privacy & deletion runbook

- Consent: unchanged — the in-game opt-in dialog and settings toggle gate both sinks. A
  revoke (`SetConsent(false)`) stops transmission and clears PostHog's unsent queue.
- **Add PostHog (EU) to the privacy policy** processor list
  (`Docs/Legal/PRIVACY_POLICY_TEMPLATE.md` has the section).
- **Deletion requests**: the in-game "delete my data" button fires the UGS erasure API
  automatically. For PostHog the client can't do it (that API needs a private key that
  must never ship in a build), so it's a 1-minute manual step:
  *PostHog → People → search the player's UGS PlayerId → Delete person → check "delete
  all events"*. Deletion of events is processed asynchronously by PostHog.

## 8. Getting the Cloud Save data out (the other half of the ask)

Everything the game persists per player (XP, crystals, vessel stats, progression — the 12
keys in `DATA_INVENTORY.md` §1) is exportable in bulk with
**`Tools/Analytics/export_cloud_save.py`** — a standard-library Python script following
Unity's official export guidance. One-time setup is a read-only service account
(*Cloud Save Viewer* role); output is JSONL you can query in DuckDB or a spreadsheet, and
`player_id` there equals the PostHog `distinct_id`, so event data and save data join
directly. Full instructions + ready-made DuckDB queries: `Tools/Analytics/README.md`.

## 9. Budget guardrails

- Free tier: **1M events/month**, resets monthly; with the $0 billing limit, overage is
  dropped, never billed.
- Watch usage: *Organization settings → Billing* shows the running event count. PostHog
  emails at 80% and 100% of the free allowance.
- If we approach the cap: add `ui_action` (and then `setting_changed`) to
  `Excluded Events` on `PostHogConfig.asset` — UGS keeps receiving them, so nothing is
  lost from the system of record.
