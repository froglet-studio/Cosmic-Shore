# Wrecking Ball — Technical Documentation

## Overview

Wrecking Ball is the **Scarab-only demolition race**, and Rampage's analog for the hull whose
weapons are a **ball** and a **plate**: every domain races to be the first to DESTROY **1,500
hostile prisms**. A sphere court — the cell nucleus resized, exactly Scarab Scramble's court — is
grown full of Rampage's six breakable flora (cacti, spires, pines, rosettes, coral and the
Borromean minimal-surface membrane — nine configs, because Borromean is four, one per element —
across all
three domains and all four elements), the arena's omni crystals respawn anywhere inside it, and
**intensity means HOW DENSE and HOW MANY BALLS**: at 1 the court holds 59 plants and eight
crystals for a four-seat lobby; at 4 it holds 35 and three.

**The loop is the Scarab's own kit, pointed at a forest.** Nothing here is scripted — the mode
arranges the arena so two things the vessel already does become the game:

| the vessel already does this | Wrecking Ball makes it the game |
|---|---|
| The skimmer turns any **omni crystal** into a ball, in place, at rest (`ScarabBallForge`) | the crystal supply IS the ball supply, and it is the intensity dial |
| A ball **eats opposing / neutral prisms** and shields its own side's as it rolls (`AstroLeagueBall`, SCARAB.md §4.1b) | bowl it into the thickest stand and every prism it plows through is yours |
| A ball **bounces off its cell's nucleus** from inside (`ResolveNucleusBoundary`) | the court wall carries a wild shot back through the stands — nothing is wasted |
| A committed juke throws the **cavitation plate**, a cylinder sweeping sideways and mirrored behind you (§3.7, §3.9) | flick a dash beside the forest and the plate shreds whatever it sweeps |
| A juke-dash **steals** a rival's ball (§4.2) | a stolen ball scores for its new pilot from then on |

So a round reads: **fly through a crystal → bowl the ball at the forest → dash beside the thicket
on the way → chase the ball off the wall and do it again.**

- **Only hostile mass scores, and hostile means COLOUR** — the metric is
  `ScoringMetric.PrismsDestroyed`, Rampage's, verbatim: environment mass is friendly iff it wears
  your colour, `Domains.Blue` is hostile to all, and your own team's trail never counts
  (`StatsManager.IsFriendlyEnvironmentPrism`). A third of the forest is yours and worth nothing.
- **Golf-timed** like every race here: the winning domain's pilots score their finish time, losers a
  prisms-remaining sentinel (`RampageScoringRuleSO.AssignScores` — inherited, the rule is a subclass
  for its reveal wording).

## The platform change: a ball scores for its pilot

The ball has always eaten opposing prisms, and it has always named itself **"Astro League"** as the
attacker of every one (`prism.Damage(ballVel, ballDomain, "Astro League")`). That name is on no
roster, so `StatsManager.PrismDestroyed` found no attacker and **nothing a ball ever ate scored for
anyone** — fine in a hoop game, where the ball's mass interaction is texture, and fatal in a
demolition race, where it is the whole verb.

`AstroLeagueBall` now carries **`n_PilotName`**, a server-written `FixedString64Bytes`
NetworkVariable mirrored into a managed string per change (never per prism):

- **stamped at the forge** with the maker (`ScarabBallForge.Request` → `RecordPilotServer`), so a
  ball is a demolition tool from its first roll;
- **re-stamped on every vessel strike** (`RecordTouchServer`), so a ball a rival bats away scores for
  THEM from then on — the Scarab way (Tollway: you score off other people's shots);
- **left alone by a blast** (a blast records a touch with no name), so a plate that shoves your ball
  does not launder the credit for the mass it goes on to eat;
- **cleared at the Astro League kickoff** (`ResetToCenterServer`), where a fresh ball belongs to
  nobody until struck.

**Why it is replicated rather than server-only.** The ball's prism scan runs on EVERY peer ("one
gate, one answer"), and prism destruction is credited by the machine that simulates the ATTACKER:
a trail exists at the same place on the server, so the server's copy of the ball credits a
client's trail kills; but flora are spawned per-peer from local `Random` rolls, so a client's forest
kills are credited only by that client's own copy of the ball, which therefore has to know the
pilot's name (`StatsManager.OwnsAttacker` + `ForwardEnvironmentKill`). A name that lived only on
the server would have scored a client pilot's ball on the trails and never on the forest.

Astro League and Scarab Scramble are unaffected in outcome (nothing there scores on prisms) and
are more honest in their stats: a ball's prism kills now land on `BlocksDestroyed` for the pilot
who last hit it.

## The second platform change: an AI Scarab can dash

The juke is stick-driven, and `ScarabJukeController.Update` returns before the gesture logic for
an autopilot vessel — so an AI could never dash and never fire the plate. In a mode where the plate
is half the verb, an all-AI domain would have been an opponent that could not play (the Tollway
rule). **`ScarabJukeController.TryAutopilotDash(worldShove)`** runs a committed dash through the
SAME `Fire` path a human's perimeter push runs — steal window, roll, `OnJukeFired` → the plate —
gated to the simulating machine (an AI is server-owned, so a spawned non-server peer refuses) and
refused while the juke is spent or a roll is live, so an AI can never fire faster than a human. It
returns whether a dash fired so a caller paces off the answer. Undertow's AI rides the same entry.

## The arena — Scramble's court, grown full of Rampage's forest

**Why not Rampage's cell.** A ball outside its cell's nucleus bleeds speed increasingly fast —
×6 drag beyond 250u past the surface (SCARAB.md §4.1c, a soft boundary, never a wall). Rampage's
forest is planted at 0.1–0.97 of the membrane, far outside a 196–490u nucleus, so a ball that
reached the trees would be dead on arrival. The court has to CONTAIN the forest.

**Why not Scramble's cell.** It authors no flora at all, and its volume ladder (Restless 164,000 /
Frenzy 391,000) is authored for Scarab trail plus a few switch daises; a 400,000-volume forest
would cross both gates before the countdown ended and the ladder would convey nothing.

So the four `WreckingBall Cell Config 1..4` fork Scramble's cell for TWO stated reasons and no
others:

1. **The forest is inside the court.** The five `WreckingBall <Species> Flora` configs are
   Rampage's, byte for byte, except their planting band: Rampage's per-species bands (spanning
   0.1–0.97 of the membrane between them) are mapped linearly into **0.28–0.92 of the 720u
   court** (0.168–0.552 of the 1200u membrane), so the layering — coral in, spire out — survives
   the move. The controller sets **`NucleusIsControlZone = false`** (the nucleus is play geometry,
   not a claim), which is what lifts `Flora.ResolvePlantRadius`'s outside-the-nucleus clamp
   (`Docs/ECOSYSTEM.md §42`) and lets the band sit inside the court at all. It also means the cell
   keeps whole-cell diet semantics: herbivores eat OPPOSING-domain mass, and the food web grazes
   the forest as it does in Rampage.
2. **The volume ladder is Rampage's, scaled by this forest.** Rampage's measured intensity-4
   forest is 441,070 volume and its play-tested ladder sits Restless at 0.256× and Frenzy at
   3.70× of it; each Wrecking Ball cell's forest is that forest × its plant scale, so its ladder is
   Rampage's × the same ratio (the rule Rampage's own four cells follow). Counts are Rampage's
   backstops. Frenzy leaves at least four switch daises (50,773 each) of headroom
   above the forest at every intensity; the generator asserts it. **ESTIMATE pending the in-editor
   baseline measure.**

   ⚠ **Those two numbers are IMPORTED from `rampage_intensity.py`, not retyped here** — they
   were literals in this mode's generator and went stale the day Rampage adopted the Borromean
   species (`Docs/ECOSYSTEM.md` §49.12), which took its forest 396,178 → 441,070 and its margin
   4.11× → 3.70×. The generator now reads `REFERENCE_FOREST_VOLUME` and `SHIPPED_VOLUME_LADDER`
   from that module and asserts Rampage's forest still matches the volume its ladder is anchored
   to. *A constant copied out of another tool's answer is true only on the day it is copied.*
   Note the gates themselves did not move: the ratio's denominator and the scaling factor are
   the same number, so it cancels — the forest grew and the play-tested gates held, exactly as
   in Rampage.

Everything else is referenced: Scramble's membrane, the standard nucleus prefab (resized at
runtime to the court), the cytoplasm, and Rampage's wildlife pair (tadpole + shark) at Rampage's
own 1×/2×/3×/4× ladder — hostile mass that also grazes the forest.

**One court radius at every intensity (720).** The forest is authored in membrane fractions
against this court; a court that grew would leave the stands outside the wall. Intensity moves the
forest and the balls, never the court.

### The intensity ladder

| intensity | plants (×Rampage's 59) | forest volume (est.) | crystals (4 seats / 2 seats) | wildlife | Restless / Frenzy (volume) |
|---|---|---|---|---|---|
| 1 | 1.00× — 59 | 396,000 | 8 / 4 | 1× | 113,000 / 1,630,000 |
| 2 | 0.85× — 50 | 337,000 | 5 / 3 | 2× | 96,000 / 1,386,000 |
| 3 | 0.72× — 42 | 285,000 | 4 / 2 | 3× | 81,000 / 1,174,000 |
| 4 | 0.60× — 35 | 238,000 | 3 / 1 | 4× | 68,000 / 978,000 |

Plant scale is flatter than Rampage's 5×/3.67×/2.33×/1× because the court band holds about a
fifth of Rampage's shell volume — Rampage's 295 plants in this court would be a wall a ball cannot
enter. Prism SIZE is 1× at every level: a bigger prism is a bigger drag on the ball, which is the
wrong direction for "easier". The crystal count follows `CrystalCountMode.IntensityScaled`
(`2p / p+1 / p / p−1`), so at 4 a two-seat lobby fights over one ball.

**Target 1,500** rather than Rampage's 2,000 because the forest is smaller: at intensity 4 about
5,900 prisms stand and roughly two thirds are hostile to any one domain, so a match ends with
forest left, and the flora breed back (Rampage's `GrowthPerOffspring` numbers are carried over).

## AI

Scramble's rollers, re-aimed (`WreckingBallController.ArmWreckers`): no team ball → fetch the
nearest forge-source crystal; team ball live → aim BEHIND its predicted position on the far side
from the densest hostile forest (`Cell.GetExplosionTarget(domain)`, the fauna hunting query — the
hoop's role in Scramble's escort), so driving to the aim point bowls the ball into the stands;
neither → fly at that forest. On a separate half-second clock, whenever the densest hostile forest
is within `aiDashRange` (70u), the AI asks its juke for a committed dash toward it and the plate
rides the dash. Steering is `SetExternalTargetProvider` and nothing else; the AI's throttle needs
no wiring (the Scarab's transformer runs full throttle under autopilot).

## Assets

All authored by `Tools/Build/author_wrecking_ball_assets.py` (`--check` passes; watched to fail on a
deleted cell config). Built on `Tools/Build/arcade_mode_lib.py`.

| Asset | Path |
|---|---|
| Card | `_SO_Assets/Games/ArcadeGameWreckingBall.asset` (Scarab only, 1–4 seats, 2–3 domains, comeback 0.006) |
| Settings | `_SO_Assets/Games/WreckingBallSettings.asset` |
| Rule | `_SO_Assets/Scoring Rules/WreckingBallScoringRule.asset` (metric 5, golf) |
| Cells | `_SO_Assets/Cell Configs/Wrecking Ball Cell/` — 4 configs, 4 profiles, 5 species |
| Toasts | `_SO_Assets/Game Toasts/GameToastConfig_WreckingBall.asset` (83 every 250, 92, 93 idle, 94 idle, 30) |
| Preview | `_SO_Assets/Mode Previews/ModePreview_WreckingBall.asset` (per-intensity cells) |
| Scene | `_Scenes/Multiplayer Scenes/MinigameWreckingBall.unity` — a clone of Scramble's with the mode identity, the cell list and the crystal economy swapped |
| Code | `Arcade/WreckingBall/WreckingBallController`, `WreckingBallSettingsSO`, `WreckingBallScoringRuleSO`; `TurnMonitors/WreckingBallPrismTurnMonitor` |

Shared-code touchpoints: `GameModes.WreckingBall = 54`, `EndConditionOverridesSO.wreckingBallPrismTarget`
(+ window rows), `ElementalComebackSystem` (PrismsDestroyed), `MiniGameHUD` (Scramble's objective
provider), `GameToastSituation` 92–94, `author_preview_spawns.SCENE_FOR_MODE`.

## Collider budget

At intensity 1 the court holds Rampage's intensity-4 forest — up to ~10,000 LOD-cullable prisms
(the count backstop freezes growth there) plus up to 88 always-on heart-crystal colliders at the
plant cap, plus Rampage's wildlife. That is Rampage's shipped, play-tested intensity-4 envelope
in a smaller volume; the denser packing changes nothing about collider count. Higher intensities
are lighter.

## Verification — compiled and played once; the ladder is still unmeasured

Status at ship: the branch compiles in the editor (one missing `using` was caught and fixed on
the first compile) and BOTH modes were played once by the author, who called them excellent
starts. Nothing below has been MEASURED — the ladder, the AI dash ranges and the plant density
are still the estimates §"Known limitations" records. The checklist stands as the re-verification
pass after any retune, in this order:

1. Compile. `WreckingBallController` / `UndertowController` and the two `TryAutopilotDash` /
   `RecordPilotServer` additions are the new surface.
2. Launch Wrecking Ball at intensity 1 with one AI. Expect: a 720u court, forest standing INSIDE
   it (not outside the wall), crystals inside the court, pilots on the 760u ring just outside.
3. Fly through a crystal, bowl the ball into a stand: the goal row's prism count must RISE
   (before this branch the ball's kills scored for nobody). Bat a ball an AI forged: its kills
   should now credit you.
4. Flick the right stick beside the forest: the plate fires and the count rises again.
5. Watch the AI: it should fetch a crystal, bowl, and visibly dash near the forest.
6. Frenzy must not arrive on boot (Restless ~113k, Frenzy 1.63M against a 396k forest at 1) —
   run **FrogletTools > Ecology > Measure Cell Environment Baselines** and re-derive the ladder if
   the forest measures far from 396k × scale.
7. Astro League / Scramble regression: a ball still bounces, still scores goals, still steals.

## Known limitations / follow-ups

- **The ladder is an estimate** (Rampage's, scaled). Measure in-editor.
- **Plant density in a 720u court is unplayed.** If the stands read as a wall at intensity 1, the
  dial is `FLORA_SCALE[0]` in the generator, not the band.
- **A ball's shield-pop and carom are unchanged** — a ball still leaves a shielded prism standing
  (Charge plants are armoured), so a quarter of the forest costs two passes.
- **Preview**: the per-intensity scale model shows the forest; the flight preview forges a ball.
