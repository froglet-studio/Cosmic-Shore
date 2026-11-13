#!/usr/bin/env python3
"""Frogma round: the Ark's Artifacts tab without the UI cards (Shombith, 2026-11-13).

Writes three option boards and a notes board into <root>/project/. Every option is the full Ark screen on its
Artifacts tab over the built backdrop (Rev 86's room, the still the game plays as a clip) with the SAME content and
the same three levels (the hoard, one artifact, feeding it); only where the words live differs:

  A  the words hang in the room, each beside its own artifact on its pedestal (no box anywhere);
  B  the lens: a round hologram lens like the domes the captains lean over in the pilot scenes (Jugger, Scales), where
     the picked artifact's payload plays out over prisms, its four tracks round the rim;
  C  one at a time: the picked artifact drawn large in the room's middle, a museum caption under it.

The hoard is four unique artifacts (Docs/Pilots/INVENTORY.md section 2: a kind, a name, a serial, one attuned element
fed on the diminishing curve). Crystals: 10 of each, Shombith's default for now. Numbers come from the shipped kinds
(Assets/_SO_Assets/Pilots/Artifacts), the curve from ArtifactCurve, the feed batch (5) from ArkArtifactsPage.

    python3 Docs/MenuRedesignB/rounds/r105_artifacts.py <root> [--round N]
"""
import argparse
import base64
import io
import json
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve()
REPO = HERE.parents[3]

BACKDROP = "/_blob/8028acea874bf38aec5c626923e33fcc"  # Rev 86's Artifacts room (art_ab), Nocturne
CRYSTAL = {"Space": "/_blob/552b1a2acbcb63737e1174c398089c9c", "Charge": "/_blob/9b5b9502defc2ffb3ddfc86af8f2b581",
           "Mass": "/_blob/f1c37884ec4eaede9f7a20f81af70273", "Time": "/_blob/5f2b5586aa94e21d23984dedc2c4f93a"}
NAV = [("CODEX", "/_blob/ec690d6b90d6fcdbc430b9bbaacf28a9"), ("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5"),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e"), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2"),
       ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de")]


def petals():
    """The element marks are the HUD's flowers (r97_voyages._petals): five white petals turned 72 degrees."""
    out = {}
    for name in ("Charge", "Mass", "Space", "Time"):
        petal = Image.open(REPO / "Assets/Resources/ElementPetals" / f"{name.lower()}_petal.png").convert("RGBA")
        flower = Image.new("RGBA", petal.size, (0, 0, 0, 0))
        for n in range(5):
            flower = Image.alpha_composite(flower, petal.rotate(-72 * n, resample=Image.BICUBIC))
        flower = flower.crop(flower.getbbox()).resize((96, 96), Image.LANCZOS)
        buf = io.BytesIO()
        flower.save(buf, "PNG", optimize=True)
        out[name] = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
    return out


# The hoard: four unique artifacts rolled on the shipped kinds. trk = the kind's four tracks (element, label, at 0, at 10,
# twist), from Artifact_<Kind>.asset; fixed = the three rolled levels (0-3) of the tracks it is not attuned to.
ARTS = [
    {"kind": "Javelin", "name": "Quiet Javelin of the Tollmoor Shoals", "serial": "2B7E-04A1", "el": "Space",
     "base": 0.27, "ceil": 8.56, "half": 47.8, "inv": 65, "holder": "You", "seat": "Leader",
     "lore": "Pulled from the Tollmoor Shoals with Space still caught in its edge.",
     "desc": "Throws the payload down your heading. It cracks every rival super-shield along its line and bursts where it stops.",
     "trk": {"Mass": ["line width", 4, 16, "Its final burst reaches twice as far."],
             "Space": ["range", 150, 500, "It pierces: it never stops for what it cracks."],
             "Time": ["linger seconds", 0, 4, "The whole line lingers, cracking what is rebuilt along it."],
             "Charge": ["cracks before it stops", 1, 6, "It cracks ordinary shields too."]},
     "fixed": {"Mass": 2, "Time": 1, "Charge": 3}, "ax": 530, "ay": 590},
    {"kind": "Hydra", "name": "Weeping Hydra of the Lantern Reach", "serial": "9F3C-01D2", "el": "Charge",
     "base": 1.1, "ceil": 9.2, "half": 42, "inv": 30, "holder": "Halcyon", "seat": "Rogue One",
     "lore": "Found weeping Charge in a dead lantern at the edge of the Reach.",
     "desc": "Splits on release into heads that fan out across your heading, each carrying part of the payload, and each cracks where it lands.",
     "trk": {"Mass": ["burst radius", 14, 40, "Each head cracks along its path as it flies."],
             "Space": ["range", 120, 360, "Each head turns toward the nearest rival super-shield ahead of it."],
             "Time": ["linger seconds", 0, 3, "The lingering bursts grow to twice their width."],
             "Charge": ["heads", 3, 6, "Each head splits again where it lands, into two more bursts beside it."]},
     "fixed": {"Mass": 2, "Space": 3, "Time": 0}, "ax": 700, "ay": 548},
    {"kind": "Aftershock", "name": "Hollow Aftershock of Tumult", "serial": "5D21-0C77", "el": "Time",
     "base": 0.6, "ceil": 7.4, "half": 55, "inv": 12, "holder": "", "seat": "",
     "lore": "Dug out of Tumult's floor, still ringing with old Time.",
     "desc": "Lands down your heading and cracks, then cracks again in the same place a few seconds later, catching whatever was rebuilt.",
     "trk": {"Mass": ["shock radius", 20, 50, "The last shock reaches twice as far."],
             "Space": ["throw distance", 0, 240, "Every shock after the first rings four smaller shocks around it."],
             "Time": ["seconds between shocks", 5, 2, "One more shock follows."],
             "Charge": ["shocks", 2, 4, "The later shocks shatter, leaving no shield behind."]},
     "fixed": {"Mass": 1, "Space": 2, "Charge": 3}, "ax": 1032, "ay": 560},
    {"kind": "Aegis", "name": "Patient Aegis of the Ninth Bloom", "serial": "E410-3F08", "el": "Mass",
     "base": 2.0, "ceil": 9.4, "half": 25, "inv": 0, "holder": "Mothwing", "seat": "Rogue Two",
     "lore": "Grown shut inside the Ninth Bloom, heavy with Mass.",
     "desc": "Release the payload beside your Ark and it super-shields the Ark's hull for a while, guarding everything aboard.",
     "trk": {"Mass": ["of the hull covered", 0.3, 0.8, "It covers the whole hull."],
             "Space": ["release reach", 120, 320, "It reaches home from anywhere."],
             "Time": ["seconds", 8, 40, "When it ends it leaves an ordinary shield behind."],
             "Charge": ["re-raises", 0, 3, "A cracked plate re-raises at once."]},
     "fixed": {"Space": 1, "Time": 3, "Charge": 0}, "ax": 1200, "ay": 576},
]

# Each kind's mark, in a 100 x 100 box: the room's own prism shapes, stroked.
GLYPH = {
    "Javelin": ["M50 2 L60 50 L50 98 L40 50 Z", "M50 2 L50 98"],
    "Hydra": ["M50 6 L61 28 L50 50 L39 28 Z", "M22 40 L33 62 L22 84 L11 62 Z", "M78 40 L89 62 L78 84 L67 62 Z"],
    "Aftershock": ["M50 4 L67 27 L50 50 L33 27 Z", "M50 50 L67 73 L50 96 L33 73 Z"],
    "Aegis": ["M50 6 L90 74 L10 74 Z", "M50 94 L10 26 L90 26 Z"],
}

HELMET = """<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:#8A8FAE}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.num{font-variant-numeric:tabular-nums}
.abs{position:absolute}
.sh{text-shadow:0 0 10px rgba(0,1,10,.95),0 0 3px rgba(0,1,10,.95),0 1px 2px rgba(0,1,10,.95)}
.sl{clip-path:polygon(12px 0,100% 0,100% calc(100% - 12px),calc(100% - 12px) 100%,0 100%,0 12px)}
.kc{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;color:#B8C0F0}
.pick{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}
.pick:focus-visible,.btn:focus-visible{outline:2px solid #00D4FF;outline-offset:3px}
.btn{box-sizing:border-box;display:inline-flex;align-items:center;gap:10px;height:44px;padding:0 18px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:15px;letter-spacing:.1em;text-transform:uppercase}
.btn .kc{border-color:currentColor;color:inherit;opacity:.85}
.cta{background:#99FF80;color:#00010A}
.ghost{background:rgba(0,1,10,.35);color:#E6E9FF;box-shadow:inset 0 0 0 1px rgba(230,233,255,.55)}
.off{background:rgba(0,1,10,.3);color:#5C5F70;box-shadow:inset 0 0 0 1px #222645}
.bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
.ell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
@keyframes feedA{0%{transform:translateY(0) scale(1);opacity:1}100%{transform:translateY(-260px) scale(.2);opacity:0}}
@keyframes feedB{0%{transform:translateY(0) scale(1);opacity:1}100%{transform:translateY(-260px) scale(.2);opacity:0}}
@keyframes rise{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}
@keyframes lensOpen{from{transform:scale(.18);opacity:0}to{transform:scale(1);opacity:1}}
@keyframes fly{from{stroke-dashoffset:300}to{stroke-dashoffset:0}}
@keyframes burst{0%,55%{r:0;opacity:0}70%{opacity:1}100%{r:26;opacity:0}}
@keyframes turn{from{transform:rotateY(0)}to{transform:rotateY(360deg)}}
</style>
</helmet>"""

# The Ark's band and tabs, as Rev 86 (kept: other rounds redesign them).
def chrome():
    nav = []
    for name, src in NAV:
        on = name == "ARK"
        mark = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        nav.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';'
                   f'font-weight:600;font-size:18px;letter-spacing:.12em;color:{"#E6E9FF" if on else "#747BAD"}">'
                   f'<img src="{src}" alt="" style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else 0.7}">{name}{mark}</span>')
    return (
        f'<img class="bg" src="{BACKDROP}" alt="The Artifacts room: the four artifacts on pedestals round a column of light, '
        f'alcoves down both walls, a porthole onto the cell">'
        '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,1,10,.7) 0%,rgba(0,1,10,0) 20%,rgba(0,1,10,0) 72%,rgba(0,1,10,.6) 100%);pointer-events:none"></div>'
        '<div class="abs" style="inset:0;background:rgba(0,1,10,{{dim}});transition:background 600ms ease;pointer-events:none"></div>'
        '<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.78);'
        'border-bottom:1px solid #222645;display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
        '<span class="kc">LT</span>' + "".join(nav) + '<span class="kc">RT</span></nav>'
        '<div class="abs" style="left:40px;right:40px;top:96px;height:52px;display:flex;align-items:center;gap:30px;'
        'border-bottom:1px solid rgba(116,123,173,.35);z-index:4">'
        '<button class="pick" type="button" onClick="{{say.tab}}" aria-label="Previous tab"><span class="kc">LB</span></button>'
        '<sc-for list="{{tabs}}" as="t" hint-placeholder-count="5"><button class="pick" type="button" onClick="{{t.pick}}" '
        'style="position:relative;display:flex;align-items:center;gap:8px;color:{{t.col}}"><span class="cap" '
        'style="font-size:15px;color:{{t.col}}">{{t.name}}</span><span class="abs" style="left:0;right:0;bottom:-15px;'
        'height:2px;background:{{t.mark}}"></span></button></sc-for>'
        '<button class="pick" type="button" onClick="{{say.tab}}" aria-label="Next tab"><span class="kc">RB</span></button>'
        '<span style="flex:1"></span>'
        # the levels, as a trail: where you are, one press back
        '<sc-for list="{{trail}}" as="c" hint-placeholder-count="3"><span class="cap" style="font-size:14px;color:{{c.col}}">{{c.sep}}{{c.name}}</span></sc-for>'
        '</div>'
    )


def glyph_svg(size, stroke, extra=""):
    """An artifact's mark, from the hole `g` (paths) with stroke colour hole."""
    return (f'<svg width="{size}" height="{size}" viewBox="0 0 100 100" fill="none" stroke="{stroke}" stroke-width="2.4" '
            f'stroke-linejoin="round" aria-hidden="true" style="overflow:visible;{extra}">'
            '<sc-for list="{{g.paths}}" as="p" hint-placeholder-count="2"><path d="{{p.d}}"></path></sc-for></svg>')


FOOT = (
    # pad hints, bottom right, plain text
    '<div class="abs sh" style="right:40px;bottom:28px;display:flex;gap:22px;align-items:center;z-index:6">'
    '<sc-for list="{{hints}}" as="h" hint-placeholder-count="3"><span style="display:flex;gap:8px;align-items:center;'
    'font-size:15px;color:#B8C0F0"><span class="kc">{{h.k}}</span>{{h.t}}</span></sc-for></div>'
    '<sc-if value="{{hasToast}}" hint-placeholder-val="{{ false }}"><div class="abs sh" role="status" style="left:50%;'
    'bottom:84px;transform:translateX(-50%);max-width:900px;text-align:center;font-size:18px;line-height:1.4;'
    'color:#E6E9FF;animation:rise 260ms ease both;z-index:7">{{toast}}</div></sc-if>'
)

# ---- the details every option shows, as one block of markup with holes (layout differs per option) ----
def details(width, align="left"):
    return f"""
<div style="width:{width}px;display:flex;flex-direction:column;gap:14px;text-align:{align};animation:rise 420ms ease both">
  <span class="lab sh">{{{{cur.kind}}}} - {{{{cur.serial}}}}</span>
  <span class="sh" style="font-size:36px;line-height:1.15;color:#E6E9FF">{{{{cur.name}}}}</span>
  <span class="sh" style="font-size:18px;line-height:1.5;color:#B8C0F0">{{{{cur.desc}}}}</span>
  <div style="display:flex;align-items:center;gap:12px;justify-content:{'flex-start' if align == 'left' else 'center'}">
    <img src="{{{{cur.petal}}}}" alt="" style="width:30px;height:30px">
    <span class="sh" style="font-size:20px;color:#E6E9FF">{{{{cur.el}}}} - {{{{cur.aspect}}}}</span>
    <span class="sh num" style="font-size:20px;color:#00D4FF">{{{{cur.val}}}}</span>
  </div>
  <span class="sh" style="font-size:15px;color:#8A8FAE">{{{{cur.held}}}}</span>
</div>"""


def meter(width):
    """The attuned track as one line: level now, the next feed's ghost, the twist at 5."""
    return f"""
<div style="width:{width}px;display:flex;flex-direction:column;gap:10px">
  <div style="display:flex;justify-content:space-between;align-items:baseline">
    <span class="lab sh">Level</span>
    <span class="sh num" style="font-size:22px;color:#E6E9FF">{{{{cur.lvl}}}}<span style="color:#99FF80">{{{{cur.lvlNext}}}}</span></span>
  </div>
  <div style="position:relative;height:6px;background:rgba(116,123,173,.3)">
    <div class="abs" style="left:0;top:0;bottom:0;width:{{{{cur.nextW}}}};background:rgba(153,255,128,.45)"></div>
    <div class="abs" style="left:0;top:0;bottom:0;width:{{{{cur.lvlW}}}};background:#00D4FF;transition:width 700ms cubic-bezier(.22,.8,.25,1)"></div>
    <div class="abs" style="left:50%;top:-6px;bottom:-6px;width:2px;background:{{{{cur.twistMark}}}}"></div>
    <div class="abs" style="left:{{{{cur.ceilX}}}};top:-4px;bottom:-4px;width:1px;background:#747BAD"></div>
  </div>
  <span class="sh" style="font-size:15px;line-height:1.45;color:{{{{cur.twistCol}}}}">At 5: {{{{cur.twist}}}}</span>
  <span class="sh num" style="font-size:15px;color:#8A8FAE">{{{{cur.valLine}}}}</span>
</div>"""


CRYSTALS = """
<div style="display:flex;flex-direction:column;gap:12px;align-items:{align}">
  <div style="display:flex;gap:6px;align-items:flex-end;height:58px">
    <sc-for list="{{{{cry}}}}" as="k" hint-placeholder-count="10"><img src="{{{{k.src}}}}" alt="" style="width:{size}px;height:{size}px;object-fit:contain;opacity:{{{{k.op}}}};animation:{{{{k.anim}}}}"></sc-for>
  </div>
  <span class="sh num" style="font-size:15px;color:#B8C0F0">{{{{cur.balLine}}}}</span>
  <div style="display:flex;gap:12px">
    <button class="btn sl {{{{feedCls}}}}" type="button" onClick="{{{{feed}}}}"><span class="kc">A</span>Feed {{{{batch}}}} {{{{cur.el}}}}</button>
    <button class="btn sl {{{{omniCls}}}}" type="button" onClick="{{{{feedOmni}}}}"><span class="kc">Y</span>Feed {{{{batch}}}} Omni</button>
  </div>
</div>"""


def crystals(size=44, align="flex-start"):
    return CRYSTALS.format(size=size, align=align)


def fixed_rows(col="#8A8FAE"):
    return f"""
<div style="display:flex;flex-direction:column;gap:8px">
  <span class="lab sh">Rolled with it, fixed</span>
  <sc-for list="{{{{cur.fixed}}}}" as="f" hint-placeholder-count="3"><div style="display:flex;align-items:center;gap:10px">
    <img src="{{{{f.petal}}}}" alt="" style="width:20px;height:20px;opacity:.7"><span class="sh" style="font-size:15px;color:{col};flex:1">{{{{f.label}}}}</span>
    <span class="sh num" style="font-size:15px;color:{col}">{{{{f.val}}}}</span></div></sc-for>
</div>"""


GIVE = ('<button class="btn sl cta" type="button" onClick="{{give}}"><span class="kc">X</span>{{cur.giveLabel}}</button>')

# ---------------- option A: the words hang in the room ----------------
BODY_A = """
<sc-for list="{{arts}}" as="g" hint-placeholder-count="4">
  <button class="pick abs" type="button" onClick="{{g.pick}}" aria-label="{{g.aria}}" aria-pressed="{{g.pressed}}" style="left:{{g.lx}}px;top:{{g.ly}}px;width:240px;margin-left:-120px;display:flex;flex-direction:column;align-items:center;gap:4px;opacity:{{g.op}};transition:opacity 500ms ease;z-index:3">
    <span class="cap sh" style="font-size:{{g.fs}}px;color:{{g.col}}">{{g.kind}}</span>
    <span class="sh" style="font-size:14px;color:#8A8FAE">{{g.sub}}</span>
    <span style="width:1px;height:{{g.lead}}px;background:{{g.leadCol}}"></span>
    <span style="width:9px;height:9px;border-radius:50%;border:2px solid {{g.leadCol}}"></span>
  </button>
</sc-for>
<span class="abs sh lab" style="left:80px;bottom:92px;opacity:{{restOp}}">4 found - 8 alcoves wait for the rest</span>
<sc-if value="{{showCur}}" hint-placeholder-val="{{ true }}">
  <div class="abs" style="left:{{cur.ax}}px;top:{{cur.ay}}px;width:{{cur.leadW}}px;height:1px;background:linear-gradient(90deg,rgba(0,212,255,.9),rgba(0,212,255,.15));transform-origin:0 0;transform:rotate({{cur.leadA}}deg);pointer-events:none"></div>
  <div class="abs" style="left:1330px;top:232px;width:540px;display:flex;flex-direction:column;gap:26px;z-index:4">
    """ + details(540) + """
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:12px;animation:rise 420ms ease both">
      <button class="btn sl ghost" type="button" onClick="{{toFeed}}"><span class="kc">A</span>Feed it</button>""" + GIVE + """
    </div>""" + fixed_rows() + """</sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:22px;animation:rise 420ms ease both">""" + meter(520) + """</div></sc-if>
  </div>
</sc-if>
<sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}">
  <div class="abs" style="left:330px;top:880px;z-index:4">""" + crystals(48) + """</div>
</sc-if>
"""

# ---------------- option B: the lens ----------------
LENS = """
<div class="abs" style="left:1180px;top:196px;width:640px;height:640px;animation:lensOpen 620ms cubic-bezier(.22,.8,.25,1) both;transform-origin:{{lensFrom}};z-index:4">
  <div class="abs" style="inset:0;border-radius:50%;background:radial-gradient(circle at 50% 42%,rgba(34,38,69,.92) 0%,rgba(0,4,31,.94) 62%,rgba(0,1,10,.96) 100%);box-shadow:0 0 0 3px rgba(0,212,255,.75),0 0 0 14px rgba(0,1,10,.75),0 0 0 15px rgba(0,212,255,.35),0 40px 90px rgba(255,174,0,.10)"></div>
  <svg class="abs" style="inset:0" width="640" height="640" viewBox="0 0 640 640" aria-hidden="true">
    <defs><clipPath id="lensClip"><circle cx="320" cy="320" r="300"></circle></clipPath>
      <pattern id="hex" width="56" height="48" patternUnits="userSpaceOnUse"><path d="M14 0 L42 0 L56 24 L42 48 L14 48 L0 24 Z" fill="none" stroke="rgba(137,59,255,.16)" stroke-width="1"></path></pattern></defs>
    <g clip-path="url(#lensClip)">
      <rect width="640" height="640" fill="url(#hex)"></rect>
      <sc-for list="{{holo.prisms}}" as="p" hint-placeholder-count="40"><rect x="{{p.x}}" y="{{p.y}}" width="22" height="12" fill="rgba(166,0,255,.35)" stroke="{{p.c}}" stroke-width="1.4" transform="rotate({{p.r}} {{p.cx}} {{p.cy}})"></rect></sc-for>
      <sc-for list="{{holo.paths}}" as="p" hint-placeholder-count="4"><path d="{{p.d}}" fill="none" stroke="#00D4FF" stroke-width="3" stroke-linecap="round" stroke-dasharray="300" style="animation:fly 2.4s ease-out {{p.delay}} infinite"></path></sc-for>
      <sc-for list="{{holo.bursts}}" as="b" hint-placeholder-count="4"><circle cx="{{b.x}}" cy="{{b.y}}" r="0" fill="rgba(0,212,255,.18)" stroke="#E6E9FF" stroke-width="2" style="animation:burst 2.4s ease-out {{b.delay}} infinite"></circle></sc-for>
      <path d="M296 600 L320 556 L344 600 Z" fill="rgba(230,233,255,.15)" stroke="#E6E9FF" stroke-width="2"></path>
    </g>
    <sc-for list="{{holo.arcs}}" as="a" hint-placeholder-count="4"><path d="{{a.track}}" fill="none" stroke="rgba(116,123,173,.35)" stroke-width="6"></path><path d="{{a.fill}}" fill="none" stroke="{{a.col}}" stroke-width="6" style="transition:all 700ms ease"></path></sc-for>
  </svg>
  <sc-for list="{{holo.arcs}}" as="a" hint-placeholder-count="4"><div class="abs" style="left:{{a.lx}}px;top:{{a.ly}}px;transform:translate(-50%,-50%);display:flex;flex-direction:column;align-items:center;gap:2px;width:150px;opacity:{{a.op}}">
    <img src="{{a.petal}}" alt="" style="width:24px;height:24px"><span class="sh num" style="font-size:15px;color:#E6E9FF;text-align:center">{{a.val}}</span><span class="sh" style="font-size:14px;color:#8A8FAE;text-align:center">{{a.label}}</span></div></sc-for>
</div>"""

BODY_B = """
<div class="abs" style="left:60px;right:60px;bottom:96px;height:150px;z-index:4">
  <div class="abs" style="left:0;right:0;top:56px;height:2px;background:linear-gradient(90deg,rgba(0,212,255,0),rgba(0,212,255,.55) 20%,rgba(0,212,255,.55) 80%,rgba(0,212,255,0))"></div>
  <div class="abs" style="left:110px;top:0;display:flex;gap:36px">
    <sc-for list="{{arts}}" as="g" hint-placeholder-count="4"><button class="pick" type="button" onClick="{{g.pick}}" aria-label="{{g.aria}}" aria-pressed="{{g.pressed}}" style="width:150px;display:flex;flex-direction:column;align-items:center;gap:10px">
      <span style="width:112px;height:112px;border-radius:50%;display:flex;align-items:center;justify-content:center;background:radial-gradient(circle,rgba(34,38,69,.9),rgba(0,1,10,.92));box-shadow:0 0 0 {{g.ringW}} {{g.ring}}">""" + glyph_svg(64, "{{g.col}}") + """</span>
      <span class="cap sh" style="font-size:15px;color:{{g.col}}">{{g.kind}}</span></button></sc-for>
    <sc-for list="{{empties}}" as="e" hint-placeholder-count="3"><span style="width:112px;height:112px;margin:0 19px;border-radius:50%;box-shadow:inset 0 0 0 1px rgba(116,123,173,.35);display:flex;align-items:center;justify-content:center;color:#747BAD;font-size:28px">?</span></sc-for>
  </div>
</div>
<sc-if value="{{showCur}}" hint-placeholder-val="{{ true }}">""" + LENS + """
  <div class="abs" style="left:80px;top:232px;width:560px;display:flex;flex-direction:column;gap:26px;z-index:4">""" + details(560) + """
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:12px;animation:rise 420ms ease both">
      <button class="btn sl ghost" type="button" onClick="{{toFeed}}"><span class="kc">A</span>Feed it</button>""" + GIVE + """</div></sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:24px;animation:rise 420ms ease both">""" + meter(520) + crystals(42) + """</div></sc-if>
  </div>
</sc-if>
<sc-if value="{{isRest}}" hint-placeholder-val="{{ false }}"><span class="abs sh" style="left:170px;bottom:270px;font-size:20px;color:#B8C0F0">Pick one to see what it does.</span></sc-if>
"""

# ---------------- option C: one at a time ----------------
BODY_C = """
<sc-if value="{{showCur}}" hint-placeholder-val="{{ true }}">
  <div class="abs" style="left:50%;top:210px;width:420px;height:420px;margin-left:-210px;perspective:900px;z-index:3;pointer-events:none">
    <div style="width:420px;height:420px;display:flex;align-items:center;justify-content:center;animation:rise 600ms ease both">
      <sc-for list="{{big}}" as="g" hint-placeholder-count="1">""" + glyph_svg(300, "#00D4FF", "filter:drop-shadow(0 0 18px rgba(0,212,255,.55));animation:turn 14s linear infinite") + """</sc-for>
    </div>
  </div>
  <div class="abs" style="left:80px;top:240px;width:470px;display:flex;flex-direction:column;gap:18px;z-index:4;animation:rise 420ms ease both">
    <span class="lab sh">{{cur.kind}} - {{cur.serial}}</span>
    <span class="sh" style="font-size:36px;line-height:1.12;color:#E6E9FF">{{cur.name}}</span>
    <span class="sh" style="font-size:18px;line-height:1.5;color:#B8C0F0;font-style:italic">{{cur.lore}}</span>
    <span class="sh" style="font-size:15px;color:#8A8FAE">{{cur.held}}</span>
  </div>
  <div class="abs" style="left:1390px;top:220px;width:450px;display:flex;flex-direction:column;gap:20px;z-index:4;animation:rise 420ms ease both">
    <span class="sh" style="font-size:18px;line-height:1.5;color:#B8C0F0">{{cur.desc}}</span>
    <div style="display:flex;align-items:center;gap:12px"><img src="{{cur.petal}}" alt="" style="width:30px;height:30px"><span class="sh" style="font-size:20px;color:#E6E9FF">{{cur.el}} - {{cur.aspect}}</span><span class="sh num" style="font-size:20px;color:#00D4FF">{{cur.val}}</span></div>
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}">""" + fixed_rows() + """</sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}">""" + meter(450) + """</sc-if>
  </div>
</sc-if>
<div class="abs" style="left:0;right:0;bottom:96px;display:flex;flex-direction:column;align-items:center;gap:18px;z-index:4">
  <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}">""" + crystals(46, "center") + """</sc-if>
  <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:12px"><button class="btn sl ghost" type="button" onClick="{{toFeed}}"><span class="kc">A</span>Feed it</button>""" + GIVE + """</div></sc-if>
  <div style="display:flex;align-items:center;gap:40px">
    <button class="pick" type="button" onClick="{{prevArt}}" aria-label="Previous artifact"><span class="kc">&lt;</span></button>
    <sc-for list="{{arts}}" as="g" hint-placeholder-count="4"><button class="pick" type="button" onClick="{{g.pick}}" aria-pressed="{{g.pressed}}" aria-label="{{g.aria}}" style="position:relative;display:flex;flex-direction:column;align-items:center;gap:4px">
      <span class="cap sh" style="font-size:{{g.fs}}px;color:{{g.col}}">{{g.kind}}</span><span style="width:100%;height:2px;background:{{g.under}}"></span></button></sc-for>
    <span class="cap sh" style="font-size:15px;color:#747BAD">+ 8 to find</span>
    <button class="pick" type="button" onClick="{{nextArt}}" aria-label="Next artifact"><span class="kc">&gt;</span></button>
  </div>
  <sc-if value="{{isRest}}" hint-placeholder-val="{{ false }}"><span class="sh" style="font-size:18px;color:#B8C0F0;max-width:900px;text-align:center">{{cur.desc}}</span></sc-if>
</div>
"""

LOGIC = r"""
const ARTS = __ARTS__;
const GLYPH = __GLYPH__;
const PET = __PET__;
const CRY = __CRY__;
const BATCH = 5;
const ELS = ['Mass', 'Space', 'Time', 'Charge'];
const OPTION = '__OPTION__';
const TABS = ['Converter', 'Squads', 'Pilots', 'Artifacts', 'Decor'];
const lvlOf = (a, inv) => a.base + (a.ceil - a.base) * inv / (inv + a.half);
const valOf = (t, L) => t[1] + (t[2] - t[1]) * L / 10;
const INTS = ['heads', 'shocks', 're-raises', 'cracks before it stops'];
const fmt = (t, v) => (INTS.indexOf(t[0]) >= 0 ? String(Math.round(v)) : t[0].indexOf('hull') >= 0 ? Math.round(v * 100) + '%' : (Math.abs(t[2] - t[1]) >= 50 ? String(Math.round(v)) : v.toFixed(1)));
const f1 = (x) => x.toFixed(1);
class Component extends DCLogic {
 constructor(props) { super(props); this.state = { lvl: OPTION === 'C' ? 1 : 0, sel: 1, inv: ARTS.map((a) => a.inv),
   bal: { Mass: 10, Space: 10, Time: 10, Charge: 10, Omni: 10 }, fed: 0, toast: '' }; this.t = null; }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x }); this.t = setTimeout(() => this.setState({ toast: '' }), 3200); }
 doFeed(omni) {
  const st = this.state, a = ARTS[st.sel], k = omni ? 'Omni' : a.el;
  if (st.bal[k] < BATCH) { this.say('Not enough ' + k + ' crystals: ' + st.bal[k] + ' held, a feed takes ' + BATCH + '. The Converter makes more.'); return; }
  const inv = st.inv.slice(); inv[st.sel] += BATCH; const bal = Object.assign({}, st.bal); bal[k] -= BATCH;
  this.setState({ inv, bal, fed: st.fed + 1, lastOmni: !!omni }); }
 renderVals() {
  const st = this.state, a = ARTS[st.sel], inv = st.inv[st.sel];
  const L = lvlOf(a, inv), Ln = lvlOf(a, inv + BATCH), t = a.trk[a.el];
  const pick = (i) => () => { if (i === st.sel && st.lvl >= 1) return; this.setState({ sel: i, lvl: 1, toast: '' }); };
  const leadA = { 0: -16, 1: -18, 2: -22, 3: -26 };
  const arts = ARTS.map((g, i) => { const on = i === st.sel && st.lvl >= 1; const rest = st.lvl === 0;
   const ly = [372, 300, 300, 372][i];
   return { kind: g.kind, sub: rest ? (g.holder ? (g.holder === 'You' ? 'Yours' : g.holder + "'s") : 'In the hoard') : '',
    pick: pick(i), aria: g.name, pressed: on ? 'true' : 'false',
    col: on || rest || OPTION === 'C' && i === st.sel ? '#E6E9FF' : '#9AA0CC', op: rest || on ? 1 : (OPTION === 'A' ? 0.45 : 1),
    fs: on || (OPTION === 'C' && i === st.sel) ? 20 : 16, lx: g.ax, ly, lead: g.ay - ly - 64,
    leadCol: on ? '#00D4FF' : 'rgba(230,233,255,.45)', ring: on ? '#00D4FF' : 'rgba(116,123,173,.45)', ringW: on ? '2px' : '1px',
    under: i === st.sel ? '#00D4FF' : 'transparent', paths: GLYPH[g.kind].map((d) => ({ d })) }; });
  const ax = a.ax, ay = a.ay, dx = 1320 - ax, dy = 280 - ay;
  const balLine = a.el + ' crystals ' + st.bal[a.el] + ' - Omni ' + st.bal.Omni + ' - a feed takes ' + BATCH;
  const held = a.holder ? (a.holder === 'You' ? 'Carried by you, in the ' + a.seat + ' seat' : 'Carried by ' + a.holder + ', in the ' + a.seat + ' seat') : 'In your hoard - no seat carries it';
  const fixed = ELS.filter((e) => e !== a.el).map((e) => ({ petal: PET[e], label: e + ' - ' + a.trk[e][0], val: fmt(a.trk[e], valOf(a.trk[e], a.fixed[e])) }));
  const cur = { kind: a.kind, serial: a.serial, name: a.name, desc: a.desc, lore: a.lore, el: a.el, petal: PET[a.el], aspect: t[0],
   val: fmt(t, valOf(t, L)), held, fixed, lvl: f1(L), lvlNext: st.lvl === 2 ? '  > ' + f1(Ln) : '', lvlW: (L * 10) + '%',
   nextW: st.lvl === 2 ? (Ln * 10) + '%' : '0%', ceilX: (a.ceil * 10) + '%', twist: t[3],
   twistCol: L >= 5 ? '#E6E9FF' : '#747BAD', twistMark: L >= 5 ? '#99FF80' : '#747BAD',
   valLine: 'Next feed: ' + fmt(t, valOf(t, L)) + ' > ' + fmt(t, valOf(t, Ln)) + ' ' + t[0] + '. ' + inv + ' crystals fed so far.',
   balLine, giveLabel: a.holder ? 'Give to another seat' : 'Give to a seat', ax, ay,
   leadW: Math.round(Math.sqrt(dx * dx + dy * dy)), leadA: Math.round(Math.atan2(dy, dx) * 180 / Math.PI) };
  const key = st.lastOmni ? 'Omni' : a.el;
  const cry = [];
  for (let i = 0; i < 10; i++) { const left = st.bal[a.el] > i;
   const justFed = st.fed > 0 && !st.lastOmni && i >= st.bal[a.el] && i < st.bal[a.el] + BATCH;
   cry.push({ src: CRY[a.el], op: left ? 1 : (justFed ? 1 : 0.12), anim: justFed ? ((st.fed % 2 ? 'feedA' : 'feedB') + ' 700ms ease-in ' + ((i - st.bal[a.el]) * 70) + 'ms both') : 'none' }); }
  // the lens's hologram: the payload over a field of prisms, from the arc of the pick
  const rnd = (n) => { const x = Math.sin(n * 91.7 + st.sel * 13.1) * 43758.5; return x - Math.floor(x); };
  const prisms = []; for (let i = 0; i < 40; i++) { const x = 60 + rnd(i) * 500, y = 50 + rnd(i + 40) * 440;
   prisms.push({ x: Math.round(x), y: Math.round(y), cx: Math.round(x + 11), cy: Math.round(y + 6), r: Math.round(rnd(i + 9) * 180), c: rnd(i + 3) > 0.5 ? 'rgba(166,0,255,.8)' : 'rgba(255,174,0,.7)' }); }
  const paths = [], bursts = [];
  const lv = L;
  if (a.kind === 'Hydra') { const n = Math.round(valOf(a.trk.Charge, lv)); for (let i = 0; i < n; i++) { const ang = (-90 - 50 + 100 * i / Math.max(1, n - 1)) * Math.PI / 180;
    const ex = 320 + Math.cos(ang) * 330, ey = 560 + Math.sin(ang) * 330; paths.push({ d: 'M320 556 Q' + Math.round(320 + Math.cos(ang) * 120) + ' ' + Math.round(470 + Math.sin(ang) * 60) + ' ' + Math.round(ex) + ' ' + Math.round(ey), delay: (i * 0.08) + 's' });
    bursts.push({ x: Math.round(ex), y: Math.round(ey), delay: (i * 0.08) + 's' }); } }
  else if (a.kind === 'Javelin') { paths.push({ d: 'M320 556 L320 150', delay: '0s' }); [430, 330, 230].forEach((y, i) => bursts.push({ x: 320, y, delay: (0.2 * i) + 's' })); bursts.push({ x: 320, y: 150, delay: '0.6s' }); }
  else if (a.kind === 'Aftershock') { paths.push({ d: 'M320 556 Q320 380 320 300', delay: '0s' }); bursts.push({ x: 320, y: 300, delay: '0s' }); bursts.push({ x: 320, y: 300, delay: '0.9s' }); bursts.push({ x: 320, y: 300, delay: '1.4s' }); }
  else { paths.push({ d: 'M180 520 A160 160 0 0 1 460 520', delay: '0s' }); paths.push({ d: 'M150 540 A190 190 0 0 1 490 540', delay: '0.3s' }); bursts.push({ x: 320, y: 360, delay: '0.5s' }); }
  const arcPath = (a0, a1) => { const r = 318, p = (d) => [320 + r * Math.cos(d * Math.PI / 180), 320 + r * Math.sin(d * Math.PI / 180)].map(Math.round);
   const s = p(a0), e = p(a1); return 'M' + s[0] + ' ' + s[1] + ' A' + r + ' ' + r + ' 0 0 1 ' + e[0] + ' ' + e[1]; };
  const arcs = ELS.map((e, i) => { const a0 = 135 + i * 90 - 40, span = 80, lvE = e === a.el ? L : a.fixed[e];
   const mid = (a0 + span / 2) * Math.PI / 180;
   return { track: arcPath(a0, a0 + span), fill: arcPath(a0, a0 + Math.max(1, span * lvE / 10)), col: e === a.el ? '#00D4FF' : 'rgba(230,233,255,.55)',
    lx: Math.round(320 + 370 * Math.cos(mid)), ly: Math.round(320 + 370 * Math.sin(mid)), petal: PET[e], op: e === a.el ? 1 : 0.6,
    val: fmt(a.trk[e], valOf(a.trk[e], lvE)), label: a.trk[e][0] }; });
  const holo = { prisms, paths, bursts, arcs };
  const tabs = TABS.map((n) => ({ name: n, col: n === 'Artifacts' ? '#E6E9FF' : (n === 'Decor' ? '#5C5F70' : '#747BAD'), mark: n === 'Artifacts' ? '#E6E9FF' : 'transparent',
   pick: () => this.say(n === 'Artifacts' ? '' : 'Only the Artifacts tab is drawn in this round.') }));
  const names = ['Hoard', a.kind, 'Feed'];
  const first = OPTION === 'C' ? 1 : 0;
  const trail = names.slice(first, st.lvl + 1).map((n, i) => ({ name: n, sep: i ? '/  ' : '', col: i + first === st.lvl ? '#E6E9FF' : '#747BAD' }));
  const hints = st.lvl === 0 ? [{ k: 'A', t: 'Look' }] : (st.lvl === 1 ? [{ k: 'A', t: 'Feed' }, { k: 'X', t: 'Give' }, { k: 'B', t: OPTION === 'C' ? 'Ark' : 'Back' }] : [{ k: 'A', t: 'Feed ' + a.el }, { k: 'Y', t: 'Feed Omni' }, { k: 'B', t: 'Back' }]);
  if (OPTION === 'C' || st.lvl > 0) hints.unshift({ k: 'D-pad', t: 'Next artifact' });
  const step = (d) => () => this.setState({ sel: (st.sel + d + ARTS.length) % ARTS.length, toast: '' });
  return { tabs, trail, arts, cur, cry, holo, hints, big: [{ paths: GLYPH[a.kind].map((d) => ({ d })) }],
   empties: [0, 1, 2], lensFrom: (110 + 75 + st.sel * 186 - 1180) + 'px 760px',
   showBack: st.lvl > (OPTION === 'C' ? 1 : 0), isRest: st.lvl === 0, isArt: st.lvl === 1, isFeed: st.lvl === 2, showCur: st.lvl >= 1,
   restOp: st.lvl === 0 ? 1 : 0, dim: st.lvl === 0 ? 0 : (OPTION === 'C' ? 0.55 : 0.32), batch: String(BATCH),
   feedCls: st.bal[a.el] >= BATCH ? 'cta' : 'off', omniCls: st.bal.Omni >= BATCH ? 'ghost' : 'off',
   toast: st.toast, hasToast: st.toast !== '', say: { tab: () => this.say('LB / RB change tab; only Artifacts is drawn here.') },
   toFeed: () => this.setState({ lvl: 2, toast: '' }), feed: () => this.doFeed(false), feedOmni: () => this.doFeed(true),
   back: () => this.setState({ lvl: Math.max(OPTION === 'C' ? 1 : 0, st.lvl - 1), toast: '' }),
   prevArt: step(-1), nextArt: step(1),
   give: () => this.say('The Squads tab opens and asks which seat carries the ' + a.kind + '.') };
 }
}
"""

BACK = ('<sc-if value="{{showBack}}" hint-placeholder-val="{{ true }}"><button class="btn sl ghost abs" type="button" '
        'onClick="{{back}}" style="left:40px;top:164px;height:38px;font-size:14px;z-index:6"><span class="kc">B</span>Back</button></sc-if>')

OPTIONS = {
    "A": ("A - In the room: each artifact's words hang beside it, no box (option)", BODY_A),
    "B": ("B - The lens: a hologram of what it does, its tracks round the rim (recommended)", BODY_B),
    "C": ("C - One at a time: the artifact large in the room, a caption under it (option)", BODY_C),
}


def board(rnd, key, title, body, pet):
    data = {"__ARTS__": json.dumps(ARTS), "__GLYPH__": json.dumps(GLYPH), "__PET__": json.dumps(pet),
            "__CRY__": json.dumps(CRYSTAL), "__OPTION__": key}
    logic = LOGIC
    for k, v in data.items():
        logic = logic.replace(k, v)
    root = ('<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; '
            'color: #E6E9FF; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">')
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>Rev {rnd} {key} - {title.split(" - ", 1)[1]}</title>\n'
            '<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + HELMET + "\n" + root + chrome() + BACK + body + FOOT +
            '\n</div>\n</x-dc>\n<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":1080}}\'>\n'
            + logic + '</script>\n</body>\n</html>\n')


NOTES = [
    ("Asked", "Shombith, 2026-11-13: \"The UI cards on top of each screen. I do not like them at all. ... For the artifacts "
              "screen, lets see what you come up with.\" The backdrops stay (\"fire for now\"); the Ark takes its light, "
              "motion and design from the pilot scenes set inside the Ark."),
    ("What goes", "Rev 86's two cards: the list box on the left and the picked artifact's box on the right. No option draws a "
                  "box behind its words: text sits on the room with a soft shadow, and the only lines are a leader, a meter "
                  "and a lens rim. The mockup colour-set switch is gone too."),
    ("Three levels", "Every option breaks the tab into three steps, one press each, B to go back: the hoard (which artifacts "
                     "you have), one artifact (what it is and does, who carries it, Give), and feeding it (its element's "
                     "crystals and the level they buy). The level shows in the tab row's trail. Each level shows only what "
                     "it needs, so there is never a wall of words."),
    ("Real content", "The hoard is four unique artifacts as the game rolls them (INVENTORY.md 2): a kind, a name, a serial, "
                     "ONE attuned element on the diminishing curve, three fixed tracks. Values come from the shipped kinds. "
                     "Crystals start at 10 of each, Shombith's default for now; a feed takes 5 (ArkArtifactsPage.feedBatch). "
                     "Feed works on the boards: the crystals fly in and the level rises."),
    ("From the pilot scenes", "B's lens is Jugger's and Scales' dome: a round glass over the room in which the game itself "
                              "plays, prisms and trails in depth, the payload of the picked artifact doing its thing. A "
                              "warm light under the rim stands for the brass and lanterns. A and C keep the scenes' rule "
                              "that the words belong to the object: labels on the pedestals, a caption under the piece."),
    ("Recommended: B", "It is the only option that SHOWS what an artifact does (Hydra's heads fan out and burst; the number "
                       "of heads is the level), which is what a player needs before feeding one, and it is the most "
                       "HyperSea. A is the quietest and keeps the room whole but crowds when the hoard grows past what the "
                       "pedestals hold. C reads best on a phone (one big thing, one caption) but hides the hoard."),
    ("Not decided", "Where the eight empty slots show (A: the alcoves, B: empty lenses on the rail, C: a count). Whether Give "
                    "stays here or moves to the Squads tab only. The hoard past four: B's rail scrolls, A needs more "
                    "pedestals in the room."),
    ("For the build", "Over the built backdrop clip (ArkBackdrop): B's lens is one UI circle with a render texture of a "
                      "small payload scene, or a looped clip per kind for a first pass; the rail and words are the "
                      "existing ArkArtifactsPage fields, re-laid out (author_menu_ark.py). No new data."),
]


def esc(b):
    return b.replace("&", "&amp;").replace('"', "&quot;")


def notes(rnd):
    rows = "".join(
        f'<div style="display:grid;grid-template-columns:280px 1fr;gap:28px;padding:16px 0;border-top:1px solid #222645">'
        f'<span class="cap" style="font-size:15px;color:#8A8FAE">{h}</span><span style="font-size:18px;line-height:1.5">'
        f'{esc(b)}</span></div>' for h, b in NOTES)
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>Rev {rnd} - notes: no cards, three '
            'levels, the recommendation</title>\n<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + HELMET +
            '\n<div style="width: 1920px; height: 900px; position: relative; overflow: hidden; background: #00041F; color: '
            '#E6E9FF; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">'
            f'<span class="abs cap" style="left:80px;top:56px;font-size:28px">Rev {rnd} - the Artifacts tab without cards</span>'
            f'<div class="abs" style="left:80px;top:112px;width:1760px">{rows}</div></div>\n</x-dc>\n'
            '<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":900}}\'>\n'
            'class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n</script>\n</body>\n</html>\n')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("root")
    ap.add_argument("--round", type=int, default=105)
    args = ap.parse_args()
    out = Path(args.root) / "project"
    out.mkdir(parents=True, exist_ok=True)
    pet = petals()
    for key, (title, body) in OPTIONS.items():
        (out / f"R{args.round}{key}.dc.html").write_text(board(args.round, key, title, body, pet))
    (out / f"R{args.round}Notes.dc.html").write_text(notes(args.round))
    print("wrote", ", ".join(f"R{args.round}{k}" for k in [*OPTIONS, "Notes"]))


if __name__ == "__main__":
    main()
