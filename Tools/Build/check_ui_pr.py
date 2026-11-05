#!/usr/bin/env python3
"""check_ui_pr.py - the UI checklist gate for pull requests (Docs/UI_STYLE_GUIDE.md section 11).

A pull request that touches a UI path must carry the "## UI checklist" section of .github/pull_request_template.md with
every box answered: ticked ([x]) or marked N/A with a reason. A pull request that touches no UI path passes.

    python3 Tools/Build/check_ui_pr.py --body-file body.md --files changed.txt   (one path per line)
    python3 Tools/Build/check_ui_pr.py --self-test

Exit 0 = passes or not a UI change, 1 = the checklist is missing or a box is unanswered (each named), 2 = bad input.
A READER: it writes nothing.
"""
from __future__ import annotations

import argparse
import re
import sys

UI_PATHS = (
    re.compile(r"^Assets/_Prefabs/UI[^/]*/"),
    re.compile(r"^Assets/_Scenes/Menu_Main\.unity$"),
    re.compile(r"(^|/)GameCanvas[^/]*\.prefab$"),
    re.compile(r"^Assets/_SO_Assets/UI/"),
    re.compile(r"^Assets/_Scripts/UI/"),
)
BOXES = ("Theme tokens", "Families", "Safe area", "Focus", "Shared prefabs", "Words", "Board")


def ui_files(files: list[str]) -> list[str]:
    return [f for f in files if not f.endswith(".meta") and any(p.search(f) for p in UI_PATHS)]


def problems(body: str, files: list[str]) -> list[str]:
    hits = ui_files(files)
    if not hits:
        return []
    m = re.search(r"^##\s*UI checklist\s*$(.*?)(?=^##\s|\Z)", body or "", re.M | re.S | re.I)
    if not m:
        return [f"this PR changes UI ({hits[0]}{' and more' if len(hits) > 1 else ''}) but has no '## UI checklist' section; "
                "copy it from .github/pull_request_template.md (Docs/UI_STYLE_GUIDE.md section 11)"]
    section = re.sub(r"<!--.*?-->", "", m.group(1), flags=re.S)
    out = []
    for name in BOXES:
        line = next((l for l in section.splitlines() if re.match(rf"\s*[-*]\s*\[.\]\s*{re.escape(name)}\b", l, re.I)), None)
        if line is None:
            out.append(f"UI checklist: the '{name}' line is missing")
            continue
        ticked = re.match(r"\s*[-*]\s*\[[xX]\]", line) is not None
        na = re.search(r"\bN/?A\b\s*[:,-]?\s*\S", line.split(name, 1)[-1]) is not None
        if not ticked and not na:
            out.append(f"UI checklist: '{name}' is neither ticked nor marked N/A with a reason")
    return out


def self_test() -> int:
    bad = []
    tmpl = open(__file__.replace("Tools/Build/check_ui_pr.py", ".github/pull_request_template.md"), encoding="utf-8").read() \
        if __file__.endswith("Tools/Build/check_ui_pr.py") else ""
    ui = ["Assets/_Prefabs/UI Elements/Main Menu Screens/Arcade Screen.prefab"]
    if problems("no checklist", ["Assets/_Scripts/Controller/X.cs"]):
        bad.append("a non-UI PR should pass")
    if not problems("## What\nstuff", ui):
        bad.append("a UI PR without the section should fail")
    if tmpl and len(problems(tmpl, ui)) != len(BOXES):
        bad.append("the unfilled template should name every box")
    filled = "## UI checklist\n" + "\n".join(f"- [x] {b}: done" for b in BOXES) + "\n## Affects\n"
    if problems(filled, ui):
        bad.append(f"a ticked checklist should pass: {problems(filled, ui)}")
    na = filled.replace("- [x] Safe area: done", "- [ ] Safe area: N/A - no layout change, colour only")
    if problems(na, ui):
        bad.append("N/A with a reason should pass")
    bare = filled.replace("- [x] Focus: done", "- [ ] Focus: done")
    if not any("'Focus'" in p for p in problems(bare, ui)):
        bad.append("an unticked box without N/A should be named")
    if problems("x", ["Assets/_Prefabs/UI Elements/Foo.prefab.meta"]):
        bad.append(".meta alone is not a UI change")
    if not ui_files(["Assets/_Prefabs/CORE/GameCanvas.prefab", "Assets/_Scenes/Menu_Main.unity"]) == ["Assets/_Prefabs/CORE/GameCanvas.prefab", "Assets/_Scenes/Menu_Main.unity"]:
        bad.append("GameCanvas and Menu_Main are UI paths")
    print("check_ui_pr self-test:", "OK" if not bad else "FAILED")
    for b in bad:
        print("  FAIL", b)
    return 1 if bad else 0


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--body-file")
    ap.add_argument("--files")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not a.body_file or not a.files:
        print("usage: --body-file <pr body> --files <changed paths, one per line>", file=sys.stderr)
        return 2
    body = open(a.body_file, encoding="utf-8").read()
    files = [l.strip() for l in open(a.files, encoding="utf-8") if l.strip()]
    p = problems(body, files)
    hits = ui_files(files)
    if not hits:
        print("UI checklist: no UI path changed - nothing to answer")
        return 0
    for x in p:
        print("FAIL", x)
    print(f"UI checklist: {len(hits)} UI file(s) changed - " + ("answered" if not p else f"{len(p)} problem(s)"))
    return 1 if p else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
