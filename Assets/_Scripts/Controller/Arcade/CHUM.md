# Chum (`GameModes.Chum = 91`)

The Angler's **SPACE** game: a timed feed on a reef. Row 1 of `Docs/ElementalLoop/GAME_KIT_ROSTER.md` (Garrett,
2026-11-15: "make all the missing games you can"). It sits beside the Angler's other cards: Lantern Run (Time),
Kelpwood (Charge) and Glasshouse (Mass).

Shoals of neutral prisms hang in the cell. Paint a shoal with the brush, let go, and your school is thrown at it and
eats it; the next throw sends the other half (ANGLER.md 1c, 1m). A bigger school eats faster: every omni crystal is a
creature, and a grazer held in the lure's light joins it. **The score is the prisms you took off the board: every
prism your hull breaks, plus every reef prism your school eats.** The wild herds eat the reef too, so the clock and
the herds race you for it.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameChum.unity` (cloned from `MinigameGlasshouse.unity`: identity, monitor, cells, crystals and the controller's reef fields swapped) |
| Controller | `ChumController : MultiplayerDomainGamesController` (lays the reef on every peer, credits school meals on the server) |
| AI | `ChumForager`, a server driver that steers and holds the lure (below). Plan step `space-arcade` |
| Metric | `ScoringMetric.PrismsDestroyed` (5, reads `HostilePrismsDestroyed`), reused. Genre **Space** through `ModeGenre`'s metric fallback (no mode row) |
| Rule | `ChumScoringRuleSO`: timed, highest wins, `IsObjectiveReached` never fires; every line an authored template |
| Turn monitor | `ChumTimeTurnMonitor : NetworkTimeBasedTurnMonitor` |
| Round length | `EndConditionOverridesSO.chumRoundSeconds` (**90**, the arcade's target length, P-modes-02) |
| Comeback | rate **0.025** per prism: a quarter of a reference domain (a quarter of the I2 reef, 192 prisms) behind buys 1.2 element levels (the generator asserts >= 1) |
| Arena | four `Chum Cell Config`s (IntensityWise): the standard 1,200 u cell with Barren's nucleus (~392 u), no environment, no flora |
| Herds | four `Chum I<n> Tadpole Fauna Config Data`s, copies of Glasshouse's tadpole (a forager: it grazes the reef) |
| Omni crystals | the scene's `NetworkCrystalManager`, per intensity; they respawn inside the nucleus (LOCKED platform rule) |
| Objective arrow | `RampageObjectiveProvider`: the nearest omni crystal, i.e. the next creature |
| Toasts | 166 `ChumHint` (new), 130 lead change (the controller's own sampler), 30 comeback |
| Generator | `Tools/Build/author_chum_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Chum.png` (`/cardart`) |

## Design brief: Chum, 2026-11-15, claude/angler-space-chum

```
The fundamental it serves      Space: mass taken off the board. A shoal of prisms is eaten by a Domain's school
                               (Fauna), steered there by the pilot's paint
The principles it obeys        P-modes-08 (the mode changes nothing about the vessel), P-modes-13 / P-modes-16 (the Cell
                               is the environment: herds, nucleus, crystals), P-modes-12 (ecology present: the wild herds
                               eat the same reef), P-modes-02 (90 s)
The one it comes closest to    P-modes-08. The paint, the throw and the taming are exactly the Angler's own kit
                               (ANGLER.md 1c, 1g, 1m); the mode only lays prisms for it to aim at and counts what is
                               eaten. No vessel file, prefab or action SO is touched. The I3-I4 starting Space 5 is
                               the card's StartingElements (Brittle Star Time's precedent), not a kit change.
What it changes for a player   The Angler's Space card becomes a game: grow a school at the core, take it out to the
                               reef, paint a shoal, throw, and race the wild herds and the rivals to the last cores
The cheapest prototype         none: built straight in Unity, because every mechanic is already shipped on the Angler
                               and the Cell; the Vessel Studio was offered as an option
Who judges it, and where       Garrett (the Angler's designer), in the editor
The retire condition           in a playtest the school does not matter: ramming the shoals with the hull alone wins
                               matches, or the reef is gone to the wild herds before the school arrives
```

## The reef

`ChumReef` is pure (no scene, no random draw), so every peer lays the same reef from the intensity alone and the
tests and the generator's twin hold it. A shoal is a centre prism and concentric shells on a Fibonacci sphere, 15 u
apart; the prism is 7 u on a side, so no two touch (the smallest axis gap is 7.97 u, asserted). The shoals sit on a
golden-angle spiral over the sphere, a band of the way from the nucleus to the membrane. The prisms are
`Domains.Blue` (no team): every Angler's brush paints them as a rival's and every grazer eats them outside the
nucleus.

## How the score is kept

- **The hull.** A pilot who breaks a reef prism with their hull is credited by `StatsManager`, as in every mode.
- **The school.** A creature's bite is credited to "light FaunaPrefab", which names no player, so nothing would count
  it. `ChumController` watches its own reef prisms: a prism that dies with `DestroyedByCreature` set is credited, on the
  server, to the pilot whose Angler leashes the nearest live creature within `attributeRadius` (90 u). A wild grazer's
  bite credits nobody.
- **The whistle.** The server snapshots every pilot's prisms and the "eaten" part of it to every peer, and the results
  read the snapshot (`BuildResults(gameData, eatenOf)`).

## The intensity ladder

| | Name | Shoals | Shells | Prisms | Band | Crust | Omni crystals | Grazers |
|---|---|---:|---:|---:|---:|---|---:|---:|
| I1 | Shallows | 8 | 2 | 512 | 0.30 | no | 4 | 6 (2 herds) |
| I2 | Reef | 12 | 2 | 768 | 0.45 | no | 3 | 9 (3 herds) |
| I3 | Crusted | 12 | 3 | 2,124 | 0.45 | outer shell shielded | 3 | 12 (4 herds) |
| I4 | Barrier | 16 | 3 | 2,832 | 0.60 | outer shell shielded | 2 | 15 (5 herds) |

- **The crust.** No grazer eats shielded mass (LOCKED), so the wild herds cannot strip a crusted shoal. The card starts
  the Angler at **Space 5** on I3 and I4, whose Paint Shields cracks it; the hull breaks it too.
- Every rung stays under 3,500 reef prisms (the collider budget; the generator asserts it).

## AI: the forager

`ChumForager` arms every AI Angler with `AIPilot.SetExternalTargetProvider` and presses **one** input: the lure
(`InputEvents.Button1Action`, bound to `AnglerLure` on Angler.prefab; the generator asserts it), through
`PerformShipControllerActionsReplicated` / `StopShipControllerActionsReplicated`. The paint and the throw are the hull's
own autopilot (`AnglerLeapfrogAIPolicySO`), which paints toward whatever the forager steers at, so steering at a shoal
is the attack.

1. **Holding the lure:** stay put until the hold runs out, then let go.
2. **School under target** (`aiSchoolTarget`): the nearest omni crystal within `aiCrystalReach`, else the nearest wild
   grazer; fly to it and hold the lure within `aiLureDistance` (70 u).
3. **Otherwise:** the nearest shoal with prisms left (re-picked every `aiRetargetSeconds`), approached to a stand-off
   (`aiStandOff`, 220 u) and then circled, so the throw has room.

Difficulty is the seat's AI level, never the intensity:

| Ladder (Easy, Medium, Hard, Legendary) | |
|---|---|
| `aiSchoolTarget` | 2, 3, 4, 5 |
| `aiLureHoldSeconds` | 2, 2.5, 3, 3.5 |
| `aiCrystalReach` | 700, 1,100, 1,500, 2,000 |
| `aiRetargetSeconds` | 9, 7, 5, 4 |

Froglet Compute is paused, so no search was queued.

## Platform changes

- `GameModes.Chum = 91` (reserved by `GAME_KIT_ROSTER.md` row 1), `EnumIntegrityTests` 83 -> 84.
- `GameToastSituation.ChumHint = 166`.
- `EndConditionOverridesSO.chumRoundSeconds` (+ `Build` twin, getter, `LiveMatchesBuild` / `Apply` / `Capture` rows)
  and the window's field, label and build-summary rows.
- `MiniGameHUD.CreateObjectiveProviderForGameMode`: Chum -> `RampageObjectiveProvider`.
- `author_preview_spawns.SCENE_FOR_MODE` 91; `render_card_backgrounds.py` a Chum recipe and camera.

## Verification status

**Authored headless, not yet run in the editor.** What ran is listed in the PR. The edit-mode tests
(`ChumReefTests`, `EnumIntegrityTests`) compile in the reference compile but have not been run in Unity. The editor
steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

What only the editor can judge:

- whether the leapfrog policy really paints a Blue shoal and throws the school at it when steered there;
- how fast a school of three to five eats a shoal, against how fast the wild herds do;
- whether the crust on I3-I4 reads, and whether Paint Shields cracks it often enough to matter;
- the hull-vs-school balance (the retire condition above).

## Known limitations

- **A client pilot's school** is empty on the client (ANGLER.md follow-up 7): the host simulates every school, and
  credits are made on the server, so scores are right; a client sees its school only through the host's simulation.
- **Attribution is by proximity**: a bite is credited to the leashed creature nearest the prism within 90 u. Two
  schools feeding on one shoal at once can swap a few credits.
- **A pooled prism reused in the same frame** it died loses its credit (the controller settles once a frame).
