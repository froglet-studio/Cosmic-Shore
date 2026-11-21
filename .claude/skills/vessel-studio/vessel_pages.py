#!/usr/bin/env python3
"""vessel_pages.py - the GENERATOR of the Vessel Studio's recipe-driven studio pages (/vessel-studio D40; the vision's B3).

A vessel is DATA: Docs/Studios/VesselStudio/vessels/<id>.json names its movement kind (studio-kinds.js), its numbers,
its four abilities over the four elements, its play styles, its words. This script turns every recipe into:

  Docs/Studios/VesselStudio/<id>.html          the page: vessel_page_template.html with the recipe inlined (file:// safe)
  Docs/Studios/VesselStudio/index.html         the hub's bay per vessel and its preview call, between markers
  Docs/Studios/VesselStudio/studios.json       one catalog entry per vessel (group "concept"), the hub card's text (D35)

    python3 .claude/skills/vessel-studio/vessel_pages.py            # write everything
    python3 .claude/skills/vessel-studio/vessel_pages.py --check    # exit 1 when any output is stale (the gate)
    python3 .claude/skills/vessel-studio/vessel_pages.py --self-test

Markers in index.html, each ALONE on its line (a marker named in prose is left alone, L-GEN-4):
  <!-- concept-bays:start --> ... <!-- concept-bays:end -->           the bays (before the + New studio card, D36)
  // concept-previews:start ... // concept-previews:end               the preview() calls (inside the hub's script)
  // concept-spec:start ... // concept-spec:end                       the studio agent's SPEC entries
  <!-- concept-options:start --> ... <!-- concept-options:end -->     the <option>s of the Ask and Request vessel pickers
After a run: re-bake the previews (bake_previews.cjs) and run parity_gate.py; both are part of /vessel-studio's gate.
"""
import argparse, glob, html, json, os, re, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
DIR = os.path.join(ROOT, 'Docs/Studios/VesselStudio')
TEMPLATE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'vessel_page_template.html')
GROUP = 'concept'
MARKS = {
    'bays': ('<!-- concept-bays:start -->', '<!-- concept-bays:end -->'),
    'previews': ('// concept-previews:start', '// concept-previews:end'),
    'spec': ('// concept-spec:start', '// concept-spec:end'),
    'options': ('<!-- concept-options:start -->', '<!-- concept-options:end -->'),
}


def read(p):
    with open(p, encoding='utf-8') as fh:
        return fh.read()


def recipes(d=None):
    out = []
    for p in sorted(glob.glob(os.path.join(d or os.path.join(DIR, 'vessels'), '*.json'))):
        with open(p, encoding='utf-8') as fh:
            r = json.load(fh)
        rid = os.path.splitext(os.path.basename(p))[0]
        if r.get('id') != rid:
            raise SystemExit(f'{p}: "id" is {r.get("id")!r}, the file says {rid!r}')
        for k in ('name', 'kind', 'summary', 'spec', 'elements'):
            if k not in r:
                raise SystemExit(f'{p}: missing "{k}"')
        out.append(r)
    out.sort(key=lambda r: (r.get('order', 999), r['id']))   # the design's order (recipe "order"), then by id
    return out


def page(recipe, template):
    """The page: the template with the recipe inlined. `</` is escaped so no recipe string can close the script."""
    js = json.dumps(recipe, ensure_ascii=True, indent=2).replace('</', '<\\/')
    rep = {'{{TITLE}}': html.escape(recipe['name'] + ' Studio'), '{{NAME}}': html.escape(recipe['name']), '{{ID}}': recipe['id'],
           '{{ROUND}}': html.escape(recipe.get('round', 'concept · round 1')), '{{SUMMARY}}': html.escape(recipe['summary']),
           '{{ACCENT}}': recipe.get('accent', '#ffb347'), '{{RECIPE_JSON}}': js}
    out = template
    for k, v in rep.items():
        out = out.replace(k, v)
    left = re.findall(r'\{\{[A-Z_]+\}\}', out)
    if left:
        raise SystemExit(f'{recipe["id"]}: template placeholders left unfilled: {sorted(set(left))}')
    return out


def bay(r):
    """The hub card (D35): preview canvas, name, chip, summary, spec rows, Open studio. Exactly the shape the catalog repeats."""
    cid = 'pv' + r['id'][0].upper() + r['id'][1:]
    spec = ''.join(f"<span>{html.escape(s['k'])}</span><b>{html.escape(s['v'])}</b>" for s in r['spec'])
    return (f'      <a class="bay" href="{r["id"]}.html" style="--accent: {r.get("accent", "#ffb347")}">\n'
            f'        <canvas id="{cid}" width="640" height="300" aria-hidden="true"></canvas>\n'
            f'        <div style="display:grid;gap:8px">\n'
            f'          <div class="name"><b>{html.escape(r["name"])}</b><span class="chip" style="color:{r.get("accent", "#ffb347")}">{html.escape(r.get("chip", "concept"))}</span></div>\n'
            f'          <p>{html.escape(r["summary"])}</p>\n'
            f'          <div class="spec">{spec}</div>\n'
            f'        </div>\n'
            f'        <span class="open">Open studio &rarr;</span>\n'
            f'      </a>\n')


def preview_call(r):
    cid = 'pv' + r['id'][0].upper() + r['id'][1:]
    return f"  preview('{cid}', (g, w, h, t) => VesselStudioKinds.{r['kind']}.preview(g, w, h, t, '{r.get('accent', '#ffb347')}', DOM));\n"


def spec_entry(r):
    els = r['elements']
    ab = '; '.join(f"{e.capitalize()} = {els[e]['name']} (scales {els[e].get('param', '?')}; L5 {els[e].get('l5', 'open')})" for e in ('charge', 'mass', 'space', 'time') if e in els)
    txt = (f"{r['name']} (concept, round 1; movement kind '{r['kind']}', a design: nothing ships). {r['summary']} Elements: {ab}. "
           f"Styles: {', '.join(s['name'] for s in r.get('styles', []))}. Every mechanic number is lab-only (Vessel Config > Mechanic shows them).")
    return "    '" + r['name'].replace("'", "\\'") + "': '" + txt.replace('\\', '\\\\').replace("'", "\\'") + "',\n"


def option(r):
    return f'<option>{html.escape(r["name"])}</option>'


def splice(text, key, body, multi=False):
    """Replace what stands between a marker pair, matching each marker ALONE on its line (L-GEN-4). One pair, or with
    `multi` every pair in order (the two vessel pickers each take the same options)."""
    a, b = MARKS[key]
    ra = re.compile(r'^[ \t]*' + re.escape(a) + r'[ \t]*$', re.M)
    rb = re.compile(r'^[ \t]*' + re.escape(b) + r'[ \t]*$', re.M)
    ma, mb = list(ra.finditer(text)), list(rb.finditer(text))
    if len(ma) != len(mb) or not ma or (not multi and len(ma) != 1):
        raise SystemExit(f'index.html: expected {"matched pairs" if multi else "exactly one line"} of {a!r} and {b!r}, found {len(ma)} and {len(mb)}')
    out, pos = [], 0
    for x, y in zip(ma, mb):
        if y.start() < x.end():
            raise SystemExit(f'index.html: {b!r} comes before {a!r}')
        out.append(text[pos:x.end()] + '\n' + body); pos = y.start()
    out.append(text[pos:])
    return ''.join(out)


def hub(index_html, rs):
    out = splice(index_html, 'bays', ''.join(bay(r) for r in rs))
    out = splice(out, 'previews', ''.join(preview_call(r) for r in rs))
    out = splice(out, 'spec', ''.join(spec_entry(r) for r in rs))
    out = splice(out, 'options', '          ' + ''.join(option(r) for r in rs) + '\n', multi=True)
    return out


def catalog(cat_text, rs):
    cat = json.loads(cat_text)
    kept = [s for s in cat['studios'] if s.get('group') != GROUP]
    for r in rs:
        kept.append({
            'id': r['id'], 'name': r['name'], 'file': r['id'] + '.html', 'group': GROUP,
            'kind': 'concept studio - round 1 (recipe-driven, movement kind ' + r['kind'] + ')',
            'chip': r.get('chip', 'concept'), 'summary': r['summary'], 'spec': [{'k': s['k'], 'v': s['v']} for s in r['spec']],
            'preview': 'previews/' + r['id'] + '.png', 'accent': r.get('accent', '#ffb347'), 'docs': r.get('docs', 'Docs/Studios/CONCEPT_FLEET.md'),
        })
    cat['studios'] = kept
    for f in ('studio-kinds.js', 'studio-vessel.js'):
        if f not in cat.get('shared', []):
            cat.setdefault('shared', []).append(f)
    return json.dumps(cat, ensure_ascii=False, indent=2) + '\n'


def outputs(d, template, rs):
    """Everything the generator would write: {path: text}."""
    out = {os.path.join(d, r['id'] + '.html'): page(r, template) for r in rs}
    out[os.path.join(d, 'index.html')] = hub(read(os.path.join(d, 'index.html')), rs)
    out[os.path.join(d, 'studios.json')] = catalog(read(os.path.join(d, 'studios.json')), rs)
    return out


def run(d, template_path, check):
    rs = recipes(os.path.join(d, 'vessels'))
    if not rs:
        raise SystemExit('no recipes under ' + os.path.join(d, 'vessels'))
    want = outputs(d, read(template_path), rs)
    stale = [p for p, t in want.items() if not os.path.exists(p) or read(p) != t]
    if check:
        for p in stale:
            print('STALE ' + os.path.relpath(p, ROOT))
        print(f'{len(rs)} recipes; ' + ('all outputs current' if not stale else f'{len(stale)} stale'))
        return 1 if stale else 0
    for p, t in want.items():
        with open(p, 'w', encoding='utf-8') as fh:
            fh.write(t)
    print(f'{len(rs)} recipes -> {len(want)} files written ({len(stale)} changed)')
    return 0


def self_test():
    bad = 0

    def expect(name, ok):
        nonlocal bad
        print(('ok   ' if ok else 'FAIL ') + name)
        if not ok:
            bad += 1
    with tempfile.TemporaryDirectory() as tmp:
        os.makedirs(os.path.join(tmp, 'vessels'))
        rec = {'id': 'testfish', 'name': 'Testfish', 'kind': 'swing', 'summary': 'A fish </script> that tests.', 'accent': '#123456',
               'spec': [{'k': 'A', 'v': 'b'}], 'elements': {'charge': {'name': 'X', 'param': 'kick', 'l5': 'Y'}}, 'styles': [{'name': 'Balanced'}]}
        with open(os.path.join(tmp, 'vessels', 'testfish.json'), 'w') as fh:
            json.dump(rec, fh)
        idx = ('<div class="bays">\n<!-- a prose mention of <!-- concept-bays:start --> is left alone -->\n'
               '      <!-- concept-bays:start -->\n      <a class="bay">old</a>\n      <!-- concept-bays:end -->\n</div>\n'
               '<select>\n          <!-- concept-options:start -->\n          <!-- concept-options:end -->\n</select>\n'
               '<select>\n          <!-- concept-options:start -->\n          <!-- concept-options:end -->\n</select>\n'
               '<script>\n  const SPEC = {\n    // concept-spec:start\n    // concept-spec:end\n  };\n  // concept-previews:start\n  // concept-previews:end\n</script>\n')
        with open(os.path.join(tmp, 'index.html'), 'w') as fh:
            fh.write(idx)
        with open(os.path.join(tmp, 'studios.json'), 'w') as fh:
            json.dump({'studios': [{'id': 'squirrel', 'file': 'squirrel.html'}, {'id': 'gone', 'group': GROUP}], 'shared': ['studio-look.js']}, fh)
        rc = run(tmp, TEMPLATE, check=False)
        expect('writes without error', rc == 0)
        pg = read(os.path.join(tmp, 'testfish.html'))
        expect('placeholders filled', '{{' not in pg and '<title>Testfish Studio</title>' in pg)
        expect('a </script> inside the recipe cannot close the page script', '<\\/script>' in pg and pg.count('</script>') == pg.count('<script'))
        hub_out = read(os.path.join(tmp, 'index.html'))
        expect('the old bay is replaced and the prose marker left alone', 'old</a>' not in hub_out and 'a prose mention of <!-- concept-bays:start -->' in hub_out and 'href="testfish.html"' in hub_out)
        expect('the preview call and SPEC entry land inside the script', "VesselStudioKinds.swing.preview" in hub_out and "'Testfish': '" in hub_out)
        expect('both vessel pickers get the option', hub_out.count('<option>Testfish</option>') == 2)
        cat = json.load(open(os.path.join(tmp, 'studios.json')))
        ids = [s['id'] for s in cat['studios']]
        expect('the catalog keeps other studios, drops the retired concept and adds the new one once', ids == ['squirrel', 'testfish'] and 'studio-vessel.js' in cat['shared'])
        expect('a second run is idempotent (--check passes)', run(tmp, TEMPLATE, check=True) == 0)
        with open(os.path.join(tmp, 'testfish.html'), 'a') as fh:
            fh.write('<!-- drift -->')
        expect('--check names a drifted page', run(tmp, TEMPLATE, check=True) == 1)
        # a marker that appears twice is refused
        with open(os.path.join(tmp, 'index.html'), 'a') as fh:
            fh.write('\n<!-- concept-bays:end -->\n')
        try:
            run(tmp, TEMPLATE, check=True); dup = False
        except SystemExit:
            dup = True
        expect('a duplicated marker line is refused', dup)
    print('self-test: ' + ('ok' if not bad else f'{bad} failure(s)'))
    return 1 if bad else 0


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    sys.exit(run(DIR, TEMPLATE, a.check))
