"""
export.py - the FBX recipe, the post-export strip, and the three-reader verification.

THE AXIS STORY (measured on the shipped Squirrel, 2026-10-17; the dossier's section 1.4):
  The artist files are Z-UP files with the nose on file +Y, the pilot's LEFT (.L bones) on file
  -X: GlobalSettings UpAxis +Z, FrontAxis +Y, CoordAxis -X. Unity imports such a file as
  Unity (x, y, z) = (file x, file z, file y): nose -> +Z, up -> +Y, .L -> -X, and the mesh node's
  own local frame stays the FILE frame (the prefab's BoxCollider on the Squirrel's mesh node is
  authored as x=width 4.12, y=length 3.11, z=thickness 0.59 - file axes). Blender's importer
  turns that file into a scene with the nose on Blender -Y and .L on Blender +X (a 180-degree
  turn about Z), which is the frame every spec here models in.
  So the exporter must UNDO that same turn: axis_forward='-Y', axis_up='Z' maps Blender -Y ->
  file +Y and Blender +X -> file -X and writes the same GlobalSettings. The DEFAULT export
  (axis_forward='-Z', axis_up='Y') would instead write a Y-up file with the conversion as a
  -90 X rotation on the root nodes: Unity would still fly it nose-forward, but the mesh node's
  local frame would differ from the shipped one, so every collider and jet offset authored in
  node/bone space on the prefab would be rotated. Same frame, same numbers, no prefab edits.
  `export_and_verify` reads the written file back and asserts the GlobalSettings axes and the
  Blender round trip.

THE SCALE STORY: an FBX's UnitScaleFactor is "centimetres per file unit": 100 means the file
is in METRES, 1 means centimetres. Unity's importer (useFileScale 1, globalScale 1 on every
vessel .meta) scales a file by UnitScaleFactor/100, so a metre file imports 1:1 and a cm file
at 0.01 (SparrowModel4 is a cm file with globalScale 100 in its meta - the same product).
Blender's 'FBX_SCALE_UNITS' mode writes UnitScaleFactor 100 and leaves the vertices in metres,
which is what the artist files carry (the Squirrel's raw vertex bounds ARE its 4.5 x 6.45 unit
hull, the one VESSEL_TAIL_AND_JETS.md's -17 camera is tuned around). 'FBX_SCALE_NONE' would
instead bake x100 into the vertices and write UnitScaleFactor 1 - a cm file that imports a
hundred times too big next to a meta that says globalScale 1. So: 1 Blender unit = 1 Unity unit,
and the verify step reads the file's UnitScaleFactor and converts before it compares bounds.

THE SHAPE-CURVE STORY: when Blender bakes "all actions" it also bakes the shape keys' values
into every take as 'Key|...' curves on the BlendShapeChannel deformers. `VesselAnimation`
defends against that in LateUpdate, but the contract (CONTRACT.md section 7) wants a model that
carries NONE, and the Squirrel's shipped file carries none. `strip_shape_curves` deletes those
AnimationCurveNode + AnimationCurve objects, their Connections and their Definitions counts
with the repo's codec, which is the surgery technique of asset-surgery SKILL.md section 4.8c.

THREE READERS, because a writer agrees with itself by construction:
  1. Tools/Build/fbx_binary.py + fbx_probe (ours)   - structure, names, shapes, takes, slots
  2. assimp info (/usr/bin/assimp)                  - an independent FBX parser
  3. a Blender re-import in a fresh scene            - bones, shape keys, actions, materials
"""

import os
import subprocess
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, HERE)
import fbx_binary as F  # noqa: E402
import fbx_probe as P  # noqa: E402


# The artist files were exported two different ways, and a replacement must match ITS file's
# way so the prefab's node-local colliders and bone-space jet offsets keep their frames:
#   "zup"  (Squirrel, Serpent, Sparrow): a Z-up file, nose on file +Y, UnitScaleFactor 100 (metres),
#          root nodes at identity, pilot's left on file -X. Blender frame -> file is a 180-degree
#          turn about Z, baked into the mesh data.
#   "yup"  (Dolphin rig): Blender's DEFAULT export - a Y-up file, nose on file +Z, UnitScaleFactor 1
#          (cm), root nodes R(-90,0,0) S(100,100,100), vertices in metres under that scale.
# A spec names its preset; verify() checks the written GlobalSettings against it.
PRESETS = {
    "zup": dict(axis_forward="-Y", axis_up="Z", apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                expect=dict(UnitScaleFactor=100.0, UpAxis=2, UpAxisSign=1, FrontAxis=1, FrontAxisSign=1, CoordAxis=0, CoordAxisSign=-1)),
    "yup": dict(axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_NONE", bake_space_transform=False,
                expect=dict(UnitScaleFactor=1.0, UpAxis=1, UpAxisSign=1, FrontAxis=2, FrontAxisSign=1, CoordAxis=0, CoordAxisSign=1)),
    # the Serpent: a Z-up file written with Blender's IDENTITY axes (axis_forward='Y'), so the
    # file frame IS the Blender frame and the exporter labels the front axis -Y. The importer
    # applies no turn, so this vessel is modelled with its nose on Blender +Y (the head end).
    # the Manta rig: Blender's default axes and FBX_SCALE_NONE, but with the space transform
    # BAKED into the mesh (mesh node identity, vertices in cm, Y-up) while the armature (which
    # Blender cannot bake) keeps R(-90,0,0) S(100).
    "yup_baked": dict(axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_NONE", bake_space_transform=True,
                      expect=dict(UnitScaleFactor=1.0, UpAxis=1, UpAxisSign=1, FrontAxis=2, FrontAxisSign=1, CoordAxis=0, CoordAxisSign=1)),
    "zup_identity": dict(axis_forward="Y", axis_up="Z", apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                         expect=dict(UnitScaleFactor=100.0, UpAxis=2, UpAxisSign=1, FrontAxis=1, FrontAxisSign=-1, CoordAxis=0, CoordAxisSign=1)),
}

EXPORT_KWARGS = dict(
    use_selection=True,
    apply_scale_options="FBX_SCALE_UNITS",  # overridden by the preset
    global_scale=1.0,
    axis_forward="-Y",                      # overridden by the preset
    axis_up="Z",
    bake_space_transform=True,              # overridden by the preset
    object_types={"ARMATURE", "MESH", "EMPTY"},
    use_mesh_modifiers=False,               # everything is applied already; the Armature modifier must NOT be baked
    mesh_smooth_type="OFF",                 # write split normals, which is what Unity imports (normalImportMode 0)
    use_tspace=False,
    use_custom_props=False,
    add_leaf_bones=False,                   # a leaf bone per tip is a node the code never asked for
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,         # keep parent/hinge bones that skin nothing (the Squirrel's WholeWing)
    armature_nodetype="NULL",
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,         # one AnimationStack per action = one take per pose
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,          # keep every key: a pose take must key every bone
    path_mode="AUTO",
    embed_textures=False,
    batch_mode="OFF",
)


def export_fbx(path, objects, preset="zup", **overrides):
    """Export exactly `objects` (armature + mesh) to `path` with the recipe of `preset`."""
    kw = dict(EXPORT_KWARGS)
    kw.update({k: v for k, v in PRESETS[preset].items() if k != "expect"})
    kw.update(overrides)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
        if o.type == "ARMATURE":
            # THE DIRTY-POSE LEAK (measured 2026-10-17): when "bake all actions" runs with a pose
            # still set on the armature, the exporter composes that live pose into every baked
            # take - the last pose authored showed up added onto every other take (speedup
            # carried speeddown's deflection, speeddown itself read as rest). Clearing the pose
            # and unassigning the action before export makes every take bake from its own keys.
            if o.animation_data:
                o.animation_data.action = None
            for pb in o.pose.bones:
                pb.rotation_mode = "XYZ"
                pb.rotation_euler = (0.0, 0.0, 0.0)
                pb.location = (0.0, 0.0, 0.0)
                pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.context.view_layer.update()
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, **kw)
    return path


# ------------------------------------------------------------------ post-export strip

def strip_shape_curves(path):
    """
    Delete every animation curve that targets a blend-shape channel, in place. Returns the
    number of objects removed. Safe on a file that has none (returns 0, rewrites identically).
    """
    nodes, version, footer = F.read(path)
    top = {n.name: n for n in nodes}
    objects, connections, definitions = top["Objects"], top["Connections"], top.get("Definitions")
    by_id = {}
    for o in objects.children:
        if o.props:
            by_id[o.props[0][1]] = o
    # OP links: src -> (dst, prop)
    conns = []
    for c in connections.children:
        if c.name == "C" and len(c.props) >= 3:
            conns.append((P._s(c.props[0][1]), c.props[1][1], c.props[2][1], c))
    deformer_ids = {i for i, o in by_id.items() if o.name == "Deformer" and P.subclass_of(o) == "BlendShapeChannel"}
    doomed = set()
    for kind, src, dst, _c in conns:
        if kind == "OP" and dst in deformer_ids and src in by_id and by_id[src].name == "AnimationCurveNode":
            doomed.add(src)
    # the curves hanging off those nodes
    for kind, src, dst, _c in conns:
        if kind == "OP" and dst in doomed and src in by_id and by_id[src].name == "AnimationCurve":
            doomed.add(src)
    if not doomed:
        return 0
    removed_by_type = {}
    for i in doomed:
        removed_by_type[by_id[i].name] = removed_by_type.get(by_id[i].name, 0) + 1
    objects.children = [o for o in objects.children if not (o.props and o.props[0][1] in doomed)]
    connections.children = [c for c in connections.children
                            if not (c.name == "C" and len(c.props) >= 3 and (c.props[1][1] in doomed or c.props[2][1] in doomed))]
    if definitions is not None:
        for ot in definitions.find("ObjectType"):
            tname = P._s(ot.props[0][1]) if ot.props else ""
            if tname in removed_by_type:
                cnt = ot.first("Count")
                if cnt is not None and cnt.props:
                    t, v = cnt.props[0]
                    cnt.props[0] = (t, max(0, v - removed_by_type[tname]))
    # assert no connection references a missing object
    ids = set(by_id) - doomed
    for c in connections.children:
        if c.name == "C" and len(c.props) >= 3:
            for x in (c.props[1][1], c.props[2][1]):
                if x != 0 and x not in ids:
                    raise RuntimeError("strip left a dangling connection to %s" % x)
    F.write(path, nodes, version, footer)
    return len(doomed)


# -------------------------------------------------------------------- verification

class VerifyError(AssertionError):
    pass


def assimp_info(path):
    try:
        out = subprocess.run(["assimp", "info", path], capture_output=True, text=True, timeout=120)
    except FileNotFoundError:
        return None
    return out.stdout + out.stderr


def blender_reimport_report(path):
    """
    Re-import the file in a SEPARATE python process (a fresh Blender state, not this scene)
    and report objects, bones, shape keys, actions and materials.
    """
    code = r'''
import bpy, sys, json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-1])
rep = {"objects": [], "actions": [a.name for a in bpy.data.actions]}
for o in bpy.data.objects:
    d = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
         "loc": list(o.location), "rot_deg": [round(__import__("math").degrees(a), 3) for a in o.rotation_euler], "scale": list(o.scale)}
    if o.type == "ARMATURE":
        d["bones"] = [b.name for b in o.data.bones]
    if o.type == "MESH":
        d["materials"] = [m.name if m else None for m in o.data.materials]
        d["shapes"] = [k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else []
        d["verts"] = len(o.data.vertices); d["polys"] = len(o.data.polygons)
        d["vgroups"] = [g.name for g in o.vertex_groups]
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        d["bounds"] = [[min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]]
        area = {}
        for poly in o.data.polygons:
            area[poly.material_index] = area.get(poly.material_index, 0.0) + poly.area
        total = sum(area.values()) or 1.0
        d["material_area_share"] = {str(k): v / total for k, v in area.items()}
    rep["objects"].append(d)
print("REPORT_JSON " + json.dumps(rep))
'''
    out = subprocess.run([sys.executable, "-c", code, path], capture_output=True, text=True, timeout=600)
    for line in out.stdout.splitlines():
        if line.startswith("REPORT_JSON "):
            import json
            return json.loads(line[len("REPORT_JSON "):])
    raise VerifyError("blender re-import produced no report:\n%s\n%s" % (out.stdout[-2000:], out.stderr[-2000:]))


def verify(path, spec):
    """
    Check the written FBX against the spec's contract. `spec` is a dict with:
      armature: name; bones: [names]; takes: [take names as '<Armature>|<action>'];
      shapes: [names]; slots: [material names]; mesh_node: name;
      bounds_blender: ((xmin,ymin,zmin),(xmax,ymax,zmax)) the built mesh's Blender-frame world
      bounds, which the re-import must reproduce within `bounds_tol` (relative);
      min_travel: minimum travel fraction per shape (default 0.02).
    Returns the probe + reports; raises VerifyError with every failure listed.
    """
    failures = []
    r = P.probe(path, want_takes=True)
    names = {m["name"]: m for m in r["models"]}
    # nodes
    for b in spec.get("bones", []):
        if b not in names or names[b]["type"] != "LimbNode":
            failures.append("bone %r missing from the file" % b)
    if spec.get("armature") and spec["armature"] not in names:
        failures.append("armature node %r missing" % spec["armature"])
    if spec.get("mesh_node") and spec["mesh_node"] not in names:
        failures.append("mesh node %r missing" % spec["mesh_node"])
    # meshes
    if len(r["meshes"]) != spec.get("mesh_count", 1):
        failures.append("expected %d mesh(es), file has %d" % (spec.get("mesh_count", 1), len(r["meshes"])))
    mesh = r["meshes"][0] if r["meshes"] else None
    if mesh:
        if mesh["skin"] is None and spec.get("bones"):
            failures.append("mesh is not skinned")
        want_slots = spec.get("slots", [])
        got_counts = mesh["material_poly_counts"]
        for i, nm in enumerate(want_slots):
            if got_counts.get(i, 0) == 0:
                failures.append("material slot %d (%s) has no polygons - Unity would drop/shift it" % (i, nm))
        shape_names = {s["name"]: s for s in mesh["shapes"]}
        for s in spec.get("shapes", []):
            if s not in shape_names:
                failures.append("shape %r missing" % s)
            elif shape_names[s]["travel_fraction"] < spec.get("min_travel", 0.02):
                failures.append("shape %r travels only %.2f%% of the diagonal (min %.0f%%)" % (
                    s, 100 * shape_names[s]["travel_fraction"], 100 * spec.get("min_travel", 0.02)))
        if not mesh["uv"]:
            failures.append("mesh has no UV layer")
        if not mesh["normals"]:
            failures.append("mesh has no normals layer")
    # materials in order
    mat_names = [m["name"] for m in r["materials"]]
    for nm in spec.get("slots", []):
        if nm not in mat_names:
            failures.append("material %r missing (have %s)" % (nm, mat_names))
    # takes
    take_names = [t["name"] for t in r["takes"]]
    for t in spec.get("takes", []):
        if t not in take_names:
            failures.append("take %r missing (have %s)" % (t, take_names))
    # what the .meta pins: every takeName the Animator's clips are cut from, every material the prefab
    # remaps by name, must exist in the file or the clip imports empty / the slot wears the importer's material
    for t in spec.get("meta_takes", []):
        if t not in take_names:
            failures.append("the .meta pins clip takeName %r and the file has no such take (have %s)" % (t, take_names))
    for nm in spec.get("meta_materials", []):
        if nm not in mat_names:
            failures.append("the .meta remaps material %r and the file has no such material (have %s)" % (nm, mat_names))
    for t in r["takes"]:
        if t["blend_shape_curves"]:
            failures.append("take %r carries %d blend-shape curves (strip them)" % (t["name"], t["blend_shape_curves"]))
        if spec.get("pose_takes") and t["name"] in spec["pose_takes"] and "idle" not in t["name"].lower() and t["nodes_that_move"] == 0:
            failures.append("pose take %r deflects nothing" % t["name"])
    # scale / axes
    s = r["settings"]
    preset = spec.get("preset", "zup")
    expect = PRESETS[preset]["expect"]
    for k, v in expect.items():
        if s.get(k) != v:
            failures.append("GlobalSettings %s = %s, preset %s wants %s" % (k, s.get(k), preset, v))
    # node TRS per preset: the mesh node's LOCAL frame is where the prefab's colliders are authored,
    # and the round-trip bounds check below cannot see it (Blender undoes its own export), so the
    # Lcl rotation / scaling of the mesh node (and, for the cm rigs, the armature) is asserted here.
    NODE_TRS = {  # preset -> (mesh rot, mesh scale, armature rot, armature scale); None = per-file, not asserted
        "zup": ((0, 0, 0), 1.0, None, None),
        "zup_identity": ((0, 0, 0), 1.0, None, None),
        "yup": ((-90, 0, 0), 100.0, (-90, 0, 0), 100.0),
        "yup_baked": ((0, 0, 0), 1.0, (-90, 0, 0), 100.0),
    }
    def _rot_close(a, b):
        return all(abs(((x - y) + 180) % 360 - 180) < 0.5 for x, y in zip(a, b))
    for node_key, want_rot, want_scale in ((spec.get("mesh_node"), NODE_TRS[preset][0], NODE_TRS[preset][1]),
                                            (spec.get("armature"), NODE_TRS[preset][2], NODE_TRS[preset][3])):
        if not node_key or node_key not in names or want_rot is None:
            continue
        node = names[node_key]
        rot, sc = node["rotation"], node["scaling"]
        if node.get("pre_rotation") and any(abs(v) > 1e-3 for v in node["pre_rotation"]):
            failures.append("node %r carries a PreRotation %s; the pipeline writes none" % (node_key, node["pre_rotation"]))
        if not _rot_close(rot, want_rot) or any(abs(v - want_scale) > 1e-3 * want_scale for v in sc):
            failures.append("node %r TRS R=%s S=%s; preset %s wants R=%s S=%s (the frame the prefab's colliders were authored in)" % (
                node_key, tuple(round(v, 2) for v in rot), tuple(round(v, 3) for v in sc), preset, want_rot, want_scale))
    # left/right symmetry on the file's own axes (x is lateral in every preset here)
    if mesh and spec.get("symmetric", True):
        lo, hi = mesh["bounds"]
        width = hi[0] - lo[0]
        if width > 1e-6 and abs(lo[0] + hi[0]) > 0.03 * width:
            failures.append("hull is not left/right symmetric: x %.3f .. %.3f (set symmetric=False in the spec for a deliberately asymmetric hull)" % (lo[0], hi[0]))
    # what each authored take must move (bones named in the pose), and that an unnamed take stays at rest
    for take_name, bones_named in spec.get("expected_moving", {}).items():
        t = next((t for t in r["takes"] if t["name"] == take_name or t["name"].endswith("|" + take_name)), None)
        if t is None:
            continue
        moving = set(t["moving_detail"].keys())
        if not bones_named:
            if t["nodes_that_move"] != 0:
                failures.append("take %r was authored at rest but moves %d node(s): %s" % (take_name, t["nodes_that_move"], sorted(moving)[:6]))
        else:
            missing = [b for b in bones_named if b not in moving]
            if missing:
                failures.append("take %r was authored to move %s but the file does not move %s" % (take_name, bones_named, missing))
    for nm, want in (spec.get("material_colours_ref") or {}).items():
        got = next((m.get("diffuse") for m in r["materials"] if m["name"] == nm), None)
        if got is None or want is None:
            continue
        if max(abs(a - b) for a, b in zip(got[:3], want[:3])) > 0.05:
            failures.append("material %r DiffuseColor %s differs from the artist file's %s (Unity generates this material from the file; set material_colours_ref in the spec if the change is meant)" % (
                nm, tuple(round(v, 3) for v in got[:3]), tuple(round(v, 3) for v in want[:3])))
    if spec.get("slot_order_ref") is not None and mesh:
        got_order = [m["name"] for m in r["materials"]]
        ref = list(spec["slot_order_ref"])
        if got_order != ref and tuple(spec.get("slot_reorder_from", ())) != tuple(ref):
            failures.append("material slot order %s differs from the replaced file's %s; declare slot_reorder_from=%s in the spec if the reorder is a deliberate migration" % (got_order, ref, ref))
    # bounds are checked on the Blender RE-IMPORT in world space (below): a file that round-trips
    # to the Blender-frame bounds the spec built is a file whose axis conversion is self-consistent
    # independent readers
    ai = assimp_info(path)
    if ai is None:
        failures.append("assimp not available")
    elif "Animations:" not in ai or "Bones:" not in ai:
        failures.append("assimp could not read the file:\n" + ai[-800:])
    rep = blender_reimport_report(path)
    arm = [o for o in rep["objects"] if o["type"] == "ARMATURE"]
    if spec.get("bones") and (not arm or set(spec["bones"]) - set(arm[0]["bones"])):
        failures.append("blender re-import lacks bones %s" % (set(spec.get("bones", [])) - set(arm[0]["bones"] if arm else [])))
    meshes = [o for o in rep["objects"] if o["type"] == "MESH"]
    if meshes and spec.get("bounds_blender"):
        want = spec["bounds_blender"]
        got = meshes[0]["bounds"]
        tol = spec.get("bounds_tol", 0.05)
        k = 1.0  # Blender re-imports into metres whatever the file's unit, so bounds compare 1:1
        for ax in range(3):
            for side in (0, 1):
                g = got[side][ax] * k
                if abs(g - want[side][ax]) > tol * max(1.0, abs(want[side][ax])):
                    failures.append("round-trip bounds mismatch axis %d side %d: re-import %.3f, built %.3f" % (ax, side, g, want[side][ax]))
                    break
    if meshes and spec.get("slots"):
        # every slot owns a readable SURFACE: a canopy of one wide quad is glass, forty slivers are not,
        # so the share is measured by area on the re-import (polygon counts are a poor proxy)
        min_share = spec.get("min_slot_share", 0.003)
        shares = meshes[0].get("material_area_share", {})
        for i, nm in enumerate(spec["slots"]):
            share = shares.get(str(i), 0.0)
            if share < min_share:
                failures.append("material slot %d (%s) covers %.2f%% of the surface (under %.1f%%): the glass / engine / band would not read at the chase camera; enlarge it or set min_slot_share in the spec" % (
                    i, nm, 100 * share, 100 * min_share))
    if meshes and spec.get("size_ref_blender"):
        # size against the ARTIST file as drawn (its bind mesh deformed by its parked pose): the
        # cameras, colliders and jets were tuned to that hull; the written file has no parked pose,
        # so its raw bounds are its drawn bounds
        ref_lo, ref_hi = spec["size_ref_blender"]
        lo, hi = meshes[0]["bounds"]
        tol = spec.get("size_tol", 0.10)
        for ax in range(3):
            ext_ref, ext = ref_hi[ax] - ref_lo[ax], hi[ax] - lo[ax]
            if ext_ref > 1e-6 and abs(ext - ext_ref) > tol * ext_ref:
                failures.append("size axis %d (Blender frame): extent %.3f vs the artist file's drawn %.3f (more than %.0f%% off; set size_ref_blender / size_tol in the spec if the change is meant)" % (
                    ax, ext, ext_ref, 100 * tol))
    if meshes:
        for s in spec.get("shapes", []):
            if s not in meshes[0]["shapes"]:
                failures.append("blender re-import lacks shape %s" % s)
        if spec.get("slots") and meshes[0]["materials"][: len(spec["slots"])] != list(spec["slots"]):
            failures.append("blender re-import material order %s != %s" % (meshes[0]["materials"], list(spec["slots"])))
    for t in spec.get("takes", []):
        # Blender re-imports a stack 'Arm|take' as action 'Arm|Arm|take'
        if not any(a.endswith("|" + t.split("|")[-1]) for a in rep["actions"]):
            failures.append("blender re-import lacks action for take %s" % t)
    if failures:
        raise VerifyError("\n".join("  - " + f for f in failures))
    return {"probe": r, "assimp": ai, "reimport": rep}


_POSED_BOUNDS_READER = r'''
import bpy, sys, json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-1], ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=False)
dg = bpy.context.evaluated_depsgraph_get()
lo = [1e9] * 3; hi = [-1e9] * 3
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    for v in me.vertices:
        p = o.matrix_world @ v.co
        for i in range(3):
            lo[i] = min(lo[i], p[i]); hi[i] = max(hi[i], p[i])
    ev.to_mesh_clear()
print("POSED_BOUNDS " + json.dumps([lo, hi]))
'''


def blender_posed_bounds(path):
    """
    The world-space bounds of a file's mesh AS DRAWN (armature modifier applied, i.e. the bind
    mesh deformed by the parked pose), in Blender's import frame. For a file with no parked
    pose this equals the raw bounds; for the artist Squirrel the posed tail sits 0.3 higher.
    """
    out = subprocess.run([sys.executable, "-c", _POSED_BOUNDS_READER, path], capture_output=True, text=True, timeout=600)
    for line in out.stdout.splitlines():
        if line.startswith("POSED_BOUNDS "):
            return json.loads(line[len("POSED_BOUNDS "):])
    raise RuntimeError("blender could not read %s:\n%s" % (path, (out.stdout + out.stderr)[-1200:]))


def export_and_verify(path, objects, spec, strip=True):
    """
    Export to a TEMP file beside `path`, strip, verify, and only then move it over `path`.
    A failed verify deletes the temp file and leaves whatever was at `path` untouched, so an
    in-place write over a live asset can never leave an unverified file on disk (the first
    version wrote first and verified second; review finding 2026-10-17).
    """
    tmp = path + ".verifying.fbx"
    try:
        export_fbx(tmp, objects, preset=spec.get("preset", "zup"))
        removed = strip_shape_curves(tmp) if strip else 0
        rep = verify(tmp, spec)
    except Exception:
        if os.path.exists(tmp):
            os.remove(tmp)
        raise
    os.replace(tmp, path)
    rep["stripped"] = removed
    rep["probe"] = rep.get("probe") or None
    return rep
