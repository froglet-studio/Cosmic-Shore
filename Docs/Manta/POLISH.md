# Manta polish log

The Manta's kit is stable and complete; this log takes it from "works" to "feels great" (handling,
readability, sound, how well its AI flies it). Each round answers ONE question. Newest round last.
The kit itself is `Assets/_Scripts/Controller/Vessel/R_VesselActions/MANTA_STING_KABLOOM.md` (§0 wins).

## Round: the steady rumble in the Toybox cell (2026-11-11)

**Question.** Why does a low, steady gamepad rumble persist for minutes while the Manta flies through
the Toybox cell, and how do we stop it without losing a haptic that means something?

**What good looks like.** The pad is quiet unless something just happened to the pilot. A skim pulse
says "that paid"; flying through dense prisms with nothing to gain is silent.

**Found (read from the code and assets, bleeding-edge 548ed93).** Not the old trail sizing:
`ScoutTrailPrismScaler` polls `PrismSpatialIndex` and touches no haptic. The rumble was the skim pulse.
The Manta's skimmer is 20-40 units across (Space), and its container carried the fleet's per-entry
`SkimmerHapticsByPrismEffect`. Every prism entering the sphere called `HapticController.PlaySkim`,
which re-plays at most every 30 ms a clip that lasts 70 ms. In dense prisms (the cell, and the
Manta's own trail waking its colliders inside the sphere when it flies slower than the radius per
second) the clip never ended, so the motors held one steady level, whether or not the bay could take
any more energy (capacity 1, 0.34 a skim: full after three).

**First try (rejected).** Pulse only on a skim that pays energy. Caleb flew it for 7 minutes in the Toybox
and felt nothing, which confirmed the hum was gone but left the pad silent.

**Decided (Caleb, 2026-11-11).** No haptic for skimming at all; sound and visuals tell it. The pad speaks only
when something meaningful happens to the pilot: a **hull collision** (the fleet's punish thud, already in the
Manta's vessel container) and **planting a bomb** (a new, fenced plant knock: `HapticController.PlayPlant`,
two quick mid-low knocks, fired from `MantaStingActionExecutor.SpendBomb`). The Sting container no longer lists
`SkimmerHapticsByPrismEffect` (generator `author_manta_kit_assets.py`, `--check` clean). The fleet-wide skim
cadence (30 ms floor under a 70 ms clip) stays as it is on the Squirrel and Rhino: Caleb and Garrett have not
felt a hum there. Branch `claude/manta-skim-rumble-fix`, PR #1905.

**Only the editor can judge.** On a real pad: silence while skimming through the cell; one heavy thud on a hull
hit; two quick knocks on each plant (graze a rival vessel or a creature with an armed bomb).

**Next question.** Do the knock and the thud read clearly mid-fight, and does the omni cash-out (a big, rare
event) want the alert shake?

## Round: speed-shaped trail prisms (2026-11-11)

### Brief

**Question.** What shape should the Manta's trail prisms take as a function of speed and turning, so the wake
reads its flight (fast and stretched, slow and tall, a Yawstery bank that bends) instead of what happens to be
near it?

**What good looks like (Caleb, 2026-11-11).** No sizing from the surroundings at all. X (width) stays
roughly uniform. Z (length) stretches at high speed. Y (height) grows when speed is very low or while
Yawstering. In a turn, the inside rail's prisms shrink a little in Z to make room for their neighbours and the
outside rail's prisms thin a little in Y while stretching slightly in Z to meet theirs.

**How we'll know.** A lab that flies the Manta through its real speed band (rest, cruise, Soar) and Yawstery
turns and lays the trail from candidate shape rules, with a scorecard that can say NO: neighbouring prisms on
each rail neither overlap nor leave a gap wider than the rule allows, X stays inside a set band, the mass laid
per metre stays in a band (polish, not a balance change), and the shape is a pure function of speed and turn
(the same flight gives the same trail with any prisms around it). Caleb flies the candidates and picks.

### Audit: what sizes the Manta's trail today (bleeding-edge 548ed93, 2026-11-11)

The lay is `VesselPrismController.CreateBlock` on `Manta.prefab`: two rails (`Gap` 18), `BaseScale`
20 × 0.75 × 5.5, one rail = `BaseScale.x × XScaler / 2 − 9` wide, laid every `initialWavelength` 10 m of travel
(spacing fixed in metres, so speed changes the lay rate, never the prism), only above 3 m/s.

| Driver | State on the Manta | Effect |
|---|---|---|
| `ScoutTrailPrismScaler` (+ `ScoutTrailPrismConfig.asset`) | **LIVE, and the only hull that has it** | Environment sizing. Every 50 ms it asks `PrismSpatialIndex.IsAnyPrismWithin` whether any prism is inside a radius that swings 20 ↔ 100 m (growth and shrink 6000 m/s, so it slams end to end each tick), and feeds `SetNormalizedXScale`. That scales X between `minBlockScale` 1 and `maxBlockScale` 4: one rail goes from **1 m to 31 m wide**. The probe has no owner filter, so the Manta's own wake (1 s old, under 100 m behind at cruise) keeps tripping it, and in the Toybox cell it is tripped by everything. It calls `SetNormalizedXScale` most frames, and each call starts a new 1.5 s `LerpXScalerAsync`, so dozens of lerps write `XScaler` at once. |
| `SkimmerScaleTrailPrismEffectSO` / `SkimmerScaleGapByPrismEffectSO` | Not wired on the Manta | Not in either skimmer container the Manta's skimmer uses. |
| `trailVolume` (Mass) | Off (`Enabled: 0`) | Matches §0 ("no element growing its volume"). |
| Turn flare `SetTurnTrail` | Driven every frame by `MantaAnalogTurnBoostExecutor` (pad) and `YawsteryActionExecutor` (touch, `driveTrailFlare: 1`), but `turnFlareMaxScale` 1 and `turnUpgradeShieldsTrail` 0 | Does nothing today. Matches §0 ("no turn flare", Shielded Turn Trails dropped). |
| Boost / speed scaling | None (base `VesselPrismController`; only Sparrow, Shrike and Brittle Star subclass it) | No prism dimension depends on speed today. |
| `GrowTrailAction`, `DriftTrailAction(Executor)` | Not on the Manta | |

So §0 and the ability map's description ("a plain Squirrel-style trail: no element grows it and turns do not
flare it") are not what flies: `ScoutTrailPrismScaler` still sizes the width from the surroundings.

**Bearing on the steady rumble** (solved in the round above). The scaler has no haptic call. The Manta's
skimmer runs `SkimmerHapticsByPrismEffect` on every prism that enters it (a 70 ms low-motor pulse, 30 ms apart,
which overlaps into a continuous buzz), ahead of the `affectSelf` check, with only a 1 s own-trail grace equal to
the trail's 1 s collider wait.

### Lab

**Manta Trail Lab**: https://claude.ai/artifact/UcrAbovUrX7NPCx1mLoB9k (source `Docs/Studios/MantaTrailLab.html`, a
`/labmaker` browser studio). The Manta flies a Redline-style loop (Soar straights, two Yawstery hairpins, a slalom,
a crawl chicane) with an input-only AI or a pad, keyboard or touch. Each rule shapes both rails from speed, turn
intensity and turn rate only; "Today" adds the scaler's prism probe over 500 scattered cell prisms. Every flight is
scored twice, with and without the cell prisms, so a rule that reads the surroundings fails "Ignores cell".

| Rule | Width spread | Ignores cell (m) | Worst overlap (m) | Speed read | Mass/m slow | Mass/m Soar | Soar fill | Turn fit | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| Today (scaler live) | **4.74** | **16.87** | 0 | 0 | 1.04 | 0.92 | 0.45 | 0.10 | NO (hard fails) |
| Scaler removed | 1.00 | 0 | 0 | 0 | 1.07 | 0.90 | 0.45 | 0.06 | 6/8 |
| Fixed width (11 m) | 1.00 | 0 | 0 | 0 | 1.07 | 0.90 | 0.45 | 0.10 | 6/8 |
| Free curves | 1.00 | 0 | 0.12 | 0.94 | 2.69 | 1.53 | 0.79 | 0.01 | 6/8 |
| Volume kept | 1.00 | 0 | 0.15 | 0.95 | 1.07 | 0.90 | 0.85 | 0.00 | **8/8** |
| Planted: overfill | 1.00 | 0 | **6.49** | 0.97 | 1.07 | 1.06 | 1.25 | 0.00 | NO (control) |

Skilled AI, seeds 1-3, 110 s (two laps) each, 60 fps (`window.__lab.runBatch()`; re-run in Chromium 2026-11-11).
With a rookie pilot (noise 0.6) the order holds: Volume kept 7/8 (it Soars less, so Soar fill drops to 0.66), Free
curves 6/8, Today still fails both hard checks, and the planted overfill fails overlap at 9.47 m.

**Checked.** `verify_lab.cjs` PASS (desktop and phone layout, hooks, SPEC vs SHIPPED, manual clock, deterministic
batch within and across loads). Both negative controls bite: Today fails "Ignores cell" and the overfill fails
"Worst overlap" with both pilots. The shared decision log is wired (`ArtifactData list decisions` answers, empty).

**Found.**
- Deleting `ScoutTrailPrismScaler` alone gives **1 m rails**: `Initialize` sets `XScaler = minBlockScale` (1), and
  a rail is `20 × XScaler / 2 − 9`. The scaler is what holds today's trail near 11 m (XScaler about 2), so its
  removal needs a stated width with it.
- The lay is frame-bound at Soar. `SpawnLoopAsync` waits `10 / speed` on `UniTask.Delay`, which ends on a frame, so
  above about 600 u/s at 60 fps the rail spacing is speed × frame time (about 11 m), and wider at 30 fps. A Z rule
  capped at the wavelength leaves gaps at Soar; the proposal caps at `max(wavelength, speed × deltaTime)`.
- Volume kept answers the whole brief with one number held constant (X × Y × Z): slow is tall, fast is long and
  thin, the outside rail of a turn stretches in Z and thins in Y, the inside shortens and grows, and mass per metre
  stays where it is today (polish, not a balance change).

**Recommendation.** Volume kept at the lab's numbers (11 m rails, reference 162 u/s, Z ∝ speed, Yawstery height
gain 0.6, full turn geometry), with the scaler removed.

**Decision needed (Caleb).** At a crawl Volume kept lays short tall posts (1.5 m long, 2.75 m tall) about 10 m apart,
so the slow trail reads as a dotted fence rather than a wall. Keep that, or let slow prisms also fill the gap (more
mass per metre while slow)? Fly the crawl chicane in the lab and record the pick in its Decisions tab.

**Decided (Caleb, 2026-11-11): Free curves, 8 m rails, the scaler removed.** "I think I like 'Free Curves' the most.
We definitely want to remove the scaler, and I think I found 8m a good value for us to start with." His recorded
settings (the lab's decision log, two identical entries): rail width 8, reference 162 u/s, Soar 600 u/s at 10 m,
spacing cap 0.9 (frame aware), crawl below 60 u/s at 4× height, Yawstery height gain 0.8, outside thin 0.25, Y held
0.1 to 3.5 m, full turn geometry. In the lab those numbers pass every hard check; the soft misses are crawl mass
2.68× cruise and Soar fill 0.75 (the gap at Soar is 10 % of the spacing by design). That answers the open question
above: slow prisms are tall and keep their length, so the crawl trail is a taller wall, not a fence.

**Graduated (same day).** `ScoutTrailPrismScaler` is off Manta.prefab (the script and `ScoutTrailPrismConfig.asset`
stay in the repo, unused). `TrailSpeedShape` (`Assets/_Scripts/Controller/Vessel/`) is the lab's `shapeFor('free')`
line for line; `VesselPrismController.speedShape` authors it on the Manta with the numbers above, measures the yaw rate
between lays, and is off by default for every other hull. `TrailSpeedShapeTests` holds the lab's values (read from
the page) and the prefab. The Time ability's description in the kit says the trail is shaped by speed.
Not verified in Unity yet: fly the Manta from the Toybox's Vessel Changer and judge the look at a crawl, at Soar
and in a held Yawstery.

### Fly test 1: gaps flying straight and fast (Caleb, 2026-11-12)

**Reported.** "A categorical improvement", but odd gaps and irregular spacing, worst flying straight with both
triggers at Time 10, and also in crawl turns on the triggers alone.

**Found (two causes).**
1. The lay ran on a timer, `UniTask.Delay(wavelength / speed)`, which only ends on a frame. Whenever a frame flew
   between half a wavelength and a whole one (300 to 600 u/s at 60 fps) it waited two frames, so prisms went down up to
   twice the wavelength apart, and flipped between one and two frames as the frame time wobbled (the irregular spacing).
   At slower speeds the same rounding added up to a frame's flight to every gap.
2. Above Soar the lay is one prism a frame, so the spacing keeps growing with speed (18 m at 1080 u/s, about Time 10
   with both triggers), while Free curves held the length at Soar Length (10 m): 8 m gaps.

**Fixed.** With the speed shape on, the trail lays by distance flown (`TrailSpeedShape.StepLay`): each frame, once
the vessel has flown a spacing since the last prism, the prism goes back along the course to the exact point and the
rest carries over. The spacing is exactly one wavelength below Soar and one frame's flight above it, at any frame
time. Toward Soar the length now stretches to Soar Length or to Max Length Of Spacing (0.9) of the spacing,
whichever is longer, so at 1080 u/s a prism is 16.2 m with a 1.8 m gap. Below Soar nothing changed in the shape.
The lab lays the same way (re-read: straight-flight fill above 600 u/s 0.45-0.79 before, 0.90 every prism after).
Cost: in the 300 to 600 u/s band the trail now lays up to a third more prisms (the ones the timer was dropping).
Other hulls keep the timed lay.

**Still open.** The crawl-turn irregularity Caleb saw is partly the same cause; fly it again to see what is left. A
gap-free Soar wall is Max Length Of Spacing 1.0 (Caleb's number, kept at 0.9).

### Fly test 2: stretch at top speed, never touching (Caleb, 2026-11-12)

**Asked.** Still some irregularity at full speed. "I definitely don't want any of the prisms touching or 'walling'
at any point. Quite the opposite. Lets increase the distance between prisms at higher speed while thinning them in
Y and stretching them in Z even more ... the whole thing stretches at the highest speeds while production struggles
to keep up."

**Why it was still irregular.** Above about 600 u/s a frame often flies further than one wavelength (10 m), so the
lay fell back to one prism a frame and each prism was sized to that frame's own flight, which wobbles with the frame
time. Fly test 1's "fill the spacing above Soar" also made the trail more wall-like there.

**Changed.** Fly test 1's fill rule is gone. Above Soar the trail stretches toward a new Top Speed (1100 u/s; Time 10
with both triggers is about 1080): the gap between prisms grows to 3x the wavelength (30 m), Z to 20 m and Y to 0.4x
(0.3 m), always under Max Length Of Spacing (0.9) of the gap. Every prism is sized to the nominal spacing rather than
the frame's measured flight, so lengths stay regular even when a slow frame opens a gap. New fields on
`TrailSpeedShape` (Stretch header): TopSpeed 1100, TopSpacingMultiplier 3, TopLength 20, TopHeightMultiplier 0.4;
the lab has the same four dials (Free curves) plus a Time speed dial. Lab, Caleb's numbers at Time 10: no two
neighbours ever touch (closest 0.97 m apart); mass per metre falls from 5.4 m³ at Soar to 1.6 m³ at Top Speed
(per rail), so mass per second drops as speed climbs past Soar: production falls behind.

**Open.** At Soar itself prisms still fill 0.9 of their 10 m gap (Max Length Of Spacing, Caleb's number). Lower it
(0.7 or so) if Soar still reads as a wall.

### Fly test 3: prisms still bunching at full speed (Caleb, 2026-11-12)

**Seen.** At Top Speed (editor, ~115 fps) some prisms sat almost against their neighbour and others far apart.

**Found.** The lay loop counted the distance flown as Speed x deltaTime, but it runs at UniTask's Update timing,
BEFORE `VesselTransformer.Update` moves the hull. So it counted a frame of flight that had not happened yet and set
the prism that much too far back. On a steady frame that is a small wobble; on a long frame (an editor hitch of
0.1 s is 108 m at Top Speed) it put the prism right up against the previous one, then a long gap.

**Fixed.** The loop now reads the distance actually flown from the hull's position (a jump far beyond the speed, a
respawn, restarts the trail). A frame that flies past several spacing points lays a prism at each of them, back
along the path (`TrailSpeedShape.NextLay`, at most four a frame; past that one gap opens), so a hitch no longer
shows. The lab lays the same way; the test drives 0.1 s hitches every 97 frames and checks every prism is exactly
one spacing from the last.

## Round: stronger hull hits and a fleet haptics rule (2026-11-12)

**Question.** Hull hits felt too light, heavier still wanted for danger and super-shielded prisms. Is there a fleet
rule for haptics on collisions, skims and jousts? There was none (only a list of feels in `Docs/HAPTICS.md`).

**Decided (Caleb).** A universal rule with the kits' spirit (`Docs/HAPTICS.md` § The universal rule): a hull-hit
ladder on every hull that does not want contact (plain ~240 ms, shielded ~300 ms, heavy danger/super-shielded
~420 ms two-slam crunch), and rammers rewarded instead (the Rhino plays the skim pulse at 0.7 on each plough,
spaced 100 ms). Skims opt-in per kit. On the Manta: the ladder on a hull hit, the knock on a plant, nothing on a skim.

**Next question.** Do the Shrike, the Urchin and the Grizzly count as rammers by their kits?

## Round: the chase camera is too far out (2026-11-12)

**Question.** Does the Manta read better, and feel faster, with its chase camera at half the distance?

**What good looks like (Caleb, 2026-11-12).** The camera sits closer: "I think it's too far out. Let's move to half
the distance to start with." The hull fills more of the frame without the view losing what is ahead.

**How we'll know.** Caleb flies it from the Toybox's Vessel Changer. This is a starting point, not a lab round: one
number, halved, then tuned by feel.

**Found (bleeding-edge 91a319c).** The distance is `MantaCameraSettingsSO.asset`, which only `Manta.prefab` uses, so
the change stays on the Manta. It flies `FixedCamera` (mode 0), so `followOffset.z` is the distance; the dynamic pair
only matters in dynamic mode. No generator owns the asset and no test pins its numbers. The camera feel
(`Docs/CAMERA_FEEL.md`) works in fractions of the follow distance, so G lag, kicks and shake read the same size on
screen at the new distance. `VesselRearView` mirrors the same offset, so the look-back comes in to 15 too.

**Changed.** `followOffset` (0, 0, -30) -> (0, 0, -15); `dynamicMinDistance` -27 -> -13.5 and `dynamicMaxDistance`
-30 -> -15 (kept in proportion in case the mode is ever switched). At a 60 degree field of view the frame is 17.3 u
tall at the hull instead of 34.6 u: the 4.63 u wingspan goes from 13% to 27% of the frame height (about 290 px at
1080p), so the hull reads twice as big and the view ahead covers half the width at the hull.

**Only the editor can judge.** Whether 15 is too close at Soar (speed reads more strongly up close); whether the
Space skimmer's visuals (20-40 u across) or the trail rails (18 apart) crowd the view now that the camera can sit
inside them; whether the prism occlusion dither keeps the hull clear in dense cells.

**Next question.** Is 15 the number, or does it want 18-20? And does the Manta want a little height or look-ahead
(`lookAheadDistance`, `lookAheadLift`) so the hull sits lower in the frame and shows where it is going?

### Fly test 1 (Caleb, 2026-11-12)

**Seen.** "I think 15 is great!" To be thorough he flies 20 as well before deciding. `followOffset` z -15 -> -20
(dynamic pair -18 / -20): the wingspan is 20% of the frame height at 20, against 27% at 15 and 13% at 30.

**Next.** Caleb picks 15 or 20 after flying both; whichever he picks goes in this PR before it ships.

### Decided (Caleb, 2026-11-12)

**18.** Caleb flew the whole range from 15 to 20 in the editor, moving the asset's value himself, and picked 18: "Lets ship
at -18". `followOffset` (0, 0, -18), dynamic pair -16.2 / -18. The wingspan is about 22% of the frame height (it was 13% at 30).

**Next question.** Should the camera look slightly ahead (`lookAheadDistance`, `lookAheadLift`) so the hull sits lower
in the frame and shows where it is going?
