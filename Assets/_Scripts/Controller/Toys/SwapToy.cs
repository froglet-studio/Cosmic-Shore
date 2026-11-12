using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A fly-through gate inside a run (a painting's stroke gate, built by
    /// <see cref="ToyFactory.CreateGate"/>). It carries no state; it just reports when the local
    /// vessel flies through it.
    /// </summary>
    public class SwapToy : ToyStation
    {
        /// <summary>Raised when the local player's vessel activates this toy (once per pass, in freestyle).</summary>
        public event Action<SwapToy> Activated;

        protected override void OnActivated(IVesselStatus localVessel) => Activated?.Invoke(this);
    }
}
