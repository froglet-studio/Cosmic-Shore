#!/usr/bin/env python3
"""Rev 81 boards: Rev 80 B1 with B3's seat cards and a livelier right panel (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md section 162).

Shombith, 2026-11-09, on Rev 80: "I like b1 but I want that 3 card information still and the right panel looks a bit
dull". Every option keeps B1's list and course as they are (the course a little narrower so the panel has room) and
redraws the right panel around B3's three seat cards - each pilot large, the hull, the name, who flies it, the
element:

  B5  Cards     labelled numbers, then B3's tall seat cards
  B6  Manifest  each card's pilot fills it like a poster, the hull over its foot
  B7  Banner    the destination's own picture heads the panel, the title and numbers over it, the cards below
  Notes

    python3 Docs/MenuRedesignB/rounds/r81_voyages.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "81"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r80", HERE / "r80_voyages.py")
r80 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r80)

REV, ROOT, page = int(_rev), r80.ROOT, r80.page
window, course, edge, stats, squad_head, tab_text, hull_grid, CTA = (
    r80.window, r80.course, r80.edge, r80.stats, r80.squad_head, r80.tab_text, r80.hull_grid, r80.CTA)

PX, PW = 1320, 560          # the right panel: x and width
MAP_W = 660                 # the course column, narrower than B1's 740 so the panel has room

ROWS = ('<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:540px;display:flex;flex-direction:column;gap:8px">'
        '<sc-for list="{{vlist}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" '
        'onClick="{{r.pick}}" style="height:190px;box-sizing:border-box;padding:0 22px;display:grid;grid-template-columns:150px 1fr;'
        'gap:22px;align-items:center;background:{{r.bg}};box-shadow:inset 3px 0 0 {{r.bar}}">'
        '<img class="sls" src="{{r.map}}" alt="" style="width:150px;height:110px;object-fit:cover;object-position:{{r.pos}}">'
        '<span style="display:flex;flex-direction:column;gap:8px"><span style="font-size:22px;color:{{r.col}}">{{r.n}}  {{r.place}}</span>'
        '<span class="num" style="font-size:15px;line-height:1.6;color:#747BAD">{{r.crew}}<br>{{r.meta}}</span></span>'
        '</button></sc-for></nav>')


def left():
    return (tab_text(64, 178) + "\n" + ROWS + "\n" + course(620, 244, MAP_W, 470, muted=True, plain=True) + "\n"
            + edge(620, 734, MAP_W, 306, muted=True, plain=True))


def b3_cards(height, portrait_h, hull_h):
    """B3's seat card: the pilot large, the hull under them, the name, who flies it and the element."""
    return ('<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:14px">'
            '<sc-for list="{{seatNames}}" as="s" hint-placeholder-count="3"><button class="pick sl" type="button" aria-pressed="{{s.pressed}}" '
            f'onClick="{{{{s.pick}}}}" style="height:{height}px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px;'
            'padding:0 8px 16px;background:linear-gradient(180deg,rgba(0,4,31,.2),rgba(0,4,31,.92));box-shadow:{{s.edge}}">'
            f'<img src="{{{{s.face}}}}" alt="" style="width:100%;height:{portrait_h}px;object-fit:contain;object-position:50% 100%">'
            f'<img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.role}}}}, {{{{s.who}}}}: {{{{s.name}}}}, press to change" style="width:100%;height:{hull_h}px">'
            '<span style="font-size:18px">{{s.name}}</span>'
            '<span style="font-size:15px;line-height:1.5;color:#E6E9FF;text-align:center">{{s.short}} - {{s.pilot}}<br>{{s.el}}</span>'
            '</button></sc-for></div>')


# ── B5: Cards ─────────────────────────────────────────────────────────────────────────────────────────────────────────

def b5_board():
    panel = f"""<section class="abs" style="left:{PX}px;top:244px;width:{PW}px;height:796px;display:flex;flex-direction:column">
<span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:12px">{{{{voyage.line}}}}</span>
<div style="margin-top:22px">{stats(labelled=True, size=28)}</div>
<span style="font-size:15px;line-height:1.5;color:#747BAD;margin-top:20px">{{{{voyage.placeLine}}}}</span>
{squad_head("margin-top:auto")}
{b3_cards(330, 150, 82)}
{CTA.format(style="margin-top:18px;width:100%")}
</section>"""
    return window(left() + panel + hull_grid(620, 244, MAP_W, 3, 176),
                  f"Rev {REV} B5 - Cards: B1 with labelled numbers and B3's three seat cards in the right panel (recommended) - interactive")


# ── B6: Manifest ──────────────────────────────────────────────────────────────────────────────────────────────────────

def b6_board():
    cards = ('<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:14px">'
             '<sc-for list="{{seatNames}}" as="s" hint-placeholder-count="3"><button class="pick sl" type="button" aria-pressed="{{s.pressed}}" '
             'onClick="{{s.pick}}" style="position:relative;height:420px;overflow:hidden;background:#00010A;box-shadow:{{s.edge}}">'
             '<img src="{{s.face}}" alt="" style="position:absolute;left:0;top:0;width:100%;height:330px;object-fit:cover;object-position:50% 12%">'
             '<span style="position:absolute;left:0;right:0;bottom:0;height:220px;display:flex;flex-direction:column;justify-content:flex-end;'
             'align-items:center;gap:4px;padding-bottom:14px;background:linear-gradient(180deg,rgba(0,1,10,0),rgba(0,1,10,.96) 42%)">'
             '<img class="hull" src="{{s.hull}}" alt="{{s.role}}, {{s.who}}: {{s.name}}, press to change" style="width:92%;height:84px">'
             '<span style="font-size:18px">{{s.name}}</span>'
             '<span style="font-size:15px;line-height:1.5;color:#E6E9FF;text-align:center">{{s.short}} - {{s.pilot}}<br>{{s.el}}</span></span>'
             '</button></sc-for></div>')
    panel = f"""<section class="abs" style="left:{PX}px;top:244px;width:{PW}px;height:796px;display:flex;flex-direction:column">
<span class="lab">{{{{voyage.kicker}}}}</span>
<span style="font-size:28px;line-height:1.25;margin-top:10px">{{{{voyage.title}}}}</span>
<div style="margin-top:18px">{stats(labelled=False, size=28)}</div>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:16px">{{{{voyage.line}}}}</span>
{squad_head("margin-top:auto")}
{cards}
{CTA.format(style="margin-top:18px;width:100%")}
</section>"""
    return window(left() + panel + hull_grid(620, 244, MAP_W, 3, 176),
                  f"Rev {REV} B6 - Manifest: each seat card is its pilot, full height like a poster, the hull over its foot - interactive")


# ── B7: Banner ────────────────────────────────────────────────────────────────────────────────────────────────────────

def b7_board():
    panel = f"""<section class="abs sl" style="left:{PX}px;top:244px;width:{PW}px;height:796px;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89">
<img src="{{{{voyage.map}}}}" alt="" style="position:absolute;left:0;top:0;width:100%;height:330px;object-fit:cover;object-position:{{{{voyage.pos}}}};filter:saturate(.7) brightness(.9)">
<div class="abs" style="left:0;top:0;width:100%;height:330px;background:linear-gradient(180deg,rgba(0,1,10,.1) 30%,rgba(0,1,10,.94))"></div>
<div class="abs" style="left:28px;right:28px;top:150px;display:flex;flex-direction:column;gap:12px"><span class="lab" style="color:#E6E9FF">{{{{voyage.place}}}}</span><span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span>{stats(labelled=False, size=22)}</div>
<div class="abs" style="left:28px;right:28px;top:340px;bottom:24px;display:flex;flex-direction:column">
{squad_head("")}
{b3_cards(320, 150, 76)}
{CTA.format(style="margin-top:auto;width:100%")}
</div>
</section>"""
    return window(left() + panel + hull_grid(620, 244, MAP_W, 3, 176),
                  f"Rev {REV} B7 - Banner: the destination's picture heads the right panel, the title and numbers over it, the seat cards below - interactive")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"I like b1 but I want that 3 card information still and the right panel looks a bit dull\" (Shombith, 2026-11-09)."),
        ("Kept from B1", "The voyage list with each course's picture and who brought it, the course map and the edge view, muted so "
                         "the course reads first. The course column is 660 wide instead of 740, so the panel has room for three cards."),
        ("B3's cards, in all three", "Each seat: its pilot large, the hull under them, the vessel's name, who flies it and the element. "
                         "A press on a card opens every hull in the grid; one press picks."),
        ("B5 Cards (recommended)", "The plainest way to get the cards in: the title, the line, the numbers labelled (Distance, Sail, "
                         "Intensity), then the three cards and Set sail. Livelier because the pilots are large."),
        ("B6 Manifest", "Each card is its pilot, full height like a poster, the hull over the foot of the card. The most character."),
        ("B7 Banner", "The destination's own picture heads the panel with the title and numbers over it; the cards sit under it. "
                      "The panel is no longer plain text on the ground."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - B1 with B3\'s seat cards, a livelier right panel</span>{items}</div>')
    return page(f"Rev {REV} - notes: what B1 keeps, B3's cards, the three right panels, the recommendation", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}B5.dc.html": b5_board, f"R{REV}B6.dc.html": b6_board, f"R{REV}B7.dc.html": b7_board,
          f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
