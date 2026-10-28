#!/usr/bin/env python3
"""
Author the HANGAR VESSEL LIST'S CARD ART (Docs/MENU_REDESIGN_B.md section 82; Shombith, 2026-10-26: "Go ahead with the
colourful card renders").

Each hull's three-quarter ("front3/4") view, in its model's slot colours, cut from the vessel-model contact sheet that
`Tools/Build/vessel_model/build.py` renders for it (`Docs/VesselModels/sheets/<vessel>_after.jpg`, Blender Cycles, the
second cell of the first row). The grey studio background is keyed out and un-blended from the edge (no grey fringe on
the navy cards), the hull is trimmed and fitted to one 600 x 200 clear canvas, so every card frames its hull the same
way. A hull with no sheet gets its card icon (SO_Vessel.IconActive) trimmed onto the same canvas.

    Assets/_Graphics/Hangar/Cards/<Vessel>.png           the art (sprite, single)
    Assets/_SO_Assets/UI/HangarCardArt.asset             HangarCardArtSO: the Hangar list's vessel -> art table
    Assets/_Scripts/UI/Elements/Hangar/HangarCardArtSO.cs.meta

ONLY THE HANGAR LIST READS IT. Every other screen keeps SO_Vessel.IconActive, the fleet's card art in its colour scheme
(Docs/VesselModels/COLOR.md, Garrett, 2026-10-18): this art shows the models' slot colours (tan body, cyan glass, lime
engine), which that scheme does not use on a vessel. When a model's sheet is re-rendered, re-run this.

    python3 Tools/Build/author_hangar_card_art.py              # write
    python3 Tools/Build/author_hangar_card_art.py --check      # fail if any output drifted (pixels within 2/255)
    python3 Tools/Build/author_hangar_card_art.py --self-test  # planted defects must all be caught
"""
import argparse
import hashlib
import io
import os
import re
import sys

import numpy as np
from PIL import Image, ImageFilter

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHEETS = "Docs/VesselModels/sheets/"
CLASSES = "Assets/_SO_Assets/Classes/"
CLASS_LIST = CLASSES + "SO_Classlist_Classes.asset"     # HangarScreen.ShipList in Menu_Main: the hulls the list shows
OUT_DIR = "Assets/_Graphics/Hangar/Cards"
SO_DIR = "Assets/_SO_Assets/UI"          # not Hangar/: that folder retired with TRY (author_menu_hangar.RETIRED_FILES)
SO_PATH = SO_DIR + "/HangarCardArt.asset"
SCRIPT = "Assets/_Scripts/UI/Elements/Hangar/HangarCardArtSO.cs"
DONOR_PNG_META = "Assets/_Graphics/CardImages/Squirrel.png.meta"   # the fleet cards' sprite import settings
DONOR_CS_META = "Assets/_Scripts/UI/Elements/Hangar/HangarVesselTile.cs.meta"

# class asset name (SO_Class_<name>) -> its model's contact sheet. The Wrasse flies the WrasseKite model.
SHEET = {
    "Sparrow": "sparrow_after.jpg", "Manta": "manta_after.jpg", "Serpent": "serpent_after.jpg",
    "Rhino": "rhino_rig_after.jpg", "Squirrel": "squirrel_after.jpg", "Dolphin": "dolphin_after.jpg",
    "Urchin": "urchin_rig_after.jpg", "Butterfly": "butterfly_after.jpg", "Thresher": "thresher_after.jpg",
    "Scarab": "scarab_after.jpg", "Stoat": "stoat_after.jpg", "Goby": "goby_after.jpg",
    "BrittleStar": "brittlestar_after.jpg", "Shrike": "shrike_after.jpg", "Angler": "angler_after.jpg",
    "Wrasse": "wrassekite_after.jpg", "Orca": "orca_after.jpg", "Starling": "starling_after.jpg",
    "Jacks": "jack_after.jpg", "Honeyguide": "honeyguide_after.jpg", "Grizzly": "grizzly_after.jpg",
}
COLS = 4                    # build.py's contact sheet: four views a row, cells 1 : 0.781
CELL_ASPECT = 0.781
VIEW = 1                    # chase, FRONT3/4, side, top
CANVAS = (600, 200)         # 3:1, the card's art band (author_menu_hangar.TILE_ART_*)
FILL = (0.94, 0.92)         # the hull's largest extent on the canvas
KEY_LO, KEY_HI = 6.0, 22.0  # colour distance from the studio grey: clear below, solid above


def guid(rel):
    return hashlib.md5(f"CosmicShore/HangarCardArt/{rel}".encode()).hexdigest()


def meta_guid(rel):
    with open(os.path.join(REPO, rel + ".meta"), encoding="utf-8") as fh:
        return re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M).group(1)


def asset_path(g):
    for root, _, files in os.walk(os.path.join(REPO, "Assets")):
        for f in files:
            if f.endswith(".meta"):
                p = os.path.join(root, f)
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    if f"guid: {g}\n" in fh.read(300):
                        return os.path.relpath(p, REPO)[:-5]
    return None


def hulls():
    """The Hangar list's hulls, in list order: (class name, class asset guid)."""
    with open(os.path.join(REPO, CLASS_LIST), encoding="utf-8") as fh:
        guids = re.findall(r"\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}", fh.read())
    by_guid = {}
    for f in os.listdir(os.path.join(REPO, CLASSES)):
        if f.startswith("SO_Class_") and f.endswith(".asset"):
            by_guid[meta_guid(CLASSES + f)] = f[len("SO_Class_"):-len(".asset")]
    out = [(by_guid[g], g) for g in guids if g in by_guid]
    assert out, "the Hangar's class list names no class assets"
    return out


def cut_from_sheet(path):
    im = Image.open(os.path.join(REPO, path)).convert("RGB")
    w, _ = im.size
    cw = w // COLS
    ch = round(cw * CELL_ASPECT)
    a = np.asarray(im.crop((VIEW * cw, 0, (VIEW + 1) * cw, ch))).astype(float).copy()
    bg = np.median(np.concatenate([a[-3:, :].reshape(-1, 3), a[:, -3:].reshape(-1, 3)]), axis=0)
    a[: int(ch * 0.07), : int(cw * 0.3)] = bg          # the view's label
    d = np.sqrt(((a - bg) ** 2).sum(-1))
    alpha = np.clip((d - KEY_LO) / (KEY_HI - KEY_LO), 0, 1)
    alpha = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.MedianFilter(3))) / 255.0
    # Un-blend the studio grey from the edge pixels, so the hull carries no grey fringe onto a navy card.
    safe = np.maximum(alpha, 1e-3)[..., None]
    rgb = np.clip((a - (1 - alpha[..., None]) * bg) / safe, 0, 255)
    return rgba(rgb, alpha)


def cut_from_icon(path):
    im = Image.open(os.path.join(REPO, path)).convert("RGBA")
    a = np.asarray(im).astype(float)
    return rgba(a[..., :3], a[..., 3] / 255.0)


def rgba(rgb, alpha):
    o = Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA")
    m = alpha > 0.4
    rows, cols = np.where(m.sum(1) > 2)[0], np.where(m.sum(0) > 2)[0]
    assert len(rows) and len(cols), "nothing left after keying the background"
    o = o.crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1))
    k = min(CANVAS[0] * FILL[0] / o.width, CANVAS[1] * FILL[1] / o.height)
    o = o.resize((max(1, round(o.width * k)), max(1, round(o.height * k))), Image.LANCZOS)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.paste(o, ((CANVAS[0] - o.width) // 2, (CANVAS[1] - o.height) // 2), o)
    return canvas


def png_bytes(im):
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return buf.getvalue()


def class_icon(name):
    with open(os.path.join(REPO, CLASSES + f"SO_Class_{name}.asset"), encoding="utf-8") as fh:
        g = re.search(r"^  IconActive: \{fileID: 21300000, guid: ([0-9a-f]{32})", fh.read(), re.M).group(1)
    return asset_path(g)


def png_meta(g):
    with open(os.path.join(REPO, DONOR_PNG_META), encoding="utf-8") as fh:
        donor = fh.read()
    out = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + g, donor, count=1, flags=re.M)
    assert out.count(g) == 1
    return out


def folder_meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def outputs():
    """{path: bytes}, plus the source of each card (for the report and the checks)."""
    files, sources = {}, {}
    entries = ""
    for name, class_guid in hulls():
        sheet = SHEET.get(name)
        if sheet and os.path.exists(os.path.join(REPO, SHEETS + sheet)):
            art, src = cut_from_sheet(SHEETS + sheet), SHEETS + sheet
        else:
            icon = class_icon(name)
            art, src = cut_from_icon(icon), icon
        rel = f"{OUT_DIR}/{name}.png"
        files[rel] = png_bytes(art)
        files[rel + ".meta"] = png_meta(guid(rel)).encode()
        sources[name] = src
        entries += (f"  - vessel: {{fileID: 11400000, guid: {class_guid}, type: 2}}\n"
                    f"    art: {{fileID: 21300000, guid: {guid(rel)}, type: 3}}\n")
    files[OUT_DIR + ".meta"] = folder_meta(guid(OUT_DIR)).encode()
    script_guid = guid(SCRIPT)
    with open(os.path.join(REPO, DONOR_CS_META), encoding="utf-8") as fh:
        files[SCRIPT + ".meta"] = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + script_guid, fh.read(), count=1,
                                         flags=re.M).encode()
    files[SO_PATH] = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
                      "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
                      "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                      f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: HangarCardArt\n"
                      "  m_EditorClassIdentifier: \n  entries:\n" + entries).encode()
    files[SO_PATH + ".meta"] = (f"fileFormatVersion: 2\nguid: {guid(SO_PATH)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
                                "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode()
    return files, sources


def validate(files, sources):
    names = [n for n, _ in hulls()]
    so = files[SO_PATH].decode()
    assert so.count("  - vessel: ") == len(names), "the art table must name every hull the Hangar lists"
    for name, g in hulls():
        assert f"vessel: {{fileID: 11400000, guid: {g}, type: 2}}" in so, f"{name} is missing from the art table"
        rel = f"{OUT_DIR}/{name}.png"
        im = Image.open(io.BytesIO(files[rel])).convert("RGBA")
        assert im.size == CANVAS, f"{name}: the card art is {im.size}, not {CANVAS}"
        al = np.asarray(im)[..., 3] / 255.0
        cover = (al > 0.5).mean()
        assert cover > 0.03, f"{name}: almost nothing of the hull survived the key ({cover:.1%})"
        assert al[:4, :4].max() == 0 and al[-4:, -4:].max() == 0, f"{name}: the canvas corners must be clear"
        rgb = np.asarray(im)[..., :3].astype(float)
        edge = (al > 0.05) & (al < 0.6)
        if edge.any():
            grey = (np.abs(rgb[edge] - np.array([73, 76, 85.])).sum(-1) < 24).mean()
            assert grey < 0.35, f"{name}: the studio grey fringes the hull ({grey:.0%} of its edge)"
        # The key left the hull, not the studio: a dark model (the BrittleStar) has little colour, but never grey.
        solid = rgb[al > 0.8]
        studio = (np.abs(solid - np.array([73, 76, 85.])).sum(-1) < 24).mean() if len(solid) else 1.0
        assert studio < 0.25, f"{name}: the card is the studio grey, not the hull ({studio:.0%} of it)"
    for name in names:
        if name not in SHEET:
            print(f"  note: {name} has no contact sheet; its card icon stands in")
    assert f"guid: {guid(SCRIPT)}, type: 3" in so, "the art table is not a HangarCardArtSO"


def drift(files):
    stale = []
    for rel, data in files.items():
        full = os.path.join(REPO, rel)
        if not os.path.exists(full):
            stale.append(rel)
            continue
        old = open(full, "rb").read()
        if old == data:
            continue
        if rel.endswith(".png"):
            a = np.asarray(Image.open(io.BytesIO(old)).convert("RGBA")).astype(float)
            b = np.asarray(Image.open(io.BytesIO(data)).convert("RGBA")).astype(float)
            if a.shape == b.shape and np.abs(a - b).mean() <= 2.0:
                continue
        stale.append(rel)
    return stale


def self_test(files, sources):
    def blank(f):
        f[f"{OUT_DIR}/Sparrow.png"] = png_bytes(Image.new("RGBA", CANVAS, (0, 0, 0, 0)))

    def grey(f):
        im = Image.open(io.BytesIO(f[f"{OUT_DIR}/Manta.png"])).convert("RGBA")
        a = np.asarray(im).copy()
        a[..., :3] = (73, 76, 85)
        f[f"{OUT_DIR}/Manta.png"] = png_bytes(Image.fromarray(a, "RGBA"))

    def dropped(f):
        so = f[SO_PATH].decode()
        f[SO_PATH] = re.sub(r"  - vessel: [^\n]*\n    art: [^\n]*\n", "", so, count=1).encode()

    def resized(f):
        im = Image.open(io.BytesIO(f[f"{OUT_DIR}/Dolphin.png"])).resize((256, 256))
        f[f"{OUT_DIR}/Dolphin.png"] = png_bytes(im)

    plants = {"a card with nothing on it": blank, "a card painted the studio grey": grey,
              "a hull missing from the table": dropped, "a card on the wrong canvas": resized}
    ok = True
    for label, fn in plants.items():
        bad = dict(files)
        fn(bad)
        try:
            validate(bad, sources)
            print(f"  MISSED: {label}")
            ok = False
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
    print("OK: every planted defect is caught" if ok else "FAIL: a planted defect slipped through")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    files, sources = outputs()
    validate(files, sources)
    print(f"OK: {len(sources)} Hangar card art images and the art table validate")
    if a.self_test:
        return 0 if self_test(files, sources) else 1
    stale = drift(files)
    if a.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for rel in stale:
        full = os.path.join(REPO, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "wb") as fh:
            fh.write(files[rel])
        print(f"wrote {rel}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
