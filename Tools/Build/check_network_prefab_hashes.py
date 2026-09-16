#!/usr/bin/env python3
"""Fail on two PREFABS sharing a Netcode GlobalObjectIdHash.

WHY THIS EXISTS
---------------
Netcode keys its network-prefab table on `GlobalObjectIdHash` alone. Unity derives that hash from
the prefab's GUID and only regenerates it in `NetworkObject.OnValidate` -- so a prefab created by
COPYING another one on disk carries the original's hash, and two registered prefabs then collide.
One silently wins and the other can never spawn.

Caught once already: Gibbon.prefab was authored by copying Squirrel.prefab and carried its hash
verbatim, which would have made one of the two unspawnable.

READ THE FIELD NAME, NOT A SUBSTRING. `InScenePlacedSourceGlobalObjectIdHash` ENDS with the same
identifier, so an unanchored /GlobalObjectIdHash: (\d+)/ matches it too -- and that field really
is shared between prefabs (Scarab and Sparrow both carry 1299232740 because one was copied from
the other). It is inert on a prefab asset, so reading it as the real hash invents a collision that
does not exist. That mistake was made while writing this file; the anchored regex below is the fix
and this paragraph is why it is anchored.

The failure is invisible in review (the YAML looks fine), invisible to the C# gates, and only
shows up as "that vessel does not spawn in multiplayer".

SCENES ARE NOT CHECKED, deliberately: an in-scene NetworkObject is keyed by
(GlobalObjectIdHash, sceneHandle), so the same hash appearing in several scenes is legal and
common -- 13 such pairs exist today. Only prefab-vs-prefab is fatal.

RATCHET
-------
BASELINE holds collisions that already shipped, so this passes today and fails on anything NEW.
A baselined pair is NOT reviewed-and-accepted -- it is a known bug with a name.

    python3 Tools/Build/check_network_prefab_hashes.py [--self-test]
"""
import os, re, sys
from collections import defaultdict

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets")
HASH_RE = re.compile(r"^\s*GlobalObjectIdHash: (\d+)\s*$", re.M)

# No known-bad pairs: the tree is clean, so this check is STRICT and any hit is a new defect.
BASELINE = set()


def collisions(root):
    seen = defaultdict(set)
    for dp, _, fs in os.walk(root):
        for f in fs:
            if not f.endswith(".prefab"):
                continue
            try:
                with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                    text = fh.read()
            except OSError:
                continue
            for m in HASH_RE.finditer(text):
                if m.group(1) != "0":
                    seen[int(m.group(1))].add(f)
    return {h: tuple(sorted(v)) for h, v in seen.items() if len(v) > 1}


def self_test():
    """Negative control: the check must FIRE on a synthetic collision and stay quiet without one."""
    import tempfile, shutil
    tmp = tempfile.mkdtemp()
    try:
        body = "NetworkObject:\n  GlobalObjectIdHash: 4242424242\n"
        for name in ("A.prefab", "B.prefab"):
            with open(os.path.join(tmp, name), "w") as fh:
                fh.write(body)
        got = collisions(tmp)
        assert got == {4242424242: ("A.prefab", "B.prefab")}, f"did not fire: {got}"
        os.remove(os.path.join(tmp, "B.prefab"))
        assert collisions(tmp) == {}, "fired on a clean tree"
        # a ZERO hash is 'not yet generated', never a collision
        with open(os.path.join(tmp, "C.prefab"), "w") as fh:
            fh.write("NetworkObject:\n  GlobalObjectIdHash: 0\n")
        with open(os.path.join(tmp, "D.prefab"), "w") as fh:
            fh.write("NetworkObject:\n  GlobalObjectIdHash: 0\n")
        assert collisions(tmp) == {}, "fired on zero hashes"
        print("network-prefab-hash self-test: OK (fires on a collision, quiet otherwise)")
        return 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    if "--self-test" in sys.argv:
        return self_test()
    found = collisions(ASSETS)
    new = {h: v for h, v in found.items() if (h, v) not in BASELINE}
    stale = [b for b in BASELINE if b[0] not in found]
    for h, v in sorted(new.items()):
        print(f"ERROR: GlobalObjectIdHash {h} is shared by {' and '.join(v)}.")
        print("       Netcode keys its prefab table on this hash, so only one of them can ever")
        print("       spawn. A prefab copied on disk keeps the original's hash; recompute it as")
        print("       XXHash32('GlobalObjectId_V1-1-<prefab guid>-<NetworkObject fileID>-0'),")
        print("       or open the prefab in the editor and save so OnValidate regenerates it.")
    for b in stale:
        print(f"NOTE: baselined collision {b[0]} {b[1]} is gone - remove it from BASELINE.")
    if new:
        return 1
    print(f"network-prefab-hash check: OK ({len(found)} collision(s) across all prefabs)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
