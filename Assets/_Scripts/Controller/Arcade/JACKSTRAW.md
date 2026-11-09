# Jackstraw (`GameModes.Jackstraw = 84`)

The Thresher's **CHARGE** game: a brawl in a 0G heap of pick-up sticks. Shombith asked for a charge game for the
Thresher on 2026-11-09 and picked this one from three drawn ideas (the others were Hammer Throw, reefs you sling
between, and Keepbreaker, team keeps in each team's colour).

A heap of straight sticks floats in the middle of a standard cell, every stick pointing a different way, with no table
and no up. Each stick is 120 u long, about a chain long (the Thresher's chain runs 22 to 140 u), so a swinging ball is
always about to clip one. A **strike**, the ball or any chain link reaching a rival hull, is one point. The first
**domain** to the target wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameJackstraw.unity` (cloned from `MinigameSerpentGulag.unity`) |
| Controller | `JackstrawController : MultiplayerDomainGamesController` |
| AI | `JackstrawHunter`, a server driver that only steers (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `JackstrawScoringRuleSO`: `strikePoints` **1**, every other hit class **0**; every result line an authored template |
| Turn monitor | `JackstrawStrikeTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.jackstrawStrikeTarget` (**8**, a first cut: nobody has played it yet) |
| Comeback | rate **0.6**: a quarter behind (2 strikes) buys 1.2 element levels; on the Thresher, Charge is more petals per strike |
| Arena | four `Jackstraw Cell Config`s (IntensityWise) in the standard 1,200 u cell (`CapsuleMembrane`, the Boneyard's) |
| Environment | `SpawnableJackstraw`, one prefab per rung, baked (`Assets/Resources/PrismLayouts/SpawnableJackstraw_*.bytes`) |
| Census | `Tools/Build/jackstraw_budget.py`, an exact mirror of the C# |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 86 (a strike, new: `StrikeHitsLanded` rose), 128-130 (shared race beats via `DomainRaceToasts`), 159 (the hint, new), 30 (comeback) |
| Generator | `Tools/Build/author_jackstraw_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Jackstraw.png` (`/cardart`, RUN tier: the shipped generator at intensity 2) |

## The loop, in the Thresher's own terms

1. **Find a rival.** The arrow points at the nearest one. Pilots spawn on a ring just outside the heap, so the
   fight is in the sticks within seconds.
2. **Strike them.** The ball and all 24 chain links strike any enemy hull within `vesselHitRadius` at any speed,
   in Mass Mode too (`ThresherExecutor.HitVesselsAlong`). A strike steals 1 to 3 petals per element (Charge-scaled),
   counts as a Strike-class combat hit through `CombatHitDrain.TryAdmit` (one per victim per second) and **silences**
   the victim for 1 s per petal, up to 8 s (`Docs/ElementalAbilitySystem/SILENCE.md`). No knockback, slow or stun:
   PvP is petals only.
3. **The sticks are cover and a price.** Every stick prism is neutral (`Domains.Blue`), so every ball breaks it, and
   below smash speed the ball keeps only `crushKeep` of its speed per kill (THRESHER.md §2). A clean line to a rival
   is the hot one; a swing through the heap arrives slow. The heap thins as the match goes on.
4. **Refuel in the clearings.** A silenced pilot cannot hurt anything, so it runs for the crystals, which float only
   in the heap's clearings, out of the sticks' cover.
5. **The tell is the ball.** At smash speed it turns red (THRESHER.md §2); a red ball coming through the sticks is
   the thing to dodge.

Nothing about the Thresher changes (the LOCKED vessel rule): the mode's levers are the heap, the crystals and the AI.

## The intensity ladder

| | Name | Heap | Sticks | Jacks | Clearings | Red tips | Ironwood | Prisms | Shielded | Danger | Spawn ring |
|---|---|---:|---:|---:|---|---|---|---:|---:|---:|---:|
| I1 | Loose Sticks | r 360 | 34 | 0 | centre r 120 | none | none | 408 | 0 | 0 | 450 |
| I2 | Woodpile | r 470 | 78 | 6 | centre r 100 + 4 at 280 (r 70) | 1 stick in 8 | 1 in 10 | 1,122 | 38 | 20 | 560 |
| I3 | Bramble | r 570 | 140 | 14 | centre r 90 + 6 at 330 (r 65) | 1 in 5 | 1 in 6 | 2,114 | 110 | 56 | 660 |
| I4 | Thicket | r 680 | 230 | 26 | centre r 80 + 8 at 400 (r 60) | 1 in 3 | 1 in 4 | 3,566 | 258 | 154 | 770 |

Each step adds at least 69 % more cover (the generator fails under 25 %). Volumes: 36,536 / 104,312 / 198,261 /
335,961. Every stick and jack finds room on every rung.

- **A stick** is 12 flush prisms, 3 x 3 x 10 u. **Red tips**: its two end prisms are danger. **Ironwood**: its middle
  four prisms are shielded, two hits to cut.
- **A jack** is three sticks crossed at right angles round a shielded 9 u knot cube: six arms of 5 prisms leave the
  cube's faces, so no two arms share volume.
- **Clearings** are balls no stick enters: one at the centre and a ring on a Fibonacci shell (no up). The omni
  crystal spawns inside the centre one (`noNucleusSpawnRadius` 50); the controller floats 3 elemental crystals in
  each (`crystalsPerClearing`), which grow back 8 s after they are taken.

**Flush, never interpenetrating**: sticks keep 7.5 u between their axes, prisms are laid a hair under their length.
The clip audit measures **0 clipping pairs** on all four prefabs.

**The mirror.** Every draw in `SpawnableJackstraw` is `Hash01` of an integer key, every per-unit count is a const,
and the only count-affecting rejections are deterministic geometry (a stick that enters a clearing or comes within
the gap of another re-draws, up to 8 times). So `jackstraw_budget.py` reproduces it exactly: the clip audit ran the
shipped C# and got 408 / 1,122 / 2,114 / 3,566 prisms and the same volumes, at every intensity (the layout reads no
intensity, so it bakes). The generator also asserts no accepted piece sits within 0.001 u of a rejection boundary,
so float32 in the C# and float64 in Python cannot pick different heaps. It greps the C# for every constant it mirrors.

## AI: the hunter

`JackstrawHunter` steers every AI Thresher with `AIPilot.SetExternalTargetProvider` and presses nothing. A Thresher
scores by being in the right place, and the hull's own autopilot sling (`ThresherSlingAIPolicySO`, on the prefab)
still plants on any corner past about 70 degrees and swings round the stopped ball. With a rival beside that ball, the
swing is a plant-scythe: the chain sweeps a disc a chain wide.

1. **Pick a rival**: the cheapest by distance and bearing, cheaper if human (`aiHumanFocus` 2) or silenced
   (`aiSilencedPreyFocus`).
2. **Run through them**: aim at their led position, off by the rung's miss radius, and 60 u past them
   (`aiOvershoot`), so the hull passes and the trailing ball crosses them instead of braking short.
3. **Refuel when silenced**: on each silence, roll the rung's chance to break off to the nearest live crystal.

Difficulty is the seat's AI level (`AIDifficultyFor`), never the intensity; the generator fails if the hunter reads
`SelectedIntensity` or presses an input.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 1.2, 0.9, 0.6, 0.4 |
| `aiHuntRange` | 600, 800, 1000, 1400 |
| `aiLeadSeconds` | 0, 0.15, 0.3, 0.45 |
| `aiMissRadius` | 45, 30, 15, 5 |
| `aiRefuelWhenSilencedChance` | 0, 0.5, 1, 1 |
| `aiSilencedPreyFocus` | 1, 1, 1.5, 2 |

No offline race model exists for this mode, so `/compute-scout` cannot teach it by self-play yet. Per the AI plan
(`Docs/AI_SYSTEM/AI_PLAN.md`), this is the Thresher's Charge arcade game, where its charge genome would train
(`charge-arcade`); nothing has been trained here.

## Platform changes (all additive)

- `GameModes.Jackstraw = 84`, `EnumIntegrityTests` count 76 -> 77. 84 was checked free on bleeding-edge and all 643
  remote branches on 2026-11-09.
- `EndConditionOverridesSO.jackstrawStrikeTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its row in the End Game Conditions window.
- `GameToastSituation.StrikeLanded = 86` with its `StatToastDriver` watch on `StrikeHitsLanded`: any mode can now toast
  a strike (Dustup's dustings and Broadside's sword strikes are Strike-class too, but neither config asks for 86, so
  nothing changes for them). `JackstrawHint = 159`.
- `MiniGameHUD`: Jackstraw reuses `BendsObjectiveProvider`.
- Tooling (`Affects: Shombith's tooling`): the card-art harness had stopped compiling after the Regatta merge
  (`f7231d035c`) taught `SpawnableRegattaRails` new types. `render_card_backgrounds.SOURCES` gained
  `RegattaCourseFeatures.cs`, `RegattaLifeformSites.cs` and `ICellFaunaCorridorSource.cs`, and the harness shim gained
  `CellEnvironmentSpawnableBase.PreparesPlantingSites` and `Trail.TrailList`. That restored the card renderer, the
  layout bake and the clip audit, which share it.

## Verification status

Authored headless; **not yet run in the Unity editor**. Run offline on 2026-11-09:
`author_jackstraw_assets.py --check` (and watched it fail on a mutated cell config and a mutated C# constant),
`unity_refcompile` (0 errors in project code), the `/ship` gates (all OK), the clip audit (0 clipping, counts and
volumes equal to the mirror), `bake_prism_layouts.py --check`, `render_card_backgrounds.py Jackstraw --check`,
`author_preview_spawns.py --check`. The hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- The target (8) and the comeback rate are first cuts. Nothing measures strikes per minute here yet; the per-attacker
  petal ledger the Charge plan asks for does not exist.
- The hunter does not read the sticks: it flies the straight line at its rival, and the autopilot's own obstacle
  avoidance (if any) is all that steers it round a stick. A stick costs the ball speed, not the hull.
- The card's `card_backgrounds_manifest.json` row is written only by a full roster run, which stops today on
  BurrowRun's missing recipe (REFACTOR_BACKLOG S99), so it is not in the manifest yet.
