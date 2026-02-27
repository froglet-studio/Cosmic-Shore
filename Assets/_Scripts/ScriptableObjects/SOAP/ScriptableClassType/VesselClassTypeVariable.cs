using UnityEngine;
using Obvious.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(VesselClassType), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(VesselClassType))]
    public class VesselClassTypeVariable : ScriptableVariable<VesselClassType>
    {
        
    }
}
