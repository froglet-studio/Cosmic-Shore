# Kelpwood (`GameModes.Kelpwood = 86`)

The Angler's **CHARGE** game: a hunt through a 0G ball of kelp. Shombith asked for a charge game for the Angler on
2026-11-09 and picked this one from three drawn ideas (the others were Grazing Grounds, drifting kelp beds as the
only ammo, and Wellspring, omni crystals refilling inside a central well).

Kelp strands radiate from a small open core in every direction, one per point of a Fibonacci sphere, so the ball has
no up. Sweep the eraser (LT) onto a rival to **lock** them: one creature leaves your school and hunts them, and every
**bite** is one point. The first **domain** to the target wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameKelpwood.unity` (cloned from `MinigameJackstraw.unity`) |
| Controller | `KelpwoodController : MultiplayerDomainGamesController` |
| AI | `KelpwoodHunter`, a server driver that steers and holds the eraser (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `KelpwoodScoringRuleSO : JackstrawScoringRuleSO`: a Strike pays **1**, every other hit class **0**; Kelpwood's own words on its asset |
| Turn monitor | `KelpwoodBiteTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.kelpwoodBiteTarget` (**12**, a first cut: nobody has played it yet) |
| Comeback | rate **0.4**: a quarter behind (3 bites) buys 1.2 element levels; Time is the element that moves a number here (`faunaSpeed`: hunters swim faster) |
| Arena | four `Kelpwood Cell Config`s (IntensityWise) in the standard 1,200 u cell (`CapsuleMembrane`, the Boneyard's) |
| Environment | `SpawnableKelpwood`, one prefab per rung, baked (`Assets/Resources/PrismLayouts/SpawnableKelpwood_*.bytes`) |
| Herds | four `Kelpwood Spawn Profile`s, each with a Kelpwood-owned copy of the Thresher Time I1 tadpole |
| Omni crystals | the scene's `NetworkCrystalManager`, per intensity, in a 200 u ball round the core |
| Census | `Tools/Build/kelpwood_budget.py`, an exact mirror of the C# |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 86 (a bite: `StrikeHitsLanded` rose), 128-130 (shared race beats via `DomainRaceToasts`), 161 (the hint, new), 30 (comeback) |
| Generator | `Tools/Build/author_kelpwood_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Kelpwood.png` (`/cardart`, RUN tier: the shipped generator at intensity 2) |

## The loop, in the Angler's own terms

1. **Find a rival.** The arrow points at the nearest one. Pilots spawn on a ring just outside the kelp.
2. **Lock them.** Hold LT (the Charge card on a phone): each time the eraser's 1.5 degree beam comes onto a rival
   vessel, one creature leaves your school and hunts it (`AnglerCommandExecutor.UpdateBeamLock`, ANGLER.md §1e). The
   creature nearest the rival goes, so a group you threw toward them earlier bites almost at once.
3. **The bite scores.** A hunter in reach bites: a Strike-class combat hit through `CombatHitDrain.TryAdmit`, at most
   one per victim per second (`lockBiteCooldown`), 3.2 petals at the Strike's fleet price, and it **silences** the
   victim for what it is worth, up to 8 s (`Docs/ElementalAbilitySystem/SILENCE.md`). No knockback, slow or stun: PvP
   is petals only. A silenced pilot's hunters still bite, but score nothing until the silence ends.
4. **Shake a hunter.** A hunter swims about 64 u/s (more with Time); the Angler cruises at 40. The way out is the
   Angler's own Time ability: dash to your other group, 75 % of the way in 0.35 s. Spread your two groups and you
   always have somewhere to go. A hunter starves 90 s after its last meal.
5. **Refill the school.** Tame the tadpoles grazing the kelp (hold the lure, Mass), or take an omni crystal at the
   core (one creature each). An empty school still gets its keeper back within a second (ANGLER.md §1e), so an Angler
   is never out of locks: the school decides how many hunts it can run at once and where its creatures start.
6. **The kelp is cover.** The beam is not stopped by prisms, so the strands do not block a lock: they hide a rival's
   hull, and a rival weaving between them is hard to hold under the nose. The tadpoles graze it, so it thins.

Nothing about the Angler changes (the LOCKED vessel rule): the mode's levers are the kelp, the herds and omni crystals,
and the AI.

## The intensity ladder

| | Name | Kelp radius | Strands | Danger tips | Danger collars | Prisms | Danger | Tadpoles | Omni | Spawn ring |
|---|---|---:|---:|---|---|---:|---:|---:|---:|---:|
| I1 | Kelp Bed | 288 | 28 | none | none | 560 | 0 | 24 (8 herds) | 3 | 368 |
| I2 | Kelp Forest | 320 | 44 | none | none | 880 | 0 | 18 (6 herds) | 2 | 400 |
| I3 | Kelp Tangle | 352 | 64 | 1 strand in 3 | none | 1,280 | 88 | 15 (5 herds) | 2 | 432 |
| I4 | Kelp Snare | 384 | 88 | 1 strand in 3 | 1 strand in 2 | 2,024 | 384 | 12 (4 herds) | 1 | 464 |

Volumes: 58,919 / 104,996 / 170,728 / 269,477. Each step adds at least 45 % more kelp (the generator fails under 25 %).

- **A strand** is 20 flush prisms, 3 x 3 u, laid along the chords of a gently swaying curve from 50 u to the kelp
  radius. **Danger tips**: its last four prisms are danger. **A collar**: six 2.5 x 2.5 x 6 u danger prisms round the
  strand, 7 u off its axis, on its twelfth prism.
- **Flush, never interpenetrating.** Strands come no closer than 16.5 u (I4); the sway is small enough that every
  bend leaves at least 0.19 u of air between two prisms' corners. The mirror asserts both on every run.

**The mirror.** Every draw in `SpawnableKelpwood` is `Hash01` of an integer key, every per-unit count is a const, and
nothing is ever rejected, so `kelpwood_budget.py` reproduces the census exactly. The generator greps the C# for every
constant and formula it mirrors.

**The approved drawing vs the build.** The drawing put a small ball of danger prisms on every second I4 strand; a ball
centred on the strand would interpenetrate it, so the build lays a collar round the strand instead. The herd markers
in the drawing were the herd COUNT; where the tadpoles spawn is the cell's spawner's choice.

## AI: the hunter

`KelpwoodHunter` arms every AI Angler with `AIPilot.SetExternalTargetProvider` and writes one input: the left
trigger, the eraser, on the hull's `InputStatus` (the channel a pad writes, replicated like every input; the hull's
own `AnglerLeapfrogAIPolicySO` writes the right trigger the same way). The leapfrog keeps its throws and dashes, aimed
at the same target.

1. **Short of creatures** (fewer than 2): fly to the nearest omni crystal.
2. **Silenced**: roll the rung's chance to break off to the nearest omni crystal, or straight away from the rival
   through the kelp.
3. **Otherwise hunt**: pick a rival (the cheapest by distance and bearing, cheaper if human or silenced), put the nose
   on its led position off by the rung's aim error, and hold the eraser for 0.3 s when the rival is inside the rung's
   gate, the lock interval has passed and fewer than the rung's hunters are out.

Difficulty is the seat's AI level (`AIDifficultyFor`), never the intensity; the generator fails if the hunter reads
`SelectedIntensity`, presses anything but the eraser, or stops pressing it.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 1.2, 0.9, 0.6, 0.4 |
| `aiHuntRange` | 400, 550, 700, 900 |
| `aiLeadSeconds` | 0, 0.1, 0.2, 0.3 |
| `aiAimError` (u) | 16, 9, 4, 1 |
| `aiLockGateDegrees` | 6, 4, 2.5, 1.5 |
| `aiLockIntervalSeconds` | 3, 2, 1.3, 0.8 |
| `aiMaxHuntersOut` | 1, 2, 3, 4 |
| `aiBreakOffWhenSilencedChance` | 0, 0.5, 1, 1 |
| `aiSilencedPreyFocus` | 1, 1, 1.5, 2 |

The hunter does not lure (taming takes holding still in the light) and does not dash on purpose to shake a hunter:
the leapfrog dashes when the dash points at its target. No offline race model exists for this mode, so
`/compute-scout` cannot teach it by self-play yet. Per the AI plan (`Docs/AI_SYSTEM/AI_PLAN.md`), this is the Angler's
Charge arcade game, where its charge genome would train (`charge-arcade`); nothing has been trained here.

## Platform changes (all additive)

- `GameModes.Kelpwood = 86`, `EnumIntegrityTests` count 77 -> 78. 84 and 85 were both claimed on 2026-11-09 (Flatline
  84 on bleeding-edge's reservation and 85 on its own branch, Jackstraw 85 on bleeding-edge); 86 was free on
  bleeding-edge and all 644 remote branches.
- `GameToastSituation.KelpwoodHint = 161` (160 is claimed twice: JackstrawHint on bleeding-edge and FlatlineHint on
  `claude/great-rubin-rur3xs`).
- `EndConditionOverridesSO.kelpwoodBiteTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its row in the End Game Conditions window.
- `MiniGameHUD`: Kelpwood reuses `BendsObjectiveProvider`.

## Verification status

Authored headless; **not yet run in the Unity editor**. Run offline on 2026-11-09:
`author_kelpwood_assets.py --check` (and watched it fail on a mutated cell config and a mutated C# constant),
`unity_refcompile` (0 errors in project code), the `/ship` gates (all OK), the clip audit (0 clipping, counts and
volumes equal to the mirror on all four prefabs), `bake_prism_layouts.py --check`, `render_card_backgrounds.py
Kelpwood --check`, `author_preview_spawns.py --check`. The hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
`author_card_backgrounds.py --strict` fails on bleeding-edge already (Tandava.png names no live card), not on this card.

## Known limitations

- **A client pilot cannot lock, dash or tame** (ANGLER.md follow-up 7): the Angler's creatures are leashed on the
  host, so a client's own executor holds no school. Solo against AI, the host and every AI seat play it; a second
  human joining as a client does not, until that follow-up lands on the Angler (a vessel change, its owner's).
- **Charge changes nothing about the lock** (`element_ability_table.py Angler`: "NO SCALING"). A mode cannot fix that;
  it is the Angler's open design slot.
- The target (12) and the comeback rate are first cuts. Nothing measures bites per minute here yet.
- The hunter flies straight at its rival; the autopilot's own avoidance is all that steers it round a strand.
