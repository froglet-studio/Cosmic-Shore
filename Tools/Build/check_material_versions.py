#!/usr/bin/env python3
"""check_material_versions.py - every material URP versions on import already carries its version (Editor Churn W6).

Why: URP's MaterialPostprocessor (URP 17.3, Editor/AssetPostProcessors/MaterialPostprocessor.cs) adds an
`AssetVersion` sub-asset (`version: 10`) to every material whose shader it recognises, the first time the material is
imported. A material committed without it, typically one a script or a session with no Unity running wrote, is
rewritten by the first editor open on every fresh clone: GitHub Desktop shows a changed .mat nobody touched
(Docs/EditorChurn/TRACKER.md row 11, `SwitchbackPathRay.mat`, measured on caleb-windows 2026-11-19).

  python3 Tools/Build/check_material_versions.py              # check every tracked .mat under Assets/
  python3 Tools/Build/check_material_versions.py PATH...      # check these .mat files only
  python3 Tools/Build/check_material_versions.py --self-test

Which materials URP versions (read from URP 17.3.0's Editor/ShaderUtils.cs GetShaderID; the only filter in
MaterialPostprocessor.OnPostprocessAllAssets is "shader id is not Unknown"):
  - a hand-written URP shader whose name is one of the 13 ShaderPathID names: the 11 GUIDs below (SpeedTree8's
    file name does not match its entry, and SpeedTree9 has no hand-written shader, so neither is versioned);
  - any Shader Graph with URP metadata, i.e. a graph with a UniversalTarget (shader fileID -6465566751694194690).
    A graph in this repo counts when its file names UniversalTarget; a graph this repo does not carry (a package's)
    is assumed to be URP and reported as such.
A material on any other shader (a project .shader, a built-in, an HDRP-only graph) is never versioned and needs
nothing. Re-read the list when the URP package version changes.

The fix for a finding is the form Unity writes: open the project once, let URP add the sub-asset, commit the .mat.
Never hand-write the sub-asset (its fileID is random per machine; any committed one is kept). A READER: it writes
nothing. Exit 0 clean, 1 findings, 3 bad input.
"""
from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# URP 17.3.0 Shaders/*.shader whose `Shader "..."` name GetEnumFromPath matches (guid -> ShaderPathID).
URP_SHADERS = {
    "933532a4fcc9baf4fa0491de14d08ed7": "Lit",
    "8d2bb70cbf9db8d4da26e15b26e74248": "SimpleLit",
    "650dd9526735d5b46b79224bc6e94025": "Unlit",
    "69c1f799e772cb6438f56c23efccb782": "TerrainLit",
    "b7839dad95683814aa64166edc107ae2": "ParticlesLit",
    "8516d7a69675844a7a0b7095af7c46af": "ParticlesSimpleLit",
    "0406db5a14f94604a8c57ccfbc9f3b46": "ParticlesUnlit",
    "0ca6dca7396eb48e5849247ffd444914": "BakedLit",
    "0f4122b9a743b744abe2fb6a0a88868b": "SpeedTree7",
    "5ec81c81908db34429b4f6ddecadd3bd": "SpeedTree7Billboard",
    "ee7e4c9a5f6364b688a332c67fc32cca": "ComplexLit",
}
ASSET_VERSION_SCRIPT = "d0353a89b1f911e48b9e16bdc9f2e058"  # UnityEditor.Rendering.Universal.AssetVersion
SHADERGRAPH_FILEID = "-6465566751694194690"
SHADER_RE = re.compile(r"^  m_Shader: \{fileID: (-?\d+), guid: ([0-9a-f]{32})", re.M)
META_GUID_RE = re.compile(r"^guid: *([0-9a-f]{32})\s*$", re.M)


def _git_files(pattern: str) -> list[str]:
    out = subprocess.run(["git", "-C", REPO, "ls-files", "-z", "--", pattern], capture_output=True, check=True)
    return [p for p in out.stdout.decode("utf-8", "replace").split("\0") if p]


def graph_targets(repo: str = REPO) -> dict[str, bool]:
    """guid of each tracked .shadergraph -> whether it has a URP (Universal) target."""
    result: dict[str, bool] = {}
    for path in _git_files("*.shadergraph"):
        try:
            with open(os.path.join(repo, path + ".meta"), encoding="utf-8", errors="replace") as fh:
                m = META_GUID_RE.search(fh.read())
            with open(os.path.join(repo, path), encoding="utf-8", errors="replace") as fh:
                urp = "UniversalTarget" in fh.read()
        except OSError:
            continue
        if m:
            result[m.group(1)] = urp
    return result


def why_versioned(text: str, graphs: dict[str, bool]) -> str | None:
    """Why URP versions a material with this text, or None when it never does."""
    m = SHADER_RE.search(text)
    if not m:
        return None
    file_id, guid = m.groups()
    if guid in URP_SHADERS:
        return f"URP shader {URP_SHADERS[guid]}"
    if file_id == SHADERGRAPH_FILEID:
        if guid not in graphs:
            return f"Shader Graph {guid} (not in this repo: assumed URP)"
        return "Shader Graph with a URP target" if graphs[guid] else None
    return None


def check(text: str, graphs: dict[str, bool]) -> str | None:
    """The finding for one material, or None."""
    reason = why_versioned(text, graphs)
    if reason and ASSET_VERSION_SCRIPT not in text:
        return f"no URP AssetVersion sub-asset ({reason}): the first editor import adds one"
    return None


def run(paths: list[str]) -> int:
    graphs = graph_targets()
    findings = []
    for path in paths:
        try:
            with open(os.path.join(REPO, path), encoding="utf-8", errors="replace") as fh:
                text = fh.read()
        except OSError as exc:
            print(f"check_material_versions: cannot read {path}: {exc}", file=sys.stderr)
            return 3
        finding = check(text, graphs)
        if finding:
            findings.append((path, finding))
    for path, finding in findings:
        print(f"  {path}: {finding}")
    if findings:
        print(f"check_material_versions: {len(findings)} material(s) Unity will rewrite on first open. Fix: open "
              "the project once and commit the .mat as Unity saved it (never hand-write the sub-asset).")
        return 1
    print(f"check_material_versions: {len(paths)} material(s), all clean")
    return 0


_STAMP = ("--- !u!114 &-9107202918723861369\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n"
          f"  m_Script: {{fileID: 11500000, guid: {ASSET_VERSION_SCRIPT}, type: 3}}\n  version: 10\n")


def _mat(file_id: str, guid: str, stamped: bool) -> str:
    head = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + (_STAMP if stamped else "")
    return head + f"--- !u!21 &2100000\nMaterial:\n  m_Name: T\n  m_Shader: {{fileID: {file_id}, guid: {guid}, type: 3}}\n"


def self_test() -> int:
    lit = "933532a4fcc9baf4fa0491de14d08ed7"
    graphs = {"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": True, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb": False}
    cases = [
        ("URP Lit, no stamp (row 11's shape)", _mat("4800000", lit, False), True),
        ("URP Lit, stamped", _mat("4800000", lit, True), False),
        ("project .shader, no stamp", _mat("4800000", "db3a7d241b308d845a0133c22554acad", False), False),
        ("built-in shader, no stamp", _mat("46", "0000000000000000f000000000000000", False), False),
        ("URP Shader Graph, no stamp", _mat(SHADERGRAPH_FILEID, "a" * 32, False), True),
        ("URP Shader Graph, stamped", _mat(SHADERGRAPH_FILEID, "a" * 32, True), False),
        ("non-URP Shader Graph, no stamp", _mat(SHADERGRAPH_FILEID, "b" * 32, False), False),
        ("graph not in repo, no stamp", _mat(SHADERGRAPH_FILEID, "c" * 32, False), True),
        ("SpeedTree8 (name mismatch), no stamp", _mat("4800000", "99134b1f0c27d54469a840832a28fadf", False), False),
        ("no shader line", "%YAML 1.1\n--- !u!21 &2100000\nMaterial:\n  m_Name: T\n", False),
    ]
    bad = 0
    for name, text, want in cases:
        got = check(text, graphs) is not None
        if got != want:
            bad += 1
            print(f"FAIL {name}: finding={got}, wanted {want}")
    # Negative control on real history: the shipped SwitchbackPathRay.mat before #1770 must fail, after must pass.
    real = graph_targets()
    before = subprocess.run(["git", "-C", REPO, "show", "eb707314cf:Assets/Resources/SwitchbackPathRay.mat"],
                            capture_output=True, text=True)
    if before.returncode == 0:
        if check(before.stdout, real) is None:
            bad += 1
            print("FAIL negative control: SwitchbackPathRay.mat before #1770 passed")
    else:
        print("note: eb707314cf not in this clone (shallow); the history control was skipped")
    now = os.path.join(REPO, "Assets/Resources/SwitchbackPathRay.mat")
    if os.path.exists(now) and check(open(now, encoding="utf-8").read(), real) is not None:
        bad += 1
        print("FAIL: the committed SwitchbackPathRay.mat is reported")
    if bad:
        print(f"self-test: {bad} failure(s)")
        return 1
    print(f"self-test: {len(cases)} cases and the history control pass")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("paths", nargs="*", help=".mat files to check (default: every tracked .mat under Assets/)")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    paths = args.paths or [p for p in _git_files("Assets/*.mat")]
    return run(paths)


if __name__ == "__main__":
    sys.exit(main())
