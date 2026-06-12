using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace CosmicShore.Engine.Injection
{
    /// <summary>Marks a field or settable property for container injection.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute { }

    /// <summary>Implemented by composition roots (the port of the Reflex installer contract).</summary>
    public interface IInstaller
    {
        void InstallBindings(Container container);
    }

    /// <summary>
    /// First-party DI container replacing Reflex, preserving the registration semantics
    /// the codebase was built on: <see cref="RegisterValue{T}"/> for shared assets,
    /// <see cref="RegisterFactory{T}"/> for lazy singletons, <c>[Inject]</c> member
    /// injection, and parent-chained scopes (Bootstrap root → per-scene children).
    /// Missing bindings throw — fail loud, no silent nulls.
    /// </summary>
    public sealed class Container
    {
        sealed class Binding
        {
            public object Instance;
            public Func<Container, object> Factory;
            public bool Created;
        }

        sealed class InjectionPlan
        {
            public FieldInfo[] Fields;
            public PropertyInfo[] Properties;
        }

        static readonly ConcurrentDictionary<Type, InjectionPlan> Plans = new();

        readonly Container _parent;
        readonly Dictionary<Type, Binding> _bindings = new();

        public Container(Container parent = null) { _parent = parent; }

        public Container CreateChild() => new(this);

        // ── Registration ─────────────────────────────────────────────

        /// <summary>Bind an existing instance to contract type T.</summary>
        public void RegisterValue<T>(T value) => RegisterValue(typeof(T), value);

        public void RegisterValue(Type contract, object value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value),
                $"RegisterValue({contract.Name}) requires a non-null instance.");
            if (!contract.IsInstanceOfType(value))
                throw new ArgumentException($"{value.GetType().Name} is not assignable to {contract.Name}.");
            _bindings[contract] = new Binding { Instance = value, Created = true };
        }

        /// <summary>Bind a lazy singleton: the factory runs once, on first resolve.</summary>
        public void RegisterFactory<T>(Func<Container, T> factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            _bindings[typeof(T)] = new Binding { Factory = c => factory(c) };
        }

        public void RegisterFactory<T>(Func<T> factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            _bindings[typeof(T)] = new Binding { Factory = _ => factory() };
        }

        public bool IsRegistered<T>() => IsRegistered(typeof(T));
        public bool IsRegistered(Type contract)
            => _bindings.ContainsKey(contract) || _parent?.IsRegistered(contract) == true;

        // ── Resolution ───────────────────────────────────────────────

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public object Resolve(Type contract)
        {
            if (TryResolve(contract, out object value)) return value;
            throw new InvalidOperationException(
                $"No binding for {contract.Name}. Register it in the composition root (RegisterValue/RegisterFactory).");
        }

        public bool TryResolve<T>(out T value)
        {
            bool found = TryResolve(typeof(T), out object boxed);
            value = found ? (T)boxed : default;
            return found;
        }

        public bool TryResolve(Type contract, out object value)
        {
            // E15: implicit self-binding — `[Inject] Container _container` resolves to the
            // nearest scope (the container doing the injecting), the Reflex contract the
            // ported spawners rely on. Explicit registrations of Container are shadowed.
            if (contract == typeof(Container))
            {
                value = this;
                return true;
            }

            for (Container c = this; c is not null; c = c._parent)
            {
                if (!c._bindings.TryGetValue(contract, out var binding)) continue;
                if (!binding.Created)
                {
                    binding.Instance = binding.Factory(c);
                    binding.Created = true;
                }
                value = binding.Instance;
                return true;
            }
            value = null;
            return false;
        }

        // ── Injection ────────────────────────────────────────────────

        /// <summary>Populate every [Inject] field/property on the target (including private/inherited).</summary>
        public void Inject(object target)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            var plan = Plans.GetOrAdd(target.GetType(), BuildPlan);

            foreach (var field in plan.Fields)
                field.SetValue(target, Resolve(field.FieldType));
            foreach (var property in plan.Properties)
                property.SetValue(target, Resolve(property.PropertyType));
        }

        /// <summary>Inject all components on a GameObject, optionally through its whole hierarchy.</summary>
        public void InjectGameObject(GameObject root, bool recursive = true)
        {
            foreach (var component in root.Components)
                if (HasInjectables(component.GetType()))
                    Inject(component);

            if (!recursive) return;
            foreach (var child in root.transform.Children)
                InjectGameObject(child.gameObject, recursive: true);
        }

        static bool HasInjectables(Type type)
        {
            var plan = Plans.GetOrAdd(type, BuildPlan);
            return plan.Fields.Length > 0 || plan.Properties.Length > 0;
        }

        static InjectionPlan BuildPlan(Type type)
        {
            var fields = new List<FieldInfo>();
            var properties = new List<PropertyInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (Type t = type; t is not null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                    if (field.IsDefined(typeof(InjectAttribute), inherit: false))
                        fields.Add(field);

                foreach (var property in t.GetProperties(flags))
                    if (property.IsDefined(typeof(InjectAttribute), inherit: false) && property.CanWrite)
                        properties.Add(property);
            }

            return new InjectionPlan { Fields = fields.ToArray(), Properties = properties.ToArray() };
        }
    }
}
