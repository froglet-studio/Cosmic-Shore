#!/usr/bin/env python3
"""
Author ITEM 8b of the style-B menu redesign - the TOY BOX window - as prefabs, and wire it into
Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 17.3; the approved ToyBox board on the "Menu Redesign B"
canvas). It is the Arcade / Arena window shape with the Toy Box's own content:

    Assets/_Prefabs/UI Elements/Main Menu Screens/Toybox/
        ToyCard.prefab      one toy: ToyboxCard on a KitTile - the accent stripe, the codex portrait, the
                            section (Pilot / World / Creation), the name and the tagline
        ToyboxView.prefab   the window body: nav dim, backdrop, header with Back (B), the shared PartyPanel
                            (item 3's prefab, instanced), and the explore scroll - today's activity and the
                            shuffle card, ALL TOYS with its count, a four-column toy grid, the empty line

    Menu_Main.unity, `ModalWindows/ToyboxScreenModal`:
        The old Explore children (Header, GameSelectScrollView, TopButtons), ArcadeLobbyList,
        CloseButton, ToyboxEmptyState, FriendListPanel and the inactive ToyCardTemplate are deleted; one
        ToyboxView instance goes under `Toybox_Panel/Explore`. ToyboxModal is KEPT and re-pointed: its grid,
        empty line, count and the two cards are the view's, its card prefab is ToyCard.prefab, and its
        freestyle toggle is wired (it used to be found at Start). The window stretches full screen, the
        panel gradient is off and the two OfflineUIGates gate the new party panel.

author_toybox_top_buttons.py stands down once this view is in the scene (it keeps the config asset the
two cards read). STANDING DOWN here too: the migration runs only while the old objects exist; the
documents this script owns are re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_toybox.py              # write
    python3 Tools/Build/author_menu_toybox.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_toybox.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_arena", os.path.join(HERE, "author_menu_arena.py"))
AR = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(AR)

P, C, A, K, T, H, M = AR.P, AR.C, AR.A, AR.K, AR.T, AR.H, AR.M
REPO = K.REPO
SCENE = P.SCENE
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Toybox"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
ref, mono, refs, calls = A.ref, A.mono, A.refs, A.calls
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, ring_node = A.image_node, A.art_node, A.ring_node
c = A.c

PREFABS = ("ToyCard", "ToyboxView")
S = dict(button=K.kit_script("KitButton"), card=C.script("ToyboxCard"), daily=C.script("DailyActivityCard"), shuffle=C.script("ToyboxShuffleCard"),
         modal=C.script("ToyboxModal"), gate=A.S["gate"], tile=A.S["tile"], kt=A.S["kt"], close=A.S["close"],
         crystal=C.script("MenuCrystalClickHandler"))
C.STUB_KIND.update({"daily": (114, S["daily"]), "shuffle": (114, S["shuffle"])})
TOY_W, TOY_H, TOY_GAP_X, TOY_GAP_Y, COLS = 341, 272, 10, 12, 4
FEAT_W, FEAT_H = 682, 202
GRID_W = COLS * TOY_W + (COLS - 1) * TOY_GAP_X
CRYSTAL_HANDLER = "1486013850"


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def frame(n, key, sliver=14):
    fill = n.child(N(f"{key}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite(f"kit_sliver{sliver}_fill"), True, raycast=True))
    bord = n.child(N(f"{key}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite(f"kit_sliver{sliver}_line1"), True))


def tile(key, primary, art=(), selected=False, sel_border="Selection"):
    body = A.tile_body(c(f"{key}/FocusRing" if key else "FocusRing", "img"),
                       c(f"{key}/Fill" if key else "Fill", "img"), c(f"{key}/Border" if key else "Border", "img"), 14,
                       primary=primary, art=art, sel_border=sel_border,
                       on_click=[lambda ctx: (ctx.c(key or "root", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])
    if not selected:
        return body
    return lambda ctx, go: body(ctx, go).replace("  isSelected: 0\n", "  isSelected: 1\n")


# --------------------------------------------------------------------------------------------
# ToyCard.prefab
# --------------------------------------------------------------------------------------------

def build_card():
    root = N("root", "ToyCard", R(size=(TOY_W, TOY_H)))
    ring_node(root, "FocusRing", 14)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    root.child(M.grid_node("Grid"))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))
    acc = root.child(N("Accent", "Accent", R((0, 1), (1, 1), (0, -1), (-24, 3), (0.5, 1))))
    acc.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0)))
    art_node(root, "Portrait", "Portrait", R((0.5, 1), None, (0, -18), (110, 110), (0.5, 1)), None, preserve=True)
    label(root.child(N("Section", "Section", R((0, 0), None, (16, 96), (300, 16), (0, 0)))), "Pilot", "Label", "NeutralLightest")
    nm = root.child(N("Name", "Name", R((0, 0), None, (16, 64), (310, 30), (0, 0))))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Toy", "H3", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H3", "TextLight", driven=True))
    tag = root.child(N("Tagline", "Tagline", R((0, 0), None, (16, 14), (310, 46), (0, 0))))
    tag.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("", "BodySmall", tok("NeutralLightest"), valign="top", wrap=True)) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest"))
    un = root.child(N("Unavailable", "Unavailable", STRETCH, active=False))
    image_node(un, "Unavailable/Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.6)
    label(un.child(N("Unavailable/Label", "Label", R(size=(200, 18)))), "Unavailable", "Label", "TextLight", align="center")
    root.add("tile", 114, tile("", [c("Name", "tmp")], art=[c("Portrait", "img")]))
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  portrait: {ref(ctx.c('Portrait', 'img'))}\n  accentFill: {ref(ctx.c('Accent', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'tmp'))}\n  taglineText: {ref(ctx.c('Tagline', 'tmp'))}\n"
             f"  sectionText: {ref(ctx.c('Section', 'tmp'))}\n  unavailableOverlay: {ref(ctx.go('Unavailable'))}\n"))
    root.add("audio", 114, A.menu_audio(2)).add("scroll", 114, A.scroll_into())
    return root


# --------------------------------------------------------------------------------------------
# ToyboxView.prefab
# --------------------------------------------------------------------------------------------

def daily_card(parent):
    k = "Daily"
    n = parent.child(N(k, "DailyActivityCard", R((0, 1), None, (0, 0), (FEAT_W, FEAT_H), (0, 1))))
    ring_node(n, f"{k}/FocusRing", 14)
    frame(n, k)
    art_node(n, f"{k}/Portrait", "Portrait", R((0, 0.5), None, (24, 0), (170, 170), (0, 0.5)), None, preserve=True)
    t = n.child(N(f"{k}/Title", "Title", R((0, 1), None, (222, -26), (420, 18), (0, 1))))
    label(t, "Today's activity", "Label", "Cta")
    act = n.child(N(f"{k}/Activity", "Activity", R((0, 1), None, (222, -46), (440, 44), (0, 1))))
    act.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Activity", "H1", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    label(n.child(N(f"{k}/Toy", "Toy", R((0, 1), None, (222, -92), (440, 22), (0, 1)))), "Toy", "Body", "NeutralLightest")
    label(n.child(N(f"{k}/EndsIn", "EndsIn", R((0, 0), None, (222, 30), (80, 18), (0, 0)))), "Ends in", "Label", "NeutralLightest")
    label(n.child(N(f"{k}/Countdown", "Countdown", R((0, 0), None, (300, 26), (160, 26), (0, 0)))), "--", "Data", "TextLight")
    chip = n.child(N(f"{k}/Reward", "Reward", R((1, 0), None, (-24, 24), (150, 38), (1, 0))))
    H.framed(chip, f"{k}/Reward", 10, "SurfaceVeryDark", "Cta")
    label(chip.child(N(f"{k}/Reward/Label", "Label", R((0, 0), (1, 1), (-10, 0), (-40, 0)))), "Try it", "Label", "Cta",
          align="center")
    image_node(chip, f"{k}/Reward/Crystal", "Crystal", R((1, 0.5), None, (-14, 0), (16, 16), (1, 0.5)), "Cta",
               K.chrome_sprite("chrome_crystal"), preserve=True)
    n.add("tile", 114, tile(k, [c(f"{k}/Activity", "tmp")], art=[c(f"{k}/Portrait", "img")], selected=True, sel_border="Cta"))
    n.add("daily", 114, lambda ctx, go: mono(go, S["daily"],
          f"  titleText: {ref(ctx.c(f'{k}/Title', 'tmp'))}\n  activityText: {ref(ctx.c(f'{k}/Activity', 'tmp'))}\n"
          f"  toyText: {ref(ctx.c(f'{k}/Toy', 'tmp'))}\n  statusText: {{fileID: 0}}\n"
          "  accentFill: {fileID: 0}\n  claimedBadge: {fileID: 0}\n"
          f"  countdownText: {ref(ctx.c(f'{k}/Countdown', 'tmp'))}\n  rewardText: {ref(ctx.c(f'{k}/Reward/Label', 'tmp'))}\n"
          f"  portrait: {ref(ctx.c(f'{k}/Portrait', 'img'))}\n"))
    n.add("audio", 114, A.menu_audio(2)).add("scroll", 114, A.scroll_into()).add("le", 114, le(mw=0, fw=1, ph=FEAT_H))


def shuffle_card(parent):
    k = "Shuffle"
    n = parent.child(N(k, "ShuffleCard", R((0, 1), None, (0, 0), (FEAT_W, FEAT_H), (0, 1))))
    ring_node(n, f"{k}/FocusRing", 14)
    frame(n, k)
    n.child(M.grid_node(f"{k}/Grid"))
    image_node(n, f"{k}/Glyph", "Glyph", R((0, 0.5), None, (84, 0), (44, 44), (0, 0.5)), "TextLight",
               K.chrome_sprite("chrome_shuffle"), preserve=True)
    label(n.child(N(f"{k}/Eyebrow", "Eyebrow", R((0, 1), None, (222, -26), (420, 18), (0, 1)))), "Shuffle", "Label",
          "NeutralLightest")
    t = n.child(N(f"{k}/Title", "Title", R((0, 1), None, (222, -46), (440, 44), (0, 1))))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Shuffle the toy box", "H1", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    label(n.child(N(f"{k}/Detail", "Detail", R((0, 1), None, (222, -92), (440, 22), (0, 1)))),
          "New world, new colours, new hull", "Body", "NeutralLightest")
    w, h = K.button_size("SHUFFLE", small=True)
    n.children.append(P.Inst("ShuffleButton", "KitButtonSmall", "ShuffleButton", R((1, 0), None, (-24, 24), (w, h), (1, 0)),
                             K.button_mods("KitButtonSmall", "Default", False, "SHUFFLE", small=True)
                             + [("root#button", "m_Navigation.m_Mode", 0, None)]))
    n.add("tile", 114, tile(k, [c(f"{k}/Title", "tmp")]))
    n.add("shuffle", 114, lambda ctx, go: mono(go, S["shuffle"],
          f"  titleText: {ref(ctx.c(f'{k}/Title', 'tmp'))}\n  detailText: {ref(ctx.c(f'{k}/Detail', 'tmp'))}\n"
          "  accentFill: {fileID: 0}\n"
          f"  shuffleButton: {ref(ctx.iref('ShuffleButton', 'root#button'))}\n"))
    n.add("audio", 114, A.menu_audio(2)).add("scroll", 114, A.scroll_into()).add("le", 114, le(mw=0, fw=1, ph=FEAT_H))


def build_view():
    root = N("root", "ToyboxView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=A.WINDOW_BACKDROP_ALPHA)
    # Rev 6: the living lattice over the glass (Lattice_Window.mat); it crystallises in as the window fades in.
    root.child(M.window_lattice_node("BackdropLattice", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))
    body = root.child(N("Body", "Body", A.WINDOW_BODY))
    body.add("hlg", 114, lambda ctx, go: K.hv_layout(False, (0, 0, 0, 0), 24, 0, (1, 1), (0, 1))(ctx, go)
             .replace("  m_ReverseArrangement: 0\n", "  m_ReverseArrangement: 1\n"))
    # Motion (section 22.8): the window's two blocks rise in on every open - the modal never
    # re-enables, so ModalWindowManager replays them (KitEntrance.ReplayUnder).
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))
    ex = body.child(N("Explore", "ExploreScroll", R((0, 1), None, (0, 0), (A.EXPLORE_W, 844), (0, 1))))
    ex.add("le", 114, le(mw=0, fw=1)).add("scroll", 114, A.scroll_rect("Explore/Viewport/Content", "Explore/Viewport"))
    vp = ex.child(N("Explore/Viewport", "Viewport", R((0, 0), (1, 1), (-5, 0), (-10, 0))))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    content = vp.child(N("Explore/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (6, 6, 6, 6), 0, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    # Sibling order is focus order (ScreenSwitcher.Refocus): today's activity comes first, so the pad
    # starts on the card the board highlights; the party panel and the header's Back follow.
    feats = content.child(N("Features", "Features", R((0, 1), None, (0, 0), (GRID_W, FEAT_H), (0, 1))))
    feats.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 20, 0, (1, 1), (1, 1))).add("le", 114, le(ph=FEAT_H))
    daily_card(feats)
    shuffle_card(feats)
    content.child(N("Gap1", "Gap", R((0, 1), None, (0, 0), (GRID_W, 26), (0, 1)))).add("le", 114, le(ph=26))
    allt = content.child(N("AllToys", "AllToys", R((0, 1), None, (0, 0), (GRID_W, 18), (0, 1))))
    allt.add("le", 114, le(ph=18))
    label(allt.child(N("AllToys/Label", "Label", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "ALL TOYS", "Label", "TextLight")
    label(allt.child(N("AllToys/Count", "Count", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "0", "Label", "NeutralLightest",
          align="right")
    content.child(N("Gap2", "Gap", R((0, 1), None, (0, 0), (GRID_W, 10), (0, 1)))).add("le", 114, le(ph=10))
    grid = content.child(N("Grid", "ToyGrid", R((0, 1), None, (0, 0), (GRID_W, TOY_H), (0, 1))))
    grid.add("grid", 114, lambda ctx, go: mono(go, AR_GRID,
             "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 0\n    m_Bottom: 0\n"
             "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
             f"  m_CellSize: {{x: {TOY_W}, y: {TOY_H}}}\n  m_Spacing: {{x: {TOY_GAP_X}, y: {TOY_GAP_Y}}}\n"
             f"  m_Constraint: 1\n  m_ConstraintCount: {COLS}\n"))
    empty = content.child(N("Empty", "Empty", R((0, 1), None, (0, 0), (GRID_W, 60), (0, 1)), active=False))
    empty.add("le", 114, le(ph=60))
    label(empty.child(N("Empty/Text", "Text", STRETCH)), "No toys are standing right now.", "Body", "NeutralLightest",
          align="center")
    # Rev 3: the party panel moved to the one friends drawer on Menu_Main.
    header = root.child(N("Header", "Header", A.WINDOW_HEADER))
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))), "TOY BOX", "Label",
          "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Explore toys", "H1",
          "TextLight")
    A.back_button(header)
    return root


AR_GRID = "8a8695521f0d02e499659fee002a26c2"   # UnityEngine.UI.GridLayoutGroup


def prefab_outputs():
    out = {}
    for name, build in (("ToyCard", build_card), ("ToyboxView", build_view)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Toybox"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

MODAL = dict(go="159236442", rt="159236443", gate="159236444", toybox="159236450")
PANEL = dict(go="1879184136", rt="1879184137", img="1879184139", gate="1879184138")
EXPLORE = dict(go="69088012", rt="69088013")
RETIRED_ROOTS = {"108378806": "Header", "1842518664": "GameSelectScrollView", "2658967910952820128": "TopButtons",
                 "1197869415": "ArcadeLobbyList", "475984033": "CloseButton", "1051717463": "ToyboxEmptyState",
                 "1204389397": "FriendListPanel", "1669693108": "ToyCardTemplate"}
TOYBOX_ORDER = ("cardGrid", "cardPrefab", "emptyState", "countText", "dailyActivityCard", "shuffleCard",
                "configureModal", "crystalClickHandler", "freestyleHandoffTimeout", "cardRevealSettings")


class Ids:
    def __init__(self):
        self.ctx = P.Ctx("Menu_Main/Toybox")
        self.inst = str(self.ctx.fid("ToyboxView#inst"))
        self.stub = str(self.ctx.fid("ToyboxView#stub"))
        V = K.FileIds("ToyboxView")
        self.refs = {
            "cardGrid": (V("Grid#rt"), 224, None),
            "emptyState": (V("Empty#go"), 1, None),
            "countText": (V("AllToys/Count#tmp"), 114, K.TMP_UGUI),
            "dailyActivityCard": (V("Daily#daily"), 114, S["daily"]),
            "shuffleCard": (V("Shuffle#shuffle"), 114, S["shuffle"]),
        }
        self.stubs = {k: str(C.derived(v[0], self.inst)) for k, v in self.refs.items()}

    def all(self):
        return {self.inst, self.stub} | set(self.stubs.values())

    def stub_doc(self, field):
        src, cls, scr = self.refs[field]
        fid, guid = self.stubs[field], asset_guid("ToyboxView")
        if cls == 1:
            return T.stub_go(fid, src, guid, self.inst)
        if cls == 224:
            return (f"--- !u!224 &{fid} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
                    f"  m_PrefabInstance: {{fileID: {self.inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
        return T.stub_mono(fid, src, guid, self.inst, scr)


def card_prefab_ref():
    return K.ref(K.FileIds("ToyCard")("root#card"), asset_guid("ToyCard"))


def toybox_doc(old, ids):
    head = old[:old.index("  cardGrid:")]
    fields, _ = H.doc_fields(old[len(head):])
    values = dict(fields)
    assert set(values) <= set(TOYBOX_ORDER), f"ToyboxModal carries fields this script does not know: {set(values) - set(TOYBOX_ORDER)}"
    for k in ("cardGrid", "emptyState", "countText", "dailyActivityCard", "shuffleCard"):
        values[k] = f" {ref(ids.stubs[k])}"
    values["cardPrefab"] = f" {card_prefab_ref()}"
    values["crystalClickHandler"] = f" {ref(CRYSTAL_HANDLER)}"
    values.setdefault("freestyleHandoffTimeout", " 8")
    values.setdefault("cardRevealSettings", " {fileID: 0}")
    return head + "".join(f"  {k}:{values[k]}\n" for k in TOYBOX_ORDER)


def transform(text_):
    sc = T.Scene(text_)
    ids = Ids()
    assert sc.field(MODAL["go"], "m_Name") == "ToyboxScreenModal", "ToyboxScreenModal moved"
    assert sc.field(PANEL["go"], "m_Name") == "Toybox_Panel", "Toybox_Panel moved"
    assert sc.field(EXPLORE["go"], "m_Name") == "Explore", "the toy box Explore moved"
    assert sc.script(MODAL["toybox"]) == S["modal"], "ToyboxModal moved"
    assert sc.script(MODAL["gate"]) == S["gate"] and sc.script(PANEL["gate"]) == S["gate"], "the toy box OfflineUIGates moved"
    assert sc.script(PANEL["img"]) == K.IMAGE, "Toybox_Panel's Image moved"
    assert sc.script(CRYSTAL_HANDLER) == S["crystal"], "the menu's MenuCrystalClickHandler moved"
    if any(sc.has(r) for r in RETIRED_ROOTS):
        clash = ids.all() & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"
    for root, name in RETIRED_ROOTS.items():
        if sc.has(root):
            assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, root)):
                if sc.has(fid):
                    sc.remove(fid)
    for fid in list(sc.order):
        if fid in ids.all():
            sc.remove(fid)
    # Rev 3 stand-down: the party panel's stub (from when ToyboxView nested it) goes with it.
    legacy_party = str(C.derived(C.derived(K.FileIds("PartyPanel")("root#go"), K.FileIds("ToyboxView")("PartyPanel#inst")), ids.inst))
    if sc.has(legacy_party):
        sc.remove(legacy_party)
    out = []
    K.emit_instance(ids.ctx, K.Instance("ToyboxView", "ToyboxView", asset_guid("ToyboxView"), "ToyboxView", STRETCH, ()),
                    EXPLORE["rt"], out)
    out += [ids.stub_doc(k) for k in ids.refs]
    anchor = EXPLORE["rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(EXPLORE["rt"], T.set_block(sc.docs[EXPLORE["rt"]], "m_Children", K.refs_list("m_Children", [ids.stub])))
    sc.replace(PANEL["rt"], T.set_block(sc.docs[PANEL["rt"]], "m_Children", K.refs_list("m_Children", [EXPLORE["rt"]])))
    mrt = T.set_block(sc.docs[MODAL["rt"]], "m_Children", K.refs_list("m_Children", [PANEL["rt"]]))
    mrt = C.field_set(mrt, "m_AnchoredPosition", "{x: 0, y: 0}")
    sc.replace(MODAL["rt"], C.field_set(mrt, "m_SizeDelta", "{x: 0, y: 0}"))
    sc.replace(PANEL["img"], C.field_set(sc.docs[PANEL["img"]], "m_Enabled", "0"))
    sc.replace(MODAL["gate"], T.set_block(sc.docs[MODAL["gate"]], "onlineOnlyObjects", "  onlineOnlyObjects: []\n"))
    sc.replace(PANEL["gate"], T.set_block(sc.docs[PANEL["gate"]], "onlineOnlyObjects",
                                          "  onlineOnlyObjects: []\n"))
    sc.replace(MODAL["toybox"], toybox_doc(sc.docs[MODAL["toybox"]], ids))
    return sc.text(), ids


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    for name in PREFABS:
        if asset_guid(name) == guid:
            return files[prefab_path(name)]
    for name in AR.PREFABS:
        if AR.asset_guid(name) == guid:
            return open(os.path.join(REPO, AR.prefab_path(name)), encoding="utf-8").read()
    return AR.prefab_source(guid, files)


def prefab_objects(guid, files, memo):
    if guid in memo:
        return memo[guid]
    text_ = prefab_source(guid, files)
    objs = A.anchors_of(text_) if text_ is not None else None
    if objs is not None:
        for m in re.finditer(r"^--- !u!1001 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.M | re.S):
            pid = int(m.group(1))
            src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", m.group(2)).group(1)
            inner = prefab_objects(src, files, memo)
            if inner is not None:
                objs |= {str(C.derived(o, pid)) for o in inner}
    memo[guid] = objs
    return objs


def check_sources(path, text_, files, memo):
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text_):
        objs = prefab_objects(g, files, memo)
        if objs is not None:
            assert src in objs, f"{path}: source object {src} is not in the prefab {g}"


TMP, IMG = K.TMP_UGUI, K.IMAGE
CARD_FIELDS = dict(portrait=IMG, accentFill=IMG, nameText=TMP, taglineText=TMP, sectionText=TMP, unavailableOverlay=1)
DAILY_FIELDS = dict(titleText=TMP, activityText=TMP, toyText=TMP, countdownText=TMP, rewardText=TMP, portrait=IMG)


def validate(files):
    M.require_entrances(files, {prefab_path("ToyboxView"): 2})
    guids = T.known_guids() | P.PACKAGE | {AR_GRID}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    memo = {}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)
        check_sources(path, text_, files, memo)
    card = files[prefab_path("ToyCard")]
    _, body = A.doc_of(card, K.FileIds("ToyCard")("root#card"))
    assert body and S["card"] in body, "ToyCard: the root carries no ToyboxCard"
    for field, want in CARD_FIELDS.items():
        C.wired(card, body, field, want, "ToyCard")
    view = files[prefab_path("ToyboxView")]
    A.check_window_inset(view, "ToyboxView")      # clear of the friends orb (rev 6)
    V = K.FileIds("ToyboxView")
    _, daily = A.doc_of(view, V("Daily#daily"))
    for field, want in DAILY_FIELDS.items():
        C.wired(view, daily, field, want, "DailyActivityCard")
    _, shuf = A.doc_of(view, V("Shuffle#shuffle"))
    for field in ("titleText", "detailText"):
        C.wired(view, shuf, field, TMP, "ToyboxShuffleCard")
    C.wired(view, shuf, "shuffleButton", S["button"], "ToyboxShuffleCard")
    assert f"guid: {A.asset_guid('PartyPanel')}" not in view, "ToyboxView still instances the PartyPanel (rev 3: the friends drawer)"
    assert view.count("guid: " + S["close"]) == 1, "ToyboxView: Back must close through ModalWindowCloseButton"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, memo)
    for root, name in RETIRED_ROOTS.items():
        assert not sc.has(root), f"the toy box still holds retired {name}"
    ids = Ids()
    doc = sc.docs[MODAL["toybox"]]
    for field in ("cardGrid", "emptyState", "countText", "dailyActivityCard", "shuffleCard"):
        assert f"  {field}: {{fileID: {ids.stubs[field]}}}" in doc, f"ToyboxModal.{field} does not point into ToyboxView"
    assert f"  cardPrefab: {card_prefab_ref()}" in doc, "ToyboxModal does not spawn ToyCard.prefab"
    assert f"  crystalClickHandler: {{fileID: {CRYSTAL_HANDLER}}}" in doc, "ToyboxModal's freestyle toggle is not wired"
    assert "onlineOnlyObjects: []" in sc.docs[PANEL["gate"]], "the toy box gate still names an object (the party panel moved to the drawer)"


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
    card, view = prefab_path("ToyCard"), prefab_path("ToyboxView")
    ids = Ids()
    cases = [
        ("the party panel back in the window", lambda f: f.__setitem__(view, f[view] + f"# guid: {A.asset_guid('PartyPanel')}\n")),
        ("the window's entrances stripped", lambda f: f.__setitem__(prefab_path("ToyboxView"), M.strip_entrances(f[prefab_path("ToyboxView")]))),
        ("the body back under the friends orb", lambda f: f.__setitem__(prefab_path("ToyboxView"), A.in_one(
            f[prefab_path("ToyboxView")], K.FileIds("ToyboxView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20"))),
        ("a toy card with no portrait", lambda f: f.__setitem__(card, re.sub(r"(  portrait: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[card]))),
        ("the daily card without its countdown", lambda f: f.__setitem__(view, re.sub(r"(  countdownText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view]))),
        ("the card prefab back on the scene template", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  cardPrefab: {card_prefab_ref()}", "  cardPrefab: {fileID: 0}"))),
        ("the shuffle card unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  shuffleCard: {{fileID: {ids.stubs['shuffleCard']}}}", "  shuffleCard: {fileID: 0}"))),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print(f"OK: {len(PREFABS)} toy box prefabs and Menu_Main validate")
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
