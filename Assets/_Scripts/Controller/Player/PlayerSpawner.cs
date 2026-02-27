using CosmicShore.UI;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
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

        [Inject] Container _container;

        public IPlayer SpawnPlayerAndShip(IPlayer.InitializeData data)
        {
            if (!data.AllowSpawning)
                return null;

            IPlayer player = (IPlayer)Instantiate(_playerPrefab);
            if (player is Component comp)
                GameObjectInjector.InjectRecursive(comp.gameObject, _container);
            vesselSpawner.SpawnShip(data.vesselClass, out IVessel ship);
            player.InitializeForSinglePlayerMode(data, ship);
            ship.Initialize(player);

            return player;
        }
    }
}