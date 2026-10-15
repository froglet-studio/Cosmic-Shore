#!/usr/bin/env python3
"""build.py - the Regatta AI artifact's publish folder (its OWN artifact, separate from the Vessel Studio).

    python3 Docs/Studios/RegattaAI/build.py --out <dir>      # index.html + course.js + the shared Vessel Studio files it loads
    python3 Docs/Studios/RegattaAI/build.py --check          # the page's scripts exist, served JS is ASCII, course.js is current

The page loads the Vessel Studio's shared look and panels as ../VesselStudio/<file> so the repo copy runs from the
checkout; an artifact serves flat paths, so the build copies those files beside it and drops the '../VesselStudio/'
prefix. Nothing else changes: the published page is the repo page.
"""
import argparse, os, re, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
SHARED = os.path.join(HERE, '..', 'VesselStudio')
PREFIX = '../VesselStudio/'


def scripts(html):
    return re.findall(r'<script src="([^"]+)"', html)


def check(html):
    errors = []
    for src in scripts(html):
        if src.startswith('http'):
            continue
        path = os.path.join(SHARED, src[len(PREFIX):]) if src.startswith(PREFIX) else os.path.join(HERE, src)
        if not os.path.exists(path):
            errors.append('missing script ' + src)
        elif path.endswith('.js') and any(b > 127 for b in open(path, 'rb').read()):
            errors.append(src + ' has non-ASCII bytes (served without a charset)')
    return errors


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out')
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args()
    html = open(os.path.join(HERE, 'index.html'), encoding='utf-8').read()
    errors = check(html)
    if a.check:
        r = subprocess.run(['bash', os.path.join(HERE, 'export_course.sh'), '--check'], capture_output=True, text=True)
        print(r.stdout.strip() or r.stderr.strip()[-300:])
        if r.returncode:
            errors.append('course.js is stale or export_course.sh failed')
        r = subprocess.run([sys.executable, os.path.join(HERE, 'export_fleet.py'), '--check'], capture_output=True, text=True)
        print(r.stdout.strip() or r.stderr.strip()[-300:])
        if r.returncode:
            errors.append('fleet.js is stale (run export_fleet.py)')
    for e in errors:
        print('FAIL ' + e)
    if errors:
        sys.exit(1)
    if a.out:
        os.makedirs(a.out, exist_ok=True)
        for src in scripts(html):
            if src.startswith(PREFIX):
                shutil.copy(os.path.join(SHARED, src[len(PREFIX):]), os.path.join(a.out, src[len(PREFIX):]))
            elif not src.startswith('http'):
                shutil.copy(os.path.join(HERE, src), os.path.join(a.out, src))
        with open(os.path.join(a.out, 'index.html'), 'w', encoding='utf-8') as fh:
            fh.write(html.replace(PREFIX, ''))
        print('built ' + a.out + ': ' + ', '.join(sorted(os.listdir(a.out))))
    if not a.out and not a.check:
        print('ok (nothing to do: pass --out or --check)')


if __name__ == '__main__':
    main()
