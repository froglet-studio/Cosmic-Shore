#!/usr/bin/env python3
"""
Static pre-build validation for Cosmic Shore.

This is the cheap line of defence in front of the Thursday UGS build. It needs
no Unity install and no runner, so it can gate every promotion into the build
branches in a few seconds. It cannot prove a build succeeds; it catches the
specific, recurring classes of mistake that have broken player builds here
before, all of which are visible without compiling:

  editor-in-runtime   Editor-only API reaching player code. UnityEditor does
                      not exist in a player build, so this is a hard link
                      error at build time even though the editor compiles it
                      happily. This is the "namespace error" that produced the
                      master commits "Move editor scripts to Editor folder to
                      fix player build errors" and "Fully qualify Editor base
                      class to avoid namespace conflict".

  monobehaviour-name  A MonoBehaviour whose class name does not match its file
                      name. Unity silently refuses to attach the component and
                      the failure only shows up as a null in a scene.

  orphan-meta         A .meta with no asset beside it. Harmless to the build,
                      but it is how GUID churn and "missing script" references
                      start in a repo with several people in the same scenes.

  missing-meta        An asset with no .meta. Unity regenerates it with a fresh
                      GUID on whichever machine opens the project first, which
                      silently breaks every reference to that asset.

Exit code is 0 when clean, 1 when any check fails. Run with --list to see the
checks, or --skip CHECK to disable one.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from pathlib import Path

# Directories that are vendored or generated. We do not own the code in them
# and a violation there is not actionable, so scanning them is pure noise.
EXCLUDED_DIRS = {
    "Library", "Temp", "Obj", "Build", "Builds", "Logs", "UserSettings",
    ".git", ".utmp", "node_modules",
}
EXCLUDED_PREFIXES = (
    "Assets/Plugins/",
    "Assets/PlayFabSDK/",
    "Assets/PlayFabEditorExtensions/",
    "Assets/NiceVibrations/",
    "Assets/ProfileAnalyzer/",
    "Assets/TextMesh Pro/",
    "Assets/Unity Assests/",   # sic: vendored Unity sample assets
    "Assets/Wwise/",
    "Assets/StreamingAssets/",
)

# Severity decides whether a check can block a promotion. Only breaks-the-build
# problems are errors; everything else reports and gets out of the way, because
# a gate that cries wolf gets switched off.
SEVERITY = {
    "editor-in-runtime": "error",
    "monobehaviour-name": "warning",
    "meta": "warning",
}

# Assets that legitimately have no .meta, plus files Unity itself ignores.
META_EXEMPT_SUFFIXES = (".meta", ".DS_Store")
UNITY_IGNORED_PREFIXES = (".", "~")

EDITOR_TOKEN = re.compile(r"\bUnityEditor\b")
# A MonoBehaviour/ScriptableObject declaration. Unity's file-name rule applies
# to MonoBehaviour only, but catching both is cheap and the message differs.
TYPE_DECL = re.compile(
    r"^\s*(?:public\s+|internal\s+|sealed\s+|abstract\s+|partial\s+)*"
    r"class\s+(\w+)\s*:\s*([^{]+)",
    re.MULTILINE,
)


class Finding:
    def __init__(self, check: str, path: str, detail: str, line: int | None = None):
        self.check, self.path, self.detail, self.line = check, path, detail, line

    def __str__(self) -> str:
        loc = f"{self.path}:{self.line}" if self.line else self.path
        return f"  {loc}\n      {self.detail}"


def is_excluded(rel: str) -> bool:
    parts = Path(rel).parts
    if any(p in EXCLUDED_DIRS for p in parts):
        return True
    return rel.startswith(EXCLUDED_PREFIXES)


def in_editor_folder(rel: str) -> bool:
    """Unity compiles anything under a folder literally named Editor into the
    editor-only assembly, at any depth."""
    return "Editor" in Path(rel).parts[:-1]


def player_visible_lines(text: str) -> set[int]:
    """Line numbers (1-based) that survive into a player build.

    Tracks #if / #elif / #else / #endif nesting and treats a region as stripped
    when its condition is editor-only (UNITY_EDITOR) or unconditionally false.
    This is deliberately conservative: anything it cannot prove is stripped is
    reported as player-visible, so an odd guard produces a finding to look at
    rather than a silent pass.
    """
    stripped_depth = 0   # >0 means we are inside a region excluded from players
    depth = 0
    visible: set[int] = set()

    for n, raw in enumerate(text.splitlines(), start=1):
        s = raw.strip()

        if s.startswith("#if"):
            depth += 1
            cond = s[3:].split("//")[0].strip()
            if stripped_depth == 0 and _is_editor_only(cond):
                stripped_depth = depth
            continue

        if s.startswith("#elif"):
            cond = s[5:].split("//")[0].strip()
            if stripped_depth == depth:
                stripped_depth = 0          # leaving the stripped arm
            if stripped_depth == 0 and _is_editor_only(cond):
                stripped_depth = depth
            continue

        if s.startswith("#else"):
            # The complement of an editor-only guard is player code, and the
            # complement of player code is not necessarily editor-only, so only
            # the first direction can be cleared here.
            if stripped_depth == depth:
                stripped_depth = 0
            continue

        if s.startswith("#endif"):
            if stripped_depth == depth:
                stripped_depth = 0
            depth = max(0, depth - 1)
            continue

        if stripped_depth == 0:
            visible.add(n)

    return visible


def _is_editor_only(cond: str) -> bool:
    c = cond.replace("(", " ").replace(")", " ").strip()
    if c == "false":
        return True
    if not c:
        return False
    # UNITY_EDITOR, or any AND-chain containing it. An OR-chain is not
    # editor-only because the other arm can still reach a player.
    if "||" in c:
        return False
    return any(tok.strip() == "UNITY_EDITOR" for tok in c.split("&&"))


def check_editor_in_runtime(root: Path, files: list[str]) -> list[Finding]:
    out = []
    for rel in files:
        if not rel.endswith(".cs") or in_editor_folder(rel):
            continue
        try:
            # utf-8-sig, not utf-8: a BOM left on line 1 would otherwise hide a
            # leading "#if UNITY_EDITOR" guard and make every such file a false
            # positive. Several files in this repo have one.
            text = (root / rel).read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        if "UnityEditor" not in text:
            continue

        visible = player_visible_lines(text)
        for n, line in enumerate(text.splitlines(), start=1):
            if n not in visible or not EDITOR_TOKEN.search(line):
                continue
            if line.strip().startswith("//"):
                continue
            out.append(Finding(
                "editor-in-runtime", rel,
                "References UnityEditor from player-visible code. Move the file "
                "under an Editor/ folder, or wrap it in #if UNITY_EDITOR.",
                n,
            ))
            break   # one finding per file is enough to act on
    return out


def check_monobehaviour_names(root: Path, files: list[str]) -> list[Finding]:
    out = []
    for rel in files:
        if not rel.endswith(".cs"):
            continue
        try:
            # utf-8-sig, not utf-8: a BOM left on line 1 would otherwise hide a
            # leading "#if UNITY_EDITOR" guard and make every such file a false
            # positive. Several files in this repo have one.
            text = (root / rel).read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        if "MonoBehaviour" not in text:
            continue

        stem = Path(rel).stem
        decls = [(m.group(1), m.group(2)) for m in TYPE_DECL.finditer(text)]
        mono = [name for name, bases in decls if "MonoBehaviour" in bases]
        # Only the unambiguous case. A file holding several MonoBehaviours is
        # usually a primary type plus helpers that are added through
        # AddComponent at runtime, which is legal and common here, so flagging
        # those would bury the real renames in noise.
        if len(mono) == 1 and stem not in mono:
            out.append(Finding(
                "monobehaviour-name", rel,
                f"Defines MonoBehaviour {mono[0]} but the file is named {stem}.cs. "
                "Unity will not let this component be attached in the editor.",
            ))
    return out


def check_meta_files(root: Path, files: list[str]) -> list[Finding]:
    present = set(files)
    out = []
    for rel in files:
        if not rel.startswith("Assets/"):
            continue
        name = Path(rel).name
        if name.startswith(UNITY_IGNORED_PREFIXES):
            continue

        if rel.endswith(".meta"):
            asset = rel[:-5]
            if asset in present or (root / asset).is_dir():
                continue
            # A folder .meta with no folder is the normal result of git not
            # tracking empty directories, not a problem. Unity records which
            # kind it is in the file itself, so ask rather than guess.
            try:
                head = (root / rel).read_text(encoding="utf-8-sig",
                                              errors="replace")[:200]
            except OSError:
                head = ""
            if "folderAsset: yes" in head:
                continue
            out.append(Finding("orphan-meta", rel, "No asset beside this .meta."))
        elif not name.endswith(META_EXEMPT_SUFFIXES):
            if rel + ".meta" not in present:
                out.append(Finding("missing-meta", rel,
                                   "No .meta. Unity will mint a fresh GUID and "
                                   "break existing references to this asset."))
    return out


CHECKS = {
    "editor-in-runtime": check_editor_in_runtime,
    "monobehaviour-name": check_monobehaviour_names,
    "meta": check_meta_files,
}


def collect(root: Path) -> list[str]:
    files = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDED_DIRS]
        for fn in filenames:
            rel = os.path.relpath(os.path.join(dirpath, fn), root)
            rel = rel.replace(os.sep, "/")
            if not is_excluded(rel):
                files.append(rel)
    return files


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=".", help="project root (default: cwd)")
    ap.add_argument("--skip", action="append", default=[], metavar="CHECK",
                    help="disable a check; repeatable")
    ap.add_argument("--list", action="store_true", help="list checks and exit")
    ap.add_argument("--github", action="store_true",
                    help="also emit ::error:: annotations for GitHub Actions")
    args = ap.parse_args()

    if args.list:
        for name in CHECKS:
            print(name)
        return 0

    root = Path(args.root).resolve()
    if not (root / "Assets").is_dir():
        print(f"error: {root} does not look like a Unity project (no Assets/)",
              file=sys.stderr)
        return 2

    files = collect(root)
    print(f"Scanned {len(files)} files under {root}\n")

    errors = warnings = 0
    for name, fn in CHECKS.items():
        if name in args.skip:
            print(f"{name}: skipped")
            continue

        severity = SEVERITY.get(name, "error")
        findings = fn(root, files)
        if severity == "error":
            errors += len(findings)
        else:
            warnings += len(findings)

        if not findings:
            print(f"{name}: clean")
            continue

        print(f"{name}: {len(findings)} {severity}(s)")
        for f in findings:
            print(f)
            if args.github:
                loc = f"file={f.path}" + (f",line={f.line}" if f.line else "")
                print(f"::{severity} {loc}::[{f.check}] {f.detail}")
        print()

    print()
    print(f"{errors} error(s), {warnings} warning(s).")
    if errors:
        print("FAILED: problems here break a player build even though the "
              "editor compiles them.")
        return 1
    print("PASSED: nothing that blocks a build.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
