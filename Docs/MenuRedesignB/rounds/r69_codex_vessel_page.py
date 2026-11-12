#!/usr/bin/env python3
"""Rev 69 boards: the Fleet Codex vessel page, option C (Flight deck) in full (Shombith, 2026-11-08).

Asked, picking Rev 68 C: "I like C can you show me whole page how it would look on the mockup before building?", then
"Not all vessels but just how with all details it would look because earlier one was pretty detailed too but noisy".

So: ONE vessel's page (the Squirrel), every fact the live codex shows - the five abilities with what each element
scales (the authored numbers), the level-5 upgrades and whether each is wired, every control on every device, the flight
numbers, the games by element with filters, the docs and the recent changes - laid out calm: C's flight-deck window on
top, then quiet sections divided by hairlines, one accent (the Jade selection) and Lime only on the one call to action.
The fleet is a switcher in the top bar, not a grid. Two boards: the desktop page (1920 wide) and the same page at phone
width (390), since it goes on the website.

Reuses Rev 68's kit (the scenes, the devices, the Nocturne tokens, the art ids) from r68_fleet_codex.py.

    python3 Docs/MenuRedesignB/rounds/r69_codex_vessel_page.py <root> <fleet.json>   # writes <root>/project/R69*.dc.html
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import r68_fleet_codex as k  # noqa: E402  (reads sys.argv[1:3]; writes nothing on import)

T, B, EASE = k.T, k.B, k.EASE
ROOT = k.ROOT
SQ = k.SQ
ELS = k.ELEMENTS

# ---------------------------------------------------------------- every detail, from fleet.json
def num(x):
    return ("%g" % round(x, 2)) if x is not None else ""


DETAIL = []
for i, el in enumerate(ELS + ["Omni"]):
    base = dict(k.KIT[i])
    if el == "Omni":
        base.update({"dials": [], "l5name": "No level 5", "l5desc": "The omni crystal has no level-5 upgrade.", "l5state": "", "l5tag": "LEVEL 5", "more": "OMNI CRYSTAL"})
    else:
        e = SQ["elements"][el]
        u = e["upgrade"]
        dials = []
        for s in e["scaling"]:
            if s.get("field") and s.get("min") is not None:
                where = (s.get("asset") or s.get("script") or "").split("/")[-1]
                dials.append({"txt": f'{s["field"]}  x{num(s["min"])} to x{num(s["max"])}' + ("" if s["on"] else "  (switched off)"),
                              "src": where, "cls": "dial" if s["on"] else "dial off"})
        base.update({"dials": dials, "l5name": u["label"], "l5desc": u["description"],
                     "l5state": "WIRED" if u["status"] == "wired" else u["status"].upper(), "more": "MORE " + el.upper()})
        base["l5tag"] = "LEVEL 5 · " + base["l5state"]
    DETAIL.append(base)

EGAMES = []
for el in ELS:
    g = next((x for x in k.GAMES if x["kind"] == "Arcade" and x["els"] and x["els"][0] == el), None)
    EGAMES.append({"el": el, "mark": B + k.MARK[el], "name": g["name"] if g else "None yet", "cls": "eg" if g else "eg none"})

CTRL = [("Steer", "Both sticks", "W A S D + P ; L '", "Two thumbs"),
        ("Boost Ring", "RT", "R-Shift", "Lift left thumb"),
        ("Crystal Joust, Steal, Skimming", "No button: fly", "No button: fly", "No button: fly"),
        ("Shield Ring", "Fly into an omni crystal", "Fly into an omni crystal", "Fly into an omni crystal")]
NOTES = SQ["controls"]["notes"]
DOCS = [d["path"].split("/")[-1] for d in SQ["docs"]][:5]
CHANGES = [(c["date"], c["subject"][:88] + ("..." if len(c["subject"]) > 88 else "")) for c in SQ["changes"]][:5]
F = SQ["flight"]
FLIGHT = [(num(F["cruise"]), "u/s cruise"), (f'{num(F["pitch"])} / {num(F["yaw"])}', "pitch / yaw deg/s"),
          (num(F["minimum"]), "minimum speed"), (f'x{num(F["boost"])}', "boost")]
NEIGHBOURS = ("Grizzly", "Serpent")   # the fleet's order (VesselClassType): the switcher's previous and next

LOGIC = r"""
class Component extends DCLogic {
  constructor(props) { super(props); this.state = { sel: 1, filt: 'All' }; }
  renderVals() {
    const K = __DETAIL__, G = __GAMES__;
    const sel = this.state.sel, filt = this.state.filt;
    const abilities = K.map((a, i) => Object.assign({}, a, { cls: i === sel ? 'ab on' : 'ab', rowCls: i === sel ? 'row on' : 'row', pick: () => this.setState({ sel: i }) }));
    const games = G.filter(g => filt === 'All' || g.kind === filt || g.els.indexOf(filt) >= 0);
    const filters = ['All', 'Charge', 'Mass', 'Space', 'Time', 'Arcade', 'Arena'].map(f => ({ label: f, cls: f === filt ? 'chip on' : 'chip', pick: () => this.setState({ filt: f }) }));
    return { abilities, games, filters, none: games.length === 0, noneText: 'No ' + filt + ' game seats the Squirrel yet.',
      cur: K[sel], s0: sel === 0, s1: sel === 1, s2: sel === 2, s3: sel === 3, s4: sel === 4 };
  }
}
"""


def logic():
    return LOGIC.replace("__DETAIL__", json.dumps(DETAIL, ensure_ascii=False)).replace("__GAMES__", json.dumps(k.GAMES, ensure_ascii=False))


CSS = f"""
body{{margin:0;background:{T['ground']}}}
.app{{position:relative;overflow:hidden;color:{T['text']};background:{T['ground']};font-family:'Saira',sans-serif}}
.app *{{box-sizing:border-box}}
button{{font:inherit;color:inherit}}
button:focus-visible,a:focus-visible{{outline:2px solid {T['jade']};outline-offset:2px}}
.disp{{font-family:'Saira Condensed',sans-serif;font-weight:700;text-transform:uppercase;letter-spacing:.02em}}
.lab{{font-size:12px;letter-spacing:.22em;color:{T['soft']}}}
.soft{{-webkit-mask-image:radial-gradient(ellipse 58% 62% at 50% 50%,#000 52%,transparent 100%);mask-image:radial-gradient(ellipse 58% 62% at 50% 50%,#000 52%,transparent 100%)}}
.hud{{position:absolute;padding:22px 24px;background:rgba(19,1,12,.84);backdrop-filter:blur(6px)}}
.glide{{animation:glide 9s ease-in-out infinite}}
@keyframes glide{{0%,100%{{transform:translate(0,0) rotate(-2deg)}}50%{{transform:translate(18px,-14px) rotate(1.5deg)}}}}
.chip{{border:0;cursor:pointer;height:34px;padding:0 14px;background:transparent;color:{T['soft']};box-shadow:inset 0 0 0 1px {T['raised']};font-size:13px;letter-spacing:.06em;transition:color .2s {EASE},background .2s {EASE}}}
.chip:hover{{color:{T['text']}}}
.chip.on{{color:{T['ground']};background:{T['jade']};box-shadow:none}}
.ab{{display:flex;flex-direction:column;align-items:center;gap:6px;width:96px;border:0;cursor:pointer;background:transparent;padding:0;transition:transform .3s {EASE}}}
.ab span.i{{width:84px;height:84px;display:flex;align-items:center;justify-content:center;background:rgba(5,0,3,.72);box-shadow:inset 0 0 0 1px {T['raised']};transition:box-shadow .3s {EASE}}}
.ab img{{width:58px;height:58px;object-fit:contain}}
.ab small{{font-size:12px;color:{T['soft']};text-align:center;line-height:1.2}}
.ab:hover{{transform:translateY(-4px)}}
.ab.on{{transform:translateY(-8px)}}
.ab.on span.i{{box-shadow:inset 0 0 0 2px {T['jade']}}}
.ab.on small{{color:{T['text']}}}
.sec{{position:absolute;left:80px;right:80px;display:flex;align-items:center;gap:18px}}
.sec .disp{{font-size:30px}}
.sec .rule{{flex:1;height:1px;background:{T['raised']}}}
.row{{display:grid;grid-template-columns:400px 1fr 380px 330px;gap:36px;align-items:start;width:100%;text-align:left;border:0;cursor:pointer;padding:26px 0 26px 20px;background:transparent;border-bottom:1px solid rgba(112,64,95,.6);transition:background .3s {EASE},box-shadow .3s {EASE}}}
.row:hover{{background:rgba(60,29,49,.35)}}
.row.on{{background:rgba(60,29,49,.55);box-shadow:inset 3px 0 0 {T['jade']}}}
.row .who{{display:grid;grid-template-columns:72px 1fr;gap:16px;align-items:center}}
.row .who img{{width:72px;height:72px;object-fit:contain;background:{T['deep']};padding:6px}}
.row b{{display:block;font-family:'Saira Condensed',sans-serif;font-size:30px;text-transform:uppercase;line-height:1}}
.row .trig{{font-size:13px;color:{T['jade']}}}
.row p{{margin:0;font-size:16px;line-height:1.55}}
.row .k{{font-size:11px;letter-spacing:.2em;color:{T['soft']};margin-bottom:6px}}
.dial{{display:block;font-size:13px;color:{T['text']};margin-top:6px}}
.dial i{{display:block;font-style:normal;font-size:11px;color:{T['off']}}}
.dial.off{{color:{T['off']};text-decoration:line-through}}
.l5 b{{font-size:22px}}
.pill{{display:inline-block;margin-top:8px;font-size:11px;letter-spacing:.16em;padding:2px 8px;color:{T['cta']};box-shadow:inset 0 0 0 1px {T['cta']}}}
.gm{{position:relative;height:210px;display:flex;flex-direction:column;justify-content:flex-end;padding:16px;overflow:hidden;transition:transform .35s {EASE}}}
.gm:hover{{transform:translateY(-4px)}}
.gm img{{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transition:transform .6s {EASE}}}
.gm:hover img{{transform:scale(1.05)}}
.gm::after{{content:"";position:absolute;inset:0;background:linear-gradient(0deg,{T['ground']} 8%,transparent 72%)}}
.gm>*:not(img){{position:relative;z-index:1}}
.gm b{{font-family:'Saira Condensed',sans-serif;font-size:26px;text-transform:uppercase;line-height:1.05}}
.gm span{{font-size:12px;color:{T['soft']}}}
.eg{{display:grid;grid-template-columns:18px 70px 1fr;gap:10px;align-items:center;padding:8px 0;border-bottom:1px solid rgba(112,64,95,.5);font-size:15px}}
.eg img{{width:16px;height:16px}}
.eg span{{font-size:11px;letter-spacing:.16em;color:{T['soft']}}}
.eg.none{{color:{T['off']}}}
.tbl{{display:grid;grid-template-columns:260px repeat(3,1fr);font-size:15px}}
.tbl div{{padding:12px 0;border-bottom:1px solid rgba(112,64,95,.5)}}
.tbl .h{{font-size:11px;letter-spacing:.2em;color:{T['soft']}}}
.lst{{margin:0;padding:0;list-style:none}}
.lst li{{display:flex;gap:16px;padding:11px 0;border-bottom:1px solid rgba(112,64,95,.5);font-size:15px}}
.lst li span{{flex:none;color:{T['soft']};font-size:13px;width:96px}}
.lst a{{color:{T['text']};text-decoration:none}}
.lst a:hover{{color:{T['jade']}}}
.top a{{color:{T['soft']};text-decoration:none}}
.top a:hover{{color:{T['text']}}}
{k.SCENE_CSS}
{k.DEVICE_CSS}
{k.SEA_CSS}
"""

ARROW_L = '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M10 3L5 8l5 5"/></svg>'
ARROW_R = '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M6 3l5 5-5 5"/></svg>'
CARET = '<svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M3 4.5l3 3 3-3"/></svg>'
SEARCH = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></svg>'


def head(title, fonts, w, h, body):
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
<style>{CSS}</style>
</helmet>
<div class="app" style="width:{w}px;height:{h}px">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic()}
</script>
</body>
</html>
"""


FONTS = "family=Saira+Condensed:wght@500;700&amp;family=Saira:wght@400;500;600"


def filt_row():
    return k.filt_row()


def move_panel(width_scene):
    return k.step_blocks(lambda i, a: (
        f'<div style="display:flex;flex-direction:column;gap:12px">'
        f'<div style="display:flex;align-items:baseline;gap:12px"><span class="disp" style="font-size:34px">{a["name"]}</span><span style="color:{T["jade"]}">{a["trig"]}</span></div>'
        f'<div style="height:{width_scene}px;background:rgba(5,0,3,.72);box-shadow:inset 0 0 0 1px {T["raised"]}">{k.scene(k.SCENE_OF[i])}</div>'
        f'{k.devices(a)}</div>'))


# ======================================================================== desktop
def desktop():
    W, H = 1920, 3400
    top = f"""
<header class="top" style="position:absolute;left:80px;right:80px;top:28px;height:52px;display:flex;align-items:center;gap:26px">
  <span class="disp" style="font-size:28px;letter-spacing:.12em">Fleet Codex</span>
  <span style="flex:1"></span>
  <a href="#Grizzly" aria-label="Previous vessel: Grizzly" style="display:flex;align-items:center;gap:6px;font-size:13px">{ARROW_L} Grizzly</a>
  <button type="button" style="display:flex;align-items:center;gap:10px;height:44px;padding:0 16px;border:0;cursor:pointer;background:{T['surface']};box-shadow:inset 0 0 0 1px {T['raised']}"><img src="{B + k.HULL['Squirrel']}" alt="" style="width:46px;height:26px;object-fit:cover"><span class="disp" style="font-size:22px">Squirrel</span><span style="font-size:12px;color:{T['soft']}">6 of 23</span>{CARET}</button>
  <a href="#Serpent" aria-label="Next vessel: Serpent" style="display:flex;align-items:center;gap:6px;font-size:13px">Serpent {ARROW_R}</a>
  <span style="flex:1"></span>
  <label style="display:flex;align-items:center;gap:8px;height:40px;width:260px;padding:0 14px;color:{T['soft']};box-shadow:inset 0 0 0 1px {T['raised']}">{SEARCH}<input aria-label="Search the fleet" placeholder="Search vessels, abilities, games" style="all:unset;font-size:13px;width:100%;color:{T['text']}"></label>
  <button type="button" style="display:flex;align-items:center;gap:8px;height:40px;padding:0 12px;border:0;cursor:pointer;background:transparent;box-shadow:inset 0 0 0 1px {T['raised']};font-size:13px"><span style="display:flex;gap:2px">{"".join(f'<i style="width:8px;height:14px;background:{c}"></i>' for c in (T['jade'], T['ruby'], T['gold'], T['blue']))}</span>Nocturne {CARET}</button>
  <span style="display:flex;gap:6px" role="radiogroup" aria-label="Domain">{"".join(f'<button type="button" role="radio" aria-checked="{str(n == "Jade").lower()}" aria-label="{n}" style="width:26px;height:26px;border:0;cursor:pointer;background:{c};transform:rotate(45deg) scale(.72);{"box-shadow:0 0 0 3px " + T["ground"] + ",0 0 0 5px " + c if n == "Jade" else ""}"></button>' for n, c in (("Jade", T['jade']), ("Ruby", T['ruby']), ("Gold", T['gold'])))}</span>
</header>"""
    window = f"""
<div style="position:absolute;left:80px;top:112px;width:1760px;height:860px;overflow:hidden;background:radial-gradient(ellipse at 52% 46%,{T['face']},{T['ground']} 74%);box-shadow:inset 0 0 0 1px {T['raised']}">
  <img class="glide soft" src="{B + k.HULL['Squirrel']}" alt="The Squirrel in the Jade domain's livery" style="position:absolute;left:470px;top:70px;width:900px">
  <div class="hud" style="left:40px;top:40px;width:500px">
    <div class="lab" style="color:{T['jade']}">VESSEL 6 · TWO-STICK · IN THE VESSEL CHANGER</div>
    <div class="disp" style="font-size:96px;line-height:.95;margin-top:6px">Squirrel</div>
    <p style="margin:12px 0 0;font-size:17px;line-height:1.55">{k.STORY}</p>
    <div style="display:flex;gap:26px;margin-top:18px">{"".join(f'<div><div class="disp" style="font-size:30px">{n}</div><div style="font-size:11px;color:{T["soft"]}">{l}</div></div>' for n, l in k.STATS)}</div>
  </div>
  <div class="hud" style="right:40px;top:40px;width:400px">
    <div class="lab">ITS ELEMENT GAMES</div>
    <div style="margin-top:8px">{"".join(f'<div class="{g["cls"]}"><img src="{g["mark"]}" alt=""><span>{g["el"].upper()}</span>{g["name"]}</div>' for g in EGAMES)}</div>
    <a href="#missions" style="display:inline-flex;align-items:center;height:46px;padding:0 22px;margin-top:18px;background:{T['cta']};color:{T['ground']};font-family:'Saira Condensed',sans-serif;font-size:20px;font-weight:700;letter-spacing:.1em;text-decoration:none">PLAY THE SQUIRREL</a>
  </div>
  <div class="hud" style="left:40px;bottom:40px;width:640px">{move_panel(150)}</div>
  <div style="position:absolute;right:40px;bottom:40px;display:flex;gap:12px;align-items:flex-end">
    <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.cls}}}}" onClick="{{{{a.pick}}}}"><span class="i"><img src="{{{{a.icon}}}}" alt=""></span><small>{{{{a.name}}}}</small></button></sc-for>
  </div>
</div>"""
    abil = f"""
<div class="sec" style="top:1030px"><span class="disp">Abilities</span><span class="lab">FOUR ELEMENTS AND THE OMNI CRYSTAL · PICK ONE TO PLAY ITS MOVE ABOVE</span><span class="rule"></span></div>
<div style="position:absolute;left:80px;right:80px;top:1090px">
  <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.rowCls}}}}" onClick="{{{{a.pick}}}}">
    <span class="who"><img src="{{{{a.icon}}}}" alt=""><span><span class="k">{{{{a.el}}}}</span><b>{{{{a.name}}}}</b><span class="trig">{{{{a.trig}}}}</span></span></span>
    <span><span class="k" style="display:block">WHAT IT DOES</span><p>{{{{a.does}}}}</p></span>
    <span><span class="k" style="display:block">{{{{a.more}}}}</span><p style="font-size:15px">{{{{a.scales}}}}</p><sc-for list="{{{{a.dials}}}}" as="d" hint-placeholder-count="1"><span class="{{{{d.cls}}}}">{{{{d.txt}}}}<i>{{{{d.src}}}}</i></span></sc-for></span>
    <span class="l5"><span class="k" style="display:block">LEVEL 5</span><b style="font-family:'Saira Condensed',sans-serif;text-transform:uppercase">{{{{a.l5name}}}}</b><p style="font-size:14px;color:{T['soft']};margin-top:4px">{{{{a.l5desc}}}}</p><sc-if value="{{{{a.l5state}}}}" hint-placeholder-val="{{{{true}}}}"><span class="pill">{{{{a.l5state}}}}</span></sc-if></span>
  </button></sc-for>
</div>"""
    missions = f"""
<div class="sec" id="missions" style="top:2060px"><span class="disp">Missions</span><span class="lab">EVERY GAME THAT SEATS IT</span><span class="rule"></span>{filt_row()}</div>
<div style="position:absolute;left:80px;right:80px;top:2124px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px">
  <sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="8"><div class="gm"><img src="{{{{g.img}}}}" alt=""><span><img src="{{{{g.mark}}}}" alt="" style="position:static;width:13px;height:13px;vertical-align:-2px"> {{{{g.label}}}} · {{{{g.sub}}}}</span><b>{{{{g.name}}}}</b></div></sc-for>
</div>
<div style="position:absolute;left:80px;top:2124px;width:600px">{k.NONE}</div>"""
    flight = f"""
<div class="sec" style="top:2620px"><span class="disp">Flight and controls</span><span class="rule"></span></div>
<div style="position:absolute;left:80px;top:2690px;width:420px;display:grid;grid-template-columns:1fr 1fr;gap:26px 30px">
  {"".join(f'<div><div class="disp" style="font-size:44px">{n}</div><div style="font-size:13px;color:{T["soft"]}">{l}</div></div>' for n, l in FLIGHT)}
</div>
<div style="position:absolute;left:580px;right:80px;top:2680px">
  <div class="tbl"><div class="h">ACTION</div><div class="h">CONTROLLER</div><div class="h">KEYBOARD</div><div class="h">PHONE</div>
  {"".join(f'<div>{a}</div><div>{p}</div><div>{kk}</div><div>{t}</div>' for a, p, kk, t in CTRL)}</div>
  <ul style="margin:14px 0 0;padding-left:18px;font-size:13px;line-height:1.7;color:{T['soft']}">{"".join(f"<li>{n}</li>" for n in NOTES)}</ul>
</div>"""
    docs = f"""
<div class="sec" style="top:3030px"><span class="disp">Docs and changes</span><span class="rule"></span></div>
<div style="position:absolute;left:80px;top:3090px;width:760px"><div class="lab">DOCS</div><ul class="lst" style="margin-top:6px">{"".join(f'<li><span>doc</span><a href="#">{d}</a></li>' for d in DOCS)}</ul></div>
<div style="position:absolute;left:920px;right:80px;top:3090px"><div class="lab">RECENT CHANGES</div><ul class="lst" style="margin-top:6px">{"".join(f'<li><span>{dt}</span><a href="#">{s}</a></li>' for dt, s in CHANGES)}</ul></div>
<div style="position:absolute;left:80px;right:80px;top:3350px;font-size:12px;color:{T['off']}">Read from the game's own files every day: the ability maps, the vessel prefabs, the class assets and the game cards.</div>"""
    return head("Rev 69 C - Fleet Codex vessel page, desktop", FONTS, W, H, k.SEA.replace('viewBox="0 0 1920 2160"', 'viewBox="0 0 1920 3400"') + top + window + abil + missions + flight + docs)


# ======================================================================== phone
def phone():
    W, H = 390, int(__import__('os').environ.get('PHONE_H', '4040'))
    pcss = f"""<style>
.ph-app .row{{grid-template-columns:1fr;gap:12px;padding:20px 0 20px 14px}}
.ph-app .row .who{{grid-template-columns:56px 1fr}}.ph-app .row .who img{{width:56px;height:56px}}
.ph-app .row b{{font-size:24px}}.ph-app .row p{{font-size:14px}}
.ph-app .ab{{width:62px}}.ph-app .ab span.i{{width:58px;height:58px}}.ph-app .ab img{{width:40px;height:40px}}.ph-app .ab small{{font-size:10px}}
.ph-app .dv{{padding:8px 4px}}.ph-app .dc{{font-size:11px;white-space:normal}}.ph-app .kc{{min-width:54px;font-size:11px}}
.ph-app .gm{{height:150px}}
</style>"""
    body = f"""{pcss}
<div class="ph-app" style="position:absolute;inset:0">
<header class="top" style="position:absolute;left:16px;right:16px;top:14px;height:44px;display:flex;align-items:center;gap:10px">
  <span class="disp" style="font-size:18px;letter-spacing:.1em">Fleet Codex</span><span style="flex:1"></span>
  <button type="button" aria-label="Search" style="width:44px;height:44px;border:0;background:transparent;color:{T['soft']};box-shadow:inset 0 0 0 1px {T['raised']}">{SEARCH}</button>
  <button type="button" aria-label="Theme: Nocturne" style="width:44px;height:44px;border:0;background:transparent;box-shadow:inset 0 0 0 1px {T['raised']};display:flex;align-items:center;justify-content:center;gap:2px">{"".join(f'<i style="width:5px;height:12px;background:{c}"></i>' for c in (T['jade'], T['ruby'], T['gold'], T['blue']))}</button>
</header>
<main style="position:absolute;left:16px;right:16px;top:70px;display:flex;flex-direction:column;gap:22px">
<div style="height:48px;display:flex;align-items:center;gap:8px">
  <a href="#Grizzly" aria-label="Previous vessel: Grizzly" style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{T['soft']}">{ARROW_L}</a>
  <button type="button" style="flex:1;height:44px;display:flex;align-items:center;justify-content:center;gap:8px;border:0;background:{T['surface']};box-shadow:inset 0 0 0 1px {T['raised']}"><span class="disp" style="font-size:20px">Squirrel</span><span style="font-size:11px;color:{T['soft']}">6 of 23</span>{CARET}</button>
  <a href="#Serpent" aria-label="Next vessel: Serpent" style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{T['soft']}">{ARROW_R}</a>
</div>
<div style="position:relative;margin:0 -16px;height:260px;overflow:hidden;background:radial-gradient(ellipse at 50% 50%,{T['face']},{T['ground']} 72%)">
  <img class="glide soft" src="{B + k.HULL['Squirrel']}" alt="The Squirrel" style="position:absolute;left:-20px;top:10px;width:430px">
</div>
<div style="">
  <div class="lab" style="color:{T['jade']};font-size:11px">VESSEL 6 · TWO-STICK</div>
  <div class="disp" style="font-size:60px;line-height:.95">Squirrel</div>
  <p style="margin:10px 0 0;font-size:15px;line-height:1.55">{k.STORY}</p>
  <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:6px;margin-top:14px">{"".join(f'<div><div class="disp" style="font-size:24px">{n}</div><div style="font-size:10px;color:{T["soft"]}">{l}</div></div>' for n, l in k.STATS)}</div>
  <a href="#missions" style="display:flex;align-items:center;justify-content:center;height:48px;margin-top:16px;background:{T['cta']};color:{T['ground']};font-family:'Saira Condensed',sans-serif;font-size:20px;font-weight:700;letter-spacing:.1em;text-decoration:none">PLAY THE SQUIRREL</a>
</div>
<div style="display:flex;justify-content:space-between">
  <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.cls}}}}" onClick="{{{{a.pick}}}}"><span class="i"><img src="{{{{a.icon}}}}" alt=""></span><small>{{{{a.name}}}}</small></button></sc-for>
</div>
<div style="padding:16px;background:rgba(60,29,49,.5);box-shadow:inset 0 0 0 1px {T['raised']}">{move_panel(130)}</div>
<div style=""><div class="lab" style="font-size:11px">ABILITIES</div>
  <sc-for list="{{{{abilities}}}}" as="a" hint-placeholder-count="5"><button type="button" class="{{{{a.rowCls}}}}" onClick="{{{{a.pick}}}}">
    <span class="who"><img src="{{{{a.icon}}}}" alt=""><span><span class="k">{{{{a.el}}}}</span><b>{{{{a.name}}}}</b><span class="trig">{{{{a.trig}}}}</span></span></span>
    <p>{{{{a.does}}}}</p>
    <span><span class="k" style="display:block">{{{{a.more}}}}</span><p style="font-size:13px">{{{{a.scales}}}}</p><sc-for list="{{{{a.dials}}}}" as="d" hint-placeholder-count="1"><span class="{{{{d.cls}}}}">{{{{d.txt}}}}</span></sc-for></span>
    <span class="l5"><span class="k" style="display:block">{{{{a.l5tag}}}}</span><b style="font-family:'Saira Condensed',sans-serif;text-transform:uppercase;font-size:20px">{{{{a.l5name}}}}</b><p style="font-size:13px;color:{T['soft']};margin-top:4px">{{{{a.l5desc}}}}</p></span>
  </button></sc-for>
</div>
<div id="missions">
  <div class="lab" style="font-size:11px;margin-bottom:10px">MISSIONS</div>{filt_row()}
  <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:12px"><sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="8"><div class="gm"><img src="{{{{g.img}}}}" alt=""><span>{{{{g.label}}}}</span><b style="font-size:20px">{{{{g.name}}}}</b></div></sc-for></div>
  {k.NONE}
</div>
<div style="">
  <div class="lab" style="font-size:11px">FLIGHT</div>
  <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:6px;margin-top:8px">{"".join(f'<div><div class="disp" style="font-size:20px">{n}</div><div style="font-size:10px;color:{T["soft"]}">{l}</div></div>' for n, l in FLIGHT)}</div>
  <div style="margin-top:16px;font-size:12px;color:{T['off']}">Controls for every device, docs and recent changes open below.</div>
</div>
</main>
</div>"""
    return head("Rev 69 C - Fleet Codex vessel page, phone", FONTS, W, H, body)


if __name__ == "__main__":
    for name, html in (("R69Desktop", desktop()), ("R69Phone", phone())):
        (ROOT / f"{name}.dc.html").write_text(html, encoding="utf-8")
        print(f"wrote {name}.dc.html ({len(html)} bytes)")
