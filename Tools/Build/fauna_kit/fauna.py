#!/usr/bin/env python3
"""One front door for making a creature (the /fauna-maker skill): start one, check it, draw it.

    python3 Tools/Build/fauna_kit/fauna.py new <name>            # specs/<name>.py from the working template
    python3 Tools/Build/fauna_kit/fauna.py check <name>          # laws, placement table, mesh budget; a FIX for every failure
    python3 Tools/Build/fauna_kit/fauna.py check <name> --silhouette   # ... and the outline gate (about four minutes)
    python3 Tools/Build/fauna_kit/fauna.py sheet <name> <png>    # the body from the side, from above and from ahead

It runs the kit's own gates (celestial.py, spindle.py, silhouette.py) and adds what a maker needs between them: where
each prism sits against the numbers it must meet, and what to change when a law says no. Exit 0 means: go to the lab.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import body as BODY  # noqa: E402
import celestial as C  # noqa: E402
import dossier as D  # noqa: E402
import spindle as SP  # noqa: E402

FIX = {
    "L1": "Something is ahead of the crystal. Move the prism back (its front must be behind z = -1.78 and 4.33 from the "
          "crystal's centre), or for WIRE that should lead (a bell, lobes) add \"lead\": <how far> to SPEC (a quarter of the length at most).",
    "L2": "A limb is not joined. A root limb (parent None) starts within 3.2 of [0,0,0]; any other starts within 1.5 of its "
          "parent's curve: make its first point a point ON the parent (K.along(parent, t)[0]).",
    "L3": "A prism is off its limb. Take its `at` from K.along(limb, t) for the limb named in `spindle`, and keep it within 2.5.",
    "L4": "Add health prisms until there are three: plates on the limbs furthest from the crystal first.",
    "L5": "The sting: kind danger, role stinger, at least 4 long and 6 times its thickness, on a limb with no child limbs, "
          "and nothing may reach further back than it. Or a side is outside 0.5 to 10: resize it.",
    "L6": "The body is not the same N times round its axis. Write each repeated part ONCE inside `for k, a in enumerate(K.angles(N))` "
          "using P(radius, z); a part on the axis needs x = y = 0 exactly.",
    "L7": "Two prisms overlap. Move one along its limb (a different t) or make it shorter.",
    "L8": "Use \"elements\": K.elements(prisms) and do not edit it.",
    "L9": "Rename the part: no leg, belly, back, eye, antenna, foot, head, torso, thorax, abdomen or wing in a name.",
    "L10": "Write the premise, and a role of herbivore / predator / pollinator / omnivore.",
    "L11": "possession needs top_speed, mouth_reach and attack. `whip` needs three or more limbs growing from ONE limb; "
           "with fewer the game lunges, so say `lunge`.",
}


def placement(spec):
    """Each prism against the numbers it must meet: the table a maker reads to learn where things go."""
    spindles = {s["name"]: s for s in spec["spindles"]}
    rows = []
    for p in spec["prisms"]:
        heart = C.box_point_dist(p, [0, 0, 0])
        front = max(c[2] for c in C.box_corners(p))
        limb = spindles.get(p.get("spindle"))
        off = min(C.line_dist(c, limb) for c in C.box_corners(p) + [p["at"]]) if limb else float("nan")
        ok = heart >= 4.325 - 1e-6 and front <= -1.775 and off <= 2.5 and all(0.5 <= v <= 10 for v in p["size"])
        rows.append((p["name"], p["kind"], p.get("spindle"), heart, front, off, ok))
    return rows


def check(name, silhouette=False):
    spec = C.load(name)
    errs = C.check(spec)
    print(f"== {spec['species']}: laws")
    for e in errs:
        print("  FAIL", e)
        print("   FIX", FIX.get(e.split(" ")[0], "see /fauna-creation section 2"))
    print("  celestial" if not errs else f"  {len(errs)} law(s) broken")
    print("== placement (a prism must be 4.33 or more from the crystal, its front behind z -1.78, within 2.5 of its limb)")
    print(f"  {'prism':<14}{'kind':<8}{'limb':<12}{'from crystal':>13}{'front z':>9}{'off limb':>9}")
    for n, kind, limb, heart, front, off, ok in placement(spec):
        print(f"  {n:<14}{kind:<8}{str(limb):<12}{heart:>13.2f}{front:>9.2f}{off:>9.2f}{'' if ok else '   <-- fix'}")
    bad = bool(errs)
    if any(s.get("mesh") == "grown" for s in spec["spindles"]) and not errs:
        rows = SP.creature_mesh(name)
        tv = sum(r[2] for r in rows)
        print(f"== mesh: {tv} vertices in {len(rows)} limbs (the bar is {SP.CREATURE_VERTS})")
        if tv > SP.CREATURE_VERTS:
            bad = True
            for r in sorted(rows, key=lambda r: -r[2])[:3]:
                print(f"  heavy: {r[0]} ({r[1]}) {r[2]}")
            print("   FIX on the heavy limbs, in this order: section=2, a larger step, fewer barbs, fewer strands (/spindle section 4)")
    if silhouette and not bad:
        import subprocess
        out = subprocess.run([sys.executable, os.path.join(HERE, "silhouette.py"), "--check"], capture_output=True, text=True).stdout
        line = next((ln for ln in out.splitlines() if ln.startswith(spec["species"] + " ")), "(not in the lab roster: add it to lab_export.NEW_SPECIES first)")
        print("== silhouette:", " ".join(line.split()))
        if "TOO ALIKE" in line:
            bad = True
            print("   FIX change the OUTLINE, not the detail: widen or lengthen the whole body, open the limbs out, or change the plan.")
    print("RESULT:", "FAIL" if bad else "PASS: add it to lab_export.NEW_SPECIES, run lab_export.py, then verify_lab.cjs")
    return 1 if bad else 0


def main(argv):
    if len(argv) >= 2 and argv[0] == "new":
        name = argv[1].lower()
        dst = os.path.join(HERE, "specs", name + ".py")
        if os.path.exists(dst):
            print("exists:", dst)
            return 1
        open(dst, "w").write(open(os.path.join(HERE, "specs", "_template.py")).read().replace("SPECIESNAME", name.capitalize()))
        print("wrote", os.path.relpath(dst, D.REPO), "- a working creature. Edit it, then: fauna.py check", name)
        return 0
    if len(argv) >= 2 and argv[0] == "check":
        return check(argv[1], "--silhouette" in argv)
    if len(argv) >= 3 and argv[0] == "sheet":
        print("sheet:", D.sheet([(C.load(argv[1])["species"], BODY.from_spec(C.load(argv[1])))], argv[2], "built from its spec", size=520))
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
