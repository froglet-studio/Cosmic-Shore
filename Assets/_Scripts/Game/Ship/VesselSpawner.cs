using System;
using System.Collections.Generic;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    public class VesselSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("_shipPrefabContainer")] [SerializeField]
        VesselPrefabContainer vesselPrefabContainer;

        [SerializeField] GameDataSO _gameData;

        public bool SpawnShip(VesselClassType vesselType, out IVessel vessel)
        {
            if (vesselType is VesselClassType.Random or VesselClassType.Any)
            {
                var allowedTypes = GetAllowedVesselTypes();

                if (allowedTypes.Count == 0)
                {
                    CSDebug.LogError("[VesselSpawner] No valid vessel types available for random selection.");
                    vessel = null;
                    return false;
                }

                vesselType = allowedTypes[UnityEngine.Random.Range(0, allowedTypes.Count)];
            }

            vessel = null;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefab))
            {
                CSDebug.LogError($"Could not find vessel prefab for {vesselType}");
                return false;
            }

            Instantiate(shipPrefab).TryGetComponent(out vessel);
            return true;
        }

        List<VesselClassType> GetAllowedVesselTypes()
        {
            // If we have a game config with a vessel list, constrain random selection to those
            if (_gameData != null && _gameData.CurrentArcadeGame != null
                && _gameData.CurrentArcadeGame.Vessels is { Count: > 0 })
            {
                var allowed = new List<VesselClassType>();
                foreach (var vessel in _gameData.CurrentArcadeGame.Vessels)
                {
                    if (vessel != null && !vessel.IsLocked)
                        allowed.Add(vessel.Class);
                }

                if (allowed.Count > 0)
                    return allowed;
            }

            // Fallback: all vessel types except meta-types
            var allTypes = new List<VesselClassType>();
            foreach (VesselClassType v in Enum.GetValues(typeof(VesselClassType)))
            {
                if (v is not VesselClassType.Random and not VesselClassType.Any)
                    allTypes.Add(v);
            }
            return allTypes;
        }
    }
}