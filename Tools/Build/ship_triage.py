#!/usr/bin/env python3
"""ship_triage: what THIS branch needs from /ship, in one read of git - so a ship reads the sections that apply and
skips the rest (Shombith, 2026-11-13: "/ship is very slow; it does not always need to go through all lines").

    python3 Tools/Build/ship_triage.py            # the plan, for a person and a session
    python3 Tools/Build/ship_triage.py --json     # for another tool
    python3 Tools/Build/ship_triage.py --self-test

It prints, from the diff against the merge base and a trial merge of bleeding-edge (`git merge-tree`, which touches
neither the work tree nor the index):
  - the size of the change and the depth it qualifies for (/ship-quick's refusal rules, measured);
  - the trial merge: clean, or the conflicted files and the MERGE_TRAPS that match them;
  - whether C# changed (the reference compile matters) and the command to start the gates in the background NOW, so
    they run while the review happens;
  - the editor tools the branch touched (§2.5 runs in every mode; this only says whether it has anything to classify);
  - the REVIEW_TRAPS whose subject matches the files (C#, tests, assets, docs, gates, a deletion, a rename);
  - the conditional steps that apply: LAB (§3.55), COMPUTE (§3.57), CODEX (§5.6);
  - the swim lane the change is about, for the hand-off at the end (§6.5).
A READER: it writes nothing.
"""
import json, os, re, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
BASE = 'origin/bleeding-edge'
REFS = '.claude/skills/ship/references'
LOCKED = re.compile(r'(Environment/|FloraAndFauna|Controller/Party/|Threading|Scoring|ScoringRule|PrismAnimation|Prisms/|'
                    r'ElementalAbilityMap|ElementalScaling|ElementalFloat)')
YAML = re.compile(r'\.(unity|prefab|asset|shadergraph|mat|controller)$')
LAB = re.compile(r'^(Docs/Studios/|\.claude/skills/labmaker/)|Lab[A-Z_].*\.(html|py)$|_lab\.(html|py)$')
COMPUTE = re.compile(r'^(Tools/Compute/|Docs/Compute/jobs/)|^Tools/Build/[^/]*(_sim|_balance|_budget)\.py$')
CODEX = re.compile(r'ElementalAbilityMaps/|Spacevessels/|SO_Class_|VesselClassType\.cs|ToyVesselRoster|R_VesselActions/|/Vessel/|'
                   r'AbilityIcons/|VesselHUD/|icon_kit/specs/|_SO_Assets/Games/|author_touch_profiles|fleet_codex|FleetCodex/')

# Which traps a kind of change should read: words in the trap's headline (references/*_TRAPS.md index lines).
REVIEW_TOPICS = {
    'cs': r'doc comment|member|FIELD|GUARDED|try/catch|primitive|ScriptableObject|COMPOSES|ABSENCE',
    'tests': r'test',
    'assets': r'ASSET|INITIALIZER|Registration|FIELD',
    'docs': r'doc|WORKED EXAMPLES|LONG branch|re-measure|ABSENCE',
    'gates': r'gate|generator|Registration|Sweep the whole family|INSTRUMENT|re-measure',
    'deleted': r'RETIREMENT|absence|Deleting|successor|dead surface|ABSENCE',
    'renamed': r'rename',
}
MERGE_TOPICS = {
    'always': r'SHALLOW|in flight|clean `git status`|SAME root cause',
    'cs': r'function BODIES|mock|shared-tail|SOURCE-CODE|chain|dangling|VALUE TYPE|bookkeepers|ENUM|collision actually arrives|IDENTICAL line|Pick one wholesale',
    'assets': r'YAML|prefab|\.meta|GUID|asset',
    'docs': r'doc|CONDITIONAL|file:line|re-scope|document\'s own past|PROSE',
    'gates': r'gate|generator|--check',
}


def git(*a, ok=(0,)):
    r = subprocess.run(['git'] + list(a), cwd=ROOT, capture_output=True, text=True, encoding='utf-8', errors='replace')
    return r.stdout if r.returncode in ok else ''


def trap_index(name):
    """[(number, headline)] from a references/*_TRAPS.md index."""
    try:
        text = open(os.path.join(ROOT, REFS, name), encoding='utf-8').read()
    except OSError:
        return []
    head = text.split('## The traps', 1)[0]
    return [(int(m.group(1)), m.group(2)) for m in re.finditer(r'^\s*(\d+)\. (.+)$', head, re.M)]


def pick(index, kinds, topics):
    rx = [re.compile(topics[k], re.I) for k in kinds if k in topics]
    return [n for n, h in index if any(r.search(h) for r in rx)]


def kinds_of(files, status):
    k = set()
    if any(f.endswith('.cs') for f in files):
        k.add('cs')
    if any('/Tests/' in f or f.endswith('Tests.cs') or 'self_test' in f for f in files):
        k.add('tests')
    if any(YAML.search(f) or f.endswith('.meta') for f in files):
        k.add('assets')
    if any(f.endswith('.md') for f in files):
        k.add('docs')
    if any(f.startswith(('Tools/Build/', 'Tools/CI/', '.github/')) for f in files):
        k.add('gates')
    if any(s.startswith('D') for s in status.values()):
        k.add('deleted')
    if any(s.startswith('R') for s in status.values()):
        k.add('renamed')
    return k


def triage():
    branch = git('rev-parse', '--abbrev-ref', 'HEAD').strip()
    if not git('rev-parse', '--verify', '-q', BASE).strip():
        return {'error': f'{BASE} is missing: git fetch origin bleeding-edge'}
    bases = git('merge-base', '--all', BASE, 'HEAD').split()
    mb = bases[0] if bases else ''
    status = {}
    for line in git('diff', '--name-status', '-M', f'{mb}..HEAD').splitlines():
        parts = line.split('\t')
        if len(parts) >= 2:
            status[parts[-1]] = parts[0]
    for line in git('status', '--porcelain').splitlines():
        status.setdefault(line[3:].split(' -> ')[-1], 'M' if line[:2].strip() != '??' else 'A')
    files = sorted(status)
    added = removed = 0
    for line in git('diff', '--numstat', f'{mb}..HEAD').splitlines():
        a, r, *_ = line.split('\t') + ['0', '0']
        added += int(a) if a.isdigit() else 0
        removed += int(r) if r.isdigit() else 0
    behind, ahead = (git('rev-list', '--left-right', '--count', f'{BASE}...HEAD').split() + ['0', '0'])[:2]
    # The trial merge: exit 1 with the conflicted files listed after the tree id.
    out = git('merge-tree', '--write-tree', '--name-only', '--no-messages', BASE, 'HEAD', ok=(0, 1))
    lines = [l for l in out.splitlines()[1:] if l.strip()]
    conflicts = sorted(set(lines))
    kinds = kinds_of(files, status)
    tools = []
    for f in files:
        if f.endswith('.cs') and os.path.isfile(os.path.join(ROOT, f)):
            try:
                if 'MenuItem("FrogletTools/' in open(os.path.join(ROOT, f), encoding='utf-8', errors='replace').read():
                    tools.append(f)
            except OSError:
                pass
    locked = [f for f in files if LOCKED.search(f)]
    yaml_hand = [f for f in files if YAML.search(f)]
    why_not_quick = []
    if len(files) > 10:
        why_not_quick.append(f'{len(files)} files (> 10)')
    if added + removed > 400:
        why_not_quick.append(f'{added + removed} changed lines (> 400)')
    if locked:
        why_not_quick.append(f'touches a LOCKED system ({locked[0]}{" and more" if len(locked) > 1 else ""})')
    if yaml_hand:
        why_not_quick.append(f'changes Unity YAML ({len(yaml_hand)} file(s))')
    deep = len(files) > 60 or (added + removed) > 3000 or len(locked) > 5
    mode = 'deep' if deep else 'full' if why_not_quick else 'quick'
    rk = pick(trap_index('REVIEW_TRAPS.md'), kinds, REVIEW_TOPICS)
    mk = pick(trap_index('MERGE_TRAPS.md'), kinds_of(conflicts, {}) | {'always'}, MERGE_TOPICS) if conflicts else []
    lane = None
    try:
        sys.path.insert(0, os.path.join(ROOT, 'Tools', 'Build'))
        import handoff
        _, lanes = handoff.lanes_of(ROOT)
        ln, _ = handoff.branch_lane(ROOT, lanes)
        lane = {'key': ln.key, 'name': ln.name, 'kind': ln.kind} if ln else None
    except Exception:
        pass
    return {
        'branch': branch, 'mergeBases': len(bases), 'behind': int(behind), 'ahead': int(ahead), 'files': len(files),
        'added': added, 'removed': removed, 'kinds': sorted(kinds), 'mode': mode, 'whyNotQuick': why_not_quick,
        'conflicts': conflicts, 'mergeTraps': mk, 'csharp': 'cs' in kinds, 'editorTools': tools, 'reviewTraps': rk,
        'lab': [f for f in files if LAB.search(f)], 'compute': [f for f in files if COMPUTE.search(f)],
        'codex': [f for f in files if CODEX.search(f)], 'lane': lane}


def show(t):
    if 'error' in t:
        print(t['error'])
        return
    p = print
    p(f'SHIP TRIAGE {t["branch"]}: {t["files"]} files, +{t["added"]}/-{t["removed"]}, {t["ahead"]} ahead and {t["behind"]} behind bleeding-edge')
    if t['mergeBases'] > 1:
        p(f'  STOP: {t["mergeBases"]} merge bases - the clone is shallow. `git fetch --unshallow`, then run this again (MERGE_TRAPS T1).')
    p(f'  depth: /ship{"-" + t["mode"] if t["mode"] != "full" else ""}' + (f' (not quick: {"; ".join(t["whyNotQuick"])})' if t['whyNotQuick'] else ' (small, no locked system, no YAML)'))
    if t['conflicts']:
        p(f'  merge: {len(t["conflicts"])} conflict(s) - ' + ', '.join(t['conflicts'][:8]) + (' ...' if len(t['conflicts']) > 8 else ''))
        p(f'         read references/MERGE_TRAPS.md: T' + ', T'.join(map(str, t['mergeTraps'])))
    else:
        p('  merge: clean trial merge (still read MERGE_TRAPS T29 and T21 if bleeding-edge changed files you changed)')
    p('  gates: start them NOW in the background while you review: `bash .claude/skills/ship/gates.sh` (parallel)'
      + ('; C# changed, so the reference compile matters' if t['csharp'] else '; no C#, the reference compile only re-confirms the base'))
    p('  §2.5:  ' + (f'{len(t["editorTools"])} editor tool file(s) to classify: ' + ', '.join(t['editorTools'][:5]) if t['editorTools']
                    else 'no FrogletTools [MenuItem] in the diff; still sweep for runtime writers and the ledger (§2.5 step 1)'))
    p('  §2:    the four gates, then references/REVIEW_TRAPS.md: ' + ('T' + ', T'.join(map(str, t['reviewTraps'])) if t['reviewTraps'] else 'none match')
      + (' (/ship-deep: all of them)' if t['mode'] == 'deep' else ''))
    for key, sec, ref in (('lab', '§3.55 LAB', 'LAB_CAPTURE.md'), ('compute', '§3.57 COMPUTE', 'COMPUTE_CAPTURE.md'),
                          ('codex', '§5.6 CODEX', 'FLEET_CODEX.md')):
        p(f'  {sec}: ' + (f'yes ({len(t[key])} file(s), e.g. {t[key][0]}): read references/{ref}' if t[key] else 'no - one report line'))
    p('  lane:  ' + (f'{t["lane"]["name"]} ({t["lane"]["key"]}) - §6.5 asks `handoff.py suggest` at the end' if t['lane'] else 'none (the change is in no swim lane)'))


def self_test():
    bad = []
    if kinds_of(['Assets/a.cs', 'Docs/x.md'], {'Assets/a.cs': 'M', 'Docs/x.md': 'D'}) != {'cs', 'docs', 'deleted'}:
        bad.append('kinds_of misreads a C# + deleted doc change')
    idx = [(1, 'Inserting a member ABOVE a method'), (2, 'A rename sweep must split'), (3, 'A test that SUPPLIES')]
    if pick(idx, {'cs'}, REVIEW_TOPICS) != [1] or pick(idx, {'renamed', 'tests'}, REVIEW_TOPICS) != [2, 3]:
        bad.append('trap picking is wrong')
    if pick(idx, set(), REVIEW_TOPICS):
        bad.append('a change of no kind picked traps')                                         # negative control
    for name in ('REVIEW_TRAPS.md', 'MERGE_TRAPS.md'):
        if len(trap_index(name)) < 10:
            bad.append(f'{name} index is missing or short')
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


if __name__ == '__main__':
    if '--self-test' in sys.argv:
        sys.exit(1 if self_test() else 0)
    t = triage()
    print(json.dumps(t, indent=1)) if '--json' in sys.argv else show(t)
