using System;

namespace Unity.Mathematics
{
    // NOTE: in Unity this type ships with the Entities packages (Unity.Mathematics.Extensions)
    // but lives in the Unity.Mathematics NAMESPACE, which is why it is owned here.

    /// <summary>Axis-aligned bounding box stored as centre + half-size.</summary>
    [Serializable]
    public struct AABB : IEquatable<AABB>
    {
        public float3 Center;
        public float3 Extents;

        public float3 Size => Extents * 2f;
        public float3 Min => Center - Extents;
        public float3 Max => Center + Extents;

        public bool Contains(float3 point) =>
            !(point.x < Center.x - Extents.x || point.x > Center.x + Extents.x
              || point.y < Center.y - Extents.y || point.y > Center.y + Extents.y
              || point.z < Center.z - Extents.z || point.z > Center.z + Extents.z);

        public bool Contains(AABB b) => math.all(Min <= b.Min) && math.all(Max >= b.Max);

        public float DistanceSq(float3 point) => math.lengthsq(math.max(math.abs(point - Center), Extents) - Extents);

        public static AABB FromMinMax(float3 min, float3 max) => new AABB { Center = (min + max) * 0.5f, Extents = (max - min) * 0.5f };

        /// <summary>Bounds of this box after an affine transform (conservative, exact for AABBs).</summary>
        public static AABB Transform(float4x4 transform, AABB localBounds)
        {
            AABB t;
            t.Center = math.transform(transform, localBounds.Center);
            float3 e = localBounds.Extents;
            t.Extents = math.abs(transform.c0.xyz) * e.x + math.abs(transform.c1.xyz) * e.y + math.abs(transform.c2.xyz) * e.z;
            return t;
        }

        public void Encapsulate(float3 point)
        {
            float3 min = math.min(Min, point), max = math.max(Max, point);
            Center = (min + max) * 0.5f; Extents = (max - min) * 0.5f;
        }

        public void Encapsulate(AABB aabb)
        {
            float3 min = math.min(Min, aabb.Min), max = math.max(Max, aabb.Max);
            Center = (min + max) * 0.5f; Extents = (max - min) * 0.5f;
        }

        public bool Equals(AABB other) => Center.Equals(other.Center) && Extents.Equals(other.Extents);
        public override bool Equals(object obj) => obj is AABB o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Center, Extents);
        public override string ToString() => $"AABB(Center:{Center}, Extents:{Extents})";
    }
}
