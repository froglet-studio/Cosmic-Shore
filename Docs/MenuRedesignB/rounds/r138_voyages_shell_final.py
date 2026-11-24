#!/usr/bin/env python3
"""Rev 138 boards: the final Voyages app shell, every screen in order and what you press to reach the next (Frogma tab
"Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-24: "I like E but why cant I see the vessels screenshots, so draw me a final round of the app shell and
each screen and where would i click to get to the next screen or select vessels, pilots or anything else" and "do not
delete the old milestone 1 salvage mission we had keep it as well in the selection lot but do not update anything about it".

Every screen is the picked one (Board B with Full screen, the full screen, Seats A+B, Pilots, Party from Rev 134, Vessels E
from Rev 136), now with the game's own hangar previews for every vessel, and numbered marks on what you press: each mark
names the control, its button and the screen it opens. The Milestone 1 Crystal Run stands on the quay as it is today.

  Flow        the map of the shell: every screen and every press between them
  Board       the quay (five jobs: the four voyages and the Crystal Run as it is)
  Full        the HyperSea full screen
  Seats       Your Ark
  Vessels     Vessels E from a seat, with the hangar previews
  Pilots      the pilot picker
  Party       a friend still choosing; Set sail waits

    python3 Docs/MenuRedesignB/rounds/r138_voyages_shell_final.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "138"
_spec = importlib.util.spec_from_file_location("r136", HERE / "r136_voyages_vessels.py")
r136 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r136)
r135, r134, r133 = r136.r135, r136.r134, r136.r133

REV = int(_rev)
ROOT = r136.ROOT
INK, MUTE, SOFT, SEL = r133.INK, r133.MUTE, r133.SOFT, r133.SEL
ARK, J, FLEET, ORDER, AI_READY, DOES, WANTS = r133.ARK, r135.J, r135.FLEET, r135.ORDER, r135.AI_READY, r135.SALVAGE_DOES, r136.WANTS
page, window, kc, glyph = r133.page, r133.window, r133.kc, r133.glyph


def blob(i):
    return f"/_blob/{i}"


# The game's own hangar previews (Assets/_Graphics/Design Assests/Hangar/<Vessel>_Hangar_Preview.png), uploaded 2026-11-24.
PREVIEW = {
    "Angler": "2bbcb760b248d97f270182686438ce07", "Brittle Star": "3ce33466a19099d0580a15e1f88fc21c", "Dolphin": "3e80d1ae84ff932f0ecc1146465494ed",
    "Goby & Pistol": "35a4055c1928cae6fe15fb717bbd9383", "Grizzly": "c911c3c97c588f9ab9f3f6ddf29fcea1", "Honeyguide": "a1c50709187fb5330854b1073c2426ce",
    "Jacks": "f9a0a7387b7d5a1283a04cab8e81cb6c", "Jewel Wasp": "0116fd771f6be06fc5f097f98686baf7", "Manta": "b055b703c7bc4463c19a0d1206726820",
    "Meerkat": "f592d5e97bf1af45029474bd63184cc0", "Orca": "6236c22b69129fa11e951d1b1cdb4475", "Rhino": "724fee13da3a1ab23212692aae9b8251",
    "Scarab": "5bea380d58d75ab290ca0ddb780f30de", "Serpent": "2210d8886716462cf1bf88a386576bfc", "Shrike": "490e7d30c0463c838bd00853aac4ba06",
    "Sparrow": "6eca76f70dba0d50bbe2a3917c26f0d1", "Squirrel": "ff1ab021cf81efee788045cf3eb826e2", "Starling": "d41095f6b5346896c8161dc381c6ce54",
    "Stoat": "5aeb55461804abde819c7ecf2a9a6d93", "Swordfish": "b3a60a6b3d7aa5996dcb48b6882ba5e2", "Thresher": "1478882b58aa259ad1613f571892647f",
    "Wrasse": "c16f483dd503495ec84202a696155b77", "Urchin": "3aa4036597b0973de3bf6b4846cfd35a",
}
MISSING = [v for c in ORDER for v in FLEET[c] if v not in PREVIEW]          # Tiger Moth: no hangar preview yet
FADE = "-webkit-mask-image:radial-gradient(ellipse 62% 62% at 50% 50%,#000 50%,transparent 100%);mask-image:radial-gradient(ellipse 62% 62% at 50% 50%,#000 50%,transparent 100%)"


def hot(x, y, n, words, to, side="right"):
    """A numbered mark on a control: the ring on it, and a tag naming the press and the screen it opens."""
    ring = (f'<span class="abs num" style="left:{x - 20}px;top:{y - 20}px;width:40px;height:40px;border-radius:50%;z-index:20;'
            f'box-shadow:inset 0 0 0 2px {SEL},0 0 0 4px rgba(0,4,31,.7);display:flex;align-items:center;justify-content:center;'
            f'background:rgba(0,4,31,.85);font-size:18px;color:{SEL}">{n}</span>')
    pos = {"right": f"left:{x + 32}px;top:{y - 18}px;",
           "left": f"left:{x - 32}px;top:{y - 18}px;transform:translateX(-100%);",
           "above": f"left:{x}px;top:{y - 32}px;transform:translate(-50%,-100%);",
           "below": f"left:{x}px;top:{y + 32}px;transform:translateX(-50%);"}[side]
    tag = (f'<span class="abs" style="{pos}z-index:20;display:flex;flex-direction:column;gap:2px;padding:6px 12px;'
           f'background:rgba(0,4,31,.92);box-shadow:inset 0 0 0 1px {SEL};white-space:nowrap">'
           f'<span style="font-size:15px;color:{INK}">{words}</span><span style="font-size:14px;color:{SEL}">opens {to}</span></span>')
    return ring + tag


def inject(html, extra):
    """Add markup inside the board's root (and pull in anything an earlier round left between the root and </x-dc>)."""
    i = html.index("</x-dc>")
    j = html.rindex("</div>", 0, i)
    stray = html[j + 6:i].strip()
    return html[:j] + stray + extra + "</div>\n" + html[i:]


# ── Board: five jobs on the quay ────────────────────────────────────────────────────────────────────────────────────
def crystal_run_card(x):
    return (f'<button class="pick abs" type="button" aria-pressed="false" style="left:{x}px;top:190px;width:230px;height:420px;overflow:hidden;'
            f'background:#00010A;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)">'
            f'<img src="{ARK}" alt="" style="position:absolute;left:-60px;top:110px;width:360px;height:170px;object-fit:contain;opacity:.6">'
            f'<div class="abs" style="left:0;right:0;bottom:0;height:190px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.95) 50%)"></div>'
            f'<div class="abs" style="left:18px;right:18px;bottom:18px;display:flex;flex-direction:column;gap:8px;pointer-events:none">'
            f'<span class="lab" style="color:{SOFT}">Raid</span><span style="font-size:18px;line-height:1.3">Crystal Run to Tollmoor</span>'
            f'<span class="row" style="gap:8px">{glyph("Cultivator", 18, SOFT)}</span></div></button>')


def board():
    html = r135.board_full()
    for old, new in (("left:40px;top:190px;width:280px", "left:40px;top:190px;width:230px"),
                     ("left:340px;top:190px;width:280px", "left:280px;top:190px;width:230px"),
                     ("left:640px;top:190px;width:280px", "left:520px;top:190px;width:230px"),
                     ("left:940px;top:190px;width:280px", "left:760px;top:190px;width:230px")):
        assert old in html, old
        html = html.replace(old, new)
    html = html.replace("Four givers on the quay.", "Five jobs on the quay.")
    marks = (hot(155, 400, 1, "Press a job (D-pad, A)", "its giver's ask below")
             + hot(1640, 226, 2, "Full screen", "the HyperSea full screen", "below")
             + hot(900, 812, 3, "See every class (Y)", "Vessels", "below")
             + hot(1585, 1018, 4, "Take the job (A)", "Your Ark", "above"))
    html = inject(html, crystal_run_card(1000) + marks)
    return html.replace(f"<title>Rev {REV} BoardFull - The Quay", f"<title>Rev {REV} Board - The Quay", 1)


def full():
    html = r135.full_screen()
    marks = (hot(1560, 61, 1, "Back from full screen (B)", "the quay", "left")
             + hot(1480, 255, 2, "Press a job", "its pin and lane on the map", "left")
             + hot(370, 66, 3, "Zoom: the tier row, + and -", "Galaxy down to one Cell")
             + hot(1600, 970, 4, "Take the job (A)", "Your Ark", "above"))
    return inject(html, marks)


# ── Seats, Pilots, Party: Rev 134 as picked ─────────────────────────────────────────────────────────────────────────
def seats():
    body = r134.seats_top() + r134.FLOOR + "".join(r134.mix_seat(i, 120 + i * 580) for i in range(3)) + r134.sail_bar()
    marks = (hot(1530, 345, 1, "Change vessel: press the hull (X)", "Vessels")
             + hot(1322, 640, 2, "Press the pilot", "Pilots", "left")
             + hot(1280, 852, 3, "Invite a friend", "Party: their seat opens to them", "left")
             + hot(1585, 1018, 4, "Set sail (A)", "the voyage (in game)", "above")
             + hot(1700, 128, 5, "Back (B)", "the quay", "left"))
    return page(f"Rev {REV} Seats - Your Ark (Rev 134 as picked): what each press opens", window(3, body + marks))


def pilots():
    html = r134.page(f"Rev {REV} Pilots - the pilot picker (Rev 134 as picked)", r134.s_pilots())
    marks = (hot(150, 330, 1, "Press a pilot (D-pad)", "their scene and mood on the right")
             + hot(340, 870, 2, "Take the seat (A)", "Your Ark, the pilot seated")
             + hot(560, 870, 3, "Back (B)", "Your Ark, unchanged"))
    return inject(html, marks)


def party():
    html = r134.page(f"Rev {REV} Party - a friend still choosing; Set sail waits (Rev 134 as picked)", r134.s_party())
    return inject(html, hot(1585, 1018, 1, "Set sail lights when every friend is Ready", "the voyage", "above"))


# ── Vessels E, with the hangar previews ─────────────────────────────────────────────────────────────────────────────
def standing(v, x, w, on=False, dim=False, line=""):
    pic = (f'<img src="{blob(PREVIEW[v])}" alt="{v}" style="position:absolute;left:0;top:10px;width:{w}px;height:{w * 9 // 16}px;object-fit:cover;{FADE}">'
           if v in PREVIEW else
           f'<span style="position:absolute;left:0;right:0;top:60px;height:170px;display:flex;align-items:center;justify-content:center">'
           f'{glyph(r135.r_cls(v), 110, SOFT, 1.1)}</span>')
    return (f'<button class="pick abs" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:250px;width:{w}px;height:580px;background:none;'
            f'opacity:{.4 if dim else 1}">{pic}'
            f'<span class="abs" style="left:0;right:0;top:320px;font-size:22px;text-align:left">{v}</span>'
            f'<span class="abs" style="left:0;right:0;top:364px;height:{2 if on else 1}px;background:{SEL if on else "rgba(67,76,137,.6)"}"></span>'
            f'<span class="abs" style="left:0;right:0;top:382px;font-size:15px;color:{SOFT};text-align:left;line-height:1.4">{line}</span></button>')


def vessels():
    cls, sel = "Protector", "Scarab"
    vs = FLEET[cls]
    w, gap = 500, 90
    x0 = (1920 - (len(vs) * w + (len(vs) - 1) * gap)) // 2
    lines = {"Scarab": "Takes the hits meant for the Ark. The AI can fly it.", "Orca": "Needs a person in the seat.",
             "Thresher": "Needs a person in the seat."}
    head = (f'<div class="abs row" style="left:40px;right:40px;top:800px;justify-content:center;gap:14px">'
            f'<span style="font-size:15px;color:{INK}">{cls}s are wanted on this voyage</span><span style="font-size:15px;color:{SOFT}">'
            f'{DOES[cls]}.</span></div>')
    body = (r134.seats_top() + r136.seat_line("Seat 3  ·  flown by the AI  ·  choose a vessel") + r134.FLOOR
            + "".join(standing(v, x0 + i * (w + gap), w, v == sel, v not in AI_READY, lines[v]) for i, v in enumerate(vs))
            + head + r136.e_tabs(cls) + r136.bar("Take the vessel"))
    marks = (hot(658, 916, 1, "LB / RB or press a class", "that class on the floor", "below")
             + hot(300, 420, 2, "Press a vessel (D-pad)", "the choice; squad fit and pay update")
             + hot(1545, 1018, 3, "Take the vessel (A)", "Your Ark, Seat 3 now Scarab", "above")
             + hot(1340, 1018, 4, "Back (B)", "Your Ark, unchanged", "left"))
    return page(f"Rev {REV} Vessels - Vessels E opened from Seat 3, with the game's hangar previews", window(3, body + marks))


# ── Flow: the map of the shell ──────────────────────────────────────────────────────────────────────────────────────
def flow():
    nodes = {  # key: (x, y, title, sub)
        "board": (80, 300, "The quay", "Five jobs, the giver's ask, your crew answers"),
        "full": (80, 640, "HyperSea full screen", "Zoom, the lane, events, rivals"),
        "seats": (760, 300, "Your Ark", "Three seats: hull, pilot, mood, rank"),
        "vessels": (760, 640, "Vessels", "One class on the floor"),
        "pilots": (1440, 640, "Pilots", "Mood for this job, rank"),
        "party": (1440, 90, "Party", "Friends pick their own seats"),
        "sail": (1440, 300, "The voyage", "In game: the intro card"),
    }
    W_, H_ = 400, 140
    boxes = "".join(
        f'<div class="abs" style="left:{x}px;top:{y}px;width:{W_}px;height:{H_}px;box-sizing:border-box;padding:22px 24px;display:flex;flex-direction:column;gap:10px;'
        f'background:rgba(0,4,31,.8);box-shadow:inset 0 0 0 1px {"rgba(67,76,137,.9)" if k != "sail" else SOFT}">'
        f'<span style="font-size:22px">{t}</span><span style="font-size:15px;color:{SOFT}">{s}</span></div>'
        for k, (x, y, t, s) in nodes.items())

    def edge(a, b, words, bend=0, lx=None, ly=None):
        ax, ay = nodes[a][0], nodes[a][1]
        bx, by = nodes[b][0], nodes[b][1]
        if ay == by:   # side by side
            x1, y1, x2, y2 = (ax + W_, ay + H_ // 2 + bend, bx, by + H_ // 2 + bend) if bx > ax else (ax, ay + H_ // 2 + bend, bx + W_, by + H_ // 2 + bend)
        else:          # one above the other
            x1, y1, x2, y2 = (ax + W_ // 2 + bend, ay + H_, bx + W_ // 2 + bend, by) if by > ay else (ax + W_ // 2 + bend, ay, bx + W_ // 2 + bend, by + H_)
            if ax != bx:
                x1, y1, x2, y2 = (ax + W_, ay + H_ // 2, bx, by + H_ // 2)
        mx, my = (x1 + x2) // 2, (y1 + y2) // 2
        lab = (f'<span class="abs" style="left:{lx if lx is not None else mx}px;top:{ly if ly is not None else my - 30}px;transform:translateX(-50%);'
               f'font-size:14px;color:{INK};white-space:nowrap;background:rgba(0,4,31,.9);padding:2px 8px">{words}</span>')
        return (f'<path d="M{x1},{y1} L{x2},{y2}" fill="none" stroke="{SEL}" stroke-width="2" marker-end="url(#ah)"></path>', lab)

    es = [edge("board", "seats", "Take the job (A)", -24), edge("seats", "board", "Back (B)", 24, None, 382),
          edge("board", "full", "Full screen", -60), edge("full", "board", "Back from full screen (B)", 60, 380, 520),
          edge("full", "seats", "Take the job (A)", 0, 600, 560),
          edge("seats", "vessels", "Press the hull (X)", -60, 830, 520), edge("vessels", "seats", "Take the vessel (A) / Back (B)", 60, 1090, 520),
          edge("seats", "pilots", "Press the pilot", 0, 1300, 560),
          edge("seats", "sail", "Set sail (A)", 0),
          edge("seats", "party", "Invite a friend", 0, 1300, 200)]
    svg = ('<svg class="abs" style="left:0;top:0" width="1920" height="1080" aria-hidden="true"><defs><marker id="ah" viewBox="0 0 10 10" refX="9" refY="5" '
           f'markerWidth="8" markerHeight="8" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{SEL}"></path></marker></defs>'
           + "".join(p for p, _ in es) + '</svg>')
    labels = "".join(l for _, l in es)
    head = (f'<div class="abs" style="left:80px;top:40px;display:flex;flex-direction:column;gap:8px">'
            f'<span class="hd" style="font-size:28px">The Voyages shell: every screen, every press</span>'
            f'<span style="font-size:15px;color:{SOFT}">Back (B) always returns one screen. The Pilots picker returns to Your Ark with Take the seat (A) or Back (B).</span></div>')
    foot = (f'<span class="abs" style="left:80px;top:900px;width:1760px;font-size:15px;color:{SOFT};line-height:1.5">'
            f'On the quay, See every class (Y) also opens Vessels, to browse. The Crystal Run stays on the quay as it is today; '
            f'it takes the same path. The next boards are each screen with its presses marked.</span>')
    return page(f"Rev {REV} Flow - the Voyages shell: every screen and every press between them", head + svg + boxes + labels + foot)


def notes():
    rows = [
        ("Asked", "Shombith, 24 Nov: \"I like E but why cant I see the vessels screenshots, so draw me a final round of the app shell and each "
                  "screen and where would i click to get to the next screen or select vessels, pilots or anything else\"; \"do not delete the old "
                  "milestone 1 salvage mission ... keep it as well in the selection lot but do not update anything about it\"."),
        ("Picked and kept", "Board B with Full screen (Rev 135), the full screen (Rev 135), Seats A+B, Pilots, Party (Rev 134), Vessels E (Rev 136). "
                            "Nothing is redrawn; the marks are the only addition."),
        ("Screenshots", "Vessels now shows the game's own hangar previews (Assets/_Graphics/Design Assests/Hangar), faded at the edge so the "
                        f"hex floor does not frame them. Rev 136 used the six hull renders the board had and a glyph for the rest. Still no "
                        f"preview: {', '.join(MISSING) or 'none'}."),
        ("Crystal Run", "The Milestone 1 Crystal Run stands on the quay as the fifth job, as it is in the game today (its own title, no giver, "
                        "Raid). Nothing about it changes."),
        ("Marks", "Each cyan ring sits on the control; its tag says the press (and the pad button) and the screen it opens. Marks are for "
                  "this board only and are not part of the screen."),
        ("In game", "Approved; the in-game UI is built in its own thread (Build the in-game voyage UI) from Rev 134 and Rev 137."),
    ]
    html = r133.notes_page("notes: the final Voyages app shell", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


BOARDS = {"Flow": flow, "Board": board, "Full": full, "Seats": seats, "Vessels": vessels, "Pilots": pilots, "Party": party}

if __name__ == "__main__":
    for key, fn in BOARDS.items():
        (ROOT / f"R{REV}{key}.dc.html").write_text(fn(), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(BOARDS) + 1, "boards to", ROOT)
