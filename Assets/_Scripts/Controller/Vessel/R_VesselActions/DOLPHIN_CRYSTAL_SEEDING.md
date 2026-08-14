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

Charge still owns the recharge; Space still owns the reach. Neither element→ability binding moved —
only which of them carries an input.

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

### Twin Seed (Charge L5)

The upgrade used to raise a **carry** limit — meaningless once nothing is carried. It is now a
**yield**: each seeding plants two crystals instead of one. The HUD art is unchanged; see §3.

---

## 2. Space: Echo Obliteration, and the Echo Sight

Hold the right trigger and the view eases into a zoomed first-person shot down the blast axis, with
every prism standing inside the destruction volume lit up. Release and it eases back. **It fires
nothing** — the blast still goes off when the Dolphin strikes a crystal. The sight only makes the
shape legible so the pilot can choose which way to be pointing when they take the crystal.

### Three view surfaces, three owners — and the split is load-bearing

| surface | owner | why |
|---|---|---|
| Camera **pose** | `EchoSightActionExecutor` | It lerps `CustomCameraController`'s follow offset. Ordinary, no law involved — the speed tunnel is explicitly a no-camera-distance-change effect. |
| Camera **FOV** | **`VesselSpeedTunnel`**, never the executor | See below. |
| Prism **highlight** | `PrismDestructionSight` | Global uniforms, zero per-prism work. |

**Why the sight must not write `Camera.fieldOfView`.** It is broken two ways and both are silent:

1. While the tunnel is engaged it overwrites the ability every frame — the zoom simply does nothing
   above walking pace.
2. When the tunnel *engages*, it captures whatever FOV it finds as the home to restore later
   (`Apply`: `if (cam != _appliedCamera) _homeFov = cam.fieldOfView`). A zoom active at that instant
   is **baked in permanently** and the player never gets their FOV back.

So the sight pushes a **home** through `VesselSpeedTunnel.SetHomeFovOverride` and the tunnel stays
the single writer. **This does not weaken the law** (`Docs/SPEED_TUNNEL.md` §1): the speed→effect
mapping is untouched and still absolute — the drop is still `fovDrop × effect01`, fleet-wide, with
no per-vessel number anywhere. What moves is the home it measures down from, and home was always a
live value rather than a constant: the player's own FOV slider moves it mid-effect through the same
path (`OnHomeFieldOfViewChanged`). A sighting zoom is that same class of thing. A zoomed-in Dolphin
at speed sits exactly as deep in the tunnel as an un-zoomed one.

Two consequences worth knowing:

- The tunnel now keeps **applying** at zero speed effect while an override is in force
  (`_effect01 > 0.001f || HasHomeFovOverride`). Releasing would restore the true home and cancel the
  sight.
- `_capturedHomeFov` is captured **once at engage** and never re-read. Once the override is in force
  the tunnel is writing the camera *from* that override, so reading the camera back each frame would
  feed the zoom into its own input and run away.

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
| FOV home override | `_Scripts/Utility/VesselSpeedTunnel.cs` (`SetHomeFovOverride`) |
| HUD | `UI/View/DolphinVesselHUDView.cs`, `Data Containers/DolphinVesselHUDController.cs` |
| Assets | `_SO_Assets/VesselActions/Dolphin/DeployTeamCrystalAction.asset`, `EchoSightAction.asset` |

## 5. Tuning knobs

| knob | asset | effect |
|---|---|---|
| `cooldown` / `minCooldown` | `DeployTeamCrystalAction` | Seeding tempo, and therefore blast tempo |
| `bandInnerFraction` / `bandOuterFraction` | " | Where in the cytoplasm crystals land (0..1 across nucleus→membrane) |
| `maxLiveSeeded` | " | How many of this Dolphin's crystals may stand at once (0 = uncapped) |
| `upgradedSeedsPerCycle` | " | Twin Seed's yield |
| `cooldownMultiplierAtFullCharge` | " | Charge's grip on the recharge |
| `sightFollowOffset` | `EchoSightAction` | How far forward the first-person view sits |
| `sightFieldOfView` | " | Zoom depth (pushed as the tunnel's home) |
| `transitionSeconds` | " | Ease in/out of the sight |
| `highlightStrength` | " | Peak highlight |
| `PRISM_SIGHT_COLOR` / `_GAIN` / `_EDGE_POWER` / `_CORE_FILL` | `PrismDestructionSight.hlsl` | The highlight's look |

---

## 6. In-editor verification

**None of this has been run.** I cannot open Unity; every row below is unverified and is the
hand-back. Mirrored in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

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
| Charge to level 5 | the mini crystal pip appears; each cycle now plants two |
| **hold RT** | the view eases forward and zooms; prisms inside the blast volume light up warm |
| release RT | it eases back; the highlight fades out; FOV returns to what it was |
| hold RT, then accelerate hard | the tunnel still narrows on top of the zoom (they compose, no fight, no snap) |
| release RT while at speed | FOV lands back on the tunnel's normal curve, not on a baked-in zoom |
| skim to fill energy, hold RT | the highlighted volume opens as a **fan** — wide across the jaw plane, narrow across the beam |
| ram a prism (halves energy), hold RT | the fan narrows to match |
| take a crystal while sighting | the blast destroys what the sight was showing |
| MPPM, two clients | a remote Dolphin holding RT shows nothing unusual — the sight is local-only |
| swap vessel while sighting | camera and FOV return to normal; no stuck zoom, no stuck highlight |

**Two things I would check first if something looks wrong**, because they are the likeliest failures
and both are silent: whether the graphs recompiled at all (an unimported graph shows no highlight and
no error), and whether `blastEffect` is actually assigned on the Dolphin's `EchoSightActionExecutor`
(unassigned = the sight zooms but highlights nothing).

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
- **`CameraManager.SetNormalizedCloseCameraDistance` is a no-op** (its whole body is commented out).
  The sight drives `CustomCameraController.SetFollowOffset` directly instead. Worth deciding whether
  that method should be repaired or removed.
