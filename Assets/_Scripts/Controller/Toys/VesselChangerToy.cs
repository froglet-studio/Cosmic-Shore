using System;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cycles the local player's vessel class on each pass, via the existing networked swap
    /// pipeline (<see cref="MenuServerPlayerVesselInitializer.RequestSwap"/>). The previous
    /// freestyle vessel-change lived in the pause menu (which now backs out to the app shell);
    /// this is the in-world replacement — fly through to hop to the next ship.
    /// </summary>
    public class VesselChangerToy : Toy
    {
        static readonly VesselClassType[] DefaultCycle =
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
            VesselClassType.Urchin, VesselClassType.Grizzly, VesselClassType.Squirrel,
            VesselClassType.Serpent, VesselClassType.Termite, VesselClassType.Falcon,
            VesselClassType.Shrike, VesselClassType.Sparrow,
        };

        VesselClassType[] _cycle;

        public void SetCycle(VesselClassType[] cycle) => _cycle = cycle;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            var initializer = Context?.VesselInitializer;
            if (!initializer)
            {
                CSDebug.LogWarning("[VesselChangerToy] No MenuServerPlayerVesselInitializer in context — cannot swap vessel.");
                return;
            }
            if (initializer.IsSwapping) return;

            var cycle = (_cycle is { Length: > 0 }) ? _cycle : DefaultCycle;
            int idx = Array.IndexOf(cycle, localVessel.VesselType);
            // idx < 0 (current vessel not in the cycle) wraps to the first entry.
            var next = cycle[((idx + 1) % cycle.Length + cycle.Length) % cycle.Length];
            initializer.RequestSwap(next);
        }
    }
}
