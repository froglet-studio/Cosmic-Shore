# The Tiger Moth

`VesselClassType.TigerMoth = 29`. The Butterfly's airframe with a different Time ability: the **Drop**. A tiger moth
escapes a bat by shutting its wings and dropping out of the sky; this one does the same, and comes out of it flying
the new way.

It exists because the drop beat the Fold in a blind A/B (Garrett, 2026-10-28: "the drop wins, bring it to unity as the
tigermoth"; the test page and its record are `Docs/Studios/ABTests/README.md` "Butterfly fold or drop v1",
https://claude.ai/artifact/7V29goD7bYi3uM23PFrMMJ). It ships as its **own hull class** beside the Butterfly, so the
Butterfly keeps its Fold, its wormholes and the Waystation race that is cut for the Fold. Whether the Butterfly is then
retired, renamed or kept is Garrett's call (Follow-ups).

## 1. The Drop

| | |
|---|---|
| **Press (hold LT)** | The wings shut over the back. The hull stops flying itself (the translation stance, and every turn axis at 0) and falls along its own **down**, the way its Dust-mode dust rains, easing up to 900 u/s in 0.12 s. It never turns. |
| **The camera** | Over 0.35 s the chase camera's frame pitches 90 degrees nose-down round the hull, so it rides 204 u above it looking down the fall, with the hull's nose at the top of the screen. Down reads as forward. |
| **Let go (or reach 1800 u)** | The landing: over 0.25 s the hull pitches 90 degrees nose-down INTO the frame the camera is already in, while its speed along the drop line eases from the fall speed back to the cruise it had before the press. The wings open and the wake comes back on. |
| **After** | It flies straight on along the line it fell. The camera does **not** level back out: the hull turned to meet it. A drop is a jump and a 90-degree turn in one. |
| **Recharge** | 30 s at rest, ×0.5 at Time 10, never worse than ×2 (the Fold's Time dial, unchanged). A press during the recharge does nothing; the Time card's veil shows the wait. |
| **Time 5, Far Drop** | The longest drop doubles (3600 u), as Far Fold doubled the reach. |

**The one change from the tested page.** The A/B's drop swung the camera back behind the hull after landing, with the
hull still facing the way it faced before. Garrett, on choosing it: "don't level the camera back out at the end because
we make a 90 degree downward rotation of the vessel as a result of using the ability, so it just keeps flying in the
new direction afterward." So the landing turns the HULL, not the camera.

**Why the turn is exactly 90 degrees.** The hull falls along its down and must fly on along that same line, so its
forward has to land on its old down: that is a 90-degree pitch and nothing else. `TigerMothDropMath.TurnDegrees` is a
constant for that reason, not a tuning knob.

**Why the landing moves no camera.** The camera rides `hull_at_press × pitch(90)` for the whole drop
(`CustomCameraController.FollowFrameOverride` at weight 1). The landing turns the hull from `hull_at_press` to exactly
that frame, so the slerp toward it stays on it, and handing the camera back to the hull at the end is a no-op
(`TigerMothDropMathTests.TheLanding_MovesNoCamera_WhileTheHullTurnsIntoTheFrame`,
`ReleasingTheFrameAfterTheLanding_IsNotACut`).

## 2. What runs where

| Thing | Where | Why |
|---|---|---|
| The press and the release | Every peer (the handler replays them) | |
| Shut wings, the stance, the frozen turn, the pen | Every peer; the owner writes the replicated stance | Everyone should see a Tiger Moth drop |
| The fall and the landing turn | **Owner only**, reaching the others through the vessel's owner-authoritative transform | One writer, as every flight |
| The camera frame | The local pilot's machine only (`VesselPlacementView.Frame` ignores any other vessel) | An AI or a remote copy must not move this machine's camera |

The drop is travel, not a teleport: `VesselTransformer.TeleportCount` does not move, the wake (when laid) is
continuous, and anything the hull flies through on the way (crystals, the dust capsule's contacts) is met on the way.

## 3. Files

| File | What |
|---|---|
| `R_VesselActions/TigerMothDropMath.cs` | Pure: the drop frame, the fall speed, the camera weight, the landing (turn and speed). Tested by `Tests/Editor/TigerMothDropMathTests.cs` |
| `Data Containers/TigerMothDropActionSO.cs` | The numbers (one shared asset, `VesselActions/TigerMoth/TigerMothDropAction.asset`); Time's recharge and Far Drop |
| `Executors/TigerMothDropExecutor.cs` | The drop: press, fall, landing, and the hand-back on every teardown path |
| `VesselTransformer.SetRotationImmediate` | Turns the nose and the command together, not a teleport. Its one caller is the landing |
| `CustomCameraController.FollowFrameOverride` / `FollowFrameOverride01` | A world frame the follow frame is slerped toward; a point-of-use surface like `RearView` and `FollowHeightScale`, reset with the follow target |
| `VesselPlacementView.Frame` / `ClearFrame` | Pushes that frame for the local pilot's vessel only, beside the Fold's placement point |
| `ButterflyHUDController.dropExecutor` | The Time card's recharge veil on this hull |
| `Tools/Build/author_tigermoth_assets.py` | Clones `Butterfly.prefab` into `TigerMoth.prefab` (one-shot), writes the action, the class asset, the element map and the registrations; `--check`, `--self-test` |
| `Tools/Build/author_touch_profiles.py` | The phone scheme: the Butterfly's (lift the right thumb to drop) |

Registrations: `VesselClassType.TigerMoth = 29`, `ToyVesselRoster.Default` (the Vessel Changer), the Vessel Prefab
Container, `DefaultNetworkPrefabs`, `SO_Class_TigerMoth` in both class lists (owned from the start),
`Resources/ElementalAbilityMaps/TigerMoth.asset`, `TouchControlProfiles.asset`, and `NetworkManager.prefab`'s
`ProtocolVersion` 24 (a new network prefab; 21-23 are taken on in-flight branches). No crystal hull fusion entries:
the Butterfly has none either.

## 4. Tuning knobs (`TigerMothDropAction.asset`)

| Knob | Shipped | Note |
|---|---|---|
| `cooldownSeconds` / `cooldownMultiplierAtFullTime` / `maxCooldownMultiplier` | 30 / 0.5 / 2 | The Fold's. The A/B ran 2.5 s so testers could use it every leg; whether 30 is right for the drop is open |
| `dropSpeed` | 900 u/s | The Fold's reach speed |
| `dropRange` / `upgradeRangeMultiplier` | 1800 u / ×2 | The Fold's reach and Far Fold |
| `dropEaseSeconds` | 0.12 | The A/B's |
| `landSeconds` | 0.25 | The A/B's landing time, now also the turn's |
| `cameraEnterSeconds` | 0.35 | The A/B's |
| `layWakeWhileDropping` | off | The A/B's default; on lays a vertical curtain |

Everything else (the hull, wings, dust, wake, camera offset, HUD) is the Butterfly's.

## 5. In-editor verification

Not run: this was written in a cloud session with no Unity editor. `bash Tools/Build/unity_refcompile/run.sh` (player
and editor configs) compiles it clean against real Unity references; that is a compile, not a play test.

1. Toybox, freestyle, open the **Vessel Changer**: **Tiger Moth** is offered. Pick it. It flies and paints like the Butterfly.
2. Hold **LT** (keyboard: left Shift). The wings shut, the hull falls straight down from its own belly without turning,
   and within about a third of a second the camera is above it looking down the fall, the nose at the top of the screen.
3. Let go. The hull pitches nose-down over a quarter second and flies on along the line it fell. **The camera does not
   move during the landing** and does not swing back afterwards.
4. Hold LT for over 2 s: the drop stops on its own at 1800 u and lands the same way.
5. Press LT again at once: nothing happens, and the Time card's veil sweeps for 30 s.
6. Drop in Dust mode: the dust capsule leads the fall. Drop in Mass mode: no wake is laid during the fall; it resumes at the landing.
7. Drop, then press Esc to the menu mid-fall, and come back: the camera follows normally (the frame was handed back).
8. **Phone:** lift the right thumb to drop, put it back to land.
9. **MPPM, two clients:** on the second client the Tiger Moth shuts its wings, falls and lands in the same place, pitched
   the same way; that client's camera does not move.

## 6. Follow-ups

- **Decision needed (Garrett):** the Butterfly. Retire it (and re-cut or retire Waystation, which is built on the
  Fold), rename it, or keep both hulls.
- **Decision needed (Garrett):** the recharge (30 s is the Fold's), whether a drop should leave something standing as
  the Fold's wormholes did, and whether it lays a wake.
- **The Time card's icon is still the Fold's** (the HUD is the Butterfly's). A drop glyph is `/ability-icon` work.
- **No AI drops.** No autopilot presses LT on this hull; an AI Tiger Moth flies and paints only. Its Time minigame
  (the lab rule: a race) does not exist yet.
- **Sound:** `dropEngageEvent` and `dropLandEvent` ship empty (the FMOD rule).
- **A fall at 900 u/s moves 15 u a frame at 60 fps**, so a small trigger can be stepped over (as any fast hull can).
- The landing turn is written on the owner only and reaches peers through the transform's interpolation; a remote copy
  may show the turn slightly later than the owner does.
