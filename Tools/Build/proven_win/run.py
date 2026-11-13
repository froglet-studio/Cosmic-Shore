#!/usr/bin/env python3
"""
Proven Win runner: runs EVERY measurement a candidate needs, in the right order, with the right controls, and leaves
the project as it found it. One command instead of a page of hand-made Unity launches.

    python Tools/Build/proven_win/run.py Docs/ProvenWin/candidates/<id> --project D:/ABLab/wt
    python Tools/Build/proven_win/run.py <candidate> --project <wt> --apis vulkan        # just one API (the decider first)
    python Tools/Build/proven_win/run.py <candidate> --project <wt> --dry-run            # print what it would run

It is slow (6 min of editor start per launch plus ~1.5 min per role), so start it in the background and wait for it.
It never re-runs a role whose report already exists: delete reports/<api>/<role>.json to measure it again.

What it does, per candidate.json (format: Docs/ProvenWin/candidates/README.md):
  shader kind: for each API (dx12, vulkan, d3d11) one batchmode launch of ShaderABBench with these roles:
      null        A vs A + a comment-only edit (made for you)     -> the instrument's noise; must read 1.00
      change      A vs B                                          -> the measurement
      neg         A vs C (a deliberately wrong B)                 -> proves the comparison can see an error
      probe_reach / probe_guard (when given)                      -> prove the bench reaches the guarded state
      state_<n>   A vs B in another game state (from "states")    -> proves no cost where the change does nothing
  scene kind: one editor Play-mode launch of the scene bench per role (change, neg), with a visible editor window.
Then it runs verdict.py and prints PASS / FAIL / HOLD.

Safety, enforced:
  - refuses the repository's MAIN worktree (the person's checkout) and any project another Unity has open;
  - after every launch, restores tracked files the editor rewrote (Gradle templates, ProjectSettings, fonts) and
    deletes the benches' temp folders, so `git status` shows only what you meant to change.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
API_FLAGS = {"dx12": [], "vulkan": ["-force-vulkan"], "d3d11": ["-force-d3d11"]}
SHADER_ROLES = ["null", "change", "neg", "probe_reach", "probe_guard"]
TEMP_DIRS = ["Assets/_ABBenchTemp", "Assets/_ABSceneBenchTemp"]


def log(cand_dir, msg):
    line = time.strftime("%H:%M:%S ") + msg
    print(line, flush=True)
    with open(os.path.join(cand_dir, "run.log"), "a", encoding="utf-8") as f:
        f.write(line + "\n")


def git(project, *args):
    return subprocess.run(["git", "-C", project, *args], capture_output=True, text=True).stdout


def unity_path(project):
    with open(os.path.join(project, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as f:
        ver = next(l.split(":", 1)[1].strip() for l in f if l.startswith("m_EditorVersion:"))
    for root in (r"C:\Unity", r"C:\Program Files\Unity\Hub\Editor"):
        p = os.path.join(root, ver, "Editor", "Unity.exe")
        if os.path.exists(p):
            return p
    sys.exit(f"no Unity {ver} found under C:\\Unity or the Hub; pass --unity")


def refuse_unsafe(project):
    top = os.path.normcase(os.path.abspath(git(project, "rev-parse", "--show-toplevel").strip()))
    first = git(project, "worktree", "list", "--porcelain").splitlines()
    main = os.path.normcase(os.path.abspath(first[0].split(" ", 1)[1])) if first else ""
    if top == main:
        sys.exit(f"refusing: {top} is the repository's main worktree (the person's checkout). "
                 "Make your own: git worktree add -b <branch> D:/ABLab/wt origin/bleeding-edge, and copy a warm Library into it.")
    ps = subprocess.run(["powershell", "-NoProfile", "-Command",
                         "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | % { $_.CommandLine }"],
                        capture_output=True, text=True).stdout
    norm = os.path.normcase(project).replace("\\", "/")
    for line in ps.splitlines():
        if norm in os.path.normcase(line).replace("\\", "/") and "AssetImportWorker" not in line:
            sys.exit(f"refusing: a Unity editor already has {project} open. Close it or wait for it.")


UNITY_WRITES = ("Assets/", "ProjectSettings/", "Packages/", "UserSettings/")


def restore(project, before):
    """Put back every tracked file the EDITOR rewrote, and drop the benches' temp folders. Only paths Unity writes
    (Assets/, ProjectSettings/, Packages/) are touched, so your own edits to docs, skills and tools during a run are
    safe; do not edit Assets/ while a run is going (it would be put back too)."""
    after = {l[3:] for l in git(project, "status", "--porcelain").splitlines() if l[:2] in (" M", "M ")}
    touched = sorted(p for p in after - before if p.strip('"').startswith(UNITY_WRITES))
    for path in touched:
        git(project, "checkout", "--", path.strip('"'))
    for d in TEMP_DIRS:
        full = os.path.join(project, d)
        if os.path.isdir(full):
            shutil.rmtree(full, ignore_errors=True)
        if os.path.exists(full + ".meta"):
            os.remove(full + ".meta")
    return touched


def null_variant(src, dst):
    text = open(src, encoding="utf-8").read()
    # A comment the compiler ignores; a .shadergraph is JSON, so it gets a trailing newline instead.
    text += "\n" if src.endswith(".shadergraph") else "\n// proven-win null control: comment-only edit\n"
    open(dst, "w", encoding="utf-8").write(text)


def shader_jobs(cand_dir, cand, api):
    """The (role, spec path) pairs still to measure on this API."""
    base = dict(cand["spec"])
    v = cand["variants"]
    rel = lambda p: os.path.abspath(os.path.join(cand_dir, p)).replace("\\", "/")
    os.makedirs(os.path.join(cand_dir, "specs"), exist_ok=True)
    gen = os.path.join(cand_dir, "variants_generated")
    os.makedirs(gen, exist_ok=True)
    a = rel(v["A"])
    ext = os.path.splitext(a)[1]
    null_b = os.path.join(gen, "Aprime" + ext).replace("\\", "/")
    null_variant(a, null_b)
    roles = {"null": null_b, "change": rel(v["B"]), "neg": rel(v["C"])}
    if "probe_reach" in v: roles["probe_reach"] = rel(v["probe_reach"])
    if "probe_guard" in v: roles["probe_guard"] = rel(v["probe_guard"])
    jobs = []
    for role, b in roles.items():
        jobs.append((role, b, {}))
    for name, st in cand.get("states", {}).items():
        jobs.append(("state_" + name, rel(v["B"]), st))
    out = []
    for role, b, override in jobs:
        if os.path.exists(os.path.join(cand_dir, "reports", api, role + ".json")):
            continue
        spec = dict(base)
        spec.update(override)
        spec["variantA"], spec["variantB"] = a, b
        spec["outDir"] = os.path.abspath(os.path.join(cand_dir, "out", api, role)).replace("\\", "/")
        if role in ("null", "neg", "probe_reach", "probe_guard"):
            spec["samples"] = min(spec.get("samples", 200), 60)   # controls need fewer pairs
        p = os.path.abspath(os.path.join(cand_dir, "specs", f"{api}_{role}.json"))
        json.dump(spec, open(p, "w", encoding="utf-8"), indent=1)
        out.append((role, p, spec["outDir"]))
    return out


def collect(cand_dir, api, role, out_dir):
    src = os.path.join(out_dir, "report.json")
    if not os.path.exists(src):
        return False
    dst = os.path.join(cand_dir, "reports", api)
    os.makedirs(dst, exist_ok=True)
    shutil.copy(src, os.path.join(dst, role + ".json"))
    if role == "change":
        frames = os.path.join(dst, "change_frames")
        os.makedirs(frames, exist_ok=True)
        for f in os.listdir(out_dir):
            if f.endswith(".png") and f[:1] in "AB":
                shutil.copy(os.path.join(out_dir, f), frames)
    return True


def run_shader(cand_dir, cand, args, unity):
    for api in args.apis:
        jobs = shader_jobs(cand_dir, cand, api)
        if not jobs:
            log(cand_dir, f"{api}: every role already measured")
            continue
        spec_list = ";".join(p for _, p, _ in jobs)
        cmd = [unity, "-batchmode", *API_FLAGS[api], "-projectPath", args.project, "-buildTarget", args.build_target,
               "-executeMethod", "CosmicShore.Editor.ProvenWin.ShaderABBench.Run", "-abSpec", spec_list,
               "-logFile", os.path.join(os.path.abspath(cand_dir), f"unity_{api}.log")]
        log(cand_dir, f"{api}: measuring {[r for r, _, _ in jobs]}")
        if args.dry_run:
            log(cand_dir, "  " + " ".join(cmd))
            continue
        before = {l[3:] for l in git(args.project, "status", "--porcelain").splitlines() if l[:2] in (" M", "M ")}
        refuse_unsafe(args.project)
        subprocess.run(cmd, timeout=4 * 3600)
        touched = restore(args.project, before)
        if touched:
            log(cand_dir, f"{api}: restored {len(touched)} file(s) the editor rewrote")
        for role, _, out_dir in jobs:
            ok = collect(cand_dir, api, role, out_dir)
            log(cand_dir, f"{api} {role}: {'ok' if ok else 'NO REPORT (read unity_' + api + '.log for [ShaderABBench] errors)'}")


def run_scene(cand_dir, cand, args, unity):
    sc = cand["scene"]
    for role in ("change", "neg"):
        if os.path.exists(os.path.join(cand_dir, "reports", "editor", role + ".json")):
            continue
        out = os.path.abspath(os.path.join(cand_dir, "out", "editor", role + ".json"))
        os.makedirs(os.path.dirname(out), exist_ok=True)
        flags = []
        for k, v in {**sc.get("common", {}), **sc.get(role, {})}.items():
            flags += [f"-{k}", str(v)]
        cmd = [unity, "-projectPath", args.project, "-buildTarget", args.build_target,
               "-executeMethod", "CosmicShore.Editor.ProvenWin.ProvenWinSceneBenchBuilder.RunInEditor", *flags,
               "-provenwinOut", out, "-logFile", os.path.join(os.path.abspath(cand_dir), f"unity_scene_{role}.log")]
        log(cand_dir, f"scene {role}: " + ("would run " + " ".join(cmd) if args.dry_run else "running (a visible editor window opens and closes itself)"))
        if args.dry_run:
            continue
        before = {l[3:] for l in git(args.project, "status", "--porcelain").splitlines() if l[:2] in (" M", "M ")}
        refuse_unsafe(args.project)
        subprocess.run(cmd, timeout=3600)
        restore(args.project, before)
        dst = os.path.join(cand_dir, "reports", "editor")
        os.makedirs(dst, exist_ok=True)
        if os.path.exists(out):
            shutil.copy(out, os.path.join(dst, role + ".json"))
            log(cand_dir, f"scene {role}: ok")
        else:
            log(cand_dir, f"scene {role}: NO REPORT (read unity_scene_{role}.log)")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("candidate")
    ap.add_argument("--project", required=True, help="your OWN bench worktree, never the person's checkout")
    ap.add_argument("--apis", nargs="+", default=["vulkan", "dx12", "d3d11"], help="Vulkan first: it is the phone path and the usual decider")
    ap.add_argument("--build-target", default="Android", help="match the Library you copied, or Unity reimports everything")
    ap.add_argument("--unity")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    cand_dir = os.path.abspath(args.candidate)
    cand = json.load(open(os.path.join(cand_dir, "candidate.json"), encoding="utf-8"))
    unity = args.unity or unity_path(args.project)
    if cand.get("kind", "shader") == "scene":
        run_scene(cand_dir, cand, args, unity)
    else:
        run_shader(cand_dir, cand, args, unity)
    if args.dry_run:
        return 0
    return subprocess.run([sys.executable, os.path.join(HERE, "verdict.py"), cand_dir]).returncode


if __name__ == "__main__":
    sys.exit(main())
