using CosmicShore.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Game
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] 
        IPlayer.InitializeData[] _initializeDatas;

        [SerializeField, RequireInterface((typeof(IPlayer)))]
        Object _playerPrefab;

        [SerializeField] 
        ShipSpawner _shipSpawner;
        
        [ContextMenu(("Spawn all Players and Ships"))]
        public void SpawnAllPlayersAndShipsForSinglePlayer()
        {
            foreach (IPlayer.InitializeData data in _initializeDatas)
            {
                IPlayer player = (IPlayer)Instantiate(_playerPrefab);
                _shipSpawner.SpawnShip(data.ShipType, out IShip ship);
                ship = Hangar.Instance.SetShipProperties(ship, data.Team, !data.EnableAIPilot);
                player.Initialize(data, ship);
                ship.Initialize(player, data.EnableAIPilot);
                
                if (!data.EnableAIPilot)
                    CameraManager.Instance.Initialize(ship);
            }
        }
    }
}