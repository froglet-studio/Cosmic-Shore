# Corona (`GameModes.Corona = 87`)

The Manta's **CHARGE** game: nested prism shells round a crystal heart, in a 0G volume with no up. Shombith asked for
a charge game for the Manta on 2026-11-10 and picked this one from three drawn ideas (the others were Tinderbox, a
lattice of prism struts, and Tiltyard, braided rails ridden head-on).

Skim a shell to arm a bomb, graze a rival to plant it, then come in through the gates and touch an omni crystal in the
heart: every bomb you planted goes off at once. **Every rival a bloom catches is one point.** The first **domain** to
the target wins, golf-timed.

> **Status.** Authored headless on 2026-11-10 (`claude/amazing-goldberg-7zn80e`). The arena's C# was compiled and RUN
> outside the editor (the bake, the clip audit and the card render all lay the mirror's census exactly), and the mode
> passes the reference compile and the `/ship` gates. **Nothing here has been run in the Unity editor, and nobody has
> flown it.** Hand tests: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameCorona.unity` (cloned from `MinigameKelpwood.unity`) |
| Controller | `CoronaController : MultiplayerDomainGamesController` |
| AI | `CoronaStinger`, a server driver that only steers (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `CoronaScoringRuleSO`: a Debuff-class hit (a bloom) pays **1**, every other hit class **0**; breakdown is `DebuffHitsLanded` |
| Turn monitor | `CoronaBloomTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.coronaBloomTarget` (**15**, a first cut: nobody has played it yet) |
| Comeback | rate **0.3**: a quarter behind (3.75 blooms) buys 1.1 element levels |
| Arena | four `Corona Cell Config`s (IntensityWise) in the standard 1,200 u cell (`CapsuleMembrane`), no nucleus, no flora, no fauna (one empty spawn profile) |
| Environment | `SpawnableCorona`, one prefab per rung, baked (`Assets/Resources/PrismLayouts/SpawnableCorona_*.bytes`) |
| Omni crystals | the scene's `NetworkCrystalManager`, per intensity, in a 45 u ball at the heart |
| Census | `Tools/Build/corona_budget.py`, an exact mirror of the C# (no random draws at all) |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 82 (a bloom hit: `DebuffHitsLanded` rose), 128-130 (shared race beats via `DomainRaceToasts`), 162 (the hint, new), 30 (comeback) |
| Generator | `Tools/Build/author_corona_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Corona.png` (`/cardart`, RUN tier: the shipped generator at intensity 2, from between the shells looking down an inner gate at a Kabloom) |

## The loop, in the Manta's own terms

The Manta's Charge kit is all passive (`R_VesselActions/MANTA_STING_KABLOOM.md` §0). Corona builds a place for it:

1. **Arm on a shell.** Every tile is neutral, and the skimmer pays energy from any prism it brushes (about three
   distinct tiles fill the one bomb of energy the Manta holds). The skimmer reaches 10 u at Space 0 (`Skimmer.Scale`
   20, sphere radius 0.5), 20 u at Space 10.
2. **Never touch.** A Manta's HULL touching any prism sets off every bomb it has planted at the small (fuse) size and
   empties its energy. A shell is fuel at skimmer reach and a trap at hull reach.
3. **Plant at the gates.** The omni crystals are only in the heart, so every Manta comes in through the gates. A graze
   plants a bomb (one per target; an already-bombed rival is denied to everyone). A plant only lands while you fly at
   least as fast as the rival (`TryPlantOnVessel`: `Speed >= target.Speed`), so a graze is an overtake or a faster
   pass, and the victim gets no tell.
4. **Cash at the heart.** All your bombs share one 20 s fuse that every plant resets. Touch an omni crystal and every
   bomb blooms at once at the Kabloom size, bigger the more there are, and a bloom also opens round your own ship, so
   cashing out in a crowded heart catches rivals there too. A fuse that runs out blooms small.
5. **The point.** Every bloom that catches a rival is a Debuff-class combat hit through `CombatHitDrain.TryAdmit`
   (`VesselCombatHitByCrystalBlast` on `MantaBloomExplosionImpactorDataContainer`): it takes petals (Charge scales how
   many), silences the victim for 1 s per petal up to 8 s, and pays one point. One per victim per second. A silenced
   Manta cannot score.
6. **The shells wear down.** Blooms destroy the tiles they reach, so a long match opens the shells. That is the
   platform's blast doing what it does everywhere; nothing in the mode regrows them.

**Nothing about the Manta changes** (the LOCKED vessel rule). The fuse is the Manta's own at every rung: Bloomrush's
per-intensity fuse override (`MantaBombRules.FuseSecondsOverride`) predates that rule and is deliberately not copied,
and the generator fails if the controller or the AI mentions it. The mode's levers are the shells, the heart's
crystals and the AI.

## The intensity ladder

| | Name | Shells (radii, u) | Gates per shell | Gate half-angle | Danger rim | Omni | Prisms | Danger | Spawn ring |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| I1 | Halo | 280 | 8 | 24.25° | none | 4 | 624 | 0 | 380 |
| I2 | Corona | 190, 310 | 6 | 19.75° | none | 3 | 1,334 | 0 | 410 |
| I3 | Chromosphere | 150, 250, 350 | 5 | 17.25° | 6.25° | 2 | 2,262 | 242 | 450 |
| I4 | Flare | 130, 220, 310, 400 | 4 | 14.25° | 6.75° | 1 | 3,703 | 284 | 500 |

Volumes: 606,528 / 1,296,648 / 2,198,664 / 3,599,316. The innermost gate is 237 / 131 / 90 / 65 u across.

- **A shell** is `round(4 pi r^2 / 32^2)` flat tiles, 18 x 3 x 18 u, one at each point of a Fibonacci sphere, facing
  out. Gates are round holes, one per point of a smaller Fibonacci sphere; each shell's gates turn 37° further about a
  fixed axis than the last, so no straight line runs from the rim to the heart. A rim is a band of DANGER tiles round
  each gate.
- **Flush, never interpenetrating.** Neighbouring tile centres are at least 27.9 u apart, more than a tile's 25.5 u
  diagonal, so no two tiles can touch at any in-plane angle; shells are at least 90 u apart.
- **The mirror is exact.** There are no random draws. The only comparisons that change a count (inside a gate,
  inside a rim) are measured by the mirror, and the generator fails if any tile sits within 1e-4 (cosine) of either
  boundary: that is why the gate angles are quarter-degree values. The bake and the clip audit, which run the compiled
  C#, laid exactly 624 / 1,334 / 2,262 / 3,703 prisms and the mirror's volumes, with 0 clipping.

**The approved drawing vs the build.** The drawing used 30 u tile spacing; the build uses 32 u and 18 u tiles, which
kept I4 under the 4,000-prism budget (3,703 against the drawing's 4,220). I4 also has four shells at 130 / 220 / 310 /
400 u, the drawing's I4 radii at 90 u spacing. The gate and rim angles moved by a quarter degree for the mirror's
margin. The shapes, the gate counts and the crystal ladder are the drawing's.

## AI: the stinger

`CoronaStinger` arms every AI Manta with `AIPilot.SetExternalTargetProvider` and **presses nothing**: every Charge verb
is passive, and the hull's own autopilot drive Soars on a straight stick (`MantaAnalogTurnBoostExecutor`). It reads the
bomb bay (`MantaStingActionExecutor`: `ArmedBombs`, `PlantedBombs`, `FuseRemaining`) to choose a goal:

1. **Silenced** (the rung's chance to break off): the heart's nearest omni crystal.
2. **Time to cash** (planted bombs at the rung's count, or the fuse at the rung's seconds): the nearest omni crystal.
3. **A bomb of energy**: the cheapest rival not already carrying a bomb, led by its velocity, passed off its hull by
   the rung's graze offset.
4. **No energy**: along the nearest shell at the rung's standoff (inside skimmer reach, outside hull reach).

**Routing through the gates.** A goal in another band (between other shells) is reached through the arena's own
gates (`SpawnableCorona.GateDirection`, the geometry the C# lays): line up on the gate's axis outside it, fly through,
and bend round the inner shell when a straight chord would cut it.

| Ladder (Easy, Medium, Hard, Legendary) | |
|---|---|
| `aiRetargetSeconds` | 1.2, 0.9, 0.6, 0.4 |
| `aiHuntRange` | 350, 500, 650, 850 u |
| `aiLeadSeconds` | 0, 0.15, 0.3, 0.45 |
| `aiGrazeOffset` | 9, 7, 5, 4 u off the rival's hull |
| `aiSkimStandoff` | 10, 9, 8, 7 u off a shell |
| `aiGateError` | 0.5, 0.35, 0.2, 0.1 of a gate's radius |
| `aiCashAtBombs` | 1, 2, 3, 3 |
| `aiCashAtFuseSeconds` | 11, 9, 7, 6 s |
| `aiBreakOffWhenSilencedChance` | 0, 0.5, 1, 1 |
| `aiSilencedPreyFocus` | 1, 1, 1.5, 2 |

Difficulty is the seat's AI level (`GameDataSO.AIDifficultyFor`), never the intensity; the generator fails if the
stinger reads `SelectedIntensity`, presses an input or stops routing through `GateDirection`.

**Not an offline race model.** Corona has no offline sim of the Manta's flight, so a compute search cannot train its
AI yet. The `charge-arcade` step of `Docs/AI_SYSTEM/AI_PLAN.md` is where a trained Manta genome would come from.

## Known limits, to confirm in the editor

- **The platform autopilot's own obstacle handling** decides whether an AI hull grazes a tile on its way through a
  gate; the stinger only chooses the line. Watch the AI's prism-impact detonations at I3 and I4.
- **Planting needs speed parity.** An AI chasing a Soaring human from behind can graze without planting. That is the
  Manta's rule, not the mode's.
- **Blooms erode the shells.** Measure whether I1's single shell is still a shell after a full match.
- **15 blooms is a first cut.** The plan wants 3-5 minutes; time a 2v2 at I2.

## Files

| Role | File |
|---|---|
| Arena | `Controller/Environment/MiniGameObjects/SpawnableCorona.cs`, `_Prefabs/Spawnables/SpawnableCorona{1-4}.prefab` |
| Mirror | `Tools/Build/corona_budget.py` |
| Controller / AI / settings / rule / monitor | `Controller/Arcade/Corona/*.cs`, `TurnMonitors/CoronaBloomTurnMonitor.cs` |
| Assets | `_SO_Assets/Games/ArcadeGameCorona.asset`, `CoronaSettings.asset`, `Scoring Rules/CoronaScoringRule.asset`, `Cell Configs/Corona Cell/`, `Game Toasts/GameToastConfig_Corona.asset`, `Mode Previews/ModePreview_Corona.asset` |
| Generator | `Tools/Build/author_corona_assets.py` |
