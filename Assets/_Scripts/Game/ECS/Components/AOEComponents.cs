using Unity.Entities;
using Unity.Mathematics;

namespace CosmicShore.ECS
{
    /// <summary>
    /// ECS-side mirror of PrismSpatialData — HOT data for Burst-compiled AOE spatial queries.
    /// 16 bytes, preserving the cache-line-packed layout of the legacy NativeArray:
    ///   4 entries per 64-byte cache line, 48 KB for 3000 prisms.
    ///
    /// The companion MonoBehaviour (PrismEntityBridge) pushes updates to this component
    /// whenever prism position or flags change. PrismAOERegistry reads it via EntityQuery
    /// when UseECSQuery is enabled.
    /// </summary>
    public struct AOESpatial : IComponentData
    {
        public float3 Position; // 12B
        public byte Flags;      // 1B (PrismFlags bit-packed: IsActive, Destroyed, IsShielded, IsSuperShielded)
        public byte _pad0;      // 1B
        public byte _pad1;      // 1B
        public byte _pad2;      // 1B
        // Total: 16B — matches PrismSpatialData layout exactly
    }

    /// <summary>
    /// ECS-side mirror of PrismDamageData — COLD data read only for hit prisms.
    /// Only accessed on main thread for the small set of prisms that pass the spatial filter.
    /// </summary>
    public struct AOEDamage : IComponentData
    {
        public float Volume; // 4B
        public int Domain;   // 4B
        // Total: 8B — matches PrismDamageData layout exactly
    }

    /// <summary>
    /// Stores the index into PrismAOERegistry's managed Prism[] array.
    /// Used to map Entity hits back to the MonoBehaviour Prism for damage callbacks
    /// during the hybrid bridge phase. Once fully migrated to ECS (Phase 3+),
    /// this component is no longer needed.
    /// </summary>
    public struct AOEManagedRef : IComponentData
    {
        public int ManagedIndex;
    }
}
