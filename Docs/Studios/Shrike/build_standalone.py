#!/usr/bin/env python3
"""build_standalone.py - the Shrike Studio as its OWN artifact (/labmaker section 6), built from the repo page.

The repo page (Docs/Studios/VesselStudio/shrike.html) is the source. This writes a folder that publishes as a
standalone claude.ai artifact: the page without the Vessel Studio hub's pieces (the hub's Sync panel tag, the back
link and the "ask the studio agent" link, which point at the hub) and without the document skeleton the Artifact tool
adds itself, plus the shared scripts the page loads, copied beside it unchanged.

    python3 Docs/Studios/Shrike/build_standalone.py --out <dir>       # writes <dir>/shrike.html + the scripts
    python3 Docs/Studios/Shrike/build_standalone.py --self-test

Publish: Artifact file_path=<dir>/shrike.html, files={name: <dir>/name for each script}, capabilities
{"db": {}, "user": {}} (the decision log), and the artifact's url once it exists (Docs/Studios/SHRIKE.md).
"""
import argparse, os, re, shutil, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
STUDIO = os.path.normpath(os.path.join(HERE, '..', 'VesselStudio'))
PAGE = os.path.join(STUDIO, 'shrike.html')

# (what, pattern, replacement, required)
EDITS = [
    ('doctype', r'\A<!doctype html>\s*', '', True),
    ('html open', r'<html lang="en">\s*', '', True),
    ('charset', r'<meta charset="utf-8">\s*', '', True),
    ('viewport', r'<meta name="viewport"[^>]*>\s*', '', True),
    ('html close', r'</html>\s*', '', True),
    ('hub sync panel', r'<script src="sync\.js"></script>\s*', '', True),
    ('back link', r'<a class="back" href="index\.html">[^<]*</a>\s*', '', True),
    ('studio agent link', r'<p class="note">Want a change in this studio or a design tried\? <a href="index\.html#agent">[^<]*</a>\.</p>\s*', '', True),
]


def scripts(html):
    return [s for s in re.findall(r'<script src="([^"/:]+\.js)"></script>', html)]


def build(src_html, out_dir, studio_dir=STUDIO):
    html = src_html
    for what, pat, rep, required in EDITS:
        html, n = re.subn(pat, rep, html, count=1)
        if required and n != 1:
            raise SystemExit('build_standalone: ' + what + ' not found in the page (the page changed: update EDITS)')
    if 'index.html' in html:
        raise SystemExit('build_standalone: the page still links to the hub (index.html)')
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, 'shrike.html'), 'w', encoding='utf-8') as f:
        f.write(html)
    names = scripts(html)
    for n in names:
        shutil.copyfile(os.path.join(studio_dir, n), os.path.join(out_dir, n))
    return names


def self_test():
    src = open(PAGE, encoding='utf-8').read()
    bad = []
    with tempfile.TemporaryDirectory() as d:
        names = build(src, d)
        out = open(os.path.join(d, 'shrike.html'), encoding='utf-8').read()
        if 'sync.js' in out or 'sync.js' in names: bad.append('sync.js survived')
        if out.lower().startswith('<!doctype'): bad.append('skeleton survived')
        if '<title>Shrike Studio</title>' not in out[:8192]: bad.append('title not in the first 8 KB')
        for n in ['studio-theme.js', 'studio-look.js', 'studio-domains.js', 'studio-ide.js', 'ai_race_panel.js']:
            if n not in names: bad.append(n + ' not copied')
    # negative control: a page whose hub link moved must refuse to build, not ship a dead link
    try:
        with tempfile.TemporaryDirectory() as d:
            build(src.replace('<a class="back" href="index.html">', '<a class="back" href="./index.html">'), d)
        bad.append('a changed back link built anyway')
    except SystemExit:
        pass
    print('build_standalone self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return 0 if not bad else 1


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--out')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    if not a.out:
        ap.error('--out is required')
    names = build(open(PAGE, encoding='utf-8').read(), a.out)
    print('build_standalone: wrote ' + os.path.join(a.out, 'shrike.html') + ' + ' + ', '.join(names))
