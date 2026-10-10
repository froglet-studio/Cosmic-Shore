#!/usr/bin/env python3
"""The git half of a Vessel Studio Sync job, run by the Claude session that receives the job.

    python3 .claude/skills/vessel-studio/sync_job.py status  --branch B [--shown SHA]
    python3 .claude/skills/vessel-studio/sync_job.py compare --from A --to B
    python3 .claude/skills/vessel-studio/sync_job.py merge   --from A --to B --yes
    python3 .claude/skills/vessel-studio/sync_job.py delete  --branch B --yes
    python3 .claude/skills/vessel-studio/sync_job.py --self-test

Prints ONE JSON object on stdout: {"ok": bool, "log": [lines for the panel console], ...result}.
The session copies `log` and the result into the job document (ArtifactData update, collection
`jobs`) and, for status/merge, rebuilds and republishes the artifact (SKILL.md section 5).

Never touches the session's own checkout: merges happen in a throwaway worktree, pushes name the
target branch explicitly. Refuses to merge into or delete a GUARDED branch, refuses a merge that
conflicts (aborts it, lists the files), and needs --yes for anything that writes to the remote.
"""
import argparse, json, os, shutil, subprocess, sys, tempfile

DIR = 'Docs/Studios/VesselStudio'
GUARDED = {'bleeding-edge', 'main', 'master', 'Ys-bleeding-edge'}
WATCH = ['cece/magical-carson-9bdq8z', 'vessel-studio', 'Ys-bleeding-edge', 'bleeding-edge']


def git(*a, cwd=None, check=True):
    r = subprocess.run(['git'] + list(a), cwd=cwd, text=True, capture_output=True)
    if check and r.returncode:
        raise RuntimeError(f'git {" ".join(a)}: {r.stderr.strip() or r.stdout.strip()}')
    return r.stdout.strip()


def fetch(*branches):
    for b in branches:
        for i in range(4):
            r = subprocess.run(['git', 'fetch', '-q', 'origin', f'+refs/heads/{b}:refs/remotes/origin/{b}'], text=True, capture_output=True)
            if r.returncode == 0:
                break
            if 'couldn\'t find remote ref' in r.stderr:
                raise RuntimeError(f'branch "{b}" does not exist on origin')
    return True


def commits(rng, n=20):
    out = git('log', f'--max-count={n}', '--format=%H%x09%s', rng)
    return [dict(zip(('sha', 'subject'), l.split('\t', 1))) for l in out.splitlines() if l]


def count(rng):
    return int(git('rev-list', '--count', rng) or 0)


def status(branch, shown):
    fetch(branch)
    ref = f'origin/{branch}'
    head = git('log', '-1', '--format=%H%x09%s%x09%cI', ref).split('\t')
    path = git('log', '-1', '--format=%H%x09%s', ref, '--', DIR).split('\t')
    log = [f'repo froglet-studio/cosmic-shore, branch {branch}',
           f'head {head[0][:7]} "{head[1]}" {head[2]}',
           f'last studio commit {path[0][:7]} "{path[1]}"']
    fresh = []
    if shown and shown != path[0] and git('cat-file', '-t', shown, check=False) == 'commit':
        out = git('log', '--max-count=10', '--format=%H%x09%s', f'{shown}..{ref}', '--', DIR, check=False)
        fresh = [dict(zip(('sha', 'subject'), l.split('\t', 1))) for l in out.splitlines() if l]
    up_to_date = shown == path[0]
    log.append('studio is up to date' if up_to_date else f'studio changed since {shown[:7] if shown else "an unknown commit"}:')
    log += [f'  {c["sha"][:7]} {c["subject"]}' for c in fresh[:8]]
    sugg = []
    for b in WATCH:
        if b == branch:
            continue
        try:
            fetch(b)
            a, behind = count(f'{ref}..origin/{b}'), count(f'origin/{b}..{ref}')
        except RuntimeError as e:
            log.append(f'  {b}: {e}'); continue
        sugg.append({'branch': b, 'ahead': a, 'behind': behind})
        log.append(f'  {b}: {a} ahead, {behind} behind' + (f' -> suggest merging {b} into {branch}' if a and b not in GUARDED else ''))
    return {'ok': True, 'log': log, 'upToDate': up_to_date, 'pathSha': path[0], 'headSha': head[0], 'suggestions': sugg}


def compare(a, b):
    fetch(a, b)
    ahead, behind = count(f'origin/{b}..origin/{a}'), count(f'origin/{a}..origin/{b}')
    log = [f'{a} vs {b}: {ahead} ahead, {behind} behind']
    cs = commits(f'origin/{b}..origin/{a}', 15)
    log += [f'  + {c["sha"][:7]} {c["subject"]}' for c in cs]
    return {'ok': True, 'log': log, 'ahead': ahead, 'behind': behind, 'commits': cs, 'guarded': b in GUARDED}


def merge(a, b, yes):
    if b in GUARDED:
        return {'ok': False, 'log': [f'refused: {b} is a shared base branch; merge it through a pull request'], 'refused': True}
    if not yes:
        return {'ok': False, 'log': ['merge needs --yes'], 'refused': True}
    fetch(a, b)
    if count(f'origin/{b}..origin/{a}') == 0:
        return {'ok': True, 'log': [f'{b} already has everything on {a}'], 'merged': False}
    wt = tempfile.mkdtemp(prefix='vs-merge-')
    try:
        git('worktree', 'add', '-q', '--detach', wt, f'origin/{b}')
        r = subprocess.run(['git', 'merge', '--no-ff', '--no-edit', '-m', f'merge: {a} into {b} (Vessel Studio sync)', f'origin/{a}'],
                           cwd=wt, text=True, capture_output=True)
        if r.returncode:
            files = git('diff', '--name-only', '--diff-filter=U', cwd=wt, check=False).splitlines()
            git('merge', '--abort', cwd=wt, check=False)
            return {'ok': False, 'conflicts': files,
                    'log': [f'conflicts merging {a} into {b}; nothing pushed:'] + [f'  {f}' for f in files[:20]] +
                           ['ask a Claude session to merge and resolve these by hand']}
        sha = git('rev-parse', 'HEAD', cwd=wt)
        for i in range(4):
            p = subprocess.run(['git', 'push', 'origin', f'HEAD:refs/heads/{b}'], cwd=wt, text=True, capture_output=True)
            if p.returncode == 0:
                break
        else:
            return {'ok': False, 'log': [f'push to {b} failed: {p.stderr.strip()[-300:]}']}
        return {'ok': True, 'merged': True, 'sha': sha, 'log': [f'merged {a} into {b}: {sha[:7]} pushed']}
    finally:
        git('worktree', 'remove', '--force', wt, check=False)
        shutil.rmtree(wt, ignore_errors=True)


def delete(branch, yes, keep=()):
    if branch in GUARDED or branch in keep:
        return {'ok': False, 'refused': True, 'log': [f'refused: {branch} cannot be deleted from the panel']}
    if not yes:
        return {'ok': False, 'refused': True, 'log': ['delete needs --yes']}
    r = subprocess.run(['git', 'push', 'origin', '--delete', branch], text=True, capture_output=True)
    if r.returncode:
        return {'ok': False, 'log': [f'could not delete {branch}: {r.stderr.strip()[-300:]}']}
    return {'ok': True, 'deleted': True, 'log': [f'deleted {branch} on origin']}


def self_test():
    """Refusals that must hold without touching the remote."""
    bad = []
    if not merge('x', 'bleeding-edge', True).get('refused'): bad.append('merge into a guarded branch was not refused')
    if not merge('x', 'y', False).get('refused'): bad.append('merge without --yes was not refused')
    if not delete('main', True).get('refused'): bad.append('delete of a guarded branch was not refused')
    if not delete('x', False).get('refused'): bad.append('delete without --yes was not refused')
    if not delete('claude/some-session', True, keep={'claude/some-session'}).get('refused'): bad.append('delete of the session branch was not refused')
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('kind', nargs='?', choices=['status', 'compare', 'merge', 'delete'])
    ap.add_argument('--branch'); ap.add_argument('--shown'); ap.add_argument('--from', dest='src'); ap.add_argument('--to')
    ap.add_argument('--yes', action='store_true'); ap.add_argument('--keep', action='append', default=[],
                    help='a branch that must never be deleted (the session\'s own branch)')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        sys.exit(1 if self_test() else 0)
    try:
        if a.kind == 'status': res = status(a.branch, a.shown)
        elif a.kind == 'compare': res = compare(a.src, a.to)
        elif a.kind == 'merge': res = merge(a.src, a.to, a.yes)
        elif a.kind == 'delete': res = delete(a.branch, a.yes, set(a.keep))
        else: ap.error('a kind or --self-test is required')
    except RuntimeError as e:
        res = {'ok': False, 'log': [str(e)]}
    print(json.dumps(res, indent=1))
    sys.exit(0 if res.get('ok') else 1)
