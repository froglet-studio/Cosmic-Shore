using System;
using System.Collections.Generic;

namespace Unity.Entities
{
    /// <summary>
    /// Creates, destroys and edits the entities of one <see cref="World"/>. A value type, as in
    /// the original: copies are cheap handles onto the same store, so
    /// <c>var em = world.EntityManager; em.SetComponentData(…)</c> writes the world's data.
    ///
    /// Behavior is REAL (an in-memory component store — see <see cref="EntityStore"/>);
    /// only rendering is absent: nothing draws an entity. Error semantics follow the
    /// original: touching a destroyed/foreign entity or reading/writing a component the
    /// entity does not have throws <see cref="ArgumentException"/>.
    /// The <c>NativeArray</c>-taking overloads live in <c>MathDependent/EntityManager.Native.cs</c>.
    /// </summary>
    public partial struct EntityManager : IEquatable<EntityManager>
    {
        readonly EntityStore _store;
        readonly World _world;

        internal EntityManager(World world, EntityStore store)
        {
            _world = world;
            _store = store;
        }

        internal EntityStore StoreOrNull => _store;

        EntityStore Store => _store ?? throw new InvalidOperationException("EntityManager is default-constructed; take it from World.EntityManager.");

        public World World => _world;
        public bool IsCreated => _store != null && _world != null && _world.IsCreated;

        /// <summary>Diagnostics (<c>em.Debug.EntityCount</c>).</summary>
        public EntityManagerDebug Debug => new EntityManagerDebug(Store);

        // ---------------------------------------------------------------- lifetime

        public Entity CreateEntity() => Store.Create();

        public Entity CreateEntity(params ComponentType[] types)
        {
            var e = Store.Create();
            if (types != null)
                foreach (var t in types) AddComponent(e, t);
            return e;
        }

        public Entity CreateEntity(EntityArchetype archetype) => CreateEntity(archetype.Types);

        public EntityArchetype CreateArchetype(params ComponentType[] types) => new EntityArchetype(types == null ? Array.Empty<ComponentType>() : (ComponentType[])types.Clone());

        public bool Exists(Entity entity) => _store != null && _store.Exists(entity);

        /// <summary>Destroys the entity (and every entity in its <see cref="LinkedEntityGroup"/>). A dead handle is a no-op.</summary>
        public void DestroyEntity(Entity entity)
        {
            var store = Store;
            if (!store.TryGet(entity, out var record)) return;
            if (record.Components.TryGetValue(typeof(LinkedEntityGroup), out var boxed) && boxed is BufferStorage<LinkedEntityGroup> group)
            {
                var members = group.Items.ToArray();
                foreach (var m in members)
                    if (m.Value != entity) store.Destroy(m.Value);
            }
            store.Destroy(entity);
        }

        public void DestroyEntity(EntityQuery query)
        {
            foreach (var e in query.MatchingEntities()) DestroyEntity(e);
        }

        /// <summary>
        /// Clones <paramref name="srcEntity"/>: every component copied by value (buffers
        /// deep-copied, enable bits copied), <see cref="Prefab"/> stripped. A source with a
        /// <see cref="LinkedEntityGroup"/> clones the whole group; the clone's group lists the clones.
        /// Entity references held inside other components are NOT remapped (port limitation).
        /// </summary>
        public Entity Instantiate(Entity srcEntity)
        {
            var store = Store;
            var src = store.Get(srcEntity);
            if (src.Components.TryGetValue(typeof(LinkedEntityGroup), out var boxed) && boxed is BufferStorage<LinkedEntityGroup> group && group.Items.Count > 0)
            {
                var members = group.Items.ToArray();
                var clones = new Entity[members.Length];
                Entity root = default;
                for (int i = 0; i < members.Length; i++)
                {
                    clones[i] = CloneOne(store, members[i].Value);
                    if (members[i].Value == srcEntity) root = clones[i];
                }
                if (root == Entity.Null) root = CloneOne(store, srcEntity);
                var rootGroup = new BufferStorage<LinkedEntityGroup>();
                foreach (var c in clones) rootGroup.Items.Add(new LinkedEntityGroup { Value = c });
                store.Get(root).Components[typeof(LinkedEntityGroup)] = rootGroup;
                return root;
            }
            return CloneOne(store, srcEntity);
        }

        /// <summary>Port-friendly batch overload: <paramref name="count"/> clones into a managed array.</summary>
        public Entity[] InstantiateManaged(Entity srcEntity, int count)
        {
            var result = new Entity[count];
            for (int i = 0; i < count; i++) result[i] = Instantiate(srcEntity);
            return result;
        }

        static Entity CloneOne(EntityStore store, Entity source)
        {
            var src = store.Get(source);
            var dst = store.Create();
            var record = store.Get(dst);
            foreach (var kv in src.Components)
            {
                if (kv.Key == typeof(Prefab)) continue;
                record.Components[kv.Key] = EntityStore.CopyValue(kv.Value);
            }
            if (src.DisabledComponents != null && src.DisabledComponents.Count > 0)
                record.DisabledComponents = new HashSet<Type>(src.DisabledComponents);
            return dst;
        }

        // ---------------------------------------------------------------- structural

        public bool HasComponent<T>(Entity entity) => Exists(entity) && Store.Get(entity).Components.ContainsKey(typeof(T));
        public bool HasComponent(Entity entity, ComponentType componentType) => Exists(entity) && Store.Get(entity).Components.ContainsKey(componentType.ManagedType);

        /// <summary>Adds <typeparamref name="T"/> at its default value. Returns false (no-op) if already present.</summary>
        public bool AddComponent<T>(Entity entity) => AddComponent(entity, ComponentType.ReadWrite<T>());

        public bool AddComponent(Entity entity, ComponentType componentType)
        {
            var record = Store.Get(entity);
            var type = componentType.ManagedType ?? throw new ArgumentException("ComponentType has no type.");
            if (record.Components.ContainsKey(type)) return false;
            record.Components[type] = DefaultValueFor(type);
            Store.MarkStructural();
            return true;
        }

        public void AddComponent(Entity entity, ComponentTypeSet types)
        {
            foreach (var t in types.Types) AddComponent(entity, t);
        }

        public void AddComponent(EntityQuery query, ComponentType componentType)
        {
            foreach (var e in query.MatchingEntities()) AddComponent(e, componentType);
        }

        static object DefaultValueFor(Type type)
        {
            if (typeof(IBufferElementData).IsAssignableFrom(type) && type.IsValueType)
                return Activator.CreateInstance(typeof(BufferStorage<>).MakeGenericType(type));
            if (type.IsValueType) return Activator.CreateInstance(type);
            return type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type) : null;
        }

        /// <summary>Adds (or, if already present, overwrites) a component with a value.</summary>
        public bool AddComponentData<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            var record = Store.Get(entity);
            bool added = !record.Components.ContainsKey(typeof(T));
            record.Components[typeof(T)] = componentData;
            if (added) Store.MarkStructural();
            return added;
        }

        public bool RemoveComponent<T>(Entity entity) => RemoveComponent(entity, ComponentType.ReadWrite<T>());

        public bool RemoveComponent(Entity entity, ComponentType componentType)
        {
            if (!Store.TryGet(entity, out var record)) return false;
            var type = componentType.ManagedType;
            if (!record.Components.Remove(type)) return false;
            record.DisabledComponents?.Remove(type);
            Store.MarkStructural();
            return true;
        }

        public void RemoveComponent(Entity entity, ComponentTypeSet types)
        {
            foreach (var t in types.Types) RemoveComponent(entity, t);
        }

        public void RemoveComponent(EntityQuery query, ComponentType componentType)
        {
            foreach (var e in query.MatchingEntities()) RemoveComponent(e, componentType);
        }

        // ---------------------------------------------------------------- data

        public T GetComponentData<T>(Entity entity) where T : struct, IComponentData
        {
            var record = Store.Get(entity);
            if (!record.Components.TryGetValue(typeof(T), out var boxed))
                throw MissingComponent(typeof(T), entity);
            return boxed is T value ? value : default; // zero-sized tags are stored as default(T)
        }

        public void SetComponentData<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            var record = Store.Get(entity);
            if (!record.Components.ContainsKey(typeof(T)))
                throw MissingComponent(typeof(T), entity);
            record.Components[typeof(T)] = componentData;
        }

        internal static ArgumentException MissingComponent(Type type, Entity entity) =>
            new ArgumentException($"A component with type:{type.Name} has not been added to the entity {entity}.");

        // Managed (class) components ------------------------------------------------

        public void AddComponentObject(Entity entity, object componentData)
        {
            if (componentData == null) throw new ArgumentNullException(nameof(componentData));
            var record = Store.Get(entity);
            bool added = !record.Components.ContainsKey(componentData.GetType());
            record.Components[componentData.GetType()] = componentData;
            if (added) Store.MarkStructural();
        }

        public T GetComponentObject<T>(Entity entity)
        {
            var record = Store.Get(entity);
            if (!record.Components.TryGetValue(typeof(T), out var boxed)) throw MissingComponent(typeof(T), entity);
            return (T)boxed;
        }

        internal object GetBoxed(Entity entity, Type type)
        {
            var record = Store.Get(entity);
            if (!record.Components.TryGetValue(type, out var boxed)) throw MissingComponent(type, entity);
            return boxed;
        }

        internal void SetBoxed(Entity entity, Type type, object value, bool addIfMissing)
        {
            var record = Store.Get(entity);
            bool had = record.Components.ContainsKey(type);
            if (!had && !addIfMissing) throw MissingComponent(type, entity);
            record.Components[type] = value;
            if (!had) Store.MarkStructural();
        }

        // Enableable ------------------------------------------------------------

        public bool IsComponentEnabled<T>(Entity entity) where T : IEnableableComponent => IsComponentEnabled(entity, ComponentType.ReadWrite<T>());

        public bool IsComponentEnabled(Entity entity, ComponentType componentType)
        {
            var record = Store.Get(entity);
            if (!record.Components.ContainsKey(componentType.ManagedType)) throw MissingComponent(componentType.ManagedType, entity);
            return record.DisabledComponents == null || !record.DisabledComponents.Contains(componentType.ManagedType);
        }

        public void SetComponentEnabled<T>(Entity entity, bool value) where T : IEnableableComponent => SetComponentEnabled(entity, ComponentType.ReadWrite<T>(), value);

        public void SetComponentEnabled(Entity entity, ComponentType componentType, bool value)
        {
            var record = Store.Get(entity);
            var type = componentType.ManagedType;
            if (!typeof(IEnableableComponent).IsAssignableFrom(type))
                throw new ArgumentException($"{type.Name} is not an IEnableableComponent.");
            if (!record.Components.ContainsKey(type)) throw MissingComponent(type, entity);
            if (value) record.DisabledComponents?.Remove(type);
            else (record.DisabledComponents ??= new HashSet<Type>()).Add(type);
        }

        // Entity enable (Disabled tag, propagated through LinkedEntityGroup) ------

        public bool IsEnabled(Entity entity) => !HasComponent<Disabled>(entity);

        public void SetEnabled(Entity entity, bool enabled)
        {
            var targets = new List<Entity> { entity };
            if (HasComponent<LinkedEntityGroup>(entity))
                foreach (var m in GetBuffer<LinkedEntityGroup>(entity))
                    if (m.Value != entity) targets.Add(m.Value);
            foreach (var t in targets)
            {
                if (!Exists(t)) continue;
                if (enabled) RemoveComponent<Disabled>(t);
                else AddComponent<Disabled>(t);
            }
        }

        // Shared components -------------------------------------------------------

        public void AddSharedComponentManaged<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
            => SetBoxed(entity, typeof(T), componentData, addIfMissing: true);

        public void AddSharedComponent<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
            => SetBoxed(entity, typeof(T), componentData, addIfMissing: true);

        public void SetSharedComponentManaged<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
            => SetBoxed(entity, typeof(T), componentData, addIfMissing: false);

        public void SetSharedComponent<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
            => SetBoxed(entity, typeof(T), componentData, addIfMissing: false);

        public T GetSharedComponentManaged<T>(Entity entity) where T : struct, ISharedComponentData
            => GetBoxed(entity, typeof(T)) is T v ? v : default;

        public T GetSharedComponent<T>(Entity entity) where T : struct, ISharedComponentData
            => GetBoxed(entity, typeof(T)) is T v ? v : default;

        // Buffers ------------------------------------------------------------------

        public DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : struct, IBufferElementData
        {
            var record = Store.Get(entity);
            if (record.Components.TryGetValue(typeof(T), out var existing) && existing is BufferStorage<T> have)
            {
                have.Items.Clear(); // the original replaces an existing buffer with an empty one
                return new DynamicBuffer<T>(have);
            }
            var storage = new BufferStorage<T>();
            record.Components[typeof(T)] = storage;
            Store.MarkStructural();
            return new DynamicBuffer<T>(storage);
        }

        public DynamicBuffer<T> GetBuffer<T>(Entity entity, bool isReadOnly = false) where T : struct, IBufferElementData
        {
            var record = Store.Get(entity);
            if (!record.Components.TryGetValue(typeof(T), out var boxed) || boxed is not BufferStorage<T> storage)
                throw MissingComponent(typeof(T), entity);
            return new DynamicBuffer<T>(storage);
        }

        public bool HasBuffer<T>(Entity entity) where T : struct, IBufferElementData => HasComponent<T>(entity);

        // Names / queries / misc -----------------------------------------------------

        public void SetName(Entity entity, string name) => Store.Get(entity).Name = name;
        public string GetName(Entity entity) => Store.TryGet(entity, out var r) ? r.Name ?? string.Empty : string.Empty;

        public EntityQuery CreateEntityQuery(params ComponentType[] requiredComponents) => new EntityQuery(this, requiredComponents, null, null);

        public EntityQuery CreateEntityQuery(params EntityQueryDesc[] queriesDesc)
        {
            if (queriesDesc == null || queriesDesc.Length == 0) return new EntityQuery(this, null, null, null);
            var d = queriesDesc[0];
            return new EntityQuery(this, d.All, d.Any, d.None, d.Options);
        }

        /// <summary>A query matching every (non-prefab, enabled) entity.</summary>
        public EntityQuery UniversalQuery => new EntityQuery(this, null, null, null);

        /// <summary>Port helper: every live entity, including prefabs and disabled ones.</summary>
        public IEnumerable<Entity> GetAllEntitiesManaged() => Store.All();

        internal IEnumerable<Entity> AllEntities() => Store.All();
        internal bool TryGetRecord(Entity e, out EntityStore.Record record) => Store.TryGet(e, out record);

        /// <summary>No jobs run in the port, so there is never anything to complete.</summary>
        public void CompleteAllTrackedJobs() { }
        public void CompleteDependencyBeforeRO<T>() { }
        public void CompleteDependencyBeforeRW<T>() { }

        public bool Equals(EntityManager other) => ReferenceEquals(_store, other._store);
        public override bool Equals(object obj) => obj is EntityManager m && Equals(m);
        public override int GetHashCode() => _store?.GetHashCode() ?? 0;
        public static bool operator ==(EntityManager a, EntityManager b) => a.Equals(b);
        public static bool operator !=(EntityManager a, EntityManager b) => !a.Equals(b);
    }

    /// <summary>Diagnostics view of an <see cref="EntityManager"/>.</summary>
    public readonly struct EntityManagerDebug
    {
        readonly EntityStore _store;
        internal EntityManagerDebug(EntityStore store) { _store = store; }
        public int EntityCount => _store.Count;
    }

    /// <summary>A set of component types for batch structural changes.</summary>
    public readonly struct ComponentTypeSet
    {
        internal readonly ComponentType[] Types;
        public ComponentTypeSet(params ComponentType[] types) { Types = types ?? Array.Empty<ComponentType>(); }
        public ComponentTypeSet(ComponentType a) : this(new[] { a }) { }
        public ComponentTypeSet(ComponentType a, ComponentType b) : this(new[] { a, b }) { }
        public ComponentTypeSet(ComponentType a, ComponentType b, ComponentType c) : this(new[] { a, b, c }) { }
        public int Length => Types?.Length ?? 0;
    }

    /// <summary>
    /// Managed-component (class <see cref="IComponentData"/>) overloads, as the original ships
    /// them — resolved when the struct-constrained instance method does not apply.
    /// </summary>
    public static class EntityManagerManagedComponentExtensions
    {
        public static void AddComponentData<T>(this EntityManager manager, Entity entity, T componentData) where T : class, IComponentData
            => manager.SetBoxed(entity, typeof(T), componentData, addIfMissing: true);

        public static T GetComponentData<T>(this EntityManager manager, Entity entity) where T : class, IComponentData
            => (T)manager.GetBoxed(entity, typeof(T));

        public static void SetComponentData<T>(this EntityManager manager, Entity entity, T componentData) where T : class, IComponentData
            => manager.SetBoxed(entity, typeof(T), componentData, addIfMissing: false);
    }
}
