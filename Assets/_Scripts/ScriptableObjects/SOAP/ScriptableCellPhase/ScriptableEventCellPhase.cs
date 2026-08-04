using UnityEngine;
using Obvious.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(CellPhase),
        menuName = "ScriptableObjects/Events/" + nameof(CellPhase))]
    public class ScriptableEventCellPhase : ScriptableEvent<CellPhase>
    {
    }
}
