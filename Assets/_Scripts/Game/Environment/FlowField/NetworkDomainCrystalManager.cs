using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public class NetworkDomainCrystalManager : NetworkCrystalManager
    {
        protected override Crystal Spawn(int crystalId, Vector3 spawnPos)
        {
            Domains domainToSet = Domains.None;
            var player = gameData.Players[crystalId - 1];
            if (player == null)
            {
                Debug.LogError("NO player found to get domain. Setting default domain!");
            }
            else
            {
                domainToSet = player.Domain;
            }

            var crystal = base.Spawn(crystalId, spawnPos);
            crystal.ChangeDomain(domainToSet);
            return crystal;
        }
    }
}