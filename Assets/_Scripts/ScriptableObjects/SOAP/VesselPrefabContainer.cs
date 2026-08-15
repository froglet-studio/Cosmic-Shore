using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "DataContainer_VesselPrefab", menuName = "ScriptableObjects/Data Containers/VesselPrefabContainer")]
    public class VesselPrefabContainer : ScriptableObject
    {
        [SerializeField]
        Transform[] _shipPrefabs;

        public bool TryGetShipPrefab(VesselClassType vesselType, out Transform shipPrefabTransform)
        {
            shipPrefabTransform = null;

            if (_shipPrefabs == null || _shipPrefabs.Length == 0)
            {
                CSDebug.LogError("No Vessel Prefabs found! This should never happen!");
                return false;
            }

            // Track what we actually saw, so a miss can name the reason instead of just the
            // symptom. An EMPTY SLOT is the failure mode this list really has — a reference
            // authored against a prefab the editor had not yet imported resolves to null, the
            // inspector shows "None (Transform)", and the old code skipped it in total silence.
            // The vessel then reads as "not registered" everywhere downstream (no spawn, and the
            // vessel-changer toy falls back to its placeholder sphere), with nothing in the log
            // pointing at the slot.
            int emptySlots = 0;
            var seen = new List<VesselClassType>();

            for (int i = 0; i < _shipPrefabs.Length; i++)
            {
                var prefab = _shipPrefabs[i];
                if (prefab == null)
                {
                    emptySlots++;
                    CSDebug.LogWarning(
                        $"[VesselPrefabContainer] Slot {i} is EMPTY. A slot goes empty when its " +
                        "prefab reference cannot be resolved — most often a prefab added to this " +
                        "asset outside the editor, or one whose .meta guid changed. Re-drag the " +
                        "prefab into the slot.");
                    continue;
                }

                if (!prefab.TryGetComponent(out IVesselStatus shipStatus))
                {
                    CSDebug.LogWarning($"[VesselPrefabContainer] Slot {i} ({prefab.name}) has no " +
                                       "VesselStatus component - skipping. The slot must hold the " +
                                       "prefab's ROOT transform.");
                    continue;
                }

                seen.Add(shipStatus.VesselType);

                if (shipStatus.VesselType != vesselType)
                    continue;

                shipPrefabTransform = prefab.transform;
            }

            if (shipPrefabTransform == null)
            {
                CSDebug.LogError(
                    $"[VesselPrefabContainer] No prefab registered for vessel type {vesselType}. " +
                    $"{_shipPrefabs.Length} slot(s), {emptySlots} empty, resolved types: " +
                    $"[{string.Join(", ", seen)}].");
                return false;
            }

            return true;
        }
    }
}