using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A toy whose choices are a small set of <b>options</b> and whose rows apply on select - the
    /// domain changer (Jade/Ruby/Gold). It used to stand one fly-through switch per option you were
    /// not on, each flipping to the one you just left; since 2026-11-01 the set lives only in the
    /// app shell's Toy Box, which lists the whole universe with the current option flagged.
    /// </summary>
    /// <typeparam name="T">The option type - <c>Domains</c>.</typeparam>
    public abstract class ToyOptionSet<T> : MonoBehaviour, IToyShellSurface
    {
        protected ToyContext Context { get; private set; }
        protected ToyDefinitionSO Definition { get; private set; }

        static readonly EqualityComparer<T> Eq = EqualityComparer<T>.Default;

        readonly List<T> _universe = new();
        bool _initialized;

        public void Initialize(ToyDefinitionSO definition, ToyContext context)
        {
            Definition = definition;
            Context = context;

            _universe.Clear();
            foreach (var o in InitialUniverse())
                if (IsValid(o) && !_universe.Contains(o)) _universe.Add(o);

            _initialized = true;
            ToyShellRegistry.Register(this);
        }

        protected virtual void OnDestroy() => ToyShellRegistry.Unregister(this);

        // ── App-shell face ───────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        bool IToyShellSurface.ShellAvailable => _initialized;

        /// <summary>The WHOLE universe, the current option included and flagged (no Apply: it is there to be read).</summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
        {
            bool hasCurrent = TryGetCurrent(out var current) && IsValid(current);
            if (hasCurrent && !_universe.Contains(current)) _universe.Add(current);

            foreach (var option in _universe)
            {
                if (!IsValid(option)) continue;

                var captured = option;
                bool isCurrent = hasCurrent && Eq.Equals(option, current);

                into.Add(new ToyShellOption
                {
                    Label = LabelFor(option),
                    Detail = isCurrent ? "current" : "",
                    Accent = ColorFor(option),
                    IsCurrent = isCurrent,
                    // The row IS the act: the apply is instant and undone by picking another row.
                    AppliesOnSelect = true,
                    Apply = isCurrent ? null : () => Apply(captured),
                });
            }
        }

        // ── Per-kind behaviour ───────────────────────────────────────────────
        protected abstract IReadOnlyList<T> InitialUniverse();
        protected abstract bool TryGetCurrent(out T current);
        protected abstract bool IsValid(T option);
        protected abstract void Apply(T target);

        /// <summary>The option's name - the app shell's row label.</summary>
        protected virtual string LabelFor(T option) => option.ToString();

        /// <summary>The colour this option wears; the toy's accent unless the option has its own (a domain).</summary>
        protected virtual Color ColorFor(T option) => Definition ? Definition.AccentColor : Color.white;
    }
}
