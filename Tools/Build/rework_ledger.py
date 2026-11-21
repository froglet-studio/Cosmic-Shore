#!/usr/bin/env python3
"""rework_ledger.py - the rework and verification counters behind Docs/SKILL_TRAINING_PLAN.md, measured fresh
from git and the docs, so the plan's numbers can be regenerated and the training rounds judged month by month.

A READER tool: it reads git history, Docs/UNITY_VERIFICATION_CHECKLIST.md, Docs/MENU_REDESIGN_B.md and
.claude/skills/*/SKILL.md, and writes nothing.

    python3 Tools/Build/rework_ledger.py                      # the counters since 2026-09-01, as a table
    python3 Tools/Build/rework_ledger.py --since 2026-10-01   # from another date
    python3 Tools/Build/rework_ledger.py --skills             # the skill maturity table (kit / gate / lessons / refs)
    python3 Tools/Build/rework_ledger.py --json               # both, as JSON
    python3 Tools/Build/rework_ledger.py --self-test

Each counter is a SIGNAL that work was done twice or shipped unseen, never a verdict on its own:
  reverts         commits whose subject starts with Revert / revert, every fetched branch, one per subject;
                  the deliberate negative-control reverts (the CI guard's broken line, the Release rehearsal) are
                  left out by their own words
  retired         commits whose subject says retire / retired / retires
  again           commits whose subject says "again" - a thing that had worked and was made to work again
  compile fixes   commits whose subject names a CSxxxx error or says a compile was fixed
  checklist       Docs/UNITY_VERIFICATION_CHECKLIST.md: "## " sections, how many are red (never editor-verified),
                  how many verified, red per month
  ui rounds       Docs/MENU_REDESIGN_B.md: "## N. Rev" mockup rounds, "## N. Built" builds, how many sections say
                  NOT RUN IN EDITOR / awaiting approval, and the owner's "do not like" lines
  skills          .claude/skills/*/SKILL.md: a kit (names Tools/Build or Tools/Compute), a gate (--check,
                  --self-test or a check_*.py), a lessons section (LESSONS / Traps / relearned / what each round
                  taught), files under references/; grade A = all four, B = kit and gate, C = one of them, D = none.
                  A grade is the mechanical proxy; the hand audit is the plan's appendix.

Run `git fetch origin` first when the counts should include what others pushed. Exit 0; 2 on a setup error.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CHECKLIST = os.path.join(ROOT, "Docs", "UNITY_VERIFICATION_CHECKLIST.md")
MENU = os.path.join(ROOT, "Docs", "MENU_REDESIGN_B.md")
SKILLS = os.path.join(ROOT, ".claude", "skills")

REVERT = re.compile(r"^(Revert|revert)\b")
NEGATIVE_CONTROL = re.compile(r"negative[- ]control|never merge", re.I)
RETIRED = re.compile(r"\bretire[sd]?\b", re.I)
AGAIN = re.compile(r"\bagain\b", re.I)
COMPILE_FIX = re.compile(
    r"CS\d{4}|compile error|compiles again|compile fix|fix(?:es|ed)? (?:the )?compile|didn.t compile|"
    r"does not compile|build break|builds again",
    re.I,
)
DATE = re.compile(r"20\d\d-\d\d-\d\d")
RED = "\U0001F534"  # 🔴
GREEN_MARKS = ("✅", "\U0001F7E2")  # ✅ 🟢
DISLIKE = re.compile(r"(do not|don't|dont) (like|love)|\bhate\b|up your game|scrap the whole", re.I)


def git(*args: str) -> str:
    out = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True)
    if out.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {out.stderr.strip()}")
    return out.stdout


def commit_subjects(since: str) -> list[tuple[str, str]]:
    """(date, subject) for every non-merge commit on every fetched branch since the date, one per subject."""
    raw = git("log", "--all", "--no-merges", f"--since={since}", "--format=%ad%x09%s", "--date=short")
    seen: set[str] = set()
    rows: list[tuple[str, str]] = []
    for line in raw.splitlines():
        if "\t" not in line:
            continue
        date, subject = line.split("\t", 1)
        if subject in seen:
            continue
        seen.add(subject)
        rows.append((date, subject))
    return rows


def git_counters(since: str, rows: list[tuple[str, str]] | None = None) -> dict:
    rows = commit_subjects(since) if rows is None else rows
    reverts = [s for _, s in rows if REVERT.search(s) and not NEGATIVE_CONTROL.search(s)]
    retired = [s for _, s in rows if RETIRED.search(s)]
    again = [s for _, s in rows if AGAIN.search(s)]
    compile_fixes = [s for _, s in rows if COMPILE_FIX.search(s)]
    by_type: dict[str, int] = {}
    for _, s in rows:
        m = re.match(r"^([a-z]+)(\([^)]*\))?!?:", s)
        key = m.group(1) if m else "(no type)"
        by_type[key] = by_type.get(key, 0) + 1
    return {
        "since": since,
        "commits": len(rows),
        "by_type": dict(sorted(by_type.items(), key=lambda kv: -kv[1])),
        "reverts": len(reverts),
        "retired": len(retired),
        "again": len(again),
        "compile_fixes": len(compile_fixes),
        "samples": {"reverts": reverts[:8], "compile_fixes": compile_fixes[:8]},
    }


def checklist_counters(path: str = CHECKLIST) -> dict:
    if not os.path.exists(path):
        return {"missing": path}
    red_by_month: dict[str, int] = {}
    sections = red = verified = 0
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            if not line.startswith("## "):
                continue
            sections += 1
            if RED in line:
                red += 1
                m = DATE.search(line)
                if m:
                    month = m.group(0)[:7]
                    red_by_month[month] = red_by_month.get(month, 0) + 1
            elif any(mark in line for mark in GREEN_MARKS):
                verified += 1
    return {
        "sections": sections,
        "red": red,
        "verified": verified,
        "red_by_month": dict(sorted(red_by_month.items())),
    }


def ui_counters(path: str = MENU) -> dict:
    if not os.path.exists(path):
        return {"missing": path}
    revs = built = unrun = dislikes = 0
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            if line.startswith("## "):
                if re.match(r"^## \d+\. Rev\b", line):
                    revs += 1
                if re.match(r"^## \d+\. Built\b", line):
                    built += 1
                if re.search(r"NOT RUN IN EDITOR|awaiting in-editor|awaiting .*approval", line, re.I):
                    unrun += 1
            if DISLIKE.search(line):
                dislikes += 1
    return {"mockup_rounds": revs, "built_sections": built, "unrun_sections": unrun, "dislike_lines": dislikes}


def skill_table(root: str = SKILLS) -> list[dict]:
    rows: list[dict] = []
    if not os.path.isdir(root):
        return rows
    for name in sorted(os.listdir(root)):
        skill_md = os.path.join(root, name, "SKILL.md")
        if not os.path.exists(skill_md):
            continue
        with open(skill_md, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        refs_dir = os.path.join(root, name, "references")
        refs = len(os.listdir(refs_dir)) if os.path.isdir(refs_dir) else 0
        kit = bool(re.search(r"Tools/(Build|Compute|QA|NCA)/", text))
        gate = bool(re.search(r"--check\b|--self-test\b|\bcheck_\w+\.py|gates\.sh|verify_\w+\.py", text))
        lessons = bool(
            re.search(r"LESSONS|LEARNINGS|\bTraps\b|relearned|what each round taught|lessons", text, re.I)
        )
        score = sum((kit, gate, lessons, refs > 0))
        grade = {4: "A", 3: "B", 2: "B", 1: "C", 0: "D"}[score]
        if score == 3 and not (kit and gate):
            grade = "C"
        rows.append(
            {
                "skill": name,
                "lines": text.count("\n") + 1,
                "kit": kit,
                "gate": gate,
                "lessons": lessons,
                "refs": refs,
                "grade": grade,
            }
        )
    return rows


def render(counters: dict, checklist: dict, ui: dict, skills: list[dict] | None) -> str:
    out = []
    out.append(f"Rework ledger (commits since {counters['since']}, every fetched branch, one per subject)")
    out.append(f"  commits {counters['commits']}   " + "  ".join(f"{k} {v}" for k, v in list(counters["by_type"].items())[:6]))
    out.append(
        f"  reverts {counters['reverts']}   retired {counters['retired']}   again {counters['again']}   "
        f"compile fixes {counters['compile_fixes']}"
    )
    if "missing" not in checklist:
        months = "  ".join(f"{m} {n}" for m, n in checklist["red_by_month"].items())
        out.append(
            f"Verification checklist: {checklist['sections']} sections, {checklist['red']} red (never editor-verified), "
            f"{checklist['verified']} verified; red by month: {months}"
        )
    if "missing" not in ui:
        out.append(
            f"Menu redesign: {ui['mockup_rounds']} mockup rounds, {ui['built_sections']} 'Built' sections, "
            f"{ui['unrun_sections']} never run in the editor, {ui['dislike_lines']} 'do not like' lines"
        )
    if skills is not None:
        grades: dict[str, int] = {}
        for r in skills:
            grades[r["grade"]] = grades.get(r["grade"], 0) + 1
        out.append("Skills: " + "  ".join(f"{g} {grades.get(g, 0)}" for g in "ABCD") + f"  (of {len(skills)})")
        out.append(f"  {'skill':<24} {'lines':>5} {'kit':>4} {'gate':>5} {'lessons':>8} {'refs':>5}  grade")
        for r in sorted(skills, key=lambda r: (r["grade"], -r["lines"])):
            out.append(
                f"  {r['skill']:<24} {r['lines']:>5} {'Y' if r['kit'] else '-':>4} {'Y' if r['gate'] else '-':>5} "
                f"{'Y' if r['lessons'] else '-':>8} {r['refs']:>5}  {r['grade']}"
            )
    return "\n".join(out)


def self_test() -> int:
    rows = [
        ("2026-10-29", 'Revert "Merge pull request #1620 from froglet-studio/feat/tunnel-shader-watery-glass"'),
        ("2026-11-01", 'Revert "test(ci): deliberately broken C# line - negative control for the guard (never merge)"'),
        ("2026-10-29", "feat(tigermoth): retire the Butterfly; distance-priced drop"),
        ("2026-10-29", "fix(prism): a restored prism grows again, and blooms back in from zero"),
        ("2026-10-29", "fix(ui): KitRectMask2D uses the public API (CS0117 compile error)"),
        ("2026-10-29", "docs(wormhole): name the removal commit"),
        ("2026-10-29", "docs(wormhole): name the removal commit"),  # a duplicate subject counts once
    ]
    # dedupe the way commit_subjects does
    seen: set[str] = set()
    rows = [r for r in rows if not (r[1] in seen or seen.add(r[1]))]
    c = git_counters("2026-09-01", rows)
    assert c["commits"] == 6, c
    assert c["reverts"] == 1, c  # the negative control is not a revert of work
    assert c["retired"] == 1, c
    assert c["again"] == 1, c
    assert c["compile_fixes"] == 1, c
    assert c["by_type"]["docs"] == 1, c

    import tempfile

    with tempfile.TemporaryDirectory() as tmp:
        p = os.path.join(tmp, "c.md")
        with open(p, "w", encoding="utf-8") as fh:
            fh.write(
                "## \U0001F534 one (2026-10-17) NOT EDITOR-VERIFIED\n## ✅ two (2026-10-18)\n"
                "## \U0001F534 three (2026-11-02)\n## plain\n"
            )
        ck = checklist_counters(p)
        assert ck == {"sections": 4, "red": 2, "verified": 1, "red_by_month": {"2026-10": 1, "2026-11": 1}}, ck
        m = os.path.join(tmp, "m.md")
        with open(m, "w", encoding="utf-8") as fh:
            fh.write(
                "## 1. Rev 2 mockups\n## 2. Built: x (NOT RUN IN EDITOR)\nShombith: \"I do not like this UI\"\n"
                "## 3. Rev 3 mockups (awaiting in-editor approval)\n"
            )
        u = ui_counters(m)
        assert u == {"mockup_rounds": 2, "built_sections": 1, "unrun_sections": 2, "dislike_lines": 1}, u
        sk = os.path.join(tmp, "skills")
        os.makedirs(os.path.join(sk, "a", "references"))
        with open(os.path.join(sk, "a", "references", "LESSONS.md"), "w") as fh:
            fh.write("x")
        with open(os.path.join(sk, "a", "SKILL.md"), "w") as fh:
            fh.write("uses Tools/Build/kit.py --check and references/LESSONS.md\n")
        os.makedirs(os.path.join(sk, "b"))
        with open(os.path.join(sk, "b", "SKILL.md"), "w") as fh:
            fh.write("prose only\n")
        t = skill_table(sk)
        assert [r["grade"] for r in t] == ["A", "D"], t
    # the real repo: the checklist must still parse, and no grade can be computed for a missing dir
    assert skill_table(os.path.join(tmp, "nope")) == []
    print("rework_ledger: self-test OK")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--since", default="2026-09-01", help="count commits from this date (default 2026-09-01)")
    ap.add_argument("--skills", action="store_true", help="also print the skill maturity table")
    ap.add_argument("--json", action="store_true", help="print everything as JSON")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    try:
        _dt.date.fromisoformat(args.since)
    except ValueError:
        print(f"rework_ledger: --since wants YYYY-MM-DD, not {args.since}", file=sys.stderr)
        return 2
    try:
        counters = git_counters(args.since)
    except RuntimeError as exc:
        print(f"rework_ledger: {exc}", file=sys.stderr)
        return 2
    checklist = checklist_counters()
    ui = ui_counters()
    skills = skill_table() if (args.skills or args.json) else None
    if args.json:
        print(json.dumps({"git": counters, "checklist": checklist, "ui": ui, "skills": skills}, indent=2))
        return 0
    print(render(counters, checklist, ui, skills))
    return 0


if __name__ == "__main__":
    sys.exit(main())
