using CosmicShore.Game;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class PlayerLoader : MonoBehaviour
    {
        [SerializeField] GameObject playerPrefab;
        [SerializeField] List<string> PlayerNames = new() { "FriendlyOne", "HostileOne", "HostileTwo" };
        [SerializeField] List<Vector3> PlayerPositions = new() { new Vector3(0, 55, 0), new Vector3(55, 0, 0), new Vector3(0, 0, 55) };
        [SerializeField] List<Teams> PlayerTeams = new() { Teams.Jade, Teams.Ruby, Teams.Ruby };
        [SerializeField] List<ShipTypes> PlayerShipTypes = new() { ShipTypes.Rhino, ShipTypes.Rhino, ShipTypes.Rhino };

        void Start()
        {
            InstantiateAndInitializePlayers();
        }

        public void InstantiateAndInitializePlayers()
        {
            for (var i = 0; i < PlayerTeams.Count; i++)
            {
                var playerClone = Instantiate(playerPrefab);
                playerClone.transform.position = PlayerPositions[i];
                playerClone.TryGetComponent(out IPlayer player);
                playerClone.name = PlayerNames[i];
                if (player == null)
                {
                    Debug.LogError($"Non player prefab provided to PlayerLoader");
                    return;
                }

                IPlayer.InitializeData data = new()
                {
                    DefaultShipType = PlayerShipTypes[i],
                    Team = PlayerTeams[i],
                    PlayerName = PlayerNames[i],
                    PlayerUUID = "Player" + (i + 1),
                    Name = "Player" + (i + 1)
                };
                player.Initialize(data);
                player.ToggleGameObject(true);
                player.ToggleActive(true);
            }
        }
    }
}