"""One-page top-down map of the Swarm cell for Docs/SWARM_CELL_PLAY_GUIDE.md.

    python3 layout.py <layout.json> && python3 cell_map.py <layout.json> <out.png>

Radii and sectors come from layout.py (the author_*.py models); callout positions are hand-placed."""
import json, math, sys
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Wedge, Circle

L = json.load(open(sys.argv[1]))
out = sys.argv[2]
EL = {0: "#e0b020", 1: "#3a8fd8", 2: "#9b59d0", 3: "#2fb59a"}   # Charge, Mass, Space, Time
ELN = {0: "Charge", 1: "Mass", 2: "Space", 3: "Time"}

fig, ax = plt.subplots(figsize=(16, 13), dpi=110)
ax.set_aspect("equal"); ax.axis("off")
ax.set_xlim(-2050, 2050); ax.set_ylim(-1600, 1750)
fig.patch.set_facecolor("white")


def ring(r0, r1, color, alpha, a0=0, a1=360, hatch=None, lw=0):
    ax.add_patch(Wedge((0, 0), r1, a0, a1, width=r1 - r0, facecolor=color, alpha=alpha, hatch=hatch,
                       edgecolor=color if hatch else "none", lw=lw))


CALLOUTS = {}


def label(r, ang, text, color="black", size=9, weight="normal"):
    """A callout outside the membrane with a leader line to (r, ang)."""
    a = math.radians(ang)
    tx, ty = CALLOUTS[text.split("\n")[0]]
    ax.annotate(text, xy=(r * math.cos(a), r * math.sin(a)), xytext=(tx, ty), ha="center", va="center",
                fontsize=size, color=color, weight=weight,
                bbox=dict(boxstyle="round,pad=0.3", fc="white", ec=color, lw=1.0),
                arrowprops=dict(arrowstyle="-|>", color=color, lw=1.1, shrinkA=2, shrinkB=0))

CALLOUTS.update({
    "WHALE swarm (Mass)": (-1650, -250),
    "PUFFERFISH swarm": (-700, 1430),
    "JELLYFISH swarm (Space)": (-1650, 650),
    "STAMPEDE herds": (1650, 420),
    "LEECH puddles": (-1650, 1150),
    "LEVIATHAN school": (-1650, -850),
    "MOBBER roosts 625-685 u (Time, pecks)": (450, -1450),
    "WEARERS founded 400-465 u": (1500, -1050),
    "FORTRESS colony 845-905 u (Mass)": (1600, 900),
    "THIEF NEST 1085-1140 u (Space)": (650, 1430),
    "PACK HUNTERS roam 690-1080 u": (-1650, 200),
    "THREAT GROVE": (1700, -350),
})
# membrane and nucleus
ax.add_patch(Circle((0, 0), L["membrane"], fill=False, lw=2.5, ec="#444"))
ax.add_patch(Circle((0, 0), L["nucleus"], fc="#eeeeee", ec="#777", lw=1.2))
ax.text(0, 0, "NUCLEUS\nr < %d" % L["nucleus"], ha="center", va="center", fontsize=11, weight="bold", color="#555")
ax.text(0, L["membrane"] + 35, "membrane r %d" % L["membrane"], ha="center", fontsize=10, color="#444")

# swarm bands (pufferfish/whale/jellyfish start creatures)
names = {"Inner": "WHALE swarm (Mass)", "Middle": "PUFFERFISH swarm (Charge)", "Outer": "JELLYFISH swarm (Space)"}
for r in L["regions"]:
    lo, hi = r["band"]
    ring(lo, hi, EL[r["start"]], 0.16)
    ax.add_patch(Circle((0, 0), lo, fill=False, lw=0.6, ec="#999", ls="--"))
    ax.add_patch(Circle((0, 0), hi, fill=False, lw=0.6, ec="#999", ls="--"))

label((470 + 620) / 2, 190, "WHALE swarm (Mass)\n470-620 u\n+ LURKERS at Mass flora", EL[1], 9, "bold")
label((690 + 840) / 2, 60, "PUFFERFISH swarm\n(Charge) 690-840 u", EL[0], 9, "bold")
label((910 + 1080) / 2, 150, "JELLYFISH swarm (Space)\n+ LOCUSTS 910-1080 u", EL[2], 9, "bold")

# middle-shell sectors (axis about Y; top-down x = +X, y = +Z)
sectors = [("stampede", 0, "STAMPEDE herds\n(Mass, bulls charge)", EL[1]),
           ("leech", 120, "LEECH puddles\n(Charge, latch + sip)", EL[0]),
           ("leviathan", 240, "LEVIATHAN school\n(Space, manta gulp)", EL[2])]
for key, ang, text, col in sectors:
    ring(690, 840, col, 0.28, ang - 55, ang + 55, hatch="//", lw=0.4)
    label(765, ang, text, col, 8)

# mobber gap, wearer gap, builders
ring(625, 685, EL[3], 0.30)
label(655, 285, "MOBBER roosts 625-685 u (Time, pecks)", EL[3], 8)
ring(400, 465, EL[0], 0.30)
label(432, 315, "WEARERS founded 400-465 u\n(Charge hearts; roam after your trail)", EL[0], 8)
ring(845, 905, EL[1], 0.35)
label(875, 40, "FORTRESS colony 845-905 u (Mass)", EL[1], 8)
ring(1085, 1140, EL[2], 0.30)
label(1112, 70, "THIEF NEST 1085-1140 u (Space)\nperches on an outer Space plant", EL[2], 8)

# pack spans middle + outer shells
ax.add_patch(Circle((0, 0), 1080, fill=False, lw=1.6, ec=EL[3], ls=":"))
ax.add_patch(Circle((0, 0), 690, fill=False, lw=1.6, ec=EL[3], ls=":"))
label(1000, 185, "PACK HUNTERS roam 690-1080 u\n(Time, 6-hunter ring)", EL[3], 8, "bold")

# grove sector (axis (1, 0.3, 0): azimuth 0 deg about Y, tilted up)
g = L["grove"]["defaults"]
ring(g["GroveInnerRadius"], g["GroveOuterRadius"], "#c0392b", 0.45, -g["GroveHalfAngleDegrees"], g["GroveHalfAngleDegrees"])
label(1140, -5, "THREAT GROVE\n1095-1192 u, ±18°\nalong (1, 0.3, 0)\nSNAP TRAPS (Time)\n+ PHYSARUM (Space)", "#c0392b", 8, "bold")

# axes
ax.annotate("", xy=(1900, -1500), xytext=(1740, -1500), arrowprops=dict(arrowstyle="->", lw=1.5))
ax.text(1905, -1500, " +X", va="center", fontsize=10)
ax.annotate("", xy=(1740, -1340), xytext=(1740, -1500), arrowprops=dict(arrowstyle="->", lw=1.5))
ax.text(1740, -1320, "+Z", ha="center", fontsize=10)
ax.text(-2000, 1730, "The Swarm cell - top-down (looking down -Y), radii from the cell centre",
        fontsize=15, weight="bold", va="top")
ax.text(-2000, 1655, "Bands and sectors read from the author_*.py models (showcase_cell_harness/layout.py). "
        "Every creature spawns in the cell's CONTROLLING domain.\nOpposing domain = Tuned burn: 1 petal per element "
        "per hostile contact (pecks and sips weigh 0.25). Populations are for the balance pass to set.",
        fontsize=9, va="top", color="#333")

# legend
from matplotlib.patches import Patch
h = [Patch(fc=EL[k], alpha=0.5, label="%s hearts" % ELN[k]) for k in range(4)]
h.append(Patch(fc="#c0392b", alpha=0.5, label="threat flora (rim grove)"))
h.append(Patch(fc="white", ec="#555", hatch="//", label="middle-shell 110° sectors (substrate species pens)"))
ax.legend(handles=h, loc="lower left", fontsize=9, frameon=True, title="colour = heart element", title_fontsize=9)
fig.savefig(out, bbox_inches="tight")
print("wrote", out)
