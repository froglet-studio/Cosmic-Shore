"""Rev 117 - the Launch Console as six pages. Shared shell, theme kit and logic prelude."""
import math

CELL = "/_blob/423125b7f05629e899e1ec1f3e076f05"  # the menu cell, already in the canvas's store
HULL = {"Sparrow": "/_blob/a2a4139d8e2822885778740d4f2fb92e", "Dolphin": "/_blob/e4ebf079ba441cd7141a2c8bc092d8ea",
        "Squirrel": "/_blob/694ae84ec6db347c4f034c8e03b56991", "Rhino": "/_blob/48e972fa9d369a737bacf39f37baf59f",
        "Manta": "/_blob/ab51cbb598b5a1e912416244829d134c", "Serpent": "/_blob/215e6ad15de9f075e7a45e7640a12f09"}

PAGES = [("Mission", "Mission"), ("Pipeline", "Pipeline"), ("Builds", "Builds"),
         ("Release", "Release"), ("Signals", "Signals"), ("Theme", "Theme")]


def ico(d, size=20, sw=1.6):
    return ('<svg width="%d" height="%d" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="%s" '
            'stroke-linecap="square" stroke-linejoin="miter" aria-hidden="true">%s</svg>') % (size, size, sw, d)


I = {
    "Mission": '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="3"/><path d="M12 1v4M12 19v4M1 12h4M19 12h4"/>',
    "Pipeline": '<circle cx="6" cy="5" r="2.2"/><circle cx="6" cy="19" r="2.2"/><circle cx="18" cy="9" r="2.2"/><path d="M6 7.2v9.6M18 11.2c0 4-6 3-11 6"/>',
    "Builds": '<path d="M12 2l9 5v10l-9 5-9-5V7z"/><path d="M3 7l9 5 9-5M12 12v10"/>',
    "Release": '<path d="M5 19l14-14M9 5h10v10"/><path d="M3 21h6"/>',
    "Signals": '<path d="M2 12h4l3-7 4 14 3-7h6"/>',
    "Theme": '<path d="M12 3l9 9-9 9-9-9z"/><path d="M12 3v18"/>',
    "check": '<path d="M4 12.5l5 5L20 6.5"/>',
    "x": '<path d="M5 5l14 14M19 5L5 19"/>',
    "arrow": '<path d="M4 12h15M13 6l6 6-6 6"/>',
    "back": '<path d="M20 12H5M11 6l-6 6 6 6"/>',
    "ask": '<path d="M4 4h16v12H9l-5 4z"/><path d="M8 9h8M8 12h5"/>',
    "phone": '<path d="M7 2h10v20H7z"/><path d="M11 18h2"/>',
    "monitor": '<path d="M2 4h20v13H2z"/><path d="M8 21h8M12 17v4"/>',
    "globe": '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18"/>',
    "cloud": '<path d="M7 18h11a4 4 0 0 0 0-8 6 6 0 0 0-11.5 1.5A3.3 3.3 0 0 0 7 18z"/>',
    "server": '<path d="M3 4h18v6H3zM3 14h18v6H3z"/><path d="M7 7h1M7 17h1"/>',
    "link": '<path d="M10 14a4 4 0 0 0 5.6 0l3-3a4 4 0 0 0-5.6-5.6l-1 1"/><path d="M14 10a4 4 0 0 0-5.6 0l-3 3a4 4 0 0 0 5.6 5.6l1-1"/>',
    "lock": '<path d="M5 11h14v10H5z"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    "drive": '<path d="M8 3h8l6 11-4 7H6l-4-7z"/><path d="M8 3l6 11h8M6 21l6-11"/>',
    "people": '<circle cx="9" cy="8" r="3.5"/><path d="M2 21c0-4 3-7 7-7s7 3 7 7"/><path d="M16 4.5a3.5 3.5 0 0 1 0 7M18 14c2.5.8 4 3.4 4 7"/>',
    "bug": '<path d="M8 8h8v9a4 4 0 0 1-8 0z"/><path d="M9 8a3 3 0 0 1 6 0M4 12h4M16 12h4M4 18l4-2M20 18l-4-2M5 6l3 2M19 6l-3 2"/>',
    "spark": '<path d="M12 2v6M12 16v6M2 12h6M16 12h6M5 5l4 4M15 15l4 4M19 5l-4 4M9 15l-4 4"/>',
    "clock": '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    "merge": '<circle cx="6" cy="5" r="2.2"/><circle cx="6" cy="19" r="2.2"/><circle cx="18" cy="19" r="2.2"/><path d="M6 7.2v9.6M6 9c0 6 6 8 9.8 10"/>',
}

THEMES_CSS = """
[data-th=deep]{--g:#00041F;--gd:#00010A;--s:#222645;--sr:#434C89;--ln:#434C89;--tx:#E6E9FF;--ts:#747BAD;--to:#5C5F70;--cta:#99FF80;--ctaf:#99FF80;--on:#00041F;--dg:#FF4B3A;--sel:#00D4FF;--cm:#A600FF;--oncm:#FFFFFF;--data:#AABCFF;--sea1:#49B8FF;--sea2:#7F98FF;--sea3:#9C8CFF;--wash:#002042;--imgop:.42;--hullbg:#00041F}
[data-th=shore]{--g:#F4F5FF;--gd:#E6E9FF;--s:#FFFFFF;--sr:#DDE1F7;--ln:#B8C0F0;--tx:#00041F;--ts:#434C89;--to:#8A8FAE;--cta:#487C1F;--ctaf:#99FF80;--on:#00041F;--dg:#CD373A;--sel:#007D73;--cm:#893BFF;--oncm:#FFFFFF;--data:#434C89;--sea1:#7F98FF;--sea2:#B8C0F0;--sea3:#9C8CFF;--wash:#FFFFFF;--imgop:.10;--hullbg:#00041F}
[data-th=nocturne]{--g:#13010C;--gd:#050003;--s:#3C1D31;--sr:#70405F;--ln:#70405F;--tx:#F5E6EE;--ts:#A8869A;--to:#6C5A65;--cta:#C4F637;--ctaf:#C4F637;--on:#13010C;--dg:#FF8169;--sel:#65E1DF;--cm:#DF5CE5;--oncm:#13010C;--data:#7BA0FF;--sea1:#65E1DF;--sea2:#7BA0FF;--sea3:#DF5CE5;--wash:#210118;--imgop:.30;--hullbg:#13010C}
[data-th=dartfrog]{--g:#150100;--gd:#060000;--s:#3F200B;--sr:#754526;--ln:#754526;--tx:#F6E7DF;--ts:#AB8975;--to:#6D5C52;--cta:#C2F625;--ctaf:#C2F625;--on:#150100;--dg:#FF513A;--sel:#85E1C2;--cm:#C75EE5;--oncm:#150100;--data:#6EB5FF;--sea1:#85E1C2;--sea2:#6EB5FF;--sea3:#C75EE5;--wash:#260901;--imgop:.30;--hullbg:#150100}
"""

KIT_CSS = """
body{margin:0;background:#00010A}
.abs{position:absolute}
.root{position:relative;width:1920px;height:1080px;overflow:hidden;background:var(--gd);color:var(--tx);font-family:'Aldrich',sans-serif;font-size:18px;transition:background-color .5s ease,color .5s ease}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:var(--ts)}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.num{font-variant-numeric:tabular-nums}
.soft{color:var(--ts)}.off{color:var(--to)}
.h1{font-size:36px;line-height:1.1;margin:0;font-weight:400}
.h2{font-size:28px;line-height:1.15;margin:0;font-weight:400}
.h3{font-size:22px;line-height:1.2;margin:0;font-weight:400}
.sm{font-size:15px}
.sl{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.sls{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.pn{position:absolute;box-sizing:border-box;padding:var(--pad,28px);background:color-mix(in srgb,var(--g) 95%,transparent);box-shadow:inset 0 0 0 1px color-mix(in srgb,var(--ln) 80%,transparent);transition:background-color .5s ease}
.pn.raised{background:color-mix(in srgb,var(--s) 70%,var(--g))}
.btn{box-sizing:border-box;display:inline-flex;align-items:center;justify-content:center;gap:12px;height:56px;padding:0 26px;white-space:nowrap;border:0;cursor:pointer;text-decoration:none;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:18px;letter-spacing:.14em;text-transform:uppercase;clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px);transition:transform .12s cubic-bezier(.5,1,.89,1),filter .2s}
.btn.s{height:44px;padding:0 18px;font-size:14px}
.btn:active{transform:scale(.97)}
.cta{background:var(--ctaf);color:var(--on)}
.commit{background:var(--cm);color:var(--oncm)}
.def{background:transparent;color:var(--tx);box-shadow:inset 0 0 0 1px var(--ts)}
.def:hover{box-shadow:inset 0 0 0 1px var(--tx)}
.danger{background:transparent;color:var(--dg);box-shadow:inset 0 0 0 1px var(--dg)}
.glow{animation:glow 3.6s ease-in-out infinite}
@keyframes glow{0%,100%{filter:drop-shadow(0 0 6px color-mix(in srgb,var(--cta) 30%,transparent))}50%{filter:drop-shadow(0 0 18px color-mix(in srgb,var(--cta) 60%,transparent))}}
.pick{background:none;border:0;padding:0;margin:0;cursor:pointer;color:inherit;font:inherit;text-align:left}
.btn:focus-visible,.pick:focus-visible,.nav:focus-visible,.chip:focus-visible{outline:2px solid var(--sel);outline-offset:3px}
.chip{display:inline-flex;align-items:center;gap:8px;height:36px;padding:0 14px;border:0;cursor:pointer;background:transparent;color:var(--ts);font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.12em;text-transform:uppercase;box-shadow:inset 0 0 0 1px var(--ln);clip-path:polygon(8px 0,100% 0,100% calc(100% - 8px),calc(100% - 8px) 100%,0 100%,0 8px);transition:color .2s,box-shadow .2s,background-color .2s}
.chip:hover{color:var(--tx)}
.chip[aria-pressed=true]{color:var(--tx);box-shadow:inset 0 0 0 1px var(--sel);background:color-mix(in srgb,var(--sel) 12%,transparent)}
.st{display:inline-flex;align-items:center;gap:8px;height:26px;padding:0 10px;box-sizing:border-box;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase;border:1px solid;white-space:nowrap}
.st i{width:8px;height:8px;display:inline-block;background:currentColor}
.st.go{color:var(--cta)}.st.nogo{color:var(--dg)}.st.risk{color:var(--dg);border-style:dashed}.st.risk i{background:linear-gradient(135deg,currentColor 50%,transparent 50%)}
.st.run{color:var(--sel)}.st.run i{animation:blink 1.2s steps(2) infinite}.st.none{color:var(--to)}
@keyframes blink{50%{opacity:.2}}
.mono{font-family:'Chakra Petch',sans-serif;font-weight:500;letter-spacing:.04em}
.hr{height:1px;background:color-mix(in srgb,var(--ln) 70%,transparent)}
.hull{mix-blend-mode:screen;object-fit:contain;width:100%;height:100%}
.hullbox{background:var(--hullbg);overflow:hidden;position:relative}
/* the sea */
.bgimg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:var(--imgop);transition:opacity .6s ease;animation:breath 48s ease-in-out infinite alternate}
@keyframes breath{from{transform:scale(1.02)}to{transform:scale(1.08) translate(-12px,6px)}}
.wash{position:absolute;inset:0;background:radial-gradient(1100px 640px at 72% 18%,color-mix(in srgb,var(--wash) 80%,transparent),transparent 70%),linear-gradient(180deg,transparent 30%,var(--gd) 100%)}
.seawrap{position:absolute;left:0;top:0;width:3840px;height:1080px;animation:drift 140s linear infinite}
.seawrap path{fill:none;stroke-width:1.2}
@keyframes drift{from{transform:translateX(0)}to{transform:translateX(-1920px)}}
/* rail */
.rail{position:absolute;left:0;top:0;width:232px;height:1080px;box-sizing:border-box;padding:28px 0 28px;background:color-mix(in srgb,var(--g) 96%,transparent);box-shadow:inset -1px 0 0 color-mix(in srgb,var(--ln) 70%,transparent)}
.nav{position:relative;display:flex;align-items:center;gap:16px;height:52px;padding:0 28px;color:var(--ts);text-decoration:none;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:15px;letter-spacing:.14em;text-transform:uppercase;transition:color .2s,background-color .2s}
.nav:hover{color:var(--tx);background:color-mix(in srgb,var(--sr) 25%,transparent)}
.nav.on{color:var(--tx);background:color-mix(in srgb,var(--sel) 10%,transparent)}
.nav.on::before{content:'';position:absolute;left:0;top:10px;bottom:10px;width:2px;background:var(--sel)}
.top{position:absolute;left:232px;top:0;right:0;height:96px;display:flex;align-items:center;gap:20px;padding:0 32px;box-sizing:border-box}
.sw{width:30px;height:30px;border:0;cursor:pointer;padding:0;clip-path:polygon(6px 0,100% 0,100% calc(100% - 6px),calc(100% - 6px) 100%,0 100%,0 6px);background:linear-gradient(135deg,var(--g) 0 50%,var(--sel) 50% 100%);transition:transform .2s}
.sw:hover{transform:translateY(-2px)}
.swring{padding:3px;display:inline-flex}
/* motion */
.rise{animation:rise .7s cubic-bezier(.22,1,.36,1) both}
.d1{animation-delay:.05s}.d2{animation-delay:.12s}.d3{animation-delay:.19s}.d4{animation-delay:.26s}.d5{animation-delay:.33s}.d6{animation-delay:.40s}.d7{animation-delay:.47s}.d8{animation-delay:.54s}
@keyframes rise{from{opacity:0;transform:translateY(18px)}to{opacity:1;transform:none}}
.fade{animation:fade .5s ease both}
@keyframes fade{from{opacity:0}to{opacity:1}}
.draw{stroke-dasharray:2400;stroke-dashoffset:2400;animation:draw 2.6s .3s cubic-bezier(.4,0,.2,1) forwards}
@keyframes draw{to{stroke-dashoffset:0}}
.spin{animation:spin 1.1s linear infinite}
@keyframes spin{to{transform:rotate(360deg)}}
.pulse{animation:pulse 2.8s ease-out infinite}
@keyframes pulse{0%{transform:scale(.6);opacity:.9}100%{transform:scale(2.2);opacity:0}}
.lift{transition:transform .25s cubic-bezier(.33,1,.68,1),box-shadow .25s}
.lift:hover{transform:translateY(-3px)}
.slidein{animation:slidein .45s cubic-bezier(.22,1,.36,1) both}
@keyframes slidein{from{opacity:0;transform:translateY(40px)}to{opacity:1;transform:none}}
[data-mo=calm] .seawrap{animation-duration:420s}
[data-mo=calm] .bgimg,[data-mo=calm] .glow,[data-mo=calm] .pulse,[data-mo=calm] .flow{animation:none}
[data-mo=off] *,[data-mo=off] *::before,[data-mo=off] *::after{animation-duration:0s!important;animation-delay:0s!important;animation-iteration-count:1!important;transition:none!important}
[data-sea=off] .seawrap{display:none}
[data-dn=compact]{--pad:18px}
"""


def helmet(extra=""):
    return ('<helmet>\n<link rel="preconnect" href="https://fonts.googleapis.com">\n'
            '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">\n'
            '<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">\n'
            '<style>' + THEMES_CSS + KIT_CSS + extra + '</style>\n</helmet>\n')


def sea_paths():
    """Five slow sea lines that tile every 1920 px so the drift loops without a seam."""
    out = []
    specs = [(250, 34, 1, 0.0, "sea1", .40), (420, 22, 2, 1.3, "sea2", .30), (610, 40, 1, 2.2, "sea3", .26),
             (780, 18, 3, .7, "sea1", .22), (930, 28, 2, 2.9, "sea2", .30)]
    for base, amp, k, ph, col, op in specs:
        pts = []
        for x in range(0, 3841, 48):
            y = base + amp * math.sin(2 * math.pi * k * x / 1920 + ph) + amp * .35 * math.sin(2 * math.pi * (k + 2) * x / 1920 + ph * 2)
            pts.append("%d %.1f" % (x, y))
        out.append('<path d="M%s" style="stroke:var(--%s);opacity:%s"/>' % (" L".join(pts), col, op))
    return ('<div class="seawrap" aria-hidden="true"><svg width="3840" height="1080" viewBox="0 0 3840 1080">'
            + "".join(out) + '</svg></div>')


def shell(page, title, sub, main):
    nav = []
    for key, label in PAGES:
        cls = "nav on" if key == page else "nav"
        cur = ' aria-current="page"' if key == page else ""
        nav.append('<a class="%s" href="R117%s.dc.html"%s>%s<span>%s</span></a>' % (cls, key, cur, ico(I[key]), label))
    rail = ('<nav class="rail" aria-label="Launch Console">'
            '<div style="padding:0 28px 28px;display:flex;align-items:center;gap:14px">'
            '<svg width="34" height="34" viewBox="0 0 34 34" aria-hidden="true"><path d="M17 2l15 15-15 15L2 17z" fill="none" stroke="var(--tx)" stroke-width="1.6"/>'
            '<path d="M17 9l8 8-8 8-8-8z" fill="var(--sel)" opacity=".85"/></svg>'
            '<div><div class="lab" style="color:var(--tx);font-size:14px">Cosmic Shore</div><div class="sm soft">Launch Console</div></div></div>'
            + "".join(nav) +
            '<div class="abs" style="left:28px;right:28px;bottom:28px">'
            '<div class="lab" style="margin-bottom:8px">Stage</div><div class="sm">Beta Phase 1</div>'
            '<div class="hr" style="margin:16px 0"></div>'
            '<div style="display:flex;align-items:center;gap:12px"><span class="cap" style="width:36px;height:36px;display:inline-flex;align-items:center;justify-content:center;background:var(--sr);color:var(--tx);font-size:14px;clip-path:polygon(6px 0,100% 0,100% calc(100% - 6px),calc(100% - 6px) 100%,0 100%,0 6px)">SH</span>'
            '<div><div class="sm">Shombith</div><div class="lab" style="font-size:14px">Owner</div></div></div></div></nav>')
    top = ('<header class="top">'
           '<div style="flex:1;min-width:0"><h1 class="h2">' + title + '</h1><div class="sm soft" style="margin-top:6px">' + sub + '</div></div>'
           '<span class="st none" title="These mockups show made-up numbers">Sample data</span>'
           '<div style="display:flex;align-items:baseline;gap:8px"><span class="num" style="font-size:28px">104</span><span class="lab">days to Steam keys</span></div>'
           '<div style="display:flex;gap:6px" role="group" aria-label="Theme">'
           '<sc-for list="{{ui.themes}}" as="t" hint-placeholder-count="4"><span class="swring" style="box-shadow:{{t.ring}}"><button class="sw" data-th="{{t.id}}" aria-label="{{t.name}} theme" aria-pressed="{{t.pressed}}" title="{{t.name}}" onClick="{{t.pick}}"></button></span></sc-for></div>'
           '<button class="btn def s" type="button" onClick="{{ui.ask}}">' + ico(I["ask"], 18) + 'Ask</button>'
           '</header>')
    ask = ('<sc-if value="{{ui.askOpen}}" hint-placeholder-val="{{ false }}">'
           '<div class="pn raised sl slidein" style="right:32px;top:96px;width:520px;height:auto;z-index:20;box-shadow:inset 0 0 0 1px var(--sel),0 30px 80px rgba(0,0,0,.45)">'
           '<div class="lab">Ask about the launch</div>'
           '<div style="margin:14px 0;padding:14px 16px;background:color-mix(in srgb,var(--gd) 80%,transparent);font-size:15px">What still blocks the Alpha build on iOS?</div>'
           '<div class="sm" style="line-height:1.55">One thing: the TestFlight invite build fails at signing (export compliance is unanswered). '
           'Android, Windows and the Web Toybox are ready. Claude answers only from this console\'s status and checklist.</div>'
           '<div style="display:flex;gap:12px;margin-top:20px"><button class="btn cta s" type="button">Start a Claude session on it</button>'
           '<button class="btn def s" type="button" onClick="{{ui.ask}}">Close</button></div></div></sc-if>')
    return ('<div class="root" data-th="{{ui.th}}" data-mo="{{ui.mo}}" data-dn="{{ui.dn}}" data-sea="{{ui.sea}}" style="width:1920px;height:1080px">'
            '<img class="bgimg" src="' + CELL + '" alt="" style="display:{{ui.img}}">'
            '<div class="wash"></div>' + sea_paths() + rail + top +
            '<main class="abs" style="left:264px;top:112px;width:1624px;height:936px">' + main + '</main>' + ask + '</div>')


PRELUDE = r"""
const THEMES = [['deep', 'Deep Sea'], ['shore', 'Shore'], ['nocturne', 'Nocturne'], ['dartfrog', 'Dart Frog']];
const UI0 = { th: 'deep', mo: 'full', dn: 'comfort', sea: 'on', img: 'on', askOpen: false };
function uiVals(self) {
  const st = self.state;
  return { th: st.th, mo: st.mo, dn: st.dn, sea: st.sea, img: st.img === 'on' ? 'block' : 'none', askOpen: !!st.askOpen,
    ask: () => self.setState({ askOpen: !self.state.askOpen }),
    themes: THEMES.map(([id, name]) => ({ id: id, name: name, pressed: st.th === id ? 'true' : 'false',
      ring: st.th === id ? '0 0 0 2px var(--sel)' : '0 0 0 1px transparent', pick: () => self.setState({ th: id }) })) };
}
const on = (b) => b ? 'true' : 'false';
"""


def board(title, body, logic, extra_css=""):
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>' + title + '</title>\n'
            '<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + helmet(extra_css) + body + '\n</x-dc>\n'
            '<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":1080}}\'>\n'
            + PRELUDE + logic + '\n</script>\n</body>\n</html>\n')


def icon_switch(var, names, size=18, sw=1.6):
    """Holes escape markup, so a per-item icon is one <sc-if> per glyph, flagged in renderVals (icf())."""
    return "".join('<sc-if value="{{%s.i_%s}}" hint-placeholder-val="{{ false }}">%s</sc-if>' % (var, n, ico(I[n], size, sw)) for n in names)


PRELUDE += r"""
const ICONS = ['phone', 'monitor', 'globe', 'cloud', 'server', 'merge', 'drive', 'people', 'check', 'bug', 'link', 'lock', 'clock', 'spark', 'x'];
function icf(k) { const o = {}; ICONS.forEach((n) => { o['i_' + n] = n === k; }); return o; }
"""
