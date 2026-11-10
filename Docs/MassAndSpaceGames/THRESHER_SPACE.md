# The Thresher's Space game: the plan (2026-11-10, `claude/keen-rubin-rw7p6q`)

Status: **built headless, not yet run in the Unity editor** (`GameModes.Harvest = 88`, the mode doc is
`Assets/_Scripts/Controller/Arcade/HARVEST.md`). Shombith picked plan A on 2026-11-10 and answered §7 the same day.
This page keeps the design brief, the facts it rests on (read from the code, not the docs) and the plans set aside;
where the build moved a number, the table below says the built value and §9 says why.

Plan: [`PLAN.md`](PLAN.md) §4 (the seed) and §6 (the rules). Hull: `R_VesselActions/THRESHER.md`. Siblings read:
Rampage, Wrecking Ball (destruction races), Jackstraw (its Charge game), Thresher Time (its Time game).

## 1. Design brief

```
## Design brief: Harvest, the Thresher's Space game, 2026-11-10, claude/keen-rubin-rw7p6q

The fundamental it serves      mass is conserved and removed only by an active force (a vessel ability); the race
                               is that removal, the way Rampage is (DESIGN_PHILOSOPHY_EMERGENCE.md, "mass")
The principles it obeys        P-modes-08 (the vessel never changes), P-modes-13 / P-modes-16 (the Cell is the
                               environment; the mode owns only its sheaves), P-modes-12 (ecology present),
                               P-modes-02 (about 90 s for an experienced pilot), P-modes-17 (the card's Vessels list)
The one it comes closest to    P-modes-08. The AI presses A once at the whistle to leave Mass Mode, and the top
                               rung may start the hull at Space 5. The A press is the hull's own button through the
                               replicated path (Kelpwood's precedent: "a charge AI may press the hull's own trigger").
                               The Space 5 start is a card row (Brittle Star Time's I4 starts at Time 5). Neither one
                               changes what an ability does. If Shombith reads either as a kit change, it is dropped (§7).
What it changes for a player   the Thresher's chain-charges-the-ball loop becomes a race: mow a sheaf's stalks with
                               the chain to bank energy, keep the ball clean, then whip it into the sheaf's core,
                               where one charged blast takes hundreds of prisms
The cheapest prototype         the Vessel Studio's Thresher sandbox (the 2D chain prototype the hull came from),
                               with one sheaf, to answer the question in §4 before any Unity work. Otherwise none:
                               the arena is built Jackstraw's way, as a Python mirror drawn before the C# exists
Who judges it, and where       Shombith (who asked for the plan), in the editor at I2 then I4; Yash hears of it
                               (the Thresher's AI and Thresher Time are his lane)
The retire condition           the bank never gets spent on a core: pilots' blasts all go off on stalks, so the
                               sheaf reads as one undifferentiated pile and the game is Rampage with a chain
```

**The sentence a player understands:** *mow the stalks with your chain to charge the ball, smash it into the sheaf's
heart to blow the whole thing apart; first team to break 750 rival prisms wins.*

## 2. What the kit already does (read from `ThresherExecutor.cs` and the hit paths)

| The hull already does this | So the game is |
|---|---|
| Every chain link **slices** a rival prism it touches (taut or slack), and each kill banks `energyPerSlice` 1, up to 70 | the stalks are the fuel |
| The ball's next hit on a **rival** prism releases the whole bank as a Grizzly blast, `45 u × min(cap, 1 + 0.1 × bank)`, cap ×4 at rest, ×8 at full Space (360 u) | the core is the payday, and a bank spent on a stalk is wasted |
| Below smash speed the ball keeps only `crushKeep` of its speed per kill | a swing through stalks arrives slow and spends the bank early |
| A red-hot ball also destroys **own-domain** prisms, and the blast hits every domain (`AffectSelfOverride`) | your own colour's sheaves are worth nothing and cost you a bank |
| The chain **shields** own-domain prisms | mowing your own stalks hardens them for nobody |
| **Omni crystal: Windmill**: the ball circles the ship red-hot for 12 s at chain length | a crystal is a 12 s combine harvester: fly a sheaf's rim and it mows and smashes at once |
| **Space L5 Breaker**: a red ball's direct hit breaks super-shielded prisms; blasts do not (`AbsorbSuperShieldHit`) | a super-shielded core can only be cracked by a hot ball, not a blast |
| Every ball kill, chain cut and blast kill is credited to the pilot: `Prism.Damage/Slice(..., _status.PlayerName)` and `ExplosionImpactor` passes `shipStatus.Player.Name` (the blast is not anonymous) | `PrismsDestroyed` (hostile only) counts all three with no platform change |
| The hull **spawns in Mass Mode**, where the ball steals rival prisms and never explodes; the chain still slices | a pilot who never presses A scores only chain cuts, and every rival prism they steal becomes a target for the others |

Mass Mode stays exactly as it is. The score is destruction only (`PrismsDestroyed`; `PrismsStolen` is not counted), so
Mass Mode is the wrong tool here, and the game says so in one authored hint at the start ("Press {glyph:A} to break").
The game does not quietly become a Mass game.

## 3. The arena: sheaves

A standard 1,200 u cell (Jackstraw's `CapsuleMembrane`). The mode owns one thing: **sheaves**, laid by one baked
`CellEnvironmentSpawnableBase` (Jackstraw's pattern, an exact Python mirror for the census and the stills).

A **sheaf** is a **core**, a hollow ball of blocks that holds most of the mass, with **stalks** standing out of it
in every direction (no up). Each stalk is a column of thin prisms about a chain long (120 u), so a chain swung across
the rim cuts many stalks at once while the ball swings outside them. One sheaf is a whole loop: mow the rim, then slam
the heart. The core's radius is under the rest-Space blast radius (90 u), so a full bank clears a core in one blast.
That keeps a single blast inside a known prism count (see the budget below).

Colour is the target choice (Rampage's rule): a domain's sheaves score only for the other domains, and neutral
(`Domains.Blue`) sheaves score for everyone.

| | I1 Stubble | I2 Sheaves | I3 Stooks | I4 Granary |
|---|---|---|---|---|
| Sheaves (built) | 8, large | 12 | 16 | 22, small |
| Colour | all neutral | neutral, 1 in 4 a domain (one each) | a quarter each Jade, Ruby, Gold, neutral | the same |
| Core rind | none | shielded (pop, then kill) | shielded | shielded (no super-shields: §7, decision 2 was no) |
| Stalks a sheaf | 40 x 6 prisms | 24 x 6 | 16 x 5 | 12 x 5, every 4th stalk danger |
| Prisms (built) | 3,200 | 3,408 | 3,040 | 3,300 |
| Omni crystals | 2 × roster | roster | roster − 1 | 1 for the lobby, at the centre |

**The budget.** The numbers above are a shape, not yet measured. The mirror sets them so that (a) a core is at most
~250 prisms, so one blast at the rest cap never asks the prism damage pass for more than that (it is deferred over
frames anyway: `PrismSpatialIndex` damage budget), (b) the whole arena is within Jackstraw's I4 census (~3,600 prisms)
or the generator says why not, and (c) an I4 windmill (12 s × 184 u/s round the ship) cannot reach more than two
sheaves at once. A blast near one core must not take the next sheaf with it: the minimum gap between sheaves is greater
than the full-Space blast radius (180 u). The flora and fauna stay at the standard cell's spawn profile (P-modes-12):
they score as hostile mass like everything else.

**The target (built: 750).** One value for every rung, in `EndConditionOverridesSO`. The plan said 1,500; the build
cut it to 750 (§9). It is an estimate for 90 s (P-modes-02); the first timed playtest replaces it.

## 4. The question to answer first

A stalk is a rival prism, so **the ball touching a stalk spends the bank on the stalk**. If the ball cannot be kept off
the stalks while the chain mows them, every bank is spent small, and the retire condition in §1 is met. The geometry
that should allow it: the ball swings OUTSIDE a turn (THRESHER.md §9), so circling a sheaf with the sheaf on the inside
of the turn puts the chain across the stalks and the ball beyond their tips. Stalk length against chain length
(22-140 u, reeled by RT) is the dial. This is what the Vessel Studio sandbox answers in an afternoon: one sheaf, circle
it, count the bank at the moment the ball first touches a core prism.

If it fails, the fallback keeps the kit and moves the fuel: neutral stalk **fields** between the sheaves, and bare
cores. That is the first idea in §8.

## 5. The AI (plan step `space-arcade`)

- **Steering only**, `SetExternalTargetProvider` (Jackstraw's `JackstrawHunter`, Corona's `CoronaStinger`): head for
  the nearest hostile sheaf's rim, circle it with the sheaf on the inside of the turn for a few seconds, then steer
  through its core. Divert to an omni crystal within reach. The arena publishes the sheaf centres and radii
  (Corona's rule: the AI reads the geometry the C# lays from).
- **One press of A at the whistle**, through `PerformShipControllerActionsReplicated`, to leave Mass Mode. Without it
  the AI only ever scores chain cuts. The generator fails if the AI presses anything else.
- The hull's own `ThresherSlingAIPolicySO` keeps planting and winching. A second presser would fight it.
- What the AI **cannot** do: the aimed launch (off by default in the policy), choosing to hold its bank (it blasts on
  whatever rival prism the ball meets first), and Breaker shots it does not aim. Its skill is the seat's
  `AIDifficulty`, never the intensity.
- Froglet Compute is paused: no compute job.

## 6. The build (the `/arcadegame` recipe, once §4 and §7 are answered)

- `GameModes.Harvest = 88` (built): 87 is the highest on bleeding-edge and no in-flight branch claims 88 or above (scanned
  2026-11-10, 200 most recent branches). Re-scan at commit, then `check_switch_label_collisions.py`.
- `HarvestController : MultiplayerDomainGamesController`, a structural copy of `RampageController` (1 round, golf,
  server winner, `SyncFinalScores_ClientRpc`, `DomainRaceToasts`).
- `HarvestScoringRuleSO : RampageScoringRuleSO` (metric `PrismsDestroyed`, hostile only, its own words) and the
  Rampage turn monitor shape with `EndConditionOverridesSO.harvestPrismTarget` and every row `/EndGameConditions` lists.
- `ModeGenre`: nothing to add (`PrismsDestroyed` falls back to Space); a `ModeGenreTests` row pins Harvest to Space.
- `SpawnableHarvestSheaves` (pure geometry in a static class, `Hash01` draws, baked layouts) + `harvest_budget.py`, an
  exact mirror, parity-checked with the clip audit.
- Four `Harvest Cell Config`s (IntensityWise), the card on `ArcadeGames` (Vessels: Thresher), the preview, toasts
  (the A hint, a "core broken" beat), every word an authored field, all from `Tools/Build/author_harvest_assets.py`
  (`--check`).
- Sounds: an empty `EventReference` for "core broken"; the blasts are the hull's own.
- Card art through `/cardart`; edit-mode tests for the rule and the sheaf geometry; a still of I2 from the card renderer.
- Docs: `Controller/Arcade/HARVEST.md`, `PLAN.md` §7, `GAME_MODES_AND_CONTROLLERS.md`.

## 7. Decisions (answered by Shombith, 2026-11-10)

1. **The name**: Harvest.
2. **I4's Space 5 start**: no. No starting level and no super-shields; I4 is smaller, sparser, shielded sheaves with
   danger stalks.
3. **The AI's A press**: yes, once, through the replicated path (`HarvestReaper`).
4. **Prototype first or build**: build now. The §4 question is checked in the editor playtest.
5. **The plan**: A (sheaves), from the three drawn plans (A sheaves, B stalk fields with bare cores, C one monument).

## 8. Ideas set aside (so the next session does not re-pitch them)

- **Neutral stalk fields with bare cores between them.** Simpler, and the fallback if §4 fails. Weaker because fuel
  and payday sit in different places, so the loop becomes "fly through a field, then fly somewhere else".
- **A Rampage-style forest of flora.** It would work, and it would be Rampage with a chain: no shape asks for the
  Thresher's mow-then-slam.
- **One big monument to demolish, shared.** The race is then decided by who arrives charged first; there is no route
  choice and no colour reading.

## 9. What the build changed, and why (2026-11-10)

- **Sheaf counts 14 / 20 / 26 became 12 / 16 / 22**, and the stalks per sheaf fell: the drawings laid 4,591 and 4,450
  prisms at I2 and I3, past Jackstraw's 3,600 (the Thresher's other arena). The built ladder lays 3,200 / 3,408 /
  3,040 / 3,300, measured from the shipped C# by the layout bake and equal to the mirror on every rung.
- **I2's colour rule became one in four**: one in three gave 2 Jade, 1 Ruby, 1 Gold among twelve sheaves. One in four
  gives one each; a test holds every rung within one sheaf of fair.
- **The target 1,500 became 750.** The sheaves never grow back, so three domains' worth of the target must fit in the
  smallest hostile pool, or the field could run dry before anyone wins: at 1,500 it could not (I3's pool is 2,280).
  Wrecking Ball, the nearest played number, went from 1,500 to 650 after a solo run had not reached 1,500 in 178 s.
- **Ecology is off** (no flora, no fauna; P-modes-12 lets a mode opt out on purpose). The brief said the standard
  spawn profile; grazers eating sheaves would score for nobody and empty the field early.
- **The objective arrow** is Rampage's (the nearest omni crystal, in the clear centre), not a new sheaf finder.
- **No new sound slot.** The mode has no event of its own: every blast, crush and smash is the Thresher's.
