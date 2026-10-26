#!/usr/bin/env python3
"""
The plans only TANDAVA: SEVERING adds (GameModes.TandavaSevering, Voyage 2; the fork of Tandava that carries PR #1046's
severing, wound memory and whale-jelly chimera - Assets/_Scripts/Controller/Arcade/TandavaSevering/TANDAVA_SEVERING.md).

The fork's creature climbs Tandava's own ladder, so its swarm config lists every plan Tandava ships (by guid - a tuning
pass on Tandava's plans reaches the fork too) and then these four, in this order (the PR's indices 51-54):

  severed, severed_feed      THE SEVERED (§3.11): the piece a clean cut parts from the body - a short Great Serpent
  chimera_whale, chimera_jelly   THE WHALE-JELLY CHIMERA (§3.13): grown by the hybrid NCA, one census, two shapes

This module builds them from Tandava's own anatomy (tandava_plans: the serpent body, the guard ring, the bake) without
editing it: the fork's variants are appended to tandava_plans.VARIANTS in this process only, after its forms, so every
ladder index stays put and tandava_plans.bake / validate treat them like any other plan (the armour rule included - the
Severed is bare only in the pose it eats in, as the Antlion is).

Run:  python3 Tools/Build/tandava_severing_plans.py [--write <dir>]
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tandava_plans as tp   # noqa: E402

# ───────────────────────────────────────────────────────────────── the Severed

# SEVERING (TANDAVA_SEVERING.md §3.11): a cut that parts the body in two sends the smaller piece off as its own creature -
# the hydra's rule, and the NCA lizard's (cut in two, it healed into two lizards; the 3D hybrid rule grew a split lizard
# back as conjoined twins). Whatever form it was cut from, the piece grows a serpent's head and becomes THE SEVERED: a
# short, fast serpent that hunts the pilots on its own, feeds to regrow, and swims back to rejoin the body it was cut
# from. One plan for every form, sized so the biggest piece a sever may take (SEVER_MAX_SHARE of the biggest body) fits
# it whole; a smaller piece wears it thin and fills in as it eats.
SEVERED = dict(nb=21, nt=6, nh=8, ring_r=1.9, wave=(26.0, 0.35, 3.4), shimmer=4, span=200.0)
SEVER_MAX_SHARE = 0.4      # TandavaSeveringDirectorSettings.SeverMaxShare: the biggest share of a form a piece may be
SEVERED_FORM, CHIMERA_FORM = 4, 5   # no rung of the ladder (forms 0-3 are Tandava's)


def severed(pose="travel"):
    """THE SEVERED: the Great Serpent's anatomy, shorter (21 body stations, a 6-ring rattle, an 8-plate hood). It has no
    coils - it eats, and lunges, in its strike pose, the hood out round its jaws as danger guards."""
    p = SEVERED
    settle = tp.FEED_SETTLE if pose != "travel" else 1.0
    wv = (p["wave"][0], p["wave"][1], p["wave"][2], settle)
    shimmer = set(range(2, p["nb"], p["shimmer"]))
    length = (p["nb"] + p["nt"] - 1) * tp.SPACING
    frame = tp._wave_frame(length, wv)
    units, length = tp._serpent_body(p["nb"], p["nt"], p["ring_r"], tp.SERPENT_TAIL_R, shimmer, frame)
    hood_r = max(4.3, tp.SPACING * p["nh"] / math.radians(p["span"]))
    hood = []
    for q in range(p["nh"]):
        ang = math.radians(90.0 - p["span"] / 2 + p["span"] * (q + 0.5) / p["nh"])

        def at(f, ang=ang):
            c, fwd, side = tp._spine_frame(1.3, length, f, wv)
            return tp.add(c, tp.add(tp.mul(side, hood_r * math.cos(ang)), [0.0, hood_r * math.sin(ang), 0.0]))
        hood.append(tp._unit_at(tp.CHARGE, 0, 2, at, lambda f: tp._spine_frame(1.3, length, f, wv)[1]))
    if pose == "feed":
        mouth = [3.0, 0.0, 0.0]
        return units + tp._guard_ring(hood, mouth, [1.0, 0.0, 0.0], hood_r + 6.5, 1), mouth
    return units + hood, None


# ───────────────────────────────────────────────────────────────── the chimera

CHIMERA_JSON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tandava_chimera.json")


def chimera(shape):
    """THE WHALE-JELLY CHIMERA: not drawn - GROWN. The hybrid NCA's whale was spliced onto its jelly and left to run,
    and the body it grew kept reshaping between a whale wrapped in a jelly shell with a bell at its head and a whale
    with a jelly stripe down its back; tandava_chimera_extract.py took one swim cycle of each moment and spread one
    census over its skin. `shape` is "whale" or "jelly": the creature flips between the two (TandavaSeveringPhase.Chimera),
    a pose change, never a molt. Slots split the body at its centre height: above is the back, below the belly."""
    src = json.load(open(CHIMERA_JSON, encoding="utf-8"))["shapes"][shape]
    pos = src["pos"]
    units = []
    for k, e in enumerate(src["elem"]):
        u = tp.Unit(e, 0 if pos[0][k][1] >= 0.0 else 1, 2 if e == tp.CHARGE else 0)
        for f in range(tp.FRAMES):
            u.pos[f] = list(pos[f][k])
            u.face[f] = [1.0, 0.0, 0.0]
        units.append(u)
    return units, None


# ───────────────────────────────────────────────────────────────── the catalogue

FORK_VARIANTS = [("severed", SEVERED_FORM, "the Severed", lambda pose: severed(pose), True)] + [
    (f"chimera_{shape}", CHIMERA_FORM, "Whale-Jelly Chimera", lambda pose, shape=shape: chimera(shape), False)
    for shape in ("whale", "jelly")]
LADDER_KEYS = tp.plan_keys()   # Tandava's, read BEFORE the fork's variants join: the swarm config's first entries
if not any(v[0] == "severed" for v in tp.VARIANTS):
    tp.VARIANTS.extend(FORK_VARIANTS)


def plan_keys():
    """The fork's own plans in swarm-config order (after every one of LADDER_KEYS)."""
    return [k for k in tp.plan_keys() if k not in LADDER_KEYS]


def bake_all():
    return {k: tp.bake(k) for k in plan_keys()}


def validate(plans):
    """Every promise the fork's plans make, judged beside Tandava's ladder (tandava_plans.validate's per-plan rules and
    its ladder rules, minus the one that says the set is exactly the four forms) - failure strings, empty = all hold."""
    ladder = tp.bake_all()
    ladder = {k: v for k, v in ladder.items() if k in LADDER_KEYS}
    every = dict(ladder, **plans)
    errors = [e for e in tp.validate(every) if not e.startswith("the plans make forms")]
    forms = sorted({p["form"] for p in plans.values()})
    if forms != [SEVERED_FORM, CHIMERA_FORM]:
        errors.append(f"the fork's plans make forms {forms}, not the Severed and the chimera")
    # the chimera's two shapes are ONE body re-arranged (turning is a pose change), and it sits between the rungs it
    # bridges: no smaller than the Many-Headed Serpent it rises from, no bigger than the Dance it rises to
    wj, jj = plans.get("chimera_whale"), plans.get("chimera_jelly")
    if wj is None or jj is None or tp.census(wj) != tp.census(jj):
        errors.append("the chimera's whale and jelly shapes must carry one census (a turn is a pose change, never a molt)")
    else:
        lo = max(p["n"] for p in ladder.values() if p["form"] == 1)
        hi = min(p["n"] for p in ladder.values() if p["form"] == 2)
        if not lo <= wj["n"] <= hi:
            errors.append(f"the chimera ({wj['n']} units) is not between the Many-Headed Serpent ({lo}) and the Dance ({hi})")
    # the Severed fits the biggest piece a sever may take whole (TandavaSeveringDirectorSettings.SeverMaxShare)
    sev = plans.get("severed")
    biggest = max(p["n"] for p in ladder.values())
    if sev is None or sev["n"] < SEVER_MAX_SHARE * biggest:
        errors.append(f"the Severed ({sev['n'] if sev else 0} units) cannot hold {SEVER_MAX_SHARE} of the biggest body ({biggest})")
    smallest = min(p["n"] for k, p in ladder.items() if p["form"] == 0 and tp.split_key(k)[1] == "travel")
    if sev is not None and sev["n"] > 0.75 * smallest:
        errors.append("the Severed is not clearly smaller than the smallest Great Serpent - it should read as a piece, not a twin")
    return errors


def main():
    plans = bake_all()
    if len(sys.argv) > 2 and sys.argv[1] == "--write":   # harness use: write the JSONs to a scratch dir
        os.makedirs(sys.argv[2], exist_ok=True)
        for kind, p in plans.items():
            with open(os.path.join(sys.argv[2], f"SwarmPlan_tandava_{kind}.json"), "w", encoding="utf-8") as fh:
                fh.write(tp.dumps(p))
    for kind, p in plans.items():
        m = tp.census(p)
        print(f"{kind:16s} n={p['n']:3d}  C/M/S/T {m[0]:3d}/{m[1]:3d}/{m[2]:3d}/{m[3]:3d}  steps {p['frameSteps']}")
    errors = validate(plans)
    for e in errors:
        print("FAIL:", e)
    print("tandava severing plans: " + ("OK" if not errors else f"{len(errors)} failure(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
