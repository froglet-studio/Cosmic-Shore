#!/usr/bin/env python3
"""Rev 68 boards: the Fleet Codex for the website, four looks (Shombith, 2026-11-08).

Asked, on the built Rev 48 B codex: "I do not like the fleet codex UI at all what do you mean select an ability and
then scroll down to see the animation and even that is not cool. The texts do not look cool as well with this font
maybe change that too. Can you show me 4 mockup designs along with all fleets and a fleet window. This should be on
our website soon so it should look better and engagable"; then "Every animation should be smooth, there should be
filters for game modes inside a vessel some story about the vessel as well."

Each option is ONE tall board (1920 x 2160): the whole fleet on top, the Squirrel's window under it. In every window
the picked ability's move PLAYS right beside it (a small scene of the ability happening, with the controller button,
key and phone touch pressing in time with it), so nothing is below the fold; the games carry filters (All, each
element, Arcade, Arena); the vessel's story is its class asset's own text. Every animation moves only transform and
opacity on an eased curve, one 3.2 s clock per scene, so it stays smooth. Colours are the Nocturne colour set's
tokens (Tools/Analytics/console/themes.css); each option tries a different Google Fonts pairing.

Facts are read from the Fleet Codex data (`fleet_codex.py` output) and `Docs/FleetCodex/abilities.json`, never typed.

    python3 Docs/MenuRedesignB/rounds/r68_fleet_codex.py <root> <fleet.json>   # writes <root>/project/R68*.dc.html
"""
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
FLEET_JSON = json.load(open(sys.argv[2], encoding="utf-8"))
REPO = Path(__file__).resolve().parents[3]
WORDS = json.load(open(REPO / "Docs" / "FleetCodex" / "abilities.json", encoding="utf-8"))

# ---------------------------------------------------------------- Nocturne (themes.css, data-fs-theme="nocturne")
T = {"ground": "#13010C", "deep": "#050003", "surface": "#3C1D31", "raised": "#70405F", "text": "#F5E6EE",
     "soft": "#A8869A", "off": "#6C5A65", "cta": "#C4F637", "jade": "#65E1DF", "ruby": "#DF5CE5", "gold": "#FFC549",
     "blue": "#7BA0FF", "face": "#480135", "wash": "#210118"}

# ---------------------------------------------------------------- the canvas's uploaded art (/_blob/<id>)
HULL = {"Manta": "1dfc5ccf5597ce1757d3df79ae0a19d1", "Dolphin": "2b3b0cc00c95ca53719597e466d881cb",
        "Rhino": "9c2d40deaaf3047946715ff8410e7b8f", "Urchin": "4eb9cd7ec4ecae65326d4f0bc952efb0",
        "Grizzly": "c380d140a449b101d472ac01a68fb7f1", "Squirrel": "27617fceea883c9a40458136506eb3ef",
        "Serpent": "1b9da511eba7837c68ed4e68474725a5", "Shrike": "5b7f1c59bcde45979d4913c57e781abc",
        "Sparrow": "6aef9a8a7aa0e752f5460db440362953", "Scarab": "e17c3128888451d102cba983e3b85734",
        "Thresher": "ddbdbd9ffa549e06fd6421bb27f85d39", "Stoat": "21914e3ed6c21be1664780c49a3d43c4",
        "BrittleStar": "a3b542258cef6a3907f216751f3393bc", "Angler": "60d425f092069d090668aba97c3e16fa",
        "Goby": "acdf7cda03fc718301da0085c5c27807", "Starling": "dd68e2019a195bc3986c327dccc3905f",
        "Jacks": "53789d005fafad3361aa5e12fdfeea52", "Honeyguide": "e8ebe75bbdcdc3957ba534191132440c",
        "Meerkat": "de076523c87916461c95de705a36c47d",
        # the codex shows these with the model their class asset's PreviewImage came from
        "Wrasse": "6aef9a8a7aa0e752f5460db440362953", "Orca": "6aef9a8a7aa0e752f5460db440362953",
        "TigerMoth": "8bf7ac800ce1fcf4b82a3c6d5e9857fd", "Swordfish": "9c2d40deaaf3047946715ff8410e7b8f"}
MARK = {"Charge": "5e320705da242326bd1f14d57cb94875", "Mass": "a94b4259b11be12ae624a402365e488f",
        "Space": "407c65dfa28d85aa1ea8e411299436b0", "Time": "34fa9e05d3c3263f8a856df7bfd0062a",
        "Omni": "ef6b3eba67683e1f949ec76faceb1fb0"}
ICON = {"Charge": "28caf13c4c51cc3dabdd9ddeb62fada3", "Mass": "e2cd6439ef4917f81b7f14c1e04b0d06",
        "Space": MARK["Space"], "Time": "f1478c57f5014fcd7215444bd90cd937", "Omni": "187b01f5930c06570dc38a4f7b702a49"}
PAD = {"RT": "8f66fc8db175fc9dad110dc28c1d1a04", "LT": "878f20c2fd5584d694da93570b98557f"}
CARD = {"Joust": "e1ed99dc1ca95f7ad7bd6799d2fef248", "Scurry": "9e0ae5004d61a1b41e0d36a172a80737",
        "Skim Race": "abfc74a0ada7ebdae0759591cdd25b79", "Broadside": "9da0d8eda380ada6d08ea573195d13ae",
        "Brood Rush": "9d3d0a6f48806339300b9ad2281d9bcf", "Regatta": "0a8a3d51016e89f3d0b86eb7820a8c94",
        "Astro League": "cca3fdfdb93bf65155c6431ed93e6396", "Maelstrom": "b139c1cb8ada70c55845af6a39401438"}
B = "/_blob/"
ELEMENTS = ["Charge", "Mass", "Space", "Time"]

# ---------------------------------------------------------------- the fleet, read from fleet.json
FLEET = []
for v in FLEET_JSON["vessels"]:
    if not v["status"]["hasMap"] or v["key"] not in HULL:
        continue
    crew = v.get("crew") or {}
    names, seats = crew.get("names") or [], crew.get("seats") or 1
    games = {}
    for c in v["cards"]:
        if "ArcadeGames" in (c.get("rosters") or []) and c.get("elements"):
            games.setdefault(c["elements"][0], c["name"])
    tag = (v.get("description") or "").split(". ")[0].rstrip(".")
    FLEET.append({"key": v["key"], "name": " & ".join(names) if names else v["name"], "id": v["id"],
                  "crew": seats > 1, "badge": f"{seats} SEATS" if seats > 1 else "", "img": B + HULL[v["key"]],
                  "tag": tag[:70] + ("..." if len(tag) > 70 else ""),
                  "egames": sum(1 for e in ELEMENTS if e in games), "pick": v["key"] == "Squirrel"})
FLEET.sort(key=lambda f: (f["crew"], f["id"]))

# ---------------------------------------------------------------- the Squirrel
SQ = next(v for v in FLEET_JSON["vessels"] if v["key"] == "Squirrel")
STORY = SQ["description"].replace("\\u2019", "’")   # the class asset's own words (SO_Class_Squirrel)
ROW = {r.get("element"): r for r in SQ["controls"]["rows"]}
KIT = []
for el in ELEMENTS + ["Omni"]:
    w = WORDS["Squirrel"][el]
    if el == "Omni":
        e, l5 = SQ["omni"], "no level 5"
        name = "Shield Ring"
    else:
        e = SQ["elements"][el]
        name, l5 = e["ability"], "L5 " + e["upgrade"]["label"]
    r = ROW.get(el) or {}
    press = r.get("pad") not in (None, "no button")
    KIT.append({"el": el.upper(), "name": name, "icon": B + ICON[el], "mark": B + MARK[el],
                "trig": w.get("trigger", ""), "does": w["does"], "scales": w.get("scales", "Flown into, never pressed."),
                "l5": l5, "press": press, "pad": r.get("pad", ""), "key": r.get("pc", ""), "touch": r.get("touch", "")})
SCENE_OF = ["joust", "ring", "steal", "skim", "omni"]
GAMES = []
for c in SQ["cards"]:
    els = c.get("elements") or []
    arena = "ArenaGames" in (c.get("rosters") or [])
    GAMES.append({"name": c["name"], "img": B + CARD[c["name"]], "els": els,
                  "kind": "Arena" if arena else "Arcade",
                  "label": (" + ".join(els) + (" arena" if arena else " game")) if els else "Party game",
                  "mark": B + MARK[els[0]] if els else B + MARK["Omni"],
                  "sub": f"{c['hullsOnCard']} hulls" if c["hullsOnCard"] > 1 else "Squirrels only",
                  "desc": c.get("description") if (c.get("description") or "").count(" ") > 4 else ""})
GAMES.sort(key=lambda g: (g["kind"] == "Arena", (ELEMENTS + [""]).index(g["els"][0] if g["els"] else "")))
FLIGHT = SQ["flight"]

# ---------------------------------------------------------------- the moves: one scene per ability, on one clock
EASE = "cubic-bezier(.45,0,.2,1)"
SCENE_CSS = f"""
.scene{{position:relative;overflow:hidden;width:100%;height:100%}}
.scene .lane{{position:absolute;left:0;right:0;top:50%;height:1px;background:linear-gradient(90deg,transparent,{T['raised']},transparent)}}
.hull{{position:absolute;top:50%;left:0;width:46px;height:26px;margin-top:-13px;animation:fly 3.2s {EASE} infinite;will-change:transform}}
.hull svg{{width:100%;height:100%;display:block}}
@keyframes fly{{0%{{transform:translateX(-60px)}}100%{{transform:translateX(560px)}}}}
.crys{{position:absolute;left:52%;top:50%;width:22px;height:22px;margin:-11px 0 0 -11px;border:2px solid {T['text']};transform:rotate(45deg);animation:crys 3.2s {EASE} infinite}}
@keyframes crys{{0%,40%{{transform:translateX(0) rotate(45deg) scale(1);opacity:1}}52%{{transform:translateX(12px) rotate(45deg) scale(1.35);opacity:1}}62%,100%{{transform:translateX(14px) rotate(45deg) scale(0);opacity:0}}}}
.plus{{position:absolute;left:54%;top:30%;font-weight:700;font-size:15px;color:{T['jade']};opacity:0;animation:plus 3.2s {EASE} infinite}}
@keyframes plus{{0%,52%{{opacity:0;transform:translateY(8px)}}62%{{opacity:1;transform:translateY(0)}}86%,100%{{opacity:0;transform:translateY(-14px)}}}}
.ringwrap{{position:absolute;left:58%;top:50%;width:0;height:0}}
.blk{{position:absolute;width:10px;height:10px;margin:-5px 0 0 -5px;background:{T['ruby']};transform:rotate(var(--a)) translateX(34px) scale(0);opacity:0;animation:blk 3.2s {EASE} infinite}}
@keyframes blk{{0%,16%{{transform:rotate(var(--a)) translateX(10px) scale(0);opacity:0}}28%,70%{{transform:rotate(var(--a)) translateX(34px) scale(1);opacity:1}}84%,100%{{transform:rotate(var(--a)) translateX(40px) scale(0);opacity:0}}}}
.streak{{position:absolute;left:30%;top:50%;width:46%;height:2px;margin-top:-1px;background:linear-gradient(90deg,transparent,{T['jade']});transform-origin:left;transform:scaleX(0);opacity:0;animation:streak 3.2s {EASE} infinite}}
@keyframes streak{{0%,50%{{transform:scaleX(0);opacity:0}}60%{{transform:scaleX(1);opacity:1}}80%,100%{{transform:scaleX(1);opacity:0}}}}
.prow{{position:absolute;left:8%;right:8%;top:62%;display:flex;justify-content:space-between}}
.pr{{position:relative;width:22px;height:22px}}
.pr i,.pr b{{position:absolute;inset:0;transform:rotate(45deg) scale(.8)}}
.pr i{{background:{T['ruby']};animation:prA 3.2s {EASE} infinite;animation-delay:var(--d)}}
.pr b{{background:{T['jade']};opacity:0;animation:prB 3.2s {EASE} infinite;animation-delay:var(--d)}}
@keyframes prA{{0%,10%{{opacity:1}}20%,88%{{opacity:0}}100%{{opacity:1}}}}
@keyframes prB{{0%,10%{{opacity:0;transform:rotate(45deg) scale(.8)}}18%{{opacity:1;transform:rotate(45deg) scale(1.05)}}26%,88%{{opacity:1;transform:rotate(45deg) scale(.8)}}100%{{opacity:0}}}}
.hull.low{{top:40%}}
.meter{{position:absolute;left:8%;right:8%;bottom:12%;height:6px;background:{T['deep']};box-shadow:inset 0 0 0 1px {T['raised']}}}
.meter i{{position:absolute;inset:0;background:{T['jade']};transform-origin:left;animation:meter 3.2s {EASE} infinite}}
@keyframes meter{{0%{{transform:scaleX(.18)}}90%{{transform:scaleX(1)}}100%{{transform:scaleX(.18)}}}}
.pr.g i{{background:{T['raised']};animation:none}}.pr.g b{{display:none}}
.burst{{position:absolute;left:50%;top:50%;width:0;height:0}}
.oc{{position:absolute;width:26px;height:26px;margin:-13px 0 0 -13px;border:2px solid {T['gold']};transform:rotate(45deg);animation:oc 3.2s {EASE} infinite}}
@keyframes oc{{0%,10%{{transform:rotate(45deg) scale(1);opacity:1}}24%{{transform:rotate(45deg) scale(1.3);opacity:1}}32%,100%{{transform:rotate(45deg) scale(0);opacity:0}}}}
.sb{{position:absolute;width:12px;height:12px;margin:-6px 0 0 -6px;background:{T['text']};box-shadow:0 0 0 2px {T['jade']};opacity:0;animation:sb 3.2s {EASE} infinite}}
@keyframes sb{{0%,26%{{transform:rotate(var(--a)) translateX(0) scale(.2);opacity:0}}40%,78%{{transform:rotate(var(--a)) translateX(44px) scale(1);opacity:1}}92%,100%{{transform:rotate(var(--a)) translateX(48px) scale(.6);opacity:0}}}}
/* the devices press on the same clock: a tap lands at 18-26 %, the ring at 28 % */
.press{{animation:press 3.2s {EASE} infinite;transform-origin:center}}
@keyframes press{{0%,16%{{transform:scale(1);filter:brightness(1)}}20%,24%{{transform:scale(.84);filter:brightness(1.6)}}30%,100%{{transform:scale(1);filter:brightness(1)}}}}
.keyp{{animation:keyp 3.2s {EASE} infinite}}
@keyframes keyp{{0%,16%{{transform:translateY(0);box-shadow:inset 0 -4px 0 {T['raised']},inset 0 0 0 1px {T['raised']}}}20%,24%{{transform:translateY(3px);box-shadow:inset 0 -1px 0 {T['jade']},inset 0 0 0 1px {T['jade']}}}30%,100%{{transform:translateY(0);box-shadow:inset 0 -4px 0 {T['raised']},inset 0 0 0 1px {T['raised']}}}}}
.lift{{animation:lift 3.2s {EASE} infinite}}
@keyframes lift{{0%,14%{{transform:translateY(0) scale(1);opacity:1}}20%,30%{{transform:translateY(-9px) scale(1.2);opacity:.3}}38%,100%{{transform:translateY(0) scale(1);opacity:1}}}}
.drift{{animation:drift 3.2s {EASE} infinite}}
@keyframes drift{{0%,100%{{transform:translate(0,0)}}30%{{transform:translate(5px,-3px)}}65%{{transform:translate(-4px,3px)}}}}
@media (prefers-reduced-motion:reduce){{.scene *,.press,.keyp,.lift,.drift{{animation:none!important}}}}
"""
HULL_SVG = (f'<svg viewBox="0 0 46 26" aria-hidden="true"><path d="M2 13 L16 4 L44 13 L16 22 Z" fill="{T["text"]}"/>'
            f'<path d="M2 13 L16 9 L16 17 Z" fill="{T["jade"]}"/><path d="M-30 13 H2" stroke="{T["jade"]}" stroke-width="2" opacity=".5"/></svg>')


def scene(kind):
    hull = f'<span class="hull{" low" if kind in ("steal", "skim") else ""}">{HULL_SVG}</span>'
    if kind == "joust":
        body = '<span class="crys"></span><span class="plus">+ petals</span>' + hull
    elif kind == "ring":
        body = ('<span class="ringwrap">' + "".join(f'<i class="blk" style="--a:{a}deg"></i>' for a in range(0, 360, 45))
                + '</span><span class="streak"></span>' + hull)
    elif kind == "steal":
        body = ('<span class="prow">' + "".join(f'<span class="pr" style="--d:{0.42 * n + 0.15:.2f}s"><i></i><b></b></span>'
                                                for n in range(6)) + '</span>' + hull)
    elif kind == "skim":
        body = ('<span class="prow">' + "".join('<span class="pr g"><i></i></span>' for _ in range(6)) + '</span>'
                + hull + '<span class="meter"><i></i></span>')
    else:
        body = ('<span class="burst"><span class="oc"></span>' +
                "".join(f'<i class="sb" style="--a:{a}deg"></i>' for a in range(0, 360, 45)) + '</span>')
    return f'<div class="scene" aria-hidden="true"><span class="lane"></span>{body}</div>'


def devices(k, style="row"):
    """The controller button, the key and the phone touch for one ability, pressing on the scene's clock."""
    if k["press"]:
        pad = f'<img class="press" src="{B + PAD[k["pad"]]}" alt="{k["pad"]}" style="width:40px;height:40px">'
        key = f'<span class="kc keyp">{k["key"]}</span>'
        phone = ('<span class="ph"><span class="th lift"></span><span class="th"></span></span>' if "left" in k["touch"].lower()
                 else '<span class="ph"><span class="th"></span><span class="th lift"></span></span>')
        cap = [k["pad"], k["key"], k["touch"]]
    else:
        pad = f'<span class="ph" style="width:56px"><span class="th drift"></span><span class="th drift"></span></span>'
        key = '<span class="kc" style="opacity:.55">W A S D</span>'
        phone = '<span class="ph"><span class="th drift"></span><span class="th drift"></span></span>'
        cap = ["Just fly", "Just fly", "Just fly"]
    cells = [("CONTROLLER", pad, cap[0]), ("KEYBOARD", key, cap[1]), ("PHONE", phone, cap[2])]
    return ('<div class="dev ' + style + '">' + "".join(
        f'<div class="dv"><span class="dl">{lab}</span><span class="dg">{g}</span><span class="dc">{c}</span></div>'
        for lab, g, c in cells) + '</div>')


DEVICE_CSS = f"""
.dev{{display:flex;gap:10px}}
.dev.col{{flex-direction:column}}
.dv{{flex:1;display:flex;flex-direction:column;align-items:center;gap:6px;padding:10px 8px;background:{T['deep']};box-shadow:inset 0 0 0 1px {T['raised']};min-width:0}}
.dl{{font-size:10px;letter-spacing:.16em;color:{T['soft']}}}
.dg{{height:44px;display:flex;align-items:center;justify-content:center}}
.dc{{font-size:12px;color:{T['text']};text-align:center;white-space:nowrap}}
.kc{{display:inline-flex;align-items:center;justify-content:center;min-width:64px;height:34px;padding:0 10px;font-size:13px;font-weight:600;color:{T['text']};background:{T['surface']};box-shadow:inset 0 -4px 0 {T['raised']},inset 0 0 0 1px {T['raised']}}}
.ph{{display:inline-flex;align-items:center;justify-content:space-between;width:74px;height:40px;padding:0 9px;border:2px solid {T['raised']};border-radius:9px;box-sizing:border-box}}
.th{{width:14px;height:18px;border-radius:50%;background:{T['soft']}}}
"""


def step_blocks(inner):
    """Five sc-if blocks: the picked ability's scene and devices (only the picked one is in the page)."""
    return "".join(f'<sc-if value="{{{{s{i}}}}}" hint-placeholder-val="{{{{{"true" if i == 1 else "false"}}}}}">{inner(i, k)}</sc-if>'
                   for i, k in enumerate(KIT))


SEA = f"""
<svg class="sea" viewBox="0 0 1920 2160" preserveAspectRatio="none" aria-hidden="true">
{"".join(f'<path d="M-400 {y} C 0 {y-40}, 400 {y+40}, 800 {y} S 1600 {y-40}, 2000 {y} S 2800 {y+40}, 3200 {y}" fill="none" stroke="{c}" stroke-opacity="{o}" stroke-width="1.2"/>' for y, c, o in [(220, T['jade'], .22), (520, T['blue'], .18), (860, T['ruby'], .16), (1240, T['jade'], .18), (1580, T['blue'], .16), (1900, T['ruby'], .14)])}
</svg>"""
SEA_CSS = """.sea{position:absolute;left:0;top:0;width:3840px;height:100%;animation:sea 90s linear infinite;pointer-events:none}
@keyframes sea{from{transform:translateX(0)}to{transform:translateX(-1200px)}}"""

LOGIC = r"""
class Component extends DCLogic {
  constructor(props) { super(props); this.state = { sel: 1, filt: 'All', fl: 'All' }; }
  renderVals() {
    const K = __KIT__, G = __GAMES__, FL = __FLEET__;
    const sel = this.state.sel, filt = this.state.filt, fl = this.state.fl;
    const abilities = K.map((k, i) => Object.assign({}, k, { cls: i === sel ? '__AB__ on' : '__AB__', pick: () => this.setState({ sel: i }) }));
    const games = G.filter(g => filt === 'All' || g.kind === filt || g.els.indexOf(filt) >= 0)
      .map(g => Object.assign({}, g, { cls: g.kind === 'Arena' ? '__GM__ arena' : '__GM__' }));
    const filters = ['All', 'Charge', 'Mass', 'Space', 'Time', 'Arcade', 'Arena'].map(f => ({ label: f, cls: f === filt ? 'chip on' : 'chip', pick: () => this.setState({ filt: f }) }));
    const fleetFilters = ['All', 'Solo', 'Crews'].map(f => ({ label: f, cls: f === fl ? 'chip on' : 'chip', pick: () => this.setState({ fl: f }) }));
    const fleet = FL.filter(v => fl === 'All' || (fl === 'Crews') === v.crew).map(v => Object.assign({}, v, { cls: v.pick ? '__FC__ on' : '__FC__' }));
    return { abilities, games, filters, fleetFilters, fleet, none: games.length === 0, noneText: 'No ' + filt + ' game seats the Squirrel yet.',
      cur: K[sel], s0: sel === 0, s1: sel === 1, s2: sel === 2, s3: sel === 3, s4: sel === 4, count: FL.length };
  }
}
"""


def logic(ab, gm, fc):
    return (LOGIC.replace("__KIT__", json.dumps(KIT, ensure_ascii=False)).replace("__GAMES__", json.dumps(GAMES, ensure_ascii=False))
            .replace("__FLEET__", json.dumps(FLEET, ensure_ascii=False)).replace("__AB__", ab).replace("__GM__", gm).replace("__FC__", fc))


def page(title, fonts, css, body, ab, gm, fc):
    return f"""<!doctype html>
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
<link href="https://fonts.googleapis.com/css2?{fonts}&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0;background:{T['ground']}}}
.app{{position:relative;overflow:hidden;color:{T['text']};background:{T['ground']}}}
.app *{{box-sizing:border-box}}
.soft{{-webkit-mask-image:radial-gradient(ellipse 58% 62% at 50% 50%,#000 52%,transparent 100%);mask-image:radial-gradient(ellipse 58% 62% at 50% 50%,#000 52%,transparent 100%)}}
button{{font:inherit;color:inherit}}
button:focus-visible,a:focus-visible{{outline:2px solid {T['jade']};outline-offset:2px}}
.chip{{border:0;cursor:pointer;height:34px;padding:0 14px;background:transparent;color:{T['soft']};box-shadow:inset 0 0 0 1px {T['raised']};font-size:13px;letter-spacing:.06em;transition:color .2s {EASE},background .2s {EASE},box-shadow .2s {EASE}}}
.chip:hover{{color:{T['text']}}}
.chip.on{{color:{T['ground']};background:{T['jade']};box-shadow:none}}
{SEA_CSS}
{SCENE_CSS}
{DEVICE_CSS}
{css}
</style>
</helmet>
<div class="app" style="width:1920px;height:2160px">
{SEA}
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1920,"height":2160}}}}'>
{logic(ab, gm, fc)}
</script>
</body>
</html>
"""


def filt_row(var="filters"):
    return (f'<div style="display:flex;gap:6px;flex-wrap:wrap"><sc-for list="{{{{{var}}}}}" as="f" hint-placeholder-count="7">'
            f'<button type="button" class="{{{{f.cls}}}}" onClick="{{{{f.pick}}}}">{{{{f.label}}}}</button></sc-for></div>')


def divider(label, y):
    return (f'<div style="position:absolute;left:80px;right:80px;top:{y}px;display:flex;align-items:center;gap:16px;font-size:12px;'
            f'letter-spacing:.2em;color:{T["soft"]}"><span>{label}</span><span style="flex:1;height:1px;background:{T["raised"]}"></span></div>')


NONE = (f'<sc-if value="{{{{none}}}}" hint-placeholder-val="{{{{false}}}}"><div style="padding:22px;color:{T["soft"]};'
        f'box-shadow:inset 0 0 0 1px {T["raised"]}">{{{{noneText}}}}</div></sc-if>')
STATS = (f'{FLIGHT["cruise"]:.0f}', "u/s cruise"), (f'{FLIGHT["pitch"]:.0f}', "deg/s turn"), ("4 / 4", "level 5 wired"), (str(len(GAMES)), "games")


# ======================================================================== A Showroom (recommended)
def option_a():
    css = f"""
.app{{font-family:'Sora',sans-serif}}
.disp{{font-family:'Unbounded',sans-serif;font-weight:700;letter-spacing:-.01em}}
.fc{{position:relative;display:flex;flex-direction:column;justify-content:flex-end;height:200px;padding:12px 14px;text-decoration:none;color:{T['text']};
  background:linear-gradient(180deg,{T['surface']} 0%,{T['deep']} 100%);box-shadow:inset 0 0 0 1px {T['raised']};overflow:hidden;
  transition:transform .35s {EASE},box-shadow .35s {EASE}}}
.fc img{{position:absolute;left:-6%;top:-4%;width:112%;height:78%;object-fit:cover;transition:transform .5s {EASE}}}
.fc:hover{{transform:translateY(-6px);box-shadow:inset 0 0 0 1px {T['jade']},0 18px 40px rgba(0,0,0,.45)}}
.fc:hover img{{transform:scale(1.06)}}
.fc.on{{box-shadow:inset 0 0 0 2px {T['jade']}}}
.fc b{{position:relative;font-family:'Unbounded',sans-serif;font-size:17px;font-weight:600}}
.fc small{{position:relative;font-size:12px;color:{T['soft']}}}
.fc em{{position:absolute;right:10px;top:10px;font-style:normal;font-size:10px;letter-spacing:.14em;padding:3px 8px;background:{T['deep']};box-shadow:inset 0 0 0 1px {T['ruby']};color:{T['ruby']}}}
.ab{{display:grid;grid-template-columns:56px 1fr;gap:14px;align-items:center;width:100%;text-align:left;border:0;cursor:pointer;padding:12px 16px;background:transparent;box-shadow:inset 0 0 0 1px {T['raised']};transition:background .3s {EASE},box-shadow .3s {EASE},transform .3s {EASE}}}
.ab:hover{{transform:translateX(4px)}}
.ab.on{{background:{T['surface']};box-shadow:inset 3px 0 0 {T['jade']},inset 0 0 0 1px {T['raised']}}}
.ab img{{width:56px;height:56px;object-fit:contain;background:{T['deep']};padding:6px}}
.ab b{{display:block;font-family:'Unbounded',sans-serif;font-size:18px;font-weight:600}}
.ab small{{font-size:12px;color:{T['soft']};letter-spacing:.08em}}
.gm{{position:relative;height:150px;display:flex;flex-direction:column;justify-content:flex-end;padding:14px;overflow:hidden;box-shadow:inset 0 0 0 1px {T['raised']}}}
.gm img{{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.75;transition:transform .6s {EASE}}}
.gm:hover img{{transform:scale(1.05)}}
.gm::after{{content:"";position:absolute;inset:0;background:linear-gradient(0deg,{T['ground']} 8%,transparent 70%)}}
.gm>*:not(img){{position:relative;z-index:1}}
.gm b{{font-family:'Unbounded',sans-serif;font-size:20px}}
.gm span{{font-size:12px;color:{T['soft']};letter-spacing:.06em}}
"""
    fleet = f"""
<header style="position:absolute;left:80px;right:80px;top:56px;display:flex;align-items:center;gap:28px">
  <img src="{B + MARK['Omni']}" alt="" style="width:30px;height:30px">
  <span class="disp" style="font-size:26px">Fleet Codex</span>
  <span style="flex:1"></span>
  {filt_row('fleetFilters')}
</header>
<div style="position:absolute;left:80px;top:150px;width:1100px">
  <div class="disp" style="font-size:76px;line-height:1">Pick your vessel.</div>
  <div style="margin-top:14px;font-size:19px;color:{T['soft']};max-width:760px">Every hull in the HyperSea, its four abilities and the games it plays in. Crews fly one hull with two or more pilots.</div>
</div>
<div style="position:absolute;left:80px;right:80px;top:340px;display:grid;grid-template-columns:repeat(8,minmax(0,1fr));gap:14px">
  <sc-for list="{{{{fleet}}}}" as="v" hint-placeholder-count="23"><a href="#{{{{v.key}}}}" class="{{{{v.cls}}}}"><img src="{{{{v.img}}}}" alt=""><sc-if value="{{{{v.crew}}}}" hint-placeholder-val="{{{{false}}}}"><em>{{{{v.badge}}}}</em></sc-if><b>{{{{v.name}}}}</b><small>{{{{v.egames}}}} of 4 element games</small></a></sc-for>
</div>
"""
    stage = lambda i, k: (f'<div style="display:flex;flex-direction:column;gap:16px;height:100%">'
                          f'<div style="display:flex;align-items:baseline;gap:14px"><span style="font-size:12px;letter-spacing:.18em;color:{T["soft"]}">{k["el"]}</span>'
                          f'<span class="disp" style="font-size:34px">{k["name"]}</span><span style="font-size:14px;color:{T["jade"]}">{k["trig"]}</span></div>'
                          f'<div style="height:200px;background:{T["deep"]};box-shadow:inset 0 0 0 1px {T["raised"]}">{scene(SCENE_OF[i])}</div>'
                          f'{devices(k)}'
                          f'<div style="font-size:16px;line-height:1.55">{k["does"]}</div>'
                          f'<div style="display:flex;gap:18px;font-size:13px;color:{T["soft"]}"><span>{k["scales"]}</span><span style="color:{T["cta"]};white-space:nowrap">{k["l5"]}</span></div></div>')
    window = f"""
{divider("THE VESSEL WINDOW", 1112)}
<div style="position:absolute;left:80px;top:1160px;width:620px;height:560px;overflow:hidden;background:radial-gradient(ellipse at 50% 45%,{T['face']} 0%,{T['ground']} 70%);box-shadow:inset 0 0 0 1px {T['raised']}">
  <img src="{B + HULL['Squirrel']}" alt="The Squirrel" class="soft" style="position:absolute;left:-40px;top:20px;width:700px">
  <div style="position:absolute;left:28px;right:28px;bottom:24px">
    <div style="font-size:12px;letter-spacing:.2em;color:{T['jade']}">VESSEL 6 · TWO-STICK · IN THE VESSEL CHANGER</div>
    <div class="disp" style="font-size:72px;line-height:1;margin-top:6px">Squirrel</div>
  </div>
</div>
<div style="position:absolute;left:80px;top:1740px;width:620px">
  <div style="font-size:12px;letter-spacing:.2em;color:{T['soft']}">ITS STORY</div>
  <p style="margin:10px 0 0;font-size:19px;line-height:1.55">{STORY}</p>
  <div style="display:flex;gap:30px;margin-top:20px">{"".join(f'<div><div class="disp" style="font-size:30px">{n}</div><div style="font-size:12px;color:{T["soft"]}">{l}</div></div>' for n, l in STATS)}</div>
</div>
<div style="position:absolute;left:740px;top:1160px;width:330px;display:flex;flex-direction:column;gap:8px">
  <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.cls}}}}" onClick="{{{{a.pick}}}}"><img src="{{{{a.icon}}}}" alt=""><span><small>{{{{a.el}}}}</small><b>{{{{a.name}}}}</b></span></button></sc-for>
</div>
<div style="position:absolute;left:1100px;top:1160px;width:740px;height:560px;padding:24px;background:{T['surface']};box-shadow:inset 0 0 0 1px {T['raised']}">
  {step_blocks(stage)}
</div>
<div style="position:absolute;left:740px;top:1750px;width:1100px">
  <div style="display:flex;align-items:center;gap:20px;margin-bottom:14px"><span class="disp" style="font-size:26px">Plays in</span><span style="flex:1"></span>{filt_row()}</div>
  <div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px">
    <sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="8"><div class="{{{{g.cls}}}}"><img src="{{{{g.img}}}}" alt=""><span><img src="{{{{g.mark}}}}" alt="" style="position:static;width:14px;height:14px;vertical-align:-2px;opacity:1"> {{{{g.label}}}}</span><b>{{{{g.name}}}}</b><span>{{{{g.sub}}}}</span></div></sc-for>
  </div>
  {NONE}
</div>
"""
    return page("Rev 68 A - Fleet Codex, Showroom", "family=Unbounded:wght@500;600;700&amp;family=Sora:wght@400;500;600",
                css, fleet + window, "ab", "gm", "fc")


# ======================================================================== B Trading cards
def option_b():
    css = f"""
.app{{font-family:'Manrope',sans-serif}}
.disp{{font-family:'Oxanium',sans-serif;font-weight:700;letter-spacing:.02em;text-transform:uppercase}}
.fc{{position:relative;display:flex;flex-direction:column;height:268px;padding:10px;text-decoration:none;color:{T['text']};background:linear-gradient(160deg,{T['raised']} 0%,{T['surface']} 30%,{T['deep']} 100%);
  border-radius:14px;box-shadow:inset 0 0 0 1px rgba(245,230,238,.18),0 10px 24px rgba(0,0,0,.4);transition:transform .45s {EASE},box-shadow .45s {EASE}}}
.fc:hover{{transform:translateY(-10px) rotate(-1.5deg);box-shadow:inset 0 0 0 1px {T['gold']},0 24px 44px rgba(0,0,0,.5)}}
.fc.on{{box-shadow:inset 0 0 0 2px {T['gold']},0 10px 24px rgba(0,0,0,.4)}}
.fc .art{{height:130px;border-radius:9px;overflow:hidden;background:{T['face']}}}
.fc .art img{{width:100%;height:100%;object-fit:cover;transition:transform .6s {EASE}}}
.fc:hover .art img{{transform:scale(1.08)}}
.fc .no{{position:absolute;right:16px;top:16px;font-family:'Oxanium',sans-serif;font-size:13px;color:{T['gold']}}}
.fc b{{margin-top:10px;font-family:'Oxanium',sans-serif;font-size:19px;text-transform:uppercase;line-height:1.1}}
.fc small{{margin-top:4px;font-size:12px;line-height:1.35;color:{T['soft']}}}
.fc em{{margin-top:auto;font-style:normal;font-size:11px;letter-spacing:.14em;color:{T['ruby']}}}
.ab{{position:relative;display:flex;flex-direction:column;gap:8px;text-align:left;border:0;cursor:pointer;height:460px;padding:14px;border-radius:16px;
  background:linear-gradient(170deg,{T['surface']},{T['deep']});box-shadow:inset 0 0 0 1px rgba(245,230,238,.16);transition:transform .45s {EASE},box-shadow .45s {EASE}}}
.ab:hover{{transform:translateY(-8px)}}
.ab.on{{transform:translateY(-16px);box-shadow:inset 0 0 0 2px {T['jade']},0 26px 50px rgba(0,0,0,.5)}}
.ab .ic{{height:120px;display:flex;align-items:center;justify-content:center;border-radius:10px;background:{T['ground']}}}
.ab .ic img{{width:92px;height:92px;object-fit:contain}}
.ab b{{font-family:'Oxanium',sans-serif;font-size:22px;text-transform:uppercase}}
.ab small{{font-size:11px;letter-spacing:.16em;color:{T['soft']}}}
.ab p{{margin:0;font-size:13px;line-height:1.45;color:{T['text']}}}
.ab i{{margin-top:auto;font-style:normal;font-size:12px;color:{T['cta']}}}
.gm{{position:relative;flex:none;width:250px;height:330px;border-radius:14px;overflow:hidden;display:flex;flex-direction:column;justify-content:flex-end;padding:14px;box-shadow:inset 0 0 0 1px rgba(245,230,238,.16);transition:transform .45s {EASE}}}
.gm:hover{{transform:translateY(-8px)}}
.gm img{{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}}
.gm::after{{content:"";position:absolute;inset:0;background:linear-gradient(0deg,{T['deep']} 10%,transparent 65%)}}
.gm>*:not(img){{position:relative;z-index:1}}
.gm b{{font-family:'Oxanium',sans-serif;font-size:22px;text-transform:uppercase}}
.gm span{{font-size:12px;color:{T['soft']}}}
"""
    fleet = f"""
<header style="position:absolute;left:80px;right:80px;top:60px;display:flex;align-items:flex-end;gap:24px">
  <div><div style="font-size:13px;letter-spacing:.24em;color:{T['gold']}">THE COLLECTION · {{{{count}}}} HULLS</div><div class="disp" style="font-size:84px;line-height:1">Fleet Codex</div></div>
  <span style="flex:1"></span>{filt_row('fleetFilters')}
</header>
<div style="position:absolute;left:80px;right:80px;top:228px;display:grid;grid-template-columns:repeat(8,minmax(0,1fr));gap:16px">
  <sc-for list="{{{{fleet}}}}" as="v" hint-placeholder-count="23"><a href="#{{{{v.key}}}}" class="{{{{v.cls}}}}"><span class="art"><img src="{{{{v.img}}}}" alt=""></span><span class="no">No. {{{{v.id}}}}</span><b>{{{{v.name}}}}</b><small>{{{{v.tag}}}}</small><em>{{{{v.badge}}}}</em></a></sc-for>
</div>
"""
    stage = lambda i, k: (f'<div style="display:grid;grid-template-columns:380px 1fr;gap:20px;align-items:center">'
                          f'<div style="height:170px;border-radius:12px;background:{T["ground"]};box-shadow:inset 0 0 0 1px {T["raised"]}">{scene(SCENE_OF[i])}</div>'
                          f'<div><div style="font-size:12px;letter-spacing:.18em;color:{T["jade"]};margin-bottom:8px">HOW TO FIRE {k["name"].upper()} · {k["trig"].upper()}</div>{devices(k)}</div></div>')
    window = f"""
{divider("THE VESSEL WINDOW", 1112)}
<div style="position:absolute;left:80px;top:1160px;width:420px;height:620px;border-radius:20px;padding:16px;display:flex;flex-direction:column;
  background:linear-gradient(160deg,{T['gold']} 0%,{T['raised']} 12%,{T['surface']} 40%,{T['deep']} 100%);box-shadow:0 30px 60px rgba(0,0,0,.5)">
  <div style="display:flex;justify-content:space-between;font-family:'Oxanium',sans-serif;font-size:14px"><span>No. 6</span><span>TWO-STICK</span></div>
  <div style="height:250px;margin-top:10px;border-radius:12px;overflow:hidden;background:{T['face']}"><img src="{B + HULL['Squirrel']}" alt="The Squirrel" style="width:100%;height:100%;object-fit:cover"></div>
  <div class="disp" style="font-size:52px;line-height:1;margin-top:14px">Squirrel</div>
  <p style="margin:10px 0 0;font-size:15px;line-height:1.5">{STORY}</p>
  <div style="margin-top:auto;display:grid;grid-template-columns:repeat(4,1fr);gap:6px;text-align:center">{"".join(f'<div style="padding:8px 2px;border-radius:8px;background:{T["ground"]}"><div style="font-family:Oxanium,sans-serif;font-size:18px">{n}</div><div style="font-size:10px;color:{T["soft"]}">{l}</div></div>' for n, l in STATS)}</div>
</div>
<div style="position:absolute;left:540px;right:80px;top:1176px;display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:14px">
  <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.cls}}}}" onClick="{{{{a.pick}}}}"><small>{{{{a.el}}}}</small><span class="ic"><img src="{{{{a.icon}}}}" alt=""></span><b>{{{{a.name}}}}</b><small style="letter-spacing:.04em;color:{T['jade']}">{{{{a.trig}}}}</small><p>{{{{a.does}}}}</p><i>{{{{a.l5}}}}</i></button></sc-for>
</div>
<div style="position:absolute;left:540px;right:80px;top:1660px;height:210px;padding:20px;border-radius:16px;background:{T['surface']};box-shadow:inset 0 0 0 1px {T['raised']}">
  {step_blocks(stage)}
</div>
<div style="position:absolute;left:80px;right:80px;top:1900px">
  <div style="display:flex;align-items:center;gap:20px;margin-bottom:12px"><span class="disp" style="font-size:24px">Its games</span><span style="flex:1"></span>{filt_row()}</div>
  <div style="display:flex;gap:14px;height:180px;overflow:hidden"><sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="8"><div class="{{{{g.cls}}}}" style="height:180px;width:210px"><img src="{{{{g.img}}}}" alt=""><span>{{{{g.label}}}}</span><b>{{{{g.name}}}}</b></div></sc-for></div>
  {NONE}
</div>
"""
    return page("Rev 68 B - Fleet Codex, Trading cards", "family=Oxanium:wght@500;600;700&amp;family=Manrope:wght@400;500;700",
                css, fleet + window, "ab", "gm", "fc")


# ======================================================================== C Flight deck
def option_c():
    br = (f'<span style="position:absolute;width:18px;height:18px;border:2px solid {T["jade"]};{{p}}"></span>')
    brackets = "".join(br.replace("{p}", p) for p in ("left:0;top:0;border-right:0;border-bottom:0", "right:0;top:0;border-left:0;border-bottom:0",
                                                       "left:0;bottom:0;border-right:0;border-top:0", "right:0;bottom:0;border-left:0;border-top:0"))
    css = f"""
.app{{font-family:'Saira',sans-serif}}
.disp{{font-family:'Saira Condensed',sans-serif;font-weight:700;text-transform:uppercase;letter-spacing:.02em}}
.fc{{position:relative;display:flex;flex-direction:column;align-items:center;gap:6px;padding:10px 6px 12px;text-decoration:none;color:{T['soft']};transition:color .3s {EASE},transform .3s {EASE}}}
.fc img{{width:100%;height:96px;object-fit:contain;transition:transform .45s {EASE},filter .45s {EASE};filter:saturate(.6) brightness(.8)}}
.fc:hover{{color:{T['text']};transform:translateY(-4px)}}
.fc:hover img,.fc.on img{{filter:none;transform:scale(1.08)}}
.fc.on{{color:{T['text']};background:linear-gradient(0deg,rgba(101,225,223,.14),transparent);box-shadow:inset 0 -2px 0 {T['jade']}}}
.fc b{{font-family:'Saira Condensed',sans-serif;font-size:17px;text-transform:uppercase;letter-spacing:.06em}}
.fc em{{font-style:normal;font-size:10px;letter-spacing:.14em;color:{T['ruby']}}}
.ab{{position:relative;width:84px;height:84px;border:0;cursor:pointer;background:rgba(5,0,3,.7);box-shadow:inset 0 0 0 1px {T['raised']};transition:transform .3s {EASE},box-shadow .3s {EASE}}}
.ab img{{width:60px;height:60px;object-fit:contain}}
.ab:hover{{transform:translateY(-4px)}}
.ab.on{{box-shadow:inset 0 0 0 2px {T['jade']};transform:translateY(-8px)}}
.gm{{display:grid;grid-template-columns:120px 1fr;gap:14px;align-items:center;padding:8px;background:rgba(5,0,3,.6);box-shadow:inset 0 0 0 1px {T['raised']};transition:transform .3s {EASE},box-shadow .3s {EASE}}}
.gm:hover{{transform:translateX(-6px);box-shadow:inset 0 0 0 1px {T['jade']}}}
.gm img{{width:120px;height:68px;object-fit:cover}}
.gm b{{display:block;font-family:'Saira Condensed',sans-serif;font-size:22px;text-transform:uppercase}}
.gm span{{font-size:12px;color:{T['soft']}}}
.hud{{position:absolute;padding:20px 22px;background:rgba(19,1,12,.82);backdrop-filter:blur(6px)}}
.glide{{animation:glide 9s ease-in-out infinite}}
@keyframes glide{{0%,100%{{transform:translate(0,0) rotate(-2deg)}}50%{{transform:translate(18px,-14px) rotate(1.5deg)}}}}
"""
    fleet = f"""
<header style="position:absolute;left:80px;right:80px;top:56px;display:flex;align-items:center;gap:20px">
  <span class="disp" style="font-size:30px;letter-spacing:.12em">Fleet Codex</span><span style="font-size:13px;letter-spacing:.2em;color:{T['soft']}">· SELECT VESSEL</span>
  <span style="flex:1"></span>{filt_row('fleetFilters')}
</header>
<div style="position:absolute;left:80px;top:130px;width:1760px;height:560px;overflow:hidden;background:radial-gradient(ellipse at 60% 55%,{T['face']},{T['ground']} 72%)">
  <img class="glide soft" src="{B + HULL['Squirrel']}" alt="The Squirrel" style="position:absolute;left:520px;top:-20px;width:1100px">
  <div style="position:absolute;left:56px;top:70px;width:520px">
    <div style="font-size:13px;letter-spacing:.24em;color:{T['jade']}">NOW SELECTED · VESSEL 6</div>
    <div class="disp" style="font-size:150px;line-height:.9">Squirrel</div>
    <div style="font-size:18px;line-height:1.5;color:{T['soft']};margin-top:12px">The racer: tube-riding along the trails it leaves behind.</div>
    <a href="#window" style="display:inline-flex;align-items:center;height:52px;padding:0 28px;margin-top:26px;background:{T['cta']};color:{T['ground']};font-family:'Saira Condensed',sans-serif;font-size:22px;font-weight:700;letter-spacing:.1em;text-decoration:none">OPEN THE VESSEL</a>
  </div>
  {brackets}
</div>
<div style="position:absolute;left:80px;right:80px;top:720px;display:grid;grid-template-columns:repeat(12,minmax(0,1fr));gap:6px">
  <sc-for list="{{{{fleet}}}}" as="v" hint-placeholder-count="23"><a href="#{{{{v.key}}}}" class="{{{{v.cls}}}}"><img src="{{{{v.img}}}}" alt=""><b>{{{{v.name}}}}</b><em>{{{{v.badge}}}}</em></a></sc-for>
</div>
"""
    stage = lambda i, k: (f'<div style="display:flex;flex-direction:column;gap:12px"><div style="display:flex;align-items:baseline;gap:12px"><span class="disp" style="font-size:34px">{k["name"]}</span><span style="color:{T["jade"]}">{k["trig"]}</span></div>'
                          f'<div style="height:150px;background:rgba(5,0,3,.7);box-shadow:inset 0 0 0 1px {T["raised"]}">{scene(SCENE_OF[i])}</div>{devices(k)}'
                          f'<div style="font-size:15px;line-height:1.5">{k["does"]}</div><div style="font-size:13px;color:{T["soft"]}">{k["scales"]} · <span style="color:{T["cta"]}">{k["l5"]}</span></div></div>')
    window = f"""
{divider("THE VESSEL WINDOW", 1112)}
<div id="window" style="position:absolute;left:80px;top:1160px;width:1760px;height:920px;overflow:hidden;background:radial-gradient(ellipse at 50% 45%,{T['face']},{T['ground']} 75%);box-shadow:inset 0 0 0 1px {T['raised']}">
  <img class="glide soft" src="{B + HULL['Squirrel']}" alt="The Squirrel" style="position:absolute;left:520px;top:120px;width:820px">
  {brackets}
  <div class="hud" style="left:40px;top:40px;width:480px">
    <div style="font-size:12px;letter-spacing:.22em;color:{T['jade']}">PILOT BRIEFING</div>
    <div class="disp" style="font-size:64px;line-height:1">Squirrel</div>
    <p style="margin:10px 0 0;font-size:16px;line-height:1.55">{STORY}</p>
    <div style="display:flex;gap:22px;margin-top:16px">{"".join(f'<div><div class="disp" style="font-size:26px">{n}</div><div style="font-size:11px;color:{T["soft"]}">{l}</div></div>' for n, l in STATS)}</div>
  </div>
  <div class="hud" style="right:40px;top:40px;width:470px;height:520px">
    <div style="display:flex;align-items:center;margin-bottom:12px"><span style="font-size:12px;letter-spacing:.22em;color:{T['jade']}">MISSIONS</span></div>
    {filt_row()}
    <div style="display:flex;flex-direction:column;gap:8px;margin-top:12px;height:390px;overflow-y:auto"><sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="5"><div class="{{{{g.cls}}}}"><img src="{{{{g.img}}}}" alt=""><div><span>{{{{g.label}}}} · {{{{g.sub}}}}</span><b>{{{{g.name}}}}</b></div></div></sc-for>{NONE}</div>
  </div>
  <div class="hud" style="left:40px;bottom:40px;width:700px">{step_blocks(stage)}</div>
  <div style="position:absolute;right:40px;bottom:40px;display:flex;gap:10px;align-items:flex-end">
    <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.cls}}}}" onClick="{{{{a.pick}}}}" aria-label="{{{{a.name}}}}"><img src="{{{{a.icon}}}}" alt=""></button></sc-for>
  </div>
</div>
"""
    return page("Rev 68 C - Fleet Codex, Flight deck", "family=Saira+Condensed:wght@500;700&amp;family=Saira:wght@400;500;600",
                css, fleet + window, "ab", "gm", "fc")


# ======================================================================== D Editorial
def option_d():
    css = f"""
.app{{font-family:'Inter',sans-serif}}
.disp{{font-family:'Space Grotesk',sans-serif;font-weight:700;letter-spacing:-.03em}}
.fc{{display:grid;grid-template-columns:34px 1fr 120px;gap:12px;align-items:center;padding:10px 0;border-bottom:1px solid {T['raised']};text-decoration:none;color:{T['text']};transition:padding .35s {EASE},color .35s {EASE}}}
.fc:hover{{padding-left:12px;color:{T['jade']}}}
.fc.on{{color:{T['jade']}}}
.fc .n{{font-family:'Space Grotesk',sans-serif;font-size:14px;color:{T['soft']}}}
.fc b{{font-family:'Space Grotesk',sans-serif;font-size:34px;font-weight:600;letter-spacing:-.02em;line-height:1}}
.fc img{{width:120px;height:52px;object-fit:cover;opacity:.0;transform:translateX(12px);transition:opacity .35s {EASE},transform .35s {EASE}}}
.fc:hover img,.fc.on img{{opacity:1;transform:none}}
.fc em{{font-style:normal;font-size:11px;letter-spacing:.14em;color:{T['ruby']};margin-left:8px;vertical-align:middle}}
.ab{{display:grid;grid-template-columns:44px 64px 1fr auto;gap:18px;align-items:center;width:100%;text-align:left;border:0;cursor:pointer;padding:14px 0;background:transparent;border-bottom:1px solid {T['raised']};transition:padding .35s {EASE}}}
.ab:hover{{padding-left:10px}}
.ab .n{{font-family:'Space Grotesk',sans-serif;font-size:14px;color:{T['soft']}}}
.ab img{{width:64px;height:64px;object-fit:contain}}
.ab b{{font-family:'Space Grotesk',sans-serif;font-size:32px;font-weight:600;letter-spacing:-.02em}}
.ab small{{font-size:13px;color:{T['soft']}}}
.ab.on b{{color:{T['jade']}}}
.gm{{display:grid;grid-template-columns:150px 1fr auto;gap:18px;align-items:center;padding:12px 0;border-bottom:1px solid {T['raised']};transition:padding .35s {EASE}}}
.gm:hover{{padding-left:10px}}
.gm img{{width:150px;height:84px;object-fit:cover}}
.gm b{{font-family:'Space Grotesk',sans-serif;font-size:26px;font-weight:600}}
.gm span{{font-size:13px;color:{T['soft']}}}
"""
    fleet = f"""
<header style="position:absolute;left:80px;right:80px;top:60px;display:flex;align-items:center;gap:24px">
  <span class="disp" style="font-size:22px">Fleet Codex</span><span style="font-size:14px;color:{T['soft']}">Issue 01 · The fleet</span><span style="flex:1"></span>{filt_row('fleetFilters')}
</header>
<div class="disp" style="position:absolute;left:80px;top:130px;font-size:170px;line-height:.9">The fleet,<br><span style="color:{T['jade']}">every hull.</span></div>
<div style="position:absolute;left:1180px;top:170px;width:660px;height:280px;overflow:hidden"><img src="{B + HULL['Squirrel']}" alt="The Squirrel" class="soft" style="width:100%;height:100%;object-fit:cover"></div>
<div style="position:absolute;left:80px;right:80px;top:500px;columns:3;column-gap:56px">
  <sc-for list="{{{{fleet}}}}" as="v" hint-placeholder-count="23"><a href="#{{{{v.key}}}}" class="{{{{v.cls}}}}" style="break-inside:avoid"><span class="n">{{{{v.id}}}}</span><span><b>{{{{v.name}}}}</b><em>{{{{v.badge}}}}</em></span><img src="{{{{v.img}}}}" alt=""></a></sc-for>
</div>
"""
    stage = lambda i, k: (f'<div style="display:grid;grid-template-columns:1fr 1fr;gap:26px;align-items:start">'
                          f'<div><div style="height:190px;background:{T["deep"]}">{scene(SCENE_OF[i])}</div><div style="margin-top:12px">{devices(k)}</div></div>'
                          f'<div><div style="font-size:13px;letter-spacing:.16em;color:{T["jade"]}">{k["el"]} · {k["trig"].upper()}</div>'
                          f'<p style="margin:10px 0 0;font-size:20px;line-height:1.5">{k["does"]}</p><p style="margin:12px 0 0;font-size:14px;color:{T["soft"]}">{k["scales"]}</p>'
                          f'<p style="margin:8px 0 0;font-size:14px;color:{T["cta"]}">{k["l5"]}</p></div></div>')
    window = f"""
{divider("THE VESSEL WINDOW", 1112)}
<div style="position:absolute;left:80px;top:1150px;width:760px">
  <div style="font-size:14px;letter-spacing:.2em;color:{T['soft']}">No. 6 · THE RACER</div>
  <div class="disp" style="font-size:190px;line-height:.85;margin-top:6px">Squirrel</div>
  <blockquote style="margin:24px 0 0;padding:0;font-family:'Space Grotesk',sans-serif;font-size:28px;line-height:1.35;font-weight:500">&#8220;{STORY}&#8221;</blockquote>
  <div style="display:flex;gap:36px;margin-top:22px">{"".join(f'<div><div class="disp" style="font-size:36px">{n}</div><div style="font-size:12px;color:{T["soft"]}">{l}</div></div>' for n, l in STATS)}</div>
</div>
<div style="position:absolute;left:900px;right:80px;top:1150px">
  <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.cls}}}}" onClick="{{{{a.pick}}}}"><span class="n">{{{{a.el}}}}</span><img src="{{{{a.icon}}}}" alt=""><b>{{{{a.name}}}}</b><small>{{{{a.trig}}}}</small></button></sc-for>
</div>
<div style="position:absolute;left:900px;right:80px;top:1640px">{step_blocks(stage)}</div>
<div style="position:absolute;left:80px;top:1700px;width:760px">
  <div style="display:flex;align-items:center;gap:16px;margin-bottom:8px"><span class="disp" style="font-size:32px">Where it plays</span></div>
  {filt_row()}
  <div style="margin-top:8px;height:330px;overflow:hidden"><sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="4"><div class="{{{{g.cls}}}}"><img src="{{{{g.img}}}}" alt=""><b>{{{{g.name}}}}</b><span>{{{{g.label}}}} · {{{{g.sub}}}}</span></div></sc-for>{NONE}</div>
</div>
"""
    return page("Rev 68 D - Fleet Codex, Editorial", "family=Space+Grotesk:wght@500;600;700&amp;family=Inter:wght@400;500",
                css, fleet + window, "ab", "gm", "fc")


# ======================================================================== notes
def notes():
    rows = [("A Showroom (recommended)", "Unbounded + Sora", "The fleet as lit hull cards, six across; the window puts the vessel, its five abilities and the picked one's move side by side, so the move plays the moment you pick, with no scroll. Recommended: the clearest website page, and the move is the biggest thing on it."),
            ("B Trading cards", "Oxanium + Manrope", "Every hull a collectible card with its number; the abilities are five cards that rise when picked, the move plays in a strip under them. The most playful; the most to read."),
            ("C Flight deck", "Saira Condensed + Saira", "Reads like the game: a big selected hull on the fleet page, the window as a HUD with the four-icon row bottom right, the briefing, missions and the move as glass panels over the flying hull."),
            ("D Editorial", "Space Grotesk + Inter", "A magazine: the fleet as a big-type index where the hull slides in on hover; the window as a spread with the story as a pull quote and each ability a line.")]
    body = "".join(f'<div style="display:grid;grid-template-columns:330px 260px 1fr;gap:24px;padding:16px 0;border-bottom:1px solid {T["raised"]}">'
                   f'<b style="font-size:20px">{a}</b><span style="color:{T["jade"]}">{f}</span><span style="color:{T["text"]};line-height:1.5">{d}</span></div>'
                   for a, f, d in rows)
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Rev 68 - notes</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link href="https://fonts.googleapis.com/css2?family=Sora:wght@400;600&amp;display=swap" rel="stylesheet">
<style>body{{margin:0}}</style>
</helmet>
<div style="width:1920px;height:900px;box-sizing:border-box;padding:56px 80px;background:{T['ground']};color:{T['text']};font-family:'Sora',sans-serif;font-size:16px">
  <div style="font-size:13px;letter-spacing:.2em;color:{T['soft']}">REV 68 · NOTES</div>
  <div style="font-size:38px;font-weight:600;margin-top:6px">The Fleet Codex for the website, four looks</div>
  <p style="margin:14px 0 0;max-width:1500px;line-height:1.55;color:{T['soft']}">Asked: the move should not be below the fold, nicer type, every animation smooth, game filters and the vessel's story. In all four, picking an ability plays its move beside it: a small scene of it happening, with the controller button, key and phone touch pressing on the same 3.2 s clock (passive abilities show the thumbs just flying). Only transform and opacity move, on an eased curve. Games filter by All, each element, Arcade and Arena. The story is the Squirrel's class asset text. Colours: the Nocturne set; the other two sets swap in as themes.</p>
  <div style="margin-top:22px">{body}</div>
  <p style="margin:20px 0 0;color:{T['soft']}">Type departs from the style guide (Aldrich and Chakra Petch) on purpose: the pick decides the codex's type. A longer story per vessel needs a writer; the boards use only what the class asset says.</p>
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1920,"height":900}}}}'>
class Component extends DCLogic {{ renderVals() {{ return {{}}; }} }}
</script>
</body>
</html>
"""


for name, html in (("R68A", option_a()), ("R68B", option_b()), ("R68C", option_c()), ("R68D", option_d()), ("R68Notes", notes())):
    (ROOT / f"{name}.dc.html").write_text(html, encoding="utf-8")
    print(f"wrote {name}.dc.html ({len(html)} bytes)")
