#!/usr/bin/env python3
"""Rev 54 boards: colour sets in the Profile, four more modern looks.

Shombith, 2026-11-05, on Rev 51 A as built: "give me a few better modern mockups, I do not love these" (and the LB / RB
on the tabs was missing). What changes from Rev 51: the set's WORLD leads - a scene drawn in depth (near prisms large
and sharp, far ones small and soft, the set's own rims lighting the sea), little text, and one clear way to browse.
Every colour is still the set's shipped SO_ColorSet value (r51_profile_colour_sets.py reads them).

    A  Showroom      the picked set's world fills the page; a rail of sets along its foot (recommended)
    B  Paint chips   tall chips side by side, the picked one opens wide
    C  Plinth        one domain's three prism tiers large on a plinth; sets left, domain and pigments right
    D  Before/after  a divider you drag across the world: the set you wear on one side, the one you try on the other

    python3 Docs/MenuRedesignB/rounds/r54_colour_sets_modern.py <root> 54
"""
import importlib.util
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
_argv = sys.argv
sys.argv = [sys.argv[0], sys.argv[1], str(REV)]
spec = importlib.util.spec_from_file_location("r51", HERE / "r51_profile_colour_sets.py")
r51 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r51)
sys.argv = _argv

SETS = [s for s in r51.SETS if s["pal"]]          # Nocturne, Photic, Dart Frog (Clear Sight has no asset yet)
SET = {s["id"]: s for s in SETS}
ORDER = [s["id"] for s in SETS]
DOMS = [(k, n) for k, _, n in r51.DOMS]
mix, kc, board = r51.mix, r51.kc, r51.board
JADE, LIME, TEXT, SOFT = r51.JADE, r51.LIME, r51.TEXT, r51.SOFT
TICK, LOCK = r51.ICON_TICK, r51.ICON_LOCK
EXTRA_CSS = """
.ghost{position:absolute;inset:0}
.chipbtn{background:none;border:0;padding:0;cursor:pointer;text-align:left;color:inherit;font:inherit}
.chipbtn:focus-visible{outline:2px solid #00D4FF;outline-offset:3px}
.tagl{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;letter-spacing:.2em;text-transform:uppercase}
input[type=range].wipe{-webkit-appearance:none;appearance:none;width:100%;height:4px;background:#2B3266;outline:none;margin:0}
input[type=range].wipe::-webkit-slider-thumb{-webkit-appearance:none;width:22px;height:22px;background:#E6E9FF;clip-path:polygon(6px 0,100% 0,100% calc(100% - 6px),calc(100% - 6px) 100%,0 100%,0 6px);cursor:ew-resize}
input[type=range].wipe::-moz-range-thumb{width:22px;height:22px;background:#E6E9FF;border:0;border-radius:0;cursor:ew-resize}
input[type=range].wipe:focus-visible{outline:2px solid #00D4FF;outline-offset:6px}
"""
HELMET = r51.HELMET.replace("</style>", EXTRA_CSS + "</style>")


def full_board(title, body, logic, w=1920, h=1080, props=None):
    return board(title, body, logic, w, h, props).replace(r51.HELMET, HELMET)


# ---------------------------------------------------------------- the scene: a set's world, drawn in depth

def _rand(seed):
    s = seed & 0x7FFFFFFF

    def r():
        nonlocal s
        s = (s * 1103515245 + 12345) & 0x7FFFFFFF
        return s / 0x7FFFFFFF
    return r


def prism(w, rot, pal_d, tier, glow, blur, alpha):
    """A long bipyramid lit from the upper left: four facets from the tier's face and rim, the rim as its outline."""
    k = ["", "s", "x"][tier]
    face, rim = pal_d[k + "f"], pal_d[k + "r"]
    lit, half, dark = mix(face, rim, .5), mix(face, rim, .2), mix(face, "#000000", .35)
    h = w * 2.3
    flt = f"filter:drop-shadow(0 0 {glow:.0f}px {rim}99)" + (f" blur({blur:.1f}px)" if blur > .2 else "")
    return (f'<svg width="{w:.0f}" height="{h:.0f}" viewBox="0 0 40 92" aria-hidden="true" '
            f'style="transform:rotate({rot:.0f}deg);opacity:{alpha:.2f};{flt}">'
            f'<polygon points="20,0 2,38 20,44" fill="{lit}"></polygon><polygon points="20,0 38,38 20,44" fill="{half}"></polygon>'
            f'<polygon points="2,38 20,44 20,92" fill="{face}"></polygon><polygon points="38,38 20,44 20,92" fill="{dark}"></polygon>'
            f'<polygon points="20,0 38,38 20,92 2,38" fill="none" stroke="{rim}" stroke-width="1.4" stroke-linejoin="round"></polygon>'
            f'<line x1="2" y1="38" x2="38" y2="38" stroke="{rim}" stroke-opacity=".35" stroke-width="1"></line></svg>')


def scene(s, w, h, density=1.0, seed=11, dof=True, crystal=True, trail=True, wash=True):
    """The set's world: a deep sea washed by its rims, a field of prisms in depth (near large and sharp, far small and
    soft), a vessel's trail and a free crystal. Positions are seeded, so every board and every set shares the same world
    and only the colours change - which is the point of comparing sets."""
    pal = s["pal"]
    r = _rand(seed)
    out = [f'<div style="position:relative;width:{w}px;height:{h}px;overflow:hidden;background:#00010A">']
    out.append(f'<div class="ghost" style="background:radial-gradient(ellipse 75% 70% at 58% 42%,#0A1450 0%,#040A30 45%,#00010A 100%)"></div>')
    if wash:
        corners = [("18% 30%", "jade"), ("85% 25%", "ruby"), ("70% 85%", "gold"), ("25% 85%", "blue")]
        out.append('<div class="ghost" style="background:' + ",".join(
            f"radial-gradient(ellipse 45% 45% at {pos},{pal[d]['r']}26,transparent 70%)" for pos, d in corners) + '"></div>')
    # far stars
    stars = "".join(f'<circle cx="{r() * w:.0f}" cy="{r() * h:.0f}" r="{.5 + r() * 1.1:.1f}" fill="#E6E9FF" opacity="{.15 + r() * .4:.2f}"></circle>'
                    for _ in range(int(70 * w * h / (1840 * 864)) + 12))
    out.append(f'<svg class="ghost" width="{w}" height="{h}" aria-hidden="true">{stars}</svg>')
    # prisms: four domain clusters, depth z in 0..1
    n = max(10, int(46 * density * (w * h) / (1840 * 864) ** 1 * 1.0)) if w * h > 300000 else max(10, int(26 * density))
    centres = {"jade": (.2, .42), "ruby": (.78, .36), "gold": (.5, .24), "blue": (.46, .7)}
    items = []
    for i in range(n):
        d = DOMS[i % 4][0]
        cx, cy = centres[d]
        z = r()
        x = (cx + (r() - .5) * .42) * w
        y = (cy + (r() - .5) * .38) * h
        tier = 0 if r() < .62 else (1 if r() < .7 else 2)
        size = (min(w, h) / 864) * (12 + z * z * 46)
        items.append((z, x, y, size, d, tier, (r() - .5) * 70))
    items.sort()
    for z, x, y, size, d, tier, rot in items:
        blur = (1 - z) * 2.6 if dof else 0
        out.append(f'<div class="pz" style="left:{x - size / 2:.0f}px;top:{y - size:.0f}px">'
                   + prism(size, rot, pal[d], tier, 3 + z * 10, blur, .45 + z * .55) + "</div>")
    if trail:
        tc, vc = pal["jade"]["t"], pal["jade"]["v"]
        x0, y0, x1, y1 = -20, h * .82, w * .74, h * .3
        path = f"M {x0:.0f} {y0:.0f} C {w * .25:.0f} {h * .78:.0f}, {w * .42:.0f} {h * .5:.0f}, {x1:.0f} {y1:.0f}"
        sc = min(w, h) / 864
        out.append(f'<svg class="ghost" width="{w}" height="{h}" aria-hidden="true">'
                   f'<path d="{path}" fill="none" stroke="{tc}" stroke-opacity=".28" stroke-width="{14 * sc:.1f}" stroke-linecap="round" style="filter:blur({4 * sc:.1f}px)"></path>'
                   f'<path d="{path}" fill="none" stroke="{tc}" stroke-width="{3 * sc:.1f}" stroke-linecap="round"></path>'
                   f'<g transform="translate({x1:.0f} {y1:.0f}) rotate(-28) scale({sc * 1.6:.2f})" style="filter:drop-shadow(0 0 8px {vc})">'
                   f'<polygon points="22,0 -16,-11 -9,0 -16,11" fill="{vc}"></polygon><polygon points="22,0 -9,0 -16,11" fill="#000" opacity=".25"></polygon></g></svg>')
    if crystal:
        cta, sc = pal["cta"], min(w, h) / 864
        cx, cy = w * .86, h * .16
        out.append(f'<svg class="ghost" width="{w}" height="{h}" aria-hidden="true"><g transform="translate({cx:.0f} {cy:.0f}) scale({sc * 1.4:.2f})" '
                   f'style="filter:drop-shadow(0 0 10px {cta})"><polygon points="-16,0 -8,-14 8,-14 16,0 8,14 -8,14" fill="{cta}" opacity=".9"></polygon>'
                   f'<polygon points="-16,0 -8,-14 8,-14 16,0" fill="#fff" opacity=".25"></polygon></g></svg>')
    out.append("</div>")
    return "".join(out)


def pairs_row(s, size=20, gap=28, text=15):
    return ('<div style="display:flex;flex-wrap:wrap;gap:10px ' + str(gap) + 'px">' + "".join(
        f'<div style="display:flex;align-items:center;gap:10px"><span class="tagl" style="color:{SOFT};width:40px">{n}</span>'
        f'<span style="display:flex"><span style="width:{size}px;height:{size}px;background:{s["pal"][k]["r"]}"></span>'
        f'<span style="width:{size}px;height:{size}px;background:{s["pal"][k]["f"]};box-shadow:inset 0 0 0 1px #434C89"></span></span>'
        f'<span style="font-size:{text}px;color:#C9CDEA;white-space:nowrap">{s["pairs"][k][0]} over {s["pairs"][k][1]}</span></div>'
        for k, n in DOMS) + "</div>")


# ---------------------------------------------------------------- the chrome every option shares

def chrome(active="Colour sets", pilot=False):
    tabs = []
    for name in ("Record", "Colour sets", "Audio Studio"):
        on = name == active
        u = f'<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:{JADE}"></span>' if on else ""
        tabs.append(f'<button class="tabw cap" type="button" role="tab" aria-selected="{"true" if on else "false"}" '
                    f'style="position:relative;font-size:17px;color:{TEXT if on else SOFT}">{name}{u}</button>')
    head = ('<div class="abs" style="left:40px;top:100px;display:flex;align-items:center;gap:40px">'
            '<div style="display:flex;flex-direction:column;gap:4px"><span class="lab">Profile</span><div style="font-size:28px">Your pilot</div></div>'
            '<div role="tablist" style="display:flex;align-items:center;gap:30px;margin-left:24px">' + kc("LB") + "".join(tabs) + kc("RB") + '</div></div>'
            '<div class="abs" style="right:40px;top:106px;display:flex;gap:12px"><span class="btn gho" style="height:44px">' + kc("B") + 'Back</span></div>')
    return r51.ground() + r51.topnav() + head


def logic(extra_state="", extra_vals=""):
    sets = json.dumps({s["id"]: s["name"] for s in SETS})
    return f"""const NAMES = {sets};
const ORDER = {json.dumps(ORDER)};
class Component extends DCLogic {{
constructor(p){{ super(p); this.state = {{ sel: this.props.start ?? 'dartfrog', eq: 'nocturne', toast: false{extra_state} }}; }}
renderVals() {{
const st = this.state;
const is = {{}}, pick = {{}}, eqIs = {{}};
ORDER.forEach(id => {{ is[id] = st.sel === id; eqIs[id] = st.eq === id; pick[id] = () => this.setState({{ sel: id, toast: false }}); }});
const idx = ORDER.indexOf(st.sel);
return {{
  is, eqIs, pick,
  canApply: st.sel !== st.eq, isEq: st.sel === st.eq, eqName: NAMES[st.eq], selName: NAMES[st.sel], toast: st.toast,
  pos: (idx + 1) + ' of ' + ORDER.length,
  prev: () => this.setState({{ sel: ORDER[(idx + ORDER.length - 1) % ORDER.length], toast: false }}),
  next: () => this.setState({{ sel: ORDER[(idx + 1) % ORDER.length], toast: false }}),
  apply: () => this.setState({{ eq: st.sel, toast: true }}),
  dismiss: () => this.setState({{ toast: false }}){extra_vals}
}};
}}
}}"""


PROPS = {"start": {"editor": "enum", "options": ORDER, "default": "dartfrog", "section": "State"}}

TOAST = ('<sc-if value="{{toast}}" hint-placeholder-val="{{ false }}"><div role="status" class="abs sls" style="right:40px;top:96px;width:440px;background:#00010A;'
         'box-shadow:inset 0 0 0 1px #434C89;display:flex;z-index:9;animation:tstIn 320ms cubic-bezier(.22,1,.36,1) both">'
         f'<span style="width:3px;background:{LIME}"></span><div style="padding:13px 18px;flex:1"><div class="cap" style="font-size:14px">Colour set applied</div>'
         '<div style="font-size:14px;color:#C9CDEA;margin-top:3px">{{eqName}} is on every prism, vessel and trail.</div></div>'
         '<button class="pick" type="button" onClick="{{dismiss}}" aria-label="Close" style="padding:0 14px;color:#747BAD">'
         '<svg width="12" height="12" viewBox="0 0 12 12" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M1 1l10 10M11 1L1 11"></path></svg></button></div></sc-if>')


def apply_block(align="flex-end"):
    return (f'<div style="display:flex;align-items:center;gap:14px;justify-content:{align}">'
            '<sc-if value="{{canApply}}" hint-placeholder-val="{{ true }}"><button class="btn cta" type="button" onClick="{{apply}}">' + kc("A") + 'Apply</button></sc-if>'
            f'<sc-if value="{{{{isEq}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="display:inline-flex;gap:10px;align-items:center;font-size:15px;color:{LIME}">'
            f'{TICK}<span style="color:{TEXT}">Equipped</span></span></sc-if></div>')


# ---------------------------------------------------------------- A  Showroom

def option_a():
    W, H, X, Y = 1840, 864, 40, 176
    blocks = []
    for s in SETS:
        i = s["id"]
        blocks.append(f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == "dartfrog" else "false"} }}}}"><div class="ghost">'
                      + scene(s, W, H, density=1.3)
                      + '<div class="ghost" style="background:linear-gradient(180deg,rgba(0,1,10,.55) 0%,rgba(0,1,10,0) 22%,rgba(0,1,10,0) 48%,rgba(0,1,10,.92) 82%)"></div>'
                      + f'<div class="abs" style="left:56px;bottom:226px;right:640px"><div class="tagl" style="color:{SOFT}">Colour set</div>'
                      f'<div style="font-size:104px;line-height:1;margin-top:8px;letter-spacing:-.01em">{s["name"]}</div>'
                      f'<div style="font-size:19px;color:#C9CDEA;margin-top:14px">{s["line"]}</div>'
                      f'<div style="margin-top:22px">{pairs_row(s)}</div></div></div></sc-if>')
    thumbs = []
    for s in SETS:
        i = s["id"]
        thumbs.append(f'<button class="chipbtn" type="button" onClick="{{{{pick.{i}}}}}" aria-label="{s["name"]}" style="position:relative;width:224px">'
                      f'<div class="sls" style="width:224px;height:112px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89">{scene(s, 224, 112, density=.9, dof=False, trail=False, crystal=False)}</div>'
                      f'<div style="display:flex;justify-content:space-between;margin-top:8px"><span class="cap" style="font-size:13px">{s["name"]}</span>'
                      f'<sc-if value="{{{{eqIs.{i}}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="color:{LIME}">{TICK}</span></sc-if></div>'
                      f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="abs" style="left:0;right:0;top:-6px;height:2px;background:{JADE}"></span></sc-if></button>')
    hero = (f'<section aria-label="Colour sets" class="abs" style="left:{X}px;top:{Y}px;width:{W}px;height:{H}px">'
            f'<div class="abs sl" style="inset:0;overflow:hidden">' + "".join(blocks) + '</div>'
            f'<div class="abs" style="left:56px;top:40px;display:flex;gap:18px;align-items:center"><span class="tagl" style="color:{SOFT}">' + '{{pos}}</span>'
            '<span class="tagl" style="color:#C9CDEA">Wearing {{eqName}}</span></div>'
            f'<div class="abs" style="right:48px;top:36px;font-size:14px;color:{SOFT}">Only you see your colour set</div>'
            '<div class="abs" style="left:56px;bottom:40px;display:flex;align-items:flex-end;gap:22px">'
            f'<button class="pick" type="button" onClick="{{{{prev}}}}" aria-label="Previous set" style="margin-bottom:34px">{kc("&lt;")}</button>'
            + "".join(thumbs) +
            f'<button class="pick" type="button" onClick="{{{{next}}}}" aria-label="Next set" style="margin-bottom:34px">{kc("&gt;")}</button></div>'
            '<div class="abs" style="right:48px;bottom:56px">' + apply_block() + '</div></section>')
    return chrome() + hero + TOAST


# ---------------------------------------------------------------- B  Paint chips

def option_b():
    X, Y, H, GAP, BIG = 40, 186, 850, 20, 1200
    small = (1840 - BIG - GAP * (len(SETS) - 1)) // (len(SETS) - 1)
    blocks = []
    for s in SETS:
        sel = s["id"]
        chips = []
        for t in SETS:
            if t["id"] == sel:
                chips.append(f'<div class="sl" style="position:relative;width:{BIG}px;height:{H}px;overflow:hidden;flex:none">'
                             + scene(t, BIG, H, density=1.1)
                             + '<div class="ghost" style="background:linear-gradient(180deg,rgba(0,1,10,0) 40%,rgba(0,1,10,.92) 85%)"></div>'
                             f'<div class="abs" style="left:44px;right:44px;bottom:40px"><div class="tagl" style="color:{SOFT}">Colour set</div>'
                             f'<div style="font-size:84px;line-height:1;margin-top:6px">{t["name"]}</div>'
                             f'<div style="font-size:18px;color:#C9CDEA;margin-top:12px">{t["line"]}</div>'
                             f'<div style="display:flex;align-items:flex-end;justify-content:space-between;gap:24px;margin-top:22px">'
                             f'<div style="flex:1">{pairs_row(t, 18, 22, 14)}</div>' + apply_block() + '</div></div></div>')
            else:
                bands = "".join(
                    f'<div style="flex:1;position:relative;background:linear-gradient(90deg,{t["pal"][k]["r"]} 0%,{t["pal"][k]["r"]} 38%,{t["pal"][k]["f"]} 62%,{t["pal"][k]["f"]} 100%)">'
                    f'<span class="tagl" style="position:absolute;left:14px;bottom:10px;color:#fff;mix-blend-mode:difference">{n}</span></div>' for k, n in DOMS)
                chips.append(f'<button class="chipbtn sl" type="button" onClick="{{{{pick.{t["id"]}}}}}" aria-label="{t["name"]}" '
                             f'style="position:relative;width:{small}px;height:{H}px;overflow:hidden;flex:none;display:flex;flex-direction:column;background:#060A26;box-shadow:inset 0 0 0 1px #2B3266">'
                             f'<div style="height:300px;flex:none">{scene(t, small, 300, density=.7, dof=False, crystal=False)}</div>'
                             f'<div style="flex:1;display:flex;flex-direction:column">{bands}</div>'
                             f'<div style="padding:18px 20px;flex:none;display:flex;justify-content:space-between;align-items:center"><span style="font-size:24px">{t["name"]}</span>'
                             f'<sc-if value="{{{{eqIs.{t["id"]}}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="color:{LIME}">{TICK}</span></sc-if></div></button>')
        blocks.append(f'<sc-if value="{{{{is.{sel}}}}}" hint-placeholder-val="{{{{ {"true" if sel == "dartfrog" else "false"} }}}}">'
                      f'<div class="abs" style="left:{X}px;top:{Y}px;width:1840px;display:flex;gap:{GAP}px">' + "".join(chips) + '</div></sc-if>')
    return chrome() + "".join(blocks) + TOAST


# ---------------------------------------------------------------- C  Plinth

TIERS = ("Prism", "Shielded", "Super-shielded")


def option_c():
    rows = []
    for s in SETS:
        i = s["id"]
        dots = "".join(f'<span style="width:14px;height:14px;background:{s["pal"][k]["r"]}"></span>' for k, _ in DOMS)
        rows.append(f'<button class="chipbtn" type="button" onClick="{{{{pick.{i}}}}}" style="position:relative;display:block;width:400px;padding:22px 24px">'
                    f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="abs" style="left:0;top:0;bottom:0;width:2px;background:{JADE}"></span>'
                    '<span class="abs" style="inset:0;background:rgba(0,212,255,.06)"></span></sc-if>'
                    f'<div style="position:relative;display:flex;justify-content:space-between;align-items:center"><span style="font-size:30px">{s["name"]}</span>'
                    f'<sc-if value="{{{{eqIs.{i}}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="color:{LIME}">{TICK}</span></sc-if></div>'
                    f'<div style="position:relative;display:flex;gap:6px;margin-top:12px">{dots}</div></button>')
    left = ('<div class="abs" style="left:40px;top:196px;width:400px"><div class="tagl" style="color:#747BAD;padding:0 24px 12px">Colour sets</div>'
            + "".join(rows) + f'<div style="padding:22px 24px;color:{SOFT};font-size:14px;line-height:1.5">Only you see your colour set.<br>Your domain and squad stay the same.</div></div>')
    stages = []
    for s in SETS:
        i = s["id"]
        per_dom = []
        for k, n in DOMS:
            d = s["pal"][k]
            big = "".join(
                f'<div style="display:flex;flex-direction:column;align-items:center;gap:22px;transform:translateY({[-10, 26, -30][t]}px)">'
                + prism(110 if t != 1 else 128, [-8, 4, 10][t], d, t, 22, 0, 1)
                + f'<span class="tagl" style="color:#C9CDEA">{TIERS[t]}</span></div>' for t in range(3))
            card = (f'<div class="tagl" style="color:{SOFT}">{n}</div><div style="display:flex;flex-direction:column;gap:16px;margin-top:16px">'
                    + "".join(f'<div style="display:flex;align-items:center;gap:14px"><span style="width:40px;height:40px;background:{c};box-shadow:inset 0 0 0 1px #434C89"></span>'
                              f'<div><div class="tagl" style="color:{SOFT};font-size:11px">{lab}</div><div style="font-size:18px">{nm}</div></div></div>'
                              for lab, nm, c in (("Rim", s["pairs"][k][0], d["r"]), ("Face", s["pairs"][k][1], d["f"]), ("Vessel", "Hull", d["v"]), ("Trail", "Trail", d["t"])))
                    + '</div>')
            per_dom.append(f'<sc-if value="{{{{dm.{k}}}}}" hint-placeholder-val="{{{{ {"true" if k == "jade" else "false"} }}}}">'
                           f'<div class="abs" style="left:0;right:0;top:240px;display:flex;justify-content:center;gap:90px;align-items:flex-end">{big}</div>'
                           f'<div class="abs" style="left:1040px;top:120px;width:360px">{card}</div></sc-if>')
        stages.append(f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == "dartfrog" else "false"} }}}}">'
                      '<div class="abs sl" style="left:0;top:0;width:1000px;height:844px;overflow:hidden">' + scene(s, 1000, 844, density=.8)
                      + '<div class="ghost" style="background:rgba(0,1,10,.55)"></div>'
                      '<div class="abs" style="left:150px;right:150px;top:600px;height:90px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,.18),transparent 70%)"></div>'
                      '<div class="abs" style="left:250px;right:250px;top:630px;height:2px;background:linear-gradient(90deg,transparent,#E6E9FF66,transparent)"></div>'
                      f'<div class="abs" style="left:48px;top:40px"><div class="tagl" style="color:{SOFT}">Colour set</div><div style="font-size:64px;margin-top:4px">{s["name"]}</div>'
                      f'<div style="font-size:17px;color:#C9CDEA;margin-top:8px">{s["line"]}</div></div></div>'
                      + "".join(per_dom) + '</sc-if>')
    domtabs = "".join(
        f'<button class="tabw cap" type="button" onClick="{{{{dpick.{k}}}}}" style="position:relative;font-size:16px;color:{TEXT}">{n}'
        f'<sc-if value="{{{{dm.{k}}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:{JADE}"></span></sc-if></button>'
        for k, n in DOMS)
    stage = ('<div class="abs" style="left:480px;top:196px;width:1400px;height:844px">' + "".join(stages)
             + f'<div class="abs" style="left:1040px;top:40px;display:flex;gap:26px">{domtabs}</div>'
             '<div class="abs" style="left:1040px;bottom:20px">' + apply_block("flex-start") + '</div></div>')
    return chrome() + left + stage + TOAST


# ---------------------------------------------------------------- D  Before / after

def option_d():
    W, H, X, Y = 1840, 640, 40, 176
    eq = SET["nocturne"]
    blocks = []
    for s in SETS:
        i = s["id"]
        blocks.append(f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == "dartfrog" else "false"} }}}}">'
                      '<div class="ghost" style="width:{{wipe}};overflow:hidden">'
                      f'<div class="abs" style="left:0;top:0;width:{W}px;height:{H}px">{scene(s, W, H, density=1.2)}</div></div>'
                      f'<div class="abs" style="left:40px;top:32px"><div class="tagl" style="color:{LIME}">Try</div><div style="font-size:44px;margin-top:2px">{s["name"]}</div></div></sc-if>')
    hero = (f'<section aria-label="Before and after" class="abs" style="left:{X}px;top:{Y}px;width:{W}px;height:{H}px">'
            '<div class="abs sl" style="inset:0;overflow:hidden">'
            f'<div class="ghost">{scene(eq, W, H, density=1.2)}</div>'
            f'<div class="abs" style="right:40px;top:32px;text-align:right"><div class="tagl" style="color:{SOFT}">Wearing</div><div style="font-size:44px;margin-top:2px">' + '{{eqName}}</div></div>'
            + "".join(blocks)
            + '<div class="abs" style="top:0;bottom:0;left:{{wipe}};width:2px;margin-left:-1px;background:#E6E9FF;box-shadow:0 0 18px rgba(230,233,255,.6)"></div>'
            '</div></section>')
    under = (f'<div class="abs" style="left:{X}px;top:{Y + H + 24}px;width:{W}px">'
             '<label for="wipe" class="tagl" style="color:#747BAD">Drag to compare - or the right stick</label>'
             '<input id="wipe" class="wipe" type="range" min="0" max="100" value="{{split}}" onChange="{{onWipe}}" style="margin-top:14px"></div>')
    rail = []
    for s in SETS:
        i = s["id"]
        rail.append(f'<button class="chipbtn" type="button" onClick="{{{{pick.{i}}}}}" style="position:relative;width:200px">'
                    f'<div class="sls" style="width:200px;height:84px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89">{scene(s, 200, 84, density=.8, dof=False, trail=False, crystal=False)}</div>'
                    f'<div style="display:flex;justify-content:space-between;margin-top:6px"><span class="cap" style="font-size:12px">{s["name"]}</span>'
                    f'<sc-if value="{{{{eqIs.{i}}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="color:{LIME}">{TICK}</span></sc-if></div>'
                    f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="abs" style="left:0;right:0;top:-6px;height:2px;background:{JADE}"></span></sc-if></button>')
    foot = (f'<div class="abs" style="left:{X}px;top:{Y + H + 88}px;width:{W}px;display:flex;align-items:flex-end;justify-content:space-between">'
            '<div style="display:flex;gap:20px">' + "".join(rail) + '</div>' + apply_block() + '</div>')
    return chrome() + hero + under + foot + TOAST


# ---------------------------------------------------------------- compare and notes

def compare_board():
    heroes = "".join(
        f'<div><div class="sl" style="overflow:hidden">{scene(s, 580, 330, density=.9)}</div>'
        f'<div style="display:flex;justify-content:space-between;align-items:baseline;margin-top:12px"><span style="font-size:26px">{s["name"]}</span>'
        f'<span style="font-size:13px;color:{SOFT}">{s["by"]}</span></div><div style="margin-top:10px">{pairs_row(s, 14, 16, 12)}</div></div>'
        for s in SETS)
    sel = [("A", "Showroom", "the world fills the page; a rail of sets along its foot", "recommended"),
           ("B", "Paint chips", "chips side by side; the picked one opens wide", ""),
           ("C", "Plinth", "one domain's three tiers large; sets left, pigments right", ""),
           ("D", "Before / after", "drag a divider between the set you wear and the one you try", "")]
    opts = "".join(
        f'<div style="padding:18px 20px;box-shadow:inset 0 0 0 1px #2B3266" class="sl"><div style="display:flex;gap:12px;align-items:baseline">'
        f'<span style="font-size:30px">{k}</span><span style="font-size:22px">{n}</span>'
        + (f'<span class="tagl" style="color:{LIME};margin-left:auto">{rec}</span>' if rec else "")
        + f'</div><div style="font-size:14px;color:#C9CDEA;margin-top:8px;line-height:1.45">{d}</div></div>' for k, n, d, rec in sel)
    body = (f'<div style="padding:44px 56px"><span class="lab">Rev {REV} - compare</span>'
            '<div style="font-size:34px;margin:6px 0 6px">The three sets in the new scene, and the four ways to pick one</div>'
            f'<div style="font-size:15px;color:{SOFT};margin-bottom:24px">One world for every set (the same seeded prisms, trail, vessel and crystal), so only '
            "the colours change. Near prisms large and sharp, far ones small and soft; each set's rims tint the sea.</div>"
            f'<div style="display:grid;grid-template-columns:repeat(3, minmax(0, 1fr));gap:28px">{heroes}</div>'
            f'<div style="display:grid;grid-template-columns:repeat(4, minmax(0, 1fr));gap:20px;margin-top:34px">{opts}</div></div>')
    return board(title("Compare"), body).replace(r51.HELMET, HELMET)


def notes_board():
    cols = [
        ("What changed from Rev 51", [
            "Asked: \"give me a few better modern mockups, I do not love these\". Rev 51 led with cards and text; these lead with the world.",
            "One scene drawn in depth for every set: near prisms large and sharp, far ones small and soft, the four domains' rims lighting the sea, a vessel's trail and a free crystal. The positions are the same for every set, so only the colours change.",
            "The tabs carry LB / RB, as the board did and the build now does (Record | Colour sets | Audio Studio)."]),
        ("The four", [
            "A Showroom (recommended): the set's world fills the page under a big name; a rail of sets along its foot, A to apply. The most like a modern cosmetics screen, one decision per screen.",
            "B Paint chips: every set at once as tall chips of its four rim-over-face pairs; the picked one opens wide. Best for comparing at a glance.",
            "C Plinth: one domain's three prism tiers large on a plinth, so a shielded and a super-shielded prism can be judged; a domain switch on the right.",
            "D Before / after: drag a divider across one world between the set you wear and the one you try. The clearest answer to 'what will change'."]),
        ("In Unity", [
            "The scene is the menu cell itself, behind the Profile's veil, repainted live as the player browses (ThemeManager.ApplyColorSet's preview), or a small render-to-texture of a prism cluster for B and C.",
            "Applying still raises the cell-change veil; the fix in this commit makes the repaint reach every prism and crystal already in the cell (it used to need a restart).",
            "Every set is unlocked for now; the Later boards of Rev 51 still show how a quest lock would read."]),
        ("Decisions", [
            "1. Which option (A, B, C or D), or parts of two.",
            "2. Should browsing preview on the real cell behind the Profile (no extra render, but the cell repaints as you scroll) or only APPLY repaint it?",
            "3. Audio Studio as its own tab (as drawn) or kept on Record as built."]),
    ]
    html = "".join(f'<div style="flex:1;min-width:0"><span class="lab" style="font-size:14px">{t}</span>'
                   + "".join(f'<p style="font-size:15px;line-height:1.5;color:#C9CDEA;margin:12px 0 0">{i}</p>' for i in items) + '</div>'
                   for t, items in cols)
    body = (f'<div style="padding:40px 56px"><span class="lab">Rev {REV} - notes</span><div style="font-size:34px;margin:6px 0 22px">Colour sets, more modern</div>'
            f'<div style="display:flex;gap:40px">{html}</div></div>')
    return board(title("Notes"), body, w=1920, h=900).replace(r51.HELMET, HELMET)


def title(key):
    canvas = json.loads((ROOT / "canvas.json").read_text())
    return (canvas["boards"].get(f"R{REV}{key}.dc.html") or {}).get("title") or f"Rev {REV} {key}"


def main():
    dom_state = ", dom: 'jade'"
    dom_vals = (",\n  dm: { jade: st.dom === 'jade', ruby: st.dom === 'ruby', gold: st.dom === 'gold', blue: st.dom === 'blue' },\n"
                "  dpick: { jade: () => this.setState({ dom: 'jade' }), ruby: () => this.setState({ dom: 'ruby' }), "
                "gold: () => this.setState({ dom: 'gold' }), blue: () => this.setState({ dom: 'blue' }) }")
    wipe_state = ", split: 50"
    wipe_vals = (",\n  split: String(st.split), wipe: st.split + '%',\n"
                 "  onWipe: (e) => { const v = e && e.target ? Number(e.target.value) : 50; this.setState({ split: isNaN(v) ? 50 : v }); }")
    out = {
        "Compare": compare_board(),
        "A": full_board(title("A"), option_a(), logic(), props=PROPS),
        "B": full_board(title("B"), option_b(), logic(), props=PROPS),
        "C": full_board(title("C"), option_c(), logic(dom_state, dom_vals), props=PROPS),
        "D": full_board(title("D"), option_d(), logic(wipe_state, wipe_vals), props=PROPS),
        "Notes": notes_board(),
    }
    for k, html in out.items():
        (ROOT / f"R{REV}{k}.dc.html").write_text(html)
        print(f"wrote R{REV}{k}.dc.html ({len(html) // 1024} KB)")


if __name__ == "__main__":
    main()
