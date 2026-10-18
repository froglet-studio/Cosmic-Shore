"""
takes.py - the animation takes a vessel's puppetry binds.

Two kinds, matching what the fleet ships:

  POSE takes  - one keyframe per bone, one take per stick extreme. The Animator-driven vessels
                (Squirrel, Serpent, Sparrow) blend nine of them in 2D blend trees by the live
                Pitch / Yaw / Roll / Throttle floats: idle (0,0), pitchup (0,+1), pitchdown
                (0,-1), yawright (+1,0), yawleft (-1,0); speedup (0,+1), speeddown (0,-1),
                rollright (+1,0), rollleft (-1,0). A pose take keys EVERY bone (the Squirrel keys
                all 43 nodes per pose) so a blend never leaves a bone at whatever the previous
                clip left it.
  MOTION takes - keyed over time (a missile launch, a boost, an idle breath). Keyed at the
                scene fps; the exporter bakes them.

Each take is one Blender Action on the armature; the FBX exporter writes one AnimationStack per
action named '<ArmatureObject>|<ActionName>', which is the take name Unity shows and the .meta
pins to a clip. So the spec's take names are action names, and the armature's object name is
part of the contract.

Pose deltas are given in DEGREES about the WORLD axes of the Blender modelling frame (X pitch,
Y roll, Z yaw) plus optional world displacements, conjugated into each bone's rest frame by
`world_to_bone` (world=True, the default, so the same numbers mean the same thing on any rig);
world=False takes raw pose-bone values. `rest` is implicit, so (0,0,0) is the rest pose.
"""

import math

import bpy
from mathutils import Euler, Matrix, Vector

STANDARD_POSES = ("idle", "pitchup", "pitchdown", "yawleft", "yawright",
                  "rollleft", "rollright", "speedup", "speeddown")


def _ensure_anim(arm):
    if arm.animation_data is None:
        arm.animation_data_create()
    return arm.animation_data


def _assign_action(arm, act):
    ad = _ensure_anim(arm)
    ad.action = act
    # Blender 4.4+ slotted actions: pick (or make) the slot for this armature
    if hasattr(act, "slots"):
        slot = None
        for s in act.slots:
            if s.target_id_type in ("OBJECT", "UNSPECIFIED"):
                slot = s
                break
        if slot is None:
            slot = act.slots.new("OBJECT", arm.name)
        try:
            ad.action_slot = slot
        except Exception:
            pass
    return act


def _rest_world_rotation(arm, bone_name):
    """The bone's rest orientation in world space (its local axes as world axes)."""
    b = arm.data.bones[bone_name]
    return (arm.matrix_world.to_3x3() @ b.matrix_local.to_3x3()).normalized()


def world_to_bone(arm, bone_name, rot_deg_world, loc_world=(0, 0, 0)):
    """
    Turn a rotation given about the WORLD axes (Blender frame: X = pitch, Y = roll about the
    hull's length, Z = yaw) and a world-space displacement into the pose-bone values that
    produce them. A pose bone rotates in its own REST-LOCAL frame, which on an artist's rig
    points wherever the artist left it (the Squirrel's bones all point along +X, some are
    rolled 90 degrees) - so a spec that wrote "(-10, 0, 0) = pitch" straight into
    rotation_euler would pitch one bone and roll the next. Conjugating by the rest
    orientation makes every pose spec mean the same thing on every rig:
        R_local = R_rest^-1 * R_world * R_rest ;  d_local = R_rest^-1 * d_world
    """
    R = _rest_world_rotation(arm, bone_name)
    Rw = Euler([math.radians(a) for a in rot_deg_world], "XYZ").to_matrix()
    Rl = R.inverted() @ Rw @ R
    dl = R.inverted() @ Vector(loc_world)
    return Rl.to_euler("XYZ"), dl


def clear_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = Euler((0, 0, 0))
        pb.location = Vector((0, 0, 0))
        pb.scale = Vector((1, 1, 1))


def apply_pose(arm, pose, world=True):
    """Set pose-bone values from a pose dict (see pose_take); world=True reads the numbers as
    world-axis rotations / world displacements and conjugates them per bone."""
    for bone, spec in pose.items():
        pb = arm.pose.bones.get(bone)
        if pb is None:
            raise KeyError("pose names bone %s which the armature lacks" % bone)
        if isinstance(spec, dict):
            rot = spec.get("rot", (0, 0, 0))
            loc = spec.get("loc", (0, 0, 0))
        else:
            rot, loc = spec, (0, 0, 0)
        pb.rotation_mode = "XYZ"
        if world:
            eul, dl = world_to_bone(arm, bone, rot, loc)
            pb.rotation_euler = eul
            pb.location = dl
        else:
            pb.rotation_euler = Euler([math.radians(a) for a in rot], "XYZ")
            pb.location = Vector(loc)


AUTHORED = {}  # take name -> the bone names the pose/motion deflected; verify checks the file moved them


def _deflects(spec, min_deg=2.0, min_loc=0.01):
    """True when a bone's pose entry asks for a deflection the probe can see (>= 2 degrees or 0.01 units)."""
    if isinstance(spec, dict):
        rot, loc = spec.get("rot", (0, 0, 0)), spec.get("loc", (0, 0, 0))
    else:
        rot, loc = spec, (0, 0, 0)
    return max(abs(float(v)) for v in rot) >= min_deg or max(abs(float(v)) for v in loc) >= min_loc


def follow(arm, parent_bone, child_bone, rot_deg_world, pivot=None):
    """
    The pose entry for `child_bone` so that it rides a WORLD rotation applied to `parent_bone`
    as if rigidly attached: the same rotation plus the displacement of the child's head around
    the parent's head (or `pivot`). For pods on a wing, struts on a shoulder, a hub on a boom:
    bones that are siblings in the artist's rig but one part in the player's eye.
    """
    import rig
    p0 = Vector(pivot) if pivot is not None else rig.bone_world_head(arm, parent_bone)
    c0 = rig.bone_world_head(arm, child_bone)
    R = Euler([math.radians(a) for a in rot_deg_world], "XYZ").to_matrix()
    c1 = p0 + R @ (c0 - p0)
    return {"rot": tuple(rot_deg_world), "loc": tuple(c1 - c0)}


def pose_take(arm, name, pose, frame=1, key_all=True, world=True):
    """
    Bake one pose as an action named `name`. `pose` maps bone name -> (rx, ry, rz) degrees,
    or -> {"rot": (rx,ry,rz), "loc": (x,y,z)}. With world=True (the default) the numbers are
    rotations about the WORLD axes of the Blender frame the hull was modelled in (X pitch,
    Y roll, Z yaw) and world displacements, converted per bone (see world_to_bone); with
    world=False they are raw pose-bone values. Every bone is keyed when key_all (the default).
    """
    act = bpy.data.actions.new(name)
    _assign_action(arm, act)
    clear_pose(arm)
    apply_pose(arm, pose, world=world)
    for pb in arm.pose.bones:
        if key_all or pb.name in pose:
            pb.keyframe_insert("rotation_euler", frame=frame)
            pb.keyframe_insert("location", frame=frame)
    act.use_fake_user = True
    AUTHORED[name] = [b for b in pose if b in arm.pose.bones and _deflects(pose[b])]
    return act


def motion_take(arm, name, keyframes, key_all=True, world=True):
    """
    Bake a motion: `keyframes` is a list of (frame, pose) where pose is as in pose_take. Bones
    not mentioned at a frame keep their previous value (Blender interpolates between keys).
    """
    act = bpy.data.actions.new(name)
    _assign_action(arm, act)
    clear_pose(arm)
    for frame, pose in keyframes:
        apply_pose(arm, pose, world=world)
        for pb in arm.pose.bones:
            if key_all or pb.name in pose:
                pb.keyframe_insert("rotation_euler", frame=frame)
                pb.keyframe_insert("location", frame=frame)
    act.use_fake_user = True
    AUTHORED[name] = sorted({b for _, pose in keyframes for b in pose if b in arm.pose.bones and _deflects(pose[b])})
    return act


def mirror_pose(pose, lr=(".l", ".r")):
    """
    The left-hand version of a right-hand pose: swap .l/.r (or .L/.R) bone names and negate
    the rotations that flip across the mirror plane (Y and Z in Blender's local bone frame
    for a bone mirrored across X). Good enough for symmetric rigs built by rig.py; a spec can
    always hand-author the asymmetric case.
    """
    out = {}
    for bone, spec in pose.items():
        if bone.endswith(lr[0]):
            other = bone[: -len(lr[0])] + lr[1]
        elif bone.endswith(lr[1]):
            other = bone[: -len(lr[1])] + lr[0]
        else:
            other = bone
        if isinstance(spec, dict):
            r = spec.get("rot", (0, 0, 0))
            out[other] = {"rot": (r[0], -r[1], -r[2]), "loc": spec.get("loc", (0, 0, 0))}
        else:
            out[other] = (spec[0], -spec[1], -spec[2])
    return out


def standard_pose_set(arm, builder, prefix="", world=True):
    """
    Build the nine standard poses from a `builder(pose_name) -> pose dict` callable, so a
    spec can define them as one function of the pose name. Returns {name: action}.
    """
    out = {}
    for p in STANDARD_POSES:
        out[p] = pose_take(arm, prefix + p, builder(p), world=world)
    clear_pose(arm)
    return out


def set_take(arm, action, frame=1):
    """Pose the armature by an action at a frame (for renders)."""
    _assign_action(arm, action)
    bpy.context.scene.frame_set(frame)


def retarget_from(arm, ref_path, only=None):
    """
    Key the REFERENCE file's takes onto `arm` so every bone lands, in armature space, exactly
    where the reference's lands in every frame of every take. `arm` must be the reference's own
    armature (same bone names and armature object), its rest free to differ - a rest that bakes
    the parked pose, say: the keys are solved per bone from the reference's posed matrices, so
    the take is the artist's take whatever rest the rebuild chose (LESSONS 41). Actions keep the
    reference's names (the .meta pins them) and frame numbers; the scene takes its fps.
    Returns {take: action}.
    """
    import take_parity
    dump = take_parity.retarget_dump(ref_path)
    bpy.context.scene.render.fps = dump["fps"]
    aw = Matrix(dump["arm_world"])
    if max(abs(a - b) for ra, rb in zip(aw, arm.matrix_world) for a, b in zip(ra, rb)) > 1e-4:
        raise RuntimeError("retarget_from: the armature object's transform differs from the reference's")

    def depth(b):
        d = 0
        while b.parent:
            b, d = b.parent, d + 1
        return d
    order = sorted(arm.pose.bones, key=lambda pb: depth(pb.bone))
    acts = {}
    for take, rec in dump["takes"].items():
        if only and take not in only:
            continue
        # the importer names an action "<object>|<stack>", and the stack itself is "<armature>|<take>"; the
        # exporter writes "<armature>|<action>", so the action is the LAST segment (the .meta's takeName round-trips)
        act = bpy.data.actions.new(rec["name"].split("|")[-1])
        _assign_action(arm, act)
        prev = {}
        moved = set()
        for f in sorted(rec["frames"], key=int):
            mats = rec["frames"][f]
            clear_pose(arm)
            bpy.context.view_layer.update()
            for pb in order:
                if pb.name in mats:
                    pb.matrix = Matrix(mats[pb.name])
                    bpy.context.view_layer.update()
            for pb in arm.pose.bones:
                loc, q, sc = pb.matrix_basis.decompose()
                pb.rotation_mode = "XYZ"
                pb.rotation_euler = q.to_euler("XYZ", prev[pb.name]) if pb.name in prev else q.to_euler("XYZ")
                prev[pb.name] = pb.rotation_euler.copy()
                if loc.length > 0.01 or math.degrees(q.angle) > 2.0 or max(abs(s - 1) for s in sc) > 0.01:
                    moved.add(pb.name)
                pb.keyframe_insert("location", frame=int(f))
                pb.keyframe_insert("rotation_euler", frame=int(f))
                pb.keyframe_insert("scale", frame=int(f))
        AUTHORED[act.name] = sorted(moved)
        acts[take] = act
    clear_pose(arm)
    if arm.animation_data:
        arm.animation_data.action = None
    return acts
