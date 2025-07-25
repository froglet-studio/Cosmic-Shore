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

        [SerializeField]
        bool _spawnAtStart;

        private void Start()
        {
            if (_spawnAtStart)
                SpawnDefaultPlayersAndShips();
        }

        [ContextMenu(("Spawn all Players and Ships"))]
        public void SpawnDefaultPlayersAndShips()
        {
            foreach (IPlayer.InitializeData data in _initializeDatas)
                SpawnPlayerAndShip(data);
        }

        public IPlayer SpawnPlayerAndShip(IPlayer.InitializeData data)
        {
            if (!data.AllowSpawning)
                return null;
                
            IPlayer player = (IPlayer)Instantiate(_playerPrefab);
            _shipSpawner.SpawnShip(data.ShipClass, out IShip ship);
            ship = Hangar.Instance.SetShipProperties(ship, data.Team, !data.EnableAIPilot);
            player.Initialize(data, ship);
            ship.Initialize(player, data.EnableAIPilot);
            player.ToggleAutoPilotMode(data.EnableAIPilot);
                
            if (!data.EnableAIPilot)
                CameraManager.Instance.Initialize(ship);
            
            return player;
        }
    }
}