using System;
using System.Collections.Generic;

namespace Unity.Entities
{
    /// <summary>
    /// Base of every managed system. Lifecycle mirrors the original (OnCreate once, then
    /// OnStartRunning/OnUpdate/OnStopRunning as it is updated and enabled, OnDestroy on removal),
    /// but nothing schedules it — a system runs when its world or group is <c>Update</c>d.
    /// </summary>
    public abstract class ComponentSystemBase
    {
        bool _running;

        public World World { get; private set; }
        public EntityManager EntityManager => World != null ? World.EntityManager : default;
        public bool Enabled { get; set; } = true;
        internal bool IsInGroup { get; set; }

        protected virtual void OnCreate() { }
        protected virtual void OnStartRunning() { }
        protected virtual void OnStopRunning() { }
        protected virtual void OnDestroy() { }

        public abstract void Update();

        internal void CreateInstance(World world)
        {
            World = world;
            OnCreate();
        }

        internal void DestroyInstance()
        {
            if (_running) { _running = false; OnStopRunning(); }
            OnDestroy();
            World = null;
        }

        /// <summary>Runs <paramref name="body"/> when enabled, raising the start/stop edges as the original does.</summary>
        internal void RunUpdate(Action body)
        {
            if (!Enabled)
            {
                if (_running) { _running = false; OnStopRunning(); }
                return;
            }
            if (!_running) { _running = true; OnStartRunning(); }
            body();
        }

        public EntityQuery GetEntityQuery(params ComponentType[] componentTypes) => EntityManager.CreateEntityQuery(componentTypes);
    }

    public abstract class SystemBase : ComponentSystemBase
    {
        protected abstract void OnUpdate();
        public sealed override void Update() => RunUpdate(OnUpdate);
    }

    /// <summary>A system that updates an ordered list of child systems.</summary>
    public abstract class ComponentSystemGroup : SystemBase
    {
        readonly List<ComponentSystemBase> _children = new();

        public IReadOnlyList<ComponentSystemBase> ManagedSystems => _children;

        public void AddSystemToUpdateList(ComponentSystemBase system)
        {
            if (system == null || _children.Contains(system)) return;
            system.IsInGroup = true;
            _children.Add(system);
        }

        public void RemoveSystemFromUpdateList(ComponentSystemBase system)
        {
            if (_children.Remove(system)) system.IsInGroup = false;
        }

        /// <summary>The port keeps insertion order (no UpdateBefore/After sorting).</summary>
        public void SortSystems() { }

        protected override void OnUpdate()
        {
            foreach (var s in _children.ToArray())
                if (s.World != null) s.Update();
        }
    }

    public class InitializationSystemGroup : ComponentSystemGroup { }
    public class SimulationSystemGroup : ComponentSystemGroup { }
    public class PresentationSystemGroup : ComponentSystemGroup { }

    /// <summary>State handed to an unmanaged <see cref="ISystem"/> callback.</summary>
    public struct SystemState
    {
        public World World { get; internal set; }
        public EntityManager EntityManager => World != null ? World.EntityManager : default;
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// An unmanaged system. The port has no unmanaged-system scheduler; the interface exists so
    /// code declaring one compiles (no system in the game implements it today).
    /// </summary>
    public interface ISystem
    {
        void OnCreate(ref SystemState state) { }
        void OnUpdate(ref SystemState state) { }
        void OnDestroy(ref SystemState state) { }
    }
}
