#!/usr/bin/env python3
"""Read the logs every multiplayer play test sends by itself - no pasting.

Each player of a test (Unity's main editor and every MPPM virtual player; an Amoebius online game) writes
one `net_<utc>_<peer>.json` when it stops: its session record (role, counters, verdict, the lifecycle
timeline) and its console. Unity's after-play "Send to Claude" pushes them, with the errors, to the
sender's `bugledger/<user>` branch under `BugLedger/shared/reports/<user>/net/` (Docs/DIAGNOSTICS.md,
"The Bug Ledger"); Amoebius's after-run notification does the same. This script finds them on every
origin branch (git, no checkout), groups the players of one test, and lines them up in one timeline.

    git fetch origin
    python3 Tools/Build/net_logs.py collect                 # every test, newest first, one line per player
    python3 Tools/Build/net_logs.py show 1                  # the newest test: one merged timeline
    python3 Tools/Build/net_logs.py show yskha-20261015_101500 --lines   # ... with every console line
    python3 Tools/Build/net_logs.py collect --dir Logs/NetSessions       # a local folder instead of / beside branches
    python3 Tools/Build/net_logs.py --self-test

A test is the files of ONE sender whose play started within GROUP_SECONDS of the first one (MPPM players
start together; a separate test minutes later is a separate group).
"""
import argparse
import datetime as dt
import glob
import json
import os
import subprocess
import sys
import tempfile

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REPORTS_PREFIX = "BugLedger/shared/reports/"
GROUP_SECONDS = 120
SCHEMA = "cosmicshore.nettestlog.v1"


# ── sources ──────────────────────────────────────────────────────────────────

def _git(*args, repo=None):
    r = subprocess.run(["git", "-C", repo or REPO] + list(args), capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else None


def from_branches(repo=None):
    """Every net log on every origin branch, deduped by (user, file name); lists the branches it is on."""
    found = {}
    refs = (_git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin", repo=repo) or "").split()
    for ref in refs:
        if ref in ("origin", "origin/HEAD"):
            continue
        listing = _git("ls-tree", "-r", ref, "--", REPORTS_PREFIX, repo=repo)
        if not listing:
            continue
        for line in listing.splitlines():
            meta, _, path = line.partition("\t")
            parts = meta.split()
            rel = path[len(REPORTS_PREFIX):].split("/")
            if len(parts) < 3 or parts[1] != "blob" or len(rel) != 3 or rel[1] != "net":
                continue
            user, _, name = rel
            key = (user, name)
            if key not in found:
                text = _git("cat-file", "blob", parts[2], repo=repo)
                log = parse(text, source=f"{ref}:{path}", user=user)
                if log is None:
                    continue
                log["branches"] = []
                found[key] = log
            found[key]["branches"].append(ref[len("origin/"):])
    return list(found.values())


def from_dirs(dirs):
    logs = []
    for d in dirs:
        for path in sorted(glob.glob(os.path.join(d, "**", "net_*.json"), recursive=True)):
            try:
                with open(path, encoding="utf-8") as f:
                    text = f.read()
            except OSError:
                continue
            log = parse(text, source=path, user=None)
            if log is not None:
                log["branches"] = []
                logs.append(log)
    return logs


def parse(text, source, user):
    """One net log, or None for anything that is not one (never fatal: a garbage file is skipped)."""
    if not text:
        return None
    try:
        doc = json.loads(text)
    except ValueError:
        return None
    if not isinstance(doc, dict) or doc.get("schema") != SCHEMA:
        return None
    doc["source"] = source
    doc["user"] = user or doc.get("user") or "unknown"
    doc["_start"] = _time(doc.get("playStartedUtc"))
    return doc if doc["_start"] is not None else None


def _time(iso):
    if not iso:
        return None
    s = iso.strip().replace("Z", "+00:00")
    if "." in s:   # .NET writes 7 fractional digits; Python takes 6
        head, _, frac = s.partition(".")
        digits = "".join(c for c in frac if c.isdigit())
        tz = frac[len(digits):]
        s = f"{head}.{digits[:6]}{tz}"
    try:
        t = dt.datetime.fromisoformat(s)
    except ValueError:
        return None
    return t if t.tzinfo else t.replace(tzinfo=dt.timezone.utc)


# ── grouping ─────────────────────────────────────────────────────────────────

def group(logs):
    """Tests, newest first: per sender, files whose play started within GROUP_SECONDS of the test's first."""
    by_user = {}
    for log in logs:
        by_user.setdefault((log["user"], log.get("engine", "unity")), []).append(log)
    tests = []
    for (user, engine), items in by_user.items():
        items.sort(key=lambda l: l["_start"])
        current = None
        for log in items:
            if current is None or (log["_start"] - current["start"]).total_seconds() > GROUP_SECONDS:
                current = {"user": user, "engine": engine, "start": log["_start"], "peers": []}
                tests.append(current)
            current["peers"].append(log)
    for t in tests:
        t["id"] = f"{t['user']}-{t['start']:%Y%m%d_%H%M%S}"
        t["peers"].sort(key=lambda l: (l.get("peer") != "main", l.get("peer") or ""))
    tests.sort(key=lambda t: t["start"], reverse=True)
    return tests


def peer_summary(log):
    rec = log.get("record") or {}
    con = log.get("console") or {}
    verdict = rec.get("verdict") or {}
    counters = rec.get("counters") or {}
    clean = not (verdict.get("offlineFallbacksWhileOnline") or verdict.get("forceResets")
                 or verdict.get("budgetExhaustions") or verdict.get("rateLimited"))
    return (f"{log.get('peer', '?'):<10} {rec.get('role', '?'):<10} "
            f"{'CLEAN' if clean else 'DIRTY':<6} errors {con.get('errors', 0):<4} warnings {con.get('warnings', 0):<4} "
            f"reads {counters.get('lobbyReads', 0)} 429s {counters.get('rateLimited', 0)} "
            f"events {len(rec.get('lifecycle') or [])}  ({log.get('why', '')})")


def timeline(test, all_lines=False):
    """Every peer's lifecycle events and warnings/errors (or every line), in wall-clock order."""
    rows = []
    origin = test["start"]
    for log in test["peers"]:
        peer = log.get("peer", "?")
        rec = log.get("record") or {}
        rec_start = _time(rec.get("startedUtc")) or log["_start"]
        for ev in rec.get("lifecycle") or []:
            at = rec_start + dt.timedelta(seconds=float(ev.get("t", 0)))
            rows.append((at, peer, "event", f"{ev.get('e', '')} {ev.get('detail', '')}".strip()))
        for line in (log.get("console") or {}).get("lines") or []:
            kind = line.get("type", "log")
            if kind == "log" and not all_lines:
                continue
            at = log["_start"] + dt.timedelta(seconds=float(line.get("t", 0)))
            text = line.get("msg", "")
            if line.get("at"):
                text += f"   @ {line['at']}"
            rows.append((at, peer, kind, text))
    rows.sort(key=lambda r: r[0])
    return [(f"+{(at - origin).total_seconds():7.1f}s", peer, kind, text) for at, peer, kind, text in rows]


# ── commands ─────────────────────────────────────────────────────────────────

def load(args):
    logs = [] if args.no_branches else from_branches()
    logs += from_dirs(args.dir or [])
    return group(logs)


def cmd_collect(args):
    tests = load(args)
    if not tests:
        print("no multiplayer test logs yet (BugLedger/shared/reports/*/net/ on any origin branch"
              + (", or the given folders" if args.dir else "") + "). After a multiplayer play test, press "
              "Send to Claude when Unity asks; run `git fetch origin` first.")
        return 0
    for i, t in enumerate(tests, 1):
        branches = sorted({b for p in t["peers"] for b in p.get("branches", [])})
        commit = next((p.get("commit") for p in t["peers"] if p.get("commit")), None)
        print(f"[{i}] {t['id']}  {t['engine']}  {len(t['peers'])} player(s)"
              + (f"  on {commit}" if commit else "") + (f"  from {', '.join(branches)}" if branches else ""))
        for p in t["peers"]:
            print("    " + peer_summary(p))
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump([{"id": t["id"], "engine": t["engine"], "start": t["start"].isoformat(),
                        "peers": [{k: v for k, v in p.items() if not k.startswith("_")} for p in t["peers"]]}
                       for t in tests], f, indent=1, default=str)
    return 0


def cmd_show(args):
    tests = load(args)
    pick = None
    if args.test.isdigit() and 1 <= int(args.test) <= len(tests):
        pick = tests[int(args.test) - 1]
    else:
        pick = next((t for t in tests if t["id"] == args.test or t["id"].endswith(args.test)), None)
    if pick is None:
        print(f"no test '{args.test}' ({len(tests)} found; `collect` lists them)")
        return 1
    print(f"{pick['id']}  {pick['engine']}  started {pick['start']:%Y-%m-%d %H:%M:%S} UTC")
    for p in pick["peers"]:
        print("  " + peer_summary(p))
        print(f"    branch {p.get('branch')} @ {p.get('commit')}   source {p.get('source')}")
    print()
    for when, peer, kind, text in timeline(pick, all_lines=args.lines):
        print(f"{when}  {peer:<10} {kind:<9} {' / '.join(text.splitlines())}")
    return 0


# ── self-test ────────────────────────────────────────────────────────────────

def _fixture(peer, start, role, events, lines, why="test"):
    return {
        "schema": SCHEMA, "engine": "unity", "peer": peer, "isVirtualPlayer": peer != "main", "why": why,
        "user": "tester", "branch": "Ys-bleeding-edge", "commit": "abc1234",
        "playStartedUtc": start, "playEndedUtc": start,
        "record": {"schema": "cosmicshore.netsession.v1", "startedUtc": start, "role": role,
                   "counters": {"lobbyReads": 3, "rateLimited": 0}, "verdict": {},
                   "lifecycle": [{"t": t, "e": e, "detail": d} for t, e, d in events]},
        "console": {"errors": sum(1 for l in lines if l[1] != "log" and l[1] != "warning"),
                    "warnings": sum(1 for l in lines if l[1] == "warning"), "dropped": 0,
                    "lines": [{"t": t, "type": k, "msg": m} for t, k, m in lines]},
    }


def self_test():
    ok = True

    def check(cond, what):
        nonlocal ok
        print(("ok    " if cond else "FAIL  ") + what)
        ok = ok and cond

    with tempfile.TemporaryDirectory() as tmp:
        files = {
            "net_20261015_100000_main.json": _fixture("main", "2026-10-15T10:00:00.1234567Z", "host",
                                                      [(1.0, "clientApproved", "1"), (9.0, "clientLeft", "1")],
                                                      [(0.5, "log", "boot"), (8.5, "error", "Rpc failed")]),
            "net_20261015_100002_p2.json": _fixture("p2", "2026-10-15T10:00:02Z", "client",
                                                    [(0.5, "signedIn", "")], [(6.0, "warning", "slow join")]),
            # Ten minutes later: a separate test (negative control for the grouping window).
            "net_20261015_101000_main.json": _fixture("main", "2026-10-15T10:10:00Z", "host", [], []),
            "net_garbage.json": None,
        }
        for name, doc in files.items():
            with open(os.path.join(tmp, name), "w", encoding="utf-8") as f:
                f.write("{not json" if doc is None else json.dumps(doc))
        logs = from_dirs([tmp])
        check(len(logs) == 3, "three net logs read, the garbage file skipped")
        tests = group(logs)
        check(len(tests) == 2, "two tests: the 10:10 run is not grouped with the 10:00 one")
        check([p["peer"] for p in tests[1]["peers"]] == ["main", "p2"], "the 10:00 test holds main then p2")
        check(tests[0]["id"] == "tester-20261015_101000", "newest test first, id from user and start")
        rows = timeline(tests[1])
        order = [(r[1], r[3].split()[0]) for r in rows]
        check(order == [("main", "clientApproved"), ("p2", "signedIn"), ("p2", "slow"), ("main", "Rpc"), ("main", "clientLeft")],
              "one wall-clock timeline across players (main +1s, p2 +2.5s, p2 +8s, main +8.5s, main +9s)")
        check(not any(r[2] == "log" for r in rows), "plain log lines left out unless --lines")
        check(any(r[2] == "log" for r in timeline(tests[1], all_lines=True)), "--lines keeps them")
        check("CLEAN" in peer_summary(tests[1]["peers"][0]), "a record with no verdict findings reads CLEAN")
        dirty = _fixture("main", "2026-10-15T10:00:00Z", "host", [], [])
        dirty["record"]["verdict"] = {"rateLimited": 2}
        dirty["_start"] = _time(dirty["playStartedUtc"])
        check("DIRTY" in peer_summary(dirty), "a 429 in the verdict reads DIRTY (negative control)")

    # Branch reading, against a throwaway repository with an origin/bugledger branch.
    with tempfile.TemporaryDirectory() as repo:
        def git(*a):
            return subprocess.run(["git", "-C", repo, "-c", "user.name=t", "-c", "user.email=t@t"] + list(a),
                                  check=True, capture_output=True, text=True)
        git("init", "-q")
        target = os.path.join(repo, REPORTS_PREFIX, "tester", "net")
        os.makedirs(target)
        with open(os.path.join(target, "net_20261015_100000_main.json"), "w", encoding="utf-8") as f:
            json.dump(_fixture("main", "2026-10-15T10:00:00Z", "host", [(1.0, "clientApproved", "1")], []), f)
        perf = os.path.join(repo, REPORTS_PREFIX, "tester", "perf")
        os.makedirs(perf)
        with open(os.path.join(perf, "capture_x.json"), "w") as f:
            f.write("{}")
        git("add", "-A")
        git("commit", "-q", "-m", "send")
        git("update-ref", "refs/remotes/origin/bugledger/tester", "HEAD")
        got = from_branches(repo=repo)
        check(len(got) == 1 and got[0]["branches"] == ["bugledger/tester"],
              "a net log on origin/bugledger/tester is found and named by its branch; perf files are not")
        check(got and got[0]["user"] == "tester", "the sender comes from the report path")

    print("self-test: OK" if ok else "self-test: FAILED")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    for name in ("collect", "show"):
        p = sub.add_parser(name)
        p.add_argument("--dir", action="append", help="also read net_*.json under this folder (repeatable)")
        p.add_argument("--no-branches", action="store_true", help="only the --dir folders")
        if name == "collect":
            p.add_argument("--json", help="write every test, in full, to this file")
        else:
            p.add_argument("test", help="a test's number from `collect` (1 = newest) or its id")
            p.add_argument("--lines", action="store_true", help="every console line, not only warnings and errors")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.cmd == "collect":
        return cmd_collect(args)
    if args.cmd == "show":
        return cmd_show(args)
    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
