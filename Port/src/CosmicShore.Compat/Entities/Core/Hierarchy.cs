using Unity.Entities;

namespace Unity.Transforms
{
    /// <summary>The entity's transform parent (data only — no transform system runs in the port).</summary>
    public struct Parent : IComponentData
    {
        public Entity Value;
    }

    /// <summary>A child link, stored on the parent (data only).</summary>
    [InternalBufferCapacity(8)]
    public struct Child : IBufferElementData
    {
        public Entity Value;
    }

    /// <summary>Tag the original uses to keep an entity's world transform from being recomputed.</summary>
    public struct Static : IComponentData { }
}
