"""Rev 109 boards: the crew PDA itself, redesigned (Shombith, 2026-11-13: "I like C but the actual device it looks too
earth like, it should be something completely different and modern and never seen before").

Rev 99 C stays: the PDA lies on the hangar's workbench and is picked up to read the crew. Only the device changes. Each
option board is the Pilots tab over the bench with that device lying on it (r109_ark_pda_render.py, PDA_KIND), and
picking it up cross-fades to the same device held square to the view, its display an upright rectangle the game lays
the crew on (as the built Rev 99 C does). The screen markup and logic are Rev 99's (r99_pilots_boards.py), unchanged.

    python3 r109_pda_device_boards.py <out project dir> <blobs.json> <corners dir> <pilots.json>
"""
import html
import json
import os
import sys

import numpy as np

OUT, BLOBS, CORNERS, PILOTS = sys.argv[1:5]
B = json.load(open(BLOBS))
P = json.load(open(PILOTS))
SW, SH = 1200, 870           # the screen element's own size (the glass is 0.27 x 0.196 m)


def matrix3d(quad):
    """CSS matrix3d taking the SW x SH rect onto quad (TL, TR, BR, BL, board pixels)."""
    src = [(0, 0), (SW, 0), (SW, SH), (0, SH)]
    A, b = [], []
    for (x, y), (u, v) in zip(src, quad):
        A.append([x, y, 1, 0, 0, 0, -u * x, -u * y]); b.append(u)
        A.append([0, 0, 0, x, y, 1, -v * x, -v * y]); b.append(v)
    h = np.linalg.solve(np.array(A, float), np.array(b, float))
    H = [[h[0], h[1], h[2]], [h[3], h[4], h[5]], [h[6], h[7], 1.0]]
    m = [H[0][0], H[1][0], 0, H[2][0], H[0][1], H[1][1], 0, H[2][1], 0, 0, 1, 0, H[0][2], H[1][2], 0, H[2][2]]
    return "matrix3d(" + ",".join(f"{v:.9g}" for v in m) + ")"


def quad_for(scene):
    j = json.load(open(os.path.join(CORNERS, f"{scene}.json")))
    k = 1920.0 / j["W"]
    return [(x * k, y * k) for (x, y) in j["corners"]]


HELMET = """<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:#8A8FAE;text-shadow:0 0 8px rgba(0,1,10,.95),0 1px 2px rgba(0,1,10,.95)}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.abs{position:absolute}
.sl{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.sls{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.kc{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;color:#B8C0F0}
.pick{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}
.pick:focus-visible,.pbtn:focus-visible{outline:2px solid #00D4FF;outline-offset:2px}
.glass{background:rgba(0,4,31,.62);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px)}
.scene{position:absolute;inset:0;transition:transform 520ms cubic-bezier(.22,.8,.25,1)}
.scr{position:absolute;left:0;top:0;width:1200px;height:870px;transform-origin:0 0;overflow:hidden;background:#00010A;color:#E6E9FF;font-family:'Aldrich','Chakra Petch',sans-serif}
.pbtn{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:12px;height:72px;padding:0 30px;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:28px;letter-spacing:.1em;text-transform:uppercase;background:rgba(0,4,31,.7);color:#E6E9FF;box-shadow:inset 0 0 0 2px #747BAD}
.pbtn .kc{min-width:40px;height:34px;font-size:18px;border-color:currentColor;color:inherit}
.chip{position:relative;width:168px;height:214px}
.chip img{display:block;width:168px;height:168px;object-fit:cover}
.ell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
</style>
</helmet>"""

NAV = """<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.78);border-bottom:1px solid #222645;display:flex;align-items:center;justify-content:center;gap:48px;z-index:5"><span class="kc">LT</span><span style="display:flex;align-items:center;gap:10px;font-family:'Chakra Petch';font-weight:600;font-size:18px;letter-spacing:.12em;color:#747BAD"><img src="/_blob/ec690d6b90d6fcdbc430b9bbaacf28a9" alt="" style="width:26px;height:26px;object-fit:contain;opacity:0.7">CODEX</span><span style="display:flex;align-items:center;gap:10px;font-family:'Chakra Petch';font-weight:600;font-size:18px;letter-spacing:.12em;color:#747BAD"><img src="/_blob/34f5a50f1275fef42808fe9df3bf6af5" alt="" style="width:26px;height:26px;object-fit:contain;opacity:0.7">HANGAR</span><span style="display:flex;align-items:center;gap:10px;font-family:'Chakra Petch';font-weight:600;font-size:18px;letter-spacing:.12em;color:#747BAD"><img src="/_blob/a5bd4d8d1de7b494567fc3de75525e1e" alt="" style="width:26px;height:26px;object-fit:contain;opacity:0.7">HOME</span><span style="position:relative;display:flex;align-items:center;gap:10px;font-family:'Chakra Petch';font-weight:600;font-size:18px;letter-spacing:.12em;color:#E6E9FF"><img src="/_blob/e5b050c5a676193c650e94d873ad40e2" alt="" style="width:26px;height:26px;object-fit:contain;opacity:1">ARK<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span></span><span style="display:flex;align-items:center;gap:10px;font-family:'Chakra Petch';font-weight:600;font-size:18px;letter-spacing:.12em;color:#747BAD"><img src="/_blob/d0e850bc927559f1a499868d26f2a3de" alt="" style="width:26px;height:26px;object-fit:contain;opacity:0.7">PORT</span><span class="kc">RT</span></nav>
<div class="abs" style="right:40px;top:30px;z-index:6;display:flex;align-items:center;gap:8px;font-size:20px;color:#FFAE00;font-variant-numeric:tabular-nums"><span style="width:14px;height:18px;background:#FFAE00;clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>
<div class="abs" style="left:40px;right:40px;top:96px;height:52px;display:flex;align-items:center;gap:30px;border-bottom:1px solid rgba(116,123,173,.35);z-index:4"><span class="kc">LB</span><sc-for list="{{tabs}}" as="t" hint-placeholder-count="5"><button class="pick" type="button" onClick="{{t.pick}}" style="position:relative;display:flex;align-items:center;gap:8px;color:{{t.col}}"><sc-if value="{{t.locked}}" hint-placeholder-val="{{ false }}"><svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><rect x="2" y="7" width="10" height="8"></rect><path d="M4 7V5a3 3 0 0 1 6 0v2"></path></svg></sc-if><span class="cap" style="font-size:15px;color:{{t.col}}">{{t.name}}</span><span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{{t.mark}}"></span></button></sc-for><span class="kc">RB</span></div>"""

ARROW_L = '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" aria-hidden="true"><path d="M15 5l-7 7 7 7"></path></svg>'
ARROW_R = '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" aria-hidden="true"><path d="M9 5l7 7-7 7"></path></svg>'
GRID = '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true"><rect x="3" y="3" width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect></svg>'


def screen_markup():
    """The PDA's screen, 1200 x 870: the crew page or one pilot's file."""
    return f"""<div class="abs" style="left:0;right:0;top:0;height:64px;display:flex;align-items:center;justify-content:space-between;padding:0 34px;border-bottom:2px solid #222645;background:#00041F">
<span class="cap" style="font-size:22px;color:#8A8FAE">{{{{headL}}}}</span><span class="cap" style="font-size:22px;color:#8A8FAE">{{{{headR}}}}</span></div>
<sc-if value="{{{{isRest}}}}" hint-placeholder-val="{{{{ true }}}}">
<img class="abs" src="{{{{close}}}}" alt="" style="left:70px;top:150px;width:470px;height:470px;object-fit:cover">
<div class="abs" style="left:600px;top:170px;width:560px;display:flex;flex-direction:column;gap:14px">
<span class="cap" style="font-size:28px;color:#8A8FAE">Last read</span>
<span class="cap" style="font-size:96px;line-height:1">{{{{cs}}}}</span>
<span style="font-size:40px;line-height:1.2">{{{{name}}}}</span>
<span style="font-size:28px;color:#B8C0F0;margin-top:26px">{{{{aboard}}}}</span>
</div>
<div class="abs" style="left:70px;right:70px;bottom:60px;height:96px;display:flex;align-items:center;justify-content:center;gap:18px;box-shadow:inset 0 0 0 3px #747BAD"><span class="kc" style="min-width:52px;height:44px;font-size:22px;border-color:#E6E9FF;color:#E6E9FF">A</span><span class="cap" style="font-size:40px">Pick up</span></div>
<button class="pick" type="button" aria-label="Pick up the crew PDA" onClick="{{{{pickUp}}}}" style="position:absolute;inset:0;z-index:3"></button>
</sc-if>
<sc-if value="{{{{isCrew}}}}" hint-placeholder-val="{{{{ false }}}}">
<div class="abs" style="left:38px;top:96px;width:1124px;display:flex;flex-wrap:wrap;gap:22px 23px">
<sc-for list="{{{{crew}}}}" as="c" hint-placeholder-count="18"><div class="chip"><button class="pick" type="button" aria-label="{{{{c.aria}}}}" onClick="{{{{c.pick}}}}" style="position:absolute;inset:0;z-index:2"></button><img src="{{{{c.img}}}}" alt="" style="box-shadow:0 0 0 2px {{{{c.ring}}}}"><span class="cap ell" style="display:block;margin-top:10px;font-size:20px;color:#E6E9FF">{{{{c.cs}}}}</span><span class="ell" style="display:block;font-size:15px;color:#8A8FAE;margin-top:2px">{{{{c.name}}}}</span></div></sc-for>
</div>
<span class="abs" style="left:38px;bottom:34px;font-size:20px;color:#747BAD">Tap a pilot to read their file.</span>
<button class="pbtn sls abs" type="button" onClick="{{{{down}}}}" style="right:34px;bottom:14px;height:62px"><span class="kc">B</span>Lay down</button>
</sc-if>
<sc-if value="{{{{isFile}}}}" hint-placeholder-val="{{{{ false }}}}">
<img class="abs" src="{{{{tall}}}}" alt="{{{{tallAlt}}}}" style="left:34px;top:92px;width:368px;height:460px;object-fit:cover">
<div class="abs" style="left:34px;top:568px;width:368px;display:flex;flex-direction:column;gap:12px">
<span class="cap" style="font-size:15px;color:#747BAD">Sight</span><span style="font-size:20px;line-height:1.35;color:#E6E9FF">{{{{sees}}}}</span></div>
<div class="abs" style="left:440px;top:88px;width:726px;display:flex;flex-direction:column;gap:6px">
<span class="cap" style="font-size:22px;color:#8A8FAE">{{{{cs}}}}</span>
<span class="ell" style="font-size:48px;line-height:1.12">{{{{name}}}}</span>
<span style="font-size:20px;color:#B8C0F0;margin-top:4px">{{{{species}}}}</span>
<span style="font-size:20px;color:#B8C0F0">{{{{faction}}}} &#183; {{{{home}}}}</span>
</div>
<div class="abs" style="left:440px;top:300px;width:726px;height:2px;background:#222645"></div>
<span class="abs cap" style="left:440px;top:322px;font-size:15px;color:#747BAD">Story</span>
<div class="abs" style="left:440px;top:354px;width:726px;display:flex;flex-direction:column;gap:16px">
<sc-for list="{{{{story}}}}" as="p" hint-placeholder-count="3"><p style="margin:0;font-size:22px;line-height:1.42;color:#E6E9FF">{{{{p.t}}}}</p></sc-for>
</div>
<div class="abs" style="left:34px;right:34px;bottom:24px;height:72px;display:flex;align-items:center;justify-content:space-between">
<button class="pbtn sls" type="button" onClick="{{{{back}}}}"><span class="kc">B</span>{GRID}Crew</button>
<div style="display:flex;align-items:center;gap:18px"><button class="pbtn sls" type="button" aria-label="Previous pilot" onClick="{{{{prev}}}}">{ARROW_L}Prev</button><span class="cap" style="font-size:22px;color:#B8C0F0;min-width:96px;text-align:center">{{{{count}}}}</span><button class="pbtn sls" type="button" aria-label="Next pilot" onClick="{{{{next}}}}">Next{ARROW_R}</button></div>
</div>
</sc-if>
<div class="abs" aria-hidden="true" style="inset:0;pointer-events:none;background:linear-gradient(118deg,rgba(255,255,255,.07) 0%,rgba(255,255,255,.02) 34%,rgba(255,255,255,0) 35%,rgba(255,255,255,0) 100%)"></div>"""


def logic(opt):
    crew = [dict(cs=p["cs"], name=p["name"], species="Human / " + p["a1"] + " / " + p["a2"], faction=p["faction"],
                 home=p["home"], sees=p["sees"], story=[s for s in p["story"].split("\n\n") if s.strip()],
                 close=B["close_" + p["cs"]], tall=B["tall_" + p["cs"]]) for p in P]
    return f"""<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1920,"height":1080}}}}'>
const CREW = {json.dumps(crew, ensure_ascii=False)};
const TABS = ["Converter", "Squads", "Pilots", "Artifacts", "Decor"];
const MAT_REST = {json.dumps(opt["mat"])};
const MAT_LEAN = {json.dumps(opt["matLean"])};
class Component extends DCLogic {{
 constructor(props) {{ super(props); this.state = {{ page: 'rest', sel: {opt["start"]}, toast: '' }}; this.t = null; }}
 componentWillUnmount() {{ if (this.t) clearTimeout(this.t); }}
 say(x) {{ if (this.t) clearTimeout(this.t); this.setState({{ toast: x }}); this.t = setTimeout(() => this.setState({{ toast: '' }}), 2600); }}
 renderVals() {{
  const st = this.state, p = CREW[st.sel], n = CREW.length, file = st.page === 'file', rest = st.page === 'rest';
  const tabs = TABS.map((x, i) => ({{ name: x, locked: x === 'Decor', col: x === 'Pilots' ? '#E6E9FF' : (x === 'Decor' ? '#5C5F70' : '#747BAD'),
   mark: x === 'Pilots' ? '#E6E9FF' : 'transparent',
   pick: () => (x === 'Pilots' ? null : this.say(x === 'Decor' ? 'Decor is locked. It opens later.' : 'This board shows the Pilots tab; ' + x + ' is on its own boards.')) }}));
  const crew = CREW.map((c, i) => ({{ cs: c.cs, name: c.name, img: c.close, aria: c.cs + ', ' + c.name,
   ring: i === st.sel ? '#00D4FF' : 'rgba(116,123,173,.35)', pick: () => this.setState({{ page: 'file', sel: i }}) }}));
  return {{ tabs, crew, isRest: rest, isCrew: st.page === 'crew', isFile: file, mat: rest ? MAT_REST : MAT_LEAN,
   restOp: rest ? 1 : 0, leanOp: rest ? 0 : 1, close: p.close, aboard: n + ' pilots aboard',
   pickUp: () => this.setState({{ page: 'crew' }}),
   headL: file ? 'Crew file' : 'Your crew', headR: file ? '' : 'Pilots: ' + n,
   down: () => this.setState({{ page: 'rest' }}),
   cs: p.cs, name: p.name, species: p.species, faction: p.faction, home: 'From ' + p.home, sees: p.sees,
   story: p.story.map((t) => ({{ t }})), tall: p.tall, tallAlt: p.name + ', ' + p.cs,
   count: (st.sel + 1) + ' / ' + n,
   back: () => this.setState({{ page: 'crew' }}),
   prev: () => this.setState({{ sel: (st.sel + n - 1) % n }}),
   next: () => this.setState({{ sel: (st.sel + 1) % n }}),
   toast: st.toast, hasToast: st.toast !== '', hint: rest ? 'A picks up the crew PDA.' : (file ? 'D-pad left and right step the crew. B goes back to the crew.' : 'A opens a pilot\\'s file. B lays the PDA down.') }};
 }}
}}
</script>"""


def board(key, title, opt):
    alt = html.escape(opt["alt"])
    body = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{html.escape(title)}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
{HELMET}
<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
<div class="scene">
<img class="abs" src="{opt["bg"]}" alt="{alt}" style="left:0;top:0;width:1920px;height:1080px;object-fit:cover;transition:opacity 450ms ease-in-out;opacity:{{{{restOp}}}}">
<img class="abs" src="{opt["bgLean"]}" alt="{html.escape(opt["altLean"])}" style="left:0;top:0;width:1920px;height:1080px;object-fit:cover;transition:opacity 450ms ease-in-out;opacity:{{{{leanOp}}}}">
<div class="scr" style="transform:{{{{mat}}}};transition:transform 450ms ease-in-out">
{screen_markup()}
</div>
</div>
<div class="abs" aria-hidden="true" style="left:0;right:0;top:0;height:220px;background:linear-gradient(180deg,rgba(0,1,10,.78),rgba(0,1,10,0));pointer-events:none"></div>
{NAV}
<div class="abs" style="left:40px;bottom:28px;z-index:4" aria-live="polite"><span class="lab">{{{{hint}}}}</span></div>
<sc-if value="{{{{hasToast}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs sl glass" role="status" style="left:50%;bottom:40px;transform:translateX(-50%);padding:14px 26px;box-shadow:inset 0 0 0 1px #434C89;font-size:18px;z-index:8">{{{{toast}}}}</div></sc-if>
</div>
</x-dc>
{logic(opt)}
</body>
</html>
"""
    open(os.path.join(OUT, f"R109{key}.dc.html"), "w").write(body)



OPTS = {
    "A": ("Rev 109 A - The tidestone: a slab of clear crystal, the crew glowing inside it, brass claws at its points (recommended)",
          "crystal", "A crew PDA cut from clear crystal, a long hexagon held at its points by brass claws, lying on the hangar's workbench",
          "The tidestone held up: the crew glowing inside the crystal, its brass claws either side", 16),
    "B": ("Rev 109 B - The nautilus: a chambered shell whose pearl throws the crew up as a sheet of light", "nautilus",
          "A crew PDA grown as a dark chambered shell, a pearl in its mouth throwing a standing sheet of light, on the hangar's workbench",
          "The nautilus held up: the sheet of light square to the view, the shell's pearl at its foot", 16),
    "C": ("Rev 109 C - The light scroll: two long prisms with the crew stretched between them as a membrane", "scroll",
          "A crew PDA made of two long hull-metal prisms with a membrane of light stretched between them, on the hangar's workbench",
          "The light scroll held up: the membrane square to the view between its two prisms", 16),
}
os.makedirs(OUT, exist_ok=True)
for key, (title, kind, alt, alt_held, start) in OPTS.items():
    board(key, title, {"bg": B["bg_" + kind], "alt": alt, "mat": matrix3d(quad_for(kind)), "start": start,
                       "bgLean": B["bg_" + kind + "_held"], "altLean": alt_held,
                       "matLean": matrix3d(quad_for(kind + "_held"))})
print("wrote", ", ".join(f"R109{k}" for k in OPTS))


def page(key, title, inner, h=1080):
    open(os.path.join(OUT, f"R109{key}.dc.html"), "w").write(f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{html.escape(title)}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
{HELMET}
<div style="width: 1920px; height: {h}px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{inner}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1920,"height":{h}}}}}'>
class Component extends DCLogic {{
renderVals() {{
return {{}};
}}
}}
</script>
</body>
</html>
""")


cells = []
for i, (k, (title, kind, alt, alt_held, _)) in enumerate(OPTS.items()):
    x = 30 + i * 630
    name = title.split(" - ", 1)[1].split(":")[0]
    cells.append(f'<span class="lab abs" style="left:{x}px;top:112px">{html.escape(k + " - " + name + ", lying")}</span>'
                 f'<img class="abs" src="{B["bg_" + kind]}" alt="{html.escape(alt)}" style="left:{x}px;top:140px;width:600px;height:338px;object-fit:cover;box-shadow:0 0 0 1px #434C89">'
                 f'<span class="lab abs" style="left:{x}px;top:520px">{html.escape(k + " - " + name + ", held")}</span>'
                 f'<img class="abs" src="{B["bg_" + kind + "_held"]}" alt="{html.escape(alt_held)}" style="left:{x}px;top:548px;width:600px;height:338px;object-fit:cover;box-shadow:0 0 0 1px #434C89">')
cells.append(f'<span class="lab abs" style="left:30px;top:930px">Before: Rev 99 C as built, a slab with a bezel</span>'
             f'<img class="abs" src="{B["bg_pda_c"]}" alt="The built crew PDA: a dark slab with a bezel on the workbench" style="left:30px;top:958px;width:200px;height:113px;object-fit:cover;box-shadow:0 0 0 1px #434C89">'
             '<span class="abs" style="left:260px;top:990px;width:1600px;font-size:18px;line-height:1.5;color:#8A8FAE">The same bench, light and camera in every still; only the device differs. The held stills are what the game shows under the crew: the display is the same upright rectangle in all three, so the built page fits any of them.</span>')
page("Compare", "Rev 109 - The three devices side by side, lying on the bench and held up",
     '<span class="abs cap" style="left:30px;top:40px;font-size:28px">The crew PDA: three devices from the Ark</span>'
     '<span class="abs" style="left:30px;top:80px;font-size:18px;color:#8A8FAE">Blender stills from the hangar the game shows (the Rev 67 kit), graded as the Ark clips are. The held displays are dark because the game draws the crew there.</span>'
     + "".join(cells))

NOTES = [
    ("Asked", "Shombith, 2026-11-13, on the built Rev 99 C: \"I like C but the actual device it looks too earth like, it should be something completely different and modern and never seen before.\" And: take the lighting and design from the pilot scenes set inside the Ark."),
    ("Kept", "Everything but the device: the hangar bench, the camera, the pick-up, the crew page and a pilot's file, the pad. The display stays an upright rectangle when held, about the same size, so ArkPilotsPage and its generator do not change."),
    ("Where the cues come from", "The in-Ark pilot scenes: Zodiac's chart table that glows at its rim, Needle's and Galleon's brass fittings, Sundial's glass, the elemental crystals' cut light, and the game's own prisms. No slab, no bezel, no keys: nothing a phone or tablet has."),
    ("A - tidestone", "A long hexagon of clear crystal with chamfered edges; the crew glows inside it, thin cyan seams run along the cuts, brass claws close over both points, a lime seed crystal at one tip. Reads as something cut, not manufactured."),
    ("B - nautilus", "A dark lacquered chambered shell with brass septa; a pearl in its mouth throws the crew up as a standing sheet of light. Lying, it is the strangest object of the three; held, the sheet fills the view and the shell drops to its foot."),
    ("C - light scroll", "Two long triangular prisms of hull metal with cyan trim and brass caps; the crew is a membrane of light stretched between them. The closest to the game's prisms; it unrolls rather than wakes."),
    ("Recommended: A", "It is the one that stays itself when held: the crystal and its claws frame the crew on every side, where B's shell and C's prisms leave the frame when the view closes in. It also matches the converter's crystals, so the Ark's two crystal things belong together."),
    ("For the build", "Re-render the bench clips (r99_pilots_bench_videos.py) from the picked device's stills, and take PDA_REST_CORNERS and GLASS_X/Y/W/H in author_menu_ark.py from its screen corners. No C# changes. The pick-up transition itself is another thread's (the transitions work)."),
    ("Open", "Whether the device glows faintly while lying (a hint it is live) or stays dark until picked up. Whether the lime seed in A should take the squad's domain colour."),
]
rows = "".join(f'<div style="display:grid;grid-template-columns:280px 1fr;gap:28px;padding:11px 0;border-top:1px solid #222645"><span class="cap" style="font-size:15px;color:#8A8FAE">{html.escape(a)}</span><span style="font-size:17px;line-height:1.45">{html.escape(b)}</span></div>' for a, b in NOTES)
page("Notes", "Rev 109 - notes: what was asked, the three devices, the recommendation, what the build changes",
     f'<span class="abs cap" style="left:80px;top:60px;font-size:28px">Rev 109 - the crew PDA, redesigned</span>'
     f'<div class="abs" style="left:80px;top:110px;width:1760px">{rows}</div>', h=900)
print("wrote R109Compare, R109Notes")
