using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// DEPRECATED SCRIPT
    /// </summary>
    public class PlayerLoader : MonoBehaviour
    {
        [SerializeField] GameObject playerPrefab;
        [SerializeField] List<string> PlayerNames = new() { "FriendlyOne", "HostileOne", "HostileTwo" };
        [SerializeField] List<Domains> PlayerTeams = new() { Domains.Jade, Domains.Ruby, Domains.Ruby };
        [SerializeField] List<VesselClassType> PlayerShipTypes = new() { VesselClassType.Rhino, VesselClassType.Rhino, VesselClassType.Rhino };

        void Start()
        {
            InstantiateAndInitializePlayers();
        }

        public void InstantiateAndInitializePlayers()
        {
            for (var i = 0; i < PlayerTeams.Count; i++)
            {
                Instantiate(playerPrefab).TryGetComponent(out IPlayer player);
                if (player == null)
                {
                    Debug.LogError($"Non player prefab provided to PlayerLoader");
                    return;
                }

                IPlayer.InitializeData data = new()
                {
                    vesselClass = PlayerShipTypes[i],
                    domain = PlayerTeams[i],
                    PlayerName = PlayerNames[i],
                    PlayerUUID = "Player" + (i + 1),
                };
                // player.Initialize(data);
                player.ToggleGameObject(true);
                player.ToggleActive(true);
            }
        }
    }
}