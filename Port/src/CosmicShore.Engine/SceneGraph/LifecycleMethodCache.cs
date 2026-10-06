using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Discovers lifecycle methods (Awake/OnEnable/Start/Update/FixedUpdate/LateUpdate/
    /// OnDisable/OnDestroy) by name via reflection — any visibility, zero args — so ported
    /// behaviours keep their original private method signatures verbatim. Bound once per
    /// concrete type as open-instance delegates (no Expression.Compile, which drops to the slow
    /// LINQ interpreter on iOS's Mono interpreter and is unavailable under NativeAOT). Trigger messages
    /// (OnTriggerEnter/OnTriggerStay/OnTriggerExit, single <see cref="Collider"/> arg) are discovered the
    /// same way for the <see cref="TriggerPass"/>.
    /// </summary>
    internal sealed class LifecycleHooks
    {
        public Action<MonoBehaviour> Awake;
        public Action<MonoBehaviour> OnEnable;
        public Action<MonoBehaviour> Start;
        public Action<MonoBehaviour> Update;
        public Action<MonoBehaviour> FixedUpdate;
        public Action<MonoBehaviour> LateUpdate;
        public Action<MonoBehaviour> OnDisable;
        public Action<MonoBehaviour> OnDestroy;
        public Action<MonoBehaviour, Collider> TriggerEnter;
        public Action<MonoBehaviour, Collider> TriggerExit;
        public Action<MonoBehaviour, Collider> TriggerStay;
        public int ExecutionOrder;

        static readonly ConcurrentDictionary<Type, LifecycleHooks> Cache = new();

        public static LifecycleHooks For(Type type) => Cache.GetOrAdd(type, Build);

        /// <summary>Drops a type's cached hooks so its next instance reads a newly registered execution order.</summary>
        internal static void Invalidate(Type type) => Cache.TryRemove(type, out _);

        static LifecycleHooks Build(Type type)
        {
            var hooks = new LifecycleHooks
            {
                Awake = Find(type, "Awake"),
                OnEnable = Find(type, "OnEnable"),
                Start = Find(type, "Start"),
                Update = Find(type, "Update"),
                FixedUpdate = Find(type, "FixedUpdate"),
                LateUpdate = Find(type, "LateUpdate"),
                OnDisable = Find(type, "OnDisable"),
                OnDestroy = Find(type, "OnDestroy"),
                TriggerEnter = FindTrigger(type, "OnTriggerEnter"),
                TriggerExit = FindTrigger(type, "OnTriggerExit"),
                TriggerStay = FindTrigger(type, "OnTriggerStay"),
                // The project's Script Execution Order settings (.meta) win over the attribute.
                ExecutionOrder = ScriptExecutionOrder.TryGet(type, out var configured) ? configured
                    : type.GetCustomAttribute<DefaultExecutionOrderAttribute>()?.order ?? 0,
            };
            return hooks;
        }

        static Action<MonoBehaviour> Find(Type type, string methodName)
        {
            // Most-derived declaration wins (same as the original engine's message dispatch).
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                MethodInfo method = t.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                if (method is null || method.IsAbstract) continue;

                // `IEnumerator Start()` runs as a coroutine (original engine contract).
                if (methodName == "Start" && method.ReturnType == typeof(System.Collections.IEnumerator))
                    return (Action<MonoBehaviour>)Generic(nameof(BindCoroutine), t)
                        .Invoke(null, new object[] { method.CreateDelegate(typeof(Func<,>).MakeGenericType(t, typeof(System.Collections.IEnumerator))) });

                if (method.ReturnType == typeof(void))
                    return (Action<MonoBehaviour>)Generic(nameof(BindAction), t)
                        .Invoke(null, new object[] { method.CreateDelegate(typeof(Action<>).MakeGenericType(t)) });

                // A message with a return value (rare): the value is ignored, as Unity ignores it.
                return mb => method.Invoke(mb, null);
            }
            return null;
        }

        static MethodInfo Generic(string name, Type t) =>
            typeof(LifecycleHooks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(t);

        // Instantiated over reference types only, so they share one compiled body (AOT-friendly).
        static Action<MonoBehaviour> BindAction<T>(Action<T> call) where T : MonoBehaviour => mb => call((T)mb);
        static Action<MonoBehaviour> BindCoroutine<T>(Func<T, System.Collections.IEnumerator> start) where T : MonoBehaviour => mb => mb.StartCoroutine(start((T)mb));
        static Action<MonoBehaviour, Collider> BindTrigger<T>(Action<T, Collider> call) where T : MonoBehaviour => (mb, other) => call((T)mb, other);

        /// <summary>
        /// Trigger-message variant of <see cref="Find"/>: any visibility, exactly one
        /// <see cref="Collider"/> parameter, most-derived declaration wins.
        /// </summary>
        static Action<MonoBehaviour, Collider> FindTrigger(Type type, string methodName)
        {
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                MethodInfo method = t.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    binder: null,
                    types: new[] { typeof(Collider) },
                    modifiers: null);

                if (method is null || method.IsAbstract) continue;

                if (method.ReturnType == typeof(void))
                    return (Action<MonoBehaviour, Collider>)Generic(nameof(BindTrigger), t)
                        .Invoke(null, new object[] { method.CreateDelegate(typeof(Action<,>).MakeGenericType(t, typeof(Collider))) });
                return (mb, other) => method.Invoke(mb, new object[] { other });
            }
            return null;
        }
    }
}
