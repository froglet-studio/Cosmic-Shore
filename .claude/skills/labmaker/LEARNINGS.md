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

### L-GEN-10 — Score the LOOK in rendered pixels against the SHIPPED baseline; a shared-envelope argument is not a shared look
- Lab: Collision Burst Lab (`Docs/Studios/PrismCollisionBurstLab.html`) · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: the collision bursts were designed to "squint as the explosion" by sharing its flight, life, palette, envelope radius and linear fade, and the docs said so. The lab drew both and scored blurred 120×80 pixels: two explosions differ by 0.082, the bursts by 0.33–0.47 — worse than a prism that never breaks (0.261, the negative control). Thin accents, a smaller cloud and the explosion's lingering last wedges each mattered. The design had also been argued against a REMEMBERED explosion ("the wedges spin in place"); the shipped subgraph, evaluated, slides each wedge outward linearly with age.
- Do instead: make the baseline the shipped graph, evaluated (and parity-gated), never its description. Measure "looks like" on rendered, blurred pixels, with a band control (the baseline vs itself on another seed) and a negative control that must fail. Hand-waved invariants become columns.
- Evidence: `Docs/Studios/COLLISION_BURST_LAB.md` round 1 scorecard; `__lab.runBatch()`
- Promoted: no

### L-GEN-11 — A fixed tolerance in a randomized harness is a latent failure: adding an unrelated case moved the draws onto it
- Lab: Collision Burst Lab round 2 (`Tools/Shaders/verify_prism_collision_burst.py`) · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: adding a fifth kind (the flake) to the pose loop consumed more of the seeded RNG, so the later bend test drew different prisms, and the unravel's isometry check failed at 2.2e-3 against its fixed 2e-3 — float32 noise of ~40-unit coordinates over a 5e-4 step. The shader was right; the check had passed only because its seed never drew such a prism. Its sibling check (the normal tilt) already scaled by the float noise floor and passed.
- Do instead: every finite-difference tolerance in a randomized harness gets a noise floor scaled to the coordinates and the step (`8 · 1.2e-7 · (|p| + R) / eps`), never a bare constant; and when a harness goes red after an unrelated addition, suspect the draws moved before the code.
- Evidence: `verify_prism_collision_burst.py` `stretchFloor`; the failing run printed `the bend stretched a step by 0.997898` four times
- Promoted: no

### L-GEN-16 — A mechanism written into a write-up is a hypothesis until a trace shows it
- Lab: Crew Vessel Lab round 1 (`Docs/Studios/CrewVesselLab.html`) · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-14
- What happened: the first draft explained the weak element-buff consoles as "the pilot's crystals fill the [0,10] band within about two minutes, so buffs only reach the draining overcharge". The arithmetic looked right (4 elements × 8 petals at ~15 catches/min). A trace of the levels at 30 / 60 / 90 / 120 s showed 6–10 at the END of the 120 s scoring window, so the band was not full for most of it. The real cause, also traced, was the payout: a match board averages about 1 level of buff spread over four elements.
- Do instead: before a "because" goes into a finding, log the quantity it names over the run (here the base levels and the summed live buff) and confirm it says what the sentence claims. An arithmetic estimate is where to look, not the evidence.
- Evidence: `CREW_VESSEL_LAB.md` round 1, Found 2 (mean live buff 1.06 levels; base levels 7.0/10.4/10.8/7.0 at 120 s).
- Promoted: no

### L-GEN-17 — A lab's canvas draws ported to Unity's instanced meshes: never `Sprites/Default`
- Lab: Crew Vessel Lab → the Wrasse in Unity (`Assets/_Scripts/Controller/Vessel/Crew/KiteNavigatorView.cs`) · Branch: `cece/compassionate-babbage-gc13io` · By: session_01C5GGXf6VL3oyrKAGWRtfMr · Date: 2026-10-18
- What happened: the lab's 2D overlay (kite, motes, rings) became world meshes drawn with `Graphics.RenderMeshInstanced` and a `Sprites/Default` material. It compiled, and the batching looked right. Garrett flew it and "couldn't see any petals": on the instanced path, `Sprites/Default` takes its colour from `unity_SpriteRendererColorArray`, which only a SpriteRenderer fills, so every instance drew at zero alpha.
- Do instead: for instanced overlay draws, use `Universal Render Pipeline/Unlit` set up as a transparent, double-sided surface (`_Surface` 1, SrcAlpha/OneMinusSrcAlpha, `_ZWrite` 0, `_Cull` Off, `_SURFACE_TYPE_TRANSPARENT`). Keep `Sprites/Default` only for single non-instanced meshes whose vertices carry their own colour. When graduating a lab's look, hand the person the one thing to check first: "can you see it at all".
- Evidence: `KiteNavigatorView.Unlit` and its comment; `KITE_NAVIGATOR.md` "Round 4b".
- Promoted: no

### L-GEN-18 — When dotnet-install is blocked, build the SDK from api.nuget.org packages
- Lab: Honeyguide Lab → the Dynamo C# parity gate · Branch: `claude/honeyguide-ratel` · By: session_01UEczCm2LP2GYLZnaMNw6vC · Date: 2026-10-20
- What happened: the cloud proxy refused builds.dotnet.microsoft.com and www.nuget.org (403), so the parity gate had no compiler. api.nuget.org/v3-flatcontainer was open, and its packages carry everything a headless compile needs.
- Do instead: assemble `~/.dotnet` from the flat-container packages (the runtime pack, `Microsoft.NETCore.App.Ref` and `Microsoft.Net.Compilers.Toolset` for csc) and compile with csc against the reference pack. The Kite and Dynamo parity gates both pass on that layout. Details in `Docs/Studios/HONEYGUIDE_LAB.md` "Port notes".
- Evidence: `Tools/Build/dynamo_lab/trace.sh`.
### L-GEN-19 — Trace a few runs' state every ten seconds before reading any aggregate
- Lab: Crew of Four Lab (`Docs/Studios/CrewOfFourLab.html`) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: the first scorecard looked plausible (Gate Run points 81, 62 boardings, 8 cast-offs). A 10-second state trace of one run showed the crew's skiff holding the Artifact from 20 s to the 180 s limit, chasing its old boarding target instead of flying home, and rival boarders re-boarding a fully drained hull every second. Both bugs had produced numbers that looked like a strong boarding meta.
- Do instead: before the first aggregate, dump `state()` every 10 s for a few seeds of each game and read it; a value pinned for a whole run (a carrier that never docks, a level stuck at its floor) is a behaviour bug, not a balance finding.
- Evidence: `CrewOfFourLab.html` `skiffAI` (the early return for `sk.art`, the drained-victim recall); `CREW_OF_FOUR_LAB.md` §4 Found 9.
- Promoted: no

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

### L-STU-88 — Gate a WebGL port of a shipped shader by transform feedback, with ZERO outliers allowed: it finds the shipped shader's float bugs
- Lab: Collision Burst Lab · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: `parity.py` reads the page's vertex shader back through WebGL2 transform feedback and compares every vertex with C++ executing the shipped HLSL on the same mesh. An outlier allowance would have hidden three real defects, all now fixed in the shipped HLSL: bloom petals picked by float noise when a centroid sat exactly on a face diagonal (~80 vertices per case, ~1 radius off; fixed with a tie epsilon), `(1 − cos a)/κ` cancelling in float32 at small angles (fixed as `2 sin²(a/2)/κ`), and the lab building mesh centroids in doubles where C# uses float (`Math.fround`). Mechanical trap: transform feedback returned all zeros with `GL_INVALID_OPERATION` because the feedback buffer was still bound to `ARRAY_BUFFER` — unbind it after creating it and check `gl.getError()` after the draw.
- Do instead: translate the shipped shader mechanically (build step, `--check`), gate it per vertex with no outlier share, and plant controls (the next kind, a doubled dial) that must fire. When parity fails on a few vertices, suspect the shipped code before the port.
- Evidence: `Tools/Build/prism_collision_burst_lab/parity.py` (`OUTLIER_SHARE = 0.0`); `PrismCollisionBurst.hlsl` `PRISM_BURST_PETAL_TIE`
- Promoted: no

### L-STU-89 — A camera that frames the scene must move with whatever moves the scene (the seed); read the screenshot at a non-zero seed
- Lab: Collision Burst Lab · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: each seed shifts every pose 7.31 units in z so the per-prism seed hash changes; the line-up camera targeted the origin. Seed 0 looked perfect; any Replay (seed + 1) and `verify_lab.cjs`'s `reset(7)` left an empty stage, and the gate still passed (a WebGL stage skips the blank check). Only reading `desktop.png` showed it.
- Do instead: derive the camera target from the same "home" the scene items use. Read the screenshot the verifier takes after `reset(seed)`, not only a fresh load.
- Evidence: `lab.src.html` `camTarget`
- Promoted: no

### L-STU-90 — "Looks like X from a distance" is a SILHOUETTE claim before it is a colour claim; and a ramp of the right curve can still read as a wipe
- Lab: Collision Burst Lab round 2 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: a steal's shed was built so its coverage runs the grade's exact smoothstep (proven equal to the executed `PrismColorLerp`), so its mean colour IS the grade's. Scored on blurred rendered pixels against the grade, the bloom (0.214) and the unravel (0.220) still read further from it than an instant colour snap (0.158), because their petals and ribbons grow the silhouette by a third (size 1.32–1.35). The flake, whose silhouette barely changes (1.10), scored 0.120 but plateaued at ~0.104 under every dial: it reveals the colour as a front along the prism, a wipe, not a uniform change.
- Do instead: before tuning colour or timing to match an effect "from a distance", score the silhouette (size column) and the spatial order of the change, not only the mean; when the ordinary effect is a colour change, blur all three channels (a hue change at equal brightness hides from a luminance blur). Give the scorecard a band (the ordinary effect, slightly late) and a negative (the change snapped) so "close" has a scale.
- Evidence: `Docs/Studios/COLLISION_BURST_LAB.md` round 2 tables; `__lab.runBatch()` `table: 'sheds'`
- Promoted: no

### L-STU-67 — A detail level that DROPS geometry pops; one that MERGES it does not — and a uniform triangulation staircases one diagonal
- Lab: Collision Burst Lab round 3 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: the brief was "from far away it will look like we just aren't seeing the small ones". Removing the small glitter fragments from the far mesh would leave the prism with holes on the first frame of the death, a pop. A nested tiling (a quadtree whose coarser level is the finer with its children merged into their parent) gives the same far picture with nothing missing, and it measured as squint-identical (0.318–0.322 across three levels). Separately, the 24-subdivision shed showed a staircase along one face diagonal: every cell cut the same way puts one diagonal on triangle edges and the other across them. A union-jack cut puts both on edges (0 straddles at every even n, checked).
- Do instead: build an LOD ladder as a hierarchy of merges (every level a complete cover), not a subset. When a piece pattern follows lines on a mesh (petal diagonals, ribbon seams, scale grids), make the mesh's edges lie on those lines (cut direction, subdivision a multiple of the pattern), and test for straddling triangles.
- Evidence: `PrismGlitterTiling.cs`, `HighPolyPrismMesh.GetShed`; `parity.py` (tiling vs C# compiled; meshes: area 6, 0 straddles)
- Promoted: no

### L-STU-68 — A step test's band decides what it can say: "no more different than another death" is weak when two deaths differ completely
- Lab: Collision Burst Lab round 3 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-14
- What happened: each LOD step was scored at the true pixel size of its line against a band of the finer level on another prism. For destruction the band was ~0.97 (two close-up deaths share almost no pixels), so every destruction step passed, the shatter→explosion one at 0.94 of it. The same test was sharp for the sheds (band ~0.46, resolution steps 0.035–0.058).
- Do instead: report the step and the band side by side, never only the verdict; when the band is near its ceiling, say the test cannot tell and look for a tighter band (the same death at a 10% time offset, a squint) before claiming a step is invisible.
- Evidence: `__lab.runSteps()`; `Docs/Studios/COLLISION_BURST_LAB.md` round 3 table
- Promoted: no

### L-STU-69 — When the cheapest level is an existing effect, make it the seed of the recursion and prove the identity
- Lab: Collision Burst Lab round 4 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: the designer proposed a Sierpinski cut "using the base explosion as the first depth". The explosion's mesh is four wedges a face fanned from the centre, which is exactly a Sierpinski seed; each deeper depth splits the corners and keeps the middle. Making depth 0 the explosion's own cut, and checking it (depth 0 on six faces = `Prism.asset`'s 24 triangles, in parity.py), removed an entire rung (the shatter) and a second tiling (the quadtree glitter), and every depth now nests in the explosion's wedges.
- Do instead: when an LOD ladder ends in an effect the game already ships, derive the finer levels FROM that effect's geometry and gate the identity of level 0 against the shipped asset, so the ladder's coarse end cannot drift from what players already see.
- Evidence: `PrismSierpinskiTiling.cs`; `parity.py` "depth 0 vs Prism.asset"
- Promoted: no

### L-STU-70 — A step band drawn from a deterministic law collapses to zero: "another prism" must be another EVENT
- Lab: Collision Burst Lab round 5 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: once every destruction fragment moved by the explosion's law (no per-prism random), the LOD-step band — the finer level on another prism, same flight — drew the identical death: bands 0.001–0.003 and step ratios of 175 to 559,000, every step "visible". With the band on a turned flight (another death, as the scorecard's explosion band already was) the bands were 0.47–0.51 and every step 0.68–1.19 of its band.
- Do instead: build a band from the variation a player actually sees between two events of the same kind (another flight, another seed, another moment); when the effect stops consuming a random, re-check what the band draws.
- Evidence: `lab.src.html` `runSteps` (the `other` prism); `Docs/Studios/COLLISION_BURST_LAB.md` round 5, Found 4
- Promoted: no

### L-STU-71 — Before matching an effect's motion, read its mesh's UV channel: the explosion's fade was laid out per wedge
- Lab: Collision Burst Lab round 5 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: the destruction fragments were given the explosion's motion and its erosion fade, reading their face coordinates as UV0. `Prism.asset` turned out to give all 24 wedges the SAME UV triangle ((0,0), (1,0), (0.5,0.5)), so the explosion wipes each wedge on its own; on half the faces the face coordinates would have faded on another pattern (36 of 72 vertices differ — now parity's negative control). Fixed with `PrismSierpinskiTiling.WedgeUV`.
- Do instead: when a new effect must reproduce a shipped one, dump the shipped mesh's every channel (position, normal, tangent, UV) and gate each one the new mesh feeds into the shared shader code, not only the positions.
- Evidence: `parity.py` `uv_vs_prism`; `PrismCollisionBurstTests.EveryFragmentWearsItsWedgesExplosionUV`
- Promoted: no

### L-STU-72 — Prove a remaining squint gap's cause with a scratch copy that removes it, before calling it shape or motion
- Lab: Collision Burst Lab round 5 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: with the explosion's law, destruction's size and fade matched the explosion but the squint stayed 0.21–0.22 against a 0.097 band, and the colour column (0.10–0.11, the explosion's band 0.012) pointed at the brighter inside colour. A scratch copy of the built page without the inside colour on the wedge scored 0.13–0.14, inside 1.5 × the band — so the remaining gap is a deliberate design rule, not a motion bug, and goes to the designer as a decision.
- Do instead: read the scorecard's columns to name the suspect, then remove exactly that in a scratch copy and re-score; put the measured trade-off in the write-up's decisions instead of "tuning" the shipped rule away.
- Evidence: `Docs/Studios/COLLISION_BURST_LAB.md` round 5, Scorecards and Found 2
- Promoted: no

### L-STU-73 — "Same law" is not "same motion" when an input the law takes per PIECE is chosen per fragment
- Lab: Collision Burst Lab round 6 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: round 5 moved every Sierpinski fragment by the explosion's exact closed form and parity proved depth 0 IS the explosion — yet in the game the designer saw the fragments spin the wrong way. The law's tangent input, which the explosion takes per WEDGE (four a face), had been chosen per FRAGMENT (its centroid direction), so every fragment had its own first axis and slide direction. Giving each fragment its root wedge's tangent dropped the squint from 0.21–0.22 to 0.17–0.18 (0.05–0.06 without the inside colour, inside the two-explosion band).
- Do instead: when a law is applied to finer pieces of something, list each of its inputs and decide whether the original takes it per piece or per parent; prove the property the player sees (here: every fragment's normal equals its parent wedge's — W7, with the per-fragment choice as a negative control), not only that the coarsest level matches.
- Evidence: `PrismCollisionBurst.hlsl` `PrismBurstWedgeTangent`; `verify_prism_collision_burst.py` W7 and the radial-tangent control
- Promoted: no

### L-STU-74 — "Hidden by its own prism" is a measurable claim: test it with a segment, and the failures name the geometry
- Lab: Collision Burst Lab round 7 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: the designer asked for steal rolls that leave by being occluded, not by dissolving. A harness test (segment from each rolled vertex to a random camera against the prism's cube) started at 80.6% hidden; sampling the visible vertices named three causes in turn — a fixed run-out distance too short for steep views (fixed by deriving it from the camera's height and offset), the roll's end caps leaking past the corner for a sideways camera (an inward tuck), and a roll that kept rolling out past the far end of a long slab (stop at the first hiding place) — to 98.5%. An occluder shrunk "a hair inside" also miscounted end caps lying exactly in a face plane.
- Do instead: turn an occlusion idea into a percentage over random poses and cameras with a negative control (roll TOWARD the camera), print a sample of the failures with object-space coordinates, and fix the geometry the sample names; measure against the occluder's true extent.
- Evidence: `verify_prism_collision_burst.py` `run_roll_tests`, `hiddenBehindPrism`; `PrismCollisionBurst.hlsl` `PrismBurstRoll`
- Promoted: no

### L-STU-75 — A test that names things by a hash inherits the hash's precision
- Lab: Collision Burst Lab round 7 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-15
- What happened: the harness's shatter-rigidity test paired triangles whose piece hash was equal. The float hash on large inputs quantises to steps of 1/1024, so two different shards could share a "name"; the test passed for rounds only because its random sequence never drew such a pair, then failed when an unrelated test added one random draw.
- Do instead: identify a piece by the quantity that defines it (here the Voronoi cell), and treat a hash as a name that can collide; when a passing test starts failing after an unrelated change to the RNG stream, suspect the test's identity assumption before the code.
- Evidence: `verify_prism_collision_burst.py` test 3b
- Promoted: no

### L-STU-76 — A parametrised test must prove it built the case it names
- Lab: Collision Burst Lab round 9 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-16
- What happened: rounds 7 and 8's harness chose the mesh with `cut.name[1] == 'i'` — true for both "sierpinski" and "silver" — so every "silver" case ran the Sierpinski cut and printed a pass under the silver's name for two rounds. It surfaced only because round 9's silver depth 10 took minutes (Sierpinski depth 10 is ~236k vertices).
- Do instead: select a case by an exact comparison, and assert a property only the named case has (here the vertex count, 72 × the per-wedge count) before testing it. A runtime far off what the case should cost is a hint the case is not the one named.
- Evidence: `verify_prism_collision_burst.py` `run_fragment_tests` (the size CHECK); `COLLISION_BURST_LAB.md` round 9 Found
- Promoted: no

### L-STU-77 — A yardstick that steered the design must go when the designer retires its goal
- Lab: Collision Burst Lab round 9 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-16
- What happened: eight rounds tuned destruction to squint as the explosion, which kept fragments grouped in their wedges' sheets. The designer saw the squint steering the look away from what they wanted and retired it. The replacement Look scorecard needed a measure that could say NO to "grouped": direction entropy at 64 bins could not tell six cones from a sphere (0.787 vs 0.726), while 256 bins could (0.81 vs 0.53). The same measure found a design hole the eye had missed: one angle off every face made six hollow cones (0.71). Weighting the radial by how far out the fragment sat filled the sphere (0.83).
- Do instead: when a goal is retired, retire its scorecard and its gates too (the squint table, the LOD-step test, the Squint button), and build the new measure with a control compiled from the shipped shader that must fail it, here `PRISM_BURST_FRAGMENT_FACE_ONLY` compiled into a page probe. Size the measure's resolution against that control before trusting it.
- Evidence: `lab.src.html` `lookOf` / `faceOnlyProbe`; harness F9 and `ENTROPY_FLOOR`
- Promoted: no

### L-STU-78 — A camera that rides the effect hides the input that moves it; give every input the designer doubts its own control and a measured readout
- Lab: Collision Burst Lab round 10 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-16
- What happened: the designer saw in game that the destruction "was not accepting the velocity". The code passed it end to end, but the lab could not show that. Its camera rode the flight, so the flight never showed, and the velocity was a hidden function of one "closing speed" slider. The fragments' own motion also barely depended on the hit. The round added a 3D velocity input, an arrow for the resolved flight, a hold-still camera, and a scorecard lean read along the input's direction. Its first version read the reversed control along the control's OWN direction, and so passed it.
- Do instead: when a designer doubts an input reaches the effect, give the input its own control in the input's own units, draw what the game resolves it to, and measure the effect's response to it with two controls: the input reversed (read along the ORIGINAL axis) and the input ignored (compiled from the shipped shader).
- Evidence: `lab.src.html` Velocity tab, `drawArrows`, `lookOf` lean; harness F10; `COLLISION_BURST_LAB.md` round 10
- Promoted: no

### L-STU-80 — A baseline drawn by a different shader cannot answer to the lab's dials: say so on the page, or make it the same law
- Lab: Collision Burst Lab round 11 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-17
- What happened: the designer tuned the Fragments tab and recorded the look they liked, then found it "reflected in the levels not the explosion". Level 0 was still the explosion's own Shader Graph, which no burst dial reaches. The lab had labelled it the baseline, but it sat in the same line-up as the levels, and nothing on the page said its look was fixed. The fix made level 0 the same fragment law on the explosion's own 24 wedges, with its own live cap outside the close-up budget, since every far death takes it. The old graph moved to the scorecard as the "before" row.
- Do instead: when a line-up mixes cells the dials drive with cells they cannot, mark the second kind on the stage, not only in a tooltip. When the designer then wants the dials everywhere, unify the law. Do not hand-tune the other shader to match. Before routing every instance through a path built for a budgeted few, give the bulk its own queue and cap.
- Evidence: `PrismInteractionLod.TryDestruction` (level 0), `PrismCollisionBurst.CanAcceptExplosion`; `COLLISION_BURST_LAB.md` round 11
- Promoted: no

### L-STU-81 — "Not copies, not noisy" is a question of WHERE the randomness lives; measure the two separately, and never hash a seed the GPU must reproduce
- Lab: Collision Burst Lab round 12 · Branch: `cece/dreamy-pascal-7xxx1m` · Date: 2026-10-17
- What happened: the designer asked that neighbours struck alike not play one animation while each explosion stays one continuous structure. More per-fragment noise would have traded one fault for the other. Drawing the randomness ONCE per death (spread, timing, spin, tumble plane, lean, a smooth ellipsoid) answered both. Two metrics made that visible: a per-region SHAPE difference between two deaths (a per-vertex difference could not tell gestalt from grain; the noise alone already read 0.18 there), and a within-death neighbour roughness. The first rose 3.3x and the second did not move. The first version read the seed through hash13, and the GPU parity gate failed (worst 7e-3, 99% of vertices). hash13 multiplies its input's rounding about a hundredfold, so the page's GPU and the harness drew different characters for the same death. Triangle waves of the seed fixed it (worst 8e-4).
- Do instead: put "differ between instances" in per-instance constants and "differ within one" in a continuous field, and score each with its own metric plus a negative control. Read an already-hashed seed through gentle functions (triangle waves, low-frequency sines). Never pass it through a second high-gain hash when two GPUs, or a page and a harness, must agree.
- Evidence: `PrismBurstFragmentVariety`, `PrismBurstSeedWave` (HLSL); harness F11 and `PRISM_BURST_FRAGMENT_VARIETY_OFF`; `COLLISION_BURST_LAB.md` round 12
- Promoted: no

### L-STU-106 — A pursuit AI with no throttle orbits anything inside its turn circle; read the per-seed spread before the mean
- Lab: Crew Vessel Lab round 1 · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-14
- What happened: the first scorecard said two of four navigator consoles made a crew WORSE than a solo pilot. The per-seed scores ran from 2 to 20 petals per minute. Two lab bugs: the hull flew at constant speed, so a crystal inside its 25 u turn circle (35 u/s at 80 °/s) was orbited forever; and the "nothing in sight" behaviour turned in place, so a pilot that lost sight of everything never found anything again. With the Sparrow's real throttle (minimum 10 + scaler 25, braking for a target inside 2.2 turn radii) and a roaming search point, skilled runs sat within about ±15% and both "negative" consoles turned positive.
- Do instead: give the lab hull the game's throttle, not a constant speed, and print per-seed values for every condition before quoting a mean. A spread of 10× between seeds is a stuck agent, not noise.
- Evidence: `CrewVesselLab.html` `pilotAI` (`turnR`, `seat.roam`); `CREW_VESSEL_LAB.md` round 1, Found 7.
- Promoted: no

### L-STU-107 — To ask "does this seat's PLAY matter", score skill in the seat separately from the seat existing
- Lab: Crew Vessel Lab round 1 · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-14
- What happened: on crew lift alone, Power Split ranked second (+20%). Scoring a Hard against an Easy navigator in the same seat showed 4% (inside the noise): the lift came from the pips being lit at all, not from how they were routed. An actions-per-minute column showed the Hard AI clicking about 200 times a minute for it. Scope Call showed the opposite shape: small lift (+14%) and large skill (31%), so a bad navigator made a good pilot worse than flying alone.
- Do instead: for any lab about a role or a seat, report three columns side by side: the role's lift over its absence, the skill spread inside the role (expert vs rookie in that seat, everything else fixed), and the role's action rate. Each catches a design the others miss: a chore (high rate, no skill), a crutch (lift, no skill) and a liability (skill bigger than lift).
- Evidence: `CrewVesselLab.html` `runBatch` (`navMatters`, `navApm`); `CREW_VESSEL_LAB.md` round 1 scorecard.
- Promoted: no

### L-STU-108 — An AI's "arrived" test must measure against the point it is actually aiming at, noise included
- Lab: Crew Vessel Lab round 2 (`Docs/Studios/CrewVesselLab.html`) · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-16
- What happened: the navigator AI moves a thumb toward `goal + aim noise` but returned its distance to `goal`. Whenever the noise was larger than the arrival tolerance (the sling's was 0.01 board radii, about the Hard AI's noise), the thumb sat on its noisy point forever, one phase short of releasing. Pinball and Pathfinder caught almost nothing on some seeds, which read as "these controls are weak" until a frame trace showed `plan=1/0` unchanged for 10 s.
- Do instead: an agent that aims with noise judges arrival against its own aim point, and any multi-phase gesture plan gets a trace of its phase over time before its column is quoted. A phase that never changes is a stuck agent.
- Evidence: `CrewVesselLab.html` `moveThumb` (returns the distance to `tx, tz`); `CREW_VESSEL_LAB.md` round 2, "Fixed in the lab".
- Promoted: no

### L-STU-109 — A target outside the playfield's clamp freezes the mover forever: clamp the TARGET at input, not only the mover
- Lab: Crew Vessel Lab round 2 · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-16
- What happened: the kite is kept on its round board every frame. A dash target or a drawn path point just past the rim was never reached (the clamp pulled the kite back each frame), so the dash and path never finished and no new one could start. Hopper seed 2 froze at 33 s and Pathfinder seed 3 at 3 s. A person tapping near the edge would have hit the same freeze; the AI found it first.
- Do instead: clamp every target a player can set (tap, drag end, drawn point) into the reachable area when it is recorded, inside the mover's own clamp. Look for a mover whose position stops changing while it still has a target.
- Evidence: `CrewVesselLab.html` `onBoard(x, z)` applied to `dashTo` and path points.
- Promoted: no

### L-STU-110 — When an exponent multiplies a spawned quantity, the spawner is a balance knob as strong as the exponent
- Lab: Crew Vessel Lab round 3 (`Docs/Studios/CrewVesselLab.html`) · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-17
- What happened: a pure loop of n motes fires `base × 2^(n − 1)`. Spreading each mote cluster 4 u wider (gaussian 14 → 18 u, a readability fix) cut the mean loop from 2.8 to 2.25 motes and Space blasts from 327 to 12 hostile prisms a minute. The exponent turned a small change in how motes arrive into a 27× change in what the effect destroyed.
- Do instead: when a payoff is exponential in a count, put the thing that sets the count (cluster size, spread, spawn rate) beside the exponent in the sign-off questions, and re-run the scorecard after ANY change to the spawner, including a "visual" one. Quote mean and best n with every effect column.
- Evidence: `CREW_VESSEL_LAB.md` round 3, Found 2 (two `runBatch` runs either side of the spread change).
- Promoted: no

### L-STU-111 — A pickup fixed in world space is almost never reachable by a player who lives in a tilting plane
- Lab: Crew Vessel Lab round 3 · Branch: `cece/compassionate-babbage-gc13io` · Date: 2026-10-17
- What happened: the navigator plays on the flying vessel's x-z plane and can touch things within ±40 u of it. Motes placed in world space near the plane were touchable 1–4 times out of 30 within seconds, because every pitch and roll of the pilot swings a 160 u board through 150 × sin(angle) u. The AI planned loops round motes it could not take, and no loop ever fired.
- Do instead: measure the touchable share of a plane-bound player's targets over time before tuning anything else. When it is low, give the targets an in-plane home (re-project them onto the plane each frame, so they flow with the world but stay touchable) and keep world-fixed as a deliberate option ("the pilot sweeps the slice").
- Evidence: `CrewVesselLab.html` `stepMotes` (`motes: 'board'`), the Motes in the world preset; round 3 Found 6.
- Promoted: no

### L-STU-112 — A five-fold flower turned 72° looks unmoved: keep motion continuous, or break the symmetry
- Lab: Crew Vessel Lab → the Wrasse's motes (`KiteNavigatorView.Flower`) · Branch: `cece/compassionate-babbage-gc13io` · By: session_01C5GGXf6VL3oyrKAGWRtfMr · Date: 2026-10-18
- What happened: the Space motes copied the Space crystal's stepped group spin (72° in 1.1 s, then a hold). On a flower of five identical petals every rest pose is the start pose, so the spin read as a twitch. Garrett: "I didn't notice the space rotating".
- Do instead: before porting a motion onto a symmetric shape, ask whether its rest poses are distinguishable. A step of 360°/n on an n-fold shape is invisible. Spin continuously, or mark one petal.
- Evidence: `KITE_NAVIGATOR.md` "Round 4e" (`spaceSpinDegreesPerSecond` 150, continuous).
- Promoted: no

### L-STU-113 — To match a shipped icon's proportions, measure the shipped sprite; never shape it with free knobs
- Lab: Crew Vessel Lab → the Wrasse's motes · Branch: `cece/compassionate-babbage-gc13io` · By: session_01C5GGXf6VL3oyrKAGWRtfMr · Date: 2026-10-18
- What happened: to separate the petals, round 4e added `petalGap` and a non-uniform `petalWidth`. The width squashed every traced outline, so the Space dart "doesn't look the same in its proportions". The totem's flower already had the answer: each `Resources/ElementPetals/<element>_petal.png` is the whole flower, one petal at its true offset and size.
- Do instead: trace the shipped asset (alpha bounds at 0.5 give each petal's centre offset and scale, in flower radii), scale uniformly, and delete the knobs that were standing in for that measurement.
- Evidence: `KiteNavigatorView.PetalCentreOut` / `PetalScale`; `KITE_NAVIGATOR.md` "Round 4f" table.
- Promoted: no

### L-STU-114 — Score each event's signed gain toward the goal; a coarse race column hides the mechanic
- Lab: Honeyguide Lab · Branch: `claude/honeyguide-ratel` · By: session_01UEczCm2LP2GYLZnaMNw6vC · Date: 2026-10-20
- What happened: the first scorecard counted whole destinations per minute. Slips (teleports along a trail) moved the hull a long way, but most runs reached the same whole number of destinations, so every variant read alike and the always-armed brief looked harmless. Counting the share of the last leg flown and recording each slip's signed gain toward the destination showed always-armed lifting the race 2% with 32% of slips going the wrong way, against 25% for hold-to-arm.
- Do instead: when a mechanic acts in discrete events, score every event's signed contribution to the goal (and the share that went backwards), and make the race column continuous (partial legs), before reading any variant.
- Evidence: `HoneyguideLab.html` score columns `slipGain`, `badSlips`, `destsPerMin`; `HONEYGUIDE_LAB.md` Found 1.
- Promoted: no

### L-STU-115 — An ordering control is only caught by a scenario where the order matters
- Lab: Honeyguide Lab → the Dynamo C# parity gate · Branch: `claude/honeyguide-ratel` · By: session_01UEczCm2LP2GYLZnaMNw6vC · Date: 2026-10-20
- What happened: a planted control reversed the placement scan in the C# core. Every scenario still matched, because the shipped patterns never offered two legal placements at once. Adding synthetic tiny patterns (two or three cells, many legal placements) made the control fire.
- Do instead: for each planted control, name the scenario that makes it observable; if none exists, build one (a synthetic input that forces the ambiguity) rather than calling the control a pass.
- Evidence: `Tools/Build/dynamo_lab/parity.cjs` variant 2 (synthetic patterns) and the reversed-scan plant.
- Promoted: no

### L-STU-116 — A greedy puzzle AI must lock in help that is about to move, and keep its plan
- Lab: Honeyguide Lab round 2 · Branch: `claude/honeyguide-ratel` · By: session_01UEczCm2LP2GYLZnaMNw6vC · Date: 2026-10-21
- What happened: round 2 let an empty injection port count as a prism. The Ratel AI (cheapest placement first) leaned on those ports, the spin turned them away before it could finish, and it made patterns 35% slower with the help than without (4.8 against 7.4 a minute). With few prisms on the board it also flipped between two equally cheap placements and moved the same prism back and forth (38 reversals in one seed).
- Do instead: when a plan uses something that expires (a port before the spin, a buff before its timer), cost it as permanent only if the plan finishes in time; otherwise spend the action that makes it permanent. Keep the placement being built until another is clearly cheaper. Measure a rule that "should help" with the AI before reading its scorecard row.
- Evidence: `HoneyguideLab.html` `placementCost` (`onPorts`, `durable`) and `aiPlan(..., memo)`; `HONEYGUIDE_LAB.md` round 2 Found 4.
### L-STU-125 — URP's Vignette multiplies: a coloured vignette over dark space is invisible, and a lab yardstick must score that cut near zero
- Lab: Silence Vignette Lab (`Docs/Studios/SilenceVignetteLab.html`) · Branch: `cece/sleepy-shannon-9qk646` · By: session_01PEp87yJAmoDHUn7QvRdtkg · Date: 2026-10-21
- What happened: the silence feedback's first cut drove the gameplay profile's URP Vignette in the palette's danger colour (intensity 0.26). Garrett played Dog Fight twice and never saw it. URP's `ApplyVignette` lerps the frame toward `frame × colour`, so red can only darken, and the arena's sky is (0, 0, 0.1): red times near-black is near-black. The driver was proven to reach weight 1.0 in Amoebius, so "the code runs" said nothing about "the player sees it". The lab's scorecard (OKLab ΔE ×100 over the edge band, 480×270, seeds 1-3) reads that cut at 2.8 on open space, about the just-noticeable difference, against 45 for an alpha-blended overlay at 0.75.
- Do instead: signal a state with an overlay that ADDS colour (a ScreenSpaceOverlay canvas with a baked mask texture), keep URP's Vignette only for black darkening, and put the shipped look in the lab as a baseline row ("Round 0") that the yardstick must score near the just-noticeable difference, or the yardstick is not measuring sight.
- Evidence: `SilenceVignetteMask.cs`, `SilenceScreenFeedback.EnsureOverlay`; the lab's scorecard and yardstick check; `Tools/Build/silence_vignette_parity/run.sh`.
- Promoted: no

### L-STU-126 — Verify the version you published, after the last edit, not the one before it
- Lab: Silence Vignette Lab (`Docs/Studios/SilenceVignetteLab.html`) · Branch: `cece/sleepy-shannon-9qk646` · By: session_01PEp87yJAmoDHUn7QvRdtkg · Date: 2026-10-21
- What happened: `verify_lab.cjs` passed, then a one-line wording edit was published straight through the Artifact tool's `edits`. The new text, "the breath's peak", put an apostrophe inside a single-quoted `innerHTML` string, so the whole script failed to parse (`Unexpected identifier 's'`). Garrett opened a page with no lab in it ("the lab seems broken").
- Do instead: re-run `verify_lab.cjs` after EVERY edit to the page, however small, and before you report it. Once it is live, download the published page (`Artifact read` with `path: index.html`) and load it headless; any `pageerror` fails it. Keep apostrophes out of single-quoted HTML-in-JS strings, or build that text with `textContent`.
- Evidence: the published version 2 against version 3 of https://claude.ai/artifact/Hequw1VLdwXi2ztrDeuXQC.
- Promoted: no

### L-STU-133 — An edge-band average under-reads a sparse, moving 3D effect; a GLSL-to-HLSL port needs its own compile gate
- Lab: Silence Vignette Lab, round 2 (the 3D perimeter) · Branch: `cece/sleepy-shannon-9qk646` · By: session_01PEp87yJAmoDHUn7QvRdtkg · Date: 2026-10-21
- What happened: the round-1 yardstick averages the colour change over the outer 12% of the frame. That scored the full 2D frame at 45 and the 3D perimeter at 5 to 17, although the shards read plainly on screen. Their motion and the hits' jolt are not in a still frame at all. Separately, the HLSL port of the lab's GLSL named a variable `line`, which HLSL reserves. WebGL had compiled the GLSL happily, so the lab could not catch it.
- Do instead: label a look's column with what it cannot see, and judge a sparse or moving effect by eye before its number. Relax the yardstick check to "beats the invisible baseline", not "beats the full frame". Give a shader port a compile gate of its own (`Tools/Shaders/verify_silence_perimeter.py`: glslang in HLSL mode against a mock of the URP calls, plus a negative control), and keep the variable names identical in both twins.
- Evidence: `Docs/Studios/SILENCE_VIGNETTE_LAB.md` round 2; `Tools/Shaders/verify_silence_perimeter.py`.
- Promoted: no

### L-STU-134 — The lab's draw ORDER is part of the look: port the compositing, not just the per-pixel math
- Lab: Silence Vignette Lab, round 4 · Branch: `cece/sleepy-shannon-9qk646` · By: session_01PEp87yJAmoDHUn7QvRdtkg · Date: 2026-10-21
- What happened: Garrett picked "the pull plus the 2d edge" as "very strong". In the lab the 2D edge is painted into the frame BEFORE the WebGL shards copy it, so the pull drags the edge and every facet refracts it. The game drew the same edge as a Screen Space Overlay canvas, AFTER the camera, on top of the shards. Every per-pixel function was under a parity gate and matched, but the game would still have shown a different, flatter picture: an unwarped red frame over the distortion. Nothing measured the order the layers were composited in.
- Do instead: when a lab composites several layers, write the order down (which layer samples which) and port it as a structure. Here the edge became pass 2 of the game's own perimeter pass, drawn before the copy, and the order is pinned by a test on the shader's pass indices. A per-function parity gate cannot see a layering difference: check it by reading the lab's render loop beside the game's pass list.
- Evidence: `SilencePerimeterPass.RecordRenderGraph` (edge, then copy, then warp and shards); `SilencePerimeterTests.TheShaderDeclaresItsPassesInTheOrderThePassDrawsThem`; `Docs/Studios/SILENCE_VIGNETTE_LAB.md` round 4.

### L-STU-118 — In a multi-team lab, step the teams in a rotating order: a fixed order decides the ties
- Lab: Crew of Four Lab (`Docs/Studios/CrewOfFourLab.html`) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: every crew was stepped in the order of the `crews` array, so crew 0 (the player's) moved, picked up and blasted first in every step. In mirror Breach matches the Corsair won 85% over 20 seeds: two skiff charges in one fortress core, and the first blast processed took the prisms both reached. With the crews (and the blasts) taking turns going first each step, the same run read 25%, and the other mirror matches moved by 15-30 points too.
- Do instead: in any lab with several teams or crews, rotate the step order (`order = crews.slice(step % n).concat(crews.slice(0, step % n))`) for movement, pickups and area effects, and run a 20-seed mirror match as a fairness check before quoting win rates. A mirror match that is not near 50% is a stepping bug until shown otherwise.
- Evidence: `CrewOfFourLab.html` `simStep` (the `order` array) and `stepBlasts`; `Docs/Studios/CREW_OF_FOUR_LAB.md` §4 Found 8.
- Promoted: no

### L-STU-135 — A "seat value" column needs a floor under its denominator, and a won-or-lost game needs more seeds
- Lab: Crew of Four Lab (`Docs/Studios/CrewOfFourLab.html`) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: seat value (points lost when one station is left empty, as % of the full crew's points) read −1319% and −2164% in Gate Run, where points are 100+ for an escape and a few for a loss, so a 3-seed base of 6-11 points turned one lucky escape into thousands of percent. Rookie share read 2344% the same way.
- Do instead: divide by max(|points|, a per-game scale) (`POINT_SCALE`), give a binary-outcome game more seeds (12 against 6), print the standard error beside every points column, and say in the write-up which seat values sit inside the noise.
- Evidence: `CrewOfFourLab.html` `POINT_SCALE`, `scoreOpts`; `CREW_OF_FOUR_LAB.md` §4 "Noise".
- Promoted: no

### L-STU-120 — Taking a lab from 2D to 3D: shrink the arena by volume, and hide the prisms between the chase camera and the hull
- Lab: Crew of Four Lab (`Docs/Studios/CrewOfFourLab.html`, round 2) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: the round-1 2D model went to a 3D sphere at the same 420 u radius. Encounter rates collapsed (the ball holds about 560 times the disc's area in volume): Anchorage crews never met (0 hits in 90 s) and a Core waited the whole match. Separately, the chase camera at the Sparrow's followOffset (0, 10, −50) sat inside the hull's own trail, so trail prisms streamed into the lens and filled the screen.
- Do instead: when a lab gains a dimension, re-measure the encounter columns before anything else and shrink the arena (here to the game cell's 320 u) or raise densities; give AI a reason to go where the others are (an Anchorage raid phase). Under a chase camera, skip drawing prisms inside a corridor from the camera to the hull, as the game's `PrismOcclusionCorridor` does (`Docs/PRISM_ANIMATION.md` §4.7).
- Evidence: `CrewOfFourLab.html` `SHIPPED.arenaR`, `helmAI` (Anchorage raid), `draw3d` (the corridor test); `Docs/Studios/CREW_OF_FOUR_LAB.md` §4 round 2, Found 6.
- Promoted: no

### L-STU-121 — A respawn that keeps re-rolling a crystal's kind lets the uncollected kind crowd the sought one out
- Lab: Crew of Four Lab (`Docs/Studios/CrewOfFourLab.html`, round 3) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: each respawned crystal rolled omni with probability 0.25. The AI collected omni crystals (they refill missiles) and ignored elemental ones, so elemental crystals sat in their slots forever and never respawned, and within a minute the arena held 13-14 crystals and 0 omni. The magazine read "empty 92% of the match" and looked like an economy-tuning problem; the fix was a slot that keeps its kind (`newCrystal(w, omni)`), after which the same settings read 69-80% and the tuned ones 31-42%.
- Do instead: when pickups of several kinds share respawn slots, give each slot a fixed kind (or respawn on a timer whether collected or not), and dump the live counts by kind in a trace before tuning the economy around a scarcity the spawner caused.
- Evidence: `CrewOfFourLab.html` `newCrystal`, `buildWorld` (`nOmni`); `Docs/Studios/CREW_OF_FOUR_LAB.md` §4 round 3, "What changed".
- Promoted: no

### L-STU-122 — A shared resource drained by AI teammates starves the human seat; reserve some for the person
- Lab: Crew of Four Lab (round 3) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: the three gunner seats share one magazine. In a scripted run the human's first Release did nothing because the two AI gunners had spent the last missiles in the same second; every human gunner would meet that. A Release pressed during the 0.6 s reload was also silently lost.
- Do instead: in any lab where AI crewmates share a pool with a person, make the AI leave a reserve for each human seat (a dial, `aiReserve`, so the designer can turn it off) and buffer a person's button press briefly across a cooldown. Test the human seat through the real key path, not only by setting the input object.
- Evidence: `CrewOfFourLab.html` `runCrew` (`reserve`, `t.queued`); the round-3 write-up's "What changed".
- Promoted: no

### L-STU-123 — Stabilize a mounted camera in world space, then measure the sway nobody asked for
- Lab: Crew of Four Lab (round 4) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened: turrets aimed in the hull's local frame swung every gunner's view at the helm's turn rate (47–54 °/s). Moving the aim to a world-space direction the gunner turns, holding each turret's own horizon and easing it toward the hull's up over 3 s, and mounting the turrets on outriggers whose position follows a lagged copy of the hull's frame cut that to 3–6 °/s. The first sway metric also counted the AI gunner's own aiming, so it barely moved. And a 3-seed run showed a large points gain (Anchorage 126 against 54) that 12 seeds erased.
- Do instead: for any camera or weapon mounted on a moving body, keep the aim in world space and let only the seat's own input turn it; hold the horizon and ease it, never lock it; let the mount position lag on a smoothed frame. Measure "sway" as the view's rotation minus the rotation the seat's own input commanded, and keep the hull-locked version as a control preset. Check any points gain from a feel change at 12 seeds before reporting it: an AI does not feel sway, so a feel fix should leave AI points unchanged.
- Evidence: `CrewOfFourLab.html` `turretDir`, `mountPos`, `followFrame`, the `swayDegS` score; `CREW_OF_FOUR_LAB.md` §4.
- Promoted: no

### L-STU-124 — Porting a lab: write element factors as the game's channel, lift inline rules into named functions, and measure small angles with atan2
- Lab: Crew of Four Lab (graduation as the Orca) · Branch: `cece/happy-noether-m54hz6` · By: a Claude Code session for Garrett · Date: 2026-10-20
- What happened:
  - The lab scaled four numbers as `1 + k × level / 15`, while the game's element channel is `ElementalFloat`
    (`LerpUnclamped(Min, Max, level / 10)`). Porting the factor as code would have left `element_ability_table.py` reading
    "NO SCALING" on two of the hull's four elements.
  - Three rules lived inline in the lab's step function (the horizon hold, the shield's shape, the missile step), so a
    parity harness had nothing to call.
  - The C# sway readout returned 0 every step: `acos(dot)` of a one-step turn rounds to zero in float32 (the page is
    float64).
- Do instead:
  - A lab factor of that shape IS an `ElementalFloat`: Min = base, Max = base × (1 + 2k/3). Port it as one, and have
    the parity gate check Min and Max against the page's SHIPPED base and k.
  - Before porting, lift every rule the harness must hold into a named pure function, expose them on
    `window.__lab.parity`, and prove a batch is byte-identical before and after.
  - Measure angles in a float port with `atan2(|a × b|, a · b)`, never `acos(a · b)`.
- Evidence: `OrcaCrewConfigSO.cs` (`boostRefill`, `shieldSeconds`, `blastRadius`, `missileScale`); `CrewOfFourLab.html`
  `holdHorizon` / `shieldDirs` / `flyMissile`; `OrcaTurret.HoldHorizon`; `Tools/Build/orca_crew_lab/parity.cjs`.
- Promoted: no

### L-STU-144 — A gather that chases a moving leader at its own speed never docks: dock kinematically
- Lab: Omega Crews Studio (`Docs/Studios/VesselStudio/omega.html`) · Branch: `Ys-bleeding-edge` · By: the combiner-crew session (session_01JZzEha3L2L4aMuJYzunPm3) · Date: 2026-10-26
- What happened: the combine was written as "each member flies to its slot round the caller"; the caller kept flying at cruise and the members, at the same cruise, circled it "gathering" for the whole 540 s limit (the trace showed form gather / mega flapping, spread 50-150 u, zero combines). A completion threshold of 8 u on a target moving at 60 u/s is never met by a steered body.
- Do instead: once a gather is called and everyone is in range, the dock is an ANIMATION on a clock (1.6 s): the caller flies, the others are carried onto their slots (lerp position, slerp attitude, inherit the caller's velocity) and the form switches when the clock runs out. The steered approach is only for closing to gather range; while calling, the AI caller holds back at half cruise so the followers can close.
- Evidence: `omega-studio.js` part B `stepWorld` (form 'gather'); the 10 s trace (`omega_trace.cjs`, scratch) before and after.
- Promoted: no

### L-STU-145 — An objective window that skips a gate lets the crew do everything else first and leaves the gates for one long run
- Lab: Omega Crews Studio · Branch: `Ys-bleeding-edge` · By: the same session · Date: 2026-10-26
- What happened: "three crystals lit = the first three untaken" let the members collect every open crystal round the whole course while the locked ones (behind walls) stayed lit and untouched; the crew then faced eight walls in a row with one omega and a draining meter, and the "combine only when near the locked crystal" rule never fired because nobody flew toward it. Scorecards read DNF across the board, and it looked like an AI bug.
- Do instead: anchor the window at the first untaken objective (lit = the next three in order), so a gated objective gates the course; send idle members to the gate's approach point; decide the combine from "the locked one is near, or is all that is lit".
- Evidence: `relight`, `assignTargets` (`toWall`), `wantCombine` in `omega-studio.js`; CONCEPT_FLEET.md section 10.1.
- Promoted: no

## NCA — swarm / neural-CA research rigs (Tools/NCA)

### L-STU-146 — A combine bolted onto autonomous vessels has no loop; design the parts so neither phase can do the other's work
- Lab: Omega Crews Studio (Docs/Studios/VesselStudio/omega.html) and Siphonophore Studio (siphonophore.html) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: the Omega lab assembled three shipped vessels (each with a complete loop) into a combiner; its scorecard found the Warden's omega never had a reason to split (always combined beat its crew), and the only lever was a meter. Yash's principle the next day: a Voltron vessel is designed from the ground up to be many vessels and one vessel at alternating parts of its own loop. The Siphonophore gives each part ONE verb (light, open, net) and the colony the only scoring verb (digest); both scorecard controls (never fuse, always fused) DNF on every course for the designed reason, with no meter at all.
- Do instead: before building a combiner, write the two controls first and ask what each form CANNOT do; if a meter is the only thing that forces the alternation, the design is assembled, not designed. The three Unity crews built from the assembled design were removed the same day (CONCEPT_FLEET.md section 10.5).
- Evidence: CONCEPT_FLEET.md sections 10.1, 10.4, 11.3; siphonophore-studio.js wantFuse / stepColony; the scorecard rows I1 / I2.
- Promoted: no

### L-STU-147 — A slow turner that orbits its target inside its own turn radius never lines a shot up: stand off, then attack straight
- Lab: Siphonophore Studio (siphonophore.html) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: the Sting (turn 85 deg/s, slowed to 33 u/s over its crystal) circled 5-45 u from the crystal for 500 s with a heading error of 23-149 deg and fired nothing; a "come round" aim beyond the crystal only tightened the orbit.
- Do instead: an AI that must align a line on a target keeps a standoff state: closer than 45 u and more than 12 deg off, fly AWAY to 95 u, then come back straight and fire within 12 deg; the omega lab's cannon AI (L-STU-145's neighbour) had the same shape.
- Evidence: siphonophore-studio.js zooidAI (B.K.standoff); the trace before and after (siph_dbg.cjs in the session's scratch: uses 19 for 60 s, then crystal 5 freed in 3 s).
- Promoted: no

### L-STU-148 — The second lab that needs a shell copied it; the shell is owed to studio-crew.js
- Lab: Siphonophore Studio (siphonophore.html) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: siphonophore-studio.js copies the Omega page's track, grid, prism, rendering, camera, input, panel and hook parts (about 60 percent of 900 lines) and rewrites the design parts, because extracting a shared shell from a page another session was publishing the same hour risked both. SKILL.md section 7 says the SECOND lab extracts the shell; this is that lab and it did not.
- Do instead: the next session that touches either page moves the generic five parts (course, grid, prisms, bodies' rendering, cameras, input, transport, decision log) into a shared studio-crew.js listed under `shared` in studios.json, and both pages load it; until then a bug fixed in one page's shell is fixed in the other by hand.
- Evidence: diff omega-studio.js siphonophore-studio.js (parts A tail, D and E are near-identical).
- Promoted: no

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

### L-NCA-10 — A lab that ports a director must reproduce one harness result before any of its own flags are read
- Lab: Tandava Form Lab (`Docs/Studios/TandavaFormLab/`) · Branch: `claude/tandava-arena-mode` · Date: 2026-10-25
- What happened: the lab's JS port of `TandavaDirectorCore` drives a stand-in flock, not the sort core. Its first flags (a hatchling that cannot heal, a spine cut forty times a cross cut, two wingmates breaking the dance in 9 s) were worth reading only because the same port first reproduced harness T7: unopposed, the creature completed in 210-237 s over 9-10 meals, inside the C# run's band.
- Do instead: name one harness test the lab must match, check it after every director change in both languages, and file each new lab result as an Editor QA item with the lever that would move it, not as a defect.
- Evidence: `TANDAVA.md` §4.1, QA-TANDAVA-15/16; `lunge_check.js`.
- Promoted: no

### L-NCA-11 — A pose the director holds briefly must be timed by the director, not baked as a loop
- Lab: Tandava Form Lab + `swarm_core_harness` T19 · Branch: `claude/tandava-arena-mode` · Date: 2026-10-25
- What happened: the Antlion's snap was first baked as a looping gape-snap plan. The members could not follow the loop at the swarm's speed, so the jaws barely moved in the harness. Directing gape and snap as two static poses, with the director committing the snap 0.5 s before contact and holding it 1.3 s, closed the jaws. That only worked once the lunge poses were also given a per-plan well clip of 5 (`SwarmSortParams.PlanWellClip`), so the members could keep up with a 144 u/s charge.
- Do instead: bake a fast tell as separate poses and let the director time it. Measure the live swing against the plan's (T19 asserts at least half).
- Evidence: `TandavaDirectorCore` `Snapping`/`_snapUntil`; harness T19; `TANDAVA.md` §8 `PlanWellClip`.
- Promoted: no

---

### L-STU-149 — An AI that holds a tap button down after a miss never presses again
- Lab: Siphonophore Studio (siphonophore.html) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: the Bract's net casts on a fresh RT press. Its AI held RT while "want" stayed true, so after one cast that caught nothing (a misjudged crystal) it never cast again: a run stalled at 6 of 24 crystals for 450 s, the Bract circling its freed crystal 10 u away with RT held.
- Do instead: for any edge-triggered ability, the AI's "want" includes the cooldown (or releases for a frame after each press), so a miss is followed by a new press. Look for "rt held, cool 0, never fires" when a run stalls.
- Evidence: siphonophore-studio.js zooidAI ("a cast is a tap"); the stall probe in the round 2 commit 1039d5ecdc.
- Promoted: no

### L-STU-150 — Count how often the AI USES an ability, not only the finish time; a rule from another ability can starve it
- Lab: Siphonophore Studio · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: the Pulse Jet changed nothing in the scorecard. Its use count showed why: the AI Bell jetted 8 times a run, because Slipstream (built one step earlier) blocked the pulse while leading and the Bell led most of its travel. Allowing the jet while leading took it to ~115 jets a run and I1 Hard from 343 to 332 s. The Ram Spool's count (about one ram a run, no red ram) showed the Lance makes the ram redundant.
- Do instead: every ability added to a lab reports its own use count (and hits, chains, steals) in the headless result next to the finish time; a flat finish time with a near-zero count is an unused ability, not a neutral one.
- Evidence: runHeadless extra { jets, onBeat, chainTop, lashes, hits, stolen, rams, redRams }; CONCEPT_FLEET.md 11.5 steps 3 and 4.
- Promoted: no

### L-STU-151 — A crew loop is chaotic: two or three seeds mislead; use eight and quote mean and spread
- Lab: Siphonophore Studio · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: with three seeds, Mass level 5 plus the lash read 60 s faster than Mass 5 alone (392/356/354 against 433/421/415); with eight seeds and the coiled-steering fix the two were 344 and 350, inside one standard deviation. Three AIs, a fuse policy and a wall rhythm make tiny changes move whole runs by 30-60 s.
- Do instead: compare variants in the same build behind a run option, on eight seeds, and quote mean and standard deviation; a difference smaller than the spread is a plateau. Remove the option once decided.
- Evidence: scratch siph_mean.cjs; CONCEPT_FLEET.md 11.5 step 2.
- Promoted: no

### L-STU-152 — A "clean run" measured in seconds punishes the fast player; measure it in distance
- Lab: Sea Turtle (seaturtle.html, kind `tide`) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: the Rapids burst needed 0.8 s through the Rapids without touching a rock. The AI flying the mechanic (tucked, fast) earned 0 bursts a run; the control without the mechanic (slower) earned 6.5. A tucked turtle crossed the Rapids in about 0.5 s.
- Do instead: a reward for getting THROUGH something is measured in distance (here 30 u), never in time, or the better pilot is the one it stops paying.
- Evidence: studio-kinds.js K.tide (`rapidsLen`); CONCEPT_FLEET.md 12.2.
- Promoted: no

### L-STU-153 — When a side mechanic loses to doing nothing, tie its reward to its story before tuning its numbers
- Lab: Seahorse (seahorse.html, kind `tend`) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: holding a plant to call the crystal in was slower than swimming (282 s against 179); doubling and nearly tripling the call speed barely moved it, because the cost was the 111 s spent flying to plants beside the course. Making a crystal a plant calls in count two (it grew in the garden, which is the vessel's story) took it to 155 s against 179.
- Do instead: split the mechanic's time (the probe: holding / going to it / everything else) before touching its numbers; if the cost is the detour, no speed number fixes it, and the reward has to be worth the detour.
- Evidence: studio-kinds.js K.tend (`gardenWorth`); CONCEPT_FLEET.md 12.2.
- Promoted: no

### L-STU-154 — Adding choices to someone else's lab: every default theirs, the baseline proven identical, and a copier for the hub
- Lab: Crew of Four Lab round 9 (CrewOfFourLab.html, the Orca, Garrett's) · Branch: Ys-bleeding-edge · By: session_01JZzEha3L2L4aMuJYzunPm3 · Date: 2026-10-27
- What happened: play-style options went into a lab another collaborator owns and ported to C#. Each option is a dropdown whose first entry is the shipped behaviour (`styleBoost: 0`, `styleShield: 0`, `styleMissile: 0`), and every new branch in the step is gated on a non-zero value, so the shipped preset's 4-game batch was re-run after each change and stayed byte-identical. A self-contained lab also failed the hub's page rules (no `studio-theme.js`, no Sync tag, the header naming the host shell), so the hub page is a COPY made by `copy_orca.py` (the `copy_stoat.py` pattern) with `--check`, never a hand edit of the owner's page.
- Do instead: for options in a collaborator's lab, gate on an opt-in key, prove the baseline unchanged, and read rows where the AI never uses the option (the breath where the helm AI never boosts) as "no data", not "no difference". To list such a lab in the hub, copy it with a checked copier.
- Evidence: CrewOfFourLab.html `STYLES`, `styleBoostStep`, `dockStep`, `seekLock`; .claude/skills/vessel-studio/copy_orca.py; CREW_OF_FOUR_LAB.md round 9.
- Promoted: no

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

### L-SIM-2 — Model URP's bloom from its source, not from a doc's table: the threshold is gamma, the clamp per channel
- Lab: crystal bloom preview (`Tools/Build/crystal_bloom_preview.py`) · Branch: `cece/charming-bell-m70ac8` · Date: 2026-10-27
- What happened: the question was whether the free crystals still read as distinct limes after the gameplay bloom. URP 17's `PostProcessPassRenderGraph` passes `Mathf.GammaToLinearSpace(threshold)` (0.2 → 0.033 linear), hard-codes the knee at half that, and `Bloom.shader` clamps each channel to `clamp` (0.5) BEFORE the threshold. The per-channel clamp is what pushes a yellow-green toward yellow in its glow; a luminance clamp would not. The URP source was already on disk in the reference-compile cache (`/tmp/unity_refcompile_cache/graphics/Packages/com.unity.render-pipelines.universal`).
- Do instead: port the effect's passes from the package source (prefilter, the mip chain with its sizes from the FRAME, the scatter lerp, the composite), read the profile's overridden values from its asset, and convert material and property-block colours gamma → linear as Unity does in a Linear project.
- Evidence: `crystal_bloom_preview.py` `bloom()`; `Docs/PALETTE.md` §2.11.
- Promoted: no

### L-SIM-3 — Judge a glow's colour from the light it ADDS, and take its hue in sRGB
- Lab: crystal bloom preview · Branch: `cece/charming-bell-m70ac8` · Date: 2026-10-27
- What happened: two wrong measurements, each of which flipped the verdict. Classifying the composite pixels called every crystal's far halo green, because a dim glow over the navy sky borrows its blue. Taking HSV on LINEAR values called the omni 100% green (`#7cfc00` is 90° in sRGB HSV and 108° in linear). The lime window (75-105°) is defined on sRGB colours.
- Do instead: subtract the backdrop, weight by the added luminance, normalise each pixel's added light to its peak, encode to sRGB, then take the hue. Sanity-check the classifier on a colour whose answer you know before trusting a table of them.
- Evidence: `crystal_bloom_preview.py` `classify()`.
- Promoted: no

### L-SIM-4 — A render that is linear in its colours is three renders; search over the table, cached
- Lab: crystal bloom preview · Branch: `cece/charming-bell-m70ac8` · Date: 2026-10-27
- What happened: the shade search needed each crystal at hundreds of candidate colours. The shading is linear in the bright and dark colours and the supersample is a mean, so `img = B + Ab·bright + Ad·dark` from three renders gives every candidate before the (non-linear) bloom. The first full search still took about 20 minutes, and each change to the search's RULES (characters, distinctness) re-ran it, until the per-candidate measurements were cached keyed on the candidates, the bloom settings and the looks; after that a rule change runs in seconds.
- Do instead: separate the expensive per-candidate measurement from the cheap combination search, cache the first, and expect the rules to change more often than the renders.
- Evidence: `crystal_bloom_preview.py` `_components()`, `proposal_cache.json`.
- Promoted: no

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

### L-STU-139 — A baseline that can reach the payoff another way measures nothing; and a negative control must be shown to fire
- Lab: Goby & Pistol Studio (`Docs/Studios/VesselStudio/goby.html`) · Branch: `cece/sharp-tesla-j0kd7z` · By: session 01KpzppLj3ETY5c6jEP7HuyA · Date: 2026-10-14
- What happened: two yardstick bugs, both caught only because the table looked odd. (1) The "No Pistol" baseline (the Goby alone) scored 17 CHAIN kills, a column meant to be the Pistol's alone: the Goby's flares were killing primed blocks, and every kill of a primed block set off the chain. The baseline was not "without the Pistol"; it was "with a Pistol built into the flares", and the pair's advantage was understated by the whole column. (2) The Unroll test's first negative control, a hairpin, reported 0% ambiguous in every unroll: its two legs part along the ribbon faster than the screen window that the mapping searches, so the fold it was meant to show never entered the search. A flat circle (every lap lies on the last) reads 100%.
- Do instead: for every baseline, list the paths by which the effect it removes could still happen, and close them (a flare now takes only its own block; a prime is the Pistol's to detonate). Run every negative control once and check that it FIRES before reading any other row; a control that passes clean proves only that the test cannot see the fault.
- Evidence: `goby.html` `kill()` (`by !== 'flare'`), `PATHS` (`circling (control)`); README § "Goby & Pistol, round 1".
- Promoted: no

### L-STU-140 — A warped view keeps rendering parity by warping only TRANSFORMS; rotate the infinitely far sky by the frame
- Lab: Goby & Pistol Studio (`Docs/Studios/VesselStudio/goby.html`) · Branch: `cece/sharp-tesla-j0kd7z` · By: session 01KpzppLj3ETY5c6jEP7HuyA · Date: 2026-10-14
- What happened: the Pistol's sidescroller (a 3D world unrolled into 2.5D) was first drawn as flat 2D rectangles, and read as a different game from the 3D view beside it. Redrawing it in three.js with the SAME geometry and material objects as the 3D view (shared across the two renderers), and warping only each object's position and rotation (flat = Mᵀ · world, with M the ribbon's frame [T U R] where the object maps), gave parity at no art cost. The sky and stars needed one rotation, the frame at the viewer, because only direction matters for something infinitely far. A side camera on the +R side, looking along -R with up U, puts the path's +T to the right (the -R side mirrors it, a trap: T × U = R).
- Do instead: for any remapped or warped view of a lab's world, reuse the world's meshes and materials and move only transforms. Turn sky, stars and lights by the frame at the viewer. Check the handedness of the frame before placing the camera.
- Evidence: `goby.html` `renderSide3D`, `flatFrame`, `ribFrame`.
- Promoted: no
### L-STU-138 — A 2.5D character on a tangent plane needs its plane pinned to the path in depth, and a ground that steps off folds instead of snapping
- Lab: Goby & Pistol Studio (`Docs/Studios/VesselStudio/goby.html`, the Tangent plane) · Branch: `cece/sharp-tesla-j0kd7z` · By: session 01KpzppLj3ETY5c6jEP7HuyA · Date: 2026-10-21
- What happened: replacing an unrolled sidescroller with a plane tangent to a 3D path (no warp, a real camera), the first build kept the plane through the Pistol, turned it toward the path's normal and projected the trail's hinges onto it as the ground, dropping hinges whose screen x went backwards. Traced over a 60 s run, an airborne Pistol drifted 100-1000 u off its trail and then rode a phantom flat ground, because nothing held the plane on the trail in DEPTH while the path turned under it; and the monotonic filter emptied the ground at folds. Fixed in three parts, each measured: pin the plane's depth to the trail point under the Pistol (moves the plane, never the world); keep the projected ground in path order and pick, at an x, the stretch nearest the Pistol ALONG the path; treat a step in that ground as a ledge to run off (and pull a Pistol under its line back up), never a snap. The grounded Pistol then stays within 15 u of its trail. Also: the "along" direction from a plain chord flips at a hairpin (its far leg is out of plane); weight each tangent by how much of it lies in the plane.
- Do instead: before tuning anything, trace the character's distance to the thing it is meant to stand on, every step of a long seeded run, and fix every jump before judging the look. A per-step slope or turn statistic hid this: the averages looked fine while the Pistol rode nothing.
- Also paid twice here: an `old_str -> new_str` replacement that appends a `// comment` to a line whose CODE continues after the matched text swallows that code (a loop body, a clamp). The page still parses; the symptom is a quiet wrong number. Put new comments on their own line.
- Evidence: `goby.html` `planeStep` (the depth pin), `planeGround` / `groundAtPlane`, `pistolStep` (`ledge`, `under`), `planeTarget` (weighted tangents), `planeMeasure`; the session's run traces.
- Promoted: no

### L-STU-117 — To hide a projection's warp, draw objects rigid and fade the bend in only where the stretch shows; smoothing the path's positions makes it worse
- Lab: Goby & Pistol Studio (`Docs/Studios/VesselStudio/goby.html`, the Settled unroll) · Branch: `cece/sharp-tesla-j0kd7z` · By: session 01KpzppLj3ETY5c6jEP7HuyA · Date: 2026-10-18
- What happened: asked to make the Pistol's warp "only where necessary and as invisible as possible", the first try eased the unroll's SPINE (a gaussian over the path's positions where it curved tighter than a radius). Measured over the same 3000 world points on seven scripted paths it did not help: drawn-bent prisms stayed at 98-100 % on curving paths, and climbs gained bends. Smoothing a curve's POSITIONS pulls a sustained arc toward its centre, which TIGHTENS it. What did help: every prism near any curve had been drawn BEND (stretched through the unroll by 1 + d x curvature, ~1.5x at d 45 on an 80 u turn); drawing each prism RIGID and blending toward the bend only where its stretch exceeds 12 % (full by 35 %) cut drawn-bent prisms to 48 % on the weave and 57 % on the roll, folds unchanged. A height PROFILE over distance smooths without tightening, so the ground's sharp vertical curves are eased that way, settling ahead of the rider and frozen once reached (measured: no settled sample moved, the ground under the rider never moved).
- Do instead: measure what the viewer sees (how many objects are drawn distorted) before choosing an operator, never a proxy like the path's tightest radius. Ease a graph (height over distance), not a curve's positions. To hide a map projection's distortion, keep objects their true shape and fade the warp in by the local stretch.
- Evidence: `goby.html` `softAdd` / `softPoint` / `softBend`, `drawPrism` (the rigid blend), `unrollMeasure` (`bentPct`, `bendMax`, `lateMove`); the session's warp, unroll and freeze measures.
- Promoted: no
### L-STU-141 — A blank panel in a headless full-page screenshot of a two-WebGL-canvas lab is the capture, not the page
- Lab: Goby & Pistol Studio (`Docs/Studios/VesselStudio/goby.html`) · Branch: `cece/sharp-tesla-j0kd7z` · By: session 01KpzppLj3ETY5c6jEP7HuyA · Date: 2026-10-18
- What happened: `verify_lab.cjs`'s desktop and phone screenshots showed a white-and-black panel, the size of the Pistol's inset, in the lower right of the main view. `elementsFromPoint` found no element there and no code draws there. A CLIPPED capture of the same region (`page.screenshot({ clip })`) was dark and correct, and hiding either inset canvas changed nothing: headless SwiftShader mis-composites the second WebGL canvas's layer in a full-viewport capture.
- Do instead: before chasing a panel that only a full screenshot shows, capture the region with `clip` and ask `elementsFromPoint` what is there. A defect the clipped capture does not show is the harness's.
- Evidence: this ship pass's probes (scratchpad `probe3.cjs`, `probe4.cjs`); `verify_lab.cjs` PASS.
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

### L-STU-79 — A free-ended cord that must whip needs a WAVE EQUATION on its link directions, not pose springs on its points
- Lab: Brittle Star Studio · Branch: `cece/beautiful-knuth-j3atgk` · Date: 2026-10-16 · (L-STU-67 to 78 are claimed on
  in-flight branches; this id skips them)
- What happened: the first model for "cords like the Thresher's chain, without the weight" pulled each point toward its
  place on a straight pose line, capped lengths with follow-the-leader and re-read velocities from the move (PBD). It
  passed straight-line and frame-rate tests, but a node prototype showed the two things a whip needs were missing: a
  root flick moved every link at once (no travelling wave), and 30 s of random flying pumped node speeds to 4,000 u/s
  (FTL plus the velocity re-read injects energy).
- Do instead: model the cord as unit link DIRECTIONS laid end to end (inextensible by construction) obeying
  `u'' = k (u[i-1] - 2u[i] + u[i+1])`, the root direction fixed and the tip free; damping and a pose spring onto the
  straight line are the analog "stiffen" dials. Waves then emerge and reflect off the tip with no scripting. Prototype it
  in node first and measure three things before porting: when each link starts to move after a root flick (arrival
  order and delay), the peak swing over a minute of random flight (bounded), and the same flick at 30 / 60 / 144 fps
  (sub-step at a fixed h so they agree).
- Evidence: `Assets/_Scripts/Controller/Vessel/BrittleStarCord.cs` and its tests in `BrittleStarTests` (arrival order,
  saber straightness, no stretch, a minute bounded, 30 vs 144 fps); the studio's `cordStep` is the line-for-line port.
- Promoted: no

### L-STU-82 — A control that equals Hard to the decimal is a bug until shown otherwise, even when equality is plausible
- Lab: Shrike Studio round 1 (`Docs/Studios/VesselStudio/shrike.html`) · Branch: `cece/affectionate-clarke-s738vh` · Date: 2026-10-15 · (L-STU-67 to 81 are claimed on
  other branches; these ids skip them)
- What happened: Den Defense's "Hard, no array" column matched Hard byte for byte, twice, for two different bugs. First
  the AI let go of the paint the moment it turned to lance: of each burst only the first missile hit, the rest flew out
  their 8 s life, and the run fell at the same 209.3 s as the no-array run. Later, at I4, the boss-wave test `wave % 2 === 2` could never be true, so no creature ever spawned and
  nothing needed missiles. Both read as a believable finding ("the lance is enough") until the runs were traced.
- Do instead: when a control ties exactly, count what the ablated piece DID in the full run (missiles fired and what
  each one hit; creatures spawned). A real "worth nothing" shows the piece working and not mattering; a bug shows it not working.
  And test every modulo/latch against the values it must fire on (L-ECO-5 is the same family).
- Evidence: `Docs/Studios/SHRIKE.md` §5 finding 7; the trace scripts behind `Docs/Studios/Shrike/shrike_score.cjs`.
- Promoted: no

### L-STU-83 — A survival score decided by one event measures luck; make the loss a sum of many events
- Lab: Shrike Studio round 1 · Branch: `cece/affectionate-clarke-s738vh` · Date: 2026-10-15
- What happened: the first Den Defense ended the run when any asteroid reached the den's core. Five seeds of one cell
  spread from 75 s to 296 s, so Easy, Medium and Hard overlapped completely. Wearing the den down instead (a core hit
  breaks 3 of its 40 blocks; it falls with a quarter left) cut the band to about +-20 s, and the levels separated.
- Do instead: before reading a survival or "first failure" column, print one cell over five seeds. If the spread is
  wider than the gaps you hope to measure, change the loss into an accumulated quantity (labelled lab-only, and put
  to the designer) rather than adding seeds.
- Evidence: `Docs/Studios/SHRIKE.md` §3 (Den Defense) and §4.
- Promoted: no

### L-STU-84 — When an easier AI level beats Hard, find what Hard spends its aim on; change one thing per measurement
- Lab: Shrike Studio round 1 · Branch: `cece/affectionate-clarke-s738vh` · Date: 2026-10-15
- What happened: in Den Defense, Medium outlived Hard at I1 and I2 (288.9 s against 274.4 s, 242.4 against 228.8). Hard
  painted asteroids for missiles while its hitscan lance (0.48 s cooldown in a nest) could have killed them on the spot,
  and painting holds the nose on one target until the missiles land. The fix was the paint rule (paint creatures only).
  It was shipped together with a target-pick hysteresis, and the first learning written here credited the hysteresis.
  A control copy with the hysteresis removed showed the opposite: Hard still beat Medium and survived longer without it
  (I1 300 s against 283.4), so the hysteresis was taken out.
- Do instead: when an easier level wins, trace what the Hard pilot does with its time (here: frames spent painting
  against lances fired). Change one thing per scorecard run, and before you write down why a fix worked, re-run with
  each half removed (L-GEN-3 for your own changes).
- Evidence: `Docs/Studios/SHRIKE.md` §5 finding 3; `shrike.html` `aiDen` (`worth`).
- Promoted: no

### L-STU-85 — A lab publishes its own artifact; a hub's "one artifact" rule is the hub's, not every lab's
- Lab: Shrike Studio round 1 · Branch: `cece/affectionate-clarke-s738vh` · By: Garrett (owner), 2026-10-15
- What happened: the Vessel Studio's rule "never publish a second studio artifact" (its skill §0, a `CLAUDE.md` line,
  the vessel-work prompt hook) read as covering any lab about a vessel. So the Shrike lab, built with `/labmaker`, was
  steered into the shared hub: a catalog row, a hub card, and a publish blocked on reading someone else's artifact.
  The owner's call: "labmaker should be capable of making artifacts just fine ... I just want to explore here and then
  bring our insights into Unity."
- Do instead: publish every lab as its own private artifact (§6), with its own decision log. A lab page that loads a
  hub's shared scripts ships them beside it through `files` and drops the hub-only parts (the Shrike's
  `build_standalone.py` strips the Sync panel and the hub links, and refuses to build when they move). Joining a hub
  is optional, later, and its owner's decision. The three places the rule lived now say it is the hub's only.
- Evidence: `SKILL.md` §6 and §7; `CLAUDE.md` vessel section; `.claude/hooks/vessel_studio_nudge.py`;
  `/vessel-studio` §0; https://claude.ai/artifact/JD17sQMeuEUETp6AUPqMmy.
- Promoted: §6

### L-STU-86 — A vessel lab carries four minigames, one per element, and the element decides the kind
- Lab: all vessel labs (raised on the Shrike Studio) · Branch: `cece/affectionate-clarke-s738vh` · By: Garrett (owner), 2026-10-15
- What happened: the Shrike lab flew the two minigames its class document named (Thread the Needle, Den Defense). The owner
  set a fleet rule: "time is always a racing game, space a destruction game, mass a creation game, and charge a vessel on
  vessel dogfight", so every vessel's lab tests the kit against the same four kinds of play.
- Do instead: build the four kinds (`SKILL.md` §1.5). Slot a document's own game by its kind; build the missing kinds as
  lab proposals. The Shrike lab has the Time race (Thread the Needle); Den Defense is a defense game (not one of the four);
  Space (Bunker Buster), Mass and Charge are its next round.
- Evidence: `SKILL.md` §1.5; `Docs/Studios/SHRIKE.md` §6.
- Promoted: §1.5

### L-STU-91 — three.js r128 instanced meshes: colour before count, and turn culling off
- Lab: Angler Command Lab (`Docs/Studios/AnglerCommandLab.html`) · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: every creature drew grey, then not at all. `InstancedMesh.setColorAt` creates `instanceColor` sized from the mesh's CURRENT `count`, and the page had set `count = 0` first, so the buffer was empty (`instanceColor.array.length` 0) and nothing errored. Separately, r128 frustum-culls an InstancedMesh by its geometry's bounding sphere at the mesh origin, so creatures 450 u from the origin vanished whenever the origin left the view. The verifier passed both: a WebGL stage skips the blank check.
- Do instead: call `setColorAt` for every slot before lowering `count`, and set `frustumCulled = false` on any instanced mesh whose instances spread out. Expose a `gl()` hook (draw calls, instance counts, the first colour) so a probe can tell "not drawn" from "drawn black" without a screenshot.
- Evidence: `initGL` (`fmesh`, `trails.frustumCulled`); `window.__lab.gl()`.
- Promoted: no

### L-GEN-12 — Before calling a failing column a design finding, trace one failing run frame by frame
- Lab: Angler Command Lab, round 1 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the Pack sling failed 3–4 of 4 orders, which read as "the sling is a bad gesture". A per-second trace of one run showed two bugs first. The commander let go after one frame because it counted the gathering ball as "busy". Creatures that latched beside a prism circled it forever: at minimum speed their turn could not catch the bearing, and at d ≈ 0 "facing it" is undefined. With both fixed, the sling turned out to be the fastest scheme on blobs, and its remaining failure (every trail, one 24 u cluster per throw) is the real design finding. A per-order breakdown (`perOrderS`) is what separated the two.
- Do instead: when a variant fails, print its state machine every 0.5 s for one seed before writing it up. Report per-order times, not only a mean, so "fails on lines, wins on blobs" cannot hide inside "slow". For any seek-and-face creature, brake to a hover inside reach and count a meal in the mouth as faced.
- Evidence: `ANGLER_COMMAND_LAB.md` round 1 "Found"; `faunaStep` (the hover and `d < 3`); `squadBusy`.
- Promoted: no

### L-GEN-13 — An attribution column needs a decoy control and per-order state resets
- Lab: Angler Command Lab, round 1 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: "on target %" charges each kill to the CURRENT order. The commander recalled its squad only before the first order (a flag never reset per order), so creatures kept eating the last order's structure and every later kill read as off-target. That made precision look about 20 points worse than it was. A planted decoy (`runMission(…, decoy = true)`: the commander aims every order at the wrong structure) must read 0% on target, and it does on all five schemes. A diet control (`dietAny` 0: "yours lost" must go to 0) proved the friendly losses come from the mouthful, not the targeting.
- Do instead: reset every per-order flag in the function that starts an order. Plant a decoy that must drive the attribution column to zero, and a control that must zero each loss column you explain by one cause.
- Evidence: `nextTask` (`w.ai.recalled = false`); `runMission`'s `decoy`; `ANGLER_COMMAND_LAB.md` "Checked".
- Promoted: no

### L-STU-92 — A companion slower than the hull is invisible from the chase camera; measure follower lag
- Lab: Angler Command Lab, round 1 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the tamed squad runs at the game's fauna speeds (at most 40 u/s at L1), the same as the Angler's cruise, so it sat on average 79 u from the hull (max 102), behind a 40 u chase camera. Every screenshot showed no squad. A speed proportional to the gap added a steady ~27 u lag on top. The same speed gap broke taming: with the lure held at cruise, six creatures were never tamed in 90 s, against 14–19 s with the lure slowing the hull to 20 u/s or less.
- Do instead: in any lab where companions follow a vessel, measure their mean and max distance from it, feed the leader's speed forward in the follow law, and put the speed gap on the page as a design question (here a lure brake and a follower-only leash tow), never as a quiet speed-up of the creature.
- Evidence: `ANGLER_COMMAND_LAB.md` "What it says" (the last two bullets); `leashTow`, `formBack`, `lureCruise`.
- Promoted: no


### L-GEN-14 — A golden table from random inputs can miss a planted change; plant the change before trusting it
- Lab: Angler Command Lab, Unity clone · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the parity gate runs the page's `CORE` functions in headless Chromium on a seeded table of inputs and writes the results into the NUnit test as goldens. Its first `--self-test` planted six one-number changes in the C# port, and two of them passed every golden:
  - The claim penalty, changed from 900 to 800: no random pick case sat near enough to the boundary for the penalty to change the winner.
  - The esca's 2.5 u lift: every lure case used the world up vector, and the plant changed only the x component, which is always zero there.
- Do instead: plant every constant before believing a golden table. When a plant survives, add the cases that reach it, here pick cases with two candidates either side of one claim's cost, and a lure case with a general (non-axis) up vector. Make each plant change every component it can. The gate is only evidence once every plant fails it.
- Evidence: `Tools/Build/angler_command_parity/run.sh --self-test` (6/6), `goldens.cjs` (the boundary pick and tilted-lure cases).
- Promoted: no

### L-STU-93 — A nested enum named like a common word trips the repo's enum-member gate; name it for its owner
- Lab: Angler Command Lab, Unity clone · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the executor's per-creature state enum was first called `Order`, matching the lab's word. `check_enum_member_references.py` resolves `X.Member` by enum NAME across the repo, so every `Order.Add(...)` elsewhere (a list called `Order` in `ProfilerCapture.cs`) read as a reference to a missing member of the new enum. The gate went red on code the change never touched.
- Do instead: give a nested enum a name only its owner would use (`LeashOrder`), even when the lab calls it something shorter. Keep the lab's word in the doc comment.
- Evidence: `AnglerCommandExecutor.LeashOrder`; the gate's output before and after the rename.
- Promoted: no

### L-STU-94 — Port the lab's LOOK of a state with its rules; a marker at an object's centre is inside the object
- Lab: Angler Command Lab, Unity clone · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-15
- What happened: the lab draws a painted prism by recolouring the prism itself (75% toward white in `render()`). The Unity port kept the paint arithmetic line for line, but drew the state as camera-facing dots at each prism's centre. Every dot sat inside an opaque prism, so the first playtest reported that painting did nothing. Every gate was green, because no gate looks at pixels.
- Do instead: when a lab state is shown by changing an object's own look, port that look onto the object with the lab's numbers (here a per-prism lift toward white, one fading stamp per change). Then list in the verification entry what the player must SEE, not only what the code does. A world-space marker for a solid object goes on its surface or above it, never at its pivot.
- Evidence: `AnglerCommandLab.html` `render()` (the `col.lerp(WHITE, 0.75)` line); `Prism.SetColorLift`; `ANGLER.md` §1a.
- Promoted: no

### L-STU-95 — A SHIPPED key two schemes share moves both; name the key per scheme or report both rows
- Lab: Angler Command Lab, round 3 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-16
- What happened: the designer asked for the Sling painter's reach x20. `paintRange` is also the round-1 Spotlight's reach, and the AI commander's approach distance. Raising it moved three things: the Sling painter (85% on target, from 90%), the Spotlight (70%, from 80%), and how the scorecard's AI flies. A comparison scheme that was meant to stay put shifted with it.
- Do instead: before changing a SHIPPED value, grep every reader (`P.<key>`): schemes, the AI commander, the renderer. Either split the key per scheme, or re-run the whole batch and report every row that moved. Here the change was meant for both painters, so both rows are reported in `ANGLER_COMMAND_LAB.md` round 3.
- Evidence: `AnglerCommandLab.html` (`P.paintRange` in `brushStroke`, the spotlight's `aimPick`, the commander's reach); the round-3 scorecard.
- Promoted: no

### L-STU-96 — Removing a timeout can turn a harmless exclusion into a deaf unit; re-test every "skip this state" after it
- Lab: Angler Command Lab, round 4 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-17
- What happened: the throw had always skipped creatures still in flight, and holding RT gathered only idle and attacking ones. That was harmless while a homing throw gave up after 6 s. Round 3 removed the give-up (any distance, limited only by speed) and raised the reach to 3200 u. A creature thrown far away was then deaf to every new paint for many seconds, and the designer's next playtest said the fauna "didn't always listen". No scorecard row moved, because the scored commander never repaints mid-flight.
- Do instead: when a change removes a timeout or a cap, grep every `state === X` skip that the timeout used to bound, and give the page a headless repro of the interaction the skip blocks (here: throw, repaint mid-flight, throw again; count who obeys). Keep that repro as a check: before 24/24 stuck, after 24/24 obey.
- Evidence: `ANGLER_COMMAND_LAB.md` round 4; `commandStep` slingpaint (the gather now includes `ST.FLUNG`); `throwAtPaint`.
- Promoted: no

### L-STU-97 — The camera's height IS the aim's error; level the chase camera before you shrink a brush
- Lab: Angler Command Lab, round 5 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-17
- What happened: the designer asked for "accurate and smaller" paint cones. A beam from the nose, seen from a chase camera 56 u above the nose line, runs below the screen's centre, so whatever the pilot centred was not what got painted. Shrinking the cone alone would have made that miss worse. Putting the camera on the nose line (`chaseUp` 0) made the screen's centre the beam's axis, and then the small cone (3°) scored the painter's best on-target rate (87%).
- Do instead: before tuning the size of any aimed volume, check where its axis lands on screen from the shipped camera. Level the camera (or aim from the camera) first. If height is wanted for overview, raise it only in the states that do not aim (here: luring and dashing, eased in and out).
- Evidence: `ANGLER_COMMAND_LAB.md` round 5; `AnglerCommandExecutor.UpdateCameraLift`.
- Promoted: no

### L-STU-98 — An order scoped to ONE gesture's targets abandons the last gesture's; queue every live mark
- Lab: Angler Command Lab, round 6 · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-17
- What happened: each throw gave its creatures a set holding only that stroke's paint, and a new throw replaced it. Prisms still lit from an earlier throw were never eaten. The scorecard did not show it: its commander paints one target at a time. A headless repro did (two throws: 0 of 12 older lit prisms eaten before, 12 of 12 after).
- Do instead: when marks persist ("lit until eaten or erased"), the units should pick from ONE queue of every live mark, not from the gesture that issued them. Spread them with a claim count, then recency. Keep a two-gesture repro as a page check.
- Evidence: `AnglerCommandLab.html` `litQueue` / `pickTarget`; `ANGLER_COMMAND_LAB.md` round 6.
- Promoted: no

### L-STU-99 — Two kinds of bug the lab cannot catch: a world rule it waives, and a readout it shows but the clone does not
- Lab: Angler Command Lab, round 7 (a Unity playtest) · Branch: `cece/zealous-feynman-po8k8d` · Date: 2026-10-18
- What happened: "the QuadFish don't always listen" had two game-side causes.
  - The lab's world is domain-blind, so every creature eats every prism. The game's creatures refuse their own domain, the nucleus claim and their pen, and an order to such a prism left a creature hovering at it for good.
  - The lab shows the picked squad; the Unity HUD did not. One stray press of the squad button (on A, the Sparrow's Mass button) silently left every other kind out of each order.
  No lab check could fail for either.
- Do instead:
  - For each rule on the lab's not-modelled list, ask what a clone must do when the game enforces it. Here the holder asks the creature's own diet before ordering (`Fauna.LeashCanEat`).
  - Port every mode readout the lab draws (squad, selection, lock) together with the mode itself.
  - When the designer remaps buttons, check what the OLD button did. A habit press is how a hidden mode gets entered.
- Evidence: `R_VesselActions/ANGLER.md` §1k; `ANGLER_COMMAND_LAB.md` round 7.

### L-STU-102 — One cooldown for the skim and the hull hides every hull contact
- Lab: Concept fleet (`Docs/Studios/VesselStudio/studio-vessel.js`) · Branch: `Ys-bleeding-edge` · By: the concept-fleet session · Date: 2026-10-17
- What happened: the platform baseline inherited from the Squirrel studio cooled a prism for 0.6 s per pilot on a SKIM (12 u out) and used the same cooldown for the HULL contact, so a hull flying through a prism it had just skimmed registered nothing. Invisible in every studio whose hull avoids prisms; the Flea, whose mechanic IS the contact, never bounced (uses 2 in 300 s), and every concept's contact count read 0.
- Do instead: keep two cooldowns (`p.cool` for the skim, `p.hull` for the contact) and test the hull branch with a vessel that wants to hit things. The game's `SkimmerImpactor` / `VesselImpactor` are separate components; worth confirming they never share a cooldown.
- Evidence: the Flea's smoke rows before and after (uses 2 -> 111 on I1); `studio-vessel.js` stepPilot.
- Promoted: no

### L-STU-103 — A chain walker needs "ahead" and "not where I came from", or a cluster is a treadmill
- Lab: Concept fleet (`studio-kinds.js`, the Gecko's `nextOnChain`) · Branch: `Ys-bleeding-edge` · Date: 2026-10-17
- What happened: the Gecko picked the next prism as "the neighbour most along my course", which inside a 14 u cluster ping-ponged A -> B -> A, and once it had run past a segment's end the clearance lerp pulled it back to the end while the run pushed it forward: a hull at 130 u/s standing still for 60 s, finishing 1 crystal of 24. Only a per-0.5 s trace of pos / stuck / next showed it; the 3 s trace read as "running".
- Do instead: exclude the previous prism, require forward progress (dot > 0.3), and step to the next prism when the projection passes the segment's end, not only when the hull is near it. Trace position, not state, when a state machine reads healthy and nothing moves.
- Evidence: `probe_gecko.cjs` trace (34-42 s, dNext 21.3 constant) in the session; Gecko I1 Hard DNF -> 160 s.
- Promoted: no

### L-GEN-15 — Give every mechanic scorecard a "same pilot, mechanic forbidden" column
- Lab: Concept fleet · Branch: `Ys-bleeding-edge` · Date: 2026-10-17
- What happened: ten movement mechanics, one AI each. The column that decided most was not Easy / Medium / Hard but the Hard pilot with its mechanic switched off (`W.mech = false`: triggers ignored, passive bonuses off). It said NO to the Gibbon's straight-line swings (Hard 15% behind its own control on I1), YES to the other nine, and separated "the mechanic IS the engine" (Marlin, Octopus: the control cannot move) from "the mechanic is a bonus" (Eel +28%, Gecko +39%, Dragonfly +35%).
- Do instead: build the no-mechanic control into the engine from the first round (one flag the kinds honour), and read it before any tuning: a mechanic that does not beat its own control is decoration, however good it feels.
- Evidence: `studio-vessel.js` raceHeadless `{ mech: false }`; `CONCEPT_FLEET.md` section 4.
- Promoted: no

### L-STU-87 — A dive is an attitude, lift is perpendicular to the path, and a stick held for seconds is a loop
- Lab: Concept fleet (`studio-kinds.js`, the Albatross's `soar` kind) · Branch: `Ys-bleeding-edge` · Date: 2026-10-17
- What happened: eight AI attempts (aim heights, airbrakes, lift caps, contact rules) left the Albatross DNF on every course, because the PHYSICS was never audited: (1) lift acted along hull-up, so a pull-up with the nose 50 degrees above the path put ~90 u/s^2 against the velocity and bled 127 u/s to 54 in two seconds; (2) drag 0.0004 at 125 u/s gave a glide ratio of 1.6, so a dive bought nothing; (3) the updraft (22) outpulled the ballast (10) at neutral wings, and the ribbon made the whole line a column, so no dive got down through it; (4) the AI's dive ADDED a pitch push to the base aim (a full stick-up toward a point 120 u above the line), which never nosed down, and once made absolute it was a pitch RATE held for the whole dive, which looped the hull 400 degrees in eight seconds. The scripted probe held its stick the same way and its loop read as "physics".
- Do instead: audit energy first (E = v^2/2 + g*y, printed per second in free space far from the course) before touching the pilot; make lift perpendicular to the flight path (hull-up minus its along-track part) with the angle of attack as its magnitude, so only gravity and drag change the speed; set the glide ratio and the ballast as design numbers (a racer's dive must be a short plunge); give a scripted or AI dive an ATTITUDE through the aim controller (`ctx.aimAt(P, pos + flat * 120 - up * 100)`), never a held stick.
- Evidence: `probe_soar2.cjs` energy audit (dE -4.4k/s in a pull-up, half of it drag) and the Hard AI trace (diving=1, Ly=-0.70 for 32 s, speed 90 -> 56); smoke I1 Hard DNF -> 357 -> 266 s across the three fixes (`CONCEPT_FLEET.md` section 4).
- Promoted: no

### L-STU-100 — A mode switch must not read the input of the mode it is leaving: the press acts on what the screen showed
- Lab: Concept fleet, the Gibbon's two modes (`studio-kinds.js`, kind `tether`) · Branch: `Ys-bleeding-edge` · Date: 2026-10-18
- What happened: Garrett's space mode gives each stick to a tether (it aims it, then reels it), and a trigger press is the door into that mode. The first version aimed the hook with the stick in the frame of the press, but in that frame the thumbs were still STEERING (time mode), so a held pitch-up aimed the tether 45 degrees up and hooked nothing; the ghost ring had shown a different prism.
- Do instead: when an input changes meaning across a mode switch, resolve the switch frame with the OLD mode's reading (here: hook the prism the ring showed, from the time-mode neutral aim) and let the new meaning start on the next frame. Test it with a scripted pad that is already deflected at the press, not a neutral one.
- Evidence: `probe_given.cjs` "space, one tether": hooked never -> hooked at 0.02 s; `CONCEPT_FLEET.md` section 8.2.
- Promoted: no

### L-STU-101 — Two rigid ropes are one constraint (a circle about the anchors' line), not two constraints solved in turn
- Lab: Concept fleet, the Gibbon's bolas · Branch: `Ys-bleeding-edge` · Date: 2026-10-18
- What happened: projecting the velocity onto each rope's sphere in turn (Gauss-Seidel, 4 passes) kept both ropes exact (error 0.000 u) and still STALLED the hull: 1 degree swept in 3 s. A hull flying between two anchors planted ahead has almost no velocity along the only circle the two ropes allow, so the projections ate it; "rope error 0" read as success while nothing moved.
- Do instead: solve the pair as what it is, a rotation about the line A-B on the circle the two lengths define (x = (la^2 - lb^2 + d^2) / 2d along the axis, radius sqrt(la^2 - x^2)), with the speed redirected onto that circle at the hook. Measure motion (swept angle, distance travelled) beside any constraint error: a solver that freezes the body satisfies every constraint.
- Evidence: `probe_given.cjs` "space, two tethers": swept 1 -> 722 degrees in 3 s, rope error 0.000 u both times.
- Promoted: no

### L-STU-102 — A studio that draws a world effect as an additive flat sheet is deciding a UI look
- Lab: Brittle Star Studio, round 5 (the ring explosion) · Branch: `cece/gallant-bell-eq7kul` · Date: 2026-10-18
- What happened: the five-fold ring explosion was first built as flat ribbons: an annulus, a pentagon, five arms. The
  preview (a 2D top render) and the studio (a `MeshBasicMaterial`, additive, double-sided sheet) both showed it as flat
  vector art. Garrett: "good start but nothing in the environment should ever look flat like this, that is reserved for
  ui". The second cut, with beams a few units thick, still read as wire across a 780 u disk.
- Do instead:
  - Draw any world effect in the studio LIT and flat-shaded (`MeshStandardMaterial`, `flatShading`, an emissive tint),
    never as an additive sheet. The studio is where the look is decided.
  - Size every thickness as a share of the effect's own size.
  - Judge the look from a 3D, lit, perspective render at two angles, oblique and low near the plane, not from a top-down
    outline. A 60-line Python painter's renderer over the C# form's dumped triangles was enough here.
  - Keep the shape as a pure C# function with a line-for-line JS twin, and check parity by point sets (C# corners vs JS
    corners, same settings).
- Evidence: `BrittleStarBlastForm`, `BrittleStarTests.Blast_IsAVolume_NotAFlatSheet` (with its flatten control), the
  LOCKED rule in `Docs/claude/SHADERS_AND_PERFORMANCE.md`, and `/vessel` §4.ab5.


### L-STU-104 — A spring cannot land a fast body; a landing is a restitution, and the spring is what brings it back
- Lab: Concept fleet, the Gibbon web-slinger (`studio-kinds.js`, kind `web`) · Branch: `Ys-bleeding-edge` · Date: 2026-10-18
- What happened: an elastic web pulled dead-on has nothing to stop the hull AT the anchor, so a zip flew through it and yo-yoed (closest 0.7 u). A braking "perch" made it land dead (0 bounces): not the springy feel asked for. A landing-pad SPRING inside the shortest web punched through at every setting tried: stopping 150 u/s inside 10 u needs about 1100 u/s^2, far past any pad that still feels soft.
- Do instead: model the arrival as a bounce with a restitution (the closing speed reflects at e, the position clamps to the shortest web) and let the web's own spring pull the body back for the next bob: each bob is e of the last, it never passes through and it always settles. Sweep the knobs with a script that prints arrival speed, closest approach, bounce count and the distance trace; a single "settled / not settled" flag hid that the springy setting was the right one.
- Evidence: `sweep_web.cjs` rows (pad spring 3-6: closest 0.0-0.6 u; restitution 0.5: closest 9.8 u, trace 148 125 80 24 22 32 31 19 11 15 16 14 12 11 10); `CONCEPT_FLEET.md` section 8.3.
- Promoted: no

### L-STU-105 — An AI for a hull with no engine must never aim past its reach, and must read an orbit as sideways motion, not distance
- Lab: Concept fleet, the Gibbon web-slinger · Branch: `Ys-bleeding-edge` · Date: 2026-10-18
- What happened: the generic line pilot (aim ~24 u ahead) webbed whatever was nearest (447 webs, 35 u/s, DNF). Aiming farther fixed that and exposed three more: a web on a crystal it was flying past ORBITED it for 23 s (a spring is a central pull: the sideways speed stays); an "it is not closing, let go" rule then dropped every web that was still braking the hull; and an aim point past web reach let the hull coast 700 u into empty space, where a hull with no engine is stranded. Its "nearest anchor" fallback then picked its own trail 11 u away, inside the shortest web, and it never fired again.
- Do instead: (1) aim at the farthest point still inside reach, and off the course at the anchor nearest the course, never its own trail; (2) count a web as an orbit only while the motion is mostly sideways to it; (3) never cap a thrustless AI's speed by letting go early (it strands itself). Still open: the AI moves (32-41 u/s) but finishes no course; a racing pilot for this hull needs a plan (which anchor brakes, which redirects), not rules.
- Evidence: `trace_web_ai.cjs` traces in the session; smoke rows I1 avg 35 / 6 / 41 across the variants.

### L-STU-119 — An orbit control must fit one lap inside the tail, and closes loops only because something drifts
- Lab: Wrasse Lasso Lab (`Docs/Studios/WrasseLassoLab.html`) · Branch: `cece/loving-fermat-ntdnbu` · By: Claude session for Garrett · Date: 2026-10-20
- What happened: the first tether (70 u) and anchor (110 u) ropes scored 0 loops in 12 s. The shipped Wrasse tail is
  2.4 × 160 = 384 u, and a 110 u orbit is 691 u round, so a lap never fits in the tail and the crossing test never
  fires. Shrunk to 42 u and 22-50 u, the Plant control went to 19 loops/min. Second trap: a clean circle round a fixed
  point retraces itself, so its segments meet tangentially and do not cross, and an inward spiral never crosses either.
  Loops closed only because the planted anchor drifts with the world (40 u/s), which makes the orbit a self-crossing
  trochoid. Third: a pull-only rope planted far away flings the kite straight through the anchor (a pendulum); the
  Thresher hooks onto the orbit at once, so the lab turns radial speed into swing at a fixed speed (`plHook`).
- Do instead: before tuning any orbit, swing or spin control for a lasso, check 2π × radius ≤ the tail length, and
  give it a drift (world flow, a moving anchor) or a snap-shut rule. Measure on a still board too, where the drift is
  gone. Trace the AI's thumb, the anchor and the loop counts frame by frame (`__lab.world()`) before blaming the
  mechanic.
- Evidence: `Docs/Studios/WRASSE_LASSO_LAB.md` round 1 "Found" 1-3; `stepKite` case `'plant'`; `advanceTail` with `labSnap`.

### L-STU-127 — A parity scenario that cannot reach a new code path passes every mutation of it
- Lab: Thresher Plant Lab (a shipped-solver port, two rounds in one day) · Branch: `cece/youthful-johnson-hccyzf` · Date: 2026-10-21
- What happened: the shipped `ThresherChainSolver` gained a two-phase "launch line" after an aimed launch, and the
  page's `Solver` got a line-for-line port. Parity passed. Three mutations of the second phase also passed, one at a
  time: deleting `keepOnLine`, then two attempts at a steering scenario. That phase only exists when the launch
  outruns the ball, i.e. with the shipped ×4.8 launch boost. The page's `armLaunch` predated the boost, so no
  scenario could launch fast enough, and steering from the start kept the line in its first phase.
- Do instead:
  - After porting a rule, delete it from the page and run parity. A pass means no scenario reaches the rule; it
    does not mean the port is right.
  - When the lab's API lags the shipped one (a parameter the game passes and the lab never learned), port the
    parameter with a default that keeps the lab's own vessel unchanged. Then give the scenarios a field for it
    (here `boost`, read by `TraceDriver.cs` and `parityTrace`).
  - A clause that still stays under tolerance (here "the ball is not closing", 0.84×) needs another owner. Name
    the C# test that fails without it.
- Evidence: `analog-armed-line-steer` in `Tools/Build/thresher_plant_lab/scenarios.json` (dropping `keepOnLine`
  fails at 2.56×); `THRESHER_PLANT_LAB.md` § Checked, 2026-10-21.
- Promoted: no

### L-STU-128 — Measure the input left over AFTER an edge, and the follower at the leader's top speed
- Lab: Thresher Plant Lab (the shipped aimed launch) · Branch: `cece/youthful-johnson-hccyzf` · Date: 2026-10-21
- What happened: "it doesn't always shoot me at the cross hair" had two causes. Neither showed in the scenarios,
  which fly each trigger at 0 or 1 after an edge.
  - The launch fires as LT passes 90%, so the trigger is still most of the way down for the next frames. That
    depth read as a partial pull: the ball became a sliding anchor and the heading went 177° off the aim.
  - With the Time element raised, the boosted launch leaves at ~1 300 u/s against the ball's 479 u/s cap. A
    rigid chain then brakes the ship under the ball's speed, and the ball overtakes and swings across.
- Do instead:
  - For any edge fired at a threshold, fly the input at the threshold's far side for a second: 0.85, not 0.
  - Sweep the element multipliers to their maximum. A capped follower (ball, tow, wake) fails only where the
    leader outruns the cap.
  - Probe with the stand-in pilot WITH grip (the nose turns with the tug, as the transformer does). Without
    grip the tug is erased every frame and the swing never shows.
- Evidence: `ThresherAnalogTriggerTests.AimedLaunch_*` (177° on the old solver, 55° on round 1);
  `R_VesselActions/THRESHER.md` §5a "The launch flies the crosshair's line".
- Promoted: no

### L-STU-129 — A retune can push a parity scenario onto a knife edge that float32 and doubles resolve differently
- Lab: Wrasse Lasso Lab (`sling_parity.cjs`, C# float32 against the page's doubles) · Branch: `cece/loving-fermat-ntdnbu` · By: Claude session for Garrett · Date: 2026-10-20
- What happened: lowering `slingMaxSpeed` 520 → 364 (a pure retune, both sides changed together) turned the parity gate
  red: 294 structural mismatches and 2.5 × tolerance in the "tow" scenario. The first structural mismatch was a
  one-point tail difference, and the velocities had split three frames earlier at frame 117. The kite sat 41.97 u from
  the hand, grazing the 42 u rope. "Taut or slack" there is decided in the 5th decimal, and the damper's kick on taut
  separated the two runs. Both implementations were the same rule; the scenario had wandered onto a discontinuity.
- Do instead:
  - When parity fails after a change that touched both sides identically, find the FIRST diverging frame (velocities,
    not positions; a dump of frames 0-200 with a 0.05 threshold) and read the state there before suspecting the port.
  - If it sits on a threshold (`d <= rope`, a crossing test, a clamp), move the scripted input off it (here the tow's
    swing 60 → 64 u). Say why in the scenario, and keep the planted controls proving the gate still bites.
  - Report the first structural mismatch apart from the worst numeric one (the gate now does): the worst error lands
    after the divergence and points at the wrong frame.
- Evidence: `Tools/Build/kite_navigator_lab/sling_parity.cjs` (the tow comment, `firstStructural`); PASS at 0.006 × after.
- Promoted: no

### L-STU-130 — Render the SHIPPED visual code offline: give it a snapshot, not the MonoBehaviour
- Lab: the Wrasse's juice (`KiteNavigatorJuice`, no browser page) · Branch: `cece/loving-fermat-ntdnbu` · By: Claude session for Garrett · Date: 2026-10-21
- What happened: the effects had to be "bespoke, unique, dynamic, and polished", and no session here can open Unity. The
  first draft read the seat (a MonoBehaviour with the whole game behind it), so nothing could run it. Splitting its
  inputs into a plain `KiteJuiceFrame` (board axes, hull, kite, pilot camera) and `KiteJuiceSettings` let
  `Tools/Build/kite_navigator_lab/juice_sheet.sh` compile the shipped file with the card-art harness's Unity shim (four
  members added) and rasterise every effect into a contact sheet, top-down and from the chase camera. The first sheet
  showed three things no test would have: Space's petals left in three clumps (one per mote), not one blast; the Omni
  bloom opened into a neat ring of dots (the very rings being removed); Time's landing chips were invisible.
- Do instead: for any effect, camera or view code meant to be judged by eye, keep its math free of engine objects
  (a frame snapshot in, vertex lists out, the MonoBehaviour fills the Mesh). Then render a time-sampled contact sheet
  offline and LOOK at it before claiming polish. Make the harness fail on non-finite vertices and on an effect that
  draws nothing, so it is also a gate.
- Evidence: `KiteNavigatorJuice.cs` (`KiteJuiceFrame`, `KiteJuiceSettings`); `juice_sheet.sh [out.png] [pilot]`;
  `KITE_NAVIGATOR.md` round 10.
- Promoted: no

### L-STU-131 — Measure how often a reward is ON THE BUTTON, not only whether it helps
- Lab: Honeyguide Lab round 5 (timed actives) · Branch: `claude/honeyguide-ratel` · By: Claude session for Garrett · Date: 2026-10-21
- What happened: the first build dealt an active only when one of the seven weapon cards was made (7 of 39). Each active
  measured well alone, but an "Active up %" column showed a button held an active 23-27% of the time, about one a minute.
  Garrett's ask ("new ones come") was not met, and no lift column would have said so. Dealing a random active on every
  pattern took the buttons to 85% and the race from +51% to +68%.
- A second check came from the rookie row: a pilot that presses whatever is ready scored the same as the skilled AI. That
  backs "not so hard to use". The first skilled AI was too picky and scored BELOW the rookie, and the gap pointed at the
  AI's thresholds, not at the design.
- Do instead:
  - For any granted, timed ability, add columns for its presence (the share of time it is available) and its uses a
    minute beside the lift.
  - Run a rookie pilot next to the skilled one. If the rookie wins, fix the skilled AI before reading any number.
- Evidence: `Docs/Studios/HONEYGUIDE_LAB.md` round 5 scorecard; `activeUp`, `usesPerMin`, PSKILL `actSense`.
- Promoted: no

### L-STU-136 — A drawing mechanic must write where the aim's sweep matches the stroke's spacing
- Lab: Crew of Four Lab, round 6 (the Orca's fast gun "writes prisms like a pen") · Branch: `cece/happy-noether-m54hz6` · Date: 2026-10-21
- What happened: the first pen laid a prism where each pen round's flight ended, 387 u out. A turret slewing at its
  normal rate moved that point about 100 u between pen rounds, against a 5 u stroke spacing. Every landing lifted the pen,
  so the "calligraphy" was a scatter of loose sticks, each pointing along the aim. Moving the paper to 120 u turned the
  same sweep into one stroke: 20 prisms, every gap exactly 5.0 u. A first screenshot also showed a radial fan of sticks,
  which looked like the pen; it was a rival's dropped shield (danger prisms laid along rays).
- Do instead:
  - For anything the player draws with an aimed weapon, compute the landing point's speed: the aim's angular rate times
    the distance. Set the distance (the paper) so that speed between samples is about one spacing.
  - Hold the first point of a stroke until the second says which way the stroke runs. Otherwise every stroke starts
    with a prism pointing at the shooter.
  - Measure a stroke from the world (gaps and turn angles between consecutive marks under a scripted sweep), not from a
    screenshot, before judging it. Name what each shape in a screenshot is before blaming the new feature.
- Evidence: `CrewOfFourLab.html` `penLay`, `penLanding`, `SHIPPED.rapidPenReach`; C# `OrcaPen` held to it by
  `Tools/Build/orca_crew_lab/parity.cjs` (400 landings); `CREW_OF_FOUR_LAB.md` §4 round 6.
- Promoted: no

### L-STU-137 — A turret on a moving hull must not hand its shots the hull's whole velocity
- Lab: Crew of Four Lab, round 7 (the Orca, after Garrett flew round 6 in Unity) · Branch: `cece/happy-noether-m54hz6` · Date: 2026-10-21
- What happened: the lab and the game both launched a turret's round at `aim × speed + hull velocity`, the rule a
  forward-firing gun gets away with. On a turret that fires sideways from a hull flying 50–175 u/s, with rounds at
  135, every shot slewed far off the barrel and the crosshair in the direction of flight. The lab's scorecard never
  showed it, because the AI hit what it hit and nobody watched a crosshair. The owner saw it in the first flight: "not
  shooting where they were aiming". A frame-old muzzle (the shot fired in `Update` before the hull moved, posed in
  `LateUpdate` after) added up to 3 u more.
- Do instead:
  - Give an aimed shot only the hull velocity ALONG its aim: `aim × (speed + max(0, aim · hullVelocity))`. It flies
    exactly down the aimed line, still outruns its hull forward, and never slows below its own speed astern.
  - Make the barrel, the crosshair and the shot one line: pose the turret the moment it fires, fire after the hull has
    moved that frame, spawn at the drawn barrel tip, and project the crosshair from that tip along the aim.
  - In a lab with aimed weapons, add a check a scorecard cannot make: draw (or measure) the shot's path against the
    crosshair while the hull flies fast and turns.
- Evidence: `CrewOfFourLab.html` `shotVel`, `SHIPPED.shotInherit`, the Round 6 shots control (gun hull hits 38 → 64 in
  Broadside with the fix); C# `OrcaCrewCore.ShotVelocity` held to it by `parity.cjs`; `OrcaHullRig.PoseSeat`;
  `CREW_OF_FOUR_LAB.md` §4 round 7.
- Promoted: no

### L-STU-132 — Show a world fact on the world, and check every new world effect against the flat rule before drawing it
- Lab: Crew of Four Lab, round 8 (the Orca, after Garrett flew round 7 in Unity) · Branch: `cece/happy-noether-m54hz6` · Date: 2026-10-21
- What happened: the bomb seat showed "what a press would catch" as a dashed screen ring round the live bomb with
  "BURST" over it. The owner: "the bomb should not have a ring that says burst. it should use a lit state to show what
  it will destroy. always go for the 3d in world solution over the 2d flat text solution." The first redraw of the
  missiles then gave them an additive ribbon trail (and the game the fleet's `TrailRenderer`), which the LOCKED
  "nothing in the world looks flat" rule names by shape. Two more traps on the way: PrismLit's aimed channel is
  cone-only, so a sphere published there is read as a cone; and lighting "every prism in the sphere" lit the gunner's
  own mass, which the blast spares, so the light said the wrong thing.
- Do instead:
  - For any "what will this hit / what state is it in" readout, first ask which world channel can carry it (PrismLit
    for mass about to be acted on, morphs and petals for a hull, a faceted lit volume for an effect). Draw it there,
    in the lab's 3D view first. Keep the screen for the aim mark only.
  - Light exactly what the action will do: gate a light to the domains it breaks (one bank light per domain), never the
    whole volume.
  - Before drawing a trail, a beam or a marker, read the flat rule's list. A plume tube that is thin at the source
    also keeps a chase camera out of its own trail.
  - Check a look by rendering the same frame with the effect on and off (`__lab.still(eye, look)` from a scripted
    search), not by finding it in a busy seat screenshot.
- Evidence: `CrewOfFourLab.html` `bombLight`, `missileView`, `tube`, `MLOOK`, `__lab.still`; C# `OrcaCrewView.LightBomb`,
  `OrcaMissileVisual`; `Docs/claude/SHADERS_AND_PERFORMANCE.md` "In the world, not on the screen"; `CREW_OF_FOUR_LAB.md`
  §4 round 8.
- Promoted: yes (the LOCKED rule in `SHADERS_AND_PERFORMANCE.md`)

### L-STU-142 — Scaling a lab's population baseline leaves every card written in absolute counts behind
- Lab: Starlings Lab (round 1 numbers, raised in Unity's round 3) · Branch: `cece/quirky-gates-skplxp` · Date: 2026-10-21
- What happened: Garrett asked for 5x the starlings ("a small pack not a group of starlings in a murmuration"). The
  baselines moved (start 24 → 120, cap 40 → 200, minimum 6 → 30) in one line of `SHIPPED`, but the card list states
  some numbers as absolute member counts (Big Family: "Swarm cap 46" at L1, read as `cap + value - 40`) and others
  per member (Hole Punch, Annex, Danger Drip, the base bite and nip). The first now adds a negligible +6 to a cap of
  200; the second multiplies the swarm's output by about 5. The lab scorecard still ran green, because nothing in it
  compares a card's effect to the baseline it was written against. The game's flocking loop was also all-pairs, fine
  at 24 and about 70x the work at 200 (fixed with a separation grid).
- Do instead:
  - When a population baseline moves, list every card or stat written in absolute counts or per member, and decide
    each with the owner (keep, scale, or restate relative to the baseline). Do not let the scorecard's green stand in
    for that decision.
  - Write population cards relative to the baseline (`+15% cap`) when the baseline is still being tuned.
  - Before raising a count, find any loop over pairs of members in the lab and the game.
- Evidence: `StarlingsLab.html` `SHIPPED`, `minNow`; `StarlingRules.CapNow`; `StarlingSwarmExecutor.Separation`; team
  task T-20261021-729e (the card decision).
- Promoted: no

### L-STU-143 — Take a mechanic out of a random draw with a POOL that defaults to everything, so every old seed still plays
- Lab: Honeyguide Lab (`Docs/Studios/HoneyguideLab.html`) · Branch: `cece/nifty-dirac-2dmclm` · By: the 2026-10-22 Ratel-portrait session · Date: 2026-10-22
- What happened: Garrett asked to remove Printer (a free prism every spin) "for now". The draw was `ABILITIES[floor(rng * 9)]`, held step for step to `DynamoCore.cs` by `parity.cjs`. Deleting the key would have renumbered the serialized `DynamoAbility` enum and changed every seed. Instead the core draws from `S.abilities` (null = all, so the arithmetic is identical), and the shipped settings pass all but `RETIRED_ABILITIES`. Parity stayed green on every old rule set, and new rule sets (the shipped pool, two abilities, one) plus a planted "ignore the pool" control (caught, 4,431 steps) cover the change. One Unity golden did need re-measuring: it plays the shipped config, and it was already stale from round 6 (start slot 27 against the page's 26), which nobody had seen because no CI runs the edit-mode tests. The page and `DynamoCore.cs` on the shipped asset agreed on the new values.
- Do instead: retire a drawn option with a list next to the lab's shipped settings and a matching list on the game's config, held equal by the generator's `--check`. Never delete the key or the enum member. When a game test plays the shipped config, recompute its golden by running the real C# core on the real asset as well as the page, and the two must agree.
- Evidence: `abilityPool` in the page's dynamo-core block; `DynamoSettings.Abilities`, `HoneyguideConfigSO.retiredAbilities`; `author_honeyguide_assets.py` check "retiredAbilities is not the lab's RETIRED_ABILITIES"; `HoneyguideTests.Ratel_NeverDrawsPrinter_WhileItIsRetired`.
- Promoted: no
