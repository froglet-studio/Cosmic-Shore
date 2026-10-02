#!/usr/bin/env python3
"""
Packs the project content the port's player reads into one zip for the Android head.

The player loads the REAL project straight off a filesystem (CosmicShore.Content: Unity YAML,
.meta guids, textures, meshes, fonts, FMOD banks). An APK's assets are entries in a zip, not
files, so the Android head ships this pack as one asset and extracts it into the app's private
storage on first launch (MainActivity.EnsureContent), then points COSMIC_SHORE_PROJECT at it.

What goes in, and why:
  * every Assets/**/*.meta - AssetDatabase indexes the guid table by enumerating ALL of them,
    and the importer settings of whatever it loads live there. A meta whose asset is not packed
    simply resolves to a missing file, which every loader already treats as absent.
  * the BUILD-REACHABLE asset set - Tools/Build/measure_build_reachability.py's walk from the
    enabled build scenes + every Resources/ asset + preloaded assets (the roots Unity's player
    build uses), so every scene a menu card can load is present.
  * every shader / shader graph / HLSL / C# source under Assets - read by name rather than by
    guid edge (shader property catalogs, script namespace lookup), so a guid walk misses some.
    A strace of a desktop menu boot found 25 opened files outside the reachable set: 20 shaders
    and 5 ProjectSettings assets; both classes are covered here.
  * ProjectSettings/*, Packages/*.json, and the FMOD Studio bank build (Cosmic Shore/Build/Desktop
    - FMOD banks are platform-neutral; the Desktop build plays on Android's FMOD runtime).

What stays out: audio clips and video (.wav/.mp3/.ogg/.mp4/...) - the port plays sound through
FMOD banks and never opens a clip (verified by the same strace); native plugin binaries.

Usage:  python3 Port/tools/pack_android_content.py OUT.zip [--list]
Prints a manifest summary; the zip carries a CONTENT_VERSION entry (also written beside it as
OUT.version) (hash of the file list +
sizes + mtimes) the head uses to decide whether an installed copy is stale.
"""
import hashlib
import os
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"project root not found at {ROOT}"
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
import measure_build_reachability as reach  # noqa: E402

EXCLUDE_EXT = {".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mp4", ".mov", ".webm",
               ".m4v", ".avi", ".dll", ".so", ".dylib", ".bundle", ".a", ".jar", ".aar",
               ".pdb", ".exe", ".lib"}
BY_NAME_EXT = {".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc", ".cs", ".compute"}
STORED_EXT = {".png", ".jpg", ".jpeg", ".bank", ".zip"}


def rel(p):
    return os.path.relpath(p, ROOT).replace(os.sep, "/")


def collect():
    files = set()
    guid_of, path_of, _ = reach.build_tables()
    roots, _ = reach.collect_roots(guid_of)
    for g in reach.reach(roots, path_of):
        p = path_of[g]
        if os.path.isfile(p):
            files.add(rel(p))
    for dirpath, _dirs, names in os.walk(os.path.join(ROOT, "Assets")):
        for n in names:
            ext = os.path.splitext(n)[1].lower()
            if n.endswith(".meta") or ext in BY_NAME_EXT:
                files.add(rel(os.path.join(dirpath, n)))
    for d in ("ProjectSettings",):
        for n in os.listdir(os.path.join(ROOT, d)):
            p = os.path.join(ROOT, d, n)
            if os.path.isfile(p):
                files.add(rel(p))
    for n in ("manifest.json", "packages-lock.json"):
        p = os.path.join(ROOT, "Packages", n)
        if os.path.isfile(p):
            files.add(rel(p))
    banks = os.path.join(ROOT, "Cosmic Shore", "Build", "Desktop")
    for n in os.listdir(banks):
        files.add(rel(os.path.join(banks, n)))
    return sorted(f for f in files
                  if os.path.splitext(f)[1].lower() not in EXCLUDE_EXT
                  and not is_lfs_pointer(os.path.join(ROOT, f)))


def is_lfs_pointer(path):
    try:
        if os.path.getsize(path) > 200:
            return False
        with open(path, "rb") as fh:
            return fh.read(40).startswith(b"version https://git-lfs")
    except OSError:
        return False


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    out = sys.argv[1]
    files = collect()
    h = hashlib.sha256()
    total = 0
    for f in files:
        st = os.stat(os.path.join(ROOT, f))
        total += st.st_size
        h.update(f"{f}\0{st.st_size}\0".encode())
        with open(os.path.join(ROOT, f), "rb") as fh:
            h.update(hashlib.sha1(fh.read()).digest())
    version = h.hexdigest()[:16]
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    tmp = out + ".tmp"
    with zipfile.ZipFile(tmp, "w", allowZip64=True) as z:
        z.writestr("CONTENT_VERSION", version)
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            z.write(os.path.join(ROOT, f), f,
                    compress_type=zipfile.ZIP_STORED if ext in STORED_EXT else zipfile.ZIP_DEFLATED)
    os.replace(tmp, out)
    # Beside the zip: the version alone, so the head can tell whether its installed copy is
    # current without opening a 100 MB archive on every launch.
    with open(os.path.splitext(out)[0] + ".version", "w") as fh:
        fh.write(version)
    print(f"[pack] {len(files)} files, {total / 1e6:.1f} MB raw -> {os.path.getsize(out) / 1e6:.1f} MB zip, version {version}")
    if "--list" in sys.argv:
        for f in files:
            print(f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
