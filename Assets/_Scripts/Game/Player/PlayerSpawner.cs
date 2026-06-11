using CosmicShore.App.UI.Controllers;
using CosmicShore.Core;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace CosmicShore.Game
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField, RequireInterface((typeof(IPlayer)))]
        Object _playerPrefab;

        [FormerlySerializedAs("_shipSpawner")] [SerializeField]
        VesselSpawner vesselSpawner;

        public IPlayer SpawnPlayerAndShip(IPlayer.InitializeData data)
        {
            if (!data.AllowSpawning)
                return null;

            IPlayer player = (IPlayer)Instantiate(_playerPrefab);

            if (!vesselSpawner.SpawnShip(data.vesselClass, out IVessel ship) || ship == null)
            {
                CSDebug.LogError($"[PlayerSpawner] Failed to spawn vessel for class {data.vesselClass}. Destroying orphaned player.");
                if (player is Object obj)
                    Destroy(obj);
                return null;
            }

            player.InitializeForSinglePlayerMode(data, ship);
            ship.Initialize(player);

            return player;
        }
    }
}