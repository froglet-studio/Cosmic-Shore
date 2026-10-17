#!/usr/bin/env python3
"""Who works where: the collaborators behind the paths you are about to touch, from git, before you touch them.

    python3 Tools/Build/who_works_where.py query Assets/_Scripts/Controller/Vessel Port/src   # landed + in flight
    python3 Tools/Build/who_works_where.py query --days 14 --json Docs/Studios
    python3 Tools/Build/who_works_where.py map            # print the team map (people x areas, open branches)
    python3 Tools/Build/who_works_where.py map --write    # refresh the generated section of Docs/TEAM.md
    python3 Tools/Build/who_works_where.py --self-test

Most commits are authored by Claude, so a commit's author rarely says which PERSON drove it. A person is found
by, in order: their own commits (names / emails in Docs/TEAM.json), a Claude commit they committed, the pull
request they MERGED into bleeding-edge, and branch prefixes they own (TEAM.json `branch_prefixes`). Run
`git fetch origin` first: everything is read from origin/*.

  Landed:    pull requests merged into origin/bleeding-edge in the window that changed a path, with who merged
             them and whose own commits are in them, plus direct commits.
  In flight: remote branches updated in the window, not yet merged into bleeding-edge, whose changes touch a
             path - work someone may be in the middle of. Read it before redoing it.

A reader: it changes nothing but, with `map --write`, the generated block of Docs/TEAM.md.
"""
import argparse, collections, datetime, json, os, re, shutil, subprocess, sys, tempfile

TEAM = 'Docs/TEAM.json'
DOC = 'Docs/TEAM.md'
BASE = 'origin/bleeding-edge'
START, END = '<!-- who_works_where:map -->', '<!-- /who_works_where:map -->'
CLAUDE_EMAILS = {'noreply@anthropic.com', 'claude@anthropic.com'}
TIP_COMMITS = 40
PR_RE = re.compile(r'Merge pull request #(\d+) from [^/\s]+/(\S+)')


def git(*a, cwd=None):
    r = subprocess.run(['git'] + list(a), cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace')
    return r.stdout if r.returncode == 0 else ''


def area(path):
    """A path's work area: deep enough to tell vessels from ecology, shallow enough to group."""
    p = path.split('/')
    if p[0] == 'Assets' and len(p) > 2:
        if p[1] == '_Scripts' and len(p) > 3 and p[2] in ('Controller', 'UI', 'System', 'Utility', 'Editor', 'Game'):
            return '/'.join(p[:4])
        return '/'.join(p[:3])
    if p[0] in ('Port', 'Docs', 'Tools', '.claude') and len(p) > 2:
        return '/'.join(p[:3])
    return '/'.join(p[:2])


class Team:
    def __init__(self, data):
        self.data = data
        self.by_name, self.by_email, self.prefixes = {}, {}, []
        for person in data.get('people', []):
            for n in person.get('names', []):
                self.by_name[n.lower()] = person['name']
            for e in person.get('emails', []):
                self.by_email[e.lower()] = person['name']
            for pre in person.get('branch_prefixes', []):
                self.prefixes.append((pre, person['name']))
        self.prefixes.sort(key=lambda x: -len(x[0]))
        self.unattributed = data.get('unattributed_prefixes', {})

    @staticmethod
    def load(root):
        try:
            with open(os.path.join(root, TEAM), encoding='utf-8') as fh:
                return Team(json.load(fh))
        except (OSError, ValueError):
            return Team({})

    def person(self, name, email, cname='', cemail=''):
        """The person behind a commit: its author, else (a Claude commit) its committer. None = a session nobody claimed."""
        for n, e in ((name, email), (cname, cemail)):
            e = (e or '').lower()
            if e in CLAUDE_EMAILS:
                continue
            hit = self.by_email.get(e) or self.by_name.get((n or '').lower())
            if hit:
                return hit
            if (n or '').lower() != 'claude' and n:
                return n          # a human git knows but TEAM.json does not: report them as git names them
        return None

    def branch_owner(self, branch):
        for pre, who in self.prefixes:
            if branch.startswith(pre):
                return who
        for pre, label in self.unattributed.items():
            if branch.startswith(pre):
                return None
        return None

    def branch_label(self, branch):
        for pre, label in self.unattributed.items():
            if branch.startswith(pre):
                return label
        return 'owner unknown'


def touches(files, paths):
    if not paths:
        return list(files)
    norm = [p.rstrip('/') for p in paths]
    return [f for f in files if any(f == p or f.startswith(p + '/') for p in norm)]


def humans_in(team, rng, cwd):
    out = git('log', '--no-merges', '--format=%an\t%ae\t%cn\t%ce', rng, cwd=cwd)
    seen = collections.Counter()
    for line in out.splitlines():
        parts = (line.split('\t') + ['', '', '', ''])[:4]
        who = team.person(*parts)
        if who:
            seen[who] += 1
    return [w for w, _ in seen.most_common()]


def landed(team, days, paths, cwd, base=BASE):
    since = f'--since={days}.days'
    rows = []
    for line in git('log', base, since, '--first-parent', '--merges', '--format=%H\t%an\t%ae\t%cI\t%s', cwd=cwd).splitlines():
        h, an, ae, date, subj = (line.split('\t') + [''] * 5)[:5]
        m = PR_RE.search(subj)
        if not m:
            continue
        files = git('diff', '--name-only', f'{h}^1...{h}^2', cwd=cwd).split('\n')
        hit = touches([f for f in files if f], paths)
        if not hit:
            continue
        title = (git('log', '-1', '--format=%b', h, cwd=cwd).strip().splitlines() or [''])[0]
        rows.append({'kind': 'pr', 'date': date[:10], 'pr': int(m.group(1)), 'branch': m.group(2), 'title': title,
                     'merger': team.person(an, ae) or an, 'authors': humans_in(team, f'{h}^1..{h}^2', cwd),
                     'areas': sorted(set(area(f) for f in hit))})
    out = git('log', base, since, '--first-parent', '--no-merges', '--name-only',
              '--format=@@%H\t%an\t%ae\t%cn\t%ce\t%cI\t%s', cwd=cwd)
    for block in out.split('@@')[1:]:
        lines = [l for l in block.splitlines() if l.strip()]
        h, an, ae, cn, ce, date, subj = (lines[0].split('\t') + [''] * 7)[:7]
        hit = touches(lines[1:], paths)
        if not hit or len(lines) > 400:          # a whole-repo snapshot says nothing about who works where
            continue
        rows.append({'kind': 'commit', 'date': date[:10], 'sha': h[:9], 'title': subj,
                     'author': team.person(an, ae, cn, ce) or 'a Claude session', 'areas': sorted(set(area(f) for f in hit))})
    rows.sort(key=lambda r: r['date'], reverse=True)
    return rows


def in_flight(team, days, paths, cwd, base=BASE):
    cutoff = (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=days)).isoformat()
    rows = []
    refs = git('for-each-ref', '--sort=-committerdate', '--format=%(refname:short)\t%(committerdate:iso-strict)',
               'refs/remotes/origin', cwd=cwd)
    for line in refs.splitlines():
        ref, date = (line.split('\t') + [''])[:2]
        branch = ref[len('origin/'):] if ref.startswith('origin/') else ref
        if not date or _iso(date) < _iso(cutoff):
            continue
        if branch in ('HEAD', base.split('/', 1)[1]) or ref == 'origin':
            continue
        # The branch's OWN recent work: its commits in the window that bleeding-edge lacks. A long-dead branch built
        # on old history is otherwise "ahead" by hundreds of commits every collaborator ever made.
        # Capped at the newest TIP_COMMITS: a branch cut from history that reached bleeding-edge only as a squashed copy
        # (the Stoat port, 2026-10-13) carries hundreds of commits git cannot see were merged; its own work is at the tip.
        # Only commits no OTHER remote branch has: shared lineage (every branch cut from the same un-merged history)
        # is nobody's in-flight work, and work contained in another branch shows up under that branch.
        out = git('log', f'--since={days}.days', f'--max-count={TIP_COMMITS}', '--no-merges', '--name-only',
                  '--format=@@%an\t%ae\t%cn\t%ce', ref, '--not', base, f'--exclude=origin/{branch}',
                  '--exclude=origin/HEAD', '--remotes=origin', cwd=cwd)
        blocks = out.split('@@')[1:]
        if not blocks:
            continue                           # merged, empty, or only merges in the window
        files, seen = collections.Counter(), collections.Counter()
        for block in blocks:
            lines = [l for l in block.splitlines() if l.strip()]
            who = team.person(*(lines[0].split('\t') + ['', '', '', ''])[:4])
            if who:
                seen[who] += 1
            if len(lines) <= 400:              # a whole-repo snapshot says nothing about the branch's work
                files.update(lines[1:])
        hit = touches(list(files), paths)
        if not hit:
            continue
        owner = team.branch_owner(branch)
        people = [w for w, _ in seen.most_common()]
        if owner and owner not in people:
            people.insert(0, owner)
        last = git('log', '-1', '--format=%s', ref, cwd=cwd).strip()
        rows.append({'branch': branch, 'date': date[:10], 'people': people,
                     'label': '' if people else team.branch_label(branch), 'last': last,
                     'ahead': len(blocks), 'capped': len(blocks) >= TIP_COMMITS,
                     'areas': [a for a, _ in collections.Counter(area(f) for f in hit for _ in range(files[f])).most_common(6)]})
    return rows


def _iso(s):
    try:
        return datetime.datetime.fromisoformat(s.replace('Z', '+00:00'))
    except ValueError:
        return datetime.datetime.min.replace(tzinfo=datetime.timezone.utc)


def print_query(paths, land, flight, days):
    print(f'Who works on {", ".join(paths) or "the whole repo"} (last {days} days; read from origin/*, fetch first)')
    print('\nIN FLIGHT (unmerged branches touching it - read before redoing or overriding):')
    if not flight:
        print('  none')
    for r in flight:
        who = ', '.join(r['people']) if r['people'] else r['label']
        n = f'{r["ahead"]}+' if r.get('capped') else str(r['ahead'])
        print(f'  {r["date"]}  {r["branch"]}  [{who}]  {n} recent commit(s)  "{r["last"][:90]}"')
        print(f'      touches {", ".join(r["areas"])}')
    print('\nLANDED on bleeding-edge:')
    if not land:
        print('  none')
    for r in land[:25]:
        if r['kind'] == 'pr':
            by = f'merged by {r["merger"]}' + (f'; commits by {", ".join(r["authors"])}' if r['authors'] else '; Claude sessions')
            print(f'  {r["date"]}  #{r["pr"]} {r["branch"]}  [{by}]  "{r["title"][:80]}"')
        else:
            print(f'  {r["date"]}  {r["sha"]}  [{r["author"]}]  "{r["title"][:80]}"')
    people = collections.Counter()
    for r in land:
        for p in ([r['merger']] + r['authors']) if r['kind'] == 'pr' else [r['author']]:
            if p and p != 'a Claude session':
                people[p] += 1
    for r in flight:
        for p in r['people']:
            people[p] += 2
    if people:
        print('\nTALK TO: ' + ', '.join(f'{p} ({n})' for p, n in people.most_common(6)) +
              '  - their work is here; build on it, and ask before replacing it (Docs/TEAM.md).')


def build_map(team, days, cwd):
    land, flight = landed(team, days, [], cwd), in_flight(team, days, [], cwd)
    per = collections.defaultdict(collections.Counter)
    for r in land:
        who = [r['merger']] + r['authors'] if r['kind'] == 'pr' else [r['author']]
        for w in set(x for x in who if x and x != 'a Claude session'):
            for a in r['areas']:
                per[w][a] += 1
    lines = [f'_Generated by `python3 Tools/Build/who_works_where.py map --write` on '
             f'{datetime.date.today().isoformat()} from origin/* (last {days} days). Do not edit by hand._', '',
             '| Person | Landed areas (pull requests merged or authored, count) |', '|---|---|']
    core = team.data.get('core', [])
    for w in sorted(per, key=lambda x: (x not in core, -sum(per[x].values()))):
        top = ', '.join(f'`{a}` {n}' for a, n in per[w].most_common(8))
        lines.append(f'| **{w}** | {top} |')
    lines += ['', f'**In flight** (unmerged branches updated in the last {days} days, newest first):', '',
              '| Branch | Who | Updated | Ahead | Touches |', '|---|---|---|---|---|']
    for r in flight[:40]:
        who = ', '.join(r['people']) if r['people'] else '_' + r['label'] + '_'
        lines.append(f'| `{r["branch"]}` | {who} | {r["date"]} | {r["ahead"]} | {", ".join(f"`{a}`" for a in r["areas"][:4])} |')
    if len(flight) > 40:
        lines.append(f'| ... {len(flight) - 40} more | | | | |')
    return '\n'.join(lines) + '\n'


def write_map(root, text):
    path = os.path.join(root, DOC)
    with open(path, encoding='utf-8') as fh:
        doc = fh.read()
    i, j = doc.find(START), doc.find(END)
    if i < 0 or j < i:
        raise SystemExit(f'{DOC}: the markers {START} ... {END} are missing')
    with open(path, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(doc[:i + len(START)] + '\n' + text + doc[j:])


def self_test():
    """A throwaway origin with a merged PR by a Claude session, a person's direct commit and an unmerged branch."""
    bad = []
    tmp = tempfile.mkdtemp(prefix='wwh-')
    try:
        def run(*a, cwd, env=None):
            e = dict(os.environ, GIT_CONFIG_NOSYSTEM='1', **(env or {}))
            subprocess.run(list(a), cwd=cwd, check=True, capture_output=True, env=e)

        def commit(cwd, path, who, email, msg, committer=None):
            full = os.path.join(cwd, path)
            os.makedirs(os.path.dirname(full), exist_ok=True)
            with open(full, 'a') as fh:
                fh.write(msg + '\n')
            run('git', 'add', path, cwd=cwd)
            cn, ce = committer or (who, email)
            run('git', 'commit', '-q', '-m', msg, cwd=cwd, env={'GIT_AUTHOR_NAME': who, 'GIT_AUTHOR_EMAIL': email,
                                                              'GIT_COMMITTER_NAME': cn, 'GIT_COMMITTER_EMAIL': ce})
        origin, work = os.path.join(tmp, 'origin.git'), os.path.join(tmp, 'work')
        run('git', 'init', '-q', '--bare', origin, cwd=tmp)
        run('git', 'init', '-q', '-b', 'bleeding-edge', work, cwd=tmp)
        run('git', 'remote', 'add', 'origin', origin, cwd=work)
        commit(work, 'Assets/_Scripts/Controller/Party/P.cs', 'Alice A', 'alice@x.y', 'party fix')
        run('git', 'checkout', '-q', '-b', 'claude/stoat', cwd=work)
        commit(work, 'Assets/_Scripts/Controller/Vessel/Stoat.cs', 'Claude', 'noreply@anthropic.com', 'stoat sling')
        run('git', 'checkout', '-q', 'bleeding-edge', cwd=work)
        run('git', 'merge', '-q', '--no-ff', 'claude/stoat', '-m', 'Merge pull request #7 from froglet-studio/claude/stoat\n\nThe Stoat sling',
            cwd=work, env={'GIT_AUTHOR_NAME': 'Bob B', 'GIT_AUTHOR_EMAIL': 'bob@x.y', 'GIT_COMMITTER_NAME': 'Bob B', 'GIT_COMMITTER_EMAIL': 'bob@x.y'})
        run('git', 'checkout', '-q', '-b', 'Ys-rework', cwd=work)
        commit(work, 'Assets/_Scripts/Controller/Vessel/Rhino.cs', 'Claude', 'noreply@anthropic.com', 'rhino rework')
        run('git', 'checkout', '-q', 'bleeding-edge', cwd=work)
        run('git', 'push', '-q', 'origin', 'bleeding-edge', 'Ys-rework', 'claude/stoat', cwd=work)
        run('git', 'fetch', '-q', 'origin', cwd=work)
        team = Team({'core': ['Alice', 'Bob'], 'people': [
            {'name': 'Alice', 'names': ['Alice A'], 'emails': ['alice@x.y']},
            {'name': 'Bob', 'names': ['Bob B'], 'emails': ['bob@x.y']},
            {'name': 'Yash', 'names': [], 'emails': [], 'branch_prefixes': ['Ys-']}],
            'unattributed_prefixes': {'claude/': 'a Claude Code session'}})
        vessel = 'Assets/_Scripts/Controller/Vessel'
        land, flight = landed(team, 30, [vessel], work), in_flight(team, 30, [vessel], work)
        prs = [r for r in land if r['kind'] == 'pr']
        if not prs or prs[0]['pr'] != 7 or prs[0]['merger'] != 'Bob' or prs[0]['title'] != 'The Stoat sling':
            bad.append(f'the merged PR is not attributed to its merger: {prs}')
        if [r['branch'] for r in flight] != ['Ys-rework'] or flight[0]['people'] != ['Yash']:
            bad.append(f'the unmerged branch is not found with its owner by prefix: {flight}')
        party = landed(team, 30, ['Assets/_Scripts/Controller/Party'], work)
        if [r.get('author') for r in party] != ['Alice']:
            bad.append(f'a person\'s direct commit is not attributed to them: {party}')
        if landed(team, 30, ['Docs'], work) or in_flight(team, 30, ['Docs'], work):
            bad.append('a path nobody touched reported work')     # the negative control
        if team.person('Claude', 'noreply@anthropic.com', 'Claude', 'noreply@anthropic.com') is not None:
            bad.append('an unclaimed Claude commit was attributed to someone')
        if team.person('Claude', 'noreply@anthropic.com', 'Shombith', 'shombith@froglet.studio') != 'Shombith':
            bad.append('a Claude commit committed by a person is not theirs')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--self-test', action='store_true')
    ap.add_argument('--root', default='.')
    sub = ap.add_subparsers(dest='cmd')
    q = sub.add_parser('query', help='who works on these paths: landed and in flight')
    q.add_argument('paths', nargs='*')
    q.add_argument('--days', type=int, default=30)
    q.add_argument('--json', action='store_true')
    m = sub.add_parser('map', help='the team map: people x areas, open branches')
    m.add_argument('--days', type=int, default=45)
    m.add_argument('--write', action='store_true', help=f'refresh the generated block of {DOC}')
    a = ap.parse_args()
    if a.self_test:
        return 1 if self_test() else 0
    root = os.path.abspath(a.root)
    team = Team.load(root)
    if a.cmd == 'query':
        land, flight = landed(team, a.days, a.paths, root), in_flight(team, a.days, a.paths, root)
        if a.json:
            print(json.dumps({'paths': a.paths, 'days': a.days, 'inFlight': flight, 'landed': land}, indent=1))
        else:
            print_query(a.paths, land, flight, a.days)
        return 0
    if a.cmd == 'map':
        text = build_map(team, a.days, root)
        if a.write:
            write_map(root, text)
            print(f'wrote the map into {DOC}')
        else:
            print(text)
        return 0
    ap.print_help()
    return 2


if __name__ == '__main__':
    try:
        sys.exit(main())
    except BrokenPipeError:      # piped into head
        sys.exit(0)
