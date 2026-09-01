#!/usr/bin/env python3
"""Remove the per-scene crystal payout table from every Scoreboard component.

The payout used to be a serialized `List<int> placementCrystalRewards` on the
Scoreboard, so nine gameplay scenes each carried their own copy of the economy and
five more surfaces still carried the RETIRED `winnerCrystalReward` key - which no
longer resolves to anything, so those five were paying out of the C# field
initializer while the other nine paid out of their serialized copy. They agreed by
luck; the first retune would have split them.

Both keys now go, and the numbers live in Resources/RewardTable.asset
(`RewardTableSO`), which every Scoreboard reads.

Scoped by the enclosing m_Script guid - Scoreboard AND its two subclasses, which
serialize the inherited field under their own guids. A bare line delete would
happily strip a same-named key from an unrelated component.

    python3 Tools/Build/strip_scoreboard_payout_overrides.py           # write
    python3 Tools/Build/strip_scoreboard_payout_overrides.py --check   # verify only
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"

# Scoreboard.cs + the two subclasses that inherit the field. Read from the .meta
# files rather than hardcoded, so a rename cannot silently narrow the sweep.
SCRIPTS = [
    "_Scripts/UI/Scoreboard.cs",
    "_Scripts/UI/DuelForCellScoreboard.cs",
    "_Scripts/UI/CoOpScoreBoard.cs",
]

DEAD_KEYS = ("placementCrystalRewards", "winnerCrystalReward")


def script_guids():
    guids = {}
    for rel in SCRIPTS:
        meta = ASSETS / (rel + ".meta")
        if not meta.exists():
            sys.exit(f"missing meta for {rel}")
        m = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text()[:200], re.M)
        if not m:
            sys.exit(f"no guid in {meta}")
        guids[m.group(1)] = rel
    return guids


def strip(text, guids):
    """Drop the dead keys, but only inside a document whose m_Script is one of ours.

    Returns (new_text, removed, rejected) - `rejected` is every hit that matched the
    key name under some OTHER component, which must stay empty for the pass to be
    provably total.
    """
    out, removed, rejected = [], [], []
    current_guid = None

    for line in text.split("\n"):
        if line.startswith("--- !u!"):
            current_guid = None                      # new document, ownership unknown
        else:
            m = re.match(r"^  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", line)
            if m:
                current_guid = m.group(1)

        key = re.match(r"^  (\w+):", line)
        if key and key.group(1) in DEAD_KEYS:
            if current_guid in guids:
                removed.append(line.strip())
                continue                              # drop the line
            rejected.append((current_guid, line.strip()))

        out.append(line)

    return "\n".join(out), removed, rejected


def main():
    check = "--check" in sys.argv
    guids = script_guids()

    targets = sorted(
        p for p in list(ASSETS.rglob("*.unity")) + list(ASSETS.rglob("*.prefab"))
        if any(k in p.read_text(encoding="utf-8", errors="ignore") for k in DEAD_KEYS)
    )

    if not targets:
        print("OK - no scene or prefab carries a payout override.")
        return

    total, problems = 0, []
    for path in targets:
        original = path.read_text(encoding="utf-8")
        new, removed, rejected = strip(original, guids)

        if rejected:
            problems += [f"{path.relative_to(ROOT)}: key under foreign script "
                         f"{g}: {l}" for g, l in rejected]
            continue

        # Round-trip guard: the ONLY difference may be the removed lines. A
        # whitespace-only byte change across 14 files is indistinguishable from a
        # real edit in review.
        expected = original
        for line in removed:
            expected = expected.replace(f"  {line}\n", "", 1)
        if new + ("\n" if original.endswith("\n") and not new.endswith("\n") else "") != expected:
            problems.append(f"{path.relative_to(ROOT)}: round-trip mismatch, refusing to write")
            continue

        if not removed:
            continue

        total += len(removed)
        print(f"{'WOULD STRIP' if check else 'STRIPPED'} {len(removed)} from "
              f"{path.relative_to(ROOT)}: {', '.join(removed)}")

        if not check:
            path.write_text(expected, encoding="utf-8")
            after = path.read_text(encoding="utf-8")
            for k in DEAD_KEYS:
                if re.search(rf"^  {k}:", after, re.M):
                    sys.exit(f"ASSERT FAILED: {k} survived in {path}")

    if problems:
        print("\nPROBLEMS:")
        for p in problems:
            print("  " + p)
        sys.exit(1)

    if check and total:
        sys.exit(1)
    print(f"\n{'Would strip' if check else 'Stripped'} {total} payout override(s) "
          f"across {len(targets)} file(s).")


if __name__ == "__main__":
    main()
