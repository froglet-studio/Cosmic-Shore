#!/usr/bin/env python3
"""studio_to_unity.py - the artifact's numbers become the game's numbers (/artifact-to-unity).

The Vessel Studio artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) is the ONE source of truth for a
studio's parameters (the user, 2026-10-10). This writes them into the vessel's Unity config assets, through a map per
vessel (Tools/Build/studio_to_unity/<vessel>.json: studio key -> asset, field, scale). Unity never carries a second
tuning UI of its own.

Where a value comes from, in order:
  1. the page's SHIPPED block (the studio's decided numbers; the first time a key appears in it), then
  2. --settings FILE: a decision from the artifact's shared log (its "settings.changed" holds every slider the team
     moved), a {"changed": {...}} object, or a flat {key: value} object. Later files win.

    python3 Tools/Build/studio_to_unity.py                    # write every mapped vessel's assets
    python3 Tools/Build/studio_to_unity.py --vessel stoat --settings decision.json
    python3 Tools/Build/studio_to_unity.py --check            # drift report: exit 1 if an asset differs from the artifact
    python3 Tools/Build/studio_to_unity.py --unmapped         # studio keys with no home in the game yet
    python3 Tools/Build/studio_to_unity.py --self-test

Only plain top-level scalar fields are written (a "  field: number" line in the asset's YAML), in place, so the asset's
guid, fileIDs and every other line stay byte-identical. A field that is not such a line is an error, never a guess.
"""
import argparse
import json
import os
import re
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MAPS = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'studio_to_unity')
NUMBER = r'-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?'


def parse_shipped(html):
    """{key: value} of the page's `const SHIPPED = { ... };` literal; a key keeps its FIRST value."""
    start = html.find('const SHIPPED = {')
    if start < 0:
        return {}
    end = html.find('\n  };', start)
    block = html[start:end if end >= 0 else len(html)]
    block = re.sub(r'//[^\n]*', '', block)
    out = {}
    for m in re.finditer(r'\b([A-Za-z_]\w*)\s*:\s*(' + NUMBER + r')\b', block):
        out.setdefault(m.group(1), float(m.group(2)))
    return out


def parse_spec_labels(html):
    """{key: label} from the page's SPEC rows (['group', 'key', 'label', min, max, step]): the artifact's own names."""
    q = r"'((?:[^'\\]|\\.)*)'"   # a JS single-quoted string, escapes included
    rows = re.finditer(r"\[\s*" + q + r"\s*,\s*'(\w+)'\s*,\s*" + q + r"\s*,\s*-?[\d.]", html)   # a row, not a key list
    return {m.group(2): m.group(3).replace("\\'", "'") for m in rows}


def settings_from(obj):
    """A decision doc, {"settings": {"changed": {}}}, {"changed": {}} or a flat {key: number}."""
    if isinstance(obj, dict) and isinstance(obj.get('settings'), dict):
        obj = obj['settings']
    if isinstance(obj, dict) and isinstance(obj.get('changed'), dict):
        obj = obj['changed']
    return {k: float(v) for k, v in (obj or {}).items() if isinstance(v, (int, float)) and not isinstance(v, bool)}


def fmt(v):
    if abs(v - round(v)) < 1e-9 and abs(v) < 1e15:
        return str(int(round(v)))
    return repr(float('%.7g' % v))


def field_re(field):
    return re.compile(r'^(  ' + re.escape(field) + r': )(' + NUMBER + r')[ \t]*$', re.MULTILINE)


def plan(vmap, values, root=ROOT):
    """[(row, asset path, current value or None, wanted value or None, problem or None)] for every row."""
    out, texts = [], {}
    for row in vmap['rows']:
        path = os.path.join(root, row['asset'])
        if path not in texts:
            texts[path] = open(path, encoding='utf-8').read() if os.path.exists(path) else None
        text = texts[path]
        key = row['key']
        want = values[key] * row.get('scale', 1) if key in values else None
        if text is None:
            out.append((row, path, None, want, 'asset missing'))
            continue
        hits = field_re(row['field']).findall(text)
        if len(hits) != 1:
            out.append((row, path, None, want, 'not one plain "  %s: number" line (found %d)' % (row['field'], len(hits))))
            continue
        problem = None if want is not None else 'the page has no SHIPPED value for ' + key
        out.append((row, path, float(hits[0][1]), want, problem))
    return out


def same(a, b):
    return a is not None and b is not None and abs(a - b) <= 1e-6 * max(1.0, abs(b))


def apply(rows):
    """Writes every differing value in place. Returns the paths it changed."""
    by_path = {}
    for row, path, have, want, problem in rows:
        if problem or same(have, want):
            continue
        by_path.setdefault(path, []).append((row['field'], want))
    for path, edits in by_path.items():
        text = open(path, encoding='utf-8', newline='').read()
        for field, want in edits:
            text, n = field_re(field).subn(lambda m: m.group(1) + fmt(want), text, count=1)
            assert n == 1, (path, field)
        with open(path, 'w', encoding='utf-8', newline='') as fh:
            fh.write(text)
    return sorted(by_path)


def load_maps(only=None):
    maps = []
    for name in sorted(os.listdir(MAPS)):
        if name.endswith('.json') and (only is None or name[:-5].lower() == only.lower()):
            with open(os.path.join(MAPS, name), encoding='utf-8') as fh:
                data = json.load(fh)
            if 'rows' in data:   # clones.json, the clone record, sits beside the maps
                maps.append(data)
    return maps


def values_for(vmap, settings_files, root=ROOT):
    html = open(os.path.join(root, vmap['page']), encoding='utf-8').read()
    values = parse_shipped(html)
    for f in settings_files:
        with open(f, encoding='utf-8') as fh:
            values.update(settings_from(json.load(fh)))
    return values, html


def run(args):
    maps = load_maps(args.vessel)
    if not maps:
        print('no map for %r in %s' % (args.vessel, os.path.relpath(MAPS, ROOT)))
        return 2
    bad = 0
    for vmap in maps:
        values, html = values_for(vmap, args.settings or [])
        labels = parse_spec_labels(html)
        rows = plan(vmap, values)
        if args.unmapped:
            mapped = {r['key'] for r in vmap['rows']}
            rest = [k for k in parse_shipped(html) if k not in mapped]
            print('%s: %d of %d studio keys have a home in the game; no home yet:' % (vmap['vessel'], len(mapped), len(mapped) + len(rest)))
            for k in rest:
                print('  %-18s %s' % (k, labels.get(k, '')))
            continue
        problems = [r for r in rows if r[4]]
        drift = [r for r in rows if not r[4] and not same(r[2], r[3])]
        for row, path, have, want, problem in problems:
            print('ERROR %s.%s (%s): %s' % (os.path.basename(path), row['field'], row['key'], problem))
        for row, path, have, want, _ in drift:
            print('%s %-26s %-14s artifact %s, Unity %s  (%s)' % ('DRIFT' if args.check else 'set  ', os.path.basename(path) + '.' + row['field'],
                                                                 row['key'], fmt(want), fmt(have), labels.get(row['key'], '')))
        if args.check:
            print('%s: %d mapped, %d match the artifact, %d drift, %d errors' % (vmap['vessel'], len(rows), len(rows) - len(drift) - len(problems), len(drift), len(problems)))
            bad += len(drift) + len(problems)
        else:
            changed = apply(rows)
            print('%s: %d mapped, %d written%s, %d errors' % (vmap['vessel'], len(rows), len(drift),
                                                            (' in ' + ', '.join(os.path.relpath(p, ROOT) for p in changed)) if changed else '', len(problems)))
            bad += len(problems)
    return 1 if bad else 0


def self_test():
    fails = []

    def want(cond, msg):
        if not cond:
            fails.append(msg)

    page = ("<script>\n  const SHIPPED = {\n    // a: 9 is a comment\n    a: 1.5, b: 2, c: -3e2,\n    a: 7,\n  };\n"
            "  const SPEC = [ ['g', 'a', 'The A row', 0, 2, 0.1] ];\n  const KEYS = ['a', 'b', 'c'];\n</script>")
    shipped = parse_shipped(page)
    want(shipped == {'a': 1.5, 'b': 2.0, 'c': -300.0}, 'parse_shipped: first value wins, comments ignored: %r' % shipped)
    want(parse_spec_labels(page).get('a') == 'The A row', 'SPEC labels')
    want(settings_from({'settings': {'changed': {'a': 2, 'x': True}}}) == {'a': 2.0}, 'decision settings')
    want(settings_from({'b': 4}) == {'b': 4.0}, 'flat settings')
    want(fmt(120000.0) == '120000' and fmt(0.55) == '0.55' and fmt(0.1 + 0.2) == '0.3', 'fmt')

    with tempfile.TemporaryDirectory() as d:
        os.makedirs(os.path.join(d, 'A'))
        asset = os.path.join(d, 'A', 'x.asset')
        body = 'MonoBehaviour:\n  m_Name: x\n  alpha: 1\n  beta: 0.25\n  nested:\n    alpha: 9\n  gamma:\n    Value: 2\n'
        with open(asset, 'w', encoding='utf-8', newline='') as fh:
            fh.write(body)
        with open(os.path.join(d, 'page.html'), 'w', encoding='utf-8') as fh:
            fh.write(page)
        vmap = {'vessel': 'T', 'page': 'page.html', 'rows': [
            {'key': 'a', 'asset': 'A/x.asset', 'field': 'alpha', 'scale': 2},
            {'key': 'b', 'asset': 'A/x.asset', 'field': 'beta'},
            {'key': 'b', 'asset': 'A/x.asset', 'field': 'gamma'},
            {'key': 'zz', 'asset': 'A/x.asset', 'field': 'beta'}]}
        values, _ = values_for(vmap, [], root=d)
        rows = plan(vmap, values, root=d)
        want(rows[2][4] and 'not one plain' in rows[2][4], 'a nested block is an error, never a guess')
        want(rows[3][4] and 'no SHIPPED value' in rows[3][4], 'a key the page lacks is an error')
        want(not same(rows[0][2], rows[0][3]), 'negative control: drift is seen before apply')
        apply(rows)
        text = open(asset, encoding='utf-8').read()
        want('  alpha: 3\n' in text and '  beta: 2\n' in text, 'apply writes scaled values: %r' % text)
        want('    alpha: 9\n' in text and '    Value: 2\n' in text, 'apply leaves nested lines alone')
        want(all(same(r[2], r[3]) for r in plan(vmap, values, root=d)[:2]), 'no drift after apply')
        settings = os.path.join(d, 'decision.json')
        with open(settings, 'w', encoding='utf-8') as fh:
            json.dump({'settings': {'changed': {'b': 0.5}}}, fh)
        values2, _ = values_for(vmap, [settings], root=d)
        want(values2['b'] == 0.5, 'a decision overrides SHIPPED')

    for vmap in load_maps():   # every real map points at real, writable fields
        values, _ = values_for(vmap, [])
        for row, path, have, want_v, problem in plan(vmap, values):
            want(problem is None, '%s map: %s.%s: %s' % (vmap['vessel'], os.path.basename(path), row['field'], problem))

    print('studio_to_unity self-test: ' + ('ok' if not fails else 'FAILED\n  ' + '\n  '.join(fails)))
    return 0 if not fails else 1


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--vessel')
    ap.add_argument('--settings', action='append', help='a decision / settings JSON from the artifact (repeatable; later wins)')
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--unmapped', action='store_true')
    ap.add_argument('--self-test', action='store_true')
    args = ap.parse_args(argv)
    return self_test() if args.self_test else run(args)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
