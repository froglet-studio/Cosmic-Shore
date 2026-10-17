#!/usr/bin/env python3
"""
Author the FRIENDS DRAWER into Menu_Main (rev 3 of the style-B menu, 2026-10-16: "move the friends tab
out of all the tabs like arcade, arena - make it independent, like a popup from the left side we can
open, minimise, maximise whenever we want"), opened by the PRESENCE ORB (rev 6, Docs/MENU_REDESIGN_B.md
section 45.4; the owner chose option A, "Orb", over the bond rail).

    Safe Area/FriendsDrawerHost     after ModalWindows, so it draws over every window; CanvasGroup (the
    |                               freestyle fade list holds it), OfflineUIGate, FriendsDrawer
    '- Drawer                       the gated object: offline it is deactivated, as the panel was
       |- Orb                       92 x 92 in the bottom-left corner, inside the windows' 100 px left
       |                            gutter: the Presence-shader field (glow, the inner ring, the orbit
       |                            with one dot per player online, the bonds), you at the centre, up to
       |                            three party members on the inner ring, "N online" under it
       |                            (KitPresenceOrb); pressing it opens the drawer
       '- Window                    glass panel that slides in from the left (460 x 860, maximised
          |                         720 x 900), the panel lattice behind it and a Space rim round it:
          |                         the Space glyph and title, a minimise (slide back) and a maximise
          '- PartyPanel             THE shared party panel (item 3's prefab, instanced - never copied),
                                    drawn as the party molecule since rev 6 (author_menu_arcade.py)

Also: MenuCrystalClickHandler.menuCanvasGroups gains the host's group, so the drawer fades with the
rest of the menu when the player flies.

STANDING DOWN: every document is minted with a deterministic fileID and re-emitted in place; a re-run is
a no-op (--check).

    python3 Tools/Build/author_menu_friends_drawer.py              # write
    python3 Tools/Build/author_menu_friends_drawer.py --check      # fail if the scene drifted
    python3 Tools/Build/author_menu_friends_drawer.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, f"{name}.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


HM = _load("author_menu_home")
AR = _load("author_menu_arcade")
LAT = _load("author_ui_lattice")
P, C, A, K, T, H, M = HM.P, HM.C, HM.A, HM.K, HM.T, HM.H, HM.M
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, label = C.TL, C.TR, C.ref, C.mono, H.label

REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
S = dict(drawer=C.script("FriendsDrawer"), gate=C.script("OfflineUIGate"), crystal=C.script("MenuCrystalClickHandler"),
         orb=C.script("KitPresenceOrb"), popup=C.script("FriendProfilePopup"))
PARTY_GUID = AR.asset_guid("PartyPanel")

# Menu_Main, measured 2026-10-16 (names asserted).
SAFE_AREA_RT = "1034510601983959698"
MODAL_WINDOWS_RT = "6818916199485298600"

NORMAL = (460, 860)
MAXIMISED = (720, 900)
INSET = 16
BTN = 28

# The presence orb (section 45.4). Its place is author_menu_arcade.ORB_* (the windows' gutter gate reads it too);
# its member seats sit on the shader's inner ring at author_ui_lattice.ORB_MEMBER_ANGLES.
ORB = AR.ORB_SIZE
YOU = 30            # your avatar's ring
MEMBER = 22         # a member's ring
COUNT_H = 18
MAX_DOTS = 8


def member_pos(i):
    a = math.radians(LAT.ORB_MEMBER_ANGLES[i])
    r = LAT.ORB_MEMBER_RADIUS * ORB / 2
    return round(r * math.cos(a), 2), round(r * math.sin(a), 2)


def ctx_new():
    return P.Ctx("Menu_Main/FriendsDrawer")


class Patch(HM.Patch):
    def __init__(self, text_):
        super().__init__(text_)
        self.ctx = ctx_new()


def seat_nodes(parent, key, size, pos=(0, 0)):
    """An avatar in an octagon: the ring KitPresenceOrb paints the pilot's domain colour, a dark gap, the icon."""
    for part, inset, colour in (("Ring", 0, tok("ElementSpace")), ("Gap", 3, tok("SurfaceVeryDark"))):
        n = parent.child(N(f"{key}/{part}", part, R(pos=pos, size=(size - inset, size - inset))))
        n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(colour, K.kit_sprite("kit_oct_fill")))
    icon = parent.child(N(f"{key}/Avatar", "Avatar", R(pos=pos, size=(size - 5, size - 5))))
    icon.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), AR.AVATAR_PLACEHOLDER, preserve=True))


def orb_tint():
    """A flat ColorTint on the orb's field, the same states as the menu's glyph controls (HM.icon_button)."""
    return (A.selectable().replace("  m_Transition: 0\n", "  m_Transition: 1\n")
            .replace("    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n", f"    m_NormalColor: {K.col(tok('NeutralLightest'))}\n")
            .replace("    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
                     f"    m_HighlightedColor: {K.col(tok('TextLight'))}\n")
            .replace("    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}\n",
                     f"    m_PressedColor: {K.col(tok('ControlActive'))}\n")
            .replace("    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
                     f"    m_SelectedColor: {K.col(tok('TextLight'))}\n"))


def orb_node(parent):
    o = parent.child(N("Drawer/Orb", "PresenceOrb", R((0, 0), None, (AR.ORB_X, AR.ORB_Y), (ORB, ORB), (0, 0))))
    o.add("cg", 225, H.canvas_group(True))
    field = o.child(N("Drawer/Orb/Field", "Field", STRETCH))
    field.add("cr", 222, K.canvas_renderer()).add("img", 114, M.material_image(K.WHITE, M.PRESENCE_MAT, raycast=True))
    for i in range(len(LAT.ORB_MEMBER_ANGLES)):
        m = o.child(N(f"Drawer/Orb/Member{i + 1}", f"Member{i + 1}", R(pos=member_pos(i), size=(MEMBER, MEMBER))))
        m.add("cg", 225, H.canvas_group(False))
        seat_nodes(m, f"Drawer/Orb/Member{i + 1}", MEMBER)
    you = o.child(N("Drawer/Orb/You", "You", R(size=(YOU, YOU))))
    seat_nodes(you, "Drawer/Orb/You", YOU)
    count = o.child(N("Drawer/Orb/Count", "Count", R((0, 0), (1, 0), (0, -4), (0, COUNT_H), (0.5, 1))))
    AR.label_e(count, "Friends", "Label", "ElementSpace", driven=True, align="center")
    o.add("button", 114, lambda ctx, go: mono(go, A.BUTTON,
          orb_tint() + f"  m_TargetGraphic: {ref(ctx.c('Drawer/Orb/Field', 'img'))}\n" + A.calls([])))
    o.add("audio", 114, A.menu_audio(11))
    o.add("orb", 114, lambda ctx, go: mono(go, S["orb"],
          "  party: {fileID: 0}\n"
          f"  orb: {ref(ctx.c('Drawer/Orb/Field', 'img'))}\n  maxDots: {MAX_DOTS}\n"
          f"  centreAvatar: {ref(ctx.c('Drawer/Orb/You/Avatar', 'img'))}\n"
          f"  centreRing: {ref(ctx.c('Drawer/Orb/You/Ring', 'img'))}\n"
          "  members:\n" + "".join(
              f"  - group: {ref(ctx.c(f'Drawer/Orb/Member{i + 1}', 'cg'))}\n"
              f"    avatar: {ref(ctx.c(f'Drawer/Orb/Member{i + 1}/Avatar', 'img'))}\n"
              f"    ring: {ref(ctx.c(f'Drawer/Orb/Member{i + 1}/Ring', 'img'))}\n"
              for i in range(len(LAT.ORB_MEMBER_ANGLES)))
          + f"  countText: {ref(ctx.c('Drawer/Orb/Count', 'kt'))}\n"
          "  onlineTemplate: '{count} online'\n  aloneText: Friends\n"
          f"  theme: {K.ref(11400000, K.THEME, 2)}\n"))
    return o


def window_node(parent):
    w = parent.child(N("Drawer/Window", "Window", R((0, 0.5), None, (-NORMAL[0] - 8, 0), NORMAL, (0, 0.5))))
    w.add("cg", 225, H.canvas_group(False))
    fill = w.child(N("Drawer/Window/Fill", "Fill", STRETCH))
    # Glass, not a slab (the user, 2026-10-16: "I liked the transparent backgrounds way more").
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.62), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    # Rev 6: the living lattice behind the panel and a Space (people) rim round it. Rev 7: the two-layer window lattice
    # (with its element energy) rather than the faint panel one - the party panel no longer covers it with a slab.
    w.children.append(M.window_lattice_node("Drawer/Window/Lattice"))
    bord = w.child(N("Drawer/Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver14_line1"), True))
    M.rim_border(bord, "Space")
    glyph = w.child(N("Drawer/Window/TitleGlyph", "TitleGlyph", TL(20, 16, 24, 24)))
    glyph.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), H.element_glyph("Space"), preserve=True))
    label(w.child(N("Drawer/Window/Title", "Title", TL(52, 18, 200, 22))), "Friends", "Label", "ElementSpace")
    # Minimise (a bar): slides the window back to the handle.
    HM.icon_button(w, "Drawer/Window/Minimise", "Minimise", None, TR(16 + BTN + 6, 14, BTN, BTN))
    # Maximise / restore: one button, two glyphs the drawer swaps (a screen to grow, a chevron to shrink).
    mx = HM.icon_button(w, "Drawer/Window/Maximise", "Maximise", "chrome_display", TR(16, 14, BTN, BTN))
    next(c for c in mx.children if c.key == "Drawer/Window/Maximise/Glyph").add("cg", 225, H.canvas_group(True))
    rs = mx.child(N("Drawer/Window/Maximise/Restore", "Restore", R(size=(14, 14))))
    rs.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.chrome_sprite("chrome_arrow_down"), preserve=True)) \
        .add("cg", 225, H.canvas_group(False))
    w.children.append(K.Instance("Drawer/Window/PartyPanel", "PartyPanel", PARTY_GUID, "PartyPanel",
                                 R((0, 0), (1, 1), (0, -28), (-40, -76)), ()))
    return w


# Rev 7 (section 47.3): an online pilot's profile card, popped beside the drawer's window level with the avatar the
# pointer rests on (the approved "Friend profile" board).
CARD_W, CARD_H = 400, 292
CARD_AVATAR = 84
PK = "Drawer/Profile/Card"


def chip(parent, key, name, word, x, filled):
    """A FRIEND / ONLINE tag: filled Space with dark text, or a Space outline with Space text."""
    w = K.label_width(word.upper(), "Label") + 20
    n = parent.child(N(key, name, TL(x, 74, w, 24)))
    sprite = K.kit_sprite("kit_sliver10_fill" if filled else "kit_sliver10_line1")
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ElementSpace"), sprite, True))
    lab = n.child(N(f"{key}/Label", "Label", STRETCH))
    label(lab, word, "Label", "SurfaceBlack" if filled else "ElementSpace", align="center")
    return n, x + w + 8


def profile_node(parent):
    pr = parent.child(N("Drawer/Profile", "ProfileCard", STRETCH))
    pr.add("cg", 225, H.canvas_group(False))
    card = pr.child(N(PK, "Card", R((0.5, 0.5), None, (0, 0), (CARD_W, CARD_H), (0, 0.5))))
    fill = card.child(N(f"{PK}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.95), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    bord = card.child(N(f"{PK}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver14_line1"), True))
    M.rim_border(bord, "Space")
    av = card.child(N(f"{PK}/Avatar", "Avatar", TL(20, 20, CARD_AVATAR + 6, CARD_AVATAR + 6)))
    seat_nodes(av, f"{PK}/Avatar", CARD_AVATAR + 6)
    label(card.child(N(f"{PK}/Name", "Name", TL(128, 28, 220, 36))), "Pilot", "H2", "TextLight")
    _, x = chip(card, f"{PK}/FriendTag", "FriendTag", "Friend", 128, True)
    next(c for c in card.children if c.key == f"{PK}/FriendTag").add("cg", 225, H.canvas_group(False))
    chip(card, f"{PK}/OnlineTag", "OnlineTag", "Online", x, False)
    HM.icon_button(card, f"{PK}/Close", "Close", "chrome_close", TR(14, 14, BTN, BTN))
    for key, y in (("RuleTop", 128), ("RuleMid", 176)):
        rule = card.child(N(f"{PK}/{key}", key, R((0, 1), (1, 1), (0, -y), (-40, 1), (0.5, 1))))
        rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark")))
    AR.label_e(card.child(N(f"{PK}/StateLabel", "StateLabel", TL(20, 138, 200, 30))), "Online", "Label",
               "NeutralLightest", driven=True)
    AR.label_e(card.child(N(f"{PK}/StateValue", "StateValue", TR(20, 138, 160, 30))), "", "Body", "ElementTime",
               driven=True, align="right")
    game = card.child(N(f"{PK}/Game", "GameRow", R((0, 1), (1, 1), (0, -182), (0, 36), (0.5, 1))))
    game.add("cg", 225, H.canvas_group(False))
    label(game.child(N(f"{PK}/Game/Key", "Key", TL(20, 4, 120, 28))), "Game", "Label", "NeutralLightest")
    AR.label_e(game.child(N(f"{PK}/Game/Value", "Value", TR(20, 4, 240, 28))), "", "Body", "TextLight",
               driven=True, align="right")
    # JOIN in Time (where you go next), INVITE in Space (people), as the board; the popup swaps JOIN for SPECTATE.
    for key, x, variant, caption in (("Join", 20, "Time", "JOIN"), ("Invite", 196, "Space", "INVITE")):
        slot = card.child(N(f"{PK}/{key}Slot", f"{key}Slot", R((0, 0), None, (x, 20), (164, 36), (0, 0))))
        slot.add("cg", 225, H.canvas_group(False))
        slot.children.append(P.kit_button(f"{PK}/{key}", f"{key}Button", variant, caption, STRETCH, opaque=False, small=True))
    pr.add("popup", 114, lambda ctx, go: mono(go, S["popup"],
           f"  group: {ref(ctx.c('Drawer/Profile', 'cg'))}\n  card: {ref(ctx.rt(PK))}\n"
           f"  edge: {ref(ctx.rt('Drawer/Window'))}\n  friendsData: {K.ref(11400000, AR.FRIENDS_DATA, 2)}\n"
           f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
           f"  avatar: {ref(ctx.c(f'{PK}/Avatar/Avatar', 'img'))}\n  ring: {ref(ctx.c(f'{PK}/Avatar/Ring', 'img'))}\n"
           f"  nameText: {ref(ctx.c(f'{PK}/Name', 'tmp'))}\n  friendTag: {ref(ctx.c(f'{PK}/FriendTag', 'cg'))}\n"
           f"  stateLabel: {ref(ctx.c(f'{PK}/StateLabel', 'kt'))}\n  stateValue: {ref(ctx.c(f'{PK}/StateValue', 'kt'))}\n"
           f"  gameRow: {ref(ctx.c(f'{PK}/Game', 'cg'))}\n  gameValue: {ref(ctx.c(f'{PK}/Game/Value', 'kt'))}\n"
           f"  joinButton: {ref(ctx.iref(f'{PK}/Join', 'root#button'))}\n"
           f"  joinLabel: {ref(ctx.iref(f'{PK}/Join', 'Label#kt'))}\n  joinGroup: {ref(ctx.c(f'{PK}/JoinSlot', 'cg'))}\n"
           f"  inviteButton: {ref(ctx.iref(f'{PK}/Invite', 'root#button'))}\n"
           f"  inviteLabel: {ref(ctx.iref(f'{PK}/Invite', 'Label#kt'))}\n  inviteGroup: {ref(ctx.c(f'{PK}/InviteSlot', 'cg'))}\n"
           f"  closeButton: {ref(ctx.c(f'{PK}/Close', 'button'))}\n"
           "  onlineWord: Online\n  inLobbyWord: In a lobby\n  inYourPartyWord: In your party\n"
           "  partyFullWord: Party full\n  inMatchWord: In a match\n  countTemplate: '{count} / {max}'\n"
           "  joinWord: Join\n  spectateWord: Spectate\n  inviteWord: Invite\n  invitedWord: Invited\n"
           "  showDelay: 0.3\n  hideDelay: 0.25\n  gap: 16\n"))
    return pr


def host_node():
    host = N("Host", "FriendsDrawerHost", STRETCH)
    host.add("cg", 225, H.canvas_group(True))
    drawer = host.child(N("Drawer", "Drawer", STRETCH))
    orb_node(drawer)
    window_node(drawer)
    profile_node(drawer)
    host.add("gate", 114, lambda ctx, go: mono(go, S["gate"],
             "  style: 0\n"
             f"  onlineOnlyObjects:\n  - {ref(ctx.go('Drawer'))}\n  onlineOnlyControls: []\n  offlineOnlyObjects: []\n"
             "  disabledAlpha: 0.4\n"))
    host.add("drawer", 114, lambda ctx, go: mono(go, S["drawer"],
             f"  handleGroup: {ref(ctx.c('Drawer/Orb', 'cg'))}\n  handleButton: {ref(ctx.c('Drawer/Orb', 'button'))}\n"
             f"  windowGroup: {ref(ctx.c('Drawer/Window', 'cg'))}\n  window: {ref(ctx.rt('Drawer/Window'))}\n"
             f"  minimiseButton: {ref(ctx.c('Drawer/Window/Minimise', 'button'))}\n"
             f"  maximiseButton: {ref(ctx.c('Drawer/Window/Maximise', 'button'))}\n"
             f"  restoreGlyph: {ref(ctx.c('Drawer/Window/Maximise/Restore', 'cg'))}\n"
             f"  maximiseGlyph: {ref(ctx.c('Drawer/Window/Maximise/Glyph', 'cg'))}\n"
             f"  normalSize: {{x: {NORMAL[0]}, y: {NORMAL[1]}}}\n  maximisedSize: {{x: {MAXIMISED[0]}, y: {MAXIMISED[1]}}}\n"
             f"  openInset: {INSET}\n  moveSeconds: 0.22\n"))
    return host


def crystal_handler(sc):
    found = [f for f in sc.order if sc.cls[f] == 114 and sc.script(f) == S["crystal"]]
    assert len(found) == 1, "Menu_Main should hold one MenuCrystalClickHandler"
    return found[0]


def subtree_docs(sc, rt):
    """Every document under RectTransform `rt`, itself included: each GameObject with its components, and for a
    prefab instance its PrefabInstance with every stripped stub that belongs to it."""
    out, todo = set(), [rt]
    while todo:
        r = todo.pop()
        if not sc.has(r):
            continue
        doc = sc.docs[r]
        if " stripped\n" in doc.split("\n", 1)[0] + "\n":
            pi = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)\}", doc).group(1)
            out.add(pi)
            out |= {f for f in sc.order if " stripped" in sc.docs[f].split("\n", 1)[0]
                    and f"m_PrefabInstance: {{fileID: {pi}}}" in sc.docs[f]}
            continue
        go = sc.go_of(r)
        out |= {r, go} | set(sc.components(go))
        todo += sc.children(r)
    return out


# Rev 7's hub window nav (author_menu_hub_nav.py) sits right after ModalWindows; the drawer draws over it, so an
# open drawer covers the left edge arrow rather than the arrow cutting through the drawer.
HUB_NAV_RT = str(P.Ctx("Menu_Main/HubWindowNav").rt("Host"))


def drawer_anchor(kids):
    """The Safe Area child the drawer follows: the hub nav when the scene has it, else ModalWindows."""
    return HUB_NAV_RT if HUB_NAV_RT in kids else MODAL_WINDOWS_RT


def transform(text_):
    p = Patch(text_)
    assert p.sc.rect_of(p.sc.go_of(SAFE_AREA_RT)) == SAFE_AREA_RT and p.sc.field(p.sc.go_of(SAFE_AREA_RT), "m_Name") == "Safe Area", "Safe Area moved"
    assert p.sc.field(p.sc.go_of(MODAL_WINDOWS_RT), "m_Name") == "ModalWindows", "ModalWindows moved"
    # The drawer is re-emitted whole: what an earlier version emitted and this one no longer does (rev 3's Handle,
    # retired for the orb) is removed rather than left orphaned under the Drawer.
    host_rt = str(p.ctx.rt("Host"))
    if p.sc.has(host_rt):
        for fid in subtree_docs(p.sc, host_rt):
            p.sc.remove(fid)
    host = host_node()
    rt = p.emit(host, SAFE_AREA_RT, MODAL_WINDOWS_RT)
    kids = [k for k in p.kids(SAFE_AREA_RT) if k != rt]
    at = kids.index(drawer_anchor(kids)) + 1
    p.children(SAFE_AREA_RT, kids[:at] + [rt] + kids[at:])
    # Flying fades the menu through MenuCrystalClickHandler.menuCanvasGroups: the drawer joins it.
    ch = crystal_handler(p.sc)
    doc = p.doc(ch)
    m = re.search(r"^  menuCanvasGroups:\n((?:  - .*\n)*)", doc, re.M)
    groups = re.findall(r"\{fileID: (-?\d+)\}", m.group(1)) if m else []
    mine = str(p.ctx.c("Host", "cg"))
    if mine not in groups:
        groups.append(mine)
    p.replace(ch, T.set_block(doc, "menuCanvasGroups", K.refs_list("menuCanvasGroups", groups)))
    return p.sc.text()


def outputs():
    return {SCENE: transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())}


def validate(files):
    sc = T.Scene(files[SCENE])
    ctx = ctx_new()
    host_rt = str(ctx.rt("Host"))
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[SAFE_AREA_RT], re.M).group(1))
    assert host_rt in kids, "the drawer is not under Safe Area"
    assert kids.index(host_rt) == kids.index(drawer_anchor(kids)) + 1, \
        "the drawer must draw right after ModalWindows and the hub nav (over every window)"
    dr = sc.docs[str(ctx.c("Host", "drawer"))]
    for f, want in (("handleButton", ctx.c("Drawer/Orb", "button")), ("window", ctx.rt("Drawer/Window")),
                    ("minimiseButton", ctx.c("Drawer/Window/Minimise", "button")), ("maximiseButton", ctx.c("Drawer/Window/Maximise", "button")),
                    ("windowGroup", ctx.c("Drawer/Window", "cg")), ("handleGroup", ctx.c("Drawer/Orb", "cg"))):
        assert f"  {f}: {{fileID: {want}}}" in dr, f"FriendsDrawer.{f} is not wired"
    validate_orb(sc, ctx)
    gate = sc.docs[str(ctx.c("Host", "gate"))]
    assert f"  - {{fileID: {ctx.go('Drawer')}}}" in gate, "the drawer is not gated offline (the party panel was)"
    pis = [f for f in sc.order if sc.cls[f] == 1001 and f"guid: {PARTY_GUID}" in sc.docs[f]]
    assert len(pis) == 1, f"Menu_Main must instance the PartyPanel exactly once (the drawer); found {len(pis)}"
    assert f"m_TransformParent: {{fileID: {ctx.rt('Drawer/Window')}}}" in sc.docs[pis[0]], "the PartyPanel is not in the drawer window"
    ch = sc.docs[crystal_handler(sc)]
    assert f"  - {{fileID: {ctx.c('Host', 'cg')}}}" in ch, "the drawer does not fade with the menu in freestyle"
    validate_profile(sc, ctx)
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) == "0" or sc.has(m.group(1)), f"dangling local fileID {m.group(1)} in {fid}"


def validate_profile(sc, ctx):
    """Rev 7 (section 47.3): the profile card is the drawer's last child (it draws over the window), rests hidden, sits
    beside the window, and every word it shows is an authored field on FriendProfilePopup."""
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[str(ctx.rt("Drawer"))], re.M).group(1))
    assert kids[-1] == str(ctx.rt("Drawer/Profile")), "the profile card must be the drawer's last child (over the window)"
    cg = sc.docs[str(ctx.c("Drawer/Profile", "cg"))]
    assert "  m_Alpha: 0\n" in cg and "  m_BlocksRaycasts: 0\n" in cg, "the profile card must rest hidden"
    pop = sc.docs[str(ctx.c("Drawer/Profile", "popup"))]
    assert S["popup"] in pop, "the profile card carries no FriendProfilePopup"
    assert f"  edge: {{fileID: {ctx.rt('Drawer/Window')}}}\n" in pop, "the card does not sit beside the drawer's window"
    assert f"  friendsData: {{fileID: 11400000, guid: {AR.FRIENDS_DATA}, type: 2}}\n" in pop, "the card cannot tell a friend"
    for f in ("group", "card", "avatar", "ring", "nameText", "friendTag", "stateLabel", "stateValue", "gameRow", "gameValue",
              "joinButton", "joinLabel", "joinGroup", "inviteButton", "inviteLabel", "inviteGroup", "closeButton"):
        m = re.search(rf"^  {f}: \{{fileID: (-?\d+)\}}", pop, re.M)
        assert m and m.group(1) != "0", f"FriendProfilePopup.{f} is not wired"
    for f in ("onlineWord", "inLobbyWord", "inYourPartyWord", "partyFullWord", "inMatchWord", "joinWord", "spectateWord",
              "inviteWord", "invitedWord"):
        assert re.search(rf"^  {f}: \S", pop, re.M), f"FriendProfilePopup.{f} is blank (the card would show nothing there)"
    assert "  countTemplate: '{count} / {max}'\n" in pop, "the party size template lost a token"
    card = sc.docs[str(ctx.c(f"{PK}/Fill", "img"))]
    assert "  m_RaycastTarget: 1\n" in card, "the card does not take the pointer (it would close as you reach for JOIN)"


def _vec(doc, field):
    m = re.search(rf"^  {field}: \{{x: (-?[\d.e-]+), y: (-?[\d.e-]+)\}}", doc, re.M)
    assert m, f"no {field}"
    return float(m.group(1)), float(m.group(2))


def validate_orb(sc, ctx):
    """Rev 6 (section 45.4): the presence orb is the drawer's handle, sits where the windows' gutter gate expects it,
    draws its field with the presence material, has its member seats on the shader's inner ring at the shader's
    angles, and the window wears the Space rim over the lattice. Rev 3's Handle is gone."""
    names = [sc.field(sc.go_of(r), "m_Name") for r in subtree_rts(sc, str(ctx.rt("Host")))]
    assert "Handle" not in names, "rev 3's Handle is still under the drawer (the orb replaced it)"
    orb_rt = sc.docs[str(ctx.rt("Drawer/Orb"))]
    assert _vec(orb_rt, "m_AnchoredPosition") == (AR.ORB_X, AR.ORB_Y) and _vec(orb_rt, "m_SizeDelta") == (ORB, ORB), \
        f"the orb moved off author_menu_arcade.ORB_* (the windows' gutter gate measures against those)"
    assert AR.ORB_X + ORB < AR.WINDOW_LEFT, "the orb no longer fits the windows' left gutter"
    field = sc.docs[str(ctx.c("Drawer/Orb/Field", "img"))]
    assert f"m_Material: {{fileID: 2100000, guid: {M.PRESENCE_MAT}, type: 2}}" in field, "the orb's field is not drawn with Presence_Orb.mat"
    assert "  m_RaycastTarget: 1\n" in field, "the orb's field is not its press area"
    orb = sc.docs[str(ctx.c("Drawer/Orb", "orb"))]
    for fld, key in (("orb", ("Drawer/Orb/Field", "img")), ("centreAvatar", ("Drawer/Orb/You/Avatar", "img")),
                     ("centreRing", ("Drawer/Orb/You/Ring", "img")), ("countText", ("Drawer/Orb/Count", "kt"))):
        assert f"  {fld}: {{fileID: {ctx.c(*key)}}}" in orb, f"KitPresenceOrb.{fld} is not wired"
    seats = re.findall(r"^  - group: \{fileID: (-?\d+)\}", orb, re.M)
    assert len(seats) == len(LAT.ORB_MEMBER_ANGLES), f"KitPresenceOrb needs {len(LAT.ORB_MEMBER_ANGLES)} member seats, has {len(seats)}"
    r = LAT.ORB_MEMBER_RADIUS * ORB / 2
    for i, angle in enumerate(LAT.ORB_MEMBER_ANGLES):
        assert seats[i] == str(ctx.c(f"Drawer/Orb/Member{i + 1}", "cg")), f"KitPresenceOrb.members[{i}] is not Member{i + 1}"
        x, y = _vec(sc.docs[str(ctx.rt(f"Drawer/Orb/Member{i + 1}"))], "m_AnchoredPosition")
        want = (r * math.cos(math.radians(angle)), r * math.sin(math.radians(angle)))
        assert math.hypot(x - want[0], y - want[1]) < 0.5, \
            f"Member{i + 1} sits at ({x}, {y}); the shader's bond {i + 1} ends at ({want[0]:.1f}, {want[1]:.1f})"
    assert "  onlineTemplate: '{count} online'\n" in orb, "the orb's count line lost its authored template"
    border = sc.docs[str(ctx.c("Drawer/Window/Border", "img"))]
    assert f"guid: {M.rim_material('Space')}" in border, "the drawer window lost its Space rim"
    assert sc.has(str(ctx.c("Drawer/Window/Border", "rim"))), "the drawer window's rim has no KitRimMesh"
    assert f"guid: {M.LATTICE_WINDOW_MAT}" in sc.docs[str(ctx.c("Drawer/Window/Lattice", "img"))], "the drawer window has no window lattice"


def subtree_rts(sc, rt):
    out, todo = [], [rt]
    while todo:
        r = todo.pop()
        if sc.has(r) and " stripped" not in sc.docs[r].split("\n", 1)[0]:
            out.append(r)
            todo += sc.children(r)
    return out


def self_test(files):
    ctx = ctx_new()

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
    def in_doc(text_, fid, old, new):
        return T.in_doc(text_, str(fid), old, new)
    member = ctx.rt("Drawer/Orb/Member2")
    x, y = member_pos(1)
    cases = [
        ("the orb unwired as the handle", lambda f: f.__setitem__(SCENE, re.sub(r"(  handleButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[SCENE], count=1))),
        ("a member seat off its bond", lambda f: f.__setitem__(SCENE, in_doc(
            f[SCENE], member, f"m_AnchoredPosition: {K.v2(x, y)}", f"m_AnchoredPosition: {K.v2(x, y + 9)}"))),
        ("the orb grown into the windows", lambda f: f.__setitem__(SCENE, in_doc(
            f[SCENE], ctx.rt("Drawer/Orb"), f"m_SizeDelta: {K.v2(ORB, ORB)}", f"m_SizeDelta: {K.v2(140, 140)}"))),
        ("the orb's field on another material", lambda f: f.__setitem__(SCENE, f[SCENE].replace(M.PRESENCE_MAT, M.LATTICE_PANEL_MAT, 1))),
        ("the window's rim gone", lambda f: f.__setitem__(SCENE, f[SCENE].replace(M.rim_material("Space"), "7" * 32, 1))),
        ("rev 3's handle left behind", lambda f: f.__setitem__(SCENE, in_doc(
            f[SCENE], ctx.go("Drawer/Orb"), "  m_Name: PresenceOrb\n", "  m_Name: Handle\n"))),
        ("the drawer ungated offline", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  onlineOnlyObjects:\n  - {{fileID: {ctx.go('Drawer')}}}\n", "  onlineOnlyObjects: []\n", 1))),
        ("the drawer left out of the freestyle fade", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  - {{fileID: {ctx.c('Host', 'cg')}}}\n", "", 1))),
        ("the profile card under the window", lambda f: f.__setitem__(SCENE, in_doc(
            f[SCENE], ctx.rt("Drawer"), f"  - {{fileID: {ctx.rt('Drawer/Window')}}}\n  - {{fileID: {ctx.rt('Drawer/Profile')}}}\n",
            f"  - {{fileID: {ctx.rt('Drawer/Profile')}}}\n  - {{fileID: {ctx.rt('Drawer/Window')}}}\n"))),
        ("the profile card's JOIN word blanked", lambda f: f.__setitem__(SCENE, in_doc(
            f[SCENE], ctx.c("Drawer/Profile", "popup"), "  joinWord: Join\n", "  joinWord: \n"))),
        ("the profile card's friend list unwired", lambda f: f.__setitem__(SCENE, in_doc(
            f[SCENE], ctx.c("Drawer/Profile", "popup"), f"guid: {AR.FRIENDS_DATA}", "guid: " + "0" * 32))),
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
    print("OK: the friends drawer validates in Menu_Main")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = [p for p, t in files.items() if open(os.path.join(REPO, p), encoding="utf-8").read() != t]
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
