# Standoff (`GameModes.Standoff = 125`)

The Grizzly's **CHARGE** game. Garrett asked for every missing elemental game on 2026-11-15 ("make all the missing
games you can"); the game kit roster (`Docs/ElementalLoop/GAME_KIT_ROSTER.md`, row 35) named this one and reserved its
ids: `GameModes` **125** and hint toast **200**, so the sessions building the roster's other games in parallel never
take the same number. Its working name there was the same.

Square **posts** of prism tiles stand on a sphere round an open centre, each facing the centre like a parapet. Fire a
trigger bomb at a rival: it flies through the posts, and **when it touches their hull it sticks** (the bomb freezes at
the contact point, GRIZZLY_TRIGGER_BOMBS.md). Pull and release that trigger again to **blow it**. Every rival a blast of
yours catches is one point. **Dig In** behind a post to aim and recharge. The first **domain** to the target wins,
golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameStandoff.unity` (cloned from `MinigameCorona.unity`) |
| Controller | `StandoffController : MultiplayerDomainGamesController` (Corona's shape) |
| AI | `StandoffGunner`, a server driver that only STEERS (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `StandoffScoringRuleSO`: a `MissileBlast` hit pays **1**, every other hit class **0** |
| Turn monitor | `StandoffBlastTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.standoffBlastTarget` (**10**, a first cut: nobody has played it yet) |
| Comeback | rate **0.4**: a quarter behind (2.5 blasts) buys 1.0 element level. Charge (Dig In's Energy regen), Space (the cannon's blast) and Time (Rush) all move a number in this fight |
| Arena | four `Standoff Cell Config`s (IntensityWise) in the standard 1,200 u cell (`CapsuleMembrane`, the Boneyard's), no nucleus, no flora, no fauna |
| Environment | `SpawnableStandoff`, one prefab per rung, baked (`Assets/Resources/PrismLayouts/SpawnableStandoff_*.bytes`) |
| Omni crystals | the scene's `NetworkCrystalManager`, per intensity, in an 80 u ball at the open centre |
| Census | `Tools/Build/standoff_budget.py`, an exact mirror of the C# (no draws at all) |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 81 (a blast hit: `MissileHitsLanded` rose), 128-130 (shared race beats via `DomainRaceToasts`), 200 (the hint, new), 30 (comeback) |
| Generator | `Tools/Build/author_standoff_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Standoff.png` (`/cardart`, RUN tier: the shipped generator at intensity 2, a sticky bomb going off on a rival staged on it) |

## Why this is the Grizzly's Charge game

The roster's row: "Weapon Systems and the Charged Cannon on rival hulls, Dig In to hold a post". Dig In is the
Grizzly's Charge ability (`element_ability_table.py Grizzly`: Energy regen x1 at rest to x3 at Charge 10; Battle Sight
at Charge 5). The only thing the Grizzly does to another pilot is blow it up, and the existing "Grizzly Charge" card
(`GrizzlyCharge = 62`) is the proving ground whose preview scores prisms destroyed, so the loop counts it as the
Grizzly's SPACE game. Standoff is the duel: the posts give Dig In a place to be, and the sticky bomb makes "hit the
other pilot" a thing you can do on purpose with a weapon whose blast you otherwise ride.

## The loop, in the Grizzly's own terms

1. **Find a rival.** The arrow points at the nearest one. Pilots spawn on a ring just outside the posts.
2. **Stick a bomb on them.** Squeeze a trigger and let go: the bomb cruises (constant speed, no range limit) and flies
   THROUGH every prism. The moment it touches another hull it freezes there (`Projectile.VesselStruck`,
   `GrizzlyTriggerBombExecutor.HandleVesselStruck`). Squeeze harder for a bigger bomb and blast (25 u for a tap, 100 u
   for a full squeeze).
3. **Blow it.** Pull and release the same trigger. The blast is an `AOEGrizzlyExplosion`; every rival hull inside it is
   a `MissileBlast`-class combat hit (`VesselCombatHitByGrizzlyBlast`: one per victim per 0.5 s), takes petals,
   **silences** the victim for what it is worth, and is the point. A bomb you froze in the air yourself scores the same
   if a rival flies into its blast when you blow it.
4. **The cannon too.** The charged cannon (X / Q) blows up as the same blast, so a well-charged shot that lands beside
   a rival scores the same way.
5. **Dig in.** A Grizzly behind a post can stop dead (Dig In), keep aiming (the gun still turns) and get Energy back up
   to three times faster with Charge: a bigger cannon sooner. Your own blasts launch you away from them, which is how
   you leave a post in a hurry.
6. **The posts wear away.** Every tile is neutral, so every blast breaks the tiles it reaches (your own blast spares
   your own domain's mass only). A shielded rim takes two hits. A post that drew fire all match is a gap by the end.

Nothing about the Grizzly changes (the LOCKED vessel rule): the mode's levers are the posts, the omni crystals and the AI.

## The intensity ladder

| | Name | Outer posts | Grid | Radius | Inner posts | Shielded rim | Danger corners | Prisms | Shielded | Danger | Omni | Spawn ring |
|---|---|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|
| I1 | Open Ground | 8 | 5 x 5 | 260 | - | no | no | 200 | 0 | 0 | 3 | 360 |
| I2 | Earthworks | 12 | 6 x 6 | 280 | - | no | no | 432 | 0 | 0 | 2 | 380 |
| I3 | Redoubts | 14 | 6 x 6 | 300 | 4 (4 x 4, 130 u) | yes | no | 568 | 328 | 0 | 2 | 400 |
| I4 | Killing Field | 18 | 7 x 7 | 320 | 6 (4 x 4, 140 u) | yes | yes | 978 | 408 | 96 | 1 | 420 |

Volumes: 38,400 / 82,944 / 109,056 / 187,776. Each step adds at least 25 % more cover (the generator fails under it).

- **A tile** is an 8 x 8 x 3 u prism; a post's tiles sit on a 9 u grid, so neighbours never touch, and the post faces
  the centre (its tiles' thin axis along the radius). The inner ring is turned 23 degrees about a fixed axis so no inner
  post stands square in front of an outer one.
- **Clear of each other.** Two tiles of different posts never come closer than 147 u (I4); the mirror asserts every
  rung keeps a hull's room between posts.

**The mirror.** `SpawnableStandoff` makes no random draws: every post's direction is a Fibonacci point and every tile's
place a closed form, so `standoff_budget.py` reproduces the census exactly. The generator greps the C# for every
constant and formula it mirrors.

## AI: the gunner

An AI Grizzly already fires on its own: the hull's **autopilot bomb-jump** (`GrizzlyTriggerBombExecutor.AutopilotDrive`)
fires a full bomb whenever the stick is straight, freezes it 18 u ahead, flies past it and blows it, all through the
replicated press and release. A full blast reaches 100 u. So a straight pass within 100 u of a rival IS an attack, and
`StandoffGunner` only answers `AIPilot.SetExternalTargetProvider` with where to fly; it presses nothing (a second
presser would fight the bomb-jump, the Flatline lesson). The generator fails if it ever presses an input.

1. **Silenced** (its blasts score nothing until the silence ends): roll the rung's chance to break off behind the
   nearest post, on its far side from the centre.
2. **A rival in range**: fly a pass beside the cheapest rival (by distance and bearing, cheaper if human or silenced),
   led by its velocity, off its hull by the rung's pass offset - inside a blast's reach, outside its hull.
3. **Nobody in range**: work round the posts, from cover to cover.

Difficulty is the seat's AI level (`AIDifficultyFor`), never the intensity.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 1.4, 1, 0.7, 0.5 |
| `aiHuntRange` | 300, 420, 560, 720 |
| `aiLeadSeconds` | 0, 0.2, 0.4, 0.6 |
| `aiPassOffset` (u) | 70, 55, 40, 28 |
| `aiBreakOffWhenSilencedChance` | 0, 0.5, 1, 1 |
| `aiSilencedPreyFocus` | 1, 1, 1.5, 2 |

The AI never sticks a bomb on purpose, never digs in and never charges the cannon: the hull's bomb-jump is its whole
arsenal, and its bombs freeze 18 u ahead rather than reaching a rival. An AI that aims its bombs is a vessel change
(the autopilot drive lives in the Grizzly's executor), its owner's call. No offline model of this mode exists, so
`/compute-scout` cannot teach it by self-play yet. Per the AI plan (`Docs/AI_SYSTEM/AI_PLAN.md`), this is the Grizzly's
Charge arcade game, where its charge genome would train; nothing has been trained here.

## Platform changes (all additive)

- `GameModes.Standoff = 125` (the roster's reservation), `EnumIntegrityTests` count 83 -> 84 and a key-value case.
  The roster's other games add their own members in parallel: a conflict on the count is expected and is resolved by
  counting the members.
- `GameToastSituation.StandoffHint = 200` (the roster's reservation).
- `EndConditionOverridesSO.standoffBlastTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its rows in the End Game Conditions window.
- `MiniGameHUD`: Standoff reuses `BendsObjectiveProvider`.
- `render_card_backgrounds.py`: `SpawnableStandoff.cs` in `SOURCES`, a `CAMERAS` row and the staged sticky bomb.
- `author_preview_spawns.py`: `125: 'MinigameStandoff'`.

## Verification status

Authored headless; **not yet run in the Unity editor**. Nobody has flown it. What ran offline is listed in the PR
(`author_standoff_assets.py --check`, watched failing on a mutated prefab and a mutated C# constant, and the `/ship`
gates). The hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- The target (10) and the comeback rate are first cuts. Nothing measures blasts per minute here yet.
- Whether a cruising bomb reliably catches a rival's hull at speed (the swept vessel query) has only been read in the
  code, not seen. If sticky bombs prove too hard to land, the lever is the arena (closer posts, a smaller cell), never
  the bomb.
- The AI's only weapon is the bomb-jump (above), so a human who sticks bombs on purpose should beat it.
- The gunner flies straight at its goal; the autopilot's own avoidance is all that steers it round a post.
