using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(AuthenticationData), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(AuthenticationData))]
    public class AuthenticationDataVariable : ScriptableVariable<AuthenticationData>
    {
            
    }
}
