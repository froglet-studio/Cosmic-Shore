#!/usr/bin/env python3
"""
Prove that NOTHING shipped references any asset under a given third-party tree.

WHY A TOOL AND NOT A GREP. Every vendored-SDK removal in Docs/THIRD_PARTY_DECISIONS.md needs
the same evidence before its deletion commit, and the obvious one-liner gets it wrong twice:

  * `grep -rl <guid> | head -1` does not establish who OWNS a guid. A `.meta` can carry an
    `externalObjects` remap pointing INTO another asset, so the first hit is a plausible false
    positive (CLAUDE.md records this costing two passes of Rhino jet placement). Ownership is
    asserted here: exactly one `.meta` may contain the guid as a whole line.
  * Checking the assets you happen to have NOTICED proves only those assets. This sweeps every
    guid owned anywhere under the tree, so an asset nobody listed cannot slip through.

It is a READER: it writes nothing and deletes nothing.

Usage:
    python3 Tools/Build/check_vendor_tree_references.py                 # the shipped roster
    python3 Tools/Build/check_vendor_tree_references.py --tree Assets/Wwise
    python3 Tools/Build/check_vendor_tree_references.py --self-test     # negative control
"""

import argparse
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Trees whose removal is already decided, with the holders each is allowed to keep.
# An exemption is a PATH PREFIX and must name why, so it cannot quietly become permanent.
ROSTER = [
    ("Assets/NiceVibrations/Demo", [
        # Contents are an open decision (LAUNCH_BLOCKER_INDEX.md B3) and the folder ships
        # nothing; deliberately not re-pointed by the row-5 swap.
        "Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/",
    ]),
    ("Assets/NiceVibrations/HapticSamples", [
        "Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/",
    ]),
]

GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
HOLDER_GLOBS = ["*.unity", "*.prefab", "*.asset", "*.mat", "*.controller",
                "*.playable", "*.spriteatlas", "*.shadergraph", "*.shadersubgraph"]


def _grep(pattern, includes, whole_line=False):
    cmd = ["grep", "-rl"] + (["-x"] if whole_line else []) + [pattern, "Assets"]
    for g in includes:
        cmd.append("--include=" + g)
    out = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True).stdout
    return [p for p in out.split("\n") if p]


def guids_owned_under(tree):
    """guid -> the asset that owns it, for every .meta under `tree`."""
    owned = {}
    for dp, _, fns in os.walk(os.path.join(REPO, tree)):
        for fn in fns:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(dp, fn)
            with open(p, encoding="utf-8", errors="ignore") as f:
                m = GUID_LINE.search(f.read())
            if m:
                owned[m.group(1)] = os.path.relpath(p, REPO)[:-5]
    return owned


def check(tree, exemptions, verbose=False):
    if not os.path.isdir(os.path.join(REPO, tree)):
        print(f"SKIP {tree} (not present - already deleted?)")
        return 0, 0

    owned = guids_owned_under(tree)
    leaks, exempt_hits = [], 0
    for guid, src in sorted(owned.items()):
        # Ownership: exactly one .meta may claim this guid, and it must be the one under the tree.
        claims = _grep("guid: " + guid, ["*.meta"], whole_line=True)
        if len(claims) != 1 or claims[0] != src + ".meta":
            print(f"  AMBIGUOUS {src}: guid claimed by {claims}")
            leaks.append((src, "<ambiguous ownership>"))
            continue
        for h in _grep("guid: " + guid, HOLDER_GLOBS):
            if h.startswith(tree + "/"):
                continue                                  # the vendor tree citing itself
            if any(h.startswith(e) for e in exemptions):
                exempt_hits += 1
                continue
            leaks.append((src, h))

    if leaks:
        print(f"FAIL {tree}: {len(leaks)} shipped reference(s)")
        for src, h in leaks:
            print(f"    {os.path.basename(src)}  <-  {h}")
    else:
        extra = f", {exempt_hits} exempt" if exempt_hits else ""
        print(f"OK   {tree}: {len(owned)} guids owned, 0 shipped references{extra}")
    return len(leaks), len(owned)


def self_test():
    """Negative control: the checker must FAIL on a tree that really is still referenced.

    Without this the tool is indistinguishable from one that always prints OK - and a gate
    nobody has watched fail is a gate nobody should trust (CLAUDE.md).
    """
    print("-- self-test: a tree that IS referenced must fail --")
    leaks, owned = check("Assets/Plugins/FMOD", [])       # the whole audio layer; must not be clean
    ok_neg = leaks > 0
    print(f"   {'PASS' if ok_neg else 'FAIL'}: expected leaks from a live tree, got {leaks}")
    print("-- self-test: exemptions must actually exempt --")
    l2, _ = check("Assets/NiceVibrations/HapticSamples", [])   # no exemption -> MIgration holders leak
    ok_ex = l2 > 0
    print(f"   {'PASS' if ok_ex else 'FAIL'}: expected the MIgration_Prefabs holders to show, got {l2}")
    return 0 if (ok_neg and ok_ex) else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tree", help="check one tree instead of the roster")
    ap.add_argument("--self-test", action="store_true", help="negative control")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    jobs = [(args.tree, [])] if args.tree else ROSTER
    total = 0
    for tree, exemptions in jobs:
        leaks, _ = check(tree, exemptions)
        total += leaks
    print(("\nFAIL: %d shipped reference(s) remain" % total) if total
          else "\nOK: every tree on the roster is unreferenced by shipped content")
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
