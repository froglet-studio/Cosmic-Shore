# Toy System — Backlog & Known Limitations

The core toy system + the four toys are in. This tracks the polish/improvement
work, grouped so each group can be its own follow-up branch, plus current known
limitations and verification status. Architecture: `ARCHITECTURE.md`.

## Verification status

- **Compile-reviewed** against the real codebase (all external APIs, the generic
  `SwapToySetCoordinator<T>`, `VesselModelBuilder`, null-safety, internal access, no dangling
  refs) — clean. Includes the second-pass fixes below.
- **Not yet play-verified in-editor** (no Unity in the authoring environment). First in-editor
  pass should confirm: toys bloom in and sit where the lava-lamp vessel flies; local-user +
  freestyle gating (autopilot never trips them); all six vessels render as mini models; a swap
  keeps your domain colour + inherits pose/speed and shows the new HUD; the vessel-changer ships
  recolour when you use the domain changer; gamepad Start exits freestyle and the pad stops
  double-driving the UI; and a swap toy can't switch you back before you fly clear.
- Assets authored by `Tools > Cosmic Shore > Setup Freestyle Toybox` are committed
  (`Resources/Toybox.asset`, `_SO_Assets/Toys/Toy_*.asset`) and `ToyboxController` is wired into
  `Menu_Main`; GUID references verified consistent.

## Resolved this pass (second branch, `claude/vessel-changer-toy-updates-*`)

All shipped on the branch; see `ARCHITECTURE.md` § "Status & follow-up" for the file table.

- **Only Rhino rendered a mini model → all six render.** Skimmer-sphere bounds pollution + the
  transparent hull material. `VesselModelBuilder` now hull-filters and paints an opaque, domain-
  tinted preview material.
- **Swap toys switched you back before you could escape.** `Toy` re-arms only after the vessel
  flies clear (distance poll + hysteresis), the flipped toy re-grows slowly, and the coordinator
  disarms the whole set on activation.
- **Swap reset your hull to Jade / desynced the domain changer.** `ReInitializePair` re-syncs
  `Player.Domain` from `NetDomain` before repainting; domain now persists per-player across swaps.
- **New ship started from a dead stop.** It now inherits the previous ship's speed
  (`SetInitialSpeed`) alongside pose.
- **Swapped-in ship had no working HUD.** `ReInitializePair` re-raises `OnPlayerPairInitialized`;
  `MenuMiniGameHUD` re-shows the local HUD in freestyle.
- **Vessel HUD buttons fought the appshell; no pad exit.** EventSystem `sendNavigationEvents` off
  in freestyle; gamepad **Start** exits to the appshell.
- **Mini ships only recoloured on flip.** They now recolour in place on any domain change.

## Branch: vessel-changer polish

- **Mini-model materials + hull extraction (FIXED).** Previously only Rhino rendered a model:
  `VesselModelBuilder` blindly extracted every renderer, so each vessel's **skimmer** (a builtin
  Sphere scaled 15–60×) dominated `NormalizeToRadius` and crushed the real hull to an invisible
  speck (Rhino is the one vessel whose skimmer has no builtin sphere). It also copied the ship's
  hull material, which is a transparent, runtime-theme-driven shader that renders dim/invisible at
  rest. Now the builder skips non-hull geometry (builtin-primitive skimmer bodies, and anything
  named skimmer/trail/jet/forcefield/crackle/pip/vfx) and skips inactive/disabled renderers (e.g.
  Manta's hidden scale-100 duplicate SMR), then paints every hull mesh with one opaque, self-lit
  preview material tinted to the player's domain colour (`TryBuild(prefab, radius, previewColor, …)`).
  Skinned hulls still show in authored (bind) pose — a static baked snapshot (`SkinnedMeshRenderer.BakeMesh`)
  is a possible future refinement for exact current-pose fidelity.
- **Scale / label tuning.** `toyBodyRadius`, label size, and the arc spacing (`anglePerToyDeg`)
  are guesses — tune in-editor.
- **Collection.** Default is a curated 6 (Manta, Dolphin, Rhino, Squirrel, Serpent, Sparrow) via
  `vesselCollection`. Decide the final set/order; for larger sets consider paging or a tighter
  arc so the ring doesn't crowd. As you visit vessels outside the collection they get added
  (so you can always flip back) — confirm that growth reads well.
- **Idle motion.** A slow spin on the mini models would make them read as "toys".

## Branch: domain-changer polish

- **Colour source.** Uses `ThemeManagerData.GetDomainUIColor` (TrailHighlightColor) on a sphere
  body. Confirm it reads clearly; consider using the prism/hull look for stronger identity.
- **Layout.** Two toys fanned by `anglePerToyDeg` around one slot — tune spacing.

## Branch: painting ("Connect the Dots") polish

Shipped in the connect-the-dots enhancement: multi-stroke multi-domain paintings
(`PaintingDefinitionSO` + `PaintingPresetLibrary`: Star / Rainbow / Saturn / **Taj Mahal**),
world-anchored upright monuments, per-stroke domain start gates
(`RequestSetDomain_ServerRpc`), pen-up between strokes
(`VesselPrismController.SetSpawnerPaused` — the previously-missing public toggle), adaptive
reach on fine detail, bench/resume via the station, cross-session stroke progress
(`PaintingProgressStore`), completion celebration, and a per-station progress label.
`MenuShapePainter` (single-stroke, billboarded, one colour) was removed. Remaining polish:

- **In-editor tuning pass.** Gallery fan spacing (`anglePerToyDeg`), `paintingClearance`,
  preset sizes vs. the lava-lamp play area, gate ring radius, ghost alphas, celebration
  timing — all first-guess values.
- **Cross-session prisms — RESOLVED.** The drawing state (per-prism pose/size/domain) is now
  saved per completed stroke (`PaintingPrismStore`) and regrown through the normal
  `PrismFactory` channel on return — across vessel swaps, other paintings, game modes, and
  sessions. Prompter-approved re-blooming of saved mass; restored prisms are ordinary
  conserved mass thereafter.
- **Share viewer polish.** The exported HTML viewer is dependency-free WebGL with orbit +
  auto-spin; candidates: a share-card PNG thumbnail alongside the HTML, prism bloom-in replay
  of the build order, background starfield matching the HyperSea.
- **Party-client colour lag.** On a party client, the gate's domain pick takes an RTT to
  replicate, so the first prism or two of a stroke can carry the previous colour.
- **Feedback juice.** Waypoint-collect VFX, gate-pass SFX/haptics (AudioSystem gameplay SFX +
  NiceVibrations — the framework-wide audio item below), a subtle beam from station to its
  monument so ownership reads at a glance.
- **More paintings — SHIPPED (12 grandiose 3D constructions), then QUALITY-REBUILT.** After the
  prompter's review ("less symbolic, more realistic, properly proportioned — pull from reference
  material, not first principles"), the seven mathematically-real forms were rebuilt at
  **reference grade** from published parametrizations, and ALL random curl-noise scribble was
  removed from them: Nautilus (embracing-whorl shell model + 58 growth-line ribs), Lotus (real
  Nelumbo: lily pads, obovate petal whorls, stamens, seed pod), Rose (recurved petal rims → furled
  heart, sepals), Double Helix (true B-DNA: pitch/diameter 1.7, 10 bp/turn, ribboned backbones),
  Torus Knot (engineered tube on rotation-minimizing frames), Buckyball (C60 + its 30 real 6:6
  double bonds), Spiral Galaxy (two-arm grand design, dust lanes, arm-following star streaks,
  22° inclination). Anatomy counts are locked by `ReferenceRebuilds_KeepTheirAnatomy`.
  The five representational subjects (Lion's Head, Peacock, Phoenix, Starry Night, Almighty
  Mountain) are pending the **Camp B real-model pipeline** (mesh → hatched flight strokes, baked
  as authored `PaintingDefinitionSO.strokes`). Remaining candidates: Great Wave, pagoda, Colosseum.
- **Perf / in-editor pass for the big paintings.** Each stroke stands up one ghost `LineRenderer` +
  one start gate at `PaintingRunner.Begin`, so Peacock (~226 strokes) and Lion's Head (~171) create a
  few hundred lightweight LineRenderers up front. This is the intended "hours of flying" ceiling but
  wants an in-editor confirmation on mobile (and a possible LOD/stream-in of ghosts for the largest
  gallery entries). Gallery fan spacing is now `anglePerToyDeg = 8°` (~120° for all 16 stations) —
  confirm it reads well against the lava-lamp play area.
- **Reviewed and deliberately deferred** (from the enhancement's review pass): coalesce the
  per-stroke synchronous saves (`DataAccessor` full-file JSON writes at each stroke boundary —
  both the small progress file and the growing `PaintingPrismStore` drawing-state file; the
  writes are human-paced but a debounce/off-thread write would remove any mobile-flash hitch,
  and the prism file could persist per-stroke deltas instead of the whole accumulation);
  replace `PaintingRunner.BenchOtherRunners`'s `FindObjectsByType` scan with a static
  registry (runs only on activation — rare); extract the ring fan-layout math shared with
  `SwapToySetCoordinator.Layout` into one helper; unify the LineRenderer config duplicated
  by `ShapeDrawingManager.ConfigureLineRenderer` with `ToyFactory.CreateLine` (touches the
  shape-drawing system, so it belongs in its own change).
- **Full experience (optional).** For a gameplay scene with ecology infra, the original
  `ShapeDrawingManager` (preview cinematic, scoring, reveal, `EndShapeDetailHUD`) remains a
  separate, score-bearing mode — the toy stays scoreless by design.

## Branch: conveyor ("Wanderway") polish

**Reviewed (two adversarial passes — compile, logic, ecology invariants, game-feel, assets, docs).**
Confirmed fixes shipped: (a) `Prism.ResetState` re-arms the scale animator that `SetupDestruction`
disabled, so a fauna-eaten prism re-minted on recycle grows in from zero and weighs volume again (a
latent gap the conveyor was first to exercise); (b) recycle now zeroes every prism's scale so
survivors bloom from zero uniformly instead of morphing; (c) a crystal skimmed mid-flight-to-vessel
is detached from the container and dropped from the belt so it isn't repositioned/scaled and the
slot tops up; (d) per-arrival derived RNG streams keep recycling deterministic under async
interleaving. Invariant audits passed clean (open-space belt mass is `poolSize`-bounded and
collider-LOD'd — valid bounded accumulation, not a leak; lifeforms released only into the containing
cell with all canonical gates; no bare-`Destroy` of visible prisms/crystals on toggle-off or
teardown). Everything below is remaining polish / not-yet-play-verified.

- **In-editor verification (second pass — post play-test rework).** Enter freestyle in
  Menu_Main, fly through the Wanderway toy. Confirm: (1) the toy flips bright + relabels
  "flowing — fly through to stop", and a second pass turns the flow off (label flips back);
  (2) a field of ~7 scenes builds ahead and holds at ANY speed — cruise, full throttle, boost —
  with spacing visibly stretching as you speed up; (3) the belt follows you OUT of the cell and
  keeps streaming in open space (prisms + crystals; no flora/fauna out there), and living
  scenes return when you fly back through the cell; (4) passed scenes clear (suction) at the
  same rhythm new ones arrive; (5) recipes vary strongly — same recipe should land with
  different radii/twists/counts each time; (6) crystals fade in and are skimmable; menagerie
  fauna spawn in the controlling colour and graze; (7) the autopilot lava-lamp vessel never
  trips the toy. Watch the `[ECOSIM]` line — belt steady-state adds ~420 prisms max.
- **Tuning dials** (all on `Toy_Conveyor.asset`): `aheadTargetScenes` (field depth, 3-10) +
  `minSceneIntervalSeconds` (seconds of flight between scenes at speed) are the pacing pair;
  `sceneSpacing` / `recycleBehindDistance` are the low-speed floors; `sceneRadius` + per-recipe
  radii vs. vessel + skimmer size; `transitionSeconds` (suction/bloom read); `poolSize` /
  `prismBudgetPerScene` (density vs. perf); `turnBreakDegrees` (forward-cone half-angle, 20-80° —
  how sharp a turn re-lays the ribbon straight ahead vs. bends it along the curve; lower snaps to
  your new heading sooner, higher follows longer curves before re-laying).
- **Recipe art pass.** The 16 `MicroscenePatterns` recipes are procedural (each re-rolls its own
  radii/counts/twists/bends per arrival) — tune ranges per recipe, and consider authored recipes
  (a `MicrosceneRecipeSO`) if designers want hand-built set pieces in the shuffle bag.
- **Belt audio/VFX.** Suction/bloom currently rides scale only; a whoosh SFX
  (`AudioSystem` gameplay SFX) + a faint particle draw toward the anchor would sell the
  conveyor. Consider a soft chime as a new scene finishes blooming.
- **Elemental-crystal collectibility gap (pre-existing).** `LifeFormCrystal`'s runtime-provisioned
  fallback crystals still lack collection components; the new internal setters
  (`ImpactCollider.SetImpactor`, `ElementalCrystalImpactor.SetCollectionEffects`) make fixing
  that a three-line follow-up.
- **Tests.** `MicroscenePatternsTests` (EditMode) locks the belt's load-bearing generator
  guarantees: budget exactness (closed-system recycling), per-seed determinism, crystal clamp,
  lifeform counts confined to Meadow/Menagerie, and scene-extent bounds. Run with the rest of
  the EditMode suite after any recipe change.

## Branch: framework / cross-cutting

- **Placement anchor.** Menu_Main has **no Cell membrane**, so toys currently ring
  `fallbackCenter` / `fallbackRadius` on `ToyboxController` — tune these to where the lava-lamp
  vessel actually flies. Better long-term: add the standard **Cell** to Menu_Main (the "Cell owns
  the environment" direction), after which placement auto-anchors to the real membrane with no
  code change.
- **Unlock conditions + persistence (deferred by design).** `ToyboxSO.SetToyUnlocked` /
  `OnToyboxChanged` are the hooks; implement persistence (the `FavoriteSystem` JSON pattern:
  load on sign-in → `SetToyUnlocked`, subscribe to `OnToyboxChanged` → save) and real unlock
  conditions when the unlock order is decided.
- **Authored art.** Replace the procedural sphere bodies (domain/painting) with authored art
  prefabs — `ToyDefinitionSO` could reference an optional body prefab that `ToyFactory` uses
  instead of a sphere.
- **Audio / haptics** on activation (`AudioSystem` gameplay SFX; NiceVibrations).
- **Tests.** Extract the `SwapToySetCoordinator` reconcile/flip logic into a pure, unit-testable
  helper and add EditMode tests (the "used toy flips to previous; set = universe\{current}"
  invariant).
- **DI (optional).** `ToyboxSO` is Resources-loaded today; register it in `AppManager` if a
  DI-injected reference is preferred.

## Known limitations (current)

- Mini-ship hulls render as static domain-tinted silhouettes (bind pose, not animated — a
  `SkinnedMeshRenderer.BakeMesh` snapshot would give exact current-pose fidelity); no unlock
  persistence; placement fallback needs in-editor tuning; toy scale/label/spacing still guessed;
  party clients may paint a prism or two in the old colour right after a gate (RTT); the share
  sheet requires a NativeShare-supported platform (editor/desktop open the exported HTML in the
  default browser instead); not yet play-verified. Speed inheritance seeds the smoothed cruise
  speed then eases to the current throttle target — with input paused during the post-swap
  autopilot window it will drift toward `MinimumSpeed`; fine for the seamless-handoff goal, tune
  if a longer hold is wanted.
