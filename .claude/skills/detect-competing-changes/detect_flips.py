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
  detect_flips.py path [path...]          # scan specific files OR directories
  detect_flips.py --since=6.months
  detect_flips.py --min-commits=4
  detect_flips.py --help
"""
import subprocess, collections, re, sys, os

def git(*args):
    return subprocess.check_output(["git", *args], text=True, errors="replace")

def shallow_boundary_commits():
    """Commits whose parents are absent from the object store (a shallow clone).

    `git show` on such a commit has nothing to diff against, so it reports the
    ENTIRE file as added - every line gets a spurious '+'. Left in, that alone
    manufactures flips for every line of every file a boundary commit touched,
    which is indistinguishable from a real result. Skip them and say so.
    """
    try:
        git_dir = git("rev-parse", "--git-dir").strip()
    except subprocess.CalledProcessError:
        return set()
    shallow = os.path.join(git_dir, "shallow")
    if not os.path.isfile(shallow):
        return set()
    with open(shallow) as fh:
        return {l.strip() for l in fh if l.strip()}

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

def commits_for_file(path, since=None):
    # --follow needs a single pathspec and only makes sense for a file.
    cmd = ["log", "--pretty=format:%H", "--reverse", "--follow"]
    if since:
        cmd.append(f"--since={since}")
    cmd += ["--", path]
    out = git(*cmd)
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

def analyze(path, since=None, skip_commits=frozenset()):
    commits = [c for c in commits_for_file(path, since) if c not in skip_commits]
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

USAGE = """detect_flips.py - find lines that git history keeps flipping back and forth.

  detect_flips.py                      scan every file with >= 3 touching commits
  detect_flips.py PATH [PATH...]       scan specific files or directories
                                       (a directory is expanded to the tracked
                                       files under it, with the same filters)

  --min-commits=N    only scan files touched by >= N commits (default 3).
                     Ignored for explicitly-named paths, which are always scanned.
  --since=WHEN       restrict history to WHEN (e.g. 6.months). Applies to BOTH
                     which files are scanned and how far back each is walked.
  --include-assets   also scan Unity scenes/prefabs/meta files (very noisy).
  --help             this message.
"""

def expand_paths(paths, since, include_assets):
    """Turn user-supplied paths (files or directories) into tracked files.

    A directory must be expanded: `git show -- <dir>` diffs everything beneath it,
    so treating a directory as one "file" mixes every file's lines together and
    floods the result with .meta boilerplate. Expansion also re-applies the
    asset-extension filter, which the explicit-path branch used to skip entirely.
    """
    out = []
    seen = set()
    for raw in paths:
        # min_commits=1: an explicitly-named path is never dropped for low churn,
        # which was the original reason this branch bypassed the filter.
        expanded = list_churn([raw], 1, since, include_assets)
        if expanded:
            for f, c in expanded:
                if f not in seen:
                    seen.add(f)
                    out.append((f, c))
        elif os.path.isfile(raw):
            # Tracked-but-unmatched (e.g. grafted history) - keep it rather than
            # silently dropping a path the user named.
            if raw not in seen:
                seen.add(raw)
                out.append((raw, 0))
        else:
            print(f"warning: no tracked files matched {raw!r}", file=sys.stderr)
    return out

def main():
    argv = sys.argv[1:]
    since = None
    paths = []
    min_commits = 3
    include_assets = False
    for a in argv:
        if a in ("--help", "-h"):
            print(USAGE)
            return 0
        elif a.startswith("--since="):
            since = a.split("=", 1)[1]
        elif a.startswith("--min-commits="):
            min_commits = int(a.split("=", 1)[1])
        elif a == "--include-assets":
            include_assets = True
        elif a.startswith("-"):
            # Without this an unrecognised flag is silently treated as a path,
            # which is how `--help` used to scan a file named "--help".
            print(f"unknown option: {a}\n", file=sys.stderr)
            print(USAGE, file=sys.stderr)
            return 2
        else:
            paths.append(a)

    shallow = shallow_boundary_commits()
    if shallow:
        print(f"note: shallow clone - ignoring {len(shallow)} graft-boundary "
              f"commit(s) whose diffs would read as whole-file additions. "
              f"Run in a full clone for complete history.", file=sys.stderr)

    subject_cache = build_subject_cache()
    if paths:
        files = expand_paths(paths, since, include_assets)
    else:
        files = list_churn(None, min_commits, since, include_assets)

    if not files:
        print("No files to scan.", file=sys.stderr)
        return 0

    scope = "named paths" if paths else f">= {min_commits} touching commits"
    print(f"Scanning {len(files)} files ({scope})...", file=sys.stderr)

    results = []
    for f, _c in files:
        try:
            flips = analyze(f, since, shallow)
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
    return 0

if __name__ == "__main__":
    sys.exit(main())
