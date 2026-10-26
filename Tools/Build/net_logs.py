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
start together; a separate test minutes later is a separate group), plus any other sender's file whose play
overlapped it in time (a test between two machines: each sends from its own bugledger branch). With more than
one sender, a player is named `<user>/<peer>`.

Read with care (both measured on Caleb's logs of 2026-10-16, both fixed in the writer since):
- A Unity file written before the writer reset the recorder at Play start can carry EARLIER Play sessions
  (the project enters Play without a domain reload). Such a file is marked "carried over": its events are cut
  to this Play's, but its counters and its `why` may belong to an earlier one.
- The refresh ticks' 429s were not counted in the record, so `429s` also shows the console's count of
  rate-limit warnings when they differ.
"""
import argparse
import datetime as dt
import glob
import json
import os
import re
import subprocess
import sys
import tempfile

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REPORTS_PREFIX = "BugLedger/shared/reports/"
GROUP_SECONDS = 120
CARRIED_SECONDS = 30        # a record that started this long before its Play did belongs to an earlier Play
SCHEMA = "cosmicshore.nettestlog.v1"
RATE_LIMIT = re.compile(r"rate[- ]limited", re.IGNORECASE)


# ── sources ──────────────────────────────────────────────────────────────────

def _git(*args, repo=None):
    r = subprocess.run(["git", "-C", repo or REPO] + list(args), capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else None


def _branch_reports(kind, reader, repo=None):
    """Every report of one kind (reports/<user>/<kind>/<file>) on every origin branch, read by reader(text, source,
    user), deduped by (user, file name); each lists the branches it is on."""
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
            if len(parts) < 3 or parts[1] != "blob" or len(rel) != 3 or rel[1] != kind:
                continue
            user, _, name = rel
            key = (user, name)
            if key not in found:
                text = _git("cat-file", "blob", parts[2], repo=repo)
                doc = reader(text, source=f"{ref}:{path}", user=user)
                if doc is None:
                    continue
                doc["branches"] = []
                found[key] = doc
            found[key]["branches"].append(ref[len("origin/"):])
    return list(found.values())


def from_branches(repo=None):
    """Every net log on every origin branch, deduped by (user, file name); lists the branches it is on."""
    return _branch_reports("net", parse, repo)


def loads_from_branches(repo=None):
    """Every Load Time Insights report the after-play send carried (kind "load")."""
    return _branch_reports("load", parse_load, repo)


# ── load timing reports (Load Time Insights, B36) ───────────────────────────

RELAY_RISK_MS = 9000        # Unity Relay drops a connection after ~10 s without traffic (B36)


def parse_load(text, source, user):
    """One LoadInsightReport JSON, or None. Its window: timestamp is when it finished; totalMs before that it began."""
    if not text:
        return None
    try:
        doc = json.loads(text)
    except ValueError:
        return None
    if not isinstance(doc, dict) or "trigger" not in doc or "totalMs" not in doc:
        return None
    end = _time(doc.get("timestamp"))
    if end is None:
        return None
    doc["source"] = source
    doc["user"] = user or "unknown"
    doc["_end"] = end
    doc["_start"] = end - dt.timedelta(milliseconds=float(doc.get("totalMs") or 0))
    return doc


def attach_loads(tests, loads):
    """Each load report joins the test whose player (same sender) was playing when it ran."""
    for t in tests:
        t.setdefault("loads", [])
    for load in loads:
        for t in tests:
            if any(p.get("user") == load["user"]
                   and p["_start"] - dt.timedelta(seconds=5) <= load["_start"] <= p["_end"] + dt.timedelta(seconds=30)
                   for p in t["peers"]):
                t["loads"].append(load)
                break
    for t in tests:
        t["loads"].sort(key=lambda l: l["_start"])


def load_line(load):
    """One line per load: who, which kind, how long, the worst frame (a Relay risk at ~10 s), how it ended."""
    worst = float(load.get("worstFrameMs") or 0)
    risk = "  RELAY RISK (a frame near Relay's 10 s timeout)" if worst >= RELAY_RISK_MS else ""
    slices = sorted(load.get("slices") or [], key=lambda s: -float(s.get("attributedMs") or 0))[:2]
    top = ", ".join(f"{s.get('name')} {float(s.get('attributedMs') or 0) / 1000:.1f}s" for s in slices)
    return (f"{load['user']:<8} {load.get('loadKind') or 'launch':<10} {float(load.get('totalMs') or 0) / 1000:6.1f}s  "
            f"worst frame {worst / 1000:5.2f}s  frames {load.get('framesDuringLoad', 0):<5} "
            f"'{load.get('trigger', '')}' -> {load.get('completionReason', '')}"
            + (f"  [{top}]" if top else "") + risk)


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
    if doc["_start"] is None:
        return None
    doc["_end"] = _time(doc.get("playEndedUtc")) or doc["_start"]
    _own_events(doc)
    return doc


def _own_events(log):
    """This Play's lifecycle events, each with its wall-clock time, in log["_events"]; log["_carried"] says
    whether the record also held earlier Plays'. The recorder's clock restarts with each Play while its
    first-mark time does not, so an earlier Play's events end where the times drop back."""
    rec = log.get("record") or {}
    events = rec.get("lifecycle") or []
    rec_start = _time(rec.get("startedUtc")) or log["_start"]
    carried = (log["_start"] - rec_start).total_seconds() > CARRIED_SECONDS
    log["_carried"] = carried
    if not carried:
        log["_events"] = [(rec_start + dt.timedelta(seconds=float(ev.get("t", 0))), ev) for ev in events]
        return
    cut = 0
    for i in range(1, len(events)):
        if float(events[i].get("t", 0)) < float(events[i - 1].get("t", 0)):
            cut = i
    if cut:
        log["_events"] = [(log["_start"] + dt.timedelta(seconds=float(ev.get("t", 0))), ev) for ev in events[cut:]]
    else:   # no clock restart seen: keep what falls inside this Play
        timed = [(rec_start + dt.timedelta(seconds=float(ev.get("t", 0))), ev) for ev in events]
        log["_events"] = [(at, ev) for at, ev in timed if at >= log["_start"]]


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

def _belongs(test, log):
    """Same sender: started within GROUP_SECONDS of the test (MPPM). Another sender: its Play overlapped one
    of the test's (two machines playing together)."""
    if test["engine"] != log.get("engine", "unity"):
        return False
    for p in test["peers"]:
        if p["user"] == log["user"]:
            if abs((log["_start"] - test["start"]).total_seconds()) <= GROUP_SECONDS:
                return True
        elif log["_start"] <= p["_end"] and p["_start"] <= log["_end"]:
            return True
    return False


def group(logs):
    """Tests, newest first (see the module doc for what makes one test)."""
    tests = []
    for log in sorted(logs, key=lambda l: l["_start"]):
        home = next((t for t in reversed(tests) if _belongs(t, log)), None)
        if home is None:
            home = {"engine": log.get("engine", "unity"), "start": log["_start"], "peers": []}
            tests.append(home)
        home["peers"].append(log)
    for t in tests:
        users = []
        for p in sorted(t["peers"], key=lambda l: l["_start"]):
            if p["user"] not in users:
                users.append(p["user"])
        t["user"] = users[0]
        t["users"] = users
        t["id"] = f"{'+'.join(users)}-{t['start']:%Y%m%d_%H%M%S}"
        t["peers"].sort(key=lambda l: (users.index(l["user"]), l.get("peer") != "main", l.get("peer") or ""))
    tests.sort(key=lambda t: t["start"], reverse=True)
    return tests


def peer_name(test, log):
    peer = log.get("peer", "?")
    return f"{log['user']}/{peer}" if len(test.get("users") or []) > 1 else peer


def _clean_at(at):
    """Unity's native trace once put its own stack walker here; that names no code, so it is left out."""
    if not at or "(Unity) " in at or "StackWalker" in at:
        return None
    return at


def console_rate_limits(log):
    return sum(1 for l in (log.get("console") or {}).get("lines") or []
               if l.get("type") == "warning" and RATE_LIMIT.search(l.get("msg", "")))


def longest_stall(events):
    """B36: the longest frame freeze (seconds) a player's record marked while online, or 0. A freeze of
    ~10 s loses the Relay connection ("player timed out due to inactivity")."""
    longest = 0.0
    for ev in events:
        ev = ev[1] if isinstance(ev, tuple) else ev
        if ev.get("e") == "stall":
            try:
                longest = max(longest, float((ev.get("detail") or "").split("s", 1)[0]))
            except ValueError:
                pass
    return longest


def peer_summary(log, name=None):
    rec = log.get("record") or {}
    con = log.get("console") or {}
    verdict = rec.get("verdict") or {}
    counters = rec.get("counters") or {}
    clean = not (verdict.get("offlineFallbacksWhileOnline") or verdict.get("forceResets")
                 or verdict.get("budgetExhaustions") or verdict.get("rateLimited"))
    counted, seen = counters.get("rateLimited", 0), console_rate_limits(log)
    limits = f"{counted}" + (f" (console {seen})" if seen != counted else "")
    events = log["_events"] if "_events" in log else rec.get("lifecycle") or []
    carried = "  [record carried over from an earlier Play: counters and why may be that one's]" if log.get("_carried") else ""
    freeze = longest_stall(events)
    stall = f" longest freeze {freeze:.1f}s" if freeze else ""
    # Block 5: reads the SDK's stale-index fault voided (counted since 2026-10-26), and roster syncs the
    # party session's push drove (0 in a party means the push never fired).
    voided = f" voided {counters['benign']}" if counters.get("benign") else ""
    pushes = f" pushes {counters['partyRosterPush']}" if counters.get("partyRosterPush") else ""
    return (f"{name or log.get('peer', '?'):<10} {rec.get('role', '?'):<10} "
            f"{'CLEAN' if clean else 'DIRTY':<6} errors {con.get('errors', 0):<4} warnings {con.get('warnings', 0):<4} "
            f"reads {counters.get('lobbyReads', 0)} 429s {limits}{voided}{pushes}{stall} "
            f"events {len(events)}  ({log.get('why', '')}){carried}")


def timeline(test, all_lines=False):
    """Every peer's lifecycle events and warnings/errors (or every line), in wall-clock order."""
    rows = []
    origin = test["start"]
    for log in test["peers"]:
        peer = peer_name(test, log)
        if "_events" not in log:
            _own_events(log)
        for at, ev in log["_events"]:
            rows.append((at, peer, "event", f"{ev.get('e', '')} {ev.get('detail', '')}".strip()))
        for line in (log.get("console") or {}).get("lines") or []:
            kind = line.get("type", "log")
            if kind == "log" and not all_lines:
                continue
            at = log["_start"] + dt.timedelta(seconds=float(line.get("t", 0)))
            text = line.get("msg", "")
            if _clean_at(line.get("at")):
                text += f"   @ {line['at']}"
            rows.append((at, peer, kind, text))
    rows.sort(key=lambda r: r[0])
    return [(f"+{(at - origin).total_seconds():7.1f}s", peer, kind, text) for at, peer, kind, text in rows]


# ── commands ─────────────────────────────────────────────────────────────────

def load(args):
    logs = [] if args.no_branches else from_branches()
    logs += from_dirs(args.dir or [])
    tests = group(logs)
    attach_loads(tests, [] if args.no_branches else loads_from_branches())
    return tests


def cmd_collect(args):
    tests = load(args)
    if not tests:
        print("no multiplayer test logs yet (BugLedger/shared/reports/*/net/ on any origin branch"
              + (", or the given folders" if args.dir else "") + "). A multiplayer play test sends its logs by itself "
              "when it stops; run `git fetch origin` first.")
        return 0
    for i, t in enumerate(tests, 1):
        branches = sorted({b for p in t["peers"] for b in p.get("branches", [])})
        commit = next((p.get("commit") for p in t["peers"] if p.get("commit")), None)
        print(f"[{i}] {t['id']}  {t['engine']}  {len(t['peers'])} player(s)"
              + (f"  on {commit}" if commit else "") + (f"  from {', '.join(branches)}" if branches else ""))
        for p in t["peers"]:
            print("    " + peer_summary(p, peer_name(t, p)))
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump([{"id": t["id"], "engine": t["engine"], "start": t["start"].isoformat(),
                        "peers": [{k: v for k, v in p.items() if not k.startswith("_")} for p in t["peers"]]}
                       for t in tests], f, indent=1, default=str)
    return 0


def config_diff(test):
    """B35: lines naming where the players' Netcode configs differ, from each one's last `netConfig`
    mark ("hash=<hex> prefabs=<n> <key>:<name> ..."). Empty when they agree or fewer than two recorded one."""
    configs = {}
    for log in test["peers"]:
        marks = [ev.get("detail") or "" for _, ev in log.get("_events", []) if ev.get("e") == "netConfig"]
        if marks:
            words = marks[-1].split()
            configs[peer_name(test, log)] = (words[0] if words else "", set(w for w in words[2:] if ":" in w))
    if len(configs) < 2 or len({h for h, _ in configs.values()}) == 1:
        return []
    out = ["NetworkConfig differs between players (B35):"]
    every = set.intersection(*(keys for _, keys in configs.values()))
    for name, (h, keys) in configs.items():
        only = sorted(keys - every)
        out.append(f"  {name}: {h}" + (f"  only here: {' '.join(only)}" if only else "  (same prefab keys: the difference is a setting)"))
    return out


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
        print("  " + peer_summary(p, peer_name(pick, p)))
        print(f"    branch {p.get('branch')} @ {p.get('commit')}   source {p.get('source')}")
    for line in config_diff(pick):
        print("  " + line)
    if pick.get("loads"):
        print("  Loads (Load Time Insights):")
        for l in pick["loads"]:
            print("    " + load_line(l))
    print()
    width = max([10] + [len(peer_name(pick, p)) for p in pick["peers"]])
    for when, peer, kind, text in timeline(pick, all_lines=args.lines):
        print(f"{when}  {peer:<{width}} {kind:<9} {' / '.join(text.splitlines())}")
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
        dirty = _fixture("main", "2026-10-15T10:00:00Z", "host", [], [(3.0, "warning", "Party session refresh rate-limited - backing off")])
        dirty["record"]["verdict"] = {"rateLimited": 2}
        dirty["record"]["counters"]["rateLimited"] = 2
        dirty["_start"] = _time(dirty["playStartedUtc"])
        check("DIRTY" in peer_summary(dirty), "a 429 in the verdict reads DIRTY (negative control)")
        check("429s 2 (console 1)" in peer_summary(dirty), "the console's rate-limit warnings show beside the record's count")
        check("console" not in peer_summary(tests[1]["peers"][0]), "...and not when they agree")
        pushed = _fixture("main", "2026-10-15T10:00:00Z", "host", [], [])
        pushed["record"]["counters"].update(benign=2, partyRosterPush=5)
        check("voided 2 pushes 5" in peer_summary(pushed), "voided reads and party pushes show on the summary line")
        check("voided" not in peer_summary(dirty) and "pushes" not in peer_summary(dirty),
              "...and nothing shows when both are 0 (negative control)")

    # Two machines: another sender whose Play overlapped is the same test; one that did not is not.
    def at(doc, end, user):
        doc["playEndedUtc"] = end
        doc["user"] = user
        return parse(json.dumps(doc), source="x", user=user)
    a = at(_fixture("main", "2026-10-16T20:05:24Z", "host", [(71.8, "clientApproved", "1")], []), "2026-10-16T20:52:01Z", "belac")
    b = at(_fixture("main", "2026-10-16T20:06:10Z", "client", [(0.5, "party", "InPresenceLobby->JoiningParty")], []), "2026-10-16T20:15:19Z", "yskha")
    c = at(_fixture("main", "2026-10-16T21:00:00Z", "host", [], []), "2026-10-16T21:05:00Z", "yskha")
    two = group([a, b, c])
    check(len(two) == 2 and two[1]["id"] == "belac+yskha-20261016_200524", "overlapping Plays on two machines are one test")
    check([peer_name(two[1], p) for p in two[1]["peers"]] == ["belac/main", "yskha/main"], "...whose players carry their sender")
    check(len(two[0]["peers"]) == 1 and peer_name(two[0], two[0]["peers"][0]) == "main",
          "a later Play that overlapped nothing stays its own test (negative control)")

    # B36: a frozen frame shows on the player's summary line.
    froze = parse(json.dumps(_fixture("main", "2026-10-20T19:58:24Z", "host",
                  [(415.0, "stall", "12.4s in MinigameBloomrush"), (418.0, "relayDrop", "after a 12.4 s freeze 2.9 s earlier")], [])), "x", "owner")
    check("longest freeze 12.4s" in peer_summary(froze), "a recorded freeze shows on the summary line")
    calm = parse(json.dumps(_fixture("main", "2026-10-20T19:58:24Z", "host", [(5.0, "party", "a->b")], [])), "x", "owner")
    check("freeze" not in peer_summary(calm), "...and nothing shows without one (negative control)")

    # B36: a player's load report joins their test, and a frame near Relay's timeout is flagged.
    lt = group([at(_fixture("main", "2026-10-20T19:58:24Z", "host", [], []), "2026-10-20T20:18:28Z", "owner")])
    menu = parse_load(json.dumps({"trigger": "Return to menu - MinigameBloomrush -> Menu_Main", "loadKind": "menuReturn",
                                  "totalMs": 14000, "worstFrameMs": 10400, "framesDuringLoad": 40,
                                  "completionReason": "Menu shown - the splash fades", "timestamp": "2026-10-20T20:05:10Z",
                                  "slices": [{"name": "Scene load", "attributedMs": 11000}]}), "x", "owner")
    other = parse_load(json.dumps({"trigger": "Game launch", "totalMs": 5000, "worstFrameMs": 200,
                                   "timestamp": "2026-10-20T20:05:10Z"}), "x", "belac")
    late = parse_load(json.dumps({"trigger": "Game launch", "totalMs": 5000, "worstFrameMs": 200,
                                  "timestamp": "2026-10-21T09:00:00Z"}), "x", "owner")
    attach_loads(lt, [menu, other, late])
    check([l["trigger"] for l in lt[0]["loads"]] == [menu["trigger"]], "a load joins its sender's test, inside its window")
    check("RELAY RISK" in load_line(menu) and "menuReturn" in load_line(menu), "a ~10 s frame is flagged as a Relay risk")
    check("RELAY RISK" not in load_line(other), "...and a 0.2 s frame is not (negative control)")
    check(parse_load(json.dumps({"schema": SCHEMA}), "x", "u") is None, "a net log is not a load report")

    # B35: two players whose Netcode configs differ - the report names the prefab key only one has.
    h = at(_fixture("main", "2026-10-23T23:02:05Z", "host", [(1.0, "netConfig", "hash=00000000000000aa prefabs=2 11:Goby 22:Player")], []), "2026-10-23T23:10:00Z", "belac")
    j = at(_fixture("main", "2026-10-23T23:02:30Z", "client", [(1.0, "netConfig", "hash=00000000000000bb prefabs=2 11:Goby 33:Player")], []), "2026-10-23T23:09:00Z", "gradi")
    diff = config_diff(group([h, j])[0])
    check(len(diff) == 3 and "22:Player" in diff[1] and "33:Player" in diff[2], "differing configs name the prefab key each player alone has")
    j2 = at(_fixture("main", "2026-10-23T23:02:30Z", "client", [(1.0, "netConfig", "hash=00000000000000aa prefabs=2 11:Goby 22:Player")], []), "2026-10-23T23:09:00Z", "gradi")
    check(config_diff(group([h, j2])[0]) == [], "matching configs print nothing (negative control)")

    # A record carried over from an earlier Play (no domain reload): cut to this Play's events.
    old = _fixture("main", "2026-10-16T21:56:40Z", "host",
                   [(0.0, "party", "x->y"), (71.8, "clientApproved", "1"), (2640.4, "readyGate", "old"),
                    (1.9, "party", "Disconnected->InPresenceLobby"), (29.3, "clientApproved", "0")], [])
    old["record"]["startedUtc"] = "2026-10-16T20:05:30Z"
    log = parse(json.dumps(old), source="x", user="belac")
    check(log["_carried"] and [ev["e"] for _, ev in log["_events"]] == ["party", "clientApproved"]
          and log["_events"][1][1]["detail"] == "0", "a carried-over record keeps only this Play's events")
    check(log["_events"][0][0] == _time("2026-10-16T21:56:41.9Z"), "...placed from this Play's start")
    check("carried over" in peer_summary(log), "...and its summary says so")
    fresh = parse(json.dumps(_fixture("main", "2026-10-16T20:05:24Z", "host", [(5.0, "party", "a->b")], [])), "x", "t")
    check(not fresh["_carried"] and len(fresh["_events"]) == 1, "a record from its own Play is kept whole (negative control)")
    check(_clean_at("0x00007ffe48c74e2e (Unity) StackWalker::ShowCallstack") is None
          and _clean_at("CosmicShore.Party.Foo:Bar ()") == "CosmicShore.Party.Foo:Bar ()", "the native stack walker is not a location")

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
    try:
        sys.exit(main())
    except BrokenPipeError:   # `show 1 | head`: the reader stopped, which is not an error
        sys.stderr.close()
        sys.exit(0)
