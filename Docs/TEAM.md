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
| **Caleb** | **Heads FTUE with Shombith**, inside the activation work Shombith leads; no one else is on it (Shombith, 2026-10-26). QA sessions (`Docs/QA/RESULTS`), the arcade mode map and previews, card art; merges some `claude/*` work (Skim Race race rings, #1096). | Caleb Steinhagen / FenrysUnchained |
| **Shombith** | **Heads tooling**: the Amoebius engine, editor tools, the Claude skills such as `/ship`, and the hooks (Shombith, 2026-10-23). **Leads activation** (onboarding, FTUE, the first-run flow), with Caleb heading FTUE; no one else is on it (2026-10-26). **Owns all cloud and backend services and data management** (UGS: Cloud Save and its data such as `HangarCloudData`, authentication, the backend; Shombith, 2026-10-18). UI: `Menu_Main`, the UI prefabs, colour tokens, the Codex; multiplayer scenes. He drives the **Amoebius engine** milestones (`Port/src/CosmicShore.Engine`, `Player`, tests, online play) and merges those pull requests (`cece/focused-planck-*`). | Shombith03, shombith@froglet.studio |
| **Yash** | The Bug Hunt; party / multiplayer (UGS) hardening (the cloud and backend layer itself is Shombith's); diagnostics (redlog); leaderboards; the **Vessel Studio** (the artifact, studio pages, Sync panel); the **Amoebius launcher** (VESSEL STUDIO page, studio server, artifact library). Branches `Ys-*`, `vessel-studio*`, `amoebius-*`. | Yash Sadhukhan / YsKhan61 |
| **Iggy** | Wrote much of the game's foundation (`Assets/_Scripts/_Core`, `Game`, `App`; 2022-04 to 2025-12). An **approver** with the core four (Shombith, 2026-10-25). | KidIgarus, isaiah.merrill@gmail.com |
| Andrew | QA sessions and results. | Andrew Mokhtary (Soncub) |
| aradia1 | Audio and haptics (FMOD project, drift / boost sounds), vessel feel. | unconfirmed |
| dbrutus | Arcade and multiplayer code, tests. | unconfirmed |
| studyholic | Skim Race AI hand-play runs, prism animation docs. Whose machine this is is unknown. | unconfirmed |
| Grok Bot | An automated agent, not a person; its commits came in through #1078. | unconfirmed |

`claude/*` and `cece/*` branches are Claude Code sessions. The person behind one is whoever merges or pushes
it; `who_works_where.py` reports a session nobody has merged as unattributed.

**Attribution audit (2026-10-27): Yash's work took no credit from anyone.** Across all 553 branches and the
full history, no commit, line, area owner or role moved credit from another collaborator to Yash
([`Reports/ATTRIBUTION_AUDIT.md`](Reports/ATTRIBUTION_AUDIT.md)). Anyone can audit anyone:
`python3 Tools/Build/attribution_audit.py --person <name>`.

## The rules for every session

1. **Look before you touch.** Run `git fetch origin`, then
   `python3 Tools/Build/who_works_where.py query <the paths you will change>` (about 10 s). It lists:
   - **Owner**: the owner `Docs/TEAM.json` declares for the area, when the owner has said so. **This decides who to
     tell, and whose queue a change goes into (rule 13).** The counts below credit whoever MERGED a pull request, so work that reached `bleeding-edge` through
     someone's integration branch reads as theirs: FTUE came in through `Ys-bleeding-edge`, read as Yash's, and a
     session wrote "FTUE owner" on a task (2026-10-18) when Shombith leads activation (2026-10-26). Add an `owners`
     entry to `Docs/TEAM.json` whenever an owner tells you an area is theirs.
   - **In flight**: unmerged branches with recent work in those paths, and whose they are.
   - **Landed**: pull requests merged into `bleeding-edge` in those paths, who merged them and whose commits
     they hold.
   - **Talk to**: the people whose work is there.
2. **Say it in one or two lines** when someone else is active there: who, which branch or PR, what it does.
   For example: "Garrett's `cece/zen-fermat` made the Stoat its own vessel on 10-13; this builds on its modes."
   Read their branch, docs and skill before writing your own.
3. **Build on it; never override it silently - but only a collision waits** (Garrett, 2026-11-10: "only slow
   people down when they collide with a swimlane"). Run `python3 Tools/Build/approvals.py check --me <person>`
   before you commit:
   - **A collision stops you.** The change is about a swim lane (rule 12) someone ELSE occupies right now. Tell the
     person you work with in one line (who, which branch, PR, last push) before going on; they talk to the occupant
     or decide to go ahead.
   - **Anything else goes through, out loud.** Replacing, retiring, renaming or re-tuning something another
     collaborator built or decided, or editing an area they own, no longer waits for their yes. Tell your person
     whose toes it is on, in a line, so they talk ("this re-tunes Garrett's play-tuned Stoat poles; worth a word with
     him"), write the "Affects:" line (rule 7), and queue it for that collaborator's after-the-fact approval (rule 13).
   A design choice between two collaborators' ideas still belongs to them: the queue is how the one who was not asked
   gets their say.
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
   more each re-solves what the other already did (below). **`/ship` merges its PR on a GO** (Garrett, 2026-11-10:
   "it is becoming fast to batch test in bleeding edge and fix what doesn't work than switch to each branch for
   testing"): shipped work is tested together on `bleeding-edge`, and what fails is fixed on a new branch (`/ship`
   §5.7; "PR only" opts out).
7. **Leave a trail.** When work touches another collaborator's area, the commit or PR says so: "Affects:
   Garrett's Thresher HUD". The next session reads the trail; the owner sees it in review.
8. **Read the team activity when you need it** (Yash, 2026-10-15: "stay updated by searching all git repos
   automatically every time someone works on something"). `python3 .claude/hooks/team_activity.py --peek 12` lists
   the commits pushed to ANY branch in the last 12 hours, leaving out the session's own; a commit is marked **HOT**
   when it touches a file this session has worked on. Read a HOT commit before you change that file again, build on
   it, and tell the person. It ran as a hook on every prompt until 2026-10-23, when Shombith switched it off as too
   noisy and too slow; it is now run on request (or from the session deck's Team button). Self-test:
   `python3 .claude/hooks/team_activity.py --self-test`.
9. **Hand work to each other on the swim lanes board** (Yash, 2026-10-15: "ask them during their work to make to-dos
   and tests for other collaborators"; Shombith, 2026-11-20: Froglet Tasks retired for Swim Lanes). A session cannot
   open Unity, sign in to a dashboard or watch a first-timer play, so what its work leaves for a person goes where the
   board reads it: an editor hand test in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (the lane shows it as work waiting),
   and a lane left for someone through `/handoff-to-<person>`, `/handoff-open` or `/handoff-for-test`
   (`Tools/Build/handoff.py`, `.claude/skills/swimlanes/HANDOFF.md`), only once its person says yes. A hand-off names
   no assignee (rule 11): it is a heads-up with a brief, and the recipient can hand it straight back to open.
   **History:** from 2026-10-15 to 2026-11-20 hand-offs were Froglet Tasks tickets (GitHub issues labelled
   `team-task`, the `/team-tasks` skill and `Tools/Build/team_tasks.py`, a claude.ai page and a nightly sweep). The
   issues stay on GitHub as the record; the tool, skill and sweep were removed.
   **An area's owner is not an assignee.** Hand-offs still name nobody; owners are who a session TELLS when it
   changes their area, and whose approval queue the change goes into (rule 13); `who_works_where.py query` prints the
   owner `Docs/TEAM.json` declares (`owners`). **Anything on UGS or the backend** (Cloud Code, Cloud Save, Access
   Control, Moderation, authentication, dashboards, deploys) **is Shombith's** (Shombith, 2026-10-26): a code change
   there goes through and into his queue like any other, but **posting to the live backend** (a deploy, a policy, a
   write to production data) is an action on a running service, not a change on a branch, and still goes through
   Shombith first. For now Yash does the Cloud Code work, and may set up the AI flight data.
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
   let one person's request override another's area silently: whoever decides there hears of it through their queue
   (rule 13) and can ask to talk. Shombith heads tooling: the Amoebius engine, editor tools, the Claude
   skills (`/ship` and the rest) and the hooks.
12. **Find your lane with `/swimlanes`, and stay out of an occupied one** (Garrett, 2026-11-08: "instead of blocking
   people because someone else worked on that area in the past, inform people that that swim lane is currently
   occupied"). A lane is anything someone holds in their head that takes more than a couple of prompts and one pull
   request to finish: a vessel (the Thresher), a game (Bloomrush), an app-shell section (the Voyages map) or a
   mechanical system (ecology, netcode), never a task. **Everything that size gets a lane** (Caleb, 2026-11-11: "the
   whole point of tracking them is to help us coordinate our efforts"): every vessel on the roster and every shipped
   game card is one automatically; a shell section or system nobody's lane covers is added to
   `.claude/skills/swimlanes/lanes.json` in the same commit that starts multi-PR work on it, and archived there once
   it is truly closed out. Keep the list usable: add a system when work on it can collide, not for every folder. `python3 .claude/skills/swimlanes/swimlanes.py` lists open lanes with obvious work on top,
   occupied lanes under them (unmerged work pushed in the last 7 days, with who and which branch), then lanes that
   need polish, then polished ones. **An occupied lane is the one thing that stops a change** (rule 3); past work is
   never a block. Who worked in an open lane before is context to build on, and if your change re-tunes or replaces
   what they built it goes into their queue (rule 13) rather than waiting for them. A lane opens when its work merges
   into `bleeding-edge`. `approvals.py check` reads the lanes for you: a change ABOUT an occupied lane (a quarter of
   its files, or three) collides; one that only brushes it is a heads-up to the occupant.
13. **Approve after the fact: `/approve`** (Garrett, 2026-11-10). Each person has a queue of changes other sessions
   made to their work: `Docs/Approvals/<Person>/`, one small file per change, committed with the change on its branch
   (`python3 Tools/Build/approvals.py add --to <them> --by <you> --area "..." --summary "..."`). Queue one for every
   person `check` names whose work your change actually CHANGES (re-tunes, replaces, retires, renames, decides
   otherwise, or edits an area they own); a pure addition beside their work needs none, and when unsure, queue it.
   The person runs `/approve` to retrieve their list (it reads every branch, so an entry shows before its change
   merges) and answers each: **approve** (they agree, on the record), **discuss** (they want to talk to whoever made
   it; that person sees it under `approvals.py list --from <them>`), and **resolve** once they have talked. A change
   they want undone is new work like any other. Files and format: [`Approvals/README.md`](Approvals/README.md).
   Self-test: `approvals.py --self-test`.

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
_Generated by `python3 Tools/Build/who_works_where.py map --write` on 2026-11-23 from origin/* (last 45 days). Do not edit by hand._

| Person | Landed areas (pull requests merged or authored, count) |
|---|---|
| **Garrett** | `Docs/Approvals/Yash` 56, `Tools/Compute/evaluators` 56, `Assets/_Scripts/Controller/Arcade` 52, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 51, `Assets/_SO_Assets/VesselActions` 34, `Assets/_Scripts/Tests` 30, `Assets/_Scripts/Controller/Vessel` 28, `Assets/_Prefabs/Spacevessels` 26 |
| **Shombith** | `Docs/MENU_REDESIGN_B.md` 9, `Assets/_Scripts/Tests` 9, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 8, `Assets/_Scripts/ScriptableObjects` 8, `Assets/_Scripts/UI/Views` 7, `.github/workflows` 7, `Assets/_Scripts/Controller/Voyages` 7, `Docs/Approvals/Garrett` 7 |
| **Yash** | `Docs/UNITY_VERIFICATION_CHECKLIST.md` 5, `Docs/Approvals/Shombith` 5, `Assets/_Scripts/Controller/Arcade` 4, `Assets/_Graphics/Materials` 4, `Assets/_SO_Assets/VesselActions` 4, `Assets/_Scripts/Controller/Environment` 4, `Assets/_Scripts/Controller/Multiplayer` 4, `Assets/_Scripts/Controller/Vessel` 4 |
| **Caleb** | `Docs/EditorChurn/TRACKER.md` 6, `Docs/Approvals/Shombith` 3, `Docs/Approvals/Yash` 2, `Docs/Approvals/Garrett` 1, `Tools/Build/check_material_versions.py` 1, `Tools/Build/editor_churn.py` 1, `.gitattributes` 1, `Tools/Build/check_meta_files.py` 1 |
| **dependabot[bot]** | `.claude/skills/launch-flow` 1, `.github/CODEOWNERS` 1, `.github/workflows` 1, `.gitignore` 1, `Assets/Resources/FpsCounterConfig.asset` 1, `Assets/Resources/FpsCounterConfig.asset.meta` 1, `Assets/Resources/LaunchFlagsConfig.asset` 1, `Assets/Resources/LaunchFlagsConfig.asset.meta` 1 |
| **aradia1** | `Assets/_Prefabs/Spacevessels` 2, `Assets/Resources/SwitchbackPathRay.mat` 1, `Cosmic Shore/.cache` 1, `Cosmic Shore/.unsaved` 1, `Cosmic Shore/Assets` 1, `Cosmic Shore/Build` 1, `Cosmic Shore/Metadata` 1, `.claude/skills/sound-brief` 1 |

**In flight** (unmerged branches updated in the last 45 days, newest first):

| Branch | Who | Updated | Ahead | Touches |
|---|---|---|---|---|
| `Ys-bleeding-edge` | Yash | 2026-11-24 | 3 | `Assets/_Scripts/Controller/Arcade`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/AI_SYSTEM/LEARNED_DRIVES_Bloomrush.md`, `Docs/AI_SYSTEM/LEARNED_DRIVES_Broadside.md` |
| `bugledger/yskha` | Yash | 2026-11-24 | 40 | `BugLedger/shared`, `Library/VP` |
| `grizzly-v3-merge` | jtgjones | 2026-11-23 | 1 | `Assets/_Scripts/Controller/Vessel`, `Assets/_Scripts/Tests` |
| `bugledger/mrglo` | jtgjones | 2026-11-23 | 27 | `BugLedger/shared` |
| `claude/project-thread-lslakw` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 3 | `Assets/_SO_Assets/Games`, `Docs/MenuRedesignB/board`, `Docs/MenuRedesignB/rounds`, `Docs/MENU_REDESIGN_B.md` |
| `bugledger/gradi` | Garrett, Yash | 2026-11-23 | 40 | `BugLedger/shared` |
| `bugledger/shombith` | Shombith, Yash | 2026-11-24 | 40 | `BugLedger/shared`, `Library/VP` |
| `launch-status` | launch-status | 2026-11-23 | 11 | `status.json`, `components/launch-flags.json`, `history/launch-flags.jsonl`, `components/windows-prerelease.json` |
| `bugledger/belac` | Caleb, Yash | 2026-11-23 | 16 | `BugLedger/shared` |
| `claude/retain-atlas-done` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 2 | `Docs/LoopAtlas/model`, `Docs/LoopAtlas/personas`, `Docs/LoopAtlas/BOARD.md`, `Docs/LoopAtlas/ledger.jsonl` |
| `claude/project-thread-vlkieg` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 17 | `Assets/_SO_Assets/Cell Configs`, `Assets/_Prefabs/Spawnables`, `Tools/Compute/evaluators`, `Assets/_Scripts/Controller/Arcade` |
| `dependabot/github_actions/github-actions-56fda48312` | dependabot[bot] | 2026-11-23 | 1 | `.github/workflows` |
| `perf-status` | perf-daily | 2026-11-23 | 31 | `bleeding-edge/PERFORMANCE.md`, `bleeding-edge/status.json` |
| `claude/project-thread-6znms8` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 1 | `Assets/_Scripts/Tests`, `Assets/_Scripts/Controller/ImpactEffects`, `Tools/Build/elemental_transfer_harness`, `Assets/_Scripts/Data` |
| `claude/project-thread-62i63b` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 1 | `Docs/Approvals/Shombith`, `Docs/SKILL_TRAINING_PLAN.md`, `Docs/claude/PRINCIPLES.md`, `Docs/claude/principles.json` |
| `cece/intelligent-mayer-7qnyfc` | _Garrett or Shombith (cece/ branches: Garrett started them, Shombith said "cece/ is mine", both work in them, nobody else; Caleb, 2026-11-11)_ | 2026-11-23 | 2 | `Docs/Icons/sheets`, `Docs/Approvals/Shombith`, `.github/workflows`, `Docs/Studios/WrasseHaulLab.html` |
| `claude/training-ftue-mbxf1o` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 15 | `Tools/Build/race_course_source_harness`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/ModePreview/TRAINING_COVERAGE.md`, `Docs/ModePreview/TRAINING_PLAN.md` |
| `claude/project-thread-ef9ick` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 1 | `Assets/_SO_Assets/Effects`, `Assets/_Scripts/Controller/Arcade`, `Tools/Build/author_broadside_assets.py`, `Tools/Build/broadside_balance.py` |
| `claude/project-thread-5psmk3` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 1 | `Assets/_SO_Assets/VesselActions`, `Assets/_Scenes/Multiplayer Scenes`, `Assets/_Scripts/Controller/Arcade`, `Assets/_Scripts/Controller/Vessel` |
| `claude/brood-night-refit` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-23 | 3 | `Assets/_Prefabs/Spacevessels`, `Tools/Build/brood_trail_levers.py`, `Assets/_SO_Assets/Games`, `Assets/_SO_Assets/VesselActions` |
| `claude/brood-unity-audit` | Garrett | 2026-11-23 | 23 | `Tools/Build/brood_unity_audit`, `Assets/_Scripts/Editor/Launch` |
| `bugledger/owner` | Andrew, Yash | 2026-11-23 | 40 | `BugLedger/shared` |
| `compute/belacstein/desktop-lqtg35e` | froglet-compute (belacstein) | 2026-11-22 | 40 | `jobs/unity-train~r20261105-090126-c1cb`, `heartbeat.json`, `requests/r20261105-090126-c1cb.json` |
| `claude/project-thread-qz3y0v` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-22 | 8 | `Docs/MenuRedesignB/board`, `Docs/MENU_REDESIGN_B.md` |
| `claude/project-thread-282neh` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-22 | 3 | `Assets/_Scripts/Tests`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Assets/_Scripts/Controller/IO`, `Assets/_Scripts/ScriptableObjects` |
| `claude/atlas-steward-2026-11-22` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-22 | 1 | `Docs/LoopAtlas/model`, `Docs/LoopAtlas/BOARD.md`, `Docs/LoopAtlas/ledger.jsonl`, `Docs/LoopAtlas/model.json` |
| `claude/wide-gbox` | Garrett | 2026-11-22 | 25 | `flywheel/wide` |
| `claude/ms-ai-thresher` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-21 | 3 | `Assets/Resources/AIBoostPolicies`, `Assets/_Scripts/Controller/Arcade`, `Tools/Compute/evaluators`, `Assets/_SO_Assets/AI Boost Policies` |
| `claude/ms-ai-orca` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-21 | 1 | `Assets/_Scripts/Controller/Arcade`, `Assets/_SO_Assets/VesselActions`, `Assets/_Scripts/Controller/AI`, `Assets/_Scripts/Controller/Vessel` |
| `claude/project-thread-rnokvq` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-21 | 1 | `Tools/CI/unity_compile_in_container.sh` |
| `claude/project-thread-227h27` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-21 | 1 | `Assets/_Scripts/Editor/Build`, `Docs/ANDROID_BUILD.md` |
| `claude/project-thread-pyglk2` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-21 | 2 | `Docs/Activation/FTUECreator` |
| `claude/project-thread-w8d2uw` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-20 | 1 | `Tools/Analytics/server` |
| `claude/project-thread-h122rc` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-20 | 1 | `Docs/Handoffs/H-20261120-voyages-fbb698.json` |
| `bugledger/will` | Yash, ownerbynight | 2026-11-20 | 23 | `BugLedger/shared` |
| `claude/pc-bench-1120` | Yash | 2026-11-20 | 5 | `Assets/_Scripts/Utility/PerformanceBenchmark`, `Assets/_Scripts/Editor/Build` |
| `claude/fervent-pascal-o28ftt` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-11-19 | 6 | `Assets/_Scripts/Controller/Vessel`, `Assets/_Scripts/Tests`, `Docs/Manta/POLISH.md`, `Assets/_Prefabs/Spacevessels` |
| `cece/focused-bell-en38q1` | _Garrett or Shombith (cece/ branches: Garrett started them, Shombith said "cece/ is mine", both work in them, nobody else; Caleb, 2026-11-11)_ | 2026-11-18 | 3 | `Docs/LoopAtlas/artifact`, `Docs/LoopAtlas/src`, `Docs/LoopAtlas/README.md`, `Tools/Build/loop_atlas.py` |
| `claude/project-thread-jw78fe` | Garrett | 2026-11-18 | 5 | `Docs/ProvenWin/candidates`, `Docs/ProvenWin/QUEUE.json`, `Docs/ProvenWin/QUEUE.md`, `Assets/_Scripts/Editor/Diagnostics` |
| `prototype-level-rebuild` | dbrutus | 2026-11-17 | 1 | `Assets/_Scripts/Tests`, `Assets/_Scripts/Controller/Arcade`, `Assets/_SO_Assets/Effects`, `Assets/_SO_Assets/Games` |
| ... 100 more | | | | |
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

**One shipping process (Garrett, 2026-10-26).** `/ship` (with its `quick`, `deep`, `tools` and `promote` lanes) is the
one way a branch reaches `bleeding-edge`. `/to-bleeding-edge`, written on 2026-10-15 for Yash's repeated promotion of
`Ys-bleeding-edge`, duplicated `/ship`'s merge, gates, push and PR steps without either skill naming the other; it is
now an alias of `/ship promote [branch]`, and its conflict rules and `gates.sh` live in `/ship` §1.5. Yash is asked to
promote through `/ship promote` so the two habits cannot drift apart again.

**Notices.** `team_context.py` used to show the person a team notice on a session's first prompt, and the tools
decision on the first prompt about the engine or the tools. Shombith switched it off on 2026-10-23 with
`team_activity.py` and `team_tasks_nudge.py` (deleted 2026-10-25): together they added about 7 KB to prompts and sent
sessions off to fetch and query before answering. `vessel_studio_nudge.py` (once per session, on vessel work) and
`report_request.py` (when a prompt asks for a report) still run. The Swim Lanes page
(https://claude.ai/artifact/PXfvsmRch9BL87oBdBHKcf) is the board for the team's hand-offs (rule 9).
