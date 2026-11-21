using System;

namespace Unity.Entities
{
    /// <summary>
    /// An entity handle: an index into the owning <see cref="EntityManager"/>'s store plus a
    /// version that goes stale when the slot is destroyed and reused (original Entities
    /// semantics — a destroyed handle never aliases the entity that later takes its slot).
    /// </summary>
    public struct Entity : IEquatable<Entity>, IComparable<Entity>
    {
        public int Index;
        public int Version;

        /// <summary>The null entity (index 0, version 0). No live entity ever uses index 0.</summary>
        public static Entity Null => default;

        public bool Equals(Entity other) => Index == other.Index && Version == other.Version;
        public override bool Equals(object obj) => obj is Entity e && Equals(e);
        public override int GetHashCode() => (Index * 397) ^ Version;
        public int CompareTo(Entity other) => Index != other.Index ? Index.CompareTo(other.Index) : Version.CompareTo(other.Version);
        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
        public override string ToString() => Equals(Null) ? "Entity.Null" : $"Entity({Index}:{Version})";
    }

    /// <summary>Marker the original uses for anything that can appear in a query.</summary>
    public interface IQueryTypeParameter { }

    /// <summary>Marker for unmanaged (struct) or managed (class) component data.</summary>
    public interface IComponentData : IQueryTypeParameter { }

    /// <summary>A component that can be toggled on/off per entity without a structural change.</summary>
    public interface IEnableableComponent { }

    /// <summary>Shared component data: one value shared by every entity that carries it.</summary>
    public interface ISharedComponentData : IQueryTypeParameter { }

    /// <summary>Element type of a <see cref="DynamicBuffer{T}"/>.</summary>
    public interface IBufferElementData { }

    /// <summary>Tag: the entity is a prefab — stripped from every clone <c>Instantiate</c> makes.</summary>
    public struct Prefab : IComponentData { }

    /// <summary>Tag: the entity is disabled — added/removed by <c>EntityManager.SetEnabled</c>.</summary>
    public struct Disabled : IComponentData { }

    /// <summary>Links a root entity to its children (the root is always element 0).</summary>
    [InternalBufferCapacity(1)]
    public struct LinkedEntityGroup : IBufferElementData
    {
        public Entity Value;
        public static implicit operator LinkedEntityGroup(Entity e) => new LinkedEntityGroup { Value = e };
    }

    /// <summary>Hint for a buffer's in-chunk capacity (no storage consequence in the port).</summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class InternalBufferCapacityAttribute : Attribute
    {
        public readonly int Capacity;
        public InternalBufferCapacityAttribute(int capacity) { Capacity = capacity; }
    }

    /// <summary>Keeps a system out of automatic world creation (the port creates none automatically).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class DisableAutoCreationAttribute : Attribute { }

    /// <summary>Places a system in a system group (recorded only — the port has no system scheduler).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class UpdateInGroupAttribute : Attribute
    {
        public Type GroupType { get; }
        public bool OrderFirst { get; set; }
        public bool OrderLast { get; set; }
        public UpdateInGroupAttribute(Type groupType) { GroupType = groupType; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class UpdateBeforeAttribute : Attribute
    {
        public Type SystemType { get; }
        public UpdateBeforeAttribute(Type systemType) { SystemType = systemType; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class UpdateAfterAttribute : Attribute
    {
        public Type SystemType { get; }
        public UpdateAfterAttribute(Type systemType) { SystemType = systemType; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class RequireMatchingQueriesForUpdateAttribute : Attribute { }

    [Flags]
    public enum WorldSystemFilterFlags : uint
    {
        Default = 1 << 0,
        Disabled = 1 << 1,
        EntitySceneOptimizations = 1 << 2,
        ProcessAfterLoad = 1 << 3,
        Editor = 1 << 4,
        LocalSimulation = 1 << 5,
        ServerSimulation = 1 << 6,
        ClientSimulation = 1 << 7,
        ThinClientSimulation = 1 << 8,
        Presentation = 1 << 9,
        All = ~0u,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class WorldSystemFilterAttribute : Attribute
    {
        public WorldSystemFilterFlags FilterFlags { get; }
        public WorldSystemFilterFlags ChildDefaultFilterFlags { get; }
        public WorldSystemFilterAttribute(WorldSystemFilterFlags flags, WorldSystemFilterFlags childDefaultFlags = WorldSystemFilterFlags.Default)
        { FilterFlags = flags; ChildDefaultFilterFlags = childDefaultFlags; }
    }
}
