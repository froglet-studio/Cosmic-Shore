using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Variable_" + nameof(ApplicationStateData),
        menuName = "ScriptableObjects/Variables/" + nameof(ApplicationStateData))]
    public class ApplicationStateDataVariable : ScriptableVariable<ApplicationStateData>
    {
    }
}
