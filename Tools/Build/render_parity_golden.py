#!/usr/bin/env python3
"""Record the render-parity CONTROL TAKE in Unity from one command, on the machine that has the editor.

    python3 Tools/Build/render_parity_golden.py            # record Port/parity/goldens/render/control/
    python3 Tools/Build/render_parity_golden.py --push     # ... then commit take.mp4 + take.json and push this branch
    python3 Tools/Build/render_parity_golden.py --check    # only find the editor and the tool, record nothing

The dispatch half of the harness in Port/parity/README.md § Render parity. It finds the project's
Unity editor (ProjectSettings/ProjectVersion.txt, the Hub's default install folders; --unity PATH or
$UNITY_EDITOR override), runs it in batch mode WITH graphics on
CosmicShore.Editor.Parity.RenderParityRecorder.RecordBatch, which opens the control scene, enters
Play, drives the Unity Recorder through the take and exits the editor (0 when take.mp4 and take.json
are written). The project must not be open in another editor (Temp/UnityLockfile). After --push,
`python3 Port/tools/render_parity.py record && ... compare` on any machine scores Amoebius against it.
"""
import argparse
import glob
import os
import platform
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
METHOD = "CosmicShore.Editor.Parity.RenderParityRecorder.RecordBatch"
GOLDENS = os.path.join("Port", "parity", "goldens", "render")


def project_version():
    with open(os.path.join(ROOT, "ProjectSettings", "ProjectVersion.txt")) as f:
        for line in f:
            if line.startswith("m_EditorVersion:"):
                return line.split(":", 1)[1].strip()
    raise SystemExit("ProjectSettings/ProjectVersion.txt has no m_EditorVersion")


def find_unity(explicit):
    if explicit:
        return explicit
    env = os.environ.get("UNITY_EDITOR")
    if env:
        return env
    ver = project_version()
    system = platform.system()
    candidates = []
    if system == "Windows":
        for hub in (os.environ.get("ProgramFiles", r"C:\Program Files"), r"C:\Program Files", r"D:\Program Files"):
            candidates.append(os.path.join(hub, "Unity", "Hub", "Editor", ver, "Editor", "Unity.exe"))
        candidates += glob.glob(r"*:\Unity\Hub\Editor\%s\Editor\Unity.exe" % ver)
    elif system == "Darwin":
        candidates.append(f"/Applications/Unity/Hub/Editor/{ver}/Unity.app/Contents/MacOS/Unity")
    else:
        candidates.append(os.path.expanduser(f"~/Unity/Hub/Editor/{ver}/Editor/Unity"))
    for c in candidates:
        if os.path.exists(c):
            return c
    raise SystemExit(f"no Unity {ver} found; pass --unity PATH (looked at: " + ", ".join(candidates) + ")")


def tool_present():
    cs = os.path.join(ROOT, "Assets", "_Scripts", "Editor", "Parity", "RenderParityRecorder.cs")
    scene = os.path.join(ROOT, "Assets", "_Scenes", "Game_TestDesign", "RenderParity.unity")
    missing = [p for p in (cs, scene) if not os.path.exists(p)]
    if missing:
        raise SystemExit("not on the harness branch: missing " + ", ".join(os.path.relpath(m, ROOT) for m in missing))


def record(unity, take, timeout):
    lock = os.path.join(ROOT, "Temp", "UnityLockfile")
    if os.path.exists(lock):
        raise SystemExit("the project is open in an editor (Temp/UnityLockfile): close it, or run FrogletTools > Amoebius > "
                         "Render Parity: Record Control Take there instead")
    out = os.path.join(ROOT, GOLDENS, take)
    os.makedirs(out, exist_ok=True)
    log = os.path.join(out, "unity.log")
    cmd = [unity, "-batchmode", "-projectPath", ROOT, "-executeMethod", METHOD, "-parityTake", take, "-logFile", log]
    print("running:", " ".join(f'"{c}"' if " " in c else c for c in cmd), flush=True)
    t0 = time.time()
    proc = subprocess.Popen(cmd, cwd=ROOT)
    pos = 0
    while proc.poll() is None:
        time.sleep(5)
        if os.path.exists(log):
            with open(log, errors="replace") as f:
                f.seek(pos)
                for line in f:
                    if "[RenderParity]" in line or "error CS" in line or "Exception" in line:
                        print("  " + line.rstrip(), flush=True)
                pos = f.tell()
        if time.time() - t0 > timeout:
            proc.kill()
            raise SystemExit(f"timed out after {timeout} s; see {log}")
    mp4, js = os.path.join(out, "take.mp4"), os.path.join(out, "take.json")
    ok = proc.returncode == 0 and os.path.exists(mp4) and os.path.exists(js)
    frames = len(glob.glob(os.path.join(out, "frames", "*.png")))
    print(f"unity exited {proc.returncode} after {time.time() - t0:.0f} s; {frames} frames, "
          f"{'take.mp4 + take.json written' if ok else 'take NOT written'} -> {os.path.relpath(out, ROOT)}")
    if not ok:
        raise SystemExit(f"see {log}")
    stamp_git(js)
    return out


def stamp_git(js):
    """Fill take.json's "git" with the commit the take was recorded on, when the editor left it empty.

    RenderParityRecorder reads .git/HEAD itself, which is a FILE (gitdir: ...) in a worktree and
    misses a ref that lives only in packed-refs, so the first golden shipped with "git": "". git
    answers both; a dirty tree is marked so the take is never mistaken for that commit's exact state.
    """
    import json
    take = json.load(open(js))
    if take.get("git"):
        return
    try:
        head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        dirty = subprocess.run(["git", "diff", "--quiet", "HEAD", "--", "Assets", "Packages", "ProjectSettings"], cwd=ROOT).returncode != 0
    except (OSError, subprocess.CalledProcessError):
        return
    take["git"] = head + ("+dirty" if dirty else "")
    text = open(js).read()
    # rewrite only the one field, so the editor's layout (one keyframe per line) stays as written
    import re
    text = re.sub(r'"git":\s*"[^"]*"', '"git": "' + take["git"] + '"', text, count=1)
    open(js, "w").write(text)
    print(f"take.json git = {take['git']}")


def push(out):
    rel = os.path.relpath(out, ROOT)
    branch = subprocess.check_output(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=ROOT, text=True).strip()
    subprocess.check_call(["git", "add", os.path.join(rel, "take.mp4"), os.path.join(rel, "take.json")], cwd=ROOT)
    msg = f"chore(parity): record the render-parity golden '{os.path.basename(out)}' in Unity\n\nAffects: Shombith's Amoebius (the control take the engine is scored against)\n"
    subprocess.check_call(["git", "commit", "-m", msg], cwd=ROOT)
    subprocess.check_call(["git", "push", "-u", "origin", branch], cwd=ROOT)
    print(f"pushed the golden on {branch}; next, on any machine: python3 Port/tools/render_parity.py record && python3 Port/tools/render_parity.py compare")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--take", default="control")
    ap.add_argument("--unity", help="the editor binary (default: the project's version in the Hub's folder, or $UNITY_EDITOR)")
    ap.add_argument("--timeout", type=int, default=1800, help="seconds before the editor is killed")
    ap.add_argument("--push", action="store_true", help="commit take.mp4 + take.json and push the current branch")
    ap.add_argument("--check", action="store_true", help="find the editor and the tool, record nothing")
    a = ap.parse_args(argv)
    tool_present()
    unity = find_unity(a.unity)
    print(f"unity {project_version()}: {unity}")
    if a.check:
        print(f"would run -executeMethod {METHOD} -parityTake {a.take}")
        return 0
    out = record(unity, a.take, a.timeout)
    if a.push:
        push(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
