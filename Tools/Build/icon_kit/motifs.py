"""The shared pictograms (Docs/Icons/ABILITY_ICON_SKILL_PLAN.md §5): the same thing is the same shape on every hull.

Every motif returns a MASK in icon px (see canvas.py), centred where asked, drawn angular. Add a motif here the
first time two hulls need it; a hull's own silhouette (Garrett 2026-10-24: "fair game where appropriate") lives in
its spec, built from these primitives, and moves here only when a second hull draws it.
"""
import math

from canvas import (W_PRIMARY, W_SECONDARY, band, disc, empty, fill, ngon, rect, ring, rotate, scale,
                    shear, stroke, translate)


def prism(cx, cy, s):
    """A prism corner-on: a diamond (the trail's block, the Thresher's `prism()`, the Starlings' Mass mark)."""
    return fill([(cx, cy - s), (cx + s * 0.8, cy), (cx, cy + s), (cx - s * 0.8, cy)])


def prism_lit(cx, cy, s):
    """The diamond with its upper-left face cut lighter: a sliver of negative space, so it reads as a solid."""
    m = prism(cx, cy, s)
    cut = fill([(cx - s * 0.55, cy - s * 0.1), (cx - s * 0.1, cy - s * 0.65), (cx - s * 0.02, cy - s * 0.5), (cx - s * 0.45, cy - s * 0.0)])
    return m & ~cut


def hull(cx, cy, s, heading=0.0):
    """The generic vessel: a notched chevron, nose at +x. s = half-length."""
    pts = [(cx + s, cy), (cx - s * 0.7, cy - s * 0.62), (cx - s * 0.3, cy), (cx - s * 0.7, cy + s * 0.62)]
    return fill(rotate(pts, heading, cx, cy))


def arrowhead(tip, direction, size, spread=0.62):
    dx, dy = direction
    m = math.hypot(dx, dy)
    dx, dy = dx / m, dy / m
    nx, ny = -dy, dx
    base = (tip[0] - dx * size, tip[1] - dy * size)
    return fill([tip, (base[0] + nx * size * spread, base[1] + ny * size * spread),
                 (base[0] - nx * size * spread, base[1] - ny * size * spread)])


def burst(cx, cy, r, spikes=10, inner=0.55, rot=0.0, jitter=(1.0, 0.82, 1.0, 0.9, 0.95)):
    """A detonation: ONE jagged polygon, spike lengths varied so it is a blast and not a gear."""
    pts = []
    for i in range(spikes):
        a_out = math.radians(rot + i * 360.0 / spikes)
        a_in = math.radians(rot + (i + 0.5) * 360.0 / spikes)
        ro = r * jitter[i % len(jitter)]
        pts.append((cx + ro * math.cos(a_out), cy + ro * math.sin(a_out)))
        pts.append((cx + r * inner * math.cos(a_in), cy + r * inner * math.sin(a_in)))
    return fill(pts)


def spiked_ball(cx, cy, r, spikes=12, spike=0.55, rot=0.0, width=0.42):
    """The Urchin: a core with short wide spikes all round, one polygon."""
    pts = []
    for i in range(spikes):
        a = math.radians(rot + i * 360.0 / spikes)
        half = math.radians(360.0 / spikes * width)
        pts.append((cx + r * 0.92 * math.cos(a - half), cy + r * 0.92 * math.sin(a - half)))
        pts.append((cx + r * (1 + spike) * math.cos(a), cy + r * (1 + spike) * math.sin(a)))
        pts.append((cx + r * 0.92 * math.cos(a + half), cy + r * 0.92 * math.sin(a + half)))
    return fill(pts) | disc(cx, cy, r, 24)


def trail(pts, w=W_PRIMARY, seg=14.0, gap=5.0):
    """A ribbon of prisms along a polyline: the trail as the game lays it, segmented so it reads as blocks."""
    m = empty()
    # Walk the polyline laying stadium-free rectangles (butt ends) with gaps.
    dist = 0.0
    for a, b in zip(pts, pts[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dy)
        ux, uy = dx / ln, dy / ln
        t = 0.0
        while t < ln:
            t0, t1 = t, min(ln, t + seg)
            p0 = (a[0] + ux * t0, a[1] + uy * t0)
            p1 = (a[0] + ux * t1, a[1] + uy * t1)
            m |= stroke([p0, p1], w)
            t += seg + gap
        dist += ln
    return m


def rail(cx, y_near, y_far, w_near, w_far, ties=4, tie_gap=4.0):
    """A track receding into the distance: a tall trapezoid cut by sleepers (the Urchin's projected track)."""
    m = fill([(cx - w_near / 2, y_near), (cx + w_near / 2, y_near), (cx + w_far / 2, y_far), (cx - w_far / 2, y_far)])
    h = y_near - y_far
    for i in range(1, ties + 1):
        t = i / (ties + 1.0)
        t = t ** 1.35                      # sleepers bunch up toward the far end: perspective
        y = y_near - h * t
        g = tie_gap * (1 - 0.5 * t)
        m &= ~rect(cx - w_near, y - g / 2, cx + w_near, y + g / 2)
    return m


def trail_band(y0=96, y1=114, x0=6, x1=122, notches=4):
    """A ribbon of trail seen from the side: one solid band with V notches cut from its top, so it reads as
    blocks at 40 px without splitting into components (the Urchin rides it, the Squirrel skims it)."""
    m = rect(x0, y0, x1, y1)
    w = (x1 - x0) / notches
    for i in range(1, notches):
        x = x0 + i * w
        m &= ~fill([(x - 6, y0), (x + 6, y0), (x, y0 + 9)])
    return m


def speed_wedge(x, y, length, w, lean=0.0):
    """One motion mark: a chamfered bar, not three lines."""
    pts = [(x, y - w / 2), (x + length, y - w / 2), (x + length - w * 0.6, y + w / 2), (x - w * 0.6, y + w / 2)]
    return fill(shear(pts, lean, y))
