#!/usr/bin/env python3
"""
Author the LATTICE materials (Docs/MENU_REDESIGN_B.md §45.2): the living crystal lattice that replaced the
blueprint grid (kit_grid) behind style-B panels and windows.

    Assets/_Graphics/Materials/Shaders/UI/Lattice.shader     hand-written; UI/Default plumbing + UILattice.cginc
    Assets/_Graphics/Materials/Shaders/UI/UILattice.cginc    hand-written; the maths (Tools/Build/lattice_preview.py
                                                             compiles and renders it with clang)
    Lattice_Panel.mat     one finer layer for panels (the Ark recipe, leaderboard, Profile card, Toy Box cards...)
    Lattice_Window.mat    two layers for a whole window's backdrop (Arcade, Arena, Mission, Configure, Toy Box)

It also writes the rev 6 rims (ElementRim.shader, §45.3: ElementRim_<element>.mat) and the presence orb and party
bonds (§45.4): Presence.shader / Bond.shader on UIPresence.cginc, Presence_Orb.mat (the friends orb in Menu_Main)
and Bond_Party.mat (the party molecule's bonds in PartyPanel.prefab).

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
RIM_SHADER = f"{DIR}/ElementRim.shader"
RIM_CGINC = f"{DIR}/UIElementRim.cginc"
PRESENCE_SHADER = f"{DIR}/Presence.shader"
BOND_SHADER = f"{DIR}/Bond.shader"
PRESENCE_CGINC = f"{DIR}/UIPresence.cginc"


def guid(key):
    return hashlib.md5(f"CosmicShore/MenuB/lattice/{key}".encode()).hexdigest()


SHADER_GUID = guid("shader")
CGINC_GUID = guid("cginc")
RIM_SHADER_GUID = guid("rim_shader")
RIM_CGINC_GUID = guid("rim_cginc")
PRESENCE_SHADER_GUID = guid("presence_shader")
BOND_SHADER_GUID = guid("bond_shader")
PRESENCE_CGINC_GUID = guid("presence_cginc")
PRESENCE_GUID = guid("presence_mat")
BOND_GUID = guid("bond_mat")
# The elemental rims (§45.3): one material per element plus omni. menu_b_motion.rim_border emits these guids.
RIMS = {"Charge": (1, 0.827451, 0.3019608), "Mass": (0.49803922, 0.65882355, 1), "Space": (0.6901961, 0.4862745, 1),
        "Time": (0.37254903, 0.9411765, 0.78431374), "Omni": None}
RIM_GUID = {e: guid(f"rim_{e.lower()}_mat") for e in RIMS}
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
# The presence orb (§45.4). Its member seats sit on the inner ring at these angles (degrees, counter-clockwise from
# the right): the shader's bonds (_BondAngles, _MemberRadius) and the drawer generator's avatar positions
# (author_menu_friends_drawer.py) both read them, so a bond always ends under its avatar.
ORB_MEMBER_ANGLES = (135, 45, 270)
ORB_MEMBER_RADIUS = 0.66
PRESENCE = dict(_GlowAlpha=0.32, _InnerRadius=ORB_MEMBER_RADIUS, _InnerAlpha=0.34, _OuterRadius=0.92, _OuterAlpha=0.55,
                _RingWidth=0.012, _Soft=0.03, _Dashes=28, _SpinSeconds=40, _Online=0, _DotSize=0.055,
                _MemberRadius=ORB_MEMBER_RADIUS, _BondWidth=0.022, _BondPeriod=0.16, _BondSpeed=0.22)
BOND = dict(_Width=1.4, _Soft=1, _Period=18, _Speed=26)
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


def shader_material(name, shader_guid, floats, colors):
    """A UI material on another of these shaders: the lattice material's layout, its own name, shader and values.
    `colors` values are (r, g, b) or (r, g, b, a) - Unity keeps Vector properties among the colours too."""
    m = material("Lattice_Window").replace("m_Name: Lattice_Window", f"m_Name: {name}")
    m = m.replace(f"guid: {SHADER_GUID}", f"guid: {shader_guid}")
    head, _ = m.split("    m_Floats:\n", 1)
    return (head + "    m_Floats:\n" + "".join(f"    - {k}: {f(v)}\n" for k, v in sorted(dict(floats, **UI_FLOATS).items()))
            + "    m_Colors:\n" + "".join(f"    - {k}: {col(v[:3], v[3] if len(v) > 3 else 1)}\n" for k, v in sorted(colors.items()))
            + "  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n")


def rim_material(element):
    omni = RIMS[element] is None
    floats = dict(_Omni=1 if omni else 0, _Speed=0.08 if omni else 0.12, _Tail=0.3 if omni else 0.22,
                  _BaseLevel=0.7 if omni else 0.45)
    colors = dict(_Color=(1, 1, 1), _ElementColor=RIMS[element] or (1, 1, 1),
                  **{k: v for k, v in ELEMENTS.items()})
    return shader_material(f"ElementRim_{element}", RIM_SHADER_GUID, floats, colors)


def presence_material():
    clear = (0, 0, 0, 0)
    return shader_material("Presence_Orb", PRESENCE_SHADER_GUID, PRESENCE,
                           dict(_Color=(1, 1, 1), _SpaceColor=ELEMENTS["_SpaceColor"], _Bond0=clear, _Bond1=clear,
                                _Bond2=clear, _BondAngles=ORB_MEMBER_ANGLES + (0,)))


def bond_material():
    return shader_material("Bond_Party", BOND_SHADER_GUID, BOND, dict(_Color=(1, 1, 1)))


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
    out[f"{RIM_SHADER}.meta"] = out[f"{SHADER}.meta"].replace(SHADER_GUID, RIM_SHADER_GUID)
    out[f"{RIM_CGINC}.meta"] = out[f"{CGINC}.meta"].replace(CGINC_GUID, RIM_CGINC_GUID)
    for e in RIMS:
        out[f"{DIR}/ElementRim_{e}.mat"] = rim_material(e)
        out[f"{DIR}/ElementRim_{e}.mat.meta"] = mat_meta(RIM_GUID[e])
    out[f"{PRESENCE_SHADER}.meta"] = out[f"{SHADER}.meta"].replace(SHADER_GUID, PRESENCE_SHADER_GUID)
    out[f"{BOND_SHADER}.meta"] = out[f"{SHADER}.meta"].replace(SHADER_GUID, BOND_SHADER_GUID)
    out[f"{PRESENCE_CGINC}.meta"] = out[f"{CGINC}.meta"].replace(CGINC_GUID, PRESENCE_CGINC_GUID)
    out[f"{DIR}/Presence_Orb.mat"] = presence_material()
    out[f"{DIR}/Presence_Orb.mat.meta"] = mat_meta(PRESENCE_GUID)
    out[f"{DIR}/Bond_Party.mat"] = bond_material()
    out[f"{DIR}/Bond_Party.mat.meta"] = mat_meta(BOND_GUID)
    return out


def shipped_prefabs():
    """The style-B screen prefabs as committed (the menu generators own them; this reads them)."""
    import glob
    out = {os.path.relpath(p, REPO): open(p, encoding="utf-8").read()
           for p in glob.glob(os.path.join(REPO, SCREENS, "**", "*.prefab"), recursive=True)}
    out["Assets/_Scenes/Menu_Main.unity"] = open(os.path.join(REPO, "Assets/_Scenes/Menu_Main.unity"), encoding="utf-8").read()
    return out


RIM_SCRIPT = "b403ee3874d54f79d0f27e7013879c0f"      # KitRimMesh.cs
PARTY_PANEL = f"{SCREENS}/Party/PartyPanel.prefab"
RIMMED = {f"{SCREENS}/Ark/ArkView.prefab": "Omni", f"{SCREENS}/Configure/ConfigureView.prefab": "Charge",
          f"{SCREENS}/Hangar/HangarView.prefab": "Mass"}


def rim_pairs(text):
    """(gameObject, rim element) for every Image drawn with a material that reads KitRimMesh's UV1 (a rim, or "Bond" for
    the party bonds), and the GameObjects that carry KitRimMesh."""
    lit, meshed = [], set()
    by_guid = dict({g: e for e, g in RIM_GUID.items()}, **{BOND_GUID: "Bond"})
    for m in re.finditer(r"^--- !u!114 &-?\d+\nMonoBehaviour:\n((?:(?!--- ).*\n)*)", text, re.M):
        body = m.group(1)
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
        if not go:
            continue
        mat = re.search(r"m_Material: \{fileID: 2100000, guid: (\w+), type: 2\}", body)
        if mat and mat.group(1) in by_guid:
            lit.append((go.group(1), by_guid[mat.group(1)]))
        if f"guid: {RIM_SCRIPT}" in body:
            meshed.add(go.group(1))
    return lit, meshed


def validate_screens(prefabs):
    """Rev 6: no style-B screen draws the blueprint grid any more, and every window's backdrop carries the lattice."""
    gridded = sorted(p for p, t in prefabs.items() if f"guid: {KIT_GRID}" in t)
    assert not gridded, f"the blueprint grid is still drawn in {gridded}"
    for w in WINDOWS:
        assert f"m_Material: {{fileID: 2100000, guid: {WINDOW_GUID}, type: 2}}" in prefabs.get(w, ""), \
            f"{w} has no window lattice behind it"
    # The rims (§45.3): every rim-lit Image has KitRimMesh beside it, and each rimmed panel carries its element.
    for path, text in prefabs.items():
        lit, meshed = rim_pairs(text)
        bare = [go for go, _ in lit if go not in meshed]
        assert not bare, f"{path}: a rim-lit or bond Image without KitRimMesh (the shader would not know where it is): {bare[:3]}"
    for path, element in RIMMED.items():
        lit, _ = rim_pairs(prefabs.get(path, ""))
        assert element in {e for _, e in lit}, f"{path} lost its {element} rim"


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
    rim = open(os.path.join(REPO, RIM_SHADER), encoding="utf-8").read()
    rim_declared = set(re.findall(r"^\s+(?:\[[^\]]*\]\s*)*(_\w+) \(", rim, re.M)) - {"_MainTex"}
    for e in RIMS:
        m = files[f"{DIR}/ElementRim_{e}.mat"]
        assert f"m_Shader: {{fileID: 4800000, guid: {RIM_SHADER_GUID}, type: 3}}" in m, f"ElementRim_{e} does not use ElementRim.shader"
        written = set(re.findall(r"^    - (_\w+):", m, re.M)) - {"_MainTex"}
        assert written == rim_declared, f"ElementRim_{e}: properties drifted (missing {rim_declared - written}, extra {written - rim_declared})"
        if RIMS[e]:
            assert f"- _ElementColor: {col(RIMS[e])}" in m, f"ElementRim_{e} is not lit in {e}"
    assert "  - _Omni: 1\n" in files[f"{DIR}/ElementRim_Omni.mat"].replace("    - _Omni: 1", "  - _Omni: 1"), "the omni rim is not omni"
    # The presence orb and the bonds (§45.4).
    for shader_path, mat, g in ((PRESENCE_SHADER, "Presence_Orb", PRESENCE_SHADER_GUID), (BOND_SHADER, "Bond_Party", BOND_SHADER_GUID)):
        src = open(os.path.join(REPO, shader_path), encoding="utf-8").read()
        assert '#include "UIPresence.cginc"' in src, f"{shader_path} does not include UIPresence.cginc"
        declared_ = set(re.findall(r"^\s+(?:\[[^\]]*\]\s*)*(_\w+) \(", src, re.M)) - {"_MainTex"}
        m = files[f"{DIR}/{mat}.mat"]
        assert f"m_Shader: {{fileID: 4800000, guid: {g}, type: 3}}" in m, f"{mat} is not on {shader_path}"
        written = set(re.findall(r"^    - (_\w+):", m, re.M)) - {"_MainTex"}
        assert written == declared_, f"{mat}: properties drifted (missing {declared_ - written}, extra {written - declared_})"
        assert f"guid: {g}" in files[f"{shader_path}.meta"], f"{shader_path}.meta has the wrong guid"
    orb = files[f"{DIR}/Presence_Orb.mat"]
    assert f"- _SpaceColor: {col(ELEMENTS['_SpaceColor'])}" in orb, "the orb is not drawn in the theme's Space"
    assert f"- _BondAngles: {col(ORB_MEMBER_ANGLES, 0)}" in orb, \
        f"the orb's bonds do not point at the member seats (ORB_MEMBER_ANGLES {ORB_MEMBER_ANGLES})"
    radii = {k: re.search(rf"^    - {k}: (.*)$", orb, re.M).group(1) for k in ("_MemberRadius", "_InnerRadius")}
    assert radii["_MemberRadius"] == radii["_InnerRadius"] == f(ORB_MEMBER_RADIUS), \
        f"the member seats are off the inner ring ({radii}, want {ORB_MEMBER_RADIUS})"
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
    f_ = f
    cases = [
        ("a material on the wrong shader", lambda f: f.__setitem__(f"{DIR}/Lattice_Panel.mat", f[f"{DIR}/Lattice_Panel.mat"].replace(SHADER_GUID, "0" * 32))),
        ("a property the shader no longer declares", lambda f: f.__setitem__(f"{DIR}/Lattice_Window.mat", f[f"{DIR}/Lattice_Window.mat"].replace(
            "    - _FadeStart:", "    - _Retired: 1\n    - _FadeStart:", 1))),
        ("Charge off the theme colour", lambda f: f.__setitem__(f"{DIR}/Lattice_Window.mat", f[f"{DIR}/Lattice_Window.mat"].replace(
            col(ELEMENTS["_ChargeColor"]), col((0, 0.83, 1)), 1))),
        ("the Time rim lit in another colour", lambda f: f.__setitem__(f"{DIR}/ElementRim_Time.mat", f[f"{DIR}/ElementRim_Time.mat"].replace(
            f"- _ElementColor: {col(RIMS['Time'])}", f"- _ElementColor: {col(RIMS['Mass'])}", 1))),
        ("the shader meta on another guid", lambda f: f.__setitem__(f"{SHADER}.meta", f[f"{SHADER}.meta"].replace(SHADER_GUID, "1" * 32))),
        ("the orb's bonds aimed away from the seats", lambda f: f.__setitem__(f"{DIR}/Presence_Orb.mat", f[f"{DIR}/Presence_Orb.mat"].replace(
            f"- _BondAngles: {col(ORB_MEMBER_ANGLES, 0)}", f"- _BondAngles: {col((90, 45, 270), 0)}", 1))),
        ("the orb's seats off the inner ring", lambda f: f.__setitem__(f"{DIR}/Presence_Orb.mat", f[f"{DIR}/Presence_Orb.mat"].replace(
            f"    - _MemberRadius: {f_(ORB_MEMBER_RADIUS)}", "    - _MemberRadius: 0.8", 1))),
        ("the bond material on the rim shader", lambda f: f.__setitem__(f"{DIR}/Bond_Party.mat", f[f"{DIR}/Bond_Party.mat"].replace(
            BOND_SHADER_GUID, RIM_SHADER_GUID))),
    ]
    screens = shipped_prefabs()
    cases += [
        ("a screen still drawing the grid", lambda f: f.__setitem__("__screens__", dict(screens, **{
            WINDOWS[0]: screens[WINDOWS[0]] + f"  m_Sprite: {{fileID: 21300000, guid: {KIT_GRID}, type: 3}}\n"}))),
        ("a rim without KitRimMesh", lambda f: f.__setitem__("__screens__", dict(screens, **{
            f"{SCREENS}/Ark/ArkView.prefab": screens[f"{SCREENS}/Ark/ArkView.prefab"].replace(RIM_SCRIPT, "3" * 32)}))),
        ("the Hangar panel back to a plain border", lambda f: f.__setitem__("__screens__", dict(screens, **{
            f"{SCREENS}/Hangar/HangarView.prefab": screens[f"{SCREENS}/Hangar/HangarView.prefab"].replace(RIM_GUID["Mass"], "4" * 32)}))),
        ("a window without its lattice", lambda f: f.__setitem__("__screens__", dict(screens, **{
            WINDOWS[2]: screens[WINDOWS[2]].replace(WINDOW_GUID, "2" * 32)}))),
        ("a party bond without KitRimMesh", lambda f: f.__setitem__("__screens__", dict(screens, **{
            PARTY_PANEL: screens[PARTY_PANEL].replace(RIM_SCRIPT, "5" * 32)}))),
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
