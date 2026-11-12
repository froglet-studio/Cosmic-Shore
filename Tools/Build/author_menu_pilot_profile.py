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

    No scene is edited: ScreenSwitcher.EnsurePilotProfileModal adds the page to the canvas from Resources at start
    when the scene has none, as EnsureCreditsModal adds the credits screen.

THE LAYOUT. The picture is the page; the words sit in one panel on the side the pilot does not stand on (the page
moves it: authored against the left edge) and leave the friends orb's corner clear. Next, Previous and the count sit
in one plate at the bottom centre, the same place for every pilot, so a mouse does not chase them from side to
side. Both plates are the window's black, solid (PLATE_ALPHA), so words read on any picture. EVERY WORD is a template
on PilotProfileModal (WORDS below) or the text of a button.

THE PAD. Next, Previous and Close steer the pad themselves (WAYS below, Unity's Explicit navigation): left from Next
to Previous and right back, up from either step or right from Next to Close, down or left from Close to Next, and
nothing else. The kit's automatic search would leave the page. Every control is drawn inline: a nested kit prefab
keeps its navigation in its own file, where WAYS cannot name it.

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
# The orb itself hides while this page is open (FriendsDrawer.Blocked: any window that is not a hub window), so the
# clearance is a margin that holds if that rule changes, not a meeting the page has today.
PANEL_W, PANEL_H, PANEL_INSET, PANEL_BOTTOM, PAD = 560, 744, 48, 140, 24
INNER = PANEL_W - 2 * PAD
# The ground under words that sit on a painted scene is SOLID. The game blends in linear light (ProjectSettings
# m_ActiveColorSpace 1), so a plate at alpha a lets through (1 - a) of the picture's LIGHT, which is far more of its
# look: at 0.88, the Credits window's dim over the menu, a white sky behind the words is still a mid grey and the
# quiet lines fall under 2:1 (seen in the engine's own render of this page, 2026-11-12; a web drawing blends in sRGB
# and shows the same plate near black). NeutralLightest on SurfaceBlack has about 5:1 with nothing behind it, so there
# is no light to give away. validate() holds every plate to PLATE_CONTRAST for every colour words are drawn in.
PLATE_ALPHA = 1.0
PLATE_CONTRAST = 4.5                                      # WCAG AA for small text, over a WHITE picture
PLATES = ("Panel/Frame/Fill", "Stepper/Frame/Fill", "Window/Close/Plate")
ORB_TOP = 124

# The step through the crew: one plate at the bottom centre.
STEP_W, STEP_H, STEP_BOTTOM, STEP_PAD = 392, 72, 32, 12
BUTTON_W, BUTTON_H, COUNT_W = 128, 48, 96

# The pad's ways round the page: where each of its three buttons sends the selection, and nowhere else (Unity's
# Navigation.Mode.Explicit). The kit's default, Automatic, picks the best-placed live control on the WHOLE canvas, and
# two stay live under every open window: the top nav's arrow buttons (Menu_Main's LeftArrrow and RightArrow, Automatic
# themselves, in a group the menu switches off only for freestyle). This page covers them, so a press up from Next or
# left from Close would land on a button nobody can see, and A there would turn the screen under the page (worked out
# with Unity's own score, dot / distance squared, after a pad run in the engine left the page, 2026-11-12). A way that
# is not named goes nowhere. Close stays a stop: the keyboard has no other way to it. With a crew of ONE the page
# disables both steps (PilotProfileModal.Draw) and Close's ways still name Next: Explicit navigation moves to a disabled
# button, which draws no ring, and up comes back. Play never leaves a crew of one (the starter crew is one pilot a
# callsign and no play code removes a pilot); only a damaged save can, where the repair after a load
# (AccountInventory.Normalize) drops every entry but one. Docs/REFACTOR_BACKLOG.md row PP6 has what the pad does then.
NAV_EXPLICIT = 4
WAY_NAMES = ("Up", "Down", "Left", "Right")               # the order Unity writes them in
WAYS = {
    "Stepper/Next": dict(Left="Stepper/Previous", Up="Window/Close", Right="Window/Close"),
    "Stepper/Previous": dict(Right="Stepper/Next", Up="Window/Close"),
    "Window/Close": dict(Down="Stepper/Next", Left="Stepper/Next"),
}

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
# The painted pilots' own words (the starter crew, Tools/Build/author_pilot_scene_assets.py writes them into
# Resources/Pilots/PaintedCrew): short paragraphs, a blank line between, and all of it inside the Story row.
PAINTED_PEOPLE = "Docs/Pilots/art/scenes/people.json"
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


def page_tokens():
    """The tokens PilotProfileModal fills, read from its own TokenNames, so this list cannot drift from the page."""
    source = open(os.path.join(REPO, "Assets/_Scripts/UI/Modals/PilotProfileModal.cs"), encoding="utf-8").read()
    names = re.search(r"static readonly string\[\] TokenNames =\s*\{(.*?)\};", source, re.S)
    assert names, "PilotProfileModal.cs no longer declares TokenNames where author_menu_pilot_profile.py reads it"
    tokens = tuple(re.findall(r'"(\w+)"', names.group(1)))
    assert tokens, "PilotProfileModal.TokenNames reads as empty"
    return tokens


TOKENS = page_tokens()
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
    """The ground under words that sit on a picture: the window's black, solid (PLATE_ALPHA), a light hairline."""
    C.frame(parent, key, sliver, "SurfaceBlack", "TextLight", fill_alpha=PLATE_ALPHA)


AUTOMATIC = K.SELECTABLE_FIELDS[:K.SELECTABLE_FIELDS.index("  m_Transition:")]   # the kit's navigation block


def steer(button, key):
    """Give `button` (the node at `key`) the page's own ways (WAYS) in place of the kit's automatic search."""
    misnamed = sorted(set(WAYS[key]) - set(WAY_NAMES)) + sorted(set(WAYS[key].values()) - set(WAYS))
    assert not misnamed, f"WAYS[{key!r}] names a way or a button the page does not have: {', '.join(misnamed)}"

    def navigation(ctx):
        landing = {way: ctx.c(WAYS[key][way], "button") if way in WAYS[key] else 0 for way in WAY_NAMES}
        return (f"  m_Navigation:\n    m_Mode: {NAV_EXPLICIT}\n    m_WrapAround: 0\n"
                + "".join(f"    m_SelectOn{way}: {{fileID: {landing[way]}}}\n" for way in WAY_NAMES))

    def steered(body):
        def write(ctx, go):
            text = body(ctx, go)
            assert text.count(AUTOMATIC) == 1, f"{key}: the kit's navigation block is not where steer() reads it"
            return text.replace(AUTOMATIC, navigation(ctx))
        return write

    at = [i for i, (comp_key, _, _) in enumerate(button.comps) if comp_key == "button"]
    assert len(at) == 1, f"{key} carries no one button to steer"
    comp_key, class_id, body = button.comps[at[0]]
    button.comps[at[0]] = (comp_key, class_id, steered(body))
    return button


def stepper(content):
    """NEXT, PREVIOUS and "3 / 18" on one plate at the bottom centre. Next is first in the hierarchy: the pad's
    selection lands on the first control that takes it (ScreenSwitcher.PreferredSelectable)."""
    st = content.child(N("Stepper", "Stepper", R((0.5, 0), None, (0, STEP_BOTTOM), (STEP_W, STEP_H), (0.5, 0))))
    plate(st, "Stepper/Frame", 10)
    steer(H.text_button(st, "Stepper/Next", "Next", "Next",
                        R((1, 0.5), None, (-STEP_PAD, 0), (BUTTON_W, BUTTON_H), (1, 0.5)), None, None, None, None),
          "Stepper/Next")
    steer(H.text_button(st, "Stepper/Previous", "Previous", "Previous",
                        R((0, 0.5), None, (STEP_PAD, 0), (BUTTON_W, BUTTON_H), (0, 0.5)), None, None, None, None),
          "Stepper/Previous")
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
    return steer(button, "Window/Close")


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


def story_lines(story):
    """The lines a story takes in the Story row: each paragraph wrapped, and one blank line between paragraphs."""
    lines = 0
    for paragraph in story.split("\n"):
        lines += len(wrapped(paragraph, "Body", INNER)) if paragraph else 1
    return lines


def painted_stories():
    """{callsign word: story as the asset holds it} for every painted pilot."""
    import json
    with open(os.path.join(REPO, PAINTED_PEOPLE), encoding="utf-8") as fh:
        people = json.load(fh)["people"]
    return {word: "\n\n".join(p["story"]) for word, p in people.items()}


def story_errors(stories):
    return [f"{word}'s painted story takes {story_lines(text)} lines of the Story row's {STORY_LINES}"
            for word, text in sorted(stories.items()) if story_lines(text) > STORY_LINES]


def callsign_meanings():
    """What each callsign lights, as authored (the page's Sight row shows one)."""
    with open(os.path.join(REPO, CALLSIGN_SET), encoding="utf-8") as fh:
        found = re.findall(r"^ +meaning: (.*)$", fh.read(), re.M)
    assert found, f"{CALLSIGN_SET} holds no meaning"
    return [m[1:-1].replace("''", "'") if m.startswith("'") else m for m in found]


def luminance(token):
    """A kit token's relative luminance (WCAG): its sRGB channels in linear light, weighted."""
    def linear(channel):
        return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4
    r, g, b, _ = tok(token)
    return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b)


def contrast_over_white(words, plate, alpha):
    """The WCAG contrast of words in the token `words` on a plate of the token `plate` at `alpha`, over a WHITE
    picture, blended in linear light as the game blends it: the plate keeps (1 - alpha) of the picture's light."""
    ground = (1 - alpha) * 1.0 + alpha * luminance(plate)
    ink = luminance(words)
    return (max(ink, ground) + 0.05) / (min(ink, ground) + 0.05)


def ways_written(text, key):
    """A button's navigation as the prefab holds it: its mode, and the file id each way lands on (0: nowhere)."""
    _, doc = A.doc_of(text, F(f"{key}#button"))
    block = re.search(r"^  m_Navigation:\n    m_Mode: (\d+)\n    m_WrapAround: \d+\n"
                      + "".join(rf"    m_SelectOn{way}: \{{fileID: (-?\d+)\}}\n" for way in WAY_NAMES),
                      doc or "", re.M)
    assert block, f"{key} carries no navigation to read"
    return int(block.group(1)), dict(zip(WAY_NAMES, (int(fid) for fid in block.groups()[1:])))


def nested_controls(text):
    """The pad controls `text` holds through the prefabs it nests, all the way down, as the prefabs' file names: a
    Selectable writes its navigation in the file that draws it, so a nested one is on the kit's automatic search
    where WAYS cannot name it. KitButtonSmall holds none of its own: it is a variant, and its base's is found."""
    found = []
    for nested in re.finditer(r"^--- !u!1001 &-?\d+\n(.*?)(?=^--- |\Z)", text, re.M | re.S):
        source = re.search(r"^  m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", nested.group(1), re.M)
        assert source, "a nested prefab names no source"
        kit = sorted(n for n in os.listdir(os.path.join(REPO, K.PREFAB_DIR)) if n.endswith(".prefab"))
        name = next((n for n in kit if K.meta_guid(f"{K.PREFAB_DIR}/{n}") == source.group(1)), None)
        assert name, (f"the page nests a prefab that is not the kit's ({source.group(1)}): "
                      "nested_controls() cannot read it for pad controls")
        with open(os.path.join(REPO, K.PREFAB_DIR, name), encoding="utf-8") as fh:
            inner = fh.read()
        found += [name] * inner.count("  m_Navigation:\n") + nested_controls(inner)
    return found


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

    # Every template names only tokens the page fills. Read from what was written, not from WORDS, so a line changed
    # in the prefab is held to it too; and before the next rule, so a planted token is caught by this one.
    written = re.findall(r"^  (\w+Template): (.*)$", page, re.M)
    assert {field for field, _ in written} == {field for field, _ in WORDS}, \
        "PilotProfileModal's templates in the prefab are not the ones WORDS authors"
    for field, value in written:
        for token in re.findall(r"\{(\w+)\}", value):
            assert token in TOKENS, f"PilotProfileModal.{field} names {{{token}}}, which the page does not fill"
    # Every word is authored, quoted where YAML needs it.
    for field, words in WORDS:
        assert f"  {field}: {ystr(words)}\n" in page, f"PilotProfileModal.{field} is not the authored line"
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

    # The pad stays on the page. Read from what was written: every control that takes the pad is drawn in this file
    # and steers it itself, each way lands on one of the page's own buttons, every button can be reached from every
    # other, and the ways are the authored ones. In that order, so a planted fault is caught by the rule that names it.
    assert pf.count("  m_Navigation:\n") == len(WAYS), \
        "the page holds a control the pad's ways (WAYS) do not name: it would search the whole menu for a neighbour"
    nested = nested_controls(pf)
    assert not nested, \
        (f"the page nests a control the pad's ways (WAYS) cannot name ({', '.join(sorted(set(nested)))}): its navigation "
         "is written in that prefab, the kit's automatic search. Draw it inline (H.text_button) and name it in WAYS")
    button_at = {F(f"{key}#button"): key for key in WAYS}
    lands = {}
    for key in WAYS:
        mode, written_ways = ways_written(pf, key)
        assert mode == NAV_EXPLICIT, \
            (f"{key} must steer the pad itself (Navigation mode {NAV_EXPLICIT}, Explicit): the kit's automatic search "
             "leaves the page for the nav's arrow buttons, which the page covers")
        for way, fid in written_ways.items():
            assert fid == 0 or fid in button_at, f"{key}'s way {way.lower()} leads off the page"
        lands[key] = {button_at[fid] for fid in written_ways.values() if fid}
    for start in WAYS:
        reached, frontier = {start}, [start]
        while frontier:
            for key in lands[frontier.pop()] - reached:
                reached.add(key)
                frontier.append(key)
        lost = sorted(set(WAYS) - reached)
        assert not lost, f"the pad cannot reach {', '.join(lost)} from {start}"
    for key, authored in WAYS.items():
        _, written_ways = ways_written(pf, key)
        for way in WAY_NAMES:
            want = F(f"{authored[way]}#button") if way in authored else 0
            assert written_ways[way] == want, f"{key}'s way {way.lower()} is not the authored one"

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
    errors = story_errors(painted_stories())
    assert not errors, "; ".join(errors)
    for meaning in callsign_meanings():
        assert len(wrapped(meaning, "Body", INNER)) <= SIGHT_LINES, \
            f"a callsign's meaning needs more than {SIGHT_LINES} lines of the Sight row: {meaning}"
    for key, _, _, _, _, token, _ in ROWS:
        assert token in READABLE, f"Panel/{key} is drawn in {token}, which does not read on the plate"
    # Words over a painted scene: each plate, as written (the Image's own alpha and the one KitThemedGraphic paints
    # it with), must hold every colour words are drawn in at PLATE_CONTRAST over a white picture.
    for key in PLATES:
        _, themed = A.doc_of(pf, F(f"{key}#th"))
        _, image = A.doc_of(pf, F(f"{key}#img"))
        assert f"  token: {K.TOKEN_INDEX['SurfaceBlack']}\n" in themed, f"{key} is not the window's black"
        painted = re.search(r"^  alpha: ([\d.e-]+)$", themed, re.M)
        authored = re.search(r"^  m_Color: \{r: [\d.e-]+, g: [\d.e-]+, b: [\d.e-]+, a: ([\d.e-]+)\}$", image, re.M)
        assert painted and authored, f"{key} carries no alpha to read"
        for alpha in (float(painted.group(1)), float(authored.group(1))):
            for token in READABLE:
                got = contrast_over_white(token, "SurfaceBlack", alpha)
                assert got >= PLATE_CONTRAST, \
                    (f"{key} at alpha {alpha:g} leaves {token} words at {got:.1f}:1 over a white picture "
                     f"(the game blends in linear light); they need {PLATE_CONTRAST}:1")
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
    def expect(label_, mutate, by=""):
        """`by`: words of the rule that must catch the control. Caught by a neighbouring rule is not caught."""
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            if by not in str(e):
                print(f"  CAUGHT BY THE WRONG RULE: {label_} ({e}); expected the rule that says '{by}'")
                return False
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

    def redrawn(add):
        """The page drawn again with a control added to the step plate, the way a later edit would add one."""
        def mutate(f):
            root = build_modal()
            content = next(node for node in root.children if node.key == "Content")
            add(next(node for node in content.children if node.key == "Stepper"))
            f[pf] = P.prefab_text(root, NAME)
        return mutate

    place = R((0.5, 1), None, (0, 12), (BUTTON_W, BUTTON_H), (0.5, 0))
    kids = "".join(f"  - {{fileID: {F(k + '#rt')}}}\n" for k in ("Dim", "Scene", "Content"))
    flipped = "".join(f"  - {{fileID: {F(k + '#rt')}}}\n" for k in ("Scene", "Dim", "Content"))
    cases = [
        ("the wrong modal type", swap(f"  ModalType: {PAGE_TYPE}\n", "  ModalType: 20\n")),
        ("the story text unwired", unwire("storyText")),
        ("the Next button unwired", unwire("nextButton")),
        ("the scene image unwired", unwire("sceneImage")),
        ("the scene catalog left off", swap(f"guid: {SCENE_CATALOG}", f"guid: {S['page']}")),
        ("a template naming a token the page does not fill",
         swap(f"  homeTemplate: {ystr('From {homeworld}')}\n", f"  homeTemplate: {ystr('From {planet}')}\n"),
         "which the page does not fill"),
        ("a template changed from its authored words",
         swap(f"  homeTemplate: {ystr('From {homeworld}')}\n", f"  homeTemplate: {ystr('Home: {homeworld}')}\n"),
         "is not the authored line"),
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
        ("a plate the theme paints thin enough for a bright picture to wash out the words",
         doc_swap(F("Panel/Frame/Fill#th"), "  alpha: 1\n", "  alpha: 0.88\n"), "over a white picture"),
        ("a plate authored thin under the step through the crew",
         doc_swap(F("Stepper/Frame/Fill#img"), ", a: 1}\n", ", a: 0.88}\n"), "over a white picture"),
        ("a plate under Close that is not the window's black",
         doc_swap(F("Window/Close/Plate#th"), f"  token: {K.TOKEN_INDEX['SurfaceBlack']}\n",
                  f"  token: {K.TOKEN_INDEX['TextLight']}\n"), "is not the window's black"),
        ("a Close that closes nothing", swap("guid: " + S["close"], "guid: " + S["scene"])),
        ("a fourth button drawn on the step plate that the ways do not name",
         redrawn(lambda st: H.text_button(st, "Stepper/Select", "Select", "Select", place, None, None, None, None)),
         "do not name"),
        ("a fourth button nested from the kit, whose navigation is written in KitButton.prefab",
         redrawn(lambda st: st.child(P.kit_button("Stepper/Select", "Select", "Cta", "SELECT", place))),
         "cannot name"),
        ("the same from the kit's small button, a variant that holds no navigation of its own",
         redrawn(lambda st: st.child(P.kit_button("Stepper/Select", "Select", "Cta", "SELECT", place, small=True))),
         "cannot name"),
        ("a step left on the kit's automatic search (the pad leaves the page for the nav's arrows)",
         doc_swap(F("Stepper/Next#button"), f"    m_Mode: {NAV_EXPLICIT}\n", "    m_Mode: 3\n"),
         "must steer the pad itself"),
        ("a way that lands on something that is not one of the page's buttons",
         doc_swap(F("Stepper/Previous#button"), f"    m_SelectOnUp: {{fileID: {F('Window/Close#button')}}}\n",
                  f"    m_SelectOnUp: {{fileID: {F('Panel/Name#tmp')}}}\n"), "leads off the page"),
        ("a Close the pad cannot leave",
         lambda f: [doc_swap(F("Window/Close#button"), f"    m_SelectOn{way}: {{fileID: {F('Stepper/Next#button')}}}\n",
                             f"    m_SelectOn{way}: {{fileID: 0}}\n")(f) for way in ("Down", "Left")],
         "the pad cannot reach"),
        ("a way changed from the authored one (left from Close lands on Previous)",
         doc_swap(F("Window/Close#button"), f"    m_SelectOnLeft: {{fileID: {F('Stepper/Next#button')}}}\n",
                  f"    m_SelectOnLeft: {{fileID: {F('Stepper/Previous#button')}}}\n"), "is not the authored one"),
    ]
    ok = all([expect(*case) for case in cases])
    long_story = dict(painted_stories())
    long_story["ANVIL"] = long_story["ANVIL"] + "\n\n" + long_story["ANVIL"]
    caught = bool(story_errors(long_story)) and not story_errors(painted_stories())
    print(f"  {'caught' if caught else 'MISSED'}: a painted story longer than the Story row")
    ok = ok and caught
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
