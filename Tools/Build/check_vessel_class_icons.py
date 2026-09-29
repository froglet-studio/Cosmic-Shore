#!/usr/bin/env python3
"""Every vessel class asset's card icons must RESOLVE to an asset in the tree.

    python3 Tools/Build/check_vessel_class_icons.py             # fail on a dangling icon guid
    python3 Tools/Build/check_vessel_class_icons.py --self-test # prove it fires

A `UnityEngine.UI.Image` whose sprite guid no `.meta` owns draws a solid white quad in its tint,
with nothing in the console - so a deleted card PNG surfaces only on screen, as "the Urchin's
icon is a white square" (2026-09-15: `SO_Class_Urchin.IconActive` / `IconInactive` had pointed at
deleted art since before this clone's history begins). A reference check by guid is the only
thing that can see it offline; `ArenaRosterTests` holds the same fact in the editor, where the
missing sprite loads as null.

Reads `Assets/_SO_Assets/Classes/SO_Class_*.asset` - the fields `IconActive` and `IconInactive`
(the Arena carousel, the arcade card, the hangar) - and resolves each guid against every `.meta`
under `Assets/`. Empty (`{fileID: 0}`) is reported too: the carousel draws nothing for it, and
a hull with no icon is a hull nobody can pick on purpose.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
FIELDS = ("IconActive", "IconInactive")


def owned_guids():
    out = {}
    for path in glob.glob(os.path.join(ROOT, "Assets", "**", "*.meta"), recursive=True):
        with open(path, encoding="utf-8", errors="ignore") as fh:
            m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
        if m:
            out[m.group(1)] = os.path.relpath(path, ROOT)
    return out


def audit(texts, owned):
    findings = []
    for name, text in texts.items():
        for field in FIELDS:
            m = re.search(r"^  %s: \{fileID: (\d+)(?:, guid: ([0-9a-f]{32}))?" % field, text, re.M)
            if not m:
                findings.append(f"{name}: no {field} field")
            elif m.group(1) == "0" or not m.group(2):
                findings.append(f"{name}: {field} is empty")
            elif m.group(2) not in owned:
                findings.append(f"{name}: {field} -> guid {m.group(2)} is owned by no .meta under Assets/")
    return findings


def self_test():
    owned = {"a" * 32: "Assets/x.png.meta"}
    good = "  IconActive: {fileID: 21300000, guid: " + "a" * 32 + ", type: 3}\n  IconInactive: {fileID: 21300000, guid: " + "a" * 32 + ", type: 3}\n"
    dangling = good.replace("a" * 32, "b" * 32, 1)
    empty = "  IconActive: {fileID: 0}\n  IconInactive: {fileID: 21300000, guid: " + "a" * 32 + ", type: 3}\n"
    assert audit({"good": good}, owned) == []
    assert len(audit({"dangling": dangling}, owned)) == 1
    assert len(audit({"empty": empty}, owned)) == 1
    print("self-test OK (1 clean, 1 dangling, 1 empty)")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    texts = {}
    for path in sorted(glob.glob(os.path.join(CLASSES, "SO_Class_*.asset"))):
        with open(path, encoding="utf-8") as fh:
            texts[os.path.basename(path)] = fh.read()
    findings = audit(texts, owned_guids())
    if findings:
        print("FAIL: vessel class icons that do not resolve:")
        for f in findings:
            print("  - " + f)
        return 1
    print(f"OK: {len(texts)} vessel class assets, every IconActive/IconInactive resolves.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
