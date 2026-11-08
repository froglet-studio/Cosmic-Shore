#!/usr/bin/env python3
"""look.py - the look-sheet driver: run the real game headless, capture what the player sees, gate it, lay it out.

    python3 Tools/Build/unity_look/look.py menu HOME,HANGAR,PORT                  # the menu screens
    python3 Tools/Build/unity_look/look.py mode switchback --times 5,15,30        # a race, the AI on every seat
    python3 Tools/Build/unity_look/look.py menu HOME --print                      # only print the Unity command
    python3 Tools/Build/unity_look/look.py verify Logs/LookSheet                  # the gate on an existing run
    python3 Tools/Build/unity_look/look.py --self-test

It builds the editor command line for CosmicShore.Editor.LookSheetLauncher.Run (Assets/_Scripts/Editor/Look/),
runs it with the project's Unity (UNITY=/path/to/Unity, else /opt/unity/<ProjectVersion>/Editor/Unity, else
`unity-editor` on PATH as GameCI's image names it, else `Unity`), then runs verify.py (the blank-frame gate) and
sheet.py (the contact sheet) on the output folder. --print writes the command for another machine (the CI job in
.github/workflows/look-sheet.yml runs the same thing inside GameCI's image under Xvfb) and exits 0.

Never pass -nographics (nothing renders) and never -quit (Unity would exit before Play). On a machine with no GPU,
run under `xvfb-run -a -s "-screen 0 1920x1080x24"`: Mesa's software renderer draws a sheet in seconds a frame.

Exit codes: 0 every capture written and gated, 1 a capture failed the gate or Unity reported a failure, 2 a setup
error (no Unity, a sparse checkout), 3 Unity could not start the run.
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)

import sheet as sheet_mod  # noqa: E402
import verify as verify_mod  # noqa: E402

ENTRY = "CosmicShore.Editor.LookSheetLauncher.Run"


def project_version() -> str:
    path = os.path.join(ROOT, "ProjectSettings", "ProjectVersion.txt")
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                if line.startswith("m_EditorVersion:"):
                    return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return ""


def find_unity() -> str | None:
    env = os.environ.get("UNITY")
    if env and os.access(env, os.X_OK):
        return env
    ver = project_version()
    if ver:
        cand = f"/opt/unity/{ver}/Editor/Unity"
        if os.access(cand, os.X_OK):
            return cand
    for name in ("unity-editor", "Unity"):
        found = shutil.which(name)
        if found:
            return found
    return None


def command(unity: str, kind: str, what: str, out: str, times: str, intensity: int, size: str, phone: str,
            settle: float, quit_when_done: bool = True) -> list[str]:
    cmd = [unity, "-batchmode", "-projectPath", ROOT, "-executeMethod", ENTRY, "-lookOut", out,
           "-lookSize", size, "-lookPhone", phone, "-lookSettle", str(settle), "-logFile", "-"]
    if kind == "menu":
        cmd += ["-lookScreens", what]
    else:
        cmd += ["-lookMode", what, "-lookTimes", times, "-lookIntensity", str(intensity)]
    if quit_when_done:
        cmd.append("-lookQuit")
    return cmd


def run(args) -> int:
    out = os.path.abspath(args.out)
    unity = find_unity() or "Unity"
    cmd = command(unity, args.kind, args.what, out, args.times, args.intensity, args.size, args.phone, args.settle)
    if args.print:
        print(" ".join(f'"{c}"' if " " in c else c for c in cmd))
        return 0
    if find_unity() is None:
        print("look: no Unity editor found (set UNITY=/path/to/Unity, or run the CI job)", file=sys.stderr)
        return 2
    if not os.path.isdir(os.path.join(ROOT, "Assets", "_Scenes")):
        print("look: this checkout has no Assets/_Scenes (a sparse checkout cannot play)", file=sys.stderr)
        return 2
    os.makedirs(out, exist_ok=True)
    manifest = os.path.join(out, "manifest.jsonl")
    if os.path.exists(manifest) and not args.keep:
        os.remove(manifest)
    print("look: " + " ".join(cmd))
    code = subprocess.call(cmd, cwd=ROOT)
    print(f"look: Unity exited {code}")
    if code == 3:
        return 3
    results, ok = verify_mod.check([out])
    print(verify_mod.render(results))
    sheet_mod.build(out, os.path.join(out, "sheet.jpg"), columns=2, commit=git_head())
    print("LOOK SHEET: " + ("ALL OK" if ok and code == 0 else "FAILED"))
    return 0 if ok and code == 0 else 1


def git_head() -> str:
    try:
        return subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    except OSError:
        return ""


def self_test() -> int:
    cmd = command("/opt/unity/x/Editor/Unity", "menu", "HOME,HANGAR", "/tmp/out", "5,15", 1, "1920x1080", "1600x900", 1.0)
    assert "-nographics" not in cmd and "-quit" not in cmd, cmd
    assert cmd[cmd.index("-lookScreens") + 1] == "HOME,HANGAR" and "-lookQuit" in cmd, cmd
    cmd = command("Unity", "mode", "switchback", "/tmp/out", "5,15", 2, "1280x720", "800x450", 0.5)
    assert cmd[cmd.index("-lookMode") + 1] == "switchback" and cmd[cmd.index("-lookIntensity") + 1] == "2", cmd
    assert project_version().startswith("6000") or project_version() == "", project_version()
    rc = verify_mod.self_test()
    rc |= sheet_mod.self_test()
    print("look: self-test OK" if rc == 0 else "look: self-test FAILED")
    return rc


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("kind", nargs="?", choices=["menu", "mode", "verify"], help="menu <SCREENS> | mode <switchback|thresher-time> | verify <folder>")
    ap.add_argument("what", nargs="?", help="screens (HOME,HANGAR,...), a mode, or a folder for verify")
    ap.add_argument("--out", default=os.path.join("Logs", "LookSheet"))
    ap.add_argument("--times", default="5,15,30", help="game seconds after the turn starts (mode)")
    ap.add_argument("--intensity", type=int, default=1)
    ap.add_argument("--size", default="1920x1080")
    ap.add_argument("--phone", default="1600x900", help="the phone-scale capture (two times 800x450)")
    ap.add_argument("--settle", type=float, default=1.0)
    ap.add_argument("--keep", action="store_true", help="keep an existing manifest (append)")
    ap.add_argument("--print", action="store_true", help="print the Unity command and exit")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if not args.kind or not args.what:
        ap.print_usage()
        return 2
    if args.kind == "verify":
        results, ok = verify_mod.check([args.what])
        print(verify_mod.render(results))
        print("LOOK SHEET: " + ("ALL OK" if ok else "FAILED"))
        return 0 if ok else 1
    return run(args)


if __name__ == "__main__":
    sys.exit(main())
