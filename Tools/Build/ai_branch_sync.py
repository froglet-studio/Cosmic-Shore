#!/usr/bin/env python3
"""ai_branch_sync.py - what is waiting to come into ai-system, and which of it is AI work.

A READER (report only): it fetches and reads git, it never merges, commits or writes a file.
Workflow it serves: Docs/AI_SYSTEM/BRANCH_WORKFLOW.md (sections 2-4).

For each source branch (Ys-bleeding-edge, bleeding-edge, perf/performance-optimization) it prints
  - how many commits it has that HEAD lacks, and whether Ys-bleeding-edge already contains bleeding-edge
    (if so, merge Ys only);
  - the AI commits among them: a commit is AI work when it touches an AI path (AI_PATHS), adds or
    removes a line naming an AI hook (AI_CODE_PATTERN, which catches AI living in mode controllers and
    ability executors, e.g. the Scarab jukes and the Grizzly bombs), or its subject says AI/autopilot;
  - the AI files those commits touch that Docs/AI_SYSTEM/ARCHITECTURE.md does not mention yet (intake).
It also prints what ai-system has that perf lacks (when to merge ai-system into perf for testing).

Usage:
  python3 Tools/Build/ai_branch_sync.py              # fetch the sources, then report
  python3 Tools/Build/ai_branch_sync.py --no-fetch   # report on what is already fetched
  python3 Tools/Build/ai_branch_sync.py --self-test  # check the classifiers
"""
import argparse
import os
import re
import subprocess
import sys
import time

REMOTE = "origin"
SOURCES = ["Ys-bleeding-edge", "bleeding-edge", "perf/performance-optimization"]
PERF = "perf/performance-optimization"
ROSTER = "Docs/AI_SYSTEM/ARCHITECTURE.md"

# Paths whose change is AI work by location.
AI_PATHS = [
    "Assets/_Scripts/Controller/AI/",
    "Assets/_Scripts/Utility/AITraining/",
    "Assets/_Scripts/Controller/Arcade/ButterflyGames/ButterflyAutopilotModeDriver.cs",
    "Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs",
    "Assets/_Scripts/Controller/Multiplayer/AIHullSeating.cs",
    "Assets/Resources/SkimRaceAIConfig",
    "Assets/Resources/SkimRaceDifficulty.asset",
    "Assets/_SO_Assets/AI Boost Policies/",
    "Tools/Build/skimrace_sim_harness/",
]
# A changed C# line naming one of these is AI work wherever the file lives.
# IsInitializedAsAI is deliberately NOT here: scoring, party and scoreboard UI read it to label a seat,
# so it flagged ~a dozen non-AI commits per sync (measured 2026-10-08). The hooks below are only ever
# named by code that DRIVES a vessel for the autopilot.
AI_CODE_PATTERN = (r"IsAutopilotDriven|AutoPilotEnabled|\bAIPilot\b|"
                   r"SetExternalTargetProvider|SetDriftLookTargetProvider|AutopilotDriver|"
                   r"AIBoostPolicy|JukePlanner")
AI_SUBJECT = re.compile(r"\bAI\b|\bautopilot\b|\bAIPilot\b|\bbots?\b", re.IGNORECASE)
AI_SUBJECT_CASED = re.compile(r"\bAI\b|[Aa]utopilot|\bAIPilot\b|\bbots?\b")


def git(*args, check=True):
    r = subprocess.run(["git", *args], capture_output=True, text=True)
    if check and r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)}: {r.stderr.strip()}")
    return r.stdout


def fetch(branches):
    for delay in (0, 2, 4, 8, 16):
        if delay:
            time.sleep(delay)
        r = subprocess.run(["git", "fetch", "-q", REMOTE, *branches], capture_output=True, text=True)
        if r.returncode == 0:
            return True
        print(f"  fetch failed ({r.stderr.strip()[:120]}), retrying", file=sys.stderr)
    return False


def is_ai_path(path):
    return any(path.startswith(p) for p in AI_PATHS)


def subject_is_ai(subject):
    return bool(AI_SUBJECT_CASED.search(subject))


def roster_unknown(paths, roster_text):
    """AI-relevant .cs files whose class name the roster does not mention."""
    out = []
    for p in paths:
        if not p.endswith(".cs"):
            continue
        name = os.path.splitext(os.path.basename(p))[0]
        if name not in roster_text:
            out.append(p)
    return out


def commits(range_spec, *options, paths=()):
    """Commits in range_spec. The range goes BEFORE "--": after it, git reads it as a path."""
    args = ["log", "--no-merges", "--format=%h%x09%ad%x09%an%x09%s", "--date=short", *options, range_spec]
    if paths:
        args += ["--", *paths]
    raw = git(*args)
    return [line.split("\t", 3) for line in raw.splitlines() if line.strip()]


def files_of(sha, pattern=None):
    """Files a commit touched; with pattern, only the files whose own diff adds/removes a matching line
    (git's -G shows just the matching file pairs unless --pickaxe-all is given)."""
    args = ["show", "--name-only", "--format="] + (["-G", pattern] if pattern else []) + [sha]
    return [f for f in git(*args).splitlines() if f.strip()]


def ai_commits(range_spec):
    by_path = {c[0]: c for c in commits(range_spec, paths=AI_PATHS)}
    by_code = {c[0]: c for c in commits(range_spec, "-G", AI_CODE_PATTERN, paths=["*.cs"])}
    found = {}
    for c in commits(range_spec):
        sha = c[0]
        reasons = []
        if sha in by_path:
            reasons.append("AI path")
        if sha in by_code:
            reasons.append("AI hook in code")
        if subject_is_ai(c[3]):
            reasons.append("subject")
        if reasons:
            found[sha] = (c, reasons)
    return found


def report_source(branch, roster_text):
    ref = f"{REMOTE}/{branch}"
    if subprocess.run(["git", "rev-parse", "-q", "--verify", ref], capture_output=True).returncode != 0:
        print(f"\n== {branch}: not fetched")
        return
    ahead = int(git("rev-list", "--count", f"HEAD..{ref}").strip())
    tip = git("log", "-1", "--format=%h %ad %s", "--date=short", ref).strip()
    print(f"\n== {branch}  (tip {tip[:90]})")
    print(f"   commits not in HEAD: {ahead}")
    if branch == "Ys-bleeding-edge":
        be = f"{REMOTE}/bleeding-edge"
        if subprocess.run(["git", "rev-parse", "-q", "--verify", be], capture_output=True).returncode == 0:
            behind = int(git("rev-list", "--count", f"{ref}..{be}").strip())
            print(f"   behind bleeding-edge: {behind}"
                  + ("  -> merge Ys-bleeding-edge only" if behind == 0 else "  -> merge bleeding-edge first"))
    if ahead == 0:
        return
    found = ai_commits(f"HEAD..{ref}")
    print(f"   AI commits: {len(found)}")
    touched = set()
    for sha, (c, reasons) in found.items():
        print(f"     {c[0]} {c[1]} {c[2][:14]:14} | {c[3][:88]}  [{', '.join(reasons)}]")
        touched.update(f for f in files_of(sha) if f.endswith(".cs") and is_ai_path(f))
        if "AI hook in code" in reasons:
            touched.update(f for f in files_of(sha, AI_CODE_PATTERN) if f.endswith(".cs"))
    unknown = roster_unknown(sorted(touched), roster_text)
    if unknown:
        print("   AI files the roster does not mention (intake: ARCHITECTURE.md section 2):")
        for f in unknown:
            print(f"     {f}")


def report_outgoing(roster_text):
    ref = f"{REMOTE}/{PERF}"
    if subprocess.run(["git", "rev-parse", "-q", "--verify", ref], capture_output=True).returncode != 0:
        return
    n = int(git("rev-list", "--count", f"{ref}..HEAD").strip())
    found = ai_commits(f"{ref}..HEAD") if n else {}
    print(f"\n== HEAD -> {PERF}: {n} commits perf lacks, {len(found)} of them AI work"
          + ("  (merge ai-system into perf when these need perf testing)" if found else ""))


def self_test():
    ok = True
    def expect(cond, what):
        nonlocal ok
        print(("ok   " if cond else "FAIL ") + what)
        ok &= cond
    expect(subject_is_ai("feat(regatta): AI Urchins choose their rail"), "subject: 'AI Urchins' is AI")
    expect(subject_is_ai("fix(grizzly): Autopilot bombs"), "subject: 'Autopilot' is AI")
    expect(not subject_is_ai("fix(ui): said hello to the main menu"), "subject: 'said' is not AI")
    expect(not subject_is_ai("feat(crystal): domain swap"), "subject: plain feature is not AI")
    expect(is_ai_path("Assets/_Scripts/Controller/AI/Urchin/UrchinRailAssessment.cs"), "path: Controller/AI is AI")
    expect(not is_ai_path("Assets/_Scripts/Controller/Arcade/Regatta/RegattaController.cs"), "path: a controller is not AI by path")
    expect(re.search(AI_CODE_PATTERN, "bool IsAutopilotDriven => _status.AIPilot != null;") is not None,
           "code: an executor's autopilot branch is AI")
    expect(re.search(AI_CODE_PATTERN, "if (p == null || !p.IsInitializedAsAI) continue;") is None,
           "code: a seat label read (IsInitializedAsAI) alone is not AI work")
    roster = "| Urchin | `UrchinAutopilotDriver` |"
    expect(roster_unknown(["A/UrchinAutopilotDriver.cs", "A/NewThingAI.cs", "A/x.md"], roster) == ["A/NewThingAI.cs"],
           "roster: names only the .cs files the roster lacks")
    return ok


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--no-fetch", action="store_true", help="do not fetch; report on what is already fetched")
    ap.add_argument("--self-test", action="store_true", help="check the classifiers and exit")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(0 if self_test() else 1)
    root = git("rev-parse", "--show-toplevel").strip()
    os.chdir(root)
    if not a.no_fetch and not fetch(SOURCES):
        print("fetch failed; reporting on what is already fetched", file=sys.stderr)
    branch = git("rev-parse", "--abbrev-ref", "HEAD").strip()
    print(f"HEAD: {branch} {git('rev-parse', '--short', 'HEAD').strip()}")
    if branch != "ai-system":
        print("  (not on ai-system - the report compares the sources against this HEAD)")
    roster_text = open(ROSTER, encoding="utf-8").read() if os.path.exists(ROSTER) else ""
    for b in SOURCES:
        report_source(b, roster_text)
    report_outgoing(roster_text)


if __name__ == "__main__":
    main()
