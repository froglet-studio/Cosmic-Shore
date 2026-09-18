# LIT — a fundamental

**Mass standing inside a force volume is LIT, in the colour of whoever owns that force.**

That is the whole of it. A producer publishes a volume and a domain; every prism inside it is
drawn lit. It is a *statement*, never a query — nothing walks prisms, tests prisms, tracks prisms
or stores anything on a prism.

| | |
|---|---|
| The state | `LIT` |
| One light | `LitVolume` (`_Scripts/Utility/Lit/`) + `LitShape` (`_Scripts/Data/Enums/`) |
| The system | `PrismLit` (`_Scripts/Utility/Lit/`) |
| The GPU half | `PrismDestructionSight.hlsl` (`_Graphics/Materials/Graphs/`) |

## Why it is a fundamental

Several systems independently wanted to say the same sentence — *my force is reaching that mass,
and it is mine* — and each was about to say it its own way. They differ only in **when** the force
lands, which is a property of the producer, not of the state:

| Producer | Volume | Moment | Owner |
|---|---|---|---|
| Echo Sight (Dolphin, Charge) | Cone | **pending** — what the next blast would sweep | `EchoSightActionExecutor` |
| Proximity fuze (Sparrow skyburst) | Sphere | **armed** — where this warhead will go off | `Projectile.PublishFuzeLit` |
| Explosion passthrough | any | **resolved** — the blast arrived and spared this | `ExplosionImpactor.PublishLit` |

Three producers, and the third one is a **replacement rather than an addition**: it is what the
2-second temporary shield used to do (see below), so it is the only one of the three that removes
code instead of adding it. It covers every shape, because every blast that spares its own domain
has a passthrough to express — the Scarab's swept plate (`affectSelf: 0`) lights through exactly
the same call as the Dolphin's cone and the sphere blasts.

A **skim field** was built as a fourth (a sphere, `continuous` — the mass a skimmer is working)
and was **cut on a look call** after playtest. The Scarab plate stopped being counted as a
producer in the same pass — not because its light went away (it still lights, through the
passthrough row above) but because calling it one implied a hook that does not exist. Two things
the skim field leaves behind. It was the only **always-on** producer in the game, and an
ARENA card seats eight hulls with two skimmers each, so on that card alone it would have filled
`PrismLit.Slots` twice over with ambient light and evicted every light that carries information —
*a producer that is always on competes with every producer that is only on when it matters.* And
it was the only one whose sentence is addressed to its **own** pilot: a pending blast, an armed
warhead and a spared prism are all things a rival needs to read, where "where am I farming" is
feedback nobody else wants. Adding a producer is therefore two questions, not one — *who is this
sentence for*, and *is it on all the time*.
Composition with the other fundamentals is what earns it the weight: **Domain** (a light says
*whose*), **Mass/Prisms** (a predicate over conserved mass that adds no state to it),
**Elementals** (every volume above is elementally scaled already), **Vessels** (who lights),
**Cells** (lit mass is ordinary mass — lighting it changes nothing about it).

## Cost

- **Zero per-prism CPU.** Five plain uniforms (the viewer's own aim) plus five `float4[8]` arrays
  and a float (everything else), written once per frame in `LateUpdate`. O(1) in total — not per
  light, and not per prism.
- **Zero colliders.** Nothing is added and nothing is enabled.
- **GPU:** per prism, per occupied slot, each shape's own first test — one dot and two compares —
  when the prism is outside. That is why there is no bounding sphere to maintain and why the bank
  went from 4 slots to 8 cheaply. The shape branch is **uniform across a wave** (every prism in a
  draw reads the same bank), so it never diverges.
- With no producer lighting anything, `Flush` returns before writing and the shader returns after
  two compares.

## Rules

1. **One predicate, three shapes.** `LitVolume.Contains` is the single CPU transcription of the
   three Burst sweeps in `PrismSpatialIndex` and of the GPU half. `BlastVolume.Contains` now
   **delegates** to it — it used to be a fourth hand-written copy of the cone arm, and a preview
   that drifts from the damage it previews is worse than no preview.
2. **`LitShape`'s numbers are the wire format.** `PrismLit` packs the enum member into a shader
   global that the HLSL switches on; `PRISM_LIT_SHAPE_*` mirrors it. Change both together.
3. **A light wears its owner's domain, resolved in one place.** `PrismLit.DomainTint` reads
   `SO_ColorSet.GetDomainSignalColor`. The palette is handed to `PrismLit.ColorSet` by
   `ThemeManager.Awake`, exactly as it already hands the same asset to `GameToastAPI` — a static
   that needs one asset and cannot be injected is an existing shape here, not a new mechanism.
   `Domains.Blue` answers white.
4. **Continuity of existence is structural, not per-producer.** A slot that stops being reported
   **fades** and is then dropped (`PrismLit.Flush`, on `unscaledDeltaTime`). There is no API that
   can make a light pop out of existence; only a scene teardown clears outright. This matters most
   for the shortest-lived producer: an explosion is `Destroy`ed the frame its sweep ends and cannot
   fade anything itself.
5. **Your own aim always wins.** `PublishAimed` is a separate, exclusive channel on plain uniforms,
   evaluated first and returning early. The instrument you are aiming with is never recoloured by
   a rival sweeping past, and that arm of the shader is **untouched** by the generalisation — which
   is what keeps `Tools/Shaders/verify_prism_sight_composition.py` a regression proof rather than a
   fresh measurement. It is cone-only, deliberately: a shape tag there would mean a fourth `Vector3`
   property on every prism graph for a shape no producer needs.
6. **Overflow evicts the weakest**, never whichever the dictionary enumerated last — an arbitrary
   drop would be an invisible, machine-dependent difference in what each player sees.

## Nothing reads it to decide an outcome — deliberately

There is no `IsLit()` consumer. The state is published and drawn; consuming it is a later,
separate decision, and it carries a replication problem this system does not solve.

A light's **size** is a function of owner-local element levels and locally-simulated resources, and
a peer's arrives as a tick-rate, 0.5%-change-gated `NetworkVariable` (`NetEchoSightShape`) while
the owner uses its live value. Two machines therefore agree on a boundary prism only to within a
tick. That is fine for photons and **not** fine for a kill.

So any future combo must resolve on the **owning machine and report** — the
`Player.ReportFaunaKill_ServerRpc` family — never evaluate `Contains` independently per peer and
act on it.

## The temporary shield it replaced

An own-domain explosion used to `ActivateShield(2f, …)` **on every prism it spared**, in both the
managed path (`ExplosionImpactor.ExecuteCommonPrismCommands`) and its Burst twin
(`PrismSpatialIndex.ResolveExplosionHit`). It was reached for as a *visual* — "the prism armours up
instead of the explosion visibly passing through it" — and a light says the same thing.

It was not only a visual. Measured, a 2-second shield also:

- **Blacked out the food web on the blast's own footprint.** Shielded mass is not food
  (`Fauna.IsShieldedMass`, `Docs/ECOSYSTEM.md` §16/§22) and is re-filed *out* of the cell's fauna
  targeting grids (`PrismStateManager.SyncAOERegistryShieldState` → `ForwardShieldChangeToCell`).
  So a friendly blast briefly armoured its own trail against the ecology and churned the targeting
  grids twice per blast. Nobody designed that; removing it is a move **toward** the conserved-mass
  invariant, since it returns mass to the one sanctioned sink (fauna eating it).
- **Played one `ShieldActivate` SFX per prism** (`ApplyShieldState`, subject only to
  `AudioSystem`'s category throttle).
- **Cost N shield transitions, N `PrismTimerManager` timers, N octahedron engages and N
  shed-debris entities**, where N is every own-domain prism in the blast. The light costs **one
  volume**.

**Collider impact: zero, in both directions.** A shield swaps the mesh and the mass, never the
collider — `shieldMeshCollider.enabled = true` appears nowhere in the codebase (four sites, all
`= false`). Note `PrismKind`'s own doc comment still claims shielded prisms carry an always-on
convex `MeshCollider`; that is stale.

**What is kept.** `shielding` — the Sparrow's CHARGE-5 *Shielded Prisms* — still lands. It is a
real ability, it is **permanent** rather than timed, and it is a gameplay grant rather than a
stand-in for a visual. Only the timed stand-in went away, and its registry sync moved *inside* that
branch: writing `UpdateShieldState(idx, true, false)` unconditionally would have told the index
every spared prism was shielded when none of them are any more, which is the blackout the swap
exists to remove.

**What is lost, stated plainly.** The shed-debris spray each shield threw when it popped 2 s later.
Light says *acknowledged*; debris said *it cost something*. A blast that destroys everything it
touches (`affectSelf` **and** `destructive`) publishes no light at all, so every fully destructive
blast in the game looks exactly as it did.

## Verification (not yet run — no editor in this session)

1. **Dolphin, freestyle.** Hold Echo Sight. The cone should read exactly as before — this arm is
   bit-identical. Run `Tools/Shaders/verify_prism_sight_composition.py` to prove it.
2. **Dolphin, The Bends / Rampage.** Fly a crystal into your own trail. The spared prisms should
   glow in your domain and fade over ~0.35 s. **No shield octahedra, no pops, no shield SFX.**
3. **Same, with fauna present.** Graze the blast's footprint immediately after. Fauna should be
   able to eat it — previously they could not for 2 s.
4. **Sparrow, Dog Fight.** Fire a skyburst past a wreck. The fuze sphere should light mass as it
   passes and grow with MASS.
5. **Scarab, Scramble.** Juke-dash beside your own mass. The plate spares its own domain
   (`affectSelf: 0`), so the passthrough light covers it — both halves of the mirrored cylinder,
   including the half **behind** you. This is the cylinder arm of one producer, not a producer of
   its own.
6. **Any vessel, freestyle.** Skim. **Nothing should light** — the skim-field producer was cut.
7. **Editor validators:** FrogletTools ▸ Ecology ▸ Prism Animation (the Custom Function name and
   file are unchanged, so no graph should need rewiring) and `PrismLitTests`.
