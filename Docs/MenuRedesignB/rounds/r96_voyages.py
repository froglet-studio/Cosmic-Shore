#!/usr/bin/env python3
"""Rev 96 boards: the squad screen redrawn - a real backdrop, softer buttons, and one press for a choice (Frogma tab
"Voyages", Docs/MENU_REDESIGN_B.md section 181).

Shombith, 2026-11-12, on Rev 95: "S1A is good but I didnt like any of the second scren designs, the bg is too bland
and also the button in both the screens are too hard not soft hard soft. Clicking through so many vessels and pilots
and artifacts also seems like a drag, we need to make the experience smoother for the users".

Three answers, in every board here:
  - The backdrop: the squad stands on a lit deck over the sea of the voyage that was picked (its own course map,
    dimmed), not on the bare menu ground.
  - The buttons: "soft hard soft" is read as the fill being solid in the middle and fading out to both ends, with no
    hard box. The Buttons board sets that reading beside two others, so the reading itself can be picked.
  - No stepping. Every choice is on screen and takes one press. Picking a vessel brings the pilot and artifact kept
    for it in the Hangar, so most seats are one press.

  1A   Screen 1 as picked (Rev 95 1A), with the soft buttons
  Buttons  three readings of "soft hard soft" on Set sail and Configure squad
  2D  Deck      the three seats stand across the deck; the picked seat's four choices are one tray under them (recommended)
  2E  Kits      the picked seat large; six whole kits from the Hangar, one press sets vessel, pilot, element and artifact
  2F  Showroom  a rail of seats, the picked seat as the hero, its four choices as strips beside it
  Notes

    python3 Docs/MenuRedesignB/rounds/r96_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "96"
sys.argv = [sys.argv[0], _root, "95"]          # Rev 95's module keeps its names; this round's files are R96*
_spec = importlib.util.spec_from_file_location("r95", HERE / "r95_voyages.py")
r95 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r95)
r89, r87 = r95.r89, r95.r87

REV, ROOT, page = int(_rev), r95.ROOT, r95.page
JADE, INK, DIM, LINE, GLASS = r95.JADE, r95.INK, r95.DIM, r95.LINE, r95.GLASS

# The kit each hull keeps in the Hangar (the board's own content): picking the hull brings it.
KIT = {"Sparrow": {"p": "Shrapnel", "e": "Charge", "a": "Extra Bay"}, "Dolphin": {"p": "Feist", "e": "Space", "a": "Seed Pod"},
       "Squirrel": {"p": "Longshift", "e": "Time", "a": "No artifact"}, "Rhino": {"p": "Flux", "e": "Mass", "a": "Long Edge"},
       "Manta": {"p": "Shrapnel", "e": "Charge", "a": "Tailwind"}, "Serpent": {"p": "Feist", "e": "Space", "a": "No artifact"}}

VALS = """  const KIT = """ + json.dumps(KIT) + """;
  const firstOk = seats.findIndex((s, i) => canEdit(i));
  const fi = st.open >= 0 && canEdit(st.open) ? st.open : firstOk;
  const fs = fi >= 0 ? seats[fi] : null;
  const mark = (on) => on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.7)';
  seatList.forEach((s, i) => { s.focus = canEdit(i) ? () => this.setState({ open: i }) : () => {}; s.lit = i === fi;
    s.dim = i === fi ? 1 : canEdit(i) ? 0.62 : 0.4; s.under = i === fi ? '#00D4FF' : 'rgba(0,0,0,0)';
    s.pool = i === fi ? 'rgba(230,233,255,.20)' : 'rgba(230,233,255,.07)'; s.focused = i === fi ? 'true' : 'false';
    s.railBg = i === fi ? 'rgba(0,4,31,.94)' : 'rgba(0,4,31,.62)'; s.railEdge = i === fi ? 'inset 3px 0 0 #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.7)'; });
  const focus = fs ? { has: true, role: fs.role, name: fs.v, who: (fs.who === 'AI wingmate' ? 'AI' : fs.who) + ' - ' + fs.p, kit: fs.e + ' - ' + fs.a,
    face: PILOT[fs.p], hull: HULL[fs.v] } : { has: false, role: '', name: '', who: '', kit: '', face: '', hull: '' };
  const pick = (patch) => () => { if (fi >= 0) set(fi, patch); };
  const strips = fs ? {
    hulls: Object.keys(HULL).map((h) => ({ name: h, img: HULL[h], note: KIT[h].p, pressed: fs.v === h ? 'true' : 'false', edge: mark(fs.v === h),
      pick: pick({ v: h, p: KIT[h].p, e: KIT[h].e, a: KIT[h].a }) })),
    pilots: Object.keys(PILOT).map((p) => { const j = seats.findIndex((x, k) => k !== fi && x.p === p);
      return { name: p, img: PILOT[p], note: j >= 0 ? 'Swaps with ' + seats[j].v : '', pressed: fs.p === p ? 'true' : 'false', edge: mark(fs.p === p), pick: pick({ p: p }) }; }),
    els: ELEMENTS.map((e) => ({ name: e, pressed: fs.e === e ? 'true' : 'false', edge: mark(fs.e === e), pick: pick({ e: e }) })),
    arts: ARTIFACTS.map((a) => ({ name: a, pressed: fs.a === a ? 'true' : 'false', edge: mark(fs.a === a), pick: pick({ a: a }) })),
    kits: Object.keys(HULL).map((h) => { const k = KIT[h], on = fs.v === h && fs.p === k.p && fs.e === k.e && fs.a === k.a;
      return { name: h, img: HULL[h], face: PILOT[k.p], pilot: k.p, kit: k.e + ' - ' + k.a, pressed: on ? 'true' : 'false', edge: mark(on),
        pick: pick({ v: h, p: k.p, e: k.e, a: k.a }) }; }),
  } : { hulls: [], pilots: [], els: [], arts: [], kits: [] };
"""


def logic():
    lg = r95.logic()
    old = "  return { voyage, vlist, tabs, presets, seatList, tray, hero, map, party, presetCards, squadNow, picker };"
    assert old in lg
    return lg.replace(old, VALS + "  return { voyage, vlist, tabs, presets, seatList, tray, hero, map, party, presetCards, squadNow, picker, focus, strips };")


# ── The soft buttons ──────────────────────────────────────────────────────────────────────────────────────────────────
# "Soft hard soft": solid in the middle, fading out to both ends; no box, so no cut corners.
ENDS = "0,{c} 20%,{c} 80%,{z} 100%"
CTA_SOFT = ("clip-path:none;background:linear-gradient(90deg,rgba(153,255,128,0) 0,#99FF80 20%,#99FF80 80%,rgba(153,255,128,0) 100%);color:#00041F")
QUIET_SOFT = ("clip-path:none;box-shadow:none;color:#E6E9FF;background:"
              "linear-gradient(90deg,rgba(230,233,255,0) 0,rgba(230,233,255,.8) 20%,rgba(230,233,255,.8) 80%,rgba(230,233,255,0) 100%) top/100% 1px no-repeat,"
              "linear-gradient(90deg,rgba(230,233,255,0) 0,rgba(230,233,255,.8) 20%,rgba(230,233,255,.8) 80%,rgba(230,233,255,0) 100%) bottom/100% 1px no-repeat,"
              "linear-gradient(90deg,rgba(0,4,31,0) 0,rgba(0,4,31,.86) 20%,rgba(0,4,31,.86) 80%,rgba(0,4,31,0) 100%)")


def sail(style="width:100%", look=CTA_SOFT):
    return f'<a class="btn cta" href="#" style="{style};{look}"><span class="kc">A</span>Set sail</a>'


def quiet(key, words, href=None, style="width:100%", look=QUIET_SOFT):
    if href:
        return f'<a class="btn" href="{href}" style="{style};{look}"><span class="kc">{key}</span>{words}</a>'
    return f'<button class="btn" type="button" style="{style};{look}"><span class="kc">{key}</span>{words}</button>'


def window(body, title, backdrop=""):
    return r89.window(body, title, ground=r89.GROUND + backdrop, lg=logic())


def deck():
    """Screen 2's backdrop: the picked voyage's own sea, dimmed, and a lit deck running away under the squad."""
    return ('<img src="{{voyage.map}}" alt="" style="position:absolute;left:0;top:84px;width:1920px;height:996px;object-fit:cover;'
            'object-position:{{voyage.pos}};opacity:.5;filter:blur(7px) saturate(.9)">'
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:'
            'linear-gradient(180deg,rgba(0,1,10,.92) 0,rgba(0,1,10,.35) 26%,rgba(0,1,10,.2) 52%,rgba(0,1,10,.9) 100%)"></div>'
            '<div class="abs" aria-hidden="true" style="left:-400px;top:560px;width:2720px;height:520px;transform-origin:50% 0;'
            'transform:perspective(900px) rotateX(64deg);background:'
            'repeating-linear-gradient(90deg,rgba(116,123,173,.34) 0,rgba(116,123,173,.34) 2px,rgba(0,0,0,0) 2px,rgba(0,0,0,0) 136px),'
            'repeating-linear-gradient(0deg,rgba(116,123,173,.34) 0,rgba(116,123,173,.34) 2px,rgba(0,0,0,0) 2px,rgba(0,0,0,0) 136px);'
            '-webkit-mask-image:linear-gradient(180deg,transparent 0,#000 30%,#000 70%,transparent 100%);'
            'mask-image:linear-gradient(180deg,transparent 0,#000 30%,#000 70%,transparent 100%)"></div>')


def summary():
    return (f'<div class="abs" style="left:40px;top:244px;width:1840px;height:56px;display:flex;align-items:center;gap:26px;z-index:3">'
            + quiet("B", "Voyages", href=f"R{REV}S1A.dc.html", style="width:220px;height:44px;font-size:14px")
            + '<span style="font-size:22px">{{voyage.n}}  {{voyage.place}}</span>'
            f'<span class="num" style="font-size:18px;color:{INK};white-space:pre">{{{{hero.stats}}}}</span>'
            f'<div style="margin-left:auto">{r87.squads()}</div></div>')


def foot():
    return (f'<div class="abs" style="left:40px;top:984px;width:1840px;height:56px;display:flex;align-items:center;z-index:3">'
            + quiet("Y", "Save as preset", style="width:360px")
            + f'<span style="margin-left:26px;font-size:15px;color:{INK}">A vessel brings the pilot and artifact you keep for it in the Hangar.</span>'
            + f'<div style="margin-left:auto;width:520px">{sail()}</div></div>')


def tile(listname, w, h, pic=None, note=False, count=4, size=15):
    """One strip of choices, every one on screen: a press picks it. <pic> is "hull" or "face" for a picture tile."""
    img = ""
    if pic == "hull":
        img = f'<img class="hull" src="{{{{t.img}}}}" alt="" style="width:88%;height:{h - 46}px">'
    elif pic == "face":
        img = f'<img src="{{{{t.img}}}}" alt="" style="width:100%;height:{h - 46}px;object-fit:contain;object-position:50% 100%">'
    sub = f'<span style="font-size:14px;color:{DIM};min-height:18px">{{{{t.note}}}}</span>' if note else ''
    return (f'<sc-for list="{{{{strips.{listname}}}}}" as="t" hint-placeholder-count="{count}"><button class="pick sls" type="button" '
            f'aria-label="{{{{t.name}}}}" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" style="width:{w}px;height:{h}px;display:flex;'
            'flex-direction:column;align-items:center;justify-content:' + ("flex-end" if pic else "center") + ';gap:2px;padding-bottom:'
            + ("8px" if pic else "0") + f';background:rgba(0,1,10,.82);box-shadow:{{{{t.edge}}}}">{img}'
            f'<span style="font-size:{size}px;color:{INK}">{{{{t.name}}}}</span>{sub}</button></sc-for>')


def strip(label, inner, style=""):
    return (f'<div style="display:flex;flex-direction:column;gap:8px;{style}"><span class="lab" style="color:{INK}">{label}</span>'
            f'<div style="display:flex;flex-wrap:wrap;gap:8px">{inner}</div></div>')


# ── Screen 1, as picked, with the soft buttons ────────────────────────────────────────────────────────────────────────

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
{r95.name_block()}
<div style="margin-top:30px">{presets}</div>
<div style="margin-top:auto;display:flex;flex-direction:column;gap:10px">
<sc-if value="{{{{party.has}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="font-size:15px;line-height:1.4;color:{INK};text-align:center">{{{{party.line}}}}</span></sc-if>
{quiet("X", "Configure squad", href=f"R{REV}S2D.dc.html")}</div>
<div style="margin-top:12px">{sail()}</div>
</section>"""
    return window(r95.chips() + r95.explorer(380, 244, 1020, 592, 204) + right,
                  f"Rev {REV} 1A - Screen 1 as picked (Rev 95 1A), with the soft buttons - interactive")


# ── The buttons, three readings ───────────────────────────────────────────────────────────────────────────────────────

def buttons_board():
    glow_cta = "box-shadow:0 0 28px rgba(153,255,128,.45),0 0 2px rgba(153,255,128,.9)"
    glow_quiet = "background:rgba(0,4,31,.86);color:#E6E9FF;box-shadow:inset 0 0 0 1px rgba(230,233,255,.55),0 0 22px rgba(230,233,255,.22)"
    wash_cta = "background:linear-gradient(180deg,#99FF80 0,rgba(153,255,128,.72) 100%);box-shadow:inset 0 2px 0 rgba(230,233,255,.55)"
    wash_quiet = ("color:#E6E9FF;background:linear-gradient(180deg,rgba(230,233,255,.16) 0,rgba(0,4,31,.86) 100%);"
                  "box-shadow:inset 0 1px 0 rgba(230,233,255,.7)")
    hard_quiet = "background:rgba(0,4,31,.78);color:#E6E9FF;box-shadow:inset 0 0 0 1px #747BAD"
    cols = [
        ("Now (Rev 95)", "A solid slab with cut corners; an outlined box.", "", hard_quiet),
        ("Soft ends (drawn on these boards)", "Solid in the middle, fading out to both ends. No box, so no cut corners.", CTA_SOFT, QUIET_SOFT),
        ("Soft glow", "The slab and its cut corners stay; a soft light round them.", glow_cta, glow_quiet),
        ("Soft face", "The slab stays; the fill eases from top to foot under one bright line.", wash_cta, wash_quiet),
    ]
    cells = "".join(
        f'<div style="display:flex;flex-direction:column;gap:22px"><span class="cap" style="font-size:18px">{name}</span>'
        f'<span style="font-size:15px;line-height:1.5;color:{INK};min-height:46px">{line}</span>'
        f'<a class="btn cta" href="#" style="width:100%;{cta}"><span class="kc">A</span>Set sail</a>'
        f'<a class="btn" href="#" style="width:100%;{q}"><span class="kc">X</span>Configure squad</a></div>'
        for name, line, cta, q in cols)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:40px">'
            f'<span style="font-size:36px">Rev {REV} - "soft hard soft": three readings beside the buttons as they were</span>'
            f'<div style="display:grid;grid-template-columns:repeat(4,1fr);gap:48px">{cells}</div>'
            f'<span style="font-size:18px;line-height:1.5;color:{INK}">The style guide today says two cut corners and nothing rounded. '
            'Soft ends drops the box; the other two keep it. Whichever is picked becomes the rule for every button.</span></div>')
    return page(f"Rev {REV} Buttons - three readings of soft hard soft on Set sail and Configure squad, beside the buttons as they were",
                r89.GROUND + body, "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=620)


# ── Screen 2: the squad ───────────────────────────────────────────────────────────────────────────────────────────────

def s2d_board():
    stage = f"""<div class="abs" style="left:40px;top:316px;width:1840px;height:330px;display:grid;grid-template-columns:repeat(3,1fr);gap:20px;z-index:2">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{{{s.focused}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.focus}}}}" style="position:relative;height:330px;opacity:{{{{s.dim}}}}">
<span aria-hidden="true" style="position:absolute;left:6%;right:6%;bottom:44px;height:120px;background:radial-gradient(ellipse 50% 50% at 50% 50%,{{{{s.pool}}}},rgba(230,233,255,0))"></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="position:absolute;left:104px;top:36px;width:480px;height:170px">
<img src="{{{{s.face}}}}" alt="" style="position:absolute;left:10px;top:20px;width:150px;height:200px;object-fit:contain;object-position:50% 100%">
<span style="position:absolute;left:0;right:0;bottom:0;display:flex;flex-direction:column;align-items:center;gap:4px">
<span class="lab" style="color:{INK}">{{{{s.role}}}}</span>
<span style="font-size:28px;padding-bottom:4px;border-bottom:2px solid {{{{s.under}}}}">{{{{s.name}}}}</span>
<span style="font-size:15px;color:{INK}">{{{{s.line1}}}}</span><span style="font-size:14px;color:{DIM}">{{{{s.line2}}}}</span></span>
</button></sc-for></div>"""
    tray = f"""<section aria-label="The picked seat's choices" class="abs" style="left:40px;top:668px;width:1840px;height:292px;z-index:2">
<div class="abs sl" style="inset:0;background:rgba(0,4,31,.84)"></div>
<div class="abs" style="left:24px;right:24px;top:20px;bottom:18px;display:grid;grid-template-columns:640px 404px 300px 1fr;gap:28px">
{strip("Vessel", tile("hulls", 100, 104, pic="hull", count=6, size=14))}
{strip("Pilot", tile("pilots", 95, 216, pic="face", note=True, count=4, size=14))}
{strip("Element", tile("els", 146, 60, count=4))}
{strip("Artifact", tile("arts", 176, 60, count=5))}
</div></section>"""
    return window(summary() + stage + tray + foot(),
                  f"Rev {REV} 2D - Deck: the three seats stand across a lit deck; the picked seat's four choices are one tray under them, one press each (recommended) - interactive",
                  backdrop=deck())


def hero(x, y, w, hull_w):
    """The picked seat, large: its hull on the lit deck, its pilot beside it, its name."""
    return f"""<section aria-label="The picked seat" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:560px;z-index:2">
<span aria-hidden="true" style="position:absolute;left:4%;right:4%;top:250px;height:160px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,.22),rgba(230,233,255,0))"></span>
<img class="hull" src="{{{{focus.hull}}}}" alt="" style="position:absolute;right:0;top:120px;width:{hull_w}px;height:{round(hull_w / 3)}px">
<img src="{{{{focus.face}}}}" alt="" style="position:absolute;left:0;top:60px;width:230px;height:300px;object-fit:contain;object-position:50% 100%">
<div style="position:absolute;left:0;right:0;top:392px;display:flex;flex-direction:column;align-items:center;gap:6px">
<span class="lab" style="color:{INK}">{{{{focus.role}}}}</span><span style="font-size:44px;line-height:1.05">{{{{focus.name}}}}</span>
<span style="font-size:18px;color:{INK}">{{{{focus.who}}}}</span><span style="font-size:18px;color:{INK}">{{{{focus.kit}}}}</span></div>
</section>"""


def rail(x, y, w, horizontal):
    """The three seats, small: a press picks the one being changed."""
    flow = "display:grid;grid-template-columns:repeat(3,1fr);gap:10px" if horizontal else "display:flex;flex-direction:column;gap:10px"
    hull = 56 if horizontal else 96
    return (f'<nav aria-label="The squad\'s seats" class="abs" style="left:{x}px;top:{y}px;width:{w}px;{flow};z-index:3">'
            '<sc-for list="{{seatList}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{s.focused}}" '
            f'aria-label="{{{{s.alt}}}}" onClick="{{{{s.focus}}}}" style="height:84px;display:grid;grid-template-columns:{hull}px 1fr;gap:10px;align-items:center;'
            'padding:0 12px;overflow:hidden;opacity:{{s.op}};background:{{s.railBg}};box-shadow:{{s.railEdge}}">'
            f'<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:{hull}px;height:50px">'
            f'<span style="display:flex;flex-direction:column;gap:3px;white-space:nowrap"><span style="font-size:18px">{{{{s.name}}}}</span>'
            f'<span style="font-size:14px;color:{DIM}">{{{{s.line1}}}}</span></span></button></sc-for></nav>')


def s2e_board():
    kit = ('<sc-for list="{{strips.kits}}" as="t" hint-placeholder-count="6"><button class="pick sl" type="button" aria-label="{{t.name}}" '
           'aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" style="position:relative;width:378px;height:178px;background:rgba(0,1,10,.82);box-shadow:{{t.edge}}">'
           '<img class="hull" src="{{t.img}}" alt="" style="position:absolute;right:10px;top:12px;width:270px;height:96px">'
           '<img src="{{t.face}}" alt="" style="position:absolute;left:8px;top:8px;width:92px;height:118px;object-fit:contain;object-position:50% 100%">'
           '<span style="position:absolute;left:16px;right:16px;bottom:12px;display:flex;align-items:baseline;justify-content:space-between;gap:10px">'
           f'<span style="font-size:22px">{{{{t.name}}}}</span><span style="font-size:14px;color:{DIM}">{{{{t.pilot}}}} - {{{{t.kit}}}}</span></span></button></sc-for>')
    right = f"""<section aria-label="Kits from the Hangar" class="abs" style="left:700px;top:316px;width:1180px;z-index:2;display:flex;flex-direction:column;gap:14px">
<span class="lab" style="color:{INK}">Your kits - one press sets the vessel, pilot, element and artifact</span>
<div style="display:flex;flex-wrap:wrap;gap:12px">{kit}</div>
<div style="display:grid;grid-template-columns:400px 1fr;gap:28px;margin-top:8px">
{strip("Or change the element", tile("els", 92, 56, count=4))}
{strip("the artifact", tile("arts", 140, 56, count=5, size=14))}
</div></section>"""
    return window(summary() + rail(40, 316, 620, True) + hero(40, 400, 620, 440) + right + foot(),
                  f"Rev {REV} 2E - Kits: the picked seat large; six whole kits from the Hangar, one press sets all four; element and artifact beside - interactive",
                  backdrop=deck())


def s2f_board():
    right = f"""<section aria-label="The picked seat's choices" class="abs" style="left:1090px;top:316px;width:790px;height:548px;z-index:2">
<div class="abs sl" style="inset:0;background:rgba(0,4,31,.84)"></div>
<div class="abs" style="left:24px;right:24px;top:20px;display:flex;flex-direction:column;gap:16px">
{strip("Vessel", tile("hulls", 117, 100, pic="hull", count=6, size=14))}
{strip("Pilot", tile("pilots", 179, 170, pic="face", note=True, count=4, size=14))}
<div style="display:grid;grid-template-columns:312px 1fr;gap:20px">
{strip("Element", tile("els", 152, 52, count=4))}
{strip("Artifact", tile("arts", 130, 52, count=5, size=14))}
</div></div></section>"""
    return window(summary() + rail(40, 316, 280, False) + hero(350, 330, 700, 520) + right + foot(),
                  f"Rev {REV} 2F - Showroom: a rail of seats, the picked seat as the hero on the deck, its four choices as strips beside it - interactive",
                  backdrop=deck())


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"S1A is good but I didnt like any of the second scren designs, the bg is too bland and also the button in both the "
                  "screens are too hard not soft hard soft. Clicking through so many vessels and pilots and artifacts also seems like a "
                  "drag, we need to make the experience smoother for the users\" (Shombith, 2026-11-12, on Rev 95)."),
        ("The backdrop", "The squad stands on a lit deck over the sea of the voyage that was picked: its own course map, dimmed, with "
                         "the deck's lines running away under the hulls."),
        ("The buttons", "\"Soft hard soft\" is read as solid in the middle and fading out to both ends. The Buttons board sets that "
                        "beside two other readings; say if another is meant."),
        ("One press", "No steppers. Every choice is on screen. Picking a vessel brings the pilot and artifact kept for it in the "
                      "Hangar, so most seats are done in one press."),
        ("2D Deck (recommended)", "The whole squad is seen at once, standing across the deck. A press on a seat picks it; one tray "
                                  "under them holds its four strips."),
        ("2E Kits", "The fewest presses: six whole kits, one press sets all four. Element and artifact can still be changed alone."),
        ("2F Showroom", "The picked seat is the hero; the others wait on a rail. The largest pictures to choose from."),
        ("Screen 1", "1A as picked, with the soft buttons. Its Configure squad opens 2D."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:22px">'
            f'<span style="font-size:36px">Rev {REV} - the squad screen: a backdrop, soft buttons, one press</span>{items}</div>')
    return page(f"Rev {REV} - notes: the backdrop, the buttons, one press, the three squad screens", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}S1A.dc.html": s1a_board, f"R{REV}Buttons.dc.html": buttons_board, f"R{REV}S2D.dc.html": s2d_board,
          f"R{REV}S2E.dc.html": s2e_board, f"R{REV}S2F.dc.html": s2f_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
