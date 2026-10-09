#!/usr/bin/env python3
"""
Author the launch panel's AI DIFFICULTY row (Easy / Medium / Hard) into
ArcadeGameConfigureModal.prefab, directly under the intensity row.

    python3 Tools/Build/author_ai_difficulty_row.py           # write the prefab
    python3 Tools/Build/author_ai_difficulty_row.py --check   # fail if the prefab drifted from this script

WHAT IT BUILDS

  ConfigurationDetailView/
    Intensity                      (unchanged)
    AIDifficulty                   AIDifficultyPicker - authored INACTIVE; the modal shows it only on
      Header                       a card whose AI reads the setting (AIDifficultyRules.IsOfferedFor)
      Buttons                      HorizontalLayoutGroup, the intensity row's spacing
        Easy / Medium / Hard       Image (the intensity row's plate) + Button + MenuAudio(OptionClick)
          Label                    TextMeshPro, ASCII only

  ...and points MinigameLaunchPanel.aiDifficultyPicker at the picker. Each button is the intensity
  button's size and plate (Game_Option_Border_Active / _Inactive), so EASY sits under 1, MEDIUM under
  2 and HARD under 3 and the two rows read as one panel. The controls block that starts under the
  intensity row is moved down at RUNTIME while the row is shown (MinigameLaunchPanel
  .SetAIDifficultyAvailable measures the row), so every other card keeps its full controls block.

WHY A SCRIPT

  The row is prefab content, and prefab content authored in an editor tool lands in the human's
  working tree rather than on the branch (Docs/TOOLING.md, "Tool output is a deliverable"). This
  writes the prefab directly, with every serialized object cloned from a same-file DONOR of the same
  type (the Intensity Level header, the close button, the intensity row's layout group), so the
  file's own serializer version is right by construction (.claude/skills/asset-surgery section 1).

  --check rebuilds the row from the prefab with the row taken OUT and compares the result to the
  file byte for byte, so a hand edit to the row, a moved neighbour that breaks the layout asserts,
  or a stale picker field all fail it. Every fileID is minted deterministically, so a re-run is a
  no-op.
"""
import argparse
import hashlib
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_toybox_layout import Scene  # noqa: E402
from author_arena_launch_panel_layout import solve_rect, overlaps  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "ArcadeGameConfigureModal.prefab")
PICKER_META = os.path.join(ROOT, "Assets", "_Scripts", "UI", "Elements", "AIDifficultyPicker.cs.meta")
FONT_ASSET = os.path.join(ROOT, "Assets", "Unity Assests", "TextMesh Pro", "Resources",
                          "Fonts & Materials", "ALDRICH-REGULAR SDF.asset")

INT64_MAX = 2 ** 63 - 1
CANVAS = (1920.0, 1080.0)          # Menu_Main's CanvasScaler reference resolution

# script guids
TMP_GUID = "f4688fdb7df04437aeb418b961361dc5"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"
MENU_AUDIO_GUID = "698f3492214843a49b922c5c64f54760"
HLG_GUID = "30649d3a9faa99c48a7b1166b86bf2a0"
PANEL_GUID = "b8cde2beae294092a0e28d5ce9af40a4"       # MinigameLaunchPanel

# the intensity row's plates (IntensitySelectButton.prefab's BorderSpriteSelected / Unselected)
PLATE_LIT = "{fileID: 21300000, guid: 28f47c9f52f227c48a39438a82bbeec7, type: 3}"
PLATE_UNLIT = "{fileID: 21300000, guid: db20d07635711f94c84f0f2a95ccf5d8, type: 3}"

MENU_AUDIO_OPTION_CLICK = 1        # MenuAudioCategory.OptionClick - what the intensity buttons play

# ---- the numbers ------------------------------------------------------------------------------
# The row hangs from the SAME anchor the intensity row's bottom edge uses, with the same +71 offset,
# so it tracks that row under any canvas aspect.
INTENSITY_ANCHOR_X = (0.16662253, 0.82736653)
INTENSITY_BOTTOM_ANCHOR_Y = 0.6919671
INTENSITY_OFFSET_Y = 71.00006
ROW_GAP_UNDER_INTENSITY = 4.0      # px between the intensity row's rect and this row
HEADER_HEIGHT = 34.0
HEADER_FONT = 28.0                 # the intensity header is 32; a sub-heading reads one step down
HEADER_GAP = 4.0
BUTTON_SIZE = (140.8747, 74.7646)  # the intensity buttons' size in this prefab
BUTTON_SPACING = 19.2              # the intensity row's HorizontalLayoutGroup spacing
LABEL_FONT = 24.0
LABEL_PADDING = 8.0                # px either side of a label inside its plate
ROW_HEIGHT = HEADER_HEIGHT + HEADER_GAP + BUTTON_SIZE[1]
LABEL_LIT = "{r: 1, g: 1, b: 1, a: 1}"
LABEL_UNLIT = "{r: 0.49019608, g: 0.49019608, b: 0.49019608, a: 1}"   # the intensity row's (125,125,125)

HEADER_TEXT = "AI Difficulty"
OPTIONS = [("Easy", 1, "EASY"), ("Medium", 2, "MEDIUM"), ("Hard", 3, "HARD")]  # AIDifficulty values

# the controls block must keep at least this much height while the row is shown
MIN_CONTROLS_HEIGHT = 200.0
CONTROLS_GAP = 10.0                # MinigameLaunchPanel.controlsGapUnderDifficultyRow's default


def fail(msg):
    raise SystemExit("author_ai_difficulty_row: " + msg)


def fmt(v):
    """Unity's own spelling: integral values bare, everything else the shortest round-trip."""
    v = round(float(v), 6)
    if v == int(v):
        return str(int(v))
    return repr(v)


def sub1(body, pattern, repl, what):
    new, n = re.subn(pattern, repl, body, count=1, flags=re.M)
    if n != 1:
        fail(f"{what}: pattern not found: {pattern}")
    return new


# ---- reading ----------------------------------------------------------------------------------

def picker_guid():
    m = re.search(r"^guid: (\w+)$", open(PICKER_META).read(), re.M)
    if not m:
        fail("AIDifficultyPicker.cs.meta has no guid")
    return m.group(1)


def go_named(sc, parent_go, name):
    hits = [g for g in sc.children(parent_go) if sc.names.get(g) == name]
    if len(hits) > 1:
        fail(f"{len(hits)} children named {name} under {sc.names.get(parent_go)}")
    return hits[0] if hits else None


def the_go(sc, name):
    hits = [g for g, n in sc.names.items() if n == name and sc.cls(g) == "1"]
    if len(hits) != 1:
        fail(f"expected exactly one GameObject named {name}, found {len(hits)}")
    return hits[0]


def component(sc, go, guid):
    for cid in sc.components(go):
        if cid in sc.docs and guid in sc.body(cid):
            return cid
    fail(f"{sc.names.get(go)} has no component with script {guid}")


def panel_doc(sc):
    hits = [fid for fid, (h, b) in sc.docs.items() if "!u!114" in h and PANEL_GUID in b]
    if len(hits) != 1:
        fail(f"expected one MinigameLaunchPanel, found {len(hits)}")
    return hits[0]


# ---- minting ----------------------------------------------------------------------------------

class Minter:
    def __init__(self, taken):
        self.taken = set(taken)
        self.ids = {}

    def __call__(self, key):
        if key in self.ids:
            return self.ids[key]
        for salt in range(64):
            h = hashlib.md5(f"CosmicShore/AIDifficultyRow/{key}/{salt}".encode()).hexdigest()
            fid = str(int(h[:16], 16) % (INT64_MAX // 2) + 1)
            if fid not in self.taken:
                self.taken.add(fid)
                self.ids[key] = fid
                return fid
        fail("could not mint a fileID for " + key)


def all_keys():
    keys = ["row.go", "row.rt", "row.picker",
            "header.go", "header.rt", "header.cr", "header.tmp",
            "buttons.go", "buttons.rt", "buttons.layout"]
    for name, _, _ in OPTIONS:
        n = name.lower()
        keys += [f"{n}.go", f"{n}.rt", f"{n}.cr", f"{n}.image", f"{n}.button", f"{n}.audio",
                 f"{n}.label.go", f"{n}.label.rt", f"{n}.label.cr", f"{n}.label.tmp"]
    return keys


# ---- strip (the prefab as it was before this script) -------------------------------------------

def strip(sc):
    """Remove the row and every reference to it. Returns the set of fileIDs removed."""
    cdv = the_go(sc, "ConfigurationDetailView")
    row = go_named(sc, cdv, "AIDifficulty")
    removed = set()
    if row:
        for go in sc.subtree(row):
            removed.add(go)
            removed.update(sc.components(go))
        for fid in removed:
            if fid in sc.docs:
                del sc.docs[fid]
        sc.order = [f for f in sc.order if f not in removed]
        cdv_rt = sc.go_tf[cdv]
        body = sc.body(cdv_rt)
        for fid in removed:
            body = re.sub(r"^  - \{fileID: %s\}\n" % fid, "", body, flags=re.M)
        sc.docs[cdv_rt][1] = body
    panel = panel_doc(sc)
    sc.docs[panel][1] = re.sub(r"^  aiDifficultyPicker: \{fileID: -?\d+\}\n", "", sc.body(panel), flags=re.M)
    sc.rebuild_maps()
    return removed


# ---- authoring --------------------------------------------------------------------------------

def clone(sc, donor, fid, go, what):
    """A donor document re-pointed at a new identity: same class, same serializer version."""
    header, body = sc.docs[donor]
    header = re.sub(r"&-?\d+", "&" + fid, header, count=1)
    body = sub1(body, r"^  m_GameObject: \{fileID: -?\d+\}$", f"  m_GameObject: {{fileID: {go}}}", what)
    return header, body


def go_doc(sc, donor_go, fid, name, comps, active):
    header, body = sc.docs[donor_go]
    header = re.sub(r"&-?\d+", "&" + fid, header, count=1)
    comp_block = "".join(f"  - component: {{fileID: {c}}}\n" for c in comps)
    body = re.sub(r"^  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)+",
                  "  m_Component:\n" + comp_block, body, count=1, flags=re.M)
    body = sub1(body, r"^  m_Name: .*$", f"  m_Name: {name}", name)
    body = sub1(body, r"^  m_IsActive: \d$", f"  m_IsActive: {1 if active else 0}", name)
    return header, body


def rect_doc(sc, donor_rt, fid, go, father, children, amin, amax, pos, size, pivot):
    header, body = clone(sc, donor_rt, fid, go, "rect")
    kids = "  m_Children: []\n" if not children else \
        "  m_Children:\n" + "".join(f"  - {{fileID: {c}}}\n" for c in children)
    body = re.sub(r"^  m_Children:(?: \[\])?\n(?:  - \{fileID: -?\d+\}\n)*", kids, body, count=1, flags=re.M)
    body = sub1(body, r"^  m_Father: \{fileID: -?\d+\}$", f"  m_Father: {{fileID: {father}}}", "father")
    for key, v in (("m_AnchorMin", amin), ("m_AnchorMax", amax), ("m_AnchoredPosition", pos),
                   ("m_SizeDelta", size), ("m_Pivot", pivot)):
        body = sub1(body, r"^  %s: \{x: [^,]+, y: [^}]+\}$" % key,
                    f"  {key}: {{x: {fmt(v[0])}, y: {fmt(v[1])}}}", key)
    return header, body


def tmp_doc(sc, donor_tmp, fid, go, text, size, h_align, wrap, spacing):
    header, body = clone(sc, donor_tmp, fid, go, "tmp")
    body = sub1(body, r"^  m_text: .*$", f"  m_text: {text}", "m_text")
    body = sub1(body, r"^  m_fontSize: .*$", f"  m_fontSize: {fmt(size)}", "m_fontSize")
    body = sub1(body, r"^  m_fontSizeBase: .*$", f"  m_fontSizeBase: {fmt(size)}", "m_fontSizeBase")
    body = sub1(body, r"^  m_enableAutoSizing: .*$", "  m_enableAutoSizing: 0", "autosize")
    body = sub1(body, r"^  m_HorizontalAlignment: .*$", f"  m_HorizontalAlignment: {h_align}", "h-align")
    body = sub1(body, r"^  m_VerticalAlignment: .*$", "  m_VerticalAlignment: 512", "v-align")
    body = sub1(body, r"^  m_TextWrappingMode: .*$", f"  m_TextWrappingMode: {wrap}", "wrap")
    body = sub1(body, r"^  m_characterSpacing: .*$", f"  m_characterSpacing: {fmt(spacing)}", "spacing")
    body = sub1(body, r"^  m_RaycastTarget: .*$", "  m_RaycastTarget: 0", "raycast")
    return header, body


def author(sc, mint, pguid):
    cdv = the_go(sc, "ConfigurationDetailView")
    cdv_rt = sc.go_tf[cdv]
    intensity = go_named(sc, cdv, "Intensity")
    if not intensity:
        fail("no Intensity row under ConfigurationDetailView")
    header_donor = go_named(sc, cdv, "Header")
    if not header_donor:
        fail("no Header (Intensity Level) under ConfigurationDetailView")
    close = go_named(sc, cdv, "CloseButton")
    if not close:
        fail("no CloseButton under ConfigurationDetailView")
    intensity_select = go_named(sc, intensity, "IntensitySelect")
    if not intensity_select:
        fail("no IntensitySelect under Intensity")

    d_go = header_donor
    d_rt = sc.go_tf[header_donor]
    d_cr = [c for c in sc.components(header_donor) if sc.cls(c) == "222"][0]
    d_tmp = component(sc, header_donor, TMP_GUID)
    d_image = component(sc, close, IMAGE_GUID)
    d_button = component(sc, close, BUTTON_GUID)
    d_audio = component(sc, close, MENU_AUDIO_GUID)
    d_hlg = component(sc, intensity_select, HLG_GUID)
    d_close_cr = [c for c in sc.components(close) if sc.cls(c) == "222"][0]

    m = mint
    groups = []   # (go_fid, [docs as (fid, header, body)])

    # -- Header ----------------------------------------------------------------------------------
    hdr = [(m("header.rt"), *rect_doc(sc, d_rt, m("header.rt"), m("header.go"), m("row.rt"), [],
                                      (0, 1), (1, 1), (0, 0), (0, HEADER_HEIGHT), (0, 1))),
           (m("header.cr"), *clone(sc, d_cr, m("header.cr"), m("header.go"), "header cr")),
           (m("header.tmp"), *tmp_doc(sc, d_tmp, m("header.tmp"), m("header.go"), HEADER_TEXT,
                                      HEADER_FONT, 1, 0, -1.8))]
    groups.append((m("header.go"), [(m("header.go"), *go_doc(sc, d_go, m("header.go"), "Header",
                                                             [d[0] for d in hdr], True))] + hdr))

    # -- Buttons -----------------------------------------------------------------------------------
    option_rts = [m(f"{n.lower()}.rt") for n, _, _ in OPTIONS]
    buttons_w = len(OPTIONS) * BUTTON_SIZE[0] + (len(OPTIONS) - 1) * BUTTON_SPACING
    hlg_h, hlg_b = clone(sc, d_hlg, m("buttons.layout"), m("buttons.go"), "layout")
    hlg_b = re.sub(r"^    m_Top: .*$", "    m_Top: 0", hlg_b, count=1, flags=re.M)
    hlg_b = sub1(hlg_b, r"^  m_Spacing: .*$", f"  m_Spacing: {fmt(BUTTON_SPACING)}", "spacing")
    btn = [(m("buttons.rt"), *rect_doc(sc, d_rt, m("buttons.rt"), m("buttons.go"), m("row.rt"), option_rts,
                                       (0, 1), (0, 1), (0, -(HEADER_HEIGHT + HEADER_GAP)),
                                       (buttons_w, BUTTON_SIZE[1]), (0, 1))),
           (m("buttons.layout"), hlg_h, hlg_b)]
    groups.append((m("buttons.go"), [(m("buttons.go"), *go_doc(sc, d_go, m("buttons.go"), "Buttons",
                                                               [d[0] for d in btn], True))] + btn))

    picker_options = []
    for i, (name, value, label) in enumerate(OPTIONS):
        n = name.lower()
        x = i * (BUTTON_SIZE[0] + BUTTON_SPACING) + BUTTON_SIZE[0] / 2
        img_h, img_b = clone(sc, d_image, m(f"{n}.image"), m(f"{n}.go"), "image")
        img_b = sub1(img_b, r"^  m_Sprite: .*$", f"  m_Sprite: {PLATE_UNLIT}", "sprite")
        img_b = sub1(img_b, r"^  m_Type: .*$", "  m_Type: 0", "type")
        img_b = sub1(img_b, r"^  m_RaycastTarget: .*$", "  m_RaycastTarget: 1", "raycast")
        aud_h, aud_b = clone(sc, d_audio, m(f"{n}.audio"), m(f"{n}.go"), "audio")
        aud_b = sub1(aud_b, r"^  category: .*$", f"  category: {MENU_AUDIO_OPTION_CLICK}", "category")
        but_h, but_b = clone(sc, d_button, m(f"{n}.button"), m(f"{n}.go"), "button")
        but_b = sub1(but_b, r"^  m_TargetGraphic: \{fileID: -?\d+\}$",
                     f"  m_TargetGraphic: {{fileID: {m(f'{n}.image')}}}", "target graphic")
        call = (f"      - m_Target: {{fileID: {m(f'{n}.audio')}}}\n"
                "        m_TargetAssemblyTypeName: CosmicShore.UI.MenuAudio, Assembly-CSharp\n"
                "        m_MethodName: PlayAudio\n"
                "        m_Mode: 1\n"
                "        m_Arguments:\n"
                "          m_ObjectArgument: {fileID: 0}\n"
                "          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n"
                "          m_IntArgument: 0\n"
                "          m_FloatArgument: 0\n"
                "          m_StringArgument: \n"
                "          m_BoolArgument: 0\n"
                "        m_CallState: 2\n")
        but_b, k = re.subn(r"^      m_Calls:\n(?:      - m_Target:.*\n(?:        .*\n)+)+",
                           "      m_Calls:\n" + call, but_b, count=1, flags=re.M)
        if k != 1:
            fail("button donor's onClick call list not found")
        docs = [(m(f"{n}.rt"), *rect_doc(sc, d_rt, m(f"{n}.rt"), m(f"{n}.go"), m("buttons.rt"),
                                         [m(f"{n}.label.rt")], (0, 1), (0, 1),
                                         (x, -BUTTON_SIZE[1] / 2), BUTTON_SIZE, (0.5, 0.5))),
                (m(f"{n}.cr"), *clone(sc, d_close_cr, m(f"{n}.cr"), m(f"{n}.go"), "cr")),
                (m(f"{n}.image"), img_h, img_b),
                (m(f"{n}.button"), but_h, but_b),
                (m(f"{n}.audio"), aud_h, aud_b)]
        groups.append((m(f"{n}.go"), [(m(f"{n}.go"), *go_doc(sc, d_go, m(f"{n}.go"), name,
                                                             [d[0] for d in docs], True))] + docs))

        lab = [(m(f"{n}.label.rt"), *rect_doc(sc, d_rt, m(f"{n}.label.rt"), m(f"{n}.label.go"), m(f"{n}.rt"),
                                              [], (0, 0), (1, 1), (0, 0), (-2 * LABEL_PADDING, 0), (0.5, 0.5))),
               (m(f"{n}.label.cr"), *clone(sc, d_cr, m(f"{n}.label.cr"), m(f"{n}.label.go"), "label cr")),
               (m(f"{n}.label.tmp"), *tmp_doc(sc, d_tmp, m(f"{n}.label.tmp"), m(f"{n}.label.go"), label,
                                              LABEL_FONT, 2, 0, 0))]
        # the unlit colour is the authored state; the picker recolours the lit one at runtime
        lab[2] = (lab[2][0], lab[2][1], set_color(lab[2][2], LABEL_UNLIT))
        groups.append((m(f"{n}.label.go"), [(m(f"{n}.label.go"), *go_doc(sc, d_go, m(f"{n}.label.go"), "Label",
                                                                         [d[0] for d in lab], True))] + lab))
        picker_options.append((value, m(f"{n}.button"), m(f"{n}.image"), m(f"{n}.label.tmp")))

    # -- Row ---------------------------------------------------------------------------------------
    row_top_offset = INTENSITY_OFFSET_Y - ROW_GAP_UNDER_INTENSITY
    row_rt = rect_doc(sc, d_rt, m("row.rt"), m("row.go"), cdv_rt, [m("header.rt"), m("buttons.rt")],
                      (INTENSITY_ANCHOR_X[0], INTENSITY_BOTTOM_ANCHOR_Y),
                      (INTENSITY_ANCHOR_X[1], INTENSITY_BOTTOM_ANCHOR_Y),
                      (0, row_top_offset), (0, ROW_HEIGHT), (0.5, 1))
    pk_h, pk_b = sc.docs[d_audio]
    pk_h = re.sub(r"&-?\d+", "&" + m("row.picker"), pk_h, count=1)
    pk_b = sub1(pk_b, r"^  m_GameObject: \{fileID: -?\d+\}$", f"  m_GameObject: {{fileID: {m('row.go')}}}", "pk go")
    pk_b = sub1(pk_b, r"^  m_Script: .*$", f"  m_Script: {{fileID: 11500000, guid: {pguid}, type: 3}}", "pk script")
    pk_b = sub1(pk_b, r"^  m_EditorClassIdentifier: .*$",
                "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.AIDifficultyPicker", "pk id")
    pk_b = re.sub(r"^  category: .*\n", "", pk_b, count=1, flags=re.M)
    fields = "  options:\n" + "".join(
        f"  - Difficulty: {v}\n    Button: {{fileID: {b}}}\n    Plate: {{fileID: {p}}}\n    Label: {{fileID: {l}}}\n"
        for v, b, p, l in picker_options)
    fields += (f"  plateSelected: {PLATE_LIT}\n  plateUnselected: {PLATE_UNLIT}\n"
               f"  labelSelected: {LABEL_LIT}\n  labelUnselected: {LABEL_UNLIT}\n")
    pk_b = pk_b.rstrip("\n") + "\n" + fields
    row_docs = [(m("row.rt"), *row_rt), (m("row.picker"), pk_h, pk_b)]
    groups.append((m("row.go"), [(m("row.go"), *go_doc(sc, d_go, m("row.go"), "AIDifficulty",
                                                       [d[0] for d in row_docs], False))] + row_docs))

    # -- place every group where Unity would serialize it: GameObjects ascend by fileID ----------
    for go_fid, docs in sorted(groups, key=lambda g: int(g[0])):
        insert_at = None
        for idx, fid in enumerate(sc.order):
            h = sc.docs[fid][0]
            if h.startswith("--- !u!1001") or (h.startswith("--- !u!1 ") and int(fid) > int(go_fid)):
                insert_at = idx
                break
        if insert_at is None:
            insert_at = len(sc.order)
        for k, (fid, h, b) in enumerate(docs):
            sc.docs[fid] = [h, b]
            sc.order.insert(insert_at + k, fid)

    # -- hook into the hierarchy and the panel ------------------------------------------------------
    body = sc.body(cdv_rt)
    intensity_rt = sc.go_tf[intensity]
    body = sub1(body, r"^(  - \{fileID: %s\}\n)" % intensity_rt,
                lambda mm: mm.group(1) + f"  - {{fileID: {m('row.rt')}}}\n", "cdv children")
    sc.docs[cdv_rt][1] = body

    panel = panel_doc(sc)
    pb = sc.body(panel)
    pb = sub1(pb, r"^(  domainTiles:\n(?:  - \{fileID: -?\d+\}\n)*)",
              lambda mm: mm.group(1) + f"  aiDifficultyPicker: {{fileID: {m('row.picker')}}}\n", "panel field")
    sc.docs[panel][1] = pb
    sc.rebuild_maps()


def set_color(body, color):
    body = sub1(body, r"^  m_fontColor: \{.*\}$", f"  m_fontColor: {color}", "font colour")
    r, g, b, a = [float(x) for x in re.findall(r"[rgba]: ([\d.]+)", color)]
    packed = (round(a * 255) << 24) | (round(b * 255) << 16) | (round(g * 255) << 8) | round(r * 255)
    body = re.sub(r"^(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+$", lambda mm: mm.group(1) + str(packed),
                  body, count=1, flags=re.M)
    return body


# ---- validation -------------------------------------------------------------------------------

def text_of(sc):
    return sc.preamble + "".join(sc.docs[f][0] + sc.docs[f][1] for f in sc.order)


def validate(sc, mint, pguid):
    text = text_of(sc)
    defined = set(re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M))
    ids = list(mint.ids.values())

    # 1. every minted id is defined exactly once, fits a signed int64, and nothing else collides
    for fid in ids:
        n = len(re.findall(r"^--- !u!\d+ &%s(?: stripped)?$" % fid, text, re.M))
        if n != 1:
            fail(f"fileID {fid} defined {n} times")
        if int(fid) > INT64_MAX:
            fail(f"fileID {fid} overflows int64")
    allids = re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M)
    if len(allids) != len(set(allids)):
        fail("duplicate fileIDs in the file")

    # 2. every local reference in the new docs resolves
    for fid in ids:
        h, b = sc.docs[fid]
        for ref in re.findall(r"\{fileID: (-?\d+)\}", b):
            if ref != "0" and ref not in defined:
                fail(f"{fid} references undefined fileID {ref}")

    # 3. two-way links: component <-> GameObject, child <-> parent
    for go in [mint.ids[k] for k in mint.ids if k.endswith(".go")]:
        for c in sc.components(go):
            if f"m_GameObject: {{fileID: {go}}}" not in sc.body(c):
                fail(f"component {c} does not point back at {go}")
        rt = sc.go_tf.get(go)
        if not rt:
            fail(f"{sc.names.get(go)} has no RectTransform")
        parent = sc.tf_parent.get(rt)
        if not parent or not re.search(r"^  - \{fileID: %s\}$" % rt, sc.body(parent), re.M):
            fail(f"{sc.names.get(go)} is not in its parent's m_Children")

    # 4. the panel points at the picker, once
    panel = panel_doc(sc)
    refs = re.findall(r"^  aiDifficultyPicker: \{fileID: (-?\d+)\}$", sc.body(panel), re.M)
    if refs != [mint.ids["row.picker"]]:
        fail(f"MinigameLaunchPanel.aiDifficultyPicker is {refs}, expected the picker")
    if pguid not in sc.body(mint.ids["row.picker"]):
        fail("picker doc does not name AIDifficultyPicker's script guid")

    # 5. the picker's serialized keys are exactly the C# fields
    cs = open(os.path.join(ROOT, "Assets", "_Scripts", "UI", "Elements", "AIDifficultyPicker.cs")).read()
    cs_fields = set(re.findall(r"^        (?:Sprite|Color|Option\[\]) (\w+)", cs, re.M))
    yaml_fields = set(re.findall(r"^  (\w+):", sc.body(mint.ids["row.picker"]), re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}
    if cs_fields != yaml_fields:
        fail(f"picker YAML keys {sorted(yaml_fields)} != C# fields {sorted(cs_fields)}")
    struct_fields = set(re.findall(r"^            public \w+ (\w+);", cs, re.M))
    if struct_fields != {"Difficulty", "Button", "Plate", "Label"}:
        fail(f"Option struct fields changed: {sorted(struct_fields)} - update this script")
    enum = open(os.path.join(ROOT, "Assets", "_Scripts", "Data", "Enums", "AIDifficulty.cs")).read()
    for name, value, _ in OPTIONS:
        if not re.search(r"^\s*%s = %d,$" % (name, value), enum, re.M):
            fail(f"AIDifficulty.{name} is no longer {value}")

    # 6. layout, solved in canvas pixels
    row = sc.tf_go[mint.ids["row.rt"]]
    row_rect = solve_rect(sc, row, CANVAS)
    cdv = the_go(sc, "ConfigurationDetailView")
    cdv_rect = solve_rect(sc, cdv, CANVAS)
    if not (row_rect[0] >= cdv_rect[0] - 0.5 and row_rect[0] + row_rect[2] <= cdv_rect[0] + cdv_rect[2] + 0.5):
        fail(f"row {row_rect} leaves ConfigurationDetailView {cdv_rect} horizontally")
    intensity = go_named(sc, cdv, "Intensity")
    intensity_rect = solve_rect(sc, intensity, CANVAS)
    if overlaps(row_rect, intensity_rect):
        fail(f"row {row_rect} overlaps the intensity row {intensity_rect}")
    for name in ("Toggle", "TeamHolder", "Header", "CloseButton"):
        other = go_named(sc, cdv, name)
        if other and overlaps(row_rect, solve_rect(sc, other, CANVAS)):
            fail(f"row overlaps {name}")
    buttons_rect = solve_rect(sc, sc.tf_go[mint.ids["buttons.rt"]], CANVAS)
    if buttons_rect[0] + buttons_rect[2] > row_rect[0] + row_rect[2] + 0.5:
        fail("the buttons overflow the row")
    controls = the_go(sc, "ControlsDescription")
    c = solve_rect(sc, controls, CANVAS)
    moved_top = row_rect[1] - CONTROLS_GAP
    if moved_top - c[1] < MIN_CONTROLS_HEIGHT:
        fail(f"the controls block would be {moved_top - c[1]:.0f}px tall under the row (< {MIN_CONTROLS_HEIGHT})")
    if c[1] + c[3] < row_rect[1]:
        fail("the controls block no longer starts under the intensity row - the runtime move "
             "in MinigameLaunchPanel.FitControlsUnderDifficultyRow is unnecessary; revisit the layout")

    # 7. every label is ASCII the UI font carries, and fits its plate
    font = open(FONT_ASSET, encoding="utf-8", errors="ignore").read()
    point = float(re.search(r"m_PointSize: ([\d.]+)", font).group(1))
    adv = {int(mm.group(1)): float(mm.group(2)) for mm in re.finditer(
        r"- m_Index: (\d+)\s*\n\s*m_Metrics:\s*\n(?:\s*m_\w+: [-\d.]+\s*\n){4}\s*m_HorizontalAdvance: ([-\d.]+)", font)}
    cmap = {int(mm.group(1)): int(mm.group(2)) for mm in re.finditer(
        r"m_Unicode: (\d+)\s*\n\s*m_GlyphIndex: (\d+)", font)}
    for label, size, width in [(HEADER_TEXT, HEADER_FONT, row_rect[2])] + \
                              [(l, LABEL_FONT, BUTTON_SIZE[0] - 2 * LABEL_PADDING) for _, _, l in OPTIONS]:
        for ch in label:
            if ord(ch) not in cmap:
                fail(f"'{ch}' in '{label}' is not in the UI font")
        w = sum(adv[cmap[ord(ch)]] for ch in label) * size / point
        if w > width:
            fail(f"'{label}' is {w:.0f}px at {size}pt and its rect is {width:.0f}px")
    return row_rect, c, moved_top


def build(path):
    sc = Scene(path)
    original = text_of(sc)
    # The parser must round-trip the file EXACTLY before anything is written through it - a
    # lossy read (an encoding that drops a byte) would ship as a silent edit to unrelated text.
    with open(path, encoding="utf-8", newline="") as fh:
        if fh.read() != original:
            fail("the YAML parser does not round-trip " + os.path.basename(path) + " - refusing to edit it")
    strip(sc)
    pguid = picker_guid()
    mint = Minter(sc.docs.keys())
    for k in all_keys():
        mint(k)
    author(sc, mint, pguid)
    row_rect, controls, moved_top = validate(sc, mint, pguid)
    return sc, original, text_of(sc), row_rect, controls, moved_top


def negative_control(path):
    """Prove the layout gate can fail: an oversized row must be refused."""
    global ROW_HEIGHT
    saved = ROW_HEIGHT
    ROW_HEIGHT = 600.0
    try:
        build(path)
    except SystemExit as e:
        return str(e)
    finally:
        ROW_HEIGHT = saved
    fail("negative control: a 600px row passed the layout gate")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    sc, original, authored, row_rect, controls, moved_top = build(PREFAB)
    print(f"row      L={row_rect[0]:.1f} B={row_rect[1]:.1f} W={row_rect[2]:.1f} H={row_rect[3]:.1f} (canvas px)")
    print(f"controls top {controls[1] + controls[3]:.1f} -> {moved_top:.1f} while the row shows "
          f"({moved_top - controls[1]:.0f}px tall; {controls[3]:.0f}px otherwise)")
    print("negative control refused:", negative_control(PREFAB).split(": ", 1)[-1])

    if args.check:
        if authored != original:
            a, b = original.splitlines(), authored.splitlines()
            for i, (x, y) in enumerate(zip(a, b)):
                if x != y:
                    print(f"first difference at line {i + 1}:\n  file:   {x}\n  script: {y}")
                    break
            fail("ArcadeGameConfigureModal.prefab differs from what this script authors - re-run it")
        print("OK: the AI difficulty row matches this script.")
        return

    if authored == original:
        print("already authored - nothing to write.")
        return
    with open(PREFAB, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(authored)
    print("wrote", os.path.relpath(PREFAB, ROOT))


if __name__ == "__main__":
    main()
