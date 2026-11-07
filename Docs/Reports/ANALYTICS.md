# Froglet Analytics: where it stands (report for the team and Ian)

**From:** Shombith's sessions, 2026-11-05 to 2026-11-06. **Branch:** everything below is on `bleeding-edge`
(froglet-studio/Cosmic-Shore#1761 and froglet-studio/Cosmic-Shore#1762, both merged 2026-11-06).
**Status:** the server is live, and PostHog's history is in. The game sends to the server from any build made from
`bleeding-edge`. **No match has yet been played in the Unity editor against the live server.** That is the first
check (section 4).

## 1. In one minute

- **Froglet's own analytics server replaces PostHog** (Shombith, 2026-11-05/06, after Ian's emails about #1639).
  - It runs on the Oracle VM at https://analytics.130-210-34-146.sslip.io.
  - It checks every event against `Docs/Analytics/EVENT_SCHEMA.json`, flags the #1639 double-sends (kept, never counted
    twice), and tags editor, port and release builds.
  - It keeps each player's country (two letters). The IP address is never stored.
- **The console** (Frogma Rev 50 B): Overview, Players (a profile per player), Retention, FTUE, Matches, Economy,
  Social, Data health, Ask Claude and Erasure.
  - Player profiles open only for admin keys and the profile readers: Ian, Garrett and Caleb.
  - Claude only ever sees totals that hold no player ids.
- **PostHog is gone from the game.** `FrogletAnalyticsConfig.asset` ships already pointed at the server, with a public
  key that can only send events. Nobody fills a field in.
- **PostHog's history is in.** 66,746 events over 91 days, from early August 2026. Every day matched PostHog's own count.
  The importer stores no IP address, and running it again stores nothing twice.
- **The server updates itself every night at 04:20 UTC** from `bleeding-edge`. New code runs the server's tests first:
  if they fail, it keeps the code that was running.

## 1.5 Rev 65, built (2026-11-06)

Shombith picked all seven Rev 65 screens (`Docs/MENU_REDESIGN_B.md` section 141) and asked for switches inside every
chart. Built on `claude/sweet-mayer-se51r6`:
- **Every chart has its own switches:** form (bars / line / pie / table), measure, and split (play type, era, platform,
  country, mode, vessel, build, intensity), from one endpoint, `/v1/series`.
- **Players:** filters, sorting, saved segments and CSV. **Profile:** one long page, every old section plus charts.
- **Cohorts:** lifespans and return by week. **Play types:** solo vs AI, solo and multiplayer side by side.
- **Eras:** the plan chart. Old PostHog data (told apart exactly, by source), Pre-beta editor testing and the planned
  betas. An Era filter on every page, and any two eras compared.
- **Admins:** Query Studio (saved queries and nightly tables, read-only over the totals) and Dashboards.
- **Rev 66** (section 142) has more ideas drawn, awaiting a pick: Live now, Alerts, Funnels, Release compare, Player compare.

## 1.6 Since Rev 65 (2026-11-06 to 2026-11-07)

Merged: themes (#1763), key fixes and every key admin (#1765, #1776), comma lists removed (#1772), Toybox, elements and
saved views (#1777), match setups (#1778). On `claude/sweet-mayer-se51r6` (the PR after #1778):
- **The game sends**:
  - `session_started` (and length on `session_ended`);
  - score, placement, AI difficulty and behind-by on `game_completed`;
  - `match_abandoned`;
  - invite ids;
  - `client_exception`, which carries no message text.
  - All of these are Froglet-only.
- **New pages**: Sessions (DAU/WAU/MAU, stickiness, length), Balance, Quits (with the frustration chain), Errors, and
  Invites on Social.
- **The Notebook**: notes, SQL, charts and questions to Claude, private or shared, with a Report view.
- **The UI pass**: a grouped nav, a phone menu and a sticky filter bar.
- **Backups**: a nightly local snapshot at 04:40 UTC (`analytics_server.py backup`). The off-VM copies come from
  Oracle's boot-volume backup policy (daily 05:00 UTC, 5 kept), set in the Oracle console. No key is on the VM.
- **Decided**:
  - dashboard email dropped (Shombith, "Leave emails");
  - device/perf and multiplayer health later;
  - ability use left out;
  - no purchase event while commerce is de-scoped.
- **Open**:
  - engine-port builds rarely reach a match end (the port has no scene transitions yet), so `match_abandoned` now
    measures it. A port match run with `--session-report` would confirm it.
  - Whether the party invite line should carry its own invite id is a party-protocol change: ask Shombith.

## 2. Where everything is

| What | Where |
|---|---|
| Server, tests, setup, self-update, PostHog import | `Tools/Analytics/server/` (README there) |
| Console | `Tools/Analytics/console/`; a review copy: https://claude.ai/artifact/4VXqkw5287tfjhdxSqMbRb |
| Game side | `Assets/_Scripts/System/Instrumentation/FrogletAnalyticsSink.cs`, `Assets/Resources/FrogletAnalyticsConfig.asset` |
| Events and their properties | `Docs/Analytics/EVENT_SCHEMA.json` (the server rejects what it does not list) |
| Data, privacy, retention | `Docs/Analytics/DATA_ARCHITECTURE.md` |
| The design picked | Frogma Rev 50 B, `Docs/MENU_REDESIGN_B.md` §137 |
| Hosting options and costs | https://claude.ai/artifact/6GEFxRUjByH1qmQ3RwiBdk |

## 3. What was checked

- Server: `python3 Tools/Analytics/server/analytics_server.py --self-test`, 30 tests. They cover:
  - the PostHog import: no IP stored, each event keeps its own time, nothing stored twice, and ties across a page
    edge neither lost nor repeated;
  - the asset's key matching the server's.
- Game C#: `Tools/Build/unity_refcompile` RESULT OK (player and editor), and `.claude/skills/ship/gates.sh` ALL OK.
- On the VM: the setup ran to 7/7 and the nightly timer is listed. The PostHog import ran: a dry run first
  (`days_short: 0`), then the real one.
- The self-update was tested in a sandbox repo, five cases: up to date, update, failing tests, the branch merged, the
  branch deleted.

## 4. Not yet checked (by hand)

1. **A match in the editor reaches the live server.** Grant consent (FrogletTools > Services > Analytics Consent
   (Dev)) and play one match. Within about 30 s you should be on **Players**, and **Data health → Last event received**
   should read "seconds ago". Also check there is no `[Analytics] Froglet Analytics rejected a batch` warning.
2. **The consent screen's new text** reads "...Froglet's own analytics server... the country you connect from".
3. **The first nightly self-update** (2026-11-07 04:20 UTC): it logs to
   `journalctl -u froglet-analytics-update`, and its test log is `/var/lib/froglet-analytics/last-update-tests.log`.

## 5. Open tasks and the decisions they wait on

- **Read keys for Ian, Garrett and Caleb**: printed on the VM with `analytics_server.py key read <name>`; sent to each
  privately (never in a shared channel).
- **The three FTUE events in UGS**: `ftue_step_completed`, `ftue_phase_completed` and `ftue_completed` must be declared
  in the UGS Event Manager. Our server already takes them.
- **Legal and store labels**: the privacy policy and the App Store / Play data labels must name our own server and the
  country, instead of PostHog (`DATA_ARCHITECTURE.md` §8.11). Needed before the next store release.
- **PostHog stays on until old builds are gone.** Builds already in players' hands still send there. When they have
  died out, run `import-posthog` once more (it adds only what is missing) and shut the PostHog project down.
  *Decision: when.*
- **Own domain (optional)**: `analytics.froglet.studio` takes a DNS record and one setup re-run. `serverUrl` in
  `FrogletAnalyticsConfig.asset` changes in the same commit. *Decision: whether.*
- **Froglet Compute is paused** while analytics has the priority (Shombith, 2026-11-06). *Decision: when it comes back.*

## 6. What cost time (so nobody pays it twice)

- **PostHog refuses `OFFSET` on queries made with a personal API key** (HTTP 400, `hogql_query_error`). Page by
  `timestamp`, and read in full the timestamp a full page ends on. A fake API in a test must refuse what the real one
  refuses: ours didn't, so the tests passed and the first real run failed.
- **`/var/lib/froglet-analytics` is mode 700**, so any check of a path inside it must run as the service user. Run as
  `ubuntu`, `[ -d ... ]` says "missing", and the setup tried to clone over the checkout.
- **GitHub deletes a head branch when its PR merges.** A download or a `git fetch` naming that branch then gets a 404,
  even with a working token. Point at `bleeding-edge` after a merge; the self-update does this by itself.
- **A fine-grained token can return 404 from `raw.githubusercontent.com`.** Download through the contents API instead,
  with the header `Accept: application/vnd.github.raw`.

## 7. Generated from git and the tickets

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-07 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area analytics --days 14`._

### Recent changes (25+)
- 2026-11-07 `088b38f63` feat(analytics): Sessions, Balance, Quits, Invites, Errors, the Notebook, nightly backups and a UI pass — on another branch
- 2026-11-07 `a98f9972d` feat(analytics): game sends sessions, balance, early quits, invite attribution and client errors — on another branch
- 2026-11-06 `de65daf1d` wip(analytics): UI pass in progress - not verified — on another branch
- 2026-11-06 `80e84bb8e` wip(analytics): game events - sessions, balance fields, match_abandoned, invite attribution, client_exception (refcompile OK, gates OK, edit-mode tests not run) — on another branch
- 2026-11-06 `6e5ac962a` wip(analytics): server pages (sessions, balance, quits, invites, errors), backups - game events still in progress, not verified — on another branch
- 2026-11-06 `16e7b70c3` wip(analytics): notebook done; sessions, balance, quits, invites, errors, backups and game events in progress, not tested — on another branch
- 2026-11-06 `ea64b2e75` wip(analytics): sessions, balance, quits, invites, errors, backups, notebook - in progress, not tested — on another branch
- 2026-11-06 `7fbf60eda` feat(analytics): match setups in Overview - competitive, teams, teams vs AI, and solo by how many AI — on bleeding-edge, bleeding-edge
- 2026-11-06 `102074e1f` wip(analytics): match setups section (server and console) - in progress, not tested — on bleeding-edge, bleeding-edge
- 2026-11-06 `c88241a27` wip(analytics): match setups (team composition, setups breakdown) - in progress, not compiled or tested — on bleeding-edge, bleeding-edge
- 2026-11-06 `a64bd5b8d` docs(analytics): schema for team composition on game_started (Ian's fields + AI per domain) — on bleeding-edge, bleeding-edge
- 2026-11-06 `9c12040f9` feat(analytics): elements on every match, the Toybox's history and vessels, saved views — on bleeding-edge, bleeding-edge
- 2026-11-06 `18be5a5d3` wip(analytics): server and console for elements, Freestyle and saved views - in progress, not tested — on bleeding-edge, bleeding-edge
- 2026-11-06 `762442567` wip(analytics): elements, Freestyle events, saved views - in progress, NOT compiled or tested — on bleeding-edge, bleeding-edge
- 2026-11-06 `2fe08aac5` docs(analytics): schema for match elements and Freestyle (Toybox) play, Froglet-only — on bleeding-edge, bleeding-edge
- 2026-11-06 `deae03424` feat(analytics): every key is an admin's (all_keys_admin, on by default) — on bleeding-edge, bleeding-edge
- 2026-11-06 `9e4d8500b` fix(analytics): keys say what they are - no read key named "admin", key list, key delete — on bleeding-edge, bleeding-edge
- 2026-11-06 `0ec313fec` fix(analytics): no more comma lists - ranked bars and tables instead — on bleeding-edge, bleeding-edge
- 2026-11-06 `cab323049` feat(analytics): the Rev 65 console - every chart switchable, seven new screens — on bleeding-edge, bleeding-edge
- 2026-11-06 `9e3318bc2` wip(analytics): console for Rev 65, in progress (not tested yet) — on bleeding-edge, bleeding-edge
- 2026-11-06 `e18f0524b` feat(analytics): server for Rev 65 - eras, chart series, cohorts, play types, Query Studio, dashboards — on bleeding-edge, bleeding-edge
- 2026-11-06 `60ba8b432` Thresher — on bleeding-edge, bleeding-edge
- 2026-11-06 `4a729d91d` feat(analytics): the read API filters by play type; the players list carries each player's play mix — on bleeding-edge, bleeding-edge
- 2026-11-06 `767f57c2d` fix(analytics): new keys work without a restart; the key screen can be typed into again — on bleeding-edge, bleeding-edge
- 2026-11-06 `1ce4897ef` fix(analytics): themes are the game's colour sets and change the whole UI — on bleeding-edge, bleeding-edge

### Also in flight on other branches
- `claude/sweet-mayer-se51r6` (unattributed, 2026-11-06): wip(analytics): UI pass in progress - not verified
- `claude/launch-build-stamp` (unattributed, 2026-11-06): merge: bleeding-edge (15f1d3ae0b, #1736) into claude/launch-build-stamp before the PR

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->
