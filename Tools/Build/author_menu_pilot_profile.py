#!/usr/bin/env python3
"""
Author a pilot's PROFILE PAGE (Garrett, 2026-11-11: "the main use will be a full screen profile for each pilot
accessible through the ark app shell menu"; 2026-11-12: "use the full size art for each pilot's profile page in the
ark"; Docs/MENU_REDESIGN_B.md section 92.1, Docs/Pilots/PORTRAITS.md section 8).

    Assets/Resources/Pilots/PilotProfileView.prefab
        The whole page: CanvasGroup + Animator (Standard.controller, which scales the child named Content) +
        PilotProfileModal (ModalType PILOT_PROFILE = 21) on the root; the ground; the SCENE (a RawImage with no
        texture, switched off: PilotSceneImage loads the pilot's scene by name, cuts it to the screen's shape and
        carries it to the screen's edges); and, under Content, the step through the crew (Next first, so the pad
        lands on it), the words panel and Close (B).

    No scene is edited: ScreenSwitcher.EnsurePilotProfileModal adds the page to the canvas from Resources, as
    EnsureQuestsModal adds the Quests window.

THE LAYOUT. The picture is the page; the words sit in one panel on the side the pilot does not stand on (the page
moves it: authored against the left edge) and leave the friends orb's corner clear. Next, Previous and the count sit
in one plate at the bottom centre, the same place for every pilot, so a mouse does not chase them from side to
side. Both plates are the window ground at PLATE_ALPHA, so words read on any picture. EVERY WORD is a template on
PilotProfileModal (WORDS below) or the text of a button.

STANDING DOWN: every output is re-derived on each run, so a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_pilot_profile.py              # write
    python3 Tools/Build/author_menu_pilot_profile.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_pilot_profile.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile_window",
                                               os.path.join(HERE, "author_menu_profile_window.py"))
W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(W)

P, C, A, K, T, H, M = W.P, W.C, W.A, W.K, W.T, W.H, W.M
REPO = K.REPO
OUT_DIR = "Assets/Resources/Pilots"
NAME = "PilotProfileView"
RESOURCE = "Pilots/PilotProfileView"                      # ScreenSwitcher.PilotProfileWindowResource
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, ref, mono = C.TL, C.ref, C.mono
label, le = H.label, H.le
image_node = A.image_node

STANDARD_CONTROLLER = "362e7fec2b6aecb4a92d91defb53b98d"   # Standard.controller (scales "Content")
SCENE_CATALOG = K.meta_guid("Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset")   # author_pilot_scene_assets.py
S = dict(page=C.script("PilotProfileModal"), scene=C.script("PilotSceneImage"),
         close=C.script("ModalWindowCloseButton"), button=K.kit_script("KitButton"))
PACKAGE = P.PACKAGE | {C.RAW_IMAGE}
PAGE_TYPE = 21                                            # ScreenSwitcher.ModalWindows.PILOT_PROFILE

# The words panel, against the left edge (PilotProfileModal.PlacePanel mirrors it). Its bottom clears the friends
# orb, whose top is 124 above the canvas's bottom left (author_menu_arcade.ORB_CLEARANCE is 100 for a 52-tall bar).
PANEL_W, PANEL_H, PANEL_INSET, PANEL_BOTTOM, PAD = 560, 744, 48, 140, 24
INNER = PANEL_W - 2 * PAD
PLATE_ALPHA = 0.88                                        # the Credits window's dim: words read on any picture
ORB_TOP = 124

# The step through the crew: one plate at the bottom centre.
STEP_W, STEP_H, STEP_BOTTOM, STEP_PAD = 392, 72, 32, 12
BUTTON_W, BUTTON_H, COUNT_W = 128, 48, 96

# Rows of the panel, top-left corner down from the panel's top: (key, name, y, height, role, token, wrap).
ROWS = (
    ("Kicker", "Kicker", 24, 20, "Label", "NeutralLightest", False),
    ("Name", "Name", 48, 48, "H1", "TextLight", False),
    ("Species", "Species", 104, 20, "BodySmall", "NeutralLightest", False),
    ("Faction", "Faction", 128, 20, "BodySmall", "NeutralLightest", False),
    ("Home", "Home", 152, 20, "BodySmall", "NeutralLightest", False),
    ("SightCaption", "SightCaption", 205, 18, "Label", "NeutralLightest", False),
    ("Sight", "Sight", 231, 48, "Body", "TextLight", True),
    ("StoryCaption", "StoryCaption", 295, 18, "Label", "NeutralLightest", False),
    ("Story", "Story", 321, 363, "Body", "TextLight", True),
    ("Pictured", "Pictured", 700, 20, "Label", "NeutralLightest", False),
)
RULE_Y = 188
# What the wrapped rows hold, at Body in INNER (measured 2026-11-12 with the game's own PilotGenerator and the UI
# font): the longest story of 360,000 rolls is 637 characters and 13 lines, and the longest its tables can compose is
# 679 characters and 14 lines. A sight is a callsign's authored meaning (CallsignSet.asset): two lines today.
STORY_LINES, SIGHT_LINES = 14, 2
WIDEST_NAME = "Darthawen Lanternweaver"                   # the widest name those tables compose, in the UI font
CALLSIGN_SET = "Assets/Resources/Pilots/CallsignSet.asset"
# Words a player must read sit on the plate in these: TextInactive is the DISABLED colour, near the plate's own.
READABLE = ("TextLight", "NeutralLightest")

# Every word of the page that is not a button's: PilotProfileModal's templates. A line shows nothing while its own
# fact is empty, so "From {homeworld}" never reads "From".
WORDS = (
    ("kickerTemplate", "{callsign}"),
    ("nameTemplate", "{name}"),
    ("speciesTemplate", "{species}"),
    ("factionTemplate", "{faction}"),
    ("homeTemplate", "From {homeworld}"),
    ("sightCaptionTemplate", "Sight"),
    ("sightTemplate", "{sight}"),
    ("storyCaptionTemplate", "Story"),
    ("storyTemplate", "{backstory}"),
    ("picturedTemplate", "Pictured: {painted}, the first {callsign}"),
    ("countTemplate", "{index} / {count}"),
)
TOKENS = ("name", "given", "family", "callsign", "sight", "species", "blend", "faction", "homeworld", "backstory",
          "painted", "index", "count")                  # PilotProfileModal.TokenNames
TEXT_FIELDS = dict(kickerText="Kicker", nameText="Name", speciesText="Species", factionText="Faction",
                   homeText="Home", sightCaptionText="SightCaption", sightText="Sight",
                   storyCaptionText="StoryCaption", storyText="Story", picturedText="Pictured")
SAFE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 .,()!?/-]*[A-Za-z0-9.!?)]$|^[A-Za-z0-9]$")


def ystr(v):
    """A YAML scalar as Unity writes one: bare when nothing in it needs quoting, else single-quoted."""
    if v == "":
        return ""
    return v if SAFE.match(v) else "'" + v.replace("'", "''") + "'"


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path():
    return f"{OUT_DIR}/{NAME}.prefab"


def F(key):
    return K.FileIds(NAME)(key)


# --------------------------------------------------------------------------------------------
# The prefab
# --------------------------------------------------------------------------------------------

def animator():
    return lambda ctx, go: (
        "Animator:\n  serializedVersion: 7\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Avatar: {{fileID: 0}}\n"
        f"  m_Controller: {{fileID: 9100000, guid: {STANDARD_CONTROLLER}, type: 2}}\n"
        "  m_CullingMode: 0\n  m_UpdateMode: 2\n  m_ApplyRootMotion: 0\n  m_LinearVelocityBlending: 0\n"
        "  m_StabilizeFeet: 0\n  m_AnimatePhysics: 0\n  m_WarningMessage: \n  m_HasTransformHierarchy: 1\n"
        "  m_AllowConstantClipSamplingOptimization: 1\n  m_KeepAnimatorStateOnDisable: 0\n"
        "  m_WriteDefaultValuesOnDisable: 0\n")


def hidden_group():
    return lambda ctx, go: ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: 0\n"
                            "  m_Interactable: 0\n  m_BlocksRaycasts: 0\n  m_IgnoreParentGroups: 0\n")


def plate(parent, key, sliver):
    """The ground under words that sit on a picture: the window's black at PLATE_ALPHA, a light hairline."""
    C.frame(parent, key, sliver, "SurfaceBlack", "TextLight", fill_alpha=PLATE_ALPHA)


def stepper(content):
    """NEXT, PREVIOUS and "3 / 18" on one plate at the bottom centre. Next is first in the hierarchy: the pad's
    selection lands on the first control that takes it (ScreenSwitcher.PreferredSelectable)."""
    st = content.child(N("Stepper", "Stepper", R((0.5, 0), None, (0, STEP_BOTTOM), (STEP_W, STEP_H), (0.5, 0))))
    plate(st, "Stepper/Frame", 10)
    H.text_button(st, "Stepper/Next", "Next", "Next",
                  R((1, 0.5), None, (-STEP_PAD, 0), (BUTTON_W, BUTTON_H), (1, 0.5)), None, None, None, None)
    H.text_button(st, "Stepper/Previous", "Previous", "Previous",
                  R((0, 0.5), None, (STEP_PAD, 0), (BUTTON_W, BUTTON_H), (0, 0.5)), None, None, None, None)
    label(st.child(N("Stepper/Count", "Count", R((0.5, 0.5), None, (0, 0), (COUNT_W, 20)))), "", "Label",
          "NeutralLightest", align="center")
    return st


def words_panel(content):
    """The pilot's words. Each row is filled from its template on open; none holds a word of its own."""
    pn = content.child(N("Panel", "Panel", R((0, 0), None, (PANEL_INSET, PANEL_BOTTOM), (PANEL_W, PANEL_H), (0, 0))))
    plate(pn, "Panel/Frame", 14)
    M.bracket_nodes(pn, "Panel/Bracket", inset=14)
    for key, name, y, h, role, token, wrap in ROWS:
        n = pn.child(N(f"Panel/{key}", name, TL(PAD, y, INNER, h)))
        H.ellipsize(label(n, "", role, token, valign="top" if wrap else "middle", wrap=wrap))
    image_node(pn, "Panel/Rule", "Rule", TL(PAD, RULE_Y, INNER, 1), "SurfaceLight")
    return pn


def close(content):
    """The kit's Close (B) at the top right, on a plate of its own that follows the button's size."""
    W.close_button(content)
    button = content.children[-1]
    under = N("Window/Close/Plate", "Plate", R((0, 0), (1, 1), (0, 0), (16, 16)))
    under.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack", PLATE_ALPHA), K.kit_sprite("kit_sliver10_fill"), True)) \
        .add("th", 114, H.themed("SurfaceBlack", PLATE_ALPHA)).add("le", 114, le(ignore=True))
    button.children.insert(0, under)
    return button


def build_modal():
    root = N("root", NAME, STRETCH)
    # No upright pose yet: the PC layout fitted to the width (author_menu_portrait.py, section 138).
    root.add("portrait", 114, K.portrait_rect(pc_block=True))
    root.add("cg", 225, hidden_group()).add("anim", 95, animator())
    # The ground: what shows while a scene loads and for a callsign with none. An Image as wide as the safe area,
    # so ScreenSwitcher bleeds it to the screen's edges on a notched phone; the scene reaches them itself.
    image_node(root, "Dim", "Dim", STRETCH, "SurfaceBlack", raycast=True)
    sc = root.child(N("Scene", "Scene", STRETCH))
    sc.add("cr", 222, K.canvas_renderer()).add("raw", 114, C.raw_image(enabled=False))
    sc.add("scene", 114, lambda ctx, go: mono(go, S["scene"],
           f"  reachScreenEdges: 1\n  keepClearOf: {ref(ctx.rt('Panel'))}\n  faceMargin: 0.03\n"))

    content = root.child(N("Content", "Content", STRETCH))
    stepper(content)
    words_panel(content)
    close(content)

    root.add("page", 114, lambda ctx, go: mono(go, S["page"], page_body(ctx)))
    return root


def page_body(ctx):
    texts = "".join(f"  {field}: {ref(ctx.c(f'Panel/{key}', 'tmp'))}\n" for field, key in TEXT_FIELDS.items())
    return ("  sharpAnimations: 1\n  closeOnGamepadB: 1\n  blockUIBehind: 1\n"
            f"  ModalType: {PAGE_TYPE}\n  windowAnimator: {ref(ctx.c('root', 'anim'))}\n"
            "  screenSwitcher: {fileID: 0}\n"
            f"  scenes: {K.ref(11400000, SCENE_CATALOG, 2)}\n  sceneImage: {ref(ctx.c('Scene', 'scene'))}\n"
            f"  panel: {ref(ctx.rt('Panel'))}\n" + texts
            + f"  countText: {ref(ctx.c('Stepper/Count', 'tmp'))}\n"
            f"  nextButton: {ref(ctx.c('Stepper/Next', 'button'))}\n"
            f"  previousButton: {ref(ctx.c('Stepper/Previous', 'button'))}\n"
            + "".join(f"  {field}: {ystr(words)}\n" for field, words in WORDS))


def prefab_outputs():
    return {prefab_path(): P.prefab_text(build_modal(), NAME),
            prefab_path() + ".meta": K.PREFAB_META.format(g=asset_guid(NAME))}


def outputs():
    return prefab_outputs()


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def children_of(text, rt):
    _, doc = A.doc_of(text, rt)
    rows = re.search(r"^  m_Children:\n((?:  - .*\n)*)", doc, re.M).group(1)
    return re.findall(r"^  - \{fileID: (-?\d+)\}", rows, re.M)


def rect_field(text, rt, field):
    _, doc = A.doc_of(text, rt)
    m = re.search(r"^  " + field + r": \{x: (-?[\d.e-]+), y: (-?[\d.e-]+)\}", doc, re.M)
    return float(m.group(1)), float(m.group(2))


def wrapped(words, role, width):
    """`words` as the lines a box `width` wide breaks it into (greedy, by the font's own advances)."""
    lines, line = [], ""
    for word in words.split(" "):
        longer = word if not line else line + " " + word
        if line and K.label_width(longer, role) > width:
            lines.append(line)
            line = word
        else:
            line = longer
    return lines + [line] if line else lines


def callsign_meanings():
    """What each callsign lights, as authored (the page's Sight row shows one)."""
    with open(os.path.join(REPO, CALLSIGN_SET), encoding="utf-8") as fh:
        found = re.findall(r"^ +meaning: (.*)$", fh.read(), re.M)
    assert found, f"{CALLSIGN_SET} holds no meaning"
    return [m[1:-1].replace("''", "'") if m.startswith("'") else m for m in found]


def validate(files):
    guids = T.known_guids() | PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    pf = files[prefab_path()]
    A.check_file(prefab_path(), pf, guids, minted)

    _, page = A.doc_of(pf, F("root#page"))
    assert page and S["page"] in page, "PilotProfileView.prefab: the root carries no PilotProfileModal"
    assert f"  ModalType: {PAGE_TYPE}\n" in page, "the page must register as ModalWindows.PILOT_PROFILE (21)"
    assert f"guid: {SCENE_CATALOG}" in page, "the page does not read the pilot scene catalog"
    for field in list(TEXT_FIELDS) + ["countText"]:
        C.wired(pf, page, field, K.TMP_UGUI, "PilotProfileModal")
    for field in ("nextButton", "previousButton"):
        C.wired(pf, page, field, S["button"], "PilotProfileModal")
    C.wired(pf, page, "sceneImage", S["scene"], "PilotProfileModal")
    C.wired(pf, page, "panel", 224, "PilotProfileModal")
    C.wired(pf, page, "windowAnimator", 95, "PilotProfileModal")

    # Every word is authored, quoted where YAML needs it, and names only tokens the page fills.
    for field, words in WORDS:
        assert f"  {field}: {ystr(words)}\n" in page, f"PilotProfileModal.{field} is not the authored line"
        for token in re.findall(r"\{(\w+)\}", words):
            assert token in TOKENS, f"PilotProfileModal.{field} names {{{token}}}, which the page does not fill"
    for key in TEXT_FIELDS.values():
        _, tmp = A.doc_of(pf, F(f"Panel/{key}#tmp"))
        assert re.search(r"^  m_text: $", tmp, re.M), f"Panel/{key} holds a word of its own; its template is the word"

    # The scene: a RawImage that starts switched off (no texture draws white), fitted by PilotSceneImage against
    # the words panel, drawn over the ground and under everything in Content.
    cls, raw = A.doc_of(pf, F("Scene#raw"))
    assert raw and C.RAW_IMAGE in raw and "  m_Enabled: 0\n" in raw, "the scene's RawImage must start switched off"
    assert "  m_Texture: {fileID: 0}\n" in raw, "the scene must reference no texture: it is loaded by name"
    _, scene = A.doc_of(pf, F("Scene#scene"))
    assert f"  keepClearOf: {{fileID: {F('Panel#rt')}}}\n" in scene, "the scene is not cut against the words panel"
    assert "  reachScreenEdges: 1\n" in scene, "the scene must reach the screen's edges"
    kids = children_of(pf, F("root#rt"))
    assert kids == [str(F("Dim#rt")), str(F("Scene#rt")), str(F("Content#rt"))], \
        "the root must hold the ground, the scene and Content, in that order"
    _, dim = A.doc_of(pf, F("Dim#img"))
    opaque = re.search(r"^  m_Color: \{r: [\d.e-]+, g: [\d.e-]+, b: [\d.e-]+, a: 1\}$", dim, re.M)
    assert "  m_RaycastTarget: 1\n" in dim and opaque, "the ground must be opaque and take the press"
    for key in ("Scene", "Content", "Dim"):
        assert rect_field(pf, F(f"{key}#rt"), "m_AnchorMin") == (0, 0) \
            and rect_field(pf, F(f"{key}#rt"), "m_AnchorMax") == (1, 1) \
            and rect_field(pf, F(f"{key}#rt"), "m_SizeDelta") == (0, 0), f"{key} must fill the page"
    assert rect_field(pf, F("Content#rt"), "m_Pivot") == rect_field(pf, F("root#rt"), "m_Pivot"), \
        "Content must share the root's pivot: the scene measures the words in Content's space"

    # Focus lands on the first control that takes it: Next, before the panel and Close.
    order = children_of(pf, F("Content#rt"))
    assert order[0] == str(F("Stepper#rt")), "the step through the crew must be Content's first child"
    step_kids = children_of(pf, F("Stepper#rt"))
    assert step_kids.index(str(F("Stepper/Next#rt"))) < step_kids.index(str(F("Stepper/Previous#rt"))), \
        "Next must come before Previous in the hierarchy"

    # The panel is authored against the left edge (PlacePanel mirrors it) and clears the friends orb.
    against_left = all(rect_field(pf, F("Panel#rt"), field) == (0, 0)
                       for field in ("m_AnchorMin", "m_AnchorMax", "m_Pivot"))
    assert against_left, "the words panel must be authored against the bottom left"
    x, y = rect_field(pf, F("Panel#rt"), "m_AnchoredPosition")
    assert x >= 24 and y >= ORB_TOP + 8, "the words panel must sit inside the safe padding and above the friends orb"
    assert PANEL_BOTTOM + PANEL_H <= 1080 - 84, "the words panel must fit a 1080-high canvas under Close"
    assert STEP_BOTTOM + STEP_H < PANEL_BOTTOM, "the step plate must sit under the words panel"
    assert K.label_width(WIDEST_NAME, "H1") <= INNER, "the name's row does not hold the widest name a pilot can have"
    story, sight = (next(r for r in ROWS if r[0] == key) for key in ("Story", "Sight"))
    assert story[3] >= STORY_LINES * K.line_height("Body"), "the story's rows do not hold the longest story"
    assert sight[3] >= SIGHT_LINES * K.line_height("Body"), "the sight's rows do not hold SIGHT_LINES lines"
    for meaning in callsign_meanings():
        assert len(wrapped(meaning, "Body", INNER)) <= SIGHT_LINES, \
            f"a callsign's meaning needs more than {SIGHT_LINES} lines of the Sight row: {meaning}"
    for key, _, _, _, _, token, _ in ROWS:
        assert token in READABLE, f"Panel/{key} is drawn in {token}, which does not read on the plate"
    for (_, _, y0, h0, *_), (key, _, y1, *_rest) in zip(ROWS, ROWS[1:]):
        assert y0 + h0 <= y1, f"Panel/{key} overlaps the row above it"
    assert ROWS[-1][2] + ROWS[-1][3] <= PANEL_H - PAD, "the last row runs into the panel's padding"

    _, anim = A.doc_of(pf, F("root#anim"))
    assert STANDARD_CONTROLLER in anim, "the root's Animator must run Standard.controller"
    assert re.search(r"^  m_Name: Content$", pf, re.M), "Standard.controller animates a child named Content"
    assert pf.count("guid: " + S["close"]) == 1, "Close must close through ModalWindowCloseButton"
    _, cg = A.doc_of(pf, F("root#cg"))
    assert "  m_Alpha: 0\n" in cg, "the page must start hidden"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def self_test(files):
    def expect(label_, mutate):
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False
    pf = prefab_path()

    def unwire(field):
        pattern = r"(  " + field + r": )\{fileID: -?\d+\}"
        return lambda f: f.__setitem__(pf, re.sub(pattern, r"\1{fileID: 0}", f[pf], count=1))

    def swap(old, new):
        def mutate(f):
            assert old in f[pf], f"the control's own text is not in the prefab: {old[:50]}"
            f[pf] = f[pf].replace(old, new, 1)
        return mutate

    def doc_swap(fid, old, new):
        """Replace `old` inside one document of the prefab."""
        def mutate(f):
            head = f"&{fid}\n"
            a = f[pf].index(head)
            b = f[pf].find("\n--- ", a)
            b = len(f[pf]) if b < 0 else b + 1          # the document's last line keeps its newline
            assert old in f[pf][a:b], f"the control's own text is not in document {fid}: {old[:50]}"
            f[pf] = f[pf][:a] + f[pf][a:b].replace(old, new, 1) + f[pf][b:]
        return mutate

    kids = "".join(f"  - {{fileID: {F(k + '#rt')}}}\n" for k in ("Dim", "Scene", "Content"))
    flipped = "".join(f"  - {{fileID: {F(k + '#rt')}}}\n" for k in ("Scene", "Dim", "Content"))
    cases = [
        ("the wrong modal type", swap(f"  ModalType: {PAGE_TYPE}\n", "  ModalType: 20\n")),
        ("the story text unwired", unwire("storyText")),
        ("the Next button unwired", unwire("nextButton")),
        ("the scene image unwired", unwire("sceneImage")),
        ("the scene catalog left off", swap(f"guid: {SCENE_CATALOG}", f"guid: {S['page']}")),
        ("a template naming a token the page does not fill",
         swap(f"  homeTemplate: {ystr('From {homeworld}')}\n", f"  homeTemplate: {ystr('From {planet}')}\n")),
        ("a row holding a word of its own",
         doc_swap(F("Panel/Name#tmp"), "  m_text: \n", "  m_text: Veliane Crestward\n")),
        ("a scene that starts switched on (a white rectangle)",
         doc_swap(F("Scene#raw"), "  m_Enabled: 0\n", "  m_Enabled: 1\n")),
        ("a scene cut against nothing", doc_swap(F("Scene#scene"), f"  keepClearOf: {{fileID: {F('Panel#rt')}}}\n",
                                                 "  keepClearOf: {fileID: 0}\n")),
        ("a scene held inside the safe area",
         doc_swap(F("Scene#scene"), "  reachScreenEdges: 1\n", "  reachScreenEdges: 0\n")),
        ("the scene drawn under the ground", swap(kids, flipped)),
        ("a ground that lets the press through",
         doc_swap(F("Dim#img"), "  m_RaycastTarget: 1\n", "  m_RaycastTarget: 0\n")),
        ("a words panel authored against the right edge",
         doc_swap(F("Panel#rt"), "  m_Pivot: {x: 0, y: 0}\n", "  m_Pivot: {x: 1, y: 0}\n")),
        ("a words panel under the friends orb",
         doc_swap(F("Panel#rt"), f"  m_AnchoredPosition: {{x: {PANEL_INSET}, y: {PANEL_BOTTOM}}}\n",
                  f"  m_AnchoredPosition: {{x: {PANEL_INSET}, y: 96}}\n")),
        ("a page that starts shown", doc_swap(F("root#cg"), "  m_Alpha: 0\n", "  m_Alpha: 1\n")),
        ("a Close that closes nothing", swap("guid: " + S["close"], "guid: " + S["scene"])),
    ]
    ok = all([expect(l, m) for l, m in cases])
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print("OK: PilotProfileView.prefab validates")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        full = os.path.join(REPO, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
