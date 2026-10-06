using System;

namespace Unity.Entities
{
    /// <summary>
    /// Random access to one component type by entity (Unity.Entities.ComponentLookup), handed out by
    /// <see cref="ComponentSystemBase.GetComponentLookup{T}"/>. A copy is a handle onto its world's
    /// store, so a lookup captured in a job reads and writes live data. Jobs run inline in the port,
    /// so there is no safety handle and no <c>Update</c> step to forget; writes through a read-only
    /// lookup succeed, as in a Unity player build (only the editor's safety checks reject them).
    /// </summary>
    public struct ComponentLookup<T> where T : unmanaged, IComponentData
    {
        readonly EntityManager _manager;

        internal ComponentLookup(EntityManager manager, bool isReadOnly)
        {
            _manager = manager;
            IsReadOnly = isReadOnly;
        }

        public bool IsReadOnly { get; }

        public bool HasComponent(Entity entity) => _manager.HasComponent<T>(entity);

        public bool HasComponent(Entity entity, out bool entityExists)
        {
            entityExists = _manager.Exists(entity);
            return entityExists && _manager.HasComponent<T>(entity);
        }

        public bool EntityExists(Entity entity) => _manager.Exists(entity);

        public bool TryGetComponent(Entity entity, out T component)
        {
            if (_manager.HasComponent<T>(entity)) { component = _manager.GetComponentData<T>(entity); return true; }
            component = default;
            return false;
        }

        /// <summary>The entity's component; throws like EntityManager.GetComponentData when it has none.</summary>
        public T this[Entity entity]
        {
            get => _manager.GetComponentData<T>(entity);
            set => _manager.SetComponentData(entity, value);
        }

        /// <summary>Refreshes the lookup after structural changes; nothing to refresh here.</summary>
        public void Update(SystemBase system) { }

        /// <summary>Refreshes the lookup after structural changes; nothing to refresh here.</summary>
        public void Update(ref SystemState state) { }
    }
}
