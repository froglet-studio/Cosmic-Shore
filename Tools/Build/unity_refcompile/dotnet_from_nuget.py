#!/usr/bin/env python3
"""dotnet_from_nuget.py - a .NET root for the reference compile, from nuget.org packages alone.

A cloud session often cannot reach the .NET installers (dot.net, builds.dotnet.microsoft.com and GitHub release
downloads are refused by the egress proxy; Ubuntu 24.04 has no dotnet apt package), while api.nuget.org is reachable
as a package registry. `Tools/Build/unity_refcompile/build.py` needs no SDK proper: a `dotnet` host, a shared
runtime, a reference pack and Roslyn's `csc.dll` under `sdk/<x>/Roslyn/bincore/`, each of which nuget.org ships as
a package. This script fetches those seven packages and lays them out as the runtime installer would, so
`bash Tools/Build/unity_refcompile/run.sh` runs (it falls back to `$HOME/.dotnet` by itself).

    python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py                 # ~65 MB download -> ~/.dotnet (about a minute)
    python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py --root /opt/x   # another root (then DOTNET_ROOT=/opt/x)
    python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py --pkgs <dir>    # reuse downloaded .nupkg files
    python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py --check         # does ~/.dotnet run csc? (no network)
    python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py --self-test     # the layout logic on synthetic packages

What it is NOT: an SDK. `dotnet build`, `restore` and MSBuild do not exist in this root; `dotnet <app>.dll` and
`dotnet .../csc.dll` do, which is all build.py uses. linux-x64 only. First assembled by hand on 2026-11-07 (the
skill-training night, `Docs/Reports/SKILL_TRAINING_NIGHT.md`); the versions below are the ones that ran the
project's reference compile end to end that night.

Exit 0 on success, 1 when --check finds the root unusable, 2 on a download or layout error.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

RUNTIME = "8.0.33"
ROSLYN = "4.11.0"
NETSTANDARD = "2.1.0"
SDK_DIR = f"{RUNTIME}-roslyn{ROSLYN}"  # sdk/<dir>: only Roslyn lives here; build.py sorts sdk dirs by their digits
NUGET = "https://api.nuget.org/v3-flatcontainer"

PACKAGES = {
    "host": ("runtime.linux-x64.microsoft.netcore.dotnethost", RUNTIME),
    "fxr": ("runtime.linux-x64.microsoft.netcore.dotnethostresolver", RUNTIME),
    "runtime": ("microsoft.netcore.app.runtime.linux-x64", RUNTIME),
    "ref": ("microsoft.netcore.app.ref", RUNTIME),
    "netstandard": ("netstandard.library.ref", NETSTANDARD),
    "roslyn": ("microsoft.net.compilers.toolset", ROSLYN),
}


def nupkg_name(pkg_id: str, version: str) -> str:
    return f"{pkg_id}.{version}.nupkg"


def fetch(pkg_id: str, version: str, pkgs: str) -> str:
    path = os.path.join(pkgs, nupkg_name(pkg_id, version))
    if os.path.isfile(path) and os.path.getsize(path) > 0:
        return path
    url = f"{NUGET}/{pkg_id}/{version}/{nupkg_name(pkg_id, version)}"
    print(f"fetch {url}")
    with urllib.request.urlopen(url, timeout=120) as resp, open(path + ".part", "wb") as out:
        shutil.copyfileobj(resp, out)
    os.replace(path + ".part", path)
    return path


def extract(z: zipfile.ZipFile, prefix: str, dest: str, flat: bool = False, exclude: tuple = ()) -> int:
    """Copy every entry under prefix into dest, keeping the sub-path unless flat."""
    n = 0
    for name in z.namelist():
        if not name.startswith(prefix) or name.endswith("/"):
            continue
        rel = name[len(prefix):]
        if any(rel == e or rel.endswith("/" + e) for e in exclude):
            continue
        target = os.path.join(dest, os.path.basename(name) if (flat or not rel) else rel)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        with z.open(name) as src, open(target, "wb") as dst:
            shutil.copyfileobj(src, dst)
        n += 1
    return n


def layout(pkgs: str, root: str, paths: dict | None = None) -> None:
    """Lay the packages out under root. `paths` maps the package keys to .nupkg paths (default: pkgs/<name>)."""
    paths = paths or {k: os.path.join(pkgs, nupkg_name(*v)) for k, v in PACKAGES.items()}
    if os.path.exists(root) and os.listdir(root):
        raise RuntimeError(f"{root} exists and is not empty; remove it or pass another --root")
    os.makedirs(root, exist_ok=True)
    with zipfile.ZipFile(paths["host"]) as z:
        extract(z, "runtimes/linux-x64/native/", root, flat=True)
    os.chmod(os.path.join(root, "dotnet"), 0o755)
    with zipfile.ZipFile(paths["fxr"]) as z:
        extract(z, "runtimes/linux-x64/native/", os.path.join(root, "host", "fxr", RUNTIME), flat=True)
    shared = os.path.join(root, "shared", "Microsoft.NETCore.App", RUNTIME)
    with zipfile.ZipFile(paths["runtime"]) as z:
        extract(z, "runtimes/linux-x64/lib/net8.0/", shared, flat=True)
        extract(z, "runtimes/linux-x64/native/", shared, flat=True, exclude=("libhostfxr.so",))
    createdump = os.path.join(shared, "createdump")
    if os.path.exists(createdump):
        os.chmod(createdump, 0o755)
    refp = os.path.join(root, "packs", "Microsoft.NETCore.App.Ref", RUNTIME)
    notices = ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT", "LICENSE.txt", "THIRD-PARTY-NOTICES.txt",
               "Microsoft.NETCore.App.versions.txt", "License.txt", "ThirdPartyNotices.txt")
    with zipfile.ZipFile(paths["ref"]) as z:
        for top in ("ref/", "data/", "analyzers/"):
            extract(z, top, os.path.join(refp, top.rstrip("/")))
        for f in notices:
            extract(z, f, refp, flat=True)
    nsp = os.path.join(root, "packs", "NETStandard.Library.Ref", NETSTANDARD)
    with zipfile.ZipFile(paths["netstandard"]) as z:
        extract(z, "ref/", os.path.join(nsp, "ref"))
        extract(z, "data/", os.path.join(nsp, "data"))
        for f in notices:
            extract(z, f, nsp, flat=True)
    ros = os.path.join(root, "sdk", SDK_DIR, "Roslyn")
    with zipfile.ZipFile(paths["roslyn"]) as z:
        extract(z, "tasks/netcore/", ros)
        for f in notices:
            extract(z, f, ros, flat=True)
    with open(os.path.join(root, "README-nuget-assembly.txt"), "w", encoding="utf-8") as fh:
        fh.write(
            "This .NET root was assembled from nuget.org packages only by Tools/Build/unity_refcompile/dotnet_from_nuget.py:\n"
            f"  dotnet                                      runtime.linux-x64.Microsoft.NETCore.DotNetHost {RUNTIME}\n"
            f"  host/fxr/{RUNTIME}/libhostfxr.so               runtime.linux-x64.Microsoft.NETCore.DotNetHostResolver {RUNTIME}\n"
            f"  shared/Microsoft.NETCore.App/{RUNTIME}/        Microsoft.NETCore.App.Runtime.linux-x64 {RUNTIME}\n"
            f"  packs/Microsoft.NETCore.App.Ref/{RUNTIME}/     Microsoft.NETCore.App.Ref {RUNTIME}\n"
            f"  packs/NETStandard.Library.Ref/{NETSTANDARD}/      NETStandard.Library.Ref {NETSTANDARD}\n"
            f"  sdk/{SDK_DIR}/Roslyn/bincore/csc.dll  Microsoft.Net.Compilers.Toolset {ROSLYN}\n"
            "No SDK proper: `dotnet build` does not work; `dotnet <app>.dll` and `dotnet .../csc.dll` do.\n"
        )


def check(root: str) -> int:
    dotnet = os.path.join(root, "dotnet")
    csc = os.path.join(root, "sdk", SDK_DIR, "Roslyn", "bincore", "csc.dll")
    for p in (dotnet, csc, os.path.join(root, "shared", "Microsoft.NETCore.App", RUNTIME, "libcoreclr.so"),
              os.path.join(root, "packs", "Microsoft.NETCore.App.Ref", RUNTIME, "ref", "net8.0", "System.Runtime.dll")):
        if not os.path.exists(p):
            print(f"check: missing {p}")
            return 1
    try:
        out = subprocess.run([dotnet, csc, "-help"], capture_output=True, text=True, timeout=120)
    except (OSError, subprocess.TimeoutExpired) as exc:
        print(f"check: csc did not run: {exc}")
        return 1
    first = (out.stdout or out.stderr).strip().splitlines()[:1]
    if out.returncode != 0 or not first or "C# Compiler" not in first[0]:
        print(f"check: csc exited {out.returncode}: {first}")
        return 1
    print(f"check: OK  {first[0]}  ({root})")
    return 0


def _synthetic_pkg(path: str, entries: dict) -> None:
    with zipfile.ZipFile(path, "w") as z:
        for name, data in entries.items():
            z.writestr(name, data)


def self_test() -> int:
    with tempfile.TemporaryDirectory() as tmp:
        pk = os.path.join(tmp, "pkgs")
        os.makedirs(pk)
        paths = {}
        paths["host"] = os.path.join(pk, "host.nupkg"); _synthetic_pkg(paths["host"], {"runtimes/linux-x64/native/dotnet": b"#!/bin/sh\necho host\n"})
        paths["fxr"] = os.path.join(pk, "fxr.nupkg"); _synthetic_pkg(paths["fxr"], {"runtimes/linux-x64/native/libhostfxr.so": b"fxr"})
        paths["runtime"] = os.path.join(pk, "rt.nupkg"); _synthetic_pkg(paths["runtime"], {
            "runtimes/linux-x64/lib/net8.0/System.Runtime.dll": b"dll", "runtimes/linux-x64/native/libcoreclr.so": b"so",
            "runtimes/linux-x64/native/libhostfxr.so": b"fxr-dup", "runtimes/linux-x64/native/createdump": b"cd"})
        paths["ref"] = os.path.join(pk, "ref.nupkg"); _synthetic_pkg(paths["ref"], {"ref/net8.0/System.Runtime.dll": b"ref", "data/FrameworkList.xml": b"<x/>"})
        paths["netstandard"] = os.path.join(pk, "ns.nupkg"); _synthetic_pkg(paths["netstandard"], {"ref/netstandard2.1/netstandard.dll": b"ns"})
        paths["roslyn"] = os.path.join(pk, "roslyn.nupkg"); _synthetic_pkg(paths["roslyn"], {"tasks/netcore/bincore/csc.dll": b"csc", "tasks/netcore/bincore/csc.runtimeconfig.json": b"{}"})
        root = os.path.join(tmp, "root")
        layout(pk, root, paths)
        expect = [
            "dotnet", f"host/fxr/{RUNTIME}/libhostfxr.so", f"shared/Microsoft.NETCore.App/{RUNTIME}/System.Runtime.dll",
            f"shared/Microsoft.NETCore.App/{RUNTIME}/libcoreclr.so", f"packs/Microsoft.NETCore.App.Ref/{RUNTIME}/ref/net8.0/System.Runtime.dll",
            f"packs/NETStandard.Library.Ref/{NETSTANDARD}/ref/netstandard2.1/netstandard.dll", f"sdk/{SDK_DIR}/Roslyn/bincore/csc.dll",
        ]
        for rel in expect:
            assert os.path.exists(os.path.join(root, rel)), rel
        # the runtime pack's duplicate hostfxr is excluded from shared/ (host/fxr owns it)
        assert not os.path.exists(os.path.join(root, "shared", "Microsoft.NETCore.App", RUNTIME, "libhostfxr.so"))
        assert os.access(os.path.join(root, "dotnet"), os.X_OK)
        # negative controls: a non-empty root is refused; --check on a root with no csc fails
        try:
            layout(pk, root, paths)
            raise AssertionError("a non-empty root was overwritten")
        except RuntimeError:
            pass
        assert check(os.path.join(tmp, "nowhere")) == 1
    print("dotnet_from_nuget: self-test OK")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", default=os.path.join(os.path.expanduser("~"), ".dotnet"))
    ap.add_argument("--pkgs", help="a folder of already downloaded .nupkg files (default: a temp folder)")
    ap.add_argument("--check", action="store_true", help="only check that the root runs csc")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if args.check:
        return check(args.root)
    pkgs = args.pkgs or tempfile.mkdtemp(prefix="dotnet-nuget-")
    os.makedirs(pkgs, exist_ok=True)
    try:
        for key, (pkg_id, version) in PACKAGES.items():
            fetch(pkg_id, version, pkgs)
        layout(pkgs, args.root)
    except (OSError, RuntimeError, zipfile.BadZipFile) as exc:
        print(f"dotnet_from_nuget: {exc}", file=sys.stderr)
        return 2
    rc = check(args.root)
    if rc == 0:
        print(f"ready: bash Tools/Build/unity_refcompile/run.sh (DOTNET_ROOT={args.root})")
    return rc


if __name__ == "__main__":
    sys.exit(main())
