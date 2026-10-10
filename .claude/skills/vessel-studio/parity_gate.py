#!/usr/bin/env python3
"""parity_gate.py - the Vessel Studio is ONE thing everywhere it opens (/vessel-studio D33).

Every surface (the claude.ai artifact, the live mirror, Amoebius's OPEN IN AMOEBIUS / OPEN IN BROWSER, Unity's
FrogletTools > Vessels > Vessel Studio through Amoebius) must open the SAME build_artifact.py output, and the pages must
look the same on every one of them. This gate fails if any surface would open something different.

    python3 .claude/skills/vessel-studio/parity_gate.py                  # pages + entry points of this checkout
    python3 .claude/skills/vessel-studio/parity_gate.py --built DIR      # also: DIR is the build of these pages, unchanged
    python3 .claude/skills/vessel-studio/parity_gate.py --served URL     # also: Amoebius serves DIR's bytes (+ only the bridge tag)
    python3 .claude/skills/vessel-studio/parity_gate.py --mirror [URL]   # also: the live mirror is the build of --ref
    python3 .claude/skills/vessel-studio/parity_gate.py --artifact       # also: the artifact's last record matches the repo (warning; --strict fails)
    python3 .claude/skills/vessel-studio/parity_gate.py --render URL...  # also: every URL shows the same tabs and controls (needs playwright)
    python3 .claude/skills/vessel-studio/parity_gate.py --self-test      # plants every defect and proves each is caught

The checks:
  pages        every page loads studio-theme.js and carries the Sync panel once; nothing VISIBLE names the host (no
               hostTag, no "Running on Amoebius", no shell-dependent text); host detection only in
               VesselStudioTheme.host(); the WebGL renderer only through VesselStudioTheme.renderer() (a page without
               WebGL still loads every tab); no page branches on window.claude to hide a control.
  entry        Amoebius's STUDIOS actions open the served build (OpenServedStudio), never a repo page or file://;
               the app window gets a URL, not a file; Unity's Vessel Studio goes through Amoebius (--page studios:hub)
               and never opens Docs/Studios as a file.
  built        each built page and shared file is the repo's, byte for byte (the Sync tag injected only into a page
               that lacked it); build.json names the commit.
  served       each page Amoebius serves is the built page plus exactly one bridge tag in <head>.
  mirror       the mirror's build.json pathSha is the ref's, and its files are the build's.
  artifact     Docs/Artifacts/artifacts.json's vessel-studio record (hashes of the last publish/import) matches the repo.
  render       DOM inventory (tabs, buttons, selects, section titles, the Sync panel) identical across URLs; only
               status lines (what is enabled) may differ.
build_artifact.py --check runs the page checks on every build.
"""
import argparse, hashlib, json, os, re, shutil, subprocess, sys, tempfile, urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import studio_cards  # noqa: E402  the card model (/vessel-studio D34)

DIR = 'Docs/Studios/VesselStudio'
STOAT_SRC = 'Docs/Studios/StoatFlightStudio.html'
LAUNCHER_STUDIOS = 'Port/src/CosmicShore.Launcher/LauncherApp.Studios.cs'
LAUNCHER_CATALOG = 'Port/src/CosmicShore.Launcher/StudioCatalog.cs'
UNITY_LAUNCH = 'Assets/_Scripts/Editor/LaunchPrisma.cs'
UNITY_WINDOW = 'Assets/_Scripts/Editor/Studios/VesselStudioWindow.cs'
UNITY_HOME = 'Assets/_Scripts/Editor/Studios/VesselStudioWindow.Home.cs'
TAG = '<script src="sync.js"></script>'
BRIDGE = '<script src=".amoebius/bridge.js"></script>'
DEFAULT_MIRROR = 'https://yskhan61.github.io/vessel-studio/'
HELPER_FILE = 'studio-theme.js'

HOST_TEXT = re.compile(r'Running on Amoebius|Running on Prisma|id="hostTag"|\$\(\'hostTag\'\)')
HOST_DETECT = re.compile(r'location\.hash[^;\n]{0,80}(amoebius|prisma)|(amoebius|prisma)[^;\n]{0,80}location\.hash|__studioHost\s*(\|\||&&|\?|\))', re.I)
SHELL_TEXT = re.compile(r'\.shell\s*===?\s*[\'"](prisma|amoebius|web)[\'"]')
RAW_GL = re.compile(r'new\s+THREE\.WebGLRenderer\s*\(')
HIDE_ON_HOST = re.compile(r'(inClaude|window\.claude)[^;\n]{0,120}\.(hidden\s*=\s*true|remove\(\)|style\.display\s*=\s*[\'"]none)')


def read(p):
    with open(p, encoding='utf-8') as fh:
        return fh.read()


def catalog_files(root):
    cat = json.loads(read(os.path.join(root, DIR, 'studios.json')))
    pages = ['index.html'] + [s['file'] for s in cat.get('studios', []) if s.get('file')]
    return cat, pages, list(cat.get('shared', []))


def page_checks(folder, pages=None, shared=None, label='', basic=True):
    """The checks every page must pass, in the repo folder or a build. Returns a list of failures."""
    errs = []
    try:
        cat = json.loads(read(os.path.join(folder, 'studios.json')))
    except Exception as e:
        return [f'{label}studios.json: {e}']
    pages = pages or ['index.html'] + [s['file'] for s in cat.get('studios', []) if s.get('file')]
    shared = shared if shared is not None else list(cat.get('shared', []))
    for f in pages + [x for x in shared if x.endswith('.js')]:
        p = os.path.join(folder, f)
        if not os.path.exists(p):
            if f.endswith('.html'):
                errs.append(f'{label}{f}: missing')
            continue
        text = read(p)
        helper = f == HELPER_FILE
        if basic and f.endswith('.html'):
            if text.count('src="sync.js"') > 1:
                errs.append(f'{label}{f}: the Sync panel tag more than once')
            if 'studio-theme.js"' not in text:
                errs.append(f'{label}{f}: does not load studio-theme.js (the one look and the one host helper)')
        if HOST_TEXT.search(text):
            errs.append(f'{label}{f}: names the host on the page (hostTag / "Running on Amoebius"): every surface looks the same (D33)')
        if not helper and SHELL_TEXT.search(text):
            errs.append(f'{label}{f}: text or layout depends on the host shell: only the device may shape the page (D33)')
        if not helper and HOST_DETECT.search(text):
            errs.append(f'{label}{f}: reads the host itself: use VesselStudioTheme.host() (one helper, D33)')
        if not helper and RAW_GL.search(text):
            errs.append(f'{label}{f}: creates THREE.WebGLRenderer directly: use VesselStudioTheme.renderer() so a page without WebGL still loads (D33)')
        if HIDE_ON_HOST.search(text):
            errs.append(f'{label}{f}: hides a control when there is (no) backend: keep it visible, say what it needs (D30/D33)')
    return errs


def region(text, start, end_pat=r'\n        }\n'):
    i = text.find(start)
    if i < 0:
        return None
    m = re.compile(end_pat).search(text, i)
    return text[i:m.end() if m else len(text)]


def entry_checks(root):
    """Every entry point opens the served build: Amoebius's actions and Unity's menu."""
    errs = []
    p = os.path.join(root, LAUNCHER_STUDIOS)
    if os.path.exists(p):
        t = read(p)
        acts = region(t, 'List<PageAction> StudioActions(')
        if acts is None:
            errs.append(f'{LAUNCHER_STUDIOS}: no StudioActions (the VESSEL STUDIO action row)')
        else:
            if re.search(r'OpenStudioWindow\(\s*page', acts) or re.search(r'OpenUrl\(\s*page', acts) or 'PageUri(' in acts:
                errs.append(f'{LAUNCHER_STUDIOS}: a Vessel Studio action opens the repo page as a file: open the served build (OpenServedStudio)')
            if 'OpenServedStudio(' not in acts:
                errs.append(f'{LAUNCHER_STUDIOS}: OPEN IN AMOEBIUS does not open the served build (OpenServedStudio)')
        if '_studioOpenAtStart' not in t:
            errs.append(f'{LAUNCHER_STUDIOS}: --page studios:<id> does not open a studio (Unity\'s Vessel Studio needs it)')
    else:
        errs.append(f'{LAUNCHER_STUDIOS}: missing')
    p = os.path.join(root, LAUNCHER_CATALOG)
    if os.path.exists(p):
        t = read(p)
        if re.search(r'"--app="\s*\+\s*PageUri', t):
            errs.append(f'{LAUNCHER_CATALOG}: the app window opens a file URI: it must open the served URL')
        if '#prisma' in t:
            errs.append(f'{LAUNCHER_CATALOG}: still opens pages with #prisma (a file page): serve the build with #amoebius')
    p = os.path.join(root, UNITY_LAUNCH)
    if os.path.exists(p):
        t = read(p)
        op = region(t, 'internal static void OpenStudio(')
        if op is None:
            errs.append(f'{UNITY_LAUNCH}: no OpenStudio (the studio home\'s cards open a studio through Amoebius)')
        elif 'studios:' not in op or 'server.json' not in t:
            errs.append(f'{UNITY_LAUNCH}: OpenStudio does not go through Amoebius (its running server, else --page studios:<file>)')
        if re.search(r'Uri\(\s*Path\.GetFullPath\(\s*page', t) or re.search(r'"Docs",\s*"Studios",\s*"VesselStudio"[^;]*\.html', t):
            errs.append(f'{UNITY_LAUNCH}: opens a Vessel Studio page from the checkout as a file: go through Amoebius (served build)')
    p = os.path.join(root, UNITY_HOME)
    if os.path.exists(p):
        t = read(p)
        if 'OpenStudioWindow(' in t or re.search(r'Application\.OpenURL\([^)]*(file:|Docs/Studios|path)', t):
            errs.append(f'{UNITY_HOME}: a card opens the studio page as a file: LaunchPrisma.OpenStudio (served by Amoebius)')
        if 'LaunchPrisma.OpenStudio(' not in t:
            errs.append(f'{UNITY_HOME}: OPEN STUDIO does not go through LaunchPrisma.OpenStudio')
    p = os.path.join(root, UNITY_WINDOW)
    if os.path.exists(p):
        t = read(p)
        if re.search(r'Application\.OpenURL\([^)]*(file:|Docs/Studios)', t):
            errs.append(f'{UNITY_WINDOW}: opens a studio page as a file')
    return errs


def card_checks(root, natives=True):
    """The studio cards are the hub's everywhere (D34): studios.json carries the hub's card text, the baked previews
    are current, and both native homes draw their cards from studios.json, never from their own copy."""
    if not os.path.exists(os.path.join(root, DIR, 'studios.json')) or not os.path.exists(os.path.join(root, DIR, 'index.html')):
        return []
    errs = ['cards: ' + e for e in studio_cards.differences(root)]
    if not natives:
        return errs
    cat = json.loads(read(os.path.join(root, DIR, 'studios.json')))
    labels = [a.get('label') for a in cat.get('cardActions') or [] if a.get('label')] + ['OPEN STUDIO']
    code = lambda t: re.sub(r'//[^\n]*', '', t or '')   # comments do not count as drawing anything
    homes = ((LAUNCHER_STUDIOS, 'void DrawStudioCard(', 'List<PageAction> CardButtons(', 'CardActions', ('.Chip', '.Spec', 'Preview')),
             (UNITY_HOME, 'void DrawCard(', 'static List<CardButton> CardButtons(', 'cardActions', ('.chip', '.spec', 'preview')))
    for rel, method, buttons, field, needles in homes:
        p = os.path.join(root, rel)
        if not os.path.exists(p):
            continue
        t = read(p)
        card, btn = code(region(t, method)), code(region(t, buttons))
        if not card:
            errs.append(f'{rel}: no {method.split("(")[0].split()[-1]} (the hub\'s studio card, drawn from studios.json)'); continue
        if not btn or 'CardButtons(' not in card or field not in btn:
            errs.append(f'{rel}: the studio card does not take its buttons from studios.json {field} ({buttons.split("(")[0].split()[-1]})')
        for n in needles:
            if n not in card:
                errs.append(f'{rel}: the studio card does not draw {n.strip(".")} from studios.json')
        for lab in labels:
            if '"' + lab in card + btn:
                errs.append(f'{rel}: the studio card spells "{lab}" itself: take the label from studios.json cardActions')
        if not re.search(r'[Ff]leet', t):
            errs.append(f'{rel}: no fleet list (studios.json "fleet")')
    p = os.path.join(root, UNITY_WINDOW)
    if os.path.exists(p):
        m = re.search(r'Tuners\s*=\s*new\(\)\s*\{([^}]*)\}', read(p))
        if m:
            unity = set(re.findall(r'\["(\w+)"\]', m.group(1)))
            listed = {x.get('id') for x in cat.get('studios', []) if x.get('tuner')}
            if unity != listed:
                errs.append(f'{UNITY_WINDOW}: Unity tunes {sorted(unity)}, studios.json marks {sorted(listed)} "tuner": TUNE IN UNITY must match')
    return errs


def cards_self_test():
    """Plant each card defect in a throwaway tree; a clean one passes."""
    bad = []
    tmp = tempfile.mkdtemp(prefix='vs-cards-self-')
    try:
        def w(rel, text, binary=False):
            p = os.path.join(tmp, rel); os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, 'wb' if binary else 'w', **({} if binary else {'encoding': 'utf-8'})) as fh: fh.write(text)
        hub = ('<style>:root { --jade: #13fff2; }</style><p class="lede">Pick a vessel.</p><div class="bays">'
               '<a class="bay" href="a.html" style="--accent: var(--jade)"><canvas id="pvA"></canvas><div><div class="name"><b>Alpha</b>'
               '<span class="chip">built · round 1</span></div><p>The first one.</p><div class="spec"><span>Speed</span><b>60 u/s</b></div></div>'
               '<span class="open">Open studio &rarr;</span></a></div><div class="fleet"><span>Manta</span></div>'
               '<p class="lede hint">No studio yet.</p><script>\n  // ---- bay previews: x ----\n  preview();\n  // ---- the studio agent ----\n</script>')
        acts = [{'id': 'open', 'label': 'Open studio →', 'on': 'web,amoebius,unity'}, {'id': 'engine', 'label': 'PLAY IN ENGINE', 'on': 'amoebius,unity', 'needs': 'engineMode'},
                {'id': 'tune', 'label': 'TUNE IN UNITY', 'on': 'unity', 'needs': 'tuner'}, {'id': 'live', 'label': 'OPEN LIVE IN BROWSER', 'on': 'amoebius,unity', 'needs': 'mirror'}]
        cat = {'lede': 'Pick a vessel.', 'cardActions': acts, 'fleet': ['Manta'], 'fleetNote': 'No studio yet.',
               'studios': [{'id': 'a', 'name': 'Alpha', 'file': 'a.html', 'chip': 'built · round 1', 'summary': 'The first one.',
                            'spec': [{'k': 'Speed', 'v': '60 u/s'}], 'accent': 'jade', 'preview': 'previews/a.png', 'tuner': True}]}
        w(f'{DIR}/index.html', hub)
        w(f'{DIR}/studios.json', json.dumps(cat, ensure_ascii=False))
        w(f'{DIR}/previews/a.png', b'png', binary=True)
        w(f'{DIR}/previews/previews.json', json.dumps({'source': studio_cards.preview_hash(tmp)}))
        good_amo = ('        List<PageAction> CardButtons(StudioCatalog cat, StudioCatalog.Studio s)\n        {\n            foreach (var a in cat.CardActions) list.Add(a.Label);\n        }\n'
                    '        void DrawStudioCard(StudioCatalog cat, StudioCatalog.Studio s)\n        {\n            Image(Preview(s)); Text(s.Chip); Rows(s.Spec);\n'
                    '            foreach (var b in CardButtons(cat, s)) Button(b.Label);\n        }\n        void Fleet() { }\n')
        good_unity = ('        public static List<CardButton> CardButtons(Catalog cat, StudioEntry s)\n        {\n            foreach (var a in cat.cardActions) list.Add(a.label);\n        }\n'
                      '        void DrawCard(Rect r, StudioEntry s)\n        {\n            Tex(s.preview); Label(s.chip); Rows(s.spec);\n'
                      '            foreach (var b in CardButtons(_catalog, s)) Button(b.Label);\n        }\n        string[] _fleet;\n')
        w(LAUNCHER_STUDIOS, good_amo)
        w(UNITY_HOME, good_unity)
        w(UNITY_WINDOW, 'static readonly Dictionary<string, X> Tuners = new() { ["a"] = Tabs };')
        clean = card_checks(tmp)
        if clean:
            bad.append('a clean card tree failed: ' + '; '.join(clean))

        def expect(name, rel, text, needle):
            p = os.path.join(tmp, rel); orig = read(p)
            w(rel, text)
            got = card_checks(tmp)
            if not any(needle in g for g in got):
                bad.append(f'{name} was not caught (got: {got})')
            w(rel, orig)
        cj = f'{DIR}/studios.json'
        def cat_with(**kw):
            c = json.loads(json.dumps(cat)); c['studios'][0].update(kw.get('studio', {}))
            for k, v in kw.items():
                if k != 'studio': c[k] = v
            return json.dumps(c, ensure_ascii=False)
        expect('a summary that drifted from the hub', cj, cat_with(studio={'summary': 'Something else.'}), 'summary')
        expect('a chip that drifted', cj, cat_with(studio={'chip': 'old'}), 'chip')
        expect('a spec row that drifted', cj, cat_with(studio={'spec': [{'k': 'Speed', 'v': '70 u/s'}]}), 'spec rows')
        expect('actions out of order', cj, cat_with(cardActions=[acts[1], acts[0], acts[2], acts[3]]), 'in that order')
        expect('another open label', cj, cat_with(cardActions=[dict(acts[0], label='Open'), acts[1], acts[2], acts[3]]), 'open action')
        expect('a fleet that drifted', cj, cat_with(fleet=['Manta', 'Rhino']), 'fleet')
        expect('a missing preview', cj, cat_with(studio={'preview': 'previews/none.png'}), 'missing')
        expect('a stale preview', f'{DIR}/previews/previews.json', json.dumps({'source': 'old'}), 're-bake' if False else 'baked')
        expect('Amoebius spelling a label', LAUNCHER_STUDIOS, good_amo.replace('list.Add(a.Label);', 'list.Add(a.Label); list.Add("PLAY IN ENGINE");'), 'spells "PLAY IN ENGINE"')
        expect('Amoebius without the chip', LAUNCHER_STUDIOS, good_amo.replace('Text(s.Chip); ', ''), 'Chip')
        expect('Amoebius without a card', LAUNCHER_STUDIOS, 'void Other() { }\n', 'no DrawStudioCard')
        expect('Amoebius buttons of its own', LAUNCHER_STUDIOS, good_amo.replace('cat.CardActions', 'MyButtons'), 'does not take its buttons')
        expect('Unity buttons only named in a comment', UNITY_HOME, good_unity.replace('foreach (var a in cat.cardActions)', '// cardActions\n            foreach (var a in Mine)'), 'does not take its buttons')
        expect('Unity spelling a label', UNITY_HOME, good_unity.replace('Button(b.Label);', 'Button(b.Label); Button("OPEN STUDIO ▸");'), 'spells "OPEN STUDIO"')
        expect('Unity without the spec', UNITY_HOME, good_unity.replace('Rows(s.spec);', ''), 'spec')
        expect('a tuner the catalog does not list', UNITY_WINDOW, 'static readonly Dictionary<string, X> Tuners = new() { ["a"] = Tabs, ["b"] = Tabs };', 'TUNE IN UNITY must match')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return bad


def built_checks(root, built, ref='HEAD'):
    """The build is the repo's files, unchanged (only the Sync tag added to a page that lacked it)."""
    errs = []
    _, pages, shared = catalog_files(root)
    def at_ref(f):
        try:
            return subprocess.check_output(['git', '-C', root, 'show', f'{ref}:{DIR}/{f}'], stderr=subprocess.DEVNULL).decode('utf-8')
        except subprocess.CalledProcessError:
            return None
    for f in pages + ['studios.json', 'sync.js'] + shared:
        p = os.path.join(built, f)
        if not os.path.exists(p):
            errs.append(f'built {f}: missing'); continue
        src = at_ref(f)
        if src is None:
            continue
        src = src.replace('\r\n', '\n')
        got = read(p)
        want = src if (not f.endswith('.html') or 'src="sync.js"' in src) else None
        if want is not None and got != want:
            errs.append(f'built {f}: differs from {ref}:{DIR}/{f} (a build must publish the repo page unchanged)')
        if want is None and got.replace(TAG, '', 1) != src:
            errs.append(f'built {f}: differs from the repo beyond the injected Sync tag')
    try:
        b = json.loads(read(os.path.join(built, 'build.json')))
        if not b.get('pathSha'):
            errs.append('built build.json: no pathSha')
    except Exception as e:
        errs.append(f'built build.json: {e}')
    return errs + page_checks(built, label='built ')


def fetch(url, timeout=20):
    req = urllib.request.Request(url, headers={'User-Agent': 'parity_gate'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode('utf-8')


def served_checks(root, built, base):
    """Amoebius serves the build: each page is the built page with the bridge first, nothing else."""
    errs = []
    base = base.split('#')[0]
    base = base if base.endswith('/') else base.rsplit('/', 1)[0] + '/'
    _, pages, shared = catalog_files(root)
    for f in pages + ['studios.json', 'sync.js', 'build.json'] + shared:
        try:
            got = fetch(base + f)
        except Exception as e:
            errs.append(f'served {f}: {e}'); continue
        want = read(os.path.join(built, f))
        if f == 'build.json':
            a, b = json.loads(got), json.loads(want)
            for k in ('repo', 'branch', 'sha', 'pathSha', 'subject', 'committedAt'):
                if a.get(k) != b.get(k):
                    errs.append(f'served build.json: {k} is {a.get(k)!r}, the build has {b.get(k)!r}')
            continue
        if f.endswith('.html'):
            if got.count(BRIDGE) != 1:
                errs.append(f'served {f}: the backend bridge tag {got.count(BRIDGE)} times (want 1)')
            elif got.index(BRIDGE) > max(got.lower().find('<script'), 0) and got.lower().find('<script') < got.index(BRIDGE):
                errs.append(f'served {f}: the bridge is not the first script (window.claude must exist before the page reads it)')
            got = got.replace(BRIDGE, '', 1)
        if got != want:
            errs.append(f'served {f}: differs from the build')
    return errs


def mirror_checks(root, ref, url):
    errs, warns = [], []
    url = url if url.endswith('/') else url + '/'
    tmp = tempfile.mkdtemp(prefix='vs-parity-')
    try:
        out = os.path.join(tmp, 'b')
        b = subprocess.run([sys.executable, os.path.join(root, '.claude/skills/vessel-studio/build_artifact.py'), '--ref', ref, '--out', out],
                           cwd=root, capture_output=True, text=True)
        if b.returncode != 0:   # the ref's own pages fail the build's checks: nothing to compare the mirror with
            lines = (b.stdout + b.stderr).strip().splitlines()
            return [f'{ref} does not build: ' + ' | '.join([l for l in lines if l.startswith('FAIL')][:3] or lines[-1:] or ['?'])], warns
        mine = json.loads(read(os.path.join(out, 'build.json')))
        try:
            theirs = json.loads(fetch(url + 'build.json'))
        except Exception as e:
            return [f'mirror {url}: {e}'], warns
        if theirs.get('pathSha') != mine['pathSha']:
            errs.append(f'mirror shows studio commit {str(theirs.get("pathSha"))[:7]}, {ref} is at {mine["pathSha"][:7]}: republish it (vessel-studio-publish.sh)')
            return errs, warns
        _, pages, shared = catalog_files(root)
        for f in pages + ['studios.json', 'sync.js'] + shared:
            try:
                if fetch(url + f) != read(os.path.join(out, f)):
                    errs.append(f'mirror {f}: differs from the build of {ref}')
            except Exception as e:
                errs.append(f'mirror {f}: {e}')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return errs, warns


def artifact_checks(root):
    """The artifact's last publish/import record (hashes) against the repo: claude.ai itself cannot be read."""
    p = os.path.join(root, 'Docs/Artifacts/artifacts.json')
    if not os.path.exists(p):
        return ['Docs/Artifacts/artifacts.json: missing']
    d = json.loads(read(p))
    entries = d if isinstance(d, list) else d.get('artifacts', [])
    e = next((x for x in entries if x.get('id') == 'vessel-studio'), None)
    if not e:
        return ['artifacts.json: no vessel-studio entry']
    behind = []
    for f, h in sorted((e.get('files') or {}).items()):
        fp = os.path.join(root, DIR, f)
        if not os.path.exists(fp) or hashlib.sha256(open(fp, 'rb').read()).hexdigest() != h:
            behind.append(f)
    if behind:
        return [f'the claude.ai artifact (record {e.get("version")}, {e.get("importedAt")}) differs from the repo in {len(behind)} file(s): '
                f'{", ".join(behind)}; republish it (SKILL.md section 4) and update the record']
    return []


def render_checks(urls):
    probe = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'parity_probe.cjs')
    try:
        out = subprocess.run(['node', probe] + urls, capture_output=True, text=True, timeout=600)
    except (OSError, subprocess.TimeoutExpired) as e:
        return [], [f'render: skipped ({e})']
    if out.returncode == 3:
        return [], ['render: skipped (' + out.stderr.strip()[-200:] + ')']
    try:
        inv = json.loads(out.stdout)
    except json.JSONDecodeError:
        return [f'render: the probe failed: {out.stderr.strip()[-400:]}'], []
    errs = []
    first = urls[0]
    for u in urls[1:]:
        for k in ('tabs', 'buttons', 'selects', 'sections', 'syncPanel', 'pageErrors'):
            a, b = inv[first].get(k), inv[u].get(k)
            if k == 'pageErrors':
                if b:
                    errs.append(f'render {u}: page errors: {b[:3]}')
                continue
            if a != b:
                if isinstance(a, list):
                    errs.append(f'render {u}: {k} differ from {first}: only here {sorted(set(b) - set(a))[:8]}, missing {sorted(set(a) - set(b))[:8]}')
                else:
                    errs.append(f'render {u}: {k} is {b}, {first} has {a}')
    return errs, []


# ---------------------------------------------------------------- self-test

def self_test():
    """Plant each defect the gate must name, in a throwaway tree; prove a clean tree passes."""
    bad = []
    tmp = tempfile.mkdtemp(prefix='vs-gate-self-')
    try:
        def w(rel, text):
            p = os.path.join(tmp, rel); os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, 'w', encoding='utf-8') as fh: fh.write(text)
        good_page = '<head><script src="studio-theme.js"></script></head><body><script>const r = VesselStudioTheme.renderer(THREE, {});</script>' + TAG + '</body>'
        w(f'{DIR}/studios.json', json.dumps({'studios': [{'file': 'a.html'}, {'file': 'b.html'}], 'shared': ['studio-theme.js', 'x.js']}))
        w(f'{DIR}/index.html', good_page)
        w(f'{DIR}/a.html', good_page)
        w(f'{DIR}/b.html', good_page)
        w(f'{DIR}/studio-theme.js', "function host(){ return /amoebius/.test(location.hash); } function renderer(T,o){ return new THREE.WebGLRenderer(o); }")
        w(f'{DIR}/x.js', '/* shared */')
        w(f'{DIR}/sync.js', '/* sync */')
        w(LAUNCHER_STUDIOS, 'string? _studioOpenAtStart;\n        List<PageAction> StudioActions(StudioCatalog cat)\n        {\n            new("OPEN IN AMOEBIUS", true, "", () => OpenServedStudio(t.File, window: true)),\n        }\n')
        w(LAUNCHER_CATALOG, 'public static List<string> AppWindowArgs(string url, string p) { "--app=" + url, }')
        w(UNITY_LAUNCH, 'internal static void OpenStudio(string file)\n        {\n            if (RunningServer() is { } s) { OpenAppWindow(s + file); return; } // server.json\n            _openPage = "studios:" + file; Launch();\n        }\n')
        w(UNITY_HOME, 'static void OpenPage(string file) { LaunchPrisma.OpenStudio(file); }')
        w(UNITY_WINDOW, '[MenuItem("FrogletTools/Vessels/Vessel Studio", false, 0)] Application.OpenURL(StudioUrl);')
        clean = page_checks(os.path.join(tmp, DIR)) + entry_checks(tmp)
        if clean:
            bad.append('a clean tree failed: ' + '; '.join(clean))

        def expect(name, rel, text, needle, checks):
            orig = read(os.path.join(tmp, rel))
            w(rel, text)
            got = checks()
            if not any(needle in g for g in got):
                bad.append(f'{name} was not caught (got: {got})')
            w(rel, orig)
        pc = lambda: page_checks(os.path.join(tmp, DIR))
        ec = lambda: entry_checks(tmp)
        expect('a visible host label', f'{DIR}/a.html', good_page.replace('<body>', '<body><span id="hostTag">Running on Amoebius</span>'), 'names the host', pc)
        expect('host-shaped text', f'{DIR}/a.html', good_page.replace('</script>', "x = PLATFORM.shell === 'prisma' ? 'Amoebius' : 'Web';</script>", 1), 'host shell', pc)
        expect('a page reading the hash itself', f'{DIR}/b.html', good_page.replace('</script>', "if (/^#prisma/.test(location.hash)) go();</script>", 1), 'reads the host itself', pc)
        expect('a raw WebGL renderer', f'{DIR}/b.html', good_page.replace('VesselStudioTheme.renderer(THREE, {})', 'new THREE.WebGLRenderer({ canvas })'), 'WebGLRenderer directly', pc)
        expect('a control hidden without a backend', f'{DIR}/index.html', good_page.replace('</script>', "if (!window.claude) $('askBtn').hidden = true;</script>", 1), 'hides a control', pc)
        expect('a page without the theme', f'{DIR}/b.html', '<body>' + TAG + '</body>', 'studio-theme.js', pc)
        expect('the Sync tag twice', f'{DIR}/a.html', good_page + TAG, 'more than once', pc)
        expect('OPEN IN AMOEBIUS on a file', LAUNCHER_STUDIOS, 'string? _studioOpenAtStart;\n        List<PageAction> StudioActions(StudioCatalog cat)\n        {\n            new("OPEN IN AMOEBIUS", true, "", () => OpenStudioWindow(page!)),\n        }\n', 'as a file', ec)
        expect('an app window on a file URI', LAUNCHER_CATALOG, 'AppWindowArgs(string pagePath, string p) { "--app=" + PageUri(pagePath), }', 'file URI', ec)
        expect('#prisma file pages', LAUNCHER_CATALOG, 'PageUri(string p) => new Uri(p).AbsoluteUri + "#prisma";', '#prisma', ec)
        expect('Unity opening a studio without Amoebius', UNITY_LAUNCH, 'internal static void OpenStudio(string file)\n        {\n            OpenStudioWindow(file);\n        }\n', 'through Amoebius', ec)
        expect('a home card opening the page as a file', UNITY_HOME, 'static void OpenPage(string file) { LaunchPrisma.OpenStudioWindow(StudioPath(file)); }', 'as a file', ec)
        # built: a page changed in the build
        subprocess.run(['git', 'init', '-q', tmp], check=True)
        subprocess.run(['git', '-C', tmp, '-c', 'user.email=t@t', '-c', 'user.name=t', 'add', '-A'], check=True)
        subprocess.run(['git', '-C', tmp, '-c', 'user.email=t@t', '-c', 'user.name=t', 'commit', '-qm', 'x'], check=True)
        out = os.path.join(tmp, 'out')
        shutil.copytree(os.path.join(tmp, DIR), out)
        with open(os.path.join(out, 'build.json'), 'w') as fh: json.dump({'pathSha': 'abc'}, fh)
        if built_checks(tmp, out):
            bad.append('a faithful build failed: ' + '; '.join(built_checks(tmp, out)))
        with open(os.path.join(out, 'a.html'), 'a') as fh: fh.write('<p>drift</p>')
        if not any('a.html: differs' in e for e in built_checks(tmp, out)):
            bad.append('a build that changed a page was not caught')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    bad += cards_self_test()
    print('self-test: ' + ('ok' if not bad else 'FAILED:\n  ' + '\n  '.join(bad)))
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--root', default='.')
    ap.add_argument('--ref', default='HEAD', help='the ref the build / mirror is compared against')
    ap.add_argument('--built')
    ap.add_argument('--served', help='a page URL Amoebius serves (needs --built)')
    ap.add_argument('--mirror', nargs='?', const=DEFAULT_MIRROR)
    ap.add_argument('--artifact', action='store_true')
    ap.add_argument('--strict', action='store_true', help='the artifact record is a failure, not a warning')
    ap.add_argument('--render', nargs='+', metavar='URL')
    ap.add_argument('--no-entry', action='store_true', help='skip the launcher / Unity entry-point checks')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        sys.exit(1 if self_test() else 0)
    root = os.path.abspath(a.root)
    errs, warns = [], []
    errs += page_checks(os.path.join(root, DIR), label=f'{DIR}/')
    src = os.path.join(root, STOAT_SRC)
    if os.path.exists(src):
        errs += [e.replace('index.html', STOAT_SRC) for e in page_checks(os.path.dirname(src), pages=[os.path.basename(src)], shared=[], label='') if 'StoatFlightStudio' in e]
    errs += card_checks(root, natives=not a.no_entry)
    if not a.no_entry:
        errs += entry_checks(root)
    if a.built:
        errs += built_checks(root, a.built, a.ref)
        if a.served:
            errs += served_checks(root, a.built, a.served)
    if a.mirror:
        e, w = mirror_checks(root, a.ref, a.mirror); errs += e; warns += w
    if a.artifact:
        (errs if a.strict else warns).extend(artifact_checks(root))
    if a.render:
        e, w = render_checks(a.render); errs += e; warns += w
    for w in warns:
        print('WARN', w)
    for e in errs:
        print('FAIL', e)
    print('parity: ' + ('ok' if not errs else f'{len(errs)} failure(s)'))
    sys.exit(1 if errs else 0)


if __name__ == '__main__':
    main()
