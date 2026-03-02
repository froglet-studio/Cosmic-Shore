using System;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class VesselSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("_shipPrefabContainer")] [SerializeField]
        VesselPrefabContainer vesselPrefabContainer;

        [Inject] Container _container;

        public bool SpawnShip(VesselClassType vesselType, out IVessel vessel)
        {
            if (vesselType is VesselClassType.Random or VesselClassType.Any)
            {
                var values = Enum.GetValues(typeof(VesselClassType));
                // Build a list excluding Any and Random to avoid infinite loops
                var validTypes = new System.Collections.Generic.List<VesselClassType>();
                foreach (VesselClassType v in values)
                {
                    if (v is not VesselClassType.Random and not VesselClassType.Any)
                        validTypes.Add(v);
                }

                if (validTypes.Count == 0)
                {
                    CSDebug.LogError("[VesselSpawner] No valid vessel types available for random selection.");
                    vessel = null;
                    return false;
                }

                vesselType = validTypes[UnityEngine.Random.Range(0, validTypes.Count)];
            }

            vessel = null;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefab))
            {
                CSDebug.LogError($"Could not find vessel prefab for {vesselType}");
                return false;
            }

            var spawned = Instantiate(shipPrefab);
            GameObjectInjector.InjectRecursive(spawned.gameObject, _container);
            spawned.TryGetComponent(out vessel);
            return true;
        }
    }
}