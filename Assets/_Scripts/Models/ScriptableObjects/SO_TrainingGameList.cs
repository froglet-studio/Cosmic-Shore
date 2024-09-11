using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Training Game List", menuName = "CosmicShore/Game/TrainingGameList", order = 21)]
    [System.Serializable]
    public class SO_TrainingGameList : ScriptableObject
    {
        [FormerlySerializedAs("GameList")]
        public List<SO_TrainingGame> Games;
    }
}