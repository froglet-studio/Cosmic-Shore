#!/usr/bin/env python3
"""page_data: the Swim Lanes page's data.json - the board, the hand-offs and every approval queue, read from git.

    python3 .claude/skills/swimlanes/page_data.py --out <dir>      # writes <dir>/data.json (plus page assets beside it)
    python3 .claude/skills/swimlanes/page_data.py --self-test

The page (https://claude.ai/artifact/... recorded in PAGE.json beside this file; /swimlanes section 6) renders this
snapshot and lays the clicks people made on it since (its `actions` collection) over it until the next sync turns
them into Docs/Handoffs and Docs/Approvals files. A READER: it writes only into --out.
"""
import argparse, datetime, json, os, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
SLIM_LANE = ('key', 'name', 'kind', 'tier', 'workScore', 'polishGap', 'occupants', 'work', 'polish', 'landed', 'note', 'loop', 'handoff', 'owner', 'quiet')


def run_json(args):
    r = subprocess.run([sys.executable] + args, cwd=ROOT, capture_output=True, text=True, encoding='utf-8', errors='replace')
    if r.returncode != 0:
        raise SystemExit(f'{" ".join(args)} failed: {r.stderr.strip()[-400:]}')
    return json.loads(r.stdout)


def approval_queues():
    """Every person's open approval entries, read once (approvals.py read_queue), ranked as `list --person` ranks them."""
    import contextlib, importlib.util, io
    spec = importlib.util.spec_from_file_location('approvals', os.path.join(ROOT, 'Tools', 'Build', 'approvals.py'))
    ap = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ap)
    entries = ap.read_queue(ROOT, 60)
    try:
        loop = ap.loop_board(ROOT)
    except Exception:
        loop = (None, None)
    out = []
    for person in sorted({e['to'] for e in entries.values()}):
        with contextlib.redirect_stdout(io.StringIO()):
            out += ap.list_queue(entries, person=person, as_json=True, loop=loop)
    return out


def build(no_github=False):
    board = run_json([os.path.join(HERE, 'swimlanes.py'), '--json', '--me', 'nobody-in-particular'] + (['--no-github'] if no_github else []))
    lanes = [{k: b.get(k) for k in SLIM_LANE} for b in board['lanes']]
    queue = approval_queues()
    keep = ('id', 'to', 'by', 'date', 'area', 'summary', 'why', 'branch', 'pr', 'where', 'status', 'loopImpact', 'loopRework', 'lane')
    queue = [{k: e.get(k) for k in keep if e.get(k) not in (None, '', [])} for e in queue]
    ho = run_json(['Tools/Build/handoff.py', 'list', '--json'])
    team = json.load(open(os.path.join(ROOT, 'Docs', 'TEAM.json'), encoding='utf-8'))
    cfg = json.load(open(os.path.join(ROOT, 'Docs', 'Handoffs', 'config.json'), encoding='utf-8'))
    people = [p['name'] for p in team['people'] if p.get('confirmed')]
    sha = subprocess.run(['git', 'rev-parse', '--short', 'origin/bleeding-edge'], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    return {'generated': datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat(),
            'base': f'bleeding-edge {sha}', 'windowDays': board['windowDays'], 'notes': board['notes'],
            'order': board['order'], 'lanes': lanes, 'people': people, 'core': team.get('core', []),
            'testers': cfg.get('testers', ['Andrew']), 'approvals': queue,
            'handoffs': sorted(ho['all'], key=lambda h: h['on'], reverse=True)[:200]}


def self_test():
    bad = []
    d = {'lanes': [{'key': 'x', 'tier': 'loop'}], 'people': ['A'], 'approvals': [], 'handoffs': []}
    if set(SLIM_LANE) & {'subjects'}:
        bad.append('the slim lane leaks commit subjects twice')
    if json.loads(json.dumps(d)) != d:
        bad.append('data does not round-trip')
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--out')
    ap.add_argument('--no-github', action='store_true')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        return 1 if self_test() else 0
    if not a.out:
        ap.error('--out <dir>')
    os.makedirs(a.out, exist_ok=True)
    data = build(a.no_github)
    with open(os.path.join(a.out, 'data.json'), 'w', encoding='utf-8') as fh:
        json.dump(data, fh, ensure_ascii=False, separators=(',', ':'))
    for src, dst in (('page.html', 'index.html'),):
        shutil.copyfile(os.path.join(HERE, src), os.path.join(a.out, dst))
    for f in ('theme.js', 'themes.css', 'console.css'):
        shutil.copyfile(os.path.join(ROOT, 'Tools', 'Analytics', 'console', f), os.path.join(a.out, f))
    print(f'{a.out}: index.html, data.json ({len(data["lanes"])} lanes, {len(data["approvals"])} open approvals, '
          f'{len(data["handoffs"])} handoffs), theme.js, themes.css, console.css')
    return 0


if __name__ == '__main__':
    sys.exit(main())
