# Vessel Studio rollout — one studio per vessel whose AI is built

Measured on `Ys-bleeding-edge` `32e9cd339` (bleeding-edge fully merged, 2026-10-09). How a studio is
built: the `/vessel-studio` skill (D1–D22). Which AIs exist: `Docs/AI_SYSTEM/ARCHITECTURE.md` §2. This
file is the order the remaining studios are built in and what each one owes. **The user picks the next
vessel**; the order below is the recommendation.

## 1. What every vessel's AI is today

"Own AI" = decision code beyond the platform autopilot (`AIPilot`, which every hull gets: stick +
throttle 0.6 + target choice). A hull with only the autopilot races at cruise wherever its speed is an
ability (`/arenagame` §1).

| Vessel | Own AI (layer, code) | Home modes (single-hull cards) | Shared cards | Studio |
|---|---|---|---|---|
| Squirrel | 5: `SkimRacePilot` (Skim Race, Regatta); 1: `SkimRingAIPolicySO` elsewhere | Skim Race, Joust | Astro League, Broadside, Brood Rush, Maelstrom, Regatta, Scurry | **Built** (round 2) |
| Stoat | 4: autopilot sling / dipole (`StoatSlingExecutor.AutopilotSling`, `StoatDipoleExecutor`, `AutopilotHold01`) | Slingshot, Warpline | — | **Built** (round 15, design) |
| Grizzly | 4: bomb-jump drive in `GrizzlyTriggerBombExecutor` (`GrizzlyTriggerBombConfig.asset` `aiFireStickBand 0.35`, `aiFreezeDistance 18`, `aiDetonateBehindDistance 10`, `aiFireIntervalSeconds 0.6`) | **Grizzly Time** (gate race), Grizzly Charge | — | — |
| Manta | 4: Soar turn-boost drive in `MantaAnalogTurnBoostExecutor` | **Redline** (gate race), Bloomrush | Broadside, Brood Rush, Maelstrom, Regatta, Scurry | — |
| Urchin | 3: `UrchinAutopilotDriver` + `UrchinRailAssessment` (`Resources/UrchinAutopilotConfig.asset`): ride / reverse / leave per rail, Slip, Chain Spikes on convertible mass, Track Projector | **Skein** (gate race), Hijack | Broadside, Regatta | — |
| Scarab | 3: `ScarabScrambleJukePlanner` (committed dashes via `ScarabJukeController.TryAutopilotDash`); 1: `SnapDashAIPolicySO` | Scarab Scramble (ball), Tollway, Undertow, Wrecking Ball | Astro League, Broadside, Regatta | — |
| Butterfly | 3: `ButterflyAutopilotModeDriver` (Mass ↔ Dust switch) | **Waystation** (gate race), Dustup, Sirocco, Tapestry | — | — |
| Dolphin | 1: `ChargeBoostAIPolicySO` (discharge on straights); 4: `EchoSightActionExecutor` holds while drifting | **Switchback** (gate race), Bends, Rampage | Broadside, Brood Rush, Regatta | — |
| Sparrow | 1: `HoldBoostAIPolicySO` | **Breakwater** (gate race), Dog Fight, Salvo, Wildlife Liberation | Broadside, Brood Rush, Maelstrom, Regatta, Scurry | — |
| Rhino | none (autopilot ramps by the straight-stick gesture) | **Headlong** (gate race), Cleave | Astro League, Broadside, Brood Rush, Regatta | — |
| Serpent | 1: `PelletBoostAIPolicySO` | none | Brood Rush, Regatta | — |
| Termite, Falcon, Shrike | none (Falcon / Shrike classes are `_SO_Assets/_TEMP`) | none | none | Not a candidate |

Every other `AutoPilotEnabled` read in the vessel code (Rhino sword FX, shield swipe, gun spray,
zoom-out, drift trail, Pip) only skips human-only feedback while the autopilot flies; none of them is
a decision.

## 2. The shared piece to build once: a gate-race studio core

Ten modes are `GateRaceController` subclasses, and **seven of them are one hull's home race**: Grizzly
Time, Redline, Skein, Waystation, Switchback, Breakwater, Headlong (plus Slingshot / Warpline, which the
Stoat studio already flies, and the arena Regatta). They share:

- the course: a closed loop of ordered rings, laps, golf on time, a 4-intensity ladder, each written by
  its own `Tools/Build/author_<mode>_assets.py` generator (D7: emit the course from the generator,
  never retype it);
- the AI's race steering: `GateRaceController` hands every AI seat one external target (commit / lead /
  through distances per mode, e.g. Grizzly Time 380 / 420 / 285);
- the end condition: `AuthoredGateTarget()` (laps × rings), authored in the End Game Conditions window.

So the first new studio also builds **`VesselStudio/gate_race.js`**: the course loader (rings, laps,
intensity), the ring marker, the gate-threading check, the lap clock, and the `GateRaceController`
commit / lead / through steering. Each later race studio is then hull flight + its kit + its own AI
drive on top. It sits beside `ai_race_panel.js`, `studio-ide.js` and `studio-look.js` under `shared`
in `studios.json`. Back-port the Stoat studio onto it at its next round (the Stoat's ring course is the
same shape, built by hand).

## 3. Recommended order

| # | Vessel | Studio map | Why this place | What its scorecard asks (D5) |
|---|---|---|---|---|
| 1 | **Grizzly** | Grizzly Time I1–I4 | The closest to the two built studios: a gate race with laps and golf, one AI drive whose four numbers live in one asset, and that AI has never been seen working in the editor (`GRIZZLYTIME.md` §4, roster "Intake"). It builds the gate-race core | Lap time per intensity (the AI's skill follows it); bomb-jumps per lap; launches that land the hull through the next ring vs off-line; self-blasts that cost time |
| 2 | **Manta** | Redline I1–I4 | The Grizzly's bomb-jump drive was copied from Redline's Soar drive, so the same AI shape on a faster hull; second user of the core | Lap time; Soar uptime on straights vs turns; yaw lost to Soar |
| 3 | **Urchin** | Skein I1–I4, then Hijack | The most developed bespoke AI after Skim Race, with tests, but its studio must model rails (own-colour 300 u/s, rival 20, ride direction from facing, aimed rail ends), the largest port after the core | Rings threaded on the right rail; rail choice vs `UrchinRailAssessment`; Slips; spike / projector use |
| 4 | **Rhino** | Headlong | No own AI: the studio answers whether the autopilot alone can drive the ramp gesture to top speed (1200), which decides if Rhino needs a drive like the Manta's | Top speed reached per straight; ramp time; rings missed |
| 5 | **Dolphin** | Switchback | Charge-boost policy on a race: discharge timing on straights | Lap time; discharges per lap; charge wasted |
| 6 | **Sparrow** | Breakwater | Hold-boost policy; simplest kit, quick once the core exists | Lap time; boost uptime |
| 7 | **Butterfly** | Waystation, then Dustup | The Mass ↔ Dust mode driver; the race first, the dust duel second | Lap time; mode switches; crystals caught (the new always-on catcher) |
| 8 | **Scarab** | Scarab Scramble | Not a race: needs ball physics, goals and rings, a new studio shape. The juke planner is pure and tested, which helps | Goals per minute per difficulty; jukes that clear a defender; dash timing |
| 9 | **Serpent** | Regatta (arena) | No home mode. Wait for the Regatta arena studio (every hull on one circuit), which this list builds toward | Lap time vs the fleet |

Not planned: Termite, Falcon, Shrike (no card, no own AI). Revisit if a card arrives.

**After the vessels: a Regatta arena studio.** Every hull above on the one shared circuit, using each
studio's hull model. It is the fleet-wide balance question `regatta_balance.py` answers on paper today.

## 4. Per-studio checklist (the skill's §2, made concrete)

For each vessel, one branch off `Ys-bleeding-edge`, one studio round per commit:

1. **Intake**: the roster row (`ARCHITECTURE.md` §2), the vessel's `R_VesselActions/<HULL>.md`, the mode
   doc, `python3 Tools/Build/element_ability_table.py <Vessel>`.
2. **Ships or design** (D2): every row above is "ships": the studio models the game's AI as it is, so
   a studio finding is a defect in the game, filed against the AI's status-board row.
3. **Course**: extend the mode's generator with an `--emit-course` (as `skimrace_track_fingerprint.py
   --emit-track`), embed I1–I4 verbatim.
4. **Hull**: the `SHIP` constants from the prefab and action SOs, each naming its asset (D1).
5. **AI**: port the game's own drive input-only (D4): the `GateRaceController` target + the hull's own
   drive, with its config asset's numbers. **The game offers AI difficulty only in Skim Race**
   (`AIDifficultyRules.IsOfferedFor`); in every other mode the AI's skill follows the INTENSITY (Grizzly
   Time: skill = intensity × 0.25, throttle 0.7 → 1.0). So the studio's default is "the game's AI at this
   course's intensity", and any Easy / Medium / Hard is lab-only and says so in `levelNote`.
6. **Page**: copy `squirrel.html`; the five tabs (D21), the race panel (D17, D20), three cameras (D16),
   transport (D18), the Stoat look (D19), folding sections (D22), phone play (D9).
7. **Gate**: `check_studio.cjs` + every course × difficulty finishes headless + the scorecard computes.
8. **Catalog + publish** into the ONE artifact (§0): `studios.json` (with `engineMode` = the home race),
   `index.html` bay, README row. Record the first decision ("what this studio is for").
9. **Tier 4/5**: a user test script in `UNITY_VERIFICATION_CHECKLIST.md` comparing the studio's lap
   times with the real mode's (PLAY IN ENGINE in Amoebius, then the editor).

## 5. Open questions for the user

- Which vessel first (recommendation: Grizzly).
- Difficulty outside Skim Race: the game has none (skill follows intensity). Model only that, or add
  lab-only Easy / Medium / Hard levels (Squirrel-style belief mistakes) as a proposal for the game?
