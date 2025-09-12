using CosmicShore.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Game
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        
        [SerializeField, RequireInterface((typeof(IPlayer)))]
        Object _playerPrefab;

        [SerializeField] 
        ShipSpawner _shipSpawner;
        

        public IPlayer SpawnPlayerAndShip(IPlayer.InitializeData data)
        {
            if (!data.AllowSpawning)
                return null;
                
            IPlayer player = (IPlayer)Instantiate(_playerPrefab);
            _shipSpawner.SpawnShip(data.ShipClass, out IShip ship);
            player.Initialize(data, ship);
            ship.Initialize(player, data.EnableAIPilot);
            player.ToggleAutoPilotMode(data.EnableAIPilot);
            PlayerVesselInitializeHelper.SetShipProperties(_themeManagerData, ship);
            return player;
        }
    }
}