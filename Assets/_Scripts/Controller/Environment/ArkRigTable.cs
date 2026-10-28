using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark model's RIG TABLE: what the game binds to the model, written by the model's own build
    /// (<c>Tools/Build/vessel_model/specs/ark_reverse.py</c>) beside the FBX as <c>ArkReverse.rig.json</c>, so the
    /// sockets the hull is laid into and the motion the jets and wings make come from the same numbers the model was
    /// built and clearance-proven with (<c>pose_clearance.py</c>), never from a second copy typed into C#.
    ///
    /// Everything is in the model's Unity space: x the pilot's right, y up, z the nose, in model units (the FBX imports
    /// 1:1). A <see cref="Socket"/> is one prism the hull wears: its centre, its long axis (<c>forward</c>), its up, its
    /// full size, its kind (the shield tier) and the bone it rides (<c>hull</c>, or a jaw for the jaw's blades). A
    /// <see cref="Bone"/> is one puppeted bone and, per input, the rotation its take applies, in model space about the
    /// bone's own pivot.
    /// </summary>
    [Serializable]
    public sealed class ArkRigTable
    {
        public string model;
        public float noseToJets;
        public Socket[] sockets = Array.Empty<Socket>();
        public Bone[] bones = Array.Empty<Bone>();

        /// <summary>The bone a socket rides when it rides the hull itself (the Ark's root).</summary>
        public const string HullBone = "hull";

        [Serializable]
        public sealed class Socket
        {
            public string name;
            public string tier;
            public string kind;
            public float[] position;
            public float[] forward;
            public float[] up;
            public float[] size;
            public string bone;

            public Vector3 Position => ArkRigTable.V3(position);
            public Vector3 Size => ArkRigTable.V3(size);

            /// <summary>The prism's pose: its long axis along <c>forward</c>, its up along <c>up</c>.</summary>
            public Quaternion Rotation
            {
                get
                {
                    var f = ArkRigTable.V3(forward);
                    var u = ArkRigTable.V3(up);
                    return f.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(f, u.sqrMagnitude > 1e-8f ? u : Vector3.up) : Quaternion.identity;
                }
            }

            public PrismKind Kind => ArkRigTable.ParseKind(kind);
            public bool RidesHull => string.IsNullOrEmpty(bone) || bone == HullBone;
        }

        /// <summary>
        /// One puppeted bone: the rotation each flight take applies to it (empty = the take leaves it), and the ACTION
        /// takes the game plays on events - <c>release</c> (the lance slides out, its slide in <c>releaseMove</c>) and
        /// <c>hangarOpen</c> (the hangar's doors).
        /// </summary>
        [Serializable]
        public sealed class Bone
        {
            public string bone;
            public float[] head;
            public float[] pitchUp, pitchDown, yawRight, yawLeft, rollRight, rollLeft, speedUp, speedDown;
            public float[] release, releaseMove, hangarOpen;

            public Vector3 Head => ArkRigTable.V3(head);
            public Vector3 ReleaseMove => ArkRigTable.V3(releaseMove);
        }

        /// <summary>Parse a table; null (with the reason) when the text is not one.</summary>
        public static ArkRigTable Parse(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "the rig table is empty";
                return null;
            }
            ArkRigTable table;
            try
            {
                table = JsonUtility.FromJson<ArkRigTable>(json);
            }
            catch (ArgumentException e)
            {
                error = "the rig table is not JSON: " + e.Message;
                return null;
            }
            if (table == null || table.sockets == null || table.sockets.Length == 0)
            {
                error = "the rig table names no sockets";
                return null;
            }
            table.bones ??= Array.Empty<Bone>();
            return table;
        }

        /// <summary>A take's rotation, identity when the take leaves the bone alone (or the entry is malformed).</summary>
        public static Quaternion Q(float[] xyzw)
        {
            if (xyzw == null || xyzw.Length != 4) return Quaternion.identity;
            var q = new Quaternion(xyzw[0], xyzw[1], xyzw[2], xyzw[3]);
            float n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return n > 1e-6f ? new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n) : Quaternion.identity;
        }

        public static Vector3 V3(float[] v) =>
            v != null && v.Length == 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.zero;

        public static PrismKind ParseKind(string kind) => kind switch
        {
            "Danger" => PrismKind.Danger,
            "Shielded" => PrismKind.Shielded,
            "SuperShielded" => PrismKind.SuperShielded,
            _ => PrismKind.Plain,
        };

        /// <summary>
        /// The blend of one input's two takes: <paramref name="positive"/> for v in (0, 1], <paramref name="negative"/>
        /// for v in [-1, 0), identity at 0. Clamped: a take played past full would leave the clearance its gate proved.
        /// </summary>
        public static Quaternion Blend(Quaternion positive, Quaternion negative, float v) =>
            v >= 0f
                ? Quaternion.Slerp(Quaternion.identity, positive, Mathf.Min(v, 1f))
                : Quaternion.Slerp(Quaternion.identity, negative, Mathf.Min(-v, 1f));

        /// <summary>
        /// One bone's share of the input (pitch, yaw, roll, speed in [-1, 1]): <paramref name="moves"/> is 1 for each
        /// input that has a take on this bone, 0 for one that leaves it. When the inputs that move the bone add up past
        /// 1 they are scaled to sum to 1, so no bone travels further than one full take: a jet's pitch and bank takes
        /// both turn it about its own x axis, and played in full together they would swing it past the clearance the
        /// model's gate proved (<c>pose_clearance.py --combos --budget</c> proves the shared envelope).
        /// </summary>
        public static Vector4 Budget(Vector4 input, Vector4 moves)
        {
            float total = Mathf.Abs(input.x) * moves.x + Mathf.Abs(input.y) * moves.y
                        + Mathf.Abs(input.z) * moves.z + Mathf.Abs(input.w) * moves.w;
            return total > 1f ? input / total : input;
        }

        /// <summary>
        /// A bone's local rotation for a take's <paramref name="delta"/>: a rotation about the bone's own pivot in the
        /// model's frame at rest, conjugated into the bone's frame (the build's <c>takes.world_to_bone</c>), so it
        /// composes under whatever the bone's parent does: a jet pitches about its yawed gimbal ring.
        /// <paramref name="restInModel"/> is the bone's rest rotation in the model's frame.
        /// </summary>
        public static Quaternion PoseLocal(Quaternion restLocal, Quaternion restInModel, Quaternion delta) =>
            restLocal * (Quaternion.Inverse(restInModel) * delta * restInModel);
    }
}
