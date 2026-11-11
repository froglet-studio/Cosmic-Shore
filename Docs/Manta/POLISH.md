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

**Changed.** The Sting container no longer lists `SkimmerHapticsByPrismEffect`;
`MantaStingSkimPrismEffectSO` plays the same skim pulse (same 0.35 floor, same closeness scaling)
only when `AddSkimCharge` actually paid energy. Generator `author_manta_kit_assets.py` updated
(`--check` clean). Branch `claude/manta-skim-rumble-fix`.

**Only the editor can judge.** The feel on a real pad: three bright ticks as the bay fills, then
silence through the cell; a hull hit empties the bay and the ticks come back on the next skims.

**Next question.** Is "pulse on a paying skim" enough feedback, or does a full bay want its own tell
(a world-side one, not a haptic)? Separately, the fleet-wide skim train (30 ms floor under a 70 ms
clip) flattens into a hum on any dense stream; that is Garrett's skim feel to decide (`Docs/HAPTICS.md`
follow-ups).
