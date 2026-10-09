#!/usr/bin/env python3
"""Assemble the Vessel Studio artifact from one branch, ready for the Artifact tool.

    python3 .claude/skills/vessel-studio/build_artifact.py --ref origin/cece/magical-carson-9bdq8z --out <dir>
    python3 .claude/skills/vessel-studio/build_artifact.py --check <dir>
    python3 .claude/skills/vessel-studio/build_artifact.py --self-test

What it writes into --out (and nothing anywhere else):
  index.html + every studio page listed in studios.json, each with the sync panel
  (<script src="sync.js"></script>) injected before its last </body>;
  studios.json; sync.js (from the ref, else from this working tree); build.json, the record of
  which branch and commit the artifact shows (the panel's Refresh compares against it).

The repo pages are never edited: the panel is added at publish time, here and by Refresh itself,
so a studio branch merges without touching the panel. Then publish:
  Artifact(file_path=<out>/index.html, files={each other file: <out>/<file>}, url=<the artifact>)
  with the capabilities listed in SKILL.md section 4 on the first publish.
"""
import argparse, json, os, subprocess, sys, tempfile

DIR = 'Docs/Studios/VesselStudio'
TAG = '<script src="sync.js"></script>'
REPO = 'froglet-studio/cosmic-shore'
DEFAULT_ARTIFACT = 'https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa'   # THE Vessel Studio: the only artifact (SKILL.md section 0)


def git(*a, quiet=False):
    return subprocess.check_output(['git'] + list(a), text=True, stderr=subprocess.DEVNULL if quiet else None)


def inject(html):
    """Add the panel tag before the LAST </body> (or at the end); never twice."""
    if 'src="sync.js"' in html:
        return html
    i = html.lower().rfind('</body>')
    return html[:i] + TAG + html[i:] if i >= 0 else html + TAG


def branch_name(ref):
    for p in ('refs/remotes/origin/', 'origin/', 'refs/heads/'):
        if ref.startswith(p):
            return ref[len(p):]
    return ref


def build(ref, out, artifact=None, session=None):
    os.makedirs(out, exist_ok=True)
    cat_text = git('show', f'{ref}:{DIR}/studios.json')
    cat = json.loads(cat_text)
    pages = ['index.html'] + [s['file'] for s in cat.get('studios', []) if s.get('file')]
    for f in pages:
        html = git('show', f'{ref}:{DIR}/{f}')
        with open(os.path.join(out, f), 'w', encoding='utf-8') as fh:
            fh.write(inject(html))
    with open(os.path.join(out, 'studios.json'), 'w', encoding='utf-8') as fh:
        fh.write(cat_text)
    try:
        sync = git('show', f'{ref}:{DIR}/sync.js', quiet=True)
    except subprocess.CalledProcessError:
        sync = open(os.path.join(DIR, 'sync.js'), encoding='utf-8').read()
    with open(os.path.join(out, 'sync.js'), 'w', encoding='utf-8') as fh:
        fh.write(sync)
    for f in cat.get('shared', []):   # files every studio loads, e.g. studio-look.js (the default graphics, D19)
        try:
            txt = git('show', f'{ref}:{DIR}/{f}', quiet=True)
        except subprocess.CalledProcessError:
            txt = open(os.path.join(DIR, f), encoding='utf-8').read()
        with open(os.path.join(out, f), 'w', encoding='utf-8') as fh:
            fh.write(txt)
    head = git('rev-parse', ref).strip()
    path_sha = git('log', '-1', '--format=%H', ref, '--', DIR).strip()
    info = {
        'repo': REPO, 'branch': branch_name(ref), 'sha': head, 'pathSha': path_sha,
        'subject': git('log', '-1', '--format=%s', path_sha).strip(),
        'committedAt': git('log', '-1', '--format=%cI', path_sha).strip(),
        'publishedAt': None,
        'artifact': artifact or DEFAULT_ARTIFACT,   # the panel names it in each job
        'session': session,                        # default Claude session for jobs (each viewer can set their own)
    }
    with open(os.path.join(out, 'build.json'), 'w', encoding='utf-8') as fh:
        json.dump(info, fh, indent=2)
    print(f'built {out}: {", ".join(pages)} from {info["branch"]} @ {path_sha[:7]} "{info["subject"]}"')
    return check(out)


def check(out):
    """Every page carries the panel once, build.json names a commit, sync.js is ASCII."""
    errs = []
    try:
        cat = json.load(open(os.path.join(out, 'studios.json'), encoding='utf-8'))
    except Exception as e:
        return [f'studios.json: {e}']
    for f in ['index.html'] + [s['file'] for s in cat.get('studios', []) if s.get('file')]:
        p = os.path.join(out, f)
        if not os.path.exists(p):
            errs.append(f'{f}: missing'); continue
        n = open(p, encoding='utf-8').read().count('src="sync.js"')
        if n != 1:
            errs.append(f'{f}: sync panel tag {n} times (want 1)')
    try:
        b = json.load(open(os.path.join(out, 'build.json'), encoding='utf-8'))
        for k in ('repo', 'branch', 'sha', 'pathSha', 'subject', 'committedAt'):
            if not b.get(k):
                errs.append(f'build.json: no {k}')
    except Exception as e:
        errs.append(f'build.json: {e}')
    for f in ['sync.js'] + list(cat.get('shared', [])):
        try:
            raw = open(os.path.join(out, f), 'rb').read()
            if any(c > 127 for c in raw):
                errs.append(f'{f}: non-ASCII bytes (a page served without a charset garbles them; escape as \\uXXXX)')
        except OSError:
            errs.append(f'{f}: missing')
    for e in errs:
        print('FAIL', e)
    if not errs:
        print('check: ok')
    return errs


def self_test():
    """Plant each defect check() must name, and prove inject() on the awkward shapes."""
    bad = []
    if inject('<html><body>a</body></html>') != '<html><body>a' + TAG + '</body></html>':
        bad.append('inject: plain page')
    if inject('<body>x</body><!-- </body> --></body>').count(TAG) != 1 or not inject('<body>x</body></body>').endswith(TAG + '</body>'):
        bad.append('inject: must use the LAST </body>')
    if inject('no body') != 'no body' + TAG:
        bad.append('inject: page with no </body>')
    once = inject('<body></body>')
    if inject(once) != once:
        bad.append('inject: injected twice')
    with tempfile.TemporaryDirectory() as d:
        w = lambda f, t: open(os.path.join(d, f), 'w', encoding='utf-8').write(t)
        w('studios.json', json.dumps({'studios': [{'file': 'a.html'}, {'file': 'b.html'}], 'shared': ['look.js']}))   # planted: look.js missing
        w('index.html', '<body>' + TAG + '</body>')
        w('a.html', '<body></body>')                                   # planted: no panel
        w('b.html', '<body>' + TAG + TAG + '</body>')                  # planted: panel twice
        w('build.json', json.dumps({'repo': 'r', 'branch': 'b', 'sha': 's', 'pathSha': '', 'subject': 's', 'committedAt': 't'}))  # planted: no pathSha
        open(os.path.join(d, 'sync.js'), 'wb').write('// ⇄\n'.encode('utf-8'))  # planted: non-ASCII
        got = ' | '.join(check(d))
        for want in ('look.js: missing', 'a.html: sync panel tag 0', 'b.html: sync panel tag 2', 'build.json: no pathSha', 'sync.js: non-ASCII'):
            if want not in got:
                bad.append('check did not name: ' + want)
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--ref', help='git ref to build from, e.g. origin/cece/magical-carson-9bdq8z')
    ap.add_argument('--out', help='output folder (use your scratchpad)')
    ap.add_argument('--artifact', help='the artifact URL this build is published to (default: the live Vessel Studio)')
    ap.add_argument('--session', help='default Claude session id for Sync jobs (the publisher\'s own)')
    ap.add_argument('--check', metavar='DIR', help='check an already built folder')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        sys.exit(1 if self_test() else 0)
    if a.check:
        sys.exit(1 if check(a.check) else 0)
    if not (a.ref and a.out):
        ap.error('--ref and --out are required (or --check / --self-test)')
    sys.exit(1 if build(a.ref, a.out, a.artifact, a.session) else 0)
