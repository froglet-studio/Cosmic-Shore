"""Run the AI trainer in the real game, in a Unity with no window: the `unity-train` job's command (README section 16).

    python3 Tools/Compute/unity_run.py --sha <commit> --out <dir> --mode HexRace --vessel Squirrel --intensity 4 \
        --episodes 200 --minutes 55 [--time-scale 4] [--unity <path to the editor>] [--nographics]
    python3 Tools/Compute/unity_run.py --find          which Unity this computer would use, and why not

What it does, in order:
1. Finds the Unity editor of the project's exact version (ProjectSettings/ProjectVersion.txt at <sha>): config.json's
   "unity", FROGLET_COMPUTE_UNITY, then Unity Hub's install folders on Windows, macOS and Linux.
2. Keeps Froglet Compute's OWN copy of the project in ~/.froglet-compute/unity/Cosmic-Shore: a `git clone --shared`
   of the person's repository (its objects are borrowed, nothing is downloaded twice), checked out at <sha>. Its
   Library/ stays between runs, so only the first run imports the whole project. The person's checkout, its config
   and their open Unity are never touched.
   With --overlay-ref (the job passes this computer's install ref) the --overlay paths come from that ref instead, but
   only while the code ref has not touched them since the two parted: a trainer change not merged yet runs before
   the merge, and never replaces newer work on the code ref.
3. Copies Tools/Compute/unity/FrogletComputeTraining.cs into that copy (Assets/Editor/FrogletCompute/), and puts back
   this unit's trainer session (SessionState.asset, kept in <out>) so training resumes where it stopped.
4. Starts Unity: -batchmode -executeMethod ...FrogletComputeTraining.Run. Unity plays the match from Bootstrap with
   every seat on the trainer's AI, as the AI Training window's Learn button does, and writes <out>/status.json.
5. Stops it cleanly on SIGINT/SIGTERM/Ctrl-Break (the worker sends one when the person comes back): drops <out>/stop,
   waits up to 45 s, then kills. Saves the session back into <out>.

Exit codes: 0 done or stopped cleanly; 2 bad arguments; 3 the match never started; 4 no Unity of that version here;
5 the project copy could not be prepared; 6 the project does not compile; 75 the person's own Unity has the project
open (an automatic run waits; a manual run, FROGLET_COMPUTE_MANUAL=1, goes ahead). Standard library only.
"""
import argparse
import glob
import json
import os
import platform
import re
import shutil
import signal
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import compute_lib as L  # noqa: E402

BATCH_CS = os.path.join(HERE, "unity", "FrogletComputeTraining.cs")
ENTRY = "CosmicShore.Utility.AITraining.Editor.FrogletComputeTraining.Run"
STATE_ASSET = "Assets/_SO_Assets/AI Training/SessionState.asset"
INJECT_DIR = "Assets/Editor/FrogletCompute"
KEEP_ON_CLEAN = ("/Library/", "/Logs/", "/UserSettings/", "/Temp/", "/obj/")
COMPILE_ERROR = re.compile(r"error CS\d{4}|Scripts have compiler errors")


# ── which Unity ──────────────────────────────────────────────────────────────

def project_version(sha=None, cwd=None):
    text = L.git_ok("show", f"{sha}:ProjectSettings/ProjectVersion.txt", cwd=cwd) if sha else None
    if text is None:
        p = os.path.join(cwd or L.REPO, "ProjectSettings", "ProjectVersion.txt")
        text = open(p, encoding="utf-8").read() if os.path.exists(p) else ""
    m = re.search(r"m_EditorVersion:\s*(\S+)", text)
    return m.group(1) if m else None


def _hub_folders():
    """Unity Hub's default install folder and the one a person may have chosen (secondaryInstallPath.json)."""
    system, home = platform.system(), os.path.expanduser("~")
    if system == "Windows":
        roots = [os.path.join(os.environ.get(v, ""), "Unity", "Hub", "Editor") for v in ("ProgramFiles", "ProgramW6432")]
        cfg = os.path.join(os.environ.get("APPDATA", ""), "UnityHub", "secondaryInstallPath.json")
    elif system == "Darwin":
        roots = ["/Applications/Unity/Hub/Editor"]
        cfg = os.path.join(home, "Library", "Application Support", "UnityHub", "secondaryInstallPath.json")
    else:
        roots = [os.path.join(home, "Unity", "Hub", "Editor"), "/opt/unity/editors"]
        cfg = os.path.join(home, ".config", "UnityHub", "secondaryInstallPath.json")
    try:
        extra = json.load(open(cfg, encoding="utf-8"))
        if isinstance(extra, str) and extra:
            roots.insert(0, extra)
    except Exception:
        pass
    return [r for r in roots if r]


def _binary(folder):
    system = platform.system()
    if system == "Windows":
        return os.path.join(folder, "Editor", "Unity.exe")
    if system == "Darwin":
        return os.path.join(folder, "Unity.app", "Contents", "MacOS", "Unity")
    return os.path.join(folder, "Editor", "Unity")


def find_editor(version, explicit=None):
    """(path, None) or (None, why not)."""
    cfg = L.read_json(os.path.join(L.cache_dir(), "config.json"), {}) or {}
    for p in (explicit, os.environ.get("FROGLET_COMPUTE_UNITY"), cfg.get("unity")):
        if p:
            return (p, None) if os.path.exists(p) else (None, f"{p} does not exist")
    if not version:
        return None, "the project's Unity version is unknown (ProjectSettings/ProjectVersion.txt)"
    tried = []
    for root in _hub_folders():
        exe = _binary(os.path.join(root, version))
        tried.append(exe)
        if os.path.exists(exe):
            return exe, None
    return None, f"Unity {version} is not installed here (looked in {', '.join(tried) or 'no Hub folders'}); install " \
                 f"it with Unity Hub, or set \"unity\" in ~/.froglet-compute/config.json"


# ── Froglet Compute's own copy of the project ────────────────────────────────

def project_dir():
    return os.path.join(L.cache_dir(), "unity", "Cosmic-Shore")


def person_unity_open(repo=None):
    """True when the person's own Unity has their project open (Unity keeps Temp/UnityLockfile while it runs)."""
    return os.path.exists(os.path.join(repo or L.REPO, "Temp", "UnityLockfile"))


def overlay_plan(sha, overlay_sha, paths, cwd=None):
    """Whether to lay `paths` from `overlay_sha` (this computer's install ref) over the job's code at `sha`: only
    while the code ref has not touched those paths since the two parted, so newer work on the code ref is never
    replaced. Returns (apply, why)."""
    def tree(rev, path):
        return L.git_ok("rev-parse", f"{rev}:{path.rstrip('/')}", cwd=cwd)
    if not overlay_sha or overlay_sha == sha:
        return False, "no overlay ref"
    base = (L.git_ok("merge-base", sha, overlay_sha, cwd=cwd) or "").strip()
    if not base:
        return False, "the install ref shares no history with the code ref"
    same, behind = True, True
    for p in paths:
        here, there, then = tree(sha, p), tree(overlay_sha, p), tree(base, p)
        same = same and here == there
        behind = behind and here == then
    if same:
        return False, "the code ref already has these files"
    if not behind:
        return False, "the code ref changed these files since the install ref left it: its own version is kept"
    return True, "the install ref's version is newer"


def prepare_project(sha, repo=None, dest=None, quiet=False, overlay_sha=None, overlay=()):
    """The copy at `sha` with the batch entry in it. Its own clone (objects shared with the person's repository through
    git's alternates), so a checkout here never touches the person's working tree or config."""
    repo, dest = repo or L.REPO, dest or project_dir()
    if not os.path.isdir(os.path.join(dest, ".git")):
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        L.log(f"unity: making Froglet Compute's copy of the project at {dest} (the first run imports everything: an "
              "hour or more, once)", quiet)
        L.git("clone", "--shared", "--no-checkout", "--quiet", repo, dest, cwd=os.path.dirname(dest), timeout=3600)
        url = (L.git_ok("remote", "get-url", "origin", cwd=repo) or "").strip()
        if url:
            L.git("remote", "set-url", "origin", url, cwd=dest)
        common = (L.git_ok("rev-parse", "--path-format=absolute", "--git-common-dir", cwd=repo) or "").strip()
        if common and os.path.isdir(os.path.join(common, "lfs")):
            L.git("config", "lfs.storage", os.path.join(common, "lfs"), cwd=dest)   # LFS files: the person's copy
    L.git("checkout", "--quiet", "--force", "--detach", sha, cwd=dest, timeout=3600)
    clean = ["clean", "-ffdxq"] + [a for p in KEEP_ON_CLEAN for a in ("-e", p)]
    L.git(*clean, cwd=dest, timeout=1800)
    if overlay:
        apply, why = overlay_plan(sha, overlay_sha, overlay, cwd=repo)
        L.log(f"unity: overlay of {', '.join(overlay)}: {'applied' if apply else 'skipped'} ({why})", quiet)
        if apply:
            L.git("checkout", "--quiet", overlay_sha, "--", *overlay, cwd=dest, timeout=600)
    inject = os.path.join(dest, *INJECT_DIR.split("/"))
    os.makedirs(inject, exist_ok=True)
    shutil.copy2(BATCH_CS, os.path.join(inject, os.path.basename(BATCH_CS)))
    return dest


# ── one run ──────────────────────────────────────────────────────────────────

def unity_argv(unity, project, out, args, deadline):
    argv = [unity, "-batchmode", "-projectPath", project, "-executeMethod", ENTRY,
            "-logFile", os.path.join(out, "unity.log"), "-frogletTrain", out,
            "-trainMode", args.mode, "-trainIntensity", str(args.intensity), "-trainEpisodes", str(args.episodes),
            "-trainDeadline", str(int(deadline)), "-trainTimeScale", str(args.time_scale)]
    if args.vessel:
        argv += ["-trainVessel", args.vessel]
    if args.nographics:
        argv.append("-nographics")
    return argv


def _status(out, **kw):
    path = os.path.join(out, "status.json")
    st = L.read_json(path, {}) or {}
    st.update(kw, updated=time.time())
    L.write_json(path, st)
    return st


def _result(out, ok, reason, **kw):
    path = os.path.join(out, "result.json")
    res = L.read_json(path, {}) or {}
    if not res.get("schema"):
        res = {"schema": "froglet-compute/unity-train/1", "mode": kw.get("mode"), "vessel": kw.get("vessel")}
    res.update({"ok": ok, "reason": reason, "updated": time.time()})
    L.write_json(path, res)


def _tail(path, n=60):
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return fh.readlines()[-n:]
    except Exception:
        return []


def run(args):
    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    for f in ("stop",):
        try:
            os.remove(os.path.join(out, f))
        except FileNotFoundError:
            pass
    meta = {"mode": args.mode, "vessel": args.vessel}
    t0 = time.time()
    deadline = t0 + max(5, args.minutes) * 60 - 90          # Unity stops itself before the worker would stop it
    version = project_version(args.sha)
    unity, why = find_editor(version, args.unity)
    if not unity:
        _status(out, phase="no Unity here")
        _result(out, False, why, **meta)
        print(why)
        return 4
    if person_unity_open() and os.environ.get("FROGLET_COMPUTE_MANUAL") != "1":
        msg = "the person's own Unity has the project open: an automatic run waits (the trainer and this would race)"
        _status(out, phase="waiting: your Unity is open")
        print(msg)
        return 75
    _status(out, phase="preparing the project copy", unit_episodes=args.episodes)
    try:
        overlay_sha = L.rev(args.overlay_ref) if args.overlay_ref else None
        project = prepare_project(args.sha, quiet=args.quiet, overlay_sha=overlay_sha, overlay=args.overlay or ())
    except Exception as e:
        _result(out, False, f"could not prepare the project copy: {e}", **meta)
        print(e)
        return 5
    saved = os.path.join(out, "SessionState.asset")
    if os.path.exists(saved):                       # resume this unit's population where it stopped
        shutil.copy2(saved, os.path.join(project, *STATE_ASSET.split("/")))
    argv = unity_argv(unity, project, out, args, deadline)
    L.log("unity: " + " ".join(argv), args.quiet)
    _status(out, phase="opening Unity (importing on the first run)", unit_episodes=args.episodes)

    stop = {"asked": False}

    def on_signal(*_):
        stop["asked"] = True
    for s in ("SIGINT", "SIGTERM", "SIGBREAK"):
        if hasattr(signal, s):
            try:
                signal.signal(getattr(signal, s), on_signal)
            except Exception:
                pass

    kw = {}
    if os.name == "nt":
        # Its own process group (the worker's Ctrl-Break reaches only this wrapper, which asks Unity to stop cleanly),
        # idle priority, no window.
        kw["creationflags"] = 0x00000200 | 0x00000040 | 0x08000000
    else:
        kw["preexec_fn"] = lambda: os.nice(10)
    log_path = os.path.join(out, "unity.log")
    proc = subprocess.Popen(argv, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **kw)
    compile_failed = False
    stop_sent = None
    while proc.poll() is None:
        time.sleep(args.poll)
        if stop["asked"] and stop_sent is None:
            open(os.path.join(out, "stop"), "w").close()
            stop_sent = time.time()
        if stop_sent and time.time() - stop_sent > 45:          # the worker kills this wrapper at 60 s
            proc.kill()
            break
        if time.time() > deadline + 300 and stop_sent is None:        # Unity missed its own deadline
            open(os.path.join(out, "stop"), "w").close()
            stop_sent = time.time()
        st = L.read_json(os.path.join(out, "status.json"), {}) or {}
        if st.get("phase") in (None, "opening Unity (importing on the first run)") and \
                any(COMPILE_ERROR.search(line) for line in _tail(log_path, 400)):
            compile_failed = True
            proc.kill()
            break
    rc = proc.wait()
    state = os.path.join(project, *STATE_ASSET.split("/"))
    if os.path.exists(state):
        shutil.copy2(state, saved)
    tail = [line.rstrip("\n") for line in _tail(log_path, 80)]
    with open(os.path.join(out, "unity-tail.log"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(tail) + "\n")
    if compile_failed:
        errors = [line for line in tail if COMPILE_ERROR.search(line)][:10]
        _result(out, False, "the project does not compile in Unity: " + " | ".join(errors), **meta)
        _status(out, phase="the project does not compile")
        return 6
    res = L.read_json(os.path.join(out, "result.json"), {}) or {}
    if not res.get("schema"):
        _result(out, False, f"Unity exited ({rc}) before the trainer wrote a result; see unity-tail.log", **meta)
        return rc or 3
    _status(out, phase="done" if res.get("ok") else "stopped")
    L.log(f"unity: {res.get('reason')} - {res.get('episodes', 0)} episodes this run, best fitness "
          f"{res.get('best_fitness')}", args.quiet)
    return 0 if stop["asked"] or rc == 0 else rc


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--find", action="store_true", help="say which Unity this computer would use, and stop")
    ap.add_argument("--sha", help="the commit to train on (the job's ref)")
    ap.add_argument("--out", help="the unit's folder (status, result, genome, the saved session)")
    ap.add_argument("--mode", help="the trainer's scenario token (HexRace, Joust, AstroLeague, ...)")
    ap.add_argument("--vessel", default="", help="the hull, for modes that let it change")
    ap.add_argument("--intensity", type=int, default=4)
    ap.add_argument("--episodes", type=int, default=200, help="the unit's total episodes (matches)")
    ap.add_argument("--minutes", type=float, default=55, help="this session's time, Unity's start included")
    ap.add_argument("--time-scale", type=float, default=4.0, help="game clock multiplier (the trainer caps it at 8)")
    ap.add_argument("--unity", help="the Unity editor to use")
    ap.add_argument("--overlay-ref", default="", help="this computer's install ref: its version of --overlay paths is "
                    "laid over the code while the code ref has not touched them (a trainer change not merged yet)")
    ap.add_argument("--overlay", action="append", help="a path taken from --overlay-ref (repeatable)")
    ap.add_argument("--nographics", action="store_true", help="start Unity with -nographics")
    ap.add_argument("--poll", type=float, default=float(os.environ.get("FROGLET_COMPUTE_UNITY_POLL") or 5.0))
    ap.add_argument("--quiet", action="store_true")
    a = ap.parse_args(argv)
    if a.find:
        v = project_version(L.rev("origin/bleeding-edge") or None)
        exe, why = find_editor(v, a.unity)
        print(f"project Unity {v}: " + (exe or why))
        return 0 if exe else 4
    if not (a.sha and a.out and a.mode):
        ap.error("--sha, --out and --mode are needed")
    return run(a)


if __name__ == "__main__":
    sys.exit(main())
