using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace CosmicShore.Game
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        
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
            ship.Initialize(player, data.IsAI);
            PlayerVesselInitializeHelper.SetShipProperties(_themeManagerData, ship);
            return player;
        }
    }
}