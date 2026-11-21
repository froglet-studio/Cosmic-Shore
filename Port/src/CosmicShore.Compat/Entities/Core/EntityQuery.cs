using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities
{
    [Flags]
    public enum EntityQueryOptions
    {
        Default = 0,
        IncludePrefab = 1,
        IncludeDisabledEntities = 2,
        FilterWriteGroup = 4,
        IgnoreComponentEnabledState = 8,
        IncludeSystems = 16,
        IncludeMetaChunks = 32,
    }

    /// <summary>Describes a query: entities with ALL of <see cref="All"/>, at least one of <see cref="Any"/>, none of <see cref="None"/>.</summary>
    public class EntityQueryDesc
    {
        public ComponentType[] All = Array.Empty<ComponentType>();
        public ComponentType[] Any = Array.Empty<ComponentType>();
        public ComponentType[] None = Array.Empty<ComponentType>();
        public ComponentType[] Disabled = Array.Empty<ComponentType>();
        public ComponentType[] Absent = Array.Empty<ComponentType>();
        public EntityQueryOptions Options = EntityQueryOptions.Default;
    }

    /// <summary>
    /// A live filter over an <see cref="EntityManager"/>'s entities, evaluated on demand
    /// (the port has no chunks to cache). Matching follows the original's defaults: prefabs
    /// and <see cref="Disabled"/> entities are excluded unless the options include them, and a
    /// DISABLED enableable component counts as absent. <c>ReadWrite</c>/<c>ReadOnly</c> types in
    /// the required list are "All"; an <c>Exclude</c> type is "None".
    /// The <c>NativeArray</c>-returning members live in <c>MathDependent/EntityManager.Native.cs</c>.
    /// </summary>
    public partial class EntityQuery : IDisposable
    {
        readonly EntityManager _manager;
        readonly Type[] _all;
        readonly Type[] _any;
        readonly Type[] _none;
        readonly EntityQueryOptions _options;

        internal EntityQuery(EntityManager manager, ComponentType[] all, ComponentType[] any, ComponentType[] none, EntityQueryOptions options = EntityQueryOptions.Default)
        {
            _manager = manager;
            all ??= Array.Empty<ComponentType>();
            _all = all.Where(c => c.AccessModeType != ComponentType.AccessMode.Exclude).Select(c => c.ManagedType).ToArray();
            _none = all.Where(c => c.AccessModeType == ComponentType.AccessMode.Exclude).Select(c => c.ManagedType)
                .Concat((none ?? Array.Empty<ComponentType>()).Select(c => c.ManagedType)).ToArray();
            _any = (any ?? Array.Empty<ComponentType>()).Select(c => c.ManagedType).ToArray();
            _options = options;
        }

        public bool IsDisposed { get; private set; }
        public EntityManager EntityManager => _manager;

        bool Has(EntityStore.Record r, Type t)
        {
            if (!r.Components.ContainsKey(t)) return false;
            if ((_options & EntityQueryOptions.IgnoreComponentEnabledState) != 0) return true;
            return r.DisabledComponents == null || !r.DisabledComponents.Contains(t);
        }

        bool Matches(EntityStore.Record r)
        {
            if ((_options & EntityQueryOptions.IncludePrefab) == 0 && r.Components.ContainsKey(typeof(Prefab)) && !_all.Contains(typeof(Prefab))) return false;
            if ((_options & EntityQueryOptions.IncludeDisabledEntities) == 0 && r.Components.ContainsKey(typeof(Disabled)) && !_all.Contains(typeof(Disabled))) return false;
            foreach (var t in _all) if (!Has(r, t)) return false;
            foreach (var t in _none) if (Has(r, t)) return false;
            if (_any.Length > 0 && !_any.Any(t => Has(r, t))) return false;
            return true;
        }

        internal IEnumerable<Entity> MatchingEntities()
        {
            var result = new List<Entity>();
            foreach (var e in _manager.AllEntities())
                if (_manager.TryGetRecord(e, out var r) && Matches(r)) result.Add(e);
            return result;
        }

        public int CalculateEntityCount() => MatchingEntities().Count();
        public int CalculateEntityCountWithoutFiltering() => CalculateEntityCount();
        public bool IsEmpty => !MatchingEntities().Any();
        public bool IsEmptyIgnoreFilter => IsEmpty;

        /// <summary>Port helper: the matching entities as a managed array.</summary>
        public Entity[] ToEntityArrayManaged() => MatchingEntities().ToArray();

        public bool Matches(Entity entity) => _manager.TryGetRecord(entity, out var r) && Matches(r);

        public Entity GetSingletonEntity()
        {
            var all = MatchingEntities().ToArray();
            if (all.Length != 1) throw new InvalidOperationException($"GetSingletonEntity() requires exactly one entity to match, but there are {all.Length}.");
            return all[0];
        }

        public bool TryGetSingletonEntity<T>(out Entity entity)
        {
            var all = MatchingEntities().ToArray();
            entity = all.Length == 1 ? all[0] : Entity.Null;
            return all.Length == 1;
        }

        public T GetSingleton<T>() where T : struct, IComponentData => _manager.GetComponentData<T>(GetSingletonEntity());
        public void SetSingleton<T>(T value) where T : struct, IComponentData => _manager.SetComponentData(GetSingletonEntity(), value);
        public bool HasSingleton<T>() => MatchingEntities().Count() == 1;

        public void Dispose() => IsDisposed = true;
    }
}
