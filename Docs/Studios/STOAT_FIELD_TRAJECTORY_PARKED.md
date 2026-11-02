# Stoat: the field trajectory, parked (2026-11-02)

**Why this file exists.** Yash, 2026-11-02: *"I think we keep only the orbit effect of stoat, and remove the pathfinding
field trajectory effect, you can document this field trajectory effect so if garrett screams, we can add it back."* The
shipped Stoat now flies the catch orbit alone (`R_VesselActions/STOAT_DIPOLE.md` §1b), in every mode. Nothing was deleted:
the field trajectory is one switch away, and this file says what it was, what it costs to leave out, and how to bring it back.

**Affects: Garrett's Stoat kit** (the field dipole is his round-15 design, shipped 2026-10-09). He has not been asked yet.

## What the field trajectory is

The Stoat's **Mass mode**, ported from the Stoat Flight Studio's field trajectory (`Docs/Studios/StoatFlightStudio.html`,
the `ft*` rows; the full spec is `R_VesselActions/STOAT_DIPOLE.md` §1):

| Piece | What it does |
|---|---|
| **The field dipole** (LT / RT) | One sink-source pair laid 250 u ahead. The triggers' difference pulls the poles apart sideways, their sum lengthways. Let go and they slam together and annihilate. Its field really bends the hull's flight, and the sink is a wormhole to the source. |
| **The pathfinder's line** | The hull's own flight run 1200 u forward on the held inputs, through the field, and drawn as dots from the nose: blue-grey while open, lime when the poles warp it (a bend of 3 degrees or more, or through the wormhole). |
| **The warp** | While the line is lime the hull's flight clock runs ×2.5 (Time → ×5). The path stays the path and only arrives sooner. |
| **The thick trail** | Mass-mode prisms laid ×3 → ×6 as thick (Mass's one dial); Mass 5 (**Armoured Wake**) lays them shielded. |
| **Accretion** (Space 5) | What the sink swallows is stolen, and grows both poles. |

**Gate mode** (`STOAT_DIPOLE.md` §1f) sits on the same B switch, so it is parked with it. The wormhole idea itself lives on as
the Meerkat's (`Docs/Studios/MEERKAT.md`).

## What the Stoat is now

Time mode alone. A press lays a light pair ahead, the attractor on the pressed side, and it catches you as you pass; hold to go
round it, and release a quarter-turn round for the whole kick. A held pair warps the clock to ×2.725 at rest (×5.6 at Time 10).
B does nothing, and the hull starts in Time mode on every peer. **The AI stays there too**, and also presses for the warp alone on
a long leg (below), because that is now its only speed on a straight.

The switch is `StoatModeActionSO.orbitOnly`, **on** in `Assets/_SO_Assets/VesselActions/Stoat/StoatModeAction.asset`.

**Dormant, not deleted:** `StoatDipoleExecutor`, `StoatDipoleMath`, `StoatDipoleConfig.asset`, the prefab's dipole bindings
(`StoatDipoleLeft/RightAction` still ride each trigger and stand aside outside Mass mode), the pathfinder's prediction,
`StoatGateMath`, the Gate code, `NetStoatDipoleSink/Source`, `StoatDipoleTests`. The pathfinder executor itself stays live: it is
still the one writer of the flight clock, draws the orbit's two rings, and draws Time 5 Foresight.

## What leaving it out costs

Measured in the studio's batch race, the AI at Hard (`STOAT_DIPOLE.md` §1b; the rows after the 2026-10-27 orbit fix):

| | Circuit (I2) | Hairpins (I2) |
|---|---:|---:|
| Field trajectory | 59.6 s | 33.2 s |
| The orbit, timing + warp | 53.8 s | 76.5 s |

So the orbit is level on a flowing circuit and **about twice as slow on hairpins**, where its circles cost time. That is tuning,
not pace, but expect it to show at Slingshot's upper intensities and in any arena corner.

Also lost while parked. These are **open decisions for the Stoat's owner**, not bugs:

- **Mass has no effect on the Stoat.** Its dial was the thick trail. Mass 5 Armoured Wake gates on Mass mode, so it never fires.
- **Space 5 Accretion never fires:** it grows the dipole's poles. Space still widens the orbit ×1 → ×1.4.
- **HUD:** the Space card's gauge (Accretion) and the Charge card's gauge (the ripple a let-go would throw, read from the dipole)
  stay empty; the Mass card shows the Time icon. The ripple itself still fires on every annihilation. The cards keep the Field
  Dipole and Modes art (`/ability-icon`).
- **The comeback** buys levels of every element, so its Mass share buys nothing.

## Bring it back

1. **The switch:** `orbitOnly: 0` in `StoatModeAction.asset`, and the same in `mode_action_asset()` in
   `Tools/Build/author_stoat_assets.py`. B steps Mass → Time → Gate again, Mass is the default, and the AI switches modes itself.
2. **The words:** `git show 8963a920d4:Assets/Resources/ElementalAbilityMaps/Stoat.asset` is the map before this change (labels
   Field Dipole, Pathfinder / Time Mode, Mass / Time Mode). Put it back, and the labels pinned by `author_stoat_assets.py`,
   `check_hangar_ability_kinds.py`'s `SPOT` and `StoatSlingTests` (`Map_DeclaresAllFourAbilitiesAndTheirLevelFives`,
   `Touch_TheModeCardIsTheModeSwitch_OnAPhoneToo`, `ModeAction_ShipsOrbitOnly`).
3. **The AI:** set `aiWarpMinDistance: 0` (the Mass-mode dipole takes the straights again), and in the hull's `StoatSlingConfig`
   restore `autopilotHold01: 0.5`, `aiSlingMinTurnDegrees: 30`, `aiReleaseConeDegrees: 20`, `aiSlingIntervalSeconds: 3`,
   `aiSlingMinDistance: 120`. `StoatDipoleConfig_Slingshot` was never touched and still holds its learned numbers.
4. **Regatta's model:** `stoat_model` in `Tools/Build/regatta_balance.py` back to the plain `boost` warp, then
   `author_regatta_assets.py --check`.

Step 1 alone is enough to fly it again; steps 2-4 make the rest of the game tell the truth about it.

## The AI on the orbit alone

`Tools/Build/stoat_orbit_sim.py` (`--self-test`, `--grid`): the autopilot and the orbit ported line for line, on the shipped
`SlingshotCourse` through `course_port.py`. Two laps, median seconds over 12 courses per intensity. It is a model (no rivals,
prisms or feel); the editor race is the measurement.

| AI press rule | I1 | I2 | I3 | I4 | Average speed |
|---|---:|---:|---:|---:|---:|
| No press at all (the floor) | 140.8 | 139.2 | 143.7 | 147.6 | 60 u/s |
| The hull's old row (30 degrees, every 3 s) | 132.2 | 128.6 | 124.9 | 125.1 | 63-71 u/s |
| The numbers learned from Yash, turns only | 82.0 | 70.0 | 74.2 | 74.4 | 102-121 u/s |
| **Shipped: those + a warp press at 280 u** | **56.0** | **57.6** | **57.3** | **62.5** | **147-150 u/s** |

At most half a missed ring a race. The turn press cannot fire with the target dead ahead (`StoatSlingMath.TryAutopilotSide`), so
a lined-up AI would sit at cruise; `StoatSlingMath.WantsWarp` presses whenever its ring is at least `aiWarpMinDistance` away, and a
pair that catches it with the target near the nose is let go at once. It goes through the replicated press path a person's does.
The sweep relearns the sling rows from Slingshot races, and keeps this hand-set row.
