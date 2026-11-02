# Ark feel pass (DONE)

Branch `claude/voyage-ark-feel` (off `origin/claude/voyage-night` e8e3c6164), worktree `D:\Voyage\wt-a`, 2026-11-01.
It answers Garrett's playtest notes, except the freestyle and scene-split part, which belongs to `claude/voyage-scene`.
Paths are under `Assets/_Scripts/` unless stated.

## What changed

1. **Damage alarm** (`Controller/Toys/ArkwayRun.Alarm.cs`, `UI/ObjectiveIndicator.cs`, `UI/ObjectiveArrowGraphic.cs`).
   - Every `Ark.HullPrismLost` (a real loss; a payload taken from the hold is not one) turns the Ark's objective arrow Fire, `(1, 0.33, 0.10)`, the Rhino's danger tint, for 5 s.
   - Each further loss restarts the 5 s.
   - While it burns, the arrow also marks the Ark when the Ark is on screen and close (`ObjectiveIndicator.ShowWhenOnScreen`). Normally it hides there.
   - The off-screen edge arrow wears the same colour.
   - When the alarm ends, `ClearAccent` puts back the arrow's own lime.
2. **Lighting** (`Controller/Environment/ArkModel.cs`, `ScriptableObjects/Toys/ArkModelDefinitionSO.cs`).
   - Root cause: the vessel materials the model wears (Body and every domain ship material) are *transparent with no depth write*. On a 196-unit ship, the cavity glow, the brood and the far side's neon drew through the hull, so it read as a lit lantern you could see across.
   - The Ark's per-Ark copies are now opaque and write depth (`MakeOpaque`). They stay double-sided, as the chaos pass made them, so the outside still renders.
   - Each slot is toned by a new "Look" block on the definition: Body 0.45, Domain 0.45, Window 0.55 and Engine 0.6 on `_ColorMultiplier`.
   - Slots are identified by material, not by submesh index. The FBX's submesh order is not the build's slot order: the first cut painted the bulkhead with the engine ember.
   - Bloom and the shared materials are untouched.
3. **Solid Ark, one passage** (`Controller/Environment/ArkHullSolid.cs`).
   - The volume is a signed distance over the hull envelope, ported from `Tools/Build/vessel_model/specs/ark_reverse.py`. It covers the deck and shell fan, the umbo, the spine and side pods, the jaws, the eight jets in their rings, the lance and the keel fin.
   - The hangar bay and the ring tunnel (the spec's own spline, carried out past the keel) are carved out. They are the only open passage, with no window into a room.
   - Each frame, a vessel inside the envelope is set back on the surface (1.2 margin) and its nose turned along it, so it slides and does not pass through. The engines are solid.
   - Vessels only, on their own authority. Creatures, rounds, blasts and abilities are untouched, so every prism is still shot and grazed as before: plates as food, the §0.x armour wear, the super-shield absorb and the regrowth.
   - A **bulkhead** closes the stern gape just ahead of the jets, at rho 117.2 from the hinge, from the deck to the shell's underside. It is notched under each groove so the gills keep their slots. It is drawn in the hull's own Body material, with two rows of Window-lit port slits per bay: the quarters and stores behind the wall. Through the gape you now see wall and ports, not the whole cavity.
   - The model (FBX) itself is unchanged.
4. **Leash boundary** (`Controller/Environment/ArkLeashShell.cs`, `Resources/Voyages/ArkLeashShell.shader`).
   - The existing leash is unchanged: `ArkwayRun.TickLeash`, 3 cell radii, 5 s grace, then a recall to the Ark's flank. It is now drawn as a ghostly lattice sphere on the Ark at that radius.
   - The shader is additive with no depth write. It draws a drifting three-way lattice with shimmer, a fresnel rim, and a bright reveal patch where the pilot is about to cross.
   - The sphere fades in from 80% of the leash and is full at 97%. It pulses while the pilot is past it.
   - The countdown is the voyage's existing toast line ("RETURN TO THE ARK — n"). It resets when the pilot comes back within about 0.95 of the leash, the existing hysteresis.
5. **Cell shards** (`Controller/Environment/Cell.cs`, `Controller/Environment/Cytoplasm/SnowChanger.cs`, `Controller/Voyages/ArkEscortRun.cs`).
   - The existing cytoplasm system is reused. The new `Cell.SatelliteCytoplasmEnabled` opt-in lets a satellite cell run its `SnowChanger`, which:
     - rebinds to the satellite's own runtime (`SnowChanger.Rebind`), so its shards point at that world's crystal;
     - builds when the core crystal is handed over (`InitializeWhenCrystalArrives`);
     - is thinned at 1.5x spacing, about 30% of the authored count, because three worlds stand at once.
   - Its only caller is `ArkEscortRun.PrepareCell`.
6. **ECOSYSTEM.md §41.6** records both exceptions (a solid Ark for vessels only; satellite cytoplasm in escort worlds). Neither touches §0.
7. **Tools.**
   - `-arkEscortShots <dir>` (`Controller/Voyages/ArkEscortShots.cs`) renders the first escort's Ark, tunnel and leash shell to PNG, plus a game-view HUD capture after a hull loss.
   - `-arkEscortShotsOnly` gives the escort up after the shots.
   - The sim now closes the first-run privacy overlay **unanswered** (nothing granted; analytics stays off). That overlay had begun holding the sim's boot in Authentication ("never ready"), probably because the full EditMode run reset the editor's privacy prefs.

## Verified

- Batchmode compile is clean (0 `error CS`).
- The full EditMode run: 4807 tests, 4777 passed, 30 failed.
  - The 30 failures are all outside this work: commerce, crystal-hull bakes, Goby, Stoat, Theater, Touch, Soap, and so on.
  - Every Ark, escort, voyage and cell suite passed, including the new `ArkHullSolidTests` (6 tests).
  - After the final fixes, the Ark and voyage subset passed 65/65.
- `ArkHullSolidTests` checks that:
  - the body, umbo, fan, spine and jaws are solid;
  - all eight jet pivots and barrels are solid;
  - every tunnel point and the hangar are open;
  - no bow-to-stern, beam or top-to-bottom line through the hull's middle gets through;
  - the bulkhead sits on its radius and faces aft;
  - the look is toned.
- **Sims.** Lock-aware and silent: `run_sim.ps1`, time scale 3, d4. Plan `4:Sparrow,Sparrow,Sparrow;4:Sparrow,Squirrel,Dolphin;4:Sparrow,Sparrow,Sparrow`, output `D:\VoyageLogs\arkfeel_d4.jsonl`, log `sim_arkfeel_d4d.log`. All three sailed and survived.

| run | result | hull min | kills on the Ark | plates lost | friendly fire |
|---|---|---|---|---|---|
| Sparrow x3 | Survived | 8% | 158 | 99 | 4 |
| Sparrow+Squirrel+Dolphin | Survived | 31% | 151 | 79 | 2 |
| Sparrow x3 | Survived | 8% | 340 | 99 | 8 |

  The chaos ladder's d4 (r3 and r4) won 8/8, with lows of 3-49% and 175-238 kills on the Ark. These numbers are in that band, so there is no regression in kills or hull. Friendly fire is single digits, as before.

## Screenshots (batch renders from the sim)

- Before: `D:\VoyageLogs\shots0\` (`ark_front34.png`, `ark_stern34.png`). The white, see-through Ark.
- After: `D:\VoyageLogs\shots3\`:
  - exterior: `ark_front34`, `ark_stern34`, `ark_side`, `ark_top`, `ark_below`, `ark_stern_close` (the bulkhead), `ark_wide`;
  - tunnel: `tunnel_mouth`, `tunnel_hangar`, `tunnel_inside`, `tunnel_exit`, `tunnel_exit_inside`;
  - boundary: `leash_inside`, `leash_outside`;
  - HUD: `hud_alarm.png` (game view after a hull loss; the Fire marker sits over the Ark).
- Intermediate: `D:\VoyageLogs\shots1\`, with the materials mis-slotted, and `D:\VoyageLogs\shots2\`, with the body at 1.0, pale.

## For Garrett to check by eye

- **Hull tone.** Body 0.45 and Domain 0.45 look right on the renders, but the renders are stills from a sim camera. The numbers are on `ArkReverseModel.asset` (Look) and can be changed live in the inspector.
- **Back faces.** Model parts wound inward still draw in the lighter rim colour from inside (the tunnel walls look pale grey close up), because the unlit vessel shader does not flip a back face's normal.
- **Flying into the hull.** Nobody has flown it by hand. The push-out is position plus a gentle nose turn. Check that it feels like sliding along a hull, not a stutter, and that the tunnel (radius 4.4, about 3.2 clear for the vessel's centre) is flyable in your vessel. The shield-plate grooves are inside the solid now; only edge-out danger blades are still touchable.
- **The leash shell** is fairly strong up close (`leash_inside`). The colour is `ArkwayRun.Alarm.cs` `LeashShellColour`, and the line strength is in the shader.
- **Cell shards** add about 30% of a scene cell's field per escort world, three worlds at a time. Watch the frame rate on mobile.
- **Privacy prompt.** The editor's privacy prompt (age gate) appears to have been reset on this machine (likely by the full EditMode run), so your editor may ask once.
