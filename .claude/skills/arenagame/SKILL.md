---
name: arenagame
description: Use for ANY new Cosmic Shore ARENA game mode - a card that seats SEVERAL vessel classes (the Arena screen's roster, ArenaGames, launched through the ArenaLaunchPanel's vessel carousel) - or a change to how one is authored - a per-hull STARTING ELEMENT table (SO_ArcadeGame.StartingElements), a mixed-fleet balance model (Tools/Build/<mode>_balance.py), a rail/track environment several hulls use differently, an AI roster drawn from the card, or the arena rosters. Loads the arcade recipe by reference and adds what a multi-hull card owes on top: the fleet facts table (speed, boost economy, what each hull can do to shielded mass, which hulls an autopilot can actually drive), the four fleet-wide levers and what each one reaches, and the gates. Trigger when editing Assets/_SO_Assets/Games/GameLists/ArenaGames.asset, a card with more than one entry in Vessels, VesselStartingElements, GameDataSO.StartingElements, the arena launch panel, or Tools/Build/author_<mode>_assets.py for a mode whose card lists several hulls.
---

# Arena Game Mode Protocol

You are adding or changing an **arena game mode**: a card the player can bring **any of several
hulls** to. Everything the `/arcadegame` skill says still holds - the metric, the target, the
comeback rate, the controller shape, the generator library, the eleven registrations, the gates -
**read it first and follow it**; this skill is only what a multi-hull card owes ON TOP. The
difference is one sentence: an arcade card is a mode cut around one hull's kit, and an arena card
is a mode that must be **playable, winnable and readable in every hull it lists** - which is a
fleet-wide claim, and a fleet-wide claim is checked against the fleet, hull by hull, never
assumed from the one you happened to fly.

## 0. Read first

- `.claude/skills/arcadegame/SKILL.md` - the whole recipe. This file assumes it.
- `Docs/HomeHub/ARCHITECTURE.md` §3 - what the Arena IS: the arcade pointed at `ArenaGames`,
  the `ArenaLaunchPanel` (a `MinigameLaunchPanel` plus a vessel carousel and a SELECT VESSEL
  button - Start is dead until a hull is confirmed, per session, per pilot), and the three-roster
  contract (master = every card; `ArcadeGames` = master minus the arena cards; `ArenaGames` = the
  arena cards). A card in the master and neither grid is launchable and reachable from nowhere.
- `Assets/_Scripts/Controller/Arcade/REGATTA.md` - the worked example (every playable hull on a
  rail circuit) and the honest record of what the fleet-wide levers can and cannot close.
- `Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md` and `BROODRUSH.md` - the two older arena
  cards, both `MaxDomainsAllowed = 2` (a RULE: two goals), which is why neither is in the
  Maelstrom pool.
- The `/vessel` skill and `R_VesselActions/<HULL>.md` for **every** hull the card lists, not the
  one you are thinking about. `python3 Tools/Build/element_ability_table.py` prints which
  element reaches which ability on each hull, from the shipped assets.

## 1. The fleet facts a multi-hull card is designed against

Measured off the shipped prefabs and assets (2026-09-15; re-measure before trusting -
`Tools/Build/regatta_balance.py` reads them by key and prints them):

| hull | cruise → top | speed source | autopilot can use it? | vs shielded / super-shielded mass |
|---|---|---|---|---|
| Manta | 180 → 720 (×1.3 Time 10) | Soar (free; costs yaw) | YES (`MantaAnalogTurnBoostExecutor` drive) | ram = slow |
| Dolphin | 68 → 347 | drift-charge → discharge; skims for seed energy | no | ram = slow + half charge |
| Rhino | 60 → 1210 | ramp on a straight stick | yes, by the gesture | **energised sword pops super-shield**; no slow wired |
| Urchin | 65 → 300 on its OWN-colour rail (20 on a rival's; Time-5 Slipstream 300) | riding, no resource | rides (aim it down the rail) | rides the shell's envelope |
| Squirrel | 60 → 300 | skim energy (+0.1/contact, decays 0.3/s) | no | ram **resets** the boost |
| Serpent | 60 → 160 (×1.6 Time 10, duration too) | 4 charges × 3 s, regen 3.6 s | no | no slow wired |
| Sparrow | 35 → 135 (×1.5 Time 10) | indefinite boost (free) | no | ram = slow |
| Scarab | 216 → 324 (Time 1→1.5) | throttle ceiling | throttle only | no slow wired |

Three things every row above teaches:

- **Skimming super-shielded mass pays skim energy; ramming it still slows the hulls that wire
  the slow; only the energised Rhino sword removes it.** So a super-shielded structure is a
  speed SOURCE for skimmers and riders, an obstacle for four hulls, and untouchable by every
  cone, gun, plate and spike - exactly the property a shared track wants.
- **Rides and speed-by-terrain key on DOMAIN.** A neutral (Blue) rail is hostile to every
  rider. A structure meant to be ridden by every domain is one structure PER domain, braided so
  no domain's is shorter (`RegattaCourse`: one twist per lap, lanes within 1.9% of one length,
  asserted at 3%).
- **The AI is per hull.** `AIPilot` writes stick and throttle (0.6 default); a hull whose speed
  is an ABILITY needs a drive the way the Manta has one, or the bot races at cruise. A card that
  lists a hull no autopilot can drive has stated that an all-AI domain in that hull cannot win -
  say so in the doc (the Tollway rule, restated for speed rather than scoring).

## 2. The four fleet-wide levers, and what each one REACHES

Decide the balance BEFORE the C#, with a model, and write the residual down. In order of how
much they respect the fundamentals:

| lever | reaches | does not reach | where |
|---|---|---|---|
| **Starting elements** (`SO_ArcadeGame.StartingElements`, per hull, optionally per intensity) | Time on Manta (×0.7-1.3), Sparrow (×0.5-1.5), Serpent (×0.25-1.6), Scarab (×0.75-1.5); Dolphin fill rate | the Rhino's ramp, the Urchin's grind, the Squirrel's skim | the card; applied in `VesselController.Initialize` on every spawn path, shipped to clients in the config-sync RPC, cleared by the menu |
| **The course** (corner mix, mouths, floors) | every hull that turns to a radius - the fast straight-line hulls most | riders on a rail (a corner is the rail's) | `HeadlongCircuit` settings per intensity |
| **The environment** (what the arena is MADE of) | riders and skimmers, by giving them terrain; four hulls, by putting an obstacle in the line | the ram-immune hulls | a `CellEnvironmentSpawnableBase` per intensity, laid by the Cell |
| **The comeback** (`ComebackRatePerScoreDeficit`) | every element the trailing domain has, up to 10 - i.e. the same set as the first lever | the same holes | the card; a function of the TARGET |

**A per-mode speed multiplier, a per-hull lap count, or a per-hull scoring weight is a cheat**
- the outcome hard-coded past the fundamentals - and a per-hull prefab edit moves every other
mode. If the four levers leave a spread the mode cannot live with, the honest options are a
VESSEL change through `/vessel` (give the ability an elemental endpoint, so the element - and
the comeback - reaches it) or a race FORMAT the sport already has (a pursuit start), proposed to
the prompter, never quietly built into the controller.

**The model is the deliverable's spine.** `Tools/Build/<mode>_balance.py`: read every flight
constant off the prefab/asset by key, take the MEASURED course (run the real C# -
`Tools/Build/regatta_course_harness/` shows how, with a source-hash guard so a course edit cannot
ship on stale numbers), estimate each hull's lap time under its own boost economy, solve the
per-hull levels, and print the spread at rest and tuned. The generator imports it and ASSERTS the
tuned spread under the number the doc states. State plainly what the model is not (a frame time,
a playtest, the AI).

## 3. What an arena card owns beyond an arcade card

| Decision | The rule |
|---|---|
| **Vessels** | Every hull in the list must be able to finish AND win in a human's hands; every hull's kit has been read (§0). Grizzly, Termite, Falcon and Shrike are not shipped playable kits - `arcade_mode_lib.VESSELS` is the roster. **The list IS the gate**: the carousel offers every hull on it and never consults the Hangar's purchase lock (`SO_Vessel.IsLocked` - six of eight class assets author it, and honouring it left two hulls in every arena carousel; `Docs/HomeHub/ARCHITECTURE.md` §3.3). Every listed hull needs `IconActive` + `IconInactive` that are ITS OWN art (`ArenaRosterTests` compares file bytes - the Scarab shipped wearing the Sparrow's bake). |
| **Roster** | `g.register_arena_card(card)` - master + `ArenaGames`, never `ArcadeGames`. `check_gamelist_scenes.py` reports the arena grid's coverage. |
| **Domains** | 2..3 unless the mode has a fixed team shape; a fixed shape (`MaxDomainsAllowed = 2`) excludes the card from the Maelstrom. |
| **AI templates** | `vesselClass: 0` (Random) in the scene's `aiInitializeDatas`, so `PickAIVesselType` draws the bot's hull from the card and a bot grid is a mixed grid too. |
| **Starting elements** | One row per (hull, intensity) the model moves off rest; a hull at rest gets NO row (the platform default is rest; a row of zeros says the card decided it). `Intensity 0` = every rung; a rung-specific row wins. Levels are normalized (`-0.5..1`, since nothing above 10 is held). |
| **Preview** | `Vessel: -1` - the carousel's pick flies the preview. |
| **Toasts** | The tutorial is "what does MY hull do here"; an idle hint per verb family, not per hull. |
| **The doc** | The fleet table (§1) for THIS arena - what each hull does with the structure, what it costs each hull - and the balance table with the residual. |

## 4. The C# an arena card may add

Usually none beyond the arcade recipe. Two platform pieces already exist and are the ones to
reach for:

- `VesselStartingElements` (Data) / `GameDataSO.StartingElements` / `TryGetStartingElements` -
  the handicap table. Do not add a second way to seed a hull.
- `IPlayerSpawnLine` - a mode that lines everyone up (a start line behind gate 0) rather than
  spreading them round the cell; fair by symmetry (`CellSpawnFormation.BuildFacingRing`).

A structure several hulls use differently is a `CellEnvironmentSpawnableBase` on a cell config,
built from a seed the CONTROLLER can re-derive off the cell's `ExpectedConfig` (Skein's and
Regatta's pattern), with a pure course class the harness can compile and run.

## 5. Gates (in addition to the arcade skill's list)

```
python3 Tools/Build/<mode>_balance.py                 # prints the model; read the spread
python3 Tools/Build/author_<mode>_assets.py --check   # asserts the spread, the course hash, the rails
python3 Tools/Build/check_gamelist_scenes.py          # the arena grid's coverage line
python3 Tools/Build/render_scarab_card_icons.py --check  # the one generated card icon, if the Scarab is on the card
bash Tools/Build/regatta_course_harness/run.sh        # after ANY edit to a pure course file
```

The editor still owns: whether a hull actually latches / skims / clears the mouth at the speed
the model assumed, whether a guest's own hull carries the seeded levels (watch the HUD flowers:
fire petals on a handicapped hull, white on a helped one), and everything about feel.

## 6. What an arena card must NEVER do

- List a hull it has not read the kit of, or one whose autopilot cannot drive it without saying so.
- Paint a ridden or skimmed structure neutral and call it shared.
- Close a balance gap with a mode-local multiplier, a per-hull lap count, or a scoring weight.
- Author starting elements by hand. The model authors them; the generator asserts them.
- Register in `ArcadeGames`.
- Filter the carousel by the Hangar lock, or by anything other than the card's own `Vessels` list.
- Point a hull's `IconActive` at a codex bake without looking at it: a harvester that reads the
  prefab ASSET photographs what the asset shows, and a hull that builds or hides itself at Awake
  looks like a different ship there (`IProceduralHullSource` is how the Scarab tells it otherwise).
