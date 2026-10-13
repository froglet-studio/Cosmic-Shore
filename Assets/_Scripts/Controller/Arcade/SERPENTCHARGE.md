# Serpent Charge (`GameModes.SerpentCharge = 67`)

The Serpent's **CHARGE** game: a sniper race through Rampage's cactus forest. On the Serpent,
**Charge is the Sniper Shot's recovery**. The rifle waits 12 s between rounds at rest and 5.4 s at
Charge 10. The round is the same at every level: a 1.5° hitscan cone, 3,000 u long, that destroys
**everything hostile it contains**, because it pierces by default. It also strips any rival pilot
inside its reach. So the mode works like this:

1. Hold the scope.
2. Lay the reticle down the **length** of a stand rather than across it, and fire.
3. Spend the cooldown collecting Charge so the next round comes sooner.

The first **DOMAIN** to the target wins. A sniped rival pilot counts as a whole stand.

It follows the `<Hull><Element>` family: Sirocco (Butterfly · Space), Grizzly Charge, Grizzly Time
and Warpline (Stoat · Time).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameSerpentCharge.unity` (cloned from `MinigameSirocco.unity`) |
| Controller | `SerpentChargeController : MultiplayerDomainGamesController` |
| AI | `SerpentMarksmanAI`, a mode-local server driver (below) |
| Metric | `ScoringMetric.PrismsDestroyed` (5). **Reused**, with sniped pilots folded in |
| Rule | `SerpentChargeScoringRuleSO : RampageScoringRuleSO`, with `pilotSnipePoints` **60** |
| Turn monitor | `SerpentChargePrismTurnMonitor` (Sirocco's shape, its own getter) |
| Target | `EndConditionOverridesSO.serpentChargePrismTarget` (**800**) |
| Comeback | rate **0.008**: a quarter behind (200) buys 1.6 element levels |
| Arena | **Rampage's cactus forest**, referenced read-only (four intensity configs) |
| Economy | waves of **Charge crystals**, 10 every 40 s |
| Objective arrow | `HostileMassObjectiveProvider`, pointing at the densest standing hostile stand |
| Genre petal | **Charge** first, **Space** second (a `ModeGenre` row; the metric alone reads Space) |
| Generator | `Tools/Build/author_serpent_charge_assets.py` (`--check`) |

## The loop

1. **Scope** (hold the left trigger). The eyepiece opens and the right trigger becomes the rifle;
   unscoped, the right trigger is still the cloak (`SERPENT_SNIPER_SCOPE.md`).
2. **Find the long line.** A plant averages ~178 prisms. A round put *across* a stand takes a
   handful of them, while one put *down its length*, or through two stands that line up, takes
   dozens to a hundred. The angle is the whole skill.
3. **Fire** (right trigger, while scoped). Everything hostile in the cone dies, super-shielded mass
   included once Charge 5 "Pierce" is up. Any rival pilot inside the round's reach is stripped,
   and that pays **60**.
4. **Collect Charge while you reload.** Each Charge crystal shortens the next cooldown, and the
   comeback hands a trailing domain extra element levels on top.

Hull ramming still destroys hostile mass (`VesselDamagePrismEffect` on the Serpent's container)
and it counts, but it is the slow way.

## Reused, and why

- **The metric and rule are Rampage's**, with one fold on top. A round's kills reach
  `HostilePrismsDestroyed` through `StatsManager` exactly as every destruction mode's do: the round
  resolves on every peer and is credited once, by whichever machine simulates the shooter. The
  pilot strip was **already** a landed combat hit of class `Debuff` (`SniperShotAction.asset`
  `hitClass: 2`), so `CombatHitScoring.Credit` already pays it into `CombatPoints` at the rule's
  price. The rule prices it at 60 and folds `CombatPoints` into `DomainValue`, the Undertow shape.
  The end condition, remaining count, winner, HUD domain boxes and comeback all read `DomainValue`,
  so they agree by construction. `IsObjectiveReached` is overridden for that reason, because
  Rampage's reads the raw metric sum.
- **The arena is Rampage's**, the same four configs The Bends, Bloomrush and Sirocco reference.
  Intensity comes with it as the forest's density: 89 / 78 / 74 / 67 plants, 15,962 → 11,918
  prisms (it was 335 → 67 and 59,590 → 11,918 until a 2 FPS play test on 2026-10-13, see
  `RAMPAGE.md` § "Four intensities"). A forest suits a piercing round because the value of a shot lives entirely in its angle.
- **The scene** is a six-line delta from Sirocco's: identity guids, plus the four AI hulls going
  from Butterfly (13) to Serpent (7). The spawn ring is Rampage's own, 500 outside the nucleus.
- **The forest PLANTS IN over the opening ~45 s** (2026-10-13, Rampage's own change, inherited).
  It used to land one plant per frame, so intensity 2's 245 plants arrived in about half a second.
  The final forest is unchanged. See `RAMPAGE.md` § "The forest".

## Charge crystals (the mode's economy)

`chargeCrystalsPerWave` (10) elemental **Charge** crystals are scattered through a 320-1000 u shell
around the arena centre. Each wave clears the previous one and lays a fresh scatter every
`chargeCrystalWaveSeconds` (40).

They are **local objects**, laid identically on every peer. The server broadcasts only a wave
index (`LayChargeWave_ClientRpc`), and each peer derives the layout from `(seed, wave)`. This is
the Salvo / Dog Fight recipe with one change for a match-long economy: a wave REPLACES the last
one. Collection is per-peer, as it is in those modes, so the per-peer state never has to agree and
never accumulates drift. That is tolerable for the same reason it is there: a crystal scores
nothing. It only shortens a cooldown, and the cooldown is owner-authoritative anyway
(`SniperShotActionExecutor`'s documented limitation).

## AI: the marksman (`SerpentMarksmanAI`)

`AIPilot` writes a stick and a throttle and nothing else. On the Serpent prefab its generic ability
cycler toggles the cloak on a timer and never touches the scope, and the rifle only fires while the
scope is up. Without a driver every AI Serpent would fly the whole match unable to fire, which is
the Tollway rule's defect: an all-AI domain that cannot play. So every AI Serpent becomes a marksman.

- **It holds the scope** with one replicated LT press (the scope is a latch) and re-presses only if
  the latch did not take, for example if input was muted for the countdown.
- **It owns the right trigger, so it cloaks deliberately.** RT is the cloak unscoped and the rifle
  scoped, so AIPilot's ability cycler is told to keep off the cloak
  (`AIPilot.SuppressCycledAbility<CloakSeedWallActionSO>`; restored on disarm). Otherwise a cycler
  press while the scope was up would fire the rifle. Once per reload, and only when at least
  `aiCloakMinCooldownSeconds` (5) of it remains, the marksman does three things: it lowers its
  scope, presses the cloak **through the replicated input**, and raises the scope again a second
  later. Because the press is replicated, the hull vanishes, the trail fades and the seed wall is
  laid on every machine.
- **It cannot see a cloaked pilot.** A rival whose hull is cloaked
  (`CloakSeedWallActionExecutor.IsCloaked`) is not a hunting target and is worth nothing in a
  line the bot is weighing. A rival it was already hunting is dropped the moment they cloak, and
  the bot flies to where it last saw them. A round fired for the forest can still catch a cloaked
  pilot by accident, and that still pays.
- **It plans the fattest line it can bring to bear.** It samples aim points from the densest
  hostile stand (`Cell.GetExplosionTarget`, the same question the objective arrow asks) and from
  the mass ahead of its nose. It scores each line **with the rifle's own query**: `QueryCone` at
  the rifle's range, half-angle and radius floor, the rifle's target filter (never its own domain;
  armour only at Charge 5), and the round's reach rule for pilots. It weighs each line by how far
  it must turn to bring it to bear and steers down the winner (`SetExternalTargetProvider`).
- **It fires when the rifle is ready and the LIVE nose line is worth it.** "Worth it" means a
  fraction of what it planned. Once it has waited past its patience, anything worth
  `aiMinShotValue` will do, so a bot that cannot settle its nose still keeps the rifle working. The
  trigger is a replicated RT press, because the round resolves on every peer.
- **It hunts pilots** from intensity 2, pricing a rival in a line at the rule's 60. It follows the
  rival live and carries a fixed aim error for the plan's life.
- **On a long cooldown it detours to Charge crystals**, but only when at least
  `aiDetourMinCooldownSeconds` of reload remain, so a detour never costs a shot.

**Difficulty is the intensity row.** Every strength is a four-rung ladder on
`SerpentChargeSettings.asset`, and the generator asserts each one is monotone in the hard
direction:

| Ladder | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| re-plan every (s) | 1.6 | 1.1 | 0.75 | 0.5 |
| candidate lines | 6 | 10 | 16 | 24 |
| aim error (°, vs a 1.5° cone) | 2.5 | 1.5 | 0.75 | 0.25 |
| reaction to a ready rifle (s) | 1.0 | 0.6 | 0.3 | 0.12 |
| fire at ≥ this fraction of the plan | 0.5 | 0.6 | 0.7 | 0.8 |
| patience before settling (s) | 2.5 | 3 | 3.5 | 4 |
| pilot-hunt range (u) | — | 1,200 | 2,000 | 2,800 |
| Charge-crystal detour range (u) | — | 450 | 700 | 1,000 |

At intensity 1 the bot is slow and wobbly and never hunts you. At intensity 4 it re-plans twice a
second, aims within a quarter-degree, reacts in about a tenth of a second, will not waste a round
on a thin line, hunts you across nearly the whole range and keeps its Charge topped up.

## Platform changes (and the rule each one records)

- **`GameModes.SerpentCharge = 67`**, and `EnumIntegrityTests` moves to 63 members (ThresherTime took 66 on bleeding-edge first).
- **`EndConditionOverridesSO.serpentChargePrismTarget`** gets the full row set (field, build twin,
  getter, authored-target row, live/build compare, apply, capture) plus the editor window's field,
  "effective now" and build-summary rows.
- **`GameToastSituation.SerpentChargeScopeHint = 137`**, an idle hint. The rest is reuse: `82`
  (`DebuffHitsLanded` rose) **is** a pilot snipe in this mode, `83` is a prism milestone, and
  `128-131` are the race beats from `DomainRaceToasts`.
- **`MiniGameHUD`**: SerpentCharge → `HostileMassObjectiveProvider`.
- **`ModeGenre`** gets a row of **Charge first, then Space**, with `ModeGenreTests` updated. The
  metric alone reads Space, but this is the Serpent's Charge game: the rifle's rate is Charge, and
  a pilot is the most valuable thing a round can pass through.
- **AI cloaks reach every player** (2026-10-13). `AIAbility.ReplicateThroughInput` is a new
  opt-in on AIPilot's ability cycler. When it is set, the entry presses the vessel's own control
  through `PerformShipControllerActionsReplicated` instead of starting the ability locally on the
  server. It is ticked on the Serpent's cloak entry, and it fixes the cloak in **every** mode that
  flies AI Serpents: before, an AI cloak hid the hull, faded the trail and laid the seed on the
  server only. `R_VesselActionHandler.TryGetInputForAsset` answers which control runs a given
  authored asset. General rule: an AI press whose output rides no other replicated channel must
  be replicated (that method's own doc states it).
- **AIs do not track a cloaked pilot** (2026-10-13). `CloakSeedWallActionExecutor.IsCloaked` is
  one predicate, true from cloak to reveal. AIPilot's platform player seeking (Joust, Dog Fight,
  Broadside) skips a cloaked candidate and breaks an existing lock when its target cloaks, and the
  marksman does the same. `AIPilot.SuppressCycledAbility<T>` lets a mode drive an ability itself.
- **`SniperShotActionExecutor.MinPathRadius`** is a new read-only accessor, so a caller that
  *predicts* a shot asks exactly the cone the shot fires. General rule: an AI that scores its own
  options should score them with the weapon's own geometry, never a copy of its numbers.

## Verification status

**Authored headless; nothing has been opened in the editor.**

Proved:
- The generator's `--check`, watched failing twice: once on a mutated asset, and once on a ladder
  edited to make intensity 4 easier. Both passed once restored.
- The stand-down path, watched by drifting the Sirocco donor's AI templates.
- The standing out-of-editor gates.
- `/cardart`'s `--check` / `--strict`.
- **The C# compiles against real Unity references** (`Tools/Build/unity_refcompile`): the player
  config and the editor config both report no errors in project code, the editor config also
  gating the new test and the end-conditions window. The negative control works: a misspelled
  member injected into `SerpentMarksmanAI` failed the player compile in its own method body.

Not proved:
- That it plays.
- That the AI's aim settles on the Serpent's turn rate.
- That 800 is the right length.
- That `SerpentChargeScoringRuleTests` passes: it compiles, but EditMode tests need the editor.

`/verify-unity` was not available in this session (no editor, no `unity` binary), so the change is
filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **The target is reasoned, not measured.** It assumes a good line takes ~60 prisms and the rifle
  fires every 12 s at rest, which puts a solo pilot at ~2.5 minutes. Play-test it.
- **The pilot price (60) is a balance guess.** It is roughly one good line, so a duel can swing a
  match without the forest becoming irrelevant.
- **An AI cloaks at most once per reload**, and only in a reload of 5 s or more, because it must
  lower its scope to do it. At rest that is every other shot or so (the cloak itself runs 15 s).
- **Charge-crystal collection is per-peer** (see above). A client can see a crystal still standing
  that a remote pilot has already collected on its own machine, until the next wave replaces it.
- **Rampage intensity 1 is the heaviest cell in any arcade mode and is not profiled.** That is
  inherited, not added. The marksman's cone and sphere queries are server-only and throttled (one
  plan per 0.5-1.6 s and one live-line query per 0.1 s per bot), and have not been profiled either.
- **Card art** has not been rendered yet: run `/cardart`. Until then the card wears the shared
  placeholder.
