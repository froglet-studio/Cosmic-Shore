# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## TOP PRIORITY: the elemental vessel loop (Garrett, 2026-11-12)

"Pushing every vessel through this plan in a systematic way remains the highest priority for anyone who has any
cycles." When a person has no task for you, or asks what to work on, start here: **`/elemental-loop`**, or
`python3 Tools/Build/elemental_loop.py next`. The loop, per vessel and element: 0 a game kit (a Time, a Charge and a
Mass or Space arcade game), 1 fun (abilities and game together, one intensity), 2 the I1-I4 ladder (intro, fun,
challenging, very hard), 3 one AI on all four rungs; then per arena game (Regatta for Time, Broadside for Charge,
Brood Rush for Mass and Space together, balanced on net control of space): 4 an environment creator trained against
the fixed AI pool, 5 the plateau's vessel imbalance tuned and sent back to steps 2-4 until no step 5 is needed. Each
loop needs fewer people. Process: [`Docs/ElementalLoop/PLAYBOOK.md`](Docs/ElementalLoop/PLAYBOOK.md); board:
`Docs/ElementalLoop/STATUS.md` (generated); sign-offs: `loop.json`, written by `elemental_loop.py set`.

## Where everything else lives (read the topic file before touching its area)

This file holds only the rules every session needs. Everything else moved verbatim into topic files under `Docs/claude/`. Each entry lists the section headings the file contains, so a reference like "CLAUDE.md § Anti-Patterns" can be found by grepping this index. To find a passage quoted from the old single-file CLAUDE.md, search both places: `grep -rn '<phrase>' CLAUDE.md Docs/claude/`.

- [`Docs/ElementalLoop/PLAYBOOK.md`](Docs/ElementalLoop/PLAYBOOK.md): **The elemental vessel loop, the top priority** (`/elemental-loop`, Garrett 2026-11-12): steps 0-5 with each step's start, work, exit and sign-off, the game kit rule, the three arena games, how the loop needs fewer people each round, the missing scaffolding and Garrett's open decisions. Board `STATUS.md` and sign-offs `loop.json`, both through `Tools/Build/elemental_loop.py` (`next`, `vessel`, `set`, `--check`).
  Sections: Garrett's words; The loop at a glance; Step 0 the game kit; The steps; Running the loop; What the loop pays for; How this maps onto what exists; Missing scaffolding; Rules this loop keeps; Decisions for Garrett; Log
- [`Docs/TEAM_TASKS.md`](Docs/TEAM_TASKS.md): **Froglet Tasks, the team's hand-off tickets** - tests, playtests, setup, content and decisions one collaborator's session leaves for another: GitHub issues labelled `team-task`, read on the Froglet Tasks page, written only through the `/team-tasks` skill (`Tools/Build/team_tasks.py`). Tickets are nobody's until someone takes one.
  Sections: a pointer to the tickets (the board moved to GitHub Issues)
- [`.claude/skills/swimlanes/SKILL.md`](.claude/skills/swimlanes/SKILL.md): **Swim lanes** (`/swimlanes`, Garrett 2026-11-08) - which vessels, games, app-shell sections and systems are occupied right now and which open ones to pick up next (open with work, occupied, needs polish, polished), from git, the tickets, the checklist and the Bug Ledger (`swimlanes.py`; catalog and verdicts in `lanes.json`). **Hand-offs** (Shombith 2026-11-13): `/handoff-to-<person>` (NEW on their board), `/handoff-open`, `/handoff-for-test` (to Andrew, who answers with `/handoff-feedback`); `Tools/Build/handoff.py`, files in `Docs/Handoffs/`, procedure `HANDOFF.md`. **The Swim Lanes page** (https://claude.ai/artifact/PXfvsmRch9BL87oBdBHKcf): the board, claim, hand off, test feedback and every approval queue in a browser, synced by `/swimlanes` section 6.
  Sections: Run it; How occupancy is read; Answer the person; Polished or rough is a person's call; 4.5 Hand-offs; Adding, fixing or archiving a lane; 6 The Swim Lanes page
- [`.claude/skills/approve/SKILL.md`](.claude/skills/approve/SKILL.md): **Approve after the fact** (`/approve`, Garrett 2026-11-10) - changes to someone else's work go through unless they collide with an occupied swim lane (`Tools/Build/approvals.py check`), and land in that person's queue (`Docs/Approvals/<Person>/`, [`Docs/Approvals/README.md`](Docs/Approvals/README.md)); `/approve` retrieves the list and records approve / discuss / resolve.
  Sections: Who you are; Retrieve the list; Walk through it; Record the answers; Commit the answers; What your own changes queued; Filing entries
- [`.claude/skills/trunk-doctor/SKILL.md`](.claude/skills/trunk-doctor/SKILL.md): **Trunk doctor** (`/trunk-doctor`, 2026-11-12) - when bleeding-edge is red: why, who broke it (the guard's runs and logs, classified static / generator / compile / tests / infra, and the PR that introduced each finding), the smallest safe repair (fix forward or a revert PR, never a skipped check), and `trunk_doctor.py --gate`, which /ship runs before every push and merge so nobody piles onto a red trunk. Every incident goes in [`Docs/Reports/TRUNK_HEALTH.md`](Docs/Reports/TRUNK_HEALTH.md).
  Sections: Rules; Status; Diagnose; Decide; Repair; Confirm; Guard against piling on; Record; Prove it works; Not this skill
- [`Docs/TEAM.md`](Docs/TEAM.md): **Read before working anywhere.** Who works where (Garrett, Caleb, Shombith, Yash and others, measured from git), the collaboration rules, what doing work twice cost, and the generated map (`Tools/Build/who_works_where.py`).
  Sections: The people; The rules for every session; What happened without this; The map (generated); Decisions about our tools
- [`Docs/TOOLING_DECISIONS.md`](Docs/TOOLING_DECISIONS.md): **DECIDED 2026-10-19 (§6)**: Amoebius stays (Shombith drives it), the Vessel Studio is an artifact and not part of the engine, no single unifying tool. The evidence, options and recommendation that led there. Volunteered feedback: `Docs/TOOLING_FEEDBACK.md`.
  Sections: The questions; What we have today; Amoebius: keep, narrow, or stop?; The Vessel Studio: keep, narrow, or remove?; The one tool; Decision record; How we decide
- [`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`](Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): LOCKED. Read before any ecology, fauna, flora, or HyperSea change.
  Sections: Ecosystem Design Principles (LOCKED — read before any ecology change)
- [`Docs/ProvenWin/PLAYBOOK.md`](Docs/ProvenWin/PLAYBOOK.md): **Proven wins** (`/proven-win`, Garrett 2026-11-11; `/proven-win-scout` finds candidates; `Tools/Build/proven_win/` run.py, verdict.py, signoff.py make every judgment mechanical): one cost-free improvement at a time, measured in real Unity on a GPU (paired A/B with null and negative controls, `ShaderABBench`), output proven identical, a blind sign-off page. `WINS.md` is the record of proven, rejected and queued candidates.
  Sections: what counts; pick; isolate; measure (and the batchmode GPU traps); the human A/B; record; limits
- [`Docs/claude/ABOUT_PROJECT.md`](Docs/claude/ABOUT_PROJECT.md): Vessel classes, team domains, tech stack.
  Sections: About This Project; Vessel Classes; Team Domains; Tech Stack
- [`Docs/claude/PROJECT_STRUCTURE.md`](Docs/claude/PROJECT_STRUCTURE.md): Folder layout, assembly definitions, where tests live, scene inventory.
  Sections: Project Structure; Assembly Definitions; **Tests live under an `Editor/` folder, not in an asmdef — until their dependencies are extracted.**; Scene Inventory
- [`Docs/claude/GAME_MODES_AND_CONTROLLERS.md`](Docs/claude/GAME_MODES_AND_CONTROLLERS.md): The GameModes enum (every mode), controller hierarchy, game launch pipeline.
  Sections: Game Modes & Controllers
- [`Docs/claude/DOCUMENTATION_INDEX.md`](Docs/claude/DOCUMENTATION_INDEX.md): Catalogue of every doc under Docs/ and what it covers.
  Sections: Documentation Index
- [`Docs/claude/ARCHITECTURE_CORE.md`](Docs/claude/ARCHITECTURE_CORE.md): SOAP, threading, bootstrap, auth/session flow, Reflex DI, input strategy (with the one Invert Y rule, no nose lag for human pilots, and the per-hull phone touch profiles).
  Sections: Architecture Patterns; ScriptableObject Config Separation; SOAP — Scriptable Object Architecture Pattern (Primary Architecture); Threading & Main-Thread Affinity; Bootstrap & Scene Flow; Authentication & Session Flow; Dependency Injection (Reflex); Input Strategy Pattern
- [`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`](Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md): Impact effects architecture; the LOCKED PvP-is-petals-only rule (and silence, its one consequence beyond the petals: every scored hit leaves the victim's abilities harmless for what the hit is worth, `Docs/ElementalAbilitySystem/SILENCE.md`, which also holds the AI-plateau balancing plan); the LOCKED FMOD exposed-field convention.
  Sections: Impact Effects Architecture; PvP is petals only (LOCKED, Garrett 2026-10-10); Audio (FMOD) — every sound is an exposed, editable field (LOCKED convention)
- [`Docs/MULTIPLAYER_START_HERE.md`](Docs/MULTIPLAYER_START_HERE.md): **Read first for ANY multiplayer work (Unity or Amoebius).** What the owner wants, the standing rules, where everything is, current state, the owner's hand-test list, and the ordered next steps.
  Sections: What the owner wants; Rules every session keeps; Where everything is; Where things stand; What the owner tests by hand; What comes next, in order; How a session starts here
- [`Docs/Voyages/HANDOFF.md`](Docs/Voyages/HANDOFF.md): **Voyages, start here** (handoff to Shombith, 2026-11-12): what is built and merged, which voyage doc says what, the code by job, how to debug a run, what was never opened in the editor, leftover branches, and Garrett's settled and open decisions.
  Sections: where things stand; read these; the code; debugging; never opened in the editor; leftover branches; who else works near here; Garrett's decisions
- [`Docs/Voyages/DEPTH.md`](Docs/Voyages/DEPTH.md): **Voyages, deep (Garrett, 2026-11-07).** Read before any voyage work:
  the living HyperSea (pressure, blooms, restocks, the Kaiju), rival crews that take the same jobs by the same rules, a
  job for every one of the 16 faction types in nine work verbs, sagas ending in named relics, standing and notoriety,
  uncharted places, and the depth harness (`Tools/Build/voyage_depth_harness/run.sh --scorecard`) whose scorecard is a
  test. The content is one spec (`Docs/Voyages/voyage_slice.json`, `author_voyage_assets.py --check`).
  Sections: what was shallow, measured; the thesis; the living galaxy; rival crews; work verbs; sagas and relics; standing and notoriety; risk and the way home; discovery; the crew remembers; in the world; the scorecard; phases; who works where; decisions; measured
- [`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`](Docs/claude/MULTIPLAYER_AND_SOCIAL.md): Netcode, player spawning, party/invite lobby, friends, AI backfill, SkimRace — the party-docs table routes every UGS call to `UgsRequestPolicy` and the invite/resilience review, lists the pinned multiplayer package versions and the protocol-version rule.
  Sections: Multiplayer / Netcode; Party / Invite Lobby System; Friend System; Player Count & AI Backfill Pipeline; SkimRace Game Mode
- [`Docs/Studios/VESSEL_STUDIO_PLAN.md`](Docs/Studios/VESSEL_STUDIO_PLAN.md): The Vessel Studio — pick a vessel and fly its studio on web, Windows (Amoebius's VESSEL STUDIO page; Unity's FrogletTools > Vessels > Vessel Studio opens the studio home, whose cards open the same pages as the artifact) and phones. Pages in `Docs/Studios/VesselStudio/`; every studio is a reader of the shipped numbers. Read before touching a studio.
  Sections: What exists; Two kinds of studio; Platforms; The game studio and Amoebius on more platforms; The studio agent; Phases; Branches
- [`Docs/Studios/VESSEL_STUDIO_VISION.md`](Docs/Studios/VESSEL_STUDIO_VISION.md): **The north star for every studio task**: a Vessel Studio so simple that a non-programmer makes vessels (presets, dice, voice), trains their AI from their own play, makes their mini-games and assigns arena games, with Amoebius as a game engine for non-programmers. The building blocks B1-B9, their order and status (update it in every change that moves a step), and what each step taught.
  Sections: What "done" looks like; Rules that keep it simple and safe; The building blocks; Status and order; Who does what; What each step taught
- [`Docs/Studios/VESSEL_TEST_RANGE_PLAN.md`](Docs/Studios/VESSEL_TEST_RANGE_PLAN.md): Plan for testing any vessel and its AI in the REAL game: one dev-only harness in the real mode scenes (AI on any seat, free-fly camera, intensity maps, time scale, Full / Mobile-low / Block look), opened from Unity and Amoebius. The `/vessel-studio` skill builds the web studios and owns the one Vessel Studio artifact.
  Sections: Three tiers, one catalog; What the game already has; The Test Range harness; Opening it; Phases; Decisions for the user
- [`Docs/LAUNCH/READINESS.md`](Docs/LAUNCH/READINESS.md): **Read before any launch, release-pipeline, store or live-ops work.** The launch: mobile (iOS, Android, with the web Toybox) as soon as activation is ready, Steam with keys by March 2027 before GDC (Shombith, 2026-11-06). The readiness audit per platform, the ranked blockers, the decisions, and which PR carries each blocker; beside it `RUNBOOK.md` (launch day, rollback per platform), `LIVE_UPDATES.md` (what changes without a store release), `STATUS_CONTRACT.md` (the `launch-status` records every pipeline publishes). The Launch Console (https://claude.ai/artifact/XwF8FQrHhUTvNv1gE8kws2; FrogletTools > Launch > Launch Console) shows it live; `/launch-flow-<platform>` (android, ios, windows, web, liveops, backend, launch-wide) works one platform in its own chat.
  Sections: Go/no-go per platform; one section per platform; live updates, backend, CI, quality, legal, store assets, analytics, support, security; Ranked blocker list; Decisions; Where each blocker stands
- [`Docs/AI_SYSTEM/README.md`](Docs/AI_SYSTEM/README.md): Home for EVERY vessel AI, worked on `Ys-bleeding-edge` (the `ai-system` branch was merged there and retired 2026-10-09) — the branch workflow (bleeding-edge, Ys-bleeding-edge, perf/performance-optimization, claude/peaceful-rubin-hhw49n), the AI roster and layers, the per-AI diagnosis playbook and intake checklist. Run `python3 Tools/Build/ai_branch_sync.py` first in any AI session. **The one AI plan every AI effort serves is [`Docs/AI_SYSTEM/AI_PLAN.md`](Docs/AI_SYSTEM/AI_PLAN.md)** (LOCKED, Garrett 2026-11-02).
  Sections: Branch Workflow; Architecture (layers, roster, target); Diagnosis Playbook (tiers, intake, status board); Sync Log
- [`Docs/Compute/README.md`](Docs/Compute/README.md): **Froglet Compute, the team's idle hardware searching for the game** (Garrett, 2026-10-26): a queue of learning searches (AI drives per vessel x Time game x intensity, the Regatta course and elemental balance plan, swarm bioforms, NCA creature trainings) every collaborator's computer works through when idle (`compute.py install`: set and forget, any OS; never touches the checkout; publishes only to `compute/<user>/<host>`), one merged board, and a scout that finds what to search next. A result is a proposal: its area's owner promotes it. Skills: `/compute`, `/compute-job`, `/compute-scout`, `/regatta-balance` (`Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md`), `/bioform-forge`.
  Sections: Start; What it does on your computer; The pieces; A job; How the search is shared; An evaluator; The queue refs; Promotion; The scout sweep; Its sibling in Unity; What this is not; Verification status; The Compute Console; Manual runs; The live console and the always-on VM; Runs in the real game (the Unity trainer, and what a run records with the game's own tools); The mothership (every computer live, runs sent to one); The app (Froglet Compute on your own computer); Follow-ups
- [`Docs/BRANCH_PARTS_STATUS.md`](Docs/BRANCH_PARTS_STATUS.md): **Current report on the A–M parts of `cece/nice-brahmagupta-ojclij`** (the 2026-10-12 branch test plan): which parts landed where, what is still to test and who owes each step. Answer any question about those parts or that plan from it, and update it when a part lands.
  Sections: Where each part stands; What landed, and how it differs from the plan; Open problems not caused by these parts; Re-measuring this report
- [`Docs/claude/FTUE_DIALOGUE_AI.md`](Docs/claude/FTUE_DIALOGUE_AI.md): The FTUE quest graph (Shombith leads activation; Caleb heads FTUE with him; no one else is on it), dialogue system, AI opponent system — with the table of every AI (platform autopilot + boost policies, the Skim Race pilot and its doc `Docs/SKIM_RACE_AI.md`, the Urchin autopilot), the input-only rule and the AI gates. Read before touching any AI. Holds the LOCKED guided-path rule: to send a player somewhere, spotlight the one control to press next, dim and disable everything else except Settings, and let them press it - never navigate for them.
  Sections: FTUE (First-Time User Experience) — the QUEST GRAPH; Dialogue System; AI Opponent System
- [`Docs/claude/MENU_AND_LAVA_LAMP.md`](Docs/claude/MENU_AND_LAVA_LAMP.md): Menu_Main screens, ScreenSwitcher, menu freestyle (lava-lamp) HUD.
  Sections: Menu Screen Navigation (Menu_Main Scene); Lava-Lamp Mode (Menu Freestyle Merge)
- [`Docs/UI_STYLE_GUIDE.md`](Docs/UI_STYLE_GUIDE.md): **The Froglet UI style guide - one look for EVERYTHING Froglet makes**
  (Shombith, 2026-11-03): the game's UI, Frogma boards, artifacts and diagrams, decks, the website, the Fleet Codex and every
  studio. Garrett's six colour families with one meaning each (Lime is always the call to action; Jade is a choice you can
  claim and reclaim, the selection and focus; Ruby is no way back, Ready), his colour chart with the shades to choose from,
  the Froglet themes (Deep Sea, its light twin Shore; a domain is one accent), phones showing the PC UI (portrait menus picked on Frogma), type, the corner sliver, buttons and states, cards, windows, toasts, motion, the safe area, each with a
  do and a don't from our own screens; drawn on Frogma's first tab (Style Guide). Tokens: `Docs/UI/froglet.css` and
  `froglet-tokens.json`, written from the game by `Tools/Build/froglet_style.py` (`--check`) - any web page loads them
  instead of typing colours. A PR touching `Assets/_Prefabs/UI*`, Menu_Main, GameCanvas, the UI theme or
  `Assets/_Scripts/UI/` answers the template's **UI checklist** (`ui-checklist.yml`, `check_ui_pr.py`); Shombith is code owner.
  Sections: 1-10 the rules; 11 the UI checklist; 12 the five worst screens; 13 open decisions; 14 outside the game
- [`Docs/MENU_REDESIGN_B.md`](Docs/MENU_REDESIGN_B.md): **The UI redesign and its design board, Frogma.** Every menu, HUD, start-flow and
  loading screen, and every variation of one, is drawn, compared and picked on the ONE Design canvas "Frogma"
  (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2; "Froglet Design Board" on 2026-11-02, "Menu Redesign B" before) before it is built.
  The canvas is in tabs ("Menu Redesign" is the master tab of every earlier round, "Compute Console", "Menu Redesign C", ...), each round
  a section (a tinted frame under its boards) and sets of boards in groups (dashed outlines): **`/frogma-new`** opens a
  new tab and asks its name; the **`/frogma`** skill (was `/design-board`) brings the board into a
  session, compares a new design against the board and the build, adds it as a new round on a tab (never a second canvas), and
  sections, groups, moves, arranges and outlines what is there; the copy of
  every board is `Docs/MenuRedesignB/board/`; `/ui-redesign-tracker` verifies a build against the picked board. Read before touching
  Menu_Main, GameCanvas's look or any `Tools/Build/author_menu_*.py`.
  Sections: Status (the item table); one numbered section per round (what was asked, drawn, picked, built; numbers claimed at commit)
- [`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`](Docs/claude/VESSEL_HUD_AND_ELEMENTS.md): Elemental bars, hull morphs, rigged-model swap, the LOCKED four-icon ability row.
  Sections: Elemental Bars (per-vessel buff/debuff display); Elemental Hull Morphs (the vessel model is an element display); The Four-Icon Ability Row (LOCKED structure — every vessel HUD)
- [`Docs/VesselModels/README.md`](Docs/VesselModels/README.md): **Vessel MODELS (the FBX hulls) are built by `/vessel-model`**, not by C# `*HullForm` surfaces (Garrett, 2026-10-17: "procedural vessel models are terrible even as placeholders"). Blender-as-a-module specs under `Tools/Build/vessel_model/specs/` rebuild each hull in place with the shipped names, bone rest poses (exact), four real element morphs, the puppetry takes and the four slots; three readers verify, a Cycles contact sheet is looked at, `build.py --check` is the drift gate. One measured dossier per vessel (Squirrel, Dolphin, Sparrow, Serpent, Manta, Urchin, Rhino, and the Ark: `ArkReverse.md` is the Arkway's Ark, its rig table and its fixed-rate omni regrowth) and the constraints a replacement meets; `COLOR.md` there is the fleet's colour language and vessel colour scheme (`vessel_colors.py --check`), `COLOR_DOMAINS.md` + `colour_domains.json` there are the SOURCE OF TRUTH for which of the six families (Fire, Gold, Lime, Jade, Blue, Ruby) a colour belongs to and where the forbidden band lies (`colour_domains.py --check`, shaped in the Colour Domains lab), `COLOR_TRACE.md` there is how every colour the game draws follows the worn colour set (`colour_trace.py --check`: a new baked colour fails until it is traced), and every class card is rendered from its model (`render_cards.py --all --check`). Read before touching any `Assets/_Models/Vessel Models/**` file or a vessel prefab's model.
  Sections: the table of dossiers; how to regenerate a number
- [`Docs/Icons/ABILITY_ICON_SKILL_PLAN.md`](Docs/Icons/ABILITY_ICON_SKILL_PLAN.md): **The ability-row ICONS are made by `/ability-icon`**
  (`Tools/Build/icon_kit/`: a hull's real mesh projected or skinned into its takes, parts a HUD tints / swaps / turns,
  the plate gauge for any how-much, legibility gates, states sheets from the spec, a writer that keeps every guid).
  Garrett's bar: "the icons are teaching the ability". Read before touching `Assets/_Graphics/Icons/AbilityIcons/**`,
  a `<Hull>HUDVariant.prefab` ability card or a `*HUDView` card method. Elements have no colour (LOCKED).
  Sections: the plan; the audit; round status (§8); decisions (§10)
- [`Docs/SKILL_TRAINING_PLAN.md`](Docs/SKILL_TRAINING_PLAN.md): **The next skills to train** (Garrett, 2026-11-07: "what is
  missing or what could use more improvements"): the measured audit of what gets redone (`Tools/Build/rework_ledger.py`:
  reverts, retirements, "again" commits, compile fixes, the 169 never-verified checklist sections, the 43 menu mockup
  rounds, every skill's maturity), the shape that made `/vessel-model` and `/ability-icon` work, and the ranked rounds:
  see the real game without a person, the world's look, the principles ledger and design brief, UI first drafts and
  board parity, sound slots, multiplayer hand tests. Update its §6 status in every change that moves a step.
  Sections: what made the last two rounds work; the audit; the ranked list; how to spend the cycles; decisions; status
- [`Docs/Pilots/PORTRAITS.md`](Docs/Pilots/PORTRAITS.md): **The PILOT PORTRAITS are made by `/pilot-art`**
  (`Tools/Build/pilot_art/`: the brief composed from the pilot's record - the first animal's head, ONE feature
  from the second, the faction's gear in its domain colour, the callsign's mark - the painter protocol through the
  Figma connector, the key-colour matte, the finish framed to the 32 painted portraits' measured envelope, the
  gates, the sheet beside the nearest references, the writer that keeps every guid and wires the `PilotSO`).
  Garrett's bar (2026-11-07): "art like the ones we have but even better". Read before touching
  `Assets/_Graphics/Pilots/**`, a `Pilot_<Callsign>.asset` portrait or `Tools/Build/pilot_art/**`. The painted set
  is the reference and is never overwritten. A pilot's realistic widescreen SCENE and the player AVATAR cut from its
  close-up are made by **`/pilot-creator`** (`scene.py`, `refs.py`, `Docs/Pilots/art/scenes/`; Garrett, 2026-11-11:
  the scenes replaced the signature busts; each pilot is designed as ONE creature; the sea in a picture is painted
  from the game's own frames). It also rolls a player's unique pilot (`creator.py`), and `blind.py` is the blind
  read that measures whether a stranger names both animals. The eighteen callsign scenes are in the build
  (`author_pilot_scene_assets.py`) for the Ark's Pilots tab and each pilot's full-screen profile page
  (`PilotProfileModal`, `Docs/MENU_REDESIGN_B.md` section 92.1).
  Sections: what a portrait is; the painted set measured; the kit; the rounds; what comes next; decisions; the signature busts; the scenes
- [`Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md`](Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md): Namespace convention, key systems & classes reference, async pattern.
  Sections: Namespace Convention; Key Systems & Classes; Async Pattern
- [`Docs/claude/ANTI_PATTERNS.md`](Docs/claude/ANTI_PATTERNS.md): Read before writing or reviewing C#.
  Sections: Anti-Patterns to Avoid
- [`Docs/claude/SHADERS_AND_PERFORMANCE.md`](Docs/claude/SHADERS_AND_PERFORMANCE.md): orthographic views (no Panini on any orthographic camera, `OrthographicLens`; 2D marks skip post through `UnprocessedOverlay`, gate `check_orthographic_views.py`) and the one picture-in-picture frame (`PipViewFrame`, style-guide tokens); the LOCKED rule that nothing in the world looks flat (flat is the UI's look: world effects are lit, faceted volumes scaled with their size); the LOCKED rule to show a world fact in the world, in 3D (light or shape the thing), never as 2D text or a ring on the screen (Garrett 2026-10-21); HLSL / Shader Graph, performance standards, prism performance — Performance Standards now routes to the perf record (`Docs/PERFORMANCE_OPTIMIZATION.md` first, `MEMORY_AUDIT.md`, `PLATFORM_UNIFICATION.md`), the in-game instruments (`diag`, `prof`, `freeze`/`ab`, `renderers`), the allocation gates and the editor-vs-build measurement rules. Read before any perf work.
  Sections: Shader & Visual Development; Orthographic views and picture-in-picture frames; HLSL / Shader Graph; Performance Standards; Prism System Performance
- [`Docs/claude/SESSION_DECK.md`](Docs/claude/SESSION_DECK.md): The session deck plugin: `/deck` pane of buttons (team activity, gates, reference compile, sync, ship), band, status line.
  Sections: What it adds; What each button runs; How it loads; Changing it
- [`Docs/claude/PLUGINS_AND_CONNECTORS.md`](Docs/claude/PLUGINS_AND_CONNECTORS.md): Claude plugins and connectors that help build this game (unity-perf, Unity's official plugin, Sentry, Figma), the check-and-suggest rule a session follows before Unity perf, C# review, URP, UI, physics or package work, and the plugin-driven performance restart.
  Sections: The rule every session follows; Plugins; Connectors; The performance restart (planned 2026-10-08)
- [`Docs/claude/VIDEOS_AND_PREVIEWS.md`](Docs/claude/VIDEOS_AND_PREVIEWS.md): **Show a change instead of handing over a "go and check" step** (Yash, 2026-10-27): a still by default, a clip (60 s max, 15 fps) for motion; measured cost (~8-10 min and ~10-15k tokens a clip once the recipe is known, against no tokens for the person at the editor); the Amoebius recipe (xvfb, `--frames`, `--render-from`, the control port, ffmpeg; the replay it records); what an Amoebius clip cannot show (the final look, feel, audio).
  Sections: The rule; What it costs; How; What an Amoebius video does not show
- [`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`](Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md): Read before designing any gameplay feature: fundamentals, order of preference, universality. Holds the LOCKED prism-movement rule (Garrett 2026-10-28): a moving prism that hits another destroys both, and a collision a shielded prism is part of bounces instead (`Docs/PRISM_MOTION.md` 1, 1a).
  Sections: Design Philosophy: Favor Emergent Systems Over Bespoke Solutions; The fundamentals (working list); Process for curating fundamentals; Order of preference; Don't "cheat" emergence without asking; Universality — one HyperSea, one rule set; When in doubt

## Team: know who works where, and build on each other's work (read before working)

Cosmic Shore is built by **Garrett, Caleb, Shombith and Yash** (the core four) plus other collaborators. Almost
all of it goes through Claude sessions, so git rarely says which person drove a change. On 2026-10-13 one merge
needed about 110 conflict fixes because work had been done twice: the Stoat in two places, enum value 14 taken twice,
the engine renamed twice, retired tools brought back. **Every session:**
- **Find your lane: `/swimlanes`** (Garrett, 2026-11-08). A lane is a vessel, a game, an app-shell section or a
  mechanical system (the Thresher, Bloomrush, the Voyages map, ecology), not a task. **Anything bigger than a couple of
  prompts and one PR gets a lane** (Caleb, 2026-11-11): vessels and game cards automatically; a section or system no
  lane covers goes into `lanes.json` with the commit that starts it, and is archived there when truly closed out. It shows the open lanes with obvious work first, the OCCUPIED ones (unmerged work
  pushed in the last 7 days: who, which branch), then the ones that need polish. Occupied is the heads-up: tell the
  person and take another lane or talk to the occupant. Work that merged earlier is context, not a block. `Docs/TEAM.md` rule 12.
- **Only a swim lane collision waits; everything else goes through and is approved after the fact** (Garrett,
  2026-11-10: "only slow people down when they collide with a swimlane"). Before committing, run
  `python3 Tools/Build/approvals.py check --me <person>`. **Exit 2 (COLLISION)**: the change is about a lane someone
  else occupies; stop and tell your person, who talks to the occupant or decides to go ahead. **Otherwise it goes
  through**, even when it replaces, retires, renames or re-tunes another collaborator's work: tell your person in a
  line whose toes it is on so they talk, and queue an entry for each one whose work it changed
  (`approvals.py add --to <them> ...`, committed with the change). Each person answers their queue with **`/approve`**
  (approve, or discuss with whoever made it). `Docs/TEAM.md` rules 3 and 13, `Docs/Approvals/README.md`.
- **Look before you touch.** Run `git fetch origin`, then `python3 Tools/Build/who_works_where.py query <the paths you
  will change>` (about 10 s). It shows who has unmerged work in flight there and who landed what.
- **Say who else works there**, in a line or two. Read their branch, docs and skill, and build on them.
- **Never replace, retire, rename or re-tune another collaborator's work silently**: name them to your person and
  queue it for their `/approve` (above). Their yes is no longer needed first, unless it is a lane collision.
- **Look for what exists before building anything new** (tool, system, skill, studio, lab, doc), and extend it.
- **Claim shared numbers only after checking `bleeding-edge` AND the in-flight branches**: enum values, `GameModes`
  ids, learnings ids, Netcode hashes, GUIDs.
- **Leave a trail**: write "Affects: <person>'s <area>" in the commit or PR whenever you touch someone else's area.
- **`/ship` merges on a GO** (Garrett, 2026-11-10): the PR goes into `bleeding-edge` with a merge commit, where the
  team batch-tests shipped branches together and fixes what fails on a new branch (`/ship` §5.7). Say "PR only" to stop at the PR.
- **Report on request**: when anyone asks for a report on a shared area or another person's work, build it with
  `python3 Tools/Build/team_report.py area <key>` (or `person <name>` / `paths <paths>`) plus the area's report in
  `Docs/Reports/`, never from memory (`Docs/TEAM.md` rule 10; the `report_request.py` hook reminds you).
- **Hand-offs are tickets on Froglet Tasks**: GitHub issues labelled `team-task`, read on the Froglet Tasks page
  (https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f). An editor test, playtest, setup step, content gap or decision your work leaves for
  someone else goes on one, **through the `/team-tasks` skill, and only once the person approves**: a session may
  *suggest* a ticket (it waits on the page until that person or an approver approves it), never file one on its own;
  no hook, no end-of-session filing, no re-ranking on your own (Shombith, 2026-10-25). People sign in to the page with
  claude.ai; the approvers (the core four and Iggy, `Docs/TEAM.json`) are its Editors and let others in. Opening the
  page at the start of a day is recommended, not required; the skill does everything it does. **Never assign one**: never a
  person, never a volunteer (Shombith, 2026-10-23). A ticket is nobody's until someone takes it; then it shows as theirs.
  Naming a branch starts it (not started in 14 days, it goes back to open), and it closes when that branch merges (`.github/workflows/team-tasks.yml`). **One ticket per branch**: every
  hand test your branch leaves is a checklist line on its one ticket (49 branches had filed 242 open tasks in 9 days).
  A ticket nobody takes in 14 days is archived.
- **No favourites among the core four.** Garrett, Caleb, Shombith and Yash carry equal weight. Never assign a task
  to anyone, and do not let one person's request override another's area silently: whoever decides there hears of it
  in their `/approve` queue. **Shombith heads tooling** (the Amoebius engine, editor tools, the Claude skills such as `/ship`, and the
  hooks; Shombith, 2026-10-23): changes to those go into his queue.
- **Areas have owners; tickets do not.** An owner is who you TELL when you change an area, and whose `/approve` queue
  the change goes into: `who_works_where.py query` prints the owner `Docs/TEAM.json` declares (`owners`) above the merge
  counts, which credit whoever merged. **Anything on UGS or the backend is Shombith's**: code there goes into his
  queue like any other change, but anything POSTED to the live backend (a deploy, a policy, production data) still
  goes through Shombith first; for now Yash does the Cloud Code work. Shombith leads activation, with Caleb heading FTUE (2026-10-26).

Who is who, the measured map and the full rules: [`Docs/TEAM.md`](Docs/TEAM.md) (identities in `Docs/TEAM.json`).
The per-prompt team hooks (`team_context.py`, `team_activity.py`, `team_tasks_nudge.py`, deleted 2026-10-25) were switched off on
2026-10-23 (Shombith: too noisy, and they slowed every chat). Ask for what the others pushed when you need it:
`python3 .claude/hooks/team_activity.py --peek 12`. The Froglet Tasks page (https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f)
is the board people read; the `/team-tasks` skill syncs it with the issues.

## Amoebius and the Vessel Studio (DECIDED 2026-10-19, `Docs/TOOLING_DECISIONS.md` §6)

Shombith, who started the engine and drives it, settled it on 2026-10-19:
- **Amoebius stays.** It is a long-term goal: Froglet's own engine, so the team can one day leave Unity completely.
  That is not plausible now (legal challenges, the porting, other problems), so Unity stays the engine the game
  ships on. The engine is focused solely on the game, and Shombith drives what is good for it.
- **The Vessel Studio is an artifact, not part of the engine.** The engine carries no studio work. Whether the
  artifact itself is kept is still the team's call; Yash drives it.
- **No single tool unifying everything**: the cost is more than the team will put in.
- Proposed: an artifact-storing webpage that keeps the synced artifacts anyone thinks are important (open).

What sessions do:
- **Do not ask whether Amoebius or the Vessel Studio helped**, and do not open answers with an "under evaluation"
  line. Shombith asked for this ("please stop asking these questions"). `Docs/TOOLING_FEEDBACK.md` stays as a log for
  anything someone volunteers.
- Do not put Vessel Studio work into `Port/`. Build engine work and studio work as separate tasks.
- Offer the Vessel Studio for vessel work in one line, as an option; never require it.
- **The rules below stay in force while the studio exists.** But before writing artifact numbers over Unity values
  that someone play-tuned in Unity, tell your person who tuned them (`who_works_where.py`) and queue it for that
  tuner's `/approve`; only an occupied lane (`approvals.py check`) waits for them.

### Working with the Vessel Studio (while it is kept)

**"Bring it to Unity" means "put it in the Toybox's Vessel Changer"** (Garrett, 2026-10-17: "my workflow is artifact
to vessel selector in toybox"). A port is done when the person can fly it from the freestyle Vessel Changer, not when
a play-mode menu or a scene reaches it. Anything that is not a hull of its own (a seat, a crew role) ships on a hull
class (`Tools/Build/author_wrasse_assets.py` is the pattern). Details: `/labmaker` §7, `/artifact-to-unity` §2.

For vessel work (how a vessel flies, its AI, difficulty levels, play styles or cameras), **offer the Vessel Studio**
to the person in one line: the ONE claude.ai artifact
(https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) where the team flies, watches and decides vessels and their AI on
phone or PC before the game changes. **Load `/vessel-studio`** for it: §1.5 is the universal panel kit every studio is
built from (the six settings tabs Scene / Game / AI / Play Style Config, Input and Vessel Config; dropdown rules; the
Players list with + / -, domain, difficulty, play style and view; the Easy / Medium / Hard levels; the I1-I4
intensity ladder; the three cameras). Load `/vessel` for the Unity side. Never publish a second Vessel Studio (hub) artifact; that rule is the shared hub's
only: a `/labmaker` lab, a vessel's included, publishes its own artifact to explore in and takes its findings to Unity by `/labmaker` §7.
**The artifact is the ONE source of truth; Unity is its clone** (the user, 2026-10-10): features and numbers are made
and decided in the artifact first, then **`/artifact-to-unity`** clones them into the game
(`Tools/Build/studio_to_unity.py` writes the artifact's values into the vessel's assets, `--check` is the drift gate;
features are ported line for line with page-golden tests). Never build a Unity tuning window or tabs that re-draw a
studio's parameters, never rename an artifact parameter on the Unity side, never hand-edit a mapped field.
**The pipeline is artifact → Unity, nothing in between** (Yash, 2026-10-25, `/vessel-studio` D37, which retired the
2026-10-14 "three surfaces" rule): a new vessel is prototyped in the artifact WITH its mini-game and its four abilities
(the element map, the level-5 upgrades), and `/artifact-to-unity` brings all three into the game: the hull to the
Vessel Changer, the mini-game as a real mode (`/arcadegame`), the abilities as the element map. Studio work no longer
adds anything to Amoebius's VESSEL STUDIO page or its studio backend; what is already there is left as it is
(`parity_gate.py` still guards it). A project hook
(`.claude/hooks/vessel_studio_nudge.py`, in `.claude/settings.json`) shows the person this notice once per session
when a prompt is about vessel work. Studio domain colours come from the game's palette (`studio-domains.js`, generated by
`.claude/skills/vessel-studio/domain_colors.py`), the same values Amoebius renders.

## AI learning is automatic (LOCKED rule, Yash, 2026-10-26)

"I don't wanna click any commands or anything. Never." Every race played in the editor is recorded and sent on its own
(`HumanFlightRecorder`, `BugHuntAutoSend`), and the daily **AI learning sweep** relearns the AIs from it
(`Tools/Build/ai_learn.py --run`), judges whether they now race like the person, fixes what numbers cannot, and pushes
to `Ys-bleeding-edge`. **Never ask a person to run a learner, a report or a test command**: run it yourself or leave it
to the sweep. A new learner joins `ai_learn.py`. **The AIs train in each vessel's OWN minigames (its Time minigame
first), never in Regatta**; the arena games are rebuilt later from those skills (Yash, 2026-10-27,
`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md`). Details and what is not automatic yet: `Docs/AI_SYSTEM/AUTO_LEARNING.md`.

## AI training serves one plan (LOCKED rule, Garrett, 2026-11-02)

**Read [`Docs/AI_SYSTEM/AI_PLAN.md`](Docs/AI_SYSTEM/AI_PLAN.md) before any AI training, tuning or learner work.** In
a voyage, a squad seat's AI is chosen by its cards:
- the **vessel and element** choose the genome (one universal AI per vessel per element, trained on that element's
  arcade game and scored on I1-I4 together);
- the **pilot** biases its choices toward what its callsign sees;
- the artifact comes later.

The ladder is the element's arcade intensities, then its arena game balanced against those FIXED AIs, then voyages.
**Every AI effort names the plan step it serves** (`Docs/AI_SYSTEM/ai_plan.json`):
- a compute job off the plan is never run, and `compute.py validate` fails on it;
- a commit that trains or tunes an AI says `Plan step: <id>`;
- work that serves no step is a proposal to Garrett first.

A new round grows from every genome that exists. No AI is trained inside an arena game.

**Froglet Compute is PAUSED (Shombith, 2026-11-06): analytics has the priority.** Until he switches it back on, queue
no compute job, send no run and install no agent; the console's hourly sync scheduled task is paused, and the rule below
waits with it. Train or check an AI by hand only as the AI plan allows. The hourly AI learning sweep (`ai_learn.py`) is
separate and unchanged.

**Froglet Compute is the default way to train an AI** (Shombith, 2026-11-02). A search, a Unity trainer run or a
re-run of a learner goes on the team's machines through Compute, never as a sweep a session runs by hand or a trainer
session in one person's editor: a new search is a `/compute-job` (its job names its plan step), a Unity trainer run
is the console's **New run** (`compute.py request` from a terminal), and the console's **AI plan** page
(https://130-210-34-146.sslip.io/#plan; the raw doc at `/ai_plan.md`) shows each step and the searches serving it.
Run something by hand only to prove an evaluator before queueing it (`/compute-job` §4). The hourly AI learning sweep
(`ai_learn.py`) stays as it is.

## A game mode never changes the vessel (LOCKED rule, Shombith, 2026-10-31)

"The vessel kit as designed always stays the same, we just make the game mode interesting." A mode makes itself
interesting with its **track, environment and AI**, never by changing what a vessel's ability does: no rationing an
ability (Serpent Time's crystal-bought sniper rounds), no press vetoes, no added contact effects (its walls that cost
speed), no per-mode rescaling, no edits to vessel code or assets for one mode. Both of those Serpent Time rules were built
and retired for this. A kit change is a vessel change, made on the vessel (its owner hears of it through `/approve`), never through
a mode. `/arcadegame` §6.

## Every colour comes from the colour set (LOCKED rule, Garrett, 2026-11-10)

"All colors come from a color set. If that rule is broken then it is a sign of incompleteness or a bug, not something
to be respected." Every colour the game draws (UI, world, sky, effects, vessels, textures, code, configs) is a value
the worn `SO_ColorSet` carries: its domain and environment blocks, or its `TracedColors`. Photic holds the values the
game was authored in. No area, vessel or owner is exempt and none decides otherwise: a colour that does not follow the
set is a bug to fix (route it through the set: `ColorTrace.Retrace`, a domain material set, a set field, the texture
grade), never a choice to keep or a question for someone. `python3 Tools/Build/colour_trace.py --check` fails on a new
colour that is not traced. How it works and what is still unfinished: `Docs/VesselModels/COLOR_TRACE.md`.

## Testing

### Test Infrastructure

- **Framework**: Unity Test Framework 1.6.0 (NUnit-based)
- **Edit-mode tests**: `Assets/_Scripts/Tests/Editor/` — 373 C# files (2026-11-01, `find Assets/_Scripts/Tests/Editor -name '*.cs' | wc -l`; 397 under any `Tests/` folder in `Assets/_Scripts`) covering enums, data SOs, geometry utils, party data, resource collection, disposable groups, camera settings, etc.
- **Bootstrap tests**: `Assets/_Scripts/System/Bootstrap/Tests/Editor/` — `AppManagerBootstrapTests` (file: `BootstrapControllerTests.cs`), `BootstrapConfigSOTests`, `SceneTransitionManagerTests`, `ApplicationLifecycleManagerTests`, `ApplicationStateMachineTests`, `SceneFlowIntegrationTests`
- **Multiplayer tests**: `Assets/_Scripts/Controller/Multiplayer/Tests/Editor/`
- **SOAP tests**: `Assets/_Scripts/Tests/Editor/SoapCoreTests.cs` (behaviour) and `SoapFormatTests.cs` (the asset format our SOAP shares with the Obvious plugin it replaced on 2026-11-06; `Docs/claude/ARCHITECTURE_CORE.md` § SOAP)
- **Test scenes**: `Assets/_Scenes/TestInput/`, `Assets/_Scenes/Game_TestDesign/`

### Build & CI

Workflows in `.github/workflows/` (2026-10-29):

- `unity-ci.yml` (Unity CI): compile / mono / il2cpp / static tiers. **No Unity runner has been attached since 2026-10-13**, so the `unity` job is skipped while `UNITY_RUNNER_LABEL` is unset; only the static tier does real work. A green run here is not a Unity compile.
- `amoebius-parity-ci.yml` (Amoebius parity CI): nightly (11:17 UTC, on bleeding-edge) and by hand from the Actions tab, compiles the port, runs the engine tests and smoke-runs scenes. It stopped running on every bleeding-edge push on 2026-11-13 (Shombith): it had never passed and mailed every merger a failure.
- `bleeding-edge-guard.yml` (Bleeding-edge guard): verifies every commit landing on `bleeding-edge` / `development` and attempts a repair.
- `build-branch-ci.yml` (Build branch CI): verifies `build/android` and `build/windows`.
- `ios-testflight.yml` (iOS TestFlight): Xcode sign + upload, no Unity step.
- `perf-daily.yml` (perf-daily): daily perf gates plus Amoebius allocations.
- `sync-build-branches.yml` (Promote test build): every third Wednesday, `development` → build branches.
- `tag-internal-build.yml` (Tag internal build): Friday tag of `bleeding-edge`.
- `team-tasks.yml` (team-tasks): the ticket sweep.
- `toybox-publish.yml` (Publish browser Toybox): manual, publishes WebGL to R2.

Player builds themselves are still made by hand from the build profiles in `Assets/Settings/Build Profiles/`.

### Unity CLI verification — the verify rule gates every C# commit (LOCKED rule)

The repo carries **`com.unity.pipeline`** (`0.5.0-exp.1`, experimental) so a session can drive the
**open** Unity Editor from the terminal via the standalone `unity` binary — real compiles, Play
mode, in-editor verification, no human alt-tabbing and no copy-pasted console errors. Setup +
troubleshooting: `Docs/unity-cli-setup.md`. The CLI is experimental and changes often; **`unity
--help` for the installed version is authoritative** — never assume a command or flag from memory
or from another machine.

- **The verify rule (Shombith's decision, 2026-10-29): every C# commit passes `Tools/Build/unity_refcompile` (`bash Tools/Build/unity_refcompile/run.sh`) plus whatever `/ship` already gates on (`bash .claude/skills/ship/gates.sh`). When a Unity editor is open, also run the `unity` CLI. If neither ran, say so in the commit/PR and add an entry to `Docs/UNITY_VERIFICATION_CHECKLIST.md`.** Never claim a verification that didn't run. `/verify-unity` is not required until that skill merges.
- **Headless real-editor check: `bash Tools/Build/verify_unity.sh --project <full worktree> [--commit <sha>]`** compiles with the real Unity editor in batchmode and runs the EditMode tests; it prints compile errors as `file:line` and exits non-zero (1 compile, 2 tests, 3 setup, 4 no verdict). This is the runnable `/verify-unity` until that skill merges. It needs a full (non-sparse, imported) checkout, refuses when another Unity has the project open, and restores the previous HEAD after `--commit`. `--no-tests` compiles only; `--filter` narrows the tests.
- **`[CliCommand]` wrappers** are the editor methods exposed to the CLI's `unity command` surface —
  the sanctioned way to give the CLI a repo-specific operation. They are editor tooling: they live
  under an `Editor/` folder (Assembly-CSharp-Editor), never in runtime code.
  <!-- TODO: no [CliCommand] wrappers exist in-repo yet; when the first one lands, record its
       location and the naming convention here. -->
- **The runtime component never ships.** `UnityPipelineReleaseGuard`
  (`Assets/_Scripts/Editor/Build/`) is an `IPreprocessBuildWithReport` that throws
  `BuildFailedException` on any **non-development** build whose shipped content (build scenes,
  prefabs they instantiate, Resources, preloaded assets) would include the `com.unity.pipeline`
  runtime component. Development builds are exempt — that is the sanctioned way to debug the CLI
  integration in a player. Do not weaken or bypass this guard: release builds go to paying Steam
  customers.

## Editor Tooling (LOCKED convention — read `Docs/TOOLING.md` before adding any `[MenuItem]`)

**Every first-party editor tool lives under ONE menu root, `FrogletTools/`, and appears
automatically in `FrogletTools > Froglet Master Tool`.** The `Tools/Cosmic Shore/…` and
`Cosmic Shore/…` roots were retired — do not reintroduce them, and do not add a tool under
`Tools/`, `Window/`, or a new root of your own.

- **Discovery is automatic, never registered.** `FrogletToolRegistry` reflects over `[MenuItem]`
  attributes; a tool shows up on the board the moment its path starts with `FrogletTools/` and it
  compiles. There is no manifest to update. That prefix is also the only filter, so third-party
  package menus (FMOD, Soap, Quick Scene Pro) are never picked up and are left where
  their vendors put them.
- **The board is a card grid**: one collapsible colour-coded section per category, one card per
  tool (title, description, five-dot importance), most important first, flowing into as many
  columns as the window is wide enough for.
- **`[FrogletTool(category, Importance, Description)]`** on the same static method as the
  `[MenuItem]` controls the section, the ranking (1–5, which is also the dot rating on the card)
  and the blurb. It is optional — omit it and the registry infers a category from the path/type
  name and uses importance 3. The attribute compiles into the **editor** assembly, so only files
  under an `Editor/` folder can use it; a runtime-assembly tool behind `#if UNITY_EDITOR` still
  appears, just with inferred metadata.
- **Draw through `FrogletEditorPalette`** (banner, `ColorButton`, `StatusPill`, `DrawCard`,
  accent stripes, semantic Ok/Warn/Error/Info colours, light-skin adaptation) so every Froglet
  window reads as one product. Do not hand-roll `GUI.color` juggling in a new window — extend the
  palette instead.
- **Prefab drift is a first-class check.** `PrefabInstanceSceneScanner` reads prefab-instance
  overrides straight out of scene YAML (fast, read-only, no scenes opened) and
  `PrefabDriftFixer` performs every write through `PrefabUtility` on a properly loaded scene.
  Use these rather than opening scenes to interrogate `PrefabUtility`, and never hand-edit scene
  or prefab YAML to "apply" an override. **FrogletTools > Ecology > Audit Cell-Owned Visuals**
  rides the same scanner for the Cell's half of this: it reports scene-placed membrane/nucleus/
  cytoplasm instances that duplicate what the scene's Cell already spawns, and Cell overrides whose
  `propertyPath` names a field the script no longer has (Unity never prunes an unresolvable
  modification, so retired fields linger for years pointing at guids no asset carries).
- **Editor-tool config belongs in a ScriptableObject**, not a hard-coded list in the window
  (`GameModePrefabKitSO` is the reference) — same config-separation rule as gameplay.
- **A tool's OUTPUT is the deliverable; the tool is scaffolding.** A wirer/setup/migration tool
  writes a scene, prefab or SO into the human's **working tree**, while the branch carries only
  the tool — so the tool merges and its data does not, and the feature is broken on every other
  machine with nothing in the diff to explain it. Any tool that writes assets therefore
  `FrogletToolChangeLedger.Record(ToolName, path)`s in the same block that writes each one and
  draws `FrogletToolShipPanel.Draw(Ship, this)`: **Validate & Push** (saves, validates, stages
  ONLY that tool's recorded paths — never `-A` — commits, pushes; protected branches refused) and
  **Retire Tool** (deletes the one-off + scratch assets, refusing while its output is still
  unpushed, so retirement can't strand it). The catch-all is **FrogletTools > Build > Pending Tool
  Changes**, which also lists dirty files no tool claimed. Contract:
  `Docs/TOOLING.md` § "Tool output is a deliverable". Agent-side gate: the `/ship-tools` skill,
  and `/ship` §2.5 — which `/ship-quick` and `/ship-deep` inherit and **no mode may skip**. A
  READER tool (audit/report only) needs none of this; say so in its doc comment.

## Shared prefabs are single sources of truth (see `Docs/GAMECANVAS.md`)

`GameCanvas.prefab` is the in-game UI surface for every mode; the same rules apply to any prefab
shared across scenes.

- **A scene override always beats the prefab.** Overrides parked in a scene are why editing the
  prefab stopped changing anything — six game-mode scenes each carried ~1,770 unapplied overrides,
  1,734 of them byte-identical. If a change should apply to every mode, **Apply to Prefab**.
  **An override that NULLS a reference is the dangerous shape**, because the prefab looks correctly
  wired and nothing on it says otherwise: `ArcadeGameConfigureModal.prefab` wires `domainInfoItems`
  to its three real tiles and Menu_Main overrode all three to `{fileID: 0}`, so the modal attached
  no click listener to any domain tile (`if (!item || !item.Button) continue;`) and **a domain pick
  never reached the server in any mode** — every player flew the Jade default, which reads on screen
  as blue. Fixed by DELETING the overrides (`Tools/Build/fix_domain_picker_wiring.py`, `--check`) so
  the prefab's own wiring applies — never by re-authoring the same references into the scene, which
  is how the override got there. General rule: **a nulled reference fails as "the feature quietly
  does nothing", not as an error**, so audit for `objectReference: {fileID: 0}` overrides on any
  serialized list a feature depends on.
- **A variant, never a copy.** If a mode needs a different canvas, use **Create ▸ Prefab Variant**.
  `GameCanvas-SkimRace.prefab` is a hard copy, which severed propagation and left 8 references
  dangling into the other prefab asset.
- **Genuinely per-mode values go in config or code**, not a scene override: an SO keyed by
  `GameModes`, or a runtime resolve. There is exactly one `MiniGameControllerBase` per gameplay
  scene, so the canvas finds it itself (`MiniGameHUD.EnsureReadyButtonWiring`,
  `Scoreboard.ResolveGameController`) — an explicit inspector assignment still wins.
- **Never bind a UnityEvent to a concrete controller subclass.** `OnReadyClicked` is public on
  `MiniGameControllerBase`; naming `SkimRaceController` in the inspector creates a per-scene
  override for no gain.
- **Run `FrogletTools > Game Modes > Game Mode Prefab Kit` ▸ Validate before committing a scene**
  that contains a shared prefab.
- **A scene's structural edits on an instance are part of its state, and an override count does not
  show them.** Every one of the 15 GameCanvas fork scenes REMOVED the prefab's own HUD and Scoreboard
  components and re-added them as scene components, deleted the end-game subtree and added a newer
  one — so "consolidate the uniform overrides into the prefab" would have produced a prefab nobody
  runs. Read `m_RemovedGameObjects` / `m_RemovedComponents` / `m_AddedComponents` (the scanner
  counts them) before deciding what a prefab should become, and take the shipped state from a
  DONOR SCENE, not the asset. Retire a fork with **FrogletTools ▸ Game Modes ▸ GameCanvas Unifier**
  (`Docs/GAMECANVAS.md §9`) — a guid swap by hand dangles every override and every scene reference.

## Show the change, cheaply (read `Docs/claude/VIDEOS_AND_PREVIEWS.md`)

A session that changed how something LOOKS or MOVES shows it when it can, instead of asking a person to go and check:
a still by default, a short clip (60 s max, 15 fps, 1280x720) for motion or order of events, made in Amoebius in a cloud
session or with Playwright for an artifact. The session reads at most 3-4 frames of its own clip (about 1.5k tokens
each), sends the file with `SendUserFile`, never commits it, and says in the same message what only the Unity editor
can judge (the final look, feel, audio). A clip never replaces the verify rule (§ Unity CLI verification) or the checklist's hand test.
For the FINAL LOOK, the look sheet (`Docs/LOOK_SHEET.md`, `/unity-look`, built 2026-11-07, not yet run in Unity) captures the real
renderer headless (menu screens, a race with the AI on every seat), gated and laid out, locally or through the `Look sheet` CI job.

## Code Style

- Clean, maintainable C# — favor readability over cleverness
- Use `[Header("Section Name")]` and `[Tooltip("...")]` attributes generously on serialized fields
- Use `[SerializeField]` with private fields, not public fields
- Pattern match where it improves clarity: `effects is { Length: > 0 }`
- Use `TryGetComponent` over `GetComponent` + null check
- Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
- Anti-spam / cooldown patterns belong in the SO config, not hardcoded
- Always assign static numeric values to enum members to prevent Unity serialization drift
- **All player-facing text has a human-facing control (LOCKED rule, every branch).** Every word the
  player can read is a serialized field on an SO, prefab or component, or in an authored table. A
  human can add, edit, blank or delete it in the inspector (or an authoring window) without touching
  code. This is the text twin of the FMOD rule ("every sound is an exposed `EventReference`"). The
  rule in practice:
  - **Never** put a C# string literal that reaches a `TMP_Text`, toast, dialogue or button label.
  - Text whose facts are computed is an **authored template with tokens** (`Hold {glyph:Time} to
    boost`). Code fills the tokens; a human owns the sentence. `TemplateText.Fill(template, "token", value, ...)`
    (`Assets/_Scripts/Data/TemplateText.cs`) is the one filler; it returns empty for an empty template.
  - An empty field shows nothing. That is how a line is deleted, so never fall back to a hardcoded
    default.
  - **Excluded:** logs, dev/diagnostic overlays and editor-tool UI.
  - Existing literal-built strings are the **legacy shape**. Move them to authored fields when you
    touch them. Worked example: `Docs/ModePreview/TRAINING_PLAN.md` §4.2.1.
- Commit messages follow conventional commits: `type(scope): summary` (see `GIT_RULES.md`)

## Debugging Methodology

When investigating issues, follow this systematic approach:

1. Reproduce the issue consistently
2. Add `ProfilerMarker`s to isolate the hot path
3. Check the call stack in Timeline view for self-time
4. Narrow to the specific derived class (base class profiling often hides the real culprit)
5. Fix, profile again, confirm improvement with data

Do not guess at performance problems. Profile first.

## Communication Preferences

- Be direct and technical. Skip preamble and motivational framing.
- When presenting solutions, lead with the code, then explain if needed.
- If you need to make a judgment call between two valid approaches, pick the one that's simpler to maintain and mention the tradeoff briefly.
- When refactoring, preserve the existing naming conventions and folder structure unless explicitly asked to reorganize.
- For shader work: always specify which render pipeline stage and what Shader Graph node types are involved.
- Don't repeat back what I just told you. Acknowledge briefly and move to the solution.

## What Claude Code Should Never Do

- Stop to ask "would you like me to continue?" after completing one of several related files
- Introduce new packages or dependencies without flagging it first
- Restructure folder organization or namespaces without explicit instruction
- Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
- **Write a lookup that cannot be asked as a QUESTION — one whose only form reports a miss as an error.** A `TryGet` that logs is a DEMAND ("give me this; a miss is a fault") and is right where the caller is about to act on the result. It is wrong for a PROBE ("which of these exist on this build?"), and the two readings end up sharing one method because the demand is written first. `VesselPrefabContainer.TryGetShipPrefab` did: **nine** of its twelve call sites were probes, including `ToyVesselRoster.ResolveOffered`, which runs every time a toy matrix is built — i.e. **every domain change** — so a hull that is on the fleet roster but whose prefab is not authored yet (the Butterfly) produced a red `LogError` per rebuild, and buried the one keyed warning that names the setup tool to run. **A question that cannot be asked without raising an error makes every asker either lie or shout**: the ones that lie skip the check and reintroduce the silent-omission defect, and the ones that shout drown the message that would have fixed it. Give the lookup a `reportMissing: false` form (or a quiet `Has…` twin) and keep the loud one for the sites that really are about to spend the result. `.claude/skills/vessel/references/CONTRACT.md` §1.
- **Leave a finished system's bring-up telemetry on `CSDebug.Log` (or, worse, raw `Debug.Log`).** The dense per-step trace you need while building a system is console spam the day it works, and every such trace outlives the cycle that wrote it — the `[FLOW-n]` spawn trace and the `[GyroidColony]` census each shipped ~60 and 1-per-5s log lines forever. Put it on a **`CSLogChannel`** (`CSDebug.LogVerbose(channel, …)`, `CSDebug.IsVerbose(channel)`), which defaults to OFF and is toggled per-channel in **FrogletTools > Toolbox > Logging** — the trace stays in the tree as knowledge without shouting. Guard with `IsVerbose` first anywhere the interpolated message itself is expensive (a `[Conditional]` method's arguments are still evaluated in the Editor). Warnings and errors never move to a channel; a real fault must always be loud. And **nothing per-frame or per-contact gets a log at all** — not even a channelled one: the offenders that surfaced here were a per-skim resource log and a per-frame camera-zoom readout, both simply deleted. **The 2026-09 console sweep made this the RULE for every subsystem, not just the two that
  prompted it**: the console sat at 999+ before a match started because ~780 info sites were
  unconditional — the party layer re-logging its 3 s presence refresh, the boot chain narrating
  ~40 steps, the lattice colonies logging per PLANT, every vessel spawn printing its audio bring-up,
  and every menu screen logging one line per CARD it populated. Every subsystem now has a channel
  (`Boot`, `Party`, `CloudData`, `Audio`, `Ecology`, `ArcadeMatch`, `MenuUI`, `Input`,
  `VesselTelemetry`, `PrismRuntime`, `FTUE`, plus the per-feature ones), the
  Logging toolbox REFLECTS the enum (its hand-kept row table had already drifted nine channels
  behind), and each member carries a `[CSLogChannelLabel]` that `CSDebugTests` asserts. The
  decision per site is mechanical — a method-entry trace, a per-item population line, a data dump
  or a placeholder is DELETED; a state transition or a session/target/course fact goes to
  `LogVerbose`; a real fault stays a warning or error; and `<color=…>` / emoji never appear in a
  log. `python3 Tools/Build/check_console_logging.py` (`--self-test`) is the gate: it fails on a
  raw `Debug.Log` in runtime code and on rich text inside any `CSDebug` call, with the tool,
  benchmark and stress-test scripts whose output IS their deliverable allow-listed by path.
- Write a tooling, diagnostics, benchmark, or debug-overlay script that uses `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD` without reading `Docs/CONDITIONAL_COMPILATION.md` and running `python3 Tools/Build/check_conditional_compilation.py` first. "It compiles in the Editor" proves nothing here — the Editor always defines `UNITY_EDITOR`, so this whole bug class is invisible until the Release build fails
- Leave TODO comments as a substitute for completing the work
- Generate code that compiles but ignores the established architecture patterns above
- Add if-null guards on SOAP ScriptableEvent serialized fields — fail loud
- Plug a placeholder/temp FMOD event into a new sound, or hardcode an event path in code. Add the `[SerializeField] EventReference` (per ability, per trigger, per emitter), ship it **empty**, and say so — an unwired slot is a visible TODO; a temp event is one nobody ever finds
- **Leave a spent one-shot `assert` sitting ABOVE a generator's validation section.** A `Tools/Build/author_*.py` script typically performs a one-time migration (clone a donor scene, patch its wiring) and *then* validates everything it built. When the donor moves on, the migration's `assert` fires — and takes every check below it with it, while `--check` is still wired into the workflow and still looks like a gate. `author_dogfight_assets.py` validated **nothing** for as long as that was true, including four checks added specifically to guard the missile-tier scoring; `author_ribcage_assets.py` and `author_wildlife_liberation_assets.py` sat in that state on the identical `controller field block not found in donor scene` assertion. **Measured across the family on 2026-09-09, SIX of the eight mode generators then checked were red and nobody was reading them**, in two distinct classes: those two plus `author_drumfire_assets.py` (`donor crystal block not found`) aborted on a spent one-shot, while `author_salvo_assets.py`, `author_dogfight_assets.py` and `author_hijack_assets.py` failed their asset-key validation because they still emitted `CallToActionTargetType`, a field the call-to-action retirement deleted from `SO_ArcadeGame` — that retirement swept the 42 shipped assets and left the generators that author them untouched, so re-running any of them re-introduced the retired key. (`author_switchback_assets.py` was a third shape: its asserts passed and it reported drift from its own output.) A fourth class surfaced when the whole family was measured again: twelve generators re-emitted the legacy `CardBackground` placeholder over the `/cardart` render. **Fixed, measured 2026-10-06:** #969 moved the shared pieces into `arcade_mode_lib.py` — `card_background()` (the guid comes from the mode's render, so a card that drifts back to the placeholder fails), `card_errors`/`check_cards` (retired keys), `drift()` (Dog Fight, Hijack and Salvo used to print "no files written" and exit 0 without diffing) and `committed_scene()` (the clone stands down once the scene is committed), all held by `arcade_mode_lib.py --self-test`; #967 guarded Wildlife Liberation's spent clone and fixed Tollway. All 25 `author_*_assets.py` scripts (21 mode generators plus four flora/vessel kits) now pass `--check`, and the Ribcage and Drumfire modes have been retired with their generators. `author_urchin_assets.py --check` still validates without diffing against disk. The general lesson restated: **a generator that owns an asset's content is a second place every schema change has to land, and it does not fail at the time of the change — it fails the next time somebody runs the generator, which may be months later and on somebody else's branch.** **A spent one-shot must STAND DOWN, not abort** — guard it (`if not DONOR_STILL_MATCHES: skip`), register the already-committed output so the downstream checks describe the shipped artifact, and prove the checks fire with a negative control. Corollary worth stating separately: **a gate that aborts looks exactly like a gate that passes if nobody reads its output**, so a green `--check` in a commit message is only evidence if you can name a failure it produced.
- Use `renderer.material` when `renderer.sharedMaterial` + MaterialPropertyBlock works
