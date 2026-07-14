# Toy System — Architecture

> Freestyle is ~¼ of the final product. While we are a party game, that is only half —
> what makes the rest right is that it is filled with **toys** instead of party games.
> A toy has **no score and no end condition**; it is something you can play with
> indefinitely. The **toybox** is the set of toys the player has unlocked, surfaced in
> the Menu_Main lava-lamp / freestyle world.

This is a new platform **fundamental** (`Toy`), added at the prompter's request. It
composes with the existing fundamentals rather than working around them:

| Composes with | How |
|---|---|
| **Vessel** | the Vessel Changer toy cycles the player's ship via the existing networked swap |
| **Domain** | the Domain Changer toy cycles the player's team colour via the server RPC |
| **Prisms / Mass** | the Painting toy lays a *conserved-mass* prism pattern (no caps/TTLs); the Wanderway conveyor transports a fixed stock of conserved prisms |
| **Cells** | toys are placed relative to the Cell membrane (read, never duplicated); Wanderway lifeforms spawn *into* the cell as ordinary citizens |
| **Flora & Fauna / Crystals** | Wanderway meadow/menagerie scenes release flora/fauna through the canonical cell spawn sequences and lay skimmable elemental crystals |

A toy imposes no decay, no timer, and no win/lose — consistent with *Mass is conserved*
and *don't cheat emergence*.

## What a toy is

A **toy** is a world-space interactive station the **local** player's vessel flies into
to activate. It is modelled on the existing menu world-triggers (`FreestyleSign`,
`ModeSelectTrigger`, `ShapeSign`): a trigger collider + `GetComponentInParent<VesselStatus>`
detection. On top of that base the `Toy` class adds:

- **Local-user gating** — only the locally-owned, non-AI vessel can trip a toy
  (`IVesselStatus.IsLocalUser`). Remote/party vessels never trip your toys.
- **Freestyle-only gating** — toys are visible in the lava lamp but inert while the vessel
  is on autopilot; they only activate once the player takes control
  (`ToyContext.IsFreestyleActive`).
- **Continuity-law bloom-in** — every toy scales from zero on spawn (nothing pops in).
- **Exit-gated re-arm** — a toy is *not consumed*, but it re-arms **only once the local vessel
  has flown clear of its trigger volume** (a per-frame distance poll with `exitRadiusMultiplier`
  hysteresis, robust to the swap's despawn/respawn which fires no `OnTriggerExit`). A swap toy
  that flips to the option you just left also **re-grows slowly** (`regrowDuration`, default ~5s)
  and stays inert while it does. Together these stop a toy from immediately switching you back
  before you can escape it. And when any toy in a set fires, the coordinator disarms the **whole
  set** (`Toy.Disarm`) so a vessel that re-spawns on top of a neighbour can't chain-trigger it.
  `Toy` exposes `bloomDuration`, `regrowDuration`, and `exitRadiusMultiplier` as serialized knobs.

## File map

| Role | File |
|---|---|
| Toy base (trigger, bloom, gating, re-arm) | `Assets/_Scripts/Controller/Toys/Toy.cs` |
| Coordinated toy (reports activation to its set) | `Assets/_Scripts/Controller/Toys/SwapToy.cs` |
| Shared "set + flip" coordinator (generic) | `Assets/_Scripts/Controller/Toys/SwapToySetCoordinator.cs` |
| Shared runtime refs handed to each toy | `Assets/_Scripts/Controller/Toys/ToyContext.cs` (`ToyContext` + `ToyPlacement`) |
| Procedural body/label/collider builder | `Assets/_Scripts/Controller/Toys/ToyFactory.cs` |
| Mini vessel model (mesh-extract from prefab) | `Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs` |
| Vessel Changer set | `Assets/_Scripts/Controller/Toys/VesselChangerToySet.cs` |
| Domain Changer set | `Assets/_Scripts/Controller/Toys/DomainChangerToySet.cs` |
| Painting station (one per painting) | `Assets/_Scripts/Controller/Toys/PaintingToy.cs` |
| Multi-stroke fly-by-numbers runner | `Assets/_Scripts/Controller/Toys/PaintingRunner.cs` |
| Painting data (strokes + domains) | `Assets/_Scripts/ScriptableObjects/Toys/PaintingDefinitionSO.cs` |
| Preset generators (Star…Peacock, 16) | `Assets/_Scripts/Controller/Toys/PaintingPresetLibrary.cs` |
| Sophisticated-stroke library (curves + curl field) | `Assets/_Scripts/Controller/Toys/PaintingStrokeToolkit.cs` |
| Painting progress persistence | `Assets/_Scripts/Controller/Toys/PaintingProgressStore.cs` |
| Drawing state (per-prism pose/domain) | `Assets/_Scripts/Controller/Toys/PaintingPrismStore.cs` |
| Web share export (inline-WebGL viewer) | `Assets/_Scripts/Controller/Toys/PaintingShareExporter.cs` |
| Idle spin for toy bodies | `Assets/_Scripts/Controller/Toys/ToyIdleSpin.cs` |
| Conveyor ("Wanderway") toy | `Assets/_Scripts/Controller/Toys/ConveyorToy.cs` |
| Conveyor belt runner | `Assets/_Scripts/Controller/Toys/MicrosceneConveyor.cs` |
| One conveyor scene (lay/transport/re-arrange) | `Assets/_Scripts/Controller/Toys/Microscene.cs` |
| Microscene recipe generators (pure) | `Assets/_Scripts/Controller/Toys/MicroscenePatterns.cs` |
| Placement + lifecycle | `Assets/_Scripts/Controller/Toys/ToyboxController.cs` |
| Per-toy config (abstract) | `Assets/_Scripts/ScriptableObjects/Toys/ToyDefinitionSO.cs` |
| Vessel/Domain/Painting configs | `Assets/_Scripts/ScriptableObjects/Toys/*ToyDefinitionSO.cs` |
| Toybox registry + unlock state | `Assets/_Scripts/ScriptableObjects/Toys/ToyboxSO.cs` |
| One-click editor setup | `Assets/_Scripts/Editor/ToyboxSetupTool.cs` |

## The "swap set" pattern (vessel + domain)

Both the vessel and domain changers are **sets** of toys managed by a shared generic
coordinator, `SwapToySetCoordinator<T>`. The set always shows *the options you are not
currently on* (`universe \ {current}`), each toy visually *being* the option it will switch
you to. Flying through a toy applies the change; the coordinator then **flips the used toy to
the option you just left**, so the set continuously mirrors "everything except where you are
now". Current state is polled each frame, so external changes (a panel, a menu reset) reconcile
the same way. `SwapToy` is the per-option toy; it just reports activation and lets the
coordinator own the option→visual mapping and the flip.

### Vessel Changer (`VesselChangerToySet`)
A **collection** of toys, each a **mini 3D model** of a ship you can switch into (every vessel
in the collection except the one you're flying). The model is built by `VesselModelBuilder`,
which reads mesh data straight off the ship **prefab asset** (never instantiates it, so no
NetworkObject/VesselStatus/controllers ever run). Flying through one swaps you into that ship
via `MenuServerPlayerVesselInitializer.RequestSwap` (the existing networked pipeline), and the
toy flips to a mini model of the ship you just left.

**Mini-model rendering.** `VesselModelBuilder` extracts only the **hull**: it skips builtin-
primitive meshes (the skimmer sphere, scaled 15–60× — it otherwise dominated `NormalizeToRadius`
and crushed the hull to an invisible speck; this is why only Rhino, the one ship whose skimmer
has no builtin sphere, used to render), anything named skimmer/trail/jet/forcefield/crackle/pip/
vfx, and inactive/disabled renderers (read via `activeSelf` up the chain — `activeInHierarchy` is
always false on a prefab asset). Each hull mesh is painted with one **opaque, self-lit preview
material** (`TryBuild(prefab, radius, previewColor, out model)`), because the ship's real hull
material is a transparent, runtime-theme-driven shader that renders dim/invisible at rest. The
`previewColor` is the local player's **domain colour**, so the mini ships preview "you, a
different hull". Vessels whose body isn't statically extractable fall back to the labelled
tinted sphere.

**Recolour on domain change.** The mini ships are tinted to your domain, but `ConfigureVisual`
only runs on create/flip. So the set watches the local player's domain each frame
(`SwapToySetCoordinator.OnTick` → `VesselChangerToySet`) and, on a change, re-tints **every** mini
ship + label in place (`ForEachSlot` → `RecolorSlot`, no rebuild → instant, pop-free) so they all
follow the domain changer, not just the one slot that flips on the next swap.

**Swap continuity.** A swap is seamless — the new vessel inherits the old one's:
- **Domain / colour** — the swap re-syncs `Player.Domain` from the authoritative `NetDomain`
  before it repaints (`ClientPlayerVesselInitializer.ReInitializePair`), so the new hull keeps the
  domain you chose instead of snapping back to the Jade menu default (which desynced the domain
  changer). Domain lives per-player in `NetDomain`, so it persists across every ship change.
- **Position + orientation** — via `newVessel.SetPose(snapshot)` (also seeds `accumulatedRotation`
  so the rotation doesn't snap back).
- **Speed** — `SwapVesselAsync` captures the outgoing `VesselStatus.Speed` before despawn and
  seeds it onto the new vessel (`IVessel.SetInitialSpeed` → `VesselTransformer.SetInitialSpeed`,
  routed through a ClientRpc like `SetPose` so a party client's own vessel inherits it too),
  avoiding the post-`ResetForPlay` dead stop.

**Lost-control fix:** the swap pipeline drops the new vessel into autopilot with input paused
(that's why the old toy left you unable to steer). `VesselChangerToySet.RestoreControlAfterSwap`
waits for `IsSwapping` to clear, then re-hands freestyle control — mirroring
`MenuVesselSelectionPanelController.RestoreFreestyleAfterSwapAsync`.

**HUD after swap.** `VesselController.Initialize` creates every vessel's HUD **hidden**, and the
only menu code that shows the local HUD fires on *entering* freestyle — which a swap doesn't do.
So `ReInitializePair` re-raises `GameDataSO.OnPlayerPairInitialized` (as the initial-spawn path
does) and `MenuMiniGameHUD` re-shows the local HUD on that event while in freestyle (gated on
freestyle + local player). Covers both the toy swap and the vessel-selection panel swap.

Collection defaults to a curated set (Manta, Dolphin, Rhino, Squirrel, Serpent, Sparrow) so
the ring isn't crowded with all 11; override per-asset via `vesselCollection`.

### Domain Changer (`DomainChangerToySet`)
Two toys (in a 3-domain session), each **tinted the domain it will switch you to**
(`ThemeManagerData.GetDomainUIColor`) and labelled with the domain name — always the two
colours you are *not*. Flying through one requests that domain via the server-authoritative
`Player.RequestSetDomain_ServerRpc` (**never** a client-local write — CLAUDE.md), and the toy
flips to the colour you just left.

### Painting / Connect the Dots (`PaintingToy` + `PaintingRunner`)

The painting toy (player-facing name **"Connect the Dots"**, formerly "Fly by Numbers") is a
**gallery**: `PaintingToyDefinitionSO` spawns one `PaintingToy` station per `PaintingDefinitionSO`,
fanned around its ring slot, each labelled with the painting's name and live progress. A painting
is **multi-stroke and multi-domain** — a list of `PaintingStroke`s (name, domain, ordered 3D
points) flown in author order — and it stands as a fixed, upright **monument-in-progress** anchored
just outside the toy ring (front facing the ring), not a billboard that follows the vessel. The
built-in gallery (`PaintingPresetLibrary`) is a 16-painting ladder: a four-station on-ramp, then a
dozen **grandiose non-planar constructions** that dwarf the Taj (every one is >20·W of flight, most
>100 strokes — the Taj is ~55 / 15·W):

| # | Painting | Size | Strokes | What it is |
|---|---|---|---|---|
| 1 | Star | 840 | 1 | the basic trace, big enough to feel real (2× — the warm-up should already feel grand) |
| 2 | Rainbow | 700 | 3 | the domain gates, one band per colour |
| 3 | Saturn | 800 | 3 | genuinely 3D flying (tilted rings) |
| 4 | **Taj Mahal** | 1100 | ~55 | plinth, chamfered body, iwan+niches, onion-dome rib cage, 4 chhatris, 4 minarets, pool + charbagh |
| 5 | Torus Knot | 1000 | ~19 | the exact (3,2) trefoil as a machine-clean TUBE: 6 rotation-minimizing longitudes (one barber-pole twist), 12 rings, spine |
| 6 | Buckyball | 1000 | ~62 | exact C60: **12 pentagons + 20 hexagons** (planar faces) + the 30 real 6:6 double bonds as inset dashes |
| 7 | Double Helix | 900 | ~88 | true B-DNA: pitch/diameter 1.7, 10 bp/turn, 144° grooves, ribboned backbones, purine+pyrimidine pairs, phosphate ticks |
| 8 | Nautilus | 900 | ~67 | the real shell model: embracing log-spiral whorls, 58 growth-line ribs, tiger striping, the open aperture |
| 9 | Lotus | 900 | ~76 | the FULL lotus: wide-open outer leaves (Jade) descending through five whorls (10+9+8+6+5) into a closed pure-petal Ruby corolla and Gold bud heart |
| 10 | Rose | 900 | ~67 | the ENCHANTED rose: a long stem owning two-thirds of the height, two leaflets, sepals curling under a compact wrapped bloom with a furled Gold heart |
| 11 | Spiral Galaxy | 1200 | ~187 | a TWO-arm grand design at 17° pitch, inclined 22°: dust lanes, old-gold bulge, star streaks flowing along the arms |
| 12 | Phoenix | 1400 | 260 | **baked from a real sculpture**: the *Striding Eagle* museum scan (threedscans / Saint Louis Art Museum, no restrictions) — 79.7·W of engraving contours + flame-chained feather feature lines |
| 13 | Almighty Mountain | 1500 | 111 | **baked from real terrain**: the Matterhorn's actual DEM (AWS Terrain Tiles) — elevation contours (Ruby rock / Jade snowline) + Gold ridge polylines |
| 14 | Starry Night | 1300 | 173 | **baked from the real painting** (v2 retrace): 11 star/moon ring clusters, the double-swirl as two long coherent Jade spirals, 6 Ruby cypress flames, streamlines bent onto an immersive curved canvas with luminance relief |
| 15 | Lion's Head | 1800 | 124 | **baked from a real sculpture**: the CC0 Temperance Union Lion scan (1896), Squirrel-scaled — engraving contours + 62 mane-curl feature lines (micro-curls under 28u turn radius filtered out) |
| 16 | Peacock | 1300 | 236 | **baked from a real scan**: YahooJAPAN's peafowl photogrammetry (CC-BY 4.0 — attribution ships in the asset description) — the fanned train, scalloped eye-feather rim, body and legs |

**Gallery stations are miniatures in a wall, not balls in a line.** Each station's body IS its
painting in miniature (`MiniaturePaintingBuilder`: the ~24 longest strokes, decimated, domain-
tinted, on a slow turntable) — a sphere only as fallback for stroke-less paintings. The sixteen
stations arrange as a roughly-square matrix cluster at the toybox slot (columns along the ring
tangent, rows climbing the off-plane vertical), and the monuments anchor behind their column in
vertical tiers — a wall of masterpieces. Every toy label (stations, gates, all toys) wears
`BillboardLabel`, facing the camera each frame so text reads from any approach.

Rows 5–11 are built by composition from **`PaintingStrokeToolkit`** (below); rows 12–16 are
**baked from real references** by the offline **painting pipeline** (`Tools/PaintingPipeline/` —
mesh→engraving-stroke converter + painting-flow tracer + asset baker; licences audited in
`Tools/PaintingPipeline/REFERENCE_MODELS.md`). Baked paintings live as authored `strokes` on the
`PaintingDefinitionSO` asset — the SO's highest-priority source — so they need zero runtime code
and their `preset` remains as fallback (fallback catalog descriptions deliberately do NOT claim
reference provenance — the CC-BY/DEM attributions belong to the baked strokes only). Monument
anchors use **width-aware pitches**: columns step along the ring tangent by the widest painting's
bounds (+`paintingClearance`), rows climb by the tallest painting's height — (wᵢ+wⱼ)/2 ≤ max(w)
for any pair, so no two monuments can interpenetrate by construction (studio zones may still
overlap; `BenchOtherRunners` arbitrates the brush).

How a run plays:

- **Ghost blueprint.** Every stroke renders as a `LineRenderer` ghost tinted its domain colour —
  pending faint, the current stroke bright, completed strokes dim-solid. The whole blueprint
  blooms in (continuity law) and fades away after completion, leaving only the painted prisms.
- **Start gates.** Each stroke opens with a ring gate at its first point (a `SwapToy`, so it
  inherits bloom/local-user/freestyle gating/re-arm), labelled `n/total StrokeName` and tinted
  the stroke's domain. Flying through it **requests that domain via
  `Player.RequestSetDomain_ServerRpc`** (never a client write; silently skipped if the session's
  `RequestedDomainCount` excludes it) so the trail recolours, then the stroke begins. This is
  the domain-changer composed into the painting — colour changes are part of the flying.
- **Pen-up between strokes.** Inside the painting's "studio zone" (bounding sphere + margin),
  the trail spawner is paused between strokes via `VesselPrismController.SetSpawnerPaused`
  (pen-up), so transit flight never scribbles across the artwork; painting a stroke, leaving
  the zone, exiting freestyle, benching, or destroying the runner ALWAYS restores it. Pausing
  the spawner is the sanctioned mass-law lever ("not creating mass is allowed; aging it out is
  not") — the painted trail itself is conserved mass, no caps/TTLs.
- **Checkpoint riding (not vertex-chasing).** A stroke is ridden through SPARSE checkpoints
  (`PaintingStrokeToolkit.RideCheckpoints`): spaced by arc (≥ max(90u, 8.5% of the painting's
  bounds diagonal)), never parked on tight curvature (>28° local turn — a hairpin apex is a
  punishing target at speed; on an all-tight stretch the flattest vertex is used so progress
  can't stall), with the stroke start (gate) and end (jack) always included. **Closed loops
  always keep a mid checkpoint near the half-arc** — otherwise a ring's end milestone sits at
  its own gate and the stroke would complete unridden the moment the gate fires.
- **Rings, not lines, not cones.** While AWAITING its gate the next stroke's ghost shows faintly
  (something to aim at); once you are RIDING it the line eases away entirely (continuity law —
  `_lineFade`, and it eases back in as the dim "done" memory line on completion) — the ride is
  the milestone RINGS and your own painted trail. Each milestone is a ring gate faced along the
  local flight tangent whose **SphereCollider trigger is scaled to the ring radius** (flying
  through the ring IS the hit test; a slightly tighter distance check backstops fast physics
  misses, and all effects run on the Update tick, never in the physics callback — the trigger
  resolves the local vessel via the shared `Toy.TryGetLocalVessel`, which also guards the
  null-`Player` window during a mid-stroke vessel swap). The ride ring **never outlives
  engagement**: leaving the studio zone or exiting freestyle folds it away (and it re-blooms on
  re-engage) so the lava-lamp autopilot can never drift through it and latch a checkpoint nobody
  rode. The trail-on **cone** appears only on the stroke's START gate; the final milestone ring
  carries the trail-off **jack** in its centre. Rings fold away as they're swept and the next
  blooms in (continuity law). Wayfinding uses the game's **standard `ObjectiveIndicator`** (the
  edge-of-screen arrow every mode shares, not a bespoke guide line): the runner implements
  `IObjectiveProvider` via a persistent objective anchor (the gate while awaiting it, the current
  ride ring while painting — the anchor outlives the ring folding on disengage), and ONE shared
  arrow is lazily created under the menu HUD container, routed through `PaintingObjectiveRelay`
  to whichever runner holds the brush. The arrow hides itself while the target is on screen, so
  the world rings stay the primary guidance. (The old guide line's perfect-ride glow retired with
  it; re-express that juice in-world — e.g. ring emission — in the tuning pass.)
- **Progress, pause, resume.** Progress is stroke-granular. Re-flying the station benches /
  resumes the run ("put the brush down"); progress also persists across sessions
  (`PaintingProgressStore`, the FavoriteSystem `DataAccessor` JSON pattern — completed strokes
  re-render as dim ghosts after a restart since prisms live only as long as the scene). After
  the completion celebration, flying the station again clears the canvas for a repaint.
- **Toy-faithful.** No score, no timer, no fail state — only progress. Solo-painting only: the
  runner tracks the *local* player; party members see the painted prisms replicate but not
  your gates/ghosts (same local-station model as every toy).

(The full `ShapeDrawingManager` experience — preview cinematic, scoring, reveal — remains
available for gameplay scenes; any existing `ShapeDefinition` can also become a painting via
`PaintingDefinitionSO.sourceShape`, which splits pen-up gaps into strokes.)

#### Shape language — one vocabulary of interactables

Toys teach each other by recycling shapes (mindshare recycling): every interactable that does
the same *kind* of thing wears the same form, in the domain's **prism material** (the exact
shader the painted trail wears — `ToyFactory.DomainPrismMaterial` →
`ThemeManagerDataContainerSO.GetTeamBlockMaterial`).

| Shape | Meaning | Where |
|---|---|---|
| **Cone** (apex = "this way next") | *turns / keeps your trail ON* | stroke-gate hubs, every intermediate stroke point (apex points at the stroke's next point), and the **Domain Changer** bodies (apex points the way you fly through) |
| **Jack** (three rods through a centre) | *turns your trail OFF* | each stroke's final point (reaching it ends the stroke and pens up) |
| **Ring** (fly-through portal) | *crossing commits a choice* | stroke start gates, the SHARE/REPAINT completion gates |

The domain changer and the painting gates deliberately share the cone so meeting either one
first sets up expectations for the other. Builders live in `ToyFactory` (`AddConeBody`,
`AddJackBody`, `AddRingBody`).

#### Authoring rule — order strokes by decreasing radius of curvature

Strokes are flown in author order, so **sequence them from the broadest curvature to the
tightest**: long straight/broad strokes first (pool outlines, plinth rectangles), fine detail
last (balcony rings, crescents). The painting then doubles as its own difficulty ramp — the
player warms up on sweeping lines and earns the precision work — and the adaptive per-stroke
reach (which tightens on short segments) ramps with them. The Taj Mahal preset is the
reference: pools → plinth → body → arches → dome → chhatris → minaret balconies. Batch
domains at meaningful architectural boundaries within that ordering (see
`PaintingPresetLibrary`).

#### The stroke toolkit — where sophisticated strokes come from

The grandiose constructions (rows 5–16 above) are not hand-authored point lists — they are
composed from **`PaintingStrokeToolkit`** (`Assets/_Scripts/Controller/Toys/`), a pure, deterministic,
unit-tested geometry library. It answers "where do we pull more sophisticated strokes from?" with
*math*, not assets:

- **Deterministic PRNG** (`Rng`) — a seedable xorshift, never `UnityEngine.Random`, so every painting
  regenerates identically (and is testable). Same painting id → same monument every time.
- **Parametric curve families** — `CatmullRom`, `Helix`, `LogSpiral3D`/`LogSpiralBand` (the golden
  spiral behind Nautilus + galaxy arms), `TorusKnot(p,q)`, `Rose3D`, `Lissajous3D`, `FibonacciSphere`,
  `Phyllotaxis` (the 137.5° golden-angle lattice behind Lotus/Rose/Peacock), plus the
  `TruncatedIcosahedron` / `SoccerBallFaces` graph (exact 60-vertex/90-edge/32-face buckyball).
  Botanical/terrain helpers: `PetalLoop`, `DomeLift`, `MidpointRidge` (fractal mountains), `ReflectY`
  (lake reflections), `FirTree`, `FeatherStroke`, `FrameStrand` (braided rope), `RadialCurlStroke`
  (mane/flame strands).
- **The impressionist field** — `CurlNoise` is a divergence-free 3D flow field (curl of a value-noise
  vector potential; `∇·(∇×Ψ)=0`, so streamlines fill space without converging to a point).
  `ImpressionistStrokes` integrates short strokes along it whose radii of curvature stochastically
  fill a region in every direction. **Scope note (quality direction):** random curl fill reads as
  scribble on *objects*, so the reference-grade rebuilds (rows 5–11) use **structured**
  surface-following strokes instead — growth lines, veins, bonds, orbital streaks. The curl field
  remains the right tool for genuinely turbulent subjects (the Van Gogh sky, flame, mist) and for
  the mane/feather sprays of the representational five, pending their real-model rebuild.
- **Structure kit (reference-grade rebuilds)** — `MinSegFilter` (dense parametric sampling never
  emits a degenerate segment), `TransportFrames` (rotation-minimizing frames — tube longitudes that
  never flip on curves like torus knots), `TubeLongitudes` / `TubeRing` (engineered tube rendering),
  `SoccerBallDoubleBonds` (C60's 30 hexagon–hexagon 6:6 bonds).

**Invariants every generator upholds** (locked by `Generate` + tests): base plane at y=0
(`RebaseToGround` runs after every grandiose preset), front toward +Z, only Jade/Ruby/Gold, flyable
segments (no degenerate or unflyable jumps), genuine non-planarity, and >20·W of flight. Multi-domain
impressionist fills are **batched by domain** so the trail recolours ≤2× per fill rather than at every
scattered stroke. Adding a construction = adding a `PaintingPreset` enum value + a generator that
composes the toolkit; nothing else changes.

**Collider budget / perf.** A painting is drawn by the *vessel's own trail* (conserved mass, no
caps) — the geometry cost is one `LineRenderer` ghost + one start gate per stroke, created at
`PaintingRunner.Begin`. The largest pieces (Phoenix 260, Peacock 236) therefore stand up a few hundred
lightweight LineRenderers; that is the deliberate "hours of flying" ceiling and is tracked in
`BACKLOG.md` for an in-editor perf pass.

#### Drawing state — the painting survives everything

Progress is not just a stroke counter: while a stroke is painted, every prism laid inside the
studio zone is recorded (painting-local **position, orientation, size, domain**, prism type)
via `VesselPrismController.OnBlockSpawned` and committed per completed stroke to
`PaintingPrismStore` (one `DataAccessor` JSON file per painting). That makes the run robust to
everything between strokes: **swap vessels** (capture re-resolves the controller), **switch to
another painting** (runs bench each other), or **leave for a whole game mode / quit** — on
return, the completed strokes' prisms are *regrown* through the normal `PrismFactory` channel
(pooled spawn, grow-in animation, streamed over frames so a monument reads as growing back,
never popping). Restored prisms are ordinary conserved mass. Abandoned mid-stroke prisms are
deliberately not persisted — an unfinished stroke re-flies fresh.

#### Sharing — the masterpiece leaves the game

Finishing a painting offers two fly-through choice gates at the station: **REPAINT** (clears
progress + drawing state, fresh canvas) and **SHARE** — `PaintingShareExporter` writes a
single self-contained HTML file (inline WebGL, zero external dependencies) that reconstructs
the painting from its saved prisms with drag/pinch orbit, zoom, and a gentle auto-spin, then
hands it to the platform share sheet via the NativeShare plugin. Paintings finished before
drawing-state capture existed fall back to boxes laid along the stroke polylines, so share
always works.

### Wanderway / Microscene Conveyor (`ConveyorToy` + `MicrosceneConveyor` + `Microscene`)

Fly through → the belt switches **ON** (the toy flips bright + relabels "flowing — fly through
to stop"; another pass switches it off) and a field of **microscenes** blooms in ahead of your
flight path, scene after scene — open-world exploring crossed with an infinite runner. **28
recipes** built from a shared geometry vocabulary (`PrismGeometry`): gate runs, helix weaves,
tunnels, slaloms, starbursts, orchards, meadows, menageries, polygon gates, serpent ribbons,
colonnades, orbitals, canyons, lattices, comet tails, spiral ramps, archways, vortices (converging
lines with an open convergence + an inviting crystal), slot corridors (parallel plates with gaps to
roll through), cube fields, torus gates, pillar halls, turbines, asteroid fields, and living
plains / groves / aviaries / preserves — each re-rolling its own radii/counts/twists/bends on every
arrival, so the same recipe never lands the same way twice. The belt **follows you anywhere at any
speed**: effective spacing = `max(sceneSpacing, speed × minSceneIntervalSeconds)` and lookahead =
`aheadTargetScenes × spacing`, so there is always a field of ~7 structures ahead.

**Geometry vs. theming (why it stays fresh, not chaotic).** A recipe produces pure *shape* only;
`MicroscenePatterns.ApplyTheming` then themes each scene from a config-authored `MicroscenePalette`
(`ConveyorToyDefinitionSO`): a **per-scene domain scheme** (mono / banded-by-structure / accented /
neutral-veined-with-Blue — weighted so most scenes read one coherent colour, never per-prism
confetti; domains read live each draw so the Domain Changer toy takes effect), a sparse **prism-kind
scheme** (mostly plain, with occasional **danger** prisms — the Squirrel danger-skim risk/reward —
and rarer **shielded** / **supershielded** accents, capped for the collider budget), a per-scene
**scale mood** (grand vs. delicate), and a **crystal mix** (mostly elemental skims, occasional
**omni** jackpots — body-collected fuel + speed buff). "Infinitely fresh" is the cross-product of
recipe × domain-scheme × kind-scheme × scale-mood × per-arrival geometry roll; coherence comes from
theming per *scene*, not per prism. Prism lay-down goes through the shared `PrismTrailBuilder` (the
one canonical Instantiate→…→Initialize primitive, also used by the Spawnable environment system).
See `Docs/EnvironmentSpawning/UNIFICATION_ASSESSMENT.md`.

**Placement is a connected ribbon that can break and re-lay.** Every scene sits *on* the flight
line — never scattered laterally (no orthogonal "sphere in front of you"). Each tick the belt scans
a **forward cone** (half-angle `turnBreakDegrees`, default 55°) around the live course and does one
of two things:

- **Near-fill** — if nothing lies just ahead on the current heading (start-up, or a turn just
  ejected the near scenes out of the cone), it drops a scene directly ahead at `firstSceneDistance`,
  so a structure appears in front of you right after any turn.
- **Extend** — otherwise it chains the next scene off the *actual frontmost scene* along the current
  course (`tip + course × spacing`). Chaining off **real mass** (not a free-floating "head") keeps the
  ribbon connected and lets it **bend** with gentle/moderate turns without ever drifting into a
  parallel path far to the side — the drift that made the old head-chained belt lay a distant
  shadow ribbon on any deviation.

A **sharp turn** drops the whole old ribbon out of the cone: its measured reach collapses, near-fill
re-lays straight down the **new** heading from `firstSceneDistance` outward, and the now-lateral
leftovers become the farthest-first recycle candidates that rebuild ahead — so the ribbon *breaks and
restarts in front of you* on a hard turn while staying a continuous ribbon through gentler ones.
Passed scenes and a turn's leftovers clear (suction) as new ones arrive — spawn frequency IS the
clear frequency, because the pool is finite and **closed**: a reclaimable scene (off the flight cone,
or dropped far behind) is *suctioned* to a point, relocated onto the ribbon ahead, re-posed into a
fresh recipe with new domain colour, and *bloomed* back out. Scenes still in the cone ahead (what
you're flying toward) are never reclaimed, and a scene mid-recycle **claims its destination slot
immediately** (`Microscene.PendingAnchor`) so a rebuild never piles several arrivals onto one point
while the blooms are in flight. No score, no end condition; every belt advance is driven by the
player's own motion (no timers). Exiting freestyle makes the belt dormant; toggling it off stops the
flow — either way its scenes stay in the world.

**Ecosystem invariants (this toy is ecology-adjacent — all hold by construction):**

- *Continuity of existence* — prisms grow in via their own `PrismScaleAnimator`; crystals
  `FadeIn`; recycling is suction-out → bloom-in (both sanctioned transitions). Nothing pops.
- *Mass conservation / no passive removal* — the belt never destroys a prism; recycling
  **transports the same prism instances** (movers contract: `Prism.NotifyPositionChanged`).
  The only sink is fauna grazing belt prisms (an active force); grazed slots are re-minted
  through the sanctioned pool-reuse lifecycle (`Prism.Initialize`), which is creation. Player
  trails through scenes are never touched. This is *not* the rejected "recycle the oldest prism"
  budget cheat: nothing is removed, the belt's total stock is fixed, and only toy-owned
  exhibit content rides the belt.
- *No imposed death* — lifeforms are **released, not owned**: meadow/menagerie scenes spawn
  flora/fauna into the host `Cell` via the canonical sequences (`Initialize(cell)`,
  `AssignLineage`, `RegisterSpawnedObject`) and never track, move, or despawn them. They live
  and die by the food web only.
- *No domain asymmetry* — fauna spawn in `Cell.ControllingDomain`; flora in a random playable
  domain (both via the canonical `CellLifeSpawnerBase` spawn path the cell's own spawner uses).
  Multi-domain colouring applies only to *prisms* (neutral mass), distributed per-scene by a
  coherent domain scheme (incl. optional neutral-Blue veins) — not a per-domain spawn bias.
- *Volume is the spine / collider budget* — belt mass is bounded at
  `poolSize × prismBudgetPerScene` prisms (default 10 × 42 = 420 BoxColliders + ≤3 crystal
  triggers per scene + 1 toy trigger, well under the ~1,500/cell target); distant scenes are
  collider-LOD-culled by `PrismColliderLodManager` automatically. **Shielded / supershielded**
  prisms swap their BoxCollider for an always-on convex MeshCollider that LOD can't reclaim, so the
  palette caps them (`MaxShielded = 3`, `MaxSuperShielded = 1` per scene, low scheme weights) —
  worst case ≈ 40 MeshColliders across a full pool, realistic steady state a handful. Danger prisms
  keep the cheap cullable BoxCollider. The belt roams freely — mass
  laid inside a cell registers with that cell's volume/grids as usual; mass laid in open space
  is ordinary registered prism mass with no cell binding (same as any open-space track). The
  conveyor adds **zero physics queries** — placement is pure arithmetic.
- *Cell owns the environment* — no parallel spawner/boundary/population: lifeforms come from the
  cell's own `SpawnProfileSO` configs, respect `FloraPlantingEnabled` + `MaxLivePopulation` +
  the prey floor, and are released only when the scene sits INSIDE a live cell
  (`Cell.FindCellContaining`) — open-space scenes are prisms + crystals only.

Crystals are the four elemental pickups (`ElementalCrystalSetSO`, Resources-loaded), made
skimmable at runtime via the internal setters added to `ImpactCollider` /
`ElementalCrystalImpactor` (the runtime mirror of the components lifeform prefabs author in the
inspector). Content is local-only, like every toy (party guests don't see your belt).

## Freestyle input ownership (reaching / leaving the toybox)

Toys only activate in freestyle, so who owns the gamepad matters. In the menu two readers poll
the one pad: the **vessel** (`InputController` → `GamepadInputStrategy`, gated by
`InputController.SetPause`) and the **appshell** (`ScreenSwitcher` screen-nav, already gated on
`_isInFreestyle`, plus the EventSystem's UI `Navigate`/`Submit`). The EventSystem was ungated, so
in freestyle the steering stick also drove the UI selection ring and the fire button also
submitted the (still touch-interactable) vessel HUD.

- **Enter/leave** freestyle via `MenuCrystalClickHandler.ToggleTransition` (tap the crystal to
  enter; the on-screen Volume/Pause button *or* — new — the **gamepad Start button** to leave).
  `MenuMiniGameHUD.Update` polls `Gamepad.current.startButton` while in freestyle and calls
  `ToggleTransition`, so pad players hand control back to the appshell without reaching for a
  touch button.
- **Mutually-exclusive ownership:** in menu state the vessel input is paused (autopilot); in
  freestyle `ScreenSwitcher.HandleEnterFreestyle` sets `EventSystem.sendNavigationEvents = false`
  (restored on exit), so the pad flies the ship and no longer double-drives the UI. Pointer/touch
  input is unaffected, so touch HUD buttons still work.

## Placement

`ToyboxController` places one toy per **unlocked** definition once the menu vessel is ready
(`GameDataSO.OnClientReady`). Each toy sits near the **cell membrane**, spaced far apart:

- If a `Cell` is active in the scene (`Cell.FindNearestActiveCell`), toys ring its membrane
  at `center = cell.transform.position`, `radius = cell.MembraneRadius * membraneFraction`
  (default 0.82 — just inside the boundary where the vessel flies).
- **Menu_Main has no Cell/membrane today**, so the controller falls back to a configurable
  `fallbackCenter` + `fallbackRadius`. The moment a Cell is added to the menu, placement
  snaps to the real membrane with no code change.

Definitions with `placementAngleDegrees < 0` (the default) auto-distribute evenly around the
ring so they stay far apart; set a specific angle per toy to pin it.

## Toybox & unlock state

`ToyboxSO` is the registry: a list of `ToyDefinitionSO` + an id→bool unlock map.

- **Unlock conditions are deferred.** Every toy ships unlocked
  (`ToyDefinitionSO.UnlockedByDefault = true`). The unlock *state* lives in `ToyboxSO` with a
  clean `SetToyUnlocked(id, unlocked)` hook and an `OnToyboxChanged` event a future
  persistence layer can drive (load on sign-in → `SetToyUnlocked`; subscribe to
  `OnToyboxChanged` → save), mirroring the `FavoriteSystem` JSON pattern. No persistence is
  implemented yet.
- **Zero-config fallback.** If no `ToyboxSO` is assigned and none exists at
  `Resources/Toybox`, the controller synthesises a default toybox of the three built-in toys
  at runtime (the `ElementalBarsView` "zero-wire by default" precedent). So the system works
  the moment `ToyboxController` is in the scene, before any assets are authored.

## Adding a new toy

1. Add a `Toy` subclass with the behaviour (`OnActivated(IVesselStatus localVessel)`), or a
   `SwapToySetCoordinator<T>` subclass for a flip-set toy.
2. Add a `ToyDefinitionSO` subclass whose `Spawn(...)` builds it via `ToyFactory`.
3. Add the new definition asset to the `ToyboxSO` (or to `BuildDefaultToybox` for a built-in).

No central switch — definitions are polymorphic factories, so the framework never changes.

## Setup (one step in Unity)

Run **Tools → Cosmic Shore → Setup Freestyle Toybox**. It:

1. authors the four toy definition assets under `Assets/_SO_Assets/Toys/` (the conveyor's
   prism prefab + crystal effect are auto-wired: `SpawnablePrism.prefab` +
   `SkimmerAdjustElementLevelByCrystalEffect.asset`),
2. creates `Assets/Resources/Toybox.asset` and registers them, and
3. adds a `ToyboxController` to Menu_Main (on the `MenuCrystalClickHandler` object) pointing
   at the toybox.

Idempotent — safe to re-run. (Or simply drop a `ToyboxController` on any Menu_Main object and
rely on the runtime default toybox.)

## Networking notes

- Each client runs its own `ToyboxController` and spawns its own local toy GameObjects
  (deterministic placement → they overlap visually across clients). Toys are **local
  interaction stations**, not networked objects; only the *effects* (vessel swap, domain
  change) go over the network, through the existing server-authoritative paths. This matches
  the "local-only freestyle toggle, network-replicated vessel behaviour" model in CLAUDE.md.
- `IsLocalUser` ensures only your own vessel trips your toys; the freestyle gate ensures the
  autopilot lava-lamp vessel never does.

## Status & follow-up

The framework + **four toys** are in (Vessel Changer, Domain Changer, Painting, and the Wanderway
microscene conveyor), plus the vessel-changer second-pass fixes above: mini-model hull rendering,
exit-gated re-arm + slow flip re-grow, swap continuity (domain / pose / speed), recolour-on-domain,
HUD-after-swap, and gamepad-Start / input-ownership. The conveyor has been through two adversarial
review passes (compile, logic, ecology invariants, game-feel, assets, docs). All are
compile-reviewed against the real codebase but **not yet play-verified in an editor** (no Unity in
the authoring environment) — an in-editor pass is the last step before/after merge. Remaining polish
(per-toy tuning, skinned-mesh `BakeMesh` fidelity, painting pen-up, placement anchor, conveyor
recipe/pacing tuning + audio, unlock persistence, tests) is tracked in **`BACKLOG.md`**, grouped so
each area can be its own branch.

### Files touched this pass (for review)

| Area | Files |
|---|---|
| Re-arm / escape | `Controller/Toys/Toy.cs`, `Controller/Toys/SwapToySetCoordinator.cs` |
| Mini-model rendering | `Controller/Toys/VesselModelBuilder.cs`, `Controller/Toys/VesselChangerToySet.cs` |
| Recolour on domain change | `Controller/Toys/SwapToySetCoordinator.cs` (`OnTick`/`ForEachSlot`), `Controller/Toys/VesselChangerToySet.cs` |
| Domain preserved on swap | `Controller/Multiplayer/ClientPlayerVesselInitializer.cs` (`ReInitializePair`) |
| Speed inherited on swap | `Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`, `Controller/Vessel/IVessel.cs`, `Controller/Vessel/VesselController.cs`, `Controller/Vessel/VesselTransformer.cs` |
| HUD re-show after swap | `Controller/Multiplayer/ClientPlayerVesselInitializer.cs`, `UI/MenuMiniGameHUD.cs` |
| Gamepad Start / input ownership | `UI/MenuMiniGameHUD.cs`, `UI/ScreenSwitcher.cs` |
