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

### L-GEN-4 — A baker that splices between markers must match the marker LINE, not the marker text
- Lab: Omni Shepard Lab (`Docs/Studios/OmniShepardLab.html`) · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-10
- What happened: `Tools/Build/omni_shepard_lab_assets.py` bakes the shipped meshes and material numbers between `// <assets>` and `// </assets>`. The page's header comment NAMED the markers in prose, `str.find` matched that first, and the splice deleted the whole `<head>`, style and body markup. The page then loaded as an empty document: no console error, no page error, only `window.__lab is missing` from the verifier.
- Do instead: match a marker only when it is alone on its line (regex anchored `^\s*marker\s*$`), require exactly one of each, and plant "a marker named in prose" in the baker's `--self-test`. When a page shows nothing and logs nothing, check `document.body.innerHTML` before debugging the script.
- Evidence: `omni_shepard_lab_assets.py:splice`, self-test "a marker mentioned in prose is left alone".
- Promoted: no

### L-GEN-5 — Run the COMMITTED tool, not the scratch copy you built it from
- Lab: Omni Shepard Lab · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-10
- What happened: round 3's pixel-parity tool was assembled as "a header heredoc + `sed '1,0d' scratch.cjs`". `1,0d` deletes line 1, which was `const { chromium } = require('playwright')`. The scratch copy kept passing, and its result went into the write-up, but the committed `Tools/Build/omni_shepard_lab_parity.cjs` could not run at all. It was found in round 4, the first time anyone ran the committed file.
- Do instead: after copying a tool into the repo, run it from its repo path once before quoting its output. Never splice files with `sed` line ranges; write the file whole.
- Evidence: round-3 commit vs round-4 fix of `omni_shepard_lab_parity.cjs`.
- Promoted: no

### L-GEN-6 — Bake an artist's animation straight out of the .blend, and refuse to write unless the replay matches Blender
- Lab: Omni Shepard Lab, round 6 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: the designer attached two `.blend` files and said the "squares weaving" effect was in there somewhere. Headless Blender (`blender -b file.blend --python script.py`) listed every object, its shape keys and its action's curves in seconds: `OmniCrystal.001`, three relative keys (one stacked on another, so a delta has to be taken against ITS relative key, not the Basis), 120 frames at 25 fps. The baker writes the basis, the per-key deltas and the per-frame weights that Blender's curves produce, then replays them and compares the result with Blender's own evaluated mesh at every frame (with the modifiers off, since the display node group splits edges). It refuses to write past 1e-5; measured 1.15e-6. The .blend held the model at 2.2417× the shipped FBX with a different vertex order, so the extractor matches the vertices by position and refuses a body it cannot map (a self-test shifts the body by 0.01 to prove it).
- Do instead: never hand-transcribe keyframes, and never eyeball an export. Read the source file headlessly, write the exact data (keys and evaluated weights, not curve guesses), prove the replay against the tool's own evaluation, and map onto the shipped mesh with a measured error and a planted mismatch. Keep the `.blend` out of the repo; commit the bake script and its JSON.
- Evidence: `Tools/Build/bake_omni_space_squares.py`, `omni_shepard_lab_assets.py:space_squares` and its self-test, `OMNI_SHEPARD_LAB.md` "Round 6".
- Promoted: no

---

### L-GEN-7 — A parity gate against a float32 reference needs a tolerance that grows with distance, and planted controls to show it can still fail
- Lab: Thresher Plant Lab (`Tools/Build/thresher_plant_lab/parity.cjs`) · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: the page's float64 port of `ThresherChainSolver` drifted from the C# solver (float32, compiled from its real path with the chain harness's stubs) by up to 0.06 u after ~600 u of a spun-up launch. The gap grew smoothly and never stepped, and the mode sequence matched on all 1 224 frames, so it was rounding, not a divergence. A fixed 0.02 u tolerance failed it; a per-coordinate relative tolerance failed it too, on a coordinate near zero while the ship was far from the origin. 0.02 u + 2·10⁻⁴ × the ship's distance from the origin passed at 0.80× of tolerance.
- Do instead: before widening a parity tolerance, print the gap every N frames. Widen only if it grows smoothly with distance or time, and then scale it by that quantity, not by the coordinate itself. Keep planted dial changes in the gate (here a 7% skid change misses by 46×) to show the wider tolerance still fails a real change.
- Evidence: `parity.cjs` (TOL, REL, the three negative controls).
- Promoted: no

### L-GEN-8 — Graduate through a SHARED per-frame entry point, and check the asset against the preset in the same gate
- Lab: Thresher Plant Lab graduation · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: round 2's control logic (the trigger thresholds, the slew, when to stop and when to let go) lived in the lab's `Vessel` class, outside the solver the parity gate measured. Graduating it as `ThresherChainSolver.AnalogTriggers` and refactoring the lab to call the same method put the controls, not just the physics, under the frame-parity gate (5 analog flights). A third check in the same gate parses `ThresherConfig.asset` and requires its 20 analog dials to equal the lab's preset, so an asset retune that the lab does not follow (L-STU-29) fails a gate instead of drifting silently.
- Do instead: when a lab graduates, move every per-frame decision the pick depends on into the ported module, call it from both sides, and add an asset-equals-preset check to the parity gate, with a planted asset change that must fail it.
- Evidence: `parity.cjs` step 5; `analogTriggers` in the page.
- Promoted: §7 (with L-STU-35)

### L-GEN-9 — Two batch calls in one page cannot see a load-time `Math.random`; reload and compare
- Lab: Stoat Flight Studio, round 15 · Branch: `cece/magical-carson-9bdq8z` · Date: 2026-10-09
- What happened: the Stoat's prism field (2,400 homes) was placed with `Math.random` at load. Every scorecard column was reproducible except "prisms pulled per sling", which differed per page load (Balanced 984.1 against 986.3). `verify_lab`'s determinism check called `runBatch()` twice in one page, so both calls saw the same random field and it passed for 14 rounds. It was found by comparing the round-14 file's batch with round 15's across two loads.
- Do instead: seed everything a batch can read, scene setup included (`mulberry32(20261009)` for the field). `verify_lab` now opens a second, fresh page and requires the same `runBatch()` rows. Its `--self-test` plants a load-time salt that must be named, and a copy of the Stoat with the old `Math.random` fails the check.
- Evidence: StoatFlightStudio.html prism-field block ("seeded (round 15)"); `verify_lab.cjs` "a second load".
- Promoted: §3

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

### L-STU-15 — A look lab's scorecard is analytic, and its best column is "where does the eye first see it"
- Lab: Omni Shepard Lab (`Docs/Studios/OmniShepardLab.html`) · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-10
- What happened: the omni crystal's Shepard triangles have no randomness and no pilot, so the scorecard is computed straight from the meshes and the shader formula (seed only picks plates). The column that decided things was "seen from", the height where alpha first passes the clip, not the authored launch height: raising `_Start` from 1.0 to 1.6 tripled the launch height on paper (6.8 → 15.2 u) but the triangles were still first seen at 7.4 u, because the shader hard-codes `alpha = (1.05 − s)`. A "landing hidden by the body" column with a planted "lands short" control did the same for the far end (prefab scale 1.2 lands 2.2 u short: a visible pop).
- Do instead: for a visual effect, score what the viewer perceives (first visible, hand-off hidden, seams) beside the authored numbers, and tag every slider with whether it ships as data, needs an asset, or needs a shader edit (`needs mesh` / `needs shader` chips on the row) so a designer never tunes a knob that cannot ship.
- Evidence: `Docs/Studios/OMNI_SHEPARD_LAB.md` round 1 scorecard; `window.__lab.metrics`.
- Promoted: no

### L-STU-16 — Chromium reports a WebGL program that failed to link as a WARNING, so a lab can pass with its main pass missing
- Lab: Omni Shepard Lab (`Docs/Studios/OmniShepardLab.html`), round 1 → 2 · Branch: `cece/eager-rubin-km7qyn` · Found by: the designer, on the live page · Date: 2026-10-10
- What happened: the shell fragment shader declared `uniform float uS` at `mediump` while the vertex shader had it at the `highp` default. WebGL refuses that link, and every shell draw was dropped as `WebGL: INVALID_OPERATION: useProgram: program not valid`. That message is a console *warning*, so `verify_lab.cjs` (errors only) passed. The screenshot I "read" showed the body alone and I took it for the effect. A second bug in the same page: re-deriving the body's normals and "flipping them outward" by a centroid test scrambled a hollow, bevelled mesh (inner faces point in on purpose, and walls point sideways, where the sign is noise).
- Do instead: (1) `precision highp float` in every fragment shader that shares a uniform with its vertex shader. (2) Check `LINK_STATUS` and `console.error` on failure. (3) Use a mesh's AUTHORED normals; never re-derive or "fix" their orientation. (4) Read a WebGL screenshot against a concrete expectation ("three translucent shells larger than the body"), not "something rendered". `verify_lab.cjs` now fails on WebGL `INVALID_*` / link warnings, with a planted defect in `--self-test`.
- Evidence: `git show <round-1 commit>:Docs/Studios/OmniShepardLab.html` fails the updated gate. `OMNI_SHEPARD_LAB.md` "Round 2".
- Promoted: §3 (verify_lab now checks it)

### L-STU-17 — "People don't like it" is answered with BREADTH and a vote, and a new model must still draw the old look to the pixel
- Lab: Omni Shepard Lab, round 3 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-10
- What happened: after round 2 the feedback was "people don't like it, give me many more options", with an empty decision log. More sliders on the one shipped look would not have helped. Round 3 generalised the effect model (per-plate clock, path, spin, size), shipped 18 named styles plus a seeded `randomStyle(n)` generator, a 3×3 compare grid that draws nine parameter sets in one WebGL canvas with scissored viewports, and shared `votes` / `styles` collections with a Most-liked ranking. Two proofs kept it honest. (1) A pixel parity harness against the previous round's page from git, at the shipped settings, with a negative control. (2) The scorecard's seamless-loop columns, which caught two of the new styles popping before anyone saw them.
- Do instead: when a look is rejected, widen the space (named styles + seeded random + side-by-side compare) and let the team vote in the page. Tag every option with what it costs to ship. When you generalise a model, diff its pixels against the old round at the shipped settings before claiming "shipped is unchanged".
- Evidence: `Tools/Build/omni_shepard_lab_parity.cjs`; `OMNI_SHEPARD_LAB.md` "Round 3".
- Promoted: no

### L-STU-18 — Graduating a lab pick: route it through the asset's generator, then prove the assets encode the recipe, by pixels AND by the compiled shader
- Lab: Omni Shepard Lab, round 4 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-10
- What happened: the designer pasted back the lab's export (Breathe). Graduation took three things. (1) The asset already had a generator (`author_omni_crystal_triangles.py`) owning the materials, mesh and prefab slots, so the change went there (constants + four opt-in shader dials + a baked per-vertex plate centre), never into the `.mat` files. (2) Proof the shipped assets encode the pick: the PREVIOUS round's page with the exported recipe applied, pixel-diffed against the NEW round's shipped page (≤ 4 px), plus a negative control. (3) Proof the real shader draws it: the shipped HLSL `vert()` compiled with clang (/asset-surgery 4.5c) and run on the real mesh against the lab's JS vertex twin (3e-6), with defaults-are-inert and two negative controls. Shipping the pick also changed SHIPPED, which would have silently restyled every library style stored as a diff from it, so the styles were pinned to a named base (Classic).
- Do instead: before graduating, find the asset's generator (`grep -l <material name> Tools/Build`) and change THAT. Make the export name the generator's constants. Store styles against a named base, never as diffs from SHIPPED. Ship with the three proofs above.
- Evidence: `Tools/Build/omni_shepard_shader_parity.py`, `Tools/Build/omni_shepard_lab_parity.cjs --old-set/--new-style`, `OMNI_SHEPARD_LAB.md` "Round 4".
- Promoted: no

### L-STU-19 — Score an occlusion illusion over many cameras, and sweep its knobs before trusting a mechanism
- Lab: Omni Shepard Lab, round 5 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: the ask was "hide each triangle's birth behind the crystal by turning the whole object to the camera". A camera-averaged score (26 directions, a ray-vs-sphere occlusion test, "seen" = solid within the first 10% of the trip at an unhidden spot) turned it into numbers. Spin alone: 20% of births seen (the four columns 69° from the spin axis, which no rotation can hide); control without spin: 87%. A parameter sweep (tilt, swing strength, swing end, launch height) then showed tilt only moves the problem (20–60%). It also exposed a real bug: the per-column swing measured its turn from the landing direction but applied it after the swirl, so spin + swing read 80% instead of 0%.
- Do instead: for any "the viewer never sees X" effect, write the occlusion score first, average it over cameras, add a control with the mechanism off, and sweep each knob before tuning by eye. A combination that scores WORSE than either part alone is a bug until proven otherwise.
- Evidence: `OMNI_SHEPARD_LAB.md` "Round 5"; `metrics()` / `centreAt()` / `steerRot()` in the page.
- Promoted: no

### L-STU-20 — A derived knob value is a hypothesis: sweep it before you write it into a style
- Lab: Omni Shepard Lab, round 6 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: "Hole fill, spin-hidden" lands each prism at 1 − dwell of its cycle, not at 1. A derivation said the opposing swirl therefore had to shrink from 180° to 180° − 360°·dwell (72°) and that value went into the style, with the derivation as a comment. The same occlusion sweep as L-STU-19 said otherwise: swirl 0 gives 0% births seen and the most arrivals in view (2.0/s). The swap happens behind the crystal, and the spin itself carries the hole round to the front, so the derivation answered the wrong question. Also this round: a new view feature (the Space squares) was built so that frame 0 is byte-identical to the feature off. That made "0 px differ" a meaningful test of the feature's wiring, not only of the pages around it.
- Do instead: when a style's value comes from algebra, run the sweep over it before committing, and delete the derivation comment if the sweep disagrees. Build every new overlay so that its rest state is pixel-identical to it being off, and diff the two.
- Evidence: `OMNI_SHEPARD_LAB.md` "Round 6" Found 1; the `fill-spin` style (`spiral: 0`).
- Promoted: no

### L-STU-21 — When the ask is a relationship ("X follows Y, from any angle"), score the relationship itself, with a control that breaks it
- Lab: Omni Shepard Lab, round 7 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: the designer wanted the far side of the crystal open at the top of its columns and the near side sealed, at any orientation. The existing columns (births seen, arrivals) could not say whether a style did that. A new column, the correlation between how far behind a prism is and how far up its column it sits, did: 0.93 for the stateless "sealed by depth", 0.71 for the one-way ratchet, 0.01 for a control with the depth rule switched off. A second new column, the share of visible travel that goes outward, showed the cost of the stateless rule (50% of visible motion rises, because a rule with no direction lifts prisms going away) and that the ratchet removes it (0%). A sweep of the ratchet's reseat depth found a cliff at about −0.75: 98.8% of reseats seen at −0.7, 0% at −0.8.
- Do instead: translate the designer's sentence into one number that would be high when the sentence is true, add a control that makes the sentence false, and check that the number collapses. Then add the number for the side effect you expect, before tuning by eye. For an "any orientation" claim, average over many cameras and prefer a rule with no clock in it, which holds by construction.
- Evidence: `OMNI_SHEPARD_LAB.md` "Round 7"; `depthMetricsOf()` in the page.
- Promoted: no

### L-STU-22 — A preserved WebGL buffer plus scissored compare cells leaves the gutters stale
- Lab: Omni Shepard Lab, round 7 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: the page creates its context with `preserveDrawingBuffer: true` (for screenshots), and each compare cell clears only inside its own scissor rectangle. The 2-px gutters between cells therefore kept whatever the last full-canvas frame drew, which showed as stray green lines across the grid. It shipped unnoticed for several rounds, because it appeared only after a single view had been drawn first.
- Do instead: clear the whole canvas once before drawing a cell grid, or don't preserve the buffer. Look at a compare-grid screenshot at full size before publishing.
- Evidence: `render()` in `OmniShepardLab.html`.
- Promoted: no

### L-STU-23 — Graduating a STATEFUL look: prove it at the knob level and in a stateless mode, and make the lab's state a function of (dials, clock, camera)
- Lab: Omni Shepard Lab, round 8 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: the pick was the Depth ratchet, whose per-prism state depends on what was drawn before. The L-STU-18 proof (old page + exported recipe vs new page, pixel-diffed) showed up to 4,924 px of difference, shrinking with time (498 px by 7.7 s): the two pages had drawn different frames before the manual clock took over, including free-running frames during load, so their ratchets held different histories. The assets were right. The proof moved to two history-free forms. (1) KNOBS: the old page's Shipped plus the recorded recipe equals the new `SHIPPED` on all 61 knobs, and dropping one recipe key is named. (2) PIXELS in the stateless sibling mode (seal by depth), which exercises everything the assets encode: 0 px on omni and Mass. The lab also now restarts its ratchet on `reset()` and when a dial it reads changes, so later rounds can pixel-compare the ratchet too. The graduation also kept the old look as a PINNED base (`BREATHE_LOOK`, read from its still-authored materials): its styles, riffs and pre-round-8 team saves moved onto it, and the old Breathe draws 0 px against the round-7 Shipped.
- Do instead: before pixel-diffing a graduation, ask whether the look has state. If it does, prove the knobs and a stateless mode, and make the lab's state reset with the clock and with every dial it reads. When the shipped look changes, pin the previous one as a named base first (L-STU-18), then repoint every style, control and saved diff that was relative to SHIPPED.
- Evidence: `OMNI_SHEPARD_LAB.md` "Round 8"; `depthState()`/`reset()` in the page; `Tools/Build/omni_shepard_lab_parity.cjs --new-set`.
- Promoted: no

### L-STU-24 — A lab has one camera; the game has several. A view-dependent look must name WHICH camera it reads
- Lab: Omni Shepard Lab, round 9 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: the Depth ratchet graduated with every proof green: the shader matched the lab to 2e-5, the ratchet tests passed headlessly, and the lab's Shipped equalled the pick pixel for pixel. In game every prism sat sealed and still. `CrystalDepthPrisms` read `Camera.main`, and in Bootstrap that is the main menu's Cinemachine-brain camera under `CameraManager`, which never follows the vessel. The gameplay view is "CM PlayerCam", untagged and driven by `CustomCameraController`. The lab has exactly one camera, so no parity proof could reach that question. The repo already had the right resolve (`WormholeView.ResolveController`).
- Do instead: when a look depends on the viewer, the graduation names the game's viewer explicitly (here `CameraManager.GetActiveController()`'s camera, falling back to `Camera.main` only where none is active), and its checklist entry carries a fly-round step that fails visibly if the resolve is wrong. Grep for how the repo already resolves the gameplay camera before writing `Camera.main`.
- Evidence: `OMNI_SHEPARD_LAB.md` "Round 9" Found 1; `CrystalDepthPrisms.ViewCamera()`.
- Promoted: no

### L-STU-25 — Root each library style on the look it was designed on, and record the view in every decision
- Lab: Omni Shepard Lab, round 9 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-12
- What happened: (1) Round 8 moved `SHIPPED` to the Depth ratchet, and `styleParams` laid every style over `SHIPPED`. So every Classic, Columns and Spin style, none of which set the depth knobs, inherited the ratchet: Classic drew 245/255 off its round-7 self, and no round-8 check compared a library style across the graduation. (2) The designer recorded a pick "that includes the space squares", but `snapshot()` dropped view knobs, so the log could not have shown it.
- Do instead: a style's root is the base it was designed on (Shipped only for `base: 'shipped'`, a pinned look for the rest), and every graduation pixel-diffs a few library styles from each base against the previous round's page (they must be 0 px). A decision snapshot carries the view as well as the look.
- Evidence: `styleParams` and `snapshot()` in the page; `omni_shepard_lab_parity.cjs --old-style X --new-style X`.
- Promoted: no

### L-STU-26 — A fixed-camera scorecard cannot test a stateful promise; score it under the motion the game has
- Lab: Omni Shepard Lab, round 10 · Branch: `cece/determined-ride-rqzaeo` · Date: 2026-10-13
- What happened: since round 9 the crystal holds still, and the scorecard's 26 cameras are fixed, so the depth ratchet never reseated and "births seen 0%" was true by default. A new field scorecard orbits the camera 90° over 20 s. It found the shipped reseat (−0.8) seen 73% of the time on the Mass at 250 u and 8.6–56.5% on the omni. Round 7's "0% at −0.8" had been measured at 34 u on a crystal TURNING 15°/s, a different motion. A sweep under the orbit put the bound at −0.87 (0% at both ranges), which matches the mechanism: seen nearly orthographically, a column top 2 radii out clears the silhouette when √(1−d²) > 0.5.
- Do instead: when a promise depends on history (a ratchet, a hysteresis, a cooldown), the scorecard must move the thing the history is about. Re-measure a promise whenever the motion that produced its number changes, and derive the bound from the mechanism before trusting the sweep.
- Evidence: `fieldMetricsOf` / `fieldScore` in `Docs/Studios/OmniShepardLab.html`; `OMNI_SHEPARD_LAB.md` "Round 10" Found 1–2 and the reseat table.
- Promoted: no

### L-STU-27 — "Do they look alike" is a PIXEL question: correlate rendered images, and measure the noise band first
- Lab: Omni Shepard Lab, round 10 · Branch: `cece/determined-ride-rqzaeo` · Date: 2026-10-13
- What happened: the ask was "a field of Mass crystals reads as one shared animation". Parameter-space metrics (prism heights matched by view direction, or by plate index) stayed at 0.87–0.99 for every variant, because the depth rule makes the same columns from the same direction whatever the knobs. Only a rendered-image metric separated them: each crystal drawn alone from its own camera direction at 48×48, pixels correlated with its nearest neighbours over the pixels either covers. Every crystal alike: 0.61 at 250 u; a random pose each: 0.12. Three seeds put the noise at ±0.02, so differences under ~0.05 were not called.
- Do instead: when the ask is about what the eye sees, score the picture, not the parameters. Measure the metric's spread across seeds before ranking variants by it, and keep the parameter metrics as diagnostics of WHY (here: the depth rule holds; only the pose decorrelates).
- Evidence: `snapLook` / `lookR` in the page; the "After the pick" table, reproduced exactly by `runFieldBatch()` at ship.
- Promoted: no

### L-STU-28 — Before deleting the assets a lab's old looks read, pin their values in the page
- Lab: Omni Shepard Lab, round 10 + follow-ups · Branch: `cece/determined-ride-rqzaeo` · Date: 2026-10-13
- What happened: the Breathe-era tone and rim materials and the Mass crystal's four shell materials were no longer drawn by the game, but the lab's pinned Breathe and Classic looks and its Mass "as shipped" view still read their values through the extractor. Deleting them first would have changed library styles that L-STU-25 says must draw 0 px. The values were copied into a `RETIRED` constant in the page (with the old names aliased to it), the extractor stopped reading the files and emits empty lists, and then the files were deleted. The parity checks that compared against the materials read `RETIRED` instead.
- Do instead: retire in that order (pin, repoint the extractor and parity, delete), and name the pinned block as retired so no one mistakes it for a shipped value.
- Evidence: `RETIRED` in the page; `omni_shepard_lab_assets.py` `"shells": []`; commits for F2/F3 on the branch.
- Promoted: no

### L-STU-29 — A mechanic play-tuned in Unity leaves its lab describing a vessel that no longer ships
- Lab: Stoat Flight Studio · Branch: `cece/fervent-hypatia-rg4why` (the Stoat ported from `claude/peaceful-rubin-hhw49n`) · Date: 2026-10-13
- What happened: after the port, four rounds of play feedback were applied directly to `StoatDipoleConfig` and `StoatDipoleMath`: smaller then bigger poles, a third of the GM, a softened sink, a slam-and-neutralize release, a fly-through zone, and a denser, depth-cued path. None of it went back into the studio, which still models round 15. Nothing fails: the studio reads its own row, so it looks healthy while describing a different vessel.
- Do instead: when a lab's mechanic is tuned in the game rather than in the lab, put a "diverged on <date>, re-read <SO>" note on the CATALOG row in the same branch. The next studio round then starts by re-reading the shipped SO (the lab contract's "a reader of the shipped numbers") instead of its own constants.
- Evidence: CATALOG row; `STOAT_DIPOLE.md` §1 (each number's history); `Docs/UNITY_VERIFICATION_CHECKLIST.md`, the four 2026-10-13 Stoat entries.
- Promoted: no

### L-STU-30 — A predicted path must fly the hull's RESPONSE, not its stick — measure the gap against the real follow
- Lab: Stoat Flight Studio → Unity pathfinder · Branch: `cece/fervent-hypatia-rg4why` · Date: 2026-10-13
- What happened: the studio's `fieldPath`, ported as `StoatDipoleMath.PredictPath`, turns a body at the stick's rate at once and holds its speed. In Unity, `VesselTransformer` turns a COMMAND that the hull slerps after at `RotationFollowRate` (1.5 /s), and eases speed to `CurrentThrottleTarget` at the same rate. Play report: "the vessel doesn't take the path". Measured headless (the shipped math against a frame-stepped model of the transformer), the instant line was 15–73 u off the hull's real path within 3 s of starting a held turn. The `HullResponse` model is within 2 u.
- Do instead: before trusting a lab's predictor in the game, step the GAME's integrator (command + follow + speed ease) beside it and report the worst gap over a few seconds of a held input from rest. Give the lab the same response model, or its line is a promise the game breaks.
- Evidence: `ThePrediction_EasesIntoATurnTheWayTheHullDoes` (StoatDipoleTests); `STOAT_DIPOLE.md` § Time.
- Promoted: no

### L-STU-31 — An art file can hold the END POSE of an animation and no motion: test shape keys against their targets, don't just list actions
- Lab: Omni Combine Lab (`Docs/Studios/OmniCombineLab.html`) · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the designer asked whether `CrystalsRaw.blend` had "an animation where all four elemental crystals combine into one omnicrystal". A survey of actions found nothing: every action on those objects was empty (frames 0-0, no curves). The combine was there all the same, as data. Each elemental crystal carries one unanimated shape key (`Key 1`, value 0), and a KD-tree test of each key against the omni's vertices showed it lands that crystal's plates exactly on its share of the omni. Time and Space land 1:1, Mass 3 prisms per triangle, Charge 5 plates per pentagon, and together they cover all 960 vertices. The vertex counts were the clue: 240 + 480 + 120 + 120 = 960.
- Do instead: when asked "is there an animation of X", also test every shape key and pose against the objects it could be heading for (nearest-vertex distance after the object offsets), and compare vertex counts across objects. Report "the end pose is authored, the motion is not" as its own answer, and bake the targets with a gate that refuses data that does not land.
- Evidence: `Tools/Build/bake_omni_crystal_combine.py` (refuses unless every `Key 1` lands within 1e-3 and all omni vertices are covered); `OMNI_COMBINE_LAB.md` "What the art file has".
- Promoted: no

### L-STU-32 — A metric measured over the phase where the event is the POINT reads the same for every variant
- Lab: Omni Combine Lab · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the first "clearance" column (how close two crystals' centres come in flight, x their combined radii) read 0.09-0.13 for every style, the controls included, because all four crystals converge on one centre and are SUPPOSED to overlap as they merge. Excluding the merge zone (inside 1.5 omni radii) made it discriminate: the art file's straight-line row 0.44, the proposals 1.75-3.29.
- Do instead: before trusting a column, check it varies across the variants. If it doesn't, ask whether it is measuring the moment the design intends; restrict it to where the event is a fault.
- Evidence: `measure()` (`MEET`) in the page; the round-1 scorecard in `OMNI_COMBINE_LAB.md`.
- Promoted: §2.3 (recurred as L-STU-42)

### L-STU-33 — A nearest-vertex "lands" check passes a SWAPPED pair; fit the plate rigidly to catch it
- Lab: Omni Combine Lab round 2 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: round 1 validated every `Key 1` vertex as landing on an omni vertex within 1e-3, and all 960 were covered. Round 2 needed Time to lock in with no morph, so it fitted each rhombus rigidly (Kabsch). One rhombus had a residual of 0.061 against 0.0068 for the rest: its `Key 1` swaps corners 9 and 129 between the inner and outer faces. Both corners still land on AN omni vertex, so the per-vertex check and the coverage check both passed.
- Do instead: when a target is a rigid plate's slot, check the plate's shape against its target as a whole (a rigid fit's residual, or its edge lengths), not each vertex against its nearest neighbour. Fit robustly, then report the outliers by name rather than letting them bend the fit.
- Evidence: `time_rigid()` in `Tools/Build/omni_combine_lab_assets.py` (`artSlips`); the round-2 Found list in `Docs/Studios/OMNI_COMBINE_LAB.md`.
- Promoted: no

### L-STU-34 — An exploded shell has no shared vertices: derive its symmetry axes from the plates' own geometry
- Lab: Omni Combine Lab round 2 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the game's flip wave starts at one of a triacontahedron's 12 five-fold vertices. On the lab's crystals, the plates are separate slabs that don't touch, so finding the vertices where five plates meet gave 60 candidates. Tilting each rhombus's outward axis by the solid's known face-to-vertex angle (atan(1/φ), along its long diagonal) and clustering within 4° gave exactly 12, and rings of 5/5/10/5/5, the same as `FlipWave.Plan`.
- Do instead: on a separated-plate mesh, build axes from per-plate normals and the polyhedron's known angles, then assert the expected count (12, and the ring sizes) before you use them.
- Evidence: `flipRig()` in `Docs/Studios/OmniCombineLab.html`.
- Promoted: no

### L-STU-35 — Graduate a lab by PORTING its pose function and running the port against the page
- Lab: Omni Combine Lab → the Elemental Converter · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the lab's `pose` (flight, four idle effects, three derived morphs, seeded RNG, an integer hash) was ported to pure C# and compiled in .NET beside a driver that loads the page in headless Chromium and asks `__lab.pose` / `omniPose` for the same times, seed and camera. The first run matched to 7e-7 per vertex. Planting a wrong flip salt or a different seeding order failed it by 0.7 and 2.5.
- Do instead: when a lab graduates, keep its pose a PURE function (time, params, camera → vertices), port it with no engine types, and hold the port to the page with a parity harness that compiles the shipped file in place. Port the RNG bit for bit (mulberry32, Math.imul, >>>) and keep the lab's draw ORDER, or one seed means two films.
- Evidence: `Tools/Build/omni_combine_parity/` (`run.sh`, `Driver.cs`, `lab_poses.cjs`); `Assets/_Scripts/Utility/OmniCombineCore.cs`.
- Promoted: §7 (recurred as L-GEN-8)

### L-STU-36 — A lab drawn in a right-handed frame graduates through a REFLECTION: map rotation axes as pseudovectors
- Lab: Omni Combine Lab → the Elemental Converter · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the lab draws Blender's data as (x, z, −y), a rotation. Unity's FBX import is (−x, z, −y), a reflection. Positions map with M, but the per-site twist axes and Time's quaternions must map with det(M)·M = −M, or every rotation runs the wrong way round. That matters on a chiral shape (the Charge petals untwist −13.1°). Axes the runtime derives from geometry through a cross product (the flip wave's) come out right on their own.
- Do instead: in the generator, re-prove every rotation in the frame it writes: a best-fit rotation about each axis against the baked angle, and a rigid replay against the baked pose. Make "map the axis like a position" the negative control.
- Evidence: `Tools/Build/author_omni_combine_asset.py` (`axis_map`, `validate`, `--self-test`).
- Promoted: no

### L-STU-37 — "The shipped mesh does not match the art file" measured under SCALE only is not a finding: fit a rotation and the import's handedness first
- Lab: Omni Combine Lab → the Elemental Converter, revision 2 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: round 1 compared the art file's elemental crystals with the shipped FBXs after a uniform scale and recorded "do NOT match (worst 0.07 of a radius); another export, or rotated". That kept the converter on the art file's Mass slabs and the shell material, and the designer saw a Mass crystal that did not look like the game's. Aligning plate-centre DIRECTIONS under a rotation showed the truth:
  - the game's `MassCrystalPrisms` is the art Mass crystal turned a quarter turn about Z (centres agree to 4e-4 at scale 0.7355);
  - the game's omni triangle prisms are the art omni's under a five-fold turn (2e-5);
  - Charge aligns once Unity's x-flip import is applied, and NOT without it.

  The game mesh then carried the lab's own morph derivation unchanged.
- Do instead: before concluding two meshes differ, solve for the best proper rotation (direction sets: one direction and its nearest neighbour frame the search), under the importer's axis convention AND without it. Report which one fits; a fit that needs the flip is the handedness, not a coincidence. Only then compare shapes.
- Evidence: `align_directions`, `game_mass`, `game_triangles` in `Tools/Build/author_omni_combine_asset.py` (and its `--self-test` negative controls: one prism moved, one shrunk 5%).
- Promoted: no

### L-STU-38 — A lab's stand-in for a shipped effect must not graduate; drive the shipped shader with the lab's motion instead
- Lab: Omni Combine Lab → the Elemental Converter, revision 2 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the lab models the Mass depth prisms with a stateless CPU "seat by depth", which was right for choosing a choreography. Ported as-is, the converter's Mass crystal looked nothing like the game's. The fix kept the lab's MOTION (flight, untwist, wedge morph) on the CPU and handed the EFFECT to the game's own `OmniDepthPrismShader` + `DepthRatchet`. Each plate's column centre (TEXCOORD2) moves with the morph, and the shader's shape uniforms blend between the two materials, so the hand-off to the omni's own depth prisms is exact by construction.
- Do instead: when a lab approximates a shipped effect, mark it in the lab's not-modelled list, and on graduation plug the lab's geometry into the shipped shader/driver rather than porting the approximation. Make the core's stand-in a switch (`ShaderDepth`) so lab parity still holds.
- Evidence: `OmniCombinePlayer.WriteMass` / `DepthColumns` / `DepthShape`; `OmniCombineParams.ShaderDepth`; the parity driver's "ShaderDepth ignores the camera" check.
- Promoted: no

### L-STU-39 — A graduated lab shown in a MENU inherits the menu's clock and the stage's float precision; neither shows up in the lab
- Lab: Omni Combine Lab → the Elemental Converter, revision 3 · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the converter's motion matched the lab to 7e-7, and on screen the discharge "was not working" and the whole crystal shimmered.
  - **The discharge:** `ScreenSwitcher` runs every menu screen but HOME at `Time.timeScale = 0`. The lab's CPU effects had been ported onto unscaled time, but the game's discharge SHADERS read `_Time.y`, so every bolt stood still.
  - **The shimmer:** the off-screen stage sat at y −150000, where a float resolves 1/64 of a unit, on a crystal 4 units across. Every vertex snapped as the camera circled.
- Do instead: before shipping a lab's motion into a menu, list every clock the drawn result reads (CPU and SHADER: `_Time`, `Time.time`, animators, particles) and give each one the display's own unscaled time. A default-off property such as `_ArcClock` leaves the game untouched. Put an off-screen stage only as far out as the far clip needs, and make it large. Its jitter is |position| × 2^-23 against the subject's size.
- Evidence: `_ArcClock` in `ChargeCrystal.shader` / `OmniChargeEdgesShader.shader` (the latter retired 2026-10-13; its successor `CrystalChargeArcsShader` draws CPU geometry on the player's clock, so it needs none); `ElementalConverterView.StageOrigin`; `OmniCombineConfigSO.omniWorldRadius`.
- Promoted: no

### L-STU-40 — A graduated lab that reads ANOTHER generator's mesh breaks when that generator bakes something new into it
- Lab: Omni Combine Lab → the Elemental Converter, `/ship` merge · Branch: `cece/eager-rubin-km7qyn` · Date: 2026-10-13
- What happened: the converter's generator fits the game's `MassCrystalPrisms` / `OmniCrystalTriangles` onto the art file's plates. A parallel branch (Omni Shepard round 10) baked the prisms' thickness into those meshes and set the materials to `_Thickness 1`. The merge was clean; the generator's `--check` failed with "misses the omni's triangles by 5.33e-01". Had it not been gated, the converter would have drawn the Mass prisms thin, then double-thick at hand-off.
- Do instead: when a lab's generator reads a mesh another generator owns, IMPORT that owner's constants (`author_omni_crystal_triangles.DEPTH_THICKNESS`, `author_mass_crystal_look.MASS_DEPTH_THICKNESS`); never copy the numbers. Undo the owner's transform exactly, and gate the inverse against something it cannot fake. Here the top face is fixed, so the mesh's highest corner must sit at the material's `_TopHeight`, and the thin triangles must land on the art omni within 1e-4. Add a negative control for a wrong constant. Re-run every such generator's `--check` after each merge of the base: the break shows there, not as a conflict.
- Evidence: `read_prisms()` in `Tools/Build/author_omni_combine_asset.py` and its "wrong baked thickness is refused" self-test; `depth.massThickness` / `depth.omniThickness`; `OmniCombinePlayer.DepthShape.Of(..., baked)`. The recovered thin Mass matched the pre-bake data to 1e-5.
- Promoted: no

### L-STU-41 — A brief names a cause; measure the mechanism before building the cure
- Lab: Omni Motion Lab (`Docs/Studios/OmniMotionLab.html`) · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: the brief was to time the squares' moves and the rhombi's flips so they never move at once, "so their movements never clip". The lab built that scheduler. It also added a convex-plate interpenetration test that records what was moving on each clipping frame (`clipTimeline`). The overlap fell from 87% to 0, but visible clips only fell from 66.7% to 28.6% of frames, and every remaining clip happened with the rhombi STILL. A square turning round its pentagon cuts through the still rhombus beside it, on the art's own keys and on a rigid turn alike. What reached 0 was a rigid turn that hops off the shell. A lift sweep found the edge: 0.14 R on the pentagon turn, 2.2× the art's lift on the triangle turn.
- Do instead: when a brief says "do X so that Y", build Y's METRIC as well as X. Have it record the state that was active when Y fails, so the lab can say whether X is the cause. Ship the requested change AND the measured fix as separate styles, so the designer sees both.
- Evidence: `runFullBatch()` rows "Proposal: the brief, as keys" (overlap 0, clips 28.6) vs "Proposal: rigid arcs that hop clear" (0, 0); `clipTimeline`; `OMNI_MOTION_LAB.md` Found 1.
- Promoted: no

### L-STU-42 — "Make it more" can be capped by a rule, not by the knob the designer named; measure what caps it
- Lab: Omni Motion Lab round 3 (`Docs/Studios/OmniMotionLab.html`) · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: "more Mass prisms out, and further out". The obvious knob, column height, moved the visible count from 2.2 to at most about 2.4. The ratchet was the cap: it holds a prism moving away sealed until the deep reseat. Taller columns made it worse, because they need a deeper reseat to stay hidden. Changing the RULE fixed it: a prism may rise whenever its column top is hidden. Visible height went 1.5 → 7.4 plate radii, with zero rises seen, and a planted "rise anywhere" control reads 100% so the check is live.
- Do instead: before sweeping the named knob, add the metric for what the designer wants to SEE (here, height seen = how far × how many). Sweep the knob, and if it plateaus, find the invariant that caps it. A new rule needs its own never-seen check and a planted control that breaks it.
- Evidence: `OMNI_MOTION_LAB.md` round 3 sweep; `depthRise` in `depthStep`; styles "Round 3" and "Control: rise anywhere".
- Promoted: no

---

### L-STU-41 — When a constraint, not a dial, sets the value, no tuning of the dial fixes it: run the tuning-only variant as a control
- Lab: Thresher Plant Lab (`Docs/Studios/ThresherPlantLab.html`) · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: the ask was "a quick deceleration instead of an instant stop". The shipped plant already HAS a deceleration (a 7/s skid for 0.3 s), but the C# reference trace showed the ball still doing 62.8 u/s when it was pinned: the taut rope re-imposes the ship's speed every frame, so the brake barely acts. A "Longer skid" variant (`skid` 12, `skidSeconds` 0.5, shipped dials only) moved the ball-stop column 24.4 → 23.4 u/s and cost 0.2 s of catch time. The fix had to move the ball kinematically and let the rope pay out (`bkMode`).
- Do instead: when the request is "make X gentler" and X already has a gentleness dial, put a tuning-only variant in the scorecard first. If it doesn't move the column, find the constraint that overrides the dial before designing approaches.
- Evidence: `THRESHER_PLANT_LAB.md` round 1 scorecard (Longer skid row); `Tools/Build/thresher_plant_lab/trace.sh` reference trace.
- Promoted: no

### L-STU-42 — Probe the worst run of a column frame by frame before trusting it; three of round 1's columns were the yardstick, not the approach
- Lab: Thresher Plant Lab · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: three first-draft columns misled. (1) The catch window overlapped the scripted wind-in, so the No-plant control read 14 u/s of "ball stop" and the wind-in's 1 600 °/s hull whip masked the hook (L-STU-32 recurring: a window that spans a second event measures that event). (2) The first "rope give" spring could pull the ball but not brake it, so the ball overshot the ship and the hard rope snapped it at 596 u/s, and the approach read WORSE than shipped. (3) The lab's own camera limiter chattered at its cap (bang-bang around zero error), so the camera column read exactly 1 500 for every run. Each was found with one probe that runs every seed, finds the frame where the column peaks, and prints ±6 frames around it.
- Do instead: before quoting a scorecard, open the worst run of each column at its peak frame. A column that equals a cap or limit on every run, or a control that is not ~0, is a yardstick bug until shown otherwise. Give the page a `runRows(overrides, seed, kind)` hook so the probe needs no copy of the model.
- Evidence: `window.__lab.runRows` in the page; the three fixes in `THRESHER_PLANT_LAB.md` "Checked".
- Promoted: §2.3 (with L-STU-32, now recurred in a second lab)

### L-STU-43 — A frame-rate change can decide which frame a state cut lands on, and so whether a jolt exists
- Lab: Thresher Plant Lab · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: the shipped spectate camera eases in from a pose captured at the cut, dropping the camera's turn: 14 400 °/s² at 60 Hz on every run. At 30 Hz the same column read 622 °/s², because the cut landed on the frame before the chase camera had started turning, so there was nothing to drop. Every other jolt column kept its ordering. (A recurrence of L-ECO-4, "a fixed timestep is a sample".)
- Do instead: re-run the scorecard at a second frame rate and report any column whose ORDER changes. A one-frame race between two state changes is a finding in its own right; here it argued for carrying the camera's turn into the entry.
- Evidence: `THRESHER_PLANT_LAB.md` "Frame rate"; `runBatch({ dt: 1/30 })`.
- Promoted: no (L-ECO-4 is already §8)

### L-STU-44 — Every handoff between two motion models is a one-frame jolt until it carries position, rate AND force across
- Lab: Thresher Plant Lab round 2 (`Docs/Studios/ThresherPlantLab.html`) · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: the analog plant chains four models: towing on an elastic chain, a braking anchor, a kinematic hook, and an elastic orbit. Five of round 2's eight findings were seams between them, each found by probing a column's worst run frame by frame. A rigid chain yanked a ship flying away from its anchor (2 100 °/s). The hook restarted the turn from zero (−125 → 0 °/s). The hook's stretch was reeled back by the rigid chain in two frames (87 000 °/s²). A slack chain's outward pull switched on at once. The orbit direction was read from a near-zero sideways velocity, so it flipped mid-hook. Each fix made the next model start from the previous one's state: start the hook at the full pull, carry the incoming turn rate, start the elastic state at the ship's real stretch and radial speed, and ease the new force in.
- Do instead: when a mechanic switches models, list for every handoff what the second model needs (position, velocity, turn rate, force) and seed it from the first. Probe the handoff frame of every pilot style, not just the default.
- Evidence: `THRESHER_PLANT_LAB.md` round 2 "Found" 1–5; `stepPivot` analog branches, `anBeginStop`.
- Promoted: no

### L-STU-45 — A stiff spring in a lab is a frame-rate bug: sub-step it and say so in the graduation notes
- Lab: Thresher Plant Lab round 2 · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: the rigid end of the elastic chain is a 30 rad/s spring. With semi-implicit Euler at 30 fps (ω·dt = 1, plus damping) it sat at the edge of stability. The 30 Hz scorecard read 65 000 °/s² on the wind-in, against 10 000 at 60 Hz. Sub-stepping (ω·h ≤ 0.15) made the two rates agree (8 900 vs 9 300). Only the second-frame-rate run showed it; the 60 Hz scorecard looked fine.
- Do instead: whenever a lab adds a spring, give it sub-steps from the start, run the scorecard at 30 Hz as well as 60 Hz, and write "must be sub-stepped or integrated implicitly" into the graduation notes, because the game's frame time varies. (Recurrence of L-ECO-4 / L-STU-43.)
- Evidence: `stepAnalog` and the analog `stepPivot` branch (`nSub`); round 2 "Found" 8.
- Promoted: no (§8's fixed-timestep trap covers it)


### L-STU-46 — A float32 game clock ends a curve a frame before (or after) a float64 lab; end curves at 1 − ε, and expect a peak column to move
- Lab: Thresher Plant Lab graduation (`ThresherChainSolver`, `parity.cjs`) · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: the analog hook ends at `u = hookT / hookSeconds >= 1`. Adding 1/60 thirty times is 0.49999997 in C# float32 and 0.5000000000000001 in JS float64, so the game's hook ran one frame longer than the lab's, and parity diverged from 1e-3 u to a mode mismatch within 50 frames. Ending every curve at `u >= 1 − 1e-4` on both sides fixed parity, and it moved the lab's analog catch column ~20% (2 620 → 3 183 °/s²), because a peak column reads the one frame where two models hand over.
- Do instead: when a lab will be ported to a float32 engine, end time-driven curves at `1 − ε` from the start. When a one-frame shift moves a scorecard column, treat that as a sign that the column is reading a hand-over seam, and say so rather than quoting the better number.
- Evidence: `ThresherChainSolver.EndOfCurve`, `END_OF_CURVE` in the page; `THRESHER_PLANT_LAB.md` "Graduated".
- Promoted: no

### L-STU-47 — Mutate the code a new test guards before trusting it: one of ten graduation tests passed with its rule deleted
- Lab: Thresher Plant Lab graduation (`ThresherAnalogTriggerTests`) · Branch: `cece/zealous-johnson-gopp6t` · Date: 2026-10-13
- What happened: all ten analog tests passed on their first run. Planting two mutations showed that the full-pull test caught a near-instant stop, but the ambiguous-plant test still passed with the turn-direction rule removed: its one scripted flight never produced the case the rule exists for (a sideways velocity that disagrees with the turn). A sweep over 80 turn patterns produced it (`(-90,-60,20)`), and with the sweep the rule's deletion fails.
- Do instead: for every test written for a lab finding, delete or break the line it guards and run it once. If it still passes, the scenario does not reach the case; sweep the scenario until it does, and assert that the sweep produced enough qualifying cases.
- Evidence: `AmbiguousPlant_OrbitsInTheFlightPlane_TheWayTheShipIsTurning` (the sweep and its `ambiguous > 10` guard).
- Promoted: no

### L-STU-58 — A "worst error" over zero matched samples is 0: gate every max-error check on its sample count
- Lab: Omni Motion Lab round 5 graduation · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: the graduation harness checked that the triangle turn with the art file's own lift and side step passes through the art's Step1 key, as the worst distance over matched vertices. The first run printed PASS with worst 0. No vertex had matched: the anchor set is mirror-symmetric, so the matcher tried both x signs and the hit count never reached a full match, leaving the max at its initial 0. Breaking on the first FULL match (x as written, the same as the baker) gave the real result, 1.97e-6 R.
- Do instead: report the sample count beside every max/mean error, and fail the check when it is zero or short of the expected count. A tolerance check that can pass on an empty set is not a check.
- Evidence: `Tools/Build/omni_turns_harness/Program.cs` ("anchors with x 1: 1440 body vertices matched"); the round-5 graduation section of `Docs/Studios/OMNI_MOTION_LAB.md`.
- Promoted: no

### L-STU-59 — A trail that fades per point from its STRIKE steps at every hold; fade from when the bolt LEFT the point
- Lab: Omni Motion Lab round 5, after graduation · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: the seam walk struck each seam in 0.07 s and then HELD 0.1–0.28 s before the next. Each point faded
  from when it was struck, so at every junction the brightness stepped by the hold's worth of fade (about 0 → 0.3).
  When the old seam died, the trail's tail began at a third of its brightness: a hard, popping end. The designer saw it
  as "discrete units separating". The harness's frame-to-frame check found it only once it measured what is visible.
  The first measurements blamed resampling and pinning.
- Do instead: fade each point from when the bolt LEFT it (`min(now, start + (leaves - start) * place)`), so consecutive
  points across a hold are the same moment. Check the trail's tail brightness and frame-to-frame motion over visible
  points, and keep the old rule as the negative control.
- Evidence: `ChargeArcs.SeamWalk` (`leftAt`); `Tools/Build/charge_arcs_harness` ("the seam trail's tail always fades
  to nothing", 0.014 vs 0.327 with the old rule).

### L-STU-48 — A harder course measures the AI's failures unless the AI learns the harder skill
- Lab: Stoat Flight Studio sim lab, round 14 · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09
- What happened: the new intensity ladder grades turning (1 plain circle → 4 side-on and dive rings). The
  round-12 AI was caught on only 3 of 15 slings at intensity 4, for two reasons: it steered for the ring
  before the hole held it, and the pair is always laid along the wings, so a climb could not use it. The
  ladder then measured an AI that couldn't fly it, not the course. Three changes fixed it:
  - **bank to sling:** roll until the turn lies off one wing;
  - **pass on the circle:** aim at the hold radius until caught;
  - **release at the best heading:** let go when the nose stops closing on the ring.

  Catches went to 19 of 19, and grip turn per race rose 766° → 1,382° → 2,420° → 2,209° across the levels.
- Do instead: when a lab adds difficulty, check the AI's own success rate (here, catches per sling) per
  level before reading any other column. Then teach the AI the skill the level demands.
- Evidence: `aiInput` in `StoatFlightStudio.html` (the `bank` / `lay` phases, `aiAim`, `ai.prevOff`);
  `STOAT_SIM_LAB_PLAN.md` §2 "The intensity ladder and the round-14 AI".
- Promoted: no

### L-STU-49 — Improving the AI moves every scorecard column; the rookie must keep the rookie's mistakes
- Lab: Stoat Flight Studio sim lab, round 14 · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09
- What happened: the smarter catch logic also reached the rookie (`aiNoise`), so every style's rookie catch
  rate became 100% and Anchor lost its column. Comet, now catching reliably, also overtook Flare on top
  speed. The fix:
  - the rookie keeps steering for the ring before the catch (its classic miss);
  - Flare's buried boost rose from +2.1 to +3.1 (slider ceiling 3 → 5).
- Do instead: re-run the whole scorecard after ANY change to the AI, not only the column it was meant for.
  Model skill differences as specific mistakes the skilled pilot does not make.
- Evidence: round-14 scorecard in `STOAT_SIM_LAB_PLAN.md`; `aiInput` `P.aiNoise < 0.5` gate.
- Promoted: no

### L-STU-50 — A lab's vessel can be in the game and still not run in Amoebius: compile the engine before you call it portable
- Lab: Stoat (game side) and Vessel Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09
- What happened: the game on this branch did not compile in Amoebius at all. Its black-hole code used:
  - `UnityEngine.Jobs` transform jobs;
  - URP's script-injected render-graph pass and `CommandBuffer`;
  - `typeof(SerializeField)` (the engine called it `SerializeFieldAttribute`);
  - a fully qualified `InputSystem.Controls.ButtonControl` (the engine had it one namespace up);
  - and the engine had no GLSL port of a new shader-graph custom function.

  All of it was filled as engine API. Engine 1,691/1,691, ported game tests 352/352.
- Do instead: before saying "test it in Amoebius", run `dotnet build Port/src/CosmicShore.Player` on the
  branch. A Unity-only API in new game code fails there first. Fill gaps in the engine (`Port/src`),
  never in `Assets/`.
- Evidence: commit `84898d17a`; `Port/src/CosmicShore.Engine/Jobs/TransformAccess.cs`, `Rendering/RenderGraph.cs`.
- Promoted: no

### L-STU-51 — One-click "play the real thing" from a studio: a catalog field plus a player flag
- Lab: Vessel Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09
- What happened: Amoebius's STUDIOS page gained two buttons:
  - **OPEN IN AMOEBIUS**: Edge or Chrome app mode, opening the page as `#prisma` with its own window profile.
  - **PLAY IN ENGINE**: `studios.json` `engineMode`. The player's `--arcade MODE` waits for `Menu_Main`,
    opens that card and presses Start (`ArcadeAutoStart`, through the existing `arcade` input verb).

  Headless, `--arcade Slingshot` reached the race at frame 258. Entering the race scene directly (`--scene`)
  does NOT work: there is no Bootstrap camera rig or app services.
- Do instead: reach a game mode through the menu path players use, driven by the same verbs the control
  port uses. Never boot a gameplay scene directly to "save time". Put the per-studio fact (which mode) in
  the catalog, not in launcher code.
- Evidence: commit `30a890606`; `StudioCatalogTests.TheShippedCatalogsEngineModesAreGameModes`.
- Promoted: no

### L-STU-52 — Two people on one studio: the artifact refreshes itself from GitHub
- Lab: Vessel Studio · Branch: `claude/peaceful-rubin-hhw49n` · Date: 2026-10-09
- What happened: two designers pushed studio rounds from two sessions, and the published artifact fell
  behind within the hour. A Sync panel now does the loop inside the artifact:
  - it reads GitHub through the viewer's own connector (`mcp`);
  - it republishes the hub with the files form of `artifact.publish`;
  - it merges branches through a PR, then asks whether to delete the merged branch;
  - it keeps a shared `decisions` collection (`db`).
- Do instead: give every shared studio artifact the panel (`build_artifact.py`), share it with edit access,
  and record decisions in it, not in chat. Remember the connector cannot delete branches,
  `get_file_contents` returns the file in a `resource` block, and a files publish leaves the publishing
  view on the old files until it reloads.
- Evidence: `.claude/skills/vessel-studio/` (SKILL.md §4-§6, `build_artifact.py --self-test`);
  `Docs/Studios/VesselStudio/SYNC_PANEL.md`.
- Promoted: yes, into the `/vessel-studio` skill

### L-STU-53 — Draw a predicted path on the HUD, after the lens, or the black hole bends and doubles it
- Lab: Stoat Flight Studio, round 15 (field trajectory) · Branch: `cece/magical-carson-9bdq8z` · Date: 2026-10-09
- What happened: the first trajectory line was a `THREE.Line` in the scene. The lens post-pass bent it like any light, so near the pair it showed up twice, once on each side of the hole (the same thin-lens map that doubles the stars). That reads as two trajectories. A prediction is information, not light.
- Do instead: project the points with the camera and stroke them on the 2D HUD canvas after the post-pass. That is also thicker than WebGL's 1 px lines.
- Evidence: `drawFieldPath` (HUD); screenshots before and after in the round-15 session.
- Promoted: §8

### L-STU-54 — Check a predicted path against the flight it predicts; three bugs hid in "the line looks right"
- Lab: Stoat Flight Studio, round 15 · Branch: `cece/magical-carson-9bdq8z` · Date: 2026-10-09
- What happened: a parity probe laid a pair, set the hull on a looping heading, flew it with no input and measured each flown point's distance to the drawn path. It found three bugs:
  - the prediction held the engine at its current speed while the engine was still spooling up;
  - it STARTED the path 8 u ahead of the nose instead of flying from the hull and DRAWING from 8 u ahead (near a hole, a path 8 u further on is a different path);
  - it tested the horizon every 3 u, so a 3 u horizon slipped between two points while the flight's 4 ms step fell in.

  After the fixes, paths clear of the horizon agree to 1.7–3.1 u. Whirls that graze the horizon still diverge by 11–78 u, and the shipped 4 ms integrator diverges as much, so that part is the chaotic orbit, not a bug.
- Do instead: share one step function between the flight and the prediction, and run the parity probe before trusting the colours. Also run it with the prediction forced onto the flight's own step: when that diverges just as much, the divergence is the system, not the integrator.
- Evidence: `fieldSubstep` / `fieldPath` (`stepBy` checks the horizon every substep; the pre-roll to `ftNose`); `Pv.ftExact`; README round 15 "Checked".
- Promoted: no

### L-STU-55 — A reward computed from a prediction that includes the reward can cancel itself
- Lab: Stoat Flight Studio, round 15 · Branch: `cece/magical-carson-9bdq8z` · Date: 2026-10-09
- What happened: the field trajectory boosts you 3× while the predicted path loops. Flown at the boosted speed (the literal request), the boost straightens the loop that earned it within about 2 frames. The lime flickered: lime 3.8% of the race against 14% with the path at engine speed, with the most loop onsets per minute (37.8). The same literal rule also made a held turn alone a permanent 3×, because a full-stick circle (about 180 u) is shorter than the 600 u path.
- Do instead: when a mechanic's trigger reads state the mechanic changes, measure it both ways (here `ftBoostInPath` 0/1) and put a pure-input control row in the scorecard ("steering only, not in path" must equal No sling). Leave the reading to the designer as **Decision needed**.
- Evidence: README round 15 field scorecard and "Decision needed".
- Promoted: no
- Superseded (same day): the designer chose a boost that cannot touch the path (L-STU-56), so the 0/1 row is gone.

### L-STU-56 — To make a boost that never changes the predicted path, warp the vessel's own clock
- Lab: Stoat Flight Studio, round 15 · Branch: `cece/magical-carson-9bdq8z` · Date: 2026-10-09
- What happened: the designer wanted the field trajectory's 3× to move the Stoat along the drawn path sooner without bending it, so the line "always shows where you go if you hold your inputs". Adding speed changes a field path, because the bend per unit length is acceleration over speed squared. Running the hull's whole flight (steering, engine spool, momentum fade, field substeps) at `ftMul` × the clock (`kdt` in `stepFly`) keeps the curve and only reparametrises time. The race clock, the pair's life and the squeeze stay on real time. Measured: a looping heading flown with no input at boost 1 and boost 3 stays 4.5 u and 3.6 u from the drawn loop, and the boosted run is 2.9× quicker. Outside the mode the warp is 1, and the dipole scorecard stays byte-identical.
- Do instead: when a reward must not change what a prediction shows, scale the time the prediction is parametrised by, not a state the prediction reads. Then prove it with the same heading flown at both settings against the drawn path.
- Evidence: `stepFly` (`inField`, `warp`, `kdt`); `stepFieldFlight(kdt, dt)`; README round 15 "Checked".
- Promoted: no

### L-STU-57 — A prediction past a teleport is only as good as the exit's sensitivity; measure where the flight leaves the line
- Lab: Stoat Flight Studio, round 15 (dipole field) · Branch: `cece/magical-carson-9bdq8z` · Date: 2026-10-09
- What happened: the flight followed the drawn line within about 1.5 u all the way into the black hole and came out within 1 u of the drawn exit. It then left the line by up to 76 u over the next 330 u. The wormhole drops you 1.05 horizons from the white hole, inside a push softened over 2 horizons, so a 1 u difference at entry became about 13° of heading on the way out. Running the prediction on the flight's own 4 ms step did not remove it, and the boost did not change it.
- Do instead: in a parity probe, log the deviation over flown distance, not only its maximum. The trace shows exactly where the flight leaves the line (here, the frame after the jump). A teleport that lands inside a strong force is a sensitivity to report or design out (a wider exit gap), not an integrator to tune.
- Evidence: README round 15 "Checked" (through the wormhole) and "Found".
- Promoted: no

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

### L-STU-60 — Before inventing a path, measure where the existing one goes wrong, and ask whether topology allows "a bit"
- Lab: Omni Combine Lab revisions 6–7 (the converter) · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: "Time came in from inside" got answered with a new per-plate path that rose to a shell, went round
  and dropped in. The designer called it heavily embellished and asked for the lab's morphs plus "a bit of extra
  spacing". Measuring the lab's morph first showed the real fault: its shell is 90% formed while the crystal is still
  0.3–0.5 R out, so the finish is a finished shell SLIDING home. Its leading rhombi rise out of their holes, its sides
  slide along at slot radius, and only its trailing side comes in from outside. A body arriving from one side must
  bring its leading side through or round the other body. No small spacing can avoid that, and five cheaper variants
  were measured and failed:
  - spacing alone;
  - retiming the ease;
  - a radial floor, which whips plates at 36 R/s;
  - re-anchoring plates to their slots, which moves them 1.5–3 R;
  - containment, which swells the shell to 2–3x.
  The conflict was put to the designer with numbers. The answer was to keep the morphs and choose WHERE the crossing
  happens: the flight turns so each crystal arrives from the camera's side, and the crossing goes behind the omni.
- Do instead:
  - Measure the existing motion against the complaint in the viewer's terms first. Here that is "a rhombus under a
    camera-facing hole is inside the omni, late in its morph".
  - Before building a new path, check whether "a bit" can satisfy the ask at all. If the geometry forbids it, put the
    conflict to the designer with measured options, rather than shipping an embellishment that looks like an answer.
  - When something unavoidable can't be made smaller, the viewer's camera is a design input. Put the crossing where
    the body itself hides it. This is the same move as the Mass "rise while hidden".
- Evidence: `OmniCombineCore.FaceCamera`, `SocketSpacing`; `Tools/Build/converter_round5_harness` (camera-facing
  rhombi inside 0.5 / 6.5 / 0.6% of late frames, against 24.6 / 19.0 / 33.0% arriving straight in). See L-STU-61: that
  measure was a proxy, and the true one does not back the occlusion claim.
- Promoted: no

### L-STU-61 — "Occluded" means a ray from the camera hits something drawn first; a facing test is not that
- Lab: Omni Combine Lab revision 7 → round 8 (the converter) · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: revision 7 shipped "arrive from the camera's side, so the clipping happens behind the omni". Its
  evidence was a proxy, "a rhombus whose hole faces the camera is inside the omni", which fell from 33% to 0.6%. The
  designer then asked for "the same treatment" on Charge and Mass, which already had it. Ray-casting from the
  converter's camera against every triangle drawn showed visible plate-through-plate intersections at about 15% on
  every approach: straight in, from the camera's side, along the line of sight, from above. From behind was best.
  The clipping is the crystal entering the omni and growing inside it. The approach direction only trades which half
  you see. A sparse structure (Charge lands on Space alone) has nothing to hide it behind.
- Do instead: when the claim is "occluded" or "hidden", measure it with occlusion. Cast rays from the real camera
  against the geometry actually drawn, at the moment it is drawn, and count the events the viewer would call the
  fault: here, triangle-triangle intersections. Keep any facing or hemisphere test only as a fast pre-filter, and say
  in the doc which one produced the number.
- Evidence: the round-8 scratch ray-cast rig (`OMNI_COMBINE_LAB.md` revision 7, "Correction, round 8").
- Promoted: no

### L-STU-62 — When the designer's eye and your measure disagree, ship their ask with the dial, and publish the disagreement
- Lab: Omni Combine Lab revision 7, round 9 (the converter) · Branch: `cece/sharp-fermi-yw38ky` · Date: 2026-10-13
- What happened: the designer saw Mass "come in from the top", with visible clipping from the side, and asked
  whether there was a 90° error. There was no error in azimuth: the approach matched only the camera's compass
  direction, not its height. Matching the height too fixed what they named, with arrival 0.2° off the camera's.
  But the triangle ray-cast scored a line-of-sight approach WORSE on the silhouette (Mass 98 against 31 rim
  intersections a second from overhead). The measure counts intersection points; it cannot see a body sinking
  through another body's outline, which is what they were describing.
- Do instead: build what the designer asked, and expose the contested quantity as one authored dial
  (`approachAboveCameraDegrees`, default the ask). Put the measure's opposing numbers in the doc and the
  verification checklist, so the next look in the editor settles it. Don't override the eye with a metric
  that was never validated against it, and don't hide a metric that disagrees.
- Evidence: `OmniCombineParams.ApproachAboveCamera`; `OMNI_COMBINE_LAB.md` revision 7, round 9.
- Promoted: no

### L-STU-63 — Run the designer's literal value as a control: it can trade the fault they named for a new one
- Lab: Omni Combine Lab revision 8 (the converter) · Branch: `cece/beautiful-goodall-8cenn3` · Date: 2026-10-14
- What happened: the designer asked for the Mass prisms to fly in "at maximum column height" so they socket instead of
  clipping. Taken literally, the raised prisms stand 2.5 R out in every direction as the crystal nears the centre, and
  the Time crystal waits 2.7–3.3 R out. Measured on the real prism bodies, the top of the column reaches into the Time
  crystal (0.00 R). The first version of the check also flagged prisms passing through the omni mid-flight. That was a
  proxy problem: it lumped "landing beside my hole" together with "bound for the far side, passing through".
- Do instead: build the ask as a dial, and run the literal value through the same harness. Here that is `massRaise`,
  with 1 meaning "maximum". If the literal value only trades the fault the designer named for a new one (a
  body-on-body contact, not a contested metric as in L-STU-62), ship the highest value that keeps the ask's purpose.
  Keep the literal value as a negative control, and give the designer the table. Split a fault metric by cause
  before gating on it.
- Evidence: `Tools/Build/converter_round5_harness` section 9; `OMNI_COMBINE_LAB.md` revision 8.
- Promoted: no

### L-STU-64 — A studio built from a design doc beside a game build from the same doc: add the game as a selectable "ships" set
- Lab: Brittle Star Studio round 2 · Branch: `cece/gallant-lamport-sc3khe` (studio from `cece/brave-galileo-d95fpj`) · Date: 2026-10-14
- What happened: two sessions built the Brittle Star on one day from one Notion document without seeing each other, a
  design studio on one branch and the Unity vessel on another. The designer then made a dozen decisions in the Unity
  session (x6 Speed, five auto-firing arms, a trail per ring corner, a nucleus-wide ring explosion, Creation the slowest),
  so the studio modelled a vessel that no longer existed. A rewrite would have thrown away round 1's scorecard and the
  design-doc comparison the studio had earned.
- Do instead: port the game's rules line for line behind ONE switch (`SHIP.unity`, a new Flight-numbers set made the
  default, the saved-settings key bumped so every tester gets it), read every number from a `SHIPPED` block keyed by the
  config SO's own field names, and keep the old sets selectable. Then a `Tools/Build/studio_to_unity/<vessel>.json` map
  makes `studio_to_unity.py --check` a drift gate between the page and the asset; negative-control it by changing one
  number. Check the port against the C# with a hook-driven rules script (speeds, the press-again Cruise, the arms meeting
  exactly at the focus, the disk sparing a prism 5 u off its plane) before trusting any scorecard it produces.
- Evidence: `Docs/Studios/BRITTLE_STAR.md` §5.2; `Tools/Build/studio_to_unity/brittlestar.json` (35 mapped, a changed
  `fireRate` reports DRIFT and exits 1).
- Promoted: no

### L-STU-65 — A studio pilot tuned at one speed orbits at another; re-score every level whenever the hull's numbers move
- Lab: Brittle Star Studio round 2 · Branch: `cece/gallant-lamport-sc3khe` · Date: 2026-10-14
- What happened: round 1's lab-only pilot rode Speed mode onto a crystal at x4 (110 u/s). On the game's x6 (297 u/s at
  Time 3) the 25 u pickup sat inside its turn radius, so every Hard Derby I4 run circled a crystal for 300 s and the
  scorecard read DNF. A first fix (drop to Cruise inside the stopping distance) flickered: the slowed hull's stopping
  distance shrank, the rule asked for Speed again, and it orbited anyway. A second (back off and re-line a sealed wall)
  made every seed worse.
- Do instead: trace one failing seed's position, speed, mode and state every couple of seconds before changing the
  pilot; the samples showed the mode flipping once a second. An approach rule needs hysteresis (here: stay in Cruise
  within max(250 u, the stopping distance)). Re-score every level and style after ANY change to the hull's numbers, and
  report where Hard is still not the fastest rather than tuning until it is (D24).
- Evidence: `Docs/Studios/BRITTLE_STAR.md` §5.2 (finding 6); `/vessel-studio` §7 (the same trap, for studio pilots).
- Promoted: no

### L-STU-66 — Run the scorecard tool a handoff names before quoting its numbers; this one could not run as committed
- Lab: Brittle Star Studio · Branch: `cece/gallant-lamport-sc3khe` (tool from `cece/brave-galileo-d95fpj`) · Date: 2026-10-14
- What happened: the handoff cited `Docs/Studios/BrittleStar/brittlestar_score.cjs` as the source of round 1's scorecard.
  As committed it used `fs`, `path` and `chromium` without requiring them, so it crashed on its first line: the numbers
  had come from an inline run, not the file a reader is told to re-run.
- Do instead: execute every measuring tool a doc or handoff names, from the committed file, before quoting or
  extending its numbers; fix and say so when it does not run. A committed measurement tool is only evidence once
  someone other than its author has run it.
- Evidence: commit "merge the Brittle Star research and studio" (the `require` lines added).
- Promoted: no

### L-STU-67 — three.js r128 instanced meshes: colour before count, and turn culling off
- Lab: Angler Command Lab (`Docs/Studios/AnglerCommandLab.html`) · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: every creature drew grey, then not at all. `InstancedMesh.setColorAt` creates `instanceColor` sized from the mesh's CURRENT `count`, and the page had set `count = 0` first, so the buffer was empty (`instanceColor.array.length` 0) and nothing errored. Separately, r128 frustum-culls an InstancedMesh by its geometry's bounding sphere at the mesh origin, so creatures 450 u from the origin vanished whenever the origin left the view. The verifier passed both: a WebGL stage skips the blank check.
- Do instead: call `setColorAt` for every slot before lowering `count`, and set `frustumCulled = false` on any instanced mesh whose instances spread out. Expose a `gl()` hook (draw calls, instance counts, the first colour) so a probe can tell "not drawn" from "drawn black" without a screenshot.
- Evidence: `initGL` (`fmesh`, `trails.frustumCulled`); `window.__lab.gl()`.
- Promoted: no

### L-GEN-10 — Before calling a failing column a design finding, trace one failing run frame by frame
- Lab: Angler Command Lab, round 1 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the Pack sling failed 3–4 of 4 orders, which read as "the sling is a bad gesture". A per-second trace of one run showed two bugs first. The commander let go after one frame because it counted the gathering ball as "busy". Creatures that latched beside a prism circled it forever: at minimum speed their turn could not catch the bearing, and at d ≈ 0 "facing it" is undefined. With both fixed, the sling turned out to be the fastest scheme on blobs, and its remaining failure (every trail, one 24 u cluster per throw) is the real design finding. A per-order breakdown (`perOrderS`) is what separated the two.
- Do instead: when a variant fails, print its state machine every 0.5 s for one seed before writing it up. Report per-order times, not only a mean, so "fails on lines, wins on blobs" cannot hide inside "slow". For any seek-and-face creature, brake to a hover inside reach and count a meal in the mouth as faced.
- Evidence: `ANGLER_COMMAND_LAB.md` round 1 "Found"; `faunaStep` (the hover and `d < 3`); `squadBusy`.
- Promoted: no

### L-GEN-11 — An attribution column needs a decoy control and per-order state resets
- Lab: Angler Command Lab, round 1 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: "on target %" charges each kill to the CURRENT order. The commander recalled its squad only before the first order (a flag never reset per order), so creatures kept eating the last order's structure and every later kill read as off-target. That made precision look about 20 points worse than it was. A planted decoy (`runMission(…, decoy = true)`: the commander aims every order at the wrong structure) must read 0% on target, and it does on all five schemes. A diet control (`dietAny` 0: "yours lost" must go to 0) proved the friendly losses come from the mouthful, not the targeting.
- Do instead: reset every per-order flag in the function that starts an order. Plant a decoy that must drive the attribution column to zero, and a control that must zero each loss column you explain by one cause.
- Evidence: `nextTask` (`w.ai.recalled = false`); `runMission`'s `decoy`; `ANGLER_COMMAND_LAB.md` "Checked".
- Promoted: no

### L-STU-68 — A companion slower than the hull is invisible from the chase camera; measure follower lag
- Lab: Angler Command Lab, round 1 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the tamed squad runs at the game's fauna speeds (at most 40 u/s at L1), the same as the Angler's cruise, so it sat on average 79 u from the hull (max 102), behind a 40 u chase camera. Every screenshot showed no squad. A speed proportional to the gap added a steady ~27 u lag on top. The same speed gap broke taming: with the lure held at cruise, six creatures were never tamed in 90 s, against 14–19 s with the lure slowing the hull to 20 u/s or less.
- Do instead: in any lab where companions follow a vessel, measure their mean and max distance from it, feed the leader's speed forward in the follow law, and put the speed gap on the page as a design question (here a lure brake and a follower-only leash tow), never as a quiet speed-up of the creature.
- Evidence: `ANGLER_COMMAND_LAB.md` "What it says" (the last two bullets); `leashTow`, `formBack`, `lureCruise`.
- Promoted: no


### L-GEN-12 — A golden table from random inputs can miss a planted change; plant the change before trusting it
- Lab: Angler Command Lab, Unity clone · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the parity gate runs the page's `CORE` functions in headless Chromium on a seeded table of inputs and writes the results into the NUnit test as goldens. Its first `--self-test` planted six one-number changes in the C# port, and two of them passed every golden:
  - The claim penalty, changed from 900 to 800: no random pick case sat near enough to the boundary for the penalty to change the winner.
  - The esca's 2.5 u lift: every lure case used the world up vector, and the plant changed only the x component, which is always zero there.
- Do instead: plant every constant before believing a golden table. When a plant survives, add the cases that reach it, here pick cases with two candidates either side of one claim's cost, and a lure case with a general (non-axis) up vector. Make each plant change every component it can. The gate is only evidence once every plant fails it.
- Evidence: `Tools/Build/angler_command_parity/run.sh --self-test` (6/6), `goldens.cjs` (the boundary pick and tilted-lure cases).
- Promoted: no

### L-STU-69 — A nested enum named like a common word trips the repo's enum-member gate; name it for its owner
- Lab: Angler Command Lab, Unity clone · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the executor's per-creature state enum was first called `Order`, matching the lab's word. `check_enum_member_references.py` resolves `X.Member` by enum NAME across the repo, so every `Order.Add(...)` elsewhere (a list called `Order` in `ProfilerCapture.cs`) read as a reference to a missing member of the new enum. The gate went red on code the change never touched.
- Do instead: give a nested enum a name only its owner would use (`LeashOrder`), even when the lab calls it something shorter. Keep the lab's word in the doc comment.
- Evidence: `AnglerCommandExecutor.LeashOrder`; the gate's output before and after the rename.
- Promoted: no

### L-STU-70 — Port the lab's LOOK of a state with its rules; a marker at an object's centre is inside the object
- Lab: Angler Command Lab, Unity clone · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the lab draws a painted prism by recolouring the prism itself (75% toward white in `render()`). The Unity port kept the paint arithmetic line for line, but drew the state as camera-facing dots at each prism's centre. Every dot sat inside an opaque prism, so the first playtest reported that painting did nothing. Every gate was green, because no gate looks at pixels.
- Do instead: when a lab state is shown by changing an object's own look, port that look onto the object with the lab's numbers (here a per-prism lift toward white, one fading stamp per change). Then list in the verification entry what the player must SEE, not only what the code does. A world-space marker for a solid object goes on its surface or above it, never at its pivot.
- Evidence: `AnglerCommandLab.html` `render()` (the `col.lerp(WHITE, 0.75)` line); `Prism.SetColorLift`; `ANGLER.md` §1a.
- Promoted: no

### L-STU-71 — A SHIPPED key two schemes share moves both; name the key per scheme or report both rows
- Lab: Angler Command Lab, round 3 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-16
- What happened: the designer asked for the Sling painter's reach x20. `paintRange` is also the round-1 Spotlight's reach, and the AI commander's approach distance. Raising it moved three things: the Sling painter (85% on target, from 90%), the Spotlight (70%, from 80%), and how the scorecard's AI flies. A comparison scheme that was meant to stay put shifted with it.
- Do instead: before changing a SHIPPED value, grep every reader (`P.<key>`): schemes, the AI commander, the renderer. Either split the key per scheme, or re-run the whole batch and report every row that moved. Here the change was meant for both painters, so both rows are reported in `ANGLER_COMMAND_LAB.md` round 3.
- Evidence: `AnglerCommandLab.html` (`P.paintRange` in `brushStroke`, the spotlight's `aimPick`, the commander's reach); the round-3 scorecard.
- Promoted: no

