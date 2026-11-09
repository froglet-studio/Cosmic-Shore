#!/usr/bin/env python3
"""
Author the player AVATAR choices from the pilot roster: one avatar per callsign, cut from that pilot's portrait.
Garrett, 2026-11-08: "update the avatar choices to reflect all the pilots". This script OWNS:

  Assets/_Graphics/Profile/Pilots/Avatar_<CALLSIGN>.png        128x128 RGBA, head and shoulders on a Deep Sea glow
  Assets/_Graphics/Profile/Pilots/Avatar_<CALLSIGN>.png.meta   the old avatar set's importer, deterministic guid
                                                               md5("CosmicShore/PilotAvatar/<CALLSIGN>")
  Assets/_SO_Assets/SO_DefaultProfileIcons.asset               the SO_ProfileIconList every avatar picker and
                                                               avatar face reads (PlayerDataService.GetAvatarSprite)

THE SOURCE is each callsign's portrait as the pilot-art kit wired it (Tools/Build/pilot_art/wired.json, the
220x280 finished sprite). The cut is measured from the sprite's alpha: the square starts just above the top of the
silhouette and centres on the head (the median column of the opaque pixels in its upper half), so a new or
re-painted portrait re-cuts itself. Run the kit first (pilot_art/author.py), then this.

THE IDS are the callsign values (CALLSIGNS.md §2: ENSIGN = 1 ... FLUME = 18), the same 1..18 the old avatar set
used, so a profile saved with an old avatar id lands on a pilot instead of falling back to the first face.

Usage:
    python3 Tools/Build/author_pilot_avatar_assets.py             # write
    python3 Tools/Build/author_pilot_avatar_assets.py --check     # diff disk; exit 1 on drift
    python3 Tools/Build/author_pilot_avatar_assets.py --self-test # negative controls
    python3 Tools/Build/author_pilot_avatar_assets.py --sheet out.png   # every avatar on one sheet, writes nothing
"""
import hashlib
import json
import os
import re
import sys

try:                                     # numpy and Pillow are not in every collaborator's Python: the ship gate
    import numpy as np                   # then SKIPs, like pilot_art/author.py
    from PIL import Image
    MISSING = None
except ImportError as _e:                # pragma: no cover - environment dependent
    np = Image = None
    MISSING = str(_e)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
from author_callsign_assets import CALLSIGNS  # noqa: E402  (value, word, meaning): one row per callsign

WIRED = "Tools/Build/pilot_art/wired.json"
OUT_DIR = "Assets/_Graphics/Profile/Pilots"
META_DONOR = "Assets/_Graphics/Profile/ProfileIcon01.png.meta"     # the old avatar set's importer settings
ICON_LIST = "Assets/_SO_Assets/SO_DefaultProfileIcons.asset"
ICON_LIST_SCRIPT_META = "Assets/_Scripts/ScriptableObjects/SO_ProfileIconList.cs.meta"

SIZE = 128              # the old avatars' size; the picker, chips and HUD faces all scale it down
CUT = 88                # the square cut from the 220x280 portrait: head and shoulders
TOP_PAD = 6             # rows of air above the silhouette's top
HEAD_SHARE = 0.55       # the upper share of the cut whose opaque columns place the head
ALPHA_SOLID = 40        # an alpha above this is the pilot, not the matte's fringe
GLOW_INNER = (0x43, 0x4C, 0x89)   # Deep Sea surface-raised (Docs/UI/froglet-tokens.json)
GLOW_OUTER = (0x00, 0x04, 0x1F)   # Deep Sea ground

TOLERANCE_LEVELS = 3    # --check: Lanczos and float maths may move a few values by a level between builds
TOLERANCE_SHARE = 0.01


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()


def write_text(rel, text):
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def guid_of(rel_meta):
    return re.search(r"^guid: ([0-9a-f]{32})\s*$", read(rel_meta), re.M).group(1)


def avatar_guid(word):
    return hashlib.md5(f"CosmicShore/PilotAvatar/{word}".encode("utf-8")).hexdigest()


def png_path(word):
    return f"{OUT_DIR}/Avatar_{word}.png"


def roster():
    """[(value, word, portrait png)] in callsign order. Every callsign must have a wired portrait."""
    wired = json.loads(read(WIRED)).get("portraits", {})
    out = []
    for value, word, _ in CALLSIGNS:
        entry = wired.get(f"pilot.callsign.{word.lower()}")
        assert entry, f"{word} has no wired portrait in {WIRED} (run Tools/Build/pilot_art/author.py for it)"
        assert os.path.exists(os.path.join(ROOT, entry["png"])), f"{entry['png']} is missing"
        out.append((value, word, entry["png"]))
    return out


def cut_box(alpha):
    """(left, top) of the CUT x CUT square on a portrait's alpha channel (H x W uint8)."""
    h, w = alpha.shape
    solid = alpha > ALPHA_SOLID
    rows = np.where(solid.sum(1) > 2)[0]
    top = max(0, int(rows[0]) - TOP_PAD) if len(rows) else 0
    top = min(top, h - CUT)
    cols = np.where(solid[top:top + int(CUT * HEAD_SHARE)])[1]
    cx = int(np.median(cols)) if len(cols) else w // 2
    left = max(0, min(w - CUT, cx - CUT // 2))
    return left, top


def glow():
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    r = np.clip(np.sqrt((xx - SIZE / 2) ** 2 + (yy - SIZE * 0.43) ** 2) / (SIZE * 0.707), 0, 1)[..., None]
    rgb = np.array(GLOW_INNER) * (1 - r) + np.array(GLOW_OUTER) * r
    return Image.fromarray(np.dstack([rgb.round().astype(np.uint8), np.full((SIZE, SIZE), 255, np.uint8)]), "RGBA")


def render(portrait_png):
    im = Image.open(os.path.join(ROOT, portrait_png)).convert("RGBA")
    left, top = cut_box(np.asarray(im)[:, :, 3])
    face = im.crop((left, top, left + CUT, top + CUT)).resize((SIZE, SIZE), Image.LANCZOS)
    out = glow()
    out.alpha_composite(face)
    return out


def meta_text(word):
    return re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {avatar_guid(word)}", read(META_DONOR), count=1, flags=re.M)


def icon_list_text(rows):
    script = guid_of(ICON_LIST_SCRIPT_META)
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}",
        "  m_Name: SO_DefaultProfileIcons",
        "  m_EditorClassIdentifier: ",
        "  profileIcons:",
    ]
    for value, word, _ in rows:
        lines += [
            f"  - Name: {word}",
            f"    Id: {value}",
            f"    IconSprite: {{fileID: 21300000, guid: {avatar_guid(word)}, type: 3}}",
        ]
    return "\n".join(lines) + "\n"


def write_all():
    rows = roster()
    for _, word, png in rows:
        rel = png_path(word)
        full = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        render(png).save(full, optimize=True)
        write_text(rel + ".meta", meta_text(word))
    folder_meta = OUT_DIR + ".meta"
    if not os.path.exists(os.path.join(ROOT, folder_meta)):
        guid = hashlib.md5(b"CosmicShore/PilotAvatar/folder").hexdigest()
        write_text(folder_meta, "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
                                "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)
    write_text(ICON_LIST, icon_list_text(rows))
    print(f"wrote {len(rows)} pilot avatars under {OUT_DIR} and {ICON_LIST}")


def check():
    problems = []
    rows = roster()
    for _, word, png in rows:
        rel = png_path(word)
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full):
            problems.append(f"{rel} is missing")
            continue
        disk = np.asarray(Image.open(full).convert("RGBA")).astype(np.int16)
        mine = np.asarray(render(png)).astype(np.int16)
        if disk.shape != mine.shape:
            problems.append(f"{rel} differs from the cut of {png} (size)")
        else:
            diff = np.abs(disk - mine)
            if diff.max() > TOLERANCE_LEVELS or (diff > 0).mean() > TOLERANCE_SHARE:
                problems.append(f"{rel} differs from the cut of {png} (max {int(diff.max())} levels)")
        if not os.path.exists(full + ".meta") or read(rel + ".meta") != meta_text(word):
            problems.append(f"{rel}.meta drifted")
    if not os.path.exists(os.path.join(ROOT, OUT_DIR + ".meta")):
        problems.append(f"{OUT_DIR}.meta is missing")
    if read(ICON_LIST) != icon_list_text(rows):
        problems.append(f"{ICON_LIST} drifted from the roster")
    return problems


def self_test():
    failures = []
    rows = roster()
    if [v for v, _, _ in rows] != list(range(1, len(CALLSIGNS) + 1)):
        failures.append("avatar ids are not the callsign values 1..N")
    if len({avatar_guid(w) for _, w, _ in rows}) != len(rows):
        failures.append("two avatars share a guid")
    text = icon_list_text(rows)
    if text.count("  - Name: ") != len(CALLSIGNS):
        failures.append("the icon list does not hold one avatar per callsign")
    # The cut finds the head: a blob high on the right pulls the square right and up.
    a = np.zeros((280, 220), np.uint8)
    a[60:90, 150:180] = 255
    a[90:280, 120:210] = 255
    left, top = cut_box(a)
    if top != 60 - TOP_PAD or not (left <= 150 and left + CUT >= 180):
        failures.append(f"cut_box missed a synthetic head (left={left}, top={top})")
    # An empty sprite cuts from the top centre instead of crashing.
    left, top = cut_box(np.zeros((280, 220), np.uint8))
    if (left, top) != ((220 - CUT) // 2, 0):
        failures.append(f"cut_box on an empty sprite gave ({left}, {top})")
    # Drift is caught: a corrupted list text is not the authored one.
    if icon_list_text(rows).replace("Id: 1\n", "Id: 99\n", 1) == text:
        failures.append("an edited icon list would not read as drift")
    for f in failures:
        print("FAIL", f)
    print("self-test:", "ok" if not failures else f"{len(failures)} failure(s)")
    return 1 if failures else 0


def sheet(out):
    rows = roster()
    s = Image.new("RGBA", (6 * (SIZE + 8), 3 * (SIZE + 8)), (20, 20, 30, 255))
    for i, (_, _, png) in enumerate(rows):
        s.paste(render(png), ((i % 6) * (SIZE + 8) + 4, (i // 6) * (SIZE + 8) + 4))
    s.save(out)
    print("sheet:", out)


def main(argv):
    if MISSING is not None:
        print(f"SKIP: numpy/Pillow not available ({MISSING})")
        return 0
    if "--self-test" in argv:
        return self_test()
    if "--sheet" in argv:
        sheet(argv[argv.index("--sheet") + 1])
        return 0
    if "--check" in argv:
        problems = check()
        for p in problems:
            print("DRIFT", p)
        print("check:", "ok" if not problems else f"{len(problems)} problem(s)")
        return 1 if problems else 0
    write_all()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
