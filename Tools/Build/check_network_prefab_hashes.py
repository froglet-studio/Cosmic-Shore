#!/usr/bin/env python3
r"""Fail on two PREFABS sharing a Netcode GlobalObjectIdHash, or on a prefab whose committed hash
is not the one Unity computes for it (a STALE hash).

WHY THIS EXISTS
---------------
Netcode keys its network-prefab table on `GlobalObjectIdHash` alone. Unity derives that hash from
the prefab's GUID and only regenerates it in `NetworkObject.OnValidate` -- so a prefab created by
COPYING another one on disk carries the original's hash, and two registered prefabs then collide.
One silently wins and the other can never spawn.

Caught once already: Gibbon.prefab (branch cece/hopeful-bardeen-rmqhmd, where this gate was
written) was authored by copying Squirrel.prefab and carried its hash verbatim, which would have
made one of the two unspawnable. Thresher.prefab is the same kind of copy and is the first such prefab
to land on bleeding-edge alongside this gate.

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

STALE HASHES BREAK EVERY JOIN (B35, Docs/PartySystem/BUGS.md)
--------------------------------------------------------------
With ForceSamePrefabs on, the connection handshake hashes the sorted GlobalObjectIdHash of every
registered prefab, and the host refuses a client whose hash differs ("NetworkConfig mismatch").
The hash each EDITOR uses is the in-memory one, and `NetworkObject.OnValidate` only regenerates it
in edit mode: a prefab first loaded while entering Play keeps the committed value. So when the
committed value is wrong, an editor that happened to touch the prefab in edit mode (selected it,
opened a scene holding it) uses the right hash and one that did not uses the wrong one -- two
editors on the same commit then fail every party join. Eleven of the 31 registered prefabs (and
AstroLeagueBall at 0) carried a wrong value until 2026-10-24. The check fails on any of them;
`--fix` writes the value Unity would write.

RATCHET
-------
BASELINE holds collisions that already shipped, so this passes today and fails on anything NEW.
A baselined pair is NOT reviewed-and-accepted -- it is a known bug with a name.

    python3 Tools/Build/check_network_prefab_hashes.py [--fix] [--self-test]
"""
import os, re, sys
from collections import defaultdict

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets")
HASH_RE = re.compile(r"^\s*GlobalObjectIdHash: (\d+)\s*$", re.M)

# No known-bad pairs: the tree is clean, so this check is STRICT and any hit is a new defect.
BASELINE = set()


# One top-level component block of a prefab file: "--- !u!114 &<fileID>" up to the next block.
BLOCK_RE = re.compile(r"^--- !u!114 &(-?\d+)( stripped)?\n(.*?)(?=^--- |\Z)", re.M | re.S)
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.M)

_P1, _P2, _P3, _P4, _P5 = 2654435761, 2246822519, 3266489917, 668265263, 374761393
_M = 0xFFFFFFFF


def _rotl(x, r):
    return ((x << r) | (x >> (32 - r))) & _M


def xxh32(data, seed=0):
    """XXHash32, the hash Netcode's `string.Hash32()` applies to a GlobalObjectId string."""
    n, i = len(data), 0
    if n >= 16:
        v = [(seed + _P1 + _P2) & _M, (seed + _P2) & _M, seed & _M, (seed - _P1) & _M]
        while i <= n - 16:
            for k in range(4):
                lane = int.from_bytes(data[i:i + 4], "little")
                v[k] = (_rotl((v[k] + lane * _P2) & _M, 13) * _P1) & _M
                i += 4
        h = (_rotl(v[0], 1) + _rotl(v[1], 7) + _rotl(v[2], 12) + _rotl(v[3], 18)) & _M
    else:
        h = (seed + _P5) & _M
    h = (h + n) & _M
    while i + 4 <= n:
        h = (_rotl((h + int.from_bytes(data[i:i + 4], "little") * _P3) & _M, 17) * _P4) & _M
        i += 4
    while i < n:
        h = (_rotl((h + data[i] * _P5) & _M, 11) * _P1) & _M
        i += 1
    h ^= h >> 15
    h = (h * _P2) & _M
    h ^= h >> 13
    h = (h * _P3) & _M
    h ^= h >> 16
    return h


def expected_hash(guid, file_id):
    """What NetworkObject.OnValidate writes: GlobalObjectId.GetGlobalObjectIdSlow(this).ToString().Hash32()
    for a component of a prefab asset (identifier type 1, no prefab-instance part)."""
    return xxh32(f"GlobalObjectId_V1-1-{guid}-{file_id}-0".encode())


def stale_hashes(root):
    """Every NetworkObject defined in a prefab file whose committed GlobalObjectIdHash is not the value
    Unity computes. Components that come from a nested prefab (stripped, or carrying a
    m_PrefabInstance) take their hash from their source prefab and are skipped."""
    out = []
    for dp, _, fs in os.walk(root):
        for f in fs:
            if not f.endswith(".prefab"):
                continue
            path = os.path.join(dp, f)
            try:
                with open(path, encoding="utf-8", errors="ignore") as fh:
                    text = fh.read()
                with open(path + ".meta", encoding="utf-8", errors="ignore") as fh:
                    g = GUID_RE.search(fh.read())
            except OSError:
                continue
            if not g or "GlobalObjectIdHash" not in text:
                continue
            for m in BLOCK_RE.finditer(text):
                body = m.group(3)
                h = HASH_RE.search(body)
                if not h or m.group(2) or "m_PrefabInstance: {fileID: 0}" not in body:
                    continue
                want = expected_hash(g.group(1), m.group(1))
                if int(h.group(1)) != want:
                    out.append((path, m.group(1), int(h.group(1)), want))
    return out


def fix_stale(stale):
    by_file = defaultdict(list)
    for path, fid, have, want in stale:
        by_file[path].append((fid, have, want))
    for path, rows in by_file.items():
        with open(path, encoding="utf-8", newline="") as fh:
            text = fh.read()
        for fid, have, want in rows:
            head = f"--- !u!114 &{fid}\n"
            start = text.index(head)
            end = text.find("\n--- ", start + len(head))
            end = len(text) if end < 0 else end
            block = text[start:end]
            new_block, n = re.subn(rf"^(\s*GlobalObjectIdHash: ){have}(\s*)$", rf"\g<1>{want}\g<2>", block, count=1, flags=re.M)
            assert n == 1, f"{path} &{fid}: hash line not found"
            text = text[:start] + new_block + text[end:]
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(text)


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

        # The hash formula, against a prefab Unity itself regenerated (Goby.prefab, 2026-10-24).
        assert expected_hash("982ad3e4c35f4421ae0424bf7bb1ee47", "1733035045152327888") == 3347286452, \
            "xxh32 of the GlobalObjectId string no longer matches Unity's value"
        # Stale: fires on a wrong hash, --fix writes Unity's value, then quiet.
        for name in os.listdir(tmp):
            os.remove(os.path.join(tmp, name))
        prefab = os.path.join(tmp, "Goby.prefab")
        with open(prefab, "w") as fh:
            fh.write("--- !u!114 &1733035045152327888\nMonoBehaviour:\n  m_PrefabInstance: {fileID: 0}\n"
                     "  GlobalObjectIdHash: 0\n  InScenePlacedSourceGlobalObjectIdHash: 0\n"
                     "--- !u!114 &5\nMonoBehaviour:\n  m_PrefabInstance: {fileID: 0}\n  Other: 1\n")
        with open(prefab + ".meta", "w") as fh:
            fh.write("fileFormatVersion: 2\nguid: 982ad3e4c35f4421ae0424bf7bb1ee47\n")
        got = stale_hashes(tmp)
        assert [(r[1], r[2], r[3]) for r in got] == [("1733035045152327888", 0, 3347286452)], f"stale did not fire: {got}"
        fix_stale(got)
        assert stale_hashes(tmp) == [], "--fix left the hash stale"
        with open(prefab) as fh:
            fixed = fh.read()
        assert "  GlobalObjectIdHash: 3347286452\n  InScenePlacedSourceGlobalObjectIdHash: 0\n" in fixed, fixed
        print("network-prefab-hash self-test: OK (fires on a collision and on a stale hash, quiet otherwise)")
        return 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    if "--self-test" in sys.argv:
        return self_test()
    stale = stale_hashes(ASSETS)
    if stale and "--fix" in sys.argv:
        fix_stale(stale)
        for path, fid, have, want in stale:
            print(f"FIXED: {os.path.relpath(path, os.path.join(ASSETS, '..'))} &{fid}: {have} -> {want}")
        stale = stale_hashes(ASSETS)
    for path, fid, have, want in stale:
        print(f"ERROR: {os.path.relpath(path, os.path.join(ASSETS, '..'))} NetworkObject &{fid} commits "
              f"GlobalObjectIdHash {have}; Unity computes {want}.")
    if stale:
        print("       Editors then disagree on the hash and every party join fails with 'NetworkConfig")
        print("       mismatch' (B35). Run with --fix to write Unity's value.")
    found = collisions(ASSETS)
    new = {h: v for h, v in found.items() if (h, v) not in BASELINE}
    gone = [b for b in BASELINE if b[0] not in found]
    for h, v in sorted(new.items()):
        print(f"ERROR: GlobalObjectIdHash {h} is shared by {' and '.join(v)}.")
        print("       Netcode keys its prefab table on this hash, so only one of them can ever")
        print("       spawn. A prefab copied on disk keeps the original's hash; recompute it as")
        print("       XXHash32('GlobalObjectId_V1-1-<prefab guid>-<NetworkObject fileID>-0'),")
        print("       or open the prefab in the editor and save so OnValidate regenerates it.")
    for b in gone:
        print(f"NOTE: baselined collision {b[0]} {b[1]} is gone - remove it from BASELINE.")
    if new or stale:
        return 1
    print(f"network-prefab-hash check: OK ({len(found)} collision(s), 0 stale hashes across all prefabs)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
