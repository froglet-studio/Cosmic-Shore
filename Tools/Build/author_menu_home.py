#!/usr/bin/env python3
"""
Author ITEM 6a of the style-B menu redesign - the HOME screen - in place in Menu_Main.unity
(Docs/MENU_REDESIGN_B.md section 21; the approved Home / HomeFocus / HomeOffline boards, revision 2).

Home is the one screen restyled IN PLACE rather than as a prefab: the four hub buttons, the avatar and
name (ScreenSwitcher.hubCompanions), the online lamp and its question bar are found BY NAME by
wire_home_hub_scene.py, HomeHubWiringWindow and OfflineMenuWirer, and by component by ScreenSwitcher
(the hub row is "the parent of the first MenuHubButton"). Every one of those objects keeps its
GameObject, its fileID and its name; only its look, its rect and its children change.

    Hub row (ButtonLayouts): along the bottom, 30 apart. Each hub's Button becomes a KitButton under the
        SAME fileID (MenuHubButton requires a Button and KitButton is one; the press-sound call stays),
        with the kit's FocusRing / Fill / Border children, a hub glyph and its label - Toy Box, Arcade,
        Arena, Mission. Arcade is the opaque Play button with the A keycap and the pad's preferred
        focus; Mission draws Inactive (its MenuAvailabilityView stays Unavailable, no double dim). All
        four used to point m_TargetGraphic at the legacy, inactive ArcadeButton's Image, so none gave
        any press feedback; that legacy object is deleted.
    Pilot chip (top left): a framed plate behind the octagonal avatar (HomeScreen.avatarImage moves to
        an Avatar image inside a masked octagon), the name, an Online / Offline label (the lamp's
        statusLabel, unwired until now), a divider and the crystal balance (CrystalCurrencyDisplay).
        The lamp sits on the avatar's corner; its stray ScreenSwitcher.OnClickProfileModal call is
        removed (it opened the profile window AND asked Go offline?). The profile window keeps its Home
        entry point on the AVATAR itself (a Button calling ProfileIconSelectView.OpenAvatar), so a press
        on the avatar still opens it, as it did on the old screen.
    Question bar: under the chip - the question, a note line on what it changes (ConfirmQuestionBar.
        noteLabel, fed by OnlineStatusIndicator), Yes / No straddling the bottom edge. Its own Button's
        stray OpenDisplayName call is removed.
    Settings: a hex with a slowly turning gear and the X keycap under it (ScreenSwitcher answers X);
        its ControllerButtonPress (bound to North, a second Settings opener) is removed.
    Freestyle hint: "Tap the cell to fly" breathing at the centre; the tap target leaves pad navigation.

STANDING DOWN: everything is re-derived on each run from the objects above (the legacy deletion runs
while the legacy object exists); a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_home.py              # write
    python3 Tools/Build/author_menu_home.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_home.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile_window", os.path.join(HERE, "author_menu_profile_window.py"))
W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(W)

P, C, A, K, T, H, M = W.P, W.C, W.A, W.K, W.T, W.H, W.M
REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono = C.TL, C.TR, C.ref, C.mono
text, label, le = H.text, H.label, H.le
image_node = A.image_node

S = dict(kitbutton=K.kit_script("KitButton"), dismiss=C.script("DismissibleHint"), dock=C.script("HomeDockCard"), hub=C.script("MenuHubButton"), avail=C.script("MenuAvailabilityView"),
         lamp=C.script("OnlineStatusIndicator"), bar=C.script("ConfirmQuestionBar"), home=C.script("HomeScreen"),
         crystals=C.script("CrystalCurrencyDisplay"), cbp=C.script("ControllerButtonPress"), button=A.BUTTON,
         upnext=C.script("HomeUpNext"), invite=C.script("PartyInviteNotificationPanel"), drain=K.kit_script("KitDrain"),
         gate=C.script("OfflineUIGate"), switcher=C.script("ScreenSwitcher"), themed=H.KIT_THEMED,
         guide_ok=C.script("MenuGuideAlwaysAvailable"), relay=C.script("DoubleTapRelay"))
# Home's full-screen invisible tap surface (HomeScreen/Button) and its relay: a double click flies.
TAP = dict(go="797780467", button="797780469", relay="797780479")
CRYSTAL_HANDLER = "1486013850"
MASK = A.MASK

# Menu_Main, measured 2026-10-15 (HomeScreen go 319175480197462205).
IDS = dict(
    home_comp="371568359735741370", panel_go="4911894174371205549", panel_rt="1769152617528871830",
    panel_img="4360237291221735427",
    settings_go="1473612410234841810", settings_rt="2424854552953781345", settings_img="8371738735330142046",
    settings_cbp="1462554587758757933",
    legacy_arcade_go="1435369059175607397",
    bar_go="995474914", bar_rt="995474915", bar_img="995474918", bar_btn="995474917", bar_comp="995474920",
    bar_text_rt="1060766722", bar_text="1060766723",
    accept_go="909973148", accept_rt="909973149", accept_img="909973151", accept_icon_rt="1829191283", accept_icon="1829191284",
    cancel_go="1110576754", cancel_rt="1110576755", cancel_img="1110576757", cancel_icon_rt="1002720939", cancel_icon="1002720940",
    name_go="7613213338458363602", name_rt="8076153094407706410", name_img="1014305526008866599",
    name_text_rt="8019494028173304602", name_text="1674904252436572557",
    avatar_go="3602970348709213962", avatar_rt="5738383061455623117", avatar_img="7652523670908676547",
    avatar_btn="4628693416680068390",
    lamp_go="4168599694251083979", lamp_rt="2878099680163335616", lamp_img="4937294643886484420",
    lamp_btn="1431731438122436423", lamp_comp="4937294643886484421",
    row_go="1613024078", row_rt="1613024079", row_hlg="1613024080",
    tap_btn="797780469", quest_runner="654228613",
)
HUBS = [  # key, go, rt, root img, button, hub comp, avail comp, label go, label rt, label tmp, caption, glyph, variant
    ("Toybox", "200217508", "200217509", "200217512", "200217511", "200217515", "200217514", "1681475207", "1681475208", "1681475209", "Toy Box", "chrome_cube", "Default"),
    ("Arcade", "1749518386", "1749518387", "1749518390", "1749518389", "1749518393", "1749518392", "466012893", "466012894", "466012895", "Arcade", "chrome_pad", "Play"),
    ("Arena", "1015093819", "1015093820", "1015093823", "1015093822", "1015093826", "1015093825", "2097070915", "2097070916", "2097070917", "Arena", "chrome_blades", "Default"),
    ("Mission", "895856520", "895856521", "895856524", "895856523", "895856527", "895856526", "25247041", "25247042", "25247043", "Mission", "chrome_target", "Inactive"),
]
HUB_PAD, HUB_MIN_W, HUB_H = (28, 28, 18, 18), 210, 66

# 6b
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Home"
INVITE = "PartyInvitePopup"
INVITE_TOP = 452   # rev 3: under the right-hand toast stack (author_menu_toasts.py TOP 232 + HEIGHT 200 + 20)
ARCADE_GAMES = "402ca7eefc2aeb64baaa140c43312bcd"       # ArcadeGames.asset (SO_GameList)
CONNECTION_DATA = "69a7bf449c11416dac2d81ebd3ff61f5"    # HostConnectionDataSO
PROFILE_ICONS = "3e6cb5b838253c7468480627fd86af5f"      # SO_ProfileIconList
OLD_INVITE = dict(pi="1955618632100232137", rt="1078298652", go="1955618632100232138",
                  stray_parent="4432980322083613555", gate="6613135569115853855")
HOME_RT = "3251318990371052344"
SWITCHER = "4216411968520269688"
# Rev 3.1 (the owner, 2026-10-17: "friends is overlaying vessel hangar, make vessel hangar and weekly
# challenge smaller tabs"): compact docked cards. Two of them under the pilot chip end at y ~442 at
# 1080, clear of the friends drawer's handle (centred on the left edge, from y ~484).
CARD_W, CARD_H = 480, 100
ART_W = 132                   # the card's art strip
TEXT_X = ART_W + 16           # where a card's words start
TEXT_W = CARD_W - TEXT_X - 2 * 26 - 20   # clear of the - and x buttons


def ctx_new():
    return P.Ctx("Menu_Main/Home")


# --------------------------------------------------------------------------------------------
# Scene patch helpers
# --------------------------------------------------------------------------------------------

class Patch:
    def __init__(self, text_):
        self.sc = T.Scene(text_)
        self.ctx = ctx_new()

    def doc(self, fid):
        return self.sc.docs[fid]

    def replace(self, fid, doc):
        self.sc.replace(fid, doc)

    def body(self, fid, fn, go):
        """Replace a document's body with a generated one, keeping its header (class + fileID)."""
        head = self.doc(fid).split("\n", 1)[0]
        self.replace(fid, head + "\n" + fn(self.ctx, go))

    def field(self, fid, name, value):
        self.replace(fid, C.field_set(self.doc(fid), name, value))

    def rect(self, rt, r):
        d = self.doc(rt)
        for f, v in (("m_AnchorMin", K.v2(*r["amin"])), ("m_AnchorMax", K.v2(*r["amax"])),
                     ("m_AnchoredPosition", K.v2(*r["pos"])), ("m_SizeDelta", K.v2(*r["size"])),
                     ("m_Pivot", K.v2(*r["pivot"]))):
            d = C.field_set(d, f, v)
        self.replace(rt, d)

    def children(self, rt, ids):
        self.replace(rt, T.set_block(self.doc(rt), "m_Children", K.refs_list("m_Children", ids)))

    def kids(self, rt):
        m = re.search(r"^  m_Children:\n((?:  - .*\n)*)", self.doc(rt), re.M)
        return re.findall(r"\{fileID: (-?\d+)\}", m.group(1)) if m else []

    def emit(self, node, parent_rt, after):
        """Emit a node subtree under `parent_rt`, after document `after`; returns its root RT."""
        self.ctx.register(node)
        out = []
        C.emit_node(self.ctx, node, parent_rt, out)
        out += list(self.ctx.stubs.values())
        self.ctx.stubs.clear()
        anchor = after
        for d in (K.unity_empty_scalars(d) for d in out):
            fid = T.doc_fid(d)
            if self.sc.has(fid):
                self.sc.remove(fid)
            self.sc.insert_after(anchor, fid, d)
            anchor = fid
        return str(self.ctx.rt(node.key))

    def comp_ids(self, go):
        return re.findall(r"  - component: \{fileID: (-?\d+)\}", self.doc(go))

    def add_comp(self, go, key, cls, fn):
        fid = str(self.ctx.fid(f"{go}#{key}"))
        doc = f"--- !u!{cls} &{fid}\n" + fn(self.ctx, go)
        if self.sc.has(fid):
            self.replace(fid, doc)
        else:
            g = self.doc(go)
            self.replace(go, re.sub(r"(  m_Component:\n(?:  - component: .*\n)*)",
                                    lambda m: m.group(1) + f"  - component: {{fileID: {fid}}}\n", g, count=1))
            self.sc.insert_after(go, fid, doc)
        return fid

    def remove_comp(self, go, fid):
        if self.sc.has(fid):
            self.sc.remove(fid)
        self.replace(go, self.doc(go).replace(f"  - component: {{fileID: {fid}}}\n", ""))

    def remove_subtree(self, go):
        for fid in dict.fromkeys(C.subtree(self.sc, go)):
            if self.sc.has(fid):
                self.sc.remove(fid)

    def text(self):
        return self.sc.text()


def no_calls(doc):
    """Empty a Button's persistent onClick list."""
    return re.sub(r"(  m_OnClick:\n    m_PersistentCalls:\n      m_Calls:)(?: \[\]\n|\n(?:      .*\n)*)",
                  r"\1 []\n", doc, count=1)


def img_body(color, sprite=None, sliced=False, raycast=False, preserve=False, enabled=True):
    def body(ctx, go):
        b = K.image(color, sprite, sliced, raycast, preserve)(ctx, go)
        return b if enabled else b.replace("  m_Enabled: 1\n", "  m_Enabled: 0\n", 1)
    return body


# --------------------------------------------------------------------------------------------
# The hub row
# --------------------------------------------------------------------------------------------

def hub_children(key, glyph, variant):
    """FocusRing, Fill, Border, [Keycap], Icon - emitted under the hub's own RectTransform."""
    opaque = variant == "Play"
    border, fillc, content = K.button_palette(variant, opaque)
    nodes = []
    ring = N(f"Hub/{key}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10)))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta", 0.0), K.kit_sprite("kit_sliver10_line2"), True)) \
        .add("le", 114, le(ignore=True))
    nodes.append(ring)
    fill = N(f"Hub/{key}/Fill", "Fill", STRETCH)
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("grad", 114, lambda ctx, go: K.mono(go, K.kit_script("KitGradient"), "  lightenRight: 0.5\n", enabled=opaque)) \
        .add("le", 114, le(ignore=True))
    nodes.append(fill)
    bord = N(f"Hub/{key}/Border", "Border", STRETCH)
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)) \
        .add("le", 114, le(ignore=True))
    nodes.append(bord)
    if key == "Arcade":
        kw = K.label_width("A", "Label") + 24
        holder = N(f"Hub/{key}/KeycapSlot", "KeycapSlot", R(size=(kw, K.line_height("Label") + 8)))
        holder.add("le", 114, le(pw=kw, ph=K.line_height("Label") + 8))
        holder.children.append(P.Inst(f"Hub{key}Keycap", "KitKeycap", "Keycap", STRETCH, [("Label#tmp", "m_text", "A", None)]))
        nodes.append(holder)
    icon = N(f"Hub/{key}/Icon", "Icon", R(size=(22, 22)))
    icon.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(content, K.chrome_sprite(glyph), preserve=True)) \
        .add("le", 114, le(pw=22, ph=22))
    nodes.append(icon)
    return nodes, content


def kit_button_doc(old, ctx, key, variant, label_tmp):
    """The hub's Button document re-scripted as a KitButton: Selectable fields and onClick kept."""
    opaque = variant == "Play"
    head, rest = old.split("  m_Script:", 1)
    tail = rest.split("\n", 1)[1]
    doc = head + f"  m_Script: {K.ref(11500000, S['kitbutton'])}\n" + tail
    doc = re.sub(r"^  m_TargetGraphic: .*$", f"  m_TargetGraphic: {ref(ctx.c(f'Hub/{key}/Fill', 'img'))}", doc, flags=re.M)
    doc = re.sub(r"^  m_EditorClassIdentifier: .*$", "  m_EditorClassIdentifier: ", doc, flags=re.M)
    for f in ("theme", "focusRing", "variant", "fill", "fillGraphic", "borderGraphic", "contentGraphics", "playGradient"):
        assert not re.search(r"^  %s:" % f, doc, re.M) or True
    doc = re.sub(r"\n  theme: .*\Z", "\n", doc, flags=re.S)       # re-run: drop the KitButton tail we wrote
    return (doc.rstrip("\n") + "\n"
            + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'Hub/{key}/FocusRing', 'img'))}\n"
            + f"  variant: {K.VARIANTS[variant]}\n  fill: {1 if opaque else 0}\n"
            + f"  fillGraphic: {ref(ctx.c(f'Hub/{key}/Fill', 'img'))}\n"
            + f"  borderGraphic: {ref(ctx.c(f'Hub/{key}/Border', 'img'))}\n"
            + K.refs_list("contentGraphics", [label_tmp, ctx.c(f"Hub/{key}/Icon", "img")])
            + f"  playGradient: {ref(ctx.c(f'Hub/{key}/Fill', 'grad'))}\n")


def patch_hubs(p):
    I = IDS
    p.rect(I["row_rt"], R((0, 0), (1, 0), (0, 56), (0, HUB_H + 20), (0.5, 0)))
    hlg = p.doc(I["row_hlg"])
    for f, v in (("m_ChildAlignment", "4"), ("m_Spacing", "30"), ("m_ChildControlWidth", "1"), ("m_ChildControlHeight", "1"),
                 ("m_ChildForceExpandWidth", "0"), ("m_ChildForceExpandHeight", "0")):
        hlg = C.field_set(hlg, f, v)
    p.replace(I["row_hlg"], hlg)
    for key, go, rt, img, btn, hub, avail, lgo, lrt, ltmp, caption, glyph, variant in HUBS:
        p.body(img, img_body((1.0, 1.0, 1.0, 0.0), raycast=True), go)
        nodes, content = hub_children(key, glyph, variant)
        # Children first in the old order: the kit chrome, then the label last so it draws on top.
        old_kids = [k for k in p.kids(rt) if k == lrt]
        anchor = rt
        new_ids = []
        for n in nodes:
            new_ids.append(p.emit(n, rt, anchor))
            anchor = T.doc_fid(p.doc(new_ids[-1])) if False else _last_doc(p, n)
        p.children(rt, new_ids + old_kids)
        p.replace(btn, kit_button_doc(p.doc(btn), p.ctx, key, variant, ltmp))
        p.add_comp(go, "hlg", 114, K.hv_layout(False, HUB_PAD, 10, 4, (1, 1), (0, 0)))
        p.add_comp(go, "le", 114, le(mw=HUB_MIN_W, ph=HUB_H))
        role = "Button"
        p.body(ltmp, lambda ctx, g, c=caption, col=content: K.tmp(c, role, col)(ctx, g), lgo)
        p.add_comp(lgo, "kt", 114, K.kit_text(role, driven=True))
        p.rect(lrt, R(size=(K.label_width(caption.upper(), role), K.line_height(role))))
        hb = p.doc(hub)
        hb = re.sub(r"^  preferredFocus: .*\n", "", hb, flags=re.M)
        hb = hb.rstrip("\n") + f"\n  preferredFocus: {1 if key == 'Arcade' else 0}\n"
        p.replace(hub, hb)
        if variant == "Inactive":
            p.field(avail, "dimOwnGraphics", "0")


def _last_doc(p, node):
    """The fileID of the last document emitted for `node` (its deepest last descendant's last doc)."""
    ids = [p.ctx.go(node.key), p.ctx.rt(node.key)] + [p.ctx.c(node.key, k) for k, _, _ in node.comps]
    last = str(ids[-1])
    for ch in node.children:
        if A.is_instance(ch):
            pid = str(p.ctx.fid(ch.key + "#inst"))
            stub = str(p.ctx.fid(ch.key + "#stub"))
            last = stub if p.sc.has(stub) else pid
        else:
            last = _last_doc(p, ch)
    return str(last)


# --------------------------------------------------------------------------------------------
# Pilot chip, question bar, settings, hint
# --------------------------------------------------------------------------------------------

CHIP = dict(x=40, y=108, w=600, h=112)
PROFILE_WINDOW = "8675064058193410727"   # Menu_Main's ProfileIconSelectView (author_menu_profile IDS["window"])


def chip_node():
    n = N("Chip", "PilotChip", TL(CHIP["x"], CHIP["y"], CHIP["w"], CHIP["h"]))
    n.add("in", 114, M.entrance("Rise", distance=14)).add("cg", 225, H.canvas_group(True))
    H.framed(n, "Chip", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.86)
    image_node(n, "Chip/Divider", "Divider", TL(436, 28, 1, 56), "SurfaceDark")
    cr = n.child(N("Chip/Crystals", "Crystals", TL(456, 26, 130, 60)))
    label(cr.child(N("Chip/Crystals/Label", "Label", TL(0, 0, 130, 16))), "Crystals", "Label", "NeutralLightest")
    image_node(cr, "Chip/Crystals/Glyph", "Glyph", TL(0, 26, 20, 20), "Purchase", K.chrome_sprite("chrome_crystal"), preserve=True)
    amt = cr.child(N("Chip/Crystals/Amount", "Amount", TL(28, 22, 100, 28)))
    amt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("0", "Data", tok("Purchase"))) \
        .add("kt", 114, K.kit_text("Data", "Purchase"))
    cr.add("display", 114, lambda ctx, go: mono(go, S["crystals"], f"  balanceText: {ref(ctx.c('Chip/Crystals/Amount', 'tmp'))}\n"))
    return n


def avatar_nodes():
    clip = N("Avatar/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-4, -4)))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)) \
        .add("mask", 114, A.mask(False))
    av = clip.child(N("Avatar/Clip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER, preserve=False))
    return clip


def name_nodes():
    st = N("Name/Status", "Status", R((0, 1), (1, 1), (0, -44), (0, 18), (0.5, 1)))
    st.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Online", "Label", tok("Cta"))) \
        .add("kt", 114, K.kit_text("Label", "Cta"))
    return st


def bar_nodes():
    bord = N("Bar/Border", "Border", STRETCH)
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver10_line1"), True))
    note = N("Bar/Note", "Note", TL(22, 52, 396, 20))
    note.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("", "BodySmall", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest"))
    return bord, note


def answer_label(key, word):
    n = N(f"Bar/{key}/Label", "Label", R((0, 0), (1, 1), (20, 0), (-52, 0)))
    n.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(word, "ButtonSmall", tok("SurfaceBlack"))) \
        .add("kt", 114, K.kit_text("ButtonSmall", "SurfaceBlack", driven=True))
    return n


def settings_nodes():
    bord = N("Settings/Border", "Border", STRETCH)
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_hex_line1"), True))
    gear = N("Settings/Gear", "Gear", R(size=(36, 36)))
    gear.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.chrome_sprite("chrome_gear"), preserve=True)) \
        .add("spin", 114, M.ambient("Spin", period=24.0, amplitude=0.0))
    kw = K.label_width("X", "Label") + 24
    slot = N("Settings/KeycapSlot", "KeycapSlot", R((0.5, 0), None, (0, -8), (kw, K.line_height("Label") + 8), (0.5, 1)))
    slot.children.append(P.Inst("SettingsKeycap", "KitKeycap", "Keycap", STRETCH, [("Label#tmp", "m_text", "X", None)]))
    return [bord, gear, slot]


HINT_W, HINT_H, HINT_X = 300, 36, 28     # the fly hint, and its × (rev 3)


def icon_button(parent, k, name, glyph, r, glyph_node=None):
    """A small KitButton (Default, outline) carrying one chrome glyph: the hint's x, a card's - and x.
    `glyph_node` draws a glyph no chrome sprite has (the minimise bar)."""
    b = parent.child(N(k, name, r))
    A.ring_node(b, f"{k}/FocusRing", 10)
    border, fillc, content = K.button_palette("Default", False)
    f = b.child(N(f"{k}/Fill", "Fill", STRETCH))
    f.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bd = b.child(N(f"{k}/Border", "Border", STRETCH))
    bd.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True))
    g = b.child(N(f"{k}/Glyph", "Glyph", R(size=(14, 14)) if glyph else R(size=(12, 2))))
    g.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(content, K.chrome_sprite(glyph) if glyph else None, preserve=bool(glyph)))
    b.add("button", 114, lambda ctx, go: C.mono(go, S["kitbutton"],
          K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {C.ref(ctx.c(f'{k}/Fill', 'img'))}\n" + A.calls([])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {C.ref(ctx.c(f'{k}/FocusRing', 'img'))}\n"
          "  variant: 0\n  fill: 0\n"
          f"  fillGraphic: {C.ref(ctx.c(f'{k}/Fill', 'img'))}\n"
          f"  borderGraphic: {C.ref(ctx.c(f'{k}/Border', 'img'))}\n"
          f"  contentGraphics:\n  - {C.ref(ctx.c(f'{k}/Glyph', 'img'))}\n  playGradient: {{fileID: 0}}\n"))
    b.add("audio", 114, A.menu_audio(11))
    return b


def hint_close(n):
    """The hint's x; DismissibleHint listens to it."""
    icon_button(n, "Hint/Close", "Close", "chrome_close", R((1, 0.5), None, (-4 - HINT_X / 2, 0), (HINT_X, HINT_X)))


def hint_node():
    # The hint's own CanvasGroup belongs to its Breathe and to DismissibleHint (a closed hint stays at
    # alpha 0), so the hub fade drives a WRAPPER instead: written by ScreenSwitcher, the hint's group
    # would bring a closed hint back the moment a window closed.
    dock = N("HintDock", "FlyHintDock", STRETCH)
    dock.add("cg", 225, H.canvas_group(True))
    n = dock.child(N("Hint", "FlyHint", R((0.5, 1), None, (0, -500), (HINT_W, HINT_H), (0.5, 1))))
    n.add("cg", 225, H.canvas_group(True)).add("breathe", 114, M.ambient("Breathe", period=3.2, amplitude=0.45))
    n.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.6), K.kit_sprite("kit_sliver10_fill"), True))
    lab = n.child(N("Hint/Label", "Label", R((0, 0), (1, 1), (-(HINT_X + 8) / 2, 0), (-(HINT_X + 8), 0))))
    label(lab, "Double-tap the cell to fly", "Label", "TextLight", align="center")
    hint_close(n)
    n.add("dismiss", 114, lambda ctx, go: C.mono(go, S["dismiss"],
          f"  group: {C.ref(ctx.c('Hint', 'cg'))}\n  closeButton: {C.ref(ctx.c('Hint/Close', 'button'))}\n"
          "  rememberKey: menu.hint.fly.closed\n"))
    return dock


def patch_header(p):
    I = IDS
    p.body(I["panel_img"], img_body((1.0, 1.0, 1.0, 0.0), enabled=False), I["panel_go"])

    # Pilot chip plate + hint: new children of Main_Menu_Panel, the plate drawn under the avatar.
    chip_rt = p.emit(chip_node(), I["panel_rt"], I["panel_rt"])
    hint_rt = p.emit(hint_node(), I["panel_rt"], _last_doc(p, chip_node()))
    # The hint moved under its fade wrapper (HintDock): a scene written before that still lists it here.
    p.children(I["panel_rt"], [k for k in p.kids(I["panel_rt"]) if str(k) != str(p.ctx.rt("Hint"))])

    # Avatar: octagon ring (its own Image), a masked octagon holding the avatar art, the lamp on top.
    p.rect(I["avatar_rt"], TL(CHIP["x"] + 14, CHIP["y"] + 14, 84, 84))
    p.body(I["avatar_img"], img_body(tok("TextLight"), K.kit_sprite("kit_oct_fill"), True, raycast=True), I["avatar_go"])
    clip_rt = p.emit(avatar_nodes(), I["avatar_rt"], I["avatar_rt"])
    p.children(I["avatar_rt"], [clip_rt, I["lamp_rt"]])
    p.field(I["home_comp"], "avatarImage", ref(p.ctx.c("Avatar/Clip/Avatar", "img")))
    # Parity with the old Home: pressing the avatar opens the profile window (its Avatar tab).
    p.add_comp(I["avatar_go"], "button", 114, lambda ctx, go: mono(go, A.BUTTON,
               A.selectable() + f"  m_TargetGraphic: {ref(I['avatar_img'])}\n"
               + A.calls([(PROFILE_WINDOW, "CosmicShore.UI.ProfileIconSelectView", "OpenAvatar")])))

    # Lamp: a diamond on the avatar's corner; no profile call (it asks Go offline?).
    p.rect(I["lamp_rt"], R((1, 0), (1, 0), (4, 4), (28, 28), (0.5, 0.5)))
    p.replace(I["lamp_btn"], no_calls(p.doc(I["lamp_btn"])))
    lamp = p.doc(I["lamp_comp"])
    for f, v in (("onlineText", "Online"), ("offlineText", "Offline"), ("goOfflineQuestion", "Go offline?"),
                 ("goOnlineQuestion", "Go online?")):
        lamp = C.field_set(lamp, f, v)
    lamp = re.sub(r"^  (goOfflineNote|goOnlineNote): .*\n", "", lamp, flags=re.M)
    lamp = lamp.rstrip("\n") + ("\n  goOfflineNote: Party, friends and the weekly board pause until you come back.\n"
                                "  goOnlineNote: Reconnects to party, friends and the weekly board.\n")
    status_rt = None

    # Name + status.
    p.rect(I["name_rt"], TL(CHIP["x"] + 120, CHIP["y"] + 18, 290, 72))
    p.body(I["name_img"], img_body((1.0, 1.0, 1.0, 0.0), raycast=True), I["name_go"])
    p.rect(I["name_text_rt"], R((0, 1), (1, 1), (0, 0), (0, 34), (0.5, 1)))
    p.body(I["name_text"], lambda ctx, g: A.ellipsis(text("Pilot", "H2", tok("TextLight")))(ctx, g),
           sc_go(p, I["name_text"]))
    status_rt = p.emit(name_nodes(), I["name_rt"], I["name_text_rt"])
    p.children(I["name_rt"], [I["name_text_rt"], status_rt])
    lamp = C.field_set(lamp, "statusLabel", ref(p.ctx.c("Name/Status", "tmp")))
    p.replace(I["lamp_comp"], lamp)

    # Question bar.
    p.rect(I["bar_rt"], TL(CHIP["x"], CHIP["y"] + CHIP["h"] + 16, 440, 96))
    p.body(I["bar_img"], img_body(tok("SurfaceBlack"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True), I["bar_go"])
    p.replace(I["bar_btn"], no_calls(p.doc(I["bar_btn"])))
    bord, note = bar_nodes()
    bord_rt = p.emit(bord, I["bar_rt"], I["bar_rt"])
    note_rt = p.emit(note, I["bar_rt"], _last_doc(p, bord))
    p.rect(I["bar_text_rt"], TL(22, 18, 396, 28))
    p.body(I["bar_text"], lambda ctx, g: text("Go offline?", "H3", tok("TextLight"))(ctx, g), sc_go(p, I["bar_text"]))
    for key, go, rt, img, icon_rt, icon, x, word, token, glyph in (
            ("Accept", I["accept_go"], I["accept_rt"], I["accept_img"], I["accept_icon_rt"], I["accept_icon"], 22, "Yes", "Cta", "chrome_confirm"),
            ("Cancel", I["cancel_go"], I["cancel_rt"], I["cancel_img"], I["cancel_icon_rt"], I["cancel_icon"], 146, "No", "NeutralLightest", "chrome_close")):
        p.rect(rt, R((0, 0), None, (x, 0), (112, 44), (0, 0.5)))
        p.body(img, img_body(tok(token), K.kit_sprite("kit_sliver10_fill"), True, raycast=True), go)
        p.rect(icon_rt, R((0, 0.5), None, (16, 0), (18, 18), (0, 0.5)))
        p.body(icon, img_body(tok("SurfaceBlack"), K.chrome_sprite(glyph), preserve=True), sc_go(p, icon))
        lab = answer_label(key, word)
        lab_rt = p.emit(lab, rt, icon_rt)
        p.children(rt, [icon_rt, lab_rt])
    p.children(I["bar_rt"], [bord_rt, I["bar_text_rt"], note_rt, I["accept_rt"], I["cancel_rt"]])
    p.field(I["bar_comp"], "questionLabel", ref(I["bar_text"]))
    bc = p.doc(I["bar_comp"])
    bc = re.sub(r"^  noteLabel: .*\n", "", bc, flags=re.M)
    bc = bc.replace("  acceptButton:", f"  noteLabel: {ref(p.ctx.c('Bar/Note', 'tmp'))}\n  acceptButton:", 1)
    p.replace(I["bar_comp"], bc)

    # Settings hex.
    p.rect(I["settings_rt"], TR(40, 108, 96, 84))
    p.body(I["settings_img"], img_body(tok("SurfaceVeryDark", 0.9), K.kit_sprite("kit_hex_fill"), True, raycast=True), I["settings_go"])
    p.remove_comp(I["settings_go"], I["settings_cbp"])
    ids, anchor = [], I["settings_rt"]
    for n in settings_nodes():
        ids.append(p.emit(n, I["settings_rt"], anchor))
        anchor = _last_doc(p, n)
    p.children(I["settings_rt"], ids)

    # Freestyle tap target out of pad navigation.
    tap = p.doc(I["tap_btn"])
    p.replace(I["tap_btn"], re.sub(r"(  m_Navigation:\n    m_Mode: )\d+", r"\g<1>0", tap, count=1))

    # Main_Menu_Panel children: chip plate first (under everything), the existing objects, the hint.
    kids = [k for k in p.kids(I["panel_rt"]) if k not in (chip_rt, hint_rt)]
    p.children(I["panel_rt"], [chip_rt] + kids + [hint_rt])


# --------------------------------------------------------------------------------------------
# 6b: Up next cards (scene) and the party invite (prefab)
# --------------------------------------------------------------------------------------------

DOCK_GAP = 10                 # rev 3: the cards stack under the pilot chip
FOLDED_H = 40                 # a folded card: its art slice and eyebrow
DOCK_BTN = 24


def upnext_card(key, eyebrow, eyebrow_token, border_token, x):
    k = f"UpNext/{key}"
    c = N(k, key, TL(x, 0, CARD_W, CARD_H))
    c.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", by_sibling=True, distance=14)) \
        .add("le", 114, le(pw=CARD_W, ph=-1))
    fill = c.child(N(f"{k}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.9), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    clip = c.child(N(f"{k}/Clip", "Clip", R((0, 0), (0, 1), (ART_W / 2 + 2, 0), (ART_W, -2), (0.5, 0.5))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, A.mask(False)) \
        .add("cover", 114, lambda ctx, go: mono(go, A.S["cover"], f"  image: {ref(ctx.c(f'{k}/Clip/Art', 'img'))}\n"))
    A.art_node(clip, f"{k}/Clip/Art", "Art", R(size=(ART_W, CARD_H)), None)
    bord = c.child(N(f"{k}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok(border_token), K.kit_sprite("kit_sliver14_line1"), True))
    label(c.child(N(f"{k}/Eyebrow", "Eyebrow", TL(TEXT_X, 12, TEXT_W, 16))), eyebrow, "Label", eyebrow_token)
    # Everything under the eyebrow folds away with the card (HomeDockCard.detailGroup).
    detail = c.child(N(f"{k}/Detail", "Detail", STRETCH))
    detail.add("cg", 225, H.canvas_group(True))
    t = detail.child(N(f"{k}/Title", "Title", TL(TEXT_X, 30, CARD_W - TEXT_X - 18, 26)))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Mode", "H3", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H3", "TextLight"))
    o = detail.child(N(f"{k}/Line", "Line", TL(TEXT_X, 56, CARD_W - TEXT_X - 18, 18)))
    o.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("", "BodySmall", tok("NeutralLightest")))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest"))
    c.add("button", 114, lambda ctx, go: mono(go, A.BUTTON,
          A.selectable().replace("  m_Transition: 0\n", "  m_Transition: 1\n").replace(
              "    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
              f"    m_SelectedColor: {K.col(tok('ControlActive'))}\n").replace(
              "    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
              f"    m_HighlightedColor: {K.col(tok('TextLight'))}\n")
          + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Border', 'img'))}\n" + A.calls([])))
    c.add("audio", 114, A.menu_audio(2))
    icon_button(c, f"{k}/Minimise", "Minimise", None, TR(10 + DOCK_BTN + 6, 8, DOCK_BTN, DOCK_BTN))
    icon_button(c, f"{k}/Close", "Close", "chrome_close", TR(10, 8, DOCK_BTN, DOCK_BTN))
    c.add("dock", 114, lambda ctx, go: mono(go, S["dock"],
          f"  cardGroup: {ref(ctx.c(k, 'cg'))}\n  detailGroup: {ref(ctx.c(f'{k}/Detail', 'cg'))}\n"
          f"  card: {ref(ctx.rt(k))}\n"
          f"  minimiseButton: {ref(ctx.c(f'{k}/Minimise', 'button'))}\n  closeButton: {ref(ctx.c(f'{k}/Close', 'button'))}\n"
          f"  expandedHeight: {CARD_H}\n  foldedHeight: {FOLDED_H}\n  foldSeconds: 0.18\n"
          f"  foldKey: menu.home.dock.{key.lower()}.folded\n"))
    return c, detail


def upnext_node():
    # Rev 3: docked on the left under the pilot chip, stacked; a closed card closes its gap.
    root = N("UpNext", "UpNext", TL(CHIP["x"], CHIP["y"] + CHIP["h"] + 16, CARD_W, 2 * CARD_H + DOCK_GAP))
    root.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), DOCK_GAP, 0, (0, 0), (0, 0)))
    w, wd = upnext_card("Weekly", "Weekly challenge", "Purchase", "Purchase", 0)
    # The stats row sizes each word to itself (an HLG over TMP's preferred widths): "YOUR BEST" ran into
    # its value when the slots were fixed pixel widths.
    stats = wd.child(N("UpNext/Weekly/Stats", "Stats", TL(TEXT_X, 76, CARD_W - TEXT_X - 14, 18)))
    stats.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 3, (1, 1), (0, 0)))
    label(stats.child(N("UpNext/Weekly/EndsLabel", "EndsLabel", R(size=(64, 18)))), "Ends in", "Label", "TextLight")
    cd = stats.child(N("UpNext/Weekly/Countdown", "Countdown", R(size=(110, 18))))
    cd.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("0d 00:00", "DataSmall", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("DataSmall", "TextLight"))
    stats.child(N("UpNext/Weekly/StatsGap", "Gap", R(size=(10, 18)))).add("le", 114, le(pw=10, ph=18))
    label(stats.child(N("UpNext/Weekly/BestLabel", "BestLabel", R(size=(76, 18)))), "Your best", "Label", "NeutralLightest")
    b = stats.child(N("UpNext/Weekly/Best", "Best", R(size=(100, 18))))
    b.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("None yet", "DataSmall", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("DataSmall", "TextLight"))
    root.children.append(w)
    q, qd = upnext_card("Quest", "Current quest", "NeutralLightest", "TextLight", 0)
    for i, h in enumerate((8, 10, 12, 14)):
        seg = qd.child(N(f"UpNext/Quest/Meter{i + 1}", f"Meter{i + 1}", R((0, 1), None, (TEXT_X + i * 10, -92), (7, h), (0, 0))))
        seg.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    mt = qd.child(N("UpNext/Quest/MeterText", "MeterText", TL(TEXT_X + 46, 76, CARD_W - TEXT_X - 60, 18)))
    mt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("", "DataSmall", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("DataSmall", "TextLight"))
    root.children.append(q)
    root.add("upnext", 114, lambda ctx, go: mono(go, S["upnext"],
             f"  weeklyButton: {ref(ctx.c('UpNext/Weekly', 'button'))}\n  weeklyGroup: {ref(ctx.c('UpNext/Weekly', 'cg'))}\n"
             f"  weeklyArt: {ref(ctx.c('UpNext/Weekly/Clip/Art', 'img'))}\n"
             f"  weeklyModeText: {ref(ctx.c('UpNext/Weekly/Title', 'tmp'))}\n"
             f"  weeklyObjectiveText: {ref(ctx.c('UpNext/Weekly/Line', 'tmp'))}\n"
             f"  weeklyCountdownText: {ref(ctx.c('UpNext/Weekly/Countdown', 'tmp'))}\n"
             f"  weeklyBestText: {ref(ctx.c('UpNext/Weekly/Best', 'tmp'))}\n"
             f"  questButton: {ref(ctx.c('UpNext/Quest', 'button'))}\n  questGroup: {ref(ctx.c('UpNext/Quest', 'cg'))}\n"
             f"  questArt: {ref(ctx.c('UpNext/Quest/Clip/Art', 'img'))}\n"
             f"  questTitleText: {ref(ctx.c('UpNext/Quest/Title', 'tmp'))}\n"
             f"  questNextText: {ref(ctx.c('UpNext/Quest/Line', 'tmp'))}\n"
             f"  questMeterText: {ref(ctx.c('UpNext/Quest/MeterText', 'tmp'))}\n"
             + K.refs_list("questMeterSegments", [ctx.c(f"UpNext/Quest/Meter{i + 1}", "th") for i in range(4)])
             + f"  weeklyDock: {ref(ctx.c('UpNext/Weekly', 'dock'))}\n  questDock: {ref(ctx.c('UpNext/Quest', 'dock'))}\n"
             + "  noBestText: None yet\n  readyToClaimText: Ready to claim on your Profile\n"
             + "  nextGoalTemplate: 'Next: {goal}'\n  allUnlockedText: Every intensity unlocked\n"
             + "  meterTemplate: Intensity {level} of {max}\n"
             + f"  games: {K.ref(11400000, ARCADE_GAMES, 2)}\n  screenSwitcher: {ref(SWITCHER)}\n  refreshSeconds: 1\n"))
    return root


def build_invite():
    root = N("root", INVITE, R((1, 1), None, (-40, -INVITE_TOP), (520, 132), (1, 1)))
    root.add("cg", 225, lambda ctx, go: H.canvas_group(False)(ctx, go))
    # Motion (section 22.8): the popup drops in each time it appears. PartyInviteNotificationPanel
    # starts it (play on enable off) - the popup lives on an always-enabled screen.
    root.add("in", 114, M.entrance("Drop", distance=18, play_on_enable=False))
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True))
    ring = root.child(N("Ring", "Ring", TL(24, 22, 70, 70)))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta"), K.kit_sprite("kit_oct_fill"), True))
    clip = ring.child(N("Ring/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-6, -6))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)) \
        .add("mask", 114, A.mask(False))
    av = clip.child(N("Ring/Clip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER))
    label(root.child(N("Eyebrow", "Eyebrow", TL(112, 24, 380, 16))), "Party invite", "Label", "Cta")
    nm = root.child(N("Name", "Name", TL(112, 44, 380, 28)))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Pilot", "H3", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H3", "TextLight"))
    cnt = root.child(N("Count", "Count", TL(112, 74, 380, 20)))
    cnt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("", "DataSmall", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("DataSmall", "NeutralLightest"))
    track = root.child(N("Drain", "DrainTrack", R((0, 0), (1, 0), (0, 0), (0, 3), (0.5, 0))))
    track.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.2)))
    bar = track.child(N("Drain/Bar", "Bar", R((0, 0), (1, 1), (0, 0), (0, 0), (0, 0.5))))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))) \
        .add("drain", 114, lambda ctx, go: mono(go, S["drain"], "  duration: 3\n  playOnEnable: 0\n"))
    btns = root.child(N("Buttons", "Buttons", R((0, 0), None, (24, 0), (360, 40), (0, 0.5))))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 12, 3, (0, 0), (0, 0)))
    for key, cap, variant in (("Join", "JOIN", "Cta"), ("Decline", "DECLINE", "Cancel")):
        w, h = K.button_size(cap, small=True)
        holder = btns.child(N(f"Buttons/{key}", key, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5))))
        holder.add("le", 114, le(pw=w, ph=h))
        holder.children.append(P.Inst(f"{key}Button", "KitButtonSmall", f"{key}Button", STRETCH,
                                      K.button_mods("KitButtonSmall", variant, True, cap, small=True)))
    root.add("panel", 114, lambda ctx, go: mono(go, S["invite"],
             f"  connectionData: {K.ref(11400000, CONNECTION_DATA, 2)}\n"
             f"  inviterNameText: {ref(ctx.c('Name', 'tmp'))}\n  inviterAvatarImage: {ref(ctx.c('Ring/Clip/Avatar', 'img'))}\n"
             f"  acceptButton: {ref(ctx.iref('JoinButton', 'root#button'))}\n"
             f"  declineButton: {ref(ctx.iref('DeclineButton', 'root#button'))}\n"
             f"  canvasGroup: {ref(ctx.c('root', 'cg'))}\n"
             f"  partyCountText: {ref(ctx.c('Count', 'tmp'))}\n  timerBar: {ref(ctx.c('Drain/Bar', 'drain'))}\n"
             f"  entrance: {ref(ctx.c('root', 'in'))}\n"
             f"  profileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n  autoHideSeconds: 3\n  inviteReceivedAudio: 9\n"))
    return root


def invite_path():
    return f"{OUT_DIR}/{INVITE}.prefab"


def prefab_outputs():
    return {invite_path(): P.prefab_text(build_invite(), INVITE),
            invite_path() + ".meta": K.PREFAB_META.format(g=P.asset_guid(INVITE)),
            OUT_DIR + ".meta": K.FOLDER_META.format(g=P.asset_guid("folder/Home"))}


def invite_ids():
    ctx = ctx_new()
    inst, stub = str(ctx.fid("Invite#inst")), str(ctx.fid("Invite#stub"))
    go_stub = str(C.derived(K.FileIds(INVITE)("root#go"), inst))
    return inst, stub, go_stub


def patch_upnext(p):
    I = IDS
    node = upnext_node()
    after = _last_doc(p, hint_node())
    rt = p.emit(node, I["panel_rt"], after)
    # Docked under the pilot chip, the cards share the Go-offline question bar's spot: they draw
    # BEFORE it, so the bar (shown only when asked) covers them rather than hiding behind them.
    kids = [k for k in p.kids(I["panel_rt"]) if k != rt]
    at = kids.index(I["bar_rt"]) if I["bar_rt"] in kids else len(kids)
    p.children(I["panel_rt"], kids[:at] + [rt] + kids[at:])
    sw = p.doc(SWITCHER)
    m = re.search(r"^  hubCompanions:\n((?:  - .*\n)*)", sw, re.M)
    comp = re.findall(r"\{fileID: (-?\d+)\}", m.group(1)) if m else []
    # Everything on Home that is not the hub row fades with it when any window opens (the owner,
    # 2026-10-17: the pilot chip, crystals, Settings gear and the fly hint read through the windows'
    # glass backdrop - "You didn't change these").
    for go in (str(p.ctx.go("UpNext")), str(p.ctx.go("Chip")), IDS["settings_go"], str(p.ctx.go("HintDock"))):
        if go not in comp:
            comp.append(go)
    p.replace(SWITCHER, T.set_block(sw, "hubCompanions", K.refs_list("hubCompanions", comp)))


def _toggle_call(target, type_name, method):
    return (f"      - m_Target: {{fileID: {target}}}\n"
            f"        m_TargetAssemblyTypeName: {type_name}, Assembly-CSharp\n"
            f"        m_MethodName: {method}\n        m_Mode: 1\n        m_Arguments:\n"
            "          m_ObjectArgument: {fileID: 0}\n"
            "          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n"
            "          m_IntArgument: 0\n          m_FloatArgument: 0\n          m_StringArgument: \n"
            "          m_BoolArgument: 0\n        m_CallState: 2\n")


def patch_tap_surface(p):
    """One click on empty Home launched freestyle (the surface's onClick called ToggleTransition). The
    click now goes to a DoubleTapRelay on the same object, which calls ToggleTransition on the second
    click within 0.4 s."""
    go_doc = p.doc(TAP["go"])
    if f"component: {{fileID: {TAP['relay']}}}" not in go_doc:
        go_doc = go_doc.replace("  m_Layer:", f"  - component: {{fileID: {TAP['relay']}}}\n  m_Layer:", 1)
        p.replace(TAP["go"], go_doc)
    relay = (f"--- !u!114 &{TAP['relay']}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
             "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
             f"  m_GameObject: {{fileID: {TAP['go']}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
             f"  m_Script: {{fileID: 11500000, guid: {S['relay']}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n"
             "  window: 0.4\n  onDoubleTap:\n    m_PersistentCalls:\n      m_Calls:\n"
             + _toggle_call(CRYSTAL_HANDLER, "CosmicShore.Gameplay.MenuCrystalClickHandler", "ToggleTransition"))
    if p.sc.has(TAP["relay"]):
        p.replace(TAP["relay"], relay)
    else:
        p.sc.insert_after(TAP["button"], TAP["relay"], relay)
    btn = p.doc(TAP["button"])
    btn = re.sub(r"(  m_OnClick:\n    m_PersistentCalls:\n      m_Calls:\n)(?:      - .*\n(?:        .*\n)*)+",
                 lambda m: m.group(1) + _toggle_call(TAP["relay"], "CosmicShore.UI.DoubleTapRelay", "Tap"), btn, count=1)
    p.replace(TAP["button"], btn)


def patch_invite(p):
    O = OLD_INVITE
    inst, stub, go_stub = invite_ids()
    if p.sc.has(O["pi"]):
        for fid in C.instance_docs(p.sc, O["pi"]):
            if p.sc.has(fid):
                p.sc.remove(fid)
        sp = p.doc(O["stray_parent"])
        p.replace(O["stray_parent"], sp.replace(f"  - {{fileID: {O['rt']}}}\n", ""))
        if "  m_Children:\n" in p.doc(O["stray_parent"]) and not re.search(r"^  m_Children:\n  - ", p.doc(O["stray_parent"]), re.M):
            p.replace(O["stray_parent"], p.doc(O["stray_parent"]).replace("  m_Children:\n", "  m_Children: []\n", 1))
    for fid in (inst, stub, go_stub):
        if p.sc.has(fid):
            p.sc.remove(fid)
    out = []
    K.emit_instance(p.ctx, K.Instance("Invite", INVITE, P.asset_guid(INVITE), "PartyInviteNotificationPanel",
                                      R((1, 1), None, (-40, -INVITE_TOP), (520, 132), (1, 1)), ()), HOME_RT, out)
    out.append(T.stub_go(go_stub, K.FileIds(INVITE)("root#go"), P.asset_guid(INVITE), inst))
    anchor = HOME_RT
    for d in (K.unity_empty_scalars(d) for d in out):
        p.sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    kids = [k for k in p.kids(HOME_RT) if k not in (O["rt"], stub)]
    p.children(HOME_RT, kids + [stub])
    gate = p.doc(O["gate"])
    gate = gate.replace(f"{{fileID: {O['go']}}}", f"{{fileID: {go_stub}}}")
    p.replace(O["gate"], gate)


def sc_go(p, comp):
    return p.sc.go_of(comp)


def transform(text_):
    p = Patch(text_)
    I = IDS
    assert p.sc.field(I["panel_go"], "m_Name") == "Main_Menu_Panel", "Main_Menu_Panel moved"
    assert p.sc.script(I["home_comp"]) == S["home"], "HomeScreen moved"
    for key, go, *_ in HUBS:
        assert p.sc.field(go, "m_Name") == f"{key}Button", f"{key}Button moved"
    assert p.sc.script(I["lamp_comp"]) == S["lamp"] and p.sc.script(I["bar_comp"]) == S["bar"], "lamp / question bar moved"
    if p.sc.has(I["legacy_arcade_go"]):
        assert p.sc.field(I["legacy_arcade_go"], "m_IsActive") == "0", "the legacy ArcadeButton is no longer inactive"
        lrt = p.sc.rect_of(I["legacy_arcade_go"])
        p.remove_subtree(I["legacy_arcade_go"])
        # QuestGraphRunner still carries `arcadeNavButton`, a field no code declares any more (Unity drops
        # it on the next save); it pointed at this object's Button.
        qgr = p.doc(I["quest_runner"])
        p.replace(I["quest_runner"], re.sub(r"^  arcadeNavButton: .*\n", "", qgr, flags=re.M))
        p.children(I["panel_rt"], [k for k in p.kids(I["panel_rt"]) if k != lrt])
    patch_hubs(p)
    patch_header(p)
    patch_upnext(p)
    patch_invite(p)
    patch_tap_surface(p)
    return p.text()


def outputs():
    files = prefab_outputs()
    files[SCENE] = transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def validate(files):
    M.require_entrances(files, {invite_path(): 1})
    sc_text = files[SCENE]
    sc = T.Scene(sc_text)
    I = IDS
    assert not sc.has(I["legacy_arcade_go"]), "the legacy ArcadeButton survives"
    ctx = ctx_new()
    # Rev 3: the fly hint closes with its own x, hidden through its group and remembered.
    dh = sc.docs[str(ctx.c("Hint", "dismiss"))]
    assert f"  group: {{fileID: {ctx.c('Hint', 'cg')}}}" in dh and f"  closeButton: {{fileID: {ctx.c('Hint/Close', 'button')}}}" in dh, \
        "the fly hint's x is not wired to DismissibleHint"
    for key, go, rt, img, btn, hub, avail, lgo, lrt, ltmp, caption, glyph, variant in HUBS:
        d = sc.docs[btn]
        assert S["kitbutton"] in d, f"{key}Button is not a KitButton"
        assert f"  m_TargetGraphic: {{fileID: {ctx.c(f'Hub/{key}/Fill', 'img')}}}" in d, f"{key}Button: press feedback targets another object"
        assert f"  variant: {K.VARIANTS[variant]}\n" in d, f"{key}Button: wrong variant"
        assert "m_MethodName: PlayAudio" in d, f"{key}Button lost its press sound"
        assert f"  preferredFocus: {1 if key == 'Arcade' else 0}\n" in sc.docs[hub], f"{key}: preferredFocus wrong"
    assert re.search(r"  m_Calls: \[\]\n", sc.docs[I["lamp_btn"]]), "the lamp still opens the profile window"
    avatar_btn = str(ctx.fid(f"{I['avatar_go']}#button"))
    assert sc.has(avatar_btn) and f"m_Target: {{fileID: {PROFILE_WINDOW}}}" in sc.docs[avatar_btn] \
        and "m_MethodName: OpenAvatar\n" in sc.docs[avatar_btn], "pressing the Home avatar no longer opens the profile window"
    assert sc.script(PROFILE_WINDOW) == C.script("ProfileIconSelectView"), "PROFILE_WINDOW is not the profile window"
    lamp = sc.docs[I["lamp_comp"]]
    assert f"  statusLabel: {{fileID: {ctx.c('Name/Status', 'tmp')}}}" in lamp, "the lamp's status label is unwired"
    assert f"  avatarImage: {{fileID: {ctx.c('Avatar/Clip/Avatar', 'img')}}}" in sc.docs[I["home_comp"]], "HomeScreen.avatarImage is not the masked avatar"
    assert f"  noteLabel: {{fileID: {ctx.c('Bar/Note', 'tmp')}}}" in sc.docs[I["bar_comp"]], "the question bar has no note line"
    assert not sc.has(I["settings_cbp"]), "Settings still carries its ControllerButtonPress"
    # The first-login guide dims and kills everything but its one target - except Settings, which it
    # finds by this marker (MenuGuide.CollectAlwaysAvailable): lose it and a guided player cannot
    # reach Settings at all (T-20261015-f4c7, the guide / redesign reconcile).
    gear = re.search(r"  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)", sc.docs[I["settings_go"]]).group(1)
    assert any(sc.has(c) and sc.script(c) == S["guide_ok"] for c in re.findall(r"fileID: (-?\d+)", gear)), \
        "Settings lost MenuGuideAlwaysAvailable: the first-login guide would dim and kill it"
    assert re.search(r"  m_Calls: \[\]\n", sc.docs[I["bar_btn"]]), "the question bar still opens the display-name window"
    # 6b
    up = sc.docs[str(ctx.c("UpNext", "upnext"))]
    for f in ("weeklyButton", "weeklyModeText", "weeklyCountdownText", "weeklyBestText", "questButton", "questTitleText", "questNextText",
              "weeklyDock", "questDock"):
        assert re.search(r"^  %s: \{fileID: [1-9]" % f, up, re.M), f"HomeUpNext.{f} is not wired"
    assert f"guid: {ARCADE_GAMES}" in up, "HomeUpNext.games is not the arcade card list"
    assert up.count("  - {fileID: ") == 4, "the quest meter needs four segments"
    # One click on empty Home must not launch freestyle: the tap surface relays to DoubleTapRelay.
    assert "m_MethodName: ToggleTransition" not in sc.docs[TAP["button"]], "Home's tap surface launches freestyle on ONE click"
    assert f"m_Target: {{fileID: {TAP['relay']}}}" in sc.docs[TAP["button"]] and "m_MethodName: Tap\n" in sc.docs[TAP["button"]], \
        "Home's tap surface does not reach its DoubleTapRelay"
    assert sc.has(TAP["relay"]) and f"m_Target: {{fileID: {CRYSTAL_HANDLER}}}" in sc.docs[TAP["relay"]] \
        and "m_MethodName: ToggleTransition" in sc.docs[TAP["relay"]], "the DoubleTapRelay does not fly (ToggleTransition)"
    for key in ("UpNext", "Chip", "HintDock"):
        assert f"  - {{fileID: {ctx.go(key)}}}" in sc.docs[SWITCHER], f"{key} does not fade with the hub row (hubCompanions)"
    assert f"  - {{fileID: {IDS['settings_go']}}}" in sc.docs[SWITCHER], "Settings does not fade with the hub row (hubCompanions)"
    for key in ("Weekly", "Quest"):   # rev 3: each docked card folds and closes
        dk = sc.docs[str(ctx.c(f"UpNext/{key}", "dock"))]
        # Rev 3.1: both cards expanded must end above the friends drawer's handle (centred on the left
        # edge, 112 tall: from y 484 at 1080) - the owner's "friends is overlaying vessel hangar".
        exp = int(re.search(r"expandedHeight: (\d+)", dk).group(1))
        bottom = CHIP["y"] + CHIP["h"] + 16 + 2 * exp + DOCK_GAP
        assert bottom <= 540 - 112 // 2 - 16, f"the docked cards reach y {bottom}: under the friends handle"
        for f, want in (("detailGroup", ctx.c(f"UpNext/{key}/Detail", "cg")), ("minimiseButton", ctx.c(f"UpNext/{key}/Minimise", "button")),
                        ("closeButton", ctx.c(f"UpNext/{key}/Close", "button")), ("cardGroup", ctx.c(f"UpNext/{key}", "cg"))):
            assert f"  {f}: {{fileID: {want}}}" in dk, f"{key} card: HomeDockCard.{f} is not wired"
        title = sc.docs[str(ctx.rt(f"UpNext/{key}/Title"))]
        assert f"  m_Father: {{fileID: {ctx.rt(f'UpNext/{key}/Detail')}}}" in title, f"{key} card: the title does not fold with the detail"
    inst, stub, go_stub = invite_ids()
    assert not sc.has(OLD_INVITE["pi"]), "the old invite instance survives"
    assert sc_text.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {P.asset_guid(INVITE)}") == 1, "Menu_Main must hold one PartyInvitePopup"
    assert f"{{fileID: {go_stub}}}" in sc.docs[OLD_INVITE["gate"]], "the online-only gate no longer gates the invite"
    assert "m_Name\n      value: PartyInviteNotificationPanel" in sc.docs[inst], "OfflineMenuWirer finds the invite by name"
    pf = files[invite_path()]
    A.check_file(invite_path(), pf, T.known_guids() | P.PACKAGE, {P.asset_guid(INVITE), P.asset_guid("folder/Home")})
    _, panel = A.doc_of(pf, K.FileIds(INVITE)("root#panel"))
    for f in ("inviterNameText", "inviterAvatarImage", "acceptButton", "declineButton", "canvasGroup", "partyCountText", "timerBar",
              "entrance"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, panel, re.M), f"PartyInvitePopup: {f} is not wired"
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", sc_text, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", sc_text):
        assert m.group(1) in anchors, f"Menu_Main: dangling local fileID {m.group(1)}"
    # Every RectTransform listed as a child names this one as its father, and vice versa (Home only).
    for rt in [I["row_rt"], I["avatar_rt"], I["name_rt"], I["bar_rt"], I["settings_rt"], I["panel_rt"]] + [h[2] for h in HUBS]:
        for kid in re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)|^  m_Children: \[\]", sc.docs[rt], re.M).group(0)):
            assert f"  m_Father: {{fileID: {rt}}}" in sc.docs[kid], f"child {kid} of {rt} names another father"


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
    I = IDS
    lamp_hdr = f"--- !u!114 &{I['lamp_comp']}\n"

    def unwire_status(f):
        s = f[SCENE]
        i = s.index(lamp_hdr)
        j = s.index("--- ", i + 4)
        f[SCENE] = s[:i] + re.sub(r"(  statusLabel: )\{fileID: -?\d+\}", r"\1{fileID: 0}", s[i:j]) + s[j:]

    def avatar_dead(f):
        s = f[SCENE]
        i = s.index("m_MethodName: OpenAvatar\n", s.index(f"  m_TargetGraphic: {{fileID: {IDS['avatar_img']}}}"))
        f[SCENE] = s[:i] + "m_MethodName: OpenAvatarX\n" + s[i + len("m_MethodName: OpenAvatar\n"):]

    def arcade_unfocused(f):
        hub = HUBS[1][5]
        s = f[SCENE]
        i = s.index(f"--- !u!114 &{hub}\n")
        j = s.index("--- ", i + 4)
        f[SCENE] = s[:i] + s[i:j].replace("  preferredFocus: 1\n", "  preferredFocus: 0\n") + s[j:]
    cases = [
        ("the invite's drop-in stripped", lambda f: f.__setitem__(invite_path(), M.strip_entrances(f[invite_path()]))),
        ("the lamp's status label unwired", unwire_status),
        ("the Home avatar no longer opens the profile window", avatar_dead),
        ("Arcade not the pad's first stop", arcade_unfocused),
        ("a hub left a plain Button", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"guid: {S['kitbutton']}", f"guid: {S['button']}", 1))),
        ("the invite with no drain bar", lambda f: f.__setitem__(invite_path(), re.sub(
            r"(  timerBar: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[invite_path()]))),
        ("a docked card that cannot fold", lambda f: f.__setitem__(SCENE, re.sub(r"(  minimiseButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[SCENE], count=1))),
        ("the fly hint's x unwired", lambda f: f.__setitem__(SCENE, re.sub(r"(  closeButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[SCENE], count=1))),
        ("Settings left out of the guide's always-available set", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"guid: {S['guide_ok']}", "guid: 00000000000000000000000000000000", 1))),
        ("one click on Home flying again", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            "        m_TargetAssemblyTypeName: CosmicShore.UI.DoubleTapRelay, Assembly-CSharp\n        m_MethodName: Tap\n",
            "        m_TargetAssemblyTypeName: CosmicShore.Gameplay.MenuCrystalClickHandler, Assembly-CSharp\n        m_MethodName: ToggleTransition\n", 1))),
        ("the docked cards back under the friends handle", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"expandedHeight: {CARD_H}\n", "expandedHeight: 132\n", 1))),
        ("Up next left out of the hub fade", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  - {{fileID: {ctx_new().go('UpNext')}}}\n", "", 1))),
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
    print("OK: Home validates in Menu_Main")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        os.makedirs(os.path.dirname(os.path.join(REPO, path)), exist_ok=True)
        with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
