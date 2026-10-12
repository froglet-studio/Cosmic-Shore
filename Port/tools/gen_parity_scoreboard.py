#!/usr/bin/env python3
"""Generate the parity scoreboard (ROADMAP C1, review H18): Port/docs/PARITY.md + Port/docs/parity.json.

Inputs (all read-only):
  Port/parity/subsystems.json      the catalogue: what each subsystem is, how far it is implemented,
                                   the engine tests and the parity channels that cover it
  Port/parity/results/latest.json  the last `engine_parity` run against the Unity goldens (optional)
  Assets/**/*.shader|.shadergraph  every first-party shader; Port/src is scanned for its name

Status is derived, never declared (C9: only a channel within tolerance of Unity counts):
  Faithful     implemented in full AND every listed parity channel PASSES against Unity goldens
  Approximate  implemented (or a dedicated shader translation) but not yet proven within tolerance
  Missing      not implemented (a shader with no dedicated translation draws through a fallback)

  python3 Port/tools/gen_parity_scoreboard.py           write both files
  python3 Port/tools/gen_parity_scoreboard.py --check   exit 1 if either file is stale (CI)
"""
import json
import os
import re
import sys

PORT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(PORT)
MD = os.path.join(PORT, 'docs', 'PARITY.md')
JS = os.path.join(PORT, 'docs', 'parity.json')
SKIP_SHADER_DIRS = ('/Plugins/', '/TextMesh Pro/', '/Samples/')


def load_json(path, default=None):
    if not os.path.exists(path):
        return default
    with open(path, encoding='utf-8') as f:
        return json.load(f)


def test_classes():
    names = set()
    for root in (os.path.join(PORT, 'tests'),):
        for d, _, files in os.walk(root):
            if os.sep + 'obj' in d or os.sep + 'bin' in d:
                continue
            names.update(f[:-3] for f in files if f.endswith('.cs'))
    return names


def port_source_text():
    chunks = []
    for d, _, files in os.walk(os.path.join(PORT, 'src')):
        if os.sep + 'obj' in d or os.sep + 'bin' in d:
            continue
        for f in files:
            if f.endswith(('.cs', '.glsl', '.vert', '.frag')):
                with open(os.path.join(d, f), encoding='utf-8', errors='replace') as h:
                    chunks.append(h.read())
    return '\n'.join(chunks)


def shader_name(path):
    """The name a material sees: a .shader's `Shader "X/Y"`, a graph's "Shader Graphs/<file>"."""
    if path.endswith('.shader'):
        with open(path, encoding='utf-8', errors='replace') as f:
            m = re.search(r'^\s*Shader\s+"([^"]+)"', f.read(), re.M)
        if m:
            return m.group(1)
    return 'Shader Graphs/' + os.path.splitext(os.path.basename(path))[0]


def shaders():
    found = []
    for d, _, files in os.walk(os.path.join(REPO, 'Assets')):
        rel_d = '/' + os.path.relpath(d, REPO).replace(os.sep, '/') + '/'
        if any(s in rel_d for s in SKIP_SHADER_DIRS):
            continue
        for f in files:
            if f.endswith(('.shader', '.shadergraph')):
                p = os.path.join(d, f)
                found.append((os.path.relpath(p, REPO).replace(os.sep, '/'), shader_name(p)))
    return sorted(found)


def build():
    catalogue = load_json(os.path.join(PORT, 'parity', 'subsystems.json'))
    results = load_json(os.path.join(PORT, 'parity', 'results', 'latest.json'), {}) or {}
    cases = results.get('cases', {})
    have_tests = test_classes()
    errors = []

    subsystems = []
    for s in catalogue['subsystems']:
        missing_tests = [t for t in s['tests'] if t not in have_tests]
        if missing_tests:
            errors.append(f"{s['id']}: covering test(s) not found under Port/tests: {', '.join(missing_tests)}")
        channels = []
        for ref in s['parity']:
            case, _, channel = ref.partition('/')
            r = cases.get(case, {}).get(channel)
            channels.append({'channel': ref, 'result': r['status'] if r else 'no result'})
        proven = s['parity'] and all(c['result'] == 'pass' for c in channels)
        if s['implemented'] == 'none':
            status, why = 'Missing', 'not implemented'
        elif s['implemented'] == 'full' and proven:
            status, why = 'Faithful', 'every parity channel within C9 tolerance of Unity'
        else:
            failing = [c['channel'] for c in channels if c['result'] == 'fail']
            if failing:
                why = 'parity FAIL: ' + ', '.join(failing)
            elif not s['parity']:
                why = 'no parity channel covers it yet'
            elif s['implemented'] != 'full':
                why = 'partly implemented'
            else:
                why = 'no Unity golden yet'
            status = 'Approximate'
        subsystems.append({
            'id': s['id'], 'area': s['area'], 'name': s['name'], 'status': status, 'why': why,
            'tests': s['tests'], 'parity': channels,
        })

    # The shader routes come from `cs-asset shadergraph-census` (Port/parity/shaders.json): a
    # hand-tuned family (keyed by guid), a graph the Shader Graph compiler translates, a hand
    # translation of a .shader, a dedicated renderer (TextMesh Pro, the skybox), or missing.
    # Without that file, fall back to the old name scan of Port/src.
    census = {r['path']: r for r in (load_json(os.path.join(PORT, 'parity', 'shaders.json'), {}) or {}).get('shaders', [])}
    src = None if census else port_source_text()
    shader_rows = []
    for path, name in shaders():
        c = census.get(path)
        if c is not None:
            translated = c['route'] in ('family', 'compiled', 'hand', 'own')
            why = {'family': 'hand-tuned family; frames not yet compared per shader (C2)',
                   'compiled': 'Shader Graph compiler; frames not yet compared per shader (C2)',
                   'hand': 'hand translation (Content/Shaders/Hand); frames not yet compared per shader (C2)',
                   'own': (c.get('why') or 'its own renderer') + '; frames not yet compared per shader (C2)'}.get(c['route'], c.get('why', 'no translation'))
            if c.get('approximations'):
                why += '; approximate nodes: ' + ', '.join(a.split(':')[0] for a in c['approximations'])
        else:
            translated = src is not None and ('"' + name + '"') in src
            why = 'dedicated translation; frames not yet compared per shader (C2)' if translated else 'generic material-family fallback'
        shader_rows.append({
            'path': path, 'name': name,
            'status': 'Approximate' if translated else 'Missing',
            'why': why,
            'test': 'ShaderGraphCompilerTests, --check-shaders' if c is not None and c['route'] == 'compiled'
                    else 'HandShaderTests, --check-shaders' if c is not None and c['route'] == 'hand'
                    else ('engine_parity frames' if translated else ''),
        })

    def count(rows):
        return {k: sum(1 for r in rows if r['status'] == k) for k in ('Faithful', 'Approximate', 'Missing')}

    doc = {
        'source': 'Port/tools/gen_parity_scoreboard.py',
        'results': {'date': results.get('date'), 'cases': sorted(cases)} if results else None,
        'summary': {'subsystems': count(subsystems), 'shaders': count(shader_rows)},
        'subsystems': subsystems,
        'shaders': shader_rows,
    }
    return doc, errors


def markdown(doc):
    out = ['# Parity scoreboard', '',
           '_Generated by `Port/tools/gen_parity_scoreboard.py` from `Port/parity/subsystems.json`, the last '
           '`engine_parity` run (`Port/parity/results/latest.json`) and a scan of the shaders. Do not edit by hand._', '',
           '**Faithful**: implemented in full and every listed parity channel is within the C9 tolerance of the Unity goldens. '
           '**Approximate**: implemented, not yet proven against Unity. **Missing**: not implemented.', '']
    r = doc['results']
    out.append(f"Last parity run against Unity goldens: {r['date']} ({', '.join(r['cases'])})." if r else
               'Last parity run against Unity goldens: none yet (the Unity capture lands with the replay PR).')
    out.append('')
    s, sh = doc['summary']['subsystems'], doc['summary']['shaders']
    out += ['| | Faithful | Approximate | Missing |', '|---|---|---|---|',
            f"| Subsystems | {s['Faithful']} | {s['Approximate']} | {s['Missing']} |",
            f"| Shaders | {sh['Faithful']} | {sh['Approximate']} | {sh['Missing']} |", '']
    out += ['## Subsystems', '', '| Area | Subsystem | Status | Why | Covering tests | Parity channels |', '|---|---|---|---|---|---|']
    for x in doc['subsystems']:
        par = ', '.join(f"{c['channel']} ({c['result']})" for c in x['parity']) or '-'
        out.append(f"| {x['area']} | {x['name']} | {x['status']} | {x['why']} | {', '.join(x['tests']) or '-'} | {par} |")
    out += ['', '## Shaders', '', '| Shader | Asset | Status | Why |', '|---|---|---|---|']
    for x in doc['shaders']:
        out.append(f"| {x['name']} | `{x['path']}` | {x['status']} | {x['why']} |")
    return '\n'.join(out) + '\n'


def main():
    doc, errors = build()
    if errors:
        for e in errors:
            print('ERROR ' + e)
        return 1
    md = markdown(doc)
    js = json.dumps(doc, indent=2) + '\n'
    if '--check' in sys.argv:
        stale = [p for p, want in ((MD, md), (JS, js)) if not os.path.exists(p) or open(p, encoding='utf-8').read() != want]
        if stale:
            print('stale: ' + ', '.join(os.path.relpath(p, REPO) for p in stale) + ' - run python3 Port/tools/gen_parity_scoreboard.py')
            return 1
        print('parity scoreboard is current')
        return 0
    with open(MD, 'w', encoding='utf-8', newline='\n') as f:
        f.write(md)
    with open(JS, 'w', encoding='utf-8', newline='\n') as f:
        f.write(js)
    s = doc['summary']
    print(f"wrote {os.path.relpath(MD, REPO)} and {os.path.relpath(JS, REPO)}: subsystems {s['subsystems']}, shaders {s['shaders']}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
