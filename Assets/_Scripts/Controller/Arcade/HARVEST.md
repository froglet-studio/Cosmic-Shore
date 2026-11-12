# Harvest (`GameModes.Harvest = 88`)

The Thresher's **SPACE** game: a race to destroy hostile prisms in a field of sheaves. Shombith asked for a Space
game for the Thresher on 2026-11-10 (`Docs/MassAndSpaceGames/PLAN.md`) and picked this one, plan A, from three drawn
plans. The plan, the brief and the plans set aside are in `Docs/MassAndSpaceGames/THRESHER_SPACE.md`.

> *Mow the stalks with your chain to charge the ball, smash it into the sheaf's heart to blow the whole thing apart;
> first team to the harvest target wins.*

A **sheaf** is a core of blocks with stalks laid flat round it as a **wheel** (2026-11-10, the stalk lab's fix). Between
the sheaves drift clouds of **chaff** (loose straws) and solid **bales**; I3 adds **Space crystals for each team present**
and I4 adds **live elemental creatures** (Shombith, 2026-11-10: "its too empty right now"). Every prism the Thresher destroys
that is not in its own colour scores (`ScoringMetric.PrismsDestroyed`, so the card reads **Space** through
`ModeGenre`'s metric fallback). The first **domain** to the target wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameHarvest.unity` (cloned from `MinigameJackstraw.unity`) |
| Controller | `HarvestController : MultiplayerDomainGamesController` (Rampage's shape) |
| AI | `HarvestReaper`: steers, and presses A once to leave Mass Mode (below) |
| Metric | `ScoringMetric.PrismsDestroyed` (5), reused: `HostilePrismsDestroyed`, your own colour never counts |
| Rule | `HarvestScoringRuleSO : RampageScoringRuleSO`, every result line an authored template |
| Turn monitor | `HarvestPrismTurnMonitor` (Wrecking Ball's shape) |
| Target | `EndConditionOverridesSO.harvestPrismTarget` **750** at every intensity (an estimate, unplayed) |
| Comeback | rate **0.012**: a quarter behind (187.5 prisms) buys 2.25 element levels (Wrecking Ball's rate scaled) |
| Arena | four `Harvest Cell Config`s (IntensityWise) in the standard 1,200 u cell (`CapsuleMembrane`, the Boneyard's) |
| Environment | `SpawnableHarvest`, one prefab per rung, baked (`Assets/Resources/PrismLayouts/SpawnableHarvest_*.bytes`) |
| Census | `Tools/Build/harvest_budget.py`, an exact mirror of the C# |
| Objective arrow | `RampageObjectiveProvider`: the nearest managed omni crystal, in the field's clear centre |
| Toasts | 83 (every 150 prisms), 128-130 (shared race beats via `DomainRaceToasts`), 163 (the hint, new), 30 (comeback) |
| Generator | `Tools/Build/author_harvest_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Harvest.png` (`/cardart`) |

## The loop, in the Thresher's own terms (THRESHER.md §2-2c)

1. **Press A.** Every Thresher spawns in Mass Mode, where the ball steals rival prisms and never explodes. A steal
   destroys nothing, so it scores nothing here, and the prism it steals becomes a target for the other teams. The hint
   toast (163) says so at the start. The chain still cuts in Mass Mode, so a pilot who never presses A scores chain
   cuts only.
2. **Mow.** Every chain link slices any rival prism it touches, taut or slack, and each kill banks 1 energy in the
   ball (up to 70). Fly a lap in a sheaf's wheel plane: the chain trails you across the spokes (a lap cuts 38-92 in
   the stalk lab) while the ball swings outside the turn, past the stalk tips. Chaff mows the same way between sheaves.
3. **Smash.** The ball's next hit on a rival prism spends the whole bank as one Grizzly blast, 45 u x
   (1 + 0.1 x bank), capped at x4 at rest Space and x8 at full. Every core is under the rest-Space full blast (90 u
   radius), so a full bank clears a core in one blast. **A bank spent on a stalk is wasted**: climb out along the
   wheel's axle and dive down it into the core, where no spoke stands in the ball's way. A bale (27 blocks) is the
   other target worth a full bank.
4. **Crystals.** The omni crystals spawn in the field's clear centre. One is the Thresher's windmill: for 12 s the
   red-hot ball circles the ship at chain length. Fly a sheaf's rim and it mows and smashes at once.
5. **Team Space crystals (I3).** Each team present gets its own Space crystals (4 a team) in the open water between the
   sheaves. Only that team's vessels can take one (a rival flies through it); each grows back 12 s after it is taken.
   Space on the Thresher is its blast (x4 at rest Space, x8 at full), so a team that keeps its crystals hits harder.
6. **Creatures (I4).** Tadpoles, piranhas, brittlestars and sharks, each rolling one of the four elements. The grazers
   eat sheaf prisms, the sharks eat the grazers, and every creature drops its element's crystal as its heart.
7. **Colour.** A sheaf in your colour scores nothing for you, and a red-hot ball or a blast destroys it all the same
   (own-domain kills are not counted). Neutral (blue) sheaves score for everyone.

Nothing about the Thresher changes (the LOCKED vessel rule): the mode's levers are the sheaves, the crystals and the AI.

## The intensity ladder

| | Name | Field | Wheel sheaves | Core (r, blocks) | Spokes x prisms | Chaff clouds x straws | Bales (3x3x3) | Shielded | Danger spokes | Colour | Prisms | Omni crystals (4 seats) | Extra | Spawn ring |
|---|---|---:|---:|---|---|---|---:|---|---|---|---:|---:|---|---:|
| I1 | Stubble | r 820 | 10 | 60 u, 150 | 36 x 6 | 8 x 24 | 4 | no | none | all neutral | 4,058 | 8 | | 900 |
| I2 | Sheaves | r 880 | 12 | 52 u, 120 | 30 x 6 | 10 x 24 | 6 | yes | none | 1 sheaf in 4 a domain; bales neutral | 4,129 | 4 | | 960 |
| I3 | Stooks | r 940 | 16 | 46 u, 100 | 28 x 5 | 12 x 22 | 8 | yes | none | sheaves and bales a quarter each | 4,448 | 3 | 4 Space crystals a team | 1,020 |
| I4 | Granary | r 1000 | 20 | 42 u, 80 | 24 x 5 | 12 x 20 (r 100) | 8 | yes | every 4th | as I3 | 4,561 | 1 | 23 live creatures at most | 1,080 |

Chaff is always neutral (one or two 20 u straws, never closer than 7.5 u to another). Each wheel's axis is drawn per
sheaf, so the wheels face every way.

The crystal counts are the scene's `CrystalManager` ladder, kept from Jackstraw (2 a pilot, 1 a pilot, one fewer than
the roster, one for the lobby). No super-shields at I4 and no starting element level (Shombith, 2026-11-10).

**The budget, asserted by the generator on every rung:**
- a core is at most 250 prisms, so one blast never asks the prism damage pass for more (it is deferred over frames
  anyway, `PrismSpatialIndex`);
- a core is under the rest-Space full blast radius (90 u), so a full bank clears it;
- two cores are at least 180 u apart surface to surface, the full-Space blast radius, so one blast never reaches two;
- a bale is at most 250 prisms too (27);
- the field is at most 5,000 prisms (raised from Jackstraw's 3,600 when the field was filled; the prism system draws
  far more, and what needs a bound is one blast's reach and the colliders);
- every sheaf, cloud and bale found room (a skipped one fails the generator);
- the baked layout the scenes load has exactly the mirror's count;
- **the field can always be finished**: the sheaves never grow back, so 3 domains x 750 must fit in the smallest
  hostile pool (everything not in one domain's colour). I3 is the tightest: 3,434 against 2,250. At I4 the grazers
  eat sheaf prisms too, which only spends the field faster; I4's pool is 3,507.
- the domain colours are shared within one sheaf of each other.

**Flush, never interpenetrating.** Core cubes (6 u) sit at least 15 u apart on their sphere (a cube's diagonal is
10.4 u); a stalk's root is a whole block outside the core sphere; stalk roots sit at least 34 u apart; stalk prisms
are laid a hair under their length. Sheaves keep their stalk tips 20 u apart.

**The mirror.** Every draw in `SpawnableHarvest` is `Hash01` of an integer key and the only count-affecting rejection is
deterministic geometry (a sheaf whose reach enters the clearing or comes too close to another re-draws, up to 48
times). So `harvest_budget.py` reproduces it exactly; the generator asserts no accepted sheaf sits within 0.001 u of a
rejection boundary (float32 against float64) and greps the C# for every constant it mirrors.

**Life.** I1-I3 have no flora and no fauna (`Harvest Spawn Profile` spawns nothing). **I4 has creatures** (`Harvest I4
Spawn Profile`): Harvest's own copies of four species, so tuning them touches no other mode (Serpentine's pattern):

| Creature | Source config | Diet | Initial | Population | Live cap |
|---|---|---|---:|---:|---:|
| Shark | Garland | predator | 2 | 2 | 3 |
| Piranha | Scarab Scramble | grazer | 6 | 6 | 10 |
| Brittlestar | Skim Race | grazer | 2 | 2 | 4 |
| Tadpole | Skim Race | grazer | 4 | 4 | 6 |

Every source has `SpreadElements` on, so each creature rolls one of the four elements, shows it and drops that
element's crystal as its heart (the lifeform-to-crystal invariant). They are seeded by the cell's spawner and kept by
reproduction and starvation; the mode never culls them. Grazers eat opposing environment prisms, which here are the
sheaves. A creature's body is rival mass too: a Thresher that destroys it scores its prisms like any other.

**Team Space crystals (I3).** `HarvestController` lays `teamCrystalsPerTeamByIntensity` (0, 0, 4, 0) Space crystals for
every domain with a seat (Blue never counts), at open points of the field `SpawnableHarvest.CollectOpenPoints` draws
per team (30 u clear of every piece). Each is the Space crystal of the scene's `ElementalCrystalSetSO` with an
`ElementalCrystalImpactor` that answers only that team's vessels (`RestrictTo`); a taken one grows back after 12 s
(checked every 0.5 s). Every peer lays them from the same draws, so nothing is replicated.

## AI: the reaper (plan step `space-arcade`)

`HarvestReaper` steers every AI Thresher with `AIPilot.SetExternalTargetProvider`. It presses **one** input of the
hull's, through the replicated path: **A, to leave Mass Mode** (Shombith's yes, 2026-11-10; Kelpwood's precedent).
It finds the input from the hull's own binding maps (`ThresherActionSO.Control == MassMode`), checks every 1.5 s and
presses only while the hull is still building, so a lost press is pressed again and a landed one is never undone. The
hull's own `ThresherSlingAIPolicySO` keeps planting and winching on the corners.

1. **Take a crystal**: its own team's Space crystal first (I3), else a managed omni crystal, when within the rung's
   reach.
2. **Choose a sheaf**: the nearest not in its own colour that it has not worked in the last `aiRevisitSeconds`.
3. **Mow**: lap the wheel in its own plane, at the spoke tips plus `aiMowStandoff` (negative: inside the tips, so the
   trailing chain crosses the spokes), steering 40 degrees ahead, for `aiMowSeconds` once it arrives.
4. **Climb**: out along the wheel's axle to `aiAxleReachFactor` (1.4) x the reach, on the side it is already on,
   until within 60 u (`aiAxleArrive`) or 3 s (`aiClimbSeconds`).
5. **Dive**: down the axle through the core and 120 u past it (`aiStrikeOvershoot`), holding the line 2.5 s.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiMowSeconds` | 2, 3, 4, 5 |
| `aiMowStandoff` | 0, -10, -20, -25 |
| `aiCrystalReach` | 250, 400, 550, 700 |
| `aiRevisitSeconds` | 8, 12, 16, 20 |

**What it cannot do:** hold its bank on purpose (the axle dive keeps spokes out of the ball's line, but chaff or a
bale on the way still takes the bank), aim a launch (the policy's aimed launch is off by default), or know a sheaf is already gone (it remembers
only when it worked one, and comes back after `aiRevisitSeconds`). Difficulty is the seat's AI level, never the
intensity; the generator fails if the reaper reads `SelectedIntensity` or presses anything but Mass Mode.

No offline race model exists for this mode, so it cannot be taught by self-play yet. Per the AI plan
(`Docs/AI_SYSTEM/AI_PLAN.md`), this is the Thresher's Space arcade game, where its space genome would train
(`space-arcade`); Froglet Compute is paused and nothing has been trained here.

## Sounds

None of its own. Every sound is the Thresher's (the crush, the smash, the blast) or the platform's. No
`EventReference` was added because the mode has no event the hull does not already voice; a "core broken" sting
would need the controller to know a core is gone, which it does not track.

## Platform changes (all additive)

- `GameModes.Harvest = 88` (free on bleeding-edge and the 250 most recently pushed branches, 2026-11-10),
  `EnumIntegrityTests` count 80 -> 81.
- `EndConditionOverridesSO.harvestPrismTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its row in the End Game Conditions window.
- `GameToastSituation.HarvestHint = 163`.
- `MiniGameHUD`: Harvest reuses `RampageObjectiveProvider`.
- `ModeGenreTests.Harvest_IsTheThreshersSpaceGame` pins the card to Space through its shipped preview definition.
- `ElementalCrystalImpactor.RestrictTo(Domains)` (2026-11-10): an optional team gate, off unless called. Its one
  caller is `HarvestController`'s team Space crystals; every other crystal is unchanged. Affects Shombith's crystal
  impactor (#1654).

## Verification status

**The fuller field (2026-11-10, second pass):** `author_harvest_assets.py --check` OK (and watched it fail on a broken
bake count), `bake_prism_layouts.py` (4,058 / 4,129 / 4,448 / 4,561, equal to the mirror), the clip audit (0
clipping, 16 runs), the card re-rendered at I2, `unity_refcompile` 0 errors in project code. Not run: the Unity
editor, the EditMode tests (two added: bale colours, team crystals at I3 only). Hand tests in the checklist.

The first pass:

Authored headless; **not yet run in the Unity editor**. Run offline on 2026-11-10: `author_harvest_assets.py --check`
(and watched it fail on a mutated cell config, a mutated C# constant and an AI keyed on intensity), the `/arcadegame`
gates, `unity_refcompile` (player and editor, 0 errors in project code), the full `/ship` gates (ALL OK), the clip
audit (0 clipping; counts equal to the mirror), `bake_prism_layouts.py --check`, `render_card_backgrounds.py Harvest
--check`, `author_preview_spawns.py --check`. Not run: the EditMode tests (they need Unity). The hand tests are in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **Wheels are built; flown only in the lab.** With stalks in every direction a lap cut 0-11 (THRESHER_SPACE.md §10,
  `Tools/Build/harvest_stalk_lab/`); the wheels and the axle dive the lab measured (38-92 a lap, half a core a blast)
  are now the shipped field and the reaper's plan, but no one has flown them in Unity.
- The I4 creatures' numbers are Serpentine I4's roster scaled to this field, unplayed. Grazers eat sheaves, so a long
  I4 match ends with less to score on.
- The target (750) and the comeback rate are unplayed estimates (P-modes-02 asks for about 90 s).
- Kills are credited where the attacker is simulated. The Thresher's ball runs on every peer and names its pilot, which
  is the shape Wrecking Ball relies on; untested in a multiplayer session here.
- The hint says "Press A", which is the pad's button; a phone pilot's Mass Mode control is the touch map's. The toast is
  an authored field.
