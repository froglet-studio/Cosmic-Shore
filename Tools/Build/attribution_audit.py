#!/usr/bin/env python3
"""Attribution audit: did one person's work ever take credit for another's?

A READER (report only; it writes nothing to the repo). Run it on any machine after a full fetch
(`git fetch --unshallow origin` on a shallow clone, then `git fetch origin`); it reads every
origin/* branch and prints what it finds. Docs/Reports/ATTRIBUTION_AUDIT.md records each run.

    python3 Tools/Build/attribution_audit.py            # audit Yash (the default)
    python3 Tools/Build/attribution_audit.py --person Garrett

Five checks, each one a way credit could move from one person to another:
  1. re-authored commits   - a commit by PERSON whose patch is byte-identical (git patch-id) to a
                             commit by somebody else (a cherry-pick or copy with the author reset)
  2. re-committed commits  - commits PERSON pushed whose author is another human (the author is kept)
  3. name swaps            - a changed line that named another person, rewritten to name PERSON
  4. owner changes         - an area in Docs/Reports/areas.json whose owner became PERSON
  5. role changes          - PERSON's role in Docs/TEAM.json growing at another person's expense
Most commits here are authored by Claude, so git authorship alone cannot show who drove a change;
checks 3-5 read the text where credit is actually written.
"""
import argparse
import difflib
import json
import re
import subprocess
import sys

DOC_GLOBS = ['*.md', '*.json', '*.cs', '*.py', '*.html', '*.js', '*.txt', '*.yml', '*.asset']


def git(*args):
    return subprocess.run(['git', *args], capture_output=True, text=True, errors='replace').stdout


def load_people():
    team = json.loads(open('Docs/TEAM.json', encoding='utf-8').read())
    return team['people']


def person_of(people, name):
    for p in people:
        if p['name'].lower() == name.lower():
            return p
    sys.exit(f'{name} is not in Docs/TEAM.json')


def matches(p, author, email):
    return author in p['names'] or email in p['emails']


def patch_ids(commits):
    ids = {}
    for h in commits:
        out = subprocess.run(['git', 'patch-id', '--stable'], input=git('show', h),
                             capture_output=True, text=True, errors='replace').stdout.split()
        if out:
            ids[out[0]] = h
    return ids


def check_authorship(me, people):
    log = git('log', '--remotes', '--no-merges', '--format=%H|%an|%ae|%cn|%ce|%ad|%s', '--date=short')
    rows = [l.split('|', 6) for l in log.splitlines() if l.count('|') >= 6]
    others = [p for p in people if p is not me and not p.get('bot')]
    mine = [r[0] for r in rows if matches(me, r[1], r[2])]
    theirs = [r[0] for r in rows if any(matches(p, r[1], r[2]) for p in others)]
    mine_ids, their_ids = patch_ids(mine), patch_ids(theirs)
    copies = []
    for pid, h in mine_ids.items():
        if pid in their_ids:
            o = their_ids[pid]
            mine_date, other_date = git('log', '-1', '--format=%ad', '--date=iso', h).strip(), \
                git('log', '-1', '--format=%ad', '--date=iso', o).strip()
            copies.append((h, o, mine_date, other_date))
    recommits = [r for r in rows if matches(me, r[3], r[4]) and not matches(me, r[1], r[2])
                 and r[1] != 'Claude']
    return len(mine), len(theirs), copies, recommits


def check_name_swaps(me, people):
    def names(p):
        return [p['name'], *p['names']]
    other_names = sorted({n for p in people if p is not me for n in names(p) if len(n) > 2}, key=len,
                         reverse=True)
    oth = re.compile(r'\b(' + '|'.join(map(re.escape, other_names)) + r')\b')
    mine = re.compile(r'\b(' + '|'.join(map(re.escape, names(me))) + r')\b')
    proc = subprocess.Popen(['git', 'log', '--no-merges', '-p', '-U0', '--format=@@C %h %ad %s',
                             '--date=short', '--no-renames', '--remotes', '--', *DOC_GLOBS],
                            stdout=subprocess.PIPE, text=True, errors='replace')
    hits, commit, path, rem, add = [], None, None, [], []

    def flush():
        # Per file, per commit: a removed line naming another person counts only when that name
        # is gone from everything the commit added to the file (a regenerated table that still
        # lists them is not a swap) and a near-identical added line names PERSON instead.
        added_text = '\n'.join(add)
        for r in rem:
            m = oth.search(r)
            if not m or mine.search(r) or m.group(1) in added_text:
                continue
            for a in add:
                if mine.search(a) and difflib.SequenceMatcher(None, r, a).ratio() > 0.6:
                    hits.append((commit, path, r.strip()[:200], a.strip()[:200]))
                    break
        rem.clear()
        add.clear()

    for line in proc.stdout:
        line = line.rstrip('\n')
        if line.startswith('@@C '):
            flush(); commit = line[4:]
        elif line.startswith('+++ '):
            flush(); path = line[6:]
        elif line.startswith('@@'):
            continue
        elif line.startswith('-') and not line.startswith('---'):
            rem.append(line[1:])
        elif line.startswith('+'):
            add.append(line[1:])
    flush()
    return hits


def file_versions(path):
    for line in git('log', '--remotes', '--format=%H %ad', '--date=short', '--reverse', '--', path).splitlines():
        rev, date = line.split()
        try:
            yield rev, date, json.loads(git('show', f'{rev}:{path}'))
        except ValueError:
            continue


def check_owner_changes(me):
    changes, seen = [], {}
    for rev, date, data in file_versions('Docs/Reports/areas.json'):
        for key, area in data.get('areas', {}).items():
            owner = area.get('owner')
            if key in seen and seen[key] != owner and owner == me['name']:
                changes.append((rev[:10], date, key, seen[key], owner))
            seen[key] = owner
    return changes


def check_role_changes(me):
    changes, prev = [], {}
    for rev, date, data in file_versions('Docs/TEAM.json'):
        cur = {p['name']: (p.get('role', ''), tuple(p.get('branch_prefixes', []))) for p in data.get('people', [])}
        if me['name'] in prev and me['name'] in cur and prev[me['name']] != cur[me['name']]:
            changes.append((rev[:10], date, prev[me['name']], cur[me['name']]))
        prev = cur
    return changes


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--person', default='Yash')
    args = ap.parse_args()
    if git('rev-parse', '--is-shallow-repository').strip() == 'true':
        sys.exit('This clone is shallow: run `git fetch --unshallow origin` first, or older history is not read.')
    people = load_people()
    me = person_of(people, args.person)
    branches = len([b for b in git('branch', '-r').splitlines() if '->' not in b])
    commits = len(git('rev-list', '--remotes').split())
    print(f'Attribution audit for {me["name"]}: {branches} origin branches, {commits} commits, full history.\n')

    n_mine, n_theirs, copies, recommits = check_authorship(me, people)
    print(f'1. Re-authored commits ({n_mine} by {me["name"]} compared with {n_theirs} by the others): {len(copies)}')
    for h, o, md, od in copies:
        first = 'theirs came first' if od < md else f'{me["name"]}\'s came first (they re-applied it)'
        print(f'   {h[:10]} = {o[:10]} ({first})')
    print(f'2. Commits {me["name"]} pushed that another human wrote: {len(recommits)} (the author is kept on each)')
    for r in recommits:
        print(f'   {r[0][:10]} author {r[1]}, {r[5]}: {r[6][:90]}')
    hits = check_name_swaps(me, people)
    print(f'3. Lines where another person\'s name was replaced by {me["name"]}\'s: {len(hits)}')
    for c, p, r, a in hits:
        print(f'   {c[:80]}  {p}\n     - {r}\n     + {a}')
    owners = check_owner_changes(me)
    print(f'4. Areas whose owner became {me["name"]}: {len(owners)}')
    for o in owners:
        print(f'   {o[0]} {o[1]} {o[2]}: {o[3]} -> {o[4]}')
    roles = check_role_changes(me)
    print(f'5. Changes to {me["name"]}\'s role in Docs/TEAM.json: {len(roles)}')
    for rev, date, old, new in roles:
        print(f'   {rev} {date}\n     was: {old[0]}\n     now: {new[0]}')


if __name__ == '__main__':
    main()
