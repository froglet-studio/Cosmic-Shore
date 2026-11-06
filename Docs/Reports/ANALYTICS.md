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
_Generated 2026-11-06 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area analytics --days 14`._

### Recent changes (25+)
- 2026-11-06 `6c8e60723` fix(analytics): PostHog import pages by timestamp, waits on rate limits, checks counts — on bleeding-edge, bleeding-edge
- 2026-11-06 `d81336e20` fix(analytics): setup re-run checks the private checkout as the service user — on bleeding-edge, bleeding-edge
- 2026-11-06 `3922062e6` feat(analytics): the game sends to Froglet's own server; PostHog removed — on bleeding-edge, bleeding-edge
- 2026-11-06 `e367d8d64` feat(launch): build stamp - every build knows who it is; release builds talk to UGS production — on another branch
- 2026-11-05 `54b87044b` fix(analytics): the VM setup can move its single-branch clone to another branch — on bleeding-edge, bleeding-edge
- 2026-11-05 `0cc1674e2` feat(analytics): the VM setup makes read keys for the profile readers — on bleeding-edge, bleeding-edge
- 2026-11-05 `da531adba` feat(analytics): player profiles, country, FTUE, economy and social pages — on bleeding-edge, bleeding-edge
- 2026-11-05 `e06da5642` feat(analytics): a review copy of the console that needs no server — on bleeding-edge, bleeding-edge
- 2026-11-05 `5cd618f72` feat(analytics): the Froglet Analytics console page (Frogma Rev 50 B) — on bleeding-edge, bleeding-edge
- 2026-11-05 `8fbeb4617` feat(analytics): our own analytics server to replace PostHog; Rev 50 B picked — on bleeding-edge, bleeding-edge
- 2026-11-01 `1597b76ef` Debug: multiplayer overlay (Ctrl+Shift+M, Editor/dev builds only) — on bleeding-edge, bleeding-edge
- 2026-10-31 `f92309b19` feat(escort): every voyage its own - seeded worlds, variants and modifiers — on bleeding-edge, bleeding-edge
- 2026-10-31 `1704c873b` fix(corridor): a failed stand never skips a route world; destroyed cell items no longer throw — on bleeding-edge, bleeding-edge
- 2026-10-31 `f129ed594` fix(escort): the Dolphin blasts the creature it claims, not the Ark-shaped centroid — on bleeding-edge, bleeding-edge
- 2026-10-31 `c7001d212` tune(escort): pacify only hostile worlds; 25k claim and 40 s in the nucleus — on bleeding-edge, bleeding-edge
- 2026-10-29 `99c90860e` docs(analytics): add match health as query 6 (6a by platform, 6b by play type and intensity) — on bleeding-edge, bleeding-edge
- 2026-10-29 `e4e905f81` docs(analytics): add player funnel as query 5 — on bleeding-edge, bleeding-edge
- 2026-10-29 `008f63df6` docs(analytics): add completion-by-mode query with historical mode names merged — on bleeding-edge, bleeding-edge
- 2026-10-29 `28f86b226` docs(analytics): exclude engine-port build from rematch query — on bleeding-edge, bleeding-edge
- 2026-10-29 `cce8ec638` docs(analytics): propose team-composition fields on game_started (patch, not applied) — on bleeding-edge, bleeding-edge
- 2026-10-29 `76c0c2b25` docs(analytics): add an editor-traffic toggle to the replay queries — on bleeding-edge, bleeding-edge
- 2026-10-29 `e099e7d04` docs(analytics): add repeatable replay-retention queries for PostHog — on bleeding-edge, bleeding-edge
- 2026-10-29 `52f38975d` fix(bootstrap): tear down stale subscriptions on lazy-singleton re-construction — on bleeding-edge, bleeding-edge
- 2026-10-27 `6e9cc954f` feat(meerkat-studio): round 16 - the Omnicrystal ability digs a 3D labyrinth through soil; the Gopher is removed (artifact only) — on bleeding-edge, bleeding-edge
- 2026-10-27 `08d9fa595` feat(ark): the Squads tab as the deck (Rev 31, layout C) — on bleeding-edge, bleeding-edge

### Also in flight on other branches
- `claude/launch-build-stamp` (unattributed, 2026-11-06): merge: bleeding-edge (15f1d3ae0b, #1736) into claude/launch-build-stamp before the PR

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->
