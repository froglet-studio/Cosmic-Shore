#!/usr/bin/env python3
"""Device play-test logs: pull from a phone, store on the bugledger-android/<user> branch, summarise.

  python3 Tools/Perf/android_logs_report.py ingest [--user yskha] [--serial X]   # adb pull + commit + push
  python3 Tools/Perf/android_logs_report.py report [--user yskha] [--dir PATH]   # summary of every session

Bundles come from DeviceLogCapture (Assets/_Scripts/Utility/PerformanceBenchmark/DeviceLogCapture.cs), written on
the phone to <persistentDataPath>/CosmicShore DeviceLogs/{pending,sent}/<session>/ (session.json, logcat_app.txt,
logcat_crash.txt). `ingest` builds the commit in a throwaway index (like BugLedgerSender), so no checkout changes.
Docs: Docs/perf/DEVICE_LOGS.md.
"""
import argparse, json, os, re, subprocess, sys, tempfile, collections

BRANCH_FMT = "bugledger-android/{user}"
PKG_DEFAULT = "com.FrogletGames.CosmicShore"
BUCKETS = ["<16.7", "<25", "<33.4", "<50", "<66.7", "<100", "<200", ">=200"]
THERMAL = ["none", "light", "moderate", "severe", "critical", "emergency", "shutdown"]


def git(*a, env=None, inp=None):
    r = subprocess.run(["git", *a], capture_output=True, text=True, env=env, input=inp)
    if r.returncode: raise SystemExit(f"git {' '.join(a)} failed: {r.stderr.strip()}")
    return r.stdout.strip()


def ingest(args):
    branch = BRANCH_FMT.format(user=args.user)
    adb = ["adb"] + (["-s", args.serial] if args.serial else [])
    tmp = tempfile.mkdtemp(prefix="devlogs_")
    remote = f"/sdcard/Android/data/{args.package}/files/CosmicShore DeviceLogs"
    r = subprocess.run(adb + ["pull", remote, tmp], capture_output=True, text=True)
    if r.returncode: raise SystemExit("adb pull failed (is the phone plugged in with USB debugging on?): " + r.stderr)
    root = os.path.join(tmp, "CosmicShore DeviceLogs")
    git("fetch", "-q", "origin", f"+refs/heads/{branch}:refs/remotes/origin/{branch}")
    parent = git("rev-parse", f"refs/remotes/origin/{branch}")
    env = dict(os.environ, GIT_INDEX_FILE=os.path.join(tmp, "index"),
               GIT_AUTHOR_NAME="Yash Sadhukhan", GIT_AUTHOR_EMAIL="30847550+YsKhan61@users.noreply.github.com",
               GIT_COMMITTER_NAME="Yash Sadhukhan", GIT_COMMITTER_EMAIL="30847550+YsKhan61@users.noreply.github.com")
    git("read-tree", parent, env=env)
    n = 0
    for state in ("pending", "sent"):
        d = os.path.join(root, state)
        if not os.path.isdir(d): continue
        for sess in sorted(os.listdir(d)):
            for f in os.listdir(os.path.join(d, sess)):
                blob = git("hash-object", "-w", os.path.join(d, sess, f))
                git("update-index", "--add", "--cacheinfo", f"100644,{blob},devicelogs/{args.user}/{sess}/{f}", env=env)
                n += 1
    tree = git("write-tree", env=env)
    if tree == git("rev-parse", parent + "^{tree}"):
        print("nothing new"); return
    commit = git("commit-tree", tree, "-p", parent, "-m", f"devicelogs: {n} file(s) from {args.user}'s phone (adb ingest)", env=env)
    git("push", "origin", f"{commit}:refs/heads/{branch}")
    print(f"pushed {commit[:9]} to {branch} ({n} files)")


def load_sessions(args):
    if args.dir:
        base = args.dir
    else:
        branch = BRANCH_FMT.format(user=args.user)
        git("fetch", "-q", "origin", f"+refs/heads/{branch}:refs/remotes/origin/{branch}")
        base = tempfile.mkdtemp(prefix="devlogs_rep_")
        arch = subprocess.run(["git", "archive", f"refs/remotes/origin/{branch}", "devicelogs"], capture_output=True)
        if arch.returncode: print("no devicelogs on the branch yet"); return []
        subprocess.run(["tar", "-x", "-C", base], input=arch.stdout, check=True)
    out = []
    for dp, _, files in os.walk(base):
        if "session.json" in files:
            try:
                s = json.load(open(os.path.join(dp, "session.json"), encoding="utf-8"))
            except Exception as e:
                print(f"skip {dp}: {e}", file=sys.stderr); continue
            s["_dir"] = dp
            out.append(s)
    return sorted(out, key=lambda s: s.get("session", ""))


def report(args):
    sessions = load_sessions(args)
    if not sessions: return
    print(f"# Device logs: {len(sessions)} session(s)\n")
    sig = collections.Counter(); sig_sessions = collections.defaultdict(set)
    scenes = collections.defaultdict(lambda: {"frames": 0, "ms": 0.0, "max": 0.0, "hitch": 0, "b": [0] * 8, "devices": set()})
    for s in sessions:
        dev = s.get("device", {})
        name = f"{dev.get('model')} / {dev.get('gpu')} / {dev.get('os')}"
        c = s.get("counts", {})
        crashed = s.get("previousSessionCrashed")
        print(f"## {s['session']}  {name}  q={dev.get('quality')} {dev.get('resolution')}@{dev.get('refreshHz')}  "
              f"err {c.get('errors',0)} exc {c.get('exceptions',0)} warn {c.get('warnings',0)}"
              + (f"  **previous session {s.get('previousSession')} ended without a clean pause/quit (crash/ANR/kill)**" if crashed else ""))
        for x in s.get("signatures", []):
            sig[x["sig"]] += x["count"]; sig_sessions[x["sig"]].add(s["session"])
        for sc in s.get("scenes", []):
            a = scenes[sc["name"]]; a["frames"] += sc["frames"]; a["ms"] += sc["avgMs"] * sc["frames"]
            a["max"] = max(a["max"], sc["maxMs"]); a["hitch"] += sc["hitches50ms"]; a["devices"].add(dev.get("model"))
            a["b"] = [p + q for p, q in zip(a["b"], sc["buckets"])]
        samples = s.get("samples", [])
        temps = [x["batteryTempC"] for x in samples if x.get("batteryTempC", -1) > 0]
        th = [x["thermalStatus"] for x in samples if x.get("thermalStatus", -1) >= 0]
        if samples:
            fps = [x["fpsAvg"] for x in samples if x.get("fpsAvg")]
            half = len(fps) // 2
            drop = (sum(fps[:half]) / half - sum(fps[half:]) / (len(fps) - half)) if half else 0
            print(f"   thermal: battery {min(temps, default=0):.1f}->{max(temps, default=0):.1f} C, worst status "
                  f"{THERMAL[max(th)] if th else 'n/a'}, fps first half vs second half drop {drop:+.1f}"
                  + ("  << likely throttling" if (th and max(th) >= 2) or drop > 8 else ""))
        for f in ("logcat_crash.txt", "logcat_app.txt"):
            p = os.path.join(s["_dir"], f)
            if os.path.exists(p):
                txt = open(p, encoding="utf-8", errors="replace").read()
                hits = re.findall(r"(FATAL EXCEPTION.*|Fatal signal.*|ANR in.*|SIGSEGV.*|Low on memory.*|lowmemorykiller.*)", txt)
                if hits: print(f"   {f}: " + "; ".join(sorted(set(h[:140] for h in hits))[:6]))
    print("\n# Top exceptions / errors (count, sessions, signature)")
    for k, v in sig.most_common(args.top):
        print(f"{v:6d}  {len(sig_sessions[k]):3d}  {k[:220]}")
    print("\n# Perf per scene")
    print(f"{'scene':28} {'frames':>8} {'avg ms':>7} {'fps':>6} {'max ms':>7} {'>50ms':>6}  histogram " + " ".join(BUCKETS))
    for n, a in sorted(scenes.items(), key=lambda kv: -kv[1]["ms"]):
        avg = a["ms"] / a["frames"] if a["frames"] else 0
        pct = " ".join(f"{100 * x / a['frames']:.0f}%" if a["frames"] else "0" for x in a["b"])
        print(f"{n[:28]:28} {a['frames']:8d} {avg:7.2f} {1000 / avg if avg else 0:6.1f} {a['max']:7.1f} {a['hitch']:6d}  {pct}")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("cmd", choices=["ingest", "report"])
    p.add_argument("--user", default="yskha"); p.add_argument("--serial"); p.add_argument("--package", default=PKG_DEFAULT)
    p.add_argument("--dir"); p.add_argument("--top", type=int, default=25)
    a = p.parse_args()
    ingest(a) if a.cmd == "ingest" else report(a)


if __name__ == "__main__":
    main()
