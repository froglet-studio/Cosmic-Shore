using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Caches all active NetworkShip instances; must reside on the same GameObject as a NetworkShip and NetcodeHooks.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(VesselController))]
    public class NetworkVesselClientCache : NetworkClientCache<VesselController> // TODO - Try using IVessel or such later.
    {}
}
