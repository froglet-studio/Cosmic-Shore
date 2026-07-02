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

## Branch: painting ("fly by numbers") polish

- **Orientation.** The shape currently billboards to face the vessel at activation
  (`PaintingToy` builds `rot` from vessel→origin). Validate it reads well; consider laying it
  flat or aligning to the flight path. `shapeScale` / `originForwardOffset` / `reachThreshold`
  are on the painting definition — tune.
- **Pen-up/down.** `ShapeDefinition.trailEnabledPerSegment` (smiley eyes, lightning gaps) is
  **not** honored because `VesselPrismController.spawnerEnabled` is private. Expose a public
  trail-spawn toggle to support gaps.
- **Feedback.** Add marker-collect VFX + a completion flourish. Optionally offer multiple shapes
  (a selector, or several painting toys).
- **Full experience (optional).** For a gameplay scene that has the ecology infra, the original
  `ShapeDrawingManager` (preview cinematic, scoring, reveal, `EndShapeDetailHUD`) can drive the
  toy instead of `MenuShapePainter`. The menu uses the lightweight runner so it works with no Cell.

## Branch: conveyor ("Wanderway") polish

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
  `prismBudgetPerScene` (density vs. perf); `courseFollow` (how tightly the belt shadows you).
- **Recipe art pass.** The eight `MicroscenePatterns` recipes are procedural first drafts —
  tune counts/radii per recipe, and consider authored recipes (a `MicrosceneRecipeSO`) if
  designers want hand-built set pieces in the shuffle bag.
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
  `SkinnedMeshRenderer.BakeMesh` snapshot would give exact current-pose fidelity); painting
  pen-up gaps not honored; no unlock persistence; placement fallback needs in-editor tuning; toy
  scale/label/spacing still guessed; not yet play-verified. Speed inheritance seeds the smoothed
  cruise speed then eases to the current throttle target — with input paused during the post-swap
  autopilot window it will drift toward `MinimumSpeed`; fine for the seamless-handoff goal, tune
  if a longer hold is wanted.
