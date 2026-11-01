using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Base class for a freestyle <b>Toy</b>: the live object behind one entry of the app shell's
    /// Toy Box. Toys have no score and no end condition; they are things to play with indefinitely.
    ///
    /// <para>A toy has NO world body (2026-11-01, Garrett: toys are selected only through the app
    /// shell's Toy Box). It is never flown into: nothing to see, no trigger, no ring. What it offers
    /// and what pressing an option does is its <see cref="IToyShellSurface"/>; what it does once
    /// going (a painting's stroke gates, a wander's way home) is play, built from
    /// <see cref="ToyStation"/>s.</para>
    /// </summary>
    public abstract class Toy : MonoBehaviour
    {
        protected ToyDefinitionSO Definition { get; private set; }
        protected ToyContext Context { get; private set; }

        /// <summary>
        /// Where the toybox anchored this toy (inside the cell, near the membrane). Not drawn: some
        /// toys still place their PLAY from it - a painting's monument anchors, say.
        /// </summary>
        protected ToyPlacement Placement { get; private set; }

        /// <summary>One-line label used in logs.</summary>
        public string DisplayName => Definition ? Definition.DisplayName : name;

        /// <summary>Wire up the toy and, when it has an app-shell face, register it with the Toy Box.</summary>
        public void Initialize(ToyDefinitionSO definition, ToyContext context, ToyPlacement placement)
        {
            Definition = definition;
            Context = context;
            Placement = placement;

            OnInitialized();

            // The one method every toy passes through, so there is nothing per-toy to remember.
            if (this is IToyShellSurface shellSurface)
                ToyShellRegistry.Register(shellSurface);
        }

        /// <summary>
        /// Virtual so every subclass EXTENDS teardown rather than hiding it - Unity invokes only
        /// the most-derived <c>OnDestroy</c>, and a hiding declaration would leave this toy in the
        /// app shell's registry as a destroyed reference. Overrides must call base.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (this is IToyShellSurface shellSurface)
                ToyShellRegistry.Unregister(shellSurface);
        }

        /// <summary>Hook for subclasses to do extra setup after <see cref="Initialize"/>.</summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// The local player's vessel, or null when there isn't one to act on (no player yet,
        /// destroyed mid-swap).
        /// </summary>
        protected IVesselStatus ResolveLocalVessel()
        {
            var status = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus;
            if (status == null) return null;
            if (status is Object uo && !uo) return null;   // destroyed mid-swap
            return status;
        }
    }
}
