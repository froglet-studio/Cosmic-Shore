using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities
{
    /// <summary>
    /// Identifies a component type (plus the access mode a query asks for). The port keys
    /// components by their CLR <see cref="Type"/>; <see cref="TypeIndex"/> is a stable
    /// per-process number handed out on first sight, like the original's type manager.
    /// </summary>
    public struct ComponentType : IEquatable<ComponentType>
    {
        public enum AccessMode
        {
            ReadWrite = 0,
            ReadOnly = 1,
            Exclude = 2,
        }

        static readonly Dictionary<Type, int> s_Indices = new();
        static readonly List<Type> s_Types = new() { null };

        internal Type ManagedType;

        public TypeIndex TypeIndex;
        public AccessMode AccessModeType;

        internal static TypeIndex IndexOf(Type type)
        {
            if (type == null) return default;
            lock (s_Indices)
            {
                if (!s_Indices.TryGetValue(type, out var index))
                {
                    index = s_Types.Count;
                    s_Types.Add(type);
                    s_Indices[type] = index;
                }
                return new TypeIndex { Value = index };
            }
        }

        internal static Type TypeOf(TypeIndex index)
        {
            lock (s_Indices)
                return index.Value > 0 && index.Value < s_Types.Count ? s_Types[index.Value] : null;
        }

        public ComponentType(Type type, AccessMode accessModeType = AccessMode.ReadWrite)
        {
            ManagedType = type ?? throw new ArgumentNullException(nameof(type));
            TypeIndex = IndexOf(type);
            AccessModeType = accessModeType;
        }

        public static ComponentType ReadWrite<T>() => new ComponentType(typeof(T), AccessMode.ReadWrite);
        public static ComponentType ReadOnly<T>() => new ComponentType(typeof(T), AccessMode.ReadOnly);
        public static ComponentType Exclude<T>() => new ComponentType(typeof(T), AccessMode.Exclude);
        public static ComponentType ReadWrite(Type type) => new ComponentType(type, AccessMode.ReadWrite);
        public static ComponentType ReadOnly(Type type) => new ComponentType(type, AccessMode.ReadOnly);
        public static ComponentType Exclude(Type type) => new ComponentType(type, AccessMode.Exclude);
        public static ComponentType FromTypeIndex(TypeIndex typeIndex) => new ComponentType(TypeOf(typeIndex), AccessMode.ReadWrite);

        public static implicit operator ComponentType(Type type) => new ComponentType(type, AccessMode.ReadWrite);

        public Type GetManagedType() => ManagedType;

        public bool IsBuffer => ManagedType != null && typeof(IBufferElementData).IsAssignableFrom(ManagedType);
        public bool IsSharedComponent => ManagedType != null && typeof(ISharedComponentData).IsAssignableFrom(ManagedType);
        public bool IsEnableable => ManagedType != null && typeof(IEnableableComponent).IsAssignableFrom(ManagedType);
        public bool IsManagedComponent => ManagedType != null && typeof(IComponentData).IsAssignableFrom(ManagedType) && !ManagedType.IsValueType;
        public bool IsZeroSized => ManagedType != null && ManagedType.IsValueType &&
                                   ManagedType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Length == 0;

        public bool Equals(ComponentType other) => ManagedType == other.ManagedType && AccessModeType == other.AccessModeType;
        public override bool Equals(object obj) => obj is ComponentType c && Equals(c);
        public override int GetHashCode() => (TypeIndex.Value * 5) ^ (int)AccessModeType;
        public static bool operator ==(ComponentType a, ComponentType b) => a.Equals(b);
        public static bool operator !=(ComponentType a, ComponentType b) => !a.Equals(b);
        public override string ToString() => ManagedType == null ? "None" : $"{ManagedType.Name}{(AccessModeType == AccessMode.ReadWrite ? "" : " [" + AccessModeType + "]")}";
    }

    /// <summary>Per-process component type number (see <see cref="ComponentType.TypeIndex"/>).</summary>
    public struct TypeIndex : IEquatable<TypeIndex>
    {
        public int Value;
        public int Index => Value;
        public static TypeIndex Null => default;
        public bool Equals(TypeIndex other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TypeIndex t && Equals(t);
        public override int GetHashCode() => Value;
        public static bool operator ==(TypeIndex a, TypeIndex b) => a.Value == b.Value;
        public static bool operator !=(TypeIndex a, TypeIndex b) => a.Value != b.Value;
        public override string ToString() => $"TypeIndex({Value})";
    }

    /// <summary>
    /// Looks up <see cref="TypeIndex"/> values. Only the members the game can reach are here.
    /// </summary>
    public static class TypeManager
    {
        public static TypeIndex GetTypeIndex<T>() => ComponentType.IndexOf(typeof(T));
        public static TypeIndex GetTypeIndex(Type type) => ComponentType.IndexOf(type);
        public static Type GetType(TypeIndex typeIndex) => ComponentType.TypeOf(typeIndex);
    }

    /// <summary>
    /// A fixed set of component types. The port has no chunk storage, so an archetype is just
    /// the set a new entity starts with (<c>EntityManager.CreateEntity(archetype)</c>).
    /// </summary>
    public struct EntityArchetype : IEquatable<EntityArchetype>
    {
        internal ComponentType[] Types;

        internal EntityArchetype(ComponentType[] types) { Types = types; }

        public bool Valid => Types != null;

        /// <summary>Port helper: the component types of this archetype (a copy).</summary>
        public ComponentType[] GetComponentTypesManaged() => Types == null ? Array.Empty<ComponentType>() : (ComponentType[])Types.Clone();

        public bool Equals(EntityArchetype other)
        {
            if (Types == null || other.Types == null) return Types == other.Types;
            return Types.Select(t => t.ManagedType).ToHashSet().SetEquals(other.Types.Select(t => t.ManagedType));
        }
        public override bool Equals(object obj) => obj is EntityArchetype a && Equals(a);
        public override int GetHashCode() => Types == null ? 0 : Types.Aggregate(17, (h, t) => h ^ t.TypeIndex.Value * 31);
        public static bool operator ==(EntityArchetype a, EntityArchetype b) => a.Equals(b);
        public static bool operator !=(EntityArchetype a, EntityArchetype b) => !a.Equals(b);
    }
}
