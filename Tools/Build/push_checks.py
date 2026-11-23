#!/usr/bin/env python3
"""
Pre-push checks: what GitHub Desktop never asks before a push, asked by the Unity Source Control window
(FrogletTools > Build > Source Control) and runnable from any terminal.

    python3 Tools/Build/push_checks.py                       # check every change: working tree + unpushed commits
    python3 Tools/Build/push_checks.py --paths-from FILE     # check these repo-relative paths (one per line)
    python3 Tools/Build/push_checks.py --json                # machine-readable (the Unity window reads this)
    python3 Tools/Build/push_checks.py --root DIR --base REF # another checkout / another base branch
    python3 Tools/Build/push_checks.py --self-test           # the checks against fixture repos, negative controls included

Every rule lives here and the Unity window only draws the result (the QA Session window's split): a rule can be
self-tested in Python, and a Claude session or a terminal gets the same answer the editor does.

WHY THESE CHECKS: each one is a real push that hurt (Charles's Thresher commit, PR #1766, 2026-11-06):
  behind-base      his branch was 35 merges behind bleeding-edge, so the commit showed 563 files for 39 real ones
  fmod-event       a prefab pointed at an FMOD event saved only in FMOD's .unsaved folder
  junk             .DS_Store, steam_appid.txt, FMOD's .unsaved / .cache, Library/Temp/Logs
  resave-only      three Configure prefabs changed only because Unity re-saved them, and two in-flight UI
                   branches then conflicted on 9-22k lines of nothing
  build-settings   an archived mode's scene was added to the build
  define-symbols   a scripting define was added to a platform
  dynamic-font     TMP filled a dynamic font while he played (8 KB -> 5.4 MB)
plus the classics: a protected branch, a missing / orphaned .meta, conflict markers, files GitHub refuses (>100 MB)
and a new package dependency (CLAUDE.md: flag it first).

Severity: `block` (the push would be refused or broken - the window will not push), `warn` (the window asks
first), `info` (shown). A finding with `fix: "discard"` offers a one-click discard in the window.

A READER: it runs git and reads files; it writes nothing.
"""
import collections
import json
import os
import re
import subprocess
import sys
import tempfile
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
BASE = 'origin/bleeding-edge'
PROTECTED = ('main', 'master', 'bleeding-edge', 'development', 'develop', 'HEAD')
BEHIND_WARN_MERGES = 10
WARN_BYTES = 50 * 1024 * 1024
BLOCK_BYTES = 100 * 1024 * 1024          # GitHub refuses a file over 100 MB
FONT_GROWTH_BYTES = 512 * 1024
URP_ASSET_VERSION_GUID = 'd0353a89b1f911e48b9e16bdc9f2e058'   # the stamp Unity appends to a URP material on save
YAML_EXTS = ('.prefab', '.unity', '.asset', '.mat', '.controller', '.anim', '.overrideController', '.mask',
             '.physicMaterial', '.lighting', '.playable', '.signal', '.preset', '.spriteatlas', '.terrainlayer')
JUNK_NAMES = ('.DS_Store', 'Thumbs.db', 'desktop.ini', 'steam_appid.txt')
JUNK_SUFFIXES = ('.orig', '.rej', '.swp', '~')
JUNK_DIRS = ('Library/', 'Temp/', 'Logs/', 'obj/', 'UserSettings/', 'Builds/', 'MemoryCaptures/', '.vs/', '.idea/')


# ----------------------------------------------------------------------------- git
def git(*args, cwd=ROOT, ok=(0,)):
    p = subprocess.run(['git', '-c', 'core.quotepath=false'] + list(args), cwd=cwd, capture_output=True)
    if p.returncode not in ok:
        return None
    return p.stdout.decode('utf-8', 'replace')


def git_bytes(*args, cwd=ROOT):
    p = subprocess.run(['git'] + list(args), cwd=cwd, capture_output=True)
    return p.stdout if p.returncode == 0 else None


def current_branch(cwd):
    out = git('rev-parse', '--abbrev-ref', 'HEAD', cwd=cwd)
    return (out or '').strip()


def ref_exists(ref, cwd):
    return git('rev-parse', '--verify', '-q', ref + '^{commit}', cwd=cwd) is not None


def status(cwd):
    """{path: two-letter porcelain code} for every change in the working tree (untracked included)."""
    out = git('status', '--porcelain=v1', '-z', '--untracked-files=all', cwd=cwd) or ''
    parts, res, i = out.split('\0'), {}, 0
    while i < len(parts):
        e = parts[i]
        if len(e) < 4:
            i += 1
            continue
        code, path = e[:2], e[3:]
        res[path] = code
        i += 2 if code[0] in 'RC' else 1      # a rename is followed by its source path
    return res


def unpushed(cwd):
    """(paths changed by commits on HEAD that no origin branch has, the commit those changes start from)."""
    commits = (git('rev-list', 'HEAD', '--not', '--remotes=origin', cwd=cwd) or '').split()
    if not commits:
        return [], None
    oldest = commits[-1]
    parent = git('rev-parse', '-q', '--verify', oldest + '^', cwd=cwd)
    start = parent.strip() if parent else None
    if start:
        files = git('diff', '--name-only', '--no-renames', start, 'HEAD', cwd=cwd) or ''
    else:
        files = git('show', '--name-only', '--format=', oldest, cwd=cwd) or ''
    return [f for f in files.splitlines() if f], start


def old_bytes(path, rev, cwd):
    if not rev:
        return None
    return git_bytes('show', f'{rev}:{path}', cwd=cwd)


def new_bytes(path, cwd):
    try:
        with open(os.path.join(cwd, path), 'rb') as fh:
            return fh.read()
    except OSError:
        return None


def text(b):
    return None if b is None else b.decode('utf-8', 'replace')


def is_binary(b):
    return b is not None and b'\0' in b[:8000]


def finding(fid, severity, title, detail='', paths=(), fix=None):
    return {'id': fid, 'severity': severity, 'title': title, 'detail': detail, 'paths': list(paths), 'fix': fix}


# ----------------------------------------------------------------------------- YAML: "only a Unity re-save?"
DOC_RE = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?\s*$')
KEY_LINE = re.compile(r'^\s*[A-Za-z_][\w.\[\]]*:(\s|$)')


def _open_quote(line):
    """The quote a `key: '...` / `key: "...` value leaves open at the end of this line, else None. Unity writes
    a long or multi-line string as a quoted scalar that continues on the following lines."""
    m = re.match(r'^\s*(?:- )?[\w.\[\]]+: ([\'"])(.*)$', line)
    if not m:
        return None
    q, rest = m.group(1), m.group(2)
    return q if not _closes(q, rest) else None


def _closes(q, text):
    """Does `text` (after an opening quote) close it? '' is an escaped single quote, \\" an escaped double one."""
    if q == "'":
        return text.replace("''", '').count("'") % 2 == 1
    return re.sub(r'\\.', '', text).count('"') % 2 == 1


def unwrap(lines):
    """Join what Unity wraps back onto one line: flow mappings (`{fileID: 1, guid: x,\\n    type: 3}`) and quoted
    strings that run onto the next lines."""
    out, buf, mode = [], None, None
    for line in lines:
        if buf is not None:
            buf += ' ' + line.strip()
            if (mode == '{' and buf.count('{') <= buf.count('}')) or (mode in ("'", '"') and _closes(mode, line)):
                out.append(buf)
                buf = mode = None
            continue
        if line.count('{') > line.count('}'):
            buf, mode = line, '{'
            continue
        q = _open_quote(line)
        if q:
            buf, mode = line, q
            continue
        out.append(line)
    if buf is not None:
        out.append(buf)
    return out


def docs_of(txt):
    """{fileID: (class id, stripped, [lines])} of a Unity YAML file."""
    docs, cur = {}, None
    for line in unwrap(txt.split('\n')):
        m = DOC_RE.match(line)
        if m:
            cur = m.group(2)
            docs[cur] = (m.group(1), bool(m.group(3)), [])
            continue
        if cur is not None and line.strip():
            docs[cur][2].append(line.rstrip())
    return docs


def stripped_key(doc):
    cls, _, lines = doc
    src = next((l.strip() for l in lines if 'm_CorrespondingSourceObject' in l), '')
    inst = next((l.strip() for l in lines if l.strip().startswith('m_PrefabInstance')), '')
    return cls, src, inst


def mod_entries(lines):
    """Split a doc's lines into ordinary lines and m_Modifications entries (each a tuple of its lines), so a
    re-ordered override list compares equal."""
    plain, entries, cur, in_mods = [], [], None, False
    for line in lines:
        s = line.strip()
        if s == 'm_Modifications:':
            in_mods = True
            plain.append(line)
            continue
        if in_mods:
            if s.startswith('- target:'):
                if cur:
                    entries.append(tuple(cur))
                cur = [s]
                continue
            if cur is not None and line.startswith('      '):
                cur.append(s)
                continue
            if cur:
                entries.append(tuple(cur))
            cur, in_mods = None, False
        plain.append(line)
    if cur:
        entries.append(tuple(cur))
    return plain, entries


def resave_only(old_txt, new_txt):
    """True when new differs from old only the way a Unity re-save differs: wrapped lines re-wrapped, documents and
    override rows re-ordered, nested-prefab stub fileIDs recomputed, fields Unity had not serialized yet written
    out, and URP's material version stamp added. A changed or removed value, a new list element (a child, a
    component, an override) or a new or deleted object is a real change."""
    if old_txt is None or new_txt is None or old_txt == new_txt:
        return False
    if not old_txt.startswith('%YAML') or not new_txt.startswith('%YAML'):
        return False
    old, new = docs_of(old_txt), docs_of(new_txt)
    if not old or not new:
        return False                           # not a Unity object file: nothing to judge a re-save by
    # Nested-prefab stubs: Unity derives their fileID; pair them by what they stand for, not by their id.
    by_key = {stripped_key(d): fid for fid, d in new.items() if d[1]}
    remap = {}
    for fid, d in old.items():
        if d[1] and fid not in new:
            n = by_key.get(stripped_key(d))
            if n is None:
                return False
            remap[fid] = n
    if remap:
        pat = re.compile(r'(?<![\d-])(' + '|'.join(re.escape(k) for k in remap) + r')(?!\d)')
        old = {remap.get(fid, fid): (c, s, [pat.sub(lambda m: remap[m.group(1)], l) for l in ls])
               for fid, (c, s, ls) in old.items()}
    added = set(new) - set(old)
    if set(old) - set(new):
        return False
    for fid in added:
        if f'guid: {URP_ASSET_VERSION_GUID}' not in ' '.join(new[fid][2]):
            return False
    for fid, (cls, _, lines) in old.items():
        ncls, _, nlines = new[fid]
        if ncls != cls:
            return False
        oplain, omods = mod_entries(lines)
        nplain, nmods = mod_entries(nlines)
        if collections.Counter(omods) != collections.Counter(nmods):
            return False
        extra = collections.Counter(nplain)
        extra.subtract(collections.Counter(oplain))
        if any(v < 0 for v in extra.values()):
            return False                       # an old line is gone: a value changed or was removed
        for line, n in extra.items():
            if n > 0 and not KEY_LINE.match(line):
                return False                   # a new list element, not a newly written field
    return True


# ----------------------------------------------------------------------------- FMOD
GUID_BLOCK = re.compile(r'Guid:\s*\n\s*Data1: (-?\d+)\s*\n\s*Data2: (-?\d+)\s*\n\s*Data3: (-?\d+)\s*\n\s*Data4: (-?\d+)'
                        r'(?:\s*\n\s*Path: ([^\n]*))?')


def event_refs(txt):
    """{guid: path} of every FMOD EventReference in a Unity YAML file (the zero guid of an empty slot excluded)."""
    out = {}
    for m in GUID_BLOCK.finditer(txt or ''):
        vals = [int(v) for v in m.groups()[:4]]
        if not any(vals):
            continue
        b = b''.join(v.to_bytes(4, 'little', signed=True) for v in vals)
        out[str(uuid.UUID(bytes_le=b))] = (m.group(5) or '').strip()
    return out


def fmod_projects(cwd):
    """Folders (repo-relative) that hold an FMOD Studio project (*.fspro), searched two levels down."""
    found = []
    for top in sorted(os.listdir(cwd)):
        d = os.path.join(cwd, top)
        if not os.path.isdir(d) or top.startswith('.') or top in ('Assets', 'Library', 'Packages', 'Temp', 'Logs'):
            continue
        if any(f.endswith('.fspro') for f in os.listdir(d)):
            found.append(top)
    return found


def fmod_event_home(guid, cwd, projects):
    """'saved', 'unsaved' (only in FMOD's .unsaved folder: never saved in FMOD Studio) or None (not in the project)."""
    for proj in projects:
        if os.path.isfile(os.path.join(cwd, proj, 'Metadata', 'Event', '{%s}.xml' % guid)):
            return 'saved'
    for proj in projects:
        if os.path.isfile(os.path.join(cwd, proj, '.unsaved', 'Metadata', 'Event', '{%s}.xml' % guid)):
            return 'unsaved'
    return None


# ----------------------------------------------------------------------------- the checks
def junk_kind(path, projects):
    name = path.rsplit('/', 1)[-1]
    if name in JUNK_NAMES:
        return 'steam_appid.txt is written by Steamworks.NET every time the editor starts Steam' \
            if name == 'steam_appid.txt' else 'an operating-system file'
    if name.endswith(JUNK_SUFFIXES):
        return 'a merge or editor leftover'
    if any(path.startswith(d) for d in JUNK_DIRS):
        return 'a folder Unity or an IDE regenerates on every machine'
    for proj in projects:
        if path.startswith(proj + '/.unsaved/'):
            return "FMOD Studio's unsaved-changes folder: save the FMOD project instead"
        if path.startswith(proj + '/.cache/'):
            return "FMOD Studio's build cache: it regenerates when banks are built"
    return None


def scene_list(txt):
    return [(m.group(2), m.group(1) == '1') for m in
            re.finditer(r'- enabled: (\d)\s*\n\s*path: ([^\n]+)', txt or '')]


def defines(txt):
    m = re.search(r'scriptingDefineSymbols:\s*\n((?:    [^\n]*\n)*)', txt or '')
    out = {}
    for line in (m.group(1) if m else '').splitlines():
        if ':' in line:
            k, v = line.strip().split(':', 1)
            out[k.strip()] = set(x for x in v.strip().split(';') if x)
    return out


def manifest_deps(txt):
    try:
        return json.loads(txt or '{}').get('dependencies', {})
    except ValueError:
        return {}


def run_checks(cwd=ROOT, paths=None, base=BASE, in_flight=None):
    """Every check. `paths`: the working-tree paths about to be committed (None = every change). The paths
    unpushed commits changed are always checked too. `in_flight(paths) -> [{branch, people}]` is injectable
    for the self-test; by default it is who_works_where.py's."""
    branch = current_branch(cwd)
    st = status(cwd)
    work = sorted(st) if paths is None else sorted(set(p for p in paths if p))
    pushed_paths, push_start = unpushed(cwd)
    findings = []

    # -- the branch
    if branch in PROTECTED or branch.startswith('release/'):
        findings.append(finding('protected-branch', 'block', f'You are on {branch}',
                                'Nobody pushes to this branch directly: make a branch (Branches tab > New branch) and '
                                'push that; it reaches bleeding-edge through a pull request.'))
    behind = None
    if ref_exists(base, cwd):
        merges = int((git('rev-list', '--count', '--merges', f'HEAD..{base}', cwd=cwd) or '0').strip() or 0)
        commits = int((git('rev-list', '--count', f'HEAD..{base}', cwd=cwd) or '0').strip() or 0)
        behind = {'merges': merges, 'commits': commits}
        if merges >= BEHIND_WARN_MERGES:
            findings.append(finding('behind-base', 'warn', f'{merges} pull requests behind {base.split("/")[-1]}',
                                    'Update from bleeding-edge before you push. A branch this far behind makes the pull '
                                    'request show every file the others changed meanwhile, and conflicts surface late.'))
        elif merges:
            findings.append(finding('behind-base', 'info', f'{merges} pull request(s) behind {base.split("/")[-1]}',
                                    'Updating from bleeding-edge before the pull request keeps it small.'))

    # -- each path: (path, old content rev, is a working-tree change)
    items = [(p, 'HEAD', True) for p in work]
    items += [(p, push_start, False) for p in pushed_paths if p not in work]
    projects = fmod_projects(cwd)
    junk, resaves, fonts, big, blocked_big, markers = [], [], [], [], [], []
    fmod_unsaved, fmod_missing = {}, {}
    for path, rev, working in items:
        code = st.get(path, '')
        deleted = 'D' in code if working else not os.path.exists(os.path.join(cwd, path))
        kind = junk_kind(path, projects)
        if kind:
            junk.append((path, kind))
            continue
        if deleted:
            continue
        nb = new_bytes(path, cwd)
        if nb is None:
            continue
        size = len(nb)
        if size > BLOCK_BYTES:
            blocked_big.append(path)
        elif size > WARN_BYTES:
            big.append(path)
        if is_binary(nb):
            continue
        ob = old_bytes(path, rev, cwd) if code != '??' else None
        nt, ot = text(nb), text(ob)
        if re.search(r'^<<<<<<< |^>>>>>>> ', nt, re.M) and re.search(r'^=======\s*$', nt, re.M):
            markers.append(path)
        if path.endswith(YAML_EXTS):
            if resave_only(ot, nt):
                resaves.append(path)
            if path.endswith('.asset') and 'm_AtlasPopulationMode: 1' in nt and size - len(ob or b'') > FONT_GROWTH_BYTES:
                fonts.append(path)
            if projects:
                for guid, epath in event_refs(nt).items():
                    if guid in event_refs(ot):
                        continue
                    home = fmod_event_home(guid, cwd, projects)
                    if home == 'unsaved':
                        fmod_unsaved.setdefault(epath or guid, []).append(path)
                    elif home is None:
                        fmod_missing.setdefault(epath or guid, []).append(path)
        if path == 'ProjectSettings/EditorBuildSettings.asset':
            o, n = scene_list(ot), scene_list(nt)
            added = [p for p, e in n if e and (p, True) not in o]
            removed = [p for p, e in o if e and (p, True) not in n]
            if added or removed:
                findings.append(finding('build-settings', 'warn', 'The scenes in the build changed',
                                        ''.join(f'added: {p}\n' for p in added) + ''.join(f'removed: {p}\n' for p in removed)
                                        + 'Every scene in the list ships in every build; keep only what you meant to add.',
                                        [path], 'discard'))
        if path == 'ProjectSettings/ProjectSettings.asset':
            o, n = defines(ot), defines(nt)
            lines = []
            for plat in sorted(set(o) | set(n)):
                add, rem = n.get(plat, set()) - o.get(plat, set()), o.get(plat, set()) - n.get(plat, set())
                if add:
                    lines.append(f'{plat}: added {";".join(sorted(add))}')
                if rem:
                    lines.append(f'{plat}: removed {";".join(sorted(rem))}')
            if lines:
                findings.append(finding('define-symbols', 'warn', 'Scripting define symbols changed',
                                        '\n'.join(lines) + '\nA define changes what compiles on that platform for everyone.',
                                        [path], 'discard'))
        if path == 'Packages/manifest.json':
            o, n = manifest_deps(ot), manifest_deps(nt)
            lines = [f'added {k} {n[k]}' for k in sorted(set(n) - set(o))]
            lines += [f'removed {k}' for k in sorted(set(o) - set(n))]
            lines += [f'{k}: {o[k]} -> {n[k]}' for k in sorted(set(o) & set(n)) if o[k] != n[k]]
            if lines:
                findings.append(finding('packages', 'warn', 'Package dependencies changed',
                                        '\n'.join(lines) + '\nA new or changed package is flagged to the team before it lands (CLAUDE.md).',
                                        [path]))

    # -- .meta pairs (Unity's own rule: every file and folder under Assets/ has one, and only one)
    for path in work:
        name = path.rsplit('/', 1)[-1]
        if not path.startswith('Assets/') or name.startswith('.') or name.endswith('~') or junk_kind(path, projects):
            continue                           # Unity gives hidden files and junk no .meta
        code = st.get(path, '')
        if path.endswith('.meta'):
            asset = path[:-5]
            if code == '??' and not os.path.exists(os.path.join(cwd, asset)):
                findings.append(finding('meta-pair', 'block', 'A .meta file without its asset', asset + '.meta', [path], 'discard'))
            elif code == '??' and st.get(asset) == '??' and asset not in work:
                findings.append(finding('meta-pair', 'block', 'A new .meta is checked but its asset is not', asset, [path, asset]))
        else:
            meta = path + '.meta'
            if code == '??' and os.path.isfile(os.path.join(cwd, path)):
                tracked = git('ls-files', '--error-unmatch', meta, cwd=cwd) is not None
                if not tracked and (meta not in work):
                    findings.append(finding('meta-pair', 'block', 'A new asset without its .meta',
                                            f'{path}\nUnity writes {os.path.basename(meta)} next to it; commit both, '
                                            'or every other machine gives the asset a new GUID and its references break.',
                                            [path, meta]))
            if 'D' in code and meta not in st and git('ls-files', '--error-unmatch', meta, cwd=cwd) is not None:
                findings.append(finding('meta-pair', 'warn', 'An asset is deleted but its .meta is not',
                                        f'{path}\nDelete {os.path.basename(meta)} too (Unity does when it next imports).',
                                        [path]))

    if junk:
        findings.append(finding('junk', 'warn', f'{len(junk)} file(s) that should not be committed',
                                '\n'.join(sorted(set(k for _, k in junk))), [p for p, _ in junk], 'discard'))
    if fmod_unsaved:
        findings.append(finding('fmod-event', 'warn', 'An FMOD event that is not saved in FMOD Studio',
                                ''.join(f'{e}: used by {", ".join(sorted(set(ps)))}\n' for e, ps in sorted(fmod_unsaved.items()))
                                + 'The event exists only in FMOD\'s .unsaved folder. It plays from your banks, but the '
                                  'next person to build banks loses it: save the FMOD project, build, and commit Metadata/.',
                                sorted(set(p for ps in fmod_unsaved.values() for p in ps))))
    if fmod_missing:
        findings.append(finding('fmod-event', 'warn', 'An FMOD event the FMOD project does not have',
                                ''.join(f'{e}: used by {", ".join(sorted(set(ps)))}\n' for e, ps in sorted(fmod_missing.items()))
                                + 'Save the FMOD project and commit its Metadata/ with the change, or the slot is silent '
                                  'on every other machine.',
                                sorted(set(p for ps in fmod_missing.values() for p in ps))))
    if resaves:
        flight = (in_flight or default_in_flight)(cwd, resaves)
        if flight:
            who = '; '.join(f'{r["branch"]} ({", ".join(r.get("people") or ["a Claude session"])})' for r in flight[:6])
            findings.append(finding('resave-only', 'warn', f'{len(resaves)} file(s) changed only because Unity re-saved them',
                                    f'Branches in flight touch them: {who}.\nCommitting a re-save makes those branches '
                                    'conflict on lines nobody changed. Discard these unless you meant to edit them.',
                                    resaves, 'discard'))
        else:
            findings.append(finding('resave-only', 'info', f'{len(resaves)} file(s) changed only because Unity re-saved them',
                                    'Nothing in flight touches them, so committing them is harmless churn; '
                                    'discarding them keeps the pull request to what you changed.', resaves, 'discard'))
    if fonts:
        findings.append(finding('dynamic-font', 'warn', 'A dynamic font asset filled itself while you played',
                                'TextMeshPro writes every glyph it draws into a dynamic font asset. Leave it out unless '
                                'you meant to bake the font.', fonts, 'discard'))
    if blocked_big:
        findings.append(finding('large-file', 'block', 'A file over 100 MB', 'GitHub refuses the push.', blocked_big))
    if big:
        findings.append(finding('large-file', 'warn', 'A file over 50 MB', 'Every clone downloads it forever.', big))
    if markers:
        findings.append(finding('conflict-markers', 'block', 'Unresolved merge conflict markers',
                                'Lines starting <<<<<<< / ======= / >>>>>>> are still in the file.', markers))

    order = {'block': 0, 'warn': 1, 'info': 2}
    findings.sort(key=lambda f: order[f['severity']])
    return {'branch': branch, 'base': base, 'behind': behind, 'checked': len(items),
            'unpushedPaths': len(pushed_paths), 'findings': findings}


def default_in_flight(cwd, paths):
    try:
        sys.path.insert(0, os.path.join(cwd, 'Tools', 'Build'))
        import who_works_where as w
        mine = current_branch(cwd)
        return [r for r in w.in_flight(w.Team.load(cwd), 30, paths, cwd) if r.get('branch') != mine]
    except Exception:          # the who-works-where map is advice: no map is not a failure of the push
        return []


# ----------------------------------------------------------------------------- self-test
def self_test():
    errs = []

    def expect(cond, msg):
        if not cond:
            errs.append(msg)

    # resave_only, on the shapes Unity really writes (PR #1766's re-saves) and on real edits.
    old = ('%YAML 1.1\n--- !u!1 &10\nGameObject:\n  m_Name: A\n  m_Component:\n  - component: {fileID: 11}\n'
           '--- !u!114 &11\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: abc, type: 3}\n  speed: 2\n'
           '--- !u!1001 &500\nPrefabInstance:\n  m_Modification:\n    m_TransformParent: {fileID: 12}\n'
           '    m_Modifications:\n    - target: {fileID: 7, guid: g, type: 3}\n      propertyPath: m_Name\n'
           '      value: X\n      objectReference: {fileID: 0}\n    - target: {fileID: 8, guid: g, type: 3}\n'
           '      propertyPath: m_LocalPosition.x\n      value: 0\n      objectReference: {fileID: 0}\n'
           '    m_RemovedComponents: []\n'
           '--- !u!4 &999 stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: 8, guid: g, type: 3}\n'
           '  m_PrefabInstance: {fileID: 500}\n  m_PrefabAsset: {fileID: 0}\n'
           '--- !u!4 &12\nTransform:\n  m_Children:\n  - {fileID: 999}\n')
    canon = str((500 ^ 8) & 0x7FFFFFFFFFFFFFFF)
    resaved = ('%YAML 1.1\n--- !u!4 &12\nTransform:\n  m_Children:\n  - {fileID: ' + canon + '}\n'
               '--- !u!1 &10\nGameObject:\n  m_Name: A\n  m_Component:\n  - component: {fileID: 11}\n'
               '--- !u!114 &11\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: abc,\n    type: 3}\n  speed: 2\n'
               '  newField: 0.6\n  shrinkToFit: 1\n'
               '--- !u!1001 &500\nPrefabInstance:\n  m_Modification:\n    m_TransformParent: {fileID: 12}\n'
               '    m_Modifications:\n    - target: {fileID: 8, guid: g, type: 3}\n'
               '      propertyPath: m_LocalPosition.x\n      value: 0\n      objectReference: {fileID: 0}\n'
               '    - target: {fileID: 7, guid: g, type: 3}\n      propertyPath: m_Name\n'
               '      value: X\n      objectReference: {fileID: 0}\n    m_RemovedComponents: []\n'
               '--- !u!4 &' + canon + ' stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: 8, guid: g, type: 3}\n'
               '  m_PrefabInstance: {fileID: 500}\n  m_PrefabAsset: {fileID: 0}\n')
    expect(resave_only(old, resaved), 'a Unity re-save (re-wrap, re-order, stub id, new fields) was not recognised')
    expect(not resave_only(old, old), 'an unchanged file counted as a re-save')
    expect(not resave_only(old, resaved.replace('speed: 2', 'speed: 3')), 'a changed value counted as a re-save')
    expect(not resave_only(old, resaved.replace('  - component: {fileID: 11}\n', '  - component: {fileID: 11}\n  - component: {fileID: 13}\n')),
           'a new component counted as a re-save')
    expect(not resave_only(old, resaved.replace('      value: X\n', '      value: Y\n')), 'a changed override counted as a re-save')
    expect(not resave_only(old, resaved + '--- !u!1 &77\nGameObject:\n  m_Name: New\n'), 'a new object counted as a re-save')
    stamp = ('--- !u!114 &55\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n  m_Script: {fileID: 11500000, guid: '
             + URP_ASSET_VERSION_GUID + ', type: 3}\n  version: 10\n')
    expect(resave_only(old, resaved + stamp), "URP's material version stamp counted as a real change")
    multiline = resaved.replace('  shrinkToFit: 1\n', "  shrinkToFit: 1\n  unavailableLine: 'LEVEL PREVIEW\n\n    NOT AVAILABLE'\n")
    expect(resave_only(old, multiline), 'a newly written multi-line string counted as a real change')
    expect(unwrap(["  a: 'it''s", "    fine'", '  b: "x \\" y', '    z"', '  c: 1']) ==
           ["  a: 'it''s fine'", '  b: "x \\" y z"', '  c: 1'], f'unwrap quoted: {unwrap(["  a: \'it\'\'s", "    fine\'"])}')
    expect(not resave_only(old, resaved.replace('--- !u!114 &11\n', '--- !u!114 &11\n').replace(
        '  m_Children:\n  - {fileID: ' + canon + '}\n', '  m_Children: []\n')), 'a removed child counted as a re-save')

    # event_refs: Unity's GUID layout (four little-endian int32s) -> FMOD's guid string.
    ev = 'x:\n  Guid:\n    Data1: 347127818\n    Data2: 1192928209\n    Data3: 1625581210\n    Data4: 120483870\n  Path: event:/SFX/Loops/Morningstar loop\n'
    expect(event_refs(ev) == {'14b0c00a-a3d1-471a-9a66-e4601e702e07': 'event:/SFX/Loops/Morningstar loop'},
           f'event_refs decoded {event_refs(ev)}')
    expect(event_refs(ev.replace('347127818', '0').replace('1192928209', '0').replace('1625581210', '0').replace('120483870', '0')) == {},
           'an empty event slot counted as a reference')

    # The whole run, in a throwaway repo with a fake origin/bleeding-edge.
    with tempfile.TemporaryDirectory() as d:
        def g(*a):
            subprocess.run(['git'] + list(a), cwd=d, check=True, capture_output=True)

        def put(path, data, mode='w'):
            full = os.path.join(d, path)
            os.makedirs(os.path.dirname(full), exist_ok=True)
            with open(full, mode) as fh:
                fh.write(data)

        g('init', '-q', '-b', 'bleeding-edge')
        g('config', 'user.email', 't@t')
        g('config', 'user.name', 't')
        put('Assets/A.prefab', old)
        put('Assets/A.prefab.meta', 'guid: 1\n')
        put('Assets/B.prefab', old)
        put('Assets/B.prefab.meta', 'guid: 2\n')
        put('Assets/Code.cs', 'class A {}\n')
        put('Assets/Code.cs.meta', 'guid: 3\n')
        put('ProjectSettings/EditorBuildSettings.asset', '%YAML 1.1\n--- !u!1045 &1\nEditorBuildSettings:\n  m_Scenes:\n  - enabled: 1\n    path: Assets/Main.unity\n')
        put('ProjectSettings/ProjectSettings.asset', '%YAML 1.1\n--- !u!129 &1\nPlayerSettings:\n  scriptingDefineSymbols:\n    Standalone: DOTWEEN\n    WebGL: DOTWEEN\n  other: 1\n')
        put('Audio/Audio.fspro', 'fspro')
        put('Audio/Metadata/Event/{e5eaad0e-b631-4200-8e2f-2b745b9c78e2}.xml', 'saved')
        put('Audio/.unsaved/Metadata/Event/{14b0c00a-a3d1-471a-9a66-e4601e702e07}.xml', 'unsaved')
        put('Packages/manifest.json', '{"dependencies": {"com.a": "1.0"}}')
        g('add', '-A')
        g('commit', '-qm', 'base')
        g('update-ref', 'refs/remotes/origin/bleeding-edge', 'HEAD')
        for i in range(BEHIND_WARN_MERGES):        # bleeding-edge moves on by enough merges to warn
            g('checkout', '-q', '-b', f'f{i}', 'origin/bleeding-edge')
            put(f'Docs/f{i}.md', 'x')
            g('add', '-A')
            g('commit', '-qm', f'f{i}')
            g('checkout', '-q', '--detach', 'origin/bleeding-edge')
            g('merge', '-q', '--no-ff', '-m', f'merge f{i}', f'f{i}')
            g('update-ref', 'refs/remotes/origin/bleeding-edge', 'HEAD')
        g('checkout', '-q', '-b', 'feature', f'HEAD~{BEHIND_WARN_MERGES}')   # where bleeding-edge was at 'base'

        # A clean feature branch first: no findings but the behind-base warning.
        r = run_checks(d, in_flight=lambda c, p: [])
        ids = [f['id'] for f in r['findings']]
        expect(ids == ['behind-base'], f'a clean branch reported {ids}')
        expect(r['behind'] == {'merges': BEHIND_WARN_MERGES, 'commits': 2 * BEHIND_WARN_MERGES}, f'behind = {r["behind"]}')

        # Now every bad shape at once.
        put('Assets/A.prefab', resaved)                                   # a re-save
        put('Assets/B.prefab', resaved.replace('speed: 2', 'speed: 9'))    # a real edit
        put('Assets/.DS_Store', 'junk')
        put('steam_appid.txt', '480')
        put('Audio/.cache/x.pdc', 'cache')
        put('Assets/New.asset', '%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n  m_Name: New\n')    # no .meta
        put('Assets/Orphan.png.meta', 'guid: 9\n')                        # no asset
        put('Assets/Code.cs', '<<<<<<< HEAD\nclass A {}\n=======\nclass B {}\n>>>>>>> theirs\n')
        put('ProjectSettings/EditorBuildSettings.asset', '%YAML 1.1\n--- !u!1045 &1\nEditorBuildSettings:\n  m_Scenes:\n  - enabled: 1\n    path: Assets/Main.unity\n'
            '  - enabled: 1\n    path: Assets/Archived.unity\n')
        put('ProjectSettings/ProjectSettings.asset', '%YAML 1.1\n--- !u!129 &1\nPlayerSettings:\n  scriptingDefineSymbols:\n    Standalone: DOTWEEN;STEAMWORKS_NET\n    WebGL: DOTWEEN\n  other: 1\n')
        put('Packages/manifest.json', '{"dependencies": {"com.a": "1.0", "com.b": "2.0"}}')
        put('Assets/Sound.prefab', '%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n  evt:\n' + ev.split('\n', 1)[1]
            + '  other:\n    Guid:\n      Data1: 1\n      Data2: 2\n      Data3: 3\n      Data4: 4\n    Path: event:/Gone\n')
        put('Assets/Sound.prefab.meta', 'guid: 4\n')
        r = run_checks(d, in_flight=lambda c, p: [{'branch': 'claude/ui', 'people': ['Shombith']}])
        by = collections.defaultdict(list)
        for f in r['findings']:
            by[f['id']].append(f)
        expect([f['severity'] for f in r['findings']] == sorted([f['severity'] for f in r['findings']],
               key=lambda s: {'block': 0, 'warn': 1, 'info': 2}[s]), 'findings are not ordered block, warn, info')
        expect(by['resave-only'] and by['resave-only'][0]['paths'] == ['Assets/A.prefab']
               and by['resave-only'][0]['severity'] == 'warn' and 'claude/ui' in by['resave-only'][0]['detail'],
               f'resave-only: {by["resave-only"]}')
        junk_paths = set(by['junk'][0]['paths']) if by['junk'] else set()
        expect(junk_paths == {'Assets/.DS_Store', 'steam_appid.txt', 'Audio/.cache/x.pdc'}, f'junk: {junk_paths}')
        metas = ' | '.join(f['title'] for f in by['meta-pair'])
        expect('without its .meta' in metas and 'without its asset' in metas, f'meta-pair: {metas}')
        expect(by['conflict-markers'] and by['conflict-markers'][0]['paths'] == ['Assets/Code.cs'], 'conflict markers missed')
        expect(by['build-settings'] and 'Assets/Archived.unity' in by['build-settings'][0]['detail'], 'build scene missed')
        expect(by['define-symbols'] and 'Standalone: added STEAMWORKS_NET' in by['define-symbols'][0]['detail'], 'define missed')
        expect(by['packages'] and 'added com.b 2.0' in by['packages'][0]['detail'], 'package missed')
        fm = ' | '.join(f['title'] + ' ' + f['detail'] for f in by['fmod-event'])
        expect('not saved in FMOD Studio' in fm and 'Morningstar loop' in fm and 'event:/Gone' in fm,
               f'fmod-event: {fm}')
        expect(not any('Assets/B.prefab' in f['paths'] for f in by['resave-only']), 'a real edit was offered for discard')

        # Selected paths narrow the check; unpushed commits are checked whatever is selected.
        r2 = run_checks(d, paths=['Assets/B.prefab'], in_flight=lambda c, p: [])
        expect(not any(f['id'] in ('junk', 'resave-only', 'meta-pair') for f in r2['findings']),
               f'a narrowed check still reported other files: {[f["id"] for f in r2["findings"]]}')
        g('add', 'steam_appid.txt')
        g('commit', '-qm', 'oops')
        r3 = run_checks(d, paths=['Assets/B.prefab'], in_flight=lambda c, p: [])
        expect(any(f['id'] == 'junk' and 'steam_appid.txt' in f['paths'] for f in r3['findings']),
               'a junk file in an unpushed commit was not reported')

        # The protected branch blocks.
        g('stash', '-q', '-u')
        g('checkout', '-q', '-B', 'bleeding-edge')
        r4 = run_checks(d, in_flight=lambda c, p: [])
        expect(r4['findings'] and r4['findings'][0]['id'] == 'protected-branch' and r4['findings'][0]['severity'] == 'block',
               'pushing from bleeding-edge was not blocked')

    for e in errs:
        print('FAIL:', e)
    print('push_checks self-test:', 'OK' if not errs else f'{len(errs)} failure(s)')
    return 1 if errs else 0


def main(argv):
    if '--self-test' in argv:
        return self_test()
    paths = None
    if '--paths-from' in argv:
        with open(argv[argv.index('--paths-from') + 1], encoding='utf-8') as fh:
            paths = [l.strip().replace('\\', '/') for l in fh if l.strip()]
    base = argv[argv.index('--base') + 1] if '--base' in argv else BASE
    root = os.path.abspath(argv[argv.index('--root') + 1]) if '--root' in argv else ROOT
    r = run_checks(root, paths=paths, base=base)
    if '--json' in argv:
        print(json.dumps(r, indent=1))
        return 0
    print(f'{r["branch"]}: {r["checked"]} path(s) checked'
          + (f', {r["behind"]["merges"]} pull request(s) behind {base}' if r['behind'] else ''))
    for f in r['findings']:
        print(f'{f["severity"].upper():5} {f["title"]}')
        for line in f['detail'].splitlines():
            print('      ' + line)
        for p in f['paths'][:20]:
            print('      - ' + p)
    blocks = sum(f['severity'] == 'block' for f in r['findings'])
    print('PUSH CHECKS:', 'BLOCKED' if blocks else 'OK')
    return 1 if blocks else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
