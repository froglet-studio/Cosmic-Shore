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
