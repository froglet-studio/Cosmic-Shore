# Glasshouse (`GameModes.Glasshouse = 89`)

The Angler's **MASS** game: a timed garden. Asked for by Shombith's Mass and Space plan
(`Docs/MassAndSpaceGames/PLAN.md`, wave 1). It sits beside the Angler's other cards: Lantern Run (Time) and Kelpwood
(Charge).

An omni crystal gives an Angler a creature **and a seed**. Let go of the lure and the seed is planted in your colour.
Hold the lure over your plants and its light grows them three times as fast. **The score is the mass standing in your
colour when the clock runs out: your trail plus your garden.** Wild herds graze every garden outside the nucleus. A
rival paints your plants, and their school is thrown at them.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameGlasshouse.unity` (cloned from `MinigameKelpwood.unity`, its combat monitor swapped for a timed one) |
| Controller | `GlasshouseController : MultiplayerDomainGamesController` |
| AI | `GlasshouseGardener`, a server driver that steers and holds the lure (below). Plan step `mass-arcade` |
| Metric | `ScoringMetric.VolumeRemaining` (12), reused. Genre **Mass** through `ModeGenre`'s metric fallback (no mode row; `ModeGenreTests.MassGame_ResolvesMassThroughItsMetric` pins it) |
| Rule | `GlasshouseScoringRuleSO`: timed, highest wins, `IsObjectiveReached` never fires; every line an authored template |
| Turn monitor | `GlasshouseTimeTurnMonitor : NetworkTimeBasedTurnMonitor` |
| Round length | `EndConditionOverridesSO.glasshouseRoundSeconds` (**90**, the arcade's target length, P-modes-02) |
| Comeback | rate **0.00003** per unit of volume: a quarter of a reference domain (one pilot's whole-round trail plus five mature plants, ~207,000) behind buys ~1.55 element levels (the generator asserts >= 1) |
| Arena | four `Glasshouse Cell Config`s (IntensityWise): the standard 1,200 u cell with Barren's nucleus (~392 u), no environment |
| Garden species | four `Glasshouse I<n> Cactus Flora Config Data`s, copies of Rampage's cactus with **SpawnProbability 0** (nothing planted wild) and the rung's cap |
| Herds | four `Glasshouse I<n> Tadpole Fauna Config Data`s, copies of the Thresher Time I1 tadpole (Kelpwood's donor) |
| Omni crystals | the scene's `NetworkCrystalManager`, per intensity; they respawn inside the nucleus (LOCKED platform rule) |
| Objective arrow | `RampageObjectiveProvider`: the nearest omni crystal, i.e. the next seed |
| Toasts | 164 `GlasshouseHint` (new), 130 lead change (the controller's own sampler; a timed mode has no target for `DomainRaceToasts`), 30 comeback |
| Generator | `Tools/Build/author_glasshouse_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Glasshouse.png` (`/cardart`, MODEL tier: the gardens, seeds and grazers read off the shipped assets at intensity 2) |

## Design brief: Glasshouse, 2026-11-10, claude/peaceful-cerf-vpl3x8

```
The fundamental it serves      Mass (prisms) grown by Flora, in a Domain's colour, inside a Cell: the score is mass a
                               domain put on the board and still holds
The principles it obeys        P-modes-08 (the mode changes nothing about the vessel), P-modes-13 / P-modes-16 (the Cell
                               is the environment: its species, herds, cap and phase thresholds), P-modes-12 (ecology
                               present), P-modes-15 (the points of interest are living flora hearts), P-modes-02 (90 s)
The one it comes closest to    P-modes-08. Planting, growth and taming are exactly the Angler's own Mass ability
                               (ANGLER.md §1d, §1g); the mode only decides what the cell grows from a seed and how many
                               seeds, grazers and plants there are. No vessel file, prefab or action SO is touched.
What it changes for a player   The Angler's Mass card becomes a game: fetch seeds from the core, plant them out past it,
                               stop and light your garden to grow it, and send your school at a rival's
The cheapest prototype         none: built straight in Unity, because every mechanic is already shipped on the Angler
                               and the Cell; the Vessel Studio was offered as an option
Who judges it, and where       Shombith (who asked for the plan) and Garrett (the Angler's designer), in the editor
The retire condition           in a playtest the garden does not matter: the trail alone decides matches, or the lure is
                               never worth stopping for
```

## The loop, in the Angler's own terms

1. **Fetch a seed.** The omni crystals respawn inside the glass nucleus, and the arrow points at the nearest one. Each
   one tames a creature into your school and leaves a **seed** on the Mass card (`AnglerTameByCrystalEffectSO`,
   `AnglerCommandExecutor.AddFloraSeed`). The species is the cell's own: a cactus.
2. **Plant it out past the core.** Hold the lure (A, the Mass card on a phone) and let go. Every seed you carry is
   planted at the esca in **your** colour (`PlantSeeds`, `domainOverride: _status.Domain`). A plant inside the nucleus
   is a sanctuary nothing can eat, so it counts nothing. The garden is everything outside it.
3. **Grow it.** While the lure is held, every plant within its reach grows `lureGrowth` (3x) as often
   (`HastenGrowth` / `Flora.Hasten`), and the grazers that wander into the light are tamed into your school. The hull
   stops while the lure is lit, and a stopped Angler lays no trail. That is the choice: fly and lay trail, or stop and
   grow.
4. **It spreads.** A plant that grows reproduces next to itself in its own colour (`Flora.SpawnOffspring`), up to the
   rung's cap. The cap is the **cell's**, shared by every colour, so a full arena is a contested one.
5. **Raid.** Paint a rival's plants (RT) and let go: your group is thrown at them and eats them. A Mass-element plant is
   armoured, so no grazer eats it; Paint Shields (Space 5) paints it anyway and the school cracks its shield.
6. **The whistle.** The domain with the most volume standing in its colour wins.

Nothing about the Angler changes (the LOCKED vessel rule). The mode's levers are the cell's species, herds, cap and
prism scale per rung, the omni crystals per rung, and the AI.

## How the score is kept

**Two producers, one stat, composed by deltas.** `StatsManager` already keeps every pilot's `VolumeRemaining` as the
trail they hold standing, in every mode: plus on lay, minus when a prism is eaten or stolen. A plant is environment
mass with no roster owner, so nothing credited it. On the server, every `gardenSampleSeconds` (1 s) the controller:

1. measures every domain's garden (`GlasshouseGarden.Measure`): each live plant in a playable colour, heart outside
   the nucleus, at **live prisms x leaf volume** (the cactus is a branching flora, which lays every prism at its leaf
   size, so the product is exact; `Flora.LeafVolume` already carries the cell's prism scale and the element's shape);
2. splits a domain's garden evenly between its pilots (`Share`);
3. moves each pilot's `VolumeRemaining` by the **change** in their share since the last sample (`ShareBook.Settle`).

The stat is therefore always "trail standing + garden standing", and neither producer overwrites the other
(`GlasshouseScoringTests.ShareBook_ComposesWithTheTrailCount`). At the whistle the controller measures once more,
assigns scores, and replicates a snapshot with each pilot's garden share, which the results screen shows as
"{count} grown".

**General rule:** when a mode scores mass that has no roster owner (flora, laid structure), credit it into the
existing stat by DELTAS from a book of what was credited, never by assignment. Assignment would erase the platform's
own count, which other code moves by deltas between your samples.

**Plants are simulated per peer.** `FloraNetworkSync` replicates where a plant stands, its colour and its element, not
its shape, and grazers eat each peer's copy. The **server's** garden is the one that scores, as the server's nucleus
is in Brood Rush. A client's own view of a rival's garden can differ in shape from the one that scores.

## Why a nucleus cell, and why nothing is planted wild

- **The nucleus makes grazing fair.** In a cell with no nucleus, a herbivore eats only prisms that are not its own
  colour, and wild herds are born in the controlling colour, which is the leader's. The leader's garden would be immune
  and the trailer's eaten: an anti-comeback current (Tapestry's reason for having no fauna). With a nucleus, everything
  outside it is edible whatever its colour (`Cell.IsPreyForHerbivore`), so every garden faces the same grazers.
- **Nothing planted wild.** The cell's species list exists only so a seed has a species (`AddFloraSeed` reads
  `SpawnProfile.SupportedFloras`). Every copy of the cactus is `SpawnProbability: 0` with no initial batch and no seeder
  floor, so the cell's spawner never plants it (`AllowSpawn` is `Random.value < 0` = never). Every plant of a colour was
  grown from that colour's seeds, and the no-domain-asymmetry invariant is untouched: the spawner plants nothing in
  anyone's colour.
- **The grazers live on the trails first.** The arena has no environment. The herds eat the Anglers' trails until
  gardens exist (`FaunaSpawnVolumeThreshold` 1: no herd before there is food), then the gardens.

## The intensity ladder

| | Name | Omni crystals | Grazers | Plant cap (shared) | Frenzy (count / volume) |
|---|---|---:|---:|---:|---|
| I1 | Seed Tray | 4 | 6 (2 herds) | 36 | 9,771 / 1,693,903 |
| I2 | Allotment | 3 | 9 (3 herds) | 30 | 8,571 / 1,446,943 |
| I3 | Meadow | 3 | 12 (4 herds) | 24 | 7,371 / 1,199,983 |
| I4 | Overgrazed | 2 | 15 (5 herds) | 18 | 6,171 / 953,023 |

- **A mature cactus** is 160 prisms at 1.4x the 5 x 5 x 3 leaf: ~32,928 volume (`PLANT_VOLUME`). An Angler's trail is
  ~471 volume a second while it flies (20 x 0.75 x 5.5 every 7 u at 40 u/s), ~42,400 over a whole round. So one
  mature plant is worth most of a round of flying. These are **estimates from the authored numbers**; how fast a cactus
  reaches 160 prisms in the live game (branching growth is random) is for the editor to measure.
- **Frenzy** freezes planting and growth, so it sits 25 % above the most the arena can hold (every plant the cap
  allows at maturity plus four pilots' whole-round trail). The garden never freezes short of the cap. Restless is at 7 %.
  The generator asserts both, and that a full arena stays under 9,000 prisms.

## AI: the gardener

`GlasshouseGardener` arms every AI Angler with `AIPilot.SetExternalTargetProvider` and presses **one** input: the lure
(`InputEvents.Button1Action`, bound to `AnglerLure` on Angler.prefab; the generator asserts it), through
`PerformShipControllerActionsReplicated` / `StopShipControllerActionsReplicated` so every peer's executor sees the
same hold. The paint and the throw are the hull's own autopilot (`AnglerLeapfrogAIPolicySO`), which paints toward
whatever the gardener steers at.

1. **Holding the lure:** stay on its bed until the hold runs out, then let go (the seeds are planted).
2. **Raiding** (its school holds `aiRaidAtSchool` creatures and a rival plant stands): steer at the nearest rival
   plant for `aiRaidSeconds`. The leapfrog policy paints it and throws the school.
3. **Carrying a seed, or due a tending visit** (`aiTendIntervalSeconds`): fly to its bed and press the lure on arrival.
4. **Otherwise:** the nearest omni crystal within `aiSeedReach`, else back to the bed.

Each AI's bed is `aiGardenBandFraction` (0.35) of the way from the nucleus to the membrane, on its own golden-spiral
direction (`BedFor`). Difficulty is the seat's AI level, never the intensity:

| Ladder (Easy, Medium, Hard, Legendary) | |
|---|---|
| `aiLureHoldSeconds` | 2.5, 3.5, 4.5, 5.5 |
| `aiRaidAtSchool` | 99 (never), 6, 4, 3 |
| `aiRaidSeconds` | 0, 6, 9, 12 |
| `aiSeedReach` | 900, 1,400, 2,000, 2,600 |

Froglet Compute is paused, so no search was queued. The mode has no offline model of its own yet. A Mass AI trained on
it would need a garden sim (seed supply, growth rate, grazing), which `/compute-scout` will list as uncovered.

## Platform changes

- `GameModes.Glasshouse = 89` (first authored as 88; Harvest, the Thresher's Space game on `claude/keen-rubin-rw7p6q`,
  pushed 88 and toast 163 first the same day, so this took 89 and 164, free on every branch),
  `EnumIntegrityTests` 80 -> 81.
- `GameToastSituation.GlasshouseHint = 164`.
- `EndConditionOverridesSO.glasshouseRoundSeconds` (+ `Build` twin, getter, `LiveMatchesBuild` / `Apply` / `Capture`
  rows) and the window's field, label and build-summary rows.
- `MiniGameHUD.CreateObjectiveProviderForGameMode`: Glasshouse -> `RampageObjectiveProvider`.
- `author_preview_spawns.SCENE_FOR_MODE` 89; `render_card_backgrounds.py` a Glasshouse recipe (MODEL) and camera.

## Verification status

**Authored headless, not yet run in the editor.** What ran: the generator and its `--check` (61 files, with a negative
control: a hand-edited `SpawnProbability` was named by line), `arcade_mode_lib.py --self-test`, the switch-label,
enum-reference, console-logging, conditional-compilation, self-referential-local and using-directive gates,
`check_gamelist_scenes.py`, `author_preview_spawns.py --check`, `render_card_backgrounds.py --check Glasshouse`,
`bake_prism_layouts.py --check`, and the reference compile (`Tools/Build/unity_refcompile`). The edit-mode tests
(`GlasshouseScoringTests`, `ModeGenreTests`, `EnumIntegrityTests`) compile but have not been run in Unity. The editor
steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

What only the editor can judge:

- how fast a seed becomes a plant worth flying back for, and whether 3x under the lure is felt;
- whether the herds graze a garden away faster than one Angler can tend it at I3-I4;
- whether the AI's beds sit somewhere worth defending, and whether the leapfrog policy really paints a rival's plants
  when steered at them;
- the trail-vs-garden balance (the retire condition above).

## Known limitations

- **A client pilot's school** is empty on the client (ANGLER.md follow-up 7), so a client's raids work only through
  the host's simulation. Solo play and the host are unaffected.
- **The garden share is split evenly** between a domain's pilots, so the results' "grown" column says what each pilot
  holds, not who planted it. The kit records no planter.
- **No offline model** of the garden exists yet (see AI).
