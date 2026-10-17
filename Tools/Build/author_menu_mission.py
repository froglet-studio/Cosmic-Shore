#!/usr/bin/env python3
"""
Author the MISSION window of the style-B menu redesign (item 8, Docs/MENU_REDESIGN_B.md section 17.2;
the approved MissionWindow board on the "Menu Redesign B" canvas) as prefabs, and wire it into
Menu_Main.unity. It takes the Arena window's LOOK (author_menu_arena.py) and none of its data path:
missions are their own games (SO_Mission : SO_Game, never an SO_ArcadeGame), so the grid is drawn by
MissionExploreView from MissionGames (the SO_MissionList Arcade launches missions from), not by an
ArcadeExploreView.

    Assets/_Prefabs/UI Elements/Main Menu Screens/Mission/
        MissionCard.prefab   KitMissionCard on a KitTile - the art, "Mission - difficulty 1 - 9", the
                             title, "8 threats", the description; a MenuAvailabilityView set Locked
                             ("Missions open with the full release."), drawn undimmed as the board shows
        MissionView.prefab   nav dim, backdrop, header ("MISSIONS / Explore missions", Back (B)), the shared
                             PartyPanel (item 3, instanced), the explore scroll with ALL MISSIONS + count
                             and a two-column grid MissionExploreView fills at runtime

    Menu_Main.unity, `ModalWindows/MissionScreenModal`:
        The old Header, GameSelectScrollView, ArcadeLobbyList, CloseButton and FriendListPanel are deleted,
        as is the ArcadeExploreView on Mission_Panel/Explore (it never drew missions - with no roster it
        drew the arcade master list); ArcadeScreen.ExploreView is cleared (it is optional there). One
        MissionView instance goes under Explore. The window stretches full screen, the panel's gradient is
        off and the panel's OfflineUIGate gates the party panel. The hub entry stays Unavailable, and the
        mission data, Arcade.LaunchMission and the mission scenes are untouched.

STANDING DOWN: the migration runs only while the old objects exist; the documents this script owns are
re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_mission.py              # write
    python3 Tools/Build/author_menu_mission.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_mission.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile", os.path.join(HERE, "author_menu_profile.py"))
P = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(P)

C, A, K, T, H, M = P.C, P.A, P.K, P.T, P.H, P.M
REPO = K.REPO
SCENE = P.SCENE
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Mission"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
ref, mono, refs = A.ref, A.mono, A.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, ring_node = A.image_node, A.art_node, A.ring_node
c = A.c

PREFABS = ("MissionCard", "MissionView")
S = dict(card=C.script("KitMissionCard"), view=C.script("MissionExploreView"), avail=C.script("MenuAvailabilityView"),
         arcade_view=A.S["view"], screen=C.script("ArcadeScreen"), gate=A.S["gate"], tile=A.S["tile"], kt=A.S["kt"],
         cover=A.S["cover"], close=A.S["close"])
MISSION_GAMES = "7f11dbcad659c684e96f9673bc3f367e"       # MissionGames.asset (SO_MissionList)
GRID_LAYOUT = "8a8695521f0d02e499659fee002a26c2"         # UnityEngine.UI.GridLayoutGroup
LOCKED_MESSAGE = "Missions open with the full release."
CARD_W, CARD_H, GAP = 682, 372, 20
GRID_W = 2 * CARD_W + GAP
PLACEHOLDER_ART = A.sprite_guid("Assets/_SO_Assets/Games/Missions/SO_Mission_Protect.asset", "CardBackground")


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


# --------------------------------------------------------------------------------------------
# MissionCard.prefab
# --------------------------------------------------------------------------------------------

def build_card():
    root = N("root", "MissionCard", R(size=(CARD_W, CARD_H)))
    ring_node(root, "FocusRing", 14)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    clip = root.child(N("Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, A.mask(False)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], f"  image: {ref(ctx.c('Clip/Art', 'img'))}\n"))
    art_node(clip, "Clip/Art", "Art", R(size=(CARD_W, CARD_W * 510 / 768)), PLACEHOLDER_ART)
    image_node(clip, "Clip/Fade", "Fade", R((0, 0), (1, 0), (0, 0), (0, 200), (0.5, 0)), "SurfaceBlack",
               K.kit_sprite("kit_fade_v"))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))

    label(root.child(N("Eyebrow", "Eyebrow", R((0, 0), None, (26, 120), (420, 18), (0, 0)))), "Mission - difficulty 1 - 9",
          "Label", "NeutralLightest")
    title = root.child(N("Title", "Title", R((0, 0), None, (26, 70), (440, 46), (0, 0))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Mission", "H1", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    label(root.child(N("Threats", "Threats", R((1, 0), None, (-26, 80), (220, 18), (1, 0)))), "8 threats", "Label",
          "TextLight", align="right")
    tag = root.child(N("Tagline", "Tagline", R((0, 0), None, (26, 22), (620, 42), (0, 0))))
    tag.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("", "BodySmall", tok("TextLight"), valign="top", wrap=True)) \
        .add("kt", 114, K.kit_text("BodySmall", "TextLight"))

    root.add("tile", 114, A.tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 14,
                                      primary=[c("Title", "tmp")], art=[c("Clip/Art", "img")], sel_border="Cta",
                                      on_click=[lambda ctx: (ctx.c("root", "card"), "CosmicShore.UI.KitMissionCard", "OnPressed")]))
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  tile: {ref(ctx.c('root', 'tile'))}\n  art: {ref(ctx.c('Clip/Art', 'img'))}\n"
             f"  eyebrowText: {ref(ctx.c('Eyebrow', 'tmp'))}\n  titleText: {ref(ctx.c('Title', 'tmp'))}\n"
             f"  threatsText: {ref(ctx.c('Threats', 'tmp'))}\n  taglineText: {ref(ctx.c('Tagline', 'tmp'))}\n"
             f"  availability: {ref(ctx.c('root', 'avail'))}\n"))
    # Locked, and drawn as the board draws it: no dimming, no overlay - the press is the refusal.
    root.add("avail", 114, lambda ctx, go: mono(go, S["avail"],
             "  availability: 1\n  lockedOverlay: {fileID: 0}\n  unavailableOverlay: {fileID: 0}\n  label: {fileID: 0}\n"
             "  unavailableLabelColor: {r: 1, g: 1, b: 1, a: 0.4}\n  dimOwnGraphics: 0\n"
             f"  dimTint: {{r: 1, g: 1, b: 1, a: 0.35}}\n  lockedMessage: {LOCKED_MESSAGE}\n"))
    root.add("shake", 114, lambda ctx, go: mono(go, K.kit_script("KitShake"), ""))
    root.add("scroll", 114, A.scroll_into())
    return root


# --------------------------------------------------------------------------------------------
# MissionView.prefab
# --------------------------------------------------------------------------------------------

def build_view():
    root = N("root", "MissionView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=A.WINDOW_BACKDROP_ALPHA)
    # Rev 6: the living lattice over the glass (Lattice_Window.mat); it crystallises in as the window fades in.
    root.child(M.window_lattice_node("BackdropLattice", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))

    # Sibling order is FOCUS order (ScreenSwitcher.Refocus): the cards first, the party panel next, Back last.
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
    allm = content.child(N("AllMissions", "AllMissions", R((0, 1), None, (0, 0), (GRID_W, 18), (0, 1))))
    allm.add("le", 114, le(ph=18))
    label(allm.child(N("AllMissions/Label", "Label", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "ALL MISSIONS", "Label", "TextLight")
    label(allm.child(N("AllMissions/Count", "Count", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "1", "Label", "NeutralLightest",
          align="right")
    content.child(N("Gap", "Gap", R((0, 1), None, (0, 0), (GRID_W, 10), (0, 1)))).add("le", 114, le(ph=10))
    grid = content.child(N("Grid", "MissionGrid", R((0, 1), None, (0, 0), (GRID_W, CARD_H), (0, 1))))
    grid.add("grid", 114, lambda ctx, go: mono(go, GRID_LAYOUT,
             "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 0\n    m_Bottom: 0\n"
             "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
             f"  m_CellSize: {{x: {CARD_W}, y: {CARD_H}}}\n  m_Spacing: {{x: {GAP}, y: {GAP}}}\n"
             "  m_Constraint: 1\n  m_ConstraintCount: 2\n"))
    # Rev 3: the party panel moved to the one friends drawer on Menu_Main.

    header = root.child(N("Header", "Header", A.WINDOW_HEADER))
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))), "MISSIONS", "Label",
          "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Explore missions", "H1",
          "TextLight")
    A.back_button(header)

    card_ref = f"{{fileID: {K.FileIds('MissionCard')('root#card')}, guid: {asset_guid('MissionCard')}, type: 3}}"
    root.add("view", 114, lambda ctx, go: mono(go, S["view"],
             f"  missions: {K.ref(11400000, MISSION_GAMES, 2)}\n  grid: {ref(ctx.rt('Grid'))}\n"
             f"  cardPrefab: {card_ref}\n  countText: {ref(ctx.c('AllMissions/Count', 'tmp'))}\n"))
    return root


def prefab_outputs():
    out = {}
    for name, build in (("MissionCard", build_card), ("MissionView", build_view)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Mission"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

MODAL = dict(go="948404564", rt="948404565", gate="948404566", screen="948404569")
PANEL = dict(go="149581059", rt="149581060", img="149581062", gate="149581061")
EXPLORE = dict(go="484090327", rt="484090328", view="484090330")
RETIRED_ROOTS = {"1495346766": "Header", "1742401770": "GameSelectScrollView", "1994025746": "ArcadeLobbyList",
                 "1582282989": "CloseButton", "745667876": "FriendListPanel"}


class Ids:
    def __init__(self):
        self.ctx = P.Ctx("Menu_Main/Mission")
        self.inst = str(self.ctx.fid("MissionView#inst"))
        self.stub = str(self.ctx.fid("MissionView#stub"))
        V = K.FileIds("MissionView")
        self.party_src = C.derived(K.FileIds("PartyPanel")("root#go"), V("PartyPanel#inst"))
        self.party = str(C.derived(self.party_src, self.inst))

    def all(self):
        # self.party is the retired party stub's id: listed so a re-run removes it (rev 3 stand-down)
        return {self.inst, self.stub, self.party}

    def party_stub(self):
        return T.stub_go(self.party, self.party_src, asset_guid("MissionView"), self.inst)


def transform(text_):
    sc = T.Scene(text_)
    ids = Ids()
    assert sc.field(MODAL["go"], "m_Name") == "MissionScreenModal", "MissionScreenModal moved"
    assert sc.field(PANEL["go"], "m_Name") == "Mission_Panel", "Mission_Panel moved"
    assert sc.field(EXPLORE["go"], "m_Name") == "Explore", "the mission Explore moved"
    assert sc.script(MODAL["gate"]) == S["gate"] and sc.script(PANEL["gate"]) == S["gate"], "the mission OfflineUIGates moved"
    assert sc.script(MODAL["screen"]) == S["screen"], "the mission ArcadeScreen moved"
    assert sc.script(PANEL["img"]) == K.IMAGE, "Mission_Panel's Image moved"
    if any(sc.has(r) for r in RETIRED_ROOTS):
        clash = ids.all() & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"
    for root, name in RETIRED_ROOTS.items():
        if sc.has(root):
            assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, root)):
                if sc.has(fid):
                    sc.remove(fid)
    if sc.has(EXPLORE["view"]):
        assert sc.script(EXPLORE["view"]) == S["arcade_view"], "the mission Explore's ArcadeExploreView moved"
        sc.remove(EXPLORE["view"])
        go = sc.docs[EXPLORE["go"]]
        sc.replace(EXPLORE["go"], go.replace(f"  - component: {{fileID: {EXPLORE['view']}}}\n", ""))
    for fid in list(sc.order):
        if fid in ids.all():
            sc.remove(fid)
    out = []
    K.emit_instance(ids.ctx, K.Instance("MissionView", "MissionView", asset_guid("MissionView"), "MissionView", STRETCH, ()),
                    EXPLORE["rt"], out)
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
    sc.replace(MODAL["screen"], C.field_set(sc.docs[MODAL["screen"]], "ExploreView", "{fileID: 0}"))
    return sc.text(), ids


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

TMP, IMG = K.TMP_UGUI, K.IMAGE
CARD_FIELDS = dict(tile=S["tile"], art=IMG, eyebrowText=TMP, titleText=TMP, threatsText=TMP, taglineText=TMP,
                   availability=S["avail"])


def validate(files):
    M.require_entrances(files, {prefab_path("MissionView"): 2})
    guids = T.known_guids() | P.PACKAGE | {GRID_LAYOUT}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)

    card = files[prefab_path("MissionCard")]
    F = K.FileIds("MissionCard")
    _, body = A.doc_of(card, F("root#card"))
    assert body and S["card"] in body, "MissionCard: the root carries no KitMissionCard"
    for field, want in CARD_FIELDS.items():
        C.wired(card, body, field, want, "MissionCard")
    _, avail = A.doc_of(card, F("root#avail"))
    assert "  availability: 1\n" in avail, "mission cards must ship Locked"
    assert f"  lockedMessage: {LOCKED_MESSAGE}\n" in avail, "a Locked mission card needs its refusal message"
    _, tile = A.doc_of(card, F("root#tile"))
    assert "m_MethodName: OnPressed" in tile, "the card's tile must call KitMissionCard.OnPressed"
    assert "SO_ArcadeGame" not in card and A.S["view"] not in card, "a mission card must not ride the arcade data path"

    view = files[prefab_path("MissionView")]
    A.check_window_inset(view, "MissionView")      # clear of the friends orb (rev 6)
    V = K.FileIds("MissionView")
    _, vbody = A.doc_of(view, V("root#view"))
    assert vbody and S["view"] in vbody, "MissionView: the root carries no MissionExploreView"
    assert f"  missions: {{fileID: 11400000, guid: {MISSION_GAMES}, type: 2}}\n" in vbody, "MissionView must draw MissionGames"
    assert f"guid: {asset_guid('MissionCard')}" in vbody, "MissionView's cardPrefab is not MissionCard"
    C.wired(view, vbody, "grid", 224, "MissionExploreView")
    C.wired(view, vbody, "countText", TMP, "MissionExploreView")
    assert f"guid: {A.asset_guid('PartyPanel')}" not in view, "MissionView still instances the PartyPanel (rev 3: the friends drawer)"
    assert view.count("guid: " + S["close"]) == 1, "MissionView: Back must close through ModalWindowCloseButton"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for root, name in RETIRED_ROOTS.items():
        assert not sc.has(root), f"the mission window still holds retired {name}"
    assert not sc.has(EXPLORE["view"]), "the mission window still carries an ArcadeExploreView"
    assert files[SCENE].count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid('MissionView')}") == 1, \
        "Menu_Main must mount exactly one MissionView"
    ids = Ids()
    assert "onlineOnlyObjects: []" in sc.docs[PANEL["gate"]], "the mission gate still names an object (the party panel moved to the drawer)"
    assert not sc.has(ids.party), "the retired party stub survives in the mission window"
    assert "  m_Enabled: 0\n" in sc.docs[PANEL["img"]], "the mission panel's gradient is still on"


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
    card, view = prefab_path("MissionCard"), prefab_path("MissionView")
    cases = [
        ("the window's entrances stripped", lambda f: f.__setitem__(prefab_path("MissionView"), M.strip_entrances(f[prefab_path("MissionView")]))),
        ("the body back under the friends orb", lambda f: f.__setitem__(prefab_path("MissionView"), A.in_one(
            f[prefab_path("MissionView")], K.FileIds("MissionView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20"))),
        ("mission cards shipped Available", lambda f: f.__setitem__(card, f[card].replace("  availability: 1\n", "  availability: 0\n"))),
        ("a card with no threat count", lambda f: f.__setitem__(card, re.sub(r"(  threatsText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[card]))),
        ("the view drawing no missions", lambda f: f.__setitem__(view, f[view].replace(
            f"  missions: {{fileID: 11400000, guid: {MISSION_GAMES}, type: 2}}\n", "  missions: {fileID: 0}\n"))),
        ("the party panel back in the window", lambda f: f.__setitem__(view, f[view] + f"# guid: {A.asset_guid('PartyPanel')}\n")),
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
    print(f"OK: {len(PREFABS)} mission prefabs and Menu_Main validate")
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
