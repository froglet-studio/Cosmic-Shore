using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(AuthenticationData), menuName = "ScriptableObjects/"+ nameof(AuthenticationData))]
    public class AuthenticationDataVariable : ScriptableVariable<AuthenticationData>
    {
            
    }
}
