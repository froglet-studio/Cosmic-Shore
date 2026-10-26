#!/usr/bin/env python3
"""Author a hull's ability-row icons through the kit: spec -> render -> verify -> sheet -> write.

    python3 Tools/Build/icon_kit/author.py manta            # render, gate, sheet, write the PNGs (metas kept)
    python3 Tools/Build/icon_kit/author.py manta --check    # re-render and compare to the committed PNGs (drift gate)
    python3 Tools/Build/icon_kit/author.py manta --preview  # render, gate and sheet only; nothing under Assets/ changes
    python3 Tools/Build/icon_kit/author.py --all [--check]  # every spec in specs/

A spec (specs/<hull>.py) is HULL plus ICONS = [dict(name, element, act, draw)], where draw() returns a mask or
[(mask, alpha), ...] in icon px, plus optional CARDS (states.py): each card's states as coloured parts, drawn to
Docs/Icons/sheets/<hull>_<card>.png on every run. An icon may carry exempt={"gate": "who said so and why"}: the gate still runs and
prints, it just does not block the write (a hull's real silhouette fails `gap` on its ribs by design). The sheet lands in Docs/Icons/sheets/<hull>.png with the icon as HEAD last committed it as BEFORE
(so a write does not erase its own comparison; a new icon has no BEFORE). The .meta is never rewritten for an icon that has one - the
prefab's binding keeps its guid; a NEW icon gets the house sprite meta with a deterministic guid.
"""
import hashlib
import importlib
import os
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
import canvas  # noqa: E402
import rebind  # noqa: E402
import sheet as sheet_mod  # noqa: E402
import verify  # noqa: E402

ICON_ROOT = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons")
DONOR_META = os.path.join(ICON_ROOT, "Manta", "Manta_Kabloom.png.meta")   # the house 128 px sprite meta
SHEETS = os.path.join(ROOT, "Docs", "Icons", "sheets")
FOLDER_META = "fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
TOLERANCE = 2


def guid_for(hull, name):
    return hashlib.md5(f"CosmicShore/IconKit/{hull}/{name}".encode()).hexdigest()


def sprite_meta(guid):
    s = open(DONOR_META).read()
    lines = s.splitlines()
    lines[1] = f"guid: {guid}"
    return "\n".join(lines) + "\n"


def load_spec(hull):
    return importlib.import_module(f"specs.{hull.lower()}")


def render_all(spec):
    out = []
    for icon in spec.ICONS:
        layers = icon["draw"]()
        out.append((icon, canvas.render(layers)))
    return out


def gate(rendered):
    """Run the legibility gates on the renders (via temp PNGs, so the gate measures what will be written).
    The distinct-within-hull gate runs over the icons that sit in one folder and are not a LADDER (family=False:
    a gauge's rungs are each other's variants on purpose)."""
    bad = 0
    with tempfile.TemporaryDirectory() as td:
        family = []
        for icon, im in rendered:
            folder = os.path.join(td, icon.get("subdir", ""))
            os.makedirs(folder, exist_ok=True)
            p = os.path.join(folder, icon["name"] + ".png")
            im.save(p)
            r = verify.measure(p)
            fails = verify.judge(r)
            exempt = [f for f in fails if f in icon.get("exempt", {})]
            fails = [f for f in fails if f not in exempt]
            bad += bool(fails)
            print("  " + verify.fmt(r, fails) + "".join(f"  [exempt {e}: {icon['exempt'][e]}]" for e in exempt))
            if icon.get("family", True):
                family.append(p)
        for a, b, iou in verify.judge_family(family):
            print(f"  FAIL distinct: {a} vs {b} IoU {iou:.2f} > {verify.PAIR_IOU_MAX}")
            bad += 1
    return bad


def committed(png, before=None):
    """The icon as the branch last COMMITTED it (so the sheet's BEFORE survives a write), else the file on disk.
    A NEW icon names the sprite it replaces as `before` (repo-relative), so the sheet still shows what it was."""
    import io
    import subprocess
    if before and not os.path.exists(png) and not _in_head(png):
        png = os.path.join(ROOT, before)
    rel = os.path.relpath(png, ROOT)
    try:
        data = subprocess.run(["git", "-C", ROOT, "show", f"HEAD:{rel}"], capture_output=True, check=True).stdout
        return Image.open(io.BytesIO(data)).convert("RGBA")
    except (subprocess.CalledProcessError, OSError):
        return Image.open(png).convert("RGBA") if os.path.exists(png) else None


def _in_head(png):
    import subprocess
    rel = os.path.relpath(png, ROOT)
    return subprocess.run(["git", "-C", ROOT, "cat-file", "-e", f"HEAD:{rel}"], capture_output=True).returncode == 0


def same_pixels(a, b):
    x = np.asarray(a.convert("RGBA"), dtype=np.int16)
    y = np.asarray(b.convert("RGBA"), dtype=np.int16)
    return x.shape == y.shape and int(np.abs(x - y).max()) <= TOLERANCE


def run(hull, check=False, preview=False):
    spec = load_spec(hull)
    out_dir = os.path.join(ICON_ROOT, spec.HULL)
    rendered = render_all(spec)
    print(f"{spec.HULL}: gates")
    bad = gate(rendered)
    rows = []
    drift = 0
    for icon, im in rendered:
        png = os.path.join(out_dir, icon.get("subdir", ""), icon["name"] + ".png")
        before = committed(png, icon.get("before"))
        rows.append((icon["name"], icon["element"], before, im))
        if check:
            on_disk = Image.open(png).convert("RGBA") if os.path.exists(png) else None
            if on_disk is None or not same_pixels(on_disk, im):
                print(f"  DRIFT {icon['name']}.png")
                drift += 1
    sheet_path = sheet_mod.before_after(rows, os.path.join(SHEETS, f"{spec.HULL.lower()}.png"),
                                        title=f"{spec.HULL} ability icons - before / after (icon_kit)")
    print(f"  sheet: {os.path.relpath(sheet_path, ROOT)}")
    # a card's STATES sheet, from the spec's CARDS (states.py): the card as the HUD draws it, before and after
    import states as states_mod
    for c in getattr(spec, "CARDS", []):
        p = states_mod.sheet(spec, c, os.path.join(SHEETS, f"{spec.HULL.lower()}_{c['name']}.png"))
        print(f"  states: {os.path.relpath(p, ROOT)}")
    if check:
        if getattr(spec, "REBIND", None) and not drift:
            metas = {i["name"]: os.path.join(out_dir, i.get("subdir", ""), i["name"] + ".png.meta") for i in spec.ICONS}
            if all(os.path.exists(m) for m in metas.values()):
                for f in rebind.apply(ROOT, spec.REBIND, metas, check=True):
                    print(f"  DRIFT binding in {f}")
                    drift += 1
        print(f"  {'no drift' if not drift else str(drift) + ' icon(s) drifted'}")
        return 1 if (drift or bad) else 0
    if preview:
        return 1 if bad else 0
    if bad:
        print("  NOT WRITTEN: a gate failed (fix the spec, or argue the gate in verify.py with a negative control)")
        return 1
    metas = {}
    for icon, im in rendered:
        folder = os.path.join(out_dir, icon.get("subdir", ""))
        os.makedirs(folder, exist_ok=True)
        folder_meta = folder.rstrip("/") + ".meta"
        if not os.path.exists(folder_meta):
            open(folder_meta, "w").write(FOLDER_META.format(guid=guid_for(spec.HULL, os.path.relpath(folder, ICON_ROOT) + ".folder")))
            print(f"  wrote {os.path.relpath(folder_meta, ROOT)} (new folder guid)")
        png = os.path.join(folder, icon["name"] + ".png")
        im.save(png, optimize=True)
        meta = png + ".meta"
        if not os.path.exists(meta):
            open(meta, "w").write(sprite_meta(guid_for(spec.HULL, icon["name"])))
            print(f"  wrote {os.path.relpath(meta, ROOT)} (new guid)")
        metas[icon["name"]] = meta
        print(f"  wrote {os.path.relpath(png, ROOT)}")
    for f in rebind.apply(ROOT, getattr(spec, "REBIND", []), metas):
        print(f"  rebound {f}")
    return 0


def main(argv):
    check = "--check" in argv
    preview = "--preview" in argv
    hulls = [a for a in argv if not a.startswith("--")]
    if "--all" in argv:
        hulls = sorted(f[:-3] for f in os.listdir(os.path.join(HERE, "specs")) if f.endswith(".py") and f != "__init__.py")
    if not hulls:
        print(__doc__)
        return 2
    rc = 0
    for h in hulls:
        rc |= run(h, check=check, preview=preview)
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
