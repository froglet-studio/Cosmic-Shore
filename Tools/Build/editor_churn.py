#!/usr/bin/env python3
"""editor_churn.py - why does a clean Unity open leave `git status` dirty? (Editor Churn W1)

A READER: it never edits, stages, commits or discards anything in the checkout it reads. `measure` runs
the real Unity editor through Tools/Build/verify_unity.sh, which writes whatever Unity writes; that
churn is what is being measured.

  classify  Sort each dirty file into one category, name its likely writer, show the smallest part of
            the diff that matters, and say whether it is noise or real damage (a forked GUID, a nulled
            reference). Reads a live checkout, or the files a person brought:
              git status --porcelain > churn-status.txt && git diff > churn.patch
            python3 Tools/Build/editor_churn.py classify --repo <checkout>
            python3 Tools/Build/editor_churn.py classify --status churn-status.txt --patch churn.patch
  measure   Open the project in Unity (batchmode) once, then again, capturing `git status` and
            `git diff` after each open, and what the SECOND open changed on top of the first (a file
            that changes again on the second open is a writer that never settles).
            python3 Tools/Build/editor_churn.py measure --project <full clean checkout> [--opens 2]
  --self-test  The classifier and the snapshot code against synthetic churn, with negative controls.

Categories (EDITOR_CHURN_PLAN.md W1):
  missing-meta        an Assets/ or Packages/ file whose .meta is new, gone or has a changed guid
  generator-yaml      YAML a Tools/Build generator writes, not in the form Unity re-serializes
  package-settings    a package or settings file that rewrites itself (EDM4U, Vivox, URP, Burst, ...)
  version-skew        editor version, package lock, or only serializedVersion lines changed
  line-endings        only CR/LF or the file mode changed (git config, not Unity)
  per-machine         state that belongs to one machine (UserSettings/, Library/, IDE files)
  editor-on-load      an editor script that writes this asset when the editor loads
  unknown             none of the above: look at it by hand

Exit codes: classify 0 = nothing dirty, 1 = churn found, 3 = bad input.
            measure  0 = clean after every open, 1 = churn, 3 = setup problem, 4 = Unity gave no verdict.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import fnmatch
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field, asdict

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

CATEGORIES = [
    "missing-meta", "generator-yaml", "package-settings", "version-skew",
    "line-endings", "per-machine", "editor-on-load", "unknown",
]

# Unity's YAML (and the text files Unity rewrites) - the extensions a generator or the editor serializes.
YAML_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller", ".anim", ".overrideController",
            ".physicMaterial", ".mixer", ".playable", ".signal", ".preset", ".lighting", ".renderTexture",
            ".spriteatlas", ".spriteatlasv2", ".terrainlayer", ".brush", ".flare", ".guiskin",
            ".fontsettings", ".mask", ".cubemap", ".shadervariants", ".giparams")

# State that belongs to one machine. Matched against the repo-relative path.
PER_MACHINE = [
    ("UserSettings/*", "Unity editor (per-user settings)"),
    ("Library/*", "Unity editor (import cache)"),
    ("Temp/*", "Unity editor (temp)"),
    ("Logs/*", "Unity editor (logs)"),
    ("obj/*", "IDE build output"),
    (".vs/*", "Visual Studio"), (".idea/*", "Rider"), (".vscode/*", "VS Code"),
    ("*.csproj", "IDE project generation"), ("*.sln", "IDE project generation"),
    ("*.slnx", "IDE project generation"),
    ("*.DS_Store", "macOS Finder"), ("*Thumbs.db", "Windows Explorer"), ("*desktop.ini", "Windows Explorer"),
    ("Assets/Plugins/FMOD/Cache/*", "FMOD (per-machine cache)"),
]

# Known package / settings auto-writers. First match wins; the generic ProjectSettings/* row is last.
PACKAGE_WRITERS = [
    ("ProjectSettings/GvhProjectSettings.xml", "EDM4U (External Dependency Manager) settings"),
    ("ProjectSettings/AndroidResolverDependencies.xml", "EDM4U Android resolver"),
    ("Assets/Plugins/Android/*.gradle", "EDM4U Android resolver (gradle templates)"),
    ("Assets/Plugins/Android/*.properties", "EDM4U Android resolver (gradle templates)"),
    ("Assets/Plugins/Android/AndroidManifest.xml", "EDM4U Android resolver / Android manifest merge"),
    ("Assets/Plugins/iOS/*", "EDM4U iOS resolver"),
    ("ProjectSettings/Packages/com.unity.services.vivox/*", "Vivox package settings"),
    ("ProjectSettings/Packages/*", "package settings (ProjectSettings/Packages)"),
    ("ProjectSettings/URPProjectSettings.asset", "URP material upgrader"),
    ("ProjectSettings/*Burst*.json", "Burst AOT settings"),
    ("ProjectSettings/SceneTemplateSettings.json", "Scene Template settings"),
    ("ProjectSettings/CanvasUpgraderUpgradedPrefabs.txt", "uGUI canvas upgrader"),
    ("ProjectSettings/ShaderGraphSettings.asset", "Shader Graph settings"),
    ("ProjectSettings/PackageManagerSettings.asset", "Package Manager"),
    ("ProjectSettings/*", "Unity editor (project settings)"),
]

VERSION_FILES = ["ProjectSettings/ProjectVersion.txt", "Packages/packages-lock.json", "Packages/manifest.json"]

# Files a Tools/Build generator writes but the generic ProjectSettings/* rule would otherwise claim.
GENERATED_SETTINGS = ["ProjectSettings/EditorBuildSettings.asset"]

# Editor code that writes on load (EDITOR_CHURN_PLAN.md W6).
ON_LOAD_MARKERS = r"InitializeOnLoad|EditorApplication\.delayCall|AssetDatabase\.SaveAssets|" \
                  r"EditorUtility\.SetDirty|AssetPostprocessor|EditorBuildSettings\.scenes\s*="

HARM_NOISE = "noise"


# ------------------------------------------------------------------------------------------ parsing

@dataclass
class FileDiff:
    path: str
    old_path: str = ""
    removed: list = field(default_factory=list)
    added: list = field(default_factory=list)
    new_file: bool = False
    deleted: bool = False
    binary: bool = False
    mode_change: str = ""


def parse_status(text: str) -> list[tuple[str, str]]:
    """`git status --porcelain` (v1) -> [(XY, path)]. Renames give the new path."""
    out = []
    for line in text.splitlines():
        if len(line) < 4 or line.startswith("##"):
            continue
        xy, path = line[:2], line[3:]
        if " -> " in path:
            path = path.split(" -> ", 1)[1]
        path = path.strip()
        if path.startswith('"') and path.endswith('"'):
            path = path[1:-1].encode("latin-1", "backslashreplace").decode("unicode_escape") \
                .encode("latin-1", "replace").decode("utf-8", "replace")
        out.append((xy, path))
    return out


def parse_patch(text: str) -> dict[str, FileDiff]:
    files: dict[str, FileDiff] = {}
    cur: FileDiff | None = None
    in_hunk = False
    for raw in text.split("\n"):
        line = raw[:-1] if raw.endswith("\n") else raw
        if line.startswith("diff --git "):
            m = re.match(r'diff --git "?a/(.*?)"? "?b/(.*?)"?$', line)
            if not m:
                cur = None
                continue
            cur = FileDiff(path=m.group(2), old_path=m.group(1))
            files[cur.path] = cur
            in_hunk = False
            continue
        if cur is None:
            continue
        if line.startswith("@@"):
            in_hunk = True
            continue
        if not in_hunk:
            if line.startswith("new file mode"):
                cur.new_file = True
            elif line.startswith("deleted file mode"):
                cur.deleted = True
            elif line.startswith("old mode "):
                cur.mode_change = line[len("old mode "):]
            elif line.startswith("new mode "):
                cur.mode_change += " -> " + line[len("new mode "):]
            elif line.startswith("Binary files") or line.startswith("GIT binary patch"):
                cur.binary = True
            elif line.startswith("rename to "):
                cur.path = line[len("rename to "):]
            continue
        if line.startswith("-") and not line.startswith("---"):
            cur.removed.append(line[1:])
        elif line.startswith("+") and not line.startswith("+++"):
            cur.added.append(line[1:])
        elif line.startswith("\\"):
            pass
    return files


# ------------------------------------------------------------------------------------------ writers

class WriterIndex:
    """Which Tools/Build generators and which on-load editor scripts mention a file.

    `generators` and `editor_scripts` map a repo-relative path to its text. Built lazily from the repo;
    the self-test injects its own."""

    def __init__(self, repo: str | None = None, generators=None, editor_scripts=None, runtime_scripts=None):
        self.repo = repo
        self._gen = generators
        self._ed = editor_scripts
        self._rt = runtime_scripts

    def _load(self):
        if self._gen is not None:
            return
        self._gen, self._ed = {}, {}
        if not self.repo:
            return
        build = os.path.join(self.repo, "Tools", "Build")
        for root, _dirs, names in os.walk(build):
            for n in names:
                if n.endswith((".py", ".sh")) and n != os.path.basename(__file__):
                    p = os.path.join(root, n)
                    try:
                        with open(p, encoding="utf-8", errors="replace") as f:
                            self._gen[os.path.relpath(p, self.repo)] = f.read()
                    except OSError:
                        pass
        try:
            hits = subprocess.run(["git", "-C", self.repo, "grep", "-l", "-E", ON_LOAD_MARKERS, "--", "*.cs"],
                                  capture_output=True, text=True, timeout=120).stdout.split()
        except (OSError, subprocess.SubprocessError):
            hits = []
        for rel in hits:
            if "/Editor/" not in "/" + rel and not rel.startswith("Editor/"):
                continue
            try:
                with open(os.path.join(self.repo, rel), encoding="utf-8", errors="replace") as f:
                    self._ed[rel] = f.read()
            except OSError:
                pass

    @staticmethod
    def _needles(path: str) -> list[str]:
        base = os.path.basename(path)
        stem = os.path.splitext(base)[0]
        needles = [path, base]
        # a generator usually builds the name: "ConfigureView" + ".prefab"; require a distinctive stem
        if len(stem) >= 8 and not stem.isdigit():
            needles.append(stem)
        return needles

    def _search(self, corpus: dict, path: str) -> list[str]:
        needles = self._needles(path)
        exact = [k for k, t in corpus.items() if any(n in t for n in needles[:2])]
        if exact:
            return sorted(exact)
        if len(needles) > 2:
            pat = re.compile(r"(?<![A-Za-z0-9_])" + re.escape(needles[2]) + r"(?![A-Za-z0-9_])")
            return sorted(k for k, t in corpus.items() if pat.search(t))
        return []

    def generators_for(self, path: str) -> list[str]:
        self._load()
        return self._search(self._gen, path)

    def editor_scripts_for(self, path: str) -> list[str]:
        self._load()
        return self._search(self._ed, path)

    def runtime_scripts_for(self, path: str) -> list[str]:
        """Runtime C# that names the asset (Resources.Load and the like): a hint, not a writer. A shared
        asset that runtime code changes in Play mode is saved back to disk by the editor."""
        stem = os.path.splitext(os.path.basename(path))[0]
        if len(stem) < 8:
            return []
        if self._rt is not None:
            return sorted(k for k, t in self._rt.items() if stem in t)
        if not self.repo:
            return []
        try:
            out = subprocess.run(["git", "-C", self.repo, "grep", "-l", "-w", "-F", stem, "--", "*.cs"],
                                 capture_output=True, text=True, timeout=60).stdout.split("\n")
        except (OSError, subprocess.SubprocessError):
            return []
        return sorted(l for l in out if l and "/Editor/" not in "/" + l)


# ------------------------------------------------------------------------------------------ classify

@dataclass
class Row:
    path: str
    status: str
    category: str
    writer: str
    harm: str
    excerpt: list
    note: str = ""


def _match(path: str, patterns) -> str | None:
    for pat, who in patterns:
        if fnmatch.fnmatchcase(path, pat):
            return who
    return None


def _norm(lines):
    return [l.rstrip("\r") for l in lines]


def _excerpt(d: FileDiff | None, limit: int = 8) -> list[str]:
    """The smallest part of the diff that matters: changed lines whose content really changed (a line
    removed and re-added unchanged except CR is dropped), paired -/+ where possible, capped."""
    if d is None:
        return []
    rem, add = _norm(d.removed), _norm(d.added)
    common = set(rem) & set(add)
    rem = [l for l in rem if l not in common]
    add = [l for l in add if l not in common]
    out = []
    for i in range(max(len(rem), len(add))):
        if i < len(rem):
            out.append("-" + rem[i].strip()[:160])
        if i < len(add):
            out.append("+" + add[i].strip()[:160])
        if len(out) >= limit:
            break
    extra = len(rem) + len(add) - len(out)
    if extra > 0:
        out.append(f"... {extra} more changed line(s)")
    return out


_REF = re.compile(r"\{fileID: *(-?\d+)(?:, *guid: *([0-9a-f]{32}))?")
_GUID = re.compile(r"^guid: *([0-9a-f]{32})\s*$")


def nulled_references(d: FileDiff | None) -> int:
    """How many object references the change turned into {fileID: 0}: a removed line that pointed at
    something and an added line, same key, that points at nothing."""
    if d is None:
        return 0

    def keyed(lines):
        out = []
        for l in _norm(lines):
            m = _REF.search(l)
            if m:
                out.append((l[:m.start()].strip(), m.group(1)))
        return out

    before = keyed(d.removed)
    after = keyed(d.added)
    n = 0
    pool = list(before)
    for key, fid in after:
        if fid != "0":
            continue
        for i, (k2, f2) in enumerate(pool):
            if k2 == key and f2 != "0":
                n += 1
                del pool[i]
                break
    return n


def meta_guid_change(d: FileDiff | None) -> tuple[str, str] | None:
    if d is None:
        return None
    old = [m.group(1) for m in (_GUID.match(l) for l in _norm(d.removed)) if m]
    new = [m.group(1) for m in (_GUID.match(l) for l in _norm(d.added)) if m]
    if old and new and old[0] != new[0]:
        return old[0], new[0]
    return None


def only_version_lines(d: FileDiff) -> bool:
    rem, add = _norm(d.removed), _norm(d.added)
    if not rem and not add:
        return False
    pat = re.compile(r"^\s*(serializedVersion|m_EditorVersion|m_EditorVersionWithRevision|"
                     r"m_LastMaterialVersion|version|m_Version)\s*:")
    return all(pat.match(l) for l in rem + add)


def only_line_endings(d: FileDiff) -> bool:
    if d.binary or d.new_file or d.deleted:
        return False
    rem, add = d.removed, d.added
    if not rem and not add:
        return bool(d.mode_change)
    if len(rem) != len(add):
        return False
    return _norm(rem) == _norm(add) and (rem != add)


def classify_file(xy: str, path: str, d: FileDiff | None, idx: WriterIndex,
                  tracked: set[str] | None = None, disk_guid=None, guid_refs=None, have_patch=True) -> Row:
    status = xy.strip() or "M"
    untracked = xy == "??"
    excerpt = _excerpt(d)

    def row(cat, writer, harm=HARM_NOISE, note=""):
        return Row(path, status, cat, writer, harm, excerpt, note)

    # untracked folder from a plain `git status --porcelain`: the files inside are not listed
    if untracked and path.endswith("/"):
        who = _match(path + "x", PER_MACHINE)
        if who:
            return row("per-machine", who)
        return row("unknown", "?", note="untracked folder: rerun `git status --porcelain --untracked-files=all` "
                                         "to see the files in it")

    who = _match(path, PER_MACHINE)
    if who:
        return row("per-machine", who)

    if d is not None and d.mode_change and not d.removed and not d.added:
        return row("line-endings", "git core.fileMode", note=f"file mode only ({d.mode_change})")
    if d is not None and only_line_endings(d):
        return row("line-endings", "git core.autocrlf / .gitattributes", note="CR/LF only")
    if have_patch and d is None and not untracked and "M" in status and path not in VERSION_FILES:
        # listed as modified with no content diff: almost always a CRLF or stat-only difference
        return row("line-endings", "git core.autocrlf / .gitattributes",
                   note="modified in git status but no content diff (stat-only or CR/LF)")

    if path in VERSION_FILES:
        return row("version-skew", "Unity editor version / Package Manager",
                   note="compare the editor version and packages-lock.json with what is pinned")

    # .meta files
    if path.endswith(".meta"):
        asset = path[:-5]
        change = meta_guid_change(d)
        if change:
            return row("missing-meta", "Unity editor (asset import)",
                       harm=f"damage: forked GUID ({change[0]} -> {change[1]})",
                       note=f"references to {change[0]} break on this machine")
        if untracked or (d is not None and d.new_file):
            asset_tracked = tracked is not None and asset in tracked
            note = "the asset is tracked without its .meta" if asset_tracked else "new .meta"
            harm = "damage: each machine invents its own random GUID"
            if disk_guid:
                refs = guid_refs.get(disk_guid, 0) if guid_refs is not None else None
                note += f"; this machine's guid {disk_guid}"
                if refs:
                    note += f" is referenced by {refs} tracked file(s): commit THIS guid"
                elif refs == 0:
                    note += " is referenced nowhere: find the guid existing references use before committing"
            return row("missing-meta", "Unity editor (asset import)", harm=harm, note=note)
        if "D" in status or (d is not None and d.deleted):
            return row("missing-meta", "Unity editor (asset import)",
                       harm="damage: references to this guid break",
                       note="a .meta deleted (orphaned by a moved or deleted asset?)")
        if d is not None and only_version_lines(d):
            return row("version-skew", "Unity editor (importer upgrade)", note="serializedVersion only")
        gens = idx.generators_for(asset)
        if gens:
            return row("generator-yaml", ", ".join(gens[:4]) + (" ..." if len(gens) > 4 else ""),
                       note="importer settings differ from what the generator wrote")
        return row("unknown", "Unity editor (importer settings)", note="importer settings rewritten")

    nulled = nulled_references(d)
    harm = f"damage: {nulled} reference(s) nulled to {{fileID: 0}}" if nulled else HARM_NOISE

    if d is not None and only_version_lines(d):
        return row("version-skew", "Unity editor (serialization upgrade)", harm,
                   note="only version fields changed")

    if path not in GENERATED_SETTINGS:
        who = _match(path, PACKAGE_WRITERS)
        if who:
            return row("package-settings", who, harm)

    gens = idx.generators_for(path)
    if gens:
        return row("generator-yaml", ", ".join(gens[:4]) + (f" (+{len(gens) - 4} more)" if len(gens) > 4 else ""),
                   harm, note="re-serialized by Unity: the generator's output is not canonical")

    eds = idx.editor_scripts_for(path)
    if eds:
        return row("editor-on-load", ", ".join(eds[:3]), harm)

    if untracked and path.startswith("Assets/") and tracked is not None and (path + ".meta") not in tracked:
        return row("unknown", "?", harm, note="an untracked asset: created by the editor or a package?")
    rts = [] if untracked else idx.runtime_scripts_for(path)
    if rts:
        return row("unknown", "runtime code? " + ", ".join(rts[:3]) + (" ..." if len(rts) > 3 else ""), harm,
                   note="no generator or on-load script names it, but runtime code does: does it change this "
                        "shared asset in Play mode?")
    return row("unknown", "?", harm)


def classify(status_text: str, patch_text: str, idx: WriterIndex, tracked=None, disk_guids=None,
             guid_refs=None) -> list[Row]:
    entries = parse_status(status_text)
    diffs = parse_patch(patch_text)
    seen = {p for _xy, p in entries}
    # a patch can carry files the status file does not (git diff HEAD after staging): keep them
    for p in diffs:
        if p not in seen:
            entries.append((" M", p))
    have_patch = bool(patch_text.strip())
    rows = []
    for xy, path in entries:
        g = (disk_guids or {}).get(path)
        r = classify_file(xy, path, diffs.get(path), idx, tracked, g, guid_refs, have_patch)
        if not have_patch and not r.note:
            r.note = "no diff supplied: the category is read from the path alone"
        rows.append(r)
    order = {c: i for i, c in enumerate(CATEGORIES)}
    rows.sort(key=lambda r: (0 if r.harm != HARM_NOISE else 1, order[r.category], r.path))
    return rows


# ------------------------------------------------------------------------------------------ git helpers

def git(repo, *args, env=None, check=True) -> str:
    r = subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True, env=env,
                       encoding="utf-8", errors="replace")
    if check and r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)}: {r.stderr.strip()}")
    return r.stdout


def live_inputs(repo: str):
    """status, patch, tracked set, guid of each untracked .meta, and how often tracked files cite each guid."""
    status = git(repo, "status", "--porcelain", "--untracked-files=all")
    patch = git(repo, "diff", "--no-color", "--no-ext-diff")
    tracked = set(git(repo, "ls-files").splitlines())
    disk_guids = {}
    for xy, path in parse_status(status):
        if xy == "??" and path.endswith(".meta"):
            try:
                with open(os.path.join(repo, path), encoding="utf-8", errors="replace") as f:
                    for line in f:
                        m = _GUID.match(line.strip())
                        if m:
                            disk_guids[path] = m.group(1)
                            break
            except OSError:
                pass
    guid_refs = {}
    for g in set(disk_guids.values()):
        r = subprocess.run(["git", "-C", repo, "grep", "-l", "-F", g], capture_output=True, text=True)
        guid_refs[g] = len([l for l in r.stdout.splitlines() if not l.endswith(".meta")])
    return status, patch, tracked, disk_guids, guid_refs


def snapshot_tree(repo: str, index_file: str) -> str:
    """The working tree as a git tree object (tracked + untracked, honouring .gitignore), written with a
    private index so the checkout's own index is never touched."""
    env = dict(os.environ, GIT_INDEX_FILE=index_file)
    git(repo, "read-tree", "HEAD", env=env)
    git(repo, "add", "-A", env=env)
    return git(repo, "write-tree", env=env).strip()


# ------------------------------------------------------------------------------------------ reporting

def counts(rows):
    c = {k: 0 for k in CATEGORIES}
    for r in rows:
        c[r.category] += 1
    return c


def render_text(rows, title="") -> str:
    if not rows:
        return (title + "\n" if title else "") + "clean: nothing dirty\n"
    out = []
    if title:
        out.append(title)
    dmg = sum(1 for r in rows if r.harm != HARM_NOISE)
    c = counts(rows)
    out.append(f"{len(rows)} dirty file(s), {dmg} with real damage. " +
               ", ".join(f"{k} {v}" for k, v in c.items() if v))
    for cat in CATEGORIES:
        grp = [r for r in rows if r.category == cat]
        if not grp:
            continue
        out.append(f"\n== {cat} ({len(grp)})")
        for r in grp:
            flag = "!! " if r.harm != HARM_NOISE else "   "
            out.append(f"{flag}[{r.status:>2}] {r.path}")
            out.append(f"       writer: {r.writer}")
            if r.harm != HARM_NOISE:
                out.append(f"       harm:   {r.harm}")
            if r.note:
                out.append(f"       note:   {r.note}")
            for e in r.excerpt:
                out.append(f"         {e}")
    return "\n".join(out) + "\n"


def render_md(rows) -> str:
    """Rows in the tracker's file-table shape, status `seen` (the person or harness saw it change)."""
    head = "| path | category | writer | trigger | harm | fix | gate | status | commit |\n" \
           "|---|---|---|---|---|---|---|---|---|\n"
    body = "".join(f"| `{r.path}` | {r.category} | {r.writer.replace('|', '/')} | ? | {r.harm} | | | seen | |\n"
                   for r in rows)
    return head + body


# ------------------------------------------------------------------------------------------ measure

def unity_version(project):
    try:
        with open(os.path.join(project, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as f:
            for line in f:
                if line.startswith("m_EditorVersion:"):
                    return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return "?"


def run_measure(a) -> int:
    project = os.path.abspath(a.project)
    if not os.path.isfile(os.path.join(project, "ProjectSettings", "ProjectVersion.txt")):
        print(f"editor_churn: {project} is not a Unity project", file=sys.stderr)
        return 3
    try:
        head = git(project, "rev-parse", "HEAD").strip()
    except RuntimeError as e:
        print(f"editor_churn: {e}", file=sys.stderr)
        return 3
    start = git(project, "status", "--porcelain", "--untracked-files=all")
    if start.strip() and not a.allow_dirty:
        print("editor_churn: the checkout is dirty before any Unity open; measure needs a clean one "
              "(clone fresh, or pass --allow-dirty to measure on top of it):", file=sys.stderr)
        print(start, file=sys.stderr)
        return 3
    logs = os.path.abspath(a.logs or os.path.join(tempfile.gettempdir(),
                                                  "editor_churn." + _dt.datetime.now().strftime("%Y%m%d-%H%M%S")))
    os.makedirs(logs, exist_ok=True)
    verify = a.verify or os.path.join(REPO, "Tools", "Build", "verify_unity.sh")
    had_library = os.path.isdir(os.path.join(project, "Library"))
    idx = WriterIndex(project)
    index_file = os.path.join(logs, "snapshot.index")
    prev_tree = snapshot_tree(project, index_file) if start.strip() else git(project, "rev-parse", "HEAD^{tree}").strip()
    summary = {
        "date": _dt.date.today().isoformat(), "machine": a.machine or platform.node(),
        "os": f"{platform.system()} {platform.release()}", "commit": head[:9],
        "unity": unity_version(project), "library_before": had_library, "logs": logs,
        "dirty_before": len(parse_status(start)), "opens": [],
    }
    worst = 0
    for i in range(1, a.opens + 1):
        odir = os.path.join(logs, f"open{i}")
        os.makedirs(odir, exist_ok=True)
        print(f"editor_churn: open {i}/{a.opens} ...", flush=True)
        cmd = ["bash", verify, "--project", project, "--no-tests", "--logs", odir, "--timeout", str(a.timeout)]
        r = subprocess.run(cmd, capture_output=True, text=True)
        with open(os.path.join(odir, "verify_unity.out"), "w", encoding="utf-8") as f:
            f.write(r.stdout + r.stderr)
        if r.returncode == 3:
            print((r.stdout + r.stderr).strip(), file=sys.stderr)
            return 3
        status, patch, tracked, disk_guids, guid_refs = live_inputs(project)
        tree = snapshot_tree(project, index_file)
        delta_names = git(project, "diff", "--name-status", prev_tree, tree)
        delta_patch = git(project, "diff", "--no-color", prev_tree, tree)
        for name, text in (("status.txt", status), ("churn.patch", patch),
                           ("delta-status.txt", delta_names), ("delta.patch", delta_patch)):
            with open(os.path.join(odir, name), "w", encoding="utf-8") as f:
                f.write(text)
        rows = classify(status, patch, idx, tracked, disk_guids, guid_refs)
        # the delta as porcelain-shaped status so it classifies the same way
        delta_status = "".join(f"{l.split(chr(9))[0][0]:>2} {l.split(chr(9))[-1]}\n"
                               for l in delta_names.splitlines() if l.strip())
        delta_rows = classify(delta_status, delta_patch, idx, tracked, disk_guids, guid_refs)
        with open(os.path.join(odir, "classified.txt"), "w", encoding="utf-8") as f:
            f.write(render_text(rows, f"after open {i} (all dirty files)"))
            f.write("\n" + render_text(delta_rows, f"changed BY open {i}"))
        summary["opens"].append({
            "open": i, "verify_unity_exit": r.returncode, "verdict": (r.stdout.strip().splitlines() or ["?"])[0],
            "dirty": len(rows), "changed_by_this_open": len(delta_rows),
            "damage": sum(1 for x in rows if x.harm != HARM_NOISE),
            "by_category": counts(rows), "rows": [asdict(x) for x in rows],
            "delta": [asdict(x) for x in delta_rows],
        })
        print(render_text(delta_rows, f"-- changed BY open {i}"))
        prev_tree = tree
        if r.returncode == 4:
            worst = 4
    try:
        os.remove(index_file)
    except OSError:
        pass
    with open(os.path.join(logs, "summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    md = measurement_md(summary)
    with open(os.path.join(logs, "summary.md"), "w", encoding="utf-8") as f:
        f.write(md)
    print(md)
    if worst:
        return worst
    return 0 if all(o["dirty"] == 0 for o in summary["opens"]) else 1


def measurement_md(s) -> str:
    o = s["opens"]
    first = o[0]["dirty"] if o else "?"
    last = o[-1]["dirty"] if o else "?"
    second = o[1]["changed_by_this_open"] if len(o) > 1 else "n/a"
    lib = "warm Library" if s["library_before"] else "fresh (no Library)"
    line = (f"| {s['date']} | {s['machine']} | {s['os']} | {s['unity']} | `{s['commit']}` | {lib} | "
            f"{first} | {last} | {second} | harness |")
    return ("Tracker measurement row (paste under ## Measurements):\n\n"
            "| date | machine | OS | Unity | commit | Library | dirty after open 1 | dirty after last open | "
            "changed by open 2 | source |\n|---|---|---|---|---|---|---|---|---|---|\n" + line + "\n")


# ------------------------------------------------------------------------------------------ self-test

def self_test() -> int:
    fails = []

    def check(cond, msg):
        if not cond:
            fails.append(msg)

    idx = WriterIndex(generators={
        "Tools/Build/author_menu_configure.py": 'PREFAB = "Assets/_Prefabs/UI/ConfigureView.prefab"\n',
        "Tools/Build/author_redline_assets.py": 'EBS = ROOT / "ProjectSettings" / "EditorBuildSettings.asset"\n',
        "Tools/Build/author_switchback_assets.py": 'MAT = "SwitchbackPathRay" + ".mat"\n',
    }, editor_scripts={
        "Assets/_Scripts/Editor/ThemeBootstrap.cs": '[InitializeOnLoad] ... "Assets/Settings/FrogletTheme.asset"',
    }, runtime_scripts={
        "Assets/_Scripts/Ray/RayPaint.cs": 'Resources.Load<Material>("GlowyRayMaterial")',
    })
    g_old, g_new = "a" * 32, "b" * 32
    status = "\n".join([
        " M Assets/_Prefabs/UI/ConfigureView.prefab",
        "?? Assets/_Scripts/Regatta/RegattaCourseFeatures.cs.meta",
        " M Assets/_Scripts/Old/Thing.cs.meta",
        " M ProjectSettings/GvhProjectSettings.xml",
        " M ProjectSettings/Packages/com.unity.services.vivox/Settings.json",
        " M ProjectSettings/EditorBuildSettings.asset",
        " M Assets/Resources/SwitchbackPathRay.mat",
        " M ProjectSettings/ProjectVersion.txt",
        " M Assets/Scenes/CRLF.unity",
        " M Assets/Tools/run.sh",
        "?? UserSettings/EditorUserSettings.asset",
        " M Assets/Settings/FrogletTheme.asset",
        " M Assets/Art/Upgraded.mat",
        " M Assets/Art/Mystery.asset",
        "?? Assets/NewFolder/",
        " M Assets/Scenes/Menu_Main.unity",
        " M Assets/Resources/GlowyRayMaterial.mat",
    ]) + "\n"
    patch = "\n".join([
        "diff --git a/Assets/_Prefabs/UI/ConfigureView.prefab b/Assets/_Prefabs/UI/ConfigureView.prefab",
        "--- a/Assets/_Prefabs/UI/ConfigureView.prefab", "+++ b/Assets/_Prefabs/UI/ConfigureView.prefab",
        "@@ -1,3 +1,4 @@", "   m_Name: ConfigureView", "+  m_IsActive: 1", "-  m_Layer: 5", "+  m_Layer: 5 ",
        "diff --git a/Assets/_Scripts/Old/Thing.cs.meta b/Assets/_Scripts/Old/Thing.cs.meta",
        "@@ -1,2 +1,2 @@", " fileFormatVersion: 2", f"-guid: {g_old}", f"+guid: {g_new}",
        "diff --git a/ProjectSettings/GvhProjectSettings.xml b/ProjectSettings/GvhProjectSettings.xml",
        "@@ -1 +1 @@", '-<projectSetting name="a" value="False" />', '+<projectSetting name="a" value="True" />',
        "diff --git a/ProjectSettings/Packages/com.unity.services.vivox/Settings.json b/ProjectSettings/Packages/com.unity.services.vivox/Settings.json",
        "@@ -1 +1,3 @@", "-{}", '+{', '+  "m_Dictionary": {}', "+}",
        "diff --git a/ProjectSettings/EditorBuildSettings.asset b/ProjectSettings/EditorBuildSettings.asset",
        "@@ -5 +5 @@", "-    path: Assets/_Scenes/A.unity", "+    path: Assets/_Scenes/B.unity",
        "diff --git a/Assets/Resources/SwitchbackPathRay.mat b/Assets/Resources/SwitchbackPathRay.mat",
        "@@ -9 +9 @@", "-    - _Glow: 1", "+    - _Glow: 1.0000001",
        "diff --git a/ProjectSettings/ProjectVersion.txt b/ProjectSettings/ProjectVersion.txt",
        "@@ -1 +1 @@", "-m_EditorVersion: 6000.3.17f1", "+m_EditorVersion: 6000.3.18f1",
        "diff --git a/Assets/Scenes/CRLF.unity b/Assets/Scenes/CRLF.unity",
        "@@ -1 +1 @@", "-  m_Name: X", "+  m_Name: X\r",
        "diff --git a/Assets/Tools/run.sh b/Assets/Tools/run.sh", "old mode 100644", "new mode 100755",
        "diff --git a/Assets/Settings/FrogletTheme.asset b/Assets/Settings/FrogletTheme.asset",
        "@@ -3 +3 @@", "-  accent: 1", "+  accent: 2",
        "diff --git a/Assets/Art/Upgraded.mat b/Assets/Art/Upgraded.mat",
        "@@ -2 +2 @@", "-  serializedVersion: 6", "+  serializedVersion: 8",
        "diff --git a/Assets/Art/Mystery.asset b/Assets/Art/Mystery.asset",
        "@@ -2 +2 @@", "-  speed: 3", "+  speed: 4",
        "diff --git a/Assets/Scenes/Menu_Main.unity b/Assets/Scenes/Menu_Main.unity",
        "@@ -2,2 +2,2 @@",
        f"-      objectReference: {{fileID: 11400000, guid: {g_old}, type: 2}}",
        "+      objectReference: {fileID: 0}",
        "diff --git a/Assets/Resources/GlowyRayMaterial.mat b/Assets/Resources/GlowyRayMaterial.mat",
        "@@ -9 +9 @@", "-    - _BaseColor: {r: 1, g: 0, b: 0, a: 1}", "+    - _BaseColor: {r: 0, g: 1, b: 0, a: 1}",
    ]) + "\n"
    tracked = {"Assets/_Scripts/Regatta/RegattaCourseFeatures.cs"}
    rows = {r.path: r for r in classify(status, patch, idx, tracked,
                                        {"Assets/_Scripts/Regatta/RegattaCourseFeatures.cs.meta": g_new},
                                        {g_new: 3})}
    expect = {
        "Assets/_Prefabs/UI/ConfigureView.prefab": ("generator-yaml", "author_menu_configure.py"),
        "Assets/_Scripts/Regatta/RegattaCourseFeatures.cs.meta": ("missing-meta", "asset import"),
        "Assets/_Scripts/Old/Thing.cs.meta": ("missing-meta", "asset import"),
        "ProjectSettings/GvhProjectSettings.xml": ("package-settings", "EDM4U"),
        "ProjectSettings/Packages/com.unity.services.vivox/Settings.json": ("package-settings", "Vivox"),
        "ProjectSettings/EditorBuildSettings.asset": ("generator-yaml", "author_redline_assets.py"),
        "Assets/Resources/SwitchbackPathRay.mat": ("generator-yaml", "author_switchback_assets.py"),
        "ProjectSettings/ProjectVersion.txt": ("version-skew", "version"),
        "Assets/Scenes/CRLF.unity": ("line-endings", "autocrlf"),
        "Assets/Tools/run.sh": ("line-endings", "fileMode"),
        "UserSettings/EditorUserSettings.asset": ("per-machine", "per-user"),
        "Assets/Settings/FrogletTheme.asset": ("editor-on-load", "ThemeBootstrap.cs"),
        "Assets/Art/Upgraded.mat": ("version-skew", "serialization"),
        "Assets/Art/Mystery.asset": ("unknown", "?"),
        "Assets/NewFolder/": ("unknown", "?"),
        "Assets/Scenes/Menu_Main.unity": ("unknown", "?"),
        "Assets/Resources/GlowyRayMaterial.mat": ("unknown", "RayPaint.cs"),
    }
    for path, (cat, writer) in expect.items():
        r = rows.get(path)
        check(r is not None, f"{path}: not classified")
        if r:
            check(r.category == cat, f"{path}: category {r.category}, want {cat}")
            check(writer in r.writer, f"{path}: writer {r.writer!r}, want it to name {writer!r}")
    check("forked GUID" in rows["Assets/_Scripts/Old/Thing.cs.meta"].harm, "forked guid not flagged as damage")
    check("commit THIS guid" in rows["Assets/_Scripts/Regatta/RegattaCourseFeatures.cs.meta"].note,
          "referenced guid of a missing meta not reported")
    check("tracked without its .meta" in rows["Assets/_Scripts/Regatta/RegattaCourseFeatures.cs.meta"].note,
          "tracked asset without meta not reported")
    check("1 reference(s) nulled" in rows["Assets/Scenes/Menu_Main.unity"].harm, "nulled reference not flagged")
    check(rows["Assets/Resources/SwitchbackPathRay.mat"].harm == HARM_NOISE, "a float change flagged as damage")
    # the smallest part that matters: the trailing-space re-add of m_Layer stays, the unchanged name goes
    ex = rows["Assets/_Prefabs/UI/ConfigureView.prefab"].excerpt
    check("+m_IsActive: 1" in ex, f"excerpt lost the real change: {ex}")
    check(not any("m_Name" in e for e in ex), f"excerpt carries context lines: {ex}")
    check("-m_Layer: 5" in ex and "+m_Layer: 5" in ex, f"excerpt dropped a whitespace change: {ex}")
    # damage sorts first
    order = [r.path for r in classify(status, patch, idx, tracked)]
    check(order[0] in ("Assets/_Scripts/Old/Thing.cs.meta", "Assets/_Scripts/Regatta/RegattaCourseFeatures.cs.meta",
                       "Assets/Scenes/Menu_Main.unity"), f"damage is not listed first: {order[:3]}")

    # negative controls: each rule must NOT fire on the shape next to it
    neg_status = " M Assets/Scenes/RealEdit.unity\n M Assets/Art/Short.mat\n"
    neg_patch = "\n".join([
        "diff --git a/Assets/Scenes/RealEdit.unity b/Assets/Scenes/RealEdit.unity",
        "@@ -1 +1 @@", "-  m_Name: X\r", "+  m_Name: Y\r",  # CRLF on both sides, real change
        "diff --git a/Assets/Art/Short.mat b/Assets/Art/Short.mat",
        "@@ -1,2 +1,2 @@", "-  serializedVersion: 6", "+  serializedVersion: 8", "-  _Glow: 1", "+  _Glow: 2",
    ]) + "\n"
    neg_idx = WriterIndex(generators={"Tools/Build/author_x.py": "Short = 1  # a stem shorter than 8 chars"},
                          editor_scripts={})
    nrows = {r.path: r for r in classify(neg_status, neg_patch, neg_idx)}
    check(nrows["Assets/Scenes/RealEdit.unity"].category == "unknown",
          f"a real edit with CRLF was called {nrows['Assets/Scenes/RealEdit.unity'].category}")
    check(nrows["Assets/Art/Short.mat"].category == "unknown",
          f"a version bump plus a value change was called {nrows['Assets/Art/Short.mat'].category} "
          f"(writer {nrows['Assets/Art/Short.mat'].writer})")
    unchanged_guid = parse_patch("diff --git a/x.meta b/x.meta\n@@ -1 +1 @@\n-guid: " + g_old + "\n+guid: " + g_old + "\r\n")
    check(meta_guid_change(unchanged_guid["x.meta"]) is None, "a CR-only guid line read as a forked guid")
    check(nulled_references(parse_patch(
        "diff --git a/s.unity b/s.unity\n@@ -1 +1 @@\n-  a: {fileID: 0}\n+  b: {fileID: 0}\n")["s.unity"]) == 0,
          "a null-to-null change counted as a nulled reference")

    # a status file with no patch must not read "no diff" as a line-ending change
    sonly = {r.path: r for r in classify(" M Assets/_Prefabs/UI/ConfigureView.prefab\n M Assets/Art/Mystery.asset\n",
                                         "", idx)}
    check(sonly["Assets/_Prefabs/UI/ConfigureView.prefab"].category == "generator-yaml",
          f"status-only prefab called {sonly['Assets/_Prefabs/UI/ConfigureView.prefab'].category}")
    check(sonly["Assets/Art/Mystery.asset"].category == "unknown" and "no diff supplied" in sonly["Assets/Art/Mystery.asset"].note,
          "status-only file not marked as read from the path alone")

    # porcelain parsing: renames and quoted paths
    st = parse_status('R  old/a.prefab -> new/a.prefab\n?? "Assets/with space.mat"\n')
    check(st == [("R ", "new/a.prefab"), ("??", "Assets/with space.mat")], f"porcelain parse: {st}")

    # snapshot_tree: a private index, the real index untouched, the second-open delta seen
    if shutil.which("git"):
        with tempfile.TemporaryDirectory() as d:
            env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@t", GIT_COMMITTER_NAME="t",
                       GIT_COMMITTER_EMAIL="t@t")
            subprocess.run(["git", "init", "-q", d], check=True)
            with open(os.path.join(d, "a.txt"), "w") as f:
                f.write("1\n")
            subprocess.run(["git", "-C", d, "add", "a.txt"], check=True)
            subprocess.run(["git", "-C", d, "commit", "-qm", "init"], check=True, env=env)
            idxf = os.path.join(d, ".git", "snap.index")
            with open(os.path.join(d, "a.txt"), "w") as f:
                f.write("2\n")
            t1 = snapshot_tree(d, idxf)
            with open(os.path.join(d, "b.txt"), "w") as f:
                f.write("new\n")
            t2 = snapshot_tree(d, idxf)
            delta = git(d, "diff", "--name-status", t1, t2).split()
            check(delta == ["A", "b.txt"], f"second-open delta: {delta}")
            check(git(d, "diff", "--cached", "--name-only").strip() == "", "snapshot touched the real index")
            check(sorted(p for _x, p in parse_status(git(d, "status", "--porcelain", "-uall"))) == ["a.txt", "b.txt"],
                  "snapshot changed the working tree's status")

    if fails:
        print("editor_churn self-test: FAILED")
        for f_ in fails:
            print("  - " + f_)
        return 1
    print(f"editor_churn self-test: OK ({len(expect)} classified shapes, 4 negative controls, snapshot delta)")
    return 0


# ------------------------------------------------------------------------------------------ main

def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("--self-test", action="store_true")
    sub = p.add_subparsers(dest="cmd")
    c = sub.add_parser("classify", help="classify the churn in a checkout or in a person's status + patch")
    c.add_argument("--repo", help="a live checkout (default when no --status/--patch: this repo)")
    c.add_argument("--status", help="git status --porcelain output (churn-status.txt)")
    c.add_argument("--patch", help="git diff output (churn.patch)")
    c.add_argument("--writers-from", default=REPO, help="the repo whose Tools/Build and editor scripts name writers")
    fmt = c.add_mutually_exclusive_group()
    fmt.add_argument("--json", action="store_true")
    fmt.add_argument("--md", action="store_true", help="rows in the tracker's file-table shape")
    m = sub.add_parser("measure", help="open the project in Unity N times and capture what each open writes")
    m.add_argument("--project", required=True, help="a FULL, clean checkout (a fresh clone measures best)")
    m.add_argument("--opens", type=int, default=2)
    m.add_argument("--logs", help="where captures go (default: a dated folder in the temp dir)")
    m.add_argument("--machine", help="name for the measurement row (default: hostname)")
    m.add_argument("--timeout", type=int, default=3600, help="per Unity open, seconds")
    m.add_argument("--allow-dirty", action="store_true")
    m.add_argument("--verify", help=argparse.SUPPRESS)  # another verify_unity.sh (tests)
    a = p.parse_args(argv)

    if a.self_test:
        return self_test()
    if a.cmd == "measure":
        if a.opens < 1:
            p.error("--opens must be at least 1")
        return run_measure(a)
    if a.cmd != "classify":
        p.print_help()
        return 3

    tracked = disk_guids = guid_refs = None
    if a.status or a.patch:
        try:
            status = open(a.status, encoding="utf-8", errors="replace").read() if a.status else ""
            patch = open(a.patch, encoding="utf-8", errors="replace", newline="").read() if a.patch else ""
        except OSError as e:
            print(f"editor_churn: {e}", file=sys.stderr)
            return 3
        repo_for_writers = a.writers_from
        if a.repo:
            tracked = set(git(a.repo, "ls-files").splitlines())
    else:
        repo = os.path.abspath(a.repo or REPO)
        try:
            status, patch, tracked, disk_guids, guid_refs = live_inputs(repo)
        except RuntimeError as e:
            print(f"editor_churn: {e}", file=sys.stderr)
            return 3
        repo_for_writers = repo
    rows = classify(status, patch, WriterIndex(repo_for_writers), tracked, disk_guids, guid_refs)
    if a.json:
        print(json.dumps({"counts": counts(rows), "rows": [asdict(r) for r in rows]}, indent=2))
    elif a.md:
        print(render_md(rows), end="")
    else:
        print(render_text(rows), end="")
    return 1 if rows else 0


if __name__ == "__main__":
    sys.exit(main())
