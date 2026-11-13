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
