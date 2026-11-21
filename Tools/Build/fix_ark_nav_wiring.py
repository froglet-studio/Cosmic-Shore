#!/usr/bin/env python3
"""
Point the ARK nav-bar link at ScreenSwitcher.OnClickArkNav.

Menu_Main's five nav-bar links each carry an EventTrigger whose PointerClick calls the
ScreenSwitcher handler for their own screen. Four do. ArkLink calls OnClickHangarNav - a
copy-paste slip, confirmed by OnClickArkNav being referenced by NOTHING in any scene.

It matters more than a mis-labelled button: with it, pressing ARK navigates to the HANGAR
screen, so ARK can never take the LOCKED state that Docs/HomeHub/ARCHITECTURE.md §2 gives a
disabled screen - NavigateTo is never asked about ARK at all. The lock and this fix ship
together or the lock is a lie.

Idempotent: re-running prints "already wired" and exits 0.

    python3 Tools/Build/fix_ark_nav_wiring.py [--check]

--check exits 1 if the link is still mis-wired (for CI).
"""
import re, sys, pathlib

SCENE = pathlib.Path("Assets/_Scenes/Menu_Main.unity")
LINK_NAME = "ArkLink"
WRONG, RIGHT = "OnClickHangarNav", "OnClickArkNav"


def components_of(docs, go_name):
    """The component fileIDs on the one GameObject with this name."""
    hits = [d for d in docs
            if d.startswith("!u!1 &") and re.search(rf"^  m_Name: {re.escape(go_name)}$", d, re.M)]
    assert len(hits) == 1, f"expected 1 GameObject named {go_name!r}, found {len(hits)}"
    return set(re.findall(r"component: \{fileID: (\d+)\}", hits[0]))


def main():
    check = "--check" in sys.argv
    raw = SCENE.read_text(encoding="utf-8")
    parts = raw.split("\n--- ")
    header, docs = parts[0], parts[1:]

    comps = components_of(docs, LINK_NAME)

    hits = []
    for i, d in enumerate(docs):
        m = re.match(r"!u!114 &(\d+)", d)
        if m and m.group(1) in comps and f"m_MethodName: {WRONG}" in d:
            hits.append(i)

    if not hits:
        # Already correct?
        ok = any(re.match(r"!u!114 &(\d+)", d) and re.match(r"!u!114 &(\d+)", d).group(1) in comps
                 and f"m_MethodName: {RIGHT}" in d for d in docs)
        print("already wired" if ok else f"WARNING: {LINK_NAME} calls neither {WRONG} nor {RIGHT}")
        return 0 if ok else 1

    if check:
        print(f"MIS-WIRED: {LINK_NAME} still calls {WRONG} (expected {RIGHT})")
        return 1

    assert len(hits) == 1, f"expected exactly one {WRONG} call on {LINK_NAME}, found {len(hits)}"
    before = docs[hits[0]]
    after = before.replace(f"m_MethodName: {WRONG}", f"m_MethodName: {RIGHT}", 1)
    assert after != before and after.count(f"m_MethodName: {RIGHT}") == 1
    docs[hits[0]] = after

    out = header + "".join("\n--- " + d for d in docs)
    # The only change is that one method name - assert it before writing.
    assert len(out) == len(raw) + (len(RIGHT) - len(WRONG))
    SCENE.write_text(out, encoding="utf-8")
    print(f"rewired: {LINK_NAME} -> ScreenSwitcher.{RIGHT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
