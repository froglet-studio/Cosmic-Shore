#!/usr/bin/env python3
"""The Tide and the quest chain on Home: one round of Frogma boards (/frogma section 3).

Garrett, 2026-11-20, on the Loop Atlas map (develop era.tide): "This and the quest chain should take over the two
little windows under the profile on the home screen." The two windows are Home's docked Up next cards
(HomeUpNext: the weekly challenge and the current quest, Docs/MENU_REDESIGN_B.md sections 21.2 and 36).

Writes the round's boards into an output folder as RTide<Key>.dc.html. The round number is claimed when the round is
published on Frogma: `frogma.py plan` takes the next free R<N>, and the files are renamed to it then.

    python3 Docs/MenuRedesignB/rounds/tide_home.py --out <dir>

Everything on Home except the two cards is drawn as the built screen (R21F1Home): the nav band, the pilot chip, the
hub row, the friends tab, the settings gear, the notifications column. Images are the canvas's own uploads.
"""
import argparse
import os

CELL = "/_blob/423125b7f05629e899e1ec1f3e076f05"
NAV = [("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5", .7), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2", .7),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e", 1), ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de", .7)]
AVATAR = "/_blob/e706037f2062090a1514ed6e21e28a7b"
WEEK_ART = "/_blob/398dbfccca32c6c4d62b1bbca09facf0"

# Tokens (Docs/UI/froglet-tokens.json, Deep Sea): Blue is neutral, Jade the focus, Gold a spend, Lime the one CTA.
INK, SOFT, OFF = "#E6E9FF", "#747BAD", "#5C5F70"
PANEL, LINE, LINE2 = "#00041F", "#434C89", "#434C89"
JADE, GOLD = "#00D4FF", "#FFAE00"

HELMET = """<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.sl{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.sls{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.slx{clip-path:polygon(5px 0,100% 0,100% calc(100% - 5px),calc(100% - 5px) 100%,0 100%,0 5px)}
.oct{clip-path:polygon(29% 0,71% 0,100% 29%,100% 71%,71% 100%,29% 100%,0 71%,0 29%)}
.hex{clip-path:polygon(25% 0,75% 0,100% 50%,75% 100%,25% 100%,0 50%)}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:#747BAD}
.num{font-variant-numeric:tabular-nums}
.abs{position:absolute}
.card{appearance:none;border:0;padding:0;margin:0;font:inherit;color:inherit;text-align:left;cursor:pointer;background:none;display:block}
.pick{position:absolute;inset:0;appearance:none;border:0;padding:0;background:none;cursor:pointer}
.pick:focus{outline:none}
.mini{appearance:none;border:0;width:26px;height:26px;padding:0;cursor:pointer;background:#222645;color:#E6E9FF;font-family:'Chakra Petch',sans-serif;font-size:14px}
</style>
</helmet>"""


def frame(edge=LINE):
    """A slivered card: the outline on a background layer, so nothing inside is clipped (Rev 51 trap)."""
    return (f'<div class="abs sl" style="inset:0;background:{edge}"></div>'
            f'<div class="abs sl" style="inset:1px;background:{PANEL}"></div>')


def fold_close(top=12):
    return (f'<div class="abs" style="right:12px;top:{top}px;display:flex;flex-direction:column;gap:6px">'
            f'<button class="mini slx" aria-label="Fold">&#8211;</button>'
            f'<button class="mini slx" aria-label="Close">&#215;</button></div>')


def tide_clock(x, y, w, week=3, of=6, day=0.4, h=6):
    """The Tide's six weeks as segments: passed weeks lit, this week filled to today, the rest dark."""
    gap = 4
    seg = (w - gap * (of - 1)) / of
    out = []
    for i in range(of):
        left = x + i * (seg + gap)
        out.append(f'<div class="abs" style="left:{left:.1f}px;top:{y}px;width:{seg:.1f}px;height:{h}px;background:#222645"></div>')
        fill = 1 if i < week - 1 else (day if i == week - 1 else 0)
        if fill:
            out.append(f'<div class="abs" style="left:{left:.1f}px;top:{y}px;width:{seg * fill:.1f}px;height:{h}px;'
                       f'background:{INK if i == week - 1 else SOFT}"></div>')
    return "".join(out)


def chain(x, y, steps, current, r=9, gap=78, labels=True):
    """The quest chain: done links solid, the current one outlined in white, the ones ahead dark. No colour by element."""
    out = []
    n = len(steps)
    for i in range(n - 1):
        lit = i < current
        out.append(f'<div class="abs" style="left:{x + i * gap + r * 2}px;top:{y + r - 1}px;width:{gap - r * 2}px;height:2px;'
                   f'background:{SOFT if lit else "#222645"}"></div>')
    for i, s in enumerate(steps):
        cx = x + i * gap
        if i < current:
            dot = f'background:{SOFT}'
        elif i == current:
            dot = f'background:{PANEL};box-shadow:inset 0 0 0 2px {INK}'
        else:
            dot = f'background:#222645'
        out.append(f'<div class="abs hex" style="left:{cx}px;top:{y}px;width:{r * 2}px;height:{r * 2}px;{dot}"></div>')
        if labels:
            col = INK if i == current else SOFT
            out.append(f'<div class="abs" style="left:{cx - 22}px;top:{y + r * 2 + 6}px;width:{r * 2 + 44}px;text-align:center;'
                       f'font-size:14px;color:{col};white-space:nowrap">{s}</div>')
    return "".join(out)


QUEST = ["Race", "Joust", "Crystals", "Cell", "Voyage"]


def home(dock, title):
    nav = []
    nav.append(f'<div class="abs" style="left:0;top:0;width:1920px;height:84px;border-bottom:1px solid #222645"></div>')
    xs = [622, 702, 866, 989, 1128, 1264]
    nav.append(f'<div class="abs lab" style="left:622px;top:30px;border:1px solid {LINE2};padding:3px 8px;font-size:14px">LT</div>')
    for (name, src, op), x in zip(NAV, xs[1:5]):
        on = name == "HOME"
        nav.append(f'<div class="abs" style="left:{x}px;top:28px;display:flex;align-items:center;gap:10px">'
                   f'<img src="{src}" alt="" style="width:26px;height:26px;object-fit:contain;opacity:{op}">'
                   f'<span style="font-family:Chakra Petch,sans-serif;font-weight:600;font-size:18px;letter-spacing:.12em;'
                   f'color:{INK if on else "#E6E9FF"}">{name}</span></div>')
    nav.append(f'<div class="abs" style="left:989px;top:62px;width:91px;height:2px;background:{JADE}"></div>')
    nav.append(f'<div class="abs lab" style="left:1264px;top:30px;border:1px solid {LINE2};padding:3px 8px;font-size:14px">RT</div>')

    pilot = (f'<div class="abs" style="left:40px;top:108px;width:420px;height:112px">{frame()}'
             f'<img class="abs oct" src="{AVATAR}" alt="" style="left:20px;top:20px;width:72px;height:72px;object-fit:cover">'
             f'<div class="abs" style="left:116px;top:16px;font-size:28px;color:{INK}">Pilot</div>'
             f'<div class="abs lab" style="left:116px;top:52px;color:{JADE}">View profile &#8250;</div>'
             f'<div class="abs num" style="left:116px;top:76px;font-size:15px;color:#E6E9FF">'
             f'<span style="color:{GOLD}">&#9670;</span> 1,250</div></div>')

    side = []
    for i, (k, v) in enumerate([("PARTY", "Kestrel joined your party"), ("UNLOCKED", "Intensity 3 for Skim Race")]):
        side.append(f'<div class="abs" style="left:1480px;top:{196 + i * 80}px;width:400px;height:60px">{frame("#222645")}'
                    f'<div class="abs lab" style="left:44px;top:12px;color:#E6E9FF">{k}</div>'
                    f'<div class="abs" style="left:44px;top:32px;font-size:14px;color:{SOFT}">{v}</div></div>')
    gear = (f'<div class="abs hex" style="left:1816px;top:108px;width:64px;height:56px;background:#434C89;display:flex;'
            f'align-items:center;justify-content:center;color:{INK};font-size:28px">&#9881;</div>')
    friends = (f'<div class="abs" style="left:0;top:484px;width:44px;height:208px;background:#00041F;border:1px solid #222645">'
               f'<div class="abs lab" style="left:8px;top:170px;transform:rotate(-90deg);transform-origin:left top;'
               f'white-space:nowrap;font-size:14px">3 friends</div></div>')
    hubs = []
    for i, name in enumerate(["TOY BOX", "ARCADE", "ARENA", "VOYAGE"]):
        hubs.append(f'<div class="abs" style="left:{495 + i * 240}px;top:970px;width:210px;height:54px">{frame("#222645")}'
                    f'<div class="abs" style="inset:0;display:flex;align-items:center;justify-content:center;'
                    f'font-family:Chakra Petch,sans-serif;font-weight:600;font-size:18px;letter-spacing:.1em;color:#E6E9FF">'
                    f'{name}</div></div>')
    hint = (f'<div class="abs" style="left:960px;top:520px;width:266px;height:48px">{frame("#222645")}'
            f'<div class="abs lab" style="left:20px;top:16px;color:#E6E9FF;font-size:15px">Tap the cell to fly</div></div>')
    return (f'<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00010A; '
            f'color: {INK}; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif" aria-label="{title}">'
            f'<img src="{CELL}" alt="" style="position:absolute;left:0;top:0;width:1920px;height:1080px;object-fit:cover">'
            + "".join(nav) + pilot + dock + "".join(side) + gear + friends + hint + "".join(hubs) + '</div>')


# ---- the options: only the dock under the pilot chip differs ---------------------------------------------------

def option_a():
    """Two cards, the slots Home already has (480 wide, under the pilot chip)."""
    tide = (f'<div class="abs" style="left:40px;top:236px;width:420px;height:140px"><button class="pick" aria-label="The Tide"></button>{frame()}'
            f'<div class="abs lab" style="left:20px;top:16px;color:{INK}">The Tide</div>'
            f'<div class="abs lab num" style="left:120px;top:16px">Week 3 of 6 &#183; turns in 26d</div>'
            + tide_clock(20, 42, 340) +
            f'<div class="abs lab" style="left:20px;top:59px;font-size:14px">Today</div>'
            f'<div class="abs" style="left:112px;top:56px;font-size:15px;color:{INK}">Salvage the Vault Field</div>'
            f'<div class="abs num" style="left:112px;top:76px;font-size:14px;color:{SOFT}">Ends in 14h &#183; pays 120</div>'
            f'<div class="abs lab" style="left:20px;top:101px;font-size:14px">This week</div>'
            f'<div class="abs" style="left:112px;top:98px;font-size:15px;color:{INK}">Skim Race</div>'
            f'<div class="abs num" style="left:112px;top:118px;font-size:14px;color:{SOFT}">Your best 01:24</div>'
            + fold_close() + '</div>')
    quest = (f'<div class="abs" style="left:40px;top:388px;width:420px;height:92px"><button class="pick" aria-label="Quest chain"></button>{frame()}'
             f'<div class="abs lab" style="left:20px;top:16px;color:{INK}">Quest chain</div>'
             f'<div class="abs lab" style="left:150px;top:16px">First flight</div>'
             + chain(28, 40, QUEST, 2, gap=72, labels=False) +
             f'<div class="abs" style="left:20px;top:64px;font-size:14px;color:{INK}">Next: collect 12 crystals in Crystal Capture</div>'
             + fold_close() + '</div>')
    return option_wrap(tide + quest)


def option_b():
    """One card, two tabs: The Tide and Quests share the space one card used."""
    tabs = (f'<div class="abs" style="left:20px;top:14px;display:flex;gap:22px">'
            f'<span class="lab" style="color:{INK};border-bottom:2px solid {JADE};padding-bottom:6px">The Tide</span>'
            f'<span class="lab" style="padding-bottom:6px">Quests</span></div>')
    body = (f'<div class="abs lab num" style="left:20px;top:50px">Week 3 of 6 &#183; turns in 26d</div>'
            + tide_clock(20, 72, 340) +
            f'<div class="abs" style="left:20px;top:92px;width:178px">'
            f'<div class="lab" style="font-size:14px">Today</div>'
            f'<div style="font-size:15px;color:{INK};margin-top:4px">Salvage the Vault Field</div>'
            f'<div class="num" style="font-size:14px;color:{SOFT};margin-top:2px">14h &#183; pays 120</div></div>'
            f'<div class="abs" style="left:200px;top:92px;width:160px">'
            f'<div class="lab" style="font-size:14px">This week</div>'
            f'<div style="font-size:15px;color:{INK};margin-top:4px">Skim Race</div>'
            f'<div class="num" style="font-size:14px;color:{SOFT};margin-top:2px">Best 01:24</div></div>'
            f'<div class="abs" style="left:20px;top:160px;right:20px;height:1px;background:#222645"></div>'
            f'<div class="abs lab" style="left:20px;top:172px;font-size:14px">Quest chain &#183; first flight &#183; 3 of 5</div>'
            f'<div class="abs" style="left:20px;top:192px;font-size:14px;color:{SOFT}">Next: collect 12 crystals in Crystal Capture</div>')
    card = (f'<div class="abs" style="left:40px;top:236px;width:420px;height:226px"><button class="pick" aria-label="Up next"></button>{frame()}'
            + tabs + body + fold_close() + '</div>')
    return option_wrap(card)


def option_c():
    """Two strips, folded to one line each, the way Home starts; focus opens one to the full card."""
    tide = (f'<div class="abs" style="left:40px;top:236px;width:420px;height:56px"><button class="pick" aria-label="The Tide"></button>{frame()}'
            f'<div class="abs lab" style="left:20px;top:12px;color:{INK}">The Tide</div>'
            f'<div class="abs num" style="left:120px;top:10px;font-size:14px;color:{SOFT}">Week 3 of 6 &#183; today: Vault Field, 14h</div>'
            + tide_clock(20, 38, 380, h=4) + '</div>')
    quest = (f'<div class="abs" style="left:40px;top:304px;width:420px;height:178px"><button class="pick" aria-label="Quest chain"></button>'
             f'{frame(JADE)}'
             f'<div class="abs lab" style="left:20px;top:14px;color:{INK}">Quest chain</div>'
             f'<div class="abs lab" style="left:150px;top:14px">First flight &#183; 3 of 5</div>'
             + chain(32, 46, QUEST, 2, gap=82) +
             f'<div class="abs" style="left:20px;top:100px;font-size:15px;color:{INK}">Collect 12 crystals in Crystal Capture</div>'
             f'<div class="abs num" style="left:20px;top:124px;font-size:14px;color:{SOFT}">7 of 12 &#183; unlocks the Cell</div>'
             f'<div class="abs lab" style="left:20px;top:150px;font-size:14px;color:{JADE}">Focused &#183; press A to open</div>'
             + '</div>')
    return option_wrap(tide + quest)


def option_wrap(dock):
    return dock


def compare():
    """The two cards of each option side by side, at 1:1 over the panel colour."""
    cols = []
    for i, (key, name, fn) in enumerate([("A", "A - two cards (recommended)", option_a),
                                         ("B", "B - one card, two tabs", option_b),
                                         ("C", "C - folded strips, focus opens", option_c)]):
        x = 60 + i * 620
        cols.append(f'<div class="abs lab" style="left:{x}px;top:120px;color:{INK};font-size:15px">{name}</div>'
                    f'<div class="abs" style="left:{x - 40}px;top:-60px;width:520px;height:700px">{fn()}</div>')
    return (f'<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; '
            f'color: {INK}; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">'
            f'<div class="abs" style="left:60px;top:48px;font-family:Chakra Petch,sans-serif;font-weight:700;font-size:28px;'
            f'letter-spacing:.06em">The Tide and the quest chain, under the pilot chip</div>'
            f'<div class="abs" style="left:0;top:140px;width:1920px;height:600px">{"".join(cols)}</div>'
            f'<div class="abs" style="left:60px;top:780px;width:1800px;font-size:18px;line-height:1.6;color:{SOFT}">'
            f'Same content in all three: the Tide clock (six weeks, this one filled to today), today\'s job and when it ends, '
            f'the card of the week and your best on it, and the quest chain with the next goal. Nothing moves at rest. '
            f'Nothing is coloured by element; Jade is only the focus.</div></div>')


def notes():
    lines = [
        ("Asked", "Garrett, 2026-11-20, on the Loop Atlas map: “This and the quest chain should take over the two "
                  "little windows under the profile on the home screen.” The two windows are the weekly challenge "
                  "and current quest cards (HomeUpNext)."),
        ("The Tide", "The long game’s clock (Loop Atlas era.tide): each day one job that expires, each week one card "
                     "(today’s weekly challenge becomes the Tide’s card of the week), each six weeks the meta turns."),
        ("Quest chain", "The current quest shown as its chain: what is done, the one you are on, what it opens next."),
        ("A, recommended", "Keeps the two slots players already know and the fold / close each card has, so the build "
                           "is HomeUpNext\u2019s two cards re-filled, not a new layout. The pair ends at y 480, above the "
                           "friends handle (section 36\u2019s rule)."),
        ("B", "Least room on Home; the quest chain is a line, not a chain, until its tab is opened."),
        ("C", "Calmest at rest; one more press to read either card."),
        ("Data", "Today’s job and the six-week clock have no service yet: the voyage job board and the living sea’s "
                 "tick are the sources (Docs/Voyages/DEPTH.md). The card of the week is WeeklyChallengeService. The chain "
                 "is GameModeProgressionService / QuestFrontier."),
        ("Words", "Every word on the cards is an authored field (HomeUpNext’s Words header)."),
    ]
    body = "".join(f'<div style="display:grid;grid-template-columns:220px 1fr;gap:24px;margin-bottom:22px">'
                   f'<div class="lab" style="color:{INK};font-size:15px">{k}</div>'
                   f'<div style="font-size:18px;line-height:1.55;color:#E6E9FF">{v}</div></div>' for k, v in lines)
    return (f'<div style="width: 1920px; height: 900px; position: relative; overflow: hidden; background: #00041F; '
            f'color: {INK}; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">'
            f'<div class="abs" style="left:80px;top:60px;font-family:Chakra Petch,sans-serif;font-weight:700;font-size:28px">'
            f'Notes: The Tide and the quest chain on Home</div>'
            f'<div class="abs" style="left:80px;top:140px;width:1700px">{body}</div></div>')


def board(title, inner, h=1080):
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>{title}</title>\n'
            f'<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>{HELMET}\n{inner}\n</x-dc>\n'
            f'<script type="text/x-dc" data-dc-script data-props=\'{{"$preview":{{"width":1920,"height":{h}}}}}\'>\n'
            f'class Component extends DCLogic {{\nrenderVals() {{\nreturn {{}};\n}}\n}}\n</script>\n</body>\n</html>\n')


BOARDS = {
    "Compare": ("The Tide on Home - compare", compare),
    "A": ("The Tide on Home - A, two cards (recommended)", lambda: home(option_a(), "A")),
    "B": ("The Tide on Home - B, one card, two tabs", lambda: home(option_b(), "B")),
    "C": ("The Tide on Home - C, folded strips", lambda: home(option_c(), "C")),
    "Notes": ("The Tide on Home - notes", notes, 900),
}


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", required=True)
    ap.add_argument("--prefix", default="RTide", help="board file prefix; R<N> once the round number is claimed")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    for key, (title, fn, *h) in BOARDS.items():
        path = os.path.join(a.out, f"{a.prefix}{key}.dc.html")
        with open(path, "w", encoding="utf-8") as f:
            f.write(board(title, fn(), *h))
        print("wrote", path)


if __name__ == "__main__":
    main()
