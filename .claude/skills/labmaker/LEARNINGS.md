# Labmaker learnings — what each lab taught

Append-only and attributed. Every session that builds or extends a lab adds what it learned here,
usually from `/ship` §3.55. `SKILL.md` §9 says when an entry gets promoted into the skill body.

## Entry format

```
### L-<FAMILY>-<n> — <the lesson in one line>
- Lab: <name> (<path>) · Branch: <branch> · By: <session id / contributor> · Date: <YYYY-MM-DD>
- What happened: <the concrete case, with numbers>
- Do instead: <the rule>
- Evidence: <commit, file:symbol, result file>
- Promoted: <SKILL.md §N | no>
```

`<FAMILY>` is the lab family: `STU` (browser studios), `NCA` (swarm / neural-CA research rigs),
`ECO` (ecology bestiary / flight labs), `SIM` (calibrated headless models), `EDT` (in-editor lab
windows), `GEN` (applies to every lab). Start a new family when a new kind of lab appears. **Grep
the id before you claim it.**

Entries are grouped by family; within a family, the newest goes at the bottom.

---

## GEN — every lab

### L-GEN-1 — /labmaker was distilled from three lab families; the template is verified, the families' own pages are not re-verified here
- Lab: all · Branch: `cece/beautiful-heisenberg-yhsm7j` · By: the session that created this skill · Date: 2026-10-09
- What happened: the skill, `template/lab.html` and `verify_lab.cjs` were written from a read-only survey of `claude/peaceful-rubin-hhw49n` (Stoat studio, rounds 1–14), `cece/swarm-x-*` (Tools/NCA) and `cece/lab-*` (Tools/Ecology). `verify_lab.cjs` passes on the template, and its `--self-test` catches all three planted defects: a console error, a shipped value outside its range, a non-deterministic batch.
- Do instead: the first lab built from this skill should alias its hooks to `window.__lab`, run `verify_lab.cjs`, and record here anything the verifier missed or got wrong.
- Evidence: `node .claude/skills/labmaker/verify_lab.cjs --self-test`
- Promoted: §3

### L-GEN-2 — In this container Playwright loads only from a CommonJS script
- Lab: `verify_lab.cjs` · Branch: `cece/beautiful-heisenberg-yhsm7j` · Date: 2026-10-09
- What happened: Playwright is installed globally and reached through `NODE_PATH=/usr/local/lib/node_modules_global`. `require('playwright')` from a `.cjs` file works. `import { chromium } from 'playwright'` in a `.mjs` file fails, because ESM resolution ignores `NODE_PATH`. Chromium is pre-installed (`PLAYWRIGHT_BROWSERS_PATH`), so never run `playwright install`.
- Do instead: write headless lab checks as `.cjs`, launch with `--use-gl=swiftshader` for WebGL, and route CDN scripts to local copies when the network blocks them.
- Evidence: measured with a two-line `.mjs` against a `.cjs` probe.
- Promoted: no

### L-GEN-3 — Check a lab survey's citations against the source before they become learnings
- Lab: this skill's own seeding · Branch: `cece/beautiful-heisenberg-yhsm7j` · Date: 2026-10-09
- What happened: the subagent summaries used to seed this file got two details wrong. They cited D24 for the runner-up cull (it is D23; D24 is rollout noise). They called the `_q` clobber a cross-module collision (it was one class reusing its own field name). Both were caught at `/ship` §2 by grepping `DISCOVERIES.md` and `git show <sha>`.
- Do instead: before an entry lands, open the cited section, commit or file and confirm it says what the entry claims (the `/ship` §2 "find the PRODUCER" rule). A learnings file is read as fact by every later lab.
- Evidence: this branch's ship commit.
- Promoted: §9

---

## STU — browser studios (Stoat Flight Studio, Vessel Studio)

### L-STU-1 — A lab's scripted input exposed a real game bug: the sling read the latest trigger sample, not the squeeze
- Lab: Stoat Flight Studio (`Docs/Studios/StoatFlightStudio.html`) · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10 (round 3)
- What happened: the studio's scripted pad released the trigger the way a thumb does, sweeping it back to zero over a few frames before the release edge. Every gamepad sling came out minimum size, because the game slung the LATEST per-frame squeeze.
- Do instead: script inputs the way hands produce them (ramps, release sweeps, noise), not as ideal steps. When the lab and the game disagree, suspect the game's input handling as well as the lab.
- Evidence: `StoatSlingMath.Peak`, test `Peak_KeepsTheDeepestSqueezeThroughTheLetGo`; Docs/Studios/README.md "Found by it".
- Promoted: §8

### L-STU-2 — A probe found a portal hull being overtaken by its own white hole
- Lab: Stoat Flight Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10 (round 4)
- What happened: a hull carried out of a CLOSING pair, flying slower than the pair closed, was run down by its own exit mouth. The probe showed the distance to the white hole falling from 23 u to 16 u after the exit.
- Do instead: log per-frame distances and relative velocities in probes, not just outcomes. The bug is visible in the time series long before it is visible on screen.
- Evidence: `BlackHoleRegistry.TryGetMouthMotion`, used by `BlackHoleVesselPull.TryCarryThrough`.
- Promoted: §7

### L-STU-3 — When the AI's best play looks like a cheat, it is a design hole: the tap sling
- Lab: Stoat Flight Studio sim lab · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09 (round 12)
- What happened: the optimal AI play was caught-and-release at 0° round, because the release kick ignored the sweep. Making the kick earned (`dpKickSweep` 90°) made Balanced slinging slower than not slinging (2:22.4 against 2:11.5).
- Do instead: add the fix as an OPT-IN slider (default = shipped rule), measure both settings, and leave the call to the designer as **Decision needed**. Never quietly "fix" the AI.
- Evidence: Docs/Studios/STOAT_SIM_LAB_PLAN.md §2 "A design hole: the tap sling".
- Promoted: §8

### L-STU-4 — A rookie column is where the design's real costs show
- Lab: Stoat Flight Studio sim lab · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09 (round 12)
- What happened: Comet won average speed (168 u/s) but a rookie (`aiNoise`) was caught on only 21% of slings, against 88% on Balanced. The skilled pilot alone showed Comet as strictly better.
- Do instead: score every variant with a skilled pilot AND a sloppy one on several seeds.
- Evidence: STOAT_SIM_LAB_PLAN.md scorecard table.
- Promoted: §2.3

### L-STU-5 — A momentum-free boost can't stack; the lab measured why high speed was unreachable
- Lab: Stoat Flight Studio sim lab · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09 (round 12)
- What happened: the release boost faded inside its 1.5 s window, so slings never compounded. A perfect AI averaged 70 u/s. Momentum carry (`dpCarry` 0.4, `dpFlowFade` 5 s, `dpFlowCap` 1.5 × cruise) took it to 108 u/s.
- Do instead: when a mechanic "feels capped", have the AI fly it perfectly first. If the ceiling is low even then, the cap is in the design, not the pilot.
- Evidence: STOAT_SIM_LAB_PLAN.md §2 "Why high speed was hard".
- Promoted: no

### L-STU-6 — `[hidden]` does not hide outside the claude.ai viewer
- Lab: Stoat + Squirrel studios · Branch: `vessel-studio` / `claude/peaceful-rubin-hhw49n` · Date: 2026-10
- What happened: elements meant to be hidden showed when the repo file was opened directly, because a component `display` rule beat the UA's `[hidden]` style. The hub also overflowed on phones.
- Do instead: every page carries `[hidden]{display:none!important}` (the template does), and the repo file gets verified outside the viewer as well as inside it.
- Evidence: commit `addef2399`; StoatFlightStudio.html `[hidden]` rule.
- Promoted: §8

### L-STU-7 — A new button inherits its neighbour's positioning
- Lab: Stoat Flight Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10 (round 11, fixed in 12)
- What happened: "Play on phone" inherited the Stop button's `left`/`bottom` and stretched across the whole stage on wide screens. The Stop button also sat over the speed readout.
- Do instead: give each overlay control its own class. Check the desktop screenshot at a WIDE viewport (1600+), not only a phone.
- Evidence: Docs/Studios/README.md round 12 "Fixed".
- Promoted: §8

### L-STU-8 — Gamepad Y must be negated to match Unity's up = +y
- Lab: Stoat Flight Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10
- What happened: the browser Gamepad API reports stick-up as −1. Unity's input reads up as +1.
- Do instead: `inp.L = stick(g.axes[0], -g.axes[1])`, and comment it at the read site.
- Evidence: StoatFlightStudio.html `pollInput` (the `// Unity: up = +y` line).
- Promoted: §8

### L-STU-9 — The claude.ai viewer can block the Gamepad API and refuse fullscreen
- Lab: Stoat Flight Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10 (rounds 3, 11)
- What happened: a controller sometimes did not reach the page inside the artifact viewer. Phones refused real fullscreen there.
- Do instead: wrap `getGamepads` in try/catch, set a `padBlocked` flag and tell the user to open the repo file in Chrome. Request fullscreen on the first touch (it needs a gesture), lock landscape in try/catch, and fill the frame when the request is refused.
- Evidence: Docs/Studios/README.md "Gamepads" and round 11.
- Promoted: §6

### L-STU-10 — Tooltips with two live previews (30% / 70% of the range) made 79 sliders legible
- Lab: Stoat Flight Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10 (round 7)
- What happened: each slider's tooltip re-runs the same moment twice with only that number changed, using the page's own formulas, grouped into a few reusable SCENES (sling pass, hole view, prisms, …). Where a number only matters in an extreme, the card stages the extreme.
- Do instead: for a lab with more than about 15 sliders, build tooltip previews from shared scenes, not one per slider. The verifier should assert none renders blank.
- Evidence: README round 7: "158 previews, none blank".
- Promoted: no

### L-STU-11 — Five named "types" per settings group beat raw sliders for a designer
- Lab: Stoat Flight Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10 (round 10)
- What happened: `GROUP_TYPES` gives five one-click types plus Shipped per group (Pinpoint, Gentle giant, Cinematic, …). Hover shows shipped against the type. The decision log records "the X type" or "custom".
- Do instead: once a group has more than about 5 sliders, offer named types. The verifier should check every type's values lie inside its slider ranges.
- Evidence: README round 10.
- Promoted: no

### L-STU-12 — Graduate a studio to a hub plus a catalog, not to a copy per surface
- Lab: Vessel Studio · Branch: `vessel-studio` → `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09
- What happened: `studios.json` (`{id, name, file, kind, summary, docs, engineMode?}`) drives the web hub, Amoebius's STUDIOS page (`StudioCatalog.cs`, with tests) and `FrogletTools ▸ Vessels ▸ Vessel Studio`. `stoat.html` in the hub is a COPY of the source page, so the two can drift.
- Do instead: one catalog file feeds every surface. When a page is copied into a hub, record which file is the source and re-copy it every round, or build the copy rather than hand-copying it.
- Evidence: Docs/Studios/VesselStudio/README.md, VESSEL_STUDIO_PLAN.md §4.
- Promoted: §7

### L-STU-13 — "No page scroll" is not "fits": `overflow:hidden` hides a clipped layout from the check
- Lab: Stoat Flight Studio, round 14 (`origin/claude/peaceful-rubin-hhw49n` @ `93fe1577`) · Found by: `verify_lab.cjs` on `cece/beautiful-heisenberg-yhsm7j` · Date: 2026-10-09
- What happened: round 14 recorded "the stage fills its cell with no page scroll" at 1600 × 900. That is true, because `body` is `overflow:hidden`. But `.wrap` measures 912 px in a 900 px window, so the bottom dock's last 6–12 px are clipped. The same run found the round-13 platform chip (`#platChip`, `#platLayout`) pushing the header 85 px past a 400 px-wide window and 103 px past a portrait iPhone 13 (390 px). Landscape (750 px) fits. These were measured on the repo page, not yet confirmed by the studio's author.
- Do instead: measure fit as `scrollHeight − clientHeight` (verify_lab does), not as "can the user scroll". Re-run the narrow-width check after EVERY header addition: round 7 had checked 420 px, and round 13's chip came after it.
- Evidence: `node .claude/skills/labmaker/verify_lab.cjs StoatFlightStudio.html` plus an element-overflow probe: `div.wrap bottom=912`; `#platChip right=493` at 390 px.
- Promoted: no

### L-STU-14 — A lab that predates the `__lab` contract fails it in ways worth knowing
- Lab: Stoat Flight Studio, round 14 · Found by: `verify_lab.cjs` · Date: 2026-10-09
- What happened: aliasing `window.__lab = window.__stoatStudio` still left `SHIPPED`, `SPEC`, `reset`, `score` and `state` unexposed, and `runBatch()` with no arguments threw (`undefined is not iterable`; it needs its squeeze list). The verifier itself crashed on that throw until it learned to report a throwing hook as a named failure.
- Do instead: when putting an existing lab on the contract, expose those five members and give `runBatch` a no-argument default (the shipped batch). The verifier's `--self-test` now plants a throwing hook.
- Evidence: verify_lab report on the aliased page.
- Promoted: §3 (the contract list)

---

## NCA — swarm / neural-CA research rigs (Tools/NCA)

### L-NCA-1 — The yardstick was wrong four times before the creatures were
- Lab: NCA swarm labs (`Tools/NCA/`) · Branch: `cece/swarm-x-live2` (DISCOVERIES D6, D7, D23, D25; summary item 6) · Date: 2026-10-01
- What happened:
  - D6: `min()` over a tie of sentinels passed extinct swarms.
  - D7: switch tests passed even when no switch happened.
  - D23: `lose_majority` only ever handed the majority to the runner-up element. It was found independently by three sessions, and CMA exploited it.
  - D25: "closest of four" is not "looks like the plan". Adding the absolute bar `MAX_TEST_LOSS = 8` overturned the day's headline (the evo 16/16 became 0/16).
  - The zoo found the old bar never bound: 48 of 48 uniform-random genomes passed.
- Do instead: plant defects the metric must catch before trusting it. Run random genomes through the bar; if they pass, the bar is decoration. D24 adds that single rollouts were noisy enough to make a pass a coin flip, so use 3 samples per test.
- Evidence: Tools/NCA/DISCOVERIES.md D6, D7, D23, D24, D25.
- Promoted: §2.3

### L-NCA-2 — An optimiser will find every gap in the loss
- Lab: NCA · Branch: `cece/swarm-x-live2` (D1, D2) · Date: 2026-10-01
- What happened: a size-blind loss made "never grow" optimal. A one-sided gate gradient made "never lay" absorbing.
- Do instead: for each degenerate strategy (do nothing, stand still, never act), compute its score first. The loss has to rank it last.
- Evidence: DISCOVERIES D1, D2. The README's "Adding time" says the yardstick for "really moving" is the best any STILL image can do.
- Promoted: §8

### L-NCA-3 — `max(nan, eps)` is nan: a silent infinite loop, found by five separate sessions
- Lab: NCA · Branch: `cece/swarm-x-live2` (D5) · Date: 2026-10-01
- What happened: a NaN in the optimal-transport cost (`sinkhorn_ot`'s eps-scaling loop) made `e` NaN. `NaN <= eps` is always False, and Python's `max(nan, eps)` returns nan. The process burned 390% CPU with no exception. Five overnight babysitter sessions each diagnosed it from scratch with `py-spy dump --locals`, and it was preceded by a headcount collapse.
- Do instead: assert `isfinite` on every loop-control value and on state every N steps, and fail loud with the step number. Give every `while True` an iteration cap.
- Evidence: DISCOVERIES D5.
- Promoted: §8

### L-NCA-4 — Warm-start chains propagate defects
- Lab: NCA · Branch: `cece/swarm-x-live2` (D22, D29) · Date: 2026-10-01
- What happened: a frame lock (`anim=0`) carried through warm starts, so no swarm in the chain ever swam.
- Do instead: re-validate the core behaviour (does it move?) at every warm-start boundary, not only at the end.
- Evidence: DISCOVERIES D22, D29.
- Promoted: no

### L-NCA-5 — Branch publishers over-report; recompute from the result files
- Lab: NCA · Branch: `cece/swarm-x-live2` (D26) · Date: 2026-10-01
- What happened: commit messages quoted counts and wins that the `summary.json` files did not support.
- Do instead: never quote a commit message's count. Recompute it from `summary.json` or the equivalent.
- Evidence: DISCOVERIES D26.
- Promoted: §5

### L-NCA-6 — Exact JS/Python parity with a sign-flipped negative control
- Lab: NCA · Branch: `cece/swarm-x-play` … · Date: 2026-10-01
- What happened: `verify_js.py` matched JS to torch at fire rate 1 to 1.4e-7 (3D: 5.6e-7). A sign-flipped Sobel kernel had to disagree.
- Do instead: any port gets a parity gate plus a deliberately broken twin that must fail it.
- Evidence: Tools/NCA/verify_js.py, verify_js3d.py.
- Promoted: §2.8

### L-NCA-7 — A frozen yardstick and integrator-only publishing keep a parallel fleet comparable
- Lab: NCA · Branch: `cece/swarm-x-*` briefs · Date: 2026-10-01
- What happened: briefs forbade editing `swarm_nca.py`, `swarm_gpu.py`, `gpu_run.py`, `viewer*.py` and `build_viewer.py` (import, subclass or copy instead). "Do NOT republish the artifact yourself; the coordinating session merges and publishes."
- Do instead: freeze the yardstick and the viewer for the duration of a fleet. One integrator publishes.
- Evidence: Tools/NCA/briefs/README.md, briefs/live2.md.
- Promoted: §5

### L-NCA-8 — A GPU gave no speedup because the step was Python-bound; container restarts kill in-flight runs
- Lab: NCA · Branch: `cece/swarm-x-*` (D19) · Date: 2026-10-01
- What happened: the GPU did not help. A container restart killed a run, and archived sessions lost theirs.
- Do instead: profile the step before buying hardware. Checkpoint, and support `--resume`, from day one.
- Evidence: DISCOVERIES D19; gpu_run.py `CHECK OK` device-vs-CPU step.
- Promoted: no

### L-NCA-9 — A verifier that starts from a mature state cannot see bugs that only matter at birth
- Lab: NCA browser runner · Branch: `cece/swarm-x-live2` (DISCOVERIES, JS runner notes) · Date: 2026-10-01
- What happened: the first JS runner passed the grown-colony parity check while killing 11 of 40 colonies in their first 3–7 steps. It stored its own alpha in a `Uint8Array` before the alive max, which truncated 0.96 to 0. A grown colony always has a visible neighbour; a lone seed does not.
- Do instead: verify from the FIRST step as well as from a warmed-up state (a lone seed plus one dormant child). `verify_lab.cjs` ticks 120 frames from `reset`; a lab whose risk is at birth adds a t = 0 check of its own.
- Evidence: DISCOVERIES "Verifier blind spot" (the check now also covers the lone seed, to 1.8e-7).
- Promoted: §8

---

## ECO — ecology bestiary and flight labs (Tools/Ecology)

### L-ECO-1 — A viewer "too austere to judge" stalls a lab; a shared viewer standard fixed it
- Lab: ecology bestiary (`Tools/Ecology/`) · Branch: `cece/lab-*` (via `cece/gifted-curie-x2cpd0`) · Date: 2026-10-05
- What happened: the lead rejected the round-1 viewers as too austere to judge. `common/viewer.py` then standardised four cameras (pilot, chase, orbit, fly), a HUD, a minimap and playback on the recording's own clock, behind a `window.HOOKS` data contract, with `--retemplate` to re-skin old viewers.
- Do instead: a lab's viewer must let a human FLY or orbit the thing at game scale. Put the viewer shell in one shared module from round 1.
- Evidence: DISCOVERIES "Flyable ecology"; common/viewer.py.
- Promoted: §2.7

### L-ECO-2 — Build the page from the file the gate measured
- Lab: ecology flight lab · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: `flight/build.py` concatenates `src/NN_*.js` into one `sim.js`. Node requires it for the gates and the page inlines it, "so the page flies exactly the code the gate measured". `--check` reports STALE. `port_manifest.json` sha256s make `--check` fail when a ported Python file changes.
- Do instead: one source file feeds both the gate and the page. Hash the files a port depends on.
- Evidence: Tools/Ecology/flight/build.py, port_manifest.json.
- Promoted: §2.8

### L-ECO-3 — Fix the pilot before the creature
- Lab: ecology bestiary · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: the scripted evader's lerp turn runs at about 0.2 rad/s near 180°, so it flew into the lurker. Lurker changes were tried first, made things worse, and were rejected. A perpendicular swerve in the pilot fixed it.
- Do instead: when a creature scores badly, first watch the scripted pilot's trajectory. The reader pilot needed three fixes before it measured the plant rather than itself.
- Evidence: DISCOVERIES (lurker).
- Promoted: §2.4

### L-ECO-4 — A fixed timestep is a SAMPLE, not a test
- Lab: ecology bestiary · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: a 320 u/s lunge tunnelled through a 19 u contact radius at the lab's dt.
- Do instead: score at the dt the game will run (the fidelity gate's `--dt30`), or sweep contacts.
- Evidence: DISCOVERIES; run_fidelity_all.sh `--dt30`.
- Promoted: §8

### L-ECO-5 — `x <= 0 and x + dt > 0` never fires
- Lab: ecology bestiary · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: this edge test silently disabled the bull charge and the mobber's pull-up.
- Do instead: give each state-machine edge a planted test case that must trigger.
- Evidence: DISCOVERIES.
- Promoted: §8

### L-ECO-6 — An emotion/legibility probe needs a NEUTRAL class
- Lab: ecology bestiary (`bestiary/emotion_check.py`) · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: without a neutral class, indifference read as "majestic". The probe also turned out to be "mostly a size detector", and its labels "are not human ratings" until a 5-rater pass is done.
- Do instead: include the null answer in any classifier-style metric. Validate by holding out whole families (LOFO). Write down what the probe actually detects.
- Evidence: DISCOVERIES (emotion); emotion_check.py.
- Promoted: §8

### L-ECO-7 — A threat the pilots never meet scores 0
- Lab: ecology bestiary · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: creatures placed away from pilot routes measured zero telegraph and zero counterplay. Flora had to become a PLACE (a grove).
- Do instead: give a creature a local reason to be where pilots go before you score it.
- Evidence: DISCOVERIES.
- Promoted: §8

### L-ECO-8 — `Array.from().sort()` in a grid query cost 13 of 18 ms
- Lab: ecology flight lab · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: per-query allocation and sorting dominated the frame.
- Do instead: use generation-stamped marks for visited sets in spatial queries. Profile before optimising (CLAUDE.md "Debugging Methodology").
- Evidence: DISCOVERIES (performance).
- Promoted: §8

### L-ECO-9 — A field-name clobber inside one class froze regrowth
- Lab: ecology bestiary · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: `NcaCreature.mesh()` stored its quaternion in `this._q`, the name the sparse step uses for its work queue. Any creature with a mesh silently stopped regrowing. The fix renamed it to `_rq`.
- Do instead: assert the liveness behaviour (regrowth happens) in the browser check, not only its absence of errors.
- Evidence: commit `f903548c7`; `creatures_browser.py` now asserts regrowth.
- Promoted: no

### L-ECO-10 — Committed MB-sized viewers and PNG shots churn every merge
- Lab: ecology bestiary · Branch: `cece/lab-*` · Date: 2026-10-05
- What happened: results JSON, logs, about 2.3 MB of generated HTML, `sim.js` and many PNGs were committed, causing heavy binary churn between branches. Only `.cache/`, `out/` and a few others were ignored.
- Do instead: decide per output whether it is a deliverable (commit it, once per round) or derived (gitignore it and rebuild). Pack recordings as binary (25 MB of JSON down to 2.3 MB).
- Evidence: Tools/Ecology/.gitignore; bestiary/build_viewer.py.
- Promoted: §5, §6

---

## SIM — calibrated headless models

### L-SIM-1 — Read the real assets and calibrate to one measured anchor; it is a lever-ranker, not an oracle
- Lab: ecosim (`Tools/ecosim/`) · Branch: on bleeding-edge · Date: 2026-09
- What happened: a stdlib model of the menu's food web and frame cost, pinned to one real `EcosystemPerfProbe` sample in `calibration.csv`, explained the 5 fps menu (the OverlapSphere term ate about 71% of the budget) and ranked the levers.
- Do instead: name the calibration anchor and the priors in the README. Close the loop with an in-game probe whose output pastes straight into the calibration file.
- Evidence: Tools/ecosim/README.md.
- Promoted: §0

---

## EDT — in-editor lab windows

### L-EDT-1 — A lab window's preview must run the SHIPPED GPU code, and Measure must run the shipped metric
- Lab: Occlusion Dither Lab (`Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs`) · Branch: on bleeding-edge · Date: 2026-09
- What happened: the window promotes compile-time `#define` dials to global uniforms so they can be slid in play mode. Its preview includes the corridor's own HLSL. Measure renders and reads back the real admission metric (|coverage − alpha| under about 0.01), measured as a RATIO against the shipped baseline. Bake closes the loop.
- Do instead: a lab that re-implements the kernel can drift from the game, and one that calls the shipped kernel cannot. Report verdicts as ratios against the shipped baseline.
- Evidence: PrismOcclusionDitherLab.cs class doc.
- Promoted: §0
