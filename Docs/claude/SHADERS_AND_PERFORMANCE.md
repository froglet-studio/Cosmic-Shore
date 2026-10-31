# Shader & Visual Development

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

## Shader & Visual Development

### Nothing in the world looks flat (LOCKED, Garrett 2026-10-18)

> "Nothing in the environment should ever look flat like this, that is reserved for ui."

Flat is the UI's look. Anything drawn in the world - an effect, a wave, a marker, a trail, a blast - is a **volume**:
geometry with real depth that the scene's light and the domain materials shade (faceted, the prism-cut read of the
hulls, crystals and prisms), never a flat sheet, an unlit fill, a plain additive ribbon or a billboard that reads as a
sticker.
- **Scale the volume with the thing.** A beam a few units thick across a 780 u blast reads as wire, which is flat
  again: tie thicknesses to the effect's own size (`BrittleStarBlastForm`: every thickness is a share of the front's
  radius).
- **Lab and studio pages follow the same rule**: draw the volume lit and flat-shaded, not an additive sheet, so what is
  decided there is what the game shows.
- **Prove it where the shape is pure code**: a test that a flat sheet would fail (`BrittleStarTests.Blast_IsAVolume_NotAFlatSheet`:
  most facets face sideways, and it stands well out of the plane), with a negative control that flattens it.
- **Where gameplay is thin and the look needs depth** (a 1.76 u disk), keep the look honest in the direction that
  decides play (never drawn past the edge where things stop breaking) and say in the doc where the volume overstates.
- **An owner's explicit call for one specific view wins over this rule**, and is recorded where it is made, so nobody
  "fixes" it back: the Kite's view draws prisms out of its plane as unlit ghost boxes because Garrett asked for exactly
  that (2026-10-18, `cece/compassionate-babbage-gc13io`, "the prisms that go out of plane for the kite should be unlit
  but not disappear"). Ask before applying this rule to someone else's deliberate look.

### In the world, not on the screen (LOCKED, Garrett 2026-10-21)

> "always go for the 3d in world solution over the 2d flat text solution."

When a player needs to see something about the world (what a blast will catch, where a shot will land, which thing is
targeted, what state a thing is in), show it **on the thing itself, in 3D**: light it, colour it, change its shape or
motion, give it a volume. A screen-space ring, a label or a word over it is the last resort, not the first.
- **Worked example: the Orca's bomb seat** (round 9). The crosshair drew a ring the size of the blast and "BURST" over
  the live bomb. Now the prisms the blast will break are lit through PrismLit's bank (a sphere gated to each domain the
  blast breaks, `OrcaCrewView.LightBomb`) and the crosshair keeps only its aim mark.
- **Reach for the world's own channels first**: PrismLit (`Docs/LIT.md`) for "this mass is about to be acted on",
  the hull's element morphs and petals for a vessel's state, faceted lit volumes for effects (the rule above).
- **The crosshair itself stays UI**: an aim mark has no world thing to sit on. Keep it small and leave the facts to
  the world.
- **It holds in labs and studios too**: a page that decides a look draws it in its 3D view, so what is decided is what
  the game shows.

### Orthographic views and picture-in-picture frames (Garrett 2026-10-22)

> "make sure anyone else using orthographic view never applies a panini or bloom to 2d assets by default."
> "give the pip views a pleasant border across the game that matches our styleguide."

- **No Panini on an orthographic camera, ever, with nothing to opt into.** `OrthographicLens`
  (`Assets/_Scripts/Utility/`) hooks `beginCameraRendering` (URP raises it before it updates the camera's volumes),
  gives every orthographic Game camera its own volume stack and holds `PaniniProjection.distance` at 0 in it. Panini is
  a perspective lens. On a flat picture it only bows straight edges. Bloom, the tonemapper and the rest are untouched.
  `OrthographicLens.AllowPanini(cam, true)` opts a camera out; nothing does.
- **A 2D camera fades the sky's Voronoi cell net** (Garrett 2026-10-25: "the characters with an orthographic view like
  the shrimp are doing something weird with the voroni effect on the skybox"; "call these 2d cameras"). A 2D camera is
  one that slides and zooms across a plane: the Pistol's off-axis side view, the Wrasse's kite board, the Jacks' herding
  view. The sky follows only rotation, so through those lenses its cell net sat magnified and glued to the screen.
  `TwoDCameraSky.Register(cam)` (in `Crew/CrewViews.cs`) sets the global `_SkyCellOverlaySuppress` to 1 as that camera
  begins rendering and 0 for every other camera; `HyperSeaSkybox.shader` multiplies its cell overlay by
  `1 - _SkyCellOverlaySuppress`. Stars, nebulae and the galactic band stay. Register every new 2D camera and unregister
  it on dispose. The baked MobileLow sky carries its cells in its texture and is not faded.
- **2D marks in an orthographic view skip its post-processing.** Icons, dots, rings, cords and petals are drawn
  through an `UnprocessedOverlay` (`Assets/_Scripts/Utility/`): an overlay camera on the view's URP stack with
  post-processing off, seeing only the otherwise empty `3D UI` layer. Draw with `RenderParams.camera = overlay.Camera`
  and `layer = overlay.Layer`. The world under the marks keeps the game's bloom and tonemapper. The Wrasse
  (`KiteNavigatorView`) and the Jacks (`JacksView`) do this.
- **The gate:** `python3 Tools/Build/check_orthographic_views.py` (`--self-test`) fails when the lens loses its hook or
  its Panini zeroing, and when a runtime script makes a camera orthographic without an `UnprocessedOverlay`. A view with
  no marks says so with `// ortho-marks: <why>`.
- **Every picture-in-picture view wears one frame.** `PipViewFrame` (`Assets/_Scripts/UI/Elements/`) draws the style
  guide's frame round whichever camera is in a view's small rect: a 1px Light hairline on a slivered outline, then the
  2px Neutral (Dark) chrome band out to a clean rectangle, the band filling the two cut corners (`STYLE_FOUNDATION.md`
  §2-§5). The look is `Resources/PipFrameStyle.asset` (`PipFrameStyleSO`, colours as `UIThemeSO` tokens). The crew
  square (`CrewPictureInPicture`), the Wrasse, the Orca's lifted pilot view and the Jacks wear it. A new PiP view owns a
  `PipViewFrame` and calls `Show(smallCamera)` each frame (null hides it).

### HLSL / Shader Graph

- **A watery or glassy surface, bent space, or a lit underground wall**: start from the Meerkat family and its recipe,
  `Docs/Shaders/WATERY_GLASS.md` (the HLSL traps of porting an artifact's GLSL are listed there).

- Custom Function nodes use HLSL files stored in a consistent location
- Function signatures must follow Shader Graph conventions (proper `_float` suffix usage, sampler declarations)
- Blend shapes are converted to textures for shader-driven animation (no controller scripts — animation is entirely GPU-driven for performance)
- Edge detection, prism rendering, Shepard tone effects, and speed trail scaling are active shader systems
- Procedural HyperSea skybox shader with Andromeda galaxy, domain-warped nebulae, and configurable star density

### Performance Standards

- Use `Unity.Profiling.ProfilerMarker` with `using (marker.Auto())` for profiling, not manual `Begin`/`EndSample`
- Watch for `Gfx.WaitForPresentOnGfxThread` bottlenecks — usually indicates GPU sync issues, not CPU
- Static batching, object pooling, and draw call management are always priorities
- Test with profiler before and after optimization changes — don't assume improvement
- A `ProfilerRecorder` built from code **records nothing unless started**: `ProfilerRecorderOptions.Default` is `SumAllSamplesInFrame | WrapAround` and does NOT include `StartImmediately`, and the constructor does not start it. Pass `Default | StartImmediately` (or call `Start()`), and report "marker not found" separately from "no samples" — the Crystal Flip Wave Benchmark's first cut printed `n/a` for every stat and the two causes looked identical (`Docs/TIME_CRYSTAL.md` §7)
- GPU instancing enabled on all prism and VFX materials
- Prism scale/material/effect animation is GPU-clock-driven (the clock-material law, `Docs/PRISM_ANIMATION.md`) — the former CPU Jobs+Burst animation managers are deleted
- Burst-compiled spatial queries replace Physics-based AOE prism damage (`PrismSpatialIndex` — see `Docs/SPATIAL_INDEX.md`)
- Cache-line-aware data layouts with hot/cold splitting and bit-packed flags (`PrismSpatialData` / `PrismDamageData` in `PrismSpatialIndex`)
- Growth occupancy checks use `PrismSpatialIndex.TryReserve` (claim-before-spawn), never `Physics.CheckBox` — prism colliders are disabled for the first 0.6s after spawn, so physics queries are structurally blind to fresh prisms

**The performance record and how to measure (2026-10-07) - start here before any perf work:**

- **Plugins first (2026-10-08):** check `Docs/claude/PLUGINS_AND_CONNECTORS.md`. The **unity-perf** plugin
  (`/unity-performance`, `/csharp-zero-gc`, `/code-standards`) runs the performance restart, and Unity's official
  plugin covers URP, TMP, uGUI, physics and the Unity CLI. If a fitting plugin is not enabled, suggest it once and
  carry on; this repo's measured rules win over its general advice.
- **Read first:** `Docs/PERFORMANCE_OPTIMIZATION.md` (where the game stands, what was done, the ranked lever
  list, how to measure without fooling yourself). Then `Docs/MEMORY_AUDIT.md` (what the game HOLDS) and
  `Docs/PLATFORM_UNIFICATION.md` (device tiers: Desktop / MobileHigh / MobileLow, the trail cap, one codebase
  for Windows, iOS and Android; §3.10 is what the Medium / Low / Very Low quality presets take off the GPU, in
  `Resources/QualityPresetCuts.asset`, read through `GraphicsCuts`). The next session's brief: `Docs/prompts/PERFORMANCE_NEXT_SESSION_PROMPT.md`.
- **The performance system** (`/perf-hunt`, `Tools/Build/perf_hunt.py`, `.github/workflows/perf-daily.yml`): once a day
  from `bleeding-edge` the static gates run, every capture a collaborator's editor shared is judged against its baseline,
  and the game's markers are measured in Amoebius with allocations as the gate; a regression becomes a team task and
  `/perf-hunt` is how it is resolved (rank by measured cost, fix against the checklist, prove before/after, adopt the
  new baseline). `Docs/PERFORMANCE_OPTIMIZATION.md` §3.4.
- **One measurement, one file:** the editor captures by itself in Play (`PerfAutoCapture`, once per scene after its time;
  no button), or `capture <label> 15` in the HUD console, runs burst + diag + prof + renderers + cells as ONE JSON; after Play the
  editor sends it by itself (no question) to the person's `bugledger/<user>` branch, and the sweep judges it
  (`Tools/Build/perf_capture_report.py`, validity first). Ask for this, never for screenshots. Status and next steps:
  ask any session "performance status" (the `health_status.py` hook answers from `Docs/perf/status.json`).
- **Instruments in the game:** the console commands `diag` (per-system timings, a report to JSON),
  `prof` (the Profiler Hierarchy as JSON; flags Burst jobs that ran as managed code), `freeze` / `ab`
  (a same-state A/B in one line), `renderers` (a renderer census; `hide <prefix>` / `show` to A/B a family's
  culling cost), the `DiagnosticsHUD`, and the Performance Benchmark tool
  (`_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md`). `diag` records the editor's Code Optimization
  mode: **measure in Release** (the bug icon, bottom right); Debug runs all C# about 5x slower and a number taken
  there says nothing about a build.
- **The same markers with no Unity (Amoebius, the engine under `Port/`):** `python3 Port/tools/amoebius_markers_run.py --mode <Mode>` boots the
  game headless in Amoebius, drives the menu to a match and writes every marker's ms, calls and **KB per frame**
  (`--compare A B` diffs two runs; `Port/docs/ARCHITECTURE.md` §11.3b). Trust its **allocations** and
  run-to-run deltas; its milliseconds are .NET's, without Burst, so a timing win still needs a Unity Release `diag`.
- **Every per-frame cost gets a `ProfilerMarker` named `System.Part`** (`SkimRace.Pilot.Decide`,
  `SkimRace.Driver.TrackMpc`, `RaceTrailCap.Hold`, ...) so `prof` and `diag` can name it; the AI's are listed
  in `Docs/SKIM_RACE_AI.md` §12.
- **Allocation gates:** `Tools/Build/check_mathf_params_alloc.py` - a 3-argument `Mathf.Min/Max/...` allocates a
  `params` array per call (the Skim Race pilot allocated 840 a frame); use `MathfNoAlloc`.
  `Tools/Build/scan_perframe_allocations.py` scans per-frame code for allocations. `Decide` now allocates nothing.
- **The editor runs Mono, in double precision, at Debug optimization**; a Release .NET number does not predict
  it. The Skim Race simulator's `SKIMRACE_RUNTIME=mono` mode does (`Docs/SKIM_RACE_AI.md` §8.0f-g, §14.4), and
  its float rewrite shows the trap: folding `Vector3` operators into float expressions changes the last bits
  on the editor's Mono unless every value the `Vector3` form rounded is rounded with an explicit `(float)`.
- **Spread a burst across seats and frames:** several AI seats re-planning in one frame paid the whole cost at
  once; `SkimRaceReplanGate` lets one re-plan claim a frame and the next seat waits one frame
  (`Docs/SKIM_RACE_AI.md` §8.0i) - the pattern for any periodic heavy step several actors run.
- **Frame rate is a gameplay input, not just a cost:** contacts are tested on the 0.04 s fixed step and the
  trail is laid at most once per frame, so a thing tuned at one frame rate behaves differently at another
  (`Docs/SKIM_RACE_AI.md` §14 - the AI is now tuned across 62/36/20 fps). Measure a frame-rate-sensitive
  system at several frame rates before calling it tuned.

### Prism System Performance

The prism system was the most performance-critical gameplay system, and its rendering is now
**solved**: each prism keeps a GameObject (colliders, gameplay components) but its
`MeshRenderer` is disabled and it draws through an **instanced Entities Graphics companion
entity** (`PrismRenderService`, `Docs/PRISM_ECS_MIGRATION.md`). Measured: **103,823 prisms at
4.8 ms CPU vs 61.9 ms on the legacy path (12.9×)**, and 8,436 boot-world prisms in **23 draw
calls**. Prism animation is GPU-clocked (`Docs/PRISM_ANIMATION.md`), deaths are batched pure
entities, and spatial queries go through `PrismSpatialIndex`.

What still costs is what hangs **off** prisms — flora spindles (one GameObject renderer per grown
lattice prism), growth `Instantiate`, and GameObject existence itself. Current state, the ranked
lever list and the measuring method: **`Docs/PERFORMANCE_OPTIMIZATION.md`**. The pre-ECS audit
that used to be linked here (`_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md`) described an
architecture that no longer exists and was deleted 2026-09-22.
