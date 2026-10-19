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
| **Shombith** | **Owns all cloud and backend services and data management** (UGS: Cloud Save and its data such as `HangarCloudData`, authentication, the backend; Shombith, 2026-10-18). UI: `Menu_Main`, the UI prefabs, colour tokens, the Codex; multiplayer scenes. He drives the **Amoebius engine** milestones (`Port/src/CosmicShore.Engine`, `Player`, tests, online play) and merges those pull requests (`cece/focused-planck-*`). | Shombith03, shombith@froglet.studio |
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
8. **Read the team activity on every prompt** (Yash, 2026-10-15: "stay updated by searching all git repos
   automatically every time someone works on something"). The `team_activity.py` hook
   (`.claude/settings.json`) fetches `origin` in the background, at most every 3 minutes. On each prompt it lists
   the commits pushed to ANY branch since the session's last prompt, or the last 12 hours on its first prompt.
   It leaves out the session's own commits. A commit is marked **HOT** when it touches a file this session has
   worked on. Read a HOT commit before you change that file again, build on it, and tell the person. The hook
   self-tests: `python3 .claude/hooks/team_activity.py --self-test`.
9. **Hand work to each other on the board** (Yash, 2026-10-15: "ask them during their work to make to-dos and
   tests for other collaborators; Yash is willing to do some of the work if you assign it there"). A session
   cannot open Unity, sign in to a dashboard or watch a first-timer play, so whatever its work leaves for a person
   goes on [`TEAM_TASKS.md`](TEAM_TASKS.md) through `python3 Tools/Build/team_tasks.py add`: the editor test, the
   playtest, the UGS or PostHog step, the missing content, the owner's decision. Each task names who can take it.
   **Yash has offered to take tasks** (editor tests, playtests, UGS and dashboard setup, follow-up fixes); name the
   area's owner where it is theirs. Tell the person what you added and suggest who to hand it to. Take a task
   before starting it (`take <id> --who <name>`) and close it with a note (`done <id> --note ...`); a failed test
   is not done, it becomes a new `dev` task. `/ship` puts `team_tasks.py pr-section` in the PR body, so GitHub
   reviewers see the hand-offs, and every task is mirrored to a GitHub issue labelled `team-task`
   (`team_tasks.py gh-sync`, run by `.github/workflows/team-tasks-sync.yml`); the board stays the source. The `team_tasks_nudge.py` hook reminds every session and gives activation /
   onboarding prompts that area's open tasks. Self-tests: `team_tasks.py --self-test`,
   `.claude/hooks/team_tasks_nudge.py --self-test`.
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
_Generated by `python3 Tools/Build/who_works_where.py map --write` on 2026-10-15 from origin/* (last 45 days). Do not edit by hand._

| Person | Landed areas (pull requests merged or authored, count) |
|---|---|
| **Garrett** | `Assets/_Scripts/Tests` 18, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 18, `Assets/_Scripts/Controller/Vessel` 17, `.claude/skills/vessel` 11, `Assets/_Scripts/Controller/Environment` 10, `.claude/skills/labmaker` 10, `Assets/_Graphics/Materials` 10, `Docs/ElementalAbilitySystem/FLEET_MAPS.md` 9 |
| **Yash** | `Docs/BUG_HUNT_HANDOFF_2026-09.md` 10, `Docs/BugHunt/FIX_LOG.md` 10, `Docs/BugHunt/PLAYBOOK.md` 8, `Assets/_Scripts/Controller/Party` 4, `Assets/_Scripts/Controller/ImpactEffects` 3, `Assets/_Scripts/ScriptableObjects` 3, `Assets/_Scripts/UI/Elements` 3, `Assets/_Scripts/UI/Modals` 3 |
| **Shombith** | `Port/tests/CosmicShore.Tests` 7, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 6, `Port/src/CosmicShore.Engine` 6, `.github/workflows` 5, `Assets/_Scripts/Tests` 5, `Docs/PLATFORM_UNIFICATION.md` 5, `Port/CLAUDE.md` 5, `.claude/skills/asset-surgery` 4 |
| **Caleb** | `.claude/skills/asset-surgery` 1, `Assets/_Graphics/ARCADE` 1, `Assets/_Prefabs/Environment` 1, `Assets/_Scenes/Multiplayer Scenes` 1, `Assets/_Scripts/Controller/AI` 1, `Assets/_Scripts/Controller/Arcade` 1, `Assets/_Scripts/Controller/Environment` 1, `Assets/_Scripts/Controller/ImpactEffects` 1 |
| **aradia1** | `.claude/hooks/vessel_studio_nudge.py` 1, `.claude/settings.json` 1, `.claude/skills/amoebius` 1, `.claude/skills/amoebius-artifact` 1, `.claude/skills/arcadegame` 1, `.claude/skills/artifact-to-unity` 1, `.claude/skills/labmaker` 1, `.claude/skills/vessel` 1 |
| **Grok Bot** | `.claude/hooks/vessel_studio_nudge.py` 1, `.claude/settings.json` 1, `.claude/skills/amoebius` 1, `.claude/skills/amoebius-artifact` 1, `.claude/skills/arcadegame` 1, `.claude/skills/artifact-to-unity` 1, `.claude/skills/labmaker` 1, `.claude/skills/vessel` 1 |
| **studyholic** | `.claude/skills/ship` 1, `Assets/Resources/SkimRaceAIConfig.asset` 1, `Assets/Resources/SkimRaceAIConfig.asset.meta` 1, `Assets/Resources/SkimRaceAIConfig_I1.asset` 1, `Assets/Resources/SkimRaceAIConfig_I1.asset.meta` 1, `Assets/Resources/SkimRaceAIConfig_I2.asset` 1, `Assets/Resources/SkimRaceAIConfig_I2.asset.meta` 1, `Assets/Resources/SkimRaceAIConfig_I4.asset` 1 |
| **Andrew** | `Docs/QA/.applied.json` 1, `Docs/QA/ARCHIVE.md` 1, `Docs/QA/DEV_TASKS.md` 1, `Docs/QA/QA_BACKLOG.md` 1, `Docs/QA/RESULTS` 1 |

**In flight** (unmerged branches updated in the last 45 days, newest first):

| Branch | Who | Updated | Ahead | Touches |
|---|---|---|---|---|
| `Ys-bleeding-edge` | Yash | 2026-10-15 | 1 | `.claude/hooks/report_request.py`, `.claude/settings.json`, `CLAUDE.md`, `Docs/Reports/REGATTA_AI.md` |
| `claude/new-session-76hnyc` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 40 | `Assets/_Prefabs/UI Elements`, `Assets/_Scripts/UI/Elements`, `Assets/_Graphics/UI`, `Docs/MENU_REDESIGN_B.md` |
| `cece/sharp-tesla-j0kd7z` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-15 | 22 | `Assets/_Scripts/Controller/Vessel`, `.claude/skills/duo-vessel`, `Docs/Studios/VesselStudio`, `Assets/_Scripts/Tests` |
| `cece/zealous-feynman-po8k8d` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-15 | 6 | `Assets/_Scripts/Controller/Vessel`, `Assets/_SO_Assets/VesselActions`, `Assets/_Graphics/Icons`, `.claude/skills/labmaker` |
| `claude/webgl-prism-animations-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 1 | `Assets/_Scripts/Utility/Effects`, `Assets/_Scripts/Controller/Environment`, `Assets/_Scripts/Controller/ECS`, `Assets/_Scripts/Controller/Vessel` |
| `cece/affectionate-clarke-s738vh` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-15 | 5 | `Assets/_Scripts/Controller/Vessel`, `Assets/_SO_Assets/VesselActions`, `Assets/_Graphics/Icons`, `Docs/Studios/VesselStudio` |
| `cece/dreamy-pascal-7xxx1m` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-15 | 10 | `Tools/Build/prism_collision_burst_lab`, `Assets/_Scripts/Utility/Effects`, `Assets/_Graphics/Materials`, `.claude/skills/labmaker` |
| `cece/charming-rubin-ro74tj` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-15 | 2 | `.claude/skills/labmaker`, `Docs/Studios/PUMP_RIDER_LAB.md`, `Docs/Studios/PumpRiderLab.html` |
| `claude/release-guard-dev-checkbox-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-15 | 1 | `Assets/_Scripts/Editor/Build`, `Assets/_Scripts/Tests`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/WEBGL_TOYBOX.md` |
| `claude/wizardly-davinci-z0zyfp` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 1 | `Assets/_Scripts/Controller/ImpactEffects` |
| `claude/compassionate-allen-ta8oa6` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 10 | `Tools/Build/prisma_party_scenarios`, `Port/src/CosmicShore.Engine`, `Docs/MULTIPLAYER_START_HERE.md`, `.claude/skills/labmaker` |
| `claude/store-page-urls-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 2 | `Docs/Legal/README.md`, `Assets/Resources/PrivacyConsentConfig.asset`, `Assets/_Prefabs/UI Elements`, `Assets/_Scripts/UI/Modals` |
| `claude/jolly-maxwell-dgwdpz` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 2 | `Assets/_Graphics/ARCADE`, `Tools/Build/card_backgrounds_manifest.json`, `Tools/Build/render_card_backgrounds.py`, `Tools/Build/card_art_harness` |
| `feat/red-console-logger` | Yash | 2026-10-14 | 1 | `Assets/_Scripts/Utility/PerformanceBenchmark` |
| `claude/serene-edison-lfv24f` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 1 | `Docs/ANDROID_BUILD.md` |
| `cece/determined-goodall-o1klfo` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 1 | `Assets/_Scripts/Controller/Toys`, `Assets/_Scripts/ScriptableObjects`, `Assets/_SO_Assets/Toys`, `Assets/_Scenes/Menu_Main.unity` |
| `cece/gracious-pasteur-9z5e9t` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 3 | `Tools/Build/converter_reel`, `Docs/Studios/CONVERTER_REEL_LAB.md`, `.claude/skills/labmaker`, `Docs/Studios/ConverterReelLab.html` |
| `cece/compassionate-babbage-gc13io` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 1 | `.claude/skills/labmaker`, `Docs/Studios/CREW_VESSEL_LAB.md`, `Docs/Studios/CrewVesselLab.html` |
| `cece/vibrant-archimedes-n3xn3x` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-13 | 4 | `Assets/_Scripts/Controller/Arcade`, `Assets/_SO_Assets/Cell Configs`, `Assets/_SO_Assets/Games`, `Assets/_Scripts/Controller/Vessel` |
| `claude/brave-feynman-g71arn` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 1 | `Assets/_Scripts/Controller/Vessel`, `.claude/skills/vessel`, `Assets/_Prefabs/Spacevessels`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `fix/weekly-challenge-reset` | Yash | 2026-10-13 | 1 | `UGS/Leaderboards`, `UGS/README.md` |
| `claude/intelligent-mendel-8jdfvr` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 1 | `Assets/_Scripts/System/Squads`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `claude/modest-sagan-0ybcgo` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 3 | `Assets/_SO_Assets/Pilots`, `Assets/_Scripts/Data`, `Assets/_SO_Assets/Classes`, `Assets/_Scripts/ScriptableObjects` |
| `cece/nice-brahmagupta-ojclij` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-13 | 40 | `Assets/_Scripts/Utility/PerformanceBenchmark`, `Assets/_Scripts/Tests`, `Docs/PartySystem/UI.md`, `Assets/_Scripts/Utility/Replay` |
| `fix/vessel-prefab-missing-ref` | Yash | 2026-10-10 | 1 | `Assets/_SO_Assets/Vessel Prefab Container.asset`, `Assets/_Scripts/Controller/Multiplayer`, `Assets/_Scripts/ScriptableObjects`, `Tools/Build/author_stoat_assets.py` |
| `claude/trusting-curie-bn6rrn` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 8 | `Docs/BranchArchive/small`, `Docs/BranchArchive/large`, `Docs/BranchArchive/medium`, `Docs/BranchArchive/README.md` |
| `claude/tandava-severing` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 8 | `Assets/_Scripts/Controller/Arcade`, `Tools/Build/swarm_core_harness`, `Assets/_SO_Assets/Swarm Fauna`, `Assets/_SO_Assets/Games` |
| `cece/charming-cerf-alf1j1` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-09 | 9 | `Assets/_Scripts/Controller/Environment`, `Assets/_Graphics/Materials`, `Assets/_Scripts/Tests`, `Assets/_SO_Assets/Cell Configs` |
| `prisma-relay-transport` | Yash | 2026-10-09 | 10 | `Port/src/CosmicShore.Engine`, `Port/tests/CosmicShore.Tests`, `Port/src/CosmicShore.Player`, `Port/src/CosmicShore.Online` |
| `claude/busy-lamport-kdwkae` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-08 | 2 | `Tools/Build/unity_refcompile`, `.claude/skills/asset-surgery` |
| `claude/optimistic-brown-g45ivk` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-08 | 1 | `Port/CLAUDE.md`, `Port/docs/ARCHITECTURE.md`, `Port/src/CosmicShore.Mcp`, `Port/tools/check_smoke_concurrency.py` |
| `cece/loving-feynman-hlgf18` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-08 | 6 | `Assets/_Scripts/Controller/Vessel`, `Assets/_SO_Assets/VesselActions`, `Assets/_Scripts/Tests`, `Tools/Build/tether_harness` |
| `cece/eco-living-cell-r3` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-08 | 3 | `Tools/Ecology/living_cell`, `Tools/Ecology/DISCOVERIES.md` |
| `cece/gifted-curie-x2cpd0` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-08 | 3 | `Tools/Ecology/flight`, `Tools/Ecology/common` |
| `claude/duel-humans-gate-wip` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-08 | 1 | `Assets/_SO_Assets/Games`, `Assets/_Graphics/ARCADE`, `Assets/_Scripts/Tests`, `Assets/_SO_Assets/GameModeQuest` |
| `claude/hybrid-creatures` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-08 | 12 | `Tools/NCA/results`, `Tools/NCA/hybrid_viewer`, `Tools/NCA/hybrid3d.py`, `Tools/Ecology/common` |
| `claude/omnicrystal-shepard-triangles-9kg32k` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-07 | 2 | `Assets/_Graphics/Materials`, `Assets/_Prefabs/Environment`, `Docs/PALETTE.md`, `Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md` |
| `claude/fix-breakwater-preview-controls-3evnnj` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-07 | 2 | `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Assets/_Scripts/Controller/IO`, `Assets/_Scripts/UI/View`, `Assets/Resources/DrillLibrary.asset` |
| `claude/admiring-euler-86o7hv` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-06 | 1 | `Assets/_Scripts/Tests`, `Assets/_Prefabs/Spacevessels`, `.claude/skills/vessel`, `Assets/_Scripts/Controller/AI` |
| `claude/eloquent-meitner-9e4u2a` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-04 | 2 | `Assets/Plugins/Android`, `ProjectSettings/AndroidResolverDependencies.xml` |
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

**Everyone is told, not only Claude.** Two hooks show the person a notice once per session:
- `team_context.py`:
  - on the first prompt, whatever it is about: who the team is, that Claude checks who works where, and the tools
    decision;
  - on the first prompt about the engine or the tools ("Amoebius", "engine", `Port/`, "launcher", "which tool"):
    one line with the decision above.
- `vessel_studio_nudge.py`: the same for vessel work.

Claude's answer to any question about these tools opens with the status line. Once §6 of
`TOOLING_DECISIONS.md` records a decision, `team_context.py` reports that instead.
