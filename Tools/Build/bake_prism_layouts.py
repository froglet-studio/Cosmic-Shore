#!/usr/bin/env python3
"""BAKE every cell environment's layout, so the game reads it instead of generating it on load.

WHY (Docs/ECOSYSTEM.md §62.3)
-----------------------------
Every world is fitted so no prism interpenetrates another, and the fit is part of its generator:
up to ~0.45 s of generation (Atlantis) before the first prism is laid. A world's output depends
only on its generator's code and its serialized fields, so it is computed HERE, once, and the game
decodes it (Garrett, 2026-10-20: "bake the fitted layouts into assets so load cost is zero").

WHAT IT WRITES
--------------
For every prefab whose generator extends CellEnvironmentSpawnableBase and does not opt out
(`BakesLayout => false`: the rail worlds Skein, Regatta, Switchyard and Breakwater, whose lays are
split into trails by build state a bake does not carry):

    Assets/Resources/PrismLayouts/<Type>_<fingerprint>.bytes   the bake (PrismLayoutBakeFormat)
    Tools/Build/prism_layout_bakes.json                         what was baked, sizes, load times

The generator is the SHIPPED C#, run through the prism clip audit's harness (the same run
`audit_prism_clipping.py` proves flush), and the bake is written by the shipped
PrismLayoutBakeFormat, so its fingerprint is the one the game computes from the prefab's fields.
A prefab whose fields change asks for a fingerprint that has no file, and the game generates it
live (with a warning naming this script) until it is re-baked. A world is baked at intensities 1
and 4 and the two must be byte-identical: a generator whose layout reads its intensity cannot be
served from one bake, and this fails rather than freezing one of them.

    python3 Tools/Build/bake_prism_layouts.py              # (re)bake everything
    python3 Tools/Build/bake_prism_layouts.py --check      # fail when a committed bake is stale
    python3 Tools/Build/bake_prism_layouts.py --self-test  # prove --check catches a bad bake

Run it after ANY change to a cell environment's generator, its prefab, the base class or the
format. Needs a dotnet 8 SDK reachable through DOTNET_ROOT (default ~/.dotnet).
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Build"))
import audit_prism_clipping as apc  # noqa: E402  prefab discovery and the harness
import render_card_backgrounds as rcb  # noqa: E402  the one reader of a generator prefab

OUT_DIR = ROOT / "Assets/Resources/PrismLayouts"
MANIFEST = ROOT / "Tools/Build/prism_layout_bakes.json"
BAKE_INTENSITIES = (1, 4)

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

BYTES_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(key):
    """A stable meta guid per file name, so a re-bake of the same world keeps its guid."""
    return hashlib.md5(f"cosmicshore-prism-layout:{key}".encode()).hexdigest()


def baked_types():
    """Generators that extend CellEnvironmentSpawnableBase and do not opt out of baking."""
    out = set()
    for p in (ROOT / apc.MG).glob("Spawnable*.cs"):
        text = rcb.read(p)
        if (re.search(rf"\bclass\s+{p.stem}\s*:\s*CellEnvironmentSpawnableBase\b", text) and
                not re.search(r"override\s+bool\s+BakesLayout\s*=>\s*false", text)):
            out.add(p.stem)
    return out


def unread_fields(prefab, fid, node):
    """Serialized keys on the generator that the harness does not set, other than object
    references and empty lists (whose harness default, the C# initializer, is what Unity loads).
    Any other one means the harness generates from a different value than the game does, and the
    bake's fingerprint would never match the prefab's."""
    body = rcb.docs(rcb.read(prefab))[str(fid)][1]
    out = []
    for m in re.finditer(r"^  ([A-Za-z_][A-Za-z0-9_]*):(.*)$", body, re.M):
        key, value = m.group(1), m.group(2).strip()
        if key.startswith("m_") or key in node["fields"]:
            continue
        if value == "[]" or value.startswith("{fileID:"):
            continue
        rest = body[m.end():].lstrip("\n")
        if value == "" and rest.startswith("  - {fileID:"):
            continue
        out.append(key)
    return out


def run_harness(tmp):
    """Bake every baked prefab at each of BAKE_INTENSITIES into tmp; returns the harness rows by
    entry name and the (prefab, fid, shape) list."""
    spec, prefabs = [], []
    for prefab, fid, name in apc.generator_prefabs(baked_types()):
        rel = prefab.relative_to(ROOT).as_posix()
        node = rcb.generator_node(prefab, fid)
        unread = unread_fields(prefab, fid, node)
        if unread:
            raise SystemExit(f"{rel}: the bake harness does not read {', '.join(unread)}, so it would bake from "
                             "values the game does not load; teach render_card_backgrounds.fields to read them")
        prefabs.append((rel, fid, name))
        for i in BAKE_INTENSITIES:
            n = json.loads(json.dumps(node))
            n["fields"]["intensityLevel"] = i
            spec.append({"name": f"{rel}#{fid}@{i}", "node": n, "source": str(ROOT / apc.MG / f"{name}.cs"),
                         "out": str(Path(tmp) / f"{len(spec)}.bytes")})
    sp = Path(tmp) / "bake_spec.json"
    sp.write_text(json.dumps(spec))
    env = dict(os.environ)
    env.setdefault("DOTNET_ROOT", str(Path.home() / ".dotnet"))
    res = subprocess.run(["bash", str(apc.HARNESS), "--bake", str(sp)], capture_output=True, text=True, env=env)
    if res.returncode != 0:
        sys.stderr.write(res.stderr[-4000:])
        raise SystemExit(f"bake harness failed (exit {res.returncode})")
    rows = {r["name"]: r for r in json.loads(res.stdout)}
    return spec, rows, prefabs


def bake(tmp):
    """Run the harness and assemble {stem: bytes} plus the manifest rows. Raises on an
    intensity-dependent world or a failed round trip."""
    spec, rows, prefabs = run_harness(tmp)
    files, manifest, errors = {}, [], []
    for rel, fid, name in prefabs:
        entries = [e for e in spec if e["name"].startswith(f"{rel}#{fid}@")]
        datas = [Path(e["out"]).read_bytes() for e in entries]
        r = rows[entries[0]["name"]]
        if not all(rows[e["name"]]["round_trip"] for e in entries):
            errors.append(f"{rel}: the bake did not decode back to its own lays")
        if any(d != datas[0] for d in datas[1:]):
            errors.append(f"{rel} ({name}): the layout differs between intensities {BAKE_INTENSITIES} - a world "
                          "that reads its intensity cannot be served from one bake (give it BakesLayout => false)")
            continue
        stem = r["stem"]
        files[stem] = datas[0]
        manifest.append({"prefab": rel, "shape": name, "stem": stem, "prisms": r["prisms"], "sites": r["sites"],
                         "bytes": r["bytes"], "sha256": hashlib.sha256(datas[0]).hexdigest(),
                         "generate_ms": r["generate_ms"], "decode_ms": r["decode_ms"]})
    if errors:
        raise SystemExit("\n".join(errors))
    return files, sorted(manifest, key=lambda m: m["prefab"])


def committed():
    """{stem: bytes} of the bakes in the repo."""
    return {p.stem: p.read_bytes() for p in sorted(OUT_DIR.glob("*.bytes"))} if OUT_DIR.exists() else {}


def compare(expected, have):
    """Every way the committed bakes differ from a fresh bake (empty = current)."""
    out = []
    for stem in sorted(expected.keys() - have.keys()):
        out.append(f"missing {stem}.bytes")
    for stem in sorted(have.keys() - expected.keys()):
        out.append(f"stale {stem}.bytes (no prefab bakes to it any more)")
    for stem in sorted(expected.keys() & have.keys()):
        if expected[stem] != have[stem]:
            out.append(f"drifted {stem}.bytes (its generator's code changed since it was baked)")
    return out


def write(files, manifest):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    folder_meta = OUT_DIR.with_name(OUT_DIR.name + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text(FOLDER_META.format(guid=guid_for("folder")))
    keep = set()
    for stem, data in files.items():
        (OUT_DIR / f"{stem}.bytes").write_bytes(data)
        meta = OUT_DIR / f"{stem}.bytes.meta"
        if not meta.exists():
            meta.write_text(BYTES_META.format(guid=guid_for(stem)))
        keep.update({f"{stem}.bytes", f"{stem}.bytes.meta"})
    for p in OUT_DIR.iterdir():
        if p.name not in keep:
            p.unlink()
    total = sum(m["bytes"] for m in manifest)
    MANIFEST.write_text(json.dumps({
        "_doc": "Written by Tools/Build/bake_prism_layouts.py - do not edit. One row per baked prefab: the bake it "
                "reads, its size, and the load time it saves (generate_ms, the live generation, against decode_ms, "
                "reading the bake; .NET 8 on the build container, a phone is slower). --check compares the bakes, "
                "not these timings.",
        "total_bytes": total,
        "bakes": manifest,
    }, indent=1) + "\n")


def self_test(expected):
    """--check must flag a changed byte, a missing bake and an orphan bake."""
    stem = next(iter(expected))
    flipped = dict(expected)
    flipped[stem] = bytes([expected[stem][0] ^ 1]) + expected[stem][1:]
    cases = {"changed byte": flipped,
             "missing bake": {k: v for k, v in expected.items() if k != stem},
             "orphan bake": {**expected, "SpawnableNothing_0000000000000000": b"x"}}
    bad = [name for name, have in cases.items() if not compare(expected, have)]
    if compare(expected, expected):
        bad.append("an identical set reads as drifted")
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="fail when a committed bake is stale")
    ap.add_argument("--self-test", action="store_true", help="prove --check catches a bad bake")
    a = ap.parse_args()

    with tempfile.TemporaryDirectory() as tmp:
        files, manifest = bake(tmp)
    if a.self_test:
        bad = self_test(files)
        if bad:
            print("bake_prism_layouts --self-test: FAIL - --check missed: " + ", ".join(bad))
            return 1
        print(f"bake_prism_layouts --self-test: OK ({len(files)} bakes; a changed byte, a missing bake and an "
              "orphan bake are each flagged)")
        return 0
    if a.check:
        diffs = compare(files, committed())
        if diffs:
            print("bake_prism_layouts --check: STALE\n  " + "\n  ".join(diffs) +
                  "\n  re-bake: python3 Tools/Build/bake_prism_layouts.py")
            return 1
        print(f"bake_prism_layouts --check: OK ({len(files)} bakes match their generators)")
        return 0

    write(files, manifest)
    total = sum(m["bytes"] for m in manifest)
    print(f"baked {len(files)} layouts for {len(manifest)} prefabs into {OUT_DIR.relative_to(ROOT)} "
          f"({total / 1e6:.1f} MB)")
    for m in manifest:
        print(f"  {m['prefab'].replace('Assets/_Prefabs/', ''):<52} {m['prisms']:>6} prisms  "
              f"{m['bytes'] / 1e6:5.2f} MB  live {m['generate_ms']:7.1f} ms  ->  bake {m['decode_ms']:5.1f} ms")
    return 0


if __name__ == "__main__":
    sys.exit(main())
