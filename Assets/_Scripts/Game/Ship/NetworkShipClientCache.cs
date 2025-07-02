using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Caches all active NetworkShip instances; must reside on the same GameObject as a NetworkShip and NetcodeHooks.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(R_ShipBase))]
    public class NetworkShipClientCache : NetworkClientCache<R_ShipController> // TODO - Try using IShip or such later.
    {}
}
