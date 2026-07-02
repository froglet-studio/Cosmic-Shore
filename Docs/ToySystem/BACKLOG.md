# Toy System — Backlog & Known Limitations

The core toy system + the three toys are in. This tracks the polish/improvement
work, grouped so each group can be its own follow-up branch, plus current known
limitations and verification status. Architecture: `ARCHITECTURE.md`.

## Verification status

- **Compile-reviewed** against the real codebase twice (all external APIs, the generic
  `SwapToySetCoordinator<T>`, `VesselModelBuilder`, null-safety, internal access, no dangling
  refs) — clean.
- **Not yet play-verified in-editor** (no Unity in the authoring environment). First in-editor
  pass should confirm: toys bloom in and sit where the lava-lamp vessel flies; local-user +
  freestyle gating (autopilot never trips them); the three behaviours below.
- Assets authored by `Tools > Cosmic Shore > Setup Freestyle Toybox` are committed
  (`Resources/Toybox.asset`, `_SO_Assets/Toys/Toy_*.asset`) and `ToyboxController` is wired into
  `Menu_Main`; GUID references verified consistent.

## Branch: vessel-changer polish

- **Mini-model materials.** `VesselModelBuilder` shows the ship prefab's *shared* materials in
  static (bind) pose. Some hull materials are driven by a MaterialPropertyBlock at runtime
  (domain tint), so the preview may look flat/untinted. Options: tint the model to the player's
  domain, bake a dedicated preview material, or accept the silhouette.
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

- Mini-ship materials render static; painting pen-up gaps not honored; no unlock persistence;
  placement fallback needs in-editor tuning; not yet play-verified.
