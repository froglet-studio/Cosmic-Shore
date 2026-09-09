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

        /// <summary>
        /// True when PICKING the row is the act - no second press. Set by a toy whose world form
        /// is a <b>flip-set</b> (<see cref="SwapToySetCoordinator{T}"/>): there, the option IS a
        /// toy you fly through, so there is no select-then-commit step in the world either and the
        /// flat surface would be inventing one.
        ///
        /// <para>False - the default - is the <b>matrix</b> shape: the row selects, and the window's
        /// Switch button commits. That is not fussiness, it is what these applies COST. A cell swap
        /// suctions the world away and grows another behind a veil; a vessel swap despawns and
        /// respawns a networked hull. Firing either from a stray tap in a scroll list is a
        /// multi-second thing the player did not ask for, where a domain change is instant and
        /// undone by picking another row.</para>
        ///
        /// <para>Declared by the toy rather than guessed at by the UI, for the reason
        /// <see cref="ToyDefinitionSO.Category"/> is: the cost of applying is a property of what
        /// the option DOES, and a menu that decided it per toy would be a second opinion about it.</para>
        /// </summary>
        public bool AppliesOnSelect;

        /// <summary>Do the thing. Null on a branch.</summary>
        public Action Apply;

        /// <summary>
        /// The word on the button that commits this option - what pressing it DOES, in the toy's
        /// own terms: "Switch" for a world or a hull you become, "Spawn" for a lifeform released
        /// into the cell, "Start" for a run that takes the player flying. Null falls back to
        /// "Switch". The toy names the verb because the verb is a property of what the option
        /// does, exactly as <see cref="AppliesOnSelect"/> is - a menu that captioned it per toy
        /// would be a second opinion about the toy.
        /// </summary>
        public string CommitVerb;

        /// <summary>
        /// After <see cref="Apply"/>, the thing it MADE - a released creature, a planted seed -
        /// so a window can turn its picture onto it and the player sees the release happen
        /// rather than being told it did. Optional; null (or a null return) means there is
        /// nothing to watch and the picture stays on the toy. Deliberately separate from
        /// <see cref="Apply"/>: an apply that returned an object would make every toy that
        /// makes nothing return null, and the shape of the common case should stay the common case.
        /// </summary>
        public Func<Transform> WatchAfterApply;

        /// <summary>
        /// How far back a window should stand to watch <see cref="WatchAfterApply"/>'s object -
        /// the creature blooms in from zero, so its own bounds say nothing on the frame it
        /// appears and the option has to state a size. 0 leaves the camera at the toy's radius.
        /// </summary>
        public float WatchRadius;

        /// <summary>
        /// Where this option LIVES in the world, for a window to turn its picture onto when the
        /// row is picked - the domain changer's switch for that colour, say. Optional; null (or a
        /// null return) means the option has no place of its own and the picture stays on the
        /// toy. Resolved late rather than captured, because a flip-set re-homes its slots every
        /// time the current option changes.
        /// </summary>
        public Func<Transform> WorldAnchor;

        /// <summary>How far back a window stands to look at <see cref="WorldAnchor"/>. 0 = the toy's radius.</summary>
        public float WorldAnchorRadius;

        /// <summary>The verb, with the fleet default applied.</summary>
        public string EffectiveCommitVerb => string.IsNullOrEmpty(CommitVerb) ? "Switch" : CommitVerb;

        /// <summary>
        /// Build a display model of what this option would GIVE you, parented under the supplied
        /// stage, or return null when the option has nothing to show.
        ///
        /// <para>Optional. It exists because a flat list can say "Blob Cell" and a picture can say
        /// what that IS - and the only thing that knows what an option looks like is the toy that
        /// offers it, exactly as the only thing that knows what it DOES is <see cref="Apply"/>. So
        /// the cell selector hands back its own cached scale model and the vessel changer its own
        /// mini hull: the same builders their world stations use, which is what stops the flat
        /// preview and the station from drifting apart.</para>
        ///
        /// <para>The caller owns the returned object and destroys it. An implementation must
        /// therefore build a NEW one rather than lending out something the toy is still using -
        /// and must not generate anything expensive: the cell selector refuses rather than
        /// triggering a ~34k-lay environment generation, which is the cost the whole
        /// EnvironmentFree boot exists to avoid.</para>
        /// </summary>
        public Func<Transform, GameObject> BuildPreview;

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
