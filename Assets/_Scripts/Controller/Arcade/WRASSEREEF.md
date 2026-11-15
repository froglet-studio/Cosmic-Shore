# The Wrasse and Grouper's reef games: Cleaner Ram, Reefbuild, Blastline

Garrett, 2026-11-15: "build any missing games ... four games (one for each element is ideal)". The Wrasse had only its
Time game (Wrasse Run, `WRASSERUN.md`). These are its Charge, Mass and Space games, on the ids
`Docs/ElementalLoop/GAME_KIT_ROSTER.md` rows 21-23 reserved: GameModes 111-113, hint toasts 186-188.

**Status: authored headless, never run in Unity.** The C# went through the static gates. No editor has opened the
scenes, compiled the controllers or played a match.

## 1. The one lever: the reef's crystals

A game mode never changes the vessel (CLAUDE.md, LOCKED). The Wrasse ships with one lever a mode can pull without
touching it. A crystal is a petal on the hull, and the hull's level in an element sets how often that element's motes
come to the kite's board (`KiteNavigator.PickElement`: `MoteBaseWeight + MoteWeightPerLevel * level`). So a reef of
mostly-Charge crystals fills the board with Charge motes, and that is what a Charge game wants.

`WrasseReefCrystals` lays that reef. It uses Bloomrush / Tapestry's peer-local scatter: every machine lays the same
crystals from one seed and there is no network object. The scatter is weighted toward the game's element
(`favouredShare` 0.6). A taken crystal regrows after 12 s, in a new place drawn from `Hash(seed, slot, generation)`, so
two peers regrow the same one. The arena's own omni crystals (the scene's CrystalManager) still drop motes of every
element.

**Intensity is scarcity:** 24 / 16 / 10 / 6 crystals in a 900 u ball. Blastline's ladder also climbs Rampage's four
forest rungs.

## 2. The three games

| | Cleaner Ram (111) | Reefbuild (112) | Blastline (113) |
|---|---|---|---|
| Element | Charge | Mass | Space |
| Act | loop Charge motes to power the kite, then RAM a rival hull (a Strike combat hit that knocks petals loose) | loop Mass motes to lay a cluster of your prisms; the Grouper's trail adds to it | loop Space motes and a blast goes off where the loop closed; the head pops hostile prisms between blasts |
| Win | first domain to 8 rams (CombatPoints) | most prism volume standing at the 90 s whistle (VolumeRemaining) | first domain to 600 hostile prisms destroyed (PrismsDestroyed) |
| Arena | the bare Barren cell | the bare Barren cell | Rampage's living forest (Rampage Cell Configs 1-4) |
| Scene donor | MinigameKelpwood | MinigameTapestry | MinigameHarvest |
| Rule | `CleanerRamScoringRuleSO` (Jackstraw's) | `ReefbuildScoringRuleSO` (timed, Tapestry-shaped) | `BlastlineScoringRuleSO` (Harvest's) |
| Turn monitor | `CleanerRamStrikeTurnMonitor` | `ReefbuildTimeTurnMonitor` | `BlastlinePrismTurnMonitor` |
| AI plan | Hunt | Build | Clean |

Each game scores on a counter the platform already keeps. A ram goes through `CombatHitDrain.TryAdmit`. A cluster prism
is credited to its owner because `KiteNavigator.LayMassQueue` initializes it with the pilot's name. A blast is a named,
non-anonymous `AOEExplosion`.

Why the forest for Blastline: it regrows, so three domains racing to 600 cannot run the field dry (Harvest's sheaves do
not regrow). Why the Barren cell for the other two: there is no food web, so the only forces that move mass or chase
rivals are the pilots'.

**Comeback** (the shared comeback system) is sized as a function of the target. A quarter of the target behind buys at
least one element level (`wrasse_reef_common.comeback_error` holds this). Reefbuild's rate (0.001111) comes from a volume
model read off `KiteNavigatorConfig.asset`, so a retune of the seat moves it.

## 3. The AI

An AI Wrasse is already two AIs: the hull's `AIPilot` flies the Grouper, and the seat's own navigator AI flies the kite
(`KITE_NAVIGATOR.md`). Neither is touched. `WrasseGrouperGuide` only answers `AIPilot.SetExternalTargetProvider` with
where the Grouper should go, so the kite has the right things on its board. It never presses anything.

- **Hunt** (Cleaner Ram): with no charge on the kite, the nearest crystal. With charge, the nearest rival in range, led
  by its velocity, so it crosses the kite's board.
- **Clean** (Blastline): the nearest crystal in reach, else the densest mass that is not its own colour
  (`Cell.GetExplosionTarget`).
- **Build** (Reefbuild): the nearest crystal in reach, else its own cruise orbit through open water. When its domain
  trails by 15 % or the clock has 20 s left, it raids the leader's reef.

Difficulty is the seat's AI level (`AIDifficultyFor`), never the intensity. There are four rungs per ladder: retarget
3 / 2 / 1.4 / 1 s, crystal reach 250-550 u, hunt range 450-900 u, and lead 0-0.5 s.

## 4. Code and assets

- `Arcade/WrasseReef/`: `WrasseReefSettingsSO`, `WrasseReefCrystals`, `WrasseGrouperGuide` and
  `WrasseReefControllerBase`, shared by the three. The base lays the crystals on every peer when the countdown ends,
  arms the guide on the server, and ticks the shared race toasts (128-130) for the two races. For the timed game it
  samples the lead and posts `DomainRaceLeadChanged` instead.
- `CleanerRamController`, `ReefbuildController`, `BlastlineController` hold only what differs: plan, metric, hint,
  golf or not.
- End conditions live in `EndConditionOverridesSO` (`cleanerRamStrikeTarget`, `reefbuildRoundSeconds`,
  `blastlinePrismTarget`) and are edited in FrogletTools > Game Modes > End Game Conditions.
- Generators: `Tools/Build/author_cleaner_ram_assets.py`, `author_reefbuild_assets.py` and `author_blastline_assets.py`,
  plus the shared `wrasse_reef_common.py`. Each one emits the shared scripts' metas identically, so any one can run
  alone. `--check` diffs all 28 files per game and checks four things:
  - the ids are the roster's;
  - the ladders match the C# initializers;
  - the kite still reads element levels for its motes;
  - the guide never presses an input.
- The card art comes from `render_card_backgrounds.py` (MODEL tier for the two Barren cards; Rampage's forest for
  Blastline).

## 5. What a person checks in the editor

1. All three scenes open and compile, and a match starts with four AI Wrasses.
2. The reef appears at countdown end on host and client, in the same places, and regrows.
3. Cleaner Ram: a ram scores one point and the race ends at 8.
4. Reefbuild: clusters count toward the volume score and the whistle goes at 90 s.
5. Blastline: blasts and head pops count, own prisms are spared, and the forest keeps up with the target.
6. AI Groupers go for crystals and rivals as planned, and the kite AI still does its own work.
