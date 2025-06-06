using System.Linq;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Caches all active NetworkPlayer instances; must reside on the same GameObject as a NetworkPlayer and NetcodeHooks.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(NetworkPlayer))]
    public class NetworkPlayerClientCache : NetworkClientCache<NetworkPlayer>
    {
        // Inherits all functionality from the generic base—
        // no additional code needed here.

        public static NetworkPlayer GetPlayerByTeam(Teams team) =>
            ActiveInstances.FirstOrDefault(player => player.Team == team);
    }
}
