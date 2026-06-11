using CosmicShore.UI;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
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
