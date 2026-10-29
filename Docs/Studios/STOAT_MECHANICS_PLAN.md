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

## 7. Update, 2026-10-14 (play feedback: "the catch is slow; I want both mechanics at the same pace")

- **The switch is T** on a keyboard in the game and in the artifact (pad B in both). R stays the studio's reset.
  `Button2Action` moved from R to T for every vessel (`KeyboardInputStrategy`, `SingleStickMouseInputStrategy`, the
  hint chip).
- **The catch sling now runs at the field's pace.** It uses the field's warp, starting from the press, at 1.15 of the
  field's boost (`dpWarp`, `dpWarpHeld`). Unity's 90° timing is back in the artifact (`dpSweetDeg` …), so the artifact is
  the source again. Numbers are in `STOAT_DIPOLE.md` §1b. The catch now takes 56.1 s on the circuit against the field's
  59.6 s; hairpins are still the field's (71 s against 33 s). This covers §4 P2's timing port and part of P3.

## 8. Round 29, 2026-10-27: the full four-ability kit, as proposals for Yash to judge

Yash (voice, 2026-10-27): *"Stoat and Meerkat are the two vessels I really wanna complete myself ... let's think of the
other abilities of these two characters, all four abilities ... the abilities I wanna test first in artifact rather than
inside game directly ... then we will see whether to add them or not in Unity."* The Stoat Flight Studio (round 29,
`Docs/Studios/StoatFlightStudio.html`, hub copy `VesselStudio/stoat.html`) now flies all four. **These are proposals, not
approved for Unity**: every number is lab-only (`SHIPPED ab*`, `GAME_NAMES` status `lab`), the shipped element map is
unchanged (`python3 Tools/Build/element_ability_table.py Stoat`: Charge Ripple passive / L5 Siphon, Mass "Mass / Time Mode"
on B / L5 Armoured Wake, Space Field Dipole on LT / L5 Accretion, Time Pathfinder passive / L5 Foresight), and nothing
reaches the game until Yash decides and the `/vessel` §3 design gate signs the map off. The kit follows the fleet's element
language (Time = speed, Mass = making, Space = destroying, Charge = pilots and crystals) and PvP is petals only: no ability
moves or slows a rival; a hit moves one petal and silences the victim for 1 s, and a silenced pilot scores nothing and
destroys no rival's prisms.

| Element | Ability | Input | What it does | Level-5 upgrade | Its sliders (lab-only) |
|---|---|---|---|---|---|
| Charge | **Ripple** (new, active) | LB, keyboard F, phone LB button | A gravity wave rings out to the reach in 0.8 s: crystals it passes fly at you (a petal of their element when they arrive), rivals it passes show you their next ring and path | **Siphon**: each rival it passes loses one Time petal to you | reach 260 u, recharge 6 s, reveal 4 s, crystal pull 160 u/s faster than you |
| Mass | **Field Trajectory** (round 28) + accretion trail | LT / RT in Mass mode | The warped (lime) stretch you fly lays prisms in your colour | **Armoured Wake**: those prisms are shielded (a cage); no Collapse destroys them | 6 prisms per 100 u, 8 s life |
| Space | **Collapse** (new) | Y, keyboard G, phone Y button | With a pair held (either mode), or within the grace after letting go, the black hole implodes every prism within the radius (they fall in; the world's grow back after 30 s and every race) and the pair is spent | **Spaghettify**: a rival inside the radius takes a scored hit (one Space petal, silenced) | radius 90 u, recharge 8 s, grace 1 s |
| Time | **Catch Sling** (round 28) | LT / RT in Time mode | As round 28 | **Whip**: let go within the window after the orbit's fastest point (the speed peak of the swing; the Time tile lights) for an extra burst | window 25 deg, burst 0.6 x speed |

Each ability has an on / off switch and a level L0-L10 in **Vessel Config ▸ Section ▸ Ability · ...** (save any of them as a
named profile, round 27). The stage carries the four-icon ability row in the locked order Charge, Mass, Space, Time (shape,
not colour; the accent marks the mode B picked, a Collapse you can fire, the Whip window, an unlocked level 5; recharge
shades; the petals of each element over its tile; lower right on a PC, under the race clock in phone touch play, where the
lower corners are the thumbs').

**The AI** (AI Config ▸ *AI uses the new abilities*, on): input-only rules. Ripple when a crystal it has not drawn is within
90% of the reach (or, with Siphon, a rival is); Collapse just after it lets go of a pair, when a prism inside the radius sits
within 20 u of its line to the next ring (or, with Spaghettify, a rival is inside it), and never while a field pair still
warps its path (measured: a collapse inside the warp cost the Hard field AI 6-30% on I2 and I3); Whip when the Time tile is
lit and its nose is within 1.5 x its release cone of the ring.

**Measured** (the level scorecard, Balanced, 2 laps, median of seeds 11/23/37, seconds; every race finished). With all four
off the 72 races are byte-identical to round 28. On:

| Course | Mass (field) Easy / Medium / Hard, off -> on | Time (catch) Easy / Medium / Hard, off -> on |
|---|---|---|
| I1 | 77.5 -> 80.3 · 79.6 -> 79.6 · 54.0 -> 54.0 | 66.6 -> 66.6 · 71.6 -> 72.5 · 60.5 -> 59.2 |
| I2 | 86.2 -> 86.2 · 78.5 -> 78.5 · 59.6 -> 59.6 | 66.9 -> 65.7 · 66.5 -> 68.5 · 53.8 -> 57.6 |
| I3 | 80.7 -> 80.7 · 69.2 -> 69.2 · 51.5 -> 51.5 | 60.0 -> 60.0 · 69.2 -> 68.4 · 77.2 -> 77.2 |
| I4 | 65.2 -> 65.2 · 65.9 -> 65.5 · 49.2 -> 49.2 | 64.9 -> 64.9 · 73.6 -> 72.7 · 79.5 -> 73.1 |

Over the 36 races of each mode the AI rang 431 / 406 ripples and drew 546 / 510 crystals; in Mass mode it laid 12,617
accretion prisms and collapsed only 5 times (the warp is almost never over before the pair dies); in Time mode it collapsed
119 times (920 prisms) and whipped 12 times. None of the four makes the Stoat faster on its own: Ripple and the accretion
trail never touch the flight, and Collapse and the Whip move the Hard catch-sling AI by -8% to +7% (chaotic, not a trend).
**For Yash to decide**: whether each ability belongs in the kit, its input, its level-5 upgrade, and its numbers. What is not
modelled: prisms do not collide with hulls in this studio (so a Collapse clears no real obstacle and the accretion trail
blocks nobody), element levels only gate the upgrade (no scaling), the petal ledger (3 of each to start) is the studio's,
and silence is 1 s flat.

## 9. Round 30, 2026-10-27: what fires the Ripple, Garrett's rule vs the Mix

**The conflict.** Round 29 put the Charge ability, **Ripple**, on **LB**. Garrett's merged pilots change
(`Docs/Pilots/CALLSIGNS.md` §5, PR #1589) makes LB the **pilot sight** on every vessel (hold LB to see what your pilot
sees). Yash (2026-10-27): *for every decision where we conflict with Garrett, make two versions, Garrett's way and a mix
with our recommendation, so he can judge.* The studio now has a **Ripple trigger** dropdown (Vessel Config ▸ Ability ·
Charge · Ripple; `abCTrigger`, saved with the settings and in the section's profiles; lab-only):

| Option | LB | What fires the Ripple | The AI |
|---|---|---|---|
| **Fleet rule** (Garrett's way) | the pilot sight | nothing: it pulses on its own every recharge (6 s) while a crystal it has not drawn, or a rival, is inside the reach (260 u) | presses nothing; the pulse is automatic |
| **Mix** (our recommendation, **the default**) | the pilot sight | the moment you **let go** of a pair (Mass) or a sling (Time), the recharge kept; a pair spent by a Collapse does not fire it | presses nothing; it fires as the AI lets go, which it already does |
| **Round 29** (archived) | the Ripple | LB, as round 29 | presses LB by round 29's rule; labelled as conflicting with the pilot sight |

Under the first two, **hold LB** (keyboard F, the phone's **LB · Sight** button) for a stand-in pilot sight: every
crystal and every rival is marked with a halo that reads through the prisms (Echo Sight's two-part mark: the crystal
driven toward its saturated colour, plus an additive halo drawn without a depth test), fading in and out. It touches
nothing but photons; no step, AI or scorecard reads it. It is a stand-in because each callsign sees one thing and the
studio has no pilots, so it marks both. The ability row's Charge tile reads **AUTO**, **LET GO** or **LB**. The Unity
Stoat's shipped Ripple (`StoatRippleExecutor`) is passive too, and it fires as a pair **annihilates**, which is close to
the Mix: the Mix fires at the let-go, the shipped one when the poles meet a moment later.

**Measured** (the level scorecard, Balanced, 2 laps, median of seeds 11/23/37, seconds; all four abilities on; every race
finished). The race times are **identical for all three triggers** (the Ripple draws crystals and reveals rivals; it never
touches the flight), and the same as round 29's on-table; with the abilities off all 72 races are byte-identical to round
29, and the Round 29 option reproduces round 29's on-runs byte for byte.

| Course | Mass (field) Easy / Medium / Hard | Time (catch) Easy / Medium / Hard |
|---|---|---|
| I1 | 80.3 · 79.6 · 54.0 | 66.6 · 72.5 · 59.2 |
| I2 | 86.2 · 78.5 · 59.6 | 65.7 · 68.5 · 57.6 |
| I3 | 80.7 · 69.2 · 51.5 | 60.0 · 68.4 · 77.2 |
| I4 | 65.2 · 65.5 · 49.2 | 64.9 · 72.7 · 73.1 |

| Over 36 races per mode | Fleet rule | Mix | Round 29 LB |
|---|---|---|---|
| Ripples per race, Mass / Time | 12.2 / 11.5 | 9.0 / 7.9 | 12.0 / 11.3 |
| Crystals pulled per race, Mass / Time | 15.2 / 14.2 | 11.4 / 8.8 | 15.2 / 14.2 |
| Four Hard Stoats racing (you AI + Comet, Needle, Anchor rivals), I1-I4, per pilot per race, Mass / Time: ripples | 9.1 / 11.3 | 7.1 / 8.0 | 9.0 / 11.0 |
| the same: crystals pulled | 5.1 / 6.9 | 3.6 / 5.2 | 5.1 / 7.1 |
| the same: Siphon hits scored | 7.4 / 7.3 | 7.7 / 6.8 | 7.2 / 7.6 |

(The race times of the four-Stoat races are identical across the three triggers too.)

**Why the Mix.** Garrett's fleet rule frees LB, but it makes the Ripple a timer: it fires at full rate whatever the pilot
does (12 a race, as many as the round-29 AI pressing LB at the best moment) and pulls every crystal it can, so there is
nothing to get better at. (Siphon scores about 7 hits per pilot per race under all three triggers: four Stoats racing fly
in a pack, so almost any ripple passes a rival; the trigger changes the crystals, not the petals.) The Mix also
frees LB and needs no button, but it ties the Ripple to the Stoat's own skill, the let-go: let go near crystals and rivals
and it pays; let go in empty space and the recharge is spent. It costs a quarter to a third of the ripples (the AI lets go
where the course tells it, not where the crystals are), which is the point: the pull is earned, not given. And it is the
closest of the two to the shipped Unity Ripple, which already fires off the pair. **For Garrett and Yash to decide**: the
fleet rule or the Mix (or the Mix with the shipped annihilation moment instead of the let-go). Not modelled: the pilot
sight is a stand-in (no callsigns, no suppression volume, no per-pilot sight), and the AI does not yet choose its let-go
for the Ripple (it lets go for the ring), so the Mix numbers are a floor for a pilot who plays it.
