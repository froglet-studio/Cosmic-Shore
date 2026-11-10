# Harvest (`GameModes.Harvest = 88`)

The Thresher's **SPACE** game: a race to destroy hostile prisms in a field of sheaves. Shombith asked for a Space
game for the Thresher on 2026-11-10 (`Docs/MassAndSpaceGames/PLAN.md`) and picked this one, plan A, from three drawn
plans. The plan, the brief and the plans set aside are in `Docs/MassAndSpaceGames/THRESHER_SPACE.md`.

> *Mow the stalks with your chain to charge the ball, smash it into the sheaf's heart to blow the whole thing apart;
> first team to the harvest target wins.*

A **sheaf** is a core of blocks with stalks standing out of it in every direction. Every prism the Thresher destroys
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
   ball (up to 70). Circle a sheaf with it inside your turn: the ball swings OUTSIDE the turn (THRESHER.md §9), past
   the stalk tips, while the chain crosses the stalks.
3. **Smash.** The ball's next hit on a rival prism spends the whole bank as one Grizzly blast, 45 u x
   (1 + 0.1 x bank), capped at x4 at rest Space and x8 at full. Every core is under the rest-Space full blast (90 u
   radius), so a full bank clears a core in one blast. **A bank spent on a stalk is wasted**: keeping the ball off the
   stalks while the chain mows them is the skill.
4. **Crystals.** The omni crystals spawn in the field's clear centre. One is the Thresher's windmill: for 12 s the
   red-hot ball circles the ship at chain length. Fly a sheaf's rim and it mows and smashes at once.
5. **Colour.** A sheaf in your colour scores nothing for you, and a red-hot ball or a blast destroys it all the same
   (own-domain kills are not counted). Neutral (blue) sheaves score for everyone.

Nothing about the Thresher changes (the LOCKED vessel rule): the mode's levers are the sheaves, the crystals and the AI.

## The intensity ladder

| | Name | Field | Sheaves | Core (r, blocks) | Stalks x prisms | Shielded cores | Danger stalks | Colour | Prisms | Crystals (4 seats) | Spawn ring |
|---|---|---:|---:|---|---|---|---|---|---:|---:|---:|
| I1 | Stubble | r 800 | 8 | 64 u, 160 | 40 x 6 | no | none | all neutral | 3,200 | 8 | 880 |
| I2 | Sheaves | r 880 | 12 | 58 u, 140 | 24 x 6 | yes | none | 1 in 4 a domain (1 each) | 3,408 | 4 | 960 |
| I3 | Stooks | r 940 | 16 | 52 u, 110 | 16 x 5 | yes | none | 4 each Jade, Ruby, Gold, neutral | 3,040 | 3 | 1,020 |
| I4 | Granary | r 1000 | 22 | 46 u, 90 | 12 x 5 | yes | every 4th | 6 / 6 / 5 / 5 | 3,300 | 1 | 1,080 |

The crystal counts are the scene's `CrystalManager` ladder, kept from Jackstraw (2 a pilot, 1 a pilot, one fewer than
the roster, one for the lobby). No super-shields at I4 and no starting element level (Shombith, 2026-11-10).

**The budget, asserted by the generator on every rung:**
- a core is at most 250 prisms, so one blast never asks the prism damage pass for more (it is deferred over frames
  anyway, `PrismSpatialIndex`);
- a core is under the rest-Space full blast radius (90 u), so a full bank clears it;
- two cores are at least 180 u apart surface to surface, the full-Space blast radius, so one blast never reaches two;
- the field is at most 3,600 prisms (Jackstraw's I4, the Thresher's other arena);
- **the field can always be finished**: the sheaves never grow back, so 3 domains x 750 must fit in the smallest
  hostile pool (everything not in one domain's colour). I3 is the tightest: 2,280 against 2,250.
- the domain colours are shared within one sheaf of each other.

**Flush, never interpenetrating.** Core cubes (6 u) sit at least 15 u apart on their sphere (a cube's diagonal is
10.4 u); a stalk's root is a whole block outside the core sphere; stalk roots sit at least 34 u apart; stalk prisms
are laid a hair under their length. Sheaves keep their stalk tips 20 u apart.

**The mirror.** Every draw in `SpawnableHarvest` is `Hash01` of an integer key and the only count-affecting rejection is
deterministic geometry (a sheaf whose reach enters the clearing or comes too close to another re-draws, up to 48
times). So `harvest_budget.py` reproduces it exactly; the generator asserts no accepted sheaf sits within 0.001 u of a
rejection boundary (float32 against float64) and greps the C# for every constant it mirrors.

**Ecology is off on purpose** (P-modes-12 lets a mode opt out): no flora, no fauna. The race is the sheaves; grazers
eating them would score for nobody and empty the field early.

## AI: the reaper (plan step `space-arcade`)

`HarvestReaper` steers every AI Thresher with `AIPilot.SetExternalTargetProvider`. It presses **one** input of the
hull's, through the replicated path: **A, to leave Mass Mode** (Shombith's yes, 2026-11-10; Kelpwood's precedent).
It finds the input from the hull's own binding maps (`ThresherActionSO.Control == MassMode`), checks every 1.5 s and
presses only while the hull is still building, so a lost press is pressed again and a landed one is never undone. The
hull's own `ThresherSlingAIPolicySO` keeps planting and winching on the corners.

1. **Take a crystal** when a managed omni crystal is within the rung's reach.
2. **Choose a sheaf**: the nearest not in its own colour that it has not worked in the last `aiRevisitSeconds`.
3. **Mow**: circle the sheaf just outside its reach (+ `aiMowStandoff`), steering 40 degrees ahead round the circle in
   the plane it is flying, for `aiMowSeconds` once it arrives.
4. **Strike**: turn in through the core and 120 u past it (`aiStrikeOvershoot`), holding the line 2.5 s.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiMowSeconds` | 2, 3, 4, 5 |
| `aiMowStandoff` | 0, 15, 30, 40 |
| `aiCrystalReach` | 250, 400, 550, 700 |
| `aiRevisitSeconds` | 8, 12, 16, 20 |

**What it cannot do:** hold its bank on purpose (it blasts on whatever rival prism the ball meets first, a stalk
included), aim a launch (the policy's aimed launch is off by default), or know a sheaf is already gone (it remembers
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

## Verification status

Authored headless; **not yet run in the Unity editor**. See the commit and `Docs/UNITY_VERIFICATION_CHECKLIST.md` for
what ran offline and the hand tests.

## Known limitations

- **The one question nobody has answered** (THRESHER_SPACE.md §4): can a pilot keep the ball off the stalks while the
  chain mows them? If every bank goes off on a stalk, the sheaf reads as one pile and the game is Rampage with a chain.
  That is the retire condition in the brief.
- The target (750) and the comeback rate are unplayed estimates (P-modes-02 asks for about 90 s).
- Kills are credited where the attacker is simulated. The Thresher's ball runs on every peer and names its pilot, which
  is the shape Wrecking Ball relies on; untested in a multiplayer session here.
- The hint says "Press A", which is the pad's button; a phone pilot's Mass Mode control is the touch map's. The toast is
  an authored field.
