#!/usr/bin/env python3
"""check_meta_files.py - every Unity asset has exactly one .meta, and every GUID is one asset's (Editor Churn W2).

Why: a session that writes an Assets/ file with no Unity running creates no .meta, so every machine's Unity
invents its own random GUID for the same file. A prefab or scene that references it then works on one machine
and breaks on the next, and GitHub Desktop shows a new .meta nobody meant to make
(Docs/EditorChurn/EDITOR_CHURN_PLAN.md W2, Docs/EditorChurn/TRACKER.md rows 1-5).

  python3 Tools/Build/check_meta_files.py              # check the tree (tracked + untracked, not ignored)
  python3 Tools/Build/check_meta_files.py --tracked    # tracked files only (what a fresh clone sees)
  python3 Tools/Build/check_meta_files.py --fix        # write the missing .meta files it can write exactly
  python3 Tools/Build/check_meta_files.py --self-test

It fails on:
  missing    a file or folder under Assets/ that Unity imports and that has no .meta
  orphan     a .meta whose file or folder does not exist (git keeps no empty folders: on a fresh clone Unity
             deletes the orphan, or recreates it with a new GUID)
  duplicate  two .meta files carrying one GUID
  malformed  a .meta with no guid line

What Unity never imports needs no .meta, by RULE, never by name (Unity's hidden-asset rules): any path part that
starts with "." or ends with "~", a part named "cvs", a file ending ".tmp", and everything INSIDE a macOS bundle
folder (*.bundle, *.framework, *.plugin, *.xcframework, *.app), whose contents are one asset.

--fix writes a .meta only where the exact form is known: folders, C# scripts (.cs) and assembly definitions
(.asmdef/.asmref). Each gets a deterministic GUID, md5("cosmic-shore:" + path), so every session and machine
writes the same one, and a GUID another .meta already holds is refused, never shared. Before committing a fixed
.cs.meta, check nothing already references the script under another GUID (an asset whose m_Script guid no .meta
carries): only a MonoBehaviour or ScriptableObject can be referenced that way. Anything else (a texture, a model,
an audio clip) needs a real Unity import: it is reported, never guessed. --fix never deletes an orphan; it says
what to do.

A READER unless --fix (which writes only new .meta files). Exit 0 clean, 1 findings, 3 bad input.
"""
from __future__ import annotations

import argparse
import hashlib
import os
import re
import subprocess
import sys
import tempfile

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ROOTS = ("Assets",)
BUNDLE_EXT = (".bundle", ".framework", ".plugin", ".xcframework", ".app")
GUID_RE = re.compile(r"^guid: *([0-9a-f]{32})\s*$", re.M)

# Unity writes "userData: " etc. with a trailing space; spelled out so an editor that strips trailing
# whitespace cannot change the template (the self-test compares it with a Unity-written .cs.meta byte for byte).
_TAIL = ["  userData: ", "  assetBundleName: ", "  assetBundleVariant: ", ""]
CS_META = "\n".join(["fileFormatVersion: 2", "guid: {guid}", "MonoImporter:", "  externalObjects: {{}}",
                      "  serializedVersion: 2", "  defaultReferences: []", "  executionOrder: 0",
                      "  icon: {{instanceID: 0}}"] + _TAIL)
FOLDER_META = "\n".join(["fileFormatVersion: 2", "guid: {guid}", "folderAsset: yes", "DefaultImporter:",
                          "  externalObjects: {{}}"] + _TAIL)
ASMDEF_META = "\n".join(["fileFormatVersion: 2", "guid: {guid}", "AssemblyDefinitionImporter:",
                          "  externalObjects: {{}}"] + _TAIL)
ASMREF_META = ASMDEF_META.replace("AssemblyDefinitionImporter", "AssemblyDefinitionReferenceImporter")
TEMPLATES = {".cs": CS_META, ".asmdef": ASMDEF_META, ".asmref": ASMREF_META}


def hidden_part(part: str) -> bool:
    """Unity's rule for a path part it never imports."""
    return part.startswith(".") or part.endswith("~") or part.lower() == "cvs" or part.endswith(".tmp")


def needs_meta(path: str) -> bool:
    """Does Unity import this repo-relative path (file or folder) and so need a .meta beside it?"""
    parts = path.split("/")
    if parts[0] not in ROOTS or len(parts) < 2:
        return False
    for i, p in enumerate(parts[1:], start=1):
        if hidden_part(p):
            return False
        if i < len(parts) - 1 and p.lower().endswith(BUNDLE_EXT):
            return False  # inside a bundle: the bundle folder is the asset
    return True


def inside_bundle(path: str) -> bool:
    parts = path.split("/")
    return any(p.lower().endswith(BUNDLE_EXT) for p in parts[1:-1])


def deterministic_guid(path: str) -> str:
    return hashlib.md5(("cosmic-shore:" + path).encode("utf-8")).hexdigest()


def list_files(repo: str, tracked_only: bool) -> list[str]:
    args = ["git", "-C", repo, "ls-files", "-z", "--cached"]
    if not tracked_only:
        args += ["--others", "--exclude-standard"]
    args += ["--", *ROOTS]
    out = subprocess.run(args, capture_output=True, check=True).stdout.decode("utf-8", "replace")
    files = sorted({f for f in out.split("\0") if f})
    # a tracked file deleted in the working tree is not there: check what exists
    return [f for f in files if os.path.lexists(os.path.join(repo, f))]


def check(files: list[str], read) -> dict[str, list]:
    """files: repo-relative paths that exist. read(path) -> text of a .meta.
    Returns {"missing": [path], "orphan": [meta], "duplicate": [(guid, [metas])], "malformed": [meta]}."""
    fileset = set(files)
    metas = {f for f in fileset if f.endswith(".meta")}
    assets = fileset - metas
    folders = set()
    for f in fileset:
        p = f.rsplit("/", 1)[0] if "/" in f else ""
        while p and p not in ROOTS:
            folders.add(p)
            p = p.rsplit("/", 1)[0] if "/" in p else ""
    existing = assets | folders

    missing = sorted(p for p in existing if needs_meta(p) and p + ".meta" not in metas)
    orphan = sorted(m for m in metas if needs_meta(m[:-5]) and m[:-5] not in existing)

    by_guid: dict[str, list[str]] = {}
    malformed = []
    for m in sorted(metas):
        if inside_bundle(m) or not needs_meta(m[:-5]):
            continue
        mm = GUID_RE.search(read(m))
        if not mm:
            malformed.append(m)
            continue
        by_guid.setdefault(mm.group(1), []).append(m)
    duplicate = sorted((g, ms) for g, ms in by_guid.items() if len(ms) > 1)
    return {"missing": missing, "orphan": orphan, "duplicate": duplicate, "malformed": malformed,
            "_guids": by_guid, "_folders": folders}


def template_for(path: str, is_folder: bool) -> str | None:
    if is_folder:
        return FOLDER_META
    return TEMPLATES.get(os.path.splitext(path)[1].lower())


def fix(repo: str, result: dict) -> tuple[list[str], list[str]]:
    """Write the .meta files whose form is exact. Returns (written, refused)."""
    written, refused = [], []
    taken = set(result["_guids"])
    for p in result["missing"]:
        tmpl = template_for(p, p in result["_folders"])
        if tmpl is None:
            refused.append(p)
            continue
        g = deterministic_guid(p)
        if g in taken:
            refused.append(p)
            continue
        with open(os.path.join(repo, p + ".meta"), "w", encoding="utf-8", newline="\n") as f:
            f.write(tmpl.format(guid=g))
        taken.add(g)
        written.append(p + ".meta")
    return written, refused


def report(result: dict, fixed=None, refused=None) -> int:
    n = 0
    hint = {
        "missing": "no .meta: Unity will invent a random GUID on each machine (--fix writes folders, .cs and .asmdef; "
                   "import anything else in Unity once and commit the .meta it makes)",
        "orphan": "no file or folder: Unity deletes this on open, or recreates it with a new GUID. For an empty "
                  "folder that must exist, commit a .gitkeep in it (Unity ignores dotfiles); otherwise delete the "
                  ".meta",
        "duplicate": "one GUID, several assets: references resolve to whichever Unity imports first",
        "malformed": "no guid line",
    }
    for kind in ("missing", "orphan", "duplicate", "malformed"):
        items = result[kind]
        if kind == "missing" and fixed is not None:
            items = [p for p in items if p + ".meta" not in set(fixed)]
        if not items:
            continue
        n += len(items)
        print(f"{kind} ({len(items)}): {hint[kind]}")
        for it in items:
            if kind == "duplicate":
                print(f"  {it[0]}: " + ", ".join(it[1]))
            else:
                print(f"  {it}")
    if fixed:
        print(f"wrote {len(fixed)} .meta file(s) with deterministic GUIDs:")
        for m in fixed:
            print(f"  {m}")
    if refused:
        print(f"--fix left {len(refused)} for a Unity import (form not known exactly, or GUID taken).")
    if n == 0:
        print("check_meta_files: OK - every imported asset under Assets/ has one .meta, no orphans, no shared GUIDs")
        return 0
    print(f"check_meta_files: {n} finding(s)")
    return 1


# ------------------------------------------------------------------------------------------ self-test

def self_test() -> int:
    fails = []

    def ok(cond, msg):
        if not cond:
            fails.append(msg)

    g = lambda c: c * 32  # noqa: E731
    metas = {
        "Assets/A.cs.meta": f"guid: {g('a')}\n",
        "Assets/Dir.meta": f"guid: {g('b')}\n",
        "Assets/Dir/B.prefab.meta": f"guid: {g('c')}\n",
        "Assets/Gone.cs.meta": f"guid: {g('d')}\n",                # orphan
        "Assets/Dup1.mat.meta": f"guid: {g('e')}\n",
        "Assets/Dup2.mat.meta": f"guid: {g('e')}\n",               # duplicate
        "Assets/Bad.txt.meta": "fileFormatVersion: 2\n",           # malformed
        "Assets/Lib.bundle.meta": f"guid: {g('f')}\n",
        "Assets/Lib.bundle/Contents.meta": f"guid: {g('1')}\n",    # inside a bundle: not judged
        "Assets/Empty.meta": f"guid: {g('2')}\n",                  # empty folder git cannot keep: orphan
        "Assets/Bad.txt": "", "Assets/Dup1.mat": "", "Assets/Dup2.mat": "",
    }
    files = sorted(set(metas) | {
        "Assets/A.cs", "Assets/Dir/B.prefab",
        "Assets/Dir/New.cs",                                       # missing
        "Assets/NewFolder/Thing.asmdef",                           # missing x2: file and folder
        "Assets/Tex.png",                                          # missing, --fix must refuse
        "Assets/.DS_Store", "Assets/Hidden~/X.java", "Assets/cvs/Y.txt", "Assets/Z.tmp",   # hidden by rule
        "Assets/Lib.bundle/Contents/Info.plist",                   # bundle contents
        "Assets/Plugins/Android/X.androidlib/build.gradle",        # NOT a bundle: needs metas
    })
    r = check(files, lambda m: metas.get(m, ""))
    ok(r["missing"] == sorted(["Assets/Dir/New.cs", "Assets/NewFolder", "Assets/NewFolder/Thing.asmdef",
                               "Assets/Tex.png", "Assets/Plugins", "Assets/Plugins/Android",
                               "Assets/Plugins/Android/X.androidlib",
                               "Assets/Plugins/Android/X.androidlib/build.gradle"]),
       f"missing: {r['missing']}")
    ok(r["orphan"] == ["Assets/Empty.meta", "Assets/Gone.cs.meta"], f"orphan: {r['orphan']}")
    ok(r["duplicate"] == [(g("e"), ["Assets/Dup1.mat.meta", "Assets/Dup2.mat.meta"])], f"duplicate: {r['duplicate']}")
    ok(r["malformed"] == ["Assets/Bad.txt.meta"], f"malformed: {r['malformed']}")

    # negative controls on the hidden rule: a name that only LOOKS hidden is still checked
    r2 = check(["Assets/Tilde~Mid/X.cs", "Assets/notcvs/Y.cs", "Assets/My.bundleish/Z.cs"], lambda m: "")
    ok(len(r2["missing"]) == 6, f"look-alike hidden names were skipped: {r2['missing']}")

    # --fix writes exact, deterministic metas and refuses what it cannot write exactly
    with tempfile.TemporaryDirectory() as d:
        for f in ["Assets/Dir/New.cs", "Assets/NewFolder/Thing.asmdef", "Assets/Tex.png"]:
            os.makedirs(os.path.join(d, os.path.dirname(f)), exist_ok=True)
            open(os.path.join(d, f), "w").close()
        res = check(["Assets/Dir.meta", "Assets/Dir/New.cs", "Assets/NewFolder/Thing.asmdef", "Assets/Tex.png"],
                    lambda m: f"guid: {g('b')}\n")
        written, refused = fix(d, res)
        ok(sorted(written) == ["Assets/Dir/New.cs.meta", "Assets/NewFolder.meta", "Assets/NewFolder/Thing.asmdef.meta"],
           f"--fix wrote {written}")
        ok(refused == ["Assets/Tex.png"], f"--fix refused {refused}")
        cs = open(os.path.join(d, "Assets/Dir/New.cs.meta"), encoding="utf-8").read()
        ok(cs == CS_META.format(guid=deterministic_guid("Assets/Dir/New.cs")), "the .cs meta is not the template")
        ok("folderAsset: yes" in open(os.path.join(d, "Assets/NewFolder.meta")).read(), "folder meta is not a folder")
        ok(deterministic_guid("Assets/Dir/New.cs") == deterministic_guid("Assets/Dir/New.cs")
           and deterministic_guid("Assets/Dir/New.cs") != deterministic_guid("Assets/Dir/Other.cs"),
           "GUID is not deterministic per path")
        # a deterministic GUID already taken is refused, never shared
        res2 = check(["Assets/Q.cs"], lambda m: "")
        res2["_guids"][deterministic_guid("Assets/Q.cs")] = ["Assets/Other.cs.meta"]
        os.makedirs(os.path.join(d, "Assets"), exist_ok=True)
        open(os.path.join(d, "Assets/Q.cs"), "w").close()
        w2, rf2 = fix(d, res2)
        ok(w2 == [] and rf2 == ["Assets/Q.cs"], "a taken GUID was written")

    # the template matches what Unity 6 writes for a script (the newest committed .cs.meta, byte for byte)
    sample = os.path.join(REPO, "Assets/_Scripts/UI/Views/VoyageEdgeGlow.cs.meta")
    if os.path.exists(sample):
        text = open(sample, encoding="utf-8", newline="").read()
        gm = GUID_RE.search(text)
        ok(gm is not None and text == CS_META.format(guid=gm.group(1)), "CS_META differs from a Unity-written .cs.meta")

    if fails:
        print("check_meta_files self-test: FAILED")
        for f in fails:
            print("  - " + f)
        return 1
    print("check_meta_files self-test: OK (4 finding kinds, hidden-name rules and their look-alikes, --fix)")
    return 0


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("--repo", default=REPO)
    p.add_argument("--tracked", action="store_true", help="tracked files only (a fresh clone's view)")
    p.add_argument("--fix", action="store_true", help="write the missing .meta files whose form is exact")
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args(argv)
    if a.self_test:
        return self_test()
    try:
        files = list_files(a.repo, a.tracked)
    except (OSError, subprocess.CalledProcessError) as e:
        print(f"check_meta_files: {e}", file=sys.stderr)
        return 3

    def read(m):
        try:
            with open(os.path.join(a.repo, m), encoding="utf-8", errors="replace") as f:
                return f.read()
        except OSError:
            return ""

    result = check(files, read)
    if a.fix:
        written, refused = fix(a.repo, result)
        return report(result, written, refused)
    return report(result)


if __name__ == "__main__":
    sys.exit(main())
