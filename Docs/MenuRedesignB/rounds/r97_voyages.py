#!/usr/bin/env python3
"""Rev 97 boards: the two parts Shombith did not like, redrawn - the squad presets and the seat's choices (Frogma tab
"Voyages", Docs/MENU_REDESIGN_B.md section 182).

Shombith, 2026-11-12, on Rev 96, with two screenshots: the Squad block of 1A (two dark plates, three small hulls on
each) and 2D's tray with its foot (dark boxed tiles, small pictures): "These screenshots are the parts I do not like".
He gave no more words, so this round reads the screenshots: both parts are rows of hard dark boxes with small pictures
in them and empty dark between.

So in both parts here the boxes go. Pictures are larger and stand on the screen itself; the picked one is lit and
underlined in Jade, the others are dimmed; a band behind a group is solid in the middle and fades out at both ends.

  Squad   the squad presets three ways: Formation (each squad its three hulls in formation), Carousel (one squad
          large, step to the next), Crew (each squad as its three pilots)
  Choices the picked seat's choices three ways: Shelf (four open groups, large pictures), Two rows (the vessels
          across the whole width, the rest under them), Focus (each group shows its picked choice large)
  1A      screen 1 with the Formation presets
  2D      the Deck with the Shelf
  2G, 2H  three players at once (Shombith, while this round was drawn: "3 players are selecting their vessel, pilot,
          element etc it should be a good experience"): every seat is a lane; each player's own lane is open with
          every choice, the others show live; 2G is the leader's screen (Set sail), 2H a friend's (Ready)
  (no notes board: the notes are this docstring and the doc section)

    python3 Docs/MenuRedesignB/rounds/r97_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "97"
sys.argv = [sys.argv[0], _root, "96"]          # Rev 96's module keeps its names; this round's files are R97*
_spec = importlib.util.spec_from_file_location("r96", HERE / "r96_voyages.py")
r96 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r96)
r95, r89, r87 = r96.r95, r96.r89, r96.r87
r70 = r89.r88.r70

REV, ROOT, page = int(_rev), r96.ROOT, r96.page
JADE, INK, DIM, LINE = r96.JADE, r96.INK, r96.DIM, r96.LINE


def _petals():
    """The element marks are the HUD's own flowers (ElementalBarsView, Docs/claude/VESSEL_HUD_AND_ELEMENTS.md): five
    copies of the element's white petal (Assets/Resources/ElementPetals), each filling the flower's square and turned
    72 degrees about its centre. Carried in the board as data, so they need no upload. Shombith, 2026-11-12: "the ones
    with petals are the ones we should use"."""
    import base64, io
    from PIL import Image
    out = {}
    for name in ("Charge", "Mass", "Space", "Time"):
        petal = Image.open(HERE.parents[2] / "Assets/Resources/ElementPetals" / f"{name.lower()}_petal.png").convert("RGBA")
        flower = Image.new("RGBA", petal.size, (0, 0, 0, 0))
        for n in range(5):
            flower = Image.alpha_composite(flower, petal.rotate(-72 * n, resample=Image.BICUBIC))
        flower = flower.crop(flower.getbbox()).resize((128, 128), Image.LANCZOS)
        buf = io.BytesIO()
        flower.save(buf, "PNG", optimize=True)
        out[name] = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
    return out


ELEMENT = _petals()

VALS = """  const ELEMENT = """ + json.dumps(ELEMENT) + """;
  const lit = (t) => { const on = t.pressed === 'true'; t.op = on ? 1 : 0.6; t.under = on ? '#00D4FF' : 'rgba(0,0,0,0)';
    t.pool = on ? 'rgba(230,233,255,.22)' : 'rgba(230,233,255,0)'; t.on = on; return t; };
  ['hulls', 'pilots', 'els', 'arts', 'kits'].forEach((k) => strips[k].forEach(lit));
  strips.els.forEach((t) => { t.img = ELEMENT[t.name]; });
  const now = (list) => list.find((t) => t.on) || { name: '', img: '', note: '' };
  const rest = (list) => list.filter((t) => !t.on);
  const chosen = { hull: now(strips.hulls), pilot: now(strips.pilots), el: now(strips.els), art: now(strips.arts),
    hulls: rest(strips.hulls), pilots: rest(strips.pilots), els: rest(strips.els), arts: rest(strips.arts) };
  presetCards.forEach((p, i) => { const on = i === st.sq; p.op = on ? 1 : 0.55; p.under = on ? '#00D4FF' : 'rgba(0,0,0,0)';
    p.pool = on ? 'rgba(230,233,255,.20)' : 'rgba(230,233,255,.06)';
    p.h0 = HULL[st.books[i][0].v]; p.h1 = HULL[st.books[i][1].v]; p.h2 = HULL[st.books[i][2].v];
    p.crew = st.books[i].map((x) => ({ face: PILOT[x.p], who: x.who === 'AI wingmate' ? 'AI' : x.who, vessel: x.v })); });
  squadNow.h0 = HULL[seats[0].v]; squadNow.h1 = HULL[seats[1].v]; squadNow.h2 = HULL[seats[2].v];
  squadNow.crew = seats.map((x) => ({ who: x.who === 'AI wingmate' ? 'AI' : x.who, vessel: x.v }));
  squadNow.dots = SQ.map((s, i) => ({ col: i === st.sq ? '#00D4FF' : '#434C89' }));
  const meReady = !!st.rdy;
  seatList.forEach((s, i) => { const who = seats[i].who, ai = who === 'AI wingmate', me = who === 'You';
    s.isOpen = i === fi; s.canOpen = i !== fi && canEdit(i); s.watch = !canEdit(i);
    s.player = me ? 'You' : who;
    s.status = me ? (meReady ? 'Ready' : 'Choosing') : ai ? (canEdit(i) ? 'Yours to set' : 'Set by ' + q.owner) : (q.mine ? 'Ready' : 'Choosing');
    s.markCol = s.status === 'Ready' ? '#E6E9FF' : 'rgba(0,0,0,0)';
    s.watchLine = ai ? '' : who + ' picks on their own screen. You see it as it changes.';
    s.el = seats[i].e; s.elImg = ELEMENT[seats[i].e]; s.art = seats[i].a; s.pilot = seats[i].p;
    s.laneOp = s.isOpen ? 1 : 0.78; s.just = s.isOpen ? 'flex-start' : 'center'; s.pad = s.isOpen ? '22px' : '0px';
    s.laneBg = s.isOpen ? 'linear-gradient(180deg,rgba(0,4,31,0) 0,rgba(0,4,31,.82) 10%,rgba(0,4,31,.82) 90%,rgba(0,4,31,0) 100%)' : 'none'; });
  const friends = seats.filter((x) => x.who !== 'You' && x.who !== 'AI wingmate').map((x) => x.who);
  const lanes = { cols: seatList.map((s) => s.isOpen ? '820px' : '1fr').join(' '),
    cta: q.mine ? 'Set sail' : meReady ? 'Not ready' : 'Ready', press: () => this.setState({ rdy: !meReady }),
    line: q.mine ? (friends.length ? friends.join(' and ') + (friends.length > 1 ? ' are' : ' is') + ' ready. Set sail when you are.' : 'The other seats are yours to set.')
      : meReady ? 'You are ready. ' + q.owner + ' sets sail.' : q.owner + ' sets sail when everyone is ready.' };
"""


def logic():
    lg = r96.logic()
    old = "  return { voyage, vlist, tabs, presets, seatList, tray, hero, map, party, presetCards, squadNow, picker, focus, strips };"
    assert old in lg
    return lg.replace(old, VALS + "  return { voyage, vlist, tabs, presets, seatList, tray, hero, map, party, presetCards, squadNow, picker, focus, strips, chosen, lanes };")


def window(body, title, backdrop="", start_squad=0):
    lg = logic()
    if start_squad:
        assert " sq: 0," in lg
        lg = lg.replace(" sq: 0,", " sq: %d," % start_squad, 1)
    return r89.window(body, title, ground=r89.GROUND + backdrop, lg=lg)


# A band behind a group: solid in the middle, fading out at both ends (no box, no corners).
BAND = "linear-gradient(90deg,rgba(0,4,31,0) 0,rgba(0,4,31,.84) 14%,rgba(0,4,31,.84) 86%,rgba(0,4,31,0) 100%)"
POOL = "radial-gradient(ellipse 50% 50% at 50% 50%,{c},rgba(230,233,255,0))"


def formation(h0, h1, h2, w, h, lead):
    """Three hulls in formation: the leader in front, the two rogues behind it to either side."""
    side = round(lead * 0.72)
    return (f'<span style="position:relative;display:block;width:{w}px;height:{h}px">'
            f'<img class="hull" src="{h1}" alt="" style="position:absolute;left:0;top:0;width:{side}px;height:{round(side / 3)}px;opacity:.85">'
            f'<img class="hull" src="{h2}" alt="" style="position:absolute;right:0;top:0;width:{side}px;height:{round(side / 3)}px;opacity:.85">'
            f'<img class="hull" src="{h0}" alt="" style="position:absolute;left:{(w - lead) // 2}px;bottom:0;width:{lead}px;height:{round(lead / 3)}px">'
            '</span>')


# ── The squad presets, three ways ─────────────────────────────────────────────────────────────────────────────────────

def squad_formation():
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Squad</span>'
            '<sc-for list="{{presetCards}}" as="p" hint-placeholder-count="2"><button class="pick" type="button" aria-pressed="{{p.pressed}}" '
            f'onClick="{{{{p.pick}}}}" style="position:relative;height:168px;opacity:{{{{p.op}}}};background:{BAND}">'
            f'<span aria-hidden="true" style="position:absolute;left:8%;right:8%;top:40px;height:110px;background:{POOL.format(c="{{p.pool}}")}"></span>'
            '<span style="position:absolute;left:30px;top:14px;display:flex;align-items:baseline;gap:14px">'
            '<span style="font-size:22px;padding-bottom:4px;border-bottom:2px solid {{p.under}}">{{p.name}}</span>'
            f'<span style="font-size:14px;color:{INK}">{{{{p.who}}}}</span></span>'
            '<span style="position:absolute;left:45px;top:52px">' + formation("{{p.h0}}", "{{p.h1}}", "{{p.h2}}", 360, 108, 230)
            + '</span></button></sc-for></div>')


def squad_carousel():
    arrow = lambda fn, lab, svg, side: (f'<button class="pick" type="button" aria-label="{lab} squad" onClick="{{{{squadNow.{fn}}}}}" '
                                        f'style="position:absolute;{side}:0;top:96px;width:48px;height:64px;display:flex;align-items:center;'
                                        f'justify-content:center;color:{INK}">{svg}</button>')
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Squad</span>'
            f'<div style="position:relative;height:290px;background:{BAND}">'
            f'<span aria-hidden="true" style="position:absolute;left:6%;right:6%;top:80px;height:150px;background:{POOL.format(c="rgba(230,233,255,.20)")}"></span>'
            '<span style="position:absolute;left:0;right:0;top:16px;text-align:center;font-size:22px">{{squadNow.name}}</span>'
            '<span style="position:absolute;left:55px;top:64px">' + formation("{{squadNow.h0}}", "{{squadNow.h1}}", "{{squadNow.h2}}", 340, 150, 250)
            + '</span>' + arrow("prev", "Previous", r87.CHEV_L, "left") + arrow("next", "Next", r87.CHEV_R, "right")
            + '<span style="position:absolute;left:30px;right:30px;top:226px;display:grid;grid-template-columns:repeat(3,1fr);text-align:center">'
            '<sc-for list="{{squadNow.crew}}" as="c" hint-placeholder-count="3"><span style="display:flex;flex-direction:column;gap:2px">'
            f'<span style="font-size:15px">{{{{c.vessel}}}}</span><span style="font-size:14px;color:{DIM}">{{{{c.who}}}}</span></span></sc-for></span>'
            '<span style="position:absolute;left:0;right:0;bottom:8px;display:flex;justify-content:center;gap:8px">'
            '<sc-for list="{{squadNow.dots}}" as="d" hint-placeholder-count="2"><span style="width:8px;height:8px;transform:rotate(45deg);background:{{d.col}}"></span>'
            '</sc-for></span></div></div>')


def squad_crew():
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Squad</span>'
            '<sc-for list="{{presetCards}}" as="p" hint-placeholder-count="2"><button class="pick" type="button" aria-pressed="{{p.pressed}}" '
            f'onClick="{{{{p.pick}}}}" style="height:168px;opacity:{{{{p.op}}}};background:{BAND};display:flex;flex-direction:column;align-items:center;gap:8px;padding-top:12px">'
            '<span style="font-size:22px;padding-bottom:4px;border-bottom:2px solid {{p.under}}">{{p.name}}</span>'
            '<span style="display:grid;grid-template-columns:repeat(3,116px);gap:8px">'
            '<sc-for list="{{p.crew}}" as="c" hint-placeholder-count="3"><span style="display:grid;grid-template-columns:48px 1fr;gap:6px;align-items:center">'
            '<img src="{{c.face}}" alt="" style="width:48px;height:92px;object-fit:contain;object-position:50% 100%">'
            f'<span style="display:flex;flex-direction:column;gap:2px"><span style="font-size:14px">{{{{c.vessel}}}}</span>'
            f'<span style="font-size:14px;color:{DIM}">{{{{c.who}}}}</span></span></span></sc-for></span></button></sc-for></div>')


def squad_compare():
    cols = [("Formation (drawn on 1A)", "Each squad is its three hulls in formation, the leader in front. No plate: the picked squad is lit and underlined.", squad_formation()),
            ("Carousel", "One squad at a time, large. Step to the next; the marks under it say how many there are.", squad_carousel()),
            ("Crew", "Each squad is its three pilots, with what each flies.", squad_crew())]
    cells = "".join(f'<div style="display:flex;flex-direction:column;gap:14px;width:450px"><span class="cap" style="font-size:18px">{n}</span>'
                    f'<span style="font-size:15px;line-height:1.5;color:{INK};min-height:68px">{d}</span>{b}'
                    f'<div style="margin-top:14px;display:flex;flex-direction:column;gap:12px">{r96.quiet("X", "Configure squad")}{r96.sail()}</div></div>'
                    for n, d, b in cols)
    body = (f'<div class="abs" style="left:80px;top:56px;width:1760px;display:flex;flex-direction:column;gap:30px">'
            f'<span style="font-size:36px">Rev {REV} - the squad presets, three ways (in place of the two dark plates)</span>'
            f'<div style="display:flex;justify-content:space-between">{cells}</div></div>')
    return page(f"Rev {REV} Squad - the squad presets three ways: Formation, Carousel, Crew - interactive", r89.GROUND + body, logic(), h=1080)


# ── The seat's choices, three ways ────────────────────────────────────────────────────────────────────────────────────

def open_tile(listname, w, h, kind, count, img_h=0, size=15, note=False, gap=4):
    """One choice with no box: its picture on the screen, its name under it; the picked one lit and underlined."""
    img = ""
    if kind == "hull":
        img = f'<img class="hull" src="{{{{t.img}}}}" alt="" style="position:relative;width:94%;height:{img_h}px">'
    elif kind == "face":
        img = f'<img src="{{{{t.img}}}}" alt="" style="position:relative;width:100%;height:{img_h}px;object-fit:contain;object-position:50% 100%">'
    elif kind == "icon":
        img = f'<img src="{{{{t.img}}}}" alt="" style="position:relative;width:{img_h}px;height:{img_h}px;object-fit:contain">'
    sub = f'<span style="font-size:14px;color:{DIM};min-height:18px">{{{{t.note}}}}</span>' if note else ''
    pool = (f'<span aria-hidden="true" style="position:absolute;left:0;right:0;top:0;height:{img_h + 10}px;background:{POOL.format(c="{{t.pool}}")}"></span>'
            if kind != "word" else '')
    return (f'<sc-for list="{{{{{listname}}}}}" as="t" hint-placeholder-count="{count}"><button class="pick" type="button" aria-label="{{{{t.name}}}}" '
            f'aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" style="position:relative;width:{w}px;height:{h}px;opacity:{{{{t.op}}}};display:flex;'
            f'flex-direction:column;align-items:center;justify-content:flex-end;gap:{gap}px">{pool}{img}'
            f'<span style="position:relative;font-size:{size}px;color:{INK};padding-bottom:3px;border-bottom:2px solid {{{{t.under}}}}">{{{{t.name}}}}</span>{sub}</button></sc-for>')


def group(label, inner, style="", gap=10, between=10):
    return (f'<div style="display:flex;flex-direction:column;gap:{gap}px;{style}"><span class="lab" style="color:{INK}">{label}</span>'
            f'<div style="display:flex;flex-wrap:wrap;gap:{between}px;align-items:flex-end">{inner}</div></div>')


def shelf(x=40, y=668, w=1840):
    return f"""<section aria-label="The picked seat's choices" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:296px;z-index:2;background:{BAND}">
<div class="abs" style="left:150px;right:150px;top:16px;bottom:10px;display:grid;grid-template-columns:600px 380px 250px 1fr;gap:34px">
{group("Vessel", open_tile("strips.hulls", 192, 112, "hull", 6, img_h=62, size=14))}
{group("Pilot", open_tile("strips.pilots", 87, 236, "face", 4, img_h=176, size=14, note=True))}
{group("Element", open_tile("strips.els", 120, 106, "icon", 4, img_h=54))}
{group("Artifact", open_tile("strips.arts", 112, 36, "word", 5))}
</div></section>"""


def two_rows(x=40, y=668, w=1840):
    return f"""<section aria-label="The picked seat's choices" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:296px;z-index:2;background:{BAND}">
<div class="abs" style="left:150px;right:150px;top:14px;bottom:8px;display:flex;flex-direction:column;gap:6px">
<div style="display:flex;justify-content:space-between;align-items:flex-end">{open_tile("strips.hulls", 246, 118, "hull", 6, img_h=80)}</div>
<div style="display:grid;grid-template-columns:560px 420px 1fr;gap:34px;align-items:end">
<div style="display:flex;gap:10px;align-items:flex-end">{open_tile("strips.pilots", 132, 144, "face", 4, img_h=104, size=14, note=False)}</div>
<div style="display:flex;gap:10px;align-items:flex-end">{open_tile("strips.els", 96, 92, "icon", 4, img_h=46)}</div>
<div style="display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end">{open_tile("strips.arts", 104, 42, "word", 5, size=14)}</div>
</div></div></section>"""


def focus_groups(x=40, y=668, w=1840):
    def col(label, big, small):
        return (f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab" style="color:{INK}">{label}</span>'
                f'<div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;align-items:end">{big}'
                f'<div style="display:flex;flex-wrap:wrap;gap:6px;align-items:flex-end">{small}</div></div></div>')
    big = lambda inner, name: (f'<div style="position:relative;height:216px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px">'
                               f'<span aria-hidden="true" style="position:absolute;left:0;right:0;top:20px;height:150px;background:{POOL.format(c="rgba(230,233,255,.22)")}"></span>'
                               f'{inner}<span style="position:relative;font-size:22px;padding-bottom:3px;border-bottom:2px solid {JADE}">{name}</span></div>')
    return f"""<section aria-label="The picked seat's choices" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:296px;z-index:2;background:{BAND}">
<div class="abs" style="left:150px;right:150px;top:14px;bottom:8px;display:grid;grid-template-columns:480px 380px 300px 1fr;gap:30px">
{col("Vessel", big('<img class="hull" src="{{chosen.hull.img}}" alt="" style="position:relative;width:100%;height:80px">', "{{chosen.hull.name}}"),
     open_tile("chosen.hulls", 112, 64, "hull", 5, img_h=36, size=14))}
{col("Pilot", big('<img src="{{chosen.pilot.img}}" alt="" style="position:relative;width:100%;height:170px;object-fit:contain;object-position:50% 100%">', "{{chosen.pilot.name}}"),
     open_tile("chosen.pilots", 56, 108, "face", 3, img_h=84, size=14))}
{col("Element", big('<img src="{{chosen.el.img}}" alt="" style="position:relative;width:76px;height:76px;object-fit:contain;margin-bottom:40px">', "{{chosen.el.name}}"),
     open_tile("chosen.els", 66, 66, "icon", 3, img_h=34, size=14))}
{col("Artifact", big('', "{{chosen.art.name}}"), open_tile("chosen.arts", 112, 40, "word", 4, size=14))}
</div></section>"""


def choices_compare():
    rows = [("Shelf (drawn on 2D)", "Four open groups; every picture stands on the screen, larger. The picked one is lit and underlined.", shelf(0, 0, 1760)),
            ("Two rows", "The six vessels across the whole width, as large as they go; pilots, elements and artifacts under them.", two_rows(0, 0, 1760)),
            ("Focus", "Each group shows what is picked, large, with the others small beside it. One press on a small one swaps it in.", focus_groups(0, 0, 1760))]
    blocks = "".join(f'<div style="display:flex;flex-direction:column;gap:4px"><div style="display:flex;align-items:baseline;gap:26px">'
                     f'<span class="cap" style="font-size:18px">{n}</span><span style="font-size:15px;color:{INK}">{d}</span></div>'
                     f'<div style="position:relative;height:296px">{b}</div></div>' for n, d, b in rows)
    body = (f'<div class="abs" style="left:80px;top:20px;width:1760px;display:flex;flex-direction:column;gap:8px">'
            f'<span style="font-size:28px">Rev {REV} - the seat\'s choices, three ways (in place of the boxed tray)</span>{blocks}</div>')
    return page(f"Rev {REV} Choices - the picked seat's choices three ways: Shelf, Two rows, Focus - interactive",
                r89.GROUND + r96.deck() + body, logic(), h=1080)


# ── The two screens with the recommended parts ────────────────────────────────────────────────────────────────────────

def s1a_board():
    right = f"""<section aria-label="The voyage and the squad" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
{r95.name_block()}
<div style="margin-top:22px">{squad_formation()}</div>
<div style="margin-top:auto;display:flex;flex-direction:column;gap:10px">
<sc-if value="{{{{party.has}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="font-size:15px;line-height:1.4;color:{INK};text-align:center">{{{{party.line}}}}</span></sc-if>
{r96.quiet("X", "Configure squad", href=f"R{REV}S2D.dc.html")}</div>
<div style="margin-top:12px">{r96.sail()}</div>
</section>"""
    return window(r95.chips() + r95.explorer(380, 244, 1020, 592, 204) + right,
                  f"Rev {REV} 1A - Screen 1 with the squads as formations in place of the dark plates - interactive")


def s2d_board():
    stage = f"""<div class="abs" style="left:40px;top:316px;width:1840px;height:330px;display:grid;grid-template-columns:repeat(3,1fr);gap:20px;z-index:2">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{{{s.focused}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.focus}}}}" style="position:relative;height:330px;opacity:{{{{s.dim}}}}">
<span aria-hidden="true" style="position:absolute;left:6%;right:6%;bottom:44px;height:120px;background:{POOL.format(c="{{s.pool}}")}"></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="position:absolute;left:104px;top:36px;width:480px;height:170px">
<img src="{{{{s.face}}}}" alt="" style="position:absolute;left:10px;top:20px;width:150px;height:200px;object-fit:contain;object-position:50% 100%">
<span style="position:absolute;left:0;right:0;bottom:0;display:flex;flex-direction:column;align-items:center;gap:4px">
<span class="lab" style="color:{INK}">{{{{s.role}}}}</span>
<span style="font-size:28px;padding-bottom:4px;border-bottom:2px solid {{{{s.under}}}}">{{{{s.name}}}}</span>
<span style="font-size:15px;color:{INK}">{{{{s.line1}}}}</span><span style="font-size:14px;color:{DIM}">{{{{s.line2}}}}</span></span>
</button></sc-for></div>"""
    return window(r96.summary().replace(f"R{r96.REV}S1A", f"R{REV}S1A") + stage + shelf() + r96.foot(),
                  f"Rev {REV} 2D - The Deck with the Shelf in place of the boxed tray: every choice an open picture, one press - interactive",
                  backdrop=r96.deck())


# ── Three players at once: every seat a lane, each player picks in their own ─────────────────────────────────────────

def lanes_board(start_squad, title):
    """Shombith, 2026-11-12: "if you are configuring, 3 players are selecting their vessel, pilot, element etc it
    should be a good experience". So nobody shares a cursor: each player's screen opens THEIR lane with every choice
    in it, and shows the other two seats as large live pictures with who is ready. The leader also sets the AI's seat."""
    choices = (f'<div style="display:flex;flex-direction:column;gap:10px;padding:6px 26px 0">'
               + group("Vessel", open_tile("strips.hulls", 116, 72, "hull", 6, img_h=38, size=14))
               + '<div style="display:grid;grid-template-columns:350px 190px 1fr;gap:14px">'
               + group("Pilot", open_tile("strips.pilots", 78, 128, "face", 4, img_h=96, size=14))
               + group("Element", open_tile("strips.els", 88, 64, "icon", 4, img_h=30, size=14))
               + group("Artifact", open_tile("strips.arts", 100, 30, "word", 5, size=14)) + '</div></div>')
    lane = f"""<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><section aria-label="{{{{s.alt}}}}" style="position:relative;height:664px;display:flex;flex-direction:column;justify-content:{{{{s.just}}}};gap:8px;padding-top:{{{{s.pad}}}};box-sizing:border-box;opacity:{{{{s.laneOp}}}};background:{{{{s.laneBg}}}}">
<div style="display:flex;flex-direction:column;align-items:center;gap:2px">
<span class="lab" style="color:{INK}">{{{{s.role}}}}</span>
<span style="display:flex;align-items:center;gap:10px"><span style="font-size:22px">{{{{s.player}}}}</span>
<span aria-hidden="true" style="width:8px;height:8px;transform:rotate(45deg);background:{{{{s.markCol}}}}"></span>
<span style="font-size:15px;color:{INK}">{{{{s.status}}}}</span></span></div>
<div style="position:relative;height:170px;flex:none;margin:0 auto;width:440px">
<span aria-hidden="true" style="position:absolute;left:0;right:0;bottom:0;height:110px;background:{POOL.format(c="{{s.pool}}")}"></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="position:absolute;left:70px;top:26px;width:370px;height:124px">
<img src="{{{{s.face}}}}" alt="" style="position:absolute;left:0;top:6px;width:110px;height:160px;object-fit:contain;object-position:50% 100%"></div>
<div style="display:flex;flex-direction:column;align-items:center;gap:6px">
<span style="font-size:28px;padding-bottom:3px;border-bottom:2px solid {{{{s.under}}}}">{{{{s.name}}}}</span>
<span style="display:flex;align-items:center;gap:10px;font-size:15px;color:{INK}"><span>{{{{s.pilot}}}}</span>
<img src="{{{{s.elImg}}}}" alt="" style="width:20px;height:20px;object-fit:contain"><span>{{{{s.el}}}}</span><span>{{{{s.art}}}}</span></span></div>
<div><sc-if value="{{{{s.isOpen}}}}" hint-placeholder-val="{{{{ true }}}}">{choices}</sc-if></div>
<div><sc-if value="{{{{s.canOpen}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="margin:22px auto 0;width:240px"><button class="btn" type="button" onClick="{{{{s.focus}}}}" style="width:100%;height:44px;font-size:14px;{r96.QUIET_SOFT}">Change</button></div></sc-if></div>
<div><sc-if value="{{{{s.watch}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="display:block;margin:18px auto 0;width:340px;text-align:center;font-size:14px;line-height:1.5;color:{DIM}">{{{{s.watchLine}}}}</span></sc-if></div>
</section></sc-for>"""
    stage = (f'<div class="abs" style="left:40px;top:308px;width:1840px;height:664px;display:grid;grid-template-columns:{{{{lanes.cols}}}};gap:14px;z-index:2">{lane}</div>')
    foot = (f'<div class="abs" style="left:40px;top:984px;width:1840px;height:56px;display:flex;align-items:center;z-index:3">'
            + r96.quiet("Y", "Save as preset", style="width:300px")
            + f'<span style="margin-left:auto;margin-right:26px;font-size:15px;color:{INK}">{{{{lanes.line}}}}</span>'
            + f'<div style="width:520px"><button class="btn cta" type="button" onClick="{{{{lanes.press}}}}" style="width:100%;{r96.CTA_SOFT}">'
              '<span class="kc">A</span>{{lanes.cta}}</button></div></div>')
    return window(r96.summary().replace(f"R{r96.REV}S1A", f"R{REV}S1A") + stage + foot, title, backdrop=r96.deck(), start_squad=start_squad)


BOARDS = {f"R{REV}Squad.dc.html": squad_compare, f"R{REV}Choices.dc.html": choices_compare,
          f"R{REV}S1A.dc.html": s1a_board, f"R{REV}S2D.dc.html": s2d_board,
          f"R{REV}S2G.dc.html": lambda: lanes_board(0, f"Rev {REV} 2G - Three players at once, the leader's screen: your lane open with every choice, the others live - interactive"),
          f"R{REV}S2H.dc.html": lambda: lanes_board(1, f"Rev {REV} 2H - The same screen for a friend in the squad: their lane open, Ready in place of Set sail - interactive")}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
