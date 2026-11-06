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

`Tools/Analytics/console/` (`index.html`, `console.css`, `console.js`, and the generated `themes.css` and `theme.js`): plain HTML, CSS and JS with no build step and no library, drawn to the Frogma style guide (Aldrich and Chakra Petch, cut corners; a problem in Fire, healthy as the Lime tick). The page sits on the style guide's Deep Sea ground with the menu's sea lines behind every page (three slow layers, the game's SeaLines numbers), and its panels are glass over them. The theme is the game's colour set, picked in the sidebar: **Nocturne**, **Photic** or **Dart Frog**. The set colours the sea lines (its Jade, Blue and Ruby), their wash and glow, the data (its Blue), the selection and focus (its Jade), and the four category colours (Jade, Ruby, Gold, Blue) where a chart compares play types or builds. `make_themes.py` writes the themes from the game's `ColorSetCatalog` and `froglet-tokens.json`, each mark and text colour fitted to 3:1 or 4.5:1. Re-run it when a colour set changes; `--check` and the server's self-test fail when the files drift. The choice is kept in that browser; with none, the set is the one the game runs on. The server serves the page at `/` with a strict Content-Security-Policy. It asks once for the reader's key and keeps it in that browser. GitHub Pages can host
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
| `analytics_store.py` | `events.db` (raw, with player ids), and the rebuild of `aggregates.db` (no player ids) |
| `analytics_views.py` | The console's pages, read from `aggregates.db` only; the guarded SQL runner for Ask |
| `analytics_geo.py` | Country from an IP address (DB-IP country table), the address not kept |
| `analytics_claude.py` | The overnight read and Ask (official `anthropic` SDK, `claude-opus-5-5`) |
| `test_analytics_server.py` | 28 tests: `python3 analytics_server.py --self-test` |
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

## Claude, and what it can see

- **The overnight read**: the aggregates per build (last 7 days against the 14, rematch by play type, match health,
  rows per play, rejects) go to Claude. Three notes come back, each marked problem or info.
- **Ask**: Claude is shown the aggregates schema and the question, and writes one query. The server runs it
  read-only, on `aggregates.db` only, through a SQLite authorizer that allows nothing but reads. The query is capped
  at 200 rows and stopped if it runs long. Claude then answers from those rows, and the page shows the query beside
  the answer. Each reader gets 50 questions a day.
- Claude never receives a player id, a display name or a raw event: it has no door to `events.db`. The tests check
  that the text sent to Claude holds no player id.
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

Locally, to try it:

```bash
FROGLET_ANALYTICS_CONFIG=/tmp/fa.json python3 Tools/Analytics/server/analytics_server.py init
FROGLET_ANALYTICS_CONFIG=/tmp/fa.json python3 Tools/Analytics/server/analytics_server.py serve
```

## Not done yet

- `build_kind` stamped by the client (would split development builds from release; today they count as release).
- Off-VM backups (the nightly backup stays on the VM's disk).
- A last `import-posthog` run, once the builds that still send to PostHog are gone; then PostHog can be shut down.
