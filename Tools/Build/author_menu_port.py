#!/usr/bin/env python3
"""
Author the PORT screen of the style-B menu - the top bar's last tab - as prefabs, and open it in Menu_Main
(Docs/MENU_REDESIGN_B.md section 90; the Rev 27 boards on the "Menu Redesign B" canvas, Shombith 2026-10-27: "lets open
up the port now. Right now the port will only contain one tab called faction messages", "The weekly leaderboard can
also go to the port for now", "use the sea line background like everywhere", Vivox for the chat; section 91, Rev 32 D:
"Go with D", the sea-lit messages and field).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Port/
        FactionMessageRow.prefab  one message (Rev 32 D, section 91): the avatar and a card sized to its words, lit from
                                  its top-left corner in the sea's periwinkle; yours on the right with the full
                                  periwinkle-to-violet border (never the selection blue); a uGUI Button whose focus
                                  outline is the selection colour, so the pad can walk the feed
        PortView.prefab           the whole screen: the veil and the sea under the top bar, the tab row (LB, FACTION
                                  MESSAGES, WEEKLY LEADERBOARD, RB), then
                                  - Messages: the faction card, the rules, the muted pilots and the controls on the
                                    left; the feed panel on the right (title, the scrolling feed, the empty / tuning-in /
                                    offline card, the field with its count and SEND, the hint, the message actions and
                                    the report toast); FactionMessagesPane
                                  - Leaderboard: one LeaderboardView instance (author_menu_configure.py's, its veil, sea
                                    and BACK header switched off) with this week's title and countdown at the tab row's
                                    right; PortLeaderboardPane
                                  PortScreen on the root

    Assets/_Scenes/Menu_Main.unity
        One PortView instance under Screens, in the old Port panel's place; ScreenSwitcher.screens' PORT entry (id 3) pointed at its root;
        the old PortScreen panel (the PlayFab-era LeaderboardsMenu) switched off; ScreenSwitcher's LeaderboardMenu field
        (removed from the class) dropped. The PORT tab's lock is lifted by author_menu_topnav.py (LOCKED).

STANDING DOWN: every output is re-derived on each run; the scene edit removes its own documents and resets the fields it
owns before writing them again, so a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_port.py              # write
    python3 Tools/Build/author_menu_port.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_port.py --self-test  # planted defects must all be caught
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
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Port"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le = H.text, H.label, H.le

PREFABS = ("FactionMessageRow", "PortView")
INPUT = W.INPUT                                     # TMPro.TMP_InputField
PACKAGE = W.PACKAGE
S = dict(screen=C.script("PortScreen"), pane=C.script("FactionMessagesPane"), row=C.script("FactionMessageRow"),
         board=C.script("PortLeaderboardPane"), switcher=C.script("ScreenSwitcher"), tab=K.kit_script("KitTextTab"),
         lbpanel=C.script("WeeklyChallengeLeaderboardPanel"), mask=H.KIT_RECT_MASK_2D)
# The nested LeaderboardView's panel is referenced by its source key "List#panel": its stub kind is the panel script.
C.STUB_KIND["panel"] = (114, S["lbpanel"])
PORT_ID = 3                                         # ScreenSwitcher.MenuScreens.PORT
PROFILE_ICONS = W.PROFILE_ICONS                     # SO_DefaultProfileIcons: a sender's avatar id -> face
LEADERBOARD_VIEW = "LeaderboardView"                # author_menu_configure.py's board (the weekly window's own)

# Menu_Main, measured 2026-10-27 (names asserted on every run).
SCENE_IDS = dict(screens_rt="2525219157635147276", switcher="4216411968520269688",
                 old_port_go="6196685382603104254", old_port_rt="3557301585904278378")
OLD_PORT_NAME = "PortScreen"

# --- layout, at 1920 x 1080 (unscaled, like the weekly window's board this screen carries) -------------------------
TOP_BAR = 84
SIDE = 40
TABS_TOP, TABS_H = 104, 44
ZONE_TOP = 176                                      # the side column and the feed start here (the board's LEFT_TOP is 196)
SIDE_W = 420
FEED_X, FEED_W = 500, 1380
FEED_H = 1080 - ZONE_TOP - 40
FEED_PAD = 40
FEED_TOP = 100                                      # under the feed's title and rule
COMPOSER_H, COMPOSER_BOTTOM = 56, 40
FEED_BOTTOM = COMPOSER_BOTTOM + COMPOSER_H + 24
SEND_W = 160
ROW_W = FEED_W - 2 * FEED_PAD
AVATAR = 46
ACTIONS_W, ACTION_H = 340, 52
TABS = (("Messages", "FACTION MESSAGES"), ("Leaderboard", "WEEKLY LEADERBOARD"))
TAB_ROLE, TAB_PAD, TAB_GAP = "Body", 18, 34
RULES = ("Every pilot in the faction reads what you send.", "Keep it kind. Report a message that is not.",
         "Up to 140 characters, one message every 3 seconds.")
CONTROLS = (("A", "Write a message"), ("X", "Send"), ("Y", "Message actions (report, mute)"))


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def F(name):
    return K.FileIds(name)


def plain_label(node, t, role, token, **kw):
    """A TMP without KitText: its colour is painted by code (the hint: quiet, or Danger for a refusal)."""
    node.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text(t, role, tok(token), **kw))
    return node


def focus_button(target_key, nav=3):
    """A uGUI Button whose transition tints `target_key`'s outline: clear at rest, the selection colour when the pad or
    pointer is on it (the outline image is authored in the selection colour; the tint carries the alpha)."""
    def body(ctx, go):
        s = A.selectable(nav).replace("  m_Transition: 0\n", "  m_Transition: 1\n")
        s = s.replace("    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n", "    m_NormalColor: {r: 1, g: 1, b: 1, a: 0}\n")
        s = s.replace("    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
                      "    m_HighlightedColor: {r: 1, g: 1, b: 1, a: 0.5}\n")
        s = s.replace("    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}\n",
                      "    m_PressedColor: {r: 1, g: 1, b: 1, a: 1}\n")
        s = s.replace("    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
                      "    m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}\n")
        s = s.replace("    m_DisabledColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}\n",
                      "    m_DisabledColor: {r: 1, g: 1, b: 1, a: 0}\n")
        return mono(go, A.BUTTON, s + f"  m_TargetGraphic: {ref(ctx.c(target_key, 'img'))}\n"
                    "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n")
    return body


# --------------------------------------------------------------------------------------------
# FactionMessageRow.prefab
# --------------------------------------------------------------------------------------------

# --- Rev 32 D: the sea-lit look (Shombith 2026-10-27, "Go with D") -----------------------------------------------
# The sea's own colours (SeaLines.shader's mid and end, #7F98FF / #9C8CFF) lift the cards and the field; none is the
# selection blue (that is only the focus outline) and none is an element's colour (Docs/UI_COLOR_RULES.md).
def rgba(hexs, a=1.0):
    h = hexs.lstrip("#")
    return tuple(round(int(h[i:i + 2], 16) / 255, 4) for i in (0, 2, 4)) + (a,)


LOOKS = {
    "othersLook": dict(borderFrom=rgba("#96ACFF", 0.9), borderTo=rgba("#9C8CFF", 0.15), fillFrom=rgba("#242B70"),
                       fillTo=rgba("#181E56"), glow=rgba("#7F98FF", 0.28), ringFrom=rgba("#5B66B3"), ringTo=rgba("#5B66B3"),
                       name=rgba("#D3DAFF")),
    "yoursLook": dict(borderFrom=rgba("#A9BCFF"), borderTo=rgba("#B49BFF"), fillFrom=rgba("#3C4296"), fillTo=rgba("#272D76"),
                      glow=rgba("#AAA0FF", 0.35), ringFrom=rgba("#A9BCFF"), ringTo=rgba("#B49BFF"), name=rgba("#FFFFFF")),
}
BODY_TEXT, TIME_TEXT = rgba("#F6F7FF"), rgba("#AEB6E8")
FIELD = dict(borderFrom=rgba("#9FB2FF"), borderTo=rgba("#B49BFF"), fill=rgba("#1B2262"), glow=rgba("#7F98FF", 0.4),
             corner=rgba("#7F98FF", 0.3), placeholder=rgba("#A9B1E6"), count=rgba("#C2C9F2"))
SEND = dict(fillFrom=rgba("#A9BCFF"), fillTo=rgba("#B49BFF"), text=rgba("#141A52"))
CARD_MAX, CARD_PAD = 860, 36
GRAD = C.script("KitTwoToneGradient")
BLOOM = H.BLOOM


def gradient(c_from, c_to, angle=-45):
    """A KitTwoToneGradient: from the lit corner (top-left at -45 degrees, the left edge at 0) to the far one."""
    return lambda ctx, go: mono(go, GRAD, f"  from: {K.col(c_from)}\n  to: {K.col(c_to)}\n  angle: {K.f32(angle)}\n")


def tinted(parent, key, name, r, sprite, colour, grad=None, raycast=False, ignore=True, sliced=False, angle=-45):
    """An Image painted by its own colour (and, with `grad`, a KitTwoToneGradient over it), out of layout by default."""
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(colour, sprite, sliced, raycast=raycast))
    if grad:
        n.add("grad", 114, gradient(*grad, angle=angle))
    if ignore:
        n.add("le", 114, le(ignore=True))
    return n


def build_row():
    root = N("root", "FactionMessageRow", R(size=(ROW_W, 80)))
    # Yours aligns right with the avatar last (FactionMessageRow.Show); everyone else's aligns left.
    root.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 6, (1, 1), (0, 0)))
    av = root.child(N("Avatar", "Avatar", R(size=(AVATAR, AVATAR))))
    av.add("le", 114, le(mw=AVATAR, mh=AVATAR, pw=AVATAR, ph=AVATAR))
    tinted(av, "Avatar/Ring", "Ring", STRETCH, K.kit_sprite("kit_oct_fill"), (1.0, 1.0, 1.0, 1.0),
           grad=(LOOKS["othersLook"]["ringFrom"], LOOKS["othersLook"]["ringTo"]), ignore=False)
    A.art_node(av, "Avatar/Icon", "Icon", R((0, 0), (1, 1), (0, 0), (-4, -4)), A.AVATAR_PLACEHOLDER, preserve=True)

    card = root.child(N("Card", "Card", R(size=(600, 72))))
    card.add("le", 114, le(pw=600)).add("vlg", 114, K.hv_layout(True, (CARD_PAD // 2, CARD_PAD // 2, 11, 13), 5, 0, (1, 1), (1, 0)))
    o = LOOKS["othersLook"]
    tinted(card, "Card/Border", "Border", STRETCH, K.kit_sprite("kit_sliver10_fill"), (1.0, 1.0, 1.0, 1.0), grad=(o["borderFrom"], o["borderTo"]), sliced=True)
    tinted(card, "Card/Fill", "Fill", R((0, 0), (1, 1), (0, 0), (-2, -2)), K.kit_sprite("kit_sliver10_fill"), (1.0, 1.0, 1.0, 1.0),
           grad=(o["fillFrom"], o["fillTo"]), sliced=True)
    tinted(card, "Card/Glow", "Glow", R((0, 1), None, (110, -36), (260, 110)), BLOOM, o["glow"])
    # The pad's focus outline: the selection colour, shown only by the Button's transition (its tint carries the alpha).
    tinted(card, "Card/Focus", "Focus", R((0, 0), (1, 1), (0, 0), (8, 8)), K.kit_sprite("kit_sliver10_line1"), tok("Selection"), raycast=True, sliced=True)

    head = card.child(N("Card/Head", "Head", R(size=(0, 20))))
    head.add("le", 114, le(ph=20)).add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 3, (1, 1), (0, 0)))
    plain_label(head.child(N("Card/Head/Name", "Name", R(size=(160, 20)))), "PILOT", "Label", "TextLight")
    you = head.child(N("Card/Head/You", "You", R(size=(40, 20))))
    you.add("cg", 225, H.canvas_group(False))
    plain_label(you, "YOU", "Label", "TextLight")
    tm = head.child(N("Card/Head/Time", "Time", R(size=(60, 20))))
    tm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("now", "DataSmall", TIME_TEXT))
    body = card.child(N("Card/Body", "Body", R(size=(0, 26))))
    body.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Message", "Body", BODY_TEXT, valign="top", wrap=True))
    # The words are the sender's: never rich text (a typed <size=500> must stay text).
    body.comps = [(k, cid, (lambda b: (lambda ctx, go: b(ctx, go).replace("  m_isRichText: 1\n", "  m_isRichText: 0\n")))(fn))
                  if k == "tmp" else (k, cid, fn) for k, cid, fn in body.comps]

    root.add("button", 114, focus_button("Card/Focus"))

    def looks():
        out = ""
        for field, look in LOOKS.items():
            out += f"  {field}:\n" + "".join(f"    {k}: {K.col(v)}\n" for k, v in look.items())
        return out
    root.add("row", 114, lambda ctx, go: mono(go, S["row"],
             f"  button: {ref(ctx.c('root', 'button'))}\n  layout: {ref(ctx.c('root', 'hlg'))}\n"
             f"  avatarHolder: {ref(ctx.rt('Avatar'))}\n  avatar: {ref(ctx.c('Avatar/Icon', 'img'))}\n"
             f"  avatarRing: {ref(ctx.c('Avatar/Ring', 'grad'))}\n  card: {ref(ctx.c('Card', 'le'))}\n"
             f"  cardBorder: {ref(ctx.c('Card/Border', 'grad'))}\n  cardFill: {ref(ctx.c('Card/Fill', 'grad'))}\n"
             f"  cardGlow: {ref(ctx.c('Card/Glow', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Card/Head/Name', 'tmp'))}\n  timeText: {ref(ctx.c('Card/Head/Time', 'tmp'))}\n"
             f"  bodyText: {ref(ctx.c('Card/Body', 'tmp'))}\n  youTag: {ref(ctx.c('Card/Head/You', 'cg'))}\n"
             f"  maxCardWidth: {CARD_MAX}\n  cardPadding: {CARD_PAD}\n  headExtra: 110\n" + looks()))
    return root


# --------------------------------------------------------------------------------------------
# PortView.prefab
# --------------------------------------------------------------------------------------------

def tab_row(root):
    row = root.child(N("Tabs", "Tabs", R((0, 1), (1, 1), (0, -TABS_TOP), (-2 * SIDE, TABS_H), (0.5, 1))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), TAB_GAP, 3, (0, 0), (0, 0)))
    row.children.append(H.keycap("TabsLeftKeycap", "LB"))
    for key, caption in TABS:
        H.text_tab(row, f"Tabs/{key}", key, caption, TAB_ROLE, K.label_width(caption, TAB_ROLE) + TAB_PAD, TABS_H)
    row.children.append(H.keycap("TabsRightKeycap", "RB"))
    rule = root.child(N("TabsRule", "TabsRule", R((0, 1), (1, 1), (0, -(TABS_TOP + TABS_H)), (-2 * SIDE, 1), (0.5, 1))))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))


def card(parent, key, name, y, h):
    n = parent.child(N(key, name, TL(0, y, SIDE_W, h)))
    C.frame(n, f"{key}/Frame", 14, fill_alpha=0.94)
    return n


def side_column(pane):
    side = pane.child(N("Messages/Side", "Side", TL(SIDE, ZONE_TOP, SIDE_W, 1080 - ZONE_TOP - 40)))
    side.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    f = card(side, "Messages/Side/Faction", "Faction", 0, 196)
    A.image_node(f, "Messages/Side/Faction/Emblem", "Emblem", TL(26, 26, 84, 74), "SurfaceLight", K.kit_sprite("kit_hex_line2"),
                 preserve=True)
    A.image_node(f, "Messages/Side/Faction/Glyph", "Glyph", TL(48, 43, 40, 40), "TextLight", K.chrome_sprite("chrome_people"),
                 preserve=True)
    label(f.child(N("Messages/Side/Faction/Kicker", "Kicker", TL(132, 34, 260, 18))), "Your faction", "Label", "NeutralLightest")
    H.ellipsize(label(f.child(N("Messages/Side/Faction/Name", "Name", TL(132, 56, 262, 44))), "All Pilots", "H2", "TextLight"))
    label(f.child(N("Messages/Side/Faction/Line", "Line", TL(26, 120, SIDE_W - 52, 56))),
          "Every pilot flies in one faction for now. Factions of your own come later.", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)

    r = card(side, "Messages/Side/Rules", "Rules", 216, 196)
    label(r.child(N("Messages/Side/Rules/Heading", "Heading", TL(26, 24, 300, 18))), "Faction rules", "Label", "TextLight")
    for i, line in enumerate(RULES):
        y = 56 + i * 44
        label(r.child(N(f"Messages/Side/Rules/Number{i + 1}", f"Number{i + 1}", TL(26, y, 20, 20))), str(i + 1), "Label",
              "NeutralLightest")
        label(r.child(N(f"Messages/Side/Rules/Rule{i + 1}", f"Rule{i + 1}", TL(56, y - 2, SIDE_W - 82, 40))), line, "BodySmall",
              "TextLight", valign="top", wrap=True)

    mu = side.child(N("Messages/Side/Muted", "Muted", TL(0, 432, SIDE_W, 64)))
    mu.add("cg", 225, H.canvas_group(False))
    C.frame(mu, "Messages/Side/Muted/Frame", 14, fill_alpha=0.94)
    label(mu.child(N("Messages/Side/Muted/Text", "Text", TL(22, 0, 220, 64))), "Muted pilots: 1", "Body", "TextLight")
    mu.children.append(P.kit_button("Messages/Side/Muted/UnmuteAll", "UnmuteAll", "Default", "UNMUTE ALL",
                                    R((1, 0.5), None, (-12, 0), (150, 44), (1, 0.5)), opaque=False, small=True))

    ct = side.child(N("Messages/Side/Controls", "Controls", TL(4, 520, SIDE_W - 8, 150)))
    ct.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 0, (1, 1), (1, 0)))
    head = ct.child(N("Messages/Side/Controls/Heading", "Heading", R(size=(0, 18))))
    head.add("le", 114, le(ph=18))
    label(head, "Controls", "Label", "NeutralLightest")
    for cap, line in CONTROLS:
        k = f"Messages/Side/Controls/{cap}"
        row = ct.child(N(k, cap, R(size=(0, 30))))
        row.add("le", 114, le(ph=30)).add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 12, 3, (0, 0), (0, 0)))
        row.children.append(H.keycap(f"{k}/Keycap", cap))
        txt = row.child(N(f"{k}/Line", "Line", R(size=(SIDE_W - 70, 24))))
        label(txt, line, "BodySmall", "TextLight")


def input_field(ctx, go):
    """The message field: single line, no rich text, the pad's A opens it (it does not open on focus alone)."""
    key = "Messages/Feed/Composer/Field"
    body = W.input_field(ctx, go)
    body = body.replace(f"  m_TargetGraphic: {ref(ctx.c('Window/NamePage/Field/Fill', 'img'))}\n",
                        f"  m_TargetGraphic: {ref(ctx.c(key + '/Border', 'img'))}\n")
    body = body.replace(f"  m_TextViewport: {ref(ctx.rt('Window/NamePage/Field/TextArea'))}\n",
                        f"  m_TextViewport: {ref(ctx.rt(key + '/TextArea'))}\n")
    body = body.replace(f"  m_TextComponent: {ref(ctx.c('Window/NamePage/Field/TextArea/Text', 'tmp'))}\n",
                        f"  m_TextComponent: {ref(ctx.c(key + '/TextArea/Text', 'tmp'))}\n")
    body = body.replace(f"  m_Placeholder: {ref(ctx.c('Window/NamePage/Field/TextArea/Placeholder', 'tmp'))}\n",
                        f"  m_Placeholder: {ref(ctx.c(key + '/TextArea/Placeholder', 'tmp'))}\n")
    body = body.replace("  m_CharacterLimit: 0\n", "  m_CharacterLimit: 140\n")
    body = body.replace("  m_ShouldActivateOnSelect: 1\n", "  m_ShouldActivateOnSelect: 0\n")
    body = body.replace("  m_OnFocusSelectAll: 1\n", "  m_OnFocusSelectAll: 0\n")
    body = body.replace("  m_ResetOnDeActivation: 1\n", "  m_ResetOnDeActivation: 0\n")
    body = body.replace("  m_RestoreOriginalTextOnEscape: 1\n", "  m_RestoreOriginalTextOnEscape: 0\n")
    # The pad's focus shows as the selection outline round the field (its alpha carried by the tint), never on the border.
    body = body.replace(f"  m_TargetGraphic: {ref(ctx.c(key + '/Border', 'img'))}\n", f"  m_TargetGraphic: {ref(ctx.c(key + '/FocusRing', 'img'))}\n")
    body = body.replace("  m_Transition: 0\n", "  m_Transition: 1\n")
    for name, a in (("Normal", 0), ("Highlighted", 0.5), ("Pressed", 1), ("Selected", 1), ("Disabled", 0)):
        body = re.sub(r"    m_%sColor: \{[^}]*\}\n" % name, f"    m_{name}Color: {{r: 1, g: 1, b: 1, a: {a}}}\n", body, count=1)
    assert "Window/NamePage" not in body and "  m_CharacterLimit: 140\n" in body
    return body


def composer(feed):
    """Rev 32 D: the field has the cards' periwinkle-to-violet border, a soft glow behind it and a lit corner; the pad's
    focus is the selection outline round it (the field's transition target). SEND is lit periwinkle with dark words."""
    key = "Messages/Feed/Composer"
    cp = feed.child(N(key, "Composer", R((0, 0), (1, 0), (0, COMPOSER_BOTTOM), (-2 * FEED_PAD, COMPOSER_H), (0.5, 0))))
    cp.add("cg", 225, H.canvas_group(True))
    f = cp.child(N(f"{key}/Field", "Field", R((0, 0), (1, 1), (-(SEND_W + 16) / 2, 0), (-(SEND_W + 16), 0))))
    tinted(f, f"{key}/Field/Glow", "Glow", R((0, 0), (1, 1), (0, 0), (60, 48)), BLOOM, FIELD["glow"], ignore=False)
    tinted(f, f"{key}/Field/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10)), K.kit_sprite("kit_sliver10_line1"),
           tok("Selection"), ignore=False, sliced=True)
    tinted(f, f"{key}/Field/Border", "Border", STRETCH, K.kit_sprite("kit_sliver10_fill"), (1.0, 1.0, 1.0, 1.0),
           grad=(FIELD["borderFrom"], FIELD["borderTo"]), ignore=False, sliced=True, angle=0)
    tinted(f, f"{key}/Field/Fill", "Fill", R((0, 0), (1, 1), (0, 0), (-4, -4)), K.kit_sprite("kit_sliver10_fill"), FIELD["fill"],
           raycast=True, ignore=False, sliced=True)
    tinted(f, f"{key}/Field/Corner", "Corner", R((0, 1), None, (150, -10), (420, 90)), BLOOM, FIELD["corner"], ignore=False)
    area = f.child(N(f"{key}/Field/TextArea", "TextArea", R((0, 0), (1, 1), (-50, 0), (-136, -8))))
    area.add("mask", 114, A.rect_mask())
    ph = area.child(N(f"{key}/Field/TextArea/Placeholder", "Placeholder", STRETCH))
    ph.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Message all pilots", "Body", FIELD["placeholder"]))
    tx = area.child(N(f"{key}/Field/TextArea/Text", "Text", STRETCH))
    tx.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("", "Body", (1.0, 1.0, 1.0, 1.0)))
    cnt = f.child(N(f"{key}/Field/Count", "Count", R((1, 0.5), None, (-18, 0), (100, 20), (1, 0.5))))
    cnt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("0 / 140", "DataSmall", FIELD["count"], align="right"))
    f.add("input", 114, input_field)
    s = cp.child(N(f"{key}/Send", "Send", R((1, 0.5), None, (0, 0), (SEND_W, COMPOSER_H), (1, 0.5))))
    tinted(s, f"{key}/Send/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10)), K.kit_sprite("kit_sliver10_line1"),
           tok("Selection"), ignore=False, sliced=True)
    tinted(s, f"{key}/Send/Fill", "Fill", STRETCH, K.kit_sprite("kit_sliver10_fill"), (1.0, 1.0, 1.0, 1.0),
           grad=(SEND["fillFrom"], SEND["fillTo"]), raycast=True, ignore=False, sliced=True, angle=0)
    lab = s.child(N(f"{key}/Send/Label", "Label", STRETCH))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("SEND", "Button", SEND["text"], align="center"))
    s.add("button", 114, focus_button(f"{key}/Send/FocusRing"))
    hint = feed.child(N("Messages/Feed/Hint", "Hint", R((0, 0), (1, 0), (0, 12), (-2 * FEED_PAD - 4, 22), (0.5, 0))))
    plain_label(hint, "Enter sends. Every pilot in All Pilots sees it.", "BodySmall", "NeutralLightest")


def feed_scroll(feed):
    top, bottom = FEED_TOP, FEED_BOTTOM
    sv = feed.child(N("Messages/Feed/Scroll", "Scroll", R((0, 0), (1, 1), (0, (bottom - top) / 2), (-2 * FEED_PAD, -(top + bottom)))))
    vp = sv.child(N("Messages/Feed/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, S["mask"], "  m_Padding: {x: -6, y: -6, z: -6, w: -6}\n  m_Softness: {x: 0, y: 0}\n"))
    # Anchored to the foot: a short feed sits on the field, newest last, as a chat reads.
    ct = vp.child(N("Messages/Feed/Scroll/Viewport/Content", "Content", R((0, 0), (1, 0), (0, 0), (0, 0), (0.5, 0))))
    ct.add("vlg", 114, K.hv_layout(True, (0, 0, 6, 6), 10, 6, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    sv.add("scroll", 114, lambda ctx, go: K.mono(go, A.SCROLL_RECT,
           f"  m_Content: {ref(ctx.rt('Messages/Feed/Scroll/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
           "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
           f"  m_ScrollSensitivity: 30\n  m_Viewport: {ref(ctx.rt('Messages/Feed/Scroll/Viewport'))}\n"
           "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
           "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
           "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
           "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))


def status_card(feed):
    key = "Messages/Feed/Status"
    st = feed.child(N(key, "Status", R((0, 0), (1, 1), (0, (FEED_BOTTOM - FEED_TOP) / 2), (-2 * FEED_PAD, -(FEED_TOP + FEED_BOTTOM)))))
    st.add("cg", 225, H.canvas_group(True))
    ld = st.child(N(f"{key}/Loading", "Loading", R((0.5, 0.5), None, (0, 74), (44, 44))))
    ld.add("cg", 225, H.canvas_group(True))
    hx = A.image_node(ld, f"{key}/Loading/Hex", "Hex", STRETCH, "NeutralLightest", K.kit_sprite("kit_hex_line2"), preserve=True)
    hx.add("amb", 114, M.ambient("Spin", period=1.2, amplitude=1))
    label(st.child(N(f"{key}/Title", "Title", R((0, 0.5), (1, 0.5), (0, 16), (0, 40)))), "Tuning in to the faction...", "H3",
          "TextLight", align="center")
    label(st.child(N(f"{key}/Line", "Line", R((0, 0.5), (1, 0.5), (0, -26), (0, 28)))), "", "Body", "NeutralLightest", align="center")
    rt = st.child(N(f"{key}/Retry", "Retry", R((0.5, 0.5), None, (0, -92), (220, 52))))
    rt.add("cg", 225, H.canvas_group(False))
    rt.children.append(P.kit_button(f"{key}/Retry/Button", "TryAgain", "Default", "TRY AGAIN", STRETCH, opaque=False))


def actions(feed):
    key = "Messages/Feed/Actions"
    ac = feed.child(N(key, "Actions", STRETCH))
    ac.add("cg", 225, H.canvas_group(False))
    h = 3 * ACTION_H + 2 * 8 + 2 * 18
    pn = ac.child(N(f"{key}/Panel", "Panel", R((1, 1), None, (-(FEED_PAD + 16), -FEED_TOP), (ACTIONS_W, h), (1, 1))))
    C.frame(pn, f"{key}/Panel/Frame", 14, border="Selection", fill_alpha=0.98)
    for i, (name, caption) in enumerate((("Report", "REPORT MESSAGE"), ("Mute", "MUTE PILOT"), ("Cancel", "CANCEL"))):
        pn.children.append(P.kit_button(f"{key}/Panel/{name}", name, "Cancel" if name == "Cancel" else "Default", caption,
                                        TL(18, 18 + i * (ACTION_H + 8), ACTIONS_W - 36, ACTION_H), opaque=False))


def toast(feed):
    key = "Messages/Feed/Toast"
    t = feed.child(N(key, "Toast", R((0, 0), None, (FEED_PAD, FEED_BOTTOM + 8), (600, 52), (0, 0))))
    t.add("cg", 225, H.canvas_group(False))
    C.frame(t, f"{key}/Frame", 10, border="Cta", fill_alpha=0.98)
    label(t.child(N(f"{key}/Text", "Text", R((0, 0), (1, 1), (0, 0), (-40, 0)))), "Report sent. Thank you.", "Body", "TextLight")


def feed_panel(pane):
    feed = pane.child(N("Messages/Feed", "Feed", TL(FEED_X, ZONE_TOP, FEED_W, FEED_H)))
    feed.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    C.frame(feed, "Messages/Feed/Frame", 14, fill_alpha=0.92)
    M.bracket_nodes(feed, "Messages/Feed/Bracket", inset=12)
    label(feed.child(N("Messages/Feed/Title", "Title", TL(FEED_PAD, 26, 600, 44))), "Faction messages", "H2", "TextLight")
    label(feed.child(N("Messages/Feed/Kept", "Kept", TR(FEED_PAD, 42, 400, 18))), "The last 50 messages", "Label",
          "NeutralLightest", align="right")
    rule = feed.child(N("Messages/Feed/Rule", "Rule", R((0, 1), (1, 1), (0, -84), (-2 * FEED_PAD, 1), (0.5, 1))))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    # Hierarchy is focus order (ScreenSwitcher.Refocus, PortScreen.Show take the first live Selectable): the field first.
    composer(feed)
    feed_scroll(feed)
    status_card(feed)
    toast(feed)
    actions(feed)           # last: drawn over the feed


def pane_body(ctx):
    I = lambda k, s: ctx.iref(k, s)
    return (f"  factionId: all\n  factionName: All Pilots\n  factionNameText: {ref(ctx.c('Messages/Side/Faction/Name', 'tmp'))}\n"
            "  maxLength: 140\n  minIntervalSeconds: 3\n  minPostAge: 13\n  historyCount: 50\n  feedCapacity: 100\n"
            "  retrySeconds: 15\n"
            f"  feedScroll: {ref(ctx.c('Messages/Feed/Scroll', 'scroll'))}\n"
            f"  feedContent: {ref(ctx.rt('Messages/Feed/Scroll/Viewport/Content'))}\n"
            f"  rowPrefab: {K.ref(F('FactionMessageRow')('root#row'), asset_guid('FactionMessageRow'))}\n"
            f"  profileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n"
            f"  statusGroup: {ref(ctx.c('Messages/Feed/Status', 'cg'))}\n"
            f"  statusTitle: {ref(ctx.c('Messages/Feed/Status/Title', 'tmp'))}\n"
            f"  statusLine: {ref(ctx.c('Messages/Feed/Status/Line', 'tmp'))}\n"
            f"  loadingGroup: {ref(ctx.c('Messages/Feed/Status/Loading', 'cg'))}\n"
            f"  retryGroup: {ref(ctx.c('Messages/Feed/Status/Retry', 'cg'))}\n"
            f"  retryButton: {ref(I('Messages/Feed/Status/Retry/Button', 'root#button'))}\n"
            "  emptyTitle: No messages yet\n  emptyLine: Say hello to every pilot in the faction.\n"
            "  loadingTitle: Tuning in to the faction...\n  offlineTitle: Faction messages are offline\n"
            "  offlineLine: Check your connection. Port keeps trying every 15 seconds.\n"
            f"  input: {ref(ctx.c('Messages/Feed/Composer/Field', 'input'))}\n"
            f"  sendButton: {ref(ctx.c('Messages/Feed/Composer/Send', 'button'))}\n"
            f"  composerGroup: {ref(ctx.c('Messages/Feed/Composer', 'cg'))}\n"
            f"  countText: {ref(ctx.c('Messages/Feed/Composer/Field/Count', 'tmp'))}\n"
            "  countTemplate: '{count} / {max}'\n"
            f"  hintText: {ref(ctx.c('Messages/Feed/Hint', 'tmp'))}\n"
            "  readyHint: Enter sends. Every pilot in {faction} sees it.\n"
            "  sentHint: Sent. Your next message can go in {seconds} s.\n"
            "  tooLongHint: Too long - {max} characters at most.\n"
            "  tooSoonHint: 'Not sent: too fast. Wait {seconds} s, then send it again.'\n"
            "  readOnlyHint: This account can read faction messages but not send them.\n"
            "  offlineHint: Not sent - faction messages are offline.\n"
            "  timeNow: now\n  timeMinutes: '{n}m'\n  timeHours: '{n}h'\n  timeDays: '{n}d'\n"
            f"  actionsGroup: {ref(ctx.c('Messages/Feed/Actions', 'cg'))}\n"
            f"  actionsPanel: {ref(ctx.rt('Messages/Feed/Actions/Panel'))}\n"
            f"  reportButton: {ref(I('Messages/Feed/Actions/Panel/Report', 'root#button'))}\n"
            f"  muteButton: {ref(I('Messages/Feed/Actions/Panel/Mute', 'root#button'))}\n"
            f"  muteLabel: {ref(I('Messages/Feed/Actions/Panel/Mute', 'Label#tmp'))}\n"
            "  muteTemplate: Mute {name}\n"
            f"  cancelButton: {ref(I('Messages/Feed/Actions/Panel/Cancel', 'root#button'))}\n"
            f"  mutedGroup: {ref(ctx.c('Messages/Side/Muted', 'cg'))}\n"
            f"  mutedText: {ref(ctx.c('Messages/Side/Muted/Text', 'tmp'))}\n"
            "  mutedTemplate: 'Muted pilots: {count}'\n"
            f"  unmuteAllButton: {ref(I('Messages/Side/Muted/UnmuteAll', 'root#button'))}\n"
            f"  toastGroup: {ref(ctx.c('Messages/Feed/Toast', 'cg'))}\n"
            f"  toastText: {ref(ctx.c('Messages/Feed/Toast/Text', 'tmp'))}\n"
            "  reportSent: Report sent. Thank you.\n"
            "  reportFailed: The report could not be sent. Try again later.\n"
            "  toastSeconds: 2.6\n"
            f"  theme: {K.ref(11400000, K.THEME, 2)}\n")


def messages_pane(root):
    pane = root.child(N("Messages", "Messages", STRETCH))
    pane.add("cg", 225, H.canvas_group(True))
    # Focus order: the feed (its field first), then the side column.
    feed_panel(pane)
    side_column(pane)
    pane.add("pane", 114, lambda ctx, go: mono(go, S["pane"], pane_body(ctx)))
    return pane


LB_OFF = ("Veil#go", "VeilSea#go", "Header#go")      # the window's own backdrop and BACK header: the Port draws its own


def leaderboard_pane(root):
    pane = root.child(N("Leaderboard", "Leaderboard", STRETCH))
    pane.add("cg", 225, H.canvas_group(False))
    mods = [(k, "m_IsActive", 0, None) for k in LB_OFF]
    pane.children.append(K.Instance("Leaderboard/View", LEADERBOARD_VIEW, C.prefab_guid(LEADERBOARD_VIEW), LEADERBOARD_VIEW,
                                    STRETCH, mods))
    # This week's challenge and its countdown, at the tab row's right.
    label(pane.child(N("Leaderboard/Countdown", "Countdown", TR(SIDE, TABS_TOP + 7, 190, 30))), "6d 03:12:09", "Data", "TextLight",
          align="right")
    label(pane.child(N("Leaderboard/EndsIn", "EndsIn", TR(SIDE + 200, TABS_TOP + 13, 110, 18))), "Ends in", "Label",
          "NeutralLightest", align="right")
    H.ellipsize(label(pane.child(N("Leaderboard/Title", "Title", TR(SIDE + 330, TABS_TOP + 5, 420, 34))), "Weekly challenge", "H3",
                      "TextLight", align="right"))
    pane.add("board", 114, lambda ctx, go: mono(go, S["board"],
             f"  panel: {ref(ctx.iref('Leaderboard/View', 'List#panel'))}\n"
             f"  worldTab: {ref(ctx.iref('Leaderboard/View', 'Tabs/World#tile'))}\n"
             f"  regionalTab: {ref(ctx.iref('Leaderboard/View', 'Tabs/Regional#tile'))}\n"
             f"  friendsTab: {ref(ctx.iref('Leaderboard/View', 'Tabs/Friends#tile'))}\n"
             "  friendsTabEnabled: 0\n"
             f"  titleText: {ref(ctx.c('Leaderboard/Title', 'tmp'))}\n  titleFallback: Weekly challenge\n"
             f"  countdownText: {ref(ctx.c('Leaderboard/Countdown', 'tmp'))}\n"
             f"  games: {K.ref(11400000, C.ALL_GAMES, 2)}\n"))
    return pane


def build_view():
    root = N("root", "PortView", STRETCH)
    # The sea, as behind every window (Shombith 2026-10-27: "use the sea line background like everywhere"): the veil
    # and the sea lines under the top bar, exactly the Codex's and the Hangar's.
    veil = root.child(N("Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -TOP_BAR / 2), (0, -TOP_BAR))))
    veil.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", H.WINDOW_BACKDROP_ALPHA), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", H.WINDOW_BACKDROP_ALPHA))
    sea = root.child(N("Sea", "Sea", R((0, 0), (1, 1), (0, -TOP_BAR / 2), (0, -TOP_BAR))))
    sea.add("cr", 222, K.canvas_renderer()).add("img", 114, M.lattice_image(M.SEA_WINDOW_MAT))
    messages_pane(root)
    leaderboard_pane(root)
    tab_row(root)
    root.add("screen", 114, lambda ctx, go: mono(go, S["screen"],
             refs("tabs", [ctx.c(f"Tabs/{k}", "tab") for k, _ in TABS])
             + refs("panes", [ctx.c("Messages", "cg"), ctx.c("Leaderboard", "cg")])
             + f"  messages: {ref(ctx.c('Messages', 'pane'))}\n  leaderboard: {ref(ctx.c('Leaderboard', 'board'))}\n"
             "  shoulderInput: 1\n"))
    return root


FOLDER_META = ("fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prefab_outputs():
    out = {OUT_DIR + ".meta": FOLDER_META.format(g=asset_guid("PortFolder"))}
    for name, build in (("FactionMessageRow", build_row), ("PortView", build_view)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    return out


# --------------------------------------------------------------------------------------------
# Menu_Main: the instance under Screens, the PORT slot pointed at it, the old panel off
# --------------------------------------------------------------------------------------------

def scene_ctx():
    return P.Ctx("Menu_Main/PortView")


def scene_ids():
    ctx = scene_ctx()
    return str(ctx.fid("Port#inst")), str(ctx.fid("Port#stub"))


def screens_entry(root_fid):
    return f"  - id: {PORT_ID}\n    root: {{fileID: {root_fid}}}\n"


def scene_transform(text_):
    sc = T.Scene(text_)
    I = SCENE_IDS
    assert sc.script(I["switcher"]) == S["switcher"], "Menu_Main's ScreenSwitcher moved"
    scr = sc.docs[I["screens_rt"]]
    assert sc.field(sc.go_of(I["screens_rt"]), "m_Name") == "Screens", "Menu_Main's Screens container moved"
    assert sc.field(I["old_port_go"], "m_Name") == OLD_PORT_NAME, "the old PortScreen panel moved"
    inst, stub = scene_ids()
    owned = C.instance_docs(sc, inst) if sc.has(inst) else []
    for fid in list(owned) + [stub]:
        if sc.has(fid):
            sc.remove(fid)
    out = []
    K.emit_instance(scene_ctx(), K.Instance("Port", "PortView", asset_guid("PortView"), "PortView", STRETCH, []), I["screens_rt"], out)
    kids = [k for k in re.findall(r"^  - \{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", scr, re.M).group(1), re.M)
            if k != stub]
    # File order: just after the old panel's transform, so the Codex's documents (just after Screens') never move ours.
    anchor = I["old_port_rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    # In the old panel's place among Screens' children (the Codex takes the first place; neither moves the other).
    at = kids.index(I["old_port_rt"])
    sc.replace(I["screens_rt"], T.set_block(scr, "m_Children", K.refs_list("m_Children", kids[:at] + [stub] + kids[at:])))
    sw = sc.docs[I["switcher"]]
    old = re.search(r"  - id: %d\n    root: \{fileID: -?\d+\}\n" % PORT_ID, sw)
    assert old, "ScreenSwitcher.screens has no PORT entry"
    sw = sw.replace(old.group(0), screens_entry(stub), 1)
    # ScreenSwitcher.LeaderboardMenu is gone from the class (the PlayFab-era Port panel is no longer loaded on PORT).
    sw = re.sub(r"^  LeaderboardMenu: \{fileID: -?\d+\}\n", "", sw, flags=re.M)
    sc.replace(I["switcher"], sw)
    # The old panel stays in the scene, switched off: nothing reaches it any more, and deleting it is a cleanup of its own.
    go = sc.docs[I["old_port_go"]]
    sc.replace(I["old_port_go"], re.sub(r"^  m_IsActive: \d\n", "  m_IsActive: 0\n", go, count=1, flags=re.M))
    return sc.text()


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

PANE_FIELDS = (("factionNameText", K.TMP_UGUI), ("feedScroll", A.SCROLL_RECT), ("feedContent", 224), ("statusGroup", 225),
               ("statusTitle", K.TMP_UGUI), ("statusLine", K.TMP_UGUI), ("loadingGroup", 225), ("retryGroup", 225),
               ("retryButton", K.kit_script("KitButton")), ("input", INPUT), ("sendButton", A.BUTTON),
               ("composerGroup", 225), ("countText", K.TMP_UGUI), ("hintText", K.TMP_UGUI), ("actionsGroup", 225),
               ("actionsPanel", 224), ("reportButton", K.kit_script("KitButton")), ("muteButton", K.kit_script("KitButton")),
               ("muteLabel", K.TMP_UGUI), ("cancelButton", K.kit_script("KitButton")), ("mutedGroup", 225),
               ("mutedText", K.TMP_UGUI), ("unmuteAllButton", K.kit_script("KitButton")), ("toastGroup", 225),
               ("toastText", K.TMP_UGUI))


def validate(files):
    guids = T.known_guids() | PACKAGE | {asset_guid(n) for n in PREFABS} | {INPUT}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for name in PREFABS:
        A.check_file(prefab_path(name), files[prefab_path(name)], guids, minted)

    row = files[prefab_path("FactionMessageRow")]
    _, rbody = A.doc_of(row, F("FactionMessageRow")("root#row"))
    assert rbody and S["row"] in rbody, "FactionMessageRow.prefab: the root carries no FactionMessageRow"
    R_ = F("FactionMessageRow")
    for field, want in (("button", A.BUTTON), ("layout", K.HLAYOUT), ("avatarHolder", 224), ("avatar", K.IMAGE),
                        ("avatarRing", GRAD), ("card", K.LAYOUT_ELEMENT), ("cardBorder", GRAD), ("cardFill", GRAD),
                        ("cardGlow", K.IMAGE), ("nameText", K.TMP_UGUI), ("timeText", K.TMP_UGUI), ("bodyText", K.TMP_UGUI),
                        ("youTag", 225)):
        C.wired(row, rbody, field, want, "FactionMessageRow")
    _, tbody = A.doc_of(row, R_("Card/Body#tmp"))
    assert "  m_isRichText: 0\n" in tbody, "FactionMessageRow: a message's words must never be rich text"
    assert "  m_Alpha: 0\n" in A.doc_of(row, R_("Card/Head/You#cg"))[1], "FactionMessageRow: YOU must start hidden"
    # Rev 32 D: both looks authored, yours brighter than everyone else's, and neither in the selection blue (that is only
    # the focus outline, Docs/UI_COLOR_RULES.md) nor in an element's colour.
    sel = K.col(tok("Selection"))
    looks = {}
    for field in LOOKS:
        m = re.search(r"^  %s:\n((?:    .*\n)+)" % field, rbody, re.M)
        assert m, f"FactionMessageRow.{field} is not authored"
        looks[field] = m.group(1)
        assert sel not in m.group(1), f"FactionMessageRow.{field} must not use the selection blue"
        assert "a: 0}" not in m.group(1).split("glow:")[0], f"FactionMessageRow.{field}: a clear border or fill draws nothing"
    assert looks["othersLook"] != looks["yoursLook"], "FactionMessageRow: your messages must look different from everyone else's"
    _, bbody = A.doc_of(row, R_("root#button"))
    assert "  m_Transition: 1\n" in bbody and f"m_TargetGraphic: {{fileID: {R_('Card/Focus#img')}}}" in bbody, \
        "FactionMessageRow: the pad's focus must show on the card's outline"
    assert f"  maxCardWidth: {CARD_MAX}\n" in rbody, "FactionMessageRow: cards are sized to their words, up to a maximum"

    view = files[prefab_path("PortView")]
    V = F("PortView")
    _, sbody = A.doc_of(view, V("root#screen"))
    assert sbody and S["screen"] in sbody, "PortView.prefab: the root carries no PortScreen"
    tabs = re.findall(r"^  - \{fileID: (-?\d+)\}", sbody.split("  tabs:")[1].split("  panes:")[0], re.M)
    assert tabs == [str(V(f"Tabs/{k}#tab")) for k, _ in TABS], "PortScreen.tabs must be FACTION MESSAGES then WEEKLY LEADERBOARD"
    panes = re.findall(r"^  - \{fileID: (-?\d+)\}", sbody.split("  panes:")[1].split("  messages:")[0], re.M)
    assert panes == [str(V("Messages#cg")), str(V("Leaderboard#cg"))], "PortScreen.panes must follow the tabs"
    C.wired(view, sbody, "messages", S["pane"], "PortScreen")
    C.wired(view, sbody, "leaderboard", S["board"], "PortScreen")
    for _, caption in TABS:
        assert f"  m_text: {caption}\n" in view, f"PortView: the {caption} tab is missing"

    _, pbody = A.doc_of(view, V("Messages#pane"))
    assert pbody and S["pane"] in pbody, "the Messages pane carries no FactionMessagesPane"
    for field, want in PANE_FIELDS:
        C.wired(view, pbody, field, want, "FactionMessagesPane")
    assert f"  rowPrefab: {K.ref(F('FactionMessageRow')('root#row'), asset_guid('FactionMessageRow'))}\n" in pbody, \
        "FactionMessagesPane.rowPrefab is not FactionMessageRow"
    assert f"  profileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n" in pbody, "FactionMessagesPane needs the profile icons"
    assert "  maxLength: 140\n" in pbody and "  minIntervalSeconds: 3\n" in pbody and "  minPostAge: 13\n" in pbody, \
        "the faction rules the boards state: 140 characters, 3 s apart, 13 to post"
    for key in ("Messages/Feed/Composer/Field/Border#grad", "Messages/Feed/Composer/Send/Fill#grad"):
        _, gbody = A.doc_of(view, V(key))
        assert gbody and GRAD in gbody and sel not in gbody, f"PortView: {key.split('#')[0]} must carry the sea-lit gradient (Rev 32 D)"
    _, ibody = A.doc_of(view, V("Messages/Feed/Composer/Field#input"))
    assert f"m_TargetGraphic: {{fileID: {V('Messages/Feed/Composer/Field/FocusRing#img')}}}" in ibody, \
        "the field's focus must show as the selection outline round it, not on its gradient border"
    assert "  m_RichText: 0\n" in ibody and "  m_CharacterLimit: 140\n" in ibody and "  m_ShouldActivateOnSelect: 0\n" in ibody, \
        "the field: no rich text, 140 characters, and the pad's focus alone must not open the keyboard"
    for key in ("Messages/Feed/Actions", "Messages/Feed/Toast", "Messages/Side/Muted", "Leaderboard", "Messages/Feed/Status/Retry"):
        assert "  m_Alpha: 0\n" in A.doc_of(view, V(f"{key}#cg"))[1], f"PortView: {key} must start hidden"
    feed_kids = re.findall(r"fileID: (-?\d+)", re.search(r"^  m_Children:\n((?:  - .*\n)*)", A.doc_of(view, V("Messages/Feed#rt"))[1], re.M).group(1))
    assert feed_kids.index(str(V("Messages/Feed/Composer#rt"))) < feed_kids.index(str(V("Messages/Feed/Scroll#rt"))), \
        "focus order: the field before the feed"
    assert feed_kids[-1] == str(V("Messages/Feed/Actions#rt")), "the message actions must draw over the feed (last child)"
    assert f"guid: {M.SEA_WINDOW_MAT}" in A.doc_of(view, V("Sea#img"))[1], "the Port draws the sea lines, as every window does"

    _, lbody = A.doc_of(view, V("Leaderboard#board"))
    assert lbody and S["board"] in lbody, "the Leaderboard pane carries no PortLeaderboardPane"
    for field, want in (("panel", S["lbpanel"]), ("worldTab", K.kit_script("KitTile")), ("regionalTab", K.kit_script("KitTile")),
                        ("friendsTab", K.kit_script("KitTile")), ("titleText", K.TMP_UGUI), ("countdownText", K.TMP_UGUI)):
        C.wired(view, lbody, field, want, "PortLeaderboardPane")
    lb_guid = C.prefab_guid(LEADERBOARD_VIEW)
    assert view.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {lb_guid}") == 1, "the Port must carry the weekly window's own board"
    for k in LB_OFF:
        assert re.search(r"target: \{fileID: %s, guid: %s, type: 3\}\n      propertyPath: m_IsActive\n      value: 0\n"
                         % (F(LEADERBOARD_VIEW)(k), lb_guid), view), f"the board's {k.split('#')[0]} must be off in the Port"

    sc_text = files[SCENE]
    inst, stub = scene_ids()
    assert sc_text.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid('PortView')}") == 1, \
        "Menu_Main must hold exactly one PortView instance"
    sc = T.Scene(sc_text)
    I = SCENE_IDS
    assert f"m_TransformParent: {{fileID: {I['screens_rt']}}}" in sc.docs[inst], "PortView is not under Screens"
    kids = re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[I["screens_rt"]], re.M).group(1)
    assert kids.count(f"{{fileID: {stub}}}") == 1, "Screens does not list the PortView"
    assert f"  - {{fileID: {stub}}}\n  - {{fileID: {I['old_port_rt']}}}\n" in kids, "the PortView takes the old panel's place"
    sw = sc.docs[I["switcher"]]
    assert screens_entry(stub) in sw, "ScreenSwitcher's PORT slot is not the PortView"
    assert "  LeaderboardMenu:" not in sw, "ScreenSwitcher.LeaderboardMenu was removed from the class"
    assert re.search(r"^  m_IsActive: 0\n", sc.docs[I["old_port_go"]], re.M), "the old PortScreen panel must be off"
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", sc_text, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", sc_text):
        assert m.group(1) in anchors, f"Menu_Main: dangling local fileID {m.group(1)}"


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
    view, row = prefab_path("PortView"), prefab_path("FactionMessageRow")
    inst, stub = scene_ids()
    V = F("PortView")
    sel = K.col(tok("Selection"))
    cases = [
        ("the PORT slot left on the old panel", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            screens_entry(stub), screens_entry(SCENE_IDS["old_port_rt"]), 1))),
        ("the leaderboard tab dropped", lambda f: f.__setitem__(view, f[view].replace(f"  - {{fileID: {V('Tabs/Leaderboard#tab')}}}\n", "", 1))),
        ("the send button unwired", lambda f: f.__setitem__(view, re.sub(r"(  sendButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("rich text back on the messages", lambda f: f.__setitem__(row, f[row].replace("  m_isRichText: 0\n", "  m_isRichText: 1\n", 1))),
        ("your own messages in the selection blue", lambda f: f.__setitem__(row, f[row].replace(
            f"  yoursLook:\n    borderFrom: {K.col(LOOKS['yoursLook']['borderFrom'])}", f"  yoursLook:\n    borderFrom: {sel}", 1))),
        ("your messages looking like everyone else's", lambda f: f.__setitem__(row, re.sub(
            r"(  yoursLook:\n)(?:    .*\n)+", lambda m: "  yoursLook:\n" + "".join(f"    {k}: {K.col(v)}\n" for k, v in LOOKS["othersLook"].items()),
            f[row], count=1))),
        ("the field's gradient border dropped", lambda f: f.__setitem__(view, re.sub(
            r"(--- !u!114 &%s\nMonoBehaviour:\n(?:  .*\n)*?  m_Script: \{fileID: 11500000, guid: )\w+"
            % V("Messages/Feed/Composer/Field/Border#grad"), r"\g<1>0", f[view], count=1))),
        ("the field opening the keyboard on focus", lambda f: f.__setitem__(view, f[view].replace(
            "  m_ShouldActivateOnSelect: 0\n", "  m_ShouldActivateOnSelect: 1\n", 1))),
        ("the message actions shown at rest", lambda f: f.__setitem__(view, re.sub(
            r"(--- !u!225 &%s\nCanvasGroup:\n(?:  .*\n)*?  m_Alpha: )0" % V("Messages/Feed/Actions#cg"), r"\g<1>1", f[view], count=1))),
        ("the board's BACK header left on", lambda f: f.__setitem__(view, f[view].replace(
            "propertyPath: m_IsActive\n      value: 0\n", "propertyPath: m_IsActive\n      value: 1\n", 3))),
        ("no sea behind the Port", lambda f: f.__setitem__(view, f[view].replace(f"guid: {M.SEA_WINDOW_MAT}", "guid: 0", 1))),
        ("the old panel left on", lambda f: f.__setitem__(SCENE, _old_on(f[SCENE]))),
        ("the faction rules loosened", lambda f: f.__setitem__(view, f[view].replace("  minIntervalSeconds: 3\n", "  minIntervalSeconds: 0\n", 1))),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def _old_on(text_):
    sc = T.Scene(text_)
    fid = SCENE_IDS["old_port_go"]
    sc.replace(fid, sc.docs[fid].replace("  m_IsActive: 0\n", "  m_IsActive: 1\n", 1))
    return sc.text()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print("OK: FactionMessageRow.prefab, PortView.prefab and the Menu_Main PORT slot validate")
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
