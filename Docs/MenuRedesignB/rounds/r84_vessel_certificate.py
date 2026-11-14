#!/usr/bin/env python3
"""Rev 84 boards: a flight certificate and element tiers for the solo vessels (Shombith, 2026-11-10).

Asked: "Okay now we want to do a completion certificate for the vessels in the game and also make a tier system based on
their specific elemental system. First part the completion, vessels get completion percentage or minimum viable pass or
arena ready progression lets set on a name you can suggest too, so it will depend if the vessel has all of its abilities
added, l5 upgrades figured out and also time games completed that's minimum and then there is mass-space(vessel can have
either one and be ready) and charge games. Then their tier system like vessels which are really good in time games have
S tier time game and least have C tier, you can confirm each vessel from me. These only apply to solo vessels not crew
do not touch them. Create mockups first before implementing"

The certificate is FIVE checks, each worth 20 %, read from the Fleet Codex data (never typed):
  1 Abilities      every element's ability is in the map (no open slot)
  2 Level 5        5 % per wired upgrade (wired = named and gated)
  3 Time game      an arcade card whose element is Time seats the vessel
  4 Mass or Space  an arcade card whose element is Mass or Space
  5 Charge game    an arcade card whose element is Charge
Stages: LAUNCH READY = 1 + 2 (all four) + 3, the minimum; FLEET READY = that + 4; ARENA READY = all five.
Tiers (S, A, B, C per element) are a person's call: the letters on these boards are PLACEHOLDERS until Shombith rates
each vessel (the notes board carries the sheet). Crews and vessels without an ability map are left out.

    python3 Docs/MenuRedesignB/rounds/r84_vessel_certificate.py <root> <fleet.json>   # writes <root>/project/R84*.dc.html
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import r68_fleet_codex as k  # noqa: E402  (reads sys.argv[1:3]; writes nothing on import)

T, B, EASE, ROOT = k.T, k.B, k.EASE, k.ROOT
ELS = k.ELEMENTS
FJ = k.FLEET_JSON
FONTS = "family=Saira+Condensed:wght@500;700&amp;family=Saira:wght@400;500;600"
CHECKS = ["Abilities", "Level 5", "Time game", "Mass or Space", "Charge game"]
TIERS = ["S", "A", "B", "C"]


# ---------------------------------------------------------------- the certificate, from fleet.json
def certify(v):
    games = {e: [] for e in ELS}
    for c in v["cards"]:
        es = c.get("elements") or []
        arcade = "ArcadeGames" in (c.get("rosters") or []) or not c.get("rosters")
        if es and arcade:
            games[es[0]].append(c["name"])
    els = v["elements"]
    abil = all(e in els and not els[e].get("open") for e in ELS)
    wired = sum(1 for e in ELS if e in els and els[e]["upgrade"]["status"] == "wired")
    time_, ms, charge = bool(games["Time"]), bool(games["Mass"] or games["Space"]), bool(games["Charge"])
    pct = 20 * abil + 5 * wired + 20 * time_ + 20 * ms + 20 * charge
    launch = abil and wired == 4 and time_
    stage = "Arena Ready" if launch and ms and charge else "Fleet Ready" if launch and ms else "Launch Ready" if launch else "In the hangar"
    done = [abil, wired == 4, time_, ms, charge]
    detail = ["All four" if abil else "Open slot",
              f"{wired} of 4 wired",
              ", ".join(games["Time"]) or "None yet",
              ", ".join(games["Mass"] + games["Space"]) or "None yet",
              ", ".join(games["Charge"]) or "None yet"]
    nxt = next((CHECKS[i] for i, d in enumerate(done) if not d), "")
    return {"pct": pct, "stage": stage, "done": done, "detail": detail, "wired": wired, "games": games,
            "next": ("Next: " + ("the level-5 upgrades" if nxt == "Level 5" else "a " + nxt if nxt in ("Mass or Space",) else "its " + nxt.lower())) if nxt else "Certified for the arena"}


# Placeholder tiers, ONLY so the boards show the design: a letter where the vessel has a game of that element, a dash
# where it has none. Shombith rates every vessel before anything is built (the notes board's sheet).
def placeholder_tier(key, el, has):
    if not has:
        return "-"
    return TIERS[(sum(map(ord, key)) + 3 * ELS.index(el)) % 4]


SOLO = []
for v in sorted(FJ["vessels"], key=lambda v: v["id"]):
    crew = v.get("crew") or {}
    if (crew.get("seats") or 1) > 1 or not v["status"]["hasMap"] or v["key"] not in k.HULL:
        continue
    c = certify(v)
    tiers = {e: placeholder_tier(v["key"], e, bool(c["games"][e])) for e in ELS}
    SOLO.append({"key": v["key"], "name": v["name"], "id": v["id"], "img": B + k.HULL[v["key"]], **c, "tiers": tiers,
                 "story": (v.get("description") or "").split(". ")[0].rstrip(".")})
BY = {s["key"]: s for s in SOLO}
STAGES = ["Arena Ready", "Fleet Ready", "Launch Ready", "In the hangar"]
COUNT = {st: sum(1 for s in SOLO if s["stage"] == st) for st in STAGES}

# ---------------------------------------------------------------- the kit (Nocturne, the codex's own look)
CSS = f"""
body{{margin:0;background:{T['ground']}}}
.app{{position:relative;overflow:hidden;color:{T['text']};background:radial-gradient(ellipse 70% 60% at 50% 30%,{T['wash']},{T['ground']} 72%);font-family:'Saira',sans-serif}}
.app *{{box-sizing:border-box}}
button{{font:inherit;color:inherit}}
button:focus-visible{{outline:2px solid {T['jade']};outline-offset:2px}}
.disp{{font-family:'Saira Condensed',sans-serif;font-weight:700;text-transform:uppercase;letter-spacing:.02em;line-height:1}}
.lab{{font-size:12px;letter-spacing:.22em;color:{T['soft']};text-transform:uppercase}}
.soft{{-webkit-mask-image:radial-gradient(ellipse 58% 62% at 50% 50%,#000 52%,transparent 100%);mask-image:radial-gradient(ellipse 58% 62% at 50% 50%,#000 52%,transparent 100%)}}
.hud{{position:absolute;padding:22px 24px;background:rgba(19,1,12,.88)}}
.chip{{border:0;cursor:pointer;height:34px;padding:0 14px;background:transparent;color:{T['soft']};box-shadow:inset 0 0 0 1px {T['raised']};font-size:13px;letter-spacing:.06em;transition:color .2s {EASE},background .2s {EASE}}}
.chip:hover{{color:{T['text']}}}
.chip.on{{color:{T['ground']};background:{T['jade']};box-shadow:none}}
/* the certificate: five segments, filled in ink, the stage word in Gold only when it is Arena Ready (a first place) */
.seg{{display:grid;grid-template-columns:repeat(5,1fr);gap:4px}}
.seg i{{display:block;height:8px;background:{T['raised']};opacity:.45;transition:opacity .4s {EASE},background .4s {EASE}}}
.seg i.on{{background:{T['text']};opacity:1}}
.seg i.part{{background:linear-gradient(90deg,{T['text']} var(--p),{T['raised']} var(--p));opacity:1}}
.seg.thin i{{height:4px}}
.chk{{display:grid;grid-template-columns:20px 130px 1fr;gap:10px;align-items:center;padding:7px 0;border-bottom:1px solid rgba(112,64,95,.5);font-size:14px}}
.chk svg{{width:16px;height:16px}}
.chk span{{color:{T['soft']};font-size:12px;letter-spacing:.12em;text-transform:uppercase}}
.chk.no{{color:{T['off']}}}
.stage{{font-family:'Saira Condensed',sans-serif;font-weight:700;text-transform:uppercase;font-size:30px;letter-spacing:.04em}}
.stage.gold{{color:{T['gold']}}}
.stage.dim{{color:{T['soft']}}}
/* a tier letter: S solid (Gold, a first place), A outlined ink, B soft, C off; a dash is no game of that element */
.tier{{display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;font-family:'Saira Condensed',sans-serif;font-weight:700;font-size:22px}}
.tier.tS{{background:{T['gold']};color:{T['ground']}}}
.tier.tA{{box-shadow:inset 0 0 0 2px {T['text']};color:{T['text']}}}
.tier.tB{{box-shadow:inset 0 0 0 1px {T['raised']};color:{T['soft']}}}
.tier.tC{{color:{T['off']};box-shadow:inset 0 0 0 1px rgba(112,64,95,.5)}}
.tier.t-{{color:{T['off']};font-size:16px}}
.vsel{{display:flex;align-items:center;gap:10px;height:44px;padding:0 14px;border:0;cursor:pointer;background:transparent;box-shadow:inset 0 0 0 1px {T['raised']};transition:background .2s {EASE}}}
.vsel.on{{background:{T['surface']};box-shadow:inset 0 0 0 1px {T['jade']}}}
.vsel img{{width:46px;height:26px;object-fit:cover}}
.trow{{display:grid;grid-template-columns:64px 170px 300px 70px 160px repeat(4,70px);gap:18px;align-items:center;height:42px;border-bottom:1px solid rgba(112,64,95,.45);font-size:15px}}
.trow img{{width:64px;height:36px;object-fit:cover}}
.trow.h{{height:34px;font-size:11px;letter-spacing:.2em;color:{T['soft']};text-transform:uppercase}}
.fcard{{display:flex;flex-direction:column;gap:7px;padding:8px 10px 10px;background:rgba(60,29,49,.42);box-shadow:inset 0 0 0 1px rgba(112,64,95,.6)}}
.fcard img{{width:100%;height:62px;object-fit:cover;-webkit-mask-image:radial-gradient(ellipse 60% 64% at 50% 50%,#000 50%,transparent 100%);mask-image:radial-gradient(ellipse 60% 64% at 50% 50%,#000 50%,transparent 100%)}}
.fcard b{{font-family:'Saira Condensed',sans-serif;font-size:21px;text-transform:uppercase;line-height:1}}
.tier.hot{{outline:2px solid {T['jade']};outline-offset:1px}}
.tile{{display:flex;flex-direction:column;gap:6px;width:150px;padding:8px;background:rgba(60,29,49,.35);box-shadow:inset 0 0 0 1px rgba(112,64,95,.6)}}
.tile img{{width:134px;height:70px;object-fit:cover}}
.tile b{{font-family:'Saira Condensed',sans-serif;font-size:19px;text-transform:uppercase;line-height:1}}
.tile small{{font-size:11px;color:{T['soft']}}}
.lane{{display:grid;grid-template-columns:90px 1fr;gap:20px;align-items:center;min-height:132px;border-bottom:1px solid rgba(112,64,95,.5);padding:10px 0}}
.lane .tier{{width:72px;height:72px;font-size:46px}}
.lane .tiles{{display:flex;gap:12px;flex-wrap:wrap}}
.grow{{animation:grow .7s {EASE} both}}
@keyframes grow{{from{{transform:scaleX(0);transform-origin:left}}to{{transform:scaleX(1);transform-origin:left}}}}
@media (prefers-reduced-motion:reduce){{.grow{{animation:none}}}}
"""
TICK = f'<svg viewBox="0 0 16 16" fill="none" stroke="{T["text"]}" stroke-width="2" aria-hidden="true"><path d="M3 8.5l3.2 3L13 4.5"/></svg>'
DASH = f'<svg viewBox="0 0 16 16" fill="none" stroke="{T["off"]}" stroke-width="2" aria-hidden="true"><path d="M4 8h8"/></svg>'


def head(title, body, logic, w=1920, h=1080):
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
<link href="https://fonts.googleapis.com/css2?{FONTS}&amp;display=swap" rel="stylesheet">
<style>{CSS}</style>
</helmet>
<div class="app" style="width:{w}px;height:{h}px">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic}
</script>
</body>
</html>
"""


def topbar(right=""):
    return (f'<header style="position:absolute;left:64px;right:64px;top:24px;height:52px;display:flex;align-items:center;gap:26px">'
            f'<span class="disp" style="font-size:28px;letter-spacing:.12em">Fleet Codex</span>'
            f'<span style="font-size:13px;color:{T["soft"]};letter-spacing:.08em">THE FLEET</span><span style="flex:1"></span>{right}</header>')


def seg_html(var):
    """The five segments of a certificate from a list of {cls, style} in `var`."""
    return f'<div class="seg"><sc-for list="{{{{{var}}}}}" as="g" hint-placeholder-count="5"><i class="{{{{g.cls}}}}" style="{{{{g.style}}}}"></i></sc-for></div>'


SEGS_JS = r"""
  segs(v) { return v.done.map((d, i) => i === 1 && !d && v.wired > 0 ? { cls: 'part', style: '--p:' + (v.wired * 25) + '%' } : { cls: d ? 'on' : '', style: '' }); }
  stageCls(v) { return v.stage === 'Arena Ready' ? 'stage gold' : v.stage === 'In the hangar' ? 'stage dim' : 'stage'; }
"""


# ======================================================================== A: on the vessel page (recommended)
def option_a():
    picks = ["Squirrel", "Grizzly", "Rhino", "Shrike"]
    data = [BY[p] for p in picks]
    logic = r"""class Component extends DCLogic {
  constructor(props) { super(props); this.state = { sel: 0 }; }
""" + SEGS_JS + r"""
  renderVals() {
    const V = __V__, C = __C__, E = __E__;
    const v = V[this.state.sel];
    const vessels = V.map((x, i) => ({ name: x.name, img: x.img, cls: i === this.state.sel ? 'vsel on' : 'vsel', pick: () => this.setState({ sel: i }) }));
    const checks = C.map((c, i) => ({ label: c, detail: v.detail[i], cls: v.done[i] ? 'chk' : 'chk no', ok: v.done[i], no: !v.done[i] }));
    const games = E.map(e => ({ el: e.toUpperCase(), names: v.games[e].join(', ') || 'None yet', tier: v.tiers[e], tcls: 'tier t' + v.tiers[e], none: !v.games[e].length }));
    return { vessels, v, checks, games, segs: this.segs(v), stageCls: this.stageCls(v), pct: v.pct + '%' };
  }
}"""
    logic = logic.replace("__V__", json.dumps(data, ensure_ascii=False)).replace("__C__", json.dumps(CHECKS)).replace("__E__", json.dumps(ELS))
    right = (f'<sc-for list="{{{{vessels}}}}" as="x" hint-placeholder-count="4"><button type="button" class="{{{{x.cls}}}}" onClick="{{{{x.pick}}}}">'
             f'<img src="{{{{x.img}}}}" alt=""><span class="disp" style="font-size:20px">{{{{x.name}}}}</span></button></sc-for>')
    body = f"""{topbar(right)}
<div style="position:absolute;left:0;right:0;top:96px;bottom:0;background:radial-gradient(ellipse at 52% 46%,{T['face']},transparent 72%)"></div>
<img class="soft" src="{{{{v.img}}}}" alt="" style="position:absolute;left:520px;top:150px;width:900px;height:520px;object-fit:cover">
<section class="hud" aria-label="Flight certificate" style="left:64px;top:120px;width:560px">
  <div class="lab" style="color:{T['jade']}">VESSEL {{{{v.id}}}}</div>
  <h1 class="disp" style="font-size:88px;margin:6px 0 4px">{{{{v.name}}}}</h1>
  <p style="margin:0 0 22px;font-size:16px;color:{T['soft']}">{{{{v.story}}}}.</p>
  <div class="lab">FLIGHT CERTIFICATE</div>
  <div style="display:flex;align-items:baseline;gap:16px;margin:8px 0 12px"><span class="disp num" style="font-size:64px">{{{{pct}}}}</span><span class="{{{{stageCls}}}}">{{{{v.stage}}}}</span></div>
  {seg_html('segs')}
  <div style="margin-top:14px"><sc-for list="{{{{checks}}}}" as="c" hint-placeholder-count="5"><div class="{{{{c.cls}}}}"><sc-if value="{{{{c.ok}}}}" hint-placeholder-val="{{{{true}}}}">{TICK}</sc-if><sc-if value="{{{{c.no}}}}" hint-placeholder-val="{{{{false}}}}">{DASH}</sc-if><span>{{{{c.label}}}}</span>{{{{c.detail}}}}</div></sc-for></div>
  <p style="margin:14px 0 0;font-size:14px;color:{T['jade']}">{{{{v.next}}}}</p>
</section>
<section class="hud" aria-label="Element games and tiers" style="right:64px;top:120px;width:470px">
  <div class="lab">ITS ELEMENT GAMES AND TIERS</div>
  <div style="margin-top:10px"><sc-for list="{{{{games}}}}" as="g" hint-placeholder-count="4">
    <div style="display:grid;grid-template-columns:80px 1fr 34px;gap:14px;align-items:center;padding:12px 0;border-bottom:1px solid rgba(112,64,95,.5)">
      <span class="lab" style="letter-spacing:.16em">{{{{g.el}}}}</span><span style="font-size:16px">{{{{g.names}}}}</span><span class="{{{{g.tcls}}}}">{{{{g.tier}}}}</span></div></sc-for></div>
  <p style="margin:14px 0 0;font-size:13px;color:{T['soft']}">S is the best of the fleet in that element's game, C the least. A dash: no game of that element yet.</p>
</section>
<div class="hud" style="left:64px;right:64px;bottom:40px;padding:16px 24px;display:flex;gap:40px;align-items:center">
  <span class="lab">THE FLEET</span>
  {''.join(f'<span style="font-size:15px"><b class="disp" style="font-size:26px;margin-right:8px{";color:" + T["gold"] if st == "Arena Ready" else ""}">{COUNT[st]}</b>{st}</span>' for st in STAGES)}
  <span style="flex:1"></span><span style="font-size:13px;color:{T['soft']}">{len(SOLO)} solo vessels &middot; crews are not certified</span>
</div>"""
    return head("Rev 84 A - On the vessel page: the certificate in the briefing, a tier beside each element game (recommended)", body, logic)


# ======================================================================== B: the whole fleet, sorted and filtered
# Shombith, 2026-11-10, while this round was drawn: "We should be able to sort and filter in the fleet codex all fleet
# view as per tier and completion separately". So the codex's #fleet page keeps its cards and gains two independent
# controls: SORT (completion, or one element's tier) and FILTER (a stage, and/or an element's tier letter).
def option_b():
    logic = r"""class Component extends DCLogic {
  constructor(props) { super(props); this.state = { sort: 'Completion', stage: 'Any stage', tel: 'Time', tl: 'Any tier' }; }
""" + SEGS_JS + r"""
  renderVals() {
    const V = __V__, E = __E__, S = __S__, N = __N__, R = { S: 0, A: 1, B: 2, C: 3, '-': 4 };
    const st = this.state;
    let rows = V.filter(v => (st.stage === 'Any stage' || v.stage === st.stage) && (st.tl === 'Any tier' || v.tiers[st.tel] === st.tl));
    const key = st.sort === 'Completion' ? null : st.sort.split(' ')[0];
    rows = rows.slice().sort((a, b) => key ? (R[a.tiers[key]] - R[b.tiers[key]]) || (b.pct - a.pct) : (b.pct - a.pct) || (a.id - b.id));
    const cards = rows.map(v => Object.assign({}, v, { segs: this.segs(v), stageCls: this.stageCls(v), pctS: v.pct + '%',
      tiers: E.map(e => ({ e: e.charAt(0), t: v.tiers[e], cls: 'tier t' + v.tiers[e] + (key === e || (st.tl !== 'Any tier' && st.tel === e) ? ' hot' : '') })) }));
    const chips = (list, cur, set, lab) => list.map(x => ({ label: lab ? lab(x) : x, cls: x === cur ? 'chip on' : 'chip', pick: () => this.setState(set(x)) }));
    return { cards, none: cards.length === 0, shown: cards.length + ' of ' + V.length,
      sorts: chips(['Completion'].concat(E.map(e => e + ' tier')), st.sort, x => ({ sort: x })),
      stages: chips(['Any stage'].concat(S), st.stage, x => ({ stage: x }), x => x === 'Any stage' ? x : x + ' ' + N[x]),
      tels: chips(E, st.tel, x => ({ tel: x })),
      tls: chips(['Any tier', 'S', 'A', 'B', 'C'], st.tl, x => ({ tl: x })) };
  }
}"""
    logic = (logic.replace("__V__", json.dumps(SOLO, ensure_ascii=False)).replace("__E__", json.dumps(ELS))
             .replace("__S__", json.dumps(STAGES)).replace("__N__", json.dumps(COUNT)))
    def chiprow(var, n):
        return (f'<div style="display:flex;gap:6px;flex-wrap:wrap"><sc-for list="{{{{{var}}}}}" as="c" hint-placeholder-count="{n}">'
                f'<button type="button" class="{{{{c.cls}}}}" onClick="{{{{c.pick}}}}">{{{{c.label}}}}</button></sc-for></div>')
    body = f"""{topbar()}
<div style="position:absolute;left:64px;right:64px;top:92px;display:flex;align-items:flex-end;gap:30px">
  <div><div class="lab" style="color:{T['jade']}">THE FLEET</div><h1 class="disp" style="font-size:60px;margin:4px 0 0">Pick a vessel</h1></div>
  <span style="flex:1"></span><span style="font-size:14px;color:{T['soft']}">{{{{shown}}}} solo vessels</span>
</div>
<div style="position:absolute;left:64px;right:64px;top:176px;display:grid;grid-template-columns:auto 1fr;gap:10px 18px;align-items:center">
  <span class="lab">SORT BY</span>{chiprow('sorts', 5)}
  <span class="lab">COMPLETION</span>{chiprow('stages', 5)}
  <span class="lab">TIER</span><div style="display:flex;gap:18px;align-items:center">{chiprow('tels', 4)}<span style="width:1px;height:26px;background:{T['raised']}"></span>{chiprow('tls', 5)}</div>
</div>
<div style="position:absolute;left:64px;right:64px;top:318px;display:grid;grid-template-columns:repeat(6,1fr);gap:12px">
  <sc-for list="{{{{cards}}}}" as="v" hint-placeholder-count="18"><div class="fcard">
    <img src="{{{{v.img}}}}" alt="">
    <div style="display:flex;align-items:baseline;gap:8px"><b>{{{{v.name}}}}</b><span style="flex:1"></span><span class="disp num" style="font-size:20px">{{{{v.pctS}}}}</span></div>
    <div class="seg thin"><sc-for list="{{{{v.segs}}}}" as="g" hint-placeholder-count="5"><i class="{{{{g.cls}}}}" style="{{{{g.style}}}}"></i></sc-for></div>
    <div style="display:flex;align-items:center;gap:5px"><span class="{{{{v.stageCls}}}}" style="font-size:15px;flex:1">{{{{v.stage}}}}</span>
      <sc-for list="{{{{v.tiers}}}}" as="t" hint-placeholder-count="4"><span style="display:flex;flex-direction:column;align-items:center;gap:2px"><span class="{{{{t.cls}}}}" style="width:24px;height:24px;font-size:15px">{{{{t.t}}}}</span><small style="font-size:9px;letter-spacing:.1em;color:{T['soft']}">{{{{t.e}}}}</small></span></sc-for></div>
  </div></sc-for>
  <sc-if value="{{{{none}}}}" hint-placeholder-val="{{{{false}}}}"><p style="color:{T['soft']};grid-column:1/-1">No vessel matches both filters.</p></sc-if>
</div>"""
    return head("Rev 84 B - The whole fleet, sorted and filtered by completion and by tier, each on its own", body, logic)


# ======================================================================== C: tier lists, one element at a time
def option_c():
    logic = r"""class Component extends DCLogic {
  constructor(props) { super(props); this.state = { el: 'Time' }; }
""" + SEGS_JS + r"""
  renderVals() {
    const V = __V__, E = __E__, T = ['S', 'A', 'B', 'C'];
    const el = this.state.el;
    const tile = v => ({ name: v.name, img: v.img, sub: v.pct + '% · ' + v.stage, segs: this.segs(v), game: v.games[el].join(', ') });
    const lanes = T.map(t => ({ t, cls: 'tier t' + t, tiles: V.filter(v => v.tiers[el] === t).map(tile) }));
    lanes.forEach(l => { l.empty = l.tiles.length === 0; });
    const none = V.filter(v => v.tiers[el] === '-').map(v => v.name).join(', ');
    const tabs = E.map(e => ({ label: e + ' games', cls: e === el ? 'chip on' : 'chip', pick: () => this.setState({ el: e }) }));
    return { lanes, tabs, none: none || 'Every solo vessel has one.', el };
  }
}"""
    logic = logic.replace("__V__", json.dumps(SOLO, ensure_ascii=False)).replace("__E__", json.dumps(ELS))
    body = f"""{topbar()}
<div style="position:absolute;left:64px;right:64px;top:96px;display:flex;align-items:flex-end;gap:30px">
  <div><div class="lab" style="color:{T['jade']}">ELEMENT TIERS</div><h1 class="disp" style="font-size:64px;margin:4px 0 0">Who flies it best</h1></div>
  <span style="flex:1"></span>
  <div style="display:flex;gap:6px"><sc-for list="{{{{tabs}}}}" as="t" hint-placeholder-count="4"><button type="button" class="{{{{t.cls}}}}" onClick="{{{{t.pick}}}}">{{{{t.label}}}}</button></sc-for></div>
</div>
<div style="position:absolute;left:64px;right:64px;top:196px">
  <sc-for list="{{{{lanes}}}}" as="l" hint-placeholder-count="4"><div class="lane">
    <span class="{{{{l.cls}}}}">{{{{l.t}}}}</span>
    <div class="tiles"><sc-for list="{{{{l.tiles}}}}" as="x" hint-placeholder-count="3"><div class="tile">
      <img src="{{{{x.img}}}}" alt=""><b>{{{{x.name}}}}</b><small>{{{{x.game}}}}</small>
      <div class="seg thin"><sc-for list="{{{{x.segs}}}}" as="g" hint-placeholder-count="5"><i class="{{{{g.cls}}}}" style="{{{{g.style}}}}"></i></sc-for></div><small>{{{{x.sub}}}}</small>
    </div></sc-for>
    <sc-if value="{{{{l.empty}}}}" hint-placeholder-val="{{{{false}}}}"><span style="color:{T['off']};font-size:14px">No vessel rated here yet.</span></sc-if></div>
  </div></sc-for>
  <p style="margin:16px 0 0;font-size:14px;color:{T['soft']}"><span class="lab" style="margin-right:14px">NO {{{{el}}}} GAME YET</span>{{{{none}}}}</p>
</div>"""
    return head("Rev 84 C - Tier lists: one element at a time, S to C, with each vessel's certificate under its hull", body, logic)


# ======================================================================== notes
def notes():
    sheet = "".join(
        f'<tr><td style="padding:3px 14px 3px 0">{s["name"]}</td><td class="num" style="padding:3px 14px 3px 0">{s["pct"]}%</td>'
        f'<td style="padding:3px 14px 3px 0;color:{T["gold"] if s["stage"] == "Arena Ready" else T["text"]}">{s["stage"]}</td>'
        + "".join(f'<td style="padding:3px 10px 3px 0;color:{T["text"] if s["games"][e] else T["off"]}">{"?" if s["games"][e] else "-"}</td>' for e in ELS)
        + "</tr>" for s in sorted(SOLO, key=lambda s: s["id"]))
    body = f"""
<div style="position:absolute;left:64px;top:48px;width:820px;font-size:15px;line-height:1.6">
  <div class="lab" style="color:{T['jade']}">REV 84 NOTES</div>
  <h1 class="disp" style="font-size:44px;margin:6px 0 14px">Flight certificate and element tiers</h1>
  <p style="margin:0 0 10px"><b>Name (suggested):</b> the <b>Flight Certificate</b>, with three stages a vessel climbs:
  <b>Launch Ready</b> (the minimum viable pass), <b>Fleet Ready</b>, <b>Arena Ready</b>. Below Launch Ready it is <i>In the hangar</i>.</p>
  <p style="margin:0 0 10px"><b>Five checks, 20% each,</b> read from the game's own files by the codex (never typed):
  1 all four abilities in the map; 2 the level-5 upgrades, 5% per wired one; 3 a Time game; 4 a Mass or a Space game (either);
  5 a Charge game. A game is an arcade card whose element is that element.</p>
  <p style="margin:0 0 10px"><b>Launch Ready</b> = checks 1, 2 (all four wired) and 3. <b>Fleet Ready</b> = and 4. <b>Arena Ready</b> = all five.</p>
  <p style="margin:0 0 10px"><b>Tiers</b> S, A, B, C per element: S the best of the fleet in that element's game, C the least. A dash where the
  vessel has no game of that element. <b>The letters on A, B and C are placeholders</b>: every vessel is rated by you first.</p>
  <p style="margin:0 0 10px"><b>Only solo vessels.</b> Crews (Goby &amp; Pistol, Wrasse &amp; Grouper, Orca, Honeyguide) and the Termite (no map) are left out.</p>
  <p style="margin:0"><b>Fleet view (B):</b> sort by completion or by one element's tier, and filter by stage and by an element's tier letter, each on its own and together (your ask of 2026-11-10). <b>Colour:</b> ink for done; Gold only for Arena Ready and an S (a first place, the style guide's Gold); no element colour.
  A is recommended: the certificate sits where a reader already looks at one vessel; B and C can come with it as the fleet views.</p>
</div>
<div style="position:absolute;left:940px;top:48px;width:920px;font-size:13px">
  <div class="lab" style="margin-bottom:8px">TODAY, FROM THE CODEX DATA &middot; TIERS TO RATE (?)</div>
  <table style="border-collapse:collapse"><thead><tr style="color:{T['soft']};font-size:11px;letter-spacing:.16em;text-align:left">
  <th style="padding:3px 14px 3px 0">VESSEL</th><th>%</th><th>STAGE</th>{''.join(f'<th style="padding:3px 10px 3px 0">{e.upper()}</th>' for e in ELS)}</tr></thead>
  <tbody>{sheet}</tbody></table>
</div>"""
    logic = "class Component extends DCLogic {\n  renderVals() { return {}; }\n}"
    return head("Rev 84 - notes: the scoring rule, the names, the tier sheet to confirm", body, logic, 1920, 900)


if __name__ == "__main__":
    ROOT.mkdir(parents=True, exist_ok=True)
    for name, fn in (("R84A", option_a), ("R84B", option_b), ("R84C", option_c), ("R84Notes", notes)):
        (ROOT / f"{name}.dc.html").write_text(fn(), encoding="utf-8")
    for s in SOLO:
        print(f'{s["name"]:14} {s["pct"]:3}%  {s["stage"]}')
