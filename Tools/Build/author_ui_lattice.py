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
# Rev 7 (§47.1): the selection's light - the rim shader in white (the vertex colour, the theme's Selection blue,
# tints it), sweeping and breathing; the Bar runs along an underline.
SELECTION_GUID = guid("selection_rim_mat")
SELECTION_BAR_GUID = guid("selection_bar_mat")
SELECTION = (0.23921569, 0.69803923, 1)            # #3DB2FF, UITheme.Selection
THEME = "Assets/_SO_Assets/UI/UITheme.asset"
# The elemental rims (§45.3), RETIRED 2026-10-24 (Docs/UI_COLOR_RULES.md): elements carry no colour in the UI, and a
# light running round a panel nobody selected read as the selection. Their five materials are deleted; the guids stay
# so the screens can be checked for a stray reference. The shader stays - the selection's two materials run on it.
RIMS = {"Charge": (1, 0.827451, 0.3019608), "Mass": (0.49803922, 0.65882355, 1), "Space": (0.6901961, 0.4862745, 1),
        "Time": (0.37254903, 0.9411765, 0.78431374), "Omni": None}
RIM_GUID = {e: guid(f"rim_{e.lower()}_mat") for e in RIMS}
PANEL_GUID = guid("panel_mat")
WINDOW_GUID = guid("window_mat")

ELEMENTS = {"_ChargeColor": (1, 0.827451, 0.3019608), "_MassColor": (0.49803922, 0.65882355, 1),
            "_SpaceColor": (0.6901961, 0.4862745, 1), "_TimeColor": (0.37254903, 0.9411765, 0.78431374)}
LINE = (0.34509805, 0.4, 0.6901961)               # the hexagons' outline, a light SurfaceLight (#5866B0)
GLOW = (0.49803922, 0.84705883, 1)                # the ring's and the tide's light (#7FD8FF)
FROST_TINT = (0, 0.003921569, 0.039215688)        # SurfaceBlack (#00010A): the frost leans toward it

# Rev 11 option H (Docs/MENU_REDESIGN_B.md section 62, the owner 2026-10-24): frosted glass with Amoebius's hex
# lattice. The window material is frosted (it draws the blurred world behind the menu while UIBackdropBlur has it live,
# and nothing under the hexagons otherwise); a panel inside a window is not (no double frost). Neither material moves
# by itself (_Motion 0): KitBackdropMotion on a window's backdrop picks the ring, the tide or the grow each time the
# window opens (the owner: "randomize H2, 4, 6 everywhere").
MOTION = dict(_Motion=0, _RingAlpha=0.85, _RingWavelength=862, _RingPeriod=5.2, _TideAlpha=0.18, _TidePeriod=14,
              _TideWidth=0.16, _GrowSeconds=1.2)
HEX = dict(_LineWidth=1, _HexRadius=34, _HexInset=0.92, _FadeStart=0.6, _Reveal=1)
MATERIALS = {
    "Lattice_Panel": dict(HEX, **MOTION, _Frost=0, _FrostAmount=0.5, _LineAlpha=0.3),
    "Lattice_Window": dict(HEX, **MOTION, _Frost=1, _FrostAmount=0.5, _LineAlpha=0.4),
}
FROSTED = {"Lattice_Panel": 0, "Lattice_Window": 1}
# The frosted glass's blur (UIBackdropBlur): its settings asset, its shader, and the motion component the window
# backdrops carry (menu_b_motion.window_lattice_node adds it).
BLUR_SHADER = f"{DIR}/BackdropBlur.shader"
BLUR_SHADER_GUID = guid("backdrop_blur_shader")
BLUR_SETTINGS = "Assets/Resources/UIBackdropBlurSettings.asset"
BLUR_SETTINGS_GUID = guid("backdrop_blur_settings_asset")
BLUR_SETTINGS_SCRIPT = guid("backdrop_blur_settings_cs")
MOTION_SCRIPT_PATH = "Assets/_Scripts/UI/Kit/KitBackdropMotion.cs"
BLUR = dict(enabled=1, downsample=4, passes=2, minQualityLevel=1)
MOTION_CODES = {"Still": 0, "Ring": 1, "Tide": 2, "Grow": 3}      # the shader's _Motion codes
MAT_GUID = {"Lattice_Panel": PANEL_GUID, "Lattice_Window": WINDOW_GUID}
# The presence orb (§45.4). Its member seats sit on the inner ring at these angles (degrees, counter-clockwise from
# the right): the shader's bonds (_BondAngles, _MemberRadius) and the drawer generator's avatar positions
# (author_menu_friends_drawer.py) both read them, so a bond always ends under its avatar.
ORB_MEMBER_ANGLES = (135, 45, 270)
ORB_MEMBER_RADIUS = 0.66
# Still since 2026-10-24 (Garrett: the charged particles and orbiting elements round the connected circles were a
# polish problem): the orbit does not turn and carries no dots (the count is the "N online" line), and every bond is
# a solid, still line (period 0). The orb's own ring is the neutral blue, not an element's colour.
PRESENCE = dict(_GlowAlpha=0.24, _InnerRadius=ORB_MEMBER_RADIUS, _InnerAlpha=0.34, _OuterRadius=0.92, _OuterAlpha=0.3,
                _RingWidth=0.012, _Soft=0.03, _Dashes=28, _SpinSeconds=0, _Online=0, _DotSize=0,
                _MemberRadius=ORB_MEMBER_RADIUS, _BondWidth=0.022, _BondPeriod=0, _BondSpeed=0)
BOND = dict(_Width=1.4, _Soft=1, _Period=0, _Speed=0)
NEUTRAL = (0.45490196, 0.48235294, 0.6784314)       # NeutralLightest #747BAD
KIT_GRID = "21a4d02295ff993cb1746d4d792daa21"       # the retired blueprint grid sprite (kit_grid.png)
SCREENS = "Assets/_Prefabs/UI Elements/Main Menu Screens"
WINDOWS = [f"{SCREENS}/{p}" for p in ("Arcade/ArcadeView.prefab", "Arena/ArenaView.prefab", "Mission/MissionView.prefab",
                                       "Toybox/ToyboxView.prefab", "Configure/ConfigureView.prefab",
                                       "Hangar/HangarView.prefab", "Settings/SettingsView.prefab")]
UI_FLOATS = dict(_ColorMask=15, _Stencil=0, _StencilComp=8, _StencilOp=0, _StencilReadMask=255,
                 _StencilWriteMask=255, _UseUIAlphaClip=0)


def f(x):
    x = float(x)
    return str(int(x)) if x == int(x) else repr(round(x, 7)).rstrip("0").rstrip(".")


def col(rgb, a=1):
    return "{r: %s, g: %s, b: %s, a: %s}" % (f(rgb[0]), f(rgb[1]), f(rgb[2]), f(a))


def material(name):
    floats = dict(MATERIALS[name], **UI_FLOATS)
    colors = dict(_Color=(1, 1, 1), _LineColor=LINE, _GlowColor=GLOW, _FrostTint=FROST_TINT)
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


def blur_settings():
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {BLUR_SETTINGS_SCRIPT}, type: 3}}\n"
            "  m_Name: UIBackdropBlurSettings\n  m_EditorClassIdentifier: \n"
            + "".join(f"  {k}: {v}\n" for k, v in BLUR.items())
            + f"  blurShader: {{fileID: 4800000, guid: {BLUR_SHADER_GUID}, type: 3}}\n")


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
                  _BaseLevel=0.7 if omni else 0.45, _Linear=0, _Breathe=0, _BreatheSeconds=1.8)
    colors = dict(_Color=(1, 1, 1), _ElementColor=RIMS[element] or (1, 1, 1),
                  **{k: v for k, v in ELEMENTS.items()})
    return shader_material(f"ElementRim_{element}", RIM_SHADER_GUID, floats, colors)


def selection_material(bar):
    floats = dict(_Omni=0, _Speed=0.6 if bar else 0.35, _Tail=0.45 if bar else 0.35, _BaseLevel=0.72,
                  _Linear=1 if bar else 0, _Breathe=0.35, _BreatheSeconds=1.8)
    colors = dict(_Color=(1, 1, 1), _ElementColor=(1, 1, 1), **ELEMENTS)
    return shader_material("SelectionBar" if bar else "SelectionRim", RIM_SHADER_GUID, floats, colors)


def theme_asset():
    """UITheme.asset with the selection colour and its two materials (the rest of the asset is untouched)."""
    text = open(os.path.join(REPO, THEME), encoding="utf-8").read()
    fields = dict(selection=col(SELECTION), selectionMaterial=f"{{fileID: 2100000, guid: {SELECTION_GUID}, type: 2}}",
                  selectionBarMaterial=f"{{fileID: 2100000, guid: {SELECTION_BAR_GUID}, type: 2}}")
    missing = ""
    for name, value in fields.items():               # field order: selection, selectionMaterial, selectionBarMaterial
        line = f"  {name}: {value}\n"
        if re.search(rf"^  {name}: .*$", text, re.M):
            text = re.sub(rf"^  {name}: .*\n", line, text, count=1, flags=re.M)
        else:
            missing += line
    if missing:
        anchor = re.search(r"^  elementTime: .*\n", text, re.M)
        assert anchor, "UITheme.asset has no elementTime to follow"
        text = text[:anchor.end()] + missing + text[anchor.end():]
    return text


def presence_material():
    clear = (0, 0, 0, 0)
    return shader_material("Presence_Orb", PRESENCE_SHADER_GUID, PRESENCE,
                           dict(_Color=(1, 1, 1), _SpaceColor=NEUTRAL, _Bond0=clear, _Bond1=clear,
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
    out[f"{PRESENCE_SHADER}.meta"] = out[f"{SHADER}.meta"].replace(SHADER_GUID, PRESENCE_SHADER_GUID)
    out[f"{BOND_SHADER}.meta"] = out[f"{SHADER}.meta"].replace(SHADER_GUID, BOND_SHADER_GUID)
    out[f"{PRESENCE_CGINC}.meta"] = out[f"{CGINC}.meta"].replace(CGINC_GUID, PRESENCE_CGINC_GUID)
    out[f"{DIR}/Presence_Orb.mat"] = presence_material()
    out[f"{DIR}/Presence_Orb.mat.meta"] = mat_meta(PRESENCE_GUID)
    out[f"{DIR}/Bond_Party.mat"] = bond_material()
    out[f"{DIR}/Bond_Party.mat.meta"] = mat_meta(BOND_GUID)
    out[f"{DIR}/SelectionRim.mat"] = selection_material(False)
    out[f"{DIR}/SelectionRim.mat.meta"] = mat_meta(SELECTION_GUID)
    out[f"{DIR}/SelectionBar.mat"] = selection_material(True)
    out[f"{DIR}/SelectionBar.mat.meta"] = mat_meta(SELECTION_BAR_GUID)
    out[THEME] = theme_asset()
    out[BLUR_SETTINGS] = blur_settings()
    out[BLUR_SETTINGS + ".meta"] = mat_meta(BLUR_SETTINGS_GUID).replace("mainObjectFileID: 2100000", "mainObjectFileID: 11400000")
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
RETIRED = tuple(f"{DIR}/ElementRim_{e}.mat{x}" for e in RIMS for x in ("", ".meta"))


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
    # Every window backdrop picks its motion and keeps the frost live: KitBackdropMotion sits beside each Image drawn
    # with Lattice_Window.mat (rev 11 option H, "randomize H2, 4, 6 everywhere").
    motion = guid("backdrop_motion_cs")
    for path, text in prefabs.items():
        gos_with_motion = set(re.findall(r"m_GameObject: \{fileID: (-?\d+)\}\n(?:.*\n){0,6}?  m_Script: \{fileID: 11500000, guid: %s" % motion, text))
        for m in re.finditer(r"^--- !u!114 &-?\d+\nMonoBehaviour:\n((?:(?!--- ).*\n)*)", text, re.M):
            body = m.group(1)
            if f"m_Material: {{fileID: 2100000, guid: {WINDOW_GUID}, type: 2}}" not in body:
                continue
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1)
            assert go in gos_with_motion, f"{path}: a window backdrop without KitBackdropMotion (no random motion, no frost)"
    # Every bond Image has KitRimMesh beside it, and no screen is lit with a retired element rim.
    for path, text in prefabs.items():
        lit, meshed = rim_pairs(text)
        bare = [go for go, _ in lit if go not in meshed]
        assert not bare, f"{path}: a bond Image without KitRimMesh (the shader would not know where it is): {bare[:3]}"
        rimmed = sorted({e for _, e in lit if e in RIMS})
        assert not rimmed, f"{path} is lit with the retired element rim(s) {rimmed} (Docs/UI_COLOR_RULES.md)"


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
    # Rev 11 option H: Amoebius's cell (radius 34, inset 0.92); the window frosted, the panel not; neither moves by
    # itself - the motion is per window, picked by KitBackdropMotion.
    for name in MATERIALS:
        m = files[f"{DIR}/{name}.mat"]
        assert "    - _HexRadius: 34\n" in m and "    - _HexInset: 0.92\n" in m, f"{name}: not Amoebius's hexagon (radius 34, inset 0.92)"
        assert f"    - _Frost: {FROSTED[name]}\n" in m, f"{name}: _Frost must be {FROSTED[name]} (only the window is frosted)"
        assert "    - _Motion: 0\n" in m, f"{name}: the material moves by itself (the motion belongs to KitBackdropMotion)"
        assert f"- _GlowColor: {col(GLOW)}" in m, f"{name}: the ring and tide light is not the light blue"

    # The blur: its settings name the shader (so the build includes it) and the shader's passes sit in the order
    # UIBackdropBlur.BlurPass uses them; the motion component's codes are the shader's.
    st = files[BLUR_SETTINGS]
    assert f"guid: {BLUR_SETTINGS_SCRIPT}" in st, "UIBackdropBlurSettings.asset is not a UIBackdropBlurSettingsSO"
    assert f"  blurShader: {{fileID: 4800000, guid: {BLUR_SHADER_GUID}, type: 3}}\n" in st, \
        "UIBackdropBlurSettings does not name BackdropBlur.shader (the build would leave it out: no frost)"
    for k, v in BLUR.items():
        assert f"  {k}: {v}\n" in st, f"UIBackdropBlurSettings: {k} is not {v}"
    bs = open(os.path.join(REPO, BLUR_SHADER), encoding="utf-8").read()
    assert f"guid: {BLUR_SHADER_GUID}" in open(os.path.join(REPO, BLUR_SHADER + ".meta"), encoding="utf-8").read(), \
        "BackdropBlur.shader.meta has the wrong guid"
    assert re.findall(r'Name "(\w+)"', bs) == ["Downsample", "Blur"], "BackdropBlur.shader's passes moved (UIBackdropBlur names them 0, 1)"
    cs = open(os.path.join(REPO, MOTION_SCRIPT_PATH), encoding="utf-8").read()
    codes = dict((n, int(v)) for n, v in re.findall(r"^\s+(\w+) = (\d+),", cs.split("enum BackdropMotion", 1)[1].split("}", 1)[0], re.M))
    assert codes == MOTION_CODES, f"BackdropMotion {codes} no longer matches the shader's _Motion codes {MOTION_CODES}"
    assert "_Motion (\"Motion without per-Image data (0 still, 1 ring, 2 tide, 3 grow)\"" in shader, \
        "Lattice.shader's _Motion codes changed: update BackdropMotion and MOTION_CODES together"

    rim = open(os.path.join(REPO, RIM_SHADER), encoding="utf-8").read()
    rim_declared = set(re.findall(r"^\s+(?:\[[^\]]*\]\s*)*(_\w+) \(", rim, re.M)) - {"_MainTex"}
    back = [p for p in RETIRED if p in files]
    assert not back, f"a retired element rim material is written again: {back}"
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
    assert f"- _SpaceColor: {col(NEUTRAL)}" in orb, "the orb's ring must be the neutral blue, not an element's colour"
    for k in ("_SpinSeconds", "_DotSize", "_BondPeriod", "_BondSpeed"):
        assert f"    - {k}: 0\n" in orb, f"Presence_Orb: {k} must be 0 - the orb has no orbiting dots or flowing bonds"
    for k in ("_Period", "_Speed"):
        assert f"    - {k}: 0\n" in files[f"{DIR}/Bond_Party.mat"], f"Bond_Party: {k} must be 0 - the party bonds are solid and still"
    assert f"- _BondAngles: {col(ORB_MEMBER_ANGLES, 0)}" in orb, \
        f"the orb's bonds do not point at the member seats (ORB_MEMBER_ANGLES {ORB_MEMBER_ANGLES})"
    radii = {k: re.search(rf"^    - {k}: (.*)$", orb, re.M).group(1) for k in ("_MemberRadius", "_InnerRadius")}
    assert radii["_MemberRadius"] == radii["_InnerRadius"] == f(ORB_MEMBER_RADIUS), \
        f"the member seats are off the inner ring ({radii}, want {ORB_MEMBER_RADIUS})"
    # Rev 7: the selection's two materials are rims, white (the theme's blue is the vertex colour), breathing; the
    # Bar runs along its line; the theme names both and carries the sky blue.
    for name, bar, g in (("SelectionRim", False, SELECTION_GUID), ("SelectionBar", True, SELECTION_BAR_GUID)):
        m = files[f"{DIR}/{name}.mat"]
        assert f"m_Shader: {{fileID: 4800000, guid: {RIM_SHADER_GUID}, type: 3}}" in m, f"{name} is not on ElementRim.shader"
        written = set(re.findall(r"^    - (_\w+):", m, re.M)) - {"_MainTex"}
        assert written == rim_declared, f"{name}: properties drifted (missing {rim_declared - written}, extra {written - rim_declared})"
        assert f"    - _Linear: {1 if bar else 0}\n" in m, f"{name} runs {'round its rect' if bar else 'along a line'}"
        assert "    - _Breathe: 0\n" not in m, f"{name} does not breathe"
        assert f"- _ElementColor: {col((1, 1, 1))}" in m, f"{name} is tinted: the theme's Selection colour must reach it unchanged"
        assert f"guid: {g}" in files[f"{DIR}/{name}.mat.meta"], f"{name}.mat.meta has the wrong guid"
    theme = files[THEME]
    assert f"  selection: {col(SELECTION)}\n" in theme, "UITheme.asset's selection is not the sky blue (#3DB2FF)"
    for field, g in (("selectionMaterial", SELECTION_GUID), ("selectionBarMaterial", SELECTION_BAR_GUID)):
        assert f"  {field}: {{fileID: 2100000, guid: {g}, type: 2}}" in theme, f"UITheme.asset does not name its {field}"
    validate_screens(files.get("__screens__") or shipped_prefabs())


def drift(files):
    bad = []
    for p, t in files.items():
        if p.startswith("__"):
            continue
        full = os.path.join(REPO, p)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != t:
            bad.append(p)
    return bad + [p for p in RETIRED if os.path.exists(os.path.join(REPO, p))]


def self_test(files):
    f_ = f
    cases = [
        ("a material on the wrong shader", lambda f: f.__setitem__(f"{DIR}/Lattice_Panel.mat", f[f"{DIR}/Lattice_Panel.mat"].replace(SHADER_GUID, "0" * 32))),
        ("a property the shader no longer declares", lambda f: f.__setitem__(f"{DIR}/Lattice_Window.mat", f[f"{DIR}/Lattice_Window.mat"].replace(
            "    - _FadeStart:", "    - _Retired: 1\n    - _FadeStart:", 1))),
        ("the blur settings without the shader", lambda f: f.__setitem__(BLUR_SETTINGS, f[BLUR_SETTINGS].replace(BLUR_SHADER_GUID, "0" * 32))),
        ("the blur switched off", lambda f: f.__setitem__(BLUR_SETTINGS, f[BLUR_SETTINGS].replace("  enabled: 1\n", "  enabled: 0\n"))),
        ("the panel frosted too", lambda f: f.__setitem__(f"{DIR}/Lattice_Panel.mat", f[f"{DIR}/Lattice_Panel.mat"].replace(
            "    - _Frost: 0\n", "    - _Frost: 1\n", 1))),
        ("the window moving by itself", lambda f: f.__setitem__(f"{DIR}/Lattice_Window.mat", f[f"{DIR}/Lattice_Window.mat"].replace(
            "    - _Motion: 0\n", "    - _Motion: 1\n", 1))),
        ("the orb's dots orbiting again", lambda f: f.__setitem__(f"{DIR}/Presence_Orb.mat", f[f"{DIR}/Presence_Orb.mat"].replace(
            "    - _SpinSeconds: 0\n", "    - _SpinSeconds: 24\n", 1))),
        ("the Time rim material brought back", lambda f: f.__setitem__(f"{DIR}/ElementRim_Time.mat", rim_material("Time"))),
        ("the shader meta on another guid", lambda f: f.__setitem__(f"{SHADER}.meta", f[f"{SHADER}.meta"].replace(SHADER_GUID, "1" * 32))),
        ("the orb's bonds aimed away from the seats", lambda f: f.__setitem__(f"{DIR}/Presence_Orb.mat", f[f"{DIR}/Presence_Orb.mat"].replace(
            f"- _BondAngles: {col(ORB_MEMBER_ANGLES, 0)}", f"- _BondAngles: {col((90, 45, 270), 0)}", 1))),
        ("the orb's seats off the inner ring", lambda f: f.__setitem__(f"{DIR}/Presence_Orb.mat", f[f"{DIR}/Presence_Orb.mat"].replace(
            f"    - _MemberRadius: {f_(ORB_MEMBER_RADIUS)}", "    - _MemberRadius: 0.8", 1))),
        ("the bond material on the rim shader", lambda f: f.__setitem__(f"{DIR}/Bond_Party.mat", f[f"{DIR}/Bond_Party.mat"].replace(
            BOND_SHADER_GUID, RIM_SHADER_GUID))),
        ("the selection stops breathing", lambda f: f.__setitem__(f"{DIR}/SelectionRim.mat", re.sub(
            r"    - _Breathe: [\d.]+\n", "    - _Breathe: 0\n", f[f"{DIR}/SelectionRim.mat"], count=1))),
        ("the theme loses the selection's material", lambda f: f.__setitem__(THEME, f[THEME].replace(SELECTION_GUID, "8" * 32, 1))),
    ]
    screens = shipped_prefabs()
    cases += [
        ("a screen still drawing the grid", lambda f: f.__setitem__("__screens__", dict(screens, **{
            WINDOWS[0]: screens[WINDOWS[0]] + f"  m_Sprite: {{fileID: 21300000, guid: {KIT_GRID}, type: 3}}\n"}))),
        ("a screen lit with an element rim again", lambda f: f.__setitem__("__screens__", dict(screens, **{
            PARTY_PANEL: screens[PARTY_PANEL].replace(f"guid: {BOND_GUID}", f"guid: {RIM_GUID['Space']}", 1)}))),
        ("a window backdrop without its motion", lambda f: f.__setitem__("__screens__", dict(screens, **{
            WINDOWS[0]: screens[WINDOWS[0]].replace(guid("backdrop_motion_cs"), "6" * 32)}))),
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
    for p in RETIRED:
        if os.path.exists(os.path.join(REPO, p)):
            os.remove(os.path.join(REPO, p))
    print(f"wrote {len(files)} files")


if __name__ == "__main__":
    main()
