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
