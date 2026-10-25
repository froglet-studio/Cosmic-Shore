# Vessels on a phone — mobile audit of the whole fleet (report for the core four)

**From:** an audit session, 2026-10-21, at `bleeding-edge` `077edc3e`. **Asked by:** Garrett ("do a mobile friendly audit
on all vessels. what doesn't translate. what can be fixed, what needs a solution").
**Update 2026-10-21 (same day):** Garrett decided the open questions and the phone controls were built on this
branch - see **§8** first. Sections 1-7 are the audit as it stood before; §8 says what changed for each row.

**Method:** I read the code and assets of all 18 hulls and the shared touch layer (`TouchInputStrategy`,
`AbilityCardPress`, `FlightTouchClaims`, `DeviceOrientationHandler`). **No phone was used, and no change was made.**
A line marked *(inferred)* is reasoning from code that a phone test still has to confirm. Every other line was read in
code or YAML. The five load-bearing claims were re-checked by hand: §2 rows 1, 2, 4 and 5, and §1's FlipAction finding.

Tags: ✅ works on a phone · 🔧 **fixable** (a bounded change, named) · 🧩 **needs a solution** (a design call; the
options are given, and Garrett decides as vessel owner unless another owner is named).

## 1. In one minute

- **Two vessels cannot be flown properly on a phone today, and both are one-line fixes.**
  - **The Scarab never moves.** Its thrust reads only the analog right trigger, which a phone never writes, and its
    minimum speed is 0. Scarab Scramble is a Scarab-only mode, so **the whole mode cannot be played on a phone**.
  - **The Urchin backs away at full speed whenever the pilot lets go of the glass.** It also does this from spawn,
    before the first touch.
- **The biggest problem is fleet-wide: lifting a thumb to tap an ability card also fires that thumb's ability.**
  - The card row sits bottom-right, under the right thumb. Moving that thumb off its stick is a "thumb lift" gesture,
    and on most hulls a thumb lift *is* an ability.
  - Some examples: a Grizzly tap arms a full bomb, a Sparrow tap fires a Skyburst rocket, a Brittle Star tap throws a
    spare ring, a Squirrel tap starts a drift, and a Shrike tap spends its sling.
  - Card taps (merged 2026-10-20) passed the phone round, but that round tapped with a **third** finger, so it never
    hit this case.
- **Two-trigger and analog-depth abilities do not translate.**
  - A phone has no trigger depth, and it can never hold "LT + RT" by gesture: lifting both thumbs means "let go of
    everything".
  - What is lost:
    - the Rhino's sword: no swing, no energize, no combos (its three cards press nothing);
    - the Manta's Soar boost: card only, and the card press also starts a Yastri turn;
    - the Angler's paint and erase;
    - the Serpent's scope zoom;
    - the Thresher's aimed launch;
    - the Shrike's zoom-and-fire together;
    - the Stoat: **no ability works on touch at all**. This was on purpose: its doc says "Steam is the target".
- **Turning the phone (FlipAction) almost certainly does nothing.**
  - Nothing enables the accelerometer that `DeviceOrientationHandler` reads, and Input System sensors start disabled.
    I checked this by grepping for `EnableDevice`.
  - The screen is locked to LandscapeLeft anyway, so the "flip" would mean holding the phone upside down.
  - On a phone, RB abilities (Brittle Star Charge Mode, Grizzly Weapon Systems, Shrike Charge mode) are reachable only
    through their card.
- **No phone has ever been measured** (`Docs/PERFORMANCE_OPTIMIZATION.md`). Six vessels render the world a second time
  or add a full-screen pass, and only one of them (the Shrike eyepiece) has a handheld tier. The Brittle Star can keep
  1,400 to 4,000 rounds alive per hull.

### Fleet scorecard

"Flies" means basic flight works with thumbs. "Abilities" counts what a phone pilot can actually trigger today, by
gesture or card.

| Vessel | Flies | Abilities on a phone | Worst problem | Verdict |
|---|---|---|---|---|
| Squirrel | ✅ | all (drift and ring by lift, passives) | card taps start a drift; idle snap | 🔧 |
| Dolphin | ✅ | all (drift by lift, Echo by lift or card) | slow nose on touch | ✅ / 🔧 |
| Manta | ✅ | Yastri ✅, **Soar card only** and it also turns | no boost gesture | 🧩 |
| Rhino | ✅ | Ramp ✅, **sword: contact only** | swing, energize and combos unreachable | 🧩 (Astro League, Cleave, Headlong) |
| Urchin | ❌ reverses on let-go | Spikes, Track and Slip ✅ | full reverse at 65 u/s with no thumbs | 🔧 **urgent** |
| Sparrow | ✅ | all | card tap fires Skyburst; 180 prisms/s in stance | 🔧 |
| Serpent | ✅ | Cloak, Pellets, Seed Wall ✅; **scope never zooms**; the shot only via card | scope is a full second render that adds nothing | 🔧 + 🧩 |
| Grizzly | ✅ | all, at maximum size | every card tap arms a full bomb | 🧩 |
| Scarab | ❌ **no thrust** | Drift, Switch, Juke ✅; Snap Dash ❌ | cannot move; accidental jukes on one thumb | 🔧 **urgent** + 🧩 |
| Termite | ❌ (any device) | none designed | creeps to the world origin; not spawnable | 🧩 retire or design |
| Falcon | n/a | none | old name of the Brittle Star; not spawnable | 🧩 retire |
| Shrike | ✅ | modes ✅, RT and LT by lift; **zoom + fire impossible** | eyepiece in raw pixels, no safe area | 🔧 + 🧩 |
| Butterfly | ✅ | Fold and Dust by lift | any re-grip of the right thumb Folds | 🧩 |
| Thresher | ✅ | Plant and Winch by lift; Mass card; **launch view unreachable** | Mass card brakes or yanks | 🧩 |
| Stoat | ✅ | **none** (no touch bindings) | Steam-only by design | 🧩 ship on phone? |
| Brittle Star | ✅ | all, guns always at full rate | one-thumb mirror splays the guns; pool size | 🔧 + 🧩 |
| Angler | ✅ (good fit) | Lure, Dash ✅; **Paint, Erase, Squad, Recall ❌** | the core verb (paint) is impossible | 🔧 + 🧩 |
| Wrasse | ✅ | crew boost by lifting the left thumb (RT), named on the Time card (third round, §8) | second base camera every frame | ✅ boost; perf open |

## 2. Fixable now (🔧), ordered by impact

Each row is bounded: one file or one asset, and no design call. The owner is Garrett (vessels and touch input) unless a
row says otherwise.

| # | Fix | Where | Hulls |
|---|---|---|---|
| 1 | **Urchin: zero throttle while idle.** `ReadThrottle` returns `(XDiff - 0.5)/span`. With no thumbs down, `ResetInput` sets `XDiff = 0`, which reads as full reverse. Return 0 when `InputStatus.Idle`. Do not change the fleet's "lift both thumbs to brake". | `GunVesselTransformer.cs:801-807`, `BaseInputStrategy.cs:41-46`, `TouchInputStrategy.cs:217` | Urchin |
| 2 | **Scarab: a stopgap throttle on touch.** `ReadThrottle01` returns 1 for autopilot only. Treat `ActiveInputDevice == Touch && !Idle` the same way. Do it here, not by writing the trigger channel, which would break every hull's binary drift. The proper control is decision D8. | `ScarabVesselTransformer.cs:123-127` | Scarab |
| 3 | **Cards that do nothing should not swallow touches.** `pressable` is "not locked and input not 0", whatever the device. A card whose input does not resolve on touch still claims the touch, which leaves a dead zone under the right thumb. Decide `pressable` with `ResolvesOnActiveDevice` (siblings included) and refresh it on a device change. | `AbilityLockupView.cs:1277-1285`, `AbilityCardPress.cs:100-124` | Rhino (3 sword cards), Stoat, Angler (Space, Charge), Scarab (Throttle) |
| 4 | **Do not start an ability twice.** `StartPressedActions` has no "already held" guard. A lift gesture plus a card on the same event starts it twice, so a toggle flips twice and nothing changes. Ignore a press of an input already held. | `R_VesselActionHandler.cs:339-350` | Butterfly Dust, Squirrel drift card, any toggle |
| 5 | **Fix the idle snap on the Squirrel and Serpent.** Puppetry writes `Pitch = -2p, Yaw/Roll/Throttle = 2x`; `Idle()` writes `+p, x`. The moment the last thumb lifts, pitch flips sign and the rest halve. Route both through one setter. `S29` in the refactor backlog misses this, and the Serpent audit wrongly passed it as eased. | `MantaAnimationContoller.cs:26-29` vs `:42-45` (also on Termite and Falcon) | Squirrel, Serpent |
| 6 | **Ease the stick in the three snapping animations** (vessel rule 53; the Thresher is the pattern). | `StoatAnimation.cs:259-268, 322-338`, `AnglerAnimation.cs:139-172`, `ButterflyAnimation.cs:224-226` | Stoat, Angler, Butterfly |
| 7 | **Give the nose its touch response.** Only five prefabs set `touchNoseResponse`. The others keep a ~0.67 s nose lag that the touch steering curve was tuned to remove. Set it to 9 on Dolphin, Manta, Rhino and Urchin. `SingleStickVesselTransformer.RotateShip` ignores the field entirely, so the Angler's 9 does nothing and no one-thumb hull gets it; route it through `NoseFollowFraction`. | the 4 prefabs; `SingleStickVesselTransformer.cs:55-62` vs `VesselTransformer.cs:576, 599-605` | 4 dual-stick + all 9 one-thumb hulls |
| 8 | **Serpent: zoom the scope on touch.** Zoom reads only `LeftTriggerAnalog`, so on a phone the scope opens at the unzoomed 60°: a full second render that adds nothing. Return full depth while engaged on touch (keyboard already writes 1). | `SniperScopeActionExecutor.cs:244` | Serpent |
| 9 | **Angler: paint and erase from a press.** Paint and erase read only the analog triggers. Copy the Thresher's press-held fallback (`rt = max(analog, held)`, `ThresherExecutor.cs:348-351`), and map the trigger events, plus their "only" twins in touch overrides, so holding the Space card paints and letting go throws. The brush width on touch is decision D5. | `AnglerCommandExecutor.cs:527-554`, `Angler.prefab:970-985` | Angler |
| 10 | **Rhino: swing on a phone.** `_touchActionOverrides` is empty and the swing actions live only in the gamepad overrides. Bind touch OnlyLeft and OnlyRight to `RhinoShieldSwipeLeft/RightAction`. That gives the swing pose only; energize is D4. | `Rhino.prefab:1235-1240` | Rhino |
| 11 | **Wrasse: the crew boost must not slow the hull.** On a phone the boost is "lift both thumbs", but `ResetInput` has already set `XDiff = 0`, so the boost multiplies zero and the hull eases to MinimumSpeed. Hold the pre-idle `XDiff` while a crew boost runs, or move the boost to a card. | `KiteNavigator.cs:971-975`, `VesselTransformer.cs:998-999` | Wrasse |
| 12 | **Scarab and Brittle Star: the one-thumb mirror misfires right-stick abilities.** One thumb is copied onto **both** sticks. So on the Scarab every turn past 35% jukes and every full turn fires the Cavitation Blast, and on the Brittle Star a hard turn splays the five guns 25°. On single-stick hulls, do not copy onto the right stick (or flag the mirror on `InputStatus` for these readers). | `TouchInputStrategy.cs:527-537`; `ScarabJukeController.cs:280`; `BrittleStarExecutor.cs:401-406, 578, 864` | Scarab, Brittle Star |
| 13 | **Shrike eyepiece and Thresher zoom window: scale them and keep them in the safe area.** The Shrike eyepiece canvas has no CanvasScaler: 2 px crosshair lines and fixed 16/220 px margins, about 1.5 mm on a 400 dpi phone, and the notch can cover it. | `ShrikeScopeOverlay.cs:44-56, 75-78, 206-208`; the Thresher launch-sight zoom window | Shrike, Thresher |
| 14 | **Invert Y on touch.** The touch `Reparameterize` never reads Invert Y or Invert Throttle (the gamepad does, `GamepadInputStrategy.cs:238-247`). Touch settings silently do nothing. | `TouchInputStrategy.cs:508-543`; one-thumb `Pitch()` (`ONE_THUMB_MOUSE_CONTROLS.md:278-285`) | fleet |
| 15 | **Grizzly claw cone: guard the shader lookup.** It tries URP/Unlit, then `Unlit/Color`, then calls `new Material(shader)` with no null check. A player build that strips both throws. Add it to T-20261019-41bb (see §5 for that task's other corrections). | `GrizzlyClawConeVisual.cs:122` | Grizzly |
| 16 | **Thresher: wire `lineMaterial`.** The ball material is wired; the line falls back to `Shader.Find`. It works today only because Sprites/Default is always included. Then close the Thresher's part of T-41bb. | `Thresher.prefab:1316` | Thresher |
| 17 | **Termite: close the coverage-test hole.** `OneThumbVesselCoverageTests` leaves `CommandVesselTransformer` out of its transformer list, so the Termite passes "exactly one transformer" while carrying two. | `OneThumbVesselCoverageTests.cs:90-99` | Termite |
| 18 | **Squirrel and Manta: tidy-up.** Each prefab carries a switched-off `PipCamera` with a 640×320, 4× MSAA render texture (`Pip.Awake` disables it). The Manta HUD variant also keeps a 350×350 raycast `Boost Button/display` near the card row, a suspect for missed card taps (refactor row S30). | `Pip.cs:79-96`; Manta HUD variant | Squirrel, Manta |

## 3. Needs a solution (🧩): the decisions

The rows are in order of how much of the fleet they unblock.

**D1. Lifting a thumb to reach a card fires that thumb's ability (fleet-wide, the top item).**
- Cause: a 2→1 thumb change raises OnlyLeft/OnlyRight **and** Left/RightStickAction the moment it happens
  (`TouchInputStrategy.cs:273-293, 444-461`). `FlightTouchClaims` only ignores a finger that *starts* on a card, and
  that finger lands after the lift has already fired.
- Letting go of the screen does the same thing unless both thumbs leave in the same frame.
- `Docs/ABILITY_LOCKUP.md:346-348` reads as if the claim fixed this.
- Options:
  - **(a) A lift grace window:** hold the lift edge back ~100 ms and cancel it if a card touch begins or the touch
    count reaches 0 inside the window. This costs ~100 ms of latency on every lift ability.
  - **(b) Move the pressable cards out of the right-thumb zone on touch,** for example to the top edge for an index
    finger.
- **Recommendation: (a).** It fixes the release case too, which (b) does not.

**D2. Card size and placement on touch.**
- *(inferred)* A card is about 6.5 mm wide with a 1.5 mm gap on a 1080p phone. Apple asks for at least 44 pt (~7 mm)
  and Android for 48 dp (~7.6 mm); 9 mm is the comfortable size.
- The row covers about the right 22% of the screen, where the right thumb flies.
- No minimum touch-target size exists anywhere.
- Options: a touch layout scale on `AbilityLockupStyle`, or a touch-only layout. **Owner: Shombith (UI) with Garrett.**
  The four-icon row's structure is LOCKED; its size and position are not.

**D3. Nothing teaches the gestures.**
- Key-cap chips are hidden on touch (`InputDeviceIconSetSwitcher.cs:113`), and the thumb rings are suspended. A phone
  pilot is never told "lift your right thumb to Fold".
- Options: thumb-lift glyphs on the cards for touch, or an FTUE beat per vessel. **Owner: Garrett with Shombith**
  (FTUE guided-path rule).

**D4. Two-trigger abilities (LT + RT, BothSticksAction).**
- Touch never raises `BothSticksAction`, and two lifts cannot coexist (the second one is "let go of everything").
- Today's only pattern is "lift one thumb, then hold a card with it". It works for the Dolphin's drift + Echo and the
  Serpent's scope + shot.
- Affected:
  - **Manta Soar** (boost is card only, and the card press also starts a Yastri turn);
  - **Rhino energize, combos and the super-shield pop.** Astro League, Cleave and Headlong are Rhino-only, so phone
    players lose the sword there. Bots and pad pilots have it.
  - **Shrike zoom + fire;**
  - **Thresher launch view** (both triggers full during a spin);
  - **Sparrow Skyburst + Pulsefire together.**
- Options:
  - (a) Make "lift one thumb + hold a card" the documented touch idiom, and give each such hull the card it needs.
  - (b) A per-vessel touch mapping. For example: on touch, Plant alone qualifies the Thresher's launch view; a held
    Rhino card writes both trigger mirrors, as the keyboard does; the Manta boosts on "thumbs spread wide".

**D5. No trigger depth on touch.**
- Every analog ability runs at full pull on a phone:
  - the Brittle Star fires at 450 rounds/s and overheats in ~6 s;
  - every Grizzly bomb is maximum size;
  - the Thresher's Plant always slams to a stop;
  - the Scarab's drift is always the sharp tier;
  - the Angler's brush (once fix 9 lands) is always full width.
- Options:
  - (a) **A hold-time ramp as the fleet standard.** The Stoat already does it (`StoatSlingExecutor.cs:435-438`,
    `Hold01`), and it works on every device.
  - (b) An authored "touch depth" per vessel (the Brittle Star holds five guns at the cooling rate at ~0.42).
- Pick one so the next vessel does not invent a third.

**D6. Does the Stoat ship on phones?**
- `STOAT.md` says Steam is the target, so it has no touch bindings.
- If yes, the work is bounded:
  - touch OnlyLeft → Dipole + Sling Left, and OnlyRight → the Right versions;
  - Button1 → Hold, Button2 → Mode;
  - the squeeze already falls back to hold time.
- The black-hole lens needs a mobile tier first (§4).

**D7. Face-button abilities with no card (Angler Squad and Recall).**

*Resolved 2026-10-21 by removal: the Angler's school is two groups the throw alternates, so there is no squad pick or recall to bind (`ANGLER.md` §1m).*
- The four-card row is LOCKED, so Button3 and Button4 have no tap target on a phone.
- Options: make the squad label over the Mass card tappable to cycle the squad, or a long-press on the Mass card for
  Recall.

**D8. The Scarab's throttle on touch** (after stopgap fix 2).
- "Right thumb down = throttle" collides with "lift right = drift", and the right thumb's travel is already the juke.
  Snap Dash (double-tap the throttle) has no touch form.
- Options: (a) auto-cruise on touch, brake when both thumbs lift; (b) a throttle slider.

**D9. Butterfly: any re-grip of the right thumb Folds.**
- A ~0.34 s lift already passes `minGateSeparation`, which lays a wormhole and replaces the team's existing one
  (`FoldActionSO.cs:76-80`).
- Options: lifts under ~0.35 s cancel with no teleport, or the Fold commits only after a short hold.

**D10. Wrasse across two phones.**
- One phone runs one human plus an AI in the other seat. The seat's board, motes, AI and effects run on that one
  machine (`KITE_NAVIGATOR.md:307-312, 347-348`).
- Which machine owns the effects when the two seats are two phones is open.
- Separately, the navigator's 21 mm "aim above the thumb" offset makes about the bottom third of the board
  unreachable on a short screen *(inferred)*. Fading the offset near the bottom edge is a 🔧.

**D11. Termite and Falcon.**
- Neither is in `Vessel Prefab Container.asset`, so neither can spawn.
- The Termite has no abilities and its command transformer creeps to the world origin: `ThreeDPosition` is never
  written, because touch builds it into a local and drops it.
- The Falcon is the Brittle Star's old name (`ABOUT_PROJECT.md:23`).
- Retire, or design. No mobile work is worth doing first.

**D12. FlipAction from turning the phone.**
- It is dead today (§1). If it were turned on, a 0.1 g threshold with no hysteresis would toggle modes from wobble.
- Options:
  - (a) **Retire orientation → FlipAction** and say "card only" in the docs. Recommended: the screen is locked to
    LandscapeLeft, so the gesture means playing upside down.
  - (b) Enable the sensor, with |x| ≥ 0.5 held for 0.3 s.
- `Button4Action` now exists as a cleaner home for mode toggles.

## 4. Performance on a phone (all unmeasured: the first phone capture is the gate)

`Docs/PERFORMANCE_OPTIMIZATION.md` records that no phone has been measured. `PlatformProfileSO` has no budget for
second cameras, projectiles or lens passes; only the Shrike eyepiece and the fold-gate window have tier levers. Ranked
by likely cost:

| Hull | What costs | Today | Lever to add |
|---|---|---|---|
| Stoat | Black-hole lens: one full-screen pass, up to 8 wells per pixel, a colour copy, and the depth texture forced on for every base camera while live. The menu camera keeps rendering in flight. | 2.7 ms/frame in the Editor (T-20261017-9990) | half-resolution lens, fewer wells, `deactivateMenuWhileFlying` on mobile |
| Wrasse | A second **base** camera with the full culling mask and skybox, every frame, in both seats. Instanced draws use 100,000 u bounds and are never culled. | no tier | small view at a lower rate or into a small render texture |
| Thresher | Launch-view picture-in-picture: a second render with post-processing, 540 px at 30 Hz | no tier | Shrike-style handheld tier (360 px, 20 Hz, no post) |
| Serpent | Scope: the same picture-in-picture, which zooms nothing on touch today (fix 8) | no tier | same |
| Butterfly | Wormholes: up to 2 exact views per frame plus panorama faces; MobileHigh render scale is 1 | MobileLow is 0.5 | MobileHigh 0.5, or 1 exact render on mobile |
| Shrike | Eyepiece: second render; the far plane goes to 20,000 against the main camera's 12,000 | handheld tier exists ✅ | none needed |
| Brittle Star | 1,400 rounds prewarmed per hull at spawn (AI hulls too), max 2,400. Up to ~4,000 alive at Space 10 on full-rate phone bursts, each running a swept query every frame. Ring explosion: ~5,900 vertices rebuilt every frame for 0.7 s, not `MarkDynamic`. `updateWhenOffscreen` skins every hull. | no cap | per-tier pool and fire-rate caps; `MarkDynamic`; upload triangles once |
| Sparrow | Turret stance lays 180 permanent prisms/s; the fire loop is peer-local, so a peer under 22.5 fps lays fewer *(inferred)* | — | profile; `firingRate` per tier |
| Rhino | Five blade-tracer trail renderers always emitting (~250 vertices each at 840 u/s) plus overdraw | — | scale min-vertex distance with speed, or drop on low tier |
| Stoat, Angler | Dynamic meshes of up to 4,096 and 2,048 quads rebuilt every frame; the Angler runs 16 sphere queries a frame while painting (`brushInterval` 0) | — | rebuild on change; `brushInterval` > 0 on mobile |
| Urchin | Chain-reaction budget is 6 volleys per frame on every platform | — | lower on Handheld after a capture |
| Manta | Kabloom cascades (fuse 70, kabloom 140 × 1.6) | — | measure |

Other phone-only gaps:
- **Haptics are silent on phones.** `PlayMobilePattern` is empty (`HapticController.cs:512-530`).
- `liblofelt_sdk.so` still ships 4 KB-aligned. Google Play blocks updates without 16 KB alignment from 1 Feb 2027
  (`Docs/ANDROID_BUILD.md`).

## 5. Docs that disagree with the code (fix in the same pass as the vessel work)

- `BRITTLE_STAR.md` F1 and `SCARAB.md` §3.1 / §15 item 9 say face-button abilities are unreachable on touch. They have
  been reachable by card since 2026-10-20 (9718a382).
- `BRITTLE_STAR.md:35` and task T-20261016-3e80 say turning the phone toggles Charge Mode. It does not (D12).
- `ABILITY_LOCKUP.md:346-348` overstates what touch claims fix (D1).
- `SQUIRREL_DRIFT.md:519` says the phone pilot "lifts both thumbs (boost)". No code path does that.
- `BUTTERFLY_FOLD.md:3-4, 121-125` still describe the sticks placing the ghost, which was retired.
- `InputEvents.cs` comments: FlipAction is RB (not LB), and Button1/2/3 are A/B/X. `InputHintBindingMap.cs:64` shows
  Button2 as R, but the keyboard raises it on T.
- `Docs/REFACTOR_BACKLOG.md` S29 lists the Stoat and Angler only. It misses the Squirrel, Serpent and Butterfly (fixes
  5, 6).
- T-20261019-41bb (runtime `Shader.Find`):
  - It lists the Brittle Star, Shrike, the Thresher line and SniperBeam's line. Those try the always-included
    Sprites/Default first, so they are likely fine.
  - It misses the Butterfly's URP/Particles/Unlit fallback (unused while the prefabs stay wired).
  - It misses the Grizzly cone's crash risk (fix 15).
- `Tools/Build/touch_drift_slip.py` holds only the Squirrel. The Thresher has the same touch overrides on events 12 and
  11.
- T-20261016-5ed6 and Brittle Star F13 say trigger depth does not replicate. It does: `InputStatus.cs:42-43, 107-117`
  is an owner-write NetworkVariable. The real gap is that the press RPC and the depth arrive at different times.

## 6. What a phone test should confirm

Run on an Android development build, in the Toybox Vessel Changer:

1. Urchin and Scarab before and after fixes 1 and 2.
2. With two thumbs flying a Grizzly, Sparrow and Squirrel, move the right thumb to a card. D1 predicts a bomb, a
   rocket and a drift.
3. Turn the phone upside down on a Brittle Star. D12 predicts nothing happens.
4. Measure a card with a ruler (D2).
5. One Profiler capture each:
   - Stoat dropping a dipole;
   - Wrasse in either seat;
   - Thresher in its launch view;
   - Serpent scoped;
   - Brittle Star at full fire;
   - Butterfly beside a wormhole.

Whoever runs it: Yash has offered to take phone tests, and Garrett ran the last phone round.

## 7. Where each finding came from

- **Shared touch layer:** `Assets/_Scripts/Controller/IO/TouchInputStrategy.cs`, `FlightTouchClaims.cs`,
  `DeviceOrientationHandler.cs`, `Assets/_Scripts/UI/Elements/AbilityCardPress.cs`,
  `AbilityLockupView.SetSlotPressInput`.
- **Per vessel:** the prefab's `R_VesselActionHandler` bindings (`_inputEventShipActions`, `_touchActionOverrides`,
  `_gamepadActionOverrides`), `Assets/Resources/ElementalAbilityMaps/<Vessel>.asset`, `element_ability_table.py`, the
  executors named above, and the design docs in `Assets/_Scripts/Controller/Vessel/R_VesselActions/`.
- **Phone ↔ pad mapping:**
  - Right-thumb lift = LT alone (events 2 + 12). Left-thumb lift = RT alone (1 + 11).
  - Both thumbs off = IdleAction plus "release everything".
  - Thumbs spread / squeezed with no steering = FullSpeedStraight / MinimumSpeedStraight.
  - Button1–4 and BothSticks = card tap only. FlipAction = card only (D12).

## 8. Decided and built (2026-10-21)

Garrett's answers to §3, built on `cece/admiring-hamilton-gyzzh8`. **None of it has been flown on a phone or opened
in the Unity Editor** - the hand tests are `Docs/UNITY_VERIFICATION_CHECKLIST.md` "Phone controls for the whole
fleet" (steps 1-15). The machinery is one per-hull row in `Resources/TouchControlProfiles`
(`Tools/Build/author_touch_profiles.py`, whose `--check` also fails if a phone button would press nothing) and the
touch scheme in `Docs/ABILITY_LOCKUP.md` "On a phone".

| Decision (Garrett's words, shortened) | What was built |
|---|---|
| "urchin solution is that idle should have parity with controller idle" | Both thumbs off now reads a released pad (`XDiff` 0.5) on every hull, so the Urchin holds course instead of reversing. `IdleAction` is pressed once (it was re-pressed every frame) and only after the first touch. Fixes the Wrasse's boost too (§2 row 11). |
| "any vessel that requires thumb movement onto the icons ... no lifting thumb input action, just the totems as buttons", sized and placed ergonomically on every screen | **TotemButtons** scheme: Urchin, Sparrow, Serpent, Grizzly, Scarab, Shrike, Brittle Star, Stoat, Angler raise nothing on a thumb lift; cards plus extra buttons are laid out round the right thumb in millimetres (`TouchButtonLayout`: 10 mm, down to 7 mm, in a 36 % × 55 % corner, inside the safe area; measured 8.5 mm for six buttons on a 6" 1080p phone, 7 mm on a 4.4" 720p one, 10 mm on a tablet). Closes D1 and D2 for these hulls. |
| Brittle Star: "all four totems and two fire buttons ... The fire button can become the right joystick once held ... rotated without firing by touching down elsewhere" | Fire and Ring discs whose drag is the right stick while held (`FlightTouchClaims.AddAim`); a second right-hand touch aims without firing; one thumb no longer splays the guns. |
| Scarab: "a throttle ... slider that is set. the juke ... from anywhere with the right thumb. drift becomes a button, and the ring is another button" | Set-and-hold slider writing `InputStatus.Throttle` (the Scarab reads it on touch: it now moves, and Scarab Scramble is playable); Drift disc; Switch card; juke from a second right-hand touch; the steering thumb no longer jukes. |
| Dolphin: "combine the scope and the drift into one when you lift either thumb ... recover [roll] with the phone's gyro" | Either lift runs drift + charge + trail + Echo Sight; tilting the phone like a wheel rolls while one thumb flies. **Changed 2026-10-22** ("1 to 1 roll like augmented reality not a control that sends the vessel into a roll ... 90 degrees on both sides"): the aim's roll is now an ANGLE, not a rate - the hull rolls exactly as far as the phone has turned since the aim began, up to 90 degrees either side, read from the phone's fused pose (attitude sensor: gyro + compass; gravity fallback). `TouchAttitudeRoll`, `TiltMath.RollBetween`, `IInputStatus.HoldsRollAngle`, `VesselTransformer.ApplyHeldRoll`. **Changed 2026-10-25** ("dolphin 1 thumb controls are too sensitive around the home ... I like that it can turn sharp but it should have a nonlinear response curve"): one-thumb deflection r becomes 0.4 r + 0.6 r^3 before the mix (`TouchControlProfile.oneThumbResponseCurve` 0.6, `TouchInputStrategy.ShapeOneThumb`); turn at quarter / half / three-quarter thumb 0.08 / 0.21 / 0.46 (was 0.19 / 0.41 / 0.67), the rim unchanged. |
| Manta: "fly ultra fast when you lift both thumbs and then steer with the gyro" | Both thumbs off = Soar at full throttle (720 u/s), steered by tilt. **Changed 2026-10-22** ("the manta should flip its gyro controls both left/right and up/down were the opposite of my expectation"): the Manta's tilt is turned over on both axes (set by `author_touch_profiles.py`; Invert Y still applies on top). **Changed 2026-10-25** ("manta gyro has its yaw controls flipped from intended"): yaw is back to unflipped, pitch stays turned over (`TouchControlProfile.flipTiltSteerPitch` / `flipTiltSteerYaw`). The Thresher's launch aim is unchanged. |
| Rhino: "energize by lifting both thumbs ... go quicker" | Both thumbs off pulls both trigger mirrors (energize); the touch energize is twice as fast. A single lift now swings that side's blade (the pad's mapping). |
| "The stoat should join the mobile fleet" | Touch bindings mirror the pad; Space card, Dipole Right and Hold discs, Mode card. |
| Angler: "work with 1 thumb controls ... while painting or erasing" | Paint and erase now work from the held Space and Charge cards (a press counts as a full pull); squad and recall discs (both retired 2026-10-21, `ANGLER.md` §1m: the four cards are the whole cluster). |
| "the serpent's scope should open to max zoom" | Full zoom on touch; the Scope card latches (tap on, tap off) so the Charge card can shoot. |
| "the shrike's zoom should be a toggle button on mobile" | Zoom disc toggles; Trigger disc fires the mode. |
| "the thresher's aimed launch should be both thumbs off and aimed with gyro" | Planted, lift the other thumb too: the launch view comes up, tilt aims the crosshair, a thumb back launches. |
| "passive cards should not swallow taps" | A card that presses nothing on the device in hand (passive, locked, or unbound on touch) neither presses nor claims the finger. |
| "create glyphs for thumb lifts for the vessels that use them" | Five glyphs (`Tools/Build/touch_glyph_art.py`): lift left, lift right, lift either, lift both, thumbs apart, under the cards of the ThumbLifts hulls (Squirrel, Dolphin, Manta, Rhino, Butterfly, Thresher). Those cards are no longer buttons, so reaching for one cannot fire a lift by accident; the Thresher's Mass card stays a button and its lifts wait 0.12 s for it. |
| "fix the idles" | Squirrel, Serpent, Sparrow (scale and sign mismatch), Stoat, Angler, Butterfly (raw stick) now ease to rest. |
| Arena: an indicator between the scores and the end condition, clickable, showing the two hulls the D-pad would go to | `PilotSwapIndicator`: the left/right swap targets as class icons, tappable, at least 9 mm. |

Also done because the work needed it: orientation no longer raises FlipAction (D12 option a; tilt now enables the
accelerometer, which would have woken the old 0.1 g flip); the Grizzly claw cone can no longer throw on a stripped
shader (§2 row 15).

**Still open from this audit:**
- **D9:** the Butterfly's re-grip Fold.
- **D10:** the Wrasse across two phones.
- **D11:** Termite / Falcon.
- ~~**§2 row 7:** the touch nose response~~ and ~~**§2 row 14:** Invert Y on touch~~: done 2026-10-21, wider
  than asked - see "Second round" below.
- **§2 rows 13 and 16-18:** the remaining small fixes.
- **§4:** every performance lever. No phone capture yet; T-20261021-d476.
- **AI Dolphins on a phone host** resolve presses as Touch, so their drift now also lights Echo. This is harmless,
  noted in `DOLPHIN_ENERGY_ECONOMY.md` §2b.
- ~~**Snap Dash on touch**~~: an upward flick on the slider now (second round, below).
- **A drift on a phone is always the sharp tier** (no trigger depth), on every drift hull.

### Second round (Garrett, 2026-10-21, same day)

| Garrett | Built |
|---|---|
| "get rid of all steering lag" | **No human pilot's nose lags on any device or hull any more.** `VesselTransformer.NoseFollowFraction` makes the nose the commanded rotation each frame for this machine's human pilot (pad, keyboard, mouse, touch), and every transformer (base, single-stick, gun, command) now goes through it; the single-stick override used to hard-code the 0.67 s lag, so no one-thumb hull ever got the touch fix. A leftover gap from a discontinuous command closes at 1.5 x the hull's own max turn rate (a fast turn, never a snap). AI, autopilot and remote hulls keep the fleet's 1.5/s: the autopilots model it. The per-hull `touchNoseResponse` is retired (five prefabs). The Stoat's foresight reads the hull's own rate. The tilt is read unsmoothed from the gravity sensor (only a raw-accelerometer fallback is smoothed). |
| "... and inconsistent y-inversions that are not left to the settings" | **One Invert Y rule** (`BaseInputStrategy.ApplyInvertSettings`): the setting turns the vertical axis over everywhere flight reads it, on every device. It used to flip only the two-stick pitch and roll, so on a pad or keyboard it did nothing on the nine one-thumb hulls; touch never read it (nor Invert Throttle); dual mouse pitched the other way by default; and the tilt carried its own "forward dives" switch. Now a push up pitches every hull the same way by default, the setting flips all of it, and tipping the phone forward is a pushed-up thumb. `InputInversionTests` holds it (and fails on the old dual-mouse line). Not changed: dual mouse's throttle runs the other way from the pad's (an X inversion, reported, not touched). |
| "wrasse and grouper, are two seats. players can select, change seats etc. both can be played on mobile or pc." | **D10 decided.** For one human, both seats are now playable on every device, and the seat is picked, not toggled: keys 1 / 2 pick the Grouper / the Wrasse, the D-pad and the phone's double tap change seat, and the pick is remembered (`CrewSeatChoice`). A phone's both-thumbs-off boost now really speeds the Grouper up (idle parity), and never spends a boost on the frames after a seat change. In an arena the seat owns the swap keys; the top-bar tiles swap hulls. **Two humans in one hull (either device)** was built on `cece/relaxed-tesla-axhxzy` (2026-10-23, untested in the Editor): the swap gesture - the D-pad, the left / right arrow keys, a two-finger swipe - steps every person round one list of the domain's seats, and keys 1 / 2 no longer pick a seat (`Docs/CREW_STATIONS.md`; T-20261021-aa19's progress line). |
| "why can't the scarab just record a swipe with the right thumb for the snap dash?" | It does: a quick upward flick that starts on the throttle slider fires the Snap Dash (Time 5), and the slider goes back to where it was set. On the slider rather than open glass because the juke already owns every right-thumb push past 35 %. |

### Third round (Garrett, 2026-10-21, same day)

| Garrett | Built |
|---|---|
| "The grouper can not use boost on mobile. Fix this oversight and others" | **The Grouper boosts on a phone by lifting the left thumb** and flying on with the right, the fleet's touch twin of RT, and the Wrasse's Time card shows that glyph (a new Wrasse row in `TouchControlProfiles`). The cause: `KiteNavigator` read `InputStatus.RightTriggerAnalog`, which no phone writes, so a phone had only "both thumbs off", which dropped the steering for every boost and was named on no card. RT is now an input binding like every other hull's (`CrewTriggerActionSO`, shared by the crew hulls), one path through `R_VesselActionHandler` on every device. **The others:** a sweep of every analog-trigger reader in vessel code found one more with no phone path, the **Orca's helm** (boost on RT, shield on LT), now on the same bindings (lift left = boost, lift right = shield); the **Honeyguide**'s RT Peck and LT Flit already worked by lift but their cards named nothing, so it gets glyphs too. The **Goby & Pistol** has no touch path at all (primer and flares on keys 3 / 4 and the D-pad, the Pistol on the mouse); it is under active development, so it is a task, not a change (`T-20261021-2d13`); the phone playtest of the three is `T-20261021-723e`. `author_touch_profiles.py --check` now also fails a card whose glyph names a thumb lift that nothing is bound to. |

### Fourth round (Garrett, 2026-10-21, same day)

| Garrett | Built |
|---|---|
| "i can not switch to the pistol on mobile" / "these should made consistent with other vessels. each seat has its own ui and controls. no need to use the dpad in a weird way" | **The Goby & Pistol on a phone** (closes `T-20261021-2d13`). A double tap on the small view swaps seats, as on the Wrasse. The Goby's primer and flares are input bindings now (`CrewButtonActionSO`: pad X / Y, keys Q / F; the D-pad's up / down and keys 3 / 4 are gone), and each seat shows its own two buttons round the right thumb while the person is in it (`GobyPistolButtons`, the Orca gunner pattern): Primer and Flares with their charges, Ghost and Snap dimmed while cooling. The Pistol dives and launches by finger (tracked by touch id). The Goby has a `TouchControlProfiles` row with the Squirrel's lift glyphs. Phone playtest: `T-20261021-52b0` and the seat-buttons task. |

### Fifth round (Garrett, 2026-10-22)

| Garrett | Built |
|---|---|
| "it should not have the squirrels abilities ... it should be a goby with its own kit" | **The Goby & Pistol's own kit** replaces the fourth round's Primer and Flares (`goby-and-pistol.md`, top section). On a phone the Goby perches and aims with one thumb and its seat shows **Dart** (hold to charge, let go) and **Lock**; the Pistol's seat shows **Ghost**, **Snap** and **Back** (`GobyPistolButtons`). Nothing is bound to the Goby's thumb lifts, so lifting a thumb while aiming fires nothing (its `TouchControlProfiles` row has no glyphs). Pad: RT dart, LT lock (X / Y too). Phone test: `T-20261021-52b0` and the kit's test task. |
