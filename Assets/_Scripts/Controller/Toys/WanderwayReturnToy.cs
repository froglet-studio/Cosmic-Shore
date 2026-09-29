using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The far end of the Wanderway tether: the station that ends the run — fly into it and you
    /// are returned to the cell with the belt switched off.
    ///
    /// It is a full <see cref="Toy"/> rather than a bespoke trigger so it inherits the whole
    /// shared contract for free: local-user-only detection, freestyle-only gating, the
    /// continuity-law bloom-in, the deferred activation (toy effects must not run inside a
    /// physics callback — this one teleports a vessel and stops an async belt), and the
    /// exit-gated re-arm. It carries no definition of its own; the conveyor toy hands it the
    /// Wanderway definition + context so its accent and gating match the toy that spawned it.
    /// </summary>
    public sealed class WanderwayReturnToy : Toy
    {
        Action _onActivated;

        /// <summary>Wire the end-the-run callback. Call before <see cref="Toy.Initialize"/>.</summary>
        public void Configure(Action onActivated) => _onActivated = onActivated;

        protected override void OnActivated(IVesselStatus localVessel) => _onActivated?.Invoke();
    }
}
