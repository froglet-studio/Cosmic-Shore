#!/usr/bin/env python3
"""check_agent_first.py - the machine checks behind Docs/claude/AGENT_FIRST_ARCHITECTURE.md.

Three rules a cold session cannot afford to have broken, each held as a RATCHET: what breaks the rule today
is recorded in agent_first_baseline.json and may only shrink; anything new fails.

  size        (rule 2, locality)  a first-party runtime .cs file over LIMIT lines. A baselined file may not
              grow more than GROWTH lines past its recorded size.
  names       (rule 5, honest names)  a script some .prefab/.unity/.asset binds by guid that declares no type
              named like its file. Unity and Amoebius bind a script through its file name.
  reflection  (rule 4, no hidden links)  a member looked up by a string the compiler cannot follow:
              GetField("x") / GetMethod / GetProperty / Type.GetType("...") / Invoke("x") / SendMessage("x") /
              StartCoroutine("x"). Counted per file; a file may not gain one.

    python3 Tools/Build/check_agent_first.py              # the gate
    python3 Tools/Build/check_agent_first.py --update     # rewrite the baseline (it only ever shrinks: a new
                                                          # violation is refused; fix it or split the file)
    python3 Tools/Build/check_agent_first.py --self-test  # each rule is shown to fire on a planted violation

READER: it writes nothing but its own baseline, and only with --update.
"""
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BASELINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "agent_first_baseline.json")
SCAN = ["Assets/_Scripts", "Assets/FTUE"]
LIMIT = 1500      # lines; 37 files were over it when the rule was written (2026-11-10)
GROWTH = 40       # a baselined file may drift this far before it must be split

TYPE_RE = re.compile(r"\b(?:class|struct|interface|enum|record)\s+(\w+)")
REFLECT_RE = re.compile(
    r"\b(?:GetField|GetMethod|GetProperty|GetNestedType|GetEvent)\s*\(\s*\"|"
    r"\bType\.GetType\s*\(\s*\"|"
    r"\b(?:Invoke|InvokeRepeating|SendMessage|SendMessageUpwards|BroadcastMessage|StartCoroutine|StopCoroutine)\s*\(\s*\"")
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")


def strip_comments(src):
    src = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)


def runtime_cs():
    out = subprocess.run(["git", "-C", ROOT, "ls-files", "-z"] + [s + "/*.cs" for s in SCAN],
                         capture_output=True, text=True, check=True).stdout.split("\0")
    return [f for f in out if f and "/Editor/" not in f and "/Tests/" not in f and os.path.exists(os.path.join(ROOT, f))]


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8-sig", errors="replace") as fh:
        return fh.read()


def size_of(src):
    return src.count("\n") + (0 if src.endswith("\n") or not src else 1)


def names_violation(rel, src):
    """True when the file declares no type named like its stem (a part file Type.Part.cs counts as Type)."""
    stem = os.path.splitext(os.path.basename(rel))[0]
    types = set(TYPE_RE.findall(strip_comments(src)))
    return stem not in types and stem.split(".")[0] not in types


def reflection_count(src):
    return len(REFLECT_RE.findall(strip_comments(src)))


def bound_guids():
    """Every guid some scene, prefab or asset names."""
    out = subprocess.run(["git", "-C", ROOT, "grep", "-h", "-o", "-E", "guid: [0-9a-f]{32}", "--",
                          "Assets/*.prefab", "Assets/*.unity", "Assets/*.asset"],
                         capture_output=True, text=True).stdout
    return set(GUID_RE.findall(out))


def measure():
    files = runtime_cs()
    bound = bound_guids()
    size, names, reflection = {}, [], {}
    for rel in files:
        src = read(rel)
        n = size_of(src)
        if n > LIMIT:
            size[rel] = n
        r = reflection_count(src)
        if r:
            reflection[rel] = r
        if names_violation(rel, src):
            meta = os.path.join(ROOT, rel + ".meta")
            if os.path.exists(meta):
                g = GUID_RE.search(open(meta, encoding="utf-8", errors="replace").read())
                if g and g.group(1) in bound:
                    names.append(rel)
    return {"size": size, "names": sorted(names), "reflection": reflection}


def compare(now, base):
    """The failures of `now` against `base`, and the baseline entries that are stale (fixed, moved or gone)."""
    fails, stale = [], []
    for rel, n in sorted(now["size"].items()):
        was = base["size"].get(rel)
        if was is None:
            fails.append(f"size        {rel}: {n} lines, over {LIMIT}. Split it by job (rule 2).")
        elif n > was + GROWTH:
            fails.append(f"size        {rel}: grew {was} -> {n} (more than {GROWTH} past its baseline). Split it, do not grow it.")
    stale += [f"size        {rel}" for rel in base["size"] if rel not in now["size"]]
    for rel in now["names"]:
        if rel not in base["names"]:
            fails.append(f"names       {rel}: an asset binds this script, and it declares no type named like the file (rule 5).")
    stale += [f"names       {rel}" for rel in base["names"] if rel not in now["names"]]
    for rel, n in sorted(now["reflection"].items()):
        was = base["reflection"].get(rel, 0)
        if n > was:
            fails.append(f"reflection  {rel}: {n} string-named lookups (baseline {was}). Use nameof, a reference or an interface (rule 4).")
    stale += [f"reflection  {rel}" for rel, was in base["reflection"].items() if now["reflection"].get(rel, 0) < was]
    return fails, stale


def load_baseline():
    if not os.path.exists(BASELINE):
        return {"size": {}, "names": [], "reflection": {}}
    with open(BASELINE, encoding="utf-8") as fh:
        return json.load(fh)


def save_baseline(now):
    with open(BASELINE, "w", encoding="utf-8") as fh:
        json.dump({"limit": LIMIT, "growth": GROWTH, **now}, fh, indent=1, sort_keys=True)
        fh.write("\n")


def self_test():
    base = {"size": {"A.cs": 1600}, "names": ["Old.cs"], "reflection": {"R.cs": 2}}
    checks = [
        ("a file at its baseline passes", compare({"size": {"A.cs": 1600 + GROWTH}, "names": ["Old.cs"], "reflection": {"R.cs": 2}}, base)[0] == []),
        ("a new file over the limit fails", len(compare({"size": {"A.cs": 1600, "New.cs": LIMIT + 1}, "names": ["Old.cs"], "reflection": {"R.cs": 2}}, base)[0]) == 1),
        ("a baselined file that grows past the allowance fails", len(compare({"size": {"A.cs": 1600 + GROWTH + 1}, "names": ["Old.cs"], "reflection": {"R.cs": 2}}, base)[0]) == 1),
        ("a newly asset-bound misnamed script fails", len(compare({"size": {"A.cs": 1600}, "names": ["Old.cs", "Bad.cs"], "reflection": {"R.cs": 2}}, base)[0]) == 1),
        ("one more string-named lookup in a file fails", len(compare({"size": {"A.cs": 1600}, "names": ["Old.cs"], "reflection": {"R.cs": 3}}, base)[0]) == 1),
        ("a string-named lookup in a new file fails", len(compare({"size": {"A.cs": 1600}, "names": ["Old.cs"], "reflection": {"R.cs": 2, "N.cs": 1}}, base)[0]) == 1),
        ("a fixed entry is reported stale, not failed", compare({"size": {}, "names": [], "reflection": {}}, base) == ([], ["size        A.cs", "names       Old.cs", "reflection  R.cs"])),
        ("the misnamed-file reader fires", names_violation("Assets/X/VesselHUD.cs", "namespace N { class ShipHUD {} }")),
        ("a matching file is clean", not names_violation("Assets/X/VesselHUD.cs", "namespace N { class VesselHUD {} }")),
        ("a partial part file counts as its type", not names_violation("Assets/X/Ball.Visuals.cs", "partial class Ball {}")),
        ("a type named only in a comment does not count", names_violation("Assets/X/Foo.cs", "// class Foo\nclass Bar {}")),
        ("reflection by string is counted", reflection_count('x.GetType().GetField("lastReport"); Invoke("Tick", 1f);') == 2),
        ("nameof and typed calls are not", reflection_count('GetField(nameof(x)); Invoke(Tick); GetComponent<Foo>();') == 0),
        ("a commented-out lookup is not", reflection_count('// GetField("x")\n/* Invoke("y") */') == 0),
    ]
    for name, ok in checks:
        print(f"  {'PASS' if ok else 'FAIL'}  {name}")
    bad = [n for n, ok in checks if not ok]
    print(f"\nself-test: {'OK' if not bad else 'FAILED'} ({len(checks)} checks)")
    return 1 if bad else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    now, base = measure(), load_baseline()
    fails, stale = compare(now, base)
    if "--update" in sys.argv:
        if fails and os.path.exists(BASELINE):
            print("check_agent_first: --update refused, the baseline only shrinks. New violations:\n")
            for f in fails:
                print("  " + f)
            return 1
        save_baseline(now)
        print(f"check_agent_first: baseline written ({len(now['size'])} over {LIMIT} lines, {len(now['names'])} "
              f"misnamed asset-bound scripts, {sum(now['reflection'].values())} string-named lookups in "
              f"{len(now['reflection'])} files)")
        return 0
    if fails:
        print("check_agent_first: FAILED (Docs/claude/AGENT_FIRST_ARCHITECTURE.md)\n")
        for f in fails:
            print("  " + f)
        return 1
    print(f"check_agent_first: OK ({len(now['size'])} files over {LIMIT} lines, {len(now['names'])} misnamed asset-bound "
          f"scripts, {sum(now['reflection'].values())} string-named lookups: all in the baseline)")
    if stale:
        print(f"  {len(stale)} baseline entr{'y is' if len(stale) == 1 else 'ies are'} fixed or moved; shrink it with --update:")
        for s in stale[:12]:
            print("    " + s)
    return 0


if __name__ == "__main__":
    sys.exit(main())
