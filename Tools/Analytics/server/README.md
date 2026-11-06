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

**Run `import-posthog` once more after deploying the 2026-11-06 server.** Every `$identify` is now kept as a row of the
player's history (`person_snapshots` in `events.db`), where before only the latest values were merged into `people`.
Re-running the import brings PostHog's identify history in (each `$identify` once: rows are keyed by player, time and
content, so the run is idempotent) and the Toybox's older vessel switches with it (below).

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
| `analytics_notebook.py` | The Notebook page's routes and rules: notebooks in `events.db` (no player ids), one cell run, Claude's turn over the notebook's re-run totals; `test_analytics_notebook.py` (19 tests) runs with `--self-test` |
| `mode_elements.py` | Writes `mode_elements.json` (each mode's genre element) from the game's `ModeGenre.cs`, `GameModes.cs` and `ModePreviewLibrary.asset`; `--check` fails when it is stale, `--self-test` checks known answers and a negative control |
| `mode_elements.json` | GENERATED: `game_mode` -> `[primary, secondary or null]`, the modes with no element, each mode's metric. The server reads only this file (the VM has no `Assets/`) |
| `test_analytics_server.py` | 77 tests: `python3 analytics_server.py --self-test` |
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
carries the team fields; earlier matches read `multi_human_unknown`, never guessed. Each play also gets a match setup (below, Match setups).

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

## Elements, the Toybox and saved views (2026-11-06)

Asked for by Shombith on 2026-11-06: "the matches over time should also have element support"; players who "went
inside freestyle/toybox and played vessels ... can we bring it for the old one?"; "save our own view as well and make
it public and that will saved in saved presets under dashboards".

**Elements.** Every mode has a genre element (Charge, Mass, Space, Time; Omni), from the game's
`ModeGenre.TryElementsFor`: the mode's own row (Scurry, Tollway, Scarab Scramble: Mass; Brood Rush: Mass and Space),
else its scoring metric's element. The metric is the one the arcade grid reads, the mode's preview definition in
`Assets/Resources/ModePreviewLibrary.asset` (the arcade cards carry no metric). `mode_elements.py` writes that answer to
`mode_elements.json`; re-run it when `ModeGenre.cs`, `GameModes.cs` or a preview definition changes (the self-test fails
while it is stale). Each play's element (`plays.element`, `element_secondary`, `element_source`) is the one its
`game_started` sent (builds from 2026-11-06: `element_source = 'event'`), else the json's row for its `game_mode`
(`'table'`: every older match, the PostHog history included), else NULL ("No element": a mode with no genre or an old
mode name; Data health lists them as `modes_without_element`). A two-element mode counts under its first element in
every split; its second is listed apart (`secondary` on the series answer, `element_secondary` on Matches), never added
in. `/v1/series` takes `by=element` for matches, players, new players, finished, rematch and flight minutes (not events);
`/v1/matches` adds `by_element` and an `element` on each `by_mode` row. **Elements have no colour** (LOCKED): the console
draws an element split in one neutral hue in steps, with the names beside the marks.

**Freestyle (the Toybox).** `freestyle_entered` came in with the PostHog history (entries since August); builds after
2026-11-06 also send its `vessel_class`, `freestyle_ended {seconds, vessel_class, vessels_flown}` and
`freestyle_vessel_changed {vessel_class, from_vessel_class, seconds_in_freestyle}`. Older switches are **recovered from the
identify history**: builds since 2026-10-12 re-identify after every Toybox flight with `selected_vessel`, so where a
player's selected vessel changes between two `$identify` rows the rebuild counts a switch A -> B at that row's time (the
time is when the profile was sent, not the click, and several switches between two identifies read as one). A player's
recovered switches stop at their first live `freestyle_vessel_changed`, so nothing counts twice. Aggregates (no player
ids): `freestyle_vessels` (per day, build, source, vessel: entries, first entries, stretches, seconds, vessels flown,
switches to and from, recovered to and from), `freestyle_switches` (per day, build, source, from, to, `origin` event or
recovered), and `player_counts` dims `freestyle`/`entered` and `freestyle_vessel`. Near-duplicate rows are left out.

| Route | Who | What |
|---|---|---|
| `GET /v1/freestyle?build&days&era` | reader | `{tiles: {entries, first_entries, players, stretches, minutes, avg_stretch_min, avg_vessels_flown, switches, recovered_switches}, per_day: [{day, entries, players, minutes, switches, recovered_switches}], vessels: [{vessel_class, entries, stretches, minutes, switches_to, switches_from, recovered_to, recovered_from}], switch_pairs: [{from_vessel, to_vessel, switches, recovered}] (top 20), history: {first_entry_day, first_imported_entry_day, last_imported_entry_day, first_minutes_day, first_vessel_entry_day, first_switch_day, first_recovered_switch_day}, notes, unfiltered}` (the Play filter does not reach it: the Toybox is not a match) |
| `GET /v1/series?measure=freestyle_entries\|freestyle_players\|freestyle_minutes\|freestyle_switches` | reader | splits none, era, build_kind, vessel_class; switches also from_vessel_class and switch_source (event \| recovered). The `notes` say what history each has |
| `GET /v1/person?id=` | profile readers | adds `freestyle: {entries, first_entries, stretches, seconds, minutes, vessels_flown, switched_to, switch_count, recovered_switches, switches: [{ts_ms, from, to, seconds_in_freestyle, approx}], stretches_recent, per_day, first_ms, last_ms, first_minutes_ms, note}` (`approx: true` = recovered) |
| `GET /v1/people` | profile readers | adds `freestyle_entries` and `freestyle_seconds` per player |
| `GET /v1/dashboards/tile?source=builtin:freestyle_entries` | reader | Toybox entries per day |

**Saved views ("presets").** Any chart panel's **Save view** keeps its switches and the page's filters under a name, for
its key or for everyone (Public); **Views** on the panel applies one (it sets the switches and the header's build, range,
play and era, and goes to the page). Dashboards lists them all under **Saved presets** (open, delete your own, and **Add
to dashboard** for a series chart, which adds a `preset:<id>` tile that runs the view's own series with its own
filters). Stored in `events.db` (`presets`); no player data. Ownership is by key name: with `all_keys_admin` every key is
an admin for the pages, but a saved view stays its owner's, and only a key in `admin_keys` may delete someone else's
public view.

| Route | Who | What |
|---|---|---|
| `GET /v1/presets` | reader | `{presets: [{id, name, owner, public, page, chart_id, series, settings, filters, created_ms, updated_ms}]}`: this key's own and every public one; someone else's private view is never listed |
| `POST /v1/presets` `{id?, name, page, chart_id, series?, settings: {form?, measure?, by?, over?}, filters: {build?, days?, play?, era?}, public?}` | reader | makes one, or (with the id of your own) replaces it. 400 with the reason for a bad page, chart id, setting, filter or unknown field; another key's id: 400 if private (not shown), 403 if public |
| `DELETE /v1/presets?id=` | owner, or a key in `admin_keys` for a public one | 404 for an id this key cannot see (someone else's private view), 403 for someone else's public one |
| `GET /v1/dashboards/tile?source=preset:<id>` | a key that can see the view | `{columns, rows, series, preset, form, unfiltered}`: the view's own measure, split, over and filters (the page's filters do not reach it); 400 if the view is not a series chart |
| `PUT /v1/dashboards` | admin | a tile may now be `preset:<id>` of a series view the saving key can see |

## Match setups (2026-11-07)

Asked for by Shombith on 2026-11-07: "a sliced grouping in overview with competitive, teams, teams vs ai as per the
domains in both multiplayer and for solo like the same how many AIs". Every play gets a **setup** (`plays.setup`,
`analytics_store.setup_of`), read from that player's own `game_started`; `play_type` is unchanged beside it. Builds from
2026-11-07 send `domain`, `humans_on_my_domain` (the player included), `humans_on_other_domains`, `ai_on_my_domain` and
`ai_on_other_domains`; the four counts are kept as `humans_mine`, `humans_other`, `ai_mine`, `ai_other` (NULL when the
build did not send them).

| Setup | Group | Rule |
|---|---|---|
| `competitive` (Competitive) | multiplayer | 2+ humans; `humans_on_my_domain` = 1 and `humans_on_other_domains` >= 1: every human for themselves, 1v1 |
| `teams` (Teams) | multiplayer | 2+ humans; `humans_on_my_domain` >= 2 and `humans_on_other_domains` >= 1: humans together against humans |
| `teams_vs_ai` (Teams vs AI) | multiplayer | 2+ humans; `humans_on_other_domains` = 0: every human on one domain, the opponents AI |
| `multi_unrecorded` (Teams not recorded) | multiplayer | 2+ humans and no `humans_on_other_domains` (older builds, the PostHog history), or other humans without `humans_on_my_domain`: never guessed |
| `solo_with_ai_team` (With AI teammates) | solo | 1 human and `ai_on_my_domain` >= 1, whatever the opponents. A slice of its own, not a flag, so the pie stays one level. Only builds from 2026-11-07 can say so |
| `solo_alone`, `solo_vs_1_ai`, `solo_vs_2_ai`, `solo_vs_3plus_ai` | solo | 1 human (or none recorded) by the AI opponents: `ai_on_other_domains` when sent, else `player_count_ai`, else `ai_count`, else 0. Old matches and the PostHog history have these too |

Humans are `player_count_human`, as `play_type` reads them, so the multiplayer setups are exactly the Multiplayer play
group. A count that is not a whole number 0 or more (text, negative, a boolean) counts as not sent. Two things to know
when reading them:

- **A match is read from each player's side.** An uneven match (two humans against one) is Teams for the pair and
  Competitive for the one, so its match counts in both; plays, finished, rematch and won count each player once.
- **The Play filter's Solo** is `play_type` `solo_alone`, which reads `player_count_ai` only. A PostHog-era play that
  sent `ai_count` without `player_count_ai` is in Solo there, and its setup says `vs N AI`.

| Route | Who | What |
|---|---|---|
| `GET /v1/overview` | reader | adds `setups: [{setup, label, group: multiplayer\|solo, matches, plays, finished_pct, rematch_pct, win_pct, players}]` in the order above (multiplayer first, every setup, zeros included), `setup_history: {domains_from, first_domain_day, multi_plays, multi_unrecorded_plays, multi_unrecorded_pct}` and `setup_notes`. Takes the Play filter and the era like the rest of the page; `players` come from `player_counts` (a player in two setups is in both) |
| `GET /v1/series?by=setup` | reader | matches, finished %, rematch %, flight minutes, players and new players by setup (not events or the Toybox, which are not matches: 400). With a Play filter, players by setup keep the setups of that group |

The console's Overview has a **Match setups** section: two slices side by side, Multiplayer and Solo, each a switchable
chart (Pie by default, Bars, Table; Matches, Players, Finished %, Rematch %; Players is never a pie, since a player can be
in two setups), and **Match setups over time**. Multiplayer wears the theme's series colours in a fixed order with "not
recorded" in the neutral grey; solo is one hue in steps, light to dark by AI count, AI teammates darkest; the shares are
written on the slices. While most multiplayer is not recorded the slice says "Builds before <the first day a build sent
domains> did not send domains; new builds fill this in." Every series chart (Matches over time included) offers Split:
Setup.

## The Notebook (2026-11-07)

Asked for by Shombith on 2026-11-07: "a chat session or report data session or notebook inside the analytics tool".
One page, **Notebook**, is all three. A notebook is cells in order:

- **Note**: text in a small markdown subset (`#` headings, `-` and `1.` lists, `**bold**`, `*italic*`, `` `code` ``,
  fenced code), drawn as page elements, never as HTML.
- **SQL**: one SELECT over `aggregates.db` through the Query Studio's own door (read-only, SELECT only, 200 rows,
  stopped if it runs long; `events.db` cannot be reached), with its result table. Running one needs an admin key, as
  the Query Studio does (every key is an admin's while `all_keys_admin` is on).
- **Chart**: a SQL cell's result drawn as bars, line, pie, table or number (its X and Y columns picked), or one of
  the console's switchable series (`/v1/series`: measure, split, over) under the notebook's filters.
- **Question**: asked in the box pinned under the cells, as in a chat. Claude's answer comes back as cells: a note,
  and when it wrote a query, that query already run and a chart of it. Claude is shown the question, the notebook's
  filters, the aggregates schema and the earlier cells (the latest 16): their text, their queries and the totals
  those return **re-run on the server** (never a result the browser sent), at most 40 rows a cell and 300 in all, so
  "now split that by platform" builds on what is there. If its query fails the guard, Claude gets one retry with the
  reason; when it runs, Claude writes the note from the rows. Same model, effort and error handling as the Ask box,
  and each question counts toward the reader's 50 a day (one asked while Claude is off costs nothing).

The notebook's filters are the header's while it is open: opening one sets the header to its build, range, play and
era; Save keeps the header's. **Report** shows the outputs only, with the title, the date, the owner and the filters:
**Print / Save as PDF** (a print stylesheet keeps the theme and hides the console around it) and **Copy as
Markdown** (tables as Markdown tables, each query in a fenced block).

| Route | Who | What |
|---|---|---|
| `GET /v1/notebooks` | reader | the notebooks this key may see, as summaries (`cell_count`, no cells) |
| `GET /v1/notebooks?id=` | reader | one, whole; 404 for someone else's private one |
| `POST /v1/notebooks {name, visibility, filters, cells}` | reader | makes one, owned by the key's name; `{duplicate: <id>, name?}` copies one this key may see, as a private notebook of its own |
| `PUT /v1/notebooks {id, name?, visibility?, filters?, cells?}` | owner | changes (renames, shares) one of the key's own; someone else's shared one is 403 (Duplicate it) |
| `DELETE /v1/notebooks?id=` | owner | deletes; a key in `admin_keys` may delete a shared one |
| `POST /v1/notebooks/run {cell, filters?}` | admin (sql), reader (chart) | runs one cell: `{output}` |
| `POST /v1/notebooks/ask {question, cells, filters}` | reader | `{cells, asks_left}` or `{error}`; 429 when the day's questions are used up |

`visibility` is `me`, `readers` or `admins`, as for saved queries: a private notebook is never listed, opened, changed
or deleted by another key. Notebooks are kept in `events.db`, table `notebooks` (made by `analytics_notebook.py` on
first use; in the nightly backup with the rest), at most 200, 80 cells each. **A notebook holds no player ids**: a save
is refused when a SQL cell's result has a player-id column (`distinct_id`, `player_id`, `install_id`, `user_id`, ...)
or when any text or value in it is the id of a player `events.db` knows; the text sent to Claude is checked the same
way and any long id-shaped token is cut out too. The routes and rules are `analytics_notebook.py`; the tests,
`test_analytics_notebook.py`, run with `--self-test` and include the negative controls (a SQL cell cannot read
`events.db`; a result with `distinct_id` is refused; another reader cannot see a private notebook; the prompt holds no
player id, no display name and no row the browser made up). The page is `console/notebook.js` and `notebook.css`;
the review copy (`make_preview.py`) carries two sample notebooks (`console/notebook_sample.py`), and its Ask and Run
say they need the server.

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

It prints `READ key for ian (shown once): fa_read_...`; paste the line or just the key into the console. The running
server picks up a new or deleted key at the next request, with no restart. A reader in `profile_readers` can open
player profiles; `key admin <name>` makes an admin key (Query Studio, Dashboards, eras, Erasure, every profile). The
console's sidebar says which kind signed in ("garrett · read key, profiles", "shombith · admin key").

- **Every key is an admin's** (Shombith, 2026-11-06: "Give everyone who has a key an admin access there is no seperation
  between us"): while `all_keys_admin` is true (the default), any valid key gets the Admin pages, eras, saved queries,
  dashboards, Erasure and every profile. Set `"all_keys_admin": false` in `config.json` to bring the read / admin split
  and `profile_readers` back; the running server picks it up at the next request.
- `key list` prints every key's name and kind, never a secret.
- `key delete [read|admin] <name>` removes one. A name that is both kinds must say which, and the last admin key
  cannot be deleted.
- A read key cannot be named "admin", and a name is one kind only: `key read admin` once made a READ key called
  "admin", and the person signed in with it saw no Admin pages and no profiles (2026-11-06). The admin key `init`
  made is in `config.json` under `admin_keys`.

Locally, to try it:

```bash
FROGLET_ANALYTICS_CONFIG=/tmp/fa.json python3 Tools/Analytics/server/analytics_server.py init
FROGLET_ANALYTICS_CONFIG=/tmp/fa.json python3 Tools/Analytics/server/analytics_server.py serve
```

## Not done yet

- `build_kind` stamped by the client (would split development builds from release; today they count as release).
- Off-VM backups (the nightly backup stays on the VM's disk).
- A last `import-posthog` run, once the builds that still send to PostHog are gone; then PostHog can be shut down.
