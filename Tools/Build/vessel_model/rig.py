"""
rig.py - the armature, and the skin that binds the hull to it.

The bone NAMES are the contract. Code puppetry resolves parts by name (`VesselAnimation.
ResolvePart`: 'fuse', 'wing.l', 'jetT.r', 'jaw.u' ...), and an Animator binds a clip's curves
by the bone's transform PATH under the animator root, so a bone that is renamed is a limb that
stops moving. A vessel spec therefore lists its bones with the exact names its driver expects,
and this module builds that skeleton and nothing more.

Skinning is RIGID BY PART by default: every vertex of the part named `wing.l` weighs 1.0 on the
bone `wing.l`. That is what a mechanical hull wants (a hinged wing does not stretch its root),
it is what the shipped Squirrel does (its 42 clusters skin disjoint vertex sets), and it keeps
the element morphs honest because a morph then never fights a skin blend. `blend_root()` is
there for the few organic joints (a serpent neck) that should bend.
"""

import math

import bpy
from mathutils import Vector, Matrix


class BoneSpec:
    """One bone: name, head, tail (Blender frame), parent name, roll (deg)."""

    __slots__ = ("name", "head", "tail", "parent", "roll", "connect")

    def __init__(self, name, head, tail, parent=None, roll=0.0, connect=False):
        self.name = name
        self.head = Vector(head)
        self.tail = Vector(tail)
        self.parent = parent
        self.roll = roll
        self.connect = connect


def build_armature(name, bones, display="OCTAHEDRAL"):
    """
    Create an armature object named `name` with the given BoneSpecs, parented in order.
    Returns the armature object (in OBJECT mode, at the origin, identity rotation - the
    exporter needs it clean; the shipped Squirrel's (71.89, -180, 0) armature rotation is a
    scar of an artist's scene, not a convention).
    """
    arm = bpy.data.armatures.new(name)
    arm.display_type = display
    obj = bpy.data.objects.new(name, arm)
    bpy.context.collection.objects.link(obj)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    made = {}
    for b in bones:
        eb = arm.edit_bones.new(b.name)
        eb.head = b.head
        eb.tail = b.tail if (b.tail - b.head).length > 1e-6 else b.head + Vector((0, 0, 0.1))
        eb.roll = math.radians(b.roll)
        made[b.name] = eb
    for b in bones:
        if b.parent:
            if b.parent not in made:
                raise KeyError("bone %s names parent %s which is not in the spec" % (b.name, b.parent))
            made[b.name].parent = made[b.parent]
            made[b.name].use_connect = b.connect
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def skin_rigid(mesh_obj, armature_obj, group_to_bone=None, default_bone=None, parent_to_armature=False):
    """
    Bind `mesh_obj` to `armature_obj`: an Armature modifier plus vertex groups renamed to
    their bones. `group_to_bone` maps part vertex-group names to bone names (identity by
    default); groups that map to no bone are folded into `default_bone` (usually the root
    'fuse') so no vertex is unweighted - Unity treats an unweighted vertex as bound to bone 0
    at weight 0, which leaves it frozen at bind pose while the hull turns.
    """
    group_to_bone = dict(group_to_bone or {})
    bone_names = {b.name for b in armature_obj.data.bones}
    for vg in list(mesh_obj.vertex_groups):
        target = group_to_bone.get(vg.name, vg.name)
        if target in bone_names:
            if target != vg.name:
                existing = mesh_obj.vertex_groups.get(target)
                if existing:
                    _merge_group(mesh_obj, vg, existing)
                    mesh_obj.vertex_groups.remove(vg)
                    continue
                vg.name = target
        else:
            if default_bone is None or default_bone not in bone_names:
                raise KeyError("vertex group %s has no bone and no default_bone" % vg.name)
            existing = mesh_obj.vertex_groups.get(default_bone)
            if existing is None:
                existing = mesh_obj.vertex_groups.new(name=default_bone)
            _merge_group(mesh_obj, vg, existing)
            mesh_obj.vertex_groups.remove(vg)
    # any vertex with no group at all -> default bone
    if default_bone and default_bone in bone_names:
        dvg = mesh_obj.vertex_groups.get(default_bone) or mesh_obj.vertex_groups.new(name=default_bone)
        orphan = [v.index for v in mesh_obj.data.vertices if not v.groups]
        if orphan:
            dvg.add(orphan, 1.0, "REPLACE")
    # The shipped artist files keep the mesh node and the armature as SIBLINGS at the top of the
    # FBX (the Skin deformer binds them, not the hierarchy); Unity's imported prefab then has the
    # same two top-level children the prefab's stripped references were made against. Keep that
    # layout unless a spec asks otherwise.
    mesh_obj.parent = armature_obj if parent_to_armature else None
    mod = mesh_obj.modifiers.get("Armature") or mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = armature_obj
    mod.use_vertex_groups = True
    return mesh_obj


def _merge_group(obj, src, dst):
    idx = src.index
    for v in obj.data.vertices:
        for g in v.groups:
            if g.group == idx:
                dst.add([v.index], g.weight, "REPLACE")
                break


def blend_root(mesh_obj, part_group, bone_a, bone_b, axis_point, axis_dir, falloff):
    """
    Soften a rigid joint: vertices of `part_group` within `falloff` of the plane through
    `axis_point` with normal `axis_dir` are shared between `bone_a` (the part's own bone) and
    `bone_b` (its parent) by distance. For necks and tails that should bend, not hinge.
    """
    me = mesh_obj.data
    ga = mesh_obj.vertex_groups.get(bone_a)
    gb = mesh_obj.vertex_groups.get(bone_b) or mesh_obj.vertex_groups.new(name=bone_b)
    pg = mesh_obj.vertex_groups.get(part_group)
    if not (ga and pg):
        return mesh_obj
    p0 = Vector(axis_point)
    n = Vector(axis_dir).normalized()
    for v in me.vertices:
        in_part = any(g.group == pg.index for g in v.groups)
        if not in_part:
            continue
        d = (v.co - p0).dot(n)
        if d < falloff:
            t = max(0.0, min(1.0, d / falloff))  # 0 at the joint plane -> all parent
            ga.add([v.index], t, "REPLACE")
            gb.add([v.index], 1.0 - t, "REPLACE")
    return mesh_obj


def bone_matrix_world(armature_obj, bone_name):
    b = armature_obj.data.bones[bone_name]
    return armature_obj.matrix_world @ b.matrix_local


def report(armature_obj, mesh_obj):
    """Bone list with skinned vertex counts, the same numbers fbx_probe reads back."""
    counts = {}
    for vg in mesh_obj.vertex_groups:
        counts[vg.name] = 0
    for v in mesh_obj.data.vertices:
        for g in v.groups:
            if g.weight > 0:
                name = mesh_obj.vertex_groups[g.group].name
                counts[name] = counts.get(name, 0) + 1
    rows = []
    for b in armature_obj.data.bones:
        rows.append((b.name, b.parent.name if b.parent else None, counts.get(b.name, 0)))
    return rows


# ------------------------------------------------------------------- reuse a shipped rig

def import_shipped_armature(fbx_path, keep_actions=False):
    """
    Bring the SHIPPED model's armature into the current scene and drop everything else it
    carries (its mesh, its materials, its actions unless keep_actions). The point: a prefab
    hangs jets, colliders and FX on bones by NAME with offsets in BONE SPACE, and the editor
    tools that swap rigs were written against these exact rest poses - so a replacement that
    reuses the artist's skeleton as-is (names, order, hierarchy, rest transforms, the armature
    object's own transform) keeps every one of those mounts landing where it did.
    `rest_compare.py` proves it on the written file.

    Returns the armature object, in the Blender frame the importer put it in (nose -Y), which
    is the frame every spec models in.
    """
    before = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=fbx_path, ignore_leaf_bones=False, automatic_bone_orientation=False,
                             use_anim=keep_actions)
    new_objs = [o for o in bpy.data.objects if o not in before]
    arm = None
    for o in new_objs:
        if o.type == "ARMATURE":
            arm = o
    if arm is None:
        raise RuntimeError("no armature in %s" % fbx_path)
    for o in new_objs:
        if o is not arm:
            bpy.data.objects.remove(o, do_unlink=True)
    if not keep_actions:
        for a in list(bpy.data.actions):
            if a not in before_actions:
                bpy.data.actions.remove(a)
    # drop the importer's leftover materials/meshes so slot names stay ours
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    arm.select_set(False)
    return arm


def bone_world_head(arm, name):
    b = arm.data.bones[name]
    return arm.matrix_world @ b.head_local


def bone_world_axis(arm, name):
    b = arm.data.bones[name]
    return (arm.matrix_world.to_3x3() @ (b.tail_local - b.head_local)).normalized()
