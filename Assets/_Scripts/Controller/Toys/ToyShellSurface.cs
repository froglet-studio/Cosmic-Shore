using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One choice inside a toy, as the <b>app shell</b> reads it - the flat 2D twin of a station
    /// you would fly through in freestyle.
    ///
    /// <para>An option is either a LEAF (<see cref="Apply"/> does the thing) or a BRANCH
    /// (<see cref="Expand"/> yields the next layer). The two shapes exist because that is exactly
    /// what a toy already is in the world: a <see cref="MatrixToy"/> unfolds into stations, and the
    /// Lifeform Matrix unfolds again into species and then variants. Modelling the shell as one
    /// flat list would have flattened a tree the player already knows is a tree.</para>
    /// </summary>
    public sealed class ToyShellOption
    {
        /// <summary>What the option is called - the station's own label.</summary>
        public string Label = "";

        /// <summary>Second line: progress, state, "you are already here". Optional.</summary>
        public string Detail = "";

        /// <summary>The colour the station wears in the world.</summary>
        public Color Accent = Color.white;

        /// <summary>
        /// True when this option is the state the player is ALREADY in - the cell they are flying
        /// in, the hull they are flying. Never hidden: the diegetic toys show it too (the cell
        /// selector haloes the current world, and flying it is the freestyle reset).
        /// </summary>
        public bool IsCurrent;

        /// <summary>
        /// True when the effect only means anything with the player at the stick - a wander, a
        /// voyage, a painting. The shell answers by entering freestyle first and then applying,
        /// so one press still gets the player playing it.
        /// </summary>
        public bool RequiresFreestyle;

        /// <summary>Do the thing. Null on a branch.</summary>
        public Action Apply;

        /// <summary>The next layer down, or null when this option is a leaf.</summary>
        public Func<List<ToyShellOption>> Expand;

        /// <summary>True when selecting this option opens another layer rather than acting.</summary>
        public bool IsBranch => Expand != null;
    }

    /// <summary>
    /// A live toy's <b>app-shell face</b>: the same choices it offers in the world, and the same
    /// calls behind them.
    ///
    /// <para>The shell asks the LIVE toy rather than carrying its own table of what each toy can
    /// do. That is the whole design: "change your domain" in the Toy Box modal is literally
    /// <c>DomainChangerToySet.Apply</c>, the call the ring makes, so the two surfaces cannot drift
    /// and a toy authored tomorrow appears in the shell by implementing one interface. A parallel
    /// list of toy actions would be a second authority on the same state - the failure the
    /// single-writer rule exists to prevent.</para>
    ///
    /// <para>Implement it on the object that OWNS the decision: the <see cref="Toy"/> itself for a
    /// matrix toy, the <see cref="SwapToySetCoordinator{T}"/> for a flip-set (the individual
    /// <see cref="SwapToy"/> slots hold no option state).</para>
    /// </summary>
    public interface IToyShellSurface
    {
        /// <summary>The definition this surface speaks for - name, tagline, accent, category.</summary>
        ToyDefinitionSO ShellDefinition { get; }

        /// <summary>
        /// False while the toy cannot answer - mid cell-swap, mid vessel-swap, no context yet. The
        /// shell draws the card greyed rather than showing an empty or lying option list.
        /// </summary>
        bool ShellAvailable { get; }

        /// <summary>
        /// Fill <paramref name="into"/> (already cleared) with this toy's top layer of choices.
        /// Called on demand, never per frame.
        /// </summary>
        void BuildShellOptions(List<ToyShellOption> into);
    }

    /// <summary>
    /// The live toys the app shell can draw, in toybox placement order.
    ///
    /// <para>A registry rather than a scan: <see cref="Toy"/>s are built at runtime by
    /// <see cref="ToyboxController"/> under one root, so there is no serialized list to find them
    /// in, and a <c>FindObjectsByType</c> sweep per modal open would be both slower and blind to
    /// the coordinator sets (which are not <see cref="Toy"/>s at all).</para>
    /// </summary>
    public static class ToyShellRegistry
    {
        static readonly List<IToyShellSurface> _surfaces = new();

        /// <summary>Raised when a surface is added or removed, so an open modal can redraw.</summary>
        public static event Action OnChanged;

        public static IReadOnlyList<IToyShellSurface> Surfaces => _surfaces;

        public static void Register(IToyShellSurface surface)
        {
            if (surface == null || _surfaces.Contains(surface)) return;
            _surfaces.Add(surface);
            OnChanged?.Invoke();
        }

        public static void Unregister(IToyShellSurface surface)
        {
            if (surface == null) return;
            if (!_surfaces.Remove(surface)) return;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// A static list outlives a domain reload with Enter Play Mode Options on, and would hand
        /// the next session a set of destroyed toys. Cleared at subsystem registration for the
        /// same reason <c>EndConditionOverridesSO</c> clears its run override there.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _surfaces.Clear();
            OnChanged = null;
        }
    }
}
