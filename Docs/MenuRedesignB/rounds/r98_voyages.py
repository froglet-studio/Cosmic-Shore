#!/usr/bin/env python3
"""Rev 98 boards: Shombith's picks from Rev 97 (1A and 2D) with room between the words (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md section 183).

Shombith, 2026-11-12, on Rev 97: "s2d and s1a but the thing is that everywhere the texts are too cramped up together.
Can we do something about that?"

The spacing rule these two boards are drawn to (the measure of "not cramped"):

  - lines of one group (a name and the line under it) stand at least 10 px apart, with a line height of 1.4 or more;
  - groups (the voyage's name, the squad, the buttons; one seat, the next) stand at least 28 px apart;
  - a label stands 16 px or more above what it labels;
  - no text wraps into a second line inside a tile: the tile is made wide enough;
  - where two short lines said one thing, they are one line with wide gaps between its parts.

    python3 Docs/MenuRedesignB/rounds/r98_voyages.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "98"
sys.argv = [sys.argv[0], _root, "97"]
_spec = importlib.util.spec_from_file_location("r97", HERE / "r97_voyages.py")
r97 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r97)
r96, r95, r89, r87 = r97.r96, r97.r95, r97.r89, r97.r87

REV, ROOT = int(_rev), r97.ROOT
JADE, INK, DIM, LINE = r97.JADE, r97.INK, r97.DIM, r97.LINE
BAND, POOL = r97.BAND, r97.POOL


def swap(text, old, new):
    assert old in text, old
    return text.replace(old, new)


def chips():
    """The voyages on offer, taller, the two lines of each further apart."""
    c = r89.voyage_chips("column", "width:100%", 96)
    c = swap(c, "left:20px;top:14px;font-size:18px", "left:22px;top:20px;font-size:18px")
    c = swap(c, "left:20px;top:44px;font-size:14px", "left:22px;top:58px;font-size:14px")
    return ('<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:310px;display:flex;flex-direction:column;gap:16px">'
            + c + '</nav>')


def explorer():
    e = r95.explorer(380, 244, 1020, 592, 204)
    e = swap(e, "flex-direction:column;gap:2px;background:rgba(0,1,10,.78);padding:8px 0", "flex-direction:column;gap:6px;background:rgba(0,1,10,.78);padding:14px 0")
    e = swap(e, "align-items:baseline;gap:10px;height:30px;padding:0 14px 0 12px", "align-items:baseline;gap:18px;height:36px;padding:0 22px 0 18px")
    return e


def name_block():
    return (f'<div style="display:flex;flex-direction:column"><span class="lab">{{{{hero.kicker}}}}</span>'
            f'<span style="font-size:36px;line-height:1.2;margin-top:14px">{{{{voyage.place}}}}</span>'
            f'<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:12px">{{{{voyage.title}}}}</span>'
            f'<span class="num" style="font-size:20px;margin-top:20px;white-space:pre">{{{{hero.stats}}}}</span>'
            + r89.difficulty("display:flex;align-items:center;gap:14px;margin-top:16px") + '</div>')


def squads():
    """The squad presets as formations: the squad's name alone on its line, who flies under it, the hulls beside."""
    return (f'<div style="display:flex;flex-direction:column;gap:18px"><span class="lab">Squad</span>'
            '<sc-for list="{{presetCards}}" as="p" hint-placeholder-count="2"><button class="pick" type="button" aria-pressed="{{p.pressed}}" '
            f'onClick="{{{{p.pick}}}}" style="position:relative;height:136px;opacity:{{{{p.op}}}};background:{BAND}">'
            f'<span aria-hidden="true" style="position:absolute;left:56%;right:0;top:18px;height:100px;background:{POOL.format(c="{{p.pool}}")}"></span>'
            '<span style="position:absolute;left:30px;top:30px;display:flex;flex-direction:column;align-items:flex-start;gap:16px">'
            '<span style="font-size:22px;line-height:1.3;padding-bottom:6px;border-bottom:2px solid {{p.under}};white-space:nowrap">{{p.name}}</span>'
            f'<span style="font-size:14px;line-height:1.4;color:{INK}">{{{{p.who}}}}</span></span>'
            '<span style="position:absolute;right:6px;top:30px">' + r97.formation("{{p.h0}}", "{{p.h1}}", "{{p.h2}}", 168, 76, 120)
            + '</span></button></sc-for></div>')


def s1a_board():
    right = f"""<section aria-label="The voyage and the squad" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
{name_block()}
<div style="margin-top:40px">{squads()}</div>
<div style="margin-top:auto;display:flex;flex-direction:column;gap:18px">
<sc-if value="{{{{party.has}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="font-size:15px;line-height:1.5;color:{INK};text-align:center">{{{{party.line}}}}</span></sc-if>
{r96.quiet("X", "Configure squad", href=f"R{REV}S2D.dc.html")}</div>
<div style="margin-top:18px">{r96.sail()}</div>
</section>"""
    return r97.window(chips() + explorer() + right, f"Rev {REV} 1A - Screen 1, Shombith's pick, with room between the words - interactive")


def summary():
    return (f'<div class="abs" style="left:40px;top:244px;width:1840px;height:56px;display:flex;align-items:center;z-index:3">'
            + r96.quiet("B", "Voyages", href=f"R{REV}S1A.dc.html", style="width:220px;height:44px;font-size:14px")
            + '<span style="font-size:22px;margin-left:44px">{{voyage.n}}  {{voyage.place}}</span>'
            f'<span class="num" style="font-size:18px;color:{INK};white-space:pre;margin-left:44px">{{{{hero.stats}}}}</span>'
            f'<div style="margin-left:auto">{swap(r87.squads(), "gap:26px", "gap:40px")}</div></div>')


def stage():
    part = lambda hole: f'<span>{{{{s.{hole}}}}}</span>'
    return f"""<div class="abs" style="left:40px;top:312px;width:1840px;height:330px;display:grid;grid-template-columns:repeat(3,1fr);gap:20px;z-index:2">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{{{s.focused}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.focus}}}}" style="position:relative;height:330px;opacity:{{{{s.dim}}}}">
<span aria-hidden="true" style="position:absolute;left:6%;right:6%;top:96px;height:110px;background:{POOL.format(c="{{s.pool}}")}"></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="position:absolute;left:120px;top:14px;width:440px;height:156px">
<img src="{{{{s.face}}}}" alt="" style="position:absolute;left:24px;top:0;width:136px;height:180px;object-fit:contain;object-position:50% 100%">
<span style="position:absolute;left:0;right:0;bottom:0;display:flex;flex-direction:column;align-items:center">
<span class="lab" style="color:{INK}">{{{{s.role}}}}</span>
<span style="font-size:28px;line-height:1.3;margin-top:10px;padding-bottom:6px;border-bottom:2px solid {{{{s.under}}}}">{{{{s.name}}}}</span>
<span style="display:flex;gap:28px;margin-top:16px;font-size:15px;line-height:1.4;color:{INK}">{part("player")}{part("pilot")}{part("el")}{part("art")}</span></span>
</button></sc-for></div>"""


def shelf():
    g = lambda label, inner: r97.group(label, inner, gap=18, between=16)
    return f"""<section aria-label="The picked seat's choices" class="abs" style="left:40px;top:664px;width:1840px;height:300px;z-index:2;background:{BAND}">
<div class="abs" style="left:110px;right:110px;top:20px;bottom:8px;display:grid;grid-template-columns:544px 610px 250px 1fr;gap:28px">
{g("Vessel", r97.open_tile("strips.hulls", 170, 104, "hull", 6, img_h=58, size=15, gap=12))}
{g("Pilot", swap(r97.open_tile("strips.pilots", 140, 216, "face", 4, img_h=150, size=15, note=True, gap=10), "min-height:18px", "min-height:20px;white-space:nowrap"))}
{g("Element", r97.open_tile("strips.els", 116, 104, "icon", 4, img_h=48, gap=12))}
{g("Artifact", r97.open_tile("strips.arts", 150, 36, "word", 5))}
</div></section>"""


def foot():
    return (f'<div class="abs" style="left:40px;top:984px;width:1840px;height:56px;display:flex;align-items:center;z-index:3">'
            + r96.quiet("Y", "Save as preset", style="width:320px")
            + f'<span style="margin-left:48px;font-size:15px;line-height:1.5;color:{INK}">A vessel brings the pilot and artifact you keep for it in the Hangar.</span>'
            + f'<div style="margin-left:auto;width:520px">{r96.sail()}</div></div>')


def s2d_board():
    return r97.window(summary() + stage() + shelf() + foot(),
                      f"Rev {REV} 2D - The Deck with the Shelf, Shombith's pick, with room between the words - interactive", backdrop=r96.deck())


BOARDS = {f"R{REV}S1A.dc.html": s1a_board, f"R{REV}S2D.dc.html": s2d_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
