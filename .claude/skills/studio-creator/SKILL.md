---
name: studio-creator
description: Use to BUILD or extend a VESSEL STUDIO - the place a designer tests one vessel's flight, abilities, play-style types and AI, first as a web page (Docs/Studios/VesselStudio/*.html, phone and PC, published as a claude.ai artifact and opened by Amoebius's STUDIOS page), then in the real game (PLAY IN ENGINE, and the planned Vessel Test Range in Unity and Amoebius). Holds the recipe (the Squirrel round-2 AI sim lab is the template), the catalog wiring (studios.json, the hub, Amoebius, Unity's FrogletTools > Vessels > Vessel Studio), the page gate (check_studio.cjs, with its own negative controls) and the traps this work paid for. Trigger on "studio for <vessel>", "vessel studio", "studio creator", "/studio-creator", "test the <vessel> AI visually", "AI sim lab", "make a studio like the Stoat's", or any change under Docs/Studios/VesselStudio/. Sibling of /labmaker (general labs); the two are being compared and merged later.
---

# Studio Creator: one vessel, from a web page to the real game

A **vessel studio** answers "how does this vessel fly, fight and race, and how well does its AI
fly it?" for one hull. It answers by letting a person play and watch, on a phone or a PC, and then
by checking the same thing in the real game. It is the vessel-shaped case of a lab. `/labmaker`
holds the general lab craft (decision logs, research fleets, graduation), and this skill holds the
**vessel recipe**: what to copy, which numbers to read, how to wire it, how to prove it works.

**This is our version.** A second studio/lab skill is being built in parallel (`/labmaker`, plus
the CEO's own). The plan is to compare them and merge the best parts. So keep this file honest about
what it covers and what it defers to `/labmaker`. Do not copy that skill in here.

## 0. The three tiers (read `Docs/Studios/VESSEL_TEST_RANGE_PLAN.md` §1)

| Tier | What flies | Where | When to use it |
|---|---|---|---|
| **A. Web studio** | A JavaScript copy built from the shipped numbers | `Docs/Studios/VesselStudio/<vessel>.html`, any browser, Amoebius ▸ STUDIOS ▸ OPEN IN AMOEBIUS | Design: minutes per idea, from a phone |
| **B. PLAY IN ENGINE** | The game's own vessel in its own mode | Amoebius ▸ STUDIOS (`engineMode` → `--arcade MODE`) | Is what got built what we decided? |
| **C. Vessel Test Range** | The same game, AI on any seat, free camera, intensity maps, fidelity switch | Unity + Amoebius (planned: R1–R6 in the plan) | Diagnosing AI, flight and look on real content |

A new vessel starts at A. It reaches B the moment its mode exists (add `engineMode`). C is shared
infrastructure, so do not build a per-vessel copy of it.

## 1. Intake: answer these before the first line

1. **The vessel and its class** (`VesselClassType`), its prefab, its ability map
   (`Assets/Resources/ElementalAbilityMaps/`), its action SOs, and its `R_VesselActions/*.md`.
2. **The decision the studio exists to make**, written as columns: a scorecard has one number per
   question. The Squirrel's are finish times per course × AI difficulty, set beside the game
   simulator's.
3. **Its maps**: the arcade cards whose `Vessels` list holds its `SO_Class_<Vessel>`
   (`grep -l <class guid> Assets/_SO_Assets/Games/*.asset`). The Squirrel has eight (SkimRace,
   Regatta, AstroLeague, Broadside, BroodRush, Joust, Maelstrom, Scurry); the Stoat has Slingshot.
   If a map's geometry matters, read it from the scene with a script. The Squirrel's four Skim Race
   tracks come from `Tools/Build/skimrace_track_fingerprint.py --emit-track`. Never retype them by eye.
4. **Its AI**: platform `AIPilot` or a replacement pilot (`Docs/AI_SYSTEM/ARCHITECTURE.md` on the
   `ai-system` branch). Does it have difficulty levels, and where do they live (Skim Race:
   `SkimRaceDifficultySO` = Hard + `SkimRaceHandicap`'s reaction and misjudge)?
5. **What the game ships vs what is a design.** The Stoat's web sling is a design the game does not
   ship yet. Say which one the page models, in its header.

## 2. The web page: copy `squirrel.html` (round 2), never start blank

`Docs/Studios/VesselStudio/squirrel.html` is the template. Its parts, in order:

| Part | What to change for a new vessel |
|---|---|
| Header + `hostTag` | The name, the round chip. Keep the `#amoebius` / `#prisma` hash and `window.__studioHost` detection |
| `SHIP` constants | **Every number names its asset** (`// Squirrel.prefab`, `// SquirrelTubeAction`). A studio-only knob is marked as such (`LOOK`: "the studio pilot's own tuning, not a shipped number") |
| `DIFF`, `SIM_REF` | The game's difficulty asset numbers and the game simulator's reference times, both cited |
| `TRACKS` / map data | Generated, embedded verbatim, with the script named in the header comment |
| Pure world: `makeWorld`, `stepPilot`, `stepWorld`, `raceHeadless` | **Never draws, never reads a device.** The frame loop, the scorecard and the test hook all run it. Seeded `rng` everywhere |
| `believe()` + `aiInput()` | The AI writes the **same input object a gamepad does** (sticks + triggers through the game's dual-stick mix). Difficulty changes what the AI *believes* (late notice, misjudged target), never its stick (`SkimRaceHandicap`'s rule) |
| Rendering | One `InstancedMesh` for every prism, hull meshes, "Show AI thinking" lines (believed vs real target; amber = not noticed, red = misjudged) |
| Cameras | Chase (smooth the OFFSET, not the position, §7), Follow any pilot, Free (drag to look, IJKL/UO; phone sticks drive it when your hull is AI-flown) |
| Input layer | Gamepad, keyboard, **Play on phone** (two floating thumb sticks, LT/RT drag handles, fullscreen + landscape lock), all into one input object |
| Rail | Race (course, your hull You/AI Easy/Medium/Hard, rivals, rival level, camera, speed 1/2/4×, auto-restart), Scorecard, Types, Elements, Ability row, Live numbers, Runs, **Sources + not modelled** |
| Test hook | `window.__<vessel>Studio = { state, set, startRace, raceHeadless, raceStats, … }`: how a later session, the gate and a recorder drive the page |

The game's dual-stick mix (from `InputController`), for any flying vessel:
`XSum = ease(R.x+L.x)` (yaw), `YSum = -ease(R.y+L.y)` (pitch), `YDiff = ease(R.y-L.y)` (roll),
`XDiff = (R.x-L.x+2)/4` (throttle), with `ease(x) = x<0 ? cos(xπ/4)-1 : -(cos(xπ/4)-1)`. The nose is −Z.
An AI that wants yaw `y`, pitch `p`, roll `r` and as much throttle as steering allows inverts it:
`sx = easeInv(y)`, `dx = min(2, 2-|sx|)`, `R = ((sx+dx)/2, (sy+dy)/2)`, `L = ((sx-dx)/2, (sy-dy)/2)`.

**Measure the AI against the game, every round.** Put `raceStats` (average speed, boost,
off-line distance, resets, skims, time per AI state) in the hook. Tune only the pilot's own `LOOK`
knobs, and publish the table in the studio's README. When the studio's pilot differs from the
game's (smaller difficulty gaps, a faster or slower course), **say so on the page**. The Squirrel
README §scorecard is the model.

## 3. Wire it into the catalog (one list feeds everything)

1. `Docs/Studios/VesselStudio/studios.json`: `id`, `name`, `file`, `kind`, `summary`, `docs`, and
   `engineMode` (a `GameModes` name with an arcade card) + `engineNote` once the game has the mode.
   Planned: `ranges` presets for the Test Range (plan §4).
2. `index.html`: the vessel's bay (canvas preview, chip, spec row) and its `SPEC` string for the
   studio agent's Ask box. Take it off the "no studio yet" row.
3. **Amoebius** reads `studios.json` (`StudioCatalog.cs`). No code change for a new studio. Run
   `dotnet test Port/tests/CosmicShore.Launcher.Tests` (its tests load the real catalog).
4. **Unity**: `FrogletTools ▸ Vessels ▸ Vessel Studio` opens Amoebius on STUDIOS. Nothing to add.
5. `Docs/Studios/VesselStudio/README.md` (the page's row, its scorecard table) and
   `Docs/Studios/VESSEL_STUDIO_PLAN.md` (phase status).

## 4. Prove it: the gate, then the studio's own checks

```sh
export NODE_PATH=<a folder with node_modules/playwright-core>   # pre-installed browsers; never `playwright install`
node .claude/skills/studio-creator/check_studio.cjs --self-test --three <local three.min.js>
node .claude/skills/studio-creator/check_studio.cjs Docs/Studios/VesselStudio/<vessel>.html \
     --hook __<vessel>Studio --three <local three.min.js> --out <dir>
```

The gate fails, by name, on:
- a console or page error (desktop, 400 px, emulated phone);
- sideways scroll at 400 px;
- a missing hook, or a race that does not advance `state.t`;
- a blank WebGL stage (read from a real screenshot);
- a page that is not in touch play mode on a sideways phone, either by itself or after a REAL tap on Play on
  phone. A start card lying over the button is a failure.

`--self-test` plants each defect and must name every one. **Read the screenshots**: the gate cannot
see an overlap or an ugly camera.

Then the studio's own checks, listed in the round's write-up:
- the AI finishes every course at every difficulty (`raceHeadless`);
- the scorecard button computes and its times sit beside the game simulator's;
- a live race with 3 AI at 4× runs;
- a keyboard pilot moves.

`/labmaker`'s `verify_lab.cjs` checks the deeper `__lab` contract (SHIPPED/SPEC, deterministic
batch). A studio that adopts that contract runs it too.

## 5. Publish and commit

- The **repo copy is the source**. The Vessel Studio artifact is published from `index.html` with
  `files` = every studio page. Re-publish to the same URL (`studios.json` → `web`).
  `stoat.html` is a copy of `../StoatFlightStudio.html`: re-copy it, never edit the copy.
- Supporting HTML pages carry their own doctype, viewport meta and `[hidden]{display:none!important}`.
- Commit per round with what was checked. A docs-only round says "no C# changed". Any C# change
  still needs `/verify-unity`, and if no editor is available the commit says so.
- Logs, menus, SOs and AI rules from the root `CLAUDE.md` apply unchanged to anything that lands in
  `Assets/`.

## 6. The game tiers (B and C)

- **B** needs only `engineMode` in the catalog. Amoebius boots the game, opens the card and presses
  Start (`ArcadeAutoStart`). Pick intensity and AI difficulty on the card.
- **C**: follow `Docs/Studios/VESSEL_TEST_RANGE_PLAN.md`.
  - One dev-only harness for every vessel, installed into the real mode scenes. No copied scenes and
    no new environment.
  - It reuses `SpectatorController`, the manual replay rig, `MouseOrbitCamera`, `Vessel.ToggleAIPilot`,
    `DeviceTier` simulation and `CellMiniatureBuilder`.
  - Its board shows the same columns as the web studio, so A and C read alike.
  - It compiles only in the Editor and development builds: read `Docs/CONDITIONAL_COMPILATION.md` and
    run `check_conditional_compilation.py` first.

## 7. Traps this work paid for

- **An AI that chases the crystal leaves the line and loses every skim.** The first Squirrel pilot
  aimed straight at crystals 320 u away: average boost 1.1–2.1, 2–3× the game's times, one DNF.
  Stay on the line and lean out only as far as capture reach needs (`reachMargin`). Result: I1
  69 s vs the game's 64 s.
- **A difficulty mistake must cost what it costs in the game.** Easy only bit once a misjudged
  crystal was flown to where it was BELIEVED, not to the lean-out point. Even then the studio's gaps are
  smaller than the game's (quick turn-back, short look-ahead). Say so on the page; do not fudge it.
- **Chase cameras lag at 5× boost** if they lerp the camera POSITION: the hull shrinks to a dot
  100 u ahead. Lerp the offset from the hull, then add the hull's position.
- **Your own skimmer sphere hides your hull** in chase view: hide the chased pilot's sphere.
- **The start card covered Play on phone on a sideways phone** (844 × 390) until `.stagebtns`
  got `z-index`. Only a real tap at phone size finds this. A DOM `.click()` does not.
- **A 400 px check under phone emulation passes everything**: the emulated phone zooms out to fit.
  Measure overflow in a plain 400 px window (the gate's own negative control caught this).
- **WebGL canvases read back blank** through `drawImage` without `preserveDrawingBuffer`. Judge the
  stage from a screenshot.
- **Headless Chromium here**: use the headless shell under `/opt/pw-browsers`, serve three.js from a
  local copy (the CDN times out), stub Google Fonts, `--use-gl=swiftshader`.
- **A page that detects a phone may skip the button entirely** (the Stoat goes straight to touch
  play). Accept `body.play` as success; do not demand the button.
- **The Write tool refuses a file it has not read in this session**, even to replace it whole.
  Read it first (and check `git log` that nobody else changed it).

## 8. Keep it alive

After every studio round:
1. Add a trap to §7 when it cost real time.
2. Extend `check_studio.cjs` when a check would have caught it, with a planted defect in
   `--self-test`.
3. Update the studio's README row.

When the merge with `/labmaker` and the CEO's skill happens, this file's §2 (recipe),
§3 (catalog wiring), §6 (game tiers) and §7 are the parts to carry over.
