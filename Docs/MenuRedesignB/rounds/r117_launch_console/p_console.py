"""All six pages in ONE board (the canvas holds 512 files and had two free): the rail switches pages, the theme follows."""
import re
import common
from common import *
import p_mission, p_pipeline, p_builds, p_release, p_signals, p_theme

MODS = [("Mission", p_mission, "Where the launch stands, and the one thing to do next"),
        ("Pipeline", p_pipeline, "Bleeding Edge to Development to Master to Release, and what each branch feeds"),
        ("Builds", p_builds, "Every platform on every branch, and where each build goes to be tested"),
        ("Release", p_release, "Perform a release, any time: five steps from a branch to the people playing it"),
        ("Signals", p_signals, "Who is playing, what they say, and how close the next gate is"),
        ("Theme", p_theme, "How the console looks and moves, for you")]


def mains():
    out = []
    for key, m, _ in MODS:
        html = m.main() if key != "Pipeline" else (m.lanes() + m.schedule() + m.drawer())
        html = html.replace('<a class="btn cta glow" href="R117Release.dc.html">', '<button class="btn cta glow" type="button" onClick="{{nav.Release}}">')
        html = html.replace('Start a release</a>', 'Start a release</button>')
        html = html.replace('<a class="btn def" href="R117Pipeline.dc.html">Pipeline</a>', '<button class="btn def" type="button" onClick="{{nav.Pipeline}}">Pipeline</button>')
        html = html.replace('<a class="btn def s showat" href="R117Signals.dc.html" style="animation-delay:7s">Watch Signals</a>',
                            '<button class="btn def s showat" type="button" style="animation-delay:7s" onClick="{{nav.Signals}}">Watch Signals</button>')
        assert 'href="R117' not in html, key
        out.append('<sc-if value="{{pg.%s}}" hint-placeholder-val="{{ %s }}">%s</sc-if>' % (key, "true" if key == "Mission" else "false", html))
    return "".join(out)


def build():
    body = shell("Mission", "{{pg.title}}", "{{pg.sub}}", mains())
    # The rail: buttons that switch the page (one board), not links between boards.
    for key, _ in PAGES:
        body = re.sub(r'<a class="nav( on)?" href="R117%s\.dc\.html"( aria-current="page")?>(.*?)</a>' % key,
                      lambda mm, k=key: '<button class="nav {{navc.%s}}" type="button" style="width:100%%;border:0;background-color:transparent;cursor:pointer" aria-current="{{navp.%s}}" onClick="{{nav.%s}}">%s</button>' % (k, k, k, mm.group(3)),
                      body)
    assert 'href="R117' not in body
    logic = "class StubBase { constructor(p) { this.props = p || {}; this.state = {}; } setState() {} }\nconst PG = {};\n"
    for key, m, _ in MODS:
        logic += "PG.%s = (function (DCLogic) {\n%s\nreturn { init: new Component({}).state, render: Component.prototype.renderVals };\n})(StubBase);\n" % (key, m.LOGIC)
    subs = {k: s for k, _, s in MODS}
    logic += r"""
const PAGE_KEYS = """ + str([k for k, _, _ in MODS]).replace("'", '"') + r""";
const SUBS = """ + str(subs).replace("'", '"') + r""";
class Component extends DCLogic {
  constructor(p) { super(p); const s = Object.assign({}, UI0); PAGE_KEYS.forEach((k) => Object.assign(s, PG[k].init)); s.page = 'Mission'; this.state = s; }
  renderVals() {
    const st = this.state, v = {};
    PAGE_KEYS.forEach((k) => { if (k !== st.page) Object.assign(v, PG[k].render.call(this)); });
    Object.assign(v, PG[st.page].render.call(this));
    v.pg = { title: st.page, sub: SUBS[st.page] }; v.nav = {}; v.navc = {}; v.navp = {};
    PAGE_KEYS.forEach((k) => { v.pg[k] = st.page === k; v.nav[k] = () => this.setState({ page: k, askOpen: false });
      v.navc[k] = st.page === k ? 'on' : ''; v.navp[k] = st.page === k ? 'page' : 'false'; });
    return v;
  }
}
"""
    extra = "".join(getattr(m, "EXTRA", "") for _, m, _ in MODS) + ".nav{text-align:left}"
    return board("Rev 117 Console - the Launch Console, six pages in one board", body, logic, extra)
