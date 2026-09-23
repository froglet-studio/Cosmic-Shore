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

        /// <summary>
        /// Resolve <paramref name="vesselType"/>, reporting a miss as an ERROR. This is the
        /// DEMAND form — use it where the caller is about to spawn and a miss is a fault.
        /// </summary>
        public bool TryGetShipPrefab(VesselClassType vesselType, out Transform shipPrefabTransform)
            => TryGetShipPrefab(vesselType, out shipPrefabTransform, reportMissing: true);

        /// <summary>
        /// The same lookup as a QUESTION rather than a demand: <paramref name="reportMissing"/>
        /// false means a miss is an answer, not a fault.
        ///
        /// <para>The split exists because the two readings had one method. A roster PROBE — "which
        /// of these hulls exist on this build?" — is the normal way a declared-but-unbuilt vessel
        /// is skipped (<c>ToyVesselRoster.ResolveOffered</c>), and it runs every time a toy matrix
        /// is built, which is every domain change. Answering it through the demand form logged a
        /// LogError per rebuild for a hull whose prefab is simply not authored yet, which is both
        /// wrong (it is not a fault) and actively harmful: it buries the one keyed warning that
        /// names the actual fix under a red storm. <b>A question that cannot be asked without
        /// raising an error makes every asker either lie or shout.</b></para>
        /// </summary>
        public bool TryGetShipPrefab(VesselClassType vesselType, out Transform shipPrefabTransform,
            bool reportMissing)
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
                    if (!reportMissing) continue;
                    CSDebug.LogWarning(
                        $"[VesselPrefabContainer] Slot {i} is EMPTY. A slot goes empty when its " +
                        "prefab reference cannot be resolved — most often a prefab added to this " +
                        "asset outside the editor, or one whose .meta guid changed. Re-drag the " +
                        "prefab into the slot.");
                    continue;
                }

                if (!prefab.TryGetComponent(out IVesselStatus shipStatus))
                {
                    if (reportMissing)
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
                if (!reportMissing) return false;
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