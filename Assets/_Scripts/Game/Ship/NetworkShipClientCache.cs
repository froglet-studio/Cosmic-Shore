using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Caches all active NetworkShip instances; must reside on the same GameObject as a NetworkShip and NetcodeHooks.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(NetworkShip))]
    public class NetworkShipClientCache : NetworkClientCache<NetworkShip>
    {
        // Inherits all functionality from the generic base—
        // no additional code needed here.
    }
}
