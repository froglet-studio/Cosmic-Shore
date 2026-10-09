---
name: vessel-studio
description: The ONE skill for the Vessel Studio - the single claude.ai artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) where every vessel and its AI are flown, tested and decided on, on phone or PC, before the game is changed. Use for ANYTHING about a vessel studio or vessel AI testing in a page - starting a studio for a new vessel, extending the Squirrel AI sim lab or the Stoat Flight Studio, the page recipe (SHIP constants, the dual-stick mix, input-only AI with difficulty levels, cameras, phone play, scorecard), the gate (check_studio.cjs), the catalog (studios.json, the hub, Amoebius STUDIOS, Unity FrogletTools > Vessels > Vessel Studio), building and publishing the artifact (build_artifact.py), the Sync panel and its jobs (sync_job.py), shared decisions and requests, and testing in the real game (PLAY IN ENGINE, the Vessel Test Range plan). Trigger on "vessel studio", "studio for <vessel>", "studio creator", "test the <vessel> AI visually", "AI sim lab", "publish / refresh the studio", "sync panel", "record a decision", "Vessel Studio Sync job", Docs/Studios/VesselStudio/**, Docs/Studios/StoatFlightStudio.html, or before deciding anything about a studio's layout, AI, scorecard, platforms or publishing. Never publish a second studio artifact.
---

# Vessel Studio: one artifact, one skill, every vessel

A vessel studio answers one question about one hull ("how does it fly, what do its play styles do,
how well does its AI fly it?") by letting a person **play and watch** on a phone or a PC. Two are
built: **Squirrel Studio** (a shipped vessel with its Skim Race AI at Easy/Medium/Hard) and the
**Stoat Flight Studio** (a vessel being designed, 15 rounds so far). This skill holds what they
settled, how a new one is built, how the one artifact is published and shared, and how a studio
reaches the real game. `/labmaker` holds the general lab craft (any lab, not only vessels): defer to
it for the `__lab` contract and `verify_lab.cjs`, never copy it here.

## 0. The one artifact (LOCKED, the user's rule 2026-10-09)

**There is exactly one Vessel Studio artifact: https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa.**
Every studio page, the hub, the studio agent (Ask + Development requests), the shared decision log
and the Sync panel live in it. From any session, anything about vessels, their AI or their studios
goes to THIS artifact:

- **Never publish a studio as its own artifact**, and never publish a new hub. A new studio is a page
  in `Docs/Studios/VesselStudio/` + a row in `studios.json`, published into this artifact (§4).
- From a conversation that has not published it yet: `Artifact read` it first, then publish with
  `url` = this URL. A publish refused as "newer version live" hands you that version: merge onto it.
- Its data: `requests` (studio agent), `decisions` (both the Stoat log and the Sync panel), `jobs`
  (Sync panel). Read `requests` and `decisions` at the start of every studio round
  (`ArtifactData list`).
- **Retired** (do not publish to them, do not link them): `EJYgDToG9R2eLzupaQpLgN` (old hub),
  `8YakjgME9H7kNuiVyNXGzc` (hub + Sync panel), `Busc3KW6DmVzbsiA2qxoHc` and `HZ6MYm4MNR2iEMnMhCWJxn`
  (Stoat rounds ≤14), `8Wvnsx3gxJXXMNUCoyyuEt` (Stoat round 15, another account). Their stores held
  no decisions or requests when this one replaced them (checked 2026-10-09).
- The artifact is private: the owner shares it (Share menu) with **edit** access to anyone who
  should press Refresh / Merge or record decisions.

## 1. The fundamental decisions (settled; do not re-decide them)

Each row was paid for once. Break one only with the designer's say-so, and record why (§5).

| # | Decision | Why | Squirrel | Stoat |
|---|---|---|---|---|
| D1 | **A reader of the shipped numbers.** Every number names its asset; a studio-only knob is marked lab-only | The game is the record. A number the page made up is a design nobody approved | `SHIP` constants, each `// Squirrel.prefab`; `LOOK` marked "the studio pilot's own tuning" | `SHIPPED` + `SPEC`, round-15 rows say "lab-only" |
| D2 | **Say which you model: what ships, or a design.** In the page header | A design studio is ahead of the game | Ships: the built racer | Design: the dipole sling, the field trajectory. The game ships the round-4 orbit sling |
| D3 | **One pure step, seeded.** Frame loop, headless scorecard and test hook run the same step; `Math.random` never decides a result | Otherwise the scorecard measures a different game | `makeWorld` / `stepWorld` / `raceHeadless` | `stepFly`; prism field seeded `mulberry32(20261009)` |
| D4 | **The AI writes the same input a gamepad does.** Difficulty changes what the AI *believes*, never its stick | An AI that cheats measures nothing; the game's AI is input-only too | `believe()` + `aiInput()`, `SkimRaceHandicap`'s rule | `aiInput()` squeezes LT/RT like a player (`L-STU-1`) |
| D5 | **A scorecard of columns, one per question**, beside the game's own numbers; keep a rookie/low-difficulty column; rescore every column after ANY AI change | A scorecard that cannot say NO decides nothing (`L-STU-4`, `L-STU-15`, `L-STU-16`) | Finish times per course × Easy/Medium/Hard beside the game simulator's | Comet avg speed, Flare top speed, Needle ring error, Anchor rookie catches, Maelstrom prisms per sling |
| D6 | **Play styles are named types over the four element levels**, not raw sliders | A designer picks a feel (`L-STU-11`) | Six types over Charge/Mass/Space/Time | Five styles: Time→Comet, Space→Anchor, Mass→Maelstrom, Charge→Flare, Needle earned |
| D7 | **Courses come from the game's data**, generated and embedded with the script named; a 4-step intensity ladder | Courses retyped by eye drift; one easy course hides the AI's failures | Skim Race I1–I4 from `skimrace_track_fingerprint.py --emit-track` | Course ladder 1–4 |
| D8 | **Editor layout, never page scroll — a Vessel Studio design constant** (the user, 2026-10-09): the game view fills the middle; every panel is a tab in a dock around it (right: settings; bottom: records); any tab pops out (⧉) into a floating window you drag, resize and dock back (⇲); splitters resize the docks; all of it is remembered per browser. Below 900 px the docks stack under the stage and the page scrolls. Use the shared `Docs/Studios/VesselStudio/studio-ide.js` (`VesselStudioIDE.build({key, host, stage, right, bottom})`, listed under `shared` in `studios.json`) | A designer tunes while flying; scrolling loses the stage (`L-STU-13`) | Moved onto `studio-ide.js` 2026-10-09 | The original (round 14), still its own copy until its next round |
| D9 | **Platform answered once at load.** Host hash (`#prisma` / `#amoebius`) or `window.__studioHost`, else phone = mobile UA / iPadOS / coarse pointer; manual override. Phone = two thumb sticks + LT/RT drag handles into the SAME input object | One page for web, Amoebius and phones | Play on phone | Phone layout opens straight into touch |
| D10 | **Every setting explains itself**: tooltip with live previews | 79 sliders became legible (`L-STU-10`) | Ability row + live numbers | Every `SPEC` row |
| D11 | **One hub, one catalog** (`studios.json`), never a copy per surface. `engineMode` gives PLAY IN ENGINE | Web, Amoebius and Unity all read the one list (`L-STU-12`, `L-STU-18`) | Bay + `engineMode: SkimRace` | Bay + `engineMode: Slingshot` |
| D12 | **The repo copy is the source; ONE artifact** (§0) | Two sources drift; a second artifact forks the decision log | `squirrel.html` | `StoatFlightStudio.html` → re-copied to `VesselStudio/stoat.html` |
| D13 | **Prototype in the studio until the numbers settle, then port once** | Porting every round costs a Unity verification each time | — | `STOAT_SIM_LAB_PLAN.md` §4 |
| D14 | **A test hook and a gate**: `window.__<vessel>Studio` (and `__lab` when the page meets the lab contract); `check_studio.cjs` (+ `verify_lab.cjs`) with negative controls | A later session, a recorder and the gate drive the page the same way | `__squirrelStudio` | `__stoatStudio` = `__lab` |
| D15 | **Every panel and popup closes**: ×, a press outside it, and Escape | A panel that cannot be dismissed blocks the stage (the user, 2026-10-09) | — | Sync panel, merge popup |
| D16 | **Three cameras in every studio: Chase · Follow · Free** (the user, 2026-10-09). **Chase**: close behind the watched hull, rolls with it. **Follow**: wider and world-up; in a race it can pick any pilot (yours, then each AI). **Free**: detached from the hull; it either flies (drag to look, I J K L / U O, Shift fast, phone sticks) or orbits the watched hull (drag to turn, wheel to zoom). C, or the pad, cycles them | One camera vocabulary across studios, so watching an AI reads the same everywhere. The chase offset is smoothed, not the position (§7) | Chase / Follow (any pilot) / Free fly | Chase / Follow (wide) / Free orbit. **Open:** offer both Free modes (fly and orbit) in both studios |
| D17 | **The RACE panel is the standard AI-test config** for every vessel AI (the user, 2026-10-09): Course (the 4-intensity ladder, D7) · Your hull (You fly / AI Easy / Medium / Hard) · AI rivals 0–3 · Rival level (Easy / Medium / Hard) · Camera (D16) · Speed 1× / 2× / 4× · Show AI thinking · Restart each race on its own. The **Scorecard** sits beside it (D5) | Any AI is tested the same way: the same knobs, the same order, comparable runs | `squirrel.html` Race panel (the reference) | AI pilot + sim lab controls; move to this panel shape at its next round |
| D18 | **Two transport buttons, the same in every studio** (the user, 2026-10-09): on the stage (`#transport`) and in the phone bar, **▶ Play ↔ ❚❚ Pause** as ONE toggle in one place (`#tpPlay` / `#tStart`: Play starts or resumes, Pause pauses; `aria-pressed` while paused) and **■ Stop** (`#tpStop` / `#tBack`). Keys: Enter starts, P pauses and resumes, R stops. Paused, your hull and the race clock halt while the stage and cameras stay live; Stop ends the run and goes back to the start. Stop is disabled, not hidden, when there is nothing to stop | A tester controls every studio the same way, like the Unity editor | Was "Pause", then a 3-button bar | Was "■ Stop" (freeze) + Start/Back |
| D19 | **The Stoat look is every studio's default graphics** (the user, 2026-10-09): load `Docs/Studios/VesselStudio/studio-look.js` after three.js and call `VesselStudioLook.install(scene, {field, prismScale})` for the Stoat's nebula sky with its grid, the coloured starfield, its lights and the seeded drifting prism field (`placeField` rings it just outside the course's farthest point). Collectables use `VesselStudioLook.crystal(color)` (faceted core, glow, ring, beacon) and the screen marker `VesselStudioLook.marker(viewport)` ("CRYSTAL 2/24 · 368 u", pinned to the edge when off screen), like the Stoat's ring marker. List it under `shared` in `studios.json` so `build_artifact.py` publishes it | One place, one look; a crystal must be findable from across the course | Restyled 2026-10-09 (whole Skim Race, all 4 courses) | The source of the look (its sky and field) |

## 2. A new studio, in order

1. **Intake** — answer before the first line:
   - the vessel, its class (`VesselClassType`), prefab, ability map (`Assets/Resources/ElementalAbilityMaps/`),
     action SOs and `R_VesselActions/*.md`;
   - the decision the studio exists to make, written as scorecard columns (D5);
   - its maps: the arcade cards whose `Vessels` list holds `SO_Class_<Vessel>`
     (`grep -l <class guid> Assets/_SO_Assets/Games/*.asset`). Squirrel: SkimRace, Regatta, AstroLeague,
     Broadside, BroodRush, Joust, Maelstrom, Scurry. Stoat: Slingshot, Warpline;
   - its AI: platform `AIPilot` or a replacement pilot (`Docs/AI_SYSTEM/ARCHITECTURE.md`, branch
     `ai-system`) and where its difficulty lives (Skim Race: `SkimRaceDifficultySO` = Hard +
     `SkimRaceHandicap`'s late notice and misjudged crystal);
   - ships or design (D2).
2. **`python3 Tools/Build/element_ability_table.py <Vessel>`** (the `/vessel` skill), so numbers start
   from the shipped asset (D1).
3. **Copy a template, never a blank page** (§3): `squirrel.html` for a shipped vessel with AI; the
   Stoat page's structure for a design studio. Walk D1–D15 against the copy.
4. **Gate** (§3.3), then READ the screenshots.
5. **Catalog** (§3.4). Amoebius and Unity need no code.
6. **Publish into the one artifact** (§4) and record the first decision (§5): what the studio is for.
7. **Rounds**: `/labmaker` §4 (one round = one commit + one write-up). Add traps to §7.

## 3. The page recipe

### 3.1 `squirrel.html`, part by part

| Part | What to change for a new vessel |
|---|---|
| Header + `hostTag` | Name, round chip. Keep the `#amoebius` / `#prisma` hash and `window.__studioHost` detection |
| `SHIP` constants | Every number names its asset (`// Squirrel.prefab`, `// SquirrelTubeAction`). Studio-only knobs live apart (`LOOK`) and say so |
| `DIFF`, `SIM_REF` | The game's difficulty-asset numbers and the game simulator's reference times, both cited |
| `TRACKS` / map data | Generated, embedded verbatim, the script named in the header comment |
| Pure world: `makeWorld`, `stepPilot`, `stepWorld`, `raceHeadless`, `raceStats` | Never draws, never reads a device; seeded `rng` everywhere (D3) |
| `believe()` + `aiInput()` | AI through sticks + triggers (D4); difficulty edits only belief |
| Rendering | One `InstancedMesh` for every prism, hull meshes, "Show AI thinking" lines (believed vs real target; amber = not noticed, red = misjudged) |
| Cameras | Chase (smooth the OFFSET, not the position), Follow any pilot, Free (drag to look, IJKL/UO; phone sticks drive it when your hull is AI-flown) |
| Input layer | Gamepad, keyboard, Play on phone; all into one input object (D9) |
| Rail | Race (course, your hull You/AI Easy/Medium/Hard, rivals, rival level, camera, speed 1/2/4×, auto-restart), Scorecard, Types, Elements, Ability row, Live numbers, Runs, Sources + not modelled |
| Test hook | `window.__<vessel>Studio = { state, set, startRace, raceHeadless, raceStats, … }` |

### 3.2 The game's dual-stick mix (any flying vessel)

From `InputController`: `XSum = ease(R.x+L.x)` (yaw), `YSum = -ease(R.y+L.y)` (pitch),
`YDiff = ease(R.y-L.y)` (roll), `XDiff = (R.x-L.x+2)/4` (throttle),
`ease(x) = x<0 ? cos(xπ/4)-1 : -(cos(xπ/4)-1)`. The nose is −Z. An AI that wants yaw `y`, pitch `p`, roll `r`
and as much throttle as steering allows inverts it: `sx = easeInv(y)`, `dx = min(2, 2-|sx|)`,
`R = ((sx+dx)/2, (sy+dy)/2)`, `L = ((sx-dx)/2, (sy-dy)/2)`.

**Measure the AI against the game every round** (`raceStats`: average speed, boost, off-line distance,
resets, skims, time per AI state). Tune only the studio pilot's own knobs and publish the table in
`VesselStudio/README.md`. Where the studio's pilot differs from the game's (the Squirrel's difficulty
gaps are smaller: quick turn-back, short look-ahead), **say so on the page**; never fudge it.

### 3.3 The gate

```sh
export NODE_PATH=<a folder with node_modules/playwright-core>   # pre-installed browsers; never `playwright install`
node .claude/skills/vessel-studio/check_studio.cjs --self-test --three <local three.min.js>
node .claude/skills/vessel-studio/check_studio.cjs Docs/Studios/VesselStudio/<vessel>.html \
     --hook __<vessel>Studio --three <local three.min.js> --out <dir>
```

It fails, by name, on:
- a console or page error (desktop, 400 px, emulated phone);
- sideways scroll in a plain 400 px window;
- a missing hook, or a race that does not advance `state.t`;
- a blank WebGL stage (read from a real screenshot);
- a page that is not in touch play on a sideways phone, either by itself or after a REAL tap on Play on
  phone. A card lying over the button fails.

`--self-test` plants each defect and must name every one. Then run the studio's own checks:
- every course at every difficulty finishes (`raceHeadless`);
- the scorecard computes;
- a live 3-AI race at 4× runs;
- a keyboard pilot moves.

A page on the `__lab` contract also runs `/labmaker`'s `verify_lab.cjs`.

### 3.4 Catalog (one list feeds everything)

1. `Docs/Studios/VesselStudio/studios.json`: `id`, `name`, `file`, `kind`, `summary`, `docs`, plus
   `engineMode` (a `GameModes` name with an arcade card) and `engineNote` once the game has the mode.
   `web` is the one artifact (§0).
2. `index.html`: the vessel's bay and its `SPEC` string for the Ask box; take it off "no studio yet".
3. **Amoebius** reads `studios.json` (`StudioCatalog.cs`): run `dotnet test Port/tests/CosmicShore.Launcher.Tests`.
4. **Unity**: `FrogletTools ▸ Vessels ▸ Vessel Studio` opens Amoebius on STUDIOS. Nothing to add.
5. `VesselStudio/README.md` (the page's row, its scorecard) and `VESSEL_STUDIO_PLAN.md` (phase status).

## 4. Publishing the one artifact

**Build** (the repo pages are never edited; the Sync panel is injected here and by Refresh):

```sh
python3 .claude/skills/vessel-studio/build_artifact.py --self-test
python3 .claude/skills/vessel-studio/build_artifact.py --ref origin/claude/peaceful-rubin-hhw49n --out <scratchpad>/vs \
        --session <your session id>          # --artifact defaults to the one artifact
```

**Publish** with the Artifact tool:
- `file_path` = `<out>/index.html`;
- `files` = every other file in `<out>` (studio pages, `studios.json`, `sync.js`, `build.json`);
- `url` = https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa.

Omit `capabilities` to keep them. If they must ever be restated, the full set is:

```json
{ "artifact": {}, "sample": {}, "user": { "scopes": ["profile"] },
  "db": { "rules": [ { "path": "decisions", "read": "view", "write": "interact" },
                     { "path": "jobs",      "read": "view", "write": "interact" },
                     { "path": "requests",  "read": "view", "write": "interact" } ] },
  "mcp": { "servers": [ { "server": "Claude Code Remote",
                          "tools": ["send_message", "create_session", "list_environments"] } ] } }
```

Then `ArtifactData list` of `decisions`, `jobs` and `requests`. Re-copy `StoatFlightStudio.html` into
`VesselStudio/stoat.html` (keeping the back link) before a build whenever the Stoat changed.

**Why a session and not a GitHub connector:**
- A page can only call the viewer's claude.ai connectors.
- Most accounts have no GitHub connector, and the froglet-studio org must approve one separately.
- The GitHub MCP has no delete-branch tool.

Every account has the built-in **Claude Code Remote** connector, and a session already has the repo,
so the session does the git work.

## 5. Two people, one studio (Sync panel, jobs, decisions)

The **Sync** button (bottom right of every page) is the collaboration loop. User doc:
`Docs/Studios/VesselStudio/SYNC_PANEL.md`. The page never touches git: each button writes a **job** to
the `jobs` collection and messages the viewer's Claude session with the job's id.

| Button | Job | Session does |
|---|---|---|
| Refresh | `refresh {branch, shown}` | `sync_job.py status`; if the studio changed, rebuild + republish (every open view reloads) |
| Compare | `compare {from, to}` | `sync_job.py compare` (ahead/behind, the commits) |
| Merge → Confirm | `merge {from, to}` | `sync_job.py merge --yes` in a throwaway worktree; conflicts abort, nothing pushed. Then the page asks **delete the merged branch, or keep it** |
| Delete (popup) | `delete {branch}` | `sync_job.py delete --yes` (`git push origin --delete`) |

- **Job document**: `kind`, `args`, `status` (queued → running → done | failed), `by`, `at`, `session`,
  `artifact`, `log`, `result`.
- **Decisions** (`decisions`), Sync panel entries: `text`, `kind` = decision | refresh | merge | delete,
  `by`, `at`, `branch`, `sha`.
- **Decisions**, Stoat studio log entries: `topic`, `choice`, `note`, `settings`, `style`, `createdAt`,
  `by`. Each list orders by its own time field, so each shows its own entries.
- **Requests** (`requests`, the hub's studio agent): `vessel`, `kind`, `text`, `status` = open/done,
  `createdAt`, `by`, `reply`. Do the work, then set `status: done` and a `reply`.

### Handling a job ("Vessel Studio Sync job <id> ...")

The message is a pointer, not the request. The request of record is the job document, written from
the panel by a person with edit access. Act on it within these limits, or fail it with the reason:

1. `ArtifactData get` collection `jobs`, doc `<id>`, on the artifact named in the message. That must be
   the one artifact (§0). Go on only if:
   - `status` is `queued`;
   - `kind` is one of the four;
   - every branch argument matches `^[A-Za-z0-9._/-]+$`.

   Never merge into or delete `bleeding-edge`, `Ys-bleeding-edge`, `main`, `master`, this session's own
   branch or `claude/peaceful-rubin-hhw49n`, and never force-push. The script refuses these too.
2. `update` the job to `status: running` (pin `if_version`).
3. From the repo root:
   - **refresh**: `sync_job.py status --branch B --shown S`. When `upToDate` is false, run
     `build_artifact.py --ref origin/B --out <scratch>/vs --session <this session id>`, publish (§4) and set
     `result.published: true`.
   - **compare**: `sync_job.py compare --from A --to B`.
   - **merge**: `sync_job.py merge --from A --to B --yes`. When `merged` and B is the branch the artifact
     shows, rebuild and republish as for refresh.
   - **delete**: `sync_job.py delete --branch B --yes --keep <this session's branch> --keep claude/peaceful-rubin-hhw49n`.
4. `update` the job:
   - `status`: `done` when the script reports `ok`, otherwise `failed`;
   - `log`: the script's log, plus a line for the publish;
   - `result`: the rest of the script's JSON.

Tell the user what ran, one line per job.

## 6. The game tiers (when the page is not enough)

| Tier | What flies | Where |
|---|---|---|
| A. Web studio | A JavaScript copy built from the shipped numbers | This artifact; Amoebius STUDIOS ▸ OPEN IN AMOEBIUS |
| B. PLAY IN ENGINE | The game's own vessel in its own mode | Amoebius STUDIOS (`engineMode` → `--arcade MODE`, `ArcadeAutoStart`) |
| C. Vessel Test Range | The real mode scenes with AI on any seat, free-fly camera, intensity maps, time scale, Full / Mobile-low / Block look | Unity + Amoebius; **plan only**: `Docs/Studios/VESSEL_TEST_RANGE_PLAN.md` |

C is shared infrastructure: one dev-only harness for every vessel, installed into the real mode
scenes. Do not make a per-vessel copy or a new environment scene. It reuses `SpectatorController`,
the manual replay rig, `MouseOrbitCamera`, `Vessel.ToggleAIPilot`, `DeviceTier` simulation and
`CellMiniatureBuilder`. Before writing it, read `Docs/CONDITIONAL_COMPILATION.md` and run
`check_conditional_compilation.py`.

## 7. Traps these studios paid for

**AI and flight**
- **An AI that chases the crystal leaves the line and loses every skim.** Aiming straight at crystals
  320 u away gave boost 1.1–2.1× and 2–3× the game's times. Stay on the line and lean out only as far
  as capture reach needs.
- **A difficulty mistake must cost what it costs in the game.** Easy only bit once a misjudged crystal
  was flown to where it was BELIEVED.
- **Chase cameras lag at 5× boost** if they lerp the POSITION. Lerp the offset from the hull.
- **Your own skimmer sphere hides your hull** in chase view: hide the chased pilot's sphere.
- **The latest trigger sample is not the squeeze** (`L-STU-1`), and **gamepad Y is inverted**
  (`L-STU-8`).

**Pages and checks**
- **The start card covered Play on phone on a sideways phone.** Only a real tap at phone size finds it.
- **A 400 px check under phone emulation passes everything**: the emulated phone zooms out to fit.
- **WebGL canvases read back blank** without `preserveDrawingBuffer`: judge the stage from a screenshot.
- **A page that detects a phone may skip the button** (the Stoat goes straight to touch play).
  Accept `body.play`.
- **Headless here**: use `/opt/pw-browsers/chromium_headless_shell-*/chrome-linux/headless_shell`,
  serve three.js from a local copy, stub Google Fonts, `--use-gl=swiftshader`.
- **ASCII-only JavaScript in served files** (`\uXXXX`): a file served without a charset garbles `×` and `→`.
- **Panels**: build them in Shadow DOM with their own tokens. Destructive actions take a second
  in-panel confirm, never `confirm()` (the viewer returns false).

**The artifact**
- **A page can only use the VIEWER's connectors, and only message that viewer's own sessions.**
  - Each viewer sets their own session (`data/users/<id>/sync`).
  - The publisher's session in `build.json` is a default for the publisher alone.
  - A job sent to a session that has ended fails "could not be reached": press Start session.
- **A files-form publish keeps the publishing view on the OLD files**: reload it. Refresh must
  re-inject the panel.
- **A new artifact forks the data**: this is why §0 exists. Before anything else, check the artifact
  you are about to publish to is the one.
- **The Write tool refuses a file it has not read in this session**: read first, and check `git log`.

## 8. Keep it alive

After a studio round or a panel change:
- add a settled decision to §1 and a costly trap to §7;
- extend `check_studio.cjs` or `build_artifact.py --self-test` when a check would have caught the
  problem, with a planted defect;
- update the studio's README row.

**This is the only vessel-studio skill**: fold any new studio guidance in here, never into a second skill.
