#!/usr/bin/env python3
"""
Retire ArcadeDPadNav (style-B menu redesign, item 3 follow-up; Docs/MENU_REDESIGN_B.md section 12).

ArcadeDPadNav polled the gamepad directly and kept its own button grid. It returned while any modal
was open - and every window it sat in (Arcade, Arena, Mission, Toy Box) IS a modal - so it did nothing
while its grid was visible, and while the window was CLOSED it selected a card in the hidden grid at
Start, moved that hidden selection on every d-pad press on HOME, and invoked it on buttonSouth. The
EventSystem drives in-window navigation now, and KitScrollIntoView keeps a focused card in view.

This removes every ArcadeDPadNav component (and its `m_Component` entry) and the retired
`ArcadeDPadNav:` field of every ArcadeExploreView, in the scene and in the one prefab that still
carried a copy. The script itself (Assets/ArcadeDPadNav.cs) is deleted in the same change, so its guid
is harvested here rather than read from a meta.

STANDING DOWN. Once nothing carries the component, the migration does nothing and the checks below
run against the files as committed - on every run, --check included.

    python3 Tools/Build/retire_arcade_dpad_nav.py              # write
    python3 Tools/Build/retire_arcade_dpad_nav.py --check      # fail if anything still carries it
    python3 Tools/Build/retire_arcade_dpad_nav.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_topnav", os.path.join(HERE, "author_menu_topnav.py"))
T = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(T)

REPO = T.REPO
DPAD = "9c7d37f119377f34cbc93e5833d5a3b3"          # Assets/ArcadeDPadNav.cs, harvested before deletion
FILES = ("Assets/_Scenes/Menu_Main.unity",
         "Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab")
FIELD = re.compile(r"^  ArcadeDPadNav: \{fileID: -?\d+\}\n", re.M)


def dpad_docs(sc):
    return [f for f in sc.order if sc.cls[f] == 114 and sc.script(f) == DPAD]


def transform(text):
    sc = T.Scene(text)
    for fid in dpad_docs(sc):
        go = sc.go_of(fid)
        line = f"  - component: {{fileID: {fid}}}\n"
        assert sc.docs[go].count(line) == 1, f"ArcadeDPadNav {fid} is not listed once on its GameObject"
        sc.replace(go, sc.docs[go].replace(line, ""))
        sc.remove(fid)
    for fid in sc.order:
        if FIELD.search(sc.docs[fid]):
            sc.replace(fid, FIELD.sub("", sc.docs[fid]))
    return sc.text()


def validate(before_text, after_text, path):
    before, sc = T.Scene(before_text), T.Scene(after_text)
    assert not dpad_docs(sc), f"{path}: an ArcadeDPadNav component survives"
    assert not FIELD.search(after_text), f"{path}: an ArcadeExploreView still serializes ArcadeDPadNav"
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"{path}: dangling local fileID {m.group(1)} in {fid}"

    retired = set(dpad_docs(before))
    removed = set(before.order) - set(sc.order)
    assert not (set(sc.order) - set(before.order)), f"{path}: new documents"
    assert removed == retired, f"{path}: unexpected removals {sorted(removed - retired)[:3]}"
    owners = {before.go_of(f) for f in retired}
    fielded = {f for f in before.order if FIELD.search(before.docs[f])}
    for fid in set(before.order) & set(sc.order):
        a, b = before.docs[fid], sc.docs[fid]
        if a == b:
            continue
        expect = a
        if fid in owners:
            for d in retired:
                expect = expect.replace(f"  - component: {{fileID: {d}}}\n", "")
        if fid in fielded:
            expect = FIELD.sub("", expect)
        assert T.normalize(expect) == T.normalize(b), f"{path}: {fid} changed beyond the retirement"


def read(path):
    with open(os.path.join(REPO, path), encoding="utf-8", newline="") as fh:
        return fh.read()


def self_test():
    caught = []
    for path in FILES:
        before = read(path)
        after = transform(before)
        validate(before, after, path)
        validate(after, after, path)
    path = FILES[0]
    before = read(path)
    after = transform(before)
    sc = T.Scene(before)
    view = next(f for f in sc.order if FIELD.search(sc.docs[f]))
    dpad = dpad_docs(sc)[0]
    plants = {
        "a component survives": lambda: validate(before, before, path),
        "the field survives on one view": lambda: validate(
            before, T.in_doc(after, view, "  m_EditorClassIdentifier: \n",
                             "  m_EditorClassIdentifier: \n  ArcadeDPadNav: {fileID: 0}\n"), path),
        "an unrelated document removed": lambda: validate(
            before, T.Scene(after).text().replace(T.Scene(after).docs[T.Scene(after).order[-1]], ""), path),
        "the view edited beyond the field": lambda: validate(
            before, T.in_doc(after, view, "  rosterOverride:", "  rosterOverrideX:"), path),
        "the GameObject keeps the component entry": lambda: validate(
            before, T.in_doc(after, sc.go_of(dpad), "  m_Component:\n",
                             f"  m_Component:\n  - component: {{fileID: {dpad}}}\n"), path),
    }
    missed = []
    for name, fn in plants.items():
        try:
            fn()
            missed.append(name)
        except AssertionError as e:
            caught.append(name)
            print(f"  caught: {name}: {str(e)[:110]}")
    assert not missed, f"validation MISSED planted defects: {missed}"
    print(f"OK: {len(plants)} planted defects all caught")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        self_test()
        return 0

    problems = []
    for path in FILES:
        before = read(path)
        after = transform(before)
        validate(before, after, path)
        if args.check:
            if after != before:
                problems.append(f"{path} still carries ArcadeDPadNav")
            continue
        if after != before:
            with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="") as fh:
                fh.write(after)
            print(f"retired ArcadeDPadNav in {path}")
    if os.path.exists(os.path.join(REPO, "Assets/ArcadeDPadNav.cs")):
        problems.append("Assets/ArcadeDPadNav.cs still exists")
    for p in problems:
        print("DRIFT:", p)
    if args.check:
        print("FAIL" if problems else "OK: no ArcadeDPadNav anywhere; the script is gone")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
