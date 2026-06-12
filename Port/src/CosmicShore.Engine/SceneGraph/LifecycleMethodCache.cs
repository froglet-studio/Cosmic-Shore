using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Discovers lifecycle methods (Awake/OnEnable/Start/Update/FixedUpdate/LateUpdate/
    /// OnDisable/OnDestroy) by name via reflection — any visibility, zero args — so ported
    /// behaviours keep their original private method signatures verbatim. Compiled to
    /// delegates and cached per concrete type. Trigger messages
    /// (OnTriggerEnter/OnTriggerExit, single <see cref="Collider"/> arg) are discovered the
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
        public int ExecutionOrder;

        static readonly ConcurrentDictionary<Type, LifecycleHooks> Cache = new();

        public static LifecycleHooks For(Type type) => Cache.GetOrAdd(type, Build);

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
                ExecutionOrder = type.GetCustomAttribute<DefaultExecutionOrderAttribute>()?.order ?? 0,
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

                var target = Expression.Parameter(typeof(MonoBehaviour), "mb");
                var call = Expression.Call(Expression.Convert(target, t), method);

                // `IEnumerator Start()` runs as a coroutine (original engine contract).
                if (methodName == "Start" && method.ReturnType == typeof(System.Collections.IEnumerator))
                {
                    var startCoroutine = typeof(MonoBehaviour).GetMethod(nameof(MonoBehaviour.StartCoroutine));
                    var wrapped = Expression.Call(target, startCoroutine, call);
                    return Expression.Lambda<Action<MonoBehaviour>>(wrapped, target).Compile();
                }

                return Expression.Lambda<Action<MonoBehaviour>>(call, target).Compile();
            }
            return null;
        }

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

                var target = Expression.Parameter(typeof(MonoBehaviour), "mb");
                var other = Expression.Parameter(typeof(Collider), "other");
                var call = Expression.Call(Expression.Convert(target, t), method, other);
                return Expression.Lambda<Action<MonoBehaviour, Collider>>(call, target, other).Compile();
            }
            return null;
        }
    }
}
