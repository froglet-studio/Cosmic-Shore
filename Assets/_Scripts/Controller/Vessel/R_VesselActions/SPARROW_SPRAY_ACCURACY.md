# Sparrow — Spray Accuracy (the walking gun)

> **The rule, in one line:** *hold the trigger and the cone opens — the danger zone grows and
> your hands feel it; let go for an instant and you are pin-accurate again.*

The Sparrow's cannons are a **saturation** weapon, not a marksman's rifle.

| you do | you get |
|---|---|
| tap the trigger (≤ 0.12 s) | a perfectly accurate burst — a scalpel, at a fraction of the volume |
| hold it down | a cone that opens over ~1.6 s to a 1.5° cap, filled at **180 rounds/s** — nothing along the path survives, and the buzz in your hands climbs the whole way |
| release and re-pull | full accuracy back, instantly. This is the "3-shot burst" the design asks for |
| collect Mass crystals | rounds swell **harder** as they fly — 3× over a flight at rest, **6× at Mass 10**. The tracer stays a thin pale-blue needle; a see-through charge shell grows around it to exactly the hit radius, drawing **one** blue-and-danger-red bolt across a randomly-oriented great circle — a burst's worth of them is what draws the sphere. Huge projectiles, earned |

---

## Round 2 (2026-08-13): the guns could not clear a small area, and spread was not why

Playtest report: *"far too difficult to destroy all the prisms in a small area. When I increase
the projectile radius I get the desired results, except that giant bullets from a small vessel is
silly. The hope was that increasing the fire rate and the spread would be a reasonable substitute,
but it was still inadequate."*

That report contains its own diagnosis. **A bigger radius worked because the bullet was missing
most of its own flight path**, and no amount of fire rate or spread can compensate for a weapon
that is structurally blind to the ground between its samples.

### The bug: a projectile is a TELEPORT, not a sweep

`Projectile.MoveProjectileAsync` advances the transform by `Velocity · Δt` each frame, and PhysX
samples that discrete trigger once per physics step. So collisions are only ever tested at the
handful of points the round *lands on* — never along the line between them:

| | muzzle speed | step / frame @60 fps | hit sphere Ø | **path actually tested** |
|---|---|---|---|---|
| SPACE 0 | 375 u/s | 6.25 u | 1.65 | **26%** |
| SPACE 5 | 1875 u/s | 31.25 u | 1.65 | **5%** |
| SPACE 10 | 3375 u/s | 56.25 u | 1.65 | **3%** |

Three-quarters of every shot's path was never tested for collision, and at range it was
97%. Prisms in the gaps were passed straight through — silently, with no miss to see. Halve the
frame rate and it halves again.

**This also explains the pre-existing collider history.** Round 6 of the turret pass shrank the
bullet's hit sphere from a 12 diameter to 1.65 after finding nothing had *authored* the 12 — it
fell out of the tracer mesh's ×20 z-stretch leaking into a `SphereCollider` radius. That was
geometrically correct and it silently removed the thing that had been papering over the tunneling:
a 12-diameter ball closes a 6.25 u step completely. The accident was load-bearing. (Even it was
not enough at range — at SPACE 10 the old ball still only covered 21% of the path.)

### The fix: test the segment, not the landing point

`PrismSpatialIndex.QuerySegment(a, b, radius, results)` — the swept counterpart of `QuerySphere`,
gathering every live prism within `radius` of the segment the round crossed this frame. This is
the fix `SPARROW_TURRET_STANCE.md` named in its follow-ups ("a swept segment query on
`PrismSpatialIndex`, not CCD — a transform teleport bypasses CCD") and the one CLAUDE.md's
anti-pattern list demands (never `Physics.OverlapSphere` against prisms; new query shapes go on
the index).

It is **exactly the effect of a huge bullet with none of the appearance** — which is what the
playtest asked for.

- **`Projectile.sweptPrismDetection`** (opt-in, on for `SparrowProjectile.prefab` and
  `Sparrow Projectile Prism.prefab`) makes the swept query the **sole** owner of prism contact;
  `ProjectileImpactor` suppresses the trigger's prism case so nothing double-dispatches. The
  trigger was never a second chance — it is the thing that was missing 74%.
- **Hits dispatch nearest-first**, which is what makes the sub-SPACE-5 "destroyed on its first
  prism impact" rule mean the *first prism along the path* rather than an arbitrary one.
- **The round is moved to each contact point before its impact fires**, so effects — and the
  Turret Stance's anchor, which places its prism "wherever the bullet would be destroyed" — see
  where the shot actually met the prism, not where the frame's step happened to end.
- **Contact is bounding-sphere**, not an exact capsule-vs-OBB narrowphase: a few instructions
  instead of a narrowphase, erring slightly generous at corners — the right direction for a
  saturation weapon, and still far tighter than the oversized collider it replaces.
- Dispatch reuses `ImpactorBase.AcceptImpacteeFromSweep`, the exact analogue of the shell tier's
  `AcceptImpacteeFromShellContact`: same init gate, same profiler marker, same effect chain.

**Vessels and mines still use the trigger.** The same tunneling applies to them and is a real
follow-up, but a vessel hull is a much bigger target than a trail prism and widening this pass to
the whole impact system is its own change.

### And the tuning the report asked for

With the path actually being tested, spread does what it was supposed to do, so the cone gets
tighter and the volume goes up:

| | round 1 | **round 2** |
|---|---|---|
| `firingRate` | 60 volleys/s | **90** (180 rounds/s) |
| `spread.growthDegreesPerSecond` | 3.2 | **1.0** |
| `spread.maxHalfAngleDegrees` | 4 | **1.5** |

The cone now reaches only 0.88° after a full second of holding and caps at 1.5° (a 1.9 u radius
at the SPACE-0 range of ~72 u). It is a *texture* on the stream rather than a scatter — which is
what it should have been once the rounds started actually connecting.

---

## Round 3 (2026-08-13): rounds GROW as they fly — huge projectiles, earned

Playtest: *"we need to get creative to get these guns to feel fun because right now the only
thing that has felt fun was huge projectiles."*

Round 2 gave every round its whole path back, but it did not change the shape of what a round
deletes: **a thread**. A huge projectile deletes a **tunnel**, and that — not the hit rate — is
what was fun. The answer is to keep huge projectiles and take away the thing that made them
silly: a small vessel firing cannonballs. So rounds now **leave the muzzle small and swell as
they travel**, and MASS decides how much.

> **The split this settles: MASS owns the SUBSTANCE of what you fire. SPACE owns its REACH.**

| | |
|---|---|
| **MASS** quantitative | turret prism stretch (unchanged) **+ in-flight round growth** |
| **MASS 5** | **Shielded Prisms** — returned here from Space 5 by design sign-off |
| **SPACE** quantitative | range (unchanged) |
| **SPACE 5** | **pierce**, on both fire modes — and nothing else now |

### The growth curve

`FullAutoActionSO.ResolveGrowthFactor` → `GrowthFactorForLevel(massLevel, 3, 6)`: linear in
LEVEL with the authored pair anchored at 0 and 10, **extrapolated** (not clamped) across the
element system's full [-5, 15] band.

| Mass level | grows to | end-of-flight hit radius | swath vs. a non-growing round |
|---|---|---|---|
| −5 | 1.5× | 1.24 | 2.2× |
| 0 | **3.0×** | 2.47 | **9×** |
| 5 | 4.5× | 3.71 | 20× |
| 10 | **6.0×** | 4.95 | **36×** |
| 15 | 7.5× | 6.19 | 56× |

Destruction footprint goes as the **square** of the radius, which is why this is worth so much
more than any affordable fire-rate increase — and it is the number the "huge projectiles" report
was really reacting to. For scale: the accidental oversized collider that felt good was 6.0
world radius. Resting Mass now ends its flight at 2.47 and **Mass 10 reaches 4.95** — the fun
size is back, as something you earn rather than something the gun always had.

### Sized honestly, the whole way

The visual and the hit volume are scaled by the **same factor every frame**, so what you see is
what you hit, at every instant, at every Mass level. **Which visual carries that is round 4's
subject — see "Growth is a hit volume, not a size" below.**

**Cross-section only.** The tracer mesh is a unit sphere at (0.75, 0.75, 20) — a 20-long dart — so
scaling it uniformly at 6× would draw a 120-unit needle across a ~72-unit range. Width is what a
hit volume is made of; length is just the streak. The swept hit radius is therefore scaled
*explicitly* rather than re-derived from `lossyScale`, because a SphereCollider takes the largest
lossy component and that stays the untouched z-stretch.

One deliberate scope line falls out of that: the **swept prism** radius grows, while the PhysX
radius the **vessel/mine** path uses does not (for the dart — the turret's uniformly-scaled
carried sphere does grow on both). Growing bullets against vessels is a Dog Fight balance change,
not a prism-clearing one, so it is not in this pass.

---

## Round 4 (2026-08-24): growth is a HIT VOLUME, not a size

Playtest report: *"as the sparrow's bullets travel they grow. Mechanically this is great, but
this doesn't look right."*

Round 2 fixed the silliness of a small ship firing cannonballs by making the cannonball
something you grow into — and then re-created it inside every single flight, because the thing
being scaled was the **tracer model**. A Ø1.5 needle at 6× is a Ø9 red lozenge: the exact
"giant bullets from a small vessel" the whole pass exists to avoid, arriving 0.3 s at a time.

> **The model is the size it left the muzzle, for the whole flight. What grows is the volume it
> deletes — and a see-through charge shell is what draws that volume.**

|  | before | after |
|---|---|---|
| tracer model | swells 1.5 → 9 wide | **fixed at 1.5**, for every Mass level |
| swept prism hit radius | 0.825 → 4.95 | **unchanged** — 0.825 → 4.95 |
| PhysX radius (dart) | unchanged (max lossy stays the z-stretch) | unchanged |
| turret's carried collider | grows (it *is* the hit volume) | **unchanged** — still grows |
| what the player reads growth off | a fattening model | a crackling shell at exactly the hit radius |

Nothing mechanical moved. `ApplyFlightGrowth` still scales `_sweepRadius` by the same factor on
the same curve; it just no longer writes the transform when that transform is drawing something.

### Why the shell is a better instrument than the model ever was

The old read was **dishonest by a fixed 10%** — the hit radius was the visible cross-section
+10%, so the thing you aimed with was never the thing that hit. The shell is sized to
`_sweepRadius` itself, so it *is* the hit volume rather than a proxy for it, and
`SparrowRoundGrowthTests.TheChargeShellIsExactlyTheHitVolume` asserts that at every level and
every point of the flight. (The test it replaced compared `hit·s / visible·s` against
`hit / visible` — the same factor top and bottom. It was true for any code at all.)

It is also **see-through**, which a solid model can never be. Measured over the shipped shader:
mean alpha **0.036 at the muzzle → 0.079 fully charged**, arcs ~34% of the light. An enormous
round no longer hides the arena behind it.

### Which transform is a model and which is a hit volume — derived, not authored

Two structurally different things call themselves a `Projectile` here, and growth has to treat
them oppositely:

| | `SparrowProjectile` (bullets) | `ProjectileCollider` (turret's carried sphere) |
|---|---|---|
| has a renderer | **yes** — the tracer mesh is on its own root | no — it is a bare hit sphere under the fired prism |
| so its transform is | the **model** | the **hit volume** |
| growth scales the transform | **no** | **yes** (the only way growth reaches its PhysX radius) |
| carries a charge shell | yes | no — its visible half is the prism, which never bloated |

`Projectile.CacheTransformRole` answers this from the prefab's own contents at `Awake` rather
than from a serialized flag, because "a projectile must not grow a visible body" is true of every
prefab present and future, and a flag is a thing you can forget to set.

### The shell: `Shader Graphs/ProjectileChargeField`

Same visual language as the skimmer's forcefield crackle — arcs, an expanding ring, a fresnel rim
— with a different **driver**. `ForcefieldCrackleController` pushes impact points into a
MaterialPropertyBlock every frame, which is right for one skimmer and ruinous here: at 90
volleys/s over a 0.3 s flight one Sparrow keeps **~54 rounds in the air**, and a per-renderer
property block is a per-renderer draw call plus two 16-element vector arrays, every frame.

So the shell drives itself:

- **Arcs are a function of `_Time` and the shell's own object-to-world matrix.** Zero per-frame
  CPU writes, no property block, every round in the match batching through one material
  (`UnityPerMaterial` cbuffer → SRP Batcher compatible).
- **Growth needs no stamp.** The vertex shader reads the shell's own world radius off the model
  matrix. The CPU already had to write that scale, so the visual comes free with it.
- **Rounds are decorrelated by their own SIZE.** Two shots fired 11 ms apart are at different
  points of their growth, therefore different radii, therefore different animation phases — a
  stable per-round offset that drifts *continuously* as the round grows, so nothing ever pops. A
  world-position hash would re-roll every frame; a constant would strobe a whole volley in unison.
  Measured across 8 consecutive volleys the phases land at 0.41 / 0.97 / 0.53 / 0.09 / 0.64 /
  0.20 / 0.76 / 0.31 — spread across the cycle.
- **`Cull Back`, additive, `ZWrite Off`.** Front faces only: a pilot is never inside their own
  round, and one shell instead of two halves the overdraw of ~54 transparent spheres.
- **Charge is absolute and fleet-wide**, like the speed tunnel's mapping: `_ChargeReferenceRadius`
  (4.95 = the Mass 10 end-of-flight radius) is the "fully charged" size, so the same hit radius
  looks the same on any round and a Mass 10 shot reads hotter because it *is* bigger.

Cost, measured by compiling the shipped HLSL with clang and censusing it over the sphere: **~4.2
FBM evaluations per fragment**, out of a 15-iteration worst case — the per-seed envelope and
per-seed spatial early-outs discard most of it.

### One thing to keep

The shell must stay **unrotated**. The dart's transform is non-uniform (0.75, 0.75, 20), and a
uniform world sphere under it needs a per-axis divide (`Projectile.ChargeFieldLocalScale`) — which
is only valid because the child is axis-aligned. A non-uniform parent above a *rotated* child is a
shear, and no local scale can undo one.

### The size and palette pass (same day)

Follow-up: *"make the model diameter half as much, and change its colour to a desaturated whiteish
blue. Use more saturated blues and the danger red instead of yellows (neutral and danger) on the
effect."*

**Halving the model was free, and the reason is worth stating.** A `SphereCollider` scales by the
**largest** lossy-scale component, which on this dart is the untouched z-stretch of 20. So taking
the transform from `(1.5, 1.5, 20)` to `(0.75, 0.75, 20)` moved the tracer's cross-section from
Ø1.52 to Ø0.77 and left the hit radius at exactly **0.825** — the same accident that once made the
collider 8× too big (`SPARROW_TURRET_STANCE.md` § Round 6) is what now makes the model's size a
purely visual dial. It is only safe to say that *because the shell, not the model, is the hit
volume's instrument*; before round 4 this edit would have silently shrunk what the player aims
with.

Two other things the halving touches, both handled: the shell's authored local scale divides the
parent's lossy scale, so it moved `1.1 → 2.2` on x/y (`SizeChargeField` recomputes it at every
launch — the authored value is only what the editor shows before one), and `_Spread` on
`SpreadFresnelShader` displaces vertices by `_Spread / objectScale`, so a smaller object gets a
*larger* displacement. At the parent material's authored `0.01` that is 0.013 local units and the
halving is exact to within 3%; at a larger `_Spread` it would not have been.

**The dart's own material is new, because the old one was shared.** `DangerProjectileMaterial` is
on five prefabs (`ExplodableProjectile`, `ProjectileFX`, `BrightNucleus`, `TimeDandruff` and this
one), so recolouring it would have repainted four unrelated things. `SparrowProjectileMaterial` is
a fresh variant of the same parent, `BlueSpreadFresnelMaterial`, overriding only the three
properties `SpreadFresnelShader` actually declares — the donor's `_Color1` / `_Color2` /
`_DullColor` / `_CellDensity` / `_SrcBlend` keys are residue from a shader that material no longer
uses, and are deliberately not carried over.

| | linear value | on screen (ACES + sRGB) | role |
|---|---|---|---|
| `_DarkColor` (body) | `(0.26, 0.38, 0.70)` | rgb(154–175, 183–196, 217–220), hue 213°, **sat 0.25** | the needle |
| `_BrightColor` (rim) | `(0.50, 0.70, 1.20)` | rgb(205, 219, 236), **sat 0.13**, val 0.92 | brighter, whiter silhouette edge |

**"Desaturated" is a screen measurement, not an authored one** — and this is the PALETTE.md trap
(§4.1) arriving from a new direction. The first candidates were authored *as* pale blues
(`(0.72, 0.78, 0.92)`, and the palette's own `BlueColors.SpikeLightColor`), and both rendered at
screen saturation **0.03–0.06**, i.e. white. ACES compresses highlights, so a bright colour
desaturates on the way to the screen and a linear value that looks blue in the inspector does not
read blue in game. The shipped pair was picked by measuring the post-tonemap sRGB and hunting for
the 0.20–0.30 saturation band — pale enough to read "whitish", blue enough to read "blue". Judge
this class of colour after tonemapping, never in the inspector swatch.

### Neutral is blue, danger is red — and the threshold is what keeps them two colours

The shell's palette:

| slot | value | source |
|---|---|---|
| `_FresnelRimColor` (always-on shell) | `(0.25, 0.55, 1.0)` | **neutral** — a saturated blue |
| `_CrackleColorB` (arc body) | `(0.10, 0.35, 1.0)` | **neutral** — a deeper saturated blue |
| `_CrackleColorA` (arc core) | `(1.4979111, 0.0058463, 0.0068495)` | **danger** — `EnvironmentColors.Danger` from `OriginalColorSetSO`, **verbatim** |

The danger colour is taken from the live colour set rather than eyeballed, because it is the one
shared, domain-independent red the arena already uses for danger mass — so a round's hot core is
literally the same red as the thing that hurts you.

**The composition needed a new dial, and this generalises.** The arc colour was
`lerp(blue, red, arcHeat²)`, and a lerp between a saturated blue and a saturated red spends most
of its range in **MAGENTA** — which is neither colour, and was most of every arc. `_CoreThreshold`
(0.75) replaces the square with `smoothstep(_CoreThreshold, 1, arcHeat)`, confining the red to the
hot centreline so the arc reads blue with a red filament inside it. Measured over the shipped
shader, by hue-bucketing every lit fragment after tonemapping:

| `_CoreThreshold` | blue | magenta | red |
|---|---|---|---|
| 0 (the old `arcHeat²`) | 54.5% | 12.6% | 32.9% |
| **0.75 (shipped)** | **77.7%** | **7.4%** | **14.9%** |
| 0.92 | 87.5% | 4.3% | 8.1% |

> **Two saturated colours at opposite ends of the wheel cannot be blended — they have to be
> SEPARATED.** Any lerp between them passes through a third hue that belongs to neither, and on an
> additive surface it also *sums* with whatever is behind it. If a two-colour effect is reading as
> one muddy colour, the fix is a threshold, not a different pair of colours.

A note on why the small-panel render lied: at 300 px the blue arc and its red filament average
together and the whole thing reads magenta. The hue census said 7% magenta while the thumbnail
said "all magenta" — the census was right, and rendering **one large panel** settled it. Measure
the pixels, and view a candidate at the size it will actually be judged.

### What was deliberately NOT done

The alternatives considered and declined, so they are not re-derived: **impact shatter** (a kill
cracking its neighbours) and **pierce depth** (rounds boring through N prisms before stopping).
Both break the one-round-one-prism ceiling too, and both were rejected in favour of growth —
"keep everything else the same, we will get this feel through the mass scaling effect." SPACE 5
remains the only thing that lets a round pass through a prism.

---

## Round 5 (2026-08-25): one round is one STROKE — the volley is the sphere

Playtest report: *"tone down the Sparrow's projectile effect so each projectile reads less like a
sphere, but together they build a spherical effect over time. In other words, just a single arc on
each projectile — after many projectiles those arcs will stochastically fill in the circle as an
after-image in the mind of the player."*

Round 4 gave growth an honest instrument and then let the instrument draw too much. Three seeds,
each throwing five radiating filaments, over a lit centre and a standing fresnel rim, re-rolled
~62 times a second: every one of those terms is somewhere else on the shell, and a shell that is
lit *everywhere, faintly, all the time* is a glowing ball. Measured, one round painted **73% of
its own shell** over a single 0.3 s flight — it was assembling the sphere by itself.

> **The sphere is the PLAYER's to assemble, not the fragment's.** One round draws ONE bolt, lying
> on ONE randomly-oriented great circle. A burst lays stroke after stroke at different
> orientations and the shape accumulates as an after-image.

A **great circle** rather than a squiggle, because it does two jobs at once: superimposed at random
orientations, great circles are a sphere's wireframe — the most legible thing a stream of them can
add up to — and a single one is still a curve of exactly the hit radius, so the shell stays an
honest instrument for the volume it deletes even when only one stroke is showing.

### What the shell draws now

| | Round 4 | Round 5 |
|---|---|---|
| simultaneous discharge seeds | 3 | **1** |
| filaments per discharge | 5, radiating from a point | **1**, lying along a great circle |
| centre glow | yes (`_CenterFillAmount`) | **gone** |
| expanding ring wavefront | yes (`_RingThickness`, `_RippleSpeed`, `_ArcReach`) | **gone** — the stroke draws itself instead |
| fresnel rim | 0.18, the shell's loudest always-on term | **0.05** — a whisper, so a round is never *dark*, never a boundary |
| envelope | strike, decay | strike, **hold**, snuff |

The bolt strikes outward from a random point on its circle **both ways at once** (so any span up
to the full circle needs no wrap handling, and a round caught early in its cycle reads as a stroke
rather than a dot), wanders off the exact circle on 1-D FBM so it is lightning that happens to be
following one, and carries the danger-red hot core on its centreline and its two advancing tips.

### The constraint nobody gets out of: radius is BOTH the identity and the progress

The shell has no per-instance CPU write by design — that is what makes ~54 simultaneous rounds
affordable — so its only per-round signal is its own world radius. And that one number is
simultaneously *which round is this* and *how far along is it*. Consequently:

- decorrelating **simultaneous rounds** (so the stream is not one synchronised flash) costs
  per-round **flicker**, because a round sweeps the whole radius range over its flight;
- and it costs it at a bad exchange rate — a round's radius moves at `(g−1)·r₀/T` = **5.5 u/s** at
  resting Mass, while the radius *spread* across the whole stream is only **1.65 u**. Every unit of
  spatial variety buys 3.3 units of flicker.

The **time** term has no such penalty: it advances everyone equally, so it buys temporal variety
1:1 — but it buys *no* spatial variety at all, and `_PhaseByRadius = 0` would strobe an entire
volley in unison. So the shipped split leans on time for most of the variety and keeps just enough
radius term to break the stream up:

| | contributes | at a per-round flicker cost of |
|---|---|---|
| `_PhaseSpeed 2.6 × _CrackleRate 3.5` | **9.1 new circles per second** | 9.1 Hz |
| `_PhaseByRadius 0.43 × _CrackleRate 3.5` | **2.5 circles across the live stream** | 8.3 Hz |

→ **17.4 discharges/s per round (58 ms a stroke)** at resting Mass, **11.6 new great circles per
second** across a burst. (A Mass 10 round runs faster on both counts — 29.8 Hz, 6.2 circles across
the stream — because its radius range is 2.5× wider. It is also 2.5× bigger on screen, so more
crackle suits it.)

**Do not tune `_CrackleRate` alone.** It multiplies both terms, so it moves stroke lifetime and
fill rate together and cannot separate them. The two `_Phase*` values are the real balance.

### The envelope holds, and that is not cosmetic

The first cut used the donor's strike-and-decay envelope and the stream read as *twinkling*: a
round spent a third of every cycle below the eye's threshold, so at any instant a third of the
rounds in the air were showing nothing but the rim whisper. The shipped envelope rises fast, **holds
at full** until `_HoldTime`, then eases to zero — measured **86.3% of the time showing a stroke**,
and it still reaches zero at **both** ends of the cycle, which is the property that lets the great
circle be re-rolled at the boundary without the stroke visibly teleporting. A round's dimmest
moment is peak alpha **0.025** — the rim — so it never pops out of existence.

### Measured, by compiling and running the shipped HLSL

`Tools/Shaders/verify_projectile_charge_field.py` translates the shipped `ProjectileChargeField.hlsl`
to C++ (swizzle accessors, `out` → reference, one rename wrapping `ChargeFBM1D` in a call counter),
compiles it with `clang++`, pulls the previous revision out of git, and runs both through the
identical harness over 20,000 Fibonacci samples of the front hemisphere `Cull Back` actually draws.
It is a CI-able gate, not a one-off (~5 min, no Unity):

| | Round 4 | Round 5 |
|---|---|---|
| lit at one instant (one round) | 3.69% | **2.50%** |
| **planarity of the lit set** | **14.1°** | **2.8°** |
| one round paints, over its whole flight | 73.1% | **19.8%** |
| one *instant* of the whole 27-round stream | 68.0% | **9.5%** |
| union after 0.25 s of fire | 99.9% | 14.8% |
| union after 1.0 s of fire | 100% | 40.1% |
| union after 3.0 s of fire | 100% | **71.0%** |
| FBM evaluations per fragment | 3.94 (worst case 15) | **1.00 (worst case 1)** |

**Planarity is the number that carries the claim, and finding that out is half of what the harness
was for.** Raw lit *area* is nearly useless here: the old shell lit *few* pixels *everywhere* and
so measured a **smaller** lit fraction than a single fat stroke would, while reading as a ball.
What separates a stroke from a scatter is whether the lit set lies in one plane through the centre
— 2.8° of RMS deviation is a curve on a great circle; 14.1° is sparks all over a sphere. The area
number is kept only as a "did not go up" guard.

> **General rule: when a visual is a claim about a DISTRIBUTION over many instances, the metric is
> almost never the per-instance total. Measure the SHAPE of one instance and the UNION over many.**

The two union rows either side of "0.25 s" are the mechanic working: at any frozen instant the
stream shows **9.5%** of a sphere (~2.5 circles, shared by neighbouring rounds whose radii are
close), and three seconds later the accumulation is at 71% and still climbing. Nothing about a
single frame looks like a ball; the burst does.

### Things that would take it straight back

- **`_ArcCount` above 1.** It exists as a knob for a future weapon, not as a tuning dial for this
  one. Two strokes is two planes and the read collapses toward a ball immediately.
- **Raising `_FresnelRimIntensity`.** The rim is a standing, view-aligned, always-on sphere — the
  purest possible "this is a ball" term. If the shell needs to be louder, raise `_ArcIntensity`.
- **Re-introducing a centre fill.** A lit blob at the seed point is a small sphere inside the big
  one.
- **Dropping the hold out of the envelope** to get "snappier" strokes. That is where the twinkle
  came from.

---

## The three moving parts

### 1. Rate of fire — 30 → 60 → **90 volleys/s** (180 rounds/s across two muzzles)

Round 6 of the turret pass shrank the bullet's hit sphere 8× and deliberately made the guns
tighter: *"the guns feel tighter is the intended outcome, not a regression to fix by inflating the
sphere again."* That still stands — the forgiveness came back as **path coverage plus volume of
fire**, never as a bigger invisible ball.

### 2. The fire loops are now frame-rate independent — and this was load-bearing

Both fire loops used `await UniTask.Delay(1 / rate)`. A frame-quantized delay can never produce
more than **one volley per frame**, so the authored rate was silently `min(rate, framerate)`:

| authored rate | 60 fps | 30 fps |
|---|---|---|
| 30 volleys/s (old) | 30 ✔ (33 ms ≈ 2 frames, correct by luck) | 30 ✔ |
| 60 volleys/s (naive) | **60**, but only exactly — 16.7 ms *is* one frame | **30** ✘ — half rate |

So raising `firingRate` past ~30 without fixing the loop would have handed 60 fps players double
the fire rate (and, in Dog Fight, double the scoring rate) of 30 fps players.

Both loops now **owe fire in seconds and pay it off in whole volleys** (`owed += Time.deltaTime`,
fire `floor(owed / interval)`), capped at `MaxVolleysPerTick = 4` with the excess *dropped* rather
than carried — after a hitch the gun resumes firing, it does not discharge the stall as a burst.
At 90 volleys/s a 60 fps client fires 1–2 volleys per frame and a 30 fps client fires 3; both put
the same rounds downrange. The cap sustains the full rate down to ~23 fps.

### 3. The cone

`GunSpreadMath.HalfAngleDegrees` — flat zero through the onset window, then linear, then hard
capped:

```
half-angle(t) = clamp( (t − onset) × growth , 0 , max )
```

`GunSpreadMath.Perturb` deflects each round to a point inside that cone, sampling the deflection
as `max × u^bias`. At the shipped **bias 0.5** that is *uniform over the cone's disc*: the whole
danger zone saturates evenly rather than piling every round in the middle. (Bias 1.0 is the
authored alternative — a dense core with a thin halo, so the thing you are aiming at still soaks
most of the fire. It is one field if the even fill reads as too loose in play.)

**One roll per ROUND, not per volley** — the two muzzles scatter independently, which is what
makes the stream a widening cone rather than two widening lines.

**It does not touch `UnityEngine.Random`.** The deflection is a pure integer-hash of a per-vessel
shot serial, for two reasons: the global RNG stream is shared state that deterministic systems
seed (`Random.InitState` for the HexRace track), and a gun drawing from it 120×/s would make
their output depend on how long someone held a trigger; and a hash keeps peers that agree on the
shot count agreeing on where the shot went, which matters for the turret's locally-spawned
prisms. The serial is **monotonic across the session** and deliberately *not* reset per hold —
resetting it would make every trigger pull replay the same deflection sequence, which is a
learnable pattern rather than a stochastic cone.

Note the cap is an **angle**, so miss distance scales with range. A Sparrow at SPACE 0 shoots
~72 u and groups within ~5 u; at SPACE 10 it shoots ~645 u and groups within ~45 u. That is
correct — you are shooting nine times further.

---

## Reset semantics, and the one subtlety in them

Releasing the trigger resets accuracy **completely**, so a release-and-re-pull always buys the
whole onset window back. The reset is deferred by exactly one frame (`GunSprayAccuracy.LateUpdate`)
for one specific reason:

> Toggling the Turret Stance mid-hold makes `SparrowModeSwitchingFireSO` **stop one fire action
> and start the other, synchronously, in the same call stack**. Without the deferral that internal
> hand-off is indistinguishable from a trigger release and would hand the pilot a free accuracy
> reset for flicking stance.

`ReleaseHold()` arms the reset; a `BeginHold()` arriving in the same frame disarms it. The fire
loops run at `PreLateUpdate`, so a real release still lands before the next volley — the deferral
is invisible in play. `BeginHold` is idempotent for the same reason: taking the hold over mid-press
refreshes the profile without restarting the clock.

## The turret stance sprays too — because a turret shot IS a bullet

`SPARROW_TURRET_STANCE.md`'s parity doctrine is unchanged and this pass extends it rather than
carving an exception: *"a turret shot is a bullet — you just see a prism flying, and where the
bullet would have been destroyed the prism stays."* Spread changes where the round goes, so the
prism goes there too. The turret authors **no cone of its own** — `FullAutoBlockShootActionSO.Spread`
forwards `bulletAction.Spread`, exactly as `FireRate`, `FlightTime` and `ResolveSpeed` already do.

The deflection is composed onto the muzzle **pose** (`Quaternion.FromToRotation`), never rebuilt
with `LookRotation`: a turret prism's long axis *is* the shot, so re-referencing roll to world up
would visibly twist every prism.

A held turret burst therefore lays a **scattered volume** of permanent mass instead of a line —
which is a better wall, and the same mechanic the bullets get.

## The fourth haptic feel

The escalating buzz is a deliberate exercise of `Docs/HAPTICS.md` ▸ "Adding / changing a feel"
(dedicated method + extended gate, never the silenced legacy API). It is the game's only
**continuous** feel, which is exactly why it sits at the bottom of the priority order:

```
alert  >  punish  >  skim  >  spray
```

Everything suppresses the spray; the spray suppresses nothing. Being interruptible costs it
nothing — the next pulse is milliseconds away — and it means adding a texture did not make the two
feels the policy is built around any less legible.

Both the **strength** (0.15 → 1.0) and the **cadence** (100 ms → 45 ms) climb with the cone, so it
reads as a gun winding up rather than a constant hum. Local human pilot only: remote players, AI
dogfighters and the Menu_Main autopilot all fire and none of them may buzz your device.

## Files

| File | Role |
|---|---|
| `_Scripts/Utility/GunSpreadMath.cs` | The pure cone math — ramp, hash-sampled deflection, roll-preserving `DeflectionOf`. No Unity state, no global RNG. |
| `R_VesselActions/Data Containers/GunSpreadProfile.cs` | The authored profile (cone + haptic ramp). Serialized on the bullet action. |
| `R_VesselActions/Executors/GunSprayAccuracy.cs` | Per-vessel hold state, the spread clock, the haptic ramp, and the deferred reset. |
| `R_VesselActions/Data Containers/FullAutoActionSO.cs` | Owns `Spread` and `ResolveGrowthFactor`/`GrowthFactorForLevel`; hands the accuracy component to its executor. |
| `R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs` | Adopts `bulletAction.Spread` — the turret authors no cone. |
| `R_VesselActions/Executors/FullAutoActionExecutor.cs` | Accumulator cadence + per-round deflection for the bullets. |
| `R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs` | Same for the turret, plus the roll-preserving shot rotation. |
| `Controller/Projectiles/Gun.cs` | `FireGun(..., aimDirection)` — the gun is *handed* a direction; it owns no spread policy and rolls no dice. |
| `Controller/Managers/PrismSpatialIndex.cs` | `QuerySegment` — the swept counterpart of `QuerySphere`, plus the public `DistanceToSegmentSq` metric. |
| `Controller/Projectiles/Projectile.cs` | `sweptPrismDetection`, `SweepPrismsAlong` (nearest-first dispatch, contact-point repositioning), `CacheSweepRadius`; `ApplyFlightGrowth` + `CacheTransformRole` + `ChargeFieldLocalScale` (round 4). |
| `Controller/ImpactEffects/Impactors/ImpactorBase.cs` | `AcceptImpacteeFromSweep` + `IsSweepDispatch` — the swept analogue of the shell tier's entry point. |
| `Controller/ImpactEffects/Impactors/ProjectileImpactor.cs` | Suppresses the trigger's prism case when the sweep owns it. |
| `Controller/IO/HapticController.cs` | `PlaySpray(strength01)` + the extended gate + the buzz clip. |
| `_Scripts/Tests/Editor/GunSpreadMathTests.cs` | Ramp, cap, cone containment, pole safety, determinism, distribution, roll preservation. |
| `_Scripts/Tests/Editor/SparrowRoundGrowthTests.cs` | The MASS growth curve: anchors, extrapolation, linearity, flight clamping, and that the charge shell IS the hit volume (round 4). |
| `_Scripts/Tests/Editor/PrismSweptQueryTests.cs` | The point-to-segment metric: endpoint clamping, degenerate steps, and the shipped mid-step geometry PhysX was missing. |
| `_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset` | The shipped numbers. |
| `_Prefabs/Spacevessels/Sparrow.prefab` | `GunSprayAccuracy` executor + resized pools. |
| `_Graphics/Materials/Graphs/ProjectileChargeField.shader` + `.hlsl` | The self-driven charge shell — the forcefield-crackle language with `_Time` + the model matrix as its driver instead of a per-frame property block. |
| `_Graphics/Materials/ProjectileChargeFieldMaterial.mat` | The one material every round in the match batches through. Neutral blue + `EnvironmentColors.Danger` red. |
| `_Graphics/Materials/SparrowProjectileMaterial.mat` | The dart's own pale-blue material — a variant of `BlueSpreadFresnelMaterial`, split off `DangerProjectileMaterial` because that one is shared by five prefabs. |
| `_Prefabs/Projectile/SparrowProjectile.prefab` | `sweptPrismDetection: 1`, the `ChargeField` child wired to `Projectile.chargeField`, the halved model scale `(0.75, 0.75, 20)` and its own material (round 4). |
| `_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab` | `sweptPrismDetection: 1`. Its carried `ProjectileCollider` has no renderer, so growth still scales its transform and it carries no shell. |

## Tuning knobs

Everything that moves **both** fire modes lives on `FullAutoAction.asset`:

| Knob | Shipped | Effect |
|---|---|---|
| `firingRate` | **90** | Volleys/s for guns **and** turret. The single lever for volume of fire — and for the turret's permanent-mass rate. |
| `growthFactorAtRestingMass` | **3** | How many times its launch cross-section a round swells to by the end of its flight at resting Mass. |
| `growthFactorAtFullMass` | **6** | The same at Mass 10; the curve is linear in level and extrapolated to [-5, 15]. |
| `spread.onsetSeconds` | **0.12** | Grace window of perfect accuracy at the start of every pull (~11 volleys / 22 rounds). Size it to the burst length that should stay surgical. |
| `spread.growthDegreesPerSecond` | **1.0** | How fast the cone opens. Full at `onset + max/growth` ≈ **1.62 s**; only 0.88° after a full second. |
| `spread.maxHalfAngleDegrees` | **1.5** | The cap (≈1.9 u radius at the SPACE-0 range of 72 u). Raise it and held fire starts missing what you aimed at; drop it to 0 to disable spread entirely (sanctioned opt-out). |
| `spread.distributionBias` | **0.5** | 0.5 = uniform over the disc (even saturation). 1.0 = dense core + thin halo. |
| `spread.hapticFloor01` | **0.15** | Buzz strength before any accuracy is lost — above zero so the gun is felt from round one. |
| `spread.hapticIntervalAtRest` / `AtMaxSpread` | **0.10 / 0.045** | Pulse cadence at each end of the ramp. Keep the max-spread value above ~0.04 s: NiceVibrations holds one clip at a time, so pulses closer than the clip just cut each other off. |

The charge shell's own dials are **not** here — they live on
`_Graphics/Materials/ProjectileChargeFieldMaterial.mat`, because they are a look, not a weapon
parameter, and because the shell is a general `Projectile` capability rather than a Sparrow one:

| Knob | Shipped | Effect |
|---|---|---|
| `_ArcSeeds` / `_ArcDensity` | **3 / 5** | Simultaneous discharge points, and branches per discharge. The inner loop, so this is also the cost dial — measured 4.2 FBM evals/fragment at these values. |
| `_ArcSharpness` | **0.12** | Arc width in radians. Lower reads as lightning, higher as a smear. |
| `_ArcIntensity` | **1** | Arc brightness. At the shipped values arcs are ~34% of the shell's light and the rest is the rim. |
| `_CoreThreshold` | **0.75** | Where the arc stops being neutral blue and becomes danger red. **Lower it and the two colours blend into magenta** — this is a separation dial, not a blend dial. Measured hue split at 0.75: 78% blue / 15% red / 7% magenta. |
| `_CrackleColorA` | **`EnvironmentColors.Danger`** | The hot core. Taken verbatim from `OriginalColorSetSO` so a round's core is the same red as the arena's danger mass. |
| `_CrackleColorB` / `_FresnelRimColor` | **(0.10,0.35,1) / (0.25,0.55,1)** | The neutral blues — the arc body and the always-on shell. |
| `_CrackleRate` | **6** | Discharges per second per seed. Also what spreads consecutive volleys' phases apart — drop it far and a burst starts flashing in unison. |
| `_FresnelRimIntensity` | **0.18** | The always-on rim. **This is the see-through dial**: it sets the shell's floor alpha (0.036 at the muzzle, 0.079 fully charged). Raise it far and an enormous round starts hiding the arena. |
| `_ChargeReferenceRadius` | **4.95** | The hit radius that reads as fully charged — the Mass 10 end-of-flight radius. Absolute and fleet-wide by design: the same hit radius must look the same on any round. |
| `_ChargeFloor` | **0.35** | What a just-launched round gets. Deliberately not 0 — a round must always show the volume it deletes. |
| `_PhaseByRadius` | **1.7** | Radians of animation phase per world unit of radius; the per-round decorrelation. Zero strobes the whole volley together. |

Pool sizes on `Sparrow.prefab` (resized for the doubled rate — the fire rate is the only reason
they are what they are):

| Pool | was | now | why |
|---|---|---|---|
| bullets (`ProjectilePoolManager`) | 25 / 100 / 25 | **90 / 320 / 90** | 180 rounds/s × 0.3 s lifetime ≈ 54 live at once. |
| turret prisms (`BlockProjectilePoolManager`) | 40 / 200 / 90 | **120 / 600 / 260** | Anchored prisms are **never returned**, so every shot past the buffer is a fresh `Instantiate`. |

## Costs this pass takes on, deliberately

- **Turret stance now lays ~180 prisms/s** of permanent world mass (was 60), at ~360 volume/s at
  base scale before the MASS stretch. That is the documented price of "the same rate as its
  bullets", and `firingRate` remains the single lever — **do not** add a turret-only divisor,
  which is exactly the drift the shared-cadence pass closed. Judge it against the host cell's
  phase ladder in play.
- **Dog Fight pace changes a LOT, and by more than the rate alone.** A bullet hit scores 1 against
  a 120-point target; rounds downrange are 3× the pre-branch figure AND each one now actually
  tests its whole path, so landed hits per second rise by considerably more than 3×. Expect the
  point target to need raising. It is authored (FrogletTools ▸ Game Modes ▸ End Game Conditions,
  `GetDogFightPointTarget`), so retuning it is one field and needs **no** code change.
- **Swept queries cost one `QuerySegment` per live bullet per frame** (~54 concurrent at the
  shipped rate). The segment's AABB is thin, so each walks only a handful of 8 m buckets — but it
  is new per-frame work in the projectile path and worth a profiler glance under a sustained hold.

## In-editor verification

Scene: `MinigameDogFight` (best — it has other pilots to shoot at) or `MinigameWildlifeLiberation`.
Sparrow, fire on input 1.

> **The haptic half needs a gamepad or a device.** On a bare desktop editor there are no motors,
> so "I feel nothing" carries no information about whether the ramp is wired. Connect a gamepad
> (the `GamepadRumble` path drives Input System motors) or run on device. The **cone** is fully
> visible on desktop — the tracers fan out — so the spread mechanic can be judged without one.

1. **Tap accuracy.** Tap the trigger repeatedly. Every burst must be a tight line — no visible
   fan at all. This is the onset window; if short taps spread, `onsetSeconds` is too small.
2. **The cone opens.** Hold the trigger on a distant wall and watch the impacts: a point that
   grows into a widening circle over ~1.4 s, then **stops growing**. If it never stops, the cap
   is not being applied.
3. **Release resets.** Hold until fully open, release for a fraction of a second, re-pull. The
   first rounds of the new pull must be dead-on again.
4. **Stance flip does NOT reset.** Hold fire while flying, open the cone fully, then toggle
   Turret Stance (input 6) **without releasing the trigger**. The prisms must start laying at the
   *open* cone — if they come out in a tight line, the deferred-reset hand-off has regressed.
5. **Turret prisms scatter.** Stopped, hold fire: prisms must anchor in a scattered volume, not a
   line — and each one must still point along its own flight (no visible twist/roll on the long
   axis).
6. **Rate.** 180 rounds/s should read as a solid stream. Then **cap the editor to 30 fps**
   (Game view ▸ or `Application.targetFrameRate = 30`) and confirm the stream looks the same
   density — that is the accumulator working. Before this pass it would have halved.
7. **Haptic ramp** (gamepad/device). Hold the trigger: a light buzz from the first round that
   climbs in strength *and* rate for ~1.6 s, then holds steady at the cap. Release → silence.
8. **Haptics stay legible.** While spraying, ram a prism with the hull — the punish **thud** must
   cut cleanly through the buzz. Confirm the buzz never plays for a remote player's Sparrow, an
   AI dogfighter, or the Menu_Main autopilot.
9. **Settings.** Haptics off / level 0 in Settings → the buzz stops with everything else.
10. **No hitching under a long hold.** Both fire modes, 10+ second holds, profiler open. The pools
    were resized for this; if the turret still spikes, raise `bufferSizeTarget` / `maxAddsPerFrame`
    on the Sparrow's `BlockProjectilePoolManager` further.
11. **Console clean.** No `[FullAutoActionExecutor]` / `[FullAutoBlockShoot]` / `[PrismClock]`
    errors during a sustained hold.
12. **MPPM two-client.** Both clients see a spraying Sparrow. Turret prisms land in *approximately*
    the same places on both — see Follow-ups; exact agreement is not expected today.
13. **Asset import.** `FullAutoAction.asset` shows the new **Accuracy** foldout with the shipped
    numbers, and `Sparrow.prefab`'s `VesselActions` node has a **GunSprayAccuracy** child listed in
    `ActionExecutorRegistry._executors` (5 entries).

## Follow-ups

- **Cross-peer turret prism placement.** The deflection is deterministic in the shot serial, so
  peers agree exactly as long as their shot counts agree — but the loops run on each peer's own
  clock, so counts drift and the spread makes that drift *visible* (up to 4°) instead of
  sub-degree. This is the same open item `SPARROW_TURRET_STANCE.md` already records ("the turret's
  prism spawning is not networked at all today"); it settles when the stance becomes
  server-authoritative, not before.
- **No HUD readout.** The cone is visible in the tracers and audible in the hands, but there is no
  on-screen indicator. If one is wanted, the natural home is a ring on the **Space** ability icon
  (Pulsefire Cannons) — which makes it a live-gauge icon and pulls in the rule-9 obligations
  (`tintIconOnUpgrade = false` + a `SetAbilityUpgraded` override re-anchoring rest scales).
  Deliberately out of scope here.
- **Vessels and mines still tunnel.** Swept detection covers prisms only. A hull is a much bigger
  target so it matters far less, but a Sparrow round crossing 6.25 u per frame can still slip past
  a vessel at a glancing angle — and in Dog Fight that is a missed point. Generalizing the sweep to
  the whole impact system (and to every other fast projectile in the game, not just the Sparrow's)
  is the natural next step; it is opt-in per prefab precisely so that can be done deliberately.
- **`placementImmunitySeconds` is now doing more work again.** Round 6 noted 0.2 s was probably too
  long once the hit sphere shrank; at 120 shots/s the shot-vs-shot spacing it guards is tighter
  again. Re-judge it in play rather than assuming either direction.
- **`MaxVolleysPerTick = 4` is a code constant, not an authored field.** It only binds below
  ~23 fps at the shipped rate. If a rate above ~240 volleys/s is ever wanted it must move with it —
  hoist it onto `GunSpreadProfile` at that point rather than raising it blind.
