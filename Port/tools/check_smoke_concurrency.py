#!/usr/bin/env python3
"""engine_smoke runs side by side must not share state: each reports its OWN scene.

Starts one `prisma-mcp --call engine_smoke` per scene, all at once, and checks that every run
  - reports the scene it was asked for (the first scene in its "scenes:" line),
  - names a report file no other run names, and that file's first scene is that scene too,
  - leaves no save profile behind (each run gets a fresh one and deletes it).

Before the fix, every run in the same second wrote `smoke-HHmmss.json` and all shared the profile
`smoke`, so a MinigameAstroLeague run could print "scenes: Bootstrap -> Authentication" with
Bootstrap's timings. Needs the player and prisma-mcp built (the smokes run with build:false):

    dotnet build Port/src/CosmicShore.Player && dotnet build Port/src/CosmicShore.Mcp
    python3 Port/tools/check_smoke_concurrency.py                      # Bootstrap + Menu_Main
    python3 Port/tools/check_smoke_concurrency.py --scenes Bootstrap,Menu_Main,MinigameAstroLeague,MinigameDogFight --frames 900
"""
import argparse
import glob
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))


def default_mcp(repo):
    exe = "prisma-mcp.exe" if os.name == "nt" else "prisma-mcp"
    return os.path.join(repo, "Port", "src", "CosmicShore.Mcp", "bin", "Debug", "net10.0", exe)


def profile_dirs():
    """Every smoke save profile on this machine (Application.persistentDataPath: LocalApplicationData/CosmicShore-<profile>)."""
    roots = [os.environ.get("LOCALAPPDATA"), os.environ.get("XDG_DATA_HOME"),
             os.path.expanduser("~/.local/share"), os.path.expanduser("~/Library/Application Support")]
    found = set()
    for root in filter(None, roots):
        found.update(glob.glob(os.path.join(root, "CosmicShore-smoke-*")))
    return found


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--repo", default=REPO, help="checkout whose built player the smokes run (default: this one)")
    ap.add_argument("--mcp", help="prisma-mcp executable under test (default: this checkout's Debug build)")
    ap.add_argument("--scenes", default="Bootstrap,Menu_Main", help="comma-separated scenes, one concurrent smoke each (at least two)")
    ap.add_argument("--frames", type=int, default=300)
    a = ap.parse_args()

    scenes = [s.strip() for s in a.scenes.split(",") if s.strip()]
    if len(scenes) < 2 or len({s.lower() for s in scenes}) != len(scenes):
        sys.exit("--scenes needs at least two distinct scenes")
    mcp = a.mcp or default_mcp(REPO)
    if not os.path.isfile(mcp):
        sys.exit(f"no prisma-mcp at {mcp}: dotnet build Port/src/CosmicShore.Mcp")

    profiles_before = profile_dirs()
    runs = {}
    for scene in scenes:
        call = json.dumps({"scene": scene, "frames": a.frames, "build": False})
        runs[scene] = subprocess.Popen([mcp, "--repo", a.repo, "--call", "engine_smoke", call],
                                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    outputs = {scene: p.communicate()[0] for scene, p in runs.items()}

    problems, reports = [], {}
    for scene, out in outputs.items():
        first = out.strip().splitlines()[0] if out.strip() else "(no output)"
        line = re.search(r"scenes: (.*?)(?: \(expected .*\))?$", out, re.M)
        rep = re.search(r"^report: (.+)$", out, re.M)
        if not line or not rep:
            problems.append(f"{scene}: no scenes/report line - {first}\n{out[-2000:]}")
            continue
        reported = [s.strip() for s in line.group(1).split("->")]
        path = rep.group(1).strip()
        reports.setdefault(path, []).append(scene)
        status = f"{scene}: {first.split()[0]}, scenes: {' -> '.join(reported)}, report {path}"
        print(status)
        if reported[0].lower() != scene.lower():
            problems.append(f"{scene}: the run reported scenes {reported} - another run's report")
        try:
            with open(path, encoding="utf-8") as f:
                on_disk = [s["name"] for s in json.load(f)["scenes"]]
            if not on_disk or on_disk[0].lower() != scene.lower():
                problems.append(f"{scene}: its report file {path} holds scenes {on_disk}")
        except (OSError, ValueError, KeyError) as e:
            problems.append(f"{scene}: cannot read its report {path}: {e}")

    for path, owners in reports.items():
        if len(owners) > 1:
            problems.append(f"runs {owners} all wrote one report file {path}")
    left = profile_dirs() - profiles_before
    if left:
        problems.append(f"save profiles left behind: {sorted(left)}")

    if problems:
        print("\nFAIL: concurrent engine_smoke runs shared state")
        for p in problems:
            print("  - " + p)
        return 1
    print(f"\nOK: {len(scenes)} concurrent smokes, each with its own scene, report and profile")
    return 0


if __name__ == "__main__":
    sys.exit(main())
