# Stoat — the Swing (Space), the Warp (Time), Heavy Launch (Mass) and the Ripple (Charge)

The Stoat's kit as it ships. The file keeps its old name (it began as the round-15 field dipole) so the links into it
hold; §1b is still the swing's section number.

> **2026-11-21 — ONE SLINGSHOT, EVERY PILOT (Yash: "make sure all AI uses our latest slingshot mechanics, and delete all
> other old pathfinding and other mechanics").** The SWING (§1b) is the Stoat's only slingshot, for a person and for every
> AI in every mode. Deleted from the code, the assets, the tests and the tools: the field dipole and its predicted path
> (Mass mode, removed from the kit 2026-11-02), Time 5's Foresight (the old path drawn in front of every rival: the "dotted
> line on the AI Stoats"), the catch orbit and its aimed exit, the skim sling, the trampoline and the crystal style. What
> each was, and every number it was tuned to, is in this file's history: `git show 4e5df7090a:<this path>` (§6 lists the
> files). **2026-11-22 (Yash: "I dont need to see the drawing line either"): nothing is drawn for any pilot** - the
> swing's reach ring, dotted exit line and fling ring are deleted with `StoatPathfinderWorldDots`.

**Quick read — the four abilities.**

| Element | Ability (map label) | Input | What the element scales | Level 5 |
|---|---|---|---|---|
| **Space** | The swing (Catch Orbit) | LT / RT | the swing's reach ×1 → ×1.4 (`orbitReach`) | **Accretion** — prisms the hole swallows are stolen; each adds 4% to its ripple, to +40% |
| **Time** | The warp (Timed Slingshot) | passive | the flight clock's warp ×2.5 → ×5 (`StoatDipoleConfig.boost`) | — open since Foresight was deleted |
| **Charge** | Ripple | passive, each time a swing hole collapses | the ripple's reach ×1 → ×2 | **Siphon** — stripped petals come to you |
| **Mass** | Heavy Launch | passive | a second push at the fling ×1 → ×2.5 (`launchPower`), **shipped off** (`swingKickShare` 0) | **Armoured Wake** — the trail laid after a swing arrives shielded |

## 1b. The swing (Yash, 2026-11-11; one slingshot 2026-11-19)

Yash, 2026-11-11: *"change it to a black hole only as before, then make the slingshot angle dynamic based on our
controller's axis like before... try a better approach this time... the black hole looks more better than the white
hole"*. One black hole, no white hole, no orbit, no shell. `StoatSlingExecutor`, `StoatSlingMath`, `StoatSlingConfigSO`
(`_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset`).

- **The press** lays a lone BLACK HOLE `swingAhead` 150 u ahead and `swingSide` 35 u to the pressed side (RT right, LT
  left; `StoatSlingMath.SwingHole`), its horizon `swingHorizon` 0.8 u, its pull `poleGM` 20 000. It is owned by the Stoat
  that laid it, so it pulls no vessel (`BlackHoleVesselPull`), and it steals prisms for Space 5 Accretion. One hole per
  Stoat: a press over a live hole collapses it first.
- **The swing.** Pass within `swingReach` 55 u (× Space's reach) with the hole at least `swingCatchDegrees` 40° off your
  nose (beside it, not into it: `SwingStarts`) and it swings you toward itself: your heading turns about the axis fixed at
  that moment (`SwingAxis`) at your speed over your distance (ω = v / d), never faster than `swingDegreesPerSecond` 150°/s
  (`SwingRate`). No radius is held and nothing pulls you: only the heading turns.
- **The stick picks the angle, live** (`SwingTurnDegrees`, read every frame): the stick's share toward the hole maps
  `swingMinDegrees` 15° (away) .. 52.5° (at rest) .. `swingMaxDegrees` 90° (toward). The moment you have turned that far
  you are let go on that heading. Letting go of the trigger while it swings you lets you go at once; before it, nothing.
- **One slingshot (Yash, 2026-11-19: "I want only one slingshot, the first one, then player should be able to reach
  their target destinations more accurately and comfortably").** The swing's own speed-up (the flight clock at the swing's
  warp from its start) is kept as momentum, fading over `momentumSeconds` 6 s; you leave on the heading the swing turned you to, with
  nothing more added. The second push (the hold's deepest squeeze × Mass's `launchPower`, under `speedCapShare` and
  `warpKickCapCruise`) is paid only as `swingKickShare` of itself, shipped **0**. So Mass scales nothing on the swing today
  (Heavy Launch is dormant; Armoured Wake still acts) — a decision for the element map.
- **The warp** (`StoatPathfinderExecutor`, the one writer of `VesselTransformer.FlightTimeScale`): from the press the clock
  rises to `pressWarpShare` (0.5) of the swing's warp (`catchWarpShare` 1.15 × `StoatDipoleConfig.boost`, ×2.7 at rest),
  to all of it and `orbitSpeedBonus` (15%) more while the swing turns you, and after the fling its momentum. The largest
  wins; they never add. The omni crystal's Overclock (parked) would hold it full.
- **Nothing is drawn (2026-11-22).** Until then the local pilot saw a faint ring at the reach, a dotted EXIT LINE along
  the heading the stick asked for, and a lime ring at each fling. All three are deleted, for every pilot, with the world
  dots that drew them. What shows a swing now: the black hole's own look, the HUD's Time card, and the Charge ripple.
- **The end.** Passed untouched (moving away beyond the reach) or after `swingSeconds` 4 s, the hole collapses; flung, it
  collapses at once. Either way it throws the Charge ripple (§1c) at `rippleSeparation` 90 u. A hull that came within the
  reach lays its trail shielded for `slingBoostSeconds` 1.5 s (Mass 5).
- **The AI** (`StoatSlingExecutor.AutopilotSwing`, the only autopilot) presses toward a turn — its target at least
  `aiSlingMinTurnDegrees` off the nose and `aiSlingMinDistance` away, judged on the race line's bearing at the ring's range
  (`StoatDipoleMath.AutopilotGauge`), every `aiSlingIntervalSeconds` — or for the warp alone on a leg longer than
  `aiWarpMinDistance` (0 = never), and lets go of the trigger at once: the hole stays up. Steering for its target it passes
  beside the hole and is swung; it is let go once it faces its target within `aiSwingFaceDegrees` 8° (after 15°), once its
  target is no longer on the swing's side (`OnSwingSide`: a press for the warp alone), or at 90°. Both edges go through the
  REPLICATED press path, so every peer runs the swing a person's would. Its numbers come through `AIModeTuning`: the
  hull's own rows (`aiSlingMinTurnDegrees` 20) everywhere, except Slingshot, which flies Stoat run1's trained copy
  (`Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset`: 154° / 11.5 u warp presses, `promote_trained_drives.py`).
  The Regatta and Warpline copies carry the same swing rows. Model: `Tools/Build/stoat_orbit_sim.py --swing`.

## 1c. The Ripple — Charge, passive

The design call: *"charge should always be focused on anti vessel/fauna gameplay ... it should look like a cool space time
ripple."* Every time this hull's swing hole collapses, it throws a ripple from where the hole was. Its front expands with an
ease-out over `expandSeconds` (0.9 s) to

```
radius = clamp(rippleSeparation (90) × 0.6, 30, 160) × Charge (×1 → ×2, at the REPLICATED level) × (1 + Accretion)
```

| reached by the front | what happens |
|---|---|
| a **rival vessel** | stripped of **one whole petal of every element** it holds (`ResourceSystem.AccrueElementalLoss`, classed `ElementalDebuffSources.Explosion`, so a ward against blasts stops it), shed as crystals around it — with **Charge 5 — Siphon** the petals go straight to the Stoat |
| a **creature** | killed (`Fauna.Predated`) on the fauna's simulating machine |
| a **prism**, the owner, a teammate | nothing |

**A strip is a scored hit** (2026-11-15, for Shockfront): admitted through `CombatHitDrain.TryAdmitPetals` (Debuff class, a
per-victim cooldown of `StoatRippleConfigSO.hitCooldownSeconds` 0.5 s), raising `Event_CombatHitStats`, so it scores and
silences its victim (`Docs/ElementalAbilitySystem/SILENCE.md`). Every peer runs it from its own copy of the hole, with
Charge and Siphon read off replicated state.

**The drawn wave — a space-time ripple with no colour (2026-11-21).** Yash: *"I want a near-realistic space-time ripple
wave, no domain color, just a wave of disturbance created from the point of annihilation of the black hole diminishing to
distance."* `StoatRipplePass` copies the camera colour after the transparents and redraws every pixel a wave crosses,
**bent** along the wave's radius, the scene's own light raised on a crest and lowered in a trough by `shimmer` (0.05);
nothing is added, so bloom has nothing to blow up. The front is `StoatRippleMath.Profile` (a sharp leading edge, about
three crests `wavelength` 9 u apart); the bend (`bend` 0.012 rad) holds inside `nearRadius` 22 u and then falls as 1/r;
when the front runs through the camera the view swells by `passShare` 0.35. The pass is enqueued only while a ripple
lives. Stated limit: no depth texture, so an opaque in front of the band is bent with what is behind it (D7). Held by
`StoatRippleTests` and `Tools/Shaders/verify_stoat_ripple.py`.

## 1d. The HUD — four cards, four gauges

`StoatHUDVariant.prefab`, `StoatHUDView`, `StoatHUDController` (every signal polled from the Stoat's OWN executors; the
local human pilot's HUD only):

| Card | Gauge |
|---|---|
| Charge | the ripple the open hole would throw now, as a share of the largest (Accretion full) |
| Mass | rests (Heavy Launch keeps its authored icon) |
| Space | Space-5 Accretion: how much of the ripple bonus the open hole has earned |
| Time | how far through the turn the stick asks for the swing is, in the pilot's domain colour while it swings you |

The card parts are drawn for the kit by the icon kit (`Tools/Build/icon_kit/specs/stoat.py`); the Time card's Foresight
part stays hidden since Foresight was deleted.

## 1e. The upgrades and the omni crystal

- **Space 5 — Accretion on the swing.** While the swing's hole is up its `PrismCapture` is `Steal` in the owner's domain
  (every peer), so a rival prism that falls in comes out yours; every capture since the hole was laid adds
  `accretionPerPrism` (4%) to its ripple's reach, to `accretionMax` (+40%).
- **Time 5 — open.** Foresight (the nearest rivals' predicted paths, drawn with the old field pathfinder) was deleted on
  2026-11-21: it was the dotted line Yash saw on the AI Stoats. The map's Time `UpgradeLabel` is empty until one is designed.
- **Charge 5 — Siphon**, §1c. **Mass 5 — Armoured Wake**, §1b "The end".
- **The omni crystal is FULMEN** (Yash, 2026-10-29; kept 2026-11-20): one bolt from the cell's centre burns every prism on
  its path (`Controller/Environment/Fulmen/README.md`). Overclock (`StoatOverclockByCrystalEffectSO`) is parked: in no
  container, its script and asset kept for the generators' guid.

## 3. Network

The press and release replicate (`R_VesselActionHandler`'s replicated press path), so every peer lays its own copy of the
hole from the replicated press, and lets it collapse on the same rules. The swing itself (the turn, the fling, the warp) is
flown on the simulating machine only (`MantaStingActionExecutor.IsSimAuthority`: the owner, the host for an AI); the
squeeze and the stick are not replicated. `R_VesselActionHandler.NetStoatDipoleSink` / `NetStoatDipoleSource` are unused
since the dipole was deleted; they stay so the wire layout (and the protocol version) does not change — remove both with
the next protocol bump. The hole budget is `BlackHoleConfig.maxBlackHoles` 16; a swing refused for room lays nothing.

## 4. Files

| | |
|---|---|
| The swing: press, turn, fling, collapse, AI | `Executors/StoatSlingExecutor.cs`, `StoatSlingMath.cs`, `Data Containers/StoatSlingConfigSO.cs` → `StoatSlingConfig.asset` (+ the AI copies `Resources/AIBoostPolicies/StoatSlingConfig_{Slingshot,Regatta,Warpline}.asset`), `Data Containers/StoatSlingActionSO.cs` → `StoatSlingLeft/RightAction.asset` |
| The warp | `Executors/StoatPathfinderExecutor.cs` (draws nothing), `Data Containers/StoatDipoleConfigSO.cs` → `StoatDipoleConfig.asset` (`boost`, `boostRise`, `boostFadeSeconds`, `warpEvent`), `StoatDipoleMath.cs` (`AutopilotGauge`, `StepBoost`) |
| The ripple | `Executors/StoatRippleExecutor.cs`, `StoatRipple.cs`, `StoatRipplePass.cs`, `StoatRippleMath.cs`, `Data Containers/StoatRippleConfigSO.cs` → `StoatRippleConfig.asset`, `_Graphics/Materials/Graphs/StoatRipple.shader` |
| The mode | `Executors/StoatModeExecutor.cs`, `Data Containers/StoatModeActionSO.cs` → `StoatModeAction.asset` (Time only; bound to no input) |
| Triggers | `StoatSlingLeft/RightAction` on LT / RT; `StoatHoldAction` on X |
| HUD | `UI/Controller/StoatHUDController.cs`, `UI/View/StoatHUDView.cs`, `_Prefabs/UI Elements/VesselHUD/StoatHUDVariant.prefab` |
| Tests | `StoatSlingTests` (the swing, the registration set, and that no AI flies anything else or draws anything), `StoatRippleTests`, `StoatHullTests` |
| Prefab + assets | `Tools/Build/author_stoat_assets.py` (`--check`, `--self-test`) |
| Studio map | `Tools/Build/studio_to_unity/stoat.json` (25 rows: the warp, the swing's warp share, the black-hole look) |

Audio slots: `holdStartEvent`, `swingStartEvent`, `swingFlingEvent` (sling config), `warpEvent` (dipole config),
`rippleEvent`, `stripEvent` (ripple config), each a listed placeholder in `Docs/AudioSystem/sound_placeholders.json`.

## 5. Element map

`Assets/Resources/ElementalAbilityMaps/Stoat.asset` (labels kept: Catch Orbit, Timed Slingshot, Ripple, Heavy Launch; the
descriptions say the swing):

| Element | Input | Scales | L5 upgrade (gate) |
|---|---|---|---|
| **Space** | LT / RT | the swing's reach ×1 → ×1.4 (`StoatSlingConfigSO.orbitReach`) | **Accretion** — `StoatSlingExecutor.StepAccretion`, `IsUpgradeActive(Space)` |
| **Time** | passive | the warp ×2.5 → ×5 (`StoatDipoleConfigSO.boost`) | none (open) |
| **Charge** | passive | the ripple's reach ×1 → ×2 | **Siphon** — `StoatRippleConfigSO.Siphons`, `IsUpgradeActive(Charge)` |
| **Mass** | passive | the second push ×1 → ×2.5 (`launchPower`), shipped off | **Armoured Wake** — `StoatSlingConfigSO.ShieldsLaunchTrail`, `IsUpgradeActive(Mass)` |

## 6. What was deleted on 2026-11-21, and where its record is

Every file below is in git at `4e5df7090a` (`git show 4e5df7090a:<path>`); this file's own history there tells each
mechanic's design and play tests.

| Deleted | What it was |
|---|---|
| `Executors/StoatDipoleExecutor.cs`, `Data Containers/StoatDipoleActionSO.cs`, `StoatDipoleLeft/RightAction.asset`, `Resources/AIBoostPolicies/StoatDipoleConfig_{Slingshot,Warpline}.asset`, `Tests/Editor/StoatDipoleTests.cs` | the round-15 FIELD DIPOLE (Mass mode, out of the kit since 2026-11-02): its placement, field flight, path-watching AI and the prefab component (5137264980012345603) |
| `StoatPathfinderDots.cs`; in `StoatPathfinderExecutor`: Foresight, the trampoline's, skim's and catch's rings; in `StoatDipoleMath`: the field, `PredictPath`, the dots; in `StoatDipoleConfigSO`: 61 rows (the dipole, the path, the dots, Foresight, the dipole AI, `openEvent`, `annihilateEvent`) | the PATHFINDER's predicted path and Time 5's Foresight |
| `Data Containers/StoatSkimConfigSO.cs`, `StoatSkimMath.cs`, `StoatSkimConfig.asset`, `Tests/Editor/StoatSkimTests.cs` | the SKIM SLING (2026-10-29) |
| in `StoatSlingExecutor` / `StoatSlingMath` / `StoatSlingConfigSO` (56 rows) | the CATCH ORBIT (2026-10-14) and its inner orbit, flick launch, sweet-spot window and AI hold; the AIMED EXIT (2026-11-03); the TRAMPOLINE (2026-11-04) and its borrowed AI steering; the CRYSTAL style; the `swing` switch (the swing is always on) |
| in `StoatHUDView` | the Skim Sling's SKIP ×N counter |
| **2026-11-22:** `StoatPathfinderWorldDots.cs`; in `StoatPathfinderExecutor`: `DrawRings`, `AddSwingRings`, the camera lookup; in `StoatSlingExecutor`: the exit heading, the fling ring's state, the hole / reach getters; in `StoatSlingMath`: `Ring`, `RingNormal`, `SwingExit`; `StoatSlingConfigSO` `ringDotSpacing`, `ringDotSize`, `detectionRingAlpha`, `orbitRingAlpha`; `StoatDipoleConfigSO` `worldDotMinPixels`, `openColor`, `warpedColor` | everything the Stoat DREW: the swing's reach ring, dotted exit line and fling ring. Kept: `StoatPathDots.shader` / `Resources/StoatPathDots.mat`, because the Angler's marks (`AnglerWorldMarks`) draw with them |
| `Docs/AudioSystem/sound_placeholders.json` | 13 Stoat sounds whose slots went with them (skip, graze, plunk, capture, launch, chain break, both sweet spots, sling, trampoline touch / bounce, dipole open / annihilate) |

Tools that modelled the old kit keep running on its last shipped numbers: `Tools/Build/slingshot_stoat_sim.py` and
`Tools/Compute/evaluators/slingshot_fit.py` (the trampoline), `Tools/Build/stoat_orbit_sim.py` (every kit; `--swing` is the
shipped one). `Docs/Studios/StoatFlightStudio.html`'s `GAME_NAMES` marks the 34 deleted studio keys `lab`.

## 7. Status

2026-11-22: the drawing removed; both assemblies compile with Unity's Roslyn (0 errors); checklist entry "The Stoat draws
nothing".

2026-11-21: compiled with Unity's Roslyn against the open editor's references (Assembly-CSharp and Assembly-CSharp-Editor,
0 errors). The EditMode run and the hand test are recorded in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Every Stoat AI flies
the swing; no AI Stoat draws a line").

## 8. Debt and follow-ups

| # | Kind | Row |
|---|---|---|
| D7 | report | **The ripple bends what is in front of it** (no depth texture). |
| D10 | debt | **The Stoat's sim-authority test lives on the Manta's executor** (`MantaStingActionExecutor.IsSimAuthority`). Move it to a vessel-level home. |
| D13 | report | **`BlackHoleWaves` has no caller now**: its only producer was the deleted dipole. Keep it as an opt-in look or retire the pipeline with its studio rows (Garrett's black-hole system). |
| D14 | walked past | `Tools/Shaders/verify_stoat_ripple.py` fails under glslang 15.1 (its URP mock). |
| D17 | decision | **Mass scales nothing on the swing** while `swingKickShare` is 0 (Heavy Launch dormant), and **Time 5 is open**. Both are Yash's element-map calls. |
| D18 | report | `SlingshotOrrery.PressSide` (45 u, the old catch pair's side) still places Slingshot's moons; the swing lays its hole 35 u aside. |
| D19 | report | `R_VesselActionHandler.NetStoatDipoleSink` / `NetStoatDipoleSource` are unused; remove them with the next protocol bump. |
| D20 | report | **The four ability cards still picture the orbit** (attractor, repulsor, orbit ring, 90-degree window) and carry a hidden Foresight part. They are the icon kit's (`Tools/Build/icon_kit/specs/stoat.py`): redraw them for the swing with `/ability-icon`. |
| D21 | report | `StoatPathDots.shader` / `.mat` keep the Stoat's name but only the Angler uses them. |
