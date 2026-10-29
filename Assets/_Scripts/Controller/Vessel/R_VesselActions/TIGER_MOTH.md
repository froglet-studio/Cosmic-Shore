# The Tiger Moth

`VesselClassType.TigerMoth = 29`. The vessel that **replaced the Butterfly** (Garrett, 2026-10-29: "retire the butterfly,
the tiger moth replaces it"): the Butterfly's airframe, wings, dust, wake, camera and HUD, with the **Drop** as its Time
ability. A tiger moth escapes a bat by shutting its wings and dropping out of the sky; this one does the same, and comes
out of it flying the new way.

History: the drop beat the Butterfly's Fold in a blind A/B (2026-10-28, `Docs/Studios/ABTests/README.md` "Butterfly fold
or drop v1", https://claude.ai/artifact/7V29goD7bYi3uM23PFrMMJ), shipped as its own hull beside the Butterfly that day,
and took the Butterfly's place the next (§3).

## 1. The Drop

| | |
|---|---|
| **Press (hold LT)** | The wings shut. The hull stops flying itself (the translation stance, every turn axis at 0) and falls along its own **down**, the way its Dust-mode dust rains, easing up to 900 u/s in 0.12 s. It never turns. |
| **Incorporeal** | For the whole fall and landing every collider on the hull is off and the wormhole mouths ignore it: it passes through prisms, crystals, rival hulls and mouths, and touches nothing (Garrett: "it should be incorporeal and clip through everything during that time"). |
| **No trail** | The wake's pen is up, and every ribbon on the hull (tail, jets) is cut at the press (the laid part drains away as a ghost, as a teleport's does) and restarted at the landing: nothing streaks the line it fell along. |
| **The camera** | Over 0.35 s the chase camera's frame pitches 90 degrees nose-down round the hull, so it rides above it looking down the fall, the nose at the top of the screen. Down reads as forward. |
| **Let go** | The landing: over 0.25 s the hull pitches 90 degrees nose-down INTO the frame the camera is already in, while its speed along the drop line eases back to its cruise. It then flies straight on along the line it fell. The camera never levels back: the hull turned to meet it. |
| **No limit** | It falls for as long as the trigger is held. |
| **The recharge is paid by distance** | 20 s × (distance ÷ 900 u), capped at 20 s: a 90 u hop waits 2 s, a 450 u drop 10 s, a medium 900 u drop the full 20 s, and anything further is free (Garrett: "a little hop has a small cooldown. a medium distance should be the full 20. anything after medium distance is free"). Time's dial multiplies it (×0.5 at Time 10, never worse than ×2). The card's veil sweeps the wait this drop paid. |
| **The wormhole** | Only a drop that pays the FULL recharge leaves a pair of wormhole mouths (the Fold's, rim in the domain's hue, rivals pay a petal toll): one where the drop started, one where it landed. The pair stands until the next full drop replaces it or the hull is destroyed. A shorter drop leaves the standing pair alone. |
| **Time 5, Far Drop** | The medium distance doubles to 1800 u: every drop costs half, and the wormhole needs twice the fall. |

**Why the turn is exactly 90 degrees.** The hull falls along its down and flies on along that same line, so its forward
has to land on its old down: a 90-degree pitch and nothing else (`TigerMothDropMath.TurnDegrees`, a constant).

**Why the landing moves no camera.** The camera rides `hull_at_press × pitch(90)` for the whole drop
(`CustomCameraController.FollowFrameOverride`); the landing turns the hull into exactly that frame, so handing the camera
back is a no-op (`TigerMothDropMathTests`).

## 2. What runs where

| Thing | Where | Why |
|---|---|---|
| Press and release | Every peer (the handler replays them) | |
| Shut wings, stance, frozen turn, pen, colliders off, ribbons cut | Every peer; the owner writes the replicated stance | Everyone sees it drop and nobody's copy can hit it |
| The fall and the landing turn | **Owner only**, reaching peers through the owner-authoritative transform | One writer |
| The wormhole | Every peer, from the replicated press pose and the replicated pose at the moment that peer's copy of the hull finishes its landing turn | Poses every peer agrees on; the full-recharge test is made on the distance between them, so every peer makes the same call |
| The camera frame | The local pilot's machine only (`VesselPlacementView.Frame`) | |

The drop is travel, not a teleport: `TeleportCount` does not move.

## 3. The Butterfly is retired (2026-10-29)

Deleted: `Butterfly.prefab`, `SO_Class_Butterfly`, its element map, its hangar card art, and the Fold (`FoldActionSO`,
`FoldActionExecutor`, `ButterflyFoldAction.asset`). Unregistered from the Vessel Prefab Container, `DefaultNetworkPrefabs`,
the class lists, the toybox roster and the phone profiles. `VesselClassType.Butterfly = 13` stays in the enum (old data
still reads) and is marked retired.

Its four modes fly the Tiger Moth: **Waystation** (Time), **Dustup** (Charge), **Tapestry** (Mass), **Sirocco** (Space):
their cards, previews and AI seats (`vesselClass: 29`), and their generators (`arcade_mode_lib.VESSELS["TigerMoth"]`).
Waystation's autopilot aims its BELLY at the next ring before a long gap and drops (§3.1). The Butterfly's own components keep their names (`ButterflyDustField`,
`ButterflyAnimation`, `ButterflyHUDView`, `ButterflySpreadWingsAction`, the `Butterfly.fbx` model): the Tiger Moth is
built from them, and renaming them is a separate refactor.

### 3.1 Waystation's AI aims its belly (2026-10-29)

Garrett: "make the waystation AI aim its belly at the rings". `WaystationController.TryOverrideAim`, two phases, only
while the drop is ready and the next ring is at least `aiFoldMinDistance` (420 u) away and within 75 degrees of the nose:

1. **Nose on the ring** (the ring is the steering point) until the nose is within `aiBellyNoseDegrees` (3).
2. **Pitch straight up** to a point captured at that moment, the hull's up made square to the ring
   (`TigerMothDropMath.BellyAimNose`). A 90-degree pitch carries the old forward onto the new down, so the belly lands on
   the ring, off by exactly the nose's error. The point is HELD, not re-derived: re-deriving it every frame pulls it out
   of the pitch plane, the autopilot answers with yaw, and its yaw drags roll (it banks into a yaw), which left the belly
   18-75 degrees off in an offline model of the autopilot's steering (six ring directions, roll coupled to yaw as
   AIPilot does it); the held pitch left it 0-2.8 degrees off in the same six (the hull's own travel during the pitch
   is not in that model).
3. **Drop** once the belly is within `aiDropAimDegrees` (10), holding for `TigerMothDropMath.HoldSecondsFor` (the ease in
   and the landing included, exact to a unit), so it lands short of the ring's plane by `aiFoldLandShort` plus twice
   the miss its aim carries and flies the rest. The drop is travel, so a ring it crosses on the way is threaded.

It gives up after `aiBellyAimMaxSeconds` (6) and flies on; the steering point is marked a LEAD
(`GateRaceController.OverrideAimIsLead` -> `AIPilot.MarkAimAsPathLead`) so the orbit break-off leaves it alone.
Waystation's step guard (`maxPlausibleSpeed`) is 1000 u/s now: the drop's 900 u/s is 15 u a frame at 60 fps against the
old 400's 18 u, so a hitch could have declined a ring the drop really crossed. The Fold-era helpers
(`FoldAimLimitDegrees`, `FoldHoldSeconds`) and `WaystationFoldTests` are retired with the Fold.

## 4. Files

| File | What |
|---|---|
| `R_VesselActions/TigerMothDropMath.cs` | Pure: the drop frame, the fall speed, the camera weight, the landing, the distance-priced recharge. Tested by `Tests/Editor/TigerMothDropMathTests.cs` |
| `Data Containers/TigerMothDropActionSO.cs` | The numbers (one shared asset, `VesselActions/TigerMoth/TigerMothDropAction.asset`): recharge, Far Drop, the wormhole and its toll |
| `Executors/TigerMothDropExecutor.cs` | The drop, the wormhole, incorporeality, the cut ribbons, and the hand-back on every teardown path |
| `Controller/Vessel/VesselIncorporeal.cs` | The vessels passing through everything right now; read by `WormholeMouth` |
| `VesselTransformer.SetRotationImmediate` | Turns nose and command together; not a teleport |
| `CustomCameraController.FollowFrameOverride` / `VesselPlacementView.Frame` | The camera's re-oriented follow frame, local pilot only |
| `ButterflyDustField` | Also lights every prism in the live capsule (`lightsPrisms`, `litStrength`; `Docs/LIT.md`) |
| `Tools/Build/author_tigermoth_assets.py` | `--check` holds the hull, its registrations and the Butterfly's retirement; `--self-test` |

## 5. Tuning knobs (`TigerMothDropAction.asset`)

| Knob | Shipped |
|---|---|
| `fullCooldownSeconds` | 20 (Garrett) |
| `fullCooldownDistance` | 900 u (one held second): the "medium" drop. Not specified by Garrett: a starting number |
| `upgradeDistanceMultiplier` (Far Drop) | ×2 |
| `cooldownMultiplierAtFullTime` / `maxCooldownMultiplier` | 0.5 / 2 |
| `dropSpeed` / `dropEaseSeconds` | 900 u/s / 0.12 s |
| `landSeconds` / `cameraEnterSeconds` | 0.25 / 0.35 |
| `gateRadius`, portal window, toll | the Fold's (33 u; 15 petals per element) |

## 6. In-editor verification

Not run: written in a cloud session with no Unity editor. `bash Tools/Build/unity_refcompile/run.sh` (player and editor
configs) compiles it clean against real Unity references; that is a compile, not a play test.

1. Toybox Vessel Changer: **Tiger Moth** is offered, the **Butterfly is not**.
2. Hold LT: wings shut, the hull falls from its belly without turning; within ~0.35 s the camera is above it looking down
   the fall. Fly the drop through a wall of prisms and through a crystal: nothing breaks, nothing is collected, no hit.
3. No ribbon or wake is drawn along the fall; the tail starts again from the landing point.
4. Let go: the hull pitches nose-down over a quarter second and flies on; the camera neither moves nor swings back.
5. A short hop (~0.1 s): the Time card's veil sweeps about 2 s. A 1 s drop: 20 s, and a pair of wormhole mouths stands at
   the start and end. A 3 s drop: still 20 s. A short hop after it: the pair is still standing.
6. Fly a rival hull through the pair: it pays the toll. Drop THROUGH a mouth: it is not carried.
7. Dustup, Tapestry, Sirocco and Waystation each start with Tiger Moths. In Waystation, before a long gap an AI turns
   its nose onto the next ring, pitches straight up (belly onto the ring), drops, and flies the rest into the mouth.
8. MPPM, two clients: the second client sees the fall, the landing turn and the same wormhole pair.

## 7. Follow-ups

- **Decision needed (Garrett):** the medium distance (900 u) is a starting number; and Far Drop's new meaning (it used to
  double the reach, which no longer exists).
- **The Time card's icon is still the Fold's.** A drop glyph is `/ability-icon` work.
- **The Waystation AI's belly aim is unplayed**: the offline model is a stand-in for AIPilot's steering, so how often it
  lines up in a real race, and whether 3 / 10 degrees are the right gates, is for the editor (and the learning sweep).
- **Sound:** `dropEngageEvent`, `dropLandEvent`, `gatesOpenEvent`, `gateThreadEvent` ship empty (the FMOD rule).
- The dust's light takes one of PrismLit's 8 bank slots per Tiger Moth in Dust mode (Dustup seats four).
- A remote copy shows the landing turn slightly later than the owner (transform interpolation), and its wormhole waits for
  that turn (up to `gateSettleSeconds`).
