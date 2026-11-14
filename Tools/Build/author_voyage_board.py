#!/usr/bin/env python3
"""
The Voyages tab's look (Docs/MENU_REDESIGN_B.md sections 162-165: Rev 81 B5 with the Rev 83 S2 seat editor, picked by
Shombith on 2026-11-09). VoyageBoard builds the tab at runtime and reads its sprites, colours and proportions from ONE
asset, Assets/Resources/VoyageBoardStyle.asset (VoyageBoardStyleSO); this writes it and its metas, and the metas of the
two scripts.

The kit sprites are the style guide's (Assets/_Graphics/UI/Kit, the corner sliver at 14 on surfaces and 10 on buttons).
The colours are the style guide's tokens, typed here once with the token they come from and checked against
Docs/UI/froglet-tokens.json, so a token change in the game fails --check here.

    python3 Tools/Build/author_voyage_board.py             # write
    python3 Tools/Build/author_voyage_board.py --check     # drift gate: exits 1 if the files on disk differ
    python3 Tools/Build/author_voyage_board.py --self-test # planted defects must be caught
"""

import argparse
import glob
import hashlib
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ASSET = "Assets/Resources/VoyageBoardStyle.asset"
STYLE_CS = "Assets/_Scripts/ScriptableObjects/Voyages/VoyageBoardStyleSO.cs"
BOARD_CS = "Assets/_Scripts/UI/Views/VoyageBoard.cs"
TOKENS = "Docs/UI/froglet-tokens.json"
KIT = "Assets/_Graphics/UI/Kit"
LABEL_FONT = "Assets/_Graphics/Fonts/ChakraPetch/ChakraPetch-SemiBold SDF.asset"

SPRITES = [  # field, kit file
    ("Sliver10Fill", "kit_sliver10_fill"), ("Sliver10Line", "kit_sliver10_line1"),
]
# field, hex, alpha, the token it is (a hex the tokens file must hold)
COLOURS = [
    ("Light", "#E6E9FF", 1.0), ("Sub", "#747BAD", 1.0), ("Jade", "#21C0BD", 1.0), ("Cyan", "#00D4FF", 1.0),
    ("Outline", "#434C89", 1.0), ("Popup", "#00010A", 1.0),
    ("RowPicked", "#00041F", 0.86), ("CtaFill", "#99FF80", 1.0), ("CtaText", "#00041F", 1.0),
]
CLASSES = "Assets/_SO_Assets/Classes"
HULL_PAD = 6  # px of air kept round each measured hull
NUMBERS = [
    # The Sea table's columns (Rev 89 A, 1840 across): chips 0-310, the table 340-1360, the squad from 1390.
    ("ListRight", "0.1685"), ("CourseLeft", "0.1848"), ("CourseRight", "0.7391"), ("PanelLeft", "0.7554"),
    ("RowHeight", "96"),
]


def guid_for(path):
    """A deterministic guid for a new file (the same on every machine)."""
    return hashlib.md5(("cosmic-shore:" + path).encode()).hexdigest()


def read(path):
    with open(os.path.join(REPO, path), encoding="utf-8") as f:
        return f.read()


def meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})$", read(path + ".meta"), re.M)
    if not m:
        raise SystemExit(f"{path}.meta has no guid")
    return m.group(1)


def existing_guid(path):
    full = os.path.join(REPO, path + ".meta")
    if os.path.exists(full):
        m = re.search(r"^guid: ([0-9a-f]{32})$", read(path + ".meta"), re.M)
        if m:
            return m.group(1)
    return guid_for(path)


def script_meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
            "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n"
            "  assetBundleVariant: \n")


def asset_meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def colour(hexv, alpha):
    r, g, b = (int(hexv[i:i + 2], 16) / 255 for i in (1, 3, 5))
    f = lambda v: f"{v:.7g}"
    return f"{{r: {f(r)}, g: {f(g)}, b: {f(b)}, a: {f(alpha)}}}"


def asset(style_guid):
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
           "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
           "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
           f"  m_Script: {{fileID: 11500000, guid: {style_guid}, type: 3}}", "  m_Name: VoyageBoardStyle",
           "  m_EditorClassIdentifier: "]
    for field, kit in SPRITES:
        out.append(f"  {field}: {{fileID: 21300000, guid: {meta_guid(f'{KIT}/{kit}.png')}, type: 3}}")
    out.append(f"  LabelFont: {{fileID: 11400000, guid: {meta_guid(LABEL_FONT)}, type: 2}}")
    for field, hexv, alpha in COLOURS:
        out.append(f"  {field}: {colour(hexv, alpha)}")
    for field, value in NUMBERS:
        out.append(f"  {field}: {value}")
    crops = hull_crops()
    out.append("  HullCrops:" + ("" if crops else " []"))
    for guid, (x, y, w, h) in crops:
        out += [f"  - Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}", "    Uv:", "      serializedVersion: 2",
                f"      x: {x:.4f}", f"      y: {y:.4f}", f"      width: {w:.4f}", f"      height: {h:.4f}"]
    return "\n".join(out) + "\n"


def png_by_guid():
    found = {}
    for meta in glob.glob(os.path.join(REPO, "Assets/_Graphics/**/*.png.meta"), recursive=True):
        with open(meta, encoding="utf-8", errors="replace") as f:
            m = re.search(r"^guid: ([0-9a-f]{32})$", f.read(), re.M)
        if m:
            found[m.group(1)] = meta[:-5]
    return found


def hull_crops():
    """Every class's SquadImage, measured: the box its opaque pixels fill, padded, as 0-1 from the bottom left."""
    from PIL import Image
    pngs = png_by_guid()
    crops = {}
    for asset_path in sorted(glob.glob(os.path.join(REPO, CLASSES, "SO_Class_*.asset"))):
        with open(asset_path, encoding="utf-8") as f:
            m = re.search(r"^  SquadImage: \{fileID: 21300000, guid: ([0-9a-f]{32})", f.read(), re.M)
        if not m or m.group(1) in crops or m.group(1) not in pngs:
            continue
        png = pngs[m.group(1)]
        with open(png + ".meta", encoding="utf-8") as f:
            if not re.search(r"^  spriteMode: 1$", f.read(), re.M):
                continue
        im = Image.open(png).convert("RGBA")
        bb = im.getchannel("A").point(lambda a: 255 if a > 20 else 0).getbbox()
        if not bb:
            continue
        W, H = im.size
        l, t, r, b = max(0, bb[0] - HULL_PAD), max(0, bb[1] - HULL_PAD), min(W, bb[2] + HULL_PAD), min(H, bb[3] + HULL_PAD)
        crops[m.group(1)] = (l / W, 1 - b / H, (r - l) / W, (b - t) / H)
    return sorted(crops.items())


def problems():
    """What --check and --self-test hold the sources to, besides the files' bytes."""
    errors = []
    cs = read(STYLE_CS)
    for field, _ in SPRITES:
        if not re.search(rf"public Sprite {field};", cs):
            errors.append(f"VoyageBoardStyleSO has no Sprite {field}")
    for field, _, _ in COLOURS:
        if not re.search(rf"public Color {field} = ", cs):
            errors.append(f"VoyageBoardStyleSO has no Color {field}")
    for field, _ in NUMBERS:
        if not re.search(rf"public float {field} = ", cs):
            errors.append(f"VoyageBoardStyleSO has no float {field}")
    tokens = read(TOKENS).upper()
    for field, hexv, _ in COLOURS:
        if hexv.upper() not in tokens:
            errors.append(f"{field} {hexv} is not a style token ({TOKENS})")
    for _, kit in SPRITES:
        if not os.path.exists(os.path.join(REPO, KIT, kit + ".png")):
            errors.append(f"{KIT}/{kit}.png is missing")
    if len(hull_crops()) < 6:
        errors.append("fewer than six hull renders measured (SO_Class_*.asset SquadImage)")
    left, cl, cr, pl = (float(v) for _, v in NUMBERS[:4])
    if not (0 < left < cl < cr < pl < 1):
        errors.append("the columns must run list < course < panel")
    return errors


def outputs():
    style_guid = existing_guid(STYLE_CS)
    return {
        STYLE_CS + ".meta": script_meta(style_guid),
        BOARD_CS + ".meta": script_meta(existing_guid(BOARD_CS)),
        ASSET: asset(style_guid),
        ASSET + ".meta": asset_meta(existing_guid(ASSET)),
    }


def self_test():
    global COLOURS, NUMBERS
    caught = 0
    saved = (COLOURS, NUMBERS)
    COLOURS = COLOURS + [("Bogus", "#123457", 1.0)]
    caught += any("Bogus" in e for e in problems())
    COLOURS = saved[0]
    NUMBERS = [("ListRight", "0.8")] + NUMBERS[1:]
    caught += any("columns" in e for e in problems())
    NUMBERS = saved[1]
    caught += not problems()
    print(f"self-test: {caught} of 3")
    return caught == 3


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(0 if self_test() else 1)
    errors = problems()
    for e in errors:
        print("error:", e)
    drifted = []
    for path, text in outputs().items():
        full = os.path.join(REPO, path)
        current = open(full, encoding="utf-8").read() if os.path.exists(full) else None
        if current == text:
            continue
        if a.check:
            drifted.append(path)
        else:
            with open(full, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
            print("wrote", path)
    if a.check:
        for p in drifted:
            print("drift:", p)
        print(f"check: {len(outputs())} files, {len(drifted)} drifted, {len(errors)} problem(s)")
    sys.exit(1 if errors or drifted else 0)


if __name__ == "__main__":
    main()
