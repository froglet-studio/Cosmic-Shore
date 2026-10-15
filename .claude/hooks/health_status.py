#!/usr/bin/env python3
"""health_status.py - a UserPromptSubmit hook (.claude/settings.json): when someone asks a session about the
performance or bug-hunt STATUS, or what to test or do next, it hands Claude the current status and next steps so the
answer comes from the measurements, not from memory (Yash, 2026-10-15: "only in Claude, whenever someone asks about
the performance status or what to do next or what to test").

It reads Docs/perf/status.json, which every `python3 Tools/Build/perf_hunt.py sweep` writes (the daily workflow, the
/perf-hunt and /bug-ledger sessions): the gates, regressions, valid baselines, the Amoebius allocation run, the Bug
Ledger's open issues and the ordered next steps. It runs nothing slow (no git, no gates), never blocks a prompt and
always exits 0. Silent on every other prompt.

    python3 .claude/hooks/health_status.py --self-test
"""
import json
import os
import re
import sys

TOPIC = re.compile(r"\b(?:perf(?:ormance)?|fps|frame ?rate|frame ?time|lag|stutter|allocat\w*|gc|bug ?hunt|bugs?|"
                   r"console errors?|bug ledger|regressions?|baselines?)\b", re.IGNORECASE)
ASK = re.compile(r"\b(?:status|report|state|where (?:do|does|are|is) (?:we|it|things) stand|what(?:'s| is) next|"
                 r"what (?:should|do|can) (?:i|we) (?:do|test|run|fix|capture)|what to (?:do|test)|next steps?|to-?do|"
                 r"how (?:are|is) (?:we|it|things) doing|any(?:thing)? (?:regress\w*|broken|red)|recheck|check)\b", re.IGNORECASE)
WHAT_TO_TEST = re.compile(r"\bwhat (?:should|do|can) (?:i|we) test\b|\bwhat to test\b|\bwhat(?:'s| is) next\b|\bnext steps?\b",
                          re.IGNORECASE)


def root():
    return os.environ.get("CLAUDE_PROJECT_DIR") or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def wanted(prompt):
    if not prompt:
        return False
    return bool(WHAT_TO_TEST.search(prompt)) or (bool(TOPIC.search(prompt)) and bool(ASK.search(prompt)))


def context(prompt, base):
    if not wanted(prompt):
        return ""
    path = os.path.join(base, "Docs", "perf", "status.json")
    try:
        with open(path, encoding="utf-8") as fh:
            st = json.load(fh)
    except (OSError, ValueError):
        return ("PERFORMANCE & BUG HUNT STATUS: no Docs/perf/status.json yet. Run `python3 Tools/Build/perf_hunt.py "
                "status --refresh` and answer from its output (the /perf-hunt skill explains it).")
    sys.path.insert(0, os.path.join(base, "Tools", "Build"))
    try:
        import perf_hunt  # noqa: E402
        text = perf_hunt.status_text(st)
    except Exception:  # noqa: BLE001 - a hook must never break a prompt
        text = json.dumps({k: st.get(k) for k in ("generatedUtc", "gatesOk", "regressions", "baselines", "next")}, indent=1)
    return ("PERFORMANCE & BUG HUNT STATUS (from Docs/perf/status.json; answer from this, not from memory). If it is "
            "more than a day old or the person wants it fresh, run `git fetch origin` then `python3 Tools/Build/perf_hunt.py "
            "status --refresh`. Give the person the next steps in plain words, one action each, and name who can take "
            "each (Docs/TEAM_TASKS.md). Load /perf-hunt to fix a performance item and /bug-ledger to fix a console bug.\n"
            + text)


def self_test():
    assert wanted("what's the performance status?") and wanted("what should I test next") and wanted("bug hunt report")
    assert wanted("any regressions in perf?") and wanted("recheck the performance")
    assert not wanted("make the Regatta AI turn earlier") and not wanted("fix the HUD colours")
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        assert "no Docs/perf/status.json" in context("performance status", tmp)
        os.makedirs(os.path.join(tmp, "Docs", "perf"))
        with open(os.path.join(tmp, "Docs", "perf", "status.json"), "w") as fh:
            json.dump({"generatedUtc": "t", "gatesOk": True, "gates": [], "regressions": [], "baselines": [],
                       "amoebius": [], "bugs": {"open": 1, "validating": 0}, "next": ["Capture S2"]}, fh)
        out = context("what to test next?", tmp)
        assert "PERFORMANCE & BUG HUNT STATUS" in out and "Capture S2" in out, out
        assert context("rename the Stoat", tmp) == ""
    print("self-test ok")


def main():
    try:
        payload = json.load(sys.stdin)
    except ValueError:
        return
    text = context(payload.get("prompt") or "", root())
    if text:
        print(text)   # plain stdout is added to the prompt's context, as the other hooks here do


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        self_test()   # outside the guard below: a failing self-test must fail loudly
        sys.exit(0)
    try:
        main()
    except Exception:  # noqa: BLE001
        pass
    sys.exit(0)
