# Open tooling decisions: Amoebius, the Vessel Studio, and the one tool that builds Froglet's games

**Status: DECIDED 2026-10-19** (raised by Yash, 2026-10-15; decided by Shombith, who started and drives the
engine; §6). Amoebius stays as the long-term engine goal, focused solely on the game; the Vessel Studio is an
artifact and not part of the engine (whether the artifact stays is still open, and Yash drives it); there is no
single unifying tool. Sessions no longer ask whether these tools helped. §1-§5 below are the evidence and options
as written on 2026-10-15, kept for the record.

## 1. The questions

1. **Amoebius** (`Port/`: our own .NET engine that runs Cosmic Shore without Unity, plus the `Amoebius.exe`
   launcher). Does it help us enough to keep building, and which part?
2. **The Vessel Studio** (the claude.ai artifact https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa and its pages in
   `Docs/Studios/VesselStudio/`). Should it stay, shrink or be removed?
3. **The one tool.** What single, unified way of working should build Cosmic Shore now, and Froglet's other
   games later, so that everyone's work stays in sync?

## 2. What we have today (measured 2026-10-15)

| Tool | For | Size | Activity, last 30 days | Who drives it (pull requests merged, 60 days) | Evidence of use by others |
|---|---|---|---|---|---|
| **Unity + FrogletTools** | The game itself: everything ships from here | 535k lines of game C# | 893 commits touching `Assets/` | Everyone | Everyone |
| **The repo + Claude Code** (skills, CLAUDE.md, `Docs/claude`, Python gates) | Where nearly all work is done | 25 skills, ~200 scripts with a `--check` / `--self-test` gate | ~85% of commits are Claude sessions (3,213 of 3,787 in 60 days) | Everyone | Everyone |
| **Amoebius engine** (`Port/src/CosmicShore.Engine`, Player, tests) | Runs and tests the game with no Unity: a headless compile, 1,949 engine and 352 ported game tests, online multiplayer between machines | 252k lines of C# in `Port/` (engine + launcher + tools) | 198 commits touching `Port/` | Shombith (8 PRs), Yash (2), 5 merged by Claude | Garrett's Regatta AI roadmap names Amoebius as its test harness. On 2026-10-13 its live compile and tests verified the merge without Unity; its prefab test caught a broken Stoat prefab. The 11 C# errors that merge produced were caught by `unity_refcompile`, which is not Amoebius |
| **Amoebius launcher** (`Amoebius.exe`: PLAY, BUILD, agent chats, GIT, board / scheduler, VESSEL STUDIO page, studio server, artifact library) | One app for running, building and working on the game | (inside the 252k) | (inside the 198) | Yash, Shombith | **No measurement.** There is no telemetry, so whether anyone else opens it is unknown. Ask (§5) |
| **Vessel Studio: the pages** (fly and test a vessel and its AI in a browser or on a phone) | Decide a vessel's feel and AI before Unity | about 20k lines of HTML / JS in `Docs/Studios` | 145 commits touching `Docs/Studios` | Yash, plus Garrett's sessions | Garrett's sessions added two new vessel prototypes to its selector on 2026-10-14 (the Goby & Pistol duo lab, the Brittle Star). The same lab pattern graduated the Omni Motion Lab into the game (#1082) |
| **Vessel Studio: the collaboration machinery** (artifact database: Ask, Development requests, Sync jobs, shared decisions; the Sync panel; the Amoebius studio server) | Let several people decide and sync through the artifact | about 1.8k lines (`sync.js`, `sync_job.py`, the launcher's `Studio/` server) | (inside the above) | Yash | **Almost none:** 1 shared decision (recorded by a Claude session), 0 requests, 0 Sync jobs in the artifact's database. The first real Sync attempt (Yash, 2026-10-14) failed on a checkout whose `origin` was not the repo |
| **Labs** (`/labmaker`: Omni Motion / Shepard / Combine, Thresher Plant Lab, NCA, ecology) | Decide a mechanic by looking and playing before Unity | (in `Docs/Studios`, `Tools/`) | (included above) | Garrett | Graduated into the game: Omni Motion Lab rounds 1-5 (#1082), Thresher Plant Lab (#1077) |

### What doing the same work twice has already cost

See [`TEAM.md`](TEAM.md) for the table: one merge (2026-10-13) needed about 110 conflict resolutions.

- The Stoat was built in Unity and in the studio at once.
- `VesselClassType` 14 was taken twice.
- The engine was renamed twice.
- The same engine APIs were added twice.
- Retired tools came back from an old copy.
- Afterwards, 4 Stoat values still differed between the studio and Unity: Unity had been play-tuned away
  from the artifact, which CLAUDE.md calls the one source of truth.

## 3. Amoebius: keep, narrow, or stop?

**Value we can point to:**
- It lets a session with no Unity compile the game, run its tests and boot it headless. It verified the
  2026-10-13 merge alongside `unity_refcompile`, and its prefab test caught a broken Stoat prefab.
- It is a multiplayer test harness: up to four players, a network simulator, a relay server.
- It produces the phone builds.
- It is an engine we own outright, so no Unity licence or runtime terms apply. That last point is a strategic
  option, not a present need.

**Cost we can point to:**
- 252k lines to keep in parity with every `Assets/_Scripts` change.
- Merge conflicts whenever two branches touch it.
- A launcher whose use beyond its two authors is unknown.

| Option | What it means |
|---|---|
| **A. Keep all of it** | Engine and launcher grow as now. |
| **B. Keep the engine, freeze the launcher** | The engine stays as our headless compile / test / multiplayer harness and the phone build path. The launcher gets no new pages or features until at least two core collaborators other than its authors say they use it. |
| **C. Stop** | Archive `Port/`; verification goes back to Unity only (a person in the editor, or `/verify-unity`). |
| **D. Commit to it as Froglet's engine** | An explicit company decision to leave Unity over time, with a roadmap, an owner and a cost estimate (`Port/docs/ROADMAP.md`). |

The deciding question is strategic: **does Froglet want an engine of its own?** If not, B is the useful
part. If yes, D needs its own decision with an owner.

## 4. The Vessel Studio: keep, narrow, or remove?

**Value we can point to:**
- Prototyping a vessel on a phone or PC before Unity: the Squirrel AI lab, the Stoat rounds 1-15, and the
  Goby, Pistol and Brittle Star studios that Garrett's sessions added.
- The lab pattern has graduated mechanics into the game.

**The vision behind it:** [`Docs/Studios/VESSEL_STUDIO_VISION.md`](Studios/VESSEL_STUDIO_VISION.md) is Yash's
north star: a studio so simple that a non-programmer makes vessels, trains their AI and makes mini-games, with
Amoebius as a game engine for non-programmers. If the team adopts that vision, option A (and Amoebius's option D)
are its path. If not, B or C keep what has already paid off.

**Cost we can point to:**
- The collaboration machinery is unused (§2).
- The rule "the artifact is the ONE source of truth" was broken within three days, once Unity was play-tuned.
  That left two places to edit each number and a drift gate.
- Three surfaces to keep identical: claude.ai, Amoebius, Unity's studio window.

| Option | What it means |
|---|---|
| **A. Keep as it is** | The artifact is the one source of truth for vessels; Unity is its clone (current rule). |
| **B. Keep the pages as a design LAB** | A vessel is prototyped and decided in its studio page. Once it is built in Unity, Unity's assets are the truth and the page reads them (the studio stays a "reader of the shipped numbers", D1). Freeze the Sync panel, jobs and requests until someone uses them. |
| **C. Fold it into `/labmaker`** | Vessel studios become ordinary labs: no hub artifact, no Sync, no Amoebius studio server. |
| **D. Remove it** | Vessel work goes straight to Unity, plus Garrett's Third Eye window for watching AI. |

## 5. The one tool (Claude's recommendation, for the team to accept or reject)

**Criteria.** The tool should:
- be used by every collaborator, already;
- hold one source of truth per fact;
- cost little to keep in sync;
- run on everyone's machine;
- be verifiable by Claude without a person;
- keep what one session learns for the next.

**What already meets them: the repository worked through Claude Code.** About 85% of the commits already
come from there. Its skills, CLAUDE.md and gate scripts carry knowledge from one session to the next. Every
collaborator uses it, from any machine or phone (claude.ai/code). Unity remains the game's editor and
runtime, used for play-testing.

So the recommendation is to **name that as the one tool, and make every other tool plug into it rather than
run beside it**:
- a FrogletTool in Unity;
- a skill or gate in the repo;
- a lab page whose numbers are read from the game;
- Amoebius as the verification layer.

Each other tool gets its own scoped rule. That means **B for Amoebius** and **B for the Vessel Studio**,
until the feedback (§7) says otherwise. What keeps everyone in sync is in the same tool too:
[`TEAM.md`](TEAM.md) and `who_works_where.py` make every session see the others' work before it touches
anything.

This recommendation could be wrong:
- If several collaborators already live in `Amoebius.exe`, the launcher is the better home for the one tool
  than a terminal.
- If the shared studio database starts getting used, option A for the studio is worth its cost.

The feedback log is how we find out.

## 6. Decision record

| Date | Question | Decision | Who decided | Follow-up |
|---|---|---|---|---|
| 2026-10-19 | Amoebius: keep, narrow, or stop? | **Amoebius stays.** It is a long-term goal: Froglet's own engine, so that one day the team can leave Unity completely. Doing that now is not plausible (legal challenges, the porting, other problems), so Unity stays the engine the game ships on. The engine is focused solely on the game. Sessions no longer ask whether it helped; Shombith drives what is good for it. | Shombith (started the engine and drives it) | Garrett, Caleb and Yash told through this row. |
| 2026-10-19 | The Vessel Studio: keep, narrow, or remove? | **The Vessel Studio is an artifact and is not part of the engine.** The engine carries no studio work; the studio is its own task. Whether the studio artifact itself is kept is not decided here. | Shombith (for the engine's side) | Yash (drives the studio and the launcher) plans taking the studio out of the engine; the artifact's own future stays with the team. |
| 2026-10-19 | One tool that builds every Froglet game? | **No.** Unifying everything into one tool costs far more than Shombith is willing to put resources into. | Shombith | §5's recommendation is not taken up. |
| 2026-10-19 | (proposal) Where synced artifacts live | An artifact-storing webpage that keeps the synced artifacts anyone thinks are important. | Proposed by Shombith | Open: who builds it and where. |

When a decision is recorded:
- update CLAUDE.md's "Tools under evaluation" section, `Port/CLAUDE.md`, and the `/vessel-studio`,
  `/amoebius` and `/amoebius-artifact` skills;
- update `.claude/hooks/vessel_studio_nudge.py`. (`team_context.py`, which read this section to word its notices,
  was switched off on 2026-10-23; CLAUDE.md carries the decision.)

## 7. How we decide

- **Collect feedback until 2026-11-05.** At the end of a task that used or touched one of these tools, a
  session asks the person one short question, and appends the answer to
  [`TOOLING_FEEDBACK.md`](TOOLING_FEEDBACK.md). It asks only when that tool has no entry from the last three
  days, so nobody is asked every time.
- **Then the core four decide** with §2's measurements, refreshed (`who_works_where.py map`; the artifact's
  database via `ArtifactData list`), and the feedback log. They record the result in §6.
