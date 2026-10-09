---
name: labmaker
description: Use to BUILD, extend or graduate a LAB - a prototype rig that lets a designer decide a mechanic by LOOKING and PLAYING before it is built in Unity - a single-file browser studio (the Stoat Flight Studio, the Vessel Studio hub), a headless research rig with a generated viewer (the NCA / swarm labs, the ecology bestiary), a calibrated headless model (Tools/ecosim), or an in-editor lab window (PrismOcclusionDitherLab). Loads the lab contract (a reader of the shipped numbers, a test surface with a manual clock and a headless scorecard, seeded runs, a decision log Claude can read back, an honest not-modelled list), the round protocol, the verifier (verify_lab.cjs) and the trap list every earlier lab paid for - and its LEARNINGS.md is where every contributor adds what their lab taught. Trigger on "make a lab / studio / sandbox / prototype page / playground / viewer / sim", on Docs/Studios/**, Tools/NCA/**, Tools/Ecology/**, Tools/ecosim/**, a *Lab.cs editor window, a DISCOVERIES.md / PROGRAM.md / briefs/ folder, or any HTML page meant to tune a mechanic. /ship §3.55 sends every branch that touched a lab back here.
---

# Labmaker — build the page that lets a human DECIDE

A lab exists to turn a design question into something a person can play with, and then to turn
what they felt into a NUMBER and a DECISION that survive the session. It is not the feature. The
Stoat Flight Studio took fourteen rounds to decide the Stoat's sling; the swarm and ecology labs ran
dozens of parallel sessions to decide which creatures were worth porting. Every one of them
rediscovered the same craft. This skill holds that craft so the next lab starts at round 5, not
round 1.

**Read `LEARNINGS.md` (this folder) before you start**: it is the running log of what each lab and
each contributor learned, newest first. **`CATALOG.md`** lists the labs that exist, where they
live and what they decided. Start from the nearest one, not from a blank file.

## 0. Pick the kind of lab from the QUESTION

| The question | Lab kind | Shape | Reference lab |
|---|---|---|---|
| "How should this FEEL to fly / steer / tune?" | **Browser studio** | one self-contained HTML page, published as a claude.ai artifact, repo copy is the source | Stoat Flight Studio (`Docs/Studios/`, branch `claude/peaceful-rubin-hhw49n`) |
| "Which of N designs/behaviours is best, by a measured yardstick?" | **Research rig + generated viewer** | Python (or Node) model + frozen shared yardstick + a build script that bakes results into ONE viewer HTML | NCA swarm labs (`Tools/NCA/`, `cece/swarm-x-*`), ecology bestiary (`Tools/Ecology/`, `cece/lab-*`) |
| "Will this config hit the budget (fps, colliders, populations)?" | **Calibrated headless model** | stdlib script that reads the REAL assets, calibrated to a measured anchor | `Tools/ecosim/` |
| "Which look/shader dial is right, in motion, on the real GPU code?" | **In-editor lab window** | `FrogletTools/` window whose preview runs the shipped shader and whose Measure runs the shipped metric | `Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs` |

**If the question is about feel AND numbers, it is a browser studio with a headless scorecard
inside it.** The Stoat studio is both: a human flies it, and **Score all five styles** runs about 80
races headless in a few seconds.

## 1. Before the first line

1. **Write down the decision the lab exists to make, as columns.** One number per question: the
   Stoat scorecard has one column per play style (Comet = average speed, Needle = ring error, …),
   and "each style wins exactly its own column" was the finding. If you cannot name the column, the
   lab will produce screenshots, not decisions.
2. **Inventory what the game has today, measured, not remembered** (the ecology `PROGRAM.md` §0.5
   shape): the class names, the asset fields, the shipped numbers. Read the ASSETS, never a C#
   field initializer (`/ship` §2: the assets are the game).
3. **Copy the lab contract** (§2) from `template/lab.html` (browser) or the nearest lab in
   `CATALOG.md`. Do not re-invent the decision log, the manual clock or the platform switch.
4. **Load `artifact-design`** before writing the page, and **`artifact-capabilities`** before
   writing any `window.claude` code: its `use()` contract is versioned and is the authority over
   any copy in this repo.

## 2. The lab contract — every lab meets all of it

### 2.1 A READER of the shipped numbers, never their authority

- One `SHIPPED` block. **Every constant names the asset or class field it was copied from**
  (`// StoatSlingConfig`, `// StoatLopeMath.Defaults`, `// prefab: throttle scaler 60`). Where the game
  has a field, the key uses the field's name. Lab-only knobs are marked as such and get a prefix
  (`dp*`, `ai*`, …).
- **Shipped vs yours is always visible**: a changed value is underlined, its tooltip shows *shipped ·
  your setting · effective*, and the header counts the differences. When the asset moves, the page
  follows **or says on screen that it differs**.
- **A proposal is labelled as a proposal** ("Recommendation: tuned", "not in the shipped asset").
  A lab number that silently replaces a shipped number is the lab becoming a second authority.
- Saved settings store only the DIFF from `SHIPPED`, so a retune of the asset reaches every saved
  browser.

### 2.2 A test surface: manual clock, pure step, headless batch, seeds

- `simStep(dt, input)` **never draws and never reads a device**. The frame loop calls it once; the
  batch calls it thousands of times without drawing; a recorder steps it frame by frame.
- `window.__<lab>` (the template uses `window.__lab`) exposes `manualClock`, `tick(dt, n)`, `reset(seed)`,
  `score()`, `runBatch()`, `state()`, `SHIPPED`, `SPEC`, `P`, `PLATFORM`. This is how a later Claude, the
  verifier and a frame recorder drive the page. A page without it can only be screenshotted.
- **Every run is seeded** (`mulberry32` in the template); every random draw, the AI's included, comes
  from a seeded stream. Same seed + same settings = same score, and the verifier checks that.
- **Sliders are data**: `SPEC = [[group, key, lo, hi, step, label, tip], …]`. Tooltips, presets,
  validation and the verifier all read the one table.

### 2.3 A scorecard that can say NO

- **Baselines in every table**: the ability OFF (Stoat "No sling") and the shipped tuning. A result
  without a baseline is a number without a unit.
- **Negative and drift controls**: the struck body vs an unstruck one, the still hole vs the moving
  hole. If the control moves, the effect is not the mechanic.
- **Know the noise band before you claim a win**: 3 seeds × 2 min was about ±0.04 on the ecology
  scorecard. A change inside the band is a plateau, not a result.
- **The yardstick will be wrong before the creature is.** Four of the swarm lab's worst findings
  were bugs in the metric (L-NCA-1). Plant a defect that the metric must catch before trusting a
  green number.
- **Skilled and rookie**: score with a skilled pilot AND a sloppy one (`aiNoise`). The Stoat's
  "Comet is the expert's style" (21% rookie catch rate against 88%) came only from the rookie
  column.

### 2.4 Pilots and AI use INPUTS only

The AI writes the same input object a gamepad does, never the vessel's or the creature's state.
This mirrors the game's own rule (`AIPilot`, `SkimRacePilot`, `Tools/Build/check_ai_no_state_writes.py`)
and is why a lab result transfers. **Fix the pilot before the creature**: when a scripted evader flew
into the lurker, the lurker changes made it worse. The pilot's lerp turn was the bug, and those
changes were rejected (L-ECO-3).

### 2.5 A decision log Claude can read back

- **Shared**: `await window.claude.use('db')` → `db.collection('decisions')`, each doc
  `{topic, choice, note, settings: <diff from SHIPPED>, createdAt, by}`, `by` from `use('user').id()`.
  Publish with `capabilities: {db: {}, user: {}}`. Claude reads it with `ArtifactData` `list`,
  collection `decisions`, **at the start of every round**.
- **Fallback**: outside the viewer, or signed out, `use()` is absent or resolves `null`. The log then lives in
  `localStorage` with **Copy log** (and pushes to the db when it appears). Every storage call is
  wrapped in try/catch.
- Pre-fill the entry from the live state (`currentChoice(topic)`), so a decision always carries the
  numbers it was made on.

### 2.6 An honest "what this does NOT model" — on the page

Every lab says on screen what it leaves out ("the course is a stand-in", "the lenses are
screen-space approximations", "frame time not measured on a real GPU"). A lab without this list
reads as a promise about the game.

### 2.7 Layout and platform

- **Editor layout, no page scroll**: stage in the middle, a right dock with a tab per settings
  group, a bottom dock for Runs / Scorecard / Decisions / About. Below 900 px the docks stack.
- **Platform detection is answered once at load**: a native host sets `window.__labHost = {shell, device}`
  (Prisma uses `#prisma` because a file page cannot be handed a variable); otherwise phone =
  mobile UA / iPadOS-as-Mac / coarse-pointer-only. A touchscreen laptop is a PC. Give a manual
  Layout override.
- **Phones**: real fullscreen needs a gesture (request it on the first touch); portrait gets a way
  back to settings; touch controls feed the SAME input object as a gamepad.

### 2.8 Fidelity when the model lives in two languages

When the lab is ported (Python ↔ JS ↔ C#), a **parity gate** owns the claim that the two models are
the same model. Use an exact gate where possible (the NCA JS against torch to 1e-7, with a sign-flipped
kernel that must disagree). Otherwise use a statistical gate (|js − py| ≤ max(floor, band, 2.5 × bootstrap
SE)) with planted broken parameters that must fail. Build the page FROM the file the gate measured
(`flight/build.py` concatenates `src/*.js` into the one `sim.js` that Node gates and the page
inlines) so "the page flies exactly the code the gate measured".

## 3. Verify before you hand it over

```sh
node .claude/skills/labmaker/verify_lab.cjs <lab.html> [--out <dir>]
node .claude/skills/labmaker/verify_lab.cjs --self-test        # the gate's own negative controls
```

It opens the page in headless Chromium (pre-installed; never `playwright install`) at 1600×900 and
as an iPhone 13, and fails on:

- any console or page error;
- a desktop page that scrolls, or a phone page that scrolls sideways;
- a missing `__lab` hook;
- a `SPEC` key absent from `SHIPPED`, or a shipped value outside its slider range;
- a manual clock that does not advance;
- a non-deterministic `runBatch`;
- a blank 2D stage.

It writes `desktop.png` and `phone.png`. **Read the screenshots**: the gate cannot see an overlap.

A studio whose hook object is not called `__lab` either aliases it (`window.__lab = window.__stoatStudio`)
or is verified by its own checks.

Then add the lab-specific checks, and **list what was checked in the round's write-up**. The Stoat
studio records, for example: "every tooltip renders and none of the 158 previews is blank", "80 races in about 4.5 s",
"a scripted gamepad released over five frames slung strength 12". For WebGL, route three.js from a
local file when the CDN is blocked, and launch with `--use-gl=swiftshader`.

## 4. Rounds — how a lab moves

- **One round = one commit + one write-up section**: what changed, a measured table, **Checked**,
  and **Found** (bugs and design holes the lab exposed). The Stoat's `Docs/Studios/README.md` is
  the reference: newest round first, older rounds kept and marked superseded.
- **The human decides between rounds.** Open each round by reading the decision log and say "your
  decisions applied". Never decide a design question the lab was built to put to a human; leave it
  open in the write-up as **Decision needed (designer)** (the tap sling).
- **Supersede, never delete**: a rejected variant moves to an **Archive** tab, so its comparison
  stays one click away.
- **Re-run every number a write-up quotes** before the round ships (`/ship` §3: a measurement is a
  derived value). Name the tool that produced it.

## 5. Fleets — many sessions, one lab

For a research lab run as parallel autonomous sessions (the NCA and ecology programs):

- **A `PROGRAM.md`** holds the lead's brief verbatim, the stop rule ("stop when we have no reason to
  believe further searching would give better results"), the locked rules copied from CLAUDE.md, the
  design target as scorecard criteria, and the work split: one direction = one session = one branch.
- **A `briefs/<tag>.md` per session**, always the same sections:
  - the problem and the status so far;
  - **SHARED YARDSTICK** ("use these unchanged");
  - **ENGINEERING RULES**: push only your own branch, no PR, long jobs in the background with
    resumable snapshots, never kill a process by name, one CPU-heavy job at a time, commit at least
    every ~90 min, `send_later` check-ins;
  - the deliverables (`summary.json`, an honest `NOTE.md` with a next-round recommendation);
  - YOUR DIRECTION.
- **The yardstick is frozen**: sessions import, subclass or copy it, and never edit it.
- **Only the integrator publishes the artifact.** Sessions write results; one session merges and
  republishes.
- **A `DISCOVERIES.md` is the durable record**, because sessions are archived and their context is
  lost. Numbered entries, each with Mechanism / Evidence / Fix / Why it matters, plus
  "Negatives (kept)" and "Open threads (deliberately not mistaken for results)". **Recompute counts
  from the result files; never quote a commit message's count** (L-NCA-5).
- Scratch (`runs/`) is gitignored; a finished run is PROMOTED into a tracked `results/<tag>/`.
  Large regenerated binaries (PNG shots, MB-sized viewers) churn every merge. Commit them
  deliberately, not by habit.

## 6. Publishing

- The **repo copy is the source**. The artifact is published FROM it, and the URL goes in the lab's
  README and in `CATALOG.md`. Re-publish to the same URL; a new URL forks the decision log.
- Declare only the capabilities the page calls (`db` + `user` for the log; `sample` for an Ask
  box). After the first publish, `ArtifactData list decisions` once to prove the log is wired.
- **The claude.ai viewer may block the Gamepad API and refuse fullscreen.** Wrap `getGamepads` in
  try/catch and tell the user to open the repo file in Chrome. That is a viewer limit, not a bug in
  the lab.
- Keep the page under 16 MB. Pack recordings as binary (int16 positions, uint8 colour, base64).
  The ecology showcase went from about 25 MB of JSON to 2.3 MB this way.

## 7. Graduation — how a lab's finding reaches the game

| What the lab produced | Where it lands |
|---|---|
| A bug in shipped code the lab exposed | A fix in the game WITH an edit-mode test named for the behaviour (`StoatSlingMath.Peak` → `Peak_KeepsTheDeepestSqueezeThroughTheLetGo`; `BlackHoleRegistry.TryGetMouthMotion`) |
| A tuned number | The SO asset, through its generator if it has one. The lab's `SHIPPED` then follows the asset |
| A scorecard claim ("each style wins its column") | An edit-mode test that asserts it on the C# model |
| A model the game should run | A pure-C# core plus a parity harness that scores it with the lab's UNCHANGED yardstick (`Tools/Build/swarm_core_harness/`, `nca_creature_harness/`) |
| A design question | **Decision needed** in the write-up and the owning `Docs/` file. Never decided by the porter |
| The lab itself, for designers | A hub entry (`Docs/Studios/VesselStudio/studios.json`), Prisma's STUDIOS page, `FrogletTools ▸ Vessels ▸ Vessel Studio` |

- **Do not import the lab into the game.** What crosses over is the numbers and the tests.
- **Extract a shared shell when the SECOND lab needs it, not before** (the Vessel Studio plan §4).
- Anything a port could not verify in the editor goes to `Docs/QA/QA_BACKLOG.md`
  (`/qa-backlog`).

## 8. Traps every lab has paid for

Each has an entry in `LEARNINGS.md` with the evidence.

- **Hidden that doesn't hide.** Outside the claude.ai viewer a component rule beats `[hidden]`.
  Every page carries `[hidden]{display:none!important}` (L-STU-6).
- **A button inherits positioning from its neighbour.** Round 11's Play-on-phone button inherited
  Stop's `left`/`bottom` and stretched across the stage. Read the desktop screenshot at a wide
  viewport (L-STU-7).
- **The latest sample is not the squeeze.** An analog trigger sweeps back toward zero before the
  release edge, so reading it at release always reads small. The lab's scripted pad releases the
  way a thumb does, which is how the game bug showed (L-STU-1).
- **Gamepad Y is inverted** relative to Unity's up = +y. Negate it, or every pitch result is a
  mirror (L-STU-8).
- **A fixed timestep is a SAMPLE, not a test.** A 320 u/s lunge tunnelled a 19 u contact radius.
  Score at the dt the game will run (L-ECO-4).
- **A state-machine latch written as `x <= 0 && x + dt > 0`** silently never fires. Test edges
  with planted cases (L-ECO-5).
- **`max(nan, eps)` is nan** in Python. A state runaway became a silent infinite loop that took
  five diagnoses and about 10 run-hours (L-NCA-3).
- **A size-blind loss makes "never grow" optimal**, and a one-sided gate gradient makes "never lay"
  absorbing. The optimiser finds every gap in the yardstick (L-NCA-2).
- **A probe needs a NEUTRAL class**, or indifference reads as an emotion (L-ECO-6).
- **A threat the pilots never meet scores 0.** Give it a local reason to be where pilots go (L-ECO-7).
- **Hot-path sorting in a grid query** (`Array.from().sort()`) cost 13 of 18 ms. Use
  generation-stamped marks (L-ECO-8).
- **A design hole looks like a skilled AI.** The best play was a 0° "tap sling" because the kick
  ignored how far round the hull went. When the AI's best play looks like a cheat, it is a design
  question for the human, not an AI bug (L-STU-3).

## 9. Keep this skill alive (every contributor, every lab)

This skill is only as good as the last lab that updated it. **Before the round's last push**, and
always when `/ship` §3.55 sends you here:

1. **`CATALOG.md`**: add or refresh the lab's row (kind, branch, path, live URL, status, what it
   decided).
2. **`LEARNINGS.md`**: append one entry per thing the lab taught that cost real time or that the
   next lab would otherwise re-learn: a trap, a technique, a verification that caught something, or
   a process change the human asked for. Use the entry format at the top of that file.
   - **Grep the id before claiming it.** The next free number is not always the last row (`/ship` §1).
   - Attribute it: branch, session or contributor, date.
   - A lab that taught nothing new says so in the ship report. Silence reads as "not checked".
3. **Promote** an entry into §2–§8 of this file when it has recurred in a second lab, or when
   it cost a round or more. Mark it `Promoted: §N` in LEARNINGS so it is not promoted twice. Keep
   §8 to traps that recur; one-offs stay in LEARNINGS.
4. If the lab adds a reusable check, extend `verify_lab.cjs`, add a planted defect to its
   `--self-test`, and run both.
