#!/usr/bin/env python3
"""Rev 94 board C, third pass: the boot splash poster with the game's own art (Frogma tab "Loading Screens").

Shombith, 2026-11-12, on the second pass (A the crew before the domains, B three crews, C the same with the living
sea far behind): "Delete the old revs for this opening scene but I like C but the captains are too low res, garrett
added new captain images, we can curate and use some of them and also use the vessels actual icons from the hangar
the silver look and for crystals I want actual omni crystals not this small icon and also some color ful prisms or a
cell works as well".

So C is the one board of this round, and everything drawn as a stand-in is replaced by the game's own picture:

  captains   five of the new 1024 px busts (Docs/Pilots/art/busts/ on bleeding-edge): Hornet, Needle, Galleon,
             Sundial, Anvil, each in a slanted card leaning the way the bands lean
  vessels    the class cards in their silver livery (Assets/_Graphics/CardImages/<Hull>_large.png), one over each band
  crystals   the Omni crystal's own render (Assets/_Graphics/Codex/ethirion_omni.png)
  the cell   a real frame of a cell from outside, with its coloured prism clouds (Docs/Pilots/art/refs/
             sea_cell_outside.jpg), far behind everything, with Rev 91's sea lines over it
  fauna      the Codex's pictures: a shoal, a quadfish, a shark, a piranha
  flora      the Codex's pictures: coral, an arbor, a lantern, a pine, rooted at the foot

Kept from the second pass: the three domain bands as prisms of the worn colour set (face colour as the body, rim
colour as a thin edge), the set changing on a seamless 28 s loop, and motion that only eases and alternates. The
earlier boards of this round are in git (aa2439de9d the four posters, 7c832611c0 A, B and the first C).

    python3 Docs/MenuRedesignB/rounds/r94_posters.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "94"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r92", HERE / "r92_splash.py")
r92 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r92)          # sets the bar's steps to the boot status lines
r91 = r92.r91

REV = int(_rev)
INK, DIM, LINE, GROUND, DEEP = r91.INK, r91.DIM, r91.LINE, r91.GROUND, r91.DEEP
b = r91.blob
LOGO, FROG = b("9ec3291104bcd9430ee361c4a7f4b879"), b("57c57263842bd92d07e7828027588b6b")
BUST = {"Anvil": b("ebcd5a6e49d48e985f33841f80a3b4ec"), "Needle": b("e196681705352c3112d95e9e0b2c6d49"),
        "Galleon": b("18869a77f2bb6a2af26cc156a2ed65d2"), "Sundial": b("c9d281f61648b93618d9c0cfb1a96fbe"),
        "Hornet": b("2e4c4614bec674e1ad041ba464279425")}
HULL = r91.HULL                                    # the silver class cards, uploaded for Rev 91
OMNI, CELL = b("3fe12db57866390e5e02f1d0f71526b9"), b("61aeb18d02d8d22a15c40350d3adb904")
FAUNA = {"quadfish": b("c7c7450c7364aa897eba79ef02adca24"), "shoal": b("ae55babdb766709c43c1f980ed0a7e55"),
         "shark": b("6d6b8ac18bfced58cf57023410ed1390"), "piranha": b("7abe39580b5c17049855a28437bfa073")}
FLORA = {"coral": b("8c5a5629ed774664c9be295f5d43be84"), "arbor": b("af101c1c60986a00d4b682df2a7e37fc"),
         "lantern": b("4955f6f5179b9b3d9cf57ae9cf517e40"), "pine": b("aa679472db539af8a6d94f1f549b9fc9")}

# (rim, face) per domain, straight from Docs/VesselModels/colour_sets.json `sets.<id>.domains.<family>.<pigment>.swatch`.
SETS = [("Nocturne", {"jade": ("#4BD5D4", "#480135"), "ruby": ("#DF5CE4", "#00384E"), "gold": ("#ECBD44", "#3F205F")}),
        ("Dart Frog", {"jade": ("#6ECDAD", "#52290C"), "ruby": ("#CE62ED", "#0A424B"), "gold": ("#FDD052", "#2A1B3D")}),
        ("Dusk", {"jade": ("#66DED7", "#3C1D08"), "ruby": ("#CB66E9", "#1E2A1E"), "gold": ("#F0D58C", "#4F3135")}),
        ("Ukiyo-e", {"jade": ("#61D5C3", "#2C6A61"), "ruby": ("#B049E5", "#5A2279"), "gold": ("#FCD269", "#94753A")})]
DOMAINS = ["jade", "ruby", "gold"]
LOOP = 28
SLANT = 0.2493                                     # tan(14 degrees): how far a slanted edge leans per unit of height


def theme_css():
    """One loop through the sets. Each holds, then blends into the next over a fifth of its turn; the last blends
    back into the first, so the loop has no seam."""
    n, out = len(SETS), ""
    for dom in DOMAINS:
        for pig, name in ((1, f"face-{dom}"), (0, f"rim-{dom}")):
            keys = ""
            for i, (_n, cols) in enumerate(SETS):
                s, e = 100 * i / n, 100 * (i + 1) / n - 100 / n * .22
                keys += f"{s:.2f}%,{e:.2f}%{{background-color:{cols[dom][pig]}}}"
            keys += f"100%{{background-color:{SETS[0][1][dom][pig]}}}"
            out += f"@keyframes {name}{{{keys}}}"
    for i in range(n):
        s, e = 100 * i / n, 100 * (i + 1) / n - 100 / n * .22
        nxt = 100 * (i + 1) / n
        if i == 0:
            out += f"@keyframes set0{{0%,{e:.2f}%{{opacity:1}}{nxt:.2f}%,{100 - 100 / n * .22:.2f}%{{opacity:0}}100%{{opacity:1}}}}"
        else:
            out += f"@keyframes set{i}{{0%,{s - 100 / n * .22:.2f}%{{opacity:0}}{s:.2f}%,{e:.2f}%{{opacity:1}}{nxt:.2f}%,100%{{opacity:0}}}}"
    return out


CSS = f"""@keyframes bob{{0%{{transform:translateY(-8px)}}100%{{transform:translateY(10px)}}}}
@keyframes push{{0%{{transform:scale(1.02) translate(-10px,6px)}}100%{{transform:scale(1.1) translate(14px,-10px)}}}}
@keyframes turn{{0%{{transform:rotate(-7deg) translateY(-6px)}}100%{{transform:rotate(7deg) translateY(8px)}}}}
@keyframes glideR{{0%{{transform:translate(-50px,16px)}}100%{{transform:translate(90px,-24px)}}}}
@keyframes glideL{{0%{{transform:translate(60px,10px)}}100%{{transform:translate(-80px,-20px)}}}}
@keyframes sway{{0%{{transform:rotate(-2.5deg)}}100%{{transform:rotate(2.5deg)}}}}
.th{{animation-duration:{LOOP}s;animation-timing-function:ease-in-out;animation-iteration-count:infinite}}
""" + theme_css()


def img(src, alt, x, y, w, h, extra=""):
    return f'<img src="{src}" alt="{alt}" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;object-fit:contain;{extra}">'


def marks(x, y, w, fx, fy, fw=240):
    return img(LOGO, "Cosmic Shore", x, y, w, round(w * 359 / 910)) + img(FROG, "Froglet Games", fx, fy, fw, round(fw * 322 / 900))


def band(i, x, w=400, top=-60, h=1000, alpha=.74):
    """One domain as its prism: the set's face colour as the body, its rim colour as the two long edges."""
    dom = DOMAINS[i]
    face, rim = SETS[0][1][dom][1], SETS[0][1][dom][0]
    edge = f"position:absolute;top:0;width:3px;height:100%;background-color:{rim};animation-name:rim-{dom}"
    return (f'<div aria-hidden="true" style="position:absolute;left:{x}px;top:{top}px;width:{w}px;height:{h}px;transform:skewX(-14deg)">'
            f'<div class="th" style="position:absolute;inset:0;opacity:{alpha};background-color:{face};animation-name:face-{dom}"></div>'
            f'<div class="th" style="{edge};left:0"></div><div class="th" style="{edge};right:0"></div></div>')


def prisms(i, x, y, n=4, w=26, h=70, step=40):
    """A short run of prisms in a domain's rim colour, as a vessel lays them: the colourful part of the far sea."""
    dom = DOMAINS[i]
    out = f'<div aria-hidden="true" style="position:absolute;left:{x}px;top:{y}px;animation:bob {8 + i}s ease-in-out infinite alternate">'
    for k in range(n):
        out += (f'<div class="th" style="position:absolute;left:{k * step}px;top:{-k * step * .45:.0f}px;width:{w}px;height:{h + k * 8}px;transform:rotate(-24deg);'
                f'opacity:.8;background-color:{SETS[0][1][dom][0]};animation-name:rim-{dom}"></div>')
    return out + "</div>"


def far_sea():
    """A real cell far behind, Rev 91's sea lines over it, then the game's own crystals, fauna and flora."""
    lines = r91.sea().split("</div>", 1)[1]            # the sea without its own flat ground
    cell = (f'<div class="abs" aria-hidden="true" style="inset:0;background:{DEEP}"></div>'
            f'<img src="{CELL}" alt="" style="position:absolute;left:0;top:-110px;width:1920px;height:1080px;object-fit:cover;opacity:.62;animation:push 40s ease-in-out infinite alternate">')
    life = ""
    for x, y, s, dur in [(170, 520, 150, 9), (1730, 250, 170, 11), (1560, 640, 96, 8), (330, 330, 84, 10), (1840, 560, 70, 12)]:
        life += img(OMNI, "", x - s // 2, y - s // 2, s, s, f"animation:turn {dur}s ease-in-out infinite alternate")
    life += img(FAUNA["shoal"], "", 40, 380, 260, 87, "opacity:.9;animation:glideR 30s ease-in-out infinite alternate")
    life += img(FAUNA["quadfish"], "", 1600, 420, 150, 152, "opacity:.9;animation:glideL 26s ease-in-out infinite alternate")
    life += img(FAUNA["shark"], "", 1570, 80, 170, 100, "opacity:.8;animation:glideL 36s ease-in-out infinite alternate")
    life += img(FAUNA["piranha"], "", 60, 650, 150, 116, "opacity:.85;animation:glideR 34s ease-in-out infinite alternate")
    root = "transform-origin:50% 100%;animation:sway {d}s ease-in-out infinite alternate"
    life += img(FLORA["arbor"], "", 250, 590, 142, 270, "opacity:.85;" + root.format(d=9))
    life += img(FLORA["coral"], "", 20, 690, 180, 176, "opacity:.8;" + root.format(d=11))
    life += img(FLORA["lantern"], "", 1790, 560, 99, 300, "opacity:.85;" + root.format(d=10))
    life += img(FLORA["pine"], "", 1560, 700, 160, 169, "opacity:.8;" + root.format(d=12))
    return cell + lines + life + prisms(0, 70, 340) + prisms(2, 1650, 820, n=3)


def card(name, cx, h, w):
    """A captain's bust in a slanted card that leans the way the bands lean; an ink line round it."""
    off = round(h * SLANT)
    box = w + off

    def shape(inset):
        return (f"clip-path:polygon({off + inset}px {inset}px,{box - inset}px {inset}px,{w - inset}px {h - inset}px,{inset}px {h - inset}px)")
    return (f'<figure style="position:absolute;left:{cx - box // 2}px;top:{862 - h}px;width:{box}px;height:{h}px;margin:0">'
            f'<div aria-hidden="true" style="position:absolute;inset:0;background:{INK};{shape(0)}"></div>'
            f'<img src="{BUST[name]}" alt="{name}" style="position:absolute;left:0;top:0;width:{box}px;height:{h}px;object-fit:cover;object-position:50% 18%;{shape(3)}">'
            f'</figure>')


def themes(x, y):
    """Which colour set is worn: the four names, the worn one lit. A readout, not a control."""
    out = (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;flex-direction:column;gap:12px;padding:16px 20px;background:rgba(0,1,10,.72)">'
           '<span class="lab">Colour set</span><div style="display:flex;flex-direction:column;gap:10px">')
    for i, (n, _c) in enumerate(SETS):
        out += (f'<span style="position:relative;font-size:18px;color:{DIM};white-space:nowrap">{n}'
                f'<span class="th" aria-hidden="true" style="position:absolute;left:0;top:0;color:{INK};opacity:{1 if i == 0 else 0};animation-name:set{i}">{n}</span></span>')
    return out + "</div></div>"


def foot(y=860):
    return f'<div class="abs" aria-hidden="true" style="left:0;top:{y}px;width:1920px;height:{1080 - y}px;background:{DEEP}"></div>' + r91.bar()


def board_c():
    line = [("Hornet", 400, 230), ("Needle", 470, 260), ("Galleon", 540, 300), ("Sundial", 470, 260), ("Anvil", 400, 230)]
    xs = [452, 700, 960, 1220, 1468]
    crew = "".join(card(line[i][0], xs[i], line[i][1], line[i][2]) for i in (0, 4, 1, 3, 2))   # the middle card in front
    hulls = "".join(img(HULL[n], f"The {n}", x, y, s, s, f"animation:bob {d}s ease-in-out infinite alternate")
                    for n, x, y, s, d in [("Squirrel", 500, 20, 300, 9), ("Dolphin", 830, -10, 300, 7), ("Sparrow", 1190, 20, 300, 8)])
    body = (far_sea() + "".join(band(i, 360 + i * 400) for i in range(3)) + hulls + crew
            + img(LOGO, "Cosmic Shore", 96, 64, 300, round(300 * 359 / 910)) + themes(1690, 300) + foot()
            # Froglet's logo under the bar's percentage, bottom right (Shombith, 2026-11-13).
            + img(FROG, "Froglet Games", 1920 - 96 - 150, 1080 - 8 - round(150 * 322 / 900), 150, round(150 * 322 / 900)))
    return r91.page(f"R{REV}C.dc.html", body, CSS)


def board_notes():
    cols = [
        ("Asked", ["Shombith, 2026-11-12: \"I like C but the captains are too low res, garrett added new captain images, we can curate and use some of them and also use the vessels actual icons from the hangar the silver look and for crystals I want actual omni crystals not this small icon and also some color ful prisms or a cell works as well\"."]),
        ("Now the game's own art", ["Captains: five of the new 1024 px busts (Hornet, Needle, Galleon, Sundial, Anvil), each in a slanted card.",
                                    "Vessels: the class cards in silver livery (Squirrel, Dolphin, Sparrow), one over each band.",
                                    "Crystals: the Omni crystal's own render.",
                                    "Fauna and flora: the Codex's pictures (shoal, quadfish, shark, piranha; coral, arbor, lantern, pine)."]),
        ("The far background", ["A real frame of a cell from outside, with its coloured prism clouds, slowly pushing in.",
                                "The sea lines over it. Two short runs of prisms in the worn set's rim colours.",
                                "The bands are see-through enough for it to show."]),
        ("Kept", ["Three domain bands as the worn colour set paints a prism: face colour body, rim colour edge.",
                  "The set changes on a seamless 28 second loop: Nocturne, Dart Frog, Dusk, Ukiyo-e.",
                  "Every motion eases both ways and alternates.",
                  "Off the canvas on his word: Rev 92, Rev 93, and this round's A and B. All in git."]),
    ]
    body = (f'<div class="abs" aria-hidden="true" style="inset:0;background:{GROUND}"></div>'
            f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:36px">The boot splash: the crew before the domains, in the game\'s own art</span></header>'
            '<div class="abs" style="left:96px;top:200px;width:1728px;display:grid;grid-template-columns:repeat(4,1fr);gap:48px">')
    for head, items in cols:
        body += (f'<section style="display:flex;flex-direction:column;gap:16px;padding-top:16px;box-shadow:inset 0 1px 0 {LINE}"><span style="font-size:22px">{head}</span>'
                 + "".join(f'<span style="font-size:18px;line-height:1.5;color:{INK}">{t}</span>' for t in items) + "</section>")
    body += "</div>"
    return r91.page(f"R{REV}Notes.dc.html", body, h=900, loop=False)


if __name__ == "__main__":
    for fn in (board_c, board_notes):
        print("wrote", fn())
