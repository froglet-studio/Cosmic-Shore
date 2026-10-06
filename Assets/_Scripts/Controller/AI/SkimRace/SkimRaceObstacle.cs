using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A box of mass the race has LAID near the pilot - a trail rail prism, a pickup-ring prism -
    /// as the pilot sees it: centre, orientation and collider half-extents. The track's own
    /// super-shielded prisms are NOT obstacles: they are the racing line, and their contact
    /// shells live on <see cref="SkimRaceCourse"/>.
    /// </summary>
    public struct SkimRaceObstacle
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Half;

        /// <summary>Distance from <paramref name="p"/> to the box surface (0 inside).</summary>
        public float Distance(Vector3 p) => Distance(Center, Quaternion.Inverse(Rotation), Half, p);

        /// <summary>
        /// The same distance with the box's inverse rotation supplied. ONE kernel for every form
        /// (<see cref="LocalFrame.Distance"/>), so they cannot disagree.
        /// </summary>
        public static float Distance(Vector3 center, Quaternion inverseRotation, Vector3 half, Vector3 p) =>
            new LocalFrame(inverseRotation).Distance(p.x - center.x, p.y - center.y, p.z - center.z, half);

        /// <summary>
        /// A box's inverse rotation as the 3x3 matrix Unity's <c>Quaternion * Vector3</c> applies, built
        /// once so the planner can measure one box from five hull points per rollout step without
        /// re-deriving it - and in floats, because the editor's Mono JIT pays for every Vector3 operator
        /// as a call and a struct copy. The coefficients and the dot products are Unity's own formula,
        /// term for term, so a point lands where <c>inverseRotation * (p - center)</c> put it.
        /// </summary>
        public readonly struct LocalFrame
        {
            readonly float _m00, _m01, _m02, _m10, _m11, _m12, _m20, _m21, _m22;

            public LocalFrame(Quaternion q)
            {
                float nx = q.x * 2f, ny = q.y * 2f, nz = q.z * 2f;
                float xx = q.x * nx, yy = q.y * ny, zz = q.z * nz;
                float xy = q.x * ny, xz = q.x * nz, yz = q.y * nz;
                float wx = q.w * nx, wy = q.w * ny, wz = q.w * nz;
                _m00 = 1f - (yy + zz); _m01 = xy - wz; _m02 = xz + wy;
                _m10 = xy + wz; _m11 = 1f - (xx + zz); _m12 = yz - wx;
                _m20 = xz - wy; _m21 = yz + wx; _m22 = 1f - (xx + yy);
            }

            /// <summary>Distance (0 inside) from the point at world offset (dx, dy, dz) from the box
            /// centre to the box of half-extents <paramref name="half"/>.</summary>
            public float Distance(float dx, float dy, float dz, Vector3 half)
            {
                float lx = _m00 * dx + _m01 * dy + _m02 * dz;
                float ly = _m10 * dx + _m11 * dy + _m12 * dz;
                float lz = _m20 * dx + _m21 * dy + _m22 * dz;
                float x = Mathf.Max(Mathf.Abs(lx) - half.x, 0f);
                float y = Mathf.Max(Mathf.Abs(ly) - half.y, 0f);
                float z = Mathf.Max(Mathf.Abs(lz) - half.z, 0f);
                return Mathf.Sqrt(x * x + y * y + z * z);
            }
        }
    }
}
