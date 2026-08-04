using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Game List", menuName = "ScriptableObjects/Game/GameList", order = 21)]
    [System.Serializable]
    public class SO_GameList : ScriptableObject
    {
        [FormerlySerializedAs("GameList")]
        public List<SO_ArcadeGame> Games;
    }
}