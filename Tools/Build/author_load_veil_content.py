#!/usr/bin/env python3
"""
Author the CELL-CHANGE LOADING SCREEN's content (Rev 7 of the style-B menu, Docs/MENU_REDESIGN_B.md section 47.4; the
approved "Loading" board; the owner, 2026-10-17: "Black loading screen when changing cell show something interesting").

    Assets/Resources/UI/EnvironmentLoadVeilContent.asset   (EnvironmentLoadVeilContentSO)
        Every word EnvironmentLoadVeil shows - "Entering", "Loading", "Tip", the four build steps, a line under each
        freestyle cell's name, the tips - plus the living lattice behind it (Lattice_Window.mat), the four element
        glyphs the steps wear (Mass, Time, Charge, Space) and the two kit fonts. The veil is built in code, so it reads
        this asset from Resources.

The words are the authored source: a human edits them in the inspector afterwards and this script's --check then
reports the drift (re-run it only to reset them). The checks hold what the veil relies on:
  - four step labels, none blank, and the glyphs in step order (Mass, Time, Charge, Space);
  - every cell line names a cell a CellConfigDataSO really carries (a renamed cell's line would never show);
  - every tip and line fits the UI font (ASCII) and one line on screen;
  - the lattice, the glyphs and the fonts resolve.

    python3 Tools/Build/author_load_veil_content.py              # write
    python3 Tools/Build/author_load_veil_content.py --check      # fail if the asset drifted
    python3 Tools/Build/author_load_veil_content.py --self-test  # planted defects must all be caught
"""

import argparse
import glob
import hashlib
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, f"{name}.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


K = _load("author_ui_kit_prefabs")
H = _load("author_menu_hangar")
M = _load("menu_b_motion")
REPO = K.REPO

ASSET = "Assets/Resources/UI/EnvironmentLoadVeilContent.asset"
SCRIPT = "Assets/_Scripts/Controller/Environment/Spawning/EnvironmentLoadVeilContentSO.cs"
CELL_CONFIGS = "Assets/_SO_Assets/Cell Configs/**/*.asset"
ELEMENTS = ("Mass", "Time", "Charge", "Space")

WORDS = dict(enteringWord="Entering", loadingWord="Loading", tipLabel="Tip")
# The real build phases (EnvironmentLoadVeil.StepFor): laying, growing, the all-clear hold, the reveal. The board's
# "Growing flora" / "Waking fauna" named phases the build does not have; these say what is happening.
STEPS = ("Laying prisms", "Growing prisms", "Settling the cell", "Opening the cell")
# The freestyle cells' own short flavour lines (CellConfigDataSO.Description, where it is one), so the screen says
# what the player is flying into. A cell not listed shows its name alone.
CELL_LINES = (
    ("Garland", "A flowering bough wound twice around the seed of the world"),
    ("Geode", "A crystal cathedral cracked open to the void"),
    ("Zephyr", "A painted sky of wind rivers and cyclones"),
    ("Yggdra", "The World-Tree - a garden grown around a colossal braided trunk"),
    ("Hesperides", "The Garden - terraced beds, pergolas and hanging baskets"),
    ("Ourobor", "One-sided country - cities on both faces, no up anywhere"),
    ("Orrery", "Flying inside a celestial clock"),
    ("Daedala", "An Escher city of roads, arches, and floating isles"),
    ("Caldera", "Four forges ringing the core - lava runs for the brave"),
)
# Only what the game does today (each checked in its code when written, 2026-10-17).
TIPS = (
    "LT and RT slide between the Toy Box, Arcade, Arena and Mission windows.",   # ScreenSwitcher.SlideHubWindow
    "Rest on a pilot's picture in the friends panel to see where they are.",      # FriendProfilePopup
    "The Toy Box's shuffle re-rolls the world, your domain and your hull.",       # ToyboxShuffleCard
)
MAX_LINE = 72   # characters: one line at the screen's 16-17 px sizes inside 1200 units


def asset_guid():
    return hashlib.md5(b"CosmicShore/LoadVeil/content").hexdigest()


def q(s):
    """A YAML scalar as Unity writes one: plain when it can be, single-quoted otherwise."""
    if s == "" or re.search(r"(: | #|^[\s\-?:,\[\]{}#&*!|>'\"%@`]|\s$)", s):
        return "'" + s.replace("'", "''") + "'"
    return s


def ref(fid, guid, kind):
    return f"{{fileID: {fid}, guid: {guid}, type: {kind}}}"


def asset_text(words=WORDS, steps=STEPS, lines=CELL_LINES, tips=TIPS, glyph_order=ELEMENTS):
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
           "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
           "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
           f"  m_Script: {ref(11500000, K.meta_guid(SCRIPT), 3)}", "  m_Name: EnvironmentLoadVeilContent",
           "  m_EditorClassIdentifier: "]
    for k, v in words.items():
        out.append(f"  {k}: {q(v)}")
    out.append("  stepLabels:")
    out += [f"  - {q(s)}" for s in steps]
    out.append("  cellLines:")
    for name, line in lines:
        out += [f"  - cellName: {q(name)}", f"    line: {q(line)}"]
    out.append("  tips:")
    out += [f"  - {q(t)}" for t in tips]
    out.append(f"  lattice: {ref(2100000, M.LATTICE_WINDOW_MAT, 2)}")
    out.append("  stepGlyphs:")
    out += [f"  - {K.sprite_ref(H.element_glyph(e))}" for e in glyph_order]
    out.append(f"  displayFont: {ref(K.ALDRICH[1], K.ALDRICH[0], 2)}")
    out.append(f"  captionFont: {ref(K.SEMIBOLD[1], K.SEMIBOLD[0], 2)}")
    return "\n".join(out) + "\n"


def meta_text():
    return (f"fileFormatVersion: 2\nguid: {asset_guid()}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def outputs():
    return {ASSET: asset_text(), ASSET + ".meta": meta_text()}


def cell_names():
    names = set()
    for p in glob.glob(os.path.join(REPO, CELL_CONFIGS), recursive=True):
        m = re.search(r"^  CellName: (.+)$", open(p, encoding="utf-8").read(), re.M)
        if m:
            names.add(m.group(1).strip().strip("'\""))
    return names


_GUIDS = None


def known_guids():
    """Every guid a .meta under Assets owns (read once)."""
    global _GUIDS
    if _GUIDS is None:
        _GUIDS = set()
        for root, _dirs, names in os.walk(os.path.join(REPO, "Assets")):
            for n in names:
                if n.endswith(".meta"):
                    with open(os.path.join(root, n), errors="ignore") as fh:
                        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(400), re.M)
                    if m:
                        _GUIDS.add(m.group(1))
    return _GUIDS


def block(text, field):
    m = re.search(rf"^  {field}:\n((?:  [- ].*\n)*)", text, re.M)
    assert m, f"the asset has no {field}"
    return m.group(1)


def unq(s):
    s = s.strip()
    return s[1:-1].replace("''", "'") if s.startswith("'") else s


def validate(files, known=None):
    text = files[ASSET]
    known = known if known is not None else cell_names()
    assert f"guid: {K.meta_guid(SCRIPT)}, type: 3}}" in text, "the asset is not an EnvironmentLoadVeilContentSO"
    for k in WORDS:
        m = re.search(rf"^  {k}: (.*)$", text, re.M)
        assert m and unq(m.group(1)), f"{k} is blank (the screen would show nothing there)"
    steps = [unq(s[4:]) for s in block(text, "stepLabels").splitlines()]
    assert len(steps) == 4 and all(steps), f"the build needs four named steps, found {steps}"
    lines = re.findall(r"^  - cellName: (.*)\n    line: (.*)$", block(text, "cellLines"), re.M)
    for name, line in lines:
        assert unq(name) in known, f"a line names the cell {unq(name)!r}, which no CellConfigDataSO carries (it would never show)"
        assert len(unq(line)) <= MAX_LINE, f"{unq(name)}'s line is longer than one line ({len(unq(line))} > {MAX_LINE})"
    tips = [unq(s[4:]) for s in block(text, "tips").splitlines()]
    assert tips, "no tips"
    for t in tips:
        assert t and len(t) <= MAX_LINE, f"a tip is blank or longer than one line: {t!r}"
    assert text.isascii(), "non-ASCII text (the UI font has 97 glyphs)"
    glyphs = re.findall(r"guid: ([0-9a-f]{32})", block(text, "stepGlyphs"))
    assert glyphs == [H.element_glyph(e) for e in ELEMENTS], "the step glyphs are not Mass, Time, Charge, Space in step order"
    assert f"  lattice: {ref(2100000, M.LATTICE_WINDOW_MAT, 2)}" in text, "the lattice is not Lattice_Window.mat"
    owned = known_guids()
    for g in [M.LATTICE_WINDOW_MAT, K.ALDRICH[0], K.SEMIBOLD[0]] + glyphs:
        assert g in owned, f"guid {g} resolves to no asset"


def self_test(files):
    ok = True
    plants = {
        "a line for a cell that no longer exists": asset_text(lines=CELL_LINES + (("Garlandia", "Nowhere"),)),
        "a blank build step": asset_text(steps=STEPS[:2] + ("",) + STEPS[3:]),
        "a tip the font cannot draw": asset_text(tips=TIPS[:1] + ("Fly to the core — then turn.",)),
        "the glyphs out of step order": asset_text(glyph_order=("Time", "Mass", "Charge", "Space")),
        "the loading word blanked": asset_text(words=dict(WORDS, loadingWord="")),
    }
    for label, bad in plants.items():
        try:
            validate({ASSET: bad})
            print(f"  MISSED: {label}")
            ok = False
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    if args.self_test:
        return 0 if self_test(files) else 1
    if args.check:
        bad = [p for p, t in files.items()
               if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]
        for p in bad:
            print("DRIFT:", p)
        print(f"FAIL: {len(bad)} file(s) differ" if bad else "OK: no drift")
        return 1 if bad else 0
    for p, t in files.items():
        with open(os.path.join(REPO, p), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    print(f"wrote {len(files)} file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
