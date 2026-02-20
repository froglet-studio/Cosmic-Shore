using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_variable_" + nameof(AuthenticationData), menuName = "Soap/ScriptableVariables/"+ nameof(AuthenticationData))]
    public class AuthenticationDataVariable : ScriptableVariable<AuthenticationData>
    {
            
    }
}
