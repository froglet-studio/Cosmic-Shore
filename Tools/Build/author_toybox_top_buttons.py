#!/usr/bin/env python3
"""
Author the Toy Box's TWO BIG BUTTONS above its grid, the way the arcade's weekly challenge and
Maelstrom sit above its cards - plus the one config asset they read.

    python3 Tools/Build/author_toybox_top_buttons.py            # write the scene + the asset
    python3 Tools/Build/author_toybox_top_buttons.py --check     # fail if either drifted
    python3 Tools/Build/author_toybox_top_buttons.py --self-test # prove the checks can fail

WHAT IT MAKES

  Explore/
    Header                 <- moved to the very top, out of the band the buttons now want
    TopButtons/            <- new, the band between the header and the grid
      DailyActivityCard    <- today's activity: which toy, which variant, what it pays
      ShuffleCard          <- re-roll every setting the toys own
    GameSelectScrollView   <- its top anchor lowered to make the band

WHY THE BUTTONS ARE NOT INSIDE THE SCROLL CONTENT (the arcade's shape)

  The arcade's two cards are children of the ScrollRect's Content, and
  Docs/HomeHub/ARCHITECTURE.md records what that cost: a ScrollRect's Content is a SCROLL EXTENT,
  not a layout frame, its VerticalLayoutGroup is disabled, and two of its three children are
  anchored to a FRACTION of its height - so every unit added to the content stretched the grid by
  0.625 and the Maelstrom banner by 0.239, and a 13th arcade card became unreachable. These two
  are siblings of the scroll view, pinned, so nothing about the grid's extent can move them and
  growing the toy grid cannot push them off screen. The stated difference in behaviour: they do
  not scroll away, where the arcade's do.

HOW IT BUILDS THEM

  Every component is CLONED from a shipped document in this same window - the Button and plate
  Image off the Toy Box's own CloseButton, the labels off the toy card template's TMP - so each
  one carries the project's font, material and theme rather than Unity's defaults. That is
  author_toybox_layout.py's own technique (ensure_label), and this script imports that module's
  scene model rather than keeping a second copy of it.
"""
import argparse
import hashlib
import importlib.util
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is one dirname too shallow: {ROOT}"

SCENE = os.path.join(ROOT, "Assets", "_Scenes", "Menu_Main.unity")
CONFIG_ASSET = os.path.join(ROOT, "Assets", "Resources", "ToyboxDailyActivityConfig.asset")

# ---- imported scene model (one implementation, not two) --------------------------------------
_spec = importlib.util.spec_from_file_location(
    "author_toybox_layout", os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                         "author_toybox_layout.py"))
L = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(L)

Scene, fmt, sub1 = L.Scene, L.fmt, L.sub1
set_field, set_rect, set_band = L.set_field, L.set_rect, L.set_band
ensure_label, make_sliced, stretch = L.ensure_label, L.make_sliced, L.stretch
TMP_GUID, IMAGE_GUID = L.TMP_GUID, L.IMAGE_GUID

BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"           # UnityEngine.UI.Button
PLATE_SPRITE_GUID = "54ad1e72496cc12498291c5a57d81f8b"      # Group 1585.png  - the card body
RIM_SPRITE_GUID = "a0f080f06c102c7469074c50e7457822"        # Rectangle 1127 (2).png - the rim

# The two card scripts. Guids are their .meta files'; a mismatch here resolves to NO component,
# which renders a card that draws and does nothing - so they are asserted against disk below.
DAILY_CARD_GUID = "524532d6b415adadc9588fbbcde710ea"
SHUFFLE_CARD_GUID = "33b969fb76b1eb511209bd8b91d2ef86"
CONFIG_SO_GUID = "0b6caf89a2b24da2e6a92b8ae5db2ff9"
SCRIPT_GUIDS = {
    DAILY_CARD_GUID: "Assets/_Scripts/UI/Elements/DailyActivityCard.cs",
    SHUFFLE_CARD_GUID: "Assets/_Scripts/UI/Elements/ToyboxShuffleCard.cs",
    CONFIG_SO_GUID: "Assets/_Scripts/ScriptableObjects/Toys/ToyboxDailyActivityConfigSO.cs",
}

# ---- the numbers ----------------------------------------------------------------------------
# The window is 1920 x 908.7 and the right column runs x 0.311..0.982. Bands are fractions of the
# Explore panel, which stretches to the whole window.
#
# Budget, top to bottom: the header (33 px), the button band (177 px), the grid (654 px). The grid
# keeps two full 250-tall rows, which is what it showed before - a third row was never visible.
HEADER_BAND = ((0.449, 0.9450), (0.824, 0.9850))
BUTTON_BAND = ((0.3114, 0.7350), (0.9820, 0.9300))
GRID_TOP = 0.7200

# Two cards side by side inside the band, with a gap between them.
DAILY_RECT = ((0.000, 0.0), (0.490, 1.0))
SHUFFLE_RECT = ((0.510, 0.0), (1.000, 1.0))

# Labels, as fractions of their own card. (anchorMin, anchorMax, sizeMin, sizeMax, align, alpha).
# align is TMP's (horizontal, vertical): 1 = left, 512 = middle, 256 = top, 1024 = bottom.
DAILY_LABELS = [
    ("Title",    (0.06, 0.660), (0.94, 0.940), 18, 24, (1, 512), 0.70),
    ("Activity", (0.06, 0.300), (0.94, 0.640), 30, 44, (1, 512), 1.00),
    ("Toy",      (0.06, 0.170), (0.94, 0.300), 14, 20, (1, 512), 0.70),
    ("Status",   (0.06, 0.045), (0.94, 0.170), 14, 20, (1, 512), 0.85),
]
SHUFFLE_LABELS = [
    ("Title",  (0.06, 0.500), (0.94, 0.880), 24, 36, (1, 512), 1.00),
    ("Detail", (0.06, 0.140), (0.94, 0.460), 16, 24, (1, 512), 0.70),
]
# The accent strip the daily card tints with the offering toy's own colour.
ACCENT_RECT = ((0.0, 0.0), (1.0, 0.030))

CONFIG_YAML = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __SO_GUID__, type: 3}
  m_Name: ToyboxDailyActivityConfig
  m_EditorClassIdentifier: 
  rewardCrystals: 10
  activityTitle: TODAY'S ACTIVITY
  shuffleTitle: SHUFFLE THE TOY BOX
  shuffleDetail: NEW WORLD, NEW COLOURS, NEW HULL
""".replace("__SO_GUID__", CONFIG_SO_GUID)

CONFIG_META = """fileFormatVersion: 2
guid: __ASSET_GUID__
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""".replace("__ASSET_GUID__",
            hashlib.md5(b"CosmicShore/ToyboxTopButtons/Resources/ToyboxDailyActivityConfig.asset")
            .hexdigest())


# The window, for stating the bands in pixels. Read off the scene rather than written down.
WINDOW_FALLBACK = (1920.0, 908.7)


def assert_layout(window=WINDOW_FALLBACK):
    """The three bands must not overlap and the two cards must not touch.

    Stated as an ORDERING over the constants above rather than as the pixel numbers they happen to
    produce, so moving one band can only fail here - never ship a header sitting on top of a button
    or a button clipped by the grid, which is a defect that looks like a layout opinion.
    """
    w, h = window
    bands = [("grid", 0.0, GRID_TOP),
             ("buttons", BUTTON_BAND[0][1], BUTTON_BAND[1][1]),
             ("header", HEADER_BAND[0][1], HEADER_BAND[1][1])]
    for name, lo, hi in bands:
        assert hi > lo, f"{name} band is inverted: {lo} .. {hi}"
    for (an, _, a_hi), (bn, b_lo, _) in zip(bands, bands[1:]):
        assert b_lo > a_hi, f"{an} (top {a_hi}) overlaps {bn} (bottom {b_lo})"

    assert DAILY_RECT[1][0] < SHUFFLE_RECT[0][0], "the two cards overlap horizontally"

    # Every label inside a card has to stay inside it and clear of its neighbours.
    for card, labels in (("daily", DAILY_LABELS), ("shuffle", SHUFFLE_LABELS)):
        rows = sorted(((amin[1], amax[1], name) for name, amin, amax, *_ in labels))
        for lo, hi, name in rows:
            assert 0.0 <= lo < hi <= 1.0, f"{card}/{name} is outside its card: {lo} .. {hi}"
        for (a_lo, a_hi, an), (b_lo, b_hi, bn) in zip(rows, rows[1:]):
            assert b_lo >= a_hi, f"{card}: {an} (top {a_hi}) overlaps {bn} (bottom {b_lo})"
        assert rows[0][0] >= ACCENT_RECT[1][1], \
            f"{card}: the lowest label ({rows[0][2]}) sits on the accent strip"

    band_w = (BUTTON_BAND[1][0] - BUTTON_BAND[0][0]) * w
    band_h = (BUTTON_BAND[1][1] - BUTTON_BAND[0][1]) * h
    card_w = (DAILY_RECT[1][0] - DAILY_RECT[0][0]) * band_w
    return card_w, band_h


# ---------------------------------------------------------------------------------------------
# object creation
# ---------------------------------------------------------------------------------------------
def mint(sc, key):
    """A fileID in THIS script's own salt space, so a re-run is a no-op and the ids cannot
    collide with author_toybox_layout.py's."""
    taken = set(sc.docs)
    for salt in range(64):
        h = hashlib.md5(f"CosmicShore/ToyboxTopButtons/{key}/{salt}".encode()).hexdigest()
        fid = str(int(h[:16], 16) % (L.INT64_MAX // 2))
        if fid not in taken:
            return fid
    raise SystemExit("could not mint a fileID for " + key)


def child_named(sc, parent_go, name):
    for ch in sc.children(parent_go):
        if sc.names.get(ch) == name:
            return ch
    return None


def parent_child(sc, parent_go, child_rt, why):
    """Register a child on its parent's transform, LAST so it draws over its siblings."""
    prt = sc.rect_of(parent_go)
    b = sc.body(prt)
    if f"- {{fileID: {child_rt}}}" in b:
        return
    if "m_Children: []" in b:
        b = b.replace("  m_Children: []", f"  m_Children:\n  - {{fileID: {child_rt}}}", 1)
    else:
        b = sub1(b, r"(^  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*)",
                 lambda m: m.group(1) + f"  - {{fileID: {child_rt}}}\n")
    sc.set_body(prt, b, f"{why}: parent")


def add_object(sc, parent_go, name, amin, amax, key, components):
    """A new GameObject with a RectTransform and `components` (a list of (class, body-factory))."""
    go = mint(sc, f"go/{key}")
    rt = mint(sc, f"rt/{key}")
    comp_ids = [rt] + [mint(sc, f"c{i}/{key}") for i in range(len(components))]

    lines = "\n".join(f"  - component: {{fileID: {c}}}" for c in comp_ids)
    sc.add_doc(go, f"--- !u!1 &{go}", f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{lines}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sc.add_doc(rt, f"--- !u!224 &{rt}", f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {sc.rect_of(parent_go)}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {fmt(amin[0])}, y: {fmt(amin[1])}}}
  m_AnchorMax: {{x: {fmt(amax[0])}, y: {fmt(amax[1])}}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
""")
    for (cls, factory), cid in zip(components, comp_ids[1:]):
        sc.add_doc(cid, f"--- !u!{cls} &{cid}", factory(go, cid))

    parent_child(sc, parent_go, rt, name)
    sc.rebuild_maps()
    return go, dict(zip([c for c, _ in components], comp_ids[1:]))


def canvas_renderer(go, cid):
    return f"""
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
"""


def image_body(sprite_guid, raycast=1, color=(1, 1, 1, 1)):
    def make(go, cid):
        r, g, b, a = color
        return f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {fmt(r)}, g: {fmt(g)}, b: {fmt(b)}, a: {fmt(a)}}}
  m_RaycastTarget: {raycast}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""
    return make


def button_body(sc, donor_button_cid, target_image_ref):
    """The Button, cloned from the window's own CloseButton so it carries the project's colour
    transitions - with m_OnClick CLEARED. Keeping the donor's persistent listeners would wire the
    close button's handler to these cards, and a persistent listener that throws eats every
    runtime listener behind it (CLAUDE.md's dead-card trap)."""
    donor = sc.body(donor_button_cid)

    def make(go, cid):
        b = donor
        b = sub1(b, r"^  m_GameObject: \{fileID: -?\d+\}$", f"  m_GameObject: {{fileID: {go}}}")
        b = sub1(b, r"^  m_TargetGraphic: \{fileID: -?\d+\}$",
                 f"  m_TargetGraphic: {{fileID: {target_image_ref}}}")
        # m_OnClick is Button's last serialized field, so truncating from it is safe.
        b = re.sub(r"\n  m_OnClick:\n.*$",
                   "\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n",
                   b, flags=re.S)
        return b
    return make


def script_body(guid, fields):
    def make(go, cid):
        lines = "\n".join(f"  {k}: {v}" for k, v in fields.items())
        return f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
{lines}
"""
    return make


def ensure_card(sc, parent_go, name, script_guid, script_fields, rect, donor_button_cid):
    """A big button: plate + Button + its card script. Idempotent by NAME."""
    existing = child_named(sc, parent_go, name)
    if existing:
        set_rect(sc, existing, rect[0], rect[1], (0, 0), (0, 0), (0.5, 0.5), name)
        make_sliced(sc, existing, PLATE_SPRITE_GUID[:8], name)
        return existing, sc.component_with(existing, guid=script_guid)

    image_ref = mint(sc, f"c1/{name}")     # the Image is component index 1 - see add_object
    go, comps = add_object(sc, parent_go, name, rect[0], rect[1], name, [
        ("222", canvas_renderer),
        ("114", image_body(PLATE_SPRITE_GUID)),
        ("114", button_body(sc, donor_button_cid, image_ref)),
        ("114", script_body(script_guid, script_fields)),
    ])
    # add_object mints "c0/..", "c1/..", ... in order, so the Image really is c1.
    actual_image = None
    for cid in sc.components(go):
        if cid in sc.docs and IMAGE_GUID in sc.body(cid):
            actual_image = cid
            break
    if actual_image and actual_image != image_ref:
        btn = sc.component_with(go, guid=BUTTON_GUID)
        if btn:
            set_field(sc, btn, "m_TargetGraphic", f"{{fileID: {actual_image}}}", f"{name}: target graphic")

    make_sliced(sc, go, PLATE_SPRITE_GUID[:8], name)
    return go, sc.component_with(go, guid=script_guid)


def ensure_accent(sc, card_go, name):
    existing = child_named(sc, card_go, name)
    if existing:
        set_rect(sc, existing, ACCENT_RECT[0], ACCENT_RECT[1], (0, 0), (0, 0), (0.5, 0.5), name)
        return existing
    go, _ = add_object(sc, card_go, name, ACCENT_RECT[0], ACCENT_RECT[1], f"{card_go}/{name}", [
        ("222", canvas_renderer),
        ("114", image_body(RIM_SPRITE_GUID, raycast=0)),
    ])
    make_sliced(sc, go, RIM_SPRITE_GUID[:8], name)
    return go


def bind_ref(sc, comp, field, target_cid, why):
    """Point a serialized reference at a component, ADDING the key when the body does not carry it.

    <para>The add is the load-bearing half. Unity writes only the fields a component HAS when the
    scene was last saved, so a field added to a C# class is simply absent from every already-saved
    document - and set_field is a regex REPLACE, so it matches nothing and silently does nothing.
    The result is the exact shape of a feature that is authored, documented and dead: the card
    exists, the modal draws the grid, and the two buttons are never bound to anything. Unity
    applies only the keys a file carries and falls back to the C# initializer (null) for the rest,
    which is why the absence is invisible rather than an error.</para>
    """
    b = sc.body(comp)
    value = f"{{fileID: {target_cid}}}"
    if re.search(r"^  %s:" % re.escape(field), b, re.M):
        set_field(sc, comp, field, value, why)
        return
    # Appended at the end of the mapping - YAML mappings are unordered, and Unity re-serialises
    # into its own field order the first time it saves the scene.
    sc.set_body(comp, b.rstrip("\n") + f"\n  {field}: {value}\n", f"{why}: add {field}")


# ---------------------------------------------------------------------------------------------
def author(sc):
    card_w, card_h = assert_layout()
    print(f"  bands OK - each button {card_w:.0f} x {card_h:.0f} px, "
          f"grid keeps {GRID_TOP * WINDOW_FALLBACK[1]:.0f} px")

    for guid, path in SCRIPT_GUIDS.items():
        meta = os.path.join(ROOT, path + ".meta")
        if not os.path.exists(meta):
            raise SystemExit(f"{path}.meta is missing - the scene cannot reference the script")
        if f"guid: {guid}" not in open(meta).read():
            raise SystemExit(f"{path}.meta does not carry {guid} - a script guid drifted, and a "
                             "wrong one resolves to NO component: a card that draws and does nothing")

    tsm = sc.find_root("ToyboxScreenModal")
    tgc = sc.find_root("ToyboxGameConfigureModal")
    if not tsm or not tgc:
        raise SystemExit("Menu_Main: the two Toy Box windows were not found - run Home Hub Wiring first")

    explore = sc.walk(tsm, "Explore")
    if not explore:
        raise SystemExit("Menu_Main: ToyboxScreenModal/Explore was not found")

    close_button = sc.walk(tsm, "CloseButton")
    donor_button = sc.component_with(close_button, guid=BUTTON_GUID) if close_button else None
    if not donor_button:
        raise SystemExit("Menu_Main: no Button to clone from (the Toy Box's CloseButton)")

    donor_tmp = sc.walk(tgc, "ToyVariantTemplate/GameDetail")
    if not donor_tmp:
        raise SystemExit("Menu_Main: no TMP document to clone the button labels from")

    # ── make room ───────────────────────────────────────────────────────────
    header = sc.walk(tsm, "Explore/Header")
    if header:
        set_rect(sc, header, HEADER_BAND[0], HEADER_BAND[1], (0, 0), (0, 0), (0.5, 1), "Explore/Header")

    scroll = sc.walk(tsm, "Explore/GameSelectScrollView")
    if scroll:
        b = sc.body(sc.rect_of(scroll))
        amin = re.search(r"m_AnchorMin: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", b)
        amax = re.search(r"m_AnchorMax: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", b)
        pos = re.search(r"m_AnchoredPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", b)
        size = re.search(r"m_SizeDelta: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", b)
        set_rect(sc, scroll,
                 (float(amin.group(1)), float(amin.group(2))),
                 (float(amax.group(1)), GRID_TOP),
                 (float(pos.group(1)), float(pos.group(2))),
                 (float(size.group(1)), float(size.group(2))),
                 None, "Explore/GameSelectScrollView")

    # ── the band ────────────────────────────────────────────────────────────
    band = child_named(sc, explore, "TopButtons")
    if band:
        set_rect(sc, band, BUTTON_BAND[0], BUTTON_BAND[1], (0, 0), (0, 0), (0.5, 0.5), "TopButtons")
    else:
        band, _ = add_object(sc, explore, "TopButtons", BUTTON_BAND[0], BUTTON_BAND[1],
                             "TopButtons", [])

    # ── the two cards ───────────────────────────────────────────────────────
    daily, daily_script = ensure_card(
        sc, band, "DailyActivityCard", DAILY_CARD_GUID,
        {"titleText": "{fileID: 0}", "activityText": "{fileID: 0}", "toyText": "{fileID: 0}",
         "statusText": "{fileID: 0}", "accentFill": "{fileID: 0}", "claimedBadge": "{fileID: 0}"},
        DAILY_RECT, donor_button)

    shuffle, shuffle_script = ensure_card(
        sc, band, "ShuffleCard", SHUFFLE_CARD_GUID,
        {"titleText": "{fileID: 0}", "detailText": "{fileID: 0}", "accentFill": "{fileID: 0}"},
        SHUFFLE_RECT, donor_button)

    for card, script, labels, field_of in (
            (daily, daily_script, DAILY_LABELS,
             {"Title": "titleText", "Activity": "activityText", "Toy": "toyText",
              "Status": "statusText"}),
            (shuffle, shuffle_script, SHUFFLE_LABELS,
             {"Title": "titleText", "Detail": "detailText"})):
        for name, amin, amax, lo, hi, align, alpha in labels:
            go = ensure_label(sc, card, name, donor_tmp, amin, amax, lo, hi, align, alpha,
                              f"{sc.names.get(card)}/{name}")
            if script:
                tmp = sc.component_with(go, guid=TMP_GUID)
                if tmp:
                    bind_ref(sc, script, field_of[name], tmp, f"{sc.names.get(card)}.{field_of[name]}")

    accent = ensure_accent(sc, daily, "Accent")
    if daily_script:
        img = sc.component_with(accent, guid=IMAGE_GUID)
        if img:
            bind_ref(sc, daily_script, "accentFill", img, "DailyActivityCard.accentFill")

    # ── hand the cards to the modal ─────────────────────────────────────────
    modal = sc.component_with(tsm, field="cardGrid")
    if not modal:
        raise SystemExit("Menu_Main: the ToyboxModal component was not found on ToyboxScreenModal")
    bind_ref(sc, modal, "dailyActivityCard", daily_script, "ToyboxModal.dailyActivityCard")
    bind_ref(sc, modal, "shuffleCard", shuffle_script, "ToyboxModal.shuffleCard")


# ---------------------------------------------------------------------------------------------
def render(path):
    sc = Scene(path)
    author(sc)
    out = [sc.preamble]
    for fid in sc.order:
        h, b = sc.docs[fid]
        out.append(h + b)
    return "".join(out), sc.changes


def write_config():
    """The one asset the buttons read. Optional to the runtime (every field has a code default),
    authored so the copy and the reward are tunable without a recompile."""
    wrote = []
    for path, text in ((CONFIG_ASSET, CONFIG_YAML), (CONFIG_ASSET + ".meta", CONFIG_META)):
        current = open(path).read() if os.path.exists(path) else None
        if current != text:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            open(path, "w").write(text)
            wrote.append(os.path.relpath(path, ROOT))
    return wrote


def check_config():
    bad = []
    for path, text in ((CONFIG_ASSET, CONFIG_YAML), (CONFIG_ASSET + ".meta", CONFIG_META)):
        rel = os.path.relpath(path, ROOT)
        if not os.path.exists(path):
            bad.append(f"{rel}: missing")
        elif open(path).read() != text:
            bad.append(f"{rel}: drifted from what this script would write")
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="fail if the scene or the asset differs from what this would write")
    ap.add_argument("--self-test", action="store_true",
                    help="prove --check can fail (it is read against disk, not against itself)")
    args = ap.parse_args()

    if args.self_test:
        text, _ = render(SCENE)
        on_disk = open(SCENE, errors="ignore").read()
        if text != on_disk:
            print("self-test: the scene is not authored yet, so --check would already fail - run "
                  "the script first, then --self-test")
            return 1
        mutated = on_disk.replace("m_Name: DailyActivityCard", "m_Name: DailyActivityCardX", 1)
        if mutated == on_disk:
            print("self-test FAILED: nothing to mutate - the card is not in the scene")
            return 1
        print("self-test: a renamed card makes the render differ from disk ->",
              "DETECTED" if render_differs(mutated) else "MISSED")
        return 0 if render_differs(mutated) else 1

    if args.check:
        text, _ = render(SCENE)
        problems = check_config()
        if text != open(SCENE, errors="ignore").read():
            problems.insert(0, "Assets/_Scenes/Menu_Main.unity: drifted from what this script "
                               "would write (re-run without --check)")
        if problems:
            print("author_toybox_top_buttons --check FAILED:")
            for p in problems:
                print("  -", p)
            return 1
        print("author_toybox_top_buttons --check: OK (scene + config asset match)")
        return 0

    text, changes = render(SCENE)
    before = open(SCENE, errors="ignore").read()
    if text != before:
        open(SCENE, "w").write(text)
        print(f"Menu_Main.unity: {len(changes)} edit(s)")
        for c in changes[:40]:
            print("  -", c)
        if len(changes) > 40:
            print(f"  ... and {len(changes) - 40} more")
    else:
        print("Menu_Main.unity: already authored")

    wrote = write_config()
    for w in wrote:
        print("wrote", w)
    if not wrote:
        print("ToyboxDailyActivityConfig.asset: already authored")
    return 0


def render_differs(mutated_scene_text):
    """Would --check notice this mutation? Renders from the mutated text and compares."""
    import tempfile
    with tempfile.NamedTemporaryFile("w", suffix=".unity", delete=False) as fh:
        fh.write(mutated_scene_text)
        tmp = fh.name
    try:
        text, _ = render(tmp)
        return text != mutated_scene_text
    finally:
        os.unlink(tmp)


if __name__ == "__main__":
    sys.exit(main())
