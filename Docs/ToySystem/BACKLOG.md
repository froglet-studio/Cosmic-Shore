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

Shipped in the fly-by-numbers enhancement: multi-stroke multi-domain paintings
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
- **Cross-session prisms.** Progress persists but painted prisms live only as long as the
  scene; after a restart the done strokes render as dim "memory" ghosts. If restoring the
  prisms themselves is ever wanted, that is a mass-law design conversation (re-blooming saved
  mass), not a quick fix.
- **Party-client colour lag.** On a party client, the gate's domain pick takes an RTT to
  replicate, so the first prism or two of a stroke can carry the previous colour.
- **Feedback juice.** Waypoint-collect VFX, gate-pass SFX/haptics (AudioSystem gameplay SFX +
  NiceVibrations — the framework-wide audio item below), a subtle beam from station to its
  monument so ownership reads at a glance.
- **More paintings.** The preset library composes easily (arcs/rects/circles/meridians) —
  candidates: Great Wave, rocket, pagoda, Colosseum. Any `ShapeDefinition` already converts
  via `sourceShape` (pen-up gaps become strokes, now honored).
- **Full experience (optional).** For a gameplay scene with ecology infra, the original
  `ShapeDrawingManager` (preview cinematic, scoring, reveal, `EndShapeDetailHUD`) remains a
  separate, score-bearing mode — the toy stays scoreless by design.

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

- Mini-ship materials render static; no unlock persistence; placement fallback needs
  in-editor tuning; painting prisms don't persist across sessions (progress does; done
  strokes show as dim ghosts); party clients may paint a prism or two in the old colour
  right after a gate (RTT); not yet play-verified.
