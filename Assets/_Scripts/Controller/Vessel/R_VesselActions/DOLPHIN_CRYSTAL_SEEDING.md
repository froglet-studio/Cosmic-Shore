# Dolphin — passive crystal seeding, and the Echo Sight that aims the blast

Design owner: Garrett. Map: `Assets/Resources/ElementalAbilityMaps/Dolphin.asset` — that asset and
this file are the record. The energy economy, the drift boost and the four gauges are the sibling
document, `DOLPHIN_ENERGY_ECONOMY.md`; this one covers the two abilities that changed when the
right trigger was freed.

---

## 0. What changed and why

The Dolphin held the right trigger to preview and plant a team crystal. That spent the vessel's
only free input on an ability whose interesting part — *where the crystal is* — the pilot was
already choosing by flying there. Meanwhile its signature ability, the crystal-impact cone
("Echo Obliteration"), had no input at all and no way to be aimed: the pilot banked its gape with
every skim and could read that gape only as an ANGLE, off the hull's jaws or the HUD's jaw icon,
with no way to know what the angle actually covered out in the world.

So the two swapped places:

| element | before | after |
|---|---|---|
| **Charge** | Crystal Seeding — hold RT to preview, release to plant | Crystal Seeding — **passive**, seeds into the cytoplasm on a loop |
| **Space** | Cone Blast — passive, fires on crystal impact | Echo Obliteration — the same blast, **plus the sight on RT** |

The Echo Sight originally also pushed the camera into a zoomed first-person view. **That half was
cut** (2026-08-14, same day) — the highlight alone carries the ability, and dropping the zoom also
dropped the only reason to touch the speed tunnel's FOV at all. See §2.

Charge still owns the recharge; Space still owns the reach. Neither element→ability binding moved —
only which of them carries an input.

> **Superseded 2026-08-17 by §8.** The element→ability bindings HAVE since moved: Charge took the
> Echo Sight and the blast's thickness, Mass took crystal seeding, Space narrowed to reach, and the
> HUD row was re-cut to match. §1 and §2 below still describe the mechanics correctly — read
> "Charge owns the seeding" as "Mass owns the seeding", and see §8 for everything that changed.

---

## 1. Charge: passive crystal seeding

A cooldown runs continuously. Each time it completes the Dolphin seeds a **team** crystal at a
random point in the containing cell's **cytoplasm** — the shell between the nucleus surface and
the membrane — and the cooldown restarts immediately. There is no input, no preview, and nothing
carried.

The seeded crystal is the Dolphin's own ammunition: it is what the vessel later flies into to
release Echo Obliteration. **So the seeding rate is the blast's tempo**, which is what makes
Charge's cooldown scaling matter.

### The placement rules, and which are load-bearing

| rule | why |
|---|---|
| Radius drawn **volume-uniformly** across the band (`cbrt(lerp(inner³, outer³, u))`) | A shell's available space grows as r². A uniform-in-radius draw crowds every seeding against the nucleus and leaves the outer cytoplasm — most of the actual volume — nearly empty. Same rule the flora planting band follows (CLAUDE.md ▸ Rampage §27.2). |
| Inner edge **clamped outside the nucleus**, whatever `bandInnerFraction` says | Nucleus mass is the cell's territorial claim and a fauna sanctuary. Seeding ability crystals into it would make the sanctuary the place to farm. |
| At the live cap the clock **pauses**, never culls | Not creating mass is allowed; aging it out is not (CLAUDE.md ▸ *Mass is conserved*). `maxLiveSeeded` bounds the field by declining to add, and a planted crystal is only ever removed by being collected. |
| No cell to measure → seed in a ball around the vessel | Freestyle transit and tool scenes have no membrane. The ability degrades to doing something rather than silently stopping. |

**This is not the omni-crystal respawn volume.** `CrystalManager.GetAnchorlessSpawnRadius` is
LOCKED to the nucleus (CLAUDE.md ▸ Rampage §27.3) because the nucleus is the visible marker of "the
middle" that every mode teaches players to contest. That governs the **cell's** own respawning
crystal. This is a vessel ability planting its own team-locked crystal, and it deliberately seeds
outside the nucleus for the reason in the table above. The two rules agree; they are about
different crystals.

### The trap this ability walks into

**A passive ability is bound to no input event, so `CollectBoundActions` can never resolve its SO.**
The executor's original lazy resolution swept every `InputEvents` value looking for its own action —
correct while the ability was on the right trigger, and dead the moment it became passive. The SO is
now wired **directly** on the executor (`config`), with the binding sweep kept only as a fallback for
a vessel that still lists the action against an input. A missing wire is visible in the inspector.

### Twin Seed (Charge L5) — RETIRED 2026-08-17

The upgrade used to raise a **carry** limit — meaningless once nothing is carried — and then became
a **yield** of two crystals per cycle. Both are gone: the ability moved to Mass, whose L5 changes the
crystal's TIER rather than its count (§8). One crystal per cycle at every level.

---

## 2. Space: Echo Obliteration, and the Echo Sight

Hold the right trigger and every prism standing inside the blast's destruction volume lights up.
Release and the highlight fades away. **It fires nothing and it moves nothing** — the blast still
goes off when the Dolphin strikes a crystal, and the camera is left entirely alone. The sight only
makes the shape legible so the pilot can choose which way to be pointing when they take the
crystal.

### It touches nothing but photons

The whole ability is `PrismDestructionSight`'s global uniforms, published while the trigger is
held. No camera write of any kind, no speed change, no input mute, nothing replicated.

That is a deliberate narrowing. The first cut of this ability also eased the camera into a zoomed
first-person shot down the blast axis, which meant moving the field of view — and FOV is owned
fleet-wide by the speed tunnel (`Docs/SPEED_TUNNEL.md`), a LOCKED law with exactly one sanctioned
hold. Composing with it cleanly was possible (the ability declared a *home* and the tunnel stayed
the single writer), but it cost the law a new public surface for one vessel's view effect. **The
zoom was cut instead**, and the tunnel is untouched.

Worth keeping if a zoom is ever revisited, because the failure modes are silent: an ability that
writes `Camera.fieldOfView` directly is overwritten every frame while the tunnel is engaged, and
when the tunnel *engages* it captures whatever FOV it finds as the home to restore later
(`Apply`: `if (cam != _appliedCamera) _homeFov = cam.fieldOfView`) — so a zoom live at that instant
is baked in permanently and the player never gets their FOV back. A zoom must therefore move the
tunnel's *home*, never the camera.

### The highlighted volume is not re-derived

`VesselExplosionByCrystalEffectSO.TryResolveBlastVolume` → `ExplosionHelper.TryResolveConicVolume`
builds it from the **same** authored scales, the **same** energy read and the **same** Space
multiplier the detonation uses, and returns it as a `BlastVolume` in the form the Burst sweep tests
against. `PrismDestructionSight.hlsl` then transcribes `AOEConicSweepQueryJob.Execute` literally,
capsule-segment clamp included.

That is deliberate: a targeting aid that lies is worse than none, and a preview with its own copy of
the arithmetic would drift the first time anyone retuned a scale. The volume is a **capsule sweep**,
not a circular cone — narrow across the beam at every charge, wide across the gape in proportion to
banked energy — so the sight shows a fan, which is the whole thing the pilot could not previously
see (`DOLPHIN_ENERGY_ECONOMY.md` §1).

### Why the highlight is a global uniform

"Is this prism inside the blast" is live, per-frame, per-prism data — it changes as the ship turns
and as the meter fills — so it can never be a per-prism stamp, and running the spatial index's conic
sweep every frame purely to tint would be exactly the per-prism CPU pass the clock-material law
exists to prevent. Five `Shader.SetGlobal*` calls per frame, zero per-prism work: the sanctioned
shape (`Docs/PRISM_ANIMATION.md` §1, §4.7), and the same contract
`PrismOcclusionCorridor` runs on.

It **adds** light rather than tinting. Replacing colour on a Jade prism lands in the domain
palette's space and reads as "this prism changed team"; adding says "this one is lit up", which no
tier's palette means. The prism graphs are Unlit and carry no Emission block, so
additive-into-BaseColor is how emission is expressed there. It composes with the occlusion corridor
for free — the corridor dissolves *coverage*, not colour, so a highlighted prism inside the corridor
thins out like its neighbours instead of punching through the ship.

---

## 3. What the HUD does now

> **Superseded 2026-08-17 by §8 and §12.** Seeding moved to the MASS slot, the pips are deleted
> with Twin Seed, and the Charge slot now carries the blast profile. The table below describes the
> 2026-08-14 arrangement and is kept only as the record of that pass.

The Charge slot's art is unchanged; only what it means moved.

| | before | after |
|---|---|---|
| main icon fill | 1 while a crystal was in hand, else the recharge | **always the recharge** — nothing is ever in hand |
| pips | one per SAVED crystal beyond the first | one per EXTRA crystal in the **cycle** (so Twin Seed still IS the pip appearing) |
| the punch | a slot finishing its recharge | a **seeding firing** — edge-detected off `SeedCount` |

The punch matters more than it did: the pilot gives no input for this ability and may be facing
anywhere when it fires, so the slot flashing is the only notification that a crystal just went out.

The Space slot is unchanged (blast icon + prism tally). It now also has a control hint, since its
ability finally has an input — `InputDeviceIconSetSwitcher.BindHintsToAbilities` derives that
placement from the binding, so nothing is hand-positioned.

---

## 4. Files

| role | file |
|---|---|
| Seeding config | `Data Containers/DeployTeamCrystalActionSO.cs` |
| Seeding executor | `Executors/DeployTeamCrystalActionExecutor.cs` |
| Sight config | `Data Containers/EchoSightActionSO.cs` |
| Sight executor | `Executors/EchoSightActionExecutor.cs` |
| Highlight publisher | `_Scripts/Utility/PrismDestructionSight.cs` |
| Highlight shader | `_Graphics/Materials/Graphs/PrismDestructionSight.hlsl` |
| Graph splice tool | `Tools/Shaders/wire_prism_destruction_sight.py` |
| Shared blast geometry | `ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs` (`BlastVolume`, `TryResolveConicVolume`) |
| HUD | `UI/View/DolphinVesselHUDView.cs`, `Data Containers/DolphinVesselHUDController.cs` |
| Assets | `_SO_Assets/VesselActions/Dolphin/DeployTeamCrystalAction.asset`, `EchoSightAction.asset` |

## 5. Tuning knobs

| knob | asset | effect |
|---|---|---|
| `cooldown` / `minCooldown` | `DeployTeamCrystalAction` | Seeding tempo, and therefore blast tempo |
| `bandInnerFraction` / `bandOuterFraction` | " | Where in the cytoplasm crystals land (0..1 across nucleus→membrane) |
| `maxLiveSeeded` | " | How many of this Dolphin's crystals may stand at once (0 = uncapped) |
| `cooldownMultiplierAtFullMass` | " | Mass's grip on the recharge (was `...AtFullCharge`; `[FormerlySerializedAs]` carries old assets over) |
| `crystalPrefab` / `upgradedCrystalPrefab` | `Dolphin.prefab` ▸ `DeployTeamCrystalActionExecutor` | The omni seed and the Mass-5 team seed |
| `_coreMultiplierAtRestCharge` / `_coreMultiplierAtFullCharge` / `_minCoreMultiplier` | `DolphinVesselExplosionByCrystalEffect` | Charge's grip on the blast's THICKNESS |
| `vesselHighlightFadeSeconds` / `vesselHighlightGain` | `Dolphin.prefab` ▸ `EchoSightActionExecutor` | Pilot Echo's bloom time and brightness |
| `transitionSeconds` | `EchoSightAction` | Highlight fade in/out |
| `highlightStrength` | " | Peak highlight |
| `PRISM_SIGHT_COLOR` / `_GAIN` / `_EDGE_POWER` / `_CORE_FILL` | `PrismDestructionSight.hlsl` | The highlight's look |

---

## 6. In-editor verification

**Play-tested by Garrett on 2026-08-14** — seeding, the highlight and the HUD all confirmed
working in the editor. The table is kept as the regression list for the next person to touch
this. Two rows remain unverified because one editor cannot reach them: the **MPPM two-client**
row, and the **live-cap** row (~4 minutes of uninterrupted seeding at the shipped 30 s cooldown).
Mirrored in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

Run **FrogletTools > Vessels > Audit Vessel Ability Rows** first — Dolphin should still report map
complete, 4/4 icons, order ✅. Then play Menu_Main and enter freestyle on the Dolphin.

| check | expect |
|---|---|
| **Compile** | No errors. The graphs need a Unity import pass to regenerate their shaders. |
| idle in a cell for one cooldown | a team crystal blooms in somewhere in the cytoplasm; the Charge slot punches |
| watch several cycles | crystals land spread through the shell, not clustered near the nucleus |
| fly to the nucleus | no seeded crystal is ever inside it |
| let `maxLiveSeeded` fill | seeding stops, the recharge fill sits at 0, and **no crystal disappears** |
| collect one | seeding resumes |
| Mass to level 5 | seeded crystals arrive in your DOMAIN's colours instead of the lime CTA, and a rival cannot collect them; the Mass slot's icon shifts to the team colour |
| Charge to level 5, hold RT near another vessel | a vessel inside the blast volume brightens in its OWN domain colours and fades back out as the cone sweeps off it |
| raise/lower Charge, hold RT | the Charge slot's generated profile fattens/thins; banking energy grows its overall extent |
| hold and release RT | the Charge profile crosses grey -> white and back; it is a solid capsule at every charge, never a bowtie or hollow wedges |
| **hold RT** | prisms inside the blast volume light up warm; the camera does **not** move and the FOV does **not** change |
| release RT | the highlight fades out over ~0.3 s (it must not snap off) |
| hold RT while accelerating hard | the speed tunnel behaves exactly as it always has — the sight is not involved in it at all |
| skim to fill energy, hold RT | the highlighted volume opens as a **fan** — wide across the jaw plane, narrow across the beam |
| ram a prism (halves energy), hold RT | the fan narrows to match |
| take a crystal while sighting | the blast destroys what the sight was showing |
| MPPM, two clients | a remote Dolphin holding RT shows nothing unusual — the sight is local-only |
| swap vessel while sighting | no stuck highlight (the globals are cleared on re-init) |

**Two things I would check first if something looks wrong**, because they are the likeliest failures
and both are silent: whether the graphs recompiled at all (an unimported graph shows no highlight and
no error), and whether `blastEffect` is actually assigned on the Dolphin's `EchoSightActionExecutor`
(unassigned = holding RT does nothing whatsoever).

## 7. Follow-ups

- **Seeded crystals are local-only.** `TeamCrystal.prefab` carries no `NetworkObject`, so the clock
  runs for the local pilot only — matching the previous hold-to-plant scope exactly, which produced
  an owner-only crystal too. Networked seeding wants crystal network sync first. Until then, AI and
  remote Dolphins seed nothing.
- **The sight ignores friendly fire.** It highlights everything geometrically inside the volume,
  including own-domain mass that Space L5 "Clean Blast" would spare. Telling the truth about that
  needs the prism's own domain in the shader, which the per-domain material split does not currently
  expose as a uniform.
- **`Crystal.ApplyDomainPreview` is now unreferenced** — it existed for the retired ghost preview.
  Left in place as a public `Crystal` API rather than deleted in a vessel branch.
- **`CameraManager.SetNormalizedCloseCameraDistance` is a no-op** — its whole body is commented
  out. Nothing here depends on it any more (the zoom was cut), but it was found while wiring the
  first cut and is worth deciding on: repair or remove.
- **A zoomed sight view is still an open idea, not a rejected one.** It was cut to keep the speed
  tunnel untouched, not because it played badly — nobody has played it. If it comes back, §2 records
  the one way it can be built safely.

---

## 8. 2026-08-17 — the map re-cut around ONE weapon

Design owner: Garrett. `Assets/Resources/ElementalAbilityMaps/Dolphin.asset` is the record.

The Dolphin has essentially one offensive act: bank energy by skimming, fly into a crystal,
release a cone. The previous map spread four loosely-related mechanics across the elements (a
recharge, a trail, a blast, a fill rate); this one gives each element **one orthogonal dimension
of that single act**, so the row reads left to right as the whole weapon.

| element | ability | parameter | L5 |
|---|---|---|---|
| **Charge** | Echo Sight (RT) | blast capsule **THICKNESS**, `0.75×` → `1.5×` | **Pilot Echo** — vessels in the volume light up |
| **Mass** | Crystal Seeding (passive) | recharge `×0.5` at level 10 | **Claimed Seed** — omni seed → team seed |
| **Space** | Echo Obliteration (crystal impact) | **REACH** `×2` at level 10 | **Clean Blast** — spares own domain |
| **Time** | Charge Fill Rate (drift) | boost fill `×1.5` at level 10 | **Live Current** — 3× energy on danger skims |

### Charge → thickness, and the first un-anchored elemental multiplier

`ElementalScaling.Multiplier` anchors at exactly 1 at the resting level, so an element can only
ever ADD to a vessel's authored baseline. That is the right default and stays the default — but it
means an element can never own a parameter's whole RANGE, only its upside. Charge → thickness is
specified as 0.75× at rest and 1.5× at level 10, so it needed an explicit resting endpoint:
`ElementalScaling.MultiplierFromRest(status, element, atRest, atFull, minMul)`.

The consequence is deliberate and worth stating plainly: **the authored `_coreExplosionScale` is
now what a MID-Charge Dolphin fires, and a fresh pilot's beam is thinner than the asset draws.**
That is a real baseline change, not a bug.

What Charge does NOT do is make the blast bigger. `halfLength + radius` is always `maxScale / 2` —
the resource sets the profile's total extent across the gape and Charge only redistributes it. So
banking energy grows the blast and raising Charge **rounds it out**, trading a long thin fan for a
short fat capsule. Energy → gape · Charge → thickness · Space → reach: three dimensions, three
owners, and none of them can steal what another one bought.

### Pilot Echo (Charge L5) — the sight extended from mass to pilots

While the trigger is held, every VESSEL standing inside the same volume brightens in its **own
domain's colours**. Nothing is recoloured: `EchoSightVesselHighlighter` drives `_ColorMultiplier`,
the brightness lever `VesselGraph.shadergraph` already exposes and `VesselAnimation` already uses
for its boost glow, lerping from each material's **own** authored value. So an engine that rests at
5 brightens from 5, a hull that rests at 1 brightens from 1, and a Ruby pilot can never read as
Jade.

Three things about it are load-bearing:

- **The predicate is shared, not re-derived.** `BlastVolume.Contains` is the CPU transcription of
  the test `AOEConicSweepQueryJob`, the capsule trigger and `PrismDestructionSight.hlsl` all run —
  clamp onto the cross-section's segment, *then* measure distance, which is what makes the ends
  round. It returns the same edge-weighted fill the shader uses, so a highlighted vessel and the
  highlighted prisms around it brighten together.
- **Per-vessel CPU is correct here and would not be on prisms.** The prism half of this sight is a
  global uniform because there are tens of thousands of prisms (`Docs/PRISM_ANIMATION.md` §4.7).
  There are at most a dozen vessels, they are already individually simulated, and this runs only
  while a trigger is held — so a MaterialPropertyBlock per renderer is the ordinary tool. The
  material is never cloned, and the block is written per material INDEX so a restore is exact.
- **Release is a restore, not a fade to a guess.** `HardReset` (disable, vessel swap, re-init)
  calls `ClearAll`, which drops every borrowed brightness immediately. A faded-but-unrestored
  highlight would leave a rival permanently over-bright with nothing left running to fix it.

Gated on `IsUpgradeActive(Element.Charge)`, not a raw level read: this is a thing other players see
on their own hull.

### Mass → seeding, and Claimed Seed

The recharge multiplier moved from Charge to Mass verbatim (`cooldownMultiplierAtFullMass`, with
`[FormerlySerializedAs]` so the shipped asset carries over). The L5 changed from a yield to a
**tier**: below Mass 5 the seed is an ordinary omni crystal, above it a team crystal.

Both halves of that gate move together and neither is decorative:

- `upgradedCrystalPrefab` swaps `Crystal.prefab` (`OmniCrystalImpactor`) for `TeamCrystal.prefab`
  (`TeamCrystalImpactor`), so the domain rejection happens inside the impact chain.
- `crystal.ownDomain` is stamped `Domains.Blue` for the omni seed and the pilot's domain for the
  team seed. That field IS `Crystal.CanBeCollected`'s gate *and* what
  `Crystal.ResolveActivationMaterial` paints from, so the crystal looks exactly as collectable as
  it is — the lime free-for-all CTA below the upgrade, the domain's crystal colours above it
  (`Docs/PALETTE.md` §2.2: crystal colour signals **who may collect**).

Resolved per seeding, never latched at init, so a Mass debuff immediately puts the pilot back to
planting crystals anyone can take.

### What Mass gave up

`trailVolume` is disabled and `massUpgradeShieldsTrail` is off on `Dolphin.prefab`. The Dolphin no
longer grows its drift prisms with Mass and no longer shields them. The machinery is untouched —
it is the Squirrel's Heavy Trail and the Squirrel still uses it — it is simply no longer wired
here.

### The HUD row, re-cut

Every slot now draws one dimension of the same weapon:

| slot | gauge | what moved |
|---|---|---|
| **Charge** | `BlastProfileGraphic` — a generated stadium mesh: the blast's cross-section, radius from Charge, extent from energy. Warms to the sight's own colour while RT is held | NEW. The band's old blast sprite is retired; `ProfileIcon` is now a transparent container with the generated profile as its child, the same arrangement `JawIcon` uses |
| **Mass** | the crystal recharge fill, tinted by the tier the next cycle will plant | moved from the Charge band; the two carry pips are **deleted** with Twin Seed |
| **Space** | the jaw pair (gape = energy) + the prism tally | jaws moved from the Time band; the tally moved onto its own row beneath them and was widened to 120px so a five-figure claim renders at full size |
| **Time** | the authored 11-step boost ring | moved from the Mass band |

The profile is procedural rather than an authored sprite ladder for the same reason the preview
volume is not re-derived: it is a continuous function of two live meters, and a sprite ladder would
quantize it and silently stop matching the blast the first time anyone retuned a scale. It is fed
by `VesselExplosionByCrystalEffectSO.TryResolveProfile`, which hands back radius, half-length and
the reference extent in one call so the icon can never mix a radius from one frame with a reference
from another.

The row wirer was re-cut to the same layout AND generalized to the whole fleet —
`VesselAbilityRowWirer` (`FrogletTools > Vessels > Wire Vessel Ability Row`). Nothing about the
row's geometry was ever Dolphin-specific, and three vessels still report 0/4 icons, so pointing
it at Manta/Rhino/Serpent builds their whole row from nothing. The Dolphin's gauges (profile
capsule, tallies, jaws, adopted boost ring) stay as a per-vessel step the generic pass calls for
this view type only.

### Files added

| role | file |
|---|---|
| Pilot Echo highlighter | `Executors/EchoSightVesselHighlighter.cs` |
| Blast profile icon | `_Scripts/UI/View/BlastProfileGraphic.cs` |
| Un-anchored elemental lerp | `ElementalScaling.MultiplierFromRest` |
| CPU volume predicate | `ExplosionHelper.cs` ▸ `BlastVolume.Contains` |
| Profile / reach readouts | `VesselExplosionByCrystalEffectSO.TryResolveProfile` / `TryResolveReach` / `MaxReach` |

---

## 9. 2026-08-17, second pass — colour becomes a language, and two things dropped

### The Charge profile goes grey → white

`ElementalBarsConfigSO` already owns a five-colour ladder that the petal flowers step through:
fire (−1) · **grey (0)** · **white (1)** · blue (2) · lime (3). Grey and white are therefore already
the HUD's words for *not in use* and *in use*, so the Echo Sight's profile reads from those two
rather than from a colour authored for this one icon. The warm cast the shader paints on the world
stays where it is — on the world; the cockpit says "engaged" in the vocabulary the rest of the HUD
uses.

Both endpoints keep a serialized fallback for a HUD with no config asset, but the config wins:
`ResolveProfileColors` is the only writer, and `Docs/PALETTE.md`'s rule applies — never author a
second copy of a palette colour.

### The Mass slot goes lime → the pilot's DOMAIN colour

The upgrade's whole point is that the seed becomes a *team* crystal, so the slot now says **which
team**. Below Mass 5 it shows the lime CTA; at Mass 5 it shows that domain's `DullCrystalColor`.

Two details are load-bearing:

- **`DullCrystalColor`, not `BrightCrystalColor`.** At the crystal shaders' fresnel power the dull
  colour paints ~93% of the crystal and the bright one is a ~2.5% hairline rim
  (`Docs/PALETTE.md` §2.2). The dull colour is what the pilot actually sees standing in the cell, so
  it is the one the icon must match.
- **Resolved live, never snapshotted.** It comes off `GameDataSO.ThemeManagerData.ColorSet` — the
  same path `MultiplayerHUD` and every other domain-tinted UI reads — and is re-read each push, so
  the freestyle domain-changer toy re-colours the slot with it. CLAUDE.md is explicit that domain
  must not be captured at component-creation time.

### The Space reach bar is dropped

The thin bar under the jaws is gone, along with `SetReachNormalized`, `TryResolveReach` and
`MaxReach`. The Space slot communicates **angle and amount** — the gape the next blast will carry,
and what the last one claimed — and nothing else. Reach only moves when the Space element moves, so
the bar was a near-static line competing for attention with two live gauges. Space still scales the
blast; it simply no longer has a gauge of its own, which is the correct trade for a slot that was
trying to say three things at once.

### The profile mesh was rendering a bowtie

`BlastProfileGraphic.OnPopulateMesh` measured each end cap's sweep from the ACROSS axis (`cos` on
across, `sin` on along). That put the first cap's last vertex at the far `+along` tip and the second
cap's first vertex back near the middle of the shape, so the outline **jumped straight across the
stadium** instead of walking its perimeter — and the centre fan then triangulated a bowtie with
hollow wedges, which is exactly what the icon looked like.

The fix is one continuous monotonic sweep from −90° to +270° measured from the `+along` axis, with
the cap centre switching at the halfway point. Both straight edges fall out of that switch for free.
Verified off-engine over five (L, R, rotation) combinations: the outline is convex and
non-self-intersecting in every case, its area is within 2% of the exact stadium at ten segments per
cap, and the largest step between consecutive vertices is exactly `2L` — the straight edge, which is
what proves there is no jump across the interior.

The general lesson, and the reason it was invisible until it was on screen: **a fan triangulation is
only as good as the outline's ordering**, and a mis-ordered outline does not fail — it renders a
plausible-looking wrong shape. A generated `MaskableGraphic` should be checked for a simple loop, not
just for "vertices in roughly the right places".

---

## 10. 2026-08-17, third pass — the highlight has to actually find a pilot

Three playtest findings, all of them cases where a change was *correct* and still did not work.

### The Mass slot rendered black

`DullCrystalColor` is authored **(0, 0, 0)** on Jade, Ruby and Gold in the live
`OriginalColorSetSO`. §9 picked it on the reasoning that the icon should wear the colour the crystal
wears, and that reasoning was sound — the near-black body with a bright fresnel rim is exactly what
makes a faceted domain crystal read as a dark gem. It is simply not a colour a UI element can be.

Fixed by adding **`SO_ColorSet.GetDomainSignalColor(domain)`** — the domain UI colour
(`TrailHighlightColor`) with its brightest channel driven to 1, hue and saturation intact — and using
it for the slot. Jade → (0.073, 1.0, 0.948), Ruby → (1.0, 0, 0.976), Gold → (1.0, 0.657, 0). It
returns white for an unauthored domain, because an accessor that can return black is an accessor that
can make a UI element vanish, and a vanished element reads as "not implemented". Full record and the
per-domain table: `Docs/PALETTE.md` §2.4.

### Pilot Echo was indistinguishable from the prisms

Reported from Rampage, which is the worst case by construction: its arena is a forest of ~9,800
cactus prisms, and the sight lights **all of them** at once. A rival brightened by `_ColorMultiplier`
sat inside a brightened forest and read as more forest. And a pilot standing *behind* mass had no
mark at all, because a hull tint cannot be seen through a prism.

The generalisable mistake: **a highlight competes with everything else the same trigger lights up.**
Brightness was the one channel already saturated by the ability itself, so it was the one channel
that could not carry the signal. Two layers replace it, each covering a case the other cannot:

1. **The hull is driven to its own SATURATED domain colour** — `_Color1` / `_Color2` (the pair
   `VesselGraph` exposes) as well as `_ColorMultiplier`. HUE is what separates a ship from lit mass,
   because the mass is already bright. Lerped from each material's own authored colours, so this is
   still a *shift*, not an override, and it still cannot make a Ruby pilot read as Jade.
2. **An additive halo drawn with `ZTest Always`** —
   `_Graphics/Materials/Graphs/EchoSightHalo.shader`, a soft disc with a hard **ring** at the hull's
   silhouette, in the same domain colour. This is the half that works through prisms and in empty
   space. The ring matters as much as the glow: a ring is a shape nothing in the arena has, whereas a
   glow among lit prisms is one more bright thing.

Three properties of that shader's render state are load-bearing and none is decorative —
`ZTest Always` (the only way "behind mass" can read), `Blend One One` (it can only add light, so it
never darkens what it marks and never needs a correct sort order), `ZWrite Off` (it can never occlude
the world it is drawn over). It is a hand-written `.shader` rather than a Shader Graph because Shader
Graph cannot express "ignore the depth buffer" on a URP Unlit target, and because at 40 lines
synthesising graph JSON would be the more fragile artefact.

Two implementation notes worth keeping:

- **The disc is billboarded in the VERTEX shader**, from the object origin in view space. So the halo
  is parented to the target at local zero and costs *no* per-frame CPU transform write, the parent
  vessel's rotation cannot tilt or foreshorten it, and one shared unit quad serves every halo at
  every size (the radius is a shader property, never a transform scale — which is also why a parent
  vessel's own scale cannot squash the circle).
- **Its size is measured with `PrismOcclusionCorridor.MeasureCircumscribedRadius`**, the same helper
  the occlusion corridor sizes itself with — hull-only, rotation-invariant, skinned meshes measured in
  root-bone space. Reusing it means the halo is ship-sized on a new vessel of any size with nothing
  authored, and it cannot disagree with the corridor about how big a hull is.

The material is loaded from `Resources/EchoSightHalo.mat` rather than serialized on a prefab: the
highlighter is a plain C# object with no inspector, and a Resources-referenced material also keeps
the shader out of the build stripper's reach (an unreferenced shader is stripped, and `Shader.Find`
would then return null in a player — an editor-only success).

### The Charge slot now reports what the blast did to the LIVING

Space says what the cone did to MASS; Charge says what it did to living things — **pilots debuffed**
and **creatures killed**, as two stacked bare numbers in the same grammar as the prism tally. They
are told apart by colour, from the shared palette: pilots in `whiteColor` (the colour the engaged
sight itself wears, because a pilot is what the sight is for), creatures in `blueColor` (the
neutral-lifeform range a living creature's uncollectable heart already wears). A zero side renders
blank rather than "0".

The two counts come from different places, and the asymmetry is the interesting part:

- **Pilots** the blast can report itself. `ExplosionImpactor` grew a per-blast vessel ledger
  (`_vesselsHit`, the same shape as `_crystalsHit`) recorded in `AcceptImpactee` after the
  friendly-fire gate, so a target loitering inside a still-growing cone is counted **once** rather
  than once per frame. `OnBlastResolved` now carries a `BlastTally` struct instead of a bare int — a
  struct so the next quantity is an added field rather than a signature change that silently reorders
  two ints at every call site.
- **Creatures** it cannot. A creature dies when its last body prism is destroyed, and that death is
  announced by the ECOLOGY several steps downstream (`CellRuntimeDataSO.OnFaunaKilled`, carrying the
  killer's NAME). So fauna are counted over the blast's own lifetime: zeroed on the new
  `ExplosionImpactor.OnBlastBegan`, read on `OnBlastResolved`. The window is exact in practice because
  the blast is the Dolphin's only prism-destroying force; two blasts overlapping inside the 0.15 s
  cooldown would share a count. **That is acceptable for a tally and for nothing else** — these
  numbers must never be read for scoring, which is `StatsManager`'s job off the same channel.

The kill filter compares `IPlayer.Name` against the channel's killer name — the exact comparison
`StatsManager.LifeformKilled` makes, so the tally credits the same kills the scoreboard does with no
second bookkeeping path to keep in sync. And `Fauna.Die` only publishes player-attributed deaths
(starvation and predation are filtered there), so the food web can never inflate it.

The kill channel is resolved from whichever cell the vessel is flying in
(`Cell.FindCellContaining` → `FindNearestActiveCell` → `Cell.RuntimeData`), the same way the seeding
executor resolves its cell — no per-prefab wiring, and a scene with no cell simply has no creatures
to count. The unsubscribe detaches from the channel it *attached* to, never a freshly-resolved one,
so a cell swap mid-flight cannot strand a subscription on the old cell's SO.

### Files added

| role | file |
|---|---|
| Halo shader | `_Graphics/Materials/Graphs/EchoSightHalo.shader` |
| Halo material | `Resources/EchoSightHalo.mat` |
| Domain signal colour | `SO_ColorSet.GetDomainSignalColor` |
| Per-blast vessel ledger + tally struct | `ExplosionImpactor` (`_vesselsHit`, `BlastTally`, `OnBlastBegan`) |

---

## 11. 2026-08-17, fourth pass — the halo stops shrinking, and what the range gate already does

### The halo is no longer a function of distance

A world-sized disc obeys perspective, so it vanished exactly when it was most needed: a rival across
the arena is the case a pilot cannot solve by looking harder. The halo's radius is now

```
r = max( what _Radius subtends in NDC at this depth , _MinScreenRadius )
```

computed in the vertex shader, which is why the offset moved from **view** space to **clip** space:
pre-multiplying the corner offset by `w` makes it survive the perspective divide, turning a world
size into a screen size. The world term uses `UNITY_MATRIX_P._m11` — `cot(fovY/2)`, so
`radius · m11 / w` is the exact NDC half-extent rather than an approximation, and it tracks the speed
tunnel's live FOV for free because the projection matrix is where that effect lands. The x offset
carries the inverse aspect (`_ScreenParams.y / _ScreenParams.x`) or the disc renders as an ellipse.

Measured off-engine at fovY 60°, hull radius 10, `haloScale` 2.4 (`_Radius` 24), floor 0.055, 1080p:

| depth | world NDC | used | on-screen diameter | regime |
|---|---|---|---|---|
| 100 | 0.416 | 0.416 | 449 px | world (hull-sized, ring on the silhouette) |
| 500 | 0.083 | 0.083 | 90 px | world |
| **756** | 0.055 | 0.055 | 59 px | **crossover** |
| 1000 | 0.042 | 0.055 | 59 px | floor (constant) |
| 2400 | 0.017 | 0.055 | 59 px | floor — **3.2× larger than before** |

2400 is the authored cone reach, i.e. the furthest a target can be and still be inside the blast at
all. It used to draw at ~20 px there; it now holds 59. The aspect correction was checked numerically
at the same time (x and y offsets subtend equal pixels — circular, not elliptical).

**The one consequence to accept rather than fix:** once the floor engages the ring no longer lands on
the hull's silhouette — it becomes a reticle *around* the ship. That is the right trade. The
silhouette trace exists to separate a marked ship from mass it is tangled up in, which is a
close-range problem; at range the job is "there is a pilot over there", and a constant glyph does
that better than an accurate one too small to see. Keeping `_RingPos` a constant fraction of the disc
also means the glyph looks identical at every distance.

`vesselHaloMinScreenRadius` on the executor is the dial. Raise it past every practical hull size to
make the halo the same size at **all** distances.

### The range gate already exists, and it is already Space-driven

Worth writing down because it looks like a missing feature and is not. Everything the sight
highlights is already clipped to the blast's reach, and that reach is already the Space-scaled one:

- `ExplosionHelper.TryResolveConicVolume` sets `Height = cone.AuthoredHeight × sizeMultiplier`, where
  `sizeMultiplier` **is** the Space multiplier (`_heightMultiplierAtFullSpace`, ×2 at Space 10).
- Both consumers of that volume reject anything past it: `PrismDestructionSight.hlsl` returns early on
  `s <= 0 || s > height`, and `BlastVolume.Contains` (the vessel half) does the same. The near clip is
  the apex, so mass *behind* the vessel is never lit either.
- **Fauna and flora are already covered** by the prism half. A creature's body prisms are
  `HealthPrism : Prism`, so they render with `BlockGraph` / `ExplodingBlockGraph` — the two graphs the
  sight is spliced into — and light up like any other prism. There is no separate lifeform path to
  add.

And it is already cheap, which is the other half of the question. Per prism fragment with the sight
UP: one dot for the axial band, one reject, then ~12 ALU for the segment distance. With the sight
DOWN: **one compare** (`_PrismSightParams.x > 0`) and return. Zero per-prism CPU either way, no
material swaps, no per-instance overrides, no change to the render queue, the batch, or the draw-call
count — it is the §4.7 global-uniform shape, five `Shader.SetGlobalVector` calls per frame for the
whole world.

**The one real gap: CRYSTALS are not highlighted.** The sight is spliced into the two prism graphs
only, and a crystal draws with `ChargeCrystal.shader` / `CrystalGraph`, which carry no sight node. So
a lifeform's HEART stays dark while its body lights up, as do seeded crystals and omni pickups.
Currently cosmetic — a creature's body is the bulk of its silhouette, so creatures do read — but if
the heart's inconsistency ever matters, the fix is the same splice into the crystal graphs
(`Tools/Shaders/wire_prism_destruction_sight.py` is the pattern), not a new mechanism.

---

## 12. 2026-08-17, fifth pass — the prism half: dimmer, cooler, and WHOLE prisms

### Whole prisms light up, and that is a correctness fix

The sight's volume test is now evaluated **once per prism**, at the prism's own origin read from the
object matrix, instead of once per fragment.

This started as a look request and turned out to be the more accurate behaviour.
`AOEConicSweepQueryJob.Execute` tests `p.Position` — **one point per prism** — and destroys the whole
prism if that point is inside. The per-fragment test was painting the *geometric intersection* of the
volume with each prism's surface, so the sight was drawing a shape the blast does not operate on: it
showed half a prism lit that the blast would remove entirely. Per-prism sampling makes the preview
select exactly the prisms the damage will, and makes the zone's boundary read as the jagged,
prism-granular edge the damage boundary actually is.

It is also not more expensive: the object matrix is already resident, so an interpolated `float3`
read becomes three matrix element reads, and the branch — which previously could diverge across a
prism near the boundary — is now coherent across the whole prism by construction. The idiom
(`GetObjectToWorldMatrix()._m03/_m13/_m23`, guarded on `SHADERGRAPH_PREVIEW`) is copied verbatim from
`PrismClockAnimation.hlsl`, which already uses it in the same two graphs.

`PRISM_SIGHT_WHOLE_PRISM 0` restores per-fragment painting. `PositionWS` stays in the node signature
as that fallback's sample point and is live on the graph either way — the occlusion corridor node
next door consumes the same Position node.

**One known imprecision, debris only.** A flying chunk's visual position is integrated in the VERTEX
stage from its stamped velocity (`PrismFlightClock`), so its object origin is where it *spawned*, not
where it currently is. A chunk therefore lights according to the prism it came from. Transient,
already fading, and arguably the more meaningful answer — not worth solving.

### Dimmer and cooler

| | before | after |
|---|---|---|
| `PRISM_SIGHT_COLOR` | `(1.00, 0.72, 0.34)` — warm amber, H 32° S 0.66 | `(0.45, 0.70, 1.00)` — pale cool blue, H 209° S 0.55 |
| `PRISM_SIGHT_GAIN` | 1.15 | 0.70 |

The gain cut is larger than it looks like it needs to be, for two compounding reasons: the old amber
drove the RED channel to `1.15 × 1.0 = 1.15` at full fill, which clips on essentially any prism and
is precisely what "washed out" means; and lighting whole prisms lights strictly *more* screen area
than partial-intersection painting did, so holding the old gain would have washed out harder than
before. At the new values the peak add is `0.7 × (0.45, 0.70, 1.0) = (0.32, 0.49, 0.70)` — enough to
read as lit, low enough that the prism's own tier colour still shows through.

**The hue carries a risk the amber did not, and it is worth stating.** Warm was originally chosen
because no palette tier owns warm, so "lit by the sight" could never be misread as "this mass is
shielded / danger / another team". Cool is a busier neighbourhood: the shielded tier is frosty and
Jade's base face is a deep blue. Two things keep it clear — the cast is deliberately **desaturated**
(S 0.55, where a tier colour at this lightness runs 0.9+), and the lower gain leaves the underlying
tier visible rather than flooding it. If a lit shielded prism ever starts reading as a tier change,
**lower the gain before touching the hue** (`Docs/PALETTE.md` — the tier colours are the language;
do not borrow their space).

---

## 13. Compiled and measured (2026-08-17, ship-deep)

Both hand-written shader files were **compiled with clang and executed** — the shipped files from
the repo, under a short listed substitution set, against a stubbed URP surface
(`/asset-surgery` §4.5c). This is stronger than the numpy ports above: it validates the FILE, not a
transcription of it. It immediately found one defect — `half4(..., 0.0h)` in the halo's fragment
return, a half-literal suffix whose support is not portable across shader compilers and which buys
nothing; now `0.0`.

**The sight's volume gate, measured** (apex at origin, axis +z, gape +y, added light in blue):

| prism origin | cone height | added light | |
|---|---|---|---|
| on-axis, z=500 | 2400 | 0.2450 | `gain 0.7 × blue 1.0 × CORE_FILL 0.35` exactly |
| y=148, z=500 (on the gape segment) | 2400 | 0.2450 | clamped onto the segment → still deep inside |
| y=200, z=500 (off the segment) | 2400 | **0** | outside the capsule radius |
| z=−500 (behind the vessel) | 2400 | **0** | the apex near-clip |
| z=3000 | 2400 | **0** | **past reach** |
| z=1500 | **1200** (Space halved) | **0** | **the SPACE gate — the same point lights at full reach and goes dark at half** |

The last two rows are the direct evidence for §11's claim that the range gate is real and is driven
by Space. It was previously argued from reading the code; it is now measured.

**The halo's distance independence, measured** (hull 10, `haloScale` 2.4, floor 0.055, 1920×1080):

| depth | NDC x / y | pixels x / y | |
|---|---|---|---|
| 100 | 0.2338 / 0.4157 | 224.5 / 224.5 | circular, world-sized |
| 500 | 0.0468 / 0.0831 | 44.9 / 44.9 | circular, world-sized |
| 756 | 0.0309 / 0.0550 | 29.7 / 29.7 | crossover |
| 1000 | 0.0309 / 0.0550 | 29.7 / 29.7 | **floor holding** |
| 2400 | 0.0309 / 0.0550 | 29.7 / 29.7 | **floor holding** — 59 px diameter at max reach |

Equal x/y pixel extents at every depth prove the aspect correction in the SHIPPED code, not just in
the Python port. The fragment profile peaks at the ring (2.60 at `d = 1/2.4`) against 1.40 at the
centre, so the ring is the brightest feature as designed.

Harness: `clang++ -std=c++17 -Wall` over `PrismDestructionSight.hlsl` verbatim and the
`EchoSightHalo` HLSLPROGRAM body, with `ext_vector_type` float2/3/4, HLSL-shaped
`abs`/`min`/`max`/`pow`/`exp`, and stubs for `GetObjectToWorldMatrix` / `TransformObjectToWorld` /
`TransformWorldToHClip` / `UNITY_MATRIX_P` / `_ScreenParams`.
