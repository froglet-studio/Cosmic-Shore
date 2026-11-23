#!/usr/bin/env python3
"""Risky merges: merge fast, flag the risky ones, sign off after the fact (Garrett, 2026-11-17: "as long as we have a
way to flag the risky stuff, and get sign off after the fact I would rather move as fast as we can and master fixing
things even faster").

    python3 Tools/Build/risky_merge.py check [--files a b ...] [--bypass] [--me Garrett]   # is this merge risky?
    python3 Tools/Build/risky_merge.py flag --pr N --by Garrett [--bypass] [--files ...]   # queue the sign-off entry
    python3 Tools/Build/risky_merge.py backfill --since 2026-11-16T20:00 --by Garrett      # merges already in
    python3 Tools/Build/risky_merge.py --self-test

What counts as risky lives in ONE place, Docs/Approvals/risky.json (shared AI code, vessel kit numbers, the
multiplayer protocol version, UGS and the backend, an area another person owns, a swim lane someone else occupies, and
a merge past a review rule). A category whose action is "queue" puts an entry in the signer's /approve list
(Docs/Approvals/<signer>/, through approvals.py, marked risky with the reasons); every risky merge also gets the
`risky-merge` label on its PR. /ship §5.7 runs `check` before it merges and `flag` when it is risky; the signer reads
the list with /approve (`approvals.py list --person Garrett`, risky entries first).

`check` exits 0 (not risky), 1 (label only) or 3 (queue a sign-off). It reads git and writes nothing; `flag` and
`backfill` write entry files, which are committed with the change (or, for a backfill, on any branch that merges).
"""
import argparse, datetime, fnmatch, importlib.util, json, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
CONFIG = 'Docs/Approvals/risky.json'
BASE = 'origin/bleeding-edge'
MERGE_RE = re.compile(r'^Merge pull request #(\d+) from [^/\s]+/(\S+)')


def repo_root():
    return subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=HERE, capture_output=True, text=True).stdout.strip() \
        or os.path.dirname(os.path.dirname(HERE))


def git(*a, cwd):
    return subprocess.run(['git', *a], cwd=cwd, capture_output=True, text=True).stdout


def approvals():
    spec = importlib.util.spec_from_file_location('approvals', os.path.join(HERE, 'approvals.py'))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def load_config(root):
    with open(os.path.join(root, CONFIG), encoding='utf-8') as fh:
        return json.load(fh)


def path_match(path, pattern):
    """`dir/*` is everything under dir; any other pattern matches one path segment per pattern segment, so
    `AI/*.cs` is the files directly in AI/ and not AI/Goby/x.cs."""
    if pattern.endswith('/*'):
        return path.startswith(pattern[:-1])
    ps, qs = path.split('/'), pattern.split('/')
    return len(ps) == len(qs) and all(fnmatch.fnmatchcase(p, q) for p, q in zip(ps, qs))


def classify(cfg, files, diff_text=None, bypass=False, owners=(), collisions=()):
    """The reasons this merge is risky: [{'key', 'name', 'action', 'detail'}]. diff_text(path) returns the changed
    lines of one file, for content categories; owners is [(owner, area)], collisions is [lane names]."""
    out = []
    for c in cfg['categories']:
        k, hit = c['key'], None
        if k == 'ruleset-bypass':
            hit = 'no approving review' if bypass else None
        elif k == 'owned-area':
            hit = '; '.join(f'{a} ({o})' for o, a in owners) or None
        elif k == 'lane':
            hit = ', '.join(collisions) or None
        else:
            skip = c.get('skip', [])
            fs = [f for f in files if any(path_match(f, p) for p in c.get('paths', []))
                  and not any(fnmatch.fnmatchcase(os.path.basename(f), s) for s in skip)]
            if fs and c.get('content'):
                rx = re.compile(c['content'])
                fs = [f for f in fs if diff_text and rx.search(diff_text(f) or '')]
            if fs:
                more = f' and {len(fs) - 4} more' if len(fs) > 4 else ''
                hit = ', '.join(fs[:4]) + more
        if hit:
            out.append({'key': k, 'name': c['name'], 'action': c.get('action', 'queue'), 'detail': hit})
    return out


def verdict(reasons):
    if any(r['action'] == 'queue' for r in reasons):
        return 3
    return 1 if reasons else 0


def changed_lines(root, rng):
    def f(path):
        d = git('diff', '-U0', rng, '--', path, cwd=root)
        return '\n'.join(l for l in d.splitlines() if l[:1] in '+-' and not l.startswith(('+++', '---')))
    return f


def owners_of(team, files, me):
    rows = []
    for o in team.declared_owners(files):
        who = o.get('owner', '')
        if me and who.split(' ')[0] == me:
            continue
        rows.append((who, o.get('area', '')))
    return rows


def summary_line(reasons):
    return '; '.join(f'{r["name"]}: {r["detail"]}' for r in reasons)


def print_reasons(reasons):
    v = verdict(reasons)
    if not v:
        print('not risky: merge it.')
        return
    print('RISKY - label the PR `risky-merge`' + (' and queue a sign-off (`flag`)' if v == 3 else ' (label only)') + ':')
    for r in reasons:
        print(f'  [{r["action"]}] {r["name"]}: {r["detail"]}')


def queue(root, team, ap, signer, by, pr, branch, reasons, title, files, now=None):
    summary = f'Risky merge PR #{pr}' + (f' ({title})' if title else '') + ' went in without waiting for you.'
    return ap.add(root, team, signer, by, 'risky merges', summary, summary_line(reasons), files, branch, pr, '',
                  now=now, risky=reasons)


def merges_since(root, since, ref):
    """[(sha, pr, branch, date, merger name, merger email)] first-parent merge commits on ref since an ISO time, oldest first."""
    out = []
    for line in git('log', '--first-parent', '--merges', f'--since={since}', '--format=%H%x09%cI%x09%an%x09%ae%x09%s',
                    ref, cwd=root).splitlines():
        sha, date, name, email, subj = line.split('\t', 4)
        m = MERGE_RE.match(subj)
        if m:
            out.append((sha, int(m.group(1)), m.group(2), date, name, email))
    return out[::-1]


def self_test():
    cfg = {'categories': [
        {'key': 'ruleset-bypass', 'name': 'bypass', 'action': 'label'},
        {'key': 'shared-ai', 'name': 'ai', 'action': 'queue', 'paths': ['A/AI/*.cs', 'A/AI/Boost/*']},
        {'key': 'kit', 'name': 'kit', 'action': 'queue', 'paths': ['K/*'], 'skip': ['*.meta']},
        {'key': 'protocol', 'name': 'proto', 'action': 'queue', 'paths': ['N.prefab'], 'content': 'ProtocolVersion'},
        {'key': 'owned-area', 'name': 'owned', 'action': 'queue'},
        {'key': 'lane', 'name': 'lane', 'action': 'queue'}]}
    fails = []

    def keys(**kw):
        return sorted(r['key'] for r in classify(cfg, **kw))

    def expect(name, got, want):
        if got != want:
            fails.append(f'{name}: got {got}, want {want}')

    expect('plain change', keys(files=['Docs/x.md']), [])
    expect('bypass alone labels', verdict(classify(cfg, files=['Docs/x.md'], bypass=True)), 1)
    expect('top-level AI file', keys(files=['A/AI/AIPilot.cs']), ['shared-ai'])
    expect('per-hull AI folder is not shared', keys(files=['A/AI/Goby/GobyAI.cs']), [])
    expect('recursive dir', keys(files=['A/AI/Boost/Sub/p.cs']), ['shared-ai'])
    expect('meta skipped', keys(files=['K/a.asset.meta']), [])
    expect('kit asset', keys(files=['K/x/a.asset']), ['kit'])
    expect('prefab without the version line', keys(files=['N.prefab'], diff_text=lambda f: '+  m_Name: x'), [])
    expect('protocol bump', keys(files=['N.prefab'], diff_text=lambda f: '-    ProtocolVersion: 28\n+    ProtocolVersion: 29'),
           ['protocol'])
    expect('owner and lane', keys(files=[], owners=[('Shombith', 'backend')], collisions=['Grizzly']), ['lane', 'owned-area'])
    expect('queue wins', verdict(classify(cfg, files=['A/AI/AIPilot.cs'], bypass=True)), 3)
    # The real config parses and names every category the script knows.
    real = load_config(repo_root())
    known = {'ruleset-bypass', 'shared-ai', 'vessel-kit', 'protocol', 'backend', 'owned-area', 'lane'}
    expect('real config categories', sorted({c['key'] for c in real['categories']} - known), [])
    expect('real: AIPilot is shared', [r['key'] for r in classify(real, ['Assets/_Scripts/Controller/AI/AIPilot.cs'])],
           ['shared-ai'])
    expect('real: a hull AI is not', classify(real, ['Assets/_Scripts/Controller/AI/Goby/GobyPilot.cs']), [])
    for f in fails:
        print('FAIL', f)
    print('risky_merge self-test:', 'FAILED' if fails else 'OK')
    return bool(fails)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--self-test', action='store_true')
    sub = p.add_subparsers(dest='cmd')
    c = sub.add_parser('check', help='is this merge risky (exit 0 no, 1 label only, 3 queue a sign-off)')
    f = sub.add_parser('flag', help='queue the sign-off entry for a risky merge (commit it on the branch before merging)')
    for s in (c, f):
        s.add_argument('--files', nargs='*', help='changed files (default: this branch against bleeding-edge)')
        s.add_argument('--bypass', action='store_true', help='the merge goes past a review rule (PR mergeable_state blocked)')
        s.add_argument('--me', help='the person driving this session (their own areas are not risky to them)')
        s.add_argument('--no-lanes', action='store_true', help='skip the swim lane check (offline)')
        s.add_argument('--json', action='store_true')
    f.add_argument('--pr', type=int, required=True)
    f.add_argument('--by', required=True, help='the person driving this session')
    f.add_argument('--title', default='')
    f.add_argument('--branch')
    b = sub.add_parser('backfill', help='queue entries for risky merges already on bleeding-edge')
    b.add_argument('--since', required=True, help='ISO time, e.g. 2026-11-16T20:00Z')
    b.add_argument('--by', required=True, help='who the entries are from when the merge commit names nobody in TEAM.json')
    b.add_argument('--ref', default=BASE)
    b.add_argument('--bypass', action='store_true', help='treat every merge in the window as past a review rule')
    b.add_argument('--branches', default='', help='regex: only merges whose head branch matches')
    b.add_argument('--dry-run', action='store_true')
    a = p.parse_args()
    if a.self_test:
        return 1 if self_test() else 0
    if not a.cmd:
        p.print_help()
        return 0
    root = repo_root()
    cfg = load_config(root)
    ap = approvals()
    wwh, _ = ap.tools(root)
    team = wwh.Team.load(root)
    signer = cfg.get('signer', 'Garrett')

    if a.cmd in ('check', 'flag'):
        me = ap.me_of(team, root, a.me or getattr(a, 'by', None))
        files = a.files or ap.changed_files(root, BASE)
        base = git('merge-base', BASE, 'HEAD', cwd=root).strip() or BASE
        collisions = []
        if not a.no_lanes:
            r = ap.check(root, files, me, ap.current_branch(root), None, True, 30)
            collisions = [row['name'] for row in r['collisions']]
        reasons = classify(cfg, files, changed_lines(root, f'{base}...HEAD'), a.bypass, owners_of(team, files, me),
                           collisions)
        if a.cmd == 'check':
            if a.json:
                print(json.dumps({'verdict': verdict(reasons), 'label': cfg.get('label'), 'reasons': reasons}, indent=1))
            else:
                print_reasons(reasons)
            return verdict(reasons)
        if verdict(reasons) < 3:
            print_reasons(reasons)
            print('nothing to queue' + (': label the PR only' if reasons else ''))
            return verdict(reasons)
        eid, rel = queue(root, team, ap, signer, a.by, a.pr, a.branch or ap.current_branch(root), reasons, a.title, files)
        print_reasons(reasons)
        print(f'queued {eid} for {signer}: {rel}\nCommit it on the branch before merging, and add the '
              f'`{cfg.get("label")}` label to PR #{a.pr}.')
        return 3

    if a.cmd == 'backfill':
        rx = re.compile(a.branches) if a.branches else None
        n = 0
        for sha, pr, branch, date, name, email in merges_since(root, a.since, a.ref):
            if rx and not rx.search(branch):
                continue
            files = [l for l in git('diff', '--name-only', f'{sha}^1', sha, cwd=root).splitlines() if l]
            reasons = classify(cfg, files, changed_lines(root, f'{sha}^1..{sha}'), a.bypass, owners_of(team, files, None))
            v = verdict(reasons)
            print(f'#{pr} {branch}: ' + ({0: 'not risky', 1: 'label only'}.get(v) or 'QUEUE ' + summary_line(reasons)))
            if v == 3 and not a.dry_run:
                subj = git('log', '-1', '--format=%b', sha, cwd=root).strip().splitlines()
                now = datetime.datetime.fromisoformat(date.replace('Z', '+00:00')).astimezone(datetime.timezone.utc)
                by = team.person(name, email) or a.by   # whoever merged it; --by when git cannot say
                queue(root, team, ap, signer, by, pr, branch, reasons, subj[0] if subj else '', files, now=now)
                n += 1
        print(f'{n} entr{"y" if n == 1 else "ies"} queued for {signer}' + (' (dry run)' if a.dry_run else ''))
        return 0
    return 0


if __name__ == '__main__':
    sys.exit(main())
