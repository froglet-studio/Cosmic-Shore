# Team and work areas: who works where, and how we build on each other's work

**Read before working anywhere in the project.** Cosmic Shore is built by a small team, and nearly all of
the work goes through Claude sessions: about 85% of the commits in the last 60 days are authored by Claude
(3,213 of 3,787). Git therefore rarely says which person drove a change, and nobody sees the others'
work until it lands. The rules below make every Claude session aware of the other collaborators' work, so
that it builds on their ideas instead of overriding them or reinventing them.

## The people

The core collaborators are **Garrett, Caleb, Shombith and Yash**. The roles below are measured from git
(pull requests merged plus each person's own commits, last 60 days, 2026-10-15). They are not job titles.
Correct them in [`TEAM.json`](TEAM.json), which also maps each person's git names and emails.

| Person | Works on (measured) | Git identity |
|---|---|---|
| **Garrett** | Owner of `bleeding-edge`. He reviews and merges most pull requests: vessels, tests, environment / ecology, arcade modes, impact effects, materials, skills. His own work: AI training, crystal hull fusion, the Regatta AI roadmap, the multiplayer (MPPM) roadmap, the labs (Omni Motion / Shepard / Combine, Thresher Plant Lab). Most `cece/*` sessions are merged by him; recent ones built the Thresher and the Stoat as their own vessels (#1076, #1079, #1085). | Garrett Milliron, garrett@froglet.studio |
| **Caleb** | QA sessions (`Docs/QA/RESULTS`), the arcade mode map and previews, card art; merges some `claude/*` work (Skim Race race rings, #1096). | Caleb Steinhagen / FenrysUnchained |
| **Shombith** | **Heads tooling**: the Amoebius engine, editor tools, the Claude skills such as `/ship`, and the hooks (Shombith, 2026-10-23). **Owns all cloud and backend services and data management** (UGS: Cloud Save and its data such as `HangarCloudData`, authentication, the backend; Shombith, 2026-10-18). UI: `Menu_Main`, the UI prefabs, colour tokens, the Codex; multiplayer scenes. He drives the **Amoebius engine** milestones (`Port/src/CosmicShore.Engine`, `Player`, tests, online play) and merges those pull requests (`cece/focused-planck-*`). | Shombith03, shombith@froglet.studio |
| **Yash** | The Bug Hunt; party / multiplayer (UGS) hardening (the cloud and backend layer itself is Shombith's); diagnostics (redlog); leaderboards; the **Vessel Studio** (the artifact, studio pages, Sync panel); the **Amoebius launcher** (VESSEL STUDIO page, studio server, artifact library). Branches `Ys-*`, `vessel-studio*`, `amoebius-*`. | Yash Sadhukhan / YsKhan61 |
| Andrew | QA sessions and results. | Andrew Mokhtary (Soncub) |
| aradia1 | Audio and haptics (FMOD project, drift / boost sounds), vessel feel. | unconfirmed |
| dbrutus | Arcade and multiplayer code, tests. | unconfirmed |
| studyholic | Skim Race AI hand-play runs, prism animation docs. Whose machine this is is unknown. | unconfirmed |
| Grok Bot | An automated agent, not a person; its commits came in through #1078. | unconfirmed |

`claude/*` and `cece/*` branches are Claude Code sessions. The person behind one is whoever merges or pushes
it; `who_works_where.py` reports a session nobody has merged as unattributed.

## The rules for every session

1. **Look before you touch.** Run `git fetch origin`, then
   `python3 Tools/Build/who_works_where.py query <the paths you will change>` (about 10 s). It lists:
   - **In flight**: unmerged branches with recent work in those paths, and whose they are.
   - **Landed**: pull requests merged into `bleeding-edge` in those paths, who merged them and whose commits
     they hold.
   - **Talk to**: the people whose work is there.
2. **Say it in one or two lines** when someone else is active there: who, which branch or PR, what it does.
   For example: "Garrett's `cece/zen-fermat` made the Stoat its own vessel on 10-13; this builds on its modes."
   Read their branch, docs and skill before writing your own.
3. **Build on it; never override it silently.** If your change would replace, retire, rename or re-tune
   something another collaborator built or decided, stop and ask the person you are working with first. Name
   the owner to check with ("this re-tunes Garrett's play-tuned Stoat poles; check with him"). A design
   choice between two collaborators' ideas belongs to them, not to the session.
4. **Do not reinvent the wheel.** Before you build a new tool, system, skill, studio, lab or doc, look for one
   that exists:
   - the skills list;
   - [`Docs/claude/DOCUMENTATION_INDEX.md`](claude/DOCUMENTATION_INDEX.md);
   - `Tools/Build/`, FrogletTools and `Docs/Studios/`;
   - `who_works_where.py query` on the area.

   Extend what exists, and say so.
5. **Shared numbers are claimed once.** Before taking "the next free" value of an enum member
   (`VesselClassType`, `GameModes`), a learnings ID, a Netcode prefab hash or a GUID, check `bleeding-edge`
   AND the in-flight branches. `who_works_where.py query Assets/_Scripts/Data/Enums` shows who else is adding
   one.
6. **Merge `bleeding-edge` in often, and keep pull requests small.** The longer two branches run apart, the
   more each re-solves what the other already did (below).
7. **Leave a trail.** When work touches another collaborator's area, the commit or PR says so: "Affects:
   Garrett's Thresher HUD". The next session reads the trail; the owner sees it in review.
8. **Read the team activity when you need it** (Yash, 2026-10-15: "stay updated by searching all git repos
   automatically every time someone works on something"). `python3 .claude/hooks/team_activity.py --peek 12` lists
   the commits pushed to ANY branch in the last 12 hours, leaving out the session's own; a commit is marked **HOT**
   when it touches a file this session has worked on. Read a HOT commit before you change that file again, build on
   it, and tell the person. It ran as a hook on every prompt until 2026-10-23, when Shombith switched it off as too
   noisy and too slow; it is now run on request (or from the session deck's Team button). Self-test:
   `python3 .claude/hooks/team_activity.py --self-test`.
9. **Hand work to each other as tickets** (Yash, 2026-10-15: "ask them during their work to make to-dos and
   tests for other collaborators"; Shombith, 2026-10-23 and 2026-10-25: never assigned, branch-tracked, a real ticket
   tool). A session cannot open Unity, sign in to a dashboard or watch a first-timer play, so what its work leaves for
   a person (the editor test, the playtest, the UGS or PostHog step, the missing content, the decision) is a ticket on
   **Froglet Tasks**: a GitHub issue labelled `team-task`, read on the Froglet Tasks page
   (https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f) and written only through the `/team-tasks` skill (`Tools/Build/team_tasks.py`),
   **when the person asks** (or `/ship` does): no hook files, closes or re-ranks anything. The rules:
   - **Nobody's until taken.** A ticket names no one (rule 11); an assignee set on GitHub is cleared by the sweep.
   - **Taking names your branch** (`take <id> --branch <branch>`, or Take on the page). The ticket closes when that
     branch merges into bleeding-edge, and goes back to open, nobody's, when the branch is deleted, released or idle
     for 14 days (`.github/workflows/team-tasks.yml` runs the sweep on every merge, deletion and nightly).
   - **14 days untaken: archived** (closed as not planned, label `archived`). Done tickets close as completed. Both
     come back with `revive`, and the 14 days start again.
   - **One ticket per branch.** Every hand test a branch leaves is a checklist line on its one ticket.
   - **Priority ranks tickets, milestones rank time** (M1 closed testing on all platforms ... M5 full release). A person
     re-ranks with `set` or on the page, and nothing automatic overrides that.
   - **Done needs its proof**: `done <id> --note "<the check>"`; a failed test is not done.
   `/ship` puts `team_tasks.py pr-section` in the PR body. The old markdown board (`TEAM_TASKS.md`) is a pointer now;
   the `team_tasks_nudge.py` hook that pushed tasks into every prompt was switched off on 2026-10-23 and deleted on
   2026-10-25 (Shombith). Self-test: `team_tasks.py --self-test`.
10. **Give a report to whoever asks** (Yash, 2026-10-15: "if someone asks you about a report of some other person
   working on some shared field, you should be able to give those reports to them"). Garrett asking for Yash's
   Regatta AI work, Shombith asking what changed in an area he shares: build the report from the system, never from
   memory. `python3 Tools/Build/team_report.py area <key>` (list: `areas`; any paths: `paths <paths>`; one person:
   `person <name>`) adds what changed (recent commits and where they are, other branches in flight there, open tasks)
   to the area's hand-written report in [`Reports/`](Reports/) (registered in `Reports/areas.json`; `--write` refreshes
   the doc's generated block). Answer in plain words: what was built and why, where it is, how to try it (artifact
   links, Unity steps), what is untested, what decisions wait on the reader. An area somebody works on with others
   gets a row and a report doc when the work starts; keep the hand-written half current when the work moves. The
   `report_request.py` hook reminds a session whenever a prompt asks for a report. Self-tests:
   `team_report.py --self-test`, `.claude/hooks/report_request.py --self-test`.

11. **No favourites among the core four** (Shombith, 2026-10-23). Garrett, Caleb, Shombith and Yash carry equal
   weight. Tasks are never assigned and nobody is a default taker; there are no standing volunteer offers. Never
   let one person's request override another's area without whoever decides there. Shombith heads tooling: the Amoebius engine, editor tools, the Claude
   skills (`/ship` and the rest) and the hooks.

## What happened without this (2026-10-13, one merge, measured)

Merging `bleeding-edge` into `Ys-bleeding-edge` took about 110 conflicts. Most were the same work done twice:

| Collision | What it cost |
|---|---|
| The **Stoat** was built in two places at once: as its own vessel in Unity (`cece/zen-fermat`, #1076) and in the Vessel Studio (rounds 1-15). Bleeding-edge's copy came from a squashed port with no shared history. | 13 files merged by hand. 4 tuning values still differ between the studio and Unity (`studio_to_unity.py --check`). The studio camera and Unity's camera are two separate assets. |
| **`VesselClassType` 14** was taken by both the Thresher and the Stoat. | The Stoat was renumbered to 15 across assets, tests and docs. |
| The engine was **renamed Prisma → Amoebius twice**: once in docs (Yash, 10-09) and once in code (bleeding-edge, 10-12). | Every launcher file conflicted. The new code on one side was renamed by hand onto the other's names. |
| The **same engine APIs were added twice** (transform jobs, render passes). | Duplicate types had to be unified into one. |
| The **black hole tools** were retired on one branch and resurrected by the other's port copy. | 24 files had to be found and re-deleted. |
| **Lab learning IDs** (`L-STU-15..24`, `L-GEN-4`) were taken on both branches. | They were renumbered, and the references outside the merged files went stale. |

## The map (generated)

Refresh: `git fetch origin && python3 Tools/Build/who_works_where.py map --write` (do it in `/reorient` and
`/ship`, or whenever this looks stale).

<!-- who_works_where:map -->
_Generated by `python3 Tools/Build/who_works_where.py map --write` on 2026-10-19 from origin/* (last 45 days). Do not edit by hand._

| Person | Landed areas (pull requests merged or authored, count) |
|---|---|
| **Garrett** | `Docs/UNITY_VERIFICATION_CHECKLIST.md` 25, `Assets/_Scripts/Tests` 24, `Assets/_Scripts/Controller/Vessel` 23, `Docs/claude/ABOUT_PROJECT.md` 15, `.claude/skills/labmaker` 15, `.claude/skills/vessel` 14, `Docs/claude/DOCUMENTATION_INDEX.md` 14, `Assets/Resources/ElementalAbilityMaps` 14 |
| **Yash** | `Docs/TEAM_TASKS.md` 8, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 8, `Docs/BUG_HUNT_HANDOFF_2026-09.md` 8, `Docs/BugHunt/FIX_LOG.md` 8, `Docs/BugHunt/PLAYBOOK.md` 7, `Assets/_Scripts/Controller/AI` 6, `Assets/_Scripts/Controller/Arcade` 6, `Assets/_Scripts/Controller/Vessel` 6 |
| **Shombith** | `Port/tests/CosmicShore.Tests` 8, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 7, `Port/src/CosmicShore.Engine` 7, `Assets/_Scripts/Tests` 6, `Port/CLAUDE.md` 6, `.claude/skills/amoebius` 5, `.claude/skills/asset-surgery` 5, `.github/workflows` 5 |
| **Caleb** | `Assets/_Scripts/Controller/ImpactEffects` 2, `.claude/skills/asset-surgery` 1, `Assets/_Graphics/ARCADE` 1, `Assets/_Prefabs/Environment` 1, `Assets/_Scenes/Multiplayer Scenes` 1, `Assets/_Scripts/Controller/AI` 1, `Assets/_Scripts/Controller/Arcade` 1, `Assets/_Scripts/Controller/Environment` 1 |
| **Grok Bot** | `.claude/hooks/vessel_studio_nudge.py` 1, `.claude/settings.json` 1, `.claude/skills/amoebius` 1, `.claude/skills/amoebius-artifact` 1, `.claude/skills/arcadegame` 1, `.claude/skills/artifact-to-unity` 1, `.claude/skills/labmaker` 1, `.claude/skills/vessel` 1 |
| **Andrew** | `Docs/QA/.applied.json` 1, `Docs/QA/ARCHIVE.md` 1, `Docs/QA/DEV_TASKS.md` 1, `Docs/QA/QA_BACKLOG.md` 1, `Docs/QA/RESULTS` 1 |

**In flight** (unmerged branches updated in the last 45 days, newest first):

| Branch | Who | Updated | Ahead | Touches |
|---|---|---|---|---|
| `cece/charming-wright-4ayoce` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-19 | 2 | `Assets/_Scripts/Utility/PerformanceBenchmark`, `Docs/ANDROID_BUILD.md`, `Assets/_Scripts/Editor/Build`, `Assets/_Scripts/Controller/AI` |
| `cece/sharp-tesla-j0kd7z` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-18 | 30 | `Assets/_Scripts/Controller/Vessel`, `.claude/skills/duo-vessel`, `Assets/_Scripts/Tests`, `Docs/Studios/VesselStudio` |
| `bugledger/gradi` | Garrett | 2026-10-18 | 24 | `BugLedger/shared` |
| `claude/exciting-bell-69b398` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-18 | 1 | `Docs/TEAM_TASKS.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `perf-status` | perf-daily | 2026-10-18 | 1 | `bleeding-edge/PERFORMANCE.md`, `bleeding-edge/status.json` |
| `Ys-bleeding-edge` | Yash | 2026-10-18 | 2 | `Docs/Studios/VesselStudio`, `Docs/Studios/CONCEPT_FLEET.md`, `Docs/TEAM_TASKS.md`, `.claude/skills/labmaker` |
| `bugledger/yskha` | Yash | 2026-10-18 | 31 | `BugLedger/shared` |
| `bugledger/shombith` | Shombith | 2026-10-18 | 9 | `BugLedger/shared` |
| `bugledger/mrglo` | jtgjones | 2026-10-17 | 16 | `BugLedger/shared` |
| `grizzly-v3` | jtgjones | 2026-10-17 | 14 | `Assets/_Scripts/Controller/Vessel`, `Assets/_SO_Assets/VesselActions`, `Assets/_Scripts/Tests`, `Assets/_Scripts/Controller/FX` |
| `bugledger/belac` | Caleb | 2026-10-17 | 8 | `BugLedger/shared` |
| `cece/charming-rubin-ro74tj` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-16 | 6 | `.claude/skills/labmaker`, `Docs/Studios/PUMP_RIDER_LAB.md`, `Docs/Studios/PumpRiderLab.html` |
| `claude/match-replays-5t3dbr` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-16 | 9 | `Assets/_Scripts/Utility/Theater`, `Assets/_Scripts/ScriptableObjects`, `Docs/THEATER.md`, `Assets/_Scripts/System/CloudData` |
| `cece/vibrant-archimedes-n3xn3x` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-16 | 7 | `Assets/_Scripts/Controller/Arcade`, `Assets/_SO_Assets/Games`, `Assets/_SO_Assets/Cell Configs`, `Assets/_Scripts/Controller/Vessel` |
| `claude/youthful-hopper-5t3dbr` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 8 | `Assets/_Scripts/Utility/Theater`, `Assets/_Scripts/ScriptableObjects`, `Docs/THEATER.md`, `Assets/_Scripts/System/CloudData` |
| `claude/serene-edison-lfv24f` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 2 | `Docs/ANDROID_BUILD.md`, `Assets/_Scripts/Editor/Build`, `Assets/_Scripts/Tests`, `Docs/PLATFORM_UNIFICATION.md` |
| `claude/menu-click-through-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 1 | `Assets/_Scripts/Tests`, `Assets/_Scripts/Controller/Multiplayer`, `Assets/_Scripts/System/MainMenuController.cs`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `claude/qa-menu-veil-pause-clarify-gcq7q5` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 1 | `Docs/QA/.applied.json`, `Docs/QA/DEV_TASKS.md`, `Docs/QA/QA_BACKLOG.md`, `Docs/QA/RESULTS` |
| `cece/upbeat-knuth-f873tn` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-15 | 1 | `Docs/Studios/ABTests`, `.claude/skills/ab-test`, `Docs/claude/DOCUMENTATION_INDEX.md` |
| `claude/webgl-prism-animations-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 1 | `Assets/_Scripts/Utility/Effects`, `Assets/_Scripts/Controller/Environment`, `Assets/_Scripts/Controller/ECS`, `Assets/_Scripts/Controller/Vessel` |
| `claude/release-guard-dev-checkbox-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 1 | `Assets/_Scripts/Editor/Build`, `Assets/_Scripts/Tests`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/WEBGL_TOYBOX.md` |
| `claude/compassionate-allen-ta8oa6` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 10 | `Tools/Build/prisma_party_scenarios`, `Port/src/CosmicShore.Engine`, `Docs/MULTIPLAYER_START_HERE.md`, `.claude/skills/labmaker` |
| `claude/store-page-urls-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 2 | `Docs/Legal/README.md`, `Assets/Resources/PrivacyConsentConfig.asset`, `Assets/_Prefabs/UI Elements`, `Assets/_Scripts/UI/Modals` |
| `claude/jolly-maxwell-dgwdpz` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 2 | `Assets/_Graphics/ARCADE`, `Tools/Build/card_backgrounds_manifest.json`, `Tools/Build/render_card_backgrounds.py`, `Tools/Build/card_art_harness` |
| `feat/red-console-logger` | Yash | 2026-10-14 | 1 | `Assets/_Scripts/Utility/PerformanceBenchmark` |
| `cece/determined-goodall-o1klfo` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 1 | `Assets/_Scripts/Controller/Toys`, `Assets/_Scripts/ScriptableObjects`, `Assets/_SO_Assets/Toys`, `Assets/_Scenes/Menu_Main.unity` |
| `cece/gracious-pasteur-9z5e9t` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 3 | `Tools/Build/converter_reel`, `Docs/Studios/CONVERTER_REEL_LAB.md`, `.claude/skills/labmaker`, `Docs/Studios/ConverterReelLab.html` |
| `claude/brave-feynman-g71arn` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 1 | `Assets/_Scripts/Controller/Vessel`, `.claude/skills/vessel`, `Assets/_Prefabs/Spacevessels`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `fix/weekly-challenge-reset` | Yash | 2026-10-13 | 1 | `UGS/Leaderboards`, `UGS/README.md` |
| `claude/intelligent-mendel-8jdfvr` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 1 | `Assets/_Scripts/System/Squads`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `claude/modest-sagan-0ybcgo` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 3 | `Assets/_SO_Assets/Pilots`, `Assets/_Scripts/Data`, `Assets/_SO_Assets/Classes`, `Assets/_Scripts/ScriptableObjects` |
| `cece/nice-brahmagupta-ojclij` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-13 | 40 | `Assets/_Scripts/Utility/PerformanceBenchmark`, `Assets/_Scripts/Tests`, `Docs/PartySystem/UI.md`, `Assets/_Scripts/Utility/Replay` |
| `fix/vessel-prefab-missing-ref` | Yash | 2026-10-10 | 1 | `Assets/_SO_Assets/Vessel Prefab Container.asset`, `Assets/_Scripts/Controller/Multiplayer`, `Assets/_Scripts/ScriptableObjects`, `Tools/Build/author_stoat_assets.py` |
| `claude/peaceful-rubin-hhw49n` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 40 | `Assets/_Scripts/Controller/Party`, `Docs/SKIM_RACE_AI.md`, `Docs/Studios/VesselStudio`, `Assets/_Scripts/Utility/PerformanceBenchmark` |
| `claude/trusting-curie-bn6rrn` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 8 | `Docs/BranchArchive/small`, `Docs/BranchArchive/large`, `Docs/BranchArchive/medium`, `Docs/BranchArchive/README.md` |
| `claude/tandava-severing` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 8 | `Assets/_Scripts/Controller/Arcade`, `Tools/Build/swarm_core_harness`, `Assets/_SO_Assets/Swarm Fauna`, `Assets/_SO_Assets/Games` |
| `cece/charming-cerf-alf1j1` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-09 | 2 | `Assets/_Scripts/Controller/Environment`, `Assets/_Graphics/Materials`, `Assets/_Prefabs/Spawnables`, `Assets/_SO_Assets/Cell Configs` |
| `prisma-relay-transport` | Yash | 2026-10-09 | 1 | `Port/src/CosmicShore.Online`, `Port/tests/CosmicShore.Tests`, `Port/src/CosmicShore.Engine`, `Port/src/CosmicShore.Player` |
| `claude/busy-lamport-kdwkae` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-08 | 2 | `Tools/Build/unity_refcompile`, `.claude/skills/asset-surgery` |
| `claude/optimistic-brown-g45ivk` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-08 | 1 | `Port/CLAUDE.md`, `Port/docs/ARCHITECTURE.md`, `Port/src/CosmicShore.Mcp`, `Port/tools/check_smoke_concurrency.py` |
| ... 69 more | | | | |
<!-- /who_works_where:map -->

## Decisions about our tools (2026-10-19)

Recorded in [`TOOLING_DECISIONS.md`](TOOLING_DECISIONS.md) §6 by Shombith, who started and drives the engine:
- **Amoebius stays**: a long-term goal of Froglet's own engine, so the team can one day leave Unity. Not plausible now
  (legal challenges, porting), so the game ships on Unity. The engine is focused solely on the game.
- **The Vessel Studio is an artifact, not part of the engine.** Whether the artifact itself stays is still open; Yash
  drives it.
- **No single tool unifying everything**: too costly.
- Proposed: an artifact-storing webpage for the synced artifacts anyone thinks are important.

Sessions no longer ask whether these tools helped. [`TOOLING_FEEDBACK.md`](TOOLING_FEEDBACK.md) keeps what people
volunteer.

**Notices.** `team_context.py` used to show the person a team notice on a session's first prompt, and the tools
decision on the first prompt about the engine or the tools. Shombith switched it off on 2026-10-23 with
`team_activity.py` and `team_tasks_nudge.py` (deleted 2026-10-25): together they added about 7 KB to prompts and sent
sessions off to fetch and query before answering. `vessel_studio_nudge.py` (once per session, on vessel work) and
`report_request.py` (when a prompt asks for a report) still run. The Froglet Tasks page
(https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f) is the board for the team's tickets (rule 9).
