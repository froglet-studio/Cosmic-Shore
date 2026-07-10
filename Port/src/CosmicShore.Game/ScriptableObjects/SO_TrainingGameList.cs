// Ported from Assets/_Scripts/ScriptableObjects/SO_TrainingGameList.cs (Arc F 2b-iii) — verbatim.
using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Training Game List", menuName = "ScriptableObjects/Game/TrainingGameList", order = 21)]
    [System.Serializable]
    public class SO_TrainingGameList : ScriptableObject
    {
        [FormerlySerializedAs("GameList")]
        public List<SO_TrainingGame> Games;
    }
}
