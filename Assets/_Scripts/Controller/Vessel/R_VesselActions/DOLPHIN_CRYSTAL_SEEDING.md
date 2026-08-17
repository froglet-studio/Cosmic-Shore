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
| raise Space | the subtle bar under the jaws lengthens |
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
| **Space** | the jaw pair (gape = energy) + a subtle reach bar + the prism tally | jaws moved from the Time band; the tally moved onto its own row beneath them and was widened to 120px so a five-figure claim renders at full size |
| **Time** | the authored 11-step boost ring | moved from the Mass band |

The profile is procedural rather than an authored sprite ladder for the same reason the preview
volume is not re-derived: it is a continuous function of two live meters, and a sprite ladder would
quantize it and silently stop matching the blast the first time anyone retuned a scale. It is fed
by `VesselExplosionByCrystalEffectSO.TryResolveProfile`, which hands back radius, half-length and
the reference extent in one call so the icon can never mix a radius from one frame with a reference
from another.

`DolphinHUDRowWirer` was re-cut to the same layout, so the row still has exactly one definition in
code (`FrogletTools > Vessels > Wire Dolphin Ability Row`).

### Files added

| role | file |
|---|---|
| Pilot Echo highlighter | `Executors/EchoSightVesselHighlighter.cs` |
| Blast profile icon | `_Scripts/UI/View/BlastProfileGraphic.cs` |
| Un-anchored elemental lerp | `ElementalScaling.MultiplierFromRest` |
| CPU volume predicate | `ExplosionHelper.cs` ▸ `BlastVolume.Contains` |
| Profile / reach readouts | `VesselExplosionByCrystalEffectSO.TryResolveProfile` / `TryResolveReach` / `MaxReach` |
