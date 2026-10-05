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
        /// The same distance with the box's inverse rotation supplied. The planner's rollouts ask it
        /// for one box at up to five points per step, so they invert each rotation once per decision
        /// instead of once per call. ONE kernel for both forms, so the two cannot disagree.
        /// </summary>
        public static float Distance(Vector3 center, Quaternion inverseRotation, Vector3 half, Vector3 p)
        {
            Vector3 lp = inverseRotation * (p - center);
            float x = Mathf.Max(Mathf.Abs(lp.x) - half.x, 0f);
            float y = Mathf.Max(Mathf.Abs(lp.y) - half.y, 0f);
            float z = Mathf.Max(Mathf.Abs(lp.z) - half.z, 0f);
            return Mathf.Sqrt(x * x + y * y + z * z);
        }
    }
}
