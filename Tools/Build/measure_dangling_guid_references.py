#!/usr/bin/env python3
"""Did removing or moving an asset leave a reference pointing at nothing?

Counts every guid reference in the project that NO `.meta` on disk owns, and --
the part that makes it useful -- differences two snapshots so a removal can be
proved not to have orphaned anything.

Written for `Docs/LAUNCH_BLOCKER_INDEX.md`, whose rows A1-A5 and E1-E2 are each
an asset removal needing exactly this proof. A1-A3 are done; the rest are not.

THE ABSOLUTE COUNT IS NOT THE SIGNAL. The DELTA IS.
--------------------------------------------------
A large unowned count is normal and not a defect: package code has no `.meta` in
a clone (`Library/PackageCache` is not checked in), so every `m_Script` pointing
into a package counts as unowned. On this project that is ~440 distinct guids
before any change -- `TMP_FontAsset` alone is referenced by 21 font assets and
resolves from the builtin `com.unity.ugui`. So never read the total as a bug
count. Take a snapshot, make the change, take another, and diff them.

A MOVED REFERRER IS NOT A LOST REFERENCE.
-----------------------------------------
An edge is keyed by the referrer's OWN guid, never by its path, so relocating a
file contributes no edge change whatsoever -- which is what makes this usable on
a branch that both MOVES and REMOVES assets. A path-keyed first cut reported 24
phantom "lost" references for three demo scenes that had simply been moved one
folder deeper.

AND A FALLING COUNT IS NOT A PROOF EITHER.
------------------------------------------
Removing a folder removes its referrers too, so the total drops -- and a NEW
dangle can hide behind a larger number of removals. `--diff` therefore reports
the SET difference in both directions and, for every edge that disappeared,
whether its referrer was inside the path you removed. The proof of a clean
removal is:

    new unowned guids introduced .................. 0
    removed edges whose referrer was OUTSIDE ...... 0

The second line is the one that matters: it says nothing the removal did not
contain lost a reference.

WHAT IT CANNOT SEE
------------------
The same two blind spots every guid pass has, and neither is closed by this
tool -- close them by hand:

  * LOAD BY NAME. `Resources.Load("Fonts & Materials/Bangers SDF")` is a string.
    Search the Resources-relative key of every asset you are removing as a
    literal, and remember TMP's `<font="X">` / `<sprite="X">` / `<gradient="X">`
    rich-text tags resolve by name too.
  * C# TYPE references. A script's guid appears in a prefab, but a `.cs` naming
    the class does not. Grep every removed class name word-boundary.

USAGE
    measure_dangling_guid_references.py --save before.json
    ...make the change...
    measure_dangling_guid_references.py --save after.json
    measure_dangling_guid_references.py --diff before.json after.json [--removed-under PATH]...
    measure_dangling_guid_references.py --self-test
"""
import argparse, collections, json, os, re, sys

ROOTS = ["Assets", "ProjectSettings", "Packages"]
OWN_RE = re.compile(rb"^guid: ([0-9a-f]{32})\s*$", re.M)
REF_RE = re.compile(rb"guid: ([0-9a-f]{32})")
# Text asset formats that carry guid references. .meta is included because an
# importer's externalObjects remap is a real edge (see the index's method note).
SCAN_EXT = {
    ".unity", ".prefab", ".asset", ".mat", ".controller", ".shadergraph", ".meta",
    ".playable", ".anim", ".overrideController", ".spriteatlas", ".shadersubgraph",
    ".signal", ".mixer", ".physicMaterial", ".guiskin", ".fontsettings",
    ".terrainlayer", ".preset", ".inputactions", ".asmdef", ".lighting",
}


def walk():
    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, _, filenames in os.walk(root):
            for fn in filenames:
                yield os.path.join(dirpath, fn)


def measure():
    """-> (owned guid set, {unowned guid: sorted referrer paths})"""
    files = list(walk())

    owned = set()
    for p in files:
        if not p.endswith(".meta"):
            continue
        try:
            m = OWN_RE.search(open(p, "rb").read())
        except OSError:
            continue
        if m:
            owned.add(m.group(1).decode())

    # A referrer is identified by its OWN guid where it has one, so that MOVING a
    # file is not mistaken for losing a reference and gaining a new one. Only
    # files with no .meta (ProjectSettings/*) fall back to their path.
    def referrer_id(path):
        meta = path if path.endswith(".meta") else path + ".meta"
        try:
            m = OWN_RE.search(open(meta, "rb").read())
        except OSError:
            return "path:" + path
        return ("guid:" + m.group(1).decode()) if m else "path:" + path

    unowned = collections.defaultdict(dict)
    for p in files:
        if os.path.splitext(p)[1] not in SCAN_EXT:
            continue
        try:
            data = open(p, "rb").read()
        except OSError:
            continue
        self_guid = None
        if p.endswith(".meta"):
            m = OWN_RE.search(data)
            self_guid = m.group(1).decode() if m else None
        rid = referrer_id(p)
        for g in {x.decode() for x in REF_RE.findall(data)}:
            if g != self_guid and g not in owned:
                unowned[g][rid] = p
    return owned, {g: dict(sorted(v.items())) for g, v in unowned.items()}


def snapshot():
    owned, unowned = measure()
    return {"owned_count": len(owned), "unowned": unowned}


def report(snap, label):
    edges = sum(len(v) for v in snap["unowned"].values())
    print(f"[{label}] guids owned on disk .................. {snap['owned_count']}")
    print(f"[{label}] distinct UNOWNED guids referenced ..... {len(snap['unowned'])}")
    print(f"[{label}] reference edges to unowned guids ...... {edges}")


def diff(before, after, removed_under=None):
    # `removed_under` is a LIST of paths, because one change can remove more than one
    # tree: the branch that retired the two unlicensed vendor packs removed
    # `Assets/PrimitivePlus/` and `Assets/Shift - Complete Sci-Fi UI/` together, and
    # naming only one reported the other's own internal referrers as losses.
    removed_under = ([removed_under] if isinstance(removed_under, str)
                     else list(removed_under or []))
    bu, au = before["unowned"], after["unowned"]
    new_guids = sorted(set(au) - set(bu))
    # Edge identity is (target guid, referrer IDENTITY) -- so a file that merely
    # moved contributes no edge change at all.
    eb = {(g, rid) for g, m in bu.items() for rid in m}
    ea = {(g, rid) for g, m in au.items() for rid in m}
    new_edges, gone = sorted(ea - eb), sorted(eb - ea)

    print(f"guids owned on disk        {before['owned_count']} -> {after['owned_count']}")
    print(f"distinct unowned guids     {len(bu)} -> {len(au)}")
    print(f"reference edges            {len(eb)} -> {len(ea)}")
    print()
    print(f"NEW unowned guids introduced ............ {len(new_guids)}")
    for g in new_guids:
        print(f"    {g}")
        for rid, path in au[g].items():
            print(f"        <- {path}")
    print(f"NEW (guid -> referrer) edges ............ {len(new_edges)}")
    for g, rid in new_edges[:40]:
        print(f"    {g}  <- {au[g][rid]}")
    print(f"edges removed ........................... {len(gone)}")

    outside = gone
    if removed_under:
        keys = [os.path.normpath(k) for k in removed_under]
        outside = [(g, rid) for g, rid in gone
                   if not any(os.path.normpath(bu[g][rid]).startswith(k) for k in keys)]
        shown = ", ".join(repr(k) for k in removed_under)
        print(f"  of which the referrer was NOT under {shown}: {len(outside)}")
        for g, rid in outside[:40]:
            print(f"    {g}  <- {bu[g][rid]}")

    clean = not new_guids and (not removed_under or not outside)
    print()
    print("VERDICT:", "clean — nothing outside the change lost a reference" if clean
          else "NOT CLEAN — read the lines above")
    return 0 if clean else 1


def self_test():
    """Negative control: a green gate is only evidence if you can name a failure
    it produced. Inject a reference to a guid nothing owns and require a catch."""
    ok = True
    base = snapshot()
    fake = "deadbeef" * 4
    if fake in base["unowned"]:
        print("self-test SKIPPED: the sentinel guid is genuinely present")
        return 0

    probe = os.path.join("Assets", "__dangle_selftest__.asset")
    try:
        with open(probe, "w") as fh:
            fh.write("%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n"
                     f"  m_Script: {{fileID: 11500000, guid: {fake}, type: 3}}\n")
        after = snapshot()
        if fake not in after["unowned"]:
            print("self-test FAILED: injected unowned guid was NOT detected"); ok = False
        elif ("path:" + probe) not in after["unowned"][fake]:
            print("self-test FAILED: detected, but not attributed to the probe file"); ok = False
        if ok and diff(base, after) == 0:
            print("self-test FAILED: --diff called an injected dangle clean"); ok = False
    finally:
        if os.path.exists(probe):
            os.remove(probe)

    restored = snapshot()
    if fake in restored["unowned"]:
        print("self-test FAILED: probe not cleaned up"); ok = False
    print("self-test OK — an injected dangling reference is detected, attributed, "
          "and reported NOT CLEAN" if ok else "self-test FAILED")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--save", metavar="OUT", help="write a snapshot to this JSON file")
    ap.add_argument("--diff", nargs=2, metavar=("BEFORE", "AFTER"),
                    help="difference two snapshots; exits 1 if not clean")
    ap.add_argument("--removed-under", metavar="PATH", action="append",
                    help="with --diff: a path the change removed, so removed edges can be "
                         "split by whether their referrer was inside it. Repeatable — a "
                         "change that removes two trees must name both, or each tree's own "
                         "internal referrers are reported as losses from the other.")
    ap.add_argument("--self-test", action="store_true",
                    help="negative control: inject a dangling reference and require a catch")
    args = ap.parse_args()

    if args.self_test:
        return self_test()
    if args.diff:
        with open(args.diff[0]) as a, open(args.diff[1]) as b:
            return diff(json.load(a), json.load(b), args.removed_under)

    snap = snapshot()
    report(snap, "now")
    if args.save:
        json.dump(snap, open(args.save, "w"), indent=1, sort_keys=True)
        print(f"[now] snapshot -> {args.save}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
