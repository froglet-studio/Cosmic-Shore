#!/usr/bin/env python3
"""Amoebius's artifact library: bring a claude.ai artifact into the repo so Amoebius shows it.

Amoebius (Port/, Prisma.exe) reads Docs/Artifacts/artifacts.json from its workspace and lists every
entry on its VESSEL STUDIO page, the Vessel Studio first. This script is the ONE writer of that
catalog from a Claude session; the /amoebius-artifact skill drives it.

    # 1. a session saves the artifact's files (Artifact list scope=files, then Artifact read paths=[...] out_dir=<dir>)
    # 2. then:
    python3 Tools/Build/amoebius_artifacts.py import --src <dir> --url https://claude.ai/artifact/<id> \
            [--title T] [--id slug] [--summary S] [--group G] [--version V] [--dry-run] [--prune]
    python3 Tools/Build/amoebius_artifacts.py import --src page.html --url ...   # a single downloaded page
    python3 Tools/Build/amoebius_artifacts.py list
    python3 Tools/Build/amoebius_artifacts.py check          # the gate: structure, paths, entry pages, local links
    python3 Tools/Build/amoebius_artifacts.py --self-test    # plants every defect check() must name

An artifact the catalog already has (same url) is UPDATED in its own folder; a new one gets
Docs/Artifacts/<id>/. Per entry:
  unwrap  the artifact service wraps a page that has no <!doctype> in its own skeleton; unwrap=true
          takes it off again (the Vessel Studio: its repo pages are the source and carry no skeleton).
  strip   tags a publish INJECTS into every page (the Vessel Studio's Sync panel tag); removed again.
  skip    publish-time files that are not source (the Vessel Studio's build.json); never written, but
          a build.json's branch/sha is kept as the entry's publishedFrom.
Files the artifact no longer has are reported, and deleted only with --prune. Nothing outside the
entry's folder is ever written, and a file name with '..', a drive or a leading '/' is refused.
"""
import argparse, datetime, hashlib, json, os, re, shutil, sys, tempfile

CATALOG = 'Docs/Artifacts/artifacts.json'
LIB_DIR = 'Docs/Artifacts'
URL_RE = re.compile(r'^https://claude\.ai/(artifact/[A-Za-z0-9]+|code/artifact/[0-9a-fA-F-]{36})$')
ID_RE = re.compile(r'^[a-z0-9][a-z0-9-]{0,62}$')
NAME_RE = re.compile(r'^[A-Za-z0-9._ -]+(/[A-Za-z0-9._ -]+)*$')
# The artifact service's page skeleton, added around a page that has no doctype of its own.
WRAP_HEAD = re.compile(r'^<!doctype html><html><head><meta charset=utf8><meta name=viewport[^>]*><style>:root\{color-scheme:light;.*?</style></head><body>\n?', re.S)
WRAP_TAIL = re.compile(r'\n?</body></html>\s*$')
LOCAL_REF = re.compile(r'''<(?:script|link|img|a|iframe|source)\b[^>]*?\b(?:src|href)\s*=\s*["']([^"'#?]+)''', re.I)


def sha(b):
    return hashlib.sha256(b).hexdigest()


def slug(text):
    s = re.sub(r'[^a-z0-9]+', '-', (text or '').lower()).strip('-')
    return s[:62] or 'artifact'


def safe_name(name):
    """A file name inside an entry's folder: relative, no '..', no drive, plain characters."""
    return bool(name) and NAME_RE.match(name) is not None and '..' not in name.split('/') and not name.startswith('/') and ':' not in name


def safe_dir(d):
    return bool(d) and d.startswith('Docs/') and safe_name(d)


def unwrap(html):
    m = WRAP_HEAD.match(html)
    if not m:
        return html
    return WRAP_TAIL.sub('', html[m.end():], count=1)


def strip_tag(html, tag):
    i = html.rfind(tag)
    return html if i < 0 else html[:i] + html[i + len(tag):]


def page_title(html):
    m = re.search(r'<title>([^<]{1,120})</title>', html, re.I)
    return m.group(1).strip() if m else None


def load_catalog(root):
    p = os.path.join(root, CATALOG)
    if not os.path.exists(p):
        return {'comment': '', 'artifacts': []}
    with open(p, encoding='utf-8') as fh:
        return json.load(fh)


def save_catalog(root, cat):
    p = os.path.join(root, CATALOG)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(cat, fh, indent=2, ensure_ascii=False)
        fh.write('\n')


def read_source(src):
    """{name: bytes} from a folder (recursively) or one .html file (it becomes index.html)."""
    out = {}
    if os.path.isfile(src):
        out['index.html'] = open(src, 'rb').read()
        return out
    for base, _, files in os.walk(src):
        for f in files:
            full = os.path.join(base, f)
            out[os.path.relpath(full, src).replace(os.sep, '/')] = open(full, 'rb').read()
    return out


def do_import(root, src, url, title=None, id_=None, summary=None, group=None, version=None, entry=None,
              dry=False, prune=False, today=None):
    """Returns (report dict, errors list). Writes nothing when there are errors or dry is set."""
    errs = []
    if not URL_RE.match(url or ''):
        return None, [f'url: not a claude.ai artifact link: {url!r}']
    files = read_source(src)
    if not files:
        return None, [f'src: no files in {src}']
    bad = [n for n in files if not safe_name(n)]
    if bad:
        return None, [f'file name refused (outside the folder or odd characters): {n}' for n in bad]

    cat = load_catalog(root)
    arts = cat.setdefault('artifacts', [])
    e = next((a for a in arts if a.get('url') == url), None)
    new = e is None
    if new:
        page = files.get(entry or 'index.html') or b''
        t = title or page_title(page.decode('utf-8', 'replace')) or 'Artifact'
        nid = id_ or slug(t)
        if any(a.get('id') == nid for a in arts):
            return None, [f'id {nid!r} is taken by another artifact; pass --id']
        e = {'id': nid, 'title': t, 'url': url, 'dir': f'{LIB_DIR}/{nid}', 'entry': entry or 'index.html',
             'group': group or 'Artifacts', 'summary': summary or ''}
    else:
        for k, v in (('title', title), ('summary', summary), ('group', group), ('entry', entry)):
            if v:
                e[k] = v
    if not ID_RE.match(e.get('id', '')):
        errs.append(f'id {e.get("id")!r}: lowercase letters, digits and dashes only')
    if not safe_dir(e.get('dir', '')):
        errs.append(f'dir {e.get("dir")!r}: must be a folder under Docs/')
    if e['entry'] not in files:
        errs.append(f'entry page {e["entry"]!r} is not among the artifact files ({", ".join(sorted(files))})')
    if errs:
        return None, errs

    skip, strip, do_unwrap = set(e.get('skip', [])), e.get('strip', []), bool(e.get('unwrap'))
    target = os.path.join(root, e['dir'])
    report = {'id': e['id'], 'dir': e['dir'], 'new': new, 'added': [], 'changed': [], 'unchanged': [], 'skipped': [],
              'removedUpstream': [], 'pruned': []}
    hashes, writes = {}, {}
    for name, data in sorted(files.items()):
        if name in skip:
            report['skipped'].append(name)
            if name == 'build.json':
                try:
                    b = json.loads(data.decode('utf-8'))
                    e['publishedFrom'] = {k: b.get(k) for k in ('branch', 'sha', 'subject') if b.get(k)}
                except ValueError:
                    pass
            continue
        if name.lower().endswith(('.html', '.htm')):
            text = data.decode('utf-8')
            if do_unwrap:
                text = unwrap(text)
            for tag in strip:
                text = strip_tag(text, tag)
            data = text.encode('utf-8')
        dest = os.path.join(target, name)
        old = open(dest, 'rb').read() if os.path.exists(dest) else None
        report['added' if old is None else 'changed' if old != data else 'unchanged'].append(name)
        hashes[name] = sha(data)
        if old != data:
            writes[dest] = data
    previous = set((e.get('files') or {}).keys())
    report['removedUpstream'] = sorted(previous - set(files) - skip)
    if dry:
        return report, []
    for dest, data in writes.items():
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, 'wb') as fh:
            fh.write(data)
    if prune:
        for name in report['removedUpstream']:
            p = os.path.join(target, name)
            if os.path.exists(p):
                os.remove(p)
                report['pruned'].append(name)
    e['files'] = hashes if not report['removedUpstream'] or prune else {**{n: (e.get('files') or {}).get(n) for n in report['removedUpstream']}, **hashes}
    if version:
        e['version'] = version
    e['importedAt'] = today or datetime.date.today().isoformat()
    if new:
        arts.append(e)
    save_catalog(root, cat)
    return report, []


def check(root, quiet=False):
    """The gate. Fails on what would break Amoebius's page or let a write escape its folder."""
    errs = []
    p = os.path.join(root, CATALOG)
    try:
        cat = json.load(open(p, encoding='utf-8'))
    except FileNotFoundError:
        return [f'{CATALOG}: missing']
    except ValueError as ex:
        return [f'{CATALOG}: not valid JSON: {ex}']
    seen_id, seen_url = set(), set()
    for i, a in enumerate(cat.get('artifacts', [])):
        tag = a.get('id') or f'#{i}'
        for k in ('id', 'title', 'url', 'dir', 'entry'):
            if not a.get(k):
                errs.append(f'{tag}: no {k}')
        if a.get('id'):
            if not ID_RE.match(a['id']):
                errs.append(f'{tag}: id must be lowercase letters, digits and dashes')
            if a['id'] in seen_id:
                errs.append(f'{tag}: id used twice')
            seen_id.add(a['id'])
        if a.get('url'):
            if not URL_RE.match(a['url']):
                errs.append(f'{tag}: url is not a claude.ai artifact link')
            if a['url'] in seen_url:
                errs.append(f'{tag}: url used by two entries')
            seen_url.add(a['url'])
        d = a.get('dir', '')
        if d and not safe_dir(d):
            errs.append(f'{tag}: dir {d!r} must be a folder under Docs/')
            continue
        if a.get('entry') and not safe_name(a['entry']):
            errs.append(f'{tag}: entry {a["entry"]!r} must be a file inside dir')
            continue
        page = os.path.join(root, d, a.get('entry', ''))
        if d and a.get('entry') and not os.path.isfile(page):
            errs.append(f'{tag}: entry page {d}/{a["entry"]} is missing')
            continue
        for name in (a.get('files') or {}):
            if not safe_name(name):
                errs.append(f'{tag}: file {name!r} outside its folder')
        # every page's local scripts/styles must be there (a missing studio-look.js is a blank stage in Amoebius)
        injected = {re.search(r'src="([^"]+)"', t).group(1) for t in a.get('strip', []) if re.search(r'src="([^"]+)"', t)}
        for name in [a.get('entry')] + [n for n in (a.get('files') or {}) if n.lower().endswith(('.html', '.htm'))]:
            fp = os.path.join(root, d, name or '')
            if not os.path.isfile(fp):
                continue
            for ref in LOCAL_REF.findall(open(fp, encoding='utf-8', errors='replace').read()):
                if re.match(r'^[a-z]+:', ref, re.I) or ref.startswith('//') or ref.startswith('/') or ref in injected:
                    continue
                if not os.path.exists(os.path.normpath(os.path.join(os.path.dirname(fp), ref))):
                    errs.append(f'{tag}: {name} loads {ref}, which is not in {d}')
    if not quiet:
        for e in errs:
            print('FAIL', e)
        if not errs:
            print(f'check: ok ({len(seen_id)} artifacts)')
    return errs


def drift(root):
    """Files edited in the repo since their import (normal for a repo-sourced entry; news for an imported one)."""
    out = []
    for a in load_catalog(root).get('artifacts', []):
        for name, h in (a.get('files') or {}).items():
            fp = os.path.join(root, a.get('dir', ''), name)
            if h and os.path.isfile(fp) and sha(open(fp, 'rb').read()) != h:
                out.append(f'{a["id"]}: {name} changed in the repo since import')
    return out


def print_report(r):
    tag = 'NEW' if r['new'] else 'UPDATE'
    print(f'{tag} {r["id"]} -> {r["dir"]}')
    for k in ('added', 'changed', 'unchanged', 'skipped', 'removedUpstream', 'pruned'):
        if r[k]:
            print(f'  {k:16} {", ".join(r[k])}')
    if r['removedUpstream'] and not r['pruned']:
        print('  (the artifact no longer has the removedUpstream files; re-run with --prune to delete them)')


def self_test():
    bad = []
    wrapped = ('<!doctype html><html><head><meta charset=utf8><meta name=viewport content="x"><style>:root{color-scheme:light;a:b}'
               '[hidden]{x}</style></head><body>\n<title>T</title>\n<p>x</p>\n<script src="sync.js"></script>\n</body></html>')
    if strip_tag(unwrap(wrapped), '<script src="sync.js"></script>') != '<title>T</title>\n<p>x</p>\n':
        bad.append('unwrap + strip did not give back the source page')
    own = '<!doctype html>\n<html><body>own</body></html>'
    if unwrap(own) != own:
        bad.append('unwrap touched a page with its own doctype')
    for n, ok in (('a.html', True), ('sub/b.js', True), ('../x', False), ('/etc/passwd', False), ('C:/x', False), ('a/../../b', False), ('', False)):
        if safe_name(n) != ok:
            bad.append(f'safe_name({n!r}) should be {ok}')
    with tempfile.TemporaryDirectory() as root, tempfile.TemporaryDirectory() as src:
        w = lambda p, t: (os.makedirs(os.path.dirname(p), exist_ok=True), open(p, 'w', encoding='utf-8').write(t))
        url = 'https://claude.ai/artifact/AbC123'
        w(os.path.join(src, 'index.html'), '<title>My Tool</title><script src="app.js"></script>')
        w(os.path.join(src, 'app.js'), '1')
        r, e = do_import(root, src, url, today='2026-01-01')
        if e or r['added'] != ['app.js', 'index.html'] or not os.path.isfile(os.path.join(root, LIB_DIR, 'my-tool', 'index.html')):
            bad.append(f'new import: {e or r}')
        os.remove(os.path.join(src, 'app.js'))
        w(os.path.join(src, 'index.html'), '<title>My Tool</title>v2')
        r, e = do_import(root, src, url, version='v2', today='2026-01-02')
        if e or r['changed'] != ['index.html'] or r['removedUpstream'] != ['app.js'] or not os.path.exists(os.path.join(root, LIB_DIR, 'my-tool', 'app.js')):
            bad.append(f'update without --prune: {e or r}')
        r, e = do_import(root, src, url, prune=True)
        if e or r['pruned'] != ['app.js'] or os.path.exists(os.path.join(root, LIB_DIR, 'my-tool', 'app.js')):
            bad.append(f'update with --prune: {e or r}')
        if len(load_catalog(root)['artifacts']) != 1:
            bad.append('an update added a second entry')
        if check(root, quiet=True):
            bad.append('check failed on a good library')
        if do_import(root, src, 'https://evil.example/x')[1] == []:
            bad.append('a non-claude.ai url was accepted')
        r, e = do_import(root, src, 'https://claude.ai/artifact/Other1', title='My Tool')
        if not e or 'taken' not in e[0]:
            bad.append('a second artifact took a used id')
        # plant every defect check() must name
        cat = load_catalog(root)
        cat['artifacts'] += [
            {'id': 'Bad Id', 'title': 't', 'url': 'https://claude.ai/artifact/B1', 'dir': 'Docs/Artifacts/my-tool', 'entry': 'index.html'},
            {'id': 'escape', 'title': 't', 'url': 'https://claude.ai/artifact/B2', 'dir': '../outside', 'entry': 'index.html'},
            {'id': 'nopage', 'title': 't', 'url': 'https://claude.ai/artifact/B3', 'dir': 'Docs/Artifacts/nopage', 'entry': 'index.html'},
            {'id': 'dupurl', 'title': 't', 'url': url, 'dir': 'Docs/Artifacts/my-tool', 'entry': 'index.html'},
            {'id': 'badurl', 'title': 't', 'url': 'http://claude.ai/artifact/B4', 'dir': 'Docs/Artifacts/my-tool', 'entry': 'index.html'},
            {'id': 'nolink', 'title': 't', 'url': 'https://claude.ai/artifact/B5', 'dir': 'Docs/Artifacts/nolink', 'entry': 'index.html'},
        ]
        w(os.path.join(root, 'Docs/Artifacts/nolink/index.html'), '<script src="gone.js"></script><a href="https://x.y/">x</a>')
        save_catalog(root, cat)
        got = ' | '.join(check(root, quiet=True))
        for want in ('Bad Id: id must be', "escape: dir '../outside'", 'nopage: entry page', 'dupurl: url used by two', 'badurl: url is not',
                     'nolink: index.html loads gone.js'):
            if want not in got:
                bad.append('check did not name: ' + want)
        if 'x.y' in got:
            bad.append('check flagged an external link')
    print('self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--self-test', action='store_true')
    ap.add_argument('--root', default='.', help='repo root (default: the current folder)')
    sub = ap.add_subparsers(dest='cmd')
    im = sub.add_parser('import', help='bring an artifact (a folder of its files, or one .html) into the library')
    im.add_argument('--src', required=True)
    im.add_argument('--url', required=True)
    for k in ('title', 'id', 'summary', 'group', 'version', 'entry'):
        im.add_argument('--' + k)
    im.add_argument('--dry-run', action='store_true', help='report what would change, write nothing')
    im.add_argument('--prune', action='store_true', help='delete files the artifact no longer has')
    sub.add_parser('list')
    sub.add_parser('check')
    sub.add_parser('drift', help='files edited in the repo since they were imported')
    a = ap.parse_args()
    if a.self_test:
        return 1 if self_test() else 0
    root = os.path.abspath(a.root)
    if a.cmd == 'import':
        r, e = do_import(root, a.src, a.url, a.title, a.id, a.summary, a.group, a.version, a.entry, a.dry_run, a.prune)
        for x in e:
            print('FAIL', x)
        if e:
            return 1
        print_report(r)
        if a.dry_run:
            print('(dry run: nothing written)')
            return 0
        return 1 if check(root) else 0
    if a.cmd == 'list':
        for x in load_catalog(root).get('artifacts', []):
            print(f'{x["id"]:24} {x.get("group", ""):14} {x.get("version", "-"):22} {x["dir"]}/{x["entry"]}  {x["url"]}')
        return 0
    if a.cmd == 'check':
        return 1 if check(root) else 0
    if a.cmd == 'drift':
        d = drift(root)
        print('\n'.join(d) if d else 'drift: none')
        return 0
    ap.print_help()
    return 2


if __name__ == '__main__':
    sys.exit(main())
