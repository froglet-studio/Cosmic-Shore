using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Vessels that are INCORPOREAL right now: they pass through everything. An ability that makes its hull incorporeal
    /// switches the hull's colliders off itself (that covers prisms, crystals, skimmers and other hulls), and registers
    /// it here for the systems that test a vessel by POSITION rather than by collider - today the wormhole mouths
    /// (<see cref="WormholeMouth"/>), which would otherwise carry a hull that passes through one. Its one writer is the
    /// Tiger Moth's Drop (<c>R_VesselActions/TIGER_MOTH.md</c>). Per machine: every peer runs the ability's press, so
    /// every peer registers its own copy of the hull.
    /// </summary>
    public static class VesselIncorporeal
    {
        static readonly HashSet<Transform> Vessels = new();

        public static void Set(Transform vessel, bool incorporeal)
        {
            if (!vessel) return;
            if (incorporeal) Vessels.Add(vessel);
            else Vessels.Remove(vessel);
        }

        public static bool Is(Transform vessel) => vessel && Vessels.Contains(vessel);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Vessels.Clear();   // statics survive play-mode exit in the editor
    }
}
