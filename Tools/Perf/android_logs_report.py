#!/usr/bin/env python3
"""Device play-test logs: read them from the bugledger-android/<user> branch, or pull them from a phone over USB.

  python3 Tools/Perf/android_logs_report.py report [--user yskha] [--new] [--dir PATH]   # summary of the sessions
  python3 Tools/Perf/android_logs_report.py handled SESSION... --fix SHA [--note TEXT]   # record what fixed them
  python3 Tools/Perf/android_logs_report.py ingest [--user yskha] [--serial X] [--keep]  # adb pull + commit + push, then delete from the phone

Bundles come from DeviceLogCapture (Assets/_Scripts/Utility/PerformanceBenchmark/DeviceLogCapture.cs): normally DeviceLogJob
commits one by itself after the game is swiped away; `ingest` is for a phone with no token. `handled` writes
devicelogs/<user>/handled.json on the data branch, and `report --new` then skips those sessions. Both writers build the commit
in a throwaway index (like BugLedgerSender), so no checkout changes. Docs: Docs/perf/DEVICE_LOGS.md; the loop: /phone-hunt.
"""
import argparse, collections, datetime, gzip, json, os, re, shutil, statistics, subprocess, sys, tempfile

BRANCH_FMT = "bugledger-android/{user}"
PKG_DEFAULT = "com.FrogletGames.CosmicShore"
BUCKETS = ["<16.7", "<25", "<33.4", "<50", "<66.7", "<100", "<200", ">=200"]
THERMAL = ["none", "light", "moderate", "severe", "critical", "emergency", "shutdown"]
FAULT = re.compile(r"(FATAL EXCEPTION.*|Fatal signal.*|ANR in.*|SIGSEGV.*|Low on memory.*|lowmemorykiller.*)")


def git(*a, env=None, inp=None):
    r = subprocess.run(["git", *a], capture_output=True, text=True, env=env, input=inp)
    if r.returncode: raise SystemExit(f"git {' '.join(a)} failed: {r.stderr.strip()}")
    return r.stdout.strip()


def commit_files(branch, message, files, tmp):
    """Commit {repo path: local file} onto the branch through a throwaway index, and push. No checkout changes.
    The branch's first commit has no parent, so a data branch never carries the game's tree."""
    has_branch = subprocess.run(["git", "ls-remote", "--exit-code", "--heads", "origin", branch], capture_output=True).returncode == 0
    env = dict(os.environ, GIT_INDEX_FILE=os.path.join(tmp, "index"))
    parent = None
    if has_branch:
        git("fetch", "-q", "origin", f"+refs/heads/{branch}:refs/remotes/origin/{branch}")
        parent = git("rev-parse", f"refs/remotes/origin/{branch}")
        git("read-tree", parent, env=env)
    else:
        git("read-tree", "--empty", env=env)
    for path, local in files.items():
        blob = git("hash-object", "-w", local)
        git("update-index", "--add", "--cacheinfo", f"100644,{blob},{path}", env=env)
    tree = git("write-tree", env=env)
    if parent and tree == git("rev-parse", parent + "^{tree}"):
        return None
    commit = git("commit-tree", tree, *(["-p", parent] if parent else []), "-m", message, env=env)
    git("push", "origin", f"{commit}:refs/heads/{branch}")
    return commit


def find_adb():
    """adb from PATH, else the SDK Unity's Android module ships (it is not on PATH by default)."""
    found = shutil.which("adb")
    if found: return found
    for hub in (r"C:\Program Files\Unity\Hub\Editor", "/Applications/Unity/Hub/Editor"):
        if not os.path.isdir(hub): continue
        for version in sorted(os.listdir(hub), reverse=True):
            for sdk in ("Editor/Data/PlaybackEngines/AndroidPlayer/SDK", "PlaybackEngines/AndroidPlayer/SDK"):
                exe = os.path.join(hub, version, sdk, "platform-tools", "adb.exe" if os.name == "nt" else "adb")
                if os.path.exists(exe): return exe
    raise SystemExit("adb not found: install Unity's Android module or put platform-tools on PATH")


def exit_records(dump, pid):
    """The ApplicationExitInfo blocks of `dumpsys activity exit-info` for one pid, or ''."""
    blocks = re.split(r"(?=ApplicationExitInfo #\d+:)", dump)
    return "\n".join(b.strip() for b in blocks if re.search(rf"\bpid={pid}\b", b))


def ingest(args):
    """Pull every finished session over USB, commit it to the data branch, and once origin has that commit, delete it from
    the phone. The session being played now (its pid file names a live process) is left alone."""
    branch = BRANCH_FMT.format(user=args.user)
    adb = [find_adb()] + (["-s", args.serial] if args.serial else [])
    tmp = tempfile.mkdtemp(prefix="devlogs_")
    data = f"/sdcard/Android/data/{args.package}/files"
    r = subprocess.run(adb + ["pull", f"{data}/CosmicShore DeviceLogs", tmp], capture_output=True, text=True)
    if r.returncode: raise SystemExit("adb pull failed (is the phone plugged in with USB debugging on?): " + r.stderr)
    live = subprocess.run(adb + ["shell", "pidof", args.package], capture_output=True, text=True).stdout.split()
    dump = subprocess.run(adb + ["shell", "dumpsys", "activity", "exit-info", args.package], capture_output=True, text=True).stdout
    root = os.path.join(tmp, "CosmicShore DeviceLogs")
    files, done, skipped = {}, [], []
    for state in ("pending", "sent"):
        d = os.path.join(root, state)
        if not os.path.isdir(d): continue
        for sess in sorted(os.listdir(d)):
            sd = os.path.join(d, sess)
            pid = open(os.path.join(sd, "pid")).read().strip() if os.path.exists(os.path.join(sd, "pid")) else ""
            if pid and pid in live:
                skipped.append(sess)
                continue
            dest = f"devicelogs/{args.user}/{sess}/"
            for f in os.listdir(sd):
                if f == "pid" or f.endswith(".tmp"): continue
                local = os.path.join(sd, f)
                if f == "unity_log.txt":
                    # No name or time in the header, so the same log makes the same blob and a re-run commits nothing.
                    with open(local, "rb") as src, open(local + ".gz", "wb") as raw, \
                            gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as gz:
                        shutil.copyfileobj(src, gz)
                    local, f = local + ".gz", f + ".gz"
                files[dest + f] = local
            if pid and not os.path.exists(os.path.join(sd, "exit.txt")):
                exit_path = os.path.join(sd, "exit.txt")
                with open(exit_path, "w", encoding="utf-8") as out:
                    out.write(exit_records(dump, pid) or f"Android kept no exit record for pid {pid}\n")
                files[dest + "exit.txt"] = exit_path
            done.append(f"{data}/CosmicShore DeviceLogs/{state}/{sess}")
    caps = os.path.join(tmp, "captures")
    if subprocess.run(adb + ["pull", f"{data}/CosmicShore Diagnostics", caps], capture_output=True).returncode == 0:
        cap_root = os.path.join(caps, "CosmicShore Diagnostics") if os.path.isdir(os.path.join(caps, "CosmicShore Diagnostics")) else caps
        for f in sorted(os.listdir(cap_root)):
            if re.fullmatch(r"(sent_)?capture_.*\.json", f):
                files[f"BugLedger/shared/reports/{args.user}-phone/perf/{f.removeprefix('sent_')}"] = os.path.join(cap_root, f)
                done.append(f"{data}/CosmicShore Diagnostics/{f}")
    if skipped: print(f"left on the phone, still running: {', '.join(skipped)}")
    if not files:
        print("nothing on the phone to send")
        return
    commit = commit_files(branch, f"devicelogs: {len(done)} item(s) from {args.user}'s phone (adb ingest)", files, tmp)
    print(f"pushed {commit[:9]} to {branch} ({len(files)} files)" if commit else "already on the branch")
    on_origin = git("ls-remote", "origin", f"refs/heads/{branch}").split()[0]
    if commit and on_origin != commit:
        raise SystemExit(f"origin's {branch} is {on_origin[:9]}, not {commit[:9]}: nothing deleted from the phone")
    if args.keep:
        print("--keep: nothing deleted from the phone")
        return
    for path in done:
        subprocess.run(adb + ["shell", "rm", "-rf", f"'{path}'"], capture_output=True)
    print(f"deleted {len(done)} item(s) from the phone; they are on {branch}")


def fetch_branch_dir(args):
    branch = BRANCH_FMT.format(user=args.user)
    git("fetch", "-q", "origin", f"+refs/heads/{branch}:refs/remotes/origin/{branch}")
    base = tempfile.mkdtemp(prefix="devlogs_rep_")
    arch = subprocess.run(["git", "archive", f"refs/remotes/origin/{branch}", "devicelogs"], capture_output=True)
    if arch.returncode:
        return None
    subprocess.run(["tar", "-x", "-C", base], input=arch.stdout, check=True)
    return base


def read_text(path):
    if path.endswith(".gz"):
        with gzip.open(path, "rt", encoding="utf-8", errors="replace") as f:
            return f.read()
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read()


def handled_map(base, user):
    p = os.path.join(base, "devicelogs", user, "handled.json")
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def load_sessions(base):
    out = []
    for dp, _, files in os.walk(base):
        if "session.json" not in files: continue
        try:
            s = json.load(open(os.path.join(dp, "session.json"), encoding="utf-8"))
        except Exception as e:
            print(f"skip {dp}: {e}", file=sys.stderr); continue
        s["_dir"] = dp
        s["marks_at"] = []
        if "samples.jsonl" in files:
            samples = []
            for line in open(os.path.join(dp, "samples.jsonl"), encoding="utf-8", errors="replace"):
                try:
                    row = json.loads(line)
                except ValueError:
                    continue  # the last line of a killed process can be cut short
                (s["marks_at"] if "mark" in row else samples).append(row)
            s["samples"] = samples
        out.append(s)
    return sorted(out, key=lambda s: s.get("session", ""))


def perf_lines(s):
    """What the 5 s samples say; captures taken by the auto-capture are left out of the fps, because the capture itself costs frames."""
    rows = [x for x in s.get("samples", []) if not x.get("capturing")]
    fps = [x["fps"] for x in rows if x.get("fps") is not None]
    if not fps: return
    th = [x["thermalStatus"] for x in rows if x.get("thermalStatus", -1) >= 0]
    rss = [x["rssMB"] for x in rows if x.get("rssMB", -1) > 0]
    cpu = [x["cpuMs"] for x in rows if x.get("cpuMs", 0) > 0]
    gpu = [x["gpuMs"] for x in rows if x.get("gpuMs", 0) > 0]
    third = max(1, len(fps) // 3)
    early, late = statistics.fmean(fps[:third]), statistics.fmean(fps[-third:])
    first_hot = next((x["t"] for x in rows if x.get("thermalStatus", -1) >= 2), None)
    print(f"   fps median {statistics.median(fps):.1f}, lowest 5 s window {min(fps):.1f}, first third {early:.1f}, last third {late:.1f}"
          + ("  << fell as it ran (throttling?)" if early - late > 8 else ""))
    if cpu and gpu:
        c, g = statistics.median(cpu), statistics.median(gpu)
        print(f"   cpu {c:.1f} ms, gpu {g:.1f} ms -> {'GPU' if g > c else 'CPU'}-bound (budget 16.7 ms)")
    if rss:
        print(f"   memory {rss[0]} -> {rss[-1]} MB, peak {max(rss)} MB" + ("  << grew 500+ MB" if rss[-1] - rss[0] > 500 else ""))
    if th:
        print(f"   thermal worst {THERMAL[max(th)]}" + (f", moderate or worse from {first_hot} s" if first_hot is not None else ""))
    for m in s.get("marks_at", []):
        near = min(rows, key=lambda x: abs(x["t"] - m["t"]), default=None)
        at = f"{near['fps']:.0f} fps, p99 {near['p99Ms']:.0f} ms" if near else "no sample near it"
        print(f"   MARK {m['mark']} at {m['t']} s in {m.get('scene')}: {at} {m.get('note', '')}")


def report(args):
    base = args.dir or fetch_branch_dir(args)
    if not base:
        print("no devicelogs on the branch yet"); return
    handled = handled_map(base, args.user) if not args.dir else {}
    sessions = load_sessions(base)
    if args.new:
        sessions = [s for s in sessions if s.get("session") not in handled]
    if not sessions:
        print("no new sessions" if args.new else "no sessions"); return
    print(f"# Device logs: {len(sessions)} session(s)\n")
    sig = collections.Counter(); sig_sessions = collections.defaultdict(set)
    scenes = collections.defaultdict(lambda: {"frames": 0, "ms": 0.0, "max": 0.0, "hitch": 0, "b": [0] * 8, "low": None})
    for s in sessions:
        dev = s.get("device", {})
        c = s.get("counts", {})
        fix = handled.get(s["session"])
        print(f"## {s['session']}  {dev.get('model')} / {dev.get('gpu')} / {dev.get('os')}  q={dev.get('quality')} {dev.get('resolution')}@{dev.get('refreshHz')}  "
              f"build {str(dev.get('buildGuid'))[:8]}  err {c.get('errors', 0)} exc {c.get('exceptions', 0)} warn {c.get('warnings', 0)}"
              + (f"  [handled: {fix.get('fix')}]" if fix else ""))
        for x in s.get("signatures", []):
            sig[x["sig"]] += x["count"]; sig_sessions[x["sig"]].add(s["session"])
        for sc in s.get("scenes", []):
            a = scenes[sc["name"]]; a["frames"] += sc["frames"]; a["ms"] += sc["avgMs"] * sc["frames"]
            a["max"] = max(a["max"], sc["maxMs"]); a["hitch"] += sc["hitches50ms"]
            a["b"] = [p + q for p, q in zip(a["b"], sc["buckets"])]
            low = sc.get("lowestWindowFps") or 0
            if low > 0: a["low"] = low if a["low"] is None else min(a["low"], low)
        perf_lines(s)
        exit_path = os.path.join(s["_dir"], "exit.txt")
        if os.path.exists(exit_path):
            m = re.search(r"reason=\d+ \(([^)]+)\)(?: subreason=\d+ \(([^)]+)\))?", read_text(exit_path))
            print("   ended: " + (" / ".join(x for x in m.groups() if x) if m else read_text(exit_path).strip()[:120]))
        for f in ("logcat_crash.txt", "logcat_crash.txt.gz", "logcat_app.txt", "logcat_app.txt.gz", "exit_trace.txt"):
            p = os.path.join(s["_dir"], f)
            if os.path.exists(p):
                hits = FAULT.findall(read_text(p))
                if hits: print(f"   {f}: " + "; ".join(sorted(set(h[:140] for h in hits))[:6]))
    print("\n# Top exceptions / errors (count, sessions, signature)")
    for k, v in sig.most_common(args.top):
        print(f"{v:6d}  {len(sig_sessions[k]):3d}  {k[:220]}")
    print("\n# Perf per scene")
    print(f"{'scene':28} {'frames':>8} {'avg ms':>7} {'fps':>6} {'low':>6} {'max ms':>7} {'>50ms':>6}  histogram " + " ".join(BUCKETS))
    for n, a in sorted(scenes.items(), key=lambda kv: -kv[1]["ms"]):
        avg = a["ms"] / a["frames"] if a["frames"] else 0
        pct = " ".join(f"{100 * x / a['frames']:.0f}%" if a["frames"] else "0" for x in a["b"])
        low = f"{a['low']:.1f}" if a["low"] else "-"
        print(f"{n[:28]:28} {a['frames']:8d} {avg:7.2f} {1000 / avg if avg else 0:6.1f} {low:>6} {a['max']:7.1f} {a['hitch']:6d}  {pct}")


def mark_handled(args):
    if not args.session or not args.fix:
        raise SystemExit("handled SESSION... --fix SHA")
    base = fetch_branch_dir(args)
    if not base:
        raise SystemExit("no devicelogs on the branch yet")
    handled = handled_map(base, args.user)
    now = datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds")
    for s in args.session:
        handled[s] = {"fix": args.fix, "note": args.note or "", "at": now}
    tmp = tempfile.mkdtemp(prefix="devlogs_")
    try:
        local = os.path.join(tmp, "handled.json")
        with open(local, "w", encoding="utf-8", newline="\n") as f:
            json.dump(handled, f, indent=1, sort_keys=True)
        commit = commit_files(BRANCH_FMT.format(user=args.user), f"devicelogs: {len(args.session)} session(s) handled by {args.fix}",
                              {f"devicelogs/{args.user}/handled.json": local}, tmp)
        print(f"pushed {commit[:9]}" if commit else "nothing new")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("cmd", choices=["ingest", "report", "handled"])
    p.add_argument("session", nargs="*")
    p.add_argument("--user", default="yskha"); p.add_argument("--serial"); p.add_argument("--package", default=PKG_DEFAULT)
    p.add_argument("--dir"); p.add_argument("--top", type=int, default=25); p.add_argument("--new", action="store_true")
    p.add_argument("--fix"); p.add_argument("--note"); p.add_argument("--keep", action="store_true")
    a = p.parse_args()
    {"ingest": ingest, "report": report, "handled": mark_handled}[a.cmd](a)


if __name__ == "__main__":
    main()
