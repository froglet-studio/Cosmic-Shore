using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptableAuthenticationData
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(AuthenticationData), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(AuthenticationData))]
    public class AuthenticationDataVariable : ScriptableVariable<AuthenticationData>
    {
            
    }
}
