using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptableClassType
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(VesselClassType), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(VesselClassType))]
    public class VesselClassTypeVariable : ScriptableVariable<VesselClassType>
    {
        
    }
}
