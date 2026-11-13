#!/usr/bin/env python3
"""The land queue: a person's open pull requests on froglet-studio/Cosmic-Shore, classified, ordered for landing
into bleeding-edge, refreshed and gated one at a time (/land-queue SKILL.md).

READ-ONLY BY DEFAULT. `list`, `trunk`, `preflight` and `gates` only read (GitHub through `gh api`, the repo through
git). Two subcommands write, and only what they say:
  refresh   merges bleeding-edge into ONE of the person's own PR branches in a scratch worktree (never your
            checkout), runs the fast gates there, and pushes the merge only with --push (never a force-push).
  record    appends a dated entry to Docs/Reports/LAND_QUEUE.md.
Nothing here merges, closes or comments on a pull request: the session does those through the GitHub tools, one at
a time, after the person approved the batch (SKILL.md 5 and 6).

    python3 .claude/skills/land-queue/land_queue.py                      # list + plan for the gh user's open PRs
    python3 .claude/skills/land-queue/land_queue.py list --user Shombith03 [--pr 1749,1755] [--json] [--save F]
    python3 .claude/skills/land-queue/land_queue.py list --from-json F   # re-plan saved records, no network
    python3 .claude/skills/land-queue/land_queue.py trunk                # is bleeding-edge green? exit 0 / 1 / 3
    python3 .claude/skills/land-queue/land_queue.py gates --pr 1749      # which fast gates its diff needs
    python3 .claude/skills/land-queue/land_queue.py files 1749           # its files (for approvals.py check)
    python3 .claude/skills/land-queue/land_queue.py refresh 1636 [--push] [--trailer "Co-Authored-By: ..."]
    python3 .claude/skills/land-queue/land_queue.py preflight 1636 --gated-sha <sha>   # right before a merge
    python3 .claude/skills/land-queue/land_queue.py record --before 42 --after 39 --merged 1636 --closed 89 ...
    python3 .claude/skills/land-queue/land_queue.py --self-test

Classes (first match wins, in this order, so a red PR can never read READY):
  SCRATCH        its title, branch or description says negative control / do not merge / scratch
  SUPERSEDED     nothing ahead of bleeding-edge, every file already reads on bleeding-edge as the PR has it,
                 or a PR says it replaces this one
  DRAFT          GitHub's draft flag, or its title / description says draft or WIP
  STALE          no commit for 14+ days, or a PR merged since its last commit changed most of its files
  RED            a failing check on its head
  PENDING        a check on its head is still running
  NEEDS-REFRESH  behind bleeding-edge, or conflicting with it
  READY          green (or no checks at all: claude/** branches fire no CI on their own, /ship 5.5), no
                 conflicts, not a draft
`refresh` can turn a NEEDS-REFRESH into BLOCKED (conflicts no side clearly wins, or a red gate).

Exit codes: 0 ok; 1 a gate / trunk / preflight said no; 2 usage or setup; 3 unknown (GitHub did not answer);
4 refresh BLOCKED on conflicts.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = 'froglet-studio/Cosmic-Shore'
BASE = 'bleeding-edge'
STALE_DAYS = 14
CI_SHARE = 0.3             # a PR whose files are at least this share pipeline files is a pipeline PR
OVERLAP_SHARE = 0.5          # a merged PR that changed this share of a PR's files (and at least 2) overlaps it
REPORT_REL = 'Docs/Reports/LAND_QUEUE.md'
READINESS_REL = 'Docs/LAUNCH/READINESS.md'

# Files nearly every branch edits: they say nothing about two PRs doing the same work.
HOT_FILES = (
    'CLAUDE.md', 'Docs/UNITY_VERIFICATION_CHECKLIST.md', 'Docs/TEAM_TASKS.md', 'Docs/TEAM.md', 'Docs/TEAM.json',
    'Docs/claude/DOCUMENTATION_INDEX.md', 'Packages/manifest.json', 'Packages/packages-lock.json',
    '.claude/settings.json', 'Docs/LAUNCH/READINESS.md',
)
HOT_PREFIXES = ('Docs/Reports/', 'Docs/Approvals/', 'ProjectSettings/', 'Docs/perf/')

RED_CONCLUSIONS = {'failure', 'timed_out', 'startup_failure', 'action_required'}
OK_CONCLUSIONS = {'success', 'neutral', 'skipped'}

SCRATCH_TITLE = re.compile(r'(?i)(negative[ -]control|do[ -]?not[ -]merge|don\'t merge|\bdnm\b|\[scratch\]|^scratch\b|'
                           r'throw-?away)')
SCRATCH_BRANCH = re.compile(r'(?i)(^scratch/|^ci-scratch/|negative|[-/]scratch\b|^scratch-)')
SCRATCH_BODY = re.compile(
    r'(?im)(\bdo not merge\b(?!\W+(without|until|before|unless|while|if)\b)|'
    r'\bdon\'t merge\b(?!\W+(without|until|before|unless|while|if)\b)|\bnot (meant|intended) to (be )?merged?\b|'
    r'\bthis (pr|pull request|branch) (is|exists as|is only) (a |an |the )?(negative[ -]control|scratch|throw-?away)|'
    r'^\s*#+\s*negative control\s*$)')
SCRATCH_LABELS = {'do not merge', 'do-not-merge', 'dnm', 'scratch', 'negative control', 'negative-control'}
DRAFT_TITLE = re.compile(r'(?i)^\s*(\[(wip|draft)\]|(wip|draft)\s*[:\-])')
DRAFT_BODY = re.compile(r'(?im)(\bthis (pr|pull request) is (still )?a draft\b|^\s*\**(status:)?\s*draft\b\**\s*$|'
                        r'\bwork in progress\b.{0,20}\bnot ready\b)')
HOLD_BODY = re.compile(r'(?i)\b(do not|don\'t) merge\W+(without|until|before|unless)\b[^.\n]{0,80}')
DRAFT_LABELS = {'wip', 'draft', 'work in progress'}
REPLACED_SELF = re.compile(r'(?i)\b(superseded|replaced|obsoleted|redone)\s+(by|in)\s+#(\d+)')
REPLACES_OTHER = re.compile(r'(?i)\b(supersedes|replaces|obsoletes|replacement for|redoes)\s+#(\d+)')

CI_PATHS = re.compile(r'^(\.github/|Tools/CI/|Tools/Build/ship_gates\.py$|\.claude/skills/ship/gates\.sh$|'
                      r'Tools/Build/unity_refcompile/|Tools/Build/push_checks\.py$)')
CI_TITLE = re.compile(r'^(ci|build)(\(|:)')
SCENE_PREFAB = re.compile(r'\.(unity|prefab)$')
UGS_PATHS = re.compile(r'(?i)(cloudcode|cloud ?save|(^|[/_ .-])ugs([/_ .-]|$)|/ugs|remoteconfig|'
                       r'\.(ccmr|ec|lb|ac|rc)$|deployment/|accesscontrol|moderation)')
DOC_ONLY = re.compile(r'(?i)(\.(md|txt|rst)$|^Docs/.*\.(png|jpg|jpeg|gif|svg|webp|json|html|csv)$)')
LAUNCH_TEXT = re.compile(r'(?i)\blaunch[ -]blocker|\(blocker \d+\)|\bblocker \d+\b')

TIERS = ['CI / pipeline', 'launch blocker', 'scene / shared prefab / UGS', 'feature', 'docs only']
LANDABLE = ('READY', 'NEEDS-REFRESH')
CLOSABLE = ('STALE', 'SUPERSEDED', 'SCRATCH')


# --------------------------------------------------------------------------------------------- plumbing

def repo_root():
    p = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=HERE, capture_output=True, text=True)
    if p.returncode != 0:
        sys.exit('land_queue: not inside a git checkout')
    return p.stdout.strip()


def git(root, *args, check=False):
    p = subprocess.run(['git', *args], cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace')
    if check and p.returncode != 0:
        raise RuntimeError(f'git {" ".join(args)}: {p.stderr.strip() or p.stdout.strip()}')
    return p


class GhError(RuntimeError):
    pass


def _gh_once(path, retries=3):
    cmd = ['gh', 'api', '-H', 'Accept: application/vnd.github+json', path]
    last = ''
    for attempt in range(retries):
        p = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace')
        if p.returncode == 0:
            return json.loads(p.stdout or 'null')
        last = (p.stderr or p.stdout).strip()
        if '404' in last or '422' in last or '403' in last:
            break
        time.sleep(2 ** attempt)
    raise GhError(f'gh api {path}: {last[:300]}')


def gh(path, paginate=False, max_pages=40):
    """`gh api <path>` as JSON. With paginate=True a list endpoint comes back as one flat list.

    Pages are walked by `&page=N` on the repos/{owner}/{repo} path, never by GitHub's `Link` header: that header
    points at repositories/{numeric id}/..., which the session proxy refuses (403), so `gh api --paginate` fails on
    the second page of any PR with more than 100 files."""
    if not paginate:
        return _gh_once(path)
    sep = '&' if '?' in path else '?'
    per = int((re.search(r'per_page=(\d+)', path) or [None, 30])[1])
    flat = []
    for page in range(1, max_pages + 1):
        data = _gh_once(f'{path}{sep}page={page}')
        if isinstance(data, dict):   # check-runs: {"total_count", "check_runs": [...]}
            data = next((data[k] for k in ('check_runs', 'statuses', 'workflow_runs', 'jobs') if k in data), [])
        flat.extend(data or [])
        if len(data or []) < per:
            break
    return flat


def now_utc():
    return dt.datetime.now(dt.timezone.utc)


def parse_time(s):
    return dt.datetime.fromisoformat(s.replace('Z', '+00:00')) if s else None


def days_since(s, now):
    t = parse_time(s)
    return (now - t).total_seconds() / 86400 if t else None


def gh_login():
    try:
        return gh('user').get('login')
    except GhError:
        return None


def load_team(root):
    sys.path.insert(0, os.path.join(root, 'Tools', 'Build'))
    try:
        from who_works_where import Team  # type: ignore
        return Team.load(root)
    except Exception:  # noqa: BLE001 - a person's name is a nicety; the queue works on logins
        return None


def person_of(team, login):
    if team and login:
        return team.by_name.get(login.lower(), login)
    return login


# --------------------------------------------------------------------------------------------- collection

def latest_checks(check_runs, statuses):
    """Collapse re-runs: the newest run per (check suite, name), the newest status per context."""
    runs = {}
    for r in check_runs:
        key = ((r.get('check_suite') or {}).get('id'), r.get('name'))
        if key not in runs or (r.get('id') or 0) > (runs[key].get('id') or 0):
            runs[key] = r
    sts = {}
    for s in statuses:
        key = s.get('context')
        if key not in sts or (s.get('id') or 0) > (sts[key].get('id') or 0):
            sts[key] = s
    out = []
    for r in runs.values():
        out.append({'name': r.get('name'), 'status': r.get('status'), 'conclusion': r.get('conclusion')})
    for s in sts.values():
        st = s.get('state')
        out.append({'name': s.get('context'), 'status': 'completed' if st != 'pending' else 'in_progress',
                    'conclusion': {'success': 'success', 'failure': 'failure', 'error': 'failure'}.get(st)})
    return out


def check_state(checks):
    """('green' | 'red' | 'pending' | 'none', the failing or pending names)."""
    if not checks:
        return 'none', []
    red = [c['name'] for c in checks if c.get('status') == 'completed' and c.get('conclusion') in RED_CONCLUSIONS]
    if red:
        return 'red', sorted(set(red))
    pending = [c['name'] for c in checks if c.get('status') != 'completed']
    if pending:
        return 'pending', sorted(set(pending))
    return 'green', []


def fetch_record(num, base=BASE):
    """Everything the queue needs about one PR, from GitHub. Raises GhError."""
    pr = gh(f'repos/{REPO}/pulls/{num}')
    for _ in range(3):   # GitHub computes `mergeable` lazily: the first read is often null
        if pr.get('mergeable') is not None:
            break
        time.sleep(2)
        pr = gh(f'repos/{REPO}/pulls/{num}')
    sha = pr['head']['sha']
    files = gh(f'repos/{REPO}/pulls/{num}/files?per_page=100', paginate=True)
    try:
        cmp_ = gh(f'repos/{REPO}/compare/{base}...{sha}?per_page=1')
        ahead, behind, cstatus = cmp_.get('ahead_by'), cmp_.get('behind_by'), cmp_.get('status')
    except GhError:
        ahead = behind = cstatus = None
    runs = gh(f'repos/{REPO}/commits/{sha}/check-runs?per_page=100', paginate=True)
    status = gh(f'repos/{REPO}/commits/{sha}/status')
    commit = gh(f'repos/{REPO}/commits/{sha}')
    comments = gh(f'repos/{REPO}/issues/{num}/comments?per_page=100', paginate=True)
    return {
        'number': num, 'title': pr.get('title') or '', 'author': (pr.get('user') or {}).get('login'),
        'head': pr['head']['ref'], 'head_repo': ((pr['head'].get('repo') or {}).get('full_name')), 'sha': sha,
        'draft': bool(pr.get('draft')), 'labels': [l['name'] for l in pr.get('labels') or []],
        'created_at': pr.get('created_at'), 'updated_at': pr.get('updated_at'),
        'last_commit_at': ((commit.get('commit') or {}).get('committer') or {}).get('date'),
        'body': pr.get('body') or '', 'comments': [c.get('body') or '' for c in comments],
        'additions': pr.get('additions'), 'deletions': pr.get('deletions'), 'changed_files': pr.get('changed_files'),
        'mergeable': pr.get('mergeable'), 'mergeable_state': pr.get('mergeable_state'),
        'ahead_by': ahead, 'behind_by': behind, 'compare_status': cstatus,
        'files': [{'path': f['filename'], 'status': f.get('status'), 'blob': f.get('sha'),
                   'previous': f.get('previous_filename')} for f in files],
        'checks': latest_checks(runs, status.get('statuses') or []),
    }


def open_prs(user=None):
    prs = gh(f'repos/{REPO}/pulls?state=open&per_page=100', paginate=True)
    return [p for p in prs if user is None or (p.get('user') or {}).get('login', '').lower() == user.lower()]


def base_blobs(root, base_ref):
    """{path: blob sha} of the base branch's tree, from the local clone."""
    p = git(root, 'ls-tree', '-r', '--full-tree', base_ref)
    out = {}
    for line in p.stdout.splitlines():
        meta, _, path = line.partition('\t')
        parts = meta.split()
        if len(parts) == 3:
            out[path] = parts[2]
    return out


def merged_history(root, base_ref, since):
    """[(pr number, merged at iso, branch, title, {files})] for every PR merged into the base since `since`."""
    fmt = '@@%H%x09%cI%x09%s'
    p = git(root, 'log', '--first-parent', '-m', '--name-only', f'--format={fmt}', f'--since={since}', base_ref)
    out, cur = [], None
    for line in p.stdout.splitlines():
        if line.startswith('@@'):
            _, when, subj = line[2:].split('\t', 2)
            m = re.match(r'^Merge pull request #(\d+) from [^/\s]+/(\S+)', subj)
            cur = None
            if m:
                cur = {'number': int(m.group(1)), 'at': when, 'branch': m.group(2), 'files': set()}
                out.append(cur)
        elif line.strip() and cur is not None:
            cur['files'].add(line.strip())
    return out


def launch_prs(root):
    """PR numbers READINESS.md's ranked blocker list (10) and where-each-blocker-stands table (12) name."""
    path = os.path.join(root, READINESS_REL)
    if not os.path.isfile(path):
        return set()
    text = open(path, encoding='utf-8').read()
    start = text.find('## 10.')
    end = text.find('\n## 13.') if '\n## 13.' in text else len(text)
    chunk = text[start:end] if start >= 0 else ''
    nums = {int(n) for n in re.findall(r'/pull/(\d+)', chunk)} | {int(n) for n in re.findall(r'#(\d{3,5})\b', chunk)}
    return nums


def is_shallow(root):
    return git(root, 'rev-parse', '--is-shallow-repository').stdout.strip() == 'true'


def collect(root, user, numbers=None, fetch=True, workers=8, progress=True):
    if fetch:
        git(root, 'fetch', '-q', 'origin', BASE)
    base_ref = f'origin/{BASE}'
    listing = open_prs(None)
    everyone = [{'number': p['number'], 'body': p.get('body') or '', 'title': p.get('title') or '',
                 'author': (p.get('user') or {}).get('login')} for p in listing]
    mine = [p for p in listing if (p.get('user') or {}).get('login', '').lower() == user.lower()]
    if numbers:
        mine = [p for p in mine if p['number'] in numbers]
    records, errors = [], []
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(fetch_record, p['number']): p['number'] for p in mine}
        for f in concurrent.futures.as_completed(futs):
            try:
                records.append(f.result())
            except GhError as e:
                errors.append((futs[f], str(e)))
            if progress:
                print(f'  read {len(records) + len(errors)}/{len(mine)}', file=sys.stderr, end='\r')
    if progress:
        print(file=sys.stderr)
    oldest = min((r['last_commit_at'] or r['created_at'] for r in records), default=None)
    history = merged_history(root, base_ref, oldest or '2 weeks ago') if records else []
    ctx = {
        'user': user, 'base': BASE, 'base_sha': git(root, 'rev-parse', base_ref).stdout.strip(),
        'read_at': now_utc().isoformat(timespec='seconds'), 'shallow': is_shallow(root),
        'history_from': (git(root, 'log', '--format=%cI', base_ref).stdout.strip().splitlines() or [''])[-1],
        'blobs': base_blobs(root, base_ref), 'merged': history, 'open_prs': everyone,
        'launch_prs': sorted(launch_prs(root)), 'errors': errors,
    }
    return sorted(records, key=lambda r: r['number']), ctx


# --------------------------------------------------------------------------------------------- classification

def is_hot(path):
    return path in HOT_FILES or path.startswith(HOT_PREFIXES)


def superseded_reason(r, ctx):
    if r.get('ahead_by') == 0:
        return 'nothing ahead of bleeding-edge: every commit is already there'
    blobs = ctx.get('blobs') or {}
    files = r.get('files') or []
    if files and blobs:
        same = 0
        for f in files:
            if f['status'] == 'removed':
                same += f['path'] not in blobs
            else:
                same += blobs.get(f['path']) == f['blob'] and (f['status'] != 'renamed' or f.get('previous') not in blobs)
        if same == len(files):
            return f'all {len(files)} files already read on bleeding-edge exactly as this PR has them'
    for text in [r.get('body') or ''] + list(r.get('comments') or []):
        m = REPLACED_SELF.search(text)
        if m and int(m.group(3)) != r['number']:
            return f'its own thread says it was {m.group(1).lower()} by #{m.group(3)}'
    for other in ctx.get('open_prs') or []:
        if other['number'] == r['number']:
            continue
        for m in REPLACES_OTHER.finditer((other.get('body') or '') + '\n' + (other.get('title') or '')):
            if int(m.group(2)) == r['number']:
                return f'#{other["number"]} says it {m.group(1).lower()} this one'
    return None


def files_already_there(r, ctx):
    blobs = ctx.get('blobs') or {}
    return sum(1 for f in r.get('files') or [] if f['status'] != 'removed' and blobs.get(f['path']) == f['blob'])


def overlap_reason(r, ctx):
    since = parse_time(r.get('last_commit_at') or r.get('created_at'))
    own = {f['path'] for f in r.get('files') or [] if not is_hot(f['path'])}
    if len(own) < 2 or since is None:
        return None
    best = None
    for m in ctx.get('merged') or []:
        if m['number'] == r['number'] or m.get('branch') == r.get('head'):
            continue
        at = parse_time(m['at'])
        if at is None or at <= since:
            continue
        shared = own & set(m['files'])
        if len(shared) >= 2 and len(shared) >= OVERLAP_SHARE * len(own):
            if best is None or len(shared) > best[1]:
                best = (m['number'], len(shared), m['at'][:10])
    if best:
        there = files_already_there(r, ctx)
        blobs = ctx.get('blobs') or {}
        new_own = [f['path'] for f in r.get('files') or []
                   if f['status'] in ('added', 'renamed') and f['path'] not in blobs and not is_hot(f['path'])]
        text = (f'#{best[0]} (merged {best[2]}, after its last commit) changed {best[1]} of its {len(own)} files; '
                f'{there} of its {len(r.get("files") or [])} files already read on bleeding-edge as it has them')
        # Still adding files the trunk does not have is work of its own (a PR stacked on one that landed), so the
        # overlap is a note for the refresh, not a reason to close it.
        if new_own:
            return text + f'; it still adds {len(new_own)} file(s) bleeding-edge lacks ({new_own[0]})', False
        return text, True
    return None


def scratch_reason(r):
    if SCRATCH_TITLE.search(r.get('title') or ''):
        return 'its title says it is a negative control / scratch / do-not-merge'
    if SCRATCH_BRANCH.search(r.get('head') or ''):
        return f'its branch `{r["head"]}` is a scratch / negative-control branch'
    if {l.lower() for l in r.get('labels') or []} & SCRATCH_LABELS:
        return 'it is labelled do-not-merge / scratch'
    m = SCRATCH_BODY.search(r.get('body') or '')
    if m:
        return f'its description says "{m.group(0).strip()[:60]}"'
    return None


def draft_reason(r):
    if r.get('draft'):
        return 'GitHub draft'
    if DRAFT_TITLE.search(r.get('title') or ''):
        return 'its title marks it a draft / WIP'
    if {l.lower() for l in r.get('labels') or []} & DRAFT_LABELS:
        return 'it is labelled draft / WIP'
    m = DRAFT_BODY.search(r.get('body') or '')
    if m:
        return f'its description says "{m.group(0).strip()[:60]}"'
    return None


def classify(r, ctx, now=None):
    """(class, reason). The order IS the rule: the first that applies wins, so RED is decided before READY."""
    now = now or now_utc()
    why = scratch_reason(r)
    if why:
        return 'SCRATCH', why
    why = superseded_reason(r, ctx)
    if why:
        return 'SUPERSEDED', why
    why = draft_reason(r)
    if why:
        return 'DRAFT', why
    age = days_since(r.get('last_commit_at') or r.get('created_at'), now)
    if age is not None and age >= STALE_DAYS:
        return 'STALE', f'no commit for {int(age)} days'
    over = overlap_reason(r, ctx)
    if over and over[1]:
        return 'STALE', 'overlaps a merged PR: ' + over[0]
    note = ('; overlaps ' + over[0]) if over else ''
    ask = HOLD_BODY.search(r.get('body') or '')
    if ask:
        note += f'; its description asks: "{ask.group(0).strip()[:70]}"'
    cls, why = _classify_rest(r)
    return cls, why + note


def _classify_rest(r):
    state, names = check_state(r.get('checks') or [])
    if state == 'red':
        return 'RED', 'failing on its head: ' + ', '.join(names[:4])
    if state == 'pending':
        return 'PENDING', 'still running on its head: ' + ', '.join(names[:4])
    if r.get('mergeable') is False or r.get('mergeable_state') == 'dirty':
        return 'NEEDS-REFRESH', f'conflicts with bleeding-edge ({r.get("behind_by")} commits behind)'
    if (r.get('behind_by') or 0) > 0:
        return 'NEEDS-REFRESH', f'{r["behind_by"]} commits behind bleeding-edge'
    if r.get('behind_by') is None and r.get('mergeable') is None:
        return 'PENDING', 'GitHub has not computed whether it merges yet; re-read it'
    return 'READY', 'green, no conflicts' if state == 'green' else 'no conflicts; no CI ran on its head (the land gates are its check)'


def tier(r, ctx):
    paths = [f['path'] for f in r.get('files') or []]
    title = r.get('title') or ''
    ci = sum(1 for p in paths if CI_PATHS.match(p))
    # A pipeline PR is one whose title says so, or whose files are mostly pipeline: a feature that adds one workflow
    # line is still the feature.
    if CI_TITLE.match(title) or (paths and ci >= CI_SHARE * len(paths)):
        return 0
    if r['number'] in set(ctx.get('launch_prs') or []) or LAUNCH_TEXT.search(title) or \
            LAUNCH_TEXT.search((r.get('body') or '')[:600]):
        return 1
    if any(SCENE_PREFAB.search(p) or UGS_PATHS.search(p) for p in paths):
        return 2
    if paths and all(DOC_ONLY.search(p) for p in paths):
        return 4
    return 3


def scenes(r):
    return {f['path'] for f in r.get('files') or [] if SCENE_PREFAB.search(f['path'])}


def plan(records, ctx, invoker, now=None, asked=()):
    """Every PR with its class, tier, action and reason, landable ones first in landing order."""
    now = now or now_utc()
    rows = []
    for r in records:
        cls, why = classify(r, ctx, now)
        own = (r.get('author') or '').lower() == (invoker or '').lower() or r['number'] in asked
        rows.append({'pr': r['number'], 'class': cls, 'reason': why, 'tier': tier(r, ctx), 'own': own, 'rec': r})
    land = sorted([x for x in rows if x['class'] in LANDABLE],
                  key=lambda x: (x['tier'], x['rec'].get('created_at') or '', x['pr']))
    # Never two PRs on the same scene / prefab back to back: pull a non-sharing PR from the same tier between them;
    # if there is none, the second refreshes after the first lands.
    i = 1
    while i < len(land):
        shared = scenes(land[i - 1]['rec']) & scenes(land[i]['rec'])
        if shared:
            j = next((k for k in range(i + 1, len(land)) if land[k]['tier'] == land[i]['tier'] and
                      not (scenes(land[k]['rec']) & scenes(land[i - 1]['rec']))), None)
            if j is not None:
                land.insert(i, land.pop(j))
            else:
                land[i]['after_refresh'] = sorted(shared)
        i += 1
    for k, x in enumerate(land, 1):
        x['order'] = k
        if not x['own']:
            x['action'] = 'report only: not yours (land it only if its author asked you to)'
        elif x['tier'] == 2 or x.get('after_refresh'):
            x['action'] = 'refresh after the previous merge, gate, merge (alone)'
        elif x['class'] == 'NEEDS-REFRESH':
            x['action'] = 'refresh (merge bleeding-edge in), gate, push, merge'
        else:
            x['action'] = 'gate, merge'
        if x.get('after_refresh'):
            x['reason'] += '; shares ' + ', '.join(x['after_refresh'][:2]) + ' with the PR before it'
    rest = [x for x in rows if x['class'] not in LANDABLE]
    for x in rest:
        x['order'] = None
        if x['class'] in CLOSABLE:
            x['action'] = 'propose close (branch kept)' if x['own'] else 'report only: not yours'
        elif x['class'] == 'RED':
            x['action'] = 'leave open: fix the red check on its branch'
        elif x['class'] == 'PENDING':
            x['action'] = 'leave open: re-run land_queue when the checks finish'
        elif x['class'] == 'DRAFT':
            x['action'] = 'leave open: its author marks it ready'
        else:
            x['action'] = 'leave open'
    order = {'SCRATCH': 0, 'SUPERSEDED': 1, 'STALE': 2, 'RED': 3, 'PENDING': 4, 'DRAFT': 5}
    rest.sort(key=lambda x: (order.get(x['class'], 9), x['pr']))
    return land + rest


def close_comment(row):
    return (f'Closing from /land-queue: {row["class"].lower()}, {row["reason"]}. The branch `{row["rec"]["head"]}` '
            f'is kept; delete it yourself if nothing on it is needed.')


# --------------------------------------------------------------------------------------------- rendering

def esc(t):
    return str(t).replace('|', '\\|').replace('\n', ' ')


def size_of(r):
    return f'+{r.get("additions") or 0}/-{r.get("deletions") or 0}, {r.get("changed_files") or len(r.get("files") or [])} files'


def checks_of(r):
    state, names = check_state(r.get('checks') or [])
    n = len(r.get('checks') or [])
    return {'none': 'no checks', 'green': f'green ({n})', 'red': 'RED: ' + ', '.join(names[:2]),
            'pending': 'running: ' + ', '.join(names[:2])}[state]


def trunk_of(r):
    bits = []
    if r.get('behind_by') is not None:
        bits.append(f'{r["behind_by"]} behind')
    if r.get('mergeable') is False or r.get('mergeable_state') == 'dirty':
        bits.append('CONFLICTS')
    elif r.get('mergeable') is None:
        bits.append('merge unknown')
    return ', '.join(bits) or '-'


def areas_of(r, n=3):
    tops = {}
    for f in r.get('files') or []:
        parts = f['path'].split('/')
        key = '/'.join(parts[:2]) if len(parts) > 2 else f['path']
        tops[key] = tops.get(key, 0) + 1
    best = sorted(tops.items(), key=lambda kv: -kv[1])[:n]
    more = len(tops) - len(best)
    return ', '.join(f'{k} ({v})' for k, v in best) + (f', +{more} more' if more > 0 else '')


def flags_of(r):
    out = []
    if draft_reason(r):
        out.append('draft')
    if scratch_reason(r):
        out.append('scratch')
    return ', '.join(out) or '-'


def render(rows, ctx, invoker, now=None):
    now = now or now_utc()
    counts = {}
    for x in rows:
        counts[x['class']] = counts.get(x['class'], 0) + 1
    out = [f'## Land queue: {len(rows)} open PRs by {ctx.get("user")} on {REPO}',
           '',
           f'Read {ctx.get("read_at", "")[:16].replace("T", " ")} UTC against `{BASE}` '
           f'`{(ctx.get("base_sha") or "")[:10]}`. Invoker: {invoker}. '
           + ', '.join(f'**{k}** {v}' for k, v in sorted(counts.items())) + '.', '']
    if ctx.get('shallow'):
        out += [f'_This clone is shallow (history from {ctx.get("history_from", "")[:10]}): the "overlaps a merged PR" '
                f'test sees only merges since then._', '']
    out += ['### What each PR is', '',
            '| PR | Age | Last commit | Branch | Behind / conflicts | Checks on head | Size | Touches | Flags | Class |',
            '|---|---|---|---|---|---|---|---|---|---|']
    for x in sorted(rows, key=lambda x: -x['pr']):
        r = x['rec']
        age = days_since(r.get('created_at'), now)
        last = days_since(r.get('last_commit_at'), now)
        out.append(f'| [#{r["number"]}](https://github.com/{REPO}/pull/{r["number"]}) {esc(r["title"][:60])} '
                   f'| {int(age) if age is not None else "?"}d | {int(last) if last is not None else "?"}d ago '
                   f'| `{esc(r["head"])}` | {trunk_of(r)} | {esc(checks_of(r))} | {size_of(r)} | {esc(areas_of(r))} '
                   f'| {flags_of(r)} | **{x["class"]}** |')
    out += ['', '### The plan', '',
            '| Order | PR | Class | Tier | Action | Reason |', '|---|---|---|---|---|---|']
    for x in rows:
        out.append(f'| {x["order"] or "-"} | #{x["pr"]} | {x["class"]} | {TIERS[x["tier"]]} | {esc(x["action"])} '
                   f'| {esc(x["reason"])} |')
    if ctx.get('errors'):
        out += ['', 'Not read (GitHub did not answer): ' + ', '.join(f'#{n}' for n, _ in ctx['errors'])]
    return '\n'.join(out)


def to_json(rows, ctx):
    slim = {k: v for k, v in ctx.items() if k not in ('blobs', 'merged', 'open_prs')}
    return json.dumps({'context': slim, 'plan': [
        {**{k: v for k, v in x.items() if k != 'rec'}, 'title': x['rec']['title'], 'head': x['rec']['head'],
         'sha': x['rec']['sha'], 'files': [f['path'] for f in x['rec'].get('files') or []],
         'close_comment': close_comment(x) if x['class'] in CLOSABLE else None} for x in rows]}, indent=1)


# --------------------------------------------------------------------------------------------- trunk + gates

def trunk_state(root=None):
    """('green' | 'red' | 'pending' | 'unknown', detail). The Bleeding-edge guard's newest run on the trunk, and
    every check on the trunk's head commit."""
    try:
        head = gh(f'repos/{REPO}/branches/{BASE}')['commit']['sha']
        runs = gh(f'repos/{REPO}/actions/workflows/bleeding-edge-guard.yml/runs?branch={BASE}&per_page=5')
        checks = latest_checks(gh(f'repos/{REPO}/commits/{head}/check-runs?per_page=100', paginate=True),
                               gh(f'repos/{REPO}/commits/{head}/status').get('statuses') or [])
    except GhError as e:
        return 'unknown', str(e)
    guard = next((w for w in runs.get('workflow_runs') or [] if w.get('status') == 'completed'
                  and w.get('conclusion') != 'cancelled'), None)
    state, names = check_state(checks)
    if guard and guard.get('conclusion') in RED_CONCLUSIONS:
        return 'red', (f'Bleeding-edge guard {guard["conclusion"]} on {guard["head_sha"][:10]} '
                       f'({guard["html_url"]})' + (f'; red on the head: {", ".join(names)}' if state == 'red' else ''))
    if state == 'red':
        return 'red', f'red on the trunk head {head[:10]}: {", ".join(names)}'
    if state == 'pending':
        return 'pending', f'still running on the trunk head {head[:10]}: {", ".join(names)}'
    return 'green', f'trunk head {head[:10]} green; guard {guard["conclusion"] if guard else "has no run"}'


def generators_for(root, paths):
    """The generator --check scripts a diff touches: the generator itself, the shared library (all of them), a path
    it names, or its mode's name in a path."""
    sys.path.insert(0, os.path.join(root, 'Tools', 'Build'))
    import ship_gates  # type: ignore
    gens = ship_gates.generators(root)
    if any(p.endswith('arcade_mode_lib.py') for p in paths):
        return gens
    out = []
    lower = [p.lower() for p in paths]
    for g in gens:
        if g in paths:
            out.append(g)
            continue
        src = open(os.path.join(root, g), encoding='utf-8', errors='replace').read()
        # Quoted repo paths of 3+ segments: 'Assets/_Scripts' would claim every C# file in the game.
        named = {m.group(2).rstrip('/') for m in re.finditer(r'([\'"])((?:Assets|Docs|Tools)/[^\'"*{}\n]+)\1', src)
                 if m.group(2).rstrip('/').count('/') >= 2}
        if any(p == n or p.startswith(n + '/') for n in named for p in paths):
            out.append(g)
            continue
        m = re.match(r'author_(.+?)(_assets)?\.py$', os.path.basename(g))
        token = (m.group(1) if m else '').replace('_', '').lower()
        if len(token) >= 4 and any(token in p.replace('_', '').replace(' ', '') for p in lower if p.startswith('assets/')):
            out.append(g)
    return out


def fast_gates(root, paths):
    """[(label, argv)] the land queue runs on a PR: three static checks, the generators the diff touches, and the
    reference compile when C# changed (run only where a .NET SDK already exists, /ship 0.05)."""
    gates = [('validate_project', ['Tools/CI/validate_project.py']),
             ('conditional_compilation', ['Tools/Build/check_conditional_compilation.py', '--root', 'Assets']),
             ('console_logging', ['Tools/Build/check_console_logging.py'])]
    gates += [(os.path.basename(g) + ' --check', [g, '--check']) for g in generators_for(root, paths)]
    if any(p.endswith('.cs') for p in paths):
        gates.append(('unity_refcompile', ['bash', 'Tools/Build/unity_refcompile/run.sh']))
    return gates


def run_gates(root, gates, quiet=False):
    """Run each gate in `root`; returns the [(label, argv)] that failed. Exit codes are read from the script
    itself, never through a pipe (/ship 0.05)."""
    sys.path.insert(0, os.path.join(root, 'Tools', 'Build'))
    import ship_gates  # type: ignore
    fails = []
    say = (lambda *_: None) if quiet else print
    for label, argv in gates:
        if argv[0] == 'bash':
            _, exe = ship_gates.dotnet_exe()
            if not exe:
                say(f'SKIP  {label}: no .NET SDK (a SKIP is not a pass; say "not compiled" in the PR, /ship 0.05)')
                continue
            p = subprocess.run(argv, cwd=root, capture_output=True, text=True, errors='replace', timeout=3600)
            rc, out = p.returncode, p.stdout + p.stderr
        else:
            rc, out = ship_gates.run_one(argv[0], argv[1:], root=root)
        if rc == 0:
            say(f'ok    {label}')
        else:
            fails.append((label, argv))
            say(f'FAIL  {label} (exit {rc}) :: {ship_gates.tail(out)}')
    say('GATES: ALL OK' if not fails else f'GATES: {len(fails)} FAILED')
    return fails


def inherited(root, fails, base_ref):
    """Which failing gates fail on the trunk too (A/B in a detached worktree of the base). Those are the trunk's,
    not the PR's: the PR may still be refreshed, and the red trunk stops every merge on its own."""
    if not fails:
        return []
    wt = tempfile.mkdtemp(prefix='land-queue-base-')
    git(root, 'worktree', 'add', '--detach', wt, base_ref, check=True)
    try:
        return [f for f in fails if f in run_gates(wt, [f], quiet=True)]
    finally:
        git(root, 'worktree', 'remove', '--force', wt)
        shutil.rmtree(wt, ignore_errors=True)


# --------------------------------------------------------------------------------------------- refresh

def deepen_until_based(root, head_ref, base_ref, steps=(100, 500, 2000)):
    """True once the merge base can be trusted. In a SHALLOW clone several bases mean git cannot see the real
    ancestor and builds a garbage virtual one (/ship 1), so deepen until there is exactly one. In a full clone
    several bases are real (criss-cross merges from earlier "merge bleeding-edge in" commits) and ort handles them."""
    for n in (0,) + tuple(steps):
        if n:
            git(root, 'fetch', '-q', f'--deepen={n}', 'origin', BASE, head_ref.split('/', 1)[1])
        bases = git(root, 'merge-base', '--all', base_ref, head_ref).stdout.split()
        if len(bases) == 1 or (bases and not is_shallow(root)):
            return True
    return False


def refresh(root, num, invoker, push=False, trailers=(), keep=False, asked=False):
    r = gh(f'repos/{REPO}/pulls/{num}')
    author, branch = (r.get('user') or {}).get('login'), r['head']['ref']
    if r.get('state') != 'open':
        print(f'#{num} is {r.get("state")}; nothing to refresh')
        return 2
    if (author or '').lower() != (invoker or '').lower() and not asked:
        print(f'#{num} is {author}\'s, not {invoker}\'s: refresh only your own branches (or pass --author-asked '
              f'"<link>" when its author asked you to)')
        return 2
    if ((r['head'].get('repo') or {}).get('full_name') or '').lower() != REPO.lower():
        print(f'#{num} comes from a fork; the land queue pushes only to {REPO} branches')
        return 2
    if branch in (BASE, 'development', 'master', 'main') or branch.startswith('build/'):
        print(f'refusing to refresh a protected branch: {branch}')
        return 2
    git(root, 'fetch', '-q', 'origin', BASE, f'{branch}:refs/remotes/origin/{branch}', check=True)
    base_ref, head_ref = f'origin/{BASE}', f'origin/{branch}'
    if git(root, 'rev-parse', head_ref).stdout.strip() != r['head']['sha']:
        print(f'origin/{branch} moved while reading; run again')
        return 3
    if git(root, 'merge-base', '--is-ancestor', base_ref, head_ref).returncode == 0:
        print(f'#{num} `{branch}` already contains {BASE}; gating its head as it is')
        merging = False
    else:
        merging = True
        if not deepen_until_based(root, head_ref, base_ref):
            print(f'BLOCKED: no single merge base between {branch} and {BASE} even after deepening (shallow clone); '
                  f'run `git fetch --unshallow` and refresh again')
            return 4
    wt = tempfile.mkdtemp(prefix=f'land-queue-{num}-')
    git(root, 'worktree', 'add', '--detach', wt, head_ref, check=True)
    try:
        if merging:
            be = git(root, 'rev-parse', '--short=10', base_ref).stdout.strip()
            m = git(wt, 'merge', '--no-ff', '--no-commit', base_ref)
            conflicted = git(wt, 'diff', '--name-only', '--diff-filter=U').stdout.split()
            resolved = []
            for path in list(conflicted):
                # One side clearly wins only when the two sides differ by whitespace alone.
                ours = git(wt, 'show', f':2:{path}').stdout
                theirs = git(wt, 'show', f':3:{path}').stdout
                if ours and theirs and re.sub(r'\s+', '', ours) == re.sub(r'\s+', '', theirs):
                    git(wt, 'checkout', '--theirs', '--', path)
                    git(wt, 'add', '--', path)
                    conflicted.remove(path)
                    resolved.append(path)
                    print(f'resolved  {path}: the two sides differ by whitespace only; took {BASE}\'s')
            if conflicted:
                print(f'BLOCKED: #{num} `{branch}` conflicts with {BASE} {be} in {len(conflicted)} file(s) no side '
                      f'clearly wins:')
                for p in conflicted:
                    print(f'  {p}')
                git(wt, 'merge', '--abort')
                return 4
            if m.returncode != 0 and not git(wt, 'diff', '--cached', '--name-only').stdout.strip():
                print(f'merge failed: {(m.stderr or m.stdout).strip()[:300]}')
                return 3
            msg = (f'merge: {BASE} ({be}) into {branch} before landing #{num}\n\n'
                   f'Refreshed by /land-queue so #{num} lands on the current trunk. '
                   + (f'Whitespace-only conflicts, {BASE}\'s side taken: {", ".join(resolved)}.\n' if resolved
                      else 'No conflicts.\n'))
            if trailers:
                msg += '\n' + '\n'.join(trailers) + '\n'
            git(wt, 'commit', '-q', '-m', msg, check=True)
        changed = git(wt, 'diff', '--name-only', f'{base_ref}...HEAD').stdout.split()
        print(f'gating #{num} `{branch}` at {git(wt, "rev-parse", "--short=10", "HEAD").stdout.strip()} '
              f'({len(changed)} files differ from {BASE})')
        fails = run_gates(wt, fast_gates(wt, changed))
        sha = git(wt, 'rev-parse', 'HEAD').stdout.strip()
        theirs = inherited(root, fails, base_ref)
        for label, _ in theirs:
            print(f'INHERITED  {label}: red on {BASE} too, so not this PR\'s (the trunk check stops the merge)')
        own = [f for f in fails if f not in theirs]
        if own:
            print(f'BLOCKED: #{num} red gates on the merged tree that {BASE} passes: '
                  f'{", ".join(l for l, _ in own)}; fix them on `{branch}`, never skip one')
            return 1
        if merging and push:
            for wait in (0, 2, 4, 8, 16):
                time.sleep(wait)
                p = git(wt, 'push', 'origin', f'HEAD:refs/heads/{branch}')
                if p.returncode == 0:
                    print(f'pushed {sha[:10]} to {branch}')
                    break
                if 'rejected' in (p.stderr or ''):
                    print(f'push rejected ({branch} moved): run refresh again')
                    return 3
            else:
                print(f'push failed: {p.stderr.strip()[:300]}')
                return 3
        elif merging:
            print(f'not pushed (no --push): the merge {sha[:10]} lives only in {wt}' if keep else
                  f'not pushed (no --push): the merge was gated and dropped')
        print(f'GATED {sha}')
        return 0
    finally:
        if not keep:
            git(root, 'worktree', 'remove', '--force', wt)
            shutil.rmtree(wt, ignore_errors=True)


def preflight(num, gated_sha):
    """Right before a merge: the PR is open and mergeable, its head is the commit the gates ran on, nothing on it
    is red, and the trunk is green."""
    problems = []
    r = gh(f'repos/{REPO}/pulls/{num}')
    for _ in range(3):
        if r.get('mergeable') is not None:
            break
        time.sleep(2)
        r = gh(f'repos/{REPO}/pulls/{num}')
    if r.get('state') != 'open':
        problems.append(f'#{num} is {r.get("state")}{" (merged)" if r.get("merged") else ""}')
    if r.get('draft'):
        problems.append('it is a draft')
    if r.get('mergeable') is False:
        problems.append('it conflicts with bleeding-edge: refresh it again')
    elif r.get('mergeable') is None:
        problems.append('GitHub has not computed mergeable yet: run preflight again')
    if gated_sha and r['head']['sha'] != gated_sha:
        problems.append(f'its head moved to {r["head"]["sha"][:10]} after the gates ran on {gated_sha[:10]}: gate it again')
    state, names = check_state(latest_checks(gh(f'repos/{REPO}/commits/{r["head"]["sha"]}/check-runs?per_page=100',
                                                paginate=True),
                                             gh(f'repos/{REPO}/commits/{r["head"]["sha"]}/status').get('statuses') or []))
    if state == 'red':
        problems.append('red on its head: ' + ', '.join(names))
    tstate, detail = trunk_state()
    if tstate != 'green':
        problems.append(f'trunk {tstate}: {detail} -- do not merge; run /trunk-doctor (or fix the trunk on a new branch)')
    if problems:
        print(f'NO MERGE #{num}:')
        for p in problems:
            print(f'  - {p}')
        return 1
    print(f'OK to merge #{num} at {r["head"]["sha"][:10]} (merge method: merge, the trunk\'s habit)')
    return 0


def merge_habit(root):
    """How PRs land on the trunk, measured: merge commits vs squashes on its first-parent line."""
    subj = git(root, 'log', '--first-parent', '--format=%s', '--since=60 days ago', f'origin/{BASE}').stdout.splitlines()
    merges = sum(1 for s in subj if s.startswith('Merge pull request #'))
    squashes = sum(1 for s in subj if not s.startswith('Merge pull request #') and re.search(r'\(#\d+\)$', s))
    return merges, squashes


# --------------------------------------------------------------------------------------------- record

def record(root, args):
    path = os.path.join(root, REPORT_REL)
    head = ('# Land queue\n\nOne entry per `/land-queue` run (`.claude/skills/land-queue/`), newest last: what landed in '
            '`bleeding-edge`, what is blocked and why, what was closed, and the open count before and after. Written '
            'by `land_queue.py record`; append, never rewrite.\n')
    text = open(path, encoding='utf-8').read() if os.path.isfile(path) else head
    when = now_utc().strftime('%Y-%m-%d %H:%M UTC')

    def nums(s):
        return ', '.join(f'#{n.strip()}' for n in (s or '').split(',') if n.strip()) or 'none'
    lines = [f'\n## {when} - {args.person or args.user}\n',
             f'- Open PRs by {args.user}: {args.before} before, {args.after} after.',
             f'- Merged into `{BASE}`: {nums(args.merged)}.',
             f'- Closed (branch kept): {nums(args.closed)}.']
    for b in args.blocked or []:
        lines.append(f'- Blocked: {b}')
    if args.note:
        lines.append(f'- {args.note}')
    with open(path, 'w', encoding='utf-8') as f:
        f.write(text.rstrip('\n') + '\n' + '\n'.join(lines) + '\n')
    print(f'appended to {REPORT_REL}')
    return 0


# --------------------------------------------------------------------------------------------- self-test

def fixture(num, **kw):
    base = {'number': num, 'title': f'feat(x): fixture {num}', 'author': 'me', 'head': f'claude/fixture-{num}',
            'head_repo': REPO, 'sha': f'{num:040d}', 'draft': False, 'labels': [], 'body': '', 'comments': [],
            'created_at': '2026-11-10T00:00:00Z', 'updated_at': '2026-11-11T00:00:00Z',
            'last_commit_at': '2026-11-11T00:00:00Z', 'additions': 10, 'deletions': 2, 'changed_files': 1,
            'mergeable': True, 'mergeable_state': 'clean', 'ahead_by': 1, 'behind_by': 0, 'compare_status': 'ahead',
            'files': [{'path': f'Assets/_Scripts/Fixture{num}.cs', 'status': 'modified', 'blob': f'b{num}', 'previous': None}],
            'checks': [{'name': 'static', 'status': 'completed', 'conclusion': 'success'}]}
    base.update(kw)
    return base


def self_test():
    now = parse_time('2026-11-12T12:00:00Z')
    ctx = {'blobs': {'Docs/Same.md': 'same-blob', 'Assets/Old.cs': 'zzz'},
           'merged': [{'number': 900, 'at': '2026-11-11T12:00:00Z', 'branch': 'claude/other',
                       'files': {'Assets/A.cs', 'Assets/B.cs', 'Assets/C.cs'}}],
           'open_prs': [{'number': 777, 'title': 'feat: the new one', 'body': 'This supersedes #12.', 'author': 'me'}],
           'launch_prs': [30]}
    red = [{'name': 'Static validation', 'status': 'completed', 'conclusion': 'failure'}]
    cases = [
        ('green, clean -> READY', fixture(1), 'READY'),
        ('no checks at all -> READY (claude/** fires no CI)', fixture(2, checks=[]), 'READY'),
        ('behind -> NEEDS-REFRESH', fixture(3, behind_by=12), 'NEEDS-REFRESH'),
        ('conflicting -> NEEDS-REFRESH', fixture(4, mergeable=False, mergeable_state='dirty'), 'NEEDS-REFRESH'),
        ('failing check -> RED', fixture(5, checks=red), 'RED'),
        ('still running -> PENDING', fixture(6, checks=[{'name': 'x', 'status': 'in_progress', 'conclusion': None}]),
         'PENDING'),
        ('20 days without a commit -> STALE', fixture(7, last_commit_at='2026-10-23T00:00:00Z'), 'STALE'),
        ('a later merged PR changed most of its files -> STALE',
         fixture(8, files=[{'path': p, 'status': 'modified', 'blob': 'q', 'previous': None}
                           for p in ('Assets/A.cs', 'Assets/B.cs', 'Assets/D.cs', 'CLAUDE.md')],
                 last_commit_at='2026-11-10T00:00:00Z'), 'STALE'),
        ('overlap, but it still adds a file the trunk lacks (stacked on a PR that landed) -> NEEDS-REFRESH',
         fixture(11, behind_by=40, files=[{'path': p, 'status': s_, 'blob': 'q', 'previous': None}
                                         for p, s_ in (('Assets/A.cs', 'added'), ('Assets/B.cs', 'added'),
                                                       ('Tools/Launch/release_notes.py', 'added'))],
                 last_commit_at='2026-11-10T00:00:00Z'), 'NEEDS-REFRESH'),
        ('every file already on bleeding-edge -> SUPERSEDED',
         fixture(9, files=[{'path': 'Docs/Same.md', 'status': 'modified', 'blob': 'same-blob', 'previous': None}]),
         'SUPERSEDED'),
        ('nothing ahead -> SUPERSEDED', fixture(10, ahead_by=0), 'SUPERSEDED'),
        ('another PR says it supersedes this one -> SUPERSEDED', fixture(12), 'SUPERSEDED'),
        ('its thread says "superseded by #800" -> SUPERSEDED',
         fixture(13, comments=['Superseded by #800, closing soon.']), 'SUPERSEDED'),
        ('negative-control title -> SCRATCH', fixture(14, title='ci: negative control for the guard'), 'SCRATCH'),
        ('scratch branch -> SCRATCH', fixture(15, head='ci-scratch/launch-tools-negative'), 'SCRATCH'),
        ('"do not merge" in the description -> SCRATCH', fixture(16, body='Rehearsal only. DO NOT MERGE.'), 'SCRATCH'),
        ('GitHub draft -> DRAFT', fixture(17, draft=True), 'DRAFT'),
        ('[WIP] title -> DRAFT', fixture(18, title='[WIP] feat: half a thing'), 'DRAFT'),
        # Evidence prose is not a label: a PR that CITES its negative control is a normal PR.
        ('"Do not merge without a human review" is a review request -> not SCRATCH',
         fixture(20, body='Turns the guard into a check. **Do not merge without a human review.**'), 'READY'),
        ('"negative control fails to compile" as evidence -> not SCRATCH',
         fixture(19, body='Proven: refcompile OK; a negative control fails to compile (#1752).'), 'READY'),
    ]
    # THE NEGATIVE CONTROL: a red PR must never read READY, whatever else about it is perfect.
    reds = [
        ('red + green + clean + current', fixture(40, checks=red + [{'name': 'ok', 'status': 'completed',
                                                                       'conclusion': 'success'}])),
        ('red + no conflicts + up to date + launch blocker', fixture(30, checks=red)),
        ('red via a commit status', fixture(41, checks=latest_checks([], [{'id': 1, 'context': 'ci/x', 'state': 'error'}]))),
        ('red re-run after a green run (newest run counts)', fixture(42, checks=latest_checks(
            [{'id': 1, 'name': 'guard', 'status': 'completed', 'conclusion': 'success', 'check_suite': {'id': 7}},
             {'id': 2, 'name': 'guard', 'status': 'completed', 'conclusion': 'failure', 'check_suite': {'id': 7}}], []))),
        ('red timed out', fixture(43, checks=[{'name': 't', 'status': 'completed', 'conclusion': 'timed_out'}])),
    ]
    errs = []
    for label, rec, want in cases:
        got, why = classify(rec, ctx, now)
        if got != want:
            errs.append(f'{label}: got {got} ({why})')
    for label, rec in reds:
        got, why = classify(rec, ctx, now)
        if got == 'READY':
            errs.append(f'NEGATIVE CONTROL FAILED: a red PR read READY ({label})')
        elif got != 'RED':
            errs.append(f'{label}: expected RED, got {got} ({why})')
    # A green re-run after a red run is green again: the collapse keeps the newest run, not the worst.
    rerun = latest_checks([{'id': 1, 'name': 'g', 'status': 'completed', 'conclusion': 'failure', 'check_suite': {'id': 3}},
                           {'id': 2, 'name': 'g', 'status': 'completed', 'conclusion': 'success', 'check_suite': {'id': 3}}],
                          [])
    if check_state(rerun)[0] != 'green':
        errs.append('a green re-run after a red run did not read green')
    # Ordering: CI, launch, scene/UGS, feature, docs; never two PRs on one scene back to back.
    def f(path, blob='n'):
        return {'path': path, 'status': 'modified', 'blob': blob, 'previous': None}
    recs = [fixture(51, files=[f('Docs/Notes.md')], created_at='2026-11-01T00:00:00Z'),
            fixture(52, files=[f('Assets/_Scripts/Game.cs')], created_at='2026-11-02T00:00:00Z'),
            fixture(53, files=[f('Assets/_Scenes/Menu_Main.unity')], created_at='2026-11-03T00:00:00Z'),
            fixture(54, files=[f('Assets/_Scenes/Menu_Main.unity'), f('Assets/x.cs')], created_at='2026-11-04T00:00:00Z'),
            fixture(55, files=[f('Assets/_Prefabs/Other.prefab')], created_at='2026-11-05T00:00:00Z'),
            fixture(30, files=[f('Assets/_Scripts/Launch.cs')], created_at='2026-11-06T00:00:00Z'),
            fixture(56, files=[f('.github/workflows/x.yml')], created_at='2026-11-07T00:00:00Z'),
            fixture(59, title='feat(launch): a launch blocker with one workflow line',
                    files=[f('.github/workflows/y.yml')] + [f(f'Assets/_Scripts/L{i}.cs') for i in range(5)],
                    created_at='2026-11-08T00:00:00Z'),
            fixture(57, author='someone-else', files=[f('Assets/Theirs.cs')]),
            fixture(58, checks=red)]
    rows = plan(recs, ctx, 'me', now)
    order = [x['pr'] for x in rows if x['order']]
    if order[:3] != [56, 30, 59]:
        errs.append(f'CI, then the launch blockers (one workflow file does not make a pipeline PR), should lead, '
                    f'got {order}')
    if order.index(51) != len(order) - 1:
        errs.append(f'docs-only should land last, got {order}')
    if order.index(53) + 1 == order.index(54):
        errs.append(f'two PRs on Menu_Main.unity landed back to back without a PR between them: {order}')
    if 58 in order:
        errs.append('a red PR was put in the landing order')
    theirs = next(x for x in rows if x['pr'] == 57)
    if not theirs['action'].startswith('report only'):
        errs.append('another person\'s PR got a landing action')
    # Same scene, nothing to put between them: the second refreshes after the first lands.
    rows2 = plan([recs[2], recs[3]], ctx, 'me', now)
    if not rows2[1].get('after_refresh'):
        errs.append('the second of two same-scene PRs was not marked to refresh after the first')
    # Gate selection: a C# change gets the compile, a generator change gets its own --check.
    root = repo_root()
    labels = [g[0] for g in fast_gates(root, ['Assets/_Scripts/Foo.cs', 'Tools/Build/author_thresher_assets.py'])]
    for want in ('validate_project', 'conditional_compilation', 'console_logging', 'unity_refcompile',
                 'author_thresher_assets.py --check'):
        if want not in labels:
            errs.append(f'fast_gates missed {want}: {labels}')
    if 'unity_refcompile' in [g[0] for g in fast_gates(root, ['Docs/x.md'])]:
        errs.append('a docs-only diff asked for the C# compile')
    stray = [g[0] for g in fast_gates(root, ['Docs/x.md', 'Tools/Build/author_ingame_hud.py', 'Assets/_Scripts/Foo.cs'])
             if g[0].endswith('--check')]
    if stray:
        errs.append(f'generators claimed a diff they do not own: {stray}')
    for e in errs:
        print('FAIL:', e)
    n = len(cases) + len(reds) + 9
    print(f'land_queue self-test: {"OK" if not errs else f"{len(errs)} failure(s)"} ({n} checks, '
          f'{len(reds)} red negative controls)')
    return 1 if errs else 0


# --------------------------------------------------------------------------------------------- main

def main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--self-test', action='store_true')
    sub = ap.add_subparsers(dest='cmd')
    li = sub.add_parser('list', help='classify and plan (the default)')
    for p in (ap, li):
        p.add_argument('--user', help='whose open PRs (default: the gh user)')
        p.add_argument('--as', dest='invoker', help='who is landing (default: the gh user); only their PRs get actions')
        p.add_argument('--pr', help='only these PR numbers (comma separated)')
        p.add_argument('--asked', default='', help='PRs whose authors asked the invoker to land them (comma separated)')
        p.add_argument('--json', action='store_true')
        p.add_argument('--save', help='write the raw records to this file (re-plan later with --from-json)')
        p.add_argument('--from-json', help='plan from saved records instead of GitHub')
        p.add_argument('--no-fetch', action='store_true', help='do not git fetch bleeding-edge first')
    sub.add_parser('trunk', help='is bleeding-edge green? exit 0 green, 1 red/pending, 3 unknown')
    fl = sub.add_parser('files', help='the files a PR changes, one per line (for approvals.py check --files)')
    fl.add_argument('pr', type=int)
    ga = sub.add_parser('gates', help='which fast gates a PR (or a file list) needs')
    ga.add_argument('--pr', type=int)
    ga.add_argument('--files', nargs='*')
    rf = sub.add_parser('refresh', help='merge bleeding-edge into ONE own PR branch in a scratch worktree, gate it')
    rf.add_argument('pr', type=int)
    rf.add_argument('--push', action='store_true', help='push the merge (never a force-push)')
    rf.add_argument('--as', dest='invoker')
    rf.add_argument('--author-asked', help='a link or quote showing the PR author asked you to land it')
    rf.add_argument('--trailer', action='append', default=[], help='a commit-message trailer line (repeatable)')
    rf.add_argument('--keep', action='store_true', help='keep the scratch worktree')
    pf = sub.add_parser('preflight', help='right before a merge: open, mergeable, gated head, no red, trunk green')
    pf.add_argument('pr', type=int)
    pf.add_argument('--gated-sha', required=True)
    rc = sub.add_parser('record', help=f'append a dated entry to {REPORT_REL}')
    rc.add_argument('--user', required=True)
    rc.add_argument('--person')
    rc.add_argument('--before', type=int, required=True)
    rc.add_argument('--after', type=int, required=True)
    rc.add_argument('--merged', default='')
    rc.add_argument('--closed', default='')
    rc.add_argument('--blocked', action='append', help='"#N: why" (repeatable)')
    rc.add_argument('--note')
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    root = repo_root()
    cmd = args.cmd or 'list'
    if cmd == 'trunk':
        state, detail = trunk_state()
        merges, squashes = merge_habit(root)
        print(f'trunk {state}: {detail}')
        print(f'habit: {merges} merge commits, {squashes} squashes on {BASE} in 60 days -> merge with a merge commit')
        return {'green': 0, 'unknown': 3}.get(state, 1)
    if cmd == 'files':
        for f in gh(f'repos/{REPO}/pulls/{args.pr}/files?per_page=100', paginate=True):
            print(f['filename'])
        return 0
    if cmd == 'gates':
        files = args.files or []
        if args.pr:
            files = [f['filename'] for f in gh(f'repos/{REPO}/pulls/{args.pr}/files?per_page=100', paginate=True)]
        for label, argv_ in fast_gates(root, files):
            print(f'{label}\t{" ".join(argv_)}')
        return 0
    if cmd == 'refresh':
        invoker = args.invoker or gh_login()
        return refresh(root, args.pr, invoker, push=args.push, trailers=args.trailer, keep=args.keep,
                       asked=bool(args.author_asked))
    if cmd == 'preflight':
        return preflight(args.pr, args.gated_sha)
    if cmd == 'record':
        return record(root, args)
    # list
    invoker = args.invoker or gh_login()
    user = args.user or invoker
    if not user:
        print('land_queue: no GitHub user (gh api user failed); pass --user <login>', file=sys.stderr)
        return 2
    asked = {int(n) for n in args.asked.split(',') if n.strip()}
    if args.from_json:
        saved = json.load(open(args.from_json, encoding='utf-8'))
        records, ctx = saved['records'], saved['context']
        for m in ctx.get('merged') or []:
            m['files'] = set(m['files'])
    else:
        nums = {int(n) for n in args.pr.split(',')} if args.pr else None
        try:
            records, ctx = collect(root, user, nums, fetch=not args.no_fetch)
        except GhError as e:
            print(f'land_queue: GitHub did not answer: {e}', file=sys.stderr)
            return 3
        if args.save:
            ser = dict(ctx, merged=[dict(m, files=sorted(m['files'])) for m in ctx['merged']])
            json.dump({'records': records, 'context': ser}, open(args.save, 'w', encoding='utf-8'))
    rows = plan(records, ctx, invoker, asked=asked)
    print(to_json(rows, ctx) if args.json else render(rows, ctx, invoker))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
