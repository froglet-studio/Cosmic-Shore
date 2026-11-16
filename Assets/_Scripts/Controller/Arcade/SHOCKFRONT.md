# Shockfront (`GameModes.Shockfront = 119`)

The Stoat's **CHARGE** game: a duel in Dog Fight's Boneyard. Squeeze a trigger to orbit, let go to sling, and when the
pair of holes you slung out of closes, the meeting throws a **ripple** through space. Every rival its front sweeps over
loses one petal of every element. **Every rival stripped is one point.** The first **domain** to the target wins,
golf-timed.

Built 2026-11-15 for Garrett's "make all the missing games" (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 29, which
reserved the ids: `GameModes` 119, hint toast 194).

> **Status.** Authored headless on 2026-11-15 (`claude/stoat-arcade-games`). The generator, the shared gates and the
> reference compile ran; **nothing here has been run in the Unity editor, and nobody has flown it.** The target (10) is
> a first cut.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameShockfront.unity` (cloned from `MinigameUndertow.unity`, as Dustup is) |
| Controller | `ShockfrontController : MultiplayerDomainGamesController` |
| AI | steering-only hunters inside the controller (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `ShockfrontScoringRuleSO`: a Debuff-class hit (a ripple strip) pays **1**, every other class **0**; breakdown is `DebuffHitsLanded` |
| Turn monitor | `ShockfrontRippleTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.shockfrontRippleTarget` (**10**) |
| Comeback | rate **0.6**: a quarter behind (2.5 hits) buys 1.5 element levels; on the Stoat Charge is the ripple's reach |
| Arena | Dog Fight's **Boneyard**, referenced read-only (four intensity configs), Dog Fight's spawn ring and crystal volume |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 82 (a ripple hit: `DebuffHitsLanded` rose), 128-130 (shared race beats via `DomainRaceToasts`), 194 (the hint), 30 (comeback) |
| Generator | `Tools/Build/author_shockfront_assets.py` (`--check`) |

## The loop, in the Stoat's own terms

The Stoat flies the orbit alone (`R_VesselActions/STOAT.md`, the 2026-11-02 cut): a trigger lays an attractor-repulsor
pair beside the hull and it orbits the attractor, the release slings it out along the tangent, and the let-go pair falls
together and annihilates where it was laid. Its **Charge ability is the Ripple** (`STOAT_DIPOLE.md` §1c), passive: every
annihilation throws a front to `clamp(separation x 0.6, 30, 160) x Charge (x1 -> x2)` units, so a wide orbit is a wide
ripple. A rival the front sweeps over is stripped of one whole petal of every element it holds (shed as crystals round
it, or with Charge 5's Siphon handed to the Stoat). Creatures it reaches die.

So a round reads: **sling round close to a rival, and let your pair close where they are flying.** The Boneyard's wrecks
are cover to swing round and canyons to lose a hunter in.

## The one vessel change (made on the vessel, not by the mode)

Until 2026-11-15 the Ripple took petals but was **not a scored hit**: it never passed `CombatHitDrain`, never silenced
its victim and scored in no mode. That broke the one-to-one rule every other petal take keeps (`SILENCE.md`: "a scored
hit and a petal theft are one event"), and it left the Stoat with no Charge game possible. Fixed on the Stoat:

- `StoatRippleExecutor.ReportHit`: on the machine that owns the Stoat, each strip is admitted through
  `CombatHitDrain.TryAdmitPetals` as `CombatHitClass.Debuff`, priced in the four petals it takes (a four-second silence),
  and raised on `Event_CombatHitStats` (`StoatRippleConfigSO.onCombatHitLanded`, `hitCooldownSeconds` 0.5).
- A **silenced** Stoat's ripple strips nothing on any peer (`VesselSilence.IsSilenced`, published to every peer).
- The take itself is unchanged: the per-peer strip through `AccrueElementalLoss`, ward honoured.

This touches every mode the Stoat flies (Slingshot, Warpline, Regatta): a ripple now silences the rival it strips. That
is what `SILENCE.md` asks of every hit. Queued for the Stoat's owners through `/approve`.

## The AI

`ShockfrontController.ArmHunters`: every AI Stoat's steering runs at a lead point on the nearest opposing pilot
(`aiInterceptLeadSeconds` 0.8 of the rival's velocity, humans counted `aiHumanFocus` x2 nearer, re-chosen every 1.5 s).
That is all. The Stoat's own autopilot slings whenever its target is 30 deg or more off the nose and 120 u away
(`StoatSlingExecutor.AutopilotSling`, the replicated press), so a hunter turning toward a rival slings round, and the pair
it slings out of closes beside the rival. The generator fails if the controller presses an input or reads the Stoat's
configs (the LOCKED vessel rule).

## Intensity

The Boneyard's own ladder (Dog Fight's, `DOGFIGHT.md`): the same four configs, so intensity is the wreck field's
density. Nothing is forked. The seat's AI difficulty is the opponent's skill, never the intensity.

## Verification and known limits

- Authored headless. The scene is a text clone; the Editor owns its fileIDs once committed (`committed_scene`).
- Not flown: whether ten hits is a good race length, and whether the hunters sling close enough often enough, are the
  first two things to watch. If the AI rarely strips, lead it to a point BESIDE the rival rather than at it.
- No offline race model: this is a Charge duel, so it is not a self-play training ground yet.
