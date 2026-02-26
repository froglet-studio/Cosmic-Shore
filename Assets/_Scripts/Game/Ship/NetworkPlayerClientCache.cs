using System.Linq;
using CosmicShore.Utility.Network;
using UnityEngine;
using CosmicShore.Models.Enums;
using Player = CosmicShore.Game.Player.Player;
namespace CosmicShore.Game.Ship
{
    /// <summary>
    /// Caches all active NetworkPlayer instances; must reside on the same GameObject as a NetworkPlayer and NetcodeHooks.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(Player))]
    public class NetworkPlayerClientCache : NetworkClientCache<Player>
    {
        // Inherits all functionality from the generic base—
        // no additional code needed here.

        public static Player GetPlayerByTeam(Domains domain) =>
            ActiveInstances.FirstOrDefault(player => player.Domain == domain);
    }
}
