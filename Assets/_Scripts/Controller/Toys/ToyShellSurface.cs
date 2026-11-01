using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One choice inside a toy, as the <b>app shell</b>'s Toy Box reads it - the only place a toy's
    /// choices are offered (toys have no world body, 2026-11-01).
    ///
    /// <para>An option is either a LEAF (<see cref="Apply"/> does the thing) or a BRANCH
    /// (<see cref="Expand"/> yields the next layer): the Spawn Matrix is kingdom, then species,
    /// then element; a finished painting opens into Share and Repaint.</para>
    /// </summary>
    public sealed class ToyShellOption
    {
        /// <summary>What the option is called.</summary>
        public string Label = "";

        /// <summary>Second line: progress, state, "you are already here". Optional.</summary>
        public string Detail = "";

        /// <summary>
        /// What this option IS, in a sentence or two - shown in the window's description panel
        /// while the row is selected, and while the player is inside the layer it opened. Optional;
        /// empty falls back to the toy's own codex copy. The Spawn Matrix sets it for every species,
        /// so a name like "Lurker" or "Watershed" says what distinguishes it.
        /// </summary>
        public string Description = "";

        /// <summary>The colour the row wears.</summary>
        public Color Accent = Color.white;

        /// <summary>A glyph the configure window draws beside the label - an element's petal, say.
        /// Optional; the style-B card resolves a hull's own icon from a <c>VesselClassType</c>
        /// <see cref="Payload"/> when this is empty.</summary>
        public Sprite Icon;

        /// <summary>
        /// True when this option is the state the player is ALREADY in - the cell they are flying
        /// in, the hull they are flying. Never hidden: the player should see what they are on (and
        /// for the cell selector, choosing it is the freestyle reset).
        /// </summary>
        public bool IsCurrent;

        /// <summary>
        /// True when the effect only means anything with the player at the stick - a wander, a
        /// voyage, a painting. The shell answers by entering freestyle first and then applying,
        /// so one press still gets the player playing it.
        /// </summary>
        public bool RequiresFreestyle;

        /// <summary>
        /// True when PICKING the row is the act - no second press. Set by an option set
        /// (<see cref="ToyOptionSet{T}"/>, the domain changer), whose applies are instant and undone
        /// by picking another row.
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

        /// <summary>
        /// True when the commit stays armed after it fires, so pressing it again does it again - a
        /// Spawn, where each press releases more of the same variant. False (the default) spends
        /// the button: the row is deselected after a commit that made something, because a second
        /// press of Switch or Start on the same row would only redo what just happened.
        /// </summary>
        public bool Repeatable;

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
        /// appears and the option has to state a size. 0 uses the preview's default radius.
        /// </summary>
        public float WatchRadius;

        /// <summary>
        /// Where this option LIVES in the world, for a window to turn its picture onto when the
        /// row is picked, and for Navigate to fly the player to - the newest creature a Spawn Matrix
        /// row released, say. Optional; null (or a null return) means the option has no place of
        /// its own. Resolved late rather than captured, because the place moves.
        /// </summary>
        public Func<Transform> WorldAnchor;

        /// <summary>How far back a window stands to look at <see cref="WorldAnchor"/>. 0 = the preview's default.</summary>
        public float WorldAnchorRadius;

        /// <summary>
        /// Where the player has to fly to PLAY this option once it is going - the ring they thread
        /// next, and the way to thread it. Resolved late (after <see cref="Apply"/>), because the
        /// ring usually does not exist until the press made it: a painting's start gate is built by
        /// starting the painting.
        ///
        /// <para>Optional, and deliberately NOT <see cref="WorldAnchor"/>: an anchor is where an
        /// option LIVES (for a picture to turn onto), an arrival is where its play HAPPENS. A wander
        /// or a voyage answers invalid - it happens wherever the player already is, so there is
        /// nowhere to take them. It is what lets today's activity take the player to the activity
        /// rather than start it somewhere they cannot see.</para>
        /// </summary>
        public Func<ToyArrival> Arrival;

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
        /// mini hull.</para>
        ///
        /// <para>The caller owns the returned object and destroys it. An implementation must
        /// therefore build a NEW one rather than lending out something the toy is still using -
        /// and must not generate anything expensive: the cell selector refuses rather than
        /// triggering a ~34k-lay environment generation, which is the cost the whole
        /// EnvironmentFree boot exists to avoid.</para>
        /// </summary>
        public Func<Transform, GameObject> BuildPreview;

        /// <summary>
        /// The toy's own handle on the thing this option names - the <c>CellConfigDataSO</c>, the
        /// <c>VesselClassType</c>, the <c>PaintingDefinitionSO</c>. Opaque to every reader except
        /// the toy that set it.
        ///
        /// <para>It lets the option carry its subject instead of the toy keeping a second, parallel
        /// list of subjects beside its option list.</para>
        /// </summary>
        public object Payload;

        /// <summary>The next layer down, or null when this option is a leaf.</summary>
        public Func<List<ToyShellOption>> Expand;

        /// <summary>True when selecting this option opens another layer rather than acting.</summary>
        public bool IsBranch => Expand != null;
    }

    /// <summary>
    /// A ring to fly to: its centre, the direction you fly THROUGH it, and its radius. Invalid
    /// (the default) when there is nowhere to go. See <see cref="ToyShellOption.Arrival"/>.
    /// </summary>
    public readonly struct ToyArrival
    {
        public readonly Vector3 Position;
        public readonly Vector3 Direction;
        public readonly float Radius;

        public ToyArrival(Vector3 position, Vector3 direction, float radius)
        {
            Position = position;
            Direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            Radius = radius;
        }

        public bool IsValid => Radius > 0f;
    }

    /// <summary>
    /// A live toy's <b>app-shell face</b>: the choices it offers, and the calls behind them.
    ///
    /// <para>The shell asks the LIVE toy rather than carrying its own table of what each toy can
    /// do. That is the whole design: "change your domain" in the Toy Box modal is literally
    /// <c>DomainChangerToySet.Apply</c>, and a toy authored tomorrow appears in the shell by implementing one interface. A parallel
    /// list of toy actions would be a second authority on the same state - the failure the
    /// single-writer rule exists to prevent.</para>
    ///
    /// <para>Implement it on the object that OWNS the decision: the <see cref="Toy"/> itself for a
    /// matrix toy, the <see cref="ToyOptionSet{T}"/> for an option set.</para>
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
