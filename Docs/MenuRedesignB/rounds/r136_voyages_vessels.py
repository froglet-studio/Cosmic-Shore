#!/usr/bin/env python3
"""Rev 136 boards: Vessels, a section of Your Ark in Rev 134's look (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-24, on Rev 135's classes boards: "I dont like any of them, there can be CTA try 2 more variations for the
classes its too noisy and confusing" and "Even the classes pick section".

And: "I already selected 134 for seats and pilots and classes/vessels should be another section following the same
design principle no?" So Rev 134's Seats and Pilots stay; Vessels is its own section of Your Ark in that look (the voyage
line, the hangar floor, hulls standing, Lime only on the one CTA), two ways, each also opened from a seat:
  E  One class on the floor: a class's vessels stand on the floor; the seven classes run along the foot.
  F  The wanted three on the floor: the giver's three classes stand like three seats, their vessels as chips.

    python3 Docs/MenuRedesignB/rounds/r136_voyages_vessels.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "136"
_spec = importlib.util.spec_from_file_location("r135", HERE / "r135_voyages_classes.py")
r135 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r135)
r133 = r135.r133

REV = int(_rev)
ROOT = r135.ROOT
INK, MUTE, SOFT, SEL = r135.INK, r135.MUTE, r135.SOFT, r135.SEL
HULL, FLEET, ORDER, EFF, AI_READY, SQUAD, ROLE, DOES, J = (r135.HULL, r135.FLEET, r135.ORDER, r135.EFF, r135.AI_READY, r135.SQUAD,
                                                          r135.ROLE, r135.SALVAGE_DOES, r135.J)
glyph, face, page, window, kc, gems = r135.glyph, r135.face, r135.page, r135.window, r135.kc, r135.gems
WANTS = J[7]                                   # Cultivator, Pathfinder, Protector
FITWORD = {3: "Best for this voyage", 2: "Does the work, no bonus", 1: "Little to do here"}


def status(v, ai_seat=False):
    if v in SQUAD:
        return "In your squad"
    if v in AI_READY:
        return "The AI can fly it"
    return "Needs a person" if ai_seat else ""


def art(v, h=130, w=260):
    if v in HULL:
        return f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:{w}px;height:{h}px;object-fit:contain">'
    return f'<span style="width:{w}px;height:{h}px;display:flex;align-items:center;justify-content:center">{glyph(r135.r_cls(v), 64, SOFT, 1.2)}</span>'


def foot(fit_from, fit_to, cta, back="Back"):
    segs = "".join(f'<span style="flex:1;height:10px;background:{INK if i < fit_to else "rgba(67,76,137,.6)"}"></span>' for i in range(3))
    change = (f'<span style="font-size:18px">Squad fit {fit_from} to {fit_to} of 3</span><span style="font-size:18px;color:{SOFT}">Pay 33 to 36</span>'
              if fit_to != fit_from else f'<span style="font-size:18px">Squad fit {fit_to} of 3</span>')
    return (f'<div class="abs row" style="left:40px;right:40px;top:980px;gap:24px">'
            f'<span class="row" style="gap:4px;width:150px">{segs}</span>{change}'
            f'<button class="btn sm soft" type="button" style="width:180px;margin-left:auto">{kc("B")}{back}</button>'
            f'<button class="btn soft-cta" type="button" style="width:360px">{kc("A")}{cta}</button></div>')


# Rev 134's Your Ark look, kept: the voyage line on top, the hangar floor, hulls standing on it, Lime only on the CTA.
_spec4 = importlib.util.spec_from_file_location("r134", HERE / "r134_voyages_m2_all.py")
r134 = importlib.util.module_from_spec(_spec4)
sys.argv = [sys.argv[0], _root, _rev]
_spec4.loader.exec_module(r134)
seats_top, FLOOR, cls_tag = r134.seats_top, r134.FLOOR, r134.cls_tag


def standing(v, x, w, on=False, dim=False, line=""):
    """A vessel standing on the hangar floor, the way Rev 134's seats stand: the hull, its name, one line."""
    hull = (f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:{w * .08:.0f}px;width:{w * .84:.0f}px;top:40px;height:200px;object-fit:contain">'
            if v in HULL else
            f'<span style="position:absolute;left:0;right:0;top:60px;height:170px;display:flex;align-items:center;justify-content:center">'
            f'{glyph(r135.r_cls(v), 110, SOFT, 1.1)}</span>')
    return (f'<button class="pick abs" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:270px;width:{w}px;height:560px;background:none;'
            f'opacity:{.4 if dim else 1}">'
            f'<span aria-hidden="true" class="abs" style="left:10%;right:10%;top:220px;height:50px;border-radius:50%;'
            f'background:radial-gradient(ellipse,rgba(150,172,252,.22),rgba(150,172,252,0) 70%)"></span>{hull}'
            f'<span class="abs" style="left:0;right:0;top:300px;font-size:22px;text-align:left">{v}</span>'
            f'<span class="abs" style="left:0;right:0;top:344px;height:{2 if on else 1}px;background:{SEL if on else "rgba(67,76,137,.6)"}"></span>'
            f'<span class="abs" style="left:0;right:0;top:362px;font-size:15px;color:{SOFT};text-align:left;line-height:1.4">{line}</span></button>')


def bar(cta, change=True):
    segs = "".join(f'<span style="flex:1;height:10px;background:{INK if i < 2 else "rgba(67,76,137,.6)"}"></span>' for i in range(3))
    return (f'<div class="abs row" style="left:40px;right:40px;top:990px;gap:24px">'
            f'<span class="row" style="gap:4px;width:180px">{segs}</span>'
            f'<span style="font-size:15px">Squad fit 1 to 2 of 3</span><span style="font-size:15px;color:{SOFT}">Pay 33 to 36</span>'
            f'<button class="btn sm soft" type="button" style="width:180px;margin-left:auto">{kc("B")}Back</button>'
            f'<button class="btn soft-cta" type="button" style="width:340px">{kc("A")}{cta}</button></div>')


def seat_line(seat):
    return (f'<span class="abs" style="left:40px;top:240px;font-size:15px;color:{SOFT}">{seat}</span>') if seat else ""


# ── E  One class on the floor ───────────────────────────────────────────────────────────────────────────────────────
def e_tabs(on, y=860):
    out = f'<div class="abs row" style="left:40px;right:40px;top:{y}px;gap:8px;justify-content:center">'
    for c in ORDER:
        lit = c == on
        col = INK if lit else (SOFT if c in WANTS else MUTE)
        out += (f'<button class="pick row" type="button" aria-pressed="{str(lit).lower()}" style="height:56px;gap:10px;padding:0 18px;background:none;'
                f'{"box-shadow:inset 0 -2px 0 " + SEL if lit else ""}">{glyph(c, 22, col, 1.6)}<span style="font-size:15px;color:{col}">{c}</span></button>')
    return out + '</div>'


def e_board(seat="", ai_seat=False):
    cls, sel = "Protector", "Scarab"
    vs = FLEET[cls]
    w, gap = 440, 140
    x0 = (1920 - (len(vs) * w + (len(vs) - 1) * gap)) // 2
    lines = {"Scarab": "Takes the hits meant for the Ark. The AI can fly it.", "Orca": "Needs a person in the seat." if ai_seat else "Takes the hits meant for the Ark.",
             "Thresher": "Needs a person in the seat." if ai_seat else "Takes the hits meant for the Ark."}
    head = (f'<div class="abs row" style="left:40px;right:40px;top:800px;justify-content:center;gap:14px">'
            f'<span style="font-size:15px;color:{INK}">{cls}s are wanted on this voyage</span><span style="font-size:15px;color:{SOFT}">'
            f'{DOES[cls]}.</span></div>')
    body = (seats_top() + seat_line(seat) + FLOOR
            + "".join(standing(v, x0 + i * (w + gap), w, v == sel, ai_seat and v not in AI_READY, lines[v]) for i, v in enumerate(vs))
            + head + e_tabs(cls) + bar("Put Scarab in Seat 3" if not seat else "Take the vessel"))
    return window(3, body)


def e_browse():
    return page(f"Rev {REV} EVessels - E One class on the floor: Your Ark's hangar look; a class's vessels stand on the floor, the classes along the foot, one CTA",
                e_board())


def e_seat():
    return page(f"Rev {REV} ESeat - E opened from Seat 3 (Change vessel): the same floor; what the AI cannot fly is dimmed",
                e_board("Seat 3  ·  flown by the AI  ·  choose a vessel", ai_seat=True))


# ── F  The wanted three on the floor ────────────────────────────────────────────────────────────────────────────────
FACE_OF = {"Cultivator": "Squirrel", "Pathfinder": "Meerkat", "Protector": "Scarab"}


def f_board(seat="", ai_seat=False):
    out = ""
    for i, c in enumerate(WANTS):
        x = 120 + i * 580
        v = FACE_OF[c]
        chips = "".join(
            f'<button class="btn sm soft chipq" type="button" aria-pressed="{str(m == "Scarab").lower()}" style="height:40px;padding:0 14px;'
            f'opacity:{.4 if ai_seat and m not in AI_READY else 1};{"box-shadow:inset 0 0 0 2px " + SEL if m == "Scarab" else ""}">{m}</button>'
            for m in FLEET[c])
        out += (f'<div class="abs" style="left:{x}px;top:250px;width:520px;height:700px">'
                + standing(v, 0, 520, v == "Scarab", False, "").replace("left:0px;top:270px", "left:0;top:0", 1)
                + f'<div class="abs row" style="left:0;right:0;top:360px;gap:12px">{cls_tag(c, 18, INK, 26)}</div>'
                  f'<span class="abs" style="left:0;right:0;top:400px;font-size:15px;color:{SOFT}">{DOES[c]}.</span>'
                  f'<div class="abs row" style="left:0;right:0;top:450px;gap:8px;flex-wrap:wrap">{chips}</div></div>')
    more = (f'<div class="abs row" style="left:120px;top:900px;gap:20px"><span style="font-size:15px;color:{MUTE}">'
            f'Destroyers, Hunters and Shepherds do the work without the bonus.</span>'
            f'<button class="btn sm soft" type="button" style="width:240px">{kc("Y")}See all seven</button></div>')
    return window(3, seats_top() + seat_line(seat) + FLOOR + out + more + bar("Put Scarab in Seat 3" if not seat else "Take the vessel"))


def f_browse():
    return page(f"Rev {REV} FVessels - F The wanted three on the floor: Your Ark's hangar look; the giver's three classes stand like three seats, "
                "their vessels as chips; one CTA", f_board())


def f_seat():
    return page(f"Rev {REV} FSeat - F opened from Seat 3 (Change vessel): the same three; what the AI cannot fly is dimmed",
                f_board("Seat 3  ·  flown by the AI  ·  choose a vessel", ai_seat=True))


def notes():
    rows = [
        ("Asked", "Shombith, 24 Nov, on Rev 135: \"too noisy and confusing\", \"there can be CTA\", and: \"I already selected 134 for seats and "
                  "pilots and classes/vessels should be another section following the same design principle\"."),
        ("Kept", "Rev 134's Seats and Pilots stay as picked. Vessels is its own section of Your Ark in the same look: the voyage line on top, "
                 "the hangar floor, hulls standing on it, one line under each, the fit and pay along the foot, Lime only on the CTA."),
        ("E  One class on the floor", "One class at a time: its vessels stand on the floor; the seven classes run along the foot, the giver's "
                                      "three brighter. Calm; you learn the fleet a class at a time."),
        ("F  The wanted three on the floor", "The giver's three classes stand like the three seats, each showing one vessel with its class's "
                                             "others as chips; the rest are one line and See all seven. The quickest answer to \"what do I choose\"."),
        ("From a seat", "Change vessel on a seat opens the same section with the seat named; in an AI seat what the AI cannot fly is dimmed."),
        ("Art", "Hull renders exist for six vessels; Scarab, Meerkat and the rest stand as their class glyph until /vessel-model renders them."),
    ]
    html = r133.notes_page("notes: Vessels, a section of Your Ark in Rev 134's look", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


BOARDS = {"EVessels": e_browse, "ESeat": e_seat, "FVessels": f_browse, "FSeat": f_seat}

if __name__ == "__main__":
    for key, fn in BOARDS.items():
        (ROOT / f"R{REV}{key}.dc.html").write_text(fn(), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(BOARDS) + 1, "boards to", ROOT)
