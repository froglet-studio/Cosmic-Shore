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
    /// delegates and cached per concrete type.
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
                return Expression.Lambda<Action<MonoBehaviour>>(call, target).Compile();
            }
            return null;
        }
    }
}
