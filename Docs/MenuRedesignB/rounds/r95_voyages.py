#!/usr/bin/env python3
"""Rev 95 boards: the Voyages tab as two screens - pick the voyage on the map, then the squad (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md section 180).

Shombith, 2026-11-12, with two screenshots of the built Sea table (section 172): "the top bar looks nothing like the
design", and "I dont like the preview earlier we could zoom in and out of the whole map, set course directly there
which is not possible. Okay lets do this, first screen in voyages is the left side list, that can stay, the middle
becomes the preview with all features that I mentioned earlier. Then we can have squad presets and set sail directly
or if we have players in the friends lobby with me and they are not in the squad we can have the configure your
current squad or something like that but then the second image can give us the options to select the vessel, pilot,
artifact, element etc and then set sail. Create 3 mockups each for both the screens".

So the tilt goes. The map is the explorer again, flat: zoom out to the whole galaxy and back in, and press a place to
set the course there. Screen 1 is the voyage; screen 2 is the squad.

  Screen 1 - the voyage (the list on the left stays)
    1A  Presets    the map over its edge view; on the right the voyage's name, the squad presets as cards, Set sail
    1B  Wide map   the map across the window; a foot bar holds the edge view, the voyage and the squad
    1C  Briefing   the map full height with the edge view inset at its foot; the squad's three seats shown on the right
  Screen 2 - the squad (vessel, pilot, element, artifact for each seat, then Set sail)
    2A  Three seats  a column per seat, its choices under it
    2B  Seat and picker  the seats on the left, one large picker on the right
    2C  Roster       one table: a row per seat, a column per choice
  Notes

In every screen 1, "Configure squad" opens the matching screen 2; a lobby friend who is not in the squad is named
beside it. The top bar is Rev 88's: small plain capitals, the shown tab underlined.

    python3 Docs/MenuRedesignB/rounds/r95_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "95"
sys.argv = [sys.argv[0], _root, "89"]          # Rev 89's own module keeps its names; this round's files are R95*
_spec = importlib.util.spec_from_file_location("r89", HERE / "r89_voyages.py")
r89 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r89)
r88, r87 = r89.r88, r89.r87

REV, ROOT, page = int(_rev), r89.ROOT, r89.page
JADE, INK, DIM, LINE, GLASS = r89.JADE, r89.INK, r89.DIM, r89.LINE, r89.GLASS
blob = r88.blob
TIERS = [["Galaxy", "10,000 ly", r88.GALAXY], ["Arm", "1,000 ly", blob("fced8d3ce00c8f4c54ce4f49f52448c0")],
         ["Sector", "200 ly", blob("6138a0a6b01214f86b8a8496fe0140ba")], ["Course", "50 ly", ""]]
# Where each voyage's destination stands on the zoomed-out views (shares of the map), and the lobby.
PINS = [[["53%", "47%"], ["47%", "44%"], ["55%", "53%"], ["45%", "55%"], ["51%", "58%"]],
        [["62%", "42%"], ["36%", "34%"], ["68%", "62%"], ["30%", "66%"], ["52%", "72%"]],
        [["70%", "36%"], ["24%", "26%"], ["78%", "70%"], ["18%", "74%"], ["54%", "82%"]]]
LOBBY = ["Halcyon", "Mirel"]

EXTRA = ("const TIERS = " + json.dumps(TIERS) + ";\nconst PINS = " + json.dumps(PINS) + ";\nconst LOBBY = " + json.dumps(LOBBY) + ";\n")
VALS = """  const tier = st.t === undefined ? 3 : st.t, T = TIERS[tier];
  const map = { img: T[2] || v.map, pos: T[2] ? '50% 50%' : v.pos, caption: T[0] + ' - ' + T[1], out: tier < 3, course: tier === 3,
    zoomIn: () => this.setState({ t: Math.min(3, tier + 1) }), zoomOut: () => this.setState({ t: Math.max(0, tier - 1) }),
    ladder: TIERS.map((x, i) => ({ name: x[0], scale: x[1], col: i === tier ? '#E6E9FF' : '#747BAD', bar: i === tier ? '#00D4FF' : 'rgba(0,0,0,0)',
      pressed: i === tier ? 'true' : 'false', pick: () => this.setState({ t: i }) })),
    pins: tier < 3 ? ALL.map((x, i) => ({ left: PINS[tier][i][0], top: PINS[tier][i][1], label: x.place, n: '0' + x.n,
      fill: i === st.v ? '#00D4FF' : '#E6E9FF', ink: i === st.v ? '#E6E9FF' : '#E6E9FF', pressed: i === st.v ? 'true' : 'false',
      pick: () => this.setState({ v: i, t: 3 }) })) : [] };
  // Friends in the lobby who fly no seat of this squad.
  const seated = seats.map((s) => s.who);
  const loose = LOBBY.filter((n) => seated.indexOf(n) < 0);
  const party = { has: loose.length > 0, line: loose.join(', ') + (loose.length === 1 ? ' is' : ' are') + ' in your lobby, not in this squad' };
  const flownBy = (i) => ['AI wingmate'].concat(LOBBY.filter((n) => n === seats[i].who || seated.indexOf(n) < 0));
  seatList.forEach((s, i) => { s.role = seats[i].role; s.ok = canEdit(i); s.locked = !canEdit(i); s.owner = seats[i].who + "'s seat";
    s.rowBg = st.open === i ? 'rgba(0,4,31,.94)' : 'rgba(0,4,31,.62)';
    if (q.mine && seats[i].who !== 'You') s.steps.push({ label: 'Flown by', value: seats[i].who,
      prev: () => set(i, { who: step(flownBy(i), seats[i].who, -1) }), next: () => set(i, { who: step(flownBy(i), seats[i].who, 1) }) }); });
  const presetCards = SQ.map((s, i) => ({ name: s.mine ? s.name : s.name + ' - ' + s.owner + "'s", who: st.books[i].map((x) => x.who === 'AI wingmate' ? 'AI' : x.who).join(', '),
    hulls: st.books[i].map((x) => ({ img: HULL[x.v] })), pressed: i === st.sq ? 'true' : 'false',
    edge: i === st.sq ? 'inset 3px 0 0 #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.7)', bg: i === st.sq ? 'rgba(0,4,31,.94)' : 'rgba(0,4,31,.62)',
    col: i === st.sq ? '#E6E9FF' : '#747BAD', pick: () => this.setState({ sq: i, open: 0 }) }));
  const squadNow = { name: q.mine ? q.name : q.name + ' - ' + q.owner + "'s", hulls: seats.map((x) => ({ img: HULL[x.v] })),
    next: () => this.setState({ sq: (st.sq + 1) % SQ.length, open: 0 }), prev: () => this.setState({ sq: (st.sq + SQ.length - 1) % SQ.length, open: 0 }) };
  const picker = { title: st.open >= 0 ? seats[st.open].role + ' - ' + (seats[st.open].who === 'AI wingmate' ? 'AI' : seats[st.open].who) : '' };
"""


def logic():
    lg = r89.logic()
    a = lg.index("class Component extends DCLogic")
    lg = lg[:a] + EXTRA + lg[a:]
    old = "  return { voyage, vlist, tabs, presets, seatList, tray, hero };"
    assert old in lg
    return lg.replace(old, VALS + "  return { voyage, vlist, tabs, presets, seatList, tray, hero, map, party, presetCards, squadNow, picker };")


def window(body, title):
    return r89.window(body, title, lg=logic())


def link(letter, screen):
    return f"R{REV}S{screen}{letter}.dc.html"


# ── Shared pieces ─────────────────────────────────────────────────────────────────────────────────────────────────────

def chips():
    return ('<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:310px;display:flex;flex-direction:column;gap:10px">'
            + r89.voyage_chips("column", "width:100%", 76) + '</nav>')


def explorer(x, y, w, h, edge_h=0, inset=False):
    """The HyperSea map as the explorer, flat: its zoom ladder and + / -, the places to set a course to when zoomed
    out, and the edge-on view under it (or inset at its foot)."""
    plus = ('<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
            '<path d="M8 2v12M2 8h12"></path></svg>')
    minus = ('<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
             '<path d="M2 8h12"></path></svg>')
    zoom = lambda fn, lab, svg, top: (f'<button class="pick sls abs" type="button" aria-label="{lab}" onClick="{{{{map.{fn}}}}}" '
                                      f'style="right:16px;top:{top}px;width:44px;height:44px;display:flex;align-items:center;justify-content:center;'
                                      f'color:{INK};background:rgba(0,1,10,.86);box-shadow:inset 0 0 0 1px {LINE}">{svg}</button>')
    ladder = ('<div class="abs" style="left:16px;top:16px;display:flex;flex-direction:column;gap:2px;background:rgba(0,1,10,.78);padding:8px 0">'
              '<sc-for list="{{map.ladder}}" as="t" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{t.pressed}}" '
              'onClick="{{t.pick}}" style="display:flex;align-items:baseline;gap:10px;height:30px;padding:0 14px 0 12px;box-shadow:inset 3px 0 0 {{t.bar}}">'
              '<span class="cap" style="font-size:14px;color:{{t.col}}">{{t.name}}</span>'
              f'<span class="num" style="font-size:14px;color:{DIM}">{{{{t.scale}}}}</span></button></sc-for></div>')
    pins = ('<sc-for list="{{map.pins}}" as="p" hint-placeholder-count="5"><button class="pick abs" type="button" aria-pressed="{{p.pressed}}" '
            'aria-label="Set course to {{p.label}}" onClick="{{p.pick}}" style="left:{{p.left}};top:{{p.top}};display:flex;align-items:center;gap:8px;'
            'height:44px;padding:0 10px 0 0">'
            '<span style="width:14px;height:14px;transform:rotate(45deg);background:{{p.fill}};box-shadow:0 0 0 2px #00010A"></span>'
            f'<span style="font-size:15px;color:{INK};background:rgba(0,1,10,.82);padding:4px 8px">{{{{p.n}}}}  {{{{p.label}}}}</span></button></sc-for>')
    hint = (f'<sc-if value="{{{{map.out}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="abs lab" style="left:16px;bottom:{14 + (edge_h if inset else 0)}px;'
            f'color:{INK};background:rgba(0,1,10,.78);padding:6px 10px">Press a place to set the course there</span></sc-if>')
    caption = (f'<span class="abs lab" style="right:16px;bottom:{14 + (edge_h if inset else 0)}px;color:{INK};background:rgba(0,1,10,.78);padding:6px 10px">'
               'HyperSea - {{map.caption}}</span>')
    edge_img = '<img src="{{voyage.edge}}" alt="The course seen edge on, through the disk" style="display:block;width:100%;height:100%;object-fit:cover">'
    out = (f'<figure class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;margin:0;overflow:hidden;background:#00010A">'
           '<img src="{{map.img}}" alt="The HyperSea map" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{map.pos}}">'
           + pins + ladder + zoom("zoomIn", "Zoom in", plus, 16) + zoom("zoomOut", "Zoom out", minus, 66) + hint + caption)
    if inset and edge_h:
        out += (f'<div class="abs" style="left:0;right:0;bottom:0;height:{edge_h}px;box-shadow:0 -1px 0 {INK};opacity:.94">{edge_img}</div>')
    out += '</figure>'
    if edge_h and not inset:
        out += (f'<figure class="abs" style="left:{x}px;top:{y + h}px;width:{w}px;height:{edge_h}px;margin:0;box-shadow:0 -1px 0 {INK}">{edge_img}</figure>')
    return out


def name_block(size=36, style=""):
    return (f'<div style="display:flex;flex-direction:column;gap:8px;{style}"><span class="lab">{{{{hero.kicker}}}}</span>'
            f'<span style="font-size:{size}px;line-height:1.1">{{{{voyage.place}}}}</span>'
            f'<span style="font-size:18px;line-height:1.4;color:{INK}">{{{{voyage.title}}}}</span>'
            f'<span class="num" style="font-size:20px;margin-top:4px;white-space:pre">{{{{hero.stats}}}}</span>'
            + r89.difficulty("display:flex;align-items:center;gap:14px;margin-top:6px") + '</div>')


def configure(letter, style):
    """Go and change the squad: names the lobby friend who has no seat yet."""
    return (f'<div style="display:flex;flex-direction:column;gap:10px;{style}">'
            f'<sc-if value="{{{{party.has}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="font-size:15px;line-height:1.4;color:{INK}">{{{{party.line}}}}</span></sc-if>'
            f'<a class="btn def" href="{link(letter, 2)}" style="width:100%"><span class="kc">X</span>Configure squad</a></div>')


SAIL = r87.sail("width:100%")


def summary(letter):
    """Screen 2's head: the voyage that was picked, and the way back to it."""
    return (f'<div class="abs" style="left:40px;top:244px;width:1840px;height:56px;display:flex;align-items:center;gap:26px">'
            f'<a class="btn sm def" href="{link(letter, 1)}"><span class="kc">B</span>Voyages</a>'
            '<span style="font-size:22px">{{voyage.n}}  {{voyage.place}}</span>'
            f'<span class="num" style="font-size:18px;color:{DIM};white-space:pre">{{{{hero.stats}}}}</span>'
            f'<div style="margin-left:auto">{r87.squads()}</div></div>')


def foot(extra=""):
    return (f'<div class="abs" style="left:40px;top:984px;width:1840px;height:56px;display:flex;align-items:center;gap:20px">'
            f'<button class="btn def" type="button" style="width:300px"><span class="kc">Y</span>Save as preset</button>{extra}'
            f'<div style="margin-left:auto;width:450px">{SAIL}</div></div>')


# ── Screen 1: the voyage ──────────────────────────────────────────────────────────────────────────────────────────────

def s1a_board():
    presets = (f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">Squad</span>'
               '<sc-for list="{{presetCards}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{p.pressed}}" '
               'onClick="{{p.pick}}" style="height:96px;display:grid;grid-template-columns:1fr 216px;align-items:center;gap:10px;padding:0 14px 0 18px;'
               'background:{{p.bg}};box-shadow:{{p.edge}}"><span style="display:flex;flex-direction:column;gap:6px">'
               '<span style="font-size:20px;color:{{p.col}}">{{p.name}}</span>'
               f'<span style="font-size:14px;color:{DIM}">{{{{p.who}}}}</span></span>'
               '<span style="display:grid;grid-template-columns:repeat(3,1fr);gap:4px"><sc-for list="{{p.hulls}}" as="h" hint-placeholder-count="3">'
               '<img class="hull" src="{{h.img}}" alt="" style="width:100%;height:44px"></sc-for></span></button></sc-for></div>')
    right = f"""<section aria-label="The voyage and the squad" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
{name_block()}
<div style="margin-top:30px">{presets}</div>
{configure("A", "margin-top:auto")}
<div style="margin-top:12px">{SAIL}</div>
</section>"""
    return window(chips() + explorer(380, 244, 1020, 592, 204) + right,
                  f"Rev {REV} 1A - Presets: the map over its edge view; the voyage's name, the squad presets as cards and Set sail on the right (recommended) - interactive")


def s1b_board():
    arrow = lambda fn, lab, svg: (f'<button class="pick" type="button" aria-label="{lab} squad" onClick="{{{{squadNow.{fn}}}}}" '
                                  f'style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{INK}">{svg}</button>')
    squad = (f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab">Squad</span>'
             f'<div class="sls" style="height:44px;display:flex;align-items:center;justify-content:space-between;background:#00010A;box-shadow:inset 0 0 0 1px {LINE}">'
             + arrow("prev", "Previous", r87.CHEV_L) + '<span style="font-size:18px">{{squadNow.name}}</span>' + arrow("next", "Next", r87.CHEV_R) + '</div>'
             '<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:6px"><sc-for list="{{squadNow.hulls}}" as="h" hint-placeholder-count="3">'
             '<img class="hull" src="{{h.img}}" alt="" style="width:100%;height:50px"></sc-for></div></div>')
    bar = f"""<section aria-label="The voyage and the squad" class="abs" style="left:380px;top:828px;width:1500px;height:212px;display:grid;grid-template-columns:470px 350px 270px 332px;gap:26px;align-items:stretch">
<figure style="margin:0;box-shadow:0 -1px 0 {INK};overflow:hidden"><img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" style="display:block;width:100%;height:100%;object-fit:cover"></figure>
{name_block(28)}
{squad}
<div style="display:flex;flex-direction:column;justify-content:flex-end;gap:12px">{configure("B", "")}{SAIL}</div>
</section>"""
    return window(chips() + explorer(380, 244, 1500, 572) + bar,
                  f"Rev {REV} 1B - Wide map: the map across the window; a foot bar holds the edge view, the voyage, the squad and Set sail - interactive")


def s1c_board():
    rows = ('<div style="display:flex;flex-direction:column;gap:8px"><sc-for list="{{seatList}}" as="s" hint-placeholder-count="3">'
            f'<div class="sls" style="height:72px;display:grid;grid-template-columns:120px 1fr;gap:14px;align-items:center;padding:0 14px;opacity:{{{{s.op}}}};'
            f'background:{GLASS};box-shadow:inset 0 0 0 1px rgba(67,76,137,.7)"><img class="hull" src="{{{{s.hull}}}}" alt="" style="width:120px;height:56px">'
            '<span style="display:flex;flex-direction:column;gap:4px"><span style="font-size:18px">{{s.name}}</span>'
            f'<span style="font-size:14px;color:{DIM}">{{{{s.line1}}}}</span></span></div></sc-for></div>')
    right = f"""<section aria-label="The voyage and the squad" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
{name_block()}
<div style="margin-top:26px">{r87.squads("margin-bottom:14px")}{rows}</div>
{configure("C", "margin-top:auto")}
<div style="margin-top:12px">{SAIL}</div>
</section>"""
    return window(chips() + explorer(380, 244, 1020, 796, 170, inset=True) + right,
                  f"Rev {REV} 1C - Briefing: the map full height with the edge view inset at its foot; the squad's three seats shown on the right - interactive")


# ── Screen 2: the squad ───────────────────────────────────────────────────────────────────────────────────────────────

def s2a_board():
    cols = f"""<div class="abs" style="left:40px;top:320px;width:1840px;height:580px;display:grid;grid-template-columns:repeat(3,1fr);gap:20px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><section class="sl" aria-label="{{{{s.alt}}}}" style="opacity:{{{{s.op}}}};display:flex;flex-direction:column;background:{GLASS};box-shadow:inset 0 0 0 1px rgba(67,76,137,.7)">
<div style="display:grid;grid-template-columns:150px 1fr;gap:16px;align-items:end;padding:18px 22px 0;height:250px">
<img src="{{{{s.face}}}}" alt="" style="width:150px;height:230px;object-fit:contain;object-position:50% 100%">
<div style="display:flex;flex-direction:column;gap:6px;align-self:center"><span class="lab">{{{{s.role}}}}</span><span style="font-size:28px">{{{{s.name}}}}</span>
<span style="font-size:15px;color:{INK}">{{{{s.line1}}}}</span><img class="hull" src="{{{{s.hull}}}}" alt="" style="width:100%;height:96px;margin-top:6px"></div></div>
<sc-if value="{{{{s.ok}}}}" hint-placeholder-val="{{{{ true }}}}"><div style="margin-top:14px">{r87.steppers()}</div></sc-if>
<sc-if value="{{{{s.locked}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="margin:auto;font-size:18px;color:{DIM}">{{{{s.owner}}}}</span></sc-if>
</section></sc-for></div>"""
    return window(summary("A") + cols + foot(),
                  f"Rev {REV} 2A - Three seats: a column per seat, its pilot and hull over its choices; Save as preset and Set sail at the foot (recommended) - interactive")


def s2b_board():
    seats = f"""<div class="abs" style="left:40px;top:320px;width:450px;display:flex;flex-direction:column;gap:10px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{{{s.pressed}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.pick}}}}" style="height:150px;display:grid;grid-template-columns:96px 1fr 150px;gap:12px;align-items:center;padding:0 14px;opacity:{{{{s.op}}}};background:{{{{s.rowBg}}}};box-shadow:{{{{s.edge}}}}">
<img src="{{{{s.face}}}}" alt="" style="width:96px;height:140px;align-self:end;object-fit:contain;object-position:50% 100%">
<span style="display:flex;flex-direction:column;gap:5px"><span class="lab">{{{{s.role}}}}</span><span style="font-size:20px">{{{{s.name}}}}</span><span style="font-size:14px;color:{INK}">{{{{s.line1}}}}</span><span style="font-size:14px;color:{DIM}">{{{{s.line2}}}}</span></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:150px;height:84px"></button></sc-for></div>"""
    tile_pic = ('<button class="pick sls" type="button" aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
                'style="width:430px;height:226px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:8px;padding-bottom:16px;'
                'background:#00010A;box-shadow:{{t.edge}}"><img class="hull" src="{{t.img}}" alt="" style="width:86%;height:150px">'
                f'<span style="font-size:18px;color:{INK}">{{{{t.name}}}}</span></button>')
    tile_word = ('<button class="pick sls" type="button" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
                 f'style="width:430px;height:110px;display:flex;align-items:center;justify-content:center;font-size:22px;color:{INK};'
                 'background:#00010A;box-shadow:{{t.edge}}">{{t.name}}</button>')
    picker = f"""<sc-if value="{{{{tray.open}}}}" hint-placeholder-val="{{{{ true }}}}"><section aria-label="The seat's choices" class="abs" style="left:520px;top:320px;width:1360px;height:644px;display:flex;flex-direction:column;gap:18px">
<div style="display:flex;align-items:flex-end;gap:34px"><span style="font-size:28px">{{{{picker.title}}}}</span>
<sc-for list="{{{{tray.tabs}}}}" as="k" hint-placeholder-count="4"><button class="pick cap" type="button" aria-pressed="{{{{k.pressed}}}}" onClick="{{{{k.pick}}}}" style="font-size:18px;color:{{{{k.col}}}};padding-bottom:6px;border-bottom:2px solid {{{{k.line}}}}">{{{{k.name}}}}</button></sc-for></div>
<sc-if value="{{{{tray.pics}}}}" hint-placeholder-val="{{{{ true }}}}"><div style="display:flex;flex-wrap:wrap;gap:16px"><sc-for list="{{{{tray.tiles}}}}" as="t" hint-placeholder-count="6">{tile_pic}</sc-for></div></sc-if>
<sc-if value="{{{{tray.words}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:flex;flex-wrap:wrap;gap:16px"><sc-for list="{{{{tray.tiles}}}}" as="t" hint-placeholder-count="4">{tile_word}</sc-for></div></sc-if>
</section></sc-if>"""
    return window(summary("B") + seats + picker + foot(),
                  f"Rev {REV} 2B - Seat and picker: the seats on the left, one large picker on the right with Vessel, Pilot, Element, Artifact - interactive")


def s2c_board():
    table = f"""<div class="abs" style="left:40px;top:320px;width:1840px;display:flex;flex-direction:column;gap:10px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><section class="sl" aria-label="{{{{s.alt}}}}" style="height:176px;opacity:{{{{s.op}}}};display:grid;grid-template-columns:470px 1fr;align-items:center;background:{GLASS};box-shadow:inset 0 0 0 1px rgba(67,76,137,.7)">
<div style="display:grid;grid-template-columns:96px 1fr 170px;gap:12px;align-items:center;padding:0 0 0 18px;height:100%">
<img src="{{{{s.face}}}}" alt="" style="width:96px;height:160px;align-self:end;object-fit:contain;object-position:50% 100%">
<span style="display:flex;flex-direction:column;gap:5px"><span class="lab">{{{{s.role}}}}</span><span style="font-size:22px">{{{{s.name}}}}</span><span style="font-size:14px;color:{INK}">{{{{s.line1}}}}</span></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:170px;height:90px"></div>
<sc-if value="{{{{s.ok}}}}" hint-placeholder-val="{{{{ true }}}}">{r87.steppers(cols=5).replace("padding:4px 18px 16px", "padding:0 22px 0 18px")}</sc-if>
<sc-if value="{{{{s.locked}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="padding-left:18px;font-size:18px;color:{DIM}">{{{{s.owner}}}}</span></sc-if>
</section></sc-for>
<sc-if value="{{{{party.has}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="font-size:15px;color:{INK};margin-top:8px">{{{{party.line}}}}. Step a seat's Flown by to seat them.</span></sc-if>
</div>"""
    return window(summary("C") + table + foot(),
                  f"Rev {REV} 2C - Roster: one table, a row per seat and a stepper per choice - interactive")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"I dont like the preview earlier we could zoom in and out of the whole map, set course directly there ... first "
                  "screen in voyages is the left side list, that can stay, the middle becomes the preview with all features ... squad "
                  "presets and set sail directly ... the second image can give us the options to select the vessel, pilot, artifact, "
                  "element etc and then set sail. Create 3 mockups each for both the screens\" (Shombith, 2026-11-12)."),
        ("The map, in all three", "Flat again, and the explorer: a zoom ladder (Galaxy, Arm, Sector, Course) with + and -, and when "
                                  "zoomed out a press on a place sets the course there. The edge-on view stays with it."),
        ("Screen 1", "1A Presets (recommended): the squad presets are cards, so Set sail is one press away. 1B Wide map: the map "
                     "takes the window and a foot bar holds the rest. 1C Briefing: the tallest map, and the squad's seats are shown."),
        ("Configure squad", "On every screen 1. It names a lobby friend who has no seat yet, and opens screen 2."),
        ("Screen 2", "2A Three seats (recommended): a column per seat, everything visible at once. 2B Seat and picker: large "
                     "pictures to choose from, one seat at a time. 2C Roster: the most compact, a table."),
        ("A lobby friend's seat", "A seat you may change has a fifth stepper, Flown by: the AI or a friend in the lobby."),
        ("The top bar", "As Rev 88 drew it: small plain capitals, the shown tab underlined. The build still shows boxed tabs; "
                        "that is a fault of the build, to be fixed with whichever of these is picked."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:22px">'
            f'<span style="font-size:36px">Rev {REV} - Voyages as two screens: the voyage on the map, then the squad</span>{items}</div>')
    return page(f"Rev {REV} - notes: what was asked, the map, the three of each screen, the recommendations", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}S1A.dc.html": s1a_board, f"R{REV}S1B.dc.html": s1b_board, f"R{REV}S1C.dc.html": s1c_board,
          f"R{REV}S2A.dc.html": s2a_board, f"R{REV}S2B.dc.html": s2b_board, f"R{REV}S2C.dc.html": s2c_board,
          f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
