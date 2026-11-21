#!/usr/bin/env python3
"""Rev 121 - What's new (2026-11-21): the pop-up after an update and from Settings, four ways, featuring Voyages.

Usage: python3 r121_whats_new.py <root> 121
Writes R121A, B, C, D, Settings and Notes into <root>/project. The kit (helmet, the Home backdrop, the sea lines,
the hub band, page()) is Rev 101's, lifted as Rev 107 does, so the look matches the menu boards already picked.
Every image is a /_blob/ id the canvas already holds (the Ark, the HyperSea, the pilots' close-ups, the hulls,
Lantern Reach's chart).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r101_quests.py')).read()
_src = _src[:_src.index('# ---------- A: the star chart')]
sys.argv = [sys.argv[0], root]
exec(_src)  # STYLE, BACKDROP, HUB, page, SOFT_LINE, out


def blob(i):
    return '/_blob/' + i


ARK = blob('f2a0a3e9c284a960c539d417b46cd1eb')
GALAXY = blob('7559d7b030c6e949f7cde2d25ce5d9e7')
FOAM = blob('1837bd8d6221b1584ff604aecf5d8ae1')
LANTERN = blob('b96bc9cef23de987e4ed42294befc28a')   # Lantern Reach on the Sector tier (Rev 100)
FACE = {'BANDIT': blob('283a48123225eca8a9a42c41e606d174'), 'VOLT': blob('32a9eed26dda493c7334d13cddaf435e'),
        'TALON': blob('77e722ebf367c3b4093b2ae2778762e4')}
HULL = {'Serpent': blob('215e6ad15de9f075e7a45e7640a12f09'), 'Sparrow': blob('a2a4139d8e2822885778740d4f2fb92e'),
        'Squirrel': blob('694ae84ec6db347c4f034c8e03b56991')}

STYLE += (
    '.win{background:rgba(0,4,31,.96)}\n'
    '.edge{background:#434C89;padding:1px;box-sizing:border-box}\n'
    '.mask-b{-webkit-mask-image:linear-gradient(180deg,#000 55%,transparent 100%);mask-image:linear-gradient(180deg,#000 55%,transparent 100%)}\n'
    '.mask-r{-webkit-mask-image:radial-gradient(ellipse 60% 60% at 50% 50%,#000 45%,transparent 100%);mask-image:radial-gradient(ellipse 60% 60% at 50% 50%,#000 45%,transparent 100%)}\n'
    '.body{font-size:18px;line-height:1.5;color:#B8C0F0}\n'
    '.new{display:inline-block;width:8px;height:8px;background:#E6E9FF;transform:rotate(45deg);flex:none}\n'
    '@keyframes slideIn{from{transform:translateX(56px);opacity:0}to{transform:none;opacity:1}}\n'
    '@keyframes rise{from{transform:translateY(18px);opacity:0}to{transform:none;opacity:1}}\n'
    '.in{animation:slideIn .7s cubic-bezier(.22,1,.36,1) both}\n'
    '.rise{animation:rise .45s cubic-bezier(.22,1,.36,1) both}\n'
)

HOME = BACKDROP + HUB
SCRIM = '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,1,10,.74)"></div>'
CHEV = '<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="%s"></path></svg>'
DOWN, UP, LEFT, RIGHT = CHEV % 'M2 5l5 5 5-5', CHEV % 'M2 9l5-5 5 5', CHEV % 'M9 2L4 7l5 5', CHEV % 'M5 2l5 5-5 5'
NOLOGIC = '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n'

# ---------- the update, as the game would author it (every word an authored field in the build) ----------
UPDATE = {'ver': 'Update 0.3', 'name': 'Voyages', 'date': 'November 2026',
          'line': 'Take your crew aboard the Ark and sail the HyperSea.'}
FEAT = [
    {'title': 'Take a job', 'line': 'Your pilots bring you work: bounty hunts, couriers, raids and the Leviathan.'},
    {'title': 'Rival crews', 'line': 'Black Tide and other crews take the same jobs. Get there first.'},
    {'title': 'Sagas and relics', 'line': 'Follow a saga to its end and bring home a relic, like the Lantern of Oriel.'},
]
FIXES = ['A new voyage HUD, the same on PC, phone and controller.',
         'The Ark no longer waits at a stop forever.',
         'Escort wingmates keep their target after a pilot swap.']


def art(kind, w, h):
    """The picture for a feature, built from canvas images, sized to a w x h box (static markup)."""
    if kind == 'voyages':
        aw = int(w * 0.52)
        return (f'<img src="{GALAXY}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 55%;opacity:.6;filter:saturate(1.1) brightness(.85)">'
                f'<img src="{ARK}" alt="The Ark" style="position:absolute;left:{(w - aw) // 2}px;top:{int(h * 0.12)}px;width:{aw}px;height:{int(aw * 0.47)}px;object-fit:contain">')
    if kind == 'jobs':
        fw = int(min(w / 3.3, h * 0.9))
        gap = int((w - 3 * fw) / 4)
        return (f'<img src="{FOAM}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.28">'
                + ''.join(f'<img src="{FACE[c]}" alt="{c.title()}" class="mask-r" style="position:absolute;left:{gap + k * (fw + gap)}px;top:{(h - fw) // 2}px;width:{fw}px;height:{fw}px;object-fit:cover">'
                          for k, c in enumerate(['BANDIT', 'VOLT', 'TALON'])))
    if kind == 'rivals':
        hw = int(w * 0.3)
        return (f'<img src="{FOAM}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:30% 60%;opacity:.3">'
                + ''.join(f'<img src="{HULL[n]}" alt="{n}" class="hull" style="position:absolute;left:{int(w * x)}px;top:{int(h * y)}px;width:{hw}px;height:{int(hw * 0.42)}px">'
                          for n, x, y in [('Serpent', .06, .22), ('Sparrow', .36, .5), ('Squirrel', .64, .2)]))
    return (f'<img src="{LANTERN}" alt="Lantern Reach" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:38% 46%;opacity:.85">'
            f'<span class="dia" style="position:absolute;left:{int(w * .45) - 9}px;top:{int(h * .46) - 9}px;width:18px;height:18px;border:2px solid #E6E9FF;background:rgba(0,0,0,0);box-shadow:0 0 0 6px rgba(0,4,31,.6)"></span>')


def box(kind, w, h, extra=''):
    return f'<div style="position:relative;width:{w}px;height:{h}px;overflow:hidden;flex:none{extra}">{art(kind, w, h)}</div>'


def frame(x, y, w, h, inner, cls='rise'):
    return (f'<section role="dialog" aria-label="What&#8217;s new" class="abs {cls}" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">'
            f'<div class="abs sl edge" style="inset:0"><div class="sl win" style="width:100%;height:100%"></div></div>'
            f'<div class="abs" style="inset:0;overflow:hidden">{inner}</div></section>')


def toast(text_hole):
    return ('<sc-if value="{{toast.show}}" hint-placeholder-val="{{ false }}">'
            '<div class="abs rise" role="status" style="left:660px;top:120px;width:600px;height:56px;display:flex;align-items:center;justify-content:center;gap:16px">'
            '<div class="abs soft" style="inset:0"></div><span style="position:relative;font-size:18px">' + text_hole + '</span>'
            '<sc-if value="{{toast.again}}" hint-placeholder-val="{{ true }}"><button class="pick cap" type="button" onClick="{{toast.open}}" style="position:relative;font-size:14px;color:#99FF80">Open</button></sc-if></div></sc-if>')


TOAST_LOGIC = '''
  const toast = { show: !!st.toast, text: st.toast || '', again: st.toast === CLOSED, open: () => this.setState({ open: true, toast: '' }) };'''


def footer(close_hole_label, close_key='B', sail=True):
    s = ('<div class="abs" style="right:48px;bottom:40px;display:flex;gap:18px">'
         f'<button class="btn soft" type="button" onClick="{{{{close}}}}" style="width:220px"><span class="kc">{close_key}</span>{close_hole_label}</button>')
    if sail:
        s += '<button class="btn softcta" type="button" onClick="{{sail}}" style="width:260px"><span class="kc">A</span>Set sail</button>'
    return s + '</div>'


def fixes_toggle(left, bottom):
    return (f'<div class="abs" style="left:{left}px;bottom:{bottom}px;display:flex;flex-direction:column;gap:14px">'
            '<sc-if value="{{alsoOpen}}" hint-placeholder-val="{{ false }}"><div class="rise" style="display:flex;flex-direction:column;gap:10px;padding-left:2px">'
            + ''.join(f'<span class="body" style="font-size:15px;display:flex;gap:12px;align-items:baseline"><span class="dia" style="width:7px;height:7px;background:#747BAD"></span>{t}</span>' for t in FIXES)
            + '</div></sc-if>'
            '<button class="pick lab" type="button" aria-expanded="{{alsoExp}}" onClick="{{toggleAlso}}" style="display:flex;align-items:center;gap:10px;color:#E6E9FF">'
            'Also in this update &#160;<span class="num" style="color:#747BAD">3</span>'
            '<sc-if value="{{alsoOpen}}" hint-placeholder-val="{{ false }}">' + DOWN + '</sc-if>'
            '<sc-if value="{{alsoShut}}" hint-placeholder-val="{{ true }}">' + UP + '</sc-if></button></div>')


ALSO_LOGIC = '''
  const alsoOpen = !!st.also, alsoShut = !st.also, alsoExp = st.also ? 'true' : 'false';
  const toggleAlso = () => this.setState({ also: !st.also });'''


# ---------- A: the poster ----------
def window_a(close_label='Close'):
    W, H = 1260, 800
    hero = (box('voyages', W, 420, ';position:absolute;left:0;top:0') +
            '<div class="abs" aria-hidden="true" style="left:0;top:220px;width:1260px;height:200px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.96))"></div>'
            '<div class="abs" style="left:56px;top:300px;display:flex;flex-direction:column;gap:8px">'
            f'<span class="lab" style="display:flex;align-items:center;gap:12px"><span class="new"></span>{UPDATE["ver"]} &#160;&#183;&#160; New</span>'
            f'<span style="font-size:48px;line-height:1">{UPDATE["name"]}</span>'
            f'<span style="font-size:20px;color:#E6E9FF">{UPDATE["line"]}</span></div>')
    cols = ''.join(
        '<div style="display:flex;flex-direction:column;gap:12px;width:360px">'
        + box(k, 360, 130) + f'<span style="font-size:20px;margin-top:6px">{f["title"]}</span><span class="body" style="font-size:15px">{f["line"]}</span></div>'
        for k, f in zip(['jobs', 'rivals', 'relics'], FEAT))
    feats = f'<div class="abs" style="left:56px;top:452px;display:flex;gap:42px">{cols}</div>'
    return frame(330, 160, W, H, hero + feats + fixes_toggle(56, 44) + footer(close_label))


LOGIC_A = '''
const CLOSED = 'What\\u2019s new stays in Settings, under Other.';
class Component extends DCLogic {
constructor(p) { super(p); this.state = { open: true, also: false, toast: '' }; }
renderVals() {
  const st = this.state;''' + ALSO_LOGIC + TOAST_LOGIC + '''
  return { open: st.open, alsoOpen, alsoShut, alsoExp, toggleAlso, toast,
    close: () => this.setState({ open: false, also: false, toast: CLOSED }),
    sail: () => this.setState({ open: false, also: false, toast: 'Opening Voyages on the Ark.' }) };
}
}
'''


def board_a():
    body = (HOME + '<sc-if value="{{open}}" hint-placeholder-val="{{ true }}">' + SCRIM + window_a() + '</sc-if>'
            + toast('{{toast.text}}'))
    return page(f'Rev {REV} A - Poster: one window, the Ark over the HyperSea, three lines and Set sail (recommended) - interactive', body, LOGIC_A)


# ---------- B: pages ----------
PAGES = [{'kind': 'voyages', 'title': UPDATE['name'], 'line': UPDATE['line'], 'sub': 'Pick a voyage on the Ark, seat your squad and set sail. Every stop is a real match.'},
         {'kind': 'jobs', 'title': FEAT[0]['title'], 'line': FEAT[0]['line'], 'sub': 'Each pilot in your squad offers their own voyages, and each pays its own rewards.'},
         {'kind': 'rivals', 'title': FEAT[1]['title'], 'line': FEAT[1]['line'], 'sub': 'Rivals sail by the same rules you do. Beat them to a job, or meet them on the water.'},
         {'kind': 'relics', 'title': FEAT[2]['title'], 'line': FEAT[2]['line'], 'sub': 'A saga runs over several voyages. Its last chapter brings home a named relic.'}]


def board_b():
    W, H, AW = 1600, 860, 900
    pics = ''.join(f'<sc-if value="{{{{p{k}}}}}" hint-placeholder-val="{{{{ {"true" if k == 0 else "false"} }}}}"><div class="abs rise" style="left:0;top:0">{box(p["kind"], AW, H)}</div></sc-if>'
                   for k, p in enumerate(PAGES))
    fade = '<div class="abs" aria-hidden="true" style="left:640px;top:0;width:262px;height:860px;background:linear-gradient(90deg,rgba(0,4,31,0),rgba(0,4,31,.96))"></div>'
    text = ('<div class="abs" style="left:960px;top:64px;width:580px;display:flex;flex-direction:column">'
            f'<span class="lab" style="display:flex;align-items:center;gap:12px"><span class="new"></span>What&#8217;s new &#160;&#183;&#160; {UPDATE["ver"]}</span>'
            '<span class="num lab" style="margin-top:150px;color:#E6E9FF">{{count}}</span>'
            '<span style="font-size:48px;line-height:1.05;margin-top:14px">{{page.title}}</span>'
            '<span style="font-size:20px;line-height:1.45;margin-top:20px">{{page.line}}</span>'
            '<span class="body" style="margin-top:18px">{{page.sub}}</span></div>')
    skip = '<button class="btn sm soft" type="button" onClick="{{close}}" style="position:absolute;right:48px;top:52px;width:150px"><span class="kc">B</span>Skip</button>'
    dots = ('<nav aria-label="Pages" class="abs" style="left:960px;bottom:62px;display:flex;gap:14px;align-items:center">'
            '<sc-for list="{{dots}}" as="d" hint-placeholder-count="4"><button class="pick" type="button" aria-label="{{d.label}}" aria-current="{{d.cur}}" onClick="{{d.go}}" style="width:44px;height:20px;display:flex;align-items:center">'
            '<span style="display:block;width:{{d.w}};height:3px;background:{{d.c}};transition:width .3s ease"></span></button></sc-for></nav>')
    nav = ('<div class="abs" style="right:48px;bottom:40px;display:flex;gap:18px">'
           '<sc-if value="{{canBack}}" hint-placeholder-val="{{ false }}"><button class="btn soft" type="button" onClick="{{back}}" style="width:170px"><span class="kc">LB</span>Back</button></sc-if>'
           '<sc-if value="{{notLast}}" hint-placeholder-val="{{ true }}"><button class="btn soft" type="button" onClick="{{next}}" style="width:200px">Next<span class="kc">RB</span></button></sc-if>'
           '<sc-if value="{{last}}" hint-placeholder-val="{{ false }}"><button class="btn softcta" type="button" onClick="{{sail}}" style="width:260px"><span class="kc">A</span>Set sail</button></sc-if></div>')
    win = frame(160, 150, W, H, pics + fade + text + skip + dots + nav)
    logic = ('''
const PAGES = %s;
const CLOSED = 'What\\u2019s new stays in Settings, under Other.';
class Component extends DCLogic {
constructor(p) { super(p); this.state = { open: true, pg: 0, toast: '' }; }
renderVals() {
  const st = this.state, pg = st.pg, n = PAGES.length;''' % json.dumps([{k: p[k] for k in ('title', 'line', 'sub')} for p in PAGES]) + TOAST_LOGIC + '''
  const dots = PAGES.map((p, i) => ({ label: p.title, cur: i === pg ? 'page' : 'false', w: i === pg ? '44px' : '18px',
    c: i === pg ? '#E6E9FF' : '#434C89', go: () => this.setState({ pg: i }) }));
  return { open: st.open, toast, page: PAGES[pg], count: (pg + 1) + ' / ' + n, dots,
    p0: pg === 0, p1: pg === 1, p2: pg === 2, p3: pg === 3,
    canBack: pg > 0, notLast: pg < n - 1, last: pg === n - 1,
    back: () => this.setState({ pg: Math.max(0, pg - 1) }), next: () => this.setState({ pg: Math.min(n - 1, pg + 1) }),
    close: () => this.setState({ open: false, toast: CLOSED }),
    sail: () => this.setState({ open: false, toast: 'Opening Voyages on the Ark.' }) };
}
}
''')
    body = HOME + '<sc-if value="{{open}}" hint-placeholder-val="{{ true }}">' + SCRIM + win + '</sc-if>' + toast('{{toast.text}}')
    return page(f'Rev {REV} B - Pages: one feature a page, paged with LB / RB, Set sail on the last - interactive', body, logic)


# ---------- C: changelog ----------
# Only Update 0.3 is real content; 0.2 and 0.1 are placeholders that show how the list grows (notes say so).
LOG = [
    {'ver': 'Update 0.3', 'name': 'Voyages', 'date': 'November 2026', 'new': True, 'line': UPDATE['line'],
     'feats': FEAT, 'fixes': FIXES},
    {'ver': 'Update 0.2', 'name': 'Quests and colour sets', 'date': 'October 2026', 'new': False,
     'line': 'Five quests across the Arcade, and a colour set to earn from each.',
     'feats': [{'title': 'Quests', 'line': 'Each quest opens the next game and its harder intensities.'},
               {'title': 'Colour sets', 'line': 'Wear a new palette for every domain from your Profile.'},
               {'title': 'The Ark', 'line': 'Your squad and your pilots, in one place.'}],
     'fixes': ['Faster menus on phones.', 'Invert Y now reaches every vessel.']},
    {'ver': 'Update 0.1', 'name': 'The first beta', 'date': 'September 2026', 'new': False,
     'line': 'The Arcade, the Hangar and the Toybox, for friends and family.',
     'feats': [{'title': 'The Arcade', 'line': 'Time, Charge, Mass and Space games for every vessel.'},
               {'title': 'The Hangar', 'line': 'Every vessel, its abilities and its pilots.'},
               {'title': 'The Toybox', 'line': 'Fly anything, anywhere, with no score.'}],
     'fixes': []},
]


def board_c():
    W, H, LW = 1600, 860, 420
    head = ('<div class="abs" style="left:48px;top:44px;display:flex;flex-direction:column;gap:6px"><span class="lab">Settings &#160;&#183;&#160; Other</span>'
            '<span style="font-size:36px">What&#8217;s new</span></div>')
    lst = ('<nav aria-label="Updates" class="abs" style="left:32px;top:150px;width:' + str(LW - 32) + 'px;display:flex;flex-direction:column;gap:6px">'
           '<sc-for list="{{log}}" as="u" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{u.pressed}}" onClick="{{u.pick}}" '
           'style="position:relative;height:80px;padding:0 20px;display:flex;flex-direction:column;justify-content:center;gap:6px;background:{{u.bg}};box-shadow:{{u.edge}}">'
           '<span style="display:flex;align-items:center;gap:12px"><span class="lab" style="color:{{u.verInk}}">{{u.ver}}</span>'
           '<sc-if value="{{u.isNew}}" hint-placeholder-val="{{ true }}"><span class="cap" style="font-size:14px;color:#E6E9FF">New</span></sc-if></span>'
           '<span style="font-size:20px;color:{{u.ink}}">{{u.name}}</span></button></sc-for></nav>'
           f'<div class="abs" aria-hidden="true" style="left:{LW}px;top:40px;width:1px;height:780px;background:#434C89"></div>')
    RX = LW + 48
    hero = ''.join(f'<sc-if value="{{{{h{k}}}}}" hint-placeholder-val="{{{{ {"true" if k == 0 else "false"} }}}}"><div class="abs" style="left:{RX}px;top:44px">{box(kind, W - RX - 48, 260, ";-webkit-mask-image:linear-gradient(180deg,#000 50%,transparent 100%);mask-image:linear-gradient(180deg,#000 50%,transparent 100%)")}</div></sc-if>'
                   for k, kind in enumerate(['voyages', 'relics', 'jobs']))
    right = (hero + f'<div class="abs" style="left:{RX}px;top:250px;width:{W - RX - 48}px;display:flex;flex-direction:column">'
             '<span class="lab">{{sel.ver}} &#160;&#183;&#160; {{sel.date}}</span>'
             '<span style="font-size:44px;line-height:1.05;margin-top:8px">{{sel.name}}</span>'
             '<span style="font-size:18px;margin-top:12px">{{sel.line}}</span>'
             '<span class="lab" style="margin-top:34px">In this update</span>'
             '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));column-gap:36px;margin-top:14px">'
             '<sc-for list="{{sel.feats}}" as="f" hint-placeholder-count="3"><div style="display:flex;flex-direction:column;gap:8px;padding-top:14px;border-top:1px solid #434C89">'
             '<span style="font-size:20px">{{f.title}}</span><span class="body" style="font-size:15px">{{f.line}}</span></div></sc-for></div>'
             '<sc-if value="{{sel.hasFixes}}" hint-placeholder-val="{{ true }}"><span class="lab" style="margin-top:34px">Also in this update</span>'
             '<div style="display:flex;flex-direction:column;gap:8px;margin-top:12px"><sc-for list="{{sel.fixes}}" as="x" hint-placeholder-count="3">'
             '<span class="body" style="font-size:15px">{{x.t}}</span></sc-for></div></sc-if></div>')
    btns = ('<div class="abs" style="right:48px;bottom:40px;display:flex;gap:18px">'
            '<button class="btn soft" type="button" onClick="{{close}}" style="width:200px"><span class="kc">B</span>Close</button>'
            '<sc-if value="{{sel.isNew}}" hint-placeholder-val="{{ true }}"><button class="btn softcta" type="button" onClick="{{sail}}" style="width:260px"><span class="kc">A</span>Set sail</button></sc-if></div>')
    win = frame(160, 150, W, H, head + lst + right + btns)
    logic = ('''
const LOG = %s;
const CLOSED = 'What\\u2019s new stays in Settings, under Other.';
class Component extends DCLogic {
constructor(p) { super(p); this.state = { open: true, sel: 0, toast: '' }; }
renderVals() {
  const st = this.state, i = st.sel;''' % json.dumps(LOG) + TOAST_LOGIC + '''
  const log = LOG.map((u, k) => { const on = k === i; return { ver: u.ver, name: u.name, date: u.date, isNew: u.new,
    pressed: on ? 'true' : 'false', ink: on ? '#E6E9FF' : '#B8C0F0', verInk: on ? '#E6E9FF' : '#747BAD',
    bg: on ? 'linear-gradient(90deg,rgba(67,76,137,.35),rgba(67,76,137,0))' : 'rgba(0,0,0,0)', edge: on ? 'inset 2px 0 0 #00D4FF' : 'none',
    pick: () => this.setState({ sel: k }) }; });
  const u = LOG[i];
  const sel = { ver: u.ver, name: u.name, date: u.date, line: u.line, isNew: u.new, feats: u.feats,
    fixes: u.fixes.map((t) => ({ t })), hasFixes: u.fixes.length > 0 };
  return { open: st.open, toast, log, sel, h0: i === 0, h1: i === 1, h2: i === 2,
    close: () => this.setState({ open: false, toast: CLOSED }),
    sail: () => this.setState({ open: false, toast: 'Opening Voyages on the Ark.' }) };
}
}
''')
    body = HOME + '<sc-if value="{{open}}" hint-placeholder-val="{{ true }}">' + SCRIM + win + '</sc-if>' + toast('{{toast.text}}')
    return page(f'Rev {REV} C - Changelog: the updates listed on the left, the picked one on the right - interactive', body, logic)


# ---------- D: the card that slides open ----------
def board_d():
    card = ('<sc-if value="{{card}}" hint-placeholder-val="{{ true }}"><section aria-label="New in this update" class="abs in" style="right:64px;bottom:64px;width:520px;height:212px">'
            '<div class="abs sl edge" style="inset:0"><div class="sl win" style="width:100%;height:100%"></div></div>'
            '<button class="pick" type="button" onClick="{{expand}}" aria-label="See what&#8217;s new" style="position:absolute;inset:0"></button>'
            '<div class="abs" style="left:0;top:0;width:200px;height:212px;overflow:hidden;pointer-events:none">' + box('voyages', 200, 212) + '</div>'
            '<div class="abs" style="left:224px;top:26px;width:270px;display:flex;flex-direction:column;gap:6px;pointer-events:none">'
            f'<span class="lab" style="display:flex;align-items:center;gap:10px"><span class="new"></span>New in {UPDATE["ver"]}</span>'
            f'<span style="font-size:28px">{UPDATE["name"]}</span><span class="body" style="font-size:15px">Sail the HyperSea with your crew.</span></div>'
            '<div class="abs" style="left:224px;bottom:22px;display:flex;gap:12px">'
            '<button class="btn sm soft" type="button" onClick="{{later}}" style="width:120px">Later</button>'
            '<button class="btn sm softcta" type="button" onClick="{{expand}}" style="width:150px"><span class="kc">Y</span>See it</button></div></section></sc-if>')
    W, H = 720, 940
    rows = ''.join(
        f'<div style="display:flex;gap:22px;align-items:center;padding:16px 0;border-top:1px solid #434C89">{box(k, 150, 84)}'
        f'<div style="display:flex;flex-direction:column;gap:6px"><span style="font-size:20px">{f["title"]}</span><span class="body" style="font-size:15px">{f["line"]}</span></div></div>'
        for k, f in zip(['jobs', 'rivals', 'relics'], FEAT))
    inner = (box('voyages', W, 340, ';position:absolute;left:0;top:0') +
             '<div class="abs" aria-hidden="true" style="left:0;top:180px;width:720px;height:160px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.96))"></div>'
             '<button class="btn sm soft" type="button" onClick="{{shut}}" style="position:absolute;right:28px;top:28px;width:120px"><span class="kc">B</span>Close</button>'
             '<div class="abs" style="left:44px;top:260px;width:640px;display:flex;flex-direction:column;gap:8px">'
             f'<span class="lab" style="display:flex;align-items:center;gap:12px"><span class="new"></span>{UPDATE["ver"]} &#160;&#183;&#160; New</span>'
             f'<span style="font-size:48px;line-height:1">{UPDATE["name"]}</span><span style="font-size:18px">{UPDATE["line"]}</span>'
             f'<div style="margin-top:24px">{rows}</div></div>'
             + fixes_toggle(44, 132) +
             '<div class="abs" style="left:44px;right:44px;bottom:40px;display:flex"><button class="btn softcta" type="button" onClick="{{sail}}" style="width:100%"><span class="kc">A</span>Set sail</button></div>')
    drawer = ('<sc-if value="{{open}}" hint-placeholder-val="{{ false }}"><div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,1,10,.45)"></div>'
              + frame(1920 - W - 32, 108, W, H, inner, 'in') + '</sc-if>')
    logic = '''
const CLOSED = 'What\\u2019s new stays in Settings, under Other.';
class Component extends DCLogic {
constructor(p) { super(p); this.state = { card: true, open: false, also: false, toast: '' }; }
renderVals() {
  const st = this.state;''' + ALSO_LOGIC + '''
  const toast = { show: !!st.toast, text: st.toast || '', again: st.toast === CLOSED, open: () => this.setState({ open: true, toast: '' }) };
  return { card: st.card, open: st.open, alsoOpen, alsoShut, alsoExp, toggleAlso, toast,
    expand: () => this.setState({ card: false, open: true }),
    later: () => this.setState({ card: false, toast: CLOSED }),
    shut: () => this.setState({ open: false, also: false, toast: CLOSED }),
    sail: () => this.setState({ open: false, also: false, toast: 'Opening Voyages on the Ark.' }) };
}
}
'''
    body = HOME + card + drawer + toast('{{toast.text}}')
    return page(f'Rev {REV} D - Card: a small card slides in over Home and opens into the window - interactive', body, logic)


# ---------- Settings: the way back in ----------
TABS = {
    'General': [['Colour vision', 'Off'], ['Subtitles', 'Off'], ['Subtitle size', 'Medium'], ['Analytics and crash reports', 'On']],
    'Display': [['Display mode', 'Fullscreen'], ['Resolution', 'Native'], ['Frame rate limit', '60'], ['V-Sync', 'On'], ['Field of view', '75']],
    'Performance': [['Quality preset', 'High'], ['Anti-aliasing', 'SMAA'], ['Texture quality', 'Full'], ['Adaptive performance', 'Balanced']],
    'Other': [['Invert Y', 'Off'], ['Music volume', '70'], ['SFX volume', '85'], ['Haptics', 'On']],
}


def board_settings():
    W, H = 1600, 860
    head = ('<div class="abs" style="left:56px;top:44px;display:flex;flex-direction:column;gap:6px"><span class="lab">Settings</span>'
            '<span style="font-size:36px">{{tabName}}</span></div>'
            '<nav aria-label="Settings tabs" class="abs" style="right:56px;top:60px;display:flex;gap:40px">'
            '<sc-for list="{{tabs}}" as="t" hint-placeholder-count="4"><button class="pick cap" type="button" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
            'style="font-size:15px;color:{{t.ink}};padding-bottom:8px;border-bottom:2px solid {{t.under}}">{{t.name}}</button></sc-for></nav>')
    rows = ('<div class="abs" style="left:56px;top:170px;width:1488px;display:flex;flex-direction:column">'
            '<sc-for list="{{rows}}" as="r" hint-placeholder-count="4"><div style="height:64px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid rgba(67,76,137,.6)">'
            '<span style="font-size:18px">{{r.label}}</span><span class="num" style="font-size:18px;color:#B8C0F0">{{r.value}}</span></div></sc-for>'
            '<sc-if value="{{isOther}}" hint-placeholder-val="{{ true }}"><span class="lab" style="margin-top:44px;margin-bottom:6px">About</span>'
            '<div style="position:relative;height:84px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid rgba(67,76,137,.6);background:linear-gradient(90deg,rgba(67,76,137,.22),rgba(67,76,137,0));box-shadow:inset 2px 0 0 #00D4FF;padding:0 0 0 20px">'
            '<span style="display:flex;flex-direction:column;gap:4px"><span style="display:flex;align-items:center;gap:12px;font-size:20px">What&#8217;s new'
            '<sc-if value="{{unseen}}" hint-placeholder-val="{{ true }}"><span class="new"></span></sc-if></span>'
            f'<span style="font-size:15px;color:#747BAD">{UPDATE["ver"]} &#160;&#183;&#160; {UPDATE["name"]}</span></span>'
            '<button class="btn sm soft" type="button" onClick="{{openNews}}" style="width:150px"><span class="kc">A</span>Open</button></div>'
            '<div style="height:64px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid rgba(67,76,137,.6)"><span style="font-size:18px">Credits</span>'
            '<span style="color:#747BAD">' + RIGHT + '</span></div>'
            '<div style="height:64px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid rgba(67,76,137,.6)"><span style="font-size:18px">Version</span>'
            '<span class="num" style="font-size:18px;color:#B8C0F0">0.3.0</span></div></sc-if></div>')
    back = ('<div class="abs" style="right:56px;bottom:40px"><button class="btn soft" type="button" style="width:200px"><span class="kc">B</span>Back</button></div>')
    settings = ('<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,1,10,.74)"></div>'
                + frame(160, 150, W, H, head + rows + back, ''))
    news = ('<sc-if value="{{news}}" hint-placeholder-val="{{ false }}"><div class="abs" aria-hidden="true" style="left:0;top:0;width:1920px;height:1080px;background:rgba(0,1,10,.6)"></div>'
            + window_a('Back') + '</sc-if>')
    logic = ('''
const TABS = %s;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { tab: 'Other', news: false, also: false, unseen: true, toast: '' }; }
renderVals() {
  const st = this.state;''' % json.dumps(TABS) + ALSO_LOGIC + '''
  const tabs = Object.keys(TABS).map((n) => ({ name: n, pressed: n === st.tab ? 'true' : 'false',
    ink: n === st.tab ? '#E6E9FF' : '#747BAD', under: n === st.tab ? '#00D4FF' : 'rgba(0,0,0,0)', pick: () => this.setState({ tab: n }) }));
  const rows = TABS[st.tab].map((r) => ({ label: r[0], value: r[1] }));
  const toast = { show: !!st.toast, text: st.toast || '', again: false, open: () => {} };
  return { tabs, rows, tabName: st.tab, isOther: st.tab === 'Other', unseen: st.unseen, news: st.news,
    alsoOpen, alsoShut, alsoExp, toggleAlso, toast,
    openNews: () => this.setState({ news: true, unseen: false }),
    close: () => this.setState({ news: false, also: false }),
    sail: () => this.setState({ news: false, also: false, toast: 'Opening Voyages on the Ark.' }) };
}
}
''')
    body = HOME + settings + news + toast('{{toast.text}}')
    return page(f"Rev {REV} Settings - Settings: the What's new row under Other opens the same window - interactive", body, logic)


# ---------- notes ----------
def notes():
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>' % head
                + ''.join('<span style="font-size:18px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>')
    cols = [
        block('Asked (Shombith, 2026-11-21)', [
            '&#8220;Create a new UI on main menu a pop up called what&#8217;s new it can be accessed from settings or after an update this shows up. Create 4 mock-ups screens for this.&#8221;',
            'Read as four ways to draw it, each one working, plus the Settings way in. This update is Voyages, so every option features it.']),
        block('The four', [
            '<b>A Poster (recommended).</b> One window over Home: the Ark on the HyperSea, three features under it, the fixes folded away, Set sail. One look and you know what is new.',
            '<b>B Pages.</b> One feature a page, LB / RB to page, Set sail on the last. Bigger art, more presses.',
            '<b>C Changelog.</b> Every update in a list, the picked one beside it. Also the page Settings opens; older entries are placeholders.',
            '<b>D Card.</b> A small card slides in at Home&#8217;s corner; it opens into a tall drawer on the right. The quietest after an update.']),
        block('When it opens', [
            'After an update: once, on the first visit to Home, only when the update has a What&#8217;s new entry. Never over a match, the start flow or a hand-off.',
            'From Settings: Other, About, What&#8217;s new. The green mark stays on the row until it has been opened once.',
            'Closing it says where it lives (&#8220;stays in Settings, under Other&#8221;). Set sail opens Voyages on the Ark.']),
        block('For the build', [
            'Every word is an authored field: one What&#8217;s new entry per update (version, name, line, features, fixes, art), so the next update is data, not code.',
            'The one motion is the window rising (A, B, C) or the card sliding in (D). Lime only on Set sail; the selection line only on the picked row.',
            'Nothing built yet; the build waits on a pick.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:36px">What&#8217;s new: after an update, and from Settings</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">' % REV
            + ''.join(cols) + '</div>')
    return page('Rev %s - notes: what each option does, where it opens from, what the build needs' % REV, body, NOLOGIC, 1920, 900)


for name, fn in [('A', board_a), ('B', board_b), ('C', board_c), ('D', board_d), ('Settings', board_settings), ('Notes', notes)]:
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, name)), 'w') as f:
        f.write(fn())
print('written', out)
