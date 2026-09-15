using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where a discrete feature can attach. The head declares these by NAME; a trait names the
    /// one it wants. Bilateral sites resolve to a left and a mirrored right instance.
    /// </summary>
    [Serializable]
    public struct HeadSiteSpec
    {
        public string Name;
        [Tooltip("Polar angle from +Y, degrees. 0 = crown.")] public float ThetaDeg;
        [Tooltip("Azimuth from +Z (the face) toward +X, degrees. The LEFT instance; right is mirrored.")] public float PhiDeg;
        [Tooltip("Angular radius of the seam ring, degrees.")] public float RingDeg;
        public bool Bilateral;
        [Tooltip("Which head axis, if any, moves this site's azimuth (e.g. OrbitalSpacing).")] public HeadAxis SpreadAxis;
        [Tooltip("Degrees of azimuth per unit of SpreadAxis.")] public float SpreadDegPerUnit;
    }

    /// <summary>
    /// A resolved site on ONE head at ONE shape: a pose, a radius, and — for seam features —
    /// the ring of surface points a feature's base ring snaps onto.
    /// </summary>
    public sealed class AttachmentSite
    {
        public string Name;
        public bool Mirrored;
        public Vector3 Position;     // on the head surface
        public Vector3 Normal;       // outward
        public Vector3 Right;        // tangent, +x of the feature's local frame (mirrored side flips)
        public Vector3 Up;           // tangent, +y of the feature's local frame
        public float Radius;         // head units; the seam ring's radius
        public Vector3[] Ring;       // surface points, Ring.Length == the contract's ring count

        public Vector3 ToHead(Vector3 local) => Position + Right * local.x + Up * local.y + Normal * local.z;
        public Vector3 DirToHead(Vector3 local) => (Right * local.x + Up * local.y + Normal * local.z);
        public Vector3 ToLocal(Vector3 head)
        {
            Vector3 d = head - Position;
            return new Vector3(Vector3.Dot(d, Right), Vector3.Dot(d, Up), Vector3.Dot(d, Normal));
        }
        public Vector3 DirToLocal(Vector3 headDir) => new Vector3(Vector3.Dot(headDir, Right), Vector3.Dot(headDir, Up), Vector3.Dot(headDir, Normal));
    }

    /// <summary>
    /// The seam contract, settled before the second seam feature existed: a seam feature's first
    /// <see cref="MeshPart.SeamRingCount"/> vertices are its base ring, lying on the UNIT circle in
    /// its local xy plane at z = 0, ordered counter-clockwise from +x; the site samples the head
    /// surface at exactly that many angles, in the same order, and the assembler snaps ring
    /// vertex k onto site ring point k. Anything else throws, here, by name.
    /// </summary>
    public static class AttachmentContract
    {
        public const int RingCount = 24;
        public const float RingTolerance = 0.02f;

        public static void AssertSeamFeature(MeshPart part, AttachmentSite site)
        {
            if (part.SeamRingCount == 0) return; // embedded: no seam
            if (part.SeamRingCount != RingCount)
                throw new InvalidOperationException(
                    $"Attachment contract: '{part.Name}' declares a seam ring of {part.SeamRingCount}, the contract is {RingCount}.");
            if (part.Verts.Count < RingCount)
                throw new InvalidOperationException(
                    $"Attachment contract: '{part.Name}' has {part.Verts.Count} vertices, fewer than its seam ring.");
            if (site.Ring == null || site.Ring.Length != RingCount)
                throw new InvalidOperationException(
                    $"Attachment contract: site '{site.Name}' carries no {RingCount}-point ring for seam feature '{part.Name}'.");
            for (int k = 0; k < RingCount; k++)
            {
                var v = part.Verts[k];
                float expectedAngle = GeometryKit.Tau * k / RingCount;
                var expected = new Vector3(Mathf.Cos(expectedAngle), Mathf.Sin(expectedAngle), 0f);
                if ((v - expected).magnitude > RingTolerance)
                    throw new InvalidOperationException(
                        $"Attachment contract: '{part.Name}' ring vertex {k} is at {v}, expected {expected} (unit circle, z = 0, CCW from +x).");
            }
        }

        /// <summary>The site-local ring point k on the unit circle — what every seam generator emits.</summary>
        public static Vector3 UnitRingPoint(int k)
        {
            float a = GeometryKit.Tau * k / RingCount;
            return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }
    }
}
