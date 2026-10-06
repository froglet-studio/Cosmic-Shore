#!/usr/bin/env python3
"""Fetch the REAL reference material the Unity ref-compile needs into a cache under $TMPDIR.

Nothing fetched here is ever committed: it lands in $UNITY_REFCOMPILE_CACHE (default
${TMPDIR:-/tmp}/unity_refcompile_cache) and is reused on the next run. See README.md.

Sources (all public, fetched read-only, used only as compile references):
  * UnityEngine module reference DLLs + UnityEngine.UI + Unity.TextMeshPro for Unity 6000.0.75
    (closest Unity 6 build published on nuget.org: package Digitalroot.References.Unity, a
    "publicized" re-pack of the engine's managed DLLs - see README for what that does and does
    not prove). The project is on 6000.3.17f1; no 6000.3 package exists on nuget.org.
  * NETStandard.Library 2.0.3 facades (mscorlib/System/System.Core -> netstandard) so the net4x
    UnityEngine references unify with the netstandard2.1 API profile Unity compiles against.
  * Every registry package in Packages/packages-lock.json, as SOURCE, at its exact locked version,
    from the needle-mirror GitHub mirrors of the Unity package registry (packages.unity.com is
    not reachable from this sandbox).
  * The three git packages at their locked commits (UniTask, Reflex, ParrelSync).
  * SRP core / URP / ShaderGraph / VFX Graph from Unity-Technologies/Graphics at the 6000.3
    staging branch - 6000.0, matching the engine references (these are editor-bundled "builtin" packages; 17.3.x has no mirror tag).

Fails loudly (exit 2) when the network is unreachable and the cache is empty.
"""
import json
import os
import subprocess
import sys
import urllib.request
import zipfile
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
CACHE = os.environ.get("UNITY_REFCOMPILE_CACHE") or os.path.join(
    os.environ.get("TMPDIR", "/tmp"), "unity_refcompile_cache")

NUGET = [
    ("digitalroot.references.unity", "6000.0.75"),
    ("netstandard.library", "2.0.3"),
    # non-publicized Unity 2021.1 UnityEngine.dll/UnityEditor.dll: the accessibility oracle that
    # undoes the publicizing of the 6000.0 references (Depublicize/Program.cs)
    ("unity3d.sdk", "2021.1.14.1"),
]

# Packages whose content is pure editor tooling / native toolchains / samples: never part of a
# runtime compile, so not worth a clone. Everything else in the lock is fetched.
SKIP = {
    "com.unity.collab-proxy", "com.unity.ide.rider", "com.unity.ide.visualstudio",
    "com.unity.toolchain.win-x86_64-linux-x86_64", "com.unity.sysroot", "com.unity.sysroot.linux-x86_64",
    "com.unity.performance.profile-analyzer", "com.unity.mobile.android-logcat", "com.unity.ai.assistant",
    "com.unity.searcher", "com.unity.settings-manager", "com.unity.feature.mobile", "com.unity.2d.sprite",
    "com.unity.bindings.openimageio", "com.unity.recorder", "com.unity.device-simulator.devices",
    "com.unity.multiplayer.center", "com.unity.multiplayer.center.quickstart", "com.unity.pipeline",
"com.unity.services.deployment",
    "com.unity.services.deployment.api", "com.unity.test-framework.performance",
    "com.unity.test-framework", "com.unity.dt.app-ui", "com.unity.ai.inference",
    "com.unity.adaptiveperformance.samsung.android", "com.unity.adaptiveperformance",
    "com.unity.ugui",  # builtin 2.0.0 is not mirrored; its two assemblies come from the 6000.0.75 reference DLLs
}

# Builtin (editor-bundled) packages served from Unity-Technologies/Graphics instead of a mirror.
GRAPHICS_REPO = "https://github.com/Unity-Technologies/Graphics"
# 6000.0, not 6000.3: SRP 17.3 needs 6000.3-only engine API (variable-rate shading, ...) that the
# 6000.0.75 reference DLLs lack; 17.0.x is the SRP that matches the references we have.
GRAPHICS_REF = "6000.0/staging"
GRAPHICS_PACKAGES = ["com.unity.render-pipelines.core", "com.unity.render-pipelines.universal",
                     "com.unity.render-pipelines.universal-config",
                     "com.unity.shadergraph", "com.unity.visualeffectgraph"]

# Registry packages whose exact tag is not on needle-mirror: nearest mirrored tag (recorded in the
# manifest so the report can say what was substituted).
NEAREST_TAG = {
    "com.unity.burst": "1.6.0-pre.2",
}


def log(msg):
    print("[fetch] " + msg, flush=True)


def run(cmd, cwd=None):
    return subprocess.run(cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)


def fetch_nuget(pkg, ver):
    dest = os.path.join(CACHE, "nuget", pkg + "." + ver)
    if os.path.isdir(dest) and os.listdir(dest):
        return ("nuget " + pkg, ver, "cached")
    os.makedirs(dest, exist_ok=True)
    url = "https://api.nuget.org/v3-flatcontainer/%s/%s/%s.%s.nupkg" % (pkg, ver, pkg, ver)
    tmp = dest + ".nupkg"
    urllib.request.urlretrieve(url, tmp)
    with zipfile.ZipFile(tmp) as z:
        z.extractall(dest)
    os.remove(tmp)
    return ("nuget " + pkg, ver, "fetched")


KEEP = (".cs", ".asmdef", ".asmref", ".dll", ".dll.meta", ".asmdef.meta", ".asmref.meta", "package.json", ".rsp")


def prune(dest):
    """Keep only what a compile reads (sources, asmdefs, DLLs and their .meta): the disk is shared."""
    import shutil
    shutil.rmtree(os.path.join(dest, ".git"), ignore_errors=True)
    for dirpath, _, files in os.walk(dest):
        for f in files:
            if not f.endswith(KEEP):
                os.remove(os.path.join(dirpath, f))
    open(os.path.join(dest, ".fetched"), "w").close()


def git_clone_tag(url, tag, dest):
    if os.path.exists(os.path.join(dest, ".fetched")):
        return "cached"
    r = run(["git", "clone", "-q", "--depth", "1", "--branch", tag, url, dest])
    if r.returncode != 0:
        raise RuntimeError("clone %s@%s failed: %s" % (url, tag, r.stdout.strip()[-300:]))
    prune(dest)
    return "fetched"


def git_clone_commit(url, sha, dest):
    if os.path.exists(os.path.join(dest, ".fetched")):
        return "cached"
    os.makedirs(dest, exist_ok=True)
    for c in (["git", "init", "-q"], ["git", "remote", "add", "origin", url],
              ["git", "fetch", "-q", "--depth", "1", "origin", sha], ["git", "checkout", "-q", "FETCH_HEAD"]):
        r = run(c, cwd=dest)
        if r.returncode != 0:
            raise RuntimeError("%s failed for %s: %s" % (" ".join(c), url, r.stdout.strip()[-300:]))
    prune(dest)
    return "fetched"


def fetch_registry(name, ver):
    tag = NEAREST_TAG.get(name, ver)
    dest = os.path.join(CACHE, "packages", name + "@" + tag)
    try:
        st = git_clone_tag("https://github.com/needle-mirror/" + name, tag, dest)
        return (name, tag, st if tag == ver else st + " (SUBSTITUTE for locked " + ver + ")")
    except RuntimeError as e:
        return (name, ver, "UNAVAILABLE: " + str(e).splitlines()[0][:160])


def fetch_git(name, url, sha):
    base = url.split("?")[0]
    dest = os.path.join(CACHE, "git", name + "@" + sha[:12])
    try:
        return (name, sha[:12], git_clone_commit(base, sha, dest))
    except RuntimeError as e:
        return (name, sha[:12], "UNAVAILABLE: " + str(e)[:160])


def fetch_graphics():
    dest = os.path.join(CACHE, "graphics")
    if os.path.isdir(os.path.join(dest, ".git")):
        run(["git", "sparse-checkout", "set"] + ["Packages/" + p for p in GRAPHICS_PACKAGES], cwd=dest)
        sha = run(["git", "rev-parse", "HEAD"], cwd=dest).stdout.strip()
        return ("Unity-Technologies/Graphics", GRAPHICS_REF + " " + sha[:12], "cached")
    r = run(["git", "clone", "-q", "--depth", "1", "--filter=blob:none", "--sparse", "--branch", GRAPHICS_REF,
             GRAPHICS_REPO, dest])
    if r.returncode != 0:
        return ("Unity-Technologies/Graphics", GRAPHICS_REF, "UNAVAILABLE: " + r.stdout.strip()[-200:])
    run(["git", "sparse-checkout", "set"] + ["Packages/" + p for p in GRAPHICS_PACKAGES], cwd=dest)
    sha = run(["git", "rev-parse", "HEAD"], cwd=dest).stdout.strip()
    return ("Unity-Technologies/Graphics", GRAPHICS_REF + " " + sha[:12], "fetched")


def online():
    try:
        urllib.request.urlopen("https://api.nuget.org/v3/index.json", timeout=20).read(64)
        return True
    except Exception:
        return False


def main():
    os.makedirs(CACHE, exist_ok=True)
    manifest_path = os.path.join(CACHE, "manifest.json")
    lock = json.load(open(os.path.join(ROOT, "Packages", "packages-lock.json")))["dependencies"]
    if not online():
        if os.path.exists(manifest_path):
            log("OFFLINE: api.nuget.org unreachable - reusing the existing cache at " + CACHE)
            return 0
        log("ERROR: OFFLINE and no cache at %s. This tool needs api.nuget.org and github.com "
            "(read-only) on its first run; nothing can be verified without real references." % CACHE)
        return 2
    jobs = []
    with ThreadPoolExecutor(max_workers=12) as ex:
        for pkg, ver in NUGET:
            jobs.append(ex.submit(fetch_nuget, pkg, ver))
        for name, info in lock.items():
            if name in SKIP or name.startswith("com.unity.modules.") or name in GRAPHICS_PACKAGES:
                continue
            if info["source"] in ("registry", "builtin"):
                jobs.append(ex.submit(fetch_registry, name, info["version"]))
            elif info["source"] == "git":
                jobs.append(ex.submit(fetch_git, name, info["version"], info["hash"]))
        jobs.append(ex.submit(fetch_graphics))
        results = [j.result() for j in jobs]
    for name, ver, st in sorted(results):
        log("%-48s %-28s %s" % (name, ver, st))
    json.dump({"cache": CACHE, "results": results}, open(manifest_path, "w"), indent=1)
    return 0


if __name__ == "__main__":
    sys.exit(main())
