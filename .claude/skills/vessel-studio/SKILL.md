---
name: vessel-studio
description: The ONE skill for the Vessel Studio - the single claude.ai artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) where every vessel and its AI are flown, tested and decided on, on phone or PC, before the game is changed. Use for ANYTHING about a vessel studio or vessel AI testing in a page - starting a studio for a new vessel, extending the Squirrel AI sim lab or the Stoat Flight Studio, the page recipe (SHIP constants, the dual-stick mix, input-only AI with difficulty levels, cameras, phone play, scorecard), the gate (check_studio.cjs), the catalog (studios.json, the hub, Amoebius VESSEL STUDIO, Unity FrogletTools > Vessels > Vessel Studio), building and publishing the artifact (build_artifact.py), the Sync panel and its jobs (sync_job.py), shared decisions and requests, and testing in the real game (PLAY IN ENGINE, the Vessel Test Range plan). Trigger on "vessel studio", "studio for <vessel>", "studio creator", "test the <vessel> AI visually", "AI sim lab", "publish / refresh the studio", "sync panel", "record a decision", "Vessel Studio Sync job", Docs/Studios/VesselStudio/**, Docs/Studios/StoatFlightStudio.html, or before deciding anything about a studio's layout, AI, scorecard, platforms or publishing. Also load it at the START of any vessel task (feel, AI, difficulty, play styles, cameras, a new vessel) to recommend the studio, and for the universal panel rules (section 1.5: Scene / Game / AI / Play Style Config, Input, Vessel Config; dropdowns; players + / -; domains; difficulty; intensity ladder; cameras). Never publish a second studio artifact.
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
- **Recommend it for every vessel task** (the user, 2026-10-09). When anyone in the project starts
  vessel work (a hull's feel, its AI, its play styles, cameras, a difficulty, a new vessel), say in one
  line that the team decides vessel and AI questions visually in the Vessel Studio first, and name the
  studio page that fits, or offer to start one (§2). CLAUDE.md and `/vessel` point here for the same
  reason, and the project hook `.claude/hooks/vessel_studio_nudge.py` (registered in `.claude/settings.json`,
  `--self-test`) shows the person a one-line notice the first time a session's prompt is about vessel work.
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
| D9 | **Platform answered once at load (D26).** Host hash (`#prisma` / `#amoebius`) or `window.__studioHost`, else phone = mobile UA / iPadOS / touch-only screen; no button asks. Phone = two thumb sticks + LT/RT drag handles into the SAME input object | One page for web, Amoebius and phones | Play on phone | Phone layout opens straight into touch |
| D10 | **Every setting explains itself**: tooltip with live previews | 79 sliders became legible (`L-STU-10`) | Ability row + live numbers | Every `SPEC` row |
| D11 | **One hub, one catalog** (`studios.json`), never a copy per surface. `engineMode` gives PLAY IN ENGINE | Web, Amoebius and Unity all read the one list (`L-STU-12`, `L-STU-18`) | Bay + `engineMode: SkimRace` | Bay + `engineMode: Slingshot` |
| D12 | **The repo copy is the source; ONE artifact** (§0) | Two sources drift; a second artifact forks the decision log | `squirrel.html` | `StoatFlightStudio.html` → re-copied to `VesselStudio/stoat.html` |
| D13 | **Prototype in the studio until the numbers settle, then port once** | Porting every round costs a Unity verification each time | — | `STOAT_SIM_LAB_PLAN.md` §4 |
| D14 | **A test hook and a gate**: `window.__<vessel>Studio` (and `__lab` when the page meets the lab contract); `check_studio.cjs` (+ `verify_lab.cjs`) with negative controls | A later session, a recorder and the gate drive the page the same way | `__squirrelStudio` | `__stoatStudio` = `__lab` |
| D15 | **Every panel and popup closes**: ×, a press outside it, and Escape | A panel that cannot be dismissed blocks the stage (the user, 2026-10-09) | — | Sync panel, merge popup |
| D16 | **Three cameras in every studio: Chase · Follow · Free** (the user, 2026-10-09). **Chase**: close behind the watched hull, rolls with it. **Follow**: wider and world-up; in a race it can pick any pilot (yours, then each AI). **Free**: detached from the hull; it either flies (drag to look, I J K L / U O, Shift fast, phone sticks) or orbits the watched hull (drag to turn, wheel to zoom). C, or the pad, cycles them | One camera vocabulary across studios, so watching an AI reads the same everywhere. The chase offset is smoothed, not the position (§7) | Chase / Follow (any pilot) / Free fly | Chase / Follow (wide) / Free orbit. **Open:** offer both Free modes (fly and orbit) in both studios |
| D17 | **One universal AI race config panel** in every studio that tests an AI, and every arcade-game studio after them (the user, 2026-10-09): `VesselStudio/ai_race_panel.js` (`StudioRacePanel.mount`, §3.4). Course (the 4-intensity ladder, D7) · Your hull (You fly / AI Easy / Medium / Hard) · one row per AI rival, each with its own level (D20) · Camera (D16) · Speed 1× / 2× / 4× · Show AI thinking · Restart each race on its own. The **Scorecard** sits beside it (D5). Never hand-build these controls in a page | Any AI is tested the same way: the same knobs, the same order, comparable runs, one implementation | Mounted in the Race panel; drives the race directly | Mounted in the AI Config tab; drives the existing controls (kept hidden). Rivals on (D20, D23); levels are the Skim Race levels plus a lab-only pair judgement (D24) |
| D18 | **Two transport buttons, the same in every studio** (the user, 2026-10-09): on the stage (`#transport`) and in the phone bar, **▶ Play ↔ ❚❚ Pause** as ONE toggle in one place (`#tpPlay` / `#tStart`: Play starts or resumes, Pause pauses; `aria-pressed` while paused) and **■ Stop** (`#tpStop` / `#tBack`). Keys: Enter starts, P pauses and resumes, R stops. Paused, your hull and the race clock halt while the stage and cameras stay live; Stop ends the run and goes back to the start. Stop is disabled, not hidden, when there is nothing to stop | A tester controls every studio the same way, like the Unity editor | Was "Pause", then a 3-button bar | Was "■ Stop" (freeze) + Start/Back |
| D19 | **The Stoat look is every studio's default graphics** (the user, 2026-10-09): load `Docs/Studios/VesselStudio/studio-look.js` after three.js and call `VesselStudioLook.install(scene, {field, prismScale})` for the Stoat's nebula sky with its grid, the coloured starfield, its lights and the seeded drifting prism field (`placeField` rings it just outside the course's farthest point). Collectables use `VesselStudioLook.crystal(color)` (faceted core, glow, ring, beacon) and the screen marker `VesselStudioLook.marker(viewport)` ("CRYSTAL 2/24 · 368 u", pinned to the edge when off screen), like the Stoat's ring marker. List it under `shared` in `studios.json` so `build_artifact.py` publishes it | One place, one look; a crystal must be findable from across the course | Restyled 2026-10-09 (whole Skim Race, all 4 courses) | The source of the look (its sky and field) |
| D20 | **Every AI seat is picked on its own, with its own level** (the user, 2026-10-09): the RACE panel lists your hull (You fly / AI Easy / Medium / Hard) and one row per AI rival in its domain colour (AI Ruby, AI Gold, AI Blue), each **Off / Easy / Medium / Hard**. A seat keeps its colour whichever seats are on. Stored as `S.ai = [level per seat]`; an older "N rivals at one level" save migrates | Tests mix levels (one Hard, one Easy) to see how the AI's mistakes play against each other | Built 2026-10-09 | Built 2026-10-09: Ruby, Gold, Blue rival Stoats, flown by the same step as your hull (the rival block swaps every per-pilot variable in and out), their pairs real pairs in the one world |
| D21 | **One set of settings tabs, the same names and order in every studio: Scene Config · Game Config (D25) · AI Config · Play Style Config · Input · Vessel Config** (Others until D27) (the user, 2026-10-09: studio interfaces are modular, alike and reusable, and these are rules for every new vessel studio). The right dock (D8) carries exactly these six; the bottom dock keeps the records (Scorecard, Runs, Decisions, About). **Scene Config**: what the world is: course and intensity, camera (D16), simulation speed, and the course/flight settings. Mount the race panel with `sceneHost` so its Course, Camera and Speed rows render here (§3.4), and never hand-build a second course picker. **AI Config**: the race panel's seats (your hull, each rival's level, D20), Show AI thinking, auto-restart, and the AI's own tuning and scorecard buttons. **Play Style Config**: the named play-style types (D6), the element levels and the vessel's main mechanic, plus its archive of retired variants. **Input**: the controls reference (gamepad, keys, free cam, transport, phone) and any feel settings for how input maps (drift depth, squeeze curve). **Vessel Config**: everything vessel-specific that fits none of the four (ability row, live numbers, lope, archive). Every choice inside a tab is a dropdown, toggle or slider, never a row of mode buttons (artifact comment, 2026-10-09). A new tab name or another tab needs the user's say-so (Game Config was added that way, D25) | One vocabulary: a tester who knows one studio finds every setting in the next; a new studio copies the layout instead of inventing it | Scene Config (course, camera, speed) · AI Config (seats, the AI race note) · Play Style Config (type, element levels) · Input (controls, drift feel) · Vessel Config (ability row, live numbers). Key `squirrel-studio-ide-v2` | Scene Config (course, camera, speed, Flight & course, Field trajectory, Black hole, White hole, Pair life, Birth & annihilation: SIBLING sections, never nested under Field trajectory, the user 2026-10-09) · AI Config (Sim lab) · Play Style Config (styles, squeeze, the archived dipole, orbit and crystal slings) · Input (controls) · Vessel Config (lope, archive). A tab's sections are siblings, one level deep, each folding on its own (D22). Key `stoat-studio-ide-v2` |
| D22 | **Every section in a settings tab folds with − / +** (the user, 2026-10-09): `VesselStudioIDE.collapsible(el, studioKey)` in `studio-ide.js`. A `<details>` folds on its summary (− open, + closed). Any other section gets a −/+ button on its first heading (h2–h4 or `.vsc-head`). Open by default; each section's state is remembered per studio and element id. `VesselStudioIDE.build` applies it to every tab element. A studio with its own dock builder calls it for each element it puts in a pane. Give each section an id so its state persists | Long tabs (the Stoat's Play Style Config holds seven sections) need to shrink to the one being tuned | Every Race / Scorecard / Types / Elements panel, through `build` | Its own `buildDocks` calls `collapsible` for each pane element; loads `studio-ide.js` for it |
| D23 | **Each AI rival seat also picks a play style** (the user, 2026-10-09: the AI flies the vessel's play styles). The panel takes `styles: [names]` and draws a style dropdown beside each seat's level; the state key is `rivalStyles`. A style is the vessel's own style changes (D6) plus how that style's AI flies (its brain: squeeze, when to lay, when to let go, lab-only and named in the page) | A rival Comet and a rival Needle must race differently, or racing them answers nothing | Not yet (its types apply to rivals through "Rivals fly this type too") | `STYLE_BRAIN` over the AI sliders, then the style's sling and hole changes (`pilotP`) |
| D24 | **AI levels are the Skim Race levels, applied to belief** (the user, 2026-10-09: Easy/Medium/Hard like the Squirrel's): Hard is the skilled pilot; Medium and Easy are Hard plus `SkimRaceDifficulty.asset`'s two mistakes in what the AI BELIEVES: it notices each new target late (0.25 / 0.5 s mean, x 0.5-1.5) and misjudges 4.5% / 9.9% of targets (believes them off to one side, flies there, misses, turns back). A vessel-specific third mistake (the Stoat's pair judgement) is lab-only and says so. **Hard must be skilled, not just mistake-free**: a pilot whose outcome is chaotic lets mistakes come out faster, and the levels decide nothing. Prove it with the level scorecard (Hard fastest on every course) | A level that is not slower measures nothing (`L-STU-4`) | `DIFF`, `believe()` | `AI_LEVELS`, `aiBelief()`, the path-watching field AI, **Score AI levels** |
| D25 | **Game Config: a Players list, a settings tab beside Scene Config** (the user, 2026-10-09). The right dock is Scene Config · **Game Config** · AI Config · Play Style Config · Input · Vessel Config. Game Config holds **Players N** with **+ / −**. **The race starts with player 1 alone; you add each player with +** (the user, 2026-10-09: no fixed four). The cap is the studio's sim (the Stoat: you + 3 rival seats, `max: 4`). Each player opens with **Is AI** (player 1 is your hull: you fly it or the AI does; every other player is always an AI), **Domain**, **Difficulty** (Easy / Medium / Hard, D24), **Play style** (D23), and **View**: the camera follows that player, with the camera mode beside it. The panel builds it: `StudioRacePanel.mount(…, { players: { host, max, domains } })` replaces the Your hull and seat rows; its state is `players` (`[{ ai, domain, level, style }]`) and `view` | Who is racing is the game's setup, not the AI's tuning: one list says who is in the race and lets you watch any of them | Not yet (Race panel seats) | `applyPlayers`: player 1 drives `aiOn`, `domainKey`, `youLevel`, `youStyle`; players 2 to 4 are the rival seats (`seatLv`, `seatStyle`, `seatDomain`). A change to the rivals starts a new race |
| D26 | **The platform is detected, never asked** (the user, 2026-10-09: no Play-on-phone button; each device opens its own interface only). `VesselStudioIDE.platform()` (shared `studio-ide.js`) answers once at load from the host (`window.__studioHost`, `#amoebius`), the mobile UA / iPadOS, or a touch-only screen, and sets `body.dev-pc` / `body.dev-phone`. A phone opens straight into touch play (fullscreen on its first touch); a PC never sees a touch button (`.phone-only` hidden). On a phone, leaving touch play shows one **Back to flying** button. A layout override, if any, is a setting, not a stage button | A button that asks what the page can tell is clutter, and on a PC it is a control that does nothing useful | Moved onto `platform()` 2026-10-09: Play on phone removed | Its own `detectPlatform` (the original), the same rule; the Layout select is the override |
| D27 | **Vessel Config, one section at a time** (the user, 2026-10-09: rename Others to Vessel Config; dropdowns and clean UI, no clutter). The sixth settings tab is **Vessel Config** (tab key stays `others` so saved layouts survive). A **Section** dropdown at its top (`VesselStudioIDE.sectionPicker`) shows one of its sections; the choice is remembered per studio. AI Config stays its own tab | A tab of unrelated vessel sections reads as clutter; one picked section reads as a settings page | Ability row · Live numbers | Lope · Archive |
| D28 | **Intensity is chosen in Game Config** (the user, 2026-10-10: a universal Vessel Studio rule). The race panel's course ladder (I1-I4, D7) renders as an **Intensity** dropdown in Game Config when the studio passes `gameHost` (its own sibling section, above Players); Scene Config keeps Camera and Speed. Every studio has a Game Config tab, even before it has a Players list | Which intensity you race is part of setting up the game, beside who is racing | `gameHost: $('intensityPanel')`, a Game Config tab holding the Intensity panel | `grp-intensity` above `grp-game` in Game Config |
| D29 | **One look: every studio wears the Stoat's typography and chrome** (the user, 2026-10-10: same fonts, spacing, line heights and sizes, so it reads as one vessel studio). `Docs/Studios/VesselStudio/studio-theme.js` (shared) carries the Stoat's values once: **Chakra Petch** for headings, labels, tabs and numbers; **Atkinson Hyperlegible** for reading text; body 15px / 1.45; title 1.5rem (1.05rem in the editor layout); section titles 0.9rem bold uppercase, 0.04em; cards 8px radius, 10px 12px padding; controls 0.9rem, 6px radius, 5px 7px; buttons 0.85rem bold; the transport bar bottom-left on the stage; the start card's night box; the Stoat's colour tokens with their light-theme twins; `--accent` = the Stoat amber in every studio (domain colours are for hulls and players, never chrome). A page loads it in `<head>` AFTER its own `<style>`, never loads its own Google Fonts, and keeps only its layout and vessel widgets. `build_artifact.py --check` fails a page that skips the theme or brings its own fonts | Two studios had two type systems (Saira + IBM Plex vs Chakra Petch + Atkinson) and different sizes everywhere; a shared file is the only way they stay alike | Moved onto the theme 2026-10-10: its own fonts and jade chrome gone; transport moved to the Stoat's place | The reference; loads the theme too, so a theme change reaches it |
| D30 | **Your hull never leaves the course unattended** (the user, 2026-10-10: a hull you can fly but are not flying went off to infinity; every vessel always heads for its next ring). With **Is AI** off and nobody on the controls for 3 s (no key, no stick past 0.2, no button or trigger, no touch), the AI flies your hull toward its next ring, racing and after the finish; the first input hands it straight back, and neither side's held pair is ever handed over (both triggers let go at the switch). The stage says **AI HOLDING YOUR HULL · touch any control to fly** | Watching a rival (View) is the normal way to use the studio, and an idle hull holding its last heading flew 6.6 km off course in 120 s | Not yet (the Squirrel's hull drifts too: port `pilotInput`) | `pilotInput` (`IDLE_TAKEOVER_S`, `humanOnControls`), used by the frame loop and the `tick`/`sim` hooks |

## 1.5 The universal studio kit: how every studio's panels are built

D1-D30 are the decisions. This section is the same rules arranged as a build manual: what a tester
sees, where it goes, which control it uses, and which shared code draws it. A new studio, or a new
panel in an old one, follows it. The aim (the user, 2026-10-09) is that **studios are modular,
alike and reusable**: a tester who has used one studio already knows where everything is in the
next.

### 1.5.1 The layout: two docks around the stage (D8)

```
+-------------------------------------------+--------------------------------+
|                                           | Scene Config | Game Config |   |
|            THE STAGE (three.js)           | AI Config | Play Style Config  |  <- right dock: SETTINGS
|  transport: > Play/|| Pause  [] Stop     | Input | Vessel Config          |
|  crystal marker, AI thinking lines        |  sections fold - / + (D22)     |
+-------------------------------------------+--------------------------------+
| Runs | Scorecard | Decisions | About                                       |  <- bottom dock: RECORDS
+----------------------------------------------------------------------------+
```

- **Built by the shared `studio-ide.js`**:
  `VesselStudioIDE.build({ key, host, stage, right: [[tabKey, title, [elements]]...], bottom: [...] })`.
  The Stoat has its own `buildDocks` (the original); it calls `VesselStudioIDE.collapsible` for each
  section, so it behaves the same.
- **Right dock = settings, in this order** (D21, D25):
  Scene Config · Game Config · AI Config · Play Style Config · Input · Vessel Config.
  Bottom dock = records: Runs · Scorecard · Decisions · About.
  Renaming a tab, or adding another, needs the user's say-so.
- **Any tab pops out** (⧉) into a floating window that you drag, resize and dock back (⇲). Splitters
  resize the docks. All of it is remembered under the layout key `<vessel>-studio-ide-vN`. Bump `N`
  when tab keys change, so a saved layout cannot point at a tab that no longer exists.
- **Below 900 px** the docks stack under the stage and the page scrolls. On a phone, touch play takes
  the whole screen (D9).

### 1.5.1b The look (D29)

Load `studio-theme.js` in `<head>` after the page `<style>` (a source one folder up: `VesselStudio/studio-theme.js`), and
write no font link, no font family and no chrome colour of your own: use `var(--font-display)` / `var(--font-body)`
(aliases `--display`, `--body`, `--mono`), `var(--fg)`, `var(--dim)` (alias `--muted`), `var(--panel)`, `var(--panel-2)`,
`var(--line)`, `var(--accent)`. Name things the way the theme styles them: `.card` / `.panel` with an `h3` or a
`details.card > summary`, `.btn` / `button.pick` (+ `.primary`), `.transport`, `.overlay > .card` (or `.box`) for the
start card, `.hud` for anything over the stage. Compare a new page with the Stoat's computed styles (body, title, tab,
section title, dropdown, row label, button, transport) before publishing; they must be identical.

### 1.5.2 How a panel is made

1. **A panel is a section with an id**: a `<details class="card" id="grp-...">` with a `<summary>`, or a
   section whose first child is an h2-h4. The id is what keeps its fold state and its tab placement.
2. **Put it in exactly one tab** by listing its id in that tab's element list. Never place the same
   control in two tabs; a second copy drifts from the first.
3. **Sections in a tab are siblings, one level deep** (the user, 2026-10-09). Never nest one section
   inside another; the Stoat's Black hole and White hole sit BESIDE Field trajectory, not inside it.
   Order them from the most-used to the least.
4. **Every section folds** with − / + (D22). It is open by default, and its state is remembered per
   studio and section id.
5. **Every setting explains itself** (D10): a tooltip, with a live preview where a number changes the
   feel. It names its source asset, or says "lab-only" (D1).
6. **A shared control is never hand-built.** If more than one studio needs it, it goes in a shared
   script under `Docs/Studios/VesselStudio/` (listed under `shared` in `studios.json`):
   - `ai_race_panel.js`: course, camera, speed, players, seats, levels, styles, thinking, auto-restart;
   - `studio-ide.js`: docks, tabs, pop-outs, folds;
   - `studio-look.js`: sky, prism field, crystals, marker;
  - `studio-theme.js`: the fonts, sizes, spacing and chrome colours (D29);
  - `studio-domains.js`: the game's domain colours.

   Then every studio gets the change.
7. **Every panel and popup closes** with ×, a press outside it, and Escape (D15).

### 1.5.3 The control rules (dropdowns, not button rows)

| A choice of... | Use | Never |
|---|---|---|
| One of several named options (course, camera, speed, level, domain, style) | a **dropdown** `<select class="arp-seg">`. A level colours its select (`data-lv`) | a row of mode buttons (artifact comment, 2026-10-09) |
| On / off | a **checkbox** with a sentence label ("Show AI thinking") | a two-button toggle |
| A number | a **slider** with its value, its unit and its source in the tooltip | a bare text box |
| How many (players) | **+ / −** buttons beside the count, disabled at the limits | a number dropdown |
| A one-shot action (Score all styles, Restart) | a **button** | a checkbox |
| Play / pause / stop | the **transport** (D18): one ▶/❚❚ toggle and ■ Stop, on the stage and the phone bar | per-panel start buttons |

- **Not supported yet? Show it disabled, with the reason once** (`supports: { rivals: 'why' }`).
  Never hide it: a hidden control reads as "this studio cannot", and a disabled one with a reason
  reads as "not yet".
- **One column wide.** Labels go on the left (86 px), controls on the right, and nothing scrolls
  sideways at 400 px (the gate checks this).
- **Served JavaScript is ASCII only.** Write `−`, never `−`, in a `.js` file (§7).

### 1.5.4 Scene Config: what the world is

| Row | Values | Rule |
|---|---|---|
| **Intensity** (in **Game Config**, D28) | the mode's **4-step intensity ladder**, `I1`-`I4` (D7) | Generated from the game's data, never retyped (Skim Race: `skimrace_track_fingerprint.py --emit-track`). Each course is `{ v, label, note }`, with the note saying what it is ("tilted spline, 3 laps"). A new arcade-mode studio passes ITS mode's ladder, from its four `CellConfigDataSO`s (`/arcadegame`) |
| **Camera** | Chase · Follow · Free (D16) | See 1.5.9 |
| **Speed** | 1× · 2× · 4× | Simulation speed. The physics step stays the same; more steps run per frame |
| then the world's own sections | sliders | Flight & course, the field, the holes, and so on: siblings, foldable |

Mount the race panel with `gameHost: <element in Game Config>` (the Intensity row, D28) and `sceneHost: <element in
Scene Config>` (Camera and Speed); the rest of the panel stays in Game Config or AI Config. It is one panel, one state.

### 1.5.5 Game Config: which intensity, and who is in the race (D28 Intensity first; then players, add / remove, domain, difficulty, view)

The Players list (D25), drawn by the panel when you pass `players: { host, max, domains }`:

- **Players N with − / +**, from 1 to `max` (4; the panel allows up to 8). **The race starts with player 1 alone**: the tester adds each player with + (never a fixed four).
  - **+** adds an AI player in the first **unused domain**, at **Hard**, with the next play style;
    its card opens.
  - **−** removes the LAST player. Player 1 cannot be removed.
- **Each player is a foldable card** whose summary reads "Player 2 · AI Medium · Ruby · Comet ·
  viewed". Inside:
  - **Is AI**: player 1 is your hull, and you choose who flies it (you, or the AI). Every other
    player is always an AI (one human per studio).
  - **Domain**: the game's four (`Domains.cs`: Jade 1 · Ruby 2 · Blue 3 · Gold 4), each in its colour.
    A player keeps its colour whichever players are in.
  - **Difficulty**: Easy · Medium · Hard (1.5.7). It is disabled while you fly the hull, and says why.
  - **Play style**: one of the vessel's named styles (D23). Shown only when the studio passes `styles`.
  - **View**: ◉ Viewing / ○ View plus a camera dropdown. The camera follows that player.
- **State**: `players = [{ ai, domain, level, style }]`, `view = index`, reported through
  `onChange('players' | 'view')`. The panel keeps the older keys (`you`, `rivals`, `rivalStyles`) in
  step, so a page that reads them keeps working.
- **A change to who is racing starts a new race.** A change to the view or camera does not.
- **Domain colours come from the GAME, never from a page** (the user, 2026-10-09: the artifact, the project and
  Amoebius show the same colours). `Docs/Studios/VesselStudio/studio-domains.js` is GENERATED by
  `.claude/skills/vessel-studio/domain_colors.py` from `OriginalColorSetSO.asset` (the live `SO_ColorSet`):

  | Domain | `color` (= `GetDomainSignalColor`, hulls, seats, lines) | `ui` (= `GetDomainUIColor`) | In the game |
  |---|---|---|---|
  | Jade (1) | `#13fff2` teal | `#0ec0b6` | a player domain |
  | Ruby (2) | `#ff00f9` magenta | `#c800c3` | a player domain |
  | Gold (4) | `#ffa700` amber | `#ffa700` | a player domain |
  | Blue (3) | `#6680ff` | `#6680ff` | the neutral "no team" domain (`Docs/PALETTE.md` 2.8); last in the list |

  A page loads it before its own script and reads `VesselStudioDomains.list` / `.get(key)`. It also sets the CSS
  variables `--jade --ruby --gold --blue`, and the race panel defaults to it. Never type a domain hex into a page.
  `domain_colors.py --check` fails when the file is stale, when Amoebius's engine palette
  (`Port/src/CosmicShore.Client/SkimRaceTheme.cs`) differs from the asset, or when Amoebius's launcher JADE / RUBY /
  GOLD themes (`Neon.cs`) no longer lead with the signal colour. After a palette change, run it without `--check`
  and republish.
- **Without Game Config** (the Squirrel today) the panel draws the older rows in AI Config instead:
  - **Your hull**: You fly / AI Easy / AI Medium / AI Hard;
  - **one row per AI rival seat** (Ruby, Gold, Blue), each Off / Easy / Medium / Hard (D20).

  Move a studio to `players` when you next touch its race panel.

### 1.5.6 AI Config: how the AI thinks

- **Show AI thinking** (checkbox): a line from each AI hull to the point it is steering for, drawn
  where the decision is made (inside `aiInput`). The colours are fixed:
  - green: flying for the target;
  - amber: has not noticed it yet;
  - red: misjudged it;
  - violet: working the vessel's mechanic (the Stoat's pair).
- **Restart each race on its own** (checkbox), for watching many races in a row.
- **The level note** (`levelNote`): one paragraph saying what Easy, Medium and Hard mean in THIS
  studio, and where the numbers come from (the game's asset, or "lab-only").
- **The AI's own tuning**: sliders, marked lab-only, and the scorecard buttons ("Score AI levels",
  "Score all styles"), whose results go to the bottom dock's Scorecard.

### 1.5.7 The AI difficulty levels (D4, D24)

| Level | What changes | Numbers (Skim Race, `SkimRaceDifficulty.asset`) |
|---|---|---|
| **Hard** | Nothing: the skilled pilot | no notice delay, no misjudgements |
| **Medium** | Hard plus mistakes in what it BELIEVES | notices each new target 0.25 s late (mean, × 0.5-1.5); misjudges 4.5% of targets |
| **Easy** | the same mistakes, more of them | 0.5 s late; 9.9% misjudged |
| **Off** | (a seat only) the seat is empty | - |

- **The AI is input-only.** A level changes only what the AI believes. It never changes its stick or
  the hull's state. A misjudged target is flown to where it was believed, so the miss costs what it
  costs in the game.
- **Hard must be skilled, not just mistake-free.** Prove it with the level scorecard: Hard is fastest
  on every course. Otherwise the levels decide nothing.
- **A vessel-specific mistake** (the Stoat's pair judgement) is lab-only, and the level note says so.

### 1.5.8 Play Style Config (D6, D23)

- **Play styles are named types over the four element levels** (Charge · Mass · Space · Time), never
  raw sliders. Examples: the Stoat's Comet (Time), Anchor (Space), Maelstrom (Mass), Flare (Charge)
  and Needle; the Squirrel's six types.
- **Under the named types**, the element level sliders, then the vessel's main mechanic.
- **An AI player flies a style too**: the style's own changes, plus how that style's AI flies it (its
  brain: `STYLE_BRAIN` in the Stoat, lab-only and named).
- **Retired variants go to an Archive section** (Vessel Config or Play Style Config), never deleted, so an
  old decision can be replayed. Example: the Stoat's dipole, orbit and crystal slings.

### 1.5.9 Cameras (D16)

| Camera | What it does |
|---|---|
| **Chase** | Close behind the viewed hull, rolling with it. Smooth the OFFSET, never the position (it lags at boost otherwise) |
| **Follow** | Wider and world-up. It follows the player picked with View (Game Config), or cycles pilots with C |
| **Free** | Detached from the hull. It flies (drag to look, I J K L / U O, Shift for fast, phone sticks) or orbits the viewed hull (drag, wheel to zoom) |

C, or the gamepad, cycles the cameras. Hide the viewed hull's own skimmer sphere in Chase.
**Open:** offer both Free modes (fly and orbit) in both studios.

### 1.5.10 Input, Vessel Config, and the rest of the frame

- **Input**: a controls card covering the gamepad (the game's dual-stick mix, §3.2), the keys, the free
  camera, the transport and the phone. It also holds the feel settings for how input maps (drift
  depth, squeeze curve). Phone play uses two thumb sticks plus LT/RT drag handles, all into the SAME
  input object (D9).
- **Vessel Config** (was Others, renamed D27): anything vessel-specific that fits none of the other tabs. A **Section** dropdown at its top shows ONE section at a time (`VesselStudioIDE.sectionPicker(pane, studioKey, 'vessel')`), so it never clutters: the ability row, live
  numbers, lope, the archive.
- **Look** (D19): `VesselStudioLook.install(scene, {...})`. It gives the Stoat sky, the prism field,
  `crystal(color)` and the edge `marker`.
- **Records**: Runs (every race, its seed and settings), Scorecard (D5: one column per question,
  beside the game's own numbers), Decisions (the shared log in the artifact's `decisions`), About
  (what is modelled and what is not).
- **State**:
  - each studio saves its settings in `localStorage`, under one versioned key
    (`cs.studio.squirrel.v2`, `stoat-studio-v9`);
  - `panel.set(key, value, true)` syncs a code-side change silently;
  - the test hook `window.__<vessel>Studio` drives it all headless (D14).

### 1.5.11 Checklist: a new studio's panels

- [ ] The six right-dock tabs in order. Every section has an id, sits in one tab, is a sibling, and
      folds.
- [ ] The page loads `studio-domains.js` and types no domain hex; the race panel is mounted with `courses` (the mode's I1-I4), `sceneHost`, `players` (with
      `VesselStudioDomains.list`), `styles`, `supports` (a reason for anything not built) and `levelNote`.
- [ ] Every choice is a dropdown, toggle, slider or + / −; no button rows.
- [ ] Easy / Medium / Hard come from the game's difficulty asset (or say lab-only); the level
      scorecard shows Hard fastest.
- [ ] The three cameras, View per player, the transport, phone play.
- [ ] `check_studio.cjs` passes (§3.3), the screenshots have been read, and the page is published into
      the ONE artifact (§4).

## 2. A new studio, in order

1. **Intake** — answer before the first line:
   - the vessel, its class (`VesselClassType`), prefab, ability map (`Assets/Resources/ElementalAbilityMaps/`),
     action SOs and `R_VesselActions/*.md`;
   - the decision the studio exists to make, written as scorecard columns (D5);
   - its maps: the arcade cards whose `Vessels` list holds `SO_Class_<Vessel>`
     (`grep -l <class guid> Assets/_SO_Assets/Games/*.asset`). Squirrel: SkimRace, Regatta, AstroLeague,
     Broadside, BroodRush, Joust, Maelstrom, Scurry. Stoat: Slingshot, Warpline;
   - its AI: platform `AIPilot` or a replacement pilot (`Docs/AI_SYSTEM/ARCHITECTURE.md`, branch
     `Ys-bleeding-edge`) and where its difficulty lives (Skim Race: `SkimRaceDifficultySO` = Hard +
     `SkimRaceHandicap`'s late notice and misjudged crystal);
   - ships or design (D2).
2. **`python3 Tools/Build/element_ability_table.py <Vessel>`** (the `/vessel` skill), so numbers start
   from the shipped asset (D1).
3. **Copy a template, never a blank page** (§3): `squirrel.html` for a shipped vessel with AI; the
   Stoat page's structure for a design studio. Walk D1–D25 and §1.5.11 against the copy. Put every setting under
   one of the six universal tabs (D21, D25: Scene Config, Game Config, AI Config, Play Style Config, Input,
   Vessel Config), and mount the race panel (D17) with `sceneHost` and `players`. Build the panels by §1.5.
4. **Gate** (§3.3), then READ the screenshots.
5. **Catalog** (§3.5). Amoebius and Unity need no code.
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

`--self-test` plants each defect and must name every one. Then
`python3 .claude/skills/vessel-studio/domain_colors.py --check` (the domain colours match the game and Amoebius). Then run the studio's own checks:
- every course at every difficulty finishes (`raceHeadless`);
- the scorecard computes;
- a live 3-AI race at 4× runs;
- a keyboard pilot moves.

A page on the `__lab` contract also runs `/labmaker`'s `verify_lab.cjs`.

### 3.4 The universal AI race config panel (`ai_race_panel.js`, D17)

Every studio that flies an AI mounts this panel. Never rebuild these controls by hand: a new key belongs
in the module, so every studio gets it.

```html
<script src="ai_race_panel.js"></script>   <!-- from a page in VesselStudio/; a source one folder up uses VesselStudio/ai_race_panel.js -->
<div id="racePanel"></div>                    <!-- inside the page's own race / sim-lab card -->
```
```js
const racePanel = StudioRacePanel.mount($('racePanel'), {
  courses: [{ v: 1, label: 'I1', note: 'what the course is' }, ...],   // the mode's 4-step intensity ladder
  seats: [{ name: 'Ruby', color: '#ff4f7b' }, ...],   // the AI rival seats in their domain colours (D20), up to 3
  value: { course, you, rivals: ['Hard', 'Hard', 'Off'], camera, speed, thinking, autoRestart },   // the page's saved state
  supports: { rivals: true | 'why not yet', thinking: true | 'why not', autoRestart: true },
  levelNote: 'What Easy / Medium / Hard mean in this studio, and where the numbers come from.',
  onChange: (key, value, state) => { /* apply it to the page's own sim; save */ },
});
racePanel.set('camera', 'Free', true);   // code-side changes (a C key, a hook) keep the panel in sync silently
```

- **Scene rows go to Scene Config (D21).** Pass `sceneHost: $('scenePanel')`, an element in the page's Scene
  card, and the panel renders Course, Camera and Speed there and the seats and checkboxes in `racePanel`. It
  is still one panel with one state and one `onChange`. Without `sceneHost` every row stays in the panel.
- **Every row is a dropdown** (`select.arp-seg`), with the level's colour on the select (`data-lv`).
- **Seat play styles (D23).** Pass `styles: ['Balanced', 'Comet', ...]` and `value.rivalStyles`; each seat row
  gets a style dropdown beside its level, and `onChange('rivalStyles', [...])` reports it.

- **Fixed vocabulary** (the module validates it): `course` = a `courses[].v`; `you` = `'You' | 'Easy' |
  'Medium' | 'Hard'`; `rivals` = one level per seat, `'Off' | 'Easy' | 'Medium' | 'Hard'` (D20); `camera` Chase/Follow/Free (D16);
  `speed` 1/2/4; `thinking` and `autoRestart` booleans.
- **Map, don't rename.** A page with its own state names maps them in `onChange`. The Squirrel maps
  `rivals` to `S.ai` (its per-seat array) and `camera` to `S.cam`.
- **A page with older controls** keeps them hidden and has `onChange` set their value and dispatch
  `change`. The Stoat does this, so its existing listeners stay the single way in.
- **Not supported yet? Say so, don't hide it.**
  - Pass a reason string in `supports`. The row stays visible, disabled, with the reason under it.
  - The day the studio gains rivals, flip it to `true` (the Stoat did on 2026-10-09).
- **Difficulty must say what it is.** `levelNote` names the source: the game's asset (the Squirrel's
  `SkimRaceDifficulty.asset`), or "lab-only" when the game ships no difficulty for that vessel (the
  Stoat: sloppiness 0 / 0.4 / 0.8). The AI stays input-only: a level changes what it believes or how
  sloppily it presses, never the hull's state (D4).
- **Show AI thinking.** Draw a line from each AI hull to the point it is steering for, coloured by its
  state:
  - Squirrel: amber = not noticed, red = misjudged;
  - Stoat: green = flying for the ring, amber = not noticed yet, red = misjudged, violet = working the pair, one
    line per AI pilot (your hull and each rival).

  Record the aim point inside `aiInput`, where the decision is made.
- **An arcade-game studio** (a new mode) passes its own intensity ladder as `courses`, and its own
  difficulty asset in `levelNote`. Everything else is the same panel.
- **Shipping**: `build_artifact.py` copies every `<script src="x.js">` a page loads into the build, and
  `--check` fails a page that loads a script the build lacks or one with non-ASCII bytes.

### 3.5 Catalog (one list feeds everything)

1. `Docs/Studios/VesselStudio/studios.json`: `id`, `name`, `file`, `kind`, `summary`, `docs`, plus
   `engineMode` (a `GameModes` name with an arcade card) and `engineNote` once the game has the mode.
   `web` is the one artifact (§0). `mirror` is its **live mirror**
   (https://yskhan61.github.io/vessel-studio/, repo `YsKhan61/vessel-studio`, GitHub Pages): the same
   `build_artifact.py --ref origin/Ys-bleeding-edge` output pushed as plain files, so the hub and every page open by
   link in any browser and update in place when it is republished. Amoebius's STUDIOS page opens it with
   **OPEN LIVE IN BROWSER** (the hub, and `<mirror>/<file>` per studio). It is a mirror, never a second source or a
   second artifact (D12): publish it from the build, never edit it. The publisher runs on the maintainer's machine
   (it fetches the branch, rebuilds with that ref's own `build_artifact.py`, commits only when the studio files
   changed). Sync, Ask, Requests and shared decisions need the claude.ai viewer, so on the mirror they say so.
2. `index.html`: the vessel's bay and its `SPEC` string for the Ask box; take it off "no studio yet".
3. **Amoebius** reads `studios.json` (`StudioCatalog.cs`) on its VESSEL STUDIO page: run `dotnet test Port/tests/CosmicShore.Launcher.Tests`.
   The page also lists Amoebius's artifact library (`Docs/Artifacts/artifacts.json`, entry `vessel-studio`): other
   artifacts come in with `/amoebius-artifact`, never as a copy of a studio.
4. **Unity**: `FrogletTools ▸ Vessels ▸ Vessel Studio` opens Amoebius on STUDIOS. Nothing to add.
5. `VesselStudio/README.md` (the page's row, its scorecard) and `VESSEL_STUDIO_PLAN.md` (phase status).

## 4. Publishing the one artifact

**Build** (the repo pages are never edited; the Sync panel is injected here and by Refresh):

```sh
python3 .claude/skills/vessel-studio/build_artifact.py --self-test
python3 .claude/skills/vessel-studio/build_artifact.py --ref origin/Ys-bleeding-edge --out <scratchpad>/vs \
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

Then `ArtifactData list` of `decisions`, `jobs` and `requests`. To record the published version in Amoebius's
library, run `/amoebius-artifact` on this URL: it must report every file `unchanged` (a `changed` file means the
repo and the artifact disagree). Whenever the Stoat changed, regenerate
its hub copy before a build:
- `python3 .claude/skills/vessel-studio/copy_stoat.py` writes it (back link, header, and
  `VesselStudio/x.js` → `x.js`);
- `--check` exits 1 when the copy is stale.

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
   branch, and never force-push. The script refuses these too.
2. `update` the job to `status: running` (pin `if_version`).
3. From the repo root:
   - **refresh**: `sync_job.py status --branch B --shown S`. When `upToDate` is false, run
     `build_artifact.py --ref origin/B --out <scratch>/vs --session <this session id>`, publish (§4) and set
     `result.published: true`.
   - **compare**: `sync_job.py compare --from A --to B`.
   - **merge**: `sync_job.py merge --from A --to B --yes`. When `merged` and B is the branch the artifact
     shows, rebuild and republish as for refresh.
   - **delete**: `sync_job.py delete --branch B --yes --keep <this session's branch>`.
4. `update` the job:
   - `status`: `done` when the script reports `ok`, otherwise `failed`;
   - `log`: the script's log, plus a line for the publish;
   - `result`: the rest of the script's JSON.

Tell the user what ran, one line per job.

## 6. The game tiers (when the page is not enough)

| Tier | What flies | Where |
|---|---|---|
| A. Web studio | A JavaScript copy built from the shipped numbers | This artifact; its live mirror (`mirror`, §3.5); Amoebius VESSEL STUDIO ▸ OPEN IN AMOEBIUS / OPEN LIVE IN BROWSER |
| B. PLAY IN ENGINE | The game's own vessel in its own mode | Amoebius VESSEL STUDIO (`engineMode` → `--arcade MODE`, `ArcadeAutoStart`) |
| A2. **Third Eye** (Unity) | The game itself, watched from a second camera while you play: the studio's Chase / Follow / Free cameras and its AI-thinking colours on the game's own AI | `FrogletTools > AI > Third Eye`; `/vessel-ai` §4 |
| C. Vessel Test Range | The real mode scenes with AI on any seat, free-fly camera, intensity maps, time scale, Full / Mobile-low / Block look | Unity + Amoebius; **plan only**: `Docs/Studios/VESSEL_TEST_RANGE_PLAN.md` |

C is shared infrastructure: one dev-only harness for every vessel, installed into the real mode
scenes. Do not make a per-vessel copy or a new environment scene. It reuses `SpectatorController`,
the manual replay rig, `MouseOrbitCamera`, `Vessel.ToggleAIPilot`, `DeviceTier` simulation and
`CellMiniatureBuilder`. Before writing it, read `Docs/CONDITIONAL_COMPILATION.md` and run
`check_conditional_compilation.py`.

## 7. Traps these studios paid for

**AI and flight**
- **Stop must END the run and hold** (2026-10-10). The Stoat's Stop only moved the hull home (`spawn()`), so the
  race ran on, and with the AI flying it the ready-state auto-start fired 0.6 s later. Stop sets the race back to
  ready and holds it there until Play (`stopRace()` / `stopHeld`); `check_studio.cjs` hands your hull to the AI
  (the case that restarted), clicks Stop, and fails a race that runs again within 4 s; the old Stoat fails it.
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
- **Domain colours drifted three ways** (2026-10-09): the Squirrel, the Stoat and Amoebius's launcher each
  typed their own Jade/Ruby/Gold, and none matched the game: Ruby is magenta in the game, not red. That is why
  colours now come from the generated `studio-domains.js` (§1.5.5).
- **Edit the Stoat's SOURCE (`StoatFlightStudio.html`), never its hub copy.** An edit made only to
  `VesselStudio/stoat.html` is lost the next time the copy is regenerated (2026-10-09: the dipole-sling archive
  landed in the copy alone and had to be ported back). `copy_stoat.py --check` before every build.
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
