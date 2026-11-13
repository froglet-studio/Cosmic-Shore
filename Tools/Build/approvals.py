#!/usr/bin/env python3
"""Approvals: changes go through; only an occupied swim lane stops them. Whoever's work a change touches hears about it
afterwards, through their own queue, and approves it or talks to the person who made it.

    python3 Tools/Build/approvals.py check [--me Garrett] [--files a b ...]   # before a commit / PR: collide or go
    python3 Tools/Build/approvals.py add --to Shombith --by Garrett --area "the /ship skill" \
        --summary "NO-GO now only on a swim lane collision" [--why "..."] [--paths p ...] [--pr N]
    python3 Tools/Build/approvals.py list --person Shombith      # their queue: waiting and to-talk-about first
    python3 Tools/Build/approvals.py list --from Garrett         # what Garrett's changes queued, and the answers
    python3 Tools/Build/approvals.py reply <id> approve|discuss|resolve --by Shombith [--note "..."]
    python3 Tools/Build/approvals.py --self-test

The process (Garrett, 2026-11-10: "only slow people down when they collide with a swimlane"):

  COLLISION  the change is ABOUT a swim lane (the /swimlanes skill: a vessel, game, app-shell section or system) that someone ELSE
             occupies right now (unmerged work pushed in the last 7 days). `check` exits 2 and names the occupant: the
             session stops and tells its person, who talks to the occupant or decides to go ahead anyway.
  HEADS-UP   anything else: declared owners (Docs/TEAM.json `owners`), people whose recent work landed in these paths,
             people with unmerged work on the same files, and occupants of a lane this change only brushes. Nothing
             waits. The session tells its person whose toes it is on, so they talk, and `add`s an entry to each one's
             queue whose work the change actually changes (re-tunes, replaces, retires, renames, or decides otherwise).
  THE QUEUE  Docs/Approvals/<Person>/<id>.json, one small file per change, committed on the branch that carries the
             change, so it lands with it. The owner runs `/approve`: `list` reads every branch, `reply` writes
             <id>.<stamp>.reply.json beside the entry on the owner's own branch (separate files, so nothing conflicts).
             approve = "I agree", discuss = "I want to talk to them about it" (the author sees it under `list --from`),
             resolve = settled after talking.

Read from origin/* (run `git fetch --prune origin` first) plus the working tree. `check` and `list` write nothing.
"""
import argparse, datetime, hashlib, importlib.util, json, os, re, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
QUEUE = 'Docs/Approvals'
CONFIG = QUEUE + '/config.json'
BASE = 'origin/bleeding-edge'
STATUSES = {'approve': 'approved', 'discuss': 'discuss', 'resolve': 'resolved'}
OPEN_STATUSES = ('pending', 'discuss')
ID_RE = re.compile(r'^\d{4}-\d{2}-\d{2}-[a-z0-9-]+-[0-9a-f]{6}$')


def repo_root():
    r = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=HERE, capture_output=True, text=True)
    return r.stdout.strip() or os.path.abspath(os.path.join(HERE, '..', '..'))


def git(*a, cwd, inp=None):
    r = subprocess.run(['git'] + list(a), cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace',
                       input=inp)
    return r.stdout if r.returncode == 0 else ''


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def tools(root):
    """who_works_where (people, owners, landed, in flight) and swimlanes (lanes, occupancy): the two readers this builds on."""
    wwh = load_module('who_works_where', os.path.join(root, 'Tools', 'Build', 'who_works_where.py'))
    sw = load_module('swimlanes', os.path.join(root, '.claude', 'skills', 'swimlanes', 'swimlanes.py'))
    return wwh, sw


def load_config(root):
    try:
        with open(os.path.join(root, CONFIG), encoding='utf-8') as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return {}


def is_shared(path, shared):
    """A path everyone writes to (the verification checklist, the docs index, this queue): nobody's toes."""
    if path.endswith('.meta'):
        return True
    return any(path == p.rstrip('/') or path.startswith(p.rstrip('/') + '/') for p in shared)


# --- People -------------------------------------------------------------------------------------------------------

def person_of(team, text):
    """A TEAM.json person from a name, a git name or an email (the email is only used to look the name up)."""
    if not text:
        return None
    t = text.strip().lower()
    if t in team.by_email:
        return team.by_email[t]
    if t in team.by_name:
        return team.by_name[t]
    for p in team.data.get('people', []):
        if p['name'].lower() == t:
            return p['name']
    return None


def people_in(team, text):
    """The TEAM.json people a sentence names ('Shombith (leads activation)' -> ['Shombith'])."""
    out = []
    for p in team.data.get('people', []):
        if re.search(r'\b' + re.escape(p['name']) + r'\b', text or '') and p['name'] not in out:
            out.append(p['name'])
    return out


def me_of(team, root, given):
    if given:
        who = person_of(team, given)
        if not who:
            raise SystemExit(f'"{given}" is not a person in Docs/TEAM.json (names: '
                             + ', '.join(p['name'] for p in team.data.get('people', [])) + ')')
        return who
    for key in ('FROGLET_PERSON',):
        if os.environ.get(key):
            return person_of(team, os.environ[key])
    return person_of(team, git('config', 'user.email', cwd=root).strip()) or \
        person_of(team, git('config', 'user.name', cwd=root).strip())


# --- check --------------------------------------------------------------------------------------------------------

def current_branch(root):
    return git('rev-parse', '--abbrev-ref', 'HEAD', cwd=root).strip()


def changed_files(root, base):
    files = set(git('diff', '--name-only', f'{base}...HEAD', cwd=root).split())
    files |= set(git('diff', '--name-only', 'HEAD', cwd=root).split())
    files |= set(git('ls-files', '--others', '--exclude-standard', cwd=root).split())
    return sorted(f for f in files if f)


def lane_hits(files, lanes):
    """lane key -> files of this change in it. Unlike swimlanes.focus_lanes there is no sweep cap: a change about four
    lanes at once still collides with an occupant of any of them."""
    hits = {}
    for f in files:
        for ln in lanes:
            if ln.matches(f):
                hits.setdefault(ln.key, []).append(f)
    return hits


def about(n, total):
    """The change is ABOUT a lane (a collision if occupied) when a quarter of its files, or three, are in it; fewer
    is a brush (a heads-up)."""
    return n >= 3 or n * 4 >= total


def check(root, files, me, branch, days, offline, landed_days):
    wwh, sw = tools(root)
    team = wwh.Team.load(root)
    cfg = load_config(root)
    shared = cfg.get('shared_paths', [])
    files = [f for f in files if not is_shared(f, shared)]
    catalog = json.load(open(sw.CATALOG, encoding='utf-8'))
    lanes = sw.load_lanes(root, catalog)
    by_key = {ln.key: ln for ln in lanes}
    have_base = bool(git('rev-parse', '--verify', '-q', BASE, cwd=root).strip())
    result = {'me': me, 'branch': branch, 'files': len(files), 'collisions': [], 'brushes': [], 'lanes': [],
              'people': {}, 'notes': []}
    if not files:
        result['notes'].append('no changed files outside the shared ones: nothing to check')
        return result
    if not have_base:
        result['notes'].append(f'{BASE} is missing: run `git fetch --prune origin`; occupancy and landed work were not read')
    hits = lane_hits(files, lanes)

    # Swim lanes: who occupies the ones this change is in, other than this branch and this person.
    occ = {}
    if have_base and hits:
        prs, merged = ({}, {}) if offline else sw.pull_requests(root, team)
        occ = sw.occupancy(root, lanes, team, catalog, days or catalog.get('window_days', 7), BASE, prs, merged)
        last = sw.landed(root, lanes, team, catalog.get('landed_days', 14), BASE,
                         re.compile(catalog.get('ignore_subjects') or r'^$'))
        handoffs, holder = sw.read_handoffs(root) if hasattr(sw, 'read_handoffs') else ({}, None)
        if handoffs:
            occ = sw.apply_handoffs(occ, handoffs, holder)   # a lane handed to someone is theirs; handed off open is free
        for k in list(occ):
            occ[k] = [o for o in occ[k] if not sw.superseded(o, last.get(k)) and o['branch'] != branch
                      and not (me and o['people'] and all(p == me for p in o['people']))]
    for k, fs in sorted(hits.items(), key=lambda kv: -len(kv[1])):
        row = {'lane': k, 'name': by_key[k].name, 'files': len(fs), 'about': about(len(fs), len(files)),
               'occupants': occ.get(k, [])}
        result['lanes'].append(row)
        if row['occupants']:
            (result['collisions'] if row['about'] else result['brushes']).append(row)

    # Whose work this touches: declared owners, recent landed work, unmerged work on the same files, brushed lanes.
    people = result['people']

    def note(who, why):
        if not who or who == me or who == 'a Claude session':
            return
        people.setdefault(who, [])
        if why not in people[who]:
            people[who].append(why)

    for o in team.declared_owners(files):
        for who in people_in(team, o['owner']):
            note(who, f'declared owner of {o["area"]} (Docs/TEAM.json, {o.get("said", "")})')
    if have_base:
        for r in wwh.landed(team, landed_days, files, root):
            ppl = (r['authors'] or [r['merger']]) if r['kind'] == 'pr' else [r['author']]
            what = f'#{r["pr"]} {r["branch"]}' if r['kind'] == 'pr' else r['sha']
            for who in ppl:
                note(who, f'landed {what} on {r["date"]} in {", ".join(r["areas"][:3])}')
        for r in wwh.in_flight(team, days or 14, files, root):
            if r['branch'] == branch:
                continue
            for who in r['people']:
                note(who, f'unmerged work on {r["branch"]} (last push {r["date"]}) in the same files')
    for row in result['brushes']:
        for o in row['occupants']:
            for who in o['people']:
                note(who, f'occupies the {row["name"]} lane on {o["branch"]}; this change brushes it ({row["files"]} file(s))')
    return result


def print_check(r, me):
    for n in r['notes']:
        print(f'note: {n}')
    if r['lanes']:
        print('Swim lanes this change is in:')
        for row in r['lanes']:
            state = 'OCCUPIED' if row['occupants'] else 'open'
            kind = 'about it' if row['about'] else 'brushes it'
            print(f'  {row["name"]} [{row["lane"]}]: {state}, {row["files"]} file(s), {kind}')
    if r['collisions']:
        print('\nCOLLISION - stop and tell your person before going on (Docs/TEAM.md rule 3):')
        for row in r['collisions']:
            for o in row['occupants']:
                ppl = ', '.join(o['people']) or o.get('label') or 'a Claude session (no person on record)'
                pr = f', PR #{o["pr"]}' if o.get('pr') else ''
                print(f'  {row["name"]}: {ppl} on {o["branch"]}{pr}, last push {o["last"]}: "{o["subjects"][0][:80]}"')
        print('  Talk to them first. If your person decides to go ahead anyway, go ahead, say "Affects: <occupant>\'s'
              ' <lane> work on <branch>" in the commit, and queue them an entry (`add`, below).')
    else:
        print('\nNo collision: nobody else occupies a swim lane this change is about. It goes through.')
    if r['people']:
        print('\nHEADS-UP - whose work this touches. Tell your person in a line so they talk to them:')
        for who, whys in r['people'].items():
            print(f'  {who}:')
            for w in whys[:4]:
                print(f'      {w}')
            if len(whys) > 4:
                print(f'      ... {len(whys) - 4} more')
        by = me or '<you>'
        print('\nQueue an entry for each one whose work this CHANGES (re-tunes, replaces, retires, renames, decides'
              ' otherwise, or edits an area they own); a pure addition beside their work needs none:')
        for who in r['people']:
            print(f'  python3 Tools/Build/approvals.py add --to {who} --by {by} --area "<their area>" '
                  f'--summary "<what changed in it, and why>"')
    elif not r['collisions']:
        print('Nobody else\'s recent work is in these files: no heads-up, no queue entry.')


# --- The queue ----------------------------------------------------------------------------------------------------

def slug(text, n=40):
    s = re.sub(r'[^a-z0-9]+', '-', (text or '').lower()).strip('-')
    return (s[:n].rstrip('-') or 'change')


def make_id(to, by, summary, now):
    h = hashlib.sha1(f'{to}|{by}|{summary}|{now.isoformat()}'.encode('utf-8')).hexdigest()[:6]
    return f'{now.date().isoformat()}-{slug(summary)}-{h}'


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(data, fh, indent=2, ensure_ascii=False)
        fh.write('\n')


def add(root, team, to, by, area, summary, why, paths, branch, pr, lane, now=None, loop=None):
    to_p, by_p = person_of(team, to), person_of(team, by)
    if not to_p:
        raise SystemExit(f'--to "{to}" is not a person in Docs/TEAM.json')
    if not by_p:
        raise SystemExit(f'--by "{by}" is not a person in Docs/TEAM.json (the person driving this session)')
    if to_p == by_p:
        raise SystemExit('an entry goes to someone else: your own work needs no approval')
    if not summary or not summary.strip():
        raise SystemExit('--summary is required: what changed in their work, in a sentence')
    now = now or datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0)
    eid = make_id(to_p, by_p, summary, now)
    entry = {'id': eid, 'to': to_p, 'by': by_p, 'date': now.isoformat(), 'area': area or '', 'summary': summary.strip(),
             'why': (why or '').strip(), 'branch': branch or '', 'pr': pr, 'lane': lane or '',
             'commit': git('rev-parse', '--short=9', 'HEAD', cwd=root).strip(), 'paths': sorted(set(paths or []))[:30],
             'loop': {k: v for k, v in (loop or {}).items() if v} or None}
    path = os.path.join(root, QUEUE, to_p, eid + '.json')
    write_json(path, {k: v for k, v in entry.items() if v not in (None, '', [])})
    return eid, os.path.relpath(path, root)


def reply(root, team, eid, verb, by, note, entries, now=None):
    if verb not in STATUSES:
        raise SystemExit(f'reply takes {", ".join(STATUSES)}')
    e = entries.get(eid) or next((x for k, x in entries.items() if k.startswith(eid)), None)
    if not e:
        raise SystemExit(f'no entry "{eid}" on any branch read (fetch first; `list --all` shows the ids)')
    by_p = person_of(team, by)
    if by_p != e['to']:
        raise SystemExit(f'only {e["to"]} answers this entry (it is their work); --by said {by!r}')
    if verb == 'discuss' and not (note or '').strip():
        raise SystemExit('discuss needs --note: what you want to talk to them about')
    now = now or datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0)
    stamp = now.strftime('%Y%m%dT%H%M%SZ')
    rep = {'id': e['id'], 'by': by_p, 'status': STATUSES[verb], 'date': now.isoformat(), 'note': (note or '').strip()}
    path = os.path.join(root, QUEUE, e['to'], f'{e["id"]}.{stamp}.reply.json')
    write_json(path, {k: v for k, v in rep.items() if v})
    return os.path.relpath(path, root), rep


def recent_refs(root, days):
    cutoff = datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=days)
    out = []
    for line in git('for-each-ref', '--sort=-committerdate', '--format=%(refname:short)\t%(committerdate:iso-strict)',
                    'refs/remotes/origin', cwd=root).splitlines():
        ref, date = (line.split('\t') + [''])[:2]
        if ref in ('origin/HEAD', 'origin', BASE):
            continue
        try:
            when = datetime.datetime.fromisoformat(date.replace('Z', '+00:00'))
        except ValueError:
            continue
        if when < cutoff:
            break
        out.append((ref, date[:10]))
    return out


def read_queue(root, days):
    """Every entry and reply under Docs/Approvals on the working tree, bleeding-edge and every branch pushed in the last
    `days`. An entry's `where` is where its change is: 'landed' (on bleeding-edge), 'local' (this working tree only), or
    the branch that carries it (not merged yet)."""
    entries, replies, seen_blob = {}, {}, set()

    def take(name, text, where, date=''):
        try:
            data = json.loads(text)
        except ValueError:
            return
        base = os.path.basename(name)
        if base.endswith('.reply.json'):
            key = (data.get('id'), base)
            replies.setdefault(key, data)
        elif base.endswith('.json') and ID_RE.match(base[:-5]):
            cur = entries.get(data.get('id'))
            if cur is None or (cur['where'] not in ('landed',) and where == 'landed'):
                data['where'] = where
                if date:
                    data['whereDate'] = date
                entries[data['id']] = data

    refs = []
    if git('rev-parse', '--verify', '-q', BASE, cwd=root).strip():
        refs.append((BASE, 'landed', ''))
    refs += [(ref, ref[len('origin/'):], date) for ref, date in recent_refs(root, days)]
    blobs = []
    for ref, where, date in refs:
        for line in git('ls-tree', '-r', ref, '--', QUEUE + '/', cwd=root).splitlines():
            meta, _, name = line.partition('\t')
            parts = meta.split()
            if len(parts) < 3 or not name.endswith('.json') or name == CONFIG:
                continue
            if parts[2] in seen_blob:
                continue                              # the same file, already read from an earlier ref (bleeding-edge first)
            seen_blob.add(parts[2])
            blobs.append((parts[2], name, where, date))
    if blobs:
        r = subprocess.run(['git', 'cat-file', '--batch'], cwd=root, capture_output=True,
                           input=''.join(b[0] + '\n' for b in blobs).encode('ascii'))
        out, i = r.stdout, 0
        for sha, name, where, date in blobs:
            nl = out.find(b'\n', i)
            if nl < 0:
                break
            hdr = out[i:nl].split()
            if len(hdr) < 3 or hdr[1] == b'missing':
                i = nl + 1
                continue
            size = int(hdr[2])
            take(name, out[nl + 1:nl + 1 + size].decode('utf-8', errors='replace'), where, date)
            i = nl + 1 + size + 1
    qdir = os.path.join(root, QUEUE)
    for dirpath, _, names in os.walk(qdir):
        for n in names:
            p = os.path.join(dirpath, n)
            rel = os.path.relpath(p, root)
            if not n.endswith('.json') or rel.replace(os.sep, '/') == CONFIG:
                continue
            try:
                text = open(p, encoding='utf-8').read()
            except OSError:
                continue
            base_id = n[:-5]
            if ID_RE.match(base_id) and base_id in entries:
                continue                              # already read from a branch
            take(n, text, 'local')
    by_entry = {}
    for (eid, _), r in replies.items():
        by_entry.setdefault(eid, []).append(r)
    for eid, e in entries.items():
        rs = sorted(by_entry.get(eid, []), key=lambda r: r.get('date', ''))
        e['replies'] = rs
        e['status'] = rs[-1]['status'] if rs else 'pending'
    return entries


# --- Loop impact: answer first what the elemental loop is building on (Garrett, 2026-11-12) -------------------------
# "/approval ... should be prioritizing sign off on the items most likely to affect the velocity of things through the
# process as things need to be rerun when a stakeholder catches an issue against the spirit of a vessel they have worked
# on, which evokes a retraining. /approval is to catch those sooner so they advance less before needing more retraining
# at each downstream step." An entry's impact is the loop work a "this is wrong" answer would send back NOW
# (elemental_loop.rework): it grows while the entry waits, as the vessel advances, so the oldest unread change to a
# vessel the loop is building on rises to the top.

LOOP_ELEMENTS = ('Time', 'Charge', 'Mass', 'Space')


def loop_board(root):
    """(elemental_loop module, its board), or (None, None) when the loop cannot be read; the queue still lists."""
    if not os.path.exists(os.path.join(root, 'Docs', 'ElementalLoop', 'loop.json')):
        return None, None
    try:
        el = load_module('elemental_loop', os.path.join(root, 'Tools', 'Build', 'elemental_loop.py'))
        return el, el.build(root)
    except Exception:                                   # a reader: never stop the queue over it
        return None, None


# Where a change can move how a vessel plays, and so what its intensities, AI and arena were built on. A path outside
# these (UI, HUD, colour, art, docs, tools, tests) or an area that says it is about the look sends nothing back.
LOOP_PATHS = ('Controller/Vessel', 'ElementalAbilityMaps', 'Controller/Arcade', 'Controller/AI', 'Controller/Projectiles',
              '_SO_Assets/Games', '_SO_Assets/VesselActions', '_SO_Assets/AI Training', '_SO_Assets/Scoring Rules',
              'Cell Configs', '_Prefabs/Spacevessels', 'Tools/Build/author_')
NOT_PLAY = re.compile(r'(Visual|HUD|Colou?r|Icon|Material|Theme|Tests/|\.md$)')
LOOK_AREA = re.compile(r'\b(colou?rs?|hud|icons?|look|art|portraits?|previews?|glow|vfx|shaders?|visuals?)\b', re.I)


def loop_target(e, vessels, cards=()):
    """Which vessels, elements and loop step an entry is about. What `add --vessel/--element/--loop-step` recorded wins;
    else it is read from the entry: the vessels named in its lane or area (or the single-hull arcade game it names), or
    whose name is in a play file it changed; the elements named with a capital there (or that game's elements); step 3
    when every play file is AI work, 2 when every one is an intensity / cell config, else 1: a change to the vessel or
    its game is a change to its spirit, and every later step is built on it. Nothing when its files are all outside
    play (LOOP_PATHS) or its area is about the look."""
    rec = e.get('loop') or {}
    text = ' '.join(str(e.get(k) or '') for k in ('lane', 'area'))
    paths = e.get('paths') or []
    play = [p for p in paths if any(k in p for k in LOOP_PATHS) and not NOT_PLAY.search(p)]
    if not rec.get('vessels') and ((paths and not play) or LOOK_AREA.search(text)):
        return [], [], ''
    found, elements = list(rec.get('vessels') or []), list(rec.get('elements') or [])
    if not found:
        found = [v for v in vessels if re.search(rf'\b{re.escape(v)}\b', text, re.I) or any(v in p for p in play)]
        for c in cards:
            if len(c['hulls']) == 1 and c['name'] and re.search(rf'\b{re.escape(c["name"])}\b', text, re.I):
                found += [h for h in c['hulls'] if h not in found]
                elements += [x for x in c['elements'] if x not in elements and not rec.get('elements')]
    if not rec.get('elements'):
        elements += [x for x in LOOP_ELEMENTS if x not in elements and
                     (re.search(rf'\b{x}\b', text) or any(re.search(rf'{x}(?![a-z])', p) for p in play))]
    step = str(rec.get('step') or '')
    if not step:
        ai = [p for p in play if '/AI' in p]
        ladder = [p for p in play if 'Cell Configs' in p]
        step = '3' if (play and len(ai) == len(play)) or (not play and re.search(r'\bAI\b', text)) else \
            '2' if play and len(ladder) == len(play) else '1'
    return found, elements, step


def loop_impact(e, el, board):
    """(score, [what a late catch would send back]) for one entry; (0, []) when it touches no vessel in the loop."""
    if not board:
        return 0, []
    vessels = [r['vessel'] for r in board['rows']]
    found, elements, step = loop_target(e, vessels, [c for c in board['cards'] if 'ArcadeGames' in c['lists']])
    score, hits = 0, []
    for v in found:
        sc, h = el.rework(board, v, elements or None, step)
        score += sc
        hits += [x for x in h if x not in hits]
    return score, hits


def print_entries(rows, header, show_to=False):
    print(header)
    if not rows:
        print('  nothing')
        return
    for e in rows:
        where = 'landed on bleeding-edge' if e['where'] == 'landed' else \
            'only in this working tree (not pushed)' if e['where'] == 'local' else f'not merged yet, on {e["where"]}'
        pr = f', PR #{e["pr"]}' if e.get('pr') else ''
        who = f'to {e["to"]}' if show_to else f'from {e["by"]}'
        imp = f'  LOOP IMPACT {e["loopImpact"]}' if e.get('loopImpact') else ''
        print(f'\n  [{e["status"].upper()}]{imp} {e["id"]}')
        print(f'    {who}, {e["date"][:10]}: {e["summary"]}')
        if e.get('loopRework'):
            more = f' and {len(e["loopRework"]) - 4} more' if len(e['loopRework']) > 4 else ''
            print(f'    answer early: if this goes against the vessel, the loop redoes {"; ".join(e["loopRework"][:4])}{more}')
        if e.get('area'):
            print(f'    their area: {e["area"]}' + (f'  (lane: {e["lane"]})' if e.get('lane') else ''))
        if e.get('why'):
            print(f'    why: {e["why"]}')
        print(f'    where: {where}' + (f' (branch {e["branch"]}{pr})' if e.get('branch') and e['where'] == 'landed' else pr))
        if e.get('paths'):
            more = f' and {len(e["paths"]) - 5} more' if len(e['paths']) > 5 else ''
            print(f'    files: {", ".join(e["paths"][:5])}{more}')
        for r in e.get('replies', []):
            print(f'    {r["date"][:10]} {r["by"]}: {r["status"]}' + (f' - {r["note"]}' if r.get('note') else ''))


def list_queue(entries, person=None, sender=None, show_all=False, as_json=False, loop=(None, None)):
    rows = [e for e in entries.values() if (not person or e['to'] == person) and (not sender or e['by'] == sender)]
    if not show_all:
        rows = [e for e in rows if e['status'] in OPEN_STATUSES]
    el, board = loop
    for e in rows:
        e['loopImpact'], e['loopRework'] = loop_impact(e, el, board) if e['status'] in OPEN_STATUSES else (0, [])
    # Open entries the loop is building on first, the most rework first; then talk-about before waiting; oldest first.
    rows.sort(key=lambda e: (e['status'] not in OPEN_STATUSES, -e['loopImpact'], e['status'] != 'discuss',
                             e['status'] != 'pending', e['date']))
    if as_json:
        print(json.dumps(rows, indent=1, ensure_ascii=False))
        return rows
    if person:
        head = f'{person}\'s approval queue ({len(rows)} {"entries" if show_all else "waiting"})'
        if board:
            head += (', LOOP IMPACT first: the elemental loop work a "this is wrong" answer would send back now. It grows'
                     ' while an entry waits, so answering these first is what keeps the loop moving (Garrett, 2026-11-12)')
        print_entries(rows, head + ':')
        if rows and not show_all:
            print('\nAnswer each: approve (you agree), discuss (talk to the person who made it; --note says about what),'
                  ' resolve (settled after talking):\n  python3 Tools/Build/approvals.py reply <id> approve --by '
                  f'{person} [--note "..."]')
    elif sender:
        print_entries(rows, f'What {sender}\'s changes queued for others ({len(rows)}):', show_to=True)
        if any(e['status'] == 'discuss' for e in rows):
            print('\nA DISCUSS answer is someone asking to talk: reach out to them about it.')
    else:
        counts = {}
        for e in rows:
            counts[e['to']] = counts.get(e['to'], 0) + 1
        print('Approval queues: ' + (', '.join(f'{k} {v}' for k, v in sorted(counts.items())) or 'all empty'))
    return rows


# --- Self-test ----------------------------------------------------------------------------------------------------

def self_test():
    bad = []

    def ok(cond, what):
        if not cond:
            bad.append(what)

    root = repo_root()
    wwh, sw = tools(root)
    team = wwh.Team.load(root)
    ok(person_of(team, 'garrett') == 'Garrett', 'a name finds its person')
    ok(person_of(team, 'YsKhan61') == 'Yash', 'a git name finds its person')
    ok(person_of(team, 'nobody-at-all') is None, 'a stranger is nobody')
    ok(people_in(team, 'Shombith (leads activation)') == ['Shombith'], 'an owner line names its person')
    ok(about(3, 40) and about(1, 4) and not about(1, 10), 'a quarter or three files is about a lane; fewer brushes it')
    ok(is_shared('Docs/UNITY_VERIFICATION_CHECKLIST.md', ['Docs/UNITY_VERIFICATION_CHECKLIST.md']), 'shared path')
    ok(is_shared('x.cs.meta', []) and not is_shared('Assets/x.cs', ['Assets/y']), 'meta files are nobody\'s')
    now = datetime.datetime(2026, 11, 10, 12, 0, 0, tzinfo=datetime.timezone.utc)
    eid = make_id('Shombith', 'Garrett', 'NO-GO only on a swim lane collision!', now)
    ok(ID_RE.match(eid) and eid.startswith('2026-11-10-no-go-only-on-a-swim-lane-collision-'), f'id shape: {eid}')
    thresher = sw.Lane('thresher', 'Thresher', 'vessel', ['Thresher'])
    hangar = sw.Lane('hangar', 'Hangar', 'shell', ['Hangar'])
    h = lane_hits(['Assets/Thresher/A.cs', 'Assets/Thresher/B.cs', 'Assets/UI/HangarView.cs', 'Docs/x.md'],
                  [thresher, hangar])
    ok(len(h.get('thresher', [])) == 2 and len(h.get('hangar', [])) == 1, 'files land in their lanes')

    # Loop impact: what an entry is about, and that the look and the tools send nothing back (negative controls).
    vs, cs = ['Manta', 'TigerMoth', 'Dolphin'], [{'name': 'Waystation', 'hulls': ['TigerMoth'], 'elements': ['Time']},
                                                {'name': 'Regatta', 'hulls': ['Manta', 'Dolphin'], 'elements': ['Time']}]
    ok(loop_target({'area': 'Redline AI nose follow rates', 'summary': 'x'}, vs, cs)[2] == '3', 'an AI area is step 3')
    ok(loop_target({'area': 'Waystation (GameModes 58)'}, vs, cs)[:2] == (['TigerMoth'], ['Time']),
       'a single-hull game names its vessel and element')
    ok(loop_target({'area': 'Regatta courses'}, vs, cs)[0] == [], 'an arena card names no one vessel')
    ok(loop_target({'area': 'Manta', 'paths': ['Assets/_Scripts/Controller/Vessel/MantaSting.cs']}, vs)[0] == ['Manta']
       and loop_target({'area': 'Manta', 'paths': ['Assets/_Scripts/Controller/Vessel/MantaSting.cs']}, vs)[2] == '1',
       'a vessel play file is a step 1 change to that vessel')
    ok(loop_target({'area': 'Manta', 'paths': ['Assets/_Scripts/UI/View/MantaHUDView.cs']}, vs)[0] == [],
       'a HUD file sends nothing back')
    ok(loop_target({'area': 'Manta colours', 'summary': 'Manta'}, vs)[0] == [], 'a look area sends nothing back')
    ok(loop_target({'area': 'x', 'loop': {'vessels': ['Dolphin'], 'step': '2'}}, vs)[::2] == (['Dolphin'], '2'),
       'what add --vessel / --loop-step recorded wins')

    # A queue round trip in a scratch repo: add -> pending, reply discuss -> open, resolve -> closed; refusals hold.
    with tempfile.TemporaryDirectory() as tmp:
        for args in (['init', '-q'], ['config', 'user.email', 't@example.com'], ['config', 'user.name', 'T'],
                     ['commit', '-q', '--allow-empty', '-m', 'root']):
            git(*args, cwd=tmp)
        eid, rel = add(tmp, team, 'Shombith', 'Garrett', 'the /ship skill', 'NO-GO only on a collision', 'faster',
                       ['.claude/skills/ship/SKILL.md'], 'cece/x', None, '', now=now)
        ok(rel.replace(os.sep, '/').startswith('Docs/Approvals/Shombith/'), 'an entry goes in its person\'s folder')
        q = read_queue(tmp, 30)
        ok(q.get(eid, {}).get('status') == 'pending' and q[eid]['where'] == 'local', 'a new entry is pending, local')
        for refused, call in (
                ('own work', lambda: add(tmp, team, 'Garrett', 'Garrett', '', 's', '', [], '', None, '')),
                ('stranger', lambda: add(tmp, team, 'Nobody', 'Garrett', '', 's', '', [], '', None, '')),
                ('no summary', lambda: add(tmp, team, 'Yash', 'Garrett', '', ' ', '', [], '', None, '')),
                ('wrong answerer', lambda: reply(tmp, team, eid, 'approve', 'Yash', '', q)),
                ('discuss w/o note', lambda: reply(tmp, team, eid, 'discuss', 'Shombith', '', q))):
            try:
                call()
                bad.append(f'refuses: {refused}')
            except SystemExit:
                pass
        reply(tmp, team, eid, 'discuss', 'Shombith', 'skills are mine; walk me through it', q, now=now)
        q = read_queue(tmp, 30)
        ok(q[eid]['status'] == 'discuss', 'a discuss answer keeps it open')
        reply(tmp, team, eid[:20], 'resolve', 'Shombith', 'talked; keep it', q,
              now=now + datetime.timedelta(minutes=5))
        q = read_queue(tmp, 30)
        ok(q[eid]['status'] == 'resolved' and len(q[eid]['replies']) == 2, 'the newest answer wins; a prefix finds the id')
        # Committed and read back from a branch ref, the entry and its answers are found once.
        git('add', '-A', cwd=tmp)
        git('commit', '-q', '-m', 'queue', cwd=tmp)
        git('update-ref', 'refs/remotes/origin/bleeding-edge', 'HEAD', cwd=tmp)
        q = read_queue(tmp, 30)
        ok(q[eid]['where'] == 'landed' and len(q[eid]['replies']) == 2, 'read from bleeding-edge: landed, answers once')
    for b in bad:
        print(f'FAIL: {b}')
    print('approvals self-test: ' + ('ok' if not bad else f'{len(bad)} failure(s)'))
    return bool(bad)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--self-test', action='store_true')
    sub = ap.add_subparsers(dest='cmd')
    c = sub.add_parser('check', help='does this change collide with an occupied swim lane; whose work does it touch')
    c.add_argument('--files', nargs='*', help='the changed files (default: this branch against bleeding-edge, plus the working tree)')
    c.add_argument('--me', help='the person driving this session (a TEAM.json name, git name or email)')
    c.add_argument('--days', type=int, help='occupancy window (default lanes.json window_days, 7)')
    c.add_argument('--landed-days', type=int, default=30, help='how far back landed work counts as someone\'s (30)')
    c.add_argument('--no-github', action='store_true', help='skip the pull requests (gh)')
    c.add_argument('--json', action='store_true')
    a_ = sub.add_parser('add', help='queue a change for the person whose work it touched')
    a_.add_argument('--to', required=True)
    a_.add_argument('--by', required=True, help='the person driving this session')
    a_.add_argument('--summary', required=True, help='what changed in their work, in a sentence')
    a_.add_argument('--area', default='', help='their area, in their words ("the Thresher HUD")')
    a_.add_argument('--why', default='')
    a_.add_argument('--paths', nargs='*', default=[])
    a_.add_argument('--branch', help='default: the current branch')
    a_.add_argument('--pr', type=int)
    a_.add_argument('--lane', default='', help='the swim lane key, when it is one')
    a_.add_argument('--vessel', nargs='*', default=[], help='the elemental loop vessel(s) it changes (else read from the entry)')
    a_.add_argument('--element', nargs='*', default=[], choices=list(LOOP_ELEMENTS), help='the loop element(s) it changes')
    a_.add_argument('--loop-step', choices=['1', '2', '3'], help='the loop step it changes: 1 the vessel / fun, 2 intensities, 3 AI')
    l_ = sub.add_parser('list', help='a person\'s queue, or what a person\'s changes queued')
    l_.add_argument('--person', help='whose queue (default: you)')
    l_.add_argument('--from', dest='sender', help='entries this person\'s changes filed for others')
    l_.add_argument('--all', action='store_true', help='answered ones too')
    l_.add_argument('--counts', action='store_true', help='one line: how many wait for each person')
    l_.add_argument('--days', type=int, default=60, help='read branches pushed in the last N days (60)')
    l_.add_argument('--json', action='store_true')
    r_ = sub.add_parser('reply', help='answer an entry in your queue')
    r_.add_argument('id')
    r_.add_argument('verb', choices=list(STATUSES))
    r_.add_argument('--by', required=True)
    r_.add_argument('--note', default='')
    r_.add_argument('--days', type=int, default=60)
    a = ap.parse_args()
    if a.self_test:
        return 1 if self_test() else 0
    root = repo_root()
    if a.cmd is None:
        ap.print_help()
        return 0
    wwh, _ = tools(root)
    team = wwh.Team.load(root)
    if a.cmd == 'check':
        me = me_of(team, root, a.me)
        r = check(root, a.files or changed_files(root, BASE), me, current_branch(root), a.days, a.no_github, a.landed_days)
        if a.json:
            print(json.dumps(r, indent=1, ensure_ascii=False))
        else:
            if not me:
                print('note: who you are is unknown (pass --me <name>): your own work may show as someone else\'s')
            print_check(r, me)
        return 2 if r['collisions'] else 0
    if a.cmd == 'add':
        eid, rel = add(root, team, a.to, a.by, a.area, a.summary, a.why, a.paths, a.branch or current_branch(root),
                       a.pr, a.lane, loop={'vessels': a.vessel, 'elements': a.element, 'step': a.loop_step})
        print(f'queued {eid} for {person_of(team, a.to)}: {rel}\nCommit it with the change, so it lands with it.')
        return 0
    if a.cmd == 'list':
        entries = read_queue(root, a.days)
        if a.counts:
            list_queue(entries)
            return 0
        person = None if a.sender else (person_of(team, a.person) if a.person else me_of(team, root, None))
        if a.person and not person:
            raise SystemExit(f'"{a.person}" is not a person in Docs/TEAM.json')
        if not person and not a.sender:
            raise SystemExit('whose queue? pass --person <name> (or --from <name>)')
        list_queue(entries, person=person, sender=person_of(team, a.sender) if a.sender else None,
                   show_all=a.all, as_json=a.json, loop=loop_board(root))
        return 0
    if a.cmd == 'reply':
        entries = read_queue(root, a.days)
        rel, rep = reply(root, team, a.id, a.verb, a.by, a.note, entries)
        print(f'{rep["status"]}: {rel}\nCommit it on your branch and push, so the queue and the person who made the change see it.')
        return 0
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except BrokenPipeError:
        sys.exit(0)
