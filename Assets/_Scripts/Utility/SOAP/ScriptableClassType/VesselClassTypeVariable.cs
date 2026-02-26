using UnityEngine;
using Obvious.Soap;
using CosmicShore.Models.Enums;

namespace CosmicShore.Utility.SOAP
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(VesselClassType), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(VesselClassType))]
    public class VesselClassTypeVariable : ScriptableVariable<VesselClassType>
    {
        
    }
}
