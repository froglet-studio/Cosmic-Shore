# The Vessel Vision Band — PLATFORM LAW

> Every vessel is progressively re-shaded into a flat, cel-banded silhouette in its own **domain**
> colour as a function of its distance from the camera drawing it. Nothing up close, full mark
> across the middle of an arena, gone again at extreme range — **both edges graded**.
>
> It is **not a feature a vessel, a scene, or a game mode may choose.** A pilot being able to find
> another pilot is a property of the game, not of the ship they happen to be flying.

Sibling of the two laws already documented here — `Docs/PRISM_ANIMATION.md §4.7` (the prism
occlusion corridor) and `Docs/SPEED_TUNNEL.md` — and built in the same shape, for the same reason:
its failure mode is **silence**. An unmarked ship just looks like a ship.

---

## §1 The problem it solves

At gameplay range a Cosmic Shore vessel is a dark, glassy, few-pixel shape against a nebula that is
itself dark, glassy and colourful. The hull's authored two-tone read — the thing that makes a ship
beautiful at conversational range — is exactly what makes it *disappear* at 900 units. Worse, the
domain hull materials are stylised rather than literal (Ruby's ship material is authored
`(0.27, 0, 0.75)`, a **purple**), so even when you do spot a ship the hull does not reliably answer
*whose it is*.

Halo Infinite's much-liked player outline solves the same problem the same way: at range a player
stops being rendered as a person and starts being rendered as a **legible, team-coloured shape**.

## §2 The law

```
 amount
   1 |          ______________________
     |         /                      \
     |        /                        \
   0 |_______/                          \___________
     0    nearStart  nearFull      farFull  farEnd     distance from the drawing camera
```

| Region | Behaviour | Why |
|---|---|---|
| `d < nearStart` | exactly **0** | Close up the ship fills the screen and its own art is the better read. This is also what silently and correctly excludes **the pilot's own vessel**, which rides 10–40 units from its camera (`CameraSettingsSO.dynamicMin/MaxDistance`). |
| `nearStart → nearFull` | graded 0 → 1 | A mark that pops on reads as a *new object appearing*. Continuity of existence is a platform law about things entering and leaving the world; this is the same rule applied to a thing entering and leaving **visibility**. |
| `nearFull → farFull` | exactly **1** | The plateau must cover a full arena crossing (cell membrane radius 1200), because pilots on opposite sides of a cell are the case the aid exists for. |
| `farFull → farEnd` | graded 1 → 0 | Same reason as the rising edge, in reverse. |
| `d > farEnd` | exactly **0** | Past that a ship subtends a few pixels, and a saturated dot is not a ship any more — it is one more bright speck in an arena full of crystals and prisms. **A signal that cannot be told from noise is worse than no signal.** |

**There is deliberately no "is this me" test anywhere in the law.** The rule is *"close things do
not need help"*, and your own ship is the closest thing there is — the exclusion **falls out**, it
is not special-cased. A broadcast or replay camera parked away from the fight therefore marks
**every** ship including the local one, which is exactly what a broadcast view wants. That is also
why this law, unlike the corridor and the speed tunnel, has **no `SetSuppressed` hold**: those two
are effects for the pilot at the controls; this one is most useful precisely where they are
suppressed.

**The mapping is ABSOLUTE.** The same distance to *any* vessel produces the same mark — a big hull
is marked at the same range as a small one, because the question the aid answers ("is there a pilot
over there, and whose?") does not depend on how big the pilot's ship is. Do **not** add per-vessel
windows, per-vessel scalars, or a normalization by apparent size. That is a different design, and
it would destroy the one property the law exists to guarantee: a player learns the distance cue
**once** and it is then true of every ship in the game.

## §2.1 What the mark actually looks like

Three things compose, all in the vessel's **domain signal colour**
(`SO_ColorSet.GetDomainSignalColor` — the domain UI colour with its brightest channel driven to 1,
the same accessor the HUD and the Dolphin's Echo Sight read):

1. **Quantized tone.** The facing term `dot(N, V)` is floored into `celSteps` flat levels. This is
   what makes it read as *cel shading* rather than as a tint: a smooth ramp over a hull is just the
   hull again in a different colour, whereas two or three flat tones with hard borders is a
   **shape**, and a shape survives being thirty pixels tall.
2. **A silhouette rim.** `smoothstep(rimInner, rimOuter, 1 - dot(N, V))`, added on top. The rim is
   the part that survives at extreme range — by then the whole ship *is* rim — and it is what
   separates a marked hull from lit mass tangled up with it.
3. **A blend, not a replacement.** `strength` caps how far the hull is driven to the mark, so a
   marked vessel still reads as *that* vessel.

Colour is the **domain's, always** — never derived from the hull's own materials, for the purple
reason in §1. Domain identity is the palette's job and this must not borrow its space for something
else (`Docs/PALETTE.md`).

## §2.2 It applies in the menu too, deliberately

Menu_Main's lava-lamp vessels go through `VesselHelper.SetShipProperties` like every other vessel,
and the `LavaLamp` menu camera orbits the **cell** at radius 686 while the vessel drifts anywhere
inside a 1200-unit membrane — so a menu ship is sometimes inside the band and sometimes not, and
gets marked accordingly. That is not an oversight to fence off. *The fundamentals are universal*
(CLAUDE.md § "Universality"): the lava lamp **is** freestyle, its vessel **is** the gameplay vessel,
and a context-specific exemption here is exactly how a rule stops being a rule. The moment the
player takes control the camera snaps to 10–40 units and their own ship is unmarked again, which is
the law working, not an exception to it.

## §3 What makes it strict

Four layers, none of which can be defeated by authoring:

1. **The shading lives in `VesselGraph.shadergraph` itself.** That one graph is what every hull
   surface of every vessel in the fleet is painted with — `VesselCustomization`'s three material
   roles (Body = `BlueBaseVesselMaterial`, Domain = the per-domain ship material, Window =
   `ScreenVesselMaterial`) plus the engine/accent/lime variants are all VesselGraph materials. A new
   vessel inherits the law **by being painted**. The non-VesselGraph materials a vessel carries are
   its skimmer crackle overlay, its jet particles, the Rhino's sword tracer and the trail viewer —
   none of which is hull, and none of which should wear a pilot-identification mark.
2. **One stamp site.** The per-vessel datum (the domain colour) is written from
   `VesselHelper.SetShipProperties` — the one method a vessel's domain flows through on **every**
   path: first spawn, runtime vessel swap, and every replicated `Player.NetDomain` change. There is
   no component to add and no scene to wire, so there is nothing to forget.
3. **Fail-loud diagnostics.** `VesselVisionDiagnostics` names, once, any vessel that could not be
   stamped, and names both fixes (repaint the hull, or re-run the wirer).
4. **Two gates that cannot drift.** `VesselVisionLawTests` (edit-mode) and **FrogletTools > Vessels
   > Validate Vessel Vision Band** both call the *same* predicates in `VesselVisionLawSource` and
   `VesselVisionShadingConfigSO.IsSane`, so an asset that passes the audit cannot fail the test.

### §3.1 The per-vessel channel, and why it heals itself

The tint rides a `MaterialPropertyBlock` on each vessel renderer. Several other systems write those
same renderers — the Echo Sight highlight, the Serpent's cloak, the Rhino's sword FX. They **compose
correctly**, because each does a get-modify-set round trip, which preserves foreign properties. What
does not compose is a `SetPropertyBlock(null)` **restore**: it clears the whole block, tint included.

> **General rule (the same one the speed tunnel records for the Panini override): when several
> systems write one channel, each restores what *it* changed; only an owner may clear.**

`EchoSightVesselHighlighter.Restore` was changed to put back exactly its own three properties for
that reason. But relying on every current *and future* MPB writer to remember is not a law, so the
publisher also **re-stamps one vessel per frame, round-robin**. With a full lobby that is a complete
sweep every twelve frames for a twelfth of the cost, it needs no cooperation from any other system,
and it repairs a stale renderer list after a rig swap for free.

### §3.2 Where the work happens, and why

| Quantity | Where | Why not the other place |
|---|---|---|
| Distance to camera | **GPU**, per fragment, from the object origin | Per-**camera** live data: the answer differs between the game view, the scene view, a replay camera and any future split screen. A CPU implementation would have to pick one camera and be wrong in all the others — and would cost a per-vessel per-frame write for a number the GPU already has. |
| Band / cel / rim tuning | **3 global uniforms per frame** | `O(1)` in the number of vessels. Re-published every frame rather than once, so an edit to the asset is live in play mode and a scene load can never leave the band holding a value nothing owns. |
| Domain colour | **CPU**, per vessel, on domain change | It is not derivable on the GPU (the hull colours are stylised, see §1) and it changes only on a domain event. Twelve vessels already individually simulated is exactly the case CLAUDE.md sanctions per-vessel CPU for. |

The band is measured from the **object's origin**, not from the fragment. A vessel is a *signal*, and
a signal must switch on as one object: metering per fragment would let a long hull have its nose
inside the band and its tail outside it, and would draw the falling edge as a gradient across the
ship. The view vector, by contrast, *is* per fragment — it is what curves the cel bands around the
hull. The origin idiom (reading the translation column of `UNITY_MATRIX_M`) is the one
`PrismClockAnimation.hlsl` and `PrismDestructionSight.hlsl` already use; it is per-draw data, so it
survives instancing and the SRP batcher.

### §3.3 The tint's alpha is a MARKER, not an opacity

`_VesselVisionTint` defaults to `(0,0,0,0)` and the shader returns the base colour untouched when
`a <= 0`. That is what keeps the law to **vessels** even though `VesselGraph` is also worn by
`BlueOrangeProjectileMaterial`: an object nobody stamped is not a vessel, and the effect **declines
rather than guessing**.

It is also why the property must be **EXPOSED**. An unexposed ShaderGraph property is declared
outside `UnityPerMaterial`, so no `MaterialPropertyBlock` could reach it and `Material.HasColor`
could never see it — the trap `PrismOcclusionDiagnostics` records for the corridor's own globals,
and the reason the runtime can census wired materials here but not there. The wirer and both gates
assert exposure explicitly, because losing it is completely silent: the shader still compiles, the
graph still looks wired, every vessel reads alpha 0, and the whole law renders as "off".

## §4 Tuning

`Assets/Resources/VesselVisionShadingConfig.asset` (`VesselVisionShadingConfigSO`) is the **only**
tuning surface for the entire fleet. With no asset the SO's own defaults apply, so the law holds
with zero authoring.

| Field | Ships at | Note |
|---|---|---|
| `nearFadeStart` | 150 | Must clear `MinLocalHullClearance` (60) or the law paints the pilot's own hull. |
| `nearFullStart` | 350 | ~5% of screen height for a 10-unit hull — small, but plainly visible. |
| `farFullEnd` | 2000 | Beyond a full cell crossing (membrane radius 1200). |
| `farFadeEnd` | 3500 | Must clear `MinUsefulReach` (2400 = one arena diameter). |
| `strength` | 0.85 | Below 1 so a marked vessel still reads as *that* vessel. |
| `celSteps` | 3 | 2–6. Two or three flat tones is the readable range. |
| `shadeFloor` | 0.35 | Never 0 — a black band punches a hole in the silhouette the aid exists to draw. |
| `gain` | 1.15 | Gameplay bloom is clamped at 0.5 (`Docs/PALETTE.md §3`), so the signal colour already blooms at 1.0 and this buys presence rather than glow. |
| `rimInner` / `rimOuter` | 0.55 / 0.95 | Closer together = harder outline; further apart = glow. |
| `rimGain` | 1.1 | How much the rim adds on top of the cel tone. |

## §5 Files

| Role | Path |
|---|---|
| GPU half (band, cel, rim, entry point) | `Assets/_Graphics/Materials/Graphs/VesselVisionShading.hlsl` |
| Wired graph | `Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph` |
| Graph splice (idempotent, `--check`) | `Tools/Shaders/wire_vessel_vision_shading.py` |
| Behavioural proof (compiles + RUNS the shipped HLSL) | `Tools/Shaders/verify_vessel_vision_band.py` |
| CPU half (globals + stamp + heal) | `Assets/_Scripts/Utility/VesselVisionShading.cs` |
| Fail-loud | `Assets/_Scripts/Utility/VesselVisionDiagnostics.cs` |
| Shared gate predicates | `Assets/_Scripts/Utility/VesselVisionLawSource.cs` |
| Tuning | `Assets/_Scripts/ScriptableObjects/VesselVisionShadingConfigSO.cs` → `Assets/Resources/VesselVisionShadingConfig.asset` |
| The one stamp site | `Assets/_Scripts/Controller/Vessel/VesselHelper.cs` (`SetShipProperties`) |
| Asset gate | `Assets/_Scripts/Editor/VesselVisionLawValidator.cs` |
| Test gate | `Assets/_Scripts/Tests/Editor/VesselVisionLawTests.cs` |

## §6 Verification

| Check | How | Catches |
|---|---|---|
| The shipped HLSL behaves | `python3 Tools/Shaders/verify_vessel_vision_band.py` | An inert off sentinel, a plateau that never reaches 1, a non-monotone or popping edge, a quantizer that overshoots its top tone, a stamped-but-near vessel being modified, an unstamped object being modified. Compiles and **runs** the shipped file with clang++ — no Unity needed. |
| The graph is still spliced | `python3 Tools/Shaders/wire_vessel_vision_shading.py --check` | A reverted or hand-edited graph. Exits non-zero; re-run without `--check` to repair. |
| Guards + assets | **FrogletTools > Vessels > Validate Vessel Vision Band** | A severed tint channel, a lost cutoff, a second stamp site, an insane config, a vessel prefab with no hull material on the wired shader. |
| Guards + assets, in CI | `VesselVisionLawTests` (edit mode) | The same, from the test runner. |
| Conditional compilation | `python3 Tools/Build/check_conditional_compilation.py` | The `#if UNITY_EDITOR` trap in `VesselVisionLawSource`. |

**In play:** fly two vessels of different domains into one arena and back away. The far ship should
*arrive* as a flat coloured shape rather than snapping on, hold that read across the middle of the
cell, and fade back out at the far edge — while your own ship, and anything you are nose-to-nose
with, keeps its ordinary hull art the whole time.

## §7 Follow-ups (not shipped)

- **The band is authored in absolute world units, and every arena is a different size.** It is
  deliberately absolute (§2), but the *numbers* were derived against a standard cell (membrane
  radius 1200). A mode whose arena is an order of magnitude larger or smaller than that has not been
  play-tested. Re-derive the four distances against the arena, never per vessel.
- **No occlusion behaviour.** The mark rides the hull's own draw, so a vessel behind mass is not
  marked through it. That is deliberate — seeing through walls is a different (and much bigger)
  design decision than being able to *find* a ship you can already see — but it is the obvious next
  question, and the Dolphin's Echo Sight halo (`EchoSightHalo.shader`, `ZTest Always`) is the
  precedent for how it would be done if it is ever wanted.
- **Vessel-adjacent geometry is unmarked.** Trails, projectiles and skimmers are not hull and are
  not stamped. A trail already carries its domain colour in its own material, so this has not been
  a problem in practice.
