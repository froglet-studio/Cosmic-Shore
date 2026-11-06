#!/usr/bin/env python3
"""Rev 66 boards: Froglet Analytics, more ideas (Shombith, 2026-11-06).

Asked, after picking all seven Rev 65 screens to be built: "If you have more ideas provide mockups lets do that too",
and the console should have "switches in graphs which helps all these departments" (business, marketing, analysis):
every chart carries its own switches for its form, its measure and its split, never one switch for the whole page.

Six new screens of the same console (Rev 65's kit, imported from r65_analytics_tool.py so the boards read as one
product: the Nocturne theme, the rail, the sea lines, panels, tiles, donut, ranked bars), plus a notes board:
Chart switches close up, Live now, Alerts, Funnel builder, Release compare, Player compare. Every number is invented
sample data, consistent with Rev 65's set (the same players, the same 45 / 23 counts).

    python3 Docs/MenuRedesignB/rounds/r66_analytics_ideas.py <root>      # writes <root>/project/R66*.dc.html
"""
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import r65_analytics_tool as k  # noqa: E402  (the Rev 65 kit; reads sys.argv[1] as its root, writes nothing on import)

ROOT = Path(sys.argv[1]) / "project"
REV = 66

GROUND, DEEP, SURFACE, LINE = k.GROUND, k.DEEP, k.SURFACE, k.LINE
TEXT, SOFT, OFF = k.TEXT, k.SOFT, k.OFF
CTA, DANGER = k.CTA, k.DANGER
JADE, RUBY, GOLD, BLUE = k.JADE, k.RUBY, k.GOLD, k.BLUE
SERIES = [JADE, RUBY, GOLD, BLUE]          # a split's parts wear these in this order, always
PLAY = k.PLAY
panel, tile, legend, donut, ranked_bars, segs = k.panel, k.tile, k.legend, k.donut, k.ranked_bars, k.segs

# ---------------------------------------------------------------- the switch row's own styles, added to Rev 65's helmet
EXTRA = f"""
.mseg{{display:flex;background:{DEEP};box-shadow:inset 0 0 0 1px {LINE}}}
.mseg button{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:11px;letter-spacing:.08em;text-transform:uppercase;border:0;background:transparent;color:{SOFT};height:28px;padding:0 8px;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}}
.mseg button.on{{color:{TEXT};box-shadow:inset 0 -2px 0 {JADE}}}
.dd{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:11px;letter-spacing:.08em;text-transform:uppercase;height:28px;padding:0 10px;border:0;background:{DEEP};color:{TEXT};box-shadow:inset 0 0 0 1px {LINE};display:inline-flex;align-items:center;gap:6px;white-space:nowrap}}
.dd i{{font-style:normal;color:{SOFT}}}
.ib{{width:28px;height:28px;border:0;background:{DEEP};box-shadow:inset 0 0 0 1px {LINE};color:{SOFT};display:inline-flex;align-items:center;justify-content:center;padding:0}}
.tick{{font-family:'Aldrich',sans-serif;font-size:13px;color:{TEXT};padding:6px 10px;border-bottom:1px solid rgba(112,64,95,.5);white-space:nowrap;font-variant-numeric:tabular-nums}}
"""
k.HELMET = k.HELMET.replace("</style>", EXTRA + "</style>")

k.ICONS.update({
    "live": '<circle cx="10" cy="10" r="2.5"/><path d="M5.5 5.5a6.4 6.4 0 0 0 0 9M14.5 5.5a6.4 6.4 0 0 1 0 9"/>',
    "alerts": '<path d="M10 3l8 14H2z"/><path d="M10 8v4M10 14v1"/>',
    "funnels": '<path d="M2 4h16l-6 7v5l-4 2v-7z"/>',
    "releases": '<path d="M3 4h7l7 7-6 6-8-8z"/><circle cx="7" cy="8" r="1.2"/>',
})
NAV = [("overview", "Overview"), ("live", "Live now"), ("players", "Players"), ("cohorts", "Cohorts"), ("eras", "Eras"),
       ("playtypes", "Play types"), ("retention", "Retention"), ("matches", "Matches"), ("funnels", "Funnels"),
       ("ftue", "FTUE"), ("economy", "Economy"), ("social", "Social"), ("releases", "Releases"), ("alerts", "Alerts"),
       ("health", "Data health"), ("ask", "Ask Claude")]
ADMIN = k.ADMIN
FORM_ICON = {
    "Bars": '<path d="M2 11V6M5.5 11V2M9 11V4"/>',
    "Line": '<path d="M1 10l3.5-4 3 2.5L11 3"/>',
    "Pie": '<circle cx="6" cy="6.5" r="4.5"/><path d="M6 2v4.5h4.5"/>',
    "Table": '<path d="M1.5 2.5h9v8h-9zM1.5 5.5h9M1.5 8h9M5 2.5v8"/>',
    "Map": '<path d="M1.5 2.5h3v3h-3zM5.5 2.5h3v3h-3zM5.5 6.5h3v3h-3zM9 6.5h2v3H9z"/>',
}


def ic(path, size=12):
    return (f'<svg viewBox="0 0 12 12" width="{size}" height="{size}" aria-hidden="true" style="fill:none;stroke:currentColor;'
            f'stroke-width:1.4;flex:none">{path}</svg>')


PIN = '<path d="M4 1.5h4l-.6 3.5L9.5 7h-7l2.1-2zM6 7v4"/>'
COPY = '<path d="M3.5 3.5h6v7h-6zM2 8.5V1.5h5.5"/>'
RESET = '<path d="M2.5 6a3.5 3.5 0 1 0 1-2.5"/><path d="M2 1.5v2.5h2.5"/>'


def switches(form, measure, split, forms=("Bars", "Line", "Pie", "Table"), split_label="Split", tools=False, labels=False):
    """The row every chart carries: form, measure, split (and, drawn large, pin / copy / back). The settings are the
    chart's own. In a panel the forms are icons and only the chosen one shows its name, so the row fits a third of the
    page; `labels` names them all (the close-up)."""
    seg = '<div class="mseg sls">' + "".join(
        f'<button type="button" class="{"on" if f == form else ""}" aria-label="{f}">{ic(FORM_ICON[f])}'
        f'{f if (labels or f == form) else ""}</button>' for f in forms) + "</div>"
    out = seg
    if measure:
        out += f'<button type="button" class="dd sls"><i>Measure</i>{measure} &#9662;</button>'
    if split:
        out += f'<button type="button" class="dd sls"><i>{split_label}</i>{split} &#9662;</button>'
    if tools:
        out += tool_icons()
    return f'<div style="display:flex;gap:6px;align-items:center;flex-wrap:wrap">{out}</div>'


def tool_icons():
    return "".join(f'<button type="button" class="ib sls" aria-label="{n}">{ic(p)}</button>'
                   for n, p in (("Pin to a dashboard", PIN), ("Copy as CSV or image", COPY), ("Back to the default", RESET)))


def cpanel(title, sw, body, note="", style="", tools=True):
    """A chart panel: the title (and pin / copy / back) on top, the chart's own switches under it, then the chart."""
    right = (f'<span style="display:flex;gap:4px">{tool_icons()}</span>' if tools else "")
    return (f'<div class="panel sl" style="padding:14px 18px 16px;display:flex;flex-direction:column;gap:10px;min-width:0;{style}">'
            f'<div style="display:flex;justify-content:space-between;align-items:center;gap:12px;min-height:28px">'
            f'<div style="display:flex;align-items:baseline;gap:12px;min-width:0"><span class="lab" style="color:{TEXT};white-space:nowrap">{title}</span>'
            f'<span class="small" style="font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{note}</span></div>{right}</div>'
            f'{sw}{body}</div>')


# ---------------------------------------------------------------- the frame (Rev 65's, with the new pages in the rail)

def rail(active):
    nav = "".join(f'<a class="nav{" on" if key == active else ""}" href="#{key}" style="height:38px">{k.icon(key)}{label}'
                  + (f'<span style="margin-left:auto;font-size:12px;color:{GROUND};background:{DANGER};padding:1px 7px">4</span>'
                     if key == "alerts" else "")
                  + (f'<span style="margin-left:auto;font-size:13px;color:{TEXT}" class="num">37</span>' if key == "live" else "")
                  + "</a>" for key, label in NAV)
    adm = "".join(f'<a class="nav{" on" if key == active else ""}" href="#{key}" style="height:38px">{k.icon(key)}{label}</a>'
                  for key, label in ADMIN)
    theme = (f'<div style="display:flex;align-items:center;gap:10px;height:32px;padding:0 8px">'
             f'<span style="display:flex;gap:2px">'
             + "".join(f'<i style="display:block;width:12px;height:18px;background:{f};box-shadow:inset 0 3px 0 {r}"></i>'
                       for r, f in k.SWATCH["nocturne"])
             + f'</span><span style="font-size:14px;color:{TEXT}">Nocturne</span><span class="small" style="margin-left:auto">&#9662;</span></div>')
    return (f'<div style="position:relative;background:rgba(5,0,3,.82);box-shadow:inset -1px 0 0 {LINE};padding:26px 14px 18px;'
            f'display:flex;flex-direction:column;gap:2px">'
            f'<div style="padding:0 16px 16px;display:flex;flex-direction:column;gap:4px"><span class="lab">Froglet</span>'
            f'<span class="h3">Analytics</span><span style="display:flex;gap:3px;margin-top:6px">'
            + "".join(f'<i style="display:block;width:26px;height:3px;background:{c}"></i>' for c in SERIES)
            + f'</span></div>{nav}'
            f'<span class="lab" style="padding:12px 16px 4px;font-size:11px">Admin</span>{adm}'
            f'<div style="margin-top:auto;padding:10px 8px 0;display:flex;flex-direction:column;gap:2px">'
            f'<span class="lab" style="padding:0 8px 4px;font-size:11px">Theme</span>{theme}</div>'
            f'<div style="padding:8px 16px 0"><div class="small" style="color:{TEXT}">shombith · admin</div></div></div>')


def screen(active, body):
    return (f'<div style="width:1920px;height:1080px;box-sizing:border-box;position:relative;overflow:hidden;'
            f'background:{GROUND};color:{TEXT};font-family:\'Aldrich\',sans-serif;display:grid;'
            f'grid-template-columns:260px minmax(0,1fr)">{rail(active)}'
            f'<div style="position:relative;overflow:hidden">{k.sea()}'
            f'<div style="position:relative;padding:26px 36px 26px;display:flex;flex-direction:column;gap:16px;height:100%;'
            f'box-sizing:border-box">{body}</div></div></div>')


def header(eyebrow, title, right):
    return (f'<div style="display:flex;align-items:flex-end;justify-content:space-between;gap:20px">'
            f'<div style="display:flex;flex-direction:column;gap:6px;min-width:0"><span class="lab">{eyebrow}</span>'
            f'<h1 class="h1" style="white-space:nowrap">{title}</h1></div>'
            f'<div style="display:flex;gap:8px;align-items:center;flex:none">{right}</div></div>')


def filters(play="All", rng="30d", era="All time"):
    return (f'<button type="button" class="chip sls" style="height:38px;white-space:nowrap">Era <i>{era}</i></button>'
            + segs(["Release", "Editor", "Port", "Dev"], "Release")
            + (segs(["7d", "30d", "90d", "365d"], rng) if rng else "")
            + segs(["All", "Solo vs AI", "Solo", "Multiplayer"], play))


# ---------------------------------------------------------------- charts (SVG; marks per the dataviz spec)

def label_ys(items, gap=16):
    """Spread end labels so none overlaps: items are (y, ...) sorted top to bottom."""
    items = sorted(items, key=lambda t: t[0])
    out, last = [], -1e9
    for it in items:
        y = max(it[0], last + gap)
        out.append((y,) + tuple(it[1:]))
        last = y
    return out


def line_chart(series, w, h, xlabels, ymax, ymin=0, yticks=(), fmt=lambda v: f"{v:g}", pad_l=44, pad_r=120, pad_t=10,
               pad_b=24, threshold=None, end=True, dots=False):
    """series: (name, colour, values). Values evenly across x; None leaves a gap. Direct labels at the line ends."""
    pw, ph = w - pad_l - pad_r, h - pad_t - pad_b
    n = max(len(s[2]) for s in series)
    X = lambda i: pad_l + pw * i / max(n - 1, 1)
    Y = lambda v: pad_t + ph - ph * (v - ymin) / (ymax - ymin)
    out = []
    for t in yticks:
        out.append(f'<rect x="{pad_l}" y="{Y(t):.1f}" width="{pw}" height="1" fill="{LINE}" opacity=".45"></rect>'
                   f'<text x="{pad_l - 8}" y="{Y(t) + 4:.1f}" text-anchor="end" fill="{SOFT}" style="font:11px Aldrich">{fmt(t)}</text>')
    out.append(f'<rect x="{pad_l}" y="{pad_t + ph}" width="{pw}" height="1" fill="{LINE}"></rect>')
    for i, lab in xlabels:
        anchor = "start" if i == 0 else ("end" if i == n - 1 else "middle")
        out.append(f'<text x="{X(i):.1f}" y="{h - 6}" text-anchor="{anchor}" fill="{SOFT}" style="font:11px Aldrich">{lab}</text>')
    if threshold:
        tv, tl = threshold
        out.append(f'<line x1="{pad_l}" x2="{pad_l + pw}" y1="{Y(tv):.1f}" y2="{Y(tv):.1f}" stroke="{TEXT}" stroke-width="1" '
                   f'stroke-dasharray="5 4" opacity=".7"></line>'
                   f'<text x="{pad_l + 6}" y="{Y(tv) + 14:.1f}" fill="{TEXT}" style="font:11px Aldrich" opacity=".85">{tl}</text>')
    ends = []
    for name, colour, vals in series:
        segs_, cur = [], []
        for i, v in enumerate(vals):
            if v is None:
                if cur:
                    segs_.append(cur)
                cur = []
            else:
                cur.append(f"{X(i):.1f},{Y(v):.1f}")
        if cur:
            segs_.append(cur)
        for sgm in segs_:
            out.append(f'<polyline points="{" ".join(sgm)}" fill="none" stroke="{colour}" stroke-width="2" '
                       f'stroke-linejoin="round"></polyline>')
        li = max(i for i, v in enumerate(vals) if v is not None)
        if dots:
            out.append(f'<rect x="{X(li) - 3.5:.1f}" y="{Y(vals[li]) - 3.5:.1f}" width="7" height="7" fill="{colour}"></rect>')
        ends.append((Y(vals[li]) + 4, X(li), name, colour, vals[li]))
    if end:
        for y, x, name, colour, v in label_ys(ends):
            out.append(f'<text x="{x + 10:.1f}" y="{y:.1f}" fill="{TEXT}" style="font:12px Aldrich">'
                       f'<tspan fill="{colour}">&#9632;</tspan> {name} {fmt(v)}</text>')
    return f'<svg width="{w}" height="{h}" role="img" aria-label="line chart">{"".join(out)}</svg>'


def col_chart(cats, series, w, h, ymax, fmt=lambda v: f"{v:g}", pad_l=40, pad_b=38, pad_t=18, yticks=(), labels=True,
              outline=(), between=()):
    """Grouped columns: cats along x, one column per series inside each group, value on top."""
    pw, ph = w - pad_l - 6, h - pad_t - pad_b
    gw = pw / len(cats)
    ns = len(series)
    bw = min(56, (gw - 18) / ns)
    out = []
    Y = lambda v: pad_t + ph - ph * v / ymax
    for t in yticks:
        out.append(f'<rect x="{pad_l}" y="{Y(t):.1f}" width="{pw}" height="1" fill="{LINE}" opacity=".45"></rect>'
                   f'<text x="{pad_l - 8}" y="{Y(t) + 4:.1f}" text-anchor="end" fill="{SOFT}" style="font:11px Aldrich">{fmt(t)}</text>')
    out.append(f'<rect x="{pad_l}" y="{pad_t + ph}" width="{pw}" height="1" fill="{LINE}"></rect>')
    for ci, cat in enumerate(cats):
        gx = pad_l + ci * gw
        x0 = gx + (gw - ns * bw - (ns - 1) * 2) / 2
        for si, (name, colour, vals) in enumerate(series):
            v = vals[ci]
            x = x0 + si * (bw + 2)
            hgt = ph * v / ymax
            if name in outline:
                out.append(f'<rect x="{x + .75:.1f}" y="{Y(v) + .75:.1f}" width="{bw - 1.5:.1f}" height="{max(hgt - 1.5, 0):.1f}" fill="none" '
                           f'stroke="{colour}" stroke-width="1.5"></rect>')
            else:
                out.append(f'<rect x="{x:.1f}" y="{Y(v):.1f}" width="{bw:.1f}" height="{hgt:.1f}" fill="{colour}"></rect>')
            if labels:
                out.append(f'<text x="{x + bw / 2:.1f}" y="{Y(v) - 5:.1f}" text-anchor="middle" fill="{TEXT}" '
                           f'style="font:11px Aldrich">{fmt(v)}</text>')
        lines = cat if isinstance(cat, tuple) else (cat,)
        for li, ln in enumerate(lines):
            out.append(f'<text x="{gx + gw / 2:.1f}" y="{pad_t + ph + 16 + li * 14}" text-anchor="middle" fill="{TEXT if li == 0 else SOFT}" '
                       f'style="font:12px Aldrich">{ln}</text>')
    for i, (txt, colour) in enumerate(between):           # a note between two groups, on the label line (a funnel's drop)
        out.append(f'<text x="{pad_l + (i + 1) * gw:.1f}" y="{pad_t + ph + 16}" text-anchor="middle" fill="{colour}" '
                   f'style="font:12px Aldrich">{txt}</text>')
    return f'<svg width="{w}" height="{h}" role="img" aria-label="column chart">{"".join(out)}</svg>'


def pair_bars(rows, a, b, width, fmt=lambda v: f"{v}%", top=100, name_w=110):
    """Two bars per row (A over B), each with its value."""
    out = []
    bw = width - name_w - 60
    for name, va, vb in rows:
        out.append(f'<div style="display:grid;grid-template-columns:{name_w}px {bw}px 50px;align-items:center;column-gap:10px;row-gap:3px">'
                   f'<span class="small" style="color:{TEXT};grid-row:span 2">{name}</span>'
                   f'<span style="display:block;height:9px;width:{bw * va / top:.0f}px;background:{a}"></span>'
                   f'<span class="small num" style="color:{TEXT};text-align:right;font-size:12px">{fmt(va)}</span>'
                   f'<span style="display:block;height:9px;width:{bw * vb / top:.0f}px;background:{b}"></span>'
                   f'<span class="small num" style="color:{TEXT};text-align:right;font-size:12px">{fmt(vb)}</span></div>')
    return '<div style="display:flex;flex-direction:column;gap:9px">' + "".join(out) + "</div>"


def wave(seed, n, base, amp, noise, lo=0, hi=None):
    rng = random.Random(seed)
    vals = [base + amp * math.sin(i / 4.0 + seed) + rng.uniform(-noise, noise) for i in range(n)]
    return [max(lo, min(hi, v) if hi is not None else v) for v in vals]


# ---------------------------------------------------------------- 1. chart switches, close up

def switches_board():
    W = 548
    biz = cpanel("Players", switches("Pie", "Players", "Platform"),
                 '<div style="display:flex;gap:26px;align-items:center">'
                 + donut([("WindowsPlayer", 21, JADE), ("Android", 14, RUBY), ("IPhonePlayer", 8, GOLD), ("WebGL", 2, BLUE)],
                         230, label="45", sub="players")
                 + '<div style="display:flex;flex-direction:column;gap:12px">'
                 + "".join(f'<div style="display:flex;flex-direction:column"><span class="small" style="display:flex;gap:8px;align-items:center;color:{TEXT}">'
                           f'<i style="display:block;width:12px;height:12px;background:{c}"></i>{n}</span>'
                           f'<span class="num" style="font-size:20px;padding-left:20px">{v} <span class="small">· {round(100 * v / 45)}%</span></span></div>'
                           for n, v, c in (("WindowsPlayer", 21, JADE), ("Android", 14, RUBY), ("IPhonePlayer", 8, GOLD), ("WebGL", 2, BLUE)))
                 + "</div></div>", "last 30 days", "flex:1")
    mkt = cpanel("New players", switches("Bars", "New players", "Country"),
                 ranked_bars([("India", 9), ("United States", 7), ("United Kingdom", 3), ("Brazil", 3), ("Germany", 2),
                              ("Philippines", 2), ("Other (4)", 4)], W - 40, BLUE).replace("height:24px", "height:32px")
                 + '<span class="small" style="font-size:13px">Unsplit data is Blue; a split wears Jade, Ruby, Gold, Blue in that order.</span>',
                 "last 30 days", "flex:1")
    days = 30
    fin = [("Solo vs AI", JADE, wave(3, days, 71, 4, 3)), ("Solo", GOLD, wave(5, days, 58, 5, 4)),
           ("Multiplayer", RUBY, wave(8, days, 47, 6, 5))]
    ana = cpanel("Finished %", switches("Line", "Finished %", "Play type"),
                 line_chart(fin, W - 36, 300, [(0, "Oct 8"), (14, "Oct 22"), (29, "Nov 6")], 100, 0, (0, 50, 100),
                            lambda v: f"{v:.0f}%", pad_r=150),
                 "last 30 days · a day a point", "flex:1")

    def dept(name, ask, chart, who):
        return (f'<div style="display:flex;flex-direction:column;gap:10px;min-width:0">'
                f'<div style="display:flex;align-items:baseline;gap:14px"><span class="h2">{name}</span><span class="small">{who}</span></div>'
                f'<p class="body" style="color:{SOFT};font-size:14px;min-height:40px">{ask}</p>{chart}</div>')
    top = ('<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:24px">'
           + dept("Business", "&quot;Where do our players play, and which port pays for itself?&quot; Pie, Players, split by Platform.", biz, "sets it to")
           + dept("Marketing", "&quot;Where are new players coming from this month?&quot; Bars, New players, split by Country.", mkt, "sets it to")
           + dept("Analysis", "&quot;Do people finish what they start, by how they play?&quot; Line, Finished %, split by Play type.", ana, "sets it to")
           + "</div>")

    # the anatomy: the switch row drawn large, its four parts, and the two menus open
    big = (f'<div style="height:52px"><div style="transform:scale(1.6);transform-origin:left top;width:700px">'
           + switches("Line", "Finished %", "Play type", tools=True, labels=True) + "</div></div>")

    def callout(num, title, body, menu=None, on=None):
        m = ""
        if menu:
            m = (f'<div style="display:flex;flex-direction:column;background:{DEEP};box-shadow:inset 0 0 0 1px {LINE};padding:4px 0;margin-top:4px">'
                 + "".join(f'<span style="font-size:12px;padding:2px 12px;color:{TEXT if o == on else SOFT};'
                           f'{"box-shadow:inset 2px 0 0 " + JADE if o == on else ""}">{o}</span>' for o in menu) + "</div>")
        return (f'<div style="display:flex;flex-direction:column;gap:6px;min-width:0">'
                f'<div style="display:flex;gap:10px;align-items:baseline"><span class="num" style="font-size:22px;color:{JADE}">{num}</span>'
                f'<span class="lab" style="color:{TEXT}">{title}</span></div><p class="small" style="font-size:13px">{body}</p>{m}</div>')
    anatomy = panel("The switch row, close up", big
                    + '<div style="display:grid;grid-template-columns:1.1fr 1fr 1fr 1fr;gap:22px">'
                    + callout(1, "Form", "Bars, Line, Pie, Table; the chosen one shows its name, the others are icons. Pie is offered "
                              "only when the split has 6 parts or fewer, Line only over time: a form the data cannot take is left out, never greyed.")
                    + callout(2, "Measure", "What is counted. Only what this chart's data holds.",
                              ["Matches", "Players", "New players", "Finished %", "Rematch %", "Minutes at the controls"], "Finished %")
                    + callout(3, "Split by", "At most 8 parts plus Other.",
                              ["None", "Play type", "Era", "Platform", "Country", "Mode", "Vessel", "Build", "Intensity"], "Play type")
                    + callout(4, "Pin, copy, back", "Pin this view to a dashboard; copy it as CSV or an image; back to the page's default. "
                              "A chart keeps its switches for each person, so Marketing's chart stays Marketing's.")
                    + "</div>", "the same row on every chart in the console", "flex:1")
    rows = [("Week of Oct 13", "73%", "60%", "49%"), ("Week of Oct 20", "69%", "55%", "45%"),
            ("Week of Oct 27", "72%", "59%", "50%"), ("Week of Nov 3", "74%", "61%", "48%")]
    table = cpanel("Finished %", switches("Table", "Finished %", "Play type", tools=False),
                   '<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Week</th>'
                   + "".join(f'<th class="th"><span style="display:inline-flex;gap:6px;align-items:center"><i style="display:block;width:10px;height:10px;background:{c}"></i>{n}</span></th>'
                             for _, n, c in PLAY) + "</tr></thead><tbody>"
                   + "".join(f'<tr><td class="td">{a}</td><td class="td">{b}</td><td class="td">{c}</td><td class="td">{d}</td></tr>' for a, b, c, d in rows)
                   + "</tbody></table>"
                   + '<span class="small" style="font-size:13px">Analysis\'s chart as a Table: the fourth form, for copying into a sheet.</span>',
                   "Analysis, weekly", "width:560px;flex:none")
    body = (f'<div style="width:1920px;height:1080px;box-sizing:border-box;position:relative;overflow:hidden;background:{GROUND};'
            f'color:{TEXT};font-family:\'Aldrich\',sans-serif">{k.sea(1920, 1080)}'
            f'<div style="position:relative;padding:40px 48px;display:flex;flex-direction:column;gap:22px;height:100%;box-sizing:border-box">'
            f'<div style="display:flex;justify-content:space-between;align-items:flex-end"><div style="display:flex;flex-direction:column;gap:6px">'
            f'<span class="lab">Rev 66 · compare · every chart has its own switches</span>'
            f'<h1 class="h1">One chart, three departments</h1></div>'
            f'<p class="small" style="max-width:640px;text-align:right">The same Overview chart as Business, Marketing and Analysis set it. '
            f'Switches belong to the chart, not the page: the rest of the page does not move.</p></div>'
            + top + f'<div style="display:flex;gap:24px;flex:1;min-height:0">{anatomy}{table}</div></div></div>')
    return body


# ---------------------------------------------------------------- 2. live now

def live_board():
    tiles = ('<div style="display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:12px">'
             + tile("Online now", "37", "peak today 52 at 19:40") + tile("In a match", "21", "57% of those online")
             + tile("In the menus", "12", "Arcade 7 · Ark 3 · Profile 2") + tile("In the FTUE", "4", "9 new players today")
             + tile("Parties open", "3", "7 players waiting") + "</div>")
    n = 60
    lines = [("Solo vs AI", JADE, [round(v) for v in wave(2, n, 8, 2.5, .6, 1)]),
             ("Solo", GOLD, [round(v) for v in wave(4, n, 5, 1.5, .5, 0)]),
             ("Multiplayer", RUBY, [round(v) for v in wave(9, n, 4, 2.5, .6, 0)])]
    for (_, _, vals), target in zip(lines, (8, 6, 7)):           # land on the tiles' numbers: 8 + 6 + 7 = 21 in a match
        start = vals[-6]
        for j in range(1, 6):
            vals[-6 + j] = round(start + (target - start) * j / 5)
    hour = cpanel("In a match, the last 60 minutes", switches("Line", "In a match", "Play type"),
                  line_chart(lines, 860, 214, [(0, "13:33"), (30, "14:03"), (59, "now")], 14, 0, (0, 5, 10),
                             lambda v: f"{v:.0f}", pad_r=150, dots=True),
                  "a point a minute · players", "flex:1")
    modes = [("Tandava", 3, 1, 1), ("Joust", 0, 0, 4), ("Scurry", 2, 1, 0), ("Switchback", 1, 2, 0), ("Redline", 1, 1, 0),
             ("Skein", 0, 1, 1), ("Other (2)", 1, 0, 1)]
    sw = 340
    rows = "".join(
        f'<div style="display:grid;grid-template-columns:110px {sw}px 30px;gap:10px;align-items:center;height:24px">'
        f'<span class="small" style="color:{TEXT}">{m}</span><span style="display:flex;gap:2px">'
        + "".join(f'<i style="display:block;height:12px;width:{sw * v / 6 - 2:.0f}px;background:{c}"></i>' for v, c in ((a, JADE), (b, GOLD), (cc, RUBY)) if v)
        + f'</span><span class="small num" style="color:{TEXT};text-align:right">{a + b + cc}</span></div>' for m, a, b, cc in modes)
    playing = cpanel("Being played now", switches("Bars", "Players", "Play type", forms=("Bars", "Pie", "Table"), tools=False),
                     f'<div style="display:flex;flex-direction:column;gap:4px">{rows}</div>'
                     + legend([("Solo vs AI", JADE), ("Solo", GOLD), ("Multiplayer", RUBY)]),
                     "21 players in 15 matches", "width:620px;flex:none")
    # the map-like country grid: a tile per country, placed roughly where it is
    online = {"IN": 11, "US": 8, "GB": 4, "BR": 3, "DE": 2, "PH": 2, "CA": 1, "FR": 1, "ES": 1, "NG": 1, "ID": 1, "JP": 1, "AU": 1}
    pos = {"CA": (2, 1), "US": (2, 2), "MX": (2, 3), "CO": (3, 4), "BR": (4, 5), "AR": (4, 6), "IE": (7, 1), "GB": (8, 1),
           "SE": (9, 0), "DE": (9, 1), "PL": (10, 1), "FR": (8, 2), "ES": (7, 3), "IT": (9, 2), "TR": (11, 3), "EG": (10, 4),
           "NG": (8, 5), "KE": (11, 5), "ZA": (10, 7), "SA": (11, 4), "AE": (12, 4), "PK": (13, 3), "IN": (14, 4), "BD": (15, 3),
           "CN": (16, 2), "KR": (17, 2), "JP": (18, 2), "VN": (16, 4), "PH": (17, 4), "ID": (16, 6), "AU": (17, 7)}
    t, g = 36, 4
    cells = []
    for code, (cx, cy) in pos.items():
        v = online.get(code, 0)
        x, y = cx * (t + g) - 2 * (t + g), cy * (t + g)
        if v:
            op = .25 + .75 * v / 11
            dark = op > .6
            cells.append(f'<rect x="{x}" y="{y}" width="{t}" height="{t}" fill="{BLUE}" opacity="{op:.2f}"></rect>'
                         f'<text x="{x + 4}" y="{y + 13}" fill="{GROUND if dark else TEXT}" style="font:10px Aldrich">{code}</text>'
                         f'<text x="{x + t - 4}" y="{y + t - 5}" text-anchor="end" fill="{GROUND if dark else TEXT}" style="font:14px Aldrich">{v}</text>')
        else:
            cells.append(f'<rect x="{x + .5}" y="{y + .5}" width="{t - 1}" height="{t - 1}" fill="none" stroke="{LINE}"></rect>'
                         f'<text x="{x + 4}" y="{y + 13}" fill="{OFF}" style="font:10px Aldrich">{code}</text>')
    gw_, gh_ = 17 * (t + g), 8 * (t + g)
    world = cpanel("Where they are", switches("Map", "Online", None, forms=("Map", "Bars", "Table"), tools=False),
                   f'<svg width="{gw_}" height="{gh_}" role="img" aria-label="Players online by country, a tile per country">{"".join(cells)}</svg>'
                   + '<div style="display:flex;gap:14px;align-items:center"><span class="small">fewer</span>'
                   + "".join(f'<i style="display:block;width:22px;height:12px;background:{BLUE};opacity:{o}"></i>' for o in (.3, .5, .7, .9, 1))
                   + '<span class="small">more</span><span class="small" style="margin-left:auto;font-size:13px">Outline: no one online. Country from the IP at ingest; the IP is not kept.</span></div>',
                   "37 online in 13 countries · one hue, brighter is more", "width:860px;flex:none")
    feed = [("14:32:07", "game_started", "Dragon", "IN", "Tandava · I2 · solo vs AI", JADE),
            ("14:32:05", "game_completed", "Kes", "US", "Scurry · finished · won", GOLD),
            ("14:31:58", "party_joined", "Halcyon", "GB", "a party of 3", RUBY),
            ("14:31:51", "ftue_step_completed", "nova_k", "BR", "First boost · 41 s", None),
            ("14:31:44", "game_first_launched", "new player", "PH", "Android · 0.9.14", None),
            ("14:31:40", "crystals_earned", "Juno", "IN", "+300 · game_placement", None),
            ("14:31:33", "game_started", "Mirelle", "DE", "Joust · I3 · 4 humans", RUBY),
            ("14:31:21", "session_ended", "Bram", "US", "quit_to_menu · 22 min", None),
            ("14:31:15", "menu_ready", "Wren", "IN", "6.2 s from launch", None),
            ("14:31:02", "game_completed", "Tamsin", "IN", "Redline · not finished", GOLD),
            ("14:30:55", "share_triggered", "Saffron", "US", "Switchback", None),
            ("14:30:41", "game_started", "Rook", "AU", "Skein · I1 · solo", GOLD)]
    ticker = panel("As it happens", '<table style="border-collapse:collapse;width:100%"><tbody>'
                   + "".join(f'<tr><td class="tick" style="color:{SOFT}">{tm}</td>'
                             f'<td class="tick"><span style="display:inline-flex;gap:7px;align-items:center">'
                             f'<i style="display:block;width:9px;height:9px;{"background:" + c if c else "box-shadow:inset 0 0 0 1px " + LINE}"></i>{ev}</span></td>'
                             f'<td class="tick">{who} <span style="color:{SOFT}">{cc}</span></td><td class="tick" style="color:{SOFT}">{d}</td></tr>'
                             for tm, ev, who, cc, d, c in feed)
                   + "</tbody></table>"
                   + '<span class="small" style="font-size:13px">Names for profile keys; a reader sees the country only. Pause holds the list; it never scrolls by itself.</span>',
                   "newest first · the square: the match's play type", "flex:1")
    right = (f'<span class="small">Updated 14:32:08 · every 10 s</span><button type="button" class="btn ghost sls">Pause</button>'
             + segs(["Release", "Editor", "Port", "Dev"], "Release") + segs(["All", "Solo vs AI", "Solo", "Multiplayer"], "All"))
    body = (header("Live · release builds · the last 5 minutes count as online", "Live now", right) + tiles
            + f'<div style="display:flex;gap:12px">{hour}{playing}</div>'
            + f'<div style="display:flex;gap:12px;flex:1;min-height:0">{world}{ticker}</div>')
    return screen("live", body)


# ---------------------------------------------------------------- 3. alerts

def alerts_board():
    def card(kind, colour, title, sub, chart, why, actions):
        return (f'<div class="panel sl" style="padding:14px 18px;display:flex;flex-direction:column;gap:9px;min-width:0;'
                f'box-shadow:inset 0 0 0 1px #7A5068,inset 0 2px 0 {colour}">'
                f'<div style="display:flex;gap:10px;align-items:center"><span class="tag" style="color:{colour};box-shadow:inset 0 0 0 1px {colour}">{kind}</span>'
                f'<span class="small" style="font-size:12px;margin-left:auto">{sub}</span></div>'
                f'<h3 class="h3">{title}</h3>{chart}'
                f'<span class="lab" style="font-size:11px">Why Claude thinks so</span>'
                + "".join(f'<p class="small" style="font-size:13px;color:{TEXT};padding-left:12px;box-shadow:inset 1px 0 0 {LINE}">{w}</p>' for w in why)
                + '<div style="display:flex;gap:8px;margin-top:auto">'
                + "".join(f'<button type="button" class="btn {"def" if i == 0 else "ghost"} sls" style="height:34px;font-size:12px;padding:0 14px">{a}</button>'
                          for i, a in enumerate(actions)) + "</div></div>")
    cw = 470
    d1 = [41, 42, 40, 43, 41, 39, 42, 40, 41, 43, 40, 41, 42, 40, 30, 28, 31, 27, 29, 28, 29]
    c1 = card("Drop", DANGER, "D1 return fell to 29% (from 41%)", "new players from Oct 30 · raised Nov 2",
              line_chart([("D1 return", BLUE, d1)], cw, 150, [(0, "Oct 16"), (14, "Oct 30"), (20, "Nov 5")], 50, 20, (20, 35, 50),
                         lambda v: f"{v:.0f}%", pad_r=110, threshold=(35, "alert below 35%"), dots=True),
              ["It starts with players whose first day is Oct 30, the day 0.9.14 shipped; players still on 0.9.13 who joined the same days return at 40%.",
               "Android only: Windows holds at 43%, Android fell from 38% to 17%.",
               "On 0.9.14 Android the FTUE step First boost takes 2 min 10 s, not 41 s."],
              ["Open the funnel", "Acknowledge", "Snooze a day"])
    rej = [28, 31, 22, 35, 40, 26, 30, 33, 24, 29, 37, 31, 1240, 610]
    c2 = card("Spike", DANGER, "Rejected events: 1,240 on Nov 5", "usual 20 to 40 a day · raised Nov 6 03:10",
              col_chart(["Oct 23"] + [""] * 6 + ["Oct 30"] + [""] * 5 + ["Nov 5"], [("rejected", BLUE, rej)], cw, 150, 1300,
                        lambda v: f"{v:,}", labels=False, pad_b=22, pad_t=6,
                        yticks=(0, 600, 1200)),
              ["1,198 of them are game_completed rejected for a missing match_id.",
               "All from 0.9.15 on WebGL; game_started from the same build is accepted, so WebGL's finish rate reads low and is not.",
               "Nothing is lost for good: the client keeps them and resends after a fix."],
              ["Open Data health", "Acknowledge", "Snooze a day"])
    dp = [("Nov 4", "game_placement", "6", "1,800"), ("Nov 5", "game_placement", "8", "2,400")]
    c3 = card("Money", GOLD, "Crystals paid twice: 14 times, 4,200 crystals", "economy · raised Nov 5",
              '<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Day</th><th class="th">Source</th><th class="th">Paid twice</th><th class="th">Crystals</th></tr></thead><tbody>'
              + "".join(f'<tr><td class="td">{a}</td><td class="td">{b}</td><td class="td">{c}</td><td class="td">{d}</td></tr>' for a, b, c, d in dp)
              + "</tbody></table>",
              ["The same player, source and amount within 1 s: the server's double-payout check.",
               "All after a rematch in Joust; none in the 30 days before Nov 4.",
               "No player ids reach Claude: it read the daily counts, not who."],
              ["Open Economy", "Acknowledge", "Snooze a day"])
    fin = [("Tandava", 74), ("Redline", 78), ("Scurry", 70), ("Switchback", 63), ("Skein", 55), ("Headlong", 4)]
    c4_rows = "".join(
        f'<div style="display:grid;grid-template-columns:96px 300px 44px;gap:10px;align-items:center;height:19px">'
        f'<span class="small" style="color:{TEXT};font-size:13px">{m}</span>'
        f'<span style="display:block;height:10px;width:{max(3, 300 * v / 100):.0f}px;background:{DANGER if m == "Headlong" else BLUE}"></span>'
        f'<span class="small num" style="color:{TEXT};text-align:right;font-size:13px">{v}%</span></div>' for m, v in fin)
    c4 = card("Mode", GOLD, "Hardly anyone finishes Headlong: 4%", "finished, last 7 days · raised Nov 4",
              f'<div style="display:flex;flex-direction:column;gap:2px">{c4_rows}</div>',
              ["96 starts in 7 days, 4 finished; the other modes' median is 66%.",
               "Players leave after 38 s at the controls (median); 81% of those sessions end in quit_to_menu.",
               "It began Nov 1, when intensity 3 became the default card."],
              ["Open Matches", "Acknowledge", "Snooze a day"])
    grid = f'<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;flex:1;min-width:0">{c1}{c2}{c3}{c4}</div>'
    rules = [("D1 return", "below 35%, or 8 pts under its 4-week mean", "a day", True),
             ("Rejected events", "over 5x the 14-day median", "a day", True),
             ("Crystals paid twice", "any", "a day", True),
             ("Finished, per mode", "under 15% with 30+ starts", "7 days", True),
             ("Rows per play", "above 1.05", "a day", True),
             ("New players", "down 40% on the week before", "a week", False)]
    rt = ('<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Watch</th><th class="th">Raise when</th><th class="th">Over</th><th class="th">On</th></tr></thead><tbody>'
          + "".join(f'<tr><td class="td" style="white-space:normal">{a}</td><td class="td" style="white-space:normal;color:{SOFT}">{b}</td><td class="td">{c}</td>'
                    f'<td class="td"><span style="display:inline-block;width:30px;height:16px;box-shadow:inset 0 0 0 1px {JADE if on else LINE};position:relative">'
                    f'<i style="position:absolute;top:3px;{"right:3px;background:" + JADE if on else "left:3px;background:" + SOFT};width:10px;height:10px"></i></span></td></tr>'
                    for a, b, c, on in rules) + "</tbody></table>")
    side = panel("Thresholds", rt
                 + '<div style="display:flex;gap:8px"><button type="button" class="btn ghost sls">New rule</button></div>'
                 + f'<div style="display:flex;justify-content:space-between;align-items:center;padding:10px 12px;background:rgba(5,0,3,.35)">'
                   f'<span class="small" style="color:{TEXT}">Claude also flags what no rule names</span>'
                   f'<span style="display:inline-block;width:30px;height:16px;box-shadow:inset 0 0 0 1px {JADE};position:relative">'
                   f'<i style="position:absolute;top:3px;right:3px;background:{JADE};width:10px;height:10px"></i></span></div>'
                 + '<span class="small" style="font-size:13px">Claude reads the aggregates an hour after each rebuild, never player ids, and writes each card\'s why. A rule raises a card on its own, Claude or not.</span>'
                 + '<span class="lab" style="margin-top:6px">Resolved this week</span>'
                 + "".join(f'<div style="display:flex;justify-content:space-between;gap:10px"><span class="small" style="color:{TEXT}">{a}</span><span class="small">{b}</span></div>'
                           for a, b in (("Rows per play 1.81 (the #1639 fix)", "Nov 3 · Shombith"), ("New players down 52%", "Nov 1 · expected"),
                                        ("Launch p90 over 15 s on Android", "Oct 31 · Yash"))),
                 "admins edit; readers see", "width:500px;flex:none")
    right = segs(["Open 4", "Snoozed 1", "Resolved 3"], "Open 4") + segs(["Release", "Editor", "Port", "Dev"], "Release")
    body = (header("Alerts · Claude watches the numbers", "Alerts", right)
            + f'<div style="display:flex;gap:12px;flex:1;min-height:0">{grid}{side}</div>')
    return screen("alerts", body)


# ---------------------------------------------------------------- 4. funnel builder

def funnel_board():
    steps = [("First launch", "game_first_launched"), ("FTUE: First boost", "ftue_step_completed"),
             ("FTUE finished", "ftue_completed"), ("First match finished", "game_completed"),
             ("Second session", "a 2nd day with events"), ("First crystal spend", "crystals_spent")]
    chips = "".join(
        (f'<span class="small" style="color:{SOFT};font-size:18px">&#8594;</span>' if i else "")
        + f'<div class="sls" style="display:flex;flex-direction:column;gap:2px;padding:8px 12px;background:{SURFACE};'
          f'box-shadow:inset 0 0 0 1px {LINE}"><span style="display:flex;gap:8px;align-items:baseline">'
          f'<span class="num" style="color:{JADE};font-size:14px">{i + 1}</span><span style="font-size:15px">{n}</span></span>'
          f'<span class="small" style="font-size:12px">{e}</span></div>' for i, (n, e) in enumerate(steps))
    builder = panel("Steps, in order", f'<div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">{chips}'
                    f'<button type="button" class="btn ghost sls" style="height:52px">+ Step</button></div>'
                    f'<div style="display:flex;gap:10px;align-items:center"><span class="lab">Within</span>{segs(["1 d", "7 d", "30 d"], "7 d")}'
                    f'<span class="lab" style="margin-left:10px">Order</span>{segs(["In this order", "Any order"], "In this order")}'
                    f'<span class="lab" style="margin-left:10px">Players who started</span><span class="chip sls">Oct 7 to Nov 5</span>'
                    f'<span style="flex:1"></span><button type="button" class="btn ghost sls">Save as segment</button>'
                    f'<button type="button" class="btn def sls">Save funnel</button></div>',
                    "any event, any FTUE step; drag to reorder")
    plat = [("WindowsPlayer", JADE, [96, 84, 64, 58, 45, 14]), ("Android", RUBY, [78, 58, 34, 30, 17, 4]),
            ("IPhonePlayer", GOLD, [38, 29, 20, 16, 11, 3])]
    pct = [(n, c, [round(100 * v / vals[0]) for v in vals]) for n, c, vals in plat]
    allv = [212, 171, 118, 104, 73, 21]
    cats = [(n, f"{allv[i]} players · {round(100 * allv[i] / 212)}%") for i, (n, _) in enumerate(steps)]
    drops = [round(100 - 100 * allv[i + 1] / allv[i]) for i in range(5)]
    chart = col_chart(cats, pct, 1500, 290, 100, lambda v: f"{v:.0f}%", yticks=(0, 50, 100), pad_b=44,
                      between=[(f"&#8600; -{d}%", DANGER if d >= 50 else SOFT) for d in drops])
    main = cpanel("Conversion", switches("Bars", "% of start", "Platform", forms=("Bars", "Line", "Table")),
                  chart + legend([(n, c) for n, c, _ in plat])
                  + '<span class="small" style="font-size:13px">Each column is the share of that platform\'s starters who reached the step. Between the steps: the drop, all platforms.</span>',
                  "212 players · release builds · each step within 7 days of the one before", "flex:none")
    times = [("Launch to First boost", "2 min 10 s", "4 min"), ("First boost to FTUE finished", "6 min", "11 min"),
             ("FTUE to a finished match", "4 min", "9 min"), ("To a second session", "1.4 days", "3 days"),
             ("To a first crystal spend", "3.2 days", "6 days")]
    tt = cpanel("Time between steps", switches("Table", "Median", None, forms=("Bars", "Table"), tools=False),
                '<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Between</th><th class="th">Median</th><th class="th">3 in 4 by</th></tr></thead><tbody>'
                + "".join(f'<tr><td class="td">{a}</td><td class="td">{b}</td><td class="td" style="color:{SOFT}">{c}</td></tr>' for a, b, c in times)
                + "</tbody></table>", "players who made the step", "flex:1")
    instead = cpanel("Where they went instead", switches("Bars", "Players", None, forms=("Bars", "Pie", "Table"), tools=False),
                     ranked_bars([("Quit the app", 31), ("To the Arcade", 11), ("Still in FTUE", 6), ("Settings, quit", 5)], 560, BLUE)
                     + '<span class="small" style="font-size:13px">The 53 who reached First boost and did not finish the FTUE: their next event. The biggest drop is the one shown.</span>',
                     "after step 2", "width:640px;flex:none")
    right = filters(rng=None, era="Pre-beta")
    body = (header("Funnels · release builds · players who started Oct 7 to Nov 5", "Funnel builder", right)
            + builder + main + f'<div style="display:flex;gap:12px;flex:1;min-height:0">{tt}{instead}</div>')
    return screen("funnels", body)


# ---------------------------------------------------------------- 5. release compare

def releases_board():
    A, B = JADE, RUBY

    def pick(letter, colour, ver, when, n):
        return (f'<div class="sls" style="display:flex;gap:12px;align-items:center;height:44px;padding:0 14px;background:{SURFACE};'
                f'box-shadow:inset 0 0 0 1px {LINE},inset 0 -2px 0 {colour}"><span class="num" style="font-size:22px;color:{colour}">{letter}</span>'
                f'<span style="font-size:16px">{ver}</span><span class="small" style="white-space:nowrap">{when} · {n}</span><span class="small">&#9662;</span></div>')
    picks = (f'<div style="display:flex;gap:12px;align-items:center">{pick("A", A, "0.9.13", "Oct 16", "61 new")}'
             f'<span class="lab">against</span>{pick("B", B, "0.9.14", "Oct 30", "74 new")}'
             f'<span class="lab" style="margin-left:14px">Compare</span>{segs(["First 7 days of each", "Same calendar days"], "First 7 days of each")}'
             f'<span class="lab" style="margin-left:14px">Platform</span>{segs(["All", "Windows", "Android", "iPhone", "WebGL"], "All")}</div>')
    verdict = [("Finished", "61%", "66%", "+5 pts", "better"), ("Exceptions per 100 sessions", "3.1", "1.4", "-1.7", "better"),
               ("Launch to menu, p50", "7.8 s", "6.1 s", "-1.7 s", "better"), ("Launch to menu, p90", "14.2 s", "18.9 s", "+4.7 s", "worse"),
               ("Back next day (D1)", "41%", "29%", "-12 pts", "worse"), ("Back in 7 days (D7)", "22%", "15%", "-7 pts", "too early"),
               ("Rematch within 10 min", "47%", "52%", "+5 pts", "better"), ("Rows per play", "1.00", "1.00", "0", "same"),
               ("Matches per player", "4.1", "4.6", "+0.5", "better")]
    col = {"better": CTA, "worse": DANGER, "too early": SOFT, "same": SOFT}
    vt = ('<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Measure</th>'
          f'<th class="th" style="color:{A}">A 0.9.13</th><th class="th" style="color:{B}">B 0.9.14</th><th class="th">Change</th><th class="th">Read</th></tr></thead><tbody>'
          + "".join(f'<tr><td class="td" style="white-space:normal;padding:6px 8px">{m}</td><td class="td" style="padding:6px 8px">{a}</td>'
                    f'<td class="td" style="padding:6px 8px">{b}</td><td class="td" style="padding:6px 8px">{c}</td>'
                    f'<td class="td" style="color:{col[r]};padding:6px 8px">{r}</td></tr>' for m, a, b, c, r in verdict) + "</tbody></table>")
    plat = [("Windows", "43%", "43%", "0"), ("Android", "38%", "17%", "-21 pts"), ("iPhone", "40%", "36%", "-4 pts")]
    pt = ('<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">D1 by platform</th>'
          f'<th class="th" style="color:{A}">A</th><th class="th" style="color:{B}">B</th><th class="th">Change</th></tr></thead><tbody>'
          + "".join(f'<tr><td class="td" style="padding:6px 8px">{m}</td><td class="td" style="padding:6px 8px">{a}</td><td class="td" style="padding:6px 8px">{b}</td>'
                    f'<td class="td" style="padding:6px 8px;color:{DANGER if c.startswith("-2") else TEXT}">{c}</td></tr>' for m, a, b, c in plat) + "</tbody></table>")
    left = panel("Is B better?", f'<p class="body" style="font-size:16px">B is better on <span style="color:{CTA}">5</span>, worse on '
                 f'<span style="color:{DANGER}">2</span>, too early on 1.</p>' + vt
                 + '<span class="small" style="font-size:13px">"Too early" when either side has under 30 players; D7 for B has 18.</span>' + pt
                 + '<span class="small" style="font-size:13px">Worse on D1 is Android only: the same drop as the open alert.</span>'
                 + '<span class="lab" style="margin-top:6px">Who is on which build today</span>'
                 + k.mix_bar([(14, A), (71, B), (12, GOLD), (3, BLUE)], 560, 14)
                 + legend([("0.9.13 · 14%", A), ("0.9.14 · 71%", B), ("0.9.15 · 12%", GOLD), ("older · 3%", BLUE)])
                 + '<div style="display:flex;gap:8px"><button type="button" class="btn ghost sls">Open the D1 alert</button><button type="button" class="btn def sls">Share</button></div>',
                 "first 7 days of each", "grid-row:span 2")
    cw = 430
    fin = cpanel("Finished, by mode", switches("Bars", "Finished %", "Build", forms=("Bars", "Table"), tools=False),
                 pair_bars([("Tandava", 74, 78), ("Scurry", 70, 73), ("Switchback", 63, 70), ("Redline", 78, 76), ("Skein", 55, 62),
                            ("Joust", 49, 58)], A, B, cw)
                 + legend([("A 0.9.13", A), ("B 0.9.14", B)]), "top six modes")
    exc = cpanel("Exceptions per 100 sessions", switches("Line", "Exceptions", "Build", forms=("Bars", "Line", "Table"), tools=False),
                 line_chart([("A", A, [4.2, 3.8, 3.0, 3.1, 2.7, 2.9, 2.4]), ("B", B, [1.8, 1.5, 1.3, 1.6, 1.2, 1.4, 1.1])], cw, 270,
                            [(0, "day 0"), (3, "day 3"), (6, "day 6")], 5, 0, (0, 2, 4), lambda v: f"{v:.1f}", pad_r=80, dots=True)
                 + '<span class="small" style="font-size:13px">Top in B: NullReference in CrewSeat.Swap (31), then MissingReference in the ScoreBoard (12).</span>',
                 "days 0 to 6")
    lt = cpanel("Launch to menu", switches("Bars", "Launches", "Build", forms=("Bars", "Line", "Table"), tools=False),
                col_chart(["0-4", "4-6", "6-8", "8-10", "10-14", "14-20", "20+"],
                          [("A", A, [3, 18, 41, 22, 9, 5, 2]), ("B", B, [6, 34, 28, 10, 6, 9, 7])], cw, 210, 45,
                          lambda v: f"{v:.0f}", yticks=(0, 20, 40), pad_b=26)
                + '<span class="small" style="font-size:13px">Seconds. B starts faster for most and slower for a tail: 16 launches over 14 s, all Android.</span>',
                "launches, by seconds")
    ret = cpanel("Coming back", switches("Line", "Returned %", "Build", forms=("Line", "Bars", "Table"), tools=False),
                 line_chart([("A", A, [100, 41, 33, 30, 27, 25, 23, 22]), ("B", B, [100, 29, 24, 22, 20, 18, 16, 15])], cw, 240,
                            [(0, "D0"), (1, "D1"), (7, "D7")], 100, 0, (0, 50, 100), lambda v: f"{v:.0f}%", pad_r=80, dots=True)
                 + '<span class="small" style="font-size:13px">Share of each build\'s new players who played again on day N or later.</span>',
                 "by day after the first")
    grid = (f'<div style="display:grid;grid-template-columns:600px repeat(2,minmax(0,1fr));grid-template-rows:repeat(2,minmax(0,1fr));'
            f'gap:12px;flex:1;min-height:0">{left}{fin}{exc}{lt}{ret}</div>')
    body = (header("Releases · release builds · app_version", "Release compare", filters(rng=None))
            + picks + grid)
    return screen("releases", body)


# ---------------------------------------------------------------- 6. player compare

def compare_board():
    P = [("Dragon", JADE, "India (IN)", "WindowsPlayer"), ("Kes", RUBY, "United States (US)", "Android"),
         ("Halcyon", GOLD, "United Kingdom (GB)", "IPhonePlayer")]
    chips = "".join(f'<span class="chip sls" style="height:38px"><i style="display:block;width:10px;height:10px;background:{c}"></i>'
                    f'<span style="color:{TEXT}">{n}</span><i>&#215;</i></span>' for n, c, _, _ in P)
    right = (chips + '<button type="button" class="btn ghost sls">+ Player</button>'
             + segs(["All", "Solo vs AI", "Solo", "Multiplayer"], "All") + '<button type="button" class="btn def sls">Export</button>')
    rows = [("Matches", ["53", "31", "22"], 0), ("Active days", ["17", "12", "9"], 0), ("Lifespan", ["22 d", "40 d", "15 d"], 1),
            ("Finished", ["62%", "71%", "55%"], 1), ("Won", ["19%", "26%", "9%"], 1), ("Rematch within 10 min", ["48%", "35%", "41%"], 0),
            ("At the controls", ["61 min", "44 min", "27 min"], 0), ("Crystals", ["10,200", "4,200", "2,500"], 0),
            ("Mostly plays", ["Solo · 42%", "Solo vs AI · 55%", "Multiplayer · 50%"], None),
            ("Top mode", ["Tandava", "Scurry", "Joust"], None), ("Top vessel", ["Squirrel", "Dolphin", "Sparrow"], None),
            ("Last seen", ["today", "2 days ago", "19 days ago"], None)]
    head = ('<tr><th class="th">Measure</th>' + "".join(
        f'<th class="th" style="padding-bottom:10px"><span style="display:flex;flex-direction:column;gap:3px">'
        f'<span style="display:flex;gap:8px;align-items:center;font-family:Aldrich;font-size:17px;letter-spacing:0;text-transform:none;color:{TEXT}">'
        f'<i style="display:block;width:12px;height:12px;background:{c}"></i>{n}</span>'
        f'<span style="font-family:Aldrich;font-size:12px;letter-spacing:0;text-transform:none">{cc} · {pl}</span></span></th>'
        for n, c, cc, pl in P) + "</tr>")
    body_rows = "".join(
        f'<tr><td class="td" style="color:{SOFT};padding:5px 10px">{m}</td>' + "".join(
            f'<td class="td" style="padding:5px 10px"><span style="{"box-shadow:inset 0 -1px 0 " + TEXT + ";padding-bottom:2px" if best is not None and i == best else ""}">{v}</span></td>'
            for i, v in enumerate(vals)) + "</tr>" for m, vals, best in rows)
    table = panel("Side by side", f'<table style="border-collapse:collapse;width:100%"><thead>{head}</thead><tbody>{body_rows}</tbody></table>'
                  + '<span class="small" style="font-size:13px">The best in each row is underlined. A column is the player\'s whole history unless a filter above narrows it.</span>',
                  "from the Players page: tick up to three, then Compare")
    weeks = [(0, "Sep 14"), (4, "Oct 12"), (7, "Nov 2")]
    wk = cpanel("Matches a week", switches("Line", "Matches", "Player", forms=("Bars", "Line", "Table"), tools=False),
                line_chart([("Dragon", JADE, [None, None, None, None, None, 9, 21, 23]), ("Kes", RUBY, [3, 5, 4, 6, 4, 3, 4, 2]),
                            ("Halcyon", GOLD, [None, None, 6, 8, 5, 3, 0, 0])], 520, 330, weeks, 25, 0, (0, 10, 20),
                           lambda v: f"{v:.0f}", pad_r=110, dots=True),
                "the last 8 weeks", "flex:1")
    how = cpanel("How they play", switches("Bars", "Matches", "Play type", forms=("Bars", "Pie", "Table"), tools=False),
                 col_chart(["Solo vs AI", "Solo", "Multiplayer"], [("Dragon", JADE, [12, 22, 19]), ("Kes", RUBY, [17, 9, 5]),
                                                                   ("Halcyon", GOLD, [5, 6, 11])], 500, 300, 25,
                           lambda v: f"{v:.0f}", yticks=(0, 10, 20))
                 + legend([(n, c) for n, c, _, _ in P]), "colour is the player", "flex:1")
    left = (f'<div style="display:flex;flex-direction:column;gap:12px;flex:1;min-width:0">{table}'
            f'<div style="display:flex;gap:12px;flex:1;min-height:0">{wk}{how}</div></div>')
    like = [("Vesper", 91, "IN", "Tandava and Scurry; weekends; a third multiplayer"),
            ("Corvid", 88, "US", "the same modes; finishes 60%; evenings"),
            ("Juno", 84, "IN", "Squirrel first; solo heavy; 3 weeks in"),
            ("Talon", 81, "BR", "weekends 14:00 to 22:00; rematches half the time"),
            ("Moss", 77, "DE", "Tandava; more multiplayer than Dragon"),
            ("Ilse", 74, "PH", "the same mix; lapsed 9 days ago")]
    like_rows = "".join(
        f'<div style="display:grid;grid-template-columns:1fr 54px 34px;gap:10px;align-items:center;padding:8px 0;border-bottom:1px solid rgba(112,64,95,.6)">'
        f'<div style="display:flex;flex-direction:column;gap:2px;min-width:0"><span style="font-size:15px">{n} <span class="small">{cc}</span></span>'
        f'<span class="small" style="font-size:12px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{why}</span></div>'
        f'<span class="num" style="font-size:18px;text-align:right">{s}%</span>'
        f'<button type="button" class="ib sls" aria-label="Add to compare" style="color:{TEXT}">+</button></div>' for n, s, cc, why in like)
    side = panel("Players like Dragon", like_rows
                 + '<div style="display:flex;gap:8px"><button type="button" class="btn ghost sls">Compare the top 2</button>'
                   '<button type="button" class="btn def sls">Save as segment</button></div>'
                 + '<span class="lab" style="margin-top:6px">Alike in</span>'
                 + ranked_bars([("Play mix", 30), ("Modes played", 25), ("When they play", 20), ("Finish and win", 15), ("Lifespan", 10)], 440, BLUE, "%")
                 + '<span class="small" style="font-size:13px">Nearest players on these five, weighted; worked out on the server from each player\'s own numbers. Claude is not asked and sees no ids.</span>',
                 "6 of 45 · most alike first", "width:470px;flex:none")
    body = (header("Players / compare · release builds · all time", "Compare players", right)
            + f'<div style="display:flex;gap:12px;flex:1;min-height:0">{left}{side}</div>')
    return screen("players", body)


# ---------------------------------------------------------------- notes

def notes_board():
    rows = [
        ("Chart switches", "Every chart carries its own row: Form (Bars, Line, Pie, Table), Measure, Split by, and pin / copy / back. "
         "Business, Marketing and Analysis each keep their own setting of the same chart; the page never changes for them.",
         "In the contract: GET /v1/series (measure x split x over) feeds every switchable chart; a pair the aggregates cannot answer is a 400 and the option is hidden. "
         "New: each person's switch settings per chart (a small store); a country-by-day aggregate."),
        ("Live now", "Online now, in a match, in the menus, in the FTUE, parties open; the last hour by play type; what is being played; a tile per country; "
         "a ticker of events, paused by hand, never scrolling by itself.",
         "New: GET /v1/live over events.db's last 5 minutes (received_ms). The client flushes every 30 s, so live is 30 to 60 s behind. "
         "Names only for profile keys; readers get counts and countries."),
        ("Alerts", "Rules an admin sets (D1 under 35%, rejects 5x the median, any double payout, a mode under 15% finished, rows per play over 1.05) "
         "and anomalies Claude finds; each card shows its chart and why, and opens the page that explains it.",
         "Has: rejects, double_payouts, returns, daily_plays, plays by mode. New: an alerts table, rules in the store, an hourly job after the rebuild; "
         "Claude reads aggregates only. Rejects are per day today; per hour is new."),
        ("Funnel builder", "Any events or FTUE steps in order, within 1 / 7 / 30 days, strict or any order; conversion and drop-off, split by any "
         "dimension; time between steps; where the ones who dropped went instead; save as a funnel or a segment.",
         "Has: the fixed first-day funnel and ftue_steps. New: POST /v1/funnel over events.db per player (profiles key), only counts come back. "
         "No real-money purchase event exists: the last step is the first crystal spend."),
        ("Release compare", "Build A against build B over the first N days of each: finished, exceptions, launch time p50 / p90, D1 / D7, rematch, rows per play; "
         "a read per measure (better, worse, too early under 30 players), split by platform.",
         "Has: app_version on plays and events. New: launches and returns split by app_version; an exception event (rate-limited, a hash of the message "
         "and its top frame) the game does not send yet."),
        ("Player compare", "Two or three players side by side from the Players page (best per row underlined, matches a week, play types), and "
         "players like this one: the nearest six on play mix, modes, hours, finish and lifespan, to compare or save as a segment.",
         "Has: /v1/people and /v1/person carry every number. New: GET /v1/similar?id= (profiles key), worked out on the server; Claude is not asked."),
    ]
    body = "".join(f'<tr><td class="td" style="vertical-align:top;color:{JADE};white-space:nowrap">{a}</td>'
                   f'<td class="td" style="vertical-align:top;white-space:normal;line-height:1.45">{b}</td>'
                   f'<td class="td" style="vertical-align:top;white-space:normal;line-height:1.45;color:{SOFT}">{c}</td></tr>' for a, b, c in rows)
    return (f'<div style="width:1920px;height:900px;box-sizing:border-box;position:relative;overflow:hidden;background:{GROUND};'
            f'color:{TEXT};font-family:\'Aldrich\',sans-serif;padding:40px 56px;display:flex;flex-direction:column;gap:16px">'
            f'<span class="lab">Rev 66 · notes</span><h1 class="h1">Froglet Analytics, more ideas</h1>'
            f'<p class="body" style="color:{SOFT};max-width:1600px">Asked (Shombith, 2026-11-06, after picking all seven Rev 65 screens): '
            f'"If you have more ideas provide mockups lets do that too", and "switches in graphs which helps all these departments". '
            f'Six new screens of the same console, not options: pick any to build. Rev 65\'s kit and theme (Nocturne, the sea lines); every number '
            f'is sample data from Rev 65\'s set (Dragon, Kes and Halcyon are the same players).</p>'
            f'<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Screen</th><th class="th">What it adds</th>'
            f'<th class="th">What the server has, and what is new</th></tr></thead><tbody>{body}</tbody></table>'
            f'<p class="small" style="max-width:1700px">The switch row is the one piece every other board uses, so it would come first. Colour: a split wears Jade, Ruby, '
            f'Gold, Blue in that order with a legend, so colour is never the only cue; play types keep Rev 65\'s Jade solo vs AI, Gold solo, Ruby multiplayer; '
            f'unsplit data is Blue; a heat or map is one hue. On a board where colour means a build or a player, play types are words. Red is only a fault '
            f'(an alert, a worse number); Lime only a better one. No player id reaches Claude on any of these.</p></div>')


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    out = {
        "R66Switches": ("Rev 66 Switches - one chart as Business, Marketing and Analysis set it", switches_board()),
        "R66Live": ("Rev 66 Live - who is online and playing now", live_board()),
        "R66Alerts": ("Rev 66 Alerts - Claude watches the numbers", alerts_board()),
        "R66Funnel": ("Rev 66 Funnel - the funnel builder", funnel_board()),
        "R66Releases": ("Rev 66 Releases - build A against build B", releases_board()),
        "R66PlayerCompare": ("Rev 66 PlayerCompare - players side by side, and players like this one", compare_board()),
    }
    for key, (title, body) in out.items():
        (ROOT / f"{key}.dc.html").write_text(k.board(title, body), encoding="utf-8")
    (ROOT / "R66Notes.dc.html").write_text(k.board("Rev 66 - notes", notes_board(), 1920, 900), encoding="utf-8")
    print("wrote", ", ".join(sorted(out)) + ", R66Notes")


if __name__ == "__main__":
    main()
