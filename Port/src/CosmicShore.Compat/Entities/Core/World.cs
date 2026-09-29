using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Unity.Entities
{
    [Flags]
    public enum WorldFlags
    {
        None = 0,
        Live = 1,
        Editor = 1 << 1 | Live,
        Game = 1 << 2 | Live,
        Simulation = 1 << 3 | Game,
        Conversion = 1 << 4,
        Staging = 1 << 5,
        Shadow = 1 << 6,
        Streaming = 1 << 7,
        GameServer = 1 << 8 | Game,
        GameClient = 1 << 9 | Game,
        GameThinClient = 1 << 10 | Game,
    }

    /// <summary>
    /// A world: one <see cref="EntityManager"/> (a real in-memory store) plus the managed
    /// systems created in it. Worlds register in <see cref="All"/> until disposed. There is no
    /// player-loop hookup in the port — <see cref="Update"/> runs the systems only when called.
    /// </summary>
    public class World : IDisposable
    {
        static readonly List<World> s_All = new();
        static ulong s_NextSequence = 1;

        readonly EntityStore _store = new();
        readonly List<ComponentSystemBase> _systems = new();

        /// <summary>The world GameObject-side code talks to (set by bootstrap / <see cref="DefaultWorldInitialization"/>).</summary>
        public static World DefaultGameObjectInjectionWorld { get; set; }

        public static IReadOnlyList<World> All => s_All;

        public string Name { get; }
        public WorldFlags Flags { get; }
        public ulong SequenceNumber { get; }
        public bool IsCreated { get; private set; }
        public EntityManager EntityManager { get; }
        public IReadOnlyList<ComponentSystemBase> Systems => _systems;
        public TimeData Time { get; private set; }

        public World(string name, WorldFlags flags = WorldFlags.Simulation)
        {
            Name = name;
            Flags = flags;
            SequenceNumber = s_NextSequence++;
            IsCreated = true;
            EntityManager = new EntityManager(this, _store);
            s_All.Add(this);
        }

        public void SetTime(TimeData newTimeData) => Time = newTimeData;

        public T GetExistingSystemManaged<T>() where T : ComponentSystemBase => (T)GetExistingSystemManaged(typeof(T));

        public ComponentSystemBase GetExistingSystemManaged(Type type)
        {
            foreach (var s in _systems)
                if (type.IsInstanceOfType(s)) return s;
            return null;
        }

        public T GetOrCreateSystemManaged<T>() where T : ComponentSystemBase, new() =>
            GetExistingSystemManaged<T>() ?? CreateSystemManaged<T>();

        public ComponentSystemBase GetOrCreateSystemManaged(Type type) =>
            GetExistingSystemManaged(type) ?? CreateSystemManaged(type);

        public T CreateSystemManaged<T>() where T : ComponentSystemBase, new() => AddSystemManaged(new T());

        public ComponentSystemBase CreateSystemManaged(Type type) =>
            AddSystemManaged((ComponentSystemBase)Activator.CreateInstance(type, nonPublic: true));

        public T AddSystemManaged<T>(T system) where T : ComponentSystemBase
        {
            if (!IsCreated) throw new InvalidOperationException($"World '{Name}' has been disposed.");
            if (system.World != null) throw new ArgumentException($"{system.GetType().Name} already belongs to world '{system.World.Name}'.");
            _systems.Add(system);
            system.CreateInstance(this);
            return system;
        }

        public void DestroySystemManaged(ComponentSystemBase system)
        {
            if (system == null || !_systems.Remove(system)) return;
            system.DestroyInstance();
        }

        /// <summary>Updates the root-level systems in creation order (groups update their children).</summary>
        public void Update()
        {
            foreach (var s in _systems.ToArray())
                if (s.World == this && !s.IsInGroup) s.Update();
        }

        public void Dispose()
        {
            if (!IsCreated) return;
            foreach (var s in _systems.ToArray().Reverse()) s.DestroyInstance();
            _systems.Clear();
            _store.Clear();
            IsCreated = false;
            s_All.Remove(this);
            if (DefaultGameObjectInjectionWorld == this) DefaultGameObjectInjectionWorld = null;
        }

        public static void DisposeAllWorlds()
        {
            foreach (var w in s_All.ToArray()) w.Dispose();
        }

        public override string ToString() => Name;
    }

    /// <summary>Elapsed/delta time of a world (set by whoever drives its update).</summary>
    public readonly struct TimeData
    {
        public readonly double ElapsedTime;
        public readonly float DeltaTime;
        public TimeData(double elapsedTime, float deltaTime) { ElapsedTime = elapsedTime; DeltaTime = deltaTime; }
    }

    /// <summary>
    /// Implement to take over default-world creation. <see cref="DefaultWorldInitialization.Initialize"/>
    /// discovers one by reflection (as the original does at startup); returning true means
    /// "I created and set <see cref="World.DefaultGameObjectInjectionWorld"/>".
    /// </summary>
    public interface ICustomBootstrap
    {
        bool Initialize(string defaultWorldName);
    }

    /// <summary>
    /// Creates the default world: the three root groups plus the package systems the original
    /// bootstrap adds that the port models — <c>EntitiesGraphicsSystem</c>, which hands visible
    /// entities to the renderer. A custom bootstrap (ICustomBootstrap) may replace all of it.
    /// </summary>
    public static class DefaultWorldInitialization
    {
        /// <summary>Set false to skip the ICustomBootstrap scan (tests).</summary>
        public static bool UseCustomBootstrap = true;

        public static World Initialize(string defaultWorldName, bool editorWorld = false)
        {
            if (UseCustomBootstrap && !editorWorld)
            {
                var bootstrap = CreateBootstrap();
                if (bootstrap != null && bootstrap.Initialize(defaultWorldName))
                {
                    if (World.DefaultGameObjectInjectionWorld == null)
                        throw new InvalidOperationException($"{bootstrap.GetType().Name}.Initialize returned true but did not set World.DefaultGameObjectInjectionWorld.");
                    return World.DefaultGameObjectInjectionWorld;
                }
            }

            var world = new World(defaultWorldName, editorWorld ? WorldFlags.Editor : WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;
            world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            world.GetOrCreateSystemManaged<PresentationSystemGroup>();
            foreach (var t in GetAllSystems(WorldSystemFilterFlags.Default)) world.GetOrCreateSystemManaged(t);
            return world;
        }

        /// <summary>The package system types the original auto-creates that the port models.</summary>
        public static IReadOnlyList<Type> GetAllSystems(WorldSystemFilterFlags filterFlags, bool requireExecuteAlways = false)
            => EditorOnly(filterFlags) ? Array.Empty<Type>() : new[] { typeof(Unity.Rendering.EntitiesGraphicsSystem) };

        static bool EditorOnly(WorldSystemFilterFlags f) => f == WorldSystemFilterFlags.Editor;

        public static void AddSystemsToRootLevelSystemGroups(World world, IEnumerable<Type> systemTypes)
        {
            foreach (var t in systemTypes) world.GetOrCreateSystemManaged(t);
        }

        static ICustomBootstrap CreateBootstrap()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t.IsAbstract || t.IsInterface || !typeof(ICustomBootstrap).IsAssignableFrom(t)) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    return (ICustomBootstrap)Activator.CreateInstance(t);
                }
            }
            return null;
        }
    }
}
