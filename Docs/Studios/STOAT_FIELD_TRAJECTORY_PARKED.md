# Stoat: Mass mode and Gate mode, removed (2026-11-02)

**Why this file exists.** Yash, 2026-11-02, in two steps. First: *"keep only the orbit effect of stoat, and remove the
pathfinding field trajectory effect, you can document this field trajectory effect so if garrett screams, we can add it
back."* That shipped behind a switch (`orbitOnly`, commit `e9b924b884`). Then, after playing it: *"there are 3 ways to
control the stoat's black-white hole pair, I only want to keep the second one, document the other two, to create again in
future if team asked, for now remove them from stoat."* The three were B's modes, **Mass → Time → Gate**. Time (the orbit)
is the Stoat now; Mass and Gate are removed from the kit, and this file is how to make them again.

**Affects: Garrett's Stoat kit** (the field dipole is his round-15 design, shipped 2026-10-09; Gate mode was Yash's,
2026-10-27). Garrett has not been asked yet.

## The one that stays: Time mode, the orbit

`R_VesselActions/STOAT_DIPOLE.md` §1b. LT lays the LEFT orbit, RT the RIGHT: a light black hole and its white hole
ahead of you, the black hole on the pressed side. Since the 2026-11-02 play test:

| | |
|---|---|
| **Spin** | the right orbit spins **clockwise** (seen from above the hull), the left **anti-clockwise**: the pair's spin axis (`StoatSlingMath.SpinAxis`), and a catch only on its own side (`OnSpinSide`). Fly past a right orbit on its left and it does not catch you. |
| **The inner orbit** | caught at the detection ring, you are drawn in to `innerOrbitShare` 0.6 of the squeeze's circle at `innerPullRate` 1.5 /s; the detection ring stays where it was |
| **Earned speed** | a held pair alone warps you only half as much as the orbit does; the orbit gives the whole warp (+15%); a timed launch keeps it as momentum for 6 s. They never add. A release before 30° pays nothing; a kick never passes 1.6 × your release speed |
| **The launch** | let go, or **flick** the steering thumb (a fast swing from rest), and you launch toward where the stick points, up to 60° off the tangent; the timed kick × **Mass** (×1 → ×2.5). Mass 5 Armoured Wake shields the launch's trail |
| **On a phone** | two orbit discs, LT and RT, round the right thumb; a thumb anywhere else steers and flicks (`author_touch_profiles.py`); a full squeeze in 1 s; a haptic tick entering the window |

These rows are Unity-first: the Stoat studio page does not have them yet.

## Removed: Mass mode, the field trajectory

The Stoat's default mode from 2026-10-09 to 2026-11-02, ported from the Stoat Flight Studio's field trajectory
(`Docs/Studios/StoatFlightStudio.html`, the `ft*` rows; the full spec is `R_VesselActions/STOAT_DIPOLE.md` §1, §1a):

| Piece | What it did |
|---|---|
| **The field dipole** (LT / RT) | One sink-source pair laid 250 u ahead. The triggers' difference pulled the poles apart sideways, their sum lengthways. Let go and they slammed together and annihilated. Its field really bent the hull's flight, and the sink was a wormhole to the source. |
| **The pathfinder's line** | The hull's own flight run 1200 u forward on the held inputs, through the field, and drawn as dots from the nose: blue-grey while open, lime when the poles warped it (a bend of 3° or more, or through the wormhole). |
| **The warp** | While the line was lime the flight clock ran ×2.5 (Time → ×5). |
| **The thick trail** | Mass-mode prisms laid ×3 → ×6 as thick (Mass's one dial); Mass 5 (**Armoured Wake**) laid them shielded. |
| **Accretion** (Space 5) | What the sink swallowed was stolen, and grew both poles. |
| **Its AI** | `StoatDipoleExecutor.Autopilot`: path-watching holds, a one-lap orbit cap, the warp let go 250 u before a ring. |

## Removed: Gate mode, the warp gate

B's third mode from 2026-10-27 to 2026-11-02 (`STOAT_DIPOLE.md` §1f; the studio's `layGate` / `gateHold` / `gateJumped` /
`stepGateBoost`). A press opened a real pair along the nose: the black hole 90 u ahead, its white hole further along the
same line, stretched by the squeeze from 120 to 600 u. Fly into the black hole and you came out of the white hole with the
gate's length skipped, and a warp after it worth up to 1.5 × the loop boost for 2.5 s: all of it for a straight entry into a
long gate, none 25° off. Held, it lasted 3 s; let go it fell together in 0.8 s. Within 30 u the black hole drew its owner onto
its mouth. Its AI opened one on a leg of 300 u or more within 12° of its nose, sized to end 80 u short of the target. In the
studio's scorecard it was the best of the three on a long circuit and second on hairpins. The wormhole idea now lives on as
the **Meerkat**'s (`Docs/Studios/MEERKAT.md`).

## What leaving them out costs

Measured in the studio's batch race, the AI at Hard (`STOAT_DIPOLE.md` §1b, §1f):

| | Circuit (I2) | Hairpins (I2) |
|---|---:|---:|
| Field trajectory (Mass) | 59.6 s | 33.2 s |
| Gate | 53.0 s | 61-71 s |
| **The orbit (Time)** | 53.8 s | 76.5 s |

So the orbit is level on a flowing circuit and about twice as slow on hairpins: that is tuning, not pace. Also gone with them:

- **Space 5 Accretion never fires** (it grew the dipole's poles): an open design call for the Stoat's owner. Mass found a new
  job the same day: the launch's power (Heavy Launch), and Armoured Wake shields the launch's trail.
- **HUD:** the Space card's gauge (Accretion) and the Charge card's gauge (read from the dipole) stay empty; the Mass card
  shows the Time icon. The ripple itself still fires on every annihilation.

## What was removed, and what is still in the tree

**Removed:** `StoatGateMath.cs`; every Gate branch in `StoatSlingExecutor`, `StoatPathfinderExecutor` and the HUD; the B
switch, the thick trail, Armoured Wake and the mode autopilot in `StoatModeExecutor` (now Time only) and every row of
`StoatModeActionSO`; the pathfinder's predicted path and its drawing; the dipole and mode bindings on LT, RT and B (pad and
phone); their tests.

**Still in the tree, inert:** `StoatDipoleExecutor` (still a component on `Stoat.prefab`, bound to nothing: removing the
component cleanly needs an editor pass), `StoatDipoleActionSO` and its two assets, `StoatDipoleMath` (Foresight and the AI
still use its path prediction, gauge and boost step), `StoatDipoleConfig.asset` (it holds the warp's boost, Time's one dial),
`NetStoatDipoleSink/Source`, `StoatDipoleTests`, and the Mass and Gate icons.

## Bring one back

Both modes are whole at commit **`e9b924b884`** (on `Ys-bleeding-edge`, 2026-11-02), behind `orbitOnly: 0`.

1. **The code:** `git checkout e9b924b884 -- <file>` for `StoatModeExecutor.cs`, `StoatModeActionSO.cs`, `StoatGateMath.cs` (+ .meta),
   `StoatSlingExecutor.cs`, `StoatPathfinderExecutor.cs`, `StoatHUDController.cs`, `StoatHUDView.cs`, then re-apply the
   2026-11-02 orbit changes on top (`git log -p` on those files after `e9b924b884`). For Gate alone, keep `StoatModeExecutor`'s
   cycle at Time ↔ Gate.
2. **The assets:** `StoatModeAction.asset` from that commit, with `orbitOnly: 0`; `mode_action_asset()` and `bindings()` in
   `Tools/Build/author_stoat_assets.py` from that commit (each trigger carries its dipole AND its orbit, B the switch); the
   prefab's two override blocks to match (`author_stoat_assets.py --check`).
3. **The words:** `git show 8963a920d4:Assets/Resources/ElementalAbilityMaps/Stoat.asset` is the map from before either change;
   its labels are pinned by `author_stoat_assets.py`, `check_hangar_ability_kinds.py`'s `SPOT` and `StoatSlingTests`.
4. **The phone:** the Stoat's row in `author_touch_profiles.py` (the Space card pressed LT, a Dipole disc RT, the Mass card B).
5. **The AI:** `aiWarpMinDistance: 0` (the dipole takes the straights again), and the hull's `StoatSlingConfig` AI rows back to
   `autopilotHold01: 0.5`, `aiSlingMinTurnDegrees: 30`, `aiReleaseConeDegrees: 20`, `aiSlingIntervalSeconds: 3`,
   `aiSlingMinDistance: 120`. `StoatDipoleConfig_Slingshot` was never touched.
6. **Regatta's model:** `stoat_model` in `Tools/Build/regatta_balance.py` back to the plain `boost` warp.

## The AI on the orbit alone

`Tools/Build/stoat_orbit_sim.py` (`--self-test`, `--grid`): the autopilot and the orbit ported line for line, on the shipped
`SlingshotCourse` through `course_port.py`. Two laps, median seconds over 12 courses per intensity. It is a model (no rivals,
prisms or feel); the editor race is the measurement.

| AI press rule | I1 | I2 | I3 | I4 | Average speed |
|---|---:|---:|---:|---:|---:|
| No press at all (the floor) | 140.8 | 139.2 | 143.7 | 147.6 | 60 u/s |
| The hull's old row (30 degrees, every 3 s) | 132.2 | 128.6 | 124.9 | 125.1 | 63-71 u/s |
| The numbers learned from Yash, turns only | 82.0 | 70.0 | 74.2 | 74.4 | 102-121 u/s |
| + a warp press at 280 u (`e9b924b884`) | 56.0 | 57.6 | 57.3 | 62.5 | 147-150 u/s |
| **+ the 2026-11-02 orbit (inner pull, spin side)** | **56.1** | **54.4** | **57.4** | **57.5** | **150-154 u/s** |

At most a fifth of a missed ring a race. In Regatta at the card's Time 10 (one course per intensity, gate steering only) the
2026-11-02 orbit runs 61.5 / 55.4 / 61.6 / 63.4 s, against 59.5 / 60.6 / 65.3 / 93.6 before it. The turn press cannot fire
with the target dead ahead (`StoatSlingMath.TryAutopilotSide`), so `StoatSlingMath.WantsWarp` presses whenever the ring is at
least `aiWarpMinDistance` away; the AI steers out of an orbit by the same rule as a person.

### Earned speed (Grok's review, 2026-11-02)

Before it, holding a pair was most of the speed (×2.7 from the press), so the best play was press-spam, and the AI did exactly
that. Now the orbit earns it (`STOAT_DIPOLE.md` §1b "Earned speed"), and the AI holds for the window. `--earned` in the model:

| | I1 | I2 | I3 | I4 | Presses / min | Timed releases |
|---|---:|---:|---:|---:|---:|---:|
| Before (the warp from the press, press-spam AI) | 56.1 | 55.4 | 57.4 | 58.4 | 19-28 | 0-1% |
| Earned, the same press-spam AI | 82.8 | 80.0 | 83.4 | 84.6 | 19-21 | 0-2% |
| **Earned, the window AI (shipped)** | **81.3** | **78.1** | **86.8** | **90.6** | **11-12** | **63-78%** |

So spamming no longer beats timing, at the cost of about 50% more race time for the AI. A person who times every corner should
beat both. In Regatta at Time 10 the shipped AI runs 73-136 s (it was 55-68 s): Regatta's own AI tuning comes after its rebuild.
