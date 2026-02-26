using CosmicShore.UI;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace CosmicShore.Gameplay
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
            vesselSpawner.SpawnShip(data.vesselClass, out IVessel ship);
            player.InitializeForSinglePlayerMode(data, ship);
            ship.Initialize(player);

            return player;
        }
    }
}