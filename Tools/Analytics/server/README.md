# Froglet Analytics server

Our own analytics backend, replacing PostHog (Shombith, 2026-11-05). It takes the game's events, keeps them on our
Oracle VM, and serves the **Froglet Analytics console**: the dashboard picked on Frogma as Rev 50 **B** (a rail of
pages: Overview, Retention, Matches, Data health, Ask Claude, Erasure; `Docs/MENU_REDESIGN_B.md` §137). Claude writes
three notes about the numbers each night and answers questions in the Ask box. UGS Analytics is unchanged.

Status (2026-11-06): live on the Oracle VM, following `bleeding-edge` and updating itself nightly. The game sends to it
directly (PostHog removed from the game). PostHog's history is in: 66,746 events over 91 days from early August 2026,
every day matching PostHog's own count. Where it stands, what is unchecked and what it waits on:
`Docs/Reports/ANALYTICS.md`.

## The console page

`Tools/Analytics/console/` (`index.html`, `console.css`, `console.js`, and the generated `themes.css` and `theme.js`): plain HTML, CSS and JS with no build step and no library, drawn to the Frogma style guide (Aldrich and Chakra Petch, cut corners; a problem in Fire, healthy as the Lime tick). Its **theme** is one of the game's colour sets, picked in the sidebar: **Nocturne**, **Photic** or **Dart Frog**, and the whole UI changes with it. The set's signature face (Plum, Deep cyan, Bronze) re-hues the ground, panels, lines and text at the style guide's Deep Sea lightness steps (Dart Frog is metallic bronze); panels are drawn like the game's prisms, a faint rim over the face; the set's rims paint the sea lines behind every page (the menu's SeaLines, its three slow layers), the data, the selection and focus, and the four category colours where a chart compares play types or builds; its own call to action and danger keep their meanings. `make_themes.py` writes the themes from the game's `ColorSetCatalog` (the assets read as linear HDR) and `froglet-tokens.json`, every text and mark colour fitted to 4.5:1 or 3:1. Re-run it when a colour set changes; `--check` and the server's self-test fail when the files drift. The choice is kept in that browser; with none, the theme is the colour set the game runs on. The server serves the page at `/` with a strict Content-Security-Policy. It asks once for the reader's key and keeps it in that browser. GitHub Pages can host
the same five files; open it there as `https://<pages>/?api=https://analytics.<the VM>` and add the Pages origin to
`allowed_origins`. The page holds no data.

## How the game reaches it (nothing to fill in)

PostHog is gone from the game (Shombith, 2026-11-06). `FrogletAnalyticsSink` posts `{api_key, batch: [...]}` to
`<serverUrl>/batch/`, and `Assets/Resources/FrogletAnalyticsConfig.asset` ships already pointed at the server:

| Field | Committed value |
|---|---|
| `serverUrl` | `https://analytics.130-210-34-146.sslip.io` |
| `ingestKey` | the public key in `Tools/Analytics/server/ingest_key.public` |

The ingest key is write-only and public by design (it ships in every build, as PostHog's project key did); the
server reads the same file, so no key is copied by hand, and a test fails if the two ever differ. The server checks
every event against the schema and rate-limits each sender; reading needs a read key that never ships. If the
server's address changes (a domain of our own), `serverUrl` is the one value to change, in one commit. Events a
device had queued for PostHog are sent here on its first start with the new sink.

## The older data (PostHog's history)

`import_posthog.py` copies PostHog's history in, once: every event the game sends and every `$identify`, one day at a
time, through the same checks as live events, with their own timestamps. Everything PostHog added is dropped (every
`$` property except `$set`, so no IP address is stored); the country comes from PostHog's own lookup, two letters.
Running it again stores nothing twice. The October double-sends come in as they happened, flagged, and every count
collapses them. It needs a PostHog **personal** API key with the `query:read` scope, in `POSTHOG_PERSONAL_API_KEY`,
and the project id:

```bash
sudo -u froglet-analytics env FROGLET_ANALYTICS_CONFIG=/var/lib/froglet-analytics/config.json \
  POSTHOG_PERSONAL_API_KEY="$POSTHOG_PERSONAL_API_KEY" \
  /var/lib/froglet-analytics/venv/bin/python /var/lib/froglet-analytics/repo/Tools/Analytics/server/analytics_server.py \
  import-posthog --project <id> --dry-run
```

Drop `--dry-run` to store; it rebuilds the numbers when it finishes. Our PostHog project is `224840` (EU cloud).

It reads one count per day first, skips empty days, and checks each day it reads against that count (`days_short` in
the summary). It pages by `timestamp`, because PostHog answers 400 to `OFFSET` on queries made with a personal API
key, and waits when PostHog rate-limits it. Run it once more after the builds that still send to PostHog have died
out: it adds only what is missing. Then delete the personal key in PostHog.

## Updates (nightly, by itself)

`self-update.sh`, installed as `/usr/local/bin/froglet-analytics-update` and run by `froglet-analytics-update.timer`
at 04:20 UTC: it fetches the branch the server follows (`/var/lib/froglet-analytics/branch`), switches to
`bleeding-edge` by itself once that branch has merged, runs the server's tests on new code, and restarts the service
only if they pass (a failure keeps the running code and is retried the next night). Log:
`journalctl -u froglet-analytics-update`; run it now: `sudo froglet-analytics-update`. Checked in a sandbox through
up to date, update, failing tests (kept the old code), merged branch (moved to bleeding-edge) and deleted branch.

## Pieces

| File | What it does |
|---|---|
| `analytics_server.py` | HTTP server and CLI: ingest, the console's read API, Ask, erasure, the nightly job |
| `analytics_store.py` | `events.db` (raw, with player ids, and the eras, segments, saved queries and dashboards people make), and the rebuild of `aggregates.db` (no player ids) |
| `analytics_views.py` | The console's pages, read from `aggregates.db` only; the switchable charts (`/v1/series`); the guarded SQL runner for Ask and the Query Studio |
| `analytics_geo.py` | Country from an IP address (DB-IP country table), the address not kept |
| `analytics_claude.py` | The overnight read and Ask (official `anthropic` SDK, `claude-opus-5-5`) |
| `test_analytics_server.py` | 51 tests: `python3 analytics_server.py --self-test` |
| `../console/` | The console page (Rev 50 B), served at `/` |
| `../console/make_preview.py` | A review copy of the console with no server: invented sample data baked in (`--out page.html`), for a claude.ai link |
| `import_posthog.py` | Copies PostHog's history in, once, without IP addresses |
| `self-update.sh` | The nightly self-update (tests before it restarts) |
| `ingest_key.public` | The public, write-only ingest key every build ships with |
| `setup-analytics-vm.sh` | Installs it on the Compute VM as its own user and service, behind Caddy |

Standard library only, except the two Claude features (`pip install anthropic`, `ANTHROPIC_API_KEY`). Without them
the overnight read is written by rules and the Ask box says it is off.

## What it does with an event

1. **Checks it.** The ingest key must match. The event must be in `Docs/Analytics/EVENT_SCHEMA.json` (or
   `$identify`, or `extra_events` in the config). It needs a player id and a timestamp from the last 90 days, and its
   properties must be under 16 KB. A failing event is counted under its reason (Data health shows them) and the
   rest of the batch is stored.
2. **Stores a resent batch once.** A client that re-sends a batch whose reply it missed does not double the rows.
3. **Flags the double-send.** The same player, event and properties within 50 ms is marked as a near-duplicate,
   which is the PR #1639 bug. Such rows are kept but marked, and every count collapses them by (player, match).
4. **Tags the build.** The client's `build_kind` wins. Until it sends one: a platform ending in `Editor` is `editor`,
   a version ending in `-port` is `port`, anything else is `release`. Editor rows never mix into release numbers.
5. **Honours deletion.** The in-game delete button already sends `gdpr_deletion_requested`. Here it deletes every
   row and the person at once. The log keeps a salted hash, not the id. This closes the blocking item in
   `Docs/Analytics/DATA_ARCHITECTURE.md` §8.6, which PostHog's write-only key could only flag.

Every 5 minutes `aggregates.db` is rebuilt. It holds per-play rows (mode, intensity, vessel, play type, completed,
rematched within 600 s, flight time), daily counts, rows per play, players in windows, and D1/D7 return cohorts. Ian's
definitions from `replay_retention.sql` are used as written. Co-op, versus and mixed split once `game_started`
carries the team fields; earlier matches read `multi_human_unknown`, never guessed.

Nightly (05:00 UTC by default): raw events older than `retention_days` (395) are deleted, `events.db` is backed up
(14 kept), the aggregates are rebuilt, and the overnight read is written.

## Pages, and who sees player profiles

Overview, **Players**, Retention, **FTUE**, Matches, **Economy**, **Social**, Data health, Ask Claude, Erasure.

- **Players**: active players per day, countries, platforms, the first-day funnel (reached the menu, started a match,
  finished one, finished the FTUE, came back another day), launch-to-menu time per platform, and how sessions end.
  Below that, **every player**: display name, player id, country, platform, first and last seen, matches, crystals.
  It is searchable, and a click opens a **profile**: where and on what they play, the person properties the game
  sent, activity per day, modes, vessels and play types, every match (finished, won, flight time, rematched),
  crystals in and out, unlocks, FTUE steps with times, friends, invites and parties, settings, and the event
  timeline.
- **FTUE**: each quest-graph node, with the share of new players who completed it and when.
- **Economy**: crystals earned and spent per day, sources, spends blocked for lack of crystals, unlocks, current
  balances, and **crystals paid twice** (same player, source and amount within 1 s: the October wallet bug).
- **Social**: invites sent and joined, friend requests and acceptance, shares.
- **Matches** adds win rate by mode, vessels, favourites, repeated fails, shares and time paused.
  **Data health** adds double payouts and cloud save failures.

Profiles come from the raw events, so they are shown only to **admin keys and the read keys named in
`profile_readers`** in `config.json`; any other read key sees the totals only. Claude never sees a profile.

**Country**: ingest looks up the sender's address in DB-IP's free country table (CC BY 4.0; the Players page
credits it) and keeps only the two-letter code; the address itself is never stored. The nightly job downloads the
month's table into the data directory when the copy is over 32 days old (`geo_csv` in the config points at your own
file instead). Decided by Shombith, 2026-11-05 (`Docs/Analytics/DATA_ARCHITECTURE.md` §8.11 row 8).

## Rev 65: eras, cohorts, play types, the Query Studio and dashboards

Asked for by Shombith on 2026-11-06 and drawn on Frogma as Rev 65 (`Docs/MENU_REDESIGN_B.md` §141). Every read
below takes the same `build`, `days` and `play` as the other pages, and answers `unfiltered`: the parts of the page
that ignore the play filter or the era. A bad value (an unknown era, a build or measure that does not exist) is a
400 with the reason, never a 500.

**Eras** are named date ranges: Old PostHog data, Pre-beta editor testing, and the planned closed beta and Steam
release. An era has an `id` (`[a-z0-9_]`, up to 40), a `name`, `from` and `to` days, a `source`
(`imported`, `live` or `any`), a `kind` (`data` or `planned`), the `builds` it covers (empty for every build), a
`colour` (`jade`, `ruby`, `gold` or `blue`) and a `note`. The four from the launch plan are made on the first start
that finds none; the PostHog one starts on the first imported day. After that only an admin changes them.
`era=<id>` on Overview, Retention, Matches, Play types, Cohorts, Players, `/v1/series` and dashboard tiles puts the
era's own range (up to today), source and builds in place of the days window, and the page says which (`from`, `to`,
`source`, `era`). **Imported** events are told apart exactly, not by date: they are the ones `import_posthog.py`
stored with their own time (`received_ms = ts_ms`), and the rebuild carries that as `imported` on `plays`,
`daily_events`, `daily_plays` and `returns`.

| Route | Who | What |
|---|---|---|
| `GET /v1/eras[?build=]` | reader | each era with `events`, `players`, `matches`; `weekly` events per week, each counted in the first era (in list order) that holds it, `era: null` for none; `milestones` |
| `PUT /v1/eras` `{"eras": [...], "milestones"?: [...]}` | admin | replaces the list (and the milestones when sent), then rebuilds the aggregates so the era counts are fresh |
| `GET /v1/eras/compare?a=&b=[&build=]` | reader | two eras side by side: players, new players, matches, editor share, finished, rematch, back next day, rows per play. No `build` means every build |
| `GET /v1/cohorts` | reader | `weekly` cohorts (players by the week of their first day, the share active each week after, `null` for weeks not begun) and `tiles` (median lifespan, D1, D7, lapsed 14 days, players). Profile readers also get `lifespans`: one row per player with their active days and the play group they played most |
| `GET /v1/playtypes` | reader | solo vs AI, solo and multiplayer side by side (matches per day, finished, rematch, D1, D7, flight minutes, top modes), their share, and the party funnel with a `funnel_note` saying what each step counts |
| `GET /v1/series?measure=&by=&over=` | reader | one switchable chart (below) |
| `GET /v1/people?play=&era=` | profile readers | as before, plus `active_days` (the latest 120) per player; `play` keeps players who played at least one match of that group |
| `GET /v1/person?id=` | profile readers | as before, plus `duration_seconds` on each match, `activity.by_hour_weekday` (7 x 24 match starts, Monday first, UTC), `economy.by_source` and `lifespan_days` (first day to last, both counted) |
| `GET /v1/segments`, `POST {name, filters, shared?}`, `DELETE ?id=` | profile readers | saved player filters. The server keeps `filters` as sent; a reader sees their own and the shared ones; only an admin shares; the owner or an admin deletes |
| `GET /v1/schema` | admin | every table in `aggregates.db` with its columns and rows; saved tables are `t_<name>` with `saved: true` |
| `POST /v1/query {"sql"}` | admin | `{columns, rows, ms, truncated}` through the Ask box's own door: one SELECT, `aggregates.db` only, a SQLite authorizer that allows nothing but reads, 200 rows, stopped if it runs long. `events.db` cannot be reached |
| `GET /v1/queries`, `POST`, `DELETE ?id=` | read: reader; write: admin | saved queries `{name, sql, chart, x, y, visibility, as_table}`. `chart` is bars, line, donut, gantt, table or number; `visibility` me, readers or admins (a reader sees the readers ones; an admin sees all). A save runs the query once and refuses it if it fails. `as_table` makes `t_<name>` (name `[a-z0-9_]`, up to 40) in `aggregates.db` at the save and at every rebuild, through the same guard with up to 10,000 rows; a query that fails then is skipped and named in `meta.saved_table_errors` |
| `GET /v1/dashboards`, `PUT {id?, name, shared, tiles, email?}`, `DELETE ?id=` | read: reader; write: admin | tiles are `{id, source, title, chart, w 1-4, h 1-3}`, `source` being `query:<saved query id>` or `builtin:` one of new_players, d1_return, ftue_finished, rows_per_play, matches_per_day, play_mix. `email` ("mondays 09:00") is stored only: nothing is sent yet |
| `GET /v1/dashboards/tile?source=` | reader | `{columns, rows}`: a query tile reruns its saved SQL (only one this key may read), a built-in reads its view with the page's filters |

Overview also answers `marketing` (new players per day, players by country and platform, shares, invites sent) and
`business` (crystals earned and spent, players holding 2,000 or more), counts only.

**The switchable charts.** Every chart in the console carries its own switches (form, measure, split), so business,
marketing and analysis read the same chart their own way. The form is drawn in the browser; the rest is one route:
`/v1/series?measure=<m>&by=<split>&over=day|week|none` answers
`{measure, by, over, points: [{x, key, value}], keys, unfiltered, notes}`. `x` is a day, a week's Monday or null;
`keys` are the split's top 8 and `other`. Measures: matches, players, finished_pct, rematch_pct, flight_min,
new_players, events. Splits: none, play_type, era, platform, country, game_mode, vessel_class, build_kind,
intensity. Events can be split only by none, era and build_kind (they carry no mode or platform); any other pair is
a 400 that says so, and the console hides it. Players come from `player_counts`, which the rebuild counts per day,
week, last-N-days window and era (a week's players are not the sum of its days), for every source and build, and
`*` for every build with each player counted once. A player on two platforms in a day counts under both, and the
answer's `notes` say so.

## Claude, and what it can see

- **The overnight read**: the aggregates per build (last 7 days against the 14, rematch by play type, match health,
  rows per play, rejects) go to Claude. Three notes come back, each marked problem or info.
- **Ask**: Claude is shown the aggregates schema and the question, and writes one query. The server runs it
  read-only, on `aggregates.db` only, through a SQLite authorizer that allows nothing but reads. The query is capped
  at 200 rows and stopped if it runs long. Claude then answers from those rows, and the page shows the query beside
  the answer. Each reader gets 50 questions a day.
- Claude never receives a player id, a display name or a raw event: it has no door to `events.db`. The tests check
  that the text sent to Claude holds no player id, and that the new tables (`player_counts`, `cohort_weeks`,
  `cohort_days`, saved `t_` tables) hold none either. The Query Studio uses the same door.
- Server-side fallbacks are on (`fallbacks: "default"`), so a declined request is retried on another model inside
  the same call.

## Running it

On the VM (as a user with sudo), once:

```bash
export GITHUB_TOKEN=...            # can read the repository
export ANALYTICS_DOMAIN=analytics.froglet.studio    # optional; default analytics.<ip>.sslip.io
export ANTHROPIC_API_KEY=...       # optional; turns on Claude
export ANALYTICS_PROFILE_READERS="ian garrett caleb"   # read keys that may open player profiles
bash Tools/Analytics/server/setup-analytics-vm.sh
```

A dashboard key for a person (revoke by deleting their line in `config.json`):

```bash
sudo -u froglet-analytics env FROGLET_ANALYTICS_CONFIG=/var/lib/froglet-analytics/config.json \
  /var/lib/froglet-analytics/venv/bin/python /var/lib/froglet-analytics/repo/Tools/Analytics/server/analytics_server.py key read ian
```

It prints `read key for ian (shown once): fa_read_...`; paste the line or just the key into the console. The running
server picks up a new or deleted key at the next request, with no restart. `key read admin` makes a READ key named
"admin"; the admin key (Erasure, every profile) is the one `init` printed at setup, kept in `config.json` under
`admin_keys` (`sudo cat /var/lib/froglet-analytics/config.json`). A reader in `profile_readers` can open player
profiles; `key admin <name>` makes another admin key.

Locally, to try it:

```bash
FROGLET_ANALYTICS_CONFIG=/tmp/fa.json python3 Tools/Analytics/server/analytics_server.py init
FROGLET_ANALYTICS_CONFIG=/tmp/fa.json python3 Tools/Analytics/server/analytics_server.py serve
```

## Not done yet

- `build_kind` stamped by the client (would split development builds from release; today they count as release).
- Off-VM backups (the nightly backup stays on the VM's disk).
- A last `import-posthog` run, once the builds that still send to PostHog are gone; then PostHog can be shut down.
