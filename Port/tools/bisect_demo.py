#!/usr/bin/env python3
"""prisma_bisect proof (ROADMAP C1b): plant a regression three commits back and find it.

Works in a throwaway clone, never in this checkout: it clones the repository (--shared, so no
object copy), commits this working tree's Port/ as the GOOD base, then lays down a history that
looks like real work -

    base  <- good
    1-3   engine and doc edits touching Port/
    4     breaks the build          (a candidate bisect has to SKIP)
    5     fixes it
    6     outside Port/ only        (must never be tested)
    7     an edit touching Port/
    8     THE PLANT                 (HEAD~3)
    9-11  edits touching Port/      <- bad = HEAD

and runs `prisma-mcp --repo <clone> --call prisma_bisect` on it. It passes when the tool names the
plant, in under 15 steps, without testing the commit outside Port/.

    python3 Port/tools/bisect_demo.py --check smoke     # plant: an error logged on every scene load
    python3 Port/tools/bisect_demo.py --check parity    # plant: Random.Range(int) off by one
"""
import argparse
import datetime
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = dict(os.environ, GIT_AUTHOR_NAME="bisect-demo", GIT_AUTHOR_EMAIL="demo@prisma",
           GIT_COMMITTER_NAME="bisect-demo", GIT_COMMITTER_EMAIL="demo@prisma", GIT_LFS_SKIP_SMUDGE="1")

SIGNATURE = "[bisect-plant]"
SCENE_MANAGER = "Port/src/CosmicShore.Engine/SceneGraph/SceneManager.cs"
RANDOM = "Port/src/CosmicShore.Engine/Math/Random.cs"
DEBUG = "Port/src/CosmicShore.Engine/Debug.cs"


def git(cwd, *args, check=True):
    r = subprocess.run(["git", *args], cwd=cwd, env=ENV, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and r.returncode != 0:
        sys.exit(f"git {' '.join(args)} failed:\n{r.stdout}{r.stderr}")
    return r.stdout.strip()


def edit(repo, path, fn):
    p = os.path.join(repo, path)
    with open(p, encoding="utf-8", newline="") as f:
        s = f.read()
    t = fn(s)
    assert t != s, f"edit of {path} changed nothing"
    with open(p, "w", encoding="utf-8", newline="") as f:
        f.write(t)


def append(repo, path, line):
    def fn(s):
        nl = "\r\n" if "\r\n" in s else "\n"
        return s + ("" if s.endswith(nl) else nl) + line + nl
    edit(repo, path, fn)


def commit(repo, message):
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "--no-verify", "-m", message)
    return git(repo, "rev-parse", "HEAD")


def plant_smoke(s):
    old = "static void Perform(string sceneName, LoadSceneMode mode)"
    i = s.index(old)
    j = s.index("{", i) + 1
    nl = "\r\n" if "\r\n" in s else "\n"
    return s[:j] + nl + f'            CosmicShore.Engine.Debug.LogError("{SIGNATURE} scene registry is stale loading " + sceneName);' + s[j:]


def plant_parity(s):
    old = "return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));"
    assert old in s, "Random.Range(int) changed shape; update the plant"
    return s.replace(old, "return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive + 1));")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", choices=["smoke", "parity"], default="smoke")
    ap.add_argument("--mcp", help="prisma-mcp.dll to run (default: build Port/src/CosmicShore.Mcp into a scratch folder, so a running server's lock does not matter)")
    ap.add_argument("--keep", action="store_true", help="keep the scratch clone")
    a = ap.parse_args()

    src = git(HERE, "rev-parse", "--show-toplevel")
    work = os.path.join(tempfile.gettempdir(), "froglet-mcp", "bisect-demo", datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    os.makedirs(work)
    clone = os.path.join(work, "repo")

    mcp = a.mcp
    if not mcp:
        out = os.path.join(work, "mcp")
        r = subprocess.run(["dotnet", "build", os.path.join(src, "Port", "src", "CosmicShore.Mcp"), "-nologo", "-v", "q", "-o", out],
                           capture_output=True, text=True)
        if r.returncode != 0:
            sys.exit("could not build prisma-mcp:\n" + r.stdout[-3000:])
        mcp = os.path.join(out, "prisma-mcp.dll")

    print(f"clone: {clone}")
    git(work, "clone", "-q", "--shared", "--no-checkout", src, clone)
    git(clone, "checkout", "-q", "--detach", git(src, "rev-parse", "HEAD"))
    git(clone, "config", "core.autocrlf", "false")

    # The base is this checkout's Port/ as it stands (committed or not): the engine under test.
    for rel in git(src, "ls-files", "-co", "--exclude-standard", "--", "Port").splitlines():
        s, d = os.path.join(src, rel), os.path.join(clone, rel)
        if os.path.isfile(s):
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copyfile(s, d)
        elif os.path.exists(d):
            os.remove(d)
    good = commit(clone, "demo: base - this checkout's Port/")

    append(clone, DEBUG, "// bisect-demo 1: logging note")
    commit(clone, "demo 1: engine comment")
    append(clone, "Port/docs/ROADMAP.md", "<!-- bisect-demo 2 -->")
    commit(clone, "demo 2: roadmap note")
    append(clone, RANDOM, "// bisect-demo 3: generator note")
    commit(clone, "demo 3: random comment")
    append(clone, DEBUG, "#error bisect-demo 4: a broken build")
    commit(clone, "demo 4: breaks the build")
    edit(clone, DEBUG, lambda s: re.sub(r"#error bisect-demo 4: a broken build\r?\n", "", s))
    commit(clone, "demo 5: fixes the build")
    with open(os.path.join(clone, "bisect-demo-outside-port.txt"), "w") as f:
        f.write("a commit that touches nothing under Port/\n")
    outside = commit(clone, "demo 6: outside Port/")
    append(clone, "Port/docs/ROADMAP.md", "<!-- bisect-demo 7 -->")
    commit(clone, "demo 7: roadmap note")
    if a.check == "smoke":
        edit(clone, SCENE_MANAGER, plant_smoke)
    else:
        edit(clone, RANDOM, plant_parity)
    plant = commit(clone, "demo 8: scene loading cleanup" if a.check == "smoke" else "demo 8: Range tidy")
    for i in (9, 10, 11):
        append(clone, "Port/docs/ROADMAP.md" if i != 10 else DEBUG, f"// bisect-demo {i}" if i == 10 else f"<!-- bisect-demo {i} -->")
        commit(clone, f"demo {i}: note")
    assert git(clone, "rev-parse", "HEAD~3") == plant
    print(f"good {good[:10]}  plant {plant[:10]} (HEAD~3)  outside-Port {outside[:10]}  bad HEAD")

    args = {"good": good, "bad": "HEAD", "check": a.check}
    if a.check == "smoke":
        args.update(signature=SIGNATURE, frames=300)
    else:
        args.update(case="random")
    cmd = ["dotnet", "exec", mcp, "--repo", clone, "--call", "prisma_bisect", json.dumps(args)]
    print("$ prisma-mcp --repo <clone> --call prisma_bisect " + json.dumps(args), flush=True)
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    print(r.stdout + r.stderr)

    ok = True
    m = re.search(r"^FOUND ([0-9a-f]{10}) in (\d+) step", r.stdout, re.M)
    if not m:
        print("FAIL: prisma_bisect found nothing"); ok = False
    else:
        steps = int(m.group(2))
        if not plant.startswith(m.group(1)):
            print(f"FAIL: found {m.group(1)}, the plant is {plant[:10]}"); ok = False
        if steps >= 15:
            print(f"FAIL: {steps} steps (must be under 15)"); ok = False
        if outside[:10] in r.stdout.split("steps:")[-1]:
            print("FAIL: the commit outside Port/ was tested"); ok = False
        if ok:
            print(f"PASS: prisma_bisect found the plant {plant[:10]} (HEAD~3) in {steps} step(s); the commit outside Port/ was never tested")
    leftovers = [l for l in git(clone, "worktree", "list").splitlines()[1:]] + [l for l in git(src, "worktree", "list").splitlines() if "bisect" in l]
    if leftovers:
        print("FAIL: scratch worktrees left behind:\n  " + "\n  ".join(leftovers)); ok = False
    if not a.keep:
        git(clone, "worktree", "prune", check=False)
        shutil.rmtree(work, ignore_errors=True)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
