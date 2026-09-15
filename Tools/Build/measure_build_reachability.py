#!/usr/bin/env python3
"""What actually ships: transitive asset reachability from the real build roots.

The question "is this folder referenced?" is not the question "does this reach a
build". This walks the second one, from Unity's real inclusion rules:

  ROOTS
    1. every ENABLED scene in ProjectSettings/EditorBuildSettings.asset
    2. every asset under a folder named Resources/ that is NOT under an Editor/
       folder  -- which is also what makes `Resources.Load` BY NAME a non-issue
       here: the whole folder is a root, so a name-loaded asset is never missed
    3. preloadedAssets in ProjectSettings/ProjectSettings.asset

  EDGES
    guid references, followed transitively, read out of every parseable asset
    and its .meta (so an importer's externalObjects remap counts as an edge).

THREE LIMITS -- read before quoting a number:

  * CODE SHIPS REGARDLESS. A .cs with no guid referrer still compiles into
    Assembly-CSharp. Unreached .cs files are a DEAD-CODE question, not a
    build-size one, and are excluded from the totals by default.
  * NATIVE PLUGINS SHIP BY PLATFORM IMPORTER SETTINGS, not by guid. Nothing
    references FMOD's per-platform binaries and they ship anyway. Assets/Plugins
    is excluded from the totals by default.
  * A RETIRED SERIALIZED KEY STILL GREPS AS A REFERENCE. This reads YAML text,
    so a field the script no longer declares still looks like a live edge; Unity
    drops it at import. This OVER-reports. (Measured instance: 40 SO_ArcadeGame
    assets still carry a `PreviewClip:` key that SO_ArcadeGame no longer declares,
    which pulled 110 MB of video into "ships" -- Docs/LAUNCH_BLOCKER_INDEX.md E2.
    Note the name is NOT globally dead: SO_VesselAbility declares a live
    `PreviewClip` and 24 of its assets carry a real one, so grep the OWNING type,
    never the field name.)

  Also unmodelled: Always Included Shaders, and per-platform texture compression
  (a PNG's bytes on disk are not its bytes in the build).

Usage:
  python3 Tools/Build/measure_build_reachability.py            # folder summary
  python3 Tools/Build/measure_build_reachability.py --folder Assets/_Graphics
  python3 Tools/Build/measure_build_reachability.py --list Assets/_Graphics --top 20
  python3 Tools/Build/measure_build_reachability.py --self-test
"""
import argparse, json, os, re, sys
from collections import defaultdict, deque

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GUID_RE = re.compile(rb"([0-9a-f]{32})")
OWN_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$")
MAX_BYTES = 60_000_000

# Excluded from totals for the reasons in the docstring, not because they are clean.
EXCLUDED_TOPS = {"_Scripts", "Plugins"}

PARSE_EXT = {
    ".unity", ".prefab", ".asset", ".mat", ".controller", ".playable",
    ".shadergraph", ".shadersubgraph", ".anim", ".overridecontroller",
    ".spriteatlas", ".mixer", ".preset", ".vfx", ".terrainlayer",
    ".physicmaterial", ".fontsettings", ".guiskin", ".flare", ".cubemap",
    ".rendertexture", ".lighting", ".giparams", ".signal", ".inputactions",
    ".shader", ".compute", ".hlsl", ".cginc",
}


def own_guid(meta_path):
    try:
        with open(meta_path, "r", encoding="utf-8", errors="ignore") as fh:
            for line in fh:
                m = OWN_RE.match(line.strip())
                if m:
                    return m.group(1)
    except OSError:
        pass
    return None


def build_tables():
    """asset path -> guid, guid -> asset path, asset path -> size."""
    guid_of, path_of, size_of = {}, {}, {}
    for dirpath, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        for name in files:
            if not name.endswith(".meta"):
                continue
            meta = os.path.join(dirpath, name)
            asset = meta[:-5]
            g = own_guid(meta)
            if not g:
                continue
            guid_of[asset] = g
            path_of[g] = asset
            try:
                size_of[asset] = os.path.getsize(asset) if os.path.isfile(asset) else 0
            except OSError:
                size_of[asset] = 0
    return guid_of, path_of, size_of


def outbound(asset_path):
    """guids referenced BY this asset, including by its .meta (importer remaps)."""
    found = set()
    for p in (asset_path, asset_path + ".meta"):
        if not p.endswith(".meta") and os.path.splitext(p)[1].lower() not in PARSE_EXT:
            continue
        try:
            if os.path.getsize(p) > MAX_BYTES:
                continue
            with open(p, "rb") as fh:
                data = fh.read()
        except OSError:
            continue
        found.update(m.decode() for m in GUID_RE.findall(data))
    return found


def collect_roots(guid_of):
    roots, notes = set(), {}

    ebs = os.path.join(ROOT, "ProjectSettings", "EditorBuildSettings.asset")
    txt = open(ebs, encoding="utf-8", errors="ignore").read()
    entries = re.findall(
        r"- enabled:\s*(\d+)\s*\n\s*path:\s*(.+?)\s*\n\s*guid:\s*([0-9a-f]{32})", txt)
    enabled = [(p, g) for en, p, g in entries if en == "1"]
    roots.update(g for _p, g in enabled)
    notes["scenes_enabled"] = len(enabled)
    notes["scenes_total"] = len(entries)

    res = 0
    for dirpath, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        parts = dirpath.split(os.sep)
        if "Resources" not in parts:
            continue
        if "Editor" in parts[: parts.index("Resources") + 1]:
            continue
        for name in files:
            if name.endswith(".meta"):
                continue
            a = os.path.join(dirpath, name)
            if a in guid_of:
                roots.add(guid_of[a])
                res += 1
    notes["resources_assets"] = res

    ps = os.path.join(ROOT, "ProjectSettings", "ProjectSettings.asset")
    pst = open(ps, encoding="utf-8", errors="ignore").read()
    m = re.search(r"preloadedAssets:\s*\n((?:\s*-\s*\{.*\n)*)", pst)
    pre = 0
    if m:
        for g in re.findall(r"guid:\s*([0-9a-f]{32})", m.group(1)):
            roots.add(g)
            pre += 1
    notes["preloaded"] = pre
    return roots, notes


def reach(roots, path_of):
    seen, queue = set(), deque()
    for g in roots:
        if g in path_of and g not in seen:
            seen.add(g)
            queue.append(g)
    while queue:
        g = queue.popleft()
        for n in outbound(path_of[g]):
            if n in path_of and n not in seen:
                seen.add(n)
                queue.append(n)
    return seen


def top_folder(asset_path):
    rel = os.path.relpath(asset_path, os.path.join(ROOT, "Assets"))
    return rel.split(os.sep)[0] if os.sep in rel else "(root)"


SANITY = [
    "Assets/Unity Assests/TextMesh Pro/Resources/TMP Settings.asset",
    "Assets/_Prefabs/Spacevessels/Manta.prefab",
    "Assets/_Prefabs/CORE/GameCanvas.prefab",
    "Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph",
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--folder", help="restrict the summary to one path prefix")
    ap.add_argument("--list", dest="listdir", help="list unreached assets under this path")
    ap.add_argument("--top", type=int, default=15)
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--self-test", action="store_true",
                    help="assert the model reaches assets that MUST be reachable")
    args = ap.parse_args()

    sys.stderr.write("indexing...\n")
    guid_of, path_of, size_of = build_tables()
    roots, notes = collect_roots(guid_of)
    seen = reach(roots, path_of)
    sys.stderr.write(f"reachable {len(seen)} of {len(path_of)}\n")

    if args.self_test:
        bad = []
        for probe in SANITY:
            p = os.path.join(ROOT, probe)
            if guid_of.get(p) not in seen:
                bad.append(probe)
        if bad:
            print("SELF-TEST FAILED — these must be reachable and are not:")
            for b in bad:
                print("   ", b)
            return 1
        print(f"self-test OK — all {len(SANITY)} probes reachable "
              f"(incl. TMP Settings.asset, which is reached as a Resources ROOT: "
              f"that is the Resources.Load-by-name blind spot being closed, not dodged)")
        return 0

    if args.listdir:
        base = os.path.join(ROOT, args.listdir)
        rows = [(size_of.get(a, 0), os.path.relpath(a, ROOT))
                for a, g in guid_of.items()
                if os.path.isfile(a) and a.startswith(base + os.sep) and g not in seen]
        rows.sort(reverse=True)
        total = sum(r[0] for r in rows)
        print(f"unreached under {args.listdir}: {len(rows)} files, {total / 1048576:.1f} MB")
        for s, p in rows[: args.top]:
            print(f"  {s / 1048576:7.1f} MB  {p}")
        return 0

    by = defaultdict(lambda: [0, 0, 0, 0])
    for a, g in guid_of.items():
        if not os.path.isfile(a):
            continue
        if args.folder and not a.startswith(os.path.join(ROOT, args.folder)):
            continue
        top = top_folder(a)
        if top in EXCLUDED_TOPS and not args.folder:
            continue
        s = size_of.get(a, 0)
        i = 0 if g in seen else 2
        by[top][i] += 1
        by[top][i + 1] += s

    if args.json:
        print(json.dumps({k: {"reached_files": v[0], "reached_bytes": v[1],
                              "unreached_files": v[2], "unreached_bytes": v[3]}
                          for k, v in by.items()}, indent=2))
        return 0

    print(f"roots: {notes['scenes_enabled']}/{notes['scenes_total']} enabled scenes, "
          f"{notes['resources_assets']} Resources assets, {notes['preloaded']} preloaded")
    print(f"reachable: {len(seen)} of {len(path_of)} assets")
    print(f"\nEXCLUDED from totals: {', '.join(sorted(EXCLUDED_TOPS))} "
          f"(code compiles regardless of references; native plugins ship by platform "
          f"importer settings) — see this file's docstring\n")
    print(f"{'folder':<34}{'reached MB':>12}{'unreached MB':>14}{'unreached':>11}")
    tr = tu = 0
    for top, (nr, br, nu, bu) in sorted(by.items(), key=lambda kv: -kv[1][3]):
        tr += br
        tu += bu
        if bu / 1048576 < 0.5 and br / 1048576 < 0.5:
            continue
        print(f"{top:<34}{br / 1048576:>12.1f}{bu / 1048576:>14.1f}{nu:>11}")
    print(f"{'TOTAL':<34}{tr / 1048576:>12.1f}{tu / 1048576:>14.1f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
