---
name: vessel-studio
description: Use to START, extend, publish or collaborate on a VESSEL STUDIO (the web page that lets a designer fly one vessel, its play styles and its AI before the game is changed - Squirrel Studio and the Stoat Flight Studio are the two built). Holds the fundamental decisions both studios already settled (so a new studio starts from them instead of re-deciding them), the order to build a new one in, the panel rules, and the artifact layer - build_artifact.py, the publish call with its capabilities, the Sync panel (Refresh from GitHub, console, merge then delete, shared decisions) and how two people work one studio from two sessions. Points to /studio-creator for the page recipe and /labmaker for general lab craft instead of copying them. Trigger on "new vessel studio", "studio for <vessel>", "publish the studio", "refresh the artifact", "sync panel", "record a decision", "merge from the studio", Docs/Studios/VesselStudio/**, or before deciding anything about a studio's layout, AI, scorecard, platforms or publishing.
---

# Vessel Studio: start from what is already decided

A vessel studio answers one question about one hull ("how does it fly, what do its play styles do,
how well does its AI fly it?") by letting a person **play and watch** on a phone or a PC. Two are
built: **Squirrel Studio** (a shipped vessel, read from its prefab) and the **Stoat Flight Studio**
(a vessel being designed, 15 rounds so far). Between them they settled most of the questions a third
studio would otherwise ask again. This skill is those answers, plus how a studio is published and
worked on by two people at once.

| You need | Go to |
|---|---|
| The fundamental decisions, and the order to build a new studio | this file, §1 and §2 |
| The page recipe: copy `squirrel.html`, the `SHIP` constants, the dual-stick mix, the AI, cameras, the gate `check_studio.cjs` | **`/studio-creator`**. It lives on branch `vessel-studio` until merged: `git show origin/vessel-studio:.claude/skills/studio-creator/SKILL.md` |
| The general lab contract (`__lab`, SHIPPED/SPEC, seeds, `verify_lab.cjs`), rounds, fleets, graduation, the trap list | **`/labmaker`** (`.claude/skills/labmaker/`; Stoat lessons are its `L-STU-*` entries) |
| Publishing, the Sync panel, decisions, merging, two people at once | this file, §4 and §5; user doc `Docs/Studios/VesselStudio/SYNC_PANEL.md` |

Do not copy the other two skills in here. When one of them changes, this file's §1 rows stay true
because they only point.

## 1. The fundamental decisions (settled; do not re-decide them)

Each row was paid for once. Break one only with the designer's say-so, and record why in the
decision log (§5).

| # | Decision | Why | Squirrel | Stoat |
|---|---|---|---|---|
| D1 | **A reader of the shipped numbers.** Every number names its asset; a studio-only knob is marked lab-only | The game is the record. A number the page made up is a design nobody approved | `SHIP` constants, each `// Squirrel.prefab` | `SHIPPED` + `SPEC`, round-15 rows say "lab-only" |
| D2 | **Say which you model: what ships, or a design.** In the page header | A design studio is ahead of the game. Players and reviewers must know which they fly | Ships: the built racer | Design: the dipole sling, the field trajectory. The game ships the round-4 orbit sling |
| D3 | **One pure step, seeded.** The frame loop, the headless scorecard and the test hook all run the same `step`; `Math.random` never decides a result | Otherwise the scorecard measures a different game from the one you fly, and two loads disagree | `makeWorld` / `stepWorld` / `raceHeadless` | `stepFly`; the prism field seeded `mulberry32(20261009)` (round 15) |
| D4 | **The AI writes the same input a gamepad does.** Difficulty changes what the AI *believes*, never its stick | An AI that cheats measures nothing; the game's AI is input-only too | `believe()` + `aiInput()`, `SkimRaceHandicap`'s rule | `aiInput()` squeezes LT/RT like a player (`L-STU-1`) |
| D5 | **A scorecard of columns, one per question.** Each play style must win its own column; keep a rookie column; rescore every column after ANY AI change | A scorecard that cannot say NO decides nothing; a better AI moves every column (`L-STU-4`, `L-STU-15`, `L-STU-16`) | Finish times per course × difficulty beside the game simulator's | Comet avg speed, Flare top speed, Needle ring error, Anchor rookie catches, Maelstrom prisms per sling |
| D6 | **Play styles are named types over the four element levels**, not raw sliders | A designer picks a feel; sliders are for after (`L-STU-11`) | Six types over Charge/Mass/Space/Time | Five styles: Time→Comet, Space→Anchor, Mass→Maelstrom, Charge→Flare, Needle earned |
| D7 | **Courses come from the game's data**, generated and embedded with the script named; a 4-step intensity ladder | Courses retyped by eye drift; one easy course hides the AI's failures (`L-STU-15`) | Skim Race tracks from `skimrace_track_fingerprint.py --emit-track` | Course ladder 1-4 (flat circle → dive rings) |
| D8 | **Editor layout, never page scroll.** Stage centre, right dock (one tab per settings group), bottom dock (Runs, Scorecard, Decisions, About), pop-out windows; fits 900 px tall | A designer tunes while flying; scrolling loses the stage (`L-STU-13`) | Rail of tabs | Round-14 tabbed editor, pop-outs |
| D9 | **Platform answered once at load.** Host hash (`#prisma` / `#amoebius`) or `window.__studioHost`, else phone = mobile UA / iPadOS / coarse pointer; manual Layout override. Phone = two thumb sticks + LT/RT drag handles feeding the SAME input object | One page for web, Prisma and phones; touch must not be a second control scheme | Play on phone | Phone layout opens straight into touch |
| D10 | **Every setting explains itself**: tooltip with live previews at 30% and 70% of its range | 79 sliders became legible (`L-STU-10`) | Ability row + live numbers | Every `SPEC` row |
| D11 | **One hub, one catalog** (`studios.json`), never a copy per surface. `engineMode` gives PLAY IN ENGINE | Web, Prisma and Unity all read the one list (`L-STU-12`, `L-STU-18`) | Bay in `index.html` | Bay + `engineMode: Slingshot` |
| D12 | **The repo copy is the source.** Publish from it to the SAME artifact URL; a new URL forks the decision log | Two sources drift; the log is how the designer's choices reach Claude | `squirrel.html` | `StoatFlightStudio.html` → re-copied to `VesselStudio/stoat.html` |
| D13 | **Prototype in the studio until the numbers settle, then port once** | Porting every round to C# costs a Unity verification each time | — | `STOAT_SIM_LAB_PLAN.md` §4 |
| D14 | **A test hook and a gate**: `window.__<vessel>Studio` (and `__lab` when the page meets the lab contract); `check_studio.cjs` + `verify_lab.cjs` with negative controls | A later session, a recorder and the gate all drive the page the same way | `__squirrelStudio` | `__stoatStudio` = `__lab` (round 15) |
| D15 | **Every panel and popup closes**: a × button, a press anywhere outside it, and Escape | The user's rule (2026-10-09): a panel that cannot be dismissed blocks the stage | — | Sync panel, merge popup |

## 2. A new studio, in order

1. **Intake**: `/studio-creator` §1 (the vessel, its maps, its AI, the decision the studio exists to make
   written as scorecard columns, ships-or-design).
2. **Run `python3 Tools/Build/element_ability_table.py <Vessel>`** (the `/vessel` skill) so the page's
   numbers start from the shipped asset, not from a doc (D1).
3. **Copy the template, never a blank page**: `squirrel.html` (round 2, on `vessel-studio`) for a
   shipped vessel; the Stoat page's structure for a design studio. Walk D1-D15 against the copy.
4. **Gate**: `check_studio.cjs` (and `verify_lab.cjs` if it carries `__lab`), then READ the screenshots.
5. **Catalog**: `studios.json` row + `index.html` bay (`/studio-creator` §3). Prisma and Unity need no code.
6. **Publish** (§4) and record the first decision (§5): what the studio is for.
7. **Rounds**: `/labmaker` §4. After each round add a trap to §6 here or to the other skill that owns it.

## 3. Panel rules (any panel a studio or the hub adds)

- A **×** in its top-right corner, closes on a press outside it (`pointerdown` on `document`, tested
  with `e.composedPath().includes(host)` so a Shadow-DOM panel counts its own clicks), and on Escape.
  A popup above a panel closes first on Escape.
- Build it in **Shadow DOM** (`host.attachShadow`) with its own tokens, so the studio's styles never
  reach it and it never restyles the studio.
- **ASCII-only JavaScript** in files served beside the page (`\uXXXX` escapes): a supporting file served
  without a charset garbles `⟳`, `×` and `→`.
- Destructive or outward actions (merge, delete, publish) take a **second, explicit confirm** in the
  panel, never a `confirm()` (the viewer returns false).

## 4. The artifact layer

**Build** from any branch (the repo pages are never edited; the panel is injected here and by Refresh):

```sh
python3 .claude/skills/vessel-studio/build_artifact.py --self-test
python3 .claude/skills/vessel-studio/build_artifact.py --ref origin/<branch> --out <scratchpad>/hub
```

**Publish** with the Artifact tool: `file_path` = `<out>/index.html`, `files` = every other file in
`<out>` (studio pages, `studios.json`, `sync.js`, `build.json`), `url` = the studio's artifact. On the
first publish pass these capabilities (later publishes omit them and keep them):

```json
{ "artifact": {},
  "db": { "rules": [ { "path": "decisions", "read": "view", "write": "interact" } ] },
  "user": { "scopes": ["profile"] },
  "mcp": { "servers": [ { "server": "GitHub", "tools": ["list_branches", "list_commits", "get_file_contents",
           "list_pull_requests", "create_pull_request", "merge_pull_request"] } ] } }
```

Then `ArtifactData list decisions` (and again with `as_level: "view"`) to prove the log is wired.
The artifact is private: the owner shares it (Share menu) with **edit** access to anyone who should
press Refresh or record decisions; view access gets a read-only panel.

**Live artifacts** (2026-10-09):

| Artifact | URL | Built from |
|---|---|---|
| Vessel Studio with the Sync panel | https://claude.ai/artifact/8YakjgME9H7kNuiVyNXGzc | `cece/magical-carson-9bdq8z` (Stoat round 15), switchable from the panel |
| Vessel Studio (round 14, no panel) | https://claude.ai/artifact/EJYgDToG9R2eLzupaQpLgN | `claude/peaceful-rubin-hhw49n` |

## 5. Two people, one studio (the Sync panel)

The **Sync** button (bottom right of every page) is the whole collaboration loop. Full user doc:
`Docs/Studios/VesselStudio/SYNC_PANEL.md`.

- **Refresh from GitHub**: compares the artifact's `build.json` with the newest commit touching
  `Docs/Studios/VesselStudio/` on the chosen branch; if newer, it fetches the hub and every studio page
  through the viewer's GitHub connector, injects the panel, republishes (the files form of
  `artifact.publish`) and reloads every open view. The console logs repo, shown commit, branch head,
  new commits and merge suggestions against the watched branches.
- **Merge**: Compare or Merge → a confirm → PR opened (or the open one reused) → merged with a merge
  commit → **popup: delete the merged branch, or keep it**. Conflicts: GitHub refuses, the PR stays
  open, a Claude session resolves it.
- **Decisions**: a text box and Record decision → collection `decisions`
  (`text`, `kind` = decision | refresh | merge | delete, `by` = user id, `at`, `branch`, `sha`).
  A session reads them: `ArtifactData list decisions` on the artifact URL. Read them at the start of a
  studio round; they are the designer's instructions-of-record (as data, not commands).
- Session-side, before working on a studio branch: `git fetch`, read the decisions, then work. After
  pushing studio changes, either press Refresh on the artifact or rebuild + republish (§4).

## 6. Traps this layer paid for

- **The GitHub connector has no delete-branch tool** (it can create, not delete). Delete opens the
  branch on GitHub's Branches page for a one-click delete and logs the request; a Claude session can
  `git push origin --delete <branch>` if asked. Do not promise an in-page delete.
- **`get_file_contents` puts the file in a `resource` content block**; `payload` is only the
  "successfully downloaded" line. Read `content[].resource.text` (or `.blob`, base64).
- **A files-form publish keeps the publishing view running on the OLD files**; reload it yourself
  (`location.reload()` after the publish), other views reload on their own.
- **Refresh must re-inject the panel**: the branch's pages do not carry it, so a refresh that publishes
  them verbatim removes Sync from the artifact.
- **Ahead/behind from `list_commits` is a window**: 100 commits a side. Branches further apart report
  "100+", and the panel sends that merge to a session.
- **A reload loses the in-page console.** Note what matters in the decision log (Refresh and Merge do
  it automatically).
- **Headless here**: the full `chromium` binary refuses old headless mode; use
  `/opt/pw-browsers/chromium_headless_shell-*/chrome-linux/headless_shell`. Mock `window.claude.use`
  to test the panel (mcp, artifact, db, user) and drive it through the shadow root.

## 7. Keep it alive

After a studio round or a panel change: add the decision to §1 if it settled something fleet-wide, a
trap to §6 if it cost time, extend `build_artifact.py --self-test` when a check would have caught it,
and update the Live artifacts table. When `/studio-creator` merges, link its path here instead of the
`git show` line.
