"""Draws docs/architecture/network.png (ARCHITECTURE.md Figure 5): START ONLINE through UGS Lobby + Relay. Usage: python3 tools/draw_network_figure.py docs/architecture/network.png"""
import sys, math
from PIL import Image, ImageDraw, ImageFont
W, H = 1960, 780
img = Image.new("RGB", (W, H), "white")
d = ImageDraw.Draw(img)
F = "/usr/share/fonts/truetype/liberation/LiberationSans-%s.ttf"
title = ImageFont.truetype(F % "Bold", 30)
head = ImageFont.truetype(F % "Bold", 27)
body = ImageFont.truetype(F % "Regular", 21)
small = ImageFont.truetype(F % "Regular", 20)

def box(x0, y0, x1, y1, fill, outline, name, lines):
    d.rounded_rectangle((x0, y0, x1, y1), 18, fill=fill, outline=outline, width=3)
    cx = (x0 + x1) / 2
    total = 34 + 28 * len(lines)
    y = (y0 + y1) / 2 - total / 2
    d.text((cx, y), name, font=head, fill="#111", anchor="mt")
    y += 40
    for l in lines:
        d.text((cx, y), l, font=body, fill="#333", anchor="mt"); y += 28

def arrow(x0, y0, x1, y1, both=False, label=None):
    d.line((x0, y0, x1, y1), fill="#444", width=3)
    def head_at(xa, ya, xb, yb):
        a = math.atan2(yb - ya, xb - xa)
        p = [(xb, yb), (xb - 18 * math.cos(a - 0.4), yb - 18 * math.sin(a - 0.4)), (xb - 18 * math.cos(a + 0.4), yb - 18 * math.sin(a + 0.4))]
        d.polygon(p, fill="#444")
    head_at(x0, y0, x1, y1)
    if both: head_at(x1, y1, x0, y0)
    if label:
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        bb = d.textbbox((mx, my), label, font=small, anchor="mm")
        d.rectangle((bb[0] - 6, bb[1] - 4, bb[2] + 6, bb[3] + 4), fill="white")
        d.text((mx, my), label, font=small, fill="#222", anchor="mm")

d.text((32, 28), "START ONLINE: players on any network meet through UGS (Lobby + Relay), as the Unity build does", font=title, fill="#111")
G, GO = "#e8f4e8", "#2e8b3e"
box(40, 250, 560, 490, G, GO, "Host game (machine A)", ["signs in to UGS; allocates on Relay,", "starts the NetworkManager host,", "creates the lobby with the join code"])
box(1400, 250, 1920, 490, G, GO, "Client game (machine B)", ["queries or is invited, joins the lobby,", "joins Relay by the lobby's code, loads the", "host's scenes, snapshot, then deltas"])
box(680, 100, 1280, 300, "#fdf6dc", "#c9a400", "UGS Lobby", ["sessions, rosters, invite properties,", "the host's Relay join code (member data);", "the host heartbeats it every 15 s"])
box(680, 450, 1280, 690, "#fdf6dc", "#c9a400", "UGS Relay", ["netcode frames [length][kind][payload]", "over Froglet's UDP transport, in Unity", "Relay's protocol; allocations by REST"])
arrow(560, 300, 680, 220, both=True, label="create")
arrow(1400, 300, 1280, 220, both=True, label="query, join")
arrow(560, 440, 680, 540, both=True, label="UDP")
arrow(1400, 440, 1280, 540, both=True, label="UDP")
d.text((W / 2, 740), "START plays offline. There is no LAN play, local server or shared folder: one UGS environment (\"amoebius\" by default) is where every Amoebius player meets.",
       font=small, fill="#333", anchor="mm")
img.save(sys.argv[1])
