#!/usr/bin/env python3
"""
Detect lines that flip back and forth across git history.

Algorithm:
  For each file, walk every commit that touched it (oldest->newest) and record
  every line that was added (+) or removed (-). A "flip" is a line whose exact
  text was added in one commit and later removed (or vice versa) >= 2 times,
  with >= 2 sign transitions. The commits that toggle the line are reported
  as candidate competing changes.

Usage:
  detect_flips.py                         # scan all files with >= 3 touching commits
  detect_flips.py path [path...]          # scan specific paths only
  detect_flips.py --since=6.months
  detect_flips.py --min-commits=4
"""
import subprocess, collections, re, sys, os

def git(*args):
    return subprocess.check_output(["git", *args], text=True, errors="replace")

def build_subject_cache():
    out = git("log", "--all", "--no-merges", "--format=%H\t%s")
    cache = {}
    for line in out.splitlines():
        if "\t" in line:
            h, s = line.split("\t", 1)
            cache[h] = s
    return cache

SKIP_EXTS = {
    ".unity", ".prefab", ".asset", ".meta", ".mat", ".controller",
    ".anim", ".png", ".jpg", ".tga", ".fbx", ".wav", ".mp3",
    ".lock", ".json", ".mixer",
}

def list_churn(paths, min_commits, since, include_assets):
    cmd = ["log", "--pretty=format:", "--name-only", "--no-merges"]
    if since:
        cmd.append(f"--since={since}")
    if paths:
        cmd += ["--"] + paths
    out = git(*cmd)
    counts = collections.Counter(l.strip() for l in out.splitlines() if l.strip())
    results = []
    for f, c in counts.items():
        if c < min_commits:
            continue
        if not include_assets:
            ext = os.path.splitext(f)[1].lower()
            if ext in SKIP_EXTS:
                continue
        results.append((f, c))
    return sorted(results, key=lambda x: -x[1])

def commits_for_file(path):
    out = git("log", "--pretty=format:%H", "--reverse", "--follow", "--", path)
    return [c for c in out.splitlines() if c]

def diff_for_file(commit, path):
    try:
        return git("show", "--format=", "--unified=0", "--no-color", commit, "--", path)
    except subprocess.CalledProcessError:
        return ""

NOISE = re.compile(r"^\s*//|^\s*/\*|^\s*\*|^\s*#|^\s*<!--")
TRIVIAL = {"{", "}", "};", "[", "]", "),", ");", "},", "];"}

def interesting(line):
    s = line.strip()
    if not s or len(s) < 8 or s in TRIVIAL:
        return False
    if NOISE.match(line):
        return False
    return True

def iter_adds_removes(diff_text):
    for raw in diff_text.splitlines():
        if raw.startswith("+++") or raw.startswith("---") or raw.startswith("@@"):
            continue
        if raw.startswith("+"):
            yield "+", raw[1:]
        elif raw.startswith("-"):
            yield "-", raw[1:]

def analyze(path):
    commits = commits_for_file(path)
    # Collapse per-commit: if a line is touched in commit C, record one event
    # per sign (+, -, or both) so YAML repetition and same-commit noise
    # don't inflate the flip score.
    per_commit = collections.defaultdict(lambda: collections.defaultdict(set))
    # per_commit[line_text][commit_hash] = {"+"} | {"-"} | {"+","-"}
    for c in commits:
        for sign, text in iter_adds_removes(diff_for_file(c, path)):
            if interesting(text):
                per_commit[text][c].add(sign)

    flips = []
    for text, commit_signs in per_commit.items():
        # Build ordered event list: one entry per (commit, sign)
        events = []
        for c in commits:
            signs = commit_signs.get(c)
            if not signs:
                continue
            # If both +/- in same commit, record as a single "+/-" toggle event
            if signs == {"+", "-"}:
                events.append((c, "*"))
            else:
                events.append((c, next(iter(signs))))
        signs = [s for _, s in events]
        transitions = sum(1 for a, b in zip(signs, signs[1:]) if a != b)
        plus = sum(1 for s in signs if s in ("+", "*"))
        minus = sum(1 for s in signs if s in ("-", "*"))
        # Flip = line re-added after being removed (or re-removed after being
        # re-added): total >= 3 events, both signs present, >= 2 transitions.
        if len(events) >= 3 and transitions >= 2 and plus >= 1 and minus >= 1:
            flips.append({
                "text": text,
                "plus": plus,
                "minus": minus,
                "transitions": transitions,
                "events": events,
            })
    flips.sort(key=lambda f: (-f["transitions"], -(f["plus"] + f["minus"])))
    return flips

def main():
    argv = sys.argv[1:]
    since = None
    paths = []
    min_commits = 3
    include_assets = False
    for a in argv:
        if a.startswith("--since="):
            since = a.split("=", 1)[1]
        elif a.startswith("--min-commits="):
            min_commits = int(a.split("=", 1)[1])
        elif a == "--include-assets":
            include_assets = True
        else:
            paths.append(a)

    subject_cache = build_subject_cache()
    if paths:
        # Explicit paths: skip churn filter so shallow/grafted history
        # or rename chains don't drop candidates.
        files = [(p, 0) for p in paths]
    else:
        files = list_churn(None, min_commits, since, include_assets)
    print(f"Scanning {len(files)} files (>= {min_commits} touching commits)...",
          file=sys.stderr)

    results = []
    for f, _c in files:
        try:
            flips = analyze(f)
        except subprocess.CalledProcessError:
            continue
        if flips:
            results.append((f, flips))

    results.sort(key=lambda x: -sum(f["transitions"] for f in x[1]))
    for path, flips in results:
        print(f"\n=== {path}  ({len(flips)} flipping lines) ===")
        for f in flips[:5]:
            snippet = f["text"].strip()[:140]
            print(f"  [+{f['plus']} -{f['minus']} t={f['transitions']}] {snippet}")
            for c, s in f["events"]:
                subj = subject_cache.get(c, "")[:90]
                print(f"      {s} {c[:9]}  {subj}")
    print(f"\nTotal files with flips: {len(results)}", file=sys.stderr)

if __name__ == "__main__":
    main()
