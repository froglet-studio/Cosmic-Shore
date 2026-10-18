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

A Rhino ramps to 840 u/s on a straight and pays ~3.6 s of wind-up (at rest) for every corner it
cannot hold. *(This line said 1200 u/s and five seconds before the 2026-09-25 ramp retune.)* A Manta trades Soar for yaw one trigger at a time. A Scarab's ceiling is its Time
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
| **Rhino** | flies beside it; an **energised** sword pops a rail prism | ramp (straightness) 840 u/s (1200 before the 2026-09-25 retune) | none, **by design** — the hull ploughs prisms aside (`RhinoPloughPrismEffect`) and ramming costs no speed ("the bulldozer ignores mass", recorded in the Time row's description since 2026-10-16) |
| **Dolphin** | skims for seed energy, not speed | charge (drift) → discharge 347 u/s | ram = slow + half the charge |
| **Serpent** | flies beside it | 4 charges × 3 s at 160 u/s, regen 3.6 s/charge | none wired |
| **Sparrow** | flies beside it | indefinite boost 135 u/s | ram = slow |
| **Scarab** | flies beside it | 216 u/s ceiling, ×1.5 at Time 10 | none wired |
| **Stoat** (on the card 2026-10-17) | flies beside it; its pair's gravity **never moves a rail** (super-shielded prisms are never admitted to `BlackHoleGravityField`) | the pathfinder's WARP: the flight clock ×2.5 while its pair bends the path (Time → ×5); turns scale with it, so its 72 u turning circle holds at any warp | none wired; a vessel crossing its attractor's horizon is carried out of the repulsor |

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
line, intensity 1; re-run 2026-10-17):

| hull | lever | at rest | tuned | with |
|---|---|---|---|---|
| Rhino | **none** | 17.8 s | 17.8 s | — |
| Manta | Time → Soar ×0.7..1.3 | 20.3 s | 23.9 s | Time −5 |
| Dolphin | Time → fill rate | 41.9 s | 41.9 s | rest (inert: a leg's first cycle dominates) |
| Scarab | Time → ceiling ×1..1.5 | 67.3 s | 44.9 s | Time 10 |
| Urchin | none (rail) | 48.3 s | 48.3 s | — |
| Squirrel | none (rail) | 48.5 s | 48.5 s | — |
| Serpent | Time → boost ×1.6, duration ×1.6 | 100.9 s | 58.1 s | Time 10 |
| Sparrow | Time → boost ×1.5 | 107.5 s | 73.6 s | Time 10 |
| Stoat | Time → warp ×2.5..5 | 96.8 s | 48.5 s | Time 10 |

**Spread 6.03× at rest → 4.13× tuned at intensity 1** (I2 3.80×, I3 3.50×, I4 3.46×; `python3
Tools/Build/regatta_balance.py`). That residual is the finding, not
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
the corner, and mouths a step wider than Headlong's because a Rhino crosses one at 1200 u/s (sized before the
2026-09-25 retune; the Rhino now tops out at 840, so the mouths are generous for it, not tight).

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

**The Stoat (on the card 2026-10-17, Yash).** It flies the same gate steering; its own autopilot is three executors,
unchanged: `StoatModeExecutor` switches to Time mode when the ring is ≥ 70° off the nose (back within half of it),
`StoatDipoleExecutor` flies Mass mode's field dipole (path-watching, one-lap cap), and `StoatSlingExecutor` slings in
Time mode (ring ≥ 30° off, ≥ 120 u away, every 3 s). In Regatta each reads its AUTOPILOT numbers through
`AIModeTuning` (`StoatModeAction_Regatta`, `StoatDipoleConfig_Regatta`, `StoatSlingConfig_Regatta`), which
`Tools/Build/learn_drives_from_flights.py` writes from people's editor flights; none exists yet, so it flies its shipped
numbers. A person flying the Stoat reads the hull's own configs. Opponent AI stay pinned to the Squirrel
(`OpponentAIVessel`); a Stoat AI is an ally pick (the launch panel's ally chip) or your own hull on the Third Eye's AI.

The platform's gate-race AI (two-waypoint approach, latched side) plus one override for the one
hull that ATTACHES: an AI **Urchin** flies its kit through the shared `UrchinAutopilotDriver`
(#975, the driver Skein and Hijack use), called from `RegattaController.TryOverrideAim` exactly
the way `SkeinController` calls it — Regatta's own course logic (the same `_course` ring through
the same lap-wrapping `RingIndexFor`) supplies the objective:

- **Attached, on its own rail:** every lane threads every ring (a ring's capture radius, 0.8 of a
  54–110 u mouth, always contains a lane 22 u off the spine), so the driver RIDES and aims down the
  rail's tangent — hold the throttle, let the cable drive.
- **Attached, the wrong way round the loop:** the ring threads BEHIND, so it REVERSES (the ride's
  direction is the pilot's facing) instead of riding most of a lap backwards.
- **Attached to a RIVAL's rail:** the braid puts the three lanes 22 u apart, so an AI latches
  whichever it touches. A rival's lane crawls at 20 u/s, and — unlike Skein's plain cable —
  Regatta's rails are **super-shielded**, so no spike can convert them (`PrismTeamManager.Steal`
  refuses a super-shielded prism). The driver therefore treats the crawl as always "dry"
  (`UrchinRailAssessment.IsDryCrawl`, convertible = `UrchinAutopilotDriver.IsConvertible`) and
  **Slips off** unless the ring is within `DryCrawlArc` (80 u), and it **never spikes** the rail —
  a volley there would only spend the meter. With the **Time-5 Slipstream** a rival's lane rides at
  full pace (`TrailFollower.IsCrawlTerrain` is false), is not a crawl, and is ridden.
- **A stall** (a reversal caught in the throttle deadband) is Slipped after `ParkedSeconds`.
- **The lanes are CLOSED loops**, so the rail walk wraps past the seam exactly as the ride does
  (`UrchinRailAssessment.Scan(..., loop: Trail.IsLoop)`). Skein's and Hijack's rails are open and
  walk exactly as before; without the wrap a pilot just before a Regatta lane's seam would have read
  the ring beyond it as unreachable and slipped off its own lane.
- **Free:** the platform's gate aiming flies it at the ring (no crystal detour in Regatta, below), and on a
  long, straight, lined-up shot it lays a **Track Projector** rail at the ring (`TryProjectTrackToward`).

Before this, an attached AI aimed down whatever rail it had latched — right on its own lane, but a
pilot on a rival's lane crawled at 20 u/s for the rest of the race. **Unmeasured:** after a Slip the
pilot flies back at the ring through the braid and may re-latch a rival's lane (1.5 s
`MinSlipIntervalSeconds` bounds the loop); how often an AI Urchin ends up on its own lane is a
playtest number. Every other hull is untouched — none attaches, and the driver finds every
control by capability. `AIPilot` is unchanged. The AI templates are
`Random`, so the bot grid draws its hulls from the card. The Manta's boost has an autopilot
drive; the Rhino's ramp did NOT engage for an AI (2026-10-14 audit: the press only comes from a human input
strategy) and is now driven by `RampBoostAIPolicySO`; every other
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
- **The host picks every seat** (2026-10-14, for testing a mixed grid): the host may tap ANY AI chip,
  an opponent's included, and step it through *auto → the card's hulls → auto*. On an opponent seat
  *auto* is the pinned Squirrel; a pick replaces the pin for that seat only and then follows arena
  seating like any pick (one pilot per hull). A guest still picks only their own allies.
  `AIHullSeating.MayPick` / `SeatHull`, read by the chips, the host's check and the spawner.

### 6.1 The five-AI audit (2026-10-14) — what was fixed, what is still open

Five read-only audits traced each racing AI's Regatta path (Squirrel, Manta, Rhino, Urchin, Dolphin). NOT
EDITOR-VERIFIED; compiled headless only.

| Hull | Found | Done |
|---|---|---|
| Rhino | **The ramp never engaged for an AI** (only a human input raises its press): it cruised at ~19–50 u/s and could not finish 3 laps | `RampBoostAIPolicySO` on `Rhino.prefab` (`AI/AI_BOOST.md`) |
| All | AIs raced at their skill throttle (0.3–0.9) | Regatta asks full throttle (`AIThrottleFloor`, `AIPilot.SetThrottleFloor`, carried by a pilot swap) |
| Manta (fast hulls) | The approach point sat up to 480 u before a ring on 461–858 u legs, i.e. behind the ring just threaded, inside a fast hull's turn: break-offs every ring | The approach lead is capped at 40% of the leg into the ring (`GateRaceController`, every gate race) |
| Dolphin | In a drift the nose pointed at the cell centre, so the discharge fired wide of the ring; on a gamepad host the AI's drift had depth 0 and never locked the course | Gate races aim the drift nose at the AI's own target (`SetDriftLookTargetProvider`); the charge driver spends only with the nose on the objective; the AI holds the drift trigger while it holds its commit drift |
| Urchin | Free, it aimed at the ring's spine, so the first lane it touched was its own only one time in three; a rival lane is a 20 u/s crawl or a slip / re-latch loop. Riding, the orbit detector fired and dropped it to 0.6 throttle | A free AI Urchin heads for its own-domain lane just ahead (`RegattaController.TryOwnLaneAhead`); no orbit break while attached |
| Squirrel | A pilot swap handed the AI a Squirrel on the old ring-blind autopilot; Regatta flew Skim Race's per-intensity tuning files | `PilotSwap` installs the Skim Race pilot; outside Skim Race it loads Regatta's own copy, `SkimRaceAIConfig_Regatta` (`SkimRaceAIDeployment`, `SkimRaceAIConfigSO.LoadForRegatta`; generated by `author_skimrace_ai_config.py`, ring face learned from flights 2026-10-16) |
| All | Difficulty did nothing in Regatta | Regatta offers Easy / Medium / Hard (`AIDifficultyRules`) |
| All (2026-10-15, from the Regatta AI studio) | The AI committed to the far-side point 420 u out whether lined up or not, so on a short leg it crossed the plane beside the mouth; the side was latched once per ring, so after a miss it circled the far-side point until a break-off fired. The studio finished 17/32 Hard hull x intensity races this way | It commits no earlier than its approach point (`GateRaceController.CommitDistanceFor`: the smaller of `aiCommitDistance` and the approach lead plus half the turning radius) and, after crossing the plane outside the mouth (`RaceGateRing.PlaneCrossing`), re-latches the side and breaks off at once (`AIPilot.BreakOffNow`). The studio's lab fix, ported line for line: 30/32. On in Regatta only (`AICommitFromApproachPoint`); the other gate races keep their shipped steering until their owners test it |

**Still open (not fixed, by priority):** the Manta's Soar cuts only after a corner has started (the look-ahead now
exists, `AIBoostContext.CornerDegrees`; dropping Soar before a corner was modelled 2026-10-17 and was slower on every
course, so the Manta instead holds Soar deeper into turns in Regatta, below); an
Urchin riding a tight inside-lane corner at I3–I4 can flip its ride direction; Skim Race lane heights stack across
every AI seat instead of per domain (Squirrels lift off their rail); the Rhino's overshoot at full ramp is
addressed but unmeasured (2026-10-17: it brakes ON the ramp at 300 u/s² to a corner speed, `AI/AI_BOOST.md`). The bleeding-edge commit `fb2bff7ac` (Skim Race ring
crystals) merges cleanly and does not change Regatta.

**2026-10-17 (Ys-bleeding-edge only, not editor-verified).** Regatta's gate steering now puts the "through" point on
the line from the pilot through the ring's CENTRE, not on the ring's axis (`GateRaceController.ArmRacer`, gated by
`AICommitFromApproachPoint`, so Regatta only). A drifting hull locks its course on that point, and an axis point lay off
that line when the pilot committed off-axis, so the locked line passed beside the mouth. In the offline models the axis
point stalled the Dolphin at I2-I4 and the old Rhino drive at I3-I4; on the centre line every race finished. It
affects every Regatta AI. The same day the Dolphin's drive learned to drift again at its discharge's peak (`9b064721b`),
and the Manta got Regatta Soar bands (`f4e35748c`, below).

**Per-hull Regatta variants.** The Rhino, Dolphin and Manta drives and the Stoat's three configs each read a
Regatta-only copy when one exists (`Resources/AIBoostPolicies/<asset>_Regatta`, written by
`learn_drives_from_flights.py` from people's editor flights). Only the Manta's exists: `MantaSoarAITuning_Regatta`,
Soar bands 0.8 / 0.85 against the executor's 0.35 / 0.75 (2026-10-17, set from `Tools/Build/manta_drive_sim.py`, which
raced three laps 1.5-2 s faster at every intensity; Manta flights replace it). So the Squirrel
(`SkimRaceAIConfig_Regatta`) and the Manta fly Regatta numbers; the Rhino, the Dolphin and the Stoat fly their shipped
numbers.

**Watching them:** FrogletTools ▸ AI ▸ Third Eye. Every pilot now leaves a **trail** of its last seconds of flight
(toolbar *Trails*, length in Settings), and each AI's readout names its phase (Approach / Through ring N, Crystal on
the way, Not noticed yet, Misjudged, Riding the rail, Heading for its own rail, Finished), its lap, the ring it is
after (the cyan ring marker), and its boost decision (the Rhino's ramp, the Dolphin's charge).

**No AI targets a crystal in Regatta (2026-10-17, Yash):** *"in Regatta no AI should target an omni crystal; only
the waypoints of the track."*
- **The rule:** every Regatta AI steers for the course's rings, and for its rail while it rides one.
- **How:** `RegattaController.AICrystalDetourSlack` is 0 in code. Every other gate race still lets an AI swing up to
  `aiCrystalDetourSlack` (220 u in the scenes) for a crystal on its way, and `MinigameRegatta.unity` serializes 220
  too. The code value wins, so no scene edit can turn the detour back on.
- **Why:** in Yash's editor flights, rival AIs came within 20 u of crystals whenever the recording could see one (the
  Manta, Dolphin, Sparrow and Serpent beside omni crystals; the Rhino beside others).
- **Test:** `RegattaNoCrystalTargetTests`, with Headlong as the negative control.
- **The Squirrel** already aimed only at rings (`RegattaRingObjective`). The Third Eye's "Crystal on the way" phase
  no longer appears in Regatta.

### 6.2 Every hull races its own line, along the sides (2026-10-17)

Yash, 2026-10-17: "every vessel moves in different ways ... bigger vessels like the Rhino are trying to get to the
center of the track like the Squirrel does, but that's not how it should fly ... it's always better to play from the
sides." The centre of the track IS the middle of the braid: the spine runs through every ring's centre and the three
super-shielded rails are braided 22 u round it, so a hull aiming at ring centres flies 22 u from all three rails at
once, and a big hull scrapes them (a ram slows the Manta, Dolphin and Sparrow and resets the Squirrel's boost, §2).

- **The line** (`RegattaRaceLine`, pure): the bisector of the GAP between the two rails that are not the pilot's
  domain's, at a radius from the spine. At radius r its nearest rail is sqrt(r² + 22² - 22 r) away, so it never crosses
  a rail, and it twists with the braid, so it threads every ring inside the mouth. Built once from the live rails.
  Measured on the recorded rails (2026-10-17): at I1 a line at r = 60 stays **51 u** from every rail (the centre: 22),
  crosses each ring at 55% of the mouth and is **3%** longer than the spine; at I3, r = 41: 35 u, 57%, the same length.
- **Each hull's style** (`RaceLineStyleSO`, `Resources/RaceLines/<Hull>_Regatta`, authored by
  `Tools/Build/author_race_lines.py`): how far out it flies (`LaneShare`), the room its hull keeps from a rail or the
  rim (`HullClearance`), and its **risk**. Bold cuts more of each bend (a longer look-ahead) and keeps 0.75x the room;
  careful follows the line tightly with 1.5x. Every AI seat draws its own risk round the hull's (seeded per seat), so a
  grid has careful and bold pilots of the same hull. Starting values: Rhino 0.80 / 18 u / 0.40 (biggest, fastest:
  out wide, safer), Manta 0.70 / 16 / 0.50, Dolphin 0.60 / 12 / 0.50, Stoat 0.55 / 10 / 0.60, Serpent 0.60 / 12 /
  0.50, Sparrow 0.55 / 10 / 0.50, Scarab 0.60 / 12 / 0.45.
- **Learned from people**: `learn_drives_from_flights.py` measures where a person crosses the rings in their fastest
  three finished races of a hull and writes that hull's `LaneShare` (`race_lines_learned.json`, read by the
  generator). No Rhino, Manta, Dolphin, Stoat, Sparrow or Scarab flights yet; the Serpent has one finished race.
- **Steering** (`GateRaceController.TryRaceLine`, overridden here): asked after the difficulty belief and the
  missed-mouth test, so an Easy pilot's mistakes and a missed ring are still flown the gate way (a missed ring is flown
  the gate way until threaded, or the closed line would carry the pilot a whole lap round to it). The point a second
  look-ahead further on is the boost drivers' corner (`AIBoostContext.CornerDegrees`).
- **Not on a line:** the Squirrel (its own Skim Race pilot; Yash's own Squirrel crosses rings 11 u from the centre,
  median of 1,042 crossings: the braid's middle IS the Squirrel's line) and the Urchin (rides its own lane).
- **Tests:** `RegattaRaceLineTests` (clearance, radius, the centre line as the negative control, the loop, the
  style's bounds and seeded risk, which hulls have a style). Not editor-verified.

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
swapped. Preview: four cells by intensity, `Vessel: -1` (the carousel's pick flies).

**Toasts** (`GameToastConfig_Regatta.asset`, authored by the generator): two idle hints
(`RegattaRailHint` 110, `RegattaLaneHint` 111), the comeback line, and — since #985 — the four
shared **race beats** that `GateRaceController`'s `DomainRaceToasts` posts for every gate race,
in the gate-race family's voice (`{0}` leading domain, `{1}` its score, `{2}` the target):

| situation | id | template |
|---|---|---|
| `DomainRaceHalf` | 129 | `{0} is halfway home - {1}/{2} gates` |
| `DomainRaceLeadChanged` | 130 | `{0} takes the lead - {1}/{2} gates` |
| `DomainRaceHomeStretch` | 131 | `{0} is on the home stretch - {1}/{2} gates` |
| `DomainRaceFinalLap` | 132 | `{0} is on the final lap` |

No Quarter (128); no gate race authors it. **Regatta's score is a TEAM SUM**, so the beats are
measured against the leading team's combined courses, not one pilot's: `RegattaController`
sets `RaceToastsSumTeams`, and `DomainRaceToasts` then scales the target, the 3-gate home
stretch and the final-lap mark by that team's pilot count (`DomainRaceToasts.Evaluate`,
`CountPilots` over the replicated RoundStats). A two-pilot team is "halfway home" at 24 of 48
gates and on the final lap at 32 — `{2}` reads 48. Before this, the beats compared the summed
score with one pilot's 24-gate course, so a pair each a quarter of the way round was announced
halfway, and with two or more pilots per team halfway, home stretch and final lap all fired
early. Every other gate race passes no counter and its arithmetic is unchanged
(`DomainRaceToastsTests.OnePilot_MatchesTheUnscaledThresholds`).

Measurement chain: `Tools/Build/regatta_course_harness/run.sh` compiles the three pure course
files against a UnityEngine math stub with Roslyn and RUNS them (the four shipped seeds plus the
60-seed sweep the editor tests make), writing `regatta_course_measurements.json`;
`regatta_balance.py` reads that plus every flight constant out of the prefabs and ability
assets; the generator reads both.

## 8. Verification status and known limitations

- **The five-hull test race (2026-10-14) — NOT EDITOR-VERIFIED.** Solo, Arcade ▸ Regatta, 6 seats
  (you + 5 AI) over 3 domains. Tap each AI chip (as host you can tap every team's) to put **Squirrel,
  Manta, Rhino, Urchin, Dolphin** (and since 2026-10-17 the **Stoat**) on the grid, and fly a sixth hull yourself (or one of the five, leaving
  that AI on *auto*). Intensity 1, Hard. Record per hull: finished 3 laps or not, place, rings missed,
  where it got stuck. The model's skilled-pilot I1 times for these five are Rhino 17.8 s, Manta 23.9 s,
  Dolphin 41.9 s, Urchin 48.3 s, Squirrel 48.5 s (`regatta_balance.py`): a 2.7× spread before any AI
  error. The result decides the next step: AI fixes for a hull that does not finish, then
  `StartingElements` or a pursuit start for the spread.

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
- **AI Urchin rail choice (2026-10, #975 follow-up) — NOT EDITOR-VERIFIED.** §6. The pure half
  (`UrchinRailAssessment`) was compiled with Roslyn against a math stub and the whole
  `UrchinRailAssessmentTests` file RUN through a minimal NUnit shim: 20/20 pass, the 13
  pre-existing tests included (the loop-aware `Scan` rewrite changed no open-rail answer). Two
  negative controls discriminated: restoring the old "hostile and no ammo" dry-crawl rule fails
  three cases, and disabling the loop wrap fails both closed-rail tests. `RegattaController`, the
  driver, `TrailFollower.IsCrawlTerrain` and `Trail.IsLoop` compile against real Unity references
  (`Tools/Build/unity_refcompile`, player and editor configs: 0 project errors; a planted missing
  member in a changed file failed the player run as expected). First editor run: solo Regatta, Jade, with one ally AI stepped
  to the **Urchin** on the launch panel. Watch it from the spectator/pilot-swap view: on its own
  (Jade) lane it rides through ring after ring at ~300 u/s; when it latches a Ruby or Gold lane it
  lets go within ~0.25–1.5 s (a short ghost flicker = Slip) rather than crawling at 20 u/s, and no
  spikes fly at the rail; if it latches its own lane facing backwards it swings round within a
  second. Then give that ally a Time-5 start (or collect to Time 5) and confirm it now RIDES a
  rival's lane at full pace instead of slipping.
- **The residual 5–6× spread is real.** The lever the user named — starting elements — reaches
  five hulls by ~1.5×. **This used to read "and the Rhino not at all", which was half wrong and
  is now wholly stale.** `RampBoostActionExecutor` has always read `Multiplier(Element.Time)` and
  scaled `accelerationPerSecond` by it; the Rhino's map entry was simply an `(open design slot)`
  authored 1.0/1.0, and Broadside's first playtest filled it (**Ramp Spool**, 2.5 / 0.5 — see
  `BROADSIDE.md` § "What the first playtest changed"). So Time now reaches the Rhino's **wind-up
  rate**, fleet-wide. What it still does **not** reach is the ramp's **ceiling**
  (`maxBoostMultiplier` 24 when this was written; **16.8** since the 2026-09-25 retune), which over a lap is most of what a race is bounded by — so this
  model's numbers stand and its Rhino row is **unchanged and now stale in one respect**:
  `rhino_model` returns its acceleration as a constant and does not scale it by level. The honest
  next steps, in order of how much they respect the fundamentals: (1) teach `rhino_model` the new
  endpoint and re-solve, then decide whether a Rhino Time row buys a Regatta lap anything (this
  file claims nothing until it is measured); (2) make `maxBoostMultiplier` an `ElementalFloat`
  too, if (1) is not enough — that is the ceiling, and it is a `/vessel` change; (3) a **pursuit
  start** — the regatta's own answer to mixed boats, a per-hull start release derived from this
  model, first through the last ring wins outright; (4) a playtest before any of them, because
  this model is arithmetic.
- **2026-10-16 Rhino kit — needs a playtest.** (1) **AI Rhinos now cut the rails.**
  `RhinoSwordAutopilot` holds the energize stance near super-shielded mass, and every rail prism is
  super-shielded, so an AI Rhino flying beside a rail will now energize and pop holes in it — before
  today no AI could. A rider bridges a hole at full pace, but whether a bot grid shreds the braid is
  a playtest question. (2) **The AI now ramps** (`RampBoostAIPolicySO`), so the AI Rhino's lap time
  is no longer a cruise lap (the model's 12 s always assumed a ramping pilot); re-check the all-AI-domain claim in §6.
  (3) **Momentum** (Time L5, danger immunity above 588 u/s) — the rails are super-shielded, not
  danger, so it matters here only against danger prisms a Rhino lays itself (Mass-L5 Danger
  Afterimages) or the comeback/Surge levels push a rival to. (4) **Afterimages** put up to 5 s of
  blade-shaped prisms on the racing line after every energize, which the lap-time model does not
  carry. `regatta_balance.py` reads `maxBoostMultiplier` from the asset, so re-run it before quoting
  the Rhino row again.
- The Dolphin's Time lever is inert here (the first charge cycle of every leg dominates), and
  its 347 peak is `BoostMultiplier × ChargedBoostCharge` — a squaring the vessel pass flagged
  as a possible defect. The model follows the code.
- Gate 0 sits at ~1,056–1,070 u from the centre at every rung, inside the 1,080 shell; the start
  line's 120 u ring reaches 1,190 — inside the 1,200 membrane by ten units. If the membrane
  ever shrinks, the standoff/ring must move.
- Squirrel skim energy on a super-shielded rail is unmodelled beyond "saturates": the shell
  tier dispatches skim contacts at the octahedron surface, which is 1.5× the prism — so the
  skimmer sphere reaches it earlier than a plain prism, in the rider's favour.
