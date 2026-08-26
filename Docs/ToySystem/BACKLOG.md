# Toy System — Backlog & Known Limitations

The core toy system + the six toys are in. This tracks the polish/improvement
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
- Assets authored by `FrogletTools > Scene Setup > Setup Freestyle Toybox` are committed
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

- **In-editor tuning pass.** `paintingClearance` (drives the width-aware monument pitches),
  preset sizes vs. the lava-lamp play area, gate/milestone ring radii, ghost alphas,
  celebration timing — all first-guess values.
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
  **All five representational subjects are now baked from real references** via the offline
  pipeline (`Tools/PaintingPipeline/`, licences audited in `REFERENCE_MODELS.md`): **Lion's
  Head** (CC0 Temperance Union Lion scan), **Starry Night** (v2 retrace of the painting's own
  brush flow), **Phoenix** (threedscans Striding Eagle, no restrictions), **Peacock**
  (YahooJAPAN Peafowl — CC-BY 4.0, attribution ships in the asset description AND must appear
  in the game credits), and **Almighty Mountain** (the real Matterhorn DEM via AWS Terrain
  Tiles — attribution line in `Tools/PaintingPipeline/README.md` must ship in the credits
  screen). Extra baked-painting candidates from the same haul: the Medici Riccardi Horse Head
  and the Glycon serpent (both threedscans, no restrictions). Remaining procedural candidates:
  Great Wave, pagoda, Colosseum.
- **Perf / in-editor pass for the big paintings** (review-verified, deferred by design). Two
  structural costs confirmed by the pre-PR review: (1) `PaintingRunner.Begin` eagerly creates one
  ghost `LineRenderer` per stroke — Phoenix (260) and Peacock (236) keep 200+ lightweight
  LineRenderers alive for the whole run (the property-write storms are transient: bloom 1.4s,
  celebrate 3s, bench fades) — candidates: merge Pending strokes into one renderer per domain, or
  a stream-in window around the active stroke; (2) `PaintingToyDefinitionSO.Spawn` synchronously
  runs `EnsureStrokes` + `MiniaturePaintingBuilder` for all 16 paintings on the toybox-spawn frame
  in Menu_Main — the 11 procedural presets regenerate there (baked assets skip generation) —
  candidate: amortize one painting per frame via UniTask, and cache `BuildDefaultGallery`
  statically so the empty-list fallback stops regenerating per spawn. Both want profiler numbers
  on mobile before restructuring (CLAUDE.md: profile first).
- **Reviewed and deliberately deferred** (from the enhancement's review pass): coalesce the
  per-stroke synchronous saves (`DataAccessor` full-file JSON writes at each stroke boundary —
  both the small progress file and the growing `PaintingPrismStore` drawing-state file; the
  writes are human-paced but a debounce/off-thread write would remove any mobile-flash hitch,
  and the prism file could persist per-stroke deltas instead of the whole accumulation);
  replace `PaintingRunner.BenchOtherRunners`'s `FindObjectsByType` scan with a static
  registry (runs only on activation — rare); extract the ring fan-layout math shared with
  `SwapToySetCoordinator.Layout` into one helper; unify the LineRenderer config duplicated
  by `ToyFactory.CreateLine` (`ShapeDrawingManager.ConfigureLineRenderer` was deleted
  with C15; remaining LineRenderer config lives on the toy side).
- **Reviewed and deliberately deferred (pre-PR review pass).** Verified findings fixed in that
  pass: closed-loop instant-complete, disengaged-milestone latch, milestone-trigger NRE during
  vessel swap (shared null-guarded `Toy.TryGetLocalVessel`), benched-gate forever-lerp, ridden
  line now eases out/in (continuity law), monument layout now width-aware, six asset YAML
  descriptions quoted, dead toolkit API pruned, `TorusKnotPreset` reuses `Tk.TorusKnot`.
  Deferred with rationale:
  - *Ride-feel constants in code* (checkpoint spacing `max(90, 0.085·diag)`, 28° turn limit,
    milestone radius `max(18, reach·1.8)`): derived heuristics, not designer knobs yet —
    promote to `PaintingToyDefinitionSO` fields when the in-editor tuning pass wants to move
    them (CLAUDE.md config-separation).
  - *Perfect-ride juice lost its visual* — the guide line (and its `_rideGlow` brightening) was
    replaced by the standard `ObjectiveIndicator` per the prompter; re-express the perfect-ride
    reward as in-world juice (milestone-ring emission/pulse when hugging the curve) in the
    tuning pass.
  - *Milestone ring create/destroy per checkpoint*: one small GameObject per ~90u of flight —
    same lifecycle as gates; pool only if the profiler pass flags it.
  - *`TrySpawnRestoredPrism` mirrors `VesselPrismController.CreateBlock`* (0.6s collider window
    literal; skips danger/shield branches + creation events — the event skip is intentional to
    avoid re-capture): extract a shared post-spawn setup on `VesselPrismController` so restored
    prisms can't drift from live-painted ones.
  - *`ToyboxSetupTool.LoadOrCreatePainting` returns existing assets untouched*: catalog edits
    don't propagate to committed assets on re-run — needs an update-in-place pass (like the
    tool's `extra` SerializedObject pass for toys) that respects baked strokes.
  - *Stroke conventions enforced only in the offline baker*: `PaintingDefinitionSO` wants an
    `OnValidate`/`EnsureStrokes` warning for Blue-domain or degenerate inspector-authored
    strokes (a Blue stroke currently paints in the player's current colour and records that
    into `PaintingPrismStore`).
  - *Fallback/baked identity*: DefaultGalleryCatalog reuses the baked paintingIds, so if the
    toy's paintings list is ever emptied the procedural fallback resets saved progress on its
    first write (totalStrokes mismatch, by design). Acceptable while the committed
    `Toy_Painting.asset` list stays populated; split the ids if that ever changes.
  - *`BillboardLabel` one-LateUpdate-per-label* (~20 in the full toybox): fold into a single
    manager iterating a static list if the profiler pass flags it (pole-degeneracy guard is in).
  - *Toolkit `Rng` vs seeded `System.Random`* (Microscene convention): kept deliberately —
    xorshift32 is stable across .NET runtimes, `System.Random`'s algorithm is not guaranteed.
  - *`CatmullRomPoint` duplicates `SpawnableWaypointTrack.CatmullRom`*: unify in a shared math
    utility in its own change (touches the environment system).
  - *Phoenix preset fallback*: the flame fill's Ruby branch is dead (seed y-range never crosses
    the threshold) — all flames come out Gold; harmless (single recolour), fix with the next
    preset-content pass.
- **Full experience (optional).** The scored `ShapeDrawingManager` minigame was
  **deleted 2026-08-25** (C15). Recover from git if a scored gameplay scene is
  wanted. The toy stays scoreless by design.

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
  Menu_Main, fly through the Wanderway toy. Confirm: (1) the toy's emblem **spins up** to flowing
  speed over ~0.8s and it relabels "flowing — fly through to stop", a second pass stops the orbit
  (label flips back), and leaving freestyle with the belt running drops the orbit to a dormant
  crawl rather than to a stop — and no OTHER toy's body changes colour (the old shared-material
  write that did exactly that has been deleted);
  (2) a field of ~7 scenes builds ahead and holds at ANY speed — cruise, full throttle, boost —
  with spacing visibly stretching as you speed up; (3) the belt follows you OUT of the cell and
  keeps streaming in open space (prisms + crystals; no flora/fauna out there), and living
  scenes return when you fly back through the cell; (4) passed scenes clear (suction) at the
  same rhythm new ones arrive; (5) recipes vary strongly — same recipe should land with
  different radii/twists/counts each time; (6) crystals fade in and are skimmable; menagerie
  fauna spawn in the controlling colour and graze; (7) the autopilot lava-lamp vessel never
  trips the toy; (8) **you never watch a scene appear in your face or suction away in view** —
  scenes bloom in only at a distance ahead and a scene is only reclaimed once it is fully off
  screen (fly straight, then hard-turn and reverse: the old ribbon should wait to recycle until
  it has left your view, briefly idling rather than popping away). Watch the `[ECOSIM]` line —
  belt steady-state adds ~420 prisms max.
- **Tuning dials** (all on `Toy_Conveyor.asset`): `aheadTargetScenes` (field depth, 3-10) +
  `minSceneIntervalSeconds` (seconds of flight between scenes at speed) are the pacing pair;
  `sceneSpacing` / `recycleBehindDistance` are the low-speed floors; `sceneRadius` + per-recipe
  radii vs. vessel + skimmer size; `transitionSeconds` (suction/bloom read); `poolSize` /
  `prismBudgetPerScene` (density vs. perf); `turnBreakDegrees` (forward-cone half-angle, 20-80° —
  how sharp a turn re-lays the ribbon straight ahead vs. bends it along the curve; lower snaps to
  your new heading sooner, higher follows longer curves before re-laying);
  `minPlacementDistance` (hard floor on how close a scene may bloom in — keep ≤ `firstSceneDistance`)
  + `offscreenMargin` (extra padding on the `sceneRadius` bounding sphere that must clear the camera
  frustum before a scene may recycle — larger = more buffer against turning mid-suction, at the cost
  of the belt waiting a touch longer for scenes to leave view). By design these can briefly *stall*
  recycling when the whole field is on screen (near-stationary or mid-U-turn); the belt idles and
  self-heals as motion pushes scenes out of view — it never pops one away to keep flowing. A future
  hardening could re-check frustum visibility per-frame *during* the ~`transitionSeconds` suction
  (today it is gated once at selection, with the margin as the buffer) — not needed at current dials.
- **Recipe art pass.** The 40 `MicroscenePatterns` recipes are procedural (each re-rolls its own
  radii/counts/twists/bends per arrival) — tune ranges per recipe, and consider authored recipes
  (a `MicrosceneRecipeSO`) if designers want hand-built set pieces in the shuffle bag.
- **Diversity pass (shipped).** Recipes stamp structural metadata (`MicroscenePlan.CloseStructure`)
  and `MicroscenePainter` paints along it: 8 domain schemes over the full triad (per-structure
  rainbows, flight gradients, pinwheels, stripes, mirrors), 7 kind schemes using danger/shield as
  palette tools (danger gates/tips, armoured frames, keystone landmarks — shield caps unchanged),
  scale moods (uniform × long-axis stretch × structure taper, per-axis family jitter), plus 12 new
  recipes on superstructure-oriented primitives (domes, grottos, torus knots, Möbius rails,
  rosettes, terrace spirals, banked ribbon chicanes, split tubes, 4 spine×motif Medley composers).
  In-editor check: ride the belt and confirm most scenes carry structural colour, danger structures
  read as deliberate hot gates (and slam you on contact — friendly fire is the design), shielded
  ribs shrug off weapon fire, and mono/plain scenes still occur as breathing room. Tune the
  `Toy_Conveyor.asset` palette weights to taste.
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
- ~~**Authored art.** Replace the procedural sphere bodies with authored art prefabs.~~
  **Answered procedurally instead** (and better): every toy root is now a `ToyEmblem` built from
  the toy's own real content — mini hulls, real painting strokes, real species, real microscenes,
  the live world. An authored art prefab would be a decorative stand-in, which is the thing the
  station-icon rule forbids. See `ARCHITECTURE.md` § "Toy-root emblems".
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

## Lifeform Matrix follow-ups

**Kingdom pass (shipped) — verification, none of it play-verified:**

1. Fly the toy in freestyle. Three kingdom stations bloom one layer out: **Fauna** wearing a mini
   tadpole, **Flora** wearing a gyroid growth preview, **Vessels** wearing a mini hull in *your*
   domain colour. Anonymous spheres mean an icon builder failed — check the console.
2. Fly **Vessels**. Eight mini hulls bloom a layer further out (the shared
   `ToyVesselRoster.Default` roster). Fly one: an AI-piloted vessel of that class, in your domain,
   appears one spacing back toward the cell centre facing inward, and flies off on autopilot.
   The console logs `[MenuServerVesselInit] Released AI companion '<Class> Bot N' (...)`.
2b. **Watch the trail.** It starts ~2.1s after release (`VesselPrismController.startDelay`) and
   must then stay on. A companion with no trail means it is under the spawner's 3 u/s gate — check
   `companionLaunchSpeed` on the menu initializer, and check the bot is not stuck in a drift (a
   held drift pins cruise speed at the value carried in). Release several of DIFFERENT classes:
   only Dolphin / Rhino / Serpent / Sparrow have an authored AI ability, and the Dolphin's is a
   drift, so that hull is the one that exposes a launch-speed regression first.
   **The RHINO is the known exception and is NOT a toy bug**: its trail is 0.75 volume per prism
   (4x smaller than any other hull's) and reads as absent at any distance, because its
   trail-growing ability has never run. Cause, the reason the one-line fix is unsafe alone, and
   the design fork it needs: `Docs/ElementalAbilitySystem/BACKLOG.md` item 27.
3. Change domain at the domain-changer toy and come back: every mini hull re-tints in place, and
   the next companion you release joins the new domain.
4. **Party check (the point of the ServerRpc):** with a second client joined, release a companion
   from the CLIENT. It must appear on both machines. Releasing from the host must likewise show on
   the client.
5. Launch an arcade game and return to the menu: every companion is gone
   (`SceneLoader.ClearPlayerVesselReferences`), and no `[FLOW-5] ... returned NULL` warnings.
6. Fauna/Flora branches must behave exactly as before, one layer further out.

**Open follow-ups from that pass:**

- **No cap on companions.** Every hangar pass releases another AI player + vessel, and nothing
  removes them until you leave the menu. That is toy-faithful (no timer, no cull) but it IS a real
  collider + simulation cost, unlike the transient stations. If it needs a bound, the honest shape
  is a per-cell ceiling read off what is actually in the cell — the same lesson
  `AstroLeagueBall.cellBallLimit` records (a rule enforced at one PRODUCER only ever sees that
  producer) — plus an active, visible removal, never a clock.
- **Companion names are generated** (`"<Class> Bot N"`) rather than drawn from
  `SO_AIProfileList`, because the menu initializer has no profile-list reference and adding one
  means scene wiring. Wire `aiProfileList` on `MenuServerPlayerVesselInitializer` if the bots
  should read as characters.
- **`companionSkill` is authored at 0.5** on the menu initializer, since the menu has no intensity
  to derive one from. Tune in play.
- **The hangar has no "release N" affordance** — one pass, one bot. A held pass, or a size
  telegraph on the station (the way a variant station draws its crystal at that variant's own
  authored heart size), is the obvious extension.

- **Charge tadpole is NEW and untuned** (authored from the Space baseline with a Charge
  crystal) — tune via the matrix, then bake into `Tadpole Fauna Charge.asset`.
- **Not in the elemental contract yet**: Seaweed (`SpawnableCord`, not a `Flora`), drone
  populations (BoidManager path — now all spawn the base tadpole; needs its own config pass
  for per-element identity). (Worms: the legacy trio was deleted; every segment of the
  rebuilt worm colony carries an authored elemental heart — Docs/ECOSYSTEM.md §23.8 — though
  the colony doesn't yet roll the element spread. The level half of that spread is retired
  outright: a lifeform is its species and its element, Docs/ECOSYSTEM.md §39.)
- **Sparrow (and other vessels') HUD ability-icon bindings** for the shared upgrade-highlight
  system are unwired (Squirrel only); fill each view's `abilityIcons` in its prefab.
- **Squirrel HUD tube/energy icons repaint colours per-frame**, so the upgrade highlight
  reads via scale only there — teach those repaints to respect the highlight tint.
- **Variant matrix stations beyond the membrane**, and the kingdom pass made it further out.
  Layers sit at `(1.5 + 2n) × stationSpacing` from the toy along the outward radial, so with
  `spacing 90` the variant matrix is now 495u out (was 315u) from a toy placed at ~0.82 of the
  1200u membrane — roughly 280u outside it (was ~100u). Spawns still work (they resolve the cell
  from the TOY's position, which is always inside), and fauna hatch on the cell's densest mass
  rather than at the station, so only FLORA actually root out there. Options if it reads badly:
  clamp the flora plant position back inside the membrane, or tighten the per-layer gap from 2
  spacings to 1.5 (which costs the corridor's readability). Play-test before choosing.
- ~~**The four element-crystal "moons" are probably invisible**~~ — resolved by the toy-root
  emblem: the crystals moved onto `ToyEmblem`'s core sub-ring, which is sized off
  `Placement.BodyRadius` like every other emblem. Item kept only so the fix is not re-litigated.

## Cell Selector follow-ups

Shipped on `claude/freestyle-cell-selector-toy-*`. Design + invariant analysis:
`Docs/ECOSYSTEM.md §19`; surface description: `ARCHITECTURE.md § Cell Selector`.

**Verification (in-editor, Menu_Main — none of this is play-verified):**

1. **The boot win is the headline.** Enter Menu_Main cold: no `EnvironmentLoadVeil`, no
   "GROWING …" hold. Launch any arcade game and return: same. Console should log the Cell
   assigning **Blob**. That is the whole point of the branch — verify it first.
2. **Pick a world.** Enter freestyle, fly the Cell Selector (300° around the membrane ring);
   a matrix of mini-cells blooms outward. Fly e.g. **Yggdra**: the (empty) world suctions,
   the veil raises with the usual prism/percent readout, Yggdra grows in. Confirm its
   `PhaseThresholds` are in force afterwards (the cell should read Calm, not boot to Frenzy).
3. **The reset.** With a world loaded and a long trail laid, fly the toy again and pick the
   **same** cell (label reads `RESET`). Everything suctions away — environment, flora/fauna,
   *and* your trail — and grows back fresh. Watch for `Trail`/`TrailFollower` NREs; the
   detach in `SetVesselTrailsDetached` is what prevents them, so this is the risky path.
4. **Squirrel specifically** — it is the tube-rider (`AttachedPrism`, `TrailFollower`). Reset
   while attached to a trail and confirm no exception storm.
5. **Teardown cost.** The 500-prisms-per-frame drain should keep the retire smooth; profile a
   Yggdra→Geode swap and raise/lower `PrismsPerFrame` if it hitches or drags.
6. **Pool health.** After several resets, confirm trail prisms still spawn at full size and
   the interactive prism pool has not drifted (the pooled prisms take a
   `SetParent(null,false)` → `ReturnToPool()` path specifically to avoid baking the suction
   scale into `localScale`).
7. **Wanderway interaction.** Run the conveyor, then reset the cell: the belt's own stock is
   instantiated (no pool handler) so it must survive untouched. If it does not, the
   `OnReturnToPool == null` discriminator in `RetireWorldIntoSuctionRoot` is wrong.
8. **`retireSuctionSeconds`** (Cell inspector, default 1.1) — tune for feel.
9. **Scale models.** Opening the matrix should show each world's real silhouette inside its
   mini-cell, blooming in one per frame. Watch the frame time while they stream: each is one
   environment generation (pure math, no prisms). If a single one hitches badly, drop
   `modelPointBudget` — it affects mesh size, not generation cost — or move generation off the
   main thread. Confirm the models look like the worlds they build (fly Yggdra, come back, and
   compare) and that the Blob mini-cell is empty.
10. **Model memory.** `ReleaseGeneratedData()` is called right after sampling, so the seven
   34k-entry lay lists must NOT stay resident. Check the memory profiler after opening the
   matrix; if they linger, the release is not landing.
11. **Layout knobs** on `Toy_CellSelector.asset`: `stationSpacing` 55, `stationRadius` 9,
   `matrixDistanceFactor` 3 (how far out the matrix sits — doubled from the first pass),
   `modelPointBudget` 1200 are all guesses at menu scale.

**Known limitations / follow-up work:**

- **Local-only selection.** In a party each client picks its own cell. Strictly better than
  the `Random` roll it replaced (which already differed per client), but the honest fix is a
  server-authoritative pick: a `CellNetworkSync` RPC carrying a config index, host-only toy
  activation, clients following. Needs a design call on who owns the menu world.
- **The player flies blind under the veil.** They opted in, and it matches a scene load, but a
  danger prism from a Caldera build could land on them. Options if it bites: park/autopilot the
  vessel for the hold, or re-pose it to the cell centre before the build. **Got worse, twice:**
  Caldera's danger count went 858 → 1,503 in the de-gravitized rework and its total mass 25k →
  41.4k prisms in the 2× pass, so it is now both the spiciest and the longest-to-build world in
  the rotation (`Docs/ECOSYSTEM.md` §18.1). Ourobor (§18.2, 37.9k) is the second-longest but
  carries zero danger.
- **Toy placement does not re-derive after a swap.** `ToyboxController` rings the membrane once
  at `OnClientReady`. Every freestyle config shares one membrane prefab so the radius does not
  change today; a config with a different membrane would leave the toys mis-ringed.
- **`Cell.Initialize` during a swap is unguarded.** Nothing raises `OnInitializeGame` mid-session
  in Menu_Main, so it cannot happen today; a guard on `_swapping` would make that structural.
- **A model now generates the environment**, so the prism count IS knowable at matrix-open time
  (`CachedLays.Count` before the release). If a load-cost telegraph is wanted, stamp it on the
  station label from that count instead of authoring a hint field.
- **Model generation is on the main thread.** One environment per frame keeps it tolerable, but a
  heavy generator will still show as a hitch. The generation is pure math over value types, so it
  is a good Burst/Job candidate if it bites.
- **Nested generators preview their own placement points**, not their descendants' prisms. Every
  freestyle environment is a leaf node, so this is graceful degradation rather than a live gap —
  but a future nested environment would show a sparse model.

## One-toy-opens-into-many follow-ups

Shipped on `claude/freestyle-cell-selector-toy-*`. Three toys now share `MatrixToy`: fly one
station, its options unfold out ahead; fly it again, they fold away. Architecture:
`ARCHITECTURE.md § The "one toy, then many" pattern`.

**Verification (in-editor, Menu_Main freestyle — none of it play-verified):**

1. **All three unfold and fold.** Cell Selector, Connect the Dots, Vessel Changer: one pass opens
   the matrix at `matrixDistanceFactor` × `stationSpacing` out along the outward radial, a second
   pass folds it away (shrinking, not popping). Confirm the matrix is reachable — you fly at the
   toy and keep going — and does not land inside the membrane or on top of another toy's matrix.
2. **Cell Selector has no orbs.** Each slot is the bare scale model + its label. Nothing ringing it.
3. **Vessel Changer.** The matrix shows every ship except the one you fly; flying one swaps you
   and the matrix closes behind you. Control still returns after the swap
   (`RestoreControlAfterSwap`), and the mini ships re-tint when you use the domain changer while
   the matrix is open.
4. **Painting gallery — the risky one.** Start a painting, fly the gallery toy to FOLD the matrix
   mid-run, then fly it again to re-open. The run must still be going, and the station must
   re-adopt it (progress on the label, no second runner spawned on the same canvas). The runner is
   parented to the toybox root and `PaintingToy.ActiveRuns` is what makes this work.
5. **First gallery open cost.** Opening the gallery generates strokes for all 16 paintings (for
   the monument packing). This used to be paid at menu **boot** — it is now on the first open, so
   expect a hitch there and confirm boot is clean. If the hitch is bad, stream the packing/station
   build over frames the way the Cell Selector streams its models.
6. **Domain Changer is unchanged** — still a `SwapToySetCoordinator` flip-set (its universe is two
   toys). Confirm it still flips to the domain you just left.

**Known limitations / follow-up work:**

- **Matrix collision between toys.** Each toy's matrix blooms outward from its own slot; with six
  toys ringed around the membrane and matrices three spacings out, two adjacent open matrices
  could overlap. Only one is normally open at a time, but nothing enforces that — a "close the
  others when one opens" coordinator on `ToyboxController` would.
- **`MiniaturePaintingBuilder` runs 16 times in one frame** on the first gallery open (see #5).
- **The vessel matrix rebuilds its models on every open.** `VesselModelBuilder` extraction is
  cheap next to the painting/cell cases, but caching them like `CellSelectorToy._miniatures`
  would make repeat opens free.
- **Cell swap vs. painting pen (cross-toy).** `VesselPrismController.SetSpawnerPaused` is one
  last-writer-wins flag shared by the cell swap's pen-up and the painting runner's between-stroke
  pen-up. Reset the cell while a painting is between strokes and the swap un-pens it; the runner
  re-asserts at its next stroke boundary, so the cost is a short stretch of unwanted trail. A
  reset also clears the painting's laid prisms — but `PaintingPrismStore` regrows them when you
  return to the station, which is the designed resume path. Left as-is deliberately: a
  refcounted pen would be more machinery than the bounded, self-correcting symptom warrants.
- **Asset drift cleaned in passing** (`Toy_VesselChanger` had a stale `vesselCycle` key with no
  field behind it; `Toy_Painting` never serialized `clusterSpacingBodies`, so that knob read 0
  and was masked by the `max()`). `Toy_Painting` still carries a stale `shape:` key — harmless,
  Unity drops unknown keys on the next save.

---

## Wanderway — the run (bare canvas · rolling tether · way home)

**In-editor verification (a human at the editor is the gate — none of this was play-tested):**

1. **Enter Menu_Main, take freestyle, fly the Wanderway toy.** The cell should suction away and come
   back as the bare Blob behind ONE load veil (not two covers back to back) — the run requests the
   cell swap before the belt's stock build so they share the hold. The toy relabels
   "wandering — fly through to come home".
2. **Fly out and watch the trail.** It should stabilise at ~100 prisms of length and stay there:
   the tail withers (shrinks away, never pops) and the head keeps laying. Confirm the total does
   NOT keep climbing — if it does, one of the two ribbons is not being rolled.
3. **Turn around.** Your trail is behind you and the RETURN station sits at its far end, gliding
   (not snapping) as the tail advances. Fly through it: belt stops, toy relabels, vessel returns to
   where the wander started with its speed intact.
4. **Repeat the wander.** The belt must RESUME, not re-prime — watch for a second veiled build or a
   doubled prism count, which would mean the stock was minted twice.
5. **Exit via the overview button and via gamepad Start** (instead of the station). Both should end
   the run and bring the vessel home, since both drop freestyle.
6. **Squirrel specifically:** ride your own tether (tube-riding attaches a `TrailFollower`). The
   rider must stay on the prism it attached to as the tail recycles — if it races forward along the
   ribbon, the `Trail.OnOldestRemoved` compensation is not firing.
7. **Tuning knob:** `Toy_Conveyor.asset ▸ tetherPrisms` (100). It is a per-ribbon LENGTH — a
   double-trail vessel holds 2× the prisms for the same visible tether.

**Collider-budget impact:** *negative* (an improvement). The rolling tether bounds the local
vessel's live trail at ~100 prisms/ribbon for the duration of a run, where it was previously
unbounded; recycled prisms return to the pool with their colliders. The belt's 30k stock is
unchanged by this work.

**Known limitations / follow-up work:**

- **Ending a wander leaves you in the Blob cell.** Restoring the world you had before is
  deliberately the Cell Selector's job, not the wander's. If "put my world back" is wanted, it needs
  a remembered-config hook on `WanderwayRun` and a second veiled swap on exit.
- **The belt's scenes stay in the world after a run ends.** Conserved mass and released citizens are
  not toy props to vanish; they are strewn along wherever the player wandered. Harmless today, but a
  long session accumulates them far from the cell.
- **Pen-up is untouched by the run now** (the rolling tether replaced the pen-up tether), so the
  cross-toy last-writer-wins note above applies only to the cell swap vs. the painting runner.
- **`WanderwayRun` ticks at 0.2s.** At very high speed a burst of lays drains over a few ticks
  (bounded to 64 removals/tick/ribbon). If a boosted Squirrel visibly overshoots the tether length
  before it settles, lower `TickSeconds` or raise the per-tick guard.
- **Only the LOCAL player's trail is tethered.** Wanderway is a solo freestyle mode today; if a
  party ever wanders together, each client tethers its own vessel and remote trails are untouched.

## Toy-root emblems — known-remaining follow-ups

- **Ring-distance legibility pass (gate for removing the labels).** Fly the full membrane ring and
  record which toys are NOT identifiable without reading their label. Expect the Lifeform bench and
  the Cell Selector to be the first to fail. The only levers are the const block at the top of
  `ToyEmblem` — raise `SatelliteRadiusBodies` first, then `OrbitRadiusBodies`, but the outer extent
  (`OrbitRadiusBodies + SatelliteRadiusBodies`) × R must stay under the 42u trigger radius.
- **Two pre-existing material leaks, deliberately left in scope-free.** `VesselChangerToy.BuildStation`
  and `LifeformMatrixToy.AddSpeciesModel` still call the COLOUR overload of `ToyModelBuilder.TryBuild`
  on the matrix-station path, orphaning one `Material` per model per matrix open (UnityEngine.Objects
  are never GC'd). The new `Material` overload — which the emblems use, and which lets one owner
  share and destroy a single material — makes adopting the same pattern there a small follow-up. Not
  done here so this change doesn't also alter matrix-station behaviour.
- **Flora icons are previews, not a second growth implementation.** Each `TryPreviewGrowth` mirrors
  its species' own rule and shares code with it where the rule is static (the Schwarz P surface math,
  the gyroid bond table). The two places they can drift are `BranchingFlora`'s branch step/scale
  falloff and `WallAssembler`'s bond offsets, which are re-expressed rather than shared. If either
  changes, re-check the icon.
- **Emblem legibility vs. the label position.** The emblem's outer extent (33.4u) and the label
  height are independent numbers. Since the switch-ring pass the label is *derived* from the ring
  (`ToyFactory.SwitchRingLabelHeight`) rather than from the body radius, so the pair that has to
  keep clearing each other is now **emblem outer (33.4u) vs. ring inner (38.6u)** — 5.2u at R=22.
  If the toybox's `toyBodyRadius` or `toyTriggerRadius` is retuned, re-check that gap.

## The switch (rings) — known-remaining follow-ups

- **Label crowding in the dense matrices is PRE-EXISTING and got no worse where it matters, but it
  is still there.** A station's label hangs clear of its own ring; in the tight grids it still ends
  up inside the *neighbour's* footprint. Measured: the Vessel Changer (spacing 60, ships fitted to
  radius 22 → only 16u of clear air between hulls) and the Painting gallery (spacing 140.8, rings
  63.4 → 3.9u between rings) both already overlapped their neighbours' models *before* rings
  existed; the ring only makes it visible. The three real fixes, in preference order: (1) drop the
  labels — the ring now carries the far read the label used to, which is the gate the emblem
  section already describes; (2) widen `VesselChangerToyDefinitionSO.stationSpacing` (and
  compensate `matrixDistanceFactor`, since the matrix distance is a multiple of the spacing);
  (3) shrink the ringed-label font, which is deliberately left at its historic
  `contentRadius × 1.425` so this pass changed affordance and not typography. **Do not "fix" it by
  lowering the label back onto its own ring.**
- **The clamp is holding two matrices now, not three.** `ToyFactory.MaxRingSpacingFraction` (0.45)
  is what keeps the Vessel Changer (1.7u between rings) and the Painting gallery (3.9u) from
  interpenetrating. The third used to be the level-5 Lifeform variant station at 2.5u, whose radius
  scaled with level; levels are retired (Docs/ECOSYSTEM.md §39) so every variant station is now the
  plain `StationRadius` with a 48.5u gap, and its clamp is no longer exercised. It cannot go much
  lower: at 0.36 the Vessel
  Changer's ring inner radius (21.6) would fall *inside* its own 22-radius ship. If a matrix's
  spacing or station radius is retuned, re-run the geometry check in `ARCHITECTURE.md` §
  "The switch" rather than nudging the constant.
- **Not yet play-verified.** In-editor pass should confirm: every toy root and every matrix station
  blooms in already ringed; the ring reads as the thing you aim at from the far side of the
  membrane; the Cell Selector's current world is legible as *two* rings (outer switch, inner
  counter-spinning halo) rather than one thick rim; the domain changer is visibly unchanged; and
  the Wanderway return station's hoop turns to face you as you fly back down the tether.

## Cell Selector — a GROWN cell has no scale model (Aug 2026, Lattice cell)

`CellMiniatureBuilder` builds a station's model by striding the environment generator's own output
(`GetTrailData` + `CellEnvironmentSpawnableBase.CachedLays`). A config with no `EnvironmentPrefab`
has no generator, so its slot reads visibly empty — which was exactly right while Blob was the only
such config ("you are in the empty one") and is now only *half* right, because the matrix carries
**two** blank stations:

- **`Barren`** (`CellConfigs[9]`) — blank is the truth. It is open water: no environment, no flora,
  no fauna. Nothing to fix.
- **`Lattice`** (`CellConfigs[0]`) — blank is a **lie**. It grows eight lattice superstructures to
  ~21,600 prisms; it is one of the densest worlds in the game and it previews as nothing. Its label
  is currently the only thing distinguishing it from the empty option.

This is not a `CellMiniatureBuilder` bug — there is genuinely no lay data to stride, because the
mass does not exist until plants grow it. Three directions, in preference order:

1. **Sample the LIVE cell when it is the current world.** `CellSelectorToy` already prefers the
   live environment's cached lays for its own emblem; a grown cell could feed the same path from
   the prism spatial index instead of from `CachedLays`. Only works for the world you are already
   in, which is the one case where the halo already tells you.
2. **Bake a still.** Author a small mesh (or a sprite) on `CellConfigDataSO` for configs that have
   no generator, so a grown world can advertise its shape without one. Costs an authored asset per
   such cell, and drifts from what the cell actually grows.
3. **Run the assembler headless for N plants.** Exact and self-maintaining, but it is a real
   simulation — the reason `CellMiniatureBuilder` strides lay data in the first place is that
   generating is the expensive part.

Until one lands, a grown cell in the selector is identified by its **label** alone. Note this
becomes more pressing, not less, if more grown-environment cells ship.

## Vessel matrices — live hulls (2026-08-25)

Stations in the vessel changer and the Lifeform Matrix hangar now show the ACTUAL ship
(`ToyVesselRoster.TryBuildLiveHull`) rather than a flat silhouette, with the vessel vision band
supplying the domain read (`Docs/VESSEL_VISION.md`, `Docs/ToySystem/ARCHITECTURE.md` § "Vessel
Changer"). Open items:

- **Glyphs still use the flat fill, and that is deliberate** — a toy's emblem and the kingdom icons
  sit inside the band's near cutoff where a real hull is a black blob. If emblems ever want real
  hulls, they need their own lighting answer, not the band.
- **The station's mark depends on the matrix geometry.** It works because the matrix blooms
  `StationSpacing × MatrixDistanceFactor` = 360 u, just past the band's `nearFullStart` (350).
  Anyone re-tuning `stationSpacing` or `matrixDistanceFactor` on `Toy_VesselChanger.asset` should
  re-check that the stations still land inside the band, or they will silently go unmarked.
- **Skinned mini hulls show their bind pose.** Unchanged by this work, but more visible now that the
  real materials are on: a skinned ship's mini model is static in its authored pose.
- **Not verified in-editor yet** — the live-hull path, the domain-material swap and the re-tint
  dispatch are machine-type-checked only. See `Docs/VESSEL_VISION.md` § "What a human still has to
  check in the editor", step 6.
