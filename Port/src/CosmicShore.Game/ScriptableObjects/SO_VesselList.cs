// Ported verbatim from _Scripts/ScriptableObjects/SO_VesselList.cs (CloudSave arc
// 2026-07-09). Mechanical substitutions (README): UnityEngine / UnityEngine.Serialization
// → CosmicShore.Engine (ScriptableObject, CreateAssetMenu, FormerlySerializedAs).
// Global namespace kept (upstream declares none — the SO_Vessel precedent).
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.Utility;

[CreateAssetMenu(fileName = "New Vessel List", menuName = "CosmicShore/Vessel/VesselList", order = 12)]
[System.Serializable]
public class SO_VesselList : ScriptableObject
{
    [FormerlySerializedAs("ShipList")]
    public List<SO_Vessel> VesselList;

    public bool TryGetVesselByClass(VesselClassType vesselClass, out SO_Vessel vessel)
    {
        vessel = VesselList.FirstOrDefault(x => x.Class == vesselClass);
        if (vessel == null)
        {
            CSDebug.LogWarning($"Vessel of type {vesselClass} not found in VesselList.");
            return false;
        }
        return true;
    }
}
