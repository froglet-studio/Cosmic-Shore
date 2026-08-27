#!/usr/bin/env python3
"""Generate the six objective-metric glyphs as pure-white silhouettes (petal convention:
white RGBA, tinted/used as-is by UI). Drawn 4x supersampled, downsampled to 256."""
import os
from PIL import Image, ImageDraw

S = 1024          # supersample canvas
OUT = 256
DIR = 'Assets/_Graphics/ObjectiveIcons'
os.makedirs(DIR, exist_ok=True)
W = (255, 255, 255, 255)


def canvas():
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def P(*pts):
    return [(x * S, y * S) for x, y in pts]


def carve_line(img, a, b, width):
    """Erase a straight groove (alpha -> 0)."""
    mask = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(mask)
    d.line(P(a, b), fill=255, width=int(width * S))
    img.putalpha(Image.composite(Image.new('L', (S, S), 0), img.getchannel('A'), mask))


def carve_circle(img, c, r):
    mask = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([ (c[0]-r)*S, (c[1]-r)*S, (c[0]+r)*S, (c[1]+r)*S ], fill=255)
    img.putalpha(Image.composite(Image.new('L', (S, S), 0), img.getchannel('A'), mask))


def save(img, name):
    img = img.resize((OUT, OUT), Image.LANCZOS)
    img.save(os.path.join(DIR, name))
    print(name)


# 1. CRYSTAL (Crystals / Omni / Elemental) - classic faceted gem
img, d = canvas()
d.polygon(P((0.30, 0.14), (0.70, 0.14), (0.92, 0.40), (0.50, 0.92), (0.08, 0.40)), fill=W)
for a, b in [((0.08, 0.40), (0.92, 0.40)),          # girdle
             ((0.30, 0.14), (0.38, 0.40)), ((0.70, 0.14), (0.62, 0.40)),  # crown facets
             ((0.38, 0.40), (0.50, 0.92)), ((0.62, 0.40), (0.50, 0.92))]: # pavilion facets
    carve_line(img, a, b, 0.022)
save(img, 'objective_crystal.png')

# 2. JOUST - two crossed lances, tips up-and-out
img, d = canvas()
for flip in (1, -1):
    def X(x):
        return 0.5 + flip * (x - 0.5)
    # tapered shaft: butt lower-corner, tip opposite upper-corner
    d.polygon(P((X(0.12), 0.90), (X(0.20), 0.94), (X(0.90), 0.16), (X(0.86), 0.12)), fill=W)
    # lance guard (small disc near the butt)
    cx, cy = X(0.30), 0.76
    r = 0.055
    d.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=W)
save(img, 'objective_joust.png')

# 3. GOAL - ball through a ring
img, d = canvas()
cx, cy, ro, ri = 0.5, 0.44, 0.32, 0.22
d.ellipse([(cx - ro) * S, (cy - ro) * S, (cx + ro) * S, (cy + ro) * S], fill=W)
carve_circle(img, (cx, cy), ri)
carve_circle(img, (0.66, 0.72), 0.165)              # clearance so the ball reads in front
bd = ImageDraw.Draw(img)
bx, by, br = 0.66, 0.72, 0.125
bd.ellipse([(bx - br) * S, (by - br) * S, (bx + br) * S, (by + br) * S], fill=W)
save(img, 'objective_goal.png')

# 4. PRISM DESTROYED - iso cube split by a crack
img, d = canvas()
d.polygon(P((0.5, 0.14), (0.80, 0.32), (0.80, 0.68), (0.5, 0.86), (0.20, 0.68), (0.20, 0.32)), fill=W)
for a, b in [((0.5, 0.5), (0.20, 0.32)), ((0.5, 0.5), (0.80, 0.32)), ((0.5, 0.5), (0.5, 0.86))]:
    carve_line(img, a, b, 0.018)                    # the cube's Y edges
for a, b in [((0.50, 0.10), (0.43, 0.34)), ((0.43, 0.34), (0.58, 0.52)),
             ((0.58, 0.52), (0.42, 0.68)), ((0.42, 0.68), (0.52, 0.90))]:
    carve_line(img, a, b, 0.05)                     # the crack
save(img, 'objective_prism.png')

# 5. LIFEFORM KILLED - creature silhouette (body, tail, dorsal fin), eye carved
img, d = canvas()
d.ellipse([(0.45 - 0.28) * S, (0.52 - 0.17) * S, (0.45 + 0.28) * S, (0.52 + 0.17) * S], fill=W)
d.polygon(P((0.66, 0.52), (0.90, 0.34), (0.90, 0.70)), fill=W)   # tail
d.polygon(P((0.32, 0.40), (0.44, 0.16), (0.52, 0.40)), fill=W)   # dorsal fin
carve_circle(img, (0.28, 0.48), 0.035)                            # eye
save(img, 'objective_lifeform.png')

# 6. COMBAT POINTS - crosshair
img, d = canvas()
cx = cy = 0.5
d.ellipse([(cx - 0.30) * S, (cy - 0.30) * S, (cx + 0.30) * S, (cy + 0.30) * S], fill=W)
carve_circle(img, (cx, cy), 0.235)
for dx, dy in [(0, -1), (0, 1), (-1, 0), (1, 0)]:
    a = (cx + dx * 0.17, cy + dy * 0.17)
    b = (cx + dx * 0.38, cy + dy * 0.38)
    dd = ImageDraw.Draw(img)
    dd.line(P(a, b), fill=W, width=int(0.06 * S))
d.ellipse([(cx - 0.055) * S, (cy - 0.055) * S, (cx + 0.055) * S, (cy + 0.055) * S], fill=W)
save(img, 'objective_combat.png')
