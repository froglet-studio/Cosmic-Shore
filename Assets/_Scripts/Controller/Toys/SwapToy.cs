using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A toy that belongs to a <see cref="SwapToySetCoordinator{T}"/> - a station representing one
    /// option (a domain, a vessel). It carries no option state itself; the coordinator owns the
    /// option→visual mapping and the flip logic. The toy just reports when the local vessel flies
    /// through it.
    /// </summary>
    public class SwapToy : Toy
    {
        /// <summary>Raised when the local player's vessel activates this toy (once per pass, in freestyle).</summary>
        public event Action<SwapToy> Activated;

        protected override void OnActivated(IVesselStatus localVessel) => Activated?.Invoke(this);
    }
}
