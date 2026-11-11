# Mass and Space games for the vessels that have none

Asked by Shombith, 2026-11-10: give every vessel that has no **Mass game** one, the way the Squirrel has Scurry and the
Scarab has Scarab Scramble. The exceptions are hulls without a real mass-making ability, which get a **Space game**
instead. The Dolphin is the model for that, and the Thresher and the Brittle Star join it.

Two prompts start the work, one session per vessel:
- [`MASS_GAME_PROMPT.md`](MASS_GAME_PROMPT.md) for a Mass game
- [`SPACE_GAME_PROMPT.md`](SPACE_GAME_PROMPT.md) for a Space game

Paste the prompt into a new session and fill in the vessel name. The seed for each vessel (its ability and its omni
crystal, quoted from `Docs/FleetCodex/abilities.json`) is in §4 below. Paste that seed under the prompt too.

## 1. What a Mass game and a Space game are

The game says it in code (`Assets/_Scripts/Data/ModeGenre.cs`, `ModeGenre.TryElementsFor`): a card's genre is its
element.

| Element | Kind of game | Scored on (the metric fallback) | Shipped examples |
| --- | --- | --- | --- |
| **Mass** | making or taking mass: the score is mass you put on the board, or took, and still hold | `PrismsRemaining`, `PrismsStolen`, `VolumeRemaining` (or an explicit mode row) | Scurry (Squirrel), Scarab Scramble and Tollway (Scarab), Hijack (Urchin), Tapestry (Tiger Moth) |
| **Space** | destroying or reducing mass: the score is mass taken off the board | `PrismsDestroyed`, `VolumeDestroyed`, `LifeformsKilled` | Rampage (Dolphin), Wrecking Ball (Scarab), Cleave (Swordfish), Salvo (Sparrow), Bloomrush (Manta), Sirocco (Tiger Moth) |
| Time | a race | `Crystals`, `Goals`, ... | every hull's Time game |
| Charge | working other pilots over | `Jousts`, `CombatPoints` | Joust, Dog Fight, Jackstraw, Flatline |

Two shipped Mass games show what "mass through the omni crystal" means:
- **Scurry** is a gather. `ModeGenre` gives it an explicit Mass row because its `Crystals` metric would otherwise
  read as a race.
- **Scarab Scramble**: the Scarab's omni crystal forges a ball, and a ball is "a crystal made into mass and driven
  home" (Garrett, 2026-10-27, quoted in `ModeGenre.cs`). Scoring in your own net gives a mass burst.

A Mass game turns the hull's own mass-making (its Mass ability, its omni crystal or both) into the score. A Space game
does the same with its destruction.

## 2. Where each vessel stands (measured 2026-11-10)

Read from the shipped cards with `vessel_mode_chart.cards()` and `mode_elements.build()`, the same readers the Fleet
Codex uses. Only hull-locked arcade cards are counted; arena cards (Brood Rush, Broadside, Regatta, Astro League) are
not a vessel's own element game.

| Vessel | Time | Charge | Mass | Space | This plan |
| --- | --- | --- | --- | --- | --- |
| Squirrel | Skim Race | Joust | Scurry (shared with Sparrow and Manta) | - | has its Mass game |
| Scarab | Scarab Rally | Undertow | Scarab Scramble, Tollway | Wrecking Ball | complete |
| Tiger Moth | Waystation | Dustup | Tapestry | Sirocco | complete |
| Urchin | Skein | - | Hijack | - | has its Mass game |
| Dolphin | Switchback | The Bends | - (no mass ability worth a game) | **Rampage** | **already done**: Rampage is its Space game |
| **Thresher** | Thresher Time | Jackstraw | - | Harvest | **Space game** (built, `claude/keen-rubin-rw7p6q`) |
| **Brittle Star** | Brittle Star Time | Flatline | - | Shatterplane | **Space game** (built, `claude/fervent-edison-ol600q`) |
| **Serpent** | Serpentine | Hood | - | - | **Mass game** (wave 1) |
| **Swordfish** | - | - | - | Cleave | **Mass game** (wave 1) |
| **Orca** | Breach | - | - | - | **Mass game** (wave 1) |
| **Angler** | Lantern Run | Kelpwood | - | - | **Mass game** (wave 1) |
| Manta | Redline | Corona | Scurry (shared) | Bloomrush | decide: own hull-locked Mass game? (§5) |
| Sparrow | Breakwater | Dog Fight | Scurry (shared) | Salvo, Wildlife Liberation | decide: own hull-locked Mass game? (§5) |
| Grizzly | Grizzly Time | - | - | Grizzly Charge (scores as Space) | **decide: Mass or Space-only?** (§5) |
| Rhino | Headlong | - | - | - | **decide: Mass or Space?** (§5) |
| Goby, Honeyguide, Jacks, Meerkat, Shrike, Stoat, Wrasse | one Time game each | - | - | - | wave 2: Mass or Space after their kits settle |

To re-measure:

```bash
python3 -c "
import sys; sys.path[:0]=['Tools/Build','Tools/Analytics/server']
import vessel_mode_chart as vmc, mode_elements
g=mode_elements.build()['modes']
for c in vmc.cards():
    if c['lists']==['ArcadeGames'] and len(c['hulls'])==1: print(c['hulls'][0], c['name'], g.get(c['mode']))
" | sort
```

## 3. The order

1. **The Space games: Thresher and Brittle Star.** Shombith named them, and both kits are built for destruction
   (the Thresher's wrecking ball and chain, the Brittle Star's five guns and ring explosion).
2. **Wave 1 Mass games: Serpent, Swordfish, Orca, Angler.** Each kit already makes mass (Seed Wall, Afterimage,
   Danger Shield, Lure, Tame & Grow), so the game has something to score.
3. **The decisions in §5**, then whatever they add.
4. **Wave 2**: the young hulls that have only a Time game.

Every vessel is its own session and its own branch. Sessions can run in parallel because each one is a different
swim lane. The only things they share are the numbers they claim (§6).

## 4. The seeds (paste under the prompt)

Quoted from `Docs/FleetCodex/abilities.json` on 2026-11-10. The session re-reads the vessel's own doc
(`Assets/_Scripts/Controller/Vessel/R_VesselActions/<HULL>*.md`) before designing anything, because abilities change.

**Thresher (Space)**
- Space: *Winch*. Stiffen the chain: slack it is a bungee; pull and it goes rigid, reels the ball in and cracks it up
  to speed. The chain slices rival prisms.
- Mass: *Mass Mode*. The ball steals rival prisms, shields your own and lays your trail, never exploding. This shows
  the Thresher also makes mass; the Space game must not quietly become a Mass game.
- Omni crystal: for 12 s the red-hot wrecking ball windmills round the ship across your line of flight, smashing what
  it sweeps.
- Siblings: Jackstraw (its Charge game, sticks that cost the ball speed), Wrecking Ball (the Scarab's Space game).

**Brittle Star (Space)**
- Space: *Space Mode*. All five guns fire together, converged or splayed by your aim. Firing heats the guns; overheated
  they stop.
- Omni crystal: *Ring Charge*. Adds a spare ring (up to two), thrown for the nucleus-wide ring explosion.
- Siblings: Flatline (its Charge game), Salvo (the Sparrow's gun-destruction game), Rampage.
- Vessel docs: `Docs/Studios/BRITTLE_STAR.md`, `R_VesselActions/BRITTLE_STAR.md`.

**Serpent (Mass)**
- Mass: *Seed Wall*. Stop dead and your last prism becomes a seed that pulls loose prisms into a growing herringbone
  wall, stealing rival prisms as they land.
- Omni crystal: an explosion, and beams from the crystal re-shield every seed wall you have standing.
- Siblings: Hood (its Charge game, hoods round a crystal heart, Shombith 2026-11-09), Hijack (stealing, nothing
  destroyed).

**Swordfish (Mass)**
- Mass: *Afterimage*. When the lit sword goes out, it keeps leaving prism copies of itself in its own shape for five
  seconds: walls you swing into place.
- Omni crystal: *Surge*. For 8 s a ring of swords surrounds the ship, swinging with your triggers.
- Siblings: Cleave (its Space game), Tapestry (painting mass), Tollway.

**Orca (Mass)**
- Mass: *Danger Shield*. The helm rings the hull in super-shielded prisms that ride with it for a few seconds, then drop
  as lingering danger prisms.
- Omni crystal: every gunner seat gets one more missile.
- The Orca is a crew vessel (`/duo-vessel`): the game is played by the whole crew, helm and gunners.
- Siblings: Breach (its Time game), Scarab Scramble.

**Angler (Mass)**
- Mass: *Lure, Tame & Grow*. Light the lure and stop: wild herbivores turn to you and join your school; plants in the
  light grow faster; let go to plant any seed an omni crystal left.
- Omni crystal: *Tame*. The crystal becomes a random creature that joins your school, and leaves a seed planted in your
  colour.
- This one is about the wildlife: load `/ecology` and `/fauna` first. Its invariants are locked.
- Siblings: Kelpwood, Lantern Run, Wildlife Liberation, Brood Rush.

## 5. Decisions waiting (for Shombith and Garrett)

1. **Grizzly.** Its Mass ability (*Weapon Systems*: shells, sniper, a plasma claw whose burn spreads) destroys. Grizzly
   Charge already scores as Space. Either the Grizzly is a Dolphin-type hull (Space only, nothing new to build) or its
   kit needs a mass-maker first. That is a vessel change for its owner, never something a mode adds.
2. **Rhino.** *Blade* throws prisms clear and *Furrow* shoves them aside; neither makes or destroys mass. A "move the
   mass" game (shove the prisms into your zone, scored on what stands in it) would be Mass. Decide before a session
   designs one.
3. **Manta and Sparrow.** Both share Scurry with the Squirrel. Is a shared card enough, or does each get a hull-locked
   Mass game built on its own kit (the Manta's *Decoy*, the Sparrow's *Turret Stance*, which builds mass as it shoots)?
4. **Squirrel Space** and the other empty cells in §2 are out of scope here unless asked.

## 6. Rules every session in this plan keeps

- **Swim lanes.** Run `/swimlanes` first. On 2026-11-10 none of these lanes was occupied; check again. Run
  `python3 Tools/Build/approvals.py check --me <person>` before committing.
- **A mode never changes the vessel (LOCKED, Shombith 2026-10-31).** The game is interesting through its track,
  environment and AI only. If the kit has no mass-making ability, stop and add it to §5.
- **Shared numbers.** The `GameModes` id (highest is 87 on bleeding-edge at the time of writing) is claimed at commit,
  after checking bleeding-edge and the in-flight branches, then `check_switch_label_collisions.py`. Several of these
  sessions will run at once; two of them taking 88 is exactly the 2026-10-13 collision.
- **The genre must come out right.** `ModeGenre.TryElementsFor` must answer Mass (or Space) for the new mode, through
  its metric or an explicit row, and `ModeGenreTests` pins it. The Fleet Codex then shows it as the vessel's Mass or
  Space game with no hand edit.
- **AI**: the mode's AI is built from the vessel's existing autopilot and names its plan step
  (`Docs/AI_SYSTEM/AI_PLAN.md`). Froglet Compute is paused; no compute jobs.

## 7. Status

| Vessel | Element | Branch | Mode | State |
| --- | --- | --- | --- | --- |
| Dolphin | Space | - | Rampage | shipped |
| Thresher | Space | `claude/keen-rubin-rw7p6q` | Harvest (`GameModes.Harvest = 88`) | built headless, not yet run in the editor ([`THRESHER_SPACE.md`](THRESHER_SPACE.md), `Controller/Arcade/HARVEST.md`) |
| Brittle Star | Space | claude/fervent-edison-ol600q | Shatterplane (90) | authored headless 2026-11-10; editor playtest pending (`SHATTERPLANE.md`) |
| Serpent | Mass | | | not started |
| Swordfish | Mass | | | not started |
| Orca | Mass | | | not started |
| Angler | Mass | `claude/peaceful-cerf-vpl3x8` | Glasshouse (89) | authored headless 2026-11-10, not editor-verified (`Arcade/GLASSHOUSE.md`) |
| Grizzly, Rhino, Manta, Sparrow | ? | | | waiting on §5 |

Each session updates its own row in the commit that lands the mode.
