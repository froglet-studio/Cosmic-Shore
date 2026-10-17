#!/usr/bin/env python3
"""
Author the LATTICE materials (Docs/MENU_REDESIGN_B.md §45.2): the living crystal lattice that replaced the
blueprint grid (kit_grid) behind style-B panels and windows.

    Assets/_Graphics/Materials/Shaders/UI/Lattice.shader     hand-written; UI/Default plumbing + UILattice.cginc
    Assets/_Graphics/Materials/Shaders/UI/UILattice.cginc    hand-written; the maths (Tools/Build/lattice_preview.py
                                                             compiles and renders it with clang)
    Lattice_Panel.mat     one finer layer for panels (the Ark recipe, leaderboard, Profile card, Toy Box cards...)
    Lattice_Window.mat    two layers for a whole window's backdrop (Arcade, Arena, Mission, Configure, Toy Box)

This writes the two materials and the shader / include metas (deterministic guids), so every reference the menu
generators emit (menu_b_motion.grid_node / window_lattice_node) resolves. The element colours are the theme's
(UIThemeSO element fields, AbilityLockupStyleSO's colours).

    python3 Tools/Build/author_ui_lattice.py              # write
    python3 Tools/Build/author_ui_lattice.py --check      # fail if an output drifted
    python3 Tools/Build/author_ui_lattice.py --self-test  # planted defects must all be caught
"""
import argparse
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
DIR = "Assets/_Graphics/Materials/Shaders/UI"
SHADER = f"{DIR}/Lattice.shader"
CGINC = f"{DIR}/UILattice.cginc"


def guid(key):
    return hashlib.md5(f"CosmicShore/MenuB/lattice/{key}".encode()).hexdigest()


SHADER_GUID = guid("shader")
CGINC_GUID = guid("cginc")
PANEL_GUID = guid("panel_mat")
WINDOW_GUID = guid("window_mat")

ELEMENTS = {"_ChargeColor": (1, 0.827451, 0.3019608), "_MassColor": (0.49803922, 0.65882355, 1),
            "_SpaceColor": (0.6901961, 0.4862745, 1), "_TimeColor": (0.37254903, 0.9411765, 0.78431374)}
LINE = (0.2627451, 0.29803923, 0.5372549)          # SurfaceLight #434C89

# name: (floats). Every property the shader declares is written, so Unity never re-serializes the asset.
MATERIALS = {
    "Lattice_Panel": dict(_LineWidth=1.1, _NodeSize=2.2, _NearSpacing=64, _NearAlpha=0.5, _NearDrift=40,
                          _FarSpacing=36, _FarAlpha=0, _FarDrift=60, _PulseDensity=0.25, _PulseSpeed=150,
                          _PulseRepeat=900, _PulseLength=70, _TwinkleDensity=0.06, _FadeStart=0.6, _Reveal=1),
    "Lattice_Window": dict(_LineWidth=1.1, _NodeSize=2.4, _NearSpacing=130, _NearAlpha=0.42, _NearDrift=55,
                           _FarSpacing=72, _FarAlpha=0.2, _FarDrift=90, _PulseDensity=0.22, _PulseSpeed=240,
                           _PulseRepeat=1400, _PulseLength=120, _TwinkleDensity=0.05, _FadeStart=0.55, _Reveal=1),
}
MAT_GUID = {"Lattice_Panel": PANEL_GUID, "Lattice_Window": WINDOW_GUID}
KIT_GRID = "21a4d02295ff993cb1746d4d792daa21"       # the retired blueprint grid sprite (kit_grid.png)
SCREENS = "Assets/_Prefabs/UI Elements/Main Menu Screens"
WINDOWS = [f"{SCREENS}/{p}" for p in ("Arcade/ArcadeView.prefab", "Arena/ArenaView.prefab", "Mission/MissionView.prefab",
                                       "Toybox/ToyboxView.prefab", "Configure/ConfigureView.prefab")]
UI_FLOATS = dict(_ColorMask=15, _Stencil=0, _StencilComp=8, _StencilOp=0, _StencilReadMask=255,
                 _StencilWriteMask=255, _UseUIAlphaClip=0)


def f(x):
    x = float(x)
    return str(int(x)) if x == int(x) else repr(round(x, 7)).rstrip("0").rstrip(".")


def col(rgb, a=1):
    return "{r: %s, g: %s, b: %s, a: %s}" % (f(rgb[0]), f(rgb[1]), f(rgb[2]), f(a))


def material(name):
    floats = dict(MATERIALS[name], **UI_FLOATS)
    colors = dict(_Color=(1, 1, 1), _LineColor=LINE, **ELEMENTS)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_Name: {name}\n"
            f"  m_Shader: {{fileID: 4800000, guid: {SHADER_GUID}, type: 3}}\n  m_Parent: {{fileID: 0}}\n"
            "  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n"
            "  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n"
            "  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n"
            "    serializedVersion: 3\n    m_TexEnvs:\n    - _MainTex:\n        m_Texture: {fileID: 0}\n"
            "        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n    m_Ints: []\n    m_Floats:\n"
            + "".join(f"    - {k}: {f(v)}\n" for k, v in sorted(floats.items()))
            + "    m_Colors:\n" + "".join(f"    - {k}: {col(v)}\n" for k, v in sorted(colors.items()))
            + "  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n")


def mat_meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def outputs():
    out = {
        f"{SHADER}.meta": (f"fileFormatVersion: 2\nguid: {SHADER_GUID}\nShaderImporter:\n  externalObjects: {{}}\n"
                           "  defaultTextures: []\n  nonModifiableTextures: []\n  preprocessorOverride: 0\n"
                           "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"),
        f"{CGINC}.meta": (f"fileFormatVersion: 2\nguid: {CGINC_GUID}\nShaderIncludeImporter:\n  externalObjects: {{}}\n"
                          "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"),
    }
    for name in MATERIALS:
        out[f"{DIR}/{name}.mat"] = material(name)
        out[f"{DIR}/{name}.mat.meta"] = mat_meta(MAT_GUID[name])
    return out


def shipped_prefabs():
    """The style-B screen prefabs as committed (the menu generators own them; this reads them)."""
    import glob
    return {os.path.relpath(p, REPO): open(p, encoding="utf-8").read()
            for p in glob.glob(os.path.join(REPO, SCREENS, "**", "*.prefab"), recursive=True)}


def validate_screens(prefabs):
    """Rev 6: no style-B screen draws the blueprint grid any more, and every window's backdrop carries the lattice."""
    gridded = sorted(p for p, t in prefabs.items() if f"guid: {KIT_GRID}" in t)
    assert not gridded, f"the blueprint grid is still drawn in {gridded}"
    for w in WINDOWS:
        assert f"m_Material: {{fileID: 2100000, guid: {WINDOW_GUID}, type: 2}}" in prefabs.get(w, ""), \
            f"{w} has no window lattice behind it"


def validate(files):
    shader = open(os.path.join(REPO, SHADER), encoding="utf-8").read()
    declared = set(re.findall(r"^\s+(?:\[[^\]]*\]\s*)*(_\w+) \(", shader, re.M)) - {"_MainTex"}
    assert '#include "UILattice.cginc"' in shader, "Lattice.shader does not include UILattice.cginc"
    for name in MATERIALS:
        m = files[f"{DIR}/{name}.mat"]
        assert f"m_Shader: {{fileID: 4800000, guid: {SHADER_GUID}, type: 3}}" in m, f"{name} does not use Lattice.shader"
        written = set(re.findall(r"^    - (_\w+):", m, re.M)) - {"_MainTex"}
        assert written == declared, f"{name}: properties drifted from the shader (missing {declared - written}, extra {written - declared})"
        assert f"guid: {MAT_GUID[name]}" in files[f"{DIR}/{name}.mat.meta"], f"{name}.mat.meta has the wrong guid"
    assert f"guid: {SHADER_GUID}" in files[f"{SHADER}.meta"], "Lattice.shader.meta has the wrong guid"
    for k, rgb in ELEMENTS.items():
        assert col(rgb) in files[f"{DIR}/Lattice_Window.mat"], f"the lattice's {k} is not the theme's element colour"
    validate_screens(files.get("__screens__") or shipped_prefabs())


def drift(files):
    bad = []
    for p, t in files.items():
        if p.startswith("__"):
            continue
        full = os.path.join(REPO, p)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != t:
            bad.append(p)
    return bad


def self_test(files):
    cases = [
        ("a material on the wrong shader", lambda f: f.__setitem__(f"{DIR}/Lattice_Panel.mat", f[f"{DIR}/Lattice_Panel.mat"].replace(SHADER_GUID, "0" * 32))),
        ("a property the shader no longer declares", lambda f: f.__setitem__(f"{DIR}/Lattice_Window.mat", f[f"{DIR}/Lattice_Window.mat"].replace(
            "    - _FadeStart:", "    - _Retired: 1\n    - _FadeStart:", 1))),
        ("Charge off the theme colour", lambda f: f.__setitem__(f"{DIR}/Lattice_Window.mat", f[f"{DIR}/Lattice_Window.mat"].replace(
            col(ELEMENTS["_ChargeColor"]), col((0, 0.83, 1)), 1))),
        ("the shader meta on another guid", lambda f: f.__setitem__(f"{SHADER}.meta", f[f"{SHADER}.meta"].replace(SHADER_GUID, "1" * 32))),
    ]
    screens = shipped_prefabs()
    cases += [
        ("a screen still drawing the grid", lambda f: f.__setitem__("__screens__", dict(screens, **{
            WINDOWS[0]: screens[WINDOWS[0]] + f"  m_Sprite: {{fileID: 21300000, guid: {KIT_GRID}, type: 3}}\n"}))),
        ("a window without its lattice", lambda f: f.__setitem__("__screens__", dict(screens, **{
            WINDOWS[2]: screens[WINDOWS[2]].replace(WINDOW_GUID, "2" * 32)}))),
    ]
    ok = True
    for label, mutate in cases:
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
            print(f"  MISSED: {label}")
            ok = False
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
    assert ok, "a planted defect was not caught"
    print("OK: every negative control fires")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    if args.self_test:
        self_test(files)
        return
    if args.check:
        bad = drift(files)
        for p in bad:
            print(f"DRIFT: {p}")
        if bad:
            sys.exit("FAIL: re-run author_ui_lattice.py")
        print("OK: no drift")
        return
    for p, t in files.items():
        with open(os.path.join(REPO, p), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    print(f"wrote {len(files)} files")


if __name__ == "__main__":
    main()
