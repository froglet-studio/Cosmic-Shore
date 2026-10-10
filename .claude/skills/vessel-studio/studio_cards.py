"""studio_cards.py - the Vessel Studio card model: what a studio card shows and does, read from the web hub and from
studios.json (/vessel-studio D34). The hub (Docs/Studios/VesselStudio/index.html) is the look; studios.json carries the
same card fields for the native homes (Amoebius's VESSEL STUDIO page, Unity's studio home), which draw only what it says.
parity_gate.py fails when the two disagree; bake_previews.cjs writes the preview thumbnails the native homes show.

    python3 .claude/skills/vessel-studio/studio_cards.py            # print the hub's cards and the catalog's, and any difference
    python3 .claude/skills/vessel-studio/studio_cards.py --hash     # the preview code's hash (previews/previews.json)
"""
import hashlib, html, json, os, re, sys
from html.parser import HTMLParser

DIR = 'Docs/Studios/VesselStudio'
ACTION_IDS = ['open', 'engine', 'tune', 'live']   # the card's buttons, in this order on every surface that has them
HOSTS = {'web', 'amoebius', 'unity'}


class _Hub(HTMLParser):
    """The hub's bays (<a class="bay">), its fleet chips, its lede: the text exactly as a reader sees it."""
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.cards, self.fleet, self.lede, self.fleet_note = [], [], None, None
        self._card, self._cls, self._grab, self._buf, self._depth = None, [], None, '', 0
        self._in_fleet = False
        self._spec_key = None

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        cls = (a.get('class') or '').split()
        if tag == 'a' and 'bay' in cls:
            self._card = {'file': a.get('href'), 'accent': _accent_var(a.get('style') or ''), 'spec': []}
            self._depth = 0
        if self._card is not None:
            self._depth += 1
            if tag == 'canvas': self._card['canvas'] = a.get('id')
            elif tag == 'b' and self._grab is None and 'name' not in self._card: self._start('name')
            elif tag == 'span' and 'chip' in cls: self._start('chip')
            elif tag == 'p': self._start('summary')
            elif tag == 'span' and 'open' in cls: self._start('open')
            elif tag == 'span' and self._in_spec(): self._start('spec_k')
            elif tag == 'b' and self._in_spec(): self._start('spec_v')
            if tag == 'div' and 'spec' in cls: self._card['_spec'] = True
        if tag == 'div' and 'fleet' in cls: self._in_fleet = True
        elif self._in_fleet and tag == 'span': self._start('fleet')
        if tag == 'p' and 'lede' in cls and self.lede is None and self._card is None and 'hint' not in cls: self._start('lede')
        if tag == 'p' and 'lede' in cls and 'hint' in cls and self.fleet_note is None and self._card is None: self._start('fleetNote')

    def _in_spec(self): return self._card is not None and self._card.get('_spec') and self._card.get('_specopen', True)

    def _start(self, what): self._grab, self._buf = what, ''

    def handle_data(self, data):
        if self._grab: self._buf += data

    def handle_endtag(self, tag):
        g = self._grab
        if g and ((g in ('name', 'spec_v') and tag == 'b') or (g in ('chip', 'open', 'spec_k', 'fleet') and tag == 'span') or (g in ('summary', 'lede', 'fleetNote') and tag == 'p')):
            text = ' '.join(self._buf.split())
            if g == 'fleet': self.fleet.append(text)
            elif g == 'lede': self.lede = text
            elif g == 'fleetNote': self.fleet_note = text
            elif g == 'spec_k': self._spec_key = text
            elif g == 'spec_v': self._card['spec'].append({'k': self._spec_key, 'v': text})
            else: self._card[g] = text
            self._grab = None
        if self._card is not None:
            if tag == 'div' and self._card.get('_spec') and self._card.get('_specopen', True) and self._grab is None and self._spec_key is not None and tag == 'div':
                self._card['_specopen'] = False
            self._depth -= 1
            if tag == 'a':
                c = {k: v for k, v in self._card.items() if not k.startswith('_')}
                self.cards.append(c); self._card = None; self._spec_key = None
        if tag == 'div' and self._in_fleet and self._grab is None: self._in_fleet = False


def _accent_var(style):
    m = re.search(r'--accent:\s*var\(--([\w-]+)\)', style)
    return m.group(1) if m else (re.search(r'--accent:\s*(#[0-9a-fA-F]{3,8})', style) or [None, None])[1]


def hub(html_text):
    """The hub's cards (file, name, chip, summary, spec, open, accent var, canvas id), fleet and lede."""
    p = _Hub(); p.feed(html_text)
    roots = dict(re.findall(r'--([\w-]+):\s*(#[0-9a-fA-F]{6})', html_text.split(':root', 1)[1].split('}', 1)[0])) if ':root' in html_text else {}
    return {'cards': p.cards, 'fleet': p.fleet, 'fleetNote': p.fleet_note, 'lede': p.lede, 'vars': roots}


def preview_source(html_text):
    """The hub's preview drawing code (the bay canvases), whose hash says whether the baked thumbnails are current."""
    m = re.search(r'// ---- bay previews.*?(?=\n  // ---- )', html_text, re.S)
    return m.group(0) if m else ''


def preview_hash(root):
    t = open(os.path.join(root, DIR, 'index.html'), encoding='utf-8').read()
    doms = os.path.join(root, DIR, 'studio-domains.js')
    d = open(doms, encoding='utf-8').read() if os.path.exists(doms) else ''
    return hashlib.sha256((preview_source(t) + '\n' + d).encode('utf-8')).hexdigest()


def differences(root):
    """Every way the catalog's cards differ from the hub's, in words. Empty = the native homes show the hub's cards."""
    errs = []
    cat = json.load(open(os.path.join(root, DIR, 'studios.json'), encoding='utf-8'))
    h = hub(open(os.path.join(root, DIR, 'index.html'), encoding='utf-8').read())
    studios = cat.get('studios', [])
    hub_files = [c.get('file') for c in h['cards']]
    cat_files = [s.get('file') for s in studios]
    if hub_files != cat_files:
        errs.append(f'the hub lists {hub_files}, studios.json {cat_files}: same studios, same order')
    acts = cat.get('cardActions') or []
    ids = [a.get('id') for a in acts]
    if ids != ACTION_IDS:
        errs.append(f'studios.json cardActions are {ids}: they must be {ACTION_IDS}, in that order')
    for a in acts:
        on = set((a.get('on') or '').split(','))
        if not on or not on <= HOSTS: errs.append(f'cardActions {a.get("id")}: "on" must name hosts from {sorted(HOSTS)}')
        if not a.get('label'): errs.append(f'cardActions {a.get("id")}: no label')
    open_label = next((a.get('label') for a in acts if a.get('id') == 'open'), None)
    for c, s in zip(h['cards'], studios):
        n = s.get('id')
        for hk, ck in (('name', 'name'), ('chip', 'chip'), ('summary', 'summary')):
            if (c.get(hk) or '') != (s.get(ck) or ''):
                errs.append(f'{n}: the hub card\'s {hk} is "{(c.get(hk) or "")[:60]}...", studios.json "{(s.get(ck) or "")[:60]}...": copy the hub\'s')
        if c.get('spec') != s.get('spec'):
            errs.append(f'{n}: the hub card\'s spec rows {c.get("spec")} differ from studios.json {s.get("spec")}')
        if c.get('open') != open_label:
            errs.append(f'{n}: the hub card says "{c.get("open")}", studios.json\'s open action "{open_label}"')
        acc, var = s.get('accent'), c.get('accent')
        if acc and var and not (acc == var or acc.lower() == (h['vars'].get(var) or '').lower()):
            errs.append(f'{n}: the hub card\'s accent is var(--{var}), studios.json "{acc}"')
        pv = s.get('preview')
        if not pv: errs.append(f'{n}: no "preview" (the baked thumbnail the native homes show)')
        elif not os.path.exists(os.path.join(root, DIR, pv)): errs.append(f'{n}: preview {pv} is missing: node .claude/skills/vessel-studio/bake_previews.cjs')
    if h['fleet'] != (cat.get('fleet') or []):
        errs.append(f'the hub\'s fleet {h["fleet"]} differs from studios.json {cat.get("fleet")}')
    if h['lede'] and h['lede'] != cat.get('lede'):
        errs.append('studios.json "lede" is not the hub\'s lede')
    if h['fleetNote'] and h['fleetNote'] != cat.get('fleetNote'):
        errs.append('studios.json "fleetNote" is not the hub\'s line under the fleet')
    stamp = os.path.join(root, DIR, 'previews', 'previews.json')
    if os.path.exists(stamp):
        if json.load(open(stamp)).get('source') != preview_hash(root):
            errs.append('the hub\'s preview code changed since the thumbnails were baked: node .claude/skills/vessel-studio/bake_previews.cjs')
    elif any(s.get('preview') for s in studios):
        errs.append('previews/previews.json is missing: node .claude/skills/vessel-studio/bake_previews.cjs')
    return errs


if __name__ == '__main__':
    if sys.argv[1:2] == ['--hash']:   # for bake_previews.cjs: the stamp it writes into previews/previews.json
        print(preview_hash(sys.argv[2] if len(sys.argv) > 2 else '.')); sys.exit(0)
    root = sys.argv[1] if len(sys.argv) > 1 else '.'
    h = hub(open(os.path.join(root, DIR, 'index.html'), encoding='utf-8').read())
    print(json.dumps(h, indent=1, ensure_ascii=False))
    d = differences(root)
    for e in d: print('DIFF', e)
    sys.exit(1 if d else 0)
