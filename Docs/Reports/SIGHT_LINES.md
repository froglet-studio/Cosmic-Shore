# Sight lines in the race games

Garrett, 2026-11-14, after Scarab Rally I4 (#1979): "respect sight lines from one ring to another so that prisms are
not occluding which ring is lime green from the perspective of leaving a ring", and "there are probably other intensity
levels of other race games that make that mistake".

**Measured with** `bash Tools/Build/sight_line_audit/run.sh [mode ...] [--field-only]`, a reader tool that builds
each mode's shipped course and the prisms it lays near it (the same generators the game calls), 16 seeds per
intensity. From the ring you leave (its centre and four points at half its radius) it casts 80 rays to the next ring's
rim. A leg is **hidden** when over half of them are blocked. The fittings of the ring you leave and of the next ring
(a door, a breach wall, a Breakwater station, a Thresher nest) count as the ring, except that a Blast Run door's plug
reaches past its ring's rim, so it shows up as a blocker.

## Result (bleeding-edge at 5a53972a)

| Mode (vessel) | I1 | I2 | I3 | I4 | What hides the next ring |
|---|---|---|---|---|---|
| Scarab Rally (Scarab) | 0 | 0 | 0 | 0 | nothing, after #1950 and #1979 |
| Switchback (Dolphin) | 1% | 12% | 14% | 67% | the **doors**: the plug across a blocked ring reaches past its rim, so the lime ring behind it is hidden until it is blasted. At I4 the gauntlet's plugged **tunnels** on the line add to it. The field alone hides 1% of legs at I1 and none at I2-I4. |
| Breach (Orca) | 0 | 0 | 39% | 22% | the **walls**: a wall stands square across the line 110 u before its ring and 16-22 u wider than it, so from the ring you leave it covers the ring. |
| Headlong (Rhino) | 16% | 34% | 16% | 16% | the **hedges** you plough through. Rhino is on hold, so this is not changed. |
| Skein (Urchin) | 0 | 0 | 0 | 3% | the rails, partly (13% of rays at I4) |
| Regatta | 0 | 0 | 0 | 0 | rails cover 12-21% of the rim, but never most of it |
| Slingshot (Stoat) | 0 | 0 | 0 | 0 | the orrery covers 19% of the rim at I4, never most of it |
| Breakwater, Thresher Time, Thornline, Perch Run, Slip Line, Decoy Run | 0 | 0 | 0 | 0 | nothing that matters (at most 5% of rays) |

Every cell is the share of legs where the next ring is hidden. The full table, with the blocked share of rays, the
share blocked by prisms off the line, and Blast Run's first blocker by part, is what the tool prints.

**Not covered**, because their layouts are built inside the controller: Serpent Time, Waystation (its towers are the
course) and Lantern Run. Redline, Grizzly Time, Wrasse Run, Brittle Star Time, Warpline and Burrow Run lay no prisms
near the line.

## What it means

No other mode repeats Scarab Rally I4's mistake: a decorative field hiding the ring. Where the next ring is hidden
elsewhere, the cause is an obstacle the mode deliberately puts in front of the ring: Switchback's doors, Breach's
walls and Headlong's hedges. Clearing those the way #1979 cleared the field would take away the mechanic, so that is
Garrett's decision, not a sight-line fix. One way to keep both is to light the next ring's guard (its door or wall)
the way the ring itself is lit, so the next one reads at a glance (the LOCKED rule: show a world fact by lighting the
thing).
