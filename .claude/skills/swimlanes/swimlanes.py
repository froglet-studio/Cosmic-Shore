#!/usr/bin/env python3
"""Swim lanes: which vessels and app-shell sections are OCCUPIED right now, and which open ones to pick up next.

    python3 .claude/skills/swimlanes/swimlanes.py                 # the board: open with work, occupied, needs polish, polished
    python3 .claude/skills/swimlanes/swimlanes.py --lane thresher # one lane in full: who is in it, its work, its polish gaps
    python3 .claude/skills/swimlanes/swimlanes.py --md | --json   # for a page or another tool
    python3 .claude/skills/swimlanes/swimlanes.py --days 5 --no-github
    python3 .claude/skills/swimlanes/swimlanes.py set thresher rough --by Garrett --note "the Belfry needs a sound pass"
    python3 .claude/skills/swimlanes/swimlanes.py --self-test

A lane is a vessel or a section of the app shell (the Voyages map, the Hangar, ...) that someone can hold in their
head; it is never a task. Lanes are listed in lanes.json beside this file; every vessel on the roster is a lane by
itself.

  OCCUPIED   an unmerged branch has pushed work focused on the lane in the last `window_days` (default 7). The lane
             opens again by itself when that work merges into bleeding-edge, or when the branch goes quiet. Past work
             that has merged never occupies a lane: history is context, not a block.
  OPEN       nobody is in it. Ranked, highest first:
               1. work waiting: open Froglet Tasks tickets that name it, editor hand tests not yet run
                  (Docs/UNITY_VERIFICATION_CHECKLIST.md), open Bug Ledger errors thrown from its files, placeholder
                  files, level-5 upgrades named but not gated;
               2. (occupied lanes are listed here, between the two)
               3. needs polish: no obvious work, but measured gaps (undesigned level-5 upgrades, no model dossier,
                  no Time game, no tests, a pile of TODOs, a style-guide worst screen) or a person said "rough";
               4. polished: none of that, or a person said "polished".

Read from origin/* (run `git fetch --prune origin` first), the tickets and open pull requests through `gh` (skipped
with --no-github or when gh is not signed in), and the repo's own files. A READER: it writes nothing except, with
`set`, the verdict in lanes.json.
"""
import argparse, collections, datetime, fnmatch, json, os, re, shutil, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
CATALOG = os.path.join(HERE, 'lanes.json')
BASE = 'origin/bleeding-edge'
TIP_COMMITS = 60
MAX_FILES = 400                     # a whole-repo snapshot says nothing about a lane
TICKET_WEIGHT = {'P0': 5, 'P1': 4, 'P2': 3, 'P3': 2}
CHECKLIST_RE = re.compile(r'^###\s+🔴\s+(.*?NOT EDITOR-VERIFIED.*)$', re.M)
STACK_AT_RE = re.compile(r'\(at (Assets/[^:)]+):\d+\)')
CAP_CHECKLIST, CAP_BUGS, CAP_PLACEHOLDERS = 4, 3, 2      # many of one kind is one kind of work, not more urgent
TODO_RE = re.compile(r'//\s*(TODO|FIXME|HACK)\b')


def repo_root():
    r = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=HERE, capture_output=True, text=True)
    return r.stdout.strip() or os.path.abspath(os.path.join(HERE, '..', '..', '..'))


def git(*a, cwd):
    r = subprocess.run(['git'] + list(a), cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace')
    return r.stdout if r.returncode == 0 else ''


def load_team(root):
    """who_works_where.Team: the one place a git identity or a branch prefix becomes a person."""
    sys.path.insert(0, os.path.join(root, 'Tools', 'Build'))
    try:
        import who_works_where as wwh
        return wwh.Team.load(root)
    except Exception:                                    # the tool moved: lanes still work, people read as git names them
        class Bare:
            by_name, data, owners = {}, {}, []
            def person(self, n, e, cn='', ce=''):
                return n if n and n.lower() != 'claude' else None
            def branch_owner(self, b):
                return None
            def declared_owners(self, paths):
                return []
        return Bare()


# --- Lanes --------------------------------------------------------------------------------------------------------

class Lane:
    def __init__(self, key, name, kind, tokens, paths=(), note=''):
        self.key, self.name, self.kind, self.note = key, name, kind, note
        self.tokens = [t for t in tokens if t]
        self.paths = [p.rstrip('/') for p in paths if p]
        alt = '|'.join(re.escape(t) for t in sorted(self.tokens, key=len, reverse=True))
        self.rx = re.compile(alt, re.I) if alt else None

    def matches(self, path):
        if any(path == p or path.startswith(p + '/') or fnmatch.fnmatch(path, p) for p in self.paths):
            return True
        return bool(self.rx) and token_hit(self.rx, path)

    def mentioned(self, text):
        return bool(self.rx) and token_hit(self.rx, text or '')


def token_hit(rx, s):
    """A token counts at a boundary: not inside a longer word ('Ratel' in 'generateLattice', 'Jack' in 'Jacket'), but at a
    CamelCase seam ('MinigameThresherTime') or a separator ('tool_vessel-changer__thresher'). A trailing plural s is fine."""
    for m in rx.finditer(s):
        i, j = m.start(), m.end()
        before_ok = i == 0 or not s[i - 1].isalpha() or (s[i].isupper() and s[i - 1].islower())
        k = j
        if k < len(s) and s[k] in 'sS' and (k + 1 == len(s) or not s[k + 1].islower()):
            k += 1                                            # a plural: 'Threshers', 'JACKS.md' for 'JACK'
        after_ok = k == len(s) or not s[k].isalpha() or (s[k].isupper() and s[k - 1].islower())
        if before_ok and after_ok:
            return True
    return False


def split_camel(name):
    return re.sub(r'(?<=[a-z])(?=[A-Z])', ' ', name)


def load_lanes(root, catalog):
    lanes = []
    vcfg = catalog.get('vessels', {})
    retired = set(vcfg.get('retired', []))
    aliases = vcfg.get('aliases', {})
    maps = os.path.join(root, 'Assets', 'Resources', 'ElementalAbilityMaps')
    roster = sorted(f[:-6] for f in os.listdir(maps) if f.endswith('.asset')) if os.path.isdir(maps) else []
    for v in roster:
        if v in retired:
            continue
        lanes.append(Lane(v.lower(), split_camel(v), 'vessel', [v] + aliases.get(v, [])))
    for s in catalog.get('shell', []):
        lanes.append(Lane(s['key'], s['name'], 'shell', s.get('tokens', []), s.get('paths', []), s.get('note', '')))
    return lanes


def focus_lanes(files, lanes):
    """The lanes a commit is ABOUT: each lane holding a quarter of its files or three of them (meta files left out). A
    sweep across twenty vessels' docs is about none of them; a fix that brushes one Hangar prefab among ten files is not
    Hangar work. A commit about three or more lanes at once is a sweep and occupies none."""
    files = [f for f in files if not f.endswith('.meta')]
    if not files or len(files) > MAX_FILES:
        return set()
    hits = collections.Counter()
    for f in files:
        for ln in lanes:
            if ln.matches(f):
                hits[ln.key] += 1
    focus = {k for k, n in hits.items() if n * 4 >= len(files) or n >= 3}
    return focus if len(focus) <= 2 else set()      # a re-bake or rename across the fleet is a sweep, not a lane


def named_lanes(text, lanes, most):
    """The lanes a sentence names, or none when it names more than `most` (a fleet-wide line is nobody's lane)."""
    hit = [ln.key for ln in lanes if ln.mentioned(text)]
    return set(hit) if len(hit) <= most else set()


# --- Occupancy ----------------------------------------------------------------------------------------------------

def ignored_branch(branch, prefixes):
    return any(branch == p or branch.startswith(p if p.endswith('/') else p + '/') or branch == p.rstrip('/')
               for p in prefixes)


def remote_branches(root, days, base):
    cutoff = datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=days)
    out = []
    for line in git('for-each-ref', '--sort=-committerdate', '--format=%(refname:short)\t%(committerdate:iso-strict)\t%(objectname)',
                    'refs/remotes/origin', cwd=root).splitlines():
        ref, date, sha = (line.split('\t') + ['', ''])[:3]
        if not ref.startswith('origin/') or ref in ('origin/HEAD', base):
            continue
        if parse_iso(date) < cutoff:
            break
        out.append((ref[len('origin/'):], date, sha))
    return out


def commits(root, ref, days, base):
    """The branch's own recent commits that bleeding-edge does not have: (sha, people-ish tuple, date, subject, files)."""
    out = git('log', f'--since={days}.days', f'--max-count={TIP_COMMITS}', '--no-merges', '--name-only',
              '--format=@@%H\t%an\t%ae\t%cn\t%ce\t%cI\t%s', ref, '--not', base, cwd=root)
    rows = []
    for block in out.split('@@')[1:]:
        lines = [l for l in block.splitlines() if l.strip()]
        h, an, ae, cn, ce, date, subj = (lines[0].split('\t') + [''] * 7)[:7]
        rows.append({'sha': h, 'who': (an, ae, cn, ce), 'date': date, 'subject': subj, 'files': lines[1:]})
    return rows


def occupancy(root, lanes, team, cfg, days, base, prs, merged_heads):
    """lane key -> [{branch, people, last, commits, subjects}] for every branch whose recent own work is about it."""
    skip = re.compile(cfg.get('ignore_subjects') or r'^$')
    prefixes = cfg.get('ignore_branches', [])
    occ = collections.defaultdict(list)
    for branch, date, sha in remote_branches(root, days, base):
        if ignored_branch(branch, prefixes) or merged_heads.get(branch) == sha:
            continue                                     # a bot's branch, or work already merged (by squash, say)
        rows = [c for c in commits(root, f'origin/{branch}', days, base) if not skip.search(c['subject'])]
        if not rows:
            continue
        per = collections.defaultdict(list)
        humans = collections.Counter()
        for c in rows:
            who = team.person(*c['who'])
            if who:
                humans[who] += 1
            for k in focus_lanes(c['files'], lanes):
                per[k].append(c)
        if not per:
            continue
        people = []
        owner = team.branch_owner(branch)
        if owner:
            people.append(owner)
        pr = prs.get(branch)
        if pr and pr.get('person') and pr['person'] not in people:
            people.append(pr['person'])
        people += [w for w, _ in humans.most_common() if w not in people]
        for k, cs in per.items():
            cs.sort(key=lambda c: c['date'], reverse=True)
            occ[k].append({'branch': branch, 'people': people, 'last': cs[0]['date'][:10], 'commits': len(cs),
                           'subjects': [c['subject'] for c in cs[:3]], 'pr': pr.get('number') if pr else None})
    for k in occ:
        occ[k].sort(key=lambda r: r['last'], reverse=True)
    return occ


def landed(root, lanes, team, days, base, skip_rx):
    """lane key -> the newest change that landed on bleeding-edge about it: when the lane last opened."""
    out = git('log', base, f'--since={days}.days', '--first-parent', '-m', '--name-only',
              '--format=@@%H\t%an\t%ae\t%cn\t%ce\t%cI\t%s', cwd=root)
    last = {}
    pr_re = re.compile(r'Merge pull request #(\d+) from [^/\s]+/(\S+)')
    for block in out.split('@@')[1:]:
        lines = [l for l in block.splitlines() if l.strip()]
        h, an, ae, cn, ce, date, subj = (lines[0].split('\t') + [''] * 7)[:7]
        if skip_rx.search(subj) and not subj.startswith('Merge pull request'):
            continue
        m = pr_re.search(subj)
        for k in focus_lanes(lines[1:], lanes):
            if k not in last:
                last[k] = {'date': date[:10], 'pr': int(m.group(1)) if m else None,
                           'branch': m.group(2) if m else '', 'by': team.person(an, ae, cn, ce) or '',
                           'subject': '' if m else subj}
    return last


# --- GitHub (optional) --------------------------------------------------------------------------------------------

def gh_json(args):
    if not shutil.which('gh'):
        return None
    try:
        r = subprocess.run(['gh'] + args, capture_output=True, text=True, timeout=60)
    except (OSError, subprocess.TimeoutExpired):
        return None
    if r.returncode != 0:
        return None
    try:
        return json.loads(r.stdout)
    except ValueError:
        return None


def repo_slug(root):
    url = git('remote', 'get-url', 'origin', cwd=root).strip()
    m = re.search(r'github\.com[:/]([^/]+/[^/.\s]+?)(?:\.git)?$', url) or re.search(r'/git/([^/]+/[^/\s]+?)(?:\.git)?$', url)
    return m.group(1) if m else 'froglet-studio/Cosmic-Shore'


def pull_requests(root, team):
    """Open PRs by head branch (their author is the person behind a Claude branch) and the head sha of every PR merged
    recently (a squash-merged branch is never in bleeding-edge's history, but it is done)."""
    slug = repo_slug(root)
    open_prs, merged = {}, {}
    for page in (1, 2):
        rows = gh_json(['api', f'repos/{slug}/pulls?state=open&per_page=100&page={page}'])
        if not rows:
            break
        for p in rows:
            login = (p.get('user') or {}).get('login', '')
            person = team.by_name.get(login.lower()) if hasattr(team, 'by_name') else None
            if not person and hasattr(team, 'by_email'):
                person = next((p for e, p in team.by_email.items() if e.split('@')[0] == login.lower()), None)
            open_prs[p['head']['ref']] = {'number': p['number'], 'person': person or login}
    rows = gh_json(['api', f'repos/{slug}/pulls?state=closed&sort=updated&direction=desc&per_page=100']) or []
    for p in rows:
        if p.get('merged_at'):
            merged[p['head']['ref']] = p['head']['sha']
    return open_prs, merged


def tickets(root, offline=None):
    args = [sys.executable, os.path.join(root, 'Tools', 'Build', 'team_tasks.py'), 'list', '--json']
    if offline:
        args += ['--offline', offline]
    try:
        r = subprocess.run(args, cwd=root, capture_output=True, text=True, timeout=90)
        return [t for t in json.loads(r.stdout) if t.get('state') == 'open' and not t.get('takenBy')]
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return None


def bugs(root):
    tool = os.path.join(root, 'Tools', 'Build', 'bug_ledger.py')
    if not os.path.exists(tool):
        return None
    tmp = tempfile.NamedTemporaryFile(suffix='.json', delete=False)
    tmp.close()
    try:
        subprocess.run([sys.executable, tool, 'collect', '--out', tmp.name], cwd=root, capture_output=True, timeout=180)
        with open(tmp.name, encoding='utf-8') as fh:
            data = json.load(fh)
        return [i for i in data.get('issues', []) if i.get('state') in ('open', 'reopened')]
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return None
    finally:
        os.unlink(tmp.name)


# --- Work and polish ----------------------------------------------------------------------------------------------

def read(root, rel):
    try:
        with open(os.path.join(root, rel), encoding='utf-8', errors='replace') as fh:
            return fh.read()
    except OSError:
        return ''


def l5_rows(root):
    tool = os.path.join(root, 'Tools', 'Build', 'element_ability_table.py')
    try:
        r = subprocess.run([sys.executable, tool, '--json'], cwd=root, capture_output=True, text=True, timeout=180)
        return json.loads(r.stdout)
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return []


def unquote(v):
    v = (v or '').strip()
    return v[1:-1].strip() if len(v) >= 2 and v[0] == v[-1] and v[0] in '\'"' else v


def worst_screens(root):
    """The rows of the UI style guide's 'five screens that break the guide most today' table."""
    text = read(root, 'Docs/UI_STYLE_GUIDE.md')
    m = re.search(r'^## 12\..*?$(.*?)^## ', text, re.M | re.S)
    return [l for l in (m.group(1).splitlines() if m else []) if re.match(r'\|\s*\d+\s*\|', l)]


def assess(root, lanes, files, ticket_rows, bug_rows, l5, verdicts):
    """lane key -> {'work': [(weight, text)], 'polish': [(weight, text)]}."""
    out = {ln.key: {'work': [], 'polish': []} for ln in lanes}
    lane_files = {ln.key: [f for f in files if ln.matches(f)] for ln in lanes}
    checklist = CHECKLIST_RE.findall(read(root, 'Docs/UNITY_VERIFICATION_CHECKLIST.md'))
    screens = worst_screens(root)
    tests = [f for f in files if '/Tests/' in f and f.endswith('.cs')]
    ticket_lanes = []
    for t in ticket_rows or []:
        on = named_lanes(t.get('area', ''), lanes, 2) or named_lanes(t.get('title', ''), lanes, 2)
        ticket_lanes.append((t, on))
    check_lanes = [(h, named_lanes(h, lanes, 3)) for h in checklist]
    for ln in lanes:
        w, p, mine = out[ln.key]['work'], out[ln.key]['polish'], lane_files[ln.key]
        for t, on in ticket_lanes:
            if ln.key in on:
                w.append((TICKET_WEIGHT.get(t.get('priority'), 2),
                          f'ticket {t["id"]} (#{t.get("number")}, {t.get("priority")}, {t.get("kind")}): {t["title"][:110]}'))
        for i, h in enumerate(h for h, on in check_lanes if ln.key in on):
            w.append((1 if i < CAP_CHECKLIST else 0, f'hand test not run in the editor: {h[:120]}'))
        thrown = collections.OrderedDict()
        for b in bug_rows or []:
            at = STACK_AT_RE.findall(b.get('stack', '') or '')
            site = at[0] if at else ''
            if (site and ln.matches(site)) or ln.mentioned(b.get('title', '')):
                thrown[b['id']] = f'console error {b["id"]}: {b.get("title", "")[:100]}' + (f' (at {site})' if site else '')
        for i, text in enumerate(thrown.values()):
            w.append((1 if i < CAP_BUGS else 0, text))
        ph = [f for f in mine if 'placeholder' in os.path.basename(f).lower() and not f.endswith('.meta')]
        for i, f in enumerate(ph[:6]):
            w.append((1 if i < CAP_PLACEHOLDERS else 0, f'placeholder file: {f}'))
        if ln.kind == 'vessel':
            vname = ln.tokens[0]
            rows = [dict(r, upgrade=unquote(r.get('upgrade'))) for r in l5 if r.get('vessel') == vname]
            for r in rows:
                if r['upgrade'] and not r.get('upgrade_gates'):
                    w.append((2, f'level-5 upgrade "{r["upgrade"]}" ({r["element"]}) is named but nothing gates it'))
            empty = [r['element'] for r in rows if not r.get('upgrade')]
            if empty:
                p.append((len(empty), f'{len(empty)} level-5 upgrade(s) not designed: {", ".join(empty)}'))
            if not any(f.startswith('Docs/VesselModels/') and f.endswith('.md') and ln.matches(f) for f in mine):
                p.append((1, 'no model dossier in Docs/VesselModels/ (/vessel-model)'))
            if not any(f.startswith('Assets/_SO_Assets/Games/') and f.endswith('.asset') for f in mine):
                p.append((1, 'no arcade card of its own in Assets/_SO_Assets/Games/ (its Time game, /arcadegame)'))
        for row in screens:
            cell = row.split('|')[2] if row.count('|') > 2 else row
            if ln.mentioned(cell):
                p.append((2, 'one of the style guide\'s five worst screens: ' + re.sub(r'\s+', ' ', cell).strip()[:90]))
        if not any(ln.matches(f) for f in tests):
            p.append((1, 'no edit-mode test names it'))
        todos = 0
        for f in mine:
            if f.endswith('.cs'):
                todos += len(TODO_RE.findall(read(root, f)))
        if todos >= 5:
            p.append((1, f'{todos} TODO / FIXME / HACK comments in its scripts'))
        v = verdicts.get(ln.key)
        if v:
            who = f'{v.get("by", "someone")} on {v.get("on", "?")}'
            if v.get('polish') == 'polished':
                out[ln.key]['polish'] = [(0, f'polished, said {who}' + (f': {v["note"]}' if v.get('note') else ''))]
            elif v.get('polish') == 'rough':
                p.append((3, f'rough, said {who}' + (f': {v["note"]}' if v.get('note') else '')))
            for item in v.get('work', []):
                w.append((3, f'{item} (said {who})'))
    return out


# --- The board ----------------------------------------------------------------------------------------------------

def build(root, args):
    catalog = json.load(open(CATALOG, encoding='utf-8'))
    days = args.days or catalog.get('window_days', 7)
    team = load_team(root)
    lanes = load_lanes(root, catalog)
    files = git('ls-files', cwd=root).splitlines()
    notes = []
    if not git('rev-parse', '--verify', '-q', BASE, cwd=root).strip():
        raise SystemExit(f'{BASE} is missing: run `git fetch --prune origin` first')
    prs, merged = ({}, {}) if args.no_github else pull_requests(root, team)
    if not args.no_github and not prs and not merged:
        notes.append('gh is not signed in or not reachable here: Claude branches show without the person who opened their pull request, and squash-merged branches may still read as occupied.')
    ticket_rows = None if args.no_github else tickets(root, args.tickets_offline)
    if ticket_rows is None:
        notes.append('Froglet Tasks tickets were not read (--no-github, or gh is not signed in): work waiting may be under-counted.')
    bug_rows = None if args.no_bugs else bugs(root)
    l5 = l5_rows(root)
    occ = occupancy(root, lanes, team, catalog, days, BASE, prs, merged)
    last = landed(root, lanes, team, catalog.get('landed_days', 14), BASE,
                  re.compile(catalog.get('ignore_subjects') or r'^$'))
    found = assess(root, lanes, files, ticket_rows, bug_rows, l5, catalog.get('verdicts', {}))
    owners = {}
    for ln in lanes:
        decl = [o['owner'] for o in team.declared_owners(ln.paths)] if ln.paths else []
        if decl or ln.note:
            owners[ln.key] = (ln.note or ('Owner: ' + ', '.join(decl) + ' (Docs/TEAM.json): ask before changing it.'))
    board = []
    for ln in lanes:
        f = found[ln.key]
        work = sum(wt for wt, _ in f['work'])
        gap = sum(wt for wt, _ in f['polish'])
        if occ.get(ln.key):
            tier = 'occupied'
        elif work > 0:
            tier = 'work'
        elif gap > 0:
            tier = 'polish'
        else:
            tier = 'polished'
        board.append({'key': ln.key, 'name': ln.name, 'kind': ln.kind, 'tier': tier, 'workScore': work, 'polishGap': gap,
                      'occupants': occ.get(ln.key, []), 'work': [t for _, t in sorted(f['work'], key=lambda x: -x[0])],
                      'polish': [t for _, t in sorted(f['polish'], key=lambda x: -x[0])], 'landed': last.get(ln.key),
                      'note': owners.get(ln.key, '')})
    order = {'work': 0, 'occupied': 1, 'polish': 2, 'polished': 3}

    board.sort(key=lambda b: b['name'])
    board.sort(key=lambda b: b['occupants'][0]['last'] if b['occupants'] else '', reverse=True)   # occupied: newest push first
    board.sort(key=lambda b: (order[b['tier']], -b['workScore'] if b['tier'] == 'work' else
                              -b['polishGap'] if b['tier'] == 'polish' else 0))
    return {'generated': datetime.date.today().isoformat(), 'windowDays': days, 'base': BASE, 'notes': notes,
            'lanes': board, 'order': list(order)}


def who(o):
    ppl = ', '.join(o['people']) if o['people'] else 'a Claude session (no person on record)'
    pr = f', PR #{o["pr"]}' if o.get('pr') else ''
    return f'{ppl} on {o["branch"]}{pr}, last push {o["last"]}, {o["commits"]} commit(s): "{o["subjects"][0][:80]}"'


def opened(b):
    l = b.get('landed')
    if not l:
        return ''
    what = f'#{l["pr"]} {l["branch"]}' if l.get('pr') else l.get('subject', '')[:60]
    return f'  (last landed {l["date"]}: {what}' + (f', {l["by"]}' if l.get('by') else '') + ')'


HEADS = {'work': 'OPEN - WORK WAITING (highest priority: pick one of these)',
         'occupied': 'OCCUPIED - someone has their head in it right now (do not collide: build elsewhere or talk to them)',
         'polish': 'OPEN - NEEDS POLISH (no obvious work, but it does not feel finished)',
         'polished': 'OPEN - POLISHED (lowest priority)'}


def print_board(data, full=False, md=False, limit=3):
    out = []
    h = (lambda s: out.append(f'\n## {s}\n')) if md else (lambda s: out.append(f'\n{s}'))
    out.append(('# Swim lanes' if md else 'SWIM LANES') + f' - {data["generated"]}; occupied = unmerged work pushed in the last '
               f'{data["windowDays"]} days (read from origin/*: run `git fetch --prune origin` first)')
    for n in data['notes']:
        out.append(('> ' if md else '  note: ') + n)
    for tier in data['order']:
        rows = [b for b in data['lanes'] if b['tier'] == tier]
        h(HEADS[tier] + f'  [{len(rows)}]')
        if not rows:
            out.append('  none')
        for i, b in enumerate(rows, 1):
            bullet = '- ' if md else f'  {i:>2}. '
            name = f'**{b["name"]}**' if md else b['name']
            tag = f'[{b["kind"]}]'
            if tier == 'occupied':
                out.append(f'{bullet}{name} {tag}: ' + who(b['occupants'][0]))
                for o in b['occupants'][1:]:
                    out.append(f'        also {who(o)}')
                if b['work'] and full:
                    out.append(f'        work waiting for after: {b["work"][0]}')
            elif tier == 'work':
                out.append(f'{bullet}{name} {tag} score {b["workScore"]}{opened(b)}')
                for t in b['work'][:None if full else limit]:
                    out.append(f'        - {t}')
                if not full and len(b['work']) > limit:
                    out.append(f'        - ... {len(b["work"]) - limit} more (--lane {b["key"]})')
            elif tier == 'polish':
                out.append(f'{bullet}{name} {tag}{opened(b)}: ' + '; '.join(b['polish'][:None if full else 2]))
            else:
                out.append(f'{bullet}{name} {tag}{opened(b)}' + (f': {b["polish"][0]}' if b['polish'] else ''))
            if b['note'] and tier != 'occupied':
                out.append(f'        ! {b["note"]}')
    out.append('\nA lane is a vessel or an app-shell section, not a task. It opens when its work merges into bleeding-edge.'
               ' Details: --lane <key>. Mark one: swimlanes.py set <key> polished|rough --by <you>.')
    print('\n'.join(out))


def print_lane(data, key):
    b = next((x for x in data['lanes'] if x['key'] == key or x['name'].lower() == key.lower()), None)
    if not b:
        raise SystemExit(f'no lane "{key}". Lanes: ' + ', '.join(x['key'] for x in data['lanes']))
    print(f'{b["name"]} [{b["kind"]}]: {HEADS[b["tier"]].split(" (")[0]}{opened(b)}')
    if b['note']:
        print(f'  ! {b["note"]}')
    print('\nIn it now:' if b['occupants'] else '\nIn it now: nobody')
    for o in b['occupants']:
        print(f'  {who(o)}')
        for s in o['subjects'][1:]:
            print(f'      "{s[:100]}"')
    print(f'\nWork waiting (score {b["workScore"]}):' if b['work'] else '\nWork waiting: none found')
    for t in b['work']:
        print(f'  - {t}')
    print('\nPolish gaps:' if b['polish'] else '\nPolish gaps: none measured')
    for t in b['polish']:
        print(f'  - {t}')


def set_verdict(key, polish, by, note):
    catalog = json.load(open(CATALOG, encoding='utf-8'))
    v = catalog.setdefault('verdicts', {})
    if polish == 'clear':
        v.pop(key, None)
    else:
        v[key] = {k: x for k, x in {'polish': polish, 'by': by, 'on': datetime.date.today().isoformat(), 'note': note}.items() if x}
    with open(CATALOG, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(catalog, fh, indent=2, ensure_ascii=False)
        fh.write('\n')
    print(f'{key}: {polish}' + (f' ({note})' if note else '') + f' - written to {os.path.relpath(CATALOG)}; commit it so everyone sees it.')


def parse_iso(s):
    try:
        return datetime.datetime.fromisoformat(s.replace('Z', '+00:00'))
    except ValueError:
        return datetime.datetime.min.replace(tzinfo=datetime.timezone.utc)


# --- Self-test ----------------------------------------------------------------------------------------------------

def self_test():
    bad = []
    ratel, jacks, thresher = Lane('h', 'Honeyguide', 'vessel', ['Honeyguide', 'Ratel']), \
        Lane('j', 'Jacks', 'vessel', ['Jacks', 'JACKS']), Lane('t', 'Thresher', 'vessel', ['Thresher'])
    voy = Lane('voyages', 'Voyages map', 'shell', ['Voyage'], ['Docs/Voyages'])
    for lane, path, want in [
            (ratel, 'Assets/_Scripts/Utility/GenerateLattice.cs', False), (ratel, 'Crew/RatelDynamoSeat.cs', True),
            (jacks, 'Assets/Jacket.cs', False), (jacks, 'R_VesselActions/JACKS.md', True), (jacks, 'JACKSON.md', False),
            (thresher, 'Assets/_Scenes/Multiplayer Scenes/MinigameThresherTime.unity', True),
            (thresher, 'Assets/_Graphics/Codex/tool_vessel-changer__thresher.png', True),
            (thresher, 'Assets/Threshers.cs', True), (thresher, 'Assets/Thresherly.cs', False),
            (voy, 'Assets/_Scripts/UI/Views/VoyageMapGraphic.cs', True), (voy, 'Docs/Voyages/DEPTH.md', True),
            (voy, 'Docs/Voyagers.md', False), (voy, 'Assets/Envoyage.cs', False)]:
        if lane.matches(path) != want:
            bad.append(f'{lane.name} matching {path} should be {want}')
    lanes = [ratel, jacks, thresher, voy]
    sweep = [f'Docs/{n}.md' for n in ('Ratel', 'Jacks', 'Thresher', 'Voyage', 'a', 'b', 'c', 'd', 'e', 'f')]
    if focus_lanes(sweep, lanes):
        bad.append('a sweep across four lanes and six other files was read as about some lane')       # negative control
    if focus_lanes(['Assets/ThresherTime.cs', 'Assets/ThresherTime.cs.meta', 'Docs/x.md'], lanes) != {'t'}:
        bad.append('a commit about the Thresher was not read as about the Thresher')
    if focus_lanes(['Docs/LEARNED_Thresher.md', 'Docs/LEARNED_Ratel.md'] + [f'Docs/L{i}.md' for i in range(12)], lanes):
        bad.append('a learner sweep brushing two lanes among fourteen files occupied them')     # negative control
    if focus_lanes([f'Bakes/{v}_{i}.asset' for v in ('Ratel', 'Jacks', 'Thresher') for i in range(3)], lanes):
        bad.append('a re-bake across three vessels occupied each of them')                       # negative control
    if named_lanes('Brittle Star, Sparrow, Stoat, Orca and Wrasse morph', [Lane(k, k, 'vessel', [k]) for k in
                   ('Sparrow', 'Stoat', 'Orca', 'Wrasse')], 3):
        bad.append('a fleet-wide line was given to every vessel it names')
    if named_lanes('Thresher Time: the Belfry', lanes, 2) != {'t'}:
        bad.append('a commit about the Thresher was not read as about the Thresher')
    tmp = tempfile.mkdtemp(prefix='lanes-')
    try:
        def run(*a, cwd, env=None):
            subprocess.run(list(a), cwd=cwd, check=True, capture_output=True, env=dict(os.environ, GIT_CONFIG_NOSYSTEM='1', **(env or {})))

        def commit(cwd, path, msg, who=('Claude', 'noreply@anthropic.com')):
            full = os.path.join(cwd, path)
            os.makedirs(os.path.dirname(full), exist_ok=True)
            with open(full, 'a') as fh:
                fh.write(msg + '\n')
            run('git', 'add', path, cwd=cwd)
            run('git', 'commit', '-q', '-m', msg, cwd=cwd, env={'GIT_AUTHOR_NAME': who[0], 'GIT_AUTHOR_EMAIL': who[1],
                                                              'GIT_COMMITTER_NAME': who[0], 'GIT_COMMITTER_EMAIL': who[1]})
        origin, work = os.path.join(tmp, 'o.git'), os.path.join(tmp, 'w')
        run('git', 'init', '-q', '--bare', origin, cwd=tmp)
        run('git', 'init', '-q', '-b', 'bleeding-edge', work, cwd=tmp)
        run('git', 'remote', 'add', 'origin', origin, cwd=work)
        commit(work, 'Assets/Voyages/VoyageMap.cs', 'voyage map landed', ('Garrett Milliron', 'g@x.y'))
        run('git', 'checkout', '-q', '-b', 'Ys-bleeding-edge', cwd=work)
        commit(work, 'Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherBelfry.cs', 'feat(thresher-time): the Belfry')
        commit(work, 'Docs/AI_SYSTEM/LEARNED_DRIVES_Jacks.md', 'chore(ai-learn): Jacks from 3 races')
        run('git', 'checkout', '-q', 'bleeding-edge', cwd=work)
        run('git', 'checkout', '-q', '-b', 'claude/voyage-done', cwd=work)
        commit(work, 'Assets/Voyages/VoyageJobs.cs', 'voyage jobs')
        run('git', 'checkout', '-q', 'bleeding-edge', cwd=work)
        run('git', 'merge', '-q', '--no-ff', 'claude/voyage-done', '-m', 'Merge pull request #9 from froglet-studio/claude/voyage-done', cwd=work)
        run('git', 'push', '-q', 'origin', 'bleeding-edge', 'Ys-bleeding-edge', 'claude/voyage-done', cwd=work)
        run('git', 'fetch', '-q', 'origin', cwd=work)

        class T:
            by_name = {}
            def person(self, n, e, cn='', ce=''):
                return {'Garrett Milliron': 'Garrett'}.get(n)
            def branch_owner(self, b):
                return 'Yash' if b.startswith('Ys-') else None
        cfg = {'ignore_subjects': r'^chore\(ai-learn\)', 'ignore_branches': ['bugledger']}
        occ = occupancy(work, lanes, T(), cfg, 7, BASE, {}, {})
        if list(occ) != ['t'] or occ['t'][0]['people'] != ['Yash'] or occ['t'][0]['branch'] != 'Ys-bleeding-edge':
            bad.append(f'the Thresher is not occupied by Yash on Ys-bleeding-edge alone: {dict(occ)}')
        if 'voyages' in occ:
            bad.append('the Voyages lane still reads as occupied after its branch merged')
        if 'j' in occ:
            bad.append('an ai-learn sweep commit occupied the Jacks lane')
        last = landed(work, lanes, T(), 14, BASE, re.compile(cfg['ignore_subjects']))
        if (last.get('voyages') or {}).get('pr') != 9:
            bad.append(f'the merge that opened the Voyages lane was not found: {last}')
        tip = git('rev-parse', 'origin/Ys-bleeding-edge', cwd=work).strip()
        if occupancy(work, lanes, T(), cfg, 7, BASE, {}, {'Ys-bleeding-edge': tip}):
            bad.append('a branch whose head was merged (squash) still occupied a lane')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('cmd', nargs='?', choices=['board', 'set'], default='board')
    ap.add_argument('rest', nargs='*', help='set: <lane> polished|rough|clear')
    ap.add_argument('--lane', help='one lane in full')
    ap.add_argument('--days', type=int, help='how recent a push must be to occupy a lane (default lanes.json window_days)')
    ap.add_argument('--full', action='store_true', help='every work item, every polish gap')
    ap.add_argument('--md', action='store_true')
    ap.add_argument('--json', action='store_true')
    ap.add_argument('--no-github', action='store_true', help='skip the tickets and pull requests (gh)')
    ap.add_argument('--no-bugs', action='store_true', help='skip the Bug Ledger')
    ap.add_argument('--tickets-offline', help='read the tickets from a team_tasks.py dump instead of GitHub')
    ap.add_argument('--by', help='set: who says so')
    ap.add_argument('--note', default='', help='set: why')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        return 1 if self_test() else 0
    if a.cmd == 'set':
        if len(a.rest) != 2 or a.rest[1] not in ('polished', 'rough', 'clear') or (a.rest[1] != 'clear' and not a.by):
            ap.error('set <lane> polished|rough|clear --by <name> [--note "..."]')
        set_verdict(a.rest[0].lower(), a.rest[1], a.by, a.note)
        return 0
    data = build(repo_root(), a)
    if a.json:
        print(json.dumps(data, indent=1, ensure_ascii=False))
    elif a.lane:
        print_lane(data, a.lane.lower())
    else:
        print_board(data, full=a.full, md=a.md)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except BrokenPipeError:
        sys.exit(0)
