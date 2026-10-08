# Design Philosophy: Emergent Systems

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

## Design Philosophy: Favor Emergent Systems Over Bespoke Solutions

Cosmic Shore aims to be built on a small, carefully curated set of
**fundamentals** whose interactions produce a large number of desirable
emergent outcomes. When solving a problem, maintain active awareness of
these fundamentals and prefer solutions that work *through* them rather than
*around* them.

### The fundamentals (working list)

Use the canonical term, not a casual synonym. This list is the team's current
best understanding and will be refined over time — propose additions or
corrections through the process below rather than silently inventing new
ones.

- **Domain** — team/affiliation identity attached to mass, vessels, and
  structures. Sometimes referred to casually as "color"; the canonical term
  is *domain*.
- **Mass** — the produced/consumed quantity that drives scoring, fueling,
  and cell control. **Mass is conserved: it has no passive decay.** A prism
  (the concrete unit of mass), once created, is only ever removed by an
  *active* force — a vessel using an ability, or fauna eating it. There is no
  aging, lifespan, timed culler, or growth/decay oscillator anywhere in the
  mass pipeline. Population homeostasis is the job of the **food web** (fauna
  consume mass; fauna starve when prey is scarce), never of artificial decay.
  A large accumulation of prisms is therefore a *valid* state, not a bug to
  auto-correct: it persists until an active force consumes it, and when the
  fauna that would eat it can't reach prey, the correction surfaces as fauna
  starving — not as prisms vanishing. This holds in **every scene the
  simulation runs in** — including Menu_Main's lava-lamp/freestyle, where the
  autopilot vessel *is* the gameplay vessel. There is no "cosmetic" or
  "menu-only" exemption. See "Universality" and "Don't cheat emergence" below
  and `Docs/ECOSYSTEM.md`.
- **Cells** (with `CellType`) — the regions of play that are the unit of
  territorial control. Casual language sometimes calls these "biomes"; the
  canonical term is *cell*.
- **Elementals** — the single system that governs **all** buffing and
  debuffing across vessels and their environment. If a buff or debuff isn't
  expressed through elementals, that's a smell.
  **A vessel DEBUFF is a TRANSFER, not a decay** (`Docs/ELEMENTAL_ECONOMY.md`): it moves petals
  permanently out of the victim's base level and into one of three places — **stolen** by the
  attacker on a contact verb, **ejected** as free-for-all crystals on any ranged verb, or
  **burned** by a hostile danger prism, which is the economy's only sink. A *buff* is not a
  transfer (there is no victim to take it from) and therefore stays temporary — making one
  permanent would mint petals out of nothing and break the rule that **lifeform reproduction and
  spawning are the only SOURCE**. The unit is the **petal**: one integer level, one step of the
  HUD flower, one crystal at world scale 1. Conservation is enforced by the victim
  (`ResourceSystem.AccrueElementalLoss`) rather than by any call site — nothing partial ever
  leaves, and nothing can be taken that is not held above resting level 0, so the base band
  **[0, 10] is the pot** and the deficit band is reachable by transients only.
- **Prisms / Prismscapes** — the geometric primitive of player-generated
  structure. Trails are the 1-dimensional case of a prismscape; higher-
  dimensional prism constructions reuse this primitive rather than
  introducing parallel structure types. **The DIMENSION ladder is shipped**:
  `PrismscapeDimension` names it (Singleton 0 / Trail 1 / Surface 2 /
  Volume 3) and `PrismscapeTopology.DimensionOf` resolves a prism's
  prismscape from authored evidence (`Trail.Dimension`) first, else a
  neighbourhood census. A vessel that ATTACHES rides 1D through
  `TrailFollower` (a rail grind) and 2D through `BlockscapeFollower`
  (marble-madness rolling); **0D — an isolated prism — is deliberately not
  rideable**. In both dimensions the prismscape constrains POSITION only:
  attitude is always the pilot's. Prisms *are*
  conserved mass (see **Mass**): only active forces — vessel abilities and
  fauna consumption — remove a prism. Whether a prism is a lifeform's health-
  prism or vessel-spawned makes no difference to this rule.
- **Flora & Fauna** — populations that live on and respond to the
  fundamentals above (e.g. fauna attraction to prisms, flora growth on
  cells).
- **Vessels** — the player/AI actors whose class-specific abilities compose
  with the fundamentals above.
- **Toys** — interactive world-space stations the player's **Vessel** flies into,
  surfaced in the Menu_Main lava-lamp/freestyle "toybox". A toy has **no score and
  no end condition** — something to play with indefinitely (toys are to freestyle
  what party games are to the rest of Cosmic Shore). Added at the prompter's request;
  it earns its place by composing with the others rather than bypassing them: the
  vessel-changer cycles **Vessel**, the domain-changer cycles **Domain** (server-RPC,
  never a client write), the element-charger raises your vessel's **Elementals** (a crystal's own
  base-level raise, so past level 10 it drains like any overcharge), the lifeform bench releases **Flora & Fauna** into the **Cell**
  and — through its Vessels kingdom — an AI-piloted **Vessel** in your own **Domain**,
  the painting/"connect the dots" toy lays a conserved **Mass**
  prism pattern, **Wander** takes you out of the cell with an **Ark** or without one — without,
  the Wanderway conveyor streams **Prisms/Mass** (a fixed stock
  it *transports* — suction-out → bloom-in — never creates or destroys), **Crystals**
  (skimmable elemental pickups), and **Flora & Fauna** (released into the containing
  **Cell** as ordinary citizens) into an endless field ahead of the vessel; with, an Ark sails a
  corridor of cells at your side — and the
  **cell-selector** picks the **Cell** itself (a matrix of mini-cells over the Cell's
  *own* config rotation — the toy never authors a parallel list — routed through the
  one `Cell.RequestCellSwap` entry point; choosing the cell you are already in is the
  freestyle reset). Toys are placed relative to the **Cell** membrane (read, not
  duplicated). **A toy is activated by a SWITCH** (below): every toy root and every
  choice a toy unfolds into is drawn inside one continuous ring at the radius of its
  own trigger collider, **in the prism shader**, so "how do I use this?" is answered by
  the shape and "what will it do?" by the material. **A toy carries NO TEXT** — no name, no
  label, no progress readout: the ring and the icon do the lift in the world, and the Toy Box menu
  is where a player learns what a toy is called (`ToyFactory` no longer has a label builder, and
  `Tools/Build/toy_switch_ring_geometry.py --check` fails on any TextMeshPro type in a toy source).
  **One named exception**: `ToyChoiceLabel` puts SHARE / REPAINT over a finished painting's two
  completion gates — identical neutral rings that do opposite things (export vs erase), with no menu
  between the player and the choice — and it is its own file so the check exempts exactly it.
  Drawn by
  `Toy.Initialize` from that collider — not by each toy's builder — so a toy authored
  tomorrow wears one; one explicit opt-out (`Toy.ConfigureSwitchRing`): a smaller radius
  where a matrix's stations, or the domain changer's slots, would otherwise interpenetrate. A toy imposes
  no decay/timer/win-lose, so it stays inside *Mass is conserved* + *don't cheat
  emergence* — a cell swap removes mass only because a player flew into a station and
  asked for a new world, the same **active**, explicit event class as a scene load,
  never a clock. Unlock *conditions* are deferred; the toybox registry + per-toy
  unlock state live in `ToyboxSO`.
  **Every toy declares a CATEGORY, and the categories are the fundamentals it composes with** —
  `ToyCategory`: **Pilot** (changes YOU: vessel changer, domain changer, element charger), **World** (changes where
  you are: cell selector, Wanderway), **Creation** (leaves something behind that lives on without
  you: painting gallery, spawn matrix). `ToyDefinitionSO.Category` is **abstract and declared in
  code**, never a serialized field — a toy's category is a property of what it DOES, an authored
  field can disagree with the behaviour under it, and abstract means a new toy cannot be added
  without saying which fundamental it reaches for. A toy that fits none of the three is the signal
  to run the fundamentals-curation process above, not to widen the enum. The in-game encyclopedia's
  **Tools** kingdom groups its pages by exactly this (`Docs/CODEX.md` §3.5).
  **A TOY DECLARES ITS CHOICES ONCE, and the fly-into station and the menu Toy Box window are two
  INPUTS to that one declaration** — they may differ in how an option is DRAWN (a station shows the
  real hull at arena range; a window shows a small preview), never in WHICH options exist or WHAT
  pressing one does. `IToyShellSurface` always stopped the MENU inventing an action; what it did
  not stop was the TOY writing the list twice, once for its stations and once for the window, and
  those agreed only by coincidence: the Lifeform bench's window had silently lost its whole
  **Vessels** kingdom, dropped on the argument that *a hull is not a lifeform and the spawn picture
  cannot show one landing* — **an argument about the PICTURE that cost the window a branch of the
  bench**. *When a surface drops an option for presentation reasons it stops being the same
  surface; a window may decline to WATCH something, never to OFFER it.* `MatrixToy` now makes it
  structural — a subclass overrides `BuildOptions` and nothing else about its choices, the base
  builds the matrix from that list AND answers the window with the same call (non-virtual, so there
  is nowhere to put a second opinion), and `CreateStation` wires the station's action to the
  option's own `Apply` so the two are one call rather than two that agree. `ToyShellOption.Payload`
  carries the toy's subject, which is what removes the parallel list of subjects that could drift.
  **Exactly one difference may be declared** (`WorldOmitsCurrentOption` — the vessel changer sets
  it because flying your own hull would swap it for itself; the cell selector does not, because
  choosing the world you are in IS the freestyle reset). A single-action toy already had it right
  and is the pattern to copy: `Apply = ActivateFromShell` → its own `OnActivated`, one
  implementation of "throw this switch". Gate: `ToySurfaceParityTests` (all three matrix toys
  failed it before the pass).
  **AND THE THING THAT LICENSED THAT OMISSION WAS THE NAME.** *A name is not a label on a system —
  it is the shortest argument anybody makes about that system's SCOPE, and it is the one they reach
  for the day a surface is inconvenient to implement.* The bench was the **Lifeform Matrix** and has
  offered three kingdoms since the hangar landed, two of which are lifeforms and one of which is a
  **Vessel**; nobody decided to drop the hangar from the window, the name just made dropping it read
  as tidy. **When a system grows past its name, the growth must provoke a RENAME, never an
  inconsistent omission.** It is the **Spawn Matrix** (`SpawnMatrixToy`,
  `SpawnMatrixToyDefinitionSO`, `Toy_SpawnMatrix.asset`, id `spawn-matrix`) — named for the ACT
  rather than the taxonomy, which is what makes it survive a fourth kingdom. Two rejected candidates
  carry the rule: **"Mass"** is already a fundamental (conserved prisms) AND an element, so a *Mass
  Spawner* reads as the painting toy or as the crystal economy — **a word that is load-bearing
  elsewhere cannot quietly carry a second meaning**; and **"Threat"** is false for two of the three
  (a plant is not a threat, and a hull release is an AI companion in your OWN domain), i.e. the same
  mistake one notch over. "Spawn" is what the toy itself already said — every kingdom commits with
  `ToyShellOption.CommitVerb = "Spawn"` — so the name was read off the button rather than invented.
  "Matrix" is kept to separate the player's bench from the Cell's own population producers
  (`CellLifeSpawnerBase`, `RandomLifeSpawner`, `IntensityWiseLifeSpawner`).
  See `Docs/ToySystem/ARCHITECTURE.md` § "One declaration" and `Docs/ECOSYSTEM.md §19`.
- **Switch** — *a ring you thread, and threading it activates something.* The one word
  the platform has for "this does something when you go through it", and deliberately
  **threader-agnostic**: a **Vessel** threads a freestyle **Toy**, a ball threads a
  Scarab switch or an Astro League goal. Named as a fundamental at the prompter's
  request; the reach was already there before it was named — freestyle toy roots +
  matrix stations, the painting toy's stroke gates and milestones, the SHARE/REPAINT
  completion gates, the Wanderway return station, `ScarabSwitch` (`SCARAB.md §5`) and
  `AstroLeagueGoal`. It composes rather than duplicating: with **Vessel/Toys** (the
  activation affordance), with **Prisms/Mass** (a Scarab switch fills its ring with
  conserved prisms, and threading it BLOWS THAT MEMBRANE OUT along the ball's velocity
  and pays a **scarab-wing dais** in its place — 255 prisms wrapping five super-shielded
  sun cores, each aiming a spike back at the spent switch; both the removal and the
  payout are active events caused by a specific strike, never a clock, `SCARAB.md`
  §5.1), with **Domain** (see the shader law below), and with
  **Cells** (rings are placed against arena/membrane geometry, never a parallel system).
  **The law that makes it teachable is one line: THE RING IS THE TRIGGER VOLUME, DRAWN
  AT ITS OWN RADIUS** — so a ring can never advertise a volume the collider does not
  have. A ring drawn *smaller* than its trigger is legal (crossing it still always
  fires); a ring drawn *larger* is a lie. It is not a new atom in the toy shape
  vocabulary — it is the existing ring, promoted: the jack (*trail OFF*) is untouched,
  and an emblem stays a **tilted** ring of discrete objects so it can never be mistaken
  for a switch.
  **The SHADER is the switch's second half, and it says what the switch WILL DO.**
  Every switch is drawn in the **prism shader** — the same material family the painted
  trail wears, so a switch is made of the same stuff as the world it acts on — which
  leaves exactly one channel free to carry meaning: *which prism it is painted as*
  (`ToySwitchSignal`). **A switch wearing a playable DOMAIN's colour is reserved: it is
  one that HANDS you that domain** (the Domain Changer's slots; the painting's
  stroke-start gates, which really do call `RequestSetDomain_ServerRpc`). Everything
  else is `Neutral`, painted **`Domains.Blue`** — the platform's existing "no team"
  sentinel — and the signal, never the caller, picks the colour, so a neutral switch
  cannot wear a playable domain even by mistake (`ToyFactory.SwitchDomain`;
  `AddSwitchRing` takes no raw `Color` or `Material` at all). **Two wearers sit outside
  the toybox, and both are a claim about the SWITCH rather than about the pilot**:
  `ScarabSwitch`, where the colour names the domain the switch *belongs* to rather than
  one it grants, and the Butterfly's fold pair, one notch further out, where it names
  **who may thread it** — a gate declines a pilot who is not already in its domain and
  can never put anyone into one. (The pair was a `FoldGate` ring and is now a domain-locked
  wormhole whose rim wears the domain's hue — same reading, no longer a switch ring.) Nothing in either case changes a pilot's domain, so the
  two readings of a domain-coloured ring never share a screen;
  `ToySwitchVocabularyTests` holds the allow-list in both directions.
  **The cone is no longer part of this vocabulary** — as a BODY (one you fly at, rather
  than a hub inside a ring) it is **reserved for a booster**, which is why the Domain
  Changer became a switch and why its meaning moved from its shape to its shader. Adding
  a verb is adding an enum member plus its row in `ToyFactory.SwitchMaterial` — one
  place, so the language grows without any switch builder learning about it. See
  `Docs/ToySystem/ARCHITECTURE.md` § "The switch".
- **Lit** — *mass standing inside a force volume is LIT, in the colour of whoever owns that force.*
  Added at the prompter's request. **THREE producers, and the third one REPLACES code rather than
  adding it**: the Dolphin's Echo Sight (a **pending** blast), the Sparrow's proximity fuze (an
  **armed** warhead), and every own-domain explosion passthrough (a blast that **arrived** and
  spared it — the 2-second temporary shield's replacement, below). They differ only in WHEN the
  force lands, which is a property of the producer; what they share is the volume, the owner and
  the light. The passthrough covers EVERY shape, because every blast that spares its own domain
  has a passthrough to express — the Scarab's swept plate (`affectSelf: 0`) lights through the same
  call as the Dolphin's cone, so it is the cylinder arm of one producer rather than a producer of
  its own. **A SKIM FIELD was built as a fourth and CUT on a look call after playtest, and its two
  reasons are the questions to ask of the next producer.** It was the only **always-on** producer,
  and an ARENA card seats eight hulls with two skimmers each — on that card alone it would have
  filled the eight-slot bank twice over with ambient light and evicted every light that carries
  information, because *a producer that is always on competes with every producer that is only on
  when it matters*. And it was the only one whose sentence is addressed to its OWN pilot: a pending
  blast, an armed warhead and a spared prism are all things a RIVAL needs to read, where "where am
  I farming" is feedback nobody else wants. So adding a producer is two questions, not one — *who
  is this sentence for*, and *is it on all the time*. It composes rather than
  duplicating: **Domain** (a light says whose), **Mass/Prisms** (a predicate over conserved mass
  that stores nothing on it), **Elementals** (every volume is already elementally scaled),
  **Vessels** (who lights), **Cells** (lit mass is ordinary mass). **A light may be restricted to ONE domain's mass, and the
  restriction belongs to the LIGHT** — only the passthrough uses it, because only its sentence
  ("that blast went through here and SPARED this") is about mass its owner owns, and lighting the
  opposing prisms it was busy destroying said the opposite of what was happening. The two AIM
  producers stay ungated on purpose: their sentence is about mass their owner does NOT own, so a
  blanket rule would have deleted both. The prism's own domain arrives as a **per-material float**
  (`_PrismLitDomain`, stamped by `ThemeManager.PaintPrismTier` on the clones it already makes one
  per domain), so a prism's MATERIAL is its domain, a stolen prism carries its new one the instant
  the swap lands, and the gate costs no per-instance override and nothing per frame. **Zero is safe
  at both ends** — `Domains` has no zero member, so an unset gate means *no gate* and an unset
  prism domain means *this has no domain*, which is what excludes a dying prism's **debris** for
  free (fragments draw with the pooled debris material nobody stamps; they are not mass any more,
  and the blast never touched them). And **a graph named for one thing draws another**:
  `ExplodingBlockGraph` is also the material a LIVE prism's plain transparent tier wears, so the
  property had to go into BOTH prism graphs or every such prism would silently fall outside every
  gated light. `LitVolume.Contains` is the ONE
  CPU transcription of the three Burst sweeps and of the GPU half — `BlastVolume.Contains` now
  DELEGATES to it rather than keeping a fourth copy of the cone arm. Cost is **zero per-prism CPU
  and zero colliders**: five uniforms plus five `float4[8]` arrays written once per frame, with
  each shape's own first test (one dot, two compares) as its GPU reject; the shape branch is
  uniform across a wave and never diverges. **Continuity of existence is structural** — an
  unreported light FADES and is then dropped, so no producer has to remember to fade its own,
  which matters most for an explosion (`Destroy`ed the frame its sweep ends). **Your own aim always
  wins** on any prism it covers, on a separate exclusive channel that is untouched and still
  bit-identical. **Nothing reads it to decide an outcome, deliberately**: a light's SIZE rides
  owner-local element levels and a tick-rate 0.5%-gated NetworkVariable, so two machines agree on a
  boundary prism only to within a tick — fine for photons, not for a kill; any future combo resolves
  on the owning machine and REPORTS (the `ReportFaunaKill_ServerRpc` family). **It replaced the
  own-domain explosion's 2-second temporary shield**, which was reached for as a visual and quietly
  carried three side effects nobody designed — shielded mass is not food AND is re-filed out of the
  cell's fauna targeting grids, so a friendly blast armoured its own trail against the ecology and
  churned the grids twice; plus one `ShieldActivate` SFX, one timer, one octahedron engage and one
  shed-debris entity PER PRISM. Removing it is a move toward the conserved-mass invariant (mass
  returns to the sanctioned sink) at **zero collider cost in both directions** — a shield swaps the
  mesh and the mass, never the collider (`shieldMeshCollider.enabled = true` appears nowhere, and
  both shield components' `sharedMesh` writes land on the MeshFilter, so there is not even a convex
  cook). **That claim had regrown to 23 sites** — `PrismKind`'s own doc comment, the whole
  Microscene palette family, and the collider-budget lines of five arenas and six `ECOSYSTEM.md`
  sections — after being refuted three separate times in three new places instead of swept. All 23
  are corrected and `Tools/Build/check_shield_collider_claims.py` (`--check`, `--self-test`, proven
  on the pre-fix tree at 23 findings and 0 after) now fails the build on the next one, letting a
  refutation or a historical narration through. The general rule is CLAUDE.md's own deleted-SDK
  rule one level down: **a retired claim goes on looking present for as long as anything still
  describes the project in its terms, so the refutation has to be a SWEEP and not a new
  paragraph.** The **authored** permanent
  shield (`shielding`, the Sparrow's CHARGE-5) is kept and is unaffected; only the timed stand-in
  went away, and its registry sync moved INSIDE that branch. Stated cost: the shed-debris spray
  each pop threw is gone, and a fully destructive blast publishes no light so it looks exactly as
  it did. **Growing the peer BANK means RENAMING its globals, because a shader global
  array's length is pinned PER EDITOR SESSION and keyed on the NAME**: `PrismLit` (8 slots)
  inherited the retired `PrismDestructionSight`'s (4) property names, so every editor that had
  ever run the old code logged `exceeds previous array size (8 vs 4)` every frame AND silently
  dropped peers 5-8 — nothing wrong in the tree, invisible to every offline gate, and a player
  build never sees it. *Restart Unity* is what the message says and it is not a fix: it helps
  only the machine that does it, only until the next supersession, and only if whoever hits the
  wall knows to. The bank is therefore `_PrismLitPeer*` / `PRISM_LIT_PEER_*` — **a name Unity has
  never been asked to bind cannot carry a pinned length.** *Superseding or resizing anything that
  publishes a shader global array: rename the globals in the same commit.* Full record:
  `Docs/LIT.md`.
- **Ark** — *a mothership: a prism-bodied home that travels the hypersea, wears a domain, and
  lives or dies by the food web.* Added at the prompter's explicit request as the anchor of the
  highest-level gameplay arc — faction missions, where players venture into the hypersea for
  story-driven reasons with galactic consequences — and derisked first through the **Arkway**
  toy (the cellular Wanderway: a corridor of three satellite **Cells**, previous/current/next,
  that an `Ark` sails at its own unhurried pace while players are leashed to its side). Its
  reach is what earns the weight: it is the pace-setter of a voyage (the one clock a toy may
  own — the player opts in, sustains it, and can end it), the objective of an escort (protect
  it / lose it / reset), and the future seat of faction identity. It composes rather than
  bypasses: its HULL is ordinary conserved **Mass** laid through the canonical prism path in
  its owner's **Domain** — so **Flora & Fauna** attack or defend it purely through the shipped
  diet rules (fauna spawn in a **Cell**'s controlling colour; in a nucleus-less cell herbivores
  eat only opposing-domain mass — so *protecting an Ark IS controlling the cell*, with no aggro
  system anywhere); it moves the way fauna move (container transform + the
  `Prism.NotifyPositionChanged` mover contract, plus `PrismSpatialIndex.NotifyCellChanged`, the
  cell re-bind written for it); it dies the way a creature dies (last hull prism destroyed) but
  is deliberately **NOT a `LifeForm`** — no elemental heart, no starvation clock, no
  reproduction: the lifeform-crystal invariant governs lifeforms, and an Ark is a vessel-like
  home, not a creature. Its only deaths are active forces (fauna consumption, player
  abilities); it imposes no decay and no timer on anything else. Code: `Ark`
  (`_Scripts/Controller/Environment/`), first vehicle the Arkway — the **Wander** toy's With Ark choice (`WanderToy`/`ArkwayRun`/`CellConveyor`).
  Record: `Docs/ECOSYSTEM.md §41`, `Docs/ToySystem/ARCHITECTURE.md` § "Arkway".

### Process for curating fundamentals

The goal is an *exhaustive, minimal* set of fundamentals — expressive enough
to solve every problem through composition, small enough that the team can
hold the whole set in their head. Every fundamental costs mental overhead
for everyone who touches the codebase, so adding one must be a deliberate
act, not a side-effect of a feature ticket.

Before treating something as a fundamental (or before proposing a new one),
run this check:

1. **Name it precisely.** Use the canonical term. If no canonical term
   exists, propose one explicitly and get it agreed before using it.
2. **Show its reach.** A fundamental earns its place by being load-bearing
   for many features. Enumerate at least three distinct features or
   behaviors that depend on it; if you can't, it's probably not fundamental.
3. **Show how it composes.** Describe how it interacts with each existing
   fundamental. Emergence comes from the cross-products between
   fundamentals, so a system that doesn't meaningfully combine with the
   others is a bespoke feature wearing a fundamental's costume.
4. **Prefer extension over addition.** If a proposed fundamental is a
   special case of, or expressible through, an existing one, extend or
   rename the existing one instead.
5. **Budget the weight.** A new fundamental must be *very* useful to justify
   the weight it adds to the set. Flag any proposed addition to the
   prompter and get explicit agreement before committing to it.

### Order of preference

When addressing a task, try these approaches in order and stop at the first
one that fits:

1. **Use an existing fundamental.** Can the goal be achieved by composing
   behaviors the current fundamentals already produce?
2. **Tune parameters.** Can it be achieved by adjusting the parameters,
   weights, or configuration of an existing fundamental?
3. **Extend a fundamental.** Can it be achieved by adding a small, general
   capability to an existing fundamental that other features could also
   benefit from?
4. **Propose a new fundamental.** Only after the steps above have been
   rejected for clear reasons, *and* after running the curation process
   above with explicit prompter sign-off.
5. **Add a bespoke solution.** Last resort, and only when a new fundamental
   would be unjustified weight.

Three similar lines is better than a premature abstraction, but a bespoke
feature that duplicates or bypasses an existing fundamental is worse than
either.

### Don't "cheat" emergence without asking

A "cheat" is any solution that directly hard-codes the desired outcome
instead of letting it arise from the interaction of the fundamentals.
Cheats are tempting because they are shorter and more predictable, but they
erode the systems that make the game's behavior rich and surprising, and
they tend to accumulate special cases.

If the most direct path to a goal would require reaching past the
fundamentals and using privileged information or a shortcut to explicitly
produce the outcome, **stop and ask the prompter for explicit permission
before doing so.** Describe the emergent alternative you considered and why
you were tempted to bypass it, so the prompter can make an informed call.

**Example.** Suppose the task is to balance the ecosystem by creating fauna
that are attracted to prisms. The emergent approach is to place prisms and
configure fauna attraction parameters (working through the Flora & Fauna
and Prism fundamentals), then let the fauna find them. A cheat would be to
use the known planted locations of the fauna to directly place or steer
things so the balance is achieved by construction. Before taking that
shortcut — for instance, before reading fauna placement data and acting on
it to short-circuit the attraction behavior — ask the prompter whether they
want the cheat or the emergent solution.

**Example (resolved): prism decay is a cheat — mass is conserved.** Cells fill
with the dominant domain's flora and "freeze solid": fauna only eat *opposing*
mass, so the leader's flora have no predator and the prism count never falls.
The tempting fix is **passive prism decay** — prisms age and die on a timer (or
a cell-level reaper culls N per tick) so the count drops on its own and flora
resume growing through the phase hysteresis. **That is a cheat** — a timed
culler is just the flora regrowth-pulse inverted, a hard-coded oscillator
reaching past the fundamentals to manufacture the breathing we want to *emerge*.
The decided answer (do not relitigate): **prisms are conserved; the only sinks
are active — vessel abilities and fauna consumption.** The down-force on a
dominant accumulation is the **food web**: opposing-domain fauna graze it down,
or, when no fauna can reach edible prey, the population crashes via starvation.
A large accumulation that nothing is eating is a *valid* equilibrium, not a
defect to auto-correct. If a future cell "freezes," fix it by giving an active
force a reason/ability to consume that mass (or by tuning fauna diet, reach, and
spawning) — never by adding decay. The flora regrowth pulse that currently
exists is the growth-side counterpart of this same cheat and is flagged for
retirement, not extension. See `Docs/ECOSYSTEM.md`.

**Example (resolved & reverted): the menu trail cap is a cheat — no "cosmetic"
exemptions.** The Menu_Main autopilot vessel lays prisms indefinitely, so a
perf-motivated commit added a per-trail ring-buffer cap (`maxTrailBlocks` /
`Trail.RemoveOldest`, commit `64d8f0c8`) that silently recycled the oldest
trail prism on every new spawn, rationalized as "cosmetic, menu-only —
gameplay unaffected." That rationale was false by construction: the lava lamp
*is* freestyle (one system, two names — see "Lava-Lamp Mode"), so the same
capped vessel is the one the player flies, and the cap followed them into
freestyle flight as an age-based trail limit — exactly the passive-removal
cheat §0 of `Docs/ECOSYSTEM.md` rejects. The commit was reverted. The decided
answer (do not relitigate): **there is no context in which trail caps, prism
TTLs, or idle cullers are acceptable.** If prism accumulation in the menu (or
anywhere) is a perf problem, solve it with the universal systems: **fauna
cleanup** (cleanup is one of the fauna's jobs — foragers consume trail mass
through the food web) or **pause/throttle the spawner** (not creating mass is
allowed; aging it out is not). **Two authorized exceptions exist** — the
Wanderway rolling tether, granted by explicit sign-off to make that toy a truly
infinite runner at fixed memory, fenced to a live `WanderwayRun`; and the Skim Race
/ Joust trail cap on low-end phones (2026-10-05), granted by the project owner for
the `MobileLow` device tier only, fenced to `RaceTrailCap` in those two modes on that
tier (`Docs/PLATFORM_UNIFICATION.md` §3.6). Both are recorded in
`Docs/ECOSYSTEM.md` §0. Each is an exception *because it was asked for*, not a
precedent: the protocol still stands, and the next one needs its own sign-off.

### Universality — one HyperSea, one rule set

The fundamentals are universal. The HyperSea has rules and **everything in it
follows them** — game scenes, Menu_Main's lava-lamp/freestyle, tools and test
scenes alike. Do not create context-specific exemptions ("it's only the menu,"
"it's just cosmetic," "it's a perf special case"). Every carve-out creeps
confusion into best practices about when the rules apply, and carve-outs are
precisely how rejected cheats re-enter the codebase — both resolved examples
above came back wearing a special-circumstance costume. (The phone race cap is the
one perf special case that was explicitly granted — by the project owner, for one
device tier and two modes, recorded in `Docs/ECOSYSTEM.md` §0. That grant does not
extend to any other tier, mode or system; asking again is the only route.)

When a context creates pressure (performance, pacing, visuals), solve it with
the universal systems that already exist — fauna have many jobs and cleanup is
one of them; spawners can pause; abilities can consume — never with a bespoke
mechanism that exists only in that context. Build systems once, use them
everywhere. If a universal system genuinely can't serve the context, that is a
fundamentals discussion (see the curation process above), not a license for a
local workaround.

### When in doubt

Name the fundamentals involved, describe how each candidate solution
interacts with them, and prefer the solution that leaves the fundamentals
intact and more expressive for future features.
