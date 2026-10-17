#!/usr/bin/env python3
"""
build.py - build one vessel model from its spec: model -> rig -> morphs -> takes -> FBX ->
verify (three readers) -> contact sheet.

    python3 Tools/Build/vessel_model/build.py <spec> [--out <file.fbx>] [--sheet-dir <dir>] [--no-render] [--check]

<spec> is a module name under Tools/Build/vessel_model/specs/ (e.g. `squirrel`) or a path to
a .py file. A spec exposes:

    NAME        = "Squirrel"                      # the vessel
    OUTPUT      = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"  # default --out
    def build(ctx) -> dict                        # builds the scene; returns the verify contract
                                                  #   {"armature": obj, "mesh": obj, "spec": {...verify spec...}}

`ctx` is a BuildContext with the library modules (forms, rig, morphs, takes, materials) and
helpers (`ctx.out_dir`, `ctx.log`). The spec owns every number; this driver owns the order of
operations, the gate and the sheet.

--check  : build to a temp file, verify, and DIFF the probe against the committed OUTPUT
           (names, bones, takes, shapes, slots, bounds); exit 1 on drift. This is the drift
           gate a session runs before committing a model and the one CI could run.
"""

import argparse
import importlib.util
import json
import os
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))  # Tools/Build/vessel_model -> repo root
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "specs"))
sys.path.insert(0, os.path.dirname(HERE))


ARTIST_REF = "b7f0fbbf3"  # the commit that holds the ARTIST files; a spec may override with its own ARTIST_REF
ARTIST_CACHE = os.path.join(HERE, ".artist_cache")  # git-ignored


def artist_path(output_rel, ref=None):
    """
    The artist's file for a spec's OUTPUT, extracted once from git at ARTIST_REF into a cache.
    Every spec reuses the artist's armature and every gate compares against the artist's file -
    never against the file on disk, which after the first in-place write is our own rebuild, so a
    comparison with it compares the pipeline with itself (review finding, 2026-10-17; the
    first "0.000 on all seven" was measured that way and was wrong for three vessels).
    """
    ref = ref or ARTIST_REF
    dst = os.path.join(ARTIST_CACHE, ref, os.path.basename(output_rel))
    if not os.path.exists(dst):
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        import subprocess
        data = subprocess.run(["git", "-C", REPO, "show", "%s:%s" % (ref, output_rel)], capture_output=True, check=True).stdout
        with open(dst, "wb") as f:
            f.write(data)
    return dst


class BuildContext:
    def __init__(self, out_dir, artist_ref=None):
        import forms, rig, morphs, takes, materials, export  # noqa: E401
        self.forms, self.rig, self.morphs, self.takes, self.materials, self.export = forms, rig, morphs, takes, materials, export
        self.out_dir = out_dir
        self.artist_ref = artist_ref
        self.notes = []

    def artist(self, output_rel):
        """The pinned artist file for this spec's OUTPUT (what the rig is reused from and the gates compare to)."""
        return artist_path(output_rel, self.artist_ref)

    def log(self, msg):
        print("  [build] " + msg)
        self.notes.append(msg)


def load_spec(spec):
    if spec.endswith(".py") and os.path.exists(spec):
        path = spec
        name = os.path.splitext(os.path.basename(spec))[0]
    else:
        path = os.path.join(HERE, "specs", spec.lower() + ".py")
        name = spec.lower()
    if not os.path.exists(path):
        raise SystemExit("no spec at %s" % path)
    mod_spec = importlib.util.spec_from_file_location("vessel_spec_" + name, path)
    mod = importlib.util.module_from_spec(mod_spec)
    mod_spec.loader.exec_module(mod)
    return mod


def run(spec_mod, out_path, sheet_dir=None, render=True, samples=20, committed=None):
    import forms  # noqa: F401  (import after bpy is importable)
    import export as E
    import render as R
    import takes as T
    import fbx_probe as P
    t0 = time.time()
    forms.fresh_scene()
    T.AUTHORED.clear()
    ctx = BuildContext(os.path.dirname(os.path.abspath(out_path)), artist_ref=getattr(spec_mod, "ARTIST_REF", None))
    result = spec_mod.build(ctx)
    arm, mesh, vspec = result["armature"], result["mesh"], result["spec"]
    # the slot materials carry the artist file's colours in the FILE (captured when the armature was imported,
    # which happens after the parts were painted, so apply them now)
    for mat in mesh.data.materials:
        if mat is not None:
            r, g, b = forms.slot_colour(mat.name)
            mat.diffuse_color = (r, g, b, 1.0)
    # what the takes authored -> verify checks the file moved exactly those bones
    vspec.setdefault("expected_moving", {k: list(v) for k, v in T.AUTHORED.items()})
    # the ARTIST file this one replaces: its size (cameras, colliders and jets were tuned to it), its
    # slot order and its material colours; a spec overrides any of these when the change is meant
    if getattr(spec_mod, "OUTPUT", ""):
        try:
            ap = ctx.artist(spec_mod.OUTPUT)
            cp = P.probe(ap, want_takes=False)
            if cp["meshes"]:
                vspec.setdefault("size_ref_blender", E.blender_posed_bounds(ap))
                vspec.setdefault("slot_order_ref", [m["name"] for m in cp["materials"]])
                vspec.setdefault("material_colours_ref", {m["name"]: m["diffuse"] for m in cp["materials"] if m.get("diffuse")})
            vspec.setdefault("artist_path", ap)
            meta = os.path.join(REPO, spec_mod.OUTPUT + ".meta")
            if os.path.exists(meta):
                takes_pinned, mats_remapped = [], []
                txt = open(meta, encoding="utf-8", errors="replace").read()
                import re as _re
                takes_pinned = _re.findall(r"^\s*takeName:\s*(.+?)\s*$", txt, flags=_re.M)
                # externalObjects entries: '- first: {type: UnityEngine:Material, assembly: ..., name: Body}'
                mats_remapped = _re.findall(r"first:\s*\{[^}]*type:\s*UnityEngine:Material[^}]*name:\s*([^,}\s]+)", txt)
                vspec.setdefault("meta_takes", takes_pinned)
                vspec.setdefault("meta_materials", mats_remapped)
        except Exception as e:  # no artist file (a new vessel) is fine; say so and verify without the references
            ctx.log("no artist reference for %s: %s" % (spec_mod.OUTPUT, e))
    print("  built %s: %d verts, %d tris, slots %s, %.1fs" % (
        spec_mod.NAME, len(mesh.data.vertices), forms.tri_count(mesh), ctx.materials.slot_report(mesh), time.time() - t0))
    rep = E.export_and_verify(out_path, [arm, mesh], vspec)
    print("  exported %s (%d KB), stripped %d blend-shape curve objects, verified by codec + assimp + re-import" % (
        os.path.abspath(out_path), os.path.getsize(out_path) // 1024, rep["stripped"]))
    probe = rep["probe"]
    m = probe["meshes"][0]
    print("  shapes: " + ", ".join("%s %.1f%%" % (s["name"], 100 * s["travel_fraction"]) for s in m["shapes"]))
    print("  takes:  " + ", ".join("%s(%d move)" % (t["name"].split("|")[-1], t["nodes_that_move"]) for t in probe["takes"]))
    files = []
    if render:
        sd = sheet_dir or os.path.join(ctx.out_dir, "sheets")
        files = R.contact_sheet(out_path, sd, tag=spec_mod.NAME.lower(), samples=samples,
                                poses=vspec.get("sheet_poses", ("pitchup", "yawright", "rollright", "speedup")),
                                nose=vspec.get("nose", -1))
        print("  sheet: %s" % files)
    return {"probe": probe, "sheet": files, "notes": ctx.notes}


def summary_of(probe):
    """
    The part of a probe that must not drift between builds: names, hierarchy, every node's rest
    TRS, the skin, the shapes' travel, the slots, the takes and what each deflects, the axes.
    (The first version kept names only, so a moved bone, a doubled pose or a re-skinned part
    still "rebuilt to the committed structure" - review finding, 2026-10-17.)
    """
    m = probe["meshes"][0]
    r3 = lambda v: [round(x, 3) for x in v]
    return {
        "models": sorted((x["name"], x["type"], x["parent"]) for x in probe["models"]),
        "node_trs": {x["name"]: [r3(x["translation"]), r3(x["rotation"]), r3(x["scaling"]), r3(x["pre_rotation"]) if x.get("pre_rotation") else None]
                     for x in probe["models"]},
        "bones": sorted(b["name"] for b in probe["bones"]),
        "skin": {b["name"]: b.get("skinned_verts", 0) for b in probe["bones"]},
        "takes": sorted(t["name"] for t in probe["takes"]),
        "take_deflections": {t["name"]: {node: {prop: round(v["offset"], 2) for prop, v in props.items()}
                                         for node, props in t["moving_detail"].items()} for t in probe["takes"]},
        "shapes": sorted((s["name"], round(s["travel_fraction"], 3)) for s in m["shapes"]),
        "slots": {str(k): v for k, v in m["material_poly_counts"].items()},
        "materials": [x["name"] for x in probe["materials"]],
        "verts": m["verts"], "polys": m["polys"],
        "bounds": [r3(m["bounds"][0]), r3(m["bounds"][1])],
        "axes": {k: probe["settings"].get(k) for k in ("UnitScaleFactor", "UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign")},
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("spec")
    ap.add_argument("--out")
    ap.add_argument("--sheet-dir")
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--samples", type=int, default=20)
    ap.add_argument("--check", action="store_true", help="rebuild to a temp file and diff against the committed output")
    a = ap.parse_args()
    mod = load_spec(a.spec)
    committed = os.path.join(REPO, getattr(mod, "OUTPUT", "")) if getattr(mod, "OUTPUT", "") else None
    if a.check:
        import fbx_probe as P
        if not committed or not os.path.exists(committed):
            raise SystemExit("--check needs a committed OUTPUT (%s)" % committed)
        with tempfile.TemporaryDirectory() as td:
            tmp = os.path.join(td, os.path.basename(committed))
            res = run(mod, tmp, render=False, committed=committed)
            fresh = summary_of(res["probe"])
        shipped = summary_of(P.probe(committed, want_takes=True))
        drift = {k: (shipped[k], fresh[k]) for k in fresh if fresh[k] != shipped[k]}
        if drift:
            print("DRIFT between the spec and the committed %s:" % committed)
            for k, (s, f) in drift.items():
                print("  %s:\n    committed: %s\n    rebuilt:   %s" % (k, json.dumps(s)[:400], json.dumps(f)[:400]))
            sys.exit(1)
        print("OK: %s rebuilds to the committed file's structure" % mod.NAME)
        return
    out = a.out or committed
    if not out:
        raise SystemExit("spec has no OUTPUT; pass --out")
    if not a.out:
        # Writing in place: say so, with the absolute path, so a session can `git status` exactly this file.
        # (The first in-place run resolved the repo root one level short and wrote seven green builds into
        # Tools/Assets/... while every gate stayed green - LESSONS.md 2026-10-17 #24.)
        print("  writing IN PLACE over the committed model: %s" % out)
    run(mod, out, a.sheet_dir, render=not a.no_render, samples=a.samples, committed=committed)


if __name__ == "__main__":
    main()
