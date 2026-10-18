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


def skin_rigid(mesh_obj, armature_obj, group_to_bone=None, default_bone=None, parent_to_armature=False, bake_park=True):
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
    # bake the artist's parked pose into the rest ONLY when that pose is what the game draws (the Squirrel's:
    # the live prefab plays it; the Urchin's 1.75: the live prefab authors it). A park nobody has seen in the
    # game (the Rhino rig's 70-degree wing fold) is a WIP pose: bake_park=False keeps the BIND as the rest
    baked = bake_parked_pose(mesh_obj, armature_obj) if bake_park else []
    if baked:
        print("  [rig] baked the artist's parked pose into the rest on %d bone(s): %s%s" % (
            len(baked), ", ".join(baked[:5]), " ..." if len(baked) > 5 else ""))
    return mesh_obj


def parked_pose_bones(arm):
    """Bones whose imported pose (matrix_basis) is not identity: the artist file's node transforms differ from its bind pose there."""
    out = []
    for pb in arm.pose.bones:
        mb = pb.matrix_basis
        if math.degrees(mb.to_quaternion().angle) > 0.01 or mb.to_translation().length > 1e-5 or any(abs(x - 1.0) > 1e-4 for x in mb.to_scale()):
            out.append(pb.name)
    return out


def bake_parked_pose(mesh_obj, arm, deform_mesh=False):
    """
    Make the armature's REST equal the shipped file's node transforms, and move the mesh with it.

    Blender's importer derives a bone's rest from the skin's bind matrix and parks the difference
    to the file's Lcl node transform as a POSE (`pose_bone.matrix_basis`). The artist Squirrel
    parks 10.78 degrees on 27 of 42 bones, the Urchin rig a 1.75 scale and 20 degrees on 16 of
    38, the Rhino rig 70 degrees and 3.3 m on 9 of 12. Unity builds the bone Transforms (where
    the prefab's jets, colliders and FX hang with bone-space offsets) from the Lcl transforms,
    and the game shows the bind mesh DEFORMED by that parked pose. Exporting with the pose
    cleared (export_fbx must, or it leaks into every take) therefore moved every one of those
    bones in Unity by the parked amount (assimp node-chain compare, 2026-10-17 review). So:
    deform the freshly skinned mesh by the parked pose (what the player saw), then apply the
    pose as the new rest; the written Lcl equals the shipped Lcl, the bind equals the Lcl, and
    the hull sits where the artist's hull sat in the game. A rest SCALE cannot exist on a
    Blender bone, so the Urchin rig's 1.75 node scale is baked into its geometry and its bone
    lengths and written as 1.0 (recorded in the dossier; the rig swap re-fits its jets).
    `deform_mesh`: also move the mesh by the parked pose. OFF by default, because a spec is
    modelled against the artist's hull AS DRAWN (the contact sheets render the posed file and
    `part_map.py` prints posed bounds), so the geometry already sits where the posed bones put
    it; deforming it again would apply the pose twice (the first baked Squirrel raised its fans
    45 degrees). Turn it on only for a mesh placed from BIND-pose measurements.
    Returns the list of bones that were baked (empty = nothing to do).
    """
    parked = parked_pose_bones(arm)
    if not parked:
        return []
    me = mesh_obj.data
    A = arm.matrix_world
    A_inv = A.inverted()
    M = mesh_obj.matrix_world
    M_inv = M.inverted()
    group_names = {g.index: g.name for g in mesh_obj.vertex_groups}
    per_bone = {}
    for pb in arm.pose.bones:
        b = arm.data.bones[pb.name]
        per_bone[pb.name] = A @ pb.matrix @ b.matrix_local.inverted() @ A_inv
    if deform_mesh:
        def posed(v, co):
            p = M @ co
            acc = Vector((0.0, 0.0, 0.0))
            total = 0.0
            for g in v.groups:
                name = group_names.get(g.group)
                if name in per_bone and g.weight > 0:
                    acc += (per_bone[name] @ p) * g.weight
                    total += g.weight
            return M_inv @ (acc / total) if total > 0 else co
        # every shape key rides along (the artist's morphs are deltas on the bind mesh; the same
        # per-vertex rigid transform carries them to the drawn pose)
        if me.shape_keys:
            for kb in me.shape_keys.key_blocks:
                for v in me.vertices:
                    kb.data[v.index].co = posed(v, kb.data[v.index].co)
        for v in me.vertices:
            v.co = posed(v, v.co)
        me.update()
    # the pose becomes the rest
    prev_active = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.select_set(False)
    bpy.context.view_layer.objects.active = prev_active
    return parked


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
    import forms
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
    # the shipped materials' colours, by name: Unity generates the file's materials from the FBX
    # (materialImportMode 2) and remaps only the slots the .meta names, so the un-remapped slots
    # (the Engine on three vessels) must carry the shipped colour in the FILE, not just on the sheet
    for o in new_objs:
        if o.type == "MESH":
            for m in o.data.materials:
                if m is not None:
                    forms.SLOT_COLOURS[m.name] = tuple(m.diffuse_color[:3])
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
        # the importer parks the node-vs-bind difference in matrix_basis through rotation_quaternion;
        # switching the mode alone would read rotation_euler (0,0,0) and silently drop the parked pose
        mb = pb.matrix_basis.copy()
        pb.rotation_mode = "XYZ"
        pb.matrix_basis = mb
    # a CONNECTED bone (head pinned to its parent's tail) ignores pose location, so a pose that
    # slides a part (the Rhino's pods on speedup) silently writes no translation; FBX has no notion
    # of connection and the heads/tails stay where they are, so every bone is disconnected
    prev_active = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.objects.active = prev_active
    arm.select_set(False)
    return arm


def import_shipped_model(fbx_path):
    """
    Bring the SHIPPED model in whole - its armature AND its mesh (vertex groups, shape keys,
    material slots, the armature modifier) - as the base a spec builds ON. This is the
    artist-hull path (LESSONS 38): the artist's geometry is the look the owner accepted, so a
    rebuild keeps it and adds the framework (real morphs, correct-direction takes, connected
    parts, gates) instead of redrawing the hull from primitives. The parked pose is preserved
    exactly as import_shipped_armature does; no action is imported (the takes are re-authored
    against the baked rest). Returns (armature, mesh).
    """
    import forms
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx_path, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=False)
    new_objs = [o for o in bpy.data.objects if o not in before]
    arms = [o for o in new_objs if o.type == "ARMATURE"]
    meshes = [o for o in new_objs if o.type == "MESH"]
    if len(arms) != 1 or len(meshes) != 1:
        raise RuntimeError("%s: expected one armature and one mesh, found %d / %d" % (fbx_path, len(arms), len(meshes)))
    arm, mesh = arms[0], meshes[0]
    for m in mesh.data.materials:
        if m is not None:
            forms.SLOT_COLOURS[m.name] = tuple(m.diffuse_color[:3])
    for o in new_objs:
        if o is not arm and o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    if mesh.data.shape_keys:
        for kb in mesh.data.shape_keys.key_blocks:
            kb.value = 0.0  # the artist files store DeformPercent 100 as the channel's static value (LESSONS 33)
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        mb = pb.matrix_basis.copy()
        pb.rotation_mode = "XYZ"
        pb.matrix_basis = mb
    prev_active = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.objects.active = prev_active
    arm.select_set(False)
    mesh.select_set(False)
    mod = mesh.modifiers.get("Armature")
    if mod is None:
        for m_ in mesh.modifiers:
            if m_.type == "ARMATURE":
                mod = m_
    if mod is None:
        mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True
    # the importer parents the mesh under the armature object and splits the file's frame between the two
    # (armature R(-108.11,0,0), mesh R(71.89,-180,0) on the Squirrel: world = R_z(180)); the shipped file has the
    # mesh node at IDENTITY as a top-level sibling (what the prefab's stripped references bind), so unparent
    # it keeping its world placement and bake that into the vertices: the node then exports at identity
    mw = mesh.matrix_world.copy()
    # the node's own uniform scale, for a file whose mesh node is NOT at identity (the Sparrow's is 0.2034,
    # its vertices 4.9x larger): `restore_node_scale` puts it back on the node before export
    mesh["artist_node_scale"] = float(sum(mw.to_scale()) / 3.0)
    mesh.parent = None
    mesh.matrix_world = mw
    forms.apply_transform(mesh)
    return arm, mesh


def restore_node_scale(mesh):
    """
    Give the mesh node back the artist file's uniform scale, keeping every vertex where it is drawn:
    the vertices AND every shape key are divided by it and the node carries it (Mesh.transform with
    shape_keys). Skinning is unchanged (Unity skins from bones and bind poses), but a node the prefab
    or rest_compare reads keeps its authored transform. A no-op when the artist's node was at scale 1.
    """
    from mathutils import Matrix
    s = float(mesh.get("artist_node_scale", 1.0))
    if abs(s - 1.0) < 1e-4:
        return 1.0
    mesh.data.transform(Matrix.Diagonal((1.0 / s, 1.0 / s, 1.0 / s, 1.0)), shape_keys=True)
    mesh.scale = (s, s, s)
    return s


MOVED_BONES = []  # names a spec moved on purpose (rest_compare --allow-moved reads the same list from the spec)


def move_bones(arm, mapping):
    """
    Move the named bones' rest positions in WORLD space: `mapping` is {bone: fn(world_head) ->
    new world head}; the tail moves by the same vector, so the bone keeps its orientation and
    roll (a prefab mount under it keeps its authored local rotation). A deliberate migration
    step (the Sparrow's jet-host bones to the stern); rest_compare must be told with
    --allow-moved. Returns the names moved.
    """
    from mathutils import Vector as _V
    prev_active = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    mw, mw_inv = arm.matrix_world, arm.matrix_world.inverted()
    moved = []
    for eb in arm.data.edit_bones:
        if eb.name not in mapping:
            continue
        head_w = mw @ eb.head
        tail_w = mw @ eb.tail
        new_head_w = _V(mapping[eb.name](head_w))
        delta = new_head_w - head_w
        roll = eb.roll
        eb.head = mw_inv @ new_head_w
        eb.tail = mw_inv @ (tail_w + delta)
        eb.roll = roll
        moved.append(eb.name)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.objects.active = prev_active
    MOVED_BONES.extend(moved)
    return moved


def bone_world_head(arm, name):
    b = arm.data.bones[name]
    return arm.matrix_world @ b.head_local


def bone_world_axis(arm, name):
    b = arm.data.bones[name]
    return (arm.matrix_world.to_3x3() @ (b.tail_local - b.head_local)).normalized()
