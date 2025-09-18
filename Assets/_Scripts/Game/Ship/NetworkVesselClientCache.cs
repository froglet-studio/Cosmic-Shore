using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Caches all active NetworkShip instances; must reside on the same GameObject as a NetworkShip and NetcodeHooks.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(VesselController))]
    public class NetworkVesselClientCache : NetworkClientCache<VesselController> // TODO - Try using IVessel or such later.
    {}
}
