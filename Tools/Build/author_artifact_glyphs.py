#!/usr/bin/env python3
"""The artifact kinds' marks for the Ark's Artifacts rail (Rev 105 B, Docs/MENU_REDESIGN_B.md sections 193 and 201).

One white, stroked mark per kind, drawn from the room's own prism shapes (the Artifacts room's backdrop shows the
same four on their pedestals): Javelin a long diamond, Hydra three small diamonds in a fan, Aftershock two diamonds
stacked, Aegis a plate within a plate (a hexagon in a hexagon). White so the rail's themed tint colours it (every colour from the colour set).

    Assets/_Graphics/Icons/Artifacts/artifact_<kind>.png (+ .meta, sprite importer cloned from the UI kit's)

    python3 Tools/Build/author_artifact_glyphs.py            # write
    python3 Tools/Build/author_artifact_glyphs.py --check    # fail if any output drifted
"""
import argparse
import hashlib
import importlib.util
import io
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
_spec = importlib.util.spec_from_file_location("author_ui_kit_sprites", os.path.join(HERE, "author_ui_kit_sprites.py"))
KS = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(KS)

OUT = "Assets/_Graphics/Icons/Artifacts"
SIZE, SS, STROKE = 256, 4, 7          # texels, supersample, stroke in texels

# Each kind's mark in a 100 x 100 box (the board's GLYPH, r105_artifacts.py): closed polygons, stroked.
GLYPHS = {
    "javelin": [[(50, 2), (60, 50), (50, 98), (40, 50)], [(50, 2), (50, 98)]],
    "hydra": [[(50, 6), (61, 28), (50, 50), (39, 28)], [(22, 40), (33, 62), (22, 84), (11, 62)],
              [(78, 40), (89, 62), (78, 84), (67, 62)]],
    "aftershock": [[(50, 4), (67, 27), (50, 50), (33, 27)], [(50, 50), (67, 73), (50, 96), (33, 73)]],
    "aegis": [[(50, 4), (90, 27), (90, 73), (50, 96), (10, 73), (10, 27)], [(50, 26), (71, 38), (71, 62), (50, 74), (29, 62), (29, 38)]],
}


def guid_for(name):
    return hashlib.md5(f"CosmicShore/ArtifactGlyph/{name}".encode()).hexdigest()


def render(polys):
    big = SIZE * SS
    pad = 0.08 * big
    scale = (big - 2 * pad) / 100.0
    img = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(img)
    for poly in polys:
        pts = [(pad + x * scale, pad + y * scale) for x, y in poly]
        closed = pts + [pts[0]] if len(pts) > 2 else pts
        d.line(closed, fill=255, width=STROKE * SS, joint="curve")
    alpha = img.resize((SIZE, SIZE), Image.LANCZOS)
    rgba = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    rgba.putalpha(alpha)
    buf = io.BytesIO()
    rgba.save(buf, "PNG", optimize=True)
    return buf.getvalue()


def outputs():
    files = {}
    for kind, polys in GLYPHS.items():
        name = f"artifact_{kind}"
        files[f"{OUT}/{name}.png"] = render(polys)
        files[f"{OUT}/{name}.png.meta"] = KS.unity_empty_scalars(KS.META.format(
            guid=guid_for(name), sprite_id=guid_for(name + "/sprite"), ppu=100,
            bl=0, bb=0, br=0, bt=0, max_size=SIZE)).encode()
    files[OUT + ".meta"] = KS.unity_empty_scalars(KS.FOLDER_META.format(guid=guid_for("__folder__"))).encode()
    return files


def glyph_guid(kind):
    """The sprite guid of a kind's mark (author_menu_ark.py wires it)."""
    return guid_for(f"artifact_{kind}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    drift = []
    for rel, data in outputs().items():
        path = os.path.join(REPO, rel)
        old = open(path, "rb").read() if os.path.exists(path) else None
        if old == data:
            continue
        if args.check:
            drift.append(rel)
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as fh:
            fh.write(data)
        print("wrote", rel)
    if drift:
        print("drift:", *drift, sep="\n  ")
        sys.exit(1)
    print("artifact glyphs:", "OK" if args.check else "written")


if __name__ == "__main__":
    main()
