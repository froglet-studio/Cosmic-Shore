#!/usr/bin/env python3
"""
Author ITEM 14 of the style-B menu redesign - the MAELSTROM GAME UI - in place in Maelstrom.unity and
its three card prefabs (Docs/MENU_REDESIGN_B.md section 27; the approved MaelstromHub / MaelstromStats /
MaelstromSummary boards).

    The three card prefabs are REGENERATED IN PLACE - same files, same guids (Maelstrom.unity is the
    only thing that references them), new contents:
        MaelstormRoundScoreCardBG.prefab        MaelstromRoundCard, 380 x 560: the mode's card art, a
                                                "Round n" eyebrow over the mode name, Winner + a domain
                                                pill (or "Up next"), ROUND / TOTAL heads over the rows,
                                                the Control Active outline when current.
        MaelStromPlayerScoreCardContainer.prefab MaelstromPlayerCard, one 344 x 40 row: avatar in a
                                                domain ring, name over the mode-formatted score, +points,
                                                total.
        MaelstromSummaryScoreCardContainer.prefab MaelstromSummaryPlayerCard, 330 x 380: rank, avatar in
                                                a domain ring, name, domain pill, Total score.
    The hub (MaelstromConfigureModal) is restyled in place; MaelstromSceneView keeps binding by name and
    every object it names keeps its name:
        the mode eyebrow, "Round n" and the win-target line top left; the Game pool / Round / Leading
        chips top right; the round cards side by side (the scroll turns horizontal, LB / RB step it);
        the live preview window on the right with "Next round" / "Last arena", LIVE PREVIEW and the
        mode name; "Race to N" with a bar per domain; "Pilots ready" with a face per human pilot; READY /
        NEXT as kit buttons, the countdown number with its caption, a line under them, pad hints.
    The summary panel: mode eyebrow, GAME WON / GAME OVER, "RUBY WINS" in the winner's colour over a
        rule, the domain ranking as chips, the pilot cards side by side, and PLAY AGAIN (A) / STATS (Y)
        / MAIN MENU (B) as kit buttons.
    Pad: READY, NEXT and PLAY AGAIN are SELECTED by the view, so A reaches them as a submit and they
        carry no ControllerButtonPress (a pad poll as well would press twice). STATS and MAIN MENU
        answer Y and B through ControllerButtonPress - neither is a submit.

STANDING DOWN: every legacy deletion runs only while the legacy object exists; a re-run re-derives the
same output (--check reports no drift).

    python3 Tools/Build/author_maelstrom_ui.py              # write
    python3 Tools/Build/author_maelstrom_ui.py --check      # fail if any output drifted
    python3 Tools/Build/author_maelstrom_ui.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_start_flow", os.path.join(HERE, "author_start_flow.py"))
SF = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(SF)

HUD, HM, P, C, A, K, T, H, M = SF.HUD, SF.HM, SF.P, SF.C, SF.A, SF.K, SF.T, SF.H, SF.M
REPO = K.REPO
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR = HM.TL, HM.TR
Patch, set_field, tmp_restyle = HUD.Patch, HUD.set_field, HUD.tmp_restyle
reparent, remove_go, ref = SF.reparent, SF.remove_go, SF.ref

SCENE = "Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity"
PREFAB_DIR = "Assets/_Prefabs/MaelStrom"
ROUND = dict(path=f"{PREFAB_DIR}/MaelstormRoundScoreCardBG.prefab", key="MaelstromRoundCard")
ROW = dict(path=f"{PREFAB_DIR}/MaelStromPlayerScoreCardContainer.prefab", key="MaelstromPlayerRow")
SUMMARY = dict(path=f"{PREFAB_DIR}/MaelstromSummaryScoreCardContainer.prefab", key="MaelstromSummaryCard")
for _p in (ROUND, ROW, SUMMARY):
    _p["guid"] = K.meta_guid(_p["path"])

S = dict(
    view=HUD.script_guid("Assets/_Scripts/Controller/Arcade/Maelstrom/MaelstromSceneView.cs"),
    round=HUD.script_guid("Assets/_Scripts/UI/MaelstromRoundCard.cs"),
    row=HUD.script_guid("Assets/_Scripts/UI/MaelstromPlayerCard.cs"),
    summary=HUD.script_guid("Assets/_Scripts/UI/MaelstromSummaryPlayerCard.cs"),
    standing=HUD.script_guid("Assets/_Scripts/UI/MaelstromStandingRow.cs"),
    seat=HUD.script_guid("Assets/_Scripts/UI/MaelstromReadySeat.cs"),
    cover=K.kit_script("KitCoverImage"),
    kitbutton=HUD.S["kitbutton"],
    cbp=HUD.S["cbp"],
)
PAD_NORTH, PAD_EAST = 4, 5      # UnityEngine.InputSystem.LowLevel.GamepadButton

# Maelstrom.unity, measured 2026-10-15.
I = dict(
    view="1556114766", content_rt="2262851545204654307", content_cr="7987527113065835983",
    bg_go="796908773135538461", bg_rt="3170450433228708807", bg_img="6990368222030682780",
    config_rt="7821616816243440894", gameview_rt="8821170366990282102", gameview_comp="3772038590567528511",
    preview_rt="2128232029432355807", name_rt="7604117807841809459", name_tmp="7439618977816656983",
    desc_go="8705897446703158132",
    scroll_rt="1465511914", scroll_img="1465511916", scroll_go="1465511913", scroll="1465511915", viewport_rt="1449084677",
    list_go="1565475376", list_rt="1565475377", list_vlg="1565475378", list_csf="1565475379",
    title_rt="1335365945", title_tmp="1335365946", pool_rt="935799788", pool_tmp="935799789",
    round_rt="462174458", round_tmp="462174459", info_rt="169444261", info_tmp="169444262",
    lead_rt="1014342534", lead_tmp="1014342535", start_rt="412212379", start_tmp="412212380",
    old_ready_go="1067315743", old_next_go="890017672",
    sum_go="1780435132", sum_rt="1780435133", sum_list_go="2098648985", sum_list_rt="2098648986", sum_list_vlg="2098648988",
    sum_title_rt="1311070267", sum_title_tmp="1311070268", sum_info_rt="1238573816", sum_info_tmp="1238573817",
    old_rank_go="1758685025", sum_lead_go="1687345745", sum_lead_rt="1687345746", sum_lead_tmp="1687345747",
    old_stats_go="1055943587", old_again_go="1932153205", old_menu_go="2009116736",
)
DOMAINS, SEATS = 3, 4


def prefab_ref(p, key):
    return K.ref(K.FileIds(p["key"])(key), p["guid"], 3)


def pill(parent, key, r):
    """A domain pill: a tinted plate, a hex mark and the name (all painted at runtime)."""
    n = parent.child(N(key, "Pill", r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.14), K.kit_sprite("kit_sliver10_fill"), True))
    mark = n.child(N(f"{key}/Mark", "Mark", R((0, 0.5), None, (14, 0), (12, 10))))
    mark.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_hex_fill"), preserve=True))
    H.label(n.child(N(f"{key}/Name", "Name", R((0, 0), (1, 1), (9, 0), (-34, 0)))), "", "Label", "TextLight")
    return n


def avatar_ring(parent, key, r, inset):
    ring = parent.child(N(key, "Ring", r))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_oct_fill"), True))
    clip = ring.child(N(f"{key}/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-inset, -inset))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)).add("mask", 114, A.mask(False))
    av = clip.child(N(f"{key}/Clip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER))
    return ring


def text_node(parent, key, name, r, t, role, token, align="left", ellipsis=False):
    n = parent.child(N(key, name, r))
    body = H.text(t, role, tok(token), align=align)
    n.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(body) if ellipsis else body)
    return n


# --------------------------------------------------------------------------------------------
# Prefabs
# --------------------------------------------------------------------------------------------

CARD_W, CARD_H, ART_H, PADX = 380, 560, 200, 18
ROW_W, ROW_H = CARD_W - 2 * PADX, 40


def build_row():
    root = N("root", "MaelstromPlayerRow", R(size=(ROW_W, ROW_H)))
    root.add("le", 114, H.le(pw=ROW_W, ph=ROW_H))
    avatar_ring(root, "Ring", TL(0, 6, 28, 28), 4)
    text_node(root, "Name", "Name", TL(38, 2, 170, 22), "Pilot", "BodySmall", "TextLight", ellipsis=True)
    text_node(root, "Score", "RoundScore", TL(38, 23, 170, 16), "", "DataSmall", "NeutralLightest", ellipsis=True)
    text_node(root, "Points", "Points", TR(58, 9, 56, 22), "", "Data", "TextLight", align="right")
    text_node(root, "Total", "Total", TR(0, 9, 48, 22), "-", "Data", "NeutralLightest", align="right")
    root.add("card", 114, lambda ctx, go: K.mono(go, S["row"],
        f"  avatarImage: {{fileID: {ctx.c('Ring/Clip/Avatar', 'img')}}}\n  nameText: {{fileID: {ctx.c('Name', 'tmp')}}}\n"
        f"  roundScoreText: {{fileID: {ctx.c('Score', 'tmp')}}}\n  roundScoreRoot: {{fileID: 0}}\n"
        f"  pointsText: {{fileID: {ctx.c('Points', 'tmp')}}}\n"
        f"  totalScoreText: {{fileID: {ctx.c('Total', 'tmp')}}}\n  totalScoreRoot: {{fileID: 0}}\n"
        f"  domainBackground: {{fileID: {ctx.c('Ring', 'img')}}}\n"))
    return root


def build_round():
    root = N("root", "MaelstromRoundCard", R(size=(CARD_W, CARD_H)))
    root.add("le", 114, H.le(pw=CARD_W, ph=CARD_H))
    H.framed(root, "Card", 14, "SurfaceVeryDark", "SurfaceDark")
    clip = root.child(N("ArtClip", "ArtClip", R((0, 1), (1, 1), (0, -1), (-2, ART_H), (0.5, 1))))
    clip.add("rmask", 114, A.rect_mask()).add("cover", 114, lambda ctx, go: K.mono(go, S["cover"], f"  image: {{fileID: {ctx.c('ArtClip/Art', 'img')}}}\n"))
    art = clip.child(N("ArtClip/Art", "Art", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    fade = clip.child(N("ArtClip/Fade", "Fade", R((0, 0), (1, 0), (0, 60), (0, 120))))
    fade.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_fade_v"))) \
        .add("th", 114, H.themed("SurfaceVeryDark"))
    H.label(root.child(N("Eyebrow", "RoundNumber", TL(PADX, 146, 340, 18))), "Round 1", "Label", "NeutralLightest")
    text_node(root, "Mode", "RoundName", TL(PADX, 168, 340, 32), "Skim Race", "H2", "TextLight", ellipsis=True)
    H.label(root.child(N("WinnerLabel", "WinnerLabel", TL(PADX, 218, 120, 18))), "Winner", "Label", "NeutralLightest")
    win = pill(root, "Winner", TR(PADX, 214, 120, 26))
    win.add("cg", 225, H.canvas_group(False))
    H.label(root.child(N("Placeholder", "WinnerPlaceholder", TR(PADX, 218, 200, 18))), "", "Label", "NeutralLightest", align="right")
    rule = root.child(N("Rule", "Rule", TL(PADX, 250, ROW_W, 1)))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    H.label(root.child(N("HeadRound", "HeadRound", TR(PADX + 58, 260, 56, 16))), "Round", "Label", "NeutralLightest", align="right")
    H.label(root.child(N("HeadTotal", "HeadTotal", TR(PADX, 260, 48, 16))), "Total", "Label", "NeutralLightest", align="right")
    rows = root.child(N("Rows", "Rows", TL(PADX, 282, ROW_W, CARD_H - 282 - 14)))
    rows.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 2, 0, (0, 0), (0, 0)))
    hl = root.child(N("Highlight", "CurrentHighlight", R((0, 0), (1, 1), (0, 0), (2, 2)), active=False))
    hl.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"), K.kit_sprite("kit_sliver14_line2"), True)) \
        .add("th", 114, H.themed("ControlActive"))
    root.add("card", 114, lambda ctx, go: K.mono(go, S["round"],
        f"  roundNumberText: {{fileID: {ctx.c('Eyebrow', 'tmp')}}}\n  roundNameText: {{fileID: {ctx.c('Mode', 'tmp')}}}\n"
        f"  winningDomainText: {{fileID: {ctx.c('Winner/Name', 'tmp')}}}\n  currentRoundHighlight: {{fileID: {ctx.go('Highlight')}}}\n"
        f"  cardBackground: {{fileID: 0}}\n"
        f"  cardArt: {{fileID: {ctx.c('ArtClip/Art', 'img')}}}\n  upcomingArtAlpha: 0.45\n"
        f"  winnerPill: {{fileID: {ctx.c('Winner', 'cg')}}}\n  winnerPillBackground: {{fileID: {ctx.c('Winner', 'img')}}}\n"
        f"  winnerPillMark: {{fileID: {ctx.c('Winner/Mark', 'img')}}}\n  pillAlpha: 0.14\n"
        f"  winnerPlaceholderText: {{fileID: {ctx.c('Placeholder', 'tmp')}}}\n"
        f"  playerCardPrefab: {prefab_ref(ROW, 'root#card')}\n"
        f"  playerCardContainer: {{fileID: {ctx.rt('Rows')}}}\n"))
    return root


SUM_W, SUM_H = 330, 380


def build_summary():
    root = N("root", "MaelstromSummaryCard", R(size=(SUM_W, SUM_H)))
    root.add("le", 114, H.le(pw=SUM_W, ph=SUM_H))
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("th", 114, H.themed("SurfaceVeryDark"))
    glow = root.child(N("Glow", "Glow", R((0, 1), (1, 1), (0, -110), (-2, 220))))
    glow.rot = 180
    glow.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark", 0.8), K.kit_sprite("kit_fade_v")))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))
    H.label(root.child(N("Rank", "Rank", R((0.5, 1), None, (0, -34), (200, 18)))), "#1", "Label", "TextLight", align="center")
    avatar_ring(root, "Ring", R((0.5, 1), None, (0, -122), (120, 120)), 8)
    text_node(root, "Name", "Name", R((0.5, 1), None, (0, -206), (290, 32)), "Pilot", "H2", "TextLight", align="center", ellipsis=True)
    pill(root, "Pill", R((0.5, 1), None, (0, -246), (120, 26)))
    H.label(root.child(N("TotalLabel", "TotalLabel", R((0.5, 1), None, (0, -286), (200, 18)))), "Total score", "Label", "NeutralLightest", align="center")
    text_node(root, "Total", "Total", R((0.5, 1), None, (0, -326), (240, 48)), "0", "DataLarge", "TextLight", align="center")
    root.add("card", 114, lambda ctx, go: K.mono(go, S["summary"],
        f"  avatarImage: {{fileID: {ctx.c('Ring/Clip/Avatar', 'img')}}}\n  nameText: {{fileID: {ctx.c('Name', 'tmp')}}}\n"
        f"  totalScoreText: {{fileID: {ctx.c('Total', 'tmp')}}}\n  domainBackground: {{fileID: {ctx.c('Ring', 'img')}}}\n"
        f"  rankText: {{fileID: {ctx.c('Rank', 'tmp')}}}\n  frameBorder: {{fileID: {ctx.c('Border', 'img')}}}\n"
        f"  pillBackground: {{fileID: {ctx.c('Pill', 'img')}}}\n  pillMark: {{fileID: {ctx.c('Pill/Mark', 'img')}}}\n"
        f"  domainText: {{fileID: {ctx.c('Pill/Name', 'tmp')}}}\n  pillAlpha: 0.14\n"))
    return root


def prefab_outputs():
    return {ROW["path"]: P.prefab_text(build_row(), ROW["key"]),
            ROUND["path"]: P.prefab_text(build_round(), ROUND["key"]),
            SUMMARY["path"]: P.prefab_text(build_summary(), SUMMARY["key"])}


# --------------------------------------------------------------------------------------------
# The hub
# --------------------------------------------------------------------------------------------

def kit_button(k, name, x_or_r, w, variant, opaque, caption, cap, pad=None, active=True):
    """A KitButton; `pad` None leaves out ControllerButtonPress (the button is selected, A submits)."""
    r = x_or_r if isinstance(x_or_r, dict) else None
    h = K.line_height("Button") + 28
    n = HUD.kit_button(k, name, r or R(size=(w, h)), variant, opaque, caption, cap, pad or 1)
    if pad is None:
        n.comps = [c for c in n.comps if c[0] != "cbp"]
    n.add("le", 114, H.le(pw=w, ph=h))
    n.active = active
    return n


def chip(key, name):
    n = N(key, name, R(size=(10, 40)))
    n.add("hlg", 114, K.hv_layout(False, (16, 16, 0, 0), 10, 3, (1, 1), (0, 0))).add("le", 114, H.le(ph=40))
    H.framed(n, key, 10, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.9)
    return n


def chip_label(n, key, t):
    lab = n.child(N(f"{key}/Label", "Label", R(size=(10, 20))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text(t, "Label", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest"))
    return lab


def chips_node():
    row = N("Chips", "Chips", R((1, 1), None, (-60, -44), (10, 40), (1, 1)))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 16, 5, (1, 1), (0, 0))).add("csf", 114, K.fitter(2, 0)) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Drop", distance=10))
    pool = row.child(chip("Chips/Pool", "PoolChip"))
    chip_label(pool, "Chips/Pool", "Game pool")
    row.child(chip("Chips/Round", "RoundChip"))
    lead = row.child(chip("Chips/Lead", "LeadingChip"))
    chip_label(lead, "Chips/Lead", "Leading")
    lp = lead.child(N("Chips/Lead/Pill", "Pill", R(size=(10, 26))))
    lp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.14), K.kit_sprite("kit_sliver10_fill"), True)) \
        .add("hlg", 114, K.hv_layout(False, (10, 10, 0, 0), 8, 3, (1, 1), (0, 0))).add("le", 114, H.le(ph=26))
    mark = lp.child(N("Chips/Lead/Pill/Mark", "Mark", R(size=(12, 10))))
    mark.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_hex_fill"), preserve=True)) \
        .add("le", 114, H.le(pw=12, ph=10))
    return row


def standing_row(parent, key, y, with_bar):
    if with_bar:
        n = parent.child(N(key, f"Row{key[-1]}", TL(0, y, 700, 30)))
    else:
        n = parent.child(N(key, f"Chip{key[-1]}", R(size=(140, 76))))
        n.add("le", 114, H.le(pw=140, ph=76))
    n.add("cg", 225, H.canvas_group(True))
    pill(n, f"{key}/Pill", TL(0, 2, 110, 26) if with_bar else R((0.5, 1), None, (0, -13), (120, 26)))
    if with_bar:
        track = n.child(N(f"{key}/Track", "Track", TL(126, 12, 470, 6)))
        track.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
        fill = track.child(N(f"{key}/Track/Fill", "Fill", R((0, 0), (0.5, 1), (0, 0), (0, 0), (0, 0.5))))
        fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE)).add("in", 114, M.entrance("GrowX", delay=0.2))
        tick = track.child(N(f"{key}/Track/Target", "Target", R((1, 0.5), None, (0, 0), (2, 18))))
        tick.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"))).add("th", 114, H.themed("NeutralLightest"))
        text_node(n, f"{key}/Value", "Value", TR(0, 4, 90, 22), "0 / 12", "Data", "TextLight", align="right")
    else:
        text_node(n, f"{key}/Value", "Value", R((0.5, 1), None, (0, -56), (140, 36)), "0", "H2", "TextLight", align="center")
    n.add("row", 114, lambda ctx, go: K.mono(go, S["standing"],
        f"  group: {{fileID: {ctx.c(key, 'cg')}}}\n"
        f"  pillBackground: {{fileID: {ctx.c(f'{key}/Pill', 'img')}}}\n  pillMark: {{fileID: {ctx.c(f'{key}/Pill/Mark', 'img')}}}\n"
        f"  domainText: {{fileID: {ctx.c(f'{key}/Pill/Name', 'tmp')}}}\n"
        f"  fill: {{fileID: {ctx.rt(f'{key}/Track/Fill') if with_bar else 0}}}\n"
        f"  fillImage: {{fileID: {ctx.c(f'{key}/Track/Fill', 'img') if with_bar else 0}}}\n"
        f"  valueText: {{fileID: {ctx.c(f'{key}/Value', 'tmp')}}}\n  pillAlpha: 0.14\n"))
    return n


def standings_node():
    n = N("Standings", "Standings", TL(60, 800, 700, 140))
    n.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", delay=0.12))
    H.label(n.child(N("Standings/Label", "Label", TL(0, 0, 400, 18))), "Race to 12", "Label", "NeutralLightest")
    for i in range(DOMAINS):
        standing_row(n, f"Standings/Row{i}", 28 + i * 36, True)
    return n


def seat_node(parent, i):
    k = f"Pilots/Seat{i}"
    n = parent.child(N(k, f"Seat{i}", R(size=(130, 100))))
    n.add("le", 114, H.le(pw=130, ph=100)).add("cg", 225, H.canvas_group(False))
    avatar_ring(n, f"{k}/Ring", R((0.5, 1), None, (0, -28), (56, 56)), 6)
    text_node(n, f"{k}/Name", "Name", R((0.5, 1), None, (0, -68), (130, 18)), "Pilot", "BodySmall", "TextLight", align="center", ellipsis=True)
    H.label(n.child(N(f"{k}/State", "State", R((0.5, 1), None, (0, -88), (130, 14)))), "Waiting", "Label", "NeutralLightest", align="center")
    n.add("seat", 114, lambda ctx, go: K.mono(go, S["seat"],
        f"  group: {{fileID: {ctx.c(k, 'cg')}}}\n  ring: {{fileID: {ctx.c(f'{k}/Ring', 'img')}}}\n"
        f"  avatar: {{fileID: {ctx.c(f'{k}/Ring/Clip/Avatar', 'img')}}}\n  nameText: {{fileID: {ctx.c(f'{k}/Name', 'tmp')}}}\n"
        f"  stateText: {{fileID: {ctx.c(f'{k}/State', 'tmp')}}}\n  theme: {K.ref(11400000, K.THEME, 2)}\n  waitingAlpha: 0.6\n"))


def pilots_node():
    n = N("Pilots", "Pilots", TL(1280, 560, 580, 130))
    n.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", delay=0.16))
    H.label(n.child(N("Pilots/Label", "Label", TL(0, 0, 300, 18))), "Pilots ready", "Label", "NeutralLightest")
    seats = n.child(N("Pilots/Seats", "Seats", TL(0, 28, 580, 100)))
    seats.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 12, 0, (0, 0), (0, 0)))
    for i in range(SEATS):
        seat_node(seats, i)
    return n


def preview_parts():
    """Over the preview, inside GameView: the bottom fade, the frame, LIVE PREVIEW and the eyebrow."""
    fade = N("Preview/Fade", "Fade", R((0, 0), (1, 0), (0, 90), (0, 180)))
    fade.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.9), K.kit_sprite("kit_fade_v"))) \
        .add("th", 114, H.themed("SurfaceBlack", 0.9))
    frame = N("Preview/Frame", "Frame", STRETCH)
    frame.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True)) \
        .add("th", 114, H.themed("SurfaceDark"))
    M.bracket_nodes(frame, "Preview/Frame/Bracket", inset=-6)
    lw = K.label_width("LIVE PREVIEW", "Label")
    live = N("Preview/Live", "LivePreview", R((1, 1), None, (-18, -16), (lw + 22, 18), (1, 1)))
    H.label(live.child(N("Preview/Live/Text", "Text", R((1, 0.5), None, (0, 0), (lw, 18), (1, 0.5)))), "Live preview", "Label", "NeutralLightest", align="right")
    dot = live.child(N("Preview/Live/Dot", "Dot", R((0, 0.5), None, (4, 0), (8, 8))))
    dot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Danger"), K.kit_sprite("kit_oct_fill"), preserve=True)) \
        .add("th", 114, H.themed("Danger")).add("breathe", 114, M.ambient("Breathe", period=1.2, amplitude=0.6))
    eyebrow = N("Preview/Eyebrow", "PreviewEyebrow", R((0, 0), None, (22, 62), (400, 18), (0, 0)))
    H.label(eyebrow, "Next round", "Label", "Cta")
    return [fade, frame, live, eyebrow]


def hub_text(p):
    for go in (I["old_ready_go"], I["old_next_go"]):
        remove_go(p, go)
    p.rect(I["bg_rt"], STRETCH)
    p.body(I["bg_img"], HM.img_body(tok("SurfaceBlack", 0.55)), I["bg_go"])

    # Title block, top left.
    p.rect(I["title_rt"], TL(60, 66, 900, 58))
    p.replace(I["title_tmp"], tmp_restyle(p.doc(I["title_tmp"]), "Aldrich", 48, False, 0, "left", "TextLight"))
    p.rect(I["info_rt"], TL(60, 128, 900, 26))
    p.replace(I["info_tmp"], tmp_restyle(p.doc(I["info_tmp"]), "Aldrich", 18, False, 0, "left", "NeutralLightest"))

    # The round cards, side by side.
    p.rect(I["scroll_rt"], TL(60, 200, 1180, 560))
    p.body(I["scroll_img"], HM.img_body((1.0, 1.0, 1.0, 0.0), raycast=True), I["scroll_go"])
    sr = p.doc(I["scroll"])
    sr = C.field_set(C.field_set(sr, "m_Horizontal", "1"), "m_Vertical", "0")
    p.replace(I["scroll"], C.field_set(sr, "m_MovementType", "2"))
    p.rect(I["viewport_rt"], STRETCH)
    p.rect(I["list_rt"], R((0, 0), (0, 1), (0, 0), (0, 0), (0, 0.5)))
    p.body(I["list_vlg"], K.hv_layout(False, (0, 0, 0, 0), 20, 3, (0, 0), (0, 0)), I["list_go"])
    p.body(I["list_csf"], K.fitter(2, 0), I["list_go"])

    # The preview window, right.
    p.rect(I["gameview_rt"], TL(1280, 200, 580, 330))
    p.rect(I["preview_rt"], STRETCH)
    p.rect(I["name_rt"], R((0, 0), None, (22, 18), (536, 40), (0, 0)))
    p.replace(I["name_tmp"], tmp_restyle(p.doc(I["name_tmp"]), "Aldrich", 30, False, 0, "left", "TextLight"))
    SF.set_active(p, I["desc_go"], False)
    parts = preview_parts()
    gv = I["gameview_rt"]
    SF.emit_under(p, gv, parts, I["gameview_comp"],
                  lambda rts: [I["preview_rt"]] + rts + [k for k in p.kids(gv) if k not in rts and k != I["preview_rt"]])

    # New blocks on Content.
    eyebrow = N("ModeEyebrow", "ModeEyebrow", TL(60, 44, 800, 20))
    H.label(eyebrow, "Maelstrom", "Label", "Cta")
    caption = N("Caption", "Caption", TL(1280, 876, 580, 22))
    H.label(caption, "", "BodySmall", "NeutralLightest")
    hints = N("KeyHints", "KeyHints", TL(60, 1030, 900, 20))
    H.label(hints, "", "Label", "NeutralLightest")
    ready = kit_button("Hub/Ready", "ReadyButton", TL(1280, 800, 300, 60), 300, "Play", True, "Ready 4/4", "A")
    nxt = kit_button("Hub/Next", "NextButton", TL(1280, 800, 260, 60), 260, "Play", True, "Next", "A", active=False)
    new = [eyebrow, chips_node(), standings_node(), pilots_node(), ready, nxt, caption, hints]
    added = SF.emit_under(p, I["content_rt"], new, I["content_cr"], lambda rts: [k for k in p.kids(I["content_rt"]) if k not in rts] + rts)

    # Into the chips: pool, round and leading values.
    pool_rt, round_rt, lead_rt = str(p.ctx.rt("Chips/Pool")), str(p.ctx.rt("Chips/Round")), str(p.ctx.rt("Chips/Lead/Pill"))
    for rt, tmp, parent, face, size, upper, spacing in ((I["pool_rt"], I["pool_tmp"], pool_rt, "Aldrich", 15, False, 0),
                                                        (I["round_rt"], I["round_tmp"], round_rt, "Chakra", 14, True, 14),
                                                        (I["lead_rt"], I["lead_tmp"], lead_rt, "Chakra", 14, True, 10)):
        reparent(p, rt, parent)
        p.rect(rt, R(size=(10, 20)))
        d = tmp_restyle(p.doc(tmp), face, size, upper, spacing, "left", "TextLight")
        p.replace(tmp, C.field_set(d, "m_TextWrappingMode", "0"))
        p.children(parent, [k for k in p.kids(parent) if k != rt] + [rt])

    # The countdown: the number, with its caption as a child so they show and hide together.
    p.rect(I["start_rt"], TL(1610, 822, 220, 56))
    p.replace(I["start_tmp"], tmp_restyle(p.doc(I["start_tmp"]), "Aldrich", 48, False, 0, "left", "Cta"))
    cap = N("Countdown/Caption", "Caption", R((0, 1), None, (0, 6), (220, 18), (0, 0)))
    H.label(cap, "Game starts in", "Label", "NeutralLightest")
    SF.emit_under(p, I["start_rt"], [cap], I["start_tmp"], lambda rts: rts)
    return added


# --------------------------------------------------------------------------------------------
# The summary
# --------------------------------------------------------------------------------------------

def summary_text(p):
    for go in (I["old_rank_go"], I["old_stats_go"], I["old_again_go"], I["old_menu_go"]):
        remove_go(p, go)
    dim = N("Summary/Dim", "Dim", STRETCH)
    dim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.82), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", 0.82)).add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Fade", duration=0.2))
    rule = N("Summary/Rule", "WinnerRule", R((0.5, 1), None, (0, -252), (420, 2), (0.5, 1)))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"))).add("in", 114, M.entrance("GrowX", delay=0.35))
    rank = N("Summary/Ranking", "Ranking", R((0.5, 1), None, (0, -290), (900, 80), (0.5, 1)))
    rank.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 40, 1, (0, 0), (0, 0))) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", delay=0.45))
    for i in range(DOMAINS):
        standing_row(rank, f"Summary/Ranking/Chip{i}", 0, False)
    buttons = N("Summary/Buttons", "Buttons", R((0.5, 0), None, (0, 80), (1000, 64)))
    buttons.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 18, 4, (0, 0), (0, 0))) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", delay=0.6))
    buttons.child(kit_button("Sum/Again", "PlayAgain Button", None, 280, "Play", True, "Play again", "A"))
    buttons.child(kit_button("Sum/Stats", "StatsScreenButton", None, 200, "Default", False, "Stats", "Y", pad=PAD_NORTH))
    buttons.child(kit_button("Sum/Menu", "Main Menu Button", None, 260, "Cancel", False, "Main menu", "B", pad=PAD_EAST))
    added = SF.emit_under(p, I["sum_rt"], [dim, rule, rank, buttons], I["sum_rt"],
                          lambda rts: [rts[0]] + [k for k in p.kids(I["sum_rt"]) if k not in rts] + rts[1:])

    p.rect(I["sum_title_rt"], R((0.5, 1), None, (0, -70), (800, 20), (0.5, 1)))
    p.replace(I["sum_title_tmp"], tmp_restyle(p.doc(I["sum_title_tmp"]), "Chakra", 14, True, 14, "center", "Cta"))
    p.rect(I["sum_info_rt"], R((0.5, 1), None, (0, -96), (800, 40), (0.5, 1)))
    p.replace(I["sum_info_tmp"], tmp_restyle(p.doc(I["sum_info_tmp"]), "Aldrich", 30, False, 0, "center", "NeutralLightest"))
    p.rect(I["sum_lead_rt"], R((0.5, 1), None, (0, -140), (1400, 100), (0.5, 1)))
    p.replace(I["sum_lead_tmp"], tmp_restyle(p.doc(I["sum_lead_tmp"]), "Aldrich", 84, False, 0, "center", "TextLight"))
    p.add_comp(I["sum_lead_go"], "cg", 225, H.canvas_group(True))
    p.add_comp(I["sum_lead_go"], "in", 114, M.entrance("Pop", delay=0.15))
    p.rect(I["sum_list_rt"], R((0.5, 1), None, (0, -400), (1500, 380), (0.5, 1)))
    p.body(I["sum_list_vlg"], K.hv_layout(False, (0, 0, 0, 0), 30, 1, (0, 0), (0, 0)), I["sum_list_go"])
    return added


# --------------------------------------------------------------------------------------------
# Wiring
# --------------------------------------------------------------------------------------------

def set_list(doc, name, ids):
    doc = re.sub(r"^  %s:\n(?:  - .*\n)*|^  %s: \[\]\n" % (name, name), "", doc, flags=re.M)
    return doc.rstrip("\n") + "\n" + K.refs_list(name, ids)


def scene_text(text_):
    p = Patch(text_, "Maelstrom/StyleB")
    assert p.sc.script(I["view"]) == S["view"], "MaelstromSceneView moved"
    assert p.sc.field(p.sc.go_of(I["scroll"]), "m_Name") == "MaelstormSummaryScrollView", "the round scroll moved"
    assert p.sc.field(I["sum_go"], "m_Name") == "Summary Panel", "the summary panel moved"
    hub_text(p)
    summary_text(p)
    c, ctx = p.ctx.c, p.ctx
    d = p.doc(I["view"])
    for f, v in (("readyButton", ref(c("Hub/Ready", "button"))), ("readyButtonLabel", ref(c("Hub/Ready/Label", "tmp"))),
                 ("nextButton", ref(c("Hub/Next", "button"))),
                 ("statsScreenButton", ref(c("Sum/Stats", "button"))), ("playAgainButton", ref(c("Sum/Again", "button"))),
                 ("mainMenuButton", ref(c("Sum/Menu", "button"))), ("summaryRankText", "{fileID: 0}"),
                 ("roundCardPrefab", prefab_ref(ROUND, "root#card")), ("summaryCardPrefab", prefab_ref(SUMMARY, "root#card")),
                 ("modeEyebrowText", ref(c("ModeEyebrow", "tmp"))),
                 ("leadingPillBackground", ref(c("Chips/Lead/Pill", "img"))), ("leadingPillMark", ref(c("Chips/Lead/Pill/Mark", "img"))),
                 ("standingsLabelText", ref(c("Standings/Label", "tmp"))), ("readySeatsGroup", ref(c("Pilots", "cg"))),
                 ("previewEyebrowText", ref(c("Preview/Eyebrow", "tmp"))), ("captionText", ref(c("Caption", "tmp"))),
                 ("keyHintText", ref(c("KeyHints", "tmp"))), ("summaryWinnerRule", ref(c("Summary/Rule", "img")))):
        d = set_field(d, f, v)
    d = set_list(d, "standingRows", [c(f"Standings/Row{i}", "row") for i in range(DOMAINS)])
    d = set_list(d, "readySeats", [c(f"Pilots/Seat{i}", "seat") for i in range(SEATS)])
    d = set_list(d, "rankChips", [c(f"Summary/Ranking/Chip{i}", "row") for i in range(DOMAINS)])
    p.replace(I["view"], d)
    return p.text()


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_text(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

field_ref = SF.field_ref


def check_local(path, text_):
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", text_, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", text_):
        assert m.group(1) in anchors, f"{path}: dangling local fileID {m.group(1)}"


def check_prefab_ref(files, value, prefab, what):
    m = re.search(r"\{fileID: (-?\d+), guid: (\w+), type: 3\}", value)
    assert m and m.group(2) == prefab["guid"], f"{what} is not {prefab['path']}"
    assert f"&{m.group(1)}\n" in files[prefab["path"]], f"{what} names a fileID {prefab['path']} does not have"


def validate(files):
    M.require_entrances(files, {SCENE: 6})
    for pf in (ROUND, ROW, SUMMARY):
        check_local(pf["path"], files[pf["path"]])
    rd = files[ROUND["path"]]
    card = next(d for d in T.Scene(rd).docs.values() if f"guid: {S['round']}" in d)
    for f in ("roundNumberText", "roundNameText", "winningDomainText", "currentRoundHighlight", "cardArt", "winnerPill",
              "winnerPlaceholderText", "playerCardContainer"):
        assert field_ref(card, f) != "0", f"MaelstromRoundCard.{f} is not wired"
    check_prefab_ref(files, re.search(r"  playerCardPrefab: (.*)", card).group(1), ROW, "MaelstromRoundCard.playerCardPrefab")
    hl = field_ref(card, "currentRoundHighlight")
    assert re.search(r"&%s\nGameObject:\n(?:(?!--- ).*\n)*?  m_IsActive: 0" % hl, rd), "the round card's outline ships lit"
    sm = files[SUMMARY["path"]]
    for f in ("rankText", "frameBorder", "domainText", "totalScoreText"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, sm, re.M), f"MaelstromSummaryPlayerCard.{f} is not wired"
    assert re.search(r"^  pointsText: \{fileID: [1-9-]", files[ROW["path"]], re.M), "MaelstromPlayerCard.pointsText is not wired"

    check_local(SCENE, files[SCENE])
    sc = T.Scene(files[SCENE])
    for go in (I["old_ready_go"], I["old_next_go"], I["old_rank_go"], I["old_stats_go"], I["old_again_go"], I["old_menu_go"]):
        assert not sc.has(go), f"a legacy {go} survives"
    view = sc.docs[I["view"]]
    check_prefab_ref(files, re.search(r"  roundCardPrefab: (.*)", view).group(1), ROUND, "MaelstromSceneView.roundCardPrefab")
    check_prefab_ref(files, re.search(r"  summaryCardPrefab: (.*)", view).group(1), SUMMARY, "MaelstromSceneView.summaryCardPrefab")
    for f in ("readyButton", "readyButtonLabel", "nextButton", "statsScreenButton", "playAgainButton", "mainMenuButton",
              "modeEyebrowText", "leadingPillBackground", "standingsLabelText", "readySeatsGroup", "previewEyebrowText",
              "captionText", "keyHintText", "summaryWinnerRule"):
        assert field_ref(view, f) != "0" and sc.has(field_ref(view, f)), f"MaelstromSceneView.{f} is not wired"
    for f, n in (("standingRows", DOMAINS), ("readySeats", SEATS), ("rankChips", DOMAINS)):
        got = re.search(r"^  %s:\n((?:  - .*\n)*)" % f, view, re.M)
        assert got and got.group(1).count("{fileID: ") == n and "{fileID: 0}" not in got.group(1), f"MaelstromSceneView.{f} needs {n} entries"
    sr = sc.docs[I["scroll"]]
    assert "  m_Horizontal: 1\n" in sr and "  m_Vertical: 0\n" in sr, "the round scroll is not horizontal (LB / RB and the auto-scroll read it)"
    assert f"guid: {K.HLAYOUT}" in sc.docs[I["list_vlg"]], "the round list does not lay cards side by side"

    def button_go(field):
        return sc.go_of(field_ref(view, field))

    def has_cbp(go, pad=None):
        for comp in sc.components(go):
            dd = sc.docs.get(comp, "")
            if f"guid: {S['cbp']}" in dd and (pad is None or f"  activationButton: {pad}\n" in dd):
                return True
        return False
    for f in ("readyButton", "nextButton", "playAgainButton"):
        assert f"guid: {S['kitbutton']}" in sc.docs[field_ref(view, f)], f"{f} is not a kit button"
        assert not has_cbp(button_go(f)), f"{f} is selected AND polls the pad (A would press it twice)"
    assert has_cbp(button_go("statsScreenButton"), PAD_NORTH), "STATS does not answer Y"
    assert has_cbp(button_go("mainMenuButton"), PAD_EAST), "MAIN MENU does not answer B"
    assert sc.field(button_go("nextButton"), "m_Name") == "NextButton" and sc.field(button_go("readyButton"), "m_Name") == "ReadyButton", \
        "READY / NEXT lost the names MaelstromSceneView.AutoBind finds them by"


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

    def sub(path, pat, repl):
        return lambda f: f.__setitem__(path, re.sub(pat, repl, f[path], count=1, flags=re.M))

    def again_polls(f):
        sc = T.Scene(f[SCENE])
        go = sc.go_of(field_ref(sc.docs[I["view"]], "playAgainButton"))
        stats = sc.go_of(field_ref(sc.docs[I["view"]], "statsScreenButton"))
        cbp = next(c for c in sc.components(stats) if f"guid: {S['cbp']}" in sc.docs.get(c, ""))
        hlg = next(c for c in sc.components(go) if f"guid: {K.HLAYOUT}" in sc.docs.get(c, ""))
        f[SCENE] = f[SCENE].replace(sc.docs[hlg], sc.docs[hlg].replace(f"guid: {K.HLAYOUT}", f"guid: {S['cbp']}"), 1)

    cases = [
        ("the hub's motion stripped", lambda f: f.__setitem__(SCENE, M.strip_entrances(f[SCENE]))),
        ("the standings unwired", sub(SCENE, r"^(  standingRows:\n  - )\{fileID: -?\d+\}", r"\1{fileID: 0}")),
        ("PLAY AGAIN also polls the pad", again_polls),
        ("the round scroll left vertical", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            "  m_Horizontal: 1\n  m_Vertical: 0\n", "  m_Horizontal: 0\n  m_Vertical: 1\n", 1))),
        ("the round card prefab reference dangling", sub(SCENE, r"(  roundCardPrefab: \{fileID: )-?\d+", r"\g<1>12345")),
        ("the row prefab reference dangling", sub(ROUND["path"], r"(  playerCardPrefab: \{fileID: )-?\d+", r"\g<1>12345")),
        ("the round outline ships lit", lambda f: f.__setitem__(ROUND["path"], re.sub(
            r"(  m_Name: CurrentHighlight\n(?:.*\n){4}  m_IsActive: )0", r"\g<1>1", f[ROUND["path"]], count=1))),
        ("the summary card's rank unwired", sub(SUMMARY["path"], r"^(  rankText: )\{fileID: -?\d+\}", r"\1{fileID: 0}")),
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
    print("OK: the Maelstrom game UI validates (hub, stats, summary and the three card prefabs)")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
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
