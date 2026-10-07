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
        public static float Distance(Vector3 center, Quaternion inverseRotation, Vector3 half, Vector3 p)
        {
            float dx = (float)(p.x - center.x), dy = (float)(p.y - center.y), dz = (float)(p.z - center.z); // p - center
            return new LocalFrame(inverseRotation).Distance(dx, dy, dz, half);
        }

        /// <summary>
        /// A box's inverse rotation, prepared once so the planner can measure one box from five hull
        /// points per rollout step - in floats, because the editor's Mono JIT pays for every Vector3
        /// operator as a call and a struct copy. It keeps the nine products Unity's
        /// <c>Quaternion * Vector3</c> keeps as locals and evaluates each component as that operator's
        /// one expression, so a point lands exactly where <c>inverseRotation * (p - center)</c> put it
        /// - including in the editor's Mono, which computes inside an expression in double precision
        /// (pre-rounding the 3x3 matrix terms would NOT be the same). Pass the offset (dx, dy, dz)
        /// rounded to float - an explicit (float) - as the fields of <c>p - center</c> would be.
        /// </summary>
        public readonly struct LocalFrame
        {
            readonly float _xx, _yy, _zz, _xy, _xz, _yz, _wx, _wy, _wz;

            public LocalFrame(Quaternion q)
            {
                float nx = q.x * 2f, ny = q.y * 2f, nz = q.z * 2f;
                _xx = q.x * nx; _yy = q.y * ny; _zz = q.z * nz;
                _xy = q.x * ny; _xz = q.x * nz; _yz = q.y * nz;
                _wx = q.w * nx; _wy = q.w * ny; _wz = q.w * nz;
            }

            /// <summary>Distance (0 inside) from the point at world offset (dx, dy, dz) from the box
            /// centre to the box of half-extents <paramref name="half"/>.</summary>
            public float Distance(float dx, float dy, float dz, Vector3 half)
            {
                // Each component rounded as the Vector3 field it lands in (see the struct's summary).
                float lx = (float)((1f - (_yy + _zz)) * dx + (_xy - _wz) * dy + (_xz + _wy) * dz);
                float ly = (float)((_xy + _wz) * dx + (1f - (_xx + _zz)) * dy + (_yz - _wx) * dz);
                float lz = (float)((_xz - _wy) * dx + (_yz + _wx) * dy + (1f - (_xx + _yy)) * dz);
                float x = Mathf.Max(Mathf.Abs(lx) - half.x, 0f);
                float y = Mathf.Max(Mathf.Abs(ly) - half.y, 0f);
                float z = Mathf.Max(Mathf.Abs(lz) - half.z, 0f);
                return Mathf.Sqrt(x * x + y * y + z * z);
            }
        }
    }
}
