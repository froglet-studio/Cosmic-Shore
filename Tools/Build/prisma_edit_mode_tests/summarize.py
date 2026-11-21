#!/usr/bin/env python3
"""Per-suite pass/fail table and grouped failures from a dotnet test TRX file (run.sh calls it).

  summarize.py results.trx [--table] [--all] [--known known_divergences.txt]

With --known, failures listed in that file are reported apart as known engine divergences, and the
exit code is 1 if any OTHER test failed or any listed test passed (a stale entry), else 0.
"""
import sys, re, collections, xml.etree.ElementTree as ET
known = {}
if "--known" in sys.argv:
    with open(sys.argv[sys.argv.index("--known") + 1]) as fh:
        for line in fh:
            line = line.strip()
            if line and not line.startswith("#"):
                key, _, why = line.partition(" ")
                known[key] = why.strip()
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(sys.argv[1]).getroot()
defs = {u.get("id"): u.find("t:TestMethod", ns).get("className") for u in root.iter("{%s}UnitTest" % ns["t"])}
per = collections.defaultdict(lambda: [0, 0]); fails = collections.defaultdict(list)
diverged, stale = [], []
for r in root.iter("{%s}UnitTestResult" % ns["t"]):
    cls = defs.get(r.get("testId"), "?").split(".")[-1]
    ok = r.get("outcome") == "Passed"
    key = f"{cls}.{r.get('testName')}"
    if key in known:
        (stale if ok else diverged).append(key)
        continue
    per[cls][0 if ok else 1] += 1
    if not ok:
        msg = (r.findtext(".//t:Message", default="", namespaces=ns) or "").strip().splitlines()
        st = (r.findtext(".//t:StackTrace", default="", namespaces=ns) or "").strip().splitlines()
        fails[cls].append((r.get("testName"), (msg[0] if msg else "")[:170], st[0].strip()[:170] if st else ""))
if "--table" in sys.argv:
    print(f"{'suite':44} pass fail")
    for cls, (p, f) in sorted(per.items(), key=lambda kv: (-kv[1][1], kv[0])): print(f"{cls:44} {p:4} {f:4}")
for cls, items in sorted(fails.items(), key=lambda kv: -len(kv[1])):
    print(f"== {cls} ({len(items)} failing)")
    seen = collections.Counter()
    for name, m, s in items:
        key = re.sub(r"\d+", "#", m)
        seen[key] += 1
        if seen[key] <= (99 if "--all" in sys.argv else 2): print(f"   {name}: {m}\n      at {s}")
    for k, n in seen.items():
        if n > 2 and "--all" not in sys.argv: print(f"   ... {n}x total: {k[:120]}")
if diverged:
    print(f"== known engine divergences ({len(diverged)}, not counted - see known_divergences.txt)")
    for k in diverged: print(f"   {k}: {known[k]}")
for k in stale:
    print(f"== STALE known_divergences.txt entry: {k} now PASSES on Prisma - remove it")
if "--known" in sys.argv:
    sys.exit(1 if stale or any(fails.values()) else 0)
