#!/usr/bin/env python3
"""Rev 44 boards: the Voyage window again, four new ways (G-J), on the menu's sea lines.

Shombith, 2026-11-02, after Rev 43: Episodes, the Saga and the quest tracker are three separate things, so the window
has FOUR tabs - Voyages (opens first), Saga, Quests, Episodes (locked, coming soon) - on LB / RB; the boards get the
sea lines behind them; four more options. The map and the edge view stay opaque on the left; the voyage card keeps
its three levels (the voyage with Your Ark, the 12 Ark crew settings, the optional story).

    python3 Docs/MenuRedesignB/rounds/r44_voyage_window.py <root> 44

<root> is the scratch folder `design_board.py plan` wrote. The pieces shared with Rev 43 (nav, map, card blocks, the
crew settings, the content) come from r43_voyage_window.py; the sea lines are the approved layer from R40E.dc.html.
"""
import importlib.util
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])

# r43 reads its root and round from argv at import; give it ours, then use its pieces.
sys.argv = [sys.argv[0], sys.argv[1], str(REV)]
_spec = importlib.util.spec_from_file_location("r43", HERE / "r43_voyage_window.py")
r43 = importlib.util.module_from_spec(_spec)
sys.modules["r43"] = r43
_spec.loader.exec_module(r43)
from r43 import (CTA, INACT, LIGHT, LOCK, NEUT, NEUT_D, NEUT_L, PILOT_ART, QUESTS, QUEST_ART, SEATS, SUB, TANDAVA,  # noqa: E402
                 V, VDARK, HULL, PRESETS, DOM, OTHERS, e, kc, nav, mapview, head_block, crew_chip, stats_row, sea_bar,
                 waypoints, preset_rows, actions, setting, seat_card, seat_row, crew_head, preset_tabs, story_blocks,
                 story_head, level_crumbs)

# The approved sea lines (R40E): the ground gradient, the glow and three slow layers of lines, masked top and bottom.
_src = (HERE.parent / "board" / "R40E.dc.html").read_text(encoding="utf-8")
_i = _src.index('<div style="position:absolute;inset:0;background:linear-gradient(180deg,rgba(8,22,84,.95)')
_j = _src.index('<div style="position:absolute;inset:0;-webkit-mask-image', _i)
_depth, _k = 0, _j
for _m in re.finditer(r"<(/?)div\b", _src[_j:]):
    _depth += -1 if _m.group(1) else 1
    if _depth == 0:
        _k = _j + _m.end() + 1
        break
SEA = _src[_i:_k]
assert SEA.count("<polyline") > 10 and SEA.endswith("</div>"), "the sea layer moved in R40E.dc.html"

HEAD = r43.HEAD.replace("</style>", ".pane{{background:rgba(6,10,38,.86)}}\n"
                        "@keyframes r33Sea{{from{{transform:translateX(0)}}to{{transform:translateX(-480px)}}}}\n</style>")
GLASS = "rgba(6,10,38,.86)"


def board(title, body, w=1920, h=1080):
    root = (f'<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: {VDARK}; color: {LIGHT}; '
            f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n"
            f'<div style="position:absolute;left:0;top:0;width:{w}px;height:{h}px;overflow:hidden">{SEA}</div>\n{body}\n</div>\n')
    return HEAD.format(title=e(title)) + root + r43.TAIL.format(w=w, h=h)


# ------------------------------------------------------------------------------------------------ the four tabs

TABS = ["Voyages", "Saga", "Quests", "Episodes"]
COUNT = {"Voyages": "4 found", "Saga": "Part 1 of 2", "Quests": "0 of 5", "Episodes": "Coming soon"}


def tab_label(t, on, size=17):
    col = LIGHT if on else (INACT if t == "Episodes" else NEUT_L)
    lock = LOCK if t == "Episodes" else ""
    return f'<span class="cap" style="display:flex;align-items:center;gap:7px;font-size:{size}px;color:{col}">{lock}{t}</span>'


def back():
    return (f'<button class="pick" type="button" style="display:flex;align-items:center;gap:10px">{kc("B")}'
            f'<span class="cap" style="font-size:14px;color:{NEUT_L}">Back</span></button>')


def tabrow(style, active):
    if style == "G":   # left-aligned, the Ark's underline row
        tabs = "".join(f'<button class="tab" type="button" style="position:relative">{tab_label(t, t == active)}'
                       + (f'<span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{LIGHT}"></span>' if t == active else "")
                       + '</button>' for t in TABS)
        return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:56px;display:flex;align-items:center;gap:34px;'
                f'border-bottom:1px solid {NEUT_D};z-index:4">{back()}<span style="width:1px;height:22px;background:{NEUT_D}"></span>'
                f'<span style="font-size:24px">Voyage</span><span style="width:12px"></span>{kc("LB")}{tabs}{kc("RB")}</div>')
    if style == "H":   # centred, a thin box on the open tab
        tabs = "".join(f'<button class="tab sls" type="button" style="padding:8px 20px;'
                       + (f'box-shadow:inset 0 0 0 1px {LIGHT};background:rgba(230,233,255,.06)' if t == active else "")
                       + f'">{tab_label(t, t == active)}</button>' for t in TABS)
        return (f'<div class="abs" style="left:40px;top:108px;z-index:4">{back()}</div>'
                f'<div class="abs" style="left:0;right:0;top:100px;height:48px;display:flex;align-items:center;justify-content:center;gap:12px;z-index:4">'
                f'{kc("LB")}{tabs}{kc("RB")}</div>')
    if style == "I":   # each tab says what is in it
        tabs = "".join(f'<button class="tab" type="button" style="display:flex;flex-direction:column;gap:3px;height:56px;justify-content:center;'
                       + (f'box-shadow:inset 0 -2px 0 {LIGHT}' if t == active else "") + f'">{tab_label(t, t == active)}'
                       f'<span class="lab num" style="font-size:11px;color:{SUB if t == active else INACT}">{COUNT[t]}</span></button>' for t in TABS)
        return (f'<div class="abs" style="left:40px;right:40px;top:94px;height:62px;display:flex;align-items:center;gap:36px;'
                f'border-bottom:1px solid {NEUT_D};z-index:4">{back()}<span style="flex:1"></span>{kc("LB")}{tabs}{kc("RB")}</div>')
    # J: the open tab is the screen's title, the others follow it small
    tabs = "".join(f'<button class="tab" type="button" style="display:flex;align-items:center;gap:8px;font-family:Aldrich;letter-spacing:.04em;'
                   f'font-size:{34 if t == active else 19}px;color:{LIGHT if t == active else (INACT if t == "Episodes" else NEUT_L)}">'
                   f'{LOCK if t == "Episodes" else ""}{t}</button>' for t in TABS)
    return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:60px;display:flex;align-items:flex-end;gap:28px;z-index:4">'
            f'{back()}<span style="width:8px"></span>{kc("LB")}{tabs}{kc("RB")}</div>')


# ------------------------------------------------------------------------------------------------ the frames each option draws in

def frame(style, x, y, w, h, inner, label="Voyage card", pad="26px 28px", gap=16):
    if style == "G":   # a card held in a hand: the other voyages' cards stand behind it
        behind = "".join(f'<div class="sl" style="position:absolute;left:{x + 18 * (3 - k)}px;top:{y - 12 * (3 - k)}px;width:{w}px;height:{h}px;'
                         f'background:{GLASS};box-shadow:inset 0 0 0 1px {NEUT_D}"></div>' for k in range(3) if k < 3)
        return (behind + f'<section aria-label="{label}" class="sl" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;box-sizing:border-box;'
                f'padding:{pad};background:#080C2E;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:{gap}px">{inner}</section>')
    if style == "H":   # a logbook page: ruled, a margin rule, square corners
        return (f'<section aria-label="{label}" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;box-sizing:border-box;'
                f'padding:{pad};padding-left:76px;background-color:{GLASS};background-image:repeating-linear-gradient(180deg,transparent 0,transparent 33px,rgba(67,76,137,.22) 33px,rgba(67,76,137,.22) 34px);'
                f'box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:{gap}px">'
                f'<span class="abs" style="left:56px;top:0;bottom:0;width:1px;background:rgba(116,123,173,.45)"></span>{inner}</section>')
    if style == "I":   # a console panel on glass, a header band
        return (f'<section aria-label="{label}" class="sl" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;box-sizing:border-box;'
                f'padding:{pad};background:{GLASS};box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:{gap}px">'
                f'<span class="abs" style="left:0;right:0;top:0;height:4px;background:{NEUT}"></span>{inner}</section>')
    # J: no box at all - the content stands on the sea, a thin rule on its left
    return (f'<section aria-label="{label}" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;box-sizing:border-box;'
            f'padding:6px 0 0 32px;border-left:1px solid {NEUT};display:flex;flex-direction:column;gap:{gap}px">{inner}</section>')


MAP_Y, MAP_H, EDGE_H = 172, 640, 216


def left_map(w=1080, strip=True, pos="38% 45%"):
    return mapview(40, MAP_Y, w, MAP_H, EDGE_H, pos, list_strip=strip)


# ------------------------------------------------------------------------------------------------ G - Crew's hand

def crew_back(level):
    side = "Ark crew" if level == 2 else "Story"
    return (f'<div style="display:flex;justify-content:space-between;align-items:center"><span class="lab">Back of the card - {side}</span>'
            f'<span style="display:flex;align-items:center;gap:8px">{kc("Y")}<span class="cap" style="font-size:12px;color:{NEUT_L}">Turn over</span></span></div>')


def screen_G(level):
    """G - Crew's hand: each voyage is a card a crew member hands you; the open one is in front, the others behind it.
    Level 2 and 3 are the BACK of the same card, turned over."""
    body = [nav(), tabrow("G", "Voyages"), left_map(1060)]
    x, y, w, h = 1180, 196, 660, 844
    if level == 1:
        hand = (f'<div style="display:flex;align-items:center;justify-content:space-between"><span class="lab">From your crew - card 2 of 4</span>'
                f'<span style="display:flex;gap:6px">{kc("LT")}{kc("RT")}</span></div>')
        inner = (f'{hand}{crew_chip()}{head_block(30, False)}{stats_row(3)}{waypoints(compact=True)}{preset_rows(dense=True)}'
                 f'<div style="flex:1"></div>{actions(1)}')
    elif level == 2:
        inner = (f'{crew_back(2)}{crew_head()}{preset_tabs()}<div style="display:flex;gap:10px">'
                 + "".join(seat_card(i, 192, dense=True) for i in range(3)) + f'</div><div style="flex:1"></div>{actions(2)}')
    else:
        inner = f'{crew_back(3)}{story_head()}{story_blocks(1, 15)}<div style="flex:1"></div>{actions(3)}'
    body.append(frame("G", x, y, w, h, inner, gap=14))
    return "\n".join(body)


# ------------------------------------------------------------------------------------------------ H - Logbook

def ledger(rows):
    return "".join(f'<div style="display:flex;align-items:baseline;gap:8px;height:34px"><span class="lab" style="font-size:12px">{k}</span>'
                   f'<span style="flex:1;border-bottom:1px dotted {NEUT};transform:translateY(-4px)"></span><span class="num" style="font-size:18px">{v}</span></div>'
                   for k, v in rows)


def manifest():
    head = "".join(f'<span class="lab" style="font-size:11px">{t}</span>' for t in ("Seat", "Vessel", "Pilot", "Element", "Artifact"))
    rows = []
    for i, (role, who, vessel, pilot, element, art) in enumerate(SEATS):
        cells = [f'<span style="display:flex;flex-direction:column;gap:2px"><span class="lab" style="font-size:10px">{role}</span><span style="font-size:16px">{who}</span></span>']
        cells += [f'<button class="pick" type="button" style="display:flex;align-items:center;gap:6px;height:34px;font-size:16px;color:{INACT if v == "No artifact" else LIGHT};'
                  f'border-bottom:1px solid {DOM["Jade"][0] if i == 0 else NEUT}"><span class="ell" style="flex:1">{v}</span><span style="color:{NEUT_L}">{r43.CHEV}</span></button>'
                  for v in (vessel, pilot, element, art)]
        rows.append("".join(cells))
    grid = "".join(f'<div style="display:grid;grid-template-columns:150px repeat(4, minmax(0, 1fr));column-gap:16px;align-items:end;min-height:68px">{r}</div>' for r in rows)
    hulls = "".join(f'<img src="{HULL[s[2]]}" alt="{s[2]}" style="width:150px;height:56px;object-fit:contain">' for s in SEATS)
    return (f'<div style="display:flex;justify-content:space-around">{hulls}</div>'
            f'<div style="display:grid;grid-template-columns:150px repeat(4, minmax(0, 1fr));column-gap:16px">{head}</div>{grid}')


def screen_H(level):
    """H - Logbook: the voyage is a page of the Ark's log; deeper levels are the next pages, turned in from the right."""
    body = [nav(), tabrow("H", "Voyages"), left_map(1060)]
    x, w = 1120, 760
    if level == 1:
        inner = (f'<div style="display:flex;justify-content:space-between"><span class="lab">Log - voyage 2 of 4</span>'
                 f'<span class="lab">Found by {V["callsign"]}</span></div>{head_block(30, False)}'
                 f'<div style="display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));column-gap:28px">'
                 f'{ledger(r43.STATS[:3])}{ledger(r43.STATS[3:])}</div>{sea_bar()}{waypoints(compact=True)}{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}')
        body.append(frame("H", x, MAP_Y, w, 868, inner, pad="26px 32px", gap=14))
    else:
        # the first page stays as a strip behind the turned page
        body.append(f'<div class="abs" style="left:{x}px;top:{MAP_Y}px;width:{w}px;height:868px;background:{GLASS};box-shadow:inset 0 0 0 1px {NEUT_D}">'
                    f'<span class="abs cap" style="left:18px;top:24px;writing-mode:vertical-rl;transform:rotate(180deg);font-size:13px;color:{NEUT_L}">Page 1 - Voyage</span></div>')
        if level == 2:
            inner = (f'<div style="display:flex;justify-content:space-between"><span class="lab">Log - page 2 - Crew manifest</span>{preset_tabs()}</div>'
                     f'<span style="font-size:28px">Skim Wing</span>{manifest()}<div style="flex:1"></div>{actions(2)}')
        else:
            inner = (f'<span class="lab">Log - page 3 - Story (optional)</span><span style="font-size:28px">{V["title"]}</span>'
                     f'{story_blocks(1, 15)}<div style="flex:1"></div>{actions(3)}')
        body.append(frame("H", x + 52, MAP_Y, w - 52, 868, inner, label=f"Log page {level}", pad="26px 32px", gap=14))
    return "\n".join(body)


# ------------------------------------------------------------------------------------------------ I - Formation

def formation(scale=1.0, labels=False):
    pos = [(170, 10, 1.0), (30, 70, .82), (310, 70, .82)]   # leader ahead, rogues on its flanks
    out = []
    for (px, py, s), seat in zip(pos, SEATS):
        role, who, vessel = seat[0], seat[1], seat[2]
        ww, hh = int(260 * s * scale), int(92 * s * scale)
        lab = (f'<span class="abs cap" style="left:0;right:0;top:{hh + 2}px;text-align:center;font-size:11px;color:{SUB}">{role} - {who}</span>') if labels else ""
        out.append(f'<div class="abs" style="left:{int(px * scale)}px;top:{int(py * scale)}px;width:{ww}px;height:{hh + (22 if labels else 0)}px">'
                   f'<img src="{HULL[vessel]}" alt="{vessel}" style="width:{ww}px;height:{hh}px;object-fit:contain">{lab}</div>')
    return f'<div style="position:relative;width:{int(580 * scale)}px;height:{int(190 * scale)}px;margin:0 auto">{"".join(out)}</div>'


def ark_switch():
    name, dom = PRESETS[0][0], PRESETS[0][1]
    c, cl = DOM[dom]
    return (f'<div style="display:flex;align-items:center;justify-content:space-between">{kc("LT")}'
            f'<div style="display:flex;flex-direction:column;align-items:center;gap:4px"><span class="lab" style="font-size:11px">Your Ark - 1 of 2</span>'
            f'<span style="display:flex;align-items:center;gap:10px;font-size:24px"><span style="width:10px;height:10px;transform:rotate(45deg);background:{c}"></span>{name}'
            f'<span class="cap" style="font-size:12px;color:{cl}">{dom}</span></span></div>{kc("RT")}</div>')


def screen_I(level):
    """I - Formation: the Ark you sail with is the hero of the card - its three hulls in formation - and the voyage sits
    under it. Ark crew opens the formation into its three seats; Story takes the formation's place."""
    body = [nav(), tabrow("I", "Voyages"), left_map(1060)]
    x, w = 1120, 760
    if level == 1:
        inner = (f'{ark_switch()}{formation(1.0)}<div style="display:flex;gap:10px"><button class="btn gho sls" type="button" style="flex:1;height:40px">{kc("X")}<span>Configure the Ark</span></button></div>'
                 f'<div class="rule"></div>{head_block(28)}{crew_chip()}{stats_row(6)}{sea_bar()}{waypoints(compact=True)}<div style="flex:1"></div>'
                 f'<div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:1.6">{kc("A")}<span>Set sail</span></button>'
                 f'<button class="btn gho sl" type="button" style="flex:1">{kc("Y")}<span>Story</span></button></div>')
    elif level == 2:
        cols = "".join(f'<div style="display:flex;flex-direction:column;gap:6px;min-width:0"><span class="lab" style="font-size:11px;color:{LIGHT if i == 0 else NEUT_L}">0{i + 1} {s[0]} - {s[1]}</span>'
                       f'{setting("Vessel", s[2], None, True, True)}{setting("Pilot", s[3], None, True, True)}{setting("Element", s[4], None, True, True)}{setting("Artifact", s[5], None, True, True)}</div>'
                       for i, s in enumerate(SEATS))
        inner = (f'{ark_switch()}{formation(1.12, labels=True)}<div style="display:grid;grid-template-columns:repeat(3, minmax(0, 1fr));gap:12px">{cols}</div>'
                 f'<div style="flex:1"></div>{actions(2)}')
    else:
        inner = f'{story_head()}{story_blocks(1, 15)}<div style="flex:1"></div>{actions(3)}'
    body.append(frame("I", x, MAP_Y, w, 868, inner, pad="26px 28px", gap=14))
    return "\n".join(body)


# ------------------------------------------------------------------------------------------------ J - On the sea

def screen_J(level):
    """J - On the sea: no card box at all. The levels are columns standing on the sea lines; a deeper level pushes the
    voyage into a slim column beside it. The map keeps its place."""
    body = [nav(), tabrow("J", "Voyages"), left_map(1040, pos="40% 45%")]
    if level == 1:
        inner = (f'{head_block(34)}{crew_chip()}{stats_row(6)}{sea_bar()}{waypoints(compact=False)}{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}')
        body.append(frame("J", 1120, 176, 760, 864, inner, gap=18))
        return "\n".join(body)
    slim = (f'<span class="lab">1 Voyage</span><span style="font-size:22px;line-height:1.15">{V["title"]}</span>'
            f'<span class="cap num" style="font-size:12px;color:{SUB}">580 ly - 29 min - I2</span>{waypoints(vertical=True)}<div style="flex:1"></div>'
            f'<button class="btn cta sl" type="button">{kc("A")}<span>Set sail</span></button>')
    body.append(frame("J", 1120, 176, 250, 864, slim, label="Voyage", gap=12))
    if level == 2:
        inner = (f'<span class="lab">2 Ark crew</span>{preset_tabs()}<span style="font-size:28px">Skim Wing</span>'
                 + "".join(f'<div style="display:grid;grid-template-columns:88px repeat(2, minmax(0, 1fr));gap:6px;align-items:center">'
                           f'<div style="display:flex;flex-direction:column;gap:2px"><img src="{PILOT_ART[s[3]]}" alt="{s[3]}" style="width:44px;height:62px;object-fit:contain">'
                           f'<span class="lab" style="font-size:10px">{s[0]}</span></div>'
                           f'<div style="display:flex;flex-direction:column;gap:6px">{setting("Vessel", s[2], None, True, True)}{setting("Element", s[4], None, True, True)}</div>'
                           f'<div style="display:flex;flex-direction:column;gap:6px">{setting("Pilot", s[3], None, True, True)}{setting("Artifact", s[5], None, True, True)}</div></div>'
                           for s in SEATS)
                 + f'<div style="flex:1"></div><div style="display:flex;gap:10px"><button class="btn sec sl" type="button" style="flex:1">{kc("X")}<span>Save as Ark</span></button>'
                 f'<button class="btn gho sl" type="button" style="flex:1">{kc("B")}<span>Voyage</span></button></div>')
    else:
        inner = (f'<span class="lab">3 Story - optional</span>{story_blocks(1, 15)}<div style="flex:1"></div>'
                 f'<button class="btn gho sl" type="button">{kc("B")}<span>Voyage</span></button>')
    body.append(frame("J", 1400, 176, 480, 864, inner, label=f"Level {level}", gap=14))
    return "\n".join(body)


SCREENS = {"G": screen_G, "H": screen_H, "I": screen_I, "J": screen_J}


# ------------------------------------------------------------------------------------------------ Saga, Quests, Episodes

def art_left(src, alt, title, kicker, chips=None, dim=False):
    chip = "".join(f'<span class="cap" style="font-size:12px;padding:5px 10px;box-shadow:inset 0 0 0 1px {NEUT};color:#C9CDEA">{t}</span>' for t in (chips or []))
    return (f'<figure style="position:absolute;left:40px;top:{MAP_Y}px;width:1060px;height:868px;margin:0;overflow:hidden;background:#04103A;box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'<img src="{src}" alt="{alt}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:{.35 if dim else 1}">'
            f'<div class="abs" style="left:0;right:0;bottom:0;height:55%;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.92))"></div>'
            f'<div class="abs" style="left:36px;right:36px;bottom:32px;display:flex;flex-direction:column;gap:12px"><span class="lab">{kicker}</span>'
            f'<span style="font-size:64px;letter-spacing:.06em">{title}</span><div style="display:flex;gap:8px;flex-wrap:wrap">{chip}</div></div></figure>')


def saga_parts():
    out = []
    for i, (name, line, on) in enumerate([("Tandava", "Open", True), ("The Severing", "After Tandava", False)]):
        out.append(f'<button class="pick sls" type="button" style="position:relative;display:flex;align-items:center;gap:14px;height:64px;padding:0 18px;'
                   f'background:{"rgba(14,22,70,.9)" if on else "rgba(6,10,36,.7)"};box-shadow:inset 0 0 0 1px {LIGHT if on else NEUT_D}">'
                   f'<span class="num" style="font-size:18px;color:{LIGHT if on else INACT}">0{i + 1}</span>'
                   f'<span style="display:flex;align-items:center;gap:8px;font-size:20px;color:{LIGHT if on else NEUT_L}">{"" if on else LOCK}{name}</span>'
                   f'<span style="flex:1"></span><span class="cap" style="font-size:12px;color:{CTA if on else INACT}">{line}</span></button>')
    return "".join(out)


def screen_saga(opt):
    body = [nav(), tabrow(opt, "Saga"),
            art_left(TANDAVA, "Tandava", "TANDAVA", "Saga - Part 1", ["Great Serpent", "Many-Headed Serpent", "Lord of the Dance", "Antlion"])]
    inner = (f'<span class="lab">The Saga</span><span style="font-size:34px">Tandava</span>'
             f'<span style="font-size:16px;color:{SUB}">Two parts. The second opens when the first is done.</span>{saga_parts()}<div style="flex:1"></div>'
             f'<button class="btn cta sl" type="button">{kc("A")}<span>Set sail</span></button>')
    x, y, w, h = (1180, 196, 660, 844) if opt == "G" else (1120, MAP_Y, 760, 868)
    if opt == "J":
        x, y, w, h = 1120, 176, 760, 864
    body.append(frame(opt, x, y, w, h, inner, label="Saga", gap=14))
    return "\n".join(body)


def quest_list_row(k):
    num, name, state, line, open_ = QUESTS[k]
    return (f'<button class="pick sls" type="button" style="display:flex;align-items:center;gap:14px;height:76px;padding:0 16px 0 0;overflow:hidden;'
            f'background:{"rgba(14,22,70,.9)" if open_ else "rgba(6,10,36,.7)"};box-shadow:inset 0 0 0 1px {LIGHT if open_ else NEUT_D}">'
            f'<img src="{QUEST_ART[k]}" alt="" style="width:120px;height:76px;object-fit:cover;opacity:{1 if open_ else .4}">'
            f'<span class="num" style="font-size:16px;color:{LIGHT if open_ else INACT}">{num}</span>'
            f'<span style="display:flex;flex-direction:column;gap:3px;flex:1;min-width:0"><span style="display:flex;align-items:center;gap:8px;font-size:19px;color:{LIGHT if open_ else NEUT_L}">'
            f'{"" if open_ else LOCK}{name.upper()}</span><span class="ell" style="font-size:13px;color:{SUB}">{line}</span></span>'
            f'<span class="cap" style="font-size:11px;color:{CTA if open_ else INACT}">{state}</span></button>')


def screen_quests(opt):
    """The quest tracker on its own tab: the open quest large on the left, the five in order on the right."""
    body = [nav(), tabrow(opt, "Quests"),
            art_left(QUEST_ART[0], "Scurry", "SCURRY", "Quest 01 - in progress", ["Intensity 1 of 4"])]
    inner = (f'<div style="display:flex;justify-content:space-between;align-items:flex-end"><span style="display:flex;flex-direction:column;gap:4px">'
             f'<span class="lab">Quest tracker</span><span style="font-size:30px">Quests</span></span>'
             f'<span style="display:flex;align-items:center;gap:12px"><span class="num" style="font-size:20px">0 / 5</span>'
             f'<span style="width:140px;height:4px;background:{NEUT_D}"></span></span></div>'
             + "".join(quest_list_row(k) for k in range(5))
             + f'<div style="flex:1"></div><button class="btn cta sl" type="button">{kc("A")}<span>Play quest</span></button>')
    x, y, w, h = (1180, 196, 660, 844) if opt == "G" else (1120, MAP_Y, 760, 868)
    if opt == "J":
        x, y, w, h = 1120, 176, 760, 864
    body.append(frame(opt, x, y, w, h, inner, label="Quests", gap=12))
    return "\n".join(body)


def screen_episodes(opt):
    """Episodes are not out yet: the tab is there, locked, and says so - in the option's own frame."""
    body = [nav(), tabrow(opt, "Episodes")]
    inner = (f'<div style="flex:1"></div><div style="display:flex;flex-direction:column;align-items:center;gap:16px;text-align:center">'
             f'<span style="color:{NEUT_L};transform:scale(3);margin-bottom:22px">{LOCK}</span>'
             f'<span class="lab">Episodes</span><span style="font-size:44px">Coming soon</span>'
             f'<span style="font-size:17px;color:{SUB}">New episodes arrive here. Voyages, the Saga and Quests are open now.</span>'
             f'<div style="display:flex;gap:10px;margin-top:10px"><button class="btn gho sl" type="button">{kc("LB")}<span>Quests</span></button>'
             f'<button class="btn sec sl" type="button">{kc("B")}<span>Back to Voyages</span></button></div></div><div style="flex:1"></div>')
    body.append(frame(opt, 560, 220, 800, 700, inner, label="Episodes", pad="40px 48px", gap=0))
    return "\n".join(body)


# ------------------------------------------------------------------------------------------------ compare and notes

OPTS = {
    "G": ("Crew's hand", "Each voyage is a card a crew member hands you; the open one in front, the others behind it. Ark crew and Story are the back of the same card, turned over (Y)."),
    "H": ("Logbook", "The voyage is a page of the Ark's log: ruled, a ledger of the numbers. Ark crew is the next page (a crew manifest table), Story the page after."),
    "I": ("Formation (recommended)", "Your Ark leads the card: its three hulls in formation, LT / RT through your Arks. Ark crew opens the formation into its three seats."),
    "J": ("On the sea", "No card box: the levels are columns standing on the sea lines. A deeper level pushes the voyage into a slim column beside it."),
}


def compare():
    rows = []
    for k, (name, line) in OPTS.items():
        rows.append(f'<div style="display:flex;flex-direction:column;gap:8px"><span style="font-size:26px">{k} - {name}</span>'
                    f'<span style="font-size:16px;line-height:1.45;color:{SUB}">{line}</span></div>')
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - compare</span>'
            f'<span style="font-size:30px">Four more: the window\'s four tabs, and what each option makes the voyage card</span></div>'
            f'<div class="abs" style="left:60px;top:140px;width:1800px;display:grid;grid-template-columns:repeat(4, minmax(0, 1fr));column-gap:40px">{"".join(rows)}</div>'
            f'<div class="abs" style="left:60px;top:400px;width:1800px;display:flex;flex-direction:column;gap:6px"><span class="lab">The tab row in each - Voyages opens first, Episodes locked</span></div>'
            + "".join(f'<div class="abs" style="left:0;top:{440 + 150 * i}px;width:1920px;height:140px;overflow:hidden">'
                      f'<span class="abs lab" style="left:14px;top:20px">{k}</span>'
                      f'<div class="abs" style="left:0;top:-96px;width:1920px;height:300px">{tabrow(k, "Voyages")}</div></div>' for i, k in enumerate(OPTS)))


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:17px;line-height:1.5;color:#C9CDEA">{t}</p>'
    left = "".join([
        '<span class="lab">Asked (Shombith, after Rev 43)</span>',
        para('"Episode 1 is not the Hypersea. Episode, saga are separate things and so is quest tracker. So 4 tabs and Voyage opens up first, with episodes coming soon or locked. '
             'I didn\'t much like the mockup ideas; we should add sea lines in the background; maybe create more mockups like 4 more."'),
        '<span class="lab" style="margin-top:10px">What changed from Rev 43</span>',
        para("Four tabs on LB / RB: Voyages (open first), Saga (Tandava, then the Severing), Quests (the quest tracker: Scurry, Hex Race, Joust, Maelstrom, Vessel Hangar, 0 of 5) "
             "and Episodes (locked: coming soon). Rev 43 wrongly called the quest tracker Episode 1. The sea lines run behind every board; "
             "the map and the edge view stay opaque on the left. The voyage card keeps its three levels and the same words, so the four options differ in shape only."),
    ])
    right = "".join([
        '<span class="lab">Recommendation: I, Formation</span>',
        para("It makes the preset the thing you see: the Ark you sail with stands in formation at the top of the card, LT / RT steps through your Arks, and Configure is one press. "
             "Ark crew grows the same formation into its three seats, so level 2 reads as the Ark opening up rather than a new form. "
             "G is the most playful and keeps the crew handing you the voyage; H is the calmest to read; J shows the most sea but asks the most of the text."),
        '<span class="lab" style="margin-top:10px">Saga, Quests, Episodes</span>',
        para("Saga and Quests share one frame in every option: the art large where the map sits, the list in the option's card on the right. Episodes is one locked panel. "
             "Their contents are unchanged."),
    ])
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:30px">Four more Voyage windows, four tabs, on the sea</span></div>'
            f'<div class="abs pane" style="left:40px;top:130px;width:880px;padding:20px;box-sizing:border-box;display:flex;flex-direction:column;gap:12px">{left}</div>'
            f'<div class="abs pane" style="left:960px;top:130px;width:920px;padding:20px;box-sizing:border-box;display:flex;flex-direction:column;gap:12px">{right}</div>')


SCREEN_KEYS = [("Voyage", 1), ("Crew", 2), ("Story", 3), ("Saga", None), ("Quests", None), ("Episodes", None)]


def main():
    files = {f"R{REV}Compare.dc.html": board(f"Rev {REV} - compare", compare())}
    for k in OPTS:
        for key, lv in SCREEN_KEYS:
            if lv:
                body = SCREENS[k](lv)
            else:
                body = {"Saga": screen_saga, "Quests": screen_quests, "Episodes": screen_episodes}[key](k)
            files[f"R{REV}{k}{key}.dc.html"] = board(f"Rev {REV} {k} - {OPTS[k][0]} - {key}", body)
    files[f"R{REV}Notes.dc.html"] = board(f"Rev {REV} - notes", notes())
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:32} {len(s):>7}")


if __name__ == "__main__":
    main()
