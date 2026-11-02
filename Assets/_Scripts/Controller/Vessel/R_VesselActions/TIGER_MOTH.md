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
| **The wormhole** | Only a drop that pays the FULL recharge leaves a pair of wormhole mouths: one where the drop started, one where it landed. By default it is NEUTRAL: the material's own rim, everyone rides free (§1.1). The pair stands until the next full drop replaces it or the hull is destroyed. A shorter drop leaves the standing pair alone. |
| **Dust mode cleanses you** | A drop pressed while the dust is raining falls through it: your silence lifts and your temporary debuffs end (§1.2). |
| **Charge 5, Toll Gate** | The wormhole strips the elements of opposing pilots who thread it (15 petals of each element, left on the mouth as crystals) and wears your domain's hue. A standing pair switches live when the upgrade unlocks or relocks (§1.1). |
| **Time 5, Afterburn** | For 3 s after the landing the hull stays incorporeal (its dust still rains) and flies a throttle boost that eases from ×2 back to cruise. |

### 1.1 The level-5 upgrades, re-scoped (2026-10-29)

Garrett: "change L5 charge so the portal strips the elements of opposing players (this should have been the case by
default before so we will need to change the portal from the neutral to domain hue once this unlocks). change Time L5
is to keep your invulnerability and a speed boost for 3 seconds after the drop ends."

| | Was | Now | Where |
|---|---|---|---|
| Charge 5 | **Monarch**: the dust bites twice as deep | **Toll Gate**: the wormhole tolls rivals and wears the domain hue; without it the mouths are neutral and nobody pays | `TigerMothDropActionSO.TollsRivals` (the replicated unlock bit, every peer); `TigerMothDropExecutor.FollowTollUpgrade` switches a standing pair; `WormholeMouth.SetDomainTolled` |
| Time 5 | **Far Drop**: the medium distance doubles | **Afterburn**: 3 s incorporeal and a speed boost after the landing | `TigerMothDropActionSO.HasAfterburn`; `TigerMothDropExecutor.End(landed, afterburn)` keeps the colliders off (the dust's back on) and calls `VesselTransformer.ModifyThrottle(2, 3)` on the owner |

The old gates are switched OFF (vessel rule 26): Monarch's `upgradeElement` is gone from
`ButterflyScaleDustDebuffBySkimmerEffect.asset` and its SO with the bite itself (the dust shreds since 2026-10-31,
§1.3), and Far Drop's `upgradeDistanceMultiplier` is gone from the SO and the asset. `author_tigermoth_assets.py --check` holds both off and both new cards on (with negative controls).

An owned mouth now wears its owner's hue only while it tolls (`WormholeMouth.ApplySurface`): the colour is the toll's
sign. The only other mouths, the crystal wormhole's, have no owner and were already neutral.

### 1.2 The dust cleanses allies (2026-10-29)

Garrett: "have the dust clear silence and debuffs of allies. this applies to the omnicrystal dust and if you dust
yourself by using drop in dust mode (the omnicrystal also dusts yourself)." One verb, `ElementalCleanse.Cleanse`: the
silence window lifts (`VesselSilence.Lift`) and every TEMPORARY negative elemental effect ends
(`ResourceSystem.ClearElementalDebuffs`). A petal a rival took is a transfer, not a debuff, and is not handed back
(that would mint it; lifeforms are the only source). Three paths reach it:

| Path | How |
|---|---|
| The Dust-mode capsule sweeping an ally | `VesselCleanseAllyBySkimmerEffectSO` on the dust's skimmer container (`TigerMothDustCleanseAllyBySkimmerEffect.asset`); rivals are the bite's |
| The omni-crystal bloom | `ButterflyBloomDust` cleanses, once each, its pilot and every ally its wavefront reaches, by position against the roster (the blast's own vessel effects bite, and its `affectSelf` gate keeps its side out of them) |
| A drop pressed in Dust mode | `TigerMothDropExecutor.Engage` cleanses the pilot |

Every peer cleanses its own copy (silence is kept per peer; the contacts are simulated on every peer). A silence
extension still on the wire when the dust lands re-silences the pilot on the peer it reaches late: lifts are not
relayed (SILENCE.md relays extensions only).

### 1.3 The dust shreds (2026-10-31)

Garrett: "have the charge ability shred a random element petal every second and decrease the cooldown to .1 seconds at
charge 10", and "most things take all four elements. this is one random element. as always score and silence should be
proportional to petals, but this is one petal not 4." The petal is stolen (the contact verb), not burned.

| | Was | Now |
|---|---|---|
| What one contact takes | a slice of every element (−0.33 each × Charge's bite, 0.5× → 2×) | ONE petal of ONE random element the rival holds |
| How often | once per pass (a skimmer runs its vessel effects on entry), latched 1 s | every `shredInterval` for as long as the rival stays in the dust: 1 s at Charge 0, 0.1 s at Charge 10, floored at 0.1 s |
| What Charge scales | the bite's size | the shred's RATE |
| Silence | a Strike's worth (3.2 petals) per pass | one petal's worth per shred |
| Score | one Strike per pass | one Strike per 3.2 petals shredded (a Strike's fleet price: ten points to the petal, on four elements) |
| A rival with no petals | scored and silenced anyway | not admitted: no score, no silence |

| Piece | Where |
|---|---|
| The shred (pick, steal, interval) | `VesselElementalDebuffBySkimmerEffectSO` (`IPacedContactPetalTake`); its asset is `ButterflyScaleDustDebuffBySkimmerEffect.asset` (`petalsPerShred`, `shredInterval`) |
| The admission, silence and score per petal | `VesselCombatHitBySkimmerEffectSO.ExecutePaced`, `CombatHitDrain.TryAdmitPetals` / `PetalsFor`, `VesselSilence.OnPetalsAdmitted` |
| The standing contact | `ButterflyCombatHitBySkimmerEffect.asset` `repeatWhileInside: 1`; `SkimmerImpactor` keeps the hulls inside it (only while its container holds a repeating effect) and `ReapplyVesselEffectsToOverlapping` re-runs those effects; `ButterflyDustField.Update` calls it every frame the dust is live |
| The numbers | `author_butterfly_dust.py` (`SHRED_INTERVAL_*`), held by its `--check` and by `author_tigermoth_assets.py --check` (with negative controls) |

The element is picked among those the rival holds by their REPLICATED level, with an integer hash of a running serial
(never `UnityEngine.Random`, vessel rule 18). Only the machine that owns the Tiger Moth picks; the rival's owner settles
the one element named, and clamps to what they hold. The interval reads the Tiger Moth's replicated Charge. Frame
quantisation: the reporter runs once a frame, so at 30 fps the 0.1 s shred lands every 0.1-0.133 s.

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

**Waystation is the Drop Tower race since 2026-11-01**: the clusters with a staircase of SHORT HOPS through floor plates
in every gap but the last (built as Waystation Prototype, 76, and folded in). It changes nothing in this kit
(`WAYSTATION.md` "The Drop Tower", `Docs/Studios/TIGER_MOTH_TIME.md`).

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

**A ring abeam is lined up by ROLL** (since the exit gates face square to the next cluster, WAYSTATION.md "The square
exit gate"): between 75 and `aiBellyRollMaxDegrees` (125) off the nose, the autopilot steers the heading square to the
ring and `AIPilot.MarkBellyTarget` replaces the roll stick with one that turns the belly onto it (offline: 200 of 200
starts lined up within 2.61 s; the yaw-coupled bank managed 33). Affects: Yash's AI (`AIPilot`), an opt-in per-frame
hook that no other provider calls.

It gives up after `aiBellyAimMaxSeconds` (6) and flies on; the steering point is marked a LEAD
(`GateRaceController.OverrideAimIsLead` -> `AIPilot.MarkAimAsPathLead`) so the orbit break-off leaves it alone.
Waystation's step guard (`maxPlausibleSpeed`) is 1000 u/s now: the drop's 900 u/s is 15 u a frame at 60 fps against the
old 400's 18 u, so a hitch could have declined a ring the drop really crossed. The Fold-era helpers
(`FoldAimLimitDegrees`, `FoldHoldSeconds`) and `WaystationFoldTests` are retired with the Fold.

## 4. Files

| File | What |
|---|---|
| `R_VesselActions/TigerMothDropMath.cs` | Pure: the drop frame, the fall speed, the camera weight, the landing, the distance-priced recharge. Tested by `Tests/Editor/TigerMothDropMathTests.cs` |
| `Data Containers/TigerMothDropActionSO.cs` | The numbers (one shared asset, `VesselActions/TigerMoth/TigerMothDropAction.asset`): recharge, Afterburn, the wormhole and its toll, and the two level-5 gates |
| `ImpactEffects/EffectsSO/Helpers/ElementalCleanse.cs` | The cleanse (§1.2); `Vessel Skimmer Effects/VesselCleanseAllyBySkimmerEffectSO.cs` is the dust's ally branch |
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
| `afterburnSeconds` / `afterburnThrottle` (Time 5) | 3 s (Garrett) / ×2 easing to ×1 (a starting number) |
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
6. Below Charge 5 the pair's rim is the material's pale neutral and a rival rides it free. Reach Charge 5 (the toybox
   element dial): the standing pair turns your domain's hue and the next rival through it pays the toll. Drop
   THROUGH a mouth: it is not carried.
6a. Time 5: after a landing the hull passes through prisms for 3 s, visibly faster at first, and its dust still works.
6b. Get silenced (or take a danger-prism sting), then have an ally Tiger Moth dust you in Dust mode: the silence
   indicator clears. Fire an omni-crystal: you and allies inside the bloom clear too. Drop in Dust mode while
   silenced: it clears.
7. Dustup, Tapestry, Sirocco and Waystation each start with Tiger Moths. In Waystation each cluster's last ring faces
   across the line to the next cluster: thread it, roll, drop, and you land flying into the next coil. An AI does the
   same (it rolls its belly onto the next ring after the exit gate, then drops).
8. MPPM, two clients: the second client sees the fall, the landing turn and the same wormhole pair.
9. The shred (§1.3), toybox with an AI rival in Dust mode: hold the dust on a rival at Charge 0. The rival's flowers
   lose one petal of ONE element about once a second, and yours gain it; a different element is picked from shred to
   shred. Raise Charge to 10 (the element dial): about ten petals a second. A rival with no petals left loses nothing
   and is not silenced. In Dustup the score rises by one dusting per ~3.2 petals while you stay on them.

## 7. Follow-ups

- **Decision needed (Garrett): Dustup's target.** Ten dustings was set for ten passes; with the shred it is 32 s of dust
  contact at Charge 0 and 3.2 s at Charge 10 (§1.3, `DUSTUP.md`). Re-tune after a playtest.
- The bite sound plays when the dust first reaches a rival, not per shred (`scaleDustBiteEvent`, empty).

- **Decision needed (Garrett):** the medium distance (900 u) and the afterburn's ×2 throttle are starting numbers.
- The afterburn keeps the whole hull incorporeal, so crystals pass through it for those 3 s too (the dust stays on).
- ~~The Time card's icon is still the Fold's.~~ Done 2026-10-31 by `/ability-icon` (`specs/tigermoth.py`): the moth with
  its wings shut falling, its wormhole drawn only at Charge 5 Toll Gate (red as a rival pays), driven by
  `ButterflyHUDView.SetDrop`; the Charge card (Scale Dust) shows a grey rival turning red on a bite (`SetScaleDust`).
- **The Waystation AI's belly aim is unplayed**: the offline model is a stand-in for AIPilot's steering, so how often it
  lines up in a real race, and whether 3 / 10 degrees are the right gates, is for the editor (and the learning sweep).
- **Sound:** `dropEngageEvent`, `dropLandEvent`, `gatesOpenEvent`, `gateThreadEvent` ship empty (the FMOD rule).
- The dust's light takes one of PrismLit's 8 bank slots per Tiger Moth in Dust mode (Dustup seats four).
- A remote copy shows the landing turn slightly later than the owner (transform interpolation), and its wormhole waits for
  that turn (up to `gateSettleSeconds`).
