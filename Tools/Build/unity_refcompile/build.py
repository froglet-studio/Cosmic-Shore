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
def version_key(name):
    """Numeric sort key for a version directory name: "10.0.12" sorts after "8.0.31" (a plain string
    sort puts it first, and picked .NET 8 over 10 whenever both were installed)."""
    return tuple(int(x) for x in re.findall(r"\d+", name))


def csc_path():
    c = sorted(glob.glob(os.path.join(DOTNET_ROOT, "sdk", "*", "Roslyn", "bincore", "csc.dll")),
               key=lambda p: version_key(p.split(os.sep)[-4]))
    if not c:
        sys.exit("csc.dll not found under %s/sdk (set DOTNET_ROOT)" % DOTNET_ROOT)
    return c[-1]


def netcore_toolchain():
    """(reference-pack dir, runtime version, tfm) for building and running the helper tools
    (Depublicize, Diagnose, Schema). Any .NET SDK from 8.0 up will do: the tools are compiled against
    the newest Microsoft.NETCore.App reference pack the newest installed runtime can run."""
    shared = os.path.join(DOTNET_ROOT, "shared", "Microsoft.NETCore.App")
    runtimes = sorted((r for r in (os.listdir(shared) if os.path.isdir(shared) else []) if version_key(r)),
                      key=version_key)
    if not runtimes:
        sys.exit("no .NET runtime under %s (DOTNET_ROOT=%s): install a .NET SDK, 8.0 or newer" % (shared, DOTNET_ROOT))
    rt = runtimes[-1]
    packs = sorted(glob.glob(os.path.join(DOTNET_ROOT, "packs", "Microsoft.NETCore.App.Ref", "*", "ref", "net*.0")),
                   key=lambda p: version_key(p.split(os.sep)[-3]))
    usable = [p for p in packs if version_key(p.split(os.sep)[-3])[:1] <= version_key(rt)[:1]]
    if not usable:
        sys.exit("no Microsoft.NETCore.App reference pack that runtime %s can run under %s/packs (found: %s): "
                 "install a .NET SDK, not only a runtime" % (rt, DOTNET_ROOT, ", ".join(packs) or "none"))
    return usable[-1], rt, os.path.basename(usable[-1])


def base_refs():
    """netstandard 2.1 (Unity's API profile) + the NETStandard 2.0 facades. netstandard.dll comes from
    the SDK's NETStandard.Library.Ref pack when the SDK bundles one (8.0 does, 10.0 does not), else
    from the nuget copy fetch.py caches."""
    sdk = sorted(glob.glob(os.path.join(DOTNET_ROOT, "packs", "NETStandard.Library.Ref", "*", "ref", "netstandard2.1",
                                        "netstandard.dll")), key=lambda p: version_key(p.split(os.sep)[-4]))
    nuget = os.path.join(CACHE, "nuget", "netstandard.library.ref.2.1.0", "ref", "netstandard2.1", "netstandard.dll")
    ns = (sdk[-1:] + [nuget])[0]
    if not os.path.exists(ns):
        sys.exit("netstandard.dll 2.1 not found: no NETStandard.Library.Ref pack under %s/packs and no %s - "
                 "rerun fetch.py (needs network once)" % (DOTNET_ROOT, nuget))
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


def depublicize_tool():
    """Build (cached per toolchain) Depublicize/Program.cs - the Mono.Cecil rewriter, also the engine index."""
    cecil = os.path.join(CACHE, "packages", "com.unity.nuget.mono-cecil@1.11.6", "Mono.Cecil.dll")
    tool_src = os.path.join(HERE, "Depublicize", "Program.cs")
    tools = os.path.join(CACHE, "tools")
    dll = os.path.join(tools, "Depublicize.dll")
    ref, rt, tfm = netcore_toolchain()
    fp = fingerprint([tool_src, cecil, csc_path(), ref, rt])
    stamp = dll + ".stamp"
    if os.path.exists(dll) and os.path.exists(stamp) and open(stamp).read() == fp:
        return dll
    os.makedirs(tools, exist_ok=True)
    cmd = [os.path.join(DOTNET_ROOT, "dotnet"), csc_path(), "-nologo", "-noconfig", "-nostdlib", "-langversion:latest",
           "-r:" + cecil, "-out:" + dll, tool_src] + ["-r:" + x for x in glob.glob(os.path.join(ref, "*.dll"))]
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if r.returncode != 0:
        sys.exit("Depublicize tool failed to build:\n" + r.stdout)
    shutil.copyfile(cecil, os.path.join(tools, "Mono.Cecil.dll"))
    json.dump({"runtimeOptions": {"tfm": tfm, "framework": {"name": "Microsoft.NETCore.App", "version": rt}}},
              open(os.path.join(tools, "Depublicize.runtimeconfig.json"), "w"))
    open(stamp, "w").write(fp)
    return dll


def depublicize():
    """Build (once, cached) the de-publicized copies of the Unity 6 reference DLLs - see
    Depublicize/Program.cs. Rebuilt whenever overrides.txt or the tool changes."""
    src = os.path.join(CACHE, "nuget", "digitalroot.references.unity.6000.0.75", "ref", "net48")
    oracle = os.path.join(CACHE, "nuget", "unity3d.sdk.2021.1.14.1", "lib", "UnityEngine.dll")
    cecil = os.path.join(CACHE, "packages", "com.unity.nuget.mono-cecil@1.11.6", "Mono.Cecil.dll")
    tool_src = os.path.join(HERE, "Depublicize", "Program.cs")
    ovr = os.path.join(HERE, "depublicize_overrides.txt")
    out = os.path.join(CACHE, "engine_refs")
    stage = os.path.join(CACHE, "engine_refs_in")
    dll = depublicize_tool()
    # the output depends on the inputs, not on which .NET built the tool: a new SDK must not rewrite
    # the engine references (that would recompile every cached package assembly)
    fp = fingerprint([tool_src, ovr, cecil])
    stamp = os.path.join(out, ".stamp")
    if os.path.exists(stamp) and open(stamp).read() == fp:
        return out
    os.makedirs(stage, exist_ok=True)
    for p in glob.glob(os.path.join(src, "*.dll")):
        b = os.path.basename(p).replace("-publicized", "")
        if b.startswith("UnityEngine") or b == "Unity.TextMeshPro.dll":
            if not b.startswith("UnityEngine.SpatialTracking") and not b.startswith("UnityEngine.XR.Legacy"):
                shutil.copyfile(p, os.path.join(stage, b))
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
    ref, rt, tfm = netcore_toolchain()
    fp = fingerprint([src, csc_path(), ref, rt])
    if os.path.exists(stamp) and open(stamp).read() == fp:
        return dll
    bincore = os.path.dirname(csc_path())
    os.makedirs(tools, exist_ok=True)
    for b in ("Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll"):
        shutil.copyfile(os.path.join(bincore, b), os.path.join(tools, b))
    cmd = [os.path.join(DOTNET_ROOT, "dotnet"), csc_path(), "-nologo", "-noconfig", "-nostdlib", "-langversion:latest",
           "-r:" + os.path.join(tools, "Microsoft.CodeAnalysis.dll"), "-r:" + os.path.join(tools, "Microsoft.CodeAnalysis.CSharp.dll"),
           "-out:" + dll, src] + ["-r:" + x for x in glob.glob(os.path.join(ref, "*.dll"))]
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if r.returncode != 0:
        sys.exit("Diagnose tool failed to build:\n" + r.stdout)
    json.dump({"runtimeOptions": {"tfm": tfm, "framework": {"name": "Microsoft.NETCore.App", "version": rt}}},
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


# Packages that are not reachable from this sandbox (no mirror): files that `using` them can only be
# checked for errors that do not involve their types.
UNOBTAINABLE_NAMESPACES = ["Unity.Services.Multiplayer", "Unity.Services.Friends", "Unity.Services.Leaderboards",
                           "Unity.Multiplayer.Playmode", "Unity.Multiplayer.Widgets"]
MISSING_CODES = {"CS0246", "CS0234", "CS0103", "CS1069", "CS0012", "CS0538"}
# In a file that uses an unobtainable package, these are cascades of its unresolved types too
# (`out var x` from an unknown TryGetValue -> CS0165, `unknown.Count > 0` -> CS0019, ...).
UNOBTAINABLE_CASCADE_CODES = MISSING_CODES | {"CS0165", "CS0019", "CS1061"}

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
        dll = depublicize_tool()
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
    args = ap.parse_args()

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
            if code in UNOBTAINABLE_CASCADE_CODES and any(re.search(r"^\s*using\s+" + re.escape(ns) + r"\b", src, re.M) for ns in UNOBTAINABLE_NAMESPACES):
                unobtainable.append((k, e))
            elif editor and code in ("CS0115", "CS0117", "CS1061") and re.search(r"'(OnValidate|Reset)'|\.(OnValidate|Reset)\(\)", m.group(3)):
                # the uGUI/engine reference DLLs are PLAYER builds: their #if UNITY_EDITOR members
                # (UIBehaviour.OnValidate/Reset) do not exist in them
                unverified.append((k, e))
            elif code in MISSING_CODES and pkg_failed:
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
    show("missing-type errors in files using a package that cannot be fetched (%s)" % ", ".join(UNOBTAINABLE_NAMESPACES),
         unobtainable, 0 if args.quiet_buckets else args.max_errors)
    show("unverified: missing types while a referenced package failed, or editor-only members absent from the player-build reference DLLs", unverified,
         0 if args.quiet_buckets else args.max_errors)
    n_changed = sum(1 for _, e in real if e.split("(")[0] in changed)
    stubbed = [k for k in need if live[k].origin == "stub"] + engine_stubs
    print("[build] stubbed assemblies: %s" % (", ".join(sorted(stubbed)) or "none"))
    unresolved = sorted({r for v in missing.values() for r in v})
    print("[build] unresolved asmdef references (Unity would also skip these): %s" % (", ".join(unresolved) or "none"))
    json.dump({"real": real, "unobtainable": unobtainable, "unverified": unverified, "package_failed": pkg_failed},
              open(os.path.join(out, "buckets.json"), "w"), indent=1)
    if real:
        print("[build] RESULT: FAILED - %d error(s) in project code (%d in files changed since %s)"
              % (len(real), n_changed, args.changed_base))
        return 1
    print("[build] RESULT: OK - no errors in project code (%d assemblies; %d unobtainable-package and %d unverified "
          "missing-type errors listed above)" % (len(order), len(unobtainable), len(unverified)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
