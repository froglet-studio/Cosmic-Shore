# Froglet Analytics server

Our own analytics backend, replacing PostHog (Shombith, 2026-11-05). It takes the game's events, keeps them on our
Oracle VM, and serves the **Froglet Analytics console**: the dashboard picked on Frogma as Rev 50 **B** (a rail of
pages: Overview, Retention, Matches, Data health, Ask Claude, Erasure; `Docs/MENU_REDESIGN_B.md` §122). Claude writes
three notes about the numbers each night and answers questions in the Ask box. UGS Analytics is unchanged.

Status (2026-11-05): the server and its tests are written. Not yet on the VM, the game not yet pointed at it,
the console page not yet built.

## How the game reaches it, with no code change

`PostHogAnalyticsSink` posts `{api_key, batch: [...]}` to `<host>/batch/`. This server accepts exactly that shape at
`/batch/`. Cutting over is two fields on `Assets/Resources/PostHogConfig.asset`:

| Field | Value |
|---|---|
| `host` | `https://analytics.<the VM>` (what the setup script prints) |
| `projectApiKey` | the ingest key `analytics_server.py init` printed |

Running both side by side (PostHog and ours) for the comparison weeks needs a second sink instance with its own
config, a small change to `AnalyticsServiceFacade` (one more `PostHogAnalyticsSink` built from a second config asset).
Not written yet; it is C# and needs the Unity verify rule.

## Pieces

| File | What it does |
|---|---|
| `analytics_server.py` | HTTP server and CLI: ingest, the console's read API, Ask, erasure, the nightly job |
| `analytics_store.py` | `events.db` (raw, with player ids), and the rebuild of `aggregates.db` (no player ids) |
| `analytics_views.py` | The console's pages, read from `aggregates.db` only; the guarded SQL runner for Ask |
| `analytics_claude.py` | The overnight read and Ask (official `anthropic` SDK, `claude-opus-5-5`) |
| `test_analytics_server.py` | 18 tests: `python3 analytics_server.py --self-test` |
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

- The console page itself (Rev 50 B). It will read `/v1/overview`, `/v1/retention`, `/v1/matches`, `/v1/health` and
  `/v1/digest`, and post to `/v1/ask`. GitHub Pages can host the page; the data stays on the VM behind read keys.
- The second sink for the side-by-side weeks, and `build_kind` stamped by the client (which would split development
  builds from release).
- Off-VM backups (the nightly backup stays on the VM's disk).
- Loading PostHog's history once, with the 50 ms duplicates dropped.
