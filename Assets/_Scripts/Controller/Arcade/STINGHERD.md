# Stingherd (`GameModes.Stingherd = 117`)

The Jacks' **CHARGE** game: herd your danger creatures into rival hulls. Garrett asked for every missing elemental game
on 2026-11-15 ("make all the missing games you can"); the game kit roster (`Docs/ElementalLoop/GAME_KIT_ROSTER.md`
row 27) named this one and reserved its ids.

Part of a Jacks' herd is dangerous (the Charge ability, **Danger Share**). A danger creature that touches a rival hull
**stings** it - a Strike-class combat hit - and is spent. Every sting is one point. The first **domain** to the target
wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameStingherd.unity` (cloned from `MinigameKelpwood.unity`) |
| Controller | `StingherdController : MultiplayerDomainGamesController` (Kelpwood's shape) |
| AI | `StingherdDrover`, the hull's race coach (`IJacksRaceCoach`): it herds, it never flies far (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `StingherdScoringRuleSO : JackstrawScoringRuleSO`: a Strike pays **1**, every other hit class **0**; Stingherd's own words on its asset |
| Turn monitor | `StingherdStingTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.stingherdStingTarget` (**12**, a first cut: nobody has played it yet) |
| Comeback | rate **0.4**: a quarter behind (3 stings) buys 1.2 element levels |
| Arena | **Kelpwood's**, referenced: its four `Kelpwood Cell Config`s (IntensityWise), the kelp ball, its tadpole herds and its omni crystals |
| Start levels | the card's `StartingElements`: Charge 5 at I1, 3 at I2, 1 at I3, at rest at I4 |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 86 (a sting: `StrikeHitsLanded` rose), 128-130 (shared race beats via `DomainRaceToasts`), 192 (the hint, reserved), 30 (comeback) |
| Generator | `Tools/Build/author_stingherd_assets.py` (`--check`) |
| Card art | not rendered yet (below) |

## The loop, in the Jacks' own terms (JACKS.md)

1. **Herd.** The jacks repel the herd (each herder's ring), so a herd swims away from them. Run your two jacks round
   to the far side of your herd from a rival hull and it drives toward that rival. Rival Jacks hold still while they
   herd, so a herding rival is a parked target; a netting rival is a moving one.
2. **Sting.** A danger creature that comes within reach of a rival hull stings it (`JacksExecutor.UpdateContacts`: a
   Strike through `CombatHitDrain.TryAdmit`, one per hull per `strikeCooldown` 1.2 s), takes petals and silences the
   rival for what the hit is worth, and is spent. No knockback, slow or stun: PvP is petals only.
3. **Keep the herd big.** The herd never starves below its floor (30) and an empty herd is reborn, but a big herd is
   more danger creatures. The tadpoles grazing Kelpwood's kelp are wild leashable herbivores: bring a herder's ring
   near them and they join.
4. **Move the board.** Take off to net, and the hull jumps to halfway between the jacks. The herd does NOT follow a
   flying hull across the cell, so the game is won on the board, and a flight is a short hop behind your own herd.
5. **Defend.** Your rivals' herds sting you too. Fly off (a herd cannot keep up with a flying hull), or drive your
   own herd between you and theirs.

Nothing about the Jacks changes (the LOCKED vessel rule): the mode's levers are the arena, the start levels and the AI.

## The intensity ladder

The danger is the ladder. A start level is the platform's lever (`SO_ArcadeGame.StartingElements`, Brittle Star Time's
and Decoy Run's shape), not a vessel change: the Jacks' own Charge curve (`dangerShare`, 0.25 at rest, 0.75 at level
10) turns it into the share of the herd that stings.

| | Arena (Kelpwood's) | Start Charge | Danger share | Attack (Charge 5) |
|---|---|---:|---:|---|
| I1 | Kelp Bed - 28 strands, 24 tadpoles, 3 omni crystals | 5 | 0.5 | yes: the herd goes for rivals within 200 u by itself, and every creature bites |
| I2 | Kelp Forest - 44 strands, 18 tadpoles, 2 omni | 3 | 0.4 | no |
| I3 | Kelp Tangle - 64 strands with danger tips, 15 tadpoles, 2 omni | 1 | 0.3 | no |
| I4 | Kelp Snare - 88 strands, tips and collars, 12 tadpoles, 1 omni | 0 | 0.25 | no |

The shares are read off the Jacks' authored curve (`JacksConfigSO.dangerShare`, linear from rest to level 10); the
generator asserts the start Charge falls with intensity and that I1 starts at the upgrade.

**Why Kelpwood's arena.** The arcade recipe says to reference an existing cell when it already authors what the mode
wants. Kelpwood's kelp is cover between pilots, its tadpoles are the herd's recruits, and its ladder already runs
from open to cluttered; forking it would have been a second copy of the same ball. Its omni crystals are a worm colony
on the Jacks (the omni ability), which eats prisms and creatures, rivals' herds included.

## AI: the drover

`StingherdDrover` is set as every AI Jacks' race coach (`JacksExecutor.RaceCoach`, the seam Decoy Run built), so the AI
herds on the drover's cycles instead of its shipped 14 s herd / 6 s net rhythm. Each cycle is a `JacksRacePlan` in the
controls a person uses:

1. Pick a rival: the nearest rival hull to the herd, an AI rival counted `aiHumanFocus` (2) times further away.
2. Plan a drive: each jack runs via a point beside the herd (so neither runs through it) to a point behind it on the
   far side from the rival, `aiStandOffRings` rings past the herd's spread and `aiFlankRings` rings either side of the
   line. No decoy is laid and none is flown to.
3. When both jacks are there the AI takes off (the hull jumps to halfway between them, behind the herd) and, after the
   seat-swap cooldown a person waits out too, herds the next cycle from there.

Between cycles the hull's own `AIPilot` steers at the rival (`SetExternalTargetProvider`), which turns the board toward
it, so the rival lies on the board the herd is drawn to. Mid-cycle it holds its heading. Difficulty is the seat's AI
level (`AIDifficultyFor`), never the intensity; the generator fails if the drover reads `SelectedIntensity`, presses an
input, lays a decoy or flies to one.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 2, 1.5, 1, 0.6 |
| `aiAimError` (u) | 120, 70, 35, 10 |
| `aiStandOffRings` | 0.9, 0.75, 0.6, 0.5 |
| `aiFlankRings` | 0.8, 0.7, 0.6, 0.5 |

No offline model of the herd exists, so `/compute-scout` cannot teach this mode by self-play. Per the AI plan
(`Docs/AI_SYSTEM/AI_PLAN.md`) this is the Jacks' Charge arcade game, where its charge genome would train; nothing has
been trained here.

## Platform changes (all additive)

- `GameModes.Stingherd = 117` (reserved by the roster; 91-116 are other vessels' games built in parallel),
  `EnumIntegrityTests` count +1.
- `GameToastSituation.StingherdHint = 192` (reserved by the roster).
- `EndConditionOverridesSO.stingherdStingTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its rows in the End Game Conditions window.
- `MiniGameHUD`: Stingherd reuses `BendsObjectiveProvider`. `author_preview_spawns.SCENE_FOR_MODE` maps 117.

## Verification status

Authored headless; **not run in the Unity editor and not compiled**. This session's container could not install a
.NET SDK (the network policy refused Microsoft's download hosts), so neither `unity_refcompile` nor the card renderer
ran. Run offline on 2026-11-15: `author_stingherd_assets.py --check` (and watched it fail on a mutated settings asset),
`arcade_mode_lib.py --self-test`, the switch-label, enum-reference, console-logging, conditional-compilation,
self-referential-local and using-directive gates, `check_gamelist_scenes.py`, `author_preview_spawns.py --check`,
`author_preview_intensities.py --check`. The hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **Card art.** The card still wears the shared placeholder: run `/cardart` (`render_card_backgrounds.py Stingherd`,
  RUN tier through Kelpwood's environment, no recipe code needed) where a .NET 8 SDK is installed.
- **Whether the drive works is a playtest question.** The drover's Strömbom drive is reasoned from the herd's steering
  (JACKS.md §4), not measured: how far a cycle moves the herd, whether the take-off hop lands too close to it, and
  whether the herd reaches a hull that is netting rather than parked.
- **Danger stings on a client-owned hull** (JACKS.md §6): a contact is decided on the hull's owner while the herd is
  leashed on the host. Solo against AI, the host and every AI seat play it; a second human as a client is untested.
- The target (12) and the comeback rate are first cuts. Nothing measures stings per minute yet.
