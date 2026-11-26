#!/usr/bin/env python3
"""
The Lists - Riposte's arena (GameModes.Riposte = 97, the Swordfish's CHARGE game) - as a MIRROR of
SpawnableRiposteYard.cs and RiposteYard.cs, not an estimate. author_riposte_assets.py imports it for the
census, the laid volume, the PhaseThresholds and the placement checks. It reuses jackstraw_budget's hash,
vectors and geometry (the yard IS a SpawnableJackstraw: same sticks, same jacks, same clearings).

Each piece stands where it does for something the Swordfish's kit does (RIPOSTE.md section 4):
  lane   a straight, stick-free tube from the centre out through a refuel clearing (the ramp winds up)
  rail   pickets parallel to a lane, just outside its wall (fuel the bill cuts without leaving the line)
  gate   a jack on a lane's axis, its four cross arms shielded (breaks the line)
  thorn  red tips on the loose sticks nearest a lane wall (drifting off the line costs)
  core   an ironwood heart in every loose stick near the centre (an unlit blade binds where lanes meet)

Run it to print the ladder:   py Tools/Build/riposte_yard_budget.py
"""
import math

import jackstraw_budget as jb

# ── Constants: must match RiposteYard.cs / SpawnableRiposteYard.cs (the generator greps for each) ──
PICKET_PRISMS = 6
PICKET_GAP = 15.0
RAIL_STANDOFF = 6.0
RAIL_START = 10.0
CLEARING_INSET = 10.0
LOOSE_JACK_KEY_BASE, LOOSE_STICK_KEY_BASE = 3_000_000, 4_000_000
DEFAULT_SEED = 9797
PICKET_LENGTH = PICKET_PRISMS * jb.SEGMENT
ORIGIN = (0.0, 0.0, 0.0)


def basis(axis):
    helper = (0.0, 0.0, 1.0) if abs(axis[2]) < 0.9 else (1.0, 0.0, 0.0)
    e1 = jb.norm(jb.cross(axis, helper))
    return e1, jb.cross(axis, e1)


def rail_offset(axis, lane, rail, rails, lane_radius):
    e1, e2 = basis(axis)
    angle = 2.0 * math.pi * rail / max(1, rails) + jb.GOLDEN_ANGLE * lane
    m = lane_radius + RAIL_STANDOFF
    return tuple((e1[i] * math.cos(angle) + e2[i] * math.sin(angle)) * m for i in range(3))


def picket_centre(centre_radius, index):
    return centre_radius + RAIL_START + PICKET_LENGTH * 0.5 + index * (PICKET_LENGTH + PICKET_GAP)


def gated_lane(lane, every_nth):
    return every_nth > 0 and lane % every_nth == 0


def gate_distance(frm, to, gate, gates):
    return frm + (to - frm) * (gate + 1) / (gates + 1)


def mag(v):
    return math.sqrt(jb.dot(v, v))


def scale(v, s):
    return (v[0] * s, v[1] * s, v[2] * s)


def build(p):
    """Lay the yard for one prefab's fields. Returns pieces, lanes, clearings and the census.

    p: seed, heapRadius, centreClearingRadius, ringClearingCount, ringClearingDistance, ringClearingRadius,
       laneCount, laneRadius, railsPerLane, gateEveryNthLane, gatesPerLane, looseSticks, looseJacks, thornBand,
       ironwoodCoreRadius.
    """
    seed = p["seed"] or DEFAULT_SEED
    R = p["heapRadius"]
    clear = jb.clearings(p)
    has_centre = p["centreClearingRadius"] > 0
    centre_radius = p["centreClearingRadius"] if has_centre else 0.0
    ring = len(clear) - (1 if has_centre else 0)
    lanes = min(p["laneCount"], ring)
    lane_axes = [jb.fibonacci(k, max(1, ring)) for k in range(lanes)]
    lane_ends = [scale(a, R) for a in lane_axes]
    w = p["laneRadius"]

    segs = []
    prisms = []       # (centre, axis, size, kind, family)   family: gate | rail | jack | stick
    margins = []
    stick_size = (jb.SECTION, jb.SECTION, jb.SEGMENT * jb.FLUSH)
    knot = (jb.KNOT, jb.KNOT, jb.KNOT)

    def clear_of_clearings(a, b):
        worst = float("inf")
        for c, r in clear:
            d = math.sqrt(jb.point_segment_sqr(c, a, b)) - (r + jb.SECTION)
            if d < 0:
                return None
            worst = min(worst, d)
        return worst

    def clear_of_laid(a, b):
        worst = float("inf")
        for sa, sb in segs:
            d = math.sqrt(jb.segment_segment_sqr(a, b, sa, sb)) - jb.STICK_GAP
            if d < 0:
                return None
            worst = min(worst, d)
        return worst

    def lane_distance(a, b):
        return min((math.sqrt(jb.segment_segment_sqr(a, b, ORIGIN, e)) for e in lane_ends), default=float("inf"))

    def clear_of_lanes(a, b):
        d = lane_distance(a, b) - (w + jb.SECTION)
        return None if d < 0 else d

    def room(a, b, lanes_too=True):
        worst = float("inf")
        for test in ((clear_of_clearings, clear_of_lanes, clear_of_laid) if lanes_too
                     else (clear_of_clearings, clear_of_laid)):
            m = test(a, b)
            if m is None:
                return None
            worst = min(worst, m)
        return worst

    def arm(centre, ax, kind, family):
        for k in range(jb.JACK_ARM_PRISMS):
            prisms.append((jb.add(centre, ax, jb.KNOT * 0.5 + jb.SEGMENT * (k + 0.5)), ax, stick_size, kind, family))

    def neg(v):
        return (-v[0], -v[1], -v[2])

    # Gates
    gates = 0
    gate_centres = []
    for k in range(lanes):
        if not gated_lane(k, p["gateEveryNthLane"]):
            continue
        axis = lane_axes[k]
        e1, e2 = basis(axis)
        c, r = clear[(1 if has_centre else 0) + k]
        frm, to = centre_radius, mag(c) - r
        for m in range(p["gatesPerLane"]):
            centre = scale(axis, gate_distance(frm, to, m, p["gatesPerLane"]))
            for ax in (axis, e1, e2):
                segs.append((jb.add(centre, ax, -jb.JACK_REACH), jb.add(centre, ax, jb.JACK_REACH)))
            prisms.append((centre, e2, knot, "shielded", "gate"))
            arm(centre, axis, "plain", "gate")
            arm(centre, neg(axis), "plain", "gate")
            for ax in (e1, neg(e1), e2, neg(e2)):
                arm(centre, ax, "shielded", "gate")
            gates += 1
            gate_centres.append((k, centre))

    # Rails
    pickets = 0
    picket_segs = []
    half = PICKET_LENGTH * 0.5
    for k in range(lanes):
        axis = lane_axes[k]
        for q in range(p["railsPerLane"]):
            off = rail_offset(axis, k, q, p["railsPerLane"], w)
            j = 0
            while True:
                along = picket_centre(centre_radius, j)
                j += 1
                a = jb.add(scale(axis, along - half), off)
                b = jb.add(scale(axis, along + half), off)
                if mag(b) > R:
                    break
                m = room(a, b, lanes_too=False)
                if m is None:
                    continue
                margins.append(min(m, R - mag(b)))
                segs.append((a, b))
                picket_segs.append((k, a, b))
                for n in range(PICKET_PRISMS):
                    prisms.append((jb.add(a, axis, jb.SEGMENT * (n + 0.5)), axis, stick_size, "plain", "rail"))
                pickets += 1

    # Loose jacks
    jacks = 0
    for j in range(p["looseJacks"]):
        for t in range(jb.STICK_TRIES):
            centre = jb.in_ball(jb.key(seed, LOOSE_JACK_KEY_BASE, j, t, 0), R - jb.JACK_REACH)
            x = jb.unit_from(jb.key(seed, LOOSE_JACK_KEY_BASE, j, t, 1))
            y, z = basis(x)
            worst, ok = float("inf"), True
            for ax in (x, y, z):
                m = room(jb.add(centre, ax, -jb.JACK_REACH), jb.add(centre, ax, jb.JACK_REACH))
                if m is None:
                    ok = False
                    break
                worst = min(worst, m)
            if not ok:
                continue
            margins.append(worst)
            for ax in (x, y, z):
                segs.append((jb.add(centre, ax, -jb.JACK_REACH), jb.add(centre, ax, jb.JACK_REACH)))
            prisms.append((centre, z, knot, "shielded", "jack"))
            for ax in (x, neg(x), y, neg(y), z, neg(z)):
                arm(centre, ax, "plain", "jack")
            jacks += 1
            break

    # Loose sticks
    sticks = thorned_sticks = ironwood_sticks = 0
    stick_segs = []
    band, core = p["thornBand"], p["ironwoodCoreRadius"]
    for s in range(p["looseSticks"]):
        for t in range(jb.STICK_TRIES):
            centre = jb.in_ball(jb.key(seed, LOOSE_STICK_KEY_BASE, s, t, 0), R - jb.STICK_HALF)
            axis = jb.unit_from(jb.key(seed, LOOSE_STICK_KEY_BASE, s, t, 1))
            a, b = jb.add(centre, axis, -jb.STICK_HALF), jb.add(centre, axis, jb.STICK_HALF)
            m = room(a, b)
            if m is None:
                continue
            ld = lane_distance(a, b)
            thorned = band > 0 and ld < w + band
            iron = core > 0 and jb.dot(centre, centre) < core * core
            if band > 0:
                m = min(m, abs(ld - (w + band)))
            if core > 0:
                m = min(m, abs(mag(centre) - core))
            margins.append(m)
            segs.append((a, b))
            stick_segs.append((a, b, thorned, iron))
            start = -jb.STICK_HALF + jb.SEGMENT * 0.5
            for k in range(jb.STICK_PRISMS):
                kind = "plain"
                if thorned and k in (0, jb.STICK_PRISMS - 1):
                    kind = "danger"
                elif iron and jb.IRONWOOD_FROM <= k < jb.IRONWOOD_TO:
                    kind = "shielded"
                prisms.append((jb.add(centre, axis, start + jb.SEGMENT * k), axis, stick_size, kind, "stick"))
            sticks += 1
            thorned_sticks += thorned
            ironwood_sticks += iron
            break

    census = {
        "prisms": len(prisms),
        "plain": sum(1 for q in prisms if q[3] == "plain"),
        "shielded": sum(1 for q in prisms if q[3] == "shielded"),
        "danger": sum(1 for q in prisms if q[3] == "danger"),
        "volume": sum(q[2][0] * q[2][1] * q[2][2] for q in prisms),
        "lanes": lanes, "gates": gates, "pickets": pickets, "railPrisms": pickets * PICKET_PRISMS,
        "sticks": sticks, "jacks": jacks, "thornedSticks": thorned_sticks, "ironwoodSticks": ironwood_sticks,
        "skippedSticks": p["looseSticks"] - sticks, "skippedJacks": p["looseJacks"] - jacks,
        "minMargin": min(margins) if margins else float("inf"),
    }
    return {"prisms": prisms, "clearings": clear, "lanes": list(zip(lane_axes, lane_ends)), "gateCentres": gate_centres,
            "picketSegs": picket_segs, "stickSegs": stick_segs, "segs": segs, "census": census}


def rung(i, label, seed, heap, centre, ring, ring_r, lanes, lane_r, rails, gate_nth, gates, sticks, jacks, band, core,
         blurb):
    return dict(i=i, label=label, seed=seed, heapRadius=heap, centreClearingRadius=centre, ringClearingCount=ring,
                ringClearingDistance=heap - ring_r - CLEARING_INSET, ringClearingRadius=ring_r, laneCount=lanes,
                laneRadius=lane_r, railsPerLane=rails, gateEveryNthLane=gate_nth, gatesPerLane=gates,
                looseSticks=sticks, looseJacks=jacks, thornBand=band, ironwoodCoreRadius=core,
                spawnRing=heap + 90, blurb=blurb)


# ── The ladder: one SpawnableRiposteYard prefab per intensity. I1 mostly helps; I4 mostly demands the read. ──
RUNGS = [
    rung(1, "The Lists", 9701, 420, 100, 4, 60, 4, 45, 4, 0, 0, 24, 0, 0, 0,
         "four wide lanes, each to a refuel clearing, four fuel rails round every lane, a thin scatter between"),
    rung(2, "Barriers", 9702, 500, 100, 4, 65, 4, 40, 3, 2, 1, 60, 4, 0, 0,
         "four lanes with three rails each; a shielded gate stands in every other lane"),
    rung(3, "Thorn Lists", 9703, 600, 90, 6, 65, 4, 34, 2, 1, 1, 120, 10, 70, 0,
         "lanes to four of six refuels, two rails each, a gate in every lane, red tips along the lane walls"),
    rung(4, "Ironwood", 9704, 700, 80, 8, 60, 3, 28, 1, 1, 2, 210, 20, 100, 300,
         "three narrow lanes for eight refuels, one rail each, two gates a lane, thorned walls, an ironwood core"),
]


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:12} prisms {c['prisms']:5} shielded {c['shielded']:4} danger {c['danger']:4} "
              f"volume {c['volume']:8.0f} lanes {c['lanes']} gates {c['gates']} pickets {c['pickets']} "
              f"({c['railPrisms']} rail prisms) sticks {c['sticks']}/{r['looseSticks']} "
              f"(thorned {c['thornedSticks']}, ironwood {c['ironwoodSticks']}) jacks {c['jacks']}/{r['looseJacks']} "
              f"margin {c['minMargin']:.3f}")
