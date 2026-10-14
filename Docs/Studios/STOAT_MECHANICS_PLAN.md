# The Stoat's mechanics — one picker, every mechanic, race-tested (plan, 2026-10-14)

The ask (the user, 2026-10-14): *"The Stoat has mechanics. Can we do the proper mechanics by being able to choose which
mechanics to follow? … compare the last few Stoat works, and create an interface where we can swap between the various
mechanics of the Stoat, with the B key or by clicking the interface UI … a proper plan to make this vessel more fun to play
and more race friendly."*

This plan follows the standing rules: **the Vessel Studio artifact is the one source of truth** (every mechanic is built
and decided there first, then cloned into Unity with `/artifact-to-unity`); Amoebius runs the same game code, so whatever
Unity gets, Amoebius gets; and **the Stoat's element map is approved** (`Docs/ElementalAbilitySystem/FLEET_MAPS.md` ▸
Stoat, Garrett 2026-10-13: B is the Mass ability, "Mass / Time Mode"), so the picker must not silently change what B
means in the shipped game — that is a design decision (§5).

## 1. What exists today — the last few Stoat works, side by side

| Mechanic | What the triggers do | Artifact (`StoatFlightStudio.html`) | Unity / Amoebius | Last verdict |
|---|---|---|---|---|
| **Field trajectory** (round 15) | Hold to lay a dipole ahead; the squeeze sets its strength and the poles' spread; the pathfinder draws where the field will carry you; a path that closes on itself turns lime and pays the warp boost | the main mechanic | **Mass mode** (`StoatDipoleExecutor` + `StoatPathfinderExecutor`), THICK trail; the default | approved 2026-10-09, shipped 2026-10-13 |
| **Catch sling** (round 6 dipole sling) | Press lays a light pair ahead to the pressed side; fly past it and it catches you; the nose settles onto the orbit's tangent; release slings you out | in the **Archive** card only (no 90° timing) | **Time mode** (`StoatSlingExecutor`), with a timing bonus the artifact does not have: the full kick only within ±15° of a quarter-turn | shipped 2026-10-14 after the orbit cut failed |
| **Orbit sling** (round 4) | Press lays the attractor beside the hull at the orbit radius; hold orbits; release slingshots | Archive only | **retired** (it was Time mode on 2026-10-13: "couldn't see the poles, just pulled right or left") | rejected as built |
| **Crystal wormhole** (charming-cerf) | A pair STYLE, not a sling: smooth wells, graded lens, seamless mouths you fly through; laid on release | Archive, pair style B | `BlackHoleConfig.crystalPairs`, a global switch, OFF | parked |
| Play styles (Momentum, Anchor, Maelstrom, Flare, …) | Blends of the field numbers, not mechanics | the Play Style Config tab | none (the game ships one tuning) | lab only |
| Passives on every mechanic | Ripple (Charge), warp boost (Time), Accretion (Space 5), Foresight (Time 5), Armoured Wake (Mass 5), Overclock (omni) | partly (ripple, boost) | all shipped | approved |

### 1.1 The race numbers (measured 2026-10-14, the studio's own batch harness)

`runBatch` in the artifact, the studio's AI pilot at Hard, one course per row, best of three squeezes (0.3 / 0.6 / 1);
the rookie is the same AI with aiNoise 0.8 on five seeds. Script: the session's `race.cjs` (to be checked in as
`Tools/Studios/race_mechanics.cjs`, §4 P1).

| | Circuit | Hairpins | Rookie, circuit (median, 5 seeds) | Top speed |
|---|---|---|---|---|
| No sling | 126.6 s | 82.1 s | — | 60 u/s |
| **Field trajectory** | **59.6 s** | **33.2 s** | **74.6 s**, 5/5 finished | 776 u/s (circuit), 175 (hairpins) |
| **Catch sling** (artifact, no 90° timing) | 111.8 s | **116.0 s — slower than not slinging** | 97.2 s | 132–136 u/s |
| **Orbit sling** | the AI never slings in this mode (0 slings): no data | | | |

What the numbers say:
1. **The field trajectory dominates.** Twice as fast as no sling on the circuit, 2.5× on hairpins, and forgiving: the
   rookie still finishes 5/5 at 74.6 s, faster than the catch sling's skilled pilot. In a race where B swaps freely,
   nobody would ever leave it — so a swap is only a choice if each mechanic WINS somewhere.
2. **The catch sling, as the artifact tunes it, costs time on hairpins.** It catches reliably (16–19 catches, mostly
   clean), but the release boost does not pay back the orbit. Unity's 90° timing bonus was never measured in the artifact
   (it was added in Unity first, against the artifact-first rule).
3. **The orbit sling has no AI.** It cannot be raced against, tested at scale, or offered to an AI seat until it has one.
4. **776 u/s spikes** on the field circuit are a readability and fairness risk in a pack (a rival vanishes from view),
   even when the average is fine.

## 2. The interface — one mechanic picker, B or a click

**The model: a mechanic RING.** The Stoat carries an ordered ring of mechanics its triggers can fly. **B steps to the
next one** (wrapping); **clicking or tapping** a mechanic on the picker jumps straight to it. The ring's DEFAULT is
today's game, `[Field trajectory, Catch sling]`, so with no setting changed B does exactly what Garrett approved. A
playtest setting widens it (`[Field, Catch, Orbit]`, …) and fixes its order.

What a mechanic carries with it (so the element map stays coherent): its trigger behaviour, its trail thickness (Field is
the THICK Mass trail, the others as authored), what the Time card shows (warp boost vs the 90° sweet spot vs the orbit
sweep), and its pole weight (Field 2.1 u / 50 000, Catch 1.6 u / 20 000).

**The picker on screen** (HUD, both the artifact and Unity):
- A compact strip above the four ability cards: one chip per mechanic in the ring, the current one lit in the domain's
  colour, its name and a one-line "how to fly it" under it for two seconds after a swap.
- Click / tap a chip to jump to it. On a gamepad, B cycles, and the strip shows which is next.
- A swap lets go of whatever the triggers hold (today's rule): a pair is never stranded under a trigger that now means
  something else; a trigger still held does nothing until pressed again.
- A short swap cooldown (0.4 s, an SO field) so B-mashing cannot cancel the previous pair's ripple into the next.

**Where it runs (Unity).** Today's mode switch is replicated by riding the B press through the server (every peer flips
together, because the trail every peer lays depends on it). B-to-cycle keeps that. A CLICK is not an input binding, so it
needs its own replicated request: a `ServerRpc(int mechanic)` on the Stoat's action handler that the server echoes as
the same "set mechanic" every peer applies — the press path generalised from "toggle" to "set index".

**Who may use the picker.** Two options for the design decision (§5):
- **(A) Playtest only** — the picker exists, but outside a playtest setting the ring is the approved `[Field, Catch]`
  and the strip is hidden; the team chooses the shipped ring with it.
- **(B) Player-facing** — the ring is a feature: players carry 2–3 mechanics and B/tap swaps between them in a race. This
  changes the approved Mass ability, so Garrett has to sign it off.

## 3. Making it race-friendly — what each mechanic needs

The goal is that **every mechanic in the ring wins a different course shape**, so swapping is a racing skill rather than a
menu choice.

| Mechanic | Its niche (target) | Work |
|---|---|---|
| Field trajectory | long sweeping lines, loops; the speed king on open courses | Cap the spike for pack readability: a soft ceiling on the warp boost's top speed (e.g. 400 u/s), measured against the circuit time; keep the rookie forgiveness. |
| Catch sling | hairpins and gate-to-gate turns: a timed 90° whip | Port Unity's 90° timing INTO the artifact (artifact-first), then tune the release kick until the catch sling beats the field trajectory on the hairpin course; target: within 5% of field on hairpins, clearly behind it on the circuit. |
| Orbit sling | (if revived) a tight hold-and-release around a fixed point; overtakes around a ring | Give the studio AI an orbit pilot, lay the attractor in front of the camera (the catch's placement fix), then race it. If it cannot find a niche the catch does not cover, it stays archived. |
| Crystal wormhole | a portal you fly THROUGH: a shortcut, not a sling | Not in the ring by default; a candidate for a course feature or a later mechanic. |

**The race-friendliness scorecard** (one table in the artifact, the same columns for every mechanic, recorded per round):
- skilled time on circuit / hairpins / a mixed course;
- rookie median time and DNF count;
- spread across seeds (consistency);
- top speed and time above 300 u/s (pack readability);
- how often the AI swaps mechanics on the mixed course, once the AI can swap.

A new **mixed course** (circuit with two hairpin sections) is the course where swapping should win: the target is that a
pilot who swaps beats either single mechanic.

## 4. The plan, in order

| Phase | Where | What | Done when |
|---|---|---|---|
| **P0** | the user / Garrett | Decide §5 (playtest-only vs player-facing, the default ring, whether the orbit is revived). | Recorded in the artifact's decision log. |
| **P1** | Artifact | The scorecard as a button: check in `race.cjs` as `Tools/Studios/race_mechanics.cjs`; add a "Mechanics" table to the studio (§3 columns); add the mixed course. | The table reproduces §1.1's numbers and shows the mixed course. |
| **P2** | Artifact | The picker: the HUD strip, B cycles, click jumps, the swap cooldown, the ring setting (default `[Field, Catch]`). Unarchive the catch sling into the ring and port Unity's 90° timing into it (so the artifact is the source again). | Flying in the artifact, B and clicks swap; `check_studio.cjs` passes with a swap test; the catch sling's kick matches Unity's `StoatSlingMath.SweetSpot` on the same inputs. |
| **P3** | Artifact | Race tuning rounds: cap the field spike, tune the catch kick for hairpins, the AI swaps on the mixed course. Each round recorded with Record (settings snapshot). | §3's targets met on the scorecard. |
| **P3b** | Artifact (optional) | The orbit's AI and camera-visible placement; race it; keep or archive. | It has a niche on the scorecard, or it is archived with its numbers. |
| **P4** | Unity (+ Amoebius) via `/artifact-to-unity` | Generalise `StoatModeExecutor` into a mechanic ring: `StoatMechanic` enum (static values), a `StoatMechanicRingSO` (the ring, the default, the cooldown), B = next, the replicated set-index request for clicks, the HUD strip in `StoatHUDVariant`, every executor standing aside unless its mechanic is current (today's pattern). The decided numbers cloned with `studio_to_unity.py`. Tests with page-golden numbers. | Unity checklist entry; Amoebius builds; `studio_to_unity --check` clean. |
| **P5** | Unity | The AI swaps across the RING (input-only, through the same replicated press). It already swaps two modes (`StoatModeExecutor.Autopilot`: Time when the target is > 70° off the nose, at most every 3 s, `StoatModeActionSO`); P5 generalises that rule to N mechanics with the rule the artifact settled in P3. | Slingshot mode with 3 AI Stoats: the AIs swap on the mixed course and the scorecard holds in the game. |

## 5. Decisions needed before P2

1. **Who uses the picker?** (A) the team, to choose the shipped mechanics, or (B) players, as a race feature (needs
   Garrett's sign-off: it changes the approved Mass ability).
2. **The default ring.** Today's `[Field, Catch]`, or another set.
3. **The orbit sling.** Revive it with an AI and a visible placement (P3b), or leave it archived.
4. **The crystal wormhole.** Keep it out of the ring (recommended), or make it a third entry.

## 6. Building on Garrett's Stoat work (surveyed 2026-10-14)

Garrett's Stoat work runs as `cece/*` Claude sessions that he merges into `bleeding-edge`. Every branch with a Stoat,
sling, dipole, black-hole or pathfinder change since 2026-09-30 was checked against `Ys-bleeding-edge`.

**Already on this branch (nothing to take; build on it as it stands):**

| PR | Branch | What it gave the Stoat |
|---|---|---|
| #1064 | `cece/fervent-hypatia-rg4why` | The Stoat port: black holes, the lens, the element map, Slingshot and Warpline |
| #1076 | `cece/zen-fermat-wu03hr` | Its own vessel: the Mass / Time modes on B, the Charge ripple, Accretion (Space 5), Foresight (Time 5), its own HUD |
| #1085 | `cece/admiring-cannon-lhmv6t` | The play-feedback pass: birth and annihilation waves OFF, a subtle ripple, the 3D path dots redrawn (`StoatPathDots.shader`), **Time mode = the catch sling with the 90° timing** |
| — | (on `Ys-bleeding-edge`) | `Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md`: the Stoat is one of Regatta's six racing AIs; its sling AI (`StoatSlingExecutor.AutopilotSling`) is the reference for "an AI that presses the hull's mechanic" |

Also already there: **the AI switches modes on its own** (`StoatModeExecutor.Autopilot`); P5 starts from it.

**Not merged anywhere yet:**

| Branch | Last commit | What | How to take it |
|---|---|---|---|
| `cece/dreamy-pascal-7xxx1m` | 2026-10-14 | **Prism movement is a fundamental** (`PrismMotion`): a moving prism that hits another destroys both. The Stoat's gravity field and the Rhino's plough are the movers; box contacts at <= 256 bodies, spheres above (`BlackHoleConfigSO.boxContactMaxBodies`). Touches `BlackHoleGravityField`, `BlackHoleRegistry`, `STOAT_DIPOLE.md`. | Work in progress, a fleet-wide rule rather than a Stoat mechanic. Let it land on `bleeding-edge` through Garrett's PR, then merge `bleeding-edge` in. Every mechanic in the ring gets it for free, since every pair runs the same gravity field. Do not cherry-pick it: it is a LOCKED-style fundamental. |
| `cece/charming-cerf-alf1j1` | 2026-10-09 | The crystal wormhole rebuilt as one geometry: field, light and flight (`CrystalWormholeView`, `CrystalWormholeLens`, `ThroatWarp`, a 905-line simulator), then tuned so sink to source takes under 10 s. Only `BlackHoleConfig.crystalPairs` (off) reached this branch. | Ask Garrett whether it is still live. If it is, it becomes the **Crystal wormhole** module (§6.1), behind its own entry rather than the global `crystalPairs` switch. It is five days stale and conflicts with the lens rework (`BlackHoleLensPass`), so merge `bleeding-edge` into it first. |

### 6.1 Modular mechanics: each one a module, none lost

Today two hard-coded modes gate three executors (`IsMassMode` / `IsTimeMode` in `StoatDipoleExecutor`,
`StoatSlingExecutor`, `StoatPathfinderExecutor`, the HUD). The modular form keeps every one of Garrett's executors
unchanged inside and only replaces the gate:

- **`StoatMechanic` enum** (static values): `Field = 0` (Garrett's Mass mode), `Catch = 1` (Garrett's Time mode, with
  his 90° timing), `Orbit = 2` (round 4, archived), `Crystal = 3` (charming-cerf). New mechanics append.
- **One module per mechanic** = its executor + its config SO + what it shows (trail thickness, path or rings, the Time
  card's readout) + its AI policy. Each executor asks `_mode.IsCurrent(StoatMechanic.X)` instead of
  `IsMassMode` / `IsTimeMode`.
- **`StoatMechanicRingSO`**: which mechanics the B ring holds, their order, the default, and the swap cooldown. The
  shipped ring is `[Field, Catch]`, which is Garrett's approved behaviour.
- **The passives stay outside the ring**, on every mechanic: ripple, Accretion, Foresight, Armoured Wake, Overclock.
- `StoatModeExecutor` becomes the ring owner. Its replicated press, its let-go-on-swap rule and its one-writer
  thickness stay exactly as Garrett wrote them.

### 6.2 Where the settings are chosen: three surfaces, one set of numbers

| Surface | What you choose there | Where it is saved |
|---|---|---|
| **Artifact** (source of truth) | Vessel Config tab ▸ **Mechanics**: the ring (tick boxes plus order), the default, the cooldown, and each mechanic's own numbers in its group (Field / Catch / Orbit / Crystal). The Archive card is retired into it. Recorded with Record. | The decision log, cloned by `/artifact-to-unity` (`studio_to_unity.py` writes `StoatMechanicRingSO` and each module's config SO) |
| **Amoebius** | Its VESSEL STUDIO page opens the same artifact. Amoebius runs the game's own assets, so the ring Unity ships is the ring Amoebius plays. In-game, B and the HUD chips swap. | Nothing of its own |
| **Unity: FrogletTools ▸ Vessels ▸ Stoat Mechanics** (the panel the user asked for, 2026-10-14) | A **play-session picker**: tick which mechanics this play session's ring holds and which one you start in, then press Play. It shows each mechanic's numbers read-only, with **Open in the studio** to change them. | A session override only (`EditorPrefs`, read by the ring owner in the Editor). It never writes the shipped `StoatMechanicRingSO` |

The Unity panel is scoped this way because of the LOCKED rule (CLAUDE.md, the user 2026-10-10): no Unity tuning
window may re-draw a studio's parameters or hand-edit a mapped field. A picker that chooses which existing mechanics a
play session uses, and shows numbers without editing them, stays inside that rule. If the panel should EDIT numbers, the
rule has to be lifted for the Stoat first.
