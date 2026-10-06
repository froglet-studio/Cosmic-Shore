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
        public float Distance(Vector3 p)
        {
            Vector3 lp = Quaternion.Inverse(Rotation) * (p - Center);
            float x = Mathf.Max(Mathf.Abs(lp.x) - Half.x, 0f);
            float y = Mathf.Max(Mathf.Abs(lp.y) - Half.y, 0f);
            float z = Mathf.Max(Mathf.Abs(lp.z) - Half.z, 0f);
            return Mathf.Sqrt(x * x + y * y + z * z);
        }
    }
}
