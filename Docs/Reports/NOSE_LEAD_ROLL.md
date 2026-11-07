# The nose lead's roll: why it corkscrewed, the fix, and what each hull should fly (2026-11-07)

Brief 22 of the Regatta balancing plan (`/mnt/project-files/regatta-training/BRIEFS.md` §22, §14, §0;
`Plan step: time-arcade`). **Every number here comes from the offline models.** Nothing was raced in the Unity
editor, and nothing is written to an asset: `NoseLeadGain` stays 0 everywhere. Draft PR #1834, on top of #1826.
Results, scripts and run logs: `/mnt/project-files/regatta-training/results/lead-roll-decoupled/`.

## 1. In short, for Yash

The PR #1786 nose lead corkscrewed every AIPilot hull with a roll stick. The fix in #1834 is that **while the lead
steers, the hull flies no roll stick**. Once you turn the lead on, nothing traps any more. It still pays off only
where the table says so:

| Hull (game) | Proposal | Model composite (lower is better), fresh courses | Misses I1-I4 | Editor check first? |
|---|---|---|---|---|
| **Thresher** (Thresher Time) | **gain 2.5, cap 40°** with #1834 (the lead-on twin's genome C, #1830) | **2.953** (lead off with the #1791 genome: 3.195; lead with the old roll: 2.985) | not counted by this model; 48/48 finish | **yes, first**: the only hull where the decoupled lead beats every lead-off cell |
| **Serpent** (Serpentine) | **gain 1.0** (#1832). #1834 changes nothing here: its roll is its single stick's x | 15.24 (lead off 15.85) | 0 / 0 / 0 / 2 | second |
| **Manta** (Redline) | **nose row instead** (#1780, 9/s), lead 0 | 7.29 with the 9/s nose (best decoupled lead: 21.78; lead off: 18.80) | 0 / 3 / 4 / 7 | the #1780 check |
| **Urchin** (Skein, what-if aim + seek) | **nose row instead** (#1780 pattern), lead 0 | lead off 7.94; best decoupled lead 8.56; lag-free nose 6.19 / 5.41 (#1831) | 19 / 43 / 65 / 83 | after Skein's aim fix (#1795) |

For the hulls whose models have no roll stick (Scarab, Dolphin, Stoat, Rhino, Sparrow), the lead-on numbers were
always roll-free. #1834 makes the game fly the way those models already did. Without it, any of them with a dual
stick could corkscrew in the editor even though its model said the lead helps.

## 2. Why it corkscrewed

`AIPilot.Steer` (on #1786) computes one cross product, from the judged forward to the steer direction, in the
nose's frame. With the lead on, the judged forward is the COMMAND's forward and the steer direction is the nose
turned past the aim. The stick is then `XSum = angle x cross.y` and `YSum = angle x cross.x`. With no belly target,
**`YDiff = XSum`**: the roll stick is the yaw stick of the LED error.

`VesselTransformer.Roll` turns the command about the NOSE's forward. With the lead off, the command sits just behind
the nose, so a roll only banks the hull. The lead keeps the command far from the nose on purpose (the want is gain x
the error, up to 40° past the aim, with the command 40-90° off the nose in a turn). A roll then swings that command
round the nose's axis, and the led yaw stick that drives it stays pinned at full. The nose chases a command that
whirls about a body-fixed axis, the gap settles near 80°, and the hull spirals away from the course. Three threads
saw this independently: the Manta (#1829), the Urchin (#1831, "YDiff = XSum") and the Serpent (#1832).

## 3. The law, and why this one

I measured three laws on the same fresh courses (`screen.py`, `screen_urchin.py`):

| Law while leading | Manta, Redline (48 races, shipped / parent C) | Urchin, what-if Skein (36 races, shipped / W) | Thresher, nests (48, parent E / lead-on C) |
|---|---|---|---|
| lead off | 22.19 / 18.80, 48 finish | 8.77 / 7.94 | 3.195 / 13.47 |
| `led` (#1786 as built) | 58.78 / 55.92, 6 / 11 finish | 38.55 / 38.55, 0 finish | 3.175 / 2.985 |
| `unled` (roll = the lead-off yaw stick) | 48.97 / 46.33, 39 / 41 finish | 36.46 / 36.35, 1-2 finish | 3.233 / 3.017 |
| **`zero` (no roll)** | **23.34 / 24.15, 48 finish** | **9.60 / 8.59, 36 finish** | **3.061 / 2.953** |

(Lead cells are gain 2.5, cap 40°. The brief's first guess was the `unled` law. It fails because a lagging nose is
off the aim for most of every turn, so even the unled yaw stick keeps swinging a command the lead holds off the nose.)

**No roll is the one law that is right for all three dual-stick hulls**: it is the only one that never traps, and it
is the best of the three laws on every genome. The hull's bank has no effect on its path (`VesselTransformer` moves
along the nose; nothing lifts), so dropping the roll costs nothing but the look of a banked turn while the lead is on.
A belly target (`MarkBellyTarget`, the rail rides) keeps its own roll. A single stick
(`SingleStickVesselTransformer`: roll = −x, yaw = x) cannot take any separate roll law, so the Serpent's lead is
the same under every law (`serpent_time_sim` check 11).

In the C#: `NoseLeadSteering.RollStick(leading, yawStick)` returns `leading ? 0 : yawStick`. AIPilot calls it for
`YDiff`, and with the lead off the stick is the same arithmetic as before. `NoseLeadRollTests` checks a closed loop:
a lagging-nose dual-stick hull with the lead at 2.5 settles on 6 of 6 aims with no roll (worst 0.9°). With the led
roll, the negative control, it settles on 4 of 6 (worst 87.7°).

## 4. The re-runs (two restarts each, run by hand, sep-CMA-ES as the parents)

| Job | Restarts' training loss | Agree | Parent (lead off) training |
|---|---|---|---|
| `redline-manta-time-lead-noroll` (2.5 / 40, no roll, 540 evals) | B 16.425 / C 16.453 | yes (0.2%) | 15.97 (3,042 evals) |
| `skein-urchin-time-whatif-lead-noroll` (2.5 / 40, no roll, 700 evals) | B 7.132 / C 6.994 | 2.0% | W 7.37 (1,500 evals) |
| `redline-manta-time-lead1-noroll` (1.0, no roll) | see §5 | | |
| `skein-urchin-time-whatif-lead1-noroll` (1.0, no roll) | see §5 | | |

The 2x2 on fresh courses (`holdout.py`; Manta: 12 Redline courses an intensity, seedbase 1001; Urchin: the what-if
Skein hold-out, 9 races an intensity). Composite, then finished and misses per intensity:

| Genome | Lead off | 2.5 / 40, no roll | 1.0, no roll |
|---|---|---|---|
| Manta parent C (lead-off winner) | **18.80** (all finish; misses 0 / 1 / 30 / 416) | 24.15 (0 / 22 / 93 / 167) | 32.56 (I4 11/12) |
| Manta twin B | 20.79 | 22.44 (0 / 15 / 72 / 231) | 30.55 |
| Manta twin C | 19.69 | 21.78 (0 / 19 / 69 / 144) | 30.76 |
| Urchin parent W (lead-off winner) | **7.94** (19 / 43 / 65 / 83) | 8.59 (24 / 35 / 68 / 72) | 9.33 |
| Urchin twin B | 8.86 | 8.62 (28 / 63 / 115 / 113) | 9.62 |
| Urchin twin C | 8.71 | 8.56 (28 / 57 / 112 / 134) | 9.77 |

What it says:
1. **The lead alone does not close the gap for the Manta or the Urchin, and moving the drive does not either.** The
   decoupled lead un-traps both hulls (every race finishes) and cuts the Manta's I4 misses by about 60%. But each
   hull's lead-off winner stays best on fresh courses: Manta 18.80 against 21.78, Urchin 7.94 against 8.56. The
   Urchin twins beat W on their training cables (6.99-7.13 against 7.37) but not on the hold-out, so that gain is
   overfitting. The lead slows the I3 races (the Manta's I3 goes from 184 s to 258-309 s).
2. **Gain 1.0 is the wrong setting for both**: 30-33 for the Manta and 9.3-9.8 for the Urchin, worse than 2.5 on
   every genome. Gain 1.0 helps only the Serpent, whose single stick still rolls and whose pull past the aim therefore
   has to stay small.
3. **The faster nose remains the Manta's and the Urchin's remedy**: 9/s takes the Manta from 22.19 to 7.29 (#1780),
   and a lag-free nose takes the Urchin from 8.77 to 6.19, with genome W 5.41 (#1831).
4. **The Thresher gains**: the decoupled lead is the best cell for both of its genomes (§3), so the lead-on genome C
   of #1830 now scores 2.953 against 2.985 with the old roll. That genome still needs the lead (13.47 with it off),
   so it ships only with the switch.

## 5. Gain 1.0 twins

(Appended when the restarts finish.)

## 6. Not verified

- The Unity editor. `NoseLeadRollTests` ran 4/4 headless against the Unity shim
  (`bash Tools/Build/nose_lead_harness/run.sh`); the editor's EditMode run is still owed.
- The editor checks, in order:
  1. AI Threshers in Thresher Time with `NoseLeadGain` 2.5 (#1834) against 0: expect no corkscrew and slightly faster nests.
  2. The Serpent at 1.0 against 0.
  3. AI Mantas in Redline at 2.5: the corkscrew should be gone, though the run is not faster than lead off. Then the #1780 9/s nose row.
  4. Any dual-stick Regatta hull at 2.5: no corkscrew.
- Each model's limits are in its docstring: no rivals, Legendary skill, and so on.
