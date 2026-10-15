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
| **Shombith** | UI: `Menu_Main`, the UI prefabs, colour tokens, the Codex; multiplayer scenes. He drives the **Amoebius engine** milestones (`Port/src/CosmicShore.Engine`, `Player`, tests, online play) and merges those pull requests (`cece/focused-planck-*`). | Shombith03, shombith@froglet.studio |
| **Yash** | The Bug Hunt; party / multiplayer (UGS) hardening; diagnostics (redlog); leaderboards; the **Vessel Studio** (the artifact, studio pages, Sync panel); the **Amoebius launcher** (VESSEL STUDIO page, studio server, artifact library). Branches `Ys-*`, `vessel-studio*`, `amoebius-*`. | Yash Sadhukhan / YsKhan61 |
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
| **Garrett** | `Assets/_Scripts/Tests` 16, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 16, `Assets/_Scripts/Controller/Vessel` 15, `.claude/skills/vessel` 9, `Assets/_Scripts/Controller/Environment` 9, `Assets/_Graphics/Materials` 9, `Docs/ElementalAbilitySystem/FLEET_MAPS.md` 8, `.claude/skills/labmaker` 8 |
| **Yash** | `Docs/BUG_HUNT_HANDOFF_2026-09.md` 10, `Docs/BugHunt/FIX_LOG.md` 10, `Docs/BugHunt/PLAYBOOK.md` 8, `Assets/_Scripts/Controller/Party` 4, `Assets/_Scripts/Controller/ImpactEffects` 3, `Assets/_Scripts/ScriptableObjects` 3, `Assets/_Scripts/UI/Elements` 3, `Assets/_Scripts/UI/Modals` 3 |
| **Shombith** | `Port/tests/CosmicShore.Tests` 7, `Port/src/CosmicShore.Engine` 6, `Port/CLAUDE.md` 5, `Docs/UNITY_VERIFICATION_CHECKLIST.md` 5, `.claude/skills/amoebius` 4, `Port/docs/ARCHITECTURE.md` 4, `Port/docs/milestones.json` 4, `Port/src/CosmicShore.Player` 4 |
| **Caleb** | `.claude/skills/asset-surgery` 1, `Assets/_Graphics/ARCADE` 1, `Assets/_Prefabs/Environment` 1, `Assets/_Scenes/Multiplayer Scenes` 1, `Assets/_Scripts/Controller/AI` 1, `Assets/_Scripts/Controller/Arcade` 1, `Assets/_Scripts/Controller/Environment` 1, `Assets/_Scripts/Controller/ImpactEffects` 1 |
| **Grok Bot** | `.claude/hooks/vessel_studio_nudge.py` 1, `.claude/settings.json` 1, `.claude/skills/amoebius` 1, `.claude/skills/amoebius-artifact` 1, `.claude/skills/arcadegame` 1, `.claude/skills/artifact-to-unity` 1, `.claude/skills/labmaker` 1, `.claude/skills/vessel` 1 |
| **studyholic** | `.claude/skills/ship` 1, `Assets/Resources/SkimRaceAIConfig.asset` 1, `Assets/Resources/SkimRaceAIConfig.asset.meta` 1, `Assets/Resources/SkimRaceAIConfig_I1.asset` 1, `Assets/Resources/SkimRaceAIConfig_I1.asset.meta` 1, `Assets/Resources/SkimRaceAIConfig_I2.asset` 1, `Assets/Resources/SkimRaceAIConfig_I2.asset.meta` 1, `Assets/Resources/SkimRaceAIConfig_I4.asset` 1 |
| **Andrew** | `Docs/QA/.applied.json` 1, `Docs/QA/ARCHIVE.md` 1, `Docs/QA/DEV_TASKS.md` 1, `Docs/QA/QA_BACKLOG.md` 1, `Docs/QA/RESULTS` 1 |

**In flight** (unmerged branches updated in the last 45 days, newest first):

| Branch | Who | Updated | Ahead | Touches |
|---|---|---|---|---|
| `Ys-bleeding-edge` | Yash | 2026-10-14 | 25 | `.github/workflows`, `Port/src/CosmicShore.Engine`, `Docs/Studios/VesselStudio`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `cece/sharp-tesla-j0kd7z` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 7 | `.claude/skills/duo-vessel`, `Docs/Studios/VesselStudio`, `.claude/skills/labmaker`, `Docs/Studios/VESSEL_STUDIO_PLAN.md` |
| `cece/dreamy-pascal-7xxx1m` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 6 | `Assets/_Scripts/ScriptableObjects`, `Assets/_Graphics/Materials`, `Tools/Build/prism_collision_burst_lab`, `Assets/_Scripts/Utility/Effects` |
| `cece/brave-shannon-ngq8f0` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 2 | `Assets/_Prefabs/FloraAndFauna`, `Docs/ECOSYSTEM.md`, `.claude/skills/flora`, `Docs/claude/GAME_MODES_AND_CONTROLLERS.md` |
| `claude/wizardly-davinci-z0zyfp` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 1 | `Assets/_Scripts/Controller/ImpactEffects` |
| `claude/webgl-toybox-kg5qx7` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 15 | `Assets/_Scripts/Controller/Environment`, `Docs/WEBGL_TOYBOX.md`, `Assets/Plugins/WebGL`, `Assets/_Scripts/Editor/Build` |
| `claude/compassionate-allen-ta8oa6` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 10 | `Tools/Build/prisma_party_scenarios`, `Port/src/CosmicShore.Engine`, `Docs/MULTIPLAYER_START_HERE.md`, `.claude/skills/labmaker` |
| `claude/new-session-76hnyc` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-14 | 34 | `Assets/_Graphics/UI`, `Assets/_Prefabs/UI Elements`, `Assets/_Scripts/UI/Kit`, `Assets/_Scripts/UI/Elements` |
| `cece/gallant-lamport-sc3khe` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-14 | 5 | `Assets/_Scripts/Controller/Vessel`, `Assets/_SO_Assets/VesselActions`, `Assets/_Graphics/Icons`, `Assets/_Scripts/Tests` |
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
| `claude/exciting-bell-69b398` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 1 | `Assets/_Scripts/Controller/Arcade` |
| `claude/intelligent-mendel-8jdfvr` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 1 | `Assets/_Scripts/System/Squads`, `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| `claude/modest-sagan-0ybcgo` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-13 | 3 | `Assets/_SO_Assets/Pilots`, `Assets/_Scripts/Data`, `Assets/_SO_Assets/Classes`, `Assets/_Scripts/ScriptableObjects` |
| `cece/nice-brahmagupta-ojclij` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-13 | 40 | `Assets/_Scripts/Utility/PerformanceBenchmark`, `Assets/_Scripts/Tests`, `Docs/PartySystem/UI.md`, `Assets/_Scripts/Utility/Replay` |
| `fix/vessel-prefab-missing-ref` | Yash | 2026-10-10 | 1 | `Assets/_SO_Assets/Vessel Prefab Container.asset`, `Assets/_Scripts/Controller/Multiplayer`, `Assets/_Scripts/ScriptableObjects`, `Tools/Build/author_stoat_assets.py` |
| `claude/trusting-curie-bn6rrn` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 8 | `Docs/BranchArchive/small`, `Docs/BranchArchive/large`, `Docs/BranchArchive/medium`, `Docs/BranchArchive/README.md` |
| `claude/tandava-severing` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-09 | 8 | `Assets/_Scripts/Controller/Arcade`, `Tools/Build/swarm_core_harness`, `Assets/_SO_Assets/Swarm Fauna`, `Assets/_SO_Assets/Games` |
| `cece/charming-cerf-alf1j1` | _a Claude Code session; merged mostly by Garrett, sometimes Shombith_ | 2026-10-09 | 2 | `Assets/_Scripts/Controller/Environment`, `Assets/_Graphics/Materials`, `Assets/_Prefabs/Spawnables`, `Assets/_SO_Assets/Cell Configs` |
| `prisma-relay-transport` | Yash | 2026-10-09 | 1 | `Port/src/CosmicShore.Online`, `Port/tests/CosmicShore.Tests`, `Port/src/CosmicShore.Engine`, `Port/src/CosmicShore.Player` |
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
| `claude/sweet-galileo-m00ltx` | _a Claude Code session; the person is the one who merges or pushes it_ | 2026-10-04 | 8 | `Docs/STEAM_RELEASE_TASKS.md`, `Docs/STEAM_CHECKPOINT_REV5_READINESS_AUDIT.html`, `Docs/STEAM_CHECKPOINT_REV5_READINESS_AUDIT.pdf`, `Docs/STEAM_CHECKPOINT_SERIES.md` |
| ... 66 more | | | | |
<!-- /who_works_where:map -->

## Open decisions about our tools

Amoebius and the Vessel Studio are **under evaluation**: we have not decided that they help enough to keep.
The single tool that should build Cosmic Shore, and later Froglet's other games, is also undecided. The
evidence, the options and how we decide: [`TOOLING_DECISIONS.md`](TOOLING_DECISIONS.md). What people say
after using them: [`TOOLING_FEEDBACK.md`](TOOLING_FEEDBACK.md).
