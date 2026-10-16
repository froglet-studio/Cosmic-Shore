#!/usr/bin/env python3
"""team_activity.py - a UserPromptSubmit hook (.claude/settings.json) that keeps every session up to date with what the
other sessions and collaborators push, on every branch, while it works (Yash, 2026-10-15: "stay updated by searching all
git repos automatically every time someone works on something").

On each prompt it:
  1. starts a background `git fetch --prune origin` when the last fetch is older than FETCH_EVERY_S (never waits for it:
     a hook must not stall a prompt, so the NEXT prompt sees what this one fetched);
  2. compares every origin/* branch tip with the tips this session saw last time and reports the commits that are new
     since (no merges, one line each: branch, author, age, subject), newest first, at most MAX_LINES;
  3. marks a commit HOT when it touches a file THIS session has worked on (every file it had uncommitted edits in at
     a prompt, plus every file in the commits it made itself), so overlapping work is flagged before it collides.
     Files every session edits (the verification checklist, the doc index) never make a commit HOT.

The session's own commits are never reported back to it.

The first prompt of a session reports the last FIRST_WINDOW_H hours instead of nothing. Context for Claude only; it
never blocks a prompt and always exits 0. State: .git/claude-team-activity/<session>.json (local, never committed).

    python3 .claude/hooks/team_activity.py --self-test
    python3 .claude/hooks/team_activity.py --peek [HOURS]   # the last HOURS (default 12) now, state untouched
                                                            # (the Cosmic Shore deck's Team button)
"""
import json
import os
import subprocess
import sys
import tempfile
import time

FETCH_EVERY_S = 180
FIRST_WINDOW_H = 12
MAX_LINES = 20
GIT_TIMEOUT_S = 4
# Edited by nearly every change, so an overlap there says nothing.
SHARED_FILES = {
    'Docs/UNITY_VERIFICATION_CHECKLIST.md', 'Docs/claude/DOCUMENTATION_INDEX.md', 'Docs/AI_SYSTEM/SYNC_LOG.md',
    'Docs/TEAM.md', 'Docs/TEAM.json', 'Docs/QA/QA_BACKLOG.md',
}


def git(root, *args, timeout=GIT_TIMEOUT_S):
    try:
        r = subprocess.run(['git', '-C', root, *args], capture_output=True, text=True, timeout=timeout)
        return r.stdout if r.returncode == 0 else ''
    except (OSError, subprocess.SubprocessError):
        return ''


def git_dir(root):
    d = git(root, 'rev-parse', '--git-dir').strip()
    return d if os.path.isabs(d) else os.path.join(root, d) if d else ''


def maybe_fetch(root, gdir, now=None):
    """Start a background fetch when FETCH_HEAD is older than FETCH_EVERY_S. Returns True when one was started."""
    now = now or time.time()
    head = os.path.join(gdir, 'FETCH_HEAD')
    try:
        age = now - os.path.getmtime(head)
    except OSError:
        age = float('inf')
    if age < FETCH_EVERY_S:
        return False
    try:
        # Touch first, so two prompts in quick succession do not start two fetches.
        with open(head, 'a', encoding='utf-8'):
            os.utime(head, None)
        subprocess.Popen(['git', '-C', root, 'fetch', '-q', '--prune', 'origin'], stdin=subprocess.DEVNULL,
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
        return True
    except OSError:
        return False


def branch_tips(root):
    out = git(root, 'for-each-ref', '--format=%(refname:short) %(objectname)', 'refs/remotes/origin')
    tips = {}
    for line in out.splitlines():
        name, _, sha = line.partition(' ')
        if name and sha and not name.endswith('/HEAD'):
            tips[name] = sha
    return tips


def own_work(root, prev_head, old_tips):
    """(shas, files) this session made since its last prompt: the commits from its previous HEAD to HEAD that were on no
    origin branch at that prompt, plus the files with uncommitted edits now."""
    shas, files = set(), set(git(root, 'diff', '--name-only', 'HEAD').split())
    if prev_head:
        out = git(root, 'log', '--no-merges', '--format=\x1e%H', '--name-only', f'{prev_head}..HEAD', '--not',
                  *old_tips.values())
        for block in out.split('\x1e'):
            if block.strip():
                sha, _, names = block.partition('\n')
                shas.add(sha.strip())
                files |= set(names.split())
    return shas, files


def new_commits(root, old_tips, new_tips, since_hours=None, skip=()):
    """[(sha, branch, author, age, time, subject, files)] new on any branch since old_tips (or, with since_hours, in
    that window), deduplicated, newest first, without the shas in skip."""
    seen, rows = set(skip), []
    for branch, tip in sorted(new_tips.items()):
        old = old_tips.get(branch)
        if old == tip:
            continue
        if since_hours is not None:
            rng = [f'--since={since_hours}.hours', tip]
        elif old:
            rng = [f'{old}..{tip}']
        else:   # a branch that appeared since: what is on it and on no branch seen before
            rng = [tip, '--not', *old_tips.values()]
        out = git(root, 'log', '--no-merges', '--format=\x1e%H\x1f%an\x1f%ar\x1f%ct\x1f%s', '--name-only', *rng,
                  timeout=GIT_TIMEOUT_S)
        for block in out.split('\x1e'):
            if not block.strip():
                continue
            head, _, names = block.partition('\n')
            parts = head.split('\x1f')
            if len(parts) < 5 or parts[0] in seen:
                continue
            seen.add(parts[0])
            rows.append((parts[0], branch.replace('origin/', '', 1), parts[1], parts[2], int(parts[3] or 0),
                         parts[4], set(names.split())))
    rows.sort(key=lambda r: -r[4])
    return rows


def report(rows, mine, first):
    hot = set(mine) - SHARED_FILES
    if not rows:
        return ''
    lines = []
    for sha, branch, author, age, _, subject, files in rows[:MAX_LINES]:
        overlap = sorted(files & hot)
        mark = f"  HOT: also touches {', '.join(overlap[:3])}{' ...' if len(overlap) > 3 else ''}" if overlap else ''
        lines.append(f'- {sha[:9]} [{branch}] {author}, {age}: {subject}{mark}')
    more = len(rows) - MAX_LINES
    head = (f'Team activity: {len(rows)} commit(s) pushed in the last {FIRST_WINDOW_H} h on other branches'
            if first else f'Team activity since your last prompt: {len(rows)} new commit(s) pushed')
    tail = [f'- ... and {more} more (git log --all --no-merges).'] if more > 0 else []
    advice = ('Read what overlaps your task before you change those files (git show <sha>; '
              'python3 Tools/Build/who_works_where.py query <paths>), build on it, and tell the person about any '
              'HOT commit. Pull or merge the branch you build on if it moved.')
    return '\n'.join([head + ' (fetched from origin by .claude/hooks/team_activity.py):', *lines, *tail, advice])


def run(root, session, now=None):
    gdir = git_dir(root)
    if not gdir:
        return ''
    maybe_fetch(root, gdir, now)
    state_dir = os.path.join(gdir, 'claude-team-activity')
    state_path = os.path.join(state_dir, f"{''.join(c for c in session if c.isalnum() or c in '-_') or 'default'}.json")
    try:
        with open(state_path, encoding='utf-8') as fh:
            state = json.load(fh)
    except (OSError, ValueError):
        state = None
    first = state is None
    state = state or {}
    old_tips = state.get('tips', {})
    shas, files = own_work(root, state.get('head'), old_tips)
    mine_shas = set(state.get('mine_shas', [])) | shas
    mine_files = set(state.get('mine_files', [])) | files
    tips = branch_tips(root)
    rows = new_commits(root, old_tips, tips, since_hours=FIRST_WINDOW_H if first else None, skip=mine_shas)
    try:
        os.makedirs(state_dir, exist_ok=True)
        with open(state_path, 'w', encoding='utf-8') as fh:
            json.dump({'tips': tips, 'head': git(root, 'rev-parse', 'HEAD').strip(),
                       'mine_shas': sorted(mine_shas)[-500:], 'mine_files': sorted(mine_files)}, fh)
    except OSError:
        pass
    return report(rows, mine_files, first)


def peek(root, hours=FIRST_WINDOW_H):
    """The last `hours` of pushes on every branch, HOT against the files of the most recent session state, without
    fetching or writing any state (so the prompt hook still reports each push once)."""
    gdir = git_dir(root)
    if not gdir:
        return ''
    state_dir = os.path.join(gdir, 'claude-team-activity')
    state = {}
    try:
        paths = [os.path.join(state_dir, n) for n in os.listdir(state_dir) if n.endswith('.json')]
        with open(max(paths, key=os.path.getmtime), encoding='utf-8') as fh:
            state = json.load(fh)
    except (OSError, ValueError):
        pass
    rows = new_commits(root, {}, branch_tips(root), since_hours=hours, skip=set(state.get('mine_shas', [])))
    mine = set(state.get('mine_files', [])) | set(git(root, 'diff', '--name-only', 'HEAD').split())
    return report(rows, mine, True).replace(f'last {FIRST_WINDOW_H} h', f'last {hours} h') or \
        f'Team activity: nothing pushed on any branch in the last {hours} h.'


def self_test():
    """A throwaway origin and clone: a push by someone else must be reported once, marked HOT when it overlaps."""
    with tempfile.TemporaryDirectory() as tmp:
        def sh(cwd, *a):
            subprocess.run(a, cwd=cwd, check=True, capture_output=True)
        origin, mine, theirs = (os.path.join(tmp, n) for n in ('origin.git', 'mine', 'theirs'))
        sh(tmp, 'git', 'init', '-q', '--bare', origin)
        for d in (mine, theirs):
            sh(tmp, 'git', 'clone', '-q', origin, d)
            sh(d, 'git', 'config', 'user.email', 'a@b'); sh(d, 'git', 'config', 'user.name', os.path.basename(d))
        open(os.path.join(mine, 'ai.cs'), 'w').write('a')
        sh(mine, 'git', 'add', '.'); sh(mine, 'git', 'commit', '-qm', 'mine: ai'); sh(mine, 'git', 'push', '-q', 'origin', 'HEAD:main')
        sh(mine, 'git', 'fetch', '-q', 'origin')
        first = run(mine, 's1')
        assert 'mine: ai' in first, 'the first prompt reports the recent window: ' + first
        assert run(mine, 's1') == '', 'nothing new: no report'
        open(os.path.join(mine, 'ai.cs'), 'w').write('mine again')          # this session works on ai.cs
        run(mine, 's1')
        sh(mine, 'git', 'commit', '-qam', 'mine: own work'); sh(mine, 'git', 'push', '-q', 'origin', 'HEAD:main')
        sh(mine, 'git', 'fetch', '-q', 'origin')
        assert 'own work' not in run(mine, 's1'), "the session's own commit is not reported back to it"
        sh(theirs, 'git', 'pull', '-q', 'origin', 'main')
        open(os.path.join(theirs, 'ai.cs'), 'w').write('b')
        sh(theirs, 'git', 'commit', '-qam', 'theirs: touches ai.cs')
        open(os.path.join(theirs, 'other.cs'), 'w').write('c')
        sh(theirs, 'git', 'add', '.'); sh(theirs, 'git', 'commit', '-qm', 'theirs: other file')
        sh(theirs, 'git', 'push', '-q', 'origin', 'HEAD:feature')
        sh(mine, 'git', 'fetch', '-q', 'origin')
        out = run(mine, 's1')
        assert 'theirs: touches ai.cs' in out and 'HOT' in out, 'an overlapping push is reported HOT: ' + out
        hot_lines = [l for l in out.splitlines() if l.startswith('- ') and 'HOT:' in l]
        assert len(hot_lines) == 1 and 'other file' not in hot_lines[0], 'only the overlapping commit is HOT: ' + out
        assert run(mine, 's1') == '', 'a push is reported once'
        # negative control: a session that never saw the old tips gets the window, not a silent skip
        assert 'theirs: other file' in run(mine, 's2'), 'a new session sees the recent window'
        before = open(os.path.join(git_dir(mine), 'claude-team-activity', 's1.json')).read()
        assert 'theirs: touches ai.cs' in peek(mine), 'peek shows the window'
        assert open(os.path.join(git_dir(mine), 'claude-team-activity', 's1.json')).read() == before, 'peek writes no state'
    print('team_activity self-test: ok')
    return 0


def main():
    if '--self-test' in sys.argv:
        return self_test()
    if '--peek' in sys.argv:
        i = sys.argv.index('--peek')
        hours = int(sys.argv[i + 1]) if len(sys.argv) > i + 1 and sys.argv[i + 1].isdigit() else FIRST_WINDOW_H
        print(peek(os.environ.get('CLAUDE_PROJECT_DIR') or os.getcwd(), hours))
        return 0
    try:
        payload = json.load(sys.stdin) if not sys.stdin.isatty() else {}
    except (ValueError, OSError):
        payload = {}
    root = os.environ.get('CLAUDE_PROJECT_DIR') or payload.get('cwd') or os.getcwd()
    try:
        text = run(root, str(payload.get('session_id') or 'default'))
    except Exception:  # a hook never breaks a prompt
        text = ''
    if text:
        print(text)
    return 0


if __name__ == '__main__':
    sys.exit(main())
