# Regatta — the arena race

> `GameModes.Regatta = 56`. Every playable hull on the same closed circuit of eight switch
> rings, with three **super-shielded rails** — one per playable domain — braided along the
> racing line. Every pilot flies **three laps** of it in order. The race ends when the first
> pilot threads the last gate of the last lap, and the **team with the most gates threaded —
> every pilot's gates, summed** — wins (§6.5). Opponent AI fly **Squirrels** until the racing AI
> can drive every hull; teammates pick their **ally AI's hulls** on the launch panel (§6). It is an **Arena** card
> (`ArenaGames`, the `ArenaLaunchPanel` vessel carousel), and the first one built for the
> whole fleet rather than for a hull.

## 1. What the mode is asking

> **What is your hull FOR, on a course everyone shares?**

A Rhino ramps to 1200 u/s on a straight and pays five seconds of wind-up for every corner it
cannot hold. A Manta trades Soar for yaw one trigger at a time. A Scarab's ceiling is its Time
level. An Urchin latches onto the rail in its colour and lets the cable drive at 300 u/s
through corners that cost it nothing. A Squirrel skims the same rail for the boost energy that
is its only speed. A Dolphin chains a charge into a discharge and holds 347 through a drift. A
Serpent spends charges. A Sparrow holds its afterburner and picks lines.

The rails are what makes a mixed grid a race rather than a demonstration. They are ordinary
conserved mass, laid by the Cell like any authored environment, in the three domain colours,
**super-shielded** — so no cone, gun, plate or spike removes a prism of them and a rider's speed
resource is never shot out from under them. The only thing that opens a hole is the Rhino's
**energised** sword (`RHINO_ENERGY_SWORD.md`), and a rider bridges a hole at full pace
(`URCHIN_TRAIL_RIDER.md`, `DestroyedTerrainSpeed` 300). A shield swaps the mesh and the mass,
never the collider, so ~2,000 super-shielded prisms cost **zero always-on colliders**
(`BREAKWATER.md` verified `shieldMeshCollider.enabled = true` appears nowhere).

## 2. Who can do what to the rails — the table the mode rests on

Measured off the shipped code and assets (the vessel-numbers pass that preceded this mode):

| hull | on the rail | speed source | what a rail costs it |
|---|---|---|---|
| **Urchin** | grinds its OWN colour at 300 u/s (`FriendlyTerrainSpeed`); a rival's at 20 (Time-5 Slipstream: 300) | riding, no resource | nothing; corners are the rail's |
| **Squirrel** | skims any colour: +0.1 boost per contact to ×5 → 300 u/s | skim energy, decays 0.3/s | ramming a rail prism **resets the boost** (`VesselResetBoostPrismEffect`) |
| **Manta** | flies beside it | Soar (free; costs yaw) 720 u/s | ram = slow (`VesselChangeSpeedByPrism`) |
| **Rhino** | flies beside it; an **energised** sword pops a rail prism | ramp (straightness) 1200 u/s | none — no speed effect wired |
| **Dolphin** | skims for seed energy, not speed | charge (drift) → discharge 347 u/s | ram = slow + half the charge |
| **Serpent** | flies beside it | 4 charges × 3 s at 160 u/s, regen 3.6 s/charge | none wired |
| **Sparrow** | flies beside it | indefinite boost 135 u/s | ram = slow |
| **Scarab** | flies beside it | 216 u/s ceiling, ×1.5 at Time 10 | none wired |

Three rails, one per domain, because `TrailFollower` keys friendly terrain on **domain**: a
neutral rail would be hostile to every rider. In a two-domain lobby the third rail is hostile to
both sides and — by the braid's symmetry — no shorter than either (Hijack's third-colour rule).

## 3. The card balances the grid — with a platform capability it introduced

Element levels are the only lever that reaches a hull's speed without touching the vessel, and
they were never applied in multiplayer: `SO_Vessel.InitialResourceLevels` is read only on the
legacy single-player launch path, every class asset authors zeros, and `ResourceSystem.Start`
seeds nothing. So the card now carries **`SO_ArcadeGame.StartingElements`** — one row per hull
(and optionally per intensity), applied in `VesselController.Initialize` on every spawn path on
every machine, shipped to clients inside the config-sync RPC because element levels are
simulated on the owning machine and never replicate. See `VesselStartingElements` (Data) and
`GameDataSO.TryGetStartingElements`. The menu clears the table so no handicap follows a pilot
home.

The table is authored by `Tools/Build/regatta_balance.py`, an offline lap-time model over the
**measured** circuits, and the honest result is this (three laps, competent pilot on the best
line, intensity 1):

| hull | lever | at rest | tuned | with |
|---|---|---|---|---|
| Rhino | **none** | 12 s | 12 s | — |
| Manta | Time → Soar ×0.7..1.3 | 20 s | 24 s | Time −5 |
| Dolphin | Time → fill rate | 42 s | 42 s | rest (inert: a leg's first cycle dominates) |
| Scarab | Time → ceiling ×1..1.5 | 67 s | 45 s | Time 10 |
| Urchin | none (rail) | 48 s | 48 s | — |
| Squirrel | none (rail) | 49 s | 49 s | — |
| Serpent | Time → boost ×1.6, duration ×1.6 | 101 s | 58 s | Time 10 |
| Sparrow | Time → boost ×1.5 | 108 s | 74 s | Time 10 |

**Spread 8.7× at rest → 6.0× tuned (5.3× at intensity 4).** That residual is the finding, not
a rounding: an element spans ~1.5× on the hulls it reaches and nothing at all on the Rhino, while
the fleet's straight-line speeds span 34×. Tighter corners compress it a little (the fast hulls
pay them; a rider pays nothing), which is why the ladder tightens the floors. What would close
the rest is recorded in §8 rather than faked here. The comeback system is the second balancer
in play: six gates behind buys 2.1 levels of every element, and Time is speed on five hulls.

The generator asserts the tuned spread stays under **6.1×** so a vessel retune that breaks the
grid fails the build, and asserts the measured course JSON's source hash matches the shipped
C# so a course edit cannot ship on stale numbers.

## 4. The course

The circuit is `HeadlongCircuit`, shared, cut for **nobody in particular** (`RegattaCourse`):
Redline's corner profiles (the solver's proven reach at each rung), absolute corner floors set
where the fastest hull that cannot slow instantly — the Scarab at 216 u/s, ~102 u — still makes
the corner, and mouths a step wider than Headlong's because a Rhino crosses one at 1200 u/s.

| intensity | turn profile (deg) | floor | mouth | measured tightest corner | spine | prisms |
|---|---|---|---|---|---|---|
| 1 | 90 72 58 48 38 28 16 10 | 200 | 110 | 362 u | 4,828 u | 1,812 |
| 2 | 118 92 66 40 22 12 6 4 | 150 | 88 | 151 u | 5,432 u | 2,038 |
| 3 | 135 112 78 22 7 3 2 1 | 120 | 72 | 135 u | 5,887 u | 2,209 |
| 4 | 150 124 86 0 0 0 0 0 | 100 | 54 | 104 u | 5,868 u | 2,201 |

**The rails are the racing line, made of mass.** A closed Hermite spline is run through the
gates with each ring's axis as its tangent, so the spine crosses every ring plane at the centre
along its axis — which is what lets a rider thread a gate by riding. Three lanes sit 22 u off
the spine at 120°, carried on a rotation-minimising frame plus exactly **one twist per lap**, so
an inside lane on one corner is the outside lane on another and the three lanes come out within
**1.9%** of one length (measured over 240 seeds; asserted at 3%). One (6,6,8) prism every 8 u,
laid down each lane's own **closed** `Trail` (a loop has no ends to launch off — an Urchin that
stays on rides the whole race), `PrismscapeDimension.Trail`.

**The seed lives on the arena.** `SpawnableRegattaRails` (one prefab variant per intensity) lays
the rails from its own seed, and `RegattaController.BuildCourse` re-derives the SAME gates from
the same pure call off the cell's *expected* config — Skein's pattern — so rails and rings
cannot disagree, and the start line can be answered during the spawn chain. The cost, as in
Skein: a given intensity is the same circuit every match.

**Laps are derived, not authored twice.** The arena lays exactly `RingsPerLap` (8) rings, the
end-condition target is authored once (24), and `LapsPerRace = target / 8`. A target that is not
a multiple of eight races to the course's honest length and warns.

## 5. The start

Everyone spawns on a ring **behind gate 0, pointed through it** (`IPlayerSpawnLine`, 260 u
standoff, 120 u ring), not on the platform's equatorial ring 1120 u out facing the centre with
gate 0 on the pole above — a grid of hulls whose cruise speeds span 34× does not want a first
corner it did not ask for.

## 6. AI

The platform's gate-race AI (two-waypoint approach, latched side) plus one override: an
**attached** AI aims down its own rail (`TryOverrideAim`, Skein's), because the rail passes
through every ring and fighting its curve is the only way to lose it. The AI templates are
`Random`, so the bot grid draws its hulls from the card. The Manta's boost has an autopilot
drive and the Rhino's ramp engages off a straight stick an AI naturally holds; every other
hull's AI now spends its boost through a per-hull **AI boost policy**
(`AI/AI_BOOST.md`): the Sparrow holds its afterburner down each straight, the Serpent stacks
pellets on long legs, the Dolphin discharges its drift charge onto the straight instead of
into the turn, and a Time-5 Scarab
Snap Dashes (below Time 5 the Scarab is already at its throttle ceiling). None of them is
Regatta-specific and none has been measured in a race yet — the claim that an all-AI domain
in those hulls can now WIN is unverified until a playtest says so. The **Squirrel** is the
exception: in Regatta it does not fly `AIPilot` at all (and so never runs its `SkimRing` boost
policy) — it flies the Skim Race pilot, below.

**Two kinds of AI seat** (`AIHullSeating`, read by the lobby chips, the backfill and the arena
hull backstop alike):

- **Opponent seat** — an AI on a domain no human flies. The card pins
  `SO_ArcadeGame.OpponentAIVessel` to the **Squirrel**, so every opponent is a Squirrel: it skims
  the rails for boost energy, which is the one speed source a gate-following autopilot reaches
  just by flying the racing line. Every Regatta Squirrel AI flies the **Skim Race pilot**
  (`SkimRacePilot`, `Docs/SKIM_RACE_AI.md`) with `RegattaRingObjective`: the same racing AI, its
  crystal swapped for the next ring and its track for the domain's rail. On the platform
  `AIPilot`'s ring waypoints the Squirrels threaded no rings at all (first playtest, 2026-10-06).
  Pinned opponents sit **outside arena seating** — a grid of identical opponents is the point, and the arena backstop
  (`ServerPlayerVesselInitializer.ResolveArenaUniqueHull`) skips them so a human who picked the
  Squirrel keeps it. **This is a stopgap**: when the racing AI can drive every hull, clear
  `OpponentAIVessel` in `author_regatta_assets.py` (and drop its assert) and the opponent grid
  goes back to the card's mixed draw.
- **Ally seat** — an AI on a domain a human flies. On the launch panel its avatar chip on the
  team tile shows the hull it will fly; **tap one of your own team's ally chips to step its hull**
  through *auto → the card's hulls in order → auto*, skipping hulls another pilot has claimed or
  another ally already flies. The host owns the roster: a guest's tap is a
  `RequestAllyVesselCycle` ServerRpc, the host refuses a pick for another team's AI, and the picks
  replicate in `LobbySnapshot.AiV0..AiV5` beside the placements. At launch the picks go to
  `GameDataSO.RequestedAIVessels` (entry *i* = bot *i*); the spawner honours a pick that is on the
  card, built and still free, and otherwise draws from the card as before. *Auto* (no pick) is
  the old behaviour. Picks are per session — not remembered with the launch preference.

## 6.5 Scoring — the team sum

`RegattaScoringRuleSO` (a `GateRaceScoringRuleSO` subclass; the asset is points rules,
`golfRules: 0`, and `RegattaController.UseGolfRules` is false to match):

- **A team's score is the SUM of every pilot's gates** (`DomainValue` = `SumByDomain`), so the
  HUD's domain boxes, the comeback deficit, the placement order and the winner all read the team
  total. It used to be the lead runner's (`BestByDomain`), so only the fastest pilot counted.
- **The race ends when any pilot finishes the course** — three laps, as before. Ending on a team
  total would make the race length depend on team size; one clock for everyone keeps it a race.
- **The highest team total at that moment wins**; a tie goes to the team that put the finisher
  across.
- Each pilot's `Score` is their own gate count, so the end-game domain totals
  (`CalculateDomainStats` sums Score) are the team sums. The scoreboard lists the winning team
  first, each row reading the pilot's own gates and the team total.
- **Not normalised for team size**: a team with more pilots has more laps to sum, and the
  comeback deficit is measured in summed gates. The host shapes the teams with Add AI, which is
  where that balance is set.

## 7. Assets and where they are authored

Everything by `Tools/Build/author_regatta_assets.py` (`--check` passes; `--check` was watched to
fail on a mutated cell config). The card is registered in the **master roster and `ArenaGames`**
(`arcade_mode_lib.register_arena_card`), never `ArcadeGames`. The scene is a clone of
`MinigameRedline` with the controller, rule, field block, IntensityWise cell list and AI templates
swapped. Toasts: two idle hints (`RegattaRailHint` 110, `RegattaLaneHint` 111) and the comeback
line. Preview: four cells by intensity, `Vessel: -1` (the carousel's pick flies).

Measurement chain: `Tools/Build/regatta_course_harness/run.sh` compiles the three pure course
files against a UnityEngine math stub with Roslyn and RUNS them (the four shipped seeds plus the
60-seed sweep the editor tests make), writing `regatta_course_measurements.json`;
`regatta_balance.py` reads that plus every flight constant out of the prefabs and ability
assets; the generator reads both.

## 8. Verification status and known limitations

**Authored headless and never run in the editor.** The pure course code was compiled and run
for real (Roslyn, 244 circuits, every contract the editor tests assert held); the C# that
touches Unity — the controller, the spawnable, the platform's starting-element plumbing — is
inspected, not compiled. First editor run: open `MinigameRegatta`, confirm three coloured rails
bloom in through eight rings, an Urchin latches at 300 u/s on its colour, a Squirrel's boost
gauge fills on the rail, the HUD flowers show the seeded levels (fire petals on a Manta, white
on a Sparrow), and a guest's own hull carries the same levels as the host's replica.

- **First playtest (2026-09-15): the carousel offered two hulls and the Scarab wore the Sparrow's
  icon.** Neither was this mode's: the arena carousel filtered the card's `Vessels` by the
  Hangar's purchase lock, and the Scarab's `IconActive` was a codex bake that photographed the
  hidden Sparrow the Scarab wraps. Both fixed platform-wide — `Docs/HomeHub/ARCHITECTURE.md`
  §3.3/§3.4 — and held by `ArenaRosterTests`.
- **Second playtest (2026-09-15): the CONFIRM button sat on Play, and the Urchin was a white
  square.** `SelectVesselButton` was a clone of `Play Button`'s RectTransform (same parent, anchors,
  pivot and offset) that never got moved, hidden on confirm — reported as a strange button over Play
  that goes away when clicked; it now lives inside the carousel under the vessel icon at the CONFIRM
  plate's native 272×72, and `Tools/Build/author_arena_launch_panel_layout.py --check` proves it and
  Play disjoint. `SO_Class_Urchin.IconActive`/`IconInactive` pointed at sprite guids no `.meta` owns
  (a missing sprite draws a solid white quad); `author_urchin_card_icons.py` re-points them at
  `Urchin_Square.png` plus a derived `Urchin_Inactive.png`, and `check_vessel_class_icons.py` gates
  every class asset's icons. `Docs/HomeHub/ARCHITECTURE.md` §3.5.
- **Team play (2026-10-06): Squirrel opponents, the ally hull picker, the team-sum score — NOT
  EDITOR-VERIFIED.** Compiled headless against real Unity references
  (`Tools/Build/unity_refcompile`, player and editor configs: 0 project errors, none of the changed
  files in the unobtainable-package bucket beyond `GameDataSO`'s pre-existing
  `Unity.Services.Multiplayer` lines). `AIHullSeating` was also run for real under .NET 8 (every
  seating/cycle case passed). The NUnit tests in `RegattaTeamPlayTests` and
  `ArcadeLobbySnapshotTests` compile but were not run (no editor). First editor run: open the
  Regatta card solo with one AI placed on Ruby and one on Jade — the Ruby chip shows the
  Squirrel and does not react to a tap; tapping the Jade chip steps it auto → Manta → Dolphin …
  → auto, skipping your own confirmed hull. Launch: every Ruby bot is a Squirrel, the Jade bot
  flies the hull you picked. As a guest, tap your own team's ally chip and confirm the host's
  chip follows; tap another team's and confirm nothing changes. In the race, the domain boxes
  climb by every pilot's gates, the race ends when the first pilot finishes lap three, and the
  scoreboard puts the higher team total first.
- **The residual 5–6× spread is real.** The lever the user named — starting elements — reaches
  five hulls by ~1.5×. **This used to read "and the Rhino not at all", which was half wrong and
  is now wholly stale.** `RampBoostActionExecutor` has always read `Multiplier(Element.Time)` and
  scaled `accelerationPerSecond` by it; the Rhino's map entry was simply an `(open design slot)`
  authored 1.0/1.0, and Broadside's first playtest filled it (**Ramp Spool**, 2.5 / 0.5 — see
  `BROADSIDE.md` § "What the first playtest changed"). So Time now reaches the Rhino's **wind-up
  rate**, fleet-wide. What it still does **not** reach is the ramp's **ceiling**
  (`maxBoostMultiplier` 24), which over a lap is most of what a race is bounded by — so this
  model's numbers stand and its Rhino row is **unchanged and now stale in one respect**:
  `rhino_model` returns its acceleration as a constant and does not scale it by level. The honest
  next steps, in order of how much they respect the fundamentals: (1) teach `rhino_model` the new
  endpoint and re-solve, then decide whether a Rhino Time row buys a Regatta lap anything (this
  file claims nothing until it is measured); (2) make `maxBoostMultiplier` an `ElementalFloat`
  too, if (1) is not enough — that is the ceiling, and it is a `/vessel` change; (3) a **pursuit
  start** — the regatta's own answer to mixed boats, a per-hull start release derived from this
  model, first through the last ring wins outright; (4) a playtest before any of them, because
  this model is arithmetic.
- The Dolphin's Time lever is inert here (the first charge cycle of every leg dominates), and
  its 347 peak is `BoostMultiplier × ChargedBoostCharge` — a squaring the vessel pass flagged
  as a possible defect. The model follows the code.
- Gate 0 sits at ~1,056–1,070 u from the centre at every rung, inside the 1,080 shell; the start
  line's 120 u ring reaches 1,190 — inside the 1,200 membrane by ten units. If the membrane
  ever shrinks, the standoff/ring must move.
- Squirrel skim energy on a super-shielded rail is unmodelled beyond "saturates": the shell
  tier dispatches skim contacts at the octahedron surface, which is 1.5× the prism — so the
  skimmer sphere reaches it earlier than a plain prism, in the rider's favour.
