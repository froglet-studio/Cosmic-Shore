using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Variable_" + nameof(CellPhase),
        menuName = "ScriptableObjects/" + nameof(CellPhase))]
    public class CellPhaseVariable : ScriptableVariable<CellPhase>
    {
    }
}
