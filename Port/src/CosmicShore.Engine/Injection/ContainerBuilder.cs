using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Injection
{
    /// <summary>Original contract: Reflex.Enums.Lifetime (placeholder-local values, never serialized).</summary>
    public enum Lifetime
    {
        Transient = 0,
        Singleton = 1,
        Scoped = 2,
    }

    /// <summary>Original contract: Reflex.Enums.Resolution (placeholder-local values, never serialized).</summary>
    public enum Resolution
    {
        Lazy = 0,
        Eager = 1,
    }

    /// <summary>
    /// Deferred-registration builder (original contract: Reflex.Core.ContainerBuilder —
    /// the surface AppManager.InstallBindings drives). Registrations accumulate here and
    /// apply to a fresh <see cref="Container"/> at <see cref="Build"/>; factories receive
    /// that built container, so <c>c.Resolve&lt;T&gt;()</c> inside a factory sees every
    /// sibling binding regardless of registration order — the Reflex behavior the
    /// composition root relies on.
    ///
    /// Semantics honored: <see cref="Lifetime.Singleton"/> (the only lifetime the
    /// codebase uses — factories run once, on first resolve) and both resolutions
    /// (<see cref="Resolution.Eager"/> resolves at Build, Lazy on first inject).
    /// <see cref="Lifetime.Transient"/>/<see cref="Lifetime.Scoped"/> fail loud rather
    /// than silently caching — grow them when a ported caller actually needs them.
    /// </summary>
    public sealed class ContainerBuilder
    {
        readonly List<Action<Container>> _registrations = new();
        readonly List<Type> _eagerContracts = new();

        public ContainerBuilder RegisterValue<T>(T value)
        {
            _registrations.Add(c => c.RegisterValue(value));
            return this;
        }

        public ContainerBuilder RegisterFactory<T>(
            Func<Container, T> factory,
            Lifetime lifetime = Lifetime.Singleton,
            Resolution resolution = Resolution.Lazy)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            if (lifetime != Lifetime.Singleton)
                throw new NotSupportedException(
                    $"Lifetime.{lifetime} is not implemented — the container caches factories as " +
                    "lazy singletons (the only lifetime the codebase registers). Grow Container " +
                    "semantics before registering a non-singleton binding.");

            _registrations.Add(c => c.RegisterFactory<T>(cc => factory(cc)));
            if (resolution == Resolution.Eager)
                _eagerContracts.Add(typeof(T));
            return this;
        }

        /// <summary>
        /// Apply every registration to a fresh container (optionally parented for
        /// Bootstrap-root → per-scene scope chains), then run eager resolutions.
        /// </summary>
        public Container Build(Container parent = null)
        {
            var container = new Container(parent);
            foreach (var register in _registrations)
                register(container);
            foreach (var contract in _eagerContracts)
                container.Resolve(contract);
            return container;
        }
    }
}
