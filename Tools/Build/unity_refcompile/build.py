#!/usr/bin/env python3
"""Compile the project's C# the way the Unity editor's player build would, against REAL references.

Mini re-implementation of Unity's script-compilation pipeline (enough of it to type-check):
  * discovers every .asmdef/.asmref in the fetched package sources and in Assets/;
  * evaluates includePlatforms/excludePlatforms, defineConstraints and versionDefines for the
    chosen configuration (player = StandaloneWindows64 / IL2CPP / NET Standard 2.1, the project's
    shipped target; see DEFINES below);
  * assigns loose Assets scripts to Assembly-CSharp-firstpass / Assembly-CSharp (Editor folders
    excluded in the player configuration);
  * resolves name and GUID references, auto-referenced asmdefs and precompiled managed DLLs
    (PluginImporter .meta: platform, isExplicitlyReferenced, defineConstraints);
  * runs Roslyn source generators labelled RoslynAnalyzer for the asmdef that owns them and every
    assembly that references it (Unity's rule) - this is how Entities' ISystem/IJobEntity code and
    Reflex's injectors are generated;
  * compiles every assembly with csc in dependency order: -langversion:9.0, netstandard 2.1, the
    project's scripting defines.

Assemblies that genuinely cannot be fetched are compiled from hand-written stubs in stubs/<Name>.cs
(each file states what it stands for). Output: per-assembly diagnostics, and a summary that marks
every error in a file changed since origin/bleeding-edge.
"""
import argparse
import glob
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CACHE = os.environ.get("UNITY_REFCOMPILE_CACHE") or os.path.join(
    os.environ.get("TMPDIR", "/tmp"), "unity_refcompile_cache")
DOTNET_ROOT = os.environ.get("DOTNET_ROOT") or "/usr/lib/dotnet"

UNITY_VERSION = (6000, 3, 17)          # ProjectSettings/ProjectVersion.txt - what Assets code is compiled as
REFS_UNITY_VERSION = (6000, 0, 75)     # the engine reference DLLs we actually have
# Package code is compiled AS IF the editor were REFS_UNITY_VERSION (its #if UNITY_6000_x / "Unity"
# versionDefines then select code paths whose engine API the references really contain), while
# Assets code gets the project's true version defines. Any 6000.0 -> 6000.3 engine API gap therefore
# surfaces only in Assets code, where the report flags it for a human verdict.

# --- scripting defines -------------------------------------------------------------------------
def unity_version_defines(V=UNITY_VERSION):
    d = ["UNITY_%d_%d_%d" % V, "UNITY_%d_%d" % V[:2], "UNITY_%d" % V[0]]
    for minor in range(3, 7):
        d.append("UNITY_5_%d_OR_NEWER" % minor)
    for year in range(2017, 2024):
        for minor in range(1, 5):
            if year == 2023 and minor > 3:
                continue
            d.append("UNITY_%d_%d_OR_NEWER" % (year, minor))
    for minor in range(0, V[1] + 1):
        d.append("UNITY_6000_%d_OR_NEWER" % minor)
    return d

COMMON = [
    "PLATFORM_ARCH_64", "UNITY_64", "ENABLE_AUDIO", "ENABLE_CACHING", "ENABLE_CLOTH", "ENABLE_MICROPHONE",
    "ENABLE_MULTIPLE_DISPLAYS", "ENABLE_PHYSICS", "ENABLE_TEXTURE_STREAMING", "ENABLE_VIRTUALTEXTURING",
    "ENABLE_LZMA", "ENABLE_UNITYEVENTS", "ENABLE_VR", "ENABLE_WEBCAM", "ENABLE_UNITYWEBREQUEST", "ENABLE_WWW",
    "ENABLE_CLOUD_SERVICES", "ENABLE_CLOUD_SERVICES_ADS", "ENABLE_CLOUD_SERVICES_USE_WEBREQUEST",
    "ENABLE_CLOUD_SERVICES_CRASH_REPORTING", "ENABLE_CLOUD_SERVICES_PURCHASING", "ENABLE_CLOUD_SERVICES_ANALYTICS",
    "ENABLE_CLOUD_SERVICES_BUILD", "ENABLE_EDITOR_GAME_SERVICES", "ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT",
    "ENABLE_CLOUD_LICENSE", "ENABLE_UNITY_CONSENT", "ENABLE_UNITY_CLOUD_IDENTIFIERS",
    "ENABLE_CUSTOM_RENDER_TEXTURE", "ENABLE_DIRECTOR", "ENABLE_LOCALIZATION", "ENABLE_SPRITES", "ENABLE_TERRAIN",
    "ENABLE_TILEMAP", "ENABLE_TIMELINE", "ENABLE_INPUT_SYSTEM", "TEXTCORE_1_0_OR_NEWER", "ENABLE_RUNTIME_GI",
    "ENABLE_MOVIES", "ENABLE_NETWORK", "ENABLE_NVIDIA", "ENABLE_AMD", "ENABLE_CRUNCH_TEXTURE_COMPRESSION",
    "ENABLE_OUT_OF_PROCESS_CRASH_HANDLER", "ENABLE_CLUSTER_SYNC", "ENABLE_CLUSTERINPUT", "ENABLE_EVENT_QUEUE",
    "ENABLE_AR", "ENABLE_SCRIPTING_GC_WBARRIERS", "ENABLE_UNITY_COLLECTIONS_CHECKS_FALLBACK_UNUSED",
    "ENABLE_BURST_AOT", "ENABLE_MARSHALLING_TESTS", "ENABLE_VIDEO", "ENABLE_ACCELERATOR_CLIENT_DEBUGGING",
    "NET_STANDARD_2_0", "NET_STANDARD", "NET_STANDARD_2_1", "NETSTANDARD", "NETSTANDARD2_1",
    "CSHARP_7_3_OR_NEWER", "CSHARP_7_OR_LATER",
    "PLATFORM_STANDALONE_WIN", "PLATFORM_STANDALONE", "UNITY_STANDALONE_WIN", "UNITY_STANDALONE",
    "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP", "GFXDEVICE_WAITFOREVENT_MESSAGEPUMP",
    "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER",
    # ProjectSettings.asset scriptingDefineSymbols, Standalone
    "DOTWEEN", "SENTIS_ANALYTICS_ENABLED", "APP_UI_EDITOR_ONLY",
]
CONFIGS = {
    # The shipped Steam player: IL2CPP (scriptingBackend Standalone: 1), release (no DEVELOPMENT_BUILD).
    "player": ["ENABLE_IL2CPP"],
    # A development player build: adds the dev-only defines (profiler, asserts, DEBUG/TRACE).
    "player-dev": ["ENABLE_IL2CPP", "DEVELOPMENT_BUILD", "ENABLE_PROFILER", "UNITY_ASSERTIONS", "DEBUG", "TRACE"],
    # APPROXIMATE editor compile of the project's runtime code: UNITY_EDITOR branches type-checked
    # against the newest non-publicized UnityEditor obtainable (2021.1 - see README), plus the
    # Editor-folder files changed since --changed-base (with NUnit). Packages stay player-compiled.
    "editor": ["ENABLE_MONO", "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_WIN", "ENABLE_PROFILER",
               "UNITY_ASSERTIONS", "DEBUG", "TRACE", "ENABLE_UNITY_COLLECTIONS_CHECKS", "UNITY_INCLUDE_TESTS"],
}
PLAYER_PLATFORM = "WindowsStandalone64"


# --- helpers -----------------------------------------------------------------------------------
def load_json(path):
    with open(path, encoding="utf-8-sig") as f:
        txt = f.read()
    try:
        return json.loads(txt)
    except ValueError:
        return json.loads(re.sub(r",(\s*[}\]])", r"\1", txt))


def meta_guid(path):
    try:
        with open(path + ".meta", encoding="utf-8", errors="replace") as f:
            m = re.search(r"^guid:\s*([0-9a-f]{32})", f.read(), re.M)
            return m.group(1) if m else None
    except OSError:
        return None


def is_managed(path):
    try:
        with open(path, "rb") as f:
            data = f.read(4096)
        if data[:2] != b"MZ":
            return False
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0":
            return False
        opt = pe + 24
        magic = struct.unpack_from("<H", data, opt)[0]
        dd = opt + (96 if magic == 0x10B else 112)
        rva, size = struct.unpack_from("<II", data, dd + 14 * 8)
        return rva != 0 and size != 0
    except Exception:
        return False


def parse_version(s):
    s = s.strip()
    m = re.match(r"(\d+)(?:\.(\d+))?(?:\.(\d+))?([a-z].*|-.*)?$", s)
    if not m:
        return None
    nums = tuple(int(x) if x else 0 for x in m.group(1, 2, 3))
    tail = m.group(4) or ""
    # pre-releases sort before the release
    return nums + ((0, tail) if tail.startswith("-") else (1, tail))


def version_matches(expr, ver):
    """Unity's versionDefines expression: '1.2', '[1.2,2.0)', '(,3.0]', '[1.2]'."""
    v = parse_version(ver)
    if v is None:
        return False
    expr = expr.strip()
    if not expr:
        return True
    if expr[0] not in "[(":
        lo = parse_version(expr)
        return lo is not None and v[:3] >= lo[:3] and v >= lo
    lo_inc, hi_inc = expr[0] == "[", expr[-1] == "]"
    body = expr[1:-1]
    if "," not in body:
        x = parse_version(body)
        return x is not None and v[:3] == x[:3]
    lo_s, hi_s = [p.strip() for p in body.split(",", 1)]
    if lo_s:
        lo = parse_version(lo_s)
        if lo is None or (v < lo if lo_inc else v <= lo):
            return False
    if hi_s:
        hi = parse_version(hi_s)
        if hi is None or (v > hi if hi_inc else v >= hi):
            return False
    return True


def constraint_ok(c, defines):
    for alt in c.split("||"):
        alt = alt.strip()
        if alt.startswith("!"):
            if alt[1:].strip() not in defines:
                return True
        elif alt in defines:
            return True
    return False


def plugin_meta(path):
    """(player_compatible, explicitly_referenced, define_constraints, is_analyzer)"""
    try:
        txt = open(path + ".meta", encoding="utf-8", errors="replace").read()
    except OSError:
        return True, False, [], False
    analyzer = bool(re.search(r"^labels:\s*\n(\s*-\s*\S+\s*\n)*?\s*-\s*RoslynAnalyzer", txt, re.M))
    explicit = bool(re.search(r"isExplicitlyReferenced:\s*1", txt))
    dc = []
    m = re.search(r"defineConstraints:\s*\n((?:\s*-\s*.+\n)+)", txt)
    if m:
        dc = [x.strip()[1:].strip() for x in m.group(1).splitlines() if x.strip().startswith("-")]
    blocks = {}
    for b in re.finditer(r"- first:\s*\n\s*([^:\n]+):\s*([^\n]*)\n\s*second:\s*\n\s*enabled:\s*(\d)((?:\n\s{6,}.*)*)", txt):
        blocks[(b.group(1).strip(), b.group(2).strip())] = (b.group(3) == "1", b.group(4))
    if not blocks:
        return True, explicit, dc, analyzer
    any_ = blocks.get(("Any", ""))
    win = None
    for (k, v), val in blocks.items():
        if k == "Standalone" and v == "Win64":
            win = val
    if any_ and any_[0]:
        excl = re.search(r"Exclude Win64:\s*1", any_[1] or "")
        if excl:
            return False, explicit, dc, analyzer
        if win is not None and not win[0] and "Exclude Win64" not in (any_[1] or ""):
            return True, explicit, dc, analyzer
        return True, explicit, dc, analyzer
    return bool(win and win[0]), explicit, dc, analyzer


class Asm:
    def __init__(self, name, root, origin):
        self.name = name
        self.root = root
        self.origin = origin            # "package:<pkg>", "assets", "stub", "predefined"
        self.files = []
        self.refs = []
        self.extra_dirs = []
        self.defines = []
        self.unsafe = False
        self.auto = True
        self.no_engine = False
        self.override = False
        self.precompiled = []
        self.analyzers = []
        self.guid = None
        self.ok_platform = True
        self.excluded_reason = None


# --- discovery ---------------------------------------------------------------------------------
def package_roots():
    lock = load_json(os.path.join(ROOT, "Packages", "packages-lock.json"))["dependencies"]
    versions = {k: v["version"] for k, v in lock.items()}
    roots = []
    for d in glob.glob(os.path.join(CACHE, "packages", "*@*")):
        roots.append((os.path.basename(d).split("@")[0], d))
    for name, info in lock.items():
        if info["source"] == "git":
            path = ""
            m = re.search(r"\?path=([^#]+)", info["version"])
            if m:
                path = m.group(1).strip("/")
            d = os.path.join(CACHE, "git", name + "@" + info["hash"][:12], path)
            if os.path.isdir(d):
                roots.append((name, d))
    for d in glob.glob(os.path.join(CACHE, "graphics", "Packages", "*")):
        roots.append((os.path.basename(d), d))
    # versionDefines may only see packages whose source we actually compile: a define for a package
    # we skipped (e.g. adaptiveperformance) would switch on code that references its absent types.
    present = {n for n, _ in roots} | {"com.unity.ugui"}   # ugui comes as reference DLLs, not source
    versions = {k: v for k, v in versions.items() if k in present or k.startswith("com.unity.modules.")}
    # git packages carry no numeric version; use their package.json for versionDefines
    for name, d in roots:
        pj = os.path.join(d, "package.json")
        if not parse_version(versions.get(name, "x")) and os.path.exists(pj):
            versions[name] = load_json(pj).get("version", "0.0.0")
    return roots, versions


def walk(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not d.endswith("~") and not d.startswith(".") and d != "cvs"]
        yield dirpath, filenames


def discover(defines, pkg_defines, versions, roots):
    asms = {}
    by_guid = {}
    owners = {}        # dir -> asm (asmdef/asmref folders)
    asmrefs = []
    dlls = []          # (path, owner_dir_root)
    all_sources = []   # (root_kind, path)

    def add_asmdef(path, origin):
        j = load_json(path)
        is_pkg = origin.startswith("package:")
        vers = dict(versions)
        vers["Unity"] = "%d.%d.%d" % (REFS_UNITY_VERSION if is_pkg else UNITY_VERSION)
        defs = pkg_defines if is_pkg else defines
        a = Asm(j["name"], os.path.dirname(path), origin)
        a.guid = meta_guid(path)
        a.refs = j.get("references", []) or []
        a.unsafe = bool(j.get("allowUnsafeCode"))
        a.auto = j.get("autoReferenced", True)
        a.no_engine = bool(j.get("noEngineReferences"))
        a.override = bool(j.get("overrideReferences"))
        a.precompiled = j.get("precompiledReferences", []) or []
        inc, exc = j.get("includePlatforms") or [], j.get("excludePlatforms") or []
        if inc and PLAYER_PLATFORM not in inc:
            a.excluded_reason = "platforms " + ",".join(inc)
        if PLAYER_PLATFORM in exc:
            a.excluded_reason = "excludes " + PLAYER_PLATFORM
        vdefs = []
        for vd in j.get("versionDefines", []) or []:
            ver = vers.get(vd.get("name"))
            if ver is None and vd.get("name", "").startswith("com.unity.modules."):
                ver = "1.0.0"
            if ver is not None and version_matches(vd.get("expression", ""), ver):
                vdefs.append(vd["define"])
        a.defines = vdefs
        for c in j.get("defineConstraints", []) or []:
            if not constraint_ok(c, set(defs) | set(vdefs)):
                a.excluded_reason = a.excluded_reason or ("defineConstraint " + c)
        if a.name in asms:
            # duplicate names: first one wins (Unity would error); keep the first, note it
            return
        asms[a.name] = a
        if a.guid:
            by_guid[a.guid] = a
        owners[a.root] = a

    for kind, root in [("package:" + n, r) for n, r in roots] + [("assets", os.path.join(ROOT, "Assets"))]:
        for dirpath, files in walk(root):
            for f in files:
                p = os.path.join(dirpath, f)
                if f.endswith(".asmdef"):
                    add_asmdef(p, kind)
                elif f.endswith(".asmref"):
                    asmrefs.append(p)
                elif f.endswith(".cs"):
                    all_sources.append((kind, root, p))
                elif f.lower().endswith(".dll"):
                    dlls.append((kind, p))
    for p in asmrefs:
        ref = load_json(p).get("reference", "")
        tgt = by_guid.get(ref[5:]) if ref.startswith("GUID:") else asms.get(ref)
        if tgt:
            owners[os.path.dirname(p)] = tgt
    return asms, by_guid, owners, all_sources, dlls


def owner_of(path, owners, stop):
    d = os.path.dirname(path)
    while True:
        if d in owners:
            return owners[d]
        if d == stop or len(d) <= len(stop) or d == "/":
            return None
        d = os.path.dirname(d)


# --- compile -----------------------------------------------------------------------------------
def csc_path():
    c = sorted(glob.glob(os.path.join(DOTNET_ROOT, "sdk", "*", "Roslyn", "bincore", "csc.dll")))
    if not c:
        sys.exit("csc.dll not found under %s/sdk (set DOTNET_ROOT)" % DOTNET_ROOT)
    return c[-1]


def base_refs():
    ns = sorted(glob.glob(os.path.join(DOTNET_ROOT, "packs", "NETStandard.Library.Ref", "*", "ref", "netstandard2.1",
                                       "netstandard.dll")))[-1]
    fac = os.path.join(CACHE, "nuget", "netstandard.library.2.0.3", "build", "netstandard2.0", "ref")
    refs = [ns] + [p for p in glob.glob(os.path.join(fac, "*.dll")) if os.path.basename(p) != "netstandard.dll"]
    return refs


# Unity 6 engine assemblies taken from the reference package; the rest of that package is
# superseded by source we compile (InputSystem, Timeline, AI.Navigation, ...).
ENGINE_PREFIX = "UnityEngine"
UGUI = {"UnityEngine.UI": "UnityEngine.UI-publicized.dll", "Unity.TextMeshPro": "Unity.TextMeshPro-publicized.dll"}
# In Unity 6 the builtin com.unity.ugui 2.0 is referenced as "Unity.ugui" (and by these GUIDs);
# it provides both UnityEngine.UI and TextMeshPro.
UGUI_ALIASES = ["Unity.ugui", "UnityEngine.UI", "Unity.TextMeshPro", "GUID:2bafac87e7f4b9b418d9448d219b01ab",
                "GUID:6055be8ebefd69e48b49212b09b47b2f", "GUID:6546d7765b4165b40850b3667f981c26"]


def depublicize():
    """Build (once, cached) the de-publicized copies of the Unity 6 reference DLLs - see
    Depublicize/Program.cs. Rebuilt whenever overrides.txt or the tool changes."""
    src = os.path.join(CACHE, "nuget", "digitalroot.references.unity.6000.0.75", "ref", "net48")
    oracle = os.path.join(CACHE, "nuget", "unity3d.sdk.2021.1.14.1", "lib", "UnityEngine.dll")
    cecil = os.path.join(CACHE, "packages", "com.unity.nuget.mono-cecil@1.11.6", "Mono.Cecil.dll")
    tool_src = os.path.join(HERE, "Depublicize", "Program.cs")
    ovr = os.path.join(HERE, "depublicize_overrides.txt")
    tools = os.path.join(CACHE, "tools")
    out = os.path.join(CACHE, "engine_refs")
    stage = os.path.join(CACHE, "engine_refs_in")
    fp = fingerprint([tool_src, ovr, cecil])
    stamp = os.path.join(out, ".stamp")
    if os.path.exists(stamp) and open(stamp).read() == fp:
        return out
    os.makedirs(tools, exist_ok=True)
    os.makedirs(stage, exist_ok=True)
    for p in glob.glob(os.path.join(src, "*.dll")):
        b = os.path.basename(p).replace("-publicized", "")
        if b.startswith("UnityEngine") or b == "Unity.TextMeshPro.dll":
            if not b.startswith("UnityEngine.SpatialTracking") and not b.startswith("UnityEngine.XR.Legacy"):
                shutil.copyfile(p, os.path.join(stage, b))
    ref = sorted(glob.glob(os.path.join(DOTNET_ROOT, "packs", "Microsoft.NETCore.App.Ref", "*", "ref", "net8.0")))[-1]
    rt = sorted(os.listdir(os.path.join(DOTNET_ROOT, "shared", "Microsoft.NETCore.App")))[-1]
    dll = os.path.join(tools, "Depublicize.dll")
    cmd = [os.path.join(DOTNET_ROOT, "dotnet"), csc_path(), "-nologo", "-noconfig", "-nostdlib", "-langversion:latest",
           "-r:" + cecil, "-out:" + dll, tool_src] + ["-r:" + x for x in glob.glob(os.path.join(ref, "*.dll"))]
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if r.returncode != 0:
        sys.exit("Depublicize tool failed to build:\n" + r.stdout)
    shutil.copyfile(cecil, os.path.join(tools, "Mono.Cecil.dll"))
    json.dump({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": rt}}},
              open(os.path.join(tools, "Depublicize.runtimeconfig.json"), "w"))
    if os.path.isdir(out):
        shutil.rmtree(out)
    r = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), dll, stage, out, oracle, ovr],
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    print(r.stdout.strip())
    if r.returncode != 0:
        sys.exit("Depublicize failed")
    open(stamp, "w").write(fp)
    return out


def tree_key():
    import hashlib
    return os.path.basename(ROOT) + "-" + hashlib.sha1(ROOT.encode()).hexdigest()[:8]


def apply_source_patches():
    """source_patches.json: minimal edits to FETCHED package sources (never to Assets) where the
    package head needs engine API newer than the reference DLLs. Idempotent; each entry says why."""
    path = os.path.join(HERE, "source_patches.json")
    if not os.path.exists(path):
        return
    for p in json.load(open(path)):
        f = os.path.join(CACHE, p["file"])
        if not os.path.exists(f):
            print("[build] WARNING: patch target missing: " + p["file"])
            continue
        txt = open(f, encoding="utf-8-sig").read()
        if p["old"] in txt:
            open(f, "w", encoding="utf-8").write(txt.replace(p["old"], p["new"]))


def diagnose_tool():
    """Build (cached) Diagnose/Program.cs - full method-body diagnostics through the Roslyn API."""
    tools = os.path.join(CACHE, "tools")
    src = os.path.join(HERE, "Diagnose", "Program.cs")
    dll = os.path.join(tools, "Diagnose.dll")
    stamp = dll + ".stamp"
    fp = fingerprint([src, csc_path()])
    if os.path.exists(stamp) and open(stamp).read() == fp:
        return dll
    bincore = os.path.dirname(csc_path())
    ref = sorted(glob.glob(os.path.join(DOTNET_ROOT, "packs", "Microsoft.NETCore.App.Ref", "*", "ref", "net8.0")))[-1]
    rt = sorted(os.listdir(os.path.join(DOTNET_ROOT, "shared", "Microsoft.NETCore.App")))[-1]
    os.makedirs(tools, exist_ok=True)
    for b in ("Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll"):
        shutil.copyfile(os.path.join(bincore, b), os.path.join(tools, b))
    cmd = [os.path.join(DOTNET_ROOT, "dotnet"), csc_path(), "-nologo", "-noconfig", "-nostdlib", "-langversion:latest",
           "-r:" + os.path.join(tools, "Microsoft.CodeAnalysis.dll"), "-r:" + os.path.join(tools, "Microsoft.CodeAnalysis.CSharp.dll"),
           "-out:" + dll, src] + ["-r:" + x for x in glob.glob(os.path.join(ref, "*.dll"))]
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if r.returncode != 0:
        sys.exit("Diagnose tool failed to build:\n" + r.stdout)
    json.dump({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": rt}}},
              open(os.path.join(tools, "Diagnose.runtimeconfig.json"), "w"))
    open(stamp, "w").write(fp)
    return dll


def editor_refs():
    """UnityEditor 2021.1 (non-publicized, nuget Unity3D.SDK) + NUnit (com.unity.ext.nunit)."""
    ed = os.path.join(CACHE, "nuget", "unity3d.sdk.2021.1.14.1", "lib", "UnityEditor.dll")
    nunit = glob.glob(os.path.join(CACHE, "packages", "com.unity.ext.nunit@*", "**", "nunit.framework.dll"), recursive=True)
    return [ed] + nunit[:1]


def engine_refs():
    d = depublicize()
    out = [p for p in sorted(glob.glob(os.path.join(d, "UnityEngine*.dll"))) if os.path.basename(p) != "UnityEngine.UI.dll"]
    return out, {"UnityEngine.UI": os.path.join(d, "UnityEngine.UI.dll"), "Unity.TextMeshPro": os.path.join(d, "Unity.TextMeshPro.dll")}


def fingerprint(items):
    h = hashlib.sha1()
    for it in items:
        h.update(it.encode())
        if os.path.isfile(it):
            st = os.stat(it)
            h.update(("%d:%d" % (st.st_size, int(st.st_mtime))).encode())
    return h.hexdigest()


# Packages needle-mirror does not carry come from the Unity registry (fetch.py) where that host is
# reachable, and are compiled like any other. Where it is not, such a package is ABSENT from the run:
# an error that names something it declares (or a cascade of one) is bucketed "unobtainable", never
# gated. What each one declares is snapshotted here from its real tarball at the locked version by
# `build.py --write-declarations` (run where they ARE fetched); see README step 7.
UNOBTAINABLE_SNAPSHOT = os.path.join(HERE, "unobtainable_declarations.tsv")
MISSING_CODES = {"CS0246", "CS0234", "CS0103", "CS1069", "CS0012", "CS0538"}
# Errors that can be cascades of an unresolved type rather than a name of their own: `out var x` from
# an unknown TryGetValue -> CS0165, `unknown.Count > 0` -> CS0019, a member of a List<Unknown> -> CS1061.
# Diagnose suffixes each with the named error types its expression involves.
CASCADE_CODES = {"CS0165", "CS0019", "CS1061"}
REFRESH_HINT = ("refresh it where packages.unity.com is reachable: run.sh (fetch), then "
                "build.py --write-declarations")
UNRESOLVED_RE = re.compile(r" \[unresolved types: ([^\]]*)\]$")

# Missing-type messages, by code: the name (and namespace / assembly) each one is about.
MISSING_SIMPLE = re.compile(r"The type or namespace name '([^']+)' could not be found|"
                            r"The name '([^']+)' does not exist in the current context|"
                            r"'([^']+)' in explicit interface declaration is not an interface")
MISSING_IN_NS = re.compile(r"The type (?:or namespace )?name '([^']+)' (?:does not exist in|could not be found in) the namespace '([^']+)'")
MISSING_ASM = re.compile(r"(?:reference to|forwarded to) assembly '([^',]+)")
USING_RE = re.compile(r"^\s*using\s+(static\s+)?(?:\w+\s*=\s*)?(?:global::)?([\w.]+)\s*;", re.M)
NAMESPACE_RE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)


def no_declarations():
    return {"asms": set(), "namespaces": set(), "types": {}}


def add_declaration(decl, f):
    """One Diagnose --declarations record, split on tabs: "N", ns | "T", ns, name."""
    if f[0] == "N":
        parts = f[1].split(".")
        decl["namespaces"].update(".".join(parts[:i]) for i in range(1, len(parts) + 1))
    elif f[0] == "T":
        decl["types"].setdefault(f[2], set()).add(f[1])


def merge_declarations(decls):
    out = no_declarations()
    for d in decls:
        out["asms"] |= d["asms"]
        out["namespaces"] |= d["namespaces"]
        for name, nss in d["types"].items():
            out["types"].setdefault(name, set()).update(nss)
    return out


def declared(rsp):
    """Diagnose --declarations for one assembly: its "N\t<ns>" and "T\t<ns>\t<public type>" lines."""
    r = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), diagnose_tool(), "--declarations", rsp],
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if r.returncode != 0:
        sys.exit("Diagnose --declarations failed for %s:\n%s" % (rsp, r.stdout))
    return r.stdout.splitlines()


def declarations(rsps):
    """What the assemblies compiled from these .rsp files declare, read from their own sources with
    their own defines (Diagnose --declarations): {"asms", "namespaces" (with every prefix), "types":
    {public top-level type name: {namespace}}}."""
    decl = no_declarations()
    for name, rsp in rsps.items():
        decl["asms"].add(name)
        for line in declared(rsp):
            add_declaration(decl, line.split("\t"))
    return decl


def load_snapshot(path=UNOBTAINABLE_SNAPSHOT):
    """unobtainable_declarations.tsv -> {package: {"version", "asms", "namespaces", "types"}}."""
    snap, cur = {}, None
    if not os.path.exists(path):
        return snap
    for line in open(path, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if not f[0] or f[0].startswith("#"):
            continue
        if f[0] == "P":
            cur = snap[f[1]] = dict(no_declarations(), version=f[2])
        elif f[0] == "A":
            cur["asms"].add(f[1])
        else:
            add_declaration(cur, f)
    return snap


def write_snapshot(packages, path=UNOBTAINABLE_SNAPSHOT):
    """packages: [(package, version, sha1, {assembly: rsp})] -> unobtainable_declarations.tsv."""
    lines = ["# What the packages needle-mirror does not carry declare, for a run that cannot fetch them from",
             "# packages.unity.com either: an Assets error is bucketed \"unobtainable\" only when it names one of",
             "# these (README step 7). Written by `build.py --write-declarations` from the real tarballs at their",
             "# locked versions, assemblies Assets code references only, public top-level types only. Do not edit.",
             "# P package version tarball-sha1 | A assembly | N namespace | T namespace type"]
    for pkg, ver, sha, asms in sorted(packages):
        lines.append("P\t%s\t%s\t%s" % (pkg, ver, sha))
        for asm in sorted(asms):
            lines.append("A\t" + asm)
            lines += sorted(set(declared(asms[asm])))
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def from_absent_assembly(code, msg, src, decl):
    """True when a missing-type error can stem from an assembly absent from this compile (decl: what
    it declares - a referenced package assembly that did not compile, or a package this run could not
    fetch). It must name one of them, or a namespace or top-level type they declare that this file can
    see: its namespace is `using`d, encloses the file, or is global. A misspelled local, member or type
    is anything else, and gates."""
    if code not in MISSING_CODES or not (decl["asms"] or decl["types"]):
        return False
    m = MISSING_ASM.search(msg)
    if m and m.group(1) in decl["asms"]:
        return True
    visible, static = {""}, []
    for st, ns in USING_RE.findall(src):
        if st:
            static.append(ns)
        else:
            visible.add(ns)
    for ns in NAMESPACE_RE.findall(src):
        parts = ns.split(".")
        visible.update(".".join(parts[:i]) for i in range(1, len(parts) + 1))

    def declares(ns, name):
        name = re.sub(r"<.*", "", name)
        names = {name, name + "Attribute"} | ({name[:-len("Attribute")]} if name.endswith("Attribute") else set())
        return any(ns in decl["types"].get(n, ()) for n in names)
    # `using static N.T;` of a type they declare: any simple name in this file may be one of its members
    if code in ("CS0103", "CS0246") and any(declares(*s.rpartition(".")[::2]) for s in static):
        return True
    m = MISSING_IN_NS.search(msg)
    if m:
        name, ns = m.group(1), m.group(2).replace("global::", "")
        return declares(ns, name) or (code == "CS0234" and ns + "." + re.sub(r"<.*", "", name) in decl["namespaces"])
    m = MISSING_SIMPLE.search(msg)
    if not m:
        return False
    name = re.sub(r"<.*", "", next(g for g in m.groups() if g)).replace("global::", "")
    if "." in name:
        ns, _, name = name.rpartition(".")
        return declares(ns, name)
    if any(declares(ns, name) for ns in visible):
        return True
    # a type-or-namespace name may be the next segment of a namespace only they declare
    return code == "CS0246" and any((v + "." + name).lstrip(".") in decl["namespaces"] for v in visible)


def stems_from(code, msg, src, decl):
    """from_absent_assembly, plus cascades: a CASCADE_CODES error stems from the absent assembly when
    one of the unresolved types Diagnose found in its expression is a name it declares that this file
    can see. With no such type (a real flow error, a missing member of a known type) it gates."""
    m = UNRESOLVED_RE.search(msg)
    if code in CASCADE_CODES:
        return any(from_absent_assembly("CS0246", "The type or namespace name '%s' could not be found" % n.strip(), src, decl)
                   for n in (m.group(1).split(",") if m else []))
    return from_absent_assembly(code, msg[:m.start()] if m else msg, src, decl)


def self_test():
    """Classifier fixtures (no dotnet, no cache): planted misspellings gate; names an absent assembly
    declares, and their cascades, are bucketed only where the file can see them."""
    decl = {"asms": {"UnityEngine.Purchasing.Stores"},
            "namespaces": {"UnityEngine", "UnityEngine.Purchasing", "UnityEngine.Purchasing.Extension"},
            "types": {"StandardPurchasingModule": {"UnityEngine.Purchasing"}, "IStoreListener": {"UnityEngine.Purchasing"},
                      "CodelessIAPStoreListener": {"UnityEngine.Purchasing"}, "IAPButtonAttribute": {"UnityEngine.Purchasing"}}}
    iap = "using UnityEngine;\nusing UnityEngine.Purchasing;\nnamespace CosmicShore.Store {\n"
    plain = "using UnityEngine;\nnamespace CosmicShore.Controller {\n"
    cases = [
        # (expect bucketed, code, message, source)
        (False, "CS0103", "The name 'nearClipPlan' does not exist in the current context", plain),
        (False, "CS0103", "The name 'nearClipPlan' does not exist in the current context", iap),
        (False, "CS0246", "The type or namespace name 'Vectr3' could not be found (are you missing a using directive or an assembly reference?)", iap),
        (False, "CS0246", "The type or namespace name 'IStoreListener' could not be found (are you missing a using directive or an assembly reference?)", plain),
        (False, "CS0234", "The type or namespace name 'Extensoin' does not exist in the namespace 'UnityEngine.Purchasing' (are you missing an assembly reference?)", iap),
        (False, "CS0012", "The type 'Foo' is defined in an assembly that is not referenced. You must add a reference to assembly 'CosmicShore.Data, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.", plain),
        (False, "CS1061", "'Camera' does not contain a definition for 'nearClipPlan' and no accessible extension method", iap),
        (True, "CS0246", "The type or namespace name 'IStoreListener' could not be found (are you missing a using directive or an assembly reference?)", iap),
        (True, "CS0246", "The type or namespace name 'IStoreListener' could not be found (are you missing a using directive or an assembly reference?)", "namespace UnityEngine.Purchasing.Custom {\n"),
        (True, "CS0246", "The type or namespace name 'IAPButton' could not be found (are you missing a using directive or an assembly reference?)", iap),
        (True, "CS0103", "The name 'StandardPurchasingModule' does not exist in the current context", iap),
        (True, "CS0103", "The name 'Instance' does not exist in the current context", "using static UnityEngine.Purchasing.StandardPurchasingModule;\n" + plain),
        (True, "CS0538", "'IStoreListener' in explicit interface declaration is not an interface", iap),
        (True, "CS0538", "'UnityEngine.Purchasing.IStoreListener' in explicit interface declaration is not an interface", plain),
        (True, "CS0234", "The type or namespace name 'Extension' does not exist in the namespace 'UnityEngine.Purchasing' (are you missing an assembly reference?)", plain),
        (True, "CS0234", "The type or namespace name 'CodelessIAPStoreListener' does not exist in the namespace 'UnityEngine.Purchasing' (are you missing an assembly reference?)", plain),
        (True, "CS0012", "The type 'IStoreListener' is defined in an assembly that is not referenced. You must add a reference to assembly 'UnityEngine.Purchasing.Stores, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.", plain),
    ]
    # an unobtainable package, as the snapshot gives it: the party services' shape
    ugs = {"asms": {"Unity.Services.Multiplayer"},
           "namespaces": {"Unity", "Unity.Services", "Unity.Services.Multiplayer"},
           "types": {n: {"Unity.Services.Multiplayer"} for n in
                     ("ISession", "ISessionInfo", "IReadOnlyPlayer", "MultiplayerService", "PlayerProperty")}}
    party = "using System;\nusing Unity.Services.Multiplayer;\nusing UnityEngine;\nnamespace CosmicShore.Gameplay {\n"
    nf = "The type or namespace name '%s' could not be found (are you missing a using directive or an assembly reference?)"
    ugs_cases = [
        (False, "CS0103", "The name 'sesion' does not exist in the current context", party),
        (False, "CS0246", nf % "ISesion", party),
        (False, "CS0234", "The type or namespace name 'Multiplayr' does not exist in the namespace 'Unity.Services' (are you missing an assembly reference?)", party),
        (False, "CS0246", nf % "ISession", plain),
        (False, "CS1061", "'HostConnectionDataSO' does not contain a definition for 'LocalPlayr' and no accessible extension method 'LocalPlayr' accepting a first argument of type 'HostConnectionDataSO' could be found (are you missing a using directive or an assembly reference?)", party),
        (False, "CS0165", "Use of unassigned local variable 'parsed'", party),
        (False, "CS0165", "Use of unassigned local variable 'parsed' [unresolved types: Vectr3]", party),
        (False, "CS0019", "Operator '>' cannot be applied to operands of type 'method group' and 'int' [unresolved types: ISessionInfo]", plain),
        (False, "CS0029", "Cannot implicitly convert type 'string' to 'int' [unresolved types: ISession]", party),
        (True, "CS0234", "The type or namespace name 'Multiplayer' does not exist in the namespace 'Unity.Services' (are you missing an assembly reference?)", party),
        (True, "CS0246", nf % "ISession", party),
        (True, "CS0246", nf % "Unity.Services.Multiplayer.ISession", plain),
        (True, "CS0103", "The name 'MultiplayerService' does not exist in the current context", party),
        (True, "CS0165", "Use of unassigned local variable 'parsedAv' [unresolved types: IReadOnlyPlayer]", party),
        (True, "CS0165", "Use of unassigned local variable 'parsedAv' [unresolved types: IReadOnlyPlayer, Vectr3]", party),
        (True, "CS0019", "Operator '>' cannot be applied to operands of type 'method group' and 'int' [unresolved types: ISessionInfo]", party),
        (True, "CS0019", "Operator '>' cannot be applied to operands of type 'method group' and 'int' [unresolved types: Unity.Services.Multiplayer.ISessionInfo]", plain),
        (True, "CS1061", "'List<ISession>' does not contain a definition for 'Lenght' and no accessible extension method 'Lenght' accepting a first argument of type 'List<ISession>' could be found [unresolved types: ISession]", party),
    ]
    nothing = no_declarations()
    bad = [(want, code, msg) for want, code, msg, src in cases if stems_from(code, msg, src, decl) != want]
    bad += [(want, code, msg) for want, code, msg, src in ugs_cases if stems_from(code, msg, src, ugs) != want]
    bad += [(False, code, msg) for want, code, msg, src in cases + ugs_cases if stems_from(code, msg, src, nothing)]
    # the committed snapshot: every name the party services use from the five packages is declared, and a
    # misspelling is not (the names below are the ones a run without packages.unity.com reports)
    snap = load_snapshot()
    real = merge_declarations(snap.values())
    friends = ("using Unity.Services.Friends;\nusing Unity.Services.Friends.Models;\nusing Unity.Services.Friends.Exceptions;\n"
               "using Unity.Services.Friends.Notifications;\n" + plain)
    board = "using Unity.Services.Leaderboards;\n" + plain
    snap_cases = [(True, "CS0103", "The name '%s' does not exist in the current context" % n, party)
                  for n in ("MultiplayerService", "VisibilityPropertyOptions", "FilterField", "FilterOperation")]
    snap_cases += [(True, "CS0246", nf % n, party) for n in
                   ("ISession", "ISessionInfo", "IReadOnlyPlayer", "PlayerProperty", "SessionProperty", "SessionOptions",
                    "JoinSessionOptions", "QuerySessionsOptions", "FilterOption", "SessionException", "SessionError",
                    "PropertyIndex", "IMultiplayerService")]
    snap_cases += [(True, "CS0246", nf % n, friends) for n in
                   ("FriendsService", "IFriendsService", "FriendsServiceException", "Availability", "Relationship", "MemberRole",
                    "RelationshipType", "IRelationshipAddedEvent", "IRelationshipDeletedEvent", "IPresenceUpdatedEvent")]
    snap_cases += [(True, "CS0246", nf % n, board) for n in
                   ("LeaderboardsService", "AddPlayerScoreOptions", "GetScoresOptions", "GetScoresByPlayerIdsOptions")]
    snap_cases += [(True, "CS0234", "The type or namespace name '%s' does not exist in the namespace 'Unity.Services' (are you missing an assembly reference?)" % n, plain)
                   for n in ("Multiplayer", "Friends", "Leaderboards")]
    snap_cases += [(False, "CS0103", "The name 'sesion' does not exist in the current context", party),
                   (False, "CS0246", nf % "ISesion", party),
                   # declared, but in Friends.Notifications, which this file does not `using`
                   (False, "CS0246", nf % "IRelationshipAddedEvent", "using Unity.Services.Friends;\n" + plain)]
    bad += [(want, code, msg) for want, code, msg, src in snap_cases if stems_from(code, msg, src, real) != want]
    lock = load_json(os.path.join(ROOT, "Packages", "packages-lock.json"))["dependencies"]
    for pkg, d in sorted(snap.items()):
        if lock.get(pkg, {}).get("version") != d["version"]:
            bad.append((True, "-", "%s@%s in unobtainable_declarations.tsv, %s in packages-lock.json: %s"
                        % (pkg, d["version"], lock.get(pkg, {}).get("version", "absent"), REFRESH_HINT)))
    if not snap:
        bad.append((True, "-", "unobtainable_declarations.tsv is missing or empty: " + REFRESH_HINT))
    for want, code, msg in bad:
        print("[self-test] FAIL: expected %s: %s %s" % ("bucketed" if want else "project error", code, msg))
    n = len(cases) + len(ugs_cases) + len(snap_cases)
    print("[self-test] %s (%d cases)" % ("FAILED" if bad else "OK", n))
    return 1 if bad else 0

LEARN_0507 = re.compile(r"overriding 'public' inherited member '([^']+)'")
LEARN_0122 = re.compile(r"error CS0122: '([^']+)' is inaccessible due to its protection level")
LEARN_0104 = re.compile(r"error CS0104: '[^']+' is an ambiguous reference between '([^']+)' and '([^']+)'")
LEARN_1061 = re.compile(r"error CS1061: '([^']+)' does not contain a definition for '([^']+)' and no accessible")
LEARN_0117 = re.compile(r"error CS0117: '([^']+)' does not contain a definition for '([^']+)'")
_INDEX = None


def engine_index():
    global _INDEX
    if _INDEX is None:
        tsv = os.path.join(CACHE, "engine_refs_index.tsv")
        dll = os.path.join(CACHE, "tools", "Depublicize.dll")
        subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), dll, "--index", os.path.join(CACHE, "engine_refs_in"), tsv],
                       check=True)
        _INDEX = [l.rstrip("\n").split("\t") for l in open(tsv)]
    return _INDEX


def learn(errs):
    """Turn a PACKAGE's CS0507/CS0122 errors against engine types into accessibility overrides.
    Packages compile cleanly in Unity, so these errors are artifacts of the reference DLLs. Never
    called for Assets code."""
    want = []
    direct = set()
    for e in errs:
        m = LEARN_0507.search(e)
        if m:
            want.append((re.sub(r"<[^>]*>", "", m.group(1).split("(")[0]), "family"))
            continue
        m = LEARN_0122.search(e)
        if m:
            want.append((re.sub(r"<[^>]*>", "", m.group(1).split("(")[0]), "public"))
            continue
        m = LEARN_0104.search(e)
        if m:
            # an engine-internal type, publicized, now collides with a package's public one
            names = [re.sub(r"<[^>]*>", "", x) for x in m.groups()]
            eng_names = [x for x in names if any(r[1] == x and r[3] == "<type>" for r in engine_index())]
            if len(eng_names) == 1:
                for r in engine_index():
                    if r[1] == eng_names[0] and r[3] == "<type>":
                        direct.add("%s|%s|<type>|assembly" % (r[0], r[1]))
            continue
        # a member the 2021.1 oracle made internal reads as "does not contain a definition"
        m = LEARN_1061.search(e) or LEARN_0117.search(e)
        if m:
            want.append((re.sub(r"<[^>]*>", "", m.group(1)) + "." + m.group(2), "public"))
    lines = set(direct)
    for sym, acc in want:
        parts = sym.split(".")
        cands = []
        for i in range(len(parts) - 1, 0, -1):
            tshort, member = ".".join(parts[:i]), ".".join(parts[i:])
            cands.append((tshort, member))
        cands.append((sym, "<type>"))
        for tshort, member in cands:
            hits = [r for r in engine_index() if (r[2] == tshort or r[2].endswith("." + tshort) or r[1].endswith("." + tshort)
                                                  or r[1] == tshort) and r[3] == member]
            if hits:
                for r in hits:
                    lines.add("%s|%s|%s|%s" % (r[0], r[1], member, acc))
                break
    ovr = os.path.join(HERE, "depublicize_overrides.txt")
    have = set(l.strip() for l in open(ovr)) if os.path.exists(ovr) else set()
    new = sorted(lines - have)
    if new:
        with open(ovr, "a") as f:
            for l in new:
                f.write(l + "\n")
        print("[build] learned %d accessibility override(s) from package diagnostics: %s" % (len(new), "; ".join(new)))
    return bool(new)


def main():
    for attempt in range(8):
        rc = run_once()
        if rc != 3:
            return rc
        global _INDEX
        _INDEX = None
        print("[build] reference accessibility changed - rebuilding (pass %d)" % (attempt + 2))
    return 1


def run_once():
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default="player", choices=sorted(CONFIGS))
    ap.add_argument("--out", default=None)
    ap.add_argument("--only", default=None, help="stop after compiling this assembly")
    ap.add_argument("--max-errors", type=int, default=400)
    ap.add_argument("--changed-base", default="origin/bleeding-edge")
    ap.add_argument("--quiet-buckets", action="store_true", help="count, do not list, the unverifiable buckets")
    ap.add_argument("--self-test", action="store_true", help="check the error bucketing on fixtures, then exit")
    ap.add_argument("--write-declarations", action="store_true",
                    help="rewrite unobtainable_declarations.tsv from the packages this cache got from packages.unity.com")
    args = ap.parse_args()
    if args.self_test:
        return self_test()

    if not os.path.exists(os.path.join(CACHE, "manifest.json")):
        print("ERROR: no reference cache at %s - run fetch.py first (needs network once)." % CACHE)
        return 2

    defines = unity_version_defines() + COMMON + CONFIGS[args.config]
    # packages are always compiled as the player sees them (shared output, shared cache)
    pkg_defines = unity_version_defines(REFS_UNITY_VERSION) + COMMON + CONFIGS["player"]
    out_root = os.path.join(os.environ.get("TMPDIR", "/tmp"), "unity_refcompile_out")
    # per working tree: several worktrees share one TMPDIR, and Assets outputs (and the .rsp files
    # check_generated_assets.py reads) must never be another tree's
    out = args.out or os.path.join(out_root, tree_key(), args.config)
    pkg_out = os.path.join(out_root, "_packages")
    os.makedirs(pkg_out, exist_ok=True)
    editor = args.config == "editor"
    editor_included = []
    changed_files = set()
    r = subprocess.run(["git", "diff", "--name-only", args.changed_base + "...HEAD", "--", "*.cs"], cwd=ROOT,
                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    changed_files = {os.path.join(ROOT, x) for x in r.stdout.split()}
    os.makedirs(out, exist_ok=True)
    apply_source_patches()
    roots, versions = package_roots()
    registry = {n: d for n, d in roots if os.path.exists(os.path.join(d, ".registry"))}
    if args.write_declarations and not registry:
        print("ERROR: --write-declarations: no package in %s came from packages.unity.com - run fetch.py where "
              "that host is reachable." % CACHE)
        return 2
    asms, by_guid, owners, sources, dlls = discover(defines, pkg_defines, versions, roots)
    eng, ugui = engine_refs()

    # ugui (UnityEngine.UI + TextMeshPro): precompiled 6000.0 reference DLLs standing in for the
    # builtin com.unity.ugui 2.0.0 sources (not mirrored anywhere reachable).
    a = Asm("Unity.ugui", None, "precompiled-ugui")
    a.dlls = list(ugui.values())
    ugui_dlls = a.dlls
    for al in UGUI_ALIASES:
        if al.startswith("GUID:"):
            by_guid[al[5:]] = a
        else:
            asms[al] = a

    # stub assemblies for packages that are genuinely unobtainable
    engine_stubs = []
    for sp in sorted(glob.glob(os.path.join(HERE, "stubs", "*.cs"))):
        n = os.path.splitext(os.path.basename(sp))[0]
        if n in asms and asms[n].origin != "stub":
            continue
        head = open(sp, encoding="utf-8").read(2048)
        if n.startswith("UnityEngine."):
            # engine-module stub: compiled up front and added to every assembly's engine references
            edll = os.path.join(out, n + ".dll")
            r = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), csc_path(), "-nologo", "-noconfig", "-nostdlib",
                                "-target:library", "-langversion:9.0", "-out:" + edll, sp]
                               + ["-r:" + x for x in base_refs() + eng], stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, text=True)
            if r.returncode != 0:
                sys.exit("engine stub %s failed:\n%s" % (n, r.stdout))
            eng.append(edll)
            engine_stubs.append(n)
            continue
        a = Asm(n, None, "stub")
        a.files = [sp]
        m = re.search(r"//\s*refs:\s*(.+)", head)
        a.refs = [x.strip() for x in m.group(1).split(",")] if m else []
        a.unsafe = True
        asms[n] = a

    # assign sources
    assets = os.path.join(ROOT, "Assets")
    firstpass_roots = [os.path.join(assets, d) + os.sep for d in ("Plugins", "Standard Assets", "Pro Standard Assets")]
    pre = {"Assembly-CSharp-firstpass": Asm("Assembly-CSharp-firstpass", assets, "predefined"),
           "Assembly-CSharp": Asm("Assembly-CSharp", assets, "predefined")}
    editor_skipped = 0
    for kind, root, p in sources:
        o = owner_of(p, owners, root)
        if o is not None:
            o.files.append(p)
            continue
        if kind != "assets":
            continue  # package scripts outside any asmdef are not compiled by Unity
        rel = os.path.relpath(p, assets).split(os.sep)
        if "Editor" in rel[:-1]:
            if editor and p in changed_files:
                pre["Assembly-CSharp"].files.append(p)
                editor_included.append(p)
                continue
            editor_skipped += 1
            continue
        tgt = "Assembly-CSharp-firstpass" if any(p.startswith(r) for r in firstpass_roots) else "Assembly-CSharp"
        pre[tgt].files.append(p)
    pre["Assembly-CSharp"].refs = ["Assembly-CSharp-firstpass"]

    # precompiled managed DLLs
    auto_dlls, named_dlls, analyzers = [], {}, []
    for kind, p in dlls:
        if not is_managed(p):
            continue
        compat, explicit, dc, analyzer = plugin_meta(p)
        if analyzer:
            if kind == "assets":
                continue  # Assets/Analyzers (Microsoft.Unity.Analyzers): diagnostics only
            o = owner_of(p, owners, os.path.dirname(p) if kind == "assets" else "/")
            if o is not None:
                o.analyzers.append(p)
            continue
        if not compat or any(not constraint_ok(c, set(defines)) for c in dc):
            continue
        if "/Tests/" in p or "/Editor/" in p:
            continue
        named_dlls[os.path.basename(p)] = p
        if not explicit:
            auto_dlls.append(p)

    allasm = dict(asms)
    allasm.update(pre)

    def resolve(r):
        if r.startswith("GUID:"):
            return by_guid.get(r[5:])
        return allasm.get(r)

    live = {n: a for n, a in allasm.items()
            if a.excluded_reason is None and (a.files or getattr(a, "dlls", None))}
    # predefined assemblies reference every auto-referenced asmdef
    for pn in ("Assembly-CSharp-firstpass", "Assembly-CSharp"):
        extra = [n for n, a in live.items() if a.origin not in ("predefined",) and a.auto]
        pre[pn].refs = pre[pn].refs + extra

    # drop transitively-unneeded assemblies: compile only what Assembly-CSharp needs
    need, stack = set(), ["Assembly-CSharp"]
    missing = {}
    while stack:
        n = stack.pop()
        if n in need:
            continue
        need.add(n)
        for r in live[n].refs:
            t = resolve(r)
            if t is None or t.name not in live:
                missing.setdefault(n, []).append(r + ("" if t is None else " (excluded: %s)" % t.excluded_reason))
                continue
            stack.append(t.name)

    # topo order
    order, seen, onstack = [], set(), set()

    def visit(n):
        if n in seen:
            return
        if n in onstack:
            return
        onstack.add(n)
        for r in live[n].refs:
            t = resolve(r)
            if t is not None and t.name in need:
                visit(t.name)
        onstack.discard(n)
        seen.add(n)
        order.append(n)
    for n in sorted(need):
        visit(n)

    csc = csc_path()
    nsrefs = base_refs()
    changed = set()
    try:
        r = subprocess.run(["git", "diff", "--name-only", args.changed_base + "...HEAD", "--", "*.cs"], cwd=ROOT,
                           stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
        changed = {os.path.join(ROOT, x) for x in r.stdout.split()}
    except Exception:
        pass

    outputs = {}
    results = {}
    if editor:
        print("[build] editor config: + %d Editor-folder file(s) changed since %s: %s" % (
            len(editor_included), args.changed_base, ", ".join(os.path.relpath(x, ROOT) for x in editor_included) or "none"))
    print("[build] config=%s, %d assemblies to compile (of %d discovered), %d Editor-folder scripts skipped"
          % (args.config, len(order), len(allasm), editor_skipped))

    def refs_closure(n):
        """Unity passes only DIRECT references; but a referenced asm's own refs must be resolvable,
        so like Unity's bee driver we pass the transitive set."""
        res, st = [], [n]
        vis = set()
        while st:
            x = st.pop()
            for r in live[x].refs:
                t = resolve(r)
                if t is None or t.name not in need or t.name in vis:
                    continue
                vis.add(t.name)
                st.append(t.name)
                res.append(t.name)
        return res

    relearn = False
    for n in order:
        a = live[n]
        if getattr(a, "dlls", None):
            outputs[n] = a.dlls
            results[n] = ("precompiled", [])
            continue
        direct = [resolve(r).name for r in a.refs if resolve(r) is not None and resolve(r).name in need]
        trans = refs_closure(n)
        blocked = [d for d in trans if d not in outputs]
        refs = list(nsrefs)
        if not a.no_engine:
            refs += eng
            if a.origin.startswith("package:"):
                # UniTask & co. use UnityEngine.UI with no asmdef reference to it and compile in
                # Unity, so package code sees ugui implicitly. Assets asmdefs do NOT get this
                # leniency (a missing reference there must still fail).
                refs += ugui_dlls
        # A package assembly that failed (a reference-set artifact, listed in the summary) is left
        # out and its dependents still compile; Assets errors that stem from it name its types.
        refs += [x for d in trans if d in outputs for x in outputs[d]]
        if blocked:
            print("[build] %-55s compiling WITHOUT failed reference(s): %s" % (n, ", ".join(blocked[:6])))
        if a.override:
            refs += [named_dlls[p] for p in a.precompiled if p in named_dlls]
        else:
            refs += auto_dlls + [named_dlls[p] for p in a.precompiled if p in named_dlls]
        if editor and not a.origin.startswith("package:"):
            refs += editor_refs()
        refs = list(dict.fromkeys(refs))
        ana = list(a.analyzers)
        for d in direct:
            ana += live[d].analyzers
        ana = list(dict.fromkeys(ana))
        defs = sorted(set(pkg_defines if a.origin.startswith("package:") else defines) | set(a.defines))
        odir = pkg_out if a.origin.startswith("package:") else out
        dll = os.path.join(odir, n + ".dll")
        fp = fingerprint(sorted(a.files) + refs + ana + defs + [str(a.unsafe)])
        stamp = dll + ".stamp"
        if os.path.exists(dll) and os.path.exists(stamp) and open(stamp).read() == fp:
            outputs[n] = [dll]
            results[n] = ("cached", [])
            print("[build] %-55s cached" % n)
            continue
        rsp = os.path.join(odir, n + ".rsp")
        with open(rsp, "w") as f:
            f.write("-nologo -noconfig -nostdlib -target:library -langversion:9.0 -deterministic -debug- -optimize-\n")
            f.write("-nowarn:0169,0649,1701,1702,0108,0114,0414,0618,0612,0067,0168,0219,0162,8321,0436,1591\n")
            f.write("-warn:0\n")
            if a.unsafe:
                f.write("-unsafe\n")
            f.write("-define:" + ";".join(defs) + "\n")
            for r in refs:
                f.write('-r:"%s"\n' % r)
            for z in ana:
                f.write('-analyzer:"%s"\n' % z)
            f.write('-out:"%s"\n' % dll)
            for s in sorted(a.files):
                f.write('"%s"\n' % s)
        r = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), csc, "@" + rsp], stdout=subprocess.PIPE,
                           stderr=subprocess.STDOUT, text=True)
        errs = [l for l in r.stdout.splitlines() if ": error " in l]
        if r.returncode == 0 and os.path.exists(dll):
            outputs[n] = [dll]
            open(stamp, "w").write(fp)
            results[n] = ("ok", [])
            print("[build] %-55s ok (%d files)" % (n, len(a.files)))
        else:
            if not a.origin.startswith("package:"):
                # csc stops at declaration errors; get EVERY error (method bodies included)
                d = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), diagnose_tool(), rsp], stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True)
                full = [l for l in d.stdout.splitlines() if ": error " in l]
                if full:
                    errs = full
            if not errs:
                errs = r.stdout.splitlines()[-20:]
            results[n] = ("FAILED", errs)
            print("[build] %-55s FAILED: %d errors (%s)" % (n, len(errs), a.origin))
            if a.origin.startswith("package:") and learn(errs):
                relearn = True
        if args.only and n == args.only:
            break

    if relearn:
        return 3
    if args.write_declarations:
        # what Assets code can see of each registry-only package: the assemblies an Assets assembly
        # references directly (auto-referenced ones included), read from the rsp they compiled from
        seen = {resolve(r).name for x in live.values() if x.origin in ("assets", "predefined") and x.name in need
                for r in x.refs if resolve(r) is not None}
        snap = []
        for pkg, d in sorted(registry.items()):
            url, ver, sha = open(os.path.join(d, ".registry")).read().split()
            rsps = {n: os.path.join(pkg_out, n + ".rsp") for n in seen
                    if n in live and live[n].origin == "package:" + pkg and os.path.exists(os.path.join(pkg_out, n + ".rsp"))}
            snap.append((pkg, ver, sha, rsps))
            print("[build] declarations of %s@%s: %s" % (pkg, ver, ", ".join(sorted(rsps)) or "NO assembly Assets references"))
        write_snapshot(snap)
        print("[build] wrote %s" % os.path.relpath(UNOBTAINABLE_SNAPSHOT, ROOT))
    # report
    rep = {"config": args.config, "results": {k: {"status": v[0], "errors": v[1]} for k, v in results.items()},
           "missing_refs": missing}
    json.dump(rep, open(os.path.join(out, "report.json"), "w"), indent=1)
    failed = [k for k, v in results.items() if v[0] == "FAILED"]
    pkg_failed = [k for k in failed if live[k].origin.startswith("package:")]
    print("\n[build] ==== summary (%s) ====" % args.config)
    if pkg_failed:
        print("[build] package assemblies that did not compile against these references (reference-set")
        print("[build] artifacts - they compile in Unity; dependents were compiled without them):")
        for k in pkg_failed:
            errs = results[k][1]
            print("[build]   %s (%d errors), e.g. %s" % (k, len(errs), re.sub(r"^.*?: error ", "", errs[0])[:150] if errs else ""))
    # what each failed assembly declares, read from its own sources: an Assets missing-type error is a
    # reference-set artifact only when it names one of these, from an assembly k actually references
    decl_cache = {}

    def failed_decl(k):
        up = tuple(sorted(d for d in refs_closure(k) if d in failed))
        if up not in decl_cache:
            decl_cache[up] = declarations({d: os.path.join(pkg_out if live[d].origin.startswith("package:") else out,
                                                           d + ".rsp") for d in up})
        return decl_cache[up]
    # packages this run could not fetch: what each declares comes from the committed snapshot
    lock = load_json(os.path.join(ROOT, "Packages", "packages-lock.json"))["dependencies"]
    snapshot = load_snapshot()
    present = {n for n, _ in roots}
    absent = {p: d for p, d in snapshot.items() if p not in present}
    absent_decl = merge_declarations(absent.values())
    for p, d in sorted(absent.items()):
        if lock.get(p, {}).get("version") != d["version"]:
            print("[build] WARNING: %s is absent and unobtainable_declarations.tsv describes %s, not the locked %s - %s"
                  % (p, d["version"], lock.get(p, {}).get("version", "(not in the lock)"), REFRESH_HINT))
    unavailable = [r[0] for r in load_json(os.path.join(CACHE, "manifest.json")).get("results", [])
                   if str(r[2]).startswith("UNAVAILABLE") and r[0] not in snapshot and r[0] not in present]
    if unavailable:
        print("[build] WARNING: not fetched and not in unobtainable_declarations.tsv: %s - errors naming their "
              "types are reported as project errors" % ", ".join(sorted(unavailable)))
    for p, d in sorted(registry.items()):
        ver = os.path.basename(d).split("@", 1)[1]
        if snapshot.get(p, {}).get("version") != ver:
            print("[build] NOTE: unobtainable_declarations.tsv does not describe %s@%s, which this cache got from "
                  "packages.unity.com - rerun with --write-declarations to refresh it" % (p, ver))
    real, unobtainable, unverified = [], [], []
    for k in failed:
        if k in pkg_failed:
            continue
        for e in results[k][1]:
            m = re.match(r"(.*?)\(\d+,\d+\): error (\w+): (.*)", e)
            if not m:
                real.append((k, e))
                continue
            path, code = m.group(1), m.group(2)
            src = open(path, encoding="utf-8-sig", errors="replace").read() if os.path.exists(path) else ""
            if stems_from(code, m.group(3), src, absent_decl):
                unobtainable.append((k, e))
            elif editor and code in ("CS0115", "CS0117", "CS1061") and re.search(r"'(OnValidate|Reset)'|\.(OnValidate|Reset)\(\)", m.group(3)):
                # the uGUI/engine reference DLLs are PLAYER builds: their #if UNITY_EDITOR members
                # (UIBehaviour.OnValidate/Reset) do not exist in them
                unverified.append((k, e))
            elif stems_from(code, m.group(3), src, failed_decl(k)):
                unverified.append((k, e))
            else:
                real.append((k, e))

    def show(title, rows, limit):
        print("[build] %s: %d" % (title, len(rows)))
        for k, e in rows[:limit]:
            path = e.split("(")[0]
            tag = "[CHANGED-TONIGHT] " if path in changed else ""
            print("    %s%s" % (tag, e.replace(ROOT + "/", "")))
        if len(rows) > limit:
            print("    ... %d more (report.json)" % (len(rows) - limit))
    show("ERRORS in project code", real, args.max_errors)
    show("unobtainable: names declared by a package this run could not fetch (%s), and their cascades"
         % (", ".join(sorted(absent)) or "none"), unobtainable, 0 if args.quiet_buckets else args.max_errors)
    show("unverified: names declared by a referenced assembly that did not compile, or editor-only members absent from the player-build reference DLLs", unverified,
         0 if args.quiet_buckets else args.max_errors)
    n_changed = sum(1 for _, e in real if e.split("(")[0] in changed)
    stubbed = [k for k in need if live[k].origin == "stub"] + engine_stubs
    print("[build] stubbed assemblies: %s" % (", ".join(sorted(stubbed)) or "none"))
    unresolved = sorted({r for v in missing.values() for r in v})
    print("[build] unresolved asmdef references (Unity would also skip these): %s" % (", ".join(unresolved) or "none"))
    json.dump({"real": real, "unobtainable": unobtainable, "unverified": unverified, "package_failed": pkg_failed,
               "absent_packages": sorted(absent)},
              open(os.path.join(out, "buckets.json"), "w"), indent=1)
    if real:
        print("[build] RESULT: FAILED - %d error(s) in project code (%d in files changed since %s)"
              % (len(real), n_changed, args.changed_base))
        return 1
    print("[build] RESULT: OK - no errors in project code (%d assemblies; %d unobtainable and %d unverified "
          "errors bucketed above)" % (len(order), len(unobtainable), len(unverified)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
