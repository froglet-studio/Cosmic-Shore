#!/usr/bin/env python3
"""Rev 91 boards: loading screens (Frogma tab "Loading Screens", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-12: "create me mockups for the animated smooth background with perfect progress bar ... maybe all
parts of the game is shown in the loading menu. Like squads or the ark or the pilots or vessels the toybox. Create
like 6 mockups".

Every board shares three things and differs in the fourth:

  the sea     three slow fields of the Blue family drifting under three layers of sea lines (CSS only)
  the bar     one bar on one 16 s loop: four segments sized to the four real steps, a tabular percentage, the
              step's name, a glint that keeps travelling when a step is slow, Lime and "Ready" at 100
  the brief   ENTERING and the mode's name, top left
  the show    A the squad, B the Ark, C pilots, D vessels, E the Toy Box, F all five in turn (recommended)

Shombith picked F for the loading screen the same day ("for the Loading screen I liked F"). This round is the
loading screen into a game mode; the boot splash and sign-in scene are Rev 92 (r92_splash.py), which imports this
file for the sea, the bar and the page.

All motion is keyframe percentages on the one loop (the Rev 33 rule), so a clip can be cut by seeking.

    python3 Docs/MenuRedesignB/rounds/r91_loading.py <scratch root> [rev]

reads <root>/project/canvas.json (written by `frogma.py plan`) for each board's title and writes the boards.
"""
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2]) if len(sys.argv) > 2 else 91
INDEX = json.loads((ROOT / "canvas.json").read_text())

INK, DIM, LINE, GROUND, DEEP, LIME = "#E6E9FF", "#747BAD", "#434C89", "#00041F", "#00010A", "#99FF80"


def blob(i):
    return "/_blob/" + i


ARK, ARK_TOP = blob("f2a0a3e9c284a960c539d417b46cd1eb"), blob("2836e11802af47009ec16cb104f72d86")
HULL = {"Dolphin": blob("b51af676a7b829c770accf465e572afe"), "Sparrow": blob("e59efc26ffc9c102ffbdbed169b912f9"),
        "Squirrel": blob("6991a2c4a1282e1ad5c356039433fb64"), "Manta": blob("74f959fbbfac9c075ba6d0cc8f076876"),
        "Rhino": blob("f5fc0c2306853690b83b0817c85cdfbb"), "Serpent": blob("4c38ac68cae739a363c7d48a1600b687")}
# Callsigns are the roster's (Assets/_SO_Assets/Pilots/Roster/Pilot_<Callsign>.asset); the art is each one's portrait.
PILOT = {"Galleon": blob("366e17b38101786ffdf483620c9d09ee"), "Needle": blob("098401b441c75c387878033b0fd0e5d2"),
         "Anvil": blob("0b0af3cdf4e10230814b2076301c093c"), "Sundial": blob("3b2ed44d1f72256d37b16e93f9dacfc1"),
         "Flume": blob("cd03e1480f12044165e0d50f85ec7c41"), "Doe": blob("1b6fe2b093d8ab06e98fcb8c3160a6b9"),
         "Hornet": blob("282b07cbf5ef2507ad94c75d4d97d222"), "Manger": blob("f0a280d827b436aea177aa208f17c287")}
TOYS = ["Vessel Changer", "Cell Selector", "Domain Changer", "Element Charger", "Spawn Matrix", "Connect the Dots", "Wander"]
ROOMS = ["Converter", "Squads", "Artifacts", "Decor"]

# The loop, in percent of 16 s. A step starts at its mark; the bar reaches the step's share of the work at the next.
STEPS = [("Connecting to shore", 0, 22), ("Building the arena", 18, 64), ("Settling the arena", 52, 86),
         ("Waiting for pilots", 70, 100)]
DONE = 84
P_KEYS = "0%,4%{--p:0;--pi:0}18%{--p:22;--pi:22}30%{--p:38;--pi:38}40%{--p:44;--pi:44}52%{--p:64;--pi:64}70%{--p:86;--pi:86}84%,100%{--p:100;--pi:100}"


def fade(name, s, e, lo=0, prop="opacity", hi=1):
    """On from s to e percent of the loop, off outside it."""
    k = []
    if s > 0:
        k.append(f"0%,{s - 1}%{{{prop}:{lo}}}")
    k.append(f"{s + 1 if s > 0 else 0}%,{e - 1 if e < 100 else 100}%{{{prop}:{hi}}}")
    if e < 100:
        k.append(f"{e + 1}%,100%{{{prop}:{lo}}}")
    return f"@keyframes {name}{{{''.join(k)}}}"


def since(name, s, lo, hi, prop="color"):
    """Off until s percent of the loop, on from then to its end."""
    if s <= 0:
        return f"@keyframes {name}{{0%,100%{{{prop}:{hi}}}}}"
    return f"@keyframes {name}{{0%,{s - 1}%{{{prop}:{lo}}}{s + 1}%,100%{{{prop}:{hi}}}}}"


def waves(period, amp, ys):
    """Sea lines 3840 wide; sliding one by 1920 is seamless because 1920 is a whole number of periods."""
    half = period // 2
    n = 3840 // half
    paths = "".join(f'<path d="M0 {y} q {half // 2} {-amp} {half} 0' + f" t {half} 0" * (n - 1) + '"></path>' for y in ys)
    return paths


CSS = f"""body{{margin:0;background:{DEEP}}}
@property --p{{syntax:'<number>';inherits:true;initial-value:0}}
@property --pi{{syntax:'<integer>';inherits:true;initial-value:0}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.18em;text-transform:uppercase;color:{DIM}}}
.num{{font-variant-numeric:tabular-nums}}
.abs{{position:absolute}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.hexc{{clip-path:polygon(25% 0,75% 0,100% 50%,75% 100%,25% 100%,0 50%)}}
.ld{{animation:ldP 16s linear infinite}}
.t{{animation-duration:16s;animation-timing-function:linear;animation-iteration-count:infinite}}
@keyframes ldP{{{P_KEYS}}}
@keyframes seaA{{0%{{transform:translate(-260px,-120px) scale(1)}}100%{{transform:translate(340px,140px) scale(1.25)}}}}
@keyframes seaB{{0%{{transform:translate(220px,90px) scale(1.2)}}100%{{transform:translate(-320px,-160px) scale(1)}}}}
@keyframes seaC{{0%{{transform:translate(-120px,160px) scale(1.1)}}100%{{transform:translate(260px,-80px) scale(1.3)}}}}
@keyframes seaSlide{{0%{{transform:translateX(0)}}100%{{transform:translateX(-1920px)}}}}
@keyframes glint{{0%{{transform:translateX(-200px)}}100%{{transform:translateX(1728px)}}}}
@keyframes bob{{0%{{transform:translateY(-10px)}}100%{{transform:translateY(12px)}}}}
@keyframes arkDrift{{0%{{transform:translate(-40px,14px) rotate(1deg)}}100%{{transform:translate(110px,-22px) rotate(-1.5deg)}}}}
@keyframes turn{{0%{{transform:rotate(0deg)}}100%{{transform:rotate(360deg)}}}}
{fade("fillDone", DONE, 100, lo=INK, hi=LIME, prop="background")}
{fade("wordDone", DONE, 100)}
{"".join(fade(f"word{i}", s, STEPS[i + 1][1] if i < 3 else DONE) for i, (_, s, _e) in enumerate(STEPS))}
{"".join(since(f"lit{i}", s, DIM, INK) for i, (_, s, _e) in enumerate(STEPS))}
{"".join(since(f"mark{i}", STEPS[i + 1][1] if i < 3 else DONE, "rgba(230,233,255,0)", INK, prop="background") for i in range(4))}
.pct::after{{counter-reset:pct var(--pi);content:counter(pct)}}"""


def sea():
    """The shared ground: three slow fields of the Blue family and three layers of sea lines."""
    field = "position:absolute;width:1500px;height:1100px;animation-timing-function:ease-in-out;animation-iteration-count:infinite;animation-direction:alternate"
    lines = "position:absolute;left:0;top:0;width:3840px;height:1080px;animation:seaSlide linear infinite"
    return f"""<div class="abs" aria-hidden="true" style="inset:0;background:linear-gradient(180deg,{GROUND} 0,{DEEP} 100%)"></div>
<div aria-hidden="true" style="{field};left:-300px;top:-260px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(48,43,213,.50),rgba(48,43,213,0));animation-name:seaA;animation-duration:26s"></div>
<div aria-hidden="true" style="{field};left:760px;top:120px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(64,78,236,.34),rgba(64,78,236,0));animation-name:seaB;animation-duration:34s"></div>
<div aria-hidden="true" style="{field};left:260px;top:380px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(67,76,137,.46),rgba(67,76,137,0));animation-name:seaC;animation-duration:42s"></div>
<svg aria-hidden="true" viewBox="0 0 3840 1080" fill="none" stroke="rgba(230,233,255,.07)" stroke-width="2" style="{lines};animation-duration:90s">{waves(1920, 70, [240, 620])}</svg>
<svg aria-hidden="true" viewBox="0 0 3840 1080" fill="none" stroke="rgba(230,233,255,.10)" stroke-width="2" style="{lines};animation-duration:56s">{waves(960, 44, [400, 800])}</svg>
<svg aria-hidden="true" viewBox="0 0 3840 1080" fill="none" stroke="rgba(230,233,255,.14)" stroke-width="1" style="{lines};animation-duration:34s">{waves(640, 22, [520, 980])}</svg>
<div class="abs" aria-hidden="true" style="inset:0;background:radial-gradient(ellipse 70% 70% at 50% 46%,rgba(0,1,10,0) 40%,rgba(0,1,10,.72) 100%)"></div>"""


def bar(x=96, y=884, w=1728, live=True, p=None, word=None, done=False, slow=False, head=True):
    """The one progress bar. live: on the loop. Otherwise held at p percent with its word."""
    t = ' class="t"' if live else ""
    dy = 0 if head else 28
    kicker = '<span class="lab abs" style="left:0;top:0">Loading</span>' if head else ""

    def anim(name):
        return f"animation-name:{name};" if live else ""

    words = ""
    if live:
        for i, (n, _s, _e) in enumerate(STEPS):
            words += f'<span class="t abs" style="left:0;top:0;white-space:nowrap;opacity:{1 if i == 0 else 0};animation-name:word{i}">{n}</span>'
        words += f'<span class="t abs" style="left:0;top:0;opacity:0;color:{LIME};animation-name:wordDone">Ready</span>'
        pct = '<span class="num pct"></span>'
        fill_w, fill_bg = "calc(var(--p) * 1%)", INK
    else:
        words = f'<span class="abs" style="left:0;top:0;white-space:nowrap;color:{LIME if done else INK}">{word}</span>'
        pct = f'<span class="num">{p}</span>'
        fill_w, fill_bg = f"{p}%", LIME if done else INK
    glint = ("" if done else
             f'<span style="position:absolute;left:0;top:0;width:200px;height:100%;background:linear-gradient(90deg,rgba(0,4,31,0),rgba(0,4,31,.55),rgba(0,4,31,0));'
             f'animation:glint {"3.6s" if slow else "2.4s"} linear infinite"></span>')
    segs, labels = "", ""
    for i, (n, s, e) in enumerate(STEPS):
        start = STEPS[i - 1][2] if i else 0
        if i:
            segs += f'<span style="position:absolute;left:calc({start}% - 2px);top:0;width:4px;height:100%;background:{GROUND}"></span>'
        on = live or (p is not None and p >= start and (p > 0 or i == 0))
        full = (not live) and p >= e
        col = (DIM if live and i else INK) if live else (INK if on else DIM)
        labels += (f'<span{t} style="position:absolute;left:{start}%;top:0;display:flex;align-items:center;gap:10px;font-size:15px;color:{col};{anim(f"lit{i}")}">'
                   f'<span{t} style="width:8px;height:8px;box-sizing:border-box;transform:rotate(45deg);border:1px solid {INK if on else DIM};'
                   f'background:{INK if full else "rgba(230,233,255,0)"};{anim(f"mark{i}")}"></span>{n}</span>')
    return f"""<section aria-label="Loading progress" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{132 - dy}px">
{kicker}
<div class="abs" style="left:0;top:{26 - dy}px;width:900px;height:34px;font-size:28px;line-height:34px">{words}</div>
<div class="abs" style="right:0;top:{6 - dy}px;display:flex;align-items:baseline;gap:6px;font-size:44px;line-height:54px">{pct}<span style="font-size:20px;color:{DIM}">%</span></div>
<div class="abs" role="progressbar" aria-label="Loading" style="left:0;top:{76 - dy}px;width:100%;height:8px;background:rgba(67,76,137,.55);overflow:hidden;clip-path:polygon(4px 0,100% 0,calc(100% - 4px) 100%,0 100%)">
<div{t} style="position:absolute;left:0;top:0;height:100%;width:{fill_w};background:{fill_bg};overflow:hidden;{anim("fillDone")}">{glint}</div>{segs}</div>
<div class="abs" style="left:0;top:{104 - dy}px;width:100%;height:20px">{labels}</div>
</section>"""


def brief(mode="Skim Race"):
    return (f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px">'
            f'<span class="lab">Entering</span><span style="font-size:28px;line-height:1.2">{mode}</span></header>')


def glow(x, y, w, h, a=.12):
    return (f'<div class="abs" aria-hidden="true" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;'
            f'background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,{a}),rgba(230,233,255,0))"></div>')


def page(name, body, css="", h=1080, loop=True):
    title = INDEX["boards"][name]["title"]
    html = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{title}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
{CSS}
{css}
</style>
</helmet>
<div class="{'ld' if loop else 'still'}" style="width: 1920px; height: {h}px; box-sizing: border-box; position: relative; overflow: hidden; background: {GROUND}; color: {INK}; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1920,"height":{h}}}}}'>
class Component extends DCLogic {{
renderVals() {{
return {{}};
}}
}}
</script>
</body>
</html>
"""
    (ROOT / name).write_text(html)
    return name


def cycle(names, prefix, end=DONE):
    """Equal slices of the loop from 0 to `end`; the last one holds to the loop's end."""
    n, css, cuts = len(names), "", []
    for i in range(n):
        s, e = round(i * end / n), round((i + 1) * end / n) if i < n - 1 else 100
        css += fade(f"{prefix}{i}", s, e)
        cuts.append((s, e))
    return css, cuts


def stack(items, prefix, style):
    """Items laid on one spot, shown in turn: (html) per item."""
    return "".join(f'<div class="t abs" style="{style};opacity:{1 if i == 0 else 0};animation-name:{prefix}{i}">{it}</div>'
                   for i, it in enumerate(items))


def text_block(x, y, w, kicker, title, line):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:12px">'
            f'<span class="lab">{kicker}</span><span style="font-size:48px;line-height:1.1">{title}</span>'
            f'<span style="font-size:18px;line-height:1.5;color:{INK}">{line}</span></div>')


# ── A Squad ───────────────────────────────────────────────────────────────────────────────

SQUAD = [("Needle", "Sparrow", "You"), ("Galleon", "Dolphin", "AI"), ("Anvil", "Rhino", "AI")]
SEAT_AT = [4, 20, 54]


def seat(i, x, y, w=544, h=600):
    call, hull, who = SQUAD[i]
    return f"""<article aria-label="Seat {i + 1}" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">
<div class="abs sl" aria-hidden="true" style="inset:0;background:{LINE}"></div>
<div class="abs sl" aria-hidden="true" style="inset:1px;background:rgba(0,4,31,.86)"></div>
<span class="lab abs" style="left:28px;top:24px">Seat {i + 1} - {who}</span>
<div class="t abs" style="inset:0;opacity:0;animation-name:seat{i}">
<img src="{HULL[hull]}" alt="{hull}" style="position:absolute;right:-20px;top:60px;width:380px;height:380px;object-fit:contain;animation:bob {5 + i}s ease-in-out infinite alternate">
<img src="{PILOT[call]}" alt="{call}" style="position:absolute;left:28px;top:96px;width:275px;height:350px;object-fit:contain">
<span class="abs" style="left:28px;top:476px;font-size:36px;line-height:1.1;text-transform:uppercase">{call}</span>
<span class="abs" style="left:28px;top:528px;font-size:18px;color:{DIM}">{hull}</span>
</div>
</article>"""


def board_a():
    css = "".join(f"@keyframes seat{i}{{0%,{s}%{{opacity:0;transform:translateY(28px)}}{s + 4}%,100%{{opacity:1;transform:translateY(0)}}}}"
                  for i, s in enumerate(SEAT_AT))
    body = (sea() + brief()
            + f'<span class="lab abs" style="left:96px;top:172px;color:{INK}">Your squad</span>'
            + "".join(seat(i, 96 + i * 592, 208) for i in range(3)) + bar())
    return page(f"R{REV}A.dc.html", body, css)


# ── B The Ark ─────────────────────────────────────────────────────────────────────────────

def rows(names, prefix, x, y, w, gap=58, size=22):
    """A quiet list; one row lit at a time, in time with the loop."""
    out = f'<ul class="abs" style="left:{x}px;top:{y}px;width:{w}px;margin:0;padding:0;list-style:none">'
    for i, n in enumerate(names):
        out += (f'<li style="position:relative;height:{gap}px;display:flex;align-items:center;font-size:{size}px;color:{DIM};box-shadow:inset 0 -1px 0 rgba(67,76,137,.6)">'
                f'<span>{n}</span>'
                f'<span class="t" aria-hidden="true" style="position:absolute;inset:0;display:flex;align-items:center;color:{INK};box-shadow:inset 0 -2px 0 {INK};'
                f'opacity:{1 if i == 0 else 0};animation-name:{prefix}{i}">{n}</span></li>')
    return out + "</ul>"


def board_b():
    css = "".join(fade(f"room{i}", s, STEPS[i + 1][1] if i < 3 else 100) for i, (_, s, _e) in enumerate(STEPS))
    body = (sea() + brief() + glow(60, 560, 1100, 260)
            + f'<img src="{ARK}" alt="The Ark" style="position:absolute;left:150px;top:250px;width:900px;height:422px;object-fit:contain;animation:arkDrift 16s ease-in-out infinite alternate">'
            + f'<img src="{ARK_TOP}" alt="" style="position:absolute;left:1560px;top:150px;width:240px;height:220px;object-fit:contain;opacity:.5;animation:turn 120s linear infinite">'
            + text_block(1180, 236, 400, "Aboard", "The Ark", "Four rooms are aboard.")
            + rows(ROOMS, "room", 1180, 450, 644, gap=72) + bar())
    return page(f"R{REV}B.dc.html", body, css)


# ── C Pilots ──────────────────────────────────────────────────────────────────────────────

ROSTER = ["Galleon", "Needle", "Sundial", "Hornet", "Anvil", "Flume", "Doe", "Manger"]


def board_c():
    css, _ = cycle(ROSTER[:4], "pil")
    big = stack([f'<img src="{PILOT[c]}" alt="{c}" style="width:418px;height:532px;object-fit:contain">' for c in ROSTER[:4]],
                "pil", "left:250px;top:190px;width:418px;height:532px")
    names = stack([f'<span style="font-size:48px;line-height:1.1;text-transform:uppercase">{c}</span>' for c in ROSTER[:4]],
                  "pil", "left:800px;top:296px;width:800px;height:56px")
    row = '<div class="abs" style="left:800px;top:520px;display:flex;gap:16px">'
    for i, c in enumerate(ROSTER):
        lit = (f'<img class="t" src="{PILOT[c]}" alt="" style="position:absolute;inset:0;width:110px;height:140px;object-fit:contain;opacity:{1 if i == 0 else 0};animation-name:pil{i}">'
               if i < 4 else "")
        row += (f'<figure style="position:relative;margin:0;width:110px;height:140px">'
                f'<img src="{PILOT[c]}" alt="{c}" style="width:110px;height:140px;object-fit:contain;opacity:.32">{lit}</figure>')
    row += "</div>"
    body = (sea() + brief() + glow(140, 600, 640, 200)
            + big + f'<span class="lab abs" style="left:800px;top:262px">Pilot</span>' + names
            + f'<span class="abs" style="left:800px;top:372px;font-size:18px;line-height:1.5">Eighteen pilots, each known by a callsign.</span>'
            + row + bar())
    return page(f"R{REV}C.dc.html", body, css)


# ── D Vessels ─────────────────────────────────────────────────────────────────────────────

FLEET = ["Sparrow", "Dolphin", "Manta", "Serpent", "Squirrel", "Rhino"]


def board_d():
    css, _ = cycle(FLEET[:4], "ves")
    big = stack([f'<img src="{HULL[v]}" alt="{v}" style="width:600px;height:600px;object-fit:contain;animation:bob 6s ease-in-out infinite alternate">'
                 for v in FLEET[:4]], "ves", "left:620px;top:160px;width:600px;height:600px")
    names = stack([f'<span style="font-size:48px;line-height:1.1">{v}</span>' for v in FLEET[:4]], "ves", "left:96px;top:330px;width:500px;height:56px")
    grid = '<div class="abs" style="left:1340px;top:330px;width:484px;display:grid;grid-template-columns:repeat(3,150px);gap:16px">'
    for i, v in enumerate(FLEET):
        lit = (f'<span class="t" aria-hidden="true" style="position:absolute;inset:0;box-shadow:inset 0 0 0 2px {INK};opacity:{1 if i == 0 else 0};animation-name:ves{i}"></span>'
               if i < 4 else "")
        grid += (f'<figure style="position:relative;margin:0;width:150px;height:150px;background:rgba(0,4,31,.7);box-shadow:inset 0 0 0 1px rgba(67,76,137,.7)">'
                 f'<img src="{HULL[v]}" alt="{v}" style="width:150px;height:150px;object-fit:contain">{lit}</figure>')
    grid += "</div>"
    body = (sea() + brief() + glow(520, 250, 800, 460, .16) + big
            + f'<span class="lab abs" style="left:96px;top:296px">Vessel</span>' + names
            + f'<span class="abs" style="left:96px;top:406px;width:440px;font-size:18px;line-height:1.5">Four abilities each: Charge, Mass, Space and Time.</span>'
            + f'<span class="lab abs" style="left:1340px;top:296px">The fleet</span>' + grid + bar())
    return page(f"R{REV}D.dc.html", body, css)


# ── E Toy Box ─────────────────────────────────────────────────────────────────────────────

def hexframe(x, y, s, inner):
    return (f'<div class="abs hexc" aria-hidden="true" style="left:{x}px;top:{y}px;width:{s}px;height:{round(s * .866)}px;background:{LINE}"></div>'
            f'<div class="abs hexc" style="left:{x + 2}px;top:{y + 2}px;width:{s - 4}px;height:{round(s * .866) - 4}px;background:rgba(0,4,31,.88)">{inner}</div>')


def board_e():
    css_t, _ = cycle(TOYS, "toy")
    css_h, _ = cycle(FLEET, "th")
    hs = round(640 * .866) - 4
    hulls = stack([f'<img src="{HULL[v]}" alt="{v}" style="width:420px;height:420px;object-fit:contain">' for v in FLEET],
                  "th", f"left:108px;top:{(hs - 420) // 2}px;width:420px;height:420px")
    hname = stack([f'<span style="font-size:28px">{v}</span>' for v in FLEET], "th", "left:1000px;top:800px;width:640px;height:34px;text-align:center")
    body = (sea() + brief() + glow(900, 200, 840, 560, .14)
            + text_block(96, 196, 520, "Toy Box", "Seven toys", "Fly a toy to use it.")
            + rows(TOYS, "toy", 96, 392, 520, gap=62)
            + hexframe(1000, 220, 640, hulls)
            + f'<span class="lab abs" style="left:1000px;top:182px;width:640px;text-align:center">Vessel Changer</span>' + hname + bar())
    return page(f"R{REV}E.dc.html", body, css_t + css_h)


# ── F The tour ────────────────────────────────────────────────────────────────────────────

TOUR = ["Squad", "The Ark", "Pilots", "Vessels", "Toy Box"]


def board_f():
    css, _ = cycle(TOUR, "ch")
    hs = round(520 * .866) - 4
    shows = [
        '<div style="display:flex;gap:24px;justify-content:center;padding-top:60px">' + "".join(
            f'<img src="{PILOT[c]}" alt="{c}" style="width:264px;height:336px;object-fit:contain">' for c, _h, _w in SQUAD) + "</div>",
        f'<img src="{ARK}" alt="The Ark" style="position:absolute;left:40px;top:60px;width:800px;height:375px;object-fit:contain;animation:arkDrift 16s ease-in-out infinite alternate">',
        f'<img src="{PILOT["Galleon"]}" alt="Galleon" style="position:absolute;left:260px;top:0;width:380px;height:484px;object-fit:contain">',
        f'<img src="{HULL["Sparrow"]}" alt="Sparrow" style="position:absolute;left:190px;top:-20px;width:520px;height:520px;object-fit:contain;animation:bob 6s ease-in-out infinite alternate">',
        ('<div style="position:absolute;left:190px;top:20px;width:520px;height:460px">' + hexframe(0, 0, 520,
            f'<img src="{HULL["Dolphin"]}" alt="Dolphin" style="position:absolute;left:88px;top:{(hs - 340) // 2}px;width:340px;height:340px;object-fit:contain">') + "</div>"),
    ]
    lines = ["Three seats, each a vessel and a pilot.", "Converter, Squads, Artifacts and Decor are aboard.",
             "Eighteen pilots, each known by a callsign.", "Four abilities each: Charge, Mass, Space and Time.",
             "Seven toys. Fly one to use it."]
    texts = [(f'<div style="display:flex;flex-direction:column;gap:12px"><span class="lab">{i + 1} of 5</span>'
              f'<span style="font-size:48px;line-height:1.1">{TOUR[i]}</span><span style="font-size:18px;line-height:1.5">{lines[i]}</span></div>')
             for i in range(5)]
    index = '<nav aria-label="The tour" class="abs" style="right:96px;top:72px;display:flex;gap:32px">'
    for i, n in enumerate(TOUR):
        index += (f'<span style="position:relative;font-size:18px;color:{DIM};padding-bottom:8px">{n}'
                  f'<span class="t" aria-hidden="true" style="position:absolute;left:0;top:0;right:0;bottom:0;color:{INK};box-shadow:inset 0 -2px 0 {INK};'
                  f'opacity:{1 if i == 0 else 0};animation-name:ch{i}">{n}</span></span>')
    index += "</nav>"
    body = (sea() + brief() + index + glow(140, 420, 1000, 340, .14)
            + stack(shows, "ch", "left:180px;top:250px;width:900px;height:500px")
            + stack(texts, "ch", "left:1180px;top:400px;width:644px;height:220px") + bar())
    return page(f"R{REV}F.dc.html", body, css)


# ── Compare: the bar on its own ───────────────────────────────────────────────────────────

def board_compare():
    held = [(0, "Connecting to shore", False, False, "Start: the first step is named before anything has moved."),
            (12, "Connecting to shore", False, False, "Step 1 of 4. Each segment is as long as its share of the work."),
            (41, "Building the arena", False, True, "A slow patch: the number holds, the glint keeps travelling, so it never looks frozen."),
            (75, "Settling the arena", False, False, "Step 3. A finished step's marker is filled."),
            (93, "Waiting for pilots", False, False, "Step 4. The bar only ever moves forward."),
            (100, "Ready", True, False, "Done: the bar turns Lime and says Ready. Lime appears nowhere else.")]
    body = (sea() + f'<header class="abs" style="left:96px;top:56px;display:flex;flex-direction:column;gap:6px"><span class="lab">The progress bar</span>'
            f'<span style="font-size:28px">Live on top, then held at six moments</span></header>'
            + bar(x=96, y=150, w=1100)
            + f'<span class="abs" style="left:1260px;top:176px;width:560px;font-size:18px;line-height:1.5">Live: one 16 second loop, the same bar on every board of this round.</span>')
    for i, (p, word, done, slow, note) in enumerate(held):
        y = 318 + i * 124
        body += (f'<div class="abs" aria-hidden="true" style="left:96px;top:{y - 20}px;width:1728px;height:1px;background:rgba(67,76,137,.6)"></div>'
                 + bar(x=96, y=y, w=1100, live=False, p=p, word=word, done=done, slow=slow, head=False)
                 + f'<span class="abs" style="left:1260px;top:{y + 6}px;width:560px;font-size:18px;line-height:1.5">{note}</span>')
    return page(f"R{REV}Compare.dc.html", body)


# ── Notes ─────────────────────────────────────────────────────────────────────────────────

def board_notes():
    cols = [
        ("Asked", ["Shombith, 2026-11-12: \"mockups for the animated smooth background with perfect progress bar ... maybe all parts of the game is shown in the loading menu. Like squads or the ark or the pilots or vessels the toybox.\""]),
        ("Shared by all six", ["The sea: three slow fields of the Blue family and three layers of sea lines. Nothing black, nothing still.",
                               "The bar: four segments for the four real steps, a tabular percentage, the step's name, Lime only at Ready.",
                               "ENTERING and the mode's name, top left. No tip line, no spinner.",
                               "When: loading into a game mode. The boot splash and sign-in scene are Rev 92."]),
        ("What each one shows", ["A Squad: three seats, each arriving as the load moves on.",
                                 "B The Ark: the ship drifting; its four rooms lit one per step.",
                                 "C Pilots: one pilot large, the roster beside.",
                                 "D Vessels: one hull large, the fleet beside.",
                                 "E Toy Box: the Vessel Changer cycling hulls, the seven toys.",
                                 "F The tour: all five in turn, so a long load is never one picture. PICKED (Shombith, 2026-11-12)."]),
        ("Against the build", ["Today: the Cell gate (Rev 33 A): an Omni crystal in a hex, the steps panel, the pilots panel, a tip and a slim bar.",
                               "New: the show replaces the crystal and both panels; the steps move into the bar itself.",
                               "Art: the hull cards, the roster's painted portraits, the Ark's model render. The game would show the live models.",
                               "Departs from the quiet rule (no idle motion) because moving was the brief."]),
    ]
    body = (f'<div class="abs" aria-hidden="true" style="inset:0;background:linear-gradient(180deg,{GROUND} 0,{DEEP} 100%)"></div>'
            f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:36px">Loading screens</span></header>'
            '<div class="abs" style="left:96px;top:200px;width:1728px;display:grid;grid-template-columns:repeat(4,1fr);gap:48px">')
    for head, items in cols:
        body += (f'<section style="display:flex;flex-direction:column;gap:16px;padding-top:16px;box-shadow:inset 0 1px 0 {LINE}"><span style="font-size:22px">{head}</span>'
                 + "".join(f'<span style="font-size:18px;line-height:1.5;color:{INK}">{t}</span>' for t in items) + "</section>")
    body += "</div>"
    return page(f"R{REV}Notes.dc.html", body, h=900, loop=False)


def main():
    for fn in (board_compare, board_a, board_b, board_c, board_d, board_e, board_f, board_notes):
        print("wrote", fn())


if __name__ == "__main__":
    main()
