using CosmicShore.Engine.Soap;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Variable_" + nameof(ApplicationStateData),
        menuName = "ScriptableObjects/" + nameof(ApplicationStateData))]
    public class ApplicationStateDataVariable : ScriptableVariable<ApplicationStateData>
    {
    }
}
